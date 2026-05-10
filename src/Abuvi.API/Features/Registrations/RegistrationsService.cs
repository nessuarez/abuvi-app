using System.Text;
using System.Text.RegularExpressions;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Payments;
using Microsoft.Extensions.Logging;

namespace Abuvi.API.Features.Registrations;

public class RegistrationsService(
    IRegistrationsRepository registrationsRepo,
    IRegistrationExtrasRepository extrasRepo,
    IRegistrationAccommodationPreferencesRepository accommodationPrefsRepo,
    IFamilyUnitsRepository familyUnitsRepo,
    ICampEditionsRepository campEditionsRepo,
    ICampEditionAccommodationsRepository accommodationsRepo,
    ICampEditionExtrasRepository extrasDefinitionRepo,
    RegistrationPricingService pricingService,
    IEmailService emailService,
    Payments.IPaymentsService paymentsService,
    IMembershipsRepository membershipsRepo,
    IRegistrationAccommodationNeedsRepository accommodationNeedsRepo,
    IRegistrationFriendLinksRepository friendLinksRepo,
    IAccommodationFeaturesRepository accommodationFeaturesRepo,
    ILogger<RegistrationsService> logger)
{
    public async Task<RegistrationResponse> CreateAsync(
        Guid userId, CreateRegistrationRequest request, CancellationToken ct)
    {
        // 1. Load FamilyUnit
        var familyUnit = await familyUnitsRepo.GetFamilyUnitByIdAsync(request.FamilyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", request.FamilyUnitId);

        // 1b. Validate family unit is active
        if (!familyUnit.IsActive)
            throw new BusinessRuleException(
                "La unidad familiar está desactivada. Contacte al administrador.");

        // 1c. Check current year membership fee (non-blocking during launch)
        // TODO: After launch, enable strict validation of membership fees
        var hasPaidCurrentYearFee = await membershipsRepo
            .HasPaidCurrentYearFeeForFamilyAsync(request.FamilyUnitId, ct);
        if (!hasPaidCurrentYearFee)
        {
            logger.LogWarning(
                "Registration {RegistrationId} created for family {FamilyUnitId} " +
                "without verified membership fee. Manual verification required.",
                Guid.NewGuid(), request.FamilyUnitId);
        }

        // 2. Verify representative
        if (familyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para inscribir esta unidad familiar");

        // 3. Load CampEdition
        var edition = await campEditionsRepo.GetByIdAsync(request.CampEditionId, ct)
            ?? throw new NotFoundException("Edición de Campamento", request.CampEditionId);

        // 4. Verify status
        if (edition.Status != CampEditionStatus.Open)
            throw new BusinessRuleException("La edición del campamento no está abierta para inscripción");

        // 5. Check duplicate
        if (await registrationsRepo.ExistsAsync(request.FamilyUnitId, request.CampEditionId, ct))
            throw new BusinessRuleException("Ya existe una inscripción para esta familia en este campamento");

        // 6. Load and validate members + calculate pricing
        var registrationMembers = new List<RegistrationMember>();
        foreach (var m in request.Members)
        {
            var member = await familyUnitsRepo.GetFamilyMemberByIdAsync(m.MemberId, ct)
                ?? throw new NotFoundException("Miembro Familiar", m.MemberId);

            if (member.FamilyUnitId != request.FamilyUnitId)
                throw new BusinessRuleException(
                    $"El miembro {member.FirstName} {member.LastName} no pertenece a esta unidad familiar");

            // Validate visit dates within camp bounds for WeekendVisit members
            if (m.AttendancePeriod == AttendancePeriod.WeekendVisit)
            {
                var campStart = DateOnly.FromDateTime(edition.StartDate);
                var campEnd = DateOnly.FromDateTime(edition.EndDate);
                if (m.VisitStartDate < campStart || m.VisitEndDate > campEnd)
                    throw new BusinessRuleException(
                        "Las fechas de la visita deben estar dentro del periodo del campamento");
            }

            var age = pricingService.CalculateAge(member.DateOfBirth, edition.StartDate);
            var category = await pricingService.GetAgeCategoryAsync(age, edition, ct);
            var price = pricingService.GetPriceForCategory(category, m.AttendancePeriod, edition);

            registrationMembers.Add(new RegistrationMember
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = m.MemberId,
                AgeAtCamp = age,
                AgeCategory = category,
                IndividualAmount = price,
                AttendancePeriod = m.AttendancePeriod,
                VisitStartDate = m.VisitStartDate,
                VisitEndDate = m.VisitEndDate,
                GuardianName = m.GuardianName,
                GuardianDocumentNumber = m.GuardianDocumentNumber,
                CreatedAt = DateTime.UtcNow
            });
        }

        // 7. Capacity check (per-period + weekend pool)
        // TODO: wrap in REPEATABLE READ transaction for production correctness
        if (edition.MaxCapacity.HasValue)
        {
            foreach (var rm in registrationMembers)
            {
                // Skip WeekendVisit — handled separately below
                if (rm.AttendancePeriod == AttendancePeriod.WeekendVisit) continue;

                var periodsToCheck = rm.AttendancePeriod == AttendancePeriod.Complete
                    ? new[] { AttendancePeriod.FirstWeek, AttendancePeriod.SecondWeek }
                    : new[] { rm.AttendancePeriod };

                foreach (var p in periodsToCheck)
                {
                    var count = await registrationsRepo
                        .CountConcurrentAttendeesByPeriodAsync(request.CampEditionId, p, ct);
                    if (count + 1 > edition.MaxCapacity.Value)
                        throw new BusinessRuleException(
                            "El campamento ha alcanzado su capacidad máxima para ese periodo");
                }
            }
        }

        // Weekend capacity check (separate pool)
        var weekendMembersCount = registrationMembers.Count(rm =>
            rm.AttendancePeriod == AttendancePeriod.WeekendVisit);
        if (weekendMembersCount > 0)
        {
            var weekendCap = edition.MaxWeekendCapacity ?? edition.MaxCapacity;
            if (weekendCap.HasValue)
            {
                var weekendCount = await registrationsRepo
                    .CountConcurrentAttendeesByPeriodAsync(
                        request.CampEditionId, AttendancePeriod.WeekendVisit, ct);
                if (weekendCount + weekendMembersCount > weekendCap.Value)
                    throw new BusinessRuleException(
                        "El campamento ha alcanzado su capacidad máxima para visitas de fin de semana");
            }
        }

        // 8. Calculate totals
        var baseTotalAmount = registrationMembers.Sum(m => m.IndividualAmount);

        // 9. Build Registration
        var notes = request.Notes ?? "";
        if (!hasPaidCurrentYearFee)
        {
            notes += (string.IsNullOrWhiteSpace(notes) ? "" : " | ") +
                "[PENDIENTE: Validación de membresía y cuota de 2026]";
        }

        var registration = new Registration
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = request.FamilyUnitId,
            CampEditionId = request.CampEditionId,
            RegisteredByUserId = userId,
            BaseTotalAmount = baseTotalAmount,
            ExtrasAmount = 0m,
            TotalAmount = baseTotalAmount,
            Status = RegistrationStatus.Pending,
            Notes = notes,
            SpecialNeeds = request.SpecialNeeds,
            CampatesPreference = request.CampatesPreference,
            HasPet = request.HasPet,
            Members = registrationMembers
        };

        // 10. Save
        await registrationsRepo.AddAsync(registration, ct);

        // 11. Log
        logger.LogInformation(
            "Registration {RegistrationId} created for family {FamilyUnitId} in edition {EditionId}",
            registration.Id, request.FamilyUnitId, request.CampEditionId);

        // 12. Create payment installments
        var installments = await paymentsService.CreateInstallmentsAsync(registration.Id, ct);

        // 13. Reload and return (includes newly created payments)
        var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registration.Id, ct)
            ?? throw new NotFoundException("Inscripción", registration.Id);

        // 14. Send confirmation email (non-blocking on failure)
        try
        {
            var paymentSettings = await paymentsService.GetPaymentSettingsAsync(ct);
            var emailData = BuildRegistrationEmailData(detailed, edition, installments, paymentSettings);
            await emailService.SendCampRegistrationConfirmationAsync(emailData, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send registration confirmation email for {RegistrationId}",
                registration.Id);
        }

        return detailed.ToResponse(amountPaid: 0m);
    }

    public async Task<RegistrationResponse> UpdateMembersAsync(
        Guid registrationId, Guid userId, UpdateRegistrationMembersRequest request, CancellationToken ct)
    {
        // 1. Load registration with payments for proof guard
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        // 2. Load FamilyUnit and verify representative
        var familyUnit = await familyUnitsRepo.GetFamilyUnitByIdAsync(registration.FamilyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", registration.FamilyUnitId);

        if (familyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para modificar esta inscripción");

        // 3. Verify status (Pending or Draft are both editable for the representative)
        if (registration.Status != RegistrationStatus.Pending && registration.Status != RegistrationStatus.Draft)
            throw new BusinessRuleException("Solo se pueden modificar inscripciones en estado Pendiente o Borrador");

        // 3b. Guard: block if any payment has a proof uploaded
        if (registration.Payments?.Any(p => p.ProofFileUrl != null) == true)
            throw new BusinessRuleException("No se pueden modificar los miembros porque ya hay un justificante de pago subido.");

        // 4. Load edition for pricing
        var edition = await campEditionsRepo.GetByIdAsync(registration.CampEditionId, ct)
            ?? throw new NotFoundException("Edición de Campamento", registration.CampEditionId);

        // 5. Validate and price new members
        var newMembers = new List<RegistrationMember>();
        foreach (var m in request.Members)
        {
            var member = await familyUnitsRepo.GetFamilyMemberByIdAsync(m.MemberId, ct)
                ?? throw new NotFoundException("Miembro Familiar", m.MemberId);

            if (member.FamilyUnitId != registration.FamilyUnitId)
                throw new BusinessRuleException(
                    $"El miembro {member.FirstName} {member.LastName} no pertenece a esta unidad familiar");

            // Validate visit dates within camp bounds for WeekendVisit members
            if (m.AttendancePeriod == AttendancePeriod.WeekendVisit)
            {
                var campStart = DateOnly.FromDateTime(edition.StartDate);
                var campEnd = DateOnly.FromDateTime(edition.EndDate);
                if (m.VisitStartDate < campStart || m.VisitEndDate > campEnd)
                    throw new BusinessRuleException(
                        "Las fechas de la visita deben estar dentro del periodo del campamento");
            }

            var age = pricingService.CalculateAge(member.DateOfBirth, edition.StartDate);
            var category = await pricingService.GetAgeCategoryAsync(age, edition, ct);
            var price = pricingService.GetPriceForCategory(category, m.AttendancePeriod, edition);

            newMembers.Add(new RegistrationMember
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                FamilyMemberId = m.MemberId,
                AgeAtCamp = age,
                AgeCategory = category,
                IndividualAmount = price,
                AttendancePeriod = m.AttendancePeriod,
                VisitStartDate = m.VisitStartDate,
                VisitEndDate = m.VisitEndDate,
                GuardianName = m.GuardianName,
                GuardianDocumentNumber = m.GuardianDocumentNumber,
                CreatedAt = DateTime.UtcNow
            });
        }

        // 5b. Capacity check for updated members
        // TODO: wrap in REPEATABLE READ transaction for production correctness
        if (edition.MaxCapacity.HasValue)
        {
            foreach (var rm in newMembers)
            {
                if (rm.AttendancePeriod == AttendancePeriod.WeekendVisit) continue;

                var periodsToCheck = rm.AttendancePeriod == AttendancePeriod.Complete
                    ? new[] { AttendancePeriod.FirstWeek, AttendancePeriod.SecondWeek }
                    : new[] { rm.AttendancePeriod };

                foreach (var p in periodsToCheck)
                {
                    var count = await registrationsRepo
                        .CountConcurrentAttendeesByPeriodAsync(registration.CampEditionId, p, ct);
                    if (count + 1 > edition.MaxCapacity.Value)
                        throw new BusinessRuleException(
                            "El campamento ha alcanzado su capacidad máxima para ese periodo");
                }
            }
        }

        var weekendCount2 = newMembers.Count(rm => rm.AttendancePeriod == AttendancePeriod.WeekendVisit);
        if (weekendCount2 > 0)
        {
            var weekendCap = edition.MaxWeekendCapacity ?? edition.MaxCapacity;
            if (weekendCap.HasValue)
            {
                var existingWeekendCount = await registrationsRepo
                    .CountConcurrentAttendeesByPeriodAsync(
                        registration.CampEditionId, AttendancePeriod.WeekendVisit, ct);
                if (existingWeekendCount + weekendCount2 > weekendCap.Value)
                    throw new BusinessRuleException(
                        "El campamento ha alcanzado su capacidad máxima para visitas de fin de semana");
            }
        }

        // 6. Delete existing members
        await registrationsRepo.DeleteMembersByRegistrationIdAsync(registrationId, ct);

        // 7-8. Recalculate and update
        var oldBaseTotalAmount = registration.BaseTotalAmount;
        var baseTotalAmount = newMembers.Sum(m => m.IndividualAmount);
        registration.BaseTotalAmount = baseTotalAmount;
        registration.TotalAmount = baseTotalAmount + registration.ExtrasAmount;

        // 9. Save registration scalars, then add new members separately
        await registrationsRepo.UpdateAsync(registration, ct);
        await registrationsRepo.AddMembersAsync(newMembers, ct);

        // 10. Sync P1/P2 installments to reflect new base total
        await paymentsService.SyncBaseInstallmentsAsync(
            registrationId, baseTotalAmount, oldBaseTotalAmount, ct);

        // 11. Reload and return
        var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var amountPaid = detailed.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return detailed.ToResponse(amountPaid);
    }

    public async Task<RegistrationResponse> SetExtrasAsync(
        Guid registrationId, Guid userId, UpdateRegistrationExtrasRequest request, CancellationToken ct)
    {
        // 1. Load registration with details
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        // 2. Verify representative
        if (registration.FamilyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para modificar esta inscripción");

        // 3. Verify status (Pending or Draft are both editable for the representative)
        if (registration.Status != RegistrationStatus.Pending && registration.Status != RegistrationStatus.Draft)
            throw new BusinessRuleException("Solo se pueden modificar inscripciones en estado Pendiente o Borrador");

        // 3b. Guard: block if any payment has a proof uploaded
        if (registration.Payments?.Any(p => p.ProofFileUrl != null) == true)
            throw new BusinessRuleException("No se pueden modificar los extras porque ya hay un justificante de pago subido.");

        // 4. Calculate duration
        var campDurationDays = (registration.CampEdition.EndDate - registration.CampEdition.StartDate).Days;

        // 5. Validate and build extras
        var newExtras = new List<RegistrationExtra>();
        foreach (var extraReq in request.Extras)
        {
            var extra = await campEditionsRepo.GetExtraByIdAsync(extraReq.CampEditionExtraId, ct)
                ?? throw new NotFoundException("Extra de Campamento", extraReq.CampEditionExtraId);

            if (extra.CampEditionId != registration.CampEditionId)
                throw new BusinessRuleException(
                    $"El extra '{extra.Name}' no pertenece a esta edición del campamento");

            if (extra.MaxQuantity.HasValue && extraReq.Quantity > extra.MaxQuantity.Value)
                throw new BusinessRuleException(
                    $"La cantidad solicitada para '{extra.Name}' supera la cantidad máxima permitida ({extra.MaxQuantity.Value})");

            if (!extra.IsActive)
                throw new BusinessRuleException($"El extra '{extra.Name}' no está disponible");

            var totalAmount = pricingService.CalculateExtraAmount(extra, extraReq.Quantity, campDurationDays);

            newExtras.Add(new RegistrationExtra
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                CampEditionExtraId = extraReq.CampEditionExtraId,
                Quantity = extraReq.Quantity,
                UnitPrice = extra.Price,              // price snapshot
                CampDurationDays = campDurationDays,  // duration snapshot
                TotalAmount = totalAmount,
                UserInput = extraReq.UserInput
            });
        }

        // 6. Delete and re-add
        await extrasRepo.DeleteByRegistrationIdAsync(registrationId, ct);
        await extrasRepo.AddRangeAsync(newExtras, ct);

        // 7. Update totals
        var newExtrasAmount = newExtras.Sum(e => e.TotalAmount);
        registration.ExtrasAmount = newExtrasAmount;
        registration.TotalAmount = registration.BaseTotalAmount + newExtrasAmount;
        await registrationsRepo.UpdateAsync(registration, ct);

        // 8. Sync P3 extras installment
        await paymentsService.SyncExtrasInstallmentAsync(registrationId, newExtrasAmount, ct);

        // 9. Reload and return
        var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var amountPaid = detailed.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return detailed.ToResponse(amountPaid);
    }

    public async Task<RegistrationResponse> UpdateInfoAsync(
        Guid registrationId, Guid userId, UpdateRegistrationInfoRequest request, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var familyUnit = await familyUnitsRepo.GetFamilyUnitByIdAsync(registration.FamilyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", registration.FamilyUnitId);

        if (familyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para modificar esta inscripción");

        if (registration.Status != RegistrationStatus.Pending && registration.Status != RegistrationStatus.Draft)
            throw new BusinessRuleException("Solo se pueden modificar inscripciones en estado Pendiente o Borrador");

        registration.SpecialNeeds = string.IsNullOrWhiteSpace(request.SpecialNeeds) ? null : request.SpecialNeeds.Trim();
        registration.CampatesPreference = string.IsNullOrWhiteSpace(request.CampatesPreference) ? null : request.CampatesPreference.Trim();
        registration.HasPet = request.HasPet;
        registration.UpdatedAt = DateTime.UtcNow;

        await registrationsRepo.UpdateAsync(registration, ct);

        var amountPaid = registration.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return registration.ToResponse(amountPaid);
    }

    public async Task<CancelRegistrationResponse> CancelAsync(
        Guid registrationId, Guid userId, bool isAdminOrBoard, CancellationToken ct)
    {
        // 1. Load registration
        var registration = await registrationsRepo.GetByIdAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        // 2. Verify representative (unless admin/board)
        if (!isAdminOrBoard)
        {
            var familyUnit = await familyUnitsRepo.GetFamilyUnitByIdAsync(registration.FamilyUnitId, ct)
                ?? throw new NotFoundException("Unidad Familiar", registration.FamilyUnitId);

            if (familyUnit.RepresentativeUserId != userId)
                throw new BusinessRuleException("No tienes permiso para cancelar esta inscripción");
        }

        // 3. Check if already cancelled
        if (registration.Status == RegistrationStatus.Cancelled)
            throw new BusinessRuleException("La inscripción ya ha sido cancelada");

        // 4. Cancel
        var previousStatus = registration.Status;
        registration.Status = RegistrationStatus.Cancelled;
        await registrationsRepo.UpdateAsync(registration, ct);

        await LogStatusHistoryAsync(registrationId, previousStatus, RegistrationStatus.Cancelled,
            userId, StatusChangeTrigger.AdminAction, null, ct);

        logger.LogInformation(
            "Registration {RegistrationId} cancelled by user {UserId}", registrationId, userId);

        // 5. Send cancellation email (non-blocking on failure)
        try
        {
            var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
                ?? throw new NotFoundException("Inscripción", registrationId);
            var edition = detailed.CampEdition;
            var emailData = BuildRegistrationEmailData(detailed, edition);
            await emailService.SendCampRegistrationCancellationAsync(emailData, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send registration cancellation email for {RegistrationId}",
                registrationId);
        }

        return new CancelRegistrationResponse("Inscripción cancelada correctamente");
    }

    public async Task<List<AvailableCampEditionResponse>> GetAvailableEditionsAsync(CancellationToken ct)
    {
        var editions = await campEditionsRepo.GetOpenEditionsAsync(ct);
        var result = new List<AvailableCampEditionResponse>();

        // Load global age ranges for the response
        var setting = await pricingService.TryGetGlobalAgeRangesAsync(ct);

        foreach (var edition in editions)
        {
            // Count current registrations for display (keep backward compatibility)
            var currentCount = await registrationsRepo.CountActiveByEditionAsync(edition.Id, ct);

            // Per-period spotsRemaining (most constrained period)
            int? spotsRemaining = null;
            if (edition.MaxCapacity.HasValue)
            {
                var firstWeekCount = await registrationsRepo
                    .CountConcurrentAttendeesByPeriodAsync(edition.Id, AttendancePeriod.FirstWeek, ct);
                var secondWeekCount = await registrationsRepo
                    .CountConcurrentAttendeesByPeriodAsync(edition.Id, AttendancePeriod.SecondWeek, ct);
                spotsRemaining = Math.Max(0,
                    edition.MaxCapacity.Value - Math.Max(firstWeekCount, secondWeekCount));
            }

            // Weekend spots remaining (separate pool)
            int? weekendSpotsRemaining = null;
            if (edition.WeekendStartDate.HasValue)
            {
                var weekendCap = edition.MaxWeekendCapacity ?? edition.MaxCapacity;
                if (weekendCap.HasValue)
                {
                    var weekendCount = await registrationsRepo
                        .CountConcurrentAttendeesByPeriodAsync(edition.Id, AttendancePeriod.WeekendVisit, ct);
                    weekendSpotsRemaining = Math.Max(0, weekendCap.Value - weekendCount);
                }
            }

            var ageRangesInfo = edition.UseCustomAgeRanges
                ? new AgeRangesInfo(
                    edition.CustomBabyMaxAge ?? setting?.BabyMaxAge ?? 3,
                    edition.CustomChildMinAge ?? setting?.ChildMinAge ?? 4,
                    edition.CustomChildMaxAge ?? setting?.ChildMaxAge ?? 17,
                    edition.CustomAdultMinAge ?? setting?.AdultMinAge ?? 18)
                : new AgeRangesInfo(
                    setting?.BabyMaxAge ?? 3,
                    setting?.ChildMinAge ?? 4,
                    setting?.ChildMaxAge ?? 17,
                    setting?.AdultMinAge ?? 18);

            result.Add(new AvailableCampEditionResponse(
                Id: edition.Id,
                CampName: edition.Camp.Name,
                Year: edition.Year,
                StartDate: edition.StartDate,
                EndDate: edition.EndDate,
                Location: edition.Camp.Location,
                PricePerAdult: edition.PricePerAdult,
                PricePerChild: edition.PricePerChild,
                PricePerBaby: edition.PricePerBaby,
                MaxCapacity: edition.MaxCapacity,
                CurrentRegistrations: currentCount,
                SpotsRemaining: spotsRemaining,
                Status: edition.Status.ToString(),
                AgeRanges: ageRangesInfo,
                AllowsPartialAttendance: edition.PricePerAdultWeek is not null,
                PricePerAdultWeek: edition.PricePerAdultWeek,
                PricePerChildWeek: edition.PricePerChildWeek,
                PricePerBabyWeek: edition.PricePerBabyWeek,
                HalfDate: edition.HalfDate,
                FirstWeekDays: RegistrationPricingService.GetPeriodDays(AttendancePeriod.FirstWeek, edition),
                SecondWeekDays: RegistrationPricingService.GetPeriodDays(AttendancePeriod.SecondWeek, edition),
                AllowsWeekendVisit: edition.WeekendStartDate.HasValue && edition.PricePerAdultWeekend.HasValue,
                PricePerAdultWeekend: edition.PricePerAdultWeekend,
                PricePerChildWeekend: edition.PricePerChildWeekend,
                PricePerBabyWeekend: edition.PricePerBabyWeekend,
                WeekendStartDate: edition.WeekendStartDate,
                WeekendEndDate: edition.WeekendEndDate,
                WeekendDays: RegistrationPricingService.GetPeriodDays(AttendancePeriod.WeekendVisit, edition),
                MaxWeekendCapacity: edition.MaxWeekendCapacity,
                WeekendSpotsRemaining: weekendSpotsRemaining,
                FirstPaymentDeadline: edition.FirstPaymentDeadline,
                SecondPaymentDeadline: edition.SecondPaymentDeadline));
        }

        return result;
    }

    public async Task<RegistrationResponse> GetByIdAsync(
        Guid registrationId, Guid userId, bool isAdminOrBoard, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (!isAdminOrBoard)
        {
            var isRepresentative = registration.FamilyUnit.RepresentativeUserId == userId;
            if (!isRepresentative)
            {
                var memberUnit = await familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(userId, ct);
                if (memberUnit?.Id != registration.FamilyUnitId)
                    throw new BusinessRuleException("No tienes permiso para ver esta inscripción");
            }
        }

        var amountPaid = registration.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        if (!isAdminOrBoard)
            return registration.ToResponse(amountPaid);

        var needs = await accommodationNeedsRepo.GetByRegistrationIdAsync(registrationId, ct);
        var friendLinks = await friendLinksRepo.GetByRegistrationIdAsync(registrationId, ct);

        return registration.ToAdminResponse(
            amountPaid,
            needs.Select(n => new AccommodationNeedResponse(
                n.AccommodationFeatureId,
                n.AccommodationFeature.Name,
                n.AccommodationFeature.ApplicabilityLevel.ToString(),
                n.TaggedByUserId,
                n.CreatedAt)).ToList(),
            friendLinks.Select(l => new FriendLinkResponse(
                l.LinkedRegistrationId,
                l.LinkedRegistration.FamilyUnit.Name,
                l.CreatedByUserId,
                l.CreatedAt)).ToList());
    }

    public async Task<List<RegistrationListResponse>> GetByFamilyUnitAsync(Guid userId, CancellationToken ct)
    {
        var familyUnit = await familyUnitsRepo.GetFamilyUnitByRepresentativeIdAsync(userId, ct)
                      ?? await familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(userId, ct);
        if (familyUnit is null) return [];

        var registrations = await registrationsRepo.GetByFamilyUnitAsync(familyUnit.Id, ct);

        return registrations.Select(r =>
        {
            var amountPaid = r.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Sum(p => p.Amount);

            return new RegistrationListResponse(
                Id: r.Id,
                FamilyUnit: new RegistrationFamilyUnitSummary(r.FamilyUnit.Id, r.FamilyUnit.Name, r.FamilyUnit.RepresentativeUserId),
                CampEdition: new RegistrationCampEditionSummary(
                    r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
                    r.CampEdition.StartDate, r.CampEdition.EndDate,
                    (r.CampEdition.EndDate - r.CampEdition.StartDate).Days,
                    r.CampEdition.Camp.Location),
                Status: r.Status,
                TotalAmount: r.TotalAmount,
                AmountPaid: amountPaid,
                AmountRemaining: r.TotalAmount - amountPaid,
                CreatedAt: r.CreatedAt);
        }).ToList();
    }

    public async Task<List<AccommodationPreferenceResponse>> SetAccommodationPreferencesAsync(
        Guid registrationId, Guid userId, bool isAdminOrBoard,
        UpdateRegistrationAccommodationPreferencesRequest request, CancellationToken ct)
    {
        // 1. Load registration with details
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        // 2. Verify representative or admin/board
        if (!isAdminOrBoard && registration.FamilyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para modificar esta inscripción");

        // 3. Verify status
        if (registration.Status != RegistrationStatus.Pending)
            throw new BusinessRuleException("Solo se pueden modificar inscripciones en estado Pendiente");

        // 4. Validate each accommodation exists, belongs to edition, and is active
        var newPreferences = new List<RegistrationAccommodationPreference>();
        foreach (var pref in request.Preferences)
        {
            var accommodation = await accommodationsRepo.GetByIdAsync(pref.CampEditionAccommodationId, ct)
                ?? throw new NotFoundException("Alojamiento", pref.CampEditionAccommodationId);

            if (accommodation.CampEditionId != registration.CampEditionId)
                throw new BusinessRuleException(
                    $"El alojamiento '{accommodation.Name}' no pertenece a esta edición del campamento");

            if (!accommodation.IsActive)
                throw new BusinessRuleException(
                    $"El alojamiento '{accommodation.Name}' no está disponible");

            newPreferences.Add(new RegistrationAccommodationPreference
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                CampEditionAccommodationId = pref.CampEditionAccommodationId,
                PreferenceOrder = pref.PreferenceOrder
            });
        }

        // 5. Delete existing and save new
        await accommodationPrefsRepo.DeleteByRegistrationIdAsync(registrationId, ct);
        if (newPreferences.Count > 0)
            await accommodationPrefsRepo.AddRangeAsync(newPreferences, ct);

        // 6. Reload and return
        return await GetAccommodationPreferencesAsync(registrationId, ct);
    }

    public async Task<List<AccommodationPreferenceResponse>> GetAccommodationPreferencesAsync(
        Guid registrationId, CancellationToken ct)
    {
        var preferences = await accommodationPrefsRepo.GetByRegistrationIdAsync(registrationId, ct);

        return preferences.Select(p => new AccommodationPreferenceResponse(
            p.CampEditionAccommodationId,
            p.CampEditionAccommodation.Name,
            p.CampEditionAccommodation.AccommodationType,
            p.PreferenceOrder)).ToList();
    }

    public async Task<AdminRegistrationListResponse> GetAdminListAsync(
        Guid campEditionId, int page, int pageSize, string? search, string? status,
        IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,
        IReadOnlyList<Guid>? extraIds,
        IReadOnlyList<AttendancePeriod>? attendancePeriods,
        IReadOnlyList<AgeCategory>? ageCategories,
        AdminRegistrationSortBy sortBy,
        bool sortDescending,
        CancellationToken ct)
    {
        var edition = await campEditionsRepo.GetByIdAsync(campEditionId, ct)
            ?? throw new NotFoundException("Edición de Campamento", campEditionId);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount, totals) = await registrationsRepo.GetAdminPagedAsync(
            campEditionId, page, pageSize, search, status,
            accommodationPreferences, extraIds, attendancePeriods, ageCategories,
            sortBy, sortDescending, ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new AdminRegistrationListResponse(
            Items: items.Select(p => new AdminRegistrationListItem(
                p.Id,
                new RegistrationFamilyUnitSummary(p.FamilyUnitId, p.FamilyUnitName, p.RepresentativeUserId),
                new RepresentativeSummary(p.RepresentativeUserId, p.RepresentativeFirstName, p.RepresentativeLastName, p.RepresentativeEmail),
                p.Status,
                p.MemberCount,
                p.TotalAmount,
                p.AmountPaid,
                p.TotalAmount - p.AmountPaid,
                p.CreatedAt,
                p.AttendancePeriods,
                p.AccommodationPreferences
            )).ToList(),
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages,
            Totals: totals
        );
    }

    public async Task<RegistrationResponse> AdminUpdateAsync(
        Guid registrationId, Guid adminUserId, AdminEditRegistrationRequest request, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (registration.Status == RegistrationStatus.Cancelled)
            throw new BusinessRuleException("No se puede editar una inscripción cancelada");

        var edition = await campEditionsRepo.GetByIdAsync(registration.CampEditionId, ct)
            ?? throw new NotFoundException("Edición de Campamento", registration.CampEditionId);

        // Capture old values before any mutation (needed for sync and history)
        var previousStatus = registration.Status;
        var oldBaseTotalAmount = registration.BaseTotalAmount;

        // Capture old state for change summary BEFORE any mutations
        var oldMemberNames = registration.Members.ToDictionary(
            m => m.FamilyMemberId,
            m => $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}");
        var oldExtrasInfo = registration.Extras.ToDictionary(
            e => e.CampEditionExtraId,
            e => (Name: e.CampEditionExtra.Name, Quantity: e.Quantity));
        var changeSummary = new List<string>();

        // Update members if provided
        if (request.Members != null)
        {
            await registrationsRepo.DeleteMembersByRegistrationIdAsync(registrationId, ct);

            var newMemberIdToName = new Dictionary<Guid, string>();
            var newMembers = new List<RegistrationMember>();
            foreach (var memberReq in request.Members)
            {
                var familyMember = await familyUnitsRepo.GetFamilyMemberByIdAsync(memberReq.MemberId, ct)
                    ?? throw new NotFoundException("Miembro Familiar", memberReq.MemberId);

                if (memberReq.AttendancePeriod == AttendancePeriod.WeekendVisit)
                {
                    var campStart = DateOnly.FromDateTime(edition.StartDate);
                    var campEnd = DateOnly.FromDateTime(edition.EndDate);
                    if (memberReq.VisitStartDate < campStart || memberReq.VisitEndDate > campEnd)
                        throw new BusinessRuleException(
                            "Las fechas de la visita deben estar dentro del periodo del campamento");
                }

                var age = pricingService.CalculateAge(familyMember.DateOfBirth, edition.StartDate);
                var category = await pricingService.GetAgeCategoryAsync(age, edition, ct);
                var price = pricingService.GetPriceForCategory(category, memberReq.AttendancePeriod, edition);

                newMemberIdToName[memberReq.MemberId] =
                    $"{familyMember.FirstName} {familyMember.LastName}";

                newMembers.Add(new RegistrationMember
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    FamilyMemberId = memberReq.MemberId,
                    AgeAtCamp = age,
                    AgeCategory = category,
                    IndividualAmount = price,
                    AttendancePeriod = memberReq.AttendancePeriod,
                    VisitStartDate = memberReq.VisitStartDate,
                    VisitEndDate = memberReq.VisitEndDate,
                    GuardianName = memberReq.GuardianName,
                    GuardianDocumentNumber = memberReq.GuardianDocumentNumber,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await registrationsRepo.AddMembersAsync(newMembers, ct);
            registration.BaseTotalAmount = newMembers.Sum(m => m.IndividualAmount);
            registration.TotalAmount = registration.BaseTotalAmount + registration.ExtrasAmount;

            foreach (var (id, name) in oldMemberNames)
                if (!newMemberIdToName.ContainsKey(id))
                    changeSummary.Add($"Eliminada persona: {name}");
            foreach (var (id, name) in newMemberIdToName)
                if (!oldMemberNames.ContainsKey(id))
                    changeSummary.Add($"Añadida persona: {name}");
        }

        // Update extras if provided
        if (request.Extras != null)
        {
            await extrasRepo.DeleteByRegistrationIdAsync(registrationId, ct);

            var campDurationDays = (edition.EndDate - edition.StartDate).Days;
            var newExtraIdToInfo = new Dictionary<Guid, (string Name, int Quantity)>();
            var newExtras = new List<RegistrationExtra>();
            foreach (var extraReq in request.Extras)
            {
                var campExtra = await campEditionsRepo.GetExtraByIdAsync(extraReq.CampEditionExtraId, ct)
                    ?? throw new NotFoundException("Extra de Campamento", extraReq.CampEditionExtraId);

                if (campExtra.CampEditionId != registration.CampEditionId)
                    throw new BusinessRuleException(
                        $"El extra '{campExtra.Name}' no pertenece a esta edición del campamento");

                var totalAmount = pricingService.CalculateExtraAmount(campExtra, extraReq.Quantity, campDurationDays);

                newExtraIdToInfo[extraReq.CampEditionExtraId] = (campExtra.Name, extraReq.Quantity);

                newExtras.Add(new RegistrationExtra
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    CampEditionExtraId = extraReq.CampEditionExtraId,
                    Quantity = extraReq.Quantity,
                    UnitPrice = campExtra.Price,
                    CampDurationDays = campDurationDays,
                    TotalAmount = totalAmount,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await extrasRepo.AddRangeAsync(newExtras, ct);
            registration.ExtrasAmount = newExtras.Sum(e => e.TotalAmount);
            registration.TotalAmount = registration.BaseTotalAmount + registration.ExtrasAmount;

            foreach (var (id, (name, qty)) in oldExtrasInfo)
            {
                if (!newExtraIdToInfo.TryGetValue(id, out var newInfo))
                    changeSummary.Add($"Eliminado cargo extra: {name}");
                else if (newInfo.Quantity != qty)
                    changeSummary.Add($"Cantidad modificada para {name}: {qty} → {newInfo.Quantity}");
            }
            foreach (var (id, (name, _)) in newExtraIdToInfo)
                if (!oldExtrasInfo.ContainsKey(id))
                    changeSummary.Add($"Añadido cargo extra: {name}");
        }

        // Update accommodation preferences if provided
        if (request.Preferences != null)
        {
            await accommodationPrefsRepo.DeleteByRegistrationIdAsync(registrationId, ct);
            if (request.Preferences.Count > 0)
            {
                var newPrefs = request.Preferences.Select(p => new RegistrationAccommodationPreference
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    CampEditionAccommodationId = p.CampEditionAccommodationId,
                    PreferenceOrder = p.PreferenceOrder,
                    CreatedAt = DateTime.UtcNow
                });
                await accommodationPrefsRepo.AddRangeAsync(newPrefs, ct);
            }
        }

        // Update text fields if provided
        if (request.Notes != null) registration.Notes = request.Notes;
        if (request.SpecialNeeds != null) registration.SpecialNeeds = request.SpecialNeeds;
        if (request.CampatesPreference != null) registration.CampatesPreference = request.CampatesPreference;
        if (request.HasPet != null) registration.HasPet = request.HasPet.Value;

        // Set status to Draft and record admin modification
        registration.Status = RegistrationStatus.Draft;
        registration.DraftTargetStatus = request.DraftTargetStatus ?? previousStatus;
        registration.HasPendingUserAcknowledgement = true;
        registration.AdminModifiedAt = DateTime.UtcNow;
        registration.FamilyNotifiedOfDraft = false;

        await registrationsRepo.UpdateAsync(registration, ct);

        // Log status history only when actually transitioning into Draft
        if (previousStatus != RegistrationStatus.Draft)
        {
            await LogStatusHistoryAsync(registrationId, previousStatus, RegistrationStatus.Draft,
                adminUserId, StatusChangeTrigger.AdminAction, null, ct);
        }

        // Sync payments after save
        if (request.Members != null)
            await paymentsService.SyncBaseInstallmentsAsync(
                registrationId, registration.BaseTotalAmount, oldBaseTotalAmount, ct);

        if (request.Extras != null)
            await paymentsService.SyncExtrasInstallmentAsync(
                registrationId, registration.ExtrasAmount, ct);

        // Reload with full details for response
        var updated = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var amountPaid = updated.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        logger.LogInformation("Registration {RegistrationId} edited by admin, status set to Draft", registrationId);

        if (request.NotifyUser)
        {
            try
            {
                await emailService.SendDraftChangesNotificationAsync(new DraftChangesEmailData
                {
                    ToEmail = updated.RegisteredByUser.Email,
                    RecipientFirstName = updated.RegisteredByUser.FirstName,
                    CampName = updated.CampEdition.Camp.Name,
                    RegistrationId = updated.Id,
                    BoardNotes = request.Notes,
                    ChangeSummary = changeSummary.Count > 0 ? changeSummary : null
                }, ct);

                registration.FamilyNotifiedOfDraft = true;
                await registrationsRepo.UpdateAsync(registration, ct);
                updated.FamilyNotifiedOfDraft = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to send draft notification email for registration {RegistrationId}",
                    registrationId);
            }
        }

        return updated.ToResponse(amountPaid);
    }

    public async Task DeleteAsync(
        Guid registrationId, Guid requestingUserId, bool isAdminOrBoard, CancellationToken ct)
    {
        // 1. Load registration with details (Payments + FamilyUnit needed for validation)
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException(nameof(Registration), registrationId);

        // 2. Validate authorization
        if (!isAdminOrBoard)
        {
            if (registration.FamilyUnit.RepresentativeUserId != requestingUserId)
                throw new UnauthorizedAccessException("You are not authorized to delete this registration.");
        }

        // 3. Validate status
        // Confirmed/FullyPaid always blocked; Cancelled is only blocked for representatives (admins can delete it)
        if (registration.Status is RegistrationStatus.Confirmed or RegistrationStatus.FullyPaid)
            throw new BusinessRuleException(
                "Confirmed or fully-paid registrations cannot be deleted. Please cancel first.");
        if (registration.Status is RegistrationStatus.Cancelled && !isAdminOrBoard)
            throw new BusinessRuleException("Cancelled registrations cannot be deleted.");

        // 4. Validate payment guard (BR-2)
        if (registration.Payments?.Any() == true)
        {
            if (!isAdminOrBoard)
                throw new BusinessRuleException("Cannot delete registration with existing payments. Please contact an administrator.");

            // Admin/Board: blocked only if proof was uploaded or a payment was confirmed
            if (registration.Payments.Any(p => p.ProofFileUrl != null))
                throw new BusinessRuleException(
                    "No se puede eliminar la inscripción porque tiene pagos con justificantes subidos. Elimina los justificantes primero.");

            if (registration.Payments.Any(p => p.Status == PaymentStatus.Completed))
                throw new BusinessRuleException(
                    "No se puede eliminar la inscripción porque tiene pagos confirmados.");

            // Payments are clean (no proof, not completed) — delete them first (FK Restrict)
            await paymentsService.DeleteByRegistrationIdAsync(registrationId, ct);
        }

        // 5. Validate time window (representative only)
        if (!isAdminOrBoard)
        {
            var gracePeriod = TimeSpan.FromHours(24);
            if (DateTime.UtcNow - registration.CreatedAt > gracePeriod)
                throw new BusinessRuleException("Registration can only be deleted within 24 hours of creation.");
        }

        // 6. Execute deletion
        await registrationsRepo.DeleteAsync(registrationId, ct);

        // 7. Log the action
        logger.LogInformation(
            "Registration {RegistrationId} deleted by user {UserId} (Admin: {IsAdmin})",
            registrationId, requestingUserId, isAdminOrBoard);
    }

    public async Task<(byte[] Content, string FileName)> ExportToCsvAsync(
        Guid campEditionId,
        string? search,
        string? status,
        IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,
        IReadOnlyList<Guid>? extraIds,
        IReadOnlyList<AttendancePeriod>? attendancePeriods,
        IReadOnlyList<AgeCategory>? ageCategories,
        CancellationToken ct)
    {
        var edition = await campEditionsRepo.GetByIdAsync(campEditionId, ct)
            ?? throw new NotFoundException("Edición de Campamento", campEditionId);

        var allExtras = await extrasDefinitionRepo.GetByCampEditionAsync(campEditionId, activeOnly: true, ct);
        allExtras = [.. allExtras.OrderBy(e => e.SortOrder)];

        var registrations = await registrationsRepo.GetAllForExportAsync(
            campEditionId, search, status, accommodationPreferences, extraIds, attendancePeriods, ageCategories, ct);

        var csv = new StringBuilder();
        csv.Append('﻿'); // UTF-8 BOM

        var headers = new List<string>
        {
            "ID Inscripción", "Familia", "Representante", "Email", "Teléfono", "Estado",
            "Nº Miembros", "Miembros",
            "Preferencia alojamiento 1", "Tipo alojamiento 1",
            "Preferencia alojamiento 2", "Tipo alojamiento 2",
            "Preferencia alojamiento 3", "Tipo alojamiento 3",
            "Necesidades especiales", "Preferencia compañeros", "Tiene mascota", "Notas",
            "Base (€)", "Extras (€)", "Total (€)", "Pagado (€)", "Pendiente (€)",
            "Fecha inscripción"
        };
        foreach (var extra in allExtras)
        {
            headers.Add(extra.Name);
            if (extra.RequiresUserInput)
                headers.Add($"{extra.Name} - Detalle");
        }
        csv.AppendLine(string.Join(",", headers.Select(EscapeCsvValue)));

        foreach (var r in registrations)
        {
            var amountPaid = r.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Sum(p => p.Amount);
            var amountRemaining = r.TotalAmount - amountPaid;

            var prefs = r.AccommodationPreferences.OrderBy(p => p.PreferenceOrder).ToList();
            var pref1 = prefs.FirstOrDefault(p => p.PreferenceOrder == 1);
            var pref2 = prefs.FirstOrDefault(p => p.PreferenceOrder == 2);
            var pref3 = prefs.FirstOrDefault(p => p.PreferenceOrder == 3);

            var members = r.Members.Select(m =>
                $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName} " +
                $"({MapAgeCategory(m.AgeCategory)}, {MapAttendancePeriod(m.AttendancePeriod)})"
            );

            var row = new List<string>
            {
                r.Id.ToString(),
                r.FamilyUnit.Name,
                $"{r.RegisteredByUser.FirstName} {r.RegisteredByUser.LastName}",
                r.RegisteredByUser.Email,
                r.RegisteredByUser.Phone ?? "",
                MapStatusEs(r.Status),
                r.Members.Count.ToString(),
                string.Join("; ", members),
                pref1?.CampEditionAccommodation.Name ?? "",
                pref1 is not null ? MapAccommodationTypeEs(pref1.CampEditionAccommodation.AccommodationType) : "",
                pref2?.CampEditionAccommodation.Name ?? "",
                pref2 is not null ? MapAccommodationTypeEs(pref2.CampEditionAccommodation.AccommodationType) : "",
                pref3?.CampEditionAccommodation.Name ?? "",
                pref3 is not null ? MapAccommodationTypeEs(pref3.CampEditionAccommodation.AccommodationType) : "",
                r.SpecialNeeds ?? "",
                r.CampatesPreference ?? "",
                r.HasPet ? "Sí" : "No",
                r.Notes ?? "",
                r.BaseTotalAmount.ToString("F2"),
                r.ExtrasAmount.ToString("F2"),
                r.TotalAmount.ToString("F2"),
                amountPaid.ToString("F2"),
                amountRemaining.ToString("F2"),
                r.CreatedAt.ToString("dd/MM/yyyy")
            };

            foreach (var extra in allExtras)
            {
                var selected = r.Extras.FirstOrDefault(e => e.CampEditionExtraId == extra.Id);
                row.Add((selected?.Quantity ?? 0).ToString());
                if (extra.RequiresUserInput)
                    row.Add(selected?.UserInput ?? "");
            }

            csv.AppendLine(string.Join(",", row.Select(EscapeCsvValue)));
        }

        var campSlug = Regex.Replace(
            edition.Camp.Name.ToLower().Normalize(NormalizationForm.FormD), @"[^a-z0-9]+", "-").Trim('-');
        var fileName = $"inscripciones-{campSlug}-{edition.Year}-{DateTime.UtcNow:yyyy-MM-dd}.csv";

        return (Encoding.UTF8.GetBytes(csv.ToString()), fileName);
    }

    public async Task<RegistrationResponse> ChangeStatusAsync(
        Guid registrationId, Guid adminUserId, ChangeRegistrationStatusRequest request, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var previousStatus = registration.Status;

        if (request.NewStatus == RegistrationStatus.Cancelled)
            throw new BusinessRuleException(
                "Use el endpoint de cancelación para cancelar una inscripción.");
        if (request.NewStatus == RegistrationStatus.Draft)
            throw new BusinessRuleException(
                "El estado En revisión se asigna automáticamente al editar la inscripción.");
        if (request.NewStatus == RegistrationStatus.FullyPaid)
            throw new BusinessRuleException(
                "El estado Pago completo se asigna automáticamente al confirmar todos los pagos.");

        var validTransitions = new Dictionary<RegistrationStatus, HashSet<RegistrationStatus>>
        {
            [RegistrationStatus.Pending]       = [RegistrationStatus.PartiallyPaid, RegistrationStatus.Confirmed],
            [RegistrationStatus.PartiallyPaid] = [RegistrationStatus.Pending, RegistrationStatus.Confirmed],
            [RegistrationStatus.FullyPaid]     = [RegistrationStatus.Confirmed, RegistrationStatus.Pending],
            [RegistrationStatus.Confirmed]     = [RegistrationStatus.Pending, RegistrationStatus.PartiallyPaid],
            [RegistrationStatus.Draft]         = [RegistrationStatus.Pending, RegistrationStatus.PartiallyPaid,
                                                  RegistrationStatus.FullyPaid, RegistrationStatus.Confirmed],
            [RegistrationStatus.Cancelled]     = [],
        };

        if (!validTransitions.TryGetValue(previousStatus, out var allowed) || !allowed.Contains(request.NewStatus))
            throw new BusinessRuleException(
                $"La transición de {MapStatusEs(previousStatus)} a {MapStatusEs(request.NewStatus)} no está permitida.");

        registration.Status = request.NewStatus;
        if (previousStatus == RegistrationStatus.Draft)
        {
            registration.DraftTargetStatus = null;
            registration.HasPendingUserAcknowledgement = false;
        }

        await registrationsRepo.UpdateAsync(registration, ct);

        await LogStatusHistoryAsync(registrationId, previousStatus, request.NewStatus,
            adminUserId, StatusChangeTrigger.AdminAction, request.Notes, ct);

        logger.LogInformation(
            "Registration {RegistrationId} status changed {Previous} → {New} by admin {AdminUserId}",
            registrationId, previousStatus, request.NewStatus, adminUserId);

        if (request.NotifyUser)
        {
            try
            {
                var emailData = new RegistrationStatusEmailData
                {
                    ToEmail = registration.RegisteredByUser.Email,
                    RecipientFirstName = registration.RegisteredByUser.FirstName,
                    CampName = registration.CampEdition.Camp.Name,
                    RegistrationId = registration.Id,
                    BoardNotes = request.Notes
                };

                Task emailTask = request.NewStatus switch
                {
                    RegistrationStatus.Pending =>
                        emailService.SendRegistrationRevertedToPendingAsync(emailData, ct),
                    RegistrationStatus.PartiallyPaid =>
                        emailService.SendRegistrationPartiallyPaidAsync(emailData, ct),
                    RegistrationStatus.Confirmed =>
                        emailService.SendRegistrationFinallyConfirmedAsync(emailData, ct),
                    _ => Task.CompletedTask
                };
                await emailTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to send status change notification email for registration {RegistrationId}",
                    registrationId);
            }
        }

        var updated = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var amountPaid = updated.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return updated.ToResponse(amountPaid);
    }

    public async Task<RegistrationResponse> AdminUpdateMembersAsync(
        Guid registrationId, Guid adminUserId, UpdateRegistrationMembersRequest request, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var edition = await campEditionsRepo.GetByIdAsync(registration.CampEditionId, ct)
            ?? throw new NotFoundException("Edición de Campamento", registration.CampEditionId);

        // Build new members with pricing
        var newMembers = new List<RegistrationMember>();
        foreach (var m in request.Members)
        {
            var member = await familyUnitsRepo.GetFamilyMemberByIdAsync(m.MemberId, ct)
                ?? throw new NotFoundException("Miembro Familiar", m.MemberId);

            if (member.FamilyUnitId != registration.FamilyUnitId)
                throw new BusinessRuleException(
                    $"El miembro {member.FirstName} {member.LastName} no pertenece a esta unidad familiar");

            if (m.AttendancePeriod == AttendancePeriod.WeekendVisit)
            {
                var campStart = DateOnly.FromDateTime(edition.StartDate);
                var campEnd = DateOnly.FromDateTime(edition.EndDate);
                if (m.VisitStartDate < campStart || m.VisitEndDate > campEnd)
                    throw new BusinessRuleException(
                        "Las fechas de la visita deben estar dentro del periodo del campamento");
            }

            var age = pricingService.CalculateAge(member.DateOfBirth, edition.StartDate);
            var category = await pricingService.GetAgeCategoryAsync(age, edition, ct);
            var price = pricingService.GetPriceForCategory(category, m.AttendancePeriod, edition);

            newMembers.Add(new RegistrationMember
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                FamilyMemberId = m.MemberId,
                AgeAtCamp = age,
                AgeCategory = category,
                IndividualAmount = price,
                AttendancePeriod = m.AttendancePeriod,
                VisitStartDate = m.VisitStartDate,
                VisitEndDate = m.VisitEndDate,
                GuardianName = m.GuardianName,
                GuardianDocumentNumber = m.GuardianDocumentNumber,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Require at least one adult
        if (!newMembers.Any(m => m.AgeCategory == AgeCategory.Adult))
            throw new BusinessRuleException("La inscripción debe tener al menos un adulto responsable");

        var oldBaseTotalAmount = registration.BaseTotalAmount;
        var newBaseTotalAmount = newMembers.Sum(m => m.IndividualAmount);

        var completedBasePayments = registration.Payments
            .Where(p => p.Status == PaymentStatus.Completed && !p.IsManual)
            .Sum(p => p.Amount);

        // Replace members
        await registrationsRepo.DeleteMembersByRegistrationIdAsync(registrationId, ct);
        await registrationsRepo.AddMembersAsync(newMembers, ct);

        registration.BaseTotalAmount = newBaseTotalAmount;
        registration.TotalAmount = newBaseTotalAmount + registration.ExtrasAmount;

        // Generate refund if overpaid
        if (completedBasePayments > newBaseTotalAmount)
        {
            var refundAmount = completedBasePayments - newBaseTotalAmount;
            await paymentsService.GenerateRefundPaymentAsync(
                registrationId, refundAmount, "Devolución por baja de participante", adminUserId, ct);
            // TotalAmount adjustment happens inside GenerateRefundPaymentAsync; reload registration
            registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
                ?? throw new NotFoundException("Inscripción", registrationId);
            registration.BaseTotalAmount = newBaseTotalAmount;
        }

        // Sync P1/P2 installments
        await paymentsService.SyncBaseInstallmentsAsync(
            registrationId, newBaseTotalAmount, oldBaseTotalAmount, ct);

        // Determine draft target status
        var draftTargetStatus = completedBasePayments > 0
            ? RegistrationStatus.PartiallyPaid
            : RegistrationStatus.Pending;

        registration.Status = RegistrationStatus.Draft;
        registration.DraftTargetStatus = draftTargetStatus;
        registration.HasPendingUserAcknowledgement = true;
        registration.AdminModifiedAt = DateTime.UtcNow;
        registration.UpdatedAt = DateTime.UtcNow;

        await registrationsRepo.UpdateAsync(registration, ct);

        await LogStatusHistoryAsync(registrationId, registration.Status, RegistrationStatus.Draft,
            adminUserId, StatusChangeTrigger.AdminAction, "Admin member update", ct);

        logger.LogInformation(
            "Admin {AdminUserId} updated members for registration {RegistrationId}. New base total={NewTotal}, refund triggered={Refund}",
            adminUserId, registrationId, newBaseTotalAmount, completedBasePayments > newBaseTotalAmount);

        var updated = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var amountPaid = updated.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return updated.ToResponse(amountPaid);
    }

    public async Task<RegistrationResponse> ConfirmChangesAsync(
        Guid registrationId, Guid requestingUserId, bool isAdminOrBoard, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (registration.Status != RegistrationStatus.Draft)
            throw new BusinessRuleException(
                "La inscripción no está en estado de revisión pendiente.");

        if (!isAdminOrBoard && registration.FamilyUnit.RepresentativeUserId != requestingUserId)
            throw new UnauthorizedAccessException(
                "No tienes permiso para confirmar los cambios de esta inscripción.");

        var previousStatus = registration.Status;
        var targetStatus = registration.DraftTargetStatus ?? RegistrationStatus.Pending;

        registration.Status = targetStatus;
        registration.DraftTargetStatus = null;
        registration.HasPendingUserAcknowledgement = false;

        await registrationsRepo.UpdateAsync(registration, ct);

        var trigger = isAdminOrBoard
            ? StatusChangeTrigger.AdminAction
            : StatusChangeTrigger.UserConfirmed;
        await LogStatusHistoryAsync(registrationId, previousStatus, targetStatus,
            requestingUserId, trigger, null, ct);

        logger.LogInformation(
            "Registration {RegistrationId} Draft confirmed by {UserId} (isAdmin={IsAdmin}), → {Target}",
            registrationId, requestingUserId, isAdminOrBoard, targetStatus);

        try
        {
            await emailService.SendDraftChangesConfirmedAsync(new DraftChangesConfirmedEmailData
            {
                ToEmail = registration.RegisteredByUser.Email,
                RecipientFirstName = registration.RegisteredByUser.FirstName,
                CampName = registration.CampEdition.Camp.Name,
                RegistrationId = registration.Id,
                NewStatusEs = MapStatusEs(targetStatus)
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send draft-confirmed email for registration {RegistrationId}",
                registrationId);
        }

        var updated = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var amountPaid = updated.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return updated.ToResponse(amountPaid);
    }

    public async Task<RegistrationResponse> NotifyDraftAsync(
        Guid registrationId, string? boardNotes, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (registration.Status != RegistrationStatus.Draft)
            throw new BusinessRuleException(
                "Solo se puede notificar de cambios a inscripciones en estado En revisión.");

        await emailService.SendDraftChangesNotificationAsync(new DraftChangesEmailData
        {
            ToEmail = registration.RegisteredByUser.Email,
            RecipientFirstName = registration.RegisteredByUser.FirstName,
            CampName = registration.CampEdition.Camp.Name,
            RegistrationId = registration.Id,
            BoardNotes = boardNotes
        }, ct);

        registration.FamilyNotifiedOfDraft = true;
        await registrationsRepo.UpdateAsync(registration, ct);

        logger.LogInformation(
            "Draft notification sent for registration {RegistrationId}", registrationId);

        var amountPaid = registration.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        registration.FamilyNotifiedOfDraft = true;
        return registration.ToResponse(amountPaid);
    }

    private async Task LogStatusHistoryAsync(
        Guid registrationId,
        RegistrationStatus previousStatus,
        RegistrationStatus newStatus,
        Guid? changedByUserId,
        StatusChangeTrigger trigger,
        string? notes,
        CancellationToken ct)
    {
        var history = new RegistrationStatusHistory
        {
            Id = Guid.NewGuid(),
            RegistrationId = registrationId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId,
            ChangedAt = DateTime.UtcNow,
            Trigger = trigger,
            Notes = notes
        };
        await registrationsRepo.AddStatusHistoryAsync(history, ct);
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
            value = " " + value;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            value = $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }

    private static string MapStatusEs(RegistrationStatus status) => status switch
    {
        RegistrationStatus.Pending       => "Pendiente",
        RegistrationStatus.PartiallyPaid => "Al corriente",
        RegistrationStatus.FullyPaid     => "Pago completo",
        RegistrationStatus.Confirmed     => "Confirmada",
        RegistrationStatus.Cancelled     => "Cancelada",
        RegistrationStatus.Draft         => "En revisión",
        _                                => status.ToString()
    };

    private static string MapAccommodationTypeEs(AccommodationType type) => type switch
    {
        AccommodationType.Lodge => "Albergue",
        AccommodationType.Tent => "Tienda",
        AccommodationType.Caravan => "Caravana",
        AccommodationType.Bungalow => "Bungalow",
        AccommodationType.Motorhome => "Autocaravana",
        _ => type.ToString()
    };

    private static CampRegistrationEmailData BuildRegistrationEmailData(
        Registration registration,
        CampEdition edition,
        List<PaymentResponse>? installments = null,
        PaymentSettingsResponse? paymentSettings = null)
    {
        var first = installments?.FirstOrDefault(i => i.InstallmentNumber == 1);
        var hasPaymentInfo = first != null && !string.IsNullOrWhiteSpace(paymentSettings?.Iban);

        return new CampRegistrationEmailData
        {
            ToEmail = registration.RegisteredByUser.Email,
            RecipientFirstName = registration.RegisteredByUser.FirstName,
            CampName = edition.Camp.Name,
            CampLocation = edition.Camp.Location ?? "Sin ubicación",
            StartDate = DateOnly.FromDateTime(edition.StartDate),
            EndDate = DateOnly.FromDateTime(edition.EndDate),
            Year = edition.Year,
            RegistrationId = registration.Id,
            TotalAmount = registration.TotalAmount,
            BaseTotalAmount = registration.BaseTotalAmount,
            ExtrasAmount = registration.ExtrasAmount,
            SpecialNeeds = registration.SpecialNeeds,
            CampatesPreference = registration.CampatesPreference,
            Members = registration.Members.Select(m => new RegistrationMemberEmailData
            {
                FullName = $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
                AgeCategory = MapAgeCategory(m.AgeCategory),
                AgeAtCamp = m.AgeAtCamp,
                AttendancePeriod = MapAttendancePeriod(m.AttendancePeriod),
                IndividualAmount = m.IndividualAmount
            }).ToList(),
            FirstInstallmentConcept = hasPaymentInfo ? first!.TransferConcept : null,
            FirstInstallmentAmount = hasPaymentInfo ? first!.Amount : null,
            Iban = hasPaymentInfo ? paymentSettings!.Iban : null,
            BankName = hasPaymentInfo ? paymentSettings!.BankName : null,
            AccountHolder = hasPaymentInfo ? paymentSettings!.AccountHolder : null,
        };
    }

    private static string MapAgeCategory(AgeCategory category) => category switch
    {
        AgeCategory.Adult => "Adulto",
        AgeCategory.Child => "Niño",
        AgeCategory.Baby => "Bebé",
        _ => category.ToString()
    };

    private static string MapAttendancePeriod(AttendancePeriod period) => period switch
    {
        AttendancePeriod.Complete => "Completo",
        AttendancePeriod.FirstWeek => "1ª Semana",
        AttendancePeriod.SecondWeek => "2ª Semana",
        AttendancePeriod.WeekendVisit => "Visita fin de semana",
        _ => period.ToString()
    };

    // ── Accommodation Needs ──────────────────────────────────────────────────────

    public async Task<AccommodationNeedsResponse> UpdateAccommodationNeedsAsync(
        Guid registrationId, Guid taggedByUserId, UpdateAccommodationNeedsRequest request, CancellationToken ct)
    {
        _ = await registrationsRepo.GetByIdAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (request.FeatureIds.Count > 0)
        {
            var features = await accommodationFeaturesRepo.GetByIdsAsync(request.FeatureIds, ct);
            if (features.Count != request.FeatureIds.Count)
                throw new ValidationException(
                    "Uno o más identificadores de característica no existen en el catálogo");
        }

        var needs = request.FeatureIds.Select(featureId => new RegistrationAccommodationNeed
        {
            Id = Guid.NewGuid(),
            RegistrationId = registrationId,
            AccommodationFeatureId = featureId,
            TaggedByUserId = taggedByUserId
        }).ToList();

        await accommodationNeedsRepo.ReplaceAsync(registrationId, needs, ct);

        var saved = await accommodationNeedsRepo.GetByRegistrationIdAsync(registrationId, ct);

        return new AccommodationNeedsResponse(
            registrationId,
            saved.Select(n => new AccommodationNeedResponse(
                n.AccommodationFeatureId,
                n.AccommodationFeature.Name,
                n.AccommodationFeature.ApplicabilityLevel.ToString(),
                n.TaggedByUserId,
                n.CreatedAt)).ToList());
    }

    public async Task<List<AccommodationNeedResponse>> GetAccommodationNeedsAsync(
        Guid registrationId, CancellationToken ct)
    {
        _ = await registrationsRepo.GetByIdAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var needs = await accommodationNeedsRepo.GetByRegistrationIdAsync(registrationId, ct);

        return needs.Select(n => new AccommodationNeedResponse(
            n.AccommodationFeatureId,
            n.AccommodationFeature.Name,
            n.AccommodationFeature.ApplicabilityLevel.ToString(),
            n.TaggedByUserId,
            n.CreatedAt)).ToList();
    }

    // ── Accommodation Notes ──────────────────────────────────────────────────────

    public async Task<AccommodationNotesResponse> UpdateAccommodationNotesAsync(
        Guid registrationId, UpdateAccommodationNotesRequest request, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        registration.AccommodationInternalNotes = string.IsNullOrWhiteSpace(request.AccommodationInternalNotes)
            ? null
            : request.AccommodationInternalNotes;

        await registrationsRepo.UpdateAsync(registration, ct);

        return new AccommodationNotesResponse(
            registrationId,
            registration.AccommodationInternalNotes,
            DateTime.UtcNow);
    }

    // ── Friend Links ─────────────────────────────────────────────────────────────

    public async Task<FriendLinksResponse> UpdateFriendLinksAsync(
        Guid registrationId, Guid createdByUserId, UpdateFriendLinksRequest request, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (request.LinkedRegistrationIds.Contains(registrationId))
            throw new BusinessRuleException("NO_SELF_LINK: No se puede crear un vínculo de una inscripción consigo misma");

        foreach (var linkedId in request.LinkedRegistrationIds)
        {
            var linked = await registrationsRepo.GetByIdAsync(linkedId, ct)
                ?? throw new NotFoundException("Inscripción vinculada", linkedId);

            if (linked.CampEditionId != registration.CampEditionId)
                throw new BusinessRuleException(
                    "SAME_EDITION_REQUIRED: Todas las inscripciones vinculadas deben pertenecer a la misma edición de campamento");
        }

        await friendLinksRepo.ReplaceAsync(registrationId, request.LinkedRegistrationIds, createdByUserId, ct);

        var saved = await friendLinksRepo.GetByRegistrationIdAsync(registrationId, ct);

        return new FriendLinksResponse(
            registrationId,
            saved.Select(l => new FriendLinkResponse(
                l.LinkedRegistrationId,
                l.LinkedRegistration.FamilyUnit.Name,
                l.CreatedByUserId,
                l.CreatedAt)).ToList());
    }

    public async Task<List<FriendLinkResponse>> GetFriendLinksAsync(
        Guid registrationId, CancellationToken ct)
    {
        _ = await registrationsRepo.GetByIdAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var links = await friendLinksRepo.GetByRegistrationIdAsync(registrationId, ct);

        return links.Select(l => new FriendLinkResponse(
            l.LinkedRegistrationId,
            l.LinkedRegistration.FamilyUnit.Name,
            l.CreatedByUserId,
            l.CreatedAt)).ToList();
    }
}

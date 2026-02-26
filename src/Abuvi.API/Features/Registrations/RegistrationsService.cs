using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Microsoft.Extensions.Logging;

namespace Abuvi.API.Features.Registrations;

public class RegistrationsService(
    IRegistrationsRepository registrationsRepo,
    IRegistrationExtrasRepository extrasRepo,
    IFamilyUnitsRepository familyUnitsRepo,
    ICampEditionsRepository campEditionsRepo,
    RegistrationPricingService pricingService,
    ILogger<RegistrationsService> logger)
{
    public async Task<RegistrationResponse> CreateAsync(
        Guid userId, CreateRegistrationRequest request, CancellationToken ct)
    {
        // 1. Load FamilyUnit
        var familyUnit = await familyUnitsRepo.GetFamilyUnitByIdAsync(request.FamilyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", request.FamilyUnitId);

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
                var campEnd   = DateOnly.FromDateTime(edition.EndDate);
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
            Notes = request.Notes,
            Members = registrationMembers
        };

        // 10. Save
        await registrationsRepo.AddAsync(registration, ct);

        // 11. Log
        logger.LogInformation(
            "Registration {RegistrationId} created for family {FamilyUnitId} in edition {EditionId}",
            registration.Id, request.FamilyUnitId, request.CampEditionId);

        // 12. Reload and return
        var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registration.Id, ct)
            ?? throw new NotFoundException("Inscripción", registration.Id);

        return detailed.ToResponse(amountPaid: 0m);
    }

    public async Task<RegistrationResponse> UpdateMembersAsync(
        Guid registrationId, Guid userId, UpdateRegistrationMembersRequest request, CancellationToken ct)
    {
        // 1. Load registration
        var registration = await registrationsRepo.GetByIdAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        // 2. Load FamilyUnit and verify representative
        var familyUnit = await familyUnitsRepo.GetFamilyUnitByIdAsync(registration.FamilyUnitId, ct)
            ?? throw new NotFoundException("Unidad Familiar", registration.FamilyUnitId);

        if (familyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para modificar esta inscripción");

        // 3. Verify status
        if (registration.Status != RegistrationStatus.Pending)
            throw new BusinessRuleException("Solo se pueden modificar inscripciones en estado Pendiente");

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
                var campEnd   = DateOnly.FromDateTime(edition.EndDate);
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
        var baseTotalAmount = newMembers.Sum(m => m.IndividualAmount);
        registration.Members = newMembers;
        registration.BaseTotalAmount = baseTotalAmount;
        registration.TotalAmount = baseTotalAmount + registration.ExtrasAmount;

        // 9. Save
        await registrationsRepo.UpdateAsync(registration, ct);

        // 10. Reload and return
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

        // 3. Verify status
        if (registration.Status != RegistrationStatus.Pending)
            throw new BusinessRuleException("Solo se pueden modificar inscripciones en estado Pendiente");

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
                TotalAmount = totalAmount
            });
        }

        // 6. Delete and re-add
        await extrasRepo.DeleteByRegistrationIdAsync(registrationId, ct);
        await extrasRepo.AddRangeAsync(newExtras, ct);

        // 7. Update totals
        registration.ExtrasAmount = newExtras.Sum(e => e.TotalAmount);
        registration.TotalAmount = registration.BaseTotalAmount + registration.ExtrasAmount;
        await registrationsRepo.UpdateAsync(registration, ct);

        // 8. Reload and return
        var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var amountPaid = detailed.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return detailed.ToResponse(amountPaid);
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
        registration.Status = RegistrationStatus.Cancelled;
        await registrationsRepo.UpdateAsync(registration, ct);

        logger.LogInformation(
            "Registration {RegistrationId} cancelled by user {UserId}", registrationId, userId);

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
                WeekendSpotsRemaining: weekendSpotsRemaining));
        }

        return result;
    }

    public async Task<RegistrationResponse> GetByIdAsync(
        Guid registrationId, Guid userId, bool isAdminOrBoard, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (!isAdminOrBoard && registration.FamilyUnit.RepresentativeUserId != userId)
            throw new BusinessRuleException("No tienes permiso para ver esta inscripción");

        var amountPaid = registration.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return registration.ToResponse(amountPaid);
    }

    public async Task<List<RegistrationListResponse>> GetByFamilyUnitAsync(Guid userId, CancellationToken ct)
    {
        var familyUnit = await familyUnitsRepo.GetFamilyUnitByRepresentativeIdAsync(userId, ct);
        if (familyUnit is null) return [];

        var registrations = await registrationsRepo.GetByFamilyUnitAsync(familyUnit.Id, ct);

        return registrations.Select(r =>
        {
            var amountPaid = r.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Sum(p => p.Amount);

            return new RegistrationListResponse(
                Id: r.Id,
                FamilyUnit: new RegistrationFamilyUnitSummary(r.FamilyUnit.Id, r.FamilyUnit.Name),
                CampEdition: new RegistrationCampEditionSummary(
                    r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
                    r.CampEdition.StartDate, r.CampEdition.EndDate,
                    (r.CampEdition.EndDate - r.CampEdition.StartDate).Days),
                Status: r.Status,
                TotalAmount: r.TotalAmount,
                AmountPaid: amountPaid,
                AmountRemaining: r.TotalAmount - amountPaid,
                CreatedAt: r.CreatedAt);
        }).ToList();
    }
}

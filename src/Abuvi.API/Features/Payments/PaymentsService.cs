using System.Globalization;
using System.Text;
using System.Text.Json;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.Registrations;

namespace Abuvi.API.Features.Payments;

public class PaymentsService(
    IPaymentsRepository paymentsRepo,
    IRegistrationsRepository registrationsRepo,
    IAssociationSettingsRepository settingsRepo,
    IBlobStorageService blobStorageService,
    ILogger<PaymentsService> logger) : IPaymentsService
{
    private const string PaymentSettingsKey = "payment_settings";

    public async Task<List<PaymentResponse>> CreateInstallmentsAsync(Guid registrationId, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var settings = await LoadPaymentSettingsAsync(ct);

        var installment1Amount = Math.Ceiling(registration.TotalAmount / 2m);
        var installment2Amount = registration.TotalAmount - installment1Amount;

        var familyName = NormalizeName(registration.FamilyUnit.Name);
        var prefix = settings.TransferConceptPrefix;

        var concept1 = $"{prefix}-{familyName}-1";
        var concept2 = $"{prefix}-{familyName}-2";

        if (concept1.Length > 100) concept1 = concept1[..100];
        if (concept2.Length > 100) concept2 = concept2[..100];

        var edition = registration.CampEdition;
        var dueDate1 = edition.FirstPaymentDeadline
            ?? edition.StartDate.AddDays(-settings.FirstInstallmentDaysBefore);
        var dueDate2 = edition.SecondPaymentDeadline
            ?? edition.StartDate.AddDays(-settings.SecondInstallmentDaysBefore);

        var payments = new List<Payment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                Amount = installment1Amount,
                PaymentDate = DateTime.UtcNow,
                Method = PaymentMethod.Transfer,
                Status = PaymentStatus.Pending,
                InstallmentNumber = 1,
                DueDate = dueDate1,
                TransferConcept = concept1,
                ConceptLinesSerialized = SerializeBaseConceptLines(
                    registration.Members, installment1Amount, registration.BaseTotalAmount),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                Amount = installment2Amount,
                PaymentDate = DateTime.UtcNow,
                Method = PaymentMethod.Transfer,
                Status = PaymentStatus.Pending,
                InstallmentNumber = 2,
                DueDate = dueDate2,
                TransferConcept = concept2,
                ConceptLinesSerialized = SerializeBaseConceptLines(
                    registration.Members, installment2Amount, registration.BaseTotalAmount),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await paymentsRepo.AddRangeAsync(payments, ct);

        logger.LogInformation(
            "Created {Count} installments for registration {RegistrationId}",
            2, registrationId);

        return payments.Select(p => MapToResponse(p, payments)).ToList();
    }

    public async Task<PaymentResponse> UploadProofAsync(
        Guid paymentId, Guid userId, IFormFile file, CancellationToken ct)
    {
        var payment = await paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)
            ?? throw new NotFoundException("Pago", paymentId);

        if (payment.Registration.RegisteredByUserId != userId)
            throw new BusinessRuleException("No tienes permiso para subir comprobante de este pago");

        if (payment.Status != PaymentStatus.Pending)
            throw new BusinessRuleException("Solo se puede subir comprobante para pagos pendientes");

        // Sequential payment enforcement
        var allPayments = await paymentsRepo.GetByRegistrationIdAsync(payment.RegistrationId, ct);
        if (!ComputeIsActionable(payment, allPayments))
            throw new BusinessRuleException("Debes completar el pago anterior antes de subir un comprobante.");

        await using var stream = file.OpenReadStream();
        var result = await blobStorageService.UploadAsync(
            stream, file.FileName, file.ContentType,
            "payment-proofs", paymentId, false, ct);

        payment.ProofFileUrl = result.FileUrl;
        payment.ProofFileName = file.FileName;
        payment.ProofUploadedAt = DateTime.UtcNow;
        payment.Status = PaymentStatus.PendingReview;

        await paymentsRepo.UpdateAsync(payment, ct);

        logger.LogInformation(
            "Proof uploaded for payment {PaymentId}, status changed to PendingReview",
            paymentId);

        return MapToResponse(payment, allPayments);
    }

    public async Task<PaymentResponse> RemoveProofAsync(
        Guid paymentId, Guid userId, CancellationToken ct)
    {
        var payment = await paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)
            ?? throw new NotFoundException("Pago", paymentId);

        if (payment.Registration.RegisteredByUserId != userId)
            throw new BusinessRuleException("No tienes permiso para eliminar el comprobante de este pago");

        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.PendingReview))
            throw new BusinessRuleException("No se puede eliminar el comprobante en el estado actual del pago");

        if (payment.ProofFileUrl is not null)
        {
            var key = ExtractBlobKey(payment.ProofFileUrl);
            await blobStorageService.DeleteManyAsync([key], ct);
        }

        payment.ProofFileUrl = null;
        payment.ProofFileName = null;
        payment.ProofUploadedAt = null;
        payment.Status = PaymentStatus.Pending;

        await paymentsRepo.UpdateAsync(payment, ct);

        logger.LogInformation(
            "Proof removed for payment {PaymentId}, status reset to Pending",
            paymentId);

        var allPayments = await paymentsRepo.GetByRegistrationIdAsync(payment.RegistrationId, ct);
        return MapToResponse(payment, allPayments);
    }

    public async Task<PaymentResponse> ConfirmPaymentAsync(
        Guid paymentId, Guid adminUserId, string? notes, CancellationToken ct)
    {
        var payment = await paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)
            ?? throw new NotFoundException("Pago", paymentId);

        if (payment.Status != PaymentStatus.PendingReview)
            throw new BusinessRuleException("Solo se pueden confirmar pagos en estado de revisión pendiente");

        payment.Status = PaymentStatus.Completed;
        payment.ConfirmedByUserId = adminUserId;
        payment.ConfirmedAt = DateTime.UtcNow;
        payment.AdminNotes = notes;

        await paymentsRepo.UpdateAsync(payment, ct);

        logger.LogInformation(
            "Payment {PaymentId} confirmed by admin {AdminUserId}",
            paymentId, adminUserId);

        // Check if all installments are completed -> confirm registration
        var allPayments = await paymentsRepo.GetByRegistrationIdAsync(
            payment.RegistrationId, ct);

        if (allPayments.All(p => p.Status == PaymentStatus.Completed))
        {
            var registration = payment.Registration;
            registration.Status = RegistrationStatus.Confirmed;
            await registrationsRepo.UpdateAsync(registration, ct);

            logger.LogInformation(
                "Registration {RegistrationId} confirmed - all installments paid",
                payment.RegistrationId);
        }

        return MapToResponse(payment, allPayments);
    }

    public async Task<PaymentResponse> RejectPaymentAsync(
        Guid paymentId, Guid adminUserId, string notes, CancellationToken ct)
    {
        var payment = await paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)
            ?? throw new NotFoundException("Pago", paymentId);

        if (payment.Status != PaymentStatus.PendingReview)
            throw new BusinessRuleException("Solo se pueden rechazar pagos en estado de revisión pendiente");

        payment.Status = PaymentStatus.Pending;
        payment.AdminNotes = notes;
        payment.ConfirmedByUserId = adminUserId;
        payment.ConfirmedAt = DateTime.UtcNow;

        await paymentsRepo.UpdateAsync(payment, ct);

        logger.LogInformation(
            "Payment {PaymentId} rejected by admin {AdminUserId}: {Reason}",
            paymentId, adminUserId, notes);

        var allPayments = await paymentsRepo.GetByRegistrationIdAsync(payment.RegistrationId, ct);
        return MapToResponse(payment, allPayments);
    }

    public async Task<List<PaymentResponse>> GetByRegistrationAsync(
        Guid registrationId, Guid userId, string? userRole, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (userRole is not ("Admin" or "Board") &&
            registration.RegisteredByUserId != userId)
            throw new BusinessRuleException("No tienes permiso para ver los pagos de esta inscripción");

        var payments = await paymentsRepo.GetByRegistrationIdAsync(registrationId, ct);
        return payments.Select(p => MapToResponse(p, payments)).ToList();
    }

    public async Task<PaymentResponse> GetByIdAsync(
        Guid paymentId, Guid userId, string? userRole, CancellationToken ct)
    {
        var payment = await paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)
            ?? throw new NotFoundException("Pago", paymentId);

        if (userRole is not ("Admin" or "Board") &&
            payment.Registration.RegisteredByUserId != userId)
            throw new BusinessRuleException("No tienes permiso para ver este pago");

        var allPayments = await paymentsRepo.GetByRegistrationIdAsync(payment.RegistrationId, ct);
        return MapToResponse(payment, allPayments);
    }

    public async Task<List<AdminPaymentResponse>> GetPendingReviewAsync(CancellationToken ct)
    {
        var payments = await paymentsRepo.GetPendingReviewAsync(ct);
        return payments.Select(p => MapToAdminResponse(p, payments)).ToList();
    }

    public async Task<(List<AdminPaymentResponse> Items, int TotalCount)> GetAllPaymentsAsync(
        PaymentFilterRequest filter, CancellationToken ct)
    {
        var (items, totalCount) = await paymentsRepo.GetFilteredAsync(filter, ct);
        // Group by registration to compute IsActionable per registration context
        var grouped = items.GroupBy(p => p.RegistrationId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var mapped = items.Select(p => MapToAdminResponse(p, grouped[p.RegistrationId])).ToList();
        return (mapped, totalCount);
    }

    // --- Manual payment methods (admin only) ---

    public async Task<AdminPaymentResponse> CreateManualPaymentAsync(
        Guid registrationId, CreateManualPaymentRequest request, Guid adminUserId, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var maxInstallment = registration.Payments.Any()
            ? registration.Payments.Max(p => p.InstallmentNumber)
            : 0;
        var nextInstallment = maxInstallment + 1;

        var settings = await LoadPaymentSettingsAsync(ct);

        var familyName = NormalizeName(registration.FamilyUnit.Name);
        var concept = $"{settings.TransferConceptPrefix}-{familyName}-{nextInstallment}";
        if (concept.Length > 100) concept = concept[..100];

        var conceptLines = new PaymentConceptLinesJson(
            MemberLines: null,
            ExtraLines: null,
            ManualLine: new ManualPaymentConceptLine(request.Description, request.Amount)
        );

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            RegistrationId = registrationId,
            Amount = request.Amount,
            PaymentDate = DateTime.UtcNow,
            Method = PaymentMethod.Transfer,
            Status = PaymentStatus.Pending,
            InstallmentNumber = nextInstallment,
            DueDate = request.DueDate,
            TransferConcept = concept,
            AdminNotes = request.AdminNotes,
            IsManual = true,
            ConceptLinesSerialized = JsonSerializer.Serialize(conceptLines),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await paymentsRepo.AddAsync(payment, ct);

        registration.TotalAmount += request.Amount;
        await registrationsRepo.UpdateAsync(registration, ct);

        logger.LogInformation(
            "Manual payment created for registration {RegistrationId}, amount={Amount}, installment={Installment}",
            registrationId, request.Amount, nextInstallment);

        // Re-load to get navigation properties for admin response
        var created = await paymentsRepo.GetByIdWithRegistrationAsync(payment.Id, ct);
        var allPayments = await paymentsRepo.GetByRegistrationIdAsync(registrationId, ct);
        return MapToAdminResponse(created!, allPayments);
    }

    public async Task<AdminPaymentResponse> UpdateManualPaymentAsync(
        Guid paymentId, UpdateManualPaymentRequest request, Guid adminUserId, CancellationToken ct)
    {
        var payment = await paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)
            ?? throw new NotFoundException("Pago", paymentId);

        if (!payment.IsManual)
            throw new BusinessRuleException("Solo se pueden editar pagos manuales.");

        if (payment.Status != PaymentStatus.Pending)
            throw new BusinessRuleException("Solo se pueden editar pagos en estado Pendiente.");

        var oldAmount = payment.Amount;

        if (request.Amount.HasValue)
            payment.Amount = request.Amount.Value;

        if (request.Description is not null)
        {
            var conceptLines = new PaymentConceptLinesJson(
                MemberLines: null,
                ExtraLines: null,
                ManualLine: new ManualPaymentConceptLine(
                    request.Description, request.Amount ?? payment.Amount)
            );
            payment.ConceptLinesSerialized = JsonSerializer.Serialize(conceptLines);
        }
        else if (request.Amount.HasValue)
        {
            var (_, _, manualLine) = DeserializeConceptLines(payment.ConceptLinesSerialized);
            if (manualLine is not null)
            {
                var conceptLines = new PaymentConceptLinesJson(
                    MemberLines: null,
                    ExtraLines: null,
                    ManualLine: manualLine with { Amount = request.Amount.Value }
                );
                payment.ConceptLinesSerialized = JsonSerializer.Serialize(conceptLines);
            }
        }

        if (request.DueDate.HasValue)
            payment.DueDate = request.DueDate.Value;

        if (request.AdminNotes is not null)
            payment.AdminNotes = request.AdminNotes;

        payment.UpdatedAt = DateTime.UtcNow;
        await paymentsRepo.UpdateAsync(payment, ct);

        if (request.Amount.HasValue && request.Amount.Value != oldAmount)
        {
            var registration = await registrationsRepo.GetByIdAsync(payment.RegistrationId, ct);
            if (registration is not null)
            {
                registration.TotalAmount += (request.Amount.Value - oldAmount);
                await registrationsRepo.UpdateAsync(registration, ct);
            }
        }

        logger.LogInformation(
            "Manual payment {PaymentId} updated by admin {AdminUserId}",
            paymentId, adminUserId);

        var allPayments = await paymentsRepo.GetByRegistrationIdAsync(payment.RegistrationId, ct);
        return MapToAdminResponse(payment, allPayments);
    }

    public async Task DeleteManualPaymentAsync(
        Guid paymentId, Guid adminUserId, CancellationToken ct)
    {
        var payment = await paymentsRepo.GetByIdAsync(paymentId, ct)
            ?? throw new NotFoundException("Pago", paymentId);

        if (!payment.IsManual)
            throw new BusinessRuleException("Solo se pueden eliminar pagos manuales.");

        if (payment.Status != PaymentStatus.Pending)
            throw new BusinessRuleException("Solo se pueden eliminar pagos en estado Pendiente.");

        var registration = await registrationsRepo.GetByIdAsync(payment.RegistrationId, ct);
        if (registration is not null)
        {
            registration.TotalAmount -= payment.Amount;
            await registrationsRepo.UpdateAsync(registration, ct);
        }

        await paymentsRepo.DeleteAsync(payment.Id, ct);

        logger.LogInformation(
            "Manual payment {PaymentId} deleted by admin {AdminUserId}",
            paymentId, adminUserId);
    }

    // --- Payment settings ---

    public async Task<PaymentSettingsResponse> GetPaymentSettingsAsync(CancellationToken ct)
    {
        var settings = await LoadPaymentSettingsAsync(ct);
        return new PaymentSettingsResponse(
            settings.Iban, settings.BankName, settings.AccountHolder,
            settings.FirstInstallmentDaysBefore,
            settings.SecondInstallmentDaysBefore,
            settings.ExtrasInstallmentDaysFromCampStart,
            settings.TransferConceptPrefix);
    }

    public async Task<PaymentSettingsResponse> UpdatePaymentSettingsAsync(
        PaymentSettingsRequest request, Guid adminUserId, CancellationToken ct)
    {
        var json = new PaymentSettingsJson
        {
            Iban = request.Iban,
            BankName = request.BankName,
            AccountHolder = request.AccountHolder,
            FirstInstallmentDaysBefore = request.FirstInstallmentDaysBefore,
            SecondInstallmentDaysBefore = request.SecondInstallmentDaysBefore,
            ExtrasInstallmentDaysFromCampStart = request.ExtrasInstallmentDaysFromCampStart,
            TransferConceptPrefix = request.TransferConceptPrefix
        };

        var existing = await settingsRepo.GetByKeyAsync(PaymentSettingsKey, ct);

        if (existing is not null)
        {
            existing.SettingValue = JsonSerializer.Serialize(json);
            existing.UpdatedBy = adminUserId;
            existing.UpdatedAt = DateTime.UtcNow;
            await settingsRepo.UpdateAsync(existing, ct);
        }
        else
        {
            var newSetting = new AssociationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = PaymentSettingsKey,
                SettingValue = JsonSerializer.Serialize(json),
                UpdatedBy = adminUserId,
                UpdatedAt = DateTime.UtcNow
            };
            await settingsRepo.CreateAsync(newSetting, ct);
        }

        logger.LogInformation(
            "Payment settings updated by admin {AdminUserId}", adminUserId);

        return new PaymentSettingsResponse(
            request.Iban, request.BankName, request.AccountHolder,
            request.FirstInstallmentDaysBefore,
            request.SecondInstallmentDaysBefore,
            request.ExtrasInstallmentDaysFromCampStart,
            request.TransferConceptPrefix);
    }

    // --- Private helpers ---

    private static bool ComputeIsActionable(Payment payment, List<Payment> allPayments)
    {
        // Manual payments are always actionable if Pending
        if (payment.IsManual)
            return payment.Status == PaymentStatus.Pending;

        // Non-pending payments are not actionable
        if (payment.Status != PaymentStatus.Pending)
            return false;

        // P1 is always actionable
        if (payment.InstallmentNumber == 1)
            return true;

        // P2 requires P1 Completed
        if (payment.InstallmentNumber == 2)
        {
            var p1 = allPayments.FirstOrDefault(p => p.InstallmentNumber == 1 && !p.IsManual);
            return p1?.Status == PaymentStatus.Completed;
        }

        // P3 requires P2 Completed
        if (payment.InstallmentNumber == 3)
        {
            var p2 = allPayments.FirstOrDefault(p => p.InstallmentNumber == 2 && !p.IsManual);
            return p2?.Status == PaymentStatus.Completed;
        }

        return false;
    }

    private async Task<PaymentSettingsJson> LoadPaymentSettingsAsync(CancellationToken ct)
    {
        var setting = await settingsRepo.GetByKeyAsync(PaymentSettingsKey, ct);
        if (setting is null) return new PaymentSettingsJson();

        try
        {
            return JsonSerializer.Deserialize<PaymentSettingsJson>(setting.SettingValue)
                   ?? new PaymentSettingsJson();
        }
        catch (JsonException)
        {
            logger.LogWarning("Failed to deserialize payment settings, using defaults");
            return new PaymentSettingsJson();
        }
    }

    private static PaymentResponse MapToResponse(Payment p, List<Payment> allPayments)
    {
        var (memberLines, extraLines, manualLine) = DeserializeConceptLines(p.ConceptLinesSerialized);
        return new PaymentResponse(
            p.Id, p.RegistrationId, p.InstallmentNumber, p.Amount, p.DueDate,
            p.Method, p.Status, p.TransferConcept, p.ProofFileUrl, p.ProofFileName,
            p.ProofUploadedAt, p.AdminNotes, p.CreatedAt,
            ComputeIsActionable(p, allPayments), p.IsManual,
            memberLines, extraLines, manualLine);
    }

    private static AdminPaymentResponse MapToAdminResponse(Payment p, List<Payment> allPayments)
    {
        var (memberLines, extraLines, manualLine) = DeserializeConceptLines(p.ConceptLinesSerialized);
        return new AdminPaymentResponse(
            p.Id, p.RegistrationId,
            p.Registration.FamilyUnit.Name,
            p.Registration.CampEdition.Camp.Name,
            p.InstallmentNumber, p.Amount, p.DueDate,
            p.Status, p.TransferConcept, p.ProofFileUrl, p.ProofFileName,
            p.ProofUploadedAt, p.AdminNotes,
            null, // ConfirmedByUserName - would need user lookup, kept null for now
            p.ConfirmedAt, p.CreatedAt,
            ComputeIsActionable(p, allPayments), p.IsManual,
            memberLines, extraLines, manualLine);
    }

    private static (List<PaymentConceptLine>?, List<PaymentExtraConceptLine>?, ManualPaymentConceptLine?) DeserializeConceptLines(
        string? json)
    {
        if (json is null) return (null, null, null);
        try
        {
            var data = JsonSerializer.Deserialize<PaymentConceptLinesJson>(json);
            return (data?.MemberLines, data?.ExtraLines, data?.ManualLine);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    internal static string SerializeBaseConceptLines(
        ICollection<RegistrationMember> members, decimal installmentAmount, decimal baseTotalAmount)
    {
        if (baseTotalAmount == 0)
            return JsonSerializer.Serialize(new PaymentConceptLinesJson([], null));

        var percentage = Math.Round(installmentAmount / baseTotalAmount * 100m, 2);
        var lines = new List<PaymentConceptLine>();
        var runningTotal = 0m;

        var memberList = members.ToList();
        for (var i = 0; i < memberList.Count; i++)
        {
            var m = memberList[i];
            decimal amountInPayment;

            if (i == memberList.Count - 1)
            {
                // Last member gets the remainder to avoid rounding drift
                amountInPayment = installmentAmount - runningTotal;
            }
            else
            {
                amountInPayment = Math.Round(m.IndividualAmount * installmentAmount / baseTotalAmount, 2);
                runningTotal += amountInPayment;
            }

            lines.Add(new PaymentConceptLine(
                $"{m.FamilyMember.FirstName} {m.FamilyMember.LastName}",
                MapAgeCategory(m.AgeCategory),
                MapAttendancePeriod(m.AttendancePeriod),
                m.IndividualAmount,
                amountInPayment,
                percentage));
        }

        return JsonSerializer.Serialize(new PaymentConceptLinesJson(lines, null));
    }

    internal static string SerializeExtrasConceptLines(ICollection<RegistrationExtra> extras)
    {
        var lines = extras.Select(e => new PaymentExtraConceptLine(
            e.CampEditionExtra.Name,
            e.Quantity,
            e.UnitPrice,
            e.TotalAmount,
            e.UserInput,
            e.CampEditionExtra.PricingType.ToString()
        )).ToList();

        return JsonSerializer.Serialize(new PaymentConceptLinesJson(null, lines));
    }

    private static string MapAgeCategory(AgeCategory category) => category switch
    {
        AgeCategory.Baby => "Bebé",
        AgeCategory.Child => "Niño",
        AgeCategory.Adult => "Adulto",
        _ => category.ToString()
    };

    private static string MapAttendancePeriod(AttendancePeriod period) => period switch
    {
        AttendancePeriod.Complete => "Completo",
        AttendancePeriod.FirstWeek => "1ª Semana",
        AttendancePeriod.SecondWeek => "2ª Semana",
        AttendancePeriod.WeekendVisit => "Fin de semana",
        _ => period.ToString()
    };

    private static string NormalizeName(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var cleanName = sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();

        return string.Concat(
            cleanName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length >= 3 ? word[..3] : word));
    }

    public async Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
    {
        await paymentsRepo.DeleteByRegistrationIdAsync(registrationId, ct);
        logger.LogInformation(
            "All payments deleted for registration {RegistrationId} during registration cleanup",
            registrationId);
    }

    public async Task<PaymentResponse?> SyncExtrasInstallmentAsync(
        Guid registrationId, decimal extrasAmount, CancellationToken ct)
    {
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        var payments = await paymentsRepo.GetByRegistrationIdTrackedAsync(registrationId, ct);
        var p3 = payments.FirstOrDefault(p => p.InstallmentNumber == 3 && !p.IsManual);

        var settings = await LoadPaymentSettingsAsync(ct);
        var dueDate = registration.CampEdition.ExtrasPaymentDeadline
            ?? registration.CampEdition.StartDate.AddDays(settings.ExtrasInstallmentDaysFromCampStart);

        if (extrasAmount > 0)
        {
            if (p3 != null)
            {
                if (p3.Status is PaymentStatus.PendingReview or PaymentStatus.Completed)
                    throw new BusinessRuleException(
                        "No se puede modificar el pago de extras porque ya tiene un justificante en revisión o está confirmado.");

                p3.Amount = extrasAmount;
                p3.DueDate = dueDate;
                p3.ConceptLinesSerialized = SerializeExtrasConceptLines(registration.Extras);
                p3.UpdatedAt = DateTime.UtcNow;
                await paymentsRepo.UpdateAsync(p3, ct);
            }
            else
            {
                var familyName = NormalizeName(registration.FamilyUnit.Name);
                var concept = $"{settings.TransferConceptPrefix}-{familyName}-3";
                if (concept.Length > 100) concept = concept[..100];

                var newP3 = new Payment
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    Amount = extrasAmount,
                    PaymentDate = DateTime.UtcNow,
                    Method = PaymentMethod.Transfer,
                    Status = PaymentStatus.Pending,
                    InstallmentNumber = 3,
                    DueDate = dueDate,
                    TransferConcept = concept,
                    ConceptLinesSerialized = SerializeExtrasConceptLines(registration.Extras),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await paymentsRepo.AddAsync(newP3, ct);
                p3 = newP3;
            }

            logger.LogInformation(
                "SyncExtrasInstallment: P3 synced for registration {RegistrationId}, amount={Amount}",
                registrationId, extrasAmount);

            var allPayments = await paymentsRepo.GetByRegistrationIdAsync(registrationId, ct);
            return MapToResponse(p3, allPayments);
        }
        else
        {
            if (p3 != null)
            {
                if (p3.Status is PaymentStatus.PendingReview or PaymentStatus.Completed)
                    throw new BusinessRuleException(
                        "No se puede eliminar el pago de extras porque ya tiene un justificante en revisión o está confirmado.");

                await paymentsRepo.DeleteAsync(p3.Id, ct);

                logger.LogInformation(
                    "SyncExtrasInstallment: P3 deleted for registration {RegistrationId}",
                    registrationId);
            }

            return null;
        }
    }

    public async Task SyncBaseInstallmentsAsync(
        Guid registrationId, decimal newBaseTotalAmount, decimal oldBaseTotalAmount, CancellationToken ct)
    {
        var payments = await paymentsRepo.GetByRegistrationIdTrackedAsync(registrationId, ct);
        var basePayments = payments.Where(p => !p.IsManual).ToList();
        var p1 = basePayments.FirstOrDefault(p => p.InstallmentNumber == 1)
            ?? throw new InvalidOperationException($"P1 not found for registration {registrationId}");
        var p2 = basePayments.FirstOrDefault(p => p.InstallmentNumber == 2)
            ?? throw new InvalidOperationException($"P2 not found for registration {registrationId}");

        if (p1.Status == PaymentStatus.PendingReview)
            throw new BusinessRuleException(
                "No se pueden modificar los miembros porque el primer pago tiene un justificante en revisión.");

        // Load registration with members for concept line regeneration
        var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Inscripción", registrationId);

        if (p1.Status == PaymentStatus.Pending)
        {
            var newP1 = Math.Ceiling(newBaseTotalAmount / 2m);
            var newP2 = newBaseTotalAmount - newP1;
            p1.Amount = newP1;
            p1.ConceptLinesSerialized = SerializeBaseConceptLines(
                registration.Members, newP1, newBaseTotalAmount);
            p2.Amount = newP2;
            p2.ConceptLinesSerialized = SerializeBaseConceptLines(
                registration.Members, newP2, newBaseTotalAmount);
            await paymentsRepo.UpdateAsync(p1, ct);
            await paymentsRepo.UpdateAsync(p2, ct);

            logger.LogInformation(
                "SyncBaseInstallments: Both pending — recalculated P1={P1}, P2={P2} for registration {RegistrationId}",
                newP1, newP2, registrationId);
        }
        else if (p1.Status == PaymentStatus.Completed)
        {
            if (p2.Status is PaymentStatus.PendingReview or PaymentStatus.Completed)
                throw new BusinessRuleException(
                    "No se pueden modificar los miembros porque el segundo pago ya está confirmado o en revisión.");

            var delta = newBaseTotalAmount - oldBaseTotalAmount;
            if (p2.Amount + delta <= 0)
                throw new BusinessRuleException(
                    "El cambio en los miembros haría que el segundo plazo fuera negativo o cero. Contacta al administrador.");

            p2.Amount += delta;
            p2.ConceptLinesSerialized = SerializeBaseConceptLines(
                registration.Members, p2.Amount, newBaseTotalAmount);
            await paymentsRepo.UpdateAsync(p2, ct);

            logger.LogInformation(
                "SyncBaseInstallments: P1 completed — absorbed delta={Delta} into P2={P2} for registration {RegistrationId}",
                delta, p2.Amount, registrationId);
        }
    }

    private static string ExtractBlobKey(string fileUrl)
    {
        // URLs are like https://cdn.example.com/payment-proofs/xxx/file.jpg
        // Key is everything after the domain: payment-proofs/xxx/file.jpg
        var uri = new Uri(fileUrl);
        return uri.AbsolutePath.TrimStart('/');
    }
}

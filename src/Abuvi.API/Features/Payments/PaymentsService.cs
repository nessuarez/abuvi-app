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

        var dueDate2 = registration.CampEdition.StartDate
            .AddDays(-settings.SecondInstallmentDaysBefore);

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
                DueDate = DateTime.UtcNow,
                TransferConcept = concept1,
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await paymentsRepo.AddRangeAsync(payments, ct);

        logger.LogInformation(
            "Created {Count} installments for registration {RegistrationId}",
            2, registrationId);

        return payments.Select(MapToResponse).ToList();
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

        return MapToResponse(payment);
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

        return MapToResponse(payment);
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

        return MapToResponse(payment);
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

        return MapToResponse(payment);
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
        return payments.Select(MapToResponse).ToList();
    }

    public async Task<PaymentResponse> GetByIdAsync(
        Guid paymentId, Guid userId, string? userRole, CancellationToken ct)
    {
        var payment = await paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)
            ?? throw new NotFoundException("Pago", paymentId);

        if (userRole is not ("Admin" or "Board") &&
            payment.Registration.RegisteredByUserId != userId)
            throw new BusinessRuleException("No tienes permiso para ver este pago");

        return MapToResponse(payment);
    }

    public async Task<List<AdminPaymentResponse>> GetPendingReviewAsync(CancellationToken ct)
    {
        var payments = await paymentsRepo.GetPendingReviewAsync(ct);
        return payments.Select(MapToAdminResponse).ToList();
    }

    public async Task<(List<AdminPaymentResponse> Items, int TotalCount)> GetAllPaymentsAsync(
        PaymentFilterRequest filter, CancellationToken ct)
    {
        var (items, totalCount) = await paymentsRepo.GetFilteredAsync(filter, ct);
        return (items.Select(MapToAdminResponse).ToList(), totalCount);
    }

    public async Task<PaymentSettingsResponse> GetPaymentSettingsAsync(CancellationToken ct)
    {
        var settings = await LoadPaymentSettingsAsync(ct);
        return new PaymentSettingsResponse(
            settings.Iban, settings.BankName, settings.AccountHolder,
            settings.SecondInstallmentDaysBefore, settings.TransferConceptPrefix);
    }

    public async Task<PaymentSettingsResponse> UpdatePaymentSettingsAsync(
        PaymentSettingsRequest request, Guid adminUserId, CancellationToken ct)
    {
        var json = new PaymentSettingsJson
        {
            Iban = request.Iban,
            BankName = request.BankName,
            AccountHolder = request.AccountHolder,
            SecondInstallmentDaysBefore = request.SecondInstallmentDaysBefore,
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
            request.SecondInstallmentDaysBefore, request.TransferConceptPrefix);
    }

    // --- Private helpers ---

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

    private static PaymentResponse MapToResponse(Payment p) => new(
        p.Id, p.RegistrationId, p.InstallmentNumber, p.Amount, p.DueDate,
        p.Method, p.Status, p.TransferConcept, p.ProofFileUrl, p.ProofFileName,
        p.ProofUploadedAt, p.AdminNotes, p.CreatedAt);

    private static AdminPaymentResponse MapToAdminResponse(Payment p) => new(
        p.Id, p.RegistrationId,
        p.Registration.FamilyUnit.Name,
        p.Registration.CampEdition.Camp.Name,
        p.InstallmentNumber, p.Amount, p.DueDate,
        p.Status, p.TransferConcept, p.ProofFileUrl, p.ProofFileName,
        p.ProofUploadedAt, p.AdminNotes,
        null, // ConfirmedByUserName - would need user lookup, kept null for now
        p.ConfirmedAt, p.CreatedAt);

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

    private static string ExtractBlobKey(string fileUrl)
    {
        // URLs are like https://cdn.example.com/payment-proofs/xxx/file.jpg
        // Key is everything after the domain: payment-proofs/xxx/file.jpg
        var uri = new Uri(fileUrl);
        return uri.AbsolutePath.TrimStart('/');
    }
}

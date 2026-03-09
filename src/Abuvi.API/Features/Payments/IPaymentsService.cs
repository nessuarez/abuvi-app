namespace Abuvi.API.Features.Payments;

public interface IPaymentsService
{
    Task<List<PaymentResponse>> CreateInstallmentsAsync(Guid registrationId, CancellationToken ct);
    Task<PaymentResponse> UploadProofAsync(Guid paymentId, Guid userId, IFormFile file, CancellationToken ct);
    Task<PaymentResponse> RemoveProofAsync(Guid paymentId, Guid userId, CancellationToken ct);
    Task<PaymentResponse> ConfirmPaymentAsync(Guid paymentId, Guid adminUserId, string? notes, CancellationToken ct);
    Task<PaymentResponse> RejectPaymentAsync(Guid paymentId, Guid adminUserId, string notes, CancellationToken ct);
    Task<List<PaymentResponse>> GetByRegistrationAsync(Guid registrationId, Guid userId, string? userRole, CancellationToken ct);
    Task<PaymentResponse> GetByIdAsync(Guid paymentId, Guid userId, string? userRole, CancellationToken ct);
    Task<List<AdminPaymentResponse>> GetPendingReviewAsync(CancellationToken ct);
    Task<(List<AdminPaymentResponse> Items, int TotalCount)> GetAllPaymentsAsync(PaymentFilterRequest filter, CancellationToken ct);
    Task<PaymentSettingsResponse> GetPaymentSettingsAsync(CancellationToken ct);
    Task<PaymentSettingsResponse> UpdatePaymentSettingsAsync(PaymentSettingsRequest request, Guid adminUserId, CancellationToken ct);
    Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct);

    /// <summary>Creates, updates, or deletes the extras installment (P3) for a registration.</summary>
    Task<PaymentResponse?> SyncExtrasInstallmentAsync(
        Guid registrationId, decimal extrasAmount, CancellationToken ct);

    /// <summary>Recalculates P1 and/or P2 when the base member total changes.</summary>
    Task SyncBaseInstallmentsAsync(
        Guid registrationId, decimal newBaseTotalAmount, decimal oldBaseTotalAmount, CancellationToken ct);
}

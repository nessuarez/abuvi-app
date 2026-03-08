using Abuvi.API.Features.Registrations;

namespace Abuvi.API.Features.Payments;

public interface IPaymentsRepository
{
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken ct);
    Task<Payment?> GetByIdWithRegistrationAsync(Guid paymentId, CancellationToken ct);
    Task<List<Payment>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task AddRangeAsync(List<Payment> payments, CancellationToken ct);
    Task UpdateAsync(Payment payment, CancellationToken ct);
    Task<decimal> GetTotalCompletedAsync(Guid registrationId, CancellationToken ct);
    Task<List<Payment>> GetPendingReviewAsync(CancellationToken ct);
    Task<(List<Payment> Items, int TotalCount)> GetFilteredAsync(PaymentFilterRequest filter, CancellationToken ct);
    Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
}

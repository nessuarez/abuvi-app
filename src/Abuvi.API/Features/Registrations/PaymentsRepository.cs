using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Registrations;

public interface IPaymentsRepository
{
    Task<decimal> GetTotalCompletedAsync(Guid registrationId, CancellationToken ct);
}

public class PaymentsRepository(AbuviDbContext db) : IPaymentsRepository
{
    public async Task<decimal> GetTotalCompletedAsync(Guid registrationId, CancellationToken ct)
        => await db.Payments
            .Where(p => p.RegistrationId == registrationId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount, ct);
}

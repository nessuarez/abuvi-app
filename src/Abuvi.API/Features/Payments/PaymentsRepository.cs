using Abuvi.API.Data;
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Payments;

public class PaymentsRepository(AbuviDbContext db) : IPaymentsRepository
{
    public async Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken ct)
        => await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public async Task<Payment?> GetByIdWithRegistrationAsync(Guid paymentId, CancellationToken ct)
        => await db.Payments
            .Include(p => p.Registration)
                .ThenInclude(r => r.CampEdition)
                    .ThenInclude(ce => ce.Camp)
            .Include(p => p.Registration)
                .ThenInclude(r => r.FamilyUnit)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public async Task<List<Payment>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
        => await db.Payments
            .Where(p => p.RegistrationId == registrationId)
            .OrderBy(p => p.InstallmentNumber)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<List<Payment>> GetByRegistrationIdTrackedAsync(Guid registrationId, CancellationToken ct)
        => await db.Payments
            .Where(p => p.RegistrationId == registrationId)
            .OrderBy(p => p.InstallmentNumber)
            .ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct)
    {
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(List<Payment> payments, CancellationToken ct)
    {
        db.Payments.AddRange(payments);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct)
    {
        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetTotalCompletedAsync(Guid registrationId, CancellationToken ct)
        => await db.Payments
            .Where(p => p.RegistrationId == registrationId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount, ct);

    public async Task<List<Payment>> GetPendingReviewAsync(CancellationToken ct)
        => await db.Payments
            .Include(p => p.Registration)
                .ThenInclude(r => r.FamilyUnit)
            .Include(p => p.Registration)
                .ThenInclude(r => r.CampEdition)
                    .ThenInclude(ce => ce.Camp)
            .Where(p => p.Status == PaymentStatus.PendingReview)
            .OrderBy(p => p.ProofUploadedAt)
            .ToListAsync(ct);

    public async Task DeleteAsync(Guid paymentId, CancellationToken ct)
        => await db.Payments
            .Where(p => p.Id == paymentId)
            .ExecuteDeleteAsync(ct);

    public async Task DeleteByRegistrationIdAsync(Guid registrationId, CancellationToken ct)
        => await db.Payments
            .Where(p => p.RegistrationId == registrationId)
            .ExecuteDeleteAsync(ct);

    public async Task<(List<Payment> Items, int TotalCount)> GetFilteredAsync(
        PaymentFilterRequest filter, CancellationToken ct)
    {
        var query = db.Payments
            .Include(p => p.Registration)
                .ThenInclude(r => r.FamilyUnit)
            .Include(p => p.Registration)
                .ThenInclude(r => r.CampEdition)
                    .ThenInclude(ce => ce.Camp)
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.CampEditionId.HasValue)
            query = query.Where(p => p.Registration.CampEditionId == filter.CampEditionId.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(p => p.CreatedAt <= filter.ToDate.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}

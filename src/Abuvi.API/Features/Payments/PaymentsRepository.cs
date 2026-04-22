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
        var query = from p in db.Payments.AsNoTracking()
                    join r in db.Registrations on p.RegistrationId equals r.Id
                    join fu in db.FamilyUnits on r.FamilyUnitId equals fu.Id
                    join u in db.Users on r.RegisteredByUserId equals u.Id
                    select new
                    {
                        Payment = p,
                        FamilyName = fu.Name,
                        RepresentativeName = u.FirstName + " " + u.LastName,
                        CampEditionId = r.CampEditionId
                    };

        if (filter.Status.HasValue)
            query = query.Where(x => x.Payment.Status == filter.Status.Value);

        if (filter.CampEditionId.HasValue)
            query = query.Where(x => x.CampEditionId == filter.CampEditionId.Value);

        if (filter.InstallmentNumber.HasValue)
        {
            if (filter.InstallmentNumber.Value >= 3)
                query = query.Where(x => x.Payment.InstallmentNumber >= 3);
            else
                query = query.Where(x => x.Payment.InstallmentNumber == filter.InstallmentNumber.Value);
        }

        if (filter.FromDate.HasValue)
            query = query.Where(x => x.Payment.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(x => x.Payment.CreatedAt <= filter.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(x =>
                x.FamilyName.ToLower().Contains(term) ||
                x.RepresentativeName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var pagedIds = await query
            .OrderByDescending(x => x.Payment.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => x.Payment.Id)
            .ToListAsync(ct);

        var items = await db.Payments
            .Include(p => p.Registration)
                .ThenInclude(r => r.FamilyUnit)
            .Include(p => p.Registration)
                .ThenInclude(r => r.CampEdition)
                    .ThenInclude(ce => ce.Camp)
            .Where(p => pagedIds.Contains(p.Id))
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, totalCount);
    }
}

using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;

namespace Abuvi.API.Features.Memberships;

public interface IMembershipsRepository
{
    Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Membership?> GetByFamilyMemberIdAsync(Guid familyMemberId, CancellationToken ct);
    Task<IReadOnlyList<Membership>> GetActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<Membership>> GetOverdueAsync(CancellationToken ct);
    Task AddAsync(Membership membership, CancellationToken ct);
    Task UpdateAsync(Membership membership, CancellationToken ct);
    Task<MembershipFee?> GetFeeByIdAsync(Guid feeId, CancellationToken ct);
    Task<MembershipFee?> GetCurrentYearFeeAsync(Guid membershipId, CancellationToken ct);
    Task<IReadOnlyList<MembershipFee>> GetFeesByMembershipAsync(Guid membershipId, CancellationToken ct);
    Task AddFeeAsync(MembershipFee fee, CancellationToken ct);
    Task UpdateFeeAsync(MembershipFee fee, CancellationToken ct);

    // Member number operations
    Task<int> GetNextMemberNumberAsync(CancellationToken ct);
    Task<bool> IsMemberNumberTakenAsync(int memberNumber, Guid? excludeId, CancellationToken ct);

    // Family-level fee check
    Task<bool> HasPaidCurrentYearFeeForFamilyAsync(Guid familyUnitId, CancellationToken ct);
}

public class MembershipsRepository(AbuviDbContext db) : IMembershipsRepository
{
    public async Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Include(m => m.Fees)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Membership?> GetByFamilyMemberIdAsync(Guid familyMemberId, CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Include(m => m.Fees)
            .FirstOrDefaultAsync(m => m.FamilyMemberId == familyMemberId && m.IsActive, ct);

    public async Task<IReadOnlyList<Membership>> GetActiveAsync(CancellationToken ct)
        => await db.Memberships
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Include(m => m.FamilyMember)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Membership>> GetOverdueAsync(CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        return await db.Memberships
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Include(m => m.Fees)
            .Where(m => m.Fees.Any(f => f.Year == currentYear && f.Status == FeeStatus.Overdue))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Membership membership, CancellationToken ct)
    {
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Membership membership, CancellationToken ct)
    {
        db.Memberships.Update(membership);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MembershipFee?> GetFeeByIdAsync(Guid feeId, CancellationToken ct)
        => await db.MembershipFees
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == feeId, ct);

    public async Task<MembershipFee?> GetCurrentYearFeeAsync(Guid membershipId, CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        return await db.MembershipFees
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.MembershipId == membershipId && f.Year == currentYear, ct);
    }

    public async Task<IReadOnlyList<MembershipFee>> GetFeesByMembershipAsync(Guid membershipId, CancellationToken ct)
        => await db.MembershipFees
            .AsNoTracking()
            .Where(f => f.MembershipId == membershipId)
            .OrderByDescending(f => f.Year)
            .ToListAsync(ct);

    public async Task AddFeeAsync(MembershipFee fee, CancellationToken ct)
    {
        db.MembershipFees.Add(fee);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateFeeAsync(MembershipFee fee, CancellationToken ct)
    {
        db.MembershipFees.Update(fee);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetNextMemberNumberAsync(CancellationToken ct)
    {
        var max = await db.Memberships
            .Where(m => m.MemberNumber != null)
            .MaxAsync(m => (int?)m.MemberNumber, ct);
        return (max ?? 0) + 1;
    }

    public async Task<bool> IsMemberNumberTakenAsync(int memberNumber, Guid? excludeId, CancellationToken ct)
    {
        return await db.Memberships
            .AnyAsync(m => m.MemberNumber == memberNumber
                && (!excludeId.HasValue || m.Id != excludeId.Value), ct);
    }

    public async Task<bool> HasPaidCurrentYearFeeForFamilyAsync(Guid familyUnitId, CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        return await db.MembershipFees
            .AnyAsync(f => f.Membership.FamilyMember.FamilyUnitId == familyUnitId
                && f.Year == currentYear
                && f.Status == FeeStatus.Paid
                && f.Membership.IsActive, ct);
    }
}

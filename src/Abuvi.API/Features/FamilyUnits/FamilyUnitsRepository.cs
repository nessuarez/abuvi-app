namespace Abuvi.API.Features.FamilyUnits;

using Abuvi.API.Data;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Repository interface for family units and members data access
/// </summary>
public interface IFamilyUnitsRepository
{
    // Family Unit operations
    Task<FamilyUnit?> GetFamilyUnitByIdAsync(Guid id, CancellationToken ct);
    Task<FamilyUnit?> GetFamilyUnitByRepresentativeIdAsync(Guid userId, CancellationToken ct);
    Task<FamilyUnit?> GetFamilyUnitByMemberUserIdAsync(Guid userId, CancellationToken ct);
    Task CreateFamilyUnitAsync(FamilyUnit familyUnit, CancellationToken ct);
    Task UpdateFamilyUnitAsync(FamilyUnit familyUnit, CancellationToken ct);
    Task DeleteFamilyUnitAsync(Guid id, CancellationToken ct);

    // Family Member operations
    Task<FamilyMember?> GetFamilyMemberByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<FamilyMember>> GetFamilyMembersByFamilyUnitIdAsync(Guid familyUnitId, CancellationToken ct);
    Task CreateFamilyMemberAsync(FamilyMember member, CancellationToken ct);
    Task UpdateFamilyMemberAsync(FamilyMember member, CancellationToken ct);
    Task DeleteFamilyMemberAsync(Guid id, CancellationToken ct);

    // User operations
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetUserByEmailAsync(string email, CancellationToken ct);
    Task UpdateUserFamilyUnitIdAsync(Guid userId, Guid? familyUnitId, CancellationToken ct);

    /// <summary>
    /// Finds all family members that have the given email (for post-registration linking).
    /// </summary>
    Task<IReadOnlyList<FamilyMember>> GetFamilyMembersByEmailAsync(string email, CancellationToken ct);

    // Family number operations
    Task<int> GetNextFamilyNumberAsync(CancellationToken ct);
    Task<bool> IsFamilyNumberTakenAsync(int familyNumber, Guid? excludeId, CancellationToken ct);

    // Admin list
    /// <summary>
    /// Returns a paginated list of all family units with representative name and member count.
    /// Supports text search on family name or representative full name.
    /// </summary>
    Task<(List<FamilyUnitAdminProjection> Items, int TotalCount)> GetAllPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? sortBy,
        string? sortOrder,
        string? membershipStatus,
        bool? isActive,
        CancellationToken ct);

    // Admin operations
    Task<bool> HasRegistrationsAsync(Guid familyUnitId, CancellationToken ct);
    Task ClearAllUserFamilyUnitLinksAsync(Guid familyUnitId, CancellationToken ct);
    Task UpdateFamilyUnitStatusAsync(Guid familyUnitId, bool isActive, CancellationToken ct);
    Task<bool> MemberHasActiveRegistrationsAsync(Guid memberId, CancellationToken ct);
}

/// <summary>
/// EF Core implementation of family units repository
/// </summary>
public class FamilyUnitsRepository(AbuviDbContext db) : IFamilyUnitsRepository
{
    // Family Unit operations
    public async Task<FamilyUnit?> GetFamilyUnitByIdAsync(Guid id, CancellationToken ct)
        => await db.FamilyUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(fu => fu.Id == id, ct);

    public async Task<FamilyUnit?> GetFamilyUnitByRepresentativeIdAsync(Guid userId, CancellationToken ct)
        => await db.FamilyUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(fu => fu.RepresentativeUserId == userId, ct);

    public async Task<FamilyUnit?> GetFamilyUnitByMemberUserIdAsync(Guid userId, CancellationToken ct)
        => await db.FamilyUnits
            .AsNoTracking()
            .Where(fu => db.FamilyMembers.Any(fm => fm.FamilyUnitId == fu.Id && fm.UserId == userId))
            .FirstOrDefaultAsync(ct);

    public async Task CreateFamilyUnitAsync(FamilyUnit familyUnit, CancellationToken ct)
    {
        db.FamilyUnits.Add(familyUnit);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateFamilyUnitAsync(FamilyUnit familyUnit, CancellationToken ct)
    {
        familyUnit.UpdatedAt = DateTime.UtcNow;
        db.FamilyUnits.Update(familyUnit);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteFamilyUnitAsync(Guid id, CancellationToken ct)
    {
        await db.FamilyUnits
            .Where(fu => fu.Id == id)
            .ExecuteDeleteAsync(ct);
    }

    // Family Member operations
    public async Task<FamilyMember?> GetFamilyMemberByIdAsync(Guid id, CancellationToken ct)
        => await db.FamilyMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(fm => fm.Id == id, ct);

    public async Task<IReadOnlyList<FamilyMember>> GetFamilyMembersByFamilyUnitIdAsync(
        Guid familyUnitId, CancellationToken ct)
        => await db.FamilyMembers
            .AsNoTracking()
            .Where(fm => fm.FamilyUnitId == familyUnitId)
            .OrderBy(fm => fm.CreatedAt)
            .ToListAsync(ct);

    public async Task CreateFamilyMemberAsync(FamilyMember member, CancellationToken ct)
    {
        db.FamilyMembers.Add(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateFamilyMemberAsync(FamilyMember member, CancellationToken ct)
    {
        member.UpdatedAt = DateTime.UtcNow;
        db.FamilyMembers.Update(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteFamilyMemberAsync(Guid id, CancellationToken ct)
    {
        await db.FamilyMembers
            .Where(fm => fm.Id == id)
            .ExecuteDeleteAsync(ct);
    }

    // User operations
    public async Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct)
        => await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken ct)
        => await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task UpdateUserFamilyUnitIdAsync(Guid userId, Guid? familyUnitId, CancellationToken ct)
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FamilyUnitId, familyUnitId)
                .SetProperty(u => u.UpdatedAt, DateTime.UtcNow), ct);
    }

    public async Task<IReadOnlyList<FamilyMember>> GetFamilyMembersByEmailAsync(string email, CancellationToken ct)
        => await db.FamilyMembers
            .Where(fm => fm.Email == email && fm.UserId == null)
            .ToListAsync(ct);

    public async Task<int> GetNextFamilyNumberAsync(CancellationToken ct)
    {
        var max = await db.FamilyUnits
            .Where(fu => fu.FamilyNumber != null)
            .MaxAsync(fu => (int?)fu.FamilyNumber, ct);
        return (max ?? 0) + 1;
    }

    public async Task<bool> IsFamilyNumberTakenAsync(int familyNumber, Guid? excludeId, CancellationToken ct)
    {
        return await db.FamilyUnits
            .AnyAsync(fu => fu.FamilyNumber == familyNumber
                && (!excludeId.HasValue || fu.Id != excludeId.Value), ct);
    }

    public async Task<(List<FamilyUnitAdminProjection> Items, int TotalCount)> GetAllPagedAsync(
        int page, int pageSize, string? search, string? sortBy, string? sortOrder,
        string? membershipStatus, bool? isActive, CancellationToken ct)
    {
        var query = from fu in db.FamilyUnits
                    join user in db.Users on fu.RepresentativeUserId equals user.Id into userGroup
                    from u in userGroup.DefaultIfEmpty()
                    select new
                    {
                        fu.Id,
                        fu.Name,
                        fu.RepresentativeUserId,
                        RepresentativeName = u != null
                            ? u.FirstName + " " + u.LastName
                            : string.Empty,
                        fu.FamilyNumber,
                        fu.IsActive,
                        MembersCount = db.FamilyMembers.Count(m => m.FamilyUnitId == fu.Id),
                        fu.CreatedAt,
                        fu.UpdatedAt
                    };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                x.RepresentativeName.ToLower().Contains(term));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        if (membershipStatus == "active")
        {
            query = query.Where(x =>
                db.Memberships.Any(m => m.IsActive &&
                    db.FamilyMembers.Any(fm => fm.FamilyUnitId == x.Id && fm.Id == m.FamilyMemberId)));
        }
        else if (membershipStatus == "none")
        {
            query = query.Where(x =>
                !db.Memberships.Any(m => m.IsActive &&
                    db.FamilyMembers.Any(fm => fm.FamilyUnitId == x.Id && fm.Id == m.FamilyMemberId)));
        }

        var totalCount = await query.CountAsync(ct);

        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("createdat", "desc") => query.OrderByDescending(x => x.CreatedAt),
            ("createdat", _)      => query.OrderBy(x => x.CreatedAt),
            ("name", "desc")      => query.OrderByDescending(x => x.Name),
            _                     => query.OrderBy(x => x.Name),
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var projections = items.Select(x => new FamilyUnitAdminProjection(
            x.Id, x.Name, x.RepresentativeUserId,
            x.RepresentativeName, x.FamilyNumber, x.IsActive, x.MembersCount, x.CreatedAt, x.UpdatedAt
        )).ToList();

        return (projections, totalCount);
    }

    // Admin operations

    public async Task<bool> HasRegistrationsAsync(Guid familyUnitId, CancellationToken ct)
    {
        return await db.Registrations
            .AnyAsync(r => r.FamilyUnitId == familyUnitId, ct);
    }

    public async Task ClearAllUserFamilyUnitLinksAsync(Guid familyUnitId, CancellationToken ct)
    {
        await db.Users
            .Where(u => u.FamilyUnitId == familyUnitId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.FamilyUnitId, (Guid?)null), ct);
    }

    public async Task UpdateFamilyUnitStatusAsync(Guid familyUnitId, bool isActive, CancellationToken ct)
    {
        await db.FamilyUnits
            .Where(fu => fu.Id == familyUnitId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(x => x.IsActive, isActive)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
    }

    public async Task<bool> MemberHasActiveRegistrationsAsync(Guid memberId, CancellationToken ct)
    {
        return await db.RegistrationMembers
            .AnyAsync(rm => rm.FamilyMemberId == memberId
                && (rm.Registration.Status == Registrations.RegistrationStatus.Pending
                    || rm.Registration.Status == Registrations.RegistrationStatus.Confirmed), ct);
    }
}

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
    Task UpdateUserFamilyUnitIdAsync(Guid userId, Guid? familyUnitId, CancellationToken ct);
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

    public async Task UpdateUserFamilyUnitIdAsync(Guid userId, Guid? familyUnitId, CancellationToken ct)
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FamilyUnitId, familyUnitId)
                .SetProperty(u => u.UpdatedAt, DateTime.UtcNow), ct);
    }
}

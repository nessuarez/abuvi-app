using Abuvi.API.Data;
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public interface ICampEditionAttendanceRepository
{
    Task<CampEditionAttendance?> GetAsync(
        Guid editionId, Guid userId, Guid? familyMemberId, CancellationToken ct);

    Task<IReadOnlyList<CampEditionAttendance>> GetDeclaredForEditionAsync(
        Guid editionId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetDeclaredEditionIdsForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Editions this user attended according to their family's registrations. Read-only —
    /// derived attendance is never written to camp_edition_attendances.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetRegisteredEditionIdsForUserAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<Registration>> GetRegistrationsForEditionAsync(
        Guid editionId, CancellationToken ct);

    Task AddAsync(CampEditionAttendance attendance, CancellationToken ct);
    Task DeleteAsync(CampEditionAttendance attendance, CancellationToken ct);
}

public class CampEditionAttendanceRepository(AbuviDbContext db) : ICampEditionAttendanceRepository
{
    public async Task<CampEditionAttendance?> GetAsync(
        Guid editionId, Guid userId, Guid? familyMemberId, CancellationToken ct)
        => await db.CampEditionAttendances
            .FirstOrDefaultAsync(
                a => a.CampEditionId == editionId
                  && a.UserId == userId
                  && a.FamilyMemberId == familyMemberId,
                ct);

    public async Task<IReadOnlyList<CampEditionAttendance>> GetDeclaredForEditionAsync(
        Guid editionId, CancellationToken ct)
        => await db.CampEditionAttendances
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.FamilyMember)
            .Where(a => a.CampEditionId == editionId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetDeclaredEditionIdsForUserAsync(
        Guid userId, CancellationToken ct)
        => await db.CampEditionAttendances
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.CampEditionId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetRegisteredEditionIdsForUserAsync(
        Guid userId, CancellationToken ct)
    {
        var familyUnitId = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FamilyUnitId)
            .FirstOrDefaultAsync(ct);

        if (familyUnitId is null) return [];

        return await db.Registrations
            .AsNoTracking()
            .Where(r => r.FamilyUnitId == familyUnitId.Value)
            .Select(r => r.CampEditionId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Registration>> GetRegistrationsForEditionAsync(
        Guid editionId, CancellationToken ct)
        => await db.Registrations
            .AsNoTracking()
            .Include(r => r.FamilyUnit)
            .Where(r => r.CampEditionId == editionId)
            .ToListAsync(ct);

    public async Task AddAsync(CampEditionAttendance attendance, CancellationToken ct)
    {
        db.CampEditionAttendances.Add(attendance);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CampEditionAttendance attendance, CancellationToken ct)
    {
        db.CampEditionAttendances.Remove(attendance);
        await db.SaveChangesAsync(ct);
    }
}

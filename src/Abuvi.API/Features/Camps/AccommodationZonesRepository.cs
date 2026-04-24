using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class AccommodationZonesRepository(AbuviDbContext db) : IAccommodationZonesRepository
{
    public async Task<AccommodationZone?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.AccommodationZones
            .AsNoTracking()
            .Include(z => z.Accommodations)
            .FirstOrDefaultAsync(z => z.Id == id, ct);

    public async Task<List<AccommodationZone>> GetByCampEditionAsync(
        Guid campEditionId,
        CancellationToken ct = default)
        => await db.AccommodationZones
            .AsNoTracking()
            .Where(z => z.CampEditionId == campEditionId && z.IsActive)
            .Include(z => z.Accommodations)
            .OrderBy(z => z.AccommodationType.ToString())
            .ThenBy(z => z.SortOrder)
            .ThenBy(z => z.Name)
            .ToListAsync(ct);

    public async Task AddAsync(AccommodationZone zone, CancellationToken ct = default)
    {
        db.AccommodationZones.Add(zone);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AccommodationZone zone, CancellationToken ct = default)
    {
        db.AccommodationZones.Update(zone);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var zone = await db.AccommodationZones.FindAsync([id], ct);
        if (zone is not null)
        {
            db.AccommodationZones.Remove(zone);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> HasActiveAssignmentsAsync(Guid zoneId, CancellationToken ct = default)
        => await db.AccommodationAssignments
            .AnyAsync(a => a.Accommodation.ZoneId == zoneId, ct);

    public async Task AttachAccommodationsAsync(
        Guid zoneId,
        IReadOnlyList<Guid> accommodationIds,
        CancellationToken ct = default)
    {
        // Clear existing attachments for this zone
        var currentlyAttached = await db.CampEditionAccommodations
            .Where(a => a.ZoneId == zoneId)
            .ToListAsync(ct);

        foreach (var acc in currentlyAttached)
            acc.ZoneId = null;

        // Set the new ones
        var newOnes = await db.CampEditionAccommodations
            .Where(a => accommodationIds.Contains(a.Id))
            .ToListAsync(ct);

        foreach (var acc in newOnes)
            acc.ZoneId = zoneId;

        await db.SaveChangesAsync(ct);
    }
}

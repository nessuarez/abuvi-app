using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class AccommodationFeaturesRepository(AbuviDbContext db) : IAccommodationFeaturesRepository
{
    public async Task<IReadOnlyList<AccommodationFeature>> GetAllAsync(bool? activeOnly, CancellationToken ct)
    {
        var query = db.AccommodationFeatures.AsNoTracking();
        if (activeOnly == true)
            query = query.Where(f => f.IsActive);
        return await query.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToListAsync(ct);
    }

    public async Task<AccommodationFeature?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.AccommodationFeatures.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<AccommodationFeature?> GetByNameAsync(string name, CancellationToken ct)
        => await db.AccommodationFeatures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Name.ToLower() == name.ToLower(), ct);

    public async Task<AccommodationFeature> AddAsync(AccommodationFeature feature, CancellationToken ct)
    {
        feature.CreatedAt = DateTime.UtcNow;
        feature.UpdatedAt = DateTime.UtcNow;
        db.AccommodationFeatures.Add(feature);
        await db.SaveChangesAsync(ct);
        return feature;
    }

    public async Task<AccommodationFeature> UpdateAsync(AccommodationFeature feature, CancellationToken ct)
    {
        feature.UpdatedAt = DateTime.UtcNow;
        db.AccommodationFeatures.Update(feature);
        await db.SaveChangesAsync(ct);
        return feature;
    }

    public async Task DeleteAsync(AccommodationFeature feature, CancellationToken ct)
    {
        db.AccommodationFeatures.Remove(feature);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> HasAssignmentsAsync(Guid featureId, CancellationToken ct)
        => await db.AccommodationFeatureAssignments.AnyAsync(a => a.FeatureId == featureId, ct)
           || await db.ZoneFeatureAssignments.AnyAsync(a => a.FeatureId == featureId, ct);

    public async Task<IReadOnlyList<AccommodationFeature>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.ToList();
        return await db.AccommodationFeatures.AsNoTracking()
            .Where(f => idList.Contains(f.Id))
            .ToListAsync(ct);
    }

    public async Task SetAccommodationAssignmentsAsync(
        Guid accommodationId, IEnumerable<Guid> featureIds, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.AccommodationFeatureAssignments.RemoveRange(
            db.AccommodationFeatureAssignments.Where(a => a.AccommodationId == accommodationId));

        db.AccommodationFeatureAssignments.AddRange(featureIds.Select(fId =>
            new AccommodationFeatureAssignment
            {
                AccommodationId = accommodationId,
                FeatureId = fId,
                CreatedAt = DateTime.UtcNow
            }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AccommodationFeature>> GetForAccommodationAsync(
        Guid accommodationId, CancellationToken ct)
        => await db.AccommodationFeatureAssignments.AsNoTracking()
            .Where(a => a.AccommodationId == accommodationId)
            .Select(a => a.Feature)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(ct);

    public async Task SetZoneAssignmentsAsync(
        Guid zoneId, IEnumerable<Guid> featureIds, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ZoneFeatureAssignments.RemoveRange(
            db.ZoneFeatureAssignments.Where(a => a.ZoneId == zoneId));

        db.ZoneFeatureAssignments.AddRange(featureIds.Select(fId =>
            new ZoneFeatureAssignment
            {
                ZoneId = zoneId,
                FeatureId = fId,
                CreatedAt = DateTime.UtcNow
            }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AccommodationFeature>> GetForZoneAsync(
        Guid zoneId, CancellationToken ct)
        => await db.ZoneFeatureAssignments.AsNoTracking()
            .Where(a => a.ZoneId == zoneId)
            .Select(a => a.Feature)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(ct);
}

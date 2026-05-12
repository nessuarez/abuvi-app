using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public interface IAccommodationTypeMediaRepository
{
    Task<IReadOnlyList<AccommodationTypeMedia>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<AccommodationTypeMedia>> GetByTypeAsync(AccommodationType type, CancellationToken ct);
    Task<AccommodationTypeMedia?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<int> CountByTypeAsync(AccommodationType type, CancellationToken ct);
    Task ClearPrimaryForTypeAsync(AccommodationType type, CancellationToken ct);
    Task AddAsync(AccommodationTypeMedia item, CancellationToken ct);
    Task UpdateAsync(AccommodationTypeMedia item, CancellationToken ct);
    Task DeleteAsync(AccommodationTypeMedia item, CancellationToken ct);
}

public class AccommodationTypeMediaRepository(AbuviDbContext db) : IAccommodationTypeMediaRepository
{
    public async Task<IReadOnlyList<AccommodationTypeMedia>> GetAllAsync(CancellationToken ct)
    {
        return await db.AccommodationTypeMedia
            .AsNoTracking()
            .OrderBy(m => m.AccommodationType)
            .ThenBy(m => m.DisplayOrder)
            .ThenBy(m => m.IsPrimary ? 0 : 1)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccommodationTypeMedia>> GetByTypeAsync(AccommodationType type, CancellationToken ct)
    {
        return await db.AccommodationTypeMedia
            .AsNoTracking()
            .Where(m => m.AccommodationType == type)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.IsPrimary ? 0 : 1)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<AccommodationTypeMedia?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await db.AccommodationTypeMedia
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<int> CountByTypeAsync(AccommodationType type, CancellationToken ct)
    {
        return await db.AccommodationTypeMedia
            .CountAsync(m => m.AccommodationType == type, ct);
    }

    public async Task ClearPrimaryForTypeAsync(AccommodationType type, CancellationToken ct)
    {
        await db.AccommodationTypeMedia
            .Where(m => m.AccommodationType == type && m.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsPrimary, false), ct);
    }

    public async Task AddAsync(AccommodationTypeMedia item, CancellationToken ct)
    {
        db.AccommodationTypeMedia.Add(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AccommodationTypeMedia item, CancellationToken ct)
    {
        db.AccommodationTypeMedia.Update(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(AccommodationTypeMedia item, CancellationToken ct)
    {
        db.AccommodationTypeMedia.Remove(item);
        await db.SaveChangesAsync(ct);
    }
}

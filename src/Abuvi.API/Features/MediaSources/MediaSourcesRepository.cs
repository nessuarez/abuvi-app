using Abuvi.API.Data;
using Abuvi.API.Features.MediaItems;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.MediaSources;

public interface IMediaSourcesRepository
{
    Task<MediaSource?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MediaSource>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Aggregates for many sources in ONE grouped query. Never call this per source —
    /// the contributor list would become N+1 the moment the archive grows.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, MediaSourceStats>> GetStatsAsync(
        IReadOnlyList<Guid> sourceIds, CancellationToken ct);

    Task<(IReadOnlyList<MediaItem> Items, int Total)> GetItemsAsync(
        Guid sourceId, int page, int pageSize, CancellationToken ct);

    Task AddAsync(MediaSource source, CancellationToken ct);
    Task UpdateAsync(MediaSource source, CancellationToken ct);

    /// <summary>Repoints every item from one source to another. Returns rows moved.</summary>
    Task<int> RepointItemsAsync(Guid fromSourceId, Guid toSourceId, CancellationToken ct);

    Task DeleteAsync(MediaSource source, CancellationToken ct);
}

public class MediaSourcesRepository(AbuviDbContext db) : IMediaSourcesRepository
{
    public async Task<MediaSource?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.MediaSources
            .Include(s => s.RegisteredBy)
            .Include(s => s.ContributorUser)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<MediaSource>> GetAllAsync(CancellationToken ct)
        => await db.MediaSources
            .AsNoTracking()
            .Include(s => s.RegisteredBy)
            .OrderBy(s => s.ContributorName)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, MediaSourceStats>> GetStatsAsync(
        IReadOnlyList<Guid> sourceIds, CancellationToken ct)
    {
        if (sourceIds.Count == 0)
            return new Dictionary<Guid, MediaSourceStats>();

        var rows = await db.MediaItems
            .AsNoTracking()
            .Where(m => m.MediaSourceId != null && sourceIds.Contains(m.MediaSourceId.Value))
            .GroupBy(m => m.MediaSourceId!.Value)
            .Select(g => new
            {
                SourceId = g.Key,
                ItemCount = g.Count(),
                UndatedItemCount = g.Count(m => m.CampEditionId == null),
                FirstYear = g.Min(m => m.Year),
                LastYear = g.Max(m => m.Year)
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.SourceId,
            r => new MediaSourceStats(r.ItemCount, r.UndatedItemCount, r.FirstYear, r.LastYear));
    }

    public async Task<(IReadOnlyList<MediaItem> Items, int Total)> GetItemsAsync(
        Guid sourceId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .Include(m => m.CampEdition)
            .Where(m => m.MediaSourceId == sourceId);

        var total = await query.CountAsync(ct);

        // Dated items first, newest year first; undated last — they are the ones
        // this contributor could still help place.
        var items = await query
            .OrderByDescending(m => m.Year == null ? 0 : 1)
            .ThenByDescending(m => m.Year)
            .ThenByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(MediaSource source, CancellationToken ct)
    {
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MediaSource source, CancellationToken ct)
    {
        db.MediaSources.Update(source);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> RepointItemsAsync(Guid fromSourceId, Guid toSourceId, CancellationToken ct)
        // ExecuteUpdateAsync so an 800-photo donation does not load into memory to be merged.
        => await db.MediaItems
            .Where(m => m.MediaSourceId == fromSourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.MediaSourceId, toSourceId), ct);

    public async Task DeleteAsync(MediaSource source, CancellationToken ct)
    {
        db.MediaSources.Remove(source);
        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;

namespace Abuvi.API.Features.MediaItems;

public interface IMediaItemsRepository
{
    Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MediaItem>> GetListAsync(int? year, bool? approved, string? context, MediaItemType? type, Guid? accommodationId, Guid? zoneId, Guid? campEditionId, bool unplacedOnly, Guid? themeId, CancellationToken ct);
    Task<IReadOnlyList<MediaItem>> GetByMemoryIdAsync(Guid memoryId, CancellationToken ct);
    Task<IReadOnlyList<MediaItem>> GetByAccommodationIdAsync(Guid accommodationId, CancellationToken ct);
    Task<IReadOnlyList<MediaItem>> GetByZoneIdAsync(Guid zoneId, CancellationToken ct);
    /// <summary>
    /// Rolls up published photos of a context by year: one count per year plus up to
    /// <paramref name="previewsPerYear"/> items to show. Fixed cost regardless of how
    /// many years are involved — callers must never query year by year.
    /// </summary>
    Task<IReadOnlyList<MediaItemYearSummary>> GetYearSummariesAsync(
        string context,
        int previewsPerYear,
        CancellationToken ct);
    /// <summary>
    /// Per-edition, per-type counts for EVERY edition in one grouped query. The album
    /// index must never issue a query per edition.
    /// </summary>
    Task<IReadOnlyList<AlbumCountRow>> GetAlbumCountsAsync(CancellationToken ct);

    /// <summary>
    /// One cover photo per edition: the primary if there is one, else the most recent
    /// approved photo. One query for all editions.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string?>> GetCoversAsync(CancellationToken ct);

    Task<(IReadOnlyList<MediaItem> Items, int Total)> GetAlbumPageAsync(
        Guid editionId, int page, int pageSize, MediaItemType? type, Guid? themeId,
        bool includeUnapproved, CancellationToken ct);

    Task<(IReadOnlyList<MediaItem> Items, int Total)> GetUnplacedPageAsync(
        int page, int pageSize, MediaItemType? type, Guid? mediaSourceId,
        IReadOnlyList<Guid>? suggestedEditionIds, Guid? contributedByUserId,
        bool includeUnapproved, CancellationToken ct);

    Task<int> CountByAccommodationAsync(Guid accommodationId, CancellationToken ct);
    Task<int> CountByZoneAsync(Guid zoneId, CancellationToken ct);
    Task ClearPrimaryForAccommodationAsync(Guid accommodationId, CancellationToken ct);
    Task ClearPrimaryForZoneAsync(Guid zoneId, CancellationToken ct);
    Task AddAsync(MediaItem mediaItem, CancellationToken ct);
    Task UpdateAsync(MediaItem mediaItem, CancellationToken ct);
    Task DeleteAsync(MediaItem mediaItem, CancellationToken ct);
}

public class MediaItemsRepository(AbuviDbContext db) : IMediaItemsRepository
{
    public async Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await db.MediaItems
            .Include(m => m.UploadedBy)
            .Include(m => m.Memory)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<MediaItem>> GetListAsync(
        int? year,
        bool? approved,
        string? context,
        MediaItemType? type,
        Guid? accommodationId,
        Guid? zoneId,
        Guid? campEditionId,
        bool unplacedOnly,
        Guid? themeId,
        CancellationToken ct)
    {
        var query = db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .AsQueryable();

        if (year.HasValue)
            query = query.Where(m => m.Year == year.Value);

        if (approved == true)
            query = query.Where(m => m.IsApproved && m.IsPublished);
        else if (approved == false)
            query = query.Where(m => !m.IsApproved);

        if (!string.IsNullOrEmpty(context))
            query = query.Where(m => m.Context == context);

        if (type.HasValue)
            query = query.Where(m => m.Type == type.Value);

        if (accommodationId.HasValue)
            query = query.Where(m => m.AccommodationId == accommodationId.Value);

        if (zoneId.HasValue)
            query = query.Where(m => m.ZoneId == zoneId.Value);

        if (campEditionId.HasValue)
            query = query.Where(m => m.CampEditionId == campEditionId.Value);

        if (unplacedOnly)
            query = query.Where(m => m.CampEditionId == null);

        if (themeId.HasValue)
            query = query.Where(m => m.Themes.Any(t => t.MediaThemeId == themeId.Value));

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaItem>> GetByMemoryIdAsync(Guid memoryId, CancellationToken ct)
    {
        return await db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .Where(m => m.MemoryId == memoryId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaItem>> GetByAccommodationIdAsync(Guid accommodationId, CancellationToken ct)
    {
        return await db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .Where(m => m.AccommodationId == accommodationId)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.IsPrimary ? 0 : 1)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaItem>> GetByZoneIdAsync(Guid zoneId, CancellationToken ct)
    {
        return await db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .Where(m => m.ZoneId == zoneId)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.IsPrimary ? 0 : 1)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaItemYearSummary>> GetYearSummariesAsync(
        string context,
        int previewsPerYear,
        CancellationToken ct)
    {
        var published = db.MediaItems
            .AsNoTracking()
            .Where(m => m.Context == context
                     && m.Type == MediaItemType.Photo
                     && m.IsApproved
                     && m.IsPublished
                     && m.Year != null);

        // One aggregate for the counts...
        var counts = await published
            .GroupBy(m => m.Year!.Value)
            .Select(g => new { Year = g.Key, PhotoCount = g.Count() })
            .ToListAsync(ct);

        // ...and one lateral join for the previews, capped per year. Two queries in
        // total, whether the archive spans one year or fifty.
        var previews = await published
            .Select(m => m.Year!.Value)
            .Distinct()
            .SelectMany(year => published
                .Where(m => m.Year == year)
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.DisplayOrder)
                .ThenByDescending(m => m.CreatedAt)
                .Take(previewsPerYear))
            .Select(m => new
            {
                Year = m.Year!.Value,
                m.Id,
                m.ThumbnailUrl,
                m.FileUrl,
                m.Title,
                m.IsPrimary,
                m.DisplayOrder,
                m.CreatedAt
            })
            .ToListAsync(ct);

        // The lateral join caps the rows but says nothing about the order they come
        // back in, so re-apply it here.
        var previewsByYear = previews
            .GroupBy(p => p.Year)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MediaItemPreview>)g
                    .OrderByDescending(p => p.IsPrimary)
                    .ThenBy(p => p.DisplayOrder)
                    .ThenByDescending(p => p.CreatedAt)
                    .Select(p => new MediaItemPreview(p.Id, p.ThumbnailUrl, p.FileUrl, p.Title))
                    .ToList());

        return counts
            .Select(c => new MediaItemYearSummary(
                c.Year,
                c.PhotoCount,
                previewsByYear.TryGetValue(c.Year, out var p) ? p : []))
            .ToList();
    }

    public async Task<IReadOnlyList<AlbumCountRow>> GetAlbumCountsAsync(CancellationToken ct)
    {
        var rows = await db.MediaItems
            .AsNoTracking()
            .Where(m => m.CampEditionId != null && m.IsApproved && m.IsPublished)
            .GroupBy(m => new { EditionId = m.CampEditionId!.Value, m.Type })
            .Select(g => new { g.Key.EditionId, g.Key.Type, Count = g.Count() })
            .ToListAsync(ct);

        return rows.Select(r => new AlbumCountRow(r.EditionId, r.Type, r.Count)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> GetCoversAsync(CancellationToken ct)
    {
        // One row per edition, chosen by the same ordering the album grid would use.
        var covers = await db.MediaItems
            .AsNoTracking()
            .Where(m => m.CampEditionId != null
                     && m.Type == MediaItemType.Photo
                     && m.IsApproved && m.IsPublished)
            .GroupBy(m => m.CampEditionId!.Value)
            .Select(g => new
            {
                EditionId = g.Key,
                Thumbnail = g
                    .OrderByDescending(m => m.IsPrimary)
                    .ThenBy(m => m.DisplayOrder)
                    .ThenByDescending(m => m.CreatedAt)
                    .Select(m => m.ThumbnailUrl ?? m.FileUrl)
                    .First()
            })
            .ToListAsync(ct);

        return covers.ToDictionary(c => c.EditionId, c => (string?)c.Thumbnail);
    }

    public async Task<(IReadOnlyList<MediaItem> Items, int Total)> GetAlbumPageAsync(
        Guid editionId, int page, int pageSize, MediaItemType? type, Guid? themeId,
        bool includeUnapproved, CancellationToken ct)
    {
        var query = db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .Include(m => m.MediaSource)
            .Where(m => m.CampEditionId == editionId);

        if (!includeUnapproved)
            query = query.Where(m => m.IsApproved && m.IsPublished);

        if (type.HasValue) query = query.Where(m => m.Type == type.Value);
        if (themeId.HasValue) query = query.Where(m => m.Themes.Any(t => t.MediaThemeId == themeId.Value));

        var total = await query.CountAsync(ct);

        // All types interleaved by default — the frontend groups them for display, the
        // API does not pre-segment.
        var items = await query
            .OrderBy(m => m.DisplayOrder)
            .ThenByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<MediaItem> Items, int Total)> GetUnplacedPageAsync(
        int page, int pageSize, MediaItemType? type, Guid? mediaSourceId,
        IReadOnlyList<Guid>? suggestedEditionIds, Guid? contributedByUserId,
        bool includeUnapproved, CancellationToken ct)
    {
        var query = db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .Include(m => m.MediaSource)
            .Where(m => m.CampEditionId == null);

        if (!includeUnapproved)
            query = query.Where(m => m.IsApproved && m.IsPublished);

        if (type.HasValue) query = query.Where(m => m.Type == type.Value);
        if (mediaSourceId.HasValue) query = query.Where(m => m.MediaSourceId == mediaSourceId.Value);

        // "Suggested for me": items this member is plausibly able to date — anything they
        // contributed themselves, plus anything whose year points at an edition they
        // attended. Provenance answers "who should we ask" more directly than attendance.
        if (contributedByUserId is { } userId && suggestedEditionIds is { Count: > 0 })
        {
            var years = await db.CampEditions
                .AsNoTracking()
                .Where(e => suggestedEditionIds.Contains(e.Id))
                .Select(e => e.Year)
                .ToListAsync(ct);

            query = query.Where(m =>
                m.UploadedByUserId == userId
                || (m.MediaSource != null && m.MediaSource.ContributorUserId == userId)
                || (m.Year != null && years.Contains(m.Year.Value)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> CountByAccommodationAsync(Guid accommodationId, CancellationToken ct)
    {
        return await db.MediaItems
            .CountAsync(m => m.AccommodationId == accommodationId, ct);
    }

    public async Task<int> CountByZoneAsync(Guid zoneId, CancellationToken ct)
    {
        return await db.MediaItems
            .CountAsync(m => m.ZoneId == zoneId, ct);
    }

    public async Task ClearPrimaryForAccommodationAsync(Guid accommodationId, CancellationToken ct)
    {
        await db.MediaItems
            .Where(m => m.AccommodationId == accommodationId && m.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsPrimary, false), ct);
    }

    public async Task ClearPrimaryForZoneAsync(Guid zoneId, CancellationToken ct)
    {
        await db.MediaItems
            .Where(m => m.ZoneId == zoneId && m.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsPrimary, false), ct);
    }

    public async Task AddAsync(MediaItem mediaItem, CancellationToken ct)
    {
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MediaItem mediaItem, CancellationToken ct)
    {
        db.MediaItems.Update(mediaItem);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MediaItem mediaItem, CancellationToken ct)
    {
        db.MediaItems.Remove(mediaItem);
        await db.SaveChangesAsync(ct);
    }
}

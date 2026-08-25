using Abuvi.API.Data;
using Abuvi.API.Features.MediaItems;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.MediaThemes;

public interface IMediaThemesRepository
{
    Task<MediaTheme?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<MediaTheme?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
    Task<IReadOnlyList<MediaTheme>> GetAllAsync(bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<MediaTheme>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);

    /// <summary>Aggregates for every theme in ONE grouped query, never one per theme.</summary>
    Task<IReadOnlyDictionary<Guid, ThemeStats>> GetStatsAsync(CancellationToken ct);

    /// <summary>
    /// Every tag for a page of items in one query. This is the N+1 killer for album and
    /// theme grids — resolve the whole page, then map in memory.
    /// </summary>
    Task<IReadOnlyList<MediaItemTheme>> GetThemesForItemsAsync(
        IReadOnlyList<Guid> mediaItemIds, CancellationToken ct);

    Task<(IReadOnlyList<MediaItem> Items, int Total)> GetItemsForThemeAsync(
        Guid themeId, int page, int pageSize, int? year, Guid? campEditionId,
        bool undatedOnly, MediaItemType? type, bool includeUnapproved, CancellationToken ct);

    Task<int> CountItemsAsync(Guid themeId, CancellationToken ct);
    Task<bool> IsAttachedAsync(Guid mediaItemId, Guid themeId, CancellationToken ct);
    Task AttachAsync(MediaItemTheme tag, CancellationToken ct);
    Task AttachManyAsync(IReadOnlyList<MediaItemTheme> tags, CancellationToken ct);
    Task<MediaItemTheme?> GetTagAsync(Guid mediaItemId, Guid themeId, CancellationToken ct);
    Task DetachAsync(MediaItemTheme tag, CancellationToken ct);
    Task AddAsync(MediaTheme theme, CancellationToken ct);
    Task UpdateAsync(MediaTheme theme, CancellationToken ct);
    Task DeleteAsync(MediaTheme theme, CancellationToken ct);
}

public class MediaThemesRepository(AbuviDbContext db) : IMediaThemesRepository
{
    public async Task<MediaTheme?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.MediaThemes.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<MediaTheme?> GetBySlugAsync(string slug, CancellationToken ct)
        => await db.MediaThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
        => await db.MediaThemes.AnyAsync(t => t.Slug == slug, ct);

    public async Task<IReadOnlyList<MediaTheme>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.MediaThemes.AsNoTracking();
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaTheme>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
        => ids.Count == 0
            ? []
            : await db.MediaThemes.AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, ThemeStats>> GetStatsAsync(CancellationToken ct)
    {
        var rows = await db.MediaItemThemes
            .AsNoTracking()
            .GroupBy(t => t.MediaThemeId)
            .Select(g => new
            {
                ThemeId = g.Key,
                ItemCount = g.Count(),
                FirstYear = g.Min(t => t.MediaItem.Year),
                LastYear = g.Max(t => t.MediaItem.Year),
                UndatedCount = g.Count(t => t.MediaItem.CampEditionId == null)
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.ThemeId,
            r => new ThemeStats(r.ItemCount, r.FirstYear, r.LastYear, r.UndatedCount));
    }

    public async Task<IReadOnlyList<MediaItemTheme>> GetThemesForItemsAsync(
        IReadOnlyList<Guid> mediaItemIds, CancellationToken ct)
        => mediaItemIds.Count == 0
            ? []
            : await db.MediaItemThemes
                .AsNoTracking()
                .Include(t => t.MediaTheme)
                .Where(t => mediaItemIds.Contains(t.MediaItemId))
                .ToListAsync(ct);

    public async Task<(IReadOnlyList<MediaItem> Items, int Total)> GetItemsForThemeAsync(
        Guid themeId, int page, int pageSize, int? year, Guid? campEditionId,
        bool undatedOnly, MediaItemType? type, bool includeUnapproved, CancellationToken ct)
    {
        var query = db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .Include(m => m.MediaSource)
            .Where(m => m.Themes.Any(t => t.MediaThemeId == themeId));

        if (!includeUnapproved)
            query = query.Where(m => m.IsApproved && m.IsPublished);

        if (year.HasValue) query = query.Where(m => m.Year == year.Value);
        if (campEditionId.HasValue) query = query.Where(m => m.CampEditionId == campEditionId.Value);
        if (undatedOnly) query = query.Where(m => m.CampEditionId == null);
        if (type.HasValue) query = query.Where(m => m.Type == type.Value);

        var total = await query.CountAsync(ct);

        // Newest year first, undated last — the undated ones are the invitation to help.
        var items = await query
            .OrderByDescending(m => m.Year == null ? 0 : 1)
            .ThenByDescending(m => m.Year)
            .ThenByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> CountItemsAsync(Guid themeId, CancellationToken ct)
        => await db.MediaItemThemes.CountAsync(t => t.MediaThemeId == themeId, ct);

    public async Task<bool> IsAttachedAsync(Guid mediaItemId, Guid themeId, CancellationToken ct)
        => await db.MediaItemThemes
            .AnyAsync(t => t.MediaItemId == mediaItemId && t.MediaThemeId == themeId, ct);

    public async Task AttachAsync(MediaItemTheme tag, CancellationToken ct)
    {
        db.MediaItemThemes.Add(tag);
        await db.SaveChangesAsync(ct);
    }

    public async Task AttachManyAsync(IReadOnlyList<MediaItemTheme> tags, CancellationToken ct)
    {
        if (tags.Count == 0) return;
        db.MediaItemThemes.AddRange(tags);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MediaItemTheme?> GetTagAsync(Guid mediaItemId, Guid themeId, CancellationToken ct)
        => await db.MediaItemThemes
            .FirstOrDefaultAsync(t => t.MediaItemId == mediaItemId && t.MediaThemeId == themeId, ct);

    public async Task DetachAsync(MediaItemTheme tag, CancellationToken ct)
    {
        db.MediaItemThemes.Remove(tag);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddAsync(MediaTheme theme, CancellationToken ct)
    {
        db.MediaThemes.Add(theme);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MediaTheme theme, CancellationToken ct)
    {
        db.MediaThemes.Update(theme);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MediaTheme theme, CancellationToken ct)
    {
        db.MediaThemes.Remove(theme);
        await db.SaveChangesAsync(ct);
    }
}

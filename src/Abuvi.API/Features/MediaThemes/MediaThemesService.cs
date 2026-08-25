using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.MediaSources;

namespace Abuvi.API.Features.MediaThemes;

public partial class MediaThemesService(
    IMediaThemesRepository repository,
    IMediaItemsRepository mediaItemsRepository,
    ILogger<MediaThemesService> logger)
{
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    /// <summary>
    /// Turns a display name into a URL slug: lowercase, accents stripped, everything else
    /// collapsed to dashes. "San Abuvino" becomes "san-abuvino".
    /// </summary>
    public static string Slugify(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var slug = NonSlugChars()
            .Replace(sb.ToString().ToLowerInvariant(), "-")
            .Trim('-');

        return slug.Length == 0 ? "tema" : slug;
    }

    private async Task<string> BuildUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slugify(name);
        var slug = baseSlug;
        var suffix = 2;

        while (await repository.SlugExistsAsync(slug, ct))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    public async Task<IReadOnlyList<MediaThemeSummaryResponse>> GetCatalogueAsync(
        bool includeInactive, CancellationToken ct)
    {
        var themes = await repository.GetAllAsync(includeInactive, ct);
        var stats = await repository.GetStatsAsync(ct);

        return themes
            .Select(t => t.ToResponse(stats.TryGetValue(t.Id, out var s) ? s : ThemeStats.Empty))
            .ToList();
    }

    public async Task<ThemeItemsResponse> GetItemsBySlugAsync(
        string slug, int page, int pageSize, int? year, Guid? campEditionId,
        bool undatedOnly, MediaItemType? type, bool isAdminOrBoard, CancellationToken ct)
    {
        var theme = await repository.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("tema", slug);

        var (items, total) = await repository.GetItemsForThemeAsync(
            theme.Id, page, pageSize, year, campEditionId, undatedOnly, type,
            includeUnapproved: isAdminOrBoard, ct);

        var themesByItem = await LoadThemeRefsAsync(items, ct);
        var stats = await repository.GetStatsAsync(ct);

        var mapped = items
            .Select(m => m.ToAlbumResponse(
                MediaSourcesService.TrimSourcePath(m.SourcePath, isAdminOrBoard),
                themesByItem.TryGetValue(m.Id, out var refs) ? refs : []))
            .ToList();

        return new ThemeItemsResponse(
            theme.ToResponse(stats.TryGetValue(theme.Id, out var s) ? s : ThemeStats.Empty),
            mapped, total, page, pageSize);
    }

    /// <summary>
    /// Resolves theme references for a whole page of items in one query. Callers building
    /// any grid must use this rather than touching item.Themes per row.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MediaThemeRef>>> LoadThemeRefsAsync(
        IReadOnlyList<MediaItem> items, CancellationToken ct)
    {
        if (items.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<MediaThemeRef>>();

        var tags = await repository.GetThemesForItemsAsync(items.Select(i => i.Id).ToList(), ct);
        return MediaThemeMappingExtensions.GroupRefsByItem(tags);
    }

    public async Task<MediaThemeSummaryResponse> CreateAsync(
        CreateMediaThemeRequest request, CancellationToken ct)
    {
        var theme = new MediaTheme
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = await BuildUniqueSlugAsync(request.Name, ct),
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(theme, ct);
        logger.LogInformation("MediaTheme {ThemeId} '{Slug}' created", theme.Id, theme.Slug);

        return theme.ToResponse(ThemeStats.Empty);
    }

    public async Task<MediaThemeSummaryResponse> UpdateAsync(
        Guid id, UpdateMediaThemeRequest request, CancellationToken ct)
    {
        var theme = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("tema", id);

        // The slug is the URL identity: renaming a theme must not break links that
        // already exist, so it is deliberately left alone.
        theme.Name = request.Name.Trim();
        theme.Description = request.Description;
        theme.IsActive = request.IsActive;
        theme.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(theme, ct);

        var stats = await repository.GetStatsAsync(ct);
        return theme.ToResponse(stats.TryGetValue(id, out var s) ? s : ThemeStats.Empty);
    }

    public async Task DeleteAsync(Guid id, bool force, CancellationToken ct)
    {
        var theme = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("tema", id);

        var count = await repository.CountItemsAsync(id, ct);
        if (count > 0 && !force)
            throw new BusinessRuleException(
                $"El tema tiene {count} elemento(s) asociado(s). Desactívalo o usa force=true para eliminarlo.");

        await repository.DeleteAsync(theme, ct);
        logger.LogInformation("MediaTheme {ThemeId} deleted ({ItemCount} tag(s) removed)", id, count);
    }

    /// <summary>
    /// Attaches a theme to an item. Idempotent — the composite primary key means a repeat
    /// is a no-op rather than a duplicate.
    /// </summary>
    public async Task AttachAsync(Guid mediaItemId, Guid themeId, Guid userId, CancellationToken ct)
    {
        _ = await mediaItemsRepository.GetByIdAsync(mediaItemId, ct)
            ?? throw new NotFoundException("elemento multimedia", mediaItemId);
        _ = await repository.GetByIdAsync(themeId, ct)
            ?? throw new NotFoundException("tema", themeId);

        if (await repository.IsAttachedAsync(mediaItemId, themeId, ct))
            return;

        await repository.AttachAsync(new MediaItemTheme
        {
            MediaItemId = mediaItemId,
            MediaThemeId = themeId,
            TaggedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        }, ct);
    }

    /// <summary>
    /// Attaches several themes during upload. Unknown or inactive ids are skipped rather
    /// than failing the upload — losing a photo because a theme id went stale would be a
    /// bad trade.
    /// </summary>
    public async Task AttachManyIgnoringUnknownAsync(
        Guid mediaItemId, IReadOnlyList<Guid> themeIds, Guid userId, CancellationToken ct)
    {
        if (themeIds.Count == 0) return;

        var known = await repository.GetByIdsAsync(themeIds.Distinct().ToList(), ct);

        var tags = known
            .Where(t => t.IsActive)
            .Select(t => new MediaItemTheme
            {
                MediaItemId = mediaItemId,
                MediaThemeId = t.Id,
                TaggedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        await repository.AttachManyAsync(tags, ct);

        var skipped = themeIds.Distinct().Count() - tags.Count;
        if (skipped > 0)
            logger.LogInformation(
                "Skipped {SkippedCount} unknown or inactive theme id(s) while tagging MediaItem {MediaItemId}",
                skipped, mediaItemId);
    }

    /// <summary>Returns false when the caller may not detach, so the endpoint can answer 403.</summary>
    public async Task<bool> DetachAsync(
        Guid mediaItemId, Guid themeId, Guid callerUserId, bool isAdminOrBoard, CancellationToken ct)
    {
        var tag = await repository.GetTagAsync(mediaItemId, themeId, ct)
            ?? throw new NotFoundException("etiqueta de tema", themeId);

        if (!isAdminOrBoard && tag.TaggedByUserId != callerUserId)
            return false;

        await repository.DetachAsync(tag, ct);
        return true;
    }
}

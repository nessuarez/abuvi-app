using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.MediaThemes;

/// <summary>
/// A recurring subject that spans many camp editions — "San Abuvino", "Actuaciones",
/// "Asambleas".
///
/// Themes are a cross-cutting tag dimension, not a rival container to the edition album:
/// a photo is edition 1998 AND San Abuvino at the same time. An item with no edition can
/// still carry themes, and those themes then become a dating clue.
/// </summary>
public class MediaTheme
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Kebab-case, unique. Used in URLs.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Soft retirement — deactivating keeps existing tags intact.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public List<MediaItemTheme> Items { get; set; } = [];
}

/// <summary>
/// N:M join between media and themes. The composite primary key
/// (MediaItemId, MediaThemeId) makes duplicate tagging impossible at the database level.
/// </summary>
public class MediaItemTheme
{
    public Guid MediaItemId { get; set; }
    public Guid MediaThemeId { get; set; }
    public Guid TaggedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public MediaItem MediaItem { get; set; } = null!;
    public MediaTheme MediaTheme { get; set; } = null!;
    public User TaggedBy { get; set; } = null!;
}

// ──────────────────────────────────────────────────────
// Request DTOs
// ──────────────────────────────────────────────────────

public record CreateMediaThemeRequest(string Name, string? Description);

public record UpdateMediaThemeRequest(string Name, string? Description, bool IsActive);

public record AttachThemeRequest(Guid ThemeId);

// ──────────────────────────────────────────────────────
// Response DTOs
// ──────────────────────────────────────────────────────

public record MediaThemeSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    int ItemCount,
    /// <summary>Earliest dated item carrying this theme.</summary>
    int? FirstYear,
    /// <summary>Latest — this pair is what makes "spans many years" visible in the UI.</summary>
    int? LastYear,
    /// <summary>Items with this theme still waiting for an edition.</summary>
    int UndatedCount);

public record ThemeItemsResponse(
    MediaThemeSummaryResponse Theme,
    IReadOnlyList<AlbumMediaItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Aggregates for one theme, computed in a single grouped query.</summary>
public record ThemeStats(int ItemCount, int? FirstYear, int? LastYear, int UndatedCount)
{
    public static readonly ThemeStats Empty = new(0, null, null, 0);
}

// ──────────────────────────────────────────────────────
// Mapping
// ──────────────────────────────────────────────────────

public static class MediaThemeMappingExtensions
{
    public static MediaThemeSummaryResponse ToResponse(this MediaTheme theme, ThemeStats stats) =>
        new(
            theme.Id,
            theme.Name,
            theme.Slug,
            theme.Description,
            theme.IsActive,
            stats.ItemCount,
            stats.FirstYear,
            stats.LastYear,
            stats.UndatedCount);

    public static MediaThemeRef ToRef(this MediaTheme theme) =>
        new(theme.Id, theme.Name, theme.Slug);

    /// <summary>
    /// Groups a flat batch of tags by media item. Pair with
    /// IMediaThemesRepository.GetThemesForItemsAsync so a whole grid resolves its themes
    /// in one query instead of one per row.
    /// </summary>
    public static IReadOnlyDictionary<Guid, IReadOnlyList<MediaThemeRef>> GroupRefsByItem(
        IReadOnlyList<MediaItemTheme> tags) =>
        tags
            .GroupBy(t => t.MediaItemId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MediaThemeRef>)g
                    .Select(t => t.MediaTheme.ToRef())
                    .OrderBy(r => r.Name)
                    .ToList());
}

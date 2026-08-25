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

using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaSources;
using Abuvi.API.Features.MediaThemes;
using Abuvi.API.Features.Memories;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.MediaItems;

public enum MediaItemType
{
    Photo,
    Video,
    Interview,
    Document,
    Audio
}

/// <summary>
/// How a media item's year was established. Admin always wins and is never
/// overwritten by community consensus.
/// </summary>
public enum MediaItemYearSource
{
    Unknown,    // no year yet — eligible for collaborative dating
    Exif,       // EXIF DateTimeOriginal
    FolderName, // resolved from the import folder name
    Uploader,   // typed into the web upload form
    Community,  // set by collaborative dating consensus
    Admin       // set manually by Admin/Board
}

public class MediaItem
{
    public Guid Id { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public MediaItemType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Year { get; set; }
    public string? Decade { get; set; }
    public Guid? MemoryId { get; set; }
    public Guid? CampLocationId { get; set; } // TODO: Add FK relationship when CampLocation entity is created
    public Guid? AccommodationId { get; set; }
    public Guid? ZoneId { get; set; }
    public bool IsPublished { get; set; }
    public bool IsApproved { get; set; }
    public string? Context { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// The camp edition this item belongs to. Null means "we do not know which edition yet" —
    /// always a temporary state, always resolvable by collaborative dating. All ABUVI media
    /// belongs to some camp; there is deliberately no "not camp related" state.
    /// </summary>
    public Guid? CampEditionId { get; set; }

    public MediaItemYearSource YearSource { get; set; } = MediaItemYearSource.Unknown;

    /// <summary>Denormalised counter so album grids never join comments.</summary>
    public int CommentCount { get; set; }

    /// <summary>Who provided the material. Null means the uploader is also the provider.</summary>
    public Guid? MediaSourceId { get; set; }

    /// <summary>
    /// Original folder path the file came from, relative to the import root. A dating clue:
    /// a human may recognise "Verano con los Martínez" where the resolver sees nothing.
    /// Members only ever see the trailing segments — see MediaSourcesService.TrimSourcePath.
    /// </summary>
    public string? SourcePath { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User UploadedBy { get; set; } = null!;
    public Memory? Memory { get; set; }
    public CampEditionAccommodation? Accommodation { get; set; }
    public AccommodationZone? Zone { get; set; }
    public CampEdition? CampEdition { get; set; }
    public MediaSource? MediaSource { get; set; }
    public List<MediaItemTheme> Themes { get; set; } = [];
    // TODO: Add CampLocation navigation when CampLocation entity is created
}

// Request DTOs
public record CreateMediaItemRequest(
    string FileUrl,
    string? ThumbnailUrl,
    MediaItemType Type,
    string Title,
    string? Description,
    int? Year,
    Guid? MemoryId,
    Guid? CampLocationId,
    string? Context,
    Guid? AccommodationId = null,
    Guid? ZoneId = null);

// Accommodation/Zone media — two-step upload (blob already uploaded)
public record AddAccommodationMediaRequest(
    string FileUrl,
    string? ThumbnailUrl,
    string? Description,
    int DisplayOrder = 0);

// Response DTOs
public record MediaItemResponse(
    Guid Id,
    Guid UploadedByUserId,
    string UploadedByName,
    string FileUrl,
    string? ThumbnailUrl,
    string Type,
    string Title,
    string? Description,
    int? Year,
    string? Decade,
    Guid? MemoryId,
    Guid? AccommodationId,
    Guid? ZoneId,
    string? Context,
    bool IsPublished,
    bool IsApproved,
    int DisplayOrder,
    bool IsPrimary,
    DateTime CreatedAt);

// Mapping extensions
public static class MediaItemMappingExtensions
{
    public static MediaItemResponse ToResponse(this MediaItem item) =>
        new(
            item.Id,
            item.UploadedByUserId,
            (item.UploadedBy?.FirstName + " " + item.UploadedBy?.LastName) ?? "Unknown",
            item.FileUrl,
            item.ThumbnailUrl,
            item.Type.ToString(),
            item.Title,
            item.Description,
            item.Year,
            item.Decade,
            item.MemoryId,
            item.AccommodationId,
            item.ZoneId,
            item.Context,
            item.IsPublished,
            item.IsApproved,
            item.DisplayOrder,
            item.IsPrimary,
            item.CreatedAt);

    public static string? DeriveDecade(int? year) => year switch
    {
        >= 1970 and < 1980 => "70s",
        >= 1980 and < 1990 => "80s",
        >= 1990 and < 2000 => "90s",
        >= 2000 and < 2010 => "00s",
        >= 2010 and < 2020 => "10s",
        >= 2020 and < 2030 => "20s",
        _ => null
    };
}

/// <summary>
/// The few fields a caller needs to show a photo without fetching the whole item.
/// </summary>
public record MediaItemPreview(
    Guid Id,
    string? ThumbnailUrl,
    string FileUrl,
    string Title);

/// <summary>
/// How many published photos exist for a year, plus a handful to show.
/// Years with no photos are simply absent from the rollup.
/// </summary>
public record MediaItemYearSummary(
    int Year,
    int PhotoCount,
    IReadOnlyList<MediaItemPreview> Previews);

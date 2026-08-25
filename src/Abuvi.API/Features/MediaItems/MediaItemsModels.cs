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
    Guid? ZoneId = null,
    /// <summary>
    /// Null means "I don't know which edition" — a VALID submission, not an error.
    /// The item lands in the unplaced pile and becomes eligible for collaborative dating,
    /// which is precisely what feeds the archive.
    /// </summary>
    Guid? CampEditionId = null,
    IReadOnlyList<Guid>? ThemeIds = null,
    /// <summary>An existing contributor, when uploading a further batch from the same person.</summary>
    Guid? MediaSourceId = null,
    /// <summary>Creates a contributor inline. Mutually exclusive with MediaSourceId.</summary>
    NewMediaSourceRequest? NewSource = null,
    /// <summary>Original folder path. Browsers only supply this for directory uploads.</summary>
    string? SourcePath = null);

/// <summary>Inline contributor creation during upload, for a first-time donor.</summary>
public record NewMediaSourceRequest(
    string ContributorName,
    Guid? ContributorUserId,
    string? ContributorContact,
    string? Notes,
    DateTime? ReceivedAt);

public record SetMediaItemEditionRequest(Guid? CampEditionId);

public record SetMediaItemSourceRequest(Guid? MediaSourceId);

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

// ──────────────────────────────────────────────────────
// Album DTOs
//
// An "album" is a QUERY over MediaItem filtered by CampEditionId — not an entity.
// (data-model.md documents PhotoAlbum/Photo entities that were never implemented;
// building them would fork the media model in two.)
// ──────────────────────────────────────────────────────

public record MediaThemeRef(Guid Id, string Name, string Slug);

public record AlbumSummaryResponse(
    Guid CampEditionId,
    int Year,
    Guid CampId,
    string CampName,
    string? CampLocality,
    decimal? Latitude,
    decimal? Longitude,
    int PhotoCount,
    int VideoCount,
    int AudioCount,
    int DocumentCount,
    int MemoryCount,
    string? CoverThumbnailUrl,
    bool ViewerAttended);

/// <summary>
/// A media item as it appears in an album, theme page or unplaced pile. Carries the
/// placement, provenance and theme context that MediaItemResponse does not.
/// </summary>
public record AlbumMediaItemResponse(
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
    Guid? CampEditionId,
    string YearSource,
    int CommentCount,
    Guid? MediaSourceId,
    string? MediaSourceName,
    /// <summary>Trimmed for members, full for Admin/Board — see MediaSourcesService.TrimSourcePath.</summary>
    string? SourcePathDisplay,
    IReadOnlyList<MediaThemeRef> Themes,
    bool IsApproved,
    bool IsPublished,
    int DisplayOrder,
    bool IsPrimary,
    DateTime CreatedAt);

public record AlbumDetailResponse(
    AlbumSummaryResponse Edition,
    IReadOnlyList<AlbumMediaItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record UnplacedMediaResponse(
    IReadOnlyList<AlbumMediaItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Per-edition, per-type counts from one grouped query.</summary>
public record AlbumCountRow(Guid CampEditionId, MediaItemType Type, int Count);

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

    /// <summary>
    /// Maps for album, theme and unplaced views. <paramref name="sourcePathDisplay"/> must
    /// already be trimmed by the caller according to the viewer's role — this mapper does
    /// not know who is asking.
    /// </summary>
    public static AlbumMediaItemResponse ToAlbumResponse(
        this MediaItem item,
        string? sourcePathDisplay,
        IReadOnlyList<MediaThemeRef>? themes = null) =>
        new(
            item.Id,
            item.UploadedByUserId,
            item.UploadedBy is null
                ? "Unknown"
                : $"{item.UploadedBy.FirstName} {item.UploadedBy.LastName}",
            item.FileUrl,
            item.ThumbnailUrl,
            item.Type.ToString(),
            item.Title,
            item.Description,
            item.Year,
            item.Decade,
            item.CampEditionId,
            item.YearSource.ToString(),
            item.CommentCount,
            item.MediaSourceId,
            item.MediaSource?.ContributorName,
            sourcePathDisplay,
            themes ?? [],
            item.IsApproved,
            item.IsPublished,
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

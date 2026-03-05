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
    public bool IsPublished { get; set; }
    public bool IsApproved { get; set; }
    public string? Context { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User UploadedBy { get; set; } = null!;
    public Memory? Memory { get; set; }
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
    string? Context);

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
    string? Context,
    bool IsPublished,
    bool IsApproved,
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
            item.Context,
            item.IsPublished,
            item.IsApproved,
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

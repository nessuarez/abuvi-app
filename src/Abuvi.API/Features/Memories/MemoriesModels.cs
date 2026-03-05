using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.Memories;

public class Memory
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? Year { get; set; }
    public Guid? CampLocationId { get; set; } // TODO: Add FK relationship when CampLocation entity is created
    public bool IsPublished { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User Author { get; set; } = null!;
    public List<MediaItem> MediaItems { get; set; } = [];
    // TODO: Add CampLocation navigation when CampLocation entity is created
}

// Request DTOs
public record CreateMemoryRequest(
    string Title,
    string Content,
    int? Year,
    Guid? CampLocationId);

// Response DTOs
public record MemoryResponse(
    Guid Id,
    Guid AuthorUserId,
    string AuthorName,
    string Title,
    string Content,
    int? Year,
    Guid? CampLocationId,
    bool IsPublished,
    bool IsApproved,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<MediaItemResponse> MediaItems);

// Mapping extensions
public static class MemoryMappingExtensions
{
    public static MemoryResponse ToResponse(this Memory memory, List<MediaItemResponse>? mediaItems = null) =>
        new(
            memory.Id,
            memory.AuthorUserId,
            (memory.Author?.FirstName + " " + memory.Author?.LastName) ?? "Unknown",
            memory.Title,
            memory.Content,
            memory.Year,
            memory.CampLocationId,
            memory.IsPublished,
            memory.IsApproved,
            memory.CreatedAt,
            memory.UpdatedAt,
            mediaItems ?? []);
}

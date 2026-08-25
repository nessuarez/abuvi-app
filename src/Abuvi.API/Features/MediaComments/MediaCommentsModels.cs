using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.MediaComments;

/// <summary>
/// A comment on a media item — "este es mi padre", "esto son las fiestas de San Abuvino".
///
/// Named MediaComment rather than PhotoComment on purpose: comments work on audio, video and
/// interviews too. An undated interview recording is exactly the kind of item the community
/// will discuss.
///
/// Comments on already-approved media publish immediately; moderation is after the fact via
/// <see cref="MediaCommentReport"/> plus Admin/Board soft-delete.
/// </summary>
public class MediaComment
{
    public Guid Id { get; set; }
    public Guid MediaItemId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Soft delete, mirroring the FamilyMember.DeletedAt pattern.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Who removed it — the author within the edit window, or a moderator.</summary>
    public Guid? DeletedByUserId { get; set; }

    // Navigation
    public MediaItem MediaItem { get; set; } = null!;
    public User Author { get; set; } = null!;
}

public enum MediaCommentReportReason
{
    Offensive,
    PrivacyConcern,
    Incorrect,
    Other
}

public enum MediaCommentReportStatus
{
    Pending,
    Actioned,
    Dismissed
}

/// <summary>
/// A member flagging a comment for moderator attention. One report per user per comment,
/// enforced by a unique index.
/// </summary>
public class MediaCommentReport
{
    public Guid Id { get; set; }
    public Guid MediaCommentId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public MediaCommentReportReason Reason { get; set; }
    public string? Notes { get; set; }
    public MediaCommentReportStatus Status { get; set; } = MediaCommentReportStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    // Navigation
    public MediaComment MediaComment { get; set; } = null!;
    public User ReportedBy { get; set; } = null!;
}

// ──────────────────────────────────────────────────────
// Request DTOs
// ──────────────────────────────────────────────────────

public record CreateMediaCommentRequest(string Body);

public record UpdateMediaCommentRequest(string Body);

public record ReportMediaCommentRequest(MediaCommentReportReason Reason, string? Notes);

public record ReviewReportRequest(MediaCommentReportStatus Status);

// ──────────────────────────────────────────────────────
// Response DTOs
// ──────────────────────────────────────────────────────

public record MediaCommentResponse(
    Guid Id,
    Guid MediaItemId,
    Guid AuthorUserId,
    string AuthorName,
    string Body,
    /// <summary>Viewer is the author AND still inside the edit window.</summary>
    bool CanEdit,
    /// <summary>That, or the viewer is Admin/Board.</summary>
    bool CanDelete,
    bool ViewerReported,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record MediaCommentReportResponse(
    Guid Id,
    Guid MediaCommentId,
    string CommentBody,
    Guid MediaItemId,
    Guid ReportedByUserId,
    string ReportedByName,
    string Reason,
    string? Notes,
    string Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

// ──────────────────────────────────────────────────────
// Mapping
// ──────────────────────────────────────────────────────

public static class MediaCommentMappingExtensions
{
    public static MediaCommentResponse ToResponse(
        this MediaComment comment,
        bool canEdit,
        bool canDelete,
        bool viewerReported) =>
        new(
            comment.Id,
            comment.MediaItemId,
            comment.AuthorUserId,
            comment.Author is null
                ? "Unknown"
                : $"{comment.Author.FirstName} {comment.Author.LastName}",
            comment.Body,
            canEdit,
            canDelete,
            viewerReported,
            comment.CreatedAt,
            comment.UpdatedAt);

    public static MediaCommentReportResponse ToResponse(this MediaCommentReport report) =>
        new(
            report.Id,
            report.MediaCommentId,
            report.MediaComment?.Body ?? string.Empty,
            report.MediaComment?.MediaItemId ?? Guid.Empty,
            report.ReportedByUserId,
            report.ReportedBy is null
                ? "Unknown"
                : $"{report.ReportedBy.FirstName} {report.ReportedBy.LastName}",
            report.Reason.ToString(),
            report.Notes,
            report.Status.ToString(),
            report.CreatedAt,
            report.ReviewedAt);
}

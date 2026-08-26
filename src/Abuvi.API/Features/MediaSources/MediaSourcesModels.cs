using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.MediaSources;

/// <summary>
/// Who provided a batch of historical material.
///
/// Distinct from <see cref="MediaItems.MediaItem.UploadedByUserId"/>, which records the account
/// that performed the upload. The provider is frequently not a registered user — a member hands
/// over a USB stick of photos taken by their late father, a family lends an album — which is why
/// <see cref="ContributorName"/> is free text rather than a User foreign key.
///
/// One row per donation, not per file: a batch of 800 photos shares a single source, so
/// correcting a misspelled name once fixes all 800.
///
/// This also feeds collaborative dating: the person who handed over the material is usually
/// the best person to ask what year it is from.
/// </summary>
public class MediaSource
{
    public Guid Id { get; set; }

    /// <summary>Free text. The provider need not be a registered user.</summary>
    public string ContributorName { get; set; } = string.Empty;

    /// <summary>Set when the provider is a member, which enables "ask them" links.</summary>
    public Guid? ContributorUserId { get; set; }

    /// <summary>
    /// Email or phone. Personal data of someone who may not be a member and never agreed to be
    /// listed — Admin/Board only, stripped server-side for everyone else.
    /// </summary>
    public string? ContributorContact { get; set; }

    public string? Notes { get; set; }

    /// <summary>When the material reached the association.</summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Who recorded this source.</summary>
    public Guid RegisteredByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User? ContributorUser { get; set; }
    public User RegisteredBy { get; set; } = null!;
}

// ──────────────────────────────────────────────────────
// Request DTOs
// ──────────────────────────────────────────────────────

public record CreateMediaSourceRequest(
    string ContributorName,
    Guid? ContributorUserId,
    string? ContributorContact,
    string? Notes,
    DateTime? ReceivedAt);

public record UpdateMediaSourceRequest(
    string ContributorName,
    Guid? ContributorUserId,
    string? ContributorContact,
    string? Notes,
    DateTime? ReceivedAt);

public record MergeMediaSourceRequest(Guid TargetId);

// ──────────────────────────────────────────────────────
// Response DTOs
// ──────────────────────────────────────────────────────

public record MediaSourceResponse(
    Guid Id,
    string ContributorName,
    Guid? ContributorUserId,
    // Null unless the caller is Admin/Board — stripped in the mapper below.
    string? ContributorContact,
    string? Notes,
    DateTime? ReceivedAt,
    Guid RegisteredByUserId,
    string RegisteredByName,
    int ItemCount,
    int UndatedItemCount,
    int? FirstYear,
    int? LastYear,
    DateTime CreatedAt);

/// <summary>Aggregates for one source, computed in a single grouped query.</summary>
public record MediaSourceStats(
    int ItemCount,
    int UndatedItemCount,
    int? FirstYear,
    int? LastYear)
{
    public static readonly MediaSourceStats Empty = new(0, 0, null, null);
}

// ──────────────────────────────────────────────────────
// Mapping
// ──────────────────────────────────────────────────────

public static class MediaSourceMappingExtensions
{
    public static MediaSourceResponse ToResponse(
        this MediaSource source,
        MediaSourceStats stats,
        bool isAdminOrBoard) =>
        new(
            source.Id,
            source.ContributorName,
            source.ContributorUserId,
            // Contact details belong to people who may not be members and never agreed to
            // be listed to the association. Stripped server-side — the frontend is not a
            // security boundary.
            isAdminOrBoard ? source.ContributorContact : null,
            source.Notes,
            source.ReceivedAt,
            source.RegisteredByUserId,
            source.RegisteredBy is null
                ? "Unknown"
                : $"{source.RegisteredBy.FirstName} {source.RegisteredBy.LastName}",
            stats.ItemCount,
            stats.UndatedItemCount,
            stats.FirstYear,
            stats.LastYear,
            source.CreatedAt);
}

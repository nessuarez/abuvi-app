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

using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.MediaDating;

/// <summary>
/// One member's answer to "¿de qué año es esta?" for an undated media item.
///
/// One vote per user per item, enforced by a unique index on
/// (MediaItemId, ProposedByUserId) — re-proposing updates the existing row rather than
/// stacking a second vote. When a year reaches consensus (see MediaDatingService) the
/// item's Year, Decade and CampEditionId are set and YearSource becomes Community.
///
/// Applies to any MediaItemType, not just photos.
/// </summary>
public class MediaItemYearProposal
{
    public Guid Id { get; set; }
    public Guid MediaItemId { get; set; }
    public Guid ProposedByUserId { get; set; }
    public int ProposedYear { get; set; }

    /// <summary>Optional venue precision when the proposer knows more than the year.</summary>
    public Guid? ProposedCampEditionId { get; set; }

    /// <summary>Why they think so — "mi hermana nació ese verano".</summary>
    public string? Rationale { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public MediaItem MediaItem { get; set; } = null!;
    public User ProposedBy { get; set; } = null!;
    public CampEdition? ProposedCampEdition { get; set; }
}

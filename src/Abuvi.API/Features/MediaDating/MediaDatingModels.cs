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

// ──────────────────────────────────────────────────────
// Request DTOs
// ──────────────────────────────────────────────────────

public record UpsertYearProposalRequest(
    int ProposedYear,
    Guid? ProposedCampEditionId,
    string? Rationale);

/// <summary>Admin override. Freezes the item against community consensus for good.</summary>
public record SetYearRequest(int Year, Guid? CampEditionId);

// ──────────────────────────────────────────────────────
// Response DTOs
// ──────────────────────────────────────────────────────

public record YearProposalResponse(
    Guid Id,
    Guid ProposedByUserId,
    string ProposedByName,
    int ProposedYear,
    Guid? ProposedCampEditionId,
    string? Rationale,
    DateTime CreatedAt);

public record YearProposalGroupResponse(
    int Year,
    Guid? CampEditionId,
    string? CampName,
    int Count,
    /// <summary>Capped at five for display — the point is "who says so", not a full roll call.</summary>
    IReadOnlyList<string> ProposerNames);

/// <summary>
/// Themes as a dating clue: if this item is tagged "San Abuvino" and other San Abuvino
/// items are dated to 1998, 2003 and 2011, those are the years worth considering.
/// </summary>
public record ThemeYearHintResponse(
    Guid ThemeId,
    string ThemeName,
    IReadOnlyList<int> YearsWithDatedItems);

/// <summary>
/// Provenance as a dating clue, and usually the strongest one: the person who handed over
/// the material generally knows roughly when it is from, and the folder it came out of
/// often names the year or the venue outright.
/// </summary>
public record SourceHintResponse(
    Guid? MediaSourceId,
    string? ContributorName,
    /// <summary>Non-null means the UI can offer "preguntar a esta persona".</summary>
    Guid? ContributorUserId,
    IReadOnlyList<int> YearsFromSameSource,
    string? SourcePathDisplay);

public record YearProposalTallyResponse(
    Guid MediaItemId,
    int? ResolvedYear,
    string YearSource,
    bool IsResolved,
    IReadOnlyList<YearProposalGroupResponse> Groups,
    YearProposalResponse? ViewerProposal,
    IReadOnlyList<ThemeYearHintResponse> ThemeHints,
    SourceHintResponse? SourceHint);

// ──────────────────────────────────────────────────────
// Mapping
// ──────────────────────────────────────────────────────

public static class MediaDatingMappingExtensions
{
    public static YearProposalResponse ToResponse(this MediaItemYearProposal p) =>
        new(
            p.Id,
            p.ProposedByUserId,
            p.ProposedBy is null
                ? "Unknown"
                : $"{p.ProposedBy.FirstName} {p.ProposedBy.LastName}",
            p.ProposedYear,
            p.ProposedCampEditionId,
            p.Rationale,
            p.CreatedAt);
}

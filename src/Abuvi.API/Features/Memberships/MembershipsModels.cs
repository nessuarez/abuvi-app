using Abuvi.API.Features.FamilyUnits;

namespace Abuvi.API.Features.Memberships;

/// <summary>
/// Represents an active membership for a family member
/// </summary>
public class Membership
{
    public Guid Id { get; set; }
    public Guid FamilyMemberId { get; set; }  // FK to FamilyMember
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }    // Nullable for active memberships
    public bool IsActive { get; set; }
    public int? MemberNumber { get; set; }  // Assigned on membership activation

    // Navigation
    public FamilyMember FamilyMember { get; set; } = null!;
    public ICollection<MembershipFee> Fees { get; set; } = new List<MembershipFee>();

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents an annual membership fee
/// </summary>
public class MembershipFee
{
    public Guid Id { get; set; }
    public Guid MembershipId { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public FeeStatus Status { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? PaymentReference { get; set; }

    // Navigation
    public Membership Membership { get; set; } = null!;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Status of a membership fee payment
/// </summary>
public enum FeeStatus
{
    Pending,    // Waiting for payment
    Paid,       // Payment received
    Overdue     // Payment deadline passed
}

// Request DTOs
public record CreateMembershipRequest(int Year);

public record UpdateMemberNumberRequest(int MemberNumber);

// Fee management DTOs
public record PayFeeRequest(
    DateTime PaidDate,
    string? PaymentReference = null
);

// Response DTOs
public record MembershipResponse(
    Guid Id,
    Guid FamilyMemberId,
    int? MemberNumber,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    IReadOnlyList<MembershipFeeResponse> Fees,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record MembershipFeeResponse(
    Guid Id,
    Guid MembershipId,
    int Year,
    decimal Amount,
    FeeStatus Status,
    DateTime? PaidDate,
    string? PaymentReference,
    DateTime CreatedAt
);

// Bulk membership DTOs
public record BulkActivateMembershipRequest(int Year);

public enum BulkMembershipResultStatus { Activated, Skipped, Failed }

public record BulkMembershipMemberResult(
    Guid MemberId,
    string MemberName,
    BulkMembershipResultStatus Status,
    string? Reason = null
);

public record BulkActivateMembershipResponse(
    int Activated,
    int Skipped,
    IReadOnlyList<BulkMembershipMemberResult> Results
);

namespace Abuvi.API.Features.FamilyUnits;

/// <summary>
/// Family unit representing a group of people who attend camp together
/// </summary>
public class FamilyUnit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid RepresentativeUserId { get; set; }
    public int? FamilyNumber { get; set; }  // Assigned when first member gets membership activated
    public string? ProfilePhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// A person within a family unit (child or adult)
/// </summary>
public class FamilyMember
{
    public Guid Id { get; set; }
    public Guid FamilyUnitId { get; set; }
    public Guid? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public FamilyRelationship Relationship { get; set; }

    // NEW FIELDS
    public string? DocumentNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Encrypted sensitive health data
    public string? MedicalNotes { get; set; }
    public string? Allergies { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Relationship type within a family unit
/// </summary>
public enum FamilyRelationship
{
    Parent,
    Child,
    Sibling,
    Spouse,
    Other
}

// Request DTOs
public record CreateFamilyUnitRequest(string Name);

public record UpdateFamilyUnitRequest(string Name);

public record UpdateFamilyNumberRequest(int FamilyNumber);

public record CreateFamilyMemberRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    FamilyRelationship Relationship,
    string? DocumentNumber = null,
    string? Email = null,
    string? Phone = null,
    string? MedicalNotes = null,
    string? Allergies = null
);

public record UpdateFamilyMemberRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    FamilyRelationship Relationship,
    string? DocumentNumber = null,
    string? Email = null,
    string? Phone = null,
    string? MedicalNotes = null,
    string? Allergies = null
);

// Response DTOs
public record FamilyUnitResponse(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    int? FamilyNumber,
    string? ProfilePhotoUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record FamilyMemberResponse(
    Guid Id,
    Guid FamilyUnitId,
    Guid? UserId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    FamilyRelationship Relationship,
    string? DocumentNumber,
    string? Email,
    string? Phone,
    bool HasMedicalNotes,    // Boolean flag only - never expose encrypted data
    bool HasAllergies,       // Boolean flag only - never expose encrypted data
    string? ProfilePhotoUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// Admin list projections and response types

/// <summary>
/// Projection for admin list queries – joins FamilyUnit with User and counts members.
/// Not a full entity; only returned from the repository's paged query.
/// </summary>
public record FamilyUnitAdminProjection(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    string RepresentativeName,
    int? FamilyNumber,
    int MembersCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Response item for the admin family units list endpoint.
/// </summary>
public record FamilyUnitListItemResponse(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    string RepresentativeName,
    int? FamilyNumber,
    int MembersCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Paginated response envelope for the admin family units list.
/// </summary>
public record PagedFamilyUnitsResponse(
    List<FamilyUnitListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

// Extension methods for mapping
public static class FamilyUnitExtensions
{
    public static FamilyUnitResponse ToResponse(this FamilyUnit unit)
        => new(
            unit.Id,
            unit.Name,
            unit.RepresentativeUserId,
            unit.FamilyNumber,
            unit.ProfilePhotoUrl,
            unit.CreatedAt,
            unit.UpdatedAt
        );
}

public static class FamilyMemberExtensions
{
    public static FamilyMemberResponse ToResponse(this FamilyMember member)
        => new(
            member.Id,
            member.FamilyUnitId,
            member.UserId,
            member.FirstName,
            member.LastName,
            member.DateOfBirth,
            member.Relationship,
            member.DocumentNumber,
            member.Email,
            member.Phone,
            !string.IsNullOrEmpty(member.MedicalNotes),
            !string.IsNullOrEmpty(member.Allergies),
            member.ProfilePhotoUrl,
            member.CreatedAt,
            member.UpdatedAt
        );
}

namespace Abuvi.API.Features.FamilyUnits;

/// <summary>
/// Family unit representing a group of people who attend camp together
/// </summary>
public class FamilyUnit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid RepresentativeUserId { get; set; }
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
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// Extension methods for mapping
public static class FamilyUnitExtensions
{
    public static FamilyUnitResponse ToResponse(this FamilyUnit unit)
        => new(
            unit.Id,
            unit.Name,
            unit.RepresentativeUserId,
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
            member.CreatedAt,
            member.UpdatedAt
        );
}

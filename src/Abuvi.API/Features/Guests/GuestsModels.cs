using Abuvi.API.Features.FamilyUnits;

namespace Abuvi.API.Features.Guests;

/// <summary>
/// External guest invited by a family to attend camps
/// </summary>
public class Guest
{
    public Guid Id { get; set; }
    public Guid FamilyUnitId { get; set; }

    // Personal data
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Encrypted sensitive health data
    public string? MedicalNotes { get; set; }
    public string? Allergies { get; set; }

    // Status
    public bool IsActive { get; set; }

    // Navigation
    public FamilyUnit FamilyUnit { get; set; } = null!;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Request DTOs
public record CreateGuestRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? DocumentNumber = null,
    string? Email = null,
    string? Phone = null,
    string? MedicalNotes = null,
    string? Allergies = null
);

public record UpdateGuestRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? DocumentNumber = null,
    string? Email = null,
    string? Phone = null,
    string? MedicalNotes = null,
    string? Allergies = null
);

// Response DTO - never expose encrypted data directly
public record GuestResponse(
    Guid Id,
    Guid FamilyUnitId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? DocumentNumber,
    string? Email,
    string? Phone,
    bool HasMedicalNotes,
    bool HasAllergies,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// Extension methods for mapping
public static class GuestExtensions
{
    public static GuestResponse ToResponse(this Guest guest)
        => new(
            guest.Id,
            guest.FamilyUnitId,
            guest.FirstName,
            guest.LastName,
            guest.DateOfBirth,
            guest.DocumentNumber,
            guest.Email,
            guest.Phone,
            HasMedicalNotes: !string.IsNullOrEmpty(guest.MedicalNotes),
            HasAllergies: !string.IsNullOrEmpty(guest.Allergies),
            guest.IsActive,
            guest.CreatedAt,
            guest.UpdatedAt
        );
}

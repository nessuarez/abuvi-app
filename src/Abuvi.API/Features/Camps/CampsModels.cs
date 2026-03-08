using System.Text.Json;
using System.Text.Json.Serialization;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Accommodation capacity breakdown for a camp or edition
/// </summary>
public class AccommodationCapacity
{
    public int? PrivateRoomsWithBathroom { get; set; }
    public int? PrivateRoomsSharedBathroom { get; set; }
    public List<SharedRoomInfo>? SharedRooms { get; set; }
    public int? Bungalows { get; set; }
    public int? CampOwnedTents { get; set; }
    public int? MemberTentAreaSquareMeters { get; set; }
    public int? MemberTentCapacityEstimate { get; set; }
    public int? MotorhomeSpots { get; set; }
    public string? Notes { get; set; }

    // Capacity descriptions (raw text from spreadsheet)
    public int? TotalCapacity { get; set; }
    public string? RoomsDescription { get; set; }
    public string? BungalowsDescription { get; set; }
    public string? TentsDescription { get; set; }
    public string? TentAreaDescription { get; set; }
    public int? ParkingSpots { get; set; }

    // Facility flags
    public bool? HasAdaptedMenu { get; set; }
    public bool? HasEnclosedDiningRoom { get; set; }
    public bool? HasSwimmingPool { get; set; }
    public bool? HasSportsCourt { get; set; }
    public bool? HasForestArea { get; set; }

    public int CalculateTotalBedCapacity()
    {
        var total = 0;
        total += (PrivateRoomsWithBathroom ?? 0) * 2;
        total += (PrivateRoomsSharedBathroom ?? 0) * 2;
        total += SharedRooms?.Sum(r => r.Quantity * r.BedsPerRoom) ?? 0;
        return total;
    }
}

/// <summary>
/// Details about a shared room configuration
/// </summary>
public class SharedRoomInfo
{
    public int Quantity { get; set; }
    public int BedsPerRoom { get; set; }
    public bool HasBathroom { get; set; }
    public bool HasShower { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Camp location entity (templates that can have multiple editions)
/// </summary>
public class Camp
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? GooglePlaceId { get; set; }

    // Extended Contact Information (from Google Places, all nullable)
    public string? FormattedAddress { get; set; }
    public string? StreetAddress { get; set; }
    public string? Locality { get; set; }
    public string? AdministrativeArea { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NationalPhoneNumber { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? GoogleMapsUrl { get; set; }

    // Google Metadata (all nullable)
    public decimal? GoogleRating { get; set; }
    public int? GoogleRatingCount { get; set; }
    public DateTime? LastGoogleSyncAt { get; set; }
    public string? BusinessStatus { get; set; }
    public string? PlaceTypes { get; set; }

    // Age-based pricing template (used as defaults for editions)
    public decimal PricePerAdult { get; set; }
    public decimal PricePerChild { get; set; }
    public decimal PricePerBaby { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Contact info
    public string? Province { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactCompany { get; set; }
    public string? SecondaryWebsiteUrl { get; set; }

    // Pricing
    public decimal? BasePrice { get; set; }
    public bool? VatIncluded { get; set; }

    // ABUVI internal tracking
    public int? ExternalSourceId { get; set; }
    public Guid? AbuviManagedByUserId { get; set; }
    public string? AbuviContactedAt { get; set; }
    public string? AbuviPossibility { get; set; }
    public string? AbuviLastVisited { get; set; }
    public bool? AbuviHasDataErrors { get; set; }

    // Audit
    public Guid? LastModifiedByUserId { get; set; }

    public string? AccommodationCapacityJson { get; set; }

    public AccommodationCapacity? GetAccommodationCapacity()
    {
        if (string.IsNullOrWhiteSpace(AccommodationCapacityJson))
            return null;
        return JsonSerializer.Deserialize<AccommodationCapacity>(
            AccommodationCapacityJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public void SetAccommodationCapacity(AccommodationCapacity? capacity)
    {
        AccommodationCapacityJson = capacity is null
            ? null
            : JsonSerializer.Serialize(capacity, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }

    // Navigation properties
    public ICollection<CampEdition> Editions { get; set; } = new List<CampEdition>();
    public ICollection<CampPhoto> Photos { get; set; } = new List<CampPhoto>();
    public Users.User? AbuviManagedByUser { get; set; }
    public ICollection<CampObservation> Observations { get; set; } = new List<CampObservation>();
    public ICollection<CampAuditLog> AuditLogs { get; set; } = new List<CampAuditLog>();
}

/// <summary>
/// Append-only observation/note for a camp with authorship tracking
/// </summary>
public class CampObservation
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Season { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Camp Camp { get; set; } = null!;
}

/// <summary>
/// Immutable field-level change tracking entry for a camp
/// </summary>
public class CampAuditLog
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
    public Camp Camp { get; set; } = null!;
}

/// <summary>
/// Represents a specific edition of a camp for a given year
/// </summary>
public class CampEdition
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Age-based pricing for this edition
    public decimal PricePerAdult { get; set; }
    public decimal PricePerChild { get; set; }
    public decimal PricePerBaby { get; set; }

    // Age ranges configuration
    public bool UseCustomAgeRanges { get; set; } = false;
    public int? CustomBabyMaxAge { get; set; }
    public int? CustomChildMinAge { get; set; }
    public int? CustomChildMaxAge { get; set; }
    public int? CustomAdultMinAge { get; set; }

    // Status workflow: Proposed → Draft → Open → Closed → Completed
    public CampEditionStatus Status { get; set; } = CampEditionStatus.Proposed;

    public int? MaxCapacity { get; set; }
    public string? Notes { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Optional reason for proposing this edition (provided during proposal, stored for board review).
    /// </summary>
    public string? ProposalReason { get; set; }

    /// <summary>
    /// Optional additional notes provided at proposal time (stored for reference; not sent by frontend after UX improvement).
    /// </summary>
    public string? ProposalNotes { get; set; }

    public bool IsArchived { get; set; } = false;

    // Period split point
    public DateOnly? HalfDate { get; set; }           // null = computed midpoint

    // Per-period pricing (one period = FirstWeek or SecondWeek)
    public decimal? PricePerAdultWeek { get; set; }   // null = partial attendance not allowed
    public decimal? PricePerChildWeek { get; set; }
    public decimal? PricePerBabyWeek { get; set; }

    // Weekend visit window (max 3 days)
    public DateOnly? WeekendStartDate { get; set; }   // null = weekend visit not allowed
    public DateOnly? WeekendEndDate { get; set; }

    // Weekend visit pricing
    public decimal? PricePerAdultWeekend { get; set; }  // null = weekend visit not allowed
    public decimal? PricePerChildWeekend { get; set; }
    public decimal? PricePerBabyWeekend { get; set; }

    // Weekend visit capacity (optional separate cap; if null, uses MaxCapacity)
    public int? MaxWeekendCapacity { get; set; }

    // Payment deadlines for this edition (null = use defaults: 117 / 75 days before StartDate)
    public DateTime? FirstPaymentDeadline { get; set; }
    public DateTime? SecondPaymentDeadline { get; set; }

    public string? AccommodationCapacityJson { get; set; }

    public AccommodationCapacity? GetAccommodationCapacity()
    {
        if (string.IsNullOrWhiteSpace(AccommodationCapacityJson))
            return null;
        return JsonSerializer.Deserialize<AccommodationCapacity>(
            AccommodationCapacityJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public void SetAccommodationCapacity(AccommodationCapacity? capacity)
    {
        AccommodationCapacityJson = capacity is null
            ? null
            : JsonSerializer.Serialize(capacity, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Camp Camp { get; set; } = null!;
    public ICollection<CampEditionExtra> Extras { get; set; } = new List<CampEditionExtra>();
    public ICollection<CampEditionAccommodation> Accommodations { get; set; } = [];
}

/// <summary>
/// Camp edition status workflow
/// </summary>
public enum CampEditionStatus
{
    Proposed,   // Initial proposal stage (multiple allowed per year)
    Draft,      // Promoted proposal (only one draft per year per camp)
    Open,       // Registrations open
    Closed,     // Registrations closed
    Completed   // Camp finished
}

/// <summary>
/// Extra service/product available for a camp edition
/// </summary>
public class CampEditionExtra
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }

    // Pricing configuration
    public PricingType PricingType { get; set; } = PricingType.PerPerson;
    public PricingPeriod PricingPeriod { get; set; } = PricingPeriod.OneTime;

    public bool IsRequired { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int? MaxQuantity { get; set; }

    // User input configuration
    public bool RequiresUserInput { get; set; } = false;
    public string? UserInputLabel { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public CampEdition CampEdition { get; set; } = null!;
}

/// <summary>
/// Pricing type for extras
/// </summary>
public enum PricingType
{
    PerPerson,  // Price per person
    PerFamily   // Fixed price per family
}

/// <summary>
/// Pricing period for extras
/// </summary>
public enum PricingPeriod
{
    OneTime,    // One-time charge
    PerDay      // Charged per day of camp
}

// ── Accommodation Types ──────────────────────────────────────────────────────

/// <summary>
/// Type of accommodation offered at a camp edition
/// </summary>
public enum AccommodationType
{
    Lodge,       // Refugio / cabaña
    Caravan,     // Caravana
    Tent,        // Tienda de campaña
    Bungalow,    // Bungalow
    Motorhome    // Autocaravana
}

/// <summary>
/// Accommodation option available for a camp edition.
/// Families rank their preferences during registration (no pricing — preference only).
/// </summary>
public class CampEditionAccommodation
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccommodationType AccommodationType { get; set; }
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public CampEdition CampEdition { get; set; } = null!;
}

// ── Camp Edition Accommodations DTOs ────────────────────────────────────────

public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool IsActive,
    int SortOrder,
    int CurrentPreferenceCount,
    int FirstChoiceCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    int SortOrder = 0
);

public record UpdateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool IsActive,
    int SortOrder
);

// ── Camp Edition Extras DTOs ──────────────────────────────────────────────────

public record CampEditionExtraResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    string? Description,
    decimal Price,
    PricingType PricingType,
    PricingPeriod PricingPeriod,
    bool IsRequired,
    bool IsActive,
    int? MaxQuantity,
    bool RequiresUserInput,
    string? UserInputLabel,
    int CurrentQuantitySold,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCampEditionExtraRequest(
    string Name,
    string? Description,
    decimal Price,
    PricingType PricingType,
    PricingPeriod PricingPeriod,
    bool IsRequired,
    int? MaxQuantity,
    bool RequiresUserInput = false,
    string? UserInputLabel = null
);

public record UpdateCampEditionExtraRequest(
    string Name,
    string? Description,
    decimal Price,
    bool IsRequired,
    bool IsActive,
    int? MaxQuantity,
    bool RequiresUserInput = false,
    string? UserInputLabel = null
);

/// <summary>
/// Association-wide settings
/// </summary>
public class AssociationSettings
{
    public Guid Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to create a new camp
/// </summary>
public record CreateCampRequest(
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    AccommodationCapacity? AccommodationCapacity = null
);

/// <summary>
/// Request to update an existing camp
/// </summary>
public record UpdateCampRequest(
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    AccommodationCapacity? AccommodationCapacity = null,
    string? Province = null,
    string? ContactEmail = null,
    string? ContactPerson = null,
    string? ContactCompany = null,
    string? SecondaryWebsiteUrl = null,
    decimal? BasePrice = null,
    bool? VatIncluded = null,
    Guid? AbuviManagedByUserId = null,
    string? AbuviContactedAt = null,
    string? AbuviPossibility = null,
    string? AbuviLastVisited = null,
    bool? AbuviHasDataErrors = null
);

/// <summary>
/// Camp response DTO (lightweight, used for list endpoints — no photos)
/// </summary>
public record CampResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    string? FormattedAddress,
    string? PhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    decimal? GoogleRating,
    int? GoogleRatingCount,
    string? BusinessStatus,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int EditionCount
);

/// <summary>
/// Full camp detail DTO including all extended Google Places fields and photos.
/// Used by GET /api/camps/{id} and POST /api/camps.
/// </summary>
public record CampDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    string? FormattedAddress,
    string? StreetAddress,
    string? Locality,
    string? AdministrativeArea,
    string? PostalCode,
    string? Country,
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    decimal? GoogleRating,
    int? GoogleRatingCount,
    string? BusinessStatus,
    string? PlaceTypes,
    DateTime? LastGoogleSyncAt,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    AccommodationCapacity? AccommodationCapacity,
    int? CalculatedTotalBedCapacity,
    IReadOnlyList<CampPhotoResponse> Photos,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Province,
    string? ContactEmail,
    string? ContactPerson,
    string? ContactCompany,
    string? SecondaryWebsiteUrl,
    decimal? BasePrice,
    bool? VatIncluded,
    int? ExternalSourceId,
    Guid? AbuviManagedByUserId,
    string? AbuviManagedByUserName,
    string? AbuviContactedAt,
    string? AbuviPossibility,
    string? AbuviLastVisited,
    bool? AbuviHasDataErrors,
    Guid? LastModifiedByUserId,
    IReadOnlyList<CampObservationResponse> Observations
);

/// <summary>
/// DTO for camp photo data returned by API
/// </summary>
public record CampPhotoResponse(
    Guid Id,
    string? PhotoReference,
    string? PhotoUrl,
    int Width,
    int Height,
    string AttributionName,
    string? AttributionUrl,
    string? Description,
    bool IsPrimary,
    int DisplayOrder
);

/// <summary>
/// Request to update age ranges configuration
/// </summary>
public record UpdateAgeRangesRequest(
    int BabyMaxAge,
    int ChildMinAge,
    int ChildMaxAge,
    int AdultMinAge
);

/// <summary>
/// Age ranges configuration response
/// </summary>
public record AgeRangesResponse(
    int BabyMaxAge,
    int ChildMinAge,
    int ChildMaxAge,
    int AdultMinAge,
    Guid? UpdatedBy,
    DateTime UpdatedAt
);

/// <summary>
/// Request to propose a new camp edition
/// </summary>
public record ProposeCampEditionRequest(
    Guid CampId,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal? PricePerAdult,
    decimal? PricePerChild,
    decimal? PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    int? MaxCapacity,
    string? Notes,
    string? Description = null,
    AccommodationCapacity? AccommodationCapacity = null,
    string? ProposalReason = null,   // Optional: reason for proposing this edition
    string? ProposalNotes = null,     // Optional: additional context (frontend no longer sends this)
    // Partial attendance (week pricing):
    DateOnly? HalfDate = null,
    decimal? PricePerAdultWeek = null,
    decimal? PricePerChildWeek = null,
    decimal? PricePerBabyWeek = null,
    // Weekend visit:
    DateOnly? WeekendStartDate = null,
    DateOnly? WeekendEndDate = null,
    decimal? PricePerAdultWeekend = null,
    decimal? PricePerChildWeekend = null,
    decimal? PricePerBabyWeekend = null,
    int? MaxWeekendCapacity = null
);

/// <summary>
/// Request to change the status of a camp edition
/// </summary>
public record ChangeEditionStatusRequest(
    CampEditionStatus Status,
    bool Force = false   // Admin-only: bypasses startDate < today constraint when re-opening
);

/// <summary>
/// Active edition response including camp location details and registration statistics.
/// RegistrationCount is always 0 until the Registrations feature is integrated.
/// </summary>
public record ActiveCampEditionResponse(
    Guid Id,
    Guid CampId,
    string CampName,
    string? CampLocation,
    string? CampFormattedAddress,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    CampEditionStatus Status,
    int? MaxCapacity,
    int RegistrationCount,
    string? Notes,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? FirstPaymentDeadline,
    DateTime? SecondPaymentDeadline
);

/// <summary>
/// Response for the current (best-available) camp edition endpoint.
/// Includes camp coordinates and computed availability fields.
/// RegistrationCount is always 0 until the Registrations feature is implemented.
/// </summary>
public record CurrentCampEditionResponse(
    Guid Id,
    Guid CampId,
    string CampName,
    string? CampLocation,
    string? CampFormattedAddress,
    decimal? CampLatitude,
    decimal? CampLongitude,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    CampEditionStatus Status,
    int? MaxCapacity,
    int RegistrationCount,
    int? AvailableSpots,
    string? Notes,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CampDescription,
    string? CampPhoneNumber,
    string? CampNationalPhoneNumber,
    string? CampWebsiteUrl,
    string? CampGoogleMapsUrl,
    decimal? CampGoogleRating,
    int? CampGoogleRatingCount,
    IReadOnlyList<CampPhotoResponse> CampPhotos,
    AccommodationCapacity? AccommodationCapacity,
    int? CalculatedTotalBedCapacity,
    IReadOnlyList<CampEditionExtraResponse> Extras,
    DateTime? FirstPaymentDeadline,
    DateTime? SecondPaymentDeadline
);

/// <summary>
/// Request to update a camp edition
/// </summary>
public record UpdateCampEditionRequest(
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    int? MaxCapacity,
    string? Notes,
    string? Description = null,
    // Partial attendance (week pricing):
    DateOnly? HalfDate = null,
    decimal? PricePerAdultWeek = null,
    decimal? PricePerChildWeek = null,
    decimal? PricePerBabyWeek = null,
    // Weekend visit:
    DateOnly? WeekendStartDate = null,
    DateOnly? WeekendEndDate = null,
    decimal? PricePerAdultWeekend = null,
    decimal? PricePerChildWeekend = null,
    decimal? PricePerBabyWeekend = null,
    int? MaxWeekendCapacity = null,
    // Payment deadlines (null = use defaults: 117/75 days before StartDate):
    DateTime? FirstPaymentDeadline = null,
    DateTime? SecondPaymentDeadline = null
);

/// <summary>
/// Camp edition response DTO
/// </summary>
public record CampEditionResponse(
    Guid Id,
    Guid CampId,
    string CampName,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    CampEditionStatus Status,
    int? MaxCapacity,
    string? Notes,
    string? Description,
    AccommodationCapacity? AccommodationCapacity,
    int? CalculatedTotalBedCapacity,
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Partial attendance fields:
    DateOnly? HalfDate,
    decimal? PricePerAdultWeek,
    decimal? PricePerChildWeek,
    decimal? PricePerBabyWeek,
    // Weekend visit fields:
    DateOnly? WeekendStartDate,
    DateOnly? WeekendEndDate,
    decimal? PricePerAdultWeekend,
    decimal? PricePerChildWeekend,
    decimal? PricePerBabyWeekend,
    int? MaxWeekendCapacity,
    // Payment deadlines:
    DateTime? FirstPaymentDeadline,
    DateTime? SecondPaymentDeadline
);

/// <summary>
/// Photo associated with a camp, sourced from Google Places API (Phase 1: references only)
/// </summary>
public class CampPhoto
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }

    public string? PhotoReference { get; set; }  // Google Places photo reference token
    public string? PhotoUrl { get; set; }         // Future: direct URL if downloaded and stored

    public int Width { get; set; }
    public int Height { get; set; }

    public string AttributionName { get; set; } = string.Empty;  // Photo author (required by Google T&C)
    public string? AttributionUrl { get; set; }                   // Author profile URL
    public string? Description { get; set; }                      // Optional caption for manual photos

    public bool IsOriginal { get; set; } = true;   // true = from Google Places, false = manually added
    public bool IsPrimary { get; set; } = false;   // Primary display photo
    public int DisplayOrder { get; set; } = 0;     // Sort order in gallery (1-based)

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Camp Camp { get; set; } = null!;
}

/// <summary>
/// Request to add a manually-uploaded photo to a camp
/// </summary>
public record AddCampPhotoRequest(
    string Url,
    string? Description,
    int DisplayOrder,
    bool IsPrimary
);

/// <summary>
/// Request to update an existing camp photo
/// </summary>
public record UpdateCampPhotoRequest(
    string Url,
    string? Description,
    int DisplayOrder,
    bool IsPrimary
);

/// <summary>
/// Request to reorder camp photos
/// </summary>
public record ReorderCampPhotosRequest(
    List<PhotoOrderItem> Photos
);

/// <summary>
/// Photo order item used in bulk reorder requests
/// </summary>
public record PhotoOrderItem(
    Guid Id,
    int DisplayOrder
);

// ── Camp Observations & Audit DTOs ──────────────────────────────────────────

public record AddCampObservationRequest(string Text, string? Season);

public record CampObservationResponse(
    Guid Id, string Text, string? Season, Guid? CreatedByUserId, DateTime CreatedAt);

public record CampAuditLogResponse(
    Guid Id, string FieldName, string? OldValue, string? NewValue,
    Guid ChangedByUserId, DateTime ChangedAt);

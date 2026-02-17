namespace Abuvi.API.Features.Camps;

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

    // Navigation properties
    public ICollection<CampEdition> Editions { get; set; } = new List<CampEdition>();
    public ICollection<CampPhoto> Photos { get; set; } = new List<CampPhoto>();
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
    public bool IsArchived { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Camp Camp { get; set; } = null!;
    public ICollection<CampEditionExtra> Extras { get; set; } = new List<CampEditionExtra>();
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
    decimal PricePerBaby
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
    bool IsActive
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
    DateTime UpdatedAt
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
    IReadOnlyList<CampPhotoResponse> Photos,
    DateTime CreatedAt,
    DateTime UpdatedAt
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
    string? Notes
);

/// <summary>
/// Request to change the status of a camp edition
/// </summary>
public record ChangeEditionStatusRequest(
    CampEditionStatus Status
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
    DateTime CreatedAt,
    DateTime UpdatedAt
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
    string? Notes
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
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt
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

    public bool IsOriginal { get; set; } = true;   // true = from Google Places, false = manually added
    public bool IsPrimary { get; set; } = false;   // Primary display photo
    public int DisplayOrder { get; set; } = 0;     // Sort order in gallery (1-based)

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Camp Camp { get; set; } = null!;
}

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

    // Age-based pricing template (used as defaults for editions)
    public decimal PricePerAdult { get; set; }
    public decimal PricePerChild { get; set; }
    public decimal PricePerBaby { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<CampEdition> Editions { get; set; } = new List<CampEdition>();
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
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive
);

/// <summary>
/// Camp response DTO
/// </summary>
public record CampResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
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

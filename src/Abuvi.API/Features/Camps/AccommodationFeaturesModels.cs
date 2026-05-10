namespace Abuvi.API.Features.Camps;

public enum FeatureApplicabilityLevel
{
    Zone,
    Accommodation,
    AccommodationType,
    Any
}

public class AccommodationFeature
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FeatureApplicabilityLevel ApplicabilityLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AccommodationFeatureAssignment> AccommodationAssignments { get; set; } = [];
    public ICollection<ZoneFeatureAssignment> ZoneAssignments { get; set; } = [];
}

public class AccommodationFeatureAssignment
{
    public Guid AccommodationId { get; set; }
    public Guid FeatureId { get; set; }
    public DateTime CreatedAt { get; set; }

    public CampEditionAccommodation Accommodation { get; set; } = null!;
    public AccommodationFeature Feature { get; set; } = null!;
}

public class ZoneFeatureAssignment
{
    public Guid ZoneId { get; set; }
    public Guid FeatureId { get; set; }
    public DateTime CreatedAt { get; set; }

    public AccommodationZone Zone { get; set; } = null!;
    public AccommodationFeature Feature { get; set; } = null!;
}

public record AccommodationFeatureResponse(
    Guid Id,
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateAccommodationFeatureRequest(
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    int SortOrder = 0
);

public record UpdateAccommodationFeatureRequest(
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    bool IsActive,
    int SortOrder
);

public record SetFeatureAssignmentsRequest(
    List<Guid> FeatureIds
);

public static class AccommodationFeatureMappingExtensions
{
    public static AccommodationFeatureResponse ToResponse(this AccommodationFeature f)
        => new(f.Id, f.Name, f.Icon, f.Description, f.ApplicabilityLevel,
               f.IsActive, f.SortOrder, f.CreatedAt, f.UpdatedAt);
}

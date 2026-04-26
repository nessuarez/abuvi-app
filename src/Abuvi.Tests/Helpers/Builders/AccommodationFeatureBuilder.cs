using Abuvi.API.Features.Camps;

namespace Abuvi.Tests.Helpers.Builders;

public class AccommodationFeatureBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Feature";
    private string _icon = "🛏";
    private FeatureApplicabilityLevel _level = FeatureApplicabilityLevel.Any;
    private bool _isActive = true;
    private int _sortOrder = 0;

    public AccommodationFeatureBuilder WithId(Guid id) { _id = id; return this; }
    public AccommodationFeatureBuilder WithName(string name) { _name = name; return this; }
    public AccommodationFeatureBuilder WithIsActive(bool active) { _isActive = active; return this; }
    public AccommodationFeatureBuilder WithSortOrder(int order) { _sortOrder = order; return this; }
    public AccommodationFeatureBuilder WithApplicabilityLevel(FeatureApplicabilityLevel level) { _level = level; return this; }

    public AccommodationFeature Build() => new()
    {
        Id = _id,
        Name = _name,
        Icon = _icon,
        ApplicabilityLevel = _level,
        IsActive = _isActive,
        SortOrder = _sortOrder,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}

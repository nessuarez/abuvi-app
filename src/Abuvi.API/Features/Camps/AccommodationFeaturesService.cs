using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Camps;

public class AccommodationFeaturesService(IAccommodationFeaturesRepository repo)
{
    public async Task<IReadOnlyList<AccommodationFeatureResponse>> GetAllAsync(
        bool? activeOnly, CancellationToken ct)
    {
        var features = await repo.GetAllAsync(activeOnly, ct);
        return features.Select(f => f.ToResponse()).ToList().AsReadOnly();
    }

    public async Task<AccommodationFeatureResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var feature = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("AccommodationFeature", id);
        return feature.ToResponse();
    }

    public async Task<AccommodationFeatureResponse> CreateAsync(
        CreateAccommodationFeatureRequest request, CancellationToken ct)
    {
        if (await repo.GetByNameAsync(request.Name, ct) is not null)
            throw new BusinessRuleException("Ya existe una característica con ese nombre");

        var feature = new AccommodationFeature
        {
            Name = request.Name,
            Icon = request.Icon,
            Description = request.Description,
            ApplicabilityLevel = request.ApplicabilityLevel,
            SortOrder = request.SortOrder
        };
        return (await repo.AddAsync(feature, ct)).ToResponse();
    }

    public async Task<AccommodationFeatureResponse> UpdateAsync(
        Guid id, UpdateAccommodationFeatureRequest request, CancellationToken ct)
    {
        var feature = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("AccommodationFeature", id);

        var duplicate = await repo.GetByNameAsync(request.Name, ct);
        if (duplicate is not null && duplicate.Id != id)
            throw new BusinessRuleException("Ya existe una característica con ese nombre");

        feature.Name = request.Name;
        feature.Icon = request.Icon;
        feature.Description = request.Description;
        feature.ApplicabilityLevel = request.ApplicabilityLevel;
        feature.IsActive = request.IsActive;
        feature.SortOrder = request.SortOrder;
        return (await repo.UpdateAsync(feature, ct)).ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var feature = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("AccommodationFeature", id);

        if (await repo.HasAssignmentsAsync(id, ct))
            throw new BusinessRuleException(
                "No se puede eliminar una característica que está en uso. Desactívela en su lugar.");

        await repo.DeleteAsync(feature, ct);
    }
}

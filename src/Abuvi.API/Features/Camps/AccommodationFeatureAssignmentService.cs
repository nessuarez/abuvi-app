using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Camps;

public class AccommodationFeatureAssignmentService(
    IAccommodationFeaturesRepository featuresRepo,
    ICampEditionAccommodationsRepository accommodationsRepo,
    IAccommodationZonesRepository zonesRepo)
{
    public async Task<IReadOnlyList<AccommodationFeatureResponse>> SetAccommodationFeaturesAsync(
        Guid accommodationId, SetFeatureAssignmentsRequest request, CancellationToken ct)
    {
        if (await accommodationsRepo.GetByIdAsync(accommodationId, ct) is null)
            throw new NotFoundException("CampEditionAccommodation", accommodationId);

        await ValidateFeaturesActiveAsync(request.FeatureIds, ct);
        await featuresRepo.SetAccommodationAssignmentsAsync(accommodationId, request.FeatureIds, ct);
        var features = await featuresRepo.GetForAccommodationAsync(accommodationId, ct);
        return features.Select(f => f.ToResponse()).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<AccommodationFeatureResponse>> SetZoneFeaturesAsync(
        Guid zoneId, SetFeatureAssignmentsRequest request, CancellationToken ct)
    {
        if (await zonesRepo.GetByIdAsync(zoneId, ct) is null)
            throw new NotFoundException("AccommodationZone", zoneId);

        await ValidateFeaturesActiveAsync(request.FeatureIds, ct);
        await featuresRepo.SetZoneAssignmentsAsync(zoneId, request.FeatureIds, ct);
        var features = await featuresRepo.GetForZoneAsync(zoneId, ct);
        return features.Select(f => f.ToResponse()).ToList().AsReadOnly();
    }

    private async Task ValidateFeaturesActiveAsync(List<Guid> featureIds, CancellationToken ct)
    {
        if (featureIds.Count == 0) return;

        var features = await featuresRepo.GetByIdsAsync(featureIds, ct);
        var foundIds = features.Select(f => f.Id).ToHashSet();

        var missing = featureIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missing.Count != 0)
            throw new ValidationException($"Las siguientes características no existen: {string.Join(", ", missing)}");

        var inactive = features.Where(f => !f.IsActive).Select(f => f.Name).ToList();
        if (inactive.Count != 0)
            throw new ValidationException(
                $"Las siguientes características están inactivas: {string.Join(", ", inactive)}");
    }
}

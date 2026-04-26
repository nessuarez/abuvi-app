namespace Abuvi.API.Features.Camps;

public interface IAccommodationFeaturesRepository
{
    Task<IReadOnlyList<AccommodationFeature>> GetAllAsync(bool? activeOnly, CancellationToken ct);
    Task<AccommodationFeature?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<AccommodationFeature?> GetByNameAsync(string name, CancellationToken ct);
    Task<AccommodationFeature> AddAsync(AccommodationFeature feature, CancellationToken ct);
    Task<AccommodationFeature> UpdateAsync(AccommodationFeature feature, CancellationToken ct);
    Task DeleteAsync(AccommodationFeature feature, CancellationToken ct);
    Task<bool> HasAssignmentsAsync(Guid featureId, CancellationToken ct);
    Task<IReadOnlyList<AccommodationFeature>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task SetAccommodationAssignmentsAsync(Guid accommodationId, IEnumerable<Guid> featureIds, CancellationToken ct);
    Task<IReadOnlyList<AccommodationFeature>> GetForAccommodationAsync(Guid accommodationId, CancellationToken ct);

    Task SetZoneAssignmentsAsync(Guid zoneId, IEnumerable<Guid> featureIds, CancellationToken ct);
    Task<IReadOnlyList<AccommodationFeature>> GetForZoneAsync(Guid zoneId, CancellationToken ct);
}

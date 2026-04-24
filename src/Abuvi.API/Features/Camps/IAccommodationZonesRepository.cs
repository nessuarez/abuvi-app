namespace Abuvi.API.Features.Camps;

public interface IAccommodationZonesRepository
{
    Task<AccommodationZone?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<AccommodationZone>> GetByCampEditionAsync(Guid campEditionId, CancellationToken ct = default);
    Task AddAsync(AccommodationZone zone, CancellationToken ct = default);
    Task UpdateAsync(AccommodationZone zone, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasActiveAssignmentsAsync(Guid zoneId, CancellationToken ct = default);
    Task AttachAccommodationsAsync(Guid zoneId, IReadOnlyList<Guid> accommodationIds, CancellationToken ct = default);
}

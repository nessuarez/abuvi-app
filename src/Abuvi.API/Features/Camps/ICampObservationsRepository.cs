namespace Abuvi.API.Features.Camps;

public interface ICampObservationsRepository
{
    Task<CampObservation> AddAsync(CampObservation observation, CancellationToken ct = default);
    Task<List<CampObservation>> GetByCampIdAsync(Guid campId, CancellationToken ct = default);
    Task<bool> CampExistsAsync(Guid campId, CancellationToken ct = default);
}

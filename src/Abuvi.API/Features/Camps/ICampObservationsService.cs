namespace Abuvi.API.Features.Camps;

public interface ICampObservationsService
{
    Task<CampObservationResponse> AddAsync(
        Guid campId, AddCampObservationRequest request, Guid userId, CancellationToken ct = default);

    Task<List<CampObservationResponse>> GetByCampIdAsync(Guid campId, CancellationToken ct = default);
}

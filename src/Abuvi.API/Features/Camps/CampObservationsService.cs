using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Camps;

public class CampObservationsService(ICampObservationsRepository repository) : ICampObservationsService
{
    public async Task<CampObservationResponse> AddAsync(
        Guid campId, AddCampObservationRequest request, Guid userId, CancellationToken ct = default)
    {
        if (!await repository.CampExistsAsync(campId, ct))
            throw new NotFoundException("Camp", campId);

        var observation = new CampObservation
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            Text = request.Text,
            Season = request.Season,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        var saved = await repository.AddAsync(observation, ct);

        return new CampObservationResponse(
            saved.Id, saved.Text, saved.Season, saved.CreatedByUserId, saved.CreatedAt);
    }

    public async Task<List<CampObservationResponse>> GetByCampIdAsync(
        Guid campId, CancellationToken ct = default)
    {
        var observations = await repository.GetByCampIdAsync(campId, ct);
        return observations.Select(o =>
            new CampObservationResponse(o.Id, o.Text, o.Season, o.CreatedByUserId, o.CreatedAt))
            .ToList();
    }
}

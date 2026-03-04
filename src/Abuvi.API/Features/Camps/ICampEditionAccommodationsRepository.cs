namespace Abuvi.API.Features.Camps;

public interface ICampEditionAccommodationsRepository
{
    Task<CampEditionAccommodation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CampEditionAccommodation>> GetByCampEditionAsync(Guid campEditionId, bool? activeOnly, CancellationToken ct = default);
    Task AddAsync(CampEditionAccommodation accommodation, CancellationToken ct = default);
    Task UpdateAsync(CampEditionAccommodation accommodation, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasPreferencesAsync(Guid accommodationId, CancellationToken ct = default);
    Task<int> GetPreferenceCountAsync(Guid accommodationId, CancellationToken ct = default);
    Task<int> GetFirstChoiceCountAsync(Guid accommodationId, CancellationToken ct = default);
}

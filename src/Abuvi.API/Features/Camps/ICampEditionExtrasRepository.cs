namespace Abuvi.API.Features.Camps;

public interface ICampEditionExtrasRepository
{
    Task<CampEditionExtra?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CampEditionExtra>> GetByCampEditionAsync(Guid campEditionId, bool? activeOnly, CancellationToken ct = default);
    Task<int> GetQuantitySoldAsync(Guid extraId, CancellationToken ct = default);
    Task AddAsync(CampEditionExtra extra, CancellationToken ct = default);
    Task UpdateAsync(CampEditionExtra extra, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class CampObservationsRepository(AbuviDbContext context) : ICampObservationsRepository
{
    public async Task<CampObservation> AddAsync(CampObservation observation, CancellationToken ct = default)
    {
        context.CampObservations.Add(observation);
        await context.SaveChangesAsync(ct);
        return observation;
    }

    public async Task<List<CampObservation>> GetByCampIdAsync(Guid campId, CancellationToken ct = default)
        => await context.CampObservations
            .AsNoTracking()
            .Where(o => o.CampId == campId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> CampExistsAsync(Guid campId, CancellationToken ct = default)
        => await context.Camps.AnyAsync(c => c.Id == campId, ct);
}

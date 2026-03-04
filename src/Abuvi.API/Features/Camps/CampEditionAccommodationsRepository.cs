using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class CampEditionAccommodationsRepository(AbuviDbContext db) : ICampEditionAccommodationsRepository
{
    public async Task<CampEditionAccommodation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.CampEditionAccommodations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<List<CampEditionAccommodation>> GetByCampEditionAsync(
        Guid campEditionId,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        var query = db.CampEditionAccommodations
            .AsNoTracking()
            .Where(e => e.CampEditionId == campEditionId);

        if (activeOnly.HasValue)
            query = query.Where(e => e.IsActive == activeOnly.Value);

        return await query
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CampEditionAccommodation accommodation, CancellationToken ct = default)
    {
        db.CampEditionAccommodations.Add(accommodation);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CampEditionAccommodation accommodation, CancellationToken ct = default)
    {
        db.CampEditionAccommodations.Update(accommodation);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var accommodation = await db.CampEditionAccommodations.FindAsync([id], ct);
        if (accommodation is not null)
        {
            db.CampEditionAccommodations.Remove(accommodation);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> HasPreferencesAsync(Guid accommodationId, CancellationToken ct = default)
        => await db.RegistrationAccommodationPreferences
            .AnyAsync(p => p.CampEditionAccommodationId == accommodationId, ct);

    public async Task<int> GetPreferenceCountAsync(Guid accommodationId, CancellationToken ct = default)
        => await db.RegistrationAccommodationPreferences
            .CountAsync(p => p.CampEditionAccommodationId == accommodationId, ct);

    public async Task<int> GetFirstChoiceCountAsync(Guid accommodationId, CancellationToken ct = default)
        => await db.RegistrationAccommodationPreferences
            .CountAsync(p => p.CampEditionAccommodationId == accommodationId && p.PreferenceOrder == 1, ct);
}

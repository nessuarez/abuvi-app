using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class CampEditionExtrasRepository(AbuviDbContext db) : ICampEditionExtrasRepository
{
    public async Task<CampEditionExtra?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.CampEditionExtras
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<List<CampEditionExtra>> GetByCampEditionAsync(
        Guid campEditionId,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        var query = db.CampEditionExtras
            .AsNoTracking()
            .Where(e => e.CampEditionId == campEditionId);

        if (activeOnly.HasValue)
            query = query.Where(e => e.IsActive == activeOnly.Value);

        return await query
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetQuantitySoldAsync(Guid extraId, CancellationToken ct = default)
    {
        // TODO: implement when RegistrationExtras DbSet is available.
        // registration_extras table does not have a DbSet yet; return 0 as a safe placeholder.
        await Task.CompletedTask;
        return 0;
    }

    public async Task AddAsync(CampEditionExtra extra, CancellationToken ct = default)
    {
        db.CampEditionExtras.Add(extra);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CampEditionExtra extra, CancellationToken ct = default)
    {
        db.CampEditionExtras.Update(extra);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var extra = await db.CampEditionExtras.FindAsync([id], ct);
        if (extra is not null)
        {
            db.CampEditionExtras.Remove(extra);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<CampEditionExtra>> GetByCampEditionTrackedAsync(
        Guid campEditionId,
        CancellationToken ct = default)
    {
        return await db.CampEditionExtras
            .Where(e => e.CampEditionId == campEditionId)
            .ToListAsync(ct);
    }

    public async Task UpdateManyAsync(List<CampEditionExtra> extras, CancellationToken ct = default)
    {
        db.CampEditionExtras.UpdateRange(extras);
        await db.SaveChangesAsync(ct);
    }
}

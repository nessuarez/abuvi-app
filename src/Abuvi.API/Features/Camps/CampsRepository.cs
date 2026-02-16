using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Repository implementation for Camp data access operations
/// </summary>
public class CampsRepository : ICampsRepository
{
    private readonly AbuviDbContext _context;

    public CampsRepository(AbuviDbContext context)
    {
        _context = context;
    }

    public async Task<Camp?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Camps
            .AsNoTracking()
            .Include(c => c.Editions)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<List<Camp>> GetAllAsync(
        bool? isActive = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Camps.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Camp> CreateAsync(Camp camp, CancellationToken cancellationToken = default)
    {
        camp.CreatedAt = DateTime.UtcNow;
        camp.UpdatedAt = DateTime.UtcNow;

        _context.Camps.Add(camp);
        await _context.SaveChangesAsync(cancellationToken);

        return camp;
    }

    public async Task<Camp> UpdateAsync(Camp camp, CancellationToken cancellationToken = default)
    {
        camp.UpdatedAt = DateTime.UtcNow;

        _context.Camps.Update(camp);
        await _context.SaveChangesAsync(cancellationToken);

        return camp;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var camp = await _context.Camps.FindAsync(new object[] { id }, cancellationToken);
        if (camp == null)
        {
            return false;
        }

        _context.Camps.Remove(camp);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}

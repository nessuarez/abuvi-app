using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Repository implementation for CampEdition data access operations
/// </summary>
public class CampEditionsRepository : ICampEditionsRepository
{
    private readonly AbuviDbContext _context;

    public CampEditionsRepository(AbuviDbContext context)
    {
        _context = context;
    }

    public async Task<CampEdition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CampEditions
            .AsNoTracking()
            .Include(e => e.Camp)
            .Include(e => e.Extras)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<CampEdition>> GetByStatusAndYearAsync(
        CampEditionStatus status,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CampEditions
            .AsNoTracking()
            .Include(e => e.Camp)
            .Where(e => e.Status == status && !e.IsArchived);

        if (year.HasValue)
        {
            query = query.Where(e => e.Year == year.Value);
        }

        return await query
            .OrderBy(e => e.Year)
            .ThenBy(e => e.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<CampEdition> CreateAsync(CampEdition edition, CancellationToken cancellationToken = default)
    {
        edition.CreatedAt = DateTime.UtcNow;
        edition.UpdatedAt = DateTime.UtcNow;

        _context.CampEditions.Add(edition);
        await _context.SaveChangesAsync(cancellationToken);

        return edition;
    }

    public async Task<CampEdition> UpdateAsync(CampEdition edition, CancellationToken cancellationToken = default)
    {
        edition.UpdatedAt = DateTime.UtcNow;

        _context.CampEditions.Update(edition);
        await _context.SaveChangesAsync(cancellationToken);

        return edition;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var edition = await _context.CampEditions.FindAsync(new object[] { id }, cancellationToken);
        if (edition == null)
        {
            return false;
        }

        _context.CampEditions.Remove(edition);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}

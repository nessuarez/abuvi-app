using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;

namespace Abuvi.API.Features.Memories;

public interface IMemoriesRepository
{
    Task<Memory?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Memory>> GetListAsync(int? year, bool? approved, CancellationToken ct);
    Task AddAsync(Memory memory, CancellationToken ct);
    Task UpdateAsync(Memory memory, CancellationToken ct);
}

public class MemoriesRepository(AbuviDbContext db) : IMemoriesRepository
{
    public async Task<Memory?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await db.Memories
            .Include(m => m.Author)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<Memory>> GetListAsync(int? year, bool? approved, CancellationToken ct)
    {
        var query = db.Memories
            .AsNoTracking()
            .Include(m => m.Author)
            .AsQueryable();

        if (year.HasValue)
            query = query.Where(m => m.Year == year.Value);

        if (approved == true)
            query = query.Where(m => m.IsApproved && m.IsPublished);
        else if (approved == false)
            query = query.Where(m => !m.IsApproved);

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Memory memory, CancellationToken ct)
    {
        db.Memories.Add(memory);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Memory memory, CancellationToken ct)
    {
        db.Memories.Update(memory);
        await db.SaveChangesAsync(ct);
    }
}

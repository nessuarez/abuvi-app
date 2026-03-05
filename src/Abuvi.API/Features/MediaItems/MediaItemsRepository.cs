using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;

namespace Abuvi.API.Features.MediaItems;

public interface IMediaItemsRepository
{
    Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MediaItem>> GetListAsync(int? year, bool? approved, string? context, MediaItemType? type, CancellationToken ct);
    Task<IReadOnlyList<MediaItem>> GetByMemoryIdAsync(Guid memoryId, CancellationToken ct);
    Task AddAsync(MediaItem mediaItem, CancellationToken ct);
    Task UpdateAsync(MediaItem mediaItem, CancellationToken ct);
    Task DeleteAsync(MediaItem mediaItem, CancellationToken ct);
}

public class MediaItemsRepository(AbuviDbContext db) : IMediaItemsRepository
{
    public async Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await db.MediaItems
            .Include(m => m.UploadedBy)
            .Include(m => m.Memory)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<MediaItem>> GetListAsync(
        int? year,
        bool? approved,
        string? context,
        MediaItemType? type,
        CancellationToken ct)
    {
        var query = db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .AsQueryable();

        if (year.HasValue)
            query = query.Where(m => m.Year == year.Value);

        if (approved == true)
            query = query.Where(m => m.IsApproved && m.IsPublished);
        else if (approved == false)
            query = query.Where(m => !m.IsApproved);

        if (!string.IsNullOrEmpty(context))
            query = query.Where(m => m.Context == context);

        if (type.HasValue)
            query = query.Where(m => m.Type == type.Value);

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MediaItem>> GetByMemoryIdAsync(Guid memoryId, CancellationToken ct)
    {
        return await db.MediaItems
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .Where(m => m.MemoryId == memoryId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(MediaItem mediaItem, CancellationToken ct)
    {
        db.MediaItems.Add(mediaItem);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MediaItem mediaItem, CancellationToken ct)
    {
        db.MediaItems.Update(mediaItem);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MediaItem mediaItem, CancellationToken ct)
    {
        db.MediaItems.Remove(mediaItem);
        await db.SaveChangesAsync(ct);
    }
}

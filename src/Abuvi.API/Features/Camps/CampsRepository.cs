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

    public async Task<Camp?> GetByIdWithPhotosAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Camps
            .AsNoTracking()
            .Include(c => c.Editions)
            .Include(c => c.Photos.OrderBy(p => p.DisplayOrder))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CampPhoto>> AddPhotosAsync(
        IEnumerable<CampPhoto> photos,
        CancellationToken cancellationToken = default)
    {
        var photoList = photos.ToList();
        if (photoList.Count == 0) return photoList;

        _context.CampPhotos.AddRange(photoList);
        await _context.SaveChangesAsync(cancellationToken);

        return photoList;
    }

    public async Task<CampPhoto?> GetPhotoByIdAsync(Guid photoId, CancellationToken cancellationToken = default)
        => await _context.CampPhotos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

    public async Task<CampPhoto> AddPhotoAsync(CampPhoto photo, CancellationToken cancellationToken = default)
    {
        photo.CreatedAt = DateTime.UtcNow;
        photo.UpdatedAt = DateTime.UtcNow;

        _context.CampPhotos.Add(photo);
        await _context.SaveChangesAsync(cancellationToken);

        return photo;
    }

    public async Task<CampPhoto> UpdatePhotoAsync(CampPhoto photo, CancellationToken cancellationToken = default)
    {
        photo.UpdatedAt = DateTime.UtcNow;

        _context.CampPhotos.Update(photo);
        await _context.SaveChangesAsync(cancellationToken);

        return photo;
    }

    public async Task<bool> DeletePhotoAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await _context.CampPhotos.FindAsync(new object[] { photoId }, cancellationToken);
        if (photo == null) return false;

        _context.CampPhotos.Remove(photo);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<CampPhoto>> GetPhotosForCampAsync(Guid campId, CancellationToken cancellationToken = default)
        => await _context.CampPhotos
            .AsNoTracking()
            .Where(p => p.CampId == campId)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task UpdatePhotoOrdersAsync(
        IEnumerable<(Guid PhotoId, int DisplayOrder)> updates,
        CancellationToken cancellationToken = default)
    {
        var updateList = updates.ToList();
        var ids = updateList.Select(u => u.PhotoId).ToList();

        var photos = await _context.CampPhotos
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        foreach (var photo in photos)
        {
            var update = updateList.First(u => u.PhotoId == photo.Id);
            photo.DisplayOrder = update.DisplayOrder;
            photo.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearPrimaryPhotoAsync(Guid campId, CancellationToken cancellationToken = default)
    {
        var primaries = await _context.CampPhotos
            .Where(p => p.CampId == campId && p.IsPrimary)
            .ToListAsync(cancellationToken);

        foreach (var photo in primaries)
        {
            photo.IsPrimary = false;
            photo.UpdatedAt = DateTime.UtcNow;
        }

        if (primaries.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
    }
}

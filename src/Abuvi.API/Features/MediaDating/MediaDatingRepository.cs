using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.MediaDating;

public interface IMediaDatingRepository
{
    Task<MediaItemYearProposal?> GetByItemAndUserAsync(
        Guid mediaItemId, Guid userId, CancellationToken ct);

    Task<IReadOnlyList<MediaItemYearProposal>> GetForItemAsync(
        Guid mediaItemId, CancellationToken ct);

    Task AddAsync(MediaItemYearProposal proposal, CancellationToken ct);
    Task UpdateAsync(MediaItemYearProposal proposal, CancellationToken ct);
    Task DeleteAsync(MediaItemYearProposal proposal, CancellationToken ct);

    /// <summary>Years that items carrying this theme have already resolved to.</summary>
    Task<IReadOnlyList<int>> GetYearsForThemeAsync(Guid themeId, CancellationToken ct);

    /// <summary>Years that other items from the same contributor resolved to.</summary>
    Task<IReadOnlyList<int>> GetYearsForSourceAsync(Guid sourceId, CancellationToken ct);
}

public class MediaDatingRepository(AbuviDbContext db) : IMediaDatingRepository
{
    public async Task<MediaItemYearProposal?> GetByItemAndUserAsync(
        Guid mediaItemId, Guid userId, CancellationToken ct)
        => await db.MediaItemYearProposals
            .Include(p => p.ProposedBy)
            .FirstOrDefaultAsync(
                p => p.MediaItemId == mediaItemId && p.ProposedByUserId == userId, ct);

    public async Task<IReadOnlyList<MediaItemYearProposal>> GetForItemAsync(
        Guid mediaItemId, CancellationToken ct)
        => await db.MediaItemYearProposals
            .AsNoTracking()
            .Include(p => p.ProposedBy)
            .Include(p => p.ProposedCampEdition)
                .ThenInclude(e => e!.Camp)
            .Where(p => p.MediaItemId == mediaItemId)
            .ToListAsync(ct);

    public async Task AddAsync(MediaItemYearProposal proposal, CancellationToken ct)
    {
        db.MediaItemYearProposals.Add(proposal);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MediaItemYearProposal proposal, CancellationToken ct)
    {
        db.MediaItemYearProposals.Update(proposal);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MediaItemYearProposal proposal, CancellationToken ct)
    {
        db.MediaItemYearProposals.Remove(proposal);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetYearsForThemeAsync(Guid themeId, CancellationToken ct)
        => await db.MediaItemThemes
            .AsNoTracking()
            .Where(t => t.MediaThemeId == themeId && t.MediaItem.Year != null)
            .Select(t => t.MediaItem.Year!.Value)
            .Distinct()
            .OrderBy(y => y)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> GetYearsForSourceAsync(Guid sourceId, CancellationToken ct)
        => await db.MediaItems
            .AsNoTracking()
            .Where(m => m.MediaSourceId == sourceId && m.Year != null)
            .Select(m => m.Year!.Value)
            .Distinct()
            .OrderBy(y => y)
            .ToListAsync(ct);
}

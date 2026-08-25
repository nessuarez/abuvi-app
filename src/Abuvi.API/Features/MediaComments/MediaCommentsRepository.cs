using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.MediaComments;

public interface IMediaCommentsRepository
{
    Task<MediaComment?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The visible thread for an item: oldest first, soft-deleted excluded.</summary>
    Task<IReadOnlyList<MediaComment>> GetThreadAsync(Guid mediaItemId, CancellationToken ct);

    /// <summary>
    /// Which of these comments the given user has already reported, in ONE query.
    /// Never ask per comment while rendering a thread.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetReportedCommentIdsForUserAsync(
        Guid userId, IReadOnlyList<Guid> commentIds, CancellationToken ct);

    /// <summary>Adds a comment and bumps the item's counter in one transaction.</summary>
    Task AddAsync(MediaComment comment, CancellationToken ct);

    /// <summary>Soft-deletes and decrements the item's counter in one transaction.</summary>
    Task SoftDeleteAsync(MediaComment comment, Guid deletedByUserId, CancellationToken ct);

    Task UpdateAsync(MediaComment comment, CancellationToken ct);
    Task<bool> ReportExistsAsync(Guid commentId, Guid userId, CancellationToken ct);
    Task AddReportAsync(MediaCommentReport report, CancellationToken ct);
    Task<MediaCommentReport?> GetReportByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MediaCommentReport>> GetReportsAsync(
        MediaCommentReportStatus? status, CancellationToken ct);
    Task UpdateReportAsync(MediaCommentReport report, CancellationToken ct);
}

public class MediaCommentsRepository(AbuviDbContext db) : IMediaCommentsRepository
{
    public async Task<MediaComment?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.MediaComments
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);

    public async Task<IReadOnlyList<MediaComment>> GetThreadAsync(Guid mediaItemId, CancellationToken ct)
        => await db.MediaComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.MediaItemId == mediaItemId && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetReportedCommentIdsForUserAsync(
        Guid userId, IReadOnlyList<Guid> commentIds, CancellationToken ct)
        => commentIds.Count == 0
            ? []
            : await db.MediaCommentReports
                .AsNoTracking()
                .Where(r => r.ReportedByUserId == userId && commentIds.Contains(r.MediaCommentId))
                .Select(r => r.MediaCommentId)
                .ToListAsync(ct);

    public async Task AddAsync(MediaComment comment, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.MediaComments.Add(comment);
        await db.MediaItems
            .Where(m => m.Id == comment.MediaItemId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CommentCount, m => m.CommentCount + 1), ct);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
    }

    public async Task SoftDeleteAsync(MediaComment comment, Guid deletedByUserId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        comment.DeletedAt = DateTime.UtcNow;
        comment.DeletedByUserId = deletedByUserId;
        db.MediaComments.Update(comment);

        // Guard against dropping below zero if a counter ever drifts.
        await db.MediaItems
            .Where(m => m.Id == comment.MediaItemId && m.CommentCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CommentCount, m => m.CommentCount - 1), ct);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
    }

    public async Task UpdateAsync(MediaComment comment, CancellationToken ct)
    {
        db.MediaComments.Update(comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ReportExistsAsync(Guid commentId, Guid userId, CancellationToken ct)
        => await db.MediaCommentReports
            .AnyAsync(r => r.MediaCommentId == commentId && r.ReportedByUserId == userId, ct);

    public async Task AddReportAsync(MediaCommentReport report, CancellationToken ct)
    {
        db.MediaCommentReports.Add(report);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MediaCommentReport?> GetReportByIdAsync(Guid id, CancellationToken ct)
        => await db.MediaCommentReports
            .Include(r => r.MediaComment)
            .Include(r => r.ReportedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<MediaCommentReport>> GetReportsAsync(
        MediaCommentReportStatus? status, CancellationToken ct)
    {
        var query = db.MediaCommentReports
            .AsNoTracking()
            .Include(r => r.MediaComment)
            .Include(r => r.ReportedBy)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    public async Task UpdateReportAsync(MediaCommentReport report, CancellationToken ct)
    {
        db.MediaCommentReports.Update(report);
        await db.SaveChangesAsync(ct);
    }
}

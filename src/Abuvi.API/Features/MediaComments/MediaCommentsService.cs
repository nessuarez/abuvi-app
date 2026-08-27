using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaItems;

namespace Abuvi.API.Features.MediaComments;

public class MediaCommentsService(
    IMediaCommentsRepository repository,
    IMediaItemsRepository mediaItemsRepository,
    ILogger<MediaCommentsService> logger)
{
    /// <summary>
    /// How long an author may edit or delete their own comment. After that only Admin/Board
    /// can remove it — the archive stays stable while typos remain fixable.
    /// </summary>
    private const int EditWindowMinutes = 15;

    private static bool IsWithinEditWindow(MediaComment c)
        => DateTime.UtcNow - c.CreatedAt < TimeSpan.FromMinutes(EditWindowMinutes);

    public async Task<IReadOnlyList<MediaCommentResponse>> GetThreadAsync(
        Guid mediaItemId, Guid viewerUserId, bool isAdminOrBoard, CancellationToken ct)
    {
        _ = await mediaItemsRepository.GetByIdAsync(mediaItemId, ct)
            ?? throw new NotFoundException("elemento multimedia", mediaItemId);

        var comments = await repository.GetThreadAsync(mediaItemId, ct);

        // One batched lookup for the whole thread rather than one per comment.
        var reported = (await repository.GetReportedCommentIdsForUserAsync(
            viewerUserId, comments.Select(c => c.Id).ToList(), ct)).ToHashSet();

        return comments
            .Select(c =>
            {
                var isAuthor = c.AuthorUserId == viewerUserId;
                var inWindow = IsWithinEditWindow(c);
                return c.ToResponse(
                    canEdit: isAuthor && inWindow,
                    canDelete: (isAuthor && inWindow) || isAdminOrBoard,
                    viewerReported: reported.Contains(c.Id));
            })
            .ToList();
    }

    /// <summary>
    /// Adds a comment. Returns null when the caller may not comment on this item, so the
    /// endpoint can answer 403 — commenting on unapproved media is restricted to moderators,
    /// because it is not published yet.
    /// </summary>
    public async Task<MediaCommentResponse?> CreateAsync(
        Guid mediaItemId, Guid userId, bool isAdminOrBoard,
        CreateMediaCommentRequest request, CancellationToken ct)
    {
        var item = await mediaItemsRepository.GetByIdAsync(mediaItemId, ct)
            ?? throw new NotFoundException("elemento multimedia", mediaItemId);

        if (!item.IsApproved && !isAdminOrBoard)
            return null;

        var comment = new MediaComment
        {
            Id = Guid.NewGuid(),
            MediaItemId = mediaItemId,
            AuthorUserId = userId,
            Body = request.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(comment, ct);

        logger.LogInformation(
            "MediaComment {CommentId} added to MediaItem {MediaItemId} by user {UserId}",
            comment.Id, mediaItemId, userId);

        var saved = await repository.GetByIdAsync(comment.Id, ct)!;
        return saved!.ToResponse(canEdit: true, canDelete: true, viewerReported: false);
    }

    /// <summary>Returns null when the caller is not the author or the window has closed.</summary>
    public async Task<MediaCommentResponse?> UpdateAsync(
        Guid id, Guid userId, UpdateMediaCommentRequest request, CancellationToken ct)
    {
        var comment = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("comentario", id);

        if (comment.AuthorUserId != userId || !IsWithinEditWindow(comment))
            return null;

        comment.Body = request.Body.Trim();
        comment.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(comment, ct);

        return comment.ToResponse(canEdit: true, canDelete: true, viewerReported: false);
    }

    /// <summary>Returns false when the caller may neither author-delete nor moderate.</summary>
    public async Task<bool> DeleteAsync(
        Guid id, Guid userId, bool isAdminOrBoard, CancellationToken ct)
    {
        var comment = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("comentario", id);

        var authorMayDelete = comment.AuthorUserId == userId && IsWithinEditWindow(comment);
        if (!authorMayDelete && !isAdminOrBoard)
            return false;

        await repository.SoftDeleteAsync(comment, userId, ct);

        logger.LogInformation(
            "MediaComment {CommentId} soft-deleted by user {UserId}", id, userId);

        return true;
    }

    public async Task ReportAsync(
        Guid commentId, Guid userId, ReportMediaCommentRequest request, CancellationToken ct)
    {
        _ = await repository.GetByIdAsync(commentId, ct)
            ?? throw new NotFoundException("comentario", commentId);

        if (await repository.ReportExistsAsync(commentId, userId, ct))
            throw new BusinessRuleException("Ya has denunciado este comentario");

        await repository.AddReportAsync(new MediaCommentReport
        {
            Id = Guid.NewGuid(),
            MediaCommentId = commentId,
            ReportedByUserId = userId,
            Reason = request.Reason,
            Notes = request.Notes,
            Status = MediaCommentReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        }, ct);

        logger.LogInformation(
            "MediaComment {CommentId} reported by user {UserId} for {Reason}",
            commentId, userId, request.Reason);
    }

    public async Task<IReadOnlyList<MediaCommentReportResponse>> GetReportsAsync(
        MediaCommentReportStatus? status, CancellationToken ct)
    {
        var reports = await repository.GetReportsAsync(status, ct);
        return reports.Select(r => r.ToResponse()).ToList();
    }

    public async Task<MediaCommentReportResponse> ReviewReportAsync(
        Guid reportId, Guid reviewerUserId, MediaCommentReportStatus status, CancellationToken ct)
    {
        if (status == MediaCommentReportStatus.Pending)
            throw new ValidationException("Una denuncia revisada no puede volver a estar pendiente");

        var report = await repository.GetReportByIdAsync(reportId, ct)
            ?? throw new NotFoundException("denuncia", reportId);

        report.Status = status;
        report.ReviewedAt = DateTime.UtcNow;
        report.ReviewedByUserId = reviewerUserId;

        await repository.UpdateReportAsync(report, ct);

        logger.LogInformation(
            "Report {ReportId} marked {Status} by user {UserId}", reportId, status, reviewerUserId);

        return report.ToResponse();
    }
}

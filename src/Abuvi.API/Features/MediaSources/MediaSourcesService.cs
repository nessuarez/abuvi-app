using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.MediaSources;

public class MediaSourcesService(
    IMediaSourcesRepository repository,
    IUsersRepository usersRepository,
    ILogger<MediaSourcesService> logger)
{
    /// <summary>
    /// How many trailing path segments a regular member may see of a SourcePath.
    /// Enough to carry the dating clue ("…/Verano 98/Selva de Oza"), not enough to
    /// expose the donor's home directory layout.
    /// </summary>
    private const int MemberVisiblePathSegments = 3;

    /// <summary>
    /// Trims an original folder path for display.
    ///
    /// The path is a genuine dating clue — a human may recognise "Verano con los Martínez"
    /// where the resolver's regex sees nothing. But raw paths leak:
    /// "D:/Users/maria.carmen.lopez/Fotos privadas/..." names a person and their directory
    /// structure. Members see only the trailing segments; Admin/Board see everything.
    ///
    /// Trimming happens here and never in the database: the full path is evidence.
    /// </summary>
    public static string? TrimSourcePath(string? path, bool isAdminOrBoard)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (isAdminOrBoard) return path;

        var segments = path.Replace('\\', '/')
                           .Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length <= MemberVisiblePathSegments
            ? string.Join('/', segments)
            : ".../" + string.Join('/', segments[^MemberVisiblePathSegments..]);
    }

    public async Task<IReadOnlyList<MediaSourceResponse>> GetListAsync(
        bool isAdminOrBoard, CancellationToken ct)
    {
        var sources = await repository.GetAllAsync(ct);
        var stats = await repository.GetStatsAsync(sources.Select(s => s.Id).ToList(), ct);

        return sources
            .Select(s => s.ToResponse(
                stats.TryGetValue(s.Id, out var st) ? st : MediaSourceStats.Empty,
                isAdminOrBoard))
            .ToList();
    }

    public async Task<MediaSourceResponse> GetByIdAsync(
        Guid id, bool isAdminOrBoard, CancellationToken ct)
    {
        var source = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("aportante", id);

        var stats = await repository.GetStatsAsync([id], ct);

        return source.ToResponse(
            stats.TryGetValue(id, out var st) ? st : MediaSourceStats.Empty,
            isAdminOrBoard);
    }

    public async Task<(IReadOnlyList<MediaItem> Items, int Total)> GetItemsAsync(
        Guid id, int page, int pageSize, CancellationToken ct)
    {
        _ = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("aportante", id);

        return await repository.GetItemsAsync(id, page, pageSize, ct);
    }

    /// <summary>
    /// Registers a contributor. Any authenticated member may do this — any member could be
    /// the one collecting a neighbour's shoebox of photos.
    /// </summary>
    public async Task<MediaSourceResponse> CreateAsync(
        Guid registeredByUserId, CreateMediaSourceRequest request, CancellationToken ct)
    {
        if (request.ContributorUserId is { } contributorId)
        {
            _ = await usersRepository.GetByIdAsync(contributorId, ct)
                ?? throw new NotFoundException("usuario", contributorId);
        }

        var source = new MediaSource
        {
            Id = Guid.NewGuid(),
            ContributorName = request.ContributorName.Trim(),
            ContributorUserId = request.ContributorUserId,
            ContributorContact = request.ContributorContact,
            Notes = request.Notes,
            ReceivedAt = request.ReceivedAt,
            RegisteredByUserId = registeredByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(source, ct);

        logger.LogInformation(
            "MediaSource {MediaSourceId} registered for contributor {ContributorName} by user {UserId}",
            source.Id, source.ContributorName, registeredByUserId);

        var saved = await repository.GetByIdAsync(source.Id, ct)!;
        return saved!.ToResponse(MediaSourceStats.Empty, isAdminOrBoard: true);
    }

    /// <summary>
    /// Returns false when the caller may not edit this source, so the endpoint can answer
    /// 403. Editing is limited to Admin/Board or whoever registered it — that prevents
    /// drive-by renaming while keeping correction easy for the person who knows the
    /// provenance.
    /// </summary>
    public async Task<MediaSourceResponse?> UpdateAsync(
        Guid id,
        Guid callerUserId,
        bool isAdminOrBoard,
        UpdateMediaSourceRequest request,
        CancellationToken ct)
    {
        var source = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("aportante", id);

        if (!isAdminOrBoard && source.RegisteredByUserId != callerUserId)
            return null;

        if (request.ContributorUserId is { } contributorId)
        {
            _ = await usersRepository.GetByIdAsync(contributorId, ct)
                ?? throw new NotFoundException("usuario", contributorId);
        }

        source.ContributorName = request.ContributorName.Trim();
        source.ContributorUserId = request.ContributorUserId;
        source.ContributorContact = request.ContributorContact;
        source.Notes = request.Notes;
        source.ReceivedAt = request.ReceivedAt;
        source.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(source, ct);

        var stats = await repository.GetStatsAsync([id], ct);
        return source.ToResponse(
            stats.TryGetValue(id, out var st) ? st : MediaSourceStats.Empty,
            isAdminOrBoard);
    }

    /// <summary>
    /// Folds one contributor into another and deletes the emptied row.
    ///
    /// Free-text names guarantee near-duplicates over time ("Manolo García" /
    /// "Manuel García"), so this is what keeps the contributor list usable rather than
    /// a nice-to-have. Both steps run in one transaction: repointing 800 items and then
    /// failing to delete the source would leave the catalogue worse than before.
    /// </summary>
    public async Task<int> MergeAsync(Guid sourceId, Guid targetId, CancellationToken ct)
    {
        if (sourceId == targetId)
            throw new ValidationException("No se puede fusionar un aportante consigo mismo");

        _ = await repository.GetByIdAsync(sourceId, ct)
            ?? throw new NotFoundException("aportante", sourceId);
        _ = await repository.GetByIdAsync(targetId, ct)
            ?? throw new NotFoundException("aportante", targetId);

        var moved = await repository.MergeAsync(sourceId, targetId, ct);

        logger.LogInformation(
            "Merged MediaSource {SourceId} into {TargetId}, {MovedCount} item(s) repointed",
            sourceId, targetId, moved);

        return moved;
    }

    /// <summary>
    /// RGPD erasure for a contributor who is not a member and never signed anything.
    /// Keeps the row and the donated media — only the identifying fields go — so the
    /// archive survives while the person disappears from it. One operation rather than
    /// an admin editing three fields by hand and missing one.
    /// </summary>
    public async Task AnonymiseAsync(Guid id, CancellationToken ct)
    {
        var source = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("aportante", id);

        source.ContributorName = "(anónimo)";
        source.ContributorContact = null;
        source.ContributorUserId = null;
        source.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(source, ct);

        logger.LogInformation("MediaSource {MediaSourceId} anonymised on request", id);
    }

    /// <summary>
    /// Deletes a contributor. Items keep their media — MediaSourceId simply becomes null
    /// through the SetNull foreign key.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var source = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("aportante", id);

        await repository.DeleteAsync(source, ct);

        logger.LogInformation("MediaSource {MediaSourceId} deleted", id);
    }
}

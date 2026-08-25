using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaSources;
using Abuvi.API.Features.MediaThemes;
using Abuvi.API.Features.Memories;

namespace Abuvi.API.Features.MediaItems;

/// <summary>
/// Camp edition albums.
///
/// An album is a QUERY over MediaItem filtered by CampEditionId — there is no album
/// entity, and building one would fork the media model in two.
///
/// The index covers 50 editions today and grows by one a year, so every method here is
/// written to issue a CONSTANT number of queries regardless of how many editions exist.
/// The integration test asserts that; do not introduce a per-edition lookup.
/// </summary>
public class AlbumsService(
    IMediaItemsRepository repository,
    ICampEditionsRepository editionsRepository,
    IMemoriesRepository memoriesRepository,
    CampEditionAttendanceService attendanceService,
    IMediaThemesRepository themesRepository)
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;

    public static int ClampPageSize(int pageSize)
        => pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    public static int ClampPage(int page) => page < 1 ? 1 : page;

    public async Task<IReadOnlyList<AlbumSummaryResponse>> GetIndexAsync(
        Guid viewerUserId, CancellationToken ct)
    {
        // Six queries total, whether there are 50 editions or 500.
        var editions = await editionsRepository.GetAllAsync(cancellationToken: ct);
        var mediaCounts = await repository.GetAlbumCountsAsync(ct);
        var memoryCounts = await memoriesRepository.GetCountsByEditionAsync(ct);
        var covers = await repository.GetCoversAsync(ct);
        var attended = (await attendanceService.GetAttendedEditionIdsAsync(viewerUserId, ct)).ToHashSet();

        var countsByEdition = mediaCounts
            .GroupBy(c => c.CampEditionId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(c => c.Type, c => c.Count));

        return editions
            .OrderByDescending(e => e.Year)
            .Select(e => BuildSummary(e, countsByEdition, memoryCounts, covers, attended))
            .ToList();
    }

    public async Task<AlbumDetailResponse> GetAlbumAsync(
        Guid editionId, int page, int pageSize, MediaItemType? type, Guid? themeId,
        Guid viewerUserId, bool isAdminOrBoard, CancellationToken ct)
    {
        var edition = await editionsRepository.GetByIdAsync(editionId, ct)
            ?? throw new NotFoundException("edición", editionId);

        page = ClampPage(page);
        pageSize = ClampPageSize(pageSize);

        var (items, total) = await repository.GetAlbumPageAsync(
            editionId, page, pageSize, type, themeId, includeUnapproved: isAdminOrBoard, ct);

        var mapped = await MapItemsAsync(items, isAdminOrBoard, ct);

        // The summary for a single album still needs the same aggregates; scoping them to
        // one edition keeps this cheap.
        var mediaCounts = await repository.GetAlbumCountsAsync(ct);
        var memoryCounts = await memoriesRepository.GetCountsByEditionAsync(ct);
        var covers = await repository.GetCoversAsync(ct);
        var attended = (await attendanceService.GetAttendedEditionIdsAsync(viewerUserId, ct)).ToHashSet();

        var countsByEdition = mediaCounts
            .GroupBy(c => c.CampEditionId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(c => c.Type, c => c.Count));

        var summary = BuildSummary(edition, countsByEdition, memoryCounts, covers, attended);

        return new AlbumDetailResponse(summary, mapped, total, page, pageSize);
    }

    /// <summary>
    /// The "sin ubicar" pile: approved media whose edition is unknown. This is a waiting
    /// room, not a rejects bin — everything here is expected to leave it eventually.
    /// </summary>
    public async Task<UnplacedMediaResponse> GetUnplacedAsync(
        int page, int pageSize, MediaItemType? type, Guid? mediaSourceId,
        bool suggestedForMe, Guid viewerUserId, bool isAdminOrBoard, CancellationToken ct)
    {
        page = ClampPage(page);
        pageSize = ClampPageSize(pageSize);

        IReadOnlyList<Guid>? suggestedEditionIds = null;
        Guid? contributedBy = null;

        if (suggestedForMe)
        {
            suggestedEditionIds = await attendanceService.GetAttendedEditionIdsAsync(viewerUserId, ct);
            contributedBy = viewerUserId;
        }

        var (items, total) = await repository.GetUnplacedPageAsync(
            page, pageSize, type, mediaSourceId, suggestedEditionIds, contributedBy,
            includeUnapproved: isAdminOrBoard, ct);

        var mapped = await MapItemsAsync(items, isAdminOrBoard, ct);
        return new UnplacedMediaResponse(mapped, total, page, pageSize);
    }

    /// <summary>
    /// Maps a page of items, resolving every item's themes in one query and trimming
    /// source paths for the viewer's role.
    /// </summary>
    public async Task<IReadOnlyList<AlbumMediaItemResponse>> MapItemsAsync(
        IReadOnlyList<MediaItem> items, bool isAdminOrBoard, CancellationToken ct)
    {
        var tags = await themesRepository.GetThemesForItemsAsync(
            items.Select(i => i.Id).ToList(), ct);
        var themesByItem = MediaThemeMappingExtensions.GroupRefsByItem(tags);

        return items
            .Select(m => m.ToAlbumResponse(
                MediaSourcesService.TrimSourcePath(m.SourcePath, isAdminOrBoard),
                themesByItem.TryGetValue(m.Id, out var refs) ? refs : []))
            .ToList();
    }

    private static AlbumSummaryResponse BuildSummary(
        CampEdition edition,
        IReadOnlyDictionary<Guid, Dictionary<MediaItemType, int>> countsByEdition,
        IReadOnlyDictionary<Guid, int> memoryCounts,
        IReadOnlyDictionary<Guid, string?> covers,
        IReadOnlySet<Guid> attended)
    {
        countsByEdition.TryGetValue(edition.Id, out var counts);

        int CountOf(MediaItemType t) => counts is not null && counts.TryGetValue(t, out var n) ? n : 0;

        return new AlbumSummaryResponse(
            edition.Id,
            edition.Year,
            edition.CampId,
            edition.Camp?.Name ?? "Desconocido",
            edition.Camp?.Locality,
            edition.Camp?.Latitude,
            edition.Camp?.Longitude,
            CountOf(MediaItemType.Photo),
            CountOf(MediaItemType.Video),
            // Interviews are audio for counting purposes — the distinction matters for
            // playback, not for "how much is in this album".
            CountOf(MediaItemType.Audio) + CountOf(MediaItemType.Interview),
            CountOf(MediaItemType.Document),
            memoryCounts.TryGetValue(edition.Id, out var m) ? m : 0,
            covers.TryGetValue(edition.Id, out var cover) ? cover : null,
            attended.Contains(edition.Id));
    }
}

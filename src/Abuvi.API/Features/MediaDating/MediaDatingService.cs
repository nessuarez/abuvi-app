using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.MediaSources;
using Abuvi.API.Features.MediaThemes;

namespace Abuvi.API.Features.MediaDating;

/// <summary>
/// Collaborative dating: "¿de qué año es esta?".
///
/// Turns the problem of a disordered archive into the mechanic of participation. Members
/// propose a year; when enough of them agree the item places itself.
/// </summary>
public class MediaDatingService(
    IMediaDatingRepository repository,
    IMediaItemsRepository mediaItemsRepository,
    IMediaThemesRepository themesRepository,
    ICampEditionsRepository campEditionsRepository,
    IMediaSourcesRepository mediaSourcesRepository,
    ILogger<MediaDatingService> logger)
{
    /// <summary>A year needs at least this many proposals before it can win.</summary>
    private const int MinProposalsForConsensus = 3;

    /// <summary>...and at least this share of all proposals for the item.</summary>
    private const double ConsensusRatio = 0.66;

    public async Task<YearProposalTallyResponse> GetTallyAsync(
        Guid mediaItemId, Guid viewerUserId, bool isAdminOrBoard, CancellationToken ct)
    {
        var item = await mediaItemsRepository.GetByIdAsync(mediaItemId, ct)
            ?? throw new NotFoundException("elemento multimedia", mediaItemId);

        var proposals = await repository.GetForItemAsync(mediaItemId, ct);
        return await BuildTallyAsync(item, proposals, viewerUserId, isAdminOrBoard, ct);
    }

    /// <summary>
    /// Records or replaces the caller's proposal, then re-evaluates consensus. The unique
    /// index on (MediaItemId, ProposedByUserId) is what makes this an upsert rather than a
    /// second vote.
    /// </summary>
    public async Task<YearProposalTallyResponse> UpsertAsync(
        Guid mediaItemId, Guid userId, bool isAdminOrBoard,
        UpsertYearProposalRequest request, CancellationToken ct)
    {
        var item = await mediaItemsRepository.GetByIdAsync(mediaItemId, ct)
            ?? throw new NotFoundException("elemento multimedia", mediaItemId);

        if (request.ProposedCampEditionId is { } editionId)
        {
            var edition = await campEditionsRepository.GetByIdAsync(editionId, ct)
                ?? throw new NotFoundException("edición", editionId);

            if (edition.Year != request.ProposedYear)
                throw new ValidationException(
                    $"La edición seleccionada es del año {edition.Year}, no de {request.ProposedYear}");
        }

        var existing = await repository.GetByItemAndUserAsync(mediaItemId, userId, ct);

        if (existing is null)
        {
            await repository.AddAsync(new MediaItemYearProposal
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                ProposedByUserId = userId,
                ProposedYear = request.ProposedYear,
                ProposedCampEditionId = request.ProposedCampEditionId,
                Rationale = request.Rationale,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, ct);
        }
        else
        {
            existing.ProposedYear = request.ProposedYear;
            existing.ProposedCampEditionId = request.ProposedCampEditionId;
            existing.Rationale = request.Rationale;
            existing.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(existing, ct);
        }

        await EvaluateConsensusAsync(item, ct);

        var proposals = await repository.GetForItemAsync(mediaItemId, ct);
        return await BuildTallyAsync(item, proposals, userId, isAdminOrBoard, ct);
    }

    /// <summary>
    /// Withdraws the caller's proposal and re-evaluates. This can UN-resolve an item whose
    /// consensus no longer holds — a vote that can only ever add is not a vote.
    /// </summary>
    public async Task<YearProposalTallyResponse> WithdrawAsync(
        Guid mediaItemId, Guid userId, bool isAdminOrBoard, CancellationToken ct)
    {
        var item = await mediaItemsRepository.GetByIdAsync(mediaItemId, ct)
            ?? throw new NotFoundException("elemento multimedia", mediaItemId);

        var existing = await repository.GetByItemAndUserAsync(mediaItemId, userId, ct)
            ?? throw new NotFoundException("propuesta de año", mediaItemId);

        await repository.DeleteAsync(existing, ct);
        await EvaluateConsensusAsync(item, ct);

        var proposals = await repository.GetForItemAsync(mediaItemId, ct);
        return await BuildTallyAsync(item, proposals, userId, isAdminOrBoard, ct);
    }

    /// <summary>
    /// Admin override. Sets YearSource to Admin, which freezes the item against consensus
    /// permanently — a human decision is not up for a vote.
    /// </summary>
    public async Task<YearProposalTallyResponse> SetYearAsAdminAsync(
        Guid mediaItemId, Guid callerUserId, SetYearRequest request, CancellationToken ct)
    {
        var item = await mediaItemsRepository.GetByIdAsync(mediaItemId, ct)
            ?? throw new NotFoundException("elemento multimedia", mediaItemId);

        var editionId = request.CampEditionId;
        if (editionId is null)
        {
            var candidates = await campEditionsRepository.GetByYearAsync(request.Year, ct);
            if (candidates.Count == 1) editionId = candidates[0].Id;
        }

        item.Year = request.Year;
        item.Decade = MediaItemMappingExtensions.DeriveDecade(request.Year);
        item.CampEditionId = editionId;
        item.YearSource = MediaItemYearSource.Admin;
        item.UpdatedAt = DateTime.UtcNow;

        await mediaItemsRepository.UpdateAsync(item, ct);

        logger.LogInformation(
            "MediaItem {MediaItemId} dated to {Year} by admin {UserId}; consensus frozen",
            mediaItemId, request.Year, callerUserId);

        var proposals = await repository.GetForItemAsync(mediaItemId, ct);
        return await BuildTallyAsync(item, proposals, callerUserId, isAdminOrBoard: true, ct);
    }

    /// <summary>
    /// The consensus rule, in one place. Runs after every insert, update and withdrawal.
    /// </summary>
    private async Task EvaluateConsensusAsync(MediaItem item, CancellationToken ct)
    {
        // A manual admin decision is never overwritten by the community.
        if (item.YearSource == MediaItemYearSource.Admin) return;

        var proposals = await repository.GetForItemAsync(item.Id, ct);

        if (proposals.Count == 0)
        {
            if (item.YearSource == MediaItemYearSource.Community)
                await ClearCommunityDatingAsync(item, ct);
            return;
        }

        var groups = proposals
            .GroupBy(p => p.ProposedYear)
            .OrderByDescending(g => g.Count())
            .ToList();

        var top = groups[0];
        var hasConsensus =
            top.Count() >= MinProposalsForConsensus &&
            (double)top.Count() / proposals.Count >= ConsensusRatio;

        if (!hasConsensus)
        {
            // A withdrawal may have dropped a previously-resolved item below the bar.
            if (item.YearSource == MediaItemYearSource.Community)
                await ClearCommunityDatingAsync(item, ct);
            return;
        }

        // Most-proposed edition within the winning year, else the unique edition for that
        // year, else leave whatever placement the item already had.
        var editionId = top
            .Where(p => p.ProposedCampEditionId is not null)
            .GroupBy(p => p.ProposedCampEditionId!.Value)
            .OrderByDescending(g => g.Count())
            .Select(g => (Guid?)g.Key)
            .FirstOrDefault();

        if (editionId is null)
        {
            var candidates = await campEditionsRepository.GetByYearAsync(top.Key, ct);
            if (candidates.Count == 1) editionId = candidates[0].Id;
        }

        item.Year = top.Key;
        item.Decade = MediaItemMappingExtensions.DeriveDecade(top.Key);
        if (editionId is not null) item.CampEditionId = editionId;
        item.YearSource = MediaItemYearSource.Community;
        item.UpdatedAt = DateTime.UtcNow;

        await mediaItemsRepository.UpdateAsync(item, ct);

        logger.LogInformation(
            "MediaItem {MediaItemId} dated to {Year} by community consensus ({Votes}/{Total})",
            item.Id, top.Key, top.Count(), proposals.Count);
    }

    private async Task ClearCommunityDatingAsync(MediaItem item, CancellationToken ct)
    {
        item.Year = null;
        item.Decade = null;
        item.CampEditionId = null;
        item.YearSource = MediaItemYearSource.Unknown;
        item.UpdatedAt = DateTime.UtcNow;

        await mediaItemsRepository.UpdateAsync(item, ct);

        logger.LogInformation(
            "MediaItem {MediaItemId} returned to the unplaced pile: consensus no longer holds",
            item.Id);
    }

    private async Task<YearProposalTallyResponse> BuildTallyAsync(
        MediaItem item,
        IReadOnlyList<MediaItemYearProposal> proposals,
        Guid viewerUserId,
        bool isAdminOrBoard,
        CancellationToken ct)
    {
        var groups = proposals
            .GroupBy(p => p.ProposedYear)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .Select(g =>
            {
                var topEdition = g
                    .Where(p => p.ProposedCampEditionId is not null)
                    .GroupBy(p => p.ProposedCampEditionId!.Value)
                    .OrderByDescending(x => x.Count())
                    .FirstOrDefault();

                return new YearProposalGroupResponse(
                    g.Key,
                    topEdition?.Key,
                    topEdition?.First().ProposedCampEdition?.Camp?.Name,
                    g.Count(),
                    g.Take(5)
                     .Select(p => p.ProposedBy is null
                         ? "Unknown"
                         : $"{p.ProposedBy.FirstName} {p.ProposedBy.LastName}")
                     .ToList());
            })
            .ToList();

        var viewerProposal = proposals
            .FirstOrDefault(p => p.ProposedByUserId == viewerUserId)?
            .ToResponse();

        var themeHints = await BuildThemeHintsAsync(item, ct);
        var sourceHint = await BuildSourceHintAsync(item, isAdminOrBoard, ct);

        return new YearProposalTallyResponse(
            item.Id,
            item.Year,
            item.YearSource.ToString(),
            item.CampEditionId is not null,
            groups,
            viewerProposal,
            themeHints,
            sourceHint);
    }

    private async Task<IReadOnlyList<ThemeYearHintResponse>> BuildThemeHintsAsync(
        MediaItem item, CancellationToken ct)
    {
        var tags = await themesRepository.GetThemesForItemsAsync([item.Id], ct);
        var hints = new List<ThemeYearHintResponse>();

        foreach (var tag in tags)
        {
            var years = (await repository.GetYearsForThemeAsync(tag.MediaThemeId, ct))
                .Where(y => y != item.Year)
                .ToList();

            if (years.Count > 0)
                hints.Add(new ThemeYearHintResponse(
                    tag.MediaThemeId, tag.MediaTheme.Name, years));
        }

        return hints;
    }

    private async Task<SourceHintResponse?> BuildSourceHintAsync(
        MediaItem item, bool isAdminOrBoard, CancellationToken ct)
    {
        var pathDisplay = MediaSourcesService.TrimSourcePath(item.SourcePath, isAdminOrBoard);

        if (item.MediaSourceId is not { } sourceId)
            return pathDisplay is null
                ? null
                : new SourceHintResponse(null, null, null, [], pathDisplay);

        var source = await mediaSourcesRepository.GetByIdAsync(sourceId, ct);
        var years = (await repository.GetYearsForSourceAsync(sourceId, ct))
            .Where(y => y != item.Year)
            .ToList();

        return new SourceHintResponse(
            sourceId,
            source?.ContributorName,
            source?.ContributorUserId,
            years,
            pathDisplay);
    }
}

using Abuvi.API.Features.MediaItems;

namespace Abuvi.API.Features.Camps;

/// <summary>
/// Read-only view of the association's camp history for the 50th anniversary section.
/// Kept apart from CampEditionsService because it is the only camp read that reaches
/// into MediaItems, and the edition lifecycle has no business depending on media.
/// </summary>
public class CampHistoryService
{
    /// <summary>Context tag the anniversary upload form stamps on every contribution.</summary>
    public const string AnniversaryContext = "anniversary-50";

    /// <summary>Enough to give a marker a face, few enough to keep the payload small.</summary>
    public const int PreviewPhotosPerEdition = 3;

    private readonly ICampEditionsRepository _editionsRepository;
    private readonly IMediaItemsRepository _mediaRepository;

    public CampHistoryService(
        ICampEditionsRepository editionsRepository,
        IMediaItemsRepository mediaRepository)
    {
        _editionsRepository = editionsRepository;
        _mediaRepository = mediaRepository;
    }

    /// <summary>
    /// Returns every completed camp edition ordered by year, with its venue resolved,
    /// its position in the venue's history, and how many photos survive from that year.
    /// </summary>
    public async Task<List<CampHistoryResponse>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var editions = await _editionsRepository.GetByStatusAndYearAsync(
            CampEditionStatus.Completed, null, cancellationToken);

        var ordered = editions.OrderBy(e => e.Year).ToList();
        if (ordered.Count == 0)
            return [];

        // One rollup for the whole history — never one query per edition.
        var summaries = await _mediaRepository.GetYearSummariesAsync(
            AnniversaryContext, PreviewPhotosPerEdition, cancellationToken);
        var photosByYear = summaries.ToDictionary(s => s.Year);

        var totalsAtVenue = ordered
            .GroupBy(e => e.CampId)
            .ToDictionary(g => g.Key, g => g.Count());

        var visitsSoFar = new Dictionary<Guid, int>();
        var history = new List<CampHistoryResponse>(ordered.Count);

        foreach (var edition in ordered)
        {
            visitsSoFar[edition.CampId] = visitsSoFar.GetValueOrDefault(edition.CampId) + 1;

            var previews = photosByYear.TryGetValue(edition.Year, out var summary)
                ? summary.Previews
                    .Select(p => new CampHistoryPhotoResponse(
                        p.Id,
                        // No thumbnail generated: the full image is still better than nothing.
                        p.ThumbnailUrl ?? p.FileUrl,
                        p.Title))
                    .ToList()
                : [];

            history.Add(new CampHistoryResponse(
                Year: edition.Year,
                CampId: edition.CampId,
                CampName: edition.Camp?.Name ?? string.Empty,
                Location: edition.Camp?.Location,
                Latitude: edition.Camp?.Latitude,
                Longitude: edition.Camp?.Longitude,
                EditionNumber: visitsSoFar[edition.CampId],
                TotalEditionsAtVenue: totalsAtVenue[edition.CampId],
                PhotoCount: summary?.PhotoCount ?? 0,
                PreviewPhotos: previews));
        }

        return history;
    }
}

using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for CampHistoryService.GetHistoryAsync()
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class CampHistoryServiceTests
{
    private readonly ICampEditionsRepository _editionsRepository;
    private readonly IMediaItemsRepository _mediaRepository;
    private readonly CampHistoryService _sut;

    private static readonly Guid EspinosaId = Guid.NewGuid();
    private static readonly Guid PalancaresId = Guid.NewGuid();

    public CampHistoryServiceTests()
    {
        _editionsRepository = Substitute.For<ICampEditionsRepository>();
        _mediaRepository = Substitute.For<IMediaItemsRepository>();

        _mediaRepository
            .GetYearSummariesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _sut = new CampHistoryService(_editionsRepository, _mediaRepository);
    }

    // ---------------------------------------------------------------------------
    // Helper factories
    // ---------------------------------------------------------------------------

    private static CampEdition BuildEdition(Guid campId, string campName, int year) =>
        new()
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            Year = year,
            StartDate = new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(year, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            Status = CampEditionStatus.Completed,
            Camp = new Camp
            {
                Id = campId,
                Name = campName,
                Location = "Burgos",
                Latitude = 43.077348m,
                Longitude = -3.552172m,
                IsActive = true
            }
        };

    private void GivenEditions(params CampEdition[] editions) =>
        _editionsRepository
            .GetByStatusAndYearAsync(CampEditionStatus.Completed, null, Arg.Any<CancellationToken>())
            .Returns([.. editions]);

    private void GivenPhotoSummaries(params MediaItemYearSummary[] summaries) =>
        _mediaRepository
            .GetYearSummariesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(summaries);

    // ---------------------------------------------------------------------------
    // Ordering and venue counters
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_OrdersEditionsByYearAscending()
    {
        GivenEditions(
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 2003),
            BuildEdition(PalancaresId, "Los Palancares", 1987),
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 1983));

        var history = await _sut.GetHistoryAsync();

        history.Select(h => h.Year).Should().ContainInOrder(1983, 1987, 2003);
    }

    [Fact]
    public async Task GetHistoryAsync_CountsEditionNumberPerVenueUpToThatYear()
    {
        GivenEditions(
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 1983),
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 1993),
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 2003),
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 2015));

        var history = await _sut.GetHistoryAsync();

        history.Select(h => h.EditionNumber).Should().ContainInOrder(1, 2, 3, 4);
    }

    [Fact]
    public async Task GetHistoryAsync_CountsEditionNumberIndependentlyPerVenue()
    {
        GivenEditions(
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 1983),
            BuildEdition(PalancaresId, "Los Palancares", 1987),
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 1993),
            BuildEdition(PalancaresId, "Los Palancares", 1994));

        var history = await _sut.GetHistoryAsync();

        history.Single(h => h.Year == 1994).EditionNumber.Should().Be(2);
        history.Single(h => h.Year == 1993).EditionNumber.Should().Be(2);
    }

    [Fact]
    public async Task GetHistoryAsync_ReportsTotalEditionsAtVenueOnEveryRow()
    {
        GivenEditions(
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 1983),
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 2015),
            BuildEdition(PalancaresId, "Los Palancares", 1987));

        var history = await _sut.GetHistoryAsync();

        history.Where(h => h.CampId == EspinosaId)
            .Should().OnlyContain(h => h.TotalEditionsAtVenue == 2);
        history.Single(h => h.CampId == PalancaresId)
            .TotalEditionsAtVenue.Should().Be(1);
    }

    [Fact]
    public async Task GetHistoryAsync_OnlyRequestsCompletedEditions()
    {
        GivenEditions(BuildEdition(EspinosaId, "Espinosa de los Monteros", 1983));

        await _sut.GetHistoryAsync();

        await _editionsRepository.Received(1).GetByStatusAndYearAsync(
            CampEditionStatus.Completed, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHistoryAsync_MapsVenueNameLocationAndCoordinates()
    {
        GivenEditions(BuildEdition(EspinosaId, "Espinosa de los Monteros", 2015));

        var row = (await _sut.GetHistoryAsync()).Single();

        row.CampName.Should().Be("Espinosa de los Monteros");
        row.Location.Should().Be("Burgos");
        row.Latitude.Should().Be(43.077348m);
        row.Longitude.Should().Be(-3.552172m);
    }

    // ---------------------------------------------------------------------------
    // Photos
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_AttachesPhotoCountForTheMatchingYear()
    {
        GivenEditions(
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 2003),
            BuildEdition(PalancaresId, "Los Palancares", 1987));
        GivenPhotoSummaries(new MediaItemYearSummary(2003, 37, []));

        var history = await _sut.GetHistoryAsync();

        history.Single(h => h.Year == 2003).PhotoCount.Should().Be(37);
        history.Single(h => h.Year == 1987).PhotoCount.Should().Be(0);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmptyPreviewListNeverNullForYearsWithoutPhotos()
    {
        GivenEditions(BuildEdition(PalancaresId, "Los Palancares", 1987));

        var row = (await _sut.GetHistoryAsync()).Single();

        row.PreviewPhotos.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_MapsPreviewPhotos()
    {
        var photoId = Guid.NewGuid();
        GivenEditions(BuildEdition(EspinosaId, "Espinosa de los Monteros", 2003));
        GivenPhotoSummaries(new MediaItemYearSummary(2003, 1,
        [
            new MediaItemPreview(photoId, "https://blob/thumb.jpg", "https://blob/full.jpg", "Llegada")
        ]));

        var preview = (await _sut.GetHistoryAsync()).Single().PreviewPhotos.Single();

        preview.Id.Should().Be(photoId);
        preview.ThumbnailUrl.Should().Be("https://blob/thumb.jpg");
        preview.Title.Should().Be("Llegada");
    }

    [Fact]
    public async Task GetHistoryAsync_FallsBackToFullImageWhenThumbnailIsMissing()
    {
        GivenEditions(BuildEdition(EspinosaId, "Espinosa de los Monteros", 2003));
        GivenPhotoSummaries(new MediaItemYearSummary(2003, 1,
        [
            new MediaItemPreview(Guid.NewGuid(), null, "https://blob/full.jpg", "Llegada")
        ]));

        var preview = (await _sut.GetHistoryAsync()).Single().PreviewPhotos.Single();

        preview.ThumbnailUrl.Should().Be("https://blob/full.jpg");
    }

    [Fact]
    public async Task GetHistoryAsync_AsksForAtMostThreePreviewsPerEditionInTheAnniversaryContext()
    {
        GivenEditions(BuildEdition(EspinosaId, "Espinosa de los Monteros", 2003));

        await _sut.GetHistoryAsync();

        await _mediaRepository.Received(1).GetYearSummariesAsync(
            CampHistoryService.AnniversaryContext, 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHistoryAsync_AggregatesPhotosWithoutQueryingPerEdition()
    {
        GivenEditions(
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 1983),
            BuildEdition(EspinosaId, "Espinosa de los Monteros", 1993),
            BuildEdition(PalancaresId, "Los Palancares", 1987),
            BuildEdition(PalancaresId, "Los Palancares", 1994));

        await _sut.GetHistoryAsync();

        // One rollup call for the whole history, not one per edition (N+1 guard).
        await _mediaRepository.Received(1).GetYearSummariesAsync(
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmptyListWhenThereAreNoCompletedEditions()
    {
        GivenEditions();

        var history = await _sut.GetHistoryAsync();

        history.Should().BeEmpty();
    }
}

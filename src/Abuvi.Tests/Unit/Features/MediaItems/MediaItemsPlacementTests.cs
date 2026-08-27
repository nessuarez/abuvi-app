using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.MediaSources;
using Abuvi.API.Features.MediaThemes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.MediaItems;

/// <summary>
/// Placement and provenance on upload.
///
/// The load-bearing rule here is that uploading WITHOUT knowing the edition or the year
/// must succeed. That is not a lenient fallback — it is the flow that fills the unplaced
/// pile, which is what collaborative dating works on. If these tests ever start demanding
/// a year, the feature has quietly lost its engine.
/// </summary>
public class MediaItemsPlacementTests
{
    private readonly IMediaItemsRepository _repository = Substitute.For<IMediaItemsRepository>();
    private readonly ICampEditionsRepository _editions = Substitute.For<ICampEditionsRepository>();
    private readonly IMediaSourcesRepository _sources = Substitute.For<IMediaSourcesRepository>();
    private readonly IMediaThemesRepository _themes = Substitute.For<IMediaThemesRepository>();
    private readonly MediaItemsService _service;

    private static readonly Guid UserId = Guid.NewGuid();

    public MediaItemsPlacementTests()
    {
        var options = Substitute.For<IOptions<BlobStorageOptions>>();
        options.Value.Returns(new BlobStorageOptions { PublicBaseUrl = "https://media.test" });

        _service = new MediaItemsService(
            _repository, _editions, _sources, _themes,
            Substitute.For<IBlobStorageService>(), options,
            Substitute.For<ILogger<MediaItemsService>>());
    }

    private static CreateMediaItemRequest Request(
        int? year = null,
        Guid? campEditionId = null,
        MediaItemType type = MediaItemType.Photo,
        IReadOnlyList<Guid>? themeIds = null,
        Guid? mediaSourceId = null,
        NewMediaSourceRequest? newSource = null,
        string? sourcePath = null) =>
        new(
            "https://media.test/file.jpg", null, type, "Title", null, year,
            null, null, "anniversary-50", null, null,
            campEditionId, themeIds, mediaSourceId, newSource, sourcePath);

    private static CampEdition Edition(int year, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CampId = Guid.NewGuid(),
        Year = year,
        StartDate = new DateTime(year, 7, 1),
        EndDate = new DateTime(year, 7, 15)
    };

    private MediaItem CapturedItem()
    {
        var call = _repository.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(IMediaItemsRepository.AddAsync));
        return (MediaItem)call.GetArguments()[0]!;
    }

    // ── Placement ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithNoEditionAndNoYear_SucceedsAndLandsUnplaced()
    {
        await _service.CreateAsync(UserId, Request(), CancellationToken.None);

        var item = CapturedItem();
        item.CampEditionId.Should().BeNull("an unknown edition is a valid, expected outcome");
        item.Year.Should().BeNull();
        item.YearSource.Should().Be(MediaItemYearSource.Unknown);
    }

    [Theory]
    [InlineData(MediaItemType.Photo)]
    [InlineData(MediaItemType.Audio)]
    [InlineData(MediaItemType.Video)]
    [InlineData(MediaItemType.Document)]
    [InlineData(MediaItemType.Interview)]
    public async Task CreateAsync_WithNoEdition_SucceedsForEveryMediaType(MediaItemType type)
    {
        await _service.CreateAsync(UserId, Request(type: type), CancellationToken.None);

        CapturedItem().Type.Should().Be(type);
    }

    [Fact]
    public async Task CreateAsync_WithExplicitEdition_DerivesYearFromTheEdition()
    {
        var edition = Edition(1998);
        _editions.GetByIdAsync(edition.Id, Arg.Any<CancellationToken>()).Returns(edition);

        await _service.CreateAsync(
            UserId, Request(campEditionId: edition.Id), CancellationToken.None);

        var item = CapturedItem();
        item.CampEditionId.Should().Be(edition.Id);
        item.Year.Should().Be(1998);
        item.Decade.Should().Be("90s");
        item.YearSource.Should().Be(MediaItemYearSource.Uploader);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownEdition_Throws()
    {
        var missing = Guid.NewGuid();
        _editions.GetByIdAsync(missing, Arg.Any<CancellationToken>()).Returns((CampEdition?)null);

        var act = () => _service.CreateAsync(
            UserId, Request(campEditionId: missing), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_WithYearMatchingExactlyOneEdition_ResolvesThatEdition()
    {
        var edition = Edition(2003);
        _editions.GetByYearAsync(2003, Arg.Any<CancellationToken>()).Returns([edition]);

        await _service.CreateAsync(UserId, Request(year: 2003), CancellationToken.None);

        var item = CapturedItem();
        item.CampEditionId.Should().Be(edition.Id);
        item.YearSource.Should().Be(MediaItemYearSource.Uploader);
    }

    [Fact]
    public async Task CreateAsync_WithAmbiguousYear_KeepsTheYearButStaysUnplaced()
    {
        // Two editions in one year: the year no longer determines the edition, so guessing
        // would be worse than leaving it for the community.
        _editions.GetByYearAsync(2003, Arg.Any<CancellationToken>())
            .Returns([Edition(2003), Edition(2003)]);

        await _service.CreateAsync(UserId, Request(year: 2003), CancellationToken.None);

        var item = CapturedItem();
        item.CampEditionId.Should().BeNull();
        item.Year.Should().Be(2003);
    }

    [Fact]
    public async Task CreateAsync_WithYearMatchingNoEdition_StaysUnplaced()
    {
        _editions.GetByYearAsync(1999, Arg.Any<CancellationToken>()).Returns([]);

        await _service.CreateAsync(UserId, Request(year: 1999), CancellationToken.None);

        CapturedItem().CampEditionId.Should().BeNull();
    }

    // ── Provenance ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithNewSource_CreatesExactlyOneSourceAndLinksIt()
    {
        var request = Request(newSource: new NewMediaSourceRequest(
            "  Manolo García  ", null, "manolo@example.com", "pendrive", null));

        await _service.CreateAsync(UserId, request, CancellationToken.None);

        await _sources.Received(1).AddAsync(Arg.Any<MediaSource>(), Arg.Any<CancellationToken>());

        var source = (MediaSource)_sources.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(IMediaSourcesRepository.AddAsync))
            .GetArguments()[0]!;

        source.ContributorName.Should().Be("Manolo García", "the name is trimmed on the way in");
        source.RegisteredByUserId.Should().Be(UserId);
        CapturedItem().MediaSourceId.Should().Be(source.Id);
    }

    [Fact]
    public async Task CreateAsync_WithExistingSource_LinksItWithoutCreatingAnother()
    {
        var sourceId = Guid.NewGuid();

        await _service.CreateAsync(
            UserId, Request(mediaSourceId: sourceId), CancellationToken.None);

        await _sources.DidNotReceive().AddAsync(Arg.Any<MediaSource>(), Arg.Any<CancellationToken>());
        CapturedItem().MediaSourceId.Should().Be(sourceId);
    }

    [Fact]
    public async Task CreateAsync_WithBothSourceIdAndNewSource_ThrowsValidation()
    {
        var request = Request(
            mediaSourceId: Guid.NewGuid(),
            newSource: new NewMediaSourceRequest("Manolo", null, null, null, null));

        var act = () => _service.CreateAsync(UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _repository.DidNotReceive().AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithNoSource_LeavesItNullMeaningUploaderIsTheProvider()
    {
        await _service.CreateAsync(UserId, Request(), CancellationToken.None);

        CapturedItem().MediaSourceId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_StoresSourcePathVerbatim()
    {
        // Trimming for display happens in the mapper; the stored value is evidence.
        const string path = "Fotos papa/Verano 98/Selva de Oza/IMG_0231.jpg";

        await _service.CreateAsync(UserId, Request(sourcePath: path), CancellationToken.None);

        CapturedItem().SourcePath.Should().Be(path);
    }

    // ── Themes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithThemes_AttachesOnlyKnownActiveOnes()
    {
        var active = new MediaTheme { Id = Guid.NewGuid(), Name = "San Abuvino", Slug = "san-abuvino", IsActive = true };
        var inactive = new MediaTheme { Id = Guid.NewGuid(), Name = "Retirado", Slug = "retirado", IsActive = false };
        var unknown = Guid.NewGuid();

        _themes.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([active, inactive]);

        await _service.CreateAsync(
            UserId, Request(themeIds: [active.Id, inactive.Id, unknown]), CancellationToken.None);

        var tags = (IReadOnlyList<MediaItemTheme>)_themes.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(IMediaThemesRepository.AttachManyAsync))
            .GetArguments()[0]!;

        tags.Should().ContainSingle().Which.MediaThemeId.Should().Be(active.Id);
    }

    [Fact]
    public async Task CreateAsync_WithOnlyUnknownThemes_StillCreatesTheItem()
    {
        // Losing a photo because a theme id went stale would be a bad trade.
        _themes.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _service.CreateAsync(
            UserId, Request(themeIds: [Guid.NewGuid()]), CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>());
    }
}

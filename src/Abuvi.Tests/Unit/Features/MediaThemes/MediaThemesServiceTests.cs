using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.MediaThemes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.MediaThemes;

/// <summary>
/// Themes are the cross-cutting axis: "San Abuvino" spans many editions, so a theme's
/// year span is what makes that visible. The slug is the URL identity, so its generation
/// and collision handling have to be deterministic.
/// </summary>
public class MediaThemesServiceTests
{
    private readonly IMediaThemesRepository _repository = Substitute.For<IMediaThemesRepository>();
    private readonly IMediaItemsRepository _items = Substitute.For<IMediaItemsRepository>();
    private readonly MediaThemesService _service;

    public MediaThemesServiceTests()
    {
        _service = new MediaThemesService(
            _repository, _items, Substitute.For<ILogger<MediaThemesService>>());
    }

    // ── Slug generation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("San Abuvino", "san-abuvino")]
    [InlineData("Cocina y comedor", "cocina-y-comedor")]
    [InlineData("Montaje y desmontaje", "montaje-y-desmontaje")]
    [InlineData("Excursiones", "excursiones")]
    [InlineData("  Juegos   de  noche  ", "juegos-de-noche")]
    [InlineData("Niños y niñas", "ninos-y-ninas")]
    [InlineData("Año 1998", "ano-1998")]
    [InlineData("¡Fiesta!", "fiesta")]
    public void Slugify_StripsAccentsAndCollapsesSeparators(string name, string expected)
    {
        MediaThemesService.Slugify(name).Should().Be(expected);
    }

    [Fact]
    public void Slugify_WithNothingUsable_FallsBackToAStableValue()
    {
        MediaThemesService.Slugify("!!!").Should().Be("tema");
    }

    [Fact]
    public async Task Create_OnSlugCollision_AppendsANumericSuffix()
    {
        _repository.SlugExistsAsync("san-abuvino", Arg.Any<CancellationToken>()).Returns(true);
        _repository.SlugExistsAsync("san-abuvino-2", Arg.Any<CancellationToken>()).Returns(true);
        _repository.SlugExistsAsync("san-abuvino-3", Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.CreateAsync(
            new CreateMediaThemeRequest("San Abuvino", null), CancellationToken.None);

        result.Slug.Should().Be("san-abuvino-3");
    }

    [Fact]
    public async Task Create_TrimsTheDisplayName()
    {
        _repository.SlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.CreateAsync(
            new CreateMediaThemeRequest("  Actuaciones  ", null), CancellationToken.None);

        result.Name.Should().Be("Actuaciones");
        result.Slug.Should().Be("actuaciones");
    }

    [Fact]
    public async Task Update_LeavesTheSlugAlone()
    {
        var theme = new MediaTheme
        {
            Id = Guid.NewGuid(), Name = "San Abuvino", Slug = "san-abuvino", IsActive = true
        };
        _repository.GetByIdAsync(theme.Id, Arg.Any<CancellationToken>()).Returns(theme);
        _repository.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, ThemeStats>());

        await _service.UpdateAsync(
            theme.Id, new UpdateMediaThemeRequest("Fiestas de San Abuvino", null, true),
            CancellationToken.None);

        theme.Name.Should().Be("Fiestas de San Abuvino");
        theme.Slug.Should().Be("san-abuvino", "renaming must not break links that already exist");
    }

    // ── Deleting ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithTaggedItems_ThrowsUnlessForced()
    {
        var theme = new MediaTheme { Id = Guid.NewGuid(), Name = "San Abuvino", Slug = "san-abuvino" };
        _repository.GetByIdAsync(theme.Id, Arg.Any<CancellationToken>()).Returns(theme);
        _repository.CountItemsAsync(theme.Id, Arg.Any<CancellationToken>()).Returns(42);

        var act = () => _service.DeleteAsync(theme.Id, force: false, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        await _repository.DidNotReceive().DeleteAsync(
            Arg.Any<MediaTheme>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WithForce_RemovesTheTheme()
    {
        var theme = new MediaTheme { Id = Guid.NewGuid(), Name = "San Abuvino", Slug = "san-abuvino" };
        _repository.GetByIdAsync(theme.Id, Arg.Any<CancellationToken>()).Returns(theme);
        _repository.CountItemsAsync(theme.Id, Arg.Any<CancellationToken>()).Returns(42);

        await _service.DeleteAsync(theme.Id, force: true, CancellationToken.None);

        await _repository.Received(1).DeleteAsync(theme, Arg.Any<CancellationToken>());
    }

    // ── Tagging ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Attach_WhenAlreadyTagged_IsANoOp()
    {
        var itemId = Guid.NewGuid();
        var theme = new MediaTheme { Id = Guid.NewGuid(), Name = "San Abuvino", Slug = "san-abuvino" };

        _items.GetByIdAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(new MediaItem { Id = itemId });
        _repository.GetByIdAsync(theme.Id, Arg.Any<CancellationToken>()).Returns(theme);
        _repository.IsAttachedAsync(itemId, theme.Id, Arg.Any<CancellationToken>()).Returns(true);

        await _service.AttachAsync(itemId, theme.Id, Guid.NewGuid(), CancellationToken.None);

        await _repository.DidNotReceive().AttachAsync(
            Arg.Any<MediaItemTheme>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Detach_ByTheTagger_IsAllowed()
    {
        var tagger = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var themeId = Guid.NewGuid();
        var tag = new MediaItemTheme
        {
            MediaItemId = itemId, MediaThemeId = themeId, TaggedByUserId = tagger
        };
        _repository.GetTagAsync(itemId, themeId, Arg.Any<CancellationToken>()).Returns(tag);

        var detached = await _service.DetachAsync(
            itemId, themeId, tagger, isAdminOrBoard: false, CancellationToken.None);

        detached.Should().BeTrue();
        await _repository.Received(1).DetachAsync(tag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Detach_ByAnUnrelatedMember_IsRefused()
    {
        var itemId = Guid.NewGuid();
        var themeId = Guid.NewGuid();
        var tag = new MediaItemTheme
        {
            MediaItemId = itemId, MediaThemeId = themeId, TaggedByUserId = Guid.NewGuid()
        };
        _repository.GetTagAsync(itemId, themeId, Arg.Any<CancellationToken>()).Returns(tag);

        var detached = await _service.DetachAsync(
            itemId, themeId, Guid.NewGuid(), isAdminOrBoard: false, CancellationToken.None);

        detached.Should().BeFalse();
        await _repository.DidNotReceive().DetachAsync(
            Arg.Any<MediaItemTheme>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Detach_ByModerator_IsAllowed()
    {
        var itemId = Guid.NewGuid();
        var themeId = Guid.NewGuid();
        var tag = new MediaItemTheme
        {
            MediaItemId = itemId, MediaThemeId = themeId, TaggedByUserId = Guid.NewGuid()
        };
        _repository.GetTagAsync(itemId, themeId, Arg.Any<CancellationToken>()).Returns(tag);

        var detached = await _service.DetachAsync(
            itemId, themeId, Guid.NewGuid(), isAdminOrBoard: true, CancellationToken.None);

        detached.Should().BeTrue();
    }

    // ── Year span ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCatalogue_ExposesTheYearSpanThatMakesAThemeCrossCutting()
    {
        var theme = new MediaTheme
        {
            Id = Guid.NewGuid(), Name = "San Abuvino", Slug = "san-abuvino", IsActive = true
        };
        _repository.GetAllAsync(false, Arg.Any<CancellationToken>()).Returns([theme]);
        _repository.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(
            new Dictionary<Guid, ThemeStats> { [theme.Id] = new(37, 1981, 2019, 4) });

        var catalogue = await _service.GetCatalogueAsync(false, CancellationToken.None);

        var result = catalogue.Single();
        result.FirstYear.Should().Be(1981);
        result.LastYear.Should().Be(2019);
        result.ItemCount.Should().Be(37);
        result.UndatedCount.Should().Be(4);
    }

    [Fact]
    public async Task GetCatalogue_ForAThemeWithNoItems_ReportsEmptyStats()
    {
        var theme = new MediaTheme
        {
            Id = Guid.NewGuid(), Name = "Talleres", Slug = "talleres", IsActive = true
        };
        _repository.GetAllAsync(false, Arg.Any<CancellationToken>()).Returns([theme]);
        _repository.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, ThemeStats>());

        var result = (await _service.GetCatalogueAsync(false, CancellationToken.None)).Single();

        result.ItemCount.Should().Be(0);
        result.FirstYear.Should().BeNull();
    }
}

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Abuvi.API.Features.MediaThemes;
using Abuvi.API.Features.MediaSources;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AccommodationMediaServiceTests
{
    // ── MediaItemsService — Zone media ───────────────────────────────────────

    private static IMediaItemsRepository MockMediaRepo() => Substitute.For<IMediaItemsRepository>();
    private static IBlobStorageService MockBlob() => Substitute.For<IBlobStorageService>();

    private static MediaItemsService BuildService(IMediaItemsRepository repo)
    {
        var blob = MockBlob();
        var blobOpts = Microsoft.Extensions.Options.Options.Create(
            new BlobStorageOptions { PublicBaseUrl = "https://storage.example.com" });
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<MediaItemsService>>();
        return new MediaItemsService(
            repo,
            Substitute.For<ICampEditionsRepository>(),
            Substitute.For<IMediaSourcesRepository>(),
            Substitute.For<IMediaThemesRepository>(),
            blob, blobOpts, logger);
    }

    private static AddAccommodationMediaRequest MediaRequest(
        string fileUrl = "https://storage.example.com/accommodation-media/photo.jpg",
        string? thumbnailUrl = null,
        string? description = null,
        int displayOrder = 0)
        => new(fileUrl, thumbnailUrl, description, displayOrder);

    private static MediaItem CreateMediaItem(Guid? id = null, Guid? zoneId = null, Guid? accommodationId = null, bool isPrimary = false) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            UploadedByUserId = Guid.NewGuid(),
            FileUrl = "https://storage.example.com/file.jpg",
            Type = MediaItemType.Photo,
            Title = string.Empty,
            ZoneId = zoneId,
            AccommodationId = accommodationId,
            IsPrimary = isPrimary,
            IsApproved = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    #region Zone Media — AddToZoneAsync

    [Fact]
    public async Task AddToZone_WhenUnderLimit_CreatesMediaItem()
    {
        var repo = MockMediaRepo();
        var zoneId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sut = BuildService(repo);

        repo.CountByZoneAsync(zoneId, Arg.Any<CancellationToken>()).Returns(3);
        repo.AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.AddToZoneAsync(userId, zoneId, MediaRequest(), CancellationToken.None);

        result.Should().NotBeNull();
        result.ZoneId.Should().Be(zoneId);
        await repo.Received(1).AddAsync(Arg.Is<MediaItem>(m => m.ZoneId == zoneId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddToZone_WhenFirstItem_SetsPrimary()
    {
        var repo = MockMediaRepo();
        var zoneId = Guid.NewGuid();
        var sut = BuildService(repo);

        repo.CountByZoneAsync(zoneId, Arg.Any<CancellationToken>()).Returns(0);
        repo.AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.AddToZoneAsync(Guid.NewGuid(), zoneId, MediaRequest(), CancellationToken.None);

        result.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task AddToZone_WhenNotFirstItem_DoesNotSetPrimary()
    {
        var repo = MockMediaRepo();
        var zoneId = Guid.NewGuid();
        var sut = BuildService(repo);

        repo.CountByZoneAsync(zoneId, Arg.Any<CancellationToken>()).Returns(2);
        repo.AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.AddToZoneAsync(Guid.NewGuid(), zoneId, MediaRequest(), CancellationToken.None);

        result.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public async Task AddToZone_WhenExceedsMaxItems_ThrowsBusinessRuleException()
    {
        var repo = MockMediaRepo();
        var zoneId = Guid.NewGuid();
        var sut = BuildService(repo);

        repo.CountByZoneAsync(zoneId, Arg.Any<CancellationToken>()).Returns(10);

        var act = () => sut.AddToZoneAsync(Guid.NewGuid(), zoneId, MediaRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    #endregion

    #region Zone Media — SetPrimaryForZoneAsync

    [Fact]
    public async Task SetPrimaryForZone_WhenItemExists_ClearsPreviousAndSetsNew()
    {
        var repo = MockMediaRepo();
        var zoneId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var sut = BuildService(repo);
        var item = CreateMediaItem(id: mediaId, zoneId: zoneId);

        repo.GetByIdAsync(mediaId, Arg.Any<CancellationToken>()).Returns(item);
        repo.ClearPrimaryForZoneAsync(zoneId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        repo.UpdateAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.SetPrimaryForZoneAsync(zoneId, mediaId, CancellationToken.None);

        result.IsPrimary.Should().BeTrue();
        await repo.Received(1).ClearPrimaryForZoneAsync(zoneId, Arg.Any<CancellationToken>());
        await repo.Received(1).UpdateAsync(Arg.Is<MediaItem>(m => m.IsPrimary), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPrimaryForZone_WhenItemNotFound_ThrowsNotFoundException()
    {
        var repo = MockMediaRepo();
        var sut = BuildService(repo);

        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MediaItem?)null);

        var act = () => sut.SetPrimaryForZoneAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetPrimaryForZone_WhenItemBelongsToDifferentZone_ThrowsNotFoundException()
    {
        var repo = MockMediaRepo();
        var sut = BuildService(repo);
        var item = CreateMediaItem(zoneId: Guid.NewGuid());

        repo.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var act = () => sut.SetPrimaryForZoneAsync(Guid.NewGuid(), item.Id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Zone Media — DeleteZoneMediaAsync

    [Fact]
    public async Task DeleteZoneMedia_WhenExists_RemovesRecord()
    {
        var repo = MockMediaRepo();
        var zoneId = Guid.NewGuid();
        var sut = BuildService(repo);
        var item = CreateMediaItem(zoneId: zoneId);

        repo.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        repo.DeleteAsync(item, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await sut.DeleteZoneMediaAsync(zoneId, item.Id, CancellationToken.None);

        await repo.Received(1).DeleteAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteZoneMedia_WhenNotFound_ThrowsNotFoundException()
    {
        var repo = MockMediaRepo();
        var sut = BuildService(repo);

        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MediaItem?)null);

        var act = () => sut.DeleteZoneMediaAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteZoneMedia_DoesNotDeleteBlobs()
    {
        var repo = MockMediaRepo();
        var blob = MockBlob();
        var blobOpts = Microsoft.Extensions.Options.Options.Create(
            new BlobStorageOptions { PublicBaseUrl = "https://storage.example.com" });
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<MediaItemsService>>();
        var sut = new MediaItemsService(
            repo,
            Substitute.For<ICampEditionsRepository>(),
            Substitute.For<IMediaSourcesRepository>(),
            Substitute.For<IMediaThemesRepository>(),
            blob, blobOpts, logger);

        var zoneId = Guid.NewGuid();
        var item = CreateMediaItem(zoneId: zoneId);

        repo.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        repo.DeleteAsync(item, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await sut.DeleteZoneMediaAsync(zoneId, item.Id, CancellationToken.None);

        await blob.DidNotReceiveWithAnyArgs().DeleteManyAsync(default!, default);
    }

    #endregion

    #region Accommodation Media — AddToAccommodationAsync

    [Fact]
    public async Task AddToAccommodation_WhenUnderLimit_CreatesMediaItem()
    {
        var repo = MockMediaRepo();
        var accommodationId = Guid.NewGuid();
        var sut = BuildService(repo);

        repo.CountByAccommodationAsync(accommodationId, Arg.Any<CancellationToken>()).Returns(2);
        repo.AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.AddToAccommodationAsync(Guid.NewGuid(), accommodationId, MediaRequest(), CancellationToken.None);

        result.AccommodationId.Should().Be(accommodationId);
        await repo.Received(1).AddAsync(
            Arg.Is<MediaItem>(m => m.AccommodationId == accommodationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddToAccommodation_WhenExceedsMaxItems_ThrowsBusinessRuleException()
    {
        var repo = MockMediaRepo();
        var accommodationId = Guid.NewGuid();
        var sut = BuildService(repo);

        repo.CountByAccommodationAsync(accommodationId, Arg.Any<CancellationToken>()).Returns(10);

        var act = () => sut.AddToAccommodationAsync(Guid.NewGuid(), accommodationId, MediaRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    #endregion

    #region Accommodation Media — SetPrimaryForAccommodationAsync

    [Fact]
    public async Task SetPrimaryForAccommodation_WhenItemExists_ClearsPreviousAndSetsNew()
    {
        var repo = MockMediaRepo();
        var accommodationId = Guid.NewGuid();
        var sut = BuildService(repo);
        var item = CreateMediaItem(accommodationId: accommodationId);

        repo.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        repo.ClearPrimaryForAccommodationAsync(accommodationId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        repo.UpdateAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.SetPrimaryForAccommodationAsync(accommodationId, item.Id, CancellationToken.None);

        result.IsPrimary.Should().BeTrue();
        await repo.Received(1).ClearPrimaryForAccommodationAsync(accommodationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPrimaryForAccommodation_WhenItemNotFound_ThrowsNotFoundException()
    {
        var repo = MockMediaRepo();
        var sut = BuildService(repo);

        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MediaItem?)null);

        var act = () => sut.SetPrimaryForAccommodationAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Accommodation Media — DeleteAccommodationMediaAsync

    [Fact]
    public async Task DeleteAccommodationMedia_WhenExists_RemovesRecord()
    {
        var repo = MockMediaRepo();
        var accommodationId = Guid.NewGuid();
        var sut = BuildService(repo);
        var item = CreateMediaItem(accommodationId: accommodationId);

        repo.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        repo.DeleteAsync(item, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await sut.DeleteAccommodationMediaAsync(accommodationId, item.Id, CancellationToken.None);

        await repo.Received(1).DeleteAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccommodationMedia_WhenNotFound_ThrowsNotFoundException()
    {
        var repo = MockMediaRepo();
        var sut = BuildService(repo);

        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MediaItem?)null);

        var act = () => sut.DeleteAccommodationMediaAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion
}

// ── AccommodationTypeMediaService tests ──────────────────────────────────────

public class AccommodationTypeMediaServiceTests
{
    private static IAccommodationTypeMediaRepository MockRepo() =>
        Substitute.For<IAccommodationTypeMediaRepository>();

    private static AccommodationTypeMediaService BuildService(IAccommodationTypeMediaRepository repo) =>
        new(repo);

    private static AddAccommodationMediaRequest MediaRequest() =>
        new("https://storage.example.com/accommodation-media/photo.jpg", null, null, 0);

    private static AccommodationTypeMedia CreateTypeMedia(Guid? id = null, AccommodationType type = AccommodationType.Lodge, bool isPrimary = false) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            AccommodationType = type,
            FileUrl = "https://storage.example.com/file.jpg",
            IsPrimary = isPrimary,
            UploadedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    #region AddAsync

    [Fact]
    public async Task AddTypeDefault_WhenUnderLimit_ReturnsCreated()
    {
        var repo = MockRepo();
        var sut = BuildService(repo);
        var type = AccommodationType.Lodge;

        repo.CountByTypeAsync(type, Arg.Any<CancellationToken>()).Returns(2);
        repo.AddAsync(Arg.Any<AccommodationTypeMedia>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.AddAsync(Guid.NewGuid(), type, MediaRequest(), CancellationToken.None);

        result.Should().NotBeNull();
        result.AccommodationType.Should().Be(type.ToString());
        await repo.Received(1).AddAsync(
            Arg.Is<AccommodationTypeMedia>(m => m.AccommodationType == type),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddTypeDefault_WhenFirstItem_SetsPrimary()
    {
        var repo = MockRepo();
        var sut = BuildService(repo);

        repo.CountByTypeAsync(AccommodationType.Tent, Arg.Any<CancellationToken>()).Returns(0);
        repo.AddAsync(Arg.Any<AccommodationTypeMedia>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.AddAsync(Guid.NewGuid(), AccommodationType.Tent, MediaRequest(), CancellationToken.None);

        result.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task AddTypeDefault_WhenExceedsMaxItems_ThrowsBusinessRuleException()
    {
        var repo = MockRepo();
        var sut = BuildService(repo);

        repo.CountByTypeAsync(AccommodationType.Caravan, Arg.Any<CancellationToken>()).Returns(10);

        var act = () => sut.AddAsync(Guid.NewGuid(), AccommodationType.Caravan, MediaRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    #endregion

    #region SetPrimaryAsync

    [Fact]
    public async Task SetPrimary_WhenItemExists_ClearsPreviousAndSetsNew()
    {
        var repo = MockRepo();
        var sut = BuildService(repo);
        var item = CreateTypeMedia(type: AccommodationType.Bungalow);

        repo.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        repo.ClearPrimaryForTypeAsync(item.AccommodationType, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        repo.UpdateAsync(Arg.Any<AccommodationTypeMedia>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await sut.SetPrimaryAsync(item.Id, CancellationToken.None);

        result.IsPrimary.Should().BeTrue();
        await repo.Received(1).ClearPrimaryForTypeAsync(item.AccommodationType, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPrimary_WhenItemNotFound_ThrowsNotFoundException()
    {
        var repo = MockRepo();
        var sut = BuildService(repo);

        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((AccommodationTypeMedia?)null);

        var act = () => sut.SetPrimaryAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteTypeMedia_WhenExists_RemovesRecord()
    {
        var repo = MockRepo();
        var sut = BuildService(repo);
        var item = CreateTypeMedia();

        repo.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        repo.DeleteAsync(item, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await sut.DeleteAsync(item.Id, CancellationToken.None);

        await repo.Received(1).DeleteAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTypeMedia_WhenNotFound_ThrowsNotFoundException()
    {
        var repo = MockRepo();
        var sut = BuildService(repo);

        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((AccommodationTypeMedia?)null);

        var act = () => sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion
}

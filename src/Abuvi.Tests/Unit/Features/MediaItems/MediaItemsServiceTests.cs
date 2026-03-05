using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.MediaItems;

public class MediaItemsServiceTests
{
    private readonly IMediaItemsRepository _repository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<MediaItemsService> _logger;
    private readonly MediaItemsService _service;

    private const string PublicBaseUrl = "https://abuvi-media.fsn1.your-objectstorage.com";

    public MediaItemsServiceTests()
    {
        _repository = Substitute.For<IMediaItemsRepository>();
        _blobStorageService = Substitute.For<IBlobStorageService>();
        _logger = Substitute.For<ILogger<MediaItemsService>>();

        var options = Substitute.For<IOptions<BlobStorageOptions>>();
        options.Value.Returns(new BlobStorageOptions { PublicBaseUrl = PublicBaseUrl });

        _service = new MediaItemsService(_repository, _blobStorageService, options, _logger);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesItemWithDefaultFlags()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateMediaItemRequest(
            $"{PublicBaseUrl}/media-items/test.jpg",
            $"{PublicBaseUrl}/media-items/test_thumb.webp",
            MediaItemType.Photo, "Test Photo", "A description", 1985, null, null, "camp");

        // Act
        var result = await _service.CreateAsync(userId, request, CancellationToken.None);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.IsPublished.Should().BeFalse();
        result.UploadedByUserId.Should().Be(userId);

        await _repository.Received(1).AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithYear1985_DerivesDecadeAs80s()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateMediaItemRequest(
            "https://example.com/file.jpg", "https://example.com/thumb.webp",
            MediaItemType.Photo, "Title", null, 1985, null, null, null);

        // Act
        var result = await _service.CreateAsync(userId, request, CancellationToken.None);

        // Assert
        result.Decade.Should().Be("80s");
    }

    [Fact]
    public async Task CreateAsync_WithYearNull_DerivesDecadeAsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateMediaItemRequest(
            "https://example.com/file.jpg", null,
            MediaItemType.Audio, "Title", null, null, null, null, null);

        // Act
        var result = await _service.CreateAsync(userId, request, CancellationToken.None);

        // Assert
        result.Decade.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithAudioType_AcceptsNullThumbnail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateMediaItemRequest(
            "https://example.com/file.mp3", null,
            MediaItemType.Audio, "Audio Recording", null, 2000, null, null, null);

        // Act
        var result = await _service.CreateAsync(userId, request, CancellationToken.None);

        // Assert
        result.ThumbnailUrl.Should().BeNull();
        result.Type.Should().Be("Audio");
    }

    [Fact]
    public async Task ApproveAsync_WithExistingId_SetsBothFlagsTrue()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = CreateTestMediaItem(itemId);

        _repository.GetByIdAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(item);

        // Act
        var result = await _service.ApproveAsync(itemId, CancellationToken.None);

        // Assert
        result.IsApproved.Should().BeTrue();
        result.IsPublished.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectAsync_WithExistingId_SetsBothFlagsFalse()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = CreateTestMediaItem(itemId);
        item.IsApproved = true;
        item.IsPublished = true;

        _repository.GetByIdAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(item);

        // Act
        var result = await _service.RejectAsync(itemId, CancellationToken.None);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_DeletesItemAndBlob()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = CreateTestMediaItem(itemId);
        item.FileUrl = $"{PublicBaseUrl}/media-items/file.jpg";
        item.ThumbnailUrl = null;

        _repository.GetByIdAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(item);

        // Act
        await _service.DeleteAsync(itemId, CancellationToken.None);

        // Assert
        await _blobStorageService.Received(1).DeleteManyAsync(
            Arg.Is<IReadOnlyList<string>>(keys => keys.Count == 1),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).DeleteAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithThumbnail_DeletesBothBlobs()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = CreateTestMediaItem(itemId);
        item.FileUrl = $"{PublicBaseUrl}/media-items/file.jpg";
        item.ThumbnailUrl = $"{PublicBaseUrl}/media-items/file_thumb.webp";

        _repository.GetByIdAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(item);

        // Act
        await _service.DeleteAsync(itemId, CancellationToken.None);

        // Assert
        await _blobStorageService.Received(1).DeleteManyAsync(
            Arg.Is<IReadOnlyList<string>>(keys => keys.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        _repository.GetByIdAsync(itemId, Arg.Any<CancellationToken>())
            .Returns((MediaItem?)null);

        // Act & Assert
        await _service.Invoking(s => s.DeleteAsync(itemId, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetListAsync_WithContextFilter_DelegatesToRepository()
    {
        // Arrange
        _repository.GetListAsync(null, null, "camp", null, Arg.Any<CancellationToken>())
            .Returns(new List<MediaItem>());

        // Act
        var result = await _service.GetListAsync(null, null, "camp", null, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _repository.Received(1).GetListAsync(null, null, "camp", null, Arg.Any<CancellationToken>());
    }

    // Helper methods
    private static MediaItem CreateTestMediaItem(Guid id) => new()
    {
        Id = id,
        UploadedByUserId = Guid.NewGuid(),
        FileUrl = "https://example.com/file.jpg",
        ThumbnailUrl = "https://example.com/thumb.webp",
        Type = MediaItemType.Photo,
        Title = "Test Photo",
        Description = "A test photo",
        Year = 1990,
        Decade = "90s",
        IsApproved = false,
        IsPublished = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        UploadedBy = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            PasswordHash = "hash",
            Role = UserRole.Member,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };
}

namespace Abuvi.Tests.Unit.Features.BlobStorage;

using Abuvi.API.Features.BlobStorage;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

public class BlobStorageServiceTests
{
    private readonly IBlobStorageRepository _repo = Substitute.For<IBlobStorageRepository>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly BlobStorageOptions _options = new()
    {
        BucketName = "test-bucket",
        PublicBaseUrl = "https://cdn.example.com",
        MaxFileSizeBytes = 52_428_800,
        AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"],
        AllowedVideoExtensions = [".mp4"],
        AllowedAudioExtensions = [".mp3", ".wav"],
        AllowedDocumentExtensions = [".pdf"],
        ThumbnailWidthPx = 400,
        ThumbnailHeightPx = 400,
        StorageQuotaBytes = 107_374_182_400L, // 100 GB
        StorageWarningThresholdPct = 80,
        StorageCriticalThresholdPct = 95
    };

    private BlobStorageService CreateSut() =>
        new(_repo, Options.Create(_options), _cache);

    // ── Upload ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_WithValidImageAndThumbnailRequested_UploadsOriginalAndThumbnail()
    {
        // Arrange
        var sut = CreateSut();
        using var stream = CreateFakeJpegStream();

        // Act
        var result = await sut.UploadAsync(stream, "photo.jpg", "image/jpeg",
            "photos", Guid.NewGuid(), generateThumbnail: true, CancellationToken.None);

        // Assert
        result.FileUrl.Should().StartWith(_options.PublicBaseUrl);
        result.ThumbnailUrl.Should().NotBeNull();
        result.ThumbnailUrl!.Should().Contain("/thumbs/");
        result.ThumbnailUrl.Should().EndWith(".webp");
        result.ContentType.Should().Be("image/jpeg");
        await _repo.Received(2).UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()); // original + thumbnail
    }

    [Fact]
    public async Task UploadAsync_WithValidImageAndNoThumbnailRequested_UploadsOriginalOnly()
    {
        // Arrange
        var sut = CreateSut();
        using var stream = CreateFakeJpegStream();

        // Act
        var result = await sut.UploadAsync(stream, "photo.jpg", "image/jpeg",
            "photos", null, generateThumbnail: false, CancellationToken.None);

        // Assert
        result.ThumbnailUrl.Should().BeNull();
        await _repo.Received(1).UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WithAudioFile_UploadsWithoutThumbnailRegardlessOfFlag()
    {
        // Arrange
        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[1024]);

        // Act
        var result = await sut.UploadAsync(stream, "recording.mp3", "audio/mpeg",
            "media-items", null, generateThumbnail: true, CancellationToken.None);

        // Assert
        result.ThumbnailUrl.Should().BeNull("audio files never get thumbnails");
        await _repo.Received(1).UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WithDocumentFile_UploadsWithoutThumbnail()
    {
        // Arrange
        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[2048]);

        // Act
        var result = await sut.UploadAsync(stream, "document.pdf", "application/pdf",
            "media-items", null, generateThumbnail: true, CancellationToken.None);

        // Assert
        result.ThumbnailUrl.Should().BeNull();
    }

    [Fact]
    public async Task UploadAsync_InvalidatesStatsCache()
    {
        // Arrange
        var sut = CreateSut();
        _cache.Set("blob-storage-stats", new BlobStorageStats(0, 0, "0 B", null, null, null,
            new Dictionary<string, FolderStats>()));

        using var stream = new MemoryStream(new byte[1024]);

        // Act
        await sut.UploadAsync(stream, "file.mp3", "audio/mpeg",
            "media-items", null, false, CancellationToken.None);

        // Assert
        _cache.TryGetValue("blob-storage-stats", out _).Should().BeFalse(
            "upload should invalidate the stats cache");
    }

    [Fact]
    public async Task UploadAsync_WhenS3Throws_PropagatesException()
    {
        // Arrange
        _repo.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("S3 error"));
        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[1024]);

        // Act
        var act = async () => await sut.UploadAsync(stream, "file.mp3", "audio/mpeg",
            "media-items", null, false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteManyAsync_WithValidKeys_CallsRepositoryAndInvalidatesCache()
    {
        // Arrange
        var sut = CreateSut();
        var keys = new[] { "photos/abc/def.jpg", "photos/abc/thumbs/def.webp" };
        _cache.Set("blob-storage-stats", new BlobStorageStats(0, 0, "0 B", null, null, null,
            new Dictionary<string, FolderStats>()));

        // Act
        await sut.DeleteManyAsync(keys, CancellationToken.None);

        // Assert
        await _repo.Received(1).DeleteManyAsync(
            Arg.Is<IReadOnlyList<string>>(k => k.SequenceEqual(keys)),
            Arg.Any<CancellationToken>());
        _cache.TryGetValue("blob-storage-stats", out _).Should().BeFalse();
    }

    // ── Stats / Cache ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_WithQuotaConfigured_ReturnsUsedPctAndFreeBytes()
    {
        // Arrange
        var objects = new List<(string Key, long SizeBytes)>
        {
            ("photos/img1.jpg", 1_000_000L),
            ("photos/img2.jpg", 2_000_000L),
        };
        _repo.ListObjectsAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(string, long)>>(objects));
        var sut = CreateSut();

        // Act
        var stats = await sut.GetStatsAsync(CancellationToken.None);

        // Assert
        stats.TotalObjects.Should().Be(2);
        stats.TotalSizeBytes.Should().Be(3_000_000L);
        stats.QuotaBytes.Should().Be(_options.StorageQuotaBytes);
        stats.UsedPct.Should().NotBeNull();
        stats.FreeBytes.Should().Be(_options.StorageQuotaBytes - 3_000_000L);
    }

    [Fact]
    public async Task GetStatsAsync_WhenCalledTwiceWithinCacheTtl_OnlyCallsS3Once()
    {
        // Arrange
        _repo.ListObjectsAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(string, long)>>([]));
        var sut = CreateSut();

        // Act
        await sut.GetStatsAsync(CancellationToken.None);
        await sut.GetStatsAsync(CancellationToken.None);

        // Assert
        await _repo.Received(1).ListObjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Health ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsHealthyAsync_WhenBucketReachable_ReturnsTrue()
    {
        // Arrange
        _repo.BucketExistsAsync(Arg.Any<CancellationToken>()).Returns(true);
        var sut = CreateSut();

        // Act
        var result = await sut.IsHealthyAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenS3Unreachable_ReturnsFalse()
    {
        // Arrange
        _repo.BucketExistsAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateSut();

        // Act
        var result = await sut.IsHealthyAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a valid 1x1 pixel JPEG stream that ImageSharp can decode for thumbnail generation tests.</summary>
    private static MemoryStream CreateFakeJpegStream()
    {
        using var image = new Image<Rgb24>(1, 1);
        var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }
}

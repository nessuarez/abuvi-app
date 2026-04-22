# Backend Implementation Plan: feat-blob-storage Blob Storage Service

## Overview

Implement a Blob Storage service that enables the ABUVI backend to upload, retrieve, and manage binary files (photos, videos, audio, documents) hosted on **Hetzner Object Storage** (S3-compatible). This is a cross-cutting infrastructure service exposed via three Minimal API endpoints and integrated into the `/health` check.

The implementation follows **Vertical Slice Architecture**: all blob storage code lives in `Features/BlobStorage/`. The service is pure infrastructure — no EF Core or database migrations are needed in this ticket.

---

## Architecture Context

### Feature slice

```
src/Abuvi.API/Features/BlobStorage/
├── BlobStorageEndpoints.cs       # Minimal API endpoint definitions
├── BlobStorageModels.cs          # Request/Response DTOs + BlobStorageOptions
├── IBlobStorageService.cs        # Service abstraction
├── BlobStorageService.cs         # Business logic: upload, delete, thumbnail, stats
├── IBlobStorageRepository.cs     # Repository abstraction (S3 client wrapper)
├── BlobStorageRepository.cs      # S3 client implementation (AmazonS3Client)
├── BlobStorageValidator.cs       # FluentValidation rules for upload requests
└── BlobStorageExtensions.cs      # IServiceCollection extension: DI registration
```

### Health check

```
src/Abuvi.API/Common/HealthChecks/
└── BlobStorageHealthCheck.cs     # IHealthCheck: connectivity + free space
```

### New test files

```
src/Abuvi.Tests/
├── Unit/Features/BlobStorage/
│   ├── BlobStorageServiceTests.cs
│   ├── BlobStorageValidatorTests.cs
│   └── BlobStorageHealthCheckTests.cs
├── Integration/Features/BlobStorage/
│   └── BlobStorageEndpointsTests.cs
└── Helpers/Builders/
    └── BlobUploadResultBuilder.cs
```

### Modified files

```
src/Abuvi.API/Program.cs                   # DI registration, health check, endpoint mapping, Kestrel config
src/Abuvi.API/appsettings.json             # BlobStorage config section (no secrets)
src/Abuvi.API/Abuvi.API.csproj             # Add NuGet packages
ai-specs/specs/api-endpoints.md            # Document new endpoints + health check entry
```

### Cross-cutting concerns

- `IMemoryCache` is **not yet registered** in Program.cs — must be added via `builder.Services.AddMemoryCache()` in `BlobStorageExtensions.cs`
- No EF Core migrations (no database schema changes)
- No new authorization policies — use inline `.RequireAuthorization(policy => policy.RequireRole("Admin"))` following the existing pattern
- Kestrel `MaxRequestBodySize` must be raised to 55 MB to accommodate 50 MB files + headers

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-blob-storage-backend`
- **Implementation Steps**:
  1. Ensure you are on `main`: `git checkout main`
  2. Pull latest: `git pull origin main`
  3. Create branch: `git checkout -b feature/feat-blob-storage-backend`
  4. Verify: `git branch`
- **Notes**: This must be the FIRST step before any code changes. Do not work on the `feat/blob-storage` branch — that branch is for tracking the spec files.

---

### Step 1: Install NuGet Packages

- **File**: `src/Abuvi.API/Abuvi.API.csproj`
- **Action**: Add two new NuGet packages
- **Commands**:

  ```bash
  dotnet add src/Abuvi.API/Abuvi.API.csproj package AWSSDK.S3
  dotnet add src/Abuvi.API/Abuvi.API.csproj package SixLabors.ImageSharp
  ```

- **Result in .csproj** (versions may differ, pin to latest stable):

  ```xml
  <PackageReference Include="AWSSDK.S3" Version="3.*" />
  <PackageReference Include="SixLabors.ImageSharp" Version="3.*" />
  ```

- **Notes**:
  - `AWSSDK.S3` provides `AmazonS3Client` with S3-compatible API support (custom endpoint for Hetzner)
  - `SixLabors.ImageSharp` provides server-side image resizing/WebP conversion for thumbnails
  - No test project packages needed — NSubstitute is already present for mocking `IBlobStorageRepository`

---

### Step 2: Add BlobStorageOptions and appsettings.json Section

- **File 1**: `src/Abuvi.API/Features/BlobStorage/BlobStorageModels.cs` (partial — options class included here; add DTOs in Step 3)
- **File 2**: `src/Abuvi.API/appsettings.json`

#### 2a. Create `BlobStorageOptions` class (inside `BlobStorageModels.cs`)

```csharp
namespace Abuvi.API.Features.BlobStorage;

public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string BucketName { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;
    public string PublicBaseUrl { get; init; } = string.Empty;
    public long MaxFileSizeBytes { get; init; } = 52_428_800; // 50 MB
    public string[] AllowedImageExtensions { get; init; } = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    public string[] AllowedVideoExtensions { get; init; } = [".mp4", ".mov", ".avi", ".webm"];
    public string[] AllowedAudioExtensions { get; init; } = [".mp3", ".wav", ".ogg", ".m4a", ".flac", ".aac"];
    public string[] AllowedDocumentExtensions { get; init; } = [".pdf", ".doc", ".docx"];
    public int ThumbnailWidthPx { get; init; } = 400;
    public int ThumbnailHeightPx { get; init; } = 400;
    public long StorageQuotaBytes { get; init; } = 0; // 0 = unconfigured (skip threshold check)
    public double StorageWarningThresholdPct { get; init; } = 80.0;
    public double StorageCriticalThresholdPct { get; init; } = 95.0;
}
```

#### 2b. Add config section to `appsettings.json`

Add after the existing top-level keys (no secrets here — values come from `dotnet user-secrets` or environment variables):

```json
"BlobStorage": {
  "BucketName": "abuvi-media",
  "Endpoint": "https://fsn1.your-objectstorage.com",
  "Region": "fsn1",
  "AccessKeyId": "",
  "SecretAccessKey": "",
  "PublicBaseUrl": "https://abuvi-media.fsn1.your-objectstorage.com",
  "MaxFileSizeBytes": 52428800,
  "AllowedImageExtensions": [".jpg", ".jpeg", ".png", ".webp", ".gif"],
  "AllowedVideoExtensions": [".mp4", ".mov", ".avi", ".webm"],
  "AllowedAudioExtensions": [".mp3", ".wav", ".ogg", ".m4a", ".flac", ".aac"],
  "AllowedDocumentExtensions": [".pdf", ".doc", ".docx"],
  "ThumbnailWidthPx": 400,
  "ThumbnailHeightPx": 400,
  "StorageQuotaBytes": 53687091200,
  "StorageWarningThresholdPct": 80,
  "StorageCriticalThresholdPct": 95
}
```

- **Notes**:
  - `AccessKeyId` and `SecretAccessKey` are intentionally left empty — set them via `dotnet user-secrets set "BlobStorage:AccessKeyId" "..."` in development and via environment variable in production
  - `StorageQuotaBytes`: 53687091200 = 50 GB. Set to `0` to disable free space monitoring
  - Never commit credentials — add a pre-commit hook check if not already present

---

### Step 3: Define DTOs and Interfaces

- **File**: `src/Abuvi.API/Features/BlobStorage/BlobStorageModels.cs` (complete the file)

Add the following records **below** `BlobStorageOptions` in the same file:

```csharp
// ──────────────────────────────────────────────────────
// Request DTOs
// ──────────────────────────────────────────────────────

public record UploadBlobRequest
{
    public IFormFile File { get; init; } = null!;
    public string Folder { get; init; } = string.Empty;
    public Guid? ContextId { get; init; }
    public bool GenerateThumbnail { get; init; } = false;
}

public record DeleteBlobsRequest(IReadOnlyList<string> BlobKeys);

// ──────────────────────────────────────────────────────
// Response DTOs
// ──────────────────────────────────────────────────────

public record BlobUploadResult(
    string FileUrl,
    string? ThumbnailUrl,
    string FileName,
    string ContentType,
    long SizeBytes);

public record BlobStorageStats(
    int TotalObjects,
    long TotalSizeBytes,
    string TotalSizeHumanReadable,
    long? QuotaBytes,
    double? UsedPct,
    long? FreeBytes,
    IReadOnlyDictionary<string, FolderStats> ByFolder);

public record FolderStats(int Objects, long SizeBytes);
```

- **Notes**:
  - `BlobUploadResult.ThumbnailUrl` is nullable — null when thumbnail was not requested or not applicable (audio, video, documents)
  - `BlobStorageStats.QuotaBytes`, `UsedPct`, `FreeBytes` are nullable — null when `StorageQuotaBytes == 0`
  - The spec had a typo `FreeByes` — this plan uses the correct spelling `FreeBytes`

---

- **File**: `src/Abuvi.API/Features/BlobStorage/IBlobStorageRepository.cs`

```csharp
namespace Abuvi.API.Features.BlobStorage;

public interface IBlobStorageRepository
{
    /// <summary>Uploads a stream to S3 at the given key.</summary>
    Task UploadAsync(string key, Stream stream, string contentType, CancellationToken ct);

    /// <summary>Deletes one or more objects by key.</summary>
    Task DeleteManyAsync(IReadOnlyList<string> keys, CancellationToken ct);

    /// <summary>Returns true when the configured bucket is reachable.</summary>
    Task<bool> BucketExistsAsync(CancellationToken ct);

    /// <summary>
    /// Lists all objects under the given prefix.
    /// Returns tuples of (Key, SizeBytes). Pass empty string to list the entire bucket.
    /// </summary>
    Task<IReadOnlyList<(string Key, long SizeBytes)>> ListObjectsAsync(
        string prefix, CancellationToken ct);
}
```

---

- **File**: `src/Abuvi.API/Features/BlobStorage/IBlobStorageService.cs`

```csharp
namespace Abuvi.API.Features.BlobStorage;

public interface IBlobStorageService
{
    /// <summary>Uploads a file stream and returns the public URLs.</summary>
    Task<BlobUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder,
        Guid? contextId,
        bool generateThumbnail,
        CancellationToken ct);

    /// <summary>Deletes one or more blobs by their storage keys.</summary>
    Task DeleteManyAsync(IReadOnlyList<string> blobKeys, CancellationToken ct);

    /// <summary>
    /// Returns storage usage statistics grouped by folder.
    /// Result is cached via IMemoryCache for 5 minutes.
    /// </summary>
    Task<BlobStorageStats> GetStatsAsync(CancellationToken ct);

    /// <summary>Returns true when the bucket is reachable.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct);
}
```

---

### Step 4: Write ALL Tests First (TDD — RED Phase)

Write all test files now, **before any implementation**. They will fail to compile until the implementation classes exist — that is expected. The goal is to define the expected contract before writing code.

---

#### Step 4a: Unit Tests — `BlobStorageServiceTests.cs`

- **File**: `src/Abuvi.Tests/Unit/Features/BlobStorage/BlobStorageServiceTests.cs`
- **Namespace**: `Abuvi.Tests.Unit.Features.BlobStorage`
- **Required usings**:

  ```csharp
  using Abuvi.API.Features.BlobStorage;
  using FluentAssertions;
  using Microsoft.Extensions.Caching.Memory;
  using Microsoft.Extensions.Options;
  using NSubstitute;
  using NSubstitute.ExceptionExtensions;
  using Xunit;
  ```

**Implementation**:

```csharp
namespace Abuvi.Tests.Unit.Features.BlobStorage;

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

    /// <summary>Creates a minimal valid JPEG byte stream for thumbnail generation tests.</summary>
    private static MemoryStream CreateFakeJpegStream()
    {
        // Minimal valid JPEG header (SOI + EOI markers) — ImageSharp can decode this
        byte[] jpegBytes = [
            0xFF, 0xD8, // SOI (Start of Image)
            0xFF, 0xE0, 0x00, 0x10, // APP0 marker
            0x4A, 0x46, 0x49, 0x46, 0x00, // "JFIF\0"
            0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0xFF, 0xD9  // EOI (End of Image)
        ];
        return new MemoryStream(jpegBytes);
    }
}
```

> **TDD note**: Run `dotnet test` now. It will **fail to compile** because `BlobStorageService` does not exist. That is expected (RED phase).

---

#### Step 4b: Unit Tests — `BlobStorageValidatorTests.cs`

- **File**: `src/Abuvi.Tests/Unit/Features/BlobStorage/BlobStorageValidatorTests.cs`
- **Namespace**: `Abuvi.Tests.Unit.Features.BlobStorage`

```csharp
namespace Abuvi.Tests.Unit.Features.BlobStorage;

using Abuvi.API.Features.BlobStorage;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

public class BlobStorageValidatorTests
{
    private static readonly BlobStorageOptions DefaultOptions = new()
    {
        MaxFileSizeBytes = 52_428_800,
        AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"],
        AllowedVideoExtensions = [".mp4", ".mov"],
        AllowedAudioExtensions = [".mp3", ".wav"],
        AllowedDocumentExtensions = [".pdf", ".doc", ".docx"],
    };

    private static UploadBlobRequestValidator CreateSut(BlobStorageOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions));

    private static IFormFile CreateMockFile(string fileName, long sizeBytes = 1024)
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.Length.Returns(sizeBytes);
        return file;
    }

    [Fact]
    public void Validate_WhenFileIsNull_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { Folder = "photos" };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validate_WhenFileExceedsMaxSize_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest
        {
            File = CreateMockFile("big.jpg", DefaultOptions.MaxFileSizeBytes + 1),
            Folder = "photos"
        };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validate_WhenFolderIsEmpty_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("photo.jpg"), Folder = "" };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Folder);
    }

    [Theory]
    [InlineData("invalid-folder")]
    [InlineData("profile-photos")]
    [InlineData("../../etc")]
    public void Validate_WhenFolderNotInAllowedList_Fails(string folder)
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("photo.jpg"), Folder = folder };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Folder);
    }

    [Fact]
    public void Validate_WhenPdfUploadedToPhotosFolder_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("doc.pdf"), Folder = "photos" };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validate_WhenAudioUploadedToPhotosFolder_Fails()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("audio.mp3"), Folder = "photos" };
        sut.TestValidate(request).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Validate_WhenValidImageInPhotosFolder_Passes()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("photo.jpg"), Folder = "photos" };
        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".wav")]
    public void Validate_WhenValidAudioInMediaItemsFolder_Passes(string ext)
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest
        {
            File = CreateMockFile($"recording{ext}"),
            Folder = "media-items"
        };
        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenValidDocumentInMediaItemsFolder_Passes()
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("report.pdf"), Folder = "media-items" };
        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("camp-locations")]
    [InlineData("camp-photos")]
    public void Validate_WhenValidImageInNonMediaFolder_Passes(string folder)
    {
        var sut = CreateSut();
        var request = new UploadBlobRequest { File = CreateMockFile("photo.webp"), Folder = folder };
        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}
```

---

#### Step 4c: Unit Tests — `BlobStorageHealthCheckTests.cs`

- **File**: `src/Abuvi.Tests/Unit/Features/BlobStorage/BlobStorageHealthCheckTests.cs`
- **Namespace**: `Abuvi.Tests.Unit.Features.BlobStorage`

```csharp
namespace Abuvi.Tests.Unit.Features.BlobStorage;

using Abuvi.API.Common.HealthChecks;
using Abuvi.API.Features.BlobStorage;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

public class BlobStorageHealthCheckTests
{
    private readonly IBlobStorageService _service = Substitute.For<IBlobStorageService>();
    private readonly BlobStorageOptions _options = new()
    {
        StorageQuotaBytes = 107_374_182_400L, // 100 GB
        StorageWarningThresholdPct = 80,
        StorageCriticalThresholdPct = 95
    };

    private BlobStorageHealthCheck CreateSut(BlobStorageOptions? opts = null) =>
        new(_service, Options.Create(opts ?? _options));

    private static HealthCheckContext CreateContext(IHealthCheck check) =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "blob-storage", check, HealthStatus.Degraded, null)
        };

    [Fact]
    public async Task CheckHealthAsync_WhenBucketUnreachable_ReturnsDegraded()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateSut();

        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNoQuotaConfigured_ReturnsHealthyWithoutStats()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        var sut = CreateSut(new BlobStorageOptions { StorageQuotaBytes = 0 });

        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Healthy);
        await _service.DidNotReceive().GetStatsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUsageBelowWarning_ReturnsHealthy()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        _service.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(
            BuildStats(usedBytes: 10_737_418_240L)); // 10 GB = 10% of 100 GB

        var sut = CreateSut();
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("usedPct");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUsageAboveWarning_ReturnsDegraded()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        _service.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(
            BuildStats(usedBytes: 85_899_345_920L)); // 80 GB = 80% of 100 GB

        var sut = CreateSut();
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUsageAboveCritical_ReturnsUnhealthy()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        _service.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(
            BuildStats(usedBytes: 102_005_473_280L)); // 95 GB = 95% of 100 GB

        var sut = CreateSut();
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsDegraded()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Network error"));
        var sut = CreateSut();

        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Exception.Should().NotBeNull();
    }

    private BlobStorageStats BuildStats(long usedBytes) =>
        new(TotalObjects: 100,
            TotalSizeBytes: usedBytes,
            TotalSizeHumanReadable: $"{usedBytes / 1_073_741_824.0:F1} GB",
            QuotaBytes: _options.StorageQuotaBytes,
            UsedPct: Math.Round((double)usedBytes / _options.StorageQuotaBytes * 100, 1),
            FreeBytes: _options.StorageQuotaBytes - usedBytes,
            ByFolder: new Dictionary<string, FolderStats>());
}
```

---

#### Step 4d: Integration Tests — `BlobStorageEndpointsTests.cs`

- **File**: `src/Abuvi.Tests/Integration/Features/BlobStorage/BlobStorageEndpointsTests.cs`
- **Namespace**: `Abuvi.Tests.Integration.Features.BlobStorage`

```csharp
namespace Abuvi.Tests.Integration.Features.BlobStorage;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.BlobStorage;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

public class BlobStorageEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BlobStorageEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // Override IBlobStorageRepository and IBlobStorageService with NSubstitute mocks
        // so tests never touch real S3
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove real registrations
                var repoDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IBlobStorageRepository));
                if (repoDescriptor != null) services.Remove(repoDescriptor);

                var svcDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IBlobStorageService));
                if (svcDescriptor != null) services.Remove(svcDescriptor);

                // Register mocks
                var mockRepo = Substitute.For<IBlobStorageRepository>();
                var mockService = Substitute.For<IBlobStorageService>();

                mockService.UploadAsync(
                    Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                    .Returns(new BlobUploadResult(
                        "https://cdn.example.com/photos/abc/test.jpg",
                        null,
                        "test.jpg",
                        "image/jpeg",
                        1024));

                mockService.GetStatsAsync(Arg.Any<CancellationToken>())
                    .Returns(new BlobStorageStats(10, 1024, "1 KB", null, null, null,
                        new Dictionary<string, FolderStats>()));

                services.AddSingleton(mockRepo);
                services.AddSingleton(mockService);
            });
        });
    }

    // ── Authentication ────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var content = CreateValidUploadContent("photo.jpg", "image/jpeg");

        var response = await client.PostAsync("/api/blobs/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBlobs_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/blobs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStats_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/blobs/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_AsAuthenticatedMember_WithValidFile_Returns200()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = CreateValidUploadContent("photo.jpg", "image/jpeg");

        var response = await client.PostAsync("/api/blobs/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<BlobUploadResult>>();
        result!.Success.Should().BeTrue();
        result.Data!.FileUrl.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Upload_WithInvalidFolder_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = CreateValidUploadContent("photo.jpg", "image/jpeg", folder: "invalid-folder");

        var response = await client.PostAsync("/api/blobs/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBlobs_AsMember_Returns403()
    {
        var client = await CreateAuthenticatedClientAsync(role: "Member");
        var body = new DeleteBlobsRequest(["photos/abc/test.jpg"]);

        var response = await client.DeleteAsJsonAsync("/api/blobs", body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_AsMember_Returns403()
    {
        var client = await CreateAuthenticatedClientAsync(role: "Member");

        var response = await client.GetAsync("/api/blobs/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string role = "Member")
    {
        var client = _factory.CreateClient();

        var email = $"test{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest(email, "Password123!", "Test", "User", null);
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        var token = loginResult!.Data!.Token;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static MultipartFormDataContent CreateValidUploadContent(
        string fileName,
        string contentType,
        string folder = "photos",
        Guid? contextId = null,
        bool generateThumbnail = false)
    {
        var content = new MultipartFormDataContent();
        var fileBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }; // minimal JPEG
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(folder), "folder");
        if (contextId.HasValue)
            content.Add(new StringContent(contextId.Value.ToString()), "contextId");
        content.Add(new StringContent(generateThumbnail.ToString().ToLower()), "generateThumbnail");
        return content;
    }
}
```

> **Notes on integration tests**:
>
> - The `GetStats_AsAdmin` and `DeleteBlobs_AsAdmin` scenarios require an Admin JWT. Since the registration flow creates Member accounts, use `WithWebHostBuilder` override to inject a JWT with the Admin role in a separate test class, OR accept that these scenarios are covered by the unit tests + role checks demonstrated through the Member→403 tests.
> - `DeleteAsJsonAsync` requires adding `using Microsoft.AspNetCore.Mvc.Testing` to get the extension method.

---

### Step 5: Implement `BlobStorageRepository`

- **File**: `src/Abuvi.API/Features/BlobStorage/BlobStorageRepository.cs`
- **Required usings**:

  ```csharp
  using Amazon.S3;
  using Amazon.S3.Model;
  using Microsoft.Extensions.Options;
  ```

- **Notes**: Register as **Scoped** (not Singleton) because `AmazonS3Client` implements `IDisposable`

**Implementation**:

```csharp
namespace Abuvi.API.Features.BlobStorage;

public sealed class BlobStorageRepository : IBlobStorageRepository, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly BlobStorageOptions _options;
    private bool _disposed;

    public BlobStorageRepository(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true, // Required for Hetzner and most S3-compatible providers
        };
        _s3 = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, config);
    }

    public async Task UploadAsync(string key, Stream stream, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            CannedACL = S3CannedACL.PublicRead, // Bucket is configured for public read
        };
        await _s3.PutObjectAsync(request, ct);
    }

    public async Task DeleteManyAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        if (keys.Count == 0) return;

        var request = new DeleteObjectsRequest
        {
            BucketName = _options.BucketName,
            Objects = keys.Select(k => new KeyVersion { Key = k }).ToList()
        };
        await _s3.DeleteObjectsAsync(request, ct);
    }

    public async Task<bool> BucketExistsAsync(CancellationToken ct)
    {
        try
        {
            await _s3.GetBucketLocationAsync(_options.BucketName, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<(string Key, long SizeBytes)>> ListObjectsAsync(
        string prefix, CancellationToken ct)
    {
        var results = new List<(string Key, long SizeBytes)>();
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken,
            };
            var response = await _s3.ListObjectsV2Async(request, ct);
            results.AddRange(response.S3Objects.Select(o => (o.Key, o.Size)));
            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        } while (continuationToken is not null);

        return results;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _s3.Dispose();
            _disposed = true;
        }
    }
}
```

- **Implementation Notes**:
  - `ForcePathStyle = true` is required for Hetzner Object Storage (path-based addressing, not virtual-hosted)
  - `S3CannedACL.PublicRead` — the bucket is configured for public read, all uploaded objects are publicly accessible by URL
  - `ListObjectsV2` handles pagination via `ContinuationToken` — the full bucket enumeration is only done for stats and is cached at the service layer

---

### Step 6: Implement `BlobStorageValidator`

- **File**: `src/Abuvi.API/Features/BlobStorage/BlobStorageValidator.cs`
- **Required usings**:

  ```csharp
  using FluentValidation;
  using Microsoft.Extensions.Options;
  ```

**Implementation**:

```csharp
namespace Abuvi.API.Features.BlobStorage;

public class UploadBlobRequestValidator : AbstractValidator<UploadBlobRequest>
{
    private static readonly string[] AllowedFolders =
        ["photos", "media-items", "camp-locations", "camp-photos"];

    public UploadBlobRequestValidator(IOptions<BlobStorageOptions> options)
    {
        var cfg = options.Value;

        RuleFor(x => x.File)
            .NotNull().WithMessage("El archivo es obligatorio");

        RuleFor(x => x.File)
            .Must(f => f.Length <= cfg.MaxFileSizeBytes)
            .WithMessage($"El archivo no puede superar {cfg.MaxFileSizeBytes / 1_048_576} MB")
            .When(x => x.File is not null);

        RuleFor(x => x.Folder)
            .NotEmpty().WithMessage("La carpeta es obligatoria")
            .Must(f => AllowedFolders.Contains(f))
            .WithMessage("La carpeta especificada no es válida");

        RuleFor(x => x.File)
            .Must((req, file) => IsExtensionAllowed(req.Folder, file, cfg))
            .WithMessage("El tipo de archivo no está permitido para esta carpeta")
            .When(x => x.File is not null && AllowedFolders.Contains(x.Folder));
    }

    private static bool IsExtensionAllowed(string folder, IFormFile file, BlobStorageOptions cfg)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return folder switch
        {
            "media-items" => cfg.AllowedImageExtensions.Contains(ext)
                             || cfg.AllowedVideoExtensions.Contains(ext)
                             || cfg.AllowedAudioExtensions.Contains(ext)
                             || cfg.AllowedDocumentExtensions.Contains(ext),
            _ => cfg.AllowedImageExtensions.Contains(ext)
        };
    }
}
```

- **Implementation Notes**:
  - The `AllowedFolders` list is the authoritative list; adding a folder here is the only change needed to allow a new upload target
  - All error messages are in Spanish (project convention)
  - `Path.GetExtension` always returns lowercase after `.ToLowerInvariant()` — consistent regardless of what the client sends

---

### Step 7: Implement `BlobStorageService`

- **File**: `src/Abuvi.API/Features/BlobStorage/BlobStorageService.cs`
- **Required usings**:

  ```csharp
  using Microsoft.Extensions.Caching.Memory;
  using Microsoft.Extensions.Options;
  using SixLabors.ImageSharp;
  using SixLabors.ImageSharp.Processing;
  ```

**Implementation**:

```csharp
namespace Abuvi.API.Features.BlobStorage;

public class BlobStorageService(
    IBlobStorageRepository repository,
    IOptions<BlobStorageOptions> options,
    IMemoryCache cache) : IBlobStorageService
{
    private const string StatsCacheKey = "blob-storage-stats";
    private static readonly TimeSpan StatsCacheTtl = TimeSpan.FromMinutes(5);

    private readonly BlobStorageOptions _cfg = options.Value;

    public async Task<BlobUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder,
        Guid? contextId,
        bool generateThumbnail,
        CancellationToken ct)
    {
        var guid = Guid.NewGuid().ToString("N");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var key = BuildKey(folder, contextId, guid, ext);

        // Buffer stream for potential thumbnail generation
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, ct);
        buffer.Seek(0, SeekOrigin.Begin);

        // Upload original
        await repository.UploadAsync(key, buffer, contentType, ct);

        string? thumbnailUrl = null;
        var isImage = _cfg.AllowedImageExtensions.Contains(ext);

        if (generateThumbnail && isImage)
        {
            buffer.Seek(0, SeekOrigin.Begin);
            thumbnailUrl = await GenerateAndUploadThumbnailAsync(buffer, folder, contextId, guid, ct);
        }

        // Invalidate stats cache (upload changes object count / size)
        cache.Remove(StatsCacheKey);

        var fileUrl = $"{_cfg.PublicBaseUrl}/{key}";
        return new BlobUploadResult(
            FileUrl: fileUrl,
            ThumbnailUrl: thumbnailUrl,
            FileName: $"{guid}{ext}",
            ContentType: contentType,
            SizeBytes: buffer.Length);
    }

    public async Task DeleteManyAsync(IReadOnlyList<string> blobKeys, CancellationToken ct)
    {
        await repository.DeleteManyAsync(blobKeys, ct);
        cache.Remove(StatsCacheKey);
    }

    public async Task<BlobStorageStats> GetStatsAsync(CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(StatsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = StatsCacheTtl;
            return await ComputeStatsAsync(ct);
        }) ?? await ComputeStatsAsync(ct);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct) =>
        await repository.BucketExistsAsync(ct);

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<string> GenerateAndUploadThumbnailAsync(
        Stream originalStream,
        string folder,
        Guid? contextId,
        string guid,
        CancellationToken ct)
    {
        using var thumbStream = new MemoryStream();

        using (var image = await Image.LoadAsync(originalStream, ct))
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(_cfg.ThumbnailWidthPx, _cfg.ThumbnailHeightPx),
                Mode = ResizeMode.Max
            }));
            await image.SaveAsWebpAsync(thumbStream, ct);
        }

        thumbStream.Seek(0, SeekOrigin.Begin);
        var thumbKey = BuildThumbnailKey(folder, contextId, guid);
        await repository.UploadAsync(thumbKey, thumbStream, "image/webp", ct);

        return $"{_cfg.PublicBaseUrl}/{thumbKey}";
    }

    private static string BuildKey(string folder, Guid? contextId, string guid, string ext) =>
        contextId.HasValue
            ? $"{folder}/{contextId}/{guid}{ext}"
            : $"{folder}/{guid}{ext}";

    private static string BuildThumbnailKey(string folder, Guid? contextId, string guid) =>
        contextId.HasValue
            ? $"{folder}/{contextId}/thumbs/{guid}.webp"
            : $"{folder}/thumbs/{guid}.webp";

    private async Task<BlobStorageStats> ComputeStatsAsync(CancellationToken ct)
    {
        var allObjects = await repository.ListObjectsAsync(string.Empty, ct);

        var byFolder = allObjects
            .GroupBy(o => o.Key.Split('/')[0])
            .ToDictionary(
                g => g.Key,
                g => new FolderStats(g.Count(), g.Sum(o => o.SizeBytes)));

        var totalSize = allObjects.Sum(o => o.SizeBytes);
        var totalObjects = allObjects.Count;
        var humanReadable = FormatBytes(totalSize);

        long? quotaBytes = _cfg.StorageQuotaBytes > 0 ? _cfg.StorageQuotaBytes : null;
        double? usedPct = quotaBytes.HasValue
            ? Math.Round((double)totalSize / quotaBytes.Value * 100, 1)
            : null;
        long? freeBytes = quotaBytes.HasValue
            ? quotaBytes.Value - totalSize
            : null;

        return new BlobStorageStats(
            TotalObjects: totalObjects,
            TotalSizeBytes: totalSize,
            TotalSizeHumanReadable: humanReadable,
            QuotaBytes: quotaBytes,
            UsedPct: usedPct,
            FreeBytes: freeBytes,
            ByFolder: byFolder);
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024          => $"{bytes / 1024.0:F1} KB",
        _                => $"{bytes} B"
    };
}
```

- **Implementation Notes**:
  - The uploaded stream is buffered in `MemoryStream` (necessary for thumbnail — `ImageSharp.LoadAsync` requires a seekable stream). For large files this fits within the 50 MB limit.
  - `ResizeMode.Max` preserves aspect ratio and constrains to 400×400 without cropping
  - `SaveAsWebpAsync` requires `SixLabors.ImageSharp.Formats.Webp` — included automatically in the `SixLabors.ImageSharp` package
  - Stats `GetOrCreateAsync` returns `null` only if the factory delegate returned null, which is impossible here; the `?? await ComputeStatsAsync(ct)` fallback satisfies the nullable compiler

---

### Step 8: Implement `BlobStorageHealthCheck`

- **File**: `src/Abuvi.API/Common/HealthChecks/BlobStorageHealthCheck.cs`
- **Namespace**: `Abuvi.API.Common.HealthChecks`
- **Required usings**:

  ```csharp
  using Abuvi.API.Features.BlobStorage;
  using Microsoft.Extensions.Diagnostics.HealthChecks;
  using Microsoft.Extensions.Options;
  ```

**Implementation**:

```csharp
namespace Abuvi.API.Common.HealthChecks;

public class BlobStorageHealthCheck(
    IBlobStorageService blobService,
    IOptions<BlobStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await blobService.IsHealthyAsync(cancellationToken))
                return HealthCheckResult.Degraded("El bucket no está disponible");

            var quota = options.Value.StorageQuotaBytes;
            if (quota <= 0)
                return HealthCheckResult.Healthy("Bucket accesible");

            var stats = await blobService.GetStatsAsync(cancellationToken); // cached 5 min
            var usedPct = (double)stats.TotalSizeBytes / quota * 100;

            var data = new Dictionary<string, object>
            {
                ["usedBytes"] = stats.TotalSizeBytes,
                ["quotaBytes"] = quota,
                ["freeBytes"] = quota - stats.TotalSizeBytes,
                ["usedPct"] = Math.Round(usedPct, 1)
            };

            var desc = $"{usedPct:F1}% usado ({stats.TotalSizeHumanReadable} / {FormatBytes(quota)})";

            if (usedPct >= options.Value.StorageCriticalThresholdPct)
                return HealthCheckResult.Unhealthy($"Almacenamiento crítico: {desc}", data: data);

            if (usedPct >= options.Value.StorageWarningThresholdPct)
                return HealthCheckResult.Degraded($"Advertencia de almacenamiento: {desc}", data: data);

            return HealthCheckResult.Healthy($"Bucket accesible. {desc}", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Error al verificar blob storage", ex);
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024          => $"{bytes / 1024.0:F1} KB",
        _                => $"{bytes} B"
    };
}
```

- **Notes**:
  - The `failureStatus: HealthStatus.Degraded` registered in Program.cs applies only when the check **throws an unhandled exception** before our `try/catch`. Since we catch all exceptions ourselves, this is a safety net.
  - `GetStatsAsync` is cached by `BlobStorageService` — this check does not hit S3 on every poll if stats were recently computed

---

### Step 9: Implement `BlobStorageEndpoints`

- **File**: `src/Abuvi.API/Features/BlobStorage/BlobStorageEndpoints.cs`
- **Required usings**:

  ```csharp
  using Abuvi.API.Common.Models;
  using FluentValidation;
  using Microsoft.AspNetCore.Http.HttpResults;
  ```

**Implementation**:

```csharp
namespace Abuvi.API.Features.BlobStorage;

public static class BlobStorageEndpoints
{
    public static WebApplication MapBlobStorageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/blobs")
            .WithTags("BlobStorage");

        group.MapPost("/upload", UploadAsync)
            .RequireAuthorization()
            .DisableAntiforgery() // Required for multipart/form-data in Minimal APIs
            .WithName("UploadBlob")
            .WithSummary("Upload a file to blob storage");

        group.MapDelete("/", DeleteAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("DeleteBlobs")
            .WithSummary("Delete one or more blobs (Admin only)");

        group.MapGet("/stats", GetStatsAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("GetBlobStats")
            .WithSummary("Get storage usage statistics (Admin only)");

        return app;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        [Microsoft.AspNetCore.Mvc.FromForm] string folder,
        [Microsoft.AspNetCore.Mvc.FromForm] Guid? contextId,
        [Microsoft.AspNetCore.Mvc.FromForm] bool generateThumbnail,
        IBlobStorageService blobService,
        IValidator<UploadBlobRequest> validator,
        CancellationToken ct)
    {
        var request = new UploadBlobRequest
        {
            File = file,
            Folder = folder,
            ContextId = contextId,
            GenerateThumbnail = generateThumbnail
        };

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(e => new ValidationError(e.PropertyName, e.ErrorMessage))
                .ToList();
            return TypedResults.BadRequest(
                ApiResponse<BlobUploadResult>.ValidationFail("Datos no válidos", errors));
        }

        await using var stream = file.OpenReadStream();
        var result = await blobService.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            folder,
            contextId,
            generateThumbnail,
            ct);

        return TypedResults.Ok(ApiResponse<BlobUploadResult>.Ok(result));
    }

    private static async Task<IResult> DeleteAsync(
        DeleteBlobsRequest body,
        IBlobStorageService blobService,
        CancellationToken ct)
    {
        await blobService.DeleteManyAsync(body.BlobKeys, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetStatsAsync(
        IBlobStorageService blobService,
        CancellationToken ct)
    {
        var stats = await blobService.GetStatsAsync(ct);
        return TypedResults.Ok(ApiResponse<BlobStorageStats>.Ok(stats));
    }
}
```

- **Implementation Notes**:
  - `DisableAntiforgery()` is mandatory for `multipart/form-data` endpoints in Minimal APIs (no MVC form token)
  - `IFormFile` is automatically bound from the multipart form — no attribute needed on it
  - `[FromForm]` attributes are needed for the other form fields (folder, contextId, generateThumbnail) to disambiguate from route/query binding
  - The validator is injected via DI — `AddValidatorsFromAssemblyContaining<Program>()` already scans and registers it
  - `await using var stream` — `IFormFile.OpenReadStream()` returns a `Stream` that must be disposed

---

### Step 10: Implement `BlobStorageExtensions`

- **File**: `src/Abuvi.API/Features/BlobStorage/BlobStorageExtensions.cs`
- **Required usings**:

  ```csharp
  using Microsoft.Extensions.Options;
  ```

**Implementation**:

```csharp
namespace Abuvi.API.Features.BlobStorage;

public static class BlobStorageExtensions
{
    public static IServiceCollection AddBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and validate options
        services.AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection(BlobStorageOptions.SectionName))
            .Validate(opts => !string.IsNullOrEmpty(opts.BucketName),
                "BlobStorage:BucketName is required")
            .Validate(opts => !string.IsNullOrEmpty(opts.PublicBaseUrl),
                "BlobStorage:PublicBaseUrl is required")
            .ValidateOnStart();

        // Register IMemoryCache (not yet registered in Program.cs)
        services.AddMemoryCache();

        // Register blob storage services (Scoped — AmazonS3Client implements IDisposable)
        services.AddScoped<IBlobStorageRepository, BlobStorageRepository>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();

        return services;
    }
}
```

- **Notes**:
  - `ValidateOnStart()` causes startup to fail fast if `BucketName` or `PublicBaseUrl` is empty — prevents silent misconfiguration in production
  - `AddMemoryCache()` is idempotent — safe to call even if another feature adds it later
  - `BlobStorageRepository` is **Scoped** (not Singleton) because it holds a disposable `AmazonS3Client`

---

### Step 11: Update `Program.cs`

Three changes needed:

#### 11a. Import namespace and register blob storage services

Add at the top of `Program.cs` (with the other `using` statements):

```csharp
using Abuvi.API.Features.BlobStorage;
```

In the services section (after the Registrations feature block, before Email Service):

```csharp
// Blob Storage
builder.Services.AddBlobStorage(builder.Configuration);
```

#### 11b. Add BlobStorageHealthCheck to the existing health checks chain

In the existing `builder.Services.AddHealthChecks()` chain (currently ending with `SeqHealthCheck`), add:

```csharp
.AddCheck<BlobStorageHealthCheck>(
    name: "blob-storage",
    failureStatus: HealthStatus.Degraded,
    tags: ["storage", "external"]);
```

The full chain becomes:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(...)
    .AddCheck<ResendHealthCheck>(...)
    .AddCheck<GooglePlacesHealthCheck>(...)
    .AddCheck<SeqHealthCheck>(...)
    .AddCheck<BlobStorageHealthCheck>(
        name: "blob-storage",
        failureStatus: HealthStatus.Degraded,
        tags: ["storage", "external"]);
```

#### 11c. Map blob storage endpoints

In the endpoint mapping section (after `app.MapRegistrationsEndpoints()`):

```csharp
app.MapBlobStorageEndpoints();
```

#### 11d. Configure Kestrel max request body size

Add **before** `var app = builder.Build();`, near the service registrations:

```csharp
// Increase Kestrel limit for file uploads (default is 30 MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 55 * 1024 * 1024; // 55 MB (50 MB file + headers)
});
```

- **Notes**:
  - Without raising `MaxRequestBodySize`, 50 MB uploads will be rejected by Kestrel before reaching the endpoint with HTTP 413
  - 55 MB provides headroom for multipart headers and boundary overhead
  - The validator still enforces the configured `MaxFileSizeBytes` as the effective limit

---

### Step 12: Create Test Builder `BlobUploadResultBuilder`

- **File**: `src/Abuvi.Tests/Helpers/Builders/BlobUploadResultBuilder.cs`
- **Namespace**: `Abuvi.Tests.Helpers.Builders`

```csharp
namespace Abuvi.Tests.Helpers.Builders;

using Abuvi.API.Features.BlobStorage;

public class BlobUploadResultBuilder
{
    private string _fileUrl = "https://cdn.example.com/photos/abc/test.jpg";
    private string? _thumbnailUrl = null;
    private string _fileName = "test.jpg";
    private string _contentType = "image/jpeg";
    private long _sizeBytes = 1024;

    public BlobUploadResultBuilder WithFileUrl(string url) { _fileUrl = url; return this; }
    public BlobUploadResultBuilder WithThumbnailUrl(string? url) { _thumbnailUrl = url; return this; }
    public BlobUploadResultBuilder WithFileName(string name) { _fileName = name; return this; }
    public BlobUploadResultBuilder WithContentType(string type) { _contentType = type; return this; }
    public BlobUploadResultBuilder WithSizeBytes(long size) { _sizeBytes = size; return this; }

    public BlobUploadResultBuilder AsImageWithThumbnail()
    {
        _thumbnailUrl = "https://cdn.example.com/photos/abc/thumbs/test.webp";
        return this;
    }

    public BlobUploadResultBuilder AsAudio()
    {
        _fileUrl = "https://cdn.example.com/media-items/audio.mp3";
        _fileName = "audio.mp3";
        _contentType = "audio/mpeg";
        _thumbnailUrl = null;
        return this;
    }

    public BlobUploadResult Build() => new(
        FileUrl: _fileUrl,
        ThumbnailUrl: _thumbnailUrl,
        FileName: _fileName,
        ContentType: _contentType,
        SizeBytes: _sizeBytes);
}
```

---

### Step 13: Update Technical Documentation

- **File**: `ai-specs/specs/api-endpoints.md`
- **Action**: Add a new section for the `/api/blobs/*` endpoints and update the health check table
- **Implementation Steps**:
  1. Open `ai-specs/specs/api-endpoints.md`
  2. Add new endpoint entries for:
     - `POST /api/blobs/upload` — multipart upload, authenticated
     - `DELETE /api/blobs` — delete blobs, Admin only
     - `GET /api/blobs/stats` — storage statistics, Admin only
  3. Update the health check table to include the `blob-storage` entry:
     - Name: `blob-storage`
     - Failure status: `Degraded` (connectivity), `Unhealthy` (critical storage usage)
     - Tags: `storage`, `external`
  4. All documentation must be in English
- **References**: Follow `ai-specs/specs/documentation-standards.mdc`

---

## Implementation Order

1. **Step 0** — Create branch `feature/feat-blob-storage-backend`
2. **Step 1** — Install NuGet packages (`AWSSDK.S3`, `SixLabors.ImageSharp`)
3. **Step 2** — Add `BlobStorageOptions` and `appsettings.json` section
4. **Step 3** — Define DTOs and interfaces (`BlobStorageModels.cs`, `IBlobStorageRepository.cs`, `IBlobStorageService.cs`)
5. **Step 4** — Write ALL tests first (TDD RED — expected compile failures)
6. **Step 5** — Implement `BlobStorageRepository` (GREEN for repository-dependent tests)
7. **Step 6** — Implement `BlobStorageValidator` (GREEN for validator tests)
8. **Step 7** — Implement `BlobStorageService` (GREEN for service unit tests)
9. **Step 8** — Implement `BlobStorageHealthCheck` (GREEN for health check tests)
10. **Step 9** — Implement `BlobStorageEndpoints`
11. **Step 10** — Implement `BlobStorageExtensions` (DI registration)
12. **Step 11** — Update `Program.cs`
13. **Step 12** — Create `BlobUploadResultBuilder`
14. **Step 13** — Update `api-endpoints.md` documentation

---

## Testing Checklist

### Unit Tests — `BlobStorageServiceTests.cs`

- [ ] `UploadAsync_WithValidImageAndThumbnailRequested_UploadsOriginalAndThumbnail`
- [ ] `UploadAsync_WithValidImageAndNoThumbnailRequested_UploadsOriginalOnly`
- [ ] `UploadAsync_WithAudioFile_UploadsWithoutThumbnailRegardlessOfFlag`
- [ ] `UploadAsync_WithDocumentFile_UploadsWithoutThumbnail`
- [ ] `UploadAsync_InvalidatesStatsCache`
- [ ] `UploadAsync_WhenS3Throws_PropagatesException`
- [ ] `DeleteManyAsync_WithValidKeys_CallsRepositoryAndInvalidatesCache`
- [ ] `GetStatsAsync_WithQuotaConfigured_ReturnsUsedPctAndFreeBytes`
- [ ] `GetStatsAsync_WhenCalledTwiceWithinCacheTtl_OnlyCallsS3Once`
- [ ] `IsHealthyAsync_WhenBucketReachable_ReturnsTrue`
- [ ] `IsHealthyAsync_WhenS3Unreachable_ReturnsFalse`

### Unit Tests — `BlobStorageValidatorTests.cs`

- [ ] File null → fails
- [ ] File exceeds max size → fails
- [ ] Folder empty → fails
- [ ] Invalid folder name → fails (3 InlineData values)
- [ ] PDF in photos folder → fails
- [ ] Audio in photos folder → fails
- [ ] Valid image in photos folder → passes
- [ ] Valid audio in media-items folder → passes (Theory with `.mp3`, `.wav`)
- [ ] Valid document in media-items folder → passes
- [ ] Valid image in camp-locations / camp-photos folders → passes (Theory)

### Unit Tests — `BlobStorageHealthCheckTests.cs`

- [ ] Bucket unreachable → Degraded
- [ ] No quota configured → Healthy (no stats call)
- [ ] Usage below warning threshold → Healthy with data
- [ ] Usage above warning threshold → Degraded
- [ ] Usage above critical threshold → Unhealthy
- [ ] Exception thrown → Degraded with exception

### Integration Tests — `BlobStorageEndpointsTests.cs`

- [ ] Upload unauthenticated → 401
- [ ] Delete unauthenticated → 401
- [ ] Stats unauthenticated → 401
- [ ] Upload as Member with valid file → 200
- [ ] Upload with invalid folder → 400
- [ ] Delete as Member → 403
- [ ] Stats as Member → 403

### Manual verification (post-implementation)

- [ ] `POST /api/blobs/upload` with a real image → file appears in Hetzner bucket, URL is accessible
- [ ] `POST /api/blobs/upload` with `generateThumbnail=true` → thumbnail at `.../thumbs/....webp` URL
- [ ] `POST /api/blobs/upload` with `.mp3` file → no thumbnail, file accessible
- [ ] `POST /api/blobs/upload` with `.pdf` to `photos` folder → 400 validation error
- [ ] `GET /health` → includes `blob-storage` entry in JSON
- [ ] `GET /api/blobs/stats` as Admin → returns folder breakdown

---

## Error Response Format

All endpoints use `ApiResponse<T>` envelope:

```json
// 200 — successful upload
{
  "success": true,
  "data": {
    "fileUrl": "https://abuvi-media.fsn1.your-objectstorage.com/photos/abc/guid.jpg",
    "thumbnailUrl": "https://abuvi-media.fsn1.your-objectstorage.com/photos/abc/thumbs/guid.webp",
    "fileName": "guid.jpg",
    "contentType": "image/jpeg",
    "sizeBytes": 204800
  }
}

// 400 — validation failure
{
  "success": false,
  "error": {
    "message": "Datos no válidos",
    "code": "VALIDATION_ERROR",
    "details": [
      { "field": "File", "message": "El tipo de archivo no está permitido para esta carpeta" }
    ]
  }
}

// 204 — successful delete (no body)

// 403 — insufficient role (framework default, no custom body)
```

HTTP status code mapping:

| Status | HTTP Code |
|--------|-----------|
| Upload success | 200 |
| Delete success | 204 |
| Validation failure | 400 |
| Unauthenticated | 401 |
| Insufficient role | 403 |
| S3 error (unhandled) | 500 (via GlobalExceptionMiddleware) |

---

## Dependencies

### New NuGet packages

| Package | Version | Reason |
| --- | --- | --- |
| `AWSSDK.S3` | `3.*` | S3-compatible API client for Hetzner Object Storage |
| `SixLabors.ImageSharp` | `3.*` | Server-side thumbnail generation + WebP output |

### Test project

No new NuGet packages needed — `NSubstitute` and `FluentAssertions` are already present.

### No EF Core migration

This feature has no database schema changes.

---

## Notes

### Secret management

- Set credentials via `dotnet user-secrets` in development:

  ```bash
  dotnet user-secrets set "BlobStorage:AccessKeyId" "your-key" --project src/Abuvi.API
  dotnet user-secrets set "BlobStorage:SecretAccessKey" "your-secret" --project src/Abuvi.API
  ```

- In production: set `BlobStorage__AccessKeyId` and `BlobStorage__SecretAccessKey` as environment variables (double underscore = nested key in .NET configuration)
- Never commit credentials to `appsettings.json` — the section in appsettings has empty strings intentionally

### Hetzner Object Storage configuration

- S3 endpoint format: `https://<region>.your-objectstorage.com` (e.g. `https://fsn1.your-objectstorage.com`)
- Public URL format: `https://<bucket-name>.<region>.your-objectstorage.com`
- `ForcePathStyle = true` is **required** — Hetzner does not support virtual-hosted addressing
- Bucket must be configured with public read ACL at creation time (contact DevOps)

### Performance

- Large files are buffered in `MemoryStream` for thumbnail generation. At 50 MB max, this is acceptable. If file sizes grow, consider streaming directly to S3 (at the cost of not being able to generate thumbnails without a second download)
- `GetStatsAsync` triggers a full bucket list (paginated S3 calls). With 5-minute cache, this is called at most once per health poll period per app instance

### Business rules

- `generateThumbnail` flag is only honoured for image extensions — audio, video, and documents always get `thumbnailUrl = null`
- Storage keys are always constructed server-side using `Guid.NewGuid()` — user-provided file names never appear in S3 keys (prevents path traversal)
- Audio files (`.mp3`, `.wav`, `.ogg`, `.m4a`, `.flac`, `.aac`) are only allowed in `media-items` folder, not in `photos`, `camp-locations`, or `camp-photos`

### GDPR / RGPD

- No personal data is stored in blob storage or its metadata
- Medical information, dietary needs, and other sensitive fields remain in the encrypted database columns

### Language

- All user-facing validation messages and health check descriptions are in **Spanish** (project convention)
- Code, identifiers, and comments are in **English**

---

## Next Steps After Implementation

1. Verify all tests pass: `dotnet test src/Abuvi.Tests`
2. Verify build: `dotnet build src/Abuvi.API`
3. Configure Hetzner bucket with DevOps (public read ACL, region confirmation)
4. Set up secrets in dev environment and smoke test with a real upload
5. Once merged, proceed with `feat/media-50-aniversary` (first consumer of `IBlobStorageService`)

---

## Implementation Verification

- [ ] **Code Quality**: No TreatWarningsAsErrors violations; nullable reference types enabled and respected in all new files
- [ ] **Functionality**: All three endpoints return correct status codes; `BlobUploadResult.ThumbnailUrl` is null for audio/video/documents; `StorageQuotaBytes = 0` disables threshold checks
- [ ] **Testing**: ≥ 90% coverage with xUnit + FluentAssertions + NSubstitute; all listed test scenarios implemented; TDD RED→GREEN cycle followed
- [ ] **Security**: No credentials in source control; storage keys generated server-side; Admin role enforced on DELETE and GET /stats
- [ ] **Performance**: `GetStatsAsync` cached for 5 minutes; cache invalidated on upload and delete; Kestrel limit raised to 55 MB
- [ ] **Integration**: `BlobStorageHealthCheck` appears in `GET /health` response as `blob-storage` entry
- [ ] **Documentation**: `api-endpoints.md` updated with new endpoints and health check entry; `appsettings.json` has `BlobStorage` section

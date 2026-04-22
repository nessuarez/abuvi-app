# Blob Storage Repository — Enriched User Story

**Feature branch:** `feat/blob-storage`
**Related source:** `ai-specs/changes/feat-blob-storage/blob-storage-repo.md`
**Date enriched:** 2026-02-25

---

## Summary

Implement a Blob Storage service that enables the ABUVI backend to upload, retrieve, and manage binary files (photos, videos, audio, documents) hosted on **Hetzner Object Storage** (S3-compatible). This service will be consumed internally by the `Photos`, `MediaItems`, and `CampLocations` features to store and serve user-uploaded content.

---

## Context & Motivation

The data model already references blob storage URLs in several entities:

| Entity | Fields |
|---|---|
| `Photo` | `fileUrl` (max 2048), `thumbnailUrl` (max 2048) |
| `MediaItem` | `fileUrl` (max 2048), `thumbnailUrl` (max 2048, required for Photo/Video types) |
| `CampLocation` | `coverPhotoUrl` (max 2048) |
| `CampPhoto` | `photoUrl` (manually managed, max 1000 stored / 2000 in request) |

These fields currently have no backing storage. This task provisions the storage layer and exposes a backend `BlobStorageService` for use by other feature slices.

---

## Technology Decision

**Use Hetzner Object Storage** (already part of the Hetzner Cloud infrastructure):

- S3-compatible API → use `AWSSDK.S3` NuGet package
- Region: `fsn1` (Falkenstein) or `nbg1` (Nuremberg) — confirm with DevOps
- Endpoint: `https://<bucket-name>.your-storagebox.de` (or the custom Hetzner endpoint)
- Access via `AccessKeyId` + `SecretAccessKey` stored in environment variables / `dotnet user-secrets`
- No vendor lock-in: the `IBlobStorageService` abstraction allows swapping providers

**NuGet packages to add to `Abuvi.API.csproj`:**

```
AWSSDK.S3
SixLabors.ImageSharp          # server-side thumbnail generation for images
```

---

## Architecture — Vertical Slice

All blob storage code lives in a single feature slice:

```
src/Abuvi.API/
└── Features/
    └── BlobStorage/
        ├── BlobStorageEndpoints.cs       # Minimal API endpoint definitions
        ├── BlobStorageModels.cs          # Request/Response DTOs
        ├── BlobStorageService.cs         # Business logic: upload, delete, thumbnail
        ├── IBlobStorageService.cs        # Abstraction for DI & testing
        ├── BlobStorageRepository.cs      # S3 client wrapper (low-level)
        ├── IBlobStorageRepository.cs     # Abstraction
        ├── BlobStorageValidator.cs       # FluentValidation rules for upload requests
        └── BlobStorageExtensions.cs      # IServiceCollection extension for DI registration
```

**Registration in `Program.cs`:**

```csharp
builder.Services.AddBlobStorage(builder.Configuration);
// ...
app.MapBlobStorageEndpoints();
```

---

## Configuration

Add to `appsettings.json` (values via environment variables in production):

```json
{
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
}
```

Validate on startup:

```csharp
var bucketName = builder.Configuration["BlobStorage:BucketName"]
    ?? throw new InvalidOperationException("BlobStorage:BucketName is required");
```

---

## Data Model Changes

**No new database tables.** The `BlobStorageService` is a pure infrastructure service that returns URLs consumed by other features' entity fields.

Optionally add a `blobs` table for audit/orphan-detection (Phase 2 only). For Phase 1, URLs are stored directly in the referencing entity fields.

---

## File Organization in Bucket

Files are stored using a deterministic key pattern to support access control and cleanup:

```
abuvi-media/
├── photos/{photoAlbumId}/{guid}.{ext}            # Photo.fileUrl
├── photos/{photoAlbumId}/thumbs/{guid}.webp       # Photo.thumbnailUrl
├── media-items/{guid}.{ext}                       # MediaItem.fileUrl (images, video, docs, audio)
├── media-items/thumbs/{guid}.webp                 # MediaItem.thumbnailUrl (images/video only)
├── camp-locations/{campLocationId}/{guid}.{ext}   # CampLocation.coverPhotoUrl
└── camp-photos/{campId}/{guid}.{ext}              # CampPhoto.photoUrl
```

Audio files (`.mp3`, `.wav`, etc.) go into `media-items/` like any other `MediaItem`. No thumbnail is generated for audio.

All GUIDs are generated server-side (`Guid.NewGuid()`). Extensions are normalized to lowercase.

---

## API Endpoints

All endpoints under `/api/blobs`. Require **authentication** (JWT). Role-based access specified per endpoint.

### POST /api/blobs/upload

Upload a file. Returns blob metadata. **Roles: Admin, Board** (for albums and media items); **Member** can only upload to their own album (enforced at calling feature level — this endpoint accepts any authenticated user and the calling feature applies its own authorization).

**Request:** `multipart/form-data`

| Field | Type | Required | Notes |
|---|---|---|---|
| `file` | `IFormFile` | Yes | The binary file |
| `folder` | `string` | Yes | One of: `photos`, `media-items`, `camp-locations`, `camp-photos` |
| `contextId` | `Guid` | No | e.g. `photoAlbumId` or `campId`; appended to the path |
| `generateThumbnail` | `bool` | No | Default `false`; only applies to image files (ignored for audio/video/documents) |

**Response 200:**

```json
{
  "success": true,
  "data": {
    "fileUrl": "https://abuvi-media.fsn1.your-objectstorage.com/photos/abc123/def456.jpg",
    "thumbnailUrl": "https://abuvi-media.fsn1.your-objectstorage.com/photos/abc123/thumbs/def456.webp",
    "fileName": "def456.jpg",
    "contentType": "image/jpeg",
    "sizeBytes": 204800
  }
}
```

`thumbnailUrl` is `null` when `generateThumbnail` is `false` or file is not an image.

**Response 400 — validation failure:**

- File exceeds `MaxFileSizeBytes`
- File extension not in allowed list for its folder
- Folder value is not one of the allowed values

**Response 413 — payload too large:** returned by ASP.NET before reaching the endpoint if request body > configured limit.

---

### DELETE /api/blobs

Delete one or more blobs by their keys. **Roles: Admin only.**

**Request body:**

```json
{
  "blobKeys": [
    "photos/abc123/def456.jpg",
    "photos/abc123/thumbs/def456.webp"
  ]
}
```

**Response 204 No Content** on success.

---

### GET /api/blobs/stats

Returns storage usage statistics. **Roles: Admin only.**

**Response 200:**

```json
{
  "success": true,
  "data": {
    "totalObjects": 1523,
    "totalSizeBytes": 5368709120,
    "totalSizeHumanReadable": "5.0 GB",
    "byFolder": {
      "photos": { "objects": 800, "sizeBytes": 3221225472 },
      "media-items": { "objects": 300, "sizeBytes": 1610612736 },
      "camp-locations": { "objects": 23, "sizeBytes": 52428800 },
      "camp-photos": { "objects": 400, "sizeBytes": 483328000 }
    }
  }
}
```

---

### GET /api/blobs/health

Connectivity check (also wired into `/health`). **No authentication required.**

Returns `200 OK` when the bucket is reachable, `503` otherwise. This endpoint is internal — the standard `/health` endpoint already aggregates it.

---

## Service Interface

```csharp
// Features/BlobStorage/IBlobStorageService.cs
public interface IBlobStorageService
{
    /// <summary>Uploads a file stream and returns the public URL.</summary>
    Task<BlobUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string folder,
        Guid? contextId,
        bool generateThumbnail,
        CancellationToken ct);

    /// <summary>Deletes one or more blobs by key.</summary>
    Task DeleteManyAsync(IReadOnlyList<string> blobKeys, CancellationToken ct);

    /// <summary>Returns storage statistics grouped by top-level folder.
    /// Result is cached via IMemoryCache for 5 minutes to avoid S3 enumeration on every health poll.</summary>
    Task<BlobStorageStats> GetStatsAsync(CancellationToken ct);

    /// <summary>Returns true when the bucket is reachable.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct);
}
```

---

## Response / DTO Models

```csharp
// Features/BlobStorage/BlobStorageModels.cs

public record UploadBlobRequest
{
    public IFormFile File { get; init; } = null!;
    public string Folder { get; init; } = string.Empty;
    public Guid? ContextId { get; init; }
    public bool GenerateThumbnail { get; init; } = false;
}

public record BlobUploadResult(
    string FileUrl,
    string? ThumbnailUrl,
    string FileName,
    string ContentType,
    long SizeBytes);

public record DeleteBlobsRequest(IReadOnlyList<string> BlobKeys);

public record BlobStorageStats(
    int TotalObjects,
    long TotalSizeBytes,
    string TotalSizeHumanReadable,
    long? QuotaBytes,
    double? UsedPct,
    long? FreeByes,
    IReadOnlyDictionary<string, FolderStats> ByFolder);

public record FolderStats(int Objects, long SizeBytes);
```

---

## Validation Rules

```csharp
// Features/BlobStorage/BlobStorageValidator.cs
public class UploadBlobRequestValidator : AbstractValidator<UploadBlobRequest>
{
    private static readonly string[] AllowedFolders = ["photos", "media-items", "camp-locations", "camp-photos"];

    public UploadBlobRequestValidator(IOptions<BlobStorageOptions> options)
    {
        var cfg = options.Value;

        RuleFor(x => x.File)
            .NotNull().WithMessage("El archivo es obligatorio")
            .Must(f => f.Length <= cfg.MaxFileSizeBytes)
            .WithMessage($"El archivo no puede superar {cfg.MaxFileSizeBytes / 1_048_576} MB");

        RuleFor(x => x.Folder)
            .NotEmpty().WithMessage("La carpeta es obligatoria")
            .Must(f => AllowedFolders.Contains(f))
            .WithMessage("La carpeta especificada no es válida");

        RuleFor(x => x.File)
            .Must((req, file) => IsExtensionAllowed(req.Folder, file, cfg))
            .WithMessage("El tipo de archivo no está permitido para esta carpeta")
            .When(x => x.File is not null && !string.IsNullOrEmpty(x.Folder));
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

---

## Thumbnail Generation

- Thumbnails are generated **server-side** using `SixLabors.ImageSharp` only when `generateThumbnail = true` and the file is an image.
- Output format: always `.webp` (better compression, broad browser support).
- Dimensions: constrained to `ThumbnailWidthPx × ThumbnailHeightPx` (default 400×400), maintaining aspect ratio.
- Thumbnail key: same path as original file with `/thumbs/` sub-folder and `.webp` extension.
- **Videos, documents, and audio**: no thumbnail generated server-side. `thumbnailUrl` is always `null` for these types. The caller is responsible for providing a separate thumbnail file if needed (e.g. a waveform image for audio, or a cover frame for video).

---

## Security

| Concern | Implementation |
|---|---|
| **Authentication** | All endpoints require a valid JWT (except `/health` via existing global auth middleware) |
| **Authorization** | `DELETE` and `GET /stats` restricted to Admin role only; `POST /upload` requires authenticated user |
| **Public read access** | Bucket configured for **public read** on all objects (files are accessed directly via URL). No presigned URLs needed for downloads. |
| **Upload access** | Only the backend accesses the bucket directly (no client-side direct uploads). Credentials stored server-side only. |
| **Path traversal** | Keys are constructed server-side using `Guid.NewGuid()` — user input never forms the storage key. |
| **Content-Type validation** | File extension AND MIME type both validated; never trust the client-provided content type. |
| **File size limit** | Enforced at validation layer AND in ASP.NET Kestrel configuration (`MaxRequestBodySize`) |
| **GDPR** | No personal data is stored in blob storage or its metadata. Medical notes / allergies are in the encrypted database fields, not in blobs. |

---

## Health Check Integration

Add blob storage to the `/health` check:

```csharp
// In BlobStorageExtensions.cs or Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<BlobStorageHealthCheck>("blob-storage", HealthStatus.Degraded);
```

### Free Space Monitoring

Hetzner Object Storage does not expose a native "free space" API. Instead, the health check queries the current usage (`GetStatsAsync`) and compares it against a configurable quota (`StorageQuotaBytes`). To avoid an expensive S3 list-all-objects operation on every health poll, `GetStatsAsync` caches its result with `IMemoryCache` for 5 minutes.

**Status thresholds:**

| Used % | Health status | Description |
| --- | --- | --- |
| < `StorageWarningThresholdPct` (default 80%) | `Healthy` | Normal operation |
| ≥ 80% and < `StorageCriticalThresholdPct` (default 95%) | `Degraded` | Storage warning: consider cleanup |
| ≥ 95% | `Unhealthy` | Storage critical: uploads may fail |
| Bucket unreachable | `Degraded` | Connectivity issue |

**Health check output (included in `/health` response data):**

```json
{
  "blob-storage": {
    "status": "Degraded",
    "description": "Storage warning: 82.4% used (41.2 GB / 50 GB)",
    "duration": "00:00:00.1230000",
    "data": {
      "usedBytes": 44236742246,
      "quotaBytes": 53687091200,
      "freeBytes": 9450348954,
      "usedPct": 82.4
    }
  }
}
```

When `StorageQuotaBytes` is `0` (unconfigured), the health check only verifies bucket reachability and omits storage percentage data.

**Implementation sketch:**

```csharp
public class BlobStorageHealthCheck(
    IBlobStorageService blobService,
    IOptions<BlobStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            if (!await blobService.IsHealthyAsync(ct))
                return HealthCheckResult.Degraded("El bucket no está disponible");

            var quota = options.Value.StorageQuotaBytes;
            if (quota <= 0)
                return HealthCheckResult.Healthy("Bucket accesible");

            var stats = await blobService.GetStatsAsync(ct); // cached 5 min
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
}
```

Update `api-endpoints.md` health check table to include the new `blob-storage` check entry (failure status: `Degraded` for connectivity, `Unhealthy` for critical storage usage).

---

## Files to Create / Modify

### New files

| File | Purpose |
|---|---|
| `src/Abuvi.API/Features/BlobStorage/BlobStorageEndpoints.cs` | Endpoint definitions |
| `src/Abuvi.API/Features/BlobStorage/BlobStorageModels.cs` | DTOs and options |
| `src/Abuvi.API/Features/BlobStorage/IBlobStorageService.cs` | Service interface |
| `src/Abuvi.API/Features/BlobStorage/BlobStorageService.cs` | Service implementation |
| `src/Abuvi.API/Features/BlobStorage/IBlobStorageRepository.cs` | S3 client interface |
| `src/Abuvi.API/Features/BlobStorage/BlobStorageRepository.cs` | S3 client implementation |
| `src/Abuvi.API/Features/BlobStorage/BlobStorageValidator.cs` | FluentValidation rules |
| `src/Abuvi.API/Features/BlobStorage/BlobStorageExtensions.cs` | DI registration |
| `src/Abuvi.API/Features/BlobStorage/BlobStorageHealthCheck.cs` | Health check |
| `src/Abuvi.Tests/Unit/Features/BlobStorage/BlobStorageServiceTests.cs` | Unit tests |
| `src/Abuvi.Tests/Unit/Features/BlobStorage/BlobStorageValidatorTests.cs` | Validator tests |
| `src/Abuvi.Tests/Integration/Features/BlobStorage/BlobStorageEndpointsTests.cs` | Integration tests |
| `src/Abuvi.Tests/Helpers/Builders/BlobUploadResultBuilder.cs` | Test data builder |

### Modified files

| File | Change |
|---|---|
| `src/Abuvi.API/Program.cs` | Register `AddBlobStorage()`, map endpoints, add health check |
| `src/Abuvi.API/appsettings.json` | Add `BlobStorage` section (no secrets) |
| `ai-specs/specs/api-endpoints.md` | Document new `/api/blobs/*` endpoints and health check entry |

---

## Testing Requirements

### Unit Tests (`BlobStorageServiceTests.cs`)

Test naming convention: `MethodName_StateUnderTest_ExpectedBehavior`

| Scenario | Expected result |
|---|---|
| `UploadAsync_WithValidImageAndThumbnailRequested_UploadsOriginalAndThumbnail` | Both `fileUrl` and `thumbnailUrl` populated |
| `UploadAsync_WithValidImageAndNoThumbnailRequested_UploadsOriginalOnly` | `thumbnailUrl` is null |
| `UploadAsync_WithValidAudioFile_UploadsWithoutThumbnail` | `thumbnailUrl` is null; file uploaded to `media-items/` |
| `UploadAsync_WithNonImageFile_DoesNotGenerateThumbnail` | `thumbnailUrl` is null regardless of flag |
| `UploadAsync_WhenS3Throws_PropagatesException` | Exception propagates (no swallowing) |
| `DeleteManyAsync_WithValidKeys_CallsS3DeleteObjects` | S3 repository called with correct keys |
| `IsHealthyAsync_WhenBucketReachable_ReturnsTrue` | Returns `true` |
| `IsHealthyAsync_WhenS3Unreachable_ReturnsFalse` | Returns `false` (no exception thrown) |
| `GetStatsAsync_WithQuotaConfigured_ReturnsUsedPctAndFreeBytes` | Quota fields populated |
| `GetStatsAsync_WhenCalledTwiceWithinCacheTtl_OnlyCallsS3Once` | S3 list called once (cache hit on second call) |

### Validator Tests (`BlobStorageValidatorTests.cs`)

| Scenario | Expected result |
|---|---|
| File is null | Validation fails |
| File exceeds max size | Validation fails |
| Folder is not in allowed list | Validation fails |
| Invalid extension for folder (e.g. `.pdf` to `photos`) | Validation fails |
| Valid image in `photos` folder | Validation passes |
| Valid audio (`.mp3`) in `media-items` folder | Validation passes |
| Audio file (`.mp3`) in `photos` folder | Validation fails |
| Valid document in `media-items` folder | Validation passes |

### Integration Tests (`BlobStorageEndpointsTests.cs`)

Use `WebApplicationFactory<Program>` with a mocked `IBlobStorageRepository` (NSubstitute) to avoid actual S3 calls.

| Scenario | Expected result |
|---|---|
| `POST /api/blobs/upload` unauthenticated | 401 Unauthorized |
| `POST /api/blobs/upload` valid file | 200 with `BlobUploadResult` |
| `POST /api/blobs/upload` file too large | 400 Bad Request |
| `DELETE /api/blobs` as Admin | 204 No Content |
| `DELETE /api/blobs` as Member | 403 Forbidden |
| `GET /api/blobs/stats` as Admin | 200 with stats |
| `GET /api/blobs/stats` as Member | 403 Forbidden |

### Coverage Threshold

90% branches, functions, lines, and statements.

---

## Non-Functional Requirements

| Requirement | Target |
|---|---|
| **Max file size** | 50 MB (configurable via `BlobStorage:MaxFileSizeBytes`) |
| **Upload throughput** | Stream directly to S3; do not buffer entire file in memory |
| **Thumbnail generation time** | < 2 seconds for images up to 10 MP |
| **Object storage costs** | Use Hetzner Object Storage pricing (~€ 0.0059/GB/month) |
| **Bucket lifecycle** | No automatic expiry; manual deletion via Admin API |
| **Observability** | Log every upload/delete with file key, size, and user ID via structured logging |
| **Stats caching** | `GetStatsAsync` caches result in `IMemoryCache` for 5 minutes to avoid expensive S3 list on every health poll |
| **Configuration reload** | `BlobStorageOptions` bound via `IOptions<BlobStorageOptions>` (no reload needed for static keys) |

---

## Out of Scope (follow-up tickets)

The following use cases are intentionally excluded from this ticket. The `IBlobStorageService` abstraction will support them without changes once the data model tickets are created:

| Use case | Spec file | Key entities |
| --- | --- | --- |
| Media real en la página del 50 aniversario | [`follow-ups/feat-media-50-aniversary.md`](follow-ups/feat-media-50-aniversary.md) | `Memory`, `MediaItem` |
| Galerías de fotos de ediciones de campamento | [`follow-ups/feat-media-camps.md`](follow-ups/feat-media-camps.md) | `PhotoAlbum`, `Photo` |
| Archivo histórico multimedia | [`follow-ups/feat-media-memories-archive.md`](follow-ups/feat-media-memories-archive.md) | `Memory`, `MediaItem`, `CampLocation` |
| Fotos de perfil de `FamilyMember` y `FamilyUnit` | [`follow-ups/feat-media-profile-photos.md`](follow-ups/feat-media-profile-photos.md) | `FamilyMember`, `FamilyUnit` |

All follow-up tickets reuse `IBlobStorageService.UploadAsync()` directly. No changes to the blob storage feature slice are needed when implementing them.

---

## Acceptance Criteria

- [ ] `POST /api/blobs/upload` accepts image, video, audio, and document files and returns a public URL.
- [ ] Audio files (`.mp3`, `.wav`, `.ogg`, `.m4a`, `.flac`, `.aac`) are accepted in `media-items` and rejected in `photos`/`camp-photos`.
- [ ] When `generateThumbnail=true` and the file is an image, a `.webp` thumbnail is uploaded and its URL returned.
- [ ] Audio, video, and document uploads always return `thumbnailUrl: null`.
- [ ] Files are rejected if extension is not in the allowed list for the target folder.
- [ ] Files are rejected if they exceed `MaxFileSizeBytes`.
- [ ] File storage keys are always generated server-side (no user input in keys).
- [ ] `DELETE /api/blobs` is restricted to Admin role and successfully removes blobs from S3.
- [ ] `GET /api/blobs/stats` is restricted to Admin role and returns usage metrics including quota, used %, and free bytes when `StorageQuotaBytes > 0`.
- [ ] `/health` endpoint includes a `blob-storage` check entry with storage percentage data.
- [ ] Health check returns `Degraded` when storage exceeds `StorageWarningThresholdPct` and `Unhealthy` when it exceeds `StorageCriticalThresholdPct`.
- [ ] `GetStatsAsync` is cached for 5 minutes (verified by test: S3 list called once for two back-to-back calls).
- [ ] All unit and integration tests pass with ≥ 90% coverage.
- [ ] No secrets are committed to source control.
- [ ] `api-endpoints.md` is updated to document the new endpoints and health check entry.
- [ ] Structured logs include file key, size, and `userId` for every upload and delete operation.

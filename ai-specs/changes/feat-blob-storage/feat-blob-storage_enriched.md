# Blob Storage Repository — Enriched User Story

**Feature branch:** `feat/blob-storage`
**Related source:** `ai-specs/changes/feat-blob-storage/blob-storage-repo.md`
**Date enriched:** 2026-02-25

---

## Summary

Implement a Blob Storage service that enables the ABUVI backend to upload, retrieve, and manage binary files (photos, videos, documents) hosted on **Hetzner Object Storage** (S3-compatible). This service will be consumed internally by the `Photos`, `MediaItems`, and `CampLocations` features to store and serve user-uploaded content.

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
    "AllowedDocumentExtensions": [".pdf", ".doc", ".docx"],
    "ThumbnailWidthPx": 400,
    "ThumbnailHeightPx": 400
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
├── media-items/{guid}.{ext}                       # MediaItem.fileUrl
├── media-items/thumbs/{guid}.webp                 # MediaItem.thumbnailUrl
├── camp-locations/{campLocationId}/{guid}.{ext}   # CampLocation.coverPhotoUrl
└── camp-photos/{campId}/{guid}.{ext}              # CampPhoto.photoUrl
```

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
| `generateThumbnail` | `bool` | No | Default `false`; only applies to image files |

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

    /// <summary>Returns storage statistics grouped by top-level folder.</summary>
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
- Videos/documents: no thumbnail generated server-side (thumbnail must be provided separately if needed).

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

`BlobStorageHealthCheck` calls `IBlobStorageService.IsHealthyAsync()`. Failure status is `Degraded` (not `Unhealthy`) because the app can still serve read-only content if blob storage is temporarily unavailable.

Update `api-endpoints.md` health check table to include the new `blob-storage` check entry.

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
| `UploadAsync_WithNonImageFile_DoesNotGenerateThumbnail` | `thumbnailUrl` is null regardless of flag |
| `UploadAsync_WhenS3Throws_PropagatesException` | Exception propagates (no swallowing) |
| `DeleteManyAsync_WithValidKeys_CallsS3DeleteObjects` | S3 repository called with correct keys |
| `IsHealthyAsync_WhenBucketReachable_ReturnsTrue` | Returns `true` |
| `IsHealthyAsync_WhenS3Unreachable_ReturnsFalse` | Returns `false` (no exception thrown) |

### Validator Tests (`BlobStorageValidatorTests.cs`)

| Scenario | Expected result |
|---|---|
| File is null | Validation fails |
| File exceeds max size | Validation fails |
| Folder is not in allowed list | Validation fails |
| Invalid extension for folder (e.g. `.pdf` to `photos`) | Validation fails |
| Valid image in `photos` folder | Validation passes |
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
| **Configuration reload** | `BlobStorageOptions` bound via `IOptions<BlobStorageOptions>` (no reload needed for static keys) |

---

## Out of Scope (follow-up tickets)

The following use cases are intentionally excluded from this ticket. The `IBlobStorageService` abstraction will support them without changes once the data model tickets are created:

| Use case | Entities affected | Action required |
| --- | --- | --- |
| Foto de perfil de `FamilyMember` | Add `profilePhotoUrl` field (max 2048, optional) | New ticket: **feat/family-member-profile-photo** |
| Foto de familia de `FamilyUnit` | Add `profilePhotoUrl` field (max 2048, optional) | New ticket: **feat/family-unit-profile-photo** |

When those tickets are implemented, they will:

1. Add a new folder `profile-photos/family-members/{familyMemberId}/` and `profile-photos/family-units/{familyUnitId}/` to the bucket key schema.
2. Add `PUT /api/family-members/{id}/profile-photo` and `PUT /api/family-units/{id}/profile-photo` endpoints in their own feature slices.
3. Reuse `IBlobStorageService.UploadAsync()` directly — no changes to the blob storage feature slice needed.

---

## Acceptance Criteria

- [ ] `POST /api/blobs/upload` accepts a file, uploads it to Hetzner Object Storage, and returns a public URL.
- [ ] When `generateThumbnail=true` and the file is an image, a `.webp` thumbnail is uploaded and its URL returned.
- [ ] Files are rejected if extension is not in the allowed list for the target folder.
- [ ] Files are rejected if they exceed `MaxFileSizeBytes`.
- [ ] File storage keys are always generated server-side (no user input in keys).
- [ ] `DELETE /api/blobs` is restricted to Admin role and successfully removes blobs from S3.
- [ ] `GET /api/blobs/stats` is restricted to Admin role and returns real storage metrics.
- [ ] `/health` endpoint includes a `blob-storage` check entry.
- [ ] All unit and integration tests pass with ≥ 90% coverage.
- [ ] No secrets are committed to source control.
- [ ] `api-endpoints.md` is updated to document the new endpoints.
- [ ] Structured logs include file key, size, and `userId` for every upload and delete operation.

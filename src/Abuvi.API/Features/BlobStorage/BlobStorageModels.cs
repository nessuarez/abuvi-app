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

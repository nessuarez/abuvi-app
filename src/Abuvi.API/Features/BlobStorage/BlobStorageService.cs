using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

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

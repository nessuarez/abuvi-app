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

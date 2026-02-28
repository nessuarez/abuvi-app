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

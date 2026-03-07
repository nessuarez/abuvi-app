using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Abuvi.API.Features.BlobStorage;

public sealed class BlobStorageRepository : IBlobStorageRepository, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly BlobStorageOptions _options;
    private bool _disposed;

    public BlobStorageRepository(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        var endpoint = _options.Endpoint;
        if (!string.IsNullOrEmpty(endpoint) && !endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            endpoint = $"https://{endpoint}";

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
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
            AutoCloseStream = false, // Caller manages stream lifetime
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
            results.AddRange(response.S3Objects.Select(o => (o.Key, o.Size ?? 0L)));
            continuationToken = (response.IsTruncated == true) ? response.NextContinuationToken : null;
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

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

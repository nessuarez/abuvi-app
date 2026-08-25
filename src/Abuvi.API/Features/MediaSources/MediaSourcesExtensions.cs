namespace Abuvi.API.Features.MediaSources;

public static class MediaSourcesExtensions
{
    public static IServiceCollection AddMediaSources(this IServiceCollection services)
    {
        services.AddScoped<IMediaSourcesRepository, MediaSourcesRepository>();
        services.AddScoped<MediaSourcesService>();
        return services;
    }
}

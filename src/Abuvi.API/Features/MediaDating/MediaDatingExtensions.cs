namespace Abuvi.API.Features.MediaDating;

public static class MediaDatingExtensions
{
    public static IServiceCollection AddMediaDating(this IServiceCollection services)
    {
        services.AddScoped<IMediaDatingRepository, MediaDatingRepository>();
        services.AddScoped<MediaDatingService>();
        return services;
    }
}

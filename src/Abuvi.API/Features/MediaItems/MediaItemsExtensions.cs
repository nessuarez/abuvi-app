namespace Abuvi.API.Features.MediaItems;

public static class MediaItemsExtensions
{
    public static IServiceCollection AddMediaItems(this IServiceCollection services)
    {
        services.AddScoped<IMediaItemsRepository, MediaItemsRepository>();
        services.AddScoped<MediaItemsService>();
        return services;
    }
}

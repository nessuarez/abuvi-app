namespace Abuvi.API.Features.MediaThemes;

public static class MediaThemesExtensions
{
    public static IServiceCollection AddMediaThemes(this IServiceCollection services)
    {
        services.AddScoped<IMediaThemesRepository, MediaThemesRepository>();
        services.AddScoped<MediaThemesService>();
        return services;
    }
}

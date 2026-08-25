namespace Abuvi.API.Features.MediaComments;

public static class MediaCommentsExtensions
{
    public static IServiceCollection AddMediaComments(this IServiceCollection services)
    {
        services.AddScoped<IMediaCommentsRepository, MediaCommentsRepository>();
        services.AddScoped<MediaCommentsService>();
        return services;
    }
}

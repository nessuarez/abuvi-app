namespace Abuvi.API.Features.Memories;

public static class MemoriesExtensions
{
    public static IServiceCollection AddMemories(this IServiceCollection services)
    {
        services.AddScoped<IMemoriesRepository, MemoriesRepository>();
        services.AddScoped<MemoriesService>();
        return services;
    }
}

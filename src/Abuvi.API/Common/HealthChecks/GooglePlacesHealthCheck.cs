namespace Abuvi.API.Common.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class GooglePlacesHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["GooglePlaces:ApiKey"];

        return string.IsNullOrWhiteSpace(apiKey)
            ? Task.FromResult(HealthCheckResult.Unhealthy("Google Places API key is not configured"))
            : Task.FromResult(HealthCheckResult.Healthy("Google Places API key is configured"));
    }
}

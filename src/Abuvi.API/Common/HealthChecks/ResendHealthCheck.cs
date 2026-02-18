namespace Abuvi.API.Common.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class ResendHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Resend:ApiKey"];

        return string.IsNullOrWhiteSpace(apiKey)
            ? Task.FromResult(HealthCheckResult.Unhealthy("Resend API key is not configured"))
            : Task.FromResult(HealthCheckResult.Healthy("Resend API key is configured"));
    }
}

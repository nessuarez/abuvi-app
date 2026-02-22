namespace Abuvi.API.Common.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class SeqHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var serverUrl = configuration["Seq:ServerUrl"];

        if (string.IsNullOrWhiteSpace(serverUrl))
            return HealthCheckResult.Degraded("Seq server URL is not configured");

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"{serverUrl.TrimEnd('/')}/api", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Seq is reachable")
                : HealthCheckResult.Degraded($"Seq returned unexpected status {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Seq is unreachable: {ex.Message}");
        }
    }
}

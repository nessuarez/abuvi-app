using Abuvi.API.Features.BlobStorage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Abuvi.API.Common.HealthChecks;

public class BlobStorageHealthCheck(
    IBlobStorageService blobService,
    IOptions<BlobStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await blobService.IsHealthyAsync(cancellationToken))
                return HealthCheckResult.Degraded("El bucket no está disponible");

            var quota = options.Value.StorageQuotaBytes;
            if (quota <= 0)
                return HealthCheckResult.Healthy("Bucket accesible");

            var stats = await blobService.GetStatsAsync(cancellationToken); // cached 5 min
            var usedPct = (double)stats.TotalSizeBytes / quota * 100;

            var data = new Dictionary<string, object>
            {
                ["usedBytes"] = stats.TotalSizeBytes,
                ["quotaBytes"] = quota,
                ["freeBytes"] = quota - stats.TotalSizeBytes,
                ["usedPct"] = Math.Round(usedPct, 1)
            };

            var desc = $"{usedPct:F1}% usado ({stats.TotalSizeHumanReadable} / {FormatBytes(quota)})";

            if (usedPct >= options.Value.StorageCriticalThresholdPct)
                return HealthCheckResult.Unhealthy($"Almacenamiento crítico: {desc}", data: data);

            if (usedPct >= options.Value.StorageWarningThresholdPct)
                return HealthCheckResult.Degraded($"Advertencia de almacenamiento: {desc}", data: data);

            return HealthCheckResult.Healthy($"Bucket accesible. {desc}", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Error al verificar blob storage", ex);
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024          => $"{bytes / 1024.0:F1} KB",
        _                => $"{bytes} B"
    };
}

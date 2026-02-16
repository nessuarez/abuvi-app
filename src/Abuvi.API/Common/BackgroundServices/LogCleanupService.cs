using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Common.BackgroundServices;

public class LogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogCleanupService> _logger;
    private readonly int _retentionDays;
    private readonly TimeSpan _interval = TimeSpan.FromDays(1);

    public LogCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<LogCleanupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionDays = configuration.GetValue<int>("LogRetention:RetentionDays", 90);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Log cleanup service started. Retention: {Days} days, Interval: {Interval}",
            _retentionDays, _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

                var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);

                // Use raw SQL for better performance
                var deletedCount = await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM logs WHERE timestamp < {0}",
                    cutoffDate,
                    stoppingToken);

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Deleted {Count} log entries older than {CutoffDate}",
                        deletedCount, cutoffDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during log cleanup");
            }
        }
    }
}

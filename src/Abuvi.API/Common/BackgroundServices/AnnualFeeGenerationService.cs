using Abuvi.API.Features.Memberships;

namespace Abuvi.API.Common.BackgroundServices;

public class AnnualFeeGenerationService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<AnnualFeeGenerationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            // Calculate time until next January 1st at 00:00 UTC
            var nextRun = CalculateNextRunTime(now);

            logger.LogInformation(
                "Annual fee generation service started. Next run scheduled for {NextRun:yyyy-MM-dd HH:mm:ss} UTC",
                nextRun);

            // Wait until the scheduled time
            var delay = nextRun - now;

            try
            {
                // Task.Delay maximum is Int32.MaxValue ms (~24.8 days), so break large delays into chunks
                var maxChunk = TimeSpan.FromHours(12);
                while (DateTime.UtcNow < nextRun)
                {
                    var remaining = nextRun - DateTime.UtcNow;
                    var chunk = remaining < maxChunk ? remaining : maxChunk;
                    await Task.Delay(chunk, stoppingToken);
                }

                // Generate fees when the time comes
                await GenerateAnnualFeesAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Service is being stopped
                logger.LogInformation("Annual fee generation service is stopping");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in annual fee generation service");
                // Wait a bit before retrying to avoid tight loop on persistent errors
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private static DateTime CalculateNextRunTime(DateTime now)
    {
        var nextYear = now.Year + 1;
        var nextRun = new DateTime(nextYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // If we're already past January 1st this year, schedule for next year
        // If we're before January 1st this year, schedule for this year
        var thisYearRun = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        if (now < thisYearRun)
        {
            return thisYearRun;
        }

        return nextRun;
    }

    private async Task GenerateAnnualFeesAsync(CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;

        logger.LogInformation(
            "Starting annual fee generation for year {Year}",
            currentYear);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IMembershipsRepository>();

            var activeMemberships = await repository.GetActiveAsync(ct);
            var defaultAmount = configuration.GetValue<decimal>("Membership:AnnualFeeAmount", 50.00m);

            logger.LogInformation(
                "Found {Count} active memberships. Default fee amount: {Amount:C}",
                activeMemberships.Count,
                defaultAmount);

            var generatedCount = 0;
            var skippedCount = 0;

            foreach (var membership in activeMemberships)
            {
                try
                {
                    // Check if fee already exists for this year
                    var existingFee = await repository.GetCurrentYearFeeAsync(membership.Id, ct);
                    if (existingFee is not null)
                    {
                        logger.LogDebug(
                            "Fee already exists for membership {MembershipId} for year {Year}, skipping",
                            membership.Id,
                            currentYear);
                        skippedCount++;
                        continue;
                    }

                    var fee = new MembershipFee
                    {
                        Id = Guid.NewGuid(),
                        MembershipId = membership.Id,
                        Year = currentYear,
                        Amount = defaultAmount,
                        Status = FeeStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await repository.AddFeeAsync(fee, ct);
                    generatedCount++;

                    logger.LogInformation(
                        "Generated fee {FeeId} for membership {MembershipId} (Member: {MemberName}), amount {Amount:C}",
                        fee.Id,
                        membership.Id,
                        $"{membership.FamilyMember.FirstName} {membership.FamilyMember.LastName}",
                        fee.Amount);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Error generating fee for membership {MembershipId}",
                        membership.Id);
                    // Continue with next membership
                }
            }

            logger.LogInformation(
                "Annual fee generation completed for year {Year}. Generated: {GeneratedCount}, Skipped: {SkippedCount}, Total: {TotalCount}",
                currentYear,
                generatedCount,
                skippedCount,
                activeMemberships.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Critical error during annual fee generation for year {Year}",
                currentYear);
            throw;
        }
    }
}

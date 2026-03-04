namespace Abuvi.Tests.Unit.Features.BlobStorage;

using Abuvi.API.Common.HealthChecks;
using Abuvi.API.Features.BlobStorage;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

public class BlobStorageHealthCheckTests
{
    private readonly IBlobStorageService _service = Substitute.For<IBlobStorageService>();
    private readonly BlobStorageOptions _options = new()
    {
        StorageQuotaBytes = 107_374_182_400L, // 100 GB
        StorageWarningThresholdPct = 80,
        StorageCriticalThresholdPct = 95
    };

    private BlobStorageHealthCheck CreateSut(BlobStorageOptions? opts = null) =>
        new(_service, Options.Create(opts ?? _options));

    private static HealthCheckContext CreateContext(IHealthCheck check) =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "blob-storage", check, HealthStatus.Degraded, null)
        };

    [Fact]
    public async Task CheckHealthAsync_WhenBucketUnreachable_ReturnsDegraded()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateSut();

        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNoQuotaConfigured_ReturnsHealthyWithoutStats()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        var sut = CreateSut(new BlobStorageOptions { StorageQuotaBytes = 0 });

        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Healthy);
        await _service.DidNotReceive().GetStatsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUsageBelowWarning_ReturnsHealthy()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        _service.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(
            BuildStats(usedBytes: 10_737_418_240L)); // 10 GB = 10% of 100 GB

        var sut = CreateSut();
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("usedPct");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUsageAboveWarning_ReturnsDegraded()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        _service.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(
            BuildStats(usedBytes: 85_899_345_920L)); // 80 GB = 80% of 100 GB

        var sut = CreateSut();
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUsageAboveCritical_ReturnsUnhealthy()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(true);
        _service.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(
            BuildStats(usedBytes: 102_005_473_280L)); // ~95 GB = 95% of 100 GB

        var sut = CreateSut();
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsDegraded()
    {
        _service.IsHealthyAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Network error"));
        var sut = CreateSut();

        var result = await sut.CheckHealthAsync(CreateContext(sut));

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Exception.Should().NotBeNull();
    }

    private BlobStorageStats BuildStats(long usedBytes) =>
        new(TotalObjects: 100,
            TotalSizeBytes: usedBytes,
            TotalSizeHumanReadable: $"{usedBytes / 1_073_741_824.0:F1} GB",
            QuotaBytes: _options.StorageQuotaBytes,
            UsedPct: Math.Round((double)usedBytes / _options.StorageQuotaBytes * 100, 1),
            FreeBytes: _options.StorageQuotaBytes - usedBytes,
            ByFolder: new Dictionary<string, FolderStats>());
}

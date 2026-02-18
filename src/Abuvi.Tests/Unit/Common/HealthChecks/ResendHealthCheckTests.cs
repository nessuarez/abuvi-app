namespace Abuvi.Tests.Unit.Common.HealthChecks;

using Abuvi.API.Common.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class ResendHealthCheckTests
{
    private static HealthCheckContext CreateContext(ResendHealthCheck check)
        => new()
        {
            Registration = new HealthCheckRegistration(
                "resend", check, HealthStatus.Degraded, null)
        };

    [Fact]
    public async Task CheckHealthAsync_WhenApiKeyIsConfigured_ReturnsHealthy()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Resend:ApiKey", "re_abc123")])
            .Build();
        var sut = new ResendHealthCheck(config);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Resend API key is configured");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckHealthAsync_WhenApiKeyIsMissingOrEmpty_ReturnsUnhealthy(string? apiKey)
    {
        // Arrange
        var inMemory = apiKey is null
            ? Array.Empty<KeyValuePair<string, string?>>()
            : [new KeyValuePair<string, string?>("Resend:ApiKey", apiKey)];

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();
        var sut = new ResendHealthCheck(config);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Resend API key is not configured");
    }
}

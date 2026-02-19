namespace Abuvi.Tests.Unit.Common.HealthChecks;

using Abuvi.API.Common.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class GooglePlacesHealthCheckTests
{
    private static HealthCheckContext CreateContext(GooglePlacesHealthCheck check)
        => new()
        {
            Registration = new HealthCheckRegistration(
                "google-places", check, HealthStatus.Degraded, null)
        };

    [Fact]
    public async Task CheckHealthAsync_WhenApiKeyIsConfigured_ReturnsHealthy()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("GooglePlaces:ApiKey", "AIzaSy_abc123")])
            .Build();
        var sut = new GooglePlacesHealthCheck(config);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Google Places API key is configured");
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
            : [new KeyValuePair<string, string?>("GooglePlaces:ApiKey", apiKey)];

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();
        var sut = new GooglePlacesHealthCheck(config);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Google Places API key is not configured");
    }
}

using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace Abuvi.Tests.Integration;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Endpoint_Returns_StructuredJsonResponse()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — response must be valid JSON with the expected shape
        // (HTTP status may be 503 in test env if DB is unavailable, but shape must be correct)
        var act = () => JsonDocument.Parse(content);
        act.Should().NotThrow("health endpoint must return valid JSON");

        using var doc = JsonDocument.Parse(content);
        doc.RootElement.TryGetProperty("status", out _).Should().BeTrue("response must include 'status'");
        doc.RootElement.TryGetProperty("entries", out var entries).Should().BeTrue("response must include 'entries'");
        entries.TryGetProperty("database", out _).Should().BeTrue("entries must include 'database' check");
        entries.TryGetProperty("resend", out _).Should().BeTrue("entries must include 'resend' check");
    }

    [Fact]
    public async Task Health_Endpoint_IsAccessibleWithoutAuthentication()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert — must not return 401 Unauthorized or 403 Forbidden
        ((int)response.StatusCode).Should().NotBe(401, "health endpoint must be publicly accessible");
        ((int)response.StatusCode).Should().NotBe(403, "health endpoint must be publicly accessible");
    }
}

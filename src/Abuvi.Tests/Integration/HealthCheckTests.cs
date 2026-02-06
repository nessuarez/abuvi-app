using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
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
    public async Task Health_Endpoint_Returns_Healthy_Status()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }
}

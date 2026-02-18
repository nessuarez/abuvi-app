using Abuvi.API.Common.BackgroundServices;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace Abuvi.Tests.Unit.BackgroundServices;

public class AnnualFeeGenerationServiceTests
{
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var configuration = Substitute.For<IConfiguration>();
        var logger = Substitute.For<ILogger<AnnualFeeGenerationService>>();

        // Act
        var service = new AnnualFeeGenerationService(serviceProvider, configuration, logger);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task Service_CanStartAndStop()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var configuration = Substitute.For<IConfiguration>();
        var logger = Substitute.For<ILogger<AnnualFeeGenerationService>>();

        var service = new AnnualFeeGenerationService(serviceProvider, configuration, logger);

        // Act
        var cts = new CancellationTokenSource();
        var startTask = service.StartAsync(cts.Token);

        // Give it a moment to start
        await Task.Delay(100);

        // Stop the service
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        startTask.IsCompleted.Should().BeTrue();
    }
}

namespace Abuvi.Tests.Unit.Common.HealthChecks;

using System.Net;
using Abuvi.API.Common.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

public class SeqHealthCheckTests
{
    private static HealthCheckContext CreateContext(SeqHealthCheck check)
        => new()
        {
            Registration = new HealthCheckRegistration(
                "seq", check, HealthStatus.Degraded, null)
        };

    private static IHttpClientFactory CreateFactory(HttpStatusCode? statusCode = null, Exception? exception = null)
    {
        var handler = Substitute.For<HttpMessageHandler>();
        handler
            .GetType()
            .GetMethod("SendAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(handler, [Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()])
            .Returns(statusCode.HasValue
                ? Task.FromResult(new HttpResponseMessage(statusCode.Value))
                : Task.FromException<HttpResponseMessage>(exception!));

        var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        return factory;
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServerUrlIsNotConfigured_ReturnsDegraded()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var factory = Substitute.For<IHttpClientFactory>();
        var sut = new SeqHealthCheck(config, factory);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Seq server URL is not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServerUrlIsEmpty_ReturnsDegraded()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Seq:ServerUrl", "")])
            .Build();
        var factory = Substitute.For<IHttpClientFactory>();
        var sut = new SeqHealthCheck(config, factory);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Seq server URL is not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServerUrlIsWhitespace_ReturnsDegraded()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Seq:ServerUrl", "   ")])
            .Build();
        var factory = Substitute.For<IHttpClientFactory>();
        var sut = new SeqHealthCheck(config, factory);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Seq server URL is not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSeqResponds200_ReturnsHealthy()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Seq:ServerUrl", "http://localhost:5341")])
            .Build();

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var sut = new SeqHealthCheck(config, factory);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Seq is reachable");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSeqRespondsNon2xx_ReturnsDegraded()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Seq:ServerUrl", "http://localhost:5341")])
            .Build();

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var sut = new SeqHealthCheck(config, factory);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Seq returned unexpected status 503");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSeqThrowsHttpRequestException_ReturnsDegraded()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Seq:ServerUrl", "http://localhost:5341")])
            .Build();

        var exception = new HttpRequestException("Connection refused");
        var handler = new FakeHttpMessageHandler(exception);
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var sut = new SeqHealthCheck(config, factory);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().StartWith("Seq is unreachable:");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSeqThrowsTaskCanceledException_ReturnsDegraded()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Seq:ServerUrl", "http://localhost:5341")])
            .Build();

        var exception = new TaskCanceledException("Request timed out");
        var handler = new FakeHttpMessageHandler(exception);
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var sut = new SeqHealthCheck(config, factory);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().StartWith("Seq is unreachable:");
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public FakeHttpMessageHandler(HttpResponseMessage response) => _response = response;
        public FakeHttpMessageHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_exception is not null)
                return Task.FromException<HttpResponseMessage>(_exception);

            return Task.FromResult(_response!);
        }
    }
}

namespace Abuvi.Tests.Integration.Features.BlobStorage;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.BlobStorage;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

public class BlobStorageEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BlobStorageEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // Override IBlobStorageRepository and IBlobStorageService with NSubstitute mocks
        // so tests never touch real S3
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove real registrations
                var repoDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IBlobStorageRepository));
                if (repoDescriptor != null) services.Remove(repoDescriptor);

                var svcDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IBlobStorageService));
                if (svcDescriptor != null) services.Remove(svcDescriptor);

                // Register mocks
                var mockRepo = Substitute.For<IBlobStorageRepository>();
                var mockService = Substitute.For<IBlobStorageService>();

                mockService.UploadAsync(
                    Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                    .Returns(new BlobUploadResult(
                        "https://cdn.example.com/photos/abc/test.jpg",
                        null,
                        "test.jpg",
                        "image/jpeg",
                        1024));

                mockService.GetStatsAsync(Arg.Any<CancellationToken>())
                    .Returns(new BlobStorageStats(10, 1024, "1 KB", null, null, null,
                        new Dictionary<string, FolderStats>()));

                services.AddSingleton(mockRepo);
                services.AddSingleton(mockService);
            });
        });
    }

    // ── Authentication ────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var content = CreateValidUploadContent("photo.jpg", "image/jpeg");

        var response = await client.PostAsync("/api/blobs/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBlobs_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/blobs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStats_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/blobs/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_AsAuthenticatedMember_WithValidFile_Returns200()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = CreateValidUploadContent("photo.jpg", "image/jpeg");

        var response = await client.PostAsync("/api/blobs/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<BlobUploadResult>>();
        result!.Success.Should().BeTrue();
        result.Data!.FileUrl.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Upload_WithInvalidFolder_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = CreateValidUploadContent("photo.jpg", "image/jpeg", folder: "invalid-folder");

        var response = await client.PostAsync("/api/blobs/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBlobs_AsMember_Returns403()
    {
        var client = await CreateAuthenticatedClientAsync(role: "Member");
        var body = new DeleteBlobsRequest(["photos/abc/test.jpg"]);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/blobs")
        {
            Content = JsonContent.Create(body)
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_AsMember_Returns403()
    {
        var client = await CreateAuthenticatedClientAsync(role: "Member");

        var response = await client.GetAsync("/api/blobs/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string role = "Member")
    {
        var client = _factory.CreateClient();

        var email = $"test{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest(email, "Password123!", "Test", "User", null);
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        var token = loginResult!.Data!.Token;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static MultipartFormDataContent CreateValidUploadContent(
        string fileName,
        string contentType,
        string folder = "photos",
        Guid? contextId = null,
        bool generateThumbnail = false)
    {
        var content = new MultipartFormDataContent();
        var fileBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }; // minimal JPEG
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(folder), "folder");
        if (contextId.HasValue)
            content.Add(new StringContent(contextId.Value.ToString()), "contextId");
        content.Add(new StringContent(generateThumbnail.ToString().ToLower()), "generateThumbnail");
        return content;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abuvi.Tests.Integration.Features.MediaItems;

public class MediaItemsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MediaItemsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostMediaItem_Unauthenticated_Returns401()
    {
        // Arrange
        var request = new CreateMediaItemRequest(
            "https://example.com/photo.jpg", "https://example.com/thumb.webp",
            MediaItemType.Photo, "Title", null, null, null, null, null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/media-items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostMediaItem_ValidRequest_Returns201()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var request = new CreateMediaItemRequest(
            "https://example.com/photo.jpg", "https://example.com/thumb.webp",
            MediaItemType.Photo, "Beach Day", "A sunny day", 1990, null, null, "camp");

        // Act
        var response = await client.PostAsJsonAsync("/api/media-items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MediaItemResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Title.Should().Be("Beach Day");
        result.Data.IsApproved.Should().BeFalse();
        result.Data.IsPublished.Should().BeFalse();
        result.Data.Decade.Should().Be("90s");
    }

    [Fact]
    public async Task PostMediaItem_PhotoWithoutThumbnail_Returns400()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var request = new CreateMediaItemRequest(
            "https://example.com/photo.jpg", null,
            MediaItemType.Photo, "Title", null, null, null, null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/media-items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMediaItems_WithFilters_Returns200()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/media-items?year=1990&context=camp");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMediaItemById_NonExistent_Returns404()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"/api/media-items/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchApprove_AsMember_Returns403()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var itemId = await SeedMediaItemAsync();

        // Act
        var response = await client.PatchAsync($"/api/media-items/{itemId}/approve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchApprove_AsAdmin_Returns200()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var itemId = await SeedMediaItemAsync();

        // Act
        var response = await client.PatchAsync($"/api/media-items/{itemId}/approve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MediaItemResponse>>(JsonOptions);
        result!.Data!.IsApproved.Should().BeTrue();
        result.Data.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task PatchReject_AsBoard_Returns200()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync(UserRole.Board);
        var itemId = await SeedMediaItemAsync();

        // Act
        var response = await client.PatchAsync($"/api/media-items/{itemId}/reject", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteMediaItem_AsMember_Returns403()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var itemId = await SeedMediaItemAsync();

        // Act
        var response = await client.DeleteAsync($"/api/media-items/{itemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Note: DeleteMediaItem_AsAdmin_Returns204 is tested in unit tests (MediaItemsServiceTests)
    // because it requires mocking IBlobStorageService to avoid real S3 calls.

    // Helper methods
    private async Task<HttpClient> CreateAuthenticatedClientAsync(UserRole role = UserRole.Member)
    {
        var email = $"test-{Guid.NewGuid()}@example.com";
        var password = "Password123!";

        var registerRequest = new RegisterRequest(email, password, "Test", "User", null);
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        if (role != UserRole.Member)
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                user.Role = role;
                await dbContext.SaveChangesAsync();
            }
        }

        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Data!.Token);

        return authenticatedClient;
    }

    private async Task<Guid> SeedMediaItemAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"uploader-{Guid.NewGuid()}@example.com",
            FirstName = "Uploader",
            LastName = "User",
            PasswordHash = "hash",
            Role = UserRole.Member,
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var item = new MediaItem
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = user.Id,
            FileUrl = "https://example.com/media-items/test.jpg",
            ThumbnailUrl = "https://example.com/media-items/test_thumb.webp",
            Type = MediaItemType.Photo,
            Title = "Test Photo",
            Year = 1990,
            Decade = "90s",
            IsApproved = false,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.MediaItems.Add(item);
        await dbContext.SaveChangesAsync();

        return item.Id;
    }
}

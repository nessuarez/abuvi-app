using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Memories;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Memories;

public class MemoriesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MemoriesEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostMemory_Unauthenticated_Returns401()
    {
        // Arrange
        var request = new CreateMemoryRequest("Title", "Content", null, null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/memories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostMemory_ValidRequest_Returns201()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var request = new CreateMemoryRequest("My Camp Memory", "A wonderful time at camp", 1990, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/memories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MemoryResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Title.Should().Be("My Camp Memory");
        result.Data.IsApproved.Should().BeFalse();
        result.Data.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task PostMemory_EmptyTitle_Returns400()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var request = new CreateMemoryRequest("", "Content", null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/memories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMemories_Returns200WithList()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/memories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMemoryById_NonExistent_Returns404()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"/api/memories/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchApprove_AsMember_Returns403()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync();
        var memoryId = await SeedMemoryAsync();

        // Act
        var response = await client.PatchAsync($"/api/memories/{memoryId}/approve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchApprove_AsAdmin_Returns200()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var memoryId = await SeedMemoryAsync();

        // Act
        var response = await client.PatchAsync($"/api/memories/{memoryId}/approve", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MemoryResponse>>(JsonOptions);
        result!.Data!.IsApproved.Should().BeTrue();
        result.Data.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task PatchReject_AsBoard_Returns200()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync(UserRole.Board);
        var memoryId = await SeedMemoryAsync();

        // Act
        var response = await client.PatchAsync($"/api/memories/{memoryId}/reject", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MemoryResponse>>(JsonOptions);
        result!.Data!.IsApproved.Should().BeFalse();
        result.Data.IsPublished.Should().BeFalse();
    }

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

    private async Task<Guid> SeedMemoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"author-{Guid.NewGuid()}@example.com",
            FirstName = "Author",
            LastName = "User",
            PasswordHash = "hash",
            Role = UserRole.Member,
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var memory = new Memory
        {
            Id = Guid.NewGuid(),
            AuthorUserId = user.Id,
            Title = "Test Memory",
            Content = "Test content",
            Year = 1990,
            IsApproved = false,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.Memories.Add(memory);
        await dbContext.SaveChangesAsync();

        return memory.Id;
    }
}

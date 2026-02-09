using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;

namespace Abuvi.Tests.Integration.Features;

/// <summary>
/// Integration tests for protected endpoints with JWT authorization
/// Following TDD: Tests written FIRST before implementation (Ticket 8)
/// </summary>
public class ProtectedEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProtectedEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helper Methods

    /// <summary>
    /// Creates an admin user in the database and returns a JWT token
    /// </summary>
    private async Task<string> GetAdminTokenAsync()
    {
        // First register a user
        var email = $"admin-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "AdminPass123!",
            FirstName: "Admin",
            LastName: "User",
            Phone: null
        );

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Update user role to Admin using database context
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            user.Role = UserRole.Admin;
            await dbContext.SaveChangesAsync();
        }

        // Login to get token
        var loginRequest = new LoginRequest(email, "AdminPass123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);

        return loginResult!.Data!.Token;
    }

    /// <summary>
    /// Registers a new user (Member role by default) and returns a JWT token
    /// </summary>
    private async Task<string> GetMemberTokenAsync()
    {
        // Register new user
        var email = $"member-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "MemberPass123!",
            FirstName: "Member",
            LastName: "User",
            Phone: null
        );

        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Login to get token
        var loginRequest = new LoginRequest(email, "MemberPass123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);

        return loginResult!.Data!.Token;
    }

    /// <summary>
    /// Creates a test user and returns their ID
    /// </summary>
    private async Task<Guid> CreateTestUserAsync(string token)
    {
        var request = new CreateUserRequest(
            Email: $"testuser-{Guid.NewGuid()}@example.com",
            Password: "TestPass123!",
            FirstName: "Test",
            LastName: "User",
            Phone: null,
            Role: UserRole.Member
        );

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/users", request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);

        return result!.Data!.Id;
    }

    #endregion

    #region GET /api/users (List All Users)

    [Fact]
    public async Task GetAllUsers_WithoutToken_Returns401()
    {
        // Arrange - No token provided
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllUsers_WithMemberToken_Returns403()
    {
        // Arrange
        var memberToken = await GetMemberTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllUsers_WithAdminToken_Returns200()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region GET /api/users/{id} (Get User By ID)

    [Fact]
    public async Task GetUserById_WithoutToken_Returns401()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithValidToken_Returns200()
    {
        // Arrange
        var memberToken = await GetMemberTokenAsync();
        var adminToken = await GetAdminTokenAsync();
        var userId = await CreateTestUserAsync(adminToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await _client.GetAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(userId);
    }

    #endregion

    #region POST /api/users (Create User)

    [Fact]
    public async Task CreateUser_WithoutToken_Returns401()
    {
        // Arrange
        var request = new CreateUserRequest(
            Email: $"newuser-{Guid.NewGuid()}@example.com",
            Password: "NewPass123!",
            FirstName: "New",
            LastName: "User",
            Phone: null,
            Role: UserRole.Member
        );
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUser_WithMemberToken_Returns403()
    {
        // Arrange
        var memberToken = await GetMemberTokenAsync();
        var request = new CreateUserRequest(
            Email: $"newuser-{Guid.NewGuid()}@example.com",
            Password: "NewPass123!",
            FirstName: "New",
            LastName: "User",
            Phone: null,
            Role: UserRole.Member
        );
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUser_WithAdminToken_Returns201()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var request = new CreateUserRequest(
            Email: $"newuser-{Guid.NewGuid()}@example.com",
            Password: "NewPass123!",
            FirstName: "New",
            LastName: "User",
            Phone: null,
            Role: UserRole.Member
        );
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Email.Should().Be(request.Email);
    }

    #endregion

    #region PUT /api/users/{id} (Update User)

    [Fact]
    public async Task UpdateUser_WithoutToken_Returns401()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateUserRequest(
            FirstName: "Updated",
            LastName: "Name",
            Phone: null,
            IsActive: true
        );
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{userId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_WithValidToken_Returns200()
    {
        // Arrange
        var memberToken = await GetMemberTokenAsync();
        var adminToken = await GetAdminTokenAsync();
        var userId = await CreateTestUserAsync(adminToken);

        var request = new UpdateUserRequest(
            FirstName: "Updated",
            LastName: "Name",
            Phone: "+1234567890",
            IsActive: true
        );
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{userId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.FirstName.Should().Be("Updated");
    }

    #endregion

    #region DELETE /api/users/{id} (Delete User)

    [Fact]
    public async Task DeleteUser_WithoutToken_Returns401()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.DeleteAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteUser_WithMemberToken_Returns403()
    {
        // Arrange
        var memberToken = await GetMemberTokenAsync();
        var adminToken = await GetAdminTokenAsync();
        var userId = await CreateTestUserAsync(adminToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await _client.DeleteAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteUser_WithAdminToken_Returns204()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var userId = await CreateTestUserAsync(adminToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.DeleteAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    #endregion

    #region Public Endpoints (Should NOT require auth)

    [Fact]
    public async Task Register_WithoutToken_Returns200()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: $"public-{Guid.NewGuid()}@example.com",
            Password: "PublicPass123!",
            FirstName: "Public",
            LastName: "User",
            Phone: null
        );
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithoutToken_Returns200()
    {
        // Arrange - Create user first
        var email = $"login-{Guid.NewGuid()}@example.com";
        var password = "LoginPass123!";
        var registerRequest = new RegisterRequest(email, password, "Login", "User", null);
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest(email, password);
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}

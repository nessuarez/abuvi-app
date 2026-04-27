using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abuvi.Tests.Integration.Features.Users;

public class UsersEndpointsUpdateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public UsersEndpointsUpdateTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helpers

    private async Task<(string token, Guid userId)> RegisterMemberAsync()
    {
        var email = $"member-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "MemberPass123!", "Member", "User", null));

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "MemberPass123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        return (loginResult!.Data!.Token, loginResult.Data.User.Id);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var email = $"admin-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "AdminPass123!", "Admin", "User", null));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null) { user.Role = UserRole.Admin; await db.SaveChangesAsync(); }

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "AdminPass123!"));
        var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        return result!.Data!.Token;
    }

    private async Task<string> GetBoardTokenAsync()
    {
        var email = $"board-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "BoardPass123!", "Board", "User", null));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null) { user.Role = UserRole.Board; await db.SaveChangesAsync(); }

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "BoardPass123!"));
        var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        return result!.Data!.Token;
    }

    private async Task<Guid> CreateUserAsync(string adminToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsJsonAsync("/api/users",
            new CreateUserRequest($"created-{Guid.NewGuid()}@example.com", "Pass123!", "Test", "User", null, UserRole.Member));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        return result!.Data!.Id;
    }

    private static UpdateUserRequest DefaultUpdateRequest(bool isActive = true) =>
        new("Updated", "Name", "+34612345678", isActive);

    #endregion

    #region Authorization

    [Fact]
    public async Task UpdateUser_Admin_CanUpdateAnyUserProfile()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var targetUserId = await CreateUserAsync(adminToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{targetUserId}", DefaultUpdateRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        result!.Data!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateUser_Board_CanUpdateAnyUserProfile()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var boardToken = await GetBoardTokenAsync();
        var targetUserId = await CreateUserAsync(adminToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", boardToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{targetUserId}", DefaultUpdateRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        result!.Data!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateUser_Member_CanUpdateOwnProfile()
    {
        // Arrange
        var (memberToken, memberId) = await RegisterMemberAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{memberId}", DefaultUpdateRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        result!.Data!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateUser_Member_CannotUpdateOtherUserProfile()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var (memberToken, _) = await RegisterMemberAsync();
        var otherUserId = await CreateUserAsync(adminToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{otherUserId}", DefaultUpdateRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateUser_Unauthenticated_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", DefaultUpdateRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region IsActive protection

    [Fact]
    public async Task UpdateUser_MemberCaller_IsActiveNotChanged()
    {
        // Arrange — member registers with IsActive=false (default), admin activates, then member tries to deactivate via update
        var adminToken = await GetAdminTokenAsync();
        var (memberToken, memberId) = await RegisterMemberAsync();

        // Admin activates the member
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var user = await db.Users.FindAsync(memberId);
        if (user != null) { user.IsActive = true; await db.SaveChangesAsync(); }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act — member sends IsActive=false but should be ignored
        var response = await _client.PutAsJsonAsync($"/api/users/{memberId}",
            new UpdateUserRequest("Updated", "Name", null, IsActive: false));

        // Assert — IsActive stays true
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        result!.Data!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUser_AdminCaller_IsActiveChanged()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var targetUserId = await CreateUserAsync(adminToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act — admin sets IsActive=false
        var response = await _client.PutAsJsonAsync($"/api/users/{targetUserId}",
            new UpdateUserRequest("Updated", "Name", null, IsActive: false));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>(JsonOptions);
        result!.Data!.IsActive.Should().BeFalse();
    }

    #endregion
}

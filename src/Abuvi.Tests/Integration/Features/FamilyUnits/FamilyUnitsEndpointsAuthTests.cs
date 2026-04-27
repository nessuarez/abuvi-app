using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abuvi.Tests.Integration.Features.FamilyUnits;

public class FamilyUnitsEndpointsAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FamilyUnitsEndpointsAuthTests(WebApplicationFactory<Program> factory)
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
        var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        return (result!.Data!.Token, result.Data.User.Id);
    }

    private async Task<string> GetElevatedTokenAsync(UserRole role)
    {
        var email = $"{role.ToString().ToLower()}-{Guid.NewGuid()}@example.com";
        var password = "ElevatedPass123!";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, password, role.ToString(), "User", null));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null) { user.Role = role; await db.SaveChangesAsync(); }

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password));
        var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        return result!.Data!.Token;
    }

    private async Task<(Guid familyUnitId, Guid memberId)> CreateFamilyUnitWithMemberAsync(string representativeToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", representativeToken);

        var unitResponse = await client.PostAsJsonAsync("/api/family-units", new CreateFamilyUnitRequest("Test Family"));
        var unit = await unitResponse.Content.ReadFromJsonAsync<ApiResponse<FamilyUnitResponse>>(JsonOptions);
        var familyUnitId = unit!.Data!.Id;

        var memberResponse = await client.PostAsJsonAsync($"/api/family-units/{familyUnitId}/members",
            new CreateFamilyMemberRequest("Child", "User", new DateOnly(2015, 6, 1), FamilyRelationship.Child));
        var member = await memberResponse.Content.ReadFromJsonAsync<ApiResponse<FamilyMemberResponse>>(JsonOptions);
        var memberId = member!.Data!.Id;

        return (familyUnitId, memberId);
    }

    #endregion

    #region UpdateFamilyUnit authorization

    [Fact]
    public async Task UpdateFamilyUnit_Admin_CanUpdate_WhenNotRepresentative()
    {
        // Arrange — representative creates a family unit; admin (different user) tries to update it
        var (repToken, _) = await RegisterMemberAsync();
        var adminToken = await GetElevatedTokenAsync(UserRole.Admin);
        var (familyUnitId, _) = await CreateFamilyUnitWithMemberAsync(repToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/family-units/{familyUnitId}",
            new UpdateFamilyUnitRequest("Updated Family Name"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateFamilyUnit_Board_CanUpdate_WhenNotRepresentative()
    {
        // Arrange
        var (repToken, _) = await RegisterMemberAsync();
        var boardToken = await GetElevatedTokenAsync(UserRole.Board);
        var (familyUnitId, _) = await CreateFamilyUnitWithMemberAsync(repToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", boardToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/family-units/{familyUnitId}",
            new UpdateFamilyUnitRequest("Updated Family Name"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateFamilyUnit_Member_CannotUpdate_WhenNotRepresentative()
    {
        // Arrange — representative creates a family unit; a different member tries to update it
        var (repToken, _) = await RegisterMemberAsync();
        var (otherMemberToken, _) = await RegisterMemberAsync();
        var (familyUnitId, _) = await CreateFamilyUnitWithMemberAsync(repToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherMemberToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/family-units/{familyUnitId}",
            new UpdateFamilyUnitRequest("Unauthorized Update"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region UpdateFamilyMember authorization

    [Fact]
    public async Task UpdateFamilyMember_Admin_CanUpdate_WhenNotRepresentative()
    {
        // Arrange
        var (repToken, _) = await RegisterMemberAsync();
        var adminToken = await GetElevatedTokenAsync(UserRole.Admin);
        var (familyUnitId, memberId) = await CreateFamilyUnitWithMemberAsync(repToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/family-units/{familyUnitId}/members/{memberId}",
            new UpdateFamilyMemberRequest("UpdatedChild", "User", new DateOnly(2015, 6, 1), FamilyRelationship.Child));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateFamilyMember_Board_CanUpdate_WhenNotRepresentative()
    {
        // Arrange
        var (repToken, _) = await RegisterMemberAsync();
        var boardToken = await GetElevatedTokenAsync(UserRole.Board);
        var (familyUnitId, memberId) = await CreateFamilyUnitWithMemberAsync(repToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", boardToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/family-units/{familyUnitId}/members/{memberId}",
            new UpdateFamilyMemberRequest("UpdatedChild", "User", new DateOnly(2015, 6, 1), FamilyRelationship.Child));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateFamilyMember_Member_CannotUpdate_WhenNotRepresentative()
    {
        // Arrange
        var (repToken, _) = await RegisterMemberAsync();
        var (otherMemberToken, _) = await RegisterMemberAsync();
        var (familyUnitId, memberId) = await CreateFamilyUnitWithMemberAsync(repToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherMemberToken);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/family-units/{familyUnitId}/members/{memberId}",
            new UpdateFamilyMemberRequest("Unauthorized", "Update", new DateOnly(2015, 6, 1), FamilyRelationship.Child));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion
}

using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.FamilyUnits;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Abuvi.Tests.Integration.Features.FamilyUnits;

public class FamilyUnitsUserLinkingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FamilyUnitsUserLinkingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    private async Task<(HttpClient client, string email, string token)> CreateAuthenticatedUserAsync()
    {
        var email = $"test{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest(email, "Password123!", "Test", "User", null);
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest(email, "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await DeserializeAsync<ApiResponse<LoginResponse>>(loginResponse);
        var token = loginResult!.Data!.Token;

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return (authenticatedClient, email, token);
    }

    [Fact]
    public async Task CreateFamilyUnit_RepresentativeMemberShouldHaveEmail()
    {
        // Arrange
        var (authClient, email, _) = await CreateAuthenticatedUserAsync();
        var createRequest = new CreateFamilyUnitRequest("Test Family");

        // Act
        var response = await authClient.PostAsJsonAsync("/api/family-units", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var familyUnitResult = await DeserializeAsync<ApiResponse<FamilyUnitResponse>>(response);
        var familyUnitId = familyUnitResult!.Data!.Id;

        // Get members list and verify representative has email set
        var membersResponse = await authClient.GetAsync($"/api/family-units/{familyUnitId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersResult = await DeserializeAsync<ApiResponse<IReadOnlyList<FamilyMemberResponse>>>(membersResponse);
        membersResult!.Data.Should().NotBeNull();
        membersResult.Data.Should().HaveCount(1);

        var representative = membersResult.Data!.First();
        representative.Email.Should().Be(email);
    }

    [Fact]
    public async Task CreateFamilyMember_WithExistingUserEmail_ShouldAutoLinkUserId()
    {
        // Arrange
        var (user1Client, _, _) = await CreateAuthenticatedUserAsync();
        var (_, user2Email, _) = await CreateAuthenticatedUserAsync();

        // User1 creates a family unit
        var createFamilyUnitRequest = new CreateFamilyUnitRequest("Test Family");
        var familyUnitResponse = await user1Client.PostAsJsonAsync("/api/family-units", createFamilyUnitRequest);
        var familyUnitResult = await DeserializeAsync<ApiResponse<FamilyUnitResponse>>(familyUnitResponse);
        var familyUnitId = familyUnitResult!.Data!.Id;

        // Act - User1 creates a family member using User2's email
        var createMemberRequest = new CreateFamilyMemberRequest(
            "User2",
            "Person",
            new DateOnly(1990, 5, 10),
            FamilyRelationship.Spouse,
            Email: user2Email
        );

        var memberResponse = await user1Client.PostAsJsonAsync(
            $"/api/family-units/{familyUnitId}/members",
            createMemberRequest);

        // Assert
        memberResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var memberResult = await DeserializeAsync<ApiResponse<FamilyMemberResponse>>(memberResponse);
        memberResult!.Data.Should().NotBeNull();
        memberResult.Data!.Email.Should().Be(user2Email);
        memberResult.Data.UserId.Should().NotBeNull("because User2 is a registered user with that email");
    }

    [Fact]
    public async Task UpdateFamilyMember_ChangingEmailToExistingUser_ShouldLinkThenUnlink()
    {
        // Arrange
        var (user1Client, _, _) = await CreateAuthenticatedUserAsync();
        var (_, user2Email, _) = await CreateAuthenticatedUserAsync();

        // User1 creates a family unit
        var createFamilyUnitRequest = new CreateFamilyUnitRequest("Test Family");
        var familyUnitResponse = await user1Client.PostAsJsonAsync("/api/family-units", createFamilyUnitRequest);
        var familyUnitResult = await DeserializeAsync<ApiResponse<FamilyUnitResponse>>(familyUnitResponse);
        var familyUnitId = familyUnitResult!.Data!.Id;

        // Create a family member without email
        var createMemberRequest = new CreateFamilyMemberRequest(
            "NoEmail",
            "Person",
            new DateOnly(1985, 3, 15),
            FamilyRelationship.Sibling
        );

        var createMemberResponse = await user1Client.PostAsJsonAsync(
            $"/api/family-units/{familyUnitId}/members",
            createMemberRequest);
        var createMemberResult = await DeserializeAsync<ApiResponse<FamilyMemberResponse>>(createMemberResponse);
        var memberId = createMemberResult!.Data!.Id;
        createMemberResult.Data.UserId.Should().BeNull("member created without email has no link");

        // Act 1 - Update with User2's email to link
        var updateLinkRequest = new UpdateFamilyMemberRequest(
            "NoEmail",
            "Person",
            new DateOnly(1985, 3, 15),
            FamilyRelationship.Sibling,
            Email: user2Email
        );

        var linkResponse = await user1Client.PutAsJsonAsync(
            $"/api/family-units/{familyUnitId}/members/{memberId}",
            updateLinkRequest);

        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var linkResult = await DeserializeAsync<ApiResponse<FamilyMemberResponse>>(linkResponse);
        linkResult!.Data!.UserId.Should().NotBeNull("email matches an existing user so it should be linked");

        // Act 2 - Update with an email that has no matching user to unlink
        var updateUnlinkRequest = new UpdateFamilyMemberRequest(
            "NoEmail",
            "Person",
            new DateOnly(1985, 3, 15),
            FamilyRelationship.Sibling,
            Email: "notregistered@example.com"
        );

        var unlinkResponse = await user1Client.PutAsJsonAsync(
            $"/api/family-units/{familyUnitId}/members/{memberId}",
            updateUnlinkRequest);

        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var unlinkResult = await DeserializeAsync<ApiResponse<FamilyMemberResponse>>(unlinkResponse);
        unlinkResult!.Data!.UserId.Should().BeNull("email no longer matches any user so it should be unlinked");
    }
}

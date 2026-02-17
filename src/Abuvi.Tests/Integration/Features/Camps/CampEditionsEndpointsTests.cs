using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Camps;

/// <summary>
/// Integration tests for Camp Editions lifecycle management endpoints (Phase 4).
/// Tests cover: PATCH status, GET active, GET by ID, PUT update, GET all.
/// </summary>
public class CampEditionsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CampEditionsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helpers

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
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        return loginResult!.Data!.Token;
    }

    private async Task<string> GetMemberTokenAsync()
    {
        var email = $"member-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "MemberPass123!", "Member", "User", null));

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "MemberPass123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        return loginResult!.Data!.Token;
    }

    /// <summary>Creates a camp and returns its ID</summary>
    private async Task<Guid> CreateCampAsync(string token)
    {
        var request = new CreateCampRequest(
            Name: $"Test Camp {Guid.NewGuid()}",
            Description: null,
            Location: "Test Location",
            Latitude: null,
            Longitude: null,
            GooglePlaceId: null,
            PricePerAdult: 180m,
            PricePerChild: 120m,
            PricePerBaby: 60m
        );

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/camps", request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampDetailResponse>>(JsonOptions);
        return result!.Data!.Id;
    }

    /// <summary>Proposes a camp edition and returns its ID</summary>
    private async Task<Guid> ProposeEditionAsync(string token, Guid campId, int year = 2030)
    {
        var request = new ProposeCampEditionRequest(
            CampId: campId,
            Year: year,
            StartDate: new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(year, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: null,
            PricePerChild: null,
            PricePerBaby: null,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: 100,
            Notes: "Integration test edition"
        );

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/camps/editions/propose", request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionResponse>>(JsonOptions);
        return result!.Data!.Id;
    }

    #endregion

    #region GET /api/camps/editions — List All Editions

    [Fact]
    public async Task GetAllEditions_WithAdminToken_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/camps/editions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CampEditionResponse>>>(JsonOptions);
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllEditions_WithMemberToken_Returns403()
    {
        // Arrange
        var token = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/camps/editions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllEditions_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/camps/editions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GET /api/camps/editions/{id} — Get Edition By ID

    [Fact]
    public async Task GetEditionById_WithExistingEdition_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync($"/api/camps/editions/{editionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionResponse>>(JsonOptions);
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(editionId);
        result.Data.Status.Should().Be(CampEditionStatus.Proposed);
    }

    [Fact]
    public async Task GetEditionById_WithNonExistentId_Returns404()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync($"/api/camps/editions/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEditionById_WithMemberToken_Returns200()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await client.GetAsync($"/api/camps/editions/{editionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GET /api/camps/editions/active — Get Active Edition

    [Fact]
    public async Task GetActiveEdition_WithNoOpenEdition_Returns200WithNullData()
    {
        // Arrange
        var token = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act — Use a far-future year unlikely to have any edition
        var response = await client.GetAsync("/api/camps/editions/active?year=2099");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ActiveCampEditionResponse>>(JsonOptions);
        result!.Success.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveEdition_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/camps/editions/active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PUT /api/camps/editions/{id} — Update Edition

    [Fact]
    public async Task UpdateEdition_WithDraftEdition_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2031);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var updateRequest = new UpdateCampEditionRequest(
            StartDate: new DateTime(2031, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2031, 8, 12, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: 200m,
            PricePerChild: 140m,
            PricePerBaby: 70m,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: 150,
            Notes: "Updated via integration test"
        );

        // Act
        var response = await client.PutAsJsonAsync($"/api/camps/editions/{editionId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionResponse>>(JsonOptions);
        result!.Success.Should().BeTrue();
        result.Data!.PricePerAdult.Should().Be(200m);
        result.Data.Notes.Should().Be("Updated via integration test");
        result.Data.MaxCapacity.Should().Be(150);
    }

    [Fact]
    public async Task UpdateEdition_WithInvalidData_Returns400()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2032);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // EndDate before StartDate — fails validation
        var updateRequest = new UpdateCampEditionRequest(
            StartDate: new DateTime(2032, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2032, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: 180m,
            PricePerChild: 120m,
            PricePerBaby: 60m,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null
        );

        // Act
        var response = await client.PutAsJsonAsync($"/api/camps/editions/{editionId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateEdition_WithMemberToken_Returns403()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2033);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        var updateRequest = new UpdateCampEditionRequest(
            StartDate: new DateTime(2033, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2033, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult: 180m,
            PricePerChild: 120m,
            PricePerBaby: 60m,
            UseCustomAgeRanges: false,
            CustomBabyMaxAge: null,
            CustomChildMinAge: null,
            CustomChildMaxAge: null,
            CustomAdultMinAge: null,
            MaxCapacity: null,
            Notes: null
        );

        // Act
        var response = await client.PutAsJsonAsync($"/api/camps/editions/{editionId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region PATCH /api/camps/editions/{id}/status — Change Edition Status

    [Fact]
    public async Task ChangeEditionStatus_WithInvalidTransition_Returns400()
    {
        // Arrange — Proposed edition cannot be transitioned to Closed
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2034);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var statusRequest = new ChangeEditionStatusRequest(CampEditionStatus.Closed);

        // Act
        var response = await client.PatchAsJsonAsync($"/api/camps/editions/{editionId}/status", statusRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeEditionStatus_WithInvalidStatusValue_Returns400()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2035);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Serialize an invalid status value
        var invalidBody = JsonSerializer.Serialize(new { Status = 999 });

        // Act
        var response = await client.PatchAsync(
            $"/api/camps/editions/{editionId}/status",
            new StringContent(invalidBody, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeEditionStatus_WithMemberToken_Returns403()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2036);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        var statusRequest = new ChangeEditionStatusRequest(CampEditionStatus.Draft);

        // Act
        var response = await client.PatchAsJsonAsync($"/api/camps/editions/{editionId}/status", statusRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeEditionStatus_WithNonExistentEdition_Returns404()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var statusRequest = new ChangeEditionStatusRequest(CampEditionStatus.Draft);

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/camps/editions/{Guid.NewGuid()}/status", statusRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

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
/// Integration tests for Camp Edition Extras endpoints.
/// Tests cover: POST create, GET list, GET by ID, PUT update, DELETE, PATCH activate/deactivate.
/// </summary>
public class CampEditionExtrasEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CampEditionExtrasEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helpers

    private async Task<string> GetAdminTokenAsync()
    {
        var email = $"admin-extras-{Guid.NewGuid()}@example.com";
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
        var email = $"member-extras-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "MemberPass123!", "Member", "User", null));

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "MemberPass123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        return loginResult!.Data!.Token;
    }

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

    private async Task<Guid> ProposeEditionAsync(string token, Guid campId, int year = 2040)
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

    private async Task<Guid> CreateExtraAsync(string token, Guid editionId)
    {
        var request = new CreateCampEditionExtraRequest(
            Name: $"Test Extra {Guid.NewGuid()}",
            Description: "Integration test extra",
            Price: 15m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: 100
        );

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync(
            $"/api/camps/editions/{editionId}/extras", request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionExtraResponse>>(JsonOptions);
        return result!.Data!.Id;
    }

    #endregion

    #region POST /api/camps/editions/{editionId}/extras

    [Fact]
    public async Task CreateExtra_WithAdminToken_Returns201Created()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2041);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateCampEditionExtraRequest(
            Name: "Camp T-Shirt",
            Description: "Official camp t-shirt",
            Price: 15m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: 100
        );

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/camps/editions/{editionId}/extras", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionExtraResponse>>(JsonOptions);
        result!.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Camp T-Shirt");
        result.Data.Price.Should().Be(15m);
        result.Data.IsActive.Should().BeTrue();
        result.Data.CurrentQuantitySold.Should().Be(0);
    }

    [Fact]
    public async Task CreateExtra_WithMemberToken_Returns403Forbidden()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2042);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        var request = new CreateCampEditionExtraRequest(
            Name: "Extra", Description: null, Price: 10m,
            PricingType: PricingType.PerPerson, PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false, MaxQuantity: null
        );

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/camps/editions/{editionId}/extras", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateExtra_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/camps/editions/{Guid.NewGuid()}/extras",
            new CreateCampEditionExtraRequest("Name", null, 10m,
                PricingType.PerPerson, PricingPeriod.OneTime, false, null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateExtra_WithInvalidData_Returns400BadRequest()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2043);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Name is empty — fails validation
        var request = new CreateCampEditionExtraRequest(
            Name: "",
            Description: null,
            Price: 10m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: null
        );

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/camps/editions/{editionId}/extras", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateExtra_WithNonExistentEdition_Returns400BadRequest()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateCampEditionExtraRequest(
            Name: "Extra", Description: null, Price: 10m,
            PricingType: PricingType.PerPerson, PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false, MaxQuantity: null
        );

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/camps/editions/{Guid.NewGuid()}/extras", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region GET /api/camps/editions/{editionId}/extras

    [Fact]
    public async Task GetExtrasByEdition_WithMemberToken_Returns200WithList()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2044);
        await CreateExtraAsync(adminToken, editionId);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await client.GetAsync($"/api/camps/editions/{editionId}/extras");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CampEditionExtraResponse>>>(JsonOptions);
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetExtrasByEdition_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync($"/api/camps/editions/{Guid.NewGuid()}/extras");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExtrasByEdition_WithActiveOnlyFilter_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2045);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync($"/api/camps/editions/{editionId}/extras?activeOnly=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CampEditionExtraResponse>>>(JsonOptions);
        result!.Success.Should().BeTrue();
    }

    #endregion

    #region GET /api/camps/editions/extras/{id}

    [Fact]
    public async Task GetExtraById_WithExistingId_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2046);
        var extraId = await CreateExtraAsync(token, editionId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync($"/api/camps/editions/extras/{extraId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionExtraResponse>>(JsonOptions);
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(extraId);
    }

    [Fact]
    public async Task GetExtraById_WithNonExistentId_Returns404()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync($"/api/camps/editions/extras/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExtraById_WithMemberToken_Returns200()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2047);
        var extraId = await CreateExtraAsync(adminToken, editionId);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await client.GetAsync($"/api/camps/editions/extras/{extraId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetExtraById_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync($"/api/camps/editions/extras/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PUT /api/camps/editions/extras/{id}

    [Fact]
    public async Task UpdateExtra_WithAdminToken_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2048);
        var extraId = await CreateExtraAsync(token, editionId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var updateRequest = new UpdateCampEditionExtraRequest(
            Name: "Updated Extra Name",
            Description: "Updated description",
            Price: 15m, // same price (0 sold, so could change, but keeping same to be safe)
            IsRequired: true,
            IsActive: true,
            MaxQuantity: 50
        );

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/camps/editions/extras/{extraId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionExtraResponse>>(JsonOptions);
        result!.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Updated Extra Name");
        result.Data.MaxQuantity.Should().Be(50);
        result.Data.IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateExtra_WithMemberToken_Returns403Forbidden()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2049);
        var extraId = await CreateExtraAsync(adminToken, editionId);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        var updateRequest = new UpdateCampEditionExtraRequest(
            Name: "Hacked", Description: null, Price: 1m,
            IsRequired: false, IsActive: true, MaxQuantity: null
        );

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/camps/editions/extras/{extraId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateExtra_WithInvalidData_Returns400BadRequest()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2050);
        var extraId = await CreateExtraAsync(token, editionId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Empty name — fails validation
        var updateRequest = new UpdateCampEditionExtraRequest(
            Name: "", Description: null, Price: 10m,
            IsRequired: false, IsActive: true, MaxQuantity: null
        );

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/camps/editions/extras/{extraId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateExtra_WithNonExistentId_Returns400BadRequest()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var updateRequest = new UpdateCampEditionExtraRequest(
            Name: "Extra", Description: null, Price: 10m,
            IsRequired: false, IsActive: true, MaxQuantity: null
        );

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/camps/editions/extras/{Guid.NewGuid()}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region DELETE /api/camps/editions/extras/{id}

    [Fact]
    public async Task DeleteExtra_WithAdminToken_Returns204NoContent()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2051);
        var extraId = await CreateExtraAsync(token, editionId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.DeleteAsync($"/api/camps/editions/extras/{extraId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteExtra_WithMemberToken_Returns403Forbidden()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2052);
        var extraId = await CreateExtraAsync(adminToken, editionId);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await client.DeleteAsync($"/api/camps/editions/extras/{extraId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteExtra_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.DeleteAsync($"/api/camps/editions/extras/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region PATCH /api/camps/editions/extras/{id}/activate

    [Fact]
    public async Task ActivateExtra_WithAdminToken_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2053);
        var extraId = await CreateExtraAsync(token, editionId);

        // First deactivate it
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.PatchAsync($"/api/camps/editions/extras/{extraId}/deactivate", null);

        // Act — activate
        var response = await client.PatchAsync($"/api/camps/editions/extras/{extraId}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionExtraResponse>>(JsonOptions);
        result!.Data!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateExtra_WithMemberToken_Returns403Forbidden()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2054);
        var extraId = await CreateExtraAsync(adminToken, editionId);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await client.PatchAsync($"/api/camps/editions/extras/{extraId}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region PATCH /api/camps/editions/extras/{id}/deactivate

    [Fact]
    public async Task DeactivateExtra_WithAdminToken_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(token);
        var editionId = await ProposeEditionAsync(token, campId, year: 2055);
        var extraId = await CreateExtraAsync(token, editionId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PatchAsync($"/api/camps/editions/extras/{extraId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CampEditionExtraResponse>>(JsonOptions);
        result!.Data!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateExtra_WithMemberToken_Returns403Forbidden()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var campId = await CreateCampAsync(adminToken);
        var editionId = await ProposeEditionAsync(adminToken, campId, year: 2056);
        var extraId = await CreateExtraAsync(adminToken, editionId);

        var memberToken = await GetMemberTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Act
        var response = await client.PatchAsync($"/api/camps/editions/extras/{extraId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion
}

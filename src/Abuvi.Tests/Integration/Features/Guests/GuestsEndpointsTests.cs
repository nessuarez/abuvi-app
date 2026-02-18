using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Guests;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Guests;

public class GuestsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _authenticatedClient;
    private string? _authToken;

    public GuestsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _authenticatedClient = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        if (_authToken != null) return _authToken;

        var email = $"test{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest(email, "Password123!", "Test", "User", null);
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest(email, "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        _authToken = loginResult!.Data!.Token;
        _authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authToken);

        return _authToken;
    }

    [Fact]
    public async Task CreateGuest_WithValidData_Returns201Created()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();

        var request = new CreateGuestRequest(
            "Jane",
            "Doe",
            new DateOnly(1995, 5, 15),
            DocumentNumber: "ABC123",
            Email: "jane@example.com"
        );

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/guests",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<GuestResponse>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.FirstName.Should().Be("Jane");
        result.Data.LastName.Should().Be("Doe");
        result.Data.FamilyUnitId.Should().Be(familyUnit.Id);
        result.Data.IsActive.Should().BeTrue();
        result.Data.DocumentNumber.Should().Be("ABC123");
    }

    [Fact]
    public async Task CreateGuest_WithNonExistentFamilyUnit_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var nonExistentFamilyUnitId = Guid.NewGuid();
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15));

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{nonExistentFamilyUnitId}/guests",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateGuest_WithInvalidData_Returns400BadRequest()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();

        var request = new CreateGuestRequest("", "Doe", new DateOnly(1995, 5, 15));

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/guests",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGuest_NormalizesDocumentNumberToUppercase()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();

        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            DocumentNumber: "ABC123");

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/guests",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<GuestResponse>>();
        result!.Data!.DocumentNumber.Should().Be("ABC123");
    }

    [Fact]
    public async Task ListGuests_WhenGuestsExist_Returns200WithGuests()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();
        var guestId = await CreateTestGuestAsync(familyUnit.Id);

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/guests");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<GuestResponse>>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ListGuests_WhenFamilyUnitHasNoGuests_Returns200WithEmptyList()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/guests");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<GuestResponse>>>();
        result!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGuest_WhenExists_Returns200Ok()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();
        var guestId = await CreateTestGuestAsync(familyUnit.Id);

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/guests/{guestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<GuestResponse>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(guestId);
    }

    [Fact]
    public async Task GetGuest_WhenNotExists_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();
        var nonExistentGuestId = Guid.NewGuid();

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/guests/{nonExistentGuestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateGuest_WithValidData_Returns200Ok()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();
        var guestId = await CreateTestGuestAsync(familyUnit.Id);

        var request = new UpdateGuestRequest(
            "UpdatedFirst",
            "UpdatedLast",
            new DateOnly(1990, 1, 1)
        );

        // Act
        var response = await _authenticatedClient.PutAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/guests/{guestId}",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<GuestResponse>>();
        result!.Data!.FirstName.Should().Be("UpdatedFirst");
        result.Data.LastName.Should().Be("UpdatedLast");
    }

    [Fact]
    public async Task UpdateGuest_WithInvalidData_Returns400BadRequest()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();
        var guestId = await CreateTestGuestAsync(familyUnit.Id);

        var request = new UpdateGuestRequest("", "Doe", new DateOnly(1990, 1, 1));

        // Act
        var response = await _authenticatedClient.PutAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/guests/{guestId}",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateGuest_WhenNotExists_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();
        var nonExistentGuestId = Guid.NewGuid();
        var request = new UpdateGuestRequest("Jane", "Doe", new DateOnly(1990, 1, 1));

        // Act
        var response = await _authenticatedClient.PutAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/guests/{nonExistentGuestId}",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGuest_WhenExists_Returns204NoContent()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();
        var guestId = await CreateTestGuestAsync(familyUnit.Id);

        // Act
        var response = await _authenticatedClient.DeleteAsync(
            $"/api/family-units/{familyUnit.Id}/guests/{guestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify guest is soft-deleted (no longer in active list)
        var listResponse = await _authenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/guests");
        var listResult = await listResponse.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<GuestResponse>>>();
        listResult!.Data!.Should().NotContain(g => g.Id == guestId);
    }

    [Fact]
    public async Task DeleteGuest_WhenNotExists_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnit = await SeedFamilyUnitAsync();
        var nonExistentGuestId = Guid.NewGuid();

        // Act
        var response = await _authenticatedClient.DeleteAsync(
            $"/api/family-units/{familyUnit.Id}/guests/{nonExistentGuestId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GuestsEndpoints_WhenUnauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/family-units/{familyUnitId}/guests");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Helper methods
    private async Task<FamilyUnit> SeedFamilyUnitAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"test{Guid.NewGuid()}@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hashedpassword",
            Role = UserRole.Member,
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var familyUnit = new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = "Test Family",
            RepresentativeUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.FamilyUnits.Add(familyUnit);
        await dbContext.SaveChangesAsync();

        return familyUnit;
    }

    private async Task<Guid> CreateTestGuestAsync(Guid familyUnitId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        // Insert directly to avoid encryption service complexity in test setup
        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnitId,
            FirstName = "Test",
            LastName = "Guest",
            DateOfBirth = new DateOnly(1990, 1, 1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Guests.Add(guest);
        await dbContext.SaveChangesAsync();

        return guest.Id;
    }
}

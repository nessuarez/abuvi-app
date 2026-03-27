using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Users;
using Abuvi.API.Features.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Memberships;

public class MembershipsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _authenticatedClient;
    private string? _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MembershipsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _authenticatedClient = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        if (_authToken != null) return _authToken;

        // Register and login to get token
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

    /// <summary>
    /// Creates a Board-role user directly in the DB and logs in to obtain a JWT token.
    /// </summary>
    private async Task<HttpClient> GetBoardAuthClientAsync()
    {
        var boardEmail = $"board{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var boardUser = new User
            {
                Id = Guid.NewGuid(),
                Email = boardEmail,
                FirstName = "Board",
                LastName = "User",
                PasswordHash = hasher.HashPassword(password),
                Role = UserRole.Board,
                EmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(boardUser);
            await dbContext.SaveChangesAsync();
        }

        var loginRequest = new LoginRequest(boardEmail, password);
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        var boardClient = _factory.CreateClient();
        boardClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Data!.Token);
        return boardClient;
    }

    [Fact]
    public async Task CreateMembership_WithValidYear_Returns201AndStartDateIsJanFirst()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipResponse>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.FamilyMemberId.Should().Be(familyMember.Id);
        result.Data.IsActive.Should().BeTrue();
        result.Data.StartDate.Should().Be(new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CreateMembership_WithNonExistentFamilyMember_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var familyUnitId = Guid.NewGuid();
        var nonExistentMemberId = Guid.NewGuid();
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnitId}/members/{nonExistentMemberId}/membership",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateMembership_WhenActiveMembershipExists_Returns409Conflict()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateMembership_WithFutureYear_Returns400BadRequest()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var request = new CreateMembershipRequest(DateTime.UtcNow.Year + 1);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMembership_WhenExists_Returns200Ok()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipResponse>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(membership.Id);
        result.Data.FamilyMemberId.Should().Be(familyMember.Id);
    }

    [Fact]
    public async Task GetMembership_WhenNotExists_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateMembership_WhenExists_Returns204NoContent()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);

        // Act
        var response = await _authenticatedClient.DeleteAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify membership was deactivated
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipsRepository>();
        var updated = await repository.GetByFamilyMemberIdAsync(familyMember.Id, CancellationToken.None);
        updated.Should().BeNull(); // GetByFamilyMemberIdAsync only returns active memberships
    }

    [Fact]
    public async Task DeactivateMembership_WhenNotExists_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (user, familyUnit, familyMember) = await SeedTestDataAsync();

        // Act
        var response = await _authenticatedClient.DeleteAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkActivateMemberships_WithValidYear_Returns200WithActivatedCount()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, familyUnit, familyMember) = await SeedTestDataAsync();
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/membership/bulk",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<BulkActivateMembershipResponse>>(content, JsonOptions);
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Activated.Should().Be(1);
        result.Data.Skipped.Should().Be(0);
        result.Data.Results.Should().HaveCount(1);
        result.Data.Results[0].Status.Should().Be(BulkMembershipResultStatus.Activated);
    }

    [Fact]
    public async Task BulkActivateMemberships_WhenAllMembersAlreadyHaveMembership_Returns200WithZeroActivated()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, familyUnit, familyMember) = await SeedTestDataAsync();
        await CreateTestMembershipAsync(familyMember.Id);
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/membership/bulk",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<BulkActivateMembershipResponse>>(content, JsonOptions);
        result!.Data!.Activated.Should().Be(0);
        result.Data.Skipped.Should().Be(1);
        result.Data.Results[0].Status.Should().Be(BulkMembershipResultStatus.Skipped);
    }

    [Fact]
    public async Task BulkActivateMemberships_WithFutureYear_Returns400BadRequest()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, familyUnit, _) = await SeedTestDataAsync();
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year + 1);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/membership/bulk",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkActivateMemberships_WithFamilyNotFound_Returns404NotFound()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var nonExistentFamilyUnitId = Guid.NewGuid();
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/family-units/{nonExistentFamilyUnitId}/membership/bulk",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkActivateMemberships_WithNonBoardUser_Returns403Forbidden()
    {
        // Arrange — non-board (Member role) user
        await GetAuthTokenAsync();
        var (_, familyUnit, _) = await SeedTestDataAsync();
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/membership/bulk",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Helper methods
    private async Task<(User user, FamilyUnit familyUnit, FamilyMember familyMember)> SeedTestDataAsync()
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

        var familyMember = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = familyUnit.Id,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Relationship = FamilyRelationship.Parent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.FamilyUnits.Add(familyUnit);
        dbContext.FamilyMembers.Add(familyMember);
        await dbContext.SaveChangesAsync();

        return (user, familyUnit, familyMember);
    }

    // ─── CreateMembershipFee: POST /api/memberships/{id}/fees ────────────────

    [Fact]
    public async Task CreateMembershipFee_BoardUser_ValidRequest_Returns201Created()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, _, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);
        var request = new CreateMembershipFeeRequest(DateTime.UtcNow.Year, 25.00m);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipFeeResponse>>(JsonOptions);
        result!.Data.Should().NotBeNull();
        result.Data!.MembershipId.Should().Be(membership.Id);
        result.Data.Year.Should().Be(request.Year);
        result.Data.Amount.Should().Be(request.Amount);
        result.Data.Status.Should().Be(FeeStatus.Pending);
    }

    [Fact]
    public async Task CreateMembershipFee_NonExistentMembership_Returns404NotFound()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var request = new CreateMembershipFeeRequest(DateTime.UtcNow.Year, 0m);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/memberships/{Guid.NewGuid()}/fees",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateMembershipFee_DuplicateFeeForSameYear_Returns409Conflict()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, _, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);
        var year = DateTime.UtcNow.Year;
        await CreateTestFeeAsync(membership.Id, year);
        var request = new CreateMembershipFeeRequest(year, 25.00m);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateMembershipFee_MemberUser_Returns403Forbidden()
    {
        // Arrange — Member role user (not Board)
        await GetAuthTokenAsync();
        var (_, _, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);
        var request = new CreateMembershipFeeRequest(DateTime.UtcNow.Year, 25.00m);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateMembershipFee_FutureYear_Returns400BadRequest()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, _, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);
        var request = new CreateMembershipFeeRequest(DateTime.UtcNow.Year + 1, 0m);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMembershipFee_NegativeAmount_Returns400BadRequest()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, _, familyMember) = await SeedTestDataAsync();
        var membership = await CreateTestMembershipAsync(familyMember.Id);
        var request = new CreateMembershipFeeRequest(DateTime.UtcNow.Year, -1m);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── ReactivateMembership: POST /membership/reactivate ───────────────────

    [Fact]
    public async Task ReactivateMembership_BoardUser_InactiveMembership_Returns200Ok()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, familyUnit, familyMember) = await SeedTestDataAsync();
        await CreateTestInactiveMembershipAsync(familyMember.Id);
        var request = new ReactivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership/reactivate",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipResponse>>(JsonOptions);
        result!.Data.Should().NotBeNull();
        result.Data!.IsActive.Should().BeTrue();
        result.Data.EndDate.Should().BeNull();
    }

    [Fact]
    public async Task ReactivateMembership_NoMembershipRecord_Returns404NotFound()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, familyUnit, familyMember) = await SeedTestDataAsync();
        var request = new ReactivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership/reactivate",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReactivateMembership_AlreadyActiveMembership_Returns409Conflict()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, familyUnit, familyMember) = await SeedTestDataAsync();
        await CreateTestMembershipAsync(familyMember.Id);
        var request = new ReactivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership/reactivate",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReactivateMembership_MemberUser_Returns403Forbidden()
    {
        // Arrange — Member role user (not Board)
        await GetAuthTokenAsync();
        var (_, familyUnit, familyMember) = await SeedTestDataAsync();
        var request = new ReactivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership/reactivate",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReactivateMembership_FutureYear_Returns400BadRequest()
    {
        // Arrange
        var boardClient = await GetBoardAuthClientAsync();
        var (_, familyUnit, familyMember) = await SeedTestDataAsync();
        await CreateTestInactiveMembershipAsync(familyMember.Id);
        var request = new ReactivateMembershipRequest(DateTime.UtcNow.Year + 1);

        // Act
        var response = await boardClient.PostAsJsonAsync(
            $"/api/family-units/{familyUnit.Id}/members/{familyMember.Id}/membership/reactivate",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Helper methods
    private async Task<Membership> CreateTestMembershipAsync(Guid familyMemberId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = familyMemberId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Memberships.Add(membership);
        await dbContext.SaveChangesAsync();

        return membership;
    }

    private async Task<Membership> CreateTestInactiveMembershipAsync(Guid familyMemberId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = familyMemberId,
            StartDate = new DateTime(DateTime.UtcNow.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = DateTime.UtcNow.AddMonths(-1),
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Memberships.Add(membership);
        await dbContext.SaveChangesAsync();

        return membership;
    }

    private async Task<MembershipFee> CreateTestFeeAsync(Guid membershipId, int year)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        var fee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membershipId,
            Year = year,
            Amount = 50.00m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.MembershipFees.Add(fee);
        await dbContext.SaveChangesAsync();

        return fee;
    }
}

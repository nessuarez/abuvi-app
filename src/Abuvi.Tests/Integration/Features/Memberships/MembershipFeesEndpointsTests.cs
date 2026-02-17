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

public class MembershipFeesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
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

    public MembershipFeesEndpointsTests(WebApplicationFactory<Program> factory)
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
    public async Task GetFees_WhenFeesExist_Returns200Ok()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (membership, fees) = await SeedTestMembershipWithFeesAsync();

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/memberships/{membership.Id}/fees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<MembershipFeeResponse>>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(2);
        result.Data.Should().AllSatisfy(f => f.MembershipId.Should().Be(membership.Id));
    }

    [Fact]
    public async Task GetCurrentYearFee_WhenFeeExists_Returns200Ok()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (membership, fees) = await SeedTestMembershipWithFeesAsync();
        var currentYearFee = fees.First(f => f.Year == DateTime.UtcNow.Year);

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/memberships/{membership.Id}/fees/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipFeeResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Year.Should().Be(DateTime.UtcNow.Year);
        result.Data.Id.Should().Be(currentYearFee.Id);
    }

    [Fact]
    public async Task GetCurrentYearFee_WhenFeeDoesNotExist_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var membership = await SeedTestMembershipWithoutFeesAsync();

        // Act
        var response = await _authenticatedClient.GetAsync(
            $"/api/memberships/{membership.Id}/fees/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PayFee_WithValidData_Returns200Ok()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (membership, fees) = await SeedTestMembershipWithFeesAsync();
        var feeId = fees.First().Id;
        var request = new PayFeeRequest(DateTime.UtcNow.AddDays(-1), "REF-TEST-123");

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees/{feeId}/pay",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MembershipFeeResponse>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(FeeStatus.Paid);
        result.Data.PaidDate.Should().BeCloseTo(request.PaidDate, TimeSpan.FromSeconds(1));
        result.Data.PaymentReference.Should().Be("REF-TEST-123");
    }

    [Fact]
    public async Task PayFee_WithFutureDate_Returns400BadRequest()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (membership, fees) = await SeedTestMembershipWithFeesAsync();
        var feeId = fees.First().Id;
        var request = new PayFeeRequest(DateTime.UtcNow.AddDays(1), "REF-123");

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees/{feeId}/pay",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PayFee_WhenFeeAlreadyPaid_Returns409Conflict()
    {
        // Arrange
        await GetAuthTokenAsync();
        var (membership, fees) = await SeedTestMembershipWithFeesAsync();
        var feeId = fees.First().Id;

        // Pay the fee first time
        var firstRequest = new PayFeeRequest(DateTime.UtcNow.AddDays(-2), "REF-FIRST");
        await _authenticatedClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees/{feeId}/pay",
            firstRequest);

        // Try to pay again
        var secondRequest = new PayFeeRequest(DateTime.UtcNow.AddDays(-1), "REF-SECOND");

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees/{feeId}/pay",
            secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PayFee_WhenFeeDoesNotExist_Returns404NotFound()
    {
        // Arrange
        await GetAuthTokenAsync();
        var membership = await SeedTestMembershipWithoutFeesAsync();
        var nonExistentFeeId = Guid.NewGuid();
        var request = new PayFeeRequest(DateTime.UtcNow, "REF-123");

        // Act
        var response = await _authenticatedClient.PostAsJsonAsync(
            $"/api/memberships/{membership.Id}/fees/{nonExistentFeeId}/pay",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Helper methods
    private async Task<(Membership membership, List<MembershipFee> fees)> SeedTestMembershipWithFeesAsync()
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

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = familyMember.Id,
            StartDate = DateTime.UtcNow.AddYears(-1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var currentYear = DateTime.UtcNow.Year;
        var fees = new List<MembershipFee>
        {
            new()
            {
                Id = Guid.NewGuid(),
                MembershipId = membership.Id,
                Year = currentYear,
                Amount = 50.00m,
                Status = FeeStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                MembershipId = membership.Id,
                Year = currentYear - 1,
                Amount = 45.00m,
                Status = FeeStatus.Paid,
                PaidDate = DateTime.UtcNow.AddMonths(-6),
                PaymentReference = "REF-OLD",
                CreatedAt = DateTime.UtcNow.AddYears(-1),
                UpdatedAt = DateTime.UtcNow.AddMonths(-6)
            }
        };

        dbContext.Users.Add(user);
        dbContext.FamilyUnits.Add(familyUnit);
        dbContext.FamilyMembers.Add(familyMember);
        dbContext.Memberships.Add(membership);
        dbContext.MembershipFees.AddRange(fees);
        await dbContext.SaveChangesAsync();

        return (membership, fees);
    }

    private async Task<Membership> SeedTestMembershipWithoutFeesAsync()
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
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1995, 5, 5),
            Relationship = FamilyRelationship.Parent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = familyMember.Id,
            StartDate = DateTime.UtcNow.AddDays(-10),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.FamilyUnits.Add(familyUnit);
        dbContext.FamilyMembers.Add(familyMember);
        dbContext.Memberships.Add(membership);
        await dbContext.SaveChangesAsync();

        return membership;
    }
}

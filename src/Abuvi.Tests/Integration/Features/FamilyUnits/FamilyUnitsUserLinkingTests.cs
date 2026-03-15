using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        var loginResult = await DeserializeAsync<ApiResponse<LoginResponse>>(loginResponse);
        return loginResult!.Data!.Token;
    }

    /// <summary>
    /// Sets up a standard test scenario: User1 creates a family unit, User2 is added as a linked member.
    /// Returns the clients and IDs needed for further testing.
    /// </summary>
    private async Task<(HttpClient user1Client, HttpClient user2Client, Guid familyUnitId, Guid user2MemberId)>
        CreateFamilyWithLinkedMemberAsync()
    {
        var (user1Client, _, _) = await CreateAuthenticatedUserAsync();
        var (user2Client, user2Email, _) = await CreateAuthenticatedUserAsync();

        // User1 creates a family unit
        var createFamilyUnitRequest = new CreateFamilyUnitRequest("Access Test Family");
        var familyUnitResponse = await user1Client.PostAsJsonAsync("/api/family-units", createFamilyUnitRequest);
        var familyUnitResult = await DeserializeAsync<ApiResponse<FamilyUnitResponse>>(familyUnitResponse);
        var familyUnitId = familyUnitResult!.Data!.Id;

        // User1 adds User2 as a family member using User2's email
        var createMemberRequest = new CreateFamilyMemberRequest(
            "User2First",
            "User2Last",
            new DateOnly(1990, 5, 10),
            FamilyRelationship.Spouse,
            Email: user2Email
        );
        var memberResponse = await user1Client.PostAsJsonAsync(
            $"/api/family-units/{familyUnitId}/members",
            createMemberRequest);
        var memberResult = await DeserializeAsync<ApiResponse<FamilyMemberResponse>>(memberResponse);
        var user2MemberId = memberResult!.Data!.Id;

        return (user1Client, user2Client, familyUnitId, user2MemberId);
    }

    [Fact]
    public async Task LinkedMember_CanGetFamilyUnit_ViaMe()
    {
        // Arrange
        var (_, user2Client, familyUnitId, _) = await CreateFamilyWithLinkedMemberAsync();

        // Act — User2 gets their family unit via /me
        var response = await user2Client.GetAsync("/api/family-units/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<ApiResponse<FamilyUnitResponse>>(response);
        result!.Data!.Id.Should().Be(familyUnitId);
    }

    [Fact]
    public async Task LinkedMember_CanGetFamilyUnit_ById()
    {
        // Arrange
        var (_, user2Client, familyUnitId, _) = await CreateFamilyWithLinkedMemberAsync();

        // Act — User2 gets the family unit by ID
        var response = await user2Client.GetAsync($"/api/family-units/{familyUnitId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<ApiResponse<FamilyUnitResponse>>(response);
        result!.Data!.Id.Should().Be(familyUnitId);
    }

    [Fact]
    public async Task LinkedMember_CanGetFamilyMembers()
    {
        // Arrange
        var (_, user2Client, familyUnitId, _) = await CreateFamilyWithLinkedMemberAsync();

        // Act — User2 lists family members
        var response = await user2Client.GetAsync($"/api/family-units/{familyUnitId}/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<ApiResponse<IReadOnlyList<FamilyMemberResponse>>>(response);
        result!.Data.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LinkedMember_CannotUpdateFamilyUnit()
    {
        // Arrange
        var (_, user2Client, familyUnitId, _) = await CreateFamilyWithLinkedMemberAsync();
        var updateRequest = new UpdateFamilyUnitRequest("Hack");

        // Act — User2 attempts to update the family unit
        var response = await user2Client.PutAsJsonAsync($"/api/family-units/{familyUnitId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LinkedMember_CannotAddFamilyMember()
    {
        // Arrange
        var (_, user2Client, familyUnitId, _) = await CreateFamilyWithLinkedMemberAsync();
        var createMemberRequest = new CreateFamilyMemberRequest(
            "Hacker",
            "Person",
            new DateOnly(2000, 1, 1),
            FamilyRelationship.Other
        );

        // Act — User2 attempts to add a new family member
        var response = await user2Client.PostAsJsonAsync(
            $"/api/family-units/{familyUnitId}/members",
            createMemberRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LinkedMember_CanSeeRegistrations_ViaMyRegistrations()
    {
        // Arrange — seed an Open camp edition via DB then have User1 register
        var (user1Client, user2Client, familyUnitId, _) = await CreateFamilyWithLinkedMemberAsync();

        // Get User1's representative member ID (first member in the unit)
        var membersResponse = await user1Client.GetAsync($"/api/family-units/{familyUnitId}/members");
        var membersResult = await DeserializeAsync<ApiResponse<IReadOnlyList<FamilyMemberResponse>>>(membersResponse);
        var representativeMemberId = membersResult!.Data!.First().Id;

        // Seed a camp and open edition directly in the DB
        Guid editionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
            var camp = new Camp
            {
                Id = Guid.NewGuid(),
                Name = $"IntegTest Camp {Guid.NewGuid()}",
                Location = "Test Location",
                PricePerAdult = 180m,
                PricePerChild = 120m,
                PricePerBaby = 60m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Camps.Add(camp);

            var edition = new CampEdition
            {
                Id = Guid.NewGuid(),
                CampId = camp.Id,
                Year = 2090,
                StartDate = new DateTime(2090, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2090, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                Status = CampEditionStatus.Open,
                MaxCapacity = 100,
                PricePerAdult = 180m,
                PricePerChild = 120m,
                PricePerBaby = 60m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.CampEditions.Add(edition);

            // Seed membership with paid fee for registration validation
            var membership = new Membership
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = representativeMemberId,
                StartDate = DateTime.UtcNow.AddYears(-1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Memberships.Add(membership);
            db.MembershipFees.Add(new MembershipFee
            {
                Id = Guid.NewGuid(),
                MembershipId = membership.Id,
                Year = DateTime.UtcNow.Year,
                Amount = 50m,
                Status = FeeStatus.Paid,
                PaidDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
            editionId = edition.Id;
        }

        // User1 creates a registration
        var createRegistrationRequest = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: familyUnitId,
            Members: new List<MemberAttendanceRequest>
            {
                new(representativeMemberId, AttendancePeriod.Complete)
            },
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null
        );
        var registrationResponse = await user1Client.PostAsJsonAsync("/api/registrations", createRegistrationRequest);
        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "User1 should be able to register their family unit");

        // Act — User2 lists registrations
        var response = await user2Client.GetAsync("/api/registrations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<ApiResponse<List<RegistrationListResponse>>>(response);
        result!.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task LinkedMember_CanSeeRegistrationDetail()
    {
        // Arrange — same setup as above
        var (user1Client, user2Client, familyUnitId, _) = await CreateFamilyWithLinkedMemberAsync();

        var membersResponse = await user1Client.GetAsync($"/api/family-units/{familyUnitId}/members");
        var membersResult = await DeserializeAsync<ApiResponse<IReadOnlyList<FamilyMemberResponse>>>(membersResponse);
        var representativeMemberId = membersResult!.Data!.First().Id;

        Guid editionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
            var camp = new Camp
            {
                Id = Guid.NewGuid(),
                Name = $"IntegTest Camp {Guid.NewGuid()}",
                Location = "Test Location",
                PricePerAdult = 180m,
                PricePerChild = 120m,
                PricePerBaby = 60m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Camps.Add(camp);

            var edition = new CampEdition
            {
                Id = Guid.NewGuid(),
                CampId = camp.Id,
                Year = 2091,
                StartDate = new DateTime(2091, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2091, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                Status = CampEditionStatus.Open,
                MaxCapacity = 100,
                PricePerAdult = 180m,
                PricePerChild = 120m,
                PricePerBaby = 60m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.CampEditions.Add(edition);

            // Seed membership with paid fee for registration validation
            var membership2 = new Membership
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = representativeMemberId,
                StartDate = DateTime.UtcNow.AddYears(-1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Memberships.Add(membership2);
            db.MembershipFees.Add(new MembershipFee
            {
                Id = Guid.NewGuid(),
                MembershipId = membership2.Id,
                Year = DateTime.UtcNow.Year,
                Amount = 50m,
                Status = FeeStatus.Paid,
                PaidDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
            editionId = edition.Id;
        }

        var createRegistrationRequest = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: familyUnitId,
            Members: new List<MemberAttendanceRequest>
            {
                new(representativeMemberId, AttendancePeriod.Complete)
            },
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null
        );
        var registrationResponse = await user1Client.PostAsJsonAsync("/api/registrations", createRegistrationRequest);
        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "User1 should be able to register their family unit");
        var registrationResult = await DeserializeAsync<ApiResponse<RegistrationResponse>>(registrationResponse);
        var registrationId = registrationResult!.Data!.Id;

        // Act — User2 gets the registration detail
        var response = await user2Client.GetAsync($"/api/registrations/{registrationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<ApiResponse<RegistrationResponse>>(response);
        result!.Data!.Id.Should().Be(registrationId);
    }

    [Fact]
    public async Task LinkedMember_CanGetFamilyMemberById()
    {
        // Arrange
        var (_, user2Client, familyUnitId, user2MemberId) = await CreateFamilyWithLinkedMemberAsync();

        // Act — User2 gets their own member record by ID
        var response = await user2Client.GetAsync(
            $"/api/family-units/{familyUnitId}/members/{user2MemberId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<ApiResponse<FamilyMemberResponse>>(response);
        result!.Data!.Id.Should().Be(user2MemberId);
    }

    [Fact]
    public async Task LinkedMember_CannotCancelRegistration()
    {
        // Arrange — same setup as registration tests
        var (user1Client, user2Client, familyUnitId, _) = await CreateFamilyWithLinkedMemberAsync();

        var membersResponse = await user1Client.GetAsync($"/api/family-units/{familyUnitId}/members");
        var membersResult = await DeserializeAsync<ApiResponse<IReadOnlyList<FamilyMemberResponse>>>(membersResponse);
        var representativeMemberId = membersResult!.Data!.First().Id;

        Guid editionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
            var camp = new Camp
            {
                Id = Guid.NewGuid(),
                Name = $"IntegTest Camp {Guid.NewGuid()}",
                Location = "Test Location",
                PricePerAdult = 180m,
                PricePerChild = 120m,
                PricePerBaby = 60m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Camps.Add(camp);

            var edition = new CampEdition
            {
                Id = Guid.NewGuid(),
                CampId = camp.Id,
                Year = 2092,
                StartDate = new DateTime(2092, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2092, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                Status = CampEditionStatus.Open,
                MaxCapacity = 100,
                PricePerAdult = 180m,
                PricePerChild = 120m,
                PricePerBaby = 60m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.CampEditions.Add(edition);

            var membership = new Membership
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = representativeMemberId,
                StartDate = DateTime.UtcNow.AddYears(-1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Memberships.Add(membership);
            db.MembershipFees.Add(new MembershipFee
            {
                Id = Guid.NewGuid(),
                MembershipId = membership.Id,
                Year = DateTime.UtcNow.Year,
                Amount = 50m,
                Status = FeeStatus.Paid,
                PaidDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
            editionId = edition.Id;
        }

        // User1 creates a registration
        var createRegistrationRequest = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: familyUnitId,
            Members: new List<MemberAttendanceRequest>
            {
                new(representativeMemberId, AttendancePeriod.Complete)
            },
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null
        );
        var registrationResponse = await user1Client.PostAsJsonAsync("/api/registrations", createRegistrationRequest);
        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var registrationResult = await DeserializeAsync<ApiResponse<RegistrationResponse>>(registrationResponse);
        var registrationId = registrationResult!.Data!.Id;

        // Act — User2 (linked member) tries to cancel
        var response = await user2Client.PostAsync($"/api/registrations/{registrationId}/cancel", null);

        // Assert — should be rejected (only representative can cancel; BusinessRuleException → 422)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
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

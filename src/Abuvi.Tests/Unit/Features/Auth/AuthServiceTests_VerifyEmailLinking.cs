namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Unit tests for auto-linking family members when a user verifies their email.
/// Scenario: representative adds family member with email X → user registers with email X →
/// on email verification, system should auto-link the family member to the new user.
/// </summary>
public class AuthServiceTests_VerifyEmailLinking
{
    private readonly IUsersRepository _usersRepository;
    private readonly IFamilyUnitsRepository _familyUnitsRepository;
    private readonly IEmailService _emailService;
    private readonly AuthService _service;

    public AuthServiceTests_VerifyEmailLinking()
    {
        _usersRepository = Substitute.For<IUsersRepository>();
        _familyUnitsRepository = Substitute.For<IFamilyUnitsRepository>();
        _emailService = Substitute.For<IEmailService>();

        var passwordHasher = Substitute.For<IPasswordHasher>();
        var jwtConfig = Substitute.For<IConfiguration>();
        var jwtTokenService = Substitute.For<JwtTokenService>(jwtConfig);
        var logger = Substitute.For<ILogger<AuthService>>();

        var configDict = new Dictionary<string, string?>
        {
            ["FrontendUrl"] = "http://localhost:5173"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _service = new AuthService(
            _usersRepository,
            passwordHasher,
            jwtTokenService,
            _emailService,
            configuration,
            _familyUnitsRepository,
            logger);
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenPendingFamilyMemberExists_LinksUserToFamilyMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var email = "newuser@example.com";

        var user = CreateUnverifiedUser(userId, email);
        var pendingMember = new FamilyMember
        {
            Id = memberId,
            FamilyUnitId = familyUnitId,
            UserId = null, // Not linked yet
            FirstName = "New",
            LastName = "User",
            Email = email,
            DateOfBirth = new DateOnly(1990, 5, 10),
            Relationship = FamilyRelationship.Spouse,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _usersRepository.GetByVerificationTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _familyUnitsRepository.GetFamilyMembersByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(new List<FamilyMember> { pendingMember });

        // Act
        await _service.VerifyEmailAsync("valid-token", CancellationToken.None);

        // Assert — family member should be linked to the user
        await _familyUnitsRepository.Received(1).UpdateFamilyMemberAsync(
            Arg.Is<FamilyMember>(fm => fm.Id == memberId && fm.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenMultiplePendingFamilyMembers_LinksAllOfThem()
    {
        // Arrange — user is a member in two different family units
        var userId = Guid.NewGuid();
        var email = "shared@example.com";

        var user = CreateUnverifiedUser(userId, email);
        var member1 = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = Guid.NewGuid(),
            UserId = null,
            FirstName = "Shared",
            LastName = "Person",
            Email = email,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Relationship = FamilyRelationship.Spouse,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var member2 = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = Guid.NewGuid(),
            UserId = null,
            FirstName = "Shared",
            LastName = "Person",
            Email = email,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Relationship = FamilyRelationship.Other,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _usersRepository.GetByVerificationTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _familyUnitsRepository.GetFamilyMembersByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(new List<FamilyMember> { member1, member2 });

        // Act
        await _service.VerifyEmailAsync("valid-token", CancellationToken.None);

        // Assert — both family members should be linked
        await _familyUnitsRepository.Received(2).UpdateFamilyMemberAsync(
            Arg.Is<FamilyMember>(fm => fm.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenNoPendingFamilyMembers_StillVerifiesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "nofamily@example.com";
        var user = CreateUnverifiedUser(userId, email);

        _usersRepository.GetByVerificationTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _familyUnitsRepository.GetFamilyMembersByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(new List<FamilyMember>());

        // Act
        await _service.VerifyEmailAsync("valid-token", CancellationToken.None);

        // Assert — verification still completes, no family member updates
        await _usersRepository.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.EmailVerified && u.IsActive),
            Arg.Any<CancellationToken>());
        await _familyUnitsRepository.DidNotReceive().UpdateFamilyMemberAsync(
            Arg.Any<FamilyMember>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenTokenExpired_ThrowsAndDoesNotLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "expired@example.com";
        var user = CreateUnverifiedUser(userId, email);
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1); // Expired

        _usersRepository.GetByVerificationTokenAsync("expired-token", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var act = async () => await _service.VerifyEmailAsync("expired-token", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*expirado*");

        // No linking should have occurred
        await _familyUnitsRepository.DidNotReceive().GetFamilyMembersByEmailAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_AfterLinking_SendsWelcomeEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "welcome@example.com";
        var user = CreateUnverifiedUser(userId, email);

        var pendingMember = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyUnitId = Guid.NewGuid(),
            UserId = null,
            FirstName = "Welcome",
            LastName = "User",
            Email = email,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Relationship = FamilyRelationship.Spouse,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _usersRepository.GetByVerificationTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _familyUnitsRepository.GetFamilyMembersByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(new List<FamilyMember> { pendingMember });

        // Act
        await _service.VerifyEmailAsync("valid-token", CancellationToken.None);

        // Assert — welcome email is still sent after linking
        await _emailService.Received(1).SendWelcomeEmailAsync(
            email, user.FirstName, user.LastName, Arg.Any<CancellationToken>());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static User CreateUnverifiedUser(Guid id, string email) => new()
    {
        Id = id,
        Email = email,
        PasswordHash = "hashed",
        FirstName = "Test",
        LastName = "User",
        Role = UserRole.Member,
        IsActive = false,
        EmailVerified = false,
        EmailVerificationToken = "valid-token",
        EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(23),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;

namespace Abuvi.Tests.Unit.Features.Auth;

/// <summary>
/// Unit tests for JwtTokenService
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class JwtTokenServiceTests
{
    private readonly JwtTokenService _sut;
    private readonly IConfiguration _configuration;

    public JwtTokenServiceTests()
    {
        // Arrange - Mock configuration
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Secret", "test-secret-key-at-least-32-characters-long-for-hmacsha256"},
            {"Jwt:Issuer", "https://test.api"},
            {"Jwt:Audience", "https://test.app"},
            {"Jwt:ExpiryInHours", "24"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _sut = new JwtTokenService(_configuration);
    }

    [Fact]
    public void GenerateToken_WithValidUser_ReturnsValidJwtToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = UserRole.Member,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Member");
    }

    [Fact]
    public void GenerateToken_WithAdminUser_IncludesAdminRole()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            Role = UserRole.Admin,
            FirstName = "Admin",
            LastName = "User",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GenerateToken_WithBoardUser_IncludesBoardRole()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "board@example.com",
            Role = UserRole.Board,
            FirstName = "Board",
            LastName = "Member",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Board");
    }

    [Fact]
    public void GenerateToken_TokenExpiresAfterConfiguredHours()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = UserRole.Member,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var token = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var expiryTime = jwtToken.ValidTo;
        var expectedExpiry = DateTime.UtcNow.AddHours(24);

        expiryTime.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GenerateToken_TokenContainsUniqueJti()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = UserRole.Member,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var token1 = _sut.GenerateToken(user);
        var token2 = _sut.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken1 = handler.ReadJwtToken(token1);
        var jwtToken2 = handler.ReadJwtToken(token2);

        var jti1 = jwtToken1.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        var jti2 = jwtToken2.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

        jti1.Should().NotBeNullOrEmpty();
        jti2.Should().NotBeNullOrEmpty();
        jti1.Should().NotBe(jti2); // Each token should have unique identifier
    }
}

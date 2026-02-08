using FluentAssertions;
using Xunit;
using Abuvi.API.Features.Auth;

namespace Abuvi.Tests.Unit.Features.Auth;

/// <summary>
/// Unit tests for PasswordHasher implementation
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class PasswordHasherTests
{
    private readonly IPasswordHasher _sut;

    public PasswordHasherTests()
    {
        _sut = new PasswordHasher();
    }

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsNonEmptyHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _sut.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().StartWith("$2"); // BCrypt hash prefix
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ReturnsDifferentHashes()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _sut.HashPassword(password);
        var hash2 = _sut.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2); // BCrypt uses random salt
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(string.Empty, hash);

        // Assert
        result.Should().BeFalse();
    }
}

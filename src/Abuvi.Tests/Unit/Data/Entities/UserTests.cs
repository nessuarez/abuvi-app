namespace Abuvi.Tests.Unit.Data.Entities;

using Abuvi.API.Features.Users;
using FluentAssertions;
using Xunit;

public class UserTests
{
    [Fact]
    public void User_WhenCreated_ShouldHaveEmailVerifiedFalseByDefault()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        // Assert
        user.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public void User_WhenCreated_ShouldHaveIsActiveFalseByDefault()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        // Assert
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void User_DocumentNumber_ShouldAcceptValidFormat()
    {
        // Arrange & Act
        var user = new User
        {
            DocumentNumber = "12345678A",
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        // Assert
        user.DocumentNumber.Should().Be("12345678A");
    }
}

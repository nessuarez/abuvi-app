using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;
using Abuvi.API.Features.Auth;

namespace Abuvi.Tests.Unit.Features.Auth;

/// <summary>
/// Unit tests for Auth validators
/// </summary>
public class AuthValidatorsTests
{
    #region LoginRequestValidator Tests

    [Fact]
    public void LoginRequestValidator_WithValidData_PassesValidation()
    {
        // Arrange
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(
            Email: "test@example.com",
            Password: "password123"
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void LoginRequestValidator_WithEmptyEmail_FailsValidation(string? email)
    {
        // Arrange
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(
            Email: email,
            Password: "password123"
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    public void LoginRequestValidator_WithInvalidEmail_FailsValidation(string email)
    {
        // Arrange
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(
            Email: email,
            Password: "password123"
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void LoginRequestValidator_WithEmptyPassword_FailsValidation(string? password)
    {
        // Arrange
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(
            Email: "test@example.com",
            Password: password
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    #endregion

    #region RegisterRequestValidator Tests

    [Fact]
    public void RegisterRequestValidator_WithValidData_PassesValidation()
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123!",
            FirstName: "John",
            LastName: "Doe",
            Phone: "555-1234"
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void RegisterRequestValidator_WithEmptyEmail_FailsValidation(string? email)
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: email,
            Password: "Password123!",
            FirstName: "John",
            LastName: "Doe",
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    public void RegisterRequestValidator_WithInvalidEmail_FailsValidation(string email)
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: email,
            Password: "Password123!",
            FirstName: "John",
            LastName: "Doe",
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void RegisterRequestValidator_WithEmailTooLong_FailsValidation()
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var longEmail = new string('a', 250) + "@example.com"; // > 255 chars
        var request = new RegisterRequest(
            Email: longEmail,
            Password: "Password123!",
            FirstName: "John",
            LastName: "Doe",
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("short")] // Too short
    [InlineData("nouppercase123")] // No uppercase
    [InlineData("NOLOWERCASE123")] // No lowercase
    [InlineData("NoNumbers")] // No numbers
    public void RegisterRequestValidator_WithWeakPassword_FailsValidation(string password)
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: password,
            FirstName: "John",
            LastName: "Doe",
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void RegisterRequestValidator_WithEmptyFirstName_FailsValidation(string? firstName)
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123!",
            FirstName: firstName,
            LastName: "Doe",
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void RegisterRequestValidator_WithFirstNameTooLong_FailsValidation()
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123!",
            FirstName: new string('a', 101), // > 100 chars
            LastName: "Doe",
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void RegisterRequestValidator_WithEmptyLastName_FailsValidation(string? lastName)
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123!",
            FirstName: "John",
            LastName: lastName,
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void RegisterRequestValidator_WithLastNameTooLong_FailsValidation()
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123!",
            FirstName: "John",
            LastName: new string('a', 101), // > 100 chars
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void RegisterRequestValidator_WithNullPhone_PassesValidation()
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123!",
            FirstName: "John",
            LastName: "Doe",
            Phone: null
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void RegisterRequestValidator_WithPhoneTooLong_FailsValidation()
    {
        // Arrange
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123!",
            FirstName: "John",
            LastName: "Doe",
            Phone: new string('1', 21) // > 20 chars
        );

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    #endregion
}

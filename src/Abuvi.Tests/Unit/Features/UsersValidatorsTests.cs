using Abuvi.API.Features.Users;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Abuvi.Tests.Unit.Features;

/// <summary>
/// Unit tests for Users validators
/// </summary>
public class UsersValidatorsTests
{
    private readonly CreateUserRequestValidator _createValidator;
    private readonly UpdateUserRequestValidator _updateValidator;

    public UsersValidatorsTests()
    {
        _createValidator = new CreateUserRequestValidator();
        _updateValidator = new UpdateUserRequestValidator();
    }

    #region CreateUserRequestValidator Tests

    [Fact]
    public void CreateValidator_ValidRequest_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new CreateUserRequest(
            "valid@example.com",
            "Password123!",
            "John",
            "Doe",
            "+1234567890",
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CreateValidator_EmptyEmail_ShouldHaveValidationError(string? email)
    {
        // Arrange
        var request = new CreateUserRequest(
            email!,
            "Password123!",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    public void CreateValidator_InvalidEmailFormat_ShouldHaveValidationError(string email)
    {
        // Arrange
        var request = new CreateUserRequest(
            email,
            "Password123!",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must be a valid email address");
    }

    [Fact]
    public void CreateValidator_EmailTooLong_ShouldHaveValidationError()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@example.com";
        var request = new CreateUserRequest(
            longEmail,
            "Password123!",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CreateValidator_EmptyPassword_ShouldHaveValidationError(string? password)
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            password!,
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void CreateValidator_PasswordTooShort_ShouldHaveValidationError()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "Pass1!",
            "John",
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CreateValidator_EmptyFirstName_ShouldHaveValidationError(string? firstName)
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "Password123!",
            firstName!,
            "Doe",
            null,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CreateValidator_EmptyLastName_ShouldHaveValidationError(string? lastName)
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "Password123!",
            "John",
            lastName!,
            null,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData("+1234567890")]
    [InlineData("123-456-7890")]
    [InlineData("(123) 456-7890")]
    [InlineData("+34 912 345 678")]
    public void CreateValidator_ValidPhoneFormat_ShouldNotHaveValidationError(string phone)
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "Password123!",
            "John",
            "Doe",
            phone,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData("invalid-phone")]
    [InlineData("abc123")]
    public void CreateValidator_InvalidPhoneFormat_ShouldHaveValidationError(string phone)
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "Password123!",
            "John",
            "Doe",
            phone,
            UserRole.Member
        );

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Phone)
            .WithErrorMessage("Phone must be a valid phone number");
    }

    #endregion

    #region UpdateUserRequestValidator Tests

    [Fact]
    public void UpdateValidator_ValidRequest_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new UpdateUserRequest(
            "John",
            "Doe",
            "+1234567890",
            true
        );

        // Act
        var result = _updateValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void UpdateValidator_EmptyFirstName_ShouldHaveValidationError(string? firstName)
    {
        // Arrange
        var request = new UpdateUserRequest(
            firstName!,
            "Doe",
            null,
            true
        );

        // Act
        var result = _updateValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void UpdateValidator_EmptyLastName_ShouldHaveValidationError(string? lastName)
    {
        // Arrange
        var request = new UpdateUserRequest(
            "John",
            lastName!,
            null,
            true
        );

        // Act
        var result = _updateValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData("+1234567890")]
    [InlineData("123-456-7890")]
    [InlineData(null)]
    public void UpdateValidator_ValidPhone_ShouldNotHaveValidationError(string? phone)
    {
        // Arrange
        var request = new UpdateUserRequest(
            "John",
            "Doe",
            phone,
            true
        );

        // Act
        var result = _updateValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    #endregion
}

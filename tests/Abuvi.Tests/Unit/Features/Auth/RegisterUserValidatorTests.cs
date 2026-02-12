namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Features.Auth;
using FluentAssertions;
using Xunit;

public class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidRequest_ShouldPass()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            "+34612345678",
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task Validate_WithInvalidEmail_ShouldFail(string email)
    {
        // Arrange
        var request = new RegisterUserRequest(
            email,
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("lowercase1!")]
    [InlineData("UPPERCASE1!")]
    [InlineData("NoDigits!")]
    [InlineData("NoSpecial1")]
    public async Task Validate_WithInvalidPassword_ShouldFail(string password)
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            password,
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Theory]
    [InlineData("")]
    public async Task Validate_WithInvalidFirstName_ShouldFail(string firstName)
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            firstName,
            "Doe",
            "12345678A",
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345678a")]
    public async Task Validate_WithInvalidDocumentNumber_ShouldFail(string documentNumber)
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            documentNumber,
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentNumber");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Validate_WithEmptyDocumentNumber_ShouldPass(string? documentNumber)
    {
        // Arrange - DocumentNumber is optional
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            documentNumber,
            null,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithTermsNotAccepted_ShouldFail()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            false // Terms not accepted
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "AcceptedTerms" &&
            e.ErrorMessage.Contains("must accept"));
    }

    [Theory]
    [InlineData("+34612345678", true)]  // Valid E.164
    [InlineData("", true)]              // Empty is allowed
    [InlineData(null, true)]            // Null is allowed
    [InlineData("invalid", false)]      // Invalid format
    public async Task Validate_WithPhone_ShouldValidateFormat(string? phone, bool shouldBeValid)
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            phone,
            true
        );

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        if (shouldBeValid)
        {
            result.Errors.Should().NotContain(e => e.PropertyName == "Phone");
        }
        else
        {
            result.Errors.Should().Contain(e => e.PropertyName == "Phone");
        }
    }
}

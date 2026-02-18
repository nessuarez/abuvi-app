using Abuvi.API.Features.Guests;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Guests;

public class UpdateGuestValidatorTests
{
    private readonly UpdateGuestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_PassesValidation()
    {
        // Arrange
        var request = new UpdateGuestRequest(
            "Jane",
            "Doe",
            new DateOnly(1995, 5, 15),
            DocumentNumber: "ABC123",
            Email: "jane@example.com",
            Phone: "+34612345678"
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenFirstNameIsEmpty_FailsValidation()
    {
        // Arrange
        var request = new UpdateGuestRequest("", "Doe", new DateOnly(1995, 5, 15));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void Validate_WhenLastNameIsEmpty_FailsValidation()
    {
        // Arrange
        var request = new UpdateGuestRequest("Jane", "", new DateOnly(1995, 5, 15));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
    }

    [Fact]
    public void Validate_WhenDateOfBirthIsFuture_FailsValidation()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var request = new UpdateGuestRequest("Jane", "Doe", futureDate);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DateOfBirth");
    }

    [Fact]
    public void Validate_WhenDocumentNumberHasSpecialChars_FailsValidation()
    {
        // Arrange
        var request = new UpdateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            DocumentNumber: "ABC-123");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentNumber");
    }

    [Fact]
    public void Validate_WhenEmailIsInvalid_FailsValidation()
    {
        // Arrange
        var request = new UpdateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            Email: "invalid");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_WhenPhoneHasNoPlus_FailsValidation()
    {
        // Arrange
        var request = new UpdateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            Phone: "34612345678");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Phone");
    }
}

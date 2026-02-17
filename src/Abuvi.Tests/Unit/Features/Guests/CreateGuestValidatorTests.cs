using Abuvi.API.Features.Guests;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Guests;

public class CreateGuestValidatorTests
{
    private readonly CreateGuestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_PassesValidation()
    {
        // Arrange
        var request = new CreateGuestRequest(
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
    public void Validate_WithMinimalRequest_PassesValidation()
    {
        // Arrange
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenFirstNameIsEmpty_FailsValidation()
    {
        // Arrange
        var request = new CreateGuestRequest("", "Doe", new DateOnly(1995, 5, 15));

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
        var request = new CreateGuestRequest("Jane", "", new DateOnly(1995, 5, 15));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
    }

    [Fact]
    public void Validate_WhenFirstNameExceeds100Characters_FailsValidation()
    {
        // Arrange
        var longName = new string('A', 101);
        var request = new CreateGuestRequest(longName, "Doe", new DateOnly(1995, 5, 15));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void Validate_WhenDateOfBirthIsFuture_FailsValidation()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var request = new CreateGuestRequest("Jane", "Doe", futureDate);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DateOfBirth");
    }

    [Fact]
    public void Validate_WhenDateOfBirthIsToday_FailsValidation()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new CreateGuestRequest("Jane", "Doe", today);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DateOfBirth");
    }

    [Fact]
    public void Validate_WhenDocumentNumberHasLowercase_FailsValidation()
    {
        // Arrange
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            DocumentNumber: "abc123");

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
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            Email: "not-an-email");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_WhenPhoneIsInvalidFormat_FailsValidation()
    {
        // Arrange
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            Phone: "612345678");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Phone");
    }

    [Fact]
    public void Validate_WhenMedicalNotesExceed2000Characters_FailsValidation()
    {
        // Arrange
        var longNotes = new string('A', 2001);
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            MedicalNotes: longNotes);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MedicalNotes");
    }

    [Fact]
    public void Validate_WhenAllergiesExceed1000Characters_FailsValidation()
    {
        // Arrange
        var longAllergies = new string('A', 1001);
        var request = new CreateGuestRequest("Jane", "Doe", new DateOnly(1995, 5, 15),
            Allergies: longAllergies);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Allergies");
    }

    [Fact]
    public void Validate_WhenOptionalFieldsAreNull_PassesValidation()
    {
        // Arrange
        var request = new CreateGuestRequest(
            "Jane",
            "Doe",
            new DateOnly(1995, 5, 15),
            DocumentNumber: null,
            Email: null,
            Phone: null,
            MedicalNotes: null,
            Allergies: null
        );

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

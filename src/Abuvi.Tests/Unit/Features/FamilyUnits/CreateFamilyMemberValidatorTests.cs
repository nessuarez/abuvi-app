namespace Abuvi.Tests.Unit.Features.FamilyUnits;

using Abuvi.API.Features.FamilyUnits;
using FluentAssertions;
using Xunit;

public class CreateFamilyMemberValidatorTests
{
    private readonly CreateFamilyMemberValidator _validator = new();

    [Fact]
    public async Task Validate_WhenAllFieldsValid_ShouldPass()
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            "ABC123", "maria@example.com", "+34612345678", "Asthma", "Peanuts");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenFirstNameEmpty_ShouldFail(string? firstName)
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            firstName!, "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName" && e.ErrorMessage == "El nombre es obligatorio");
    }

    [Fact]
    public async Task Validate_WhenFirstNameTooLong_ShouldFail()
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            new string('A', 101), "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenLastNameEmpty_ShouldFail(string? lastName)
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", lastName!, new DateOnly(2015, 6, 15), FamilyRelationship.Child);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName" && e.ErrorMessage == "Los apellidos son obligatorios");
    }

    [Fact]
    public async Task Validate_WhenDateOfBirthInFuture_ShouldFail()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", futureDate, FamilyRelationship.Child);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DateOfBirth" && e.ErrorMessage == "La fecha de nacimiento debe ser una fecha pasada");
    }

    [Fact]
    public async Task Validate_WhenRelationshipInvalid_ShouldFail()
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), (FamilyRelationship)999);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Relationship");
    }

    [Theory]
    [InlineData("abc123")]  // Lowercase
    [InlineData("ABC-123")] // Special characters
    public async Task Validate_WhenDocumentNumberInvalidFormat_ShouldFail(string documentNumber)
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            DocumentNumber: documentNumber);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentNumber");
    }

    [Fact]
    public async Task Validate_WhenDocumentNumberUppercaseAlphanumeric_ShouldPass()
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            DocumentNumber: "ABC123");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    public async Task Validate_WhenEmailInvalid_ShouldFail(string email)
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            Email: email);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorMessage == "Formato de correo electrónico inválido");
    }

    [Theory]
    [InlineData("612345678")]     // Missing country code
    [InlineData("+346123456789012345")] // Too long
    public async Task Validate_WhenPhoneNotE164Format_ShouldFail(string phone)
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            Phone: phone);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Phone");
    }

    [Fact]
    public async Task Validate_WhenMedicalNotesTooLong_ShouldFail()
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            MedicalNotes: new string('A', 2001));

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MedicalNotes");
    }

    [Fact]
    public async Task Validate_WhenAllergiesTooLong_ShouldFail()
    {
        // Arrange
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            Allergies: new string('A', 1001));

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Allergies");
    }
}

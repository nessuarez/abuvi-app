namespace Abuvi.Tests.Unit.Features.FamilyUnits;

using Abuvi.API.Features.FamilyUnits;
using FluentAssertions;
using Xunit;

public class UpdateFamilyMemberValidatorTests
{
    private readonly UpdateFamilyMemberValidator _validator = new();

    [Fact]
    public async Task Validate_WhenAllFieldsValid_ShouldPass()
    {
        // Arrange
        var request = new UpdateFamilyMemberRequest(
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
        var request = new UpdateFamilyMemberRequest(
            firstName!, "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName" && e.ErrorMessage == "El nombre es obligatorio");
    }

    [Fact]
    public async Task Validate_WhenDateOfBirthInFuture_ShouldFail()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var request = new UpdateFamilyMemberRequest(
            "Maria", "Garcia", futureDate, FamilyRelationship.Child);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DateOfBirth" && e.ErrorMessage == "La fecha de nacimiento debe ser una fecha pasada");
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("ABC-123")]
    public async Task Validate_WhenDocumentNumberInvalidFormat_ShouldFail(string documentNumber)
    {
        // Arrange
        var request = new UpdateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            DocumentNumber: documentNumber);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentNumber");
    }

    [Fact]
    public async Task Validate_WhenEmailInvalid_ShouldFail()
    {
        // Arrange
        var request = new UpdateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            Email: "invalid-email");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorMessage == "Formato de correo electrónico inválido");
    }
}

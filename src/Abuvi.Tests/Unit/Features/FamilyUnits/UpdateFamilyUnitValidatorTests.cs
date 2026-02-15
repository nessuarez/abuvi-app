namespace Abuvi.Tests.Unit.Features.FamilyUnits;

using Abuvi.API.Features.FamilyUnits;
using FluentAssertions;
using Xunit;

public class UpdateFamilyUnitValidatorTests
{
    private readonly UpdateFamilyUnitValidator _validator = new();

    [Fact]
    public async Task Validate_WhenNameValid_ShouldPass()
    {
        // Arrange
        var request = new UpdateFamilyUnitRequest("Garcia Family");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenNameEmpty_ShouldFail(string? name)
    {
        // Arrange
        var request = new UpdateFamilyUnitRequest(name!);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage == "El nombre de la unidad familiar es obligatorio");
    }

    [Fact]
    public async Task Validate_WhenNameTooLong_ShouldFail()
    {
        // Arrange
        var request = new UpdateFamilyUnitRequest(new string('A', 201));

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage == "El nombre de la unidad familiar no puede exceder 200 caracteres");
    }
}

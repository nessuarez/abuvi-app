using Abuvi.API.Features.Memories;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memories;

public class MemoriesValidatorTests
{
    private readonly CreateMemoryRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Passes()
    {
        // Arrange
        var request = new CreateMemoryRequest("My Memory", "Great content about camp", 1990, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullYear_Passes()
    {
        // Arrange
        var request = new CreateMemoryRequest("My Memory", "Content", null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyTitle_Fails()
    {
        // Arrange
        var request = new CreateMemoryRequest("", "Content", null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_WithTitleOver200Chars_Fails()
    {
        // Arrange
        var longTitle = new string('A', 201);
        var request = new CreateMemoryRequest(longTitle, "Content", null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_WithEmptyContent_Fails()
    {
        // Arrange
        var request = new CreateMemoryRequest("Title", "", null, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Content");
    }

    [Fact]
    public void Validate_WithYearBelow1975_Fails()
    {
        // Arrange
        var request = new CreateMemoryRequest("Title", "Content", 1974, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Year");
    }

    [Fact]
    public void Validate_WithYearAbove2026_Fails()
    {
        // Arrange
        var request = new CreateMemoryRequest("Title", "Content", 2027, null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Year");
    }
}

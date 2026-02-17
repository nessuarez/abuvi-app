using Abuvi.API.Features.Memberships;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class CreateMembershipValidatorTests
{
    private readonly CreateMembershipValidator _validator;

    public CreateMembershipValidatorTests()
    {
        _validator = new CreateMembershipValidator();
    }

    [Fact]
    public void Validate_WhenStartDateIsValid_PassesValidation()
    {
        // Arrange
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddDays(-1));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenStartDateIsDefault_FailsValidation()
    {
        // Arrange
        var request = new CreateMembershipRequest(default);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateMembershipRequest.StartDate));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("obligatoria"));
    }

    [Fact]
    public void Validate_WhenStartDateIsInFuture_FailsValidation()
    {
        // Arrange
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddDays(1));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateMembershipRequest.StartDate));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("futura"));
    }

    [Fact]
    public void Validate_WhenStartDateIsToday_PassesValidation()
    {
        // Arrange
        var request = new CreateMembershipRequest(DateTime.UtcNow.AddMilliseconds(-100));

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

using Abuvi.API.Features.Memberships;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class BulkActivateMembershipValidatorTests
{
    private readonly BulkActivateMembershipValidator _validator;

    public BulkActivateMembershipValidatorTests()
    {
        _validator = new BulkActivateMembershipValidator();
    }

    [Fact]
    public void Validate_WhenYearIsCurrentYear_PassesValidation()
    {
        // Arrange
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenYearIsPast_PassesValidation()
    {
        // Arrange
        var request = new BulkActivateMembershipRequest(2001);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenYearIs2000_FailsValidation()
    {
        // Arrange
        var request = new BulkActivateMembershipRequest(2000);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BulkActivateMembershipRequest.Year));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("válido"));
    }

    [Fact]
    public void Validate_WhenYearIsFuture_FailsValidation()
    {
        // Arrange
        var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year + 1);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BulkActivateMembershipRequest.Year));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("futuro"));
    }
}

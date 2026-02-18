using Abuvi.API.Features.Memberships;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Memberships;

public class PayFeeValidatorTests
{
    private readonly PayFeeValidator _validator;

    public PayFeeValidatorTests()
    {
        _validator = new PayFeeValidator();
    }

    [Fact]
    public void Validate_WhenPaidDateIsValid_PassesValidation()
    {
        // Arrange
        var request = new PayFeeRequest(DateTime.UtcNow.AddMilliseconds(-100), "REF-123");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenPaidDateIsDefault_FailsValidation()
    {
        // Arrange
        var request = new PayFeeRequest(default, "REF-123");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(PayFeeRequest.PaidDate));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("obligatoria"));
    }

    [Fact]
    public void Validate_WhenPaidDateIsInFuture_FailsValidation()
    {
        // Arrange
        var request = new PayFeeRequest(DateTime.UtcNow.AddDays(1), "REF-123");

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(PayFeeRequest.PaidDate));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("futura"));
    }

    [Fact]
    public void Validate_WhenPaymentReferenceIsTooLong_FailsValidation()
    {
        // Arrange
        var longReference = new string('X', 101);
        var request = new PayFeeRequest(DateTime.UtcNow.AddMilliseconds(-100), longReference);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(PayFeeRequest.PaymentReference));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("100 caracteres"));
    }

    [Fact]
    public void Validate_WhenPaymentReferenceIsNull_PassesValidation()
    {
        // Arrange
        var request = new PayFeeRequest(DateTime.UtcNow.AddMilliseconds(-100), null);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

using Abuvi.API.Features.Payments;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Abuvi.Tests.Unit.Features.Payments;

public class RejectPaymentRequestValidatorTests
{
    private readonly RejectPaymentRequestValidator _validator = new();

    [Fact]
    public void Notes_Empty_Fails()
    {
        var result = _validator.TestValidate(new RejectPaymentRequest(""));
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Notes_TooShort_Fails()
    {
        var result = _validator.TestValidate(new RejectPaymentRequest("short"));
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Notes_ValidLength_Passes()
    {
        var result = _validator.TestValidate(new RejectPaymentRequest("This is a valid rejection reason"));
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }
}

public class PaymentSettingsRequestValidatorTests
{
    private readonly PaymentSettingsRequestValidator _validator = new();

    [Theory]
    [InlineData("INVALID")]
    [InlineData("ES12345")]
    [InlineData("DE1234567890123456789012")]
    public void Iban_InvalidFormat_Fails(string iban)
    {
        var request = CreateValidRequest() with { Iban = iban };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Iban);
    }

    [Fact]
    public void Iban_ValidSpanish_Passes()
    {
        var request = CreateValidRequest() with { Iban = "ES1234567890123456789012" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Iban);
    }

    [Fact]
    public void BankName_Empty_Fails()
    {
        var request = CreateValidRequest() with { BankName = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.BankName);
    }

    [Fact]
    public void ValidRequest_Passes()
    {
        var result = _validator.TestValidate(CreateValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("camp lower")]
    [InlineData("HAS SPACE")]
    [InlineData("SPECIAL@CHAR")]
    public void TransferConceptPrefix_InvalidChars_Fails(string prefix)
    {
        var request = CreateValidRequest() with { TransferConceptPrefix = prefix };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.TransferConceptPrefix);
    }

    private static PaymentSettingsRequest CreateValidRequest() => new(
        "ES1234567890123456789012",
        "Test Bank",
        "Test Holder",
        "CAMP"
    );
}

public class PaymentFilterRequestValidatorTests
{
    private readonly PaymentFilterRequestValidator _validator = new();

    [Fact]
    public void Page_LessThan1_Fails()
    {
        var result = _validator.TestValidate(new PaymentFilterRequest(Page: 0));
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void PageSize_GreaterThan100_Fails()
    {
        var result = _validator.TestValidate(new PaymentFilterRequest(PageSize: 101));
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void ValidFilter_Passes()
    {
        var result = _validator.TestValidate(new PaymentFilterRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }
}

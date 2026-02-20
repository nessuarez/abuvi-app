using Abuvi.API.Features.Camps;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class CampEditionExtrasValidatorTests
{
    private readonly CreateCampEditionExtraRequestValidator _createValidator = new();
    private readonly UpdateCampEditionExtraRequestValidator _updateValidator = new();

    #region CreateCampEditionExtraRequestValidator

    [Fact]
    public void Create_ValidRequest_PassesValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: "Camp T-Shirt",
            Description: "Official t-shirt",
            Price: 15m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: 100
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenNameIsEmpty_FailsValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: "",
            Description: null,
            Price: 10m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: null
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Create_WhenNameExceeds200Chars_FailsValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: new string('A', 201),
            Description: null,
            Price: 10m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: null
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Create_WhenDescriptionExceeds1000Chars_FailsValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: "Valid Name",
            Description: new string('D', 1001),
            Price: 10m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: null
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }

    [Fact]
    public void Create_WhenPriceIsNegative_FailsValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: "Extra",
            Description: null,
            Price: -1m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: null
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Fact]
    public void Create_WhenPriceIsZero_PassesValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: "Free Extra",
            Description: null,
            Price: 0m,
            PricingType: PricingType.PerFamily,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: true,
            MaxQuantity: null
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenMaxQuantityIsZero_FailsValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: "Extra",
            Description: null,
            Price: 10m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: 0
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxQuantity");
    }

    [Fact]
    public void Create_WhenMaxQuantityIsPositive_PassesValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: "Extra",
            Description: null,
            Price: 10m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: 50
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenMaxQuantityIsNull_PassesValidation()
    {
        var request = new CreateCampEditionExtraRequest(
            Name: "Extra",
            Description: null,
            Price: 10m,
            PricingType: PricingType.PerPerson,
            PricingPeriod: PricingPeriod.OneTime,
            IsRequired: false,
            MaxQuantity: null
        );

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region UpdateCampEditionExtraRequestValidator

    [Fact]
    public void Update_ValidRequest_PassesValidation()
    {
        var request = new UpdateCampEditionExtraRequest(
            Name: "Updated Name",
            Description: "Updated description",
            Price: 20m,
            IsRequired: false,
            IsActive: true,
            MaxQuantity: 50
        );

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WhenNameIsEmpty_FailsValidation()
    {
        var request = new UpdateCampEditionExtraRequest(
            Name: "",
            Description: null,
            Price: 10m,
            IsRequired: false,
            IsActive: true,
            MaxQuantity: null
        );

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Update_WhenPriceIsNegative_FailsValidation()
    {
        var request = new UpdateCampEditionExtraRequest(
            Name: "Name",
            Description: null,
            Price: -5m,
            IsRequired: false,
            IsActive: true,
            MaxQuantity: null
        );

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Fact]
    public void Update_WhenMaxQuantityIsNullAndIsActiveIsTrue_PassesValidation()
    {
        var request = new UpdateCampEditionExtraRequest(
            Name: "Name",
            Description: null,
            Price: 10m,
            IsRequired: false,
            IsActive: true,
            MaxQuantity: null
        );

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}

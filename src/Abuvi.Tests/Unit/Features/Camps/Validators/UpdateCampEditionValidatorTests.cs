using Abuvi.API.Features.Camps;
using FluentValidation.TestHelper;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps.Validators;

/// <summary>
/// Unit tests for UpdateCampEditionRequestValidator
/// </summary>
public class UpdateCampEditionValidatorTests
{
#pragma warning disable CS8604 // Possible null reference argument.

    private readonly UpdateCampEditionRequestValidator _validator = new();

    private static UpdateCampEditionRequest ValidRequest() => new(
        StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
        PricePerAdult: 180m,
        PricePerChild: 120m,
        PricePerBaby: 60m,
        UseCustomAgeRanges: false,
        CustomBabyMaxAge: null,
        CustomChildMinAge: null,
        CustomChildMaxAge: null,
        CustomAdultMinAge: null,
        MaxCapacity: 100,
        Notes: "Some notes"
    );

    [Fact]
    public void UpdateCampEditionRequestValidator_WithValidData_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WithDefaultStartDate_FailsValidation()
    {
        var request = ValidRequest() with { StartDate = default };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WithDefaultEndDate_FailsValidation()
    {
        var request = ValidRequest() with { EndDate = default };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WithEndDateBeforeStartDate_FailsValidation()
    {
        var request = ValidRequest() with
        {
            StartDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Theory]
    [InlineData(-1)]
    public void UpdateCampEditionRequestValidator_WithNegativePricePerAdult_FailsValidation(decimal price)
    {
        var request = ValidRequest() with { PricePerAdult = price };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PricePerAdult);
    }

    [Theory]
    [InlineData(-1)]
    public void UpdateCampEditionRequestValidator_WithNegativePricePerChild_FailsValidation(decimal price)
    {
        var request = ValidRequest() with { PricePerChild = price };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PricePerChild);
    }

    [Theory]
    [InlineData(-1)]
    public void UpdateCampEditionRequestValidator_WithNegativePricePerBaby_FailsValidation(decimal price)
    {
        var request = ValidRequest() with { PricePerBaby = price };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PricePerBaby);
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WithZeroMaxCapacity_FailsValidation()
    {
        var request = ValidRequest() with { MaxCapacity = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.MaxCapacity);
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WithNullMaxCapacity_PassesValidation()
    {
        var request = ValidRequest() with { MaxCapacity = null };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxCapacity);
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WithNotesTooLong_FailsValidation()
    {
        var request = ValidRequest() with { Notes = new string('x', 2001) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WithUseCustomAgeRangesTrue_RequiresAllAgeFields()
    {
        var request = ValidRequest() with
        {
            UseCustomAgeRanges = true,
            CustomBabyMaxAge = null,
            CustomChildMinAge = null,
            CustomChildMaxAge = null,
            CustomAdultMinAge = null
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomBabyMaxAge);
        result.ShouldHaveValidationErrorFor(x => x.CustomChildMinAge);
        result.ShouldHaveValidationErrorFor(x => x.CustomChildMaxAge);
        result.ShouldHaveValidationErrorFor(x => x.CustomAdultMinAge);
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WithValidCustomAgeRanges_PassesValidation()
    {
        var request = ValidRequest() with
        {
            UseCustomAgeRanges = true,
            CustomBabyMaxAge = 3,
            CustomChildMinAge = 4,
            CustomChildMaxAge = 12,
            CustomAdultMinAge = 13
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WhenBabyMaxAgeNotLessThanChildMinAge_FailsValidation()
    {
        var request = ValidRequest() with
        {
            UseCustomAgeRanges = true,
            CustomBabyMaxAge = 5,
            CustomChildMinAge = 5, // Equal — not less than
            CustomChildMaxAge = 12,
            CustomAdultMinAge = 13
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("CustomBabyMaxAge");
    }

    [Fact]
    public void UpdateCampEditionRequestValidator_WhenChildMaxAgeNotLessThanAdultMinAge_FailsValidation()
    {
        var request = ValidRequest() with
        {
            UseCustomAgeRanges = true,
            CustomBabyMaxAge = 3,
            CustomChildMinAge = 4,
            CustomChildMaxAge = 13,
            CustomAdultMinAge = 13 // Equal — not less than
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("CustomChildMaxAge");
    }

#pragma warning restore CS8604
}

using Abuvi.API.Features.Camps;
using FluentValidation.TestHelper;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps.Validators;

/// <summary>
/// Unit tests for ChangeEditionStatusRequestValidator
/// </summary>
public class ChangeEditionStatusValidatorTests
{
    private readonly ChangeEditionStatusRequestValidator _validator = new();

    [Theory]
    [InlineData(CampEditionStatus.Proposed)]
    [InlineData(CampEditionStatus.Draft)]
    [InlineData(CampEditionStatus.Open)]
    [InlineData(CampEditionStatus.Closed)]
    [InlineData(CampEditionStatus.Completed)]
    public void ChangeEditionStatusRequestValidator_WithValidStatus_PassesValidation(CampEditionStatus status)
    {
        var request = new ChangeEditionStatusRequest(status);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ChangeEditionStatusRequestValidator_WithInvalidStatusValue_FailsValidation()
    {
        var request = new ChangeEditionStatusRequest((CampEditionStatus)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}

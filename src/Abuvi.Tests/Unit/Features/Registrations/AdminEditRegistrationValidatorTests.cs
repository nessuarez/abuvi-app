using Abuvi.API.Features.Registrations;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Abuvi.Tests.Unit.Features.Registrations;

public class AdminEditRegistrationValidatorTests
{
    private readonly AdminEditRegistrationValidator _validator = new();

    [Fact]
    public void AllFieldsNull_ShouldPass()
    {
        var request = new AdminEditRegistrationRequest(null, null, null, null, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Notes_WhenExceeds1000Chars_ShouldFail()
    {
        var request = new AdminEditRegistrationRequest(null, null, null, new string('a', 1001), null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void SpecialNeeds_WhenExceeds2000Chars_ShouldFail()
    {
        var request = new AdminEditRegistrationRequest(null, null, null, null, new string('a', 2001), null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SpecialNeeds);
    }

    [Fact]
    public void CampatesPreference_WhenExceeds500Chars_ShouldFail()
    {
        var request = new AdminEditRegistrationRequest(null, null, null, null, null, new string('a', 501), null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CampatesPreference);
    }

    [Fact]
    public void Members_WhenProvidedEmpty_ShouldFail()
    {
        var request = new AdminEditRegistrationRequest([], null, null, null, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Members);
    }

    [Fact]
    public void Members_WhenValidSingle_ShouldPass()
    {
        var members = new List<MemberAttendanceRequest>
        {
            new(Guid.NewGuid(), AttendancePeriod.Complete)
        };
        var request = new AdminEditRegistrationRequest(members, null, null, null, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Extras_WhenQuantityIsZero_ShouldFail()
    {
        var extras = new List<ExtraSelectionRequest>
        {
            new(Guid.NewGuid(), 0)
        };
        var request = new AdminEditRegistrationRequest(null, extras, null, null, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Extras_WhenValid_ShouldPass()
    {
        var extras = new List<ExtraSelectionRequest>
        {
            new(Guid.NewGuid(), 2)
        };
        var request = new AdminEditRegistrationRequest(null, extras, null, null, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Preferences_WhenPreferenceOrderIsZero_ShouldFail()
    {
        var prefs = new List<AccommodationPreferenceRequest>
        {
            new(Guid.NewGuid(), 0)
        };
        var request = new AdminEditRegistrationRequest(null, null, prefs, null, null, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }
}

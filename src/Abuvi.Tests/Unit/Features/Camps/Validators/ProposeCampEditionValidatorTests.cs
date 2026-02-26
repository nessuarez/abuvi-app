using Abuvi.API.Features.Camps;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps.Validators;

/// <summary>
/// Unit tests for ProposeCampEditionRequestValidator — focused on optional ProposalReason and ProposalNotes fields.
/// </summary>
public class ProposeCampEditionValidatorTests
{
    private readonly ProposeCampEditionRequestValidator _validator = new();

    /// <summary>
    /// Builds a minimal valid ProposeCampEditionRequest with all required fields populated.
    /// New optional fields default to null.
    /// </summary>
    private static ProposeCampEditionRequest BuildMinimalValidRequest() => new(
        CampId: Guid.NewGuid(),
        Year: 2026,
        StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
        PricePerAdult: null,
        PricePerChild: null,
        PricePerBaby: null,
        UseCustomAgeRanges: false,
        CustomBabyMaxAge: null,
        CustomChildMinAge: null,
        CustomChildMaxAge: null,
        CustomAdultMinAge: null,
        MaxCapacity: null,
        Notes: null
    );

    [Fact]
    public void Validate_WithNullProposalReason_IsValid()
    {
        var request = BuildMinimalValidRequest() with { ProposalReason = null };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyProposalReason_IsValid()
    {
        var request = BuildMinimalValidRequest() with { ProposalReason = "" };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithProposalReasonExceeding1000Chars_ReturnsError()
    {
        var request = BuildMinimalValidRequest() with { ProposalReason = new string('x', 1001) };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "ProposalReason");
    }

    [Fact]
    public void Validate_WithProposalNotesExceeding2000Chars_ReturnsError()
    {
        var request = BuildMinimalValidRequest() with { ProposalNotes = new string('x', 2001) };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "ProposalNotes");
    }

    [Fact]
    public void Validate_WithProposalReasonAtMaxLength_IsValid()
    {
        var request = BuildMinimalValidRequest() with { ProposalReason = new string('x', 1000) };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithProposalNotesAtMaxLength_IsValid()
    {
        var request = BuildMinimalValidRequest() with { ProposalNotes = new string('x', 2000) };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithBothProposalFieldsNull_IsValid()
    {
        var request = BuildMinimalValidRequest() with { ProposalReason = null, ProposalNotes = null };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}

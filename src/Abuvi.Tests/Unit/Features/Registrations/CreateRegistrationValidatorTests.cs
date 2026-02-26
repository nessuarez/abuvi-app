using Abuvi.API.Features.Camps;
using Abuvi.API.Features.Registrations;
using FluentAssertions;
using FluentValidation.TestHelper;
using NSubstitute;

namespace Abuvi.Tests.Unit.Features.Registrations;

public class CreateRegistrationValidatorTests
{
    private readonly ICampEditionsRepository _editionsRepo;
    private readonly CreateRegistrationValidator _sut;

    public CreateRegistrationValidatorTests()
    {
        _editionsRepo = Substitute.For<ICampEditionsRepository>();
        _sut = new CreateRegistrationValidator(_editionsRepo);
    }

    [Fact]
    public async Task Validate_WhenCampEditionIsOpen_ShouldPass()
    {
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2025,
            StartDate = DateTime.UtcNow.AddMonths(1),
            EndDate = DateTime.UtcNow.AddMonths(1).AddDays(7),
            PricePerAdult = 500m,
            PricePerChild = 300m,
            PricePerBaby = 100m,
            Status = CampEditionStatus.Open
        };
        _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>()).Returns(edition);

        var request = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: Guid.NewGuid(),
            Members: [new MemberAttendanceRequest(Guid.NewGuid(), AttendancePeriod.Complete)],
            Notes: null);

        var result = await _sut.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenCampEditionClosed_ShouldFailEditionRule()
    {
        var editionId = Guid.NewGuid();
        var edition = new CampEdition
        {
            Id = editionId,
            CampId = Guid.NewGuid(),
            Year = 2025,
            StartDate = DateTime.UtcNow.AddMonths(1),
            EndDate = DateTime.UtcNow.AddMonths(1).AddDays(7),
            PricePerAdult = 500m,
            PricePerChild = 300m,
            PricePerBaby = 100m,
            Status = CampEditionStatus.Closed
        };
        _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>()).Returns(edition);

        var request = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: Guid.NewGuid(),
            Members: [new MemberAttendanceRequest(Guid.NewGuid(), AttendancePeriod.Complete)],
            Notes: null);

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.CampEditionId)
            .WithErrorMessage("La edición del campamento no está abierta para inscripción");
    }

    [Fact]
    public async Task Validate_WhenMemberIdsEmpty_ShouldFail()
    {
        var editionId = Guid.NewGuid();
        _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(new CampEdition
            {
                Id = editionId, CampId = Guid.NewGuid(), Year = 2025,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(7),
                PricePerAdult = 500m, PricePerChild = 300m, PricePerBaby = 100m,
                Status = CampEditionStatus.Open
            });

        var request = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: Guid.NewGuid(),
            Members: [],
            Notes: null);

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Members)
            .WithErrorMessage("Debe seleccionar al menos un miembro de la familia");
    }

    [Fact]
    public async Task Validate_WhenDuplicateMemberIds_ShouldFail()
    {
        var editionId = Guid.NewGuid();
        _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(new CampEdition
            {
                Id = editionId, CampId = Guid.NewGuid(), Year = 2025,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(7),
                PricePerAdult = 500m, PricePerChild = 300m, PricePerBaby = 100m,
                Status = CampEditionStatus.Open
            });

        var memberId = Guid.NewGuid();
        var request = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: Guid.NewGuid(),
            Members: [new MemberAttendanceRequest(memberId, AttendancePeriod.Complete), new MemberAttendanceRequest(memberId, AttendancePeriod.Complete)],
            Notes: null);

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Members)
            .WithErrorMessage("No se puede incluir el mismo miembro dos veces");
    }

    [Fact]
    public async Task Validate_WhenNotesExceedsMaxLength_ShouldFail()
    {
        var editionId = Guid.NewGuid();
        _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(new CampEdition
            {
                Id = editionId, CampId = Guid.NewGuid(), Year = 2025,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(7),
                PricePerAdult = 500m, PricePerChild = 300m, PricePerBaby = 100m,
                Status = CampEditionStatus.Open
            });

        var request = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: Guid.NewGuid(),
            Members: [new MemberAttendanceRequest(Guid.NewGuid(), AttendancePeriod.Complete)],
            Notes: new string('A', 1001));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage("Las notas no pueden superar los 1000 caracteres");
    }

    [Fact]
    public async Task Validate_WhenCampEditionIdEmpty_ShouldFail()
    {
        _editionsRepo.GetByIdAsync(Guid.Empty, Arg.Any<CancellationToken>()).Returns((CampEdition?)null);

        var request = new CreateRegistrationRequest(
            CampEditionId: Guid.Empty,
            FamilyUnitId: Guid.NewGuid(),
            Members: [new MemberAttendanceRequest(Guid.NewGuid(), AttendancePeriod.Complete)],
            Notes: null);

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.CampEditionId);
    }

    [Fact]
    public async Task Validate_WhenFamilyUnitIdEmpty_ShouldFail()
    {
        var editionId = Guid.NewGuid();
        _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(new CampEdition
            {
                Id = editionId, CampId = Guid.NewGuid(), Year = 2025,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(7),
                PricePerAdult = 500m, PricePerChild = 300m, PricePerBaby = 100m,
                Status = CampEditionStatus.Open
            });

        var request = new CreateRegistrationRequest(
            CampEditionId: editionId,
            FamilyUnitId: Guid.Empty,
            Members: [new MemberAttendanceRequest(Guid.NewGuid(), AttendancePeriod.Complete)],
            Notes: null);

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.FamilyUnitId)
            .WithErrorMessage("La unidad familiar es obligatoria");
    }
}

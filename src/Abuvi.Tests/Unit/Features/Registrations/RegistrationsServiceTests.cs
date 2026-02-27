using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.Registrations;

public class RegistrationsServiceTests
{
    private readonly IRegistrationsRepository _repo;
    private readonly IRegistrationExtrasRepository _extrasRepo;
    private readonly IFamilyUnitsRepository _familyUnitsRepo;
    private readonly ICampEditionsRepository _editionsRepo;
    private readonly IAssociationSettingsRepository _settingsRepo;
    private readonly ILogger<RegistrationsService> _logger;
    private readonly RegistrationPricingService _pricingService;
    private readonly RegistrationsService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FamilyUnitId = Guid.NewGuid();
    private static readonly Guid CampEditionId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    public RegistrationsServiceTests()
    {
        _repo = Substitute.For<IRegistrationsRepository>();
        _extrasRepo = Substitute.For<IRegistrationExtrasRepository>();
        _familyUnitsRepo = Substitute.For<IFamilyUnitsRepository>();
        _editionsRepo = Substitute.For<ICampEditionsRepository>();
        _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        _logger = Substitute.For<ILogger<RegistrationsService>>();
        _pricingService = new RegistrationPricingService(_settingsRepo);
        _sut = new RegistrationsService(
            _repo, _extrasRepo, _familyUnitsRepo, _editionsRepo, _pricingService, _logger);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenEditionOpen_CreatesRegistrationWithCorrectPricing()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var member = CreateFamilyMember(MemberId, FamilyUnitId, dateOfBirth: new DateOnly(2000, 1, 1));

        SetupGlobalAgeRanges();
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.ExistsAsync(FamilyUnitId, CampEditionId, Arg.Any<CancellationToken>()).Returns(false);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);
        _repo.AddAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // GetByIdWithDetailsAsync returns a Registration entity (not a DTO)
        _repo.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildFullRegistration(Guid.NewGuid(), familyUnit, edition, [member], edition.PricePerAdult));

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null);

        // Act
        var result = await _sut.CreateAsync(UserId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FamilyUnit.Id.Should().Be(FamilyUnitId);
        await _repo.Received(1).AddAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenEditionNotOpen_ThrowsBusinessRuleException()
    {
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateEditionWithStatus(CampEditionStatus.Closed);

        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null);

        Func<Task> act = async () => await _sut.CreateAsync(UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*abierta*");
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateRegistration_ThrowsBusinessRuleException()
    {
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();

        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.ExistsAsync(FamilyUnitId, CampEditionId, Arg.Any<CancellationToken>()).Returns(true);

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null);

        Func<Task> act = async () => await _sut.CreateAsync(UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Ya existe*");
    }

    [Fact]
    public async Task CreateAsync_WhenCampFull_ThrowsBusinessRuleException()
    {
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition(maxCapacity: 10);
        var member = CreateFamilyMember(MemberId, FamilyUnitId, dateOfBirth: new DateOnly(2000, 1, 1));

        SetupGlobalAgeRanges();
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.ExistsAsync(FamilyUnitId, CampEditionId, Arg.Any<CancellationToken>()).Returns(false);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);
        _repo.CountConcurrentAttendeesByPeriodAsync(CampEditionId, Arg.Any<AttendancePeriod>(), Arg.Any<CancellationToken>()).Returns(10);

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null);

        Func<Task> act = async () => await _sut.CreateAsync(UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*capacidad máxima*");
    }

    [Fact]
    public async Task CreateAsync_WhenMemberNotInFamilyUnit_ThrowsBusinessRuleException()
    {
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var otherFamilyUnitId = Guid.NewGuid();
        var member = CreateFamilyMember(MemberId, otherFamilyUnitId);

        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.ExistsAsync(FamilyUnitId, CampEditionId, Arg.Any<CancellationToken>()).Returns(false);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);
        SetupGlobalAgeRanges();

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null);

        Func<Task> act = async () => await _sut.CreateAsync(UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*no pertenece*");
    }

    [Fact]
    public async Task CreateAsync_WhenUserNotRepresentative_ThrowsBusinessRuleException()
    {
        var otherUserId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(representativeUserId: otherUserId);

        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null);

        Func<Task> act = async () => await _sut.CreateAsync(UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*permiso*");
    }

    // ── UpdateMembersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMembersAsync_WhenRegistrationPending_RecalculatesPricingCorrectly()
    {
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var member = CreateFamilyMember(MemberId, FamilyUnitId);

        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Pending;

        SetupGlobalAgeRanges();
        _repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);
        _repo.DeleteMembersByRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildFullRegistration(registrationId, familyUnit, edition, [member], edition.PricePerAdult));

        var request = new UpdateRegistrationMembersRequest([new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)]);

        var result = await _sut.UpdateMembersAsync(registrationId, UserId, request, CancellationToken.None);

        result.Should().NotBeNull();
        await _repo.Received(1).UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).DeleteMembersByRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMembersAsync_WhenRegistrationConfirmed_ThrowsBusinessRuleException()
    {
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Confirmed;

        _repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);

        var request = new UpdateRegistrationMembersRequest([new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)]);

        Func<Task> act = async () =>
            await _sut.UpdateMembersAsync(registrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Pendiente*");
    }

    [Fact]
    public async Task UpdateMembersAsync_WhenRegistrationCancelled_ThrowsBusinessRuleException()
    {
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Cancelled;

        _repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);

        var request = new UpdateRegistrationMembersRequest([new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)]);

        Func<Task> act = async () =>
            await _sut.UpdateMembersAsync(registrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Pendiente*");
    }

    // ── SetExtrasAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SetExtrasAsync_WhenExtrasValid_UpdatesExtrasAmountAndTotal()
    {
        var registrationId = Guid.NewGuid();
        var extraId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var extra = CreateCampEditionExtra(extraId, CampEditionId, price: 50m);

        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Pending;

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _editionsRepo.GetExtraByIdAsync(extraId, Arg.Any<CancellationToken>()).Returns(extra);
        _extrasRepo.DeleteByRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _extrasRepo.AddRangeAsync(Arg.Any<IEnumerable<RegistrationExtra>>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Second call to GetByIdWithDetailsAsync returns updated entity
        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);

        var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(extraId, 2)]);

        var result = await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None);

        await _extrasRepo.Received(1).DeleteByRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>());
        await _extrasRepo.Received(1).AddRangeAsync(Arg.Any<IEnumerable<RegistrationExtra>>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetExtrasAsync_WhenExtraNotInEdition_ThrowsBusinessRuleException()
    {
        var registrationId = Guid.NewGuid();
        var extraId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var extra = CreateCampEditionExtra(extraId, Guid.NewGuid(), price: 50m); // different edition

        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Pending;

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _editionsRepo.GetExtraByIdAsync(extraId, Arg.Any<CancellationToken>()).Returns(extra);

        var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(extraId, 1)]);

        Func<Task> act = async () =>
            await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*no pertenece*");
    }

    [Fact]
    public async Task SetExtrasAsync_WhenQuantityExceedsMax_ThrowsBusinessRuleException()
    {
        var registrationId = Guid.NewGuid();
        var extraId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var extra = CreateCampEditionExtra(extraId, CampEditionId, price: 50m, maxQuantity: 2);

        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Pending;

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _editionsRepo.GetExtraByIdAsync(extraId, Arg.Any<CancellationToken>()).Returns(extra);

        var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(extraId, 5)]);

        Func<Task> act = async () =>
            await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*cantidad*");
    }

    [Fact]
    public async Task SetExtrasAsync_WhenRegistrationNotPending_ThrowsBusinessRuleException()
    {
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Confirmed;

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);

        var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(Guid.NewGuid(), 1)]);

        Func<Task> act = async () =>
            await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Pendiente*");
    }

    // ── CancelAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_WhenStatusPending_SetsStatusCancelled()
    {
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Pending;

        _repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _sut.CancelAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        result.Message.Should().NotBeNullOrEmpty();
        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.Status == RegistrationStatus.Cancelled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_WhenStatusConfirmed_SetsStatusCancelled()
    {
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Confirmed;

        _repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _sut.CancelAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        result.Message.Should().NotBeNullOrEmpty();
        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.Status == RegistrationStatus.Cancelled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_WhenStatusAlreadyCancelled_ThrowsBusinessRuleException()
    {
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Cancelled;

        _repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);

        Func<Task> act = async () =>
            await _sut.CancelAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*cancelada*");
    }

    // ── GetAvailableEditionsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableEditionsAsync_ReturnsOnlyOpenEditions()
    {
        var openEdition = CreateOpenEdition();
        _editionsRepo.GetOpenEditionsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CampEdition> { openEdition }.AsReadOnly());
        _repo.CountActiveByEditionAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(5);
        SetupGlobalAgeRanges();

        var result = await _sut.GetAvailableEditionsAsync(CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(CampEditionId);
        result[0].Status.Should().Be(CampEditionStatus.Open.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FamilyUnit CreateFamilyUnit(Guid representativeUserId) => new()
    {
        Id = FamilyUnitId,
        Name = "Test Family",
        RepresentativeUserId = representativeUserId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static CampEdition CreateOpenEdition(int? maxCapacity = null) => new()
    {
        Id = CampEditionId,
        CampId = Guid.NewGuid(),
        Year = 2025,
        StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2025, 7, 14, 0, 0, 0, DateTimeKind.Utc),
        PricePerAdult = 500m,
        PricePerChild = 300m,
        PricePerBaby = 100m,
        Status = CampEditionStatus.Open,
        UseCustomAgeRanges = false,
        MaxCapacity = maxCapacity,
        Camp = new Camp { Id = Guid.NewGuid(), Name = "Test Camp", PricePerAdult = 500m, PricePerChild = 300m, PricePerBaby = 100m }
    };

    private static CampEdition CreateEditionWithStatus(CampEditionStatus status)
    {
        var edition = CreateOpenEdition();
        edition.Status = status;
        return edition;
    }

    private static FamilyMember CreateFamilyMember(Guid id, Guid familyUnitId,
        DateOnly? dateOfBirth = null) => new()
    {
        Id = id,
        FamilyUnitId = familyUnitId,
        FirstName = "Ana",
        LastName = "García",
        DateOfBirth = dateOfBirth ?? new DateOnly(2000, 1, 1),
        Relationship = FamilyRelationship.Parent,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Registration CreateRegistrationWithFamilyUnit(
        Guid id, FamilyUnit familyUnit, CampEdition edition) => new()
    {
        Id = id,
        FamilyUnitId = familyUnit.Id,
        CampEditionId = edition.Id,
        RegisteredByUserId = familyUnit.RepresentativeUserId,
        BaseTotalAmount = 500m,
        ExtrasAmount = 0m,
        TotalAmount = 500m,
        Status = RegistrationStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FamilyUnit = familyUnit,
        CampEdition = edition,
        Members = [],
        Extras = [],
        Payments = []
    };

    /// <summary>Builds a full Registration entity with navigation properties — used for GetByIdWithDetailsAsync mocks.</summary>
    private static Registration BuildFullRegistration(
        Guid id, FamilyUnit familyUnit, CampEdition edition,
        List<FamilyMember> members, decimal pricePerMember) => new()
    {
        Id = id,
        FamilyUnitId = familyUnit.Id,
        CampEditionId = edition.Id,
        RegisteredByUserId = familyUnit.RepresentativeUserId,
        BaseTotalAmount = members.Count * pricePerMember,
        ExtrasAmount = 0m,
        TotalAmount = members.Count * pricePerMember,
        Status = RegistrationStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FamilyUnit = familyUnit,
        CampEdition = edition,
        Members = members.Select(m => new RegistrationMember
        {
            Id = Guid.NewGuid(),
            RegistrationId = id,
            FamilyMemberId = m.Id,
            FamilyMember = m,
            AgeAtCamp = 25,
            AgeCategory = AgeCategory.Adult,
            IndividualAmount = pricePerMember,
            CreatedAt = DateTime.UtcNow
        }).ToList(),
        Extras = [],
        Payments = []
    };

    private static CampEditionExtra CreateCampEditionExtra(
        Guid id, Guid campEditionId, decimal price, int? maxQuantity = null) => new()
    {
        Id = id,
        CampEditionId = campEditionId,
        Name = "Test Extra",
        Price = price,
        PricingType = PricingType.PerPerson,
        PricingPeriod = PricingPeriod.OneTime,
        IsActive = true,
        MaxQuantity = maxQuantity
    };

    private void SetupGlobalAgeRanges()
    {
        var json = "{\"babyMaxAge\":3,\"childMinAge\":4,\"childMaxAge\":17,\"adultMinAge\":18}";
        _settingsRepo.GetByKeyAsync("age_ranges", Arg.Any<CancellationToken>())
            .Returns(new AssociationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = "age_ranges",
                SettingValue = json,
                UpdatedAt = DateTime.UtcNow
            });
    }

    // ── New field mapping tests ───────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithSpecialNeeds_PersistsValue()
    {
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var member = CreateFamilyMember(MemberId, FamilyUnitId, dateOfBirth: new DateOnly(2000, 1, 1));

        SetupGlobalAgeRanges();
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.ExistsAsync(FamilyUnitId, CampEditionId, Arg.Any<CancellationToken>()).Returns(false);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);

        Registration? captured = null;
        _repo.AddAsync(Arg.Do<Registration>(r => captured = r), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildFullRegistration(Guid.NewGuid(), familyUnit, edition, [member], edition.PricePerAdult));

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: "Wheelchair access needed",
            CampatesPreference: null);

        await _sut.CreateAsync(UserId, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.SpecialNeeds.Should().Be("Wheelchair access needed");
    }

    [Fact]
    public async Task CreateAsync_WithCampatesPreference_PersistsValue()
    {
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var member = CreateFamilyMember(MemberId, FamilyUnitId, dateOfBirth: new DateOnly(2000, 1, 1));

        SetupGlobalAgeRanges();
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.ExistsAsync(FamilyUnitId, CampEditionId, Arg.Any<CancellationToken>()).Returns(false);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);

        Registration? captured = null;
        _repo.AddAsync(Arg.Do<Registration>(r => captured = r), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildFullRegistration(Guid.NewGuid(), familyUnit, edition, [member], edition.PricePerAdult));

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: "Near family Garcia");

        await _sut.CreateAsync(UserId, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.CampatesPreference.Should().Be("Near family Garcia");
    }

    [Fact]
    public async Task CreateAsync_WithGuardianInfoOnMember_PersistsGuardianFields()
    {
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var member = CreateFamilyMember(MemberId, FamilyUnitId, dateOfBirth: new DateOnly(2020, 1, 1)); // minor

        SetupGlobalAgeRanges();
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.ExistsAsync(FamilyUnitId, CampEditionId, Arg.Any<CancellationToken>()).Returns(false);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);

        Registration? captured = null;
        _repo.AddAsync(Arg.Do<Registration>(r => captured = r), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildFullRegistration(Guid.NewGuid(), familyUnit, edition, [member], edition.PricePerBaby));

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete,
                GuardianName: "Maria Garcia", GuardianDocumentNumber: "12345678A")],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null);

        await _sut.CreateAsync(UserId, request, CancellationToken.None);

        captured.Should().NotBeNull();
        var capturedMember = captured!.Members.First();
        capturedMember.GuardianName.Should().Be("Maria Garcia");
        capturedMember.GuardianDocumentNumber.Should().Be("12345678A");
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_UsesDefaults()
    {
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var member = CreateFamilyMember(MemberId, FamilyUnitId, dateOfBirth: new DateOnly(2000, 1, 1));

        SetupGlobalAgeRanges();
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.ExistsAsync(FamilyUnitId, CampEditionId, Arg.Any<CancellationToken>()).Returns(false);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);

        Registration? captured = null;
        _repo.AddAsync(Arg.Do<Registration>(r => captured = r), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildFullRegistration(Guid.NewGuid(), familyUnit, edition, [member], edition.PricePerAdult));

        var request = new CreateRegistrationRequest(
            CampEditionId: CampEditionId,
            FamilyUnitId: FamilyUnitId,
            Members: [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)],
            Notes: null,
            SpecialNeeds: null,
            CampatesPreference: null);

        await _sut.CreateAsync(UserId, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.SpecialNeeds.Should().BeNull();
        captured.CampatesPreference.Should().BeNull();
        captured.Members.First().GuardianName.Should().BeNull();
        captured.Members.First().GuardianDocumentNumber.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMembersAsync_WithGuardianInfo_PersistsOnNewMembers()
    {
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit(UserId);
        var edition = CreateOpenEdition();
        var member = CreateFamilyMember(MemberId, FamilyUnitId, dateOfBirth: new DateOnly(2020, 1, 1)); // minor

        var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
        existing.Status = RegistrationStatus.Pending;

        SetupGlobalAgeRanges();
        _repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>()).Returns(familyUnit);
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _familyUnitsRepo.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>()).Returns(member);
        _repo.DeleteMembersByRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        Registration? captured = null;
        _repo.UpdateAsync(Arg.Do<Registration>(r => captured = r), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(BuildFullRegistration(registrationId, familyUnit, edition, [member], edition.PricePerBaby));

        var request = new UpdateRegistrationMembersRequest([
            new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete,
                GuardianName: "Juan López", GuardianDocumentNumber: "87654321B")]);

        await _sut.UpdateMembersAsync(registrationId, UserId, request, CancellationToken.None);

        captured.Should().NotBeNull();
        var capturedMember = captured!.Members.First();
        capturedMember.GuardianName.Should().Be("Juan López");
        capturedMember.GuardianDocumentNumber.Should().Be("87654321B");
    }
}

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.Registrations;

/// <summary>
/// Unit tests for linked (non-representative) family member access to registrations.
/// Covers GetByIdAsync, GetByFamilyUnitAsync, and write-operation authorization boundaries.
/// </summary>
public class RegistrationsServiceTests_LinkedMemberAccess
{
    private readonly IRegistrationsRepository _repo;
    private readonly IFamilyUnitsRepository _familyUnitsRepo;
    private readonly ICampEditionsRepository _editionsRepo;
    private readonly RegistrationsService _sut;

    private static readonly Guid RepresentativeUserId = Guid.NewGuid();
    private static readonly Guid LinkedMemberUserId = Guid.NewGuid();
    private static readonly Guid FamilyUnitId = Guid.NewGuid();
    private static readonly Guid CampEditionId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    public RegistrationsServiceTests_LinkedMemberAccess()
    {
        _repo = Substitute.For<IRegistrationsRepository>();
        _familyUnitsRepo = Substitute.For<IFamilyUnitsRepository>();
        _editionsRepo = Substitute.For<ICampEditionsRepository>();

        var extrasRepo = Substitute.For<IRegistrationExtrasRepository>();
        var accommodationPrefsRepo = Substitute.For<IRegistrationAccommodationPreferencesRepository>();
        var accommodationsRepo = Substitute.For<ICampEditionAccommodationsRepository>();
        var settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        var emailService = Substitute.For<IEmailService>();
        var paymentsService = Substitute.For<IPaymentsService>();
        var membershipsRepo = Substitute.For<IMembershipsRepository>();
        var logger = Substitute.For<ILogger<RegistrationsService>>();
        var pricingService = new RegistrationPricingService(settingsRepo);

        _sut = new RegistrationsService(
            _repo, extrasRepo, accommodationPrefsRepo, _familyUnitsRepo,
            _editionsRepo, accommodationsRepo, pricingService, emailService,
            paymentsService, membershipsRepo, logger);
    }

    // ── GetByIdAsync — linked member access ─────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_LinkedMemberOfSameFamily_ReturnsRegistration()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = BuildRegistration(registrationId);

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // LinkedMemberUserId is not the representative, so the fallback lookup fires
        var familyUnit = CreateFamilyUnit();
        _familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(LinkedMemberUserId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.GetByIdAsync(
            registrationId, LinkedMemberUserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(registrationId);
    }

    [Fact]
    public async Task GetByIdAsync_LinkedMemberOfDifferentFamily_ThrowsBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = BuildRegistration(registrationId);

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // User belongs to a different family unit
        var differentFamilyUnit = new FamilyUnit
        {
            Id = Guid.NewGuid(), // different ID
            Name = "Other Family",
            RepresentativeUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(LinkedMemberUserId, Arg.Any<CancellationToken>())
            .Returns(differentFamilyUnit);

        // Act
        var act = async () => await _sut.GetByIdAsync(
            registrationId, LinkedMemberUserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*permiso*ver*");
    }

    [Fact]
    public async Task GetByIdAsync_UnlinkedUser_ThrowsBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = BuildRegistration(registrationId);
        var unlinkedUserId = Guid.NewGuid();

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // User is not a member of any family
        _familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(unlinkedUserId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.GetByIdAsync(
            registrationId, unlinkedUserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*permiso*ver*");
    }

    // ── GetByFamilyUnitAsync — linked member access ─────────────────────────

    [Fact]
    public async Task GetByFamilyUnitAsync_LinkedMember_ReturnsRegistrations()
    {
        // Arrange — user is not representative, but is a linked member
        _familyUnitsRepo.GetFamilyUnitByRepresentativeIdAsync(LinkedMemberUserId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var familyUnit = CreateFamilyUnit();
        _familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(LinkedMemberUserId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        var registration = BuildRegistration(Guid.NewGuid());
        _repo.GetByFamilyUnitAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns([registration]);

        // Act
        var result = await _sut.GetByFamilyUnitAsync(LinkedMemberUserId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].FamilyUnit.Id.Should().Be(FamilyUnitId);
    }

    [Fact]
    public async Task GetByFamilyUnitAsync_UnlinkedUser_ReturnsEmptyList()
    {
        // Arrange — user is neither representative nor linked member
        var unknownUserId = Guid.NewGuid();

        _familyUnitsRepo.GetFamilyUnitByRepresentativeIdAsync(unknownUserId, Arg.Any<CancellationToken>())
            .ReturnsNull();
        _familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(unknownUserId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await _sut.GetByFamilyUnitAsync(unknownUserId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    // ── Write operations — linked member must be blocked ────────────────────

    [Fact]
    public async Task CancelAsync_LinkedMemberNotRepresentative_ThrowsBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit();
        var registration = new Registration
        {
            Id = registrationId,
            FamilyUnitId = FamilyUnitId,
            CampEditionId = CampEditionId,
            RegisteredByUserId = RepresentativeUserId,
            Status = RegistrationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FamilyUnit = familyUnit,
            Members = [],
            Extras = [],
            Payments = []
        };

        _repo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act — linked member (not representative) tries to cancel
        var act = async () => await _sut.CancelAsync(
            registrationId, LinkedMemberUserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*permiso*cancelar*");

        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMembersAsync_LinkedMemberNotRepresentative_ThrowsBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var familyUnit = CreateFamilyUnit();
        var registration = BuildRegistration(registrationId);
        registration.Status = RegistrationStatus.Pending;

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _familyUnitsRepo.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        var request = new UpdateRegistrationMembersRequest(
            [new MemberAttendanceRequest(MemberId, AttendancePeriod.Complete)]);

        // Act — linked member tries to update members
        var act = async () => await _sut.UpdateMembersAsync(
            registrationId, LinkedMemberUserId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*permiso*modificar*");
    }

    [Fact]
    public async Task SetExtrasAsync_LinkedMemberNotRepresentative_ThrowsBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = BuildRegistration(registrationId);
        registration.Status = RegistrationStatus.Pending;

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        var request = new UpdateRegistrationExtrasRequest(
            [new ExtraSelectionRequest(Guid.NewGuid(), 1)]);

        // Act — linked member tries to set extras
        var act = async () => await _sut.SetExtrasAsync(
            registrationId, LinkedMemberUserId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*permiso*modificar*");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static FamilyUnit CreateFamilyUnit() => new()
    {
        Id = FamilyUnitId,
        Name = "Test Family",
        RepresentativeUserId = RepresentativeUserId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Registration BuildRegistration(Guid id) => new()
    {
        Id = id,
        FamilyUnitId = FamilyUnitId,
        CampEditionId = CampEditionId,
        RegisteredByUserId = RepresentativeUserId,
        BaseTotalAmount = 500m,
        ExtrasAmount = 0m,
        TotalAmount = 500m,
        Status = RegistrationStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FamilyUnit = CreateFamilyUnit(),
        CampEdition = new CampEdition
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
            Camp = new Camp
            {
                Id = Guid.NewGuid(),
                Name = "Test Camp",
                Location = "Test Location",
                PricePerAdult = 500m,
                PricePerChild = 300m,
                PricePerBaby = 100m
            }
        },
        RegisteredByUser = new User
        {
            Id = RepresentativeUserId,
            Email = "representative@example.com",
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            EmailVerified = true
        },
        Members = [
            new RegistrationMember
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = MemberId,
                FamilyMember = new FamilyMember
                {
                    Id = MemberId,
                    FamilyUnitId = FamilyUnitId,
                    FirstName = "Ana",
                    LastName = "García",
                    DateOfBirth = new DateOnly(2000, 1, 1),
                    Relationship = FamilyRelationship.Parent,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                AgeAtCamp = 25,
                AgeCategory = AgeCategory.Adult,
                IndividualAmount = 500m,
                AttendancePeriod = AttendancePeriod.Complete,
                CreatedAt = DateTime.UtcNow
            }
        ],
        Extras = [],
        Payments = []
    };
}

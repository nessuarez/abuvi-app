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

namespace Abuvi.Tests.Unit.Features.Registrations;

public class AdminRegistrationServiceTests
{
    private readonly IRegistrationsRepository _repo;
    private readonly IRegistrationExtrasRepository _extrasRepo;
    private readonly IRegistrationAccommodationPreferencesRepository _accommodationPrefsRepo;
    private readonly IFamilyUnitsRepository _familyUnitsRepo;
    private readonly ICampEditionsRepository _editionsRepo;
    private readonly ICampEditionAccommodationsRepository _accommodationsRepo;
    private readonly IAssociationSettingsRepository _settingsRepo;
    private readonly IEmailService _emailService;
    private readonly IPaymentsService _paymentsService;
    private readonly ILogger<RegistrationsService> _logger;
    private readonly RegistrationPricingService _pricingService;
    private readonly RegistrationsService _sut;

    private static readonly Guid CampEditionId = Guid.NewGuid();
    private static readonly Guid RegistrationId = Guid.NewGuid();
    private static readonly Guid FamilyUnitId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public AdminRegistrationServiceTests()
    {
        _repo = Substitute.For<IRegistrationsRepository>();
        _extrasRepo = Substitute.For<IRegistrationExtrasRepository>();
        _accommodationPrefsRepo = Substitute.For<IRegistrationAccommodationPreferencesRepository>();
        _familyUnitsRepo = Substitute.For<IFamilyUnitsRepository>();
        _editionsRepo = Substitute.For<ICampEditionsRepository>();
        _accommodationsRepo = Substitute.For<ICampEditionAccommodationsRepository>();
        _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        _emailService = Substitute.For<IEmailService>();
        _paymentsService = Substitute.For<IPaymentsService>();
        _logger = Substitute.For<ILogger<RegistrationsService>>();
        _pricingService = new RegistrationPricingService(_settingsRepo);
        var membershipsRepo = Substitute.For<IMembershipsRepository>();
        membershipsRepo.HasPaidCurrentYearFeeForFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var extrasDefinitionRepo = Substitute.For<ICampEditionExtrasRepository>();
        var accommodationNeedsRepo = Substitute.For<IRegistrationAccommodationNeedsRepository>();
        var friendLinksRepo = Substitute.For<IRegistrationFriendLinksRepository>();
        var accommodationFeaturesRepo = Substitute.For<IAccommodationFeaturesRepository>();
        _sut = new RegistrationsService(
            _repo, _extrasRepo, _accommodationPrefsRepo, _familyUnitsRepo,
            _editionsRepo, _accommodationsRepo, extrasDefinitionRepo, _pricingService, _emailService,
            _paymentsService, membershipsRepo, accommodationNeedsRepo, friendLinksRepo, accommodationFeaturesRepo, _logger);
    }

    // ── GetAdminListAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminListAsync_WhenEditionExists_ReturnsPaginatedList()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);

        var projections = new List<AdminRegistrationProjection>
        {
            new(Guid.NewGuid(), FamilyUnitId, "García Family", UserId,
                "Juan", "García", "juan@test.com", RegistrationStatus.Pending,
                3, 900m, 200m, DateTime.UtcNow,
                [AttendancePeriod.Complete], [])
        };
        var totals = new AdminRegistrationTotals(1, 3, 900m, 200m, 700m);

        _repo.GetAdminPagedAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
                Arg.Any<AdminRegistrationSortBy>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((projections, 1, totals));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Totals.TotalRegistrations.Should().Be(1);
        result.Totals.TotalMembers.Should().Be(3);
        result.Items[0].FamilyUnit.Name.Should().Be("García Family");
        result.Items[0].FamilyUnit.RepresentativeUserId.Should().Be(UserId);
        result.Items[0].Representative.Email.Should().Be("juan@test.com");
        result.Items[0].AmountRemaining.Should().Be(700m);
    }

    [Fact]
    public async Task GetAdminListAsync_WhenEditionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>())
            .Returns((CampEdition?)null);

        // Act
        var act = () => _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAdminListAsync_ClampsPageAndPageSize()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.GetAdminPagedAsync(CampEditionId, 1, 100, null, null, null, null, null, null,
                Arg.Any<AdminRegistrationSortBy>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, -5, 500, null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, CancellationToken.None);

        // Assert
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetAdminListAsync_WhenNoRegistrations_ReturnsEmptyList()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.GetAdminPagedAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
                Arg.Any<AdminRegistrationSortBy>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    // ── AdminUpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AdminUpdateAsync_WhenUpdatingNotes_SetsStatusToDraftAndUpdatesNotes()
    {
        // Arrange
        var registration = BuildRegistration(RegistrationStatus.Confirmed);
        var edition = CreateEdition();

        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _editionsRepo.GetByIdAsync(registration.CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // For the reload after update
        var updatedRegistration = BuildRegistration(RegistrationStatus.Draft);
        updatedRegistration.Notes = "Updated notes";
        updatedRegistration.AdminModifiedAt = DateTime.UtcNow;
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, updatedRegistration);

        var request = new AdminEditRegistrationRequest(
            Members: null, Extras: null, Preferences: null,
            Notes: "Updated notes", SpecialNeeds: null, CampatesPreference: null, HasPet: null);

        // Act
        var result = await _sut.AdminUpdateAsync(RegistrationId, UserId, request, CancellationToken.None);

        // Assert
        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.Status == RegistrationStatus.Draft && r.AdminModifiedAt != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminUpdateAsync_WhenRegistrationCancelled_ThrowsBusinessRuleException()
    {
        // Arrange
        var registration = BuildRegistration(RegistrationStatus.Cancelled);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);

        var request = new AdminEditRegistrationRequest(
            Members: null, Extras: null, Preferences: null,
            Notes: "test", SpecialNeeds: null, CampatesPreference: null, HasPet: null);

        // Act
        var act = () => _sut.AdminUpdateAsync(RegistrationId, UserId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*cancelada*");
    }

    [Fact]
    public async Task AdminUpdateAsync_WhenRegistrationNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns((Registration?)null);

        var request = new AdminEditRegistrationRequest(
            Members: null, Extras: null, Preferences: null,
            Notes: "test", SpecialNeeds: null, CampatesPreference: null, HasPet: null);

        // Act
        var act = () => _sut.AdminUpdateAsync(RegistrationId, UserId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(RegistrationStatus.Pending)]
    [InlineData(RegistrationStatus.Confirmed)]
    [InlineData(RegistrationStatus.Draft)]
    public async Task AdminUpdateAsync_AllowsEditForNonCancelledStatuses(RegistrationStatus sourceStatus)
    {
        // Arrange
        var registration = BuildRegistration(sourceStatus);
        var edition = CreateEdition();

        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _editionsRepo.GetByIdAsync(registration.CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var updatedRegistration = BuildRegistration(RegistrationStatus.Draft);
        updatedRegistration.AdminModifiedAt = DateTime.UtcNow;
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, updatedRegistration);

        var request = new AdminEditRegistrationRequest(
            Members: null, Extras: null, Preferences: null,
            Notes: "admin edit", SpecialNeeds: null, CampatesPreference: null, HasPet: null);

        // Act
        var act = () => _sut.AdminUpdateAsync(RegistrationId, UserId, request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AdminUpdateAsync_NotifyUser_True_EmailSuccess_SetsFamilyNotifiedTrue()
    {
        // Arrange
        var registration = BuildRegistration(RegistrationStatus.Confirmed);
        var edition = CreateEdition();

        Registration? firstCaptured = null;
        Registration? secondCaptured = null;
        var captureCount = 0;
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.Draft));
        _editionsRepo.GetByIdAsync(registration.CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.UpdateAsync(Arg.Do<Registration>(r =>
        {
            captureCount++;
            if (captureCount == 1) firstCaptured = r;
            else secondCaptured = r;
        }), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _emailService.SendDraftChangesNotificationAsync(Arg.Any<DraftChangesEmailData>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new AdminEditRegistrationRequest(
            Members: null, Extras: null, Preferences: null,
            Notes: "Test note", SpecialNeeds: null, CampatesPreference: null, HasPet: null,
            NotifyUser: true);

        // Act
        await _sut.AdminUpdateAsync(RegistrationId, UserId, request, CancellationToken.None);

        // Assert: two UpdateAsync calls; second sets FamilyNotifiedOfDraft = true
        captureCount.Should().Be(2);
        firstCaptured!.FamilyNotifiedOfDraft.Should().BeFalse();
        secondCaptured!.FamilyNotifiedOfDraft.Should().BeTrue();
    }

    [Fact]
    public async Task AdminUpdateAsync_NotifyUser_True_EmailFailure_FamilyNotifiedRemainsfalse()
    {
        // Arrange
        var registration = BuildRegistration(RegistrationStatus.Confirmed);
        var edition = CreateEdition();

        Registration? captured = null;
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.Draft));
        _editionsRepo.GetByIdAsync(registration.CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.UpdateAsync(Arg.Do<Registration>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _emailService.SendDraftChangesNotificationAsync(Arg.Any<DraftChangesEmailData>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP error")));

        var request = new AdminEditRegistrationRequest(
            Members: null, Extras: null, Preferences: null,
            Notes: null, SpecialNeeds: null, CampatesPreference: null, HasPet: null,
            NotifyUser: true);

        // Act (should not throw despite email failure)
        await _sut.AdminUpdateAsync(RegistrationId, UserId, request, CancellationToken.None);

        // Assert: only one UpdateAsync, FamilyNotifiedOfDraft = false
        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => !r.FamilyNotifiedOfDraft),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminUpdateAsync_NotifyUser_SendsDraftChangesEmailDataWithBoardNotes()
    {
        // Arrange
        var registration = BuildRegistration(RegistrationStatus.Pending);
        var edition = CreateEdition();

        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.Draft));
        _editionsRepo.GetByIdAsync(registration.CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _emailService.SendDraftChangesNotificationAsync(Arg.Any<DraftChangesEmailData>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var request = new AdminEditRegistrationRequest(
            Members: null, Extras: null, Preferences: null,
            Notes: "Cuota pendiente", SpecialNeeds: null, CampatesPreference: null, HasPet: null,
            NotifyUser: true);

        // Act
        await _sut.AdminUpdateAsync(RegistrationId, UserId, request, CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendDraftChangesNotificationAsync(
            Arg.Is<DraftChangesEmailData>(d => d.BoardNotes == "Cuota pendiente"),
            Arg.Any<CancellationToken>());
    }

    // ── NotifyDraftAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyDraftAsync_NonDraftStatus_ThrowsBusinessRuleException()
    {
        // Arrange
        var registration = BuildRegistration(RegistrationStatus.Pending);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);

        // Act
        var act = () => _sut.NotifyDraftAsync(RegistrationId, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*En revisión*");
    }

    [Fact]
    public async Task NotifyDraftAsync_DraftStatus_SendsEmailAndSetsFamilyNotifiedTrue()
    {
        // Arrange
        var registration = BuildRegistration(RegistrationStatus.Draft);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        _emailService.SendDraftChangesNotificationAsync(Arg.Any<DraftChangesEmailData>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.NotifyDraftAsync(RegistrationId, "Motivo junta", CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendDraftChangesNotificationAsync(
            Arg.Is<DraftChangesEmailData>(d => d.BoardNotes == "Motivo junta"),
            Arg.Any<CancellationToken>());
        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.FamilyNotifiedOfDraft),
            Arg.Any<CancellationToken>());
        result.FamilyNotifiedOfDraft.Should().BeTrue();
    }

    // ── Sort parameters ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminListAsync_DefaultSort_PassesCreatedAtDescToRepository()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.GetAdminPagedAsync(
                CampEditionId, 1, 20, null, null, null, null, null, null,
                AdminRegistrationSortBy.CreatedAt, true, Arg.Any<CancellationToken>())
            .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

        // Act
        await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, CancellationToken.None);

        // Assert
        await _repo.Received(1).GetAdminPagedAsync(
            CampEditionId, Arg.Any<int>(), Arg.Any<int>(),
            null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAdminListAsync_SortByFamilyName_PassesFamilyNameSortToRepository()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.GetAdminPagedAsync(
                CampEditionId, 1, 20, null, null, null, null, null, null,
                AdminRegistrationSortBy.FamilyName, false, Arg.Any<CancellationToken>())
            .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

        // Act
        await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
            AdminRegistrationSortBy.FamilyName, false, CancellationToken.None);

        // Assert
        await _repo.Received(1).GetAdminPagedAsync(
            CampEditionId, Arg.Any<int>(), Arg.Any<int>(),
            null, null, null, null, null, null,
            AdminRegistrationSortBy.FamilyName, false, Arg.Any<CancellationToken>());
    }

    // ── New projection fields ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminListAsync_WhenRegistrationHasMembers_ReturnsAttendancePeriodsInItem()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);

        var projections = new List<AdminRegistrationProjection>
        {
            new(Guid.NewGuid(), FamilyUnitId, "López Family", UserId,
                "Ana", "López", "ana@test.com", RegistrationStatus.Confirmed,
                2, 600m, 600m, DateTime.UtcNow,
                [AttendancePeriod.FirstWeek, AttendancePeriod.SecondWeek], [])
        };
        _repo.GetAdminPagedAsync(
                CampEditionId, Arg.Any<int>(), Arg.Any<int>(),
                null, null, null, null, null, null,
                Arg.Any<AdminRegistrationSortBy>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((projections, 1, new AdminRegistrationTotals(1, 2, 600m, 600m, 0m)));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, CancellationToken.None);

        // Assert
        result.Items[0].AttendancePeriods.Should().BeEquivalentTo(
            [AttendancePeriod.FirstWeek, AttendancePeriod.SecondWeek]);
    }

    [Fact]
    public async Task GetAdminListAsync_WhenRegistrationHasAccommodationPreferences_ReturnsPreferencesInItem()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);

        var prefs = new List<AdminRegistrationAccommodationSummary>
        {
            new("Bungalow Norte", AccommodationType.Bungalow, 1),
            new("Tienda Sur", AccommodationType.Tent, 2),
        };
        var projections = new List<AdminRegistrationProjection>
        {
            new(Guid.NewGuid(), FamilyUnitId, "Martín Family", UserId,
                "Pedro", "Martín", "pedro@test.com", RegistrationStatus.Pending,
                3, 750m, 0m, DateTime.UtcNow,
                [AttendancePeriod.Complete], prefs)
        };
        _repo.GetAdminPagedAsync(
                CampEditionId, Arg.Any<int>(), Arg.Any<int>(),
                null, null, null, null, null, null,
                Arg.Any<AdminRegistrationSortBy>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((projections, 1, new AdminRegistrationTotals(1, 3, 750m, 0m, 750m)));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, CancellationToken.None);

        // Assert
        result.Items[0].AccommodationPreferences.Should().HaveCount(2);
        result.Items[0].AccommodationPreferences[0].AccommodationName.Should().Be("Bungalow Norte");
        result.Items[0].AccommodationPreferences[0].AccommodationType.Should().Be(AccommodationType.Bungalow);
        result.Items[0].AccommodationPreferences[0].PreferenceOrder.Should().Be(1);
    }

    [Fact]
    public async Task GetAdminListAsync_WhenRegistrationHasNoAccommodationPreferences_ReturnsEmptyList()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);

        var projections = new List<AdminRegistrationProjection>
        {
            new(Guid.NewGuid(), FamilyUnitId, "Ruiz Family", UserId,
                "María", "Ruiz", "maria@test.com", RegistrationStatus.Pending,
                1, 300m, 0m, DateTime.UtcNow,
                [AttendancePeriod.WeekendVisit], [])
        };
        _repo.GetAdminPagedAsync(
                CampEditionId, Arg.Any<int>(), Arg.Any<int>(),
                null, null, null, null, null, null,
                Arg.Any<AdminRegistrationSortBy>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((projections, 1, new AdminRegistrationTotals(1, 1, 300m, 0m, 300m)));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, null, null, null, null,
            AdminRegistrationSortBy.CreatedAt, true, CancellationToken.None);

        // Assert
        result.Items[0].AccommodationPreferences.Should().BeEmpty();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static CampEdition CreateEdition() => new()
    {
        Id = CampEditionId,
        CampId = Guid.NewGuid(),
        Year = 2026,
        StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc),
        PricePerAdult = 500m,
        PricePerChild = 300m,
        PricePerBaby = 100m,
        Status = CampEditionStatus.Open,
        Camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 500m,
            PricePerChild = 300m,
            PricePerBaby = 100m
        }
    };

    private static Registration BuildRegistration(RegistrationStatus status) => new()
    {
        Id = RegistrationId,
        FamilyUnitId = FamilyUnitId,
        CampEditionId = CampEditionId,
        RegisteredByUserId = UserId,
        BaseTotalAmount = 500m,
        ExtrasAmount = 0m,
        TotalAmount = 500m,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FamilyUnit = new FamilyUnit
        {
            Id = FamilyUnitId,
            Name = "Test Family",
            RepresentativeUserId = UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        CampEdition = new CampEdition
        {
            Id = CampEditionId,
            CampId = Guid.NewGuid(),
            Year = 2026,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 500m,
            PricePerChild = 300m,
            PricePerBaby = 100m,
            Status = CampEditionStatus.Open,
            Camp = new Camp
            {
                Id = Guid.NewGuid(),
                Name = "Test Camp",
                PricePerAdult = 500m,
                PricePerChild = 300m,
                PricePerBaby = 100m
            }
        },
        RegisteredByUser = new User
        {
            Id = UserId,
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        },
        Members = [],
        Extras = [],
        Payments = [],
        StatusHistory = [],
        AccommodationPreferences = []
    };
}

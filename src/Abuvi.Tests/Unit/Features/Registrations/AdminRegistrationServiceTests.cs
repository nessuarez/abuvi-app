using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
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
        _sut = new RegistrationsService(
            _repo, _extrasRepo, _accommodationPrefsRepo, _familyUnitsRepo,
            _editionsRepo, _accommodationsRepo, _pricingService, _emailService, _paymentsService, _logger);
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
                3, 900m, 200m, DateTime.UtcNow)
        };
        var totals = new AdminRegistrationTotals(1, 3, 900m, 200m, 700m);

        _repo.GetAdminPagedAsync(CampEditionId, 1, 20, null, null, Arg.Any<CancellationToken>())
            .Returns((projections, 1, totals));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, CancellationToken.None);

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
        var act = () => _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAdminListAsync_ClampsPageAndPageSize()
    {
        // Arrange
        var edition = CreateEdition();
        _editionsRepo.GetByIdAsync(CampEditionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repo.GetAdminPagedAsync(CampEditionId, 1, 100, null, null, Arg.Any<CancellationToken>())
            .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, -5, 500, null, null, CancellationToken.None);

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
        _repo.GetAdminPagedAsync(CampEditionId, 1, 20, null, null, Arg.Any<CancellationToken>())
            .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

        // Act
        var result = await _sut.GetAdminListAsync(CampEditionId, 1, 20, null, null, CancellationToken.None);

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
            Notes: "Updated notes", SpecialNeeds: null, CampatesPreference: null);

        // Act
        var result = await _sut.AdminUpdateAsync(RegistrationId, request, CancellationToken.None);

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
            Notes: "test", SpecialNeeds: null, CampatesPreference: null);

        // Act
        var act = () => _sut.AdminUpdateAsync(RegistrationId, request, CancellationToken.None);

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
            Notes: "test", SpecialNeeds: null, CampatesPreference: null);

        // Act
        var act = () => _sut.AdminUpdateAsync(RegistrationId, request, CancellationToken.None);

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
            Notes: "admin edit", SpecialNeeds: null, CampatesPreference: null);

        // Act
        var act = () => _sut.AdminUpdateAsync(RegistrationId, request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
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
        Payments = []
    };
}

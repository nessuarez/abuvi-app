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

public class RegistrationsService_DeleteAsync_Tests
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

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FamilyUnitId = Guid.NewGuid();
    private static readonly Guid CampEditionId = Guid.NewGuid();

    public RegistrationsService_DeleteAsync_Tests()
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
            _editionsRepo, _accommodationsRepo, _pricingService, _emailService,
            _paymentsService, _logger);
    }

    // ── Successful Cases ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenPendingRegistrationWithinTimeWindow_ShouldDeleteSuccessfully()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Pending,
            createdAt: DateTime.UtcNow.AddHours(-1));

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _repo.DeleteAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await _repo.Received(1).DeleteAsync(registrationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenDraftRegistrationWithinTimeWindow_ShouldDeleteSuccessfully()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Draft,
            createdAt: DateTime.UtcNow.AddHours(-1));

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _repo.DeleteAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await _repo.Received(1).DeleteAsync(registrationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenAdminDeletesOutsideTimeWindow_ShouldDeleteSuccessfully()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Pending,
            createdAt: DateTime.UtcNow.AddDays(-3));

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _repo.DeleteAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: true, CancellationToken.None);

        // Assert
        await _repo.Received(1).DeleteAsync(registrationId, Arg.Any<CancellationToken>());
    }

    // ── Not Found ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenRegistrationNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns((Registration?)null);

        // Act
        Func<Task> act = async () =>
            await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Authorization Errors ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenUserIsNotRepresentativeOrAdmin_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Pending,
            createdAt: DateTime.UtcNow.AddHours(-1));

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // Act — use a different user ID that is NOT the representative
        Func<Task> act = async () =>
            await _sut.DeleteAsync(registrationId, otherUserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Business Rule Violations ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenStatusIsConfirmed_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Confirmed,
            createdAt: DateTime.UtcNow.AddHours(-1));

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // Act
        Func<Task> act = async () =>
            await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Confirmed*cannot be deleted*");
    }

    [Fact]
    public async Task DeleteAsync_WhenStatusIsCancelled_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Cancelled,
            createdAt: DateTime.UtcNow.AddHours(-1));

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // Act
        Func<Task> act = async () =>
            await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Cancelled*cannot be deleted*");
    }

    [Fact]
    public async Task DeleteAsync_WhenPaymentsExist_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Pending,
            createdAt: DateTime.UtcNow.AddHours(-1));
        registration.Payments =
        [
            new Payment
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                Amount = 100m,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            }
        ];

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // Act
        Func<Task> act = async () =>
            await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*payments*");
    }

    [Fact]
    public async Task DeleteAsync_WhenTimeWindowExpiredForRepresentative_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Pending,
            createdAt: DateTime.UtcNow.AddHours(-25));

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // Act
        Func<Task> act = async () =>
            await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*24 hours*");
    }

    [Fact]
    public async Task DeleteAsync_WhenAdminAndPaymentsExist_ShouldStillThrowBusinessRuleException()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        var registration = CreateRegistrationForDelete(registrationId,
            status: RegistrationStatus.Pending,
            createdAt: DateTime.UtcNow.AddHours(-1));
        registration.Payments =
        [
            new Payment
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                Amount = 100m,
                Status = PaymentStatus.Completed,
                CreatedAt = DateTime.UtcNow
            }
        ];

        _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        // Act
        Func<Task> act = async () =>
            await _sut.DeleteAsync(registrationId, UserId, isAdminOrBoard: true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*payments*");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Registration CreateRegistrationForDelete(
        Guid id,
        RegistrationStatus status,
        DateTime createdAt) => new()
    {
        Id = id,
        FamilyUnitId = FamilyUnitId,
        CampEditionId = CampEditionId,
        RegisteredByUserId = UserId,
        BaseTotalAmount = 500m,
        ExtrasAmount = 0m,
        TotalAmount = 500m,
        Status = status,
        CreatedAt = createdAt,
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
            Id = UserId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            EmailVerified = true
        },
        Members = [],
        Extras = [],
        Payments = []
    };
}

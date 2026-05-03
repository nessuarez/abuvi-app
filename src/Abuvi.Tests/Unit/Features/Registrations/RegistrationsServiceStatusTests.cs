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
using NSubstitute.ExceptionExtensions;

namespace Abuvi.Tests.Unit.Features.Registrations;

public class RegistrationsServiceStatusTests
{
    private readonly IRegistrationsRepository _repo = Substitute.For<IRegistrationsRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IPaymentsService _paymentsService = Substitute.For<IPaymentsService>();
    private readonly RegistrationsService _sut;

    private static readonly Guid RegistrationId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid FamilyUnitId = Guid.NewGuid();
    private static readonly Guid RepresentativeUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public RegistrationsServiceStatusTests()
    {
        var extrasRepo = Substitute.For<IRegistrationExtrasRepository>();
        var accommodationPrefsRepo = Substitute.For<IRegistrationAccommodationPreferencesRepository>();
        var familyUnitsRepo = Substitute.For<IFamilyUnitsRepository>();
        var editionsRepo = Substitute.For<ICampEditionsRepository>();
        var accommodationsRepo = Substitute.For<ICampEditionAccommodationsRepository>();
        var extrasDefinitionRepo = Substitute.For<ICampEditionExtrasRepository>();
        var settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        var membershipsRepo = Substitute.For<IMembershipsRepository>();
        var logger = Substitute.For<ILogger<RegistrationsService>>();
        var pricingService = new RegistrationPricingService(settingsRepo);

        _sut = new RegistrationsService(
            _repo, extrasRepo, accommodationPrefsRepo, familyUnitsRepo,
            editionsRepo, accommodationsRepo, extrasDefinitionRepo, pricingService, _emailService,
            _paymentsService, membershipsRepo, logger);
    }

    // ── ChangeStatusAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeStatusAsync_ValidTransition_PendingToPartiallyPaid_UpdatesStatus()
    {
        var registration = BuildRegistration(RegistrationStatus.Pending);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

        var request = new ChangeRegistrationStatusRequest(RegistrationStatus.PartiallyPaid, null);
        await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.Status == RegistrationStatus.PartiallyPaid),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_ValidTransition_LogsStatusHistory()
    {
        var registration = BuildRegistration(RegistrationStatus.Pending);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

        var request = new ChangeRegistrationStatusRequest(RegistrationStatus.PartiallyPaid, null);
        await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

        await _repo.Received(1).AddStatusHistoryAsync(
            Arg.Is<RegistrationStatusHistory>(h =>
                h.RegistrationId == RegistrationId &&
                h.PreviousStatus == RegistrationStatus.Pending &&
                h.NewStatus == RegistrationStatus.PartiallyPaid &&
                h.ChangedByUserId == AdminUserId &&
                h.Trigger == StatusChangeTrigger.AdminAction),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_TargetIsFullyPaid_ThrowsBusinessRuleException()
    {
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(RegistrationStatus.Pending));

        var act = () => _sut.ChangeStatusAsync(RegistrationId, AdminUserId,
            new ChangeRegistrationStatusRequest(RegistrationStatus.FullyPaid, null), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*automáticamente*");
    }

    [Fact]
    public async Task ChangeStatusAsync_TargetIsCancelled_ThrowsBusinessRuleException()
    {
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(RegistrationStatus.Pending));

        var act = () => _sut.ChangeStatusAsync(RegistrationId, AdminUserId,
            new ChangeRegistrationStatusRequest(RegistrationStatus.Cancelled, null), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*cancelación*");
    }

    [Fact]
    public async Task ChangeStatusAsync_TargetIsDraft_ThrowsBusinessRuleException()
    {
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(RegistrationStatus.Pending));

        var act = () => _sut.ChangeStatusAsync(RegistrationId, AdminUserId,
            new ChangeRegistrationStatusRequest(RegistrationStatus.Draft, null), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*revisión*");
    }

    [Fact]
    public async Task ChangeStatusAsync_CancelledToPending_ThrowsBusinessRuleException()
    {
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(RegistrationStatus.Cancelled));

        var act = () => _sut.ChangeStatusAsync(RegistrationId, AdminUserId,
            new ChangeRegistrationStatusRequest(RegistrationStatus.Pending, null), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*no está permitida*");
    }

    [Fact]
    public async Task ChangeStatusAsync_NotifyUser_PartiallyPaid_SendsPartiallyPaidEmail()
    {
        var registration = BuildRegistration(RegistrationStatus.Pending);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

        var request = new ChangeRegistrationStatusRequest(RegistrationStatus.PartiallyPaid, null, NotifyUser: true);
        await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

        await _emailService.Received(1).SendRegistrationPartiallyPaidAsync(
            Arg.Any<RegistrationStatusEmailData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_NotifyUser_Confirmed_SendsConfirmedEmail()
    {
        var registration = BuildRegistration(RegistrationStatus.FullyPaid);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.Confirmed));

        var request = new ChangeRegistrationStatusRequest(RegistrationStatus.Confirmed, null, NotifyUser: true);
        await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

        await _emailService.Received(1).SendRegistrationFinallyConfirmedAsync(
            Arg.Any<RegistrationStatusEmailData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_NotifyUser_False_NoEmailSent()
    {
        var registration = BuildRegistration(RegistrationStatus.Pending);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

        var request = new ChangeRegistrationStatusRequest(RegistrationStatus.PartiallyPaid, null, NotifyUser: false);
        await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

        await _emailService.DidNotReceive().SendRegistrationPartiallyPaidAsync(
            Arg.Any<RegistrationStatusEmailData>(), Arg.Any<CancellationToken>());
        await _emailService.DidNotReceive().SendRegistrationFinallyConfirmedAsync(
            Arg.Any<RegistrationStatusEmailData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_FromDraft_ClearsDraftFields()
    {
        var registration = BuildRegistration(RegistrationStatus.Draft,
            draftTargetStatus: RegistrationStatus.PartiallyPaid,
            hasPendingAcknowledgement: true);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

        var request = new ChangeRegistrationStatusRequest(RegistrationStatus.PartiallyPaid, null);
        await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r =>
                r.DraftTargetStatus == null &&
                !r.HasPendingUserAcknowledgement),
            Arg.Any<CancellationToken>());
    }

    // ── ConfirmChangesAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmChangesAsync_Draft_TransitionsToTargetStatus()
    {
        var registration = BuildRegistration(RegistrationStatus.Draft,
            draftTargetStatus: RegistrationStatus.PartiallyPaid);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

        await _sut.ConfirmChangesAsync(RegistrationId, RepresentativeUserId, false, CancellationToken.None);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.Status == RegistrationStatus.PartiallyPaid),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmChangesAsync_Draft_NullTargetStatus_TransitionsToPending()
    {
        var registration = BuildRegistration(RegistrationStatus.Draft, draftTargetStatus: null);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.Pending));

        await _sut.ConfirmChangesAsync(RegistrationId, RepresentativeUserId, false, CancellationToken.None);

        await _repo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.Status == RegistrationStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmChangesAsync_NotDraft_ThrowsBusinessRuleException()
    {
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(RegistrationStatus.Pending));

        var act = () => _sut.ConfirmChangesAsync(RegistrationId, RepresentativeUserId, false, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*revisión pendiente*");
    }

    [Fact]
    public async Task ConfirmChangesAsync_NonRepresentative_ThrowsUnauthorizedAccessException()
    {
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(BuildRegistration(RegistrationStatus.Draft));

        var act = () => _sut.ConfirmChangesAsync(RegistrationId, OtherUserId, false, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ConfirmChangesAsync_Admin_CanConfirmRegardlessOfRepresentative()
    {
        var registration = BuildRegistration(RegistrationStatus.Draft,
            draftTargetStatus: RegistrationStatus.Confirmed);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.Confirmed));

        var act = () => _sut.ConfirmChangesAsync(RegistrationId, OtherUserId, isAdminOrBoard: true, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConfirmChangesAsync_NonAdmin_LogsHistoryWithUserConfirmedTrigger()
    {
        var registration = BuildRegistration(RegistrationStatus.Draft,
            draftTargetStatus: RegistrationStatus.PartiallyPaid);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

        await _sut.ConfirmChangesAsync(RegistrationId, RepresentativeUserId, false, CancellationToken.None);

        await _repo.Received(1).AddStatusHistoryAsync(
            Arg.Is<RegistrationStatusHistory>(h => h.Trigger == StatusChangeTrigger.UserConfirmed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmChangesAsync_Admin_LogsHistoryWithAdminActionTrigger()
    {
        var registration = BuildRegistration(RegistrationStatus.Draft,
            draftTargetStatus: RegistrationStatus.Confirmed);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.Confirmed));

        await _sut.ConfirmChangesAsync(RegistrationId, AdminUserId, isAdminOrBoard: true, CancellationToken.None);

        await _repo.Received(1).AddStatusHistoryAsync(
            Arg.Is<RegistrationStatusHistory>(h => h.Trigger == StatusChangeTrigger.AdminAction),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmChangesAsync_SendsDraftChangesConfirmedEmail()
    {
        var registration = BuildRegistration(RegistrationStatus.Draft,
            draftTargetStatus: RegistrationStatus.PartiallyPaid);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

        await _sut.ConfirmChangesAsync(RegistrationId, RepresentativeUserId, false, CancellationToken.None);

        await _emailService.Received(1).SendDraftChangesConfirmedAsync(
            Arg.Any<DraftChangesConfirmedEmailData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmChangesAsync_EmailFailureIsNonBlocking()
    {
        var registration = BuildRegistration(RegistrationStatus.Draft,
            draftTargetStatus: RegistrationStatus.PartiallyPaid);
        _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));
        _emailService
            .SendDraftChangesConfirmedAsync(Arg.Any<DraftChangesConfirmedEmailData>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SMTP error"));

        var act = () => _sut.ConfirmChangesAsync(RegistrationId, RepresentativeUserId, false, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Registration BuildRegistration(
        RegistrationStatus status,
        RegistrationStatus? draftTargetStatus = null,
        bool hasPendingAcknowledgement = false) => new()
    {
        Id = RegistrationId,
        FamilyUnitId = FamilyUnitId,
        CampEditionId = Guid.NewGuid(),
        RegisteredByUserId = RepresentativeUserId,
        BaseTotalAmount = 500m,
        ExtrasAmount = 0m,
        TotalAmount = 500m,
        Status = status,
        DraftTargetStatus = draftTargetStatus,
        HasPendingUserAcknowledgement = hasPendingAcknowledgement,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FamilyUnit = new FamilyUnit
        {
            Id = FamilyUnitId,
            Name = "Test Family",
            RepresentativeUserId = RepresentativeUserId
        },
        CampEdition = new CampEdition
        {
            Id = Guid.NewGuid(),
            Year = 2026,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc),
            PricePerAdult = 500m,
            PricePerChild = 300m,
            PricePerBaby = 0m,
            Status = CampEditionStatus.Open,
            Camp = new Camp { Id = Guid.NewGuid(), Name = "Test Camp" }
        },
        RegisteredByUser = new User
        {
            Id = RepresentativeUserId,
            Email = "rep@test.com",
            FirstName = "Test",
            LastName = "Rep",
            PasswordHash = "hash"
        },
        Members = [],
        Extras = [],
        Payments = [],
        StatusHistory = [],
        AccommodationPreferences = []
    };
}

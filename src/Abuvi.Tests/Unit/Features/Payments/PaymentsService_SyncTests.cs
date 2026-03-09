using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.Payments;

public class PaymentsService_SyncTests
{
    private readonly IPaymentsRepository _paymentsRepo = Substitute.For<IPaymentsRepository>();
    private readonly IRegistrationsRepository _registrationsRepo = Substitute.For<IRegistrationsRepository>();
    private readonly IAssociationSettingsRepository _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
    private readonly IBlobStorageService _blobStorageService = Substitute.For<IBlobStorageService>();
    private readonly ILogger<PaymentsService> _logger = Substitute.For<ILogger<PaymentsService>>();
    private readonly PaymentsService _sut;

    private static readonly Guid RegId = Guid.NewGuid();

    private static readonly DateTime CampStart = new(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExtrasDeadline = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    public PaymentsService_SyncTests()
    {
        _sut = new PaymentsService(
            _paymentsRepo, _registrationsRepo, _settingsRepo, _blobStorageService, _logger);
    }

    // ── SyncExtrasInstallmentAsync ──────────────────────────────────────────

    [Fact]
    public async Task SyncExtras_NoP3Exists_PositiveExtras_CreatesP3WithCorrectAmount()
    {
        var registration = CreateRegistration();
        _registrationsRepo.GetByIdWithDetailsAsync(RegId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([CreatePayment(1, PaymentStatus.Pending, 250m), CreatePayment(2, PaymentStatus.Pending, 250m)]);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.SyncExtrasInstallmentAsync(RegId, 150m, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Amount.Should().Be(150m);
        result.InstallmentNumber.Should().Be(3);
        await _paymentsRepo.Received(1).AddAsync(
            Arg.Is<Payment>(p => p.InstallmentNumber == 3 && p.Amount == 150m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncExtras_P3ExistsPending_UpdatesAmount()
    {
        var registration = CreateRegistration();
        _registrationsRepo.GetByIdWithDetailsAsync(RegId, Arg.Any<CancellationToken>())
            .Returns(registration);
        var p3 = CreatePayment(3, PaymentStatus.Pending, 100m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([CreatePayment(1, PaymentStatus.Completed, 250m), CreatePayment(2, PaymentStatus.Pending, 250m), p3]);

        var result = await _sut.SyncExtrasInstallmentAsync(RegId, 200m, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Amount.Should().Be(200m);
        p3.Amount.Should().Be(200m);
        await _paymentsRepo.Received(1).UpdateAsync(p3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncExtras_P3ExistsPending_ZeroExtras_DeletesP3()
    {
        var registration = CreateRegistration();
        _registrationsRepo.GetByIdWithDetailsAsync(RegId, Arg.Any<CancellationToken>())
            .Returns(registration);
        var p3 = CreatePayment(3, PaymentStatus.Pending, 100m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([CreatePayment(1, PaymentStatus.Pending, 250m), CreatePayment(2, PaymentStatus.Pending, 250m), p3]);

        var result = await _sut.SyncExtrasInstallmentAsync(RegId, 0m, CancellationToken.None);

        result.Should().BeNull();
        await _paymentsRepo.Received(1).DeleteAsync(p3.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncExtras_P3PendingReview_ThrowsBusinessRuleException()
    {
        var registration = CreateRegistration();
        _registrationsRepo.GetByIdWithDetailsAsync(RegId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([CreatePayment(1, PaymentStatus.Pending, 250m), CreatePayment(2, PaymentStatus.Pending, 250m),
                      CreatePayment(3, PaymentStatus.PendingReview, 100m)]);

        var act = () => _sut.SyncExtrasInstallmentAsync(RegId, 200m, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task SyncExtras_P3Completed_ThrowsBusinessRuleException()
    {
        var registration = CreateRegistration();
        _registrationsRepo.GetByIdWithDetailsAsync(RegId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([CreatePayment(1, PaymentStatus.Completed, 250m), CreatePayment(2, PaymentStatus.Completed, 250m),
                      CreatePayment(3, PaymentStatus.Completed, 100m)]);

        var act = () => _sut.SyncExtrasInstallmentAsync(RegId, 150m, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task SyncExtras_DueDateReflectsCampEditionExtrasDeadline()
    {
        var registration = CreateRegistration(extrasDeadline: ExtrasDeadline);
        _registrationsRepo.GetByIdWithDetailsAsync(RegId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([CreatePayment(1, PaymentStatus.Pending, 250m), CreatePayment(2, PaymentStatus.Pending, 250m)]);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        Payment? capturedP3 = null;
        await _paymentsRepo.AddAsync(Arg.Do<Payment>(p => capturedP3 = p), Arg.Any<CancellationToken>());

        await _sut.SyncExtrasInstallmentAsync(RegId, 100m, CancellationToken.None);

        capturedP3.Should().NotBeNull();
        capturedP3!.DueDate.Should().Be(ExtrasDeadline);
    }

    // ── SyncBaseInstallmentsAsync ────────────────────────────────────────────

    [Fact]
    public async Task SyncBase_BothPending_RecalculatesBoth()
    {
        var p1 = CreatePayment(1, PaymentStatus.Pending, 100m);
        var p2 = CreatePayment(2, PaymentStatus.Pending, 100m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        await _sut.SyncBaseInstallmentsAsync(RegId, 300m, 200m, CancellationToken.None);

        p1.Amount.Should().Be(150m);
        p2.Amount.Should().Be(150m);
        await _paymentsRepo.Received(1).UpdateAsync(p1, Arg.Any<CancellationToken>());
        await _paymentsRepo.Received(1).UpdateAsync(p2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncBase_BothPending_OddAmount_RoundsP1Up()
    {
        var p1 = CreatePayment(1, PaymentStatus.Pending, 100m);
        var p2 = CreatePayment(2, PaymentStatus.Pending, 101m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        await _sut.SyncBaseInstallmentsAsync(RegId, 201m, 201m, CancellationToken.None);

        p1.Amount.Should().Be(101m);
        p2.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task SyncBase_P1Completed_P2Pending_AbsorbsDelta()
    {
        var p1 = CreatePayment(1, PaymentStatus.Completed, 250m);
        var p2 = CreatePayment(2, PaymentStatus.Pending, 250m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        await _sut.SyncBaseInstallmentsAsync(RegId, 600m, 500m, CancellationToken.None);

        p2.Amount.Should().Be(350m); // 250 + 100 delta
        await _paymentsRepo.Received(1).UpdateAsync(p2, Arg.Any<CancellationToken>());
        await _paymentsRepo.DidNotReceive().UpdateAsync(p1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncBase_P1Completed_P2Pending_NegativeDelta_DecreasesP2()
    {
        var p1 = CreatePayment(1, PaymentStatus.Completed, 250m);
        var p2 = CreatePayment(2, PaymentStatus.Pending, 250m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        await _sut.SyncBaseInstallmentsAsync(RegId, 400m, 500m, CancellationToken.None);

        p2.Amount.Should().Be(150m); // 250 - 100 delta
    }

    [Fact]
    public async Task SyncBase_P1Completed_P2Pending_DeltaWouldMakeP2NonPositive_Throws()
    {
        var p1 = CreatePayment(1, PaymentStatus.Completed, 250m);
        var p2 = CreatePayment(2, PaymentStatus.Pending, 100m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        // delta = 300 - 500 = -200; p2 = 100 - 200 = -100 → invalid
        var act = () => _sut.SyncBaseInstallmentsAsync(RegId, 300m, 500m, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task SyncBase_P1PendingReview_Throws()
    {
        var p1 = CreatePayment(1, PaymentStatus.PendingReview, 250m);
        var p2 = CreatePayment(2, PaymentStatus.Pending, 250m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        var act = () => _sut.SyncBaseInstallmentsAsync(RegId, 600m, 500m, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task SyncBase_P1Completed_P2PendingReview_Throws()
    {
        var p1 = CreatePayment(1, PaymentStatus.Completed, 250m);
        var p2 = CreatePayment(2, PaymentStatus.PendingReview, 250m);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        var act = () => _sut.SyncBaseInstallmentsAsync(RegId, 600m, 500m, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Registration CreateRegistration(DateTime? extrasDeadline = null) => new()
    {
        Id = RegId,
        FamilyUnitId = Guid.NewGuid(),
        CampEditionId = Guid.NewGuid(),
        RegisteredByUserId = Guid.NewGuid(),
        TotalAmount = 500m,
        BaseTotalAmount = 500m,
        ExtrasAmount = 0m,
        Status = RegistrationStatus.Pending,
        FamilyUnit = new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = "García López",
            RepresentativeUserId = Guid.NewGuid()
        },
        CampEdition = new CampEdition
        {
            Id = Guid.NewGuid(),
            Year = 2026,
            StartDate = CampStart,
            EndDate = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = CampEditionStatus.Open,
            PricePerAdult = 500m,
            PricePerChild = 300m,
            PricePerBaby = 0m,
            ExtrasPaymentDeadline = extrasDeadline,
            Camp = new Camp { Id = Guid.NewGuid(), Name = "Camp Test" }
        },
        Members = [],
        Extras = [],
        Payments = [],
        AccommodationPreferences = []
    };

    private static Payment CreatePayment(int installment, PaymentStatus status, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        RegistrationId = RegId,
        Amount = amount,
        PaymentDate = DateTime.UtcNow,
        Method = PaymentMethod.Transfer,
        Status = status,
        InstallmentNumber = installment,
        TransferConcept = $"CAMP-GAR-{installment}",
        DueDate = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
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

public class PaymentsService_AdjustmentTests
{
    private readonly IPaymentsRepository _paymentsRepo = Substitute.For<IPaymentsRepository>();
    private readonly IRegistrationsRepository _registrationsRepo = Substitute.For<IRegistrationsRepository>();
    private readonly IAssociationSettingsRepository _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
    private readonly IBlobStorageService _blobStorage = Substitute.For<IBlobStorageService>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILogger<PaymentsService> _logger = Substitute.For<ILogger<PaymentsService>>();
    private readonly PaymentsService _sut;

    private static readonly Guid RegistrationId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();

    public PaymentsService_AdjustmentTests()
    {
        _sut = new PaymentsService(
            _paymentsRepo, _registrationsRepo, _settingsRepo,
            _blobStorage, _emailService, _logger);

        _settingsRepo.GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsNull();
    }

    // ── AdminEditPaymentAsync ────────────────────────────────────────────────

    [Fact]
    public async Task AdminEditPaymentAsync_WhenPaymentFailed_ThrowsBusinessRuleException()
    {
        var payment = MakePayment(PaymentStatus.Failed, 1, 100m);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var act = () => _sut.AdminEditPaymentAsync(
            PaymentId, new AdminEditPaymentRequest { Amount = 80m }, AdminUserId, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task AdminEditPaymentAsync_WhenPaymentRefunded_ThrowsBusinessRuleException()
    {
        var payment = MakePayment(PaymentStatus.Refunded, 1, 100m);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var act = () => _sut.AdminEditPaymentAsync(
            PaymentId, new AdminEditPaymentRequest { Amount = 50m }, AdminUserId, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task AdminEditPaymentAsync_WhenFirstEdit_SnapshotsOriginalAmount()
    {
        var payment = MakePayment(PaymentStatus.Pending, 1, 100m);
        payment.OriginalAmount = null;
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([payment]);

        await _sut.AdminEditPaymentAsync(
            PaymentId, new AdminEditPaymentRequest { Amount = 80m }, AdminUserId, CancellationToken.None);

        payment.OriginalAmount.Should().Be(100m);
    }

    [Fact]
    public async Task AdminEditPaymentAsync_WhenSecondEdit_DoesNotOverwriteOriginalAmount()
    {
        var payment = MakePayment(PaymentStatus.Pending, 1, 90m);
        payment.OriginalAmount = 100m; // already set from first edit
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([payment]);

        await _sut.AdminEditPaymentAsync(
            PaymentId, new AdminEditPaymentRequest { Amount = 70m }, AdminUserId, CancellationToken.None);

        payment.OriginalAmount.Should().Be(100m);
    }

    [Fact]
    public async Task AdminEditPaymentAsync_SetsConceptOverriddenToTrue()
    {
        var payment = MakePayment(PaymentStatus.Pending, 1, 100m);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([payment]);

        await _sut.AdminEditPaymentAsync(
            PaymentId, new AdminEditPaymentRequest { Amount = 80m }, AdminUserId, CancellationToken.None);

        payment.ConceptOverridden.Should().BeTrue();
    }

    [Fact]
    public async Task AdminEditPaymentAsync_WhenAmountNotProvided_DoesNotUpdateAmount()
    {
        var payment = MakePayment(PaymentStatus.Pending, 1, 100m);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([payment]);

        await _sut.AdminEditPaymentAsync(
            PaymentId, new AdminEditPaymentRequest { AdminNotes = "updated note" }, AdminUserId, CancellationToken.None);

        payment.Amount.Should().Be(100m);
        payment.OriginalAmount.Should().BeNull();
        payment.ConceptOverridden.Should().BeFalse();
    }

    [Fact]
    public async Task AdminEditPaymentAsync_WhenConceptProvided_SetsConceptOverridden()
    {
        var payment = MakePayment(PaymentStatus.Pending, 1, 100m);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([payment]);

        await _sut.AdminEditPaymentAsync(
            PaymentId,
            new AdminEditPaymentRequest { ConceptDescription = "Cargo especial" },
            AdminUserId, CancellationToken.None);

        payment.ConceptOverridden.Should().BeTrue();
        payment.ConceptLinesSerialized.Should().Contain("Cargo especial");
    }

    [Fact]
    public async Task AdminEditPaymentAsync_WhenPaymentCompleted_TriggersRecalculation()
    {
        var payment = MakePayment(PaymentStatus.Completed, 1, 100m);
        var p2 = MakePaymentEntity(PaymentStatus.Pending, 2, 100m);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([payment, p2]);

        var registration = MakeRegistration(baseTotalAmount: 200m, extrasAmount: 0m);
        _registrationsRepo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([payment, p2]);

        await _sut.AdminEditPaymentAsync(
            PaymentId, new AdminEditPaymentRequest { Amount = 150m }, AdminUserId, CancellationToken.None);

        // RecalculatePendingInstallmentsAsync should have updated P2
        await _paymentsRepo.Received().UpdateAsync(
            Arg.Is<Payment>(p => p.InstallmentNumber == 2),
            Arg.Any<CancellationToken>());
    }

    // ── RecalculatePendingInstallmentsAsync ──────────────────────────────────

    [Fact]
    public async Task RecalculatePendingInstallments_WhenP1EditedHigher_ReducesP2Accordingly()
    {
        var p1 = MakePaymentEntity(PaymentStatus.Completed, 1, 150m);
        var p2 = MakePaymentEntity(PaymentStatus.Pending, 2, 100m);

        var registration = MakeRegistration(baseTotalAmount: 200m, extrasAmount: 0m);
        _registrationsRepo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        await _sut.RecalculatePendingInstallmentsAsync(RegistrationId, AdminUserId, CancellationToken.None);

        p2.Amount.Should().Be(50m); // 200 - 150 = 50
    }

    [Fact]
    public async Task RecalculatePendingInstallments_WhenP1EditedToFullTotal_SetsP2ToZero()
    {
        var p1 = MakePaymentEntity(PaymentStatus.Completed, 1, 200m);
        var p2 = MakePaymentEntity(PaymentStatus.Pending, 2, 100m);

        var registration = MakeRegistration(baseTotalAmount: 200m, extrasAmount: 0m);
        _registrationsRepo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([p1, p2]);

        await _sut.RecalculatePendingInstallmentsAsync(RegistrationId, AdminUserId, CancellationToken.None);

        p2.Amount.Should().Be(0m);
    }

    [Fact]
    public async Task RecalculatePendingInstallments_WhenOverpaid_GeneratesRefundPayment()
    {
        var p1 = MakePaymentEntity(PaymentStatus.Completed, 1, 250m); // overpaid by 50

        var registration = MakeRegistration(baseTotalAmount: 200m, extrasAmount: 0m);
        _registrationsRepo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([p1]);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([p1]);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(MakeRegistrationWithDetails());

        await _sut.RecalculatePendingInstallmentsAsync(RegistrationId, AdminUserId, CancellationToken.None);

        await _paymentsRepo.Received(1).AddAsync(
            Arg.Is<Payment>(p => p.Status == PaymentStatus.Refunded && p.Amount == -50m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecalculatePendingInstallments_SkipsManualPayments()
    {
        var p1 = MakePaymentEntity(PaymentStatus.Completed, 1, 100m);
        var manual = MakePaymentEntity(PaymentStatus.Pending, 4, 50m, isManual: true);

        var registration = MakeRegistration(baseTotalAmount: 200m, extrasAmount: 0m);
        _registrationsRepo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([p1, manual]);

        await _sut.RecalculatePendingInstallmentsAsync(RegistrationId, AdminUserId, CancellationToken.None);

        // Manual payment amount should not have changed
        manual.Amount.Should().Be(50m);
    }

    [Fact]
    public async Task RecalculatePendingInstallments_NoPendingPayments_DoesNotCallUpdate()
    {
        var p1 = MakePaymentEntity(PaymentStatus.Completed, 1, 200m);

        var registration = MakeRegistration(baseTotalAmount: 200m, extrasAmount: 0m);
        _registrationsRepo.GetByIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([p1]);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([p1]);

        await _sut.RecalculatePendingInstallmentsAsync(RegistrationId, AdminUserId, CancellationToken.None);

        await _paymentsRepo.DidNotReceive().UpdateAsync(
            Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    // ── ConfirmCombinedPaymentsAsync ─────────────────────────────────────────

    [Fact]
    public async Task ConfirmCombinedPayments_ExactAmountMatch_ConfirmsAtOriginalAmounts()
    {
        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var p1 = MakePaymentEntityWithId(p1Id, PaymentStatus.Pending, 1, 100m);
        var p2 = MakePaymentEntityWithId(p2Id, PaymentStatus.Pending, 2, 100m);

        SetupRegistrationAndPayments([p1, p2]);

        var request = new ConfirmCombinedPaymentsRequest
        {
            PaymentIds = [p1Id, p2Id],
            TotalReceivedAmount = 200m
        };

        var result = await _sut.ConfirmCombinedPaymentsAsync(
            RegistrationId, request, AdminUserId, CancellationToken.None);

        result.Should().HaveCount(2);
        p1.Status.Should().Be(PaymentStatus.Completed);
        p2.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task ConfirmCombinedPayments_SurplusAmount_GreedyFillsInOrder()
    {
        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var p1 = MakePaymentEntityWithId(p1Id, PaymentStatus.Pending, 1, 100m);
        var p2 = MakePaymentEntityWithId(p2Id, PaymentStatus.Pending, 2, 100m);

        SetupRegistrationAndPayments([p1, p2]);

        var request = new ConfirmCombinedPaymentsRequest
        {
            PaymentIds = [p1Id, p2Id],
            TotalReceivedAmount = 150m // only covers P1 fully, P2 partially
        };

        await _sut.ConfirmCombinedPaymentsAsync(RegistrationId, request, AdminUserId, CancellationToken.None);

        p1.Amount.Should().Be(100m); // fully covered
        p2.Amount.Should().Be(50m);  // remainder
    }

    [Fact]
    public async Task ConfirmCombinedPayments_PaymentNotInRegistration_ThrowsNotFoundException()
    {
        var p1 = MakePaymentEntityWithId(Guid.NewGuid(), PaymentStatus.Pending, 1, 100m);
        SetupRegistrationAndPayments([p1]);

        var request = new ConfirmCombinedPaymentsRequest
        {
            PaymentIds = [Guid.NewGuid()], // unknown ID
            TotalReceivedAmount = 100m
        };

        var act = () => _sut.ConfirmCombinedPaymentsAsync(
            RegistrationId, request, AdminUserId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ConfirmCombinedPayments_PaymentAlreadyCompleted_ThrowsBusinessRuleException()
    {
        var p1Id = Guid.NewGuid();
        var p1 = MakePaymentEntityWithId(p1Id, PaymentStatus.Completed, 1, 100m);
        SetupRegistrationAndPayments([p1]);

        var request = new ConfirmCombinedPaymentsRequest
        {
            PaymentIds = [p1Id],
            TotalReceivedAmount = 100m
        };

        var act = () => _sut.ConfirmCombinedPaymentsAsync(
            RegistrationId, request, AdminUserId, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task ConfirmCombinedPayments_SetsConfirmedByAndConfirmedAt()
    {
        var p1Id = Guid.NewGuid();
        var p1 = MakePaymentEntityWithId(p1Id, PaymentStatus.Pending, 1, 100m);
        SetupRegistrationAndPayments([p1]);

        var request = new ConfirmCombinedPaymentsRequest
        {
            PaymentIds = [p1Id],
            TotalReceivedAmount = 100m
        };

        await _sut.ConfirmCombinedPaymentsAsync(RegistrationId, request, AdminUserId, CancellationToken.None);

        p1.ConfirmedByUserId.Should().Be(AdminUserId);
        p1.ConfirmedAt.Should().NotBeNull();
    }

    // ── GenerateRefundPaymentAsync ───────────────────────────────────────────

    [Fact]
    public async Task GenerateRefundPayment_CreatesNegativeAmountPayment()
    {
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([MakePaymentEntity(PaymentStatus.Completed, 1, 100m)]);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(MakeRegistrationWithDetails());

        await _sut.GenerateRefundPaymentAsync(
            RegistrationId, 50m, "Test refund", AdminUserId, CancellationToken.None);

        await _paymentsRepo.Received(1).AddAsync(
            Arg.Is<Payment>(p => p.Amount == -50m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRefundPayment_SetsIsManualTrue()
    {
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([]);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(MakeRegistrationWithDetails());

        await _sut.GenerateRefundPaymentAsync(
            RegistrationId, 30m, "Refund", AdminUserId, CancellationToken.None);

        await _paymentsRepo.Received(1).AddAsync(
            Arg.Is<Payment>(p => p.IsManual),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRefundPayment_SetsStatusRefunded()
    {
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([]);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(MakeRegistrationWithDetails());

        await _sut.GenerateRefundPaymentAsync(
            RegistrationId, 30m, "Refund", AdminUserId, CancellationToken.None);

        await _paymentsRepo.Received(1).AddAsync(
            Arg.Is<Payment>(p => p.Status == PaymentStatus.Refunded),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRefundPayment_AssignsNextInstallmentNumber()
    {
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([
                MakePaymentEntity(PaymentStatus.Completed, 1, 100m),
                MakePaymentEntity(PaymentStatus.Completed, 2, 100m)
            ]);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(MakeRegistrationWithDetails());

        await _sut.GenerateRefundPaymentAsync(
            RegistrationId, 30m, "Refund", AdminUserId, CancellationToken.None);

        await _paymentsRepo.Received(1).AddAsync(
            Arg.Is<Payment>(p => p.InstallmentNumber == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateRefundPayment_UpdatesRegistrationTotalAmount()
    {
        var registration = MakeRegistrationWithDetails();
        registration.TotalAmount = 200m;

        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([]);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);

        await _sut.GenerateRefundPaymentAsync(
            RegistrationId, 50m, "Refund", AdminUserId, CancellationToken.None);

        await _registrationsRepo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.TotalAmount == 150m),
            Arg.Any<CancellationToken>());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Payment MakePayment(PaymentStatus status, int installment, decimal amount) => new()
    {
        Id = PaymentId,
        RegistrationId = RegistrationId,
        Amount = amount,
        InstallmentNumber = installment,
        Status = status,
        PaymentDate = DateTime.UtcNow,
        Method = PaymentMethod.Transfer,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Registration = new Registration
        {
            Id = RegistrationId,
            FamilyUnit = new FamilyUnit { Id = Guid.NewGuid(), Name = "García", RepresentativeUserId = Guid.NewGuid() },
            CampEdition = new CampEdition
            {
                Id = Guid.NewGuid(),
                Year = 2026,
                StartDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
                Camp = new Camp { Id = Guid.NewGuid(), Name = "Camp Test" }
            }
        }
    };

    private static Payment MakePaymentEntity(
        PaymentStatus status, int installment, decimal amount, bool isManual = false) => new()
    {
        Id = Guid.NewGuid(),
        RegistrationId = RegistrationId,
        Amount = amount,
        InstallmentNumber = installment,
        Status = status,
        IsManual = isManual,
        PaymentDate = DateTime.UtcNow,
        Method = PaymentMethod.Transfer,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Payment MakePaymentEntityWithId(
        Guid id, PaymentStatus status, int installment, decimal amount) => new()
    {
        Id = id,
        RegistrationId = RegistrationId,
        Amount = amount,
        InstallmentNumber = installment,
        Status = status,
        PaymentDate = DateTime.UtcNow,
        Method = PaymentMethod.Transfer,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Registration = new Registration
        {
            Id = RegistrationId,
            FamilyUnit = new FamilyUnit { Id = Guid.NewGuid(), Name = "García", RepresentativeUserId = Guid.NewGuid() },
            CampEdition = new CampEdition
            {
                Id = Guid.NewGuid(),
                Year = 2026,
                StartDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
                Camp = new Camp { Id = Guid.NewGuid(), Name = "Camp Test" }
            }
        }
    };

    private static Registration MakeRegistration(decimal baseTotalAmount, decimal extrasAmount) => new()
    {
        Id = RegistrationId,
        BaseTotalAmount = baseTotalAmount,
        ExtrasAmount = extrasAmount,
        TotalAmount = baseTotalAmount + extrasAmount,
        Status = RegistrationStatus.PartiallyPaid
    };

    private static Registration MakeRegistrationWithDetails() => new()
    {
        Id = RegistrationId,
        BaseTotalAmount = 200m,
        ExtrasAmount = 0m,
        TotalAmount = 200m,
        Status = RegistrationStatus.PartiallyPaid,
        FamilyUnit = new FamilyUnit { Id = Guid.NewGuid(), Name = "García", RepresentativeUserId = Guid.NewGuid() },
        CampEdition = new CampEdition
        {
            Id = Guid.NewGuid(),
            Year = 2026,
            StartDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Camp = new Camp { Id = Guid.NewGuid(), Name = "Camp Test" }
        },
        Members = [],
        Extras = [],
        Payments = []
    };

    private void SetupRegistrationAndPayments(List<Payment> payments)
    {
        var registration = MakeRegistrationWithDetails();
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _paymentsRepo.GetByRegistrationIdTrackedAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(payments);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(payments);

        // Return a payment with navigation for GetByIdWithRegistrationAsync
        foreach (var p in payments)
        {
            if (p.Registration == null)
                p.Registration = registration;
            _paymentsRepo.GetByIdWithRegistrationAsync(p.Id, Arg.Any<CancellationToken>())
                .Returns(p);
        }
    }
}

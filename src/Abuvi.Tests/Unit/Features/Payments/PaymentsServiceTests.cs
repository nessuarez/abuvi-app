using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.Payments;

public class PaymentsServiceTests
{
    private readonly IPaymentsRepository _paymentsRepo = Substitute.For<IPaymentsRepository>();
    private readonly IRegistrationsRepository _registrationsRepo = Substitute.For<IRegistrationsRepository>();
    private readonly IAssociationSettingsRepository _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
    private readonly IBlobStorageService _blobStorageService = Substitute.For<IBlobStorageService>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILogger<PaymentsService> _logger = Substitute.For<ILogger<PaymentsService>>();
    private readonly PaymentsService _sut;

    private static readonly Guid RegistrationId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();

    public PaymentsServiceTests()
    {
        _sut = new PaymentsService(
            _paymentsRepo, _registrationsRepo, _settingsRepo,
            _blobStorageService, _emailService, _logger);
    }

    // ── CreateInstallmentsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task CreateInstallmentsAsync_ValidRegistration_CreatesTwoPayments()
    {
        // Arrange
        var registration = CreateRegistration(totalAmount: 200m);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        await _paymentsRepo.Received(1).AddRangeAsync(
            Arg.Is<List<Payment>>(p => p.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateInstallmentsAsync_ValidRegistration_SplitsAmountEvenly()
    {
        var registration = CreateRegistration(totalAmount: 200m);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        result[0].Amount.Should().Be(100m);
        result[1].Amount.Should().Be(100m);
    }

    [Fact]
    public async Task CreateInstallmentsAsync_OddAmount_RoundsFirstInstallmentUp()
    {
        var registration = CreateRegistration(totalAmount: 201m);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        result[0].Amount.Should().Be(101m);
        result[1].Amount.Should().Be(100m);
    }

    [Fact]
    public async Task CreateInstallmentsAsync_ValidRegistration_GeneratesTransferConcepts()
    {
        var registration = CreateRegistration(totalAmount: 200m);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        result[0].TransferConcept.Should().Contain("-1");
        result[1].TransferConcept.Should().Contain("-2");
        result[0].TransferConcept.Should().StartWith("CAMP-");
    }

    [Fact]
    public async Task CreateInstallmentsAsync_NoDeadlinesSet_FallsBackToSettings()
    {
        var registration = CreateRegistration(totalAmount: 200m);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        // Default FirstInstallmentDaysBefore = 30, SecondInstallmentDaysBefore = 15
        result[0].DueDate.Should().BeCloseTo(
            registration.CampEdition.StartDate.AddDays(-30), TimeSpan.FromMinutes(1));
        result[1].DueDate.Should().BeCloseTo(
            registration.CampEdition.StartDate.AddDays(-15), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateInstallmentsAsync_DeadlinesSetOnEdition_UsesEditionDates()
    {
        var registration = CreateRegistration(totalAmount: 200m);
        var firstDeadline = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var secondDeadline = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        registration.CampEdition.FirstPaymentDeadline = firstDeadline;
        registration.CampEdition.SecondPaymentDeadline = secondDeadline;

        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        result[0].DueDate.Should().Be(firstDeadline);
        result[1].DueDate.Should().Be(secondDeadline);
    }

    [Fact]
    public async Task CreateInstallmentsAsync_RegistrationNotFound_ThrowsNotFoundException()
    {
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var act = () => _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateInstallmentsAsync_NoPaymentSettings_UsesDefaults()
    {
        var registration = CreateRegistration(totalAmount: 200m);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        // Default prefix is "CAMP"
        result[0].TransferConcept.Should().StartWith("CAMP-");
    }

    // ── UploadProofAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UploadProofAsync_PendingPayment_UpdatesProofFieldsAndStatus()
    {
        var payment = CreatePayment(PaymentStatus.Pending);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _blobStorageService.UploadAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new BlobUploadResult("https://cdn.test.com/proof.jpg", null, "proof.jpg", "image/jpeg", 1024));

        var file = CreateFormFile();
        var result = await _sut.UploadProofAsync(PaymentId, UserId, file, CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.PendingReview);
        result.ProofFileUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task UploadProofAsync_CompletedPayment_ThrowsBusinessRuleException()
    {
        var payment = CreatePayment(PaymentStatus.Completed);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var file = CreateFormFile();
        var act = () => _sut.UploadProofAsync(PaymentId, UserId, file, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UploadProofAsync_WrongUser_ThrowsBusinessRuleException()
    {
        var payment = CreatePayment(PaymentStatus.Pending);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var file = CreateFormFile();
        var wrongUserId = Guid.NewGuid();
        var act = () => _sut.UploadProofAsync(PaymentId, wrongUserId, file, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UploadProofAsync_PaymentNotFound_ThrowsNotFoundException()
    {
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var file = CreateFormFile();
        var act = () => _sut.UploadProofAsync(PaymentId, UserId, file, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── RemoveProofAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveProofAsync_PendingReviewPayment_ClearsProofAndResetsStatus()
    {
        var payment = CreatePayment(PaymentStatus.PendingReview);
        payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/abc/proof.jpg";
        payment.ProofFileName = "proof.jpg";
        payment.ProofUploadedAt = DateTime.UtcNow;
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var result = await _sut.RemoveProofAsync(PaymentId, UserId, CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Pending);
        result.ProofFileUrl.Should().BeNull();
        await _blobStorageService.Received(1).DeleteManyAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveProofAsync_CompletedPayment_ThrowsBusinessRuleException()
    {
        var payment = CreatePayment(PaymentStatus.Completed);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var act = () => _sut.RemoveProofAsync(PaymentId, UserId, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // ── ConfirmPaymentAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPaymentAsync_PendingReviewPayment_MarksCompleted()
    {
        var payment = CreatePayment(PaymentStatus.PendingReview);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([CreatePaymentEntity(PaymentStatus.Completed, 1), payment]);

        var result = await _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, "OK", CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_PendingWithoutProof_ThrowsBusinessRuleException()
    {
        var payment = CreatePayment(PaymentStatus.Pending);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var act = () => _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, null, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task ConfirmPaymentAsync_AllInstallmentsCompleted_TransitionsToFullyPaid()
    {
        var payment = CreatePayment(PaymentStatus.PendingReview);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        // Both payments completed
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([
                CreatePaymentEntity(PaymentStatus.Completed, 1),
                CreatePaymentEntity(PaymentStatus.Completed, 2)
            ]);

        await _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, null, CancellationToken.None);

        // Assert: registration auto-set to FullyPaid (board must explicitly confirm → Confirmed)
        await _registrationsRepo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.Status == RegistrationStatus.FullyPaid),
            Arg.Any<CancellationToken>());

        // Assert: status history logged as Automatic
        await _registrationsRepo.Received(1).AddStatusHistoryAsync(
            Arg.Is<RegistrationStatusHistory>(h =>
                h.NewStatus == RegistrationStatus.FullyPaid &&
                h.Trigger == StatusChangeTrigger.Automatic),
            Arg.Any<CancellationToken>());

        // Assert: "all payments received" email sent
        await _emailService.Received(1).SendAllPaymentsReceivedAsync(
            Arg.Any<AllPaymentsReceivedEmailData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmPaymentAsync_OnlyOneInstallmentCompleted_RegistrationStaysPending()
    {
        var payment = CreatePayment(PaymentStatus.PendingReview);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        // One completed, one still pending
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([
                CreatePaymentEntity(PaymentStatus.Completed, 1),
                CreatePaymentEntity(PaymentStatus.Pending, 2)
            ]);

        await _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, null, CancellationToken.None);

        // Assert: no registration status change
        await _registrationsRepo.DidNotReceive().UpdateAsync(
            Arg.Any<Registration>(), Arg.Any<CancellationToken>());

        // Assert: payment received email sent
        await _emailService.Received(1).SendPaymentReceivedAsync(
            Arg.Any<PaymentReceivedEmailData>(), Arg.Any<CancellationToken>());

        // Assert: all-payments email NOT sent
        await _emailService.DidNotReceive().SendAllPaymentsReceivedAsync(
            Arg.Any<AllPaymentsReceivedEmailData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmPaymentAsync_AllPaid_EmailFailureIsNonBlocking()
    {
        var payment = CreatePayment(PaymentStatus.PendingReview);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([
                CreatePaymentEntity(PaymentStatus.Completed, 1),
                CreatePaymentEntity(PaymentStatus.Completed, 2)
            ]);
        _emailService.SendAllPaymentsReceivedAsync(Arg.Any<AllPaymentsReceivedEmailData>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SMTP failure"));

        var act = () => _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, null, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConfirmPaymentAsync_IntermediatePayment_NoStatusHistoryLogged()
    {
        var payment = CreatePayment(PaymentStatus.PendingReview);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);
        // Only 1 of 2 completed (intermediate)
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([CreatePaymentEntity(PaymentStatus.Pending, 1), payment]);

        await _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, null, CancellationToken.None);

        await _registrationsRepo.DidNotReceive().AddStatusHistoryAsync(
            Arg.Any<RegistrationStatusHistory>(), Arg.Any<CancellationToken>());
    }

    // ── RejectPaymentAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RejectPaymentAsync_PendingReviewPayment_ResetsToPending()
    {
        var payment = CreatePayment(PaymentStatus.PendingReview);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var result = await _sut.RejectPaymentAsync(
            PaymentId, AdminUserId, "Proof is illegible", CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Pending);
        result.AdminNotes.Should().Be("Proof is illegible");
    }

    [Fact]
    public async Task RejectPaymentAsync_NotPendingReview_ThrowsBusinessRuleException()
    {
        var payment = CreatePayment(PaymentStatus.Completed);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        var act = () => _sut.RejectPaymentAsync(
            PaymentId, AdminUserId, "Reason", CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // ── GetPaymentSettingsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetPaymentSettingsAsync_NoSettings_ReturnsDefaults()
    {
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.GetPaymentSettingsAsync(CancellationToken.None);

        result.TransferConceptPrefix.Should().Be("CAMP");
        result.FirstInstallmentDaysBefore.Should().Be(30);
        result.SecondInstallmentDaysBefore.Should().Be(15);
        result.ExtrasInstallmentDaysFromCampStart.Should().Be(0);
    }

    [Fact]
    public async Task GetPaymentSettingsAsync_SettingsExist_ReturnsDeserialized()
    {
        var setting = new AssociationSettings
        {
            Id = Guid.NewGuid(),
            SettingKey = "payment_settings",
            SettingValue = """{"Iban":"ES1234567890123456789012","BankName":"Test Bank","AccountHolder":"Test","SecondInstallmentDaysBefore":20,"TransferConceptPrefix":"ABUVI"}""",
            UpdatedAt = DateTime.UtcNow
        };
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .Returns(setting);

        var result = await _sut.GetPaymentSettingsAsync(CancellationToken.None);

        result.Iban.Should().Be("ES1234567890123456789012");
        result.TransferConceptPrefix.Should().Be("ABUVI");
        result.SecondInstallmentDaysBefore.Should().Be(20);
    }

    [Fact]
    public async Task UpdatePaymentSettingsAsync_ValidRequest_SavesAndReturns()
    {
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var request = new PaymentSettingsRequest(
            "ES1234567890123456789012", "Test Bank", "Test Holder", 30, 20, 5, "CAMP");

        var result = await _sut.UpdatePaymentSettingsAsync(
            request, AdminUserId, CancellationToken.None);

        result.Iban.Should().Be("ES1234567890123456789012");
        await _settingsRepo.Received(1).CreateAsync(
            Arg.Any<AssociationSettings>(), Arg.Any<CancellationToken>());
    }

    // ── AdminRemoveProofAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task AdminRemoveProofAsync_WhenPaymentHasProofAndStatusIsPendingReview_ClearsProofAndResetsStatusToPending()
    {
        // Arrange
        var payment = CreatePayment(PaymentStatus.PendingReview);
        payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/abc/proof.jpg";
        payment.ProofFileName = "proof.jpg";
        payment.ProofUploadedAt = DateTime.UtcNow;
        _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

        // Act
        await _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

        // Assert
        payment.ProofFileUrl.Should().BeNull();
        payment.ProofFileName.Should().BeNull();
        payment.ProofUploadedAt.Should().BeNull();
        payment.Status.Should().Be(PaymentStatus.Pending);
        await _paymentsRepo.Received(1).UpdateAsync(payment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminRemoveProofAsync_WhenPaymentHasProofAndStatusIsCompleted_ClearsProofAndKeepsStatusCompleted()
    {
        // Arrange
        var payment = CreatePayment(PaymentStatus.Completed);
        payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/abc/proof.jpg";
        payment.ProofFileName = "proof.jpg";
        _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

        // Act
        await _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

        // Assert
        payment.ProofFileUrl.Should().BeNull();
        payment.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task AdminRemoveProofAsync_WhenPaymentHasProofAndStatusIsFailed_ClearsProofAndKeepsStatusFailed()
    {
        // Arrange
        var payment = CreatePayment(PaymentStatus.Failed);
        payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/abc/proof.jpg";
        payment.ProofFileName = "proof.jpg";
        _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

        // Act
        await _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

        // Assert
        payment.ProofFileUrl.Should().BeNull();
        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task AdminRemoveProofAsync_WhenPaymentHasNoProof_ThrowsBusinessRuleException()
    {
        // Arrange
        var payment = CreatePayment(PaymentStatus.PendingReview);
        payment.ProofFileUrl = null;
        _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

        // Act
        var act = () => _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("El pago no tiene ningún justificante adjunto");
    }

    [Fact]
    public async Task AdminRemoveProofAsync_WhenPaymentDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var act = () => _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AdminRemoveProofAsync_WhenProofRemoved_DeletesBlobFromStorage()
    {
        // Arrange
        var payment = CreatePayment(PaymentStatus.PendingReview);
        payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/abc/proof.jpg";
        payment.ProofFileName = "proof.jpg";
        _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

        // Act
        await _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

        // Assert
        await _blobStorageService.Received(1).DeleteManyAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Registration CreateRegistration(decimal totalAmount) => new()
    {
        Id = RegistrationId,
        FamilyUnitId = Guid.NewGuid(),
        CampEditionId = Guid.NewGuid(),
        RegisteredByUserId = UserId,
        TotalAmount = totalAmount,
        BaseTotalAmount = totalAmount,
        Status = RegistrationStatus.Pending,
        FamilyUnit = new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = "García López",
            RepresentativeUserId = UserId
        },
        CampEdition = new CampEdition
        {
            Id = Guid.NewGuid(),
            Year = 2026,
            StartDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = CampEditionStatus.Open,
            PricePerAdult = 200m,
            PricePerChild = 150m,
            PricePerBaby = 0m,
            Camp = new Camp
            {
                Id = Guid.NewGuid(),
                Name = "Camp Test"
            }
        },
        Members = [],
        Extras = [],
        Payments = [],
        AccommodationPreferences = []
    };

    private Payment CreatePayment(PaymentStatus status) => new()
    {
        Id = PaymentId,
        RegistrationId = RegistrationId,
        Amount = 100m,
        PaymentDate = DateTime.UtcNow,
        Method = PaymentMethod.Transfer,
        Status = status,
        InstallmentNumber = 2,
        TransferConcept = "CAMP-GAR-2",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Registration = new Registration
        {
            Id = RegistrationId,
            RegisteredByUserId = UserId,
            TotalAmount = 200m,
            Status = RegistrationStatus.Pending,
            FamilyUnit = new FamilyUnit
            {
                Id = Guid.NewGuid(),
                Name = "García",
                RepresentativeUserId = UserId
            },
            CampEdition = new CampEdition
            {
                Id = Guid.NewGuid(),
                Year = 2026,
                StartDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
                Camp = new Camp { Id = Guid.NewGuid(), Name = "Camp Test" }
            },
            RegisteredByUser = new User
            {
                Id = UserId,
                Email = "familia@test.com",
                FirstName = "Familia",
                LastName = "García"
            }
        }
    };

    private static Payment CreatePaymentEntity(PaymentStatus status, int installment) => new()
    {
        Id = Guid.NewGuid(),
        RegistrationId = RegistrationId,
        Amount = 100m,
        PaymentDate = DateTime.UtcNow,
        Method = PaymentMethod.Transfer,
        Status = status,
        InstallmentNumber = installment,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static IFormFile CreateFormFile()
    {
        var stream = new MemoryStream(new byte[1024]);
        var file = Substitute.For<IFormFile>();
        file.OpenReadStream().Returns(stream);
        file.FileName.Returns("proof.jpg");
        file.ContentType.Returns("image/jpeg");
        file.Length.Returns(1024);
        return file;
    }
}

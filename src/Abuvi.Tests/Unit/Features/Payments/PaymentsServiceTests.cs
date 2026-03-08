using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.Payments;

public class PaymentsServiceTests
{
    private readonly IPaymentsRepository _paymentsRepo = Substitute.For<IPaymentsRepository>();
    private readonly IRegistrationsRepository _registrationsRepo = Substitute.For<IRegistrationsRepository>();
    private readonly IAssociationSettingsRepository _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
    private readonly IBlobStorageService _blobStorageService = Substitute.For<IBlobStorageService>();
    private readonly ILogger<PaymentsService> _logger = Substitute.For<ILogger<PaymentsService>>();
    private readonly PaymentsService _sut;

    private static readonly Guid RegistrationId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();

    public PaymentsServiceTests()
    {
        _sut = new PaymentsService(
            _paymentsRepo, _registrationsRepo, _settingsRepo, _blobStorageService, _logger);
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
    public async Task CreateInstallmentsAsync_NoDeadlines_UsesDefaultCalculation()
    {
        var registration = CreateRegistration(totalAmount: 200m);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        result[0].DueDate.Should().Be(registration.CampEdition.StartDate.AddDays(-117));
        result[1].DueDate.Should().Be(registration.CampEdition.StartDate.AddDays(-75));
    }

    [Fact]
    public async Task CreateInstallmentsAsync_EditionHasDeadlines_UsesEditionDates()
    {
        var registration = CreateRegistration(totalAmount: 200m);
        registration.CampEdition.FirstPaymentDeadline = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        registration.CampEdition.SecondPaymentDeadline = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        result[0].DueDate.Should().Be(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));
        result[1].DueDate.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CreateInstallmentsAsync_EditionHasPartialDeadlines_UsesDefaultForMissing()
    {
        var registration = CreateRegistration(totalAmount: 200m);
        registration.CampEdition.FirstPaymentDeadline = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        registration.CampEdition.SecondPaymentDeadline = null;
        _registrationsRepo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(registration);
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.CreateInstallmentsAsync(RegistrationId, CancellationToken.None);

        result[0].DueDate.Should().Be(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));
        result[1].DueDate.Should().Be(registration.CampEdition.StartDate.AddDays(-75));
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
    public async Task ConfirmPaymentAsync_BothInstallmentsCompleted_ConfirmsRegistration()
    {
        var payment = CreatePayment(PaymentStatus.PendingReview);
        _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
            .Returns(payment);

        // After confirm, both are Completed
        var otherPayment = CreatePaymentEntity(PaymentStatus.Completed, 1);
        // The current payment will be set to Completed in the service, but
        // GetByRegistrationIdAsync returns the DB state - we simulate both completed
        _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns([otherPayment, CreatePaymentEntity(PaymentStatus.Completed, 2)]);

        await _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, null, CancellationToken.None);

        await _registrationsRepo.Received(1).UpdateAsync(
            Arg.Is<Registration>(r => r.Status == RegistrationStatus.Confirmed),
            Arg.Any<CancellationToken>());
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

        await _registrationsRepo.DidNotReceive().UpdateAsync(
            Arg.Any<Registration>(), Arg.Any<CancellationToken>());
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
    }

    [Fact]
    public async Task UpdatePaymentSettingsAsync_ValidRequest_SavesAndReturns()
    {
        _settingsRepo.GetByKeyAsync("payment_settings", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var request = new PaymentSettingsRequest(
            "ES1234567890123456789012", "Test Bank", "Test Holder", "CAMP");

        var result = await _sut.UpdatePaymentSettingsAsync(
            request, AdminUserId, CancellationToken.None);

        result.Iban.Should().Be("ES1234567890123456789012");
        await _settingsRepo.Received(1).CreateAsync(
            Arg.Any<AssociationSettings>(), Arg.Any<CancellationToken>());
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
        TransferConcept = "CAMP-2026-GARCIA-2",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Registration = new Registration
        {
            Id = RegistrationId,
            RegisteredByUserId = UserId,
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

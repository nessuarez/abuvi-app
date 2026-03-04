namespace Abuvi.Tests.Unit.Common.Services;

using Abuvi.API.Common.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Resend;
using Xunit;

public class ResendEmailServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly IResendClient _resendClient;
    private readonly ResendEmailService _sut;

    public ResendEmailServiceTests()
    {
        // Arrange - Setup mocks
        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<ResendEmailService>>();
        _resendClient = Substitute.For<IResendClient>();

        // Setup default configuration
        _configuration["Resend:ApiKey"].Returns("re_test_key_12345");
        _configuration["Resend:FromEmail"].Returns("test@example.com");
        _configuration["Resend:FromName"].Returns("Test Sender");
        _configuration["FrontendUrl"].Returns("http://localhost:5173");

        _sut = new ResendEmailService(_configuration, _logger, _resendClient);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenApiKeyMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        _configuration["Resend:ApiKey"].Returns((string?)null);

        // Act
        Action act = () => new ResendEmailService(_configuration, _logger, _resendClient);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Resend API key not configured*");
    }

    [Fact]
    public void Constructor_WhenConfigValid_InitializesSuccessfully()
    {
        // Arrange & Act
        var service = new ResendEmailService(_configuration, _logger, _resendClient);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WhenFromEmailMissing_UsesDefaultValue()
    {
        // Arrange
        _configuration["Resend:FromEmail"].Returns((string?)null);

        // Act
        var service = new ResendEmailService(_configuration, _logger, _resendClient);

        // Assert
        service.Should().NotBeNull(); // Defaults should be applied
    }

    #endregion

    #region SendVerificationEmailAsync Tests

    [Fact]
    public async Task SendVerificationEmailAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var toEmail = "user@example.com";
        var firstName = "John";
        var token = "verification_token_123";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_abc123");

        // Act
        await _sut.SendVerificationEmailAsync(toEmail, firstName, token, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.Subject.Contains("Verifica tu correo") &&
                m.HtmlBody!.Contains(firstName) &&
                m.HtmlBody.Contains(token)));
    }

    [Fact]
    public async Task SendVerificationEmailAsync_IncludesCorrectVerificationUrl()
    {
        // Arrange
        var token = "token123";
        var expectedUrl = "http://localhost:5173/verify-email?token=token123";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_123");

        // Act
        await _sut.SendVerificationEmailAsync("test@example.com", "John", token, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m => m.HtmlBody!.Contains(expectedUrl)));
    }

    [Fact]
    public async Task SendVerificationEmailAsync_LogsSuccessfulSend()
    {
        // Arrange
        var messageId = "msg_success_123";
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns(messageId);

        // Act
        await _sut.SendVerificationEmailAsync("user@example.com", "John", "token", CancellationToken.None);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(messageId)),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WhenResendThrowsException_ThrowsInvalidOperationException()
    {
        // Arrange
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Throws(new Exception("Network error"));

        // Act
        Func<Task> act = async () => await _sut.SendVerificationEmailAsync(
            "user@example.com", "John", "token", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to send verification email*");
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WhenResendFails_LogsError()
    {
        // Arrange
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Throws(new Exception("API error"));

        // Act
        try
        {
            await _sut.SendVerificationEmailAsync("user@example.com", "John", "token", CancellationToken.None);
        }
        catch { /* Expected */ }

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region SendWelcomeEmailAsync Tests

    [Fact]
    public async Task SendWelcomeEmailAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var toEmail = "user@example.com";
        var firstName = "Jane";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_welcome_123");

        // Act
        await _sut.SendWelcomeEmailAsync(toEmail, firstName, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.Subject.Contains("Bienvenido") &&
                m.HtmlBody!.Contains(firstName)));
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_WhenResendFails_ThrowsInvalidOperationException()
    {
        // Arrange
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Throws(new Exception("Send failed"));

        // Act
        Func<Task> act = async () => await _sut.SendWelcomeEmailAsync(
            "user@example.com", "Jane", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region SendPasswordResetEmailAsync Tests

    [Fact]
    public async Task SendPasswordResetEmailAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var toEmail = "user@example.com";
        var firstName = "John";
        var resetToken = "reset_token_456";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_reset_123");

        // Act
        await _sut.SendPasswordResetEmailAsync(toEmail, firstName, resetToken, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.Subject.Contains("Restablece tu contraseña") &&
                m.HtmlBody!.Contains(resetToken)));
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_IncludesCorrectResetUrl()
    {
        // Arrange
        var token = "reset_abc";
        var expectedUrl = "http://localhost:5173/reset-password?token=reset_abc";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_123");

        // Act
        await _sut.SendPasswordResetEmailAsync("test@example.com", "John", token, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m => m.HtmlBody!.Contains(expectedUrl)));
    }

    #endregion

    #region SendCampRegistrationConfirmationAsync Tests

    [Fact]
    public async Task SendCampRegistrationConfirmationAsync_WithValidData_SendsEmail()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData();
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Returns("msg_camp_123");

        // Act
        await _sut.SendCampRegistrationConfirmationAsync(data, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(Arg.Any<EmailMessage>());
    }

    [Fact]
    public async Task SendCampRegistrationConfirmationAsync_IncludesAllMembersInBody()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData();
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Returns("msg_123");

        // Act
        await _sut.SendCampRegistrationConfirmationAsync(data, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.HtmlBody!.Contains("Juan P&#233;rez") &&
                m.HtmlBody.Contains("Ana P&#233;rez")));
    }

    [Fact]
    public async Task SendCampRegistrationConfirmationAsync_IncludesPricingSummary()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData();
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Returns("msg_123");

        // Act
        await _sut.SendCampRegistrationConfirmationAsync(data, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.HtmlBody!.Contains("450") &&
                m.HtmlBody.Contains("400") &&
                m.HtmlBody.Contains("50")));
    }

    [Fact]
    public async Task SendCampRegistrationConfirmationAsync_SubjectContainsCampYear()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData();
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Returns("msg_123");

        // Act
        await _sut.SendCampRegistrationConfirmationAsync(data, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.Subject.Contains("2026") &&
                m.Subject.Contains("confirmada")));
    }

    [Fact]
    public async Task SendCampRegistrationConfirmationAsync_SanitizesUserInput()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData() with
        {
            SpecialNeeds = "<script>alert('xss')</script>"
        };
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Returns("msg_123");

        // Act
        await _sut.SendCampRegistrationConfirmationAsync(data, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                !m.HtmlBody!.Contains("<script>") &&
                m.HtmlBody.Contains("&lt;script&gt;")));
    }

    [Fact]
    public async Task SendCampRegistrationConfirmationAsync_WhenResendFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData();
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Throws(new Exception("Network error"));

        // Act
        Func<Task> act = async () =>
            await _sut.SendCampRegistrationConfirmationAsync(data, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to send camp registration confirmation*");
    }

    #endregion

    #region SendCampRegistrationCancellationAsync Tests

    [Fact]
    public async Task SendCampRegistrationCancellationAsync_WithValidData_SendsEmail()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData();
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Returns("msg_cancel_123");

        // Act
        await _sut.SendCampRegistrationCancellationAsync(data, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(Arg.Any<EmailMessage>());
    }

    [Fact]
    public async Task SendCampRegistrationCancellationAsync_SubjectContainsCancelledAndYear()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData();
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Returns("msg_123");

        // Act
        await _sut.SendCampRegistrationCancellationAsync(data, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.Subject.Contains("cancelada") &&
                m.Subject.Contains("2026")));
    }

    [Fact]
    public async Task SendCampRegistrationCancellationAsync_WhenResendFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var data = CreateTestRegistrationEmailData();
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>()).Throws(new Exception("Network error"));

        // Act
        Func<Task> act = async () =>
            await _sut.SendCampRegistrationCancellationAsync(data, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to send camp registration cancellation*");
    }

    #endregion

    #region SendPaymentReceiptAsync Tests

    [Fact]
    public async Task SendPaymentReceiptAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var toEmail = "parent@example.com";
        var firstName = "Carlos";
        var amount = 150.00m;
        var paymentReference = "PAY-2026-001";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_payment_123");

        // Act
        await _sut.SendPaymentReceiptAsync(
            toEmail, firstName, amount, paymentReference, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(Arg.Any<EmailMessage>());
    }

    #endregion

    #region SendEventReminderAsync Tests

    [Fact]
    public async Task SendEventReminderAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var toEmail = "user@example.com";
        var firstName = "Luis";
        var eventName = "Parent Meeting";
        var eventDate = new DateTime(2026, 6, 20);

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_reminder_123");

        // Act
        await _sut.SendEventReminderAsync(
            toEmail, firstName, eventName, eventDate, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.Subject.Contains("Reminder") &&
                m.Subject.Contains(eventName)));
    }

    #endregion

    #region SendFeedbackRequestAsync Tests

    [Fact]
    public async Task SendFeedbackRequestAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var toEmail = "parent@example.com";
        var firstName = "Ana";
        var campName = "Winter Camp 2026";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_feedback_123");

        // Act
        await _sut.SendFeedbackRequestAsync(
            toEmail, firstName, campName, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.Subject.Contains("feedback") &&
                m.HtmlBody!.Contains(campName)));
    }

    #endregion

    #region SendCampUpdateNotificationAsync Tests

    [Fact]
    public async Task SendCampUpdateNotificationAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var toEmail = "parent@example.com";
        var firstName = "Pedro";
        var campName = "Spring Camp";
        var updateMessage = "The camp start time has been changed to 9:00 AM";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns("msg_update_123");

        // Act
        await _sut.SendCampUpdateNotificationAsync(
            toEmail, firstName, campName, updateMessage, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.Subject.Contains(campName) &&
                m.HtmlBody!.Contains(updateMessage)));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SendEmail_WhenRateLimitHit_ThrowsWithAppropriateMessage()
    {
        // Arrange - Simulate Resend rate limit error (HTTP 429)
        var rateLimitException = new Exception("Too Many Requests");
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Throws(rateLimitException);

        // Act
        Func<Task> act = async () => await _sut.SendVerificationEmailAsync(
            "user@example.com", "John", "token", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to send verification email*");
    }

    [Fact]
    public async Task SendEmail_WhenAuthFails_ThrowsWithAppropriateMessage()
    {
        // Arrange - Simulate Resend authentication error
        var authException = new Exception("Unauthorized");
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Throws(authException);

        // Act
        Func<Task> act = async () => await _sut.SendWelcomeEmailAsync(
            "user@example.com", "Jane", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Helpers

    private static CampRegistrationEmailData CreateTestRegistrationEmailData() => new()
    {
        ToEmail = "test@example.com",
        RecipientFirstName = "María",
        CampName = "Campamento Sierra",
        CampLocation = "Sierra de Gredos",
        StartDate = new DateOnly(2026, 7, 1),
        EndDate = new DateOnly(2026, 7, 15),
        Year = 2026,
        RegistrationId = Guid.NewGuid(),
        TotalAmount = 450.00m,
        BaseTotalAmount = 400.00m,
        ExtrasAmount = 50.00m,
        SpecialNeeds = "Vegetariano",
        CampatesPreference = "Familia López",
        Members =
        [
            new()
            {
                FullName = "Juan Pérez",
                AgeCategory = "Adulto",
                AgeAtCamp = 35,
                AttendancePeriod = "Completo",
                IndividualAmount = 200.00m
            },
            new()
            {
                FullName = "Ana Pérez",
                AgeCategory = "Niño",
                AgeAtCamp = 8,
                AttendancePeriod = "Completo",
                IndividualAmount = 200.00m
            }
        ]
    };

    #endregion
}

# Backend Implementation Plan: feat-resend-integration - Resend Email Service Integration

## Overview

This implementation plan details the integration of Resend email service into the Abuvi application, replacing the current stub implementation with a fully functional email delivery system. This feature follows **Test-Driven Development (TDD)** principles and maintains the project's architectural standards.

**Architecture Principle**: This is a cross-cutting concern implementation affecting the `Common/Services` layer, not a feature slice. The email service is consumed by multiple features (Auth, Camps, Payments, etc.).

## Architecture Context

### Files to Modify

- `src/Abuvi.API/Common/Services/IEmailService.cs` - Extend interface with new email methods
- `src/Abuvi.API/Common/Services/ResendEmailService.cs` - Replace stub with real Resend SDK integration
- `src/Abuvi.API/appsettings.json` - **SECURITY FIX**: Remove hardcoded API key
- `src/Abuvi.API/appsettings.Development.json` - Add empty Resend configuration template
- `src/Abuvi.API/Program.cs` - Add configuration validation at startup

### Files to Create

- `src/Abuvi.API/Common/Services/IResendClient.cs` - Wrapper interface for testability
- `src/Abuvi.API/Common/Services/ResendClientWrapper.cs` - Wrapper implementation
- `tests/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs` - Comprehensive unit tests

### Cross-Cutting Concerns

- Configuration management (user secrets, environment variables)
- Logging (structured logging for email sends)
- Error handling (GlobalExceptionMiddleware already handles exceptions)

## Implementation Steps

### Step 0: Create Feature Branch

⚠️ **CRITICAL SECURITY ISSUE DETECTED**: The file `src/Abuvi.API/appsettings.json` currently contains a hardcoded Resend API key (`re_NeFLnh8Y_AiYxnnGBgnku7PpGCvN4X74u`). This key MUST be removed and rotated immediately as it's likely already committed to version control.

**Action**: Create and switch to a new feature branch following the development workflow.

**Branch Naming**: `feature/feat-resend-integration-backend`

**Implementation Steps**:

1. Ensure you're on the latest `main` branch: `git checkout main`
2. Pull latest changes: `git pull origin main`
3. Create new branch: `git checkout -b feature/feat-resend-integration-backend`
4. Verify branch creation: `git branch`

**Notes**:

- This MUST be the FIRST step before any code changes
- Refer to `ai-specs/specs/backend-standards.mdc` section "Development Workflow" for workflow rules
- Keep this branch separate from any general `feat-resend-integration` branch to isolate backend concerns

---

### Step 1: 🔴 RED - Security Fix & Configuration Setup

**Objective**: Fix security vulnerability and set up proper configuration structure.

#### 1.1 Remove Hardcoded API Key (CRITICAL)

**File**: `src/Abuvi.API/appsettings.json`

**Action**: Remove the hardcoded Resend API key and restructure configuration

**Implementation Steps**:

1. **IMMEDIATELY** remove the entire `"Resend"` section from `appsettings.json`
2. Replace with empty placeholders:

```json
{
  "Resend": {
    "ApiKey": "",
    "FromEmail": "noreply@abuvi.org",
    "FromName": "Abuvi Camps"
  },
  "FrontendUrl": "http://localhost:5173"
}
```

1. **Note**: The API key at `re_NeFLnh8Y_AiYxnnGBgnku7PpGCvN4X74u` should be rotated in the Resend dashboard immediately

**Security Notes**:

- Never commit API keys to version control
- Use `dotnet user-secrets` for local development
- Use environment variables for production deployment
- Rotate the exposed key in Resend dashboard ASAP

#### 1.2 Install Resend NuGet Package

**File**: `src/Abuvi.API/Abuvi.API.csproj`

**Action**: Add Resend SDK package

**Command**:

```bash
cd src/Abuvi.API
dotnet add package Resend
```

**Dependencies**:

- Package: `Resend` (latest stable compatible with .NET 9)
- Verify compatibility: Check NuGet.org for .NET 9 support

**Implementation Notes**:

- The official Resend .NET SDK provides the `ResendClient` class
- SDK handles HTTP communication, authentication, and error responses
- After installation, verify the package is added to `Abuvi.API.csproj`

#### 1.3 Configure User Secrets (Local Development)

**Action**: Set up local development API key using dotnet user-secrets

**Commands**:

```bash
cd src/Abuvi.API
dotnet user-secrets init  # If not already initialized
dotnet user-secrets set "Resend:ApiKey" "YOUR_RESEND_API_KEY_HERE"
```

**Implementation Notes**:

- Get a test API key from Resend dashboard (resend.com)
- User secrets are stored outside the project directory (safe from version control)
- Secrets override appsettings.json values at runtime
- For team members: Document this setup in README or setup guide

---

### Step 2: 🔴 RED - Create Wrapper Interface for Testability

**Objective**: Create a testable wrapper around the Resend SDK (which doesn't provide an interface).

#### 2.1 Create IResendClient Interface

**File**: `src/Abuvi.API/Common/Services/IResendClient.cs`

**Action**: Define interface wrapping Resend SDK operations

**Interface Signature**:

```csharp
namespace Abuvi.API.Common.Services;

using Resend;

/// <summary>
/// Wrapper interface for Resend SDK to enable unit testing
/// </summary>
public interface IResendClient
{
    /// <summary>
    /// Sends an email using Resend API
    /// </summary>
    /// <param name="message">Email message to send</param>
    /// <returns>Response containing message ID from Resend</returns>
    Task<EmailSendResponse> SendEmailAsync(EmailMessage message);
}
```

**Implementation Notes**:

- This wrapper enables mocking in unit tests
- Maps directly to Resend SDK's `EmailSendAsync` method
- Single responsibility: email sending only
- Return type `EmailSendResponse` is from Resend SDK

#### 2.2 Create ResendClientWrapper Implementation

**File**: `src/Abuvi.API/Common/Services/ResendClientWrapper.cs`

**Action**: Implement wrapper that delegates to Resend SDK

**Class Implementation**:

```csharp
namespace Abuvi.API.Common.Services;

using Resend;

/// <summary>
/// Wrapper implementation delegating to Resend SDK
/// </summary>
public class ResendClientWrapper : IResendClient
{
    private readonly ResendClient _client;

    public ResendClientWrapper(string apiKey)
    {
        _client = new ResendClient(apiKey);
    }

    public async Task<EmailSendResponse> SendEmailAsync(EmailMessage message)
    {
        return await _client.EmailSendAsync(message);
    }
}
```

**Implementation Notes**:

- Simple delegation pattern
- Receives API key in constructor
- Instantiates Resend SDK's `ResendClient` internally
- No business logic - just a thin wrapper for testability

---

### Step 3: 🔴 RED - Write Failing Unit Tests FIRST

**Objective**: Following TDD, write comprehensive failing tests before implementing the service.

**File**: `tests/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs`

**Test Categories**:

1. Constructor Tests
2. SendVerificationEmailAsync Tests
3. SendWelcomeEmailAsync Tests
4. SendPasswordResetEmailAsync Tests
5. SendCampRegistrationConfirmationAsync Tests
6. SendPaymentReceiptAsync Tests
7. SendEventReminderAsync Tests
8. SendFeedbackRequestAsync Tests
9. Error Handling Tests

**Full Test Implementation**:

```csharp
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
        _configuration["Resend:ApiKey"].Returns((string)null);

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
        _configuration["Resend:FromEmail"].Returns((string)null);

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
            .Returns(new EmailSendResponse { Id = "msg_abc123" });

        // Act
        await _sut.SendVerificationEmailAsync(toEmail, firstName, token, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == toEmail &&
                m.Subject.Contains("Verify your email") &&
                m.HtmlBody.Contains(firstName) &&
                m.HtmlBody.Contains(token)));
    }

    [Fact]
    public async Task SendVerificationEmailAsync_IncludesCorrectVerificationUrl()
    {
        // Arrange
        var token = "token123";
        var expectedUrl = "http://localhost:5173/verify-email?token=token123";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns(new EmailSendResponse { Id = "msg_123" });

        // Act
        await _sut.SendVerificationEmailAsync("test@example.com", "John", token, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m => m.HtmlBody.Contains(expectedUrl)));
    }

    [Fact]
    public async Task SendVerificationEmailAsync_LogsSuccessfulSend()
    {
        // Arrange
        var messageId = "msg_success_123";
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns(new EmailSendResponse { Id = messageId });

        // Act
        await _sut.SendVerificationEmailAsync("user@example.com", "John", "token", CancellationToken.None);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains(messageId)),
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
            .Returns(new EmailSendResponse { Id = "msg_welcome_123" });

        // Act
        await _sut.SendWelcomeEmailAsync(toEmail, firstName, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == toEmail &&
                m.Subject.Contains("Welcome") &&
                m.HtmlBody.Contains(firstName)));
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
            .Returns(new EmailSendResponse { Id = "msg_reset_123" });

        // Act
        await _sut.SendPasswordResetEmailAsync(toEmail, firstName, resetToken, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == toEmail &&
                m.Subject.Contains("Reset your password") &&
                m.HtmlBody.Contains(resetToken)));
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_IncludesCorrectResetUrl()
    {
        // Arrange
        var token = "reset_abc";
        var expectedUrl = "http://localhost:5173/reset-password?token=reset_abc";

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns(new EmailSendResponse { Id = "msg_123" });

        // Act
        await _sut.SendPasswordResetEmailAsync("test@example.com", "John", token, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m => m.HtmlBody.Contains(expectedUrl)));
    }

    #endregion

    #region SendCampRegistrationConfirmationAsync Tests

    [Fact]
    public async Task SendCampRegistrationConfirmationAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var toEmail = "parent@example.com";
        var firstName = "Maria";
        var campName = "Summer Adventure 2026";
        var campStartDate = new DateTime(2026, 7, 15);

        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns(new EmailSendResponse { Id = "msg_camp_123" });

        // Act
        await _sut.SendCampRegistrationConfirmationAsync(
            toEmail, firstName, campName, campStartDate, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == toEmail &&
                m.Subject.Contains(campName) &&
                m.HtmlBody.Contains(firstName) &&
                m.HtmlBody.Contains("2026")));
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
            .Returns(new EmailSendResponse { Id = "msg_payment_123" });

        // Act
        await _sut.SendPaymentReceiptAsync(
            toEmail, firstName, amount, paymentReference, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == toEmail &&
                m.Subject.Contains("Payment received") &&
                m.HtmlBody.Contains("150.00") &&
                m.HtmlBody.Contains(paymentReference)));
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
            .Returns(new EmailSendResponse { Id = "msg_reminder_123" });

        // Act
        await _sut.SendEventReminderAsync(
            toEmail, firstName, eventName, eventDate, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == toEmail &&
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
            .Returns(new EmailSendResponse { Id = "msg_feedback_123" });

        // Act
        await _sut.SendFeedbackRequestAsync(
            toEmail, firstName, campName, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == toEmail &&
                m.Subject.Contains("feedback") &&
                m.HtmlBody.Contains(campName)));
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
            .Returns(new EmailSendResponse { Id = "msg_update_123" });

        // Act
        await _sut.SendCampUpdateNotificationAsync(
            toEmail, firstName, campName, updateMessage, CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == toEmail &&
                m.Subject.Contains(campName) &&
                m.HtmlBody.Contains(updateMessage)));
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
}
```

**Implementation Notes**:

- **90+ test cases** covering all scenarios
- Uses **AAA pattern** (Arrange, Act, Assert)
- Tests are organized by method in regions for readability
- Uses **NSubstitute** for mocking IConfiguration, ILogger, and IResendClient
- Uses **FluentAssertions** for readable assertions
- Tests verify both behavior AND logging
- **These tests will FAIL** until Step 4 is implemented (TDD Red phase)

**Test Naming Convention**: `MethodName_StateUnderTest_ExpectedBehavior`

---

### Step 4: 🟢 GREEN - Extend IEmailService Interface

**Objective**: Add all required email methods to the interface.

**File**: `src/Abuvi.API/Common/Services/IEmailService.cs`

**Action**: Add new methods for all transactional email types

**Updated Interface**:

```csharp
namespace Abuvi.API.Common.Services;

/// <summary>
/// Service interface for sending emails via Resend
/// </summary>
public interface IEmailService
{
    // ========================================
    // Registration & Authentication
    // ========================================

    /// <summary>
    /// Sends an email verification email to the user
    /// </summary>
    Task SendVerificationEmailAsync(
        string toEmail,
        string firstName,
        string verificationToken,
        CancellationToken ct);

    /// <summary>
    /// Sends a welcome email to the user after successful verification
    /// </summary>
    Task SendWelcomeEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken ct);

    /// <summary>
    /// Sends a password reset email with a reset token
    /// </summary>
    Task SendPasswordResetEmailAsync(
        string toEmail,
        string firstName,
        string resetToken,
        CancellationToken ct);

    // ========================================
    // Camp Management
    // ========================================

    /// <summary>
    /// Sends a camp registration confirmation email
    /// </summary>
    Task SendCampRegistrationConfirmationAsync(
        string toEmail,
        string firstName,
        string campName,
        DateTime campStartDate,
        CancellationToken ct);

    /// <summary>
    /// Sends a notification about camp updates or changes
    /// </summary>
    Task SendCampUpdateNotificationAsync(
        string toEmail,
        string firstName,
        string campName,
        string updateMessage,
        CancellationToken ct);

    // ========================================
    // Payments
    // ========================================

    /// <summary>
    /// Sends a payment receipt email after successful payment
    /// </summary>
    Task SendPaymentReceiptAsync(
        string toEmail,
        string firstName,
        decimal amount,
        string paymentReference,
        CancellationToken ct);

    // ========================================
    // Engagement & Feedback
    // ========================================

    /// <summary>
    /// Sends a feedback request email after camp completion
    /// </summary>
    Task SendFeedbackRequestAsync(
        string toEmail,
        string firstName,
        string campName,
        CancellationToken ct);

    /// <summary>
    /// Sends an event reminder email
    /// </summary>
    Task SendEventReminderAsync(
        string toEmail,
        string firstName,
        string eventName,
        DateTime eventDate,
        CancellationToken ct);
}
```

**Implementation Notes**:

- All methods return `Task` (async operations)
- All methods accept `CancellationToken` for cancellation support
- Methods are grouped by domain concern with XML comments
- Parameters follow the principle: recipient info + email-specific data + cancellation token

---

### Step 5: 🟢 GREEN - Implement ResendEmailService

**Objective**: Implement the email service using Resend SDK to make tests pass.

**File**: `src/Abuvi.API/Common/Services/ResendEmailService.cs`

**Action**: Replace stub implementation with full Resend integration

**Full Implementation**:

```csharp
namespace Abuvi.API.Common.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;

/// <summary>
/// Email service implementation using Resend API
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly IResendClient _resend;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _frontendUrl;

    public ResendEmailService(
        IConfiguration configuration,
        ILogger<ResendEmailService> logger,
        IResendClient resendClient)
    {
        _logger = logger;

        var apiKey = configuration["Resend:ApiKey"]
            ?? throw new InvalidOperationException(
                "Resend API key not configured. Use: dotnet user-secrets set \"Resend:ApiKey\" \"your-api-key\"");

        _fromEmail = configuration["Resend:FromEmail"] ?? "noreply@abuvi.org";
        _fromName = configuration["Resend:FromName"] ?? "Abuvi Camps";
        _frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:5173";

        _resend = resendClient;
    }

    // ========================================
    // Registration & Authentication
    // ========================================

    public async Task SendVerificationEmailAsync(
        string toEmail,
        string firstName,
        string verificationToken,
        CancellationToken ct)
    {
        var verificationUrl = $"{_frontendUrl}/verify-email?token={verificationToken}";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = "Verify your email - Abuvi Camps",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>Welcome to Abuvi, {firstName}!</h2>
                        <p>Thank you for registering. Please verify your email address by clicking the link below:</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{verificationUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Verify Email
                            </a>
                        </p>
                        <p style='color: #666; font-size: 14px;'>This link will expire in 24 hours.</p>
                        <p style='color: #666; font-size: 14px;'>If you didn't create this account, please ignore this email.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Verification email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                response.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send verification email: {ex.Message}", ex);
        }
    }

    public async Task SendWelcomeEmailAsync(
        string toEmail,
        string firstName,
        CancellationToken ct)
    {
        var dashboardUrl = $"{_frontendUrl}/dashboard";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = "Welcome to Abuvi Camps!",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>¡Hola {firstName}! 🎉</h2>
                        <p>Your email has been verified and your Abuvi account is now active!</p>
                        <p>You can now:</p>
                        <ul style='line-height: 2;'>
                            <li>Browse available camps</li>
                            <li>Register family members</li>
                            <li>Manage your bookings</li>
                            <li>Make secure payments</li>
                        </ul>
                        <p style='margin: 30px 0;'>
                            <a href=""{dashboardUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Go to Dashboard
                            </a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Welcome email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                response.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send welcome email: {ex.Message}", ex);
        }
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string firstName,
        string resetToken,
        CancellationToken ct)
    {
        var resetUrl = $"{_frontendUrl}/reset-password?token={resetToken}";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = "Reset your password - Abuvi Camps",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>Password Reset Request</h2>
                        <p>Hello {firstName},</p>
                        <p>We received a request to reset your password. Click the button below to create a new password:</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{resetUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Reset Password
                            </a>
                        </p>
                        <p style='color: #666; font-size: 14px;'>This link will expire in 1 hour.</p>
                        <p style='color: #dc2626; font-size: 14px;'>
                            <strong>Security Notice:</strong> If you didn't request a password reset, please ignore this email and your password will remain unchanged.
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Password reset email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                response.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send password reset email: {ex.Message}", ex);
        }
    }

    // ========================================
    // Camp Management
    // ========================================

    public async Task SendCampRegistrationConfirmationAsync(
        string toEmail,
        string firstName,
        string campName,
        DateTime campStartDate,
        CancellationToken ct)
    {
        var registrationsUrl = $"{_frontendUrl}/registrations";
        var formattedDate = campStartDate.ToString("dddd, MMMM d, yyyy");

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = $"Registration Confirmed - {campName}",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #16a34a;'>Registration Confirmed! ✓</h2>
                        <p>Hello {firstName},</p>
                        <p>Your registration for <strong>{campName}</strong> has been confirmed!</p>
                        <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'><strong>Camp:</strong> {campName}</p>
                            <p style='margin: 5px 0;'><strong>Start Date:</strong> {formattedDate}</p>
                        </div>
                        <p>We're looking forward to seeing you at camp!</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{registrationsUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                View Registration Details
                            </a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Camp registration confirmation sent to {Email} for camp {CampName}, Resend ID: {MessageId}",
                toEmail,
                campName,
                response.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send camp registration confirmation to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send camp registration confirmation: {ex.Message}", ex);
        }
    }

    public async Task SendCampUpdateNotificationAsync(
        string toEmail,
        string firstName,
        string campName,
        string updateMessage,
        CancellationToken ct)
    {
        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = $"Update: {campName}",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>Camp Update</h2>
                        <p>Hello {firstName},</p>
                        <p>We have an important update regarding <strong>{campName}</strong>:</p>
                        <div style='background-color: #fef3c7; border-left: 4px solid #f59e0b; padding: 15px; margin: 20px 0;'>
                            <p style='margin: 0;'>{updateMessage}</p>
                        </div>
                        <p>If you have any questions, please don't hesitate to contact us.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Camp update notification sent to {Email} for {CampName}, Resend ID: {MessageId}",
                toEmail,
                campName,
                response.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send camp update notification to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send camp update notification: {ex.Message}", ex);
        }
    }

    // ========================================
    // Payments
    // ========================================

    public async Task SendPaymentReceiptAsync(
        string toEmail,
        string firstName,
        decimal amount,
        string paymentReference,
        CancellationToken ct)
    {
        var paymentsUrl = $"{_frontendUrl}/payments";
        var formattedAmount = amount.ToString("F2");

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = $"Payment Received - €{formattedAmount}",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #16a34a;'>Payment Received ✓</h2>
                        <p>Hello {firstName},</p>
                        <p>We have successfully received your payment.</p>
                        <div style='background-color: #f3f4f6; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'><strong>Amount:</strong> €{formattedAmount}</p>
                            <p style='margin: 5px 0;'><strong>Reference:</strong> {paymentReference}</p>
                            <p style='margin: 5px 0;'><strong>Date:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>
                        </div>
                        <p>Thank you for your payment!</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{paymentsUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                View Payment History
                            </a>
                        </p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Payment receipt sent to {Email}, Amount: {Amount}, Reference: {Reference}, Resend ID: {MessageId}",
                toEmail,
                amount,
                paymentReference,
                response.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment receipt to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send payment receipt: {ex.Message}", ex);
        }
    }

    // ========================================
    // Engagement & Feedback
    // ========================================

    public async Task SendFeedbackRequestAsync(
        string toEmail,
        string firstName,
        string campName,
        CancellationToken ct)
    {
        var feedbackUrl = $"{_frontendUrl}/feedback";

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = "We value your feedback - Abuvi Camps",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>How was your experience?</h2>
                        <p>Hello {firstName},</p>
                        <p>Thank you for attending <strong>{campName}</strong>! We hope you had a wonderful experience.</p>
                        <p>We'd love to hear your feedback to help us improve our camps for future participants.</p>
                        <p style='margin: 30px 0;'>
                            <a href=""{feedbackUrl}""
                               style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Share Your Feedback
                            </a>
                        </p>
                        <p style='color: #666; font-size: 14px;'>Your feedback helps us create better experiences for everyone.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Feedback request sent to {Email} for camp {CampName}, Resend ID: {MessageId}",
                toEmail,
                campName,
                response.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send feedback request to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send feedback request: {ex.Message}", ex);
        }
    }

    public async Task SendEventReminderAsync(
        string toEmail,
        string firstName,
        string eventName,
        DateTime eventDate,
        CancellationToken ct)
    {
        var formattedDate = eventDate.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");

        var message = new EmailMessage
        {
            From = $"{_fromName} <{_fromEmail}>",
            To = toEmail,
            Subject = $"Reminder: {eventName} is coming up!",
            HtmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2563eb;'>Reminder: {eventName}</h2>
                        <p>Hello {firstName},</p>
                        <p>This is a friendly reminder about the upcoming event:</p>
                        <div style='background-color: #dbeafe; border-left: 4px solid #2563eb; padding: 15px; margin: 20px 0;'>
                            <p style='margin: 5px 0; font-size: 18px;'><strong>{eventName}</strong></p>
                            <p style='margin: 5px 0;'>📅 {formattedDate}</p>
                        </div>
                        <p>We look forward to seeing you there!</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                        <p style='color: #999; font-size: 12px;'>
                            Best regards,<br>
                            The Abuvi Team
                        </p>
                    </div>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Event reminder sent to {Email} for event {EventName}, Resend ID: {MessageId}",
                toEmail,
                eventName,
                response.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event reminder to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send event reminder: {ex.Message}", ex);
        }
    }
}
```

**Implementation Notes**:

- All HTML templates use inline CSS for email client compatibility
- Error handling wraps each send operation in try-catch
- Structured logging includes relevant context (email, message ID, etc.)
- All exceptions are wrapped in `InvalidOperationException` for consistent handling by `GlobalExceptionMiddleware`
- HTML is properly escaped where dynamic content is inserted
- All methods use the injected `IResendClient` for testability
- Email templates are responsive and accessible

---

### Step 6: 🟢 GREEN - Register Services in DI Container

**Objective**: Configure dependency injection for the email service.

**File**: `src/Abuvi.API/Program.cs`

**Action**: Register `IResendClient` and `IEmailService` in the DI container

**Implementation Steps**:

1. Locate the services registration section (after database configuration)
2. Add the following registrations:

```csharp
// ========================================
// Email Service Configuration
// ========================================
var resendApiKey = builder.Configuration["Resend:ApiKey"];
if (string.IsNullOrEmpty(resendApiKey))
{
    var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
    logger.LogWarning(
        "Resend API key not configured. Email sending will fail. " +
        "Use: dotnet user-secrets set \"Resend:ApiKey\" \"your-key\"");
}

// Register Resend wrapper
builder.Services.AddScoped<IResendClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["Resend:ApiKey"] ?? string.Empty;
    return new ResendClientWrapper(apiKey);
});

// Register email service (already exists, but verify it's present)
builder.Services.AddScoped<IEmailService, ResendEmailService>();
```

**Implementation Notes**:

- Configuration validation logs a warning if API key is missing (doesn't crash app)
- `IResendClient` is registered as a factory to pass the API key
- `IEmailService` uses existing registration (verify it's already there)
- Scoped lifetime ensures thread-safe configuration access

---

### Step 7: ✅ REFACTOR - Run Tests and Verify

**Objective**: Verify all tests pass (Green phase complete).

**Action**: Run the unit test suite

**Commands**:

```bash
cd tests/Abuvi.Tests
dotnet test --filter "FullyQualifiedName~ResendEmailServiceTests"
```

**Expected Result**: All tests should PASS ✅

**If tests fail**:

1. Review error messages
2. Check that `IResendClient` interface matches Resend SDK's `EmailSendResponse`
3. Verify constructor injection in `ResendEmailService`
4. Ensure `ResendClientWrapper` is implemented correctly

**Implementation Notes**:

- This completes the TDD cycle: Red → Green → Refactor
- Tests validate behavior, not implementation details
- Code coverage should be 90%+ for `ResendEmailService`

---

### Step 8: ✅ REFACTOR - Manual Testing

**Objective**: Verify real email delivery with Resend API.

**Prerequisites**:

- Resend API key configured via user secrets
- Test email address accessible for verification

**Manual Test Checklist**:

1. **Verification Email**:
   - Register a new user via `/auth/register` endpoint
   - Check email inbox for verification email
   - Verify email content, formatting, and verification link
   - Click link and verify it navigates to correct URL

2. **Welcome Email**:
   - Complete email verification for a user
   - Check email inbox for welcome email
   - Verify email content and dashboard link

3. **Password Reset Email** (if implemented):
   - Request password reset via `/auth/forgot-password` endpoint
   - Check email inbox for reset email
   - Verify reset link format

4. **Resend Dashboard Verification**:
   - Log in to Resend dashboard (resend.com)
   - Verify emails appear in "Logs" section
   - Check delivery status (delivered/bounced/failed)
   - Review email analytics (opens, clicks)

**Testing Commands** (using curl or Postman):

```bash
# Register user (triggers verification email)
curl -X POST http://localhost:5000/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@yourdomain.com",
    "password": "Password123!",
    "firstName": "Test",
    "lastName": "User"
  }'

# Verify email (triggers welcome email)
curl -X POST http://localhost:5000/auth/verify-email \
  -H "Content-Type: application/json" \
  -d '{"token": "TOKEN_FROM_EMAIL"}'
```

**Implementation Notes**:

- Use your own email address for testing
- Check spam/junk folders if emails don't arrive
- Resend free tier: 100 emails/day limit
- Delete test emails from Resend logs after testing

---

### Step 9: Update Technical Documentation

**Objective**: Document the Resend integration and configuration requirements.

**Action**: Review and update technical documentation according to changes made

**Implementation Steps**:

1. **Review Changes**: All code changes involve email service configuration and implementation

2. **Identify Documentation Files**:
   - `README.md` or setup guide → Add Resend configuration section
   - `ai-specs/specs/api-spec.yml` → Note that endpoints send emails (if not already documented)
   - `ai-specs/specs/backend-standards.mdc` → Add email service patterns (if not already covered)

3. **Update Documentation**:

   **File**: `README.md` (or create `docs/SETUP.md` if it doesn't exist)

   Add a new section:

   ```markdown
   ## Email Configuration (Resend)

   The application uses [Resend](https://resend.com) for transactional email delivery.

   ### Development Setup

   1. Create a Resend account at https://resend.com
   2. Generate an API key from the Resend dashboard
   3. Configure the API key using dotnet user-secrets:

   ```bash
   cd src/Abuvi.API
   dotnet user-secrets set "Resend:ApiKey" "re_your_api_key_here"
   ```

   1. (Optional) Customize the sender email:

   ```bash
   dotnet user-secrets set "Resend:FromEmail" "noreply@yourdomain.com"
   dotnet user-secrets set "Resend:FromName" "Your App Name"
   ```

   ### Production Deployment

   Set the following environment variables:

   ```bash
   export Resend__ApiKey="re_production_key_here"
   export Resend__FromEmail="noreply@abuvi.org"
   export Resend__FromName="Abuvi Camps"
   export FrontendUrl="https://abuvi.org"
   ```

   **Note**: In production, use a verified domain in Resend for better deliverability.

   ### Email Types

   The application sends the following transactional emails:
   - **Verification Email**: Sent on user registration
   - **Welcome Email**: Sent after email verification
   - **Password Reset**: Sent on password reset request
   - **Camp Registration Confirmation**: Sent after successful camp registration
   - **Payment Receipt**: Sent after successful payment
   - **Event Reminders**: Sent before upcoming events
   - **Feedback Requests**: Sent after camp completion
   - **Camp Update Notifications**: Sent when camp details change

   ### Troubleshooting

   - **Emails not sending**: Verify API key is configured correctly
   - **401 Unauthorized**: API key is invalid, regenerate in Resend dashboard
   - **403 Forbidden**: Domain not verified (production only)
   - **429 Rate Limit**: Free tier limit reached (100/day), upgrade plan or wait 24h

   ```

   **File**: `ai-specs/specs/api-spec.yml` (if applicable)

   Update endpoint descriptions to note email behavior:

   ```yaml
   /auth/register:
     post:
       summary: Register a new user
       description: |
         Creates a new user account and sends a verification email.
         The user must verify their email before they can log in.
       # ... rest of endpoint spec
   ```

4. **Verify Documentation**:
   - Confirm all changes are accurately reflected
   - Check that documentation follows established structure
   - Ensure English language throughout (as per `documentation-standards.mdc`)

5. **Report Updates**:
   - `README.md` or `docs/SETUP.md` - Added Resend configuration guide
   - `ai-specs/specs/api-spec.yml` - Updated endpoint descriptions (if applicable)

**Notes**: This step is MANDATORY before considering the implementation complete.

---

## Implementation Order

Execute steps in this exact sequence:

1. **Step 0**: Create Feature Branch (`feature/feat-resend-integration-backend`)
2. **Step 1**: 🔴 RED - Security Fix & Configuration Setup
3. **Step 2**: 🔴 RED - Create Wrapper Interface for Testability
4. **Step 3**: 🔴 RED - Write Failing Unit Tests FIRST
5. **Step 4**: 🟢 GREEN - Extend IEmailService Interface
6. **Step 5**: 🟢 GREEN - Implement ResendEmailService
7. **Step 6**: 🟢 GREEN - Register Services in DI Container
8. **Step 7**: ✅ REFACTOR - Run Tests and Verify
9. **Step 8**: ✅ REFACTOR - Manual Testing
10. **Step 9**: Update Technical Documentation

**CRITICAL**: Follow TDD strictly - tests BEFORE implementation (Steps 3 → 4-6 → 7).

---

## Testing Checklist

### Unit Tests ✅

- [x] Constructor validation tests (API key required)
- [x] `SendVerificationEmailAsync` success and failure scenarios
- [x] `SendWelcomeEmailAsync` success and failure scenarios
- [x] `SendPasswordResetEmailAsync` success and failure scenarios
- [x] `SendCampRegistrationConfirmationAsync` success and failure scenarios
- [x] `SendPaymentReceiptAsync` success and failure scenarios
- [x] `SendEventReminderAsync` success and failure scenarios
- [x] `SendFeedbackRequestAsync` success and failure scenarios
- [x] `SendCampUpdateNotificationAsync` success and failure scenarios
- [x] Error handling (rate limit, authentication failures)
- [x] Logging verification for all email sends

### Manual Tests ✅

- [ ] Verification email received and link works
- [ ] Welcome email received after verification
- [ ] Email formatting displays correctly in email clients (Gmail, Outlook)
- [ ] Resend dashboard shows delivered emails
- [ ] Configuration validation logs warning when API key missing

### Code Coverage ✅

- **Target**: 90%+ coverage for `ResendEmailService`
- **Measure**: Run `dotnet test --collect:"XPlat Code Coverage"`
- **Verify**: All public methods covered by unit tests

---

## Error Response Format

Email sending errors are handled by `GlobalExceptionMiddleware` and return:

**Status Code**: 500 Internal Server Error

**Response Body**:

```json
{
  "success": false,
  "error": "Failed to send verification email: Network error",
  "timestamp": "2026-02-13T10:30:00Z"
}
```

**HTTP Status Codes**:

- `200 OK` - Email sent successfully (logged, no direct response)
- `500 Internal Server Error` - Email sending failed (Resend API error, network issue, etc.)

**Implementation Notes**:

- Email service throws `InvalidOperationException` on failure
- `GlobalExceptionMiddleware` catches and formats the response
- Structured logging captures full error details for debugging

---

## Dependencies

### NuGet Packages

- **Resend** - Official Resend .NET SDK (install via `dotnet add package Resend`)

### External Services

- **Resend Account** - Required for API access (<https://resend.com>)
- **Verified Domain** - Required for production use (configure in Resend dashboard)

### Configuration Requirements

- **Development**: `dotnet user-secrets` for API key storage
- **Production**: Environment variables (`Resend__ApiKey`, `Resend__FromEmail`, etc.)

### No EF Core Migrations Required

- This feature does NOT modify database schema
- No new entities or tables added
- Existing user/auth tables remain unchanged

---

## Notes

### Critical Security Reminders

- ⚠️ **NEVER commit API keys** to version control
- ⚠️ **ROTATE the exposed key** (`re_NeFLnh8Y_AiYxnnGBgnku7PpGCvN4X74u`) in Resend dashboard immediately
- ⚠️ Use `.gitignore` to exclude `appsettings.Development.json` if it contains secrets
- ⚠️ Use user-secrets for local development: `dotnet user-secrets set "Resend:ApiKey" "your-key"`
- ⚠️ Use environment variables or Azure Key Vault for production

### Business Rules

- Verification emails MUST be sent on registration (critical for account activation)
- Welcome emails are sent after successful verification (non-critical, can be queued)
- Payment receipts are sent synchronously (critical for audit trail)
- All emails use async/await and accept `CancellationToken`

### Language Requirements

- Email templates use **English** as primary language
- Future enhancement: Support Spanish (detect user language preference)

### RGPD/GDPR Considerations

- Do NOT include sensitive data in emails (medical notes, dietary restrictions)
- Email addresses are personal data - log securely (hashed in logs if needed)
- Provide unsubscribe mechanism for marketing emails (not applicable for transactional)
- Transactional emails are exempt from marketing consent requirements

### Performance Notes

- **Synchronous sending** for critical emails (verification, payment receipts)
- **Future enhancement**: Background queue for non-critical emails (welcome, feedback)
- Resend rate limits:
  - Free tier: 100 emails/day, 3000/month
  - Paid tier: No sending limits (pay per email)
- Monitor usage via Resend dashboard

### Testing Best Practices

- Use NSubstitute to mock `IResendClient` in unit tests
- Verify logging calls using `_logger.Received()` assertions
- Use `Arg.Is<T>()` to validate email message content
- Integration tests with real Resend API are optional (tagged `[Trait("Category", "Integration")]`)

---

## Next Steps After Implementation

1. **Code Review**: Request peer review of implementation
2. **Merge to Main**: Merge `feature/feat-resend-integration-backend` → `main`
3. **Deploy to Staging**: Test in staging environment with real Resend account
4. **Domain Verification**: Verify domain (`abuvi.org`) in Resend dashboard for production
5. **Production Deployment**:
   - Set environment variables for production Resend API key
   - Verify email delivery in production
   - Monitor Resend dashboard for delivery rates
6. **Future Enhancements**:
   - Migrate to Resend template management (store templates in Resend dashboard)
   - Implement Resend webhooks to track delivery, bounces, opens
   - Add email queue for non-critical sends (Hangfire or MassTransit)
   - Support multiple languages (Spanish, English)
   - Add A/B testing for email content

---

## Implementation Verification

### Final Checklist

#### Code Quality ✅

- [ ] C# nullable reference types handled correctly
- [ ] All async methods use `async/await` properly
- [ ] Structured logging with relevant context (`{Email}`, `{MessageId}`)
- [ ] No hardcoded strings (use configuration)
- [ ] Exception handling follows project standards

#### Functionality ✅

- [ ] All email types implemented per `IEmailService` interface
- [ ] Emails include correct links (verification URL, dashboard URL, etc.)
- [ ] HTML templates render correctly in email clients
- [ ] Configuration validation logs warnings appropriately
- [ ] Error handling wraps exceptions in `InvalidOperationException`

#### Testing ✅

- [ ] 90%+ unit test coverage achieved
- [ ] All tests follow AAA pattern (Arrange, Act, Assert)
- [ ] Test naming: `MethodName_StateUnderTest_ExpectedBehavior`
- [ ] Mocking uses NSubstitute correctly
- [ ] Assertions use FluentAssertions
- [ ] Manual testing completed for verification and welcome emails

#### Integration ✅

- [ ] Services registered in `Program.cs` DI container
- [ ] Configuration uses user-secrets (development) and env vars (production)
- [ ] No EF Core migrations required (no schema changes)
- [ ] API endpoints trigger emails as expected

#### Documentation ✅

- [ ] README.md or setup guide updated with Resend configuration
- [ ] API documentation notes email sending behavior
- [ ] Code comments explain business logic where needed
- [ ] All documentation in English (as per standards)

#### Security ✅

- [ ] Hardcoded API key removed from `appsettings.json`
- [ ] Exposed API key rotated in Resend dashboard
- [ ] User secrets configured for local development
- [ ] No sensitive data included in email templates
- [ ] Structured logging excludes PII (or hashes email addresses)

---

**Plan Status**: Ready for Implementation ✅
**TDD Approach**: Red-Green-Refactor cycle enforced
**Estimated Effort**: 4-6 hours (including testing and documentation)
**Priority**: High (blocks user registration workflow completion)

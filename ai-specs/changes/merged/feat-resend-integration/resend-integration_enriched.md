# Resend Integration for Abuvi - Enriched User Story

## Overview

Integrate Resend (<https://resend.com>) as the email service provider for all transactional emails in the Abuvi application. Replace the current stub implementation (`ResendEmailService`) that only logs email actions with a fully functional Resend integration.

API documentation is in [resend-docs.md](./resend-docs.md) - this enriched spec provides detailed implementation guidance, testing strategy, and considerations for the Resend integration.

## Business Context

The application currently has a placeholder email service that logs email events without actually sending emails. This prevents users from receiving:

- Email verification links (blocking account activation)
- Welcome emails
- Password reset emails
- Camp registration confirmations
- Payment receipts
- Event reminders

Implementing Resend will enable real email delivery, completing the user registration workflow and supporting all email-based features.

## Technical Requirements

### 1. Resend SDK Integration

**Package to Install:**

- `Resend` NuGet package (official Resend .NET SDK)
- Target version: Latest stable (verify compatibility with .NET 9)

**Installation Command:**

```bash
dotnet add src/Abuvi.API package Resend
```

### 2. Configuration Changes

**File:** `src/Abuvi.API/appsettings.json`

Add the following configuration section:

```json
{
  "Resend": {
    "ApiKey": "",  // Empty in appsettings.json
    "FromEmail": "noreply@abuvi.org",
    "FromName": "Abuvi Camps",
    "VerificationTemplateId": "",  // Optional: for template-based emails
    "WelcomeTemplateId": ""        // Optional: for template-based emails
  },
  "FrontendUrl": "http://localhost:5173"  // Already exists
}
```

**User Secrets (Development):**

For local development, set the API key using dotnet user-secrets:

```bash
dotnet user-secrets set "Resend:ApiKey" "re_123456789abcdefghijklmnopqrstuvwxyz" --project src/Abuvi.API
```

**Environment Variables (Production):**

```bash
export Resend__ApiKey="re_production_key_here"
export Resend__FromEmail="noreply@abuvi.org"
export FrontendUrl="https://abuvi.org"
```

### 3. Files to Modify

#### 3.1 Update `IEmailService` Interface

**File:** `src/Abuvi.API/Common/Services/IEmailService.cs`

Add new methods for all transactional email types:

```csharp
namespace Abuvi.API.Common.Services;

/// <summary>
/// Service interface for sending emails via Resend
/// </summary>
public interface IEmailService
{
    // Registration & Authentication
    Task SendVerificationEmailAsync(string toEmail, string firstName, string verificationToken, CancellationToken ct);
    Task SendWelcomeEmailAsync(string toEmail, string firstName, CancellationToken ct);
    Task SendPasswordResetEmailAsync(string toEmail, string firstName, string resetToken, CancellationToken ct);

    // Camp Registration
    Task SendCampRegistrationConfirmationAsync(string toEmail, string firstName, string campName, DateTime campStartDate, CancellationToken ct);
    Task SendCampUpdateNotificationAsync(string toEmail, string firstName, string campName, string updateMessage, CancellationToken ct);

    // Payments
    Task SendPaymentReceiptAsync(string toEmail, string firstName, decimal amount, string paymentReference, CancellationToken ct);

    // Feedback & Reminders
    Task SendFeedbackRequestAsync(string toEmail, string firstName, string campName, CancellationToken ct);
    Task SendEventReminderAsync(string toEmail, string firstName, string eventName, DateTime eventDate, CancellationToken ct);
}
```

#### 3.2 Implement `ResendEmailService`

**File:** `src/Abuvi.API/Common/Services/ResendEmailService.cs`

Complete implementation using Resend SDK:

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
    private readonly ResendClient _resend;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _frontendUrl;

    public ResendEmailService(IConfiguration configuration, ILogger<ResendEmailService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Resend:ApiKey"]
            ?? throw new InvalidOperationException("Resend API key not configured. Use dotnet user-secrets set \"Resend:ApiKey\" \"your-api-key\"");

        _fromEmail = configuration["Resend:FromEmail"] ?? "noreply@abuvi.org";
        _fromName = configuration["Resend:FromName"] ?? "Abuvi Camps";
        _frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:5173";

        _resend = new ResendClient(apiKey);
    }

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
                <body>
                    <h2>Welcome to Abuvi, {firstName}!</h2>
                    <p>Thank you for registering. Please verify your email address by clicking the link below:</p>
                    <p><a href=""{verificationUrl}"">Verify Email</a></p>
                    <p>This link will expire in 24 hours.</p>
                    <p>If you didn't create this account, please ignore this email.</p>
                    <br>
                    <p>Best regards,<br>The Abuvi Team</p>
                </body>
                </html>
            "
        };

        try
        {
            var response = await _resend.EmailSendAsync(message);
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

    // Implement other methods similarly...
}
```

#### 3.3 Update Service Registration

**File:** `src/Abuvi.API/Program.cs`

No changes needed - already registered:

```csharp
builder.Services.AddScoped<Abuvi.API.Common.Services.IEmailService, Abuvi.API.Common.Services.ResendEmailService>();
```

However, add validation for required configuration:

```csharp
// Email service configuration validation
var resendApiKey = builder.Configuration["Resend:ApiKey"];
if (string.IsNullOrEmpty(resendApiKey))
{
    var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
    logger.LogWarning("Resend API key not configured. Email sending will fail. Use: dotnet user-secrets set \"Resend:ApiKey\" \"your-key\"");
}
```

### 4. Email Templates

**Decision:** Start with inline HTML templates in code. Future enhancement: migrate to Resend's template management feature.

**Template Structure (for each email type):**

1. **Verification Email**
   - Subject: "Verify your email - Abuvi Camps"
   - Content: Welcome message, verification link (24h expiry), ignore if not registered
   - CTA: "Verify Email" button/link

2. **Welcome Email**
   - Subject: "Welcome to Abuvi Camps!"
   - Content: Account activated, next steps, explore features
   - CTA: "Go to Dashboard" link

3. **Password Reset Email** (future)
   - Subject: "Reset your password - Abuvi Camps"
   - Content: Password reset link (1h expiry), security notice
   - CTA: "Reset Password" button/link

4. **Camp Registration Confirmation**
   - Subject: "Your camp registration is confirmed - {CampName}"
   - Content: Registration details, camp info, payment summary, what to bring
   - CTA: "View Registration" link

5. **Payment Receipt**
   - Subject: "Payment received - {Amount}€"
   - Content: Payment amount, reference, registration details, remaining balance
   - No primary CTA (informational)

6. **Event Reminder**
   - Subject: "Reminder: {EventName} is coming up!"
   - Content: Event details, date/time, location, what to prepare
   - CTA: "View Details" link

7. **Feedback Request**
   - Subject: "We value your feedback - Abuvi Camps"
   - Content: Thank you for attending, feedback request, link to survey
   - CTA: "Give Feedback" button/link

### 5. Error Handling

**Strategy:**

- Wrap all Resend API calls in try-catch blocks
- Log failures with structured logging (email address, error details)
- Throw `InvalidOperationException` with descriptive message on failure
- Let `GlobalExceptionMiddleware` handle the exception and return 500 to client
- **Do NOT fail the entire request** if email sending fails during non-critical flows (e.g., welcome email)
- **DO fail the request** if email is critical (e.g., verification email during registration)

**Implementation Pattern:**

```csharp
try
{
    var response = await _resend.EmailSendAsync(message);
    _logger.LogInformation("Email sent to {Email}, ID: {MessageId}", toEmail, response.Id);
}
catch (ResendException ex) when (ex.StatusCode == 429)
{
    _logger.LogWarning("Resend rate limit hit for {Email}", toEmail);
    throw new InvalidOperationException("Email service rate limit exceeded. Please try again later.", ex);
}
catch (ResendException ex) when (ex.StatusCode == 401 || ex.StatusCode == 403)
{
    _logger.LogError("Resend authentication failed: {Error}", ex.Message);
    throw new InvalidOperationException("Email service configuration error.", ex);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
    throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
}
```

### 6. Testing Strategy

**Following TDD principles:**

#### 6.1 Unit Tests

**File:** `src/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs`

**Test Cases:**

1. **Constructor Tests:**
   - `Constructor_WhenApiKeyMissing_ThrowsInvalidOperationException`
   - `Constructor_WhenConfigValid_InitializesSuccessfully`

2. **SendVerificationEmailAsync Tests:**
   - `SendVerificationEmailAsync_WithValidInputs_SendsEmailSuccessfully`
   - `SendVerificationEmailAsync_WhenResendThrowsException_ThrowsInvalidOperationException`
   - `SendVerificationEmailAsync_IncludesCorrectVerificationUrl`
   - `SendVerificationEmailAsync_LogsSuccessfulSend`

3. **SendWelcomeEmailAsync Tests:**
   - `SendWelcomeEmailAsync_WithValidInputs_SendsEmailSuccessfully`
   - `SendWelcomeEmailAsync_WhenResendFails_ThrowsInvalidOperationException`

4. **SendPasswordResetEmailAsync Tests:**
   - (Similar pattern)

5. **Error Handling Tests:**
   - `SendEmail_WhenRateLimitHit_ThrowsWithAppropriateMessage`
   - `SendEmail_WhenAuthFails_ThrowsWithAppropriateMessage`

**Mocking Strategy:**

- Use NSubstitute to mock `IConfiguration` for constructor tests
- Use NSubstitute to mock `ILogger<ResendEmailService>` to verify logging
- **Challenge:** Resend SDK's `ResendClient` may not have an interface
  - **Solution 1:** Create a wrapper interface `IResendClient` and wrap the SDK
  - **Solution 2:** Use integration tests with real API calls (see below)
  - **Recommended:** Solution 1 for full unit test coverage

**Example Test:**

```csharp
public class ResendEmailServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly IResendClient _resendClient; // Wrapper interface

    public ResendEmailServiceTests()
    {
        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<ResendEmailService>>();
        _resendClient = Substitute.For<IResendClient>();

        // Setup configuration
        _configuration["Resend:ApiKey"].Returns("re_test_key");
        _configuration["Resend:FromEmail"].Returns("test@example.com");
        _configuration["FrontendUrl"].Returns("http://localhost:5173");
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WithValidInputs_SendsEmailSuccessfully()
    {
        // Arrange
        var sut = new ResendEmailService(_configuration, _logger, _resendClient);
        _resendClient.SendEmailAsync(Arg.Any<EmailMessage>())
            .Returns(new EmailResponse { Id = "msg_123" });

        // Act
        await sut.SendVerificationEmailAsync("user@example.com", "John", "token123", CancellationToken.None);

        // Assert
        await _resendClient.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == "user@example.com" &&
                m.HtmlBody.Contains("token123")));
    }
}
```

#### 6.2 Integration Tests

**File:** `src/Abuvi.Tests/Integration/Common/Services/ResendEmailServiceIntegrationTests.cs`

**Purpose:** Test actual Resend API integration (optional, requires real API key)

**Configuration:**

- Use test API key from user secrets or skip if not configured
- Tag tests with `[Trait("Category", "Integration")]`
- Send to test email addresses only

**Test Cases:**

- `SendVerificationEmail_WithRealAPI_SendsSuccessfully`
- `SendEmail_WithInvalidApiKey_ThrowsAuthenticationException`

**Note:** These tests are optional and can be skipped in CI/CD if API key is not configured.

#### 6.3 Manual Testing

**Checklist:**

1. Register a new user → Receive verification email
2. Click verification link → Account activated
3. Resend verification email → Receive new email
4. Register for a camp → Receive confirmation email
5. Make a payment → Receive receipt email
6. Check Resend dashboard for delivery analytics

### 7. Security Considerations

**API Key Protection:**

- Never commit API keys to version control
- Use dotnet user-secrets for local development
- Use environment variables or Azure Key Vault for production
- Validate API key presence at startup

**Email Content Security:**

- Sanitize user-provided content before including in emails
- Use HTML encoding for dynamic values (user names, camp names)
- Validate email addresses before sending
- Implement rate limiting (Resend has built-in limits)

**RGPD Compliance:**

- Do not include sensitive data (medical notes, allergies) in emails
- Include unsubscribe mechanism for marketing emails (not applicable for transactional)
- Log email sends for audit trail (who, when, what type)

### 8. Performance Considerations

**Async/Await:**

- All email operations are async
- Use `CancellationToken` for all methods
- Do not block on email sends

**Background Processing:**

- For non-critical emails (welcome, feedback), consider background jobs
- Use `IHostedService` or Hangfire for queued email processing
- **Phase 1:** Synchronous sending (simpler, acceptable for low volume)
- **Phase 2:** Background queue (future enhancement)

**Rate Limits:**

- Resend free tier: 100 emails/day, 3000/month
- Resend paid tier: No sending limits (pay per email)
- Monitor usage via Resend dashboard

### 9. Monitoring & Observability

**Logging:**

- Log all email send attempts (success/failure)
- Include email type, recipient (hashed for privacy), Resend message ID
- Use structured logging for easy querying

**Metrics:**

- Track email send success rate
- Track email delivery rate (via Resend webhooks - future)
- Alert on high failure rates

**Resend Dashboard:**

- Monitor delivery, bounce, and spam rates
- Track email performance (opens, clicks)
- View error logs for failed sends

### 10. Acceptance Criteria

**Definition of Done:**

1. ✅ Resend SDK installed and configured
2. ✅ `ResendEmailService` fully implements all email types in `IEmailService`
3. ✅ Configuration uses user-secrets/env variables (no hardcoded keys)
4. ✅ All unit tests passing (90%+ coverage)
5. ✅ Manual testing completed for verification and welcome emails
6. ✅ Error handling in place for API failures
7. ✅ Logging implemented for all send operations
8. ✅ Documentation updated in `api-endpoints.md` (note that emails are sent)
9. ✅ Configuration example added to `README.md` or setup docs
10. ✅ Verified in Resend dashboard that emails are delivered

### 11. Implementation Steps (TDD Approach)

**Phase 1: Verification & Welcome Emails**

1. **Setup & Configuration**
   - Install Resend NuGet package
   - Add configuration to appsettings.json
   - Set API key in user-secrets

2. **Create Test Suite**
   - Create `ResendEmailServiceTests.cs`
   - Write failing tests for `SendVerificationEmailAsync`
   - Write failing tests for `SendWelcomeEmailAsync`

3. **Implement Wrapper (if needed)**
   - Create `IResendClient` wrapper interface
   - Implement wrapper around Resend SDK

4. **Implement Service**
   - Update `ResendEmailService` constructor
   - Implement `SendVerificationEmailAsync` to pass tests
   - Implement `SendWelcomeEmailAsync` to pass tests

5. **Manual Testing**
   - Test verification email flow end-to-end
   - Verify emails arrive and links work
   - Check Resend dashboard

**Phase 2: Additional Email Types**

1. **Password Reset** (if password reset feature exists)
   - Write tests for `SendPasswordResetEmailAsync`
   - Implement method
   - Test manually

2. **Camp Registration Confirmation**
   - Write tests for `SendCampRegistrationConfirmationAsync`
   - Implement method
   - Test manually

3. **Payment Receipt**
   - Write tests for `SendPaymentReceiptAsync`
   - Implement method
   - Test manually

4. **Reminders & Notifications**
   - Write tests for `SendEventReminderAsync`, `SendFeedbackRequestAsync`, etc.
   - Implement methods
   - Test manually

**Phase 3: Documentation & Deployment**

1. **Update Documentation**
    - Document Resend configuration in setup guide
    - Update API documentation to note email delivery
    - Add troubleshooting section for email issues

2. **Deployment Preparation**
    - Document environment variable setup
    - Create deployment checklist
    - Test in staging environment

### 12. Future Enhancements

**Template Management:**

- Migrate to Resend's template editor for easier email updates
- Store template IDs in configuration
- Version control for email content

**Webhooks:**

- Implement Resend webhook endpoint to track delivery, bounces, opens
- Update database with email status
- Retry failed sends

**Background Queue:**

- Implement email queue for non-critical sends
- Use Hangfire or MassTransit for reliability
- Retry logic for transient failures

**Localization:**

- Support multiple languages (Spanish, English)
- Detect user language preference
- Store templates per language

**Analytics:**

- Track email open rates
- Track link click-through rates
- A/B testing for email content

## Related Documentation

- [API Endpoints](../../specs/api-endpoints.md) - Update to note email sending behavior
- [Backend Standards](../../specs/backend-standards.mdc) - Follow TDD and service patterns
- [Manual Testing Guide](../../../docs/MANUAL_TESTING_REGISTRATION.md) - Add email testing scenarios

## Dependencies

**External:**

- Resend account and API key (<https://resend.com>)
- Verified domain in Resend (for production - "abuvi.org")

**Internal:**

- User registration workflow (already implemented)
- Configuration management (appsettings.json, user-secrets)

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Resend API downtime | Emails not sent | Implement retry logic, fallback logging |
| Rate limit exceeded | Emails rejected | Monitor usage, upgrade plan, implement queue |
| Invalid email addresses | Bounces, spam complaints | Validate emails, double opt-in |
| Email templates broken | Poor user experience | Version control templates, preview in Resend |
| API key leaked | Security breach | Use secrets management, rotate keys regularly |

## Success Metrics

- 100% of verification emails delivered within 1 minute
- <1% email bounce rate
- <0.1% spam complaint rate
- 95%+ email open rate for verification emails
- Zero failed email sends due to configuration errors

---

**Story Status:** Ready for development
**Priority:** High
**Estimated Effort:** 2-3 days
**Dependencies:** None (standalone feature)

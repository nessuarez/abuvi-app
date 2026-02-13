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
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Verification email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                messageId);
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
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Welcome email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                messageId);
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
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Password reset email sent to {Email}, Resend ID: {MessageId}",
                toEmail,
                messageId);
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
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Camp registration confirmation sent to {Email} for camp {CampName}, Resend ID: {MessageId}",
                toEmail,
                campName,
                messageId);
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
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Camp update notification sent to {Email} for {CampName}, Resend ID: {MessageId}",
                toEmail,
                campName,
                messageId);
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
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Payment receipt sent to {Email}, Amount: {Amount}, Reference: {Reference}, Resend ID: {MessageId}",
                toEmail,
                amount,
                paymentReference,
                messageId);
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
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Feedback request sent to {Email} for camp {CampName}, Resend ID: {MessageId}",
                toEmail,
                campName,
                messageId);
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
            var messageId = await _resend.SendEmailAsync(message);
            _logger.LogInformation(
                "Event reminder sent to {Email} for event {EventName}, Resend ID: {MessageId}",
                toEmail,
                eventName,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event reminder to {Email}", toEmail);
            throw new InvalidOperationException($"Failed to send event reminder: {ex.Message}", ex);
        }
    }
}

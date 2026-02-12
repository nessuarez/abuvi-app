namespace Abuvi.API.Common.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Email service implementation (simplified for TDD - Resend integration pending)
/// </summary>
public class ResendEmailService(IConfiguration configuration, ILogger<ResendEmailService> logger)
    : IEmailService
{
    private readonly string _frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:5173";

    public Task SendVerificationEmailAsync(
        string toEmail,
        string firstName,
        string verificationToken,
        CancellationToken ct)
    {
        // TODO: Implement actual Resend email sending
        // For now, just log the action to allow TDD to proceed
        var verificationUrl = $"{_frontendUrl}/verify-email?token={verificationToken}";
        logger.LogInformation(
            "Verification email would be sent to {Email} with URL: {Url}",
            toEmail,
            verificationUrl);

        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string toEmail, string firstName, CancellationToken ct)
    {
        // TODO: Implement actual Resend email sending
        // For now, just log the action to allow TDD to proceed
        logger.LogInformation("Welcome email would be sent to {Email}", toEmail);

        return Task.CompletedTask;
    }
}

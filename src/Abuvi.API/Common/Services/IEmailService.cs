namespace Abuvi.API.Common.Services;

/// <summary>
/// Service interface for sending emails
/// </summary>
public interface IEmailService
{
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
}

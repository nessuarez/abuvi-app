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
    /// <returns>Email ID from Resend</returns>
    Task<string> SendEmailAsync(EmailMessage message);
}

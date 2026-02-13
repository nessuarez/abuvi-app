namespace Abuvi.API.Common.Services;

using Resend;

/// <summary>
/// Wrapper implementation delegating to Resend SDK
/// </summary>
public class ResendClientWrapper : IResendClient
{
    private readonly IResend _client;

    public ResendClientWrapper(string apiKey)
    {
        _client = ResendClient.Create(apiKey);
    }

    public async Task<string> SendEmailAsync(EmailMessage message)
    {
        try
        {
            var response = await _client.EmailSendAsync(message);

            // Response.Content is a Guid representing the message ID
            // If we get here, the SDK call succeeded
            return response.Content.ToString();
        }
        catch (Exception ex)
        {
            // Log the original exception and rethrow with context
            throw new InvalidOperationException(
                $"Failed to send email via Resend API: {ex.Message}",
                ex);
        }
    }
}

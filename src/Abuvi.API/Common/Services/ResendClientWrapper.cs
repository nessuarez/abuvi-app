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
        var response = await _client.EmailSendAsync(message);
        return response.Content.ToString();
    }
}

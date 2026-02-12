namespace Abuvi.API.Features.Auth;

/// <summary>
/// Request DTO for email verification
/// </summary>
public record VerifyEmailRequest(string Token);

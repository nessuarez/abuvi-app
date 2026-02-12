namespace Abuvi.API.Features.Auth;

/// <summary>
/// Request DTO for resending verification email
/// </summary>
public record ResendVerificationRequest(string Email);

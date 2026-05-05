namespace Abuvi.API.Features.Auth;

/// <summary>
/// Request DTO for user registration with email verification
/// </summary>
public record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    bool AcceptedTerms
);

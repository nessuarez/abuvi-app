namespace Abuvi.API.Features.Auth;

/// <summary>
/// Request DTO for user login
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Request DTO for new user registration
/// </summary>
public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone
);

/// <summary>
/// Response DTO for successful login
/// </summary>
public record LoginResponse(
    string Token,
    UserInfo User
);

/// <summary>
/// User information included in auth responses
/// </summary>
public record UserInfo(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role
);

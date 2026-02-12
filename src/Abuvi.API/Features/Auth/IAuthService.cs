using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Interface for authentication and user registration services
/// </summary>
public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserInfo> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> RegisterUserAsync(RegisterUserRequest request, CancellationToken ct);
    Task VerifyEmailAsync(string token, CancellationToken ct);
    Task ResendVerificationAsync(string email, CancellationToken ct);
}

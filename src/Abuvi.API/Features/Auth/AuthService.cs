using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Handles authentication and user registration logic
/// </summary>
public class AuthService
{
    private readonly IUsersRepository _usersRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(
        IUsersRepository usersRepository,
        IPasswordHasher passwordHasher,
        JwtTokenService jwtTokenService)
    {
        _usersRepository = usersRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LoginResponse with token and user info, or null if authentication fails</returns>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Find user by email
        var user = await _usersRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null) return null;

        // Verify password
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            return null;

        // Check if user is active
        if (!user.IsActive) return null;

        // Generate JWT token
        var token = _jwtTokenService.GenerateToken(user);

        return new LoginResponse(
            token,
            new UserInfo(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role.ToString()
            )
        );
    }

    /// <summary>
    /// Registers a new user with Member role by default
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>UserInfo for the newly created user</returns>
    /// <exception cref="InvalidOperationException">Thrown when email already exists</exception>
    public async Task<UserInfo> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        var existing = await _usersRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException("Email already registered");

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // Create user with Member role by default
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Role = UserRole.Member, // New registrations default to Member
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _usersRepository.CreateAsync(user, cancellationToken);

        return new UserInfo(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role.ToString()
        );
    }
}

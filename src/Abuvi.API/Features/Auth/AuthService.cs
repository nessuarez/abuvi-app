using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Abuvi.API.Features.Auth;

/// <summary>
/// Handles authentication and user registration logic
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUsersRepository _usersRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUsersRepository usersRepository,
        IPasswordHasher passwordHasher,
        JwtTokenService jwtTokenService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _usersRepository = usersRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _usersRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null) return null;

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            return null;

        if (!user.IsActive) return null;

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
    /// Registers a new user with email verification (old version - kept for compatibility)
    /// </summary>
    public async Task<UserInfo> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _usersRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException("Email already registered");

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Role = UserRole.Member,
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

    /// <summary>
    /// Registers a new user with email verification workflow
    /// </summary>
    public virtual async Task<UserResponse> RegisterUserAsync(RegisterUserRequest request, CancellationToken ct)
    {
        // Check for duplicate email
        var existingUser = await _usersRepository.GetByEmailAsync(request.Email, ct);
        if (existingUser is not null)
        {
            throw new BusinessRuleException("Ya existe una cuenta con este correo electrónico");
        }

        // Check for duplicate document number (only if provided)
        if (!string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            var existingByDocument = await _usersRepository.GetByDocumentNumberAsync(request.DocumentNumber, ct);
            if (existingByDocument is not null)
            {
                throw new BusinessRuleException("Ya existe una cuenta con este número de documento");
            }
        }

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // Generate email verification token
        var verificationToken = GenerateVerificationToken();
        var tokenExpiry = DateTime.UtcNow.AddHours(24);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            DocumentNumber = request.DocumentNumber,
            Role = UserRole.Member,
            IsActive = false,
            EmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiry = tokenExpiry,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _usersRepository.CreateAsync(user, ct);

        // Send verification email
        await _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, verificationToken, ct);

        _logger.LogInformation("User {Email} registered successfully. Verification email sent.", user.Email);

        return user.ToResponse();
    }

    /// <summary>
    /// Verifies user's email address and activates account
    /// </summary>
    public virtual async Task VerifyEmailAsync(string token, CancellationToken ct)
    {
        var user = await _usersRepository.GetByVerificationTokenAsync(token, ct)
            ?? throw new NotFoundException("User", Guid.Empty);

        if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
        {
            throw new BusinessRuleException("El código de verificación ha expirado");
        }

        user.EmailVerified = true;
        user.IsActive = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _usersRepository.UpdateAsync(user, ct);

        // Send welcome email after successful verification
        await _emailService.SendWelcomeEmailAsync(user.Email, user.FirstName, ct);

        _logger.LogInformation("User {Email} email verified successfully", user.Email);
    }

    /// <summary>
    /// Resends verification email to user
    /// </summary>
    public virtual async Task ResendVerificationAsync(string email, CancellationToken ct)
    {
        var user = await _usersRepository.GetByEmailAsync(email, ct)
            ?? throw new NotFoundException("User", Guid.Empty);

        if (user.EmailVerified)
        {
            throw new BusinessRuleException("El correo electrónico ya está verificado");
        }

        // Generate new verification token
        var verificationToken = GenerateVerificationToken();
        var tokenExpiry = DateTime.UtcNow.AddHours(24);

        user.EmailVerificationToken = verificationToken;
        user.EmailVerificationTokenExpiry = tokenExpiry;
        user.UpdatedAt = DateTime.UtcNow;

        await _usersRepository.UpdateAsync(user, ct);

        // Send new verification email
        await _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, verificationToken, ct);

        _logger.LogInformation("Verification email resent to {Email}", user.Email);
    }

    private static string GenerateVerificationToken()
    {
        var randomBytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_");
    }
}

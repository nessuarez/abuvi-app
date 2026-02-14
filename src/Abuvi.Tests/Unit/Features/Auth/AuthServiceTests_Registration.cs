namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

public class AuthServiceTests_Registration
{
    private readonly IUsersRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _service;

    public AuthServiceTests_Registration()
    {
        _repository = Substitute.For<IUsersRepository>();
        _emailService = Substitute.For<IEmailService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();

        var jwtConfig = Substitute.For<IConfiguration>();
        _jwtTokenService = Substitute.For<JwtTokenService>(jwtConfig);
        _logger = Substitute.For<ILogger<AuthService>>();

        var configDict = new Dictionary<string, string?>
        {
            ["FrontendUrl"] = "http://localhost:5173"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _service = new AuthService(
            _repository,
            _passwordHasher,
            _jwtTokenService,
            _emailService,
            _configuration,
            _logger);
    }

    [Fact]
    public async Task RegisterUserAsync_WithValidRequest_CreatesUserAndSendsEmail()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            "+34612345678",
            true
        );

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _repository.GetByDocumentNumberAsync(request.DocumentNumber!, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _passwordHasher.HashPassword(request.Password)
            .Returns("hashed_password");

        // Act
        var result = await _service.RegisterUserAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("user@example.com");
        result.FirstName.Should().Be("John");
        result.EmailVerified.Should().BeFalse();
        result.IsActive.Should().BeFalse();

        await _repository.Received(1).CreateAsync(
            Arg.Is<User>(u =>
                u.Email == request.Email &&
                u.FirstName == request.FirstName &&
                u.DocumentNumber == request.DocumentNumber &&
                u.EmailVerified == false &&
                u.IsActive == false &&
                u.EmailVerificationToken != null &&
                u.EmailVerificationTokenExpiry != null
            ),
            Arg.Any<CancellationToken>()
        );

        await _emailService.Received(1).SendVerificationEmailAsync(
            request.Email,
            request.FirstName,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RegisterUserAsync_WithDuplicateEmail_ThrowsBusinessRuleException()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "existing@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            PasswordHash = "hash",
            FirstName = "Existing",
            LastName = "User",
            DocumentNumber = "99999999Z",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        Func<Task> act = async () => await _service.RegisterUserAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("Ya existe una cuenta con este correo electrónico");

        await _repository.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WithValidToken_ActivatesUser()
    {
        // Arrange
        var token = "valid-token-123";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            DocumentNumber = "12345678A",
            EmailVerified = false,
            IsActive = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByVerificationTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _service.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.EmailVerified == true &&
                u.IsActive == true &&
                u.EmailVerificationToken == null &&
                u.EmailVerificationTokenExpiry == null
            ),
            Arg.Any<CancellationToken>()
        );

        await _emailService.Received(1).SendWelcomeEmailAsync(
            user.Email,
            user.FirstName,
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task VerifyEmailAsync_WithExpiredToken_ThrowsBusinessRuleException()
    {
        // Arrange
        var token = "expired-token";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            DocumentNumber = "12345678A",
            EmailVerified = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1), // Expired
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByVerificationTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Func<Task> act = async () => await _service.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("El código de verificación ha expirado");
    }
}

namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class AuthServiceTests_Registration
{
    private readonly Mock<IUsersRepository> _repository;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<IPasswordHasher> _passwordHasher;
    private readonly Mock<JwtTokenService> _jwtTokenService;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<AuthService>> _logger;
    private readonly AuthService _service;

    public AuthServiceTests_Registration()
    {
        _repository = new Mock<IUsersRepository>();
        _emailService = new Mock<IEmailService>();
        _passwordHasher = new Mock<IPasswordHasher>();
        _jwtTokenService = new Mock<JwtTokenService>(MockBehavior.Loose, new object[] { Mock.Of<IConfiguration>() });
        _logger = new Mock<ILogger<AuthService>>();

        var configDict = new Dictionary<string, string?>
        {
            ["FrontendUrl"] = "http://localhost:5173"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _service = new AuthService(
            _repository.Object,
            _passwordHasher.Object,
            _jwtTokenService.Object,
            _emailService.Object,
            _configuration,
            _logger.Object);
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

        _repository.Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repository.Setup(r => r.GetByDocumentNumberAsync(request.DocumentNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _passwordHasher.Setup(p => p.HashPassword(request.Password))
            .Returns("hashed_password");

        // Act
        var result = await _service.RegisterUserAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("user@example.com");
        result.FirstName.Should().Be("John");
        result.EmailVerified.Should().BeFalse();
        result.IsActive.Should().BeFalse();

        _repository.Verify(r => r.CreateAsync(
            It.Is<User>(u =>
                u.Email == request.Email &&
                u.FirstName == request.FirstName &&
                u.DocumentNumber == request.DocumentNumber &&
                u.EmailVerified == false &&
                u.IsActive == false &&
                u.EmailVerificationToken != null &&
                u.EmailVerificationTokenExpiry != null
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _emailService.Verify(e => e.SendVerificationEmailAsync(
            request.Email,
            request.FirstName,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
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

        _repository.Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        Func<Task> act = async () => await _service.RegisterUserAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*email already exists*");

        _repository.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _repository.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _service.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        _repository.Verify(r => r.UpdateAsync(
            It.Is<User>(u =>
                u.EmailVerified == true &&
                u.IsActive == true &&
                u.EmailVerificationToken == null &&
                u.EmailVerificationTokenExpiry == null
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _emailService.Verify(e => e.SendWelcomeEmailAsync(
            user.Email,
            user.FirstName,
            It.IsAny<CancellationToken>()
        ), Times.Once);
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

        _repository.Setup(r => r.GetByVerificationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        Func<Task> act = async () => await _service.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*token has expired*");
    }
}

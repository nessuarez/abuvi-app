namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Common.Services;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

public class AuthServiceTests_ForgotPassword
{
    private readonly IUsersRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _service;

    public AuthServiceTests_ForgotPassword()
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _service = new AuthService(
            _repository,
            _passwordHasher,
            _jwtTokenService,
            _emailService,
            configuration,
            _logger);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserNotFound_DoesNotSendEmailAndDoesNotThrow()
    {
        // Arrange
        _repository.GetByEmailAsync("notfound@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => _service.ForgotPasswordAsync("notfound@example.com", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await _emailService.DidNotReceive().SendPasswordResetEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserInactive_DoesNotSendEmail()
    {
        // Arrange
        var inactiveUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "inactive@example.com",
            FirstName = "Jane",
            IsActive = false
        };

        _repository.GetByEmailAsync("inactive@example.com", Arg.Any<CancellationToken>())
            .Returns(inactiveUser);

        // Act
        await _service.ForgotPasswordAsync("inactive@example.com", CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendPasswordResetEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_SavesResetTokenWithOneHourExpiry()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            IsActive = true
        };

        _repository.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());

        var before = DateTime.UtcNow;

        // Act
        await _service.ForgotPasswordAsync("user@example.com", CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.PasswordResetToken != null &&
                u.PasswordResetToken.Length > 0 &&
                u.PasswordResetTokenExpiry >= before.AddHours(1).AddSeconds(-5) &&
                u.PasswordResetTokenExpiry <= before.AddHours(1).AddSeconds(5)
            ),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_SendsPasswordResetEmail()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            IsActive = true
        };

        _repository.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());

        // Act
        await _service.ForgotPasswordAsync("user@example.com", CancellationToken.None);

        // Assert
        await _emailService.Received(1).SendPasswordResetEmailAsync(
            "user@example.com",
            "John",
            Arg.Is<string>(t => t.Length > 0),
            Arg.Any<CancellationToken>());
    }
}

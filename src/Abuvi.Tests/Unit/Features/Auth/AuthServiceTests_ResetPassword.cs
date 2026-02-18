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

public class AuthServiceTests_ResetPassword
{
    private readonly IUsersRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _service;

    public AuthServiceTests_ResetPassword()
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
    public async Task ResetPasswordAsync_WhenTokenNotFound_ThrowsBusinessRuleException()
    {
        // Arrange
        _repository.GetByPasswordResetTokenAsync("invalid-token", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => _service.ResetPasswordAsync("invalid-token", "NewPassword1!", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("El enlace de recuperación es inválido o ha expirado");
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenExpired_ThrowsBusinessRuleException()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordResetToken = "expired-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1) // expired 1 hour ago
        };

        _repository.GetByPasswordResetTokenAsync("expired-token", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var act = () => _service.ResetPasswordAsync("expired-token", "NewPassword1!", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("El enlace de recuperación es inválido o ha expirado");
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenValid_UpdatesPasswordHash()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = "old_hash",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30)
        };

        _repository.GetByPasswordResetTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());
        _passwordHasher.HashPassword("NewPassword1!").Returns("new_hashed_password");

        // Act
        await _service.ResetPasswordAsync("valid-token", "NewPassword1!", CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.PasswordHash == "new_hashed_password"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenValid_ClearsResetToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30)
        };

        _repository.GetByPasswordResetTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");

        // Act
        await _service.ResetPasswordAsync("valid-token", "NewPassword1!", CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.PasswordResetToken == null &&
                u.PasswordResetTokenExpiry == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenValid_DoesNotSendAnyEmail()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30)
        };

        _repository.GetByPasswordResetTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(x => x.Arg<User>());
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");

        // Act
        await _service.ResetPasswordAsync("valid-token", "NewPassword1!", CancellationToken.None);

        // Assert — no email is sent on successful reset (consistent with other reset flows)
        await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(
            default!, default!, default!, default);
    }
}

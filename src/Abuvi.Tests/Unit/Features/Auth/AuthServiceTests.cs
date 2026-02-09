using FluentAssertions;
using NSubstitute;
using Xunit;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;

namespace Abuvi.Tests.Unit.Features.Auth;

/// <summary>
/// Unit tests for AuthService
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class AuthServiceTests
{
    private readonly IUsersRepository _usersRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _usersRepository = Substitute.For<IUsersRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenService = Substitute.For<JwtTokenService>(Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>());
        _sut = new AuthService(_usersRepository, _passwordHasher, _jwtTokenService);
    }

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _usersRepository.GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("password123", "hashed-password").Returns(true);
        _jwtTokenService.GenerateToken(user).Returns("jwt-token");

        var request = new LoginRequest("test@example.com", "password123");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be("jwt-token");
        result.User.Email.Should().Be("test@example.com");
        result.User.FirstName.Should().Be("Test");
        result.User.LastName.Should().Be("User");
        result.User.Role.Should().Be("Member");
        result.User.Id.Should().Be(userId);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidEmail_ReturnsNull()
    {
        // Arrange
        _usersRepository.GetByEmailAsync("invalid@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);
        var request = new LoginRequest("invalid@example.com", "password123");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _usersRepository.GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("wrong-password", "hashed-password").Returns(false);
        var request = new LoginRequest("test@example.com", "wrong-password");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = false, // Inactive user
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _usersRepository.GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("password123", "hashed-password").Returns(true);
        var request = new LoginRequest("test@example.com", "password123");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region RegisterAsync Tests

    [Fact]
    public async Task RegisterAsync_WithUniqueEmail_CreatesUserAndReturnsUserInfo()
    {
        // Arrange
        _usersRepository.GetByEmailAsync("newuser@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);
        _passwordHasher.HashPassword("Password123!").Returns("hashed-password");

        var request = new RegisterRequest(
            "newuser@example.com",
            "Password123!",
            "New",
            "User",
            "555-1234"
        );

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("newuser@example.com");
        result.FirstName.Should().Be("New");
        result.LastName.Should().Be("User");
        result.Role.Should().Be("Member"); // Default role

        await _usersRepository.Received(1).CreateAsync(
            Arg.Is<User>(u =>
                u.Email == "newuser@example.com" &&
                u.PasswordHash == "hashed-password" &&
                u.FirstName == "New" &&
                u.LastName == "User" &&
                u.Phone == "555-1234" &&
                u.Role == UserRole.Member &&
                u.IsActive == true
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            PasswordHash = "hash",
            FirstName = "Existing",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _usersRepository.GetByEmailAsync("existing@example.com", Arg.Any<CancellationToken>()).Returns(existingUser);

        var request = new RegisterRequest(
            "existing@example.com",
            "Password123!",
            "New",
            "User",
            null
        );

        // Act
        var act = () => _sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Email already registered");
    }

    [Fact]
    public async Task RegisterAsync_CreatesUserWithMemberRole()
    {
        // Arrange
        _usersRepository.GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);
        _passwordHasher.HashPassword("Password123!").Returns("hashed-password");

        var request = new RegisterRequest(
            "test@example.com",
            "Password123!",
            "Test",
            "User",
            null
        );

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Role.Should().Be("Member");

        await _usersRepository.Received(1).CreateAsync(
            Arg.Is<User>(u => u.Role == UserRole.Member),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RegisterAsync_HashesPasswordBeforeStoring()
    {
        // Arrange
        _usersRepository.GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);
        _passwordHasher.HashPassword("PlaintextPassword123!").Returns("bcrypt-hashed-password");

        var request = new RegisterRequest(
            "test@example.com",
            "PlaintextPassword123!",
            "Test",
            "User",
            null
        );

        // Act
        await _sut.RegisterAsync(request);

        // Assert
        _passwordHasher.Received(1).HashPassword("PlaintextPassword123!");

        await _usersRepository.Received(1).CreateAsync(
            Arg.Is<User>(u => u.PasswordHash == "bcrypt-hashed-password"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task RegisterAsync_WithNullPhone_CreatesUserSuccessfully()
    {
        // Arrange
        _usersRepository.GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);
        _passwordHasher.HashPassword("Password123!").Returns("hashed-password");

        var request = new RegisterRequest(
            "test@example.com",
            "Password123!",
            "Test",
            "User",
            null
        );

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Should().NotBeNull();

        await _usersRepository.Received(1).CreateAsync(
            Arg.Is<User>(u => u.Phone == null),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion
}

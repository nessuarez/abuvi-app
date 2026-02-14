using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features;

/// <summary>
/// Unit tests for UsersService
/// </summary>
public class UsersServiceTests
{
    private readonly IUsersRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRoleChangeLogsRepository _auditLogRepository;
    private readonly UsersService _service;

    public UsersServiceTests()
    {
        _repository = Substitute.For<IUsersRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _auditLogRepository = Substitute.For<IUserRoleChangeLogsRepository>();
        _service = new UsersService(_repository, _passwordHasher, _auditLogRepository);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUserResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            Phone = "+1234567890",
            Role = UserRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _service.GetByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await _service.GetByIdAsync(userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsListOfUserResponses()
    {
        // Arrange
        var users = new List<User>
        {
            new() {
                Id = Guid.NewGuid(),
                Email = "user1@example.com",
                FirstName = "User",
                LastName = "One",
                Role = UserRole.Member,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Id = Guid.NewGuid(),
                Email = "user2@example.com",
                FirstName = "User",
                LastName = "Two",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _repository.GetAllAsync(0, 100, Arg.Any<CancellationToken>())
            .Returns(users);

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Email.Should().Be("user1@example.com");
        result[1].Email.Should().Be("user2@example.com");
    }

    [Fact]
    public async Task CreateAsync_WhenEmailDoesNotExist_CreatesAndReturnsUser()
    {
        // Arrange
        var request = new CreateUserRequest(
            "newuser@example.com",
            "Password123!",
            "New",
            "User",
            "+1234567890",
            UserRole.Member
        );

        _repository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _passwordHasher.HashPassword(request.Password)
            .Returns("$2a$12$hashed_password_value");

        _repository.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<User>();
                user.Id = Guid.NewGuid();
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                return user;
            });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("newuser@example.com");
        result.FirstName.Should().Be("New");
        result.LastName.Should().Be("User");
        result.Role.Should().Be(UserRole.Member);
        result.IsActive.Should().BeTrue();

        _passwordHasher.Received(1).HashPassword(request.Password);

        await _repository.Received(1).CreateAsync(
            Arg.Is<User>(u => u.Email == request.Email && u.PasswordHash == "$2a$12$hashed_password_value"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CreateAsync_WhenEmailAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateUserRequest(
            "existing@example.com",
            "Password123!",
            "Existing",
            "User",
            null,
            UserRole.Member
        );

        _repository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var act = async () => await _service.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Ya existe un usuario con este correo electrónico");

        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());

        await _repository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CreateAsync_HashesPasswordWithBCrypt()
    {
        // Arrange
        var request = new CreateUserRequest(
            "test@example.com",
            "MySecurePassword123!",
            "Test",
            "User",
            null,
            UserRole.Member
        );

        var bcryptHash = "$2a$12$KIXqFc4MYYzRhJqOjvQmFu.xF3Z4YzLbF3Z4YzLbF3Z4YzLb";

        _repository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _passwordHasher.HashPassword(request.Password)
            .Returns(bcryptHash);

        _repository.CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<User>();
                user.Id = Guid.NewGuid();
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                return user;
            });

        // Act
        await _service.CreateAsync(request);

        // Assert
        _passwordHasher.Received(1).HashPassword("MySecurePassword123!");

        await _repository.Received(1).CreateAsync(
            Arg.Is<User>(u =>
                u.PasswordHash == bcryptHash &&
                u.PasswordHash.StartsWith("$2a$") // BCrypt format
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenUserExists_UpdatesAndReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingUser = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hashedpassword",
            FirstName = "Old",
            LastName = "Name",
            Phone = "+1111111111",
            Role = UserRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };

        var updateRequest = new UpdateUserRequest(
            "Updated",
            "Name",
            "+2222222222",
            false
        );

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        _repository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _service.UpdateAsync(userId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Updated");
        result.LastName.Should().Be("Name");
        result.Phone.Should().Be("+2222222222");
        result.IsActive.Should().BeFalse();

        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.Id == userId &&
                u.FirstName == "Updated" &&
                u.LastName == "Name"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var updateRequest = new UpdateUserRequest("First", "Last", null, true);

        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await _service.UpdateAsync(userId, updateRequest);

        // Assert
        result.Should().BeNull();

        await _repository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task DeleteAsync_WhenUserExists_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.DeleteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _service.DeleteAsync(userId);

        // Assert
        result.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenUserDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.DeleteAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _service.DeleteAsync(userId);

        // Assert
        result.Should().BeFalse();
        await _repository.Received(1).DeleteAsync(userId, Arg.Any<CancellationToken>());
    }
}

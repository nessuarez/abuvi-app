using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.Users;

public class UsersServiceUpdateTests
{
    private readonly IUsersRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRoleChangeLogsRepository _auditRepository;
    private readonly UsersService _service;

    public UsersServiceUpdateTests()
    {
        _repository = Substitute.For<IUsersRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _auditRepository = Substitute.For<IUserRoleChangeLogsRepository>();
        _service = new UsersService(_repository, _passwordHasher, _auditRepository);
    }

    #region IsActive protection

    [Fact]
    public async Task UpdateAsync_AdminCaller_CanChangeIsActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, isActive: true);
        var request = new UpdateUserRequest("First", "Last", null, false);

        _repository.GetByIdAsync(userId, default).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), default).Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _service.UpdateAsync(userId, request, "Admin");

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(Arg.Is<User>(u => u.IsActive == false), default);
    }

    [Fact]
    public async Task UpdateAsync_BoardCaller_CanChangeIsActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, isActive: true);
        var request = new UpdateUserRequest("First", "Last", null, false);

        _repository.GetByIdAsync(userId, default).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), default).Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _service.UpdateAsync(userId, request, "Board");

        // Assert
        result!.IsActive.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(Arg.Is<User>(u => u.IsActive == false), default);
    }

    [Fact]
    public async Task UpdateAsync_MemberCaller_CannotChangeIsActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, isActive: true);
        var request = new UpdateUserRequest("First", "Last", null, IsActive: false);

        _repository.GetByIdAsync(userId, default).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), default).Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _service.UpdateAsync(userId, request, "Member");

        // Assert — IsActive stays true despite request saying false
        result!.IsActive.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Is<User>(u => u.IsActive == true), default);
    }

    [Fact]
    public async Task UpdateAsync_NullCallerRole_CannotChangeIsActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, isActive: true);
        var request = new UpdateUserRequest("First", "Last", null, IsActive: false);

        _repository.GetByIdAsync(userId, default).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), default).Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _service.UpdateAsync(userId, request, callerRole: null);

        // Assert
        result!.IsActive.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Is<User>(u => u.IsActive == true), default);
    }

    #endregion

    #region Field persistence

    [Fact]
    public async Task UpdateAsync_UpdatesNameAndPhone()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var request = new UpdateUserRequest("NewFirst", "NewLast", "+34612345678", true);

        _repository.GetByIdAsync(userId, default).Returns(user);
        _repository.UpdateAsync(Arg.Any<User>(), default).Returns(callInfo => callInfo.Arg<User>());

        // Act
        var result = await _service.UpdateAsync(userId, request, "Admin");

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("NewFirst");
        result.LastName.Should().Be("NewLast");
        result.Phone.Should().Be("+34612345678");
        await _repository.Received(1).UpdateAsync(
            Arg.Is<User>(u => u.FirstName == "NewFirst" && u.LastName == "NewLast" && u.Phone == "+34612345678"),
            default);
    }

    #endregion

    #region Not found

    [Fact]
    public async Task UpdateAsync_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateUserRequest("First", "Last", null, true);

        _repository.GetByIdAsync(userId, default).ReturnsNull();

        // Act
        var result = await _service.UpdateAsync(userId, request, "Admin");

        // Assert
        result.Should().BeNull();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<User>(), default);
    }

    #endregion

    #region Helpers

    private static User CreateUser(Guid id, bool isActive = true) => new()
    {
        Id = id,
        Email = "test@example.com",
        PasswordHash = "hash",
        FirstName = "Old",
        LastName = "Name",
        Phone = null,
        Role = UserRole.Member,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    #endregion
}

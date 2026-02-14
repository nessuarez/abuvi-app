using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using NSubstitute;

namespace Abuvi.Tests.Unit.Features.Users;

/// <summary>
/// Unit tests for UsersService.UpdateRoleAsync method
/// Tests cover authorization, security checks, and audit logging
/// </summary>
public class UsersServiceRoleUpdateTests
{
    private readonly IUsersRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRoleChangeLogsRepository _auditRepository;
    private readonly UsersService _service;

    public UsersServiceRoleUpdateTests()
    {
        _repository = Substitute.For<IUsersRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _auditRepository = Substitute.For<IUserRoleChangeLogsRepository>();
        _service = new UsersService(_repository, _passwordHasher, _auditRepository);
    }

    #region Admin Authorization Tests

    [Fact]
    public async Task UpdateRoleAsync_AdminChangingMemberToBoard_ShouldSucceed()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = CreateUser(adminId, "admin@test.com", UserRole.Admin);
        var targetUser = CreateUser(targetUserId, "member@test.com", UserRole.Member);
        var updatedUser = CreateUser(targetUserId, "member@test.com", UserRole.Board);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(adminId, default)
            .Returns(adminUser);
        _repository.UpdateAsync(Arg.Any<User>(), default)
            .Returns(updatedUser);
        _auditRepository.LogRoleChangeAsync(Arg.Any<UserRoleChangeLog>(), default)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, "Promotion", "192.168.1.1");

        // Assert
        result.Should().NotBeNull();
        result!.Role.Should().Be(UserRole.Board);
        await _repository.Received(1).UpdateAsync(Arg.Is<User>(u => u.Role == UserRole.Board), default);
        await _auditRepository.Received(1).LogRoleChangeAsync(
            Arg.Is<UserRoleChangeLog>(log =>
                log.UserId == targetUserId &&
                log.ChangedByUserId == adminId &&
                log.PreviousRole == UserRole.Member &&
                log.NewRole == UserRole.Board &&
                log.Reason == "Promotion" &&
                log.IpAddress == "192.168.1.1"),
            default);
    }

    [Fact]
    public async Task UpdateRoleAsync_AdminChangingAdminToMember_ShouldSucceed()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = CreateUser(adminId, "admin@test.com", UserRole.Admin);
        var targetUser = CreateUser(targetUserId, "otheradmin@test.com", UserRole.Admin);
        var updatedUser = CreateUser(targetUserId, "otheradmin@test.com", UserRole.Member);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(adminId, default)
            .Returns(adminUser);
        _repository.UpdateAsync(Arg.Any<User>(), default)
            .Returns(updatedUser);
        _auditRepository.LogRoleChangeAsync(Arg.Any<UserRoleChangeLog>(), default)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateRoleAsync(targetUserId, UserRole.Member, adminId, "Demotion");

        // Assert
        result.Should().NotBeNull();
        result!.Role.Should().Be(UserRole.Member);
        await _repository.Received(1).UpdateAsync(Arg.Any<User>(), default);
    }

    #endregion

    #region Board Authorization Tests

    [Fact]
    public async Task UpdateRoleAsync_BoardChangingMemberRole_ShouldSucceed()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var boardUser = CreateUser(boardId, "board@test.com", UserRole.Board);
        var targetUser = CreateUser(targetUserId, "member@test.com", UserRole.Member);
        var updatedUser = CreateUser(targetUserId, "member@test.com", UserRole.Member);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(boardId, default)
            .Returns(boardUser);
        _repository.UpdateAsync(Arg.Any<User>(), default)
            .Returns(updatedUser);
        _auditRepository.LogRoleChangeAsync(Arg.Any<UserRoleChangeLog>(), default)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateRoleAsync(targetUserId, UserRole.Member, boardId, "Update");

        // Assert
        result.Should().NotBeNull();
        await _repository.Received(1).UpdateAsync(Arg.Any<User>(), default);
    }

    [Fact]
    public async Task UpdateRoleAsync_BoardChangingAdminRole_ShouldThrowUnauthorizedAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var boardUser = CreateUser(boardId, "board@test.com", UserRole.Board);
        var targetUser = CreateUser(targetUserId, "admin@test.com", UserRole.Admin);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(boardId, default)
            .Returns(boardUser);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Member, boardId, "Unauthorized");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Privilegios insuficientes para cambiar este rol");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<User>(), default);
        await _auditRepository.DidNotReceive().LogRoleChangeAsync(Arg.Any<UserRoleChangeLog>(), default);
    }

    [Fact]
    public async Task UpdateRoleAsync_BoardChangingOtherBoardRole_ShouldThrowUnauthorizedAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var boardUser = CreateUser(boardId, "board@test.com", UserRole.Board);
        var targetUser = CreateUser(targetUserId, "otherboard@test.com", UserRole.Board);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(boardId, default)
            .Returns(boardUser);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Member, boardId, "Unauthorized");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Privilegios insuficientes para cambiar este rol");
    }

    [Fact]
    public async Task UpdateRoleAsync_BoardPromotingMemberToBoard_ShouldThrowUnauthorizedAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var boardUser = CreateUser(boardId, "board@test.com", UserRole.Board);
        var targetUser = CreateUser(targetUserId, "member@test.com", UserRole.Member);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(boardId, default)
            .Returns(boardUser);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Board, boardId, "Promotion");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Privilegios insuficientes para cambiar este rol");
    }

    #endregion

    #region Member Authorization Tests

    [Fact]
    public async Task UpdateRoleAsync_MemberChangingAnyRole_ShouldThrowUnauthorizedAccess()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var memberUser = CreateUser(memberId, "member@test.com", UserRole.Member);
        var targetUser = CreateUser(targetUserId, "othermember@test.com", UserRole.Member);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(memberId, default)
            .Returns(memberUser);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Board, memberId, "Unauthorized");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Privilegios insuficientes para cambiar este rol");
    }

    #endregion

    #region Self-Role Change Prevention Tests

    [Fact]
    public async Task UpdateRoleAsync_UserChangingOwnRole_ShouldThrowInvalidOperation()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var act = async () => await _service.UpdateRoleAsync(userId, UserRole.Admin, userId, "Self-change");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Users cannot change their own role");
        await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), default);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<User>(), default);
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task UpdateRoleAsync_TargetUserNotFound_ShouldReturnNull()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _repository.GetByIdAsync(targetUserId, default)
            .Returns((User?)null);

        // Act
        var result = await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, "Test");

        // Assert
        result.Should().BeNull();
        await _repository.DidNotReceive().GetByIdAsync(adminId, default);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<User>(), default);
    }

    [Fact]
    public async Task UpdateRoleAsync_RequestingUserNotFound_ShouldThrowInvalidOperation()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var targetUser = CreateUser(targetUserId, "target@test.com", UserRole.Member);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(adminId, default)
            .Returns((User?)null);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Requesting user not found");
    }

    #endregion

    #region Audit Trail Tests

    [Fact]
    public async Task UpdateRoleAsync_Success_ShouldCreateAuditLogWithAllDetails()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        const string reason = "Performance improvement";
        const string ipAddress = "10.0.0.5";

        var adminUser = CreateUser(adminId, "admin@test.com", UserRole.Admin);
        var targetUser = CreateUser(targetUserId, "member@test.com", UserRole.Member);
        var updatedUser = CreateUser(targetUserId, "member@test.com", UserRole.Board);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(adminId, default)
            .Returns(adminUser);
        _repository.UpdateAsync(Arg.Any<User>(), default)
            .Returns(updatedUser);
        _auditRepository.LogRoleChangeAsync(Arg.Any<UserRoleChangeLog>(), default)
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, reason, ipAddress);

        // Assert
        await _auditRepository.Received(1).LogRoleChangeAsync(
            Arg.Is<UserRoleChangeLog>(log =>
                log.UserId == targetUserId &&
                log.ChangedByUserId == adminId &&
                log.PreviousRole == UserRole.Member &&
                log.NewRole == UserRole.Board &&
                log.Reason == reason &&
                log.IpAddress == ipAddress),
            default);
    }

    [Fact]
    public async Task UpdateRoleAsync_NoIpAddress_ShouldUseUnknownInAuditLog()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = CreateUser(adminId, "admin@test.com", UserRole.Admin);
        var targetUser = CreateUser(targetUserId, "member@test.com", UserRole.Member);
        var updatedUser = CreateUser(targetUserId, "member@test.com", UserRole.Board);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(adminId, default)
            .Returns(adminUser);
        _repository.UpdateAsync(Arg.Any<User>(), default)
            .Returns(updatedUser);
        _auditRepository.LogRoleChangeAsync(Arg.Any<UserRoleChangeLog>(), default)
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, ipAddress: null);

        // Assert
        await _auditRepository.Received(1).LogRoleChangeAsync(
            Arg.Is<UserRoleChangeLog>(log => log.IpAddress == "Unknown"),
            default);
    }

    [Fact]
    public async Task UpdateRoleAsync_NoReason_ShouldCreateAuditLogWithNullReason()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = CreateUser(adminId, "admin@test.com", UserRole.Admin);
        var targetUser = CreateUser(targetUserId, "member@test.com", UserRole.Member);
        var updatedUser = CreateUser(targetUserId, "member@test.com", UserRole.Board);

        _repository.GetByIdAsync(targetUserId, default)
            .Returns(targetUser);
        _repository.GetByIdAsync(adminId, default)
            .Returns(adminUser);
        _repository.UpdateAsync(Arg.Any<User>(), default)
            .Returns(updatedUser);
        _auditRepository.LogRoleChangeAsync(Arg.Any<UserRoleChangeLog>(), default)
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, reason: null);

        // Assert
        await _auditRepository.Received(1).LogRoleChangeAsync(
            Arg.Is<UserRoleChangeLog>(log => log.Reason == null),
            default);
    }

    #endregion

    #region Helper Methods

    private static User CreateUser(Guid id, string email, UserRole role)
    {
        return new User
        {
            Id = id,
            Email = email,
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}

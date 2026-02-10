using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Moq;

namespace Abuvi.Tests.Unit.Features.Users;

/// <summary>
/// Unit tests for UsersService.UpdateRoleAsync method
/// Tests cover authorization, security checks, and audit logging
/// </summary>
public class UsersServiceRoleUpdateTests
{
    private readonly Mock<IUsersRepository> _mockRepository;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<IUserRoleChangeLogsRepository> _mockAuditRepository;
    private readonly UsersService _service;

    public UsersServiceRoleUpdateTests()
    {
        _mockRepository = new Mock<IUsersRepository>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockAuditRepository = new Mock<IUserRoleChangeLogsRepository>();
        _service = new UsersService(_mockRepository.Object, _mockPasswordHasher.Object, _mockAuditRepository.Object);
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

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(adminId, default))
            .ReturnsAsync(adminUser);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync(updatedUser);
        _mockAuditRepository.Setup(r => r.LogRoleChangeAsync(It.IsAny<UserRoleChangeLog>(), default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, "Promotion", "192.168.1.1");

        // Assert
        result.Should().NotBeNull();
        result!.Role.Should().Be(UserRole.Board);
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Role == UserRole.Board), default), Times.Once);
        _mockAuditRepository.Verify(r => r.LogRoleChangeAsync(
            It.Is<UserRoleChangeLog>(log =>
                log.UserId == targetUserId &&
                log.ChangedByUserId == adminId &&
                log.PreviousRole == UserRole.Member &&
                log.NewRole == UserRole.Board &&
                log.Reason == "Promotion" &&
                log.IpAddress == "192.168.1.1"),
            default),
            Times.Once);
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

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(adminId, default))
            .ReturnsAsync(adminUser);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync(updatedUser);
        _mockAuditRepository.Setup(r => r.LogRoleChangeAsync(It.IsAny<UserRoleChangeLog>(), default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateRoleAsync(targetUserId, UserRole.Member, adminId, "Demotion");

        // Assert
        result.Should().NotBeNull();
        result!.Role.Should().Be(UserRole.Member);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), default), Times.Once);
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

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(boardId, default))
            .ReturnsAsync(boardUser);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync(updatedUser);
        _mockAuditRepository.Setup(r => r.LogRoleChangeAsync(It.IsAny<UserRoleChangeLog>(), default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateRoleAsync(targetUserId, UserRole.Member, boardId, "Update");

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), default), Times.Once);
    }

    [Fact]
    public async Task UpdateRoleAsync_BoardChangingAdminRole_ShouldThrowUnauthorizedAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var boardUser = CreateUser(boardId, "board@test.com", UserRole.Board);
        var targetUser = CreateUser(targetUserId, "admin@test.com", UserRole.Admin);

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(boardId, default))
            .ReturnsAsync(boardUser);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Member, boardId, "Unauthorized");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Insufficient privileges to change this role");
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), default), Times.Never);
        _mockAuditRepository.Verify(r => r.LogRoleChangeAsync(It.IsAny<UserRoleChangeLog>(), default), Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_BoardChangingOtherBoardRole_ShouldThrowUnauthorizedAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var boardUser = CreateUser(boardId, "board@test.com", UserRole.Board);
        var targetUser = CreateUser(targetUserId, "otherboard@test.com", UserRole.Board);

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(boardId, default))
            .ReturnsAsync(boardUser);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Member, boardId, "Unauthorized");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Insufficient privileges to change this role");
    }

    [Fact]
    public async Task UpdateRoleAsync_BoardPromotingMemberToBoard_ShouldThrowUnauthorizedAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var boardUser = CreateUser(boardId, "board@test.com", UserRole.Board);
        var targetUser = CreateUser(targetUserId, "member@test.com", UserRole.Member);

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(boardId, default))
            .ReturnsAsync(boardUser);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Board, boardId, "Promotion");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Insufficient privileges to change this role");
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

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(memberId, default))
            .ReturnsAsync(memberUser);

        // Act
        var act = async () => await _service.UpdateRoleAsync(targetUserId, UserRole.Board, memberId, "Unauthorized");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Insufficient privileges to change this role");
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
        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), default), Times.Never);
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task UpdateRoleAsync_TargetUserNotFound_ShouldReturnNull()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, "Test");

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.GetByIdAsync(adminId, default), Times.Never);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_RequestingUserNotFound_ShouldThrowInvalidOperation()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var targetUser = CreateUser(targetUserId, "target@test.com", UserRole.Member);

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(adminId, default))
            .ReturnsAsync((User?)null);

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

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(adminId, default))
            .ReturnsAsync(adminUser);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync(updatedUser);
        _mockAuditRepository.Setup(r => r.LogRoleChangeAsync(It.IsAny<UserRoleChangeLog>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, reason, ipAddress);

        // Assert
        _mockAuditRepository.Verify(r => r.LogRoleChangeAsync(
            It.Is<UserRoleChangeLog>(log =>
                log.UserId == targetUserId &&
                log.ChangedByUserId == adminId &&
                log.PreviousRole == UserRole.Member &&
                log.NewRole == UserRole.Board &&
                log.Reason == reason &&
                log.IpAddress == ipAddress),
            default),
            Times.Once);
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

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(adminId, default))
            .ReturnsAsync(adminUser);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync(updatedUser);
        _mockAuditRepository.Setup(r => r.LogRoleChangeAsync(It.IsAny<UserRoleChangeLog>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, ipAddress: null);

        // Assert
        _mockAuditRepository.Verify(r => r.LogRoleChangeAsync(
            It.Is<UserRoleChangeLog>(log => log.IpAddress == "Unknown"),
            default),
            Times.Once);
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

        _mockRepository.Setup(r => r.GetByIdAsync(targetUserId, default))
            .ReturnsAsync(targetUser);
        _mockRepository.Setup(r => r.GetByIdAsync(adminId, default))
            .ReturnsAsync(adminUser);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync(updatedUser);
        _mockAuditRepository.Setup(r => r.LogRoleChangeAsync(It.IsAny<UserRoleChangeLog>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateRoleAsync(targetUserId, UserRole.Board, adminId, reason: null);

        // Assert
        _mockAuditRepository.Verify(r => r.LogRoleChangeAsync(
            It.Is<UserRoleChangeLog>(log => log.Reason == null),
            default),
            Times.Once);
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

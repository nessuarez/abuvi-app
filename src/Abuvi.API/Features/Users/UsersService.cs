using Abuvi.API.Features.Auth;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Service for handling user business logic
/// </summary>
public class UsersService(
    IUsersRepository repository,
    IPasswordHasher passwordHasher,
    IUserRoleChangeLogsRepository auditLogRepository)
{
    /// <summary>
    /// Gets a user by ID
    /// </summary>
    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(id, cancellationToken);
        return user is null ? null : MapToResponse(user);
    }

    /// <summary>
    /// Gets all users with pagination
    /// </summary>
    public async Task<List<UserResponse>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        var users = await repository.GetAllAsync(skip, take, cancellationToken);
        return users.Select(MapToResponse).ToList();
    }

    /// <summary>
    /// Creates a new user
    /// </summary>
    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        if (await repository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new InvalidOperationException("Ya existe un usuario con este correo electrónico");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Role = request.Role,
            IsActive = true
        };

        var createdUser = await repository.CreateAsync(user, cancellationToken);
        return MapToResponse(createdUser);
    }

    /// <summary>
    /// Updates an existing user
    /// </summary>
    public async Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return null;

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Phone = request.Phone;
        user.IsActive = request.IsActive;

        var updatedUser = await repository.UpdateAsync(user, cancellationToken);
        return MapToResponse(updatedUser);
    }

    /// <summary>
    /// Deletes a user
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await repository.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Updates a user's role with security checks and audit logging
    /// </summary>
    /// <param name="targetUserId">The ID of the user whose role will be changed</param>
    /// <param name="newRole">The new role to assign</param>
    /// <param name="requestingUserId">The ID of the user making the change</param>
    /// <param name="reason">Optional reason for the change</param>
    /// <param name="ipAddress">IP address of the requester for audit trail</param>
    public async Task<UserResponse?> UpdateRoleAsync(
        Guid targetUserId,
        UserRole newRole,
        Guid requestingUserId,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Prevent self-role changes
        if (targetUserId == requestingUserId)
            throw new InvalidOperationException("Los usuarios no pueden cambiar su propio rol");

        // 2. Get target user
        var user = await repository.GetByIdAsync(targetUserId, cancellationToken);
        if (user is null)
            return null;

        // 3. Get requesting user for authorization check
        var requestingUser = await repository.GetByIdAsync(requestingUserId, cancellationToken);
        if (requestingUser is null)
            throw new InvalidOperationException("No se encontró el usuario solicitante");

        // 4. Validate authorization
        if (!CanChangeRole(requestingUser.Role, user.Role, newRole))
            throw new UnauthorizedAccessException("Privilegios insuficientes para cambiar este rol");

        // 5. Store previous role for audit
        var previousRole = user.Role;

        // 6. Update role
        user.Role = newRole;
        user.UpdatedAt = DateTime.UtcNow;
        var updatedUser = await repository.UpdateAsync(user, cancellationToken);

        // 7. Create audit log entry
        await auditLogRepository.LogRoleChangeAsync(new UserRoleChangeLog
        {
            UserId = targetUserId,
            ChangedByUserId = requestingUserId,
            PreviousRole = previousRole,
            NewRole = newRole,
            Reason = reason,
            IpAddress = ipAddress ?? "Unknown",
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);

        return MapToResponse(updatedUser);
    }

    /// <summary>
    /// Determines if a user can change another user's role
    /// </summary>
    private static bool CanChangeRole(UserRole requestingRole, UserRole currentRole, UserRole newRole)
    {
        // Admin can change any role
        if (requestingRole == UserRole.Admin)
            return true;

        // Board can only change Member roles (not their own, not Admin, not other Board members)
        if (requestingRole == UserRole.Board)
            return currentRole == UserRole.Member && newRole == UserRole.Member;

        // Members cannot change roles
        return false;
    }

    /// <summary>
    /// Maps User entity to UserResponse DTO
    /// </summary>
    private static UserResponse MapToResponse(User user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Phone,
        user.Role,
        user.IsActive,
        user.EmailVerified,
        user.CreatedAt,
        user.UpdatedAt
    );
}

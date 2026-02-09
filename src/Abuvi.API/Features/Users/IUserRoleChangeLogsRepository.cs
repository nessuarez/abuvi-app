namespace Abuvi.API.Features.Users;

/// <summary>
/// Repository interface for UserRoleChangeLog audit operations
/// </summary>
public interface IUserRoleChangeLogsRepository
{
    /// <summary>
    /// Logs a role change event
    /// </summary>
    Task LogRoleChangeAsync(UserRoleChangeLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets role change history for a specific user
    /// </summary>
    Task<List<UserRoleChangeLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

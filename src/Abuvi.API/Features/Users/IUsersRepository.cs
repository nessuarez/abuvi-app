namespace Abuvi.API.Features.Users;

/// <summary>
/// Repository interface for User data access operations
/// </summary>
public interface IUsersRepository
{
    /// <summary>
    /// Gets a user by their unique identifier
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email address
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email verification token
    /// </summary>
    Task<User?> GetByVerificationTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their password reset token
    /// </summary>
    Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a filtered, sorted page of users and the total matching count
    /// </summary>
    Task<(List<User> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, UserRole? role, bool? isActive,
        string? sortBy, string? sortOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user
    /// </summary>
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user
    /// </summary>
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user by their unique identifier
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user with the given email already exists
    /// </summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}

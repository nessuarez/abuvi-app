namespace Abuvi.API.Features.Users;

/// <summary>
/// Extension methods for User entity
/// </summary>
public static class UserExtensions
{
    /// <summary>
    /// Converts User entity to UserResponse DTO
    /// </summary>
    public static UserResponse ToResponse(this User user) => new(
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

namespace Abuvi.API.Features.Users;

/// <summary>
/// User entity representing a platform account
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public Guid? FamilyUnitId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// User roles in the system
/// </summary>
public enum UserRole
{
    Admin,
    Board,
    Member
}

/// <summary>
/// Request to create a new user
/// </summary>
public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    UserRole Role
);

/// <summary>
/// Request to update an existing user
/// </summary>
public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? Phone,
    bool IsActive
);

/// <summary>
/// User response DTO
/// </summary>
public record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

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

    // NEW FIELDS FOR EMAIL VERIFICATION
    public string? DocumentNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Member;
    public Guid? FamilyUnitId { get; set; }
    public bool IsActive { get; set; } = false; // Changed default to false
    public bool EmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }

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
/// Request to update a user's role (Admin/Board only)
/// </summary>
public record UpdateUserRoleRequest(
    UserRole NewRole,
    string? Reason  // Optional reason for the role change (for audit purposes)
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
    bool EmailVerified, // NEW: Email verification status
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Audit log entry for user role changes
/// </summary>
public class UserRoleChangeLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public UserRole PreviousRole { get; set; }
    public UserRole NewRole { get; set; }
    public string? Reason { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}

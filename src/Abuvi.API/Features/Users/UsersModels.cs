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
    public UserRole Role { get; set; } = UserRole.Member;
    public Guid? FamilyUnitId { get; set; }
    public bool IsActive { get; set; } = false; // Changed default to false
    public bool EmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

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
/// Query parameters for the paged admin users list
/// </summary>
/// <param name="Page">1-based page number</param>
/// <param name="PageSize">Items per page (clamped to 1..100)</param>
/// <param name="Search">Case-insensitive match on email, first name, last name or full name</param>
/// <param name="Role">Optional role filter</param>
/// <param name="IsActive">Optional active-state filter</param>
/// <param name="SortBy">email | firstName | lastName | createdAt | role (default createdAt)</param>
/// <param name="SortOrder">asc | desc (default desc)</param>
public record UserListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    UserRole? Role = null,
    bool? IsActive = null,
    string? SortBy = null,
    string? SortOrder = null
);

/// <summary>
/// Paginated response envelope for the admin users list
/// </summary>
public record PagedUsersResponse(
    List<UserResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
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

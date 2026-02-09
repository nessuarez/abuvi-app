# Backend Implementation Plan: User Role Management

## Overview

Implement a secure system for updating user roles within the Abuvi platform following **Vertical Slice Architecture** with .NET 9 Minimal APIs. This feature allows administrators and board members to change user roles while maintaining strict security controls and comprehensive audit trails.

**Critical Security Considerations:**
- Users cannot change their own role (prevent privilege escalation)
- Only Admin and Board roles can update user roles
- Separate endpoint from general user update
- Complete audit trail with who, when, what, and from where (IP address)

## Architecture Context

### Feature Slice
- **Location**: `src/Abuvi.API/Features/Users/`
- **Files to Modify**:
  - `UsersModels.cs` - Add `UpdateUserRoleRequest` record and `UserRoleChangeLog` entity
  - `UsersService.cs` - Add `UpdateRoleAsync` method and `CanChangeRole` helper
  - `IUsersRepository.cs` - No changes needed (uses existing `GetByIdAsync` and `UpdateAsync`)
  - `UsersEndpoints.cs` - Add PATCH endpoint for role updates
  - `UsersValidators.cs` - Add `UpdateUserRoleRequestValidator`

### New Components
- **Entity**: `UserRoleChangeLog` - Audit log for role changes
- **Repository**: `IUserRoleChangeLogsRepository` and `UserRoleChangeLogsRepository` (in Users feature folder)
- **Entity Configuration**: `UserRoleChangeLogConfiguration` in `Data/Configurations/`
- **Extension**: `HttpContextExtensions` in `Common/Extensions/` with `GetUserId()` method

### Cross-Cutting Concerns
- **Common Extensions**: Create `HttpContextExtensions` for extracting user ID from JWT claims
- **Database Migration**: Add `UserRoleChangeLogs` table
- **Authorization**: Endpoint requires Admin or Board role
- **Audit Logging**: Every role change must be logged with full context

## Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to a new feature branch for backend implementation.

**Branch Naming**: `feature/user-role-management-backend` (REQUIRED - do not use generic ticket names)

**Implementation Steps**:
1. Ensure you're on the latest `main` branch: `git checkout main`
2. Pull latest changes: `git pull origin main`
3. Check if branch exists: `git branch -a | grep user-role-management-backend`
4. If branch doesn't exist, create it: `git checkout -b feature/user-role-management-backend`
5. If branch exists, switch to it: `git checkout feature/user-role-management-backend`
6. Verify branch: `git branch` (should show `* feature/user-role-management-backend`)

**Notes**: This MUST be the first step before any code changes. The `-backend` suffix separates backend work from frontend work or general task tracking.

### Step 1: Create UserRoleChangeLog Entity and Configuration

**File**: `src/Abuvi.API/Features/Users/UsersModels.cs`

**Action**: Add the `UserRoleChangeLog` entity class for audit trail.

**Implementation Steps**:
1. Add the following entity class to the file after existing models:

```csharp
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
```

**File**: `src/Abuvi.API/Data/Configurations/UserRoleChangeLogConfiguration.cs`

**Action**: Create EF Core configuration for the `UserRoleChangeLog` entity.

**Implementation Steps**:
1. Create new file with the following content:

```csharp
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class UserRoleChangeLogConfiguration : IEntityTypeConfiguration<UserRoleChangeLog>
{
    public void Configure(EntityTypeBuilder<UserRoleChangeLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.ChangedByUserId).IsRequired();

        builder.Property(x => x.PreviousRole)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.NewRole)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45) // IPv6 max length
            .IsRequired();

        builder.Property(x => x.ChangedAt)
            .IsRequired();

        // Indexes for efficient querying
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ChangedAt);

        // Foreign keys
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

**File**: `src/Abuvi.API/Data/AbuviDbContext.cs`

**Action**: Add `UserRoleChangeLogs` DbSet to the context.

**Implementation Steps**:
1. Add the following property to the `AbuviDbContext` class:

```csharp
public DbSet<UserRoleChangeLog> UserRoleChangeLogs => Set<UserRoleChangeLog>();
```

**Dependencies**:
- `Microsoft.EntityFrameworkCore`
- Existing `User` entity

**Implementation Notes**:
- UUID primary key with database-generated values
- Store roles as strings for readability in database
- IP address supports both IPv4 and IPv6 (max 45 characters)
- Indexes on `UserId` and `ChangedAt` for efficient audit queries
- Foreign keys with `Restrict` delete behavior to maintain audit integrity

### Step 2: Create Repository for UserRoleChangeLogs

**File**: `src/Abuvi.API/Features/Users/IUserRoleChangeLogsRepository.cs`

**Action**: Create repository interface for audit log operations.

**Implementation Steps**:
1. Create new file with the following content:

```csharp
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
```

**File**: `src/Abuvi.API/Features/Users/UserRoleChangeLogsRepository.cs`

**Action**: Implement the repository with EF Core.

**Implementation Steps**:
1. Create new file with the following content:

```csharp
using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Repository for UserRoleChangeLog audit operations
/// </summary>
public class UserRoleChangeLogsRepository(AbuviDbContext context) : IUserRoleChangeLogsRepository
{
    public async Task LogRoleChangeAsync(UserRoleChangeLog log, CancellationToken cancellationToken = default)
    {
        context.UserRoleChangeLogs.Add(log);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<UserRoleChangeLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.UserRoleChangeLogs
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.ChangedAt)
            .ToListAsync(cancellationToken);
    }
}
```

**Dependencies**:
- `AbuviDbContext`
- `Microsoft.EntityFrameworkCore`

**Implementation Notes**:
- `LogRoleChangeAsync` persists audit logs
- `GetByUserIdAsync` retrieves history sorted by most recent first
- Uses `AsNoTracking()` for read-only queries

### Step 3: Create UpdateUserRoleRequest DTO and Validator

**File**: `src/Abuvi.API/Features/Users/UsersModels.cs`

**Action**: Add the `UpdateUserRoleRequest` record.

**Implementation Steps**:
1. Add the following record after existing request DTOs:

```csharp
/// <summary>
/// Request to update a user's role (Admin/Board only)
/// </summary>
public record UpdateUserRoleRequest(
    UserRole NewRole,
    string? Reason  // Optional reason for the role change (for audit purposes)
);
```

**File**: `src/Abuvi.API/Features/Users/UsersValidators.cs`

**Action**: Add FluentValidation validator for `UpdateUserRoleRequest`.

**Implementation Steps**:
1. Add the following validator class:

```csharp
public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.NewRole)
            .IsInEnum()
            .WithMessage("Invalid role specified. Must be Admin, Board, or Member.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null)
            .WithMessage("Reason must not exceed 500 characters.");
    }
}
```

**Dependencies**:
- `FluentValidation`

**Implementation Notes**:
- Validates that `NewRole` is a valid enum value
- Optional `Reason` field capped at 500 characters
- Validation occurs automatically via `ValidationFilter<T>` endpoint filter

### Step 4: Create HttpContext Extensions for User Claims

**File**: `src/Abuvi.API/Common/Extensions/HttpContextExtensions.cs`

**Action**: Create extension methods for extracting user information from JWT claims.

**Implementation Steps**:
1. Create new file with the following content:

```csharp
using System.Security.Claims;

namespace Abuvi.API.Common.Extensions;

/// <summary>
/// Extension methods for HttpContext and ClaimsPrincipal
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the authenticated user's ID from JWT claims
    /// </summary>
    /// <param name="user">The claims principal from HttpContext.User</param>
    /// <returns>The user's Guid ID, or null if not found or invalid</returns>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return null;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Gets the authenticated user's email from JWT claims
    /// </summary>
    public static string? GetUserEmail(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Gets the authenticated user's role from JWT claims
    /// </summary>
    public static string? GetUserRole(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Role)?.Value;
    }
}
```

**Dependencies**:
- `System.Security.Claims`

**Implementation Notes**:
- Extracts user ID from `ClaimTypes.NameIdentifier` claim
- Returns `Guid?` to handle missing or invalid claims gracefully
- Additional helper methods for email and role extraction
- Can be reused across the application for any authenticated endpoint

### Step 5: Implement UpdateRoleAsync in UsersService

**File**: `src/Abuvi.API/Features/Users/UsersService.cs`

**Action**: Add role update logic with security checks and audit logging.

**Function Signature**:
```csharp
public async Task<UserResponse?> UpdateRoleAsync(
    Guid targetUserId,
    UserRole newRole,
    Guid requestingUserId,
    string? reason = null,
    string? ipAddress = null,
    CancellationToken cancellationToken = default)
```

**Implementation Steps**:

1. Update the constructor to inject `IUserRoleChangeLogsRepository`:

```csharp
public class UsersService(
    IUsersRepository repository,
    IPasswordHasher passwordHasher,
    IUserRoleChangeLogsRepository auditLogRepository)
```

2. Add the `UpdateRoleAsync` method:

```csharp
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
        throw new InvalidOperationException("Users cannot change their own role");

    // 2. Get target user
    var user = await repository.GetByIdAsync(targetUserId, cancellationToken);
    if (user is null)
        return null;

    // 3. Get requesting user for authorization check
    var requestingUser = await repository.GetByIdAsync(requestingUserId, cancellationToken);
    if (requestingUser is null)
        throw new InvalidOperationException("Requesting user not found");

    // 4. Validate authorization
    if (!CanChangeRole(requestingUser.Role, user.Role, newRole))
        throw new UnauthorizedAccessException("Insufficient privileges to change this role");

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
```

3. Add the `CanChangeRole` helper method:

```csharp
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
```

**Dependencies**:
- `IUsersRepository` (existing)
- `IUserRoleChangeLogsRepository` (new)

**Implementation Notes**:
- **Step 1**: Self-role changes are blocked to prevent privilege escalation
- **Step 2**: Validates target user exists
- **Step 3**: Validates requesting user exists (security)
- **Step 4**: Authorization logic: Admin can change any role, Board can only change Member roles
- **Step 5**: Stores previous role before update for audit
- **Step 6**: Updates the user's role and `UpdatedAt` timestamp
- **Step 7**: Creates comprehensive audit log with all context
- Returns `null` if target user not found (404)
- Throws `InvalidOperationException` for self-change (400)
- Throws `UnauthorizedAccessException` for insufficient privileges (403)

### Step 6: Add PATCH Endpoint for Role Updates

**File**: `src/Abuvi.API/Features/Users/UsersEndpoints.cs`

**Action**: Add the PATCH `/api/users/{id}/role` endpoint.

**Implementation Steps**:

1. Add using directive at the top of the file:

```csharp
using Abuvi.API.Common.Extensions;
```

2. In the `MapUsersEndpoints` method, add the new endpoint after existing endpoints:

```csharp
// PATCH /api/users/{id}/role - Update user role (Admin/Board only)
group.MapPatch("/{id:guid}/role", UpdateUserRole)
    .WithName("UpdateUserRole")
    .WithSummary("Update a user's role")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
    .AddEndpointFilter<ValidationFilter<UpdateUserRoleRequest>>()
    .Produces<ApiResponse<UserResponse>>()
    .Produces(400)  // Bad Request - validation or self-change
    .Produces(401)  // Unauthorized - not authenticated
    .Produces(403)  // Forbidden - insufficient privileges
    .Produces(404); // Not Found - user doesn't exist
```

3. Add the endpoint handler method:

```csharp
/// <summary>
/// Update a user's role
/// </summary>
private static async Task<IResult> UpdateUserRole(
    [FromRoute] Guid id,
    [FromBody] UpdateUserRoleRequest request,
    [FromServices] UsersService service,
    HttpContext httpContext,
    CancellationToken cancellationToken = default)
{
    try
    {
        // Get requesting user ID from claims
        var requestingUserId = httpContext.User.GetUserId();
        if (requestingUserId is null)
            return Results.Unauthorized();

        // Get IP address for audit trail
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        var user = await service.UpdateRoleAsync(
            id,
            request.NewRole,
            requestingUserId.Value,
            request.Reason,
            ipAddress,
            cancellationToken);

        if (user is null)
        {
            return Results.NotFound(
                ApiResponse<UserResponse>.NotFound($"User with ID {id} not found")
            );
        }

        return Results.Ok(ApiResponse<UserResponse>.Ok(user));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(
            ApiResponse<UserResponse>.Fail(ex.Message, "INVALID_OPERATION")
        );
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.StatusCode(403); // Forbidden
    }
}
```

**Dependencies**:
- `UsersService` (modified)
- `UpdateUserRoleRequest` (new)
- `ValidationFilter<T>` (existing)
- `HttpContextExtensions` (new)

**Implementation Notes**:
- Uses PATCH method (partial update) since only role is modified
- Requires authentication and Admin/Board role via policy
- Automatic validation via `ValidationFilter<UpdateUserRoleRequest>`
- Extracts requesting user ID from JWT claims
- Captures IP address for audit trail
- Returns appropriate HTTP status codes:
  - 200 OK: Role updated successfully
  - 400 Bad Request: Self-change attempt or validation failure
  - 401 Unauthorized: Not authenticated
  - 403 Forbidden: Insufficient privileges
  - 404 Not Found: Target user doesn't exist

### Step 7: Register Services in Dependency Injection

**File**: `src/Abuvi.API/Program.cs`

**Action**: Register the new repository in the DI container.

**Implementation Steps**:
1. Locate the service registration section (where other services are registered)
2. Add the following line after the `IUsersRepository` registration:

```csharp
builder.Services.AddScoped<IUserRoleChangeLogsRepository, UserRoleChangeLogsRepository>();
```

**Dependencies**:
- `IUserRoleChangeLogsRepository` and `UserRoleChangeLogsRepository`

**Implementation Notes**:
- Uses `AddScoped` lifetime (one instance per HTTP request)
- Consistent with other repository registrations
- Must be registered before building the application

### Step 8: Create EF Core Migration

**Action**: Create a database migration for the `UserRoleChangeLogs` table.

**Implementation Steps**:
1. Open a terminal in the project root
2. Run the following command:

```bash
dotnet ef migrations add AddUserRoleChangeLogsTable --project src/Abuvi.API
```

3. Review the generated migration file in `src/Abuvi.API/Data/Migrations/`
4. Verify the migration creates the table with:
   - Primary key `Id` (UUID)
   - Foreign keys to `Users` table for `UserId` and `ChangedByUserId`
   - Enum columns stored as strings
   - Indexes on `UserId` and `ChangedAt`
5. Apply the migration to the local database:

```bash
dotnet ef database update --project src/Abuvi.API
```

**Dependencies**:
- `dotnet ef` CLI tool
- PostgreSQL database running (Docker or local)

**Implementation Notes**:
- Migration must be reviewed before applying to ensure correctness
- Foreign key constraints use `RESTRICT` to maintain audit integrity
- Indexes improve query performance for audit history retrieval
- Migration should be idempotent for production deployment

### Step 9: Write Unit Tests

**File**: `tests/Abuvi.Tests/Unit/Features/Users/UsersServiceRoleUpdateTests.cs`

**Action**: Create comprehensive unit tests for `UpdateRoleAsync` method.

**Test Categories**:
1. **Successful Cases**: Admin changes any role, Board changes Member role
2. **Authorization Failures**: Board trying to change Admin/Board roles
3. **Business Rule Violations**: Self-role change attempt
4. **Not Found Cases**: Target user or requesting user not found
5. **Audit Logging**: Verify all audit details are captured

**Implementation Steps**:
1. Create the test file with comprehensive test cases following AAA pattern
2. Test scenarios:
   - `UpdateRoleAsync_AdminChangesAnyRole_Succeeds`
   - `UpdateRoleAsync_BoardChangesMemberRole_Succeeds`
   - `UpdateRoleAsync_BoardTriesToChangeAdminRole_ThrowsUnauthorizedAccessException`
   - `UpdateRoleAsync_BoardTriesToChangeBoardRole_ThrowsUnauthorizedAccessException`
   - `UpdateRoleAsync_UserChangesOwnRole_ThrowsInvalidOperationException`
   - `UpdateRoleAsync_TargetUserNotFound_ReturnsNull`
   - `UpdateRoleAsync_RequestingUserNotFound_ThrowsInvalidOperationException`
   - `UpdateRoleAsync_CreatesAuditLogWithAllDetails`
   - `UpdateRoleAsync_MemberTriesToChangeRole_ThrowsUnauthorizedAccessException`

**Dependencies**:
- `xUnit`
- `FluentAssertions`
- `NSubstitute`

**Implementation Notes**:
- Mock `IUsersRepository` and `IUserRoleChangeLogsRepository`
- Use `Arg.Any<T>()` for flexible matching
- Use `Received()` to verify method calls
- Use `ThrowsAsync()` for exception testing
- Follow naming convention: `MethodName_StateUnderTest_ExpectedBehavior`

### Step 10: Write Integration Tests

**File**: `tests/Abuvi.Tests/Integration/Features/Users/UsersRoleUpdateIntegrationTests.cs`

**Action**: Create integration tests for the role update endpoint.

**Test Categories**:
1. **Successful Role Update**: Admin changes user role
2. **Self-Change Blocked**: User attempts to change their own role
3. **Authorization Failures**: Board member tries to change Admin role
4. **Authentication**: Unauthenticated requests rejected
5. **Not Found**: Non-existent user returns 404
6. **Audit Trail**: Verify audit log entry created

**Implementation Steps**:
1. Create the test file with end-to-end API tests
2. Test scenarios:
   - `UpdateUserRole_AsAdmin_ReturnsOkWithUpdatedRole`
   - `UpdateUserRole_SelfChange_ReturnsBadRequest`
   - `UpdateUserRole_BoardChangesAdmin_ReturnsForbidden`
   - `UpdateUserRole_BoardChangesMember_ReturnsOk`
   - `UpdateUserRole_Unauthenticated_ReturnsUnauthorized`
   - `UpdateUserRole_NonExistentUser_ReturnsNotFound`
   - `UpdateUserRole_CreatesAuditLogEntry`

**Dependencies**:
- `WebApplicationFactory<Program>`
- `FluentAssertions`
- Test database (in-memory or Testcontainers)

**Implementation Notes**:
- Use `WebApplicationFactory` for full HTTP pipeline testing
- Test with actual JWT tokens
- Verify HTTP status codes
- Check response body structure
- Validate audit log persistence

### Step 11: Update Technical Documentation

**Action**: Review and update technical documentation according to changes made.

**Implementation Steps**:

1. **Review Changes**: Analyze all code changes made during implementation

2. **Update Data Model Documentation** (`ai-specs/specs/data-model.md`):
   Add `UserRoleChangeLog` entity after the User entity

3. **Update API Specification**:
   - If `api-spec.yml` is manually maintained, add the new endpoint
   - Otherwise, verify auto-generated OpenAPI documentation includes the endpoint

4. **Verify Auto-Generated Documentation**:
   - Build the project
   - Navigate to Swagger UI (typically at `/swagger`)
   - Verify the new PATCH endpoint appears with correct documentation

5. **Report Updates**: Document which files were updated

**References**:
- Follow process described in `ai-specs/specs/documentation-standards.mdc`
- All documentation must be written in English

**Notes**: This step is MANDATORY before considering the implementation complete.

## Implementation Order

1. Step 0: Create Feature Branch (`feature/user-role-management-backend`)
2. Step 1: Create `UserRoleChangeLog` Entity and Configuration
3. Step 2: Create Repository for UserRoleChangeLogs
4. Step 3: Create `UpdateUserRoleRequest` DTO and Validator
5. Step 4: Create HttpContext Extensions for User Claims
6. Step 5: Implement `UpdateRoleAsync` in UsersService
7. Step 6: Add PATCH Endpoint for Role Updates
8. Step 7: Register Services in Dependency Injection
9. Step 8: Create EF Core Migration
10. Step 9: Write Unit Tests
11. Step 10: Write Integration Tests
12. Step 11: Update Technical Documentation

## Testing Checklist

### Unit Tests (xUnit + FluentAssertions + NSubstitute)
- [ ] Admin can change any role (Member → Admin, Member → Board, Board → Admin, etc.)
- [ ] Board can change Member roles only
- [ ] Board cannot change Admin or Board roles
- [ ] Users cannot change their own role (throws `InvalidOperationException`)
- [ ] Audit log is created for each role change with all details
- [ ] Returns null when target user not found
- [ ] Throws `InvalidOperationException` when requesting user not found
- [ ] Throws `UnauthorizedAccessException` when insufficient privileges
- [ ] UpdatedAt timestamp is updated

### Integration Tests (WebApplicationFactory)
- [ ] End-to-end role update via API endpoint returns 200 OK
- [ ] Authorization correctly blocks unauthorized users (403 Forbidden)
- [ ] Self-role change blocked (400 Bad Request)
- [ ] Unauthenticated requests rejected (401 Unauthorized)
- [ ] Non-existent user returns 404 Not Found
- [ ] Audit trail correctly stored in database
- [ ] IP address captured in audit log

### Manual Verification
- [ ] Swagger documentation shows new endpoint
- [ ] PATCH `/api/users/{id}/role` appears in OpenAPI spec
- [ ] Request validation works (invalid role enum returns 400)
- [ ] Reason field accepts up to 500 characters
- [ ] Database migration creates table with correct schema
- [ ] Foreign key constraints enforce referential integrity

## Error Response Format

All endpoints use the standard `ApiResponse<T>` envelope:

### Success (200 OK)
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+1234567890",
    "role": "Board",
    "isActive": true,
    "createdAt": "2026-01-15T10:30:00Z",
    "updatedAt": "2026-02-09T22:45:00Z"
  },
  "error": null
}
```

### Bad Request (400) - Self Role Change
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Users cannot change their own role",
    "code": "INVALID_OPERATION"
  }
}
```

### Forbidden (403)
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Insufficient privileges",
    "code": "FORBIDDEN"
  }
}
```

### Not Found (404)
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "User with ID {id} not found",
    "code": "NOT_FOUND"
  }
}
```

## Dependencies

### NuGet Packages (Already Installed)
- `Microsoft.EntityFrameworkCore`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `FluentValidation`
- `Microsoft.AspNetCore.Authentication.JwtBearer`

### EF Core Migration Commands
```bash
dotnet ef migrations add AddUserRoleChangeLogsTable --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

## Notes

### Security Considerations
⚠️ **CRITICAL**: This feature has significant security implications:
1. **Self-Role Change Prevention**: Users must NEVER be able to change their own role
2. **Separate Endpoint**: Role update endpoint is separate from general user update
3. **Authorization Matrix**:
   - Admin: Can change any role (Admin ↔ Board ↔ Member)
   - Board: Can only modify Member roles (Member → Member)
   - Member: Cannot change any roles
4. **Audit Trail**: Every role change is logged with full context

### Business Rules
- Only Admin and Board roles can access the role update endpoint
- Users cannot escalate their own privileges
- Board members have limited authority
- All role transitions are allowed for Admin users
- Audit logs are immutable

### Language Requirements
- All code, comments, and documentation in English
- Database enum values stored as English strings

## Next Steps After Implementation

1. **Code Review**: Request review focusing on security aspects
2. **Manual Testing**: Test all scenarios in staging with real JWT tokens
3. **Documentation Review**: Ensure all documentation is updated
4. **Deployment**: Deploy to staging first, then production

## Implementation Verification

### Code Quality
- [ ] C# analyzers pass with no warnings
- [ ] Nullable reference types properly configured
- [ ] Primary constructors used for dependency injection
- [ ] Records used for DTOs
- [ ] Async/await used consistently

### Functionality
- [ ] PATCH endpoint registered and accessible
- [ ] Endpoint returns correct HTTP status codes
- [ ] Authorization policy correctly restricts access
- [ ] Self-role change blocked at service level
- [ ] Board members cannot modify Admin/Board roles
- [ ] Audit logs created with complete information

### Testing
- [ ] Unit tests cover all business logic scenarios
- [ ] Integration tests verify end-to-end functionality
- [ ] Test coverage meets 90% threshold
- [ ] All tests pass locally

### Database
- [ ] EF Core migration created successfully
- [ ] Migration applied without errors
- [ ] Table exists with correct schema
- [ ] Foreign key constraints properly configured
- [ ] Indexes created

### Documentation
- [ ] Data model updated
- [ ] API specification updated
- [ ] Code comments added

---

**Implementation Status**: 📋 Ready for Implementation
**Priority**: High
**Security Impact**: Critical
**Estimated Effort**: 2-3 days
**Branch**: `feature/user-role-management-backend`

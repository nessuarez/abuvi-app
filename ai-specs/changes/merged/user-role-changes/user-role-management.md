# Feature: User Role Management

## Overview

Implement a secure system for updating user roles within the Abuvi platform. This feature allows administrators and board members to change user roles while maintaining strict security controls and audit trails.

## Current State

- ✅ Roles can be assigned when creating a new user (`CreateUserRequest`)
- ❌ Roles **cannot** be updated for existing users
- Current roles: `Admin`, `Board`, `Member`

## Requirements

### Functional Requirements

1. **Role Update Capability**
   - Administrators must be able to change any user's role
   - Board members must be able to change Member roles (not Admin or other Board members)
   - Users cannot change their own role
   - System must prevent elevation of privileges without proper authorization

2. **Supported Role Transitions**
   - Admin can change any role to any other role
   - Board can change Member ↔ Member only
   - No user can change their own role (prevent self-elevation)

### Security Requirements

⚠️ **CRITICAL SECURITY CONSIDERATIONS**

1. **Authorization Controls**
   - Only Admin and Board roles can update user roles
   - Users cannot update their own role (prevent privilege escalation)
   - Separate endpoint from general user update to prevent unauthorized role changes
   - Validate that the requesting user has sufficient privileges for the target role change

2. **Audit Trail**
   - Log who changed the role
   - Log when the role was changed
   - Log the previous role and new role
   - Include IP address and user agent for security auditing

3. **Validation**
   - Prevent users from changing their own role
   - Validate that the target user exists
   - Validate that the new role is a valid `UserRole` enum value
   - Prevent role changes that would violate business rules

## Proposed Implementation

### 1. Data Models

```csharp
/// <summary>
/// Request to update a user's role (Admin/Board only)
/// </summary>
public record UpdateUserRoleRequest(
    UserRole NewRole,
    string? Reason  // Optional reason for the role change (for audit purposes)
);

/// <summary>
/// Audit log entry for role changes
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

### 2. Service Layer

```csharp
// In UsersService.cs

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

    // Board can only change Member roles
    if (requestingRole == UserRole.Board)
        return currentRole == UserRole.Member && newRole == UserRole.Member;

    // Members cannot change roles
    return false;
}
```

### 3. API Endpoint

```csharp
// In UsersEndpoints.cs

// PATCH /api/users/{id}/role - Update user role (Admin/Board only)
group.MapPatch("/{id:guid}/role", UpdateUserRole)
    .WithName("UpdateUserRole")
    .WithSummary("Update a user's role")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
    .AddEndpointFilter<ValidationFilter<UpdateUserRoleRequest>>()
    .Produces<ApiResponse<UserResponse>>()
    .Produces(400)
    .Produces(401)  // Unauthorized
    .Produces(403)  // Forbidden - insufficient privileges
    .Produces(404); // User not found

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
        return Results.Forbid();
    }
}
```

### 4. Request Validator

```csharp
// In UsersValidators.cs

public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.NewRole)
            .IsInEnum()
            .WithMessage("Invalid role specified");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null)
            .WithMessage("Reason must not exceed 500 characters");
    }
}
```

## Testing Requirements

### Unit Tests

1. ✅ Admin can change any role
2. ✅ Board can change Member roles
3. ✅ Board cannot change Admin or Board roles
4. ✅ Users cannot change their own role
5. ✅ Audit log is created for each role change
6. ✅ Returns null when user not found
7. ✅ Throws when requesting user not found

### Integration Tests

1. ✅ End-to-end role update via API endpoint
2. ✅ Authorization correctly blocks unauthorized users
3. ✅ Audit trail correctly stored in database
4. ✅ IP address captured in audit log

## Database Migration

```sql
-- Create audit log table for role changes
CREATE TABLE UserRoleChangeLogs (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NOT NULL REFERENCES Users(Id),
    ChangedByUserId UUID NOT NULL REFERENCES Users(Id),
    PreviousRole INT NOT NULL,
    NewRole INT NOT NULL,
    Reason VARCHAR(500),
    IpAddress VARCHAR(45) NOT NULL,
    ChangedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_userrolelogs_userid ON UserRoleChangeLogs(UserId);
CREATE INDEX idx_userrolelogs_changedat ON UserRoleChangeLogs(ChangedAt DESC);
```

## API Documentation Example

### Request

```http
PATCH /api/users/123e4567-e89b-12d3-a456-426614174000/role
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "newRole": "Admin",
  "reason": "Promoted to administrator for platform management"
}
```

### Response (Success)

```json
{
  "success": true,
  "data": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "email": "chachosua@gmail.com",
    "firstName": "Nestor",
    "lastName": "Suarez",
    "phone": "+1234567890",
    "role": "Admin",
    "isActive": true,
    "createdAt": "2026-01-15T10:30:00Z",
    "updatedAt": "2026-02-09T22:45:00Z"
  }
}
```

### Response (Forbidden - Self Change)

```json
{
  "success": false,
  "error": "Users cannot change their own role",
  "errorCode": "INVALID_OPERATION"
}
```

## Future Enhancements

- [ ] Role change notifications via email
- [ ] Role change approval workflow for critical roles
- [ ] Bulk role updates with CSV import
- [ ] Role change history view in admin dashboard
- [ ] Temporary role assignments with expiration dates

## Related Documentation

- [Authentication & Authorization](../phase2-authentication.md)
- [User Management API](../api/users.md)
- [Security Best Practices](../security-guidelines.md)

---

**Status**: 📋 Planned
**Priority**: High
**Security Impact**: Critical
**Estimated Effort**: 2-3 days

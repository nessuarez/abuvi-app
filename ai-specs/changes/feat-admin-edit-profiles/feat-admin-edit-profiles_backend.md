# Backend Implementation Plan: feat-admin-edit-profiles — Admin/Board Edit User, FamilyUnit, and FamilyMember Profiles

## Overview

This feature closes two authorization gaps that prevent Admin/Board from correcting profile data, and fixes a security bug in the `PUT /api/users/{id}` endpoint. The changes are confined to two existing feature slices (`Users` and `FamilyUnits`) — no new slices, no new entities, and no EF Core migrations are required.

Guiding principle: **authorization logic lives in the endpoint handler; business rule enforcement (IsActive protection) lives in the service layer as defense-in-depth.**

---

## Architecture Context

### Feature slices affected

| Slice | Path |
|---|---|
| Users | `src/Abuvi.API/Features/Users/` |
| FamilyUnits | `src/Abuvi.API/Features/FamilyUnits/` |

### Files to modify

| File | Type of change |
|---|---|
| `src/Abuvi.API/Features/Users/UsersService.cs` | Business logic: callerRole param + IsActive protection |
| `src/Abuvi.API/Features/Users/UsersEndpoints.cs` | Security fix + authorization |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Authorization bypass for Admin/Board |

### Files to create

| File | Type |
|---|---|
| `src/Abuvi.Tests/Unit/Features/Users/UsersServiceUpdateTests.cs` | Unit tests for `UpdateAsync` |
| `src/Abuvi.Tests/Unit/Features/Users/UsersEndpointsUpdateTests.cs` | Unit tests for `UpdateUser` handler |
| `src/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsEndpointsAuthTests.cs` | Unit tests for FamilyUnit/Member auth bypass |

### Cross-cutting concerns

- `HttpContextExtensions.GetUserRole()` already exists in `src/Abuvi.API/Common/Extensions/HttpContextExtensions.cs` — no changes needed.
- No new NuGet packages required.
- No EF Core migration required.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a dedicated backend branch.
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/feat-admin-edit-profiles-backend`
  3. Verify: `git branch`
- **Notes**: Never commit directly to `dev` or `main`. All PRs target `dev`.

---

### Step 1: Update `UsersService.UpdateAsync`

- **File**: `src/Abuvi.API/Features/Users/UsersService.cs`
- **Action**: Add `callerRole` parameter to `UpdateAsync`; protect `IsActive` from non-admin callers.

**Implementation Steps**:

1. **Change `UpdateAsync` signature** — add `string? callerRole` parameter:

   ```csharp
   public async Task<UserResponse?> UpdateAsync(
       Guid id,
       UpdateUserRequest request,
       string? callerRole,
       CancellationToken cancellationToken = default)
   ```

2. **Protect `IsActive`** — inside `UpdateAsync`, after the null guard:

   ```csharp
   user.FirstName = request.FirstName;
   user.LastName = request.LastName;
   user.Phone = request.Phone;

   // Only Admin/Board may change IsActive
   if (callerRole == "Admin" || callerRole == "Board")
       user.IsActive = request.IsActive;
   // else: IsActive is silently preserved
   ```

- **Implementation Notes**:
  - The `toggleUserActive` method (role-protected path) is separate and needs no changes.
  - The `callerRole` string values are `"Admin"` and `"Board"` — matching the `UserRole` enum names as strings from JWT claims.

---

### Step 2: Fix Security Bug and Add Admin/Board Support in `UsersEndpoints.cs`

- **File**: `src/Abuvi.API/Features/Users/UsersEndpoints.cs`
- **Action**: Add `HttpContext` parameter to `UpdateUser`; extract caller identity; enforce ownership check; pass `callerRole` to service.

**Current state**: `UpdateUser` has no ownership check — any authenticated `Member` can update any user profile by knowing their ID.

**Implementation Steps**:

1. **Add `HttpContext httpContext` parameter** to `UpdateUser`:

   ```csharp
   private static async Task<IResult> UpdateUser(
       [FromRoute] Guid id,
       [FromBody] UpdateUserRequest request,
       [FromServices] UsersService service,
       HttpContext httpContext,         // NEW
       CancellationToken cancellationToken = default)
   ```

2. **Extract caller identity and apply authorization**:

   ```csharp
   var requestingUserId = httpContext.User.GetUserId();
   var requestingUserRole = httpContext.User.GetUserRole();
   var isAdminOrBoard = requestingUserRole == "Admin" || requestingUserRole == "Board";

   if (requestingUserId is null)
       return Results.Unauthorized();

   if (!isAdminOrBoard && requestingUserId.Value != id)
       return Results.StatusCode(403);
   ```

3. **Pass `requestingUserRole` to service**:

   ```csharp
   var user = await service.UpdateAsync(id, request, requestingUserRole, cancellationToken);
   ```

4. **No changes needed to the `.Produces()` declarations** on the route registration — 403 is already returned via `Results.StatusCode(403)`.

- **Implementation Notes**:
  - `GetUserId()` and `GetUserRole()` are both already in `HttpContextExtensions`.
  - The endpoint is already `RequireAuthorization()` (any authenticated user), so unauthenticated callers never reach the handler body; the `requestingUserId is null` guard is defense-in-depth.
  - Return `Results.StatusCode(403)` (not `TypedResults.Forbid()`) to be consistent with the pattern used in `UpdateUserRole`.

---

### Step 3: Add Admin/Board Bypass in `FamilyUnitsEndpoints.cs`

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`
- **Action**: Modify `UpdateFamilyUnit` and `UpdateFamilyMember` handlers to allow Admin/Board callers through without the representative check.

#### `UpdateFamilyUnit` (currently ~lines 252–278)

**Current state**: returns `Forbid()` for any caller who is not the representative.

**Implementation Steps**:

1. Add `var userRole = user.GetUserRole();` immediately after extracting `userId`.
2. Add `var isAdminOrBoard = userRole == "Admin" || userRole == "Board";` inside the `try` block.
3. Change the authorization guard from:

   ```csharp
   if (!isRepresentative)
       return TypedResults.Forbid();
   ```

   to:

   ```csharp
   if (!isRepresentative && !isAdminOrBoard)
       return TypedResults.Forbid();
   ```

#### `UpdateFamilyMember` (currently ~lines 406–433)

**Same pattern** — apply identical changes:

1. Add `var userRole = user.GetUserRole();`
2. Add `var isAdminOrBoard = userRole == "Admin" || userRole == "Board";`
3. Change guard to `if (!isRepresentative && !isAdminOrBoard)`

- **Implementation Notes**:
  - `user.GetUserRole()` is already imported via `Abuvi.API.Common.Extensions` (used in `GetFamilyMembers` and `GetFamilyMemberById` in the same file).
  - The `IsRepresentativeAsync` call is still made even for Admin/Board — this is acceptable as a single DB call; no performance concern.
  - No changes to `FamilyUnitsService` or its repositories.

---

### Step 4: Build to Confirm Zero Errors

- Run: `dotnet build src/Abuvi.API/Abuvi.API.csproj`
- Expected: zero errors, zero warnings.

---

### Step 5: Write Unit Tests

#### `src/Abuvi.Tests/Unit/Features/Users/UsersServiceUpdateTests.cs`

Test class: `UsersServiceUpdateTests`

Setup: mock `IUsersRepository`, `IPasswordHasher`, `IUserRoleChangeLogsRepository`. Instantiate real `UsersService`.

Tests to write (AAA pattern, `MethodName_StateUnderTest_ExpectedBehavior`):

| Test Name | What it verifies |
|---|---|
| `UpdateAsync_AdminCaller_CanChangeIsActive` | `IsActive` is updated when `callerRole == "Admin"` |
| `UpdateAsync_BoardCaller_CanChangeIsActive` | `IsActive` is updated when `callerRole == "Board"` |
| `UpdateAsync_MemberCaller_CannotChangeIsActive` | `IsActive` remains unchanged regardless of request value when `callerRole == "Member"` |
| `UpdateAsync_NullCallerRole_CannotChangeIsActive` | `IsActive` remains unchanged when `callerRole` is `null` |
| `UpdateAsync_WhenUserNotFound_ReturnsNull` | Returns `null` when repository returns `null` |
| `UpdateAsync_UpdatesNameAndPhone` | `FirstName`, `LastName`, `Phone` are persisted and returned |

#### `src/Abuvi.Tests/Unit/Features/Users/UsersEndpointsUpdateTests.cs`

> **Note**: Minimal API handler unit testing in this project uses integration test patterns with `WebApplicationFactory`. Check `src/Abuvi.Tests/Integration/Features/` for the pattern. If an integration test approach is preferred, place these in `Integration/Features/Users/UsersEndpointsTests.cs` instead and follow the `WebApplicationFactory`-based pattern used in `CampEditionsEndpointsTests.cs` or `GuestsEndpointsTests.cs`.

Tests to write:

| Test Name | Expected HTTP status |
|---|---|
| `UpdateUser_Admin_CanUpdateAnyUserProfile` | 200 OK |
| `UpdateUser_Board_CanUpdateAnyUserProfile` | 200 OK |
| `UpdateUser_Member_CanUpdateOwnProfile` | 200 OK |
| `UpdateUser_Member_CannotUpdateOtherUserProfile` | 403 |
| `UpdateUser_Unauthenticated_Returns401` | 401 |
| `UpdateUser_MemberCaller_IsActiveNotChanged` | 200, `isActive` unchanged in response |
| `UpdateUser_AdminCaller_IsActiveChanged` | 200, `isActive` matches request |

#### `src/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsEndpointsAuthTests.cs`

Tests to write:

| Test Name | Expected result |
|---|---|
| `UpdateFamilyUnit_Admin_CanUpdate_WhenNotRepresentative` | 200 OK |
| `UpdateFamilyUnit_Board_CanUpdate_WhenNotRepresentative` | 200 OK |
| `UpdateFamilyUnit_Member_CannotUpdate_WhenNotRepresentative` | 403 |
| `UpdateFamilyMember_Admin_CanUpdate_WhenNotRepresentative` | 200 OK |
| `UpdateFamilyMember_Board_CanUpdate_WhenNotRepresentative` | 200 OK |
| `UpdateFamilyMember_Member_CannotUpdate_WhenNotRepresentative` | 403 |

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-admin-edit-profiles-backend`
2. **Step 1** — Update `UsersService.UpdateAsync` with `callerRole` and `IsActive` protection
3. **Step 2** — Fix security bug + Admin/Board support in `UsersEndpoints.UpdateUser`
4. **Step 3** — Add Admin/Board bypass in `FamilyUnitsEndpoints.UpdateFamilyUnit` and `UpdateFamilyMember`
5. **Step 4** — Build to confirm zero errors
6. **Step 5** — Write unit/integration tests

---

## Testing Checklist

- [ ] `UsersServiceUpdateTests` covers: IsActive protection per role, null callerRole, null user, field updates
- [ ] `UpdateUser` handler integration tests cover: Member→own (200), Member→other (403), Admin→other (200), Board→other (200), IsActive protection
- [ ] FamilyUnits auth tests cover: Admin/Board bypass (200), Member non-representative (403), for both `UpdateFamilyUnit` and `UpdateFamilyMember`
- [ ] `dotnet test` passes with no regressions
- [ ] 90% test coverage threshold maintained (per `backend-standards.mdc`)
- [ ] All tests follow AAA pattern and `MethodName_StateUnderTest_ExpectedBehavior` naming

---

## Error Response Format

All responses use `ApiResponse<T>`:

```json
{ "success": true, "data": { ... } }
{ "success": false, "error": "...", "code": "..." }
```

| Scenario | HTTP Status |
|---|---|
| Successful update | 200 OK |
| User/FamilyUnit not found | 404 Not Found |
| Member updating another user | 403 Forbidden |
| Unauthenticated | 401 Unauthorized |
| Validation failure | 400 Bad Request |

---

## Dependencies

No new NuGet packages required. No EF Core migrations needed.

---

## Notes

- **Security bug fix is mandatory**: the current `UpdateUser` handler has no ownership check. This must ship in the same PR.
- **`IsActive` protection is defense-in-depth**: the endpoint-level role check for Admin/Board covers the normal path; the service-level check ensures `IsActive` is never accidentally changed even if the endpoint auth is misconfigured in the future.
- **No `Email`, `Password`, or `DocumentNumber` changes**: these fields are intentionally excluded from `UpdateUserRequest`. Do not add them.
- **`RepresentativeUserId` on `FamilyUnit`** and **`UserId` on `FamilyMember`** are not changeable via these endpoints.
- **String comparison for roles** uses `"Admin"` and `"Board"` — these are the JWT claim values corresponding to `UserRole.Admin` and `UserRole.Board` enum names.
- **Language**: All code comments, test names, and documentation in English; user-facing error messages in Spanish (existing convention).

---

## Next Steps After Implementation

1. Open PR targeting `dev` branch (never `main` directly)
2. Frontend implementation tracked separately in `feat-admin-edit-profiles_frontend.md`
3. Future enhancement: audit log for Admin/Board edits to user and family data (out of scope for this ticket)

---

## Implementation Verification

- [ ] **Code Quality**: No C# nullable warnings; all new properties nullable-typed correctly
- [ ] **Functionality**: `PUT /api/users/{id}` returns 403 for Member→other; 200 for Admin→any
- [ ] **Functionality**: `PUT /api/family-units/{id}` and `PUT /api/family-units/{id}/members/{memberId}` return 200 for Admin/Board non-representatives
- [ ] **Security**: Member cannot set `IsActive` on their own profile update
- [ ] **Testing**: All new tests pass; no regressions; 90% coverage
- [ ] **Build**: `dotnet build` with zero errors and zero warnings

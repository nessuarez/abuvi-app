# Backend Implementation Plan: fix-board-users-list-access

## Overview

Fix authorization on `GET /api/users` to allow Board role access alongside Admin. This is a minimal change — the endpoint logic, service, and repository remain untouched. Only the authorization policy and related tests need updating.

**Architecture**: Vertical Slice Architecture — all changes within the `Users` feature slice (`src/Abuvi.API/Features/Users/`).

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Users/`
- **Files to modify**: `UsersEndpoints.cs` (authorization policy change)
- **Test file to modify**: `src/Abuvi.Tests/Integration/Features/ProtectedEndpointsTests.cs` (add Board role test)
- **No changes needed**: `UsersService.cs`, `UsersRepository.cs`, `UsersModels.cs`, `UsersValidators.cs`
- **No schema changes**: No EF Core migration required

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/fix-board-users-list-access-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/fix-board-users-list-access-backend`
  4. Verify branch creation: `git branch`

### Step 1: Update Authorization on `GET /api/users`

- **File**: `src/Abuvi.API/Features/Users/UsersEndpoints.cs`
- **Action**: Change `RequireRole("Admin")` to `RequireRole("Admin", "Board")` on the `GetAllUsers` endpoint
- **Implementation Steps**:
  1. On line 22, update the comment from `// GET /api/users - Get all users (Admin only)` to `// GET /api/users - Get all users (Admin/Board)`
  2. On line 26, change `.RequireAuthorization(policy => policy.RequireRole("Admin"))` to `.RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))`
- **Implementation Notes**:
  - This follows the exact same pattern already used on the `PATCH /api/users/{id}/role` endpoint (line 77): `.RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))`
  - No other endpoints change — `POST`, `PUT`, `DELETE` remain Admin-only
  - The `GetAllUsers` handler method requires no changes (no role-based filtering in the query)

### Step 2: Add Integration Tests for Board Role

- **File**: `src/Abuvi.Tests/Integration/Features/ProtectedEndpointsTests.cs`
- **Action**: Add a `GetBoardTokenAsync()` helper and a new test for Board role accessing `GET /api/users`
- **Implementation Steps**:

  1. **Add `GetBoardTokenAsync()` helper** (after `GetMemberTokenAsync()`):
     ```csharp
     private async Task<string> GetBoardTokenAsync()
     {
         var email = $"board-{Guid.NewGuid()}@example.com";
         var registerRequest = new RegisterRequest(
             Email: email,
             Password: "BoardPass123!",
             FirstName: "Board",
             LastName: "User",
             Phone: null
         );

         await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

         using var scope = _factory.Services.CreateScope();
         var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
         var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
         if (user != null)
         {
             user.Role = UserRole.Board;
             await dbContext.SaveChangesAsync();
         }

         var loginRequest = new LoginRequest(email, "BoardPass123!");
         var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
         var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);

         return loginResult!.Data!.Token;
     }
     ```

  2. **Add test inside `#region GET /api/users (List All Users)`** (after `GetAllUsers_WithAdminToken_Returns200`):
     ```csharp
     [Fact]
     public async Task GetAllUsers_WithBoardToken_Returns200()
     {
         // Arrange
         var boardToken = await GetBoardTokenAsync();
         _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", boardToken);

         // Act
         var response = await _client.GetAsync("/api/users");

         // Assert
         response.StatusCode.Should().Be(HttpStatusCode.OK);
         var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>(JsonOptions);
         result.Should().NotBeNull();
         result!.Success.Should().BeTrue();
         result.Data.Should().NotBeNull();
     }
     ```

  3. **Verify existing test `GetAllUsers_WithMemberToken_Returns403` still passes** — no changes needed to this test, it validates the regression guard.

- **Dependencies**: `UserRole.Board` enum value must exist (it does — already used in the `UpdateUserRole` endpoint authorization)
- **Implementation Notes**:
  - The `GetBoardTokenAsync()` helper follows the exact same pattern as `GetAdminTokenAsync()` — register, set role via DB, login
  - The existing `GetAllUsers_WithMemberToken_Returns403` test serves as the negative test case — Members must still be blocked

### Step 3: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **`ai-specs/specs/api-endpoints.md`**: If a Users section exists or is added, update `GET /api/users` authorization from `Admin` to `Admin | Board`
  2. Verify no other documentation files need changes (no schema/model/migration changes)
- **Notes**: This step is MANDATORY before considering the implementation complete.

## Implementation Order

1. Step 0: Create Feature Branch
2. Step 1: Update Authorization on `GET /api/users`
3. Step 2: Add Integration Tests for Board Role
4. Step 3: Update Technical Documentation

## Testing Checklist

- [ ] `GetAllUsers_WithBoardToken_Returns200` — Board user can list users (NEW)
- [ ] `GetAllUsers_WithAdminToken_Returns200` — Admin user can still list users (EXISTING, no regression)
- [ ] `GetAllUsers_WithMemberToken_Returns403` — Member user is still blocked (EXISTING, no regression)
- [ ] `GetAllUsers_WithoutToken_Returns401` — Unauthenticated requests are still blocked (EXISTING, no regression)
- [ ] All other existing tests in `ProtectedEndpointsTests.cs` continue to pass

**Run tests with**: `dotnet test src/Abuvi.Tests/ --filter "FullyQualifiedName~ProtectedEndpointsTests"`

## Error Response Format

No changes — the endpoint already uses `ApiResponse<T>` envelope. Status codes remain:
- `200 OK` — Successful (Admin or Board)
- `401 Unauthorized` — No token
- `403 Forbidden` — Member role or insufficient privileges

## Dependencies

- No new NuGet packages
- No EF Core migrations

## Notes

- **Security**: Backend is the authoritative guard. The frontend hiding of buttons (separate ticket) is UX-only.
- **Minimal change**: Only 1 line of code + 1 comment change in production code. The rest is tests.
- **Board role already exists** in `UserRole` enum and is used by `PATCH /api/users/{id}/role` — no enum changes needed.
- The `GetAllUsers` handler returns the same `UserResponse` DTO regardless of caller role — no sensitive fields like `passwordHash` are exposed (already the case).

## Next Steps After Implementation

1. Create PR targeting `dev` branch
2. Implement the **frontend ticket** (separate branch): hide Create/Delete/Toggle Active buttons for Board users in `UsersAdminPanel.vue`

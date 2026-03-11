# Fix: Board Role Cannot Access Users List in Admin Panel

## Problem Description

Users with the `Board` role receive a **"Failed to load users"** error (HTTP 403 Forbidden) when navigating to **Administración → Usuarios**. The page is accessible (sidebar shows it, route is not blocked), but the underlying API call fails because the backend restricts `GET /api/users` to `Admin` role only.

This is inconsistent with the rest of the system: Board users can already update user roles (`PATCH /api/users/{id}/role`), which logically requires them to be able to see the user list first.

## Root Cause

**Backend** — `UsersEndpoints.cs` line 26:

```csharp
// GET /api/users - Get all users (Admin only)  ← BUG: should include Board
.RequireAuthorization(policy => policy.RequireRole("Admin"))
```

**Frontend** — `UsersAdminPanel.vue` does not differentiate actions by role. Board users see "Crear Usuario", Delete, and Toggle Active buttons even though those operations are Admin-only on the backend.

## Role-Permission Matrix (Target State)

| Action | Admin | Board | Member |
|---|---|---|---|
| View Users list (`GET /api/users`) | ✅ | ✅ ← **fix** | ❌ |
| Create User (`POST /api/users`) | ✅ | ❌ | ❌ |
| Delete User (`DELETE /api/users/{id}`) | ✅ | ❌ | ❌ |
| Toggle Active/Inactive (`PUT /api/users/{id}`) | ✅ | ❌ | ❌ |
| Update Role (`PATCH /api/users/{id}/role`) | ✅ | ✅ (Members only) | ❌ |

## Implementation Plan

### 1. Backend: `src/Abuvi.API/Features/Users/UsersEndpoints.cs`

Change the authorization on `GET /api/users` from Admin-only to Admin+Board:

```csharp
// BEFORE
group.MapGet("/", GetAllUsers)
    .WithName("GetAllUsers")
    .WithSummary("Get all users")
    .RequireAuthorization(policy => policy.RequireRole("Admin"))  // ← change this
    ...

// AFTER
group.MapGet("/", GetAllUsers)
    .WithName("GetAllUsers")
    .WithSummary("Get all users")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))  // ← include Board
    ...
```

No changes required in `UsersService.cs` or `UsersRepository.cs` — the list query has no role-based filtering.

Update the comment on line 22 from `// Get all users (Admin only)` to `// Get all users (Admin/Board)`.

### 2. Frontend: `frontend/src/components/admin/UsersAdminPanel.vue`

Import `useAuthStore` and use `auth.isAdmin` to conditionally render Admin-only controls:

**"Crear Usuario" button** — hide for Board:

```vue
<Button
  v-if="auth.isAdmin"
  label="Crear Usuario"
  icon="pi pi-plus"
  @click="openCreateDialog"
/>
```

**Delete and Toggle Active buttons in the Actions column** — hide for Board:

```vue
<!-- Toggle Active: Admin only -->
<Button
  v-if="auth.isAdmin"
  :icon="data.isActive ? 'pi pi-ban' : 'pi pi-check'"
  ...
  @click="handleToggleActive(data)"
/>
<!-- Delete: Admin only -->
<Button
  v-if="auth.isAdmin"
  icon="pi pi-trash"
  ...
  @click="handleDeleteUser(data)"
/>
```

The **Actions column** should still be visible for Board users (they can use Edit Role via `UserRoleCell`). If all action buttons inside are hidden for Board, consider whether to hide the column header too — but since the role-edit button in `UserRoleCell` is also in this area, keep it visible.

Check `UserRoleCell.vue` — the edit-role affordance should remain visible for Board users (already correct since `UpdateUserRole` already allows Board).

### 3. Backend Tests: `tests/Abuvi.API.Tests/Features/Users/`

Add integration test cases for the Board role:

```csharp
[Fact]
public async Task GetAllUsers_BoardRole_Returns200()
{
    // Arrange: authenticate as Board user
    // Act: GET /api/users
    // Assert: 200 OK with user list
}

[Fact]
public async Task GetAllUsers_MemberRole_Returns403()
{
    // Existing behavior — verify Members still cannot access
}
```

### 4. Frontend Tests: `frontend/src/components/admin/__tests__/UsersAdminPanel.test.ts`

If a test file exists, add/update cases:

- Board user: list loads, "Crear Usuario" button NOT visible, Delete/Toggle NOT visible, role edit IS visible
- Admin user: all controls visible

### 5. API Documentation: `ai-specs/specs/api-endpoints.md`

Update the `GET /api/users` entry to reflect `Admin | Board` authorization:

```
Authorization: Admin | Board
```

## Files to Modify

| File | Change |
|---|---|
| `src/Abuvi.API/Features/Users/UsersEndpoints.cs` | Add "Board" to `GET /api/users` RequireRole |
| `frontend/src/components/admin/UsersAdminPanel.vue` | Conditionally hide Create/Delete/Toggle for non-Admin |
| `tests/Abuvi.API.Tests/Features/Users/UsersEndpointsTests.cs` (or equivalent) | Add Board role test cases |
| `ai-specs/specs/api-endpoints.md` | Update authorization note for GET /api/users |

## Acceptance Criteria

- [ ] Board user navigates to `/admin/users` and sees the full users list without errors
- [ ] Board user does NOT see "Crear Usuario" button
- [ ] Board user does NOT see Delete or Toggle Active buttons for any user
- [ ] Board user CAN click the role-edit affordance (`UserRoleCell`) to update a Member's role
- [ ] Admin user still sees all controls and all users
- [ ] `GET /api/users` returns 403 for `Member` role (no regression)
- [ ] Backend integration tests pass for Board role on `GET /api/users`

## Non-Functional Requirements

- **Security**: The backend is the authoritative guard. Frontend hiding of buttons is UX only — all destructive endpoints (`POST`, `DELETE`, `PUT`) retain `Admin`-only backend enforcement.
- **No data leakage**: Board users receive the same `UserResponse` DTO as Admin (no sensitive fields like `passwordHash` are included in the DTO — already the case).
- **No pagination change needed**: The existing `skip`/`take` query parameters are sufficient.

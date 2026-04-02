# Admin/Board: Edit User, FamilyUnit, and FamilyMember Profiles

## User Story

**As** a user with the Admin or Board role,
**I want** to be able to edit the profiles of Users, FamilyUnits, and FamilyMembers,
**So that** I can correct data errors (wrong names, document numbers, contact info, etc.) without depending on the representative or the user themselves.

---

## Problem Description

Currently three independent gaps prevent Admin/Board from correcting profile data:

1. **`UpdateUserRequest` is missing `DocumentNumber`** — the field exists on the `User` entity and in `UserResponse` is not exposed today either. Admin/Board cannot correct a mis-typed DNI.
2. **`PUT /api/family-units/{id}` and `PUT /api/family-units/{familyUnitId}/members/{memberId}` return 403 for Admin/Board** — both handlers only allow the representative (`IsRepresentativeAsync` check).
3. **No edit UI in `UsersAdminPanel`** — the panel only has role change, activate/deactivate, and delete. There is no dialog to edit firstName, lastName, phone, or documentNumber.
4. **`FamilyUnitPage` hides all edit controls when `isViewingOther`** — Admin/Board visiting `/family-units/:id` see a read-only view.

Additionally, **`PUT /api/users/{id}` has a security bug**: the handler currently has no ownership check — any authenticated `Member` can update any other user's profile by knowing their ID. This must be fixed as part of this work.

---

## Business Rules

### Authorization Model

| Resource | Allowed callers |
| --- | --- |
| `User` profile (firstName, lastName, phone, documentNumber) | The user themselves **or** Admin/Board |
| `FamilyUnit.Name` | Representative of that unit **or** Admin/Board |
| `FamilyMember` (all fields) | Representative of that unit **or** Admin/Board |

- Admin and Board can edit **any** resource, not just their own.
- A regular `Member` may only edit their own user profile, and their own family unit/members (as representative).
- **`IsActive` on `User`** remains Admin/Board-only. When a non-Admin/Board user calls the update endpoint for their own profile, `IsActive` must be silently preserved (not overwritten with the request value).
- **`Email` and `Password`** are not editable via this flow — email changes require a separate verification flow; password changes go through the reset flow.
- **`RepresentativeUserId`** on `FamilyUnit` cannot be changed via this endpoint — that is a separate admin action.
- **`UserId` on `FamilyMember`** cannot be changed — it is a system-managed link.

---

## Backend Changes

### 1. `src/Abuvi.API/Features/Users/UsersModels.cs`

**Extend `UpdateUserRequest`** — add `DocumentNumber`:

```csharp
public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? Phone,
    bool IsActive,
    string? DocumentNumber   // NEW
);
```

**Extend `UserResponse`** — expose `DocumentNumber` (currently missing from the DTO):

```csharp
public record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string? DocumentNumber,   // NEW
    UserRole Role,
    bool IsActive,
    bool EmailVerified,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

---

### 2. `src/Abuvi.API/Features/Users/UsersService.cs`

**`UpdateAsync`** — persist `DocumentNumber`; protect `IsActive` from non-admin callers.

Accept an additional `callerRole` parameter so the service enforces the IsActive rule:

```csharp
public async Task<UserResponse?> UpdateAsync(
    Guid id,
    UpdateUserRequest request,
    string? callerRole,   // NEW
    CancellationToken cancellationToken = default)
{
    var user = await repository.GetByIdAsync(id, cancellationToken);
    if (user is null) return null;

    user.FirstName = request.FirstName;
    user.LastName = request.LastName;
    user.Phone = request.Phone;
    user.DocumentNumber = request.DocumentNumber;   // NEW

    // Only Admin/Board may change IsActive
    if (callerRole == "Admin" || callerRole == "Board")
        user.IsActive = request.IsActive;

    var updatedUser = await repository.UpdateAsync(user, cancellationToken);
    return MapToResponse(updatedUser);
}
```

**`MapToResponse`** — include `DocumentNumber`:

```csharp
private static UserResponse MapToResponse(User user) => new(
    user.Id,
    user.Email,
    user.FirstName,
    user.LastName,
    user.Phone,
    user.DocumentNumber,   // NEW
    user.Role,
    user.IsActive,
    user.EmailVerified,
    user.CreatedAt,
    user.UpdatedAt
);
```

> `toggleUserActive` in `UsersService` is a separate method that always sets `IsActive`; it is only called from the role-protected `toggleUserActive` composable path — no change needed there.

---

### 3. `src/Abuvi.API/Features/Users/UsersEndpoints.cs`

**`UpdateUser` handler** — fix security bug and add Admin/Board support:

```csharp
private static async Task<IResult> UpdateUser(
    [FromRoute] Guid id,
    [FromBody] UpdateUserRequest request,
    [FromServices] UsersService service,
    HttpContext httpContext,   // NEW parameter
    CancellationToken cancellationToken = default)
{
    var requestingUserId = httpContext.User.GetUserId();
    var requestingUserRole = httpContext.User.GetUserRole();
    var isAdminOrBoard = requestingUserRole == "Admin" || requestingUserRole == "Board";

    if (requestingUserId is null)
        return Results.Unauthorized();

    if (!isAdminOrBoard && requestingUserId.Value != id)
        return Results.StatusCode(403);  // Member trying to update someone else

    var user = await service.UpdateAsync(id, request, requestingUserRole, cancellationToken);

    if (user is null)
        return Results.NotFound(
            ApiResponse<UserResponse>.NotFound($"User with ID {id} not found"));

    return Results.Ok(ApiResponse<UserResponse>.Ok(user));
}
```

---

### 4. `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`

**`UpdateFamilyUnit` handler** — add Admin/Board bypass (currently lines ~252–278):

```csharp
private static async Task<IResult> UpdateFamilyUnit(
    Guid id,
    UpdateFamilyUnitRequest request,
    FamilyUnitsService service,
    ClaimsPrincipal user,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var userRole = user.GetUserRole();   // NEW

    try
    {
        var isAdminOrBoard = userRole == "Admin" || userRole == "Board";   // NEW
        var isRepresentative = await service.IsRepresentativeAsync(id, userId, ct);

        if (!isRepresentative && !isAdminOrBoard)   // CHANGED
            return TypedResults.Forbid();

        var result = await service.UpdateFamilyUnitAsync(id, request, ct);
        return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

**`UpdateFamilyMember` handler** — add Admin/Board bypass (currently lines ~406–433):

```csharp
private static async Task<IResult> UpdateFamilyMember(
    Guid familyUnitId,
    Guid memberId,
    UpdateFamilyMemberRequest request,
    FamilyUnitsService service,
    ClaimsPrincipal user,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var userRole = user.GetUserRole();   // NEW

    try
    {
        var isAdminOrBoard = userRole == "Admin" || userRole == "Board";   // NEW
        var isRepresentative = await service.IsRepresentativeAsync(familyUnitId, userId, ct);

        if (!isRepresentative && !isAdminOrBoard)   // CHANGED
            return TypedResults.Forbid();

        var result = await service.UpdateFamilyMemberAsync(memberId, request, ct);
        return TypedResults.Ok(ApiResponse<FamilyMemberResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

> No changes needed to `FamilyUnitsService` or its repositories — only the authorization check in the endpoint handlers changes.

---

## Frontend Changes

### 5. `frontend/src/types/user.ts`

Add `documentNumber` to `User` and `UpdateUserRequest`. Also add `emailVerified` which exists in the backend `UserResponse` but is missing in the frontend type:

```typescript
export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  documentNumber: string | null   // NEW
  role: UserRole
  isActive: boolean
  emailVerified: boolean           // NEW (was already in backend response)
  createdAt: string
  updatedAt: string
}

export interface UpdateUserRequest {
  firstName: string
  lastName: string
  phone: string | null
  isActive: boolean
  documentNumber: string | null   // NEW
}
```

---

### 6. `frontend/src/components/users/UserForm.vue`

Add `documentNumber` field visible in **edit mode only**.

**Form data** — add `documentNumber`:

```typescript
const formData = reactive({
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  phone: '',
  documentNumber: '',   // NEW
  role: 'Member' as UserRole,
  isActive: true
})
```

**Watch init** — populate from user:

```typescript
formData.documentNumber = user.documentNumber ?? ''
```

**`handleSubmit` (edit mode)** — include in request:

```typescript
const request: UpdateUserRequest = {
  firstName: formData.firstName.trim(),
  lastName: formData.lastName.trim(),
  phone: formData.phone.trim() || null,
  isActive: formData.isActive,
  documentNumber: formData.documentNumber.trim() || null   // NEW
}
```

**Template** — add after the Phone field, before the Active status toggle:

```html
<!-- Document Number (edit mode only) -->
<div v-if="mode === 'edit'">
  <label for="documentNumber" class="mb-2 block text-sm font-medium">
    Número de documento (DNI/NIE) <span class="text-gray-400">(opcional)</span>
  </label>
  <InputText
    id="documentNumber"
    v-model="formData.documentNumber"
    class="w-full"
    placeholder="12345678A"
    data-testid="input-document-number"
  />
</div>
```

---

### 7. `frontend/src/components/admin/UsersAdminPanel.vue`

Add an "Edit profile" button per row and a dialog backed by `UserForm` in edit mode.

**Add `updateUser` to the destructured `useUsers()` call.**

**New state:**

```typescript
const showEditDialog = ref(false)
const editingUser = ref<User | null>(null)
const updatingUser = ref(false)
```

**New handlers:**

```typescript
const openEditDialog = (user: User) => {
  editingUser.value = user
  showEditDialog.value = true
  clearError()
}

const closeEditDialog = () => {
  showEditDialog.value = false
  editingUser.value = null
  clearError()
}

const handleEditUserSubmit = async (data: CreateUserRequest | UpdateUserRequest) => {
  if (!editingUser.value) return
  updatingUser.value = true
  const updated = await updateUser(editingUser.value.id, data as UpdateUserRequest)
  updatingUser.value = false
  if (updated) {
    closeEditDialog()
    toast.add({
      severity: 'success',
      summary: 'Usuario actualizado',
      detail: `${updated.firstName} ${updated.lastName} ha sido actualizado`,
      life: 5000
    })
  }
}
```

**Edit button** in the Actions column (add before the activate/deactivate button, visible to both Admin and Board):

```html
<Button
  v-if="auth.isAdmin || auth.isBoard"
  icon="pi pi-pencil"
  severity="info"
  text
  rounded
  size="small"
  aria-label="Editar perfil"
  v-tooltip.top="'Editar perfil'"
  :data-testid="`edit-user-${data.id}`"
  @click="openEditDialog(data)"
/>
```

**Edit Dialog** (add after the Create User Dialog):

```html
<!-- Edit User Dialog -->
<Dialog
  v-model:visible="showEditDialog"
  header="Editar Perfil de Usuario"
  modal
  class="w-full max-w-md"
>
  <UserForm
    v-if="editingUser"
    mode="edit"
    :user="editingUser"
    :loading="updatingUser"
    @submit="handleEditUserSubmit"
    @cancel="closeEditDialog"
  />
  <Message v-if="error" severity="error" :closable="false" class="mt-4">
    {{ error }}
  </Message>
</Dialog>
```

---

### 8. `frontend/src/views/FamilyUnitPage.vue`

Admin/Board visiting `/family-units/:id` have `isViewingOther = true` and currently see all edit controls hidden. Unlock them.

**Family unit edit/delete buttons** (currently `v-if="!isViewingOther"`, ~line 358):

```html
<!-- Before -->
<div v-if="!isViewingOther" class="flex gap-2">
  <Button icon="pi pi-pencil" label="Editar" ... @click="openEditFamilyUnitDialog" />
  <Button icon="pi pi-trash" label="Eliminar" ... @click="handleDeleteFamilyUnit" />
</div>

<!-- After: show Edit for Admin/Board too; keep Delete hidden for non-owners -->
<div v-if="!isViewingOther || (auth.isAdmin || auth.isBoard)" class="flex gap-2">
  <Button icon="pi pi-pencil" label="Editar" ... @click="openEditFamilyUnitDialog" />
  <Button
    v-if="!isViewingOther"
    icon="pi pi-trash"
    label="Eliminar"
    severity="danger"
    outlined
    @click="handleDeleteFamilyUnit"
  />
</div>
```

> Admin/Board have a separate admin delete flow (`AdminDeleteFamilyUnit`). The representative's own delete button should remain hidden when viewing as Admin to avoid confusion.

**Family member list editability** — change `:editable` binding on `FamilyMemberList`:

```html
<!-- Before -->
:editable="!isViewingOther"

<!-- After -->
:editable="!isViewingOther || (auth.isAdmin || auth.isBoard)"
```

**"Add member" button** — find any `v-if="!isViewingOther"` on the add-member button and extend it similarly:

```html
v-if="!isViewingOther || (auth.isAdmin || auth.isBoard)"
```

**Profile photo avatar** — leave read-only for Admin/Board. The profile photo belongs to the family and should not be changed by admins:

```html
:editable="!isViewingOther"   <!-- unchanged -->
```

---

## Files to Modify

### Backend

| File | Change |
| --- | --- |
| `src/Abuvi.API/Features/Users/UsersModels.cs` | Add `DocumentNumber` to `UpdateUserRequest` and `UserResponse` |
| `src/Abuvi.API/Features/Users/UsersService.cs` | Persist `DocumentNumber`; add `callerRole` param to protect `IsActive`; update `MapToResponse` |
| `src/Abuvi.API/Features/Users/UsersEndpoints.cs` | Fix security bug: add ownership/role check in `UpdateUser`; pass `callerRole` to service |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Add Admin/Board bypass to `UpdateFamilyUnit` and `UpdateFamilyMember` |

### Frontend

| File | Change |
| --- | --- |
| `frontend/src/types/user.ts` | Add `documentNumber` and `emailVerified` to `User`; add `documentNumber` to `UpdateUserRequest` |
| `frontend/src/components/users/UserForm.vue` | Add `documentNumber` input field in edit mode |
| `frontend/src/components/admin/UsersAdminPanel.vue` | Add edit button + `UserForm` edit dialog |
| `frontend/src/views/FamilyUnitPage.vue` | Unlock edit controls for Admin/Board when `isViewingOther` |

---

## Tests

### Backend (xUnit)

**File**: `src/Abuvi.API.Tests/Features/Users/UsersEndpointsTests.cs` (or integration tests)

- `UpdateUser_Admin_CanUpdateAnyUserProfile` — Admin can call `PUT /api/users/{otherId}`, receives 200
- `UpdateUser_Board_CanUpdateAnyUserProfile` — Board receives 200
- `UpdateUser_Member_CanUpdateOwnProfile` — Member calling with own ID receives 200
- `UpdateUser_Member_CannotUpdateOtherUserProfile` — Member calling with another user's ID receives 403
- `UpdateUser_SavesDocumentNumber` — `DocumentNumber` is persisted and returned in response
- `UpdateUser_MemberCaller_CannotChangeIsActive` — When a Member updates their own profile, `IsActive` is unchanged regardless of request value
- `UpdateUser_AdminCaller_CanChangeIsActive` — Admin can set `IsActive`

**File**: `src/Abuvi.API.Tests/Features/FamilyUnits/FamilyUnitsEndpointsTests.cs`

- `UpdateFamilyUnit_Admin_CanUpdate_WhenNotRepresentative` — 200 OK
- `UpdateFamilyUnit_Board_CanUpdate_WhenNotRepresentative` — 200 OK
- `UpdateFamilyUnit_Member_CannotUpdate_WhenNotRepresentative` — 403
- `UpdateFamilyMember_Admin_CanUpdate_WhenNotRepresentative` — 200 OK
- `UpdateFamilyMember_Board_CanUpdate_WhenNotRepresentative` — 200 OK
- `UpdateFamilyMember_Member_CannotUpdate_WhenNotRepresentative` — 403

### Frontend (Vitest)

**`frontend/src/components/admin/__tests__/UsersAdminPanel.test.ts`**

- `renders edit button for each user row`
- `opens edit dialog when edit button is clicked` — `showEditDialog` becomes true, `editingUser` is set
- `pre-populates edit form with existing user data`
- `calls updateUser with documentNumber on form submit`
- `shows success toast and closes dialog on successful update`
- `keeps dialog open and shows error when update fails`

**`frontend/src/components/users/__tests__/UserForm.test.ts`**

- `edit mode renders documentNumber field`
- `edit mode initializes documentNumber from user prop`
- `edit mode includes documentNumber in submit payload`
- `create mode does not render documentNumber field`

**`frontend/src/views/__tests__/FamilyUnitPage.spec.ts`**

- `Admin sees edit button on family unit when isViewingOther`
- `Board sees edit button on family unit when isViewingOther`
- `Member does not see edit button on another user's family unit`
- `FamilyMemberList receives editable=true for Admin when isViewingOther`

---

## Acceptance Criteria

- [ ] Admin and Board can edit `firstName`, `lastName`, `phone`, and `documentNumber` of any User from the Users admin panel
- [ ] `documentNumber` is pre-populated in the edit form with the existing value
- [ ] `documentNumber` is included in `UserResponse` for all users endpoints
- [ ] A `Member` calling `PUT /api/users/{otherId}` (not their own ID) receives 403
- [ ] A `Member` updating their own profile cannot change their `IsActive` status
- [ ] Admin and Board can edit any `FamilyUnit` name from the FamilyUnit detail page
- [ ] Admin and Board can edit any `FamilyMember`'s fields from the FamilyUnit detail page
- [ ] The profile photo avatar remains read-only for Admin/Board on other families' pages
- [ ] The representative's Delete family unit button is not shown to Admin/Board (they have separate admin delete)
- [ ] No changes to `Email` or `Password` are possible through this flow

---

## Security Notes

- Always extract and check `user.GetUserRole()` in the handler — never rely on the frontend to enforce role restrictions.
- The `IsActive` protection in the service layer is a defense-in-depth measure so even if the endpoint auth check were bypassed, non-admins cannot lock/unlock accounts.
- Future enhancement: audit log for Admin/Board edits to user and family data.

---

## References

- Users endpoints: [UsersEndpoints.cs](src/Abuvi.API/Features/Users/UsersEndpoints.cs)
- Users models: [UsersModels.cs](src/Abuvi.API/Features/Users/UsersModels.cs)
- Users service: [UsersService.cs](src/Abuvi.API/Features/Users/UsersService.cs)
- FamilyUnits endpoints: [FamilyUnitsEndpoints.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs)
- User form component: [UserForm.vue](frontend/src/components/users/UserForm.vue)
- Users admin panel: [UsersAdminPanel.vue](frontend/src/components/admin/UsersAdminPanel.vue)
- Family unit page: [FamilyUnitPage.vue](frontend/src/views/FamilyUnitPage.vue)

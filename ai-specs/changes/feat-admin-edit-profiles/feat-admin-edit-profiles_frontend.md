# Frontend Implementation Plan: feat-admin-edit-profiles — Admin/Board Edit Profiles

## Overview

This feature enables Admin/Board to edit user and family profile data from the admin interface and family unit page. Changes include:

1. **TypeScript types**: Add `emailVerified` to the `User` type
2. **UsersAdminPanel**: Add edit button + dialog to edit user profiles
3. **FamilyUnitPage**: Unlock edit controls for Admin/Board

No new routes, no new composables, no Pinia store changes, and no `UserForm` component changes are required.

---

## Architecture Context

### Features involved

- User management admin panel (`frontend/src/components/admin/UsersAdminPanel.vue`)
- Family unit detail page (`frontend/src/views/FamilyUnitPage.vue`)
- Family member list component (`frontend/src/components/family-units/FamilyMemberList.vue`)

### Files to modify

| File | Change |
|---|---|
| `frontend/src/types/user.ts` | Add `emailVerified` to `User` |
| `frontend/src/components/admin/UsersAdminPanel.vue` | Add edit button per user; implement edit dialog with `UserForm` |
| `frontend/src/views/FamilyUnitPage.vue` | Unlock edit buttons and member list editability for Admin/Board |

### Composables

- `useUsers()` already has `updateUser()` method — no changes needed
- `useFamilyUnits()` already has `updateFamilyUnit()` and `updateFamilyMember()` methods — no changes needed

### Routing

- No new routes; existing routes remain unchanged
- Authorization already enforced by backend (no frontend role checks needed for edit operations)

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a dedicated frontend branch.
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/feat-admin-edit-profiles-frontend`
  3. Verify: `git branch`
- **Notes**: Never commit directly to `dev` or `main`. All PRs target `dev`.

---

### Step 1: Extend User Types in `frontend/src/types/user.ts`

- **File**: `frontend/src/types/user.ts`
- **Action**: Add `emailVerified` to `User` interface.

**Current state**:

- `User` has: `id`, `email`, `firstName`, `lastName`, `phone`, `role`, `isActive`, `createdAt`, `updatedAt`
- `UpdateUserRequest` has: `firstName`, `lastName`, `phone`, `isActive`

**Implementation Steps**:

1. Add `emailVerified: boolean` to `User`:

   ```typescript
   export interface User {
     id: string
     email: string
     firstName: string
     lastName: string
     phone: string | null
     role: UserRole
     isActive: boolean
     emailVerified: boolean   // NEW (was already in backend response)
     createdAt: string
     updatedAt: string
   }
   ```

- **Implementation Notes**:
  - `emailVerified` is already returned by the backend `UserResponse` DTO but missing in the frontend type — this is a purely additive fix.
  - `UpdateUserRequest` is unchanged — no new fields.

---

### Step 2: Add Edit Dialog to UsersAdminPanel

- **File**: `frontend/src/components/admin/UsersAdminPanel.vue`
- **Action**: Add edit button per user row; implement edit dialog backed by `UserForm` in edit mode.

**Implementation Steps**:

1. **Destructure `updateUser` from `useUsers()`**:

   ```typescript
   const { users, loading, error, fetchUsers, createUser, updateUser, toggleUserActive, deleteUser, clearError } = useUsers()
   ```

2. **Add new state refs**:

   ```typescript
   const showEditDialog = ref(false)
   const editingUser = ref<User | null>(null)
   const updatingUser = ref(false)
   ```

3. **Add new handler functions**:

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

4. **Add edit button in the Actions column** — find the existing `<Column header="Acciones"...>` section. Add a new Button before the toggle/delete buttons:

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

5. **Add edit dialog** after the existing "Create User Dialog":

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

- **Implementation Notes**:
  - Edit button is visible only to `auth.isAdmin || auth.isBoard` (matches backend authorization).
  - The dialog conditionally renders `UserForm` only if `editingUser` is set (prevents errors during dialog open/close animation).
  - Error messages from the composable are shown in a `Message` component inside the dialog.

---

### Step 3: Unlock Edit Controls in FamilyUnitPage

- **File**: `frontend/src/views/FamilyUnitPage.vue`
- **Action**: Allow Admin/Board to edit family unit name and member details even when `isViewingOther = true`.

**Implementation Steps**:

1. **Find the family unit edit/delete buttons** (search for `<div v-if="!isViewingOther"` around line 350–360). Change:

   ```html
   <!-- Before -->
   <div v-if="!isViewingOther" class="flex gap-2">
     <Button icon="pi pi-pencil" label="Editar" ... @click="openEditFamilyUnitDialog" />
     <Button icon="pi pi-trash" label="Eliminar" ... @click="handleDeleteFamilyUnit" />
   </div>

   <!-- After -->
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

2. **Find the FamilyMemberList component** (search for `<FamilyMemberList`). Change its `:editable` binding:

   ```html
   <!-- Before -->
   :editable="!isViewingOther"

   <!-- After -->
   :editable="!isViewingOther || (auth.isAdmin || auth.isBoard)"
   ```

3. **Find the "Add member" button** (search for `v-if="!isViewingOther"` on a button with icon `pi-plus` or label "Agregar miembro"). Change:

   ```html
   <!-- Before -->
   v-if="!isViewingOther"

   <!-- After -->
   v-if="!isViewingOther || (auth.isAdmin || auth.isBoard)"
   ```

4. **Profile photo avatar** — leave its `:editable` binding unchanged (keep it read-only for Admin/Board):

   ```html
   :editable="!isViewingOther"   <!-- unchanged; profile photos belong to family -->
   ```

- **Implementation Notes**:
  - Delete button is **never shown to Admin/Board** viewing another family's unit — they have a separate admin delete flow. This avoids confusion about which delete action applies.
  - Edit and "Add member" buttons are shown to both representative and Admin/Board.
  - The backend already enforces authorization — Admin/Board can update; non-representatives cannot.

---

### Step 4: Write Unit Tests

#### `frontend/src/components/__tests__/UsersAdminPanel.test.ts`

Test the edit dialog functionality:

| Test | What it verifies |
|---|---|
| `renders edit button for each user row` | Edit button appears in Actions column |
| `opens edit dialog when edit button is clicked` | Dialog opens, `editingUser` is set, form is rendered |
| `pre-populates edit form with existing user data` | Form fields match selected user's data |
| `calls updateUser on form submit` | Composable method called with correct payload |
| `shows success toast and closes dialog on successful update` | Toast shown, dialog closed after update |
| `keeps dialog open and shows error when update fails` | Dialog remains open, error displayed if update fails |

#### `frontend/src/views/__tests__/FamilyUnitPage.spec.ts`

| Test | What it verifies |
|---|---|
| `Admin sees edit button on family unit when isViewingOther` | Button visible |
| `Board sees edit button on family unit when isViewingOther` | Button visible |
| `Member does not see edit button on another user's family unit` | Button hidden |
| `FamilyMemberList receives editable=true for Admin when isViewingOther` | `:editable` prop is true |

- **Implementation Notes**:
  - Tests follow the project's existing Vitest + Vue Test Utils pattern (see `src/components/__tests__/*.test.ts`)
  - Aim for 90% coverage threshold (branches, functions, lines, statements)

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-admin-edit-profiles-frontend`
2. **Step 1** — Add `emailVerified` to `User` type
3. **Step 2** — Implement edit dialog in `UsersAdminPanel`
4. **Step 3** — Unlock edit controls in `FamilyUnitPage`
5. **Step 4** — Write unit tests

---

## Testing Checklist

- [ ] `UsersAdminPanel.test.ts` covers: edit button renders, dialog opens, form pre-populates, submit calls API, success toast, error handling
- [ ] `FamilyUnitPage.spec.ts` covers: Admin/Board see edit controls, Member does not
- [ ] `npm run test` passes with no errors
- [ ] `npm run dev` runs without build errors
- [ ] Manual testing: Admin can edit user profile → name and phone saved
- [ ] Manual testing: Admin can edit family unit name → name updates
- [ ] Manual testing: Admin can add/edit family members → changes persisted
- [ ] 90% test coverage threshold maintained (per `frontend-standards.mdc`)

---

## Error Handling Patterns

**UsersAdminPanel edit dialog**:

- API errors from composable → show in `<Message severity="error">` component
- Server 404 → "User not found"
- Server 403 → "Insufficient permissions" (should not occur, but defense-in-depth)

**FamilyUnitPage**:

- API errors already handled by `useFamilyUnits()` composable — show in existing error message area

---

## UI/UX Considerations

**UsersAdminPanel**:

- Edit button appears **inline in Actions column** (consistent with existing toggle/delete buttons)
- Edit button uses `severity="info"` (blue) to distinguish from delete (red)
- Tooltip shows "Editar perfil" on hover
- Dialog header: "Editar Perfil de Usuario" (clear Spanish label)

**FamilyUnitPage**:

- Edit button visible to both representative and Admin/Board (consistent permissions model)
- Delete button **hidden from Admin/Board** (they have separate admin delete flow)
- "Add member" and member list remain editable for Admin/Board
- Profile photo avatar remains read-only for Admin/Board

---

## Dependencies

No new npm packages required. Existing dependencies are sufficient:

- `vue@^3.x`
- `primevue@^4.x` (all components already used)
- `tailwindcss@^3.x`

---

## Notes

- **No routing changes** — all existing routes remain the same
- **No Pinia store changes** — auth store already available via `useAuthStore()`
- **Authorization is backend-enforced** — frontend shows/hides UI; backend validates all changes
- **Language**: Frontend user-facing text in Spanish; code comments in English
- **TypeScript strict mode** — all types fully typed, no `any`
- **PrimeVue + Tailwind** — no custom CSS; use PrimeVue components and Tailwind utilities only

---

## Next Steps After Implementation

1. Open PR targeting `dev` branch (never `main` directly)
2. Backend implementation tracked separately in `feat-admin-edit-profiles_backend.md`
3. Once both frontend and backend PRs merge, feature is complete and testable in `dev` environment

---

## Implementation Verification

- [ ] **Code Quality**: All TypeScript files compile with zero errors/warnings; no `any` types
- [ ] **Functionality**: Edit button visible to Admin/Board; edit form pre-populates; updates save correctly
- [ ] **Functionality**: Family unit edit/add member buttons visible to Admin/Board; changes persist
- [ ] **Composables**: No changes needed — `useUsers()` and `useFamilyUnits()` already support updates
- [ ] **Testing**: All tests pass; 90% coverage threshold maintained
- [ ] **Build**: `npm run build` succeeds with zero errors

# Frontend Implementation Plan: fix-board-users-list-access

## Overview

Hide Admin-only action buttons (Create User, Toggle Active, Delete) from Board role users in `UsersAdminPanel.vue`. Board users should still see the users list and the role-edit button (already handled by `UserRoleCell.vue`).

This is a pure UI guard — the backend remains the authoritative enforcer. All changes are in a single component file.

## Architecture Context

- **Component to modify**: `frontend/src/components/admin/UsersAdminPanel.vue`
- **Existing auth infrastructure**: `useAuthStore` from `@/stores/auth` — already exports `isAdmin` (true only for Admin) and `isBoard` (true for Admin or Board)
- **No changes needed**: `UserRoleCell.vue` (already uses `useAuthStore` and correctly shows edit button for Board users), router, composables, types, stores
- **No new components or files needed**

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/fix-board-users-list-access-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/fix-board-users-list-access-frontend`
  4. Verify branch creation: `git branch`

### Step 1: Add Auth Store to UsersAdminPanel.vue

- **File**: `frontend/src/components/admin/UsersAdminPanel.vue`
- **Action**: Import and use `useAuthStore` in the `<script setup>` block
- **Implementation Steps**:
  1. Add import: `import { useAuthStore } from '@/stores/auth'`
  2. Add store instance: `const auth = useAuthStore()`
- **Implementation Notes**: Place the import after the existing composable imports (after line 5). Place the `const auth` after the existing composable destructuring (after line 24).

### Step 2: Conditionally Hide "Crear Usuario" Button

- **File**: `frontend/src/components/admin/UsersAdminPanel.vue`
- **Action**: Add `v-if="auth.isAdmin"` to the "Crear Usuario" button
- **Current code** (line 143):
  ```vue
  <Button label="Crear Usuario" icon="pi pi-plus" @click="openCreateDialog" />
  ```
- **Target code**:
  ```vue
  <Button v-if="auth.isAdmin" label="Crear Usuario" icon="pi pi-plus" @click="openCreateDialog" />
  ```

### Step 3: Conditionally Hide Toggle Active and Delete Buttons

- **File**: `frontend/src/components/admin/UsersAdminPanel.vue`
- **Action**: Add `v-if="auth.isAdmin"` to both the Toggle Active button (lines 203-213) and the Delete button (lines 214-224) inside the Actions column
- **Current code** (lines 202-225):
  ```vue
  <div class="flex items-center gap-1">
    <Button
      :icon="data.isActive ? 'pi pi-ban' : 'pi pi-check'"
      ...
      @click="handleToggleActive(data)"
    />
    <Button
      icon="pi pi-trash"
      ...
      @click="handleDeleteUser(data)"
    />
  </div>
  ```
- **Target code**:
  ```vue
  <div class="flex items-center gap-1">
    <Button
      v-if="auth.isAdmin"
      :icon="data.isActive ? 'pi pi-ban' : 'pi pi-check'"
      ...
      @click="handleToggleActive(data)"
    />
    <Button
      v-if="auth.isAdmin"
      icon="pi pi-trash"
      ...
      @click="handleDeleteUser(data)"
    />
  </div>
  ```
- **Implementation Notes**:
  - The Actions column itself should remain visible for all Board users because `UserRoleCell.vue` in the Rol column already shows the role-edit button for Board users — no action buttons in the Actions column for Board is acceptable.
  - However, for Board users, the Actions column will render an empty `<div>` with no buttons. This is acceptable UX since the role-edit affordance lives in the Rol column via `UserRoleCell`.

### Step 4: Write Vitest Unit Tests

- **File**: `frontend/src/components/admin/__tests__/UsersAdminPanel.test.ts` (NEW file)
- **Action**: Create tests verifying conditional visibility of Admin-only controls
- **Implementation Steps**:
  1. Create the test file
  2. Mock `useAuthStore` to return different roles
  3. Mock `useUsers` composable to return test data
  4. Test cases:

  ```typescript
  describe('UsersAdminPanel', () => {
    describe('Admin role', () => {
      it('should show "Crear Usuario" button')
      it('should show Toggle Active buttons in table rows')
      it('should show Delete buttons in table rows')
    })

    describe('Board role', () => {
      it('should NOT show "Crear Usuario" button')
      it('should NOT show Toggle Active buttons in table rows')
      it('should NOT show Delete buttons in table rows')
      it('should show the users table (data loads)')
    })
  })
  ```

- **Dependencies**: `vitest`, `@vue/test-utils`, mock for `useAuthStore` and `useUsers`
- **Implementation Notes**:
  - Use `vi.mock('@/stores/auth')` to control the `isAdmin` computed value
  - Use `vi.mock('@/composables/useUsers')` to provide test user data without API calls
  - Follow existing test patterns from `frontend/src/components/admin/__tests__/MediaItemsReviewPanel.test.ts`

### Step 5: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. No routing changes, no new dependencies, no API changes (frontend-only UI guard)
  2. If `frontend-standards.mdc` documents role-based UI patterns, ensure this pattern is consistent
  3. Verify the enriched spec's acceptance criteria are met

## Implementation Order

1. Step 0: Create Feature Branch
2. Step 1: Add Auth Store to UsersAdminPanel.vue
3. Step 2: Conditionally Hide "Crear Usuario" Button
4. Step 3: Conditionally Hide Toggle Active and Delete Buttons
5. Step 4: Write Vitest Unit Tests
6. Step 5: Update Technical Documentation

## Testing Checklist

- [ ] Admin user: "Crear Usuario" button is visible
- [ ] Admin user: Toggle Active buttons visible in every row
- [ ] Admin user: Delete buttons visible in every row
- [ ] Board user: "Crear Usuario" button is NOT visible
- [ ] Board user: Toggle Active buttons NOT visible in any row
- [ ] Board user: Delete buttons NOT visible in any row
- [ ] Board user: Users table loads and displays data
- [ ] Board user: Role edit button in `UserRoleCell` still visible for eligible users
- [ ] Vitest tests pass: `npx vitest run --reporter verbose src/components/admin/__tests__/UsersAdminPanel.test.ts`

## Error Handling Patterns

No new error handling needed — the existing error states in the composable (`loading`, `error`) remain unchanged. The only change is conditional rendering of buttons.

## UI/UX Considerations

- **No layout shift**: Hiding the buttons does not affect table column layout since they're inside a flex container
- **Actions column for Board**: Will appear empty for Board users. This is acceptable — the meaningful Board action (role edit) is in the Rol column via `UserRoleCell`
- **Create User Dialog**: Since the button is hidden, the dialog and its associated state (`showCreateDialog`, `creatingUser`) are simply unused for Board users — no need to remove them
- **Accessibility**: No ARIA changes needed — hidden elements are not rendered at all (`v-if` removes from DOM)

## Dependencies

- No new npm packages
- PrimeVue components used: same as existing (Button, DataTable, etc.)
- Auth store: `useAuthStore` from `@/stores/auth` (already exists)

## Notes

- **Security**: This is a UX-only guard. The backend endpoints (`POST /api/users`, `DELETE /api/users/{id}`, `PUT /api/users/{id}`) retain `Admin`-only authorization. If a Board user somehow triggers these actions, the backend will return 403.
- **`isAdmin` vs `isBoard`**: Use `auth.isAdmin` (true only for Admin role) to guard Admin-only buttons. Do NOT use `auth.isBoard` (which is true for both Admin and Board).
- **`UserRoleCell.vue`**: Already correctly implemented — uses `useAuthStore` and shows the edit-role button for Board users who can edit Member roles. No changes needed.

## Next Steps After Implementation

1. Create PR targeting `dev` branch
2. Coordinate with the **backend ticket** (separate branch) that adds Board to `GET /api/users` authorization — both changes needed for full fix

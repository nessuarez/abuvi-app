# Frontend Implementation Plan: feat-my-memberships-dialog — Wire MembershipDialog into FamilyUnitPage and ProfilePage

## Overview

This feature wires the already-implemented `MembershipDialog.vue` component into two existing pages (`FamilyUnitPage.vue` and `ProfilePage.vue`) and adds a "Gestionar membresía" action button to `FamilyMemberList.vue`. No new components, composables, types, routes, or npm packages are required. The work is purely about connecting existing pieces.

Architecture principles followed:
- Vue 3 Composition API with `<script setup lang="ts">`
- Dumb/presentational `FamilyMemberList` receives a boolean prop from the parent — it does not import auth state directly
- Parent pages (views) own dialog state and data-refresh logic
- `ConfirmDialog` must be present in the DOM tree for `MembershipDialog`'s `useConfirm()` to work

---

## Architecture Context

### Components/composables involved

| File | Role |
|---|---|
| `frontend/src/components/family-units/FamilyMemberList.vue` | Add `canManageMemberships` prop + `manageMembership` emit + action button |
| `frontend/src/views/FamilyUnitPage.vue` | Import `MembershipDialog`, add dialog state + handler, wire to list |
| `frontend/src/views/ProfilePage.vue` | Import `MembershipDialog` + `ConfirmDialog`, add dialog state + reload-on-close handler, add button per member row |
| `frontend/src/components/memberships/MembershipDialog.vue` | Already implemented — no changes |
| `frontend/src/stores/auth.ts` | `auth.isBoard` used by parent pages to gate the feature |
| `frontend/src/composables/useMemberships.ts` | Already implemented — no changes |

### State management approach

Local `ref` state in each parent page (no Pinia store needed — dialog state is page-scoped).

### Routing

No new routes.

### Key types

`FamilyMemberResponse` from `@/types/family-unit` — already used by both pages.
`MemberMembershipData` — local interface already defined inside `ProfilePage.vue` (not exported from types); the `openMembershipDialog` handler receives this type.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-my-memberships-dialog-frontend`
- **Implementation Steps**:
  1. Ensure you are on the latest `main` branch: `git checkout main && git pull origin main`
  2. Create the feature branch: `git checkout -b feature/feat-my-memberships-dialog-frontend`
  3. Verify: `git branch`
- **Notes**: This MUST be the first step before any code changes.

---

### Step 1: Update `FamilyMemberList.vue`

- **File**: `frontend/src/components/family-units/FamilyMemberList.vue`
- **Action**: Add `canManageMemberships` prop, `manageMembership` emit, and the action button in the "Acciones" column.

**Props — add `canManageMemberships`:**

```typescript
const props = defineProps<{
  members: FamilyMemberResponse[]
  loading?: boolean
  canManageMemberships?: boolean   // NEW
}>()
```

**Emits — add `manageMembership`:**

```typescript
const emit = defineEmits<{
  edit: [member: FamilyMemberResponse]
  delete: [member: FamilyMemberResponse]
  manageMembership: [member: FamilyMemberResponse]   // NEW
}>()
```

**Template — add button in "Acciones" column, BEFORE the Edit button:**

```html
<Button
  v-if="props.canManageMemberships"
  icon="pi pi-id-card"
  severity="secondary"
  text
  rounded
  v-tooltip.top="'Gestionar membresía'"
  @click="emit('manageMembership', data)"
/>
```

**Implementation Notes:**
- Place the new button before the existing Edit (`pi-pencil`) button so the order is: Membership | Edit | Delete.
- The component must NOT import `useAuthStore` — keep it dumb. The parent passes the boolean.
- `canManageMemberships` defaults to `undefined` / falsy, so no regression for existing usages of `FamilyMemberList` that do not pass the prop.
- No new imports needed (PrimeVue `Button` is already imported).

---

### Step 2: Update `FamilyUnitPage.vue`

- **File**: `frontend/src/views/FamilyUnitPage.vue`
- **Action**: Import `MembershipDialog`, add auth store, add dialog state and handler, wire to `FamilyMemberList`, add `MembershipDialog` in template.

**New imports** (add to existing import block):

```typescript
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
import { useAuthStore } from '@/stores/auth'
```

`FamilyMemberResponse` is already imported. No additional type import needed.

**New state** (add after existing `const toast = useToast()`):

```typescript
const auth = useAuthStore()

// Membership dialog state
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<FamilyMemberResponse | null>(null)
```

**New handler** (add after `handleDeleteMember`):

```typescript
const handleManageMembership = (member: FamilyMemberResponse) => {
  selectedMemberForMembership.value = member
  showMembershipDialog.value = true
}
```

**Template — update `FamilyMemberList` usage** (add two new attributes):

```html
<FamilyMemberList
  :members="familyMembers"
  :loading="loading"
  :can-manage-memberships="auth.isBoard"
  @edit="openEditMemberDialog"
  @delete="handleDeleteMember"
  @manage-membership="handleManageMembership"
/>
```

**Template — add `MembershipDialog` at the bottom** (alongside the existing `<Dialog>` blocks, before the closing `</div>`):

```html
<MembershipDialog
  v-if="selectedMemberForMembership"
  v-model:visible="showMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :member-id="selectedMemberForMembership.id"
  :member-name="`${selectedMemberForMembership.firstName} ${selectedMemberForMembership.lastName}`"
/>
```

**Implementation Notes:**
- `ConfirmDialog` is already mounted in `FamilyUnitPage.vue` (`<ConfirmDialog />` at the top of template). `MembershipDialog` internally mounts its own `<ConfirmDialog />` too, so no additional `ConfirmDialog` is needed in `FamilyUnitPage`.
- `v-if="selectedMemberForMembership"` ensures the dialog is not mounted with empty props on initial render, and that the composable state inside `MembershipDialog` is reset each time a new member is selected.
- No data reload is needed on dialog close — `FamilyUnitPage` does not display membership status.

---

### Step 3: Update `ProfilePage.vue`

- **File**: `frontend/src/views/ProfilePage.vue`
- **Action**: Import `MembershipDialog` and `ConfirmDialog`, add dialog state, add `openMembershipDialog` + `handleMembershipDialogClose` handlers, add "Gestionar membresía" button per member row, mount `MembershipDialog` in template.

**New imports** (add to existing import block):

```typescript
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
import ConfirmDialog from 'primevue/confirmdialog'
import { useConfirm } from 'primevue/useconfirm'
```

**Add `useConfirm` instantiation** (add alongside existing `const toast = useToast()`):

```typescript
const confirm = useConfirm()
```

Note: `confirm` itself doesn't need to be called in `ProfilePage` — it just needs to be instantiated so the `ConfirmService` is registered in the component tree for `MembershipDialog`'s internal `useConfirm()` to work.

**New state** (add after existing `// --- Pay fee dialog ---` block):

```typescript
// --- Membership dialog ---
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<MemberMembershipData | null>(null)
```

`MemberMembershipData` is already defined as a local interface inside `ProfilePage.vue`. No additional import.

**New handlers** (add after `handlePayFee`):

```typescript
const openMembershipDialog = (data: MemberMembershipData) => {
  selectedMemberForMembership.value = data
  showMembershipDialog.value = true
}

const handleMembershipDialogClose = async () => {
  showMembershipDialog.value = false
  selectedMemberForMembership.value = null
  // Reload to pick up any create/deactivate changes
  await loadMemberMembershipData()
}
```

**Template — add "Gestionar membresía" button** in the member row actions area, AFTER the existing "Pagar cuota" `<Button>` (inside the `<div class="flex flex-wrap items-center gap-2">` block):

```html
<Button
  v-if="auth.isBoard"
  label="Gestionar membresía"
  icon="pi pi-id-card"
  size="small"
  severity="secondary"
  outlined
  :data-testid="`manage-membership-btn-${data.member.id}`"
  @click="openMembershipDialog(data)"
/>
```

**Template — add `ConfirmDialog` mount** (add `<ConfirmDialog />` near the top of the `<Container>` template, alongside where `PayFeeDialog` is placed at the bottom):

```html
<ConfirmDialog />
```

**Template — add `MembershipDialog`** at the bottom of the template, alongside `<PayFeeDialog>`:

```html
<MembershipDialog
  v-if="selectedMemberForMembership"
  v-model:visible="showMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :member-id="selectedMemberForMembership.member.id"
  :member-name="`${selectedMemberForMembership.member.firstName} ${selectedMemberForMembership.member.lastName}`"
  @update:visible="(val) => { if (!val) handleMembershipDialogClose() }"
/>
```

**Implementation Notes:**
- `@update:visible="(val) => { if (!val) handleMembershipDialogClose() }"` — intercept the `update:visible` event emitted by the inner `Dialog`. When `val` becomes `false` (dialog closed), trigger the reload. This avoids calling reload when the dialog opens (val=true).
- `v-model:visible` is also bound so the dialog can close itself normally via the close button / dismissable mask.
- `ConfirmDialog` must be present in the DOM tree for `MembershipDialog`'s `useConfirm()` deactivate confirmation to work. `ProfilePage` does not currently use `useConfirm`, so both `ConfirmDialog` and `useConfirm()` must be added.
- `selectedMemberForMembership.value = null` in `handleMembershipDialogClose` clears the selection so `v-if` unmounts the dialog — ensuring fresh composable state next time it opens.
- The button is added for all board users regardless of the member's current membership state; `MembershipDialog` handles all sub-cases internally (no membership / active / inactive).

---

### Step 4: Write Vitest Unit Tests — `FamilyMemberList`

- **File**: `frontend/src/components/family-units/__tests__/FamilyMemberList.spec.ts` (create)
- **Action**: Write unit tests using Vitest + Vue Test Utils covering the new membership button behaviour.

**Test setup:**

```typescript
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import FamilyMemberList from '@/components/family-units/FamilyMemberList.vue'
import type { FamilyMemberResponse } from '@/types/family-unit'
```

You will need to provide a global stub for PrimeVue components (DataTable, Column, Button, Tag) or use `global.stubs`. Check existing test setup in the project for the standard approach — if no Vitest config exists yet, use `global: { stubs: { ... } }` in mount options.

**Test cases:**

1. `renders manageMembership button when canManageMemberships is true`
   - Mount with `canManageMemberships: true` and a member
   - `expect(wrapper.find('[v-tooltip\\.top="\'Gestionar membresía\'"]'))` — or find by icon class `pi-id-card` in the button markup
   - Assert button exists / is visible

2. `does not render manageMembership button when canManageMemberships is false (default)`
   - Mount with `canManageMemberships: false` (or omit prop)
   - Assert button is NOT rendered

3. `emits manageMembership with the correct member when button is clicked`
   - Mount with `canManageMemberships: true` and one member
   - Click the membership button
   - `expect(wrapper.emitted('manageMembership')).toHaveLength(1)`
   - `expect(wrapper.emitted('manageMembership')![0][0]).toEqual(mockMember)`

4. `existing edit and delete buttons still emit correctly`
   - Regression guard — click edit/delete, verify emits unchanged

**Mock member fixture:**

```typescript
const mockMember: FamilyMemberResponse = {
  id: 'member-1',
  firstName: 'Ana',
  lastName: 'García',
  dateOfBirth: '1990-01-01',
  relationship: 'Primary',
  email: 'ana@example.com',
  phone: null,
  hasMedicalNotes: false,
  hasAllergies: false,
  userId: null,
}
```

---

### Step 5: Write Vitest Unit Tests — `ProfilePage`

- **File**: `frontend/src/views/__tests__/ProfilePage.spec.ts` (create)
- **Action**: Write unit tests covering the new membership button visibility and dialog open behaviour.

**Test setup considerations:**
- `ProfilePage` has heavy dependencies (multiple composables, stores, router). Use `vi.mock` for all composables and the auth store.
- Mock `useAuthStore` to return `{ isBoard: true/false, ... }` as needed.
- Mock `useProfile`, `useFamilyUnits`, `useMemberships` to return controlled data.
- Stub `MembershipDialog`, `PayFeeDialog`, `ConfirmDialog` as empty components.

**Test cases:**

1. `shows manage-membership button for board user when member has no membership`
   - Auth mock: `isBoard = true`
   - memberData mock: member with `membershipId: null`
   - Assert `[data-testid="manage-membership-btn-{id}"]` is visible

2. `shows manage-membership button for board user when member has active membership`
   - Auth mock: `isBoard = true`
   - memberData mock: member with `isActiveMembership: true`
   - Assert button is visible

3. `does not show manage-membership button for non-board user`
   - Auth mock: `isBoard = false`
   - Assert button is NOT in DOM

4. `opens MembershipDialog with correct props when manage-membership button clicked`
   - Click the button for a specific member
   - Assert `showMembershipDialog` becomes true
   - Assert `selectedMemberForMembership` contains the correct member data
   - (Or assert the stub `MembershipDialog` receives correct props)

5. `reloads memberData when MembershipDialog emits update:visible with false`
   - Spy on `loadMemberMembershipData`
   - Trigger `@update:visible` with `false` on the `MembershipDialog` stub
   - Assert `loadMemberMembershipData` was called

---

### Step 6: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/membership-management.cy.ts` (create)
- **Action**: Cover the critical user flows using API intercepts.

**Scenarios:**

```typescript
describe('Membership management from Mi Perfil', () => {
  beforeEach(() => {
    // Intercept auth + profile + family unit + members API calls
    cy.intercept('GET', '/api/auth/me', { fixture: 'board-user.json' }).as('getUser')
    cy.intercept('GET', '/api/family-units/me', { fixture: 'family-unit.json' }).as('getFamilyUnit')
    cy.intercept('GET', '/api/family-units/*/members', { fixture: 'members.json' }).as('getMembers')
    // Board user login
    cy.login('board@abuvi.org', 'password')
  })

  it('shows Gestionar membresía button for board user', () => {
    cy.intercept('GET', '/api/family-units/*/members/*/membership', { statusCode: 404 })
    cy.visit('/profile')
    cy.wait(['@getFamilyUnit', '@getMembers'])
    cy.get('[data-testid^="manage-membership-btn-"]').should('exist')
  })

  it('does not show Gestionar membresía button for non-board user', () => {
    cy.intercept('GET', '/api/auth/me', { fixture: 'regular-user.json' })
    cy.visit('/profile')
    cy.get('[data-testid^="manage-membership-btn-"]').should('not.exist')
  })

  it('opens MembershipDialog when button clicked and member has no membership', () => {
    cy.intercept('GET', '/api/family-units/*/members/*/membership', { statusCode: 404 })
    cy.visit('/profile')
    cy.get('[data-testid^="manage-membership-btn-"]').first().click()
    // MembershipDialog should appear
    cy.contains('Membresía —').should('be.visible')
    cy.contains('no tiene una membresía activa').should('be.visible')
  })

  it('activates membership and refreshes member card on dialog close', () => {
    cy.intercept('GET', '/api/family-units/*/members/*/membership', { statusCode: 404 }).as('getMembership')
    cy.intercept('POST', '/api/family-units/*/members/*/membership', { statusCode: 201, body: { success: true, data: { id: 'm1', isActive: true, startDate: '2026-01-01', fees: [] } } }).as('createMembership')
    cy.visit('/profile')
    cy.get('[data-testid^="manage-membership-btn-"]').first().click()
    cy.get('#membership-start-date').type('01/01/2026')
    cy.contains('Activar membresía').click()
    cy.wait('@createMembership')
    // After closing dialog, membership badge should update
    cy.intercept('GET', '/api/family-units/*/members/*/membership', { body: { success: true, data: { id: 'm1', isActive: true, startDate: '2026-01-01', fees: [] } } })
    cy.get('.p-dialog-close-button, [aria-label="Close"]').click()
    cy.get('[data-testid^="membership-badge-"]').first().should('contain', 'Socio activo')
  })
})

describe('Membership management from FamilyUnitPage', () => {
  it('shows Gestionar membresía button in member table for board user', () => {
    // Setup intercepts and login as board user
    cy.login('board@abuvi.org', 'password')
    cy.visit('/family-unit/me')
    cy.get('[v-tooltip\\.top="Gestionar membresía"]').should('exist')
    // or target by icon class
    cy.get('.pi-id-card').closest('button').should('exist')
  })
})
```

**Fixtures needed** (create in `frontend/cypress/fixtures/`):
- `board-user.json` — user with role `Board`
- `regular-user.json` — user with role `Member`
- `family-unit.json` — basic family unit
- `members.json` — array of `FamilyMemberResponse`

---

### Step 7: Update Technical Documentation

- **Action**: Review and update documentation based on changes made
- **Implementation Steps**:
  1. No new API endpoints — `api-spec.yml` does NOT need updating
  2. No new routing changes — router documentation does NOT need updating
  3. No new npm packages — `package.json` docs unchanged
  4. Consider updating `frontend-standards.mdc` if any new patterns were established (e.g., the `v-if` + `@update:visible` pattern for dialogs that need post-close side effects)
  5. Confirm no other documentation files reference these components in a way that needs updating
- **References**: Follow `ai-specs/specs/documentation-standards.mdc`
- **Notes**: This step is MANDATORY. Even if no doc files need changes, confirm explicitly.

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-my-memberships-dialog-frontend`
2. **Step 1**: Modify `FamilyMemberList.vue` — add prop, emit, button
3. **Step 2**: Modify `FamilyUnitPage.vue` — import dialog, add state/handler, wire list
4. **Step 3**: Modify `ProfilePage.vue` — import dialog + ConfirmDialog, add state/handlers/button, mount dialog
5. **Step 4**: Write Vitest tests for `FamilyMemberList`
6. **Step 5**: Write Vitest tests for `ProfilePage`
7. **Step 6**: Write Cypress E2E tests
8. **Step 7**: Update technical documentation

---

## Testing Checklist

- [ ] `FamilyMemberList` button visible when `canManageMemberships=true`
- [ ] `FamilyMemberList` button hidden when `canManageMemberships=false` (or prop omitted)
- [ ] `FamilyMemberList` emits `manageMembership` with correct member on click
- [ ] `FamilyMemberList` edit/delete emits are unaffected (regression)
- [ ] `FamilyUnitPage` passes `canManageMemberships` correctly (true for board, false otherwise)
- [ ] `FamilyUnitPage` opens `MembershipDialog` with correct `familyUnitId`, `memberId`, `memberName`
- [ ] `FamilyUnitPage` — existing edit/delete member flows unaffected (regression)
- [ ] `ProfilePage` shows "Gestionar membresía" button only when `auth.isBoard`
- [ ] `ProfilePage` opens `MembershipDialog` with correct props
- [ ] `ProfilePage` calls `loadMemberMembershipData` when dialog closes
- [ ] `ProfilePage` "Pagar cuota" flow unaffected (regression)
- [ ] `ConfirmDialog` available in `ProfilePage` tree (deactivate confirmation works)
- [ ] Vitest unit tests pass for `FamilyMemberList`
- [ ] Vitest unit tests pass for `ProfilePage`
- [ ] Cypress E2E scenarios pass

---

## Error Handling Patterns

No new error handling is required. `MembershipDialog` manages its own loading/error/toast states. The parent pages only need to handle the post-close reload in `ProfilePage`, which re-uses the existing `loadMemberMembershipData()` that already has error handling (catches and returns empty on failure).

---

## UI/UX Considerations

- **Button icon**: `pi pi-id-card` — consistent with the role icon already used in `ProfilePage` personal info section.
- **Button placement in `FamilyMemberList`**: Before Edit, before Delete. Order: Membership | Edit | Delete.
- **Button in `ProfilePage`**: Uses `severity="secondary" outlined` consistent with the "Pagar cuota" button next to it.
- **Dialog width**: `MembershipDialog` already uses `class="w-full max-w-2xl"` — no changes needed.
- **Responsive**: No additional responsive work needed — PrimeVue `Dialog` and existing button layouts handle small screens.
- **Loading feedback**: `MembershipDialog` handles its own internal loading state.
- **`v-if` on dialog**: Prevents mounting the dialog with null/empty props and ensures fresh composable state (membership data fetched fresh on each open).

---

## Dependencies

No new npm packages required. All needed components are already in the codebase:
- `MembershipDialog.vue` — `@/components/memberships/MembershipDialog.vue`
- `ConfirmDialog` — `primevue/confirmdialog` (already used in `FamilyUnitPage`)
- `useAuthStore` — `@/stores/auth` (already used in `ProfilePage`)
- `useMemberships` composable — no changes

---

## Notes

1. **ConfirmDialog must be in the DOM tree**: `MembershipDialog` calls `confirm.require()` for deactivation. The `ConfirmDialog` component must be rendered somewhere in the ancestor tree. `FamilyUnitPage` already has it. `ProfilePage` does not — add it.

2. **`v-if` vs `v-show` on `MembershipDialog`**: Use `v-if="selectedMemberForMembership"` (not `v-show`). This ensures the `useMemberships` composable inside `MembershipDialog` starts fresh with null state on every open. Using `v-show` would keep stale membership data from the previous member visible during the loading flash.

3. **No reload needed in `FamilyUnitPage`**: The page shows a members table with name/age/relationship/contact/health columns. No membership status column exists. Do not add a reload hook — it would be premature optimisation.

4. **Reload only on close, not on open** (`ProfilePage`): `handleMembershipDialogClose` is triggered via `@update:visible="(val) => { if (!val) ... }"`. This fires only when the dialog transitions from visible to hidden — not when it opens.

5. **User-facing text in Spanish**: All button labels and tooltip text must remain in Spanish ("Gestionar membresía", not "Manage membership").

6. **TypeScript**: `canManageMemberships` is `boolean | undefined` in props (optional). In the parent, it is bound as `:can-manage-memberships="auth.isBoard"` where `auth.isBoard` is always a `boolean` computed. No type issues.

7. **`data-testid` on manage button**: Use `` `manage-membership-btn-${data.member.id}` `` in `ProfilePage` (as per spec). In `FamilyMemberList`, a `v-tooltip` attribute is sufficient for Cypress targeting (or add a `data-testid` if preferred by the team).

---

## Next Steps After Implementation

- After merging, verify in staging that the deactivation confirmation modal renders correctly (requires `ConfirmDialog` in the tree — the most common wiring mistake).
- If membership status columns are added to `FamilyUnitPage`'s member table in the future, add a `handleMembershipDialogClose` reload hook at that point.
- No backend changes, no migrations, no deployment config changes required.

---

## Implementation Verification

### Code Quality
- [ ] All three modified files use `<script setup lang="ts">` — no Options API
- [ ] No `any` types introduced
- [ ] `canManageMemberships` prop is `boolean | undefined` (optional) — no breaking change
- [ ] No `<style>` blocks added — Tailwind only

### Functionality
- [ ] `MembershipDialog` opens from `FamilyUnitPage` with correct `familyUnitId`, `memberId`, `memberName`
- [ ] `MembershipDialog` opens from `ProfilePage` with correct IDs (board user only)
- [ ] `ConfirmDialog` works inside `MembershipDialog` in both parent pages
- [ ] After activate/deactivate and dialog close in `ProfilePage`, membership badge updates

### Testing
- [ ] Vitest tests cover visibility gating and emit behaviour of `FamilyMemberList`
- [ ] Vitest tests cover `ProfilePage` button visibility and dialog open/close + reload
- [ ] Cypress E2E covers board vs non-board visibility, dialog open, activate flow, badge refresh

### Integration
- [ ] No regression on existing "Pagar cuota" flow in `ProfilePage`
- [ ] No regression on edit/delete member actions in `FamilyUnitPage` and `ProfilePage`
- [ ] Composable and backend API calls work end-to-end

### Documentation
- [ ] `ai-specs/specs/frontend-standards.mdc` updated if new patterns documented
- [ ] All documentation written in English

# Enriched User Story: Integrar MembershipDialog en las vistas de gestión

## Summary

As a **Board/Admin** user, I want to be able to create, view, and manage memberships for family members directly from the family unit management page and from "Mi Perfil", so that I can activate memberships for members who don't have one yet and manage existing ones.

---

## Problem Context

The backend membership API is fully implemented and the frontend composable (`useMemberships.ts`) and components (`MembershipDialog.vue`, `PayFeeDialog.vue`) are complete — but `MembershipDialog` is **not wired to any page**.

Current state:

- `ProfilePage.vue` fetches and displays membership status (active/inactive/none) per member.
- `ProfilePage.vue` shows a "Pagar cuota" button (board-only) for members with unpaid fees.
- **But there is no way to CREATE a membership from the UI.**
- Members created before the membership migration (2026-02-16) show "Sin membresía" and there is no action to fix this.
- `FamilyMemberList.vue` only has Edit and Delete actions — no membership action.
- `FamilyUnitPage.vue` has no membership management whatsoever.
- `MembershipDialog.vue` exists but is imported nowhere (`grep MembershipDialog` → no results).

The result: boards cannot activate memberships for any family member through the UI.

---

## Scope

### In scope

1. **`FamilyMemberList.vue`** — add a "Gestionar membresía" action button (board/admin only) that emits `manageMembership`.
2. **`FamilyUnitPage.vue`** — import and wire `MembershipDialog`; handle the `manageMembership` emit from `FamilyMemberList`.
3. **`ProfilePage.vue`** — add a "Gestionar membresía" button (board/admin only) next to the existing "Pagar cuota" button on each member card. This allows boards to manage memberships without navigating away from the profile page.

### Out of scope

- Backend changes (API is complete).
- Any changes to `MembershipDialog.vue` or `PayFeeDialog.vue` (they are ready to use).
- Composable changes (already implemented).
- Any new routes.

---

## Business Rules

### Rule 1 — Membership management is Board/Admin only

The "Gestionar membresía" button must only be visible to users with `isBoard` or `isAdmin` from the auth store. Regular authenticated users can see membership status but cannot manage it.

### Rule 2 — MembershipDialog is the single source of truth

`MembershipDialog` already handles all sub-cases:

- Member has no membership → shows "Activar membresía" form with date picker.
- Member has active membership → shows status, fees table, "Desactivar membresía" (with confirmation), and "Pagar" per unpaid fee.
- Member has inactive membership → shows status only (membership was manually deactivated; creating a new one is not currently supported by the backend unique index — this is existing behaviour).

### Rule 3 — No N+1 on FamilyMemberList

`FamilyMemberList.vue` must **not** fetch membership data per row. It receives members as props and emits an event — the parent page (FamilyUnitPage or ProfilePage) is responsible for fetching membership data and opening the dialog.

### Rule 4 — After MembershipDialog actions, refresh member data

When `MembershipDialog` is closed after a create or deactivate action, the parent page must reload the membership data to keep the displayed status in sync. The dialog already shows its own in-dialog state; the parent only needs to reload on close.

---

## Technical Changes Required

### 1. `FamilyMemberList.vue`

**File:** `frontend/src/components/family-units/FamilyMemberList.vue`

**Changes:**

Add `manageMembership` to the emits block:

```typescript
const emit = defineEmits<{
  edit: [member: FamilyMemberResponse]
  delete: [member: FamilyMemberResponse]
  manageMembership: [member: FamilyMemberResponse]   // ← NEW
}>()
```

Add a prop for role visibility:

```typescript
const props = defineProps<{
  members: FamilyMemberResponse[]
  loading?: boolean
  canManageMemberships?: boolean   // ← NEW: passed from parent based on auth.isBoard
}>()
```

Add button in the "Acciones" column, before the Edit button:

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

- The button uses `pi-id-card` icon (already used in ProfilePage for the role icon — consistent).
- `canManageMemberships` is a boolean prop rather than importing the auth store directly in the list component — this keeps the component dumb and testable.

---

### 2. `FamilyUnitPage.vue`

**File:** `frontend/src/views/FamilyUnitPage.vue`

**New imports:**

```typescript
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
import { useAuthStore } from '@/stores/auth'
import type { FamilyMemberResponse } from '@/types/family-unit'
```

**New state:**

```typescript
const auth = useAuthStore()

// Membership dialog state
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<FamilyMemberResponse | null>(null)
```

**New handler:**

```typescript
const handleManageMembership = (member: FamilyMemberResponse) => {
  selectedMemberForMembership.value = member
  showMembershipDialog.value = true
}
```

**Template changes:**

Pass `canManageMemberships` to `FamilyMemberList` and listen to the new emit:

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

Add `MembershipDialog` at the bottom of the template:

```html
<MembershipDialog
  v-if="selectedMemberForMembership"
  v-model:visible="showMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :member-id="selectedMemberForMembership.id"
  :member-name="`${selectedMemberForMembership.firstName} ${selectedMemberForMembership.lastName}`"
/>
```

**No data refresh needed** in `FamilyUnitPage` — the page does not display membership status (it only shows the members table). If we add membership status to the table in the future, a reload would be needed.

---

### 3. `ProfilePage.vue`

**File:** `frontend/src/views/ProfilePage.vue`

**New import:**

```typescript
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
```

**New state:**

```typescript
// Membership dialog state
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<MemberMembershipData | null>(null)
```

**New handler:**

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

**Template changes:**

Add "Gestionar membresía" button in each member row (next to the existing "Pagar cuota" button), board/admin only:

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

Add `MembershipDialog` at the bottom of the template (alongside `PayFeeDialog`):

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

**Note on `v-model:visible` vs `@update:visible`:**
`MembershipDialog` uses `v-model:visible`. To trigger the reload on close, intercept the `update:visible` event when the value becomes `false`.

---

## Files to Modify

| File | Change |
|---|---|
| `frontend/src/components/family-units/FamilyMemberList.vue` | Add `manageMembership` emit, `canManageMemberships` prop, "Gestionar membresía" button |
| `frontend/src/views/FamilyUnitPage.vue` | Import `MembershipDialog`, add dialog state and handler, wire to `FamilyMemberList` |
| `frontend/src/views/ProfilePage.vue` | Import `MembershipDialog`, add dialog state, reload on close, add "Gestionar membresía" button per member row |

**No backend changes. No new components. No migrations.**

---

## TDD Test Cases

### Unit Tests — `FamilyMemberList`

**File:** `frontend/src/components/__tests__/FamilyMemberList.spec.ts` (create if not exists)

- `renders manageMembership button when canManageMemberships is true`
- `does not render manageMembership button when canManageMemberships is false`
- `emits manageMembership with member when button is clicked`
- `does not emit manageMembership on edit/delete clicks`

### Unit Tests — `ProfilePage`

**File:** `frontend/src/views/__tests__/ProfilePage.spec.ts` (create if not exists)

- `shows manage-membership button for board user when member has no membership`
- `shows manage-membership button for board user when member has active membership`
- `does not show manage-membership button for non-board user`
- `opens MembershipDialog with correct memberId and familyUnitId when button clicked`
- `reloads memberData when MembershipDialog is closed`

### Cypress E2E — membership management from ProfilePage

**File:** `frontend/cypress/e2e/membership-management.cy.ts` (create if not exists)

```
Scenario: Board user activates a membership from Mi Perfil
  Given a family unit with a member who has no membership
  And the current user is a Board member
  When the user visits /profile
  And clicks "Gestionar membresía" on a member card
  Then MembershipDialog opens showing "Sin membresía activa"
  When the user selects a start date and clicks "Activar membresía"
  Then POST /api/family-units/{id}/members/{id}/membership is called
  And the dialog shows the new membership as active
  When the user closes the dialog
  Then the member card in the page refreshes and shows "Socio activo"

Scenario: Non-board user does NOT see manage button
  Given a family unit with members
  And the current user is a regular (non-board) user
  When the user visits /profile
  Then no "Gestionar membresía" button is visible

Scenario: Board user deactivates a membership from Mi Perfil
  Given a member with an active membership
  When the user opens MembershipDialog
  And clicks "Desactivar membresía" and confirms
  Then DELETE /api/family-units/{id}/members/{id}/membership is called
  And on dialog close, the member card shows "Membresía inactiva"
```

---

## Acceptance Criteria

- [ ] "Gestionar membresía" button appears in `FamilyMemberList` only when `canManageMemberships=true`
- [ ] Clicking the button in `FamilyUnitPage` opens `MembershipDialog` with the correct `familyUnitId`, `memberId`, and `memberName`
- [ ] Clicking the button in `ProfilePage` opens `MembershipDialog` with the correct IDs (board user only)
- [ ] Non-board users on `ProfilePage` do NOT see the "Gestionar membresía" button
- [ ] Creating a membership via `MembershipDialog` from `ProfilePage` and then closing updates the member card status to "Socio activo"
- [ ] Deactivating a membership via `MembershipDialog` from `ProfilePage` and then closing updates the member card status to "Membresía inactiva"
- [ ] No regression on existing "Pagar cuota" flow in `ProfilePage`
- [ ] No regression on edit/delete member actions in `FamilyUnitPage`
- [ ] `ConfirmDialog` for membership deactivation works correctly (PrimeVue confirm service must be available in the dialog tree)

---

## Implementation Notes

1. **`ConfirmDialog` availability**: `MembershipDialog` internally uses `useConfirm()` for the deactivate confirmation. `ConfirmDialog` must be mounted in the DOM. Both `ProfilePage` and `FamilyUnitPage` should include `<ConfirmDialog />` if not already present. Check `FamilyUnitPage` — it already imports `ConfirmDialog`. For `ProfilePage`, it does not currently use `useConfirm`; add `ConfirmDialog` import and mount it.

2. **`v-if` on MembershipDialog**: Use `v-if="selectedMemberForMembership"` to avoid mounting the dialog with empty props on initial render. The dialog fetches data on `visible=true` via a watcher — using `v-if` also ensures the composable state is fresh each time.

3. **Reload strategy in ProfilePage**: `loadMemberMembershipData()` already exists and re-fetches all member memberships in parallel. Call it on dialog close. Do not call it on every dialog open — only on close.

4. **`canManageMemberships` vs auth store in list component**: The list receives a boolean prop from the parent. The parent derives it from `auth.isBoard`. This pattern keeps `FamilyMemberList` agnostic of the auth layer and consistent with how it currently receives `members` and `loading`.

5. **`useConfirm` in MembershipDialog**: The existing `MembershipDialog.vue` already calls `confirm.require(...)`. Verify it imports `useConfirm` and that `ConfirmDialog` is present in the parent tree before assuming it works.

6. **No data changes on FamilyUnitPage**: The page shows only a DataTable of members (name, age, relationship, contact, health). It does not show membership status. Therefore, no reload is needed when the dialog closes. If membership status columns are added to the table in the future, a reload hook would be needed.

---

## Security Considerations

- The "Gestionar membresía" button is gated on `auth.isBoard` in the UI. Backend authorization is already enforced (endpoints require authentication; service validates the family unit ownership).
- No sensitive data is exposed in the dialog that isn't already returned by the membership endpoints.

---

## Document Control

- **Feature ID**: `feat-my-memberships-dialog`
- **Date**: 2026-02-22
- **Status**: Ready for implementation
- **Approach**: Wire existing `MembershipDialog.vue` into `FamilyUnitPage.vue` and `ProfilePage.vue`
- **Depends on**: `feat-membership-and-guests` (backend already merged; frontend composables and components already implemented)
- **No migration required**
- **No new npm packages required**

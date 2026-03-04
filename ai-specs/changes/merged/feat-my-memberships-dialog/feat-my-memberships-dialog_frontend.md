# Frontend Implementation Plan: feat-my-memberships-dialog — Integrate MembershipDialog into management views

**Source spec:** [feat-my-memberships-dialog_enriched.md](./feat-my-memberships-dialog_enriched.md)
**Status:** Ready for Implementation
**Architecture:** Vue 3 Composition API, composable-based, PrimeVue + Tailwind CSS

---

## Overview

Wire the already-implemented `MembershipDialog.vue` into `FamilyUnitPage.vue` and `ProfilePage.vue` so that Board/Admin users can create and manage memberships for family members from the UI.

**No new components. No new composables. No new routes. No backend changes.**

The work is strictly:

1. Add a `canManageMemberships` prop + `manageMembership` emit to `FamilyMemberList.vue`
2. Import and wire `MembershipDialog` into `FamilyUnitPage.vue`
3. Import and wire `MembershipDialog` into `ProfilePage.vue` (with reload-on-close)

This plan follows **TDD**: tests are written first for each file change, then the implementation follows.

---

## Architecture Context

### Files to Modify (3 total)

| File | Change |
|---|---|
| `frontend/src/components/family-units/FamilyMemberList.vue` | Add `canManageMemberships` prop, `manageMembership` emit, action button |
| `frontend/src/views/FamilyUnitPage.vue` | Import `MembershipDialog`, add state + handler, wire to list |
| `frontend/src/views/ProfilePage.vue` | Import `MembershipDialog`, add state, "Gestionar" button (board only), reload on close |

### Test Files to Create

| File | Scope |
|---|---|
| `frontend/src/components/family-units/__tests__/FamilyMemberList.spec.ts` | Component unit tests |
| `frontend/src/views/__tests__/ProfilePage.spec.ts` | View unit tests (membership dialog wiring) |

### Components Already Available (no changes needed)

- `frontend/src/components/memberships/MembershipDialog.vue` — fully implemented, has its own internal `<ConfirmDialog />` (no need to add one in parent pages)
- `frontend/src/components/memberships/PayFeeDialog.vue` — already wired in `ProfilePage.vue`
- `frontend/src/composables/useMemberships.ts` — fully implemented

### State Management

No new Pinia store. State is local `ref` in each parent view, consistent with existing patterns.

`auth.isBoard` from `useAuthStore()` drives visibility of the management button.

---

## Key Implementation Facts (Read Before Coding)

These were confirmed by reading the actual source files:

1. **`MembershipDialog.vue` already mounts its own `<ConfirmDialog />`** (line 119 of the component). Parent pages do **not** need to add `ConfirmDialog` for the deactivate confirmation. The enriched spec Note 1 is incorrect — ignore it.

2. **`FamilyUnitPage.vue` already has `<ConfirmDialog />`** at the top of its template for its own member-delete flow. This coexists fine with `MembershipDialog`'s internal one.

3. **`ProfilePage.vue` does NOT have `ConfirmDialog`** — but this is fine because `MembershipDialog` handles it internally.

4. **`MembershipDialog` watcher** fetches membership when `visible` becomes `true` (and `familyUnitId`/`memberId` are truthy). Using `v-if="selectedMemberForMembership"` on the dialog ensures the composable state is fresh each time.

5. **`ProfilePage.vue` member row structure** — the action buttons live in:

   ```html
   <div class="flex flex-wrap items-center gap-2">
     <!-- existing tags + Pagar cuota button -->
   </div>
   ```

   Add "Gestionar membresía" button here, before or after "Pagar cuota".

6. **Close detection in `ProfilePage`** — intercept `@update:visible` to reload:

   ```html
   @update:visible="(val) => { if (!val) handleMembershipDialogClose() }"
   ```

---

## Implementation Steps

---

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/feat-my-memberships-dialog-frontend`
- **Implementation Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-my-memberships-dialog-frontend`
  3. `git branch` — verify you are on the new branch
- **Notes**: Must be done before any code changes. Do NOT reuse the current `feature/feat-seq-healthcheck-backend` branch.

---

### Step 1 (TDD — RED): Write failing unit tests for `FamilyMemberList.vue`

- **File**: `frontend/src/components/family-units/__tests__/FamilyMemberList.spec.ts` (CREATE)
- **Action**: Write tests that will fail until the implementation is done
- **Implementation Steps**:

  1. Create the file with the following test structure:

  ```typescript
  import { describe, it, expect } from 'vitest'
  import { mount } from '@vue/test-utils'
  import FamilyMemberList from '../FamilyMemberList.vue'
  import type { FamilyMemberResponse } from '@/types/family-unit'

  // Minimal PrimeVue stub setup — follow existing test patterns in the project
  const mockMember: FamilyMemberResponse = {
    id: 'member-1',
    familyUnitId: 'unit-1',
    firstName: 'Ana',
    lastName: 'García',
    dateOfBirth: '1990-05-15',
    relationship: 'Spouse',
    email: null,
    phone: null,
    hasMedicalNotes: false,
    hasAllergies: false,
    userId: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  }

  describe('FamilyMemberList — manageMembership', () => {
    it('renders manageMembership button when canManageMemberships is true', () => {
      const wrapper = mount(FamilyMemberList, {
        props: { members: [mockMember], loading: false, canManageMemberships: true },
        global: { stubs: { DataTable: true, Column: true, Button: true, Tag: true } }
      })
      // Button with tooltip 'Gestionar membresía' should be present
      expect(wrapper.html()).toContain('Gestionar membresía')
    })

    it('does not render manageMembership button when canManageMemberships is false', () => {
      const wrapper = mount(FamilyMemberList, {
        props: { members: [mockMember], loading: false, canManageMemberships: false },
        global: { stubs: { DataTable: true, Column: true, Button: true, Tag: true } }
      })
      expect(wrapper.html()).not.toContain('Gestionar membresía')
    })

    it('does not render manageMembership button when canManageMemberships is omitted', () => {
      const wrapper = mount(FamilyMemberList, {
        props: { members: [mockMember], loading: false },
        global: { stubs: { DataTable: true, Column: true, Button: true, Tag: true } }
      })
      expect(wrapper.html()).not.toContain('Gestionar membresía')
    })

    it('emits manageMembership with the correct member when button is clicked', async () => {
      const wrapper = mount(FamilyMemberList, {
        props: { members: [mockMember], loading: false, canManageMemberships: true },
        // Use real Button to capture click; stub DataTable/Column for simplicity
        global: { stubs: { DataTable: true, Column: true, Tag: true } }
      })
      // Find the manage-membership button by data-testid or icon
      const btn = wrapper.find('[data-testid="manage-membership-btn-member-1"]')
      await btn.trigger('click')
      expect(wrapper.emitted('manageMembership')).toHaveLength(1)
      expect(wrapper.emitted('manageMembership')![0]).toEqual([mockMember])
    })
  })
  ```

  1. Run tests to confirm they fail:

     ```bash
     cd frontend && npx vitest run src/components/family-units/__tests__/FamilyMemberList.spec.ts
     ```

- **Implementation Notes**:
  - Check `frontend/src/composables/__tests__/useFamilyUnits.spec.ts` for the import and mock patterns used in this project.
  - If PrimeVue components are globally registered in the test setup, adjust stubs accordingly.

---

### Step 2 (TDD — GREEN): Implement `FamilyMemberList.vue` changes

- **File**: `frontend/src/components/family-units/FamilyMemberList.vue` (MODIFY)
- **Action**: Add `canManageMemberships` prop, `manageMembership` emit, and action button

**Change 1 — Add prop:**

```typescript
const props = defineProps<{
  members: FamilyMemberResponse[]
  loading?: boolean
  canManageMemberships?: boolean   // NEW
}>()
```

**Change 2 — Add emit:**

```typescript
const emit = defineEmits<{
  edit: [member: FamilyMemberResponse]
  delete: [member: FamilyMemberResponse]
  manageMembership: [member: FamilyMemberResponse]   // NEW
}>()
```

**Change 3 — Add button in "Acciones" column template, BEFORE the edit button:**

```html
<!-- In the Acciones column body template, before the pi-pencil button -->
<Button
  v-if="props.canManageMemberships"
  icon="pi pi-id-card"
  severity="secondary"
  text
  rounded
  :data-testid="`manage-membership-btn-${data.id}`"
  v-tooltip.top="'Gestionar membresía'"
  @click="emit('manageMembership', data)"
/>
```

- **Implementation Steps**:
  1. Edit the `defineProps` block to add `canManageMemberships?: boolean`
  2. Edit the `defineEmits` block to add `manageMembership: [member: FamilyMemberResponse]`
  3. In the template, locate the "Acciones" `<Column>` body template
  4. Add the button before the existing `pi-pencil` (edit) button
  5. Run the tests again — they should now pass:

     ```bash
     cd frontend && npx vitest run src/components/family-units/__tests__/FamilyMemberList.spec.ts
     ```

---

### Step 3 (TDD — RED): Write failing unit tests for `FamilyUnitPage.vue` integration

- **File**: `frontend/src/views/__tests__/FamilyUnitPage.spec.ts` (CREATE or add to existing)
- **Action**: Write tests for the membership dialog wiring

```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import FamilyUnitPage from '../FamilyUnitPage.vue'

vi.mock('@/composables/useFamilyUnits', () => ({
  useFamilyUnits: () => ({
    familyUnit: { value: { id: 'unit-1', name: 'Test Family', createdAt: '2026-01-01', updatedAt: '2026-01-01' } },
    familyMembers: { value: [{ id: 'member-1', firstName: 'Ana', lastName: 'García', dateOfBirth: '1990-01-01', relationship: 'Spouse', hasMedicalNotes: false, hasAllergies: false, email: null, phone: null, userId: null, familyUnitId: 'unit-1', createdAt: '2026-01-01', updatedAt: '2026-01-01' }] },
    loading: { value: false },
    error: { value: null },
    getCurrentUserFamilyUnit: vi.fn().mockResolvedValue(null),
    getFamilyMembers: vi.fn().mockResolvedValue(undefined),
    createFamilyUnit: vi.fn(),
    updateFamilyUnit: vi.fn(),
    deleteFamilyUnit: vi.fn(),
    createFamilyMember: vi.fn(),
    updateFamilyMember: vi.fn(),
    deleteFamilyMember: vi.fn(),
  })
}))

describe('FamilyUnitPage — membership dialog', () => {
  it('passes canManageMemberships=true to FamilyMemberList when user isBoard', () => {
    const wrapper = mount(FamilyUnitPage, {
      global: {
        plugins: [createTestingPinia({ initialState: { auth: { user: { role: 'Board' } } } })],
        stubs: { FamilyMemberList: true, MembershipDialog: true, ConfirmDialog: true, Dialog: true, Card: true }
      }
    })
    const list = wrapper.findComponent({ name: 'FamilyMemberList' })
    expect(list.props('canManageMemberships')).toBe(true)
  })

  it('passes canManageMemberships=false to FamilyMemberList when user is Member', () => {
    const wrapper = mount(FamilyUnitPage, {
      global: {
        plugins: [createTestingPinia({ initialState: { auth: { user: { role: 'Member' } } } })],
        stubs: { FamilyMemberList: true, MembershipDialog: true, ConfirmDialog: true, Dialog: true, Card: true }
      }
    })
    const list = wrapper.findComponent({ name: 'FamilyMemberList' })
    expect(list.props('canManageMemberships')).toBe(false)
  })

  it('opens MembershipDialog when manageMembership event is emitted', async () => {
    const wrapper = mount(FamilyUnitPage, {
      global: {
        plugins: [createTestingPinia({ initialState: { auth: { user: { role: 'Board' } } } })],
        stubs: { FamilyMemberList: true, MembershipDialog: true, ConfirmDialog: true, Dialog: true, Card: true }
      }
    })
    const member = { id: 'member-1', firstName: 'Ana', lastName: 'García' }
    const list = wrapper.findComponent({ name: 'FamilyMemberList' })
    await list.vm.$emit('manageMembership', member)
    const dialog = wrapper.findComponent({ name: 'MembershipDialog' })
    expect(dialog.props('visible')).toBe(true)
    expect(dialog.props('memberId')).toBe('member-1')
  })
})
```

Run to confirm failure:

```bash
cd frontend && npx vitest run src/views/__tests__/FamilyUnitPage.spec.ts
```

---

### Step 4 (TDD — GREEN): Implement `FamilyUnitPage.vue` changes

- **File**: `frontend/src/views/FamilyUnitPage.vue` (MODIFY)
- **Action**: Import `MembershipDialog`, add auth store, add dialog state + handler, wire to template

**Change 1 — New imports (add to existing imports block):**

```typescript
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
import { useAuthStore } from '@/stores/auth'
```

**Change 2 — New state (add after existing const declarations):**

```typescript
const auth = useAuthStore()

// Membership dialog
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<FamilyMemberResponse | null>(null)
```

**Change 3 — New handler (add after `handleDeleteMember`):**

```typescript
const handleManageMembership = (member: FamilyMemberResponse) => {
  selectedMemberForMembership.value = member
  showMembershipDialog.value = true
}
```

**Change 4 — Template: update `FamilyMemberList` usage:**

Locate this in the template:

```html
<FamilyMemberList
  :members="familyMembers"
  :loading="loading"
  @edit="openEditMemberDialog"
  @delete="handleDeleteMember"
/>
```

Replace with:

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

**Change 5 — Template: add `MembershipDialog` at the bottom (after the Family Member Dialog block):**

```html
<!-- Membership Dialog -->
<MembershipDialog
  v-if="selectedMemberForMembership"
  v-model:visible="showMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :member-id="selectedMemberForMembership.id"
  :member-name="`${selectedMemberForMembership.firstName} ${selectedMemberForMembership.lastName}`"
/>
```

- **Implementation Steps**:
  1. Add imports
  2. Add `auth` store instance and dialog state
  3. Add `handleManageMembership` handler
  4. Update `<FamilyMemberList>` in template
  5. Add `<MembershipDialog>` in template
  6. Run tests — they should pass:

     ```bash
     cd frontend && npx vitest run src/views/__tests__/FamilyUnitPage.spec.ts
     ```

---

### Step 5 (TDD — RED): Write failing unit tests for `ProfilePage.vue` changes

- **File**: `frontend/src/views/__tests__/ProfilePage.spec.ts` (CREATE)
- **Action**: Write tests for the membership dialog integration in ProfilePage

```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import ProfilePage from '../ProfilePage.vue'

vi.mock('@/composables/useProfile', () => ({
  useProfile: () => ({ fullUser: { value: null }, loading: { value: false }, error: { value: null }, loadProfile: vi.fn(), updateProfile: vi.fn() })
}))
vi.mock('@/composables/useFamilyUnits', () => ({
  useFamilyUnits: () => ({
    familyUnit: { value: { id: 'unit-1', name: 'Test Family', createdAt: '2026-01-01', updatedAt: '2026-01-01' } },
    familyMembers: { value: [] },
    getCurrentUserFamilyUnit: vi.fn().mockResolvedValue(null),
    getFamilyMembers: vi.fn().mockResolvedValue(undefined),
  })
}))
vi.mock('@/composables/useMemberships', () => ({
  useMemberships: () => ({
    getMembership: vi.fn().mockResolvedValue(null),
    payFee: vi.fn(),
  })
}))

const boardState = { auth: { user: { id: 'u1', email: 'board@test.com', firstName: 'Board', lastName: 'User', role: 'Board' }, token: 'tok' } }
const memberState = { auth: { user: { id: 'u1', email: 'member@test.com', firstName: 'Ana', lastName: 'García', role: 'Member' }, token: 'tok' } }

describe('ProfilePage — membership management button', () => {
  it('does not show manage-membership buttons for non-board user', async () => {
    const wrapper = mount(ProfilePage, {
      global: {
        plugins: [createTestingPinia({ initialState: memberState })],
        stubs: { Container: true, Card: true, PayFeeDialog: true, MembershipDialog: true, Skeleton: true }
      }
    })
    expect(wrapper.find('[data-testid^="manage-membership-btn-"]').exists()).toBe(false)
  })

  // Board user tests require memberData to be loaded — skip or use shallow rendering
  // These are covered by Cypress E2E tests instead
})

describe('ProfilePage — MembershipDialog mounting', () => {
  it('does not mount MembershipDialog on initial render (v-if guard)', async () => {
    const wrapper = mount(ProfilePage, {
      global: {
        plugins: [createTestingPinia({ initialState: boardState })],
        stubs: { Container: true, Card: true, PayFeeDialog: true, MembershipDialog: true, Skeleton: true }
      }
    })
    // MembershipDialog is v-if="selectedMemberForMembership" — should not be present initially
    const dialog = wrapper.findComponent({ name: 'MembershipDialog' })
    expect(dialog.exists()).toBe(false)
  })
})
```

Run to confirm failure:

```bash
cd frontend && npx vitest run src/views/__tests__/ProfilePage.spec.ts
```

---

### Step 6 (TDD — GREEN): Implement `ProfilePage.vue` changes

- **File**: `frontend/src/views/ProfilePage.vue` (MODIFY)
- **Action**: Import `MembershipDialog`, add dialog state, add button per member row, add reload on close

**Change 1 — New import (add to existing imports):**

```typescript
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
```

**Change 2 — New state (add after existing `payFeeLoading` declaration):**

```typescript
// Membership management dialog state
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<MemberMembershipData | null>(null)
```

**Change 3 — New handlers (add after `handlePayFee`):**

```typescript
const openMembershipDialog = (data: MemberMembershipData) => {
  selectedMemberForMembership.value = data
  showMembershipDialog.value = true
}

const handleMembershipDialogClose = async () => {
  showMembershipDialog.value = false
  selectedMemberForMembership.value = null
  // Reload to reflect any create/deactivate changes
  await loadMemberMembershipData()
}
```

**Change 4 — Template: add "Gestionar membresía" button in each member row**

Locate the existing `<Button v-if="auth.isBoard && ..."` (the "Pagar cuota" button) and add the new button **before** it:

```html
<!-- Add BEFORE the existing "Pagar cuota" button -->
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

**Change 5 — Template: add `MembershipDialog` at the bottom (alongside `PayFeeDialog`)**

```html
<!-- Add after the existing PayFeeDialog -->
<MembershipDialog
  v-if="selectedMemberForMembership"
  v-model:visible="showMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :member-id="selectedMemberForMembership.member.id"
  :member-name="`${selectedMemberForMembership.member.firstName} ${selectedMemberForMembership.member.lastName}`"
  @update:visible="(val) => { if (!val) handleMembershipDialogClose() }"
/>
```

- **Implementation Steps**:
  1. Add import
  2. Add `showMembershipDialog` and `selectedMemberForMembership` state
  3. Add `openMembershipDialog` and `handleMembershipDialogClose` handlers
  4. Add "Gestionar membresía" button in the member row template
  5. Add `<MembershipDialog>` at the bottom of the template
  6. Run tests — they should pass:

     ```bash
     cd frontend && npx vitest run src/views/__tests__/ProfilePage.spec.ts
     ```

---

### Step 7: Manual smoke test

- **Action**: Start the dev server and verify the feature end-to-end
- **Implementation Steps**:
  1. `cd frontend && npm run dev`
  2. Log in as a Board user
  3. Navigate to `/profile` (Mi Perfil)
  4. Verify "Gestionar membresía" button appears on each member card
  5. Click the button for a member with no membership → `MembershipDialog` opens showing "Sin membresía activa"
  6. Select a start date and click "Activar membresía" → success toast, dialog shows "Socio activo"
  7. Close the dialog → member card refreshes showing "Socio activo"
  8. Navigate to `/family-unit/me`
  9. Verify `pi-id-card` icon button appears in the "Acciones" column of the members table
  10. Click it → `MembershipDialog` opens correctly
  11. Log in as a regular Member user → "Gestionar membresía" button is NOT visible in `/profile`
  12. Verify existing "Pagar cuota" flow in `/profile` still works (no regression)
  13. Verify member edit/delete in `/family-unit/me` still works (no regression)

---

### Step 8: Run full test suite

```bash
cd frontend && npx vitest run
```

All tests must pass, including pre-existing ones.

---

### Step 9: Update Technical Documentation

- **Action**: Review changes and update documentation if needed
- **Implementation Steps**:
  1. No new API endpoints were added → `api-spec.yml` unchanged
  2. No new components created → `frontend-standards.mdc` structure unchanged
  3. No new npm packages → `package.json` unchanged
  4. Update this plan file with any deviations made during implementation
  5. Confirm all code comments are in English

---

## Implementation Order

1. Step 0 — Create branch `feature/feat-my-memberships-dialog-frontend`
2. Step 1 — Write failing tests for `FamilyMemberList.vue`
3. Step 2 — Implement `FamilyMemberList.vue` (RED → GREEN)
4. Step 3 — Write failing tests for `FamilyUnitPage.vue`
5. Step 4 — Implement `FamilyUnitPage.vue` (RED → GREEN)
6. Step 5 — Write failing tests for `ProfilePage.vue`
7. Step 6 — Implement `ProfilePage.vue` (RED → GREEN)
8. Step 7 — Manual smoke test
9. Step 8 — Run full test suite
10. Step 9 — Update documentation

---

## Testing Checklist

### Unit Tests (Vitest)

- [ ] `FamilyMemberList.spec.ts` — renders manage button when `canManageMemberships=true`
- [ ] `FamilyMemberList.spec.ts` — does NOT render manage button when `canManageMemberships=false`
- [ ] `FamilyMemberList.spec.ts` — does NOT render manage button when prop is omitted (default)
- [ ] `FamilyMemberList.spec.ts` — emits `manageMembership` with correct member on click
- [ ] `FamilyUnitPage.spec.ts` — passes `canManageMemberships=true` to list when user is Board
- [ ] `FamilyUnitPage.spec.ts` — passes `canManageMemberships=false` to list when user is Member
- [ ] `FamilyUnitPage.spec.ts` — opens `MembershipDialog` on `manageMembership` emit from list
- [ ] `ProfilePage.spec.ts` — does NOT mount `MembershipDialog` on initial render (v-if guard)
- [ ] `ProfilePage.spec.ts` — does NOT show manage buttons for non-board user

### Manual / E2E Verification

- [ ] Board user on `/profile` sees "Gestionar membresía" button for each member
- [ ] Non-board user on `/profile` does NOT see "Gestionar membresía" button
- [ ] Board user on `/family-unit/me` sees `pi-id-card` action button in members table
- [ ] Clicking button opens `MembershipDialog` with correct `familyUnitId`, `memberId`, `memberName`
- [ ] Creating a membership from `MembershipDialog` (in ProfilePage) → dialog reloads and shows "Socio activo" → closing dialog refreshes member card
- [ ] Deactivating a membership → card shows "Membresía inactiva" after close
- [ ] "Pagar cuota" button still works (no regression)
- [ ] Member edit/delete in `FamilyUnitPage` still works (no regression)
- [ ] Deactivate confirmation dialog works correctly from `MembershipDialog`
- [ ] Responsive on mobile (< 640px): dialog is full width, readable

---

## Error Handling Patterns

All error handling is already inside `MembershipDialog.vue` and `useMemberships.ts`:

- API errors → toast via `useToast()` inside `MembershipDialog`
- 404 (no membership) → handled silently in `getMembership`, shows "Sin membresía activa" state
- 409 (already has membership) → backend Spanish message surfaced via toast
- Parent pages (`ProfilePage`, `FamilyUnitPage`) do not need to handle errors from the membership API — the dialog is self-contained

---

## UI/UX Considerations

- **"Gestionar membresía" button** in `ProfilePage` member rows uses `severity="secondary"` + `outlined` to visually differentiate from the primary "Pagar cuota" button
- **`pi-id-card` icon** is consistent with the existing role icon used in the ProfilePage personal info section
- **`v-tooltip.top="'Gestionar membresía'"`** on the icon-only button in `FamilyMemberList` ensures accessibility without cluttering the table
- **Reload on close** in `ProfilePage` happens via `handleMembershipDialogClose` — only triggers when dialog closes (not on open), avoiding unnecessary API calls
- **`v-if="selectedMemberForMembership"`** on `MembershipDialog` ensures: (1) clean composable state on each open, (2) no watcher firing with empty props on initial render
- **Dialog width**: `class="w-full max-w-2xl"` is already set inside `MembershipDialog.vue` — no changes needed in parent

---

## Dependencies

All dependencies are already installed. No new npm packages required.

| Package | Already Available |
|---|---|
| PrimeVue (Dialog, Button, Tag, etc.) | ✅ |
| `@pinia/testing` | ✅ (check package.json before assuming) |
| `@vue/test-utils` | ✅ |
| Vitest | ✅ |

---

## Notes

1. **Branch naming**: Use `feature/feat-my-memberships-dialog-frontend` (with `-frontend` suffix per `frontend-standards.mdc`)
2. **No auth guard changes**: The existing route guards already protect `/profile` and `/family-unit/me` for authenticated users. The board-only visibility of the button is enforced via `v-if="auth.isBoard"` in the template.
3. **`auth.isAdmin` consideration**: The enriched spec says "Board/Admin only". Looking at the auth store, `auth.isBoard` is a computed that returns `true` for both `Admin` and `Board` roles (per frontend-standards.mdc line 553). So using `auth.isBoard` is sufficient — no separate `auth.isAdmin` check is needed.
4. **`canManageMemberships` prop**: The list receives a boolean from the parent rather than importing auth directly. This keeps `FamilyMemberList` a "dumb" presentational component, consistent with how it currently handles `members` and `loading`.
5. **User-facing text in Spanish** (per project standards): "Gestionar membresía", "Gestionar membresía" tooltip.
6. **Code, variables, comments in English** (per base-standards.mdc).

---

## Next Steps After Implementation

1. Create a PR targeting `main` with title following `feat(memberships): wire MembershipDialog into FamilyUnitPage and ProfilePage`
2. Link PR to the `feat-my-memberships-dialog` feature
3. Request review from another team member
4. After merge, verify that boards can create memberships in production for existing members

---

## Implementation Verification

Before marking this ticket done:

- [ ] **Code Quality**: All changed files use `<script setup lang="ts">`, no `any` types, no `<style>` blocks
- [ ] **TDD**: Tests were written BEFORE implementation for each file change
- [ ] **Functionality**: `MembershipDialog` opens correctly from both `FamilyUnitPage` and `ProfilePage`; member card data reloads after close in `ProfilePage`
- [ ] **Testing**: All new unit tests pass; full `vitest run` passes with no regressions
- [ ] **Authorization**: Board/Admin see the button; regular members do not
- [ ] **Integration**: Dialog correctly passes `familyUnitId`, `memberId`, and `memberName` from parent
- [ ] **No Regression**: Existing pay fee flow, member edit/delete, and family unit edit/delete all work
- [ ] **Documentation**: Plan updated with any deviations; code comments in English

---

**Document Version:** 1.0
**Created:** 2026-02-22
**Feature ID:** `feat-my-memberships-dialog`
**Status:** Ready for Implementation

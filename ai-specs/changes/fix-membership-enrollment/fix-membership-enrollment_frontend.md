# Frontend Implementation Plan: fix-membership-enrollment — Membership Enrollment & Fee Fixes

## Overview

This plan addresses four frontend gaps exposed by the backend membership bugs:

1. **BulkMembershipDialog shows wrong count** — `FamilyUnitPage` passes `:member-data="[]"` to the dialog. The dialog's fallback (`if memberData.length === 0 → return all members`) incorrectly shows all members as "sin alta de socio/a" regardless of their real membership state.
2. **MembershipDialog has no "Reactivate" flow** — When `membership.isActive === false`, the dialog only shows a status tag with no action available.
3. **MembershipDialog has no "Create annual fee" flow** — When `membership.isActive === true` but no fee exists for the current year, the UI shows "No hay cuotas registradas todavía" with no action.
4. **Missing composable functions and types** — `createFee` and `reactivateMembership` API calls don't exist yet.

**Architecture:** Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue + Tailwind CSS, composable-based API layer.

---

## Architecture Context

### Files to Modify

| File | Change |
|------|--------|
| `frontend/src/types/membership.ts` | Add `CreateMembershipFeeRequest`, `ReactivateMembershipRequest` |
| `frontend/src/composables/useMemberships.ts` | Add `createFee()`, `reactivateMembership()` |
| `frontend/src/components/memberships/BulkMembershipDialog.vue` | Self-load membership data on open; remove faulty fallback |
| `frontend/src/components/memberships/MembershipDialog.vue` | Add reactivate + create fee flows |

### Files to Create

| File | Purpose |
|------|---------|
| `frontend/src/components/memberships/CreateFeeDialog.vue` | Reusable dialog for manually creating an annual fee |

### Files to Update (Tests)

| File | Change |
|------|--------|
| `frontend/src/composables/__tests__/useMemberships.test.ts` | Add tests for `createFee` and `reactivateMembership` |
| `frontend/src/components/memberships/__tests__/BulkMembershipDialog.spec.ts` | Update to reflect self-loading behavior |
| `frontend/src/components/memberships/__tests__/MembershipDialog.spec.ts` *(new)* | Tests for reactivate and create fee flows |

### No routing changes required.
### No Pinia store changes required (all state is local to composable calls per dialog).

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Switch to `dev`, pull latest, create frontend branch.
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/fix-membership-enrollment-frontend`
  3. `git branch` — verify you are on the new branch.

---

### Step 1: Add TypeScript Types

**File:** `frontend/src/types/membership.ts`

Add after the existing `BulkActivateMembershipRequest` interface:

```typescript
/** Admin/Board: manually create an annual fee for an existing membership. */
export interface CreateMembershipFeeRequest {
  year: number   // > 2000 and <= current year
  amount: number // >= 0
}

/** Admin/Board: reactivate a previously deactivated membership. */
export interface ReactivateMembershipRequest {
  year: number   // > 2000 and <= current year
}
```

Also extend `MemberMembershipData` with a derived status field to allow consumers to differentiate between states without re-deriving them:

```typescript
export type MembershipStatus = 'none' | 'active' | 'activeFeePending' | 'inactive'

export interface MemberMembershipData {
  member: FamilyMemberResponse
  membershipId: string | null
  isActiveMembership: boolean
  currentFee: MembershipFeeResponse | null
  feeLoading: boolean
  /** Derived status for display. */
  membershipStatus: MembershipStatus
}
```

> **Note:** `membershipStatus` derivation logic:
> - `none` → `membershipId === null`
> - `inactive` → `membershipId !== null && !isActiveMembership`
> - `active` → `isActiveMembership && currentFee?.status === FeeStatus.Paid`
> - `activeFeePending` → `isActiveMembership && currentFee?.status !== FeeStatus.Paid` (Pending, Overdue, or no fee at all)
>
> Any existing code building `MemberMembershipData` objects (e.g., `ProfilePage.vue`) must be updated to include the `membershipStatus` field.

---

### Step 2: Add Composable Functions

**File:** `frontend/src/composables/useMemberships.ts`

Add the following imports at the top (alongside existing imports):

```typescript
import type {
  // ...existing...
  CreateMembershipFeeRequest,
  ReactivateMembershipRequest,
} from '@/types/membership'
```

Add two new functions inside `useMemberships()`, before the `return` statement:

#### `createFee`

```typescript
/**
 * Manually create an annual fee for an existing membership.
 * Admin/Board only.
 */
const createFee = async (
  membershipId: string,
  request: CreateMembershipFeeRequest,
): Promise<MembershipFeeResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.post<ApiResponse<MembershipFeeResponse>>(
      `/memberships/${membershipId}/fees`,
      request,
    )
    fees.value = [...fees.value, response.data.data]
    return response.data.data
  } catch (err: any) {
    error.value = err.response?.data?.error?.message || 'Error al crear la cuota'
    return null
  } finally {
    loading.value = false
  }
}
```

#### `reactivateMembership`

```typescript
/**
 * Reactivate a previously deactivated membership.
 * Admin/Board only.
 */
const reactivateMembership = async (
  familyUnitId: string,
  memberId: string,
  request: ReactivateMembershipRequest,
): Promise<MembershipResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.post<ApiResponse<MembershipResponse>>(
      `/family-units/${familyUnitId}/members/${memberId}/membership/reactivate`,
      request,
    )
    membership.value = response.data.data
    fees.value = response.data.data?.fees ?? []
    return response.data.data
  } catch (err: any) {
    error.value = err.response?.data?.error?.message || 'Error al reactivar la membresía'
    return null
  } finally {
    loading.value = false
  }
}
```

Add both functions to the `return` statement.

---

### Step 3: Create `CreateFeeDialog.vue`

**File:** `frontend/src/components/memberships/CreateFeeDialog.vue`

New dialog to create an annual fee for an existing active membership. Modeled after `PayFeeDialog.vue`.

**Props:**
```typescript
defineProps<{
  visible: boolean
  membershipId: string
  loading?: boolean
}>()
```

**Emits:**
```typescript
defineEmits<{
  'update:visible': [value: boolean]
  submit: [request: CreateMembershipFeeRequest]
}>()
```

**Form fields:**
- **Year** — `InputNumber`, min: 2001, max: current year, required, default: current year.
- **Amount (€)** — `InputNumber`, min: 0, mode: `currency`, currency: `EUR`, locale: `es-ES`, required, default: 0.

**Validation:**
- Year must be `> 2000` and `<= new Date().getFullYear()`
- Amount must be `>= 0`
- Show inline field errors using `<small class="text-red-500">` (same pattern as `PayFeeDialog.vue`)

**Behavior:**
- Reset form when dialog opens (`watch(props.visible)`)
- Emit `submit` with `{ year, amount }` — parent calls composable and handles toast

**Template structure:**
```
Dialog (header="Cargar cuota anual", w-full max-w-md)
  InputNumber (year)
  InputNumber (amount, mode=currency)
  div.flex.justify-end.gap-2
    Button (Cancelar, secondary)
    Button (Cargar cuota, icon=pi pi-plus, :loading)
```

---

### Step 4: Update `MembershipDialog.vue`

**File:** `frontend/src/components/memberships/MembershipDialog.vue`

#### 4a — Import new composable functions and dialog

Add to the import block:
```typescript
import CreateFeeDialog from './CreateFeeDialog.vue'
import type { CreateMembershipFeeRequest, ReactivateMembershipRequest } from '@/types/membership'
```

Update the `useMemberships()` destructure to include the new functions:
```typescript
const { membership, fees, loading, error, getMembership, createMembership,
  deactivateMembership, payFee, createFee, reactivateMembership } = useMemberships()
```

#### 4b — Add reactivate state

```typescript
const showCreateFeeDialog = ref(false)
const reactivateYear = ref<number>(currentYear)
```

#### 4c — Add `handleReactivate` handler

```typescript
const handleReactivate = async () => {
  const result = await reactivateMembership(props.familyUnitId, props.memberId, {
    year: reactivateYear.value,
  })
  if (result) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Membresía reactivada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}
```

#### 4d — Add `handleCreateFee` handler

```typescript
const handleCreateFee = async (request: CreateMembershipFeeRequest) => {
  if (!membership.value) return
  const result = await createFee(membership.value.id, request)
  if (result) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Cuota registrada', life: 3000 })
    showCreateFeeDialog.value = false
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}
```

#### 4e — Update template: inactive membership section

Replace the existing inactive section (`<Tag v-else value="Membresía inactiva" .../>`) with:

```html
<!-- Inactive membership: show reactivate option -->
<div v-if="!membership.isActive" class="space-y-4">
  <div class="flex items-center gap-3">
    <Tag value="Membresía inactiva" severity="secondary" />
    <span class="text-sm text-gray-600">Desde {{ formatDate(membership.startDate) }}</span>
  </div>

  <Message severity="warn">
    Esta membresía está desactivada. Puedes reactivarla eligiendo el año de reactivación.
  </Message>

  <div class="flex flex-col gap-2">
    <label class="text-sm font-medium">
      Año de reactivación <span class="text-red-500">*</span>
    </label>
    <InputNumber
      v-model="reactivateYear"
      :min="2001"
      :max="currentYear"
      :use-grouping="false"
      class="w-full"
    />
    <small class="text-gray-500">Se creará una cuota pendiente para este año.</small>
  </div>

  <div class="flex justify-end">
    <Button
      label="Reactivar membresía"
      icon="pi pi-refresh"
      severity="success"
      :loading="loading"
      @click="handleReactivate"
    />
  </div>
</div>
```

#### 4f — Update template: active membership, no current year fee

Replace the existing `v-else-if="membership.isActive && fees.length === 0"` section:

```html
<div
  v-else-if="membership.isActive && fees.length === 0"
  class="space-y-3"
>
  <Message severity="warn">
    No hay cuotas registradas para esta membresía. Carga la cuota del año en curso para
    que la familia pueda inscribirse al campamento.
  </Message>
  <div class="flex justify-end">
    <Button
      label="Cargar cuota anual"
      icon="pi pi-plus"
      severity="primary"
      :loading="loading"
      @click="showCreateFeeDialog = true"
    />
  </div>
</div>
```

Also add a "Cargar cuota anual" button in the fees table section for when the membership is active, has fees, but none for the current year. Add it below the DataTable:

```html
<div class="flex justify-end mt-2">
  <Button
    v-if="!fees.some(f => f.year === currentYear)"
    label="Cargar cuota {{ currentYear }}"
    icon="pi pi-plus"
    size="small"
    severity="secondary"
    outlined
    @click="showCreateFeeDialog = true"
  />
</div>
```

#### 4g — Add CreateFeeDialog at bottom of template

```html
<CreateFeeDialog
  v-if="showCreateFeeDialog && membership"
  v-model:visible="showCreateFeeDialog"
  :membership-id="membership.id"
  :loading="loading"
  @submit="handleCreateFee"
/>
```

---

### Step 5: Fix `BulkMembershipDialog.vue` — Self-load membership data

**File:** `frontend/src/components/memberships/BulkMembershipDialog.vue`

The current fallback `if (props.memberData.length === 0) return props.members` shows ALL members as "sin alta" when no membership data is provided. The fix is to self-load membership data when the dialog opens.

#### 5a — Add internal state

```typescript
import { ref, computed, watch } from 'vue'
import type { MemberMembershipData } from '@/types/membership'

const internalMemberData = ref<MemberMembershipData[]>([])
const loadingMemberData = ref(false)
```

#### 5b — Load membership data on dialog open

```typescript
const { loading, error, bulkActivateMemberships, getMembership } = useMemberships()

watch(
  () => props.visible,
  async (val) => {
    if (!val) return
    // If parent already provides data, use it
    if (props.memberData.length > 0) {
      internalMemberData.value = props.memberData
      return
    }
    // Otherwise self-load
    loadingMemberData.value = true
    try {
      const results = await Promise.all(
        props.members.map(async (member) => {
          const ms = await getMembership(props.familyUnitId, member.id)
          const currentFee = ms?.fees.find((f) => f.year === new Date().getFullYear()) ?? null
          return {
            member,
            membershipId: ms?.id ?? null,
            isActiveMembership: ms?.isActive ?? false,
            currentFee,
            feeLoading: false,
            membershipStatus: !ms ? 'none' as const
              : !ms.isActive ? 'inactive' as const
              : currentFee?.status === 'Paid' ? 'active' as const
              : 'activeFeePending' as const,
          }
        }),
      )
      internalMemberData.value = results
    } finally {
      loadingMemberData.value = false
    }
  },
)
```

#### 5c — Update computed properties to use `internalMemberData`

```typescript
const membersWithoutMembership = computed(() =>
  internalMemberData.value.filter((d) => !d.membershipId),
)

const membersAlreadyActive = computed(() =>
  internalMemberData.value.filter((d) => d.membershipId && d.isActiveMembership),
)
```

Remove the old fallback logic entirely.

#### 5d — Show loading skeleton while fetching

In the template, add a loading state before the content:

```html
<div v-if="loadingMemberData" class="flex items-center justify-center py-6 gap-2 text-gray-500">
  <i class="pi pi-spin pi-spinner" />
  <span>Cargando datos de membresía...</span>
</div>

<template v-else>
  <!-- existing content -->
</template>
```

#### 5e — Reset internal data on close

In `handleClose`:
```typescript
const handleClose = () => {
  if ((result.value?.activated ?? 0) > 0) emit('done')
  emit('update:visible', false)
  result.value = null
  selectedYear.value = currentYear
  internalMemberData.value = []  // reset on close
}
```

---

### Step 6: Update `ProfilePage.vue` — Add `membershipStatus` field

**File:** `frontend/src/views/ProfilePage.vue`

When building `MemberMembershipData` objects, add the `membershipStatus` derived field to comply with the updated type:

```typescript
const membershipStatus: MembershipStatus =
  !ms ? 'none'
  : !ms.isActive ? 'inactive'
  : currentFee?.status === FeeStatus.Paid ? 'active'
  : 'activeFeePending'

return {
  member,
  membershipId: ms?.id ?? null,
  isActiveMembership: ms?.isActive ?? false,
  currentFee,
  feeLoading: false,
  membershipStatus,
}
```

No visual changes needed in ProfilePage for this ticket — it already shows fee status correctly.

---

### Step 7: Write / Update Tests

#### `composables/__tests__/useMemberships.test.ts`

Add tests for the two new functions following the existing mock pattern (`vi.spyOn(api, 'post')`):

| Test | Scenario | Expected |
|------|----------|----------|
| `createFee — success` | API returns 201 with fee | Returns fee, updates `fees.value` |
| `createFee — API error` | API returns 409 | Returns null, sets `error.value` |
| `reactivateMembership — success` | API returns 200 | Returns membership, updates `membership.value` |
| `reactivateMembership — API error (409)` | Member already active | Returns null, sets `error.value` |

#### `components/memberships/__tests__/BulkMembershipDialog.spec.ts`

Update existing tests that assumed the fallback behavior. Add:

| Test | Scenario | Expected |
|------|----------|----------|
| `shows loading state while fetching membership data` | Dialog opens, memberData=[] | Shows loading indicator |
| `shows correct count after self-loading` | 3 members: 2 with active, 1 without | Shows "1 miembro(s) sin alta" |
| `uses parent memberData when provided` | memberData has 5 entries | Does not call getMembership |

#### `components/memberships/__tests__/MembershipDialog.spec.ts` *(new)*

| Test | Scenario | Expected |
|------|----------|----------|
| `shows reactivate section when membership is inactive` | `membership.isActive=false` | Reactivate button visible |
| `calls reactivateMembership on reactivate click` | Board user clicks Reactivar | `reactivateMembership` called with correct args |
| `shows create fee button when active and no fees` | `membership.isActive=true, fees=[]` | "Cargar cuota anual" button visible |
| `shows create fee button when no current year fee` | Active membership, fees for 2024 only, current=2026 | "Cargar cuota 2026" button visible |
| `opens CreateFeeDialog on create fee button click` | Click "Cargar cuota anual" | `showCreateFeeDialog` becomes true |

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Add TypeScript types
3. Step 2 — Add composable functions (`createFee`, `reactivateMembership`)
4. Step 3 — Create `CreateFeeDialog.vue`
5. Step 4 — Update `MembershipDialog.vue` (reactivate + create fee)
6. Step 5 — Fix `BulkMembershipDialog.vue` (self-load, remove fallback)
7. Step 6 — Update `ProfilePage.vue` (add `membershipStatus` field)
8. Step 7 — Tests
9. Step 8 (documentation update) — See below

---

## Documentation Update

**File:** `ai-specs/specs/frontend-standards.mdc`

No structural changes needed. After implementation, verify any pattern changes are reflected.

**Internal:** Update `ai-specs/changes/fix-membership-enrollment/fix-membership-enrollment_enriched.md` to mark Frontend Fix 4 (BulkActivate count) as resolved in this ticket.

---

## Testing Checklist

- [ ] `useMemberships.test.ts` — `createFee` and `reactivateMembership` pass
- [ ] `BulkMembershipDialog.spec.ts` — self-loading behavior tested, old fallback tests removed
- [ ] `MembershipDialog.spec.ts` — reactivate and create-fee flows covered
- [ ] Manual: Open BulkMembership dialog for a family with mixed membership states → count is correct
- [ ] Manual: Open MembershipDialog for a member with `isActive=false` → reactivate UI visible
- [ ] Manual: Open MembershipDialog for a member with active membership but no fees → "Cargar cuota anual" visible
- [ ] Manual: Create fee via CreateFeeDialog → fee appears in table with Pending status
- [ ] Manual: Reactivate membership → dialog refreshes showing active state + new pending fee

---

## Error Handling Patterns

All new composable calls follow the existing pattern:
- `loading.value = true` on start
- `error.value = null` on start
- `error.value = err.response?.data?.error?.message || 'Error fallback'` on catch
- Return `null` on error
- Toast notifications in the component (not in the composable)

HTTP 409 from `createFee` (duplicate year) and `reactivateMembership` (already active) surface via `error.value` → shown in a `toast.add({ severity: 'error', ... })`.

---

## UI/UX Considerations

- **BulkMembershipDialog loading state**: Shows spinner (`pi pi-spin pi-spinner`) centered with descriptive text — matches the pattern used in other dialogs.
- **MembershipDialog — reactivate section**: Uses `Message severity="warn"` to explain why the membership is inactive, then a year picker and action button — consistent with the existing create-membership section below it.
- **CreateFeeDialog**: Compact (max-w-md), mirrors `PayFeeDialog` structure. Year input defaults to current year; amount defaults to 0 (admin fills in the real amount).
- **"Cargar cuota" button in fees table**: Shown only when no fee exists for the current year. Uses `outlined` style to not compete visually with the "Pagar" buttons in the table.

---

## Dependencies

No new npm packages required. All PrimeVue components used (`Dialog`, `InputNumber`, `Button`, `Message`, `Tag`, `DataTable`) are already installed.

---

## Notes

- **English only in code** — variable names, comments, console logs. Spanish is for UI labels only.
- **`<script setup lang="ts">` required** for all components.
- **No `any` types** — use typed API response types or explicit `unknown` with type guard.
- The `MemberMembershipData.membershipStatus` field is additive — existing usages that don't set it will get a TypeScript error and must be updated (only `ProfilePage.vue` identified).
- The `BulkMembershipDialog` self-loading creates parallel `getMembership` calls (one per member). For large families this is acceptable. If performance becomes a concern, a backend endpoint returning all members' membership status in one call would be preferable (out of scope for this ticket).

---

## Next Steps After Implementation

- Coordinate with backend: ensure `feature/fix-membership-enrollment-backend` is merged to `dev` before testing the frontend against a real API.
- Optionally: add a "Cargar cuota anual masiva" bulk-fee flow for families where all members need the current year fee created at once (separate ticket).

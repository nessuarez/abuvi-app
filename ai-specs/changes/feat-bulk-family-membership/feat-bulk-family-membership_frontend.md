# Frontend Implementation Plan: feat-bulk-family-membership

## Overview

This feature extends the membership UI with two related changes:

1. **`MembershipDialog.vue`**: Replace the `<Calendar>` date picker with a `<InputNumber>` year picker. The backend now accepts `{ year: number }` instead of `{ startDate: string }`.
2. **Bulk membership flow**: New `BulkMembershipDialog.vue` component + "Activar membresía familiar" button on `FamilyUnitPage.vue` and `ProfilePage.vue`, allowing a board user to activate memberships for all family members in a single action.

Architecture: all changes follow the established composable-based architecture — no direct API calls from components. A new `bulkActivateMemberships` method is added to the existing `useMemberships` composable. No new Pinia store is needed (no globally shared state).

---

## Architecture Context

**Files to modify:**

- `frontend/src/types/membership.ts` — change `CreateMembershipRequest`, add bulk types, export `MemberMembershipData`
- `frontend/src/composables/useMemberships.ts` — update `createMembership` (type only), add `bulkActivateMemberships`
- `frontend/src/components/memberships/MembershipDialog.vue` — replace Calendar with InputNumber
- `frontend/src/views/FamilyUnitPage.vue` — add button + wire `BulkMembershipDialog`
- `frontend/src/views/ProfilePage.vue` — add button + wire `BulkMembershipDialog`, import shared `MemberMembershipData`

**Files to create:**

- `frontend/src/components/memberships/BulkMembershipDialog.vue` — new bulk activation dialog

**Test files to update:**

- `frontend/src/composables/__tests__/useMemberships.test.ts` (currently does not exist — check; if missing, create it)
- `frontend/src/views/__tests__/FamilyUnitPage.spec.ts`
- `frontend/src/views/__tests__/ProfilePage.spec.ts`

**Test files to create:**

- `frontend/src/components/memberships/__tests__/MembershipDialog.spec.ts`
- `frontend/src/components/memberships/__tests__/BulkMembershipDialog.spec.ts`

**State management:** Local component `ref()` only — no Pinia store changes.

**Routing:** No new routes.

**PrimeVue components used:**

- `InputNumber` (replaces `Calendar` in `MembershipDialog.vue` and used in `BulkMembershipDialog.vue`)
- `Dialog`, `Button`, `Message`, `Tag` (already used)

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the frontend feature branch.
- **Branch name**: `feature/feat-bulk-family-membership-frontend`
- **Implementation Steps**:
  1. Ensure you are on `main`: `git checkout main && git pull origin main`
  2. Create branch: `git checkout -b feature/feat-bulk-family-membership-frontend`
  3. Verify: `git branch`
- **Notes**: Do NOT work on the general task branch. This is a separate branch from the backend branch (`feature/feat-bulk-family-membership-backend`). The frontend must depend on the backend being merged first (or both merged together in a coordinated release).

---

### Step 1: Update `membership.ts` — Types

- **File**: `frontend/src/types/membership.ts`
- **Action A**: Change `CreateMembershipRequest` from `startDate: string` to `year: number`.
- **Action B**: Add bulk types.
- **Action C**: Export `MemberMembershipData` (currently a local interface in `ProfilePage.vue` — must be shared to allow `BulkMembershipDialog` to use it).

**Action A — Change `CreateMembershipRequest`:**

```typescript
// BEFORE
export interface CreateMembershipRequest {
  startDate: string // ISO 8601 date string — must not be in the future
}

// AFTER
export interface CreateMembershipRequest {
  year: number // Calendar year — must not be in the future (≤ current year)
}
```

**Action B — Add bulk types (append to the file after `PayFeeRequest`):**

```typescript
export type BulkMembershipResultStatus = 'Activated' | 'Skipped' | 'Failed'

export interface BulkMembershipMemberResult {
  memberId: string
  memberName: string
  status: BulkMembershipResultStatus
  reason?: string | null
}

export interface BulkActivateMembershipResponse {
  activated: number
  skipped: number
  results: BulkMembershipMemberResult[]
}

export interface BulkActivateMembershipRequest {
  year: number
}
```

**Action C — Export `MemberMembershipData` (append to the file):**

Import `MembershipFeeResponse` is already in the file. Add:

```typescript
import type { FamilyMemberResponse } from '@/types/family-unit'

export interface MemberMembershipData {
  member: FamilyMemberResponse
  membershipId: string | null
  isActiveMembership: boolean
  currentFee: MembershipFeeResponse | null
  feeLoading: boolean
}
```

- **Implementation Notes**:
  - The `MemberMembershipData` interface is currently defined locally inside `ProfilePage.vue`. After this change, remove the local definition from `ProfilePage.vue` and import it from `@/types/membership`.
  - The `FamilyMemberResponse` import introduces a cross-type dependency between `membership.ts` and `family-unit.ts`. This is acceptable — they are related domain types.

---

### Step 2: Update `useMemberships.ts` — Add `bulkActivateMemberships`

- **File**: `frontend/src/composables/useMemberships.ts`
- **Action A**: Update imports (the `CreateMembershipRequest` type change is transparent — the composable signature stays the same).
- **Action B**: Add `bulkActivateMemberships` method.
- **Action C**: Export `bulkActivateMemberships` from the composable return.

**Action A — Update imports at the top:**

```typescript
import type {
  MembershipResponse,
  MembershipFeeResponse,
  CreateMembershipRequest,
  PayFeeRequest,
  BulkActivateMembershipRequest,
  BulkActivateMembershipResponse,
} from '@/types/membership'
```

**Action B — Add `bulkActivateMemberships` (after `deactivateMembership`, before `getFees`):**

```typescript
/**
 * Activate memberships for all family members without one (bulk operation).
 * Board/Admin only.
 */
const bulkActivateMemberships = async (
  familyUnitId: string,
  request: BulkActivateMembershipRequest,
): Promise<BulkActivateMembershipResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.post<ApiResponse<BulkActivateMembershipResponse>>(
      `/family-units/${familyUnitId}/membership/bulk`,
      request,
    )
    return response.data.data
  } catch (err: any) {
    error.value = err.response?.data?.error?.message || 'Error al activar las membresías'
    return null
  } finally {
    loading.value = false
  }
}
```

**Action C — Add `bulkActivateMemberships` to the return object:**

```typescript
return {
  membership,
  fees,
  loading,
  error,
  getMembership,
  createMembership,
  deactivateMembership,
  bulkActivateMemberships, // ← new
  getFees,
  payFee,
}
```

- **Implementation Notes**:
  - The `createMembership` method body does NOT change — it just `POST`s `request` as-is. Since `CreateMembershipRequest` now contains `{ year: number }`, the call `api.post(..., request)` will send the correct payload automatically.
  - The `bulkActivateMemberships` method returns `BulkActivateMembershipResponse | null`. It does not update any `ref` state (unlike `createMembership` which updates `membership.value`) — the caller handles the result directly.
  - The URL is `/family-units/${familyUnitId}/membership/bulk` (no `/members/{id}` segment).

---

### Step 3: Update `MembershipDialog.vue` — Replace Calendar with InputNumber

- **File**: `frontend/src/components/memberships/MembershipDialog.vue`
- **Action**: Replace the `<Calendar>` import and `createStartDate` ref with `InputNumber` and `createStartYear`. Update `handleCreate` to send `{ year }`.

**Script changes:**

Remove:

```typescript
import Calendar from 'primevue/calendar'
const createStartDate = ref<Date>(new Date())
```

Add:

```typescript
import InputNumber from 'primevue/inputnumber'
const currentYear = new Date().getFullYear()
const createStartYear = ref<number>(currentYear)
```

**Template changes — replace the `<Calendar>` block:**

```html
<!-- BEFORE -->
<div class="flex flex-col gap-2">
  <label for="membership-start-date" class="font-medium text-sm">
    Fecha de inicio <span class="text-red-500">*</span>
  </label>
  <Calendar
    id="membership-start-date"
    v-model="createStartDate"
    dateFormat="dd/mm/yy"
    :maxDate="new Date()"
    showIcon
    class="w-full"
  />
  <small class="text-gray-500">Debe ser la fecha actual o una fecha pasada.</small>
</div>

<!-- AFTER -->
<div class="flex flex-col gap-2">
  <label for="membership-start-year" class="font-medium text-sm">
    Año de inicio <span class="text-red-500">*</span>
  </label>
  <InputNumber
    id="membership-start-year"
    v-model="createStartYear"
    :min="2000"
    :max="currentYear"
    :use-grouping="false"
    class="w-full"
  />
  <small class="text-gray-500">Año en que el miembro se hizo socio. No puede ser futuro.</small>
</div>
```

**Update `handleCreate`:**

```typescript
// BEFORE
const handleCreate = async () => {
  const dateStr = createStartDate.value.toISOString().split('T')[0]
  const result = await createMembership(props.familyUnitId, props.memberId, { startDate: dateStr })
  ...
}

// AFTER
const handleCreate = async () => {
  const result = await createMembership(props.familyUnitId, props.memberId, { year: createStartYear.value })
  ...
}
```

- **Implementation Notes**:
  - `InputNumber` from `primevue/inputnumber` is already available in the project (it is part of the PrimeVue installation). No npm install needed.
  - `:use-grouping="false"` prevents the year from displaying as `2,025`. This is important for a year input.
  - `createStartYear` defaults to `currentYear` (the result of `new Date().getFullYear()` at component initialization). This is static for the lifetime of the dialog — acceptable since the year rarely changes during a session.
  - The `handleCreate` success path (toast and state) remains unchanged.

---

### Step 4: Create `BulkMembershipDialog.vue`

- **File**: `frontend/src/components/memberships/BulkMembershipDialog.vue` (new)
- **Action**: Create the new bulk membership activation dialog component.

**Full component:**

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'
import { useToast } from 'primevue/usetoast'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import { useMemberships } from '@/composables/useMemberships'
import type { BulkActivateMembershipResponse, MemberMembershipData } from '@/types/membership'
import type { FamilyMemberResponse } from '@/types/family-unit'

const props = defineProps<{
  visible: boolean
  familyUnitId: string
  members: FamilyMemberResponse[]
  memberData: MemberMembershipData[]
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  done: []
}>()

const toast = useToast()

const currentYear = new Date().getFullYear()
const selectedYear = ref<number>(currentYear)
const result = ref<BulkActivateMembershipResponse | null>(null)

const { loading, error, bulkActivateMemberships } = useMemberships()

// Members without an active membership (from memberData prop)
// When memberData is empty (FamilyUnitPage context), fall back to all members
const membersWithoutMembership = computed(() => {
  if (props.memberData.length === 0) return props.members
  return props.memberData.filter((d) => !d.membershipId)
})

const membersAlreadyActive = computed(() =>
  props.memberData.filter((d) => d.membershipId && d.isActiveMembership),
)

const handleBulkActivate = async () => {
  result.value = null
  const res = await bulkActivateMemberships(props.familyUnitId, { year: selectedYear.value })
  if (res) {
    result.value = res
    if (res.activated > 0) {
      toast.add({
        severity: 'success',
        summary: 'Éxito',
        detail: `${res.activated} membresía(s) activada(s)`,
        life: 3000,
      })
    }
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleClose = () => {
  if ((result.value?.activated ?? 0) > 0) {
    emit('done')
  }
  emit('update:visible', false)
  result.value = null
  selectedYear.value = currentYear
}
</script>

<template>
  <Dialog
    :visible="visible"
    header="Activar membresía familiar"
    :modal="true"
    :closable="true"
    :dismissable-mask="!loading"
    class="w-full max-w-xl"
    @update:visible="handleClose"
  >
    <!-- Summary of current state (before activation) -->
    <div v-if="!result" class="mb-4 space-y-1 text-sm text-gray-600">
      <p>
        <strong>{{ membersWithoutMembership.length }}</strong>
        miembro(s) sin membresía activa.
      </p>
      <p v-if="membersAlreadyActive.length > 0">
        <strong>{{ membersAlreadyActive.length }}</strong>
        miembro(s) ya con membresía activa (se omitirán).
      </p>
    </div>

    <!-- Empty state: all members already have membership -->
    <Message
      v-if="membersWithoutMembership.length === 0 && !result"
      severity="info"
      class="mb-4"
    >
      Todos los miembros de esta familia ya tienen una membresía activa.
    </Message>

    <!-- Year picker (only shown before activation and when there are members to activate) -->
    <div
      v-if="!result && membersWithoutMembership.length > 0"
      class="mb-6 flex flex-col gap-2"
    >
      <label for="bulk-start-year" class="text-sm font-medium">
        Año de inicio <span class="text-red-500">*</span>
      </label>
      <InputNumber
        id="bulk-start-year"
        v-model="selectedYear"
        :min="2000"
        :max="currentYear"
        :use-grouping="false"
        class="w-full"
        data-testid="bulk-year-input"
      />
      <small class="text-gray-500">
        Año en que estos miembros se hacen socios. Se aplicará a todos los que no tengan membresía.
      </small>
    </div>

    <!-- Result summary (after activation) -->
    <div v-if="result" class="mb-4 space-y-3">
      <Message :severity="result.activated > 0 ? 'success' : 'info'">
        {{ result.activated }} membresía(s) activada(s), {{ result.skipped }} omitida(s).
      </Message>
      <div class="space-y-2">
        <div
          v-for="r in result.results"
          :key="r.memberId"
          class="flex items-center justify-between rounded-lg border border-gray-100 px-3 py-2 text-sm"
        >
          <span>{{ r.memberName }}</span>
          <Tag
            :value="r.status === 'Activated' ? 'Activada' : r.status === 'Skipped' ? 'Omitida' : 'Error'"
            :severity="r.status === 'Activated' ? 'success' : r.status === 'Skipped' ? 'secondary' : 'danger'"
          />
        </div>
      </div>
    </div>

    <!-- Actions -->
    <div class="flex justify-end gap-2">
      <Button
        label="Cancelar"
        severity="secondary"
        :disabled="loading"
        data-testid="cancel-btn"
        @click="handleClose"
      />
      <Button
        v-if="!result && membersWithoutMembership.length > 0"
        :label="`Activar ${membersWithoutMembership.length} membresía(s)`"
        icon="pi pi-check"
        :loading="loading"
        data-testid="activate-btn"
        @click="handleBulkActivate"
      />
      <Button
        v-if="result"
        label="Cerrar"
        data-testid="close-btn"
        @click="handleClose"
      />
    </div>
  </Dialog>
</template>
```

- **Implementation Notes**:
  - `membersWithoutMembership` computed: when `memberData` is empty (i.e., called from `FamilyUnitPage` where membership status is not pre-loaded), it falls back to `props.members`. This means the count shown before activation will be `members.length` (all assumed to need activation), which is acceptable per the spec.
  - `handleClose` emits `done` only if `result.value.activated > 0`. The `result.value?.activated ?? 0` pattern handles the case where `result.value` is null (dialog closed without activating).
  - The `@update:visible` on `<Dialog>` calls `handleClose` instead of directly emitting `update:visible` — this ensures cleanup (`result.value = null`, `selectedYear.value = currentYear`) always happens.
  - No `<style>` block — all styling via Tailwind.
  - `data-testid` attributes on interactive elements for testability.

---

### Step 5: Update `ProfilePage.vue` — Import Shared Type + Add Bulk Dialog

- **File**: `frontend/src/views/ProfilePage.vue`
- **Action A**: Remove local `MemberMembershipData` interface definition; import from `@/types/membership`.
- **Action B**: Add `showBulkMembershipDialog` state.
- **Action C**: Import `BulkMembershipDialog`.
- **Action D**: Add "Activar membresía familiar" button in the family unit card header.
- **Action E**: Add `<BulkMembershipDialog>` component at the bottom.

**Action A — Remove local interface, update import:**

Remove from the script block:

```typescript
// DELETE this local interface:
interface MemberMembershipData {
  member: FamilyMemberResponse
  membershipId: string | null
  isActiveMembership: boolean
  currentFee: MembershipFeeResponse | null
  feeLoading: boolean
}
```

Update the membership type import line to include `MemberMembershipData`:

```typescript
import {
  FeeStatus,
  FeeStatusLabels,
  FeeStatusSeverity,
  type MembershipFeeResponse,
  type PayFeeRequest,
  type MemberMembershipData,  // ← add
} from '@/types/membership'
```

**Action B — Add state:**

```typescript
// After showMembershipDialog / selectedMemberForMembership:
const showBulkMembershipDialog = ref(false)
```

**Action C — Add import:**

```typescript
import BulkMembershipDialog from '@/components/memberships/BulkMembershipDialog.vue'
```

**Action D — Template: add button in family unit card `#title` slot:**

The existing `#title` slot of the family unit `Card` in `ProfilePage`:

```html
<template #title>
  <div class="flex items-center justify-between gap-2">
    <div class="flex items-center gap-2">
      <i class="pi pi-users" aria-hidden="true" />
      <span>Mi Unidad Familiar</span>
    </div>
    <Button
      v-if="familyUnit"
      label="Gestionar"
      icon="pi pi-arrow-right"
      icon-pos="right"
      outlined
      size="small"
      data-testid="manage-family-unit-btn"
      @click="goToFamilyManagement"
    />
  </div>
</template>
```

Replace with:

```html
<template #title>
  <div class="flex items-center justify-between gap-2">
    <div class="flex items-center gap-2">
      <i class="pi pi-users" aria-hidden="true" />
      <span>Mi Unidad Familiar</span>
    </div>
    <div class="flex gap-2">
      <Button
        v-if="auth.isBoard && familyUnit"
        label="Activar membresía familiar"
        icon="pi pi-users"
        severity="secondary"
        outlined
        size="small"
        data-testid="bulk-membership-btn"
        @click="showBulkMembershipDialog = true"
      />
      <Button
        v-if="familyUnit"
        label="Gestionar"
        icon="pi pi-arrow-right"
        icon-pos="right"
        outlined
        size="small"
        data-testid="manage-family-unit-btn"
        @click="goToFamilyManagement"
      />
    </div>
  </div>
</template>
```

**Action E — Template: add `BulkMembershipDialog` at the bottom (after `<MembershipDialog>`):**

```html
<BulkMembershipDialog
  v-if="showBulkMembershipDialog"
  v-model:visible="showBulkMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :members="familyMembers"
  :member-data="memberData"
  @done="loadMemberMembershipData"
/>
```

- **Implementation Notes**:
  - In `ProfilePage`, `memberData` is already populated (each member's membership status is loaded via `loadMemberMembershipData()`), so the `BulkMembershipDialog` can show accurate pre-activation counts.
  - After `@done`, `loadMemberMembershipData()` is called to refresh the membership badges on the profile page.
  - The `v-if="showBulkMembershipDialog"` pattern (not `v-show`) is used for memory efficiency — consistent with how `MembershipDialog` is handled in this page.

---

### Step 6: Update `FamilyUnitPage.vue` — Add Bulk Dialog

- **File**: `frontend/src/views/FamilyUnitPage.vue`
- **Action A**: Import `BulkMembershipDialog`.
- **Action B**: Add `showBulkMembershipDialog` state.
- **Action C**: Add button in the members `Card` `#title` slot.
- **Action D**: Add `<BulkMembershipDialog>` component.

**Action A — Add import (after `MembershipDialog` import):**

```typescript
import BulkMembershipDialog from '@/components/memberships/BulkMembershipDialog.vue'
```

**Action B — Add state (after `showMembershipDialog`):**

```typescript
const showBulkMembershipDialog = ref(false)
```

**Action C — Template: update the members `Card` `#title` slot:**

Current template:

```html
<template #title>
  <div class="flex justify-between items-center">
    <span>Miembros Familiares</span>
    <Button
      icon="pi pi-plus"
      label="Añadir Miembro"
      @click="openCreateMemberDialog"
    />
  </div>
</template>
```

Replace with:

```html
<template #title>
  <div class="flex justify-between items-center">
    <span>Miembros Familiares</span>
    <div class="flex gap-2">
      <Button
        v-if="auth.isBoard"
        icon="pi pi-users"
        label="Activar membresía familiar"
        severity="secondary"
        outlined
        size="small"
        data-testid="bulk-membership-btn"
        @click="showBulkMembershipDialog = true"
      />
      <Button
        icon="pi pi-plus"
        label="Añadir Miembro"
        @click="openCreateMemberDialog"
      />
    </div>
  </div>
</template>
```

**Action D — Template: add `BulkMembershipDialog` at the bottom (after `<MembershipDialog>`):**

```html
<BulkMembershipDialog
  v-if="showBulkMembershipDialog"
  v-model:visible="showBulkMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :members="familyMembers"
  :member-data="[]"
  @done="familyUnit && getFamilyMembers(familyUnit.id)"
/>
```

- **Implementation Notes**:
  - `:member-data="[]"` because `FamilyUnitPage` does not load membership status. The `BulkMembershipDialog` handles this gracefully: it falls back to showing `members.length` as the count of members to activate.
  - `@done` reloads family members list (not membership data — `FamilyUnitPage` doesn't track that). This is consistent with the page's existing data flow.
  - The `auth.isBoard` check on the button is already available since `const auth = useAuthStore()` is in the script.

---

### Step 7: Write Unit Tests

#### 7a. `useMemberships` composable tests

- **File**: `frontend/src/composables/__tests__/useMemberships.test.ts` (create if does not exist)
- **Action**: Test `bulkActivateMemberships` method. Update any existing tests that call `createMembership` with `{ startDate }` to use `{ year }`.

**Test structure:**

```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useMemberships } from '../useMemberships'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}))

describe('useMemberships', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('bulkActivateMemberships', () => {
    it('should call the correct endpoint and return the response', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: { activated: 2, skipped: 1, results: [] },
        },
      }
      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const { bulkActivateMemberships } = useMemberships()
      const result = await bulkActivateMemberships('family-unit-1', { year: 2025 })

      expect(result).toEqual(mockResponse.data.data)
      expect(api.post).toHaveBeenCalledWith(
        '/family-units/family-unit-1/membership/bulk',
        { year: 2025 },
      )
    })

    it('should set error and return null on API failure', async () => {
      vi.mocked(api.post).mockRejectedValueOnce({
        response: { data: { error: { message: 'Error al activar las membresías' } } },
      })

      const { bulkActivateMemberships, error } = useMemberships()
      const result = await bulkActivateMemberships('family-unit-1', { year: 2025 })

      expect(result).toBeNull()
      expect(error.value).toBe('Error al activar las membresías')
    })

    it('should set loading to false after completion', async () => {
      vi.mocked(api.post).mockResolvedValueOnce({
        data: { success: true, data: { activated: 0, skipped: 0, results: [] } },
      })

      const { bulkActivateMemberships, loading } = useMemberships()
      const promise = bulkActivateMemberships('family-unit-1', { year: 2025 })
      // loading starts true
      await promise
      expect(loading.value).toBe(false)
    })
  })

  describe('createMembership', () => {
    it('should send { year } payload to the correct endpoint', async () => {
      vi.mocked(api.post).mockResolvedValueOnce({
        data: {
          success: true,
          data: { id: 'm1', familyMemberId: 'fm1', startDate: '2025-01-01T00:00:00Z', isActive: true, fees: [], createdAt: '2025-01-01', updatedAt: '2025-01-01' },
        },
      })

      const { createMembership } = useMemberships()
      await createMembership('fu1', 'fm1', { year: 2025 })

      expect(api.post).toHaveBeenCalledWith(
        '/family-units/fu1/members/fm1/membership',
        { year: 2025 },
      )
    })
  })
})
```

#### 7b. `MembershipDialog.vue` component tests

- **File**: `frontend/src/components/memberships/__tests__/MembershipDialog.spec.ts` (create)
- **Action**: Test that the component renders `InputNumber` (not `Calendar`) and calls `createMembership` with `{ year }`.

**Test cases:**

- `renders InputNumber year picker when member has no membership`
- `defaults InputNumber to current year`
- `does not allow year > currentYear (max prop is set)`
- `calls createMembership with { year: selectedYear } when Activar button clicked`

**Pattern** (follow `ProfilePage.spec.ts` style — vi.mock composables):

```typescript
vi.mock('@/composables/useMemberships', () => ({
  useMemberships: () => ({
    membership: { value: null },
    fees: { value: [] },
    loading: { value: false },
    error: { value: null },
    getMembership: vi.fn().mockResolvedValue(null),
    createMembership: vi.fn().mockResolvedValue({ id: 'm1' }),
    deactivateMembership: vi.fn(),
    payFee: vi.fn(),
  }),
}))
```

Key assertion for `InputNumber`:

```typescript
const inputNumber = wrapper.findComponent({ name: 'InputNumber' })
expect(inputNumber.exists()).toBe(true)
expect(inputNumber.props('max')).toBe(new Date().getFullYear())
expect(inputNumber.props('useGrouping')).toBe(false)
```

Key assertion for `createMembership` call:

```typescript
await wrapper.find('[data-testid="activate-btn"]').trigger('click')  // or find the button by label
expect(createMembershipMock).toHaveBeenCalledWith('fu1', 'member1', { year: currentYear })
```

#### 7c. `BulkMembershipDialog.vue` component tests

- **File**: `frontend/src/components/memberships/__tests__/BulkMembershipDialog.spec.ts` (create)
- **Action**: Test the new bulk dialog component.

**Test cases:**

```
shows correct count of members without membership (from memberData)
shows all members as needing activation when memberData is empty (FamilyUnitPage context)
shows "all members have membership" message when all are active
calls bulkActivateMemberships with correct familyUnitId and year when button clicked
shows result summary after activation
emits done when dialog is closed after successful activation (activated > 0)
does not emit done when closed with 0 activations
emits update:visible when cancel is clicked
```

**Example test:**

```typescript
it('emits done when closed after successful activation', async () => {
  // Arrange: mock bulkActivateMemberships to return { activated: 2, skipped: 0, results: [] }
  // Act: click activate button, then close
  // Assert: wrapper.emitted('done') is truthy
})

it('does not emit done when closed with 0 activations', async () => {
  // Arrange: mock returns { activated: 0, skipped: 2, results: [] }
  // Act: click activate, then close
  // Assert: wrapper.emitted('done') is falsy / undefined
})
```

---

### Step 8: Update View Tests

#### 8a. `ProfilePage.spec.ts`

- **File**: `frontend/src/views/__tests__/ProfilePage.spec.ts`
- **Action**:
  1. Update the `useMemberships` mock to include `bulkActivateMemberships: vi.fn()`.
  2. Add `BulkMembershipDialog` to `componentStubs`.
  3. Add test cases for the "Activar membresía familiar" button visibility.

**New test cases:**

- `shows bulk-membership-btn for board users when familyUnit exists`
- `does not show bulk-membership-btn for non-board users`
- `does not show bulk-membership-btn when familyUnit is null`

**Update mock:**

```typescript
vi.mock('@/composables/useMemberships', () => ({
  useMemberships: () => ({
    getMembership: vi.fn().mockResolvedValue(null),
    payFee: vi.fn(),
    bulkActivateMemberships: vi.fn(), // ← add
  }),
}))
```

**Update stubs:**

```typescript
const componentStubs = {
  // ... existing stubs ...
  BulkMembershipDialog: { name: 'BulkMembershipDialog', template: '<div />', props: ['visible', 'familyUnitId', 'members', 'memberData'] },
}
```

#### 8b. `FamilyUnitPage.spec.ts`

- **File**: `frontend/src/views/__tests__/FamilyUnitPage.spec.ts`
- **Action**: Add `BulkMembershipDialog` stub and test cases for the button visibility.

**New test cases:**

- `shows bulk-membership-btn for board users`
- `does not show bulk-membership-btn for non-board users`

---

### Step 9: Update Technical Documentation

- **Action**: Update frontend standards and API endpoint documentation.

**`ai-specs/specs/frontend-standards.mdc`**:

- Under "PrimeVue component usage", note that `InputNumber` is used for year pickers (`:use-grouping="false"`, `:min`, `:max`) — replacing `Calendar` for semantically annual inputs.

**`ai-specs/specs/api-endpoints.md`**:

- Already updated by the backend ticket, but verify the frontend-facing endpoint list matches:
  - `POST /api/family-units/{id}/members/{id}/membership` — request body: `{ year: number }`.
  - `POST /api/family-units/{id}/membership/bulk` — request body: `{ year: number }`, response: `{ activated, skipped, results[] }`.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-bulk-family-membership-frontend`
2. **Step 1** — Update `membership.ts` (change `CreateMembershipRequest`, add bulk types, export `MemberMembershipData`)
3. **Step 2** — Update `useMemberships.ts` (add `bulkActivateMemberships`)
4. **Step 3** — Update `MembershipDialog.vue` (Calendar → InputNumber)
5. **Step 4** — Create `BulkMembershipDialog.vue`
6. **Step 5** — Update `ProfilePage.vue` (remove local type, add button + dialog)
7. **Step 6** — Update `FamilyUnitPage.vue` (add button + dialog)
8. **Step 7** — Write unit/component tests
9. **Step 8** — Update view tests
10. **Step 9** — Update technical documentation

---

## Testing Checklist

### Composable Tests (`useMemberships.test.ts`)

| Test | Expected |
|---|---|
| `bulkActivateMemberships` calls correct URL `/family-units/{id}/membership/bulk` | Pass |
| `bulkActivateMemberships` sends `{ year }` payload | Pass |
| `bulkActivateMemberships` returns null + sets `error` on failure | Pass |
| `bulkActivateMemberships` resets `loading` after completion | Pass |
| `createMembership` sends `{ year }` (not `{ startDate }`) | Pass |

### Component Tests (`MembershipDialog.spec.ts`)

| Test | Expected |
|---|---|
| Renders `InputNumber` (not `Calendar`) when no membership | Pass |
| `InputNumber` `:max` equals current year | Pass |
| `InputNumber` `:use-grouping="false"` | Pass |
| `handleCreate` calls `createMembership` with `{ year: selectedYear }` | Pass |

### Component Tests (`BulkMembershipDialog.spec.ts`)

| Test | Expected |
|---|---|
| Shows correct count from `memberData` | Pass |
| Falls back to `members.length` when `memberData` is empty | Pass |
| Shows info message when all members already active | Pass |
| Calls `bulkActivateMemberships` with correct args | Pass |
| Shows result summary after activation | Pass |
| Emits `done` when closed after `activated > 0` | Pass |
| Does NOT emit `done` when `activated === 0` | Pass |

### View Tests

| Test | Expected |
|---|---|
| `ProfilePage`: `bulk-membership-btn` visible for board user | Pass |
| `ProfilePage`: `bulk-membership-btn` hidden for non-board user | Pass |
| `FamilyUnitPage`: `bulk-membership-btn` visible for board user | Pass |
| `FamilyUnitPage`: `bulk-membership-btn` hidden for non-board user | Pass |

---

## Error Handling Patterns

| Scenario | Handling |
|---|---|
| `bulkActivateMemberships` API error | `error.value` set; toast shown with `severity: 'error'`; `result.value` stays null |
| `createMembership` API error | Existing handling unchanged (toast shown in `MembershipDialog.handleCreate`) |
| Dialog closed mid-operation (`loading = true`) | `:dismissable-mask="!loading"` prevents accidental close |
| `familyUnitId` empty string (no family unit) | Button has `v-if="familyUnit"` guard — dialog never opens without a valid ID |

---

## UI/UX Considerations

- **`InputNumber` for year**: `:use-grouping="false"` prevents thousands separator. `:min="2000"` and `:max="currentYear"` enforce the business rule client-side (backend also validates). Defaults to `currentYear`.
- **`BulkMembershipDialog` two phases**: Phase 1 shows member counts + year picker; Phase 2 (after activation) shows per-member result rows with colored `Tag` badges.
- **"Activar membresía familiar" button**: `severity="secondary"` + `outlined` + `size="small"` — visually secondary to the primary action ("Añadir Miembro" / "Gestionar") to avoid accidental clicks.
- **Board-only visibility**: Both buttons are wrapped in `v-if="auth.isBoard"`. `auth.isBoard` is already a computed in the auth store (`role === 'Admin' || role === 'Board'`).
- **`@done` reload**: In `ProfilePage`, `@done="loadMemberMembershipData"` re-fetches all membership statuses in parallel, updating the per-member badges immediately.
- **Responsive**: `class="w-full max-w-xl"` on the dialog — full width on mobile, capped at `xl` on desktop. Member result rows use `flex items-center justify-between`.

---

## Dependencies

- **No new npm packages** — `InputNumber` is already part of the PrimeVue installation.
- **PrimeVue components used** (all already in the project):
  - `InputNumber` (`primevue/inputnumber`) — new usage in `MembershipDialog` and `BulkMembershipDialog`
  - `Dialog`, `Button`, `Message`, `Tag` — already used

---

## Notes

1. **Type export**: `MemberMembershipData` moves from being a local `ProfilePage.vue` interface to an exported type in `membership.ts`. This is the only way to share it with `BulkMembershipDialog`. Make sure the `import` in `ProfilePage.vue` is updated and the local `interface` definition is removed to avoid duplication.

2. **Backend coordination**: This frontend ticket depends on the backend (`feat-bulk-family-membership-backend`) being deployed first, or deployed simultaneously. The `createMembership` call will break with the old backend (which expects `{ startDate }`) as soon as the frontend sends `{ year }`.

3. **`InputNumber` v-model type**: `v-model="createStartYear"` where `createStartYear` is `ref<number>(currentYear)`. PrimeVue `InputNumber` will correctly bind to a `number` ref. Do not use `ref<number | null>()` — always initialize with a concrete integer to avoid null edge cases.

4. **`BulkMembershipDialog` `memberData` fallback**: When `memberData` is `[]` (from `FamilyUnitPage`), the computed `membersWithoutMembership` falls back to `props.members`. This means the dialog may show "4 members without membership" even if some already have one — the discrepancy is corrected post-activation by the `results[]` in the response. This is documented behavior per the spec.

5. **No `<style>` blocks**: All styling via Tailwind. `BulkMembershipDialog.vue` must not include a `<style>` block.

6. **`data-testid` attributes**: Add `data-testid` to the "Activar membresía familiar" button in both `FamilyUnitPage` and `ProfilePage` (use `data-testid="bulk-membership-btn"`), and to interactive elements in `BulkMembershipDialog` for testability.

7. **Language**: All UI text in Spanish. Code, types, and documentation in English.

---

## Next Steps After Implementation

- Coordinate deployment with the backend ticket — both must go live together.
- After merge, verify Cypress E2E smoke test covering the board user flow: open profile → click "Activar membresía familiar" → select year → click activate → verify toast + result rows.
- If Cypress E2E tests exist for the membership flow, update them to use `{ year }` instead of `{ startDate }`.

---

## Implementation Verification

- [ ] **TypeScript**: No `any` types, `CreateMembershipRequest` has `year: number` everywhere, `MemberMembershipData` imported from `@/types/membership` in `ProfilePage`
- [ ] **No Calendar import**: `MembershipDialog.vue` no longer imports or uses `Calendar`
- [ ] **No `<style>` blocks**: `BulkMembershipDialog.vue` uses only Tailwind classes
- [ ] **Composable pattern**: `BulkMembershipDialog` calls API via `useMemberships()` — no direct `api.post()` calls in the component
- [ ] **Board-only guards**: `v-if="auth.isBoard"` on both "Activar membresía familiar" buttons
- [ ] **`@done` emit wired**: Both pages handle `@done` by reloading relevant data
- [ ] **Tests pass**: `vitest run` passes for all new and updated test files
- [ ] **Build passes**: `vite build` (or `npm run build`) passes without TypeScript errors
- [ ] **Documentation updated**: `frontend-standards.mdc` and `api-endpoints.md` reflect changes

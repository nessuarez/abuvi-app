# Frontend Implementation Plan: feat-camp-editions-management — Camp Editions Management (Phase 4)

**Source spec:** [camp-editions-management_enriched.md](./camp-editions-management_enriched.md)
**Branch:** `feature/feat-camp-editions-management-frontend`
**Date:** 2026-02-17

---

## 1. Overview

This plan covers the frontend implementation for Phase 4 of Camp Editions Management. The backend exposes 5 new endpoints for status lifecycle management, CRUD operations, and the active edition query.

The frontend delivers two distinct user experiences:

- **Board+ users**: A full edition management page (`/camps/editions`) with listing, filtering, status transitions, and edit functionality.
- **Member users**: The existing `/camp` page is populated with live active edition data fetched from `GET /api/camps/editions/active`.

Architecture principles: Vue 3 Composition API (`<script setup lang="ts">`), composable-based API communication (`useCampEditions`), PrimeVue components, Tailwind CSS utility classes, and TypeScript strict typing.

---

## 2. Architecture Context

### Files to modify

| File | Change |
|------|--------|
| `frontend/src/types/camp-edition.ts` | Add `UpdateCampEditionRequest`, `ChangeEditionStatusRequest`, `ActiveCampEditionResponse`; align field names with backend |
| `frontend/src/composables/useCampEditions.ts` | Fix `changeStatus` (PATCH + `status`), fix `updateEdition` type, fix `getActiveEdition` return type, add `fetchAllEditions` |
| `frontend/src/views/CampPage.vue` | Implement active edition display for Member+ |
| `frontend/src/router/index.ts` | Add `/camps/editions` route (Board+), `/camps/editions/:id` route (Member+) |

### Files to create

| File | Purpose |
|------|---------|
| `frontend/src/components/camps/CampEditionStatusBadge.vue` | Reusable status badge with color coding |
| `frontend/src/components/camps/ActiveEditionCard.vue` | Card component for active edition (Member view) |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | Edit dialog for updating an edition (Board+) |
| `frontend/src/components/camps/CampEditionStatusDialog.vue` | Confirmation dialog for status transitions (Board+) |
| `frontend/src/views/camps/CampEditionsPage.vue` | Board-only management page |
| `frontend/src/composables/__tests__/useCampEditions.test.ts` | Unit tests for composable |
| `frontend/cypress/e2e/camps/camp-editions.cy.ts` | E2E tests for both user flows |

### State management approach

- No new Pinia store needed — composable-local state is sufficient.
- `useCampEditions()` composable already holds `editions` and `activeEdition` refs; extend it with `allEditions` ref.
- Each view instantiates its own composable instance (local scoping is appropriate here).

### Routing

- `/camps/editions` → `CampEditionsPage.vue` (Board+, uses `requiresBoard: true`)
- `/camps/editions/:id` → `CampEditionDetailPage.vue` (Member+, uses `requiresAuth: true`)
  - **Decision**: For Phase 4, `GET /api/camps/editions/{id}` only returns the same `CampEditionResponse` fields visible in the table. Implement as a simple read-only detail page; a separate edit experience is handled via the dialog in the list page.

---

## 3. Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to a new feature branch.
**Branch name**: `feature/feat-camp-editions-management-frontend`
**Implementation Steps**:

1. Ensure you are on `main`: `git checkout main && git pull origin main`
2. Create branch: `git checkout -b feature/feat-camp-editions-management-frontend`
3. Verify: `git branch`

> **Note**: The backend branch `feature/feat-camp-editions-management-backend` may already be merged or in-progress. Create from `main` unless the backend endpoints are not yet deployed — in that case, base from the backend branch and rebase on `main` when ready.

---

### Step 1: Update TypeScript Types in `camp-edition.ts`

**File**: `frontend/src/types/camp-edition.ts`
**Action**: Add new DTOs and align field names with backend JSON serialization.

#### 1a. Field naming audit

The backend `CampEditionResponse` entity uses `CustomBabyMaxAge`, `CustomChildMinAge`, `CustomChildMaxAge`, `CustomAdultMinAge`. After camelCase serialization these become `customBabyMaxAge`, `customChildMinAge`, `customChildMaxAge`, `customAdultMinAge`.

The **current** frontend type uses `babyMaxAge`, `childMinAge`, `childMaxAge`, `adultMinAge` (without `custom` prefix) — **this is a mismatch** that must be confirmed against the actual backend response before renaming. During implementation, add a `console.log` on the API response to verify exact field names, then align accordingly.

#### 1b. Add `UpdateCampEditionRequest`

```typescript
export interface UpdateCampEditionRequest {
  startDate: string        // ISO 8601 format
  endDate: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge?: number
  customChildMinAge?: number
  customChildMaxAge?: number
  customAdultMinAge?: number
  maxCapacity?: number
  notes?: string
}
```

#### 1c. Add `ChangeEditionStatusRequest`

```typescript
export interface ChangeEditionStatusRequest {
  status: CampEditionStatus
}
```

#### 1d. Add `ActiveCampEditionResponse`

```typescript
export interface ActiveCampEditionResponse {
  id: string
  campId: string
  campName: string
  campLocation: string | null
  campFormattedAddress: string | null
  year: number
  startDate: string
  endDate: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge?: number
  customChildMinAge?: number
  customChildMaxAge?: number
  customAdultMinAge?: number
  status: CampEditionStatus
  maxCapacity?: number
  registrationCount: number      // Always 0 until registrations feature ships
  notes?: string
  createdAt: string
  updatedAt: string
}
```

#### 1e. Add `CampEditionFilters` helper type

```typescript
export interface CampEditionFilters {
  year?: number
  status?: CampEditionStatus
  campId?: string
}
```

**Notes**:

- Do NOT remove the existing `CampEdition` interface — it is still used by Phase 3 code.
- `UpdateCampEditionRequest` maps to the backend's existing `UpdateCampEditionRequest` DTO.

---

### Step 2: Update `useCampEditions` Composable

**File**: `frontend/src/composables/useCampEditions.ts`
**Action**: Fix method signatures/HTTP methods and add `fetchAllEditions`.

#### 2a. Fix `changeStatus` — HTTP method and payload

The current code uses `api.post(...)` with `{ newStatus }`. The backend Phase 4 endpoint is `PATCH /api/camps/editions/{id}/status` with `{ status: CampEditionStatus }`.

Change:

```typescript
// BEFORE (wrong HTTP verb, wrong key)
const response = await api.post<ApiResponse<CampEdition>>(
  `/camps/editions/${id}/status`, { newStatus }
)

// AFTER
const response = await api.patch<ApiResponse<CampEdition>>(
  `/camps/editions/${id}/status`, { status: newStatus } satisfies ChangeEditionStatusRequest
)
```

Also update the function signature to import and use `ChangeEditionStatusRequest`.

#### 2b. Fix `updateEdition` type signature

Change the parameter type from `Partial<CreateCampEditionRequest>` to `UpdateCampEditionRequest`:

```typescript
const updateEdition = async (
  id: string,
  request: UpdateCampEditionRequest
): Promise<CampEdition | null> => {
  // ...uses api.put (already correct)
}
```

#### 2c. Fix `getActiveEdition` return type

The backend returns `ActiveCampEditionResponse`, not `CampEdition`. Update:

```typescript
const activeEdition = ref<ActiveCampEditionResponse | null>(null)

const getActiveEdition = async (year?: number): Promise<void> => {
  loading.value = true
  error.value = null
  try {
    const url = year
      ? `/camps/editions/active?year=${year}`
      : '/camps/editions/active'
    const response = await api.get<ApiResponse<ActiveCampEditionResponse>>(url)
    activeEdition.value = response.data.success ? (response.data.data ?? null) : null
  } catch (err: unknown) {
    error.value = (err as { response?: { data?: { error?: { message?: string } } } })
      ?.response?.data?.error?.message || 'Error al cargar edición activa'
    activeEdition.value = null
  } finally {
    loading.value = false
  }
}
```

#### 2d. Add `fetchAllEditions` (Board+ only)

Add a new `allEditions` ref and method:

```typescript
const allEditions = ref<CampEdition[]>([])

const fetchAllEditions = async (filters?: CampEditionFilters): Promise<void> => {
  loading.value = true
  error.value = null
  try {
    const params = new URLSearchParams()
    if (filters?.year) params.append('year', String(filters.year))
    if (filters?.status) params.append('status', filters.status)
    if (filters?.campId) params.append('campId', filters.campId)
    const query = params.toString() ? `?${params.toString()}` : ''
    const response = await api.get<ApiResponse<CampEdition[]>>(`/camps/editions${query}`)
    allEditions.value = response.data.success ? (response.data.data ?? []) : []
  } catch (err: unknown) {
    error.value = (err as { response?: { data?: { error?: { message?: string } } } })
      ?.response?.data?.error?.message || 'Error al cargar ediciones'
    allEditions.value = []
  } finally {
    loading.value = false
  }
}
```

#### 2e. Return new members

Add `allEditions` and `fetchAllEditions` to the return object.

**Implementation Notes**:

- Import `UpdateCampEditionRequest`, `ChangeEditionStatusRequest`, `ActiveCampEditionResponse`, `CampEditionFilters` from `@/types/camp-edition`.
- The existing `getEditionById` returns `CampEdition | null` and also sets nothing in shared state — this is acceptable for Phase 4 (detail page calls it and stores locally).

---

### Step 3: Create `CampEditionStatusBadge.vue`

**File**: `frontend/src/components/camps/CampEditionStatusBadge.vue`
**Action**: Reusable badge component that maps status to color and Spanish label.

**Component signature**:

```typescript
defineProps<{
  status: CampEditionStatus
  size?: 'sm' | 'md'  // default 'md'
}>()
```

**Status → color mapping**:

| Status | Tailwind classes | Spanish label |
|--------|-----------------|---------------|
| `Proposed` | `bg-purple-100 text-purple-800` | Propuesta |
| `Draft` | `bg-gray-100 text-gray-700` | Borrador |
| `Open` | `bg-green-100 text-green-800` | Abierto |
| `Closed` | `bg-orange-100 text-orange-800` | Cerrado |
| `Completed` | `bg-blue-100 text-blue-800` | Completado |

**Template**:

```vue
<template>
  <span
    class="inline-flex items-center rounded-full font-medium"
    :class="[badgeClasses, size === 'sm' ? 'px-2 py-0.5 text-xs' : 'px-3 py-1 text-sm']"
  >
    {{ statusLabel }}
  </span>
</template>
```

---

### Step 4: Create `CampEditionStatusDialog.vue`

**File**: `frontend/src/components/camps/CampEditionStatusDialog.vue`
**Action**: Dialog for confirming a status transition from the current status to the next valid one.

**Component signature**:

```typescript
interface Props {
  visible: boolean
  edition: CampEdition
}

defineProps<Props>()
defineEmits<{
  'update:visible': [value: boolean]
  'confirm': [newStatus: CampEditionStatus]
}>()
```

**Logic**:

- Compute the `nextStatus` from the current one using the same transition map as the backend:

  ```typescript
  const validNextStatus: Partial<Record<CampEditionStatus, CampEditionStatus>> = {
    Proposed: 'Draft',
    Draft: 'Open',
    Open: 'Closed',
    Closed: 'Completed'
  }
  ```

- If `props.edition.status === 'Completed'`, the dialog should not be openable (guard at parent).
- Show the current status → next status with arrows, a warning about date constraints (Open requires future start date; Completed requires past end date), and Confirm / Cancel buttons.
- Uses PrimeVue `Dialog` with `modal`.

**Implementation Notes**:

- The parent passes `edition` and handles calling `changeStatus` on confirm.
- Display a loading spinner while the API call is in progress (parent controls `loading`).

---

### Step 5: Create `CampEditionUpdateDialog.vue`

**File**: `frontend/src/components/camps/CampEditionUpdateDialog.vue`
**Action**: Modal form dialog for updating an edition. Enforces status-based field restrictions.

**Component signature**:

```typescript
interface Props {
  visible: boolean
  edition: CampEdition
}

defineProps<Props>()
defineEmits<{
  'update:visible': [value: boolean]
  'saved': [edition: CampEdition]
}>()
```

**Form fields**:

- `startDate` — PrimeVue `DatePicker` (disabled when status is `Open`)
- `endDate` — PrimeVue `DatePicker` (disabled when status is `Open`)
- `pricePerAdult`, `pricePerChild`, `pricePerBaby` — PrimeVue `InputNumber` with `mode="currency"` `currency="EUR"` (disabled when status is `Open`)
- `maxCapacity` — PrimeVue `InputNumber` (nullable)
- `notes` — PrimeVue `Textarea`
- `useCustomAgeRanges` — PrimeVue `ToggleSwitch`
- Custom age fields — shown only when `useCustomAgeRanges` is true (4 `InputNumber` fields)

**Client-side validation**:

```typescript
const validate = (): boolean => {
  errors.value = {}
  if (!form.startDate) errors.value.startDate = 'La fecha de inicio es obligatoria'
  if (!form.endDate) errors.value.endDate = 'La fecha de fin es obligatoria'
  if (form.endDate && form.startDate && form.endDate <= form.startDate)
    errors.value.endDate = 'La fecha de fin debe ser posterior a la fecha de inicio'
  if (form.pricePerAdult < 0) errors.value.pricePerAdult = 'El precio por adulto debe ser mayor o igual a 0'
  if (form.pricePerChild < 0) errors.value.pricePerChild = 'El precio por niño debe ser mayor o igual a 0'
  if (form.pricePerBaby < 0) errors.value.pricePerBaby = 'El precio por bebé debe ser mayor o igual a 0'
  if (form.maxCapacity !== null && form.maxCapacity !== undefined && form.maxCapacity <= 0)
    errors.value.maxCapacity = 'La capacidad máxima debe ser mayor a 0'
  if (form.notes && form.notes.length > 2000)
    errors.value.notes = 'Las notas no deben superar los 2000 caracteres'
  if (form.useCustomAgeRanges) {
    if (!form.customBabyMaxAge) errors.value.customBabyMaxAge = 'La edad máxima de bebé es obligatoria'
    if (!form.customChildMinAge) errors.value.customChildMinAge = 'La edad mínima de niño es obligatoria'
    if (!form.customChildMaxAge) errors.value.customChildMaxAge = 'La edad máxima de niño es obligatoria'
    if (!form.customAdultMinAge) errors.value.customAdultMinAge = 'La edad mínima de adulto es obligatoria'
    if (form.customBabyMaxAge && form.customChildMinAge && form.customBabyMaxAge >= form.customChildMinAge)
      errors.value.customBabyMaxAge = 'La edad máxima de bebé debe ser menor a la edad mínima de niño'
    if (form.customChildMaxAge && form.customAdultMinAge && form.customChildMaxAge >= form.customAdultMinAge)
      errors.value.customChildMaxAge = 'La edad máxima de niño debe ser menor a la edad mínima de adulto'
  }
  return Object.keys(errors.value).length === 0
}
```

**Implementation Notes**:

- Initialize form from `props.edition` using a watcher on `visible` to reset.
- `Open` edition: disable date/price fields and show an informational `Message` explaining why.
- `Closed`/`Completed`: the parent should not open this dialog for those statuses (guard at parent).
- Emit `saved` with the returned `CampEdition` on success; parent handles toast and list refresh.
- Uses `api.put` through `updateEdition` from `useCampEditions`.

---

### Step 6: Create `ActiveEditionCard.vue`

**File**: `frontend/src/components/camps/ActiveEditionCard.vue`
**Action**: Card displaying active edition details for Member+ users.

**Component signature**:

```typescript
defineProps<{
  edition: ActiveCampEditionResponse
}>()
```

**Display**:

- Camp name and location
- Year, start date → end date (formatted in Spanish: `dd/mm/yyyy`)
- Status badge (always `Open`)
- Pricing table: price per adult, child, baby
- Max capacity (if set) and registration count placeholder
- Notes (if present)

**Notes**:

- No actions for members — read-only card.
- Uses Tailwind grid for the pricing breakdown.
- Date formatting: use `Intl.DateTimeFormat('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' })`.

---

### Step 7: Update `CampPage.vue`

**File**: `frontend/src/views/CampPage.vue`
**Action**: Implement active edition display for Member+ users.

**Current state**: Empty placeholder.

**Implementation**:

```vue
<script setup lang="ts">
import { onMounted } from 'vue'
import Container from '@/components/ui/Container.vue'
import ActiveEditionCard from '@/components/camps/ActiveEditionCard.vue'
import { useCampEditions } from '@/composables/useCampEditions'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'

const { activeEdition, loading, error, getActiveEdition } = useCampEditions()

onMounted(() => getActiveEdition())
</script>

<template>
  <Container>
    <div class="py-8">
      <h1 class="mb-6 text-3xl font-bold text-gray-900">Campamento {{ new Date().getFullYear() }}</h1>

      <div v-if="loading" class="flex justify-center py-12">
        <ProgressSpinner />
      </div>

      <Message v-else-if="error" severity="error" :closable="false">
        {{ error }}
      </Message>

      <div v-else-if="activeEdition">
        <ActiveEditionCard :edition="activeEdition" />
      </div>

      <div v-else class="rounded-lg border border-gray-200 bg-gray-50 p-8 text-center">
        <p class="text-gray-500">No hay ningún campamento abierto para este año.</p>
        <p class="mt-1 text-sm text-gray-400">Cuando haya una edición disponible, aparecerá aquí.</p>
      </div>
    </div>
  </Container>
</template>
```

---

### Step 8: Create `CampEditionsPage.vue`

**File**: `frontend/src/views/camps/CampEditionsPage.vue`
**Action**: Board-only management page listing all camp editions with filtering, status change, and edit actions.

**Composables used**:

- `useCampEditions()` — `allEditions`, `loading`, `error`, `fetchAllEditions`, `changeStatus`, `updateEdition`
- `useCamps()` — `camps`, `fetchCamps` (for campId filter dropdown)
- `useToast()`, `useConfirm()`
- `useAuthStore()` for role check (guard)

**Page structure**:

```
CampEditionsPage
├── Page header: "Gestión de Ediciones de Campamento" + (optional) "Nueva Propuesta" button
├── Filter bar
│   ├── Year selector (InputNumber or Select)
│   ├── Status dropdown (Select with CampEditionStatus options)
│   └── Camp selector (Select from camps list)
├── DataTable
│   ├── Column: Camp name
│   ├── Column: Year
│   ├── Column: Dates (start → end)
│   ├── Column: Status (CampEditionStatusBadge)
│   ├── Column: Capacity / Notes preview
│   └── Column: Actions
│       ├── "Ver detalle" → router.push to /camps/editions/:id
│       ├── "Cambiar estado" (disabled for Completed) → opens CampEditionStatusDialog
│       └── "Editar" (disabled for Closed/Completed) → opens CampEditionUpdateDialog
├── CampEditionStatusDialog (v-model:visible)
└── CampEditionUpdateDialog (v-model:visible)
```

**Filter logic**:

- Use `reactive` for filter state: `{ year: null, status: null, campId: null }`.
- Watch filter state with debounce (300ms) using `useDebounceFn` from `@vueuse/core`.
- Call `fetchAllEditions(filters)` on filter change and on mount.

**Status change flow**:

1. User clicks "Cambiar estado" → set `selectedEdition` ref, open `CampEditionStatusDialog`.
2. Dialog emits `confirm(newStatus)` → call `changeStatus(id, newStatus)`.
3. On success: show toast "Estado actualizado correctamente", refresh list via `fetchAllEditions`.
4. On error: show toast with backend error message.

**Edit flow**:

1. User clicks "Editar" → set `selectedEdition` ref, open `CampEditionUpdateDialog`.
2. Dialog emits `saved(edition)` → show toast "Edición actualizada correctamente", refresh list.
3. On error inside dialog, display inline error.

**Guard**: Only accessible via `requiresBoard: true` route — no in-component role check needed, but confirm the route meta is correct.

**Implementation Notes**:

- Use `@vueuse/core`'s `useDebounceFn` for filter debounce (already in project dependencies).
- The DataTable should have `striped-rows`, `paginator`, `:rows="10"` per project standard.
- For the "Nueva Propuesta" button — this is Phase 3 functionality; link to the existing proposal flow if it exists, or hide for now.
- Status action button should be disabled when `edition.status === 'Completed'` using `:disabled`.
- Edit button should be disabled when `edition.status === 'Closed' || edition.status === 'Completed'`.

---

### Step 9: Create `CampEditionDetailPage.vue`

**File**: `frontend/src/views/camps/CampEditionDetailPage.vue`
**Action**: Read-only detail page for a single camp edition. Accessible to Member+ users.

**Route param**: `id` (UUID)

**Implementation**:

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import Container from '@/components/ui/Container.vue'
import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
import { useCampEditions } from '@/composables/useCampEditions'
import type { CampEdition } from '@/types/camp-edition'

const route = useRoute()
const { loading, error, getEditionById } = useCampEditions()
const edition = ref<CampEdition | null>(null)

onMounted(async () => {
  edition.value = await getEditionById(route.params.id as string)
})
</script>
```

- Show all edition fields in a structured layout.
- Back button → `router.back()`.
- If null, show "Edición no encontrada" with a back button.
- Members only see Open/Closed/Completed editions (the API enforces this server-side; frontend shows 404-style message if null).

---

### Step 10: Add Routes to Router

**File**: `frontend/src/router/index.ts`
**Action**: Add two new routes inside the Camp Management section.

```typescript
// Camp Editions Management (Board only)
{
  path: '/camps/editions',
  name: 'camp-editions',
  component: () => import('@/views/camps/CampEditionsPage.vue'),
  meta: {
    title: 'ABUVI | Gestión de Ediciones',
    requiresAuth: true,
    requiresBoard: true
  }
},
// Camp Edition Detail (Member+)
{
  path: '/camps/editions/:id',
  name: 'camp-edition-detail',
  component: () => import('@/views/camps/CampEditionDetailPage.vue'),
  meta: {
    title: 'ABUVI | Detalle de Edición',
    requiresAuth: true
  }
},
```

Place these **before** the legacy routes section, in the "Camp Management routes" block.

**Important**: The route `/camps/editions/active` would conflict with `/camps/editions/:id` if not ordered correctly. However, because navigation to `/active` is done via the API only (not as a page route), there is no conflict — the string `active` in the URL would be matched as an `id` parameter. Since the backend handles the `/active` URL path, the frontend does not need a `/camps/editions/active` route.

---

### Step 11: Write Composable Unit Tests

**File**: `frontend/src/composables/__tests__/useCampEditions.test.ts`
**Action**: Unit tests for all new/modified methods.

**Setup**:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampEditions } from '@/composables/useCampEditions'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() }
}))
```

**Tests to write**:

| Group | Test | Description |
|-------|------|-------------|
| `getActiveEdition` | success | Returns `ActiveCampEditionResponse` and sets `activeEdition.value` |
| `getActiveEdition` | with year param | URL includes `?year=2026` |
| `getActiveEdition` | not found (null data) | Sets `activeEdition.value = null`, no error |
| `getActiveEdition` | API error | Sets `error.value` with backend message |
| `changeStatus` | uses PATCH | Calls `api.patch` (not `api.post`) |
| `changeStatus` | correct payload | Sends `{ status: 'Open' }` (not `{ newStatus }`) |
| `changeStatus` | updates editions array | Local state is updated after success |
| `changeStatus` | not found | Sets `error.value` |
| `updateEdition` | success | Returns updated edition, updates local array |
| `updateEdition` | accepts UpdateCampEditionRequest | Type check — no `campId` or `year` in request |
| `fetchAllEditions` | no filters | Calls `/camps/editions` |
| `fetchAllEditions` | with year filter | URL includes `?year=2025` |
| `fetchAllEditions` | with multiple filters | All filter params included |
| `fetchAllEditions` | API error | Sets `error.value`, `allEditions.value = []` |
| `fetchAllEditions` | loading state | `loading` is `true` during call, `false` after |

---

### Step 12: Write Cypress E2E Tests

**File**: `frontend/cypress/e2e/camps/camp-editions.cy.ts`
**Action**: E2E tests for the two user flows.

**Test structure**:

```typescript
describe('Camp Editions — Member view (/camp)', () => {
  beforeEach(() => {
    cy.login('member@abuvi.org', 'password')
    cy.intercept('GET', '/api/camps/editions/active', { fixture: 'active-edition.json' }).as('getActive')
    cy.visit('/camp')
    cy.wait('@getActive')
  })

  it('should display active edition card when an open edition exists', () => {
    cy.get('[data-testid="active-edition-card"]').should('be.visible')
    cy.get('[data-testid="active-edition-card"]').should('contain.text', 'Campamento 2026')
  })

  it('should show empty state when no active edition exists', () => {
    cy.intercept('GET', '/api/camps/editions/active', { body: { success: true, data: null, error: null } })
    cy.visit('/camp')
    cy.contains('No hay ningún campamento abierto para este año.').should('be.visible')
  })
})

describe('Camp Editions — Board management (/camps/editions)', () => {
  beforeEach(() => {
    cy.login('board@abuvi.org', 'password')
    cy.intercept('GET', '/api/camps/editions*', { fixture: 'editions-list.json' }).as('getEditions')
    cy.visit('/camps/editions')
    cy.wait('@getEditions')
  })

  it('should display editions list with status badges', () => {
    cy.get('[data-testid="editions-table"]').should('be.visible')
    cy.get('[data-testid="status-badge"]').should('have.length.greaterThan', 0)
  })

  it('should open status dialog when clicking change status', () => {
    cy.get('[data-testid="change-status-btn"]').first().click()
    cy.get('[data-testid="status-dialog"]').should('be.visible')
  })

  it('should change status and refresh list on confirm', () => {
    cy.intercept('PATCH', '/api/camps/editions/*/status', { fixture: 'edition-updated.json' }).as('patchStatus')
    cy.get('[data-testid="change-status-btn"]').first().click()
    cy.get('[data-testid="confirm-status-btn"]').click()
    cy.wait('@patchStatus')
    cy.contains('Estado actualizado correctamente').should('be.visible')
  })

  it('should open edit dialog and save changes', () => {
    cy.intercept('PUT', '/api/camps/editions/*', { fixture: 'edition-updated.json' }).as('putEdition')
    cy.get('[data-testid="edit-edition-btn"]').first().click()
    cy.get('[data-testid="edition-dialog"]').should('be.visible')
    cy.get('[data-testid="save-edition-btn"]').click()
    cy.wait('@putEdition')
    cy.contains('Edición actualizada correctamente').should('be.visible')
  })

  it('should redirect Member to /home when visiting /camps/editions', () => {
    cy.login('member@abuvi.org', 'password')
    cy.visit('/camps/editions')
    cy.url().should('include', '/home')
  })
})
```

**Cypress fixtures to create** (`frontend/cypress/fixtures/`):

- `active-edition.json` — `ActiveCampEditionResponse` sample
- `editions-list.json` — Array of `CampEdition` samples with various statuses
- `edition-updated.json` — Single `CampEdition` after update

**`data-testid` attributes to add** (during component implementation):

- `active-edition-card` on `ActiveEditionCard.vue` root
- `editions-table` on the DataTable wrapper
- `status-badge` on each `CampEditionStatusBadge`
- `change-status-btn`, `edit-edition-btn` on action buttons
- `status-dialog`, `edition-dialog` on Dialog components
- `confirm-status-btn`, `save-edition-btn` on dialog confirm buttons

---

### Step 13: Update Technical Documentation

**Action**: Update `ai-specs/specs/api-spec.yml` with the 5 new Phase 4 endpoints.

**Endpoints to document**:

```yaml
PATCH /camps/editions/{id}/status:
  auth: Board+
  body: ChangeEditionStatusRequest
  response: ApiResponse<CampEditionResponse>

GET /camps/editions/active:
  auth: Member+
  query: year (optional, int)
  response: ApiResponse<ActiveCampEditionResponse | null>

GET /camps/editions/{id}:
  auth: Member+
  response: ApiResponse<CampEditionResponse>

PUT /camps/editions/{id}:
  auth: Board+
  body: UpdateCampEditionRequest
  response: ApiResponse<CampEditionResponse>

GET /camps/editions:
  auth: Board+
  query: year, status, campId (all optional)
  response: ApiResponse<CampEditionResponse[]>
```

---

## 4. Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Update TypeScript types (`camp-edition.ts`)
3. Step 2 — Update `useCampEditions` composable (fixes + `fetchAllEditions`)
4. Step 3 — Create `CampEditionStatusBadge.vue`
5. Step 11 — Write composable unit tests (TDD — write failing tests first for Step 2 changes)
6. Step 4 — Create `CampEditionStatusDialog.vue`
7. Step 5 — Create `CampEditionUpdateDialog.vue`
8. Step 6 — Create `ActiveEditionCard.vue`
9. Step 7 — Update `CampPage.vue`
10. Step 8 — Create `CampEditionsPage.vue`
11. Step 9 — Create `CampEditionDetailPage.vue`
12. Step 10 — Add routes to router
13. Step 12 — Write Cypress E2E tests
14. Step 13 — Update technical documentation

---

## 5. Testing Checklist

- [ ] `useCampEditions.changeStatus` uses `api.patch`, not `api.post`
- [ ] `useCampEditions.changeStatus` sends `{ status }`, not `{ newStatus }`
- [ ] `useCampEditions.getActiveEdition` uses `ActiveCampEditionResponse` type
- [ ] `useCampEditions.fetchAllEditions` builds query string correctly for all filter combinations
- [ ] All composable methods set `loading = true` before and `loading = false` in `finally`
- [ ] All composable methods set `error` on catch
- [ ] `CampEditionsPage` loads on mount with no filters
- [ ] Filters debounce 300ms before triggering API
- [ ] Status badge renders correct color for all 5 statuses
- [ ] Status dialog shows correct next status for each current status
- [ ] Status dialog is not openable for `Completed` editions
- [ ] Edit dialog disables date/price fields for `Open` editions
- [ ] Edit dialog shows message explaining restrictions for `Open` editions
- [ ] Edit dialog is not openable for `Closed`/`Completed` editions
- [ ] `CampPage` shows empty state when active edition is null
- [ ] `CampPage` shows `ActiveEditionCard` when active edition exists
- [ ] `/camps/editions` route redirects Members to `/home`
- [ ] `/camp` route is accessible to all authenticated users
- [ ] All unit tests pass: `npx vitest`
- [ ] No TypeScript errors: `npx vue-tsc --noEmit`
- [ ] All E2E tests pass: `npx cypress run`

---

## 6. Error Handling Patterns

### Composable errors

- All methods use the standard pattern: `error.value = response?.data?.error?.message || 'fallback message'`
- Fallback messages in Spanish (e.g., `'Error al cambiar estado'`, `'Error al actualizar edición'`)

### Status transition errors

- `changeStatus` returns `null` on failure — parent checks for `null` and shows toast:

  ```typescript
  const result = await changeStatus(edition.id, newStatus)
  if (result) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Estado actualizado correctamente', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value || 'Error al cambiar estado', life: 5000 })
  }
  ```

### Edit form errors

- Backend validation errors come back as `ApiError` with `details[]` array (field-level errors).
- The edit dialog displays inline field errors if the backend returns `error.details`.

---

## 7. UI/UX Considerations

### Status transition colors

Use the `CampEditionStatusBadge` consistently across all views — never hardcode status colors elsewhere.

### Status transition dialog

- Show a clear "before → after" status visual (arrows between badges).
- Include a warning for date-constrained transitions:
  - `Draft → Open`: "La edición se abrirá para inscripciones. Asegúrate de que la fecha de inicio no ha pasado."
  - `Closed → Completed`: "La edición se marcará como completada. Solo es posible si la fecha de fin ya ha pasado."

### Edit form — Open edition

- Show a `Message` component with `severity="info"`:
  > "Esta edición está abierta para inscripciones. Solo se pueden modificar las notas y la capacidad máxima."
- Visually disable (via `:disabled="true"`) date and price fields so users understand why they can't be changed.

### Loading states

- Use `ProgressSpinner` centered in the page content area during `loading`.
- Disable action buttons while `loading` is true to prevent double-clicks.

### Responsive design

- `CampEditionsPage` DataTable: On mobile (`< sm`), hide less critical columns (notes, capacity).
- `ActiveEditionCard`: Full-width on mobile, 2-column grid on `md+` for price breakdown.

### Accessibility

- `data-testid` attributes on all interactive elements (for testing and debugging).
- `aria-label` on icon-only buttons in the DataTable actions column.
- Keyboard navigation in dialogs (PrimeVue Dialog handles focus trap).

---

## 8. Dependencies

All dependencies already exist in the project:

- `primevue` — `DataTable`, `Column`, `Dialog`, `InputNumber`, `InputText`, `Textarea`, `Select`, `DatePicker`, `Button`, `Message`, `ProgressSpinner`, `ToggleSwitch`
- `@vueuse/core` — `useDebounceFn` for filter debounce
- `vitest` + `@vue/test-utils` — unit tests
- `cypress` — E2E tests

No new npm packages required.

---

## 9. Notes

### Critical constraints

- **`changeStatus` HTTP method mismatch**: The existing composable uses `POST` but the backend expects `PATCH`. This must be fixed in Step 2a — it is a breaking change relative to any code that calls `changeStatus`.
- **Field naming verification**: Before finalizing types, verify the exact JSON field names returned by the backend for `customBabyMaxAge` vs `babyMaxAge` by inspecting an actual API response.
- **`Proposed → Draft` is handled by the existing `promoteEdition` method (Phase 3)**, NOT by `changeStatus`. The `changeStatus` method only handles `Draft → Open → Closed → Completed`.
- **Rejection is Phase 3** (`rejectEdition`). Do not add rejection UI in Phase 4 components.
- **`registrationCount` is always 0** — display it with a note like "inscripciones: 0 (próximamente)" or simply display the number without special annotation.

### Business rules in UI

- Users cannot see editions in `Proposed` or `Draft` status (backend enforces; frontend shows null/empty for members).
- Board members can see all non-archived editions via `GET /camps/editions`.
- The "active" edition for members is specifically the `Open` status edition for the current year.

### Language

- All user-facing text, labels, buttons, validation messages in **Spanish**.
- All code (variables, functions, component names, TypeScript interfaces) in **English**.
- All test names and descriptions in **English**.

---

## 10. Next Steps After Implementation

- Verify backend is running with Phase 4 endpoints deployed before testing E2E.
- When the Registrations feature is implemented, update `ActiveEditionCard` to display real `registrationCount` (the field is already in the type — just the value will become non-zero).
- Consider adding navigation link to `/camps/editions` in the board navigation menu (`AppHeader.vue` or sidebar).
- Pagination for `GET /api/camps/editions` is out of scope for this ticket — the backend returns all results.

---

## 11. Implementation Verification

- [ ] **Code Quality**: All `<script setup lang="ts">`, no `any`, TypeScript strict compliance
- [ ] **Functionality**: `CampPage` loads active edition; `CampEditionsPage` lists, filters, updates, and transitions
- [ ] **Testing**: Vitest composable tests pass; Cypress E2E for both user flows pass
- [ ] **Integration**: `changeStatus` uses `PATCH`; `getActiveEdition` uses `ActiveCampEditionResponse`
- [ ] **Documentation**: `api-spec.yml` updated with 5 new endpoints
- [ ] **Responsive**: Mobile layout verified for `CampPage` and `CampEditionsPage`
- [ ] **Auth**: `/camps/editions` redirects Members; `/camp` accessible to all authenticated users

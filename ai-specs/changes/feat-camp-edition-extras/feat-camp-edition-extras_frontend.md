# Frontend Implementation Plan: feat-camp-edition-extras — Camp Edition Extras Management

## Overview

This plan covers the frontend implementation of Camp Edition Extras — optional add-ons (t-shirts, meals, insurance, etc.) that board members can define per camp edition. The feature follows Vue 3 Composition API with composable-based API communication, PrimeVue components, and Tailwind CSS for styling. No new routes are needed; extras are embedded within the existing `CampEditionDetailPage.vue` as a dedicated section, following the same pattern as `CampPhotoGallery.vue` within the camp detail page.

**Critical pre-existing scaffold (must update, not create from scratch):**

- `frontend/src/composables/useCampExtras.ts` — **exists with URL bugs and missing methods**
- `frontend/src/types/camp-edition.ts` — **exists with field name discrepancies**

---

## Architecture Context

### Components / Composables Involved

| File | Status | Action |
|------|--------|--------|
| `frontend/src/types/camp-edition.ts` | Exists — has bugs | Fix `CampEditionExtra`, `CreateCampExtraRequest`; add `UpdateCampExtraRequest` |
| `frontend/src/composables/useCampExtras.ts` | Exists — has URL bugs + missing methods | Fix URLs, add `activeOnly`, `activateExtra`, `deactivateExtra` |
| `frontend/src/components/camps/CampEditionExtrasFormDialog.vue` | Does not exist | Create |
| `frontend/src/components/camps/CampEditionExtrasList.vue` | Does not exist | Create |
| `frontend/src/views/camps/CampEditionDetailPage.vue` | Exists | Add `CampEditionExtrasList` section |
| `frontend/src/composables/__tests__/useCampExtras.test.ts` | Does not exist | Create |
| `frontend/cypress/e2e/camps/camp-edition-extras.cy.ts` | Does not exist | Create |

### Routing

No new routes are required. The extras management UI is embedded in the existing edition detail page at `/camps/editions/:id`.

### State Management

- **Local state only** via `useCampExtras(editionId)` composable — no Pinia store needed.
- Role-based UI visibility via `useAuthStore().isBoard` computed getter.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the frontend-specific feature branch.
- **Branch Naming**: `feature/feat-camp-edition-extras-frontend`
- **Implementation Steps**:
  1. Ensure you are on `main`: `git checkout main`
  2. Pull latest changes: `git pull origin main`
  3. Create new branch: `git checkout -b feature/feat-camp-edition-extras-frontend`
  4. Verify branch creation: `git branch`
- **Notes**: This branch is separate from the backend branch (`feature/feat-camp-edition-extras-backend`). Never commit directly to `main`. Refer to `ai-specs/specs/frontend-standards.mdc` for development workflow.

---

### Step 1: Update TypeScript Types

- **File**: `frontend/src/types/camp-edition.ts`
- **Action**: Fix field discrepancies in `CampEditionExtra` and `CreateCampExtraRequest`, and add new `UpdateCampExtraRequest` type.

#### 1.1 — Fix `CampEditionExtra` interface (lines 68–83)

Current issues:

- `currentQuantity: number` → rename to `currentQuantitySold: number | null` (nullable, calculated from registrations)
- `sortOrder: number` → **remove** (not in backend schema — flagged as future enhancement in spec)

Correct interface:

```typescript
export interface CampEditionExtra {
  id: string
  campEditionId: string
  name: string
  description?: string
  price: number
  pricingType: 'PerPerson' | 'PerFamily'
  pricingPeriod: 'OneTime' | 'PerDay'
  isRequired: boolean
  maxQuantity?: number
  currentQuantitySold: number | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}
```

#### 1.2 — Fix `CreateCampExtraRequest` interface (lines 85–94)

Current issues:

- `sortOrder: number` → **remove**

Correct interface:

```typescript
export interface CreateCampExtraRequest {
  name: string
  description?: string
  price: number
  pricingType: 'PerPerson' | 'PerFamily'
  pricingPeriod: 'OneTime' | 'PerDay'
  isRequired: boolean
  maxQuantity?: number
}
```

#### 1.3 — Add `UpdateCampExtraRequest` interface (new, after `CreateCampExtraRequest`)

The update request is distinct from create: it allows toggling `isActive` but does NOT allow changing `pricingType` or `pricingPeriod`.

```typescript
export interface UpdateCampExtraRequest {
  name: string
  description?: string
  price: number
  isRequired: boolean
  isActive: boolean
  maxQuantity?: number
}
```

- **Dependencies**: None (pure TypeScript types).
- **Implementation Notes**: After changing `currentQuantity` to `currentQuantitySold`, TypeScript will flag any existing usages in components (none currently exist since no extras components exist yet). The `sortOrder` removal is safe — no existing component references it.

---

### Step 2: Update `useCampExtras` Composable

- **File**: `frontend/src/composables/useCampExtras.ts`
- **Action**: Fix three URL bugs, add `activeOnly` filter, add `activateExtra` and `deactivateExtra`, fix `updateExtra` type signature.

#### URL Bug Summary

| Method | Current (wrong) URL | Correct URL |
|--------|---------------------|-------------|
| `getExtraById` | `/camps/editions/${editionId}/extras/${extraId}` | `/camps/editions/extras/${extraId}` |
| `updateExtra` | PUT `/camps/editions/${editionId}/extras/${extraId}` | PUT `/camps/editions/extras/${extraId}` |
| `deleteExtra` | DELETE `/camps/editions/${editionId}/extras/${extraId}` | DELETE `/camps/editions/extras/${extraId}` |

The list endpoint (`fetchExtras`) correctly uses `/camps/editions/${editionId}/extras` — only add the `activeOnly` query param.
The create endpoint (`createExtra`) correctly uses POST `/camps/editions/${editionId}/extras` — no change needed.

#### Complete rewrite of `useCampExtras.ts`

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { CampEditionExtra, CreateCampExtraRequest, UpdateCampExtraRequest } from '@/types/camp-edition'
import type { ApiResponse } from '@/types/api'

export function useCampExtras(editionId: string) {
  const extras = ref<CampEditionExtra[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchExtras = async (activeOnly?: boolean): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const params = activeOnly !== undefined ? { activeOnly } : {}
      const response = await api.get<ApiResponse<CampEditionExtra[]>>(
        `/camps/editions/${editionId}/extras`,
        { params }
      )
      extras.value = response.data.success && response.data.data ? response.data.data : []
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar extras'
      console.error('Failed to fetch extras:', err)
      extras.value = []
    } finally {
      loading.value = false
    }
  }

  const getExtraById = async (extraId: string): Promise<CampEditionExtra | null> => {
    loading.value = true
    error.value = null
    try {
      // NOTE: by-ID endpoint does NOT use editionId in URL
      const response = await api.get<ApiResponse<CampEditionExtra>>(
        `/camps/editions/extras/${extraId}`
      )
      return response.data.success && response.data.data ? response.data.data : null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar extra'
      console.error('Failed to fetch extra:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const createExtra = async (
    request: CreateCampExtraRequest
  ): Promise<CampEditionExtra | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<CampEditionExtra>>(
        `/camps/editions/${editionId}/extras`,
        request
      )
      if (response.data.success && response.data.data) {
        extras.value.push(response.data.data)
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al crear extra'
      console.error('Failed to create extra:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const updateExtra = async (
    extraId: string,
    request: UpdateCampExtraRequest
  ): Promise<CampEditionExtra | null> => {
    loading.value = true
    error.value = null
    try {
      // NOTE: by-ID endpoint does NOT use editionId in URL
      const response = await api.put<ApiResponse<CampEditionExtra>>(
        `/camps/editions/extras/${extraId}`,
        request
      )
      if (response.data.success && response.data.data) {
        const index = extras.value.findIndex((e) => e.id === extraId)
        if (index !== -1) {
          extras.value[index] = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al actualizar extra'
      console.error('Failed to update extra:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const deleteExtra = async (extraId: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      // NOTE: by-ID endpoint does NOT use editionId in URL
      await api.delete(`/camps/editions/extras/${extraId}`)
      extras.value = extras.value.filter((e) => e.id !== extraId)
      return true
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al eliminar extra'
      console.error('Failed to delete extra:', err)
      return false
    } finally {
      loading.value = false
    }
  }

  const activateExtra = async (extraId: string): Promise<CampEditionExtra | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.patch<ApiResponse<CampEditionExtra>>(
        `/camps/editions/extras/${extraId}/activate`
      )
      if (response.data.success && response.data.data) {
        const index = extras.value.findIndex((e) => e.id === extraId)
        if (index !== -1) {
          extras.value[index] = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al activar extra'
      console.error('Failed to activate extra:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const deactivateExtra = async (extraId: string): Promise<CampEditionExtra | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.patch<ApiResponse<CampEditionExtra>>(
        `/camps/editions/extras/${extraId}/deactivate`
      )
      if (response.data.success && response.data.data) {
        const index = extras.value.findIndex((e) => e.id === extraId)
        if (index !== -1) {
          extras.value[index] = response.data.data
        }
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al desactivar extra'
      console.error('Failed to deactivate extra:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    extras,
    loading,
    error,
    fetchExtras,
    getExtraById,
    createExtra,
    updateExtra,
    deleteExtra,
    activateExtra,
    deactivateExtra
  }
}
```

- **Dependencies**: `UpdateCampExtraRequest` from Step 1.
- **Implementation Notes**: The composable factory pattern `useCampExtras(editionId)` is preserved. The `editionId` is still required for the list and create endpoints.

---

### Step 3: Create `CampEditionExtrasFormDialog.vue` Component

- **File**: `frontend/src/components/camps/CampEditionExtrasFormDialog.vue`
- **Action**: Create a Dialog-based form for creating and editing camp edition extras.
- **Component Signature**:

  ```typescript
  interface Props {
    visible: boolean
    editionId: string
    extra?: CampEditionExtra  // undefined = create mode, defined = edit mode
  }
  interface Emits {
    'update:visible': [value: boolean]
    saved: [extra: CampEditionExtra]
  }
  ```

#### Implementation Steps

1. Use `<Dialog v-model:visible="dialogVisible" :header="..." modal>` (PrimeVue Dialog)
2. Manage `dialogVisible` as a computed wrapping `props.visible` / emitting `update:visible`
3. Form fields:
   - **Nombre** (`name`): `InputText`, required, max 200 chars — show validation error if empty
   - **Descripción** (`description`): `Textarea`, optional, max 1000 chars
   - **Precio** (`price`): `InputNumber`, mode `currency`, currency `EUR`, min 0 — required
   - **Tipo de precio** (`pricingType`): `Select` with options: `{ label: 'Por persona', value: 'PerPerson' }`, `{ label: 'Por familia', value: 'PerFamily' }`
   - **Período de precio** (`pricingPeriod`): `Select` with options: `{ label: 'Una vez', value: 'OneTime' }`, `{ label: 'Por día', value: 'PerDay' }`
   - **Obligatorio** (`isRequired`): `Checkbox`
   - **Cantidad máxima** (`maxQuantity`): `InputNumber`, optional, integer, min 1 — show label "Sin límite" when empty
   - **Activo** (`isActive`): `Checkbox` — only shown in **edit mode** (not on create since new extras start active by default)
4. Client-side validation before submit:
   - `name` non-empty
   - `price` >= 0
   - `maxQuantity` > 0 if provided
5. On submit: call `createExtra(formData)` (create mode) or `updateExtra(extra.id, formData)` (edit mode) from `useCampExtras(editionId)`
6. On success: emit `saved(result)`, close dialog, show toast success
7. On error: show toast error (use `error.value` from composable)
8. Reset form state when dialog opens (watch `visible`)
9. Use `data-testid` attributes on key elements: `extra-form-dialog`, `extra-name-input`, `extra-price-input`, `extra-submit-button`

#### PrimeVue Components Used

- `Dialog`, `InputText`, `Textarea`, `InputNumber`, `Select`, `Checkbox`, `Button`
- `useToast()` for notifications

#### Notes

- All labels in **Spanish**
- No `<style>` block — Tailwind only
- `pricingType` and `pricingPeriod` are **not editable** in edit mode (render as read-only display text) since changing them could cause calculation inconsistencies for already-sold extras

---

### Step 4: Create `CampEditionExtrasList.vue` Component

- **File**: `frontend/src/components/camps/CampEditionExtrasList.vue`
- **Action**: Create the main extras management UI — a DataTable of extras with create/edit/delete/activate/deactivate actions, visible only to authenticated users; write actions restricted to Board+.
- **Component Signature**:

  ```typescript
  interface Props {
    editionId: string
    editionStatus: CampEditionStatus
  }
  ```

#### Implementation Steps

1. Import `useCampExtras(props.editionId)`, `useAuthStore`, `useToast`, `useConfirm`
2. Call `fetchExtras()` in `onMounted`
3. Board-only write actions: `const canManage = computed(() => authStore.isBoard)`
4. **Header section**: title "Extras de la edición ({{ extras.length }})" + "Añadir extra" `Button` (visible only when `canManage && editionStatus !== 'Completed' && editionStatus !== 'Closed'`)
5. **Loading state**: `ProgressSpinner` while `loading`
6. **Error state**: PrimeVue `Message` severity="error" when `error`
7. **Empty state**: icon `pi pi-list-check`, text "No hay extras configurados para esta edición." + "Añadir el primero" button (Board only) — `data-testid="empty-extras-state"`
8. **DataTable** (when extras.length > 0):
   - Columns:
     - **Nombre** — `extra.name` (bold) + `extra.description` (small, gray, below name)
     - **Precio** — formatted as currency EUR; below it: pricing type label (e.g., "Por persona · Una vez")
     - **Obligatorio** — `Tag` severity: "danger" label "Sí" / grayed out "No"
     - **Estado** — `Tag`: severity "success" label "Activo" / severity "secondary" label "Inactivo"
     - **Vendidos** — `extra.currentQuantitySold ?? '—'` / `extra.maxQuantity ? '${qty}/${max}' : '${qty}'`
     - **Acciones** (Board only):
       - Toggle activate/deactivate: icon `pi pi-eye` (if inactive) / `pi pi-eye-slash` (if active), text/plain button, `data-testid="toggle-active-button-{id}"`
       - Edit: icon `pi pi-pencil`, text button, opens form dialog, `data-testid="edit-extra-button-{id}"`
       - Delete: icon `pi pi-trash`, text button, danger, calls `confirmDelete`, `data-testid="delete-extra-button-{id}"`
9. **Activate/Deactivate logic**:
   - Call `activateExtra(id)` or `deactivateExtra(id)` from composable
   - On success: show toast "Extra activado" / "Extra desactivado"
   - On error: show toast error
10. **Delete confirmation** via `useConfirm().require(...)`:
    - Message: "¿Estás seguro de que quieres eliminar este extra? Esta acción no se puede deshacer. Si el extra tiene ventas registradas, no podrá eliminarse."
    - On confirm: call `deleteExtra(id)`, show success toast or error toast
11. **Form dialog**: `CampEditionExtrasFormDialog` with `v-model:visible` + `extra` prop + `@saved="handleSaved"`
    - `handleSaved`: update extras list optimistically (the composable already updates the local array on save)
12. Include `<ConfirmDialog />` at the bottom
13. `data-testid` attributes: `extras-list`, `extras-table`, `add-extra-button`

#### Helper Display Functions

```typescript
const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR', minimumFractionDigits: 0 }).format(amount)

const pricingTypeLabel = (type: 'PerPerson' | 'PerFamily'): string =>
  type === 'PerPerson' ? 'Por persona' : 'Por familia'

const pricingPeriodLabel = (period: 'OneTime' | 'PerDay'): string =>
  period === 'OneTime' ? 'Una vez' : 'Por día'
```

#### PrimeVue Components Used

- `DataTable`, `Column`, `Button`, `Tag`, `ProgressSpinner`, `Message`, `ConfirmDialog`
- `useConfirm`, `useToast`

---

### Step 5: Update `CampEditionDetailPage.vue`

- **File**: `frontend/src/views/camps/CampEditionDetailPage.vue`
- **Action**: Add the `CampEditionExtrasList` section at the bottom of the edition detail view.

#### Implementation Steps

1. Import `CampEditionExtrasList` from `@/components/camps/CampEditionExtrasList.vue`
2. Import `CampEditionStatus` type from `@/types/camp-edition` (already imported indirectly through `CampEdition`)
3. In the template, after the description section, add a new section:

   ```html
   <!-- Camp Edition Extras -->
   <div v-if="edition" class="mt-6 rounded-lg border border-gray-200 bg-white p-6">
     <CampEditionExtrasList
       :edition-id="edition.id"
       :edition-status="edition.status"
       data-testid="edition-extras-section"
     />
   </div>
   ```

4. Place the extras section **inside** the `v-else-if="edition"` block, after the description card.

- **Implementation Notes**: The section is wrapped in a conditional so it only renders when the edition is loaded. No other changes to the page are needed. Member users will see the extras list (read-only); Board users will see action buttons.

---

### Step 6: Write Vitest Unit Tests for `useCampExtras`

- **File**: `frontend/src/composables/__tests__/useCampExtras.test.ts`
- **Action**: Create comprehensive unit tests covering all composable methods, including the URL fixes and new methods.

#### Test Structure Pattern (follow `useCampPhotos.test.ts`)

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampExtras } from '@/composables/useCampExtras'
import { api } from '@/utils/api'
import type { CampEditionExtra } from '@/types/camp-edition'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    patch: vi.fn()
  }
}))

const editionId = 'edition-123'

const makeExtra = (overrides: Partial<CampEditionExtra> = {}): CampEditionExtra => ({
  id: 'extra-1',
  campEditionId: editionId,
  name: 'Camp T-Shirt',
  description: 'Official t-shirt',
  price: 15,
  pricingType: 'PerPerson',
  pricingPeriod: 'OneTime',
  isRequired: false,
  maxQuantity: 100,
  currentQuantitySold: 0,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  ...overrides
})
```

#### Required Test Cases

**`fetchExtras`:**

- `fetchExtras_WithNoFilter_CallsEditionScopedEndpoint` — verify URL `/camps/editions/${editionId}/extras` without params
- `fetchExtras_WithActiveOnlyTrue_PassesQueryParam` — verify `{ params: { activeOnly: true } }`
- `fetchExtras_OnSuccess_PopulatesExtrasArray`
- `fetchExtras_OnFailure_SetsErrorAndEmptiesArray`
- `fetchExtras_SetsLoadingFalseAfterCompletion`

**`getExtraById` (URL fix):**

- `getExtraById_CallsGlobalExtrasEndpointWithoutEditionId` — verify URL is `/camps/editions/extras/${extraId}` (no editionId prefix)
- `getExtraById_OnSuccess_ReturnsExtra`
- `getExtraById_OnFailure_ReturnsNull`

**`createExtra`:**

- `createExtra_OnSuccess_AddsExtraToLocalArray`
- `createExtra_OnSuccess_ReturnsCreatedExtra`
- `createExtra_OnFailure_ReturnsNullAndSetsError`

**`updateExtra` (URL fix + type fix):**

- `updateExtra_CallsGlobalExtrasEndpointWithoutEditionId` — verify PUT URL is `/camps/editions/extras/${extraId}`
- `updateExtra_OnSuccess_UpdatesLocalArrayAtCorrectIndex`
- `updateExtra_OnFailure_ReturnsNullAndSetsError`

**`deleteExtra` (URL fix):**

- `deleteExtra_CallsGlobalExtrasEndpointWithoutEditionId` — verify DELETE URL is `/camps/editions/extras/${extraId}`
- `deleteExtra_OnSuccess_RemovesExtraFromLocalArray`
- `deleteExtra_OnFailure_ReturnsFalseAndSetsError`

**`activateExtra` (new):**

- `activateExtra_CallsActivateEndpoint` — verify PATCH URL is `/camps/editions/extras/${extraId}/activate`
- `activateExtra_OnSuccess_UpdatesExtraInLocalArray`
- `activateExtra_OnFailure_ReturnsNullAndSetsError`

**`deactivateExtra` (new):**

- `deactivateExtra_CallsDeactivateEndpoint` — verify PATCH URL is `/camps/editions/extras/${extraId}/deactivate`
- `deactivateExtra_OnSuccess_UpdatesExtraInLocalArray`
- `deactivateExtra_OnFailure_ReturnsNullAndSetsError`

**Total: ~18 test cases minimum.**

---

### Step 7: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/camps/camp-edition-extras.cy.ts`
- **Action**: Write E2E tests for the critical user flows of Camp Edition Extras management.

#### Test Setup Pattern (follow `camp-photos.cy.ts`)

```typescript
const EDITION_ID = 'edition-test-1'
const EDITION_URL = `/camps/editions/${EDITION_ID}`
const EXTRAS_API = `/api/camps/editions/${EDITION_ID}/extras`

const boardUser = { id: 'user-board', email: 'board@example.com', firstName: 'Board', lastName: 'User', role: 'Board', isActive: true }
const memberUser = { id: 'user-member', email: 'member@example.com', firstName: 'Member', lastName: 'User', role: 'Member', isActive: true }

const mockExtra = {
  id: 'extra-1', campEditionId: EDITION_ID, name: 'Camiseta del campamento',
  description: 'Camiseta oficial', price: 15, pricingType: 'PerPerson',
  pricingPeriod: 'OneTime', isRequired: false, maxQuantity: 100,
  currentQuantitySold: 5, isActive: true, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z'
}

const setAuthState = (user: typeof boardUser | typeof memberUser) => {
  cy.window().then((win) => {
    win.localStorage.setItem('abuvi_auth_token', 'fake-jwt-token')
    win.localStorage.setItem('abuvi_user', JSON.stringify(user))
  })
}
```

#### Required Test Suites

**Role-based visibility:**

- Board user sees extras section with "Añadir extra" button
- Member user sees extras section but NOT "Añadir extra" button and NOT action buttons
- Empty state shown when no extras exist (Board user)

**Create extra (Board):**

- Opens form dialog when clicking "Añadir extra"
- Submits form and shows newly created extra in the list
- Shows validation error when name is missing
- Shows validation error when price is negative

**Edit extra (Board):**

- Opens edit dialog with pre-filled values when clicking edit button
- Submits updated values and reflects in the list

**Delete extra (Board):**

- Shows confirmation dialog before deleting
- Deletes extra and removes from list after confirmation
- Shows error toast when API returns 400 (extra has sales)

**Activate/Deactivate (Board):**

- Clicking deactivate on active extra shows "Inactivo" state
- Clicking activate on inactive extra shows "Activo" state

---

### Step 8: Update Technical Documentation

- **Action**: Review and update documentation files affected by this implementation.
- **Implementation Steps**:
  1. Review all code changes made during implementation
  2. **`ai-specs/specs/api-spec.yml`** (if it exists): Add the 7 new extras API endpoints with request/response schemas
  3. **`ai-specs/specs/frontend-standards.mdc`**: No changes needed — the implementation follows existing patterns
  4. Verify all changes are accurately reflected in documentation
- **References**: Follow `ai-specs/specs/documentation-standards.mdc`
- **Notes**: This step is MANDATORY before considering the implementation complete.

---

## Implementation Order

1. Step 0 — Create feature branch `feature/feat-camp-edition-extras-frontend`
2. Step 1 — Update TypeScript types (`camp-edition.ts`)
3. Step 2 — Update `useCampExtras.ts` composable (fix URLs, add methods)
4. Step 6 — Write Vitest unit tests for composable (test-driven for the URL fixes)
5. Step 3 — Create `CampEditionExtrasFormDialog.vue`
6. Step 4 — Create `CampEditionExtrasList.vue`
7. Step 5 — Update `CampEditionDetailPage.vue`
8. Step 7 — Write Cypress E2E tests
9. Step 8 — Update technical documentation

---

## Testing Checklist

### Vitest Unit Tests

- [ ] `useCampExtras.test.ts` created with 18+ test cases
- [ ] `fetchExtras` with/without `activeOnly` param tested
- [ ] `getExtraById` URL fix tested (no `editionId` prefix)
- [ ] `updateExtra` URL fix tested + new type signature
- [ ] `deleteExtra` URL fix tested (no `editionId` prefix)
- [ ] `activateExtra` PATCH URL tested
- [ ] `deactivateExtra` PATCH URL tested
- [ ] All loading/error state management tested
- [ ] All tests pass: `npm run test -- useCampExtras`

### Cypress E2E Tests

- [ ] Board role sees full management UI (create/edit/delete/toggle)
- [ ] Member role sees read-only view (no action buttons)
- [ ] Create flow: form opens, validates, submits, shows success toast
- [ ] Edit flow: pre-filled form, updates list on save
- [ ] Delete flow: confirmation dialog, removes from list
- [ ] Activate/deactivate flow: toggle button updates status badge
- [ ] Error flow: API error shown as toast

### Component Functionality

- [ ] `CampEditionExtrasList` renders in `CampEditionDetailPage`
- [ ] Extras are loaded on mount via `fetchExtras()`
- [ ] Empty state shows when no extras
- [ ] Loading spinner shown during API calls
- [ ] Error message shown on API failure
- [ ] `pricingType`/`pricingPeriod` are read-only in edit mode

### Integration Verification

- [ ] All API calls use the correct base URL (no duplicated `editionId` for by-ID endpoints)
- [ ] `createExtra` POST to `/camps/editions/${editionId}/extras` (edition-scoped)
- [ ] `fetchExtras` GET from `/camps/editions/${editionId}/extras` (edition-scoped)
- [ ] `getExtraById` GET from `/camps/editions/extras/${extraId}` (global)
- [ ] `updateExtra` PUT to `/camps/editions/extras/${extraId}` (global)
- [ ] `deleteExtra` DELETE to `/camps/editions/extras/${extraId}` (global)
- [ ] `activateExtra` PATCH to `/camps/editions/extras/${extraId}/activate`
- [ ] `deactivateExtra` PATCH to `/camps/editions/extras/${extraId}/deactivate`

---

## Error Handling Patterns

### In Composable (`useCampExtras.ts`)

- Each method wraps API calls in `try/catch/finally`
- `error.value` is populated with API error message (from `response.data.error.message`) or a Spanish fallback string
- `loading.value` is always reset to `false` in `finally`
- Fallback messages in Spanish: "Error al cargar extras", "Error al crear extra", "Error al actualizar extra", "Error al eliminar extra", "Error al activar extra", "Error al desactivar extra"

### In Components

- Display `<Message severity="error">{{ error }}</Message>` for persistent errors (list failed to load)
- Use `useToast()` for transient operation feedback (success/error after create, update, delete, activate, deactivate)
- Show toast on success: `{ severity: 'success', summary: 'Éxito', detail: '...', life: 3000 }`
- Show toast on error: `{ severity: 'error', summary: 'Error', detail: error.value, life: 5000 }`

---

## UI/UX Considerations

### PrimeVue Components

- `DataTable` + `Column` for the extras list (vs. card grid for photos — extras have tabular data)
- `Dialog` for create/edit form
- `ConfirmDialog` + `useConfirm` for delete confirmation
- `Tag` for status (active/inactive) and isRequired badges
- `InputNumber` with `mode="currency"` for price input
- `Select` for `pricingType` and `pricingPeriod` dropdowns
- `Checkbox` for `isRequired` and `isActive` (edit mode only)

### Layout

- Extras section is a card (`rounded-lg border border-gray-200 bg-white p-6`) within `CampEditionDetailPage`, below the description card
- Full-width section (not in 2-column grid like General Info / Pricing cards)
- DataTable is not paginated (extras per edition expected to be < 20)

### Responsive Design

- DataTable uses `scrollable` with horizontal scroll on mobile
- Hide less important columns (`description`, `maxQuantity`) on small screens using PrimeVue column `hidden sm:table-cell` pattern

### Accessibility

- All buttons have descriptive labels or `aria-label`
- Form inputs have associated `<label>` elements with `for` attribute
- Error messages are announced via PrimeVue Toast (aria-live)

---

## Dependencies

### npm Packages Required

No new npm packages required. All dependencies are already in the project:

### PrimeVue Components Used

- `DataTable`, `Column` — extras list
- `Dialog` — create/edit form
- `InputText` — name field
- `Textarea` — description field
- `InputNumber` — price and maxQuantity fields
- `Select` — pricingType, pricingPeriod fields
- `Checkbox` — isRequired, isActive fields
- `Button` — all actions
- `Tag` — status badges
- `ProgressSpinner` — loading state
- `Message` — error state
- `ConfirmDialog` — delete confirmation
- `useConfirm`, `useToast` — composable hooks

---

## Notes

- **User-facing text**: All text visible to users must be in **Spanish** (as per `frontend-standards.mdc`)
- **TypeScript**: Strict typing — no `any` usage; all interfaces properly typed
- **Script setup**: All components must use `<script setup lang="ts">`
- **No `<style>` blocks**: Tailwind utility classes only
- **`pricingType` / `pricingPeriod` immutability in edit**: These fields cannot be changed via update — hide them or show as read-only text in the edit form. The backend `UpdateCampEditionExtraRequest` does not include these fields.
- **Board vs Admin**: Use `authStore.isBoard` (which returns `true` for both `Board` and `Admin` roles) to gate write actions
- **Edition status gating**: Do not show "Añadir extra" for `Completed` or `Closed` editions (the backend will reject it, but the UI should prevent it proactively)
- **`currentQuantitySold` display**: Show `'—'` when `null` (extras created before registration system is implemented). When a limit exists, show as `"vendidos / max"` (e.g., "5 / 100")
- **`sortOrder` is out of scope**: The spec notes `sortOrder` as a future enhancement; do not implement it

---

## Next Steps After Implementation

- Coordinate with backend team to ensure the 7 API endpoints (`feat-camp-edition-extras-backend` branch) are deployed before testing the full E2E flow
- Integration testing against the real API (not mocked) once both branches are merged
- Registration system integration (future): `currentQuantitySold` will show real data once `RegistrationExtras` is implemented on the backend

---

## Implementation Verification

### Code Quality

- [ ] No TypeScript `any` usage
- [ ] All components use `<script setup lang="ts">`
- [ ] No `<style>` blocks (Tailwind only)
- [ ] All user-facing strings are in Spanish
- [ ] ESLint passes: `npm run lint`
- [ ] TypeScript checks pass: `npm run type-check`

### Functionality

- [ ] `CampEditionExtrasList` renders correctly within `CampEditionDetailPage`
- [ ] All 7 API endpoints are called with the correct URLs
- [ ] Create/Edit/Delete/Activate/Deactivate operations work end-to-end (with mocked API in tests)
- [ ] Role-based visibility works: Board users see actions, Member users do not
- [ ] Edition status gating: "Añadir extra" hidden for Completed/Closed editions

### Testing

- [ ] Vitest: 18+ unit tests for `useCampExtras`, all passing
- [ ] Cypress: 10+ E2E scenarios for critical user flows, all passing

### Integration

- [ ] `useCampExtras` composable connects to backend API correctly (URL verification)
- [ ] Types in `camp-edition.ts` match backend DTOs (`currentQuantitySold`, no `sortOrder`)
- [ ] `UpdateCampExtraRequest` does not include `pricingType`/`pricingPeriod`

### Documentation

- [ ] API spec updated with new extras endpoints (if `api-spec.yml` covers them)
- [ ] No documentation regressions

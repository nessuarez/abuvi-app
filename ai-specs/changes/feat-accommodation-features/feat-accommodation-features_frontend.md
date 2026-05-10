# Frontend Implementation Plan: feat-accommodation-features — Configurable Accommodation Features

## Overview

This feature adds a UI for the Board to manage an extensible catalogue of accommodation characteristics (`AccommodationFeature`) and to tag those characteristics onto individual accommodations and accommodation zones (`AccommodationZone`). It also allows attaching photos and floor-plans to accommodations and zones via the existing media-items flow.

Tech stack: Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue 4.5.4, TailwindCSS 4, Pinia (auth only), composable-based data fetching with Axios.

---

## Architecture Context

**New files:**

- `frontend/src/types/accommodation-feature.ts`
- `frontend/src/composables/useAccommodationFeatures.ts`
- `frontend/src/composables/useAccommodationFeatureAssignment.ts`
- `frontend/src/components/camps/AccommodationFeaturesCataloguePanel.vue`
- `frontend/src/components/camps/AccommodationFeatureDialog.vue`
- `frontend/src/components/camps/FeatureAssignmentDialog.vue`

**Modified files:**

- `frontend/src/types/camp-edition.ts` — extend `CampEditionAccommodation` with `features`; extend `AccommodationZone` with `features` + `mediaItems`
- `frontend/src/composables/useAccommodationZones.ts` — add zone feature assignment methods
- `frontend/src/composables/useMediaItems.ts` — add `accommodationId`/`zoneId` support
- `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` — add feature assignment button + `features` display
- `frontend/src/components/camps/AccommodationZonePanel.vue` — add feature assignment section

**Routing:** No new routes required. Features catalogue panel integrates into the existing camp-edition detail view (same page, additional panel or tab).

**State management:** Composable-local state (no new Pinia store). Feature catalogue is fetched once per parent view and passed as props to child components.

---

## ⚠️ API URL Note

Zone feature assignment endpoints live under **`/accommodation-zones`**, not `/zones`:

- `GET/PUT /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/features`

All other paths match the spec.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch:** `feature/feat-accommodation-features-frontend`
- Base on the current `feat-encaje-bolillos` worktree.
- Verify: `git branch`

---

### Step 1: Define TypeScript Types

**File:** `frontend/src/types/accommodation-feature.ts` *(new)*

```typescript
export type FeatureApplicabilityLevel =
  | 'Zone'
  | 'Accommodation'
  | 'AccommodationType'
  | 'Any'

export const FEATURE_APPLICABILITY_LABELS: Record<FeatureApplicabilityLevel, string> = {
  Zone: 'Solo zonas',
  Accommodation: 'Solo alojamientos',
  AccommodationType: 'Por tipo de alojamiento',
  Any: 'Cualquiera',
}

export interface AccommodationFeature {
  id: string
  name: string
  icon: string
  description: string | null
  applicabilityLevel: FeatureApplicabilityLevel
  isActive: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export interface CreateAccommodationFeatureRequest {
  name: string
  icon: string
  description?: string | null
  applicabilityLevel: FeatureApplicabilityLevel
  sortOrder?: number
}

export interface UpdateAccommodationFeatureRequest {
  name: string
  icon: string
  description?: string | null
  applicabilityLevel: FeatureApplicabilityLevel
  isActive: boolean
  sortOrder: number
}

export interface SetFeatureAssignmentsRequest {
  featureIds: string[]
}
```

**File:** `frontend/src/types/camp-edition.ts` *(modify)*

1. Add `features: AccommodationFeature[]` to `CampEditionAccommodation` interface.
2. Add `features: AccommodationFeature[]` and `mediaItems: MediaItem[]` to `AccommodationZone` interface (or wherever the zone response type is defined).
3. Add optional `zoneId?: string | null` to `CreateCampEditionAccommodationRequest` and `UpdateCampEditionAccommodationRequest` if not already present.

Import: `import type { AccommodationFeature } from './accommodation-feature'`

---

### Step 2: Create `useAccommodationFeatures` Composable

**File:** `frontend/src/composables/useAccommodationFeatures.ts` *(new)*

Handles the global feature catalogue (not edition-scoped).

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AccommodationFeature,
  CreateAccommodationFeatureRequest,
  UpdateAccommodationFeatureRequest,
} from '@/types/accommodation-feature'

export function useAccommodationFeatures() {
  const features = ref<AccommodationFeature[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const saving = ref(false)
  const saveError = ref<string | null>(null)

  async function fetchFeatures(activeOnly?: boolean) {
    loading.value = true
    error.value = null
    try {
      const params = activeOnly !== undefined ? { activeOnly } : {}
      const res = await api.get<ApiResponse<AccommodationFeature[]>>(
        '/accommodation-features',
        { params },
      )
      features.value = res.data.data ?? []
    } catch (err) {
      error.value = extractError(err)
    } finally {
      loading.value = false
    }
  }

  async function createFeature(
    request: CreateAccommodationFeatureRequest,
  ): Promise<AccommodationFeature | null> {
    saving.value = true
    saveError.value = null
    try {
      const res = await api.post<ApiResponse<AccommodationFeature>>(
        '/accommodation-features',
        request,
      )
      const created = res.data.data!
      features.value.push(created)
      return created
    } catch (err) {
      saveError.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  async function updateFeature(
    id: string,
    request: UpdateAccommodationFeatureRequest,
  ): Promise<AccommodationFeature | null> {
    saving.value = true
    saveError.value = null
    try {
      const res = await api.put<ApiResponse<AccommodationFeature>>(
        `/accommodation-features/${id}`,
        request,
      )
      const updated = res.data.data!
      const idx = features.value.findIndex((f) => f.id === id)
      if (idx !== -1) features.value[idx] = updated
      return updated
    } catch (err) {
      saveError.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  async function deleteFeature(id: string): Promise<boolean> {
    try {
      await api.delete(`/accommodation-features/${id}`)
      features.value = features.value.filter((f) => f.id !== id)
      return true
    } catch (err) {
      saveError.value = extractError(err)
      return false
    }
  }

  return {
    features,
    loading,
    error,
    saving,
    saveError,
    fetchFeatures,
    createFeature,
    updateFeature,
    deleteFeature,
  }
}

function extractError(err: unknown): string {
  return (
    (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
      ?.message ?? 'Ha ocurrido un error inesperado'
  )
}
```

---

### Step 3: Create `useAccommodationFeatureAssignment` Composable

**File:** `frontend/src/composables/useAccommodationFeatureAssignment.ts` *(new)*

Handles feature assignments to individual accommodations and zones. Edition-scoped.

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { AccommodationFeature, SetFeatureAssignmentsRequest } from '@/types/accommodation-feature'

export function useAccommodationFeatureAssignment(editionId: string) {
  const saving = ref(false)
  const error = ref<string | null>(null)

  async function getAccommodationFeatures(
    accommodationId: string,
  ): Promise<AccommodationFeature[]> {
    const res = await api.get<ApiResponse<AccommodationFeature[]>>(
      `/camps/editions/${editionId}/accommodations/${accommodationId}/features`,
    )
    return res.data.data ?? []
  }

  async function setAccommodationFeatures(
    accommodationId: string,
    featureIds: string[],
  ): Promise<AccommodationFeature[] | null> {
    saving.value = true
    error.value = null
    try {
      const body: SetFeatureAssignmentsRequest = { featureIds }
      const res = await api.put<ApiResponse<AccommodationFeature[]>>(
        `/camps/editions/${editionId}/accommodations/${accommodationId}/features`,
        body,
      )
      return res.data.data ?? []
    } catch (err) {
      error.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  // Zone feature assignment — note the URL uses "accommodation-zones", not "zones"
  async function getZoneFeatures(zoneId: string): Promise<AccommodationFeature[]> {
    const res = await api.get<ApiResponse<AccommodationFeature[]>>(
      `/camps/editions/${editionId}/accommodation-zones/${zoneId}/features`,
    )
    return res.data.data ?? []
  }

  async function setZoneFeatures(
    zoneId: string,
    featureIds: string[],
  ): Promise<AccommodationFeature[] | null> {
    saving.value = true
    error.value = null
    try {
      const body: SetFeatureAssignmentsRequest = { featureIds }
      const res = await api.put<ApiResponse<AccommodationFeature[]>>(
        `/camps/editions/${editionId}/accommodation-zones/${zoneId}/features`,
        body,
      )
      return res.data.data ?? []
    } catch (err) {
      error.value = extractError(err)
      return null
    } finally {
      saving.value = false
    }
  }

  return { saving, error, getAccommodationFeatures, setAccommodationFeatures, getZoneFeatures, setZoneFeatures }
}

function extractError(err: unknown): string {
  return (
    (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
      ?.message ?? 'Ha ocurrido un error inesperado'
  )
}
```

---

### Step 4: Extend `useAccommodationZones` Composable

**File:** `frontend/src/composables/useAccommodationZones.ts` *(modify)*

The zone feature assignment methods are handled by `useAccommodationFeatureAssignment` (Step 3), so no changes needed in this composable for features. However, if `AccommodationZoneResponse` now includes `features` and `mediaItems` from the backend, ensure the type used in this composable matches the updated `AccommodationZone` interface (Step 1).

If the composable constructs the type locally, update it to include `features: AccommodationFeature[]` and `mediaItems: MediaItem[]` in the return type.

---

### Step 5: Extend `useMediaItems` Composable

**File:** `frontend/src/composables/useMediaItems.ts` *(modify)*

1. Update `createMediaItem` (or `uploadMediaItem`) to accept optional `accommodationId?: string` and `zoneId?: string` and include them in the POST body.
2. The backend enforces `IsApproved = true` / `IsPublished = false` automatically when these IDs are set — no client-side change needed for those flags.
3. If there is a `fetchMediaItems` function, add optional `accommodationId` and `zoneId` query params so callers can fetch media scoped to an accommodation or zone.

---

### Step 6: Create `AccommodationFeatureDialog.vue`

**File:** `frontend/src/components/camps/AccommodationFeatureDialog.vue` *(new)*

Create/edit dialog for a single feature in the catalogue.

**Props:**

```typescript
defineProps<{
  visible: boolean
  feature?: AccommodationFeature | null
}>()
```

**Emits:** `update:visible`, `saved`

**Form fields:**

| Field | Component | Validation |
|---|---|---|
| `name` | `InputText` | Required, max 100 chars |
| `icon` | `InputText` with emoji preview | Required, max 100 chars |
| `description` | `Textarea` | Optional, max 500 chars |
| `applicabilityLevel` | `Select` (options from `FEATURE_APPLICABILITY_LABELS`) | Required |
| `isActive` | `ToggleSwitch` | Edit mode only (hidden on create) |
| `sortOrder` | `InputNumber` | >= 0 |

**Behaviour:**

- On open: reset form to defaults or populate from `feature` prop.
- On save: validate → call `createFeature` or `updateFeature` from `useAccommodationFeatures` injected/passed as prop → emit `saved` → emit `update:visible(false)`.
- Show field-level error messages below each input.
- Footer: Cancel button + Save button (disabled while `saving`).

**Pattern:** Follow the same structure as `CampEditionAccommodationDialog.vue` (watch `props.visible`, reactive form object, `validationErrors` ref).

---

### Step 7: Create `FeatureAssignmentDialog.vue`

**File:** `frontend/src/components/camps/FeatureAssignmentDialog.vue` *(new)*

Multi-select dialog to assign features to an accommodation or zone.

**Props:**

```typescript
defineProps<{
  visible: boolean
  title: string                          // e.g. "Características de Habitación Norte"
  initialFeatureIds: string[]            // currently assigned
  availableFeatures: AccommodationFeature[]  // full active catalogue (passed from parent)
}>()
```

**Emits:** `update:visible`, `saved(featureIds: string[])`

**Content:**

- List of checkboxes (PrimeVue `Checkbox` inside a scrollable container, or `MultiSelect` component) showing only active features from `availableFeatures`.
- Group by `applicabilityLevel` using subheadings for clarity.
- Each row shows: `[icon] [name]` — icon renders as emoji if the string is a single emoji, otherwise renders as plain text label.
- Pre-check the IDs in `initialFeatureIds`.

**Behaviour:**

- Tracks `selectedIds: string[]` local state.
- Save button: emit `saved(selectedIds)` → parent calls `setAccommodationFeatures` or `setZoneFeatures` → parent handles loading/error, closes dialog on success.
- No API call inside the dialog itself (keeps it pure UI).

---

### Step 8: Create `AccommodationFeaturesCataloguePanel.vue`

**File:** `frontend/src/components/camps/AccommodationFeaturesCataloguePanel.vue` *(new)*

Board-accessible panel for managing the global feature catalogue. Integrates into the camp-edition detail page as an additional section/tab.

**Structure:**

```
[Header: "Catálogo de características"]  [+ Nueva característica] button
[DataTable]
  Columns: Icon | Nombre | Nivel | Estado | Orden | Acciones
  Row actions: Editar (pencil) | Eliminar (trash, with confirm)
[Loading spinner / error message if fetch fails]
```

**DataTable columns:**

- **Icono:** renders `feature.icon` (emoji-safe via `<span>`)
- **Nombre:** `feature.name`
- **Nivel:** `FEATURE_APPLICABILITY_LABELS[feature.applicabilityLevel]` + PrimeVue `Tag` with colour per level:
  - `Zone` → `secondary` | `Accommodation` → `info` | `AccommodationType` → `warning` | `Any` → `success`
- **Estado:** `Tag` — Active (success) / Inactive (danger)
- **Orden:** `feature.sortOrder`
- **Acciones:** Edit button → opens `AccommodationFeatureDialog`; Delete button → PrimeVue `ConfirmDialog`

**Delete handling:**

- On 409 (`FEATURE_IN_USE` code or any 409): show `Toast` error — "Esta característica está en uso. Desactívala en lugar de eliminarla."
- On success: remove from local list + success toast.

**Uses:** `useAccommodationFeatures()` composable (fetch on `onMounted`).

**Parent integration:** Add this component to the camp-edition detail view. Recommended placement: a new collapsible section or tab labelled "Características" alongside the existing Accommodations and Zones sections. The exact placement depends on the current UI layout — confirm with the existing `CampEditionDetailPage.vue` structure.

---

### Step 9: Extend `CampEditionAccommodationsPanel.vue`

**File:** `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` *(modify)*

1. **Display assigned features** on each accommodation row: render a row of `Tag` or icon badges below the name, using `accommodation.features`.

2. **Add "Características" action button** per row (or icon in the actions column) that opens `FeatureAssignmentDialog` with:
   - `title`: `"Características — {accommodation.name}"`
   - `initialFeatureIds`: `accommodation.features.map(f => f.id)`
   - `availableFeatures`: the active catalogue passed as prop from parent

3. **On `FeatureAssignmentDialog` `saved` event:**
   - Call `setAccommodationFeatures(accommodationId, featureIds)` from `useAccommodationFeatureAssignment`.
   - On success: update `accommodation.features` in local list + success toast.
   - On error: error toast with message from `saveError`.

4. **Prop change:** Accept `availableFeatures: AccommodationFeature[]` as a prop (the active catalogue is fetched once by the parent view and shared).

5. **`ZoneId` support:** If the accommodation dialog (`CampEditionAccommodationDialog.vue`) doesn't yet have a `ZoneId` selector, add a `Select` field bound to `zoneId` showing the list of zones for the edition. This requires the panel to receive `zones` as a prop too.

---

### Step 10: Extend `AccommodationZonePanel.vue`

**File:** `frontend/src/components/camps/AccommodationZonePanel.vue` *(modify)*

1. **Display assigned features** in each zone row/card: render feature icon badges from `zone.features`.

2. **Add "Características" button** per zone that opens `FeatureAssignmentDialog` with:
   - `title`: `"Características — {zone.name}"`
   - `initialFeatureIds`: `zone.features.map(f => f.id)`
   - `availableFeatures`: active catalogue prop from parent

3. **On `FeatureAssignmentDialog` `saved` event:**
   - Call `setZoneFeatures(zoneId, featureIds)` from `useAccommodationFeatureAssignment`.
   - On success: update `zone.features` in local list + success toast.

4. **Accept `availableFeatures: AccommodationFeature[]` prop** from parent.

5. **Media items section** (per zone): add a small media gallery sub-section per zone. Each zone in `zone.mediaItems` renders as a thumbnail grid. Add an "Adjuntar archivo" button that opens the existing file-upload flow with `zoneId` set. Use `useMediaItems` with `zoneId` context (Step 5). This is a lower-priority addition — implement last.

---

### Step 11: Wire Together in the Parent View

**File:** wherever the camp-edition detail renders accommodations + zones panels *(modify)*

The parent view needs to:

1. Instantiate `useAccommodationFeatures()` and call `fetchFeatures(true)` on mount (active only).
2. Pass `availableFeatures` as a prop to `CampEditionAccommodationsPanel`, `AccommodationZonePanel`, and `AccommodationFeaturesCataloguePanel`.
3. Instantiate `useAccommodationFeatureAssignment(editionId)` and pass its methods (or the composable result) as props or provide/inject to the child panels.
4. Add `AccommodationFeaturesCataloguePanel` to the view (new section or tab).

If the parent view is getting too large, consider splitting into sub-panels with a tab layout using PrimeVue `Tabs`.

---

### Step 12: Write Unit Tests (Vitest)

**`useAccommodationFeatures.test.ts`:**

```
fetchFeatures — returns list on success
fetchFeatures — sets error on API failure
createFeature — appends to list on success, returns null on error
updateFeature — updates item in list on success
deleteFeature — removes from list on success, returns false and sets saveError on 409
```

**`useAccommodationFeatureAssignment.test.ts`:**

```
setAccommodationFeatures — calls correct URL and returns list on success
setAccommodationFeatures — returns null and sets error on failure
setZoneFeatures — calls correct accommodation-zones URL
```

**`AccommodationFeatureDialog.test.ts`:**

```
renders empty form when no feature prop
renders form populated when feature prop provided
shows isActive toggle only in edit mode
emits saved on successful create
shows validation errors on empty submit
```

**`FeatureAssignmentDialog.test.ts`:**

```
renders all availableFeatures as checkboxes
pre-checks initialFeatureIds
emits saved with selected IDs on save
emits saved with empty array when all unchecked
```

---

### Step 13: Write E2E Tests (Cypress)

**File:** `frontend/cypress/e2e/accommodation-features.cy.ts` *(new)*

```
Feature catalogue — create a new feature and verify it appears in the list
Feature catalogue — edit a feature and verify changes persisted
Feature catalogue — delete a feature in use shows 409 error toast
Feature assignment (accommodation) — assign features and verify they appear as badges on the row
Feature assignment (zone) — assign features to a zone and verify badges appear
```

---

### Step 14: Update Technical Documentation

- **`ai-specs/specs/frontend-standards.mdc`**: If any new patterns are introduced (e.g. feature icon rendering, multi-select assignment dialog pattern), document them.
- **`ai-specs/specs/api-spec.yml`**: Verify the accommodation feature endpoints are reflected (the backend plan handles this, but cross-check).

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — TypeScript types (`accommodation-feature.ts` + updates to `camp-edition.ts`)
3. Step 2 — `useAccommodationFeatures` composable
4. Step 3 — `useAccommodationFeatureAssignment` composable
5. Step 4 — Update `useAccommodationZones` types if needed
6. Step 5 — Extend `useMediaItems`
7. Step 6 — `AccommodationFeatureDialog.vue`
8. Step 7 — `FeatureAssignmentDialog.vue`
9. Step 8 — `AccommodationFeaturesCataloguePanel.vue`
10. Step 9 — Extend `CampEditionAccommodationsPanel.vue`
11. Step 10 — Extend `AccommodationZonePanel.vue`
12. Step 11 — Wire parent view
13. Step 12 — Vitest unit tests
14. Step 13 — Cypress E2E tests
15. Step 14 — Documentation

---

## Testing Checklist

- [ ] `pnpm type-check` passes — no TypeScript errors
- [ ] `pnpm lint` passes — no ESLint errors
- [ ] `pnpm test:unit` — all Vitest tests green
- [ ] `pnpm test:e2e` — Cypress happy paths green
- [ ] Feature catalogue: create / edit / deactivate / delete (409 on in-use)
- [ ] Feature assignment to accommodation: multiselect → save → badges appear on row
- [ ] Feature assignment to zone: same flow
- [ ] Empty assignment (uncheck all) removes all badges
- [ ] Network error states shown as error messages / toasts
- [ ] Responsive at `sm` / `md` / `lg` breakpoints

---

## Error Handling Patterns

Follow the existing composable error pattern:

```typescript
} catch (err) {
  saveError.value =
    (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error
      ?.message ?? 'Ha ocurrido un error inesperado'
  return null
}
```

**Specific error codes to handle in the UI:**

| Backend code | Scenario | UI message |
|---|---|---|
| `BUSINESS_RULE_VIOLATION` (409) on delete | Feature in use | "Esta característica está en uso. Desactívala en lugar de eliminarla." |
| `BUSINESS_RULE_VIOLATION` (409) on create/update | Duplicate name | Use the server message directly (it's already in Spanish) |
| `VALIDATION_ERROR` (400) on assignment | Inactive/missing feature | Show server message |
| Network error | API unreachable | "No se pudo conectar con el servidor. Inténtalo de nuevo." |

Display errors via PrimeVue `Toast` for save/delete operations and `Message` component (severity=error) for load failures.

---

## UI/UX Considerations

- **Icon field:** Simple `InputText`. Show a live preview of the emoji/icon value next to the field. No emoji picker library needed — the board enters the emoji directly from their OS keyboard.
- **Applicability level select:** Use PrimeVue `Select` with `optionLabel`/`optionValue` from `FEATURE_APPLICABILITY_LABELS`. Apply coloured `Tag` badges in the table for quick visual scan.
- **Feature badges on accommodation/zone rows:** Small `Tag` components or `<span class="rounded px-1 text-sm">` with the icon + name. Cap at 3 visible + "…+N more" if overflowing.
- **FeatureAssignmentDialog:** Use a scrollable checkbox list (PrimeVue `Checkbox` per item inside a `div` with `overflow-y-auto max-h-96`), grouped by applicability level with a `<p class="font-semibold mt-2">` subheading.
- **Loading:** `ProgressSpinner` centered when `loading` is true. Disabled (opacity-50) state on Save button when `saving` is true.
- **Empty state:** "No hay características configuradas. Crea la primera haciendo clic en 'Nueva característica'."

---

## Dependencies

No new npm packages required. All needed packages are already installed:

- `primevue` 4.5.4 — `Button`, `DataTable`, `Column`, `Dialog`, `InputText`, `Textarea`, `Select`, `ToggleSwitch`, `InputNumber`, `Checkbox`, `Tag`, `Toast`, `ConfirmDialog`, `Message`
- `axios` — API calls
- `vue` 3 — Composition API

---

## Notes

1. **Zone URL pattern.** Zone feature assignment is at `/api/camps/editions/{editionId}/accommodation-zones/{zoneId}/features` — NOT `/zones/`. This is different from the spec document but matches the actual backend implementation.
2. **Type changes to records.** The backend changed `CampEditionAccommodationResponse` and `AccommodationZoneResponse` to include `features`. Update the corresponding TypeScript interfaces accordingly, or the TypeScript compiler will surface missing fields.
3. **Active catalogue only.** When showing the `FeatureAssignmentDialog`, fetch only active features (`activeOnly=true`). The catalogue panel shows all features (active + inactive) to allow the board to manage them.
4. **Catalogue is not edition-scoped.** `useAccommodationFeatures` calls `/api/accommodation-features` — no edition ID needed. Only the assignment endpoints are edition-scoped.
5. **`<script setup lang="ts">` is required** on all new components — no Options API.
6. **No `any` types** — use the error extraction pattern shown in Step 2 instead of casting to `any`.

---

## Next Steps After Implementation

- Coordinate with backend on whether `zoneId` on accommodations (for assigning an accommodation to a zone) is also exposed in the UI — the backend DTO update supports it but the current `CampEditionAccommodationDialog` may not have a `ZoneId` selector yet. Check and add if missing.
- The encaje de bolillos assignment board will eventually display feature badges as compatibility signals — that is a separate ticket.

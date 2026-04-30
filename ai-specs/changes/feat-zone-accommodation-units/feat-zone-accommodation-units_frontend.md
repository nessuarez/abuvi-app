# Frontend Implementation Plan: feat-zone-accommodation-units — Unit management inside accommodation zones

## Overview

Adds `countByFamily` visibility and configurability to the accommodation management UI, and introduces a units sub-panel inside each zone row in `AccommodationZonePanel`. The feature allows the Board to create/edit/manage individual units (rooms, plots) directly from within a zone, with the accommodation type pre-set and locked to the zone's type.

Architecture principles: Vue 3 `<script setup lang="ts">`, composable-based API access, PrimeVue + Tailwind CSS, no Pinia (component-local or page-prop-drilled state is sufficient here).

---

## Architecture Context

### Files to modify

| File | Change |
|---|---|
| `frontend/src/types/camp-edition.ts` | Add `countByFamily` to 3 interfaces |
| `frontend/src/components/camps/CampEditionAccommodationDialog.vue` | New props `prefilledZoneId`, `prefilledType`; `countByFamily` toggle; locked type select |
| `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` | Add `countByFamily` tag badge per row |
| `frontend/src/components/camps/AccommodationZonePanel.vue` | Add `editionId` prop; expandable DataTable rows with units sub-panel |
| `frontend/src/views/camps/CampEditionDetailPage.vue` | Page-level `useCampAccommodations`; pass `accommodations` + `editionId` to `AccommodationZonePanel` |

### No new files

No new composables, no new components — all changes are incremental to existing files. `CampEditionAccommodationDialog` is reused with new optional props.

### State flow

```
CampEditionDetailPage
  ├── useCampAccommodations(editionId)  ← new, page-level instance
  ├── CampEditionAccommodationsPanel    ← keeps its own internal instance (unchanged)
  └── AccommodationZonePanel
        :accommodations="accommodations"  ← from page-level composable
        :edition-id="edition.id"          ← new prop
        @unit-saved="fetchAccommodations" ← triggers re-fetch from page
```

`AccommodationZonePanel` filters accommodations client-side (`acc.zoneId === zone.id`) to build the sub-list for each expanded zone row. No additional API calls are needed in the zone panel itself.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch:** `feature/feat-zone-accommodation-units-frontend`
- Base on `feature/feat-zone-accommodation-units-backend`

```bash
git checkout feature/feat-zone-accommodation-units-backend
git checkout -b feature/feat-zone-accommodation-units-frontend
git branch
```

---

### Step 1: Update TypeScript types — `camp-edition.ts`

**File:** `frontend/src/types/camp-edition.ts`

**1a. `CampEditionAccommodation`** — add `countByFamily: boolean` after `capacity`:

```typescript
export interface CampEditionAccommodation {
  id: string
  campEditionId: string
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  countByFamily: boolean       // ← NEW
  isActive: boolean
  sortOrder: number
  currentPreferenceCount: number
  firstChoiceCount: number
  zoneId?: string | null
  zoneName?: string | null
  features: AccommodationFeature[]
  createdAt: string
  updatedAt: string
}
```

**1b. `CreateCampEditionAccommodationRequest`** — add optional `countByFamily?` and `zoneId?`:

```typescript
export interface CreateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  countByFamily?: boolean      // ← NEW (omit = server applies smart default)
  zoneId?: string | null       // ← NEW (was missing; needed for zone pre-assignment)
  sortOrder?: number
}
```

**1c. `UpdateCampEditionAccommodationRequest`** — add required `countByFamily`:

```typescript
export interface UpdateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  countByFamily: boolean       // ← NEW (required on update)
  isActive: boolean
  zoneId?: string | null       // ← NEW (was missing)
  sortOrder: number
}
```

**Implementation notes:**
- `countByFamily` is `boolean` (not `boolean | undefined`) on `CampEditionAccommodation` because the backend always returns it.
- `zoneId` on the request types was already in the backend but missing from the frontend types — add it now so the dialog can pre-assign zone when creating from the zone sub-panel.

---

### Step 2: Update `CampEditionAccommodationDialog.vue`

**File:** `frontend/src/components/camps/CampEditionAccommodationDialog.vue`

**2a. New optional props:**

```typescript
const props = defineProps<{
  visible: boolean
  editionId: string
  accommodation?: CampEditionAccommodation
  prefilledZoneId?: string | null    // pre-assigns zoneId on create
  prefilledType?: AccommodationType  // locks the type select on create
}>()
```

**2b. New reactive state for `countByFamily`:**

```typescript
const countByFamily = ref(false)
```

**2c. Update `watch` on `visible` to initialize `countByFamily` and apply smart default:**

In the `else` (create mode) branch:
```typescript
// smart default: Tent, Caravan, Motorhome → true; Lodge, Bungalow → false
countByFamily.value = props.prefilledType
  ? (['Tent', 'Caravan', 'Motorhome'] as AccommodationType[]).includes(props.prefilledType)
  : false

// lock type to prefilled, or default to Lodge
accommodationType.value = props.prefilledType ?? 'Lodge'
```

In the `if (props.accommodation)` (edit mode) branch:
```typescript
countByFamily.value = props.accommodation.countByFamily
```

**2d. Lock the `Select` when `prefilledType` is provided:**

```html
<Select
  v-model="accommodationType"
  :options="ACCOMMODATION_TYPE_OPTIONS"
  option-label="label"
  option-value="value"
  class="w-full"
  :disabled="!!props.prefilledType"
/>
```

**2e. Add `countByFamily` ToggleSwitch field** (place after Capacity, before Sort Order):

```html
<!-- Occupancy model -->
<div>
  <label class="mb-1 block text-sm font-medium text-gray-700">Modelo de ocupación</label>
  <div class="flex items-center gap-3">
    <ToggleSwitch v-model="countByFamily" />
    <span class="text-sm text-gray-700">
      {{ countByFamily ? 'Por familia/unidad' : 'Por personas (usar capacidad numérica)' }}
    </span>
  </div>
  <small class="text-gray-400">
    Activa cuando 1 tienda, caravana o autocaravana ocupa una plaza independientemente del número de personas.
  </small>
</div>
```

**2f. Update `handleSave` to include `countByFamily` and `zoneId` in both request payloads:**

Create payload:
```typescript
const result = await createAccommodation({
  name: name.value.trim(),
  accommodationType: accommodationType.value,
  description: description.value.trim() || undefined,
  capacity: capacity.value ?? undefined,
  countByFamily: countByFamily.value,
  zoneId: props.prefilledZoneId ?? undefined,
  sortOrder: sortOrder.value
})
```

Update payload:
```typescript
const result = await updateAccommodation(props.accommodation.id, {
  name: name.value.trim(),
  accommodationType: accommodationType.value,
  description: description.value.trim() || undefined,
  capacity: capacity.value ?? undefined,
  countByFamily: countByFamily.value,
  isActive: isActive.value,
  zoneId: props.accommodation.zoneId ?? undefined,
  sortOrder: sortOrder.value
})
```

---

### Step 3: Update `CampEditionAccommodationsPanel.vue` — countByFamily badge

**File:** `frontend/src/components/camps/CampEditionAccommodationsPanel.vue`

In the row header `div.flex.items-center.gap-2`, after the existing type tag, add the countByFamily indicator:

```html
<Tag
  v-if="acc.countByFamily"
  value="Por unidad"
  severity="warn"
  class="text-xs"
/>
<Tag
  v-else
  value="Por personas"
  severity="info"
  class="text-xs"
/>
```

No other changes needed in this panel — it uses its own `useCampAccommodations` instance which will automatically receive `countByFamily` in the API response once the types are updated.

---

### Step 4: Rewrite `AccommodationZonePanel.vue` — units sub-panel

**File:** `frontend/src/components/camps/AccommodationZonePanel.vue`

This is the most significant change. The zone DataTable gains row expansion to show a per-zone units sub-panel.

**4a. Add `editionId` prop:**

```typescript
const props = defineProps<{
  campEditionId: string
  accommodations: CampEditionAccommodation[]    // already exists
  availableFeatures: AccommodationFeature[]     // already exists
  editionId: string                             // ← NEW
}>()
```

**Implementation note:** `campEditionId` and `editionId` carry the same value (the parent passes `edition.id` for both). Keep `campEditionId` for the existing `useAccommodationZones` composable and add `editionId` for `CampEditionAccommodationDialog` and `useAccommodationFeatureAssignment`. This slight redundancy avoids a breaking rename.

**4b. New refs for the units sub-panel:**

```typescript
const expandedRows = ref<Record<string, boolean>>({})

// Dialog state for creating/editing units
const showUnitDialog = ref(false)
const editingUnit = ref<CampEditionAccommodation | undefined>(undefined)
const unitDialogZoneId = ref<string | null>(null)
const unitDialogType = ref<AccommodationTypeValue | undefined>(undefined)

// Delete state for units
const showDeleteUnitDialog = ref(false)
const deleteUnitTarget = ref<CampEditionAccommodation | null>(null)
```

**4c. Computed helper to get units per zone:**

```typescript
const unitsForZone = (zoneId: string): CampEditionAccommodation[] =>
  props.accommodations.filter((a) => a.zoneId === zoneId)
```

**4d. Occupancy type label helper:**

```typescript
const occupancyLabel = (acc: CampEditionAccommodation): string =>
  acc.countByFamily ? 'Por unidad' : 'Por personas'
```

**4e. Unit dialog open functions:**

```typescript
function openCreateUnit(zone: AccommodationZoneResponse) {
  editingUnit.value = undefined
  unitDialogZoneId.value = zone.id
  unitDialogType.value = zone.accommodationType
  showUnitDialog.value = true
}

function openEditUnit(acc: CampEditionAccommodation) {
  editingUnit.value = acc
  unitDialogZoneId.value = acc.zoneId ?? null
  unitDialogType.value = acc.accommodationType as AccommodationTypeValue
  showUnitDialog.value = true
}
```

**4f. Unit delete:**

```typescript
function confirmDeleteUnit(acc: CampEditionAccommodation) {
  deleteUnitTarget.value = acc
  showDeleteUnitDialog.value = true
}
```

Wire delete to `useCampAccommodations` — but since this panel receives accommodations as a prop (not owns them), emit an event to the parent to re-fetch. Use a separate local composable instance for delete/activate/deactivate operations:

```typescript
import { useCampAccommodations } from '@/composables/useCampAccommodations'

const {
  loading: unitLoading,
  error: unitError,
  deleteAccommodation,
  activateAccommodation,
  deactivateAccommodation,
} = useCampAccommodations(props.editionId)

const emit = defineEmits<{
  'unit-saved': []
}>()

async function handleDeleteUnit() {
  if (!deleteUnitTarget.value) return
  const success = await deleteAccommodation(deleteUnitTarget.value.id)
  showDeleteUnitDialog.value = false
  if (success) {
    toast.add({ severity: 'success', summary: 'Unidad eliminada', life: 3000 })
    emit('unit-saved')
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: unitError.value, life: 5000 })
  }
  deleteUnitTarget.value = null
}

async function handleToggleUnitActive(acc: CampEditionAccommodation) {
  const success = acc.isActive
    ? await deactivateAccommodation(acc.id)
    : await activateAccommodation(acc.id)
  if (success) {
    emit('unit-saved')
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: unitError.value, life: 5000 })
  }
}

function handleUnitSaved() {
  showUnitDialog.value = false
  emit('unit-saved')
}
```

**4g. Template: add `expandable` to the DataTable and row expansion template:**

```html
<DataTable
  v-else
  :value="zones"
  :loading="loading"
  v-model:expanded-rows="expandedRows"
  data-key="id"
  class="text-sm"
>
  <Column expander style="width: 3rem" />
  <!-- ... existing columns ... -->
  
  <template #expansion="{ data: zone }">
    <div class="bg-gray-50 px-4 py-3">
      <div class="mb-2 flex items-center justify-between">
        <span class="text-sm font-medium text-gray-700">
          Unidades — {{ zone.name }}
          <span class="ml-1 text-gray-400">({{ unitsForZone(zone.id).length }})</span>
        </span>
        <Button
          label="Nueva unidad"
          icon="pi pi-plus"
          size="small"
          severity="secondary"
          outlined
          @click="openCreateUnit(zone)"
        />
      </div>

      <!-- Empty state -->
      <div
        v-if="unitsForZone(zone.id).length === 0"
        class="rounded border border-dashed border-gray-200 px-3 py-4 text-center text-xs text-gray-400"
      >
        No hay unidades en esta zona. Crea la primera con "+ Nueva unidad".
      </div>

      <!-- Units table -->
      <DataTable
        v-else
        :value="unitsForZone(zone.id)"
        class="text-xs"
        size="small"
      >
        <Column header="Nombre">
          <template #body="{ data: acc }">
            <div>
              <span class="font-medium">{{ acc.name }}</span>
              <div v-if="(acc.features ?? []).length > 0" class="mt-0.5 flex flex-wrap gap-1">
                <span
                  v-for="f in (acc.features ?? []).slice(0, 3)"
                  :key="f.id"
                  class="inline-flex items-center gap-0.5 rounded bg-gray-100 px-1 py-0.5 text-xs text-gray-600"
                >
                  {{ f.icon }} {{ f.name }}
                </span>
                <span v-if="(acc.features ?? []).length > 3" class="text-gray-400">
                  +{{ (acc.features ?? []).length - 3 }} más
                </span>
              </div>
            </div>
          </template>
        </Column>
        <Column header="Cap.">
          <template #body="{ data: acc }">
            {{ acc.capacity ?? '—' }}
          </template>
        </Column>
        <Column header="Ocupación">
          <template #body="{ data: acc }">
            <Tag
              :value="occupancyLabel(acc)"
              :severity="acc.countByFamily ? 'warn' : 'info'"
              class="text-xs"
            />
          </template>
        </Column>
        <Column header="Activo">
          <template #body="{ data: acc }">
            <Tag
              :value="acc.isActive ? 'Activo' : 'Inactivo'"
              :severity="acc.isActive ? 'success' : 'secondary'"
              class="text-xs"
            />
          </template>
        </Column>
        <Column header="Acciones" style="width: 10rem">
          <template #body="{ data: acc }">
            <div class="flex gap-1">
              <Button
                icon="pi pi-pencil"
                size="small"
                text
                severity="secondary"
                title="Editar"
                @click="openEditUnit(acc)"
              />
              <Button
                :icon="acc.isActive ? 'pi pi-eye-slash' : 'pi pi-eye'"
                size="small"
                text
                severity="secondary"
                :title="acc.isActive ? 'Desactivar' : 'Activar'"
                @click="handleToggleUnitActive(acc)"
              />
              <Button
                icon="pi pi-trash"
                size="small"
                text
                severity="danger"
                title="Eliminar"
                @click="confirmDeleteUnit(acc)"
              />
            </div>
          </template>
        </Column>
      </DataTable>
    </div>
  </template>
</DataTable>
```

**4h. Add `CampEditionAccommodationDialog` to the zone panel template** (after the existing dialogs):

```html
<!-- Unit Create/Edit Dialog -->
<CampEditionAccommodationDialog
  v-model:visible="showUnitDialog"
  :edition-id="editionId"
  :accommodation="editingUnit"
  :prefilled-zone-id="unitDialogZoneId"
  :prefilled-type="unitDialogType"
  @saved="handleUnitSaved"
/>

<!-- Delete Unit Confirmation -->
<Dialog
  v-model:visible="showDeleteUnitDialog"
  header="Eliminar unidad"
  modal
  class="w-full max-w-sm"
>
  <p class="text-sm text-gray-700">
    ¿Eliminar <strong>{{ deleteUnitTarget?.name }}</strong>? Esta acción no se puede deshacer.
  </p>
  <template #footer>
    <div class="flex justify-end gap-2">
      <Button label="Cancelar" severity="secondary" text @click="showDeleteUnitDialog = false" />
      <Button label="Eliminar" severity="danger" :loading="unitLoading" @click="handleDeleteUnit" />
    </div>
  </template>
</Dialog>
```

**4i. Add import for `CampEditionAccommodationDialog`:**

```typescript
import CampEditionAccommodationDialog from './CampEditionAccommodationDialog.vue'
```

---

### Step 5: Update `CampEditionDetailPage.vue` — share accommodations with zone panel

**File:** `frontend/src/views/camps/CampEditionDetailPage.vue`

**5a. Add import for `useCampAccommodations`:**

```typescript
import { useCampAccommodations } from '@/composables/useCampAccommodations'
```

**5b. Add page-level accommodations composable instance** (after the existing composable declarations):

```typescript
// Page-level instance to share accommodations with AccommodationZonePanel
const pageAccommodations = ref<CampEditionAccommodation[]>([])
```

Actually, use the composable directly:

```typescript
import type { CampEditionAccommodation } from '@/types/camp-edition'

// Initialized lazily after edition loads — editionId is not known at module scope
const pageAccommodationsRef = ref<CampEditionAccommodation[]>([])
let fetchPageAccommodations: (() => Promise<void>) | null = null
```

But Vue composables can't be conditionally called. The cleaner pattern is to use `computed` with a null guard, or initialize after the edition loads by wrapping in `onMounted`.

**Recommended approach** — use a wrapper computed based on a ref set in `onMounted`:

```typescript
import { useCampAccommodations } from '@/composables/useCampAccommodations'

// Will be set once edition is loaded
const editionIdRef = computed(() => edition.value?.id ?? '')
const {
  accommodations: pageAccommodations,
  fetchAccommodations: fetchPageAccommodations
} = useCampAccommodations(editionIdRef.value || '__placeholder__')

// ... inside onMounted, after edition.value is set:
onMounted(async () => {
  edition.value = await getEditionById(route.params.id as string)
  // ...
  fetchFeatures(true)
  if (edition.value) {
    // Re-initialize after we know the editionId
    await fetchPageAccommodations()
  }
})
```

**Simpler alternative that avoids the placeholder issue** — pass `editionId` as a `computed ref` via the composable's string param:

Actually, the cleanest pattern given the existing codebase (where `useCampAccommodations` takes a plain `string` not a `Ref<string>`) is:

1. Keep `pageAccommodations` as a plain `ref<CampEditionAccommodation[]>([])`
2. Fetch inline in `onMounted` after edition loads using a one-off API call via the composable

**Best pattern (matches the codebase style):**

```typescript
// Declare at component scope — editionId will be filled after load
const sharedAccommodations = ref<CampEditionAccommodation[]>([])

// In onMounted, after edition is loaded:
onMounted(async () => {
  edition.value = await getEditionById(route.params.id as string)
  if (edition.value && route.query.edit === 'true' && canEdit.value) {
    startEditing()
    router.replace({ query: {} })
  }
  fetchFeatures(true)
  if (edition.value) {
    const { accommodations, fetchAccommodations } = useCampAccommodations(edition.value.id)
    sharedAccommodations.value = accommodations.value  // initially []
    await fetchAccommodations()
    sharedAccommodations.value = accommodations.value
  }
})
```

This doesn't work well because the composable's `accommodations` ref is local to the composable call inside `onMounted`.

**Final recommended pattern** — make `editionId` available as a `computed` and call the composable before `onMounted`. Use a `watchEffect` or `watch` on `edition`:

```typescript
// After edition is loaded, start the page-level accommodations fetch
const sharedEditionId = computed(() => edition.value?.id ?? '')
const {
  accommodations: sharedAccommodations,
  fetchAccommodations: refetchSharedAccommodations
} = useCampAccommodations(sharedEditionId.value)

// The composable takes a plain string; we watch edition to trigger fetch
watch(
  () => edition.value?.id,
  async (id) => {
    if (id) await refetchSharedAccommodations()
  }
)
```

But the composable is scoped to `sharedEditionId.value` at the time of the call (plain string, not reactive). Since `edition.value` is null at call time, `sharedEditionId.value` is `''` which creates a broken URL.

**Actual simplest solution given the codebase**: create a local async function that populates a `sharedAccommodations` ref using `api.get` directly (same pattern as the composable internally):

```typescript
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { CampEditionAccommodation } from '@/types/camp-edition'

const sharedAccommodations = ref<CampEditionAccommodation[]>([])

async function loadSharedAccommodations(editionId: string) {
  try {
    const response = await api.get<ApiResponse<CampEditionAccommodation[]>>(
      `/camps/editions/${editionId}/accommodations`
    )
    if (response.data.success && response.data.data) {
      sharedAccommodations.value = response.data.data
    }
  } catch {
    // non-critical; zone panel will show empty state
  }
}

// In onMounted:
onMounted(async () => {
  edition.value = await getEditionById(route.params.id as string)
  // ...
  fetchFeatures(true)
  if (edition.value) {
    await loadSharedAccommodations(edition.value.id)
  }
})
```

This is clean, doesn't duplicate composable logic for the critical path, and the `unit-saved` event from `AccommodationZonePanel` calls `loadSharedAccommodations(edition.value.id)` to re-sync.

**5c. Template changes in Tab 7:**

```html
<AccommodationZonePanel
  :camp-edition-id="edition.id"
  :accommodations="sharedAccommodations"   <!-- was :accommodations="[]" -->
  :available-features="availableFeatures"
  :edition-id="edition.id"                 <!-- ← NEW prop -->
  @unit-saved="loadSharedAccommodations(edition.id)"  <!-- ← NEW event handler -->
/>
```

---

### Step 6: Type-check verification

```bash
cd frontend
npx vue-tsc --noEmit
```

Fix any TypeScript errors surfaced (likely: missing `countByFamily` in existing `updateAccommodation` call sites, or missing `zoneId`).

---

### Step 7: Update technical documentation

**File:** `ai-specs/specs/api-spec.yml` — update `CampEditionAccommodation` schema to include `countByFamily: boolean`.

No routing or test pattern changes are required.

---

## Implementation Order

1. Step 0 — Create branch `feature/feat-zone-accommodation-units-frontend`
2. Step 1 — Add `countByFamily` (and `zoneId`) to TypeScript interfaces in `camp-edition.ts`
3. Step 2 — Update `CampEditionAccommodationDialog` (props, toggle, locked type, payloads)
4. Step 3 — Add `countByFamily` tag badge in `CampEditionAccommodationsPanel`
5. Step 4 — Add units sub-panel with row expansion to `AccommodationZonePanel`
6. Step 5 — Update `CampEditionDetailPage` to share accommodations with zone panel
7. Step 6 — Run `npx vue-tsc --noEmit` and fix any errors
8. Step 7 — Update `api-spec.yml`

---

## Testing Checklist

- [ ] `npx vue-tsc --noEmit` — 0 errors
- [ ] **Create dialog from global panel**: opens without prefilled type, type Select is enabled, `countByFamily` toggle shows, default is `false` for Lodge/Bungalow
- [ ] **Create dialog from zone panel**: type is pre-set and disabled, `countByFamily` defaults to `true` for Tent/Caravan/Motorhome, `false` for Lodge/Bungalow
- [ ] **Saving from zone panel**: new unit appears in the zone's sub-list after save
- [ ] **Edit unit from zone sub-panel**: opens dialog with all fields pre-filled including `countByFamily`
- [ ] **Activate/deactivate unit**: `Activo`/`Inactivo` badge updates
- [ ] **Delete unit**: unit disappears from sub-list
- [ ] **Occupancy badge in global panel**: "Por unidad" (warn) or "Por personas" (info) shown correctly
- [ ] **Occupancy tag in zone sub-panel**: matches the badge shown in global panel
- [ ] **Empty zone expansion**: shows empty state message + "Nueva unidad" button
- [ ] **Multiple zones**: expanding one zone doesn't expand others (each zone's `expandedRows` key is independent)
- [ ] **No regressions**: existing zone CRUD (create/edit/delete zone, attach accommodations, features) unaffected

---

## Error Handling Patterns

- Unit create/edit errors: surfaced via `error` ref from `useCampAccommodations` inside the dialog (existing pattern via `Message` in the dialog footer area)
- Unit delete/toggle errors: `toast.add({ severity: 'error', ... })` from `AccommodationZonePanel` (same pattern as zone delete)
- `sharedAccommodations` load failure in the page: caught silently — the zone panel shows an empty sub-list (non-critical)

---

## UI/UX Considerations

- **Row expander**: PrimeVue DataTable `expander` Column + `v-model:expanded-rows` — clicking the chevron expands the zone row inline; no navigation
- **Nested DataTable**: the inner DataTable uses `size="small"` to distinguish it visually from the outer table
- **`countByFamily` ToggleSwitch**: same pattern as the existing `allowPartialAttendance` toggle in `CampEditionDetailPage` — label changes dynamically based on the toggle value
- **Type Select locked**: `:disabled="!!props.prefilledType"` — PrimeVue `Select` shows the value but is not interactive
- **Tag severity mapping**: `countByFamily = true → severity="warn"` (amber), `false → severity="info"` (blue) — consistent between global panel and zone sub-panel
- **Loading states**: `unitLoading` ref from the zone panel's local `useCampAccommodations` instance drives `:loading` on the delete button

---

## Dependencies

No new npm packages. All PrimeVue components already in use:
- `DataTable`, `Column` (row expansion via `expander` + `v-model:expanded-rows` — existing, just not used for expansion yet)
- `ToggleSwitch` (already imported in the dialog)
- `Tag` (already in both panels)
- `Dialog`, `Button`, `InputText`, `InputNumber`, `Textarea`, `Select` (all existing)

---

## Notes

- `prefilledType` on the dialog is typed as `AccommodationTypeValue` (from `accommodation-assignment.ts`) but `AccommodationType` (from `camp-edition.ts`) has the same string union values — use `AccommodationType` throughout to keep types consistent inside the dialog; no import of `AccommodationTypeValue` needed.
- The zone panel's `useCampAccommodations` instance is used **only for mutations** (delete/activate/deactivate). The `accommodations` state read from it is ignored — the source of truth for the sub-list is the `accommodations` prop passed from the page.
- `expandedRows` is `Record<string, boolean>` (PrimeVue's DataTable expects `object` or `DataTableExpandedRows`) — using `Record<string, boolean>` with `data-key="id"` works with PrimeVue 4.x's `v-model:expanded-rows`.
- The `"attach accommodations"` dialog (existing `pi pi-link` button in the zone panel) remains — it's a different workflow (linking pre-existing accommodations to a zone by ID). The new units sub-panel is for creating new units directly inside the zone.
- All user-facing text in Spanish (project standard).

---

## Next Steps After Implementation

- Frontend ticket `feat-zone-accommodation-units-frontend` PR targeting `dev`
- Remove the old "Gestionar alojamientos" (`pi pi-link`) attach dialog from `AccommodationZonePanel` if the Board confirms the new create-from-zone workflow makes it redundant (post-launch UX decision, not in scope now)

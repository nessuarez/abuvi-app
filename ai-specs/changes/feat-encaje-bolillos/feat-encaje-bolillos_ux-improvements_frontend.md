# Frontend Implementation Plan: feat-encaje-bolillos-ux-improvements

## Overview

Implements the six UX improvements defined in `feat-encaje-bolillos_ux-improvements_enriched.md` for the "Encaje de Bolillos" assignment board. All changes are within the `Camps` feature area. No new routes, no new Pinia stores — all state is local to existing composables and components.

**Depends on backend branch** `feature/feat-encaje-bolillos-ux-improvements-backend` being merged first (adds `isAssignable`, `zonePrimaryThumbnailUrl`, `zonePrimaryFileUrl` to the API response).

Architecture: Vue 3 `<script setup lang="ts">`, composable-based, PrimeVue 4 + Tailwind CSS only. No `<style>` blocks.

---

## Architecture Context

### Files to modify (no new files)

| File | Changes |
| ---- | ------- |
| `frontend/src/types/accommodation-assignment.ts` | Add `isAssignable`, `zonePrimaryThumbnailUrl`, `zonePrimaryFileUrl` to `AssignmentAccommodationResponse`; add `ACCOMMODATION_TYPE_ICONS` constant |
| `frontend/src/types/camp-edition.ts` | Add `isAssignable: boolean` to `CampEditionAccommodation` interface |
| `frontend/src/composables/useCampAccommodations.ts` | Add `toggleIsAssignable()` method |
| `frontend/src/components/camps/FamilyAssignmentCard.vue` | Member count badge; preference pills with type name; pet icon; add `accommodationTypeMap` prop |
| `frontend/src/components/camps/AccommodationSlotCard.vue` | Compact padding/font; zone photo fallback; update thumbnail logic |
| `frontend/src/components/camps/AccommodationAssignmentPanel.vue` | Type filter buttons; denser grid; zone gallery modal; `campEditionId` prop; pass `accommodationTypeMap` to cards |
| `frontend/src/views/camps/AccommodationAssignmentView.vue` | Pass `campEditionId` prop to `AccommodationAssignmentPanel` |
| `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` | `isAssignable` toggle; quantity multiplier restyled to primary badge |

### State management

No Pinia store changes. The zone gallery modal state lives entirely inside `AccommodationAssignmentPanel`. The `isAssignable` toggle calls `useCampAccommodations.toggleIsAssignable()` and updates the local `accommodations` ref optimistically.

---

## Implementation Steps

### Step 0: Create Feature Branch

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-encaje-bolillos-ux-improvements-frontend
git branch
```

> Do NOT work on the main `feat-encaje-bolillos` or backend branch. This branch is frontend only.

---

### Step 1: Update TypeScript types

#### 1a. `frontend/src/types/accommodation-assignment.ts`

**Add `ACCOMMODATION_TYPE_ICONS`** constant after `ACCOMMODATION_TYPE_LABELS` (line 12):

```typescript
export const ACCOMMODATION_TYPE_ICONS: Record<AccommodationTypeValue, string> = {
  Lodge: 'pi pi-building',
  Bungalow: 'pi pi-home',
  Motorhome: 'pi pi-car',
  Caravan: 'pi pi-truck',
  Tent: 'pi pi-sun'
}
```

**Update `AssignmentAccommodationResponse`** (lines 83–97) — add three new optional fields after `primaryFileUrl`:

```typescript
export interface AssignmentAccommodationResponse {
  id: string
  name: string
  type: AccommodationTypeValue
  capacity: number | null
  countByFamily: boolean
  zoneId: string | null
  zoneName: string | null
  sortOrder: number
  availableFeatures: string[]
  quantity: number
  unitIndex: number | null
  primaryThumbnailUrl?: string | null
  primaryFileUrl?: string | null
  zonePrimaryThumbnailUrl?: string | null   // ADD — zone primary media thumbnail
  zonePrimaryFileUrl?: string | null        // ADD — zone primary media file
  isAssignable?: boolean                    // ADD — false = excluded from assignment board
}
```

> These fields are optional (`?`) so existing tests and mock data that don't set them remain valid.

#### 1b. `frontend/src/types/camp-edition.ts`

Add `isAssignable` to `CampEditionAccommodation` interface (after `isActive`, line ~160):

```typescript
isActive: boolean
isAssignable: boolean   // ADD
sortOrder: number
```

---

### Step 2: Add `toggleIsAssignable` to `useCampAccommodations`

**File:** `frontend/src/composables/useCampAccommodations.ts`

The existing composable exposes `activateAccommodation` / `deactivateAccommodation`. Add a new method that PATCHes the accommodation's `isAssignable` field by calling the existing `PUT /api/camps/editions/accommodations/{id}` endpoint with the full updated request body.

**Implementation:** Read the current accommodation from the local `accommodations` ref and call PUT with `isAssignable` flipped. Update the local ref optimistically on success.

```typescript
const toggleIsAssignable = async (acc: CampEditionAccommodation): Promise<boolean> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.put<ApiResponse<CampEditionAccommodation>>(
      `/camps/editions/accommodations/${acc.id}`,
      {
        name: acc.name,
        accommodationType: acc.accommodationType,
        description: acc.description ?? null,
        capacity: acc.capacity ?? null,
        countByFamily: acc.countByFamily,
        quantity: acc.quantity,
        isActive: acc.isActive,
        isAssignable: !acc.isAssignable,   // toggle
        zoneId: acc.zoneId ?? null,
        sortOrder: acc.sortOrder,
      }
    )
    if (response.data.success && response.data.data) {
      const idx = accommodations.value.findIndex((a) => a.id === acc.id)
      if (idx !== -1) accommodations.value[idx] = response.data.data
      return true
    }
    return false
  } catch (err: unknown) {
    error.value =
      (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message ?? 'Error al actualizar alojamiento'
    return false
  } finally {
    loading.value = false
  }
}
```

Add `toggleIsAssignable` to the composable return object.

---

### Step 3: Update `FamilyAssignmentCard.vue`

**File:** `frontend/src/components/camps/FamilyAssignmentCard.vue`

Three targeted changes:

#### 3a — Add `accommodationTypeMap` prop and helper functions

```typescript
import { ACCOMMODATION_TYPE_LABELS, ACCOMMODATION_TYPE_ICONS } from '@/types/accommodation-assignment'
import type { AccommodationTypeValue } from '@/types/accommodation-assignment'

const props = defineProps<{
  family: AssignmentFamilyResponse
  assignedAccommodationName: string | null
  isSelected: boolean
  accommodationTypeMap: Map<string, AccommodationTypeValue>   // ADD
}>()

function prefTypeLabel(accommodationId: string): string {
  const type = props.accommodationTypeMap.get(accommodationId)
  return type ? ACCOMMODATION_TYPE_LABELS[type] : '?'
}

function prefTypeIcon(accommodationId: string): string {
  const type = props.accommodationTypeMap.get(accommodationId)
  return type ? ACCOMMODATION_TYPE_ICONS[type] : 'pi pi-question'
}
```

#### 3b — Member count: circular primary badge

Replace lines 27–29:

```html
<!-- Before -->
<span class="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
  {{ family.memberCount }} pers.
</span>

<!-- After -->
<span
  class="inline-flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full bg-primary-500 text-xs font-bold text-white"
  v-tooltip.top="`${family.memberCount} personas`"
>
  {{ family.memberCount }}
</span>
```

#### 3c — Preference pills with type icon + name

Replace lines 45–49:

```html
<!-- Before -->
<span
  v-for="pref in family.accommodationPreferences"
  :key="pref.preferenceOrder"
  class="text-xs text-gray-400"
>{{ pref.preferenceOrder }}ª</span>

<!-- After -->
<span
  v-for="pref in family.accommodationPreferences"
  :key="pref.preferenceOrder"
  class="inline-flex items-center gap-0.5 rounded-full border border-gray-200 bg-gray-50 px-1.5 py-0.5"
>
  <span class="text-[10px] font-medium text-gray-500">{{ pref.preferenceOrder }}º</span>
  <i :class="[prefTypeIcon(pref.accommodationId), 'text-[9px] text-gray-400']" />
  <span class="text-[10px] text-gray-400">{{ prefTypeLabel(pref.accommodationId) }}</span>
</span>
```

#### 3d — Pet icon: replace `pi pi-tag` with `pi pi-heart`

Replace line 37:

```html
<!-- Before -->
<i class="pi pi-tag text-xs text-amber-500" title="Tiene mascota" />

<!-- After -->
<i
  class="pi pi-heart text-xs text-amber-500"
  v-tooltip.top="'Viaja con mascota'"
  aria-label="Viaja con mascota"
/>
```

---

### Step 4: Update `AccommodationSlotCard.vue`

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

#### 4a — Zone photo fallback

The card already shows `accommodation.primaryThumbnailUrl` in the top-right corner (lines 99–109). Extend the thumbnail to fall back to `zonePrimaryThumbnailUrl` when no accommodation-specific photo exists, and add a "zona" micro-label for the fallback case:

```typescript
// Add in <script setup>
const displayThumbnail = computed(
  () => props.accommodation.primaryThumbnailUrl ?? props.accommodation.zonePrimaryThumbnailUrl ?? null
)
const thumbnailIsZoneFallback = computed(
  () => !props.accommodation.primaryThumbnailUrl && !!props.accommodation.zonePrimaryThumbnailUrl
)
```

Update the thumbnail section (lines 99–109):

```html
<div
  v-if="displayThumbnail"
  class="absolute right-2 top-2 overflow-hidden rounded-md shadow-sm"
  :class="thumbnailIsZoneFallback ? 'h-7 w-7 opacity-60' : 'h-8 w-8'"
>
  <img
    :src="displayThumbnail"
    alt=""
    class="h-full w-full object-cover"
    @error="($event.target as HTMLImageElement).style.display = 'none'"
  />
  <span
    v-if="thumbnailIsZoneFallback"
    class="absolute bottom-0 left-0 w-full bg-black/40 text-center text-[7px] text-white"
  >
    zona
  </span>
</div>
```

#### 4b — Compact card: reduce padding and name font

- Line 95: change `p-3` to `p-2`
- Line 112: change `text-sm font-semibold` to `text-xs font-semibold`

---

### Step 5: Update `AccommodationAssignmentPanel.vue`

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

#### 5a — Add `campEditionId` prop and new imports

```typescript
import { ref, computed } from 'vue'
import Dialog from 'primevue/dialog'
import Galleria from 'primevue/galleria'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import { api } from '@/utils/api'
import type { MediaItem } from '@/types/media-item'
import { ACCOMMODATION_TYPE_LABELS, ACCOMMODATION_TYPE_ICONS } from '@/types/accommodation-assignment'
import type { AccommodationTypeValue } from '@/types/accommodation-assignment'

const props = defineProps<{
  state: ProposalAssignmentStateResponse
  assignmentsMap: Map<string, { accommodationId: string; unitIndex: number | null }>
  selectedRegistrationId: string | null
  saving: boolean
  campEditionId: string   // ADD
}>()
```

#### 5b — `accommodationTypeMap` computed (pass to FamilyAssignmentCard)

Add inside `<script setup>`:

```typescript
const accommodationTypeMap = computed((): Map<string, AccommodationTypeValue> => {
  const map = new Map<string, AccommodationTypeValue>()
  props.state.accommodations.forEach((a) => map.set(a.id, a.type))
  return map
})
```

Pass it to every `FamilyAssignmentCard`:

```html
<FamilyAssignmentCard
  v-for="family in filteredFamilies"
  :key="family.registrationId"
  :family="family"
  :assigned-accommodation-name="assignedAccommodationName(family.registrationId)"
  :is-selected="family.registrationId === selectedRegistrationId"
  :accommodation-type-map="accommodationTypeMap"   // ADD
  @select="$emit('selectFamily', $event)"
/>
```

#### 5c — Replace type `Select` filter with type button bar

The existing type filter (lines 222–232) is a `Select` dropdown. Replace it with button chips for each available type.

Remove the `Select` import for type filter (keep it for zone filter). Add:

```typescript
const activeTypeFilter = ref<AccommodationTypeValue | null>(null)

const availableTypes = computed((): AccommodationTypeValue[] =>
  [...new Set(props.state.accommodations.map((a) => a.type))] as AccommodationTypeValue[]
)
```

Replace the type `Select` in the filter bar with:

```html
<div class="flex flex-wrap gap-1.5">
  <button
    class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors"
    :class="activeTypeFilter === null
      ? 'border-primary-500 bg-primary-500 text-white'
      : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'"
    @click="activeTypeFilter = null"
  >
    Todos
  </button>
  <button
    v-for="type in availableTypes"
    :key="type"
    class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors"
    :class="activeTypeFilter === type
      ? 'border-primary-500 bg-primary-500 text-white'
      : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'"
    @click="activeTypeFilter = activeTypeFilter === type ? null : type"
  >
    <i :class="ACCOMMODATION_TYPE_ICONS[type]" />
    {{ ACCOMMODATION_TYPE_LABELS[type] }}
  </button>
</div>
```

Update `groupedAccommodations` computed to apply `activeTypeFilter`:

```typescript
const groupedAccommodations = computed(() => {
  const filtered = props.state.accommodations.filter((a) => {
    const matchesType = !activeTypeFilter.value || a.type === activeTypeFilter.value
    const matchesZone = !filterZone.value || a.zoneName === filterZone.value
    const matchesAvailable = !filterOnlyAvailable.value || (
      a.capacity === null || (a.capacity - (a.countByFamily
        ? assignedFamiliesFor(a).length
        : assignedFamiliesFor(a).reduce((s, f) => s + f.memberCount, 0))) > 0
    )
    return matchesType && matchesZone && matchesAvailable
  })

  const byType = new Map<string, Map<string | null, AssignmentAccommodationResponse[]>>()
  for (const acc of filtered) {
    if (!byType.has(acc.type)) byType.set(acc.type, new Map())
    const byZone = byType.get(acc.type)!
    const key = acc.zoneName ?? null
    if (!byZone.has(key)) byZone.set(key, [])
    byZone.get(key)!.push(acc)
  }
  return byType
})
```

> Remove the old `filterType` ref and its `Select` (the new filter replaces it). Keep `filterZone` and `filterOnlyAvailable` with their existing `Select` + `ToggleSwitch`.

#### 5d — Denser accommodation grid

Line 278 — update the grid class:

```html
<!-- Before -->
<div class="grid grid-cols-2 gap-2 lg:grid-cols-3 xl:grid-cols-4">

<!-- After -->
<div class="grid grid-cols-3 gap-1.5 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6">
```

#### 5e — Zone header: use `zonePrimaryThumbnailUrl`; add "ver fotos" button

The zone header (lines 261–277) currently searches `state.accommodations` for a matching zone ID to find a thumbnail. Now that `zonePrimaryThumbnailUrl` is directly on each accommodation, simplify and add the gallery trigger.

Replace lines 261–277:

```html
<div class="mb-2 flex items-center gap-2">
  <!-- Zone thumbnail -->
  <template v-if="accommodations[0]?.zoneId">
    <img
      v-if="accommodations[0]?.zonePrimaryThumbnailUrl"
      :src="accommodations[0].zonePrimaryThumbnailUrl"
      alt=""
      class="h-7 w-10 cursor-pointer flex-shrink-0 rounded object-cover shadow-sm hover:opacity-80"
      @click="openZoneGallery(accommodations[0].zoneId!, zoneName ?? 'Zona')"
    />
    <div
      v-else
      class="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded bg-gray-100 text-gray-400"
    >
      <i class="pi pi-image" style="font-size: 0.6rem" />
    </div>
  </template>
  <h4 class="text-xs font-medium text-gray-400">{{ zoneName }}</h4>
  <!-- "Ver fotos" button — only when zone has an ID -->
  <button
    v-if="accommodations[0]?.zoneId"
    class="ml-auto flex items-center gap-1 text-[10px] text-gray-400 hover:text-primary-500"
    @click="openZoneGallery(accommodations[0].zoneId!, zoneName ?? 'Zona')"
  >
    <i class="pi pi-images text-[10px]" />
    ver fotos
  </button>
</div>
```

#### 5f — Zone gallery modal state and method

Add inside `<script setup>`:

```typescript
const zoneGalleryVisible = ref(false)
const zoneGalleryTitle = ref('')
const zoneGalleryImages = ref<MediaItem[]>([])
const zoneGalleryLoading = ref(false)

async function openZoneGallery(zoneId: string, zoneName: string): Promise<void> {
  zoneGalleryTitle.value = zoneName
  zoneGalleryVisible.value = true
  zoneGalleryLoading.value = true
  zoneGalleryImages.value = []
  try {
    const res = await api.get(
      `/camps/editions/${props.campEditionId}/accommodation-zones/${zoneId}`
    )
    zoneGalleryImages.value = res.data.data?.mediaItems ?? []
  } catch {
    // silently fail — gallery just shows empty state
  } finally {
    zoneGalleryLoading.value = false
  }
}
```

Add the gallery `Dialog` at the bottom of the template (outside the accommodation loop, before the closing `</div>`):

```html
<Dialog
  v-model:visible="zoneGalleryVisible"
  :header="zoneGalleryTitle"
  modal
  class="w-[90vw] max-w-2xl"
>
  <div v-if="zoneGalleryLoading" class="flex justify-center py-8">
    <ProgressSpinner />
  </div>
  <Galleria
    v-else-if="zoneGalleryImages.length"
    :value="zoneGalleryImages"
    :num-visible="4"
    :show-thumbnails="true"
    :show-indicators="true"
    class="w-full"
  >
    <template #item="{ item }: { item: MediaItem }">
      <img
        :src="item.fileUrl"
        :alt="item.altText ?? zoneGalleryTitle"
        class="max-h-96 w-full rounded object-contain"
        @error="($event.target as HTMLImageElement).style.display = 'none'"
      />
    </template>
    <template #thumbnail="{ item }: { item: MediaItem }">
      <img
        :src="item.thumbnailUrl ?? item.fileUrl"
        class="h-12 w-16 rounded object-cover"
        @error="($event.target as HTMLImageElement).style.display = 'none'"
      />
    </template>
  </Galleria>
  <p v-else class="py-6 text-center text-sm text-gray-400">
    Esta zona no tiene fotografías.
  </p>
</Dialog>
```

> Check `frontend/src/types/media-item.ts` for the exact field names (`fileUrl`, `thumbnailUrl`, `altText`). Adjust if the actual type uses different names.

---

### Step 6: Update `AccommodationAssignmentView.vue`

**File:** `frontend/src/views/camps/AccommodationAssignmentView.vue`

Find the `<AccommodationAssignmentPanel` usage and add the `campEditionId` prop:

```html
<AccommodationAssignmentPanel
  :state="assignmentState"
  :assignments-map="assignmentsMap"
  :selected-registration-id="selectedRegistrationId"
  :saving="saving"
  :camp-edition-id="campEditionId"   // ADD
  @select-family="selectedRegistrationId = $event"
  @assign="handleAssign"
  @unassign="unassignFamily"
/>
```

---

### Step 7: Update `CampEditionAccommodationsPanel.vue`

**File:** `frontend/src/components/camps/CampEditionAccommodationsPanel.vue`

#### 7a — Import `toggleIsAssignable` from composable

Update the composable destructuring (lines 22–29):

```typescript
const {
  accommodations,
  loading,
  error,
  fetchAccommodations,
  deleteAccommodation,
  activateAccommodation,
  deactivateAccommodation,
  toggleIsAssignable,   // ADD
} = useCampAccommodations(props.editionId)
```

Add import for `ToggleSwitch` at the top of the imports.

#### 7b — Add `handleToggleIsAssignable` handler

```typescript
const handleToggleIsAssignable = async (acc: CampEditionAccommodation) => {
  const success = await toggleIsAssignable(acc)
  if (!success) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}
```

#### 7c — Restyle quantity multiplier: from `×N` Tag to `N×` primary badge

Find the existing multiplier Tag (around line 155):

```html
<!-- Before -->
<Tag
  v-if="acc.quantity > 1"
  :value="`×${acc.quantity}`"
  severity="secondary"
  class="text-xs"
  title="Número de unidades"
/>

<!-- After -->
<span
  v-if="acc.quantity > 1"
  class="inline-flex items-center rounded bg-primary-100 px-1.5 py-0.5 text-xs font-semibold text-primary-700"
  title="Número de unidades de este tipo"
>
  {{ acc.quantity }}×
</span>
```

#### 7d — Add `isAssignable` toggle to each accommodation card

In the actions area of each card (near the `activate/deactivate` button, around line 195+), add a `ToggleSwitch` with a label:

```html
<div class="flex items-center gap-1" title="Visible en tablero de asignación">
  <ToggleSwitch
    :model-value="acc.isAssignable"
    size="small"
    @change="handleToggleIsAssignable(acc)"
  />
  <span class="text-xs text-gray-400">Asignable</span>
</div>
```

Place this before the existing action buttons group, inside the `<div class="flex items-center gap-1">` wrapper.

---

### Step 8: Verify TypeScript build and write unit tests

```bash
cd c:/repos/abuvi-app/frontend
npm run build
```

Fix any TypeScript errors before writing tests.

**File:** `frontend/src/components/camps/__tests__/FamilyAssignmentCard.test.ts`

Tests to add (extend existing or create):

```
renders_memberCount_asCircularBadge
renders_preferenceWithTypeNameAndIcon_whenTypeMapProvided
renders_preferenceWithFallback_whenTypeNotInMap
renders_petIcon_asHeartIcon_whenHasPetTrue
does_not_render_petIcon_whenHasPetFalse
```

**File:** `frontend/src/components/camps/__tests__/AccommodationSlotCard.test.ts`

Tests to add:

```
shows_zoneThumbnail_withZonaLabel_whenAccommodationHasNoPrimaryPhoto
shows_accommodationThumbnail_whenPrimaryThumbnailExists
hides_thumbnail_gracefully_onImageError
```

**File:** `frontend/src/composables/__tests__/useCampAccommodations.test.ts`

Tests to add:

```
toggleIsAssignable_callsPutEndpointWithFlippedValue
toggleIsAssignable_updatesLocalAccommodationsRef_onSuccess
toggleIsAssignable_setsError_onFailure
```

---

### Step 9: Update technical documentation

1. **`ai-specs/specs/api-spec.yml`** — no new endpoints; verify `AssignmentAccommodationResponse` schema reflects new fields if documented.
2. **`ai-specs/specs/frontend-standards.mdc`** — add note about `ACCOMMODATION_TYPE_ICONS` pattern if it establishes a new convention.
3. No routing changes.

---

## Implementation Order

1. Step 0 — Create branch `feature/feat-encaje-bolillos-ux-improvements-frontend`
2. Step 1 — Update TypeScript types (`accommodation-assignment.ts`, `camp-edition.ts`)
3. Step 2 — Add `toggleIsAssignable` to `useCampAccommodations`
4. Step 3 — Update `FamilyAssignmentCard.vue` (badge, preferences, pet icon)
5. Step 4 — Update `AccommodationSlotCard.vue` (zone fallback, compact)
6. Step 5 — Update `AccommodationAssignmentPanel.vue` (type filter buttons, denser grid, zone gallery)
7. Step 6 — Update `AccommodationAssignmentView.vue` (pass `campEditionId`)
8. Step 7 — Update `CampEditionAccommodationsPanel.vue` (isAssignable toggle, quantity badge)
9. Step 8 — TypeScript build check + unit tests
10. Step 9 — Documentation

---

## Testing Checklist

- [ ] `npm run build` passes with zero TypeScript errors
- [ ] `npm run test:unit` all pass
- [ ] `FamilyAssignmentCard` shows circular primary badge with number (no "pers." text)
- [ ] Preference pills show "1º [icon] Albergue" format in compact pill
- [ ] Pet icon is heart-style, not tag
- [ ] `AccommodationSlotCard` shows zone thumbnail fallback with "zona" label when accommodation has no own photo
- [ ] `AccommodationSlotCard` padding is tighter — at least 4–5 cards fit per row on 1440px
- [ ] Type filter buttons appear; clicking one filters the right panel; "Todos" resets
- [ ] Zone header shows "ver fotos" button; clicking opens `Galleria` dialog
- [ ] Gallery shows `ProgressSpinner` while loading; empty message if no media
- [ ] Zone gallery loads photos from the existing zone endpoint (check network tab)
- [ ] `CampEditionAccommodationsPanel` shows `isAssignable` toggle per accommodation
- [ ] Toggling `isAssignable` calls PUT endpoint; card reflects new state
- [ ] Quantity `4×` badge shown in primary blue for accommodations with `quantity > 1`

---

## Error Handling Patterns

- **Zone gallery**: API errors are silently swallowed — the dialog opens and shows the empty-state message. No toast for this since it's a secondary UX enhancement, not a critical operation.
- **`toggleIsAssignable`**: On failure, show `toast.add({ severity: 'error', ... })` in `handleToggleIsAssignable`. The local ref is NOT updated on failure (pessimistic update).
- **Broken image URLs**: All `<img>` elements that show media items or thumbnails must have `@error="($event.target as HTMLImageElement).style.display = 'none'"`.
- **Missing `accommodationTypeMap` entries**: `prefTypeLabel()` and `prefTypeIcon()` in `FamilyAssignmentCard` return `'?'` / `'pi pi-question'` as fallback.

---

## UI/UX Considerations

- **No `<style>` blocks** — all styling via Tailwind utilities only.
- **`v-tooltip`** directive is already available globally — no import needed for tooltip usage.
- **PrimeVue components used** (new imports in this ticket): `Dialog`, `Galleria`, `ToggleSwitch`, `ProgressSpinner` — verify they are globally registered in `main.ts` or import per-component.
- **`Galleria`** is a PrimeVue 4 component. Import: `import Galleria from 'primevue/galleria'`. Check that `PrimeVue/themes` include it if using auto-import.
- **`MediaItem` type**: referenced in the gallery. Find the exact type at `frontend/src/types/media-item.ts`. If fields differ from `fileUrl`/`thumbnailUrl`, adjust the template accordingly.
- **Responsive**: the denser grid (`grid-cols-3 ... 2xl:grid-cols-6`) still collapses gracefully on `md:` and below since the assignment view is board-only (desktop-first).

---

## Dependencies

No new npm packages. All PrimeVue components used are already in the project.

| Component | Already installed |
| --------- | ----------------- |
| `Dialog` | ✅ |
| `Galleria` | verify — likely ✅ |
| `ToggleSwitch` | ✅ (used in `AccommodationAssignmentPanel`) |
| `ProgressSpinner` | ✅ |
| Tailwind CSS | ✅ |

---

## Notes

- **Backend dependency**: `zonePrimaryThumbnailUrl`, `zonePrimaryFileUrl`, and `isAssignable` in `AssignmentAccommodationResponse` require the backend branch to be merged. Until then, treat them as optional (`?`) in the types so the existing assignment board still works.
- **`filterType` ref removal**: Steps 5c removes the existing `filterType` ref and its `Select`. Make sure to remove references to `filterType` throughout the file (search for `filterType` in the template and script before deleting).
- **`Galleria` empty state**: PrimeVue `Galleria` renders nothing if `:value="[]"`. Always wrap it in `v-else-if="zoneGalleryImages.length"` with a `<p v-else>` fallback.
- **Spanish UI text**: all user-visible strings in Spanish. Error messages from API already come in Spanish.
- **TypeScript strict**: No `any`. Use `unknown` with type guards. All props fully typed.
- **`v-tooltip`** directive: confirm global registration in `main.ts` before using `.top` modifier.

---

## Next Steps After Implementation

1. Create PR `feature/feat-encaje-bolillos-ux-improvements-frontend → dev`
2. Coordinate with the Board for QA — they are the primary users of the assignment board
3. After merge: the `feat-nav-improvements` frontend ticket adds sidebar shortcuts to the assignment view — coordinate with that branch if it is in progress simultaneously

---

## Implementation Verification

- **TypeScript**: `npm run build` — zero errors, zero `any`
- **Functionality**: manually test in browser with a real camp edition that has ≥ 1 zone with media, ≥ 3 accommodation types, ≥ 5 families with preferences
- **Testing**: all new Vitest unit tests pass (`npm run test:unit`)
- **Integration**: `toggleIsAssignable` hits the real PUT endpoint (verify in network tab)
- **Documentation**: `api-spec.yml` and `frontend-standards.mdc` updated if applicable

# Encaje de Bolillos — UX Improvements: Assignment Clarity, Filters, Card Design & Photos

## Summary

Five UX problems / improvements identified for the "Encaje de Bolillos" assignment interface, based on product review. These amend `feat-encaje-bolillos_enriched.md` and `feat-encaje-bolillos_frontend.md`.

---

## Improvement 1 — Remove type-level assignment; enforce specific accommodation targets

### Problem

In the current prototype and data setup, it is possible to assign a family to an entry that represents a whole accommodation type (e.g., a `CampEditionAccommodation` record named "Albergue" with capacity = total lodge beds, rather than individual rooms). This produces an assignment that says "Albergue" but conveys no concrete location — effectively useless for execution.

The system must prevent assignment to type-level accommodation entries. Assignment must target **a specific accommodation** (individual room, bungalow unit, caravan pitch, tent area, etc.) or at minimum **a zone**.

### Root cause

`CampEditionAccommodation` records can be created at any granularity. Nothing currently prevents creating a single "Albergue" record with capacity 80 to represent the whole lodge wing. When this appears in the assignment right panel alongside individual rooms, the Board may place families there and the assignment becomes meaningless.

### Solution

Introduce a boolean flag `IsAssignable` on `CampEditionAccommodation`. This flag:

- Defaults to `true` for all new records.
- Is set to `false` for type-level placeholder entries (records whose name matches an accommodation type label, or which have very high capacity without a zone).
- Is manually settable by admin.
- Controls whether the record appears as an **assignable target** in the right panel.

Non-assignable accommodations are still listed in the admin panel (for reference/capacity planning) but are greyed out and excluded from the assignment board.

#### Backend changes

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add to `CampEditionAccommodation`:

```csharp
public bool IsAssignable { get; set; } = true;
```

**File:** `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs`

```csharp
builder.Property(a => a.IsAssignable)
    .HasDefaultValue(true)
    .HasColumnName("is_assignable");
```

**Migration:**

```bash
dotnet ef migrations add AddIsAssignableToAccommodations --project src/Abuvi.API
```

Adds `is_assignable boolean NOT NULL DEFAULT TRUE` to `camp_edition_accommodations`.

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs` — DTOs

Update `CampEditionAccommodationResponse`:

```csharp
public bool IsAssignable { get; init; }
```

Update `AssignmentAccommodationResponse`:

```csharp
public record AssignmentAccommodationResponse(
    Guid Id,
    string Name,
    AccommodationType Type,
    int? Capacity,
    bool CountByFamily,
    Guid? ZoneId,
    string? ZoneName,
    string? ZonePhotoUrl,
    string? PhotoUrl,
    bool IsAssignable,    // ADD
    int SortOrder);
```

**File:** `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` — update existing `GET /assignments` query

Filter the accommodations returned in `AssignmentAccommodationResponse` to only include `IsAssignable = true` records:

```csharp
// In the assignments query — filter to assignable accommodations only
.Where(a => a.IsAssignable)
```

Non-assignable entries do not appear in the assignment board at all (they are still visible in the admin accommodations panel).

#### Frontend changes

**File:** `frontend/src/components/camps/CampEditionAccommodationsPanel.vue`

Show a "No asignable" badge on non-assignable entries, and provide a toggle button to flip `isAssignable`:

```html
<Column header="Asignable" style="width: 100px">
  <template #body="{ data }">
    <ToggleSwitch
      v-model="data.isAssignable"
      @change="toggleAssignable(data)"
    />
    <span class="text-xs text-gray-400 ml-1">
      {{ data.isAssignable ? 'Sí' : 'No' }}
    </span>
  </template>
</Column>
```

Add a banner in the assignment panel when all accommodations of a type are non-assignable:

```html
<Message
  v-if="typeHasNoAssignableAccommodations(type)"
  severity="warn"
  class="mb-2"
>
  Ningún alojamiento de tipo {{ ACCOMMODATION_TYPE_LABELS[type] }} está marcado como asignable.
  Configúralos desde la gestión de alojamientos.
</Message>
```

**File:** `frontend/src/types/accommodation-assignment.ts`

```typescript
export interface AssignmentAccommodationResponse {
  // ...existing fields...
  isAssignable: boolean      // ADD
}
```

#### Acceptance criteria

- [ ] Migration adds `is_assignable` column defaulting to `true`
- [ ] `GET /assignments` only returns accommodations with `isAssignable = true` in the assignment state
- [ ] Admin panel shows toggle per accommodation; PATCH endpoint updates `isAssignable`
- [ ] Assignment right panel shows warning when a type has zero assignable accommodations
- [ ] Non-assignable entries are greyed out and non-interactive in the admin list

---

## Improvement 2 — Filter buttons by accommodation type in the right panel

### Problem

When there are many accommodations (e.g., 40 rooms across 4 types), the right panel is long and hard to scan. The Board needs to focus on one type at a time.

### Solution

Add a horizontal filter bar above the accommodation grid with one button per accommodation type present in the current proposal. Each button shows the type icon + label. Clicking it filters the right panel to show only that type. An "All" button resets the filter.

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

Add filter state:

```typescript
const activeTypeFilter = ref<AccommodationTypeValue | null>(null)
```

Filter bar template (above the accommodation grid, inside the right panel):

```html
<!-- Type filter bar -->
<div class="flex flex-wrap gap-2 border-b bg-white px-4 py-2 sticky top-0 z-10">
  <Button
    label="Todos"
    size="small"
    :outlined="activeTypeFilter !== null"
    :severity="activeTypeFilter === null ? 'primary' : 'secondary'"
    @click="activeTypeFilter = null"
  />
  <Button
    v-for="type in availableTypes"
    :key="type"
    size="small"
    :outlined="activeTypeFilter !== type"
    :severity="activeTypeFilter === type ? 'primary' : 'secondary'"
    @click="activeTypeFilter = activeTypeFilter === type ? null : type"
  >
    <template #default>
      <i :class="ACCOMMODATION_TYPE_ICONS[type]" class="mr-1" />
      {{ ACCOMMODATION_TYPE_LABELS[type] }}
    </template>
  </Button>
</div>
```

Add constants:

```typescript
export const ACCOMMODATION_TYPE_ICONS: Record<AccommodationTypeValue, string> = {
  Lodge: 'pi pi-building',
  Bungalow: 'pi pi-home',
  Motorhome: 'pi pi-car',
  Caravan: 'pi pi-truck',
  Tent: 'pi pi-sun'
}
```

> Note: use the most appropriate PrimeIcons available. If a perfect match doesn't exist, use the closest semantic icon. Do not add a new icon library.

Filter the `groupedAccommodations` computed:

```typescript
const filteredGroupedAccommodations = computed(() => {
  if (!activeTypeFilter.value) return groupedAccommodations.value
  const filtered = new Map<string, Map<string | null, AssignmentAccommodationResponse[]>>()
  const entry = groupedAccommodations.value.get(activeTypeFilter.value)
  if (entry) filtered.set(activeTypeFilter.value, entry)
  return filtered
})
```

Use `filteredGroupedAccommodations` in the template instead of `groupedAccommodations`.

`availableTypes` computed:

```typescript
const availableTypes = computed((): AccommodationTypeValue[] =>
  [...new Set(state.accommodations.map(a => a.type))] as AccommodationTypeValue[]
)
```

#### Acceptance criteria

- [ ] Filter bar shows one button per type present in the proposal (no button for types with zero accommodations)
- [ ] Clicking a type filters the right panel to show only that type's groups/zones/cards
- [ ] Active filter button is visually distinct (filled vs outlined)
- [ ] "Todos" button deactivates the filter
- [ ] Filter state resets when switching proposals

---

## Improvement 3 — Reduce accommodation card width in the right panel

### Problem

Accommodation cards in the right panel are too wide, showing few cards per row and wasting horizontal space. The Board needs to see many rooms at a glance.

### Solution

Reduce grid columns and card padding to fit more cards per row. Target: 3 columns on `lg`, 4 on `xl`, 5 on `2xl`.

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

Update the grid class (in the zone section):

```html
<!-- Before -->
<div class="grid grid-cols-2 gap-2 lg:grid-cols-3 xl:grid-cols-4">

<!-- After -->
<div class="grid grid-cols-3 gap-1.5 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6">
```

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

Reduce padding and font sizes for a more compact card:

```html
<!-- Before: p-3 -->
<div class="rounded-lg border-2 p-3 transition-all" ...>

<!-- After: p-2 -->
<div class="rounded-lg border-2 p-2 transition-all" ...>
```

Reduce accommodation name size from `text-sm` to `text-xs font-semibold`.

Capacity counter: keep `text-xs` but reduce margin.

The photo thumbnail (from Improvement 5) should be limited to `h-14` in compact mode to avoid dominating the card.

#### Acceptance criteria

- [ ] Cards are visually compact — at least 4 cards fit per row on a 1440px screen
- [ ] Card content remains readable (name, occupancy bar, assigned family chips)

---

## Improvement 4 — FamilyAssignmentCard: clearer preferences, member count badge, pet icon

### Problem

In the left panel, preference indicators show only ordinal numbers ("1º 2º 3º") without context. A Board member cannot tell what type was preferred without memorising. Member count is shown as "6 pers." text, easy to miss. The pet flag is shown as a generic `pi-tag` icon.

### Solution

Three targeted changes to `FamilyAssignmentCard.vue`:

#### 4a — Member count as a prominent badge

Replace the plain `"{{ family.memberCount }} pers."` text with a circled badge that stands out:

```html
<!-- Before -->
<span class="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
  {{ family.memberCount }} pers.
</span>

<!-- After -->
<span
  class="inline-flex h-6 w-6 items-center justify-center rounded-full bg-primary-500 text-xs font-bold text-white"
  v-tooltip.top="family.memberCount + ' personas'"
>
  {{ family.memberCount }}
</span>
```

#### 4b — Preferences with type name label

Show "1º Tienda propia" instead of just "1º". Use a compact pill/badge for each preference so they don't take up too much space. Map `accommodationId` → accommodation type → label using the assignments state already available in the parent.

The `FamilyAssignmentCard` needs a new prop:

```typescript
defineProps<{
  family: AssignmentFamilyResponse
  assignedAccommodationName: string | null
  isSelected: boolean
  accommodationTypeMap: Map<string, AccommodationTypeValue>  // ADD: accommodationId → type
}>()
```

`accommodationTypeMap` is built once in `AccommodationAssignmentPanel` from `state.accommodations`:

```typescript
const accommodationTypeMap = computed((): Map<string, AccommodationTypeValue> => {
  const map = new Map<string, AccommodationTypeValue>()
  state.accommodations.forEach(a => map.set(a.id, a.type))
  return map
})
```

Preference rendering in the card template:

```html
<!-- Before: just ordinal numbers -->
<span
  v-for="pref in family.accommodationPreferences"
  :key="pref.preferenceOrder"
  class="text-xs text-gray-400"
>{{ pref.preferenceOrder }}ª</span>

<!-- After: ordinal + type name in a compact pill -->
<span
  v-for="pref in family.accommodationPreferences"
  :key="pref.preferenceOrder"
  class="inline-flex items-center gap-0.5 rounded-full border border-gray-200 bg-gray-50 px-1.5 py-0.5 text-xs text-gray-500"
>
  <span class="font-medium">{{ pref.preferenceOrder }}º</span>
  <i :class="typeIcon(pref.accommodationId)" class="text-[10px]" />
  <span class="text-[10px]">{{ typeLabel(pref.accommodationId) }}</span>
</span>
```

Helper methods (inside `<script setup>`):

```typescript
function typeIcon(accommodationId: string): string {
  const type = props.accommodationTypeMap.get(accommodationId)
  return type ? ACCOMMODATION_TYPE_ICONS[type] : 'pi pi-question'
}

function typeLabel(accommodationId: string): string {
  const type = props.accommodationTypeMap.get(accommodationId)
  return type ? ACCOMMODATION_TYPE_LABELS[type] : '?'
}
```

#### 4c — Pet icon: dog-like PrimeIcon

Replace `pi pi-tag` with the most dog/pet appropriate PrimeIcon (`pi pi-heart` or a contextual alternative). Add a tooltip and a colour:

```html
<!-- Before -->
<i v-if="family.hasPet" class="pi pi-tag text-amber-500" title="Mascota" />

<!-- After -->
<i
  v-if="family.hasPet"
  class="pi pi-heart-fill text-amber-500"
  v-tooltip.top="'Viaja con mascota'"
  aria-label="Viaja con mascota"
/>
```

> Use `pi pi-heart-fill` or the closest PrimeIcons 6 equivalent that suggests an animal/pet. Do not add an external icon library.

#### Acceptance criteria

- [ ] Member count is displayed as a filled circular badge (primary colour, white number)
- [ ] Each preference pill shows ordinal + type icon + type name (e.g., "1º 🏕 Tienda propia") in a compact badge
- [ ] Preference pills do not exceed one line on a 300px-wide left panel (overflow: ellipsis or wrap allowed)
- [ ] Pet icon uses a heart or paw-style icon (not a generic tag) with tooltip "Viaja con mascota"
- [ ] `accommodationTypeMap` prop is passed from `AccommodationAssignmentPanel` to all `FamilyAssignmentCard` instances

---

## Improvement 5 — Accommodation and zone photos in the assignment panel

### Problem

When deciding which specific room or zone to assign a family to, the Board has no visual reference. Photos of accommodations or zones would significantly aid decision-making (e.g., seeing that "Hab. 3" has bunks, unsuitable for elderly members).

### Context — existing photo system

**Accommodation photos already exist.** `AssignmentAccommodationResponse` already includes `primaryThumbnailUrl` and `primaryFileUrl` from the `MediaItem` system — but they are not yet displayed in `AccommodationSlotCard.vue`. No backend change needed for accommodation photos.

**Zone photos also exist** via `AccommodationZone.MediaItems`. The backend plan adds `zonePrimaryThumbnailUrl` and `zonePrimaryFileUrl` to `AssignmentAccommodationResponse` (thumbnail of the zone's primary media item, loaded via `ThenInclude` on the assignments query). See `feat-encaje-bolillos_ux-improvements_backend.md` Step 6 for details.

**Do NOT add `PhotoUrl` string columns** to any entity. All photos are managed through the existing `MediaItem` system.

### Solution — two parts

#### 5a — Accommodation thumbnail in `AccommodationSlotCard`

The accommodation thumbnail is already available in `primaryThumbnailUrl`. Show it in the card with a zone-photo fallback to `zonePrimaryThumbnailUrl`.

**File:** `frontend/src/types/accommodation-assignment.ts`

Verify / add these fields to `AssignmentAccommodationResponse` (they come from the backend plan):

```typescript
export interface AssignmentAccommodationResponse {
  // ...existing fields...
  primaryThumbnailUrl: string | null    // accommodation's own primary photo
  primaryFileUrl: string | null         // accommodation's own primary file
  zonePrimaryThumbnailUrl: string | null  // zone primary photo thumbnail (new, from backend plan)
  zonePrimaryFileUrl: string | null       // zone primary photo file (new, from backend plan)
}
```

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

Show a compact thumbnail at the top of the card. Use `primaryThumbnailUrl` for the accommodation photo; fall back to `zonePrimaryThumbnailUrl` if the accommodation has no own photo. Photo is expandable on click:

```typescript
const photoExpanded = ref(false)

const displayPhoto = computed(
  () => props.accommodation.primaryThumbnailUrl ?? props.accommodation.zonePrimaryThumbnailUrl ?? null
)

const photoIsZoneFallback = computed(
  () => !props.accommodation.primaryThumbnailUrl && !!props.accommodation.zonePrimaryThumbnailUrl
)
```

```html
<div v-if="displayPhoto" class="mb-1.5">
  <img
    :src="displayPhoto"
    :alt="accommodation.name"
    class="w-full cursor-zoom-in rounded object-cover transition-all"
    :class="photoExpanded ? 'h-32' : 'h-14'"
    @click.stop="photoExpanded = !photoExpanded"
    @error="($event.target as HTMLImageElement).style.display = 'none'"
  />
  <span v-if="photoIsZoneFallback" class="text-[10px] text-gray-300 italic">zona</span>
</div>
```

#### 5b — Zone photo gallery modal triggered from zone header

The zone header in the right panel shows a thumbnail of the zone's primary photo. A "ver fotos" button opens a `Galleria` dialog showing **all** media items for that zone. Photos are loaded on demand via the existing `GET /api/camps/editions/{campEditionId}/accommodation-zones/{zoneId}` endpoint (already returns `MediaItems` in `AccommodationZoneResponse`).

**No new backend endpoint required.**

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

Add state for the zone gallery modal:

```typescript
import { api } from '@/utils/api'
import type { MediaItemResponse } from '@/types/media'

const zoneGalleryVisible = ref(false)
const zoneGalleryTitle = ref('')
const zoneGalleryImages = ref<MediaItemResponse[]>([])
const zoneGalleryLoading = ref(false)

async function openZoneGallery(zoneId: string, zoneName: string): Promise<void> {
  zoneGalleryTitle.value = zoneName
  zoneGalleryVisible.value = true
  zoneGalleryLoading.value = true
  try {
    const res = await api.get(`/camps/editions/${props.campEditionId}/accommodation-zones/${zoneId}`)
    zoneGalleryImages.value = res.data.data?.mediaItems ?? []
  } finally {
    zoneGalleryLoading.value = false
  }
}
```

> `props.campEditionId` must be passed to `AccommodationAssignmentPanel` from `AccommodationAssignmentView`. Add it as a prop:

```typescript
defineProps<{
  state: ProposalAssignmentStateResponse
  assignmentsMap: Map<string, string>
  selectedRegistrationId: string | null
  saving: boolean
  campEditionId: string   // ADD
}>()
```

Zone sub-header template (inside the right panel's zone loop):

```html
<div class="flex items-center gap-2 mb-2">
  <!-- Zone primary thumbnail -->
  <img
    v-if="zonePrimaryThumbnail(acc.zoneId)"
    :src="zonePrimaryThumbnail(acc.zoneId)"
    class="h-8 w-12 cursor-pointer rounded object-cover hover:opacity-80"
    :alt="zoneName"
    @click="openZoneGallery(acc.zoneId!, zoneName ?? 'Zona')"
  />
  <h4 class="text-xs font-medium text-gray-400">{{ zoneName ?? 'Sin zona' }}</h4>
  <!-- View all photos button — only if zone has a zoneId -->
  <button
    v-if="acc.zoneId"
    class="ml-auto flex items-center gap-1 text-[10px] text-gray-400 hover:text-primary-500"
    @click="openZoneGallery(acc.zoneId, zoneName ?? 'Zona')"
  >
    <i class="pi pi-images text-[10px]" />
    ver fotos
  </button>
</div>
```

Helper computed (map zoneId → primary thumbnail URL, derived once from all accommodations):

```typescript
const zoneThumbnailMap = computed((): Map<string, string> => {
  const map = new Map<string, string>()
  for (const acc of state.accommodations) {
    if (acc.zoneId && acc.zonePrimaryThumbnailUrl && !map.has(acc.zoneId)) {
      map.set(acc.zoneId, acc.zonePrimaryThumbnailUrl)
    }
  }
  return map
})

function zonePrimaryThumbnail(zoneId: string | null): string | null {
  return zoneId ? (zoneThumbnailMap.value.get(zoneId) ?? null) : null
}
```

Zone gallery modal (add once at the bottom of the template, outside the accommodation loop):

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
    <template #item="{ item }">
      <img
        :src="item.fileUrl"
        :alt="item.altText ?? zoneGalleryTitle"
        class="max-h-96 w-full rounded object-contain"
      />
    </template>
    <template #thumbnail="{ item }">
      <img
        :src="item.thumbnailUrl ?? item.fileUrl"
        class="h-14 w-20 rounded object-cover"
      />
    </template>
  </Galleria>
  <p v-else class="py-6 text-center text-sm text-gray-400">
    Esta zona no tiene fotografías.
  </p>
</Dialog>
```

> `Galleria` is already available in PrimeVue 4.x. Import from `primevue/galleria`.

#### Acceptance criteria

- [ ] `AccommodationSlotCard` shows `primaryThumbnailUrl` when the accommodation has its own photo
- [ ] `AccommodationSlotCard` falls back to `zonePrimaryThumbnailUrl` when accommodation has no photo; shows "zona" micro-label
- [ ] Photo in slot card expands/collapses on click; broken URLs are gracefully hidden
- [ ] Zone header shows a small thumbnail when `zonePrimaryThumbnailUrl` is available
- [ ] Zone header shows "ver fotos" button only when `zoneId` is not null
- [ ] Clicking thumbnail or "ver fotos" opens the `Galleria` dialog with all zone media items loaded on demand
- [ ] Dialog shows `ProgressSpinner` while loading; shows empty message if zone has no media
- [ ] No new backend endpoint required — uses existing `GET /accommodation-zones/{zoneId}`

---

## Improvement 6 — Quantity multiplier badge in the accommodation admin table

### Problem

In `CampEditionAccommodationsPanel.vue`, when an accommodation has `Quantity > 1` (e.g., 4 identical bungalows of the same type defined as a single record), there is no visible indicator of this. The Board cannot tell at a glance that "Bungalow Norte" represents 4 units.

### Solution

Add a **"Cantidad"** column to the accommodation DataTable showing a compact multiplier badge: `2×`, `3×`, etc. When `Quantity = 1` the column shows `—` (a single unit needs no multiplier). The `quantity` field is already present in `CampEditionAccommodationResponse` — no backend change required.

**File:** `frontend/src/components/camps/CampEditionAccommodationsPanel.vue`

Add a column after the "Nombre" column:

```html
<Column header="Cantidad" style="width: 80px">
  <template #body="{ data }">
    <span
      v-if="data.quantity > 1"
      class="inline-flex items-center rounded bg-primary-100 px-1.5 py-0.5 text-xs font-semibold text-primary-700"
    >
      {{ data.quantity }}×
    </span>
    <span v-else class="text-xs text-gray-300">—</span>
  </template>
</Column>
```

#### Acceptance criteria

- [ ] "Cantidad" column appears in the accommodation admin table
- [ ] Accommodations with `quantity > 1` show a filled badge (e.g., `4×`) in primary colour
- [ ] Accommodations with `quantity = 1` show `—` in muted grey
- [ ] No backend change required — `quantity` is already in `CampEditionAccommodationResponse`

---

## Combined files to create / modify

### Backend

| Action | File |
| ------ | ---- |
| Modify | `src/Abuvi.API/Features/Camps/CampsModels.cs` — `IsAssignable` on `CampEditionAccommodation` entity + 3 DTOs; `ZonePrimaryThumbnailUrl`/`ZonePrimaryFileUrl` on `AssignmentAccommodationResponse` |
| Modify | `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs` — `is_assignable` column |
| Modify | `src/Abuvi.API/Features/Camps/CampEditionAccommodationsService.cs` — map `IsAssignable` in `ToResponse` + update handler |
| Modify | `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs` — filter `IsAssignable = true`; `ThenInclude` zone media; populate zone photo fields |
| Create | `src/Abuvi.API/Migrations/<ts>_AddIsAssignableToAccommodations.cs` |

### Frontend

| Action | File |
| ------ | ---- |
| Modify | `frontend/src/types/accommodation-assignment.ts` — `isAssignable` on `AssignmentAccommodationResponse`; `zonePrimaryThumbnailUrl`/`zonePrimaryFileUrl` on `AssignmentAccommodationResponse`; correct field names (`primaryThumbnailUrl` not `photoUrl`) |
| Modify | `frontend/src/components/camps/AccommodationAssignmentPanel.vue` — type filter bar; compact grid; `accommodationTypeMap` prop; `campEditionId` prop; zone gallery modal + state; `zoneThumbnailMap` computed |
| Modify | `frontend/src/components/camps/AccommodationSlotCard.vue` — compact padding/font; photo thumbnail using `primaryThumbnailUrl`/`zonePrimaryThumbnailUrl` |
| Modify | `frontend/src/components/camps/FamilyAssignmentCard.vue` — member count badge; preference pills with type name; pet icon |
| Modify | `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` — `isAssignable` toggle column; grouped by type; `Cantidad` multiplier badge column |
| Modify | `frontend/src/views/camps/AccommodationAssignmentView.vue` — pass `campEditionId` prop to `AccommodationAssignmentPanel` |

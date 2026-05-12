# Frontend Implementation Plan: feat-accommodation-media — Accommodation Media

## Overview

Add photo/multimedia management to accommodation zones, individual accommodations, and accommodation type defaults. Surface primary thumbnails in the room-assignment board ("encaje de bolillos") for visual reference when assigning families.

Architecture: Vue 3 Composition API with `<script setup lang="ts">`, composable-based API communication, PrimeVue components, Tailwind CSS. No Pinia store needed — all state is local/composable-scoped. No new routes required.

---

## Architecture Context

### New files
| File | Purpose |
|---|---|
| `frontend/src/types/accommodation-media.ts` | TypeScript types for media items and requests |
| `frontend/src/composables/useAccommodationMedia.ts` | API CRUD composable for zone/accommodation/type media |
| `frontend/src/components/camps/AccommodationMediaGallery.vue` | Read-only thumbnail strip (used everywhere media is displayed) |
| `frontend/src/components/camps/AccommodationMediaManager.vue` | Admin upload/delete/primary management panel |

### Modified files
| File | Change |
|---|---|
| `frontend/src/types/blob-storage.ts` | Add `'accommodation-media'` to `BlobFolder` union |
| `frontend/src/types/accommodation-assignment.ts` | Add `primaryThumbnailUrl?: string` and `primaryFileUrl?: string` to zone/accommodation response types |
| `frontend/src/components/camps/AccommodationZonePanel.vue` | Embed `AccommodationMediaManager` (admin) and `AccommodationMediaGallery` (view) per zone |
| `frontend/src/components/camps/CampEditionAccommodationDialog.vue` | Embed `AccommodationMediaManager` (admin) + `AccommodationMediaGallery` (view) for the accommodation |
| `frontend/src/components/camps/AccommodationSlotCard.vue` | Show primary thumbnail in card header |
| `frontend/src/components/camps/AccommodationAssignmentPanel.vue` | Show zone primary thumbnail in zone group headers |

### State management
No Pinia store. `useAccommodationMedia` manages its own reactive state per component instance. The primary thumbnail fields (`primaryThumbnailUrl`, `primaryFileUrl`) flow through existing zone/accommodation response types.

---

## Implementation Steps

### Step 0 — Create Feature Branch

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-accommodation-media-frontend
git branch
```

> All code changes start from this branch. Do not work on `dev` directly.

---

### Step 1 — Update `BlobFolder` Type

**File:** `frontend/src/types/blob-storage.ts`

Add `'accommodation-media'` to the `BlobFolder` union type.

Before:
```typescript
export type BlobFolder = 'photos' | 'media-items' | 'camp-locations' | 'camp-photos' | 'payment-proofs' | 'profile-photos'
```

After:
```typescript
export type BlobFolder = 'photos' | 'media-items' | 'camp-locations' | 'camp-photos' | 'payment-proofs' | 'profile-photos' | 'accommodation-media'
```

---

### Step 2 — Define TypeScript Types

**File:** `frontend/src/types/accommodation-media.ts`

```typescript
export type AccommodationMediaOwnerType = 'zone' | 'accommodation' | 'type'

export interface AccommodationMediaItem {
  id: string
  fileUrl: string
  thumbnailUrl: string | null
  title: string
  caption: string | null
  displayOrder: number
  isPrimary: boolean
  type: string          // MediaItemType: 'Photo' | 'Video' etc.
  createdAt: string
}

export interface AddAccommodationMediaRequest {
  fileUrl: string
  thumbnailUrl: string | null
  type: string
  title: string
  caption: string | null
  displayOrder?: number
}

export interface AccommodationTypeMediaItem {
  id: string
  accommodationType: string
  fileUrl: string
  thumbnailUrl: string | null
  caption: string | null
  displayOrder: number
  isPrimary: boolean
  createdAt: string
}

export interface AddAccommodationTypeMediaRequest {
  accommodationType: string
  fileUrl: string
  thumbnailUrl: string | null
  caption: string | null
  displayOrder?: number
}

export const ACCOMMODATION_TYPE_VALUES = ['Lodge', 'Caravan', 'Tent', 'Bungalow', 'Motorhome'] as const
export type AccommodationTypeValue = typeof ACCOMMODATION_TYPE_VALUES[number]
```

---

### Step 3 — Update Accommodation Assignment Types

**File:** `frontend/src/types/accommodation-assignment.ts`

Find `AccommodationZoneResponse` interface (or type) and add:
```typescript
primaryThumbnailUrl?: string | null
primaryFileUrl?: string | null
```

Find `AssignmentAccommodationResponse` interface and add the same two optional fields.

These fields are populated by the updated backend queries and will be non-null when a primary media item has been set.

---

### Step 4 — Create `useAccommodationMedia` Composable

**File:** `frontend/src/composables/useAccommodationMedia.ts`

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AccommodationMediaItem,
  AddAccommodationMediaRequest,
  AccommodationTypeMediaItem,
  AddAccommodationTypeMediaRequest,
} from '@/types/accommodation-media'

export function useAccommodationMedia() {
  const items = ref<AccommodationMediaItem[]>([])
  const typeItems = ref<AccommodationTypeMediaItem[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  // ── Zone media ──────────────────────────────────────────────────────────────

  async function fetchZoneMedia(editionId: string, zoneId: string) { ... }
  async function addZoneMedia(editionId: string, zoneId: string, request: AddAccommodationMediaRequest) { ... }
  async function deleteZoneMedia(editionId: string, zoneId: string, mediaId: string) { ... }
  async function setZonePrimary(editionId: string, zoneId: string, mediaId: string) { ... }

  // ── Accommodation media ─────────────────────────────────────────────────────

  async function fetchAccommodationMedia(editionId: string, accommodationId: string) { ... }
  async function addAccommodationMedia(editionId: string, accommodationId: string, request: AddAccommodationMediaRequest) { ... }
  async function deleteAccommodationMedia(editionId: string, accommodationId: string, mediaId: string) { ... }
  async function setAccommodationPrimary(editionId: string, accommodationId: string, mediaId: string) { ... }

  // ── Type default media ──────────────────────────────────────────────────────

  async function fetchTypeMedia(type?: string) { ... }
  async function addTypeMedia(type: string, request: AddAccommodationTypeMediaRequest) { ... }
  async function deleteTypeMedia(mediaId: string) { ... }
  async function setTypePrimary(mediaId: string) { ... }

  return {
    items, typeItems, loading, error,
    fetchZoneMedia, addZoneMedia, deleteZoneMedia, setZonePrimary,
    fetchAccommodationMedia, addAccommodationMedia, deleteAccommodationMedia, setAccommodationPrimary,
    fetchTypeMedia, addTypeMedia, deleteTypeMedia, setTypePrimary,
  }
}
```

**Implementation notes for each method:**
- All methods set `loading.value = true` at the start and `false` in `finally`.
- `error.value = null` at the start; set to Spanish message on catch.
- `fetchZone/AccommodationMedia` → `GET /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media` and corresponding accommodation URL. Assigns to `items.value`.
- `addZoneMedia` → `POST` to the same path. Pushes returned item into `items.value`.
- `deleteZoneMedia` → `DELETE .../media/{mediaId}`. Filters item out of `items.value`.
- `setZonePrimary` → `PATCH .../media/{mediaId}/primary`. Locally updates `items.value`: sets `isPrimary = false` for all, then `true` for the given id (optimistic update).
- `fetchTypeMedia(type?)` → `GET /api/accommodation-types/media` or `GET /api/accommodation-types/{type}/media`. Assigns to `typeItems.value`.
- Error messages in Spanish: `'Error al cargar los archivos'`, `'Error al añadir el archivo'`, `'Error al eliminar el archivo'`, `'Error al establecer como principal'`.

---

### Step 5 — Create `AccommodationMediaGallery` Component

**File:** `frontend/src/components/camps/AccommodationMediaGallery.vue`

**Props:**
```typescript
interface Props {
  items: AccommodationMediaItem[]
  loading?: boolean
}
```

**Behaviour:**
- Horizontal scrollable strip of thumbnail images.
- Primary item is displayed first (sort by `isPrimary desc, displayOrder asc` in the template).
- Each thumbnail: 80×80px (`w-20 h-20`), `object-cover`, rounded (`rounded-md`).
- Primary item gets a ring highlight: `ring-2 ring-primary-500`.
- Click on a thumbnail opens it full-size using PrimeVue `Image` component with `preview` prop.
- If no items, show nothing (empty component, no placeholder text — the manager handles empty state).
- Show `ProgressSpinner` (small, `w-6 h-6`) while loading.

**Template structure:**
```html
<div v-if="loading" class="flex items-center gap-2 py-2">
  <ProgressSpinner class="w-6 h-6" />
</div>
<div v-else-if="sortedItems.length > 0" class="flex gap-2 overflow-x-auto py-2">
  <div v-for="item in sortedItems" :key="item.id" class="relative flex-shrink-0">
    <Image
      :src="item.thumbnailUrl ?? item.fileUrl"
      :preview="true"
      :preview-src="item.fileUrl"
      class="w-20 h-20 object-cover rounded-md"
      :class="{ 'ring-2 ring-primary-500': item.isPrimary }"
      alt=""
    />
  </div>
</div>
```

**Computed:**
```typescript
const sortedItems = computed(() =>
  [...props.items].sort((a, b) => {
    if (a.isPrimary !== b.isPrimary) return a.isPrimary ? -1 : 1
    return a.displayOrder - b.displayOrder
  })
)
```

---

### Step 6 — Create `AccommodationMediaManager` Component

**File:** `frontend/src/components/camps/AccommodationMediaManager.vue`

**Props:**
```typescript
interface Props {
  ownerType: 'zone' | 'accommodation' | 'type'
  ownerId: string           // zoneId, accommodationId, or AccommodationTypeValue
  editionId?: string        // required for zone/accommodation, omitted for type
  readonly?: boolean        // default false — hides controls, shows gallery only
}
```

**Emits:** none (self-contained)

**Internal state:**
- `items` — loaded from composable on `onMounted`
- `uploading` — boolean during upload
- `MAX_ITEMS = 10`

**Template layout (admin mode, `readonly = false`):**
```
┌────────────────────────────────────────────────┐
│ Fotos y multimedia            [2/10] [+ Añadir] │
│ ┌──────────────────────────────────────────┐   │
│ │ [thumb1*] [thumb2] [thumb3]  ← gallery   │   │
│ │  ☆ ✕      ✕        ✕                    │   │
│ └──────────────────────────────────────────┘   │
└────────────────────────────────────────────────┘
```

- The `[+ Añadir]` button is disabled when `items.length >= MAX_ITEMS`.
- Clicking `[+ Añadir]` opens a PrimeVue `FileUpload` in `auto` mode (or triggers a hidden `<input type="file">`).

**Upload flow (two-step):**
1. User picks file → call `useBlobStorage().uploadFile({ file, folder: 'accommodation-media', contextId: ownerId, generateThumbnail: true })`
2. On success → call composable `addZoneMedia / addAccommodationMedia / addTypeMedia` with the returned URLs.
3. On error → toast `'Error al subir el archivo. Por favor, inténtalo de nuevo.'`

**Per-thumbnail controls (shown on hover, `absolute` positioned):**
- `☆ / ★` button — star outline if not primary, filled if primary. Click → `setZonePrimary / setAccommodationPrimary / setTypePrimary`. Tooltip: `Establecer como principal`.
- `✕` button — delete. Show PrimeVue `ConfirmDialog` with message `'¿Eliminar este archivo?'`. On confirm → `deleteZoneMedia / deleteAccommodationMedia / deleteTypeMedia`.

**File type filter for upload:** images (`.jpg,.jpeg,.png,.webp,.gif`) and videos (`.mp4,.mov,.avi,.webm`). Show error toast for unsupported types before upload.

**Max size guard:** if file > 50 MB, show toast `'El archivo es demasiado grande. El tamaño máximo es 50 MB.'` without uploading.

**Count badge:** `({{ items.length }}/{{ MAX_ITEMS }})` shown next to section title in gray text.

**Readonly mode (`readonly = true`):** only show `AccommodationMediaGallery` — hide controls, upload button, and per-item action buttons.

**PrimeVue components used:** `Button`, `ConfirmDialog`, `useConfirm`, `useToast`, `ProgressSpinner`.

---

### Step 7 — Update `AccommodationZonePanel.vue`

**File:** `frontend/src/components/camps/AccommodationZonePanel.vue`

**Location of change:** Inside the row expansion or zone detail section — after the zone's feature assignment block, add the media manager/gallery.

**Implementation steps:**

1. Import `AccommodationMediaManager` and `AccommodationMediaGallery`.
2. Determine user role: use `useAuthStore()` → `isAdmin` or `isBoard` computed.
3. In the zone detail/expansion template, add:

```html
<!-- Media section per zone -->
<div class="mt-4">
  <AccommodationMediaManager
    v-if="isAdmin || isBoard"
    owner-type="zone"
    :owner-id="zone.id"
    :edition-id="campEditionId"
  />
  <AccommodationMediaGallery
    v-else
    :items="zone.mediaItems ?? []"
  />
</div>
```

4. The `zone.mediaItems` field already exists on `AccommodationZoneResponse` (see existing types). It will be populated when the zone detail is loaded.

> **Note:** `AccommodationMediaManager` fetches its own items internally on mount, so no prop-drilling of media items is needed for the manager. The `AccommodationMediaGallery` in readonly mode uses `zone.mediaItems` from the parent's zone data.

---

### Step 8 — Update `CampEditionAccommodationDialog.vue`

**File:** `frontend/src/components/camps/CampEditionAccommodationDialog.vue`

**Change:** Add a media section at the bottom of the dialog form, visible only when editing an existing accommodation (not when creating new — `accommodation` prop is non-null).

**Implementation steps:**

1. Import `AccommodationMediaManager`.
2. Use `useAuthStore()` for role check.
3. Add a section after the last form field (before the dialog footer buttons):

```html
<Divider v-if="accommodation" />
<div v-if="accommodation" class="pt-2">
  <p class="mb-2 text-sm font-medium text-gray-700">Fotos y multimedia</p>
  <AccommodationMediaManager
    owner-type="accommodation"
    :owner-id="accommodation.id"
    :edition-id="editionId"
    :readonly="!(isAdmin || isBoard)"
  />
</div>
```

4. The dialog already has `editionId` and optional `accommodation` props — no new props needed.

---

### Step 9 — Update `AccommodationSlotCard.vue`

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

The `AssignmentAccommodationResponse` type now has `primaryThumbnailUrl?: string | null`. The slot card receives the accommodation object.

**Changes:**

1. In the card's header area (top section), add a small thumbnail if available:

```html
<!-- Primary thumbnail — top-right corner of card -->
<div
  v-if="accommodation.primaryThumbnailUrl"
  class="absolute right-2 top-2 h-8 w-8 overflow-hidden rounded-md shadow-sm"
>
  <img
    :src="accommodation.primaryThumbnailUrl"
    alt=""
    class="h-full w-full object-cover"
  />
</div>
```

2. Add `relative` to the card's root element class if not already present (to anchor the absolute thumbnail).

3. No new props needed — `accommodation` already contains the primary thumbnail from the updated backend response.

---

### Step 10 — Update `AccommodationAssignmentPanel.vue`

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

`AccommodationZoneResponse` now has `primaryThumbnailUrl`. The assignment panel groups accommodations by zone and renders zone headers.

**Change:** In the zone header/group header section, add the zone thumbnail:

```html
<!-- Zone header -->
<div class="flex items-center gap-3 py-2">
  <img
    v-if="zone.primaryThumbnailUrl"
    :src="zone.primaryThumbnailUrl"
    alt=""
    class="h-10 w-10 flex-shrink-0 rounded-md object-cover shadow-sm"
  />
  <div
    v-else
    class="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-md bg-gray-100 text-gray-400"
  >
    <i class="pi pi-image text-sm" />
  </div>
  <span class="font-semibold text-gray-800">{{ zone.name }}</span>
  <!-- ... rest of zone header ... -->
</div>
```

The fallback icon (`pi-image`) shows when no primary thumbnail has been set.

---

### Step 11 — Write Unit Tests

**File:** `frontend/src/composables/__tests__/useAccommodationMedia.test.ts`

```typescript
describe('useAccommodationMedia', () => {
  // Zone media
  it('should fetch zone media and populate items')
  it('should add zone media and push to items array')
  it('should delete zone media and remove from items array')
  it('should set zone primary — update all isPrimary flags optimistically')
  it('should set error message in Spanish on fetch failure')
  it('should set error message in Spanish on add failure')

  // Accommodation media
  it('should fetch accommodation media and populate items')
  it('should add accommodation media and push to items array')
  it('should delete accommodation media and remove from items array')
  it('should set accommodation primary — update all isPrimary flags optimistically')

  // Type media
  it('should fetch all type media')
  it('should add type media')
  it('should delete type media')
  it('should set type primary')
})
```

All tests mock `api` from `@/utils/api` using `vi.mock`. Use `vi.mocked(api.get).mockResolvedValue(...)` pattern. Follow AAA.

**File:** `frontend/src/components/camps/__tests__/AccommodationMediaGallery.test.ts`

```typescript
describe('AccommodationMediaGallery', () => {
  it('should render thumbnails sorted by isPrimary then displayOrder')
  it('should apply ring highlight to primary item')
  it('should show spinner when loading is true')
  it('should render nothing when items array is empty and not loading')
})
```

---

### Step 12 — Update Technical Documentation

1. **`ai-specs/specs/api-spec.yml`** (if it exists): Add the new media endpoint paths for zones, accommodations, and types.
2. **No frontend-standards changes needed** — no new patterns or libraries introduced; `useBlobStorage` and two-step upload flow are already established patterns.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Update `BlobFolder` type (5 min)
3. Step 2 — Define `accommodation-media.ts` types
4. Step 3 — Add `primaryThumbnailUrl`/`primaryFileUrl` to accommodation assignment types
5. Step 4 — Create `useAccommodationMedia` composable
6. Step 5 — Create `AccommodationMediaGallery` component
7. Step 6 — Create `AccommodationMediaManager` component
8. Step 7 — Update `AccommodationZonePanel.vue`
9. Step 8 — Update `CampEditionAccommodationDialog.vue`
10. Step 9 — Update `AccommodationSlotCard.vue`
11. Step 10 — Update `AccommodationAssignmentPanel.vue`
12. Step 11 — Write unit tests
13. Step 12 — Update documentation

---

## Testing Checklist

- [ ] `npx vue-tsc --noEmit` passes with zero errors
- [ ] `npx vitest` passes — all composable and component tests green
- [ ] Admin can open zone panel and see the "Fotos y multimedia" section
- [ ] Admin can upload an image file — thumbnail appears in the gallery strip
- [ ] Admin cannot upload an 11th file (button disabled + count shows 10/10)
- [ ] Clicking the ★ button sets a new primary; previous primary loses its ring highlight
- [ ] Clicking ✕ shows confirm dialog; on confirm the thumbnail is removed
- [ ] Non-admin (Member) user sees gallery in read-only mode — no upload/delete buttons
- [ ] Opening an accommodation edit dialog shows the media manager at the bottom
- [ ] Assignment board slot cards show a small thumbnail for accommodations with a primary image
- [ ] Assignment board zone headers show the zone's primary thumbnail (or fallback icon)
- [ ] Uploading a file > 50 MB shows Spanish toast error (no API call made)
- [ ] Uploading an unsupported file type shows Spanish toast error

---

## Error Handling Patterns

All errors surface via PrimeVue `useToast`:

```typescript
toast.add({
  severity: 'error',
  summary: 'Error',
  detail: error.value ?? 'Ocurrió un error inesperado',
  life: 5000
})
```

The composable sets `error.value` in Spanish for all failure cases. Components watch `error` and display toast when it becomes non-null.

Upload-specific errors caught before API calls (file size, file type) also show toasts directly in the manager component without going through the composable.

---

## UI/UX Considerations

- **Gallery strip**: `overflow-x-auto` horizontal scroll — works on mobile without taking vertical space.
- **Thumbnail size**: 80×80px (`w-20 h-20`) in the zone panel and accommodation dialog. 32×32px (`w-8 h-8`) in the assignment slot card.
- **Primary indicator**: `ring-2 ring-primary-500` on primary thumbnail; star icon (filled `pi-star-fill` vs outline `pi-star`) for the set-primary action.
- **Responsive**: The media manager shows a single row of thumbnails on all screen sizes — no grid needed.
- **Accessibility**: All `<img>` elements use `alt=""` (decorative images). Interactive thumbnails have `aria-label="Ver imagen"`. Upload button has aria-label `"Subir archivo"`.
- **Loading feedback**: `ProgressSpinner` shown while upload is in progress; the upload button is disabled during upload.
- **Empty state in manager**: When no media uploaded yet, show subtle text `"Sin archivos. Haz clic en '+ Añadir' para subir fotos."` in gray.
- **PrimeVue `ConfirmDialog`**: must be included once in the parent page (or global layout) — check if it already exists before adding it again.

---

## Dependencies

No new npm packages required. All dependencies already exist:
- `primevue` — `Button`, `Image`, `ProgressSpinner`, `ConfirmDialog`, `Divider`, `useConfirm`, `useToast`
- `@/composables/useBlobStorage` — already handles file upload to S3

---

## Notes

- **Two-step upload** (existing pattern): call `POST /api/blobs/upload` first to get the URLs, then call the media endpoint. Do NOT try to send the file directly to the media endpoint.
- **`accommodation-media` folder**: use `folder: 'accommodation-media'` and `contextId: ownerId` in the blob upload call for organized bucket storage.
- **`generateThumbnail: true`**: always pass this — thumbnails are used for the 80×80 and 32×32 display sizes.
- **Optimistic primary update**: when setting primary, update `items.value` locally immediately (don't wait for re-fetch). This avoids a visible flicker in the gallery.
- **Backend must be deployed first**: the composable endpoints (`/api/camps/editions/.../media`, etc.) must exist before the frontend can be tested. Coordinate with the backend ticket deployment.
- **All user-facing text in Spanish**: labels, toast messages, confirm dialogs, empty-state text.
- **TypeScript strict**: no `any`, all composable return types inferred or explicitly typed, all component props typed with interfaces.
- **No `<style>` blocks**: use Tailwind utility classes exclusively.
- **`v-if="accommodation"` guard**: the media manager in `CampEditionAccommodationDialog` is only shown when editing (not creating) because a new accommodation has no ID yet.

---

## Next Steps After Implementation

- Coordinate frontend merge with the backend feature branch merge — the two should be merged together or the frontend after the backend.
- Optionally: add onboarding tour steps for the media upload feature (using the existing Driver.js onboarding system — add `data-onboarding` attributes to the upload button and primary-toggle).
- Consider adding type-default media management UI to the existing `AccommodationFeaturesCataloguePanel.vue` or a dedicated admin settings section (currently out of scope but the composable is ready for it).

---

## Implementation Verification

- [ ] **Code quality:** `npx vue-tsc --noEmit` zero errors; no `any`; all components use `<script setup lang="ts">`; no `<style>` blocks
- [ ] **Functionality:** upload → thumbnail appears → set primary → ring shown → delete → thumbnail gone
- [ ] **Testing:** Vitest passes; composable tests cover all 14 cases; component tests cover gallery rendering
- [ ] **Integration:** composable `items.value` reflects server state after each mutation; `primaryThumbnailUrl` visible in assignment board
- [ ] **Documentation:** api-spec updated
- [ ] **Language:** all user-facing strings in Spanish; code/variables in English

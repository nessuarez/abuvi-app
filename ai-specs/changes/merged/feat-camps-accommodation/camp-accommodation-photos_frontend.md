# Frontend Implementation Plan: feat-camps-accommodation — Accommodation Capacity & Photo Management

**Source spec:** [camp-accommodation-photos_enriched.md](./camp-accommodation-photos_enriched.md)
**Branch:** `feature/feat-camps-accommodation-photos-frontend`
**Date:** 2026-02-18

---

## 1. Overview

This plan covers the frontend implementation for the accommodation capacity and manual photo management feature in the Camps domain.

**Two capabilities to implement:**

1. **Accommodation capacity** — Display and form editing of a structured JSON payload (`AccommodationCapacity`) embedded in `Camp` and `CampEdition`, including a calculated total bed capacity.
2. **Manual photo management** — Full CRUD + reorder + set-primary gallery for camp photos, backed by new `/api/camps/{campId}/photos` endpoints.

Architecture: Vue 3 Composition API (`<script setup lang="ts">`), composable-based API calls, no new Pinia store (local composable state is sufficient), PrimeVue + Tailwind CSS, TypeScript strict typing.

---

## 2. Architecture Context

### Files to modify

| File | Change |
|------|--------|
| `frontend/src/types/camp.ts` | Add `AccommodationCapacity`, `SharedRoomInfo`; update `Camp`, `CreateCampRequest`, `UpdateCampRequest` |
| `frontend/src/types/camp-edition.ts` | Update `CampEdition`, `ProposeCampEditionRequest` with accommodation fields |
| `frontend/src/composables/useCampEditions.ts` | Pass `accommodationCapacity` in `proposeEdition` |
| `frontend/src/components/camps/CampLocationForm.vue` | Add `AccommodationCapacityForm` section |
| `frontend/src/components/camps/CampEditionDetails.vue` | Add `AccommodationCapacityDisplay` card |
| `frontend/src/views/camps/CampLocationDetailPage.vue` | Add accommodation display + photo gallery sections |

### Files to create

| File | Purpose |
|------|---------|
| `frontend/src/types/camp-photo.ts` | All photo-related TypeScript interfaces |
| `frontend/src/composables/useCampPhotos.ts` | Photo CRUD, reorder, set-primary composable |
| `frontend/src/components/camps/AccommodationCapacityForm.vue` | Collapsible form for all accommodation fields |
| `frontend/src/components/camps/AccommodationCapacityDisplay.vue` | Read-only summary card with calculated bed count |
| `frontend/src/components/camps/CampPhotoCard.vue` | Single photo card with edit/delete/set-primary actions |
| `frontend/src/components/camps/CampPhotoForm.vue` | Add/edit photo dialog (URL, description, order, primary flag) |
| `frontend/src/components/camps/CampPhotoGallery.vue` | Full gallery: grid view + reorder + manage |
| `frontend/src/composables/__tests__/useCampPhotos.test.ts` | Unit tests for photo composable |
| `frontend/cypress/e2e/camps/camp-photos.cy.ts` | E2E tests for photo management workflow |

### State management

No new Pinia store. Each view/component instantiates `useCampPhotos()` locally. Photos are loaded on demand when the gallery is mounted.

### Routing

No new routes. Accommodation and photos are embedded in existing camp detail and form pages:

- Accommodation → shown in `CampLocationDetailPage` + `CampLocationForm` + `CampEditionDetails`
- Photos → shown in `CampLocationDetailPage` (Board+ only section, guarded by `auth.isBoard`)

---

## 3. Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to a new feature branch.
**Branch name**: `feature/feat-camps-accommodation-photos-frontend`

**Implementation Steps**:

1. Ensure you are on `main`: `git checkout main && git pull origin main`
2. Create branch: `git checkout -b feature/feat-camps-accommodation-photos-frontend`
3. Verify: `git branch`

> **Note**: The backend for this feature is on `feature/feat-camps-accommodation-backend`. Coordinate with backend to ensure the migration and new endpoints are deployed before running integration tests. Use the mock strategy in unit tests to develop independently.

---

### Step 1: TypeScript Types — `camp.ts`

**File**: `frontend/src/types/camp.ts`
**Action**: Add new value-object interfaces and extend `Camp`, `CreateCampRequest`, `UpdateCampRequest`.

```typescript
// Add these new interfaces

export interface SharedRoomInfo {
  quantity: number
  bedsPerRoom: number
  hasBathroom: boolean
  hasShower: boolean
  notes?: string | null
}

export interface AccommodationCapacity {
  privateRoomsWithBathroom?: number | null
  privateRoomsSharedBathroom?: number | null
  sharedRooms?: SharedRoomInfo[] | null
  bungalows?: number | null
  campOwnedTents?: number | null
  memberTentAreaSquareMeters?: number | null
  memberTentCapacityEstimate?: number | null
  motorhomeSpots?: number | null
  notes?: string | null
}
```

**Update `Camp` interface** — add at the end:

```typescript
accommodationCapacity?: AccommodationCapacity | null
calculatedTotalBedCapacity?: number | null
photos?: CampPhoto[]        // import CampPhoto from './camp-photo'
```

**Update `CreateCampRequest`** — add:

```typescript
accommodationCapacity?: AccommodationCapacity | null
```

**Update `UpdateCampRequest`** (which extends `CreateCampRequest`) — no extra changes needed since it extends `CreateCampRequest`.

**Implementation Notes**:

- `CampPhoto` is imported from `./camp-photo` (new file, Step 2).
- Field names mirror the backend camelCase JSON output exactly.
- All capacity fields are optional (the backend allows partial updates).

---

### Step 2: TypeScript Types — `camp-photo.ts` (NEW)

**File**: `frontend/src/types/camp-photo.ts`
**Action**: Create all photo-related interfaces mirroring backend DTOs.

```typescript
export interface CampPhoto {
  id: string
  campId: string
  url: string
  description?: string | null
  displayOrder: number
  isPrimary: boolean
  isOriginal: boolean
  createdAt: string
  updatedAt: string
}

export interface AddCampPhotoRequest {
  url: string
  description?: string | null
  displayOrder: number
  isPrimary: boolean
}

export interface UpdateCampPhotoRequest {
  url: string
  description?: string | null
  displayOrder: number
  isPrimary: boolean
}

export interface PhotoOrderItem {
  id: string
  displayOrder: number
}

export interface ReorderCampPhotosRequest {
  photos: PhotoOrderItem[]
}
```

---

### Step 3: TypeScript Types — `camp-edition.ts`

**File**: `frontend/src/types/camp-edition.ts`
**Action**: Import `AccommodationCapacity` and add fields to `CampEdition` and `ProposeCampEditionRequest`.

Add import at the top:

```typescript
import type { AccommodationCapacity } from './camp'
```

**Update `CampEdition`** — add at the end:

```typescript
accommodationCapacity?: AccommodationCapacity | null
calculatedTotalBedCapacity?: number | null
```

**Update `ProposeCampEditionRequest`** — add at the end:

```typescript
accommodationCapacity?: AccommodationCapacity | null
```

---

### Step 4: Composable — `useCampPhotos.ts` (NEW)

**File**: `frontend/src/composables/useCampPhotos.ts`
**Action**: Create composable encapsulating all photo API calls.

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  CampPhoto,
  AddCampPhotoRequest,
  UpdateCampPhotoRequest,
  ReorderCampPhotosRequest
} from '@/types/camp-photo'

export function useCampPhotos() {
  const photos = ref<CampPhoto[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const addPhoto = async (
    campId: string,
    request: AddCampPhotoRequest
  ): Promise<CampPhoto | null> => { ... }

  const updatePhoto = async (
    campId: string,
    photoId: string,
    request: UpdateCampPhotoRequest
  ): Promise<CampPhoto | null> => { ... }

  const deletePhoto = async (campId: string, photoId: string): Promise<boolean> => { ... }

  const setPrimaryPhoto = async (
    campId: string,
    photoId: string
  ): Promise<CampPhoto | null> => { ... }

  const reorderPhotos = async (
    campId: string,
    request: ReorderCampPhotosRequest
  ): Promise<boolean> => { ... }

  return { photos, loading, error, addPhoto, updatePhoto, deletePhoto, setPrimaryPhoto, reorderPhotos }
}
```

**Implementation Notes for each function:**

- `addPhoto`: `POST /api/camps/{campId}/photos` → on success push to `photos.value`, set IsPrimary if needed.
- `updatePhoto`: `PUT /api/camps/{campId}/photos/{photoId}` → on success update photo in `photos.value` array.
- `deletePhoto`: `DELETE /api/camps/{campId}/photos/{photoId}` → on success remove from `photos.value`. Return `true`/`false`.
- `setPrimaryPhoto`: `POST /api/camps/{campId}/photos/{photoId}/set-primary` → on success update `isPrimary` on all photos in local state (clear all then set the updated one).
- `reorderPhotos`: `PUT /api/camps/{campId}/photos/reorder` → on success reorder `photos.value` array by `displayOrder`.

**Error handling pattern** (same as existing composables):

```typescript
try {
  const response = await api.post<ApiResponse<CampPhoto>>(
    `/camps/${campId}/photos`,
    request
  )
  if (response.data.success && response.data.data) {
    photos.value.push(response.data.data)
    return response.data.data
  }
  return null
} catch (err: unknown) {
  error.value =
    (err as { response?: { data?: { error?: { message?: string } } } })
      ?.response?.data?.error?.message || 'Error al añadir la foto'
  console.error('Failed to add photo:', err)
  return null
} finally {
  loading.value = false
}
```

---

### Step 5: Update Composable — `useCampEditions.ts`

**File**: `frontend/src/composables/useCampEditions.ts`
**Action**: The `proposeEdition` function already passes the whole `ProposeCampEditionRequest` object. After Step 3 adds `accommodationCapacity` to the type, the composable passes it automatically — **no code change needed in the composable itself**, only the type update enables the new field.

**Verify**: Search for `proposeEdition` in the file and confirm it calls `api.post('/camps/editions/propose', request)` where `request` is typed as `ProposeCampEditionRequest`. That is sufficient.

---

### Step 6: Component — `AccommodationCapacityForm.vue` (NEW)

**File**: `frontend/src/components/camps/AccommodationCapacityForm.vue`
**Action**: A collapsible form section that allows Board+ users to enter accommodation capacity data.

**Props:**

```typescript
interface Props {
  modelValue: AccommodationCapacity | null
}
```

**Emits:**

```typescript
defineEmits<{
  'update:modelValue': [value: AccommodationCapacity | null]
}>()
```

**Template structure:**

```
<Panel header="Capacidad de alojamiento" :toggleable="true" :collapsed="true">
  <!-- Private rooms with bathroom -->
  <InputNumber label="Habitaciones privadas con baño" />
  <!-- Private rooms shared bathroom -->
  <InputNumber label="Habitaciones privadas baño compartido" />
  <!-- Bungalows -->
  <InputNumber label="Bungalows / Casetas" />
  <!-- Camp owned tents -->
  <InputNumber label="Tiendas del camping" />
  <!-- Member tent area -->
  <InputNumber label="Área tiendas socios (m²)" />
  <!-- Member tent capacity estimate -->
  <InputNumber label="Capacidad estimada tiendas socios" />
  <!-- Motorhome spots -->
  <InputNumber label="Plazas autocaravanas" />
  <!-- Notes -->
  <Textarea label="Notas libres" />

  <!-- Shared Rooms (dynamic list) -->
  <div v-for="(room, index) in localValue.sharedRooms" :key="index">
    <InputNumber label="Habitaciones" />   <!-- quantity -->
    <InputNumber label="Camas/hab." />     <!-- bedsPerRoom -->
    <Checkbox label="Baño propio" />       <!-- hasBathroom -->
    <Checkbox label="Ducha propia" />      <!-- hasShower -->
    <InputText label="Notas" />            <!-- notes -->
    <Button icon="pi pi-trash" text @click="removeSharedRoom(index)" />
  </div>
  <Button label="Añadir tipo habitación compartida" icon="pi pi-plus" text @click="addSharedRoom" />

  <!-- Clear all -->
  <Button label="Limpiar capacidad" severity="secondary" text @click="clearCapacity" />
</Panel>
```

**Implementation Notes:**

- Use `reactive` to manage a local copy of the `AccommodationCapacity` value.
- Emit `update:modelValue` on every field change using `watch`.
- The `Panel` component from PrimeVue supports collapsible behavior — start collapsed since this is optional data.
- "Shared rooms" section renders a list of `SharedRoomInfo` items; use `addSharedRoom()` to push `{ quantity: 1, bedsPerRoom: 2, hasBathroom: false, hasShower: false }` defaults.
- `removeSharedRoom(index)` splices the item from the local array.
- `clearCapacity` sets `localValue` to `null` and emits `null` (lets parent set `accommodationCapacity: null`).
- All labels in Spanish. Field names in the component code in English.
- Use `InputNumber` with `:min="0"` for all numeric fields to prevent negatives.

---

### Step 7: Component — `AccommodationCapacityDisplay.vue` (NEW)

**File**: `frontend/src/components/camps/AccommodationCapacityDisplay.vue`
**Action**: Read-only card summary of accommodation capacity.

**Props:**

```typescript
interface Props {
  capacity: AccommodationCapacity | null | undefined
  totalBedCapacity?: number | null
}
```

**Template structure:**

```
<Card v-if="capacity">
  <template #title>
    <div class="flex items-center gap-2">
      <i class="pi pi-home text-primary-600" />
      Capacidad de alojamiento
      <span v-if="totalBedCapacity" class="ml-auto text-sm font-normal text-gray-500">
        {{ totalBedCapacity }} camas totales estimadas
      </span>
    </div>
  </template>
  <template #content>
    <dl class="grid grid-cols-2 gap-2 text-sm">
      <template v-if="capacity.privateRoomsWithBathroom">
        <dt class="text-gray-500">Hab. privadas c/ baño:</dt>
        <dd class="font-medium">{{ capacity.privateRoomsWithBathroom }}</dd>
      </template>
      <!-- ... all other fields ... -->
      <template v-if="capacity.sharedRooms?.length">
        <!-- Render each shared room type -->
      </template>
    </dl>
    <p v-if="capacity.notes" class="mt-3 text-sm italic text-gray-600">
      {{ capacity.notes }}
    </p>
  </template>
</Card>
<!-- Show nothing if capacity is null/undefined -->
```

**Implementation Notes:**

- Use `v-if` on each `<template>` pair so null/zero fields are hidden.
- The `totalBedCapacity` prop is the backend-calculated value (no frontend calculation needed).
- Use PrimeVue `Card` component for consistent styling.

---

### Step 8: Component — `CampPhotoCard.vue` (NEW)

**File**: `frontend/src/components/camps/CampPhotoCard.vue`
**Action**: Single photo card showing image thumbnail, description, primary badge, and action buttons.

**Props:**

```typescript
interface Props {
  photo: CampPhoto
}
```

**Emits:**

```typescript
defineEmits<{
  edit: [photo: CampPhoto]
  delete: [photo: CampPhoto]
  setPrimary: [photo: CampPhoto]
}>()
```

**Template structure:**

```
<div class="relative rounded-lg overflow-hidden border border-gray-200">
  <!-- Primary badge -->
  <span v-if="photo.isPrimary"
    class="absolute top-2 left-2 z-10 bg-primary-500 text-white text-xs px-2 py-1 rounded-full">
    Principal
  </span>

  <!-- Image -->
  <img :src="photo.url" :alt="photo.description || 'Foto del campamento'"
    class="w-full h-40 object-cover" />

  <!-- Footer -->
  <div class="p-2 bg-white">
    <p v-if="photo.description" class="text-xs text-gray-600 truncate">{{ photo.description }}</p>
    <p class="text-xs text-gray-400">Orden: {{ photo.displayOrder }}</p>
    <div class="flex gap-1 mt-2 justify-end">
      <Button v-if="!photo.isPrimary" icon="pi pi-star" text size="small"
        v-tooltip.top="'Establecer como principal'"
        @click="emit('setPrimary', photo)" />
      <Button icon="pi pi-pencil" text size="small"
        v-tooltip.top="'Editar'"
        @click="emit('edit', photo)" />
      <Button icon="pi pi-trash" text severity="danger" size="small"
        v-tooltip.top="'Eliminar'"
        @click="emit('delete', photo)" />
    </div>
  </div>
</div>
```

**Implementation Notes:**

- Use `data-testid="camp-photo-card"` for Cypress.
- Wrap the image in a fixed-height container to prevent layout jumps.
- The "set primary" button is hidden when `photo.isPrimary === true`.
- Tooltip text in Spanish.

---

### Step 9: Component — `CampPhotoForm.vue` (NEW)

**File**: `frontend/src/components/camps/CampPhotoForm.vue`
**Action**: Dialog for adding or editing a camp photo.

**Props:**

```typescript
interface Props {
  visible: boolean
  campId: string
  photo?: CampPhoto    // undefined = add mode; defined = edit mode
}
```

**Emits:**

```typescript
defineEmits<{
  'update:visible': [value: boolean]
  saved: [photo: CampPhoto]
}>()
```

**Template:**

```
<Dialog v-model:visible="localVisible"
  :header="photo ? 'Editar foto' : 'Añadir foto'"
  modal class="w-full max-w-lg">

  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <!-- URL -->
    <div>
      <label class="block text-sm font-medium mb-1">URL de la foto *</label>
      <InputText v-model="formData.url" class="w-full" :invalid="!!errors.url" />
      <small v-if="errors.url" class="text-red-500">{{ errors.url }}</small>
    </div>

    <!-- Description -->
    <div>
      <label class="block text-sm font-medium mb-1">Descripción (opcional)</label>
      <Textarea v-model="formData.description" class="w-full" rows="2"
        placeholder="Descripción breve de la foto..." />
      <small v-if="errors.description" class="text-red-500">{{ errors.description }}</small>
    </div>

    <!-- Display Order -->
    <div>
      <label class="block text-sm font-medium mb-1">Orden de visualización</label>
      <InputNumber v-model="formData.displayOrder" :min="0" class="w-full" />
    </div>

    <!-- Is Primary -->
    <div class="flex items-center gap-2">
      <Checkbox v-model="formData.isPrimary" binary inputId="isPrimary" />
      <label for="isPrimary" class="text-sm">Establecer como foto principal</label>
    </div>

    <!-- Actions -->
    <div class="flex justify-end gap-2 pt-2">
      <Button label="Cancelar" severity="secondary" @click="close" />
      <Button type="submit" :label="photo ? 'Guardar cambios' : 'Añadir foto'"
        :loading="loading" :disabled="loading" />
    </div>
  </form>
</Dialog>
```

**Implementation Notes:**

- `formData` is initialized from `props.photo` if in edit mode, otherwise from empty defaults.
- Validation: `url` required + max 2000 chars; `description` optional max 500 chars; `displayOrder` ≥ 0.
- On submit, call `useCampPhotos().addPhoto()` or `updatePhoto()` based on whether `photo` prop is defined.
- On success, emit `saved` with the returned `CampPhoto` and close the dialog.
- Error messages in Spanish.

---

### Step 10: Component — `CampPhotoGallery.vue` (NEW)

**File**: `frontend/src/components/camps/CampPhotoGallery.vue`
**Action**: Full gallery management: grid view, add, edit, delete, set-primary, and drag-to-reorder.

**Props:**

```typescript
interface Props {
  campId: string
  initialPhotos: CampPhoto[]
}
```

**Emits:**

```typescript
defineEmits<{
  photosChanged: [photos: CampPhoto[]]
}>()
```

**Internal structure:**

```typescript
const { loading, error, addPhoto, updatePhoto, deletePhoto, setPrimaryPhoto, reorderPhotos } = useCampPhotos()
const photos = ref<CampPhoto[]>([...props.initialPhotos].sort((a, b) => a.displayOrder - b.displayOrder))
const showForm = ref(false)
const editingPhoto = ref<CampPhoto | undefined>(undefined)
const showDeleteConfirm = ref(false)
const photoToDelete = ref<CampPhoto | null>(null)
```

**Template structure:**

```
<div>
  <!-- Header with add button -->
  <div class="flex items-center justify-between mb-4">
    <h3 class="text-lg font-semibold">Fotos del campamento ({{ photos.length }})</h3>
    <Button label="Añadir foto" icon="pi pi-plus" @click="openAddForm" />
  </div>

  <!-- Loading / Error -->
  <ProgressSpinner v-if="loading" />
  <Message v-else-if="error" severity="error">{{ error }}</Message>

  <!-- Empty state -->
  <div v-else-if="photos.length === 0" class="text-center py-8 text-gray-500">
    <i class="pi pi-images text-4xl mb-2" />
    <p>No hay fotos todavía. Añade la primera foto del campamento.</p>
  </div>

  <!-- Photo grid -->
  <div v-else>
    <!-- Reorder button (toggles OrderList mode) -->
    <Button v-if="photos.length > 1" :label="reorderMode ? 'Guardar orden' : 'Reordenar'"
      :icon="reorderMode ? 'pi pi-check' : 'pi pi-arrows-v'"
      text class="mb-3"
      @click="toggleReorderMode" />

    <!-- Reorder mode: OrderList -->
    <OrderList v-if="reorderMode" v-model="photos" :option-label="() => ''" class="mb-4">
      <template #item="{ item }">
        <div class="flex items-center gap-3 p-2">
          <img :src="item.url" class="w-16 h-12 object-cover rounded" />
          <span class="text-sm">{{ item.description || item.url }}</span>
        </div>
      </template>
    </OrderList>

    <!-- Grid mode -->
    <div v-else class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
      <CampPhotoCard
        v-for="photo in photos"
        :key="photo.id"
        :photo="photo"
        data-testid="camp-photo-card"
        @edit="openEditForm"
        @delete="confirmDelete"
        @set-primary="handleSetPrimary"
      />
    </div>
  </div>

  <!-- Add/Edit Dialog -->
  <CampPhotoForm
    v-model:visible="showForm"
    :camp-id="campId"
    :photo="editingPhoto"
    @saved="handlePhotoSaved"
  />

  <!-- Delete Confirmation Dialog -->
  <ConfirmDialog />
</div>
```

**Implementation Notes:**

- `reorderMode` is a `ref<boolean>(false)`. When toggling OFF, call `reorderPhotos(campId, { photos: photos.value.map((p, i) => ({ id: p.id, displayOrder: i })) })`.
- Use PrimeVue `OrderList` for reordering (drag-to-reorder built in). Import: `import OrderList from 'primevue/orderlist'`.
- For delete confirmation use PrimeVue `useConfirm()` + `ConfirmDialog`:

  ```typescript
  const confirm = useConfirm()
  const confirmDelete = (photo: CampPhoto) => {
    confirm.require({
      message: '¿Estás seguro de que quieres eliminar esta foto?',
      header: 'Eliminar foto',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Eliminar',
      rejectLabel: 'Cancelar',
      accept: () => handleDelete(photo)
    })
  }
  ```

- After any mutation (add/update/delete/set-primary/reorder), emit `photosChanged` with the updated `photos.value` array.
- When `handleSetPrimary` is called, update local state immediately (optimistic update) before the API call, then confirm with the server response.

---

### Step 11: Update — `CampLocationForm.vue`

**File**: `frontend/src/components/camps/CampLocationForm.vue`
**Action**: Add `AccommodationCapacityForm` section to the camp create/edit form.

**Changes:**

1. Import `AccommodationCapacityForm` and `AccommodationCapacity` type.
2. Add to `formData` reactive object:

   ```typescript
   accommodationCapacity: null as AccommodationCapacity | null
   ```

3. In edit-mode initialization block, also set:

   ```typescript
   accommodationCapacity: props.camp.accommodationCapacity ?? null
   ```

4. Add `AccommodationCapacityForm` component after the pricing fields section:

   ```html
   <AccommodationCapacityForm v-model="formData.accommodationCapacity" />
   ```

5. The existing `emit('submit', formData)` call already sends the full `formData` object — no additional changes needed since `accommodationCapacity` is now part of it.

---

### Step 12: Update — `CampEditionDetails.vue`

**File**: `frontend/src/components/camps/CampEditionDetails.vue`
**Action**: Add `AccommodationCapacityDisplay` card to the details grid.

**Changes:**

1. Import `AccommodationCapacityDisplay`.
2. After the existing cards (dates, pricing, map, etc.), add:

   ```html
   <AccommodationCapacityDisplay
     v-if="campEdition.accommodationCapacity"
     :capacity="campEdition.accommodationCapacity"
     :total-bed-capacity="campEdition.calculatedTotalBedCapacity"
   />
   ```

---

### Step 13: Update — `CampLocationDetailPage.vue`

**File**: `frontend/src/views/camps/CampLocationDetailPage.vue`
**Action**: Add accommodation display section and photo gallery section for Board+ users.

**Changes:**

1. Import `AccommodationCapacityDisplay`, `CampPhotoGallery`, and `useAuthStore`.

2. Add auth check:

   ```typescript
   const auth = useAuthStore()
   ```

3. After the existing "Info Panel" grid, add two new sections inside `<div v-else-if="camp">`:

   **Accommodation section** (visible to all authenticated users):

   ```html
   <AccommodationCapacityDisplay
     v-if="camp.accommodationCapacity"
     :capacity="camp.accommodationCapacity"
     :total-bed-capacity="camp.calculatedTotalBedCapacity"
     class="mt-6"
   />
   ```

   **Photo gallery section** (Board+ only):

   ```html
   <div v-if="auth.isBoard" class="mt-6">
     <div class="rounded-lg border border-gray-200 bg-white p-6">
       <CampPhotoGallery
         :camp-id="camp.id"
         :initial-photos="camp.photos ?? []"
       />
     </div>
   </div>
   ```

4. No new API calls needed — photos are returned in the existing `GET /api/camps/{id}` response (backend includes `Camp.Photos` in `CampDetailResponse`).

**Implementation Notes:**

- Check if the backend `GET /api/camps/{id}` actually returns `photos` in the response. If the existing endpoint uses a different method (e.g., `GetByIdAsync` vs `GetByIdWithPhotosAsync`), you may need to verify this. The spec states photos are already on `Camp.Photos`. Log the response during development to confirm.
- If photos are not in the detail response by default, a separate `GET /api/camps/{campId}/photos` endpoint may be needed — the spec does not list one, so rely on the embedded response for now.

---

### Step 14: Write Unit Tests — `useCampPhotos.test.ts`

**File**: `frontend/src/composables/__tests__/useCampPhotos.test.ts`

**Tests to write** (AAA pattern, Vitest + vi.mock):

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampPhotos } from '@/composables/useCampPhotos'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('useCampPhotos', () => {
  describe('addPhoto', () => {
    it('should add a photo and return it on success')
    it('should push the new photo to the photos array')
    it('should set error message when API call fails')
    it('should return null on failure')
  })

  describe('updatePhoto', () => {
    it('should update the photo in the local array on success')
    it('should return the updated photo on success')
    it('should set error when photo not found')
  })

  describe('deletePhoto', () => {
    it('should remove the photo from the local array on success')
    it('should return true on success')
    it('should return false and set error on failure')
  })

  describe('setPrimaryPhoto', () => {
    it('should set isPrimary=true on the target photo')
    it('should set isPrimary=false on all other photos')
    it('should return the updated photo on success')
  })

  describe('reorderPhotos', () => {
    it('should call the reorder endpoint with correct payload')
    it('should return true on success')
    it('should set error and return false on failure')
  })
})
```

---

### Step 15: Write E2E Tests — `camp-photos.cy.ts`

**File**: `frontend/cypress/e2e/camps/camp-photos.cy.ts`

```typescript
describe('Camp Photo Management (Board user)', () => {
  beforeEach(() => {
    cy.loginAsBoard()          // Custom Cypress command
    cy.visit('/camps/locations')
    cy.get('[data-testid="camp-location-row"]').first().click()
  })

  it('should show the photo gallery section for Board users')
  it('should show "No hay fotos" empty state when camp has no photos')
  it('should open the add photo dialog when clicking Añadir foto')
  it('should add a new photo after submitting the form')
  it('should show validation errors for empty URL')
  it('should edit an existing photo description')
  it('should delete a photo after confirmation')
  it('should set a photo as primary and display the "Principal" badge')
  it('should reorder photos and save the new order')
})

describe('Camp Photo Management (Member user)', () => {
  beforeEach(() => {
    cy.loginAsMember()
    cy.visit('/camps/locations/some-camp-id')
  })

  it('should NOT show the photo gallery management section for Member users')
})
```

**Implementation Note**: Define `cy.loginAsBoard()` and `cy.loginAsMember()` as custom commands in `cypress/support/commands.ts` if they don't already exist. Check existing commands first.

---

### Step 16: Update Technical Documentation

**Action**: Update `ai-specs/specs/frontend-standards.mdc` and `ai-specs/specs/api-spec.yml` to reflect the new patterns and endpoints.

**Implementation Steps:**

1. Check `ai-specs/specs/api-spec.yml` for `/api/camps/{campId}/photos` entries — add them if missing.
2. In `frontend-standards.mdc`, add `useCampPhotos` to the composables examples if a new pattern is introduced (e.g., the `setPrimary` optimistic update pattern).
3. Document the `OrderList` usage for reordering as an approved PrimeVue pattern.
4. Confirm documentation is in English per `documentation-standards.mdc`.

---

## 4. Implementation Order

1. **Step 0** — Create feature branch `feature/feat-camps-accommodation-photos-frontend`
2. **Step 1** — Update `camp.ts` types (`AccommodationCapacity`, `SharedRoomInfo`, extend `Camp`)
3. **Step 2** — Create `camp-photo.ts` types
4. **Step 3** — Update `camp-edition.ts` types
5. **Step 4** — Create `useCampPhotos` composable
6. **Step 5** — Verify `useCampEditions` (no code change needed)
7. **Step 6** — Build `AccommodationCapacityForm.vue`
8. **Step 7** — Build `AccommodationCapacityDisplay.vue`
9. **Step 8** — Build `CampPhotoCard.vue`
10. **Step 9** — Build `CampPhotoForm.vue`
11. **Step 10** — Build `CampPhotoGallery.vue`
12. **Step 11** — Update `CampLocationForm.vue` (add accommodation form)
13. **Step 12** — Update `CampEditionDetails.vue` (add accommodation display)
14. **Step 13** — Update `CampLocationDetailPage.vue` (add both sections)
15. **Step 14** — Write unit tests for `useCampPhotos`
16. **Step 15** — Write Cypress E2E tests
17. **Step 16** — Update technical documentation

---

## 5. Testing Checklist

### Unit tests (Vitest)

- [ ] `useCampPhotos.addPhoto` — success path, error path, local state update
- [ ] `useCampPhotos.updatePhoto` — success path, error path
- [ ] `useCampPhotos.deletePhoto` — success path, error path
- [ ] `useCampPhotos.setPrimaryPhoto` — correct `isPrimary` flag update on all photos
- [ ] `useCampPhotos.reorderPhotos` — correct payload construction
- [ ] `AccommodationCapacityForm` — renders all fields, emits on change, shared rooms add/remove
- [ ] `AccommodationCapacityDisplay` — renders only non-null fields, shows bed count
- [ ] `CampPhotoForm` — validation errors, submit calls correct composable method, emits `saved`

### Component tests

- [ ] `CampPhotoCard` — primary badge visible/hidden, emits `edit`/`delete`/`set-primary`
- [ ] `CampPhotoGallery` — empty state, grid renders, reorder mode toggle

### Cypress E2E

- [ ] Board user can add a photo via the form
- [ ] Board user can edit a photo description
- [ ] Board user can delete a photo with confirmation
- [ ] Board user can set a primary photo (badge shown)
- [ ] Board user can trigger reorder mode and save
- [ ] Member user does not see the photo management section
- [ ] Accommodation capacity displays correctly on camp detail page
- [ ] AccommodationCapacity is included when creating/editing a camp

---

## 6. Error Handling Patterns

**Composable level (`useCampPhotos`):**

- `error.value` is set from `err.response?.data?.error?.message` with Spanish fallback
- `loading.value` set to `true` at start, always reset in `finally`
- Return `null`/`false` on failure; return data on success

**Component level:**

- `CampPhotoGallery` shows `<Message severity="error">{{ error }}</Message>` below the header
- `CampPhotoForm` shows per-field validation errors inline using `<small class="text-red-500">`
- Toast notifications (PrimeVue `useToast`) for success confirmations:

  ```typescript
  import { useToast } from 'primevue/usetoast'
  const toast = useToast()
  // On success:
  toast.add({ severity: 'success', summary: 'Éxito', detail: 'Foto añadida correctamente', life: 3000 })
  // On error:
  toast.add({ severity: 'error', summary: 'Error', detail: error.value || 'Ocurrió un error', life: 5000 })
  ```

---

## 7. UI/UX Considerations

- **Accommodation form**: Use PrimeVue `Panel` with `toggleable` to hide the section by default — it's optional data and shouldn't clutter the main form.
- **Photo gallery grid**: `grid-cols-2 sm:grid-cols-3 lg:grid-cols-4` for responsive layout.
- **Primary photo badge**: Green pill `"Principal"` overlaid on the image top-left.
- **Reorder UX**: Toggle between grid view and `OrderList` drag-to-reorder. Save button triggers API call.
- **Empty state**: Clear message with icon when no photos exist.
- **Loading states**: Use `ProgressSpinner` while composable `loading.value === true`.
- **Delete confirmation**: PrimeVue `ConfirmDialog` (requires `ConfirmationService` registered in `main.ts` — verify it's already registered).
- **Accessibility**: `alt` text on all images; ARIA labels on icon-only buttons; keyboard-navigable delete confirm.
- **Image errors**: Use `@error` on `<img>` to replace broken images with a placeholder icon.
- **Shared rooms** in `AccommodationCapacityForm`: Use `Divider` between each shared room row for clarity.

---

## 8. Dependencies

### PrimeVue components used (all already installed)

- `Panel` (with `toggleable`) — collapsible accommodation section
- `InputNumber` — numeric accommodation fields
- `Textarea` — notes fields
- `Checkbox` — bathroom/shower flags in shared rooms; IsPrimary flag
- `Card` — accommodation display card
- `OrderList` — drag-to-reorder photos
- `Dialog` — add/edit photo form
- `ConfirmDialog` + `useConfirm()` — delete confirmation
- `Button` — all actions
- `Message` — error states
- `ProgressSpinner` — loading states
- `Tooltip` directive (`v-tooltip`) — icon button hints

### No new npm packages required

All required libraries (`@vueuse/core`, PrimeVue, etc.) are already installed.

---

## 9. Notes

### Business rules to enforce in the UI

- Only one photo can be `isPrimary = true` per camp — the gallery handles this by updating all local `isPrimary` flags when `setPrimaryPhoto` returns.
- Manual photos have `isOriginal = false` — set this in the composable's `addPhoto` call payload (the backend sets it, but confirm the API doesn't require it from the client; according to spec, the service sets `IsOriginal = false` automatically).
- All photo endpoints require `Admin` or `Board` role — guard the gallery section in the template with `v-if="auth.isBoard"`.

### Language requirements

- All UI labels, messages, validation errors, and toast notifications in **Spanish**.
- Code (variables, functions, interfaces, types) in **English**.

### TypeScript strict mode

- No `any` types. Use `unknown` with narrowing for error handling.
- All props typed with `defineProps<T>()`.
- All emits typed with `defineEmits<T>()`.

### Integration dependency

- The backend migration and new endpoints (`/api/camps/{campId}/photos`) must be deployed before E2E tests can run against the real API.
- Unit tests use `vi.mock('@/utils/api')` and can run independently.

---

## 10. Next Steps After Implementation

- Coordinate with backend team to confirm migration has been applied and endpoints are live.
- Verify actual JSON response shape of `GET /api/camps/{id}` includes `photos` and `accommodationCapacity` — log `response.data` during initial integration.
- Consider whether a public-facing photo gallery (non-management, read-only) is needed for the member camp view — out of scope for this ticket per the spec.

---

## 11. Implementation Verification

### Code Quality

- [ ] All components use `<script setup lang="ts">` — no Options API
- [ ] No `any` types — `unknown` used where needed
- [ ] All props typed with generic `defineProps<T>()`
- [ ] All emits typed with generic `defineEmits<T>()`

### Functionality

- [ ] `AccommodationCapacityForm` emits correct `AccommodationCapacity` structure
- [ ] `CampPhotoGallery` shows/hides correctly based on `auth.isBoard`
- [ ] Create camp form sends `accommodationCapacity` in request body
- [ ] Camp detail page shows accommodation data (when present)
- [ ] Photo add/edit/delete/reorder/set-primary all work against real backend

### Testing

- [ ] Unit tests pass: `npx vitest --run`
- [ ] E2E tests pass: `npx cypress run`
- [ ] Coverage ≥ 90%: `npx vitest --coverage`

### Integration

- [ ] `useCampPhotos` composable calls correct API endpoints
- [ ] `useCampEditions.proposeEdition` sends `accommodationCapacity` when provided
- [ ] `useCamps.updateCamp` / `createCamp` include `accommodationCapacity` in payload

### Documentation

- [ ] `api-spec.yml` updated with photo endpoints
- [ ] `frontend-standards.mdc` updated if new patterns were introduced

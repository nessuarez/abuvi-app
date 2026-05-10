# Frontend Implementation Plan: feat-media-50-aniversary — Real Media Uploads for the 50th Anniversary

## Overview

Replace the static mock anniversary page with real file upload, gallery display, and admin review functionality. The frontend integrates with the new `/api/memories` and `/api/media-items` endpoints (built in the backend ticket) and the existing `/api/blobs/upload` endpoint.

**Architecture:** Vue 3 Composition API, composable-based architecture, PrimeVue components, Tailwind CSS utilities. All user-facing text in Spanish, all code in English.

---

## Architecture Context

### Components/Composables Involved

| Type | File | Action |
|---|---|---|
| Composable (new) | `frontend/src/composables/useMediaItems.ts` | CRUD + approve/reject for media items |
| Composable (new) | `frontend/src/composables/useMemories.ts` | CRUD + approve/reject for memories |
| Composable (existing) | `frontend/src/composables/useBlobStorage.ts` | File upload to blob storage |
| Type (new) | `frontend/src/types/media-item.ts` | TypeScript interfaces |
| Type (new) | `frontend/src/types/memory.ts` | TypeScript interfaces |
| Component (modify) | `frontend/src/components/anniversary/AnniversaryUploadForm.vue` | Enable submit, integrate API |
| Component (modify) | `frontend/src/components/anniversary/AnniversaryGallery.vue` | Replace placeholders with real data |
| Component (new) | `frontend/src/components/admin/MediaItemsReviewPanel.vue` | Admin review panel for media items |
| View (modify) | `frontend/src/views/AdminPage.vue` | Add media review tab |
| Router (modify) | `frontend/src/router/index.ts` | No new routes needed (using admin tabs) |

### State Management Approach

- **Local state via composables** — no Pinia store needed. Each composable manages its own `ref()` state.
- `useMediaItems()` and `useMemories()` follow the same pattern as `useBlobStorage()` and `useCampPhotos()`.

### Routing Considerations

The enriched spec suggests a separate `/admin/media-review` route. However, the existing admin pattern uses a **tabbed interface** (`AdminPage.vue` with PrimeVue `Tabs`). To stay consistent with the established pattern, the media review will be added as a **new tab** in the existing admin panel rather than a separate route.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a frontend-specific branch
- **Branch name**: `feature/feat-media-50-aniversary-frontend`
- **Implementation Steps**:
  1. Ensure on latest base: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/feat-media-50-aniversary-frontend`
  3. Verify: `git branch`
- **Notes**: This branch assumes the backend branch has been merged or is available. The frontend composables need the backend endpoints to be functional.

---

### Step 1: Define TypeScript Interfaces

**File**: `frontend/src/types/media-item.ts`

**Action**: Create type definitions for MediaItem and related DTOs.

```typescript
export type MediaItemType = 'Photo' | 'Video' | 'Audio' | 'Interview' | 'Document'

export interface MediaItem {
  id: string
  uploadedByUserId: string
  uploadedByName: string
  fileUrl: string
  thumbnailUrl: string | null
  type: MediaItemType
  title: string
  description: string | null
  year: number | null
  decade: string | null
  memoryId: string | null
  context: string | null
  isPublished: boolean
  isApproved: boolean
  createdAt: string
}

export interface CreateMediaItemRequest {
  fileUrl: string
  thumbnailUrl?: string | null
  type: MediaItemType
  title: string
  description?: string
  year?: number
  memoryId?: string
  campLocationId?: string
  context?: string
}
```

---

**File**: `frontend/src/types/memory.ts`

```typescript
import type { MediaItem } from './media-item'

export interface Memory {
  id: string
  authorUserId: string
  authorName: string
  title: string
  content: string
  year: number | null
  campLocationId: string | null
  isPublished: boolean
  isApproved: boolean
  createdAt: string
  updatedAt: string
  mediaItems: MediaItem[]
}

export interface CreateMemoryRequest {
  title: string
  content: string
  year?: number
  campLocationId?: string
}
```

**Dependencies**: None beyond existing type infrastructure.

---

### Step 2: Create Composables

**File**: `frontend/src/composables/useMediaItems.ts`

**Action**: Create composable for media items API communication. Follow the `useBlobStorage.ts` pattern exactly.

**Signature:**
```typescript
export function useMediaItems() {
  // State
  const mediaItems = ref<MediaItem[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const creating = ref(false)
  const createError = ref<string | null>(null)

  // Methods
  const fetchMediaItems = async (params?: {
    year?: number
    approved?: boolean
    context?: string
    type?: MediaItemType
  }): Promise<void>

  const createMediaItem = async (request: CreateMediaItemRequest): Promise<MediaItem | null>

  const approveMediaItem = async (id: string): Promise<boolean>

  const rejectMediaItem = async (id: string): Promise<boolean>

  const deleteMediaItem = async (id: string): Promise<boolean>

  return {
    mediaItems, loading, error,
    creating, createError,
    fetchMediaItems, createMediaItem,
    approveMediaItem, rejectMediaItem, deleteMediaItem
  }
}
```

**Implementation Notes:**
- API base paths: `/media-items` (relative to api base URL)
- `fetchMediaItems`: Build query string from params. `GET /media-items?year=X&approved=X&context=X&type=X`. Update `mediaItems.value` on success.
- `createMediaItem`: `POST /media-items` with JSON body. Return the created `MediaItem` or null on error.
- `approveMediaItem`: `PATCH /media-items/{id}/approve`. Return `true` on success.
- `rejectMediaItem`: `PATCH /media-items/{id}/reject`. Return `true` on success.
- `deleteMediaItem`: `DELETE /media-items/{id}`. Return `true` on success.
- Error handling: Extract error message via `(err as ApiErrorShape)?.response?.data?.error?.message` — same pattern as `useBlobStorage.ts`.
- All methods set `loading`/`error` state appropriately.

---

**File**: `frontend/src/composables/useMemories.ts`

**Action**: Create composable for memories API communication.

**Signature:**
```typescript
export function useMemories() {
  const memories = ref<Memory[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const creating = ref(false)
  const createError = ref<string | null>(null)

  const fetchMemories = async (params?: {
    year?: number
    approved?: boolean
  }): Promise<void>

  const createMemory = async (request: CreateMemoryRequest): Promise<Memory | null>

  const approveMemory = async (id: string): Promise<boolean>

  const rejectMemory = async (id: string): Promise<boolean>

  return {
    memories, loading, error,
    creating, createError,
    fetchMemories, createMemory,
    approveMemory, rejectMemory
  }
}
```

**Implementation Notes:**
- API base paths: `/memories`
- Same patterns as `useMediaItems` for error handling and state management.

---

### Step 3: Update AnniversaryUploadForm.vue

**File**: `frontend/src/components/anniversary/AnniversaryUploadForm.vue`

**Action**: Enable the submit button, integrate with real API endpoints, add progress indicator.

**Current state**: Form has `name`, `contentType`, `year`, `description` fields and a `FileUpload` component. Submit calls `handleSubmit()` which only shows a toast and resets the form.

**Changes required:**

1. **Import composables:**
   ```typescript
   import { useBlobStorage } from '@/composables/useBlobStorage'
   import { useMediaItems } from '@/composables/useMediaItems'
   import { useMemories } from '@/composables/useMemories'
   import ProgressBar from 'primevue/progressbar'
   ```

2. **Initialize composables:**
   ```typescript
   const { uploadFile, uploading, uploadError } = useBlobStorage()
   const { createMediaItem, creating: creatingMedia, createError: mediaError } = useMediaItems()
   const { createMemory, creating: creatingMemory, createError: memoryError } = useMemories()
   ```

3. **Add `selectedFile` ref** to capture the file from `FileUpload`:
   ```typescript
   const selectedFile = ref<File | null>(null)
   ```
   Use `@select` event on `FileUpload` to capture: `(event: { files: File[] }) => selectedFile.value = event.files[0]`

4. **Add computed `isSubmitting`:**
   ```typescript
   const isSubmitting = computed(() => uploading.value || creatingMedia.value || creatingMemory.value)
   ```

5. **Map content types to `MediaItemType`:**
   ```typescript
   const contentTypeToMediaType: Record<string, MediaItemType> = {
     foto: 'Photo',
     video: 'Video',
     audio: 'Audio',
   }
   ```

6. **Update validation:**
   - For `foto`, `video`, `audio`: require a file to be selected
   - For `historia`: require description (content) to be non-empty
   ```typescript
   if (form.contentType !== 'historia' && !selectedFile.value) {
     errors.value.file = 'Debes seleccionar un archivo'
   }
   if (form.contentType === 'historia' && !form.description.trim()) {
     errors.value.description = 'La descripción es obligatoria para historias escritas'
   }
   ```

7. **Rewrite `handleSubmit()` to be async:**
   ```typescript
   const handleSubmit = async () => {
     if (!validate()) return

     try {
       if (form.contentType === 'historia') {
         // Written story → create Memory
         const memory = await createMemory({
           title: `${form.name} — Historia 50 aniversario`,
           content: form.description,
           year: form.year ?? 2026,
         })
         if (!memory) {
           toast.add({ severity: 'error', summary: 'Error', detail: memoryError.value ?? 'Error al enviar la historia', life: 5000 })
           return
         }
       } else {
         // File upload → blob + media item
         const mediaType = contentTypeToMediaType[form.contentType!]
         const isImage = form.contentType === 'foto'

         const blobResult = await uploadFile({
           file: selectedFile.value!,
           folder: 'media-items',
           generateThumbnail: isImage,
         })
         if (!blobResult) {
           toast.add({ severity: 'error', summary: 'Error', detail: uploadError.value ?? 'Error al subir el archivo', life: 5000 })
           return
         }

         const mediaItem = await createMediaItem({
           fileUrl: blobResult.fileUrl,
           thumbnailUrl: blobResult.thumbnailUrl,
           type: mediaType,
           title: `${form.name} — Recuerdo 50 aniversario`,
           description: form.description || undefined,
           year: form.year ?? 2026,
           context: 'anniversary-50',
         })
         if (!mediaItem) {
           toast.add({ severity: 'error', summary: 'Error', detail: mediaError.value ?? 'Error al crear el recuerdo', life: 5000 })
           return
         }
       }

       toast.add({
         severity: 'success',
         summary: 'Éxito',
         detail: '¡Tu recuerdo ha sido enviado! Lo revisaremos pronto.',
         life: 4000,
       })
       resetForm()
     } catch {
       toast.add({ severity: 'error', summary: 'Error', detail: 'Error inesperado al enviar el recuerdo', life: 5000 })
     }
   }
   ```

8. **Add `resetForm()` helper:**
   ```typescript
   const resetForm = () => {
     form.name = ''
     form.contentType = null
     form.year = null
     form.description = ''
     selectedFile.value = null
     errors.value = {}
   }
   ```

9. **Template changes:**
   - Add `ProgressBar` below the form, visible when `isSubmitting`:
     ```html
     <ProgressBar v-if="isSubmitting" mode="indeterminate" class="mt-4" style="height: 6px" />
     ```
   - Update the `Button` to disable during submission:
     ```html
     <Button type="submit" label="Enviar recuerdo" icon="pi pi-send" class="w-full" :disabled="isSubmitting" :loading="isSubmitting" />
     ```
   - Update `FileUpload` to capture selected file:
     ```html
     <FileUpload
       mode="basic"
       name="memory"
       accept="image/*,video/*,audio/*"
       :max-file-size="50000000"
       choose-label="Seleccionar archivo"
       class="w-full"
       :auto="false"
       @select="(e: { files: File[] }) => selectedFile = e.files[0]"
     />
     ```
   - Add file validation error display:
     ```html
     <small v-if="errors.file" class="mt-1 block text-red-500">{{ errors.file }}</small>
     ```
   - Conditionally show file upload only for non-story types:
     ```html
     <div v-if="form.contentType && form.contentType !== 'historia'">
       <!-- File upload field -->
     </div>
     ```
   - For `historia` type, make `description` required and increase character limit (the description becomes the actual story content):
     ```html
     <Textarea ... :maxlength="form.contentType === 'historia' ? 5000 : 500" />
     ```

---

### Step 4: Update AnniversaryGallery.vue

**File**: `frontend/src/components/anniversary/AnniversaryGallery.vue`

**Action**: Replace hardcoded gallery items with real data from the API.

**Changes required:**

1. **Import composable and components:**
   ```typescript
   import { onMounted } from 'vue'
   import { useMediaItems } from '@/composables/useMediaItems'
   import Image from 'primevue/image'
   import Skeleton from 'primevue/skeleton'
   ```

2. **Initialize composable:**
   ```typescript
   const { mediaItems, loading, error, fetchMediaItems } = useMediaItems()
   ```

3. **Fetch data on mount:**
   ```typescript
   onMounted(() => {
     fetchMediaItems({ approved: true, context: 'anniversary-50' })
   })
   ```

4. **Remove static imports** (imgGrupo, imgFriends, etc.) and the hardcoded `galleryItems` array.

5. **Remove the `GalleryItem` interface** — use `MediaItem` type from the composable instead.

6. **Update template to render by media type:**

   ```html
   <!-- Loading state -->
   <div v-if="loading" class="grid grid-cols-1 gap-6 sm:grid-cols-3 lg:grid-cols-4">
     <div v-for="i in 8" :key="i" class="overflow-hidden rounded-xl bg-white shadow-sm">
       <Skeleton width="100%" height="12rem" />
       <div class="p-4">
         <Skeleton width="30%" height="1rem" class="mb-2" />
         <Skeleton width="60%" height="0.875rem" />
       </div>
     </div>
   </div>

   <!-- Empty state -->
   <div v-else-if="mediaItems.length === 0" class="py-12 text-center">
     <i class="pi pi-images mb-4 text-4xl text-amber-300" />
     <p class="text-lg text-gray-500">Aún no hay recuerdos aprobados.</p>
     <p class="mt-2 text-sm text-gray-400">¡Sé el primero en compartir!</p>
   </div>

   <!-- Gallery grid -->
   <div v-else class="grid grid-cols-1 gap-6 sm:grid-cols-3 lg:grid-cols-4">
     <article
       v-for="item in mediaItems"
       :key="item.id"
       class="overflow-hidden rounded-xl bg-white shadow-sm transition-shadow hover:shadow-md"
     >
       <!-- Photo type -->
       <template v-if="item.type === 'Photo'">
         <div class="overflow-hidden">
           <Image
             :src="item.fileUrl"
             :alt="item.title"
             preview
             image-class="w-full h-48 object-cover transition-transform hover:scale-105 cursor-pointer"
           />
         </div>
       </template>

       <!-- Video type -->
       <template v-else-if="item.type === 'Video'">
         <div class="relative">
           <video
             :src="item.fileUrl"
             :poster="item.thumbnailUrl ?? undefined"
             controls
             preload="metadata"
             class="h-48 w-full object-cover"
           />
         </div>
       </template>

       <!-- Audio type -->
       <template v-else-if="item.type === 'Audio'">
         <div class="flex h-48 flex-col items-center justify-center bg-amber-50 p-4">
           <i class="pi pi-volume-up mb-3 text-3xl text-amber-600" />
           <audio :src="item.fileUrl" controls class="w-full" :aria-label="item.title" />
         </div>
       </template>

       <!-- Document type -->
       <template v-else>
         <div class="flex h-48 flex-col items-center justify-center bg-gray-50 p-4">
           <i class="pi pi-file mb-3 text-3xl text-gray-400" />
           <a :href="item.fileUrl" target="_blank" rel="noopener"
              class="text-sm font-medium text-amber-700 hover:underline">
             Descargar documento
           </a>
         </div>
       </template>

       <!-- Card footer (all types) -->
       <div class="p-4">
         <span class="text-sm font-bold text-amber-600">{{ item.year ?? '—' }}</span>
         <p class="mt-1 text-sm font-medium text-gray-800">{{ item.title }}</p>
         <p v-if="item.description" class="mt-1 line-clamp-2 text-xs text-gray-500">{{ item.description }}</p>
         <p class="mt-1 text-xs text-gray-400">{{ item.uploadedByName }}</p>
       </div>
     </article>
   </div>
   ```

7. **Keep the scroll-to-upload CTA** at the bottom (no changes needed).

8. **Remove the placeholder disclaimer** text ("En el futuro esta galería mostrará...").

---

### Step 5: Create MediaItemsReviewPanel Component

**File**: `frontend/src/components/admin/MediaItemsReviewPanel.vue`

**Action**: Create an admin panel component for reviewing and moderating media items. This will be added as a tab in `AdminPage.vue`.

**Component structure:**

```typescript
<script setup lang="ts">
import { onMounted, computed } from 'vue'
import { useMediaItems } from '@/composables/useMediaItems'
import { useMemories } from '@/composables/useMemories'
import { useToast } from 'primevue/usetoast'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Image from 'primevue/image'

const toast = useToast()
const {
  mediaItems, loading, error,
  fetchMediaItems, approveMediaItem, rejectMediaItem
} = useMediaItems()

const {
  memories, loading: memoriesLoading,
  fetchMemories, approveMemory, rejectMemory
} = useMemories()

onMounted(() => {
  fetchMediaItems({ approved: false })
  fetchMemories({ approved: false })
})

// Preview dialog state
const previewItem = ref<MediaItem | null>(null)
const showPreview = ref(false)

const openPreview = (item: MediaItem) => {
  previewItem.value = item
  showPreview.value = true
}

const handleApproveMedia = async (id: string) => {
  const success = await approveMediaItem(id)
  if (success) {
    toast.add({ severity: 'success', summary: 'Aprobado', detail: 'Elemento aprobado y publicado', life: 3000 })
    // Remove from local list
    mediaItems.value = mediaItems.value.filter(i => i.id !== id)
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: 'Error al aprobar el elemento', life: 5000 })
  }
}

const handleRejectMedia = async (id: string) => {
  const success = await rejectMediaItem(id)
  if (success) {
    toast.add({ severity: 'info', summary: 'Rechazado', detail: 'Elemento rechazado', life: 3000 })
    mediaItems.value = mediaItems.value.filter(i => i.id !== id)
  }
}

const handleApproveMemory = async (id: string) => {
  const success = await approveMemory(id)
  if (success) {
    toast.add({ severity: 'success', summary: 'Aprobado', detail: 'Recuerdo aprobado y publicado', life: 3000 })
    memories.value = memories.value.filter(m => m.id !== id)
  }
}

const handleRejectMemory = async (id: string) => {
  const success = await rejectMemory(id)
  if (success) {
    toast.add({ severity: 'info', summary: 'Rechazado', detail: 'Recuerdo rechazado', life: 3000 })
    memories.value = memories.value.filter(m => m.id !== id)
  }
}
</script>
```

**Template:**

Two sections:

1. **Media Items Review** — `DataTable` with columns:
   - Thumbnail/preview (clickable, opens `Dialog` modal)
   - Title
   - Type (rendered as `Tag` with severity based on type)
   - Uploader name
   - Year
   - Context
   - Created date (formatted)
   - Actions: Approve (green `Button`, `pi-check`) and Reject (red `Button`, `pi-times`)

2. **Written Memories Review** — `DataTable` with columns:
   - Title
   - Content (truncated)
   - Author name
   - Year
   - Created date
   - Actions: Approve / Reject

3. **Empty state**: "No hay elementos pendientes de revisión" when both lists are empty.

4. **Preview Dialog**: Shows full image/video/audio player for the selected media item.

**Styling:**
- Use PrimeVue `DataTable` with `stripedRows`, `paginator` (5 rows per page)
- `Tag` severity mapping: Photo → `success`, Video → `info`, Audio → `warn`, Document → `secondary`
- Amber accent colors consistent with anniversary theme
- Responsive: table scrolls horizontally on mobile

---

### Step 6: Update AdminPage.vue

**File**: `frontend/src/views/AdminPage.vue`

**Action**: Add a "Revisión de medios" tab to the admin panel.

**Changes:**

1. **Import** the new component:
   ```typescript
   import MediaItemsReviewPanel from '@/components/admin/MediaItemsReviewPanel.vue'
   ```

2. **Add a new `Tab`** (visible to Board and Admin roles):
   ```html
   <Tab v-if="auth.isBoard" value="4" data-testid="tab-media-review">
     <i class="pi pi-images mr-2" />
     Revisión de medios
   </Tab>
   ```

3. **Add a new `TabPanel`**:
   ```html
   <TabPanel v-if="auth.isBoard" value="4" data-testid="panel-media-review">
     <div class="py-4">
       <MediaItemsReviewPanel />
     </div>
   </TabPanel>
   ```

**Notes:**
- Use `auth.isBoard` (not `auth.isAdmin`) since both Admin and Board can review.
- Tab value `"4"` follows after the existing `"3"` (Storage tab).
- The existing Storage tab uses `auth.isAdmin` — keep it as is. The media review tab uses `auth.isBoard`.

---

### Step 7: Write Vitest Unit Tests

#### Test file: `frontend/src/composables/__tests__/useMediaItems.test.ts`

| Test | Description |
|---|---|
| `fetchMediaItems should call GET /media-items with query params` | Verify API called with correct URL |
| `fetchMediaItems should update mediaItems ref on success` | State updated |
| `fetchMediaItems should set error on API failure` | Error state set |
| `createMediaItem should call POST /media-items` | Verify API call |
| `createMediaItem should return created item on success` | Return value correct |
| `createMediaItem should return null and set error on failure` | Error handling |
| `approveMediaItem should call PATCH /media-items/{id}/approve` | Correct URL |
| `rejectMediaItem should call PATCH /media-items/{id}/reject` | Correct URL |
| `deleteMediaItem should call DELETE /media-items/{id}` | Correct URL |

**Mock pattern:** Mock `@/utils/api` module:
```typescript
vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  }
}))
```

#### Test file: `frontend/src/composables/__tests__/useMemories.test.ts`

| Test | Description |
|---|---|
| `fetchMemories should call GET /memories with query params` | API called correctly |
| `createMemory should call POST /memories` | API called correctly |
| `approveMemory should call PATCH /memories/{id}/approve` | Correct endpoint |
| `rejectMemory should call PATCH /memories/{id}/reject` | Correct endpoint |

---

#### Test file: `frontend/src/components/anniversary/__tests__/AnniversaryUploadForm.test.ts`

**Update existing tests** and add new ones:

| Test | Description |
|---|---|
| `should show validation error when name is empty` | (existing — keep) |
| `should show validation error when content type not selected` | (existing — keep) |
| `should show file required error for photo/video/audio without file` | New: file validation |
| `should show description required error for historia without description` | New: story validation |
| `should call blob upload then createMediaItem on photo submission` | New: full upload flow mock |
| `should call createMemory on historia submission` | New: memory creation flow mock |
| `should show progress bar during upload` | New: loading state |
| `should show success toast after successful upload` | New: success message |
| `should show error toast on API failure` | New: error handling |
| `should reset form after successful submission` | (existing test may need update) |
| `should disable submit button during upload` | New: button state |

**Mock approach:**
```typescript
const mockUploadFile = vi.fn()
const mockCreateMediaItem = vi.fn()
const mockCreateMemory = vi.fn()

vi.mock('@/composables/useBlobStorage', () => ({
  useBlobStorage: () => ({
    uploadFile: mockUploadFile,
    uploading: ref(false),
    uploadError: ref(null),
  })
}))

vi.mock('@/composables/useMediaItems', () => ({
  useMediaItems: () => ({
    createMediaItem: mockCreateMediaItem,
    creating: ref(false),
    createError: ref(null),
  })
}))

vi.mock('@/composables/useMemories', () => ({
  useMemories: () => ({
    createMemory: mockCreateMemory,
    creating: ref(false),
    createError: ref(null),
  })
}))
```

---

#### Test file: `frontend/src/components/anniversary/__tests__/AnniversaryGallery.test.ts`

| Test | Description |
|---|---|
| `should call fetchMediaItems with approved=true and context on mount` | API called with correct params |
| `should show loading skeletons while fetching` | Skeleton elements visible |
| `should render photo items with Image component` | PrimeVue Image rendered |
| `should render audio items with audio element` | `<audio>` element present |
| `should render video items with video element` | `<video>` element present |
| `should show empty state when no items returned` | Empty state message shown |

**Mock approach:** Mock `useMediaItems` composable to return controlled state.

---

#### Test file: `frontend/src/components/admin/__tests__/MediaItemsReviewPanel.test.ts`

| Test | Description |
|---|---|
| `should fetch unapproved items on mount` | `fetchMediaItems({ approved: false })` called |
| `should fetch unapproved memories on mount` | `fetchMemories({ approved: false })` called |
| `should call approveMediaItem on approve button click` | Approve endpoint triggered |
| `should call rejectMediaItem on reject button click` | Reject endpoint triggered |
| `should remove item from list after approve` | Local state updated |
| `should show empty state when no pending items` | Empty message shown |
| `should show toast on successful approve` | Toast called |

---

### Step 8: Update Technical Documentation

- **Action**: Review and update documentation per changes
- **Files to update**:
  1. If any frontend standards were modified (e.g., new composable patterns), update `ai-specs/specs/frontend-standards.mdc`
  2. Update the enriched spec to mark frontend items as completed
- **Notes**: This step is mandatory before the implementation is considered complete.

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-media-50-aniversary-frontend`
2. **Step 1**: Create TypeScript interfaces (`media-item.ts`, `memory.ts`)
3. **Step 2**: Create composables (`useMediaItems.ts`, `useMemories.ts`)
4. **Step 3**: Update `AnniversaryUploadForm.vue` with real API integration
5. **Step 4**: Update `AnniversaryGallery.vue` with real data
6. **Step 5**: Create `MediaItemsReviewPanel.vue` admin component
7. **Step 6**: Update `AdminPage.vue` to add media review tab
8. **Step 7**: Write Vitest unit tests for composables and components
9. **Step 8**: Update technical documentation

---

## Testing Checklist

- [ ] `useMediaItems` composable tests pass (all CRUD + approve/reject)
- [ ] `useMemories` composable tests pass (all CRUD + approve/reject)
- [ ] `AnniversaryUploadForm` tests pass (validation, upload flow, error handling)
- [ ] `AnniversaryGallery` tests pass (fetch, render by type, loading/empty states)
- [ ] `MediaItemsReviewPanel` tests pass (fetch, approve, reject, toast)
- [ ] Manual testing: photo upload end-to-end works
- [ ] Manual testing: written story submission works
- [ ] Manual testing: gallery shows approved items
- [ ] Manual testing: admin can approve/reject items
- [ ] TypeScript: no `any` types, strict mode passes
- [ ] `npm run lint` passes with no errors
- [ ] `npm run build` succeeds

---

## Error Handling Patterns

### Composable Error State

Each composable exposes:
- `loading: Ref<boolean>` — true during API calls
- `error: Ref<string | null>` — error message on failure, null on success

### Error Message Extraction

Follow the existing `useBlobStorage.ts` pattern:
```typescript
type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }

catch (err: unknown) {
  error.value = (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error fallback message'
  console.error('Operation failed:', err)
}
```

### User-Facing Errors

- **Upload form**: Show PrimeVue `Toast` with `severity: 'error'` on API failures
- **Gallery**: Show inline error message if fetch fails
- **Admin panel**: Show `Toast` on approve/reject failures

### Special Cases

- **413 status** (file too large): Handled in `useBlobStorage` — shows "El archivo supera el tamaño máximo permitido"
- **401 status** (token expired): Handled globally by axios interceptor in `api.ts` — redirects to login
- **403 status** (insufficient role): Show "No tienes permisos para esta acción"

---

## UI/UX Considerations

### PrimeVue Components Used

| Component | Where | Purpose |
|---|---|---|
| `ProgressBar` (mode="indeterminate") | Upload form | Show upload progress |
| `Skeleton` | Gallery | Loading placeholders |
| `Image` (with preview) | Gallery | Photo display with zoom |
| `DataTable` + `Column` | Admin review | Tabular item list |
| `Tag` | Admin review | Media type badges |
| `Dialog` | Admin review | Full preview modal |
| `Button` | Multiple | Actions (submit, approve, reject) |
| `Toast` | Multiple | Success/error notifications |

### Responsive Design

- **Gallery grid**: `grid-cols-1 sm:grid-cols-3 lg:grid-cols-4` (kept from current)
- **Upload form**: `max-w-2xl mx-auto` (kept from current)
- **Admin DataTable**: `scrollable` for horizontal scroll on mobile
- **Audio player**: Full width within card container

### Accessibility

- `aria-label` on `<audio>` and `<video>` elements with item title
- `alt` text on images using item title
- Form labels with `for` attributes (already present)
- Keyboard navigation for approve/reject buttons
- `role="status"` on progress indicators

### Loading States

- **Upload form**: Indeterminate `ProgressBar` + disabled submit button with PrimeVue loading spinner
- **Gallery**: Skeleton cards (8 placeholders matching grid layout)
- **Admin panel**: DataTable `loading` prop with built-in spinner

---

## Dependencies

### npm Packages

No new npm packages required. All PrimeVue components used are already available:
- `primevue/progressbar` — ProgressBar
- `primevue/skeleton` — Skeleton
- `primevue/datatable` — DataTable
- `primevue/column` — Column
- `primevue/tag` — Tag
- `primevue/dialog` — Dialog
- `primevue/image` — Image (already used)
- `primevue/button` — Button (already used)

---

## Notes

### Business Rules
- Uploaded items start with `isApproved = false, isPublished = false` — never shown in gallery until approved
- Gallery fetches only `approved=true` items with `context=anniversary-50`
- Written stories (`historia` type) create a `Memory` entity, not a `MediaItem`
- For photos, `generateThumbnail: true` is sent to blob storage
- For videos/audio, `generateThumbnail: false` (no server-side thumbnail)
- Admin panel review shows items with `approved=false`

### Content Type Mapping
- `foto` → Blob upload + `MediaItem` with `type: 'Photo'`
- `video` → Blob upload + `MediaItem` with `type: 'Video'`
- `audio` → Blob upload + `MediaItem` with `type: 'Audio'`
- `historia` → No blob upload → `Memory` with content from description field

### Language
- All user-facing text in **Spanish**
- All code (variables, functions, types, comments) in **English**

### TypeScript
- Strict typing throughout — no `any` types
- Use `<script setup lang="ts">` for all components
- Import types with `import type { ... }` where possible

---

## Next Steps After Implementation

1. **Integration testing** with backend — verify end-to-end upload flow
2. **Backend branch merge** — ensure backend endpoints are available
3. **Cypress E2E tests** — optional follow-up for critical flows (upload, approve)
4. **Performance optimization** — lazy loading for gallery if item count grows

---

## Implementation Verification

- [ ] **Code Quality**: TypeScript strict, no `any`, `<script setup lang="ts">` on all components
- [ ] **Functionality**: Upload form submits to real API, gallery shows real data, admin can review
- [ ] **Testing**: Vitest unit tests pass for composables and components
- [ ] **Integration**: Composables connect to backend API correctly
- [ ] **UI/UX**: Loading states, error states, empty states all handled
- [ ] **Accessibility**: ARIA labels, alt text, keyboard navigation
- [ ] **Responsive**: Works on mobile, tablet, desktop
- [ ] **Documentation**: Updated per Step 8

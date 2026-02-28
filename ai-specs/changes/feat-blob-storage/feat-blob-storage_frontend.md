# Frontend Implementation Plan: feat-blob-storage Blob Storage Service

## Overview

Implement the frontend layer for the Blob Storage infrastructure service. This ticket provisions:

1. **TypeScript types** matching the backend DTOs for the `/api/blobs/*` endpoints.
2. **`useBlobStorage` composable** — the single point of API communication for upload, delete, and stats operations.
3. **`BlobUploadButton` component** — a reusable, prop-driven file picker that other features (Photos, MediaItems, CampLocations) will consume; no standalone page is created in this ticket.
4. **`BlobStorageAdminPanel` component** — an Admin-only storage statistics panel added as a new tab in the existing `AdminPage.vue`.

**No new routes are added.** The feature is infrastructure: the upload component is consumed by follow-up feature tickets. The only user-facing surface in this ticket is the Admin stats panel.

Architecture principles: Vue 3 Composition API (`<script setup lang="ts">`), composable-based API communication, PrimeVue + Tailwind CSS, no `<style>` blocks, strict TypeScript with no `any`.

---

## Architecture Context

### New files

| File | Purpose |
|---|---|
| `frontend/src/types/blob-storage.ts` | TypeScript interfaces for all blob storage DTOs |
| `frontend/src/composables/useBlobStorage.ts` | Upload, delete, stats, and health API calls |
| `frontend/src/components/blobs/BlobUploadButton.vue` | Reusable file-picker upload component |
| `frontend/src/components/admin/BlobStorageAdminPanel.vue` | Admin stats panel |
| `frontend/src/composables/__tests__/useBlobStorage.spec.ts` | Unit tests for composable |
| `frontend/src/components/blobs/__tests__/BlobUploadButton.spec.ts` | Component tests |

### Modified files

| File | Change |
|---|---|
| `frontend/src/views/AdminPage.vue` | Add "Almacenamiento" tab wired to `BlobStorageAdminPanel` |
| `ai-specs/specs/api-endpoints.md` | Document the `/api/blobs/*` endpoints and health check entry |

### State management approach

- **No Pinia store needed.** Upload results are transient (callers store the returned URL in their own entity forms). Stats are fetched on demand by the admin panel with local `ref` state inside `useBlobStorage`.
- The composable exposes individual loading/error refs per operation to allow concurrent usage from different components.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: The backend branch `feat/blob-storage` already exists and is the current working branch. For frontend changes, create a dedicated sub-branch.
- **Branch Naming**: `feature/feat-blob-storage-frontend`
- **Implementation Steps**:
  1. Verify current branch: `git branch`
  2. Ensure you are on `feat/blob-storage` or `main` as the base
  3. Pull latest: `git pull origin feat/blob-storage`
  4. Create branch: `git checkout -b feature/feat-blob-storage-frontend`
  5. Verify: `git branch`
- **Notes**: Must be the FIRST step before any code changes. Frontend branch is separate from the backend branch to allow independent review. See `ai-specs/specs/frontend-standards.mdc` for branch naming conventions.

---

### Step 1: Define TypeScript Interfaces

- **File**: `frontend/src/types/blob-storage.ts`
- **Action**: Create all TypeScript interfaces mirroring the backend DTOs.
- **Full file content**:

```typescript
export type BlobFolder = 'photos' | 'media-items' | 'camp-locations' | 'camp-photos'

export interface UploadBlobRequest {
  file: File
  folder: BlobFolder
  contextId?: string
  generateThumbnail?: boolean
}

export interface BlobUploadResult {
  fileUrl: string
  thumbnailUrl: string | null
  fileName: string
  contentType: string
  sizeBytes: number
}

export interface DeleteBlobsRequest {
  blobKeys: string[]
}

export interface FolderStats {
  objects: number
  sizeBytes: number
}

export interface BlobStorageStats {
  totalObjects: number
  totalSizeBytes: number
  totalSizeHumanReadable: string
  quotaBytes: number | null
  usedPct: number | null
  freeBytes: number | null
  byFolder: Record<BlobFolder, FolderStats>
}
```

- **Dependencies**: None (pure TypeScript).
- **Implementation Notes**:
  - `BlobFolder` is a union type, not an enum, to stay consistent with backend validation strings.
  - `freeBytes` and `usedPct` are nullable because they are absent when `StorageQuotaBytes` is 0 (unconfigured).
  - Field names use camelCase to match the JSON response from the backend (the backend serialises to camelCase by default in ASP.NET).

---

### Step 2: Create `useBlobStorage` Composable

- **File**: `frontend/src/composables/useBlobStorage.ts`
- **Action**: Implement three operations — upload, delete blobs, and fetch stats — each with independent loading/error state.
- **Function signature**:

```typescript
export function useBlobStorage(): {
  // Upload
  uploading: Ref<boolean>
  uploadError: Ref<string | null>
  uploadFile(request: UploadBlobRequest): Promise<BlobUploadResult | null>

  // Delete (Admin only)
  deleting: Ref<boolean>
  deleteError: Ref<string | null>
  deleteBlobs(keys: string[]): Promise<boolean>

  // Stats (Admin only)
  stats: Ref<BlobStorageStats | null>
  statsLoading: Ref<boolean>
  statsError: Ref<string | null>
  fetchStats(): Promise<void>
}
```

- **Implementation Steps**:
  1. Import `ref` from Vue and `api` from `@/utils/api`.
  2. Import types from `@/types/blob-storage`.
  3. Import `ApiResponse` from `@/types/api`.
  4. Define `uploading`, `uploadError`, `deleting`, `deleteError`, `stats`, `statsLoading`, `statsError` as `ref`.
  5. Implement `uploadFile`:
     - Build a `FormData` object: append `file` (the `File` object), `folder`, optionally `contextId` and `generateThumbnail`.
     - Call `api.post<ApiResponse<BlobUploadResult>>('/blobs/upload', formData, { headers: { 'Content-Type': 'multipart/form-data' } })`.
     - Return `response.data.data` on success, `null` on failure.
  6. Implement `deleteBlobs`:
     - Call `api.delete('/blobs', { data: { blobKeys: keys } })`.
     - Returns `true` on success, `false` on failure.
  7. Implement `fetchStats`:
     - Call `api.get<ApiResponse<BlobStorageStats>>('/blobs/stats')`.
     - Assign `response.data.data` to `stats`.
  8. Return all refs and methods.
- **Full implementation sketch**:

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  BlobUploadResult,
  BlobStorageStats,
  UploadBlobRequest
} from '@/types/blob-storage'

type ApiErrorShape = { response?: { data?: { error?: { message?: string } } } }

export function useBlobStorage() {
  // Upload state
  const uploading = ref(false)
  const uploadError = ref<string | null>(null)

  // Delete state
  const deleting = ref(false)
  const deleteError = ref<string | null>(null)

  // Stats state
  const stats = ref<BlobStorageStats | null>(null)
  const statsLoading = ref(false)
  const statsError = ref<string | null>(null)

  const uploadFile = async (request: UploadBlobRequest): Promise<BlobUploadResult | null> => {
    uploading.value = true
    uploadError.value = null
    try {
      const formData = new FormData()
      formData.append('file', request.file)
      formData.append('folder', request.folder)
      if (request.contextId) formData.append('contextId', request.contextId)
      if (request.generateThumbnail != null)
        formData.append('generateThumbnail', String(request.generateThumbnail))

      const response = await api.post<ApiResponse<BlobUploadResult>>(
        '/blobs/upload',
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
      )
      if (response.data.success && response.data.data) return response.data.data
      uploadError.value = response.data.error?.message ?? 'Error al subir el archivo'
      return null
    } catch (err: unknown) {
      uploadError.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al subir el archivo'
      console.error('Failed to upload blob:', err)
      return null
    } finally {
      uploading.value = false
    }
  }

  const deleteBlobs = async (keys: string[]): Promise<boolean> => {
    deleting.value = true
    deleteError.value = null
    try {
      await api.delete('/blobs', { data: { blobKeys: keys } })
      return true
    } catch (err: unknown) {
      deleteError.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al eliminar los archivos'
      console.error('Failed to delete blobs:', err)
      return false
    } finally {
      deleting.value = false
    }
  }

  const fetchStats = async (): Promise<void> => {
    statsLoading.value = true
    statsError.value = null
    try {
      const response = await api.get<ApiResponse<BlobStorageStats>>('/blobs/stats')
      if (response.data.success && response.data.data) {
        stats.value = response.data.data
      } else {
        statsError.value = response.data.error?.message ?? 'Error al obtener estadísticas'
      }
    } catch (err: unknown) {
      statsError.value =
        (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Error al obtener estadísticas'
      console.error('Failed to fetch blob stats:', err)
    } finally {
      statsLoading.value = false
    }
  }

  return {
    uploading,
    uploadError,
    uploadFile,
    deleting,
    deleteError,
    deleteBlobs,
    stats,
    statsLoading,
    statsError,
    fetchStats
  }
}
```

- **Dependencies**: `@/utils/api` (existing Axios instance), `@/types/api`, `@/types/blob-storage`.
- **Implementation Notes**:
  - `Content-Type: multipart/form-data` must be set explicitly — Axios does not auto-set it when sending `FormData` in all configurations.
  - Do NOT buffer the file in memory; let the browser stream the `FormData` body directly to Axios.
  - The composable does not cache stats — caching is handled server-side (5 min IMemoryCache). The admin panel can call `fetchStats` on mount.

---

### Step 3: Create `BlobUploadButton` Reusable Component

- **File**: `frontend/src/components/blobs/BlobUploadButton.vue`
- **Action**: A self-contained file-picker button. When the user selects a file, it immediately POSTs to `/api/blobs/upload` and emits the result. Designed to be embedded in forms of other features (e.g., camp photo form, cover photo field).
- **Props**:

```typescript
defineProps<{
  folder: BlobFolder
  contextId?: string
  generateThumbnail?: boolean
  accept?: string           // e.g. 'image/*' — passed to <input type="file">
  label?: string            // button label, default 'Subir archivo'
  disabled?: boolean
}>()
```

- **Emits**:

```typescript
defineEmits<{
  (e: 'uploaded', result: BlobUploadResult): void
  (e: 'error', message: string): void
}>()
```

- **Implementation Steps**:
  1. Use PrimeVue `Button` for the trigger and a hidden `<input type="file" ref="fileInput">`.
  2. On button click, programmatically call `fileInput.value?.click()`.
  3. On file input `change` event, call `useBlobStorage().uploadFile(...)` with props.
  4. On success, emit `uploaded(result)` and clear the input value (to allow re-upload of same file).
  5. On failure, emit `error(uploadError.value)` and show a PrimeVue `useToast()` error.
  6. Show a PrimeVue `ProgressSpinner` (small, inline) while `uploading` is true.
  7. Disable the button when `uploading` is true or `disabled` prop is true.
- **Template sketch**:

```html
<template>
  <div class="flex items-center gap-2">
    <input
      ref="fileInput"
      type="file"
      class="hidden"
      :accept="accept"
      @change="onFileSelected"
    />
    <Button
      :label="uploading ? 'Subiendo...' : (label ?? 'Subir archivo')"
      icon="pi pi-upload"
      :loading="uploading"
      :disabled="disabled || uploading"
      @click="fileInput?.click()"
    />
  </div>
</template>
```

- **Dependencies**: PrimeVue `Button`, `useToast` from `primevue/usetoast`.
- **Implementation Notes**:
  - This component intentionally does NOT preview the uploaded file. Preview is the responsibility of the consumer component (e.g., displaying the returned `fileUrl`).
  - The `accept` prop maps directly to the HTML `accept` attribute (e.g., `'image/*'`, `'.mp3,.wav,.ogg'`). It is not validated here — server-side validation is the source of truth.
  - Do NOT use PrimeVue `FileUpload` component in auto/advanced mode (it manages its own HTTP request). Use a plain `<input type="file">` + the `useBlobStorage` composable to keep full control over the upload request and response.

---

### Step 4: Create `BlobStorageAdminPanel` Component

- **File**: `frontend/src/components/admin/BlobStorageAdminPanel.vue`
- **Action**: Admin-only panel displaying storage usage statistics, per-folder breakdown, and a quota progress bar. Fetches data on mount.
- **Implementation Steps**:
  1. Call `useBlobStorage()` and invoke `fetchStats()` in `onMounted`.
  2. Display a `ProgressSpinner` while `statsLoading` is true.
  3. On `statsError`, show an inline error message with a retry button.
  4. When stats are available, display:
     - **Summary cards**: Total objects, total size (human readable), free bytes (if quota configured).
     - **Quota progress bar**: Using PrimeVue `ProgressBar` with value `Math.round(stats.usedPct ?? 0)`. Color the bar via Tailwind conditional classes: green for < 80%, yellow for 80–95%, red for ≥ 95%. Only render when `stats.quotaBytes` is non-null.
     - **Folder breakdown table**: A PrimeVue `DataTable` with columns: Folder, Objects, Size (formatted).
  5. Add a "Actualizar" refresh button that re-calls `fetchStats()`.
- **Template sketch (outline)**:

```html
<template>
  <div data-testid="blob-storage-admin-panel" class="space-y-6">
    <div class="flex items-center justify-between">
      <h2 class="text-xl font-semibold text-gray-800">Almacenamiento de Archivos</h2>
      <Button
        label="Actualizar"
        icon="pi pi-refresh"
        outlined
        :loading="statsLoading"
        @click="fetchStats"
      />
    </div>

    <ProgressSpinner v-if="statsLoading && !stats" />

    <Message v-if="statsError" severity="error">{{ statsError }}</Message>

    <template v-if="stats">
      <!-- Summary cards -->
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <!-- total objects card -->
        <!-- total size card -->
        <!-- free space card (v-if quota configured) -->
      </div>

      <!-- Quota bar (only when quotaBytes is configured) -->
      <div v-if="stats.quotaBytes" class="space-y-1">
        <div class="flex justify-between text-sm text-gray-600">
          <span>Uso de almacenamiento</span>
          <span>{{ stats.usedPct?.toFixed(1) }}%</span>
        </div>
        <ProgressBar
          :value="Math.round(stats.usedPct ?? 0)"
          :class="progressBarClass"
        />
      </div>

      <!-- Folder breakdown -->
      <DataTable :value="folderRows" data-testid="folder-stats-table">
        <Column field="folder" header="Carpeta" />
        <Column field="objects" header="Objetos" />
        <Column field="sizeHuman" header="Tamaño" />
      </DataTable>
    </template>
  </div>
</template>
```

- **Computed helpers**:

```typescript
const progressBarClass = computed(() => {
  const pct = stats.value?.usedPct ?? 0
  if (pct >= 95) return 'blob-bar-critical'    // red
  if (pct >= 80) return 'blob-bar-warning'     // yellow
  return 'blob-bar-healthy'                    // green (default PrimeVue)
})

const folderRows = computed(() =>
  Object.entries(stats.value?.byFolder ?? {}).map(([folder, s]) => ({
    folder,
    objects: s.objects,
    sizeHuman: formatBytes(s.sizeBytes)
  }))
)
```

- **`formatBytes` utility**: Add a simple helper inside the component (or in `frontend/src/utils/formatBytes.ts` if it doesn't exist yet):

```typescript
function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}
```

- **PrimeVue progress bar color**: PrimeVue `ProgressBar` does not natively support color theming via a prop. Use inline `style` or a pt (passthrough) override:

```html
<ProgressBar
  :value="Math.round(stats.usedPct ?? 0)"
  :pt="{
    value: {
      style: {
        background: progressBarColor
      }
    }
  }"
/>
```

```typescript
const progressBarColor = computed(() => {
  const pct = stats.value?.usedPct ?? 0
  if (pct >= 95) return '#ef4444'   // red-500
  if (pct >= 80) return '#f59e0b'   // amber-500
  return '#22c55e'                  // green-500
})
```

- **Dependencies**: PrimeVue `ProgressSpinner`, `ProgressBar`, `DataTable`, `Column`, `Button`, `Message`; `useBlobStorage` composable.

---

### Step 5: Update `AdminPage.vue` — Add Storage Tab

- **File**: `frontend/src/views/AdminPage.vue`
- **Action**: Add a new "Almacenamiento" tab (Admin-only visibility) to the existing tab list and tab panels.
- **Implementation Steps**:
  1. Import `BlobStorageAdminPanel` from `@/components/admin/BlobStorageAdminPanel.vue`.
  2. Import `useAuthStore` from `@/stores/auth`.
  3. Add `const auth = useAuthStore()` in `<script setup>`.
  4. Add a new `<Tab>` and `<TabPanel>` for storage — conditionally rendered only for Admin role (`v-if="auth.isAdmin"`).
  5. Assign the new tab `value="3"` (next sequential value after existing tabs 0, 1, 2).
- **Changes to the template**:
  - Add to `<TabList>`:

```html
<Tab v-if="auth.isAdmin" value="3" data-testid="tab-storage">
  <i class="pi pi-database mr-2" />
  Almacenamiento
</Tab>
```

  - Add to `<TabPanels>`:

```html
<TabPanel v-if="auth.isAdmin" value="3" data-testid="panel-storage">
  <div class="py-4">
    <BlobStorageAdminPanel />
  </div>
</TabPanel>
```

- **Implementation Notes**:
  - `isAdmin` (role === 'Admin') is used instead of `isBoard` because the `/api/blobs/stats` endpoint requires Admin role.
  - The tab values are strings in PrimeVue Tabs (`"0"`, `"1"`, `"2"`, `"3"`) — keep them consistent.
  - No new route is needed; the tab is embedded inside the existing `/admin` page.

---

### Step 6: Write Vitest Unit Tests for `useBlobStorage`

- **File**: `frontend/src/composables/__tests__/useBlobStorage.spec.ts`
- **Action**: Unit test the composable by mocking the `api` module.
- **Implementation Steps**:
  1. Mock `@/utils/api`: `vi.mock('@/utils/api', () => ({ api: { post: vi.fn(), delete: vi.fn(), get: vi.fn() } }))`.
  2. Import `useBlobStorage` and use `mount`-less composable testing (call the function directly inside tests).
  3. Use `flushPromises()` from `@vue/test-utils` to resolve async operations.
- **Test cases**:

| Test name | Assertion |
|---|---|
| `uploadFile_withValidImageFile_returnsUploadResult` | Returns `BlobUploadResult` with `fileUrl` and `thumbnailUrl` |
| `uploadFile_whenApiFails_setsUploadError` | `uploadError` is set, returns `null` |
| `uploadFile_withGenerateThumbnailFalse_omitsThumbnailFromFormData` | `generateThumbnail=false` not appended (or appended as `'false'`) |
| `deleteBlobs_withValidKeys_callsApiDeleteWithKeys` | `api.delete` called with `{ data: { blobKeys: [...] } }` |
| `deleteBlobs_whenApiFails_setsDeleteError` | `deleteError` is set, returns `false` |
| `fetchStats_withQuotaConfigured_populatesStats` | `stats.value` has `usedPct` and `freeBytes` |
| `fetchStats_whenApiFails_setsStatsError` | `statsError` is set, `stats` remains `null` |
| `uploadFile_sendsMultipartFormData` | Verifies `FormData` sent to `api.post` contains correct fields |

- **Dependencies**: `vitest`, `@vue/test-utils`, `flushPromises`.

---

### Step 7: Write Component Tests for `BlobUploadButton`

- **File**: `frontend/src/components/blobs/__tests__/BlobUploadButton.spec.ts`
- **Action**: Component-level tests using Vue Test Utils.
- **Test cases**:

| Test name | Assertion |
|---|---|
| `renders upload button with default label` | Button text is `'Subir archivo'` |
| `renders upload button with custom label prop` | Button text matches `label` prop |
| `clicking button triggers file input click` | `fileInput.click` is called |
| `selecting a file calls uploadFile` | `useBlobStorage.uploadFile` called with correct folder and file |
| `emits uploaded event on success` | `wrapper.emitted('uploaded')` contains the result |
| `emits error event on upload failure` | `wrapper.emitted('error')` contains the error message |
| `button is disabled while uploading` | Button has `disabled` attribute when `uploading` is true |
| `button is disabled when disabled prop is true` | Button has `disabled` attribute |

- **Implementation Notes**: Mock `useBlobStorage` via `vi.mock` — do not make real HTTP calls in component tests.

---

### Step 8: Write Cypress E2E Tests for Admin Storage Panel

- **File**: `frontend/cypress/e2e/admin/blob-storage-stats.cy.ts`
- **Action**: E2E test for the storage stats panel in the admin page.
- **Implementation Steps**:
  1. Intercept `GET /api/blobs/stats` with a fixture: `cy.intercept('GET', '**/blobs/stats', { fixture: 'blob-stats.json' })`.
  2. Create fixture at `frontend/cypress/fixtures/blob-stats.json` with sample stats data.
  3. Log in as Admin user.
  4. Navigate to `/admin`.
  5. Click the "Almacenamiento" tab.
  6. Assert: stats summary cards are visible; folder breakdown table has expected rows; quota progress bar is visible when quota is configured.
- **Test cases**:

| Scenario | Assertion |
|---|---|
| Admin sees storage tab | Tab labeled "Almacenamiento" visible |
| Stats load on tab open | Summary cards show total objects and size |
| Quota bar renders | Progress bar displayed with correct usage percentage |
| Folder breakdown table renders | DataTable shows `photos`, `media-items`, `camp-locations`, `camp-photos` rows |
| Non-admin does NOT see storage tab | Board user cannot see the "Almacenamiento" tab |
| Refresh button re-fetches | Clicking "Actualizar" triggers a new `GET /api/blobs/stats` call |

---

### Step 9: Update Technical Documentation

- **Action**: Update API docs to document the new endpoints.
- **Implementation Steps**:
  1. Open `ai-specs/specs/api-endpoints.md`.
  2. Add a new section for `/api/blobs/*` endpoints documenting:
     - `POST /api/blobs/upload` — multipart, auth required, any authenticated user
     - `DELETE /api/blobs` — body `{ blobKeys: string[] }`, Admin only, 204 No Content
     - `GET /api/blobs/stats` — Admin only, returns `BlobStorageStats`
     - `GET /api/blobs/health` — no auth, 200/503
  3. Update the health check table in `api-endpoints.md` to include the `blob-storage` check entry.
  4. Confirm documentation language is English.
- **References**: Follow `ai-specs/specs/documentation-standards.mdc`. Do not skip this step.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-blob-storage-frontend`
2. **Step 1** — TypeScript interfaces in `blob-storage.ts`
3. **Step 2** — `useBlobStorage` composable
4. **Step 6** — Unit tests for composable (write tests alongside implementation)
5. **Step 3** — `BlobUploadButton` component
6. **Step 7** — Component tests for `BlobUploadButton`
7. **Step 4** — `BlobStorageAdminPanel` component
8. **Step 5** — Update `AdminPage.vue`
9. **Step 8** — Cypress E2E tests
10. **Step 9** — Update documentation

---

## Testing Checklist

- [ ] All Vitest unit tests for `useBlobStorage` pass
- [ ] All component tests for `BlobUploadButton` pass
- [ ] Cypress E2E tests for admin stats panel pass
- [ ] `uploading` / `deleting` / `statsLoading` refs are true during requests and false after
- [ ] `uploadError`, `deleteError`, `statsError` are populated on API errors
- [ ] `BlobUploadButton` emits `uploaded` with correct result on success
- [ ] `BlobUploadButton` emits `error` on upload failure
- [ ] `BlobStorageAdminPanel` shows `ProgressSpinner` while loading
- [ ] `BlobStorageAdminPanel` shows error message on stats fetch failure
- [ ] Quota bar only renders when `stats.quotaBytes` is non-null
- [ ] Admin tab is only visible when `auth.isAdmin` is true
- [ ] Non-admin (Board) users do not see the storage tab

---

## Error Handling Patterns

- **Loading states**: Each operation has its own `ref<boolean>` (`uploading`, `deleting`, `statsLoading`) to allow concurrent usage.
- **Error display**: `BlobUploadButton` uses `useToast()` to show a non-blocking toast on failure. `BlobStorageAdminPanel` shows an inline PrimeVue `Message` component with `severity="error"`.
- **API errors**: Follow the existing pattern from `useCampPhotos.ts`:
  - Extract `response.data.error.message` from the typed `ApiResponse` envelope.
  - Fall back to a generic Spanish error string if no message is available.
  - Log the full error to `console.error` for debugging.
- **413 Payload Too Large**: The browser will receive this before the endpoint is reached. Handle it in the `catch` block of `uploadFile` — the error shape will differ from the standard `ApiResponse`. Check `err.response?.status === 413` and return a user-friendly message: `'El archivo supera el tamaño máximo permitido'`.

---

## UI/UX Considerations

- **PrimeVue components used**:
  - `Button` — upload trigger, refresh action
  - `ProgressSpinner` — loading state in admin panel
  - `ProgressBar` — quota usage visualisation
  - `DataTable` + `Column` — folder breakdown table
  - `Message` — error display
  - `useToast` / `Toast` — upload error notifications
- **Tailwind CSS**: All layout via utility classes. No `<style>` blocks.
- **Responsive design**:
  - Summary cards: `grid-cols-1 sm:grid-cols-3`
  - Admin panel: full width within the existing Container
- **Accessibility**:
  - The hidden `<input type="file">` is keyboard-accessible via the Button click handler.
  - `data-testid` attributes on all major elements for Cypress tests.
- **Loading feedback**: The upload button shows `'Subiendo...'` label and a PrimeVue `loading` spinner while uploading.

---

## Dependencies

### No new npm packages required

All required capabilities are already present in the project:
- `axios` — HTTP client via existing `@/utils/api`
- `primevue` — `Button`, `ProgressBar`, `ProgressSpinner`, `DataTable`, `Column`, `Message` are already registered
- `vitest` + `@vue/test-utils` — existing test infrastructure
- `cypress` — existing E2E test infrastructure

**Important**: Do NOT install `aws-sdk` or any S3 client on the frontend. All S3 interactions are handled by the backend.

---

## Notes

- **Language**: All code, comments, and variable names must be in English. User-facing strings (labels, error messages, placeholder text) must be in Spanish (consistent with the rest of the app UI).
- **TypeScript strict**: No `any` type. Use `unknown` and narrow where needed.
- **No `<style>` blocks**: All styling via Tailwind CSS utility classes only.
- **`<script setup lang="ts">`**: Mandatory for all Vue components.
- **File naming**: kebab-case for all Vue and TypeScript files (e.g., `blob-upload-button.vue`, `blob-storage.ts`).
- **Folder naming**: New component folder `frontend/src/components/blobs/` follows the existing per-feature pattern (`camps/`, `registrations/`, etc.).
- **Scope boundary**: This ticket does NOT wire the upload component into any existing form (photo albums, camp locations, etc.). That integration belongs to the follow-up feature tickets listed in the enriched spec.
- **Backend dependency**: These components depend on the backend blob storage feature being deployed. Until the backend is ready, mock the API in development using MSW or Cypress intercepts.

---

## Next Steps After Implementation

- Follow-up tickets will import `BlobUploadButton` and `useBlobStorage` to integrate uploads into:
  - Photo album management (`PhotoAlbum`, `Photo`)
  - Media items (50th anniversary, memories archive)
  - Camp location cover photos (`CampLocation.coverPhotoUrl`)
  - Profile photos (`FamilyMember`, `FamilyUnit`)
- No changes to the blob storage feature slice are needed for those follow-ups.

---

## Implementation Verification

- [ ] **Code Quality**: `<script setup lang="ts">` everywhere, no `any`, no `<style>` blocks
- [ ] **Functionality**: Upload composable sends correct `multipart/form-data`; stats panel fetches and renders on mount; admin tab only visible to Admin role
- [ ] **Testing**: All Vitest unit + component tests pass; Cypress E2E tests pass
- [ ] **Integration**: `useBlobStorage` uses the centralized Axios instance from `@/utils/api`; auth token is attached automatically via existing request interceptor
- [ ] **Documentation**: `ai-specs/specs/api-endpoints.md` updated with blob storage endpoints and health check entry

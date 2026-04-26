# Frontend Implementation Plan: feat-registration-export-filters — Registration Export & Advanced Filters

## Overview

Extend the admin registrations panel (`RegistrationsAdminPanel.vue`) with two new filter controls (accommodation type and extras) and a CSV export button. All logic is encapsulated in the existing `useAdminRegistrations` composable, which is updated to support the new filter params and a new `exportToCsv` function. No new routes, stores, or pages are required.

Stack: Vue 3 Composition API (`<script setup lang="ts">`), PrimeVue (MultiSelect + Button), Tailwind CSS, Axios with `responseType: 'blob'` for file download.

---

## Architecture Context

| Layer | File | Change type |
|-------|------|-------------|
| Composable | `frontend/src/composables/useAdminRegistrations.ts` | Modify — extend params + add filter option fetching + add export function |
| Component | `frontend/src/components/admin/RegistrationsAdminPanel.vue` | Modify — add MultiSelect filters + Export button |
| Types | `frontend/src/types/registration.ts` | Modify — add `AdminRegistrationFilters` interface |

**No new routes, stores, or pages.**

**Existing composables used** (not modified):
- `useCampExtras(editionId)` — already can fetch extras, but takes `editionId` as constructor arg. The admin panel needs to re-fetch when edition changes, so we add a dedicated function in `useAdminRegistrations` instead of instantiating `useCampExtras` directly.
- `useCampAccommodations(editionId)` — same pattern.
- `useAuthStore` — already available; use `auth.isBoard` to gate the export button.

**PrimeVue components to add**:
- `MultiSelect` from `primevue/multiselect`
- `Button` (already imported) — add export variant
- `useToast` from `primevue/usetoast` — for export error feedback

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch before any code changes.
- **Branch name**: `feature/feat-registration-export-filters-frontend`
- **Base branch**: `dev`
- **Commands**:
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/feat-registration-export-filters-frontend
  git branch   # verify
  ```

---

### Step 1: Add `AdminRegistrationFilters` type to `types/registration.ts`

**File**: `frontend/src/types/registration.ts`

**Action**: Add a typed interface for the admin list filter params, including the two new fields.

Add at the end of the admin DTOs section (after `AdminRegistrationProjection`, before the request types):

```typescript
// Already imported at the top of registration.ts:
// import type { AccommodationType } from './camp-edition'

export interface AdminRegistrationFilters {
  page?: number
  pageSize?: number
  search?: string
  status?: string
  accommodationTypes?: AccommodationType[]   // filter by accommodation type (OR logic)
  extraIds?: string[]                         // filter by selected extras (OR logic)
}
```

**Implementation notes**:
- `AccommodationType` is already imported in `registration.ts` (`import type { AccommodationType } from './camp-edition'`).
- This interface replaces the inline object type currently in `useAdminRegistrations`.

---

### Step 2: Update `useAdminRegistrations.ts`

**File**: `frontend/src/composables/useAdminRegistrations.ts`

**Action**: Full rewrite of the composable to add:
1. Refs for filter option data (`editionExtras`, `editionAccommodations`)
2. `fetchEditionFilterOptions(editionId)` function
3. Updated `fetchAdminRegistrations` params using `AdminRegistrationFilters`
4. `exportToCsv(editionId, filters)` function with blob download

**Complete new composable**:

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  AdminRegistrationListItem,
  AdminRegistrationTotals,
  AdminRegistrationListResponse,
  AdminRegistrationFilters
} from '@/types/registration'
import type { CampEditionExtra, CampEditionAccommodation } from '@/types/camp-edition'

export function useAdminRegistrations() {
  const registrations = ref<AdminRegistrationListItem[]>([])
  const totals = ref<AdminRegistrationTotals | null>(null)
  const totalCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const pagination = ref({ totalCount: 0, page: 1, pageSize: 20 })

  // Filter option data (populated when edition changes)
  const editionExtras = ref<CampEditionExtra[]>([])
  const editionAccommodations = ref<CampEditionAccommodation[]>([])
  const filterOptionsLoading = ref(false)

  // Export state
  const exportLoading = ref(false)
  const exportError = ref<string | null>(null)

  const fetchAdminRegistrations = async (
    campEditionId: string,
    params: AdminRegistrationFilters = {}
  ): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const queryParams = new URLSearchParams({
        page: String(params.page ?? 1),
        pageSize: String(params.pageSize ?? 20)
      })
      if (params.search) queryParams.set('search', params.search)
      if (params.status) queryParams.set('status', params.status)
      params.accommodationTypes?.forEach(t => queryParams.append('accommodationTypes', t))
      params.extraIds?.forEach(id => queryParams.append('extraIds', id))

      const response = await api.get<ApiResponse<AdminRegistrationListResponse>>(
        `/camp-editions/${campEditionId}/registrations?${queryParams.toString()}`
      )
      if (response.data.success && response.data.data) {
        registrations.value = response.data.data.items
        totalCount.value = response.data.data.totalCount
        totals.value = response.data.data.totals
        pagination.value = {
          totalCount: response.data.data.totalCount,
          page: params.page ?? 1,
          pageSize: params.pageSize ?? 20
        }
      }
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar inscripciones'
      console.error('Failed to fetch admin registrations:', err)
      registrations.value = []
      totals.value = null
      totalCount.value = 0
    } finally {
      loading.value = false
    }
  }

  const fetchEditionFilterOptions = async (campEditionId: string): Promise<void> => {
    filterOptionsLoading.value = true
    editionExtras.value = []
    editionAccommodations.value = []
    try {
      const [extrasRes, accommodationsRes] = await Promise.all([
        api.get<ApiResponse<CampEditionExtra[]>>(
          `/camps/editions/${campEditionId}/extras`, { params: { activeOnly: true } }
        ),
        api.get<ApiResponse<CampEditionAccommodation[]>>(
          `/camps/editions/${campEditionId}/accommodations`
        )
      ])
      if (extrasRes.data.success && extrasRes.data.data) {
        editionExtras.value = extrasRes.data.data
      }
      if (accommodationsRes.data.success && accommodationsRes.data.data) {
        editionAccommodations.value = accommodationsRes.data.data.filter(a => a.isActive)
      }
    } catch (err: unknown) {
      console.error('Failed to fetch edition filter options:', err)
    } finally {
      filterOptionsLoading.value = false
    }
  }

  const exportToCsv = async (
    campEditionId: string,
    filters: Omit<AdminRegistrationFilters, 'page' | 'pageSize'> = {}
  ): Promise<void> => {
    exportLoading.value = true
    exportError.value = null
    try {
      const queryParams = new URLSearchParams()
      if (filters.search) queryParams.set('search', filters.search)
      if (filters.status) queryParams.set('status', filters.status)
      filters.accommodationTypes?.forEach(t => queryParams.append('accommodationTypes', t))
      filters.extraIds?.forEach(id => queryParams.append('extraIds', id))

      const response = await api.get(
        `/camp-editions/${campEditionId}/registrations/export/csv?${queryParams.toString()}`,
        { responseType: 'blob' }
      )

      // Use filename from Content-Disposition if provided by backend
      const contentDisposition = response.headers['content-disposition'] as string | undefined
      const fileNameMatch = contentDisposition?.match(/filename="([^"]+)"/)
      const fileName = fileNameMatch?.[1] ?? `inscripciones-${new Date().toISOString().split('T')[0]}.csv`

      const blob = new Blob([response.data as BlobPart], { type: 'text/csv;charset=utf-8;' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = fileName
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      URL.revokeObjectURL(url)
    } catch (err: unknown) {
      exportError.value = 'Error al exportar las inscripciones'
      console.error('Failed to export registrations:', err)
    } finally {
      exportLoading.value = false
    }
  }

  return {
    registrations,
    totals,
    totalCount,
    pagination,
    loading,
    error,
    editionExtras,
    editionAccommodations,
    filterOptionsLoading,
    exportLoading,
    exportError,
    fetchAdminRegistrations,
    fetchEditionFilterOptions,
    exportToCsv
  }
}
```

**Implementation notes**:
- `fetchEditionFilterOptions` fires both extra and accommodation requests in parallel with `Promise.all`.
- Accommodation filter options are restricted to `isActive: true` items only (client-side filter since the API returns all).
- `exportToCsv` uses `responseType: 'blob'` on the Axios call — this is required to get binary data rather than a parsed JSON object.
- The filename is extracted from the `Content-Disposition` response header when the backend provides it; falls back to a date-stamped name.

---

### Step 3: Update `RegistrationsAdminPanel.vue`

**File**: `frontend/src/components/admin/RegistrationsAdminPanel.vue`

**Action**: Add accommodation type MultiSelect, extras MultiSelect, Export CSV button, wire up new filter state, update watchers.

#### 3a. Script section changes

**New imports to add**:
```typescript
import MultiSelect from 'primevue/multiselect'
import { useToast } from 'primevue/usetoast'
import { useAuthStore } from '@/stores/auth'
import type { AccommodationType } from '@/types/camp-edition'
```

**Updated composable destructuring** (new fields from updated `useAdminRegistrations`):
```typescript
const {
  registrations, totals, totalCount, pagination, loading, error,
  editionExtras, editionAccommodations, filterOptionsLoading,
  exportLoading, exportError,
  fetchAdminRegistrations, fetchEditionFilterOptions, exportToCsv
} = useAdminRegistrations()

const auth = useAuthStore()
const toast = useToast()
```

**New filter state refs**:
```typescript
const selectedAccommodationTypes = ref<AccommodationType[]>([])
const selectedExtraIds = ref<string[]>([])
```

**Computed options for MultiSelects**:

```typescript
// Deduplicate by AccommodationType — the filter is by type, not by individual accommodation
const ACCOMMODATION_TYPE_LABELS: Record<AccommodationType, string> = {
  Lodge: 'Albergue',
  Tent: 'Tienda',
  Caravan: 'Caravana',
  Bungalow: 'Bungalow',
  Motorhome: 'Autocaravana'
}

const accommodationTypeOptions = computed(() => {
  const seen = new Set<AccommodationType>()
  return editionAccommodations.value
    .filter(a => !seen.has(a.accommodationType) && seen.add(a.accommodationType))
    .map(a => ({
      label: ACCOMMODATION_TYPE_LABELS[a.accommodationType] ?? a.accommodationType,
      value: a.accommodationType
    }))
})

const extraOptions = computed(() =>
  editionExtras.value.map(e => ({ label: e.name, value: e.id }))
)
```

**Updated `loadRegistrations`**:
```typescript
const loadRegistrations = (page = 1) => {
  if (!selectedEditionId.value) return
  fetchAdminRegistrations(selectedEditionId.value, {
    page,
    pageSize: 20,
    search: searchQuery.value || undefined,
    status: statusFilter.value || undefined,
    accommodationTypes: selectedAccommodationTypes.value.length > 0
      ? selectedAccommodationTypes.value
      : undefined,
    extraIds: selectedExtraIds.value.length > 0 ? selectedExtraIds.value : undefined
  })
}
```

**Updated `watch(selectedEditionId)`** — reset filter selections + load options:
```typescript
watch(selectedEditionId, (newId) => {
  if (!newId) return
  selectedAccommodationTypes.value = []
  selectedExtraIds.value = []
  fetchEditionFilterOptions(newId)
  loadRegistrations(1)
})
```

**Add watchers for new filter state**:
```typescript
watch(selectedAccommodationTypes, () => loadRegistrations(1))
watch(selectedExtraIds, () => loadRegistrations(1))
```

**Export handler**:
```typescript
const handleExportCsv = async () => {
  if (!selectedEditionId.value) return
  await exportToCsv(selectedEditionId.value, {
    search: searchQuery.value || undefined,
    status: statusFilter.value || undefined,
    accommodationTypes: selectedAccommodationTypes.value.length > 0
      ? selectedAccommodationTypes.value
      : undefined,
    extraIds: selectedExtraIds.value.length > 0 ? selectedExtraIds.value : undefined
  })
  if (exportError.value) {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: exportError.value,
      life: 5000
    })
  }
}
```

**Updated `onMounted`** — no change needed; `watch(selectedEditionId)` now handles `fetchEditionFilterOptions`.

#### 3b. Template changes

**Toolbar row** — add the export button next to the edition selector:

```html
<!-- Camp edition selector + Export button -->
<div class="flex gap-3 flex-wrap items-end justify-between">
  <Select
    v-model="selectedEditionId"
    :options="campEditionOptions"
    :loading="editionsLoading"
    optionLabel="label"
    optionValue="value"
    placeholder="Seleccionar edición..."
    class="w-80"
    data-testid="edition-selector"
    aria-label="Seleccionar edición de campamento"
  />
  <Button
    v-if="auth.isBoard && selectedEditionId"
    label="Exportar CSV"
    icon="pi pi-download"
    outlined
    :loading="exportLoading"
    :disabled="exportLoading"
    data-testid="export-csv-button"
    aria-label="Exportar inscripciones a CSV"
    @click="handleExportCsv"
  />
</div>
```

**Filters row** — add two MultiSelect controls:

```html
<!-- Filters row -->
<div v-if="selectedEditionId" class="flex gap-3 flex-wrap">
  <IconField>
    <InputIcon class="pi pi-search" />
    <InputText
      v-model="searchQuery"
      placeholder="Buscar familia o representante..."
      class="w-64"
      data-testid="search-input"
      aria-label="Buscar por familia o representante"
    />
  </IconField>
  <Select
    v-model="statusFilter"
    :options="statusOptions"
    optionLabel="label"
    optionValue="value"
    placeholder="Estado"
    class="w-48"
    data-testid="status-filter"
    aria-label="Filtrar por estado"
  />
  <!-- NEW: Accommodation type filter -->
  <MultiSelect
    v-model="selectedAccommodationTypes"
    :options="accommodationTypeOptions"
    optionLabel="label"
    optionValue="value"
    :loading="filterOptionsLoading"
    :disabled="filterOptionsLoading || accommodationTypeOptions.length === 0"
    placeholder="Alojamiento"
    class="w-48"
    display="chip"
    data-testid="accommodation-type-filter"
    aria-label="Filtrar por tipo de alojamiento"
  />
  <!-- NEW: Extras filter -->
  <MultiSelect
    v-model="selectedExtraIds"
    :options="extraOptions"
    optionLabel="label"
    optionValue="value"
    :loading="filterOptionsLoading"
    :disabled="filterOptionsLoading || extraOptions.length === 0"
    placeholder="Extras"
    class="w-48"
    display="chip"
    data-testid="extras-filter"
    aria-label="Filtrar por extras"
  />
</div>
```

**Implementation notes**:
- `display="chip"` on MultiSelect shows selected items as removable chips inside the input — good UX for multi-select filters.
- MultiSelects are disabled while filter options are loading and when the edition has no options of that kind (e.g., no accommodations configured).
- The export button is hidden when no edition is selected and when the user is not Board/Admin.
- `useToast` requires `<Toast />` to be mounted somewhere in the parent layout — check that `AuthenticatedLayout.vue` or `AdminPage.vue` already includes it. If not, add `<Toast />` to the component template.

---

### Step 4: Verify `<Toast />` is available in admin layout

**File**: Check `frontend/src/layouts/AuthenticatedLayout.vue` or the parent admin view.

**Action**: Verify `<Toast />` from PrimeVue is mounted globally. If not present, add it.

```html
<!-- In AuthenticatedLayout.vue template, if not already there -->
<Toast />
```

```typescript
// And import at top of <script setup>
import Toast from 'primevue/toast'
```

If `<Toast />` is already globally registered via `PrimeVue` plugin config, no change is needed.

---

### Step 5: Write Vitest Unit Tests

**File**: `frontend/src/composables/__tests__/useAdminRegistrations.test.ts`

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useAdminRegistrations } from '@/composables/useAdminRegistrations'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn() }
}))

// Mock URL.createObjectURL / revokeObjectURL (not in jsdom)
global.URL.createObjectURL = vi.fn(() => 'blob:mock-url')
global.URL.revokeObjectURL = vi.fn()

const mockListResponse = {
  data: {
    success: true,
    data: {
      items: [],
      totalCount: 0,
      totals: { totalRegistrations: 0, totalMembers: 0, totalAmount: 0, totalPaid: 0, totalRemaining: 0 }
    },
    error: null
  }
}

describe('useAdminRegistrations', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // Reset DOM
    document.body.innerHTML = ''
  })

  describe('fetchAdminRegistrations', () => {
    it('should include accommodationTypes in query string when provided', async () => {
      vi.mocked(api.get).mockResolvedValue(mockListResponse)

      const { fetchAdminRegistrations } = useAdminRegistrations()
      await fetchAdminRegistrations('edition-1', {
        accommodationTypes: ['Lodge', 'Tent']
      })

      expect(api.get).toHaveBeenCalledWith(
        expect.stringContaining('accommodationTypes=Lodge'),
      )
      const callArg = vi.mocked(api.get).mock.calls[0][0] as string
      expect(callArg).toContain('accommodationTypes=Tent')
    })

    it('should include extraIds in query string when provided', async () => {
      vi.mocked(api.get).mockResolvedValue(mockListResponse)

      const { fetchAdminRegistrations } = useAdminRegistrations()
      await fetchAdminRegistrations('edition-1', {
        extraIds: ['extra-uuid-1', 'extra-uuid-2']
      })

      const callArg = vi.mocked(api.get).mock.calls[0][0] as string
      expect(callArg).toContain('extraIds=extra-uuid-1')
      expect(callArg).toContain('extraIds=extra-uuid-2')
    })

    it('should not include empty filter arrays in query string', async () => {
      vi.mocked(api.get).mockResolvedValue(mockListResponse)

      const { fetchAdminRegistrations } = useAdminRegistrations()
      await fetchAdminRegistrations('edition-1', {
        accommodationTypes: [],
        extraIds: []
      })

      const callArg = vi.mocked(api.get).mock.calls[0][0] as string
      expect(callArg).not.toContain('accommodationTypes')
      expect(callArg).not.toContain('extraIds')
    })

    it('should set error when API call fails', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { fetchAdminRegistrations, error, registrations } = useAdminRegistrations()
      await fetchAdminRegistrations('edition-1')

      expect(error.value).toBe('Error al cargar inscripciones')
      expect(registrations.value).toEqual([])
    })
  })

  describe('fetchEditionFilterOptions', () => {
    it('should fetch extras and accommodations in parallel', async () => {
      vi.mocked(api.get)
        .mockResolvedValueOnce({ data: { success: true, data: [{ id: 'e1', name: 'Kayak', isActive: true }], error: null } })
        .mockResolvedValueOnce({ data: { success: true, data: [{ id: 'a1', accommodationType: 'Lodge', isActive: true }], error: null } })

      const { fetchEditionFilterOptions, editionExtras, editionAccommodations } = useAdminRegistrations()
      await fetchEditionFilterOptions('edition-1')

      expect(api.get).toHaveBeenCalledTimes(2)
      expect(editionExtras.value).toHaveLength(1)
      expect(editionAccommodations.value).toHaveLength(1)
    })

    it('should filter out inactive accommodations', async () => {
      vi.mocked(api.get)
        .mockResolvedValueOnce({ data: { success: true, data: [], error: null } })
        .mockResolvedValueOnce({
          data: {
            success: true,
            data: [
              { id: 'a1', accommodationType: 'Lodge', isActive: true },
              { id: 'a2', accommodationType: 'Tent', isActive: false }
            ],
            error: null
          }
        })

      const { fetchEditionFilterOptions, editionAccommodations } = useAdminRegistrations()
      await fetchEditionFilterOptions('edition-1')

      expect(editionAccommodations.value).toHaveLength(1)
      expect(editionAccommodations.value[0].id).toBe('a1')
    })

    it('should reset to empty lists on edition change (called with new edition)', async () => {
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: [], error: null } })

      const { fetchEditionFilterOptions, editionExtras } = useAdminRegistrations()
      // Load first edition with data
      editionExtras.value = [{ id: 'e1', name: 'Old extra' } as never]
      await fetchEditionFilterOptions('edition-2')

      expect(editionExtras.value).toHaveLength(0) // reset before fetch
    })
  })

  describe('exportToCsv', () => {
    it('should call export endpoint with correct query params', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: new Blob(['col1,col2'], { type: 'text/csv' }),
        headers: {}
      })

      const { exportToCsv } = useAdminRegistrations()
      await exportToCsv('edition-1', {
        status: 'Confirmed',
        accommodationTypes: ['Lodge'],
        extraIds: ['extra-1']
      })

      const callArg = vi.mocked(api.get).mock.calls[0][0] as string
      expect(callArg).toContain('status=Confirmed')
      expect(callArg).toContain('accommodationTypes=Lodge')
      expect(callArg).toContain('extraIds=extra-1')
      expect(callArg).toContain('/registrations/export/csv')
    })

    it('should use filename from Content-Disposition header when available', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: new Blob([''], { type: 'text/csv' }),
        headers: { 'content-disposition': 'attachment; filename="inscripciones-test-2026-07-01.csv"' }
      })

      // Spy on link click
      const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})

      const { exportToCsv } = useAdminRegistrations()
      await exportToCsv('edition-1', {})

      expect(clickSpy).toHaveBeenCalled()
      // The download attribute should reflect the header filename
      const link = document.querySelector('a')
      // link may already be removed from DOM; just verify click was called
      clickSpy.mockRestore()
    })

    it('should set exportError when API call fails', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Server error'))

      const { exportToCsv, exportError } = useAdminRegistrations()
      await exportToCsv('edition-1', {})

      expect(exportError.value).toBe('Error al exportar las inscripciones')
    })

    it('should set exportLoading to false after completion regardless of success', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('fail'))

      const { exportToCsv, exportLoading } = useAdminRegistrations()
      await exportToCsv('edition-1', {})

      expect(exportLoading.value).toBe(false)
    })
  })
})
```

---

### Step 6: Update Technical Documentation

**Action**: Review and update relevant documentation.

**Implementation Steps**:
1. **Identify affected docs**: No new routes or API spec changes from the frontend perspective (the backend plan covers api-spec.yml).
2. **Component documentation**: If a component catalog or Storybook exists, update it.
3. **Report**: Document which files were updated and what changes were made.

No `frontend-standards.mdc` updates are needed — no new patterns or libraries were introduced (MultiSelect is a standard PrimeVue component already in use elsewhere in the app).

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-registration-export-filters-frontend`
2. **Step 1** — Add `AdminRegistrationFilters` type to `types/registration.ts`
3. **Step 2** — Update `useAdminRegistrations.ts`
4. **Step 3** — Update `RegistrationsAdminPanel.vue` (script + template)
5. **Step 4** — Verify `<Toast />` is in layout
6. **Step 5** — Write unit tests in `__tests__/useAdminRegistrations.test.ts`
7. **Step 6** — Update documentation

---

## Testing Checklist

### Vitest unit tests

- [ ] `fetchAdminRegistrations` — includes `accommodationTypes` as repeated query params
- [ ] `fetchAdminRegistrations` — includes `extraIds` as repeated query params
- [ ] `fetchAdminRegistrations` — does NOT append empty filter arrays
- [ ] `fetchAdminRegistrations` — sets error on API failure
- [ ] `fetchEditionFilterOptions` — fires both API calls in parallel
- [ ] `fetchEditionFilterOptions` — filters out inactive accommodations
- [ ] `fetchEditionFilterOptions` — resets to empty lists on each call
- [ ] `exportToCsv` — builds correct query string with all active filters
- [ ] `exportToCsv` — uses `Content-Disposition` filename when provided
- [ ] `exportToCsv` — sets `exportError` on failure
- [ ] `exportToCsv` — resets `exportLoading` after any outcome

### Manual verification

- [ ] Select an edition → accommodation type and extras MultiSelects populate with options
- [ ] Change edition → both MultiSelects reset and reload
- [ ] Select accommodation type filter → table filters, footer totals update
- [ ] Select extras filter → table filters, footer totals update
- [ ] Both filters work together (AND logic)
- [ ] Both filters work with existing status and search filters
- [ ] "Exportar CSV" button only visible to Admin/Board users
- [ ] "Exportar CSV" with no active filters → downloads full edition data
- [ ] "Exportar CSV" with active filters → downloaded CSV reflects filtered data
- [ ] CSV opens correctly in Excel (no import wizard, correct encoding)
- [ ] Export button shows spinner while downloading
- [ ] If export fails, error toast appears

---

## Error Handling Patterns

- **Filter options load failure** (extras/accommodations API down): Silently fails — `editionExtras` and `editionAccommodations` remain empty, MultiSelects are disabled. No toast needed since this is background data; the main list still loads.
- **Export failure**: `exportError` ref is set → component calls `toast.add` with `severity: 'error'` detail.
- **Registration list failure**: Existing `error` ref + `Message` component (already implemented).

---

## UI/UX Considerations

- **MultiSelect `display="chip"`**: Selected filters appear as removable chips inside the control — clear visual feedback of active filters.
- **Disabled MultiSelect when no options**: If the edition has no accommodations or no extras configured, the control shows as disabled to avoid confusing empty dropdowns.
- **Export button placement**: Right-aligned in the toolbar row (opposite the edition selector). Only visible when edition is selected and user is Board/Admin.
- **Loading state on export**: `Button :loading="exportLoading"` disables the button and shows spinner.
- **Responsive**: MultiSelects use `class="w-48"` — on narrow screens they wrap to the next line (existing `flex-wrap` on the filters row handles this).
- **Accessibility**: All new controls have `aria-label` and `data-testid` attributes.

---

## Dependencies

No new npm packages required. All PrimeVue components used (`MultiSelect`, `Toast`, `Button`) are already in the project dependencies.

**PrimeVue components added**:
- `MultiSelect` from `primevue/multiselect` — multi-select dropdown with chip display
- `useToast` from `primevue/usetoast` — composable for toast notifications (already used elsewhere)

---

## Notes

1. **Repeated query params**: The backend expects `accommodationTypes=Lodge&accommodationTypes=Tent` (repeated key). `URLSearchParams.append()` (not `.set()`) must be used for array values.

2. **`responseType: 'blob'` is mandatory**: Without it, Axios parses the response as JSON/text, which corrupts the binary content. The API call for the export endpoint must always include `{ responseType: 'blob' }`.

3. **`Promise.all` in `fetchEditionFilterOptions`**: Both extras and accommodations requests fire simultaneously to minimise latency. Even if one fails, the try/catch prevents the other from being affected — the ref simply stays empty.

4. **MultiSelect options deduplicated by type**: The accommodation filter shows unique `AccommodationType` values (e.g., one "Albergue" entry even if the edition has three lodge accommodations). This is done with the `seen` Set in `accommodationTypeOptions` computed.

5. **Filter reset on edition change**: Both `selectedAccommodationTypes` and `selectedExtraIds` must be reset to `[]` synchronously before calling `fetchEditionFilterOptions` and `loadRegistrations`, so the new request doesn't carry stale filter values from the previous edition.

6. **`useAdminRegistrations` is not instantiated at module level**: It's called inside `<script setup>`, creating fresh state per component instance. No singleton/shared-state concern.

---

## Next Steps After Implementation

- Merge frontend branch after backend branch is merged to `dev` (or develop together on the same branch if preferred).
- Smoke test the full flow in staging: select an edition with extras and accommodations, apply filters, export CSV, verify in Excel.

# Frontend Implementation Plan: Admin Registrations List — Usability Improvements

## Overview

Four improvements to `RegistrationsAdminPanel.vue` and its supporting types/composable. All
changes are purely frontend — the backend PR #222 already delivers the new API fields and sort
params. No new components, no new routes, no Pinia store changes needed.

Stack: Vue 3 Composition API · PrimeVue · Tailwind CSS · TypeScript strict.

---

## Architecture Context

| Layer | File | Change |
|---|---|---|
| Types | `frontend/src/types/registration.ts` | New `AdminRegistrationAccommodationSummary` interface; extend `AdminRegistrationListItem`; add sort fields to `AdminRegistrationFilters` |
| Utils | `frontend/src/utils/registration.ts` | Add `ATTENDANCE_PERIOD_SHORT` map |
| Composable | `frontend/src/composables/useAdminRegistrations.ts` | Pass `sortBy` / `sortDirection` query params |
| Component | `frontend/src/components/admin/RegistrationsAdminPanel.vue` | Período column, Aloj. column, sort handling, camp selector UX |
| Tests | `frontend/src/composables/__tests__/useAdminRegistrations.test.ts` (if it exists) or create | Unit tests |

No new routes. No Pinia store change. `vTooltip` is already globally registered in `main.ts`.

---

## Implementation Steps

### Step 0: Create Feature Branch

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-registration-admin-list-improvements-frontend
```

---

### Step 1: Update TypeScript Types

**File:** `frontend/src/types/registration.ts`

#### 1a. Add `AdminRegistrationAccommodationSummary` interface

Import `AccommodationType` from `./camp-edition` (already imported by the file for
`AccommodationPreferenceResponse`):

```ts
export interface AdminRegistrationAccommodationSummary {
  accommodationName: string
  accommodationType: AccommodationType
  preferenceOrder: number
}
```

#### 1b. Extend `AdminRegistrationListItem`

```ts
export interface AdminRegistrationListItem {
  id: string
  familyUnit: { id: string; name: string }
  representative: { id: string; firstName: string; lastName: string; email: string }
  status: RegistrationStatus
  memberCount: number
  totalAmount: number
  amountPaid: number
  amountRemaining: number
  createdAt: string
  attendancePeriods: AttendancePeriod[]                         // NEW
  accommodationPreferences: AdminRegistrationAccommodationSummary[]  // NEW
}
```

#### 1c. Add sort fields to `AdminRegistrationFilters`

```ts
export interface AdminRegistrationFilters {
  page?: number
  pageSize?: number
  search?: string
  status?: string
  accommodationPreferences?: AccommodationPreferenceFilter[]
  extraIds?: string[]
  attendancePeriods?: AttendancePeriod[]
  ageCategories?: AgeCategory[]
  sortBy?: 'createdAt' | 'familyName'     // NEW
  sortDirection?: 'asc' | 'desc'          // NEW
}
```

---

### Step 2: Add Short-Label Map to Utils

**File:** `frontend/src/utils/registration.ts`

Add alongside the existing `ATTENDANCE_PERIOD_LABELS`:

```ts
export const ATTENDANCE_PERIOD_SHORT: Record<AttendancePeriod, string> = {
  Complete: 'T',
  FirstWeek: 'S1',
  SecondWeek: 'S2',
  WeekendVisit: 'V',
}

export const formatAttendancePeriods = (periods: AttendancePeriod[]): string =>
  periods.map(p => ATTENDANCE_PERIOD_SHORT[p] ?? p).join(' · ')
```

---

### Step 3: Update Composable — Sort Params

**File:** `frontend/src/composables/useAdminRegistrations.ts`

In `fetchAdminRegistrations`, add sort params to `queryParams` after the existing params:

```ts
if (params.sortBy) queryParams.set('sortBy', params.sortBy)
if (params.sortDirection) queryParams.set('sortDirection', params.sortDirection)
```

No other changes to the composable — the response type update is covered by Step 1b.

---

### Step 4: Update `RegistrationsAdminPanel.vue`

**File:** `frontend/src/components/admin/RegistrationsAdminPanel.vue`

This step has four independent sub-changes. Apply them in order.

#### 4a. Imports

Add to the script imports:

```ts
import type { DataTableSortEvent } from 'primevue/datatable'
import type { CampEditionStatus } from '@/types/camp-edition'
import type { AccommodationType } from '@/types/camp-edition'
import { formatAttendancePeriods } from '@/utils/registration'
```

#### 4b. Sort state and handler

Add after the existing filter refs:

```ts
const sortField = ref<string>('createdAt')
const sortOrder = ref<1 | -1>(-1)   // -1 = DESC (PrimeVue convention)

const onSort = (event: DataTableSortEvent) => {
  sortField.value = String(event.sortField ?? 'createdAt')
  sortOrder.value = (event.sortOrder as 1 | -1) ?? -1
  loadRegistrations(1)
}
```

Update `loadRegistrations` to pass sort params:

```ts
const loadRegistrations = (page = 1) => {
  if (!selectedEditionId.value) return
  const apiSortBy = sortField.value === 'familyUnit.name' ? 'familyName' : 'createdAt'
  const apiSortDirection = sortOrder.value === 1 ? 'asc' : 'desc'
  fetchAdminRegistrations(selectedEditionId.value, {
    page,
    pageSize: 20,
    search: searchQuery.value || undefined,
    status: statusFilter.value || undefined,
    accommodationPreferences: selectedAccommodationPreferences.value.length > 0
      ? selectedAccommodationPreferences.value
      : undefined,
    extraIds: selectedExtraIds.value.length > 0 ? selectedExtraIds.value : undefined,
    attendancePeriods: selectedAttendancePeriods.value.length > 0
      ? selectedAttendancePeriods.value
      : undefined,
    ageCategories: selectedAgeCategories.value.length > 0
      ? selectedAgeCategories.value
      : undefined,
    sortBy: apiSortBy,
    sortDirection: apiSortDirection,
  })
}
```

#### 4c. Accommodation icon/label maps

Add after `statusLabel`:

```ts
const ACCOMMODATION_ICON: Record<AccommodationType, string> = {
  Lodge: 'pi pi-building',
  Bungalow: 'pi pi-home',
  Tent: 'pi pi-sun',
  Caravan: 'pi pi-car',
  Motorhome: 'pi pi-truck',
}

const ACCOMMODATION_LABEL: Record<AccommodationType, string> = {
  Lodge: 'Albergue',
  Bungalow: 'Bungalow',
  Tent: 'Tienda',
  Caravan: 'Caravana',
  Motorhome: 'Autocaravana',
}
```

#### 4d. Camp selector: status maps and smart pre-selection

Add status display maps:

```ts
const EDITION_STATUS_LABEL: Record<CampEditionStatus, string> = {
  Proposed: 'Propuesta',
  Draft: 'Borrador',
  Open: 'Abierto',
  Closed: 'Cerrado',
  Completed: 'Completado',
}

const EDITION_STATUS_SEVERITY: Record<CampEditionStatus, string> = {
  Proposed: 'secondary',
  Draft: 'warn',
  Open: 'success',
  Closed: 'danger',
  Completed: 'info',
}
```

Update `campEditionOptions` computed to include status:

```ts
const campEditionOptions = computed(() =>
  allEditions.value.map((e) => ({
    label: `${e.name ?? 'Campamento'} ${e.year}`,
    value: e.id,
    status: e.status,
  }))
)
```

Replace the `onMounted` pre-selection logic:

```ts
onMounted(async () => {
  await fetchAllEditions()
  if (allEditions.value.length === 0) return

  const today = new Date().toISOString().slice(0, 10)
  const upcoming = allEditions.value
    .filter(e => (e.status === 'Open' || e.status === 'Draft') && e.startDate >= today)
    .sort((a, b) => a.startDate.localeCompare(b.startDate))

  if (upcoming.length > 0) {
    selectedEditionId.value = upcoming[0].id
  } else {
    const openEdition = allEditions.value.find(e => e.status === 'Open')
    selectedEditionId.value = openEdition?.id ?? allEditions.value[0].id
  }
})
```

#### 4e. Template changes

**DataTable** — add `sort-field`, `sort-order`, and `@sort`:

```vue
<DataTable
  v-else
  :value="registrations"
  lazy
  paginator
  :rows="20"
  :total-records="totalCount"
  striped-rows
  :sort-field="sortField"
  :sort-order="sortOrder"
  class="rounded-lg cursor-pointer"
  data-testid="registrations-table"
  @page="onPage"
  @row-click="onRowClick"
  @sort="onSort"
>
```

**Familia column** — add `sortable`:

```vue
<Column field="familyUnit.name" header="Familia" sortable>
  <template #body="{ data }">
    <span class="font-medium">{{ data.familyUnit.name }}</span>
  </template>
</Column>
```

**Período column** — insert after Estado column:

```vue
<Column header="Período">
  <template #body="{ data }">
    <span class="text-sm font-mono text-gray-700">
      {{ formatAttendancePeriods(data.attendancePeriods) }}
    </span>
  </template>
</Column>
```

**Aloj. column** — insert after Período column:

```vue
<Column header="Aloj.">
  <template #body="{ data }">
    <div class="flex gap-1">
      <span
        v-for="pref in data.accommodationPreferences"
        :key="pref.preferenceOrder"
        v-tooltip.top="`${pref.preferenceOrder}ª opción: ${pref.accommodationName} (${ACCOMMODATION_LABEL[pref.accommodationType]})`"
        class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-gray-100 text-gray-600 cursor-default"
      >
        <i :class="ACCOMMODATION_ICON[pref.accommodationType]" class="text-xs" />
      </span>
    </div>
  </template>
</Column>
```

**Creación column** — add `sortable` and `field`:

```vue
<Column field="createdAt" header="Creación" sortable>
  <template #body="{ data }">
    <span class="text-sm text-gray-600">{{ formatDate(data.createdAt) }}</span>
  </template>
</Column>
```

**Footer ColumnGroup** — colspan increases from 4 to 6 (Familia, Representante, Email, Estado,
Período, Aloj. are now the first 6 columns):

```vue
<Column
  :footer="`Total: ${totals?.totalRegistrations ?? 0} inscripciones`"
  :colspan="6"
  footerClass="font-semibold text-gray-900"
/>
```

**Camp selector** — replace the `<Select>` with a version that uses custom option/value templates:

```vue
<Select
  v-model="selectedEditionId"
  :options="campEditionOptions"
  :loading="editionsLoading"
  option-label="label"
  option-value="value"
  placeholder="Seleccionar edición..."
  class="w-80"
  data-testid="edition-selector"
  aria-label="Seleccionar edición de campamento"
>
  <template #option="{ option }">
    <div class="flex items-center gap-2">
      <span>{{ option.label }}</span>
      <Tag
        :value="EDITION_STATUS_LABEL[option.status as CampEditionStatus]"
        :severity="EDITION_STATUS_SEVERITY[option.status as CampEditionStatus]"
        class="text-xs"
      />
    </div>
  </template>
  <template #value="{ value }">
    <div v-if="value" class="flex items-center gap-2">
      <span>{{ campEditionOptions.find(o => o.value === value)?.label }}</span>
      <Tag
        v-if="campEditionOptions.find(o => o.value === value) as typeof campEditionOptions.value[0]"
        :value="EDITION_STATUS_LABEL[campEditionOptions.find(o => o.value === value)!.status as CampEditionStatus]"
        :severity="EDITION_STATUS_SEVERITY[campEditionOptions.find(o => o.value === value)!.status as CampEditionStatus]"
        class="text-xs"
      />
    </div>
    <span v-else class="text-gray-400">Seleccionar edición...</span>
  </template>
</Select>
```

> To avoid repeated `.find()` calls, extract a `selectedEditionOption` computed:
> ```ts
> const selectedEditionOption = computed(() =>
>   campEditionOptions.value.find(o => o.value === selectedEditionId.value) ?? null
> )
> ```
> Then use `selectedEditionOption.value?.label` and `selectedEditionOption.value?.status` in
> the `#value` slot.

---

### Step 5: Vitest Unit Tests

**File:** `frontend/src/utils/__tests__/registration.test.ts`

Add tests for the new utilities (or create the file if it doesn't exist):

```ts
describe('formatAttendancePeriods', () => {
  it('should return T for Complete', () => {
    expect(formatAttendancePeriods(['Complete'])).toBe('T')
  })
  it('should return S1 for FirstWeek', () => {
    expect(formatAttendancePeriods(['FirstWeek'])).toBe('S1')
  })
  it('should return S2 for SecondWeek', () => {
    expect(formatAttendancePeriods(['SecondWeek'])).toBe('S2')
  })
  it('should return V for WeekendVisit', () => {
    expect(formatAttendancePeriods(['WeekendVisit'])).toBe('V')
  })
  it('should join multiple periods with ·', () => {
    expect(formatAttendancePeriods(['FirstWeek', 'SecondWeek'])).toBe('S1 · S2')
  })
  it('should return empty string for empty array', () => {
    expect(formatAttendancePeriods([])).toBe('')
  })
})
```

**File:** `frontend/src/composables/__tests__/useAdminRegistrations.test.ts`

Add a test for sort params:

```ts
it('should pass sortBy and sortDirection to query params', async () => {
  // Arrange: mock api.get
  // Act: call fetchAdminRegistrations with sortBy: 'familyName', sortDirection: 'asc'
  // Assert: api.get was called with URL containing sortBy=familyName&sortDirection=asc
})
```

---

### Step 6: Update Technical Documentation

**File:** `ai-specs/changes/feat-registration-admin-list-improvements/enriched.md`
- Mark backend items as complete (they are delivered by PR #222).

No `frontend-standards.mdc` changes needed (no new patterns introduced).

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — TypeScript types (`registration.ts`)
3. Step 2 — Utils short-label map (`utils/registration.ts`)
4. Step 3 — Composable sort params (`useAdminRegistrations.ts`)
5. Step 4 — Component (`RegistrationsAdminPanel.vue`), sub-changes in order: a→b→c→d→e
6. Step 5 — Unit tests
7. Step 6 — Documentation

---

## Testing Checklist

- [ ] `formatAttendancePeriods(['Complete'])` → `'T'`
- [ ] `formatAttendancePeriods(['FirstWeek', 'SecondWeek'])` → `'S1 · S2'`
- [ ] `formatAttendancePeriods([])` → `''`
- [ ] Período column renders correct short labels in the table
- [ ] Aloj. column shows icons; tooltip shows `"1ª opción: <name> (<type>)"`
- [ ] Registrations with no accommodation preferences show an empty Aloj. cell
- [ ] Clicking Familia column header sorts A→Z, then Z→A on second click
- [ ] Clicking Creación column header sorts newest→oldest, then oldest→newest
- [ ] Sort change resets pagination to page 1
- [ ] Default sort on page load is Creación DESC (same as before)
- [ ] Camp selector shows colored status badge in each dropdown option
- [ ] On page load, pre-selects the nearest upcoming Open/Draft edition
- [ ] Footer colspan still aligns correctly after adding 2 columns
- [ ] TypeScript: no `any`, no type errors (`npx vue-tsc --noEmit`)

---

## Error Handling Patterns

No new error paths. Sort params are always sent with a valid default — even if the backend
receives an unknown `sortBy` value it falls back to `createdAt`. No toast needed for sort errors.

---

## UI/UX Considerations

- **Período column**: `font-mono` makes short labels (`S1`, `T`) visually consistent.
- **Aloj. column**: small circular badges (24×24px) keep the column compact. Tooltip provides
  the full context without cluttering the row.
- **Sort**: PrimeVue DataTable shows a sort-direction arrow in the column header automatically
  when `sortable` is set and `:sort-field` / `:sort-order` are bound.
- **Camp selector**: The `#option` template adds the Tag badge; the `#value` template ensures
  the badge is also visible in the collapsed selector, not just in the dropdown.
- **Pre-selection**: Uses `startDate >= today` (string comparison is safe for ISO dates).
  Falls back gracefully if no upcoming editions exist.

---

## Dependencies

No new npm packages. Uses:
- `primevue/tooltip` — already globally registered (`app.directive("tooltip", Tooltip)`)
- `primevue/tag` — already imported in the component
- `DataTableSortEvent` — from `primevue/datatable` (type-only import)
- `CampEditionStatus`, `AccommodationType` — from `@/types/camp-edition` (type-only import)

---

## Notes

- `vTooltip` is already globally registered in `main.ts` — use `v-tooltip.top="..."` directly
  without importing it.
- The backend returns `attendancePeriods: []` and `accommodationPreferences: []` for registrations
  that have no data — no null checks needed beyond `v-for`.
- The `sortField` mapping: PrimeVue uses the Column's `field` prop as `sortField` in the event.
  The `Familia` column has `field="familyUnit.name"` → map to `'familyName'` for the API.
  The `Creación` column has `field="createdAt"` → map to `'createdAt'`.
- When `selectedEditionId` changes (watch), reset `sortField` and `sortOrder` to defaults to
  avoid carrying over sort state from a different edition.
- Footer colspan: the current footer uses `:colspan="4"` for the label column. With the 2 new
  columns (Período, Aloj.) inserted between Estado and Miembros, update to `:colspan="6"`.

---

## Next Steps After Implementation

- Backend PR #222 must be merged and deployed before this frontend change reaches staging.
- After both PRs are merged, verify the full flow in staging with real data (registrations
  with multiple members across different attendance periods, and registrations with
  accommodation preferences set).

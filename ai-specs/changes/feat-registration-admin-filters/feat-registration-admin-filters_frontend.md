# Frontend Implementation Plan: feat-registration-admin-filters — Additional Filters for Admin Registrations Screen

## Overview

Extend the admin registrations panel with three filter changes, all within Vue 3 Composition API + PrimeVue + Tailwind CSS patterns:

1. **Redesign the Accommodation filter**: replace the existing type-based `MultiSelect` (Lodge/Tent/…) with a preference-aware `MultiSelect` whose options are `"Xª opción: [accommodation name]"` pairs, AND-combined. Internally the v-model stores encoded strings to avoid PrimeVue object-equality issues.
2. **Add Attendance Period filter**: new `MultiSelect` (Completo / Primera semana / Segunda semana / Fin de semana).
3. **Add Age Category filter**: new `MultiSelect` (Bebés / Niños / Adultos).
4. **Remove "Seleccionar todas"** from Accommodation and Extras `MultiSelect` (`:showSelectAll="false"`).

Changes are contained to three files: types, composable, and panel component. No new routes, stores, or components.

---

## Architecture Context

- **Composables**: `frontend/src/composables/useAdminRegistrations.ts` — serializes filters into query params
- **Types**: `frontend/src/types/registration.ts` — `AdminRegistrationFilters` and new `AccommodationPreferenceFilter`
- **Component**: `frontend/src/components/admin/RegistrationsAdminPanel.vue` — all filter UI
- **Tests**: `frontend/src/composables/__tests__/useAdminRegistrations.test.ts`
- No Pinia store changes. No new routes. No new components.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch name**: `feature/feat-registration-admin-filters-frontend`
- **Base branch**: `dev`

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-registration-admin-filters-frontend
git branch  # verify
```

---

### Step 1: Update TypeScript Types

**File**: `frontend/src/types/registration.ts`

#### 1a — Add `AccommodationPreferenceFilter` interface

Add near the top of the file alongside the other filter/enum types:

```typescript
export interface AccommodationPreferenceFilter {
  accommodationId: string
  preferenceOrder: 1 | 2 | 3
}
```

#### 1b — Update `AdminRegistrationFilters`

Replace the `accommodationTypes` field and add two new fields:

```typescript
// BEFORE
export interface AdminRegistrationFilters {
  page?: number
  pageSize?: number
  search?: string
  status?: string
  accommodationTypes?: AccommodationType[]
  extraIds?: string[]
}

// AFTER
export interface AdminRegistrationFilters {
  page?: number
  pageSize?: number
  search?: string
  status?: string
  accommodationPreferences?: AccommodationPreferenceFilter[]  // REPLACES accommodationTypes
  extraIds?: string[]
  attendancePeriods?: AttendancePeriod[]                      // NEW
  ageCategories?: AgeCategory[]                               // NEW
}
```

> `AccommodationType` import in this file can be removed if it is no longer referenced elsewhere in the file. Verify before deleting.

---

### Step 2: Update the Composable

**File**: `frontend/src/composables/useAdminRegistrations.ts`

#### 2a — `fetchAdminRegistrations` — replace query param serialization

In the `fetchAdminRegistrations` function, replace:

```typescript
// REMOVE:
params.accommodationTypes?.forEach(t => queryParams.append('accommodationTypes', t))
```

Add in its place, and also add the two new params:

```typescript
// Accommodation preference pairs — two parallel arrays (matched by index)
params.accommodationPreferences?.forEach(f => {
  queryParams.append('accommodationIds', f.accommodationId)
  queryParams.append('accommodationPreferenceOrders', String(f.preferenceOrder))
})
// Attendance period filter
params.attendancePeriods?.forEach(p => queryParams.append('attendancePeriods', p))
// Age category filter
params.ageCategories?.forEach(c => queryParams.append('ageCategories', c))
```

#### 2b — `exportToCsv` — same serialization change

Replace:

```typescript
// REMOVE:
filters.accommodationTypes?.forEach(t => queryParams.append('accommodationTypes', t))
```

Add:

```typescript
filters.accommodationPreferences?.forEach(f => {
  queryParams.append('accommodationIds', f.accommodationId)
  queryParams.append('accommodationPreferenceOrders', String(f.preferenceOrder))
})
filters.attendancePeriods?.forEach(p => queryParams.append('attendancePeriods', p))
filters.ageCategories?.forEach(c => queryParams.append('ageCategories', c))
```

#### 2c — Update import (if needed)

Remove the `AccommodationType` import from this file if it was imported to type `accommodationTypes`. Add `AccommodationPreferenceFilter` to the import from `@/types/registration`.

---

### Step 3: Update `RegistrationsAdminPanel.vue`

**File**: `frontend/src/components/admin/RegistrationsAdminPanel.vue`

This is the most involved step. Apply all changes below to the `<script setup>` block and the `<template>`.

#### 3a — Update imports

Remove `AccommodationType` from the registration types import. Add the new types:

```typescript
import type { AccommodationPreferenceFilter, AttendancePeriod, AgeCategory } from '@/types/registration'
```

Remove the `AccommodationType` import from `@/types/camp-edition` if it is no longer used anywhere else in the component.

#### 3b — Replace accommodation reactive state

```typescript
// REMOVE:
const selectedAccommodationTypes = ref<AccommodationType[]>([])

// ADD:
// String-encoded keys: "${accommodationId}:${preferenceOrder}"
// Using strings avoids PrimeVue MultiSelect object-equality issues
const selectedAccommodationPreferenceKeys = ref<string[]>([])
```

#### 3c — Add new reactive state for the two new filters

```typescript
const selectedAttendancePeriods = ref<AttendancePeriod[]>([])
const selectedAgeCategories = ref<AgeCategory[]>([])
```

#### 3d — Remove `accommodationTypeLabels` and `accommodationTypeOptions`

Delete the following entirely:

```typescript
// DELETE:
const accommodationTypeLabels: Record<AccommodationType, string> = { ... }
const accommodationTypeOptions = computed(() => { ... })
```

#### 3e — Add `accommodationPreferenceOptions` computed

```typescript
const PREFERENCE_LABELS: Record<1 | 2 | 3, string> = {
  1: '1ª opción',
  2: '2ª opción',
  3: '3ª opción',
}

// Options ordered by preference position first, then by accommodation sort order.
// Value is a string key encoding both fields to avoid PrimeVue object-equality issues.
const accommodationPreferenceOptions = computed(() =>
  ([1, 2, 3] as const).flatMap(order =>
    editionAccommodations.value.map(a => ({
      label: `${PREFERENCE_LABELS[order]}: ${a.name}`,
      value: `${a.id}:${order}`,
    }))
  )
)
```

#### 3f — Derive `AccommodationPreferenceFilter[]` from the string keys

This computed value is what gets passed to the composable — it converts the string keys back to typed objects:

```typescript
const selectedAccommodationPreferences = computed<AccommodationPreferenceFilter[]>(() =>
  selectedAccommodationPreferenceKeys.value.map(key => {
    const colonIndex = key.lastIndexOf(':')
    return {
      accommodationId: key.slice(0, colonIndex),
      preferenceOrder: Number(key.slice(colonIndex + 1)) as 1 | 2 | 3,
    }
  })
)
```

> Using `lastIndexOf(':')` rather than `split(':')` safely handles accommodation IDs that could theoretically contain colons (UUIDs don't, but this is more robust).

#### 3g — Add static option lists for period and age

```typescript
const attendancePeriodOptions: { label: string; value: AttendancePeriod }[] = [
  { label: 'Campamento completo', value: 'Complete' },
  { label: 'Primera semana',      value: 'FirstWeek' },
  { label: 'Segunda semana',      value: 'SecondWeek' },
  { label: 'Fin de semana',       value: 'WeekendVisit' },
]

const ageCategoryOptions: { label: string; value: AgeCategory }[] = [
  { label: 'Bebés',   value: 'Baby' },
  { label: 'Niños',   value: 'Child' },
  { label: 'Adultos', value: 'Adult' },
]
```

#### 3h — Update `watch(selectedEditionId, ...)` to reset new refs

Inside the existing `watch(selectedEditionId, ...)` callback, replace the accommodation reset and add the new ones:

```typescript
// REMOVE:
selectedAccommodationTypes.value = []

// ADD:
selectedAccommodationPreferenceKeys.value = []
selectedAttendancePeriods.value = []
selectedAgeCategories.value = []
```

#### 3i — Remove old watcher, add new watchers

```typescript
// REMOVE:
watch(selectedAccommodationTypes, () => { loadRegistrations(1) })

// ADD:
watch(selectedAccommodationPreferenceKeys, () => loadRegistrations(1))
watch(selectedAttendancePeriods, () => loadRegistrations(1))
watch(selectedAgeCategories, () => loadRegistrations(1))
```

#### 3j — Update `loadRegistrations`

Replace the `accommodationTypes` filter line and add the two new filters:

```typescript
const loadRegistrations = (page = 1) => {
  if (!selectedEditionId.value) return
  fetchAdminRegistrations(selectedEditionId.value, {
    page,
    pageSize: 20,
    search: searchQuery.value || undefined,
    status: statusFilter.value || undefined,
    // CHANGED: was accommodationTypes
    accommodationPreferences: selectedAccommodationPreferences.value.length > 0
      ? selectedAccommodationPreferences.value
      : undefined,
    extraIds: selectedExtraIds.value.length > 0 ? selectedExtraIds.value : undefined,
    // NEW:
    attendancePeriods: selectedAttendancePeriods.value.length > 0
      ? selectedAttendancePeriods.value
      : undefined,
    ageCategories: selectedAgeCategories.value.length > 0
      ? selectedAgeCategories.value
      : undefined,
  })
}
```

#### 3k — Update `handleExportCsv`

Apply the same changes as in `loadRegistrations`:

```typescript
const handleExportCsv = async () => {
  if (!selectedEditionId.value) return
  await exportToCsv(selectedEditionId.value, {
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
  })
  if (exportError.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: exportError.value, life: 4000 })
  }
}
```

#### 3l — Update template: replace Accommodation MultiSelect

Replace the existing `<MultiSelect … placeholder="Alojamiento">` block with:

```html
<MultiSelect
  v-if="accommodationPreferenceOptions.length > 0"
  v-model="selectedAccommodationPreferenceKeys"
  :options="accommodationPreferenceOptions"
  optionLabel="label"
  optionValue="value"
  placeholder="Alojamiento"
  display="chip"
  :showSelectAll="false"
  class="w-72"
  :loading="filterOptionsLoading"
  data-testid="accommodation-preference-filter"
  aria-label="Filtrar por preferencia de alojamiento"
/>
```

> `v-model` now binds `string[]` (the encoded keys). `optionValue="value"` tells PrimeVue to store the option's `value` string in the model, so chip display and selection work correctly without object-equality concerns.

#### 3m — Add two new MultiSelect filters in template

Add immediately after the Extras `<MultiSelect>`:

```html
<MultiSelect
  v-model="selectedAttendancePeriods"
  :options="attendancePeriodOptions"
  optionLabel="label"
  optionValue="value"
  placeholder="Período"
  display="chip"
  :showSelectAll="false"
  class="w-56"
  data-testid="attendance-period-filter"
  aria-label="Filtrar por período de asistencia"
/>
<MultiSelect
  v-model="selectedAgeCategories"
  :options="ageCategoryOptions"
  optionLabel="label"
  optionValue="value"
  placeholder="Edad"
  display="chip"
  :showSelectAll="false"
  class="w-48"
  data-testid="age-category-filter"
  aria-label="Filtrar por categoría de edad"
/>
```

#### 3n — Add `:showSelectAll="false"` to the Extras MultiSelect

Find the existing `<MultiSelect … placeholder="Extras">` and add:

```html
:showSelectAll="false"
```

---

### Step 4: Update Composable Tests

**File**: `frontend/src/composables/__tests__/useAdminRegistrations.test.ts`

#### 4a — Update the existing accommodation test

Replace the test named `'appends accommodationTypes and extraIds as repeated query params'`:

```typescript
it('appends accommodationPreferences as parallel accommodationIds and accommodationPreferenceOrders params', async () => {
  vi.mocked(api.get).mockResolvedValueOnce(mockListResponse)

  const { fetchAdminRegistrations } = useAdminRegistrations()

  await fetchAdminRegistrations(EDITION_ID, {
    accommodationPreferences: [
      { accommodationId: 'acc-1', preferenceOrder: 1 },
      { accommodationId: 'acc-2', preferenceOrder: 2 },
    ],
    extraIds: ['extra-1'],
  })

  const url = vi.mocked(api.get).mock.calls[0][0] as string
  expect(url).toContain('accommodationIds=acc-1')
  expect(url).toContain('accommodationIds=acc-2')
  expect(url).toContain('accommodationPreferenceOrders=1')
  expect(url).toContain('accommodationPreferenceOrders=2')
  expect(url).toContain('extraIds=extra-1')
  expect(url).not.toContain('accommodationTypes')
})
```

#### 4b — Add tests for new filters in `fetchAdminRegistrations`

```typescript
it('appends attendancePeriods as repeated query params', async () => {
  vi.mocked(api.get).mockResolvedValueOnce(mockListResponse)

  const { fetchAdminRegistrations } = useAdminRegistrations()

  await fetchAdminRegistrations(EDITION_ID, {
    attendancePeriods: ['FirstWeek', 'SecondWeek'],
  })

  const url = vi.mocked(api.get).mock.calls[0][0] as string
  expect(url).toContain('attendancePeriods=FirstWeek')
  expect(url).toContain('attendancePeriods=SecondWeek')
})

it('appends ageCategories as repeated query params', async () => {
  vi.mocked(api.get).mockResolvedValueOnce(mockListResponse)

  const { fetchAdminRegistrations } = useAdminRegistrations()

  await fetchAdminRegistrations(EDITION_ID, {
    ageCategories: ['Baby', 'Child'],
  })

  const url = vi.mocked(api.get).mock.calls[0][0] as string
  expect(url).toContain('ageCategories=Baby')
  expect(url).toContain('ageCategories=Child')
})

it('omits filter params when filters are undefined', async () => {
  vi.mocked(api.get).mockResolvedValueOnce(mockListResponse)

  const { fetchAdminRegistrations } = useAdminRegistrations()

  await fetchAdminRegistrations(EDITION_ID, {})

  const url = vi.mocked(api.get).mock.calls[0][0] as string
  expect(url).not.toContain('accommodationIds')
  expect(url).not.toContain('attendancePeriods')
  expect(url).not.toContain('ageCategories')
})
```

#### 4c — Update the `exportToCsv` filter test

Update the test `'passes active filters as query params'` in the `exportToCsv` describe block:

```typescript
it('passes active filters as query params', async () => {
  const mockBlob = new Blob(['col1,col2'], { type: 'text/csv' })
  vi.mocked(api.get).mockResolvedValueOnce({
    data: mockBlob,
    headers: {}
  })

  const { exportToCsv } = useAdminRegistrations()

  await exportToCsv(EDITION_ID, {
    status: 'Confirmed',
    accommodationPreferences: [{ accommodationId: 'acc-1', preferenceOrder: 1 }],
    extraIds: ['extra-1'],
    attendancePeriods: ['Complete'],
    ageCategories: ['Adult'],
  })

  const [url, config] = vi.mocked(api.get).mock.calls[0]
  expect(url).toContain('status=Confirmed')
  expect(url).toContain('accommodationIds=acc-1')
  expect(url).toContain('accommodationPreferenceOrders=1')
  expect(url).toContain('extraIds=extra-1')
  expect(url).toContain('attendancePeriods=Complete')
  expect(url).toContain('ageCategories=Adult')
  expect(url).not.toContain('accommodationTypes')
  expect(config).toMatchObject({ responseType: 'blob' })
})
```

---

### Step 5: Update Technical Documentation

No new routes, stores, or API endpoints are introduced on the frontend side. The only documentation that may need updating:

- **`ai-specs/specs/api-spec.yml`** (if it documents frontend-observed query params): verify the accommodation filter params match the backend plan (`accommodationIds[]`, `accommodationPreferenceOrders[]`, `attendancePeriods[]`, `ageCategories[]`). This should already be covered by the backend plan (Step 9 there), but cross-check that both plans agree on param names.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Update TypeScript types (`registration.ts`)
3. Step 2 — Update composable (`useAdminRegistrations.ts`)
4. Step 3 — Update panel component (`RegistrationsAdminPanel.vue`) — follow 3a through 3n in order
5. Step 4 — Update composable tests
6. Step 5 — Verify documentation

---

## Testing Checklist

**Vitest (unit)**
- [ ] Existing test suite still passes (`npm run test` in `frontend/`)
- [ ] New test: `accommodationPreferences` serialized as parallel `accommodationIds` + `accommodationPreferenceOrders` params
- [ ] New test: `attendancePeriods` serialized as repeated params
- [ ] New test: `ageCategories` serialized as repeated params
- [ ] New test: no filter params emitted when filters are `undefined`
- [ ] Updated `exportToCsv` test uses new param names

**Manual / browser verification**
- [ ] Selecting "1ª opción: Albergue" + "2ª opción: Autocaravana" triggers a list reload and the URL contains `accommodationIds=<id1>&accommodationPreferenceOrders=1&accommodationIds=<id2>&accommodationPreferenceOrders=2`
- [ ] Selecting a "Período" option filters the list
- [ ] Selecting an "Edad" option filters the list
- [ ] Changing camp edition resets all five filters (status, accommodation, extras, period, age)
- [ ] "Seleccionar todas" checkbox is absent from Accommodation and Extras dropdowns
- [ ] CSV export button sends all active filters in the request
- [ ] Filter chips display the option label correctly (e.g. "1ª opción: Albergue", "Primera semana", "Bebés")

---

## Error Handling Patterns

No new error paths are introduced. The existing composable error pattern is preserved:
- `fetchAdminRegistrations` catches errors → sets `error.value` → component shows `<Message severity="error">`
- `exportToCsv` catches errors → sets `exportError.value` → component shows a Toast notification

---

## UI/UX Considerations

- **Filter row layout**: five filters (`Alojamiento w-72`, `Extras w-56`, `Período w-56`, `Edad w-48`) plus the existing search and status controls. With `flex-wrap` already on the filter row, they wrap naturally on narrow viewports.
- **Accommodation MultiSelect width**: increased to `w-72` (from `w-56`) because "1ª opción: Autocaravana" labels are longer than the previous type-only labels.
- **`:showSelectAll="false"`**: applied to Accommodation and Extras dropdowns. The two new dropdowns never had it enabled.
- **`display="chip"`**: all five MultiSelect filters use chip display — consistent with existing pattern.
- **Empty filter options**: `v-if="accommodationPreferenceOptions.length > 0"` on the accommodation MultiSelect — same guard as before, edition must have active accommodations before the filter appears. Attendance period and age category always appear (static options).
- **Accessibility**: all new `<MultiSelect>` elements include `aria-label` and `data-testid` attributes.

---

## Dependencies

No new npm packages required. All components used (`MultiSelect`) are already imported in the panel component.

---

## Notes

- **String encoding for accommodation preferences**: `v-model` on the accommodation `MultiSelect` stores `"${accommodationId}:${preferenceOrder}"` strings, not objects. This sidesteps PrimeVue's object-equality comparison for selected items. The actual `AccommodationPreferenceFilter[]` is derived via a `computed` and passed to the composable. Do not try to use object values directly in MultiSelect — it causes deselection bugs.
- **`lastIndexOf(':')` in the decoder**: UUIDs (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) do not contain colons, so `split(':')` would also work, but `lastIndexOf` is more defensive.
- **Parallel array contract with backend**: `accommodationIds[]` and `accommodationPreferenceOrders[]` are sent as parallel arrays. The i-th `accommodationIds` value corresponds to the i-th `accommodationPreferenceOrders` value. The backend enforces that they must have equal length; mismatched arrays result in no filter applied (safe degradation).
- **AND semantics on the backend for accommodation**: selecting multiple accommodation preference pairs is AND-combined on the backend. On the frontend this means selecting "1ª opción: Albergue" + "2ª opción: Autocaravana" will only show families who have BOTH preferences set — not just either one.
- **All code in English** per `base-standards.mdc`. Spanish only in user-facing labels (`attendancePeriodOptions`, `ageCategoryOptions`, `PREFERENCE_LABELS`, placeholder text).
- **No `AccommodationType` in the revised filter**: the previous filter required `AccommodationType` from `@/types/camp-edition`. After this change, the component no longer needs that import for filtering purposes. Remove it if it has no other usages in the component.

---

## Next Steps After Implementation

- Backend implementation must be merged first (or run in parallel on separate branches) for the new query params to return filtered results.
- The frontend feature branch (`feature/feat-registration-admin-filters-frontend`) can be tested locally against the backend branch by starting both dev servers simultaneously.
- PR targets `dev` branch per the project's git workflow.

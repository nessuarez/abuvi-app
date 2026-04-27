# Feature: Additional Filters for Admin Registrations Screen

## Summary

Add two new filters to the admin registrations panel (`RegistrationsAdminPanel.vue`):

- **Attendance Period** (Semana 1 / Semana 2 / Completa / Fin de semana)
- **Age Category** (Bebés / Niños / Adultos)

Redesign the existing **Accommodation** filter: instead of filtering by accommodation type (Lodge, Tent…), filter by **specific accommodation + preference position**. Options are generated dynamically from the edition's accommodations × [1ª opción, 2ª opción, 3ª opción], e.g. "1ª opción: Albergue", "2ª opción: Autocaravana". Multiple selections are AND-combined — a registration must satisfy **all** selected pairs simultaneously (e.g. "1ª opción: Albergue AND 2ª opción: Autocaravana" shows only families who want Albergue first and could move to Autocaravana as second choice).

Additionally, remove the "Select All" toggle from the **Accommodation** and **Extras** `MultiSelect` filters.

---

## Acceptance Criteria

1. A "Período" multi-select filter appears in the filters row. Selecting one or more periods shows only registrations where **at least one member** has that `AttendancePeriod`.
2. An "Edad" multi-select filter appears in the filters row. Selecting one or more age categories shows only registrations where **at least one member** belongs to that `AgeCategory`.
3. Both new filters use `display="chip"` and `:showSelectAll="false"`, reset when the camp edition changes, and re-trigger the query on change.
4. Both new filters are also applied when exporting to CSV.
5. The existing "Alojamiento" filter is **replaced**: instead of filtering by accommodation type enum, options are `"Xª opción: [accommodation name]"` pairs. Each option represents a specific `CampEditionAccommodation` at a specific preference position (1, 2, or 3). Selecting multiple options is AND-combined — a registration must satisfy **every** selected pair (e.g. selecting "1ª opción: Albergue" + "2ª opción: Autocaravana" returns only families with Albergue as 1st choice AND Autocaravana as 2nd choice). Options are generated dynamically from `editionAccommodations` × `[1, 2, 3]`.
6. The Accommodation and Extras `MultiSelect` components have `:showSelectAll="false"` (the "Seleccionar todas" checkbox is removed).

---

## Implementation Plan

### Step 1 — Backend: New model & repository interface

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

Add a new record to represent an (accommodation, preference position) filter pair:

```csharp
public record AccommodationPreferenceFilter(Guid AccommodationId, int PreferenceOrder);
```

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`

Replace `IReadOnlyList<AccommodationType>? accommodationTypes` with `IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences` in both `GetAdminPagedAsync` and `GetAllForExportAsync`. Also add `attendancePeriods` and `ageCategories`:

```csharp
Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
    GetAdminPagedAsync(
        Guid campEditionId, int page, int pageSize,
        string? search, string? status,
        IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,  // CHANGED
        IReadOnlyList<Guid>? extraIds,
        IReadOnlyList<AttendancePeriod>? attendancePeriods,                      // NEW
        IReadOnlyList<AgeCategory>? ageCategories,                               // NEW
        CancellationToken ct);

Task<IReadOnlyList<Registration>> GetAllForExportAsync(
    Guid campEditionId,
    string? search,
    string? status,
    IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,      // CHANGED
    IReadOnlyList<Guid>? extraIds,
    IReadOnlyList<AttendancePeriod>? attendancePeriods,                          // NEW
    IReadOnlyList<AgeCategory>? ageCategories,                                   // NEW
    CancellationToken ct);
```

Replace the existing accommodation type filter clause in `GetAdminPagedAsync` with:

```csharp
// Accommodation preference filter (AND across selected pairs — each must match independently)
if (accommodationPreferences?.Count > 0)
{
    foreach (var f in accommodationPreferences)
    {
        var accommodationId = f.AccommodationId;
        var preferenceOrder = f.PreferenceOrder;
        query = query.Where(x =>
            db.RegistrationAccommodationPreferences.Any(p =>
                p.RegistrationId == x.Id &&
                p.CampEditionAccommodationId == accommodationId &&
                p.PreferenceOrder == preferenceOrder));
    }
}

// Attendance period filter
if (attendancePeriods?.Count > 0)
{
    query = query.Where(x =>
        db.RegistrationMembers.Any(m =>
            m.RegistrationId == x.Id &&
            attendancePeriods.Contains(m.AttendancePeriod)));
}

// Age category filter
if (ageCategories?.Count > 0)
{
    query = query.Where(x =>
        db.RegistrationMembers.Any(m =>
            m.RegistrationId == x.Id &&
            ageCategories.Contains(m.AgeCategory)));
}
```

> **Note on AND logic**: Each iteration of the `foreach` adds an independent `.Where()` clause, which EF Core chains as `AND`. Local variable capture (`accommodationId`, `preferenceOrder`) is required to avoid the classic LINQ closure-over-loop-variable bug.

Apply the equivalent AND logic to `GetAllForExportAsync` using navigation properties:

```csharp
if (accommodationPreferences?.Count > 0)
    foreach (var f in accommodationPreferences)
    {
        var accommodationId = f.AccommodationId;
        var preferenceOrder = f.PreferenceOrder;
        query = query.Where(r =>
            r.AccommodationPreferences.Any(p =>
                p.CampEditionAccommodationId == accommodationId &&
                p.PreferenceOrder == preferenceOrder));
    }

if (attendancePeriods?.Count > 0)
    query = query.Where(r =>
        r.Members.Any(m => attendancePeriods.Contains(m.AttendancePeriod)));

if (ageCategories?.Count > 0)
    query = query.Where(r =>
        r.Members.Any(m => ageCategories.Contains(m.AgeCategory)));
```

---

### Step 2 — Backend: Endpoint binding

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

Remove the existing `accommodationTypes[]` query params and replace with parallel arrays that are zipped into `AccommodationPreferenceFilter` records. Add the two new params:

```csharp
// Replaces: AccommodationType[]? accommodationTypes
[FromQuery] Guid[]? accommodationIds = null,
[FromQuery] int[]? accommodationPreferenceOrders = null,

// New params
[FromQuery] AttendancePeriod[]? attendancePeriods = null,
[FromQuery] AgeCategory[]? ageCategories = null,
```

Zip the two arrays into filter records before calling the service/repository:

```csharp
var accommodationPreferences = (accommodationIds?.Length > 0 && accommodationPreferenceOrders?.Length == accommodationIds.Length)
    ? accommodationIds.Zip(accommodationPreferenceOrders, (id, order) => new AccommodationPreferenceFilter(id, order)).ToList()
    : null;
```

Pass `accommodationPreferences`, `attendancePeriods`, and `ageCategories` to both `GetAdminPagedAsync` and `GetAllForExportAsync`.

---

### Step 3 — Backend: Tests

**File:** `tests/Abuvi.API.Tests/Features/Registrations/RegistrationsRepositoryTests.cs` (or equivalent integration test file)

Add tests covering:

- `GetAdminPagedAsync` with `accommodationPreferences = [{ accommodationId: X, preferenceOrder: 1 }]` returns only registrations with that accommodation as 1st choice.
- Two options with AND logic: `[{ X, 1 }, { Y, 2 }]` returns only registrations where 1st choice is X AND 2nd choice is Y (e.g. "who wants Albergue first and could move to Autocaravana second").
- `GetAdminPagedAsync` with `attendancePeriods = [FirstWeek]` returns only registrations with a `FirstWeek` member.
- `GetAdminPagedAsync` with `ageCategories = [Baby]` returns only registrations with a baby member.
- Combined: `attendancePeriods = [SecondWeek]` + `ageCategories = [Child]` returns registrations with a member who is a child in the second week.
- `GetAllForExportAsync` applies the same filters correctly.

---

### Step 4 — Frontend: Type update

**File:** `frontend/src/types/registration.ts`

Replace `accommodationTypes` with `accommodationPreferences` and add the two new fields:

```typescript
export interface AccommodationPreferenceFilter {
  accommodationId: string
  preferenceOrder: 1 | 2 | 3
}

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

---

### Step 5 — Frontend: Composable

**File:** `frontend/src/composables/useAdminRegistrations.ts`

The composable must serialize `accommodationPreferences` as two parallel repeated params matching the backend's `accommodationIds[]` + `accommodationPreferenceOrders[]` arrays:

```typescript
// Remove accommodationTypes serialization, replace with:
if (filters.accommodationPreferences?.length) {
  filters.accommodationPreferences.forEach(f => {
    params.append('accommodationIds', f.accommodationId)
    params.append('accommodationPreferenceOrders', String(f.preferenceOrder))
  })
}
```

Also serialize `attendancePeriods` and `ageCategories` as repeated params, matching the existing `extraIds` pattern.

---

### Step 6 — Frontend: Panel component

**File:** `frontend/src/components/admin/RegistrationsAdminPanel.vue`

#### New reactive state

```typescript
// Replaces: selectedAccommodationTypes
const selectedAccommodationPreferences = ref<AccommodationPreferenceFilter[]>([])

// New
const selectedAttendancePeriods = ref<AttendancePeriod[]>([])
const selectedAgeCategories = ref<AgeCategory[]>([])
```

#### New computed options

Replace `accommodationTypeOptions` with `accommodationPreferenceOptions`. Options are generated from the edition's accommodations × 3 preference positions:

```typescript
const PREFERENCE_LABELS: Record<1 | 2 | 3, string> = {
  1: '1ª opción',
  2: '2ª opción',
  3: '3ª opción',
}

const accommodationPreferenceOptions = computed(() =>
  ([1, 2, 3] as const).flatMap(order =>
    editionAccommodations.value.map(a => ({
      label: `${PREFERENCE_LABELS[order]}: ${a.name}`,
      value: { accommodationId: a.id, preferenceOrder: order } satisfies AccommodationPreferenceFilter,
    }))
  )
)
```

Because the `value` is an object, PrimeVue `MultiSelect` needs `dataKey="accommodationId"` and `optionValue` omitted (bind the whole object), and the parent `v-model` holds `AccommodationPreferenceFilter[]`.

Static option lists (unchanged from before):

```typescript
const attendancePeriodOptions = [
  { label: 'Campamento completo', value: 'Complete' as AttendancePeriod },
  { label: 'Primera semana',      value: 'FirstWeek' as AttendancePeriod },
  { label: 'Segunda semana',      value: 'SecondWeek' as AttendancePeriod },
  { label: 'Fin de semana',       value: 'WeekendVisit' as AttendancePeriod },
]

const ageCategoryOptions = [
  { label: 'Bebés',   value: 'Baby' as AgeCategory },
  { label: 'Niños',   value: 'Child' as AgeCategory },
  { label: 'Adultos', value: 'Adult' as AgeCategory },
]
```

#### Reset on edition change (in `watch(selectedEditionId, ...)`)

```typescript
selectedAccommodationPreferences.value = []
selectedAttendancePeriods.value = []
selectedAgeCategories.value = []
```

#### Watchers

```typescript
watch(selectedAccommodationPreferences, () => loadRegistrations(1))
watch(selectedAttendancePeriods, () => loadRegistrations(1))
watch(selectedAgeCategories, () => loadRegistrations(1))
```

#### Pass filters in `loadRegistrations` and `handleExportCsv`

```typescript
accommodationPreferences: selectedAccommodationPreferences.value.length > 0 ? selectedAccommodationPreferences.value : undefined,
attendancePeriods: selectedAttendancePeriods.value.length > 0 ? selectedAttendancePeriods.value : undefined,
ageCategories: selectedAgeCategories.value.length > 0 ? selectedAgeCategories.value : undefined,
```

#### Template — replace accommodation MultiSelect

Replace the existing `<MultiSelect … placeholder="Alojamiento">` with:

```html
<MultiSelect
  v-if="accommodationPreferenceOptions.length > 0"
  v-model="selectedAccommodationPreferences"
  :options="accommodationPreferenceOptions"
  optionLabel="label"
  dataKey="accommodationId"
  placeholder="Alojamiento"
  display="chip"
  :showSelectAll="false"
  class="w-72"
  :loading="filterOptionsLoading"
  data-testid="accommodation-preference-filter"
  aria-label="Filtrar por preferencia de alojamiento"
/>
```

Note: `dataKey` alone is not sufficient to make PrimeVue compare objects by identity — use a custom `equalityKey` or manage selection state manually if PrimeVue requires it. Verify chip display works as expected for object-valued options.

#### Template — add new filters after Extras MultiSelect

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

#### Fix Extras MultiSelect

Add `:showSelectAll="false"` to the existing Extras `<MultiSelect>`.

---

## Files to Modify

| Layer    | File |
|----------|------|
| Backend  | `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs` |
| Backend  | `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` |
| Backend  | `tests/.../RegistrationsRepositoryTests.cs` (or equivalent) |
| Frontend | `frontend/src/types/registration.ts` |
| Frontend | `frontend/src/composables/useAdminRegistrations.ts` |
| Frontend | `frontend/src/components/admin/RegistrationsAdminPanel.vue` |

---

## Non-Functional Requirements

- **Performance**: All new filters use `EXISTS` subqueries (consistent with the existing extras filter pattern). The accommodation preference AND filter chains multiple `.Where()` calls, each translating to a separate correlated `EXISTS` — one per selected pair. Selecting 3 pairs produces 3 `EXISTS` clauses, which is acceptable given the small number of preference positions. No N+1 queries introduced.
- **Correctness**: Filters are AND-combined: a registration must satisfy all active filters simultaneously.
- **No migration needed**: All filtered fields (`AttendancePeriod`, `AgeCategory`) already exist on `RegistrationMember`.

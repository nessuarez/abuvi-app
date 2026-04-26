# feat: Admin Registrations List — Usability Improvements

## Context

The admin registrations panel (`RegistrationsAdminPanel.vue`) currently shows a paginated table
with columns: Familia, Representante, Email, Estado, Miembros, Total, Pagado, Pendiente, Creación.

Two data fields that exist in the domain — attendance periods and accommodation preferences — are
already used as _filters_ but are **not projected** into the `AdminRegistrationListItem` DTO.
Sorting is hardcoded to `CreatedAt DESC`. The camp edition selector auto-selects the first `Open`
edition regardless of its start date.

---

## Scope

Four independent improvements, each can be implemented as a separate sub-task:

1. **Attendance period column** — short-label (S1 / S2 / T / V)
2. **Accommodation type icons column**
3. **Column sorting** — Familia (alphabetical) and Creación (date)
4. **Camp selector UX** — show status badge, pre-select closest upcoming edition

---

## 1 — Attendance Period Column

### Mapping

| `AttendancePeriod` value | Short label | Meaning |
|---|---|---|
| `Complete` | `T` | Toda la semana |
| `FirstWeek` | `S1` | Semana 1 |
| `SecondWeek` | `S2` | Semana 2 |
| `WeekendVisit` | `V` | Visitas |

### Backend

**`RegistrationsModels.cs`**
- Add `List<AttendancePeriod> AttendancePeriods` to `AdminRegistrationListItem`.

```csharp
public record AdminRegistrationListItem(
    Guid Id,
    RegistrationFamilyUnitSummary FamilyUnit,
    RepresentativeSummary Representative,
    RegistrationStatus Status,
    int MemberCount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal AmountRemaining,
    DateTime CreatedAt,
    List<AttendancePeriod> AttendancePeriods,          // NEW
    List<RegistrationAccommodationPreferenceSummary> AccommodationPreferences  // NEW (see §2)
);
```

**`RegistrationsRepository.cs` — `GetAdminPagedAsync` projection**
- Join `RegistrationMembers` to collect distinct attendance periods per registration.
- EF Core approach: include `r.Members` in the query and project with `.Select(m => m.AttendancePeriod).Distinct().ToList()`.

```csharp
// In the Select projection
AttendancePeriods = r.Members
    .Select(m => m.AttendancePeriod)
    .Distinct()
    .ToList()
```

> The query already joins `RegistrationMembers` for the attendance period filter; this only
> adds it to the SELECT clause.

### Frontend

**`frontend/src/types/registration.ts`**

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
  attendancePeriods: AttendancePeriod[]           // NEW
  accommodationPreferences: AccommodationPreferenceResponse[]  // NEW (see §2)
}
```

**`RegistrationsAdminPanel.vue`**

Add a utility function (can be placed in `frontend/src/utils/registration.ts` or inline):

```ts
const ATTENDANCE_SHORT: Record<AttendancePeriod, string> = {
  Complete: 'T',
  FirstWeek: 'S1',
  SecondWeek: 'S2',
  WeekendVisit: 'V',
}

const formatAttendancePeriods = (periods: AttendancePeriod[]): string =>
  periods.map(p => ATTENDANCE_SHORT[p]).join(' · ')
```

Add column after `Estado`:

```vue
<Column header="Período">
  <template #body="{ data }">
    <span class="text-sm font-mono text-gray-700">
      {{ formatAttendancePeriods(data.attendancePeriods) }}
    </span>
  </template>
</Column>
```

---

## 2 — Accommodation Type Icons Column

### Icon mapping

| `AccommodationType` | PrimeIcon | Tooltip label |
|---|---|---|
| `Lodge` | `pi pi-building` | Albergue |
| `Bungalow` | `pi pi-home` | Bungalow |
| `Tent` | `pi pi-sun` | Tienda |
| `Caravan` | `pi pi-car` | Caravana |
| `Motorhome` | `pi pi-truck` | Autocaravana |

> Note: PrimeVue 4 does not include a tent/caravan icon. `pi-sun` and `pi-car` are closest matches.
> If the visual distinction matters, consider using emoji (⛺ 🚐) as fallback text instead.

### Backend

**`RegistrationsModels.cs`**

Add new summary record:

```csharp
public record RegistrationAccommodationPreferenceSummary(
    string AccommodationName,
    AccommodationType AccommodationType,
    int PreferenceOrder
);
```

Add `List<RegistrationAccommodationPreferenceSummary> AccommodationPreferences` to
`AdminRegistrationListItem` (shown in §1 above).

**`RegistrationsRepository.cs` — `GetAdminPagedAsync` projection**

```csharp
AccommodationPreferences = r.AccommodationPreferences
    .OrderBy(ap => ap.PreferenceOrder)
    .Select(ap => new RegistrationAccommodationPreferenceSummary(
        ap.CampEditionAccommodation.Accommodation.Name,
        ap.CampEditionAccommodation.Accommodation.AccommodationType,
        ap.PreferenceOrder
    ))
    .ToList()
```

> Verify the navigation properties match the actual entity model. Include
> `CampEditionAccommodation.Accommodation` if not already in the query.

### Frontend

**`RegistrationsAdminPanel.vue`**

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

Add column after `Período`:

```vue
<Column header="Aloj.">
  <template #body="{ data }">
    <div class="flex gap-1 flex-wrap">
      <span
        v-for="pref in data.accommodationPreferences"
        :key="pref.preferenceOrder"
        v-tooltip.top="`${pref.preferenceOrder}ª opción: ${pref.accommodationName}`"
        class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-gray-100 text-gray-600"
      >
        <i :class="ACCOMMODATION_ICON[pref.accommodationType]" class="text-xs" />
      </span>
    </div>
  </template>
</Column>
```

Import `vTooltip` directive from PrimeVue if not already globally registered.

---

## 3 — Column Sorting (Familia + Creación)

The DataTable is in **lazy mode** (`:lazy`) so sorting must be server-side.

### Backend

**`RegistrationsModels.cs`**

```csharp
public enum AdminRegistrationSortBy { CreatedAt, FamilyName }
```

Update or create an `AdminRegistrationQuery` record:

```csharp
public record AdminRegistrationQuery(
    int Page,
    int PageSize,
    string? Search,
    RegistrationStatus? Status,
    List<(Guid AccommodationId, int PreferenceOrder)>? AccommodationPreferences,
    List<Guid>? ExtraIds,
    List<AttendancePeriod>? AttendancePeriods,
    List<AgeCategory>? AgeCategories,
    AdminRegistrationSortBy SortBy = AdminRegistrationSortBy.CreatedAt,   // NEW
    bool SortDescending = true                                              // NEW
);
```

**`RegistrationsRepository.cs`**

Replace `OrderByDescending(r => r.CreatedAt)` with:

```csharp
IQueryable<Registration> ApplySort(IQueryable<Registration> q) => sortBy switch {
    AdminRegistrationSortBy.FamilyName =>
        sortDescending ? q.OrderByDescending(r => r.FamilyUnit.Name)
                       : q.OrderBy(r => r.FamilyUnit.Name),
    _ =>
        sortDescending ? q.OrderByDescending(r => r.CreatedAt)
                       : q.OrderBy(r => r.CreatedAt),
};
```

**`RegistrationsEndpoints.cs`**

Add query params:

```csharp
app.MapGet("/api/camp-editions/{campEditionId}/registrations", async (
    ...
    [FromQuery] string? sortBy,
    [FromQuery] string? sortDirection,
    ...
) =>
{
    var parsedSortBy = sortBy?.ToLower() == "familyname"
        ? AdminRegistrationSortBy.FamilyName
        : AdminRegistrationSortBy.CreatedAt;
    var sortDescending = sortDirection?.ToLower() != "asc";
    ...
});
```

### Frontend

**`frontend/src/types/registration.ts`**

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
  sortBy?: 'createdAt' | 'familyName'    // NEW
  sortDirection?: 'asc' | 'desc'         // NEW
}
```

**`frontend/src/composables/useAdminRegistrations.ts`**

Pass sort params to the API query string alongside existing params.

**`RegistrationsAdminPanel.vue`**

```ts
// Track sort state
const sortField = ref<string>('createdAt')
const sortOrder = ref<1 | -1>(-1) // -1 = desc (PrimeVue convention)

const onSort = (event: { sortField: string; sortOrder: 1 | -1 }) => {
  sortField.value = event.sortField
  sortOrder.value = event.sortOrder
  loadRegistrations()
}
```

Map PrimeVue `sortField` to backend param:

```ts
const apiSortBy = computed(() =>
  sortField.value === 'familyUnit.name' ? 'familyName' : 'createdAt'
)
const apiSortDirection = computed(() =>
  sortOrder.value === 1 ? 'asc' : 'desc'
)
```

DataTable changes:

```vue
<DataTable
  ...
  :sort-field="sortField"
  :sort-order="sortOrder"
  @sort="onSort"
>
  <Column field="familyUnit.name" header="Familia" sortable>...</Column>
  ...
  <Column field="createdAt" header="Creación" sortable>...</Column>
```

---

## 4 — Camp Selector: Status Badge + Smart Pre-selection

### Smart pre-selection logic

Replace "first Open edition" with "nearest upcoming Open or Draft edition":

```ts
// In onMounted, after fetchAllEditions():
const today = new Date().toISOString().slice(0, 10) // YYYY-MM-DD

const upcoming = allEditions.value
  .filter(e => (e.status === 'Open' || e.status === 'Draft') && e.startDate >= today)
  .sort((a, b) => a.startDate.localeCompare(b.startDate))

if (upcoming.length > 0) {
  selectedEditionId.value = upcoming[0].id
} else {
  // Fallback: first Open edition regardless of date, then first edition overall
  const openEdition = allEditions.value.find(e => e.status === 'Open')
  selectedEditionId.value = openEdition?.id ?? allEditions.value[0]?.id ?? null
}
```

### Status display in the selector

Update `campEditionOptions` to include status, and use a custom option template on the `Select`
component to render a status badge alongside the camp name:

```ts
const STATUS_LABEL: Record<CampEditionStatus, string> = {
  Proposed: 'Propuesta',
  Draft: 'Borrador',
  Open: 'Abierto',
  Closed: 'Cerrado',
  Completed: 'Completado',
}

const STATUS_SEVERITY: Record<CampEditionStatus, string> = {
  Proposed: 'secondary',
  Draft: 'warn',
  Open: 'success',
  Closed: 'danger',
  Completed: 'info',
}

const campEditionOptions = computed(() =>
  allEditions.value.map(e => ({
    label: `${e.name ?? 'Campamento'} ${e.year}`,
    value: e.id,
    status: e.status,
  }))
)
```

```vue
<Select
  v-model="selectedEditionId"
  :options="campEditionOptions"
  option-label="label"
  option-value="value"
  placeholder="Seleccionar edición..."
  class="w-80"
  data-testid="edition-selector"
>
  <template #option="{ option }">
    <div class="flex items-center gap-2">
      <span>{{ option.label }}</span>
      <Tag
        :value="STATUS_LABEL[option.status]"
        :severity="STATUS_SEVERITY[option.status]"
        class="text-xs"
      />
    </div>
  </template>
  <template #value="{ value }">
    <div v-if="value" class="flex items-center gap-2">
      <span>{{ campEditionOptions.find(o => o.value === value)?.label }}</span>
      <Tag
        :value="STATUS_LABEL[campEditionOptions.find(o => o.value === value)?.status ?? 'Draft']"
        :severity="STATUS_SEVERITY[campEditionOptions.find(o => o.value === value)?.status ?? 'Draft']"
        class="text-xs"
      />
    </div>
    <span v-else class="text-gray-400">Seleccionar edición...</span>
  </template>
</Select>
```

---

## Acceptance Criteria

### Período column
- [ ] Each row shows the distinct attendance periods of all its members in short format
- [ ] Multiple periods shown separated by ` · ` (e.g., `S1 · S2`)
- [ ] Registrations with a single period show a single label (e.g., `T`)
- [ ] Column header: **Período**

### Aloj. column
- [ ] Each row shows one icon per accommodation preference, ordered by preference order
- [ ] Hovering an icon shows tooltip: `"1ª opción: <accommodation name>"`
- [ ] Registrations with no accommodation preferences show an empty cell (not an error)
- [ ] Column header: **Aloj.**

### Sorting
- [ ] Clicking "Familia" column header sorts alphabetically (A→Z then Z→A)
- [ ] Clicking "Creación" column header sorts by date (newest then oldest — default)
- [ ] Sort persists when paginating (current page resets to 1 on sort change)
- [ ] Default sort on load: Creación DESC (same as current behavior)

### Camp selector
- [ ] Every option in the dropdown shows a colored status badge (Abierto, Cerrado, etc.)
- [ ] On page load, pre-selects the nearest upcoming Open or Draft edition by `startDate`
- [ ] If no upcoming Open/Draft edition exists, falls back to first Open, then first overall

---

## Files to Modify

### Backend
| File | Change |
|---|---|
| `RegistrationsModels.cs` | Add `RegistrationAccommodationPreferenceSummary`, extend `AdminRegistrationListItem`, add `AdminRegistrationSortBy` enum |
| `RegistrationsRepository.cs` | Project `AttendancePeriods` and `AccommodationPreferences`; add dynamic sort |
| `RegistrationsEndpoints.cs` | Add `sortBy` / `sortDirection` query params |

### Frontend
| File | Change |
|---|---|
| `frontend/src/types/registration.ts` | Extend `AdminRegistrationListItem`; add sort fields to `AdminRegistrationFilters` |
| `frontend/src/composables/useAdminRegistrations.ts` | Pass sort params to API |
| `frontend/src/components/admin/RegistrationsAdminPanel.vue` | All four UI changes |

---

## Non-functional Requirements

- **Performance**: The `AttendancePeriods` and `AccommodationPreferences` projections add at most
  two joins already present in the filter subqueries. Verify with EF Core query logging that no
  N+1 queries are introduced (use `.Select()` projection, not `.Include()` + navigation properties
  in a loop).
- **Backward compatibility**: `AttendancePeriods` and `AccommodationPreferences` default to empty
  lists `[]` — no existing callers break.
- **CSV export**: The CSV export endpoint shares the same DTO. Consider whether `AttendancePeriods`
  and `AccommodationPreferences` columns should also appear in the CSV. If yes, update the CSV
  mapping accordingly.
- **Tests**: Add unit tests for `formatAttendancePeriods` helper. Add a repository integration
  test that verifies the new projection fields are populated correctly.

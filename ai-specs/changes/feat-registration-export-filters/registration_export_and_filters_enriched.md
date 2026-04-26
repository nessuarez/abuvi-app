# Registration Export & Advanced Filters

## Summary

After registration closes, admins need to work with registration data in ways the current list view doesn't support: filtering by accommodation type, filtering by extras selected, and exporting to CSV for offline analysis. The goal is to maximise what the UI can do natively (filtering) and provide a well-structured CSV export as the last resort for data not covered by the filters.

---

## Context

- **Current admin panel** (`RegistrationsAdminPanel.vue`): shows Family, Representative, Email, Status, Member Count, Total, Paid, Pending, Created Date. Existing filters: status, free-text search by name.
- **Extras** are dynamic per camp edition (`CampEditionExtra`): name, description, price, whether they require user input (e.g. T-shirt size). Registrations record selections in `RegistrationExtra` with quantity and the user input value.
- **Accommodation** is preference-based (1–3 ranked choices), not a confirmed assignment. Filtering means "has this type as any/first preference".
- **No export functionality exists** today.

---

## User Stories

### 1 — Advanced Filtering in the Admin Panel

**As an** admin/board member  
**I want to** filter the registrations list by accommodation type and by extras selected  
**So that** I can see grouped subsets (e.g. all families who requested a lodge, all who signed up for kayaking) without exporting to Excel.

#### Acceptance criteria

1. A **"Tipo de alojamiento"** multi-select filter appears in the filter bar. Options are the accommodation types that are active for the selected camp edition (loaded from `GET /api/camp-editions/{id}/accommodations`). Selecting one or more types narrows the list to registrations that have at least one of those types among their preferences (any preference order).
2. An **"Extras"** multi-select filter appears in the filter bar. Options are the active extras for the selected camp edition (loaded from `GET /api/camp-editions/{id}/extras`). Selecting one or more extras narrows the list to registrations that selected at least one of those extras (quantity > 0).
3. Both filters compose with the existing status and search filters (all conditions are ANDed).
4. When a different camp edition is selected, both filter selections reset and options reload.
5. The footer totals row reflects the filtered subset.
6. URL query parameters are **not** required to persist filter state across reloads (in-memory state is fine).

---

### 2 — CSV Export

**As an** admin/board member  
**I want to** export the current filtered registration list to a CSV file  
**So that** I can do ad-hoc analysis (e.g. build accommodation lists, T-shirt size tally) in a spreadsheet when the UI filters aren't enough.

#### Acceptance criteria

1. An **"Exportar CSV"** button appears in the admin panel toolbar (next to edition selector, role-gated to Admin/Board).
2. Clicking it triggers a file download named `inscripciones-{camp-edition-slug}-{YYYY-MM-DD}.csv`.
3. The export respects the **current active filters** (status, search, accommodation type, extras).
4. The CSV is UTF-8 with BOM (for Excel compatibility on Windows/Mac).
5. The CSV contains **one row per registration** (not per member) with the columns listed below.
6. Extras columns are **dynamic**: one column per active extra in the camp edition, ordered by `sort_order`. The column header is the extra name. The value is the quantity selected (integer), or `0` if the registration did not select that extra. If the extra `RequiresUserInput`, an adjacent column `{extra name} - Detalle` contains the user's input value (or empty string).
7. If no registrations match the current filters, the export still downloads a CSV with only the header row.

#### CSV column specification (fixed columns first, then dynamic)

| # | Column name | Source |
|---|-------------|--------|
| 1 | `ID Inscripción` | `Registration.Id` |
| 2 | `Familia` | `FamilyUnit.Name` |
| 3 | `Representante` | `User.FirstName + LastName` |
| 4 | `Email` | `User.Email` |
| 5 | `Teléfono` | `User.Phone` |
| 6 | `Estado` | `Registration.Status` (translated: Pending→Pendiente, Confirmed→Confirmada, Cancelled→Cancelada, Draft→Borrador) |
| 7 | `Nº Miembros` | count of `RegistrationMember` rows |
| 8 | `Miembros` | semicolon-separated list of `{FirstName} {LastName} ({AgeCategory}, {AttendancePeriod})` |
| 9 | `Preferencia alojamiento 1` | name of 1st-choice accommodation, or empty |
| 10 | `Tipo alojamiento 1` | `AccommodationType` of 1st choice (translated: Lodge→Albergue, Tent→Tienda, Caravan→Caravana, Bungalow→Bungalow, Motorhome→Autocaravana) |
| 11 | `Preferencia alojamiento 2` | name of 2nd-choice accommodation, or empty |
| 12 | `Tipo alojamiento 2` | `AccommodationType` of 2nd choice |
| 13 | `Preferencia alojamiento 3` | name of 3rd-choice accommodation, or empty |
| 14 | `Tipo alojamiento 3` | `AccommodationType` of 3rd choice |
| 15 | `Necesidades especiales` | `Registration.SpecialNeeds` |
| 16 | `Preferencia compañeros` | `Registration.CampatesPreference` |
| 17 | `Tiene mascota` | `Registration.HasPet` (Sí/No) |
| 18 | `Notas` | `Registration.Notes` |
| 19 | `Base (€)` | `Registration.BaseTotalAmount` |
| 20 | `Extras (€)` | `Registration.ExtrasAmount` |
| 21 | `Total (€)` | `Registration.TotalAmount` |
| 22 | `Pagado (€)` | sum of completed payments |
| 23 | `Pendiente (€)` | Total − Pagado |
| 24 | `Fecha inscripción` | `Registration.CreatedAt` formatted as `dd/MM/yyyy` |
| 25+ | `{Extra name}` | quantity selected (0 if not selected) |
| 26+ | `{Extra name} - Detalle` | user input value (only present if `RequiresUserInput = true`) |

---

## Backend

### New query parameters on existing list endpoint

`GET /api/camp-editions/{campEditionId}/registrations`

Add optional query params:

| Param | Type | Description |
|-------|------|-------------|
| `accommodationTypes` | `string[]` | Filter by accommodation type enum values (e.g. `Lodge`, `Tent`). Match if any preference order contains this type. OR-logic within parameter. |
| `extraIds` | `Guid[]` | Filter registrations that selected at least one of these extras (quantity > 0). OR-logic within parameter. |

Both new params compose with existing `search` and `status` via AND.

**Repository method to add** (`RegistrationsRepository`):

```csharp
Task<PagedResult<RegistrationAdminSummary>> GetAdminPagedAsync(
    Guid campEditionId,
    int page,
    int pageSize,
    string? search,
    RegistrationStatus? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,  // NEW
    IReadOnlyList<Guid>? extraIds,                         // NEW
    CancellationToken ct);
```

EF Core filtering:
- `accommodationTypes`: `.Where(r => r.AccommodationPreferences.Any(p => accommodationTypes.Contains(p.CampEditionAccommodation.AccommodationType)))`
- `extraIds`: `.Where(r => r.Extras.Any(e => extraIds.Contains(e.CampEditionExtraId) && e.Quantity > 0))`

### New export endpoint

```
GET /api/camp-editions/{campEditionId}/registrations/export/csv
```

**Auth**: Admin or Board role required.

**Query params**: Same as the list endpoint (`search`, `status`, `accommodationTypes`, `extraIds`). No pagination — returns all matching rows.

**Response**: 
- `Content-Type: text/csv; charset=utf-8`
- `Content-Disposition: attachment; filename="inscripciones-{slug}-{date}.csv"`
- UTF-8 with BOM (`\xEF\xBB\xBF`)

**Implementation**:
1. Fetch all matching registrations with full details (members, extras, accommodation preferences, payments, family unit, user). Use a single query with `.Include()` chains — do **not** lazy-load.
2. Fetch all active `CampEditionExtra` for the edition (ordered by `SortOrder`) to build dynamic columns.
3. Build CSV in memory using `StringBuilder` or `StreamWriter` (no external CSV library needed for this size).
4. Stream response with `Results.Stream(...)`.

**Service method**:
```csharp
Task<(byte[] content, string fileName)> ExportToCsvAsync(
    Guid campEditionId,
    string? search,
    RegistrationStatus? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct);
```

Place in `RegistrationsService.cs`. No new file needed.

**Repository method to add**:
```csharp
Task<IReadOnlyList<Registration>> GetAllForExportAsync(
    Guid campEditionId,
    string? search,
    RegistrationStatus? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct);
```

Must eagerly load:
```csharp
.Include(r => r.FamilyUnit)
.Include(r => r.RegisteredByUser)
.Include(r => r.Members).ThenInclude(m => m.FamilyMember)
.Include(r => r.Extras).ThenInclude(e => e.CampEditionExtra)
.Include(r => r.AccommodationPreferences)
    .ThenInclude(p => p.CampEditionAccommodation)
.Include(r => r.Payments)
```

**Files to modify**:
- `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` — add export endpoint + update list endpoint params
- `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` — add `ExportToCsvAsync`
- `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs` — add `GetAllForExportAsync`, update `GetAdminPagedAsync`

No migration needed (no schema change).

---

## Frontend

### Filter bar additions (`RegistrationsAdminPanel.vue`)

Add two new filter controls below the existing ones (or inline if space allows):

1. **Accommodation type multi-select** (`MultiSelect` from PrimeVue):
   - Label: `Tipo de alojamiento`
   - Options: loaded via `GET /api/camp-editions/{id}/accommodations` when edition changes. Map `accommodationType` enum to Spanish label (Lodge→Albergue, Tent→Tienda, Caravan→Caravana, Bungalow→Bungalow, Motorhome→Autocaravana).
   - Bind to `selectedAccommodationTypes: AccommodationType[]`.
   - On change: re-fetch registrations with new filter.

2. **Extras multi-select** (`MultiSelect`):
   - Label: `Extras`
   - Options: loaded via `GET /api/camp-editions/{id}/extras` when edition changes. Display extra name.
   - Bind to `selectedExtraIds: string[]`.
   - On change: re-fetch registrations with new filter.

Both filters reset when the edition selector changes.

### Export button

Add a `Button` with label `Exportar CSV` and icon `pi pi-download` to the toolbar (visible only to Admin/Board roles, check `auth.isBoard`).

On click, call the export endpoint with current filter params. Trigger browser download via:
```typescript
const blob = new Blob([response.data], { type: 'text/csv;charset=utf-8;' })
const url = URL.createObjectURL(blob)
const link = document.createElement('a')
link.href = url
link.download = `inscripciones-${editionSlug}-${today}.csv`
link.click()
URL.revokeObjectURL(url)
```

Use `responseType: 'blob'` in the Axios call.

### Composable changes (`useAdminRegistrations.ts`)

Extend to:
- Accept `accommodationTypes` and `extraIds` params in the fetch function.
- Add `exportToCsv(filters)` function that calls the export endpoint and triggers download.
- Add `fetchEditionExtras(editionId)` and `fetchEditionAccommodations(editionId)` for populating filter options.

**Files to modify**:
- `frontend/src/components/admin/RegistrationsAdminPanel.vue`
- `frontend/src/composables/useAdminRegistrations.ts`
- `frontend/src/types/registration.ts` — add filter param types if missing

---

## Testing

### Backend unit tests (`Abuvi.Tests/Unit/Features/Registrations/`)

- `RegistrationsServiceTests`: `ExportToCsvAsync_WithNoRegistrations_ReturnsHeaderOnlyFile`, `ExportToCsvAsync_WithRegistrations_IncludesDynamicExtraColumns`, `ExportToCsvAsync_WithExtrasFilter_OnlyIncludesMatchingRegistrations`, `ExportToCsvAsync_WithAccommodationTypeFilter_OnlyIncludesMatchingRegistrations`
- `RegistrationsEndpointsTests`: `GetRegistrations_WithAccommodationTypeFilter_Returns200`, `ExportCsv_WithoutBoardRole_Returns403`, `ExportCsv_WithBoardRole_ReturnsCsvFile`

### Frontend unit tests

- `useAdminRegistrations.test.ts`: `exportToCsv_triggersDownload_withCurrentFilters`, `fetchEditionExtras_resetsSelectedExtras_onEditionChange`

---

## Non-functional requirements

- **Performance**: The export endpoint must complete in < 5 s for editions with up to 500 registrations. Use `AsNoTracking()` on all export queries.
- **Security**: Export endpoint requires `Board` or `Admin` role (same as the admin list endpoint). Validate `campEditionId` exists before processing.
- **Privacy (RGPD)**: The CSV contains personal data (name, email, phone, special needs). Access is already restricted to board members. No additional storage or logging of exported data is required.
- **Encoding**: UTF-8 with BOM ensures correct display in Excel without a manual import wizard.
- **CSV injection**: Sanitize all string values — prefix with a space any value starting with `=`, `+`, `-`, or `@` to prevent formula injection in spreadsheet tools.

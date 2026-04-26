# Backend Implementation Plan: Admin Registrations List — Usability Improvements

## Overview

Three backend changes are bundled because they all touch the same four files inside
`Features/Registrations/`. No database migration is required — the data already exists;
we are only projecting more of it and adding a sort parameter.

**Changes in scope:**
1. Add `AttendancePeriods` (distinct, per-registration) to `AdminRegistrationListItem`
2. Add `AccommodationPreferences` (ordered list, per-registration) to `AdminRegistrationListItem`
3. Add server-side sorting (`sortBy` / `sortDirection`) to the admin list endpoint

Architecture: Vertical Slice (`Features/Registrations/`). The four files touched are
`RegistrationsModels.cs`, `RegistrationsRepository.cs`, `RegistrationsService.cs`, and
`RegistrationsEndpoints.cs`.

---

## Architecture Context

| Layer | File | Role |
|---|---|---|
| Models/DTOs | `Features/Registrations/RegistrationsModels.cs` | Records, enums, projection types |
| Repository | `Features/Registrations/RegistrationsRepository.cs` | EF Core queries |
| Service | `Features/Registrations/RegistrationsService.cs` | Orchestration & DTO mapping |
| Endpoint | `Features/Registrations/RegistrationsEndpoints.cs` | HTTP binding |
| Tests | `Tests/Unit/Features/Registrations/AdminRegistrationServiceTests.cs` | Unit tests to update |

**No new files needed.** No migration required (no schema changes).

**Data already exists:**
- Attendance periods: `registration_members.attendance_period` (one row per member)
- Accommodation preferences: `registration_accommodation_preferences` joined to
  `camp_edition_accommodations` (navigation: `RegistrationAccommodationPreference
  → CampEditionAccommodation.{Name, AccommodationType}`)

---

## Implementation Steps

### Step 0: Create Feature Branch

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-registration-admin-list-improvements-backend
```

---

### Step 1: Update Models (`RegistrationsModels.cs`)

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

#### 1a. Add new summary record

Add after `AccommodationPreferenceResponse`:

```csharp
/// <summary>Lightweight accommodation preference for the admin registration list.</summary>
public record AdminRegistrationAccommodationSummary(
    string AccommodationName,
    AccommodationType AccommodationType,
    int PreferenceOrder
);
```

> Uses `AccommodationType` from `Abuvi.API.Features.Camps` — the existing `using` already covers it.

#### 1b. Add sort enum

```csharp
public enum AdminRegistrationSortBy { CreatedAt, FamilyName }
```

#### 1c. Extend `AdminRegistrationProjection`

Add two fields at the end of the existing record:

```csharp
public record AdminRegistrationProjection(
    Guid Id,
    Guid FamilyUnitId,
    string FamilyUnitName,
    Guid RepresentativeUserId,
    string RepresentativeFirstName,
    string RepresentativeLastName,
    string RepresentativeEmail,
    RegistrationStatus Status,
    int MemberCount,
    decimal TotalAmount,
    decimal AmountPaid,
    DateTime CreatedAt,
    List<AttendancePeriod> AttendancePeriods,                          // NEW
    List<AdminRegistrationAccommodationSummary> AccommodationPreferences  // NEW
);
```

#### 1d. Extend `AdminRegistrationListItem`

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
    List<AttendancePeriod> AttendancePeriods,                          // NEW
    List<AdminRegistrationAccommodationSummary> AccommodationPreferences  // NEW
);
```

---

### Step 2: Update Repository Interface and Implementation (`RegistrationsRepository.cs`)

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`

#### 2a. Update `IRegistrationsRepository` interface

Add two parameters with defaults at the end of `GetAdminPagedAsync`:

```csharp
Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
    GetAdminPagedAsync(
        Guid campEditionId, int page, int pageSize,
        string? search, string? status,
        IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,
        IReadOnlyList<Guid>? extraIds,
        IReadOnlyList<AttendancePeriod>? attendancePeriods,
        IReadOnlyList<AgeCategory>? ageCategories,
        AdminRegistrationSortBy sortBy,           // NEW (no default on interface)
        bool sortDescending,                       // NEW (no default on interface)
        CancellationToken ct);
```

#### 2b. Update `RegistrationsRepository.GetAdminPagedAsync` implementation

**2b-i. Add the two new parameters** to the method signature (matching the interface).

**2b-ii. Replace the hardcoded sort** with a dynamic one.

Replace:
```csharp
var items = await query
    .OrderByDescending(x => x.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(ct);
```

With:
```csharp
var ordered = sortBy switch
{
    AdminRegistrationSortBy.FamilyName =>
        sortDescending
            ? query.OrderByDescending(x => x.FamilyUnitName)
            : query.OrderBy(x => x.FamilyUnitName),
    _ =>
        sortDescending
            ? query.OrderByDescending(x => x.CreatedAt)
            : query.OrderBy(x => x.CreatedAt),
};

var items = await ordered
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(ct);
```

**2b-iii. After pagination, fetch related data in bulk (split-query pattern)**

Add after `var items = await ordered...`:

```csharp
var ids = items.Select(x => x.Id).ToList();

// Fetch distinct attendance periods per registration (one query, IN clause)
var periodsByReg = await db.RegistrationMembers
    .AsNoTracking()
    .Where(m => ids.Contains(m.RegistrationId))
    .GroupBy(m => m.RegistrationId)
    .Select(g => new
    {
        RegistrationId = g.Key,
        Periods = g.Select(m => m.AttendancePeriod).Distinct().ToList()
    })
    .ToDictionaryAsync(x => x.RegistrationId, x => x.Periods, ct);

// Fetch accommodation preferences per registration (one query, IN clause)
var accommodationRows = await db.RegistrationAccommodationPreferences
    .AsNoTracking()
    .Where(p => ids.Contains(p.RegistrationId))
    .OrderBy(p => p.PreferenceOrder)
    .Select(p => new
    {
        p.RegistrationId,
        AccommodationName = p.CampEditionAccommodation.Name,
        AccommodationType = p.CampEditionAccommodation.AccommodationType,
        p.PreferenceOrder
    })
    .ToListAsync(ct);

var accommodationsByReg = accommodationRows
    .GroupBy(p => p.RegistrationId)
    .ToDictionary(
        g => g.Key,
        g => g.Select(p => new AdminRegistrationAccommodationSummary(
            p.AccommodationName, p.AccommodationType, p.PreferenceOrder)).ToList());
```

**2b-iv. Update the projection mapping** to populate the new fields:

```csharp
var projections = items.Select(x => new AdminRegistrationProjection(
    x.Id, x.FamilyUnitId, x.FamilyUnitName,
    x.RepresentativeUserId, x.RepresentativeFirstName,
    x.RepresentativeLastName, x.RepresentativeEmail,
    x.Status, x.MemberCount, x.TotalAmount, x.AmountPaid, x.CreatedAt,
    periodsByReg.GetValueOrDefault(x.Id, []),          // AttendancePeriods
    accommodationsByReg.GetValueOrDefault(x.Id, [])    // AccommodationPreferences
)).ToList();
```

> **Why split queries?** The main query uses a LINQ query-syntax anonymous type with inline
> correlated subqueries (`Count`, `Sum`). EF Core cannot include collection-returning subqueries
> (`.ToList()`) inside that anonymous type and translate them to SQL. The split-query approach
> issues two extra `WHERE id IN (...)` queries after the paginated fetch — no N+1, and the
> filters/aggregations on the main query remain unchanged.

---

### Step 3: Update Service (`RegistrationsService.cs`)

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

#### 3a. Add parameters to `GetAdminListAsync`

```csharp
public async Task<AdminRegistrationListResponse> GetAdminListAsync(
    Guid campEditionId, int page, int pageSize, string? search, string? status,
    IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,
    IReadOnlyList<Guid>? extraIds,
    IReadOnlyList<AttendancePeriod>? attendancePeriods,
    IReadOnlyList<AgeCategory>? ageCategories,
    AdminRegistrationSortBy sortBy,           // NEW
    bool sortDescending,                       // NEW
    CancellationToken ct)
```

#### 3b. Pass new params to repository

```csharp
var (items, totalCount, totals) = await registrationsRepo.GetAdminPagedAsync(
    campEditionId, page, pageSize, search, status,
    accommodationPreferences, extraIds, attendancePeriods, ageCategories,
    sortBy, sortDescending,   // NEW
    ct);
```

#### 3c. Update `AdminRegistrationListItem` mapping

```csharp
Items: items.Select(p => new AdminRegistrationListItem(
    p.Id,
    new RegistrationFamilyUnitSummary(p.FamilyUnitId, p.FamilyUnitName, p.RepresentativeUserId),
    new RepresentativeSummary(p.RepresentativeUserId, p.RepresentativeFirstName,
        p.RepresentativeLastName, p.RepresentativeEmail),
    p.Status,
    p.MemberCount,
    p.TotalAmount,
    p.AmountPaid,
    p.TotalAmount - p.AmountPaid,
    p.CreatedAt,
    p.AttendancePeriods,          // NEW
    p.AccommodationPreferences    // NEW
)).ToList(),
```

---

### Step 4: Update Endpoint (`RegistrationsEndpoints.cs`)

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

#### 4a. Add query parameters to `GetAdminRegistrations`

```csharp
private static async Task<IResult> GetAdminRegistrations(
    Guid campEditionId,
    RegistrationsService service,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] string? status = null,
    [FromQuery] Guid[]? accommodationIds = null,
    [FromQuery] int[]? accommodationPreferenceOrders = null,
    [FromQuery] Guid[]? extraIds = null,
    [FromQuery] string[]? attendancePeriods = null,
    [FromQuery] string[]? ageCategories = null,
    [FromQuery] string? sortBy = null,          // NEW: "createdAt" | "familyName"
    [FromQuery] string? sortDirection = null,   // NEW: "asc" | "desc"
    CancellationToken ct = default)
```

#### 4b. Parse sort params before calling service

Add after the existing `parsedAgeCategories` block:

```csharp
var parsedSortBy = sortBy?.ToLowerInvariant() == "familyname"
    ? AdminRegistrationSortBy.FamilyName
    : AdminRegistrationSortBy.CreatedAt;

var sortDescending = sortDirection?.ToLowerInvariant() != "asc"; // default: desc
```

#### 4c. Pass to service

```csharp
var result = await service.GetAdminListAsync(
    campEditionId, page, pageSize, search, status,
    accommodationPreferences,
    extraIds?.Distinct().ToList(),
    parsedAttendancePeriods?.Count > 0 ? parsedAttendancePeriods : null,
    parsedAgeCategories?.Count > 0 ? parsedAgeCategories : null,
    parsedSortBy, sortDescending,   // NEW
    ct);
```

---

### Step 5: Update Tests (`AdminRegistrationServiceTests.cs`)

**File:** `src/Abuvi.Tests/Unit/Features/Registrations/AdminRegistrationServiceTests.cs`

The existing tests must be updated because `AdminRegistrationProjection` has two new constructor
parameters, and `GetAdminPagedAsync` has two new parameters.

#### 5a. Update `AdminRegistrationProjection` construction in all test helpers

Every `new AdminRegistrationProjection(...)` call must include the two new trailing fields:

```csharp
new AdminRegistrationProjection(
    Guid.NewGuid(), FamilyUnitId, "García Family", UserId,
    "Juan", "García", "juan@test.com",
    RegistrationStatus.Pending, 3, 900m, 200m, DateTime.UtcNow,
    [AttendancePeriod.Complete],                          // AttendancePeriods
    []                                                     // AccommodationPreferences
)
```

#### 5b. Update `_repo.GetAdminPagedAsync(...)` mock setups

All NSubstitute `.Returns(...)` calls on `GetAdminPagedAsync` must add the two new parameters.
Use `Arg.Any<AdminRegistrationSortBy>()` and `Arg.Any<bool>()` to keep tests non-brittle:

```csharp
_repo.GetAdminPagedAsync(
    CampEditionId, 1, 20, null, null, null, null, null, null,
    Arg.Any<AdminRegistrationSortBy>(), Arg.Any<bool>(),  // NEW
    Arg.Any<CancellationToken>())
    .Returns((projections, 1, totals));
```

#### 5c. Add new test cases

Add to `AdminRegistrationServiceTests`:

```
GetAdminListAsync_DefaultSort_IsCreatedAtDescending
GetAdminListAsync_SortByFamilyName_PassesFamilyNameSortToRepository
GetAdminListAsync_WhenRegistrationHasMembers_ReturnsAttendancePeriodsInItem
GetAdminListAsync_WhenRegistrationHasAccommodationPreferences_ReturnsPreferencesInItem
GetAdminListAsync_WhenRegistrationHasNoAccommodationPreferences_ReturnsEmptyList
```

Each test follows the AAA pattern: Arrange mock with specific projection data, Act by calling
`GetAdminListAsync`, Assert the resulting `AdminRegistrationListItem` contains the expected values.

---

### Step 6: Update Technical Documentation

**File:** `ai-specs/specs/api-spec.yml` (if maintained) — add `sortBy` and `sortDirection` to
the `GET /api/camp-editions/{campEditionId}/registrations` operation's query parameters, and
add `attendancePeriods` and `accommodationPreferences` to `AdminRegistrationListItem` schema.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Models (new record, enum, extended projection and list item)
3. Step 2 — Repository (interface + implementation: dynamic sort + split queries)
4. Step 3 — Service (add params, pass through, update mapping)
5. Step 4 — Endpoint (add query params, parse, pass to service)
6. Step 5 — Update and extend tests
7. Step 6 — Documentation

---

## Testing Checklist

- [ ] All existing tests in `AdminRegistrationServiceTests.cs` pass after updating
  `AdminRegistrationProjection` constructor calls and mock setups
- [ ] New test: `GetAdminListAsync_DefaultSort_IsCreatedAtDescending` — verifies
  `sortBy = CreatedAt, sortDescending = true` are passed when no query params provided
- [ ] New test: `GetAdminListAsync_SortByFamilyName_PassesFamilyNameSortToRepository`
- [ ] New test: attendance periods and accommodation preferences flow through service to list item
- [ ] New test: empty attendance periods / no accommodation preferences → empty lists (not null)
- [ ] `dotnet build` passes with no warnings on nullable reference types
- [ ] Manual smoke test: call endpoint with `sortBy=familyName&sortDirection=asc` and verify order

---

## Error Response Format

No new error cases introduced. The endpoint continues to return:

| Status | Condition |
|---|---|
| 200 OK | List returned (may be empty) |
| 404 Not Found | `campEditionId` does not exist |
| 401/403 | Not authenticated or not Admin/Board |

---

## Dependencies

No new NuGet packages. No EF Core migration.

---

## Notes

- **No breaking change**: `AttendancePeriods` and `AccommodationPreferences` default to `[]`
  in all code paths; existing callers of the API receive the new fields transparently.
- **Default sort behaviour unchanged**: omitting `sortBy` / `sortDirection` defaults to
  `CreatedAt DESC` — the current behaviour.
- **`sortBy` value is case-insensitive** (`familyname` = `FamilyName` = `FAMILYNAME`).
- **N+1 prevention**: the two split queries use `ids.Contains(...)` which EF Core translates
  to a single `WHERE id IN (...)` SQL statement. Verify with EF Core query logging or
  `EF.CompileAsyncQuery` during review.
- **CSV export** (`GetAllForExportAsync`): not in scope. The export already navigates
  `r.AccommodationPreferences` and `r.Members` via `Include`. If the CSV columns need updating,
  that is a separate ticket.

---

## Next Steps After Implementation

- Frontend ticket: update `AdminRegistrationListItem` TypeScript type, add Período and Aloj.
  columns, and wire up `@sort` event in `RegistrationsAdminPanel.vue` (separate branch).

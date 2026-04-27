# Backend Implementation Plan: feat-registration-admin-filters — Additional Filters for Admin Registrations Screen

## Overview

Extend the admin registrations list and CSV export with three new filter capabilities:

1. **Accommodation preference filter** (replaces the existing accommodation type filter): filter by specific `CampEditionAccommodation` + preference position (1, 2, or 3). Multiple selected pairs are AND-combined — each pair must independently match a preference on the registration.
2. **Attendance period filter**: filter registrations that include at least one member with a given `AttendancePeriod` (Complete, FirstWeek, SecondWeek, WeekendVisit).
3. **Age category filter**: filter registrations that include at least one member with a given `AgeCategory` (Baby, Child, Adult).

No DB migration is needed — all filtered fields already exist on `RegistrationMember` and `RegistrationAccommodationPreference`. The change is purely additive query logic + parameter threading through repository → service → endpoint.

The feature follows **Vertical Slice Architecture**: all changes stay within `Features/Registrations/`.

---

## Architecture Context

**Feature slice**: `src/Abuvi.API/Features/Registrations/`

**Files to modify** (no new files):

| File | Change |
|------|--------|
| `RegistrationsModels.cs` | Add `AccommodationPreferenceFilter` record |
| `RegistrationsRepository.cs` | Update interface + implementation for both paged and export queries |
| `RegistrationsService.cs` | Update `GetAdminListAsync` and `ExportToCsvAsync` signatures |
| `RegistrationsEndpoints.cs` | Update `GetAdminRegistrations` and `ExportRegistrationsToCsv` handlers |
| `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs` | Add tests for the new filter parameters |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch before any code changes.
- **Branch name**: `feature/feat-registration-admin-filters-backend`
- **Base branch**: `dev`

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-registration-admin-filters-backend
git branch  # verify
```

---

### Step 1: Add `AccommodationPreferenceFilter` Record

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

Add after the existing enums/records (near the bottom of the models file):

```csharp
/// <summary>
/// Represents a filter pair: a specific accommodation at a specific preference position.
/// AND-combined when multiple pairs are provided.
/// </summary>
public record AccommodationPreferenceFilter(Guid AccommodationId, int PreferenceOrder);
```

No other model changes are needed — `AttendancePeriod` and `AgeCategory` already exist.

---

### Step 2: Update `IRegistrationsRepository` Interface

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`

Replace the `accommodationTypes` parameter with `accommodationPreferences` and add two new parameters to both interface methods:

**`GetAdminPagedAsync` — before:**
```csharp
Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
    GetAdminPagedAsync(
        Guid campEditionId, int page, int pageSize,
        string? search, string? status,
        IReadOnlyList<AccommodationType>? accommodationTypes,
        IReadOnlyList<Guid>? extraIds,
        CancellationToken ct);
```

**`GetAdminPagedAsync` — after:**
```csharp
Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
    GetAdminPagedAsync(
        Guid campEditionId, int page, int pageSize,
        string? search, string? status,
        IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,
        IReadOnlyList<Guid>? extraIds,
        IReadOnlyList<AttendancePeriod>? attendancePeriods,
        IReadOnlyList<AgeCategory>? ageCategories,
        CancellationToken ct);
```

**`GetAllForExportAsync` — before:**
```csharp
Task<IReadOnlyList<Registration>> GetAllForExportAsync(
    Guid campEditionId,
    string? search,
    string? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct);
```

**`GetAllForExportAsync` — after:**
```csharp
Task<IReadOnlyList<Registration>> GetAllForExportAsync(
    Guid campEditionId,
    string? search,
    string? status,
    IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,
    IReadOnlyList<Guid>? extraIds,
    IReadOnlyList<AttendancePeriod>? attendancePeriods,
    IReadOnlyList<AgeCategory>? ageCategories,
    CancellationToken ct);
```

---

### Step 3: Update `RegistrationsRepository` Implementation

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`

#### 3a — `GetAdminPagedAsync`

Update the method signature to match the new interface. Replace the existing accommodation type filter block with the AND-looping pattern, then add the two new filter blocks:

```csharp
// ── REPLACE the existing accommodationTypes filter block ──────────────────

// Accommodation preference filter — AND across pairs (each pair adds one EXISTS clause)
if (accommodationPreferences?.Count > 0)
{
    foreach (var f in accommodationPreferences)
    {
        var accommodationId = f.AccommodationId;   // capture for LINQ closure
        var preferenceOrder = f.PreferenceOrder;
        query = query.Where(x =>
            db.RegistrationAccommodationPreferences.Any(p =>
                p.RegistrationId == x.Id &&
                p.CampEditionAccommodationId == accommodationId &&
                p.PreferenceOrder == preferenceOrder));
    }
}

// ── ADD after the existing extraIds filter block ──────────────────────────

// Attendance period filter — OR across selected periods (registration has at least one matching member)
if (attendancePeriods?.Count > 0)
{
    query = query.Where(x =>
        db.RegistrationMembers.Any(m =>
            m.RegistrationId == x.Id &&
            attendancePeriods.Contains(m.AttendancePeriod)));
}

// Age category filter — OR across selected categories
if (ageCategories?.Count > 0)
{
    query = query.Where(x =>
        db.RegistrationMembers.Any(m =>
            m.RegistrationId == x.Id &&
            ageCategories.Contains(m.AgeCategory)));
}
```

> **Important**: Local variable capture (`accommodationId`, `preferenceOrder`) inside the `foreach` is required. Without it, LINQ closures would capture the loop variable by reference, and all clauses would filter by the last iteration's values.

> **Query pattern note**: `attendancePeriods.Contains(m.AttendancePeriod)` translates to `IN (...)` in SQL when EF Core is given a local list. This is the same pattern used by `extraIds.Contains(...)` already in the file.

#### 3b — `GetAllForExportAsync`

Update the method signature and replace/add the same filter logic using navigation properties (the pattern already in use in this method):

```csharp
// ── REPLACE the existing accommodationTypes filter block ──────────────────

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

// ── ADD after the existing extraIds filter block ──────────────────────────

if (attendancePeriods?.Count > 0)
    query = query.Where(r =>
        r.Members.Any(m => attendancePeriods.Contains(m.AttendancePeriod)));

if (ageCategories?.Count > 0)
    query = query.Where(r =>
        r.Members.Any(m => ageCategories.Contains(m.AgeCategory)));
```

---

### Step 4: Update `RegistrationsService.GetAdminListAsync`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**Method signature — before:**
```csharp
public async Task<AdminRegistrationListResponse> GetAdminListAsync(
    Guid campEditionId, int page, int pageSize, string? search, string? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct)
```

**Method signature — after:**
```csharp
public async Task<AdminRegistrationListResponse> GetAdminListAsync(
    Guid campEditionId, int page, int pageSize, string? search, string? status,
    IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,
    IReadOnlyList<Guid>? extraIds,
    IReadOnlyList<AttendancePeriod>? attendancePeriods,
    IReadOnlyList<AgeCategory>? ageCategories,
    CancellationToken ct)
```

Update the `registrationsRepo.GetAdminPagedAsync(...)` call to pass through the new parameters:

```csharp
var (items, totalCount, totals) = await registrationsRepo.GetAdminPagedAsync(
    campEditionId, page, pageSize, search, status,
    accommodationPreferences, extraIds,
    attendancePeriods, ageCategories, ct);
```

---

### Step 5: Update `RegistrationsService.ExportToCsvAsync`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**Method signature — before:**
```csharp
public async Task<(byte[] Content, string FileName)> ExportToCsvAsync(
    Guid campEditionId,
    string? search,
    string? status,
    IReadOnlyList<AccommodationType>? accommodationTypes,
    IReadOnlyList<Guid>? extraIds,
    CancellationToken ct)
```

**Method signature — after:**
```csharp
public async Task<(byte[] Content, string FileName)> ExportToCsvAsync(
    Guid campEditionId,
    string? search,
    string? status,
    IReadOnlyList<AccommodationPreferenceFilter>? accommodationPreferences,
    IReadOnlyList<Guid>? extraIds,
    IReadOnlyList<AttendancePeriod>? attendancePeriods,
    IReadOnlyList<AgeCategory>? ageCategories,
    CancellationToken ct)
```

Update the `registrationsRepo.GetAllForExportAsync(...)` call:

```csharp
var registrations = await registrationsRepo.GetAllForExportAsync(
    campEditionId, search, status,
    accommodationPreferences, extraIds,
    attendancePeriods, ageCategories, ct);
```

---

### Step 6: Update `GetAdminRegistrations` Endpoint Handler

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

**Handler — before:**
```csharp
private static async Task<IResult> GetAdminRegistrations(
    Guid campEditionId,
    RegistrationsService service,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] string? status = null,
    [FromQuery] string[]? accommodationTypes = null,
    [FromQuery] Guid[]? extraIds = null,
    CancellationToken ct = default)
{
    try
    {
        var parsedAccommodationTypes = accommodationTypes?
            .Select(t => Enum.TryParse<AccommodationType>(t, true, out var parsed) ? parsed : (AccommodationType?)null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .Distinct()
            .ToList();

        var result = await service.GetAdminListAsync(
            campEditionId, page, pageSize, search, status,
            parsedAccommodationTypes?.Count > 0 ? parsedAccommodationTypes : null,
            extraIds?.Distinct().ToList(),
            ct);
        return TypedResults.Ok(ApiResponse<AdminRegistrationListResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

**Handler — after:**
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
    CancellationToken ct = default)
{
    try
    {
        var accommodationPreferences = BuildAccommodationPreferences(accommodationIds, accommodationPreferenceOrders);

        var parsedAttendancePeriods = attendancePeriods?
            .Select(p => Enum.TryParse<AttendancePeriod>(p, true, out var parsed) ? parsed : (AttendancePeriod?)null)
            .Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();

        var parsedAgeCategories = ageCategories?
            .Select(c => Enum.TryParse<AgeCategory>(c, true, out var parsed) ? parsed : (AgeCategory?)null)
            .Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();

        var result = await service.GetAdminListAsync(
            campEditionId, page, pageSize, search, status,
            accommodationPreferences,
            extraIds?.Distinct().ToList(),
            parsedAttendancePeriods?.Count > 0 ? parsedAttendancePeriods : null,
            parsedAgeCategories?.Count > 0 ? parsedAgeCategories : null,
            ct);

        return TypedResults.Ok(ApiResponse<AdminRegistrationListResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

---

### Step 7: Update `ExportRegistrationsToCsv` Endpoint Handler

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

Apply the same query parameter changes as Step 6:

```csharp
private static async Task<IResult> ExportRegistrationsToCsv(
    Guid campEditionId,
    RegistrationsService service,
    [FromQuery] string? search = null,
    [FromQuery] string? status = null,
    [FromQuery] Guid[]? accommodationIds = null,
    [FromQuery] int[]? accommodationPreferenceOrders = null,
    [FromQuery] Guid[]? extraIds = null,
    [FromQuery] string[]? attendancePeriods = null,
    [FromQuery] string[]? ageCategories = null,
    CancellationToken ct = default)
{
    try
    {
        var accommodationPreferences = BuildAccommodationPreferences(accommodationIds, accommodationPreferenceOrders);

        var parsedAttendancePeriods = attendancePeriods?
            .Select(p => Enum.TryParse<AttendancePeriod>(p, true, out var parsed) ? parsed : (AttendancePeriod?)null)
            .Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();

        var parsedAgeCategories = ageCategories?
            .Select(c => Enum.TryParse<AgeCategory>(c, true, out var parsed) ? parsed : (AgeCategory?)null)
            .Where(c => c.HasValue).Select(c => c!.Value).Distinct().ToList();

        var (content, fileName) = await service.ExportToCsvAsync(
            campEditionId, search, status,
            accommodationPreferences,
            extraIds?.Distinct().ToList(),
            parsedAttendancePeriods?.Count > 0 ? parsedAttendancePeriods : null,
            parsedAgeCategories?.Count > 0 ? parsedAgeCategories : null,
            ct);

        return Results.File(content, contentType: "text/csv; charset=utf-8", fileDownloadName: fileName);
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

Also add the private helper method to `RegistrationsEndpoints` (static class) to avoid duplication between the two handlers:

```csharp
private static List<AccommodationPreferenceFilter>? BuildAccommodationPreferences(
    Guid[]? accommodationIds,
    int[]? preferenceOrders)
{
    if (accommodationIds is not { Length: > 0 }) return null;
    if (preferenceOrders?.Length != accommodationIds.Length) return null;

    return accommodationIds
        .Zip(preferenceOrders, (id, order) => new AccommodationPreferenceFilter(id, order))
        .ToList();
}
```

> **Binding note**: `[FromQuery] Guid[]? accommodationIds` and `[FromQuery] int[]? accommodationPreferenceOrders` are bound as repeated query string values: `?accommodationIds=<guid1>&accommodationIds=<guid2>&accommodationPreferenceOrders=1&accommodationPreferenceOrders=2`. The frontend must send them in matching order.

> **Validation**: If the arrays have mismatched lengths, `BuildAccommodationPreferences` returns `null` (no filter applied), preventing a runtime index error. This is a safe degradation.

---

### Step 8: Write Unit Tests

**File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`

Add a new test region `// ── GetAdminListAsync ───────────────────────────────────────────────────────` following the existing AAA + NSubstitute pattern.

```csharp
[Fact]
public async Task GetAdminListAsync_WithAccommodationPreferences_PassesFilterToRepository()
{
    // Arrange
    var editionId = Guid.NewGuid();
    var accomId = Guid.NewGuid();
    var filter = new List<AccommodationPreferenceFilter>
    {
        new(accomId, 1)
    };
    _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
        .Returns(CreateTestEdition(editionId));
    _repo.GetAdminPagedAsync(
            editionId, 1, 20, null, null,
            Arg.Is<IReadOnlyList<AccommodationPreferenceFilter>>(f =>
                f.Count == 1 && f[0].AccommodationId == accomId && f[0].PreferenceOrder == 1),
            null, null, null, Arg.Any<CancellationToken>())
        .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

    // Act
    await _sut.GetAdminListAsync(editionId, 1, 20, null, null, filter, null, null, null, CancellationToken.None);

    // Assert
    await _repo.Received(1).GetAdminPagedAsync(
        editionId, 1, 20, null, null,
        Arg.Is<IReadOnlyList<AccommodationPreferenceFilter>>(f =>
            f.Count == 1 && f[0].AccommodationId == accomId && f[0].PreferenceOrder == 1),
        null, null, null, Arg.Any<CancellationToken>());
}

[Fact]
public async Task GetAdminListAsync_WithAttendancePeriods_PassesFilterToRepository()
{
    // Arrange
    var editionId = Guid.NewGuid();
    var periods = new List<AttendancePeriod> { AttendancePeriod.FirstWeek };
    _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
        .Returns(CreateTestEdition(editionId));
    _repo.GetAdminPagedAsync(
            editionId, 1, 20, null, null, null, null,
            Arg.Is<IReadOnlyList<AttendancePeriod>>(p => p.Count == 1 && p[0] == AttendancePeriod.FirstWeek),
            null, Arg.Any<CancellationToken>())
        .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

    // Act
    await _sut.GetAdminListAsync(editionId, 1, 20, null, null, null, null, periods, null, CancellationToken.None);

    // Assert
    await _repo.Received(1).GetAdminPagedAsync(
        editionId, 1, 20, null, null, null, null,
        Arg.Is<IReadOnlyList<AttendancePeriod>>(p => p.Count == 1 && p[0] == AttendancePeriod.FirstWeek),
        null, Arg.Any<CancellationToken>());
}

[Fact]
public async Task GetAdminListAsync_WithAgeCategories_PassesFilterToRepository()
{
    // Arrange
    var editionId = Guid.NewGuid();
    var categories = new List<AgeCategory> { AgeCategory.Baby };
    _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
        .Returns(CreateTestEdition(editionId));
    _repo.GetAdminPagedAsync(
            editionId, 1, 20, null, null, null, null, null,
            Arg.Is<IReadOnlyList<AgeCategory>>(c => c.Count == 1 && c[0] == AgeCategory.Baby),
            Arg.Any<CancellationToken>())
        .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

    // Act
    await _sut.GetAdminListAsync(editionId, 1, 20, null, null, null, null, null, categories, CancellationToken.None);

    // Assert
    await _repo.Received(1).GetAdminPagedAsync(
        editionId, 1, 20, null, null, null, null, null,
        Arg.Is<IReadOnlyList<AgeCategory>>(c => c.Count == 1 && c[0] == AgeCategory.Baby),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task GetAdminListAsync_WithNullFilters_PassesNullsToRepository()
{
    // Arrange
    var editionId = Guid.NewGuid();
    _editionsRepo.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
        .Returns(CreateTestEdition(editionId));
    _repo.GetAdminPagedAsync(
            editionId, 1, 20, null, null, null, null, null, null, Arg.Any<CancellationToken>())
        .Returns((new List<AdminRegistrationProjection>(), 0, new AdminRegistrationTotals(0, 0, 0, 0, 0)));

    // Act
    await _sut.GetAdminListAsync(editionId, 1, 20, null, null, null, null, null, null, CancellationToken.None);

    // Assert
    await _repo.Received(1).GetAdminPagedAsync(
        editionId, 1, 20, null, null, null, null, null, null, Arg.Any<CancellationToken>());
}
```

> **Note**: The `CreateTestEdition` helper likely already exists in `RegistrationsServiceTests`. If not, add a private factory method returning a minimal valid `CampEdition`.

---

### Step 9: Update Technical Documentation

**File**: `ai-specs/specs/api-spec.yml` (if it exists and documents query parameters for the admin list endpoint)

Update the `GET /api/camp-editions/{campEditionId}/registrations` and `GET /api/camp-editions/{campEditionId}/registrations/export/csv` entries:

- Remove: `accommodationTypes[]` (string array of `AccommodationType` enum values)
- Add:
  - `accommodationIds[]` (array of UUIDs — `CampEditionAccommodation` IDs)
  - `accommodationPreferenceOrders[]` (array of integers 1–3, parallel to `accommodationIds`)
  - `attendancePeriods[]` (array of `AttendancePeriod` string values: Complete, FirstWeek, SecondWeek, WeekendVisit)
  - `ageCategories[]` (array of `AgeCategory` string values: Baby, Child, Adult)

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `AccommodationPreferenceFilter` record to `RegistrationsModels.cs`
3. Step 2 — Update `IRegistrationsRepository` interface signatures
4. Step 3 — Update `RegistrationsRepository` implementation (filter logic)
5. Step 4 — Update `RegistrationsService.GetAdminListAsync`
6. Step 5 — Update `RegistrationsService.ExportToCsvAsync`
7. Step 6 — Update `GetAdminRegistrations` endpoint handler + `BuildAccommodationPreferences` helper
8. Step 7 — Update `ExportRegistrationsToCsv` endpoint handler
9. Step 8 — Write unit tests
10. Step 9 — Update API documentation

---

## Testing Checklist

- [ ] `GetAdminListAsync` with `AccommodationPreferenceFilter` list → passes list to repository
- [ ] `GetAdminListAsync` with `AttendancePeriod` list → passes to repository
- [ ] `GetAdminListAsync` with `AgeCategory` list → passes to repository
- [ ] `GetAdminListAsync` with all nulls → still calls repository with nulls (no crash)
- [ ] Build passes with no compiler warnings (nullable reference types enabled)
- [ ] `dotnet build` in `src/Abuvi.API` succeeds
- [ ] `dotnet test` in `src/Abuvi.Tests` succeeds

---

## Error Response Format

This feature adds no new error cases. Existing error handling is preserved:
- `404 Not Found` — camp edition not found
- `401/403` — unauthorized (handled by `RequireAuthorization` policy)

---

## Dependencies

No new NuGet packages required. No EF Core migration needed.

---

## Notes

- **No migration**: `AttendancePeriod` and `AgeCategory` are already on `RegistrationMember`; `PreferenceOrder` already on `RegistrationAccommodationPreference`. Zero schema changes.
- **AND vs OR for accommodation filter**: The accommodation preference filter is AND-combined across pairs (each pair adds a separate `.Where()` clause). This is intentional — it lets the admin find families who put Albergue as 1st preference AND Autocaravana as 2nd. The period and age category filters are OR-combined across selected values within a single `.Where()`.
- **Closure capture bug**: The `foreach` loop in the repository must capture `f.AccommodationId` and `f.PreferenceOrder` into local variables before using them in a LINQ lambda. Failure to do so causes all clauses to use the last loop value (classic C# closure bug).
- **Parallel array contract**: The frontend sends `accommodationIds[]` and `accommodationPreferenceOrders[]` as two parallel query string arrays. If they have mismatched lengths `BuildAccommodationPreferences` returns `null` (safe degradation, no filter applied).
- **`string[]` for enum params**: `attendancePeriods` and `ageCategories` are bound as `string[]` and parsed via `Enum.TryParse`, consistent with the existing `accommodationTypes` parsing pattern removed in this ticket.
- **All code in English** per `base-standards.mdc`. Spanish only in user-facing error messages (none added here).

---

## Next Steps After Implementation

- Frontend implementation: update `AdminRegistrationFilters` type, composable serialization, and `RegistrationsAdminPanel.vue` (see `feat-registration-admin-filters_enriched.md`).

---

## Implementation Verification

- [ ] **Code Quality**: Nullable reference types enabled, no `!` suppressions without justification
- [ ] **Functionality**: Both list and CSV export endpoints accept and forward all four new filter parameters
- [ ] **AND logic**: Repository applies one `EXISTS` clause per accommodation preference pair (verifiable via EF Core logging)
- [ ] **Testing**: New tests pass; existing tests unchanged (no regression)
- [ ] **No migration**: `dotnet ef migrations list` shows no pending migrations
- [ ] **Documentation**: API spec updated

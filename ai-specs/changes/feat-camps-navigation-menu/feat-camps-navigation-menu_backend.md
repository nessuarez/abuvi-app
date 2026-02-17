# Backend Implementation Plan: feat-camps-navigation-menu – Camps Navigation & Current Edition

## Overview

This plan covers the backend work for the `feat-camps-navigation-menu` feature. Two new API endpoints are needed:

1. **`GET /api/camps/current`** – Returns the "best available" camp edition for all authenticated users. The front-end uses this to power the dynamic `/camp` page.
2. **`GET /api/family-units`** – Returns a paginated, searchable list of all family units for Board/Admin users. Powers the new admin panel's "Unidades Familiares" tab.

> **Architecture principle**: Vertical Slice Architecture – all new code stays inside its feature slice (`Features/Camps` or `Features/FamilyUnits`). No cross-feature dependencies added.

> **TDD is mandatory**. Tests come before implementation. Each service method must have failing tests before a single line of production code is written.

---

## Architecture Context

### Feature Slices Involved

| Slice | Files to modify | Files to create |
|---|---|---|
| `Features/Camps` | `ICampEditionsRepository.cs`, `CampEditionsRepository.cs`, `CampEditionsService.cs`, `CampsEndpoints.cs`, `CampsModels.cs` | — |
| `Features/FamilyUnits` | `FamilyUnitsRepository.cs` (interface + impl), `FamilyUnitsService.cs`, `FamilyUnitsEndpoints.cs`, `FamilyUnitsModels.cs` | — |
| Tests | `CampEditionsServiceTests.cs` | `CampEditionsServiceTests_GetCurrent.cs`, `FamilyUnitsServiceTests_GetAll.cs` |

### No schema changes needed (no migration required)
The existing `CampEditions` and `FamilyUnits` tables already have all the columns needed.

### Cross-cutting concerns
- Authorization: `RequireRole("Admin", "Board", "Member")` for `/api/camps/current`; `RequireRole("Admin", "Board")` for `GET /api/family-units`
- Response envelope: existing `ApiResponse<T>` wrapper used throughout

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a feature branch dedicated to backend work
- **Implementation Steps**:
  1. Ensure you are on `main` and up to date:
     ```bash
     git checkout main && git pull origin main
     ```
  2. Create the backend branch:
     ```bash
     git checkout -b feature/feat-camps-navigation-menu-backend
     ```
  3. Verify: `git branch`
- **Notes**: This branch is separate from the frontend branch to keep concerns isolated.

---

### Step 1: [TDD Red] Write failing tests – `GetCurrentAsync` (CampEditionsService)

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests_GetCurrent.cs` *(NEW)*
- **Action**: Write failing xUnit tests for the new `GetCurrentAsync()` service method **before** implementing it.
- **Test class setup** (mirrors existing `CampEditionsServiceTests`):

```csharp
public class CampEditionsServiceTests_GetCurrent
{
    private readonly ICampEditionsRepository _repository;
    private readonly ICampsRepository _campsRepository;
    private readonly CampEditionsService _sut;

    public CampEditionsServiceTests_GetCurrent()
    {
        _repository = Substitute.For<ICampEditionsRepository>();
        _campsRepository = Substitute.For<ICampsRepository>();
        _sut = new CampEditionsService(_repository, _campsRepository);
    }
}
```

- **Test cases to write** (all must fail at this point — method doesn't exist yet):

  1. **Returns current year's Open edition** – repository returns an `Open` edition for current year; service returns it.
  2. **Returns current year's Closed edition when no Open exists** – repository returns `null` for Open, then returns a `Closed` edition for current year.
  3. **Returns previous year's Completed edition when current year has none** – repository returns `null` for current year, then returns a `Completed` edition from `currentYear - 1`.
  4. **Returns previous year's Closed edition when no Completed is available** – repository returns `null` for current year, `null` for Completed previous year, then returns `Closed` from previous year.
  5. **Returns null when no editions exist within lookback window** – all queries return `null`; service returns `null`.
  6. **Computes AvailableSpots correctly when MaxCapacity is set** – `AvailableSpots = MaxCapacity - RegistrationCount`; since registrations are not yet built, `RegistrationCount = 0`, so `AvailableSpots = MaxCapacity`.
  7. **AvailableSpots is null when MaxCapacity is null** – `MaxCapacity = null` → `AvailableSpots = null`.
  8. **Includes camp coordinates in response** – response contains `CampLatitude` and `CampLongitude` from the camp's navigation property.

- **Helper**: Create a private `BuildEdition(Guid campId, int year, CampEditionStatus status)` factory to reduce boilerplate across tests.

---

### Step 2: [TDD Red] Write failing tests – `GetAllFamilyUnitsAsync` (FamilyUnitsService)

- **File**: `src/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsServiceTests_GetAll.cs` *(NEW)*
- **Action**: Write failing xUnit tests for the new paginated admin list method **before** implementing it.
- **Test class setup**:

```csharp
public class FamilyUnitsServiceTests_GetAll
{
    private readonly IFamilyUnitsRepository _repository;
    private readonly FamilyUnitsService _sut;

    public FamilyUnitsServiceTests_GetAll()
    {
        _repository = Substitute.For<IFamilyUnitsRepository>();
        _sut = new FamilyUnitsService(_repository, ...); // match existing constructor
    }
}
```

- **Test cases to write**:

  1. **Returns paged list with correct metadata** – 25 family units in DB, pageSize=20, page=1 → 20 items, totalCount=25, totalPages=2.
  2. **Returns second page correctly** – page=2, pageSize=20 → 5 items.
  3. **Filters by search term on family name** – search="Garcia" → only family units with name containing "Garcia".
  4. **Filters by search term on representative name** – search="Juan" → family units whose representative's name contains "Juan".
  5. **Sorts by name ascending** – `sortBy=name, sortOrder=asc`.
  6. **Sorts by createdAt descending** – `sortBy=createdAt, sortOrder=desc`.
  7. **Returns empty list when no family units exist** – empty DB → items=[], totalCount=0, totalPages=0.
  8. **Defaults to page 1, pageSize 20 when not specified** – no pagination params → page=1, pageSize=20.

---

### Step 3: [TDD Green] Add repository support for current camp edition

- **File**: `src/Abuvi.API/Features/Camps/ICampEditionsRepository.cs`
- **Action**: Add new method to interface

```csharp
/// <summary>
/// Returns the best available camp edition for the current year (Open preferred, then Closed),
/// falling back to the most recent Completed or Closed edition from the previous year.
/// Returns null if no qualifying edition exists within the lookback window.
/// </summary>
Task<CampEdition?> GetCurrentAsync(int currentYear, CancellationToken cancellationToken = default);
```

---

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsRepository.cs`
- **Action**: Implement `GetCurrentAsync` using EF Core with proper ordering and status priority

**Implementation logic**:
```csharp
public async Task<CampEdition?> GetCurrentAsync(int currentYear, CancellationToken cancellationToken = default)
{
    // 1. Current year – Open first
    var currentYearEdition = await _context.CampEditions
        .AsNoTracking()
        .Include(e => e.Camp)
        .Where(e => e.Year == currentYear && !e.IsArchived
            && (e.Status == CampEditionStatus.Open || e.Status == CampEditionStatus.Closed))
        .OrderByDescending(e => e.Status == CampEditionStatus.Open ? 1 : 0) // Open wins
        .FirstOrDefaultAsync(cancellationToken);

    if (currentYearEdition != null)
        return currentYearEdition;

    // 2. Previous year fallback (only 1 year back per spec Q4 / archive threshold)
    var previousYear = currentYear - 1;
    return await _context.CampEditions
        .AsNoTracking()
        .Include(e => e.Camp)
        .Where(e => e.Year == previousYear && !e.IsArchived
            && (e.Status == CampEditionStatus.Completed || e.Status == CampEditionStatus.Closed))
        .OrderByDescending(e => e.Status == CampEditionStatus.Completed ? 1 : 0) // Completed wins
        .FirstOrDefaultAsync(cancellationToken);
}
```

**Notes**:
- Uses two separate DB queries to keep the logic explicit and easy to maintain.
- Looks back only 1 year (matches spec clarification Q4: "only current year and previous year").
- Archived editions are always excluded.
- `Include(e => e.Camp)` is required to populate camp coordinates in the response.

---

### Step 4: [TDD Green] Add `CurrentCampEditionResponse` DTO + `GetCurrentAsync` service method

#### 4a – Add `CurrentCampEditionResponse` to CampsModels.cs

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add new response record (below existing `ActiveCampEditionResponse`)

```csharp
/// <summary>
/// Response for the current (best-available) camp edition endpoint.
/// Includes camp coordinates and computed availability fields.
/// RegistrationCount is always 0 until the Registrations feature is implemented.
/// </summary>
public record CurrentCampEditionResponse(
    Guid Id,
    Guid CampId,
    string CampName,
    string? CampLocation,
    string? CampFormattedAddress,
    double? CampLatitude,
    double? CampLongitude,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    CampEditionStatus Status,
    int? MaxCapacity,
    int RegistrationCount,
    int? AvailableSpots,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 4b – Add `GetCurrentAsync` to CampEditionsService

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: Add new service method

```csharp
/// <summary>
/// Returns the best available camp edition using status-priority and year-fallback logic.
/// Returns null if no qualifying edition exists within the 1-year lookback window.
/// </summary>
public async Task<CurrentCampEditionResponse?> GetCurrentAsync(
    CancellationToken cancellationToken = default)
{
    var currentYear = DateTime.UtcNow.Year;
    var edition = await _repository.GetCurrentAsync(currentYear, cancellationToken);

    if (edition == null)
        return null;

    var registrationCount = 0; // Placeholder until Registrations feature is built
    var availableSpots = edition.MaxCapacity.HasValue
        ? edition.MaxCapacity.Value - registrationCount
        : (int?)null;

    return new CurrentCampEditionResponse(
        Id: edition.Id,
        CampId: edition.CampId,
        CampName: edition.Camp.Name,
        CampLocation: edition.Camp.Location,
        CampFormattedAddress: edition.Camp.FormattedAddress,
        CampLatitude: edition.Camp.Latitude,
        CampLongitude: edition.Camp.Longitude,
        Year: edition.Year,
        StartDate: edition.StartDate,
        EndDate: edition.EndDate,
        PricePerAdult: edition.PricePerAdult,
        PricePerChild: edition.PricePerChild,
        PricePerBaby: edition.PricePerBaby,
        UseCustomAgeRanges: edition.UseCustomAgeRanges,
        CustomBabyMaxAge: edition.CustomBabyMaxAge,
        CustomChildMinAge: edition.CustomChildMinAge,
        CustomChildMaxAge: edition.CustomChildMaxAge,
        CustomAdultMinAge: edition.CustomAdultMinAge,
        Status: edition.Status,
        MaxCapacity: edition.MaxCapacity,
        RegistrationCount: registrationCount,
        AvailableSpots: availableSpots,
        Notes: edition.Notes,
        CreatedAt: edition.CreatedAt,
        UpdatedAt: edition.UpdatedAt
    );
}
```

**Run tests**: The Step 1 tests should now pass (green).

---

### Step 5: Add `GET /api/camps/current` endpoint

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`
- **Action**: Register the new endpoint in a **separate member-level group** (the existing `/api/camps` group is Board/Admin only)

**In `MapCampsEndpoints()`**, add a new group after the existing `editionsMemberGroup`:

```csharp
// GET /api/camps/current – accessible to all authenticated users
var campCurrentGroup = app.MapGroup("/api/camps")
    .WithTags("Camp Editions")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"));

// NOTE: Must be registered before /{id:guid} patterns in other groups.
// Since "current" is not a GUID, the route constraint on the existing /{id:guid}
// ensures no conflict, but explicit ordering is still good practice.
campCurrentGroup.MapGet("/current", GetCurrentCampEdition)
    .WithName("GetCurrentCampEdition")
    .WithSummary("Get the current (best-available) camp edition for the authenticated user")
    .Produces<ApiResponse<CurrentCampEditionResponse>>()
    .Produces(404)
    .Produces(401);
```

**Add the handler** (private static method in `CampsEndpoints`):

```csharp
/// <summary>
/// Gets the current best-available camp edition.
/// Returns 404 if no qualifying edition exists within the 1-year lookback window.
/// </summary>
private static async Task<IResult> GetCurrentCampEdition(
    [FromServices] CampEditionsService service,
    CancellationToken cancellationToken = default)
{
    var edition = await service.GetCurrentAsync(cancellationToken);

    if (edition == null)
        return Results.NotFound(
            ApiResponse<CurrentCampEditionResponse>.NotFound(
                "No hay ninguna edición de campamento disponible"));

    return Results.Ok(ApiResponse<CurrentCampEditionResponse>.Ok(edition));
}
```

**Authorization**: All authenticated users (Admin, Board, Member) can access this endpoint.

**No `Program.cs` changes needed** – `CampEditionsService` is already registered.

---

### Step 6: [TDD Green] Add repository support for paginated family units list

#### 6a – Extend `IFamilyUnitsRepository` interface

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`
- **Action**: Add to `IFamilyUnitsRepository` interface:

```csharp
/// <summary>
/// Returns a paginated list of all family units with representative name and member count.
/// Supports text search on family name or representative full name.
/// </summary>
Task<(List<FamilyUnitAdminProjection> Items, int TotalCount)> GetAllPagedAsync(
    int page,
    int pageSize,
    string? search,
    string? sortBy,
    string? sortOrder,
    CancellationToken ct);
```

Add the projection record (in `FamilyUnitsModels.cs` - see Step 7 for details):
```csharp
public record FamilyUnitAdminProjection(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    string RepresentativeName,
    int MembersCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 6b – Implement `GetAllPagedAsync` in `FamilyUnitsRepository`

```csharp
public async Task<(List<FamilyUnitAdminProjection> Items, int TotalCount)> GetAllPagedAsync(
    int page, int pageSize, string? search, string? sortBy, string? sortOrder, CancellationToken ct)
{
    var query = from fu in db.FamilyUnits
                join user in db.Users on fu.RepresentativeUserId equals user.Id into userGroup
                from u in userGroup.DefaultIfEmpty()
                select new
                {
                    fu.Id,
                    fu.Name,
                    fu.RepresentativeUserId,
                    RepresentativeName = u != null
                        ? u.FirstName + " " + u.LastName
                        : string.Empty,
                    MembersCount = db.FamilyMembers.Count(m => m.FamilyUnitId == fu.Id),
                    fu.CreatedAt,
                    fu.UpdatedAt
                };

    // Apply search filter
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(x =>
            x.Name.ToLower().Contains(term) ||
            x.RepresentativeName.ToLower().Contains(term));
    }

    var totalCount = await query.CountAsync(ct);

    // Apply sorting
    query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
    {
        ("createdat", "desc") => query.OrderByDescending(x => x.CreatedAt),
        ("createdat", _)      => query.OrderBy(x => x.CreatedAt),
        ("name", "desc")      => query.OrderByDescending(x => x.Name),
        _                     => query.OrderBy(x => x.Name), // default: name asc
    };

    // Apply pagination
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

    var projections = items.Select(x => new FamilyUnitAdminProjection(
        x.Id, x.Name, x.RepresentativeUserId,
        x.RepresentativeName, x.MembersCount, x.CreatedAt, x.UpdatedAt
    )).ToList();

    return (projections, totalCount);
}
```

**Note**: `db.FamilyMembers.Count(m => m.FamilyUnitId == fu.Id)` is a correlated subquery that EF Core translates to a SQL `COUNT` — acceptable for admin panel use cases where response time expectations are relaxed.

---

### Step 7: [TDD Green] Add DTOs and `GetAllFamilyUnitsAsync` service method

#### 7a – Add new DTOs to FamilyUnitsModels.cs

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`
- **Action**: Add these records at the end of the file:

```csharp
/// <summary>
/// Projection for admin list queries – joins FamilyUnit with User and counts members.
/// Not a full entity; only returned from the repository's paged query.
/// </summary>
public record FamilyUnitAdminProjection(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    string RepresentativeName,
    int MembersCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Response item for the admin family units list endpoint.
/// </summary>
public record FamilyUnitListItemResponse(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    string RepresentativeName,
    int MembersCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Paginated response envelope for the admin family units list.
/// </summary>
public record PagedFamilyUnitsResponse(
    List<FamilyUnitListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
```

#### 7b – Add `GetAllFamilyUnitsAsync` to FamilyUnitsService

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`
- **Action**: Add new method (Board/Admin use only)

```csharp
/// <summary>
/// Returns a paginated list of all family units for admin/board use.
/// </summary>
public async Task<PagedFamilyUnitsResponse> GetAllFamilyUnitsAsync(
    int page,
    int pageSize,
    string? search,
    string? sortBy,
    string? sortOrder,
    CancellationToken ct)
{
    // Clamp pagination values to safe defaults
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var (items, totalCount) = await _repository.GetAllPagedAsync(
        page, pageSize, search, sortBy, sortOrder, ct);

    var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

    return new PagedFamilyUnitsResponse(
        Items: items.Select(p => new FamilyUnitListItemResponse(
            p.Id, p.Name, p.RepresentativeUserId,
            p.RepresentativeName, p.MembersCount, p.CreatedAt, p.UpdatedAt
        )).ToList(),
        TotalCount: totalCount,
        Page: page,
        PageSize: pageSize,
        TotalPages: totalPages
    );
}
```

**Run tests**: The Step 2 tests should now pass (green).

---

### Step 8: Add `GET /api/family-units` admin list endpoint

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`
- **Action**: Add a **separate Board/Admin group** and register the new endpoint. The existing `group` uses `.RequireAuthorization()` (any user) and must not be changed.

**In `MapFamilyUnitsEndpoints()`**, add after the existing `group` definition:

```csharp
// Board/Admin only group for administrative operations
var adminGroup = app.MapGroup("/api/family-units")
    .WithTags("Family Units")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

// GET /api/family-units – paginated list for admin panel
adminGroup.MapGet("/", GetAllFamilyUnits)
    .WithName("GetAllFamilyUnits")
    .WithSummary("Get paginated list of all family units (Admin/Board only)")
    .Produces<ApiResponse<PagedFamilyUnitsResponse>>()
    .Produces(401)
    .Produces(403);
```

**Add the handler**:

```csharp
private static async Task<IResult> GetAllFamilyUnits(
    FamilyUnitsService service,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] string? sortBy = null,
    [FromQuery] string? sortOrder = null,
    CancellationToken ct = default)
{
    var result = await service.GetAllFamilyUnitsAsync(page, pageSize, search, sortBy, sortOrder, ct);
    return TypedResults.Ok(ApiResponse<PagedFamilyUnitsResponse>.Ok(result));
}
```

**No `Program.cs` changes needed** – `FamilyUnitsService` is already registered.

---

### Step 9: Update Technical Documentation

- **Action**: Update API documentation to reflect the two new endpoints
- **Implementation Steps**:

  1. **`ai-specs/specs/api-endpoints.md`** – Add entries for:
     - `GET /api/camps/current` (Member+): returns `CurrentCampEditionResponse`, 404 if none available
     - `GET /api/family-units` (Admin/Board): returns `PagedFamilyUnitsResponse` with search/sort/pagination query params

  2. **`ai-specs/specs/data-model.md`** – No entity changes needed (no new DB columns). No update required.

  3. **OpenAPI (Swagger)** – The `.WithOpenApi()` on the endpoint group automatically generates OpenAPI documentation. No additional changes needed.

---

## Implementation Order

| # | Step | File(s) | TDD Phase |
|---|---|---|---|
| 0 | Create feature branch | git | — |
| 1 | Write failing tests for `GetCurrentAsync` | `CampEditionsServiceTests_GetCurrent.cs` | 🔴 Red |
| 2 | Write failing tests for `GetAllFamilyUnitsAsync` | `FamilyUnitsServiceTests_GetAll.cs` | 🔴 Red |
| 3 | Add repository interface + impl for current edition | `ICampEditionsRepository.cs`, `CampEditionsRepository.cs` | 🟢 Green |
| 4 | Add `CurrentCampEditionResponse` DTO + `GetCurrentAsync` service method | `CampsModels.cs`, `CampEditionsService.cs` | 🟢 Green |
| 5 | Add `GET /api/camps/current` endpoint | `CampsEndpoints.cs` | 🟢 Green |
| 6 | Add repository interface + impl for paged family units | `FamilyUnitsRepository.cs` (interface + impl) | 🟢 Green |
| 7 | Add DTOs + `GetAllFamilyUnitsAsync` service method | `FamilyUnitsModels.cs`, `FamilyUnitsService.cs` | 🟢 Green |
| 8 | Add `GET /api/family-units` admin endpoint | `FamilyUnitsEndpoints.cs` | 🟢 Green |
| 9 | Update API documentation | `ai-specs/specs/api-endpoints.md` | — |

---

## Testing Checklist

### `GET /api/camps/current` – Unit Tests (Step 1)

| Test | Scenario | Expected |
|---|---|---|
| ✅ 1 | Current year has Open edition | Returns it |
| ✅ 2 | Current year has only Closed edition | Returns it |
| ✅ 3 | Current year has nothing; previous year has Completed | Returns previous year's Completed |
| ✅ 4 | Current year has nothing; previous year has only Closed | Returns previous year's Closed |
| ✅ 5 | No editions in lookback window | Returns null |
| ✅ 6 | MaxCapacity=100, RegistrationCount=0 | AvailableSpots=100 |
| ✅ 7 | MaxCapacity=null | AvailableSpots=null |
| ✅ 8 | Camp has coordinates | CampLatitude/CampLongitude populated |

### `GET /api/family-units` (admin) – Unit Tests (Step 2)

| Test | Scenario | Expected |
|---|---|---|
| ✅ 1 | 25 items, page=1, pageSize=20 | 20 items, totalPages=2 |
| ✅ 2 | 25 items, page=2, pageSize=20 | 5 items |
| ✅ 3 | search="Garcia" | Only matching family names |
| ✅ 4 | search="Juan" | Only families with representative named Juan |
| ✅ 5 | sortBy=name, sortOrder=asc | Alphabetical |
| ✅ 6 | sortBy=createdAt, sortOrder=desc | Newest first |
| ✅ 7 | Empty database | Items=[], totalCount=0, totalPages=0 |
| ✅ 8 | No params | Defaults to page=1, pageSize=20 |

---

## Error Response Format

Follows the standard `ApiResponse<T>` envelope:

```json
// 200 OK – current edition found
{
  "success": true,
  "data": { /* CurrentCampEditionResponse fields */ },
  "error": null
}

// 404 Not Found – no edition in lookback window
{
  "success": false,
  "data": null,
  "error": {
    "message": "No hay ninguna edición de campamento disponible",
    "code": "NOT_FOUND"
  }
}

// 403 Forbidden – regular user accessing GET /api/family-units
{
  "success": false,
  "data": null,
  "error": {
    "message": "Forbidden",
    "code": "FORBIDDEN"
  }
}
```

HTTP Status Code Mapping:

| Status | Scenario |
|---|---|
| 200 | Edition found / list returned |
| 401 | Not authenticated |
| 403 | Insufficient role (Board-only endpoint) |
| 404 | No qualifying camp edition exists |

---

## Dependencies

### NuGet Packages
No new packages needed. All dependencies (`EF Core`, `xUnit`, `NSubstitute`, `FluentAssertions`) are already installed.

### EF Core Migration
**No migration needed.** Both endpoints query existing tables with no schema changes.

---

## Notes

### Business Rules

1. **"Current" camp priority order**:
   - Priority 1: Current year + `Open` status
   - Priority 2: Current year + `Closed` status
   - Priority 3: Previous year + `Completed` status
   - Priority 4: Previous year + `Closed` status
   - Result 5: `null` (returns HTTP 404)

2. **Archive threshold**: Only looks back 1 year (current year + previous year). Editions from 2+ years ago are never returned as "current".

3. **RegistrationCount**: Always `0` until the Registrations feature is implemented. `AvailableSpots` is computed from `MaxCapacity - 0 = MaxCapacity`.

4. **Archived editions**: `IsArchived = true` editions are always excluded from `GetCurrentAsync`.

5. **Family units admin list**: `GET /api/family-units` is Board/Admin only. Regular members use `GET /api/family-units/me` for their own unit.

6. **No sensitive data in admin list**: `FamilyUnitListItemResponse` does not include member details, medical notes, or allergies. Those remain protected under the existing `/{id}/members` endpoints with representative-only access.

### Language Requirements
- All error messages in Spanish (e.g., `"No hay ninguna edición de campamento disponible"`)
- Documentation updates in English

### GDPR Considerations
- The admin list endpoint exposes representative names. This is acceptable for Board/Admin users managing the association.
- Medical notes and allergies remain encrypted and are not returned by any new endpoint.

---

## Next Steps After Implementation

1. **Frontend ticket** (`feat-camps-navigation-menu-frontend`): Wire up the new `GET /api/camps/current` in the `useCampEditions` composable and update `CampPage.vue`.
2. **Future**: When the Registrations feature is built, update `GetCurrentAsync` to query actual registration counts and compute `AvailableSpots` from real data.
3. **Optional**: If user preference for selected camp is needed (spec Q1), add `selectedCampEditionId` to the User entity and update `GetCurrentAsync` to check it first.

---

## Implementation Verification Checklist

- [ ] **Code Quality**: No compiler warnings, nullable reference types enabled, C# analyzers pass
- [ ] **Functionality**:
  - [ ] `GET /api/camps/current` returns correct edition for each priority scenario
  - [ ] `GET /api/camps/current` returns 404 when no editions exist
  - [ ] `GET /api/family-units` returns paginated list with correct metadata
  - [ ] `GET /api/family-units` is accessible only to Admin/Board (403 for Member)
  - [ ] `GET /api/family-units/me` still works (not broken by the new endpoint)
  - [ ] `GET /api/camps/editions/active` still works (not broken by the new endpoint)
- [ ] **Testing**: All new tests pass, `dotnet test` shows green, coverage ≥ 90% for new code
- [ ] **Integration**: No EF Core migration errors, existing endpoints not broken
- [ ] **Documentation**: `ai-specs/specs/api-endpoints.md` updated with both new endpoints

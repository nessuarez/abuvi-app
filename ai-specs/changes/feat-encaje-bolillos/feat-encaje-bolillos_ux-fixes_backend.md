# Backend Implementation Plan: feat-encaje-bolillos-ux-fixes

## Overview

Fixes and enhancements for the Encaje de Bolillos accommodation-assignment board, all within the `Camps` Vertical Slice. Four of the six issues in the enriched spec have backend work:

- **Fix 1**: Add `GET /{zoneId}` endpoint so the zone photo gallery resolves instead of 405.
- **Fix 2 / Feature 4**: Enrich `ProposalAssignmentStateResponse` with an `AccommodationTypeLookup` (all active accommodation id→type, not just assignable ones) and `AllFeatures` catalog (feature id/name/icon for every feature present in any accommodation).
- **Enhancement 6**: Add `BabyCount` to `AssignmentFamilyResponse`; currently it is merged into `ChildCount`.

Fixes 3 and 5 are frontend-only (ProgressBar visibility, zone thumbnail fallback) — no backend action.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

| File | Action |
|------|--------|
| `CampsModels.cs` | Add `AccommodationTypeLookupItem`, `AccommodationFeatureSummary` records; update `AssignmentFamilyResponse` (add `BabyCount`); update `ProposalAssignmentStateResponse` (add `AccommodationTypeLookup`, `AllFeatures`) |
| `AccommodationZonesService.cs` | Add `GetByIdAsync(campEditionId, zoneId)` method |
| `CampsEndpoints.cs` | Register `GET /{zoneId:guid}` in `zonesGroup` |
| `AccommodationAssignmentsRepository.cs` | Add feature includes; populate `AvailableFeatures`, `AccommodationTypeLookup`, `AllFeatures`, `BabyCount` |
| `AccommodationAssignmentsRepositoryTests.cs` | Update existing tests that construct `ProposalAssignmentStateResponse`; add new tests |

No new entity, migration, or validator is required.

---

## Implementation Steps

### Step 0 — Create feature branch

- **Base branch**: `dev`
- **Branch name**: `feature/feat-encaje-bolillos-ux-fixes-backend`
- Run: `git checkout dev && git pull origin dev && git checkout -b feature/feat-encaje-bolillos-ux-fixes-backend`

---

### Step 1 — Update `CampsModels.cs` DTOs

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

#### 1a — New supporting records

Add after the existing `AccommodationPreferenceItem` record:

```csharp
public record AccommodationTypeLookupItem(Guid Id, string Type);

public record AccommodationFeatureSummary(Guid Id, string Name, string Icon);
```

#### 1b — Add `BabyCount` to `AssignmentFamilyResponse`

Insert `int BabyCount` **after** `int ChildCount` and **before** `bool HasPet`:

```csharp
public record AssignmentFamilyResponse(
    Guid RegistrationId,
    Guid FamilyUnitId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    int AdultCount,
    int ChildCount,
    int BabyCount,          // ← ADD
    bool HasPet,
    string? SpecialNeeds,
    string? CampatesPreference,
    IReadOnlyList<AccommodationPreferenceItem> AccommodationPreferences,
    bool HasSpecialNeeds,
    IReadOnlyList<Guid> RequiredFeatures,
    IReadOnlyList<Guid> FriendlyFamilyUnitIds
);
```

#### 1c — Add lookup fields to `ProposalAssignmentStateResponse`

```csharp
public record ProposalAssignmentStateResponse(
    Guid ProposalId,
    IReadOnlyList<AssignmentFamilyResponse> Families,
    IReadOnlyList<AssignmentAccommodationResponse> Accommodations,
    IReadOnlyList<AssignmentEntry> Assignments,
    IReadOnlyList<AccommodationTypeLookupItem> AccommodationTypeLookup,  // ← ADD
    IReadOnlyList<AccommodationFeatureSummary> AllFeatures               // ← ADD
);
```

---

### Step 2 — Update `AccommodationAssignmentsRepository.GetAssignmentStateAsync`

**File:** `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs`

Apply four targeted changes to `GetAssignmentStateAsync`:

#### 2a — Include feature assignments in accommodation query

Add `.Include(a => a.FeatureAssignments).ThenInclude(fa => fa.Feature)` to the assignable-accommodations query (the existing query at line 49–57):

```csharp
var accommodations = await db.CampEditionAccommodations
    .AsNoTracking()
    .Where(a => a.CampEditionId == campEditionId && a.IsActive && a.IsAssignable)
    .Include(a => a.Zone)
        .ThenInclude(z => z!.MediaItems.Where(m => m.IsPrimary).Take(1))
    .Include(a => a.MediaItems.Where(m => m.IsPrimary).Take(1))
    .Include(a => a.FeatureAssignments)          // ← ADD
        .ThenInclude(fa => fa.Feature)           // ← ADD
    .OrderBy(a => a.SortOrder)
    .ThenBy(a => a.Name)
    .ToListAsync(ct);
```

#### 2b — Add type-lookup query (all active accommodations, no IsAssignable filter)

Insert after the existing `accommodations` query:

```csharp
var allActiveAccommodations = await db.CampEditionAccommodations
    .AsNoTracking()
    .Where(a => a.CampEditionId == campEditionId && a.IsActive)
    .Select(a => new AccommodationTypeLookupItem(a.Id, a.AccommodationType.ToString()))
    .ToListAsync(ct);
```

#### 2c — Pass `BabyCount` separately in the families projection

The existing code (around line 66–68) already computes:
```csharp
var adultCount = r.Members.Count(m => m.AgeCategory == AgeCategory.Adult);
var childCount = r.Members.Count(m => m.AgeCategory == AgeCategory.Child);
var babyCount = r.Members.Count(m => m.AgeCategory == AgeCategory.Baby);
```

Currently the response is built with `childCount + babyCount` for the `ChildCount` positional arg. Change to pass them separately:

```csharp
// Before (original line 82 area):
r.Members.Count,
adultCount,
childCount + babyCount,   // ← merged
r.HasPet,

// After:
r.Members.Count,
adultCount,
childCount,               // ← children only
babyCount,                // ← ADD BabyCount
r.HasPet,
```

#### 2d — Populate `AvailableFeatures` in `accommodationResponses` and build `AllFeatures`

In the `SelectMany` projection (around line 96), replace the empty `[]` for `AvailableFeatures`:

```csharp
var accommodationResponses = accommodations
    .SelectMany(a =>
    {
        var primaryMedia = a.MediaItems.FirstOrDefault(m => m.IsPrimary);
        var zonePrimaryMedia = a.Zone?.MediaItems.FirstOrDefault(m => m.IsPrimary);
        var featureIds = a.FeatureAssignments.Select(fa => fa.FeatureId).ToList();   // ← ADD
        return Enumerable.Range(0, a.Quantity).Select(unitIndex =>
            new AssignmentAccommodationResponse(
                a.Id,
                a.Quantity > 1 ? $"{a.Name} #{unitIndex + 1}" : a.Name,
                a.AccommodationType,
                a.Capacity,
                a.CountByFamily,
                a.ZoneId,
                a.Zone?.Name,
                a.SortOrder,
                featureIds,                             // ← was []
                a.Quantity,
                a.Quantity > 1 ? unitIndex : (int?)null,
                primaryMedia?.ThumbnailUrl,
                primaryMedia?.FileUrl,
                zonePrimaryMedia?.ThumbnailUrl,
                zonePrimaryMedia?.FileUrl
            ));
    })
    .OrderBy(s => s.SortOrder)
    .ThenBy(s => s.Name)
    .ToList();
```

Build `AllFeatures` from the loaded feature nav properties (deduplicated):

```csharp
var allFeatures = accommodations
    .SelectMany(a => a.FeatureAssignments.Select(fa => fa.Feature))
    .DistinctBy(f => f.Id)
    .Select(f => new AccommodationFeatureSummary(f.Id, f.Name, f.Icon))
    .ToList();
```

#### 2e — Update the return statement

```csharp
// Before:
return new ProposalAssignmentStateResponse(proposalId, families, accommodationResponses, assignmentEntries);

// After:
return new ProposalAssignmentStateResponse(
    proposalId,
    families,
    accommodationResponses,
    assignmentEntries,
    allActiveAccommodations,
    allFeatures);
```

---

### Step 3 — Add `GetByIdAsync` to `AccommodationZonesService`

**File:** `src/Abuvi.API/Features/Camps/AccommodationZonesService.cs`

Add after `GetByEditionAsync`:

```csharp
public async Task<AccommodationZoneResponse> GetByIdAsync(
    Guid campEditionId,
    Guid zoneId,
    CancellationToken ct = default)
{
    var zone = await zonesRepository.GetByIdAsync(zoneId, ct)
        ?? throw new NotFoundException("AccommodationZone", zoneId);

    if (zone.CampEditionId != campEditionId)
        throw new NotFoundException("AccommodationZone", zoneId);

    return ToResponse(zone);
}
```

**Implementation notes:**
- `zonesRepository.GetByIdAsync` already exists in `IAccommodationZonesRepository` and its implementation includes `.Include(z => z.Accommodations)`, `.Include(z => z.FeatureAssignments).ThenInclude(fa => fa.Feature)`, and `.Include(z => z.MediaItems)` — so all data needed for a full `AccommodationZoneResponse` is already loaded.
- A zone from a different edition returns 404 (same as non-existent) to avoid information leakage.

---

### Step 4 — Register `GET /{zoneId:guid}` in `CampsEndpoints.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`

Add the route registration **after** the existing `zonesGroup.MapGet("/", GetZonesByEdition)` call:

```csharp
zonesGroup.MapGet("/{zoneId:guid}", GetZoneById)
    .WithName("GetAccommodationZoneById")
    .WithSummary("Get a single accommodation zone by ID (includes media items)")
    .Produces<ApiResponse<AccommodationZoneResponse>>()
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden);
```

Add the private handler method alongside the other zone handlers (around the `GetZonesByEdition` handler):

```csharp
private static async Task<IResult> GetZoneById(
    Guid campEditionId,
    Guid zoneId,
    [FromServices] AccommodationZonesService service,
    CancellationToken ct)
{
    try
    {
        var zone = await service.GetByIdAsync(campEditionId, zoneId, ct);
        return Results.Ok(ApiResponse<AccommodationZoneResponse>.Ok(zone));
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(ApiResponse.Fail(ex.Message));
    }
}
```

---

### Step 5 — Update existing tests that break due to record changes

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationAssignmentsRepositoryTests.cs`

The `AssignmentFamilyResponse` and `ProposalAssignmentStateResponse` record changes break any test that directly constructs these records. Update:

1. Any direct `new AssignmentFamilyResponse(...)` call: insert `BabyCount: 0` (or the appropriate value) after `ChildCount`.
2. Any direct `new ProposalAssignmentStateResponse(...)` call: add `AccommodationTypeLookup: []` and `AllFeatures: []` (or named args) after `Assignments`.

Use the repository's in-memory DB approach already established in `AccommodationAssignmentsRepositoryTests` — no behavior changes needed for existing tests, only constructor call fixes.

---

### Step 6 — Write new unit tests

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationAssignmentsRepositoryTests.cs`

Add the following test cases to the existing `AccommodationAssignmentsRepositoryTests` class (which already uses `InMemoryDatabase`):

#### 6a — `GetAssignmentStateAsync_IncludesBabyCount_Separately`

Arrange: one registration with 1 adult, 1 child, 1 baby member.
Assert: returned family has `AdultCount = 1`, `ChildCount = 1`, `BabyCount = 1`, `MemberCount = 3`.

#### 6b — `GetAssignmentStateAsync_AccommodationTypeLookupIncludesNonAssignable`

Arrange: two accommodations — one `IsAssignable = true`, one `IsAssignable = false`, both `IsActive = true`.
Assert: `AccommodationTypeLookup` contains entries for **both** accommodations; `Accommodations` list contains only the assignable one.

#### 6c — `GetAssignmentStateAsync_AllFeaturesCatalogIncludesFeatureNamesAndIcons`

Arrange: one assignable accommodation with one `AccommodationFeature` attached (name="Accessible", icon="pi pi-wheelchair").
Assert: `AllFeatures` contains one entry with matching `Id`, `Name`, `Icon`; `Accommodations[0].AvailableFeatures` contains that feature's `Id`.

#### 6d — `GetAssignmentStateAsync_AllFeaturesIsEmpty_WhenNoFeaturesAssigned`

Arrange: one accommodation with no feature assignments.
Assert: `AllFeatures` is empty; `Accommodations[0].AvailableFeatures` is empty.

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationZonesServiceTests.cs` (new file)

#### 6e — `GetByIdAsync_WhenZoneExistsInEdition_ReturnsZoneResponse`

Arrange: mock `IAccommodationZonesRepository.GetByIdAsync` returns a zone with correct `CampEditionId`.
Assert: returned `AccommodationZoneResponse.Id` matches; service does not throw.

#### 6f — `GetByIdAsync_WhenZoneNotFound_ThrowsNotFoundException`

Arrange: `zonesRepository.GetByIdAsync` returns `null`.
Assert: `NotFoundException` is thrown.

#### 6g — `GetByIdAsync_WhenZoneBelongsToDifferentEdition_ThrowsNotFoundException`

Arrange: zone found but `CampEditionId` differs from the requested `campEditionId`.
Assert: `NotFoundException` is thrown (same behaviour as not found).

---

### Step 7 — Build and run tests

```bash
dotnet build src/Abuvi.API
dotnet test src/Abuvi.Tests --filter "FullyQualifiedName~AccommodationAssignmentsRepositoryTests|FullyQualifiedName~AccommodationZonesServiceTests"
dotnet test src/Abuvi.Tests
```

All pre-existing tests must still pass. No new compilation warnings (treat warnings as errors is on).

---

### Step 8 — Update technical documentation

1. **`ai-specs/specs/api-spec.yml`**: Add `GET /api/camps/editions/{campEditionId}/accommodation-zones/{zoneId}` endpoint entry with its 200 / 404 responses.
2. **`ai-specs/specs/data-model.md`**: Note that `ProposalAssignmentStateResponse` now carries `AccommodationTypeLookup` and `AllFeatures`; `AssignmentFamilyResponse` now has a separate `BabyCount` field.

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Update `CampsModels.cs` (new records + DTO changes)
3. Step 5 — Fix existing tests that break (compile check)
4. Step 6e–6g — Write failing `AccommodationZonesServiceTests`
5. Step 3 — Implement `AccommodationZonesService.GetByIdAsync` (makes 6e–6g pass)
6. Step 4 — Register endpoint in `CampsEndpoints.cs`
7. Step 6a–6d — Write failing repository tests
8. Step 2 — Update `AccommodationAssignmentsRepository` (makes 6a–6d pass)
9. Step 7 — Build + full test run
10. Step 8 — Documentation

---

## Testing Checklist

- [ ] `GetAssignmentStateAsync` returns `BabyCount` separately from `ChildCount`
- [ ] `AccommodationTypeLookup` includes all active accommodations (including non-assignable)
- [ ] `AllFeatures` contains deduplicated feature name/icon entries for every feature present in any assignable accommodation
- [ ] `AvailableFeatures` on each `AssignmentAccommodationResponse` is now a non-empty list (when features are assigned)
- [ ] `GET /api/camps/editions/{campEditionId}/accommodation-zones/{zoneId}` returns 200 with full zone DTO
- [ ] `GET /{zoneId}` returns 404 for unknown zone or wrong edition
- [ ] No regression in other `AccommodationAssignmentsRepository` tests (auto-assign, unassign, bulk replace)
- [ ] `dotnet test` reports 0 failures

---

## Error Response Format

```json
// 200 OK — GetZoneById
{ "success": true, "data": { "id": "...", "name": "Zona Albergue", "mediaItems": [...], ... } }

// 404 Not Found — zone not found or wrong edition
{ "success": false, "data": null, "error": { "message": "AccommodationZone with ID '...' was not found" } }
```

---

## Dependencies

No new NuGet packages. No new EF Core migration (no schema change).

---

## Notes

- `CampEditionAccommodation.FeatureAssignments` is the nav prop (via `AccommodationFeatureAssignment` join entity). The `ThenInclude(fa => fa.Feature)` pattern is already used in `CampEditionAccommodationsRepository.GetByEditionAsync` — follow that pattern.
- `AccommodationFeatureAssignment.FeatureId` is the scalar FK; `AccommodationFeatureAssignment.Feature` is the nav prop loaded by `ThenInclude`.
- The `allActiveAccommodations` projection query uses `.Select(a => new AccommodationTypeLookupItem(...))` — EF Core translates this to SQL projection, so it is efficient (no full entity load).
- `AllFeatures` is built **in memory** from the already-loaded `accommodations` nav properties — no extra DB round trip needed.
- `AccommodationZonesService.GetByIdAsync` does **not** check `z.IsActive` — zones are returned even if inactive (admin gallery access). If this should be restricted, add the check and a dedicated test.

---

## Next Steps After Implementation

- Open PR against `dev` following the project git workflow.
- Merge before `feat-encaje-bolillos-ux-fixes-frontend` is merged (frontend uses the new DTO fields).

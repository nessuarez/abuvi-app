# Backend Implementation Plan: feat-encaje-bolillos-ux-improvements

## Overview

Implements the backend changes required by `feat-encaje-bolillos_ux-improvements_enriched.md`. Two concrete backend improvements:

1. **IsAssignable flag** — adds a `bool IsAssignable` column to `camp_edition_accommodations` so the Board can mark type-level/placeholder accommodations as non-assignable. The assignment board only shows `IsAssignable = true` entries.
2. **Zone photo in assignment response** — extends `AssignmentAccommodationResponse` with zone media (thumbnail/file URLs from the existing `MediaItem` system) so the frontend can show zone photos in the assignment panel.

> **Important design note on photos:** The spec draft proposed adding a simple `PhotoUrl` string to both entities. This is incorrect for this project — photos are managed via the existing `MediaItem` system (`AccommodationZone.MediaItems`, `CampEditionAccommodation.MediaItems`). No new URL column is needed; the missing piece is loading zone media in the assignments query and surfacing it in the response DTO.

Architecture: all changes stay within `src/Abuvi.API/Features/Camps/` (Vertical Slice Architecture).

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

**Files to modify (no new files needed):**

| File | Change |
|------|--------|
| `CampsModels.cs` | Add `IsAssignable` to entity + 3 DTOs; add `ZonePrimaryThumbnailUrl`/`ZonePrimaryFileUrl` to `AssignmentAccommodationResponse` |
| `Data/Configurations/CampEditionAccommodationConfiguration.cs` | Add `is_assignable` column |
| `CampEditionAccommodationsService.cs` | Update `ToResponse` extension to include `IsAssignable` |
| `AccommodationAssignmentsRepository.cs` | Load zone media in query; filter `IsAssignable`; populate new DTO fields |
| Migration (new file) | `AddIsAssignableToAccommodations` |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-encaje-bolillos-ux-improvements-backend
git branch
```

> Do NOT work directly on `feature/feat-encaje-bolillos` — this is a separate concern.

---

### Step 1: Write failing unit tests

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/AccommodationAssignmentsRepositoryTests.cs` (extend existing or create)
- **Action**: Write failing tests first (TDD).

Test cases to add:

```
GetAssignmentStateAsync_WhenAccommodationIsNotAssignable_ExcludesItFromResponse
GetAssignmentStateAsync_WhenZoneHasPrimaryMedia_IncludesZoneThumbnailInResponse
GetAssignmentStateAsync_WhenZoneHasNoMedia_ReturnsNullZoneThumbnailUrl
```

For `CampEditionAccommodationsServiceTests.cs` (extend existing):

```
ToResponse_IncludesIsAssignable_WhenTrue
ToResponse_IncludesIsAssignable_WhenFalse
```

For `UpdateCampEditionAccommodationValidatorTests.cs` (extend existing):

```
Validate_IsAssignable_IsRequired_WhenNotProvided  // FluentValidation has no rule — it's a bool, always valid
```

> These tests will fail until Steps 2–5 are done. Run `dotnet test` to confirm red.

---

### Step 2: Add `IsAssignable` to `CampEditionAccommodation` entity

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add the property after `IsActive`.

**Current entity (lines ~386-407):**

```csharp
public bool IsActive { get; set; } = true;
// ... SortOrder follows
```

**Change**: Add after `IsActive`:

```csharp
public bool IsAssignable { get; set; } = true;
```

Full property position (after `IsActive`, before `SortOrder`):

```csharp
public bool IsActive { get; set; } = true;
public bool IsAssignable { get; set; } = true;   // ADD
public int SortOrder { get; set; } = 0;
```

---

### Step 3: Update DTOs in `CampsModels.cs`

Three DTOs need updating:

#### 3a. `CampEditionAccommodationResponse` (lines ~411-429)

Add `bool IsAssignable` **after** `bool IsActive`:

```csharp
public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,
    int Quantity,
    bool IsActive,
    bool IsAssignable,              // ADD
    int SortOrder,
    int CurrentPreferenceCount,
    int FirstChoiceCount,
    Guid? ZoneId,
    string? ZoneName,
    IReadOnlyList<AccommodationFeatureResponse> Features,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 3b. `UpdateCampEditionAccommodationRequest` (lines ~442-452)

Add `bool IsAssignable` **after** `bool IsActive`:

```csharp
public record UpdateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,
    int Quantity,
    bool IsActive,
    bool IsAssignable,              // ADD
    Guid? ZoneId,
    int SortOrder
);
```

#### 3c. `AssignmentAccommodationResponse` (lines ~1112-1126)

Add zone media fields **after** existing `PrimaryFileUrl`:

```csharp
public record AssignmentAccommodationResponse(
    Guid Id,
    string Name,
    AccommodationType Type,
    int? Capacity,
    bool CountByFamily,
    Guid? ZoneId,
    string? ZoneName,
    int SortOrder,
    IReadOnlyList<Guid> AvailableFeatures,
    int Quantity,
    int? UnitIndex,
    string? PrimaryThumbnailUrl = null,
    string? PrimaryFileUrl = null,
    string? ZonePrimaryThumbnailUrl = null,     // ADD
    string? ZonePrimaryFileUrl = null           // ADD
);
```

> `IsAssignable` is intentionally **not** added to `AssignmentAccommodationResponse` because the assignments query already filters it out — there is no reason to expose the flag to the assignment board client.

---

### Step 4: Update EF Core configuration

- **File**: `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs`
- **Action**: Add `is_assignable` column after the `is_active` mapping.

```csharp
builder.Property(a => a.IsAssignable)
    .HasDefaultValue(true)
    .HasColumnName("is_assignable");
```

Position: after the `is_active` property configuration, before `sort_order`.

---

### Step 5: Update `CampEditionAccommodationsService.cs` — `ToResponse` extension

- **File**: `src/Abuvi.API/Features/Camps/CampEditionAccommodationsService.cs`
- **Action**: Add `IsAssignable` to the `ToResponse` mapping (lines ~143-168).

**Current call** (inside `new CampEditionAccommodationResponse(...)` positional record constructor):

```csharp
a.IsActive,
a.SortOrder,
```

**Updated call** — add `a.IsAssignable` between `IsActive` and `SortOrder`:

```csharp
a.IsActive,
a.IsAssignable,     // ADD
a.SortOrder,
```

Also update `UpdateAccommodation` handler (wherever the entity is updated from `UpdateCampEditionAccommodationRequest`) to map the new field:

```csharp
accommodation.IsAssignable = request.IsAssignable;
```

Search for the update handler in `CampsEndpoints.cs` (around lines 414-457) to find where `accommodation.IsActive = request.IsActive` is set and add the new line immediately after.

---

### Step 6: Update `AccommodationAssignmentsRepository.cs`

- **File**: `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs`
- **Action**: Two changes in `GetAssignmentStateAsync`.

#### 6a. Filter non-assignable accommodations (line ~49)

**Current:**

```csharp
var accommodations = await db.CampEditionAccommodations
    .AsNoTracking()
    .Where(a => a.CampEditionId == campEditionId && a.IsActive)
    .Include(a => a.Zone)
    .Include(a => a.MediaItems.Where(m => m.IsPrimary).Take(1))
```

**Updated** — add `&& a.IsAssignable` to the `Where` clause, and add `ThenInclude` for zone media:

```csharp
var accommodations = await db.CampEditionAccommodations
    .AsNoTracking()
    .Where(a => a.CampEditionId == campEditionId && a.IsActive && a.IsAssignable)
    .Include(a => a.Zone)
        .ThenInclude(z => z!.MediaItems.Where(m => m.IsPrimary).Take(1))
    .Include(a => a.MediaItems.Where(m => m.IsPrimary).Take(1))
```

> The `z!` null-forgiving is needed because `Zone` is a nullable navigation property. EF Core will silently skip the `ThenInclude` for rows where `Zone` is null.

#### 6b. Populate `ZonePrimaryThumbnailUrl` and `ZonePrimaryFileUrl` in the response builder (lines ~94-117)

**Current** (inside the `SelectMany` lambda):

```csharp
var primaryMedia = a.MediaItems.FirstOrDefault(m => m.IsPrimary);
return Enumerable.Range(0, a.Quantity).Select(unitIndex =>
    new AssignmentAccommodationResponse(
        // ...existing params...
        primaryMedia?.ThumbnailUrl,
        primaryMedia?.FileUrl
    ));
```

**Updated** — add zone media lookup before the `return`:

```csharp
var primaryMedia = a.MediaItems.FirstOrDefault(m => m.IsPrimary);
var zonePrimaryMedia = a.Zone?.MediaItems.FirstOrDefault(m => m.IsPrimary);
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
        [],
        a.Quantity,
        a.Quantity > 1 ? unitIndex : (int?)null,
        primaryMedia?.ThumbnailUrl,
        primaryMedia?.FileUrl,
        zonePrimaryMedia?.ThumbnailUrl,      // ADD
        zonePrimaryMedia?.FileUrl            // ADD
    ));
```

---

### Step 7: Create EF Core migration

- **Action**: Generate migration after all model/config changes are in place.

```bash
dotnet ef migrations add AddIsAssignableToAccommodations --project src/Abuvi.API
```

**Expected migration operations:**

1. `AddColumn<bool>` on `camp_edition_accommodations`: `is_assignable` (boolean, NOT NULL, DEFAULT TRUE)

**Verify migration** before applying:

```bash
dotnet ef migrations script --idempotent --project src/Abuvi.API
```

Confirm only one `ALTER TABLE camp_edition_accommodations ADD COLUMN is_assignable boolean NOT NULL DEFAULT TRUE` operation appears.

**Apply:**

```bash
dotnet ef database update --project src/Abuvi.API
```

---

### Step 8: Update validator

- **File**: `src/Abuvi.API/Features/Camps/CampsValidators.cs`
- **Action**: `UpdateCampEditionAccommodationRequestValidator` — no new rule needed. `IsAssignable` is a `bool` (always valid). No change required to the validator.

If a `CreateCampEditionAccommodationRequest` also exists, check if `IsAssignable` needs to be added there too (it defaults to `true` so it is optional at creation time). If the create request DTO does not include it, the entity default (`= true`) applies automatically — no change needed.

---

### Step 9: Run tests to green

```bash
dotnet test src/Abuvi.Tests --filter "Category=Unit" --no-build
```

All tests added in Step 1 should now pass. Verify no regressions in existing accommodation tests.

---

### Step 10: Update technical documentation

- **Action**: Review and update docs to reflect the schema change.

1. **Data model**: Update `ai-specs/specs/data-model.md` — add `is_assignable boolean NOT NULL DEFAULT TRUE` to the `camp_edition_accommodations` table entry.
2. **API spec**: Update `ai-specs/specs/api-spec.yml` — add `isAssignable` field to `CampEditionAccommodationResponse` and `UpdateCampEditionAccommodationRequest` schemas; add `zonePrimaryThumbnailUrl`/`zonePrimaryFileUrl` to `AssignmentAccommodationResponse`.
3. No architecture or standards changes needed.

---

## Implementation Order

1. Step 0 — Create branch `feature/feat-encaje-bolillos-ux-improvements-backend`
2. Step 1 — Write failing unit tests (TDD)
3. Step 2 — Add `IsAssignable` to `CampEditionAccommodation` entity
4. Step 3 — Update DTOs (`CampEditionAccommodationResponse`, `UpdateCampEditionAccommodationRequest`, `AssignmentAccommodationResponse`)
5. Step 4 — Update EF configuration (`is_assignable` column)
6. Step 5 — Update `ToResponse` extension + update endpoint handler
7. Step 6 — Update `AccommodationAssignmentsRepository` (filter + zone media)
8. Step 7 — Generate and apply migration
9. Step 8 — Verify validator (no rule needed)
10. Step 9 — Run all tests to green
11. Step 10 — Update technical documentation

---

## Testing Checklist

- [ ] `GetAssignmentStateAsync_WhenAccommodationIsNotAssignable_ExcludesItFromResponse` — pass
- [ ] `GetAssignmentStateAsync_WhenZoneHasPrimaryMedia_IncludesZoneThumbnailInResponse` — pass
- [ ] `GetAssignmentStateAsync_WhenZoneHasNoMedia_ReturnsNullZoneThumbnailUrl` — pass
- [ ] `ToResponse_IncludesIsAssignable_WhenTrue` — pass
- [ ] `ToResponse_IncludesIsAssignable_WhenFalse` — pass
- [ ] `dotnet test` — all existing tests still pass (no regressions)
- [ ] `PUT /api/camps/editions/accommodations/{id}` with `isAssignable: false` persists correctly
- [ ] `GET /api/camps/editions/{id}/assignment-proposals/{id}/assignments` excludes non-assignable accommodations
- [ ] Zone photo URLs appear in `AssignmentAccommodationResponse` when zone has a primary media item

---

## Error Response Format

No new error cases. Existing `ApiResponse<T>` envelope applies. The `IsAssignable` field is a boolean — invalid values are rejected at model binding level (400) before reaching the validator.

---

## Dependencies

No new NuGet packages required.

```bash
# Migration
dotnet ef migrations add AddIsAssignableToAccommodations --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Notes

- **No `PhotoUrl` string column**: The spec draft proposed a raw URL string. This project uses the existing `MediaItem` system — do NOT add a `PhotoUrl` property. Zone photos come from `AccommodationZone.MediaItems` (already supported by the media management feature).
- **`IsAssignable` default = `true`**: The migration uses `DEFAULT TRUE` so all existing rows remain assignable after the migration — no data migration script needed.
- **Existing assignments not broken**: Non-assignable accommodations may already have assignments in `AccommodationAssignment` rows (from proposals created before this change). The filter is only on the board's READING of available accommodations, not on historical data. Existing assignment rows are not deleted.
- **English only**: All C# identifiers, logs, and migration names must be in English per `base-standards.mdc`. Validation messages (if any) in Spanish.
- **Branch separation**: This is `feature/feat-encaje-bolillos-ux-improvements-backend`, not the original `feature/feat-encaje-bolillos-backend`. Both branches may coexist; this one targets `dev` after the original has merged.

---

## Next Steps After Implementation

1. Create PR `feature/feat-encaje-bolillos-ux-improvements-backend → dev`
2. Frontend changes (filter bar, compact cards, `FamilyAssignmentCard` improvements) are in a separate frontend ticket — they depend on the new `isAssignable` and `zonePrimaryThumbnailUrl` fields being available in the API response.
3. After merge, the assignment board frontend implementation can use `isAssignable` to show warnings and `zonePrimaryThumbnailUrl` for zone thumbnails.

---

## Implementation Verification

- **Build**: `dotnet build src/Abuvi.API` — zero warnings, zero errors
- **Functionality**: `PUT` with `isAssignable: false` → subsequent `GET /assignments` excludes that accommodation
- **Testing**: All 5 new unit tests pass; `dotnet test` green
- **Migration**: `dotnet ef database update` applies cleanly; `\d camp_edition_accommodations` in psql confirms `is_assignable` column with DEFAULT TRUE
- **Documentation**: `data-model.md` and `api-spec.yml` updated

# Backend Implementation Plan: feat-zone-accommodation-units — CountByFamily field on accommodation units

## Overview

Adds `CountByFamily: bool` as an explicit, persisted field on `CampEditionAccommodation`, exposes it through all management DTOs, and removes the current hard-coded derivation from `AccommodationType` in `AccommodationAssignmentsRepository`. No new endpoints or migrations beyond the single schema column addition. All changes are contained within the `Camps` vertical slice.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

**Files to modify:**

| File | Change |
|---|---|
| `CampsModels.cs` | Add `CountByFamily` to entity + 3 DTOs |
| `Data/Configurations/CampEditionAccommodationConfiguration.cs` | Map new column |
| `CampEditionAccommodationsService.cs` | Assign field in Create/Update; include in ToResponse |
| `AccommodationAssignmentsRepository.cs` | Remove `ByFamilyTypes`, use `a.CountByFamily` |
| `CampsValidators.cs` | No changes — bool needs no validation |

**New files:**

| File | Purpose |
|---|---|
| `Migrations/YYYYMMDDHHMMSS_AddCountByFamilyToAccommodations.cs` | Schema migration |
| `src/Abuvi.Tests/Unit/Features/Camps/CampEditionAccommodationsServiceTests.cs` | New unit test class |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch:** `feature/feat-zone-accommodation-units-backend`
- Base on `feature/feat-accommodation-features-backend` (the encaje-bolillos working branch)
- Commands:
  ```bash
  cd C:\repos\abuvi-app.worktrees\feat-encaje-bolillos
  git checkout -b feature/feat-zone-accommodation-units-backend
  git branch
  ```

---

### Step 1: Add `CountByFamily` to the entity — `CampsModels.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add the new property to `CampEditionAccommodation` (the entity class, around line 360):

```csharp
public bool CountByFamily { get; set; } = false;
```

Full entity after change:

```csharp
public class CampEditionAccommodation
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public Guid? ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccommodationType AccommodationType { get; set; }
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public bool CountByFamily { get; set; } = false;   // ← NEW
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public CampEdition CampEdition { get; set; } = null!;
    public AccommodationZone? Zone { get; set; }
    public ICollection<AccommodationFeatureAssignment> FeatureAssignments { get; set; } = [];
}
```

---

### Step 2: Update DTOs — `CampsModels.cs`

**2a. `CampEditionAccommodationResponse`** — add `CountByFamily` after `Capacity`:

```csharp
public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,      // ← NEW (position after Capacity)
    bool IsActive,
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

**2b. `CreateCampEditionAccommodationRequest`** — add `CountByFamily` as optional with `false` default (service will override for Tent/Caravan):

```csharp
public record CreateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool? CountByFamily = null,  // ← NEW — null means "apply smart default in service"
    Guid? ZoneId = null,
    int SortOrder = 0
);
```

**2c. `UpdateCampEditionAccommodationRequest`** — add `CountByFamily` as required:

```csharp
public record UpdateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,  // ← NEW (required on update)
    bool IsActive,
    Guid? ZoneId,
    int SortOrder
);
```

**Implementation notes:**
- `bool?` on Create allows the caller to pass `null` meaning "use smart default". The service resolves it.
- `bool` (non-nullable) on Update is required — no guessing on edit.
- Position `CountByFamily` immediately after `Capacity` in both request and response records for readability.

---

### Step 3: EF Core column mapping — `CampEditionAccommodationConfiguration.cs`

**File:** `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs`

Add after the `Capacity` property mapping (before the check constraint for `Capacity`):

```csharp
builder.Property(e => e.CountByFamily)
    .IsRequired()
    .HasDefaultValue(false)
    .HasColumnName("count_by_family");
```

Full `Configure` method after change — insert the block between Capacity and IsActive mappings:

```csharp
builder.Property(e => e.Capacity)
    .HasColumnName("capacity");

// ← INSERT HERE:
builder.Property(e => e.CountByFamily)
    .IsRequired()
    .HasDefaultValue(false)
    .HasColumnName("count_by_family");

builder.ToTable(t => t.HasCheckConstraint(
    "CK_CampEditionAccommodations_Capacity",
    "capacity IS NULL OR capacity > 0"));

builder.Property(e => e.IsActive)
    ...
```

---

### Step 4: Update `CampEditionAccommodationsService.cs`

**File:** `src/Abuvi.API/Features/Camps/CampEditionAccommodationsService.cs`

**4a. In `CreateAsync`** — resolve `CountByFamily` with smart default:

```csharp
var accommodation = new CampEditionAccommodation
{
    Id = Guid.NewGuid(),
    CampEditionId = campEditionId,
    Name = request.Name,
    AccommodationType = request.AccommodationType,
    Description = request.Description,
    Capacity = request.Capacity,
    CountByFamily = request.CountByFamily            // ← NEW
        ?? request.AccommodationType is AccommodationType.Tent or AccommodationType.Caravan,
    ZoneId = request.ZoneId,
    IsActive = true,
    SortOrder = request.SortOrder,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

**4b. In `UpdateAsync`** — assign the field:

```csharp
accommodation.Name = request.Name;
accommodation.AccommodationType = request.AccommodationType;
accommodation.Description = request.Description;
accommodation.Capacity = request.Capacity;
accommodation.CountByFamily = request.CountByFamily;  // ← NEW
accommodation.IsActive = request.IsActive;
accommodation.ZoneId = request.ZoneId;
accommodation.SortOrder = request.SortOrder;
accommodation.UpdatedAt = DateTime.UtcNow;
```

**4c. In `ToResponse` extension** (`CampEditionAccommodationExtensions`) — add `a.CountByFamily` to the positional record constructor after `a.Capacity`:

```csharp
public static CampEditionAccommodationResponse ToResponse(
    this CampEditionAccommodation a,
    int currentPreferenceCount,
    int firstChoiceCount)
    => new(
        a.Id,
        a.CampEditionId,
        a.Name,
        a.AccommodationType,
        a.Description,
        a.Capacity,
        a.CountByFamily,      // ← NEW (must match record constructor position)
        a.IsActive,
        a.SortOrder,
        currentPreferenceCount,
        firstChoiceCount,
        a.ZoneId,
        a.Zone?.Name,
        a.FeatureAssignments.Select(fa => fa.Feature.ToResponse()).ToList().AsReadOnly(),
        a.CreatedAt,
        a.UpdatedAt
    );
```

---

### Step 5: Remove hard-coded `ByFamilyTypes` — `AccommodationAssignmentsRepository.cs`

**File:** `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs`

**5a. Delete** the static field (lines 11-12):
```csharp
// DELETE THIS:
private static readonly HashSet<AccommodationType> ByFamilyTypes =
    [AccommodationType.Caravan, AccommodationType.Tent];
```

**5b. In `GetAssignmentStateAsync`** — change the `AssignmentAccommodationResponse` projection (around line 74-83):
```csharp
var accommodationResponses = accommodations.Select(a => new AssignmentAccommodationResponse(
    a.Id,
    a.Name,
    a.AccommodationType,
    a.Capacity,
    a.CountByFamily,                                // ← WAS: ByFamilyTypes.Contains(a.AccommodationType)
    a.ZoneId,
    a.Zone?.Name,
    a.SortOrder
)).ToList();
```

**5c. Check for any other use of `ByFamilyTypes`** — search the file. If found at line ~177 (capacity validation), replace similarly:
```csharp
// Look for: ByFamilyTypes.Contains(acc.AccommodationType)
// Replace with: acc.CountByFamily
```

**Implementation note:** The EF Core query that loads `accommodations` in `GetAssignmentStateAsync` must include the `Zone` navigation for `Zone?.Name` — verify `.Include(a => a.Zone)` is already in the query. Also verify the `accommodations` collection has the new `CountByFamily` field populated (it will, because it comes from `db.CampEditionAccommodations` which is the entity after migration).

---

### Step 6: EF Core Migration

```bash
cd C:\repos\abuvi-app.worktrees\feat-encaje-bolillos
dotnet ef migrations add AddCountByFamilyToAccommodations \
  --project src/Abuvi.API \
  --startup-project src/Abuvi.API
```

**Expected generated migration** (verify it contains):

```csharp
migrationBuilder.AddColumn<bool>(
    name: "count_by_family",
    table: "camp_edition_accommodations",
    type: "boolean",
    nullable: false,
    defaultValue: false);
```

**Data migration note:** Existing records will have `count_by_family = false` (the column default). For correctness, the migration `Up()` should backfill `Tent` and `Caravan` rows:

```csharp
migrationBuilder.Sql(@"
    UPDATE camp_edition_accommodations
    SET count_by_family = true
    WHERE accommodation_type IN ('Tent', 'Caravan');
");
```

Add this `migrationBuilder.Sql(...)` call manually after the `AddColumn` in the generated migration's `Up()` method.

Apply:
```bash
dotnet ef database update --project src/Abuvi.API --startup-project src/Abuvi.API
```

---

### Step 7: Unit Tests — `CampEditionAccommodationsServiceTests.cs`

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampEditionAccommodationsServiceTests.cs` *(new)*

Follow the pattern from `AccommodationFeaturesServiceTests.cs`. Use NSubstitute for repository, FluentAssertions for assertions.

```csharp
using Abuvi.API.Features.Camps;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class CampEditionAccommodationsServiceTests
{
    private readonly ICampEditionAccommodationsRepository _repository;
    private readonly ICampEditionsRepository _editionsRepository;
    private readonly CampEditionAccommodationsService _service;

    public CampEditionAccommodationsServiceTests()
    {
        _repository = Substitute.For<ICampEditionAccommodationsRepository>();
        _editionsRepository = Substitute.For<ICampEditionsRepository>();
        _service = new CampEditionAccommodationsService(_repository, _editionsRepository);
    }

    [Fact]
    public async Task CreateAsync_WithTentType_DefaultsCountByFamilyTrue()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var edition = CreateTestEdition(editionId);
        _editionsRepository.GetByIdAsync(editionId, Arg.Any<CancellationToken>()).Returns(edition);
        _repository.GetPreferenceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetFirstChoiceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);

        var request = new CreateCampEditionAccommodationRequest(
            "Parcela T-01", AccommodationType.Tent, null, 1,
            CountByFamily: null, ZoneId: null, SortOrder: 0);

        // Act
        var result = await _service.CreateAsync(editionId, request, CancellationToken.None);

        // Assert
        result.CountByFamily.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithCaravanType_DefaultsCountByFamilyTrue()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        _editionsRepository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(CreateTestEdition(editionId));
        _repository.GetPreferenceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetFirstChoiceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);

        var request = new CreateCampEditionAccommodationRequest(
            "Parcela C-01", AccommodationType.Caravan, null, null,
            CountByFamily: null, ZoneId: null, SortOrder: 0);

        // Act
        var result = await _service.CreateAsync(editionId, request, CancellationToken.None);

        // Assert
        result.CountByFamily.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithLodgeType_DefaultsCountByFamilyFalse()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        _editionsRepository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(CreateTestEdition(editionId));
        _repository.GetPreferenceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetFirstChoiceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);

        var request = new CreateCampEditionAccommodationRequest(
            "Habitación 101", AccommodationType.Lodge, null, 4,
            CountByFamily: null, ZoneId: null, SortOrder: 0);

        // Act
        var result = await _service.CreateAsync(editionId, request, CancellationToken.None);

        // Assert
        result.CountByFamily.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithExplicitCountByFamilyFalse_OverridesTypeDefault()
    {
        // Arrange — Tent type but explicit CountByFamily = false
        var editionId = Guid.NewGuid();
        _editionsRepository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(CreateTestEdition(editionId));
        _repository.GetPreferenceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetFirstChoiceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);

        var request = new CreateCampEditionAccommodationRequest(
            "Parcela especial", AccommodationType.Tent, null, 6,
            CountByFamily: false, ZoneId: null, SortOrder: 0);  // explicit override

        // Act
        var result = await _service.CreateAsync(editionId, request, CancellationToken.None);

        // Assert
        result.CountByFamily.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithExplicitCountByFamilyTrue_OnLodge_UsesProvidedValue()
    {
        // Arrange — Lodge type but explicitly CountByFamily = true (edge case)
        var editionId = Guid.NewGuid();
        _editionsRepository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
            .Returns(CreateTestEdition(editionId));
        _repository.GetPreferenceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetFirstChoiceCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(0);

        var request = new CreateCampEditionAccommodationRequest(
            "Suite familiar", AccommodationType.Lodge, null, null,
            CountByFamily: true, ZoneId: null, SortOrder: 0);

        // Act
        var result = await _service.CreateAsync(editionId, request, CancellationToken.None);

        // Assert
        result.CountByFamily.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCountByFamily()
    {
        // Arrange
        var id = Guid.NewGuid();
        var accommodation = CreateTestAccommodation(id, AccommodationType.Lodge, countByFamily: false);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(accommodation);
        _repository.GetPreferenceCountAsync(id, Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetFirstChoiceCountAsync(id, Arg.Any<CancellationToken>()).Returns(0);

        var request = new UpdateCampEditionAccommodationRequest(
            "Habitación 101", AccommodationType.Lodge, null, 4,
            CountByFamily: true,   // changed to true
            IsActive: true, ZoneId: null, SortOrder: 0);

        // Act
        var result = await _service.UpdateAsync(id, request, CancellationToken.None);

        // Assert
        result.CountByFamily.Should().BeTrue();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static CampEdition CreateTestEdition(Guid id) => new()
    {
        Id = id,
        CampId = Guid.NewGuid(),
        Year = 2026,
        StartDate = DateTime.UtcNow.AddDays(30),
        EndDate = DateTime.UtcNow.AddDays(37),
        Location = "Test location",
        Status = CampEditionStatus.Draft,
        PricePerAdult = 100m,
        PricePerChild = 80m,
        PricePerBaby = 40m,
        MaxCapacity = 100,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static CampEditionAccommodation CreateTestAccommodation(
        Guid id,
        AccommodationType type,
        bool countByFamily) => new()
    {
        Id = id,
        CampEditionId = Guid.NewGuid(),
        Name = "Test Unit",
        AccommodationType = type,
        CountByFamily = countByFamily,
        IsActive = true,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FeatureAssignments = []
    };
}
```

---

### Step 8: Update Technical Documentation

**File:** `ai-specs/specs/data-model.md`

In the `CampEditionAccommodation` entity section, add the `countByFamily` field:

```
- countByFamily: boolean — when true, capacity is measured in family/unit slots (one per registration);
  when false, capacity is measured in persons. Defaults to true for Tent and Caravan types, false otherwise.
  Replaces the previous hard-coded derivation from accommodation type.
```

Also note in the business rules section:
> `AccommodationAssignmentsRepository` previously derived `CountByFamily` from `AccommodationType in [Caravan, Tent]`. This is now an explicit field on the entity, backfilled by migration for existing rows.

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Add `CountByFamily` property to `CampEditionAccommodation` entity
3. Step 2 — Update `CampEditionAccommodationResponse`, `CreateCampEditionAccommodationRequest`, `UpdateCampEditionAccommodationRequest`
4. Step 3 — Add EF Core column mapping in configuration
5. Step 4 — Update service (`CreateAsync`, `UpdateAsync`, `ToResponse`)
6. Step 5 — Remove `ByFamilyTypes` from `AccommodationAssignmentsRepository`
7. Step 6 — Generate migration + add backfill SQL + apply
8. Step 7 — Write unit tests
9. Step 8 — Update data-model.md documentation

---

## Testing Checklist

- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] `dotnet test` — all new tests pass; no regressions on `AutoAssignServiceTests`
- [ ] `dotnet ef database update` — migration applies cleanly
- [ ] `GET /api/camps/editions/{editionId}/accommodations` response includes `countByFamily` field
- [ ] `POST` with no `countByFamily` → Lodge gets `false`, Tent gets `true`, Caravan gets `true`
- [ ] `POST` with explicit `countByFamily: false` on Tent → stored as `false`
- [ ] `PUT` with `countByFamily: true` → persisted and returned
- [ ] Assignment board still works (verify `AutoAssignServiceTests` pass unchanged)

---

## Error Response Format

Standard `ApiResponse<T>` envelope — no new error codes. All existing 400/404/409 patterns remain.

---

## Dependencies

No new NuGet packages required.

Migration commands:
```bash
dotnet ef migrations add AddCountByFamilyToAccommodations \
  --project src/Abuvi.API --startup-project src/Abuvi.API

dotnet ef database update \
  --project src/Abuvi.API --startup-project src/Abuvi.API
```

---

## Notes

- **No breaking change to the assignment board.** `AssignmentAccommodationResponse.CountByFamily` still exists and the `AutoAssignService` logic is unchanged — it simply reads from the entity field instead of deriving it. `AutoAssignServiceTests` should pass without modification.
- **Positional record constructors** — adding `CountByFamily` to `CampEditionAccommodationResponse` and `UpdateCampEditionAccommodationRequest` is a positional record change. All call sites (service `ToResponse`, endpoint tests) must be updated simultaneously in Step 4.
- **Existing records after migration** — the backfill SQL sets `count_by_family = true` for Tent and Caravan rows, preserving current behaviour for existing data.
- **`CampsValidators.cs`** — no changes needed. `CountByFamily` is a `bool`/`bool?` with no constraints to validate.
- All error messages remain in Spanish (as per project standards for domain errors surfaced to users).

---

## Next Steps After Implementation

- Frontend ticket (`feat-zone-accommodation-units-frontend`) will expose `countByFamily` in `CampEditionAccommodationDialog` and add the zone units sub-panel in `AccommodationZonePanel`.

# Backend Implementation Plan: feat-accommodation-quantity — Accommodation Quantity per Zone

## Overview

Adds a `Quantity` field to `CampEditionAccommodation` so admins can define how many physical units of a given accommodation type exist in a zone (e.g., "10 double rooms"). When `Quantity > 1`, the assignment board API expands the record into N virtual slots, each independently assignable. Slot ownership is tracked via a `UnitIndex` field added to `AccommodationAssignment`.

Architecture: **Vertical Slice Architecture** — all changes are contained within `src/Abuvi.API/Features/Camps/` and its EF Core configurations.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

### Files to modify

| File | Change |
|---|---|
| `CampsModels.cs` | Add `Quantity` to entity; add `Quantity` to 3 accommodation DTOs; add `Quantity` + `UnitIndex` to `AssignmentAccommodationResponse`; add `UnitIndex` to `AssignmentEntry` + `SingleAssignRequest` + `AccommodationAssignment` entity |
| `Data/Configurations/CampEditionAccommodationConfiguration.cs` | Map `quantity` column + check constraint |
| `Data/Configurations/AccommodationAssignmentConfiguration.cs` | Map `unit_index` nullable column + unique filtered index |
| `CampEditionAccommodationsService.cs` | Map `Quantity` in `CreateAsync`, `UpdateAsync` |
| `AccommodationAssignmentsRepository.cs` | Expand slots by quantity in `GetAssignmentStateAsync`; accept + store `UnitIndex` in `AssignAsync`; validate `UnitIndex` + update per-unit capacity check in `BulkReplaceAsync` |
| `IAccommodationAssignmentsRepository.cs` | Add `unitIndex` parameter to `AssignAsync` signature |
| `AccommodationAssignmentReportsService.cs` | Add `Quantity` + `CountByFamily` to `AccommodationReportItem`; update `ComputeGroupCapacity` to multiply by `Quantity`; replace `ByFamilyTypes` with `CountByFamily` in `ComputeUsedCapacity`; include both fields in `LoadAccommodationsAsync` projection |
| `CampsValidators.cs` | Add `Quantity >= 1` rule to both accommodation validators |

### New files

| File | Purpose |
|---|---|
| `Migrations/YYYYMMDDHHMMSS_AddQuantityToAccommodations.cs` | Adds `quantity` column to `camp_edition_accommodations` |
| `Migrations/YYYYMMDDHHMMSS_AddUnitIndexToAccommodationAssignments.cs` | Adds `unit_index` nullable column + unique filtered index to `accommodation_assignments` |
| `src/Abuvi.Tests/Unit/Features/Camps/AccommodationQuantityTests.cs` | Unit tests for slot expansion, capacity calculations, and BulkReplace validation |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch:** `feature/feat-accommodation-quantity-backend`
- **Base:** `dev` (or the current main integration branch)
- **Commands:**
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/feat-accommodation-quantity-backend
  git branch
  ```
- **Note:** This is the first step before any code changes.

---

### Step 1: Add `Quantity` to `CampEditionAccommodation` entity — `CampsModels.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

In the `CampEditionAccommodation` class, add `Quantity` immediately after `CountByFamily`:

```csharp
public bool CountByFamily { get; set; } = false;
public int Quantity { get; set; } = 1;        // ← NEW
```

Full entity after change (for reference):

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
    public bool CountByFamily { get; set; } = false;
    public int Quantity { get; set; } = 1;            // ← NEW
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

**Also in the same file — add `UnitIndex` to `AccommodationAssignment` entity:**

```csharp
public class AccommodationAssignment
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid AccommodationId { get; set; }
    public int? UnitIndex { get; set; }               // ← NEW
    public Guid AssignedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public AccommodationAssignmentProposal Proposal { get; set; } = null!;
    public Registrations.Registration Registration { get; set; } = null!;
    public CampEditionAccommodation Accommodation { get; set; } = null!;
}
```

---

### Step 2: Update DTOs — `CampsModels.cs`

#### 2a. `CampEditionAccommodationResponse` — add `Quantity` after `CountByFamily`

```csharp
public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,
    int Quantity,           // ← NEW (position after CountByFamily)
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

#### 2b. `CreateCampEditionAccommodationRequest` — add optional `Quantity`

```csharp
public record CreateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool? CountByFamily = null,
    int Quantity = 1,       // ← NEW (default 1 — single unit)
    Guid? ZoneId = null,
    int SortOrder = 0
);
```

#### 2c. `UpdateCampEditionAccommodationRequest` — add required `Quantity`

```csharp
public record UpdateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,
    int Quantity,           // ← NEW (required on update)
    bool IsActive,
    Guid? ZoneId,
    int SortOrder
);
```

#### 2d. `AssignmentAccommodationResponse` — add `Quantity` and `UnitIndex`

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
    int Quantity,           // ← NEW
    int? UnitIndex          // ← NEW: null when Quantity = 1
);
```

#### 2e. `AssignmentEntry` — add `UnitIndex`

```csharp
public record AssignmentEntry(
    Guid RegistrationId,
    Guid AccommodationId,
    int? UnitIndex          // ← NEW
);
```

#### 2f. `SingleAssignRequest` — add `UnitIndex`

```csharp
public record SingleAssignRequest(
    Guid AccommodationId,
    int? UnitIndex = null   // ← NEW: null for single-unit accommodations
);
```

**Important:** `AssignmentEntry` and `CampEditionAccommodationResponse` are positional records. All call sites must be updated simultaneously — the compiler will catch them. Run `dotnet build` after Step 2 to identify all affected call sites before proceeding.

---

### Step 3: EF Core column mappings

#### 3a. `CampEditionAccommodationConfiguration.cs`

Add the `quantity` property mapping immediately **after** the `count_by_family` mapping (before `IsActive`):

```csharp
// After the count_by_family mapping:
builder.Property(e => e.Quantity)
    .IsRequired()
    .HasDefaultValue(1)
    .HasColumnName("quantity");

builder.ToTable(t => t.HasCheckConstraint(
    "CK_CampEditionAccommodations_Quantity",
    "quantity > 0"));
```

The existing `CK_CampEditionAccommodations_Capacity` constraint remains unchanged.

#### 3b. `AccommodationAssignmentConfiguration.cs`

Add the `unit_index` property mapping **before** the existing unique index:

```csharp
builder.Property(a => a.UnitIndex)
    .HasColumnName("unit_index");

// Prevents double-booking the same physical unit within a proposal:
builder.HasIndex(a => new { a.ProposalId, a.AccommodationId, a.UnitIndex })
    .IsUnique()
    .HasFilter("unit_index IS NOT NULL")
    .HasDatabaseName("IX_AccommodationAssignments_Proposal_Accommodation_UnitIndex");
```

The existing `IX_AccommodationAssignments_Proposal_Registration` unique index (one registration per proposal) is not affected.

---

### Step 4: Update validators — `CampsValidators.cs`

In both `CreateCampEditionAccommodationRequestValidator` and `UpdateCampEditionAccommodationRequestValidator`, add after the `SortOrder` rule:

```csharp
RuleFor(x => x.Quantity)
    .GreaterThanOrEqualTo(1)
    .WithMessage("Quantity must be at least 1.");
```

---

### Step 5: Update service — `CampEditionAccommodationsService.cs`

#### 5a. In `CreateAsync` — add `Quantity` mapping

```csharp
var accommodation = new CampEditionAccommodation
{
    Id = Guid.NewGuid(),
    CampEditionId = campEditionId,
    Name = request.Name,
    AccommodationType = request.AccommodationType,
    Description = request.Description,
    Capacity = request.Capacity,
    CountByFamily = request.CountByFamily
        ?? request.AccommodationType is AccommodationType.Tent or AccommodationType.Caravan,
    Quantity = request.Quantity,           // ← NEW
    ZoneId = request.ZoneId,
    IsActive = true,
    SortOrder = request.SortOrder,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

#### 5b. In `UpdateAsync` — add `Quantity` mapping

```csharp
accommodation.Name = request.Name;
accommodation.AccommodationType = request.AccommodationType;
accommodation.Description = request.Description;
accommodation.Capacity = request.Capacity;
accommodation.CountByFamily = request.CountByFamily;
accommodation.Quantity = request.Quantity;             // ← NEW
accommodation.IsActive = request.IsActive;
accommodation.ZoneId = request.ZoneId;
accommodation.SortOrder = request.SortOrder;
accommodation.UpdatedAt = DateTime.UtcNow;
```

#### 5c. Update `ToResponse` extension method

Find the `ToResponse` extension on `CampEditionAccommodation` (in `CampsModels.cs` or a nearby extensions file). The positional record constructor call must include `a.Quantity` at the correct position (after `a.CountByFamily`, before `a.IsActive`):

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
        a.CountByFamily,
        a.Quantity,           // ← NEW
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

### Step 6: Update assignment repository — `AccommodationAssignmentsRepository.cs`

#### 6a. `GetAssignmentStateAsync` — expand slots by `Quantity`

Replace the existing `accommodationResponses` projection (lines 93–103):

**Before:**
```csharp
var accommodationResponses = accommodations.Select(a => new AssignmentAccommodationResponse(
    a.Id,
    a.Name,
    a.AccommodationType,
    a.Capacity,
    a.CountByFamily,
    a.ZoneId,
    a.Zone?.Name,
    a.SortOrder,
    []
)).ToList();
```

**After:**
```csharp
var accommodationResponses = accommodations
    .SelectMany(a => Enumerable.Range(0, a.Quantity).Select(unitIndex =>
        new AssignmentAccommodationResponse(
            a.Id,
            a.Quantity > 1 ? $"{a.Name} #{unitIndex + 1}" : a.Name,
            a.AccommodationType,
            a.Capacity,
            a.CountByFamily,
            a.ZoneId,
            a.Zone?.Name,
            a.SortOrder,
            [],             // AvailableFeatures: populated by features ticket
            a.Quantity,
            a.Quantity > 1 ? unitIndex : (int?)null
        )
    ))
    .OrderBy(s => s.SortOrder)
    .ThenBy(s => s.Name)
    .ToList();
```

#### 6b. `GetAssignmentStateAsync` — include `UnitIndex` in assignment entries

Replace line 105–107:

**Before:**
```csharp
var assignmentEntries = assignments
    .Select(a => new AssignmentEntry(a.RegistrationId, a.AccommodationId))
    .ToList();
```

**After:**
```csharp
var assignmentEntries = assignments
    .Select(a => new AssignmentEntry(a.RegistrationId, a.AccommodationId, a.UnitIndex))
    .ToList();
```

#### 6c. Update `IAccommodationAssignmentsRepository` interface

Add `unitIndex` parameter to `AssignAsync`:

```csharp
Task AssignAsync(
    Guid proposalId, Guid registrationId, Guid accommodationId,
    int? unitIndex,
    Guid assignedByUserId, CancellationToken ct = default);
```

#### 6d. `AssignAsync` — store `UnitIndex`

Add `int? unitIndex` parameter. In the upsert logic:

```csharp
public async Task AssignAsync(
    Guid proposalId, Guid registrationId, Guid accommodationId,
    int? unitIndex,
    Guid assignedByUserId, CancellationToken ct = default)
{
    var existing = await db.AccommodationAssignments
        .FirstOrDefaultAsync(
            a => a.ProposalId == proposalId && a.RegistrationId == registrationId, ct);

    var now = DateTime.UtcNow;
    if (existing is not null)
    {
        existing.AccommodationId = accommodationId;
        existing.UnitIndex = unitIndex;         // ← NEW
        existing.AssignedByUserId = assignedByUserId;
        existing.UpdatedAt = now;
        db.AccommodationAssignments.Update(existing);
    }
    else
    {
        db.AccommodationAssignments.Add(new AccommodationAssignment
        {
            Id = Guid.NewGuid(),
            ProposalId = proposalId,
            RegistrationId = registrationId,
            AccommodationId = accommodationId,
            UnitIndex = unitIndex,              // ← NEW
            AssignedByUserId = assignedByUserId,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    await StampProposalModifierAsync(proposalId, assignedByUserId, ct);
    await db.SaveChangesAsync(ct);
}
```

#### 6e. `BulkReplaceAsync` — update capacity validation

The existing validation groups assignments by `AccommodationId` and checks total capacity. With `Quantity` and `UnitIndex`, capacity must be enforced **per unit slot**, not across the whole accommodation.

Replace the `foreach (var accGroup in assignments.GroupBy(a => a.AccommodationId))` block:

```csharp
// Validate UnitIndex bounds
foreach (var entry in assignments.Where(a => a.UnitIndex.HasValue))
{
    var acc = accommodations.FirstOrDefault(a => a.Id == entry.AccommodationId);
    if (acc is not null && entry.UnitIndex >= acc.Quantity)
        throw new BusinessRuleException(
            $"El índice de unidad {entry.UnitIndex} no es válido para el alojamiento " +
            $"'{acc.Name}' (máximo: {acc.Quantity - 1}).");
}

// Per-unit capacity validation
foreach (var accGroup in assignments.GroupBy(a => a.AccommodationId))
{
    var acc = accommodations.First(a => a.Id == accGroup.Key);
    if (acc.Capacity is null) continue;

    foreach (var unitGroup in accGroup.GroupBy(a => a.UnitIndex))
    {
        if (acc.CountByFamily)
        {
            if (unitGroup.Count() > acc.Capacity)
                throw new BusinessRuleException(
                    $"La unidad '{acc.Name}'{UnitLabel(unitGroup.Key)} no tiene capacidad " +
                    $"para {unitGroup.Count()} familias (máximo: {acc.Capacity}).");
        }
        else
        {
            var totalPersons = unitGroup.Sum(a => regSizes.GetValueOrDefault(a.RegistrationId, 0));
            if (totalPersons > acc.Capacity)
                throw new BusinessRuleException(
                    $"La unidad '{acc.Name}'{UnitLabel(unitGroup.Key)} no tiene capacidad " +
                    $"para {totalPersons} personas (máximo: {acc.Capacity}).");
        }
    }
}
```

Add private helper at the bottom of the class:

```csharp
private static string UnitLabel(int? unitIndex)
    => unitIndex.HasValue ? $" #{unitIndex + 1}" : string.Empty;
```

Also update the `BulkReplaceAsync` new assignment creation block to include `UnitIndex`:

```csharp
var newAssignments = assignments.Select(a => new AccommodationAssignment
{
    Id = Guid.NewGuid(),
    ProposalId = proposalId,
    RegistrationId = a.RegistrationId,
    AccommodationId = a.AccommodationId,
    UnitIndex = a.UnitIndex,           // ← NEW
    AssignedByUserId = assignedByUserId,
    CreatedAt = now,
    UpdatedAt = now
}).ToList();
```

#### 6f. Update the endpoint handler that calls `AssignAsync`

In `CampsEndpoints.cs`, find the single-assign endpoint. It reads `SingleAssignRequest` and calls `repository.AssignAsync(...)`. Add `unitIndex` from the request:

```csharp
await repository.AssignAsync(
    proposalId,
    registrationId,
    request.AccommodationId,
    request.UnitIndex,          // ← NEW
    userId,
    ct);
```

---

### Step 7: Update reports service — `AccommodationAssignmentReportsService.cs`

This step also fixes a pre-existing issue: `ComputeUsedCapacity` uses a static `ByFamilyTypes` set instead of the `CountByFamily` field on the entity.

#### 7a. Update `AccommodationReportItem` private record — add `Quantity` and `CountByFamily`

```csharp
private record AccommodationReportItem(
    Guid AccommodationId,
    string Name,
    AccommodationType AccommodationType,
    int? Capacity,
    string? ZoneName,
    Guid? ZoneId,
    bool CountByFamily,    // ← NEW (was derived from ByFamilyTypes)
    int Quantity);         // ← NEW
```

#### 7b. Update `LoadAccommodationsAsync` projection — include both new fields

```csharp
private async Task<List<AccommodationReportItem>> LoadAccommodationsAsync(
    Guid campEditionId, CancellationToken ct)
    => await db.CampEditionAccommodations
        .AsNoTracking()
        .Where(a => a.CampEditionId == campEditionId && a.IsActive)
        .Include(a => a.Zone)
        .Select(a => new AccommodationReportItem(
            a.Id,
            a.Name,
            a.AccommodationType,
            a.Capacity,
            a.Zone != null ? a.Zone.Name : null,
            a.ZoneId,
            a.CountByFamily,   // ← NEW
            a.Quantity))       // ← NEW
        .ToListAsync(ct);
```

#### 7c. Update `ComputeGroupCapacity` — multiply by `Quantity`

```csharp
private int ComputeGroupCapacity(List<AccommodationReportItem> accommodations)
    => accommodations.Sum(a => (a.Capacity ?? 0) * a.Quantity);
```

#### 7d. Update `ComputeUsedCapacity` — use `CountByFamily` instead of `ByFamilyTypes`

```csharp
private int ComputeUsedCapacity(
    List<AccommodationReportItem> accommodations,
    List<ReportAssignmentRow> assignments)
{
    var total = 0;
    foreach (var acc in accommodations)
    {
        var accAssignments = assignments.Where(a => a.AccommodationId == acc.AccommodationId).ToList();
        total += acc.CountByFamily                    // ← was: ByFamilyTypes.Contains(acc.AccommodationType)
            ? accAssignments.Count
            : accAssignments.Sum(a => a.MemberCount);
    }
    return total;
}
```

#### 7e. Remove the `ByFamilyTypes` static field

Delete lines 11–12:
```csharp
// DELETE:
private static readonly HashSet<AccommodationType> ByFamilyTypes =
    [AccommodationType.Caravan, AccommodationType.Tent];
```

---

### Step 8: EF Core Migrations

#### Migration 1: `AddQuantityToAccommodations`

```bash
dotnet ef migrations add AddQuantityToAccommodations \
  --project src/Abuvi.API --startup-project src/Abuvi.API
```

Expected generated content — verify the migration contains:

```csharp
migrationBuilder.AddColumn<int>(
    name: "quantity",
    table: "camp_edition_accommodations",
    type: "integer",
    nullable: false,
    defaultValue: 1);

// The check constraint for quantity > 0 will be added by EF from the configuration.
```

No data migration needed — existing rows default to `quantity = 1` (single unit), preserving all existing behaviour.

Apply:
```bash
dotnet ef database update --project src/Abuvi.API --startup-project src/Abuvi.API
```

#### Migration 2: `AddUnitIndexToAccommodationAssignments`

```bash
dotnet ef migrations add AddUnitIndexToAccommodationAssignments \
  --project src/Abuvi.API --startup-project src/Abuvi.API
```

Expected generated content:

```csharp
migrationBuilder.AddColumn<int>(
    name: "unit_index",
    table: "accommodation_assignments",
    type: "integer",
    nullable: true);

migrationBuilder.CreateIndex(
    name: "IX_AccommodationAssignments_Proposal_Accommodation_UnitIndex",
    table: "accommodation_assignments",
    columns: new[] { "proposal_id", "accommodation_id", "unit_index" },
    unique: true,
    filter: "unit_index IS NOT NULL");
```

No data migration needed — existing assignment rows will have `unit_index = null`, which is correct for Quantity = 1 accommodations.

Apply:
```bash
dotnet ef database update --project src/Abuvi.API --startup-project src/Abuvi.API
```

---

### Step 9: Unit Tests — `AccommodationQuantityTests.cs`

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationQuantityTests.cs` *(new)*

Follow the pattern from existing test classes (`AccommodationFeaturesServiceTests.cs`). Use NSubstitute for mocking, FluentAssertions for assertions, xUnit.

```csharp
using Abuvi.API.Features.Camps;
using FluentAssertions;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

public class AccommodationQuantityTests
{
    // ── Slot expansion (tested via GetAssignmentStateAsync logic extracted to a helper) ───

    [Fact]
    public void ExpandSlots_WithQuantity1_ReturnsOneSlotWithNullUnitIndex()
    {
        var accommodation = MakeAccommodation("Cabaña A", quantity: 1);
        var slots = ExpandSlots([accommodation]);
        slots.Should().HaveCount(1);
        slots[0].Name.Should().Be("Cabaña A");
        slots[0].UnitIndex.Should().BeNull();
        slots[0].Quantity.Should().Be(1);
    }

    [Fact]
    public void ExpandSlots_WithQuantity3_Returns3SlotsWithIndexedNames()
    {
        var accommodation = MakeAccommodation("Habitación doble", quantity: 3);
        var slots = ExpandSlots([accommodation]);
        slots.Should().HaveCount(3);
        slots[0].Name.Should().Be("Habitación doble #1");
        slots[0].UnitIndex.Should().Be(0);
        slots[1].Name.Should().Be("Habitación doble #2");
        slots[1].UnitIndex.Should().Be(1);
        slots[2].Name.Should().Be("Habitación doble #3");
        slots[2].UnitIndex.Should().Be(2);
    }

    [Fact]
    public void ExpandSlots_WithMultipleAccommodations_ExpandsEachIndependently()
    {
        var a1 = MakeAccommodation("Habitación doble", quantity: 2);
        var a2 = MakeAccommodation("Suite", quantity: 1);
        var slots = ExpandSlots([a1, a2]);
        slots.Should().HaveCount(3);
        slots.Where(s => s.Id == a1.Id).Should().HaveCount(2);
        slots.Where(s => s.Id == a2.Id).Should().HaveCount(1);
    }

    // ── Capacity calculation ───────────────────────────────────────────────────

    [Fact]
    public void ComputeGroupCapacity_WithQuantity5AndCapacity2_Returns10()
    {
        // Quantity = 5, Capacity = 2 → 5 * 2 = 10
        var items = new[] { MakeReportItem(capacity: 2, quantity: 5, countByFamily: false) };
        ComputeCapacity(items).Should().Be(10);
    }

    [Fact]
    public void ComputeGroupCapacity_WithQuantity1_MatchesLegacyBehavior()
    {
        // Quantity = 1 (default), Capacity = 4 → 1 * 4 = 4
        var items = new[] { MakeReportItem(capacity: 4, quantity: 1, countByFamily: false) };
        ComputeCapacity(items).Should().Be(4);
    }

    [Fact]
    public void ComputeGroupCapacity_WithNullCapacity_CountsZeroPerUnit()
    {
        var items = new[] { MakeReportItem(capacity: null, quantity: 10, countByFamily: true) };
        ComputeCapacity(items).Should().Be(0);
    }

    // ── Validator ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateValidator_WithQuantityLessThan1_Fails(int quantity)
    {
        var validator = new CreateCampEditionAccommodationRequestValidator();
        var request = new CreateCampEditionAccommodationRequest(
            "Test", AccommodationType.Lodge, null, null, null, quantity);
        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }

    [Fact]
    public void CreateValidator_WithQuantity1_Passes()
    {
        var validator = new CreateCampEditionAccommodationRequestValidator();
        var request = new CreateCampEditionAccommodationRequest(
            "Test", AccommodationType.Lodge, null, null, null, 1);
        validator.Validate(request).IsValid.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CampEditionAccommodation MakeAccommodation(string name, int quantity) => new()
    {
        Id = Guid.NewGuid(),
        CampEditionId = Guid.NewGuid(),
        Name = name,
        AccommodationType = AccommodationType.Lodge,
        Quantity = quantity,
        IsActive = true,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FeatureAssignments = []
    };

    // Extract the expansion logic from GetAssignmentStateAsync into a testable static method.
    // If you prefer, test this through the repository integration test instead.
    private static List<AssignmentAccommodationResponse> ExpandSlots(
        IEnumerable<CampEditionAccommodation> accommodations)
        => accommodations
            .SelectMany(a => Enumerable.Range(0, a.Quantity).Select(unitIndex =>
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
                    a.Quantity > 1 ? unitIndex : (int?)null
                )
            ))
            .ToList();

    // Mirrors ComputeGroupCapacity from AccommodationAssignmentReportsService.
    private static int ComputeCapacity(
        IEnumerable<(int? Capacity, int Quantity, bool CountByFamily)> items)
        => items.Sum(a => (a.Capacity ?? 0) * a.Quantity);

    private static (int? Capacity, int Quantity, bool CountByFamily) MakeReportItem(
        int? capacity, int quantity, bool countByFamily)
        => (capacity, quantity, countByFamily);
}
```

**Note on test isolation:** The `ExpandSlots` and `ComputeCapacity` helpers mirror the exact logic that will be in the repository and reports service. If the project prefers testing via `WebApplicationFactory` (integration tests), the same scenarios can be tested against the real HTTP layer. The unit tests above are independent of EF Core.

---

### Step 10: Update Technical Documentation

**Files to update:**

#### `ai-specs/specs/data-model.md`

In the `CampEditionAccommodation` section, add:
```
- quantity: int (default 1, CHECK quantity > 0) — number of physical units of this accommodation type
  available in the zone. When > 1, the assignment board expands it into N independently assignable slots.
```

In the `AccommodationAssignment` section, add:
```
- unitIndex: int? (nullable) — 0-indexed slot number within a multi-unit accommodation.
  null for single-unit accommodations (Quantity = 1). Unique constraint on
  (proposal_id, accommodation_id, unit_index) filtered on NOT NULL prevents double-booking.
```

Also note the capacity formula change:
> TotalCapacity for a group = SUM(accommodation.Capacity * accommodation.Quantity). Previously Quantity was always 1.

#### `ai-specs/specs/api-spec.yml`

Update the following endpoint schemas:
- `POST /api/camps/editions/{editionId}/accommodations` — add `quantity: integer (min 1, default 1)` to request body
- `PUT /api/camps/editions/{editionId}/accommodations/{id}` — add `quantity: integer (min 1)` to request body
- `GET /api/camps/editions/{editionId}/accommodations` — add `quantity: integer` to response
- `GET /api/camps/editions/{editionId}/proposals/{proposalId}/assignment-state`:
  - `accommodations[].quantity: integer`
  - `accommodations[].unitIndex: integer | null`
  - `assignments[].unitIndex: integer | null`
- `POST .../assign` — add `unitIndex: integer | null` to request body

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-accommodation-quantity-backend`
2. **Step 1** — Add `Quantity` to `CampEditionAccommodation` entity + `UnitIndex` to `AccommodationAssignment` entity in `CampsModels.cs`
3. **Step 2** — Update all 6 DTOs/records in `CampsModels.cs`
4. **Run `dotnet build`** — let the compiler surface all positional-record call sites that must be updated (fix them before continuing)
5. **Step 3** — Add EF Core column mappings in both configuration files
6. **Step 4** — Add `Quantity >= 1` validation rules to both accommodation validators
7. **Step 5** — Update `CampEditionAccommodationsService.cs` (Create + Update + ToResponse)
8. **Step 6** — Update `AccommodationAssignmentsRepository.cs` (slot expansion, AssignAsync, BulkReplaceAsync) + update `IAccommodationAssignmentsRepository` interface + update endpoint handler in `CampsEndpoints.cs`
9. **Step 7** — Update `AccommodationAssignmentReportsService.cs` (AccommodationReportItem, LoadAccommodationsAsync, ComputeGroupCapacity, ComputeUsedCapacity, remove ByFamilyTypes)
10. **Step 8** — Generate both migrations + apply (`dotnet ef database update` twice)
11. **Step 9** — Write unit tests, run `dotnet test`
12. **Step 10** — Update `data-model.md` + `api-spec.yml`

---

## Testing Checklist

### Build & compile
- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] All positional-record call sites updated (compiler enforces this)

### Migrations
- [ ] `dotnet ef database update` — both migrations apply cleanly
- [ ] `quantity` column exists in `camp_edition_accommodations` with default = 1
- [ ] `unit_index` column exists in `accommodation_assignments` as nullable
- [ ] Unique filtered index `IX_AccommodationAssignments_Proposal_Accommodation_UnitIndex` created

### Unit tests
- [ ] `dotnet test` — all new tests pass; no regressions in `AutoAssignServiceTests`, `CampEditionAccommodationsServiceTests`

### Manual API verification
- [ ] `GET /api/camps/editions/{editionId}/accommodations` → response includes `"quantity": 1` for existing records
- [ ] `POST .../accommodations` with no `quantity` → defaults to 1
- [ ] `POST .../accommodations` with `quantity: 10` → stored and returned
- [ ] `PUT .../accommodations/{id}` with `quantity: 5` → persisted
- [ ] `POST .../accommodations` with `quantity: 0` → 400 with validation error on `quantity`
- [ ] `GET .../proposals/{id}/assignment-state` for an accommodation with Quantity = 3:
  - Response `accommodations` contains 3 entries with `name: "X #1"`, `"X #2"`, `"X #3"`
  - Each entry has the same `id` (parent accommodation ID) but different `unitIndex` (0, 1, 2)
- [ ] `GET .../assignment-state` for an accommodation with Quantity = 1:
  - Response `accommodations` contains 1 entry; `unitIndex: null`; name unchanged
- [ ] Assignment of family to slot #2 → `assignments` array contains `{accommodationId, unitIndex: 1}`
- [ ] Attempt to assign two families to the same slot → DB unique constraint prevents it → 409 or `BusinessRuleException`
- [ ] Capacity reports (`GetByTypeAsync`, `GetByZoneAsync`) reflect `Quantity × Capacity` in `totalCapacity`

---

## Error Response Format

Standard `ApiResponse<T>` envelope — no new error codes.

| Scenario | HTTP Status |
|---|---|
| `Quantity < 1` in create/update request | 400 Bad Request |
| `UnitIndex` out of range for the accommodation | 400 Bad Request (from `BusinessRuleException`) |
| Duplicate `(proposal, accommodation, unitIndex)` assignment | 409 Conflict (from DB unique constraint) |
| Accommodation not found | 404 Not Found |

---

## Dependencies

No new NuGet packages.

Migration commands:
```bash
dotnet ef migrations add AddQuantityToAccommodations \
  --project src/Abuvi.API --startup-project src/Abuvi.API

dotnet ef database update \
  --project src/Abuvi.API --startup-project src/Abuvi.API

dotnet ef migrations add AddUnitIndexToAccommodationAssignments \
  --project src/Abuvi.API --startup-project src/Abuvi.API

dotnet ef database update \
  --project src/Abuvi.API --startup-project src/Abuvi.API
```

---

## Notes

- **Backward compatibility:** All existing `AccommodationAssignment` rows have `unit_index = NULL` and all `CampEditionAccommodation` rows will default to `quantity = 1`. Both are correct for the single-unit case — no data migration required.
- **Positional records:** `CampEditionAccommodationResponse`, `AssignmentAccommodationResponse`, and `AssignmentEntry` use positional constructor syntax. Adding fields to these records will cause compile errors at all existing call sites — let the compiler guide you rather than manually searching.
- **`ByFamilyTypes` removal:** The `AccommodationAssignmentReportsService` still has the legacy `ByFamilyTypes` static set. Step 7 fixes this as part of adding `Quantity` support, since we're already modifying `AccommodationReportItem`. This brings reports in line with the explicit `CountByFamily` field on the entity.
- **`AutoAssignService`:** Verify `AutoAssignServiceTests` still pass after the changes. The auto-assign logic reads accommodations from the DB and creates `AccommodationAssignment` entities. If `AutoAssignService` calls `repository.BulkReplaceAsync`, the `AssignmentEntry` it constructs must include `UnitIndex`. For auto-assign, `UnitIndex` should be set to `null` (or computed) — check the logic and decide whether auto-assign should distribute families across units.
- **Spanish error messages:** All `BusinessRuleException` messages must be in Spanish (user-facing), matching existing patterns in the codebase.
- **`SingleAssignRequest.UnitIndex` default:** Defaults to `null`, which means single-unit assignments work without any client-side changes for existing Quantity = 1 accommodations.
- All code in English; error messages in Spanish.

---

## Next Steps After Implementation

- Frontend ticket (`feat-accommodation-quantity-frontend`) will add the "Número de unidades" input to `CampEditionAccommodationDialog.vue`, a `×N` badge to the accommodation list, and update slot-family matching in the assignment board to use `(accommodationId, unitIndex)`.

# User Story: Accommodation Quantity per Zone

## Problem

Currently, `CampEditionAccommodation` represents a single physical unit. If a zone has 10 identical double rooms, an admin must create 10 separate accommodation records manually — one per unit. There is no way to define "10 units of type Habitación doble" in a single step.

On the assignment board (*encaje*), the expectation is that each of those 10 rooms appears as an independently assignable slot, with the same name, capacity, and features as the template.

---

## Solution

Add a `Quantity` field (int, min 1, default 1) to `CampEditionAccommodation`. When `Quantity > 1`, the assignment board API expands that record into N virtual slots, each independently assignable. Slot identity is preserved via a `UnitIndex` field added to `AccommodationAssignment`.

---

## Data Model Changes

### `CampEditionAccommodation` — new field

| Column | Type | Default | Constraint |
|---|---|---|---|
| `quantity` | `int NOT NULL` | `1` | `CHECK (quantity > 0)` |

Meaning: the number of physical units of this accommodation type available in the zone.

### `AccommodationAssignment` — new field

| Column | Type | Default | Constraint |
|---|---|---|---|
| `unit_index` | `int NULL` | `null` | `UNIQUE(proposal_id, accommodation_id, unit_index)` filtered on `NOT NULL` |

- `null` for accommodations with `Quantity = 1` (backward compatible with existing data).
- `0`-indexed integer for `Quantity > 1` accommodations.
- The unique filtered index prevents double-booking the same unit within a proposal.

---

## Backend Changes

### Files to modify

| File | Change |
|---|---|
| `CampsModels.cs` | Add `Quantity` to entity + 3 accommodation DTOs; add `Quantity` + `UnitIndex` to `AssignmentAccommodationResponse`; add `UnitIndex` to `AssignmentEntry` |
| `Data/Configurations/CampEditionAccommodationConfiguration.cs` | Map `quantity` column + check constraint |
| `Data/Configurations/AccommodationAssignmentConfiguration.cs` | Map `unit_index` column + unique filtered index |
| `CampEditionAccommodationsService.cs` | Map `Quantity` in Create/Update/ToResponse |
| `AccommodationAssignmentsRepository.cs` | Expand slots by quantity in `GetAssignmentStateAsync`; include `UnitIndex` in `AssignmentEntry` projection |
| `CampsValidators.cs` | Validate `Quantity >= 1`; validate `UnitIndex` is within bounds on assignment |
| `AccommodationAssignmentReportsService.cs` | Update `TotalCapacity` to account for `Quantity` |

### New files

| File | Purpose |
|---|---|
| `Migrations/YYYYMMDDHHMMSS_AddQuantityToAccommodations.cs` | Schema migration |
| `Migrations/YYYYMMDDHHMMSS_AddUnitIndexToAccommodationAssignments.cs` | Schema migration |
| `src/Abuvi.Tests/Unit/Features/Camps/AccommodationQuantityExpansionTests.cs` | Unit tests for slot expansion |

---

### Step 1: `CampEditionAccommodation` entity

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add after `CountByFamily`:

```csharp
public int Quantity { get; set; } = 1;
```

---

### Step 2: DTOs — `CampsModels.cs`

**`CampEditionAccommodationResponse`** — add `Quantity` after `CountByFamily`:

```csharp
public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,
    int Quantity,           // ← NEW
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

**`CreateCampEditionAccommodationRequest`** — add optional `Quantity`:

```csharp
public record CreateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool? CountByFamily = null,
    int Quantity = 1,       // ← NEW
    Guid? ZoneId = null,
    int SortOrder = 0
);
```

**`UpdateCampEditionAccommodationRequest`** — add required `Quantity`:

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

**`AssignmentAccommodationResponse`** — add `Quantity` and `UnitIndex`:

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
    int? UnitIndex          // ← NEW: null when Quantity=1
);
```

**`AssignmentEntry`** — add `UnitIndex`:

```csharp
public record AssignmentEntry(
    Guid RegistrationId,
    Guid AccommodationId,
    int? UnitIndex          // ← NEW
);
```

---

### Step 3: EF Core column mappings

**`CampEditionAccommodationConfiguration.cs`** — add after `CountByFamily`:

```csharp
builder.Property(e => e.Quantity)
    .IsRequired()
    .HasDefaultValue(1)
    .HasColumnName("quantity");

builder.ToTable(t => t.HasCheckConstraint(
    "CK_CampEditionAccommodations_Quantity",
    "quantity > 0"));
```

**`AccommodationAssignmentConfiguration.cs`** — add:

```csharp
builder.Property(e => e.UnitIndex)
    .HasColumnName("unit_index");

builder.HasIndex(e => new { e.ProposalId, e.AccommodationId, e.UnitIndex })
    .IsUnique()
    .HasFilter("unit_index IS NOT NULL");
```

Also add `UnitIndex` property to the `AccommodationAssignment` entity in `CampsModels.cs`:

```csharp
public int? UnitIndex { get; set; }
```

---

### Step 4: Service — `CampEditionAccommodationsService.cs`

**In `CreateAsync`:**

```csharp
var accommodation = new CampEditionAccommodation
{
    // ...existing fields...
    CountByFamily = request.CountByFamily
        ?? request.AccommodationType is AccommodationType.Tent or AccommodationType.Caravan,
    Quantity = request.Quantity,     // ← NEW
    // ...
};
```

**In `UpdateAsync`:**

```csharp
accommodation.Quantity = request.Quantity;   // ← NEW
```

**In `ToResponse`:**

```csharp
=> new(
    a.Id,
    a.CampEditionId,
    a.Name,
    a.AccommodationType,
    a.Description,
    a.Capacity,
    a.CountByFamily,
    a.Quantity,       // ← NEW (must match record constructor position)
    a.IsActive,
    // ...
);
```

---

### Step 5: Assignment repository — `AccommodationAssignmentsRepository.cs`

In `GetAssignmentStateAsync`, replace the single-record accommodation projection with a slot expansion:

```csharp
var accommodationSlots = accommodations
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
            a.FeatureAssignments.Select(fa => fa.FeatureId).ToList().AsReadOnly(),
            a.Quantity,
            a.Quantity > 1 ? unitIndex : (int?)null
        )
    ))
    .OrderBy(s => s.SortOrder)
    .ThenBy(s => s.Name)
    .ToList();
```

Include `UnitIndex` in the assignments projection:

```csharp
var assignmentEntries = assignments.Select(a => new AssignmentEntry(
    a.RegistrationId,
    a.AccommodationId,
    a.UnitIndex           // ← NEW
)).ToList();
```

---

### Step 6: Validation — `CampsValidators.cs`

For `CreateCampEditionAccommodationRequest` and `UpdateCampEditionAccommodationRequest`:

```csharp
RuleFor(x => x.Quantity)
    .GreaterThanOrEqualTo(1)
    .WithMessage("Quantity must be at least 1.");
```

For assignment creation, validate `UnitIndex` range in the endpoint handler or service:

- If `accommodation.Quantity == 1`: `UnitIndex` must be null.
- If `accommodation.Quantity > 1`: `UnitIndex` must be in `[0, Quantity - 1]`.

---

### Step 7: Capacity calculations — `AccommodationAssignmentReportsService.cs`

`TotalCapacity` for a group must account for quantity. Where currently capacity is per-accommodation, now it should be:

```csharp
// If CountByFamily: Quantity * 1 (one family slot per unit)
// If not CountByFamily: Quantity * Capacity (persons per unit)
int slotCapacity = accommodation.CountByFamily
    ? accommodation.Quantity
    : (accommodation.Quantity * (accommodation.Capacity ?? 0));
```

Verify this change also propagates to `ComputeGroupCapacity()` and any methods that sum `TotalCapacity`.

---

### Step 8: EF Core Migrations

**Migration 1:** `AddQuantityToAccommodations`

```bash
dotnet ef migrations add AddQuantityToAccommodations \
  --project src/Abuvi.API --startup-project src/Abuvi.API
```

Expected column:

```csharp
migrationBuilder.AddColumn<int>(
    name: "quantity",
    table: "camp_edition_accommodations",
    nullable: false,
    defaultValue: 1);
```

**Migration 2:** `AddUnitIndexToAccommodationAssignments`

```bash
dotnet ef migrations add AddUnitIndexToAccommodationAssignments \
  --project src/Abuvi.API --startup-project src/Abuvi.API
```

Expected:

```csharp
migrationBuilder.AddColumn<int>(
    name: "unit_index",
    table: "accommodation_assignments",
    nullable: true);

migrationBuilder.CreateIndex(
    name: "IX_accommodation_assignments_proposal_accommodation_unit_index",
    table: "accommodation_assignments",
    columns: new[] { "proposal_id", "accommodation_id", "unit_index" },
    unique: true,
    filter: "unit_index IS NOT NULL");
```

Apply:

```bash
dotnet ef database update --project src/Abuvi.API --startup-project src/Abuvi.API
```

---

### Step 9: Unit Tests — `AccommodationQuantityExpansionTests.cs`

```csharp
[Fact]
public void GetAssignmentStateAsync_WithQuantity3_ExpandsInto3Slots()
{
    // One accommodation with Quantity = 3
    // Should yield 3 AssignmentAccommodationResponse entries:
    //   "Habitación doble #1" (UnitIndex=0)
    //   "Habitación doble #2" (UnitIndex=1)
    //   "Habitación doble #3" (UnitIndex=2)
}

[Fact]
public void GetAssignmentStateAsync_WithQuantity1_RetainsOriginalNameAndNullUnitIndex()
{
    // Quantity=1: name unchanged, UnitIndex=null
}

[Fact]
public void TotalCapacity_WithQuantity5AndCapacity2_Returns10()
{
    // CountByFamily=false, Quantity=5, Capacity=2 → TotalCapacity=10
}

[Fact]
public void TotalCapacity_WithCountByFamilyAndQuantity5_Returns5()
{
    // CountByFamily=true, Quantity=5 → TotalCapacity=5
}
```

---

## Frontend Changes

### Files to modify

| File | Change |
|---|---|
| `frontend/src/types/accommodation-assignment.ts` | Add `quantity`, `unitIndex` to `AssignmentAccommodationResponse`; add `unitIndex` to `AssignmentEntry` |
| `frontend/src/types/camp-edition.ts` | Add `quantity` to `CampEditionAccommodationResponse` |
| `frontend/src/components/camps/CampEditionAccommodationDialog.vue` | Add "Número de unidades" input field |
| `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` | Show quantity badge (`×N`) in accommodation list items |
| `frontend/src/views/camps/AccommodationAssignmentView.vue` | Slot-family matching now uses `(accommodationId, unitIndex)` |

---

### Step A: TypeScript types

**`accommodation-assignment.ts`:**

```typescript
export interface AssignmentAccommodationResponse {
  id: string
  name: string
  type: AccommodationTypeValue
  capacity: number | null
  countByFamily: boolean
  zoneId: string | null
  zoneName: string | null
  sortOrder: number
  availableFeatures: string[]
  quantity: number          // NEW
  unitIndex: number | null  // NEW: null when quantity === 1
}

export interface AssignmentEntry {
  registrationId: string
  accommodationId: string
  unitIndex: number | null  // NEW
}
```

**`camp-edition.ts`** (wherever `CampEditionAccommodationResponse` is defined):

```typescript
export interface CampEditionAccommodationResponse {
  // ...existing fields...
  quantity: number   // NEW
}
```

---

### Step B: `CampEditionAccommodationDialog.vue`

Add a number input for `quantity` between the `capacity` field and the features section:

```vue
<div class="form-field">
  <label>Número de unidades</label>
  <input type="number" v-model.number="form.quantity" min="1" />
  <span class="hint">
    Cuántas unidades físicas de este tipo hay disponibles en la zona.
  </span>
</div>
```

- Default: `1`.
- Show a warning if `quantity > 1` and `countByFamily = false` (edge case: shared rooms with quantity is unusual but valid).
- On submit, include `quantity` in the Create/Update request payload.

---

### Step C: `CampEditionAccommodationsPanel.vue`

In the accommodation list item, show a badge when `quantity > 1`:

```vue
<span v-if="accommodation.quantity > 1" class="badge">
  ×{{ accommodation.quantity }}
</span>
```

---

### Step D: `AccommodationAssignmentView.vue` (or `AccommodationAssignmentPanel.vue`)

The backend now returns `assignments` with `unitIndex`. When determining which families are in a given slot, match on both `accommodationId` and `unitIndex`:

```typescript
function getFamiliesForSlot(slot: AssignmentAccommodationResponse): AssignmentFamilyResponse[] {
  return assignments.value
    .filter(a => a.accommodationId === slot.id && a.unitIndex === slot.unitIndex)
    .map(a => familiesMap.value[a.registrationId])
    .filter(Boolean)
}
```

When assigning a family to a slot, include `unitIndex` in the request body:

```typescript
async function assignFamily(slot: AssignmentAccommodationResponse, family: AssignmentFamilyResponse) {
  await api.createAssignment({
    registrationId: family.registrationId,
    accommodationId: slot.id,
    unitIndex: slot.unitIndex,   // NEW
  })
}
```

---

## API Endpoints Summary

| Endpoint | Change |
|---|---|
| `POST /api/camps/editions/{editionId}/accommodations` | Request body gains `quantity: int` (default 1) |
| `PUT /api/camps/editions/{editionId}/accommodations/{id}` | Request body gains `quantity: int` (required) |
| `GET /api/camps/editions/{editionId}/accommodations` | Response gains `quantity` |
| `GET /api/camps/editions/{editionId}/proposals/{proposalId}/assignment-state` | `accommodations` expanded by quantity; gains `quantity`, `unitIndex`; `assignments` gains `unitIndex` |
| `POST /api/camps/editions/{editionId}/proposals/{proposalId}/assignments` | Request body gains `unitIndex: int?` |

---

## Validation Rules

| Rule | Where |
|---|---|
| `Quantity >= 1` | `CampsValidators.cs` + frontend form min |
| `UnitIndex` in `[0, Quantity-1]` when not null | Assignment service/endpoint handler |
| `UnitIndex` null when `Quantity = 1` | Assignment service/endpoint handler |
| No two families assigned to same `(proposalId, accommodationId, unitIndex)` | Unique DB index + 409 conflict response |

---

## Non-Functional Requirements

- **Performance:** Slot expansion happens in-memory after the DB query. Max realistic `Quantity` is ~50 (e.g., 50 bungalows). Expansion is O(N) and adds no DB roundtrips.
- **Backward compatibility:** All existing `AccommodationAssignment` rows have `UnitIndex = null`. These rows remain valid and map correctly to accommodations with `Quantity = 1`.
- **Positional records:** Adding fields to `AssignmentAccommodationResponse`, `AssignmentEntry`, and accommodation request/response records requires updating all call sites simultaneously.

---

## Acceptance Criteria

- [ ] Admin can create or edit an accommodation with `Quantity ≥ 1` (default 1 preserves current behavior)
- [ ] Accommodation list shows `×N` badge when `Quantity > 1`
- [ ] Assignment board shows N individually named slots (`#1` … `#N`) for an accommodation with `Quantity = N`
- [ ] Each slot is independently assignable to one family (if `CountByFamily`) or up to `Capacity` people
- [ ] All N slots inherit the same features and capacity from the parent accommodation record
- [ ] Two families cannot be assigned to the same slot within the same proposal (enforced by DB unique index)
- [ ] Capacity totals in reports reflect `Quantity × per-unit-capacity`
- [ ] Existing accommodations (Quantity = 1) continue to work without any data migration

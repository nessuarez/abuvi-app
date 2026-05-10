# Backend Implementation Plan: feat-assignment-board-v2 — Assignment Board Enhancements v2

## Overview

This ticket enriches the accommodation assignment board with compatibility signals, family/accommodation panel filters, a scoring-based auto-assign algorithm, and last-modifier traceability on proposals.

The changes are **purely additive**: new fields on existing DTOs, a nullable column on `AccommodationAssignmentProposal`, and logic changes inside `AutoAssignService`. No existing API contracts break. The feature relies on two already-merged tickets:

- **Ticket B (feat-family-needs-tagging):** `RegistrationAccommodationNeed` entity with `AccommodationFeatureId` is in `dev`. `RegistrationFriendLink` bidirectional links are also in `dev`.
- **Ticket A (feat-accommodation-features):** `CampEditionAccommodation` may have a `Features` collection (`ICollection<AccommodationFeature>`). If that relationship does not yet exist in `dev`, `AvailableFeatures` is populated as an empty list and the system continues to function — scoring simply scores 0 for features.

Architecture: Vertical Slice inside `src/Abuvi.API/Features/Camps/`.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

| File | Change type |
|------|-------------|
| `CampsModels.cs` | Extend 3 records |
| `AccommodationAssignmentsRepository.cs` | Enrich state builder + proposal list query + write ops |
| `AutoAssignService.cs` | Add `ScoreAccommodation`, modify `Compute` fallback |
| `Data/Configurations/AccommodationAssignmentProposalConfiguration.cs` | New column |
| `Migrations/` | New migration |
| `Abuvi.Tests/Unit/Features/Camps/AutoAssignServiceTests.cs` | New test cases |

**Untracked dependency to verify:** whether `CampEditionAccommodation` already has an `AccommodationFeatures` navigation (Ticket A). Check `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs` before Step 4.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action:** Create and switch to the feature branch.
- **Implementation Steps:**
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-assignment-board-v2-backend`
  3. `git branch` — confirm you are on the new branch.
- **Notes:** The worktree at `c:\repos\abuvi-app.worktrees\feat-family-needs-tagging-backend` is already occupied. Work directly in `c:\repos\abuvi-app` on the new branch, or create a new worktree.

---

### Step 1: Add `LastModifiedByUserId` to `AccommodationAssignmentProposal` Entity

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

**Action:** Add a nullable property to the entity class.

```csharp
public class AccommodationAssignmentProposal
{
    // ... existing fields ...
    public Guid? LastModifiedByUserId { get; set; }   // NEW
}
```

**File:** `src/Abuvi.API/Data/Configurations/AccommodationAssignmentProposalConfiguration.cs`

**Action:** Register the new column in the EF configuration (append inside `Configure`):

```csharp
builder.Property(p => p.LastModifiedByUserId)
    .HasColumnName("last_modified_by_user_id");
```

---

### Step 2: EF Core Migration

**Action:** Generate and verify the migration.

```bash
cd src/Abuvi.API
dotnet ef migrations add AddLastModifiedByUserIdToProposal
```

Verify the generated migration adds a single nullable `last_modified_by_user_id uuid` column to `accommodation_assignment_proposals`. No `Down()` changes needed beyond dropping the column.

---

### Step 3: Extend DTOs in `CampsModels.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

#### 3a. `AssignmentFamilyResponse`

Replace the existing record with:

```csharp
public record AssignmentFamilyResponse(
    Guid RegistrationId,
    Guid FamilyUnitId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    int AdultCount,
    int ChildCount,
    bool HasPet,
    string? SpecialNeeds,
    string? CampatesPreference,
    IReadOnlyList<AccommodationPreferenceItem> AccommodationPreferences,
    // NEW:
    bool HasSpecialNeeds,
    IReadOnlyList<Guid> RequiredFeatures,
    IReadOnlyList<Guid> FriendlyFamilyUnitIds
);
```

- `HasSpecialNeeds` is derived: `SpecialNeeds is { Length: > 0 }`.
- `RequiredFeatures` = list of `AccommodationFeatureId` values from `RegistrationAccommodationNeed`.
- `FriendlyFamilyUnitIds` = resolved family unit IDs from the registration's friend links (see Step 4).

#### 3b. `AssignmentAccommodationResponse`

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
    // NEW:
    IReadOnlyList<Guid> AvailableFeatures
);
```

- `AvailableFeatures` = list of `AccommodationFeature.Id` values linked to the `CampEditionAccommodation`. If Ticket A's relationship is not yet in place, pass `[]` here.

#### 3c. `AccommodationAssignmentProposalSummaryResponse`

```csharp
public record AccommodationAssignmentProposalSummaryResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    string? Notes,
    bool IsActive,
    int AssignmentCount,
    int UnassignedCount,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // NEW:
    string? LastModifiedByUserName
);
```

---

### Step 4: Enrich `GetAssignmentStateAsync` in `AccommodationAssignmentsRepository.cs`

**File:** `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs`

This method currently loads registrations, accommodations, assignments, and builds the state. Extend it as follows:

#### 4a. Load accommodation needs and friend links for registrations

```csharp
var registrations = await db.Registrations
    .AsNoTracking()
    .Where(r => r.CampEditionId == campEditionId && r.Status != RegistrationStatus.Cancelled)
    .Include(r => r.FamilyUnit)
    .Include(r => r.Members)
    .Include(r => r.AccommodationPreferences)
    .Include(r => r.AccommodationNeeds)        // NEW — RegistrationAccommodationNeed
    .ToListAsync(ct);
```

#### 4b. Build friend-link resolution map

After loading registrations, build a map from `RegistrationId → FamilyUnitId` for all registrations in this camp edition, then load friend links:

```csharp
// All registrations in this edition by their ID (for friend-link resolution)
var registrationFamilyMap = registrations.ToDictionary(r => r.Id, r => r.FamilyUnitId);

// Load friend links for these registrations (both directions, since they are bidirectional)
var regIds = registrations.Select(r => r.Id).ToHashSet();
var friendLinks = await db.RegistrationFriendLinks
    .AsNoTracking()
    .Where(fl => regIds.Contains(fl.RegistrationId))
    .ToListAsync(ct);

// registrationId → list of friendly FamilyUnitIds
var friendlyFamilyMap = friendLinks
    .GroupBy(fl => fl.RegistrationId)
    .ToDictionary(
        g => g.Key,
        g => g
            .Select(fl => registrationFamilyMap.GetValueOrDefault(fl.LinkedRegistrationId))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() as IReadOnlyList<Guid>
    );
```

> **Note on entity name:** Ticket B may have named the entity `RegistrationFriendLink` with properties `RegistrationId` and `LinkedRegistrationId`. Verify in `RegistrationsModels.cs`. The DbSet name on `AppDbContext` will be `RegistrationFriendLinks` (or similar). Adjust accordingly.

#### 4c. Load accommodation features (conditional on Ticket A)

```csharp
var accommodations = await db.CampEditionAccommodations
    .AsNoTracking()
    .Where(a => a.CampEditionId == campEditionId && a.IsActive)
    .Include(a => a.Zone)
    .Include(a => a.Features)    // NEW — only if Ticket A added this navigation
    .OrderBy(a => a.SortOrder)
    .ThenBy(a => a.Name)
    .ToListAsync(ct);
```

> If `CampEditionAccommodation` does not yet have a `Features` navigation property, skip the `Include` and pass `[]` in the mapping below.

#### 4d. Update the family mapping

```csharp
return new AssignmentFamilyResponse(
    r.Id,
    r.FamilyUnitId,
    r.FamilyUnit.Name,
    repName,
    r.Members.Count,
    adultCount,
    childCount + babyCount,
    r.HasPet,
    r.SpecialNeeds,
    r.CampatesPreference,
    r.AccommodationPreferences
        .OrderBy(p => p.PreferenceOrder)
        .Select(p => new AccommodationPreferenceItem(p.CampEditionAccommodationId, p.PreferenceOrder))
        .ToList(),
    // NEW:
    r.SpecialNeeds is { Length: > 0 },
    r.AccommodationNeeds.Select(n => n.AccommodationFeatureId).ToList(),
    friendlyFamilyMap.GetValueOrDefault(r.Id, [])
);
```

#### 4e. Update the accommodation mapping

```csharp
new AssignmentAccommodationResponse(
    a.Id,
    a.Name,
    a.AccommodationType,
    a.Capacity,
    a.CountByFamily,
    a.ZoneId,
    a.Zone?.Name,
    a.SortOrder,
    // NEW (guard for Ticket A not yet merged):
    a.Features?.Select(f => f.Id).ToList() ?? []
)
```

---

### Step 5: Add `LastModifiedByUserName` to Proposal List Query

**File:** `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs`

Find the method that returns `IReadOnlyList<AccommodationAssignmentProposalSummaryResponse>` (likely `GetProposalsAsync` or similar). It currently projects without the last-modifier. Extend it:

```csharp
// Add a join/lookup on LastModifiedByUserId
var proposals = await db.AccommodationAssignmentProposals
    .AsNoTracking()
    .Where(p => p.CampEditionId == campEditionId)
    // ... existing filters ...
    .ToListAsync(ct);

// Collect user IDs to resolve names
var userIds = proposals
    .Where(p => p.LastModifiedByUserId.HasValue)
    .Select(p => p.LastModifiedByUserId!.Value)
    .Distinct()
    .ToList();

var users = await db.Users
    .AsNoTracking()
    .Where(u => userIds.Contains(u.Id))
    .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct);

// Map
return proposals.Select(p =>
{
    // ... existing assignment counts ...
    var lastModifiedName = p.LastModifiedByUserId.HasValue
        ? users.GetValueOrDefault(p.LastModifiedByUserId.Value)
        : null;

    return new AccommodationAssignmentProposalSummaryResponse(
        p.Id, p.CampEditionId, p.Name, p.Notes, p.IsActive,
        assignmentCount, unassignedCount,
        p.CreatedByUserId, p.CreatedAt, p.UpdatedAt,
        lastModifiedName   // NEW
    );
}).ToList();
```

> **Implementation note:** The current query structure may differ. Read the full method before modifying. The pattern of loading users separately (as done in `GetAssignmentStateAsync`) is preferred over a JOIN for clarity.

---

### Step 6: Propagate `LastModifiedByUserId` on Write Operations

**File:** `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs`

Each write operation must load the proposal and stamp `LastModifiedByUserId`. Apply this to all three write methods:

#### `AssignAsync`

```csharp
public async Task AssignAsync(
    Guid proposalId, Guid registrationId, Guid accommodationId,
    Guid assignedByUserId, CancellationToken ct = default)
{
    // ... existing logic to create AccommodationAssignment ...

    // NEW: stamp proposal
    var proposal = await db.AccommodationAssignmentProposals
        .FirstOrDefaultAsync(p => p.Id == proposalId, ct);
    if (proposal is not null)
    {
        proposal.LastModifiedByUserId = assignedByUserId;
        proposal.UpdatedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync(ct);
}
```

#### `UnassignAsync`

Currently uses `ExecuteDeleteAsync` (no `SaveChangesAsync`). Change to load-and-delete pattern so the proposal can be stamped:

```csharp
public async Task UnassignAsync(
    Guid proposalId, Guid registrationId,
    Guid modifiedByUserId,   // NEW param
    CancellationToken ct = default)
{
    var assignment = await db.AccommodationAssignments
        .FirstOrDefaultAsync(a => a.ProposalId == proposalId && a.RegistrationId == registrationId, ct);
    if (assignment is not null) db.AccommodationAssignments.Remove(assignment);

    var proposal = await db.AccommodationAssignmentProposals
        .FirstOrDefaultAsync(p => p.Id == proposalId, ct);
    if (proposal is not null)
    {
        proposal.LastModifiedByUserId = modifiedByUserId;
        proposal.UpdatedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync(ct);
}
```

> **Important:** Update the calling service method `AccommodationAssignmentsService.UnassignAsync` to pass `userId` down. The endpoint must obtain the userId from the `ClaimsPrincipal` (already done for `AssignAsync`; replicate that pattern).

#### `BulkReplaceAsync`

```csharp
// At the end, before SaveChangesAsync:
var proposal = await db.AccommodationAssignmentProposals
    .FirstOrDefaultAsync(p => p.Id == proposalId, ct);
if (proposal is not null)
{
    proposal.LastModifiedByUserId = assignedByUserId;
    proposal.UpdatedAt = DateTime.UtcNow;
}

await db.SaveChangesAsync(ct);
```

---

### Step 7: Update `AutoAssignService` — Scoring Algorithm

**File:** `src/Abuvi.API/Features/Camps/AutoAssignService.cs`

#### 7a. Add `ScoreAccommodation` static method

Append to the class:

```csharp
private static int ScoreAccommodation(
    AssignmentAccommodationResponse acc,
    AssignmentFamilyResponse family,
    Dictionary<Guid, List<Guid>> occupancy,
    Dictionary<Guid, int> sizeMap,
    IReadOnlyDictionary<Guid, Guid> registrationToFamilyUnit,
    ProposalAssignmentStateResponse state)
{
    var score = 0;

    // +5 per required feature covered by this accommodation
    score += family.RequiredFeatures.Count(req => acc.AvailableFeatures.Contains(req)) * 5;

    // +15 if a friendly family is already assigned to this exact accommodation
    foreach (var assignedRegId in occupancy[acc.Id])
    {
        if (!registrationToFamilyUnit.TryGetValue(assignedRegId, out var fuId)) continue;
        if (family.FriendlyFamilyUnitIds.Contains(fuId)) score += 15;
    }

    // +10 if a friendly family is in another accommodation of the same zone
    if (acc.ZoneId.HasValue)
    {
        var sameZoneAccIds = state.Accommodations
            .Where(a => a.ZoneId == acc.ZoneId && a.Id != acc.Id)
            .Select(a => a.Id)
            .ToHashSet();

        var bonusGiven = false;
        foreach (var sameZoneAccId in sameZoneAccIds)
        {
            if (bonusGiven) break;
            if (!occupancy.TryGetValue(sameZoneAccId, out var sameZoneRegIds)) continue;
            foreach (var regId in sameZoneRegIds)
            {
                if (!registrationToFamilyUnit.TryGetValue(regId, out var fuId)) continue;
                if (!family.FriendlyFamilyUnitIds.Contains(fuId)) continue;
                score += 10;
                bonusGiven = true;
                break;
            }
        }
    }

    return score;
}
```

#### 7b. Modify `Compute` — build lookup maps and replace fallback selection

At the top of `Compute`, after building `sizeMap`:

```csharp
// NEW: maps for scoring
var registrationToFamilyUnit = state.Families.ToDictionary(
    f => f.RegistrationId, f => f.FamilyUnitId);
```

Replace the fallback block:

```csharp
// BEFORE:
var fallback = state.Accommodations
    .Where(acc => HasCapacity(acc, occupancy[acc.Id], family.MemberCount, sizeMap))
    .OrderBy(acc => GetRemainingCapacity(acc, occupancy[acc.Id], sizeMap))
    .FirstOrDefault();

// AFTER:
var fallback = state.Accommodations
    .Where(acc => HasCapacity(acc, occupancy[acc.Id], family.MemberCount, sizeMap))
    .OrderByDescending(acc => ScoreAccommodation(
        acc, family, occupancy, sizeMap, registrationToFamilyUnit, state))
    .ThenBy(acc => GetRemainingCapacity(acc, occupancy[acc.Id], sizeMap))
    .FirstOrDefault();
```

The preference-first loop (Fase 1) remains unchanged — declared preferences are still the primary criterion; scoring only applies to the fallback selection.

---

### Step 8: Unit Tests for `AutoAssignService`

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AutoAssignServiceTests.cs`

#### Update `MakeFamily` and `MakeAccommodation` helpers

The helpers must now pass values for the new DTO fields:

```csharp
private static AssignmentFamilyResponse MakeFamily(
    Guid? id = null,
    int memberCount = 2,
    IEnumerable<(Guid accommodationId, int order)>? preferences = null,
    IEnumerable<Guid>? requiredFeatures = null,
    IEnumerable<Guid>? friendlyFamilyUnitIds = null)
{
    var regId = id ?? Guid.NewGuid();
    var prefs = (preferences ?? [])
        .Select(p => new AccommodationPreferenceItem(p.accommodationId, p.order))
        .ToList();
    return new AssignmentFamilyResponse(
        regId, Guid.NewGuid(), "Familia Test", "Rep Test",
        memberCount, memberCount, 0, false, null, null, prefs,
        false,                                         // HasSpecialNeeds
        requiredFeatures?.ToList() ?? [],
        friendlyFamilyUnitIds?.ToList() ?? []);
}

private static AssignmentAccommodationResponse MakeAccommodation(
    Guid? id = null,
    int? capacity = 4,
    AccommodationType type = AccommodationType.Lodge,
    bool countByFamily = false,
    Guid? zoneId = null,
    string? zoneName = null,
    IEnumerable<Guid>? availableFeatures = null)
    => new(id ?? Guid.NewGuid(), "Alojamiento Test", type, capacity, countByFamily,
        zoneId, zoneName, 0, availableFeatures?.ToList() ?? []);
```

#### New test cases to add

```
ScoreAccommodation_AllFeaturesMatch_Returns5PerFeature
ScoreAccommodation_FriendlyFamilyInSameAccommodation_Returns15
ScoreAccommodation_FriendlyFamilyInSameZone_Returns10
ScoreAccommodation_NoMatchingFeaturesOrFriendlyFamilies_Returns0
ScoreAccommodation_MultipleFeatures_AccumulatesPoints
ScoreAccommodation_MultipleFriendlyFamiliesInSameAccommodation_AccumulatesBonus
ScoreAccommodation_FriendlyFamilyInZone_NoBonusIfAlreadyInSameAccommodation
Compute_PrefersAccommodationWithFriendlyFamily_InFallback
Compute_PrefersAccommodationCoveringRequiredFeatures_InFallback
Compute_WithFeaturesAndFriendlyFamilies_SelectsHighestScoredFallback
Compute_EmptyRequiredFeaturesAndNoFriends_BehavesLikePreviousAlgorithm
```

Follow the existing AAA pattern. The `ScoreAccommodation` method is private, so test it indirectly through `Compute` — create a controlled state with one accommodation having features that match and verify it is preferred over one that doesn't.

---

### Step 9: Update Technical Documentation

**Action:** After implementation, update:

1. **`ai-specs/specs/api-spec.yml`** — add `hasSpecialNeeds`, `requiredFeatures`, `friendlyFamilyUnitIds` to `AssignmentFamilyResponse` schema; add `availableFeatures` to `AssignmentAccommodationResponse`; add `lastModifiedByUserName` to `AccommodationAssignmentProposalSummaryResponse`.
2. **`ai-specs/specs/data-model.md`** — add `last_modified_by_user_id` column to `accommodation_assignment_proposals` table description.

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Entity field + EF config
3. Step 2 — Migration
4. Step 3 — Extend DTOs
5. Step 4 — Enrich `GetAssignmentStateAsync`
6. Step 5 — Proposal list query with `LastModifiedByUserName`
7. Step 6 — Propagate `LastModifiedByUserId` in write ops
8. Step 7 — `AutoAssignService` scoring
9. Step 8 — Unit tests
10. Step 9 — Documentation

---

## Testing Checklist

- [ ] `GetAssignmentStateAsync` returns `RequiredFeatures` populated from `RegistrationAccommodationNeed.AccommodationFeatureId`
- [ ] `GetAssignmentStateAsync` returns `FriendlyFamilyUnitIds` resolved through `RegistrationFriendLink`
- [ ] `GetAssignmentStateAsync` returns `AvailableFeatures` from accommodation features (or `[]` if Ticket A not yet integrated)
- [ ] `GetProposalsAsync` (or equivalent) returns `LastModifiedByUserName` after any write
- [ ] `AssignAsync`, `BulkReplaceAsync`, `UnassignAsync` each stamp `LastModifiedByUserId` on the proposal
- [ ] `AutoAssignService.Compute` — with matching features and friendly family, selects the higher-scored accommodation in fallback
- [ ] `AutoAssignService.Compute` — with empty `RequiredFeatures` and `FriendlyFamilyUnitIds`, output is identical to the previous algorithm
- [ ] All new xUnit tests pass; no existing tests broken

---

## Error Response Format

No new endpoints are introduced. All existing error codes remain. The `UnassignAsync` signature change (new `modifiedByUserId` param) must be propagated through the service layer to the endpoint — the endpoint already has access to `HttpContext.User`; extract the user ID with `user.GetUserId()` (or equivalent helper used elsewhere in the Camps endpoints).

---

## Dependencies

- No new NuGet packages.
- EF Core migration command:
  ```bash
  cd src/Abuvi.API
  dotnet ef migrations add AddLastModifiedByUserIdToProposal --project . --startup-project .
  dotnet ef database update
  ```

---

## Notes

- **Verify Ticket A integration before Step 4:** Check `CampEditionAccommodationConfiguration.cs` for a `HasMany(... Features ...)` relationship. If absent, skip the `Include(a => a.Features)` and pass `[]` for `AvailableFeatures` — the frontend and scoring will degrade gracefully.
- **Entity name for friend links:** Confirm the exact class name and property names in `RegistrationsModels.cs` (likely `RegistrationFriendLink` with `RegistrationId` / `LinkedRegistrationId`). The Ticket B migration `20260504073310_AddRegistrationAccommodationNeedsAndFriendLinks` confirms both are in `dev`.
- **`UnassignAsync` breaking change:** Adding `modifiedByUserId` to the method signature is a backend-internal change (the endpoint layer calls it). No API shape changes.
- **Scoring is only for fallback:** Declared preferences are still honoured in order. Scoring is the tiebreaker only when no preference has capacity.
- **All code in English** per `base-standards.mdc`.
- **Branch:** `feature/feat-assignment-board-v2-backend` from `dev`. PR target: `dev`.

---

## Next Steps After Implementation

- Merge backend PR to `dev`.
- Frontend ticket (`feat-assignment-board-v2` frontend) can then be planned and implemented; it consumes the new DTO fields.

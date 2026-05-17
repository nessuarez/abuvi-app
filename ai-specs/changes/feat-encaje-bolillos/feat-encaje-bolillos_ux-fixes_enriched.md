# Encaje de Bolillos — UX Fixes & Enhancements Round 2

## Summary

Six issues found after the `ux-improvements` implementation. Three are bugs (gallery 405, missing preference icons, zone thumbnail fallback), two are UX tweaks (hide empty ProgressBar, fuller selected-family detail), and one is a new feature (dynamic accommodation-feature filter chips). All build on the code already in `feat-encaje-bolillos_ux-improvements_*.md`.

---

## Fix 1 — Zone gallery returns HTTP 405

### Root cause

`AccommodationAssignmentPanel.vue` calls:

```
GET /api/camps/editions/{campEditionId}/accommodation-zones/{zoneId}
```

The backend `zonesGroup` at `CampsEndpoints.cs` only registers:

| Method | Path |
|--------|------|
| `GET` | `/` |
| `POST` | `/` |
| `PUT` | `/{zoneId}` |
| `DELETE` | `/{zoneId}` |
| `PATCH` | `/{zoneId}/accommodations` |

There is no `GET /{zoneId}` route. The framework matches `/{zoneId}` to the route group path but finds no handler for `GET` → **405 Method Not Allowed**.

### Solution

Add a `GET /{zoneId}` endpoint that returns the full `AccommodationZoneResponse` (including `mediaItems`).

#### Backend

**File:** `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`

Register the route after `MapGet("/", ...)`:

```csharp
zonesGroup.MapGet("/{zoneId:guid}", GetZoneById)
    .WithName("GetAccommodationZoneById")
    .WithSummary("Get a single accommodation zone by ID (includes media items)")
    .Produces<ApiResponse<AccommodationZoneResponse>>()
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden);
```

Add handler (alongside the other zone handlers):

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

**File:** `src/Abuvi.API/Features/Camps/AccommodationZonesService.cs`

Add `GetByIdAsync`:

```csharp
public async Task<AccommodationZoneResponse> GetByIdAsync(Guid campEditionId, Guid zoneId, CancellationToken ct)
{
    var zone = await _db.AccommodationZones
        .Include(z => z.Accommodations)
        .Include(z => z.Features)
        .Include(z => z.MediaItems)
        .FirstOrDefaultAsync(z => z.Id == zoneId && z.CampEditionId == campEditionId, ct)
        ?? throw new NotFoundException($"Zone {zoneId} not found");
    return zone.ToResponse();
}
```

No migration needed.

#### Acceptance criteria

- [ ] `GET /api/camps/editions/{campEditionId}/accommodation-zones/{zoneId}` returns 200 + full zone DTO including `mediaItems`
- [ ] Returns 404 when `zoneId` does not exist or belongs to a different edition
- [ ] Zone gallery modal opens and loads photos correctly

---

## Fix 2 — Preference type icons missing in `FamilyAssignmentCard`

### Root cause

`AccommodationAssignmentPanel` builds `accommodationTypeMap` from `state.accommodations`. The backend filters `state.accommodations` to **only `IsAssignable = true`** entries.

Families may have stated preferences for non-assignable accommodations (e.g., a type-level "Albergue" placeholder they selected before it was marked non-assignable). Those accommodation IDs are not in the map → `prefTypeIcon()` returns `'pi pi-question'` and `prefTypeLabel()` returns `'?'`.

### Solution

Include all accommodations (assignable and non-assignable) in the state response for reference, but continue showing only assignable ones in the assignment grid. The `accommodationTypeMap` built in the panel will then cover every accommodation ID that families may reference.

**Backend** — `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs`

Add an `AllAccommodations` field that returns the full list (no `IsAssignable` filter), separate from the filtered `Accommodations` used in the grid:

Change `ProposalAssignmentStateResponse` to carry both:

```csharp
// CampsModels.cs
public record ProposalAssignmentStateResponse(
    Guid ProposalId,
    IReadOnlyList<AssignmentFamilyResponse> Families,
    IReadOnlyList<AssignmentAccommodationResponse> Accommodations,        // IsAssignable = true only
    IReadOnlyList<AssignmentEntry> Assignments,
    IReadOnlyList<AccommodationTypeLookupItem> AccommodationTypeLookup    // ADD: all IDs → type, for preference display
);

public record AccommodationTypeLookupItem(Guid Id, string Type);
```

Populate `AccommodationTypeLookup` in the repository from the unfiltered accommodation list (just id + type — no need to load full data):

```csharp
var allAccommodations = await _db.CampEditionAccommodations
    .Where(a => a.CampEditionId == campEditionId && a.IsActive)
    .Select(a => new AccommodationTypeLookupItem(a.Id, a.Type.ToString()))
    .ToListAsync(ct);
```

**Frontend** — `frontend/src/types/accommodation-assignment.ts`

```typescript
export interface AccommodationTypeLookupItem {
  id: string
  type: AccommodationTypeValue
}

// Update ProposalAssignmentStateResponse:
export interface ProposalAssignmentStateResponse {
  proposalId: string
  families: AssignmentFamilyResponse[]
  accommodations: AssignmentAccommodationResponse[]
  assignments: AssignmentEntry[]
  accommodationTypeLookup: AccommodationTypeLookupItem[]   // ADD
}
```

**Frontend** — `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

Update `accommodationTypeMap` to also include `accommodationTypeLookup` entries:

```typescript
const accommodationTypeMap = computed((): Map<string, AccommodationTypeValue> => {
  const map = new Map<string, AccommodationTypeValue>()
  props.state.accommodations.forEach((a) => map.set(a.id, a.type))
  // Also include non-assignable accommodations referenced in family preferences
  props.state.accommodationTypeLookup.forEach((item) => map.set(item.id, item.type))
  return map
})
```

#### Acceptance criteria

- [ ] `ProposalAssignmentStateResponse` includes `accommodationTypeLookup` with all active accommodations (not just assignable ones)
- [ ] Preference pills in `FamilyAssignmentCard` show correct type icon and label for every preference, even if the accommodation is non-assignable
- [ ] Assignment grid still shows only `IsAssignable = true` accommodations

---

## Fix 3 — Hide ProgressBar when no families are assigned

### Problem

`AccommodationSlotCard.vue` renders `<ProgressBar>` with `v-if="accommodation.capacity"` — so it always shows when capacity is defined, even when `occupiedUnits = 0`. At 0 % it just adds visual clutter with a flat empty bar and a redundant `0 / X` number already visible in the header row.

### Solution

Only render the bar when at least one family/person is assigned to the slot.

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

```html
<!-- Before -->
<ProgressBar
  v-if="accommodation.capacity"
  ...

<!-- After -->
<ProgressBar
  v-if="accommodation.capacity && occupiedUnits > 0"
  ...
```

No other changes needed.

#### Acceptance criteria

- [ ] ProgressBar is hidden for empty accommodations (`occupiedUnits = 0`)
- [ ] ProgressBar appears as soon as any family is assigned to the slot
- [ ] Existing colour/overflow behaviour is unchanged

---

## Feature 4 — Dynamic accommodation-feature filter chips

### Problem

The filter bar currently shows type chips (Lodge / Bungalow / …) but families express preferences in terms of **features** (e.g., "Accesible", "Baño propio", "Planta baja"). The Board needs to narrow the grid to accommodations that match a family's required features or that have a specific characteristic without manually scanning each card.

### Context

- `AssignmentAccommodationResponse.availableFeatures` is a list of feature **IDs** (UUIDs).
- `AssignmentFamilyResponse.requiredFeatures` is also a list of feature IDs.
- Feature metadata (name, icon) is not currently included in the assignment state response.

### Solution

Add feature metadata to the state response so the panel can display named chips without an extra API call. Then add a multi-select feature filter to `AccommodationAssignmentPanel`.

#### Backend — add feature catalog to state response

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

```csharp
public record AccommodationFeatureSummary(Guid Id, string Name, string Icon);

// Update ProposalAssignmentStateResponse:
public record ProposalAssignmentStateResponse(
    Guid ProposalId,
    IReadOnlyList<AssignmentFamilyResponse> Families,
    IReadOnlyList<AssignmentAccommodationResponse> Accommodations,
    IReadOnlyList<AssignmentEntry> Assignments,
    IReadOnlyList<AccommodationTypeLookupItem> AccommodationTypeLookup,
    IReadOnlyList<AccommodationFeatureSummary> AllFeatures   // ADD
);
```

**File:** `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs`

Collect all unique features across all accommodations in the edition:

```csharp
var allFeatureIds = accommodations.SelectMany(a => a.Features.Select(f => f.Id)).Distinct().ToList();
var features = accommodations
    .SelectMany(a => a.Features)
    .DistinctBy(f => f.Id)
    .Select(f => new AccommodationFeatureSummary(f.Id, f.Name, f.Icon))
    .ToList();
```

This requires that the accommodation query already `Include`s `Features`. If not, add `.ThenInclude(a => a.Features)` to the include chain.

#### Frontend — type updates

**File:** `frontend/src/types/accommodation-assignment.ts`

```typescript
export interface AccommodationFeatureSummary {
  id: string
  name: string
  icon: string
}

// Update ProposalAssignmentStateResponse:
export interface ProposalAssignmentStateResponse {
  proposalId: string
  families: AssignmentFamilyResponse[]
  accommodations: AssignmentAccommodationResponse[]
  assignments: AssignmentEntry[]
  accommodationTypeLookup: AccommodationTypeLookupItem[]
  allFeatures: AccommodationFeatureSummary[]   // ADD
}
```

#### Frontend — filter chips in `AccommodationAssignmentPanel`

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

Add state:

```typescript
const activeFeatureFilter = ref<string | null>(null)  // feature ID or null = all
```

Add computed:

```typescript
const availableFeatures = computed((): AccommodationFeatureSummary[] => {
  const presentIds = new Set(props.state.accommodations.flatMap((a) => a.availableFeatures))
  return props.state.allFeatures.filter((f) => presentIds.has(f.id))
})
```

Feature chips (add below the existing type chips row):

```html
<div v-if="availableFeatures.length" class="flex flex-wrap gap-1">
  <button
    class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors"
    :class="activeFeatureFilter === null
      ? 'border-indigo-500 bg-indigo-500 text-white'
      : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'"
    @click="activeFeatureFilter = null"
  >
    <i class="pi pi-tag text-[10px]" />
    Todas las características
  </button>
  <button
    v-for="feat in availableFeatures"
    :key="feat.id"
    class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors"
    :class="activeFeatureFilter === feat.id
      ? 'border-indigo-500 bg-indigo-500 text-white'
      : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'"
    @click="activeFeatureFilter = activeFeatureFilter === feat.id ? null : feat.id"
  >
    <i :class="[feat.icon, 'text-[10px]']" />
    {{ feat.name }}
  </button>
</div>
```

Apply filter in `groupedAccommodations` (add after existing zone/type filters):

```typescript
if (activeFeatureFilter.value && !acc.availableFeatures.includes(activeFeatureFilter.value)) continue
```

Reset feature filter when proposal changes — add to the `watch(selectedProposalId, …)` in `AccommodationAssignmentView` or to a watcher inside the panel:

```typescript
watch(() => props.state.proposalId, () => {
  activeFeatureFilter.value = null
  activeTypeFilter.value = null
})
```

#### Acceptance criteria

- [ ] `ProposalAssignmentStateResponse` includes `allFeatures` with name + icon for every feature present in any accommodation
- [ ] Feature chips appear below the type chips (only when there are features to show)
- [ ] Clicking a feature chip filters the grid to accommodations that have that feature in `availableFeatures`
- [ ] Active chip is visually distinct; clicking again deactivates the filter
- [ ] Feature filter resets when switching proposals
- [ ] No extra API call needed — feature data comes from the state response

---

## Fix 5 — Remove zone thumbnail fallback from `AccommodationSlotCard`

### Problem

`AccommodationSlotCard` currently falls back to the **zone** thumbnail when the accommodation has no photo of its own:

```typescript
const displayThumbnail = computed(
  () => props.accommodation.primaryThumbnailUrl ?? props.accommodation.zonePrimaryThumbnailUrl ?? null
)
```

A zone photo represents dozens of accommodations — showing it as if it belongs to a specific room is misleading. In a dense grid it also adds noise to cards that have no distinct photo. The zone photo is already visible in the zone group header; repeating it inside every card in that zone adds no information and clutters the grid.

### Solution

Show only the accommodation's own photo. Remove the zone fallback from `AccommodationSlotCard`.

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

```typescript
// Before
const displayThumbnail = computed(
  () => props.accommodation.primaryThumbnailUrl ?? props.accommodation.zonePrimaryThumbnailUrl ?? null
)
const thumbnailIsZoneFallback = computed(
  () => !props.accommodation.primaryThumbnailUrl && !!props.accommodation.zonePrimaryThumbnailUrl
)

// After
const displayThumbnail = computed(() => props.accommodation.primaryThumbnailUrl ?? null)
```

Remove all uses of `thumbnailIsZoneFallback` and the "zona" overlay label from the template.

#### Acceptance criteria

- [ ] `AccommodationSlotCard` shows a thumbnail only when the accommodation itself has a photo (`primaryThumbnailUrl`)
- [ ] Cards without their own photo show no thumbnail, even if the parent zone has one
- [ ] "zona" fallback label removed

---

## Enhancement 6 — Richer selected-family detail panel

### Problem

When a Board member selects a family, the blue bar at the top of the right panel says only:

> **García López** seleccionada — haz clic en un alojamiento para asignarla

This gives no context about size, composition, or special needs — exactly the information needed to pick the right accommodation.

### Solution

Expand the selected-family banner with the full context needed for an informed assignment decision.

**Backend** — add `BabyCount` to `AssignmentFamilyResponse`

`AssignmentFamilyResponse` currently has `MemberCount`, `AdultCount`, `ChildCount`. Baby count (`MemberCount - AdultCount - ChildCount`) is not explicit. Add it for unambiguous display.

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

```csharp
public record AssignmentFamilyResponse(
    Guid RegistrationId,
    Guid FamilyUnitId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    int AdultCount,
    int ChildCount,
    int BabyCount,          // ADD
    bool HasPet,
    string? SpecialNeeds,
    string? CampatesPreference,
    IReadOnlyList<AccommodationPreferenceItem> AccommodationPreferences,
    bool HasSpecialNeeds,
    IReadOnlyList<Guid> RequiredFeatures,
    IReadOnlyList<Guid> FriendlyFamilyUnitIds
);
```

Populate `BabyCount` in the repository from the registration data. If the registration stores age ranges, `BabyCount` = registrants whose age falls in the baby range. If not available from the families query, derive as `MemberCount - AdultCount - ChildCount` (safe assumption given the 3-category system).

**Frontend** — type update

**File:** `frontend/src/types/accommodation-assignment.ts`

```typescript
export interface AssignmentFamilyResponse {
  // ...existing fields...
  babyCount: number   // ADD (after childCount)
}
```

**Frontend** — expanded selected-family banner

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

Replace the existing blue info div with a richer panel:

```html
<div
  v-if="selectedFamily"
  class="mb-4 rounded-lg border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-800"
>
  <div class="flex items-center justify-between">
    <span class="font-semibold">{{ selectedFamily.familyName }}</span>
    <span class="text-xs text-blue-500">Haz clic en un alojamiento para asignar</span>
  </div>

  <!-- Composition row -->
  <div class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-blue-700">
    <span v-if="selectedFamily.adultCount > 0">
      <i class="pi pi-user mr-0.5" />{{ selectedFamily.adultCount }}
      {{ selectedFamily.adultCount === 1 ? 'adulto' : 'adultos' }}
    </span>
    <span v-if="selectedFamily.childCount > 0">
      <i class="pi pi-star mr-0.5" />{{ selectedFamily.childCount }}
      {{ selectedFamily.childCount === 1 ? 'niño' : 'niños' }}
    </span>
    <span v-if="selectedFamily.babyCount > 0">
      <i class="pi pi-heart mr-0.5" />{{ selectedFamily.babyCount }}
      {{ selectedFamily.babyCount === 1 ? 'bebé' : 'bebés' }}
    </span>
    <span v-if="selectedFamily.hasPet" class="text-amber-600">
      <i class="pi pi-heart-fill mr-0.5" />Mascota
    </span>
  </div>

  <!-- Special needs -->
  <div
    v-if="selectedFamily.specialNeeds"
    class="mt-1.5 rounded border border-amber-300 bg-amber-50 px-2 py-1 text-xs text-amber-800"
  >
    <i class="pi pi-exclamation-triangle mr-1 text-amber-500" />
    {{ selectedFamily.specialNeeds }}
  </div>

  <!-- Campates preference text -->
  <p v-if="selectedFamily.campatesPreference" class="mt-1 text-xs italic text-blue-500">
    "{{ selectedFamily.campatesPreference }}"
  </p>
</div>
```

> **Icons**: use PrimeIcons only. `pi-user` for adults, `pi-star` for children (small/young), `pi-heart` for babies, `pi-heart-fill` for pet. Do not add external icon libraries.

#### Acceptance criteria

- [ ] Backend `AssignmentFamilyResponse` includes `BabyCount`
- [ ] Selected-family banner shows `X adultos`, `Y niños`, `Z bebés` (labels hidden when count = 0)
- [ ] Mascota indicator shown in amber only when `hasPet = true`
- [ ] `specialNeeds` text displayed in an amber warning box (not clipped/truncated)
- [ ] `campatesPreference` text shown in italic if present
- [ ] Banner remains compact and does not push the accommodation grid out of view on smaller screens

---

## Combined files to create / modify

### Backend

| Action | File | Reason |
|--------|------|--------|
| Modify | `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` | Add `GET /{zoneId}` to zones route group (Fix 1) |
| Modify | `src/Abuvi.API/Features/Camps/AccommodationZonesService.cs` | Add `GetByIdAsync` (Fix 1) |
| Modify | `src/Abuvi.API/Features/Camps/CampsModels.cs` | Add `AccommodationTypeLookupItem`, `AccommodationFeatureSummary` records; add `AccommodationTypeLookup` + `AllFeatures` to `ProposalAssignmentStateResponse`; add `BabyCount` to `AssignmentFamilyResponse` (Fixes 2, 4, 6) |
| Modify | `src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs` | Populate `AccommodationTypeLookup`, `AllFeatures`, `BabyCount` when building state (Fixes 2, 4, 6) |

### Frontend

| Action | File | Reason |
|--------|------|--------|
| Modify | `frontend/src/types/accommodation-assignment.ts` | Add `AccommodationTypeLookupItem`, `AccommodationFeatureSummary`; update `AssignmentFamilyResponse` (babyCount); update `ProposalAssignmentStateResponse` (Fixes 2, 4, 6) |
| Modify | `frontend/src/components/camps/AccommodationAssignmentPanel.vue` | Fix `accommodationTypeMap` to cover all IDs; add feature filter chips; expand selected-family banner (Fixes 2, 4, 6) |
| Modify | `frontend/src/components/camps/AccommodationSlotCard.vue` | Hide ProgressBar when empty; remove zone thumbnail fallback (Fixes 3, 5) |

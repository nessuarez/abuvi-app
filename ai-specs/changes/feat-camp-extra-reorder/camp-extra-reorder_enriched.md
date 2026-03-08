# User Story: CampEditionExtra Manual Reordering

## Summary

As a camp administrator, I want to manually reorder CampEditionExtras so that related extras are grouped together and displayed in a logical order for families during registration.

## Problem

Currently, `CampEditionExtra` items are ordered solely by `CreatedAt` (creation timestamp). Administrators cannot rearrange extras to group related items together (e.g., placing all meal-related extras adjacent, or grouping transport options). This makes it harder for families to understand the available extras during registration.

## Solution

Add a `SortOrder` field to `CampEditionExtra` and provide API + UI support for manual reordering, following the existing pattern already implemented in `CampEditionAccommodation`.

---

## Technical Implementation

### Reference Pattern

`CampEditionAccommodation` already implements `SortOrder` — use it as the reference:

- Entity: `src/Abuvi.API/Features/Camps/CampsModels.cs:360` — `public int SortOrder { get; set; } = 0;`
- Repository: `CampEditionAccommodationsRepository.cs:26` — `.OrderBy(e => e.SortOrder)`
- Service: `CampEditionAccommodationsService.cs:57,80` — maps `SortOrder` on create/update
- Validators: `CampsValidators.cs:408,436` — validates `SortOrder`

### Backend Changes

#### 1. Entity Model

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add to `CampEditionExtra` class:

```csharp
public int SortOrder { get; set; } = 0;
```

#### 2. Database Configuration

**File:** `src/Abuvi.API/Data/Configurations/CampEditionExtraConfiguration.cs`

Add column mapping:

```csharp
builder.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
```

#### 3. Migration

Create a new migration to add `sort_order` column to `camp_edition_extras` table:

```sql
ALTER TABLE camp_edition_extras ADD COLUMN sort_order integer NOT NULL DEFAULT 0;
```

Consider a data migration to set initial `sort_order` values based on current `created_at` ordering so existing extras maintain their current display order.

#### 4. Repository

**File:** `src/Abuvi.API/Features/Camps/CampEditionExtrasRepository.cs`

Change ordering from:

```csharp
.OrderBy(e => e.CreatedAt)
```

To:

```csharp
.OrderBy(e => e.SortOrder)
.ThenBy(e => e.CreatedAt)
```

#### 5. Request/Response DTOs

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

- Add `SortOrder` to `CreateCampEditionExtraRequest` (default 0)
- Add `SortOrder` to `UpdateCampEditionExtraRequest` (default 0)
- Add `SortOrder` to `CampEditionExtraResponse`

#### 6. Service

**File:** `src/Abuvi.API/Features/Camps/CampEditionExtrasService.cs`

- Map `SortOrder` in create and update methods
- Include `SortOrder` in response mapping

#### 7. Validators

**File:** `src/Abuvi.API/Features/Camps/CampsValidators.cs`

Add validation for `SortOrder`:

```csharp
RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
```

#### 8. Reorder Endpoint (Optional Enhancement)

**File:** `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`

Add a bulk reorder endpoint:

```
PUT /api/camps/editions/{editionId}/extras/reorder
Body: { "orderedIds": ["id1", "id2", "id3"] }
```

This assigns `SortOrder` values (0, 1, 2, ...) based on array position. This is the same pattern used for photo reordering (`ReorderCampPhotosRequest`).

### Frontend Changes

#### 1. TypeScript Types

**File:** `frontend/src/types/camp-edition.ts`

Add to `CampEditionExtra` interface:

```typescript
sortOrder: number
```

Add to create/update request types:

```typescript
sortOrder?: number
```

#### 2. Composable

**File:** `frontend/src/composables/useCampExtras.ts`

Add reorder method:

```typescript
async function reorderExtras(orderedIds: string[]): Promise<void>
```

#### 3. UI Component

**File:** `frontend/src/components/camps/CampEditionExtrasList.vue`

- Add drag-and-drop reordering to the DataTable (PrimeVue DataTable supports `reorderableRows`)
- Add `@row-reorder` event handler that calls the reorder API
- Alternatively, add up/down arrow buttons in the Actions column for simpler UX

---

## Files to Modify

| Layer | File | Change |
|-------|------|--------|
| Backend | `CampsModels.cs` | Add `SortOrder` to entity + DTOs |
| Backend | `CampEditionExtraConfiguration.cs` | Add column mapping |
| Backend | New migration file | Add `sort_order` column |
| Backend | `CampEditionExtrasRepository.cs` | Change ordering to `SortOrder` |
| Backend | `CampEditionExtrasService.cs` | Map `SortOrder` in create/update/response |
| Backend | `CampsValidators.cs` | Add `SortOrder` validation |
| Backend | `CampsEndpoints.cs` | Add reorder endpoint |
| Frontend | `camp-edition.ts` | Add `sortOrder` to types |
| Frontend | `useCampExtras.ts` | Add `reorderExtras()` method |
| Frontend | `CampEditionExtrasList.vue` | Add drag-and-drop or arrow reordering UI |

## Acceptance Criteria

- [ ] `CampEditionExtra` entity has a `SortOrder` integer field (default 0)
- [ ] Existing extras get initial `SortOrder` values preserving current `CreatedAt` order
- [ ] Extras are returned sorted by `SortOrder` (then `CreatedAt` as tiebreaker)
- [ ] `SortOrder` is included in create/update requests and responses
- [ ] A reorder endpoint accepts an ordered list of IDs and updates `SortOrder` accordingly
- [ ] Frontend allows reordering via drag-and-drop or manual controls
- [ ] Reordering is persisted and reflected immediately in the UI
- [ ] Only users with camp management permissions can reorder extras
- [ ] Unit tests cover the reorder logic and endpoint

## Non-Functional Requirements

- **Security:** Reorder endpoint must validate that all IDs belong to the specified `CampEditionId` and that the user has management permissions
- **Performance:** Reorder should be a single database transaction updating all `SortOrder` values
- **Backwards Compatibility:** Extras with `SortOrder = 0` should fall back to `CreatedAt` ordering (achieved by the `ThenBy` clause)

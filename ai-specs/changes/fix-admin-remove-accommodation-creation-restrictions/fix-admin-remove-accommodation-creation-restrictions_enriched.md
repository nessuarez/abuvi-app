# Fix: Remove Edition-Status Restrictions on Accommodation/Zone/Extra Creation

## Problem

Camp administrators cannot add accommodations, accommodation zones, or extras to a camp edition once its registrations have been closed (status `Closed`). This is incorrect because:

1. **Room assignment happens after registration closes.** Administrators close registrations first, then configure specific accommodations and assign registered families to them.
2. The restriction conflates "registrations are closed" (a normal workflow state) with "the edition is done" (status `Completed`).

Currently the guard blocks both `Closed` and `Completed` editions. Only `Completed` editions should be protected from structural changes; `Closed` editions still need accommodation management.

---

## Scope

This fix only removes the `Closed` guard from accommodation/zone/extra **creation and editing**. The `Completed` status guard remains intact — a finished edition must stay immutable.

| Resource | Current restriction | After fix |
|---|---|---|
| Accommodation (create) | NOT (Closed OR Completed) | NOT Completed |
| Extra (create) | NOT (Closed OR Completed) | NOT Completed |
| Edition general edit (canEdit) | NOT (Closed OR Completed) | NOT Completed |
| Extras "add" button (canAdd) | NOT (Closed OR Completed) | NOT Completed |

> **Out of scope:** Accommodation zone creation has no status guard in the backend service today (`AccommodationZonesService`) — no change needed there.

---

## Files to Modify

### Backend — `src/Abuvi.API/Features/Camps/`

#### 1. `CampEditionAccommodationsService.cs` (line 44-46)

Remove `CampEditionStatus.Closed` from the guard:

```csharp
// Before
if (edition.Status is CampEditionStatus.Completed or CampEditionStatus.Closed)
    throw new InvalidOperationException(
        "No se pueden añadir alojamientos a una edición cerrada o completada");

// After
if (edition.Status is CampEditionStatus.Completed)
    throw new InvalidOperationException(
        "No se pueden añadir alojamientos a una edición completada");
```

#### 2. `CampEditionExtrasService.cs` (line 44-46)

Same change for extras:

```csharp
// Before
if (edition.Status is CampEditionStatus.Completed or CampEditionStatus.Closed)
    throw new InvalidOperationException(
        "No se pueden añadir extras a una edición cerrada o completada");

// After
if (edition.Status is CampEditionStatus.Completed)
    throw new InvalidOperationException(
        "No se pueden añadir extras a una edición completada");
```

> **Note:** `CampEditionsService.UpdateAsync()` (line 230-232) also guards against `Closed` for ALL edition updates. That guard is broader and covers things like dates and prices — leave it unchanged for this task. The accommodation and extra services have their own independent guards that we are fixing here.

### Frontend — `frontend/src/`

#### 3. `views/camps/CampEditionDetailPage.vue` (line 71-76)

`canEdit` controls the top-level "Editar" button that enables editing mode for the whole edition page (dates, prices, notes, etc.). **Do not change this** — the general edition edit flow should stay locked for `Closed` editions.

What does need updating: the `CampEditionAccommodationsPanel` and `AccommodationZonePanel` components receive `canEdit` as a prop and use it to enable their own add/edit buttons. We need to pass a separate prop that allows accommodation management even when the edition is `Closed`.

Add a new computed:

```typescript
// In CampEditionDetailPage.vue, after existing canEdit computed
const canManageAccommodations = computed(() =>
  isBoard.value &&
  edition.value != null &&
  edition.value.status !== 'Completed'
)
```

Pass it down to the accommodation panels:

```html
<!-- CampEditionAccommodationsPanel and AccommodationZonePanel -->
<CampEditionAccommodationsPanel
  :edition-id="editionId"
  :can-manage="canManageAccommodations"
  ...
/>
<AccommodationZonePanel
  :edition-id="editionId"
  :can-manage="canManageAccommodations"
  ...
/>
```

Verify those components use a `canManage` prop (or equivalent) to gate their add/edit actions. If they use `canEdit` from the parent, update accordingly.

#### 4. `components/camps/CampEditionExtrasList.vue` (line 36-38)

Update the `canAdd` computed to allow adding extras when the edition is `Closed`:

```typescript
// Before
const canAdd = computed(
  () => canManage.value && props.editionStatus !== 'Completed' && props.editionStatus !== 'Closed'
)

// After
const canAdd = computed(
  () => canManage.value && props.editionStatus !== 'Completed'
)
```

---

## Acceptance Criteria

1. A Board/Admin user **can** create a new accommodation for an edition with status `Closed`.
2. A Board/Admin user **can** create a new extra for an edition with status `Closed`.
3. A Board/Admin user **cannot** create accommodations or extras for an edition with status `Completed` (guard unchanged).
4. The frontend "Añadir alojamiento" button is visible for `Closed` editions.
5. The frontend "Añadir extra" button is visible for `Closed` editions.
6. The general edition edit button (dates, prices, notes) remains hidden/disabled for `Closed` editions — no regression there.
7. All existing tests pass. Update any test that explicitly asserts the `Closed` guard on accommodation/extra creation.

---

## Tests to Update

Search for test methods that assert `InvalidOperationException` when creating an accommodation or extra on a `Closed` edition:

```
grep -r "Closed" src/Abuvi.API.Tests/Features/Camps/ --include="*.cs"
```

Any test asserting that creation fails on a `Closed` edition must be inverted (creation should now succeed) or removed.

---

## Non-functional

- No new endpoints or migrations required.
- No change to authorization roles — Board/Admin restriction remains.
- Error messages should remain in Spanish (existing convention).

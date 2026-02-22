# Enriched User Story: Amendments to Open Camp Editions

## Summary

As a **Board/Admin** user, I want to be able to modify certain fields of a camp edition that is already in `Open` status (accepting registrations) — such as prices, capacity, notes, and extras — so that I can correct mistakes or make last-minute adjustments without having to close and reopen the edition.

---

## Problem Context

The current system **blocks all price and date changes** once a `CampEdition` is in `Open` status:

```
CampEditionsService.UpdateAsync (line 204–215):
  if (edition.Status == CampEditionStatus.Open)
  {
      if (startDate changed || endDate changed || any price changed)
          throw "No se pueden modificar las fechas ni los precios de una edición abierta";
  }
```

Additionally, `Closed` and `Completed` editions reject all updates entirely.

Real-world use cases that are currently impossible:

- Fixing a typo in a price before families have paid
- Adding a new optional extra (e.g., "Camp Hoodie") after opening registration
- Adjusting `maxCapacity` upward after securing more accommodation
- Correcting a price that was entered incorrectly

---

## Scope of This Story

### In scope

1. **Relax price restrictions on Open editions**: Allow `pricePerAdult`, `pricePerChild`, `pricePerBaby` to be updated on an Open edition, with guard rules (see Business Rules).
2. **Extras on Open editions**: Already allowed (the extras service only blocks `Closed`/`Completed`). No code change needed — this story validates and documents the existing behaviour.
3. **Audit logging**: Log any change to prices of an Open edition with the old and new values.

### Out of scope

- Changing `startDate` / `endDate` / `year` on an Open edition (structural fields — remain locked).
- Retroactively recalculating `TotalAmount` for existing registrations (snapshots are immutable by design).
- UI changes (separate frontend ticket).
- Changing the pricing type or period of existing extras that have already been sold (already blocked by `CampEditionExtrasService.UpdateAsync`).

---

## Business Rules

### Rule 1 — Price Change Guard

**When a price is changed on an `Open` edition:**

**Option A (Recommended — simple, safe):**

- Allow price changes at **any point** while `Open`.
- Existing registrations keep their **snapshot prices** (`RegistrationMember.IndividualAmount` and `Registration.TotalAmount` are never recalculated).
- New registrations will use the updated prices.
- Log the change: old price → new price, who made it, timestamp.

**Option B (Stricter):**

- Block price changes if the edition has any registrations with `Status = Confirmed`.
- Allow price changes if all existing registrations are still `Pending`.

**Decision**: Use **Option A**. It is simpler, consistent with the snapshot pattern already built into the registration system (`RegistrationMember.IndividualAmount`, `RegistrationExtra.UnitPrice` are both snapshots), and avoids a complex query guard. The audit log is the safety net.

### Rule 2 — Date Fields Remain Locked

`startDate`, `endDate`, and `year` **cannot** be changed on an `Open` edition. These are structural and families have already committed based on these dates.

### Rule 3 — Extras on Open Editions

Adding a **new** extra to an `Open` edition is already allowed (no code change needed). This confirms the intended behaviour:

| Extra operation | Proposed/Draft | Open | Closed/Completed |
|---|---|---|---|
| Add extra | ✅ | ✅ | ❌ |
| Update extra (no sold units) | ✅ | ✅ | ❌ |
| Update extra price (has sold units) | ✅ | ❌ | ❌ |
| Deactivate extra | ✅ | ✅ | ❌ |
| Delete extra (no sold units) | ✅ | ✅ | ❌ |
| Delete extra (has sold units) | ❌ | ❌ | ❌ |

### Rule 4 — `maxCapacity` on Open Editions

Already allowed today. Rule: `maxCapacity` cannot be reduced below the current number of non-cancelled registrations.

---

## Technical Changes Required

### Backend

#### File: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`

**Method:** `UpdateAsync`

**Current behaviour (line 204–215):**

```csharp
if (edition.Status == CampEditionStatus.Open)
{
    if (request.StartDate != edition.StartDate ||
        request.EndDate != edition.EndDate ||
        request.PricePerAdult != edition.PricePerAdult ||
        request.PricePerChild != edition.PricePerChild ||
        request.PricePerBaby != edition.PricePerBaby)
    {
        throw new InvalidOperationException(
            "No se pueden modificar las fechas ni los precios de una edición abierta");
    }
}
```

**Required change:**

```csharp
if (edition.Status == CampEditionStatus.Open)
{
    // Dates and year are structural — always locked
    if (request.StartDate != edition.StartDate ||
        request.EndDate != edition.EndDate)
    {
        throw new InvalidOperationException(
            "No se pueden modificar las fechas de una edición abierta");
    }

    // Log price changes for audit
    if (request.PricePerAdult != edition.PricePerAdult ||
        request.PricePerChild != edition.PricePerChild ||
        request.PricePerBaby != edition.PricePerBaby)
    {
        _logger.LogWarning(
            "Price change on Open edition {EditionId}: " +
            "Adult {OldAdult}→{NewAdult}, Child {OldChild}→{NewChild}, Baby {OldBaby}→{NewBaby}",
            edition.Id,
            edition.PricePerAdult, request.PricePerAdult,
            edition.PricePerChild, request.PricePerChild,
            edition.PricePerBaby, request.PricePerBaby);
    }
}
```

**`maxCapacity` guard** (add inside `UpdateAsync`, applies to Open editions):

```csharp
// When reducing maxCapacity on an Open edition, ensure it doesn't go below current active registrations
if (edition.Status == CampEditionStatus.Open &&
    request.MaxCapacity.HasValue &&
    edition.MaxCapacity.HasValue &&
    request.MaxCapacity.Value < edition.MaxCapacity.Value)
{
    var activeRegistrations = await _registrationsRepository.CountActiveByEditionAsync(edition.Id, cancellationToken);
    if (request.MaxCapacity.Value < activeRegistrations)
        throw new InvalidOperationException(
            $"No se puede reducir la capacidad máxima a {request.MaxCapacity} " +
            $"porque ya hay {activeRegistrations} inscripciones activas");
}
```

> **Note:** `_registrationsRepository` will only be available once the Registrations feature is merged. Until then, skip the capacity guard (keep the existing `maxCapacity` update without the check) and add a `// TODO: add active registration check when IRegistrationsRepository is available` comment.

**`CampEditionsService` constructor change** (once Registrations feature lands):

```csharp
public CampEditionsService(
    ICampEditionsRepository repository,
    ICampsRepository campsRepository,
    IRegistrationsRepository registrationsRepository,  // NEW
    ILogger<CampEditionsService> logger)               // NEW
```

#### File: `src/Abuvi.API/Features/Camps/CampsValidators.cs` (or `CampsModels.cs`)

The `UpdateCampEditionRequest` validator currently accepts date fields for all statuses. Add a note: date field validation in the validator remains as-is (not empty, end > start). The _business rule_ rejecting date changes on Open editions lives in the service, not the validator.

---

### Updated Field Editability Matrix

| Field | Proposed | Draft | Open | Closed | Completed |
|---|---|---|---|---|---|
| `startDate` | ✅ | ✅ | ❌ | ❌ | ❌ |
| `endDate` | ✅ | ✅ | ❌ | ❌ | ❌ |
| `pricePerAdult` | ✅ | ✅ | ✅ (logged) | ❌ | ❌ |
| `pricePerChild` | ✅ | ✅ | ✅ (logged) | ❌ | ❌ |
| `pricePerBaby` | ✅ | ✅ | ✅ (logged) | ❌ | ❌ |
| `maxCapacity` | ✅ | ✅ | ✅ (guard) | ❌ | ❌ |
| `notes` | ✅ | ✅ | ✅ | ❌ | ❌ |
| `useCustomAgeRanges` | ✅ | ✅ | ❌¹ | ❌ | ❌ |
| `customBabyMaxAge` | ✅ | ✅ | ❌¹ | ❌ | ❌ |
| `customChildMinAge` | ✅ | ✅ | ❌¹ | ❌ | ❌ |
| `customChildMaxAge` | ✅ | ✅ | ❌¹ | ❌ | ❌ |
| `customAdultMinAge` | ✅ | ✅ | ❌¹ | ❌ | ❌ |
| Add extras | ✅ | ✅ | ✅ | ❌ | ❌ |
| Deactivate/activate extras | ✅ | ✅ | ✅ | ❌ | ❌ |
| Update extra price (0 sold) | ✅ | ✅ | ✅ | ❌ | ❌ |
| Update extra price (>0 sold) | ✅ | ✅ | ❌ | ❌ | ❌ |

¹ Age range fields affect how new registrations are priced. Changing them on an Open edition could create inconsistent pricing. **Recommendation: lock these on Open editions** — same as dates.

---

### API Contract Change

#### `PUT /api/camps/editions/{id}`

**Updated allowed fields when `status = Open`:**

| Field | Previously allowed | After this change |
|---|---|---|
| `notes` | ✅ | ✅ |
| `maxCapacity` | ✅ | ✅ (with guard) |
| `pricePerAdult` | ❌ | ✅ |
| `pricePerChild` | ❌ | ✅ |
| `pricePerBaby` | ❌ | ✅ |
| `startDate` | ❌ | ❌ |
| `endDate` | ❌ | ❌ |
| `useCustomAgeRanges` / custom ages | ❌ | ❌ |

**Error response (unchanged format) for locked fields:**

```json
{
  "success": false,
  "error": {
    "message": "No se pueden modificar las fechas de una edición abierta",
    "code": "OPERATION_ERROR"
  }
}
```

---

### Data Model Impact

**No migration required.** This story only changes business logic in the service layer. No new tables, columns, or indexes.

The existing snapshot pattern in `RegistrationMember.IndividualAmount` and `RegistrationExtra.UnitPrice` already ensures that price changes to the edition do NOT retroactively alter existing registrations. This is the correct, intended behaviour.

---

## Files to Modify

| File | Change |
|---|---|
| `src/Abuvi.API/Features/Camps/CampEditionsService.cs` | Relax Open edition price guard; add audit logging; add `maxCapacity` guard (post-Registrations) |
| `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs` | Update/add tests for new Open edition behaviour |

---

## TDD Test Cases

### New/Updated Unit Tests (`CampEditionsServiceTests.cs`)

**Existing test to update:**

- `UpdateAsync_WhenStatusIsOpen_CannotChangeDatesOrPrices` → **Split into two tests:**
  - `UpdateAsync_WhenStatusIsOpen_CannotChangeDates`
  - `UpdateAsync_WhenStatusIsOpen_CanChangePrices_AndLogsWarning`

**New tests to add:**

- `UpdateAsync_WhenStatusIsOpen_ChangingPrices_ReturnsUpdatedEdition`
- `UpdateAsync_WhenStatusIsOpen_ChangingStartDate_ThrowsInvalidOperationException`
- `UpdateAsync_WhenStatusIsOpen_ChangingEndDate_ThrowsInvalidOperationException`
- `UpdateAsync_WhenStatusIsOpen_ChangingAgeRanges_ThrowsInvalidOperationException`
- `UpdateAsync_WhenStatusIsOpen_PriceChanges_LogsAuditWarning`
- `UpdateAsync_WhenStatusIsOpen_ReducingCapacityBelowActiveRegistrations_ThrowsException` _(add as TODO until IRegistrationsRepository exists)_
- `UpdateAsync_WhenStatusIsOpen_IncreasingCapacity_AllowsUpdate`

---

## Acceptance Criteria

- [ ] `PUT /api/camps/editions/{id}` on an Open edition with changed prices returns 200 OK
- [ ] `PUT /api/camps/editions/{id}` on an Open edition with changed `startDate` or `endDate` returns 400 with `OPERATION_ERROR`
- [ ] `PUT /api/camps/editions/{id}` on an Open edition with changed age range fields returns 400 with `OPERATION_ERROR`
- [ ] Changing prices on an Open edition produces a structured warning log entry with old and new values
- [ ] Existing registrations for that edition retain their original `TotalAmount` (unaffected by price change)
- [ ] New registrations created after the price change use the updated price
- [ ] Adding a new extra to an Open edition (`POST /api/camps/editions/{editionId}/extras`) still returns 201 Created (no regression)
- [ ] All existing tests pass
- [ ] New unit tests pass (90%+ coverage on modified service)

---

## Security Considerations

- Only `Admin` or `Board` roles can call `PUT /api/camps/editions/{id}` (no change to existing authorization).
- Price changes are audit-logged at `Warning` level with the edition ID, old prices, and new prices.
- Existing registration amounts are never recalculated server-side by this change.

---

## Implementation Notes

1. **Registrations repository dependency**: The `maxCapacity` guard requires `IRegistrationsRepository.CountActiveByEditionAsync`. Add a `// TODO` comment if that interface is not yet available when this story is implemented. The capacity guard is a nice-to-have safety net — it can land in a follow-up once the Registrations feature is merged.

2. **Logger injection**: `CampEditionsService` currently does not inject `ILogger<CampEditionsService>`. Add it to the constructor.

3. **No migration**: Zero database changes.

4. **Extras behaviour confirmation**: The `CampEditionExtrasService.CreateAsync` currently rejects creation for `Closed` and `Completed` editions but allows `Open`. This is correct and requires no change.

---

## Document Control

- **Feature ID**: `feat-open-edition-amendments`
- **Date**: 2026-02-22
- **Status**: ✅ Ready for implementation
- **Depends on**: `feat-camp-edition-extras` (already merged), `feat-camps-registration` (for capacity guard — can be deferred)
- **No migration required**

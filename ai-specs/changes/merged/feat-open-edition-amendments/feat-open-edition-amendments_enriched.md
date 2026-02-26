# Enriched User Story: Amendments to Open Camp Editions

## Summary

As an **Admin** user, I want to be able to correct mistakes in a camp edition that is already in `Open` status (accepting registrations) — such as a wrong price or a missing extra — so that I can fix the issue without disrupting families who are already registered.

---

## Problem Context

The current system **blocks all price and date changes** once a `CampEdition` is in `Open` status:

```
CampEditionsService.UpdateAsync (lines 204–215):
  if (edition.Status == CampEditionStatus.Open)
  {
      if (startDate changed || endDate changed || any price changed)
          throw "No se pueden modificar las fechas ni los precios de una edición abierta";
  }
```

Real-world use cases that are currently impossible:

- Correcting a price entered incorrectly when the edition was opened
- Adding a new optional extra (e.g., "Camp Hoodie") after opening registration
- Adjusting `maxCapacity` upward after securing more accommodation

---

## Two Approaches Evaluated

### Approach A — Relax field restrictions on Open editions

Allow `pricePerAdult`, `pricePerChild`, `pricePerBaby` to be updated while the edition stays `Open`, with audit logging. Dates remain locked.

**Pros:**

- Edition stays `Open` → no interruption for families registering
- Surgical: only the specific fields are relaxed
- Simple code change (remove price check from the `if` block in `UpdateAsync`)

**Cons:**

- A price change could happen mid-registration (a family starts the form at the old price and submits at the new price — risk is low since prices are calculated server-side at submission time)
- Age range fields (`useCustomAgeRanges`, `customBabyMaxAge`, etc.) are harder to relax safely — they affect pricing of new registrations

---

### Approach B — Admin can roll back an Open edition to Draft ✅ RECOMMENDED

Allow an **Admin-only** backward status transition: `Open → Draft`. The Admin makes all necessary changes (prices, extras, dates, anything), then transitions back `Draft → Open`.

**Pros:**

- No relaxation of field restrictions — the existing `UpdateAsync` logic remains entirely unchanged
- All fields can be edited during the Draft window, including dates and age ranges
- The transition itself is an explicit action, making the intent clear in the audit trail
- Consistent with the existing status machine pattern

**Cons:**

- The edition is temporarily unavailable for new registrations while in `Draft`
- A second concern (see below): the `Draft → Open` transition currently rejects editions whose `startDate` is in the past — this blocks re-opening if the camp has already started

**How to resolve the `startDate` blocker:**

```csharp
// Current check (ValidateDateConstraintsForTransition, line 375):
if (newStatus == CampEditionStatus.Open && edition.StartDate.Date < today)
    throw new InvalidOperationException("...");
```

This check exists to prevent opening registration for a camp that hasn't been organised yet and whose dates have already passed. But when the edition is being **re-opened** (it was already `Open` before and has existing registrations), this check is unnecessary.

**Solution**: Accept an optional `force: bool` flag in `PATCH /api/camps/editions/{id}/status`, restricted to `Admin` role, that bypasses the `startDate < today` constraint. Board users cannot bypass it.

Alternatively: check whether the edition has any existing registrations (indicating it was previously Open) and skip the date constraint in that case.

---

## Decision: Approach B

Approach B is preferred because:

1. Zero change to `UpdateAsync` and its existing restrictions
2. The `Open → Draft` transition is a clear, auditable admin action
3. Board users retain zero ability to bypass restrictions (only Admin can roll back)
4. The `force` flag for re-opening is minimal and explicit

---

## Scope

### In scope

1. **New backward status transition `Open → Draft`** — Admin only
2. **`force` flag on `PATCH .../status`** — allows Admin to reopen an edition even if `startDate` is in the past
3. **Extras on Open editions** — already allowed, no change needed (documented here for clarity)

### Out of scope

- Relaxing field restrictions in `UpdateAsync` (not needed with Approach B)
- UI changes (separate frontend ticket)
- Any changes to `Closed` or `Completed` status (remain immutable)

---

## Business Rules

### Rule 1 — `Open → Draft` transition is Admin-only

The backward transition `Open → Draft` must **not** be available to `Board` users. Only `Admin` can roll back a live edition to `Draft`.

Rationale: the edition going offline (even briefly) affects families who are actively registering. This is an escalated permission.

### Rule 2 — Re-opening with `force` flag

When transitioning `Draft → Open` and the edition was previously `Open` (i.e., it has existing registrations OR the Admin explicitly sets `force: true`), the `startDate < today` date constraint is bypassed.

The `force` flag is only accepted when:

- The caller has `Admin` role
- The current status is `Draft`
- The target status is `Open`

If `force: true` is sent by a `Board` user → 403 Forbidden.

### Rule 3 — Extras on Open editions (existing behaviour, no change)

Adding a new extra to an `Open` edition is already supported. No code change needed.

| Extra operation | Proposed/Draft | Open | Closed/Completed |
| --- | --- | --- | --- |
| Add extra | ✅ | ✅ | ❌ |
| Update extra (no sold units) | ✅ | ✅ | ❌ |
| Update extra price (has sold units) | ✅ | ❌ | ❌ |
| Deactivate/activate extra | ✅ | ✅ | ❌ |
| Delete extra (no sold units) | ✅ | ✅ | ❌ |
| Delete extra (has sold units) | ❌ | ❌ | ❌ |

### Rule 4 — Existing registrations are unaffected

When the edition returns to `Open` after a price correction, all existing registrations keep their snapshot amounts (`RegistrationMember.IndividualAmount`, `RegistrationExtra.UnitPrice`). No recalculation is performed.

---

## Technical Changes Required

### 1. `ValidateStatusTransition` in `CampEditionsService.cs`

**Current** (line 355–368): only forward transitions.

**Change**: allow `Open → Draft` as a valid transition. The role restriction (`Admin` only) is enforced at the endpoint level (see below), not in this private method.

```csharp
private static void ValidateStatusTransition(CampEditionStatus current, CampEditionStatus next)
{
    var validTransitions = new Dictionary<CampEditionStatus, CampEditionStatus[]>
    {
        [CampEditionStatus.Proposed]  = [CampEditionStatus.Draft],
        [CampEditionStatus.Draft]     = [CampEditionStatus.Open],
        [CampEditionStatus.Open]      = [CampEditionStatus.Closed, CampEditionStatus.Draft], // ← Draft added
        [CampEditionStatus.Closed]    = [CampEditionStatus.Completed],
        [CampEditionStatus.Completed] = []
    };

    if (!validTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
        throw new InvalidOperationException(
            $"La transición de '{current}' a '{next}' no es válida");
}
```

### 2. `ChangeStatusAsync` — accept `force` flag

```csharp
public async Task<CampEditionResponse> ChangeStatusAsync(
    Guid editionId,
    CampEditionStatus newStatus,
    bool force,                   // ← NEW: only honoured for Admin role (validated by caller)
    CancellationToken cancellationToken = default)
{
    var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
    if (edition == null)
        throw new InvalidOperationException("La edición de campamento no fue encontrada");

    ValidateStatusTransition(edition.Status, newStatus);

    if (!force)
        ValidateDateConstraintsForTransition(edition, newStatus);
    // When force=true, date constraints are skipped (Admin re-opening an in-progress edition)

    edition.Status = newStatus;
    var updated = await _repository.UpdateAsync(edition, cancellationToken);
    return MapToCampEditionResponse(updated, updated.Camp.Name);
}
```

### 3. `PATCH /api/camps/editions/{id}/status` endpoint

**Current request body:**
```json
{ "status": "Draft" }
```

**Updated request body:**
```json
{ "status": "Draft", "force": false }
```

**Authorization change:**

| Transition | Board | Admin |
| --- | --- | --- |
| `Proposed → Draft` | ✅ | ✅ |
| `Draft → Open` | ✅ | ✅ |
| `Draft → Open` (force) | ❌ | ✅ |
| `Open → Draft` | ❌ | ✅ |
| `Open → Closed` | ✅ | ✅ |
| `Closed → Completed` | ✅ | ✅ |

**Endpoint handler change** (`CampsEndpoints.cs`):

```csharp
private static async Task<IResult> ChangeEditionStatus(
    Guid id,
    ChangeEditionStatusRequest request,
    ClaimsPrincipal user,
    [FromServices] CampEditionsService service,
    CancellationToken ct)
{
    var isAdmin = user.IsInRole("Admin");
    var isBoard = user.IsInRole("Board");

    // Open → Draft is Admin-only
    if (request.Status == CampEditionStatus.Draft)
    {
        var edition = /* fetch current edition to check its status */;
        if (edition?.Status == CampEditionStatus.Open && !isAdmin)
            return Results.Forbid();
    }

    // force flag is Admin-only
    if (request.Force && !isAdmin)
        return Results.Forbid();

    try
    {
        var updated = await service.ChangeStatusAsync(id, request.Status, request.Force, ct);
        return Results.Ok(ApiResponse<CampEditionResponse>.Ok(updated));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ApiResponse<object>.Fail(ex.Message, "OPERATION_ERROR"));
    }
}
```

**Updated DTO** (`CampsModels.cs`):

```csharp
public record ChangeEditionStatusRequest(
    CampEditionStatus Status,
    bool Force = false   // ← NEW: Admin-only flag to bypass startDate constraint on re-open
);
```

### 4. Updated status transition diagram

```
Proposed ──► Draft ──► Open ──► Closed ──► Completed
                        ▲  │
                        │  │  (Admin only: Open → Draft)
                        └──┘
```

---

## Files to Modify

| File | Change |
| --- | --- |
| `src/Abuvi.API/Features/Camps/CampEditionsService.cs` | Add `Open → Draft` to `ValidateStatusTransition`; add `force` param to `ChangeStatusAsync` |
| `src/Abuvi.API/Features/Camps/CampsModels.cs` | Add `Force = false` to `ChangeEditionStatusRequest` |
| `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` | Add Admin-only guard for `Open → Draft`; pass `force` to service |
| `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs` | Add new test cases |
| `src/Abuvi.Tests/Integration/Features/Camps/CampEditionsEndpointsTests.cs` | Add role-based transition tests |

**No database migration required.**

---

## TDD Test Cases

### Unit Tests (`CampEditionsServiceTests.cs`)

**New tests for `ChangeStatusAsync`:**

- `ChangeStatusAsync_WhenOpenToD raft_WithForce_False_UpdatesStatus`
- `ChangeStatusAsync_WhenDraftToOpen_WithForceFalse_AndStartDateInPast_ThrowsInvalidOperationException`
- `ChangeStatusAsync_WhenDraftToOpen_WithForceTrue_AndStartDateInPast_UpdatesStatus`
- `ChangeStatusAsync_WhenClosedToDraft_ThrowsInvalidOperationException` (backward not allowed from Closed)
- `ChangeStatusAsync_WhenOpenToDraft_SetsStatusToDraft`

### Integration Tests (`CampEditionsEndpointsTests.cs`)

- `ChangeStatus_OpenToDraft_WithBoardToken_ReturnsForbidden`
- `ChangeStatus_OpenToDraft_WithAdminToken_Returns200`
- `ChangeStatus_DraftToOpen_WithForce_WithBoardToken_ReturnsForbidden`
- `ChangeStatus_DraftToOpen_WithForce_WithAdminToken_Returns200`
- `ChangeStatus_DraftToOpen_WithForce_False_AndStartDateInPast_Returns400`

---

## Acceptance Criteria

- [ ] `PATCH /api/camps/editions/{id}/status` with `{ "status": "Draft" }` on an Open edition returns 403 for Board role
- [ ] Same call returns 200 for Admin role
- [ ] After `Open → Draft`, `GET /api/camps/editions/active` returns `null` (no active edition)
- [ ] All previously existing registrations for the edition remain intact
- [ ] `PATCH` with `{ "status": "Open", "force": true }` on a Draft edition with `startDate` in the past returns 403 for Board role
- [ ] Same call returns 200 for Admin role
- [ ] `PATCH` with `{ "status": "Open", "force": false }` on a Draft edition with `startDate` in the past returns 400
- [ ] `PATCH` from `Closed → Draft` or `Completed → Draft` still returns 400 (backward transition from Closed/Completed not allowed)
- [ ] All existing tests pass (no regression)

---

## Security Considerations

- The `Open → Draft` rollback is **Admin-only**. Board users cannot take an edition offline.
- The `force` re-open flag is **Admin-only**. No Board user can bypass the `startDate` date constraint.
- Role enforcement happens in the endpoint handler using `ClaimsPrincipal`, not in the service (service only accepts the already-validated `force` boolean).
- No data is deleted when rolling back to Draft. Existing registrations are preserved in `Pending` or `Confirmed` state.

---

## Implementation Notes

1. **No migration needed** — zero database changes.
2. **`UpdateAsync` unchanged** — the existing field restriction logic in `CampEditionsService.UpdateAsync` is NOT modified. This is the key advantage of Approach B.
3. **ClaimsPrincipal in endpoint** — `CampsEndpoints.cs` already receives `ClaimsPrincipal` in other handlers (pattern established). Use `user.IsInRole("Admin")`.
4. **Registrations during Draft window** — families who attempt to register while the edition is `Draft` will receive a 422 `EDITION_NOT_OPEN` error (existing behaviour from `CreateRegistrationValidator`). This is expected and acceptable.
5. **Extras during Draft window** — extras can still be added/modified while `Draft` (existing behaviour, no change needed).

---

## Document Control

- **Feature ID**: `feat-open-edition-amendments`
- **Date**: 2026-02-22
- **Status**: ✅ Ready for implementation
- **Approach selected**: B — Admin-only `Open → Draft` rollback
- **Depends on**: `feat-camp-edition-extras` (already merged)
- **No migration required**
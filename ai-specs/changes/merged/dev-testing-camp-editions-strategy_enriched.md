# Dev Testing Strategy for CampEditions — Enriched Analysis

**Source**: Inline question — manual testing of campEditions blocked by a closed 2026 edition and date-based validation rules.

**Date**: 2026-02-26

---

## Problem Statement

The developer wants to test the full campEdition lifecycle (propose → promote → open → close → complete) manually in the development environment. The current dev database already contains a **Closed** camp edition for year 2026, which causes two classes of problems:

1. **Proposal blocked**: `ExistsAsync` in `CampEditionsRepository` returns `true` for any non-archived edition with the same `(CampId, Year)` combination. Since there is already a 2026 Closed edition, trying to propose a new 2026 edition throws `"Ya existe una edición para este campamento en el año 2026"`.
2. **Status transition blocked by date constraints**: `ValidateDateConstraintsForTransition` in `CampEditionsService` enforces:
   - Moving to `Open` requires `StartDate >= today` (today = 2026-02-26).
   - Moving to `Completed` requires `EndDate < today`.
   - Since realistic future camp dates have not passed yet, you cannot complete a test edition without special handling.

---

## Root Cause: Exact Business Rules That Block Testing

From `CampEditionsService.cs`:

```csharp
// Blocks propose if non-archived edition already exists for same camp+year
var exists = await _repository.ExistsAsync(request.CampId, request.Year, cancellationToken);
if (exists)
    throw new InvalidOperationException($"Ya existe una edición para este campamento en el año {request.Year}");

// Blocks Open transition if start date is in the past
if (newStatus == CampEditionStatus.Open && edition.StartDate.Date < today)
    throw new InvalidOperationException("No se puede abrir el registro de una edición con fecha de inicio en el pasado");

// Blocks Completed transition if end date has not yet passed
if (newStatus == CampEditionStatus.Completed && edition.EndDate.Date >= today)
    throw new InvalidOperationException("No se puede marcar como completada una edición cuya fecha de fin no ha pasado");
```

From `CampEditionsRepository.cs`:

```csharp
// ExistsAsync — includes Closed status, so the existing 2026 edition prevents new proposals
return await _context.CampEditions
    .AnyAsync(e => e.CampId == campId && e.Year == year && !e.IsArchived, cancellationToken);
```

---

## Strategy: Best Approach Without Breaking Business Logic

### Option A — Archive the Existing Closed Edition via the Database (Recommended for dev)

The `IsArchived` flag already exists specifically to exclude editions from uniqueness and display queries. Setting it to `true` on the problematic 2026 edition is semantically correct (it signals "this entry is superseded") and does not alter any business rule — it mirrors exactly what `RejectProposalAsync` does.

**Steps:**

1. Connect to the dev PostgreSQL database and run:

```sql
-- Step 1: Identify the blocking edition
SELECT id, camp_id, year, status, is_archived
FROM camp_editions
WHERE year = 2026 AND is_archived = false;

-- Step 2: Archive it (equivalent to a rejected proposal, safe for dev)
UPDATE camp_editions
SET is_archived = true, updated_at = NOW()
WHERE year = 2026 AND status = 'Closed' AND is_archived = false;
```

2. You can now propose a fresh edition for the same camp and year 2026.

3. To test the full lifecycle with realistic date constraints, use **future dates** for the new proposal (e.g., StartDate = 2026-07-01, EndDate = 2026-07-15). This satisfies the `Open` transition rule without needing date mocking.

4. To reach `Completed` in dev, use the `force` flag (already implemented):

```json
PATCH /api/camps/editions/{id}/status
{
  "status": "Completed",
  "force": true
}
```

Note: The `force` flag **only** bypasses the `StartDate < today` guard for re-opening. It does **not** bypass the `EndDate >= today` guard for Completed. The guard for Completed is hardcoded unconditionally (no `force` path). See below for the specific fix needed.

**Correction after re-reading the code:**

```csharp
private static void ValidateDateConstraintsForTransition(CampEdition edition, CampEditionStatus newStatus)
{
    var today = DateTime.UtcNow.Date;

    if (newStatus == CampEditionStatus.Open && edition.StartDate.Date < today)
        throw new ...;

    if (newStatus == CampEditionStatus.Completed && edition.EndDate.Date >= today)
        throw new ...;
}
```

The method is only called when `force == false`:

```csharp
if (!force)
    ValidateDateConstraintsForTransition(edition, newStatus);
```

So `force: true` **does** bypass both date constraints. This is the correct escape hatch for dev/admin use. Only Admin role can set `force: true` (enforced at the endpoint level in `CampsEndpoints.cs`).

---

### Option B — Use a Different Year (Simplest, Zero DB Touch)

Since the constraint is per `(CampId, Year)`, you can simply propose an edition for **year 2027** (or any future year without an existing edition). This requires zero database changes and respects all business rules. The only limitation is that you lose the ability to test a "current year 2026" scenario.

**Steps:**
1. Propose an edition for the same camp but with `year: 2027`, `startDate` set to a future date (e.g. 2027-07-01).
2. Walk the full lifecycle normally.
3. Use `force: true` (Admin) to skip date guards when testing Completed.

This is the least intrusive option and should be the default for routine testing.

---

### Option C — Use a Different Camp (Also Zero DB Touch)

If there are multiple camps in the dev database, create or use a camp that has no 2026 edition. You can create a camp via `POST /api/camps` and then propose a fresh 2026 edition for it.

---

### Option D — Add a Dev-Only API Endpoint or Seeder (Only if repeated testing is needed)

If this situation recurs frequently (e.g. after each database reset or for CI), consider adding a dev-only data-seeding mechanism. This could be:

- A EF Core `DataSeeder` class that runs on `app.Environment.IsDevelopment()` and inserts a fresh Proposed edition for 2027 if none exists.
- A `DELETE /api/dev/reset-editions` endpoint guarded by `app.Environment.IsDevelopment()` that archives all non-archived editions and inserts a clean test edition.

This option requires code changes and should only be done if manual testing scenarios justify the maintenance cost.

---

## Recommended Decision Tree

```
Do you need to test a 2026 edition specifically?
  ├── YES → Archive the existing 2026 Closed edition via SQL (Option A)
  └── NO  → Propose for year 2027 (Option B) — preferred, zero risk

After proposing:
  - Promote to Draft: POST /api/camps/editions/{id}/promote
  - Transition to Open: PATCH /api/camps/editions/{id}/status { "status": "Open" }
      → Requires StartDate >= today, so use future dates
  - Transition to Closed: PATCH /api/camps/editions/{id}/status { "status": "Closed" }
  - Transition to Completed (bypassing end date constraint):
      PATCH /api/camps/editions/{id}/status { "status": "Completed", "force": true }
      → Requires Admin role JWT
```

---

## Summary of Blockers and Their Resolution

| Blocker | Cause | Resolution |
|---|---|---|
| Cannot propose new 2026 edition | `ExistsAsync` matches the existing Closed 2026 edition | Archive the existing edition via SQL, or use a different year |
| Cannot open an edition in the past | `ValidateDateConstraintsForTransition` blocks `Open` if `StartDate < today` | Use future start dates, or use `force: true` (Admin only) |
| Cannot complete a future edition | Same guard blocks `Completed` if `EndDate >= today` | Use `force: true` (Admin only) — bypasses all date constraints |
| Cannot update a Closed edition | `UpdateAsync` blocks all edits on Closed/Completed status | No workaround; this is correct behavior — test updates while in Proposed/Draft/Open |

---

## What NOT to Do

- Do not modify the `ExistsAsync` query to exclude `Closed` status — a closed edition for a given year should prevent re-proposals in production.
- Do not remove the date-constraint validations — they exist to prevent real operational mistakes (e.g. opening a registration for a camp that already happened).
- Do not change the `force` flag scope to bypass the uniqueness check — the uniqueness check is in `ProposeAsync`, which does not receive the `force` flag and should not.
- Do not hard-delete (`DeleteAsync`) editions in production; use archiving. In dev, hard deletion is acceptable for one-off cleanup.

---

## Files Relevant to This Analysis

- `/src/Abuvi.API/Features/Camps/CampEditionsService.cs` — business logic, all validation and transitions
- `/src/Abuvi.API/Features/Camps/CampEditionsRepository.cs` — `ExistsAsync` uniqueness query, `GetCurrentAsync` priority logic
- `/src/Abuvi.API/Features/Camps/CampsValidators.cs` — FluentValidation for `ProposeCampEditionRequest` and `UpdateCampEditionRequest`
- `/src/Abuvi.API/Features/Camps/CampsEndpoints.cs` — `ChangeEditionStatus` handler with role-guarded `force` flag
- `/src/Abuvi.API/Features/Camps/CampsModels.cs` — `CampEditionStatus` enum, entity model, all DTOs

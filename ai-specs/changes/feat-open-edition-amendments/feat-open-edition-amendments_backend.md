# Backend Implementation Plan: feat-open-edition-amendments — Amendments to Open Camp Editions

## Overview

Allow an **Admin** to roll back an `Open` camp edition to `Draft` (Admin-only backward status transition), make the required corrections, then re-open it. A `force` flag enables re-opening even when `StartDate` is in the past (needed when a camp is in progress).

This is a pure **logic change** — zero database schema changes, no migration required. All changes are confined to the existing `Camps` feature slice.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

### Files to Modify

| File | Change |
| --- | --- |
| `CampsModels.cs` | Add `Force = false` to `ChangeEditionStatusRequest` |
| `CampEditionsService.cs` | Add `Open → Draft` to `ValidateStatusTransition`; add `force` param to `ChangeStatusAsync` |
| `CampsEndpoints.cs` | Add `ClaimsPrincipal user` to `ChangeEditionStatus`; add Admin-only role guards; pass `force` to service |
| `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs` | Update existing tests to new `ChangeStatusAsync` signature; add new TDD test cases |

**No cross-cutting concerns affected.** No `Program.cs` changes. No migration.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-open-edition-amendments-backend`
- **Implementation Steps**:
  1. Ensure you are on the latest `main`: `git checkout main && git pull origin main`
  2. Create new branch: `git checkout -b feature/feat-open-edition-amendments-backend`
  3. Verify: `git branch`
- **Note**: This must be the FIRST step before any code changes.

---

### Step 1 (TDD — RED): Write Failing Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs`
- **Action**: Update all existing calls to `ChangeStatusAsync` to the new 3-argument signature, then add new test methods covering the new behavior. All tests will fail to compile until Step 2–3.

#### 1a. Update existing `ChangeStatusAsync` calls

All 5 existing tests that call `_sut.ChangeStatusAsync(editionId, ...)` must be updated to add `force: false` (the new third argument):

1. `ChangeStatusAsync_WithValidTransition_UpdatesStatus` (line 499):
   ```csharp
   var result = await _sut.ChangeStatusAsync(editionId, to, force: false);
   ```
   Also add a new `[InlineData]` entry for the new valid transition:
   ```csharp
   [InlineData(CampEditionStatus.Open, CampEditionStatus.Draft)]
   ```
   For `Open → Draft`, set `startDate = DateTime.UtcNow.AddDays(-30)` and `endDate = DateTime.UtcNow.AddDays(10)` (no date constraint applies on `→ Draft`).

2. `ChangeStatusAsync_WithInvalidTransition_ThrowsException` (line 534):
   ```csharp
   var act = async () => await _sut.ChangeStatusAsync(editionId, to, force: false);
   ```

3. `ChangeStatusAsync_ToOpen_WithPastStartDate_ThrowsException` (line 561):
   ```csharp
   var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Open, force: false);
   ```

4. `ChangeStatusAsync_ToCompleted_WithFutureEndDate_ThrowsException` (line 588):
   ```csharp
   var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Completed, force: false);
   ```

5. `ChangeStatusAsync_WithNotFoundEdition_ThrowsException` (line 602):
   ```csharp
   var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Draft, force: false);
   ```

#### 1b. Add new test methods

Add the following test methods inside the `#region ChangeStatusAsync Tests` block:

```csharp
[Fact]
public async Task ChangeStatusAsync_WhenOpenToDraft_WithForceFalse_SetsStatusToDraft()
{
    // Arrange
    var editionId = Guid.NewGuid();
    var edition = new CampEdition
    {
        Id = editionId,
        CampId = Guid.NewGuid(),
        Year = 2026,
        Status = CampEditionStatus.Open,
        StartDate = DateTime.UtcNow.AddDays(-5), // Camp already started — still allowed for Open→Draft
        EndDate = DateTime.UtcNow.AddDays(5),
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m,
        Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
    };

    _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
        .Returns(edition);
    _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
        .Returns(args => args.Arg<CampEdition>());

    // Act
    var result = await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Draft, force: false);

    // Assert
    result.Status.Should().Be(CampEditionStatus.Draft);
    await _repository.Received(1).UpdateAsync(
        Arg.Is<CampEdition>(e => e.Status == CampEditionStatus.Draft),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task ChangeStatusAsync_WhenDraftToOpen_WithForceTrue_AndStartDateInPast_UpdatesStatus()
{
    // Arrange
    var editionId = Guid.NewGuid();
    var edition = new CampEdition
    {
        Id = editionId,
        CampId = Guid.NewGuid(),
        Year = 2026,
        Status = CampEditionStatus.Draft,
        StartDate = DateTime.UtcNow.AddDays(-3), // Past start date
        EndDate = DateTime.UtcNow.AddDays(5),
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m,
        Camp = new Camp { Name = "Test Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
    };

    _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
        .Returns(edition);
    _repository.UpdateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
        .Returns(args => args.Arg<CampEdition>());

    // Act — force=true bypasses the startDate < today constraint
    var result = await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Open, force: true);

    // Assert
    result.Status.Should().Be(CampEditionStatus.Open);
    await _repository.Received(1).UpdateAsync(
        Arg.Is<CampEdition>(e => e.Status == CampEditionStatus.Open),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task ChangeStatusAsync_WhenDraftToOpen_WithForceFalse_AndStartDateInPast_ThrowsException()
{
    // Arrange
    var editionId = Guid.NewGuid();
    var edition = new CampEdition
    {
        Id = editionId,
        CampId = Guid.NewGuid(),
        Year = 2026,
        Status = CampEditionStatus.Draft,
        StartDate = DateTime.UtcNow.AddDays(-3), // Past start date
        EndDate = DateTime.UtcNow.AddDays(5),
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m
    };

    _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act & Assert — force=false keeps the date constraint active
    var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Open, force: false);
    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*No se puede abrir el registro*");
}

[Fact]
public async Task ChangeStatusAsync_WhenClosedToDraft_ThrowsInvalidTransitionException()
{
    // Arrange
    var editionId = Guid.NewGuid();
    var edition = new CampEdition
    {
        Id = editionId,
        CampId = Guid.NewGuid(),
        Year = 2026,
        Status = CampEditionStatus.Closed,
        StartDate = DateTime.UtcNow.AddDays(-10),
        EndDate = DateTime.UtcNow.AddDays(-1),
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m
    };

    _repository.GetByIdAsync(editionId, Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act & Assert — Closed → Draft is never valid (only Open → Draft is the new backward transition)
    var act = async () => await _sut.ChangeStatusAsync(editionId, CampEditionStatus.Draft, force: false);
    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*La transición de 'Closed' a 'Draft' no es válida*");
}
```

---

### Step 2: Update `ChangeEditionStatusRequest` DTO

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add `Force = false` optional parameter to the record

**Find** (lines 434–437):
```csharp
/// <summary>
/// Request to change the status of a camp edition
/// </summary>
public record ChangeEditionStatusRequest(
    CampEditionStatus Status
);
```

**Replace with**:
```csharp
/// <summary>
/// Request to change the status of a camp edition
/// </summary>
public record ChangeEditionStatusRequest(
    CampEditionStatus Status,
    bool Force = false   // Admin-only: bypasses startDate < today constraint when re-opening
);
```

- **No validator changes needed** — `Force` is a `bool`, any value is valid. The `ChangeEditionStatusRequestValidator` only validates `Status` and remains unchanged.

---

### Step 3: Update `CampEditionsService.ChangeStatusAsync` and `ValidateStatusTransition`

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: Two changes in this file.

#### 3a. `ValidateStatusTransition` — add `Open → Draft`

**Find** (lines 355–369):
```csharp
private static void ValidateStatusTransition(CampEditionStatus current, CampEditionStatus next)
{
    var validTransitions = new Dictionary<CampEditionStatus, CampEditionStatus[]>
    {
        [CampEditionStatus.Proposed]  = [CampEditionStatus.Draft],
        [CampEditionStatus.Draft]     = [CampEditionStatus.Open],
        [CampEditionStatus.Open]      = [CampEditionStatus.Closed],
        [CampEditionStatus.Closed]    = [CampEditionStatus.Completed],
        [CampEditionStatus.Completed] = []
    };

    if (!validTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
        throw new InvalidOperationException(
            $"La transición de '{current}' a '{next}' no es válida");
}
```

**Replace the `Open` entry only** (surgically):
```csharp
[CampEditionStatus.Open]      = [CampEditionStatus.Closed, CampEditionStatus.Draft],
```

#### 3b. `ChangeStatusAsync` — add `force` parameter

**Find** (lines 167–183):
```csharp
public async Task<CampEditionResponse> ChangeStatusAsync(
    Guid editionId,
    CampEditionStatus newStatus,
    CancellationToken cancellationToken = default)
{
    var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
    if (edition == null)
        throw new InvalidOperationException("La edición de campamento no fue encontrada");

    ValidateStatusTransition(edition.Status, newStatus);
    ValidateDateConstraintsForTransition(edition, newStatus);

    edition.Status = newStatus;
    var updated = await _repository.UpdateAsync(edition, cancellationToken);
    return MapToCampEditionResponse(updated, updated.Camp.Name);
}
```

**Replace with**:
```csharp
public async Task<CampEditionResponse> ChangeStatusAsync(
    Guid editionId,
    CampEditionStatus newStatus,
    bool force,
    CancellationToken cancellationToken = default)
{
    var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
    if (edition == null)
        throw new InvalidOperationException("La edición de campamento no fue encontrada");

    ValidateStatusTransition(edition.Status, newStatus);

    if (!force)
        ValidateDateConstraintsForTransition(edition, newStatus);

    edition.Status = newStatus;
    var updated = await _repository.UpdateAsync(edition, cancellationToken);
    return MapToCampEditionResponse(updated, updated.Camp.Name);
}
```

- **Implementation Notes**:
  - `force = true` skips ALL date constraint checks (both `startDate < today` for `→ Open` and `endDate >= today` for `→ Completed`). This is intentional — the only real-world use case for `force` is re-opening an in-progress edition, and Admins are trusted.
  - The role enforcement is done in the endpoint (Step 4), not here. The service is role-agnostic.

---

### Step 4: Update `ChangeEditionStatus` Endpoint Handler

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`
- **Action**: Add `ClaimsPrincipal user` parameter, Admin-only guards, and pass `force` to the service.

**Find** (lines 592–613):
```csharp
/// <summary>
/// Change the status of a camp edition
/// </summary>
private static async Task<IResult> ChangeEditionStatus(
    [FromServices] CampEditionsService service,
    Guid id,
    ChangeEditionStatusRequest request,
    CancellationToken cancellationToken = default)
{
    try
    {
        var edition = await service.ChangeStatusAsync(id, request.Status, cancellationToken);
        return Results.Ok(ApiResponse<CampEditionResponse>.Ok(edition));
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("no fue encontrada"))
    {
        return Results.NotFound(ApiResponse<CampEditionResponse>.NotFound(ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "OPERATION_ERROR"));
    }
}
```

**Replace with**:
```csharp
/// <summary>
/// Change the status of a camp edition
/// </summary>
private static async Task<IResult> ChangeEditionStatus(
    [FromServices] CampEditionsService service,
    Guid id,
    ChangeEditionStatusRequest request,
    ClaimsPrincipal user,
    CancellationToken cancellationToken = default)
{
    var isAdmin = user.IsInRole("Admin");

    // Open → Draft is Admin-only: fetch current status to verify the role guard
    if (request.Status == CampEditionStatus.Draft)
    {
        var current = await service.GetByIdAsync(id, cancellationToken);
        if (current?.Status == CampEditionStatus.Open && !isAdmin)
            return Results.Forbid();
    }

    // force flag is Admin-only
    if (request.Force && !isAdmin)
        return Results.Forbid();

    try
    {
        var edition = await service.ChangeStatusAsync(id, request.Status, request.Force, cancellationToken);
        return Results.Ok(ApiResponse<CampEditionResponse>.Ok(edition));
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("no fue encontrada"))
    {
        return Results.NotFound(ApiResponse<CampEditionResponse>.NotFound(ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "OPERATION_ERROR"));
    }
}
```

- **Implementation Notes**:
  - `ClaimsPrincipal user` is a first-class Minimal API parameter — ASP.NET Core resolves it automatically from the `HttpContext`. No `[FromServices]` needed. This pattern is already used in `UpdateAgeRanges` (line 490).
  - The pre-fetch via `service.GetByIdAsync` is necessary because the 403 must be returned **before** calling `ChangeStatusAsync`. It results in one extra DB read, but correctness requires knowing the current status before the service call.
  - If the edition is not found during the pre-fetch (`current == null`), the guard passes (not Admin-restricted) and the subsequent `ChangeStatusAsync` call will throw a 404 through the normal path.
  - `Proposed → Draft` via this endpoint is valid for `Board+`. The guard only triggers when transitioning **to** `Draft` from `Open`.

---

### Step 5 (TDD — GREEN): Run Tests and Verify

- **Action**: Run the unit test suite to confirm all tests pass (both existing and new)
- **Command**: `dotnet test src/Abuvi.Tests/ --filter "Category=Unit" --no-build` or simply `dotnet test src/Abuvi.Tests/`
- **Expected result**: All tests pass, including the 4 new test methods

---

### Step 6: Update Documentation

- **File**: `ai-specs/specs/api-spec.yml` — add `force` field to the `ChangeEditionStatusRequest` schema
- **Action**: Locate the `ChangeEditionStatusRequest` schema entry and add the `force` boolean field

Find the schema definition for `ChangeEditionStatusRequest` and add:
```yaml
ChangeEditionStatusRequest:
  type: object
  required:
    - status
  properties:
    status:
      $ref: '#/components/schemas/CampEditionStatus'
    force:
      type: boolean
      default: false
      description: >
        Admin-only flag. When true, bypasses the startDate < today constraint
        when transitioning Draft → Open. Ignored for all other transitions.
        Returns 403 if sent by a non-Admin user.
```

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-open-edition-amendments-backend`
2. **Step 1 (RED)** — Update existing tests (add `force: false`) + add 4 new failing test methods
3. **Step 2** — Update `ChangeEditionStatusRequest` record in `CampsModels.cs` (add `Force = false`)
4. **Step 3** — Update `CampEditionsService.cs`: `ValidateStatusTransition` + `ChangeStatusAsync`
5. **Step 4** — Update `ChangeEditionStatus` handler in `CampsEndpoints.cs`
6. **Step 5 (GREEN)** — Run tests, verify all pass
7. **Step 6** — Update `api-spec.yml`

---

## Testing Checklist

### Unit Tests — `CampEditionsServiceTests.cs`

#### Updated existing tests (signature change — add `force: false`)
- [ ] `ChangeStatusAsync_WithValidTransition_UpdatesStatus` — add `force: false`; add `[InlineData(Open, Draft)]`
- [ ] `ChangeStatusAsync_WithInvalidTransition_ThrowsException` — add `force: false`
- [ ] `ChangeStatusAsync_ToOpen_WithPastStartDate_ThrowsException` — add `force: false`
- [ ] `ChangeStatusAsync_ToCompleted_WithFutureEndDate_ThrowsException` — add `force: false`
- [ ] `ChangeStatusAsync_WithNotFoundEdition_ThrowsException` — add `force: false`

#### New tests
- [ ] `ChangeStatusAsync_WhenOpenToDraft_WithForceFalse_SetsStatusToDraft` — Open→Draft works (no date constraint on →Draft)
- [ ] `ChangeStatusAsync_WhenDraftToOpen_WithForceTrue_AndStartDateInPast_UpdatesStatus` — `force=true` bypasses date check
- [ ] `ChangeStatusAsync_WhenDraftToOpen_WithForceFalse_AndStartDateInPast_ThrowsException` — `force=false` still enforces date check
- [ ] `ChangeStatusAsync_WhenClosedToDraft_ThrowsInvalidTransitionException` — Closed→Draft stays invalid

### Integration Tests

Integration tests are **blocked** by the EF Core provider conflict (see `MEMORY.md`). Role-based authorization for `Open → Draft` and `force` flag must be verified manually with a running instance or deferred until Testcontainers setup is available.

---

## Error Response Format

All errors use `ApiResponse<T>` envelope:

```json
{
  "success": false,
  "error": "OPERATION_ERROR",
  "message": "La transición de 'Closed' a 'Draft' no es válida"
}
```

| Scenario | HTTP Status |
| --- | --- |
| Successful status change | 200 OK |
| Edition not found | 404 Not Found |
| Invalid transition or date constraint | 400 Bad Request |
| Board user attempts `Open → Draft` | 403 Forbidden |
| Board user sends `force: true` | 403 Forbidden |

---

## Dependencies

- **NuGet**: No new packages required
- **Migration**: None — zero schema changes

---

## Notes

1. **TDD is mandatory** — all new tests must be written and confirmed failing (RED) before any production code is changed.
2. **`UpdateAsync` unchanged** — the `Open`-status field restrictions in `CampEditionsService.UpdateAsync` are NOT modified. This is the key benefit of the chosen approach.
3. **`force` semantics** — when `force = true`, ALL date constraints are skipped (both `startDate < today` and `endDate >= today`). In practice, only `Draft → Open` uses `force`, but the implementation is simpler this way.
4. **No validator changes** — `ChangeEditionStatusRequestValidator` only validates `Status` (enum check). `Force` is a `bool` — no validation rule needed.
5. **Double DB read for guard** — the endpoint pre-fetches the edition via `GetByIdAsync` to evaluate the Admin guard before calling `ChangeStatusAsync`. This adds one extra read but is the cleanest way to return 403 vs 400.
6. **Proposed → Draft via endpoint** — the PATCH endpoint handles `Proposed → Draft` too (for `Board+`). The Admin guard only fires when the **current** status is `Open`. The `PromoteEditionToDraft` endpoint (POST `/{id}/promote`) remains unchanged.
7. **Registrations preserved** — rolling back to `Draft` does not delete or modify existing registrations. Families attempting to register while `Draft` will receive a 422 `EDITION_NOT_OPEN` (existing behaviour).

---

## Next Steps After Implementation

- Manual acceptance testing against a running instance to verify 403 role enforcement (Board vs Admin)
- Frontend ticket to expose the `Open → Draft` rollback action in the admin UI (separate ticket)
- Consider Testcontainers setup for role-based integration tests in a future infrastructure ticket

---

## Implementation Verification

- [ ] **Code quality**: No C# analyzer warnings, nullable reference types handled
- [ ] **Functionality**: `Open → Draft` returns 200 for Admin, 403 for Board
- [ ] **Functionality**: `force: true` on `Draft → Open` returns 403 for Board, 200 for Admin when `startDate` in past
- [ ] **Functionality**: `force: false` on `Draft → Open` with `startDate` in past returns 400
- [ ] **Functionality**: `Closed → Draft` still returns 400 for all roles
- [ ] **Testing**: All 5 updated tests pass; all 4 new tests pass; no regressions in full suite
- [ ] **No migration**: `dotnet ef migrations list` shows no pending migrations
- [ ] **Documentation**: `api-spec.yml` updated with `force` field

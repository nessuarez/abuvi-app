# Camp Editions Management — Phase 4: Enriched Implementation Spec

**Source spec:** [camp-editions-management.md](./camp-editions-management.md)
**Status:** ⚡ Ready for implementation
**Date:** 2026-02-17

---

## Context & Current State

Phase 3 (Proposal Workflow) is fully implemented and tested. This spec covers **Phase 4 only**: completing the status state machine, CRUD operations, and the active edition query.

### What already exists (do NOT recreate)

| File | Content |
|------|---------|
| `CampEditionsService.cs` | `ProposeAsync`, `GetProposedAsync`, `PromoteToDraftAsync`, `RejectProposalAsync` |
| `CampEditionsRepository.cs` | `GetByIdAsync`, `GetByStatusAndYearAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` |
| `ICampEditionsRepository.cs` | Interface matching above |
| `CampsEndpoints.cs` | All 4 Phase 3 endpoints |
| `CampsValidators.cs` | `ProposeCampEditionRequestValidator` |
| `CampsModels.cs` | `CampEdition`, `CampEditionStatus`, `CampEditionResponse`, `UpdateCampEditionRequest`, `ProposeCampEditionRequest` |

### Critical design notes from the codebase

1. **`Rejected` is NOT in `CampEditionStatus` enum.** Rejection is handled with `IsArchived = true`. Do NOT add a `Rejected` enum value — it would break existing data.
2. **Dates use `DateTime`**, not `DateOnly`. Match existing models.
3. **`GetByStatusAndYearAsync` already filters by status + year** — reuse for the active edition query.
4. **Service uses old-style constructor** (not primary constructor). Match it for consistency.
5. **Validation messages must be in Spanish** (per backend-standards) for user-facing errors.
6. **Endpoints use try/catch** (not global exception middleware). Match the existing pattern.

---

## Implementation Tasks

### Task 1: Add `ChangeStatusRequest` DTO to `CampsModels.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add after `UpdateCampEditionRequest`:

```csharp
/// <summary>
/// Request to change the status of a camp edition
/// </summary>
public record ChangeEditionStatusRequest(
    CampEditionStatus Status
);
```

Also add `RegistrationCount` to `CampEditionResponse` for the active edition endpoint (Phase 4.4). However, since this is a breaking change, instead add a new DTO:

```csharp
/// <summary>
/// Active edition response including registration statistics
/// </summary>
public record ActiveCampEditionResponse(
    Guid Id,
    Guid CampId,
    string CampName,
    string? CampLocation,
    string? CampFormattedAddress,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    CampEditionStatus Status,
    int? MaxCapacity,
    int RegistrationCount,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

---

### Task 2: Extend `ICampEditionsRepository` with new methods

**File:** `src/Abuvi.API/Features/Camps/ICampEditionsRepository.cs`

Add these methods to the interface:

```csharp
/// <summary>
/// Checks if an edition already exists for a given camp and year (excludes archived)
/// </summary>
Task<bool> ExistsAsync(Guid campId, int year, CancellationToken cancellationToken = default);

/// <summary>
/// Gets all editions filtered by optional year, status, and campId
/// </summary>
Task<List<CampEdition>> GetAllAsync(
    int? year = null,
    CampEditionStatus? status = null,
    Guid? campId = null,
    CancellationToken cancellationToken = default);
```

> **Note:** `GetActiveEditionAsync` in the service can reuse the existing `GetByStatusAndYearAsync`. No new repository method needed.

---

### Task 3: Implement new repository methods

**File:** `src/Abuvi.API/Features/Camps/CampEditionsRepository.cs`

```csharp
public async Task<bool> ExistsAsync(Guid campId, int year, CancellationToken cancellationToken = default)
{
    return await _context.CampEditions
        .AnyAsync(e => e.CampId == campId && e.Year == year && !e.IsArchived, cancellationToken);
}

public async Task<List<CampEdition>> GetAllAsync(
    int? year = null,
    CampEditionStatus? status = null,
    Guid? campId = null,
    CancellationToken cancellationToken = default)
{
    var query = _context.CampEditions
        .AsNoTracking()
        .Include(e => e.Camp)
        .Where(e => !e.IsArchived);

    if (year.HasValue)
        query = query.Where(e => e.Year == year.Value);

    if (status.HasValue)
        query = query.Where(e => e.Status == status.Value);

    if (campId.HasValue)
        query = query.Where(e => e.CampId == campId.Value);

    return await query
        .OrderByDescending(e => e.Year)
        .ThenBy(e => e.StartDate)
        .ToListAsync(cancellationToken);
}
```

---

### Task 4: Implement new service methods in `CampEditionsService.cs`

**File:** `src/Abuvi.API/Features/Camps/CampEditionsService.cs`

Add these methods to the existing `CampEditionsService` class.

#### 4a. `ChangeStatusAsync`

```csharp
/// <summary>
/// Changes the status of a camp edition, enforcing valid transitions.
/// </summary>
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

private static void ValidateStatusTransition(CampEditionStatus current, CampEditionStatus next)
{
    var validTransitions = new Dictionary<CampEditionStatus, CampEditionStatus[]>
    {
        [CampEditionStatus.Proposed] = [CampEditionStatus.Draft],
        [CampEditionStatus.Draft]    = [CampEditionStatus.Open],
        [CampEditionStatus.Open]     = [CampEditionStatus.Closed],
        [CampEditionStatus.Closed]   = [CampEditionStatus.Completed],
        [CampEditionStatus.Completed] = []
    };

    if (!validTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
        throw new InvalidOperationException(
            $"La transición de '{current}' a '{next}' no es válida");
}

private static void ValidateDateConstraintsForTransition(CampEdition edition, CampEditionStatus newStatus)
{
    var today = DateTime.UtcNow.Date;

    if (newStatus == CampEditionStatus.Open && edition.StartDate.Date < today)
        throw new InvalidOperationException(
            "No se puede abrir el registro de una edición con fecha de inicio en el pasado");

    if (newStatus == CampEditionStatus.Completed && edition.EndDate.Date >= today)
        throw new InvalidOperationException(
            "No se puede marcar como completada una edición cuya fecha de fin no ha pasado");
}
```

#### 4b. `UpdateAsync`

```csharp
/// <summary>
/// Updates a camp edition. Restrictions apply based on status.
/// - Proposed/Draft: all fields
/// - Open: only Notes and MaxCapacity
/// - Closed/Completed: no updates allowed
/// </summary>
public async Task<CampEditionResponse> UpdateAsync(
    Guid editionId,
    UpdateCampEditionRequest request,
    CancellationToken cancellationToken = default)
{
    var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
    if (edition == null)
        throw new InvalidOperationException("La edición de campamento no fue encontrada");

    if (edition.Status is CampEditionStatus.Closed or CampEditionStatus.Completed)
        throw new InvalidOperationException(
            "No se puede modificar una edición cerrada o completada");

    if (edition.Status == CampEditionStatus.Open)
    {
        // Only allow notes and capacity changes
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

    edition.StartDate = request.StartDate;
    edition.EndDate = request.EndDate;
    edition.PricePerAdult = request.PricePerAdult;
    edition.PricePerChild = request.PricePerChild;
    edition.PricePerBaby = request.PricePerBaby;
    edition.UseCustomAgeRanges = request.UseCustomAgeRanges;
    edition.CustomBabyMaxAge = request.CustomBabyMaxAge;
    edition.CustomChildMinAge = request.CustomChildMinAge;
    edition.CustomChildMaxAge = request.CustomChildMaxAge;
    edition.CustomAdultMinAge = request.CustomAdultMinAge;
    edition.MaxCapacity = request.MaxCapacity;
    edition.Notes = request.Notes;

    var updated = await _repository.UpdateAsync(edition, cancellationToken);
    return MapToCampEditionResponse(updated, updated.Camp.Name);
}
```

#### 4c. `GetByIdAsync`

```csharp
/// <summary>
/// Gets a camp edition by ID. Board+ can see all; Members can see Open, Closed, Completed only.
/// Authorization is enforced at the endpoint level.
/// </summary>
public async Task<CampEditionResponse?> GetByIdAsync(
    Guid editionId,
    CancellationToken cancellationToken = default)
{
    var edition = await _repository.GetByIdAsync(editionId, cancellationToken);
    if (edition == null)
        return null;

    return MapToCampEditionResponse(edition, edition.Camp.Name);
}
```

#### 4d. `GetAllAsync`

```csharp
/// <summary>
/// Gets all camp editions with optional filtering by year, status, and campId.
/// Board+ only.
/// </summary>
public async Task<List<CampEditionResponse>> GetAllAsync(
    int? year,
    CampEditionStatus? status,
    Guid? campId,
    CancellationToken cancellationToken = default)
{
    var editions = await _repository.GetAllAsync(year, status, campId, cancellationToken);
    return editions.Select(e => MapToCampEditionResponse(e, e.Camp.Name)).ToList();
}
```

#### 4e. `GetActiveEditionAsync`

```csharp
/// <summary>
/// Gets the active (Open) edition for the given year (defaults to current year).
/// Returns null if no Open edition exists.
/// </summary>
public async Task<ActiveCampEditionResponse?> GetActiveEditionAsync(
    int? year,
    CancellationToken cancellationToken = default)
{
    var targetYear = year ?? DateTime.UtcNow.Year;

    var editions = await _repository.GetByStatusAndYearAsync(
        CampEditionStatus.Open,
        targetYear,
        cancellationToken);

    var edition = editions.FirstOrDefault();
    if (edition == null)
        return null;

    // Registration count will be 0 until the Registrations feature is integrated.
    // The field is present in the response for forward compatibility.
    return new ActiveCampEditionResponse(
        Id: edition.Id,
        CampId: edition.CampId,
        CampName: edition.Camp.Name,
        CampLocation: edition.Camp.Location,
        CampFormattedAddress: edition.Camp.FormattedAddress,
        Year: edition.Year,
        StartDate: edition.StartDate,
        EndDate: edition.EndDate,
        PricePerAdult: edition.PricePerAdult,
        PricePerChild: edition.PricePerChild,
        PricePerBaby: edition.PricePerBaby,
        UseCustomAgeRanges: edition.UseCustomAgeRanges,
        CustomBabyMaxAge: edition.CustomBabyMaxAge,
        CustomChildMinAge: edition.CustomChildMinAge,
        CustomChildMaxAge: edition.CustomChildMaxAge,
        CustomAdultMinAge: edition.CustomAdultMinAge,
        Status: edition.Status,
        MaxCapacity: edition.MaxCapacity,
        RegistrationCount: 0,
        Notes: edition.Notes,
        CreatedAt: edition.CreatedAt,
        UpdatedAt: edition.UpdatedAt
    );
}
```

#### 4f. Duplicate prevention: Add to `ProposeAsync` (existing method)

Add **before** creating the edition in the existing `ProposeAsync` method:

```csharp
// Check for existing non-archived edition for same camp+year
var exists = await _repository.ExistsAsync(request.CampId, request.Year, cancellationToken);
if (exists)
    throw new InvalidOperationException(
        $"Ya existe una edición para este campamento en el año {request.Year}");
```

---

### Task 5: Add validators to `CampsValidators.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsValidators.cs`

Append to the existing file:

```csharp
/// <summary>
/// Validator for UpdateCampEditionRequest
/// </summary>
public class UpdateCampEditionRequestValidator : AbstractValidator<UpdateCampEditionRequest>
{
    public UpdateCampEditionRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("La fecha de inicio es obligatoria");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("La fecha de fin es obligatoria")
            .GreaterThan(x => x.StartDate).WithMessage("La fecha de fin debe ser posterior a la fecha de inicio");

        RuleFor(x => x.PricePerAdult)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por adulto debe ser mayor o igual a 0");

        RuleFor(x => x.PricePerChild)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por niño debe ser mayor o igual a 0");

        RuleFor(x => x.PricePerBaby)
            .GreaterThanOrEqualTo(0).WithMessage("El precio por bebé debe ser mayor o igual a 0");

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).WithMessage("La capacidad máxima debe ser mayor a 0")
            .When(x => x.MaxCapacity.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Las notas no deben superar los 2000 caracteres")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));

        When(x => x.UseCustomAgeRanges, () =>
        {
            RuleFor(x => x.CustomBabyMaxAge)
                .NotNull().WithMessage("La edad máxima de bebé es obligatoria con rangos personalizados");

            RuleFor(x => x.CustomChildMinAge)
                .NotNull().WithMessage("La edad mínima de niño es obligatoria con rangos personalizados");

            RuleFor(x => x.CustomChildMaxAge)
                .NotNull().WithMessage("La edad máxima de niño es obligatoria con rangos personalizados");

            RuleFor(x => x.CustomAdultMinAge)
                .NotNull().WithMessage("La edad mínima de adulto es obligatoria con rangos personalizados");

            RuleFor(x => x)
                .Must(x => x.CustomBabyMaxAge!.Value < x.CustomChildMinAge!.Value)
                .WithMessage("La edad máxima de bebé debe ser menor a la edad mínima de niño")
                .WithName("CustomBabyMaxAge");

            RuleFor(x => x)
                .Must(x => x.CustomChildMaxAge!.Value < x.CustomAdultMinAge!.Value)
                .WithMessage("La edad máxima de niño debe ser menor a la edad mínima de adulto")
                .WithName("CustomChildMaxAge");
        });
    }
}

/// <summary>
/// Validator for ChangeEditionStatusRequest
/// </summary>
public class ChangeEditionStatusRequestValidator : AbstractValidator<ChangeEditionStatusRequest>
{
    public ChangeEditionStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("El estado proporcionado no es válido");
    }
}
```

---

### Task 6: Add new endpoints to `CampsEndpoints.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`

Add these endpoints inside `MapCampsEndpoints`, appended to the existing `editionsGroup` block (before `return app;`):

```csharp
// PATCH /api/camps/editions/{id}/status - Change edition status (Board+)
editionsGroup.MapPatch("/{id:guid}/status", ChangeEditionStatus)
    .WithName("ChangeEditionStatus")
    .WithSummary("Change the status of a camp edition")
    .AddEndpointFilter<ValidationFilter<ChangeEditionStatusRequest>>()
    .Produces<ApiResponse<CampEditionResponse>>()
    .Produces(400)
    .Produces(401)
    .Produces(403)
    .Produces(404);

// GET /api/camps/editions/active - Get active (Open) edition (Member+)
editionsGroup.MapGet("/active", GetActiveEdition)
    .WithName("GetActiveEdition")
    .WithSummary("Get the currently open camp edition")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"))
    .Produces<ApiResponse<ActiveCampEditionResponse>>()
    .Produces(401);

// GET /api/camps/editions/{id} - Get edition by ID (Member+ for Open/Closed/Completed)
editionsGroup.MapGet("/{id:guid}", GetEditionById)
    .WithName("GetEditionById")
    .WithSummary("Get a camp edition by ID")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board", "Member"))
    .Produces<ApiResponse<CampEditionResponse>>()
    .Produces(401)
    .Produces(404);

// PUT /api/camps/editions/{id} - Update edition (Board+)
editionsGroup.MapPut("/{id:guid}", UpdateEdition)
    .WithName("UpdateEdition")
    .WithSummary("Update a camp edition")
    .AddEndpointFilter<ValidationFilter<UpdateCampEditionRequest>>()
    .Produces<ApiResponse<CampEditionResponse>>()
    .Produces(400)
    .Produces(401)
    .Produces(403)
    .Produces(404);

// GET /api/camps/editions - List all editions with optional filtering (Board+)
editionsGroup.MapGet("/", GetAllEditions)
    .WithName("GetAllEditions")
    .WithSummary("Get all camp editions with optional filtering")
    .Produces<ApiResponse<List<CampEditionResponse>>>()
    .Produces(401)
    .Produces(403);
```

Add the private handler methods:

```csharp
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
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "OPERATION_ERROR"));
    }
}

private static async Task<IResult> GetActiveEdition(
    [FromServices] CampEditionsService service,
    [FromQuery] int? year = null,
    CancellationToken cancellationToken = default)
{
    var edition = await service.GetActiveEditionAsync(year, cancellationToken);
    return Results.Ok(ApiResponse<ActiveCampEditionResponse?>.Ok(edition));
}

private static async Task<IResult> GetEditionById(
    [FromServices] CampEditionsService service,
    Guid id,
    CancellationToken cancellationToken = default)
{
    var edition = await service.GetByIdAsync(id, cancellationToken);
    if (edition == null)
        return Results.NotFound(ApiResponse<CampEditionResponse>.NotFound("La edición de campamento no fue encontrada"));

    return Results.Ok(ApiResponse<CampEditionResponse>.Ok(edition));
}

private static async Task<IResult> UpdateEdition(
    [FromServices] CampEditionsService service,
    Guid id,
    UpdateCampEditionRequest request,
    CancellationToken cancellationToken = default)
{
    try
    {
        var edition = await service.UpdateAsync(id, request, cancellationToken);
        return Results.Ok(ApiResponse<CampEditionResponse>.Ok(edition));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ApiResponse<CampEditionResponse>.Fail(ex.Message, "OPERATION_ERROR"));
    }
}

private static async Task<IResult> GetAllEditions(
    [FromServices] CampEditionsService service,
    [FromQuery] int? year = null,
    [FromQuery] CampEditionStatus? status = null,
    [FromQuery] Guid? campId = null,
    CancellationToken cancellationToken = default)
{
    var editions = await service.GetAllAsync(year, status, campId, cancellationToken);
    return Results.Ok(ApiResponse<List<CampEditionResponse>>.Ok(editions));
}
```

---

## Test Files

### Unit Tests — Service

**File:** `tests/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs`

> **Note:** No test file exists yet — create it fresh.

#### Setup pattern (match existing project style)

```csharp
namespace Abuvi.Tests.Unit.Features.Camps;

public class CampEditionsServiceTests
{
    private readonly ICampEditionsRepository _repository;
    private readonly ICampsRepository _campsRepository;
    private readonly CampEditionsService _sut;

    public CampEditionsServiceTests()
    {
        _repository = Substitute.For<ICampEditionsRepository>();
        _campsRepository = Substitute.For<ICampsRepository>();
        _sut = new CampEditionsService(_repository, _campsRepository);
    }
    // ...
}
```

#### Status transition tests — `ChangeStatusAsync`

| Test method | Description |
|---|---|
| `ChangeStatusAsync_ProposedToDraft_ChangesStatus` | Valid |
| `ChangeStatusAsync_DraftToOpen_ChangesStatus` | Valid |
| `ChangeStatusAsync_OpenToClosed_ChangesStatus` | Valid |
| `ChangeStatusAsync_ClosedToCompleted_ChangesStatus` | Valid |
| `ChangeStatusAsync_OpenToDraft_ThrowsInvalidOperationException` | Invalid backward |
| `ChangeStatusAsync_CompletedToOpen_ThrowsInvalidOperationException` | Invalid backward |
| `ChangeStatusAsync_ProposedToOpen_ThrowsInvalidOperationException` | Skip transition |
| `ChangeStatusAsync_WhenEditionNotFound_ThrowsInvalidOperationException` | Not found |
| `ChangeStatusAsync_DraftToOpen_WhenStartDateInPast_ThrowsInvalidOperationException` | Date constraint |
| `ChangeStatusAsync_ClosedToCompleted_WhenEndDateInFuture_ThrowsInvalidOperationException` | Date constraint |

#### UpdateAsync tests

| Test method | Description |
|---|---|
| `UpdateAsync_WhenStatusIsDraft_UpdatesAllFields` | Full update |
| `UpdateAsync_WhenStatusIsOpen_UpdatesOnlyNotesAndCapacity` | Restricted update |
| `UpdateAsync_WhenStatusIsOpen_AttemptChangeDates_ThrowsInvalidOperationException` | Date change blocked |
| `UpdateAsync_WhenStatusIsOpen_AttemptChangePrices_ThrowsInvalidOperationException` | Price change blocked |
| `UpdateAsync_WhenStatusIsClosed_ThrowsInvalidOperationException` | Read-only |
| `UpdateAsync_WhenStatusIsCompleted_ThrowsInvalidOperationException` | Read-only |
| `UpdateAsync_WhenEditionNotFound_ThrowsInvalidOperationException` | Not found |

#### GetActiveEditionAsync tests

| Test method | Description |
|---|---|
| `GetActiveEditionAsync_WhenOpenEditionExists_ReturnsEdition` | Happy path |
| `GetActiveEditionAsync_WhenNoOpenEdition_ReturnsNull` | No result |
| `GetActiveEditionAsync_UsesCurrentYearWhenNotProvided` | Default year |
| `GetActiveEditionAsync_UsesCampLocationDetails` | Camp details in response |

#### ProposeAsync duplicate prevention

| Test method | Description |
|---|---|
| `ProposeAsync_WhenEditionAlreadyExistsForCampAndYear_ThrowsInvalidOperationException` | Duplicate blocked |
| `ProposeAsync_WhenEditionExistsButArchived_AllowsCreation` | Archived doesn't block |

### Unit Tests — Validators

**File:** `tests/Abuvi.Tests/Unit/Features/Camps/Validators/UpdateCampEditionValidatorTests.cs`

| Test method | Description |
|---|---|
| `Validate_ValidRequest_PassesValidation` | Happy path |
| `Validate_WhenEndDateBeforeStartDate_FailsValidation` | Date order |
| `Validate_WhenPriceIsNegative_FailsValidation` | Price range |
| `Validate_WhenNotesTooLong_FailsValidation` | Length limit |
| `Validate_WhenCustomAgeRangesEnabled_RequiresAllAgeFields` | Conditional required |
| `Validate_WhenCustomBabyAgeExceedsChildAge_FailsValidation` | Age range logic |

**File:** `tests/Abuvi.Tests/Unit/Features/Camps/Validators/ChangeEditionStatusValidatorTests.cs`

| Test method | Description |
|---|---|
| `Validate_ValidStatus_PassesValidation` | Valid enum value |
| `Validate_InvalidStatus_FailsValidation` | Out-of-range enum |

### Integration Tests — Endpoints

**File:** `tests/Abuvi.Tests/Integration/Features/Camps/CampEditionsEndpointsTests.cs`

Minimum integration tests (using `WebApplicationFactory`):

| Test method | Auth | Expected status |
|---|---|---|
| `PatchStatus_AsBoard_WithValidTransition_Returns200` | Board | 200 |
| `PatchStatus_AsMember_Returns403` | Member | 403 |
| `PatchStatus_WithInvalidTransition_Returns400` | Board | 400 |
| `GetActive_AsMember_Returns200OrNull` | Member | 200 |
| `GetActive_Unauthenticated_Returns401` | None | 401 |
| `GetById_AsMember_ForOpenEdition_Returns200` | Member | 200 |
| `PutEdition_AsBoard_WithValidData_Returns200` | Board | 200 |
| `PutEdition_AsMember_Returns403` | Member | 403 |
| `GetAll_AsBoard_Returns200` | Board | 200 |
| `GetAll_AsMember_Returns403` | Member | 403 |

---

## Complete API Surface

### Existing (Phase 3) ✅

| Method | Endpoint | Auth |
|--------|----------|------|
| POST | `/api/camps/editions/propose` | Board+ |
| GET | `/api/camps/editions/proposed?year={year}` | Board+ |
| POST | `/api/camps/editions/{id}/promote` | Board+ |
| DELETE | `/api/camps/editions/{id}/reject` | Board+ |

### New (Phase 4) — this ticket

| Method | Endpoint | Auth |
|--------|----------|------|
| PATCH | `/api/camps/editions/{id}/status` | Board+ |
| GET | `/api/camps/editions/active?year={year}` | Member+ |
| GET | `/api/camps/editions/{id}` | Member+ |
| PUT | `/api/camps/editions/{id}` | Board+ |
| GET | `/api/camps/editions?year={year}&status={status}&campId={guid}` | Board+ |

---

## Status Machine — Valid Transitions

```
Proposed → Draft    (via promote endpoint — already implemented)
Draft    → Open     (via PATCH /status)
Open     → Closed   (via PATCH /status)
Closed   → Completed (via PATCH /status)
```

**All other transitions must throw `InvalidOperationException`** with Spanish message.

**Date constraints on PATCH /status:**
- `Draft → Open`: `StartDate.Date >= DateTime.UtcNow.Date`
- `Closed → Completed`: `EndDate.Date < DateTime.UtcNow.Date`

---

## Acceptance Criteria

- [ ] `PATCH /api/camps/editions/{id}/status` — changes status with transition validation
- [ ] `GET /api/camps/editions/active` — returns open edition with camp location details, accessible to Member+
- [ ] `GET /api/camps/editions/{id}` — returns edition by ID, accessible to Member+
- [ ] `PUT /api/camps/editions/{id}` — updates edition with status-based field restrictions
- [ ] `GET /api/camps/editions` — lists editions with optional year/status/campId filter, Board+ only
- [ ] Backward transitions blocked with Spanish error messages
- [ ] Skip transitions blocked (e.g., `Proposed → Open`)
- [ ] Cannot re-propose a camp edition for same `(camp_id, year)` combination (unless archived)
- [ ] Cannot change dates/prices on Open editions
- [ ] Cannot update Closed or Completed editions at all
- [ ] Date constraints enforced on `Open` and `Completed` transitions
- [ ] All validators in Spanish
- [ ] `RegistrationCount = 0` placeholder included in `ActiveCampEditionResponse` for future integration
- [ ] All new endpoints documented in Swagger (`WithSummary`)
- [ ] Unit tests cover all valid and invalid transition combinations
- [ ] Integration tests cover auth scenarios for each new endpoint
- [ ] All tests pass (`dotnet test`)

---

## Out of Scope for This Ticket

- Real `RegistrationCount` from the registrations table (future spec)
- Pagination on `GET /api/camps/editions` (can be added later; not needed for current scale)
- Email notifications on status change (future spec)
- `GET /api/camps/editions/{id}` role-based field filtering (all fields returned regardless; future hardening)

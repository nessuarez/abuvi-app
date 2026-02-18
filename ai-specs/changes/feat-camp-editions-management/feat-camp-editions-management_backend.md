# Backend Implementation Plan: feat-camp-editions-management — Phase 4: Edition Lifecycle Management

## Overview

This plan covers Phase 4 of Camp Editions Management: completing the status state machine, full CRUD operations, and the active edition query. Phase 3 (Proposal Workflow) is already implemented and functional.

The feature follows **Vertical Slice Architecture** — all changes are confined to `src/Abuvi.API/Features/Camps/` with corresponding tests in `tests/Abuvi.Tests/`.

**No EF Core migration is needed** — the `camp_editions` table already exists from the `AddCampsAndSettings` migration.

**No Program.cs changes are needed** — `CampEditionsService` and `ICampEditionsRepository` are already registered as scoped services.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

| File | Change |
|------|--------|
| `CampsModels.cs` | Add 2 new DTOs: `ChangeEditionStatusRequest`, `ActiveCampEditionResponse` |
| `ICampEditionsRepository.cs` | Add 2 new interface methods: `ExistsAsync`, `GetAllAsync` |
| `CampEditionsRepository.cs` | Implement 2 new methods |
| `CampEditionsService.cs` | Add 5 new methods + 2 private helpers + patch `ProposeAsync` |
| `CampsValidators.cs` | Append 2 new validators |
| `CampsEndpoints.cs` | Add 5 new endpoints + 5 private handlers |

**Test files (create fresh):**

| File | Type |
|------|------|
| `tests/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs` | Unit — Service |
| `tests/Abuvi.Tests/Unit/Features/Camps/Validators/UpdateCampEditionValidatorTests.cs` | Unit — Validator |
| `tests/Abuvi.Tests/Unit/Features/Camps/Validators/ChangeEditionStatusValidatorTests.cs` | Unit — Validator |
| `tests/Abuvi.Tests/Integration/Features/Camps/CampEditionsEndpointsTests.cs` | Integration |

**Cross-cutting concerns:** None. The `ValidationFilter<T>` and `ApiResponse<T>` patterns are already in use within the Camps slice.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Switch to a dedicated backend branch
- **Branch name**: `feature/feat-camp-editions-management-backend`
- **Implementation steps**:
  1. Ensure you are on `main` (or the current integration branch): `git checkout main && git pull origin main`
  2. If working from the existing feature branch: `git checkout feature/feat-camps-extra-data-backend && git pull`
  3. Create the new branch: `git checkout -b feature/feat-camp-editions-management-backend`
  4. Confirm: `git branch`
- **Notes**: Do not commit code to the existing `feature/feat-camps-extra-data-backend` branch. This work lives in its own branch.

---

### Step 1: Add New DTOs to `CampsModels.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Append two new record types after the existing `UpdateCampEditionRequest` record
- **Implementation steps**:
  1. Add `ChangeEditionStatusRequest` record with a single `CampEditionStatus Status` property
  2. Add `ActiveCampEditionResponse` record — a richer variant of `CampEditionResponse` that includes camp location data (`CampLocation`, `CampFormattedAddress`) and a `RegistrationCount` integer (set to `0` as a placeholder until the Registrations feature is integrated)
- **Implementation notes**:
  - Use `record` types (immutable) per project standards
  - Dates must remain `DateTime` (NOT `DateOnly`) — the existing entity uses `DateTime`
  - `RegistrationCount` is `int` (not nullable) and always `0` for now; include a code comment explaining the placeholder
  - Do NOT modify `CampEditionResponse` — adding fields there would be a breaking change for Phase 3 callers

---

### Step 2: Extend `ICampEditionsRepository`

- **File**: `src/Abuvi.API/Features/Camps/ICampEditionsRepository.cs`
- **Action**: Add two method signatures to the interface
- **Implementation steps**:
  1. Add `Task<bool> ExistsAsync(Guid campId, int year, CancellationToken cancellationToken = default)` — checks for a non-archived edition matching the given `(campId, year)` pair
  2. Add `Task<List<CampEdition>> GetAllAsync(int? year, CampEditionStatus? status, Guid? campId, CancellationToken cancellationToken = default)` — returns filtered list of non-archived editions
- **Implementation notes**:
  - Keep the existing `GetByStatusAndYearAsync` — it will be reused by the service's `GetActiveEditionAsync` without changes
  - XML doc comments on each new method are required for consistency with existing interface documentation

---

### Step 3: Implement New Repository Methods

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsRepository.cs`
- **Action**: Implement the two new interface methods
- **Implementation steps**:

  **`ExistsAsync`:**
  1. Query `_context.CampEditions` with `.AnyAsync()`
  2. Filter by `e.CampId == campId && e.Year == year && !e.IsArchived`
  3. Return the boolean result

  **`GetAllAsync`:**
  1. Start from `_context.CampEditions.AsNoTracking().Include(e => e.Camp).Where(e => !e.IsArchived)`
  2. Conditionally apply `.Where(e => e.Year == year.Value)` if `year.HasValue`
  3. Conditionally apply `.Where(e => e.Status == status.Value)` if `status.HasValue`
  4. Conditionally apply `.Where(e => e.CampId == campId.Value)` if `campId.HasValue`
  5. Order by `e.Year` descending, then by `e.StartDate` ascending
  6. Return `.ToListAsync(cancellationToken)`

- **Implementation notes**:
  - Use `AsNoTracking()` for all read queries (read-only path)
  - Always `Include(e => e.Camp)` so the service can access `Camp.Name` without a second query
  - Match the existing repository constructor style (old-style, not primary constructor)

---

### Step 4: Implement New Service Methods

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: Add 5 public methods and 2 private helpers; also patch the existing `ProposeAsync`

#### 4a. Patch `ProposeAsync` — Duplicate Prevention

Add before the `CampEdition` object construction in the existing method:

```
ExistsAsync check → if exists → throw InvalidOperationException (Spanish)
```

Error message: `"Ya existe una edición para este campamento en el año {request.Year}"`

#### 4b. Add `ChangeStatusAsync(Guid, CampEditionStatus, CancellationToken)`

**Logic:**
1. Fetch edition by ID → throw if null (Spanish message)
2. Call `ValidateStatusTransition(edition.Status, newStatus)` (private helper)
3. Call `ValidateDateConstraintsForTransition(edition, newStatus)` (private helper)
4. Set `edition.Status = newStatus`
5. Call `_repository.UpdateAsync(edition, ct)`
6. Return `MapToCampEditionResponse(updated, updated.Camp.Name)`

#### 4c. Add `ValidateStatusTransition(CampEditionStatus, CampEditionStatus)` — private static

Build a `Dictionary<CampEditionStatus, CampEditionStatus[]>` of valid transitions:
- `Proposed → [Draft]`
- `Draft → [Open]`
- `Open → [Closed]`
- `Closed → [Completed]`
- `Completed → []`

Throw `InvalidOperationException` with Spanish message if the transition is not found.

> **Note**: `Proposed → Draft` also happens via the existing `PromoteToDraftAsync`. Both paths are valid since `PromoteToDraftAsync` already enforces the source status internally. `ChangeStatusAsync` allows the same transition for completeness, but the promote endpoint remains the canonical path.

#### 4d. Add `ValidateDateConstraintsForTransition(CampEdition, CampEditionStatus)` — private static

- `Draft → Open`: `edition.StartDate.Date < DateTime.UtcNow.Date` → throw (Spanish)
- `Closed → Completed`: `edition.EndDate.Date >= DateTime.UtcNow.Date` → throw (Spanish)
- All other transitions: no date check

#### 4e. Add `UpdateAsync(Guid, UpdateCampEditionRequest, CancellationToken)`

**Logic:**
1. Fetch edition by ID → throw if null
2. If status is `Closed` or `Completed` → throw (cannot update read-only editions)
3. If status is `Open`:
   - Check if `StartDate`, `EndDate`, `PricePerAdult`, `PricePerChild`, or `PricePerBaby` changed vs current values → throw if any changed
4. Apply all fields from `request` onto `edition`
5. `UpdateAsync` + return mapped response

#### 4f. Add `GetByIdAsync(Guid, CancellationToken)` → `CampEditionResponse?`

1. Call `_repository.GetByIdAsync(editionId, ct)`
2. Return `null` if not found
3. Return `MapToCampEditionResponse(edition, edition.Camp.Name)`

#### 4g. Add `GetAllAsync(int?, CampEditionStatus?, Guid?, CancellationToken)` → `List<CampEditionResponse>`

1. Call `_repository.GetAllAsync(year, status, campId, ct)`
2. Project with `.Select(e => MapToCampEditionResponse(e, e.Camp.Name)).ToList()`

#### 4h. Add `GetActiveEditionAsync(int?, CancellationToken)` → `ActiveCampEditionResponse?`

1. Resolve `targetYear = year ?? DateTime.UtcNow.Year`
2. Call existing `_repository.GetByStatusAndYearAsync(CampEditionStatus.Open, targetYear, ct)`
3. Take `.FirstOrDefault()` — return `null` if none
4. Construct and return `new ActiveCampEditionResponse(...)` with `RegistrationCount = 0`

- **Implementation notes**:
  - All user-facing error messages must be in **Spanish**
  - Match the existing old-style constructor pattern of the service class
  - Do NOT change the existing `MapToCampEditionResponse` private method

---

### Step 5: Add Validators to `CampsValidators.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsValidators.cs`
- **Action**: Append two new validators at the bottom of the file

#### `UpdateCampEditionRequestValidator : AbstractValidator<UpdateCampEditionRequest>`

Rules:
- `StartDate`: `NotEmpty` — "La fecha de inicio es obligatoria"
- `EndDate`: `NotEmpty` + `GreaterThan(x => x.StartDate)` — "La fecha de fin debe ser posterior a la fecha de inicio"
- `PricePerAdult/Child/Baby`: `GreaterThanOrEqualTo(0)` — Spanish messages
- `MaxCapacity`: `GreaterThan(0)` when provided — "La capacidad máxima debe ser mayor a 0"
- `Notes`: `MaximumLength(2000)` when not empty — "Las notas no deben superar los 2000 caracteres"
- `When(UseCustomAgeRanges)`: all four custom age fields `NotNull` + logical range checks, Spanish messages

#### `ChangeEditionStatusRequestValidator : AbstractValidator<ChangeEditionStatusRequest>`

Rules:
- `Status`: `IsInEnum()` — "El estado proporcionado no es válido"

- **Implementation notes**:
  - Follow the pattern of the existing `ProposeCampEditionRequestValidator` already in this file
  - All `.WithMessage()` strings must be in Spanish
  - Feminine/masculine noun agreement: "La fecha" (feminine), "El precio" (masculine), "La capacidad" (feminine)

---

### Step 6: Add New Endpoints to `CampsEndpoints.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`
- **Action**: Add 5 endpoint registrations and 5 private static handler methods

#### Endpoint registrations (append to the `editionsGroup` block, before `return app;`)

| Method | Route | Handler | Auth | Filter |
|--------|-------|---------|------|--------|
| `PATCH` | `/{id:guid}/status` | `ChangeEditionStatus` | Board+ (inherited from group) | `ValidationFilter<ChangeEditionStatusRequest>` |
| `GET` | `/active` | `GetActiveEdition` | Member+ (override with `RequireAuthorization`) | — |
| `GET` | `/{id:guid}` | `GetEditionById` | Member+ (override with `RequireAuthorization`) | — |
| `PUT` | `/{id:guid}` | `UpdateEdition` | Board+ (inherited) | `ValidationFilter<UpdateCampEditionRequest>` |
| `GET` | `/` | `GetAllEditions` | Board+ (inherited) | — |

> **Route ordering note**: `GET /active` must be registered **before** `GET /{id:guid}` to prevent ASP.NET Core from treating "active" as a GUID. Register `/active` first.

#### Private handler signatures and behavior

**`ChangeEditionStatus`**: `(CampEditionsService, Guid id, ChangeEditionStatusRequest, CancellationToken)` → `Results.Ok` or `Results.BadRequest` on `InvalidOperationException`

**`GetActiveEdition`**: `(CampEditionsService, [FromQuery] int? year, CancellationToken)` → always `Results.Ok(...)` with nullable data (returns `{ success: true, data: null }` when no active edition)

**`GetEditionById`**: `(CampEditionsService, Guid id, CancellationToken)` → `Results.Ok` or `Results.NotFound`

**`UpdateEdition`**: `(CampEditionsService, Guid id, UpdateCampEditionRequest, CancellationToken)` → `Results.Ok` or `Results.BadRequest` on `InvalidOperationException`

**`GetAllEditions`**: `(CampEditionsService, [FromQuery] int? year, [FromQuery] CampEditionStatus? status, [FromQuery] Guid? campId, CancellationToken)` → `Results.Ok`

- **Implementation notes**:
  - Match the `[FromServices]` attribute pattern used by existing handlers in this file
  - Error messages inside `NotFound(...)` calls must be in Spanish
  - `GetActive` returns 200 with null data when no active edition (not 404) — this makes frontend polling simpler
  - Each endpoint needs `WithName(...)` and `WithSummary(...)` for Swagger documentation

---

### Step 7: Write Unit Tests

- **File**: `tests/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs` *(create new)*
- **Action**: Create the test file with full coverage of all new service methods

#### Test class setup

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
}
```

#### ChangeStatusAsync — status transition tests (use `[Theory][InlineData]`)

| Test name | From | To | Expected |
|---|---|---|---|
| `ChangeStatusAsync_ProposedToDraft_ChangesStatus` | Proposed | Draft | status updated |
| `ChangeStatusAsync_DraftToOpen_ChangesStatus` | Draft | Open | status updated |
| `ChangeStatusAsync_OpenToClosed_ChangesStatus` | Open | Closed | status updated |
| `ChangeStatusAsync_ClosedToCompleted_ChangesStatus` | Closed | Completed | status updated |
| `ChangeStatusAsync_OpenToDraft_ThrowsInvalidOperationException` | Open | Draft | throws |
| `ChangeStatusAsync_CompletedToOpen_ThrowsInvalidOperationException` | Completed | Open | throws |
| `ChangeStatusAsync_ProposedToOpen_ThrowsInvalidOperationException` | Proposed | Open | throws |
| `ChangeStatusAsync_WhenEditionNotFound_ThrowsInvalidOperationException` | — | — | throws |

#### ChangeStatusAsync — date constraint tests

| Test name | Setup | Expected |
|---|---|---|
| `ChangeStatusAsync_DraftToOpen_WhenStartDateInPast_ThrowsInvalidOperationException` | StartDate = yesterday | throws |
| `ChangeStatusAsync_DraftToOpen_WhenStartDateIsToday_ChangesStatus` | StartDate = today | succeeds |
| `ChangeStatusAsync_ClosedToCompleted_WhenEndDateInFuture_ThrowsInvalidOperationException` | EndDate = tomorrow | throws |
| `ChangeStatusAsync_ClosedToCompleted_WhenEndDateIsYesterday_ChangesStatus` | EndDate = yesterday | succeeds |

#### UpdateAsync tests

| Test name | Status | Expected |
|---|---|---|
| `UpdateAsync_WhenStatusIsDraft_UpdatesAllFields` | Draft | all fields updated |
| `UpdateAsync_WhenStatusIsProposed_UpdatesAllFields` | Proposed | all fields updated |
| `UpdateAsync_WhenStatusIsOpen_UpdatesOnlyNotesAndCapacity` | Open | notes/capacity updated, no exception |
| `UpdateAsync_WhenStatusIsOpen_AndDatesChanged_ThrowsInvalidOperationException` | Open | throws |
| `UpdateAsync_WhenStatusIsOpen_AndPricesChanged_ThrowsInvalidOperationException` | Open | throws |
| `UpdateAsync_WhenStatusIsClosed_ThrowsInvalidOperationException` | Closed | throws |
| `UpdateAsync_WhenStatusIsCompleted_ThrowsInvalidOperationException` | Completed | throws |
| `UpdateAsync_WhenEditionNotFound_ThrowsInvalidOperationException` | — | throws |

#### GetActiveEditionAsync tests

| Test name | Setup | Expected |
|---|---|---|
| `GetActiveEditionAsync_WhenOpenEditionExists_ReturnsEdition` | Open edition in DB | returns response |
| `GetActiveEditionAsync_WhenNoOpenEdition_ReturnsNull` | no Open edition | returns null |
| `GetActiveEditionAsync_WhenYearNotProvided_UsesCurrentYear` | no year param | queries current year |
| `GetActiveEditionAsync_IncludesCampLocationDetails` | edition with camp | CampLocation/FormattedAddress mapped |
| `GetActiveEditionAsync_ReturnsZeroRegistrationCount` | any | RegistrationCount == 0 |

#### ProposeAsync — duplicate prevention tests

| Test name | Expected |
|---|---|
| `ProposeAsync_WhenEditionAlreadyExistsForCampAndYear_ThrowsInvalidOperationException` | throws |
| `ProposeAsync_WhenEditionExistsButIsArchived_AllowsCreation` | succeeds (ExistsAsync returns false for archived) |

- **File**: `tests/Abuvi.Tests/Unit/Features/Camps/Validators/UpdateCampEditionValidatorTests.cs` *(create new)*

| Test name | Scenario |
|---|---|
| `Validate_ValidRequest_PassesValidation` | All fields valid |
| `Validate_WhenEndDateBeforeStartDate_FailsValidation` | End < Start |
| `Validate_WhenPricesAreNegative_FailsValidation` | Price < 0 |
| `Validate_WhenNotesTooLong_FailsValidation` | > 2000 chars |
| `Validate_WhenCustomAgeRangesEnabled_RequiresAllAgeFields` | UseCustomAgeRanges=true, fields null |
| `Validate_WhenCustomBabyAgeExceedsChildMinAge_FailsValidation` | BabyMax >= ChildMin |

- **File**: `tests/Abuvi.Tests/Unit/Features/Camps/Validators/ChangeEditionStatusValidatorTests.cs` *(create new)*

| Test name | Scenario |
|---|---|
| `Validate_ValidStatus_PassesValidation` | `Status = CampEditionStatus.Open` |
| `Validate_InvalidEnumValue_FailsValidation` | Status = (CampEditionStatus)99 |

- **Implementation notes**:
  - Follow **AAA pattern** (Arrange / Act / Assert) strictly
  - Use `NSubstitute` for all mocks (`Substitute.For<T>()`)
  - Use `FluentAssertions` for all assertions
  - Naming convention: `MethodName_StateUnderTest_ExpectedBehavior`
  - Use `[Theory][InlineData]` for transition matrix tests to keep test count manageable
  - For `UpdateAsync_WhenStatusIsOpen_UpdatesOnlyNotesAndCapacity`: build a request where StartDate/EndDate/Prices match the current edition values, change only Notes and MaxCapacity

---

### Step 8: Write Integration Tests

- **File**: `tests/Abuvi.Tests/Integration/Features/Camps/CampEditionsEndpointsTests.cs` *(create new)*
- **Action**: HTTP-level tests using `WebApplicationFactory<Program>`

#### Minimum required test methods

| Test name | Method + Route | Auth | Expected |
|---|---|---|---|
| `PatchStatus_AsBoard_WithValidDraftToOpenTransition_Returns200` | PATCH `/editions/{id}/status` | Board | 200 |
| `PatchStatus_AsMember_Returns403` | PATCH `/editions/{id}/status` | Member | 403 |
| `PatchStatus_Unauthenticated_Returns401` | PATCH `/editions/{id}/status` | None | 401 |
| `PatchStatus_WithInvalidTransition_Returns400` | PATCH `/editions/{id}/status` | Board | 400 |
| `GetActive_AsMember_Returns200` | GET `/editions/active` | Member | 200 |
| `GetActive_WhenNoOpenEdition_Returns200WithNullData` | GET `/editions/active` | Member | 200, data=null |
| `GetActive_Unauthenticated_Returns401` | GET `/editions/active` | None | 401 |
| `GetById_AsMember_ForExistingEdition_Returns200` | GET `/editions/{id}` | Member | 200 |
| `GetById_ForNonExistentEdition_Returns404` | GET `/editions/{id}` | Board | 404 |
| `PutEdition_AsBoard_WithValidData_Returns200` | PUT `/editions/{id}` | Board | 200 |
| `PutEdition_AsMember_Returns403` | PUT `/editions/{id}` | Member | 403 |
| `GetAll_AsBoard_Returns200` | GET `/editions` | Board | 200 |
| `GetAll_AsMember_Returns403` | GET `/editions` | Member | 403 |

- **Implementation notes**:
  - Use `WebApplicationFactory<Program>` with in-memory database or Testcontainers (match existing integration test pattern if any exist; otherwise use in-memory DB)
  - Seed test data in each test's arrange phase
  - Use `HttpClient.PostAsJsonAsync`, `PatchAsJsonAsync`, etc.
  - Assert both HTTP status code and response body structure (`ApiResponse<T>`)

---

### Step 9: Update Technical Documentation

- **File**: `ai-specs/specs/api-endpoints.md`
- **Action**: Add the 5 new endpoints under a new "Camp Editions — Phase 4" subsection within the Camp Management section
- **Implementation steps**:
  1. Add section `## Camp Edition Lifecycle Endpoints` after the existing Camp Management section
  2. Document each of the 5 new endpoints with: method, path, authorization, request body/query params, success response shape, error responses
  3. Include the status transition diagram in the documentation
  4. All documentation must be in English

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-camp-editions-management-backend`
2. **Step 1** — Add DTOs to `CampsModels.cs`
3. **Step 2** — Extend `ICampEditionsRepository` interface
4. **Step 3** — Implement repository methods in `CampEditionsRepository.cs`
5. **Step 4** — Implement service methods in `CampEditionsService.cs` (including `ProposeAsync` patch)
6. **Step 5** — Add validators to `CampsValidators.cs`
7. **Step 6** — Add endpoints to `CampsEndpoints.cs`
8. **Step 7** — Write unit tests (`CampEditionsServiceTests.cs`, validator tests)
9. **Step 8** — Write integration tests (`CampEditionsEndpointsTests.cs`)
10. **Step 9** — Update `ai-specs/specs/api-endpoints.md`

> Build and run `dotnet test` after Step 7 and again after Step 8 to catch regressions early.

---

## Testing Checklist

- [ ] `dotnet build` — zero warnings (TreatWarningsAsErrors = true)
- [ ] `dotnet test` — all tests pass
- [ ] Status transitions: 4 valid paths tested with `[Theory][InlineData]`
- [ ] Invalid transitions: at least 3 invalid cases tested (backward, skip)
- [ ] Date constraints: 4 tests (past start date, today start date, future end date, past end date)
- [ ] `UpdateAsync`: all 3 status categories (Proposed/Draft, Open, Closed/Completed) tested
- [ ] `GetActiveEditionAsync`: null case + happy path + year default
- [ ] Duplicate prevention: both blocking and non-blocking (archived) cases tested
- [ ] Integration: auth scenarios (Board, Member, unauthenticated) for restricted endpoints
- [ ] FluentAssertions used for all assertions (no `Assert.`)
- [ ] NSubstitute used for all mocks (no Moq)
- [ ] Coverage ≥ 90% for new code paths

---

## Error Response Format

All endpoints follow the project's `ApiResponse<T>` envelope:

```json
// Success
{ "success": true, "data": { ... } }

// Error (400 / 404)
{ "success": false, "data": null, "error": { "message": "...", "code": "OPERATION_ERROR" } }
```

| Scenario | HTTP Status | Code |
|---|---|---|
| Invalid status transition | 400 | `OPERATION_ERROR` |
| Cannot update closed edition | 400 | `OPERATION_ERROR` |
| Date constraint violation | 400 | `OPERATION_ERROR` |
| Validation failed (FluentValidation) | 400 | `VALIDATION_ERROR` |
| Edition not found | 404 | `NOT_FOUND` |
| Not authenticated | 401 | — |
| Insufficient role | 403 | — |

---

## Dependencies

**No new NuGet packages required.** All dependencies (FluentValidation, NSubstitute, FluentAssertions, EF Core, xUnit) are already present.

**No EF Core migration required.** The `camp_editions` table already exists.

**No Program.cs changes required.** `CampEditionsService` and `ICampEditionsRepository` are already registered as scoped services (lines 148–149 of Program.cs).

---

## Notes

### Business Rules to Enforce

- **Status machine is linear and irreversible**: `Proposed → Draft → Open → Closed → Completed`. No backward or skip transitions.
- **`Proposed → Draft` remains in `PromoteToDraftAsync`** — the new `ChangeStatusAsync` also supports this transition for completeness, but the promote endpoint is the canonical path from Phase 3.
- **`Rejected` is NOT an enum value** — rejection sets `IsArchived = true`. Do NOT add a `Rejected` value to `CampEditionStatus`.
- **Open editions are partially frozen**: only `Notes` and `MaxCapacity` can be changed.
- **Closed/Completed editions are fully frozen**: no updates allowed.
- **`ExistsAsync` excludes archived editions** — a camp can be re-proposed after rejection.
- **`GetActiveEditionAsync` returns `null` (not 404)** when no Open edition exists — the endpoint returns `200 { success: true, data: null }`.

### Language Requirements

- **User-facing error messages**: Spanish (exception messages, validation `.WithMessage()`)
- **Developer logs**: English
- **Code and comments**: English
- **Documentation files**: English

### No GDPR Considerations

Camp edition data (dates, prices, capacity, notes about the camp location) is not personal data — no encryption needed.

---

## Next Steps After Implementation

- Open a PR from `feature/feat-camp-editions-management-backend` → `main`
- Tag the PR with the camp editions management spec reference
- After merge, the Registration feature spec can reference `GET /api/camps/editions/active` and `GET /api/camps/editions/{id}` as the source-of-truth for edition data during registration

---

## Implementation Verification

### Code Quality
- [ ] Nullable reference types respected (no `!` suppressions without justification)
- [ ] `CancellationToken` passed through every async call
- [ ] `AsNoTracking()` on all read-only queries
- [ ] File-scoped namespaces (`namespace Abuvi.API.Features.Camps;`)
- [ ] Old-style constructor in service (matches existing pattern)

### Functionality
- [ ] `PATCH /api/camps/editions/{id}/status` returns 200 on valid transition, 400 on invalid
- [ ] `GET /api/camps/editions/active` returns 200 with null data when no Open edition
- [ ] `GET /api/camps/editions/{id}` returns 404 when not found
- [ ] `PUT /api/camps/editions/{id}` returns 400 on status-restricted field changes
- [ ] `GET /api/camps/editions` returns filtered list for Board, 403 for Member

### Testing
- [ ] ≥ 90% code coverage on new code
- [ ] All unit tests pass with `dotnet test`
- [ ] All integration tests pass with `dotnet test`
- [ ] No test uses real database connections

### Documentation
- [ ] `api-endpoints.md` updated with 5 new endpoints
- [ ] All Swagger `WithSummary(...)` strings filled in

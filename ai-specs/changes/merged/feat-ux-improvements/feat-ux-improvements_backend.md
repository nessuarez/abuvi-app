# Backend Implementation Plan: feat-ux-improvements UX Improvements — Web App

## Overview

This ticket delivers five targeted UX improvements to the web application. Only **Improvement 1** has a backend component: making `proposalReason` optional (and adding it to the DTO if it does not exist) in the `POST /api/camps/editions/propose` endpoint.

Improvements 2–5 are purely frontend changes and produce no backend work.

**Architecture principle:** Vertical Slice Architecture — all changes remain within the existing `Abuvi.API/Features/Camps/` slice. No new files need to be created; only existing files require targeted modifications.

**Important finding from codebase analysis:** `ProposalReason` and `ProposalNotes` do **not** exist in the current `ProposeCampEditionRequest` record or in `ProposeCampEditionRequestValidator`. The `api-endpoints.md` documentation references them, but they were never implemented in code. This means:

- `ProposalReason` must be **added** as an optional (`string?`) field to the request DTO and stored in the database.
- `ProposalNotes` must be **added** as an optional (`string?`) field to the request DTO only (frontend will not send it, but the API contract accepts it for completeness, and the documentation must be corrected).
- No database migration is needed if the entity does not yet have these columns; if those columns are missing from the `CampEdition` entity and table, a migration IS required.

---

## Architecture Context

- **Feature slice:** `src/Abuvi.API/Features/Camps/`
- **Files to modify:**
  - `src/Abuvi.API/Features/Camps/CampsModels.cs` — add optional fields to `ProposeCampEditionRequest` and update `CampEdition` entity
  - `src/Abuvi.API/Features/Camps/CampsValidators.cs` — update `ProposeCampEditionRequestValidator` (no required rule for `ProposalReason`)
  - `src/Abuvi.API/Features/Camps/CampEditionsService.cs` — map new fields when creating `CampEdition`
  - `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs` — add tests for optional `ProposalReason`
- **Files to create:**
  - EF Core migration for the new columns on `CampEditions` table (if columns are absent)
- **Documentation to update:**
  - `ai-specs/specs/api-endpoints.md`

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action:** Create and switch to a new feature branch before making any changes.
- **Branch Naming:** `feature/feat-ux-improvements-backend`
- **Implementation Steps:**
  1. Ensure you are on the latest `main` branch: `git checkout main`
  2. Pull latest changes: `git pull origin main`
  3. Create new branch: `git checkout -b feature/feat-ux-improvements-backend`
  4. Verify: `git branch`
- **Notes:** Do NOT remain on the general `feature/feat-ux-improvements` branch if it exists; the backend and frontend work must be separated into distinct branches.

---

### Step 1: Update `CampEdition` Entity (if columns are missing)

- **File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action:** Add `ProposalReason` and `ProposalNotes` as optional (`string?`) properties to the `CampEdition` entity class.
- **Implementation Steps:**
  1. Open `CampsModels.cs` and locate the `CampEdition` class (currently at line ~114).
  2. After the `Notes` property, add:

     ```csharp
     /// <summary>
     /// Optional reason for proposing this edition (provided during proposal, stored for board review).
     /// </summary>
     public string? ProposalReason { get; set; }

     /// <summary>
     /// Optional additional notes provided at proposal time (stored for reference; not sent by frontend after UX improvement).
     /// </summary>
     public string? ProposalNotes { get; set; }
     ```

  3. Both properties are nullable — no default value needed.
- **Dependencies:** No new NuGet packages required.
- **Implementation Notes:**
  - These are purely optional fields. They will be `null` for all existing editions in production — this is safe and backward-compatible.
  - EF Core will handle the mapping automatically via convention (no Fluent API configuration needed for `string?`).

---

### Step 2: Update `ProposeCampEditionRequest` DTO

- **File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action:** Add `ProposalReason` and `ProposalNotes` as optional parameters to the `ProposeCampEditionRequest` record.
- **Current record signature** (condensed):

  ```csharp
  public record ProposeCampEditionRequest(
      Guid CampId,
      int Year,
      DateTime StartDate,
      DateTime EndDate,
      decimal? PricePerAdult,
      decimal? PricePerChild,
      decimal? PricePerBaby,
      bool UseCustomAgeRanges,
      int? CustomBabyMaxAge,
      int? CustomChildMinAge,
      int? CustomChildMaxAge,
      int? CustomAdultMinAge,
      int? MaxCapacity,
      string? Notes,
      AccommodationCapacity? AccommodationCapacity = null,
      DateOnly? HalfDate = null,
      decimal? PricePerAdultWeek = null,
      decimal? PricePerChildWeek = null,
      decimal? PricePerBabyWeek = null,
      DateOnly? WeekendStartDate = null,
      DateOnly? WeekendEndDate = null,
      decimal? PricePerAdultWeekend = null,
      decimal? PricePerChildWeekend = null,
      decimal? PricePerBabyWeekend = null,
      int? MaxWeekendCapacity = null
  );
  ```

- **Target record signature** — add two new optional parameters with defaults:

  ```csharp
  public record ProposeCampEditionRequest(
      Guid CampId,
      int Year,
      DateTime StartDate,
      DateTime EndDate,
      decimal? PricePerAdult,
      decimal? PricePerChild,
      decimal? PricePerBaby,
      bool UseCustomAgeRanges,
      int? CustomBabyMaxAge,
      int? CustomChildMinAge,
      int? CustomChildMaxAge,
      int? CustomAdultMinAge,
      int? MaxCapacity,
      string? Notes,
      AccommodationCapacity? AccommodationCapacity = null,
      DateOnly? HalfDate = null,
      decimal? PricePerAdultWeek = null,
      decimal? PricePerChildWeek = null,
      decimal? PricePerBabyWeek = null,
      DateOnly? WeekendStartDate = null,
      DateOnly? WeekendEndDate = null,
      decimal? PricePerAdultWeekend = null,
      decimal? PricePerChildWeekend = null,
      decimal? PricePerBabyWeekend = null,
      int? MaxWeekendCapacity = null,
      string? ProposalReason = null,    // Optional: reason for proposing this edition
      string? ProposalNotes = null      // Optional: additional context (frontend no longer sends this)
  );
  ```

- **Implementation Steps:**
  1. Locate the `ProposeCampEditionRequest` record in `CampsModels.cs` (currently around line 433).
  2. Append `string? ProposalReason = null` and `string? ProposalNotes = null` as the last two optional parameters (after `MaxWeekendCapacity`).
  3. Add XML doc comments on the record summarising the new fields.
- **Implementation Notes:**
  - Both new parameters have default values (`= null`), so all existing callers (tests, frontend) remain unaffected without any changes.
  - Placing them at the end with defaults ensures backward compatibility with positional record construction in existing tests.

---

### Step 3: Update `ProposeCampEditionRequestValidator`

- **File:** `src/Abuvi.API/Features/Camps/CampsValidators.cs`
- **Action:** Add optional-field length validation rules for `ProposalReason` and `ProposalNotes`. Do NOT add any `NotEmpty`/`NotNull` rule — both fields must remain fully optional.
- **Implementation Steps:**
  1. Locate `ProposeCampEditionRequestValidator` in `CampsValidators.cs` (around line 117).
  2. After the existing `Notes` rule (around line 154), add:

     ```csharp
     // Optional proposal metadata — no required constraint, only length guard
     RuleFor(x => x.ProposalReason)
         .MaximumLength(1000).WithMessage("Proposal reason must not exceed 1000 characters")
         .When(x => !string.IsNullOrWhiteSpace(x.ProposalReason));

     RuleFor(x => x.ProposalNotes)
         .MaximumLength(2000).WithMessage("Proposal notes must not exceed 2000 characters")
         .When(x => !string.IsNullOrWhiteSpace(x.ProposalNotes));
     ```

  3. Confirm there is no `NotEmpty` or `NotNull` rule for either field anywhere in this validator.
- **Implementation Notes:**
  - The 1 000 / 2 000 character limits match the pattern used for `Notes` and similar free-text fields in this slice.
  - The `When(...)` guard prevents FluentValidation from triggering the `MaximumLength` rule on a null value (which would evaluate to 0 and pass anyway, but the guard makes intent explicit).

---

### Step 4: Update `CampEditionsService.ProposeAsync`

- **File:** `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action:** Map `ProposalReason` and `ProposalNotes` from the request DTO to the `CampEdition` entity when creating a new edition.
- **Implementation Steps:**
  1. Locate `ProposeAsync` in `CampEditionsService.cs` (around line 22).
  2. Inside the `CampEdition` object initialiser (around line 58), add the two new property assignments after `Notes = request.Notes`:

     ```csharp
     ProposalReason = request.ProposalReason,
     ProposalNotes  = request.ProposalNotes,
     ```

  3. No other logic changes are required in this method.
- **Implementation Notes:**
  - Both values may be `null` — that is valid and expected for editions submitted after the UX improvement rolls out.
  - Existing editions created before this change will have `null` for both fields; this is intentional and safe.

---

### Step 5: Create EF Core Migration

- **Action:** Add an EF Core migration to introduce `ProposalReason` and `ProposalNotes` columns in the `CampEditions` table.
- **Implementation Steps:**
  1. From the `src/Abuvi.API/` directory, run:

     ```
     dotnet ef migrations add AddProposalFieldsToCampEdition --project ../Abuvi.API --startup-project ../Abuvi.API
     ```

     *(Adjust paths to match the solution layout if needed.)*
  2. Review the generated migration file to confirm:
     - Two nullable `text` columns are added: `ProposalReason` and `ProposalNotes`.
     - No existing column is dropped or altered.
     - The `Down()` method drops both columns correctly.
  3. Apply the migration locally:

     ```
     dotnet ef database update
     ```

- **Implementation Notes:**
  - PostgreSQL maps `string?` to `text` (nullable) by default via EF Core convention — no Fluent API configuration is needed.
  - These columns will be `NULL` for all existing rows after migration, which is the correct default.

---

### Step 6: Update Unit Tests

- **File:** `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs`
- **Action:** Add test cases that verify the new optional behaviour for `ProposalReason` and `ProposalNotes`.
- **New test methods to add** (in the existing `#region ProposeAsync Tests` section):

  ```csharp
  [Fact]
  public async Task ProposeAsync_WithNullProposalReason_CreatesEditionSuccessfully()
  {
      // Arrange
      var camp = CreateActiveTestCamp();
      _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(camp);
      SetupRepositoryCreateToReturnEdition();

      var request = new ProposeCampEditionRequest(
          CampId: camp.Id,
          Year: 2026,
          StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
          EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
          PricePerAdult: null,
          PricePerChild: null,
          PricePerBaby: null,
          UseCustomAgeRanges: false,
          CustomBabyMaxAge: null,
          CustomChildMinAge: null,
          CustomChildMaxAge: null,
          CustomAdultMinAge: null,
          MaxCapacity: null,
          Notes: null,
          ProposalReason: null   // explicitly null — must be accepted
      );

      // Act
      var act = async () => await _sut.ProposeAsync(request);

      // Assert
      await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task ProposeAsync_WithProposalReason_StoresReasonOnEdition()
  {
      // Arrange
      var camp = CreateActiveTestCamp();
      _campsRepository.GetByIdAsync(camp.Id, Arg.Any<CancellationToken>()).Returns(camp);

      CampEdition? capturedEdition = null;
      _repository
          .CreateAsync(Arg.Do<CampEdition>(e => capturedEdition = e), Arg.Any<CancellationToken>())
          .Returns(callInfo => Task.FromResult((CampEdition)callInfo[0]));

      // Fake Camp nav prop for MapToCampEditionResponse
      _repository
          .CreateAsync(Arg.Any<CampEdition>(), Arg.Any<CancellationToken>())
          .Returns(callInfo =>
          {
              var e = (CampEdition)callInfo[0];
              e.Camp = camp;
              capturedEdition = e;
              return Task.FromResult(e);
          });

      var request = new ProposeCampEditionRequest(
          CampId: camp.Id,
          Year: 2026,
          StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
          EndDate: new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
          PricePerAdult: null,
          PricePerChild: null,
          PricePerBaby: null,
          UseCustomAgeRanges: false,
          CustomBabyMaxAge: null,
          CustomChildMinAge: null,
          CustomChildMaxAge: null,
          CustomAdultMinAge: null,
          MaxCapacity: null,
          Notes: null,
          ProposalReason: "Annual summer camp proposal"
      );

      // Act
      await _sut.ProposeAsync(request);

      // Assert
      capturedEdition.Should().NotBeNull();
      capturedEdition!.ProposalReason.Should().Be("Annual summer camp proposal");
  }
  ```

- **Validator tests to add** in `src/Abuvi.Tests/Unit/Features/Camps/Validators/` (create new file `ProposeCampEditionValidatorTests.cs` or add to existing):

  ```csharp
  [Fact]
  public void Validate_WithNullProposalReason_IsValid()
  {
      var validator = new ProposeCampEditionRequestValidator();
      var request = BuildMinimalValidRequest() with { ProposalReason = null };

      var result = validator.Validate(request);

      result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void Validate_WithEmptyProposalReason_IsValid()
  {
      var validator = new ProposeCampEditionRequestValidator();
      var request = BuildMinimalValidRequest() with { ProposalReason = "" };

      var result = validator.Validate(request);

      result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void Validate_WithProposalReasonExceeding1000Chars_ReturnsError()
  {
      var validator = new ProposeCampEditionRequestValidator();
      var request = BuildMinimalValidRequest() with { ProposalReason = new string('x', 1001) };

      var result = validator.Validate(request);

      result.IsValid.Should().BeFalse();
      result.Errors.Should().ContainSingle(e => e.PropertyName == "ProposalReason");
  }
  ```

- **Implementation Notes:**
  - Follow the existing `AAA` pattern already established in `CampEditionsServiceTests.cs`.
  - Use `NSubstitute` for mocks and `FluentAssertions` for assertions.
  - Helper method `CreateActiveTestCamp()` should already exist in the test file or can be extracted as a private helper.

---

### Step 7: Update Technical Documentation

- **Action:** Update `ai-specs/specs/api-endpoints.md` to reflect that `proposalReason` is now optional and `proposalNotes` is accepted but not required.
- **Implementation Steps:**
  1. Open `ai-specs/specs/api-endpoints.md`.
  2. Locate the section `### POST /api/camps/editions/propose` (around line 1646).
  3. Update the **Request Body** JSON example to remove any indication that `proposalReason` is required:

     ```json
     {
       "campId": "...",
       "year": 2026,
       "startDate": "2026-07-01T00:00:00Z",
       "endDate": "2026-07-10T00:00:00Z",
       "pricePerAdult": 180.00,
       "pricePerChild": 120.00,
       "pricePerBaby": 60.00,
       "maxCapacity": 100,
       "proposalReason": "Annual summer camp",
       "proposalNotes": "Same location as last year"
     }
     ```

     *(Keep both fields in the example, but mark them optional in the field notes.)*
  4. Update the **Field Notes** section:
     - `proposalReason`: ~~Required~~ **Optional** (`string | null`). Reason for proposing this edition. Previously documented as required; now optional.
     - `proposalNotes`: **Optional** (`string | null`). Additional context for the board. Accepted by the API but no longer sent by the frontend after the UX improvement.
  5. Verify that no other section references `proposalReason` or `proposalNotes` as required.
- **References:** Follow `ai-specs/specs/documentation-standards.mdc` — all documentation must be written in English.

---

## Implementation Order

1. **Step 0:** Create feature branch `feature/feat-ux-improvements-backend`
2. **Step 1:** Add `ProposalReason` / `ProposalNotes` properties to `CampEdition` entity in `CampsModels.cs`
3. **Step 2:** Add optional `ProposalReason` / `ProposalNotes` parameters to `ProposeCampEditionRequest` record in `CampsModels.cs`
4. **Step 3:** Add length-only validators to `ProposeCampEditionRequestValidator` in `CampsValidators.cs`
5. **Step 4:** Map new DTO fields to entity in `CampEditionsService.ProposeAsync`
6. **Step 5:** Generate and review EF Core migration; apply locally
7. **Step 6:** Write and run unit tests in `CampEditionsServiceTests.cs` and validator tests
8. **Step 7:** Update `ai-specs/specs/api-endpoints.md` documentation

---

## Testing Checklist

### Unit Tests — `CampEditionsServiceTests`

- [ ] `ProposeAsync_WithNullProposalReason_CreatesEditionSuccessfully` — passes without throwing
- [ ] `ProposeAsync_WithProposalReason_StoresReasonOnEdition` — value persisted on entity
- [ ] `ProposeAsync_WithNullProposalNotes_CreatesEditionSuccessfully` — passes without throwing
- [ ] All existing `ProposeAsync` tests still pass (no regression — new parameters default to `null`)

### Unit Tests — `ProposeCampEditionRequestValidator`

- [ ] `Validate_WithNullProposalReason_IsValid` — null is accepted
- [ ] `Validate_WithEmptyProposalReason_IsValid` — empty string is accepted
- [ ] `Validate_WithProposalReasonExceeding1000Chars_ReturnsError` — too-long value rejected
- [ ] `Validate_WithProposalNotesExceeding2000Chars_ReturnsError` — too-long value rejected

### Integration Tests (Optional, Recommended)

- [ ] `POST /api/camps/editions/propose` with `proposalReason: null` returns `201 Created`
- [ ] `POST /api/camps/editions/propose` with valid `proposalReason` returns `201 Created`
- [ ] `POST /api/camps/editions/propose` with `proposalReason` > 1000 chars returns `400 Bad Request`

### Coverage Requirement

- Maintain ≥ 90 % branch, function, line, and statement coverage as per `backend-standards.mdc`.

---

## Error Response Format

All errors use the standard `ApiResponse<T>` envelope:

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Proposal reason must not exceed 1000 characters",
    "code": "VALIDATION_ERROR"
  }
}
```

| HTTP Status | Scenario |
|-------------|----------|
| 201 Created | Edition proposed successfully |
| 400 Bad Request | Validation failure (e.g., `proposalReason` > 1000 chars, missing required dates) |
| 400 Bad Request | Business rule violation (e.g., duplicate edition for same camp+year) |
| 401 Unauthorized | No JWT token |
| 403 Forbidden | Role insufficient (Member role cannot propose) |

---

## Dependencies

### NuGet Packages

No new packages required. All dependencies already in place:

- `FluentValidation.AspNetCore`
- `Microsoft.EntityFrameworkCore`
- `Npgsql.EntityFrameworkCore.PostgreSQL`

### EF Core Migration Commands

```bash
# From solution root or src/Abuvi.API directory:
dotnet ef migrations add AddProposalFieldsToCampEdition

dotnet ef database update
```

---

## Notes

- **No migration needed for existing validator/DTO-only changes.** However, because `ProposalReason`/`ProposalNotes` are new entity properties that must be stored in the database, a migration IS required.
- **Backward compatibility:** All new record parameters use `= null` defaults; zero existing code or tests break.
- **Frontend alignment:** The frontend change (Improvement 1) removes the `Notas adicionales` field and makes `Motivo de la propuesta` optional. The backend change described here aligns the API contract with that intent. The two changes can be deployed independently — the backend is safe to deploy first.
- **`ProposalNotes` storage rationale:** Even though the frontend will stop sending `proposalNotes`, retaining it in the backend DTO and database allows:
  - Admin tools or future forms to submit it if needed
  - Historical data to remain coherent if the field was previously used
- **Language in code:** All code identifiers, comments, error messages, and log messages must remain in **English** per `base-standards.mdc`.
- **Validator message language:** The existing codebase mixes English and Spanish in validator messages. New messages added here follow **English** to comply with `base-standards.mdc`.

---

## Next Steps After Implementation

- Open a PR from `feature/feat-ux-improvements-backend` → `main` (or `develop`) for peer review.
- Coordinate with the frontend developer to align on the deploy sequence; the backend change is safe to merge before the frontend.
- Verify the integration environment has run the migration before the frontend is deployed.
- After both changes are deployed, perform a smoke test: submit the propose form without filling in `Motivo de la propuesta` and confirm a `201` response is received.

---

## Implementation Verification

Before marking implementation complete, verify:

| Area | Checklist |
|------|-----------|
| **Code Quality** | C# analyzers pass; nullable reference types enabled and used correctly; no warnings introduced |
| **Functionality** | `POST /api/camps/editions/propose` with `proposalReason: null` returns 201; with `proposalReason: "text"` returns 201; with oversized string returns 400 |
| **Testing** | ≥ 90 % branch/function/line/statement coverage; all new test methods follow AAA naming `MethodName_StateUnderTest_ExpectedBehavior` |
| **Migration** | EF Core migration reviewed, applied, and does not alter existing columns |
| **Documentation** | `ai-specs/specs/api-endpoints.md` updated — `proposalReason` marked optional, `proposalNotes` marked optional |
| **Branch hygiene** | Branch is `feature/feat-ux-improvements-backend`, not the general feature branch |

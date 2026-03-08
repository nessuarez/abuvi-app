# Backend Implementation Plan: fix-registration-detail-button

## Overview

The "Ver detalles" button on the "Mis Inscripciones" page navigates to the registration detail page, but the page may not render correctly due to missing fields in the backend DTOs that the frontend expects. This plan addresses the backend changes needed to ensure the `RegistrationFamilyUnitSummary` and `RegistrationCampEditionSummary` shared DTOs include all data the frontend requires, and that the list/detail response types are consistent.

**Architecture**: Vertical Slice Architecture. All changes are within the `Registrations` feature slice (`src/Abuvi.API/Features/Registrations/`), with no cross-slice modifications needed.

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Registrations/`
- **Files to modify**:
  - `RegistrationsModels.cs` — Update shared DTOs (`RegistrationFamilyUnitSummary`, `RegistrationCampEditionSummary`) and mapping extension
  - `RegistrationsService.cs` — Update list mapping in `GetByFamilyUnitAsync` to pass new DTO fields
- **No new files needed**
- **No database schema changes** — All data already exists in entities (`FamilyUnit.RepresentativeUserId`, `Camp.Location`)
- **No new NuGet packages needed**

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `fix/fix-registration-detail-button-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b fix/fix-registration-detail-button-backend`
  4. Verify branch creation: `git branch`

### Step 1: Add `RepresentativeUserId` to `RegistrationFamilyUnitSummary`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Extend the shared summary record to include the representative user ID
- **Current** (line 211):
  ```csharp
  public record RegistrationFamilyUnitSummary(Guid Id, string Name);
  ```
- **Target**:
  ```csharp
  public record RegistrationFamilyUnitSummary(Guid Id, string Name, Guid RepresentativeUserId);
  ```
- **Implementation Steps**:
  1. Add `Guid RepresentativeUserId` as the third positional parameter
  2. This record is used by both `RegistrationListResponse` and `RegistrationResponse`, so both endpoints benefit automatically
- **Dependencies**: `FamilyUnit` entity already has `RepresentativeUserId` property (confirmed in `FamilyUnitsModels.cs`)
- **Impact**: All call sites constructing `RegistrationFamilyUnitSummary` must now pass the third argument

### Step 2: Add `Location` to `RegistrationCampEditionSummary`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Extend the shared summary record to include the camp location
- **Current** (line 212):
  ```csharp
  public record RegistrationCampEditionSummary(Guid Id, string CampName, int Year, DateTime StartDate, DateTime EndDate, int Duration);
  ```
- **Target**:
  ```csharp
  public record RegistrationCampEditionSummary(Guid Id, string CampName, int Year, DateTime StartDate, DateTime EndDate, int Duration, string? Location);
  ```
- **Implementation Steps**:
  1. Add `string? Location` as the last positional parameter (nullable because `Camp.Location` is `string?`)
  2. This record is used by both `RegistrationListResponse` and `RegistrationResponse`, so both endpoints benefit automatically
- **Dependencies**: `Camp` entity has `Location` property (`string?`, confirmed in `CampsModels.cs`)
- **Impact**: All call sites constructing `RegistrationCampEditionSummary` must now pass the location argument

### Step 3: Update `ToResponse()` mapping in `RegistrationMappingExtensions`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Update the `ToResponse` extension method to pass the new fields
- **Current** (lines 345-390, relevant sections):
  ```csharp
  public static RegistrationResponse ToResponse(this Registration r, decimal amountPaid) => new(
      r.Id,
      new(r.FamilyUnit.Id, r.FamilyUnit.Name),  // Missing RepresentativeUserId
      new(r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
          r.CampEdition.StartDate, r.CampEdition.EndDate,
          (r.CampEdition.EndDate - r.CampEdition.StartDate).Days),  // Missing Location
      ...
  );
  ```
- **Target**:
  ```csharp
  public static RegistrationResponse ToResponse(this Registration r, decimal amountPaid) => new(
      r.Id,
      new(r.FamilyUnit.Id, r.FamilyUnit.Name, r.FamilyUnit.RepresentativeUserId),
      new(r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
          r.CampEdition.StartDate, r.CampEdition.EndDate,
          (r.CampEdition.EndDate - r.CampEdition.StartDate).Days,
          r.CampEdition.Camp.Location),
      ...
  );
  ```
- **Implementation Steps**:
  1. In the `RegistrationFamilyUnitSummary` constructor call, add `r.FamilyUnit.RepresentativeUserId`
  2. In the `RegistrationCampEditionSummary` constructor call, add `r.CampEdition.Camp.Location`
- **Implementation Notes**:
  - `ToResponse()` is called by `GetByIdAsync` and `AdminUpdateAsync` in the service — both load `FamilyUnit` and `CampEdition.Camp` via includes, so navigation properties are available
  - Verify that `GetByIdWithDetailsAsync` in the repository includes `.Include(r => r.CampEdition).ThenInclude(e => e.Camp)` — this is confirmed in the existing code

### Step 4: Update `GetByFamilyUnitAsync` list mapping in service

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Update the list endpoint mapping at lines 535-546 to pass new DTO fields
- **Current** (lines 535-546):
  ```csharp
  return new RegistrationListResponse(
      Id: r.Id,
      FamilyUnit: new RegistrationFamilyUnitSummary(r.FamilyUnit.Id, r.FamilyUnit.Name),
      CampEdition: new RegistrationCampEditionSummary(
          r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
          r.CampEdition.StartDate, r.CampEdition.EndDate,
          (r.CampEdition.EndDate - r.CampEdition.StartDate).Days),
      ...
  );
  ```
- **Target**:
  ```csharp
  return new RegistrationListResponse(
      Id: r.Id,
      FamilyUnit: new RegistrationFamilyUnitSummary(r.FamilyUnit.Id, r.FamilyUnit.Name, r.FamilyUnit.RepresentativeUserId),
      CampEdition: new RegistrationCampEditionSummary(
          r.CampEdition.Id, r.CampEdition.Camp.Name, r.CampEdition.Year,
          r.CampEdition.StartDate, r.CampEdition.EndDate,
          (r.CampEdition.EndDate - r.CampEdition.StartDate).Days,
          r.CampEdition.Camp.Location),
      ...
  );
  ```
- **Implementation Notes**:
  - The repository method `GetByFamilyUnitAsync` already includes `.Include(r => r.FamilyUnit)` and `.Include(r => r.CampEdition).ThenInclude(e => e.Camp)`, so navigation properties are available
  - No repository changes needed

### Step 5: Check and update any other call sites constructing these DTOs

- **Action**: Search for all places constructing `RegistrationFamilyUnitSummary` and `RegistrationCampEditionSummary` to ensure they pass the new arguments
- **Known call sites**:
  1. `RegistrationMappingExtensions.ToResponse()` — Updated in Step 3
  2. `RegistrationsService.GetByFamilyUnitAsync()` — Updated in Step 4
  3. `RegistrationsService.GetAdminListAsync()` — Uses `AdminRegistrationListItem` with its own `RegistrationFamilyUnitSummary`, needs update
- **Implementation Steps**:
  1. Search: `grep -rn "RegistrationFamilyUnitSummary\|RegistrationCampEditionSummary" src/Abuvi.API/`
  2. Update each constructor call to include the new parameters
  3. For `GetAdminListAsync`: check if the query projection includes `FamilyUnit.RepresentativeUserId` and `Camp.Location` — if not, add them to the projection

### Step 6: Write/Update Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/` (existing test files)
- **Action**: Update existing tests and add new assertions for the new DTO fields
- **Implementation Steps**:
  1. **Update test data builders**: Any test that constructs `RegistrationFamilyUnitSummary` or `RegistrationCampEditionSummary` must include the new fields
  2. **Add assertion for `RepresentativeUserId`**:
     - In `GetByIdAsync` tests: assert `result.FamilyUnit.RepresentativeUserId` matches the expected value
     - In `GetByFamilyUnitAsync` tests: assert each item's `FamilyUnit.RepresentativeUserId` is populated
  3. **Add assertion for `Location`**:
     - In `GetByIdAsync` tests: assert `result.CampEdition.Location` matches `Camp.Location`
     - In `GetByFamilyUnitAsync` tests: assert each item's `CampEdition.Location` is populated
  4. **Test naming**: Follow pattern `MethodName_StateUnderTest_ExpectedBehavior`
  5. **Test pattern**: Use AAA (Arrange, Act, Assert) with FluentAssertions

- **Test cases**:
  - `GetByIdAsync_ValidId_ResponseIncludesRepresentativeUserId`
  - `GetByIdAsync_ValidId_ResponseIncludesCampLocation`
  - `GetByIdAsync_CampWithNullLocation_ResponseLocationIsNull`
  - `GetByFamilyUnitAsync_HasRegistrations_ResponsesIncludeRepresentativeUserId`
  - `GetByFamilyUnitAsync_HasRegistrations_ResponsesIncludeCampLocation`

### Step 7: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes made during implementation
  2. **Identify Documentation Files**:
     - API response shape changed for two shared DTOs → Update `ai-specs/specs/api-spec.yml` if it documents `RegistrationFamilyUnitSummary` and `RegistrationCampEditionSummary`
     - No data model changes (no new columns/tables)
     - No new endpoints
  3. **Update Documentation**:
     - Document the new fields in `RegistrationFamilyUnitSummary` (`representativeUserId`) and `RegistrationCampEditionSummary` (`location`) in the API spec
  4. **Verify Documentation**: Confirm OpenAPI auto-generation reflects the new record fields
- **Notes**: This step is MANDATORY before considering the implementation complete.

## Implementation Order

1. **Step 0**: Create feature branch `fix/fix-registration-detail-button-backend`
2. **Step 1**: Add `RepresentativeUserId` to `RegistrationFamilyUnitSummary` record
3. **Step 2**: Add `Location` to `RegistrationCampEditionSummary` record
4. **Step 3**: Update `ToResponse()` mapping extension (detail endpoint)
5. **Step 4**: Update `GetByFamilyUnitAsync` mapping (list endpoint)
6. **Step 5**: Update any remaining call sites (admin endpoints, etc.)
7. **Step 6**: Write/update unit tests
8. **Step 7**: Update technical documentation

## Testing Checklist

- [ ] All existing tests still pass after DTO changes (compile errors from missing constructor args must be fixed)
- [ ] `GetByIdAsync` returns `representativeUserId` in `familyUnit` summary
- [ ] `GetByIdAsync` returns `location` in `campEdition` summary
- [ ] `GetByFamilyUnitAsync` returns `representativeUserId` and `location` in each list item
- [ ] `GetAdminListAsync` returns `representativeUserId` and `location` (if applicable)
- [ ] Null `Location` on Camp entity maps to `null` in response (not empty string)
- [ ] Build succeeds with zero warnings

## Error Response Format

No new error responses introduced. Existing `ApiResponse<T>` envelope remains unchanged:

| Status Code | Scenario |
|---|---|
| 200 | Successful GET for list or detail |
| 403 | User not authorized to view registration |
| 404 | Registration not found |

## Dependencies

- **No new NuGet packages**
- **No EF Core migrations** — All data (`FamilyUnit.RepresentativeUserId`, `Camp.Location`) already exists in the database schema

## Notes

- **Breaking change for API consumers**: The `RegistrationFamilyUnitSummary` and `RegistrationCampEditionSummary` JSON responses will now include additional fields (`representativeUserId`, `location`). Since these are additive (new fields, not removed/renamed), this is backward-compatible for JSON deserialization.
- **Shared DTOs**: `RegistrationFamilyUnitSummary` and `RegistrationCampEditionSummary` are used by both `RegistrationListResponse` (list) and `RegistrationResponse` (detail). Changing them in one place automatically propagates to all endpoints that use them.
- **Admin endpoint impact**: `GetAdminListAsync` uses `AdminRegistrationProjection` with a raw SQL projection, then maps to `AdminRegistrationListItem` which also uses `RegistrationFamilyUnitSummary`. This mapping must also be updated in Step 5.
- **Language**: All code, comments, test names, and documentation must be in English per project standards.

## Next Steps After Implementation

1. Coordinate with frontend developer to update the frontend types (`RegistrationCampEditionSummary` to include `location`, `RegistrationFamilyUnitSummary` to use `representativeUserId`)
2. Frontend should also create a separate `RegistrationListItem` type matching `RegistrationListResponse` instead of reusing `RegistrationResponse` for the list endpoint
3. Manual end-to-end verification: navigate to "Mis Inscripciones" > click "Ver detalles" > verify detail page renders correctly

## Implementation Verification

- [ ] **Code Quality**: C# analyzers pass, nullable reference types handled (especially `string? Location`)
- [ ] **Functionality**: `GET /api/registrations` and `GET /api/registrations/{id}` both return new fields
- [ ] **Testing**: Existing + new tests pass with xUnit + FluentAssertions
- [ ] **Integration**: No EF Core migrations needed — verify app starts cleanly
- [ ] **Documentation**: API spec and relevant docs updated

# Backend Implementation Plan: feat-extras-user-input-and-hide-zero-prices

## Overview

This plan adds backend support for two capabilities in the Camp Extras system:

1. **User input for extras** — New `RequiresUserInput` flag and `UserInputLabel` on `CampEditionExtra`, plus a `UserInput` text field on `RegistrationExtra`, allowing families to provide free-text information when selecting certain extras during registration.
2. **Data support for hiding 0-price extras** — While the "hide 0 EUR" logic is primarily frontend, the backend must ensure `UserInput` is persisted and returned in all relevant DTOs.

Architecture: Vertical Slice Architecture. Changes span the **Camps** and **Registrations** feature slices.

---

## Architecture Context

### Feature slices affected

| Slice | Path | Files to modify |
|-------|------|-----------------|
| Camps | `src/Abuvi.API/Features/Camps/` | `CampsModels.cs`, `CampEditionExtrasService.cs`, `CampEditionExtrasValidator.cs` |
| Registrations | `src/Abuvi.API/Features/Registrations/` | `RegistrationsModels.cs`, `RegistrationsService.cs`, `UpdateRegistrationExtrasValidator.cs` |
| Data | `src/Abuvi.API/Data/Configurations/` | `CampEditionExtraConfiguration.cs`, `RegistrationExtraConfiguration.cs` |
| Migrations | `src/Abuvi.API/Migrations/` | New migration file |

### Cross-cutting concerns

- No new services or middleware required
- No changes to `Program.cs` (validators are auto-registered via `AddValidatorsFromAssemblyContaining<Program>()`)

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/feat-extras-user-input-and-hide-zero-prices-backend`
- **Implementation Steps**:
  1. Ensure on latest `main` branch
  2. `git pull origin main`
  3. `git checkout -b feature/feat-extras-user-input-and-hide-zero-prices-backend`
  4. Verify: `git branch`
- **Notes**: This MUST be a separate branch from the general task branch to separate backend concerns.

---

### Step 1: Update CampEditionExtra Entity

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add two new properties to the `CampEditionExtra` class (after `MaxQuantity`, before timestamps ~line 304)
- **Implementation Steps**:
  1. Add `public bool RequiresUserInput { get; set; }` — whether this extra needs free-text from the registrant
  2. Add `public string? UserInputLabel { get; set; }` — custom prompt/label for the text input (e.g. "Indica tu talla")
- **Implementation Notes**: Follow existing property ordering: business fields, then timestamps, then navigation properties.

---

### Step 2: Update CampEditionExtra EF Configuration

- **File**: `src/Abuvi.API/Data/Configurations/CampEditionExtraConfiguration.cs`
- **Action**: Configure the new columns following existing snake_case naming convention
- **Implementation Steps**:
  1. Add `requires_user_input` column:
     ```csharp
     builder.Property(e => e.RequiresUserInput)
         .HasColumnName("requires_user_input")
         .IsRequired()
         .HasDefaultValue(false);
     ```
  2. Add `user_input_label` column:
     ```csharp
     builder.Property(e => e.UserInputLabel)
         .HasColumnName("user_input_label")
         .HasMaxLength(200);
     ```
- **Implementation Notes**: `UserInputLabel` is nullable by convention (string? in C#), no `.IsRequired()` needed. Default `false` on `RequiresUserInput` ensures existing rows get the correct value during migration.

---

### Step 3: Update RegistrationExtra Entity

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `UserInput` property to `RegistrationExtra` class (after `TotalAmount`, before `CreatedAt` ~line 64)
- **Implementation Steps**:
  1. Add `public string? UserInput { get; set; }` — free-text input provided by the family (max 500 chars)

---

### Step 4: Update RegistrationExtra EF Configuration

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationExtraConfiguration.cs`
- **Action**: Configure the new column
- **Implementation Steps**:
  1. Add `user_input` column:
     ```csharp
     builder.Property(e => e.UserInput)
         .HasColumnName("user_input")
         .HasMaxLength(500);
     ```
- **Implementation Notes**: Column is nullable by default. No `.IsRequired()` needed.

---

### Step 5: Update Camp Edition Extra DTOs

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Update the three records related to extras
- **Implementation Steps**:
  1. **`CampEditionExtraResponse`** (~line 400-414): Add `bool RequiresUserInput` and `string? UserInputLabel` after `MaxQuantity`
  2. **`CreateCampEditionExtraRequest`** (~line 416-423): Add `bool RequiresUserInput = false` and `string? UserInputLabel = null` (with defaults for backward compatibility)
  3. **`UpdateCampEditionExtraRequest`** (~line 426-433): Add `bool RequiresUserInput` and `string? UserInputLabel`
- **Implementation Notes**: Default values on `CreateCampEditionExtraRequest` ensure existing API callers continue working without changes.

---

### Step 6: Update Camp Edition Extras Validators

- **File**: `src/Abuvi.API/Features/Camps/CampEditionExtrasValidator.cs`
- **Action**: Add validation rules for the new fields in both validators
- **Implementation Steps**:
  1. **`CreateCampEditionExtraRequestValidator`**: Add:
     ```csharp
     RuleFor(x => x.UserInputLabel)
         .MaximumLength(200)
         .When(x => x.UserInputLabel != null);
     ```
  2. **`UpdateCampEditionExtraRequestValidator`**: Add same `UserInputLabel` max length rule
- **Implementation Notes**: `RequiresUserInput` is a bool — no validation needed. `UserInputLabel` only needs max length enforcement since it's optional.

---

### Step 7: Update CampEditionExtrasService — Create, Update, and Response Mappings

- **File**: `src/Abuvi.API/Features/Camps/CampEditionExtrasService.cs`
- **Action**: Map the new fields in all create/update/response paths
- **Implementation Steps**:
  1. **In `CreateAsync`** — when constructing the `CampEditionExtra` entity from `CreateCampEditionExtraRequest`, set:
     ```csharp
     RequiresUserInput = request.RequiresUserInput,
     UserInputLabel = request.UserInputLabel
     ```
  2. **In `UpdateAsync`** — when updating the entity from `UpdateCampEditionExtraRequest`, set:
     ```csharp
     extra.RequiresUserInput = request.RequiresUserInput;
     extra.UserInputLabel = request.UserInputLabel;
     ```
  3. **In all response mappings** — wherever `CampEditionExtraResponse` is constructed, include:
     ```csharp
     RequiresUserInput = extra.RequiresUserInput,
     UserInputLabel = extra.UserInputLabel
     ```
- **Implementation Notes**: Search for all occurrences of `new CampEditionExtraResponse(` or `CampEditionExtraResponse` construction to ensure no mapping is missed.

---

### Step 8: Update Registration Extras DTOs

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Update request and response DTOs
- **Implementation Steps**:
  1. **`ExtraSelectionRequest`** (~line 136): Add `string? UserInput = null`:
     ```csharp
     public record ExtraSelectionRequest(Guid CampEditionExtraId, int Quantity, string? UserInput = null);
     ```
  2. **`ExtraPricingDetail`** (~line 221): Add `string? UserInput` at the end:
     ```csharp
     public record ExtraPricingDetail(
         Guid CampEditionExtraId,
         string Name,
         decimal UnitPrice,
         string PricingType,
         string PricingPeriod,
         int Quantity,
         int? CampDurationDays,
         string Calculation,
         decimal TotalAmount,
         string? UserInput
     );
     ```
- **Implementation Notes**: Default value on `ExtraSelectionRequest.UserInput` ensures backward compatibility. Update ALL places where `ExtraPricingDetail` is constructed to include the new parameter.

---

### Step 9: Update Registration Extras Validator

- **File**: `src/Abuvi.API/Features/Registrations/UpdateRegistrationExtrasValidator.cs`
- **Action**: Add validation for `UserInput` max length
- **Implementation Steps**:
  1. Inside the `RuleForEach(x => x.Extras)` child validator, add:
     ```csharp
     RuleFor(x => x.UserInput)
         .MaximumLength(500)
         .When(x => x.UserInput != null);
     ```
- **Implementation Notes**: `UserInput` is optional at the API level. The frontend handles "required when `requiresUserInput` is true" logic. Backend only enforces max length (500 chars).

---

### Step 10: Update RegistrationsService — SetExtrasAsync

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Persist `UserInput` when creating `RegistrationExtra` records and include it in responses
- **Implementation Steps**:
  1. **In `SetExtrasAsync`** (~line 298-366), where `RegistrationExtra` objects are constructed from `ExtraSelectionRequest`, add:
     ```csharp
     UserInput = selection.UserInput
     ```
  2. **In the response mapping** where `ExtraPricingDetail` is built from `RegistrationExtra`, add:
     ```csharp
     UserInput = registrationExtra.UserInput
     ```
- **Implementation Notes**: `SetExtrasAsync` deletes all existing extras and re-adds them (delete + re-add pattern), so `UserInput` is naturally replaced on each save. No special merge logic needed.

---

### Step 11: Create EF Core Migration

- **Action**: Generate a new migration for the schema changes
- **Implementation Steps**:
  1. Navigate to `src/Abuvi.API/`
  2. Run:
     ```bash
     dotnet ef migrations add AddExtraUserInputFields
     ```
  3. Review the generated migration file and verify:
     - `camp_edition_extras` table gets `requires_user_input` (boolean, NOT NULL, default `false`) and `user_input_label` (varchar(200), NULL)
     - `registration_extras` table gets `user_input` (varchar(500), NULL)
  4. Apply locally:
     ```bash
     dotnet ef database update
     ```
- **Implementation Notes**: The `HasDefaultValue(false)` ensures existing rows get `false` automatically during migration. All new string columns are nullable so no data backfill is needed.

---

### Step 12: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **`ai-specs/specs/data-model.md`**: Add or update `CampEditionExtra` and `RegistrationExtra` entity sections with the new fields:
     - `CampEditionExtra.RequiresUserInput` (bool, NOT NULL, default false)
     - `CampEditionExtra.UserInputLabel` (string?, max 200 chars)
     - `RegistrationExtra.UserInput` (string?, max 500 chars)
  2. **`ai-specs/specs/api-spec.yml`**: Update extras endpoints documentation:
     - New fields in `CreateCampEditionExtraRequest` and `UpdateCampEditionExtraRequest`
     - New fields in `CampEditionExtraResponse`
     - New `userInput` field in `ExtraSelectionRequest`
     - New `userInput` field in `ExtraPricingDetail`
  3. Verify auto-generated OpenAPI matches by running the app and checking `/swagger`
- **References**: Follow process described in `ai-specs/specs/documentation-standards.mdc`
- **Notes**: This step is MANDATORY before considering the implementation complete.

---

## Implementation Order

1. Step 0: Create Feature Branch
2. Step 1: Update CampEditionExtra Entity
3. Step 2: Update CampEditionExtra EF Configuration
4. Step 3: Update RegistrationExtra Entity
5. Step 4: Update RegistrationExtra EF Configuration
6. Step 5: Update Camp Edition Extra DTOs
7. Step 6: Update Camp Edition Extras Validators
8. Step 7: Update CampEditionExtrasService Mappings
9. Step 8: Update Registration Extras DTOs
10. Step 9: Update Registration Extras Validator
11. Step 10: Update RegistrationsService SetExtrasAsync
12. Step 11: Create EF Core Migration
13. Step 12: Update Technical Documentation

---

## Testing Checklist

### Unit tests to add/update (xUnit + FluentAssertions + NSubstitute)

**CampEditionExtrasService tests:**
- `CreateAsync_WithRequiresUserInput_PersistsFlag`
- `CreateAsync_WithUserInputLabel_PersistsLabel`
- `CreateAsync_WithoutRequiresUserInput_DefaultsFalse`
- `UpdateAsync_WithRequiresUserInput_UpdatesFlag`
- `UpdateAsync_WithUserInputLabel_UpdatesLabel`

**CampEditionExtrasValidator tests:**
- `Validate_UserInputLabelExceedsMaxLength_ReturnsError` (label > 200 chars)
- `Validate_UserInputLabelNull_IsValid`

**RegistrationsService tests:**
- `SetExtrasAsync_WithUserInput_PersistsText`
- `SetExtrasAsync_WithNullUserInput_PersistsNull`
- `SetExtrasAsync_ResponseIncludesUserInput` (ExtraPricingDetail contains the text)

**UpdateRegistrationExtrasValidator tests:**
- `Validate_UserInputExceedsMaxLength_ReturnsError` (input > 500 chars)
- `Validate_UserInputNull_IsValid`

### Testing patterns
- AAA pattern (Arrange-Act-Assert)
- Test names: `MethodName_StateUnderTest_ExpectedBehavior`
- Target 90% coverage for new/modified code

---

## Error Response Format

All endpoints continue to use the existing `ApiResponse<T>` envelope:

| Status | When |
|--------|------|
| 200 | Successful GET, PUT |
| 201 | Successful POST (create) |
| 400 | Validation errors (FluentValidation) — includes UserInputLabel > 200 or UserInput > 500 |
| 404 | Entity not found |
| 422 | Business rule violation |

No new error codes or response types introduced.

---

## Dependencies

### NuGet packages
No new packages required. All changes use existing Entity Framework Core and FluentValidation.

### EF Core migration commands
```bash
cd src/Abuvi.API
dotnet ef migrations add AddExtraUserInputFields
dotnet ef database update
```

---

## Notes

- **Language**: All code, comments, variable names, and documentation must be in English
- **Backend-only validation**: `UserInput` is optional at the API level. The frontend enforces "required when `requiresUserInput` is true". Backend only enforces max length (500 chars)
- **No RGPD concern**: `UserInput` is general free-text (sizes, preferences), not medical/sensitive data. No encryption required
- **Backward compatibility**: Default values on `CreateCampEditionExtraRequest` (`RequiresUserInput = false`, `UserInputLabel = null`) and `ExtraSelectionRequest` (`UserInput = null`) ensure existing callers work unchanged
- **Migration safety**: New columns have defaults (`false` for bool, `NULL` for strings), so existing data is unaffected
- **No endpoint changes**: No new routes. Existing CRUD endpoints for camp extras and registration extras are extended with the new fields

---

## Next Steps After Implementation

1. Frontend implementation (separate branch) to consume the new fields
2. Manual verification via Swagger that all endpoints accept and return the new fields
3. Verify migration applies cleanly to staging database

---

## Implementation Verification

- [ ] Code compiles without warnings (nullable reference types enabled)
- [ ] All new fields have proper EF Core configuration (column names, max lengths, defaults)
- [ ] Migration generates correct SQL for PostgreSQL
- [ ] All DTOs include the new fields with proper naming
- [ ] Validators enforce max length constraints
- [ ] Service layer maps fields in all create/update/response paths
- [ ] Existing tests still pass
- [ ] New tests cover the added fields (90% coverage target)
- [ ] Documentation updated (`data-model.md`, `api-spec.yml`)

# Backend Implementation Plan: Payment Deadlines Per Camp Edition

## Overview

Move payment installment due dates from global `PaymentSettings` to per-edition configuration on `CampEdition`. Each edition gets `FirstPaymentDeadline` and `SecondPaymentDeadline` (nullable DateTime). When null, defaults are computed as 117 days and 75 days before `StartDate` respectively (based on April 20 / June 1 for a camp starting August 15). The global `SecondInstallmentDaysBefore` setting is removed.

## Architecture Context

**Feature slices involved:**
- `src/Abuvi.API/Features/Camps/` — CampEdition entity, DTOs, service
- `src/Abuvi.API/Features/Payments/` — PaymentsService, PaymentsModels, PaymentsValidators

**Files to modify:**
- `src/Abuvi.API/Features/Camps/CampsModels.cs` — Entity + DTOs
- `src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs` — Column mappings
- `src/Abuvi.API/Features/Camps/CampEditionsService.cs` — Mapping + update logic
- `src/Abuvi.API/Features/Payments/PaymentsService.cs` — Installment creation
- `src/Abuvi.API/Features/Payments/PaymentsModels.cs` — Remove `SecondInstallmentDaysBefore`
- `src/Abuvi.API/Features/Payments/PaymentsValidators.cs` — Remove days validation
- `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — AvailableCampEditionResponse
- `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs` — Update due date tests
- `src/Abuvi.Tests/Unit/Features/Payments/PaymentsValidatorTests.cs` — Remove days tests

**New files:**
- New EF Core migration

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/payment-deadlines-per-edition-backend` from `feat/registrations-payments`
- **Implementation Steps**:
  1. Ensure on `feat/registrations-payments` with latest changes
  2. `git checkout -b feature/payment-deadlines-per-edition-backend`
  3. Verify with `git branch`

### Step 1: Add Fields to CampEdition Entity

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add `FirstPaymentDeadline` and `SecondPaymentDeadline` to the `CampEdition` class
- **Implementation Steps**:
  1. Add after the `MaxWeekendCapacity` property (before `AccommodationCapacityJson`):
     ```csharp
     // Payment deadlines for this edition (null = use defaults: 117 / 75 days before StartDate)
     public DateTime? FirstPaymentDeadline { get; set; }
     public DateTime? SecondPaymentDeadline { get; set; }
     ```
  2. Both are nullable — existing editions without dates will use computed defaults

### Step 2: Update EF Core Configuration

- **File**: `src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs`
- **Action**: Add column mappings for the new fields
- **Implementation Steps**:
  1. Add property mappings:
     ```csharp
     builder.Property(e => e.FirstPaymentDeadline)
         .HasColumnName("first_payment_deadline");
     builder.Property(e => e.SecondPaymentDeadline)
         .HasColumnName("second_payment_deadline");
     ```

### Step 3: Create EF Core Migration

- **Action**: Generate migration for the new columns
- **Command**: `dotnet ef migrations add AddPaymentDeadlinesToCampEdition --project src/Abuvi.API`
- **Implementation Notes**:
  - Both columns are nullable timestamps
  - No data migration needed — null values will trigger default computation

### Step 4: Update CampEdition DTOs

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add `FirstPaymentDeadline` and `SecondPaymentDeadline` to all relevant DTOs
- **Implementation Steps**:
  1. Add to `UpdateCampEditionRequest` record (as optional parameters with null defaults):
     ```csharp
     DateTime? FirstPaymentDeadline = null,
     DateTime? SecondPaymentDeadline = null
     ```
  2. Add to `CampEditionResponse` record:
     ```csharp
     DateTime? FirstPaymentDeadline,
     DateTime? SecondPaymentDeadline
     ```
  3. Add to `ActiveCampEditionResponse` record:
     ```csharp
     DateTime? FirstPaymentDeadline,
     DateTime? SecondPaymentDeadline
     ```
  4. Add to `CurrentCampEditionResponse` record:
     ```csharp
     DateTime? FirstPaymentDeadline,
     DateTime? SecondPaymentDeadline
     ```

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add deadline fields to `AvailableCampEditionResponse` so the registration wizard can display them
- **Implementation Steps**:
  1. Add after the `WeekendSpotsRemaining` field:
     ```csharp
     DateTime? FirstPaymentDeadline,
     DateTime? SecondPaymentDeadline
     ```

### Step 5: Update CampEditionsService Mapping

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: Map new fields in all response methods and accept them in update
- **Implementation Steps**:
  1. In `MapToCampEditionResponse()`: Add `edition.FirstPaymentDeadline` and `edition.SecondPaymentDeadline` to the response constructor
  2. In `MapToActiveCampEditionResponse()` (if it exists): Same mapping
  3. In `MapToCurrentCampEditionResponse()` (if it exists): Same mapping
  4. In `UpdateAsync()`: Accept and persist the new fields:
     ```csharp
     edition.FirstPaymentDeadline = request.FirstPaymentDeadline;
     edition.SecondPaymentDeadline = request.SecondPaymentDeadline;
     ```
  5. In the `AvailableCampEditionResponse` mapping (in RegistrationsService or wherever it's mapped): Include `edition.FirstPaymentDeadline` and `edition.SecondPaymentDeadline`
- **Implementation Notes**: The update should be allowed in all edition statuses (Proposed, Draft, Open) since payment configuration may be adjusted at any time.

### Step 6: Update PaymentsService — CreateInstallmentsAsync

- **File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- **Action**: Use edition-specific deadlines instead of global settings
- **Implementation Steps**:
  1. Add default constants to the class:
     ```csharp
     private const int DefaultFirstPaymentDaysBefore = 117;
     private const int DefaultSecondPaymentDaysBefore = 75;
     ```
  2. In `CreateInstallmentsAsync`, replace the due date computation:

     **Before:**
     ```csharp
     // Installment 1 DueDate = DateTime.UtcNow
     DueDate = DateTime.UtcNow,
     // Installment 2
     var dueDate2 = registration.CampEdition.StartDate
         .AddDays(-settings.SecondInstallmentDaysBefore);
     ```

     **After:**
     ```csharp
     var dueDate1 = registration.CampEdition.FirstPaymentDeadline
         ?? registration.CampEdition.StartDate.AddDays(-DefaultFirstPaymentDaysBefore);
     var dueDate2 = registration.CampEdition.SecondPaymentDeadline
         ?? registration.CampEdition.StartDate.AddDays(-DefaultSecondPaymentDaysBefore);
     ```
  3. Use `dueDate1` for installment 1 and `dueDate2` for installment 2
  4. The `LoadPaymentSettingsAsync` call remains for IBAN, bank info, and `TransferConceptPrefix`

### Step 7: Remove SecondInstallmentDaysBefore from PaymentSettings

- **File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
- **Action**: Remove `SecondInstallmentDaysBefore` from all settings DTOs
- **Implementation Steps**:
  1. Remove from `PaymentSettingsResponse` record:
     - **Before**: `PaymentSettingsResponse(string Iban, string BankName, string AccountHolder, int SecondInstallmentDaysBefore, string TransferConceptPrefix)`
     - **After**: `PaymentSettingsResponse(string Iban, string BankName, string AccountHolder, string TransferConceptPrefix)`
  2. Remove from `PaymentSettingsRequest` record — same change
  3. Remove from `PaymentSettingsJson` class:
     - Remove `public int SecondInstallmentDaysBefore { get; set; } = 15;`
     - Keep the property in the JSON class for backward compatibility during deserialization (or remove — the `JsonSerializer` will ignore unknown properties with default options). **Safest**: remove it entirely since `JsonSerializer` with default options ignores extra JSON properties on deserialization.

- **File**: `src/Abuvi.API/Features/Payments/PaymentsValidators.cs`
- **Action**: Remove `SecondInstallmentDaysBefore` validation rule
- **Implementation Steps**:
  1. In `PaymentSettingsRequestValidator`, remove:
     ```csharp
     RuleFor(x => x.SecondInstallmentDaysBefore)
         .InclusiveBetween(1, 90)
         .WithMessage("...");
     ```

- **File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- **Action**: Update settings mapping methods
- **Implementation Steps**:
  1. In `GetPaymentSettingsAsync`: Remove `settings.SecondInstallmentDaysBefore` from response constructor
  2. In `UpdatePaymentSettingsAsync`: Remove `SecondInstallmentDaysBefore` from the `PaymentSettingsJson` construction and from the returned `PaymentSettingsResponse`

### Step 8: Update Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs`
- **Action**: Update existing due date tests and add new ones
- **Implementation Steps**:
  1. **Update** `CreateInstallmentsAsync_ValidRegistration_SetsCorrectDueDates`:
     - Set `registration.CampEdition.FirstPaymentDeadline = null` and `SecondPaymentDeadline = null`
     - Assert installment 1 DueDate = `StartDate.AddDays(-117)` (not `DateTime.UtcNow`)
     - Assert installment 2 DueDate = `StartDate.AddDays(-75)` (not `StartDate.AddDays(-15)`)
  2. **Add** `CreateInstallmentsAsync_EditionHasDeadlines_UsesEditionDates`:
     - Set explicit `FirstPaymentDeadline = new DateTime(2026, 4, 20)` and `SecondPaymentDeadline = new DateTime(2026, 6, 1)` on the CampEdition
     - Assert installment 1 DueDate = `2026-04-20`
     - Assert installment 2 DueDate = `2026-06-01`
  3. **Add** `CreateInstallmentsAsync_EditionHasPartialDeadlines_UsesDefaultForMissing`:
     - Set only `FirstPaymentDeadline`, leave `SecondPaymentDeadline = null`
     - Assert installment 1 uses the explicit date
     - Assert installment 2 falls back to `StartDate.AddDays(-75)`
  4. **Update** `CreateInstallmentsAsync_NoPaymentSettings_UsesDefaults`: Remove expectation for `SecondInstallmentDaysBefore` if it checks that field
  5. Update all test fixtures that create mock `PaymentSettingsJson` to remove `SecondInstallmentDaysBefore`

- **File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsValidatorTests.cs`
- **Action**: Remove `SecondInstallmentDaysBefore` validator tests
- **Implementation Steps**:
  1. Remove tests for `SecondInstallmentDaysBefore` validation (valid range 1-90)
  2. Keep all other validator tests unchanged

### Step 9: Update Technical Documentation

- **Action**: Review and update documentation
- **Implementation Steps**:
  1. Update `ai-specs/specs/data-model.md` (if it exists) — document new `CampEdition` fields
  2. Verify OpenAPI spec reflects updated endpoints
  3. Update the enriched user story if any implementation details diverge
- **Notes**: Mandatory before considering implementation complete

---

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Add fields to CampEdition entity
3. **Step 2**: Update EF Core configuration
4. **Step 3**: Create EF Core migration
5. **Step 4**: Update CampEdition DTOs (all response + request records)
6. **Step 5**: Update CampEditionsService mapping + update logic
7. **Step 6**: Update PaymentsService.CreateInstallmentsAsync
8. **Step 7**: Remove SecondInstallmentDaysBefore from PaymentSettings (models, validators, service)
9. **Step 8**: Update unit tests
10. **Step 9**: Update technical documentation

---

## Testing Checklist

### PaymentsServiceTests (update/add)
- [ ] `CreateInstallmentsAsync_EditionHasDeadlines_UsesEditionDates` — explicit dates used directly
- [ ] `CreateInstallmentsAsync_EditionHasNoDeadlines_UsesDefaultCalculation` — 117/75 day fallbacks
- [ ] `CreateInstallmentsAsync_EditionHasPartialDeadlines_UsesDefaultForMissing` — mixed explicit + fallback
- [ ] Existing tests updated to reflect new default (117/75 instead of now/15)

### PaymentsValidatorTests (remove)
- [ ] Remove `SecondInstallmentDaysBefore` range validation tests

### Existing tests (verify no regressions)
- [ ] All existing PaymentsService tests still pass
- [ ] All existing CampEditionsService tests still pass
- [ ] `dotnet test` passes for entire solution

---

## Error Response Format

Standard `ApiResponse<T>` envelope. No new error codes needed — this change only modifies how due dates are computed internally and adds optional fields to existing endpoints.

---

## Dependencies

- No new NuGet packages required
- **Migration command**: `dotnet ef migrations add AddPaymentDeadlinesToCampEdition --project src/Abuvi.API`

---

## Notes

- **Backward compatibility**: Existing `PaymentSettingsJson` in the database may still contain `SecondInstallmentDaysBefore`. The `JsonSerializer` with default options ignores extra properties during deserialization, so no data migration is needed.
- **Null defaults**: When `FirstPaymentDeadline` / `SecondPaymentDeadline` are null on a CampEdition, the system computes defaults: `StartDate - 117 days` and `StartDate - 75 days`.
- **Default calculation source**: April 20 and June 1 for a camp starting August 15 → 117 and 75 days respectively.
- **Already-created payments**: Existing payments that were created with the old logic (immediate / 15 days before) keep their due dates. Only new registrations will use the edition-specific dates.
- **Language**: All code in English, user-facing messages in Spanish.

---

## Next Steps After Implementation

1. Frontend implementation: update types, remove days field from settings form, add date pickers to camp edition admin form
2. Integration testing with frontend
3. Consider adding validation on CampEdition: `FirstPaymentDeadline < SecondPaymentDeadline < StartDate` (optional business rule)

---

## Implementation Verification

- [ ] `CampEdition` entity has `FirstPaymentDeadline` and `SecondPaymentDeadline` (nullable DateTime)
- [ ] EF Core configuration maps new columns
- [ ] Migration creates `first_payment_deadline` and `second_payment_deadline` columns
- [ ] All CampEdition DTOs include the new fields
- [ ] `CampEditionsService` maps and accepts the new fields
- [ ] `PaymentsService.CreateInstallmentsAsync` uses edition dates with 117/75 day fallbacks
- [ ] `SecondInstallmentDaysBefore` removed from `PaymentSettingsResponse`, `PaymentSettingsRequest`, `PaymentSettingsJson`
- [ ] Validator no longer checks `SecondInstallmentDaysBefore`
- [ ] Unit tests updated and all pass
- [ ] `dotnet build` and `dotnet test` succeed
- [ ] Documentation updated

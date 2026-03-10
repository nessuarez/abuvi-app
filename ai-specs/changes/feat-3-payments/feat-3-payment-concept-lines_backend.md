# Backend Implementation Plan: feat-3-payment-concept-lines — Payment Concept Lines

## Overview

Add a `ConceptLinesSerialized` field to the `Payment` entity that stores a JSON snapshot of what each installment covers. For P1/P2 (base installments), it stores one line per registration member with name, age category, attendance period, and amount attribution. For P3 (extras), it stores one aggregated line per extra type. This field is generated at write time and regenerated on recalculation, following the existing Vertical Slice Architecture pattern within the Payments feature.

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Payments/` (primary) + entity in `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Files to modify**:
  - `RegistrationsModels.cs` — Add property to `Payment` entity
  - `PaymentConfiguration.cs` — Add column mapping
  - `PaymentsModels.cs` — Add concept line records and extend response DTOs
  - `PaymentsService.cs` — Generate concept lines in create/sync methods
  - `PaymentsEndpoints.cs` — Deserialize concept lines when building responses
- **Files to create**:
  - EF Core migration for `concept_lines` column
- **Cross-cutting**: None. No new middleware, filters, or shared types needed.

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a backend-specific branch
- **Branch Naming**: `feature/feat-3-payment-concept-lines-backend`
- **Implementation Steps**:
  1. Ensure you're on latest `dev`: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/feat-3-payment-concept-lines-backend`
  3. Verify: `git branch`
- **Notes**: If already on `feature/feat-3-payments-backend`, create the sub-branch from there instead of `dev`.

### Step 1: Add `ConceptLinesSerialized` Property to Payment Entity

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add a nullable string property to the `Payment` class
- **Implementation Steps**:
  1. Add `public string? ConceptLinesSerialized { get; set; }` to the `Payment` class (after `AdminNotes`)
- **Notes**: This is a single text column storing JSON. No navigation property, no separate table.

### Step 2: Add EF Core Configuration for `concept_lines` Column

- **File**: `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs`
- **Action**: Map the new property to a `concept_lines` column
- **Implementation Steps**:
  1. Add property configuration after `AdminNotes` mapping:
     ```csharp
     builder.Property(p => p.ConceptLinesSerialized)
         .HasColumnName("concept_lines")
         .HasColumnType("text")
         .IsRequired(false);
     ```
- **Notes**: `text` type (not `varchar`) since JSON payload length varies. No index needed — this field is never queried directly.

### Step 3: Add Concept Line Records and Extend Response DTOs

- **File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
- **Action**: Add internal records for serialization and response DTOs
- **Implementation Steps**:
  1. Add `PaymentConceptLine` record (internal, for JSON serialization/deserialization):
     ```csharp
     public record PaymentConceptLine(
         string PersonFullName,
         string AgeCategory,
         string AttendancePeriod,
         decimal IndividualAmount,
         decimal AmountInPayment,
         decimal Percentage
     );
     ```
  2. Add `PaymentExtraConceptLine` record:
     ```csharp
     public record PaymentExtraConceptLine(
         string ExtraName,
         int Quantity,
         decimal UnitPrice,
         decimal TotalAmount,
         string? UserInput,
         string PricingType
     );
     ```
  3. Add a wrapper record for serialization (both types stored in same JSON field):
     ```csharp
     public record PaymentConceptLinesJson(
         List<PaymentConceptLine>? MemberLines,
         List<PaymentExtraConceptLine>? ExtraLines
     );
     ```
  4. Extend `PaymentResponse` with:
     ```csharp
     List<PaymentConceptLine>? ConceptLines,
     List<PaymentExtraConceptLine>? ExtraConceptLines
     ```
  5. Extend `AdminPaymentResponse` with the same two fields.

- **Notes**: Using a single wrapper `PaymentConceptLinesJson` for serialization into the one `ConceptLinesSerialized` column avoids needing to distinguish between member vs extra lines at the storage level. At the response level, they are separated into `ConceptLines` and `ExtraConceptLines` for clarity.

### Step 4: Implement Concept Line Generation in PaymentsService

- **File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- **Action**: Generate and assign concept lines when creating or recalculating installments
- **Implementation Steps**:

  1. **Add private helper `GenerateBaseConceptLines()`**:
     ```csharp
     private static string SerializeConceptLines(
         ICollection<RegistrationMember> members,
         decimal installmentAmount,
         decimal baseTotalAmount)
     ```
     - For each `RegistrationMember`, create a `PaymentConceptLine`:
       - `PersonFullName`: load from `member.FamilyMember.FirstName + " " + member.FamilyMember.LastName` (requires eager loading — see dependency note)
       - `AgeCategory`: map enum → `"Adulto"` / `"Niño"` / `"Bebé"`
       - `AttendancePeriod`: map enum → `"Completo"` / `"1ª Semana"` / `"2ª Semana"` / `"Fin de semana"`
       - `IndividualAmount`: `member.IndividualAmount`
       - `Percentage`: `Math.Round(installmentAmount / baseTotalAmount * 100, 2)` (same for all members in a given installment)
       - `AmountInPayment`: `Math.Round(member.IndividualAmount * installmentAmount / baseTotalAmount, 2)` with rounding adjustment on last member so sum matches `installmentAmount` exactly
     - Wrap in `PaymentConceptLinesJson(MemberLines: lines, ExtraLines: null)`
     - Serialize with `JsonSerializer.Serialize()`
     - Return serialized string

  2. **Add private helper `GenerateExtrasConceptLines()`**:
     ```csharp
     private static string SerializeExtrasConceptLines(
         ICollection<RegistrationExtra> extras)
     ```
     - For each `RegistrationExtra`, create a `PaymentExtraConceptLine`:
       - `ExtraName`: loaded from `extra.CampEditionExtra.Name` (requires eager loading)
       - `Quantity`: `extra.Quantity`
       - `UnitPrice`: `extra.UnitPrice`
       - `TotalAmount`: `extra.TotalAmount`
       - `UserInput`: `extra.UserInput`
       - `PricingType`: from `extra.CampEditionExtra.PricingType.ToString()` → `"PerPerson"` / `"PerFamily"`
     - Wrap in `PaymentConceptLinesJson(MemberLines: null, ExtraLines: lines)`
     - Serialize and return

  3. **Modify `CreateInstallmentsAsync()`** (lines 20-84):
     - After loading the registration, ensure `Members` collection includes `FamilyMember` navigation (add `.Include(r => r.Members).ThenInclude(m => m.FamilyMember)` to the registration query if not already present)
     - After creating P1 and P2 Payment objects, assign:
       ```csharp
       p1.ConceptLinesSerialized = SerializeConceptLines(
           registration.Members, p1Amount, registration.BaseTotalAmount);
       p2.ConceptLinesSerialized = SerializeConceptLines(
           registration.Members, p2Amount, registration.BaseTotalAmount);
       ```

  4. **Modify `SyncExtrasInstallmentAsync()`** (lines 370-443):
     - When creating or updating P3, ensure `Extras` collection includes `CampEditionExtra` navigation (add `.Include(r => r.Extras).ThenInclude(e => e.CampEditionExtra)`)
     - Assign:
       ```csharp
       p3.ConceptLinesSerialized = SerializeExtrasConceptLines(registration.Extras);
       ```

  5. **Modify `SyncBaseInstallmentsAsync()`** (lines 445-489):
     - When recalculating P1/P2 amounts, regenerate concept lines:
     - Ensure `Members` with `FamilyMember` are loaded
     - After updating amounts, assign new concept lines:
       ```csharp
       p1.ConceptLinesSerialized = SerializeConceptLines(
           registration.Members, p1.Amount, newBaseTotalAmount);
       p2.ConceptLinesSerialized = SerializeConceptLines(
           registration.Members, p2.Amount, newBaseTotalAmount);
       ```
     - Handle scenario where P1 is Completed (fixed): regenerate only P2's concept lines

- **Dependencies**:
  - The registration queries in these methods must eager-load `Members.FamilyMember` and `Extras.CampEditionExtra`. Check the current `IRegistrationsRepository` methods used — if they don't include these navigations, either:
    - (a) Add an overload/parameter to the existing repository method, or
    - (b) Add a new repository method specifically for payment concept line generation
  - `System.Text.Json.JsonSerializer` (already available in .NET 9)

- **Implementation Notes**:
  - The `AgeCategory` and `AttendancePeriod` enum-to-Spanish-string mappings should be a simple `switch` expression within the helper method. No need for a resource file — these are internal display labels stored in JSON.
  - Rounding adjustment: sum all `AmountInPayment` values. If the sum differs from `installmentAmount`, adjust the last member's `AmountInPayment` by the difference (can be +/- 0.01€).

### Step 5: Update MapToResponse / MapToAdminResponse Helpers

- **File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- **Action**: Deserialize `ConceptLinesSerialized` into the response DTOs
- **Implementation Steps**:
  1. In `MapToResponse()`, deserialize the JSON and populate `ConceptLines` and `ExtraConceptLines`:
     ```csharp
     PaymentConceptLinesJson? conceptData = null;
     if (payment.ConceptLinesSerialized is not null)
     {
         conceptData = JsonSerializer.Deserialize<PaymentConceptLinesJson>(
             payment.ConceptLinesSerialized);
     }
     ```
  2. Pass `conceptData?.MemberLines` to `ConceptLines` and `conceptData?.ExtraLines` to `ExtraConceptLines` in the response record constructor.
  3. Repeat for `MapToAdminResponse()`.

- **Notes**: If deserialization fails (corrupted data), log a warning and return `null` for both fields. Do not throw.

### Step 6: Create EF Core Migration

- **Action**: Generate and apply migration for the new `concept_lines` column
- **Implementation Steps**:
  1. Run from project root:
     ```bash
     dotnet ef migrations add AddPaymentConceptLines \
       --project src/Abuvi.API \
       --startup-project src/Abuvi.API
     ```
  2. Review generated migration: should only add a `text` nullable column `concept_lines` to `payments` table
  3. Apply migration:
     ```bash
     dotnet ef database update \
       --project src/Abuvi.API \
       --startup-project src/Abuvi.API
     ```

- **Notes**: Existing rows will have `NULL` for `concept_lines`. No data backfill needed — concept lines are generated going forward only.

### Step 7: Write Unit Tests

- **File**: `tests/Abuvi.API.Tests/Features/Payments/PaymentsServiceTests.cs` (or new file if it doesn't exist)
- **Action**: Add tests for concept line generation
- **Implementation Steps**:

  1. **Test: P1/P2 concept lines generated with correct member data**
     - Create a registration with 2 adults (Complete) and 1 child (FirstWeek)
     - Call `CreateInstallmentsAsync()`
     - Assert P1 and P2 have non-null `ConceptLinesSerialized`
     - Deserialize and verify: 3 lines, correct names, categories, periods
     - Verify `AmountInPayment` values sum to installment amount
     - Verify `Percentage` is ~50 for both installments

  2. **Test: P3 concept lines generated with correct extras data**
     - Create a registration with 2 extras (one PerPerson, one PerFamily)
     - Call `SyncExtrasInstallmentAsync()`
     - Assert P3 has non-null `ConceptLinesSerialized`
     - Deserialize and verify: 2 extra lines, correct names, quantities, pricing types

  3. **Test: Concept lines regenerated on SyncBaseInstallmentsAsync**
     - Create registration, generate installments
     - Modify members (add one), call `SyncBaseInstallmentsAsync()`
     - Verify P1/P2 concept lines now show updated member list and recalculated amounts

  4. **Test: Concept lines regenerated on SyncExtrasInstallmentAsync update**
     - Create P3, then change extras amount
     - Verify P3 concept lines reflect updated extras

  5. **Test: Rounding adjustment works correctly**
     - Create registration with 3 members where individual amounts don't split evenly
     - Verify `AmountInPayment` values sum exactly to installment amount (no rounding drift)

  6. **Test: Null concept lines don't break response mapping**
     - Payment with `ConceptLinesSerialized = null`
     - Verify `MapToResponse()` returns null for both concept fields without error

- **Dependencies**: xUnit, FluentAssertions, NSubstitute
- **Notes**: Follow existing test patterns in the project. Use `NSubstitute` for repository mocks.

### Step 8: Update Technical Documentation

- **Action**: Update documentation to reflect the new field
- **Implementation Steps**:
  1. **Update `ai-specs/specs/data-model.md`**:
     - Add `conceptLinesSerialized` to the Payment entity fields
     - Document the JSON schema (PaymentConceptLinesJson wrapper with MemberLines/ExtraLines)
     - Note: nullable, generated at write time
  2. **Review `ai-specs/specs/api-spec.yml`** (if exists):
     - Add `conceptLines` and `extraConceptLines` to PaymentResponse and AdminPaymentResponse schemas
  3. **Update the enriched spec** `ai-specs/changes/feat-3-payments/feat-3-payment-concept-lines_enriched.md`:
     - Mark as implemented, note any deviations from the original spec
- **References**: Follow `ai-specs/specs/documentation-standards.mdc` — all documentation in English.

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Add `ConceptLinesSerialized` to Payment entity
3. **Step 2**: Add EF Core column configuration
4. **Step 3**: Add concept line records and extend response DTOs
5. **Step 4**: Implement concept line generation in PaymentsService (create + sync methods)
6. **Step 5**: Update MapToResponse / MapToAdminResponse
7. **Step 6**: Create and apply EF Core migration
8. **Step 7**: Write unit tests
9. **Step 8**: Update technical documentation

## Testing Checklist

- [ ] P1/P2 concept lines contain correct member data (name, age category, period, amounts)
- [ ] P3 concept lines contain correct extras data (name, quantity, pricing type)
- [ ] Concept lines regenerated when `SyncBaseInstallmentsAsync` runs
- [ ] Concept lines regenerated when `SyncExtrasInstallmentAsync` runs
- [ ] Rounding: `AmountInPayment` values sum exactly to installment `Amount`
- [ ] Null `ConceptLinesSerialized` handled gracefully in response mapping
- [ ] Existing payments (null concept lines) don't break API responses
- [ ] Migration adds only the `concept_lines` column with no side effects
- [ ] 90% test coverage on new/modified code

## Error Response Format

No new error responses. This feature only adds data to existing responses. The `ApiResponse<T>` envelope is unchanged.

If JSON deserialization of `ConceptLinesSerialized` fails at read time:
- Log warning with payment ID
- Return `null` for `ConceptLines` and `ExtraConceptLines`
- Do **not** throw or return error to client

## Dependencies

- **NuGet packages**: None new. `System.Text.Json` is built into .NET 9.
- **EF Core migration**: `dotnet ef migrations add AddPaymentConceptLines`

## Notes

- **Language**: Code, comments, and documentation in English. The Spanish display labels (`"Adulto"`, `"Niño"`, `"Bebé"`, `"Completo"`, `"1ª Semana"`, `"2ª Semana"`, `"Fin de semana"`) are stored as data values in JSON, not code-level strings. They could be extracted to constants for consistency.
- **No backfill**: Existing payments will have `null` concept lines. This is acceptable per the enriched spec.
- **RGPD/GDPR**: Person full names are already present in `FamilyMember` (non-encrypted). Storing them in concept lines JSON does not introduce new privacy concerns — the data is already accessible via the registration.
- **Performance**: JSON serialization at write time has negligible overhead. No additional queries needed if navigations are already loaded (which they typically are in create/sync flows). If not, add targeted `.Include()` calls.
- **Idempotency**: Concept lines are fully regenerated on each sync, not appended. No risk of stale or duplicate lines.

## Next Steps After Implementation

1. Frontend integration: display concept lines in the payment detail view (separate frontend ticket)
2. Consider adding concept lines to admin payment export (CSV/PDF) in the future
3. Consider backfill command for existing payments if historically needed

## Implementation Verification

- [ ] **Code Quality**: No nullable warnings, no C# analyzer issues
- [ ] **Functionality**: GET payment endpoints return `conceptLines` and `extraConceptLines` fields
- [ ] **Testing**: 90%+ coverage on new/modified code with xUnit + FluentAssertions + NSubstitute
- [ ] **Integration**: EF Core migration applied successfully, no schema conflicts
- [ ] **Documentation**: data-model.md and api-spec updated

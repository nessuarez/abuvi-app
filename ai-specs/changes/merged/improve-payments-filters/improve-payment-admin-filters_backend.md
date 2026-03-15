# Backend Implementation Plan: improve-payment-admin-filters — Installment Number Filter

## Overview

Add an optional `InstallmentNumber` query parameter to the `GET /admin/payments` endpoint so administrators can filter payments by payment period (Plazo 1, Plazo 2, Plazo 3+). This follows Vertical Slice Architecture — all changes stay within the `Payments` feature slice. No new entities, migrations, or service registrations are required.

## Architecture Context

- **Feature slice:** `src/Abuvi.API/Features/Payments/`
- **Files to modify:**
  - `PaymentsModels.cs` — Add `InstallmentNumber` to `PaymentFilterRequest`
  - `PaymentsRepository.cs` — Add filter clause in `GetFilteredAsync`
- **Files unchanged:**
  - `PaymentsEndpoints.cs` — `[AsParameters]` binding automatically picks up the new record property
  - `IPaymentsRepository.cs` — Signature unchanged (takes `PaymentFilterRequest`)
  - `IPaymentsService.cs` — Signature unchanged (passes `PaymentFilterRequest` through)
  - `PaymentsService.cs` — Just passes filter to repository, no changes needed
- **New test file:**
  - `src/Abuvi.Tests/Unit/Features/Payments/PaymentsRepository_FilterTests.cs`

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/improve-payment-admin-filters-backend`
- **Implementation Steps**:
  1. Ensure on `dev` branch: `git checkout dev`
  2. Pull latest: `git pull origin dev`
  3. Create branch: `git checkout -b feature/improve-payment-admin-filters-backend`
  4. Verify: `git branch`

### Step 1: Add `InstallmentNumber` to `PaymentFilterRequest`

- **File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
- **Action**: Add optional `int? InstallmentNumber` parameter to the `PaymentFilterRequest` record
- **Current signature (line 88-95)**:
  ```csharp
  public record PaymentFilterRequest(
      PaymentStatus? Status = null,
      Guid? CampEditionId = null,
      DateTime? FromDate = null,
      DateTime? ToDate = null,
      int Page = 1,
      int PageSize = 20
  );
  ```
- **Updated signature**:
  ```csharp
  public record PaymentFilterRequest(
      PaymentStatus? Status = null,
      Guid? CampEditionId = null,
      int? InstallmentNumber = null,
      DateTime? FromDate = null,
      DateTime? ToDate = null,
      int Page = 1,
      int PageSize = 20
  );
  ```
- **Implementation Notes**:
  - Default is `null` (no filter applied) — fully backwards compatible
  - Placed after `CampEditionId` and before date filters for logical grouping
  - The `[AsParameters]` binding in `PaymentsEndpoints.cs` (line 229) will automatically bind `?InstallmentNumber=1` from query string — no endpoint changes needed

### Step 2: Add Filter Clause in Repository

- **File**: `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`
- **Action**: Add `InstallmentNumber` filter logic in `GetFilteredAsync` method (after the `CampEditionId` filter at line 93)
- **Code to add** (after line 93, before `FromDate` filter):
  ```csharp
  if (filter.InstallmentNumber.HasValue)
  {
      if (filter.InstallmentNumber.Value >= 3)
          query = query.Where(p => p.InstallmentNumber >= 3);
      else
          query = query.Where(p => p.InstallmentNumber == filter.InstallmentNumber.Value);
  }
  ```
- **Implementation Notes**:
  - When `InstallmentNumber = 1` → exact match for installment 1
  - When `InstallmentNumber = 2` → exact match for installment 2
  - When `InstallmentNumber >= 3` → matches installment 3 and any higher (manual payments can push installment numbers beyond 3)
  - This logic mirrors the business domain: there are exactly 3 structured payment periods (P1 = first installment, P2 = second installment, P3+ = extras/manual)
  - EF Core translates this directly to a SQL WHERE clause — no performance concern

### Step 3: Write Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsRepository_FilterTests.cs` (new file)
- **Action**: Integration-style tests for the repository filter using an in-memory database
- **Dependencies**: `Microsoft.EntityFrameworkCore.InMemory` (if available) or verify existing test patterns for repository tests
- **Test Cases**:

  1. **`GetFilteredAsync_WithInstallmentNumber1_ReturnsOnlyFirstInstallment`**
     - Arrange: Seed payments with installment numbers 1, 2, 3
     - Act: Call with `InstallmentNumber = 1`
     - Assert: Only installment 1 payments returned

  2. **`GetFilteredAsync_WithInstallmentNumber2_ReturnsOnlySecondInstallment`**
     - Arrange: Seed payments with installment numbers 1, 2, 3
     - Act: Call with `InstallmentNumber = 2`
     - Assert: Only installment 2 payments returned

  3. **`GetFilteredAsync_WithInstallmentNumber3_ReturnsThirdAndHigher`**
     - Arrange: Seed payments with installment numbers 1, 2, 3, 4
     - Act: Call with `InstallmentNumber = 3`
     - Assert: Payments with installment 3 and 4 returned

  4. **`GetFilteredAsync_WithoutInstallmentNumber_ReturnsAll`**
     - Arrange: Seed payments with installment numbers 1, 2, 3
     - Act: Call with `InstallmentNumber = null`
     - Assert: All payments returned

  5. **`GetFilteredAsync_CombinedFilters_AppliesBothStatusAndInstallment`**
     - Arrange: Seed payments with varying statuses and installment numbers
     - Act: Call with `Status = Completed` and `InstallmentNumber = 1`
     - Assert: Only completed installment 1 payments returned

- **Testing Pattern**: Follow existing patterns from `PaymentsServiceTests.cs`:
  - Use xUnit `[Fact]` attributes
  - Use FluentAssertions for assertions
  - Use NSubstitute for mocking (if testing at service level) or in-memory DbContext (if testing repository directly)

### Step 4: Update Technical Documentation

- **Action**: Review and update technical documentation
- **Implementation Steps**:
  1. **API Spec**: Update `ai-specs/specs/api-spec.yml` to add the `InstallmentNumber` query parameter to the `GET /admin/payments` endpoint documentation
  2. **Verify**: Auto-generated OpenAPI from the running app will reflect the new parameter automatically, but the manual spec should be kept in sync
- **Notes**: No data model changes (no new entities/columns), no migration, no standards changes

## Implementation Order

1. Step 0: Create feature branch
2. Step 1: Add `InstallmentNumber` to `PaymentFilterRequest` (models)
3. Step 2: Add filter clause in repository
4. Step 3: Write unit tests
5. Step 4: Update technical documentation

## Testing Checklist

- [ ] `InstallmentNumber = 1` returns only first installment payments
- [ ] `InstallmentNumber = 2` returns only second installment payments
- [ ] `InstallmentNumber = 3` returns installment 3 and higher
- [ ] `InstallmentNumber = null` (omitted) returns all payments — backwards compatible
- [ ] Combined filters (`Status` + `InstallmentNumber`) work correctly
- [ ] Pagination works correctly with the new filter applied
- [ ] Endpoint binds query parameter correctly via `[AsParameters]`

## Error Response Format

No new error responses introduced. The existing `ApiResponse<T>` envelope is unchanged:
- `200 OK` with `ApiResponse<{ Items, TotalCount, Page, PageSize }>` — same as today
- Invalid `InstallmentNumber` values (e.g., 0, -1) will simply return no results — no validation error needed since these are edge cases with no security impact

## Dependencies

- No new NuGet packages
- No EF Core migration (no schema changes — `InstallmentNumber` already exists on the `Payment` entity)

## Notes

- **Backwards compatibility**: The parameter is optional with default `null`. Existing API consumers are fully unaffected.
- **Business rule**: Installment numbers are: 1 (first payment, ~50%), 2 (second payment, ~50%), 3 (extras), and potentially higher for manual payments. The "3+" grouping captures all of these.
- **Language**: All code artifacts in English as per `base-standards.mdc`.
- **No RGPD/GDPR concerns**: This is a read-only filter on existing data with no new personal data exposure.

## Next Steps After Implementation

- Implement the frontend counterpart (see `improve-payment-admin-filters_enriched.md` sections 2.2–2.4)
- The frontend will add the `installmentNumber` field to `PaymentFilterParams` type and the `usePayments` composable

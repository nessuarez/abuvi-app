# Payment Deadlines Per Camp Edition - Enriched User Story

## Objective

Move payment installment due dates from global `PaymentSettings` to per-edition configuration on `CampEdition`. Each camp edition should specify its own first and second payment deadlines, allowing different camp editions to have different payment schedules. The global setting `secondInstallmentDaysBefore` is replaced by two explicit dates on each edition.

## Context

### Current State

- Payment installment due dates are computed dynamically:
  - **Installment 1**: `DueDate = DateTime.UtcNow` (immediate)
  - **Installment 2**: `DueDate = CampEdition.StartDate - PaymentSettings.SecondInstallmentDaysBefore` (default 15 days before camp)
- `PaymentSettings` (in `AssociationSettings` JSON) has `secondInstallmentDaysBefore: int` (default 15)
- `PaymentsService.CreateInstallmentsAsync()` reads the global setting and computes due dates at registration creation time

### Problem

The current approach is too rigid:
1. Due dates are relative to camp start date only, with a single global offset
2. In practice, camps need specific calendar dates (e.g., "April 20" and "June 1" for a camp starting August 15)
3. Different editions may need different payment schedules
4. The first payment is always "immediate" which doesn't allow a grace period

### Desired Behavior

- Each `CampEdition` stores two explicit deadline dates: `FirstPaymentDeadline` and `SecondPaymentDeadline`
- When creating installments, use these dates directly as `DueDate` for each installment
- Default values when creating/updating a camp edition (if not specified): 117 days before start for 1st payment, 75 days before start for 2nd payment (based on: April 20 and June 1 for a camp starting August 15)
- Remove `secondInstallmentDaysBefore` from `PaymentSettings` (the global setting is no longer needed for due date computation)
- The admin payment settings form should also be updated to remove the "days before" field

---

## Domain Model Changes

### `CampEdition` Entity (modify)

**File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add two new fields to the `CampEdition` class:

```csharp
// Payment deadlines for this edition
public DateTime? FirstPaymentDeadline { get; set; }   // Deadline for installment 1
public DateTime? SecondPaymentDeadline { get; set; }  // Deadline for installment 2
```

These are nullable to support existing editions that don't have them configured yet. When null, the system should compute defaults (117 / 75 days before `StartDate`).

### `PaymentSettingsJson` (modify)

**File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`

Remove `SecondInstallmentDaysBefore` from `PaymentSettingsJson`, `PaymentSettingsRequest`, and `PaymentSettingsResponse`. This field is no longer needed since deadlines are per-edition.

**Before**:
```csharp
public class PaymentSettingsJson
{
    public string Iban { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public int SecondInstallmentDaysBefore { get; set; } = 15;
    public string TransferConceptPrefix { get; set; } = "CAMP";
}
```

**After**:
```csharp
public class PaymentSettingsJson
{
    public string Iban { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string TransferConceptPrefix { get; set; } = "CAMP";
}
```

Same change for `PaymentSettingsResponse` and `PaymentSettingsRequest` records — remove `SecondInstallmentDaysBefore`.

### Database Migration

New migration to add columns to `camp_editions`:
- `first_payment_deadline` (timestamp nullable)
- `second_payment_deadline` (timestamp nullable)

No columns removed from `association_settings` — the existing JSON can keep the field for backward compatibility (it will simply be ignored).

---

## Backend Implementation

### 1. Update `CampEdition` Entity

**File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add fields as described above.

### 2. Update EF Core Configuration

**File**: `src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs`

Add column mappings:
```csharp
builder.Property(e => e.FirstPaymentDeadline)
    .HasColumnName("first_payment_deadline");
builder.Property(e => e.SecondPaymentDeadline)
    .HasColumnName("second_payment_deadline");
```

### 3. Update CampEdition DTOs

**File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add `FirstPaymentDeadline` and `SecondPaymentDeadline` to:
- `CampEditionResponse` record
- `UpdateCampEditionRequest` record (with defaults: null)
- `ActiveCampEditionResponse` record
- `CurrentCampEditionResponse` record

Also update the `AvailableCampEditionResponse` (used by the registration wizard) in `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — add the deadline fields so the frontend can display them.

### 4. Update CampEdition Service Mapping

**File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`

Ensure the new fields are mapped in all `MapToResponse` methods and accepted in `UpdateEdition`.

### 5. Update PaymentsService — CreateInstallmentsAsync

**File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`

Change `CreateInstallmentsAsync` to use edition deadlines instead of global settings:

```csharp
// Default constants (days before camp start)
private const int DefaultFirstPaymentDaysBefore = 117;  // ~April 20 for Aug 15 camp
private const int DefaultSecondPaymentDaysBefore = 75;   // ~June 1 for Aug 15 camp

// In CreateInstallmentsAsync:
var dueDate1 = registration.CampEdition.FirstPaymentDeadline
    ?? registration.CampEdition.StartDate.AddDays(-DefaultFirstPaymentDaysBefore);

var dueDate2 = registration.CampEdition.SecondPaymentDeadline
    ?? registration.CampEdition.StartDate.AddDays(-DefaultSecondPaymentDaysBefore);
```

The `LoadPaymentSettingsAsync` call can remain for IBAN/bank info and `TransferConceptPrefix`, but should no longer provide `SecondInstallmentDaysBefore`.

### 6. Update PaymentSettings DTOs and Validators

**File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`

- Remove `SecondInstallmentDaysBefore` from `PaymentSettingsResponse`, `PaymentSettingsRequest`, `PaymentSettingsJson`
- Remove `int SecondInstallmentDaysBefore` parameter from records

**File**: `src/Abuvi.API/Features/Payments/PaymentsValidators.cs`

- Remove the `SecondInstallmentDaysBefore` validation rule from `PaymentSettingsRequestValidator`

### 7. Update PaymentsService Settings Methods

**File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`

- Remove `settings.SecondInstallmentDaysBefore` from `GetPaymentSettingsAsync` response mapping
- Remove `SecondInstallmentDaysBefore` from `UpdatePaymentSettingsAsync`

### 8. Database Migration

Create migration: `dotnet ef migrations add AddPaymentDeadlinesToCampEdition`

---

## Frontend Implementation

### 1. Update TypeScript Types

**File**: `frontend/src/types/camp-edition.ts`

Add to `CampEdition` interface:
```typescript
firstPaymentDeadline: string | null   // ISO date string
secondPaymentDeadline: string | null  // ISO date string
```

**File**: `frontend/src/types/payment.ts`

Remove `secondInstallmentDaysBefore` from `PaymentSettings` interface.

### 2. Update Camp Edition Admin Form

The camp edition create/update form should include optional date pickers for:
- **First payment deadline** (`DatePicker`)
- **Second payment deadline** (`DatePicker`)

With helper text: "If not set, defaults to 117 days before camp start (1st) and 75 days before camp start (2nd)."

Files to check/update:
- The camp edition edit form component (wherever `UpdateCampEditionRequest` is used)
- Add two `DatePicker` fields for the new dates

### 3. Update Payment Settings Admin Form

**File**: `frontend/src/components/admin/PaymentSettingsForm.vue`

Remove the "Days before deadline for 2nd installment" (`InputNumber`) field and its associated state/validation. The form should only contain:
- IBAN
- Bank name
- Account holder
- Transfer concept prefix

### 4. Update Registration Wizard / Detail Page

No changes needed to the payment display components — they already display `dueDate` from `PaymentResponse` which will now be populated with the edition-specific dates.

### 5. Display Payment Deadlines in Registration Context

Optionally, the `BankTransferInstructions` component or the payment step could display the edition's payment deadline dates as informational context. This is already handled since `PaymentInstallmentCard` shows the `dueDate` from each `PaymentResponse`.

---

## API Endpoint Changes

| Endpoint | Change |
|----------|--------|
| `PUT /api/camps/editions/{id}` | Accept `firstPaymentDeadline` and `secondPaymentDeadline` in request body |
| `GET /api/camps/editions/{id}` | Return `firstPaymentDeadline` and `secondPaymentDeadline` in response |
| `GET /api/settings/payment` | Remove `secondInstallmentDaysBefore` from response |
| `PUT /api/settings/payment` | Remove `secondInstallmentDaysBefore` from request body |

---

## Testing Strategy

### Backend Unit Tests

**File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs`

Update existing tests and add new ones:

1. `CreateInstallmentsAsync_EditionHasDeadlines_UsesEditionDates` — When edition has both deadlines set, use them directly
2. `CreateInstallmentsAsync_EditionHasNoDeadlines_UsesDefaultCalculation` — When edition deadlines are null, compute from StartDate with 117/75 day defaults
3. `CreateInstallmentsAsync_EditionHasPartialDeadlines_UsesDefaultForMissing` — When only one deadline is set, use the set one and compute the missing one
4. Update existing `CreateInstallmentsAsync_ValidRegistration_SetsCorrectDueDates` to reflect new logic

**File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsValidatorTests.cs`

- Remove `PaymentSettingsRequestValidator` tests for `SecondInstallmentDaysBefore`

### Frontend Unit Tests

- Update `PaymentSettingsForm` tests (if they exist) to remove the days field
- Verify camp edition form tests include the new date picker fields

---

## Files to Create/Modify Summary

### New Files
- New EF Core migration file

### Modified Files (Backend)
- `src/Abuvi.API/Features/Camps/CampsModels.cs` — Add fields to entity + DTOs
- `src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs` — Column mappings
- `src/Abuvi.API/Features/Camps/CampEditionsService.cs` — Map new fields in responses and updates
- `src/Abuvi.API/Features/Payments/PaymentsService.cs` — Use edition deadlines instead of global setting
- `src/Abuvi.API/Features/Payments/PaymentsModels.cs` — Remove `SecondInstallmentDaysBefore` from settings DTOs
- `src/Abuvi.API/Features/Payments/PaymentsValidators.cs` — Remove days validation
- `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — Add deadline fields to `AvailableCampEditionResponse`
- `src/Abuvi.API/Migrations/AbuviDbContextModelSnapshot.cs` — Updated by migration
- `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs` — Update/add due date tests
- `src/Abuvi.Tests/Unit/Features/Payments/PaymentsValidatorTests.cs` — Remove days validation tests

### Modified Files (Frontend)
- `frontend/src/types/camp-edition.ts` — Add deadline fields to `CampEdition`
- `frontend/src/types/payment.ts` — Remove `secondInstallmentDaysBefore` from `PaymentSettings`
- `frontend/src/components/admin/PaymentSettingsForm.vue` — Remove days before field
- Camp edition edit form component — Add date pickers for deadlines

---

## Implementation Order

1. Add `FirstPaymentDeadline` / `SecondPaymentDeadline` to `CampEdition` entity
2. Update EF Core configuration and create migration
3. Update CampEdition DTOs (response + request records)
4. Update CampEditionsService mapping
5. Update `PaymentsService.CreateInstallmentsAsync` to use edition dates
6. Remove `SecondInstallmentDaysBefore` from PaymentSettings DTOs and validators
7. Update backend unit tests
8. Update frontend types (`camp-edition.ts`, `payment.ts`)
9. Update `PaymentSettingsForm.vue` — remove days field
10. Update camp edition admin form — add date pickers
11. Run all tests, verify no regressions

---

## Default Value Calculation

Reference values based on a camp starting **August 15**:
- **1st payment deadline: April 20** → 117 days before start
- **2nd payment deadline: June 1** → 75 days before start

These defaults (117 and 75 days) are used as fallback constants in `PaymentsService` when a `CampEdition` doesn't have explicit deadline dates configured.

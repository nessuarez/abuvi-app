# Backend Implementation Plan: Payment Deadline Inheritance

## Overview

Implement a deadline inheritance pattern where `AssociationSettings` stores day-offset defaults for all 3 payment installments, and `CampEdition` materializes concrete deadline dates at creation time. The admin can then override dates per edition. `PaymentsService` reads deadlines from `CampEdition` directly, with settings-based fallback only as a safety net for legacy data.

**Architecture**: Vertical Slice Architecture — changes span the `Payments` and `Camps` feature slices, with a new cross-slice dependency (`Camps → Payments settings`).

## Architecture Context

**Feature slices involved:**
- `src/Abuvi.API/Features/Payments/` — settings DTOs, validators, service (settings endpoints)
- `src/Abuvi.API/Features/Camps/` — `CampEditionsService` (propose + update logic)

**Files to modify:**
| File | Slice | Change |
|---|---|---|
| `Features/Payments/PaymentsModels.cs` | Payments | Add 2 new fields to settings DTOs |
| `Features/Payments/PaymentsService.cs` | Payments | Include new fields in get/update settings; simplify deadline resolution in `CreateInstallmentsAsync` |
| `Features/Payments/PaymentsValidators.cs` | Payments | Add validation rules for new settings fields |
| `Features/Camps/CampEditionsService.cs` | Camps | Inject `IAssociationSettingsRepository`; materialize deadlines in `ProposeAsync`; re-derive on `UpdateAsync` when `null` |
| `src/Abuvi.API/Program.cs` | Root | No change needed (repo already registered) |

**No files to create. No migrations needed.**

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: This work is on the current branch `feature/feat-3-payments-backend`. Verify we are on it and up to date.
- **Commands**:
  ```bash
  git branch --show-current   # Should be feature/feat-3-payments-backend
  git status                  # Should be clean
  ```

---

### Step 1: Add New Fields to Payment Settings DTOs

- **File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
- **Action**: Add `FirstInstallmentDaysBefore` and `ExtrasInstallmentDaysFromCampStart` to the 3 settings types.

**PaymentSettingsJson** (internal persistence DTO):
```csharp
public class PaymentSettingsJson
{
    public string Iban { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public int FirstInstallmentDaysBefore { get; set; } = 30;
    public int SecondInstallmentDaysBefore { get; set; } = 15;
    public int ExtrasInstallmentDaysFromCampStart { get; set; } = 0;
    public string TransferConceptPrefix { get; set; } = "CAMP";
}
```

**PaymentSettingsRequest** (admin input):
```csharp
public record PaymentSettingsRequest(
    string Iban,
    string BankName,
    string AccountHolder,
    int FirstInstallmentDaysBefore,
    int SecondInstallmentDaysBefore,
    int ExtrasInstallmentDaysFromCampStart,
    string TransferConceptPrefix
);
```

**PaymentSettingsResponse** (admin output):
```csharp
public record PaymentSettingsResponse(
    string Iban,
    string BankName,
    string AccountHolder,
    int FirstInstallmentDaysBefore,
    int SecondInstallmentDaysBefore,
    int ExtrasInstallmentDaysFromCampStart,
    string TransferConceptPrefix
);
```

- **Implementation Notes**:
  - Default for `FirstInstallmentDaysBefore` is `30` (30 days before camp start).
  - Default for `ExtrasInstallmentDaysFromCampStart` is `0` (same day as camp start). Negative = before, positive = after.
  - Existing JSON blobs without these fields will deserialize with the C# defaults (backward compatible).

---

### Step 2: Add Validation Rules for New Settings Fields

- **File**: `src/Abuvi.API/Features/Payments/PaymentsValidators.cs`
- **Action**: Add rules to `PaymentSettingsRequestValidator`.

**Rules to add:**
```csharp
RuleFor(x => x.FirstInstallmentDaysBefore)
    .InclusiveBetween(0, 365)
    .WithMessage("First installment days before must be between 0 and 365");

RuleFor(x => x.ExtrasInstallmentDaysFromCampStart)
    .InclusiveBetween(-90, 90)
    .WithMessage("Extras installment offset must be between -90 and 90 days");
```

- **Implementation Notes**:
  - `FirstInstallmentDaysBefore = 0` means the deadline equals `StartDate` itself.
  - `ExtrasInstallmentDaysFromCampStart` allows negative (before camp) and positive (after camp).

---

### Step 3: Update PaymentsService Settings Methods

- **File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- **Action**: Update `GetPaymentSettingsAsync` and `UpdatePaymentSettingsAsync` to include the 2 new fields.

**3a. `GetPaymentSettingsAsync`** — add new fields to the response mapping:
```csharp
return new PaymentSettingsResponse(
    settings.Iban, settings.BankName, settings.AccountHolder,
    settings.FirstInstallmentDaysBefore,
    settings.SecondInstallmentDaysBefore,
    settings.ExtrasInstallmentDaysFromCampStart,
    settings.TransferConceptPrefix);
```

**3b. `UpdatePaymentSettingsAsync`** — map new fields from request to JSON:
```csharp
var json = new PaymentSettingsJson
{
    Iban = request.Iban,
    BankName = request.BankName,
    AccountHolder = request.AccountHolder,
    FirstInstallmentDaysBefore = request.FirstInstallmentDaysBefore,
    SecondInstallmentDaysBefore = request.SecondInstallmentDaysBefore,
    ExtrasInstallmentDaysFromCampStart = request.ExtrasInstallmentDaysFromCampStart,
    TransferConceptPrefix = request.TransferConceptPrefix
};
```

And update the return statement to include the new fields.

**3c. Simplify deadline resolution in `CreateInstallmentsAsync`**:

Current code (lines ~39-41):
```csharp
var dueDate1 = registration.CampEdition.FirstPaymentDeadline ?? DateTime.UtcNow;
var dueDate2 = registration.CampEdition.SecondPaymentDeadline
    ?? registration.CampEdition.StartDate.AddDays(-settings.SecondInstallmentDaysBefore);
```

Updated code:
```csharp
var edition = registration.CampEdition;
var dueDate1 = edition.FirstPaymentDeadline
    ?? edition.StartDate.AddDays(-settings.FirstInstallmentDaysBefore);
var dueDate2 = edition.SecondPaymentDeadline
    ?? edition.StartDate.AddDays(-settings.SecondInstallmentDaysBefore);
```

**3d. Update `SyncExtrasInstallmentAsync`** deadline resolution:

Current code uses `registration.CampEdition.ExtrasPaymentDeadline ?? registration.CampEdition.StartDate`.

Updated code:
```csharp
var edition = registration.CampEdition;
var dueDate = edition.ExtrasPaymentDeadline
    ?? edition.StartDate.AddDays(settings.ExtrasInstallmentDaysFromCampStart);
```

- **Implementation Notes**:
  - The fallbacks are safety nets for legacy CampEditions that were created before this feature. For any newly created edition, the fields will be materialized.

---

### Step 4: Inject Settings Repository into CampEditionsService

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: Add `IAssociationSettingsRepository` as a constructor dependency.

**Current constructor:**
```csharp
public CampEditionsService(
    ICampEditionsRepository repository,
    ICampsRepository campsRepository)
```

**Updated constructor:**
```csharp
public CampEditionsService(
    ICampEditionsRepository repository,
    ICampsRepository campsRepository,
    IAssociationSettingsRepository settingsRepo)
```

Add a private field `_settingsRepo` and a private helper method to load payment settings:

```csharp
private const string PaymentSettingsKey = "payment_settings";

private async Task<PaymentSettingsJson> LoadPaymentSettingsAsync(CancellationToken ct)
{
    var setting = await _settingsRepo.GetByKeyAsync(PaymentSettingsKey, ct);
    if (setting is null) return new PaymentSettingsJson();

    try
    {
        return JsonSerializer.Deserialize<PaymentSettingsJson>(setting.SettingValue)
               ?? new PaymentSettingsJson();
    }
    catch (JsonException)
    {
        return new PaymentSettingsJson();
    }
}
```

- **Implementation Notes**:
  - `IAssociationSettingsRepository` is already registered in `Program.cs` as `AddScoped<IAssociationSettingsRepository, AssociationSettingsRepository>()`.
  - The `LoadPaymentSettingsAsync` helper duplicates logic from `PaymentsService`. This is acceptable in VSA — each slice owns its data access. Alternatively, a shared `PaymentSettingsLoader` could be extracted if this pattern repeats, but for now KISS.
  - Add `using System.Text.Json;` and `using Abuvi.API.Features.Payments;` (for `PaymentSettingsJson`) to the file if not already present.

---

### Step 5: Materialize Deadlines in `ProposeAsync`

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: After building the `CampEdition` entity and before `_repository.CreateAsync()`, populate the 3 deadline fields.

**Insert after line ~89 (before `edition.SetAccommodationCapacity`):**
```csharp
// Materialize payment deadlines from global settings
var paymentSettings = await LoadPaymentSettingsAsync(cancellationToken);
edition.FirstPaymentDeadline = edition.StartDate.AddDays(-paymentSettings.FirstInstallmentDaysBefore);
edition.SecondPaymentDeadline = edition.StartDate.AddDays(-paymentSettings.SecondInstallmentDaysBefore);
edition.ExtrasPaymentDeadline = edition.StartDate.AddDays(paymentSettings.ExtrasInstallmentDaysFromCampStart);
```

- **Implementation Notes**:
  - The deadlines are `DateTime` values derived from `StartDate` (which is a `DateTime`).
  - The admin can override any of these later via `UpdateAsync`.

---

### Step 6: Re-derive Deadlines on `UpdateAsync` When Null

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: In `UpdateAsync`, when a deadline field is `null` in the request, re-derive it from settings + the (possibly updated) `StartDate`.

**Current code (lines ~265-267):**
```csharp
edition.FirstPaymentDeadline = request.FirstPaymentDeadline;
edition.SecondPaymentDeadline = request.SecondPaymentDeadline;
edition.ExtrasPaymentDeadline = request.ExtrasPaymentDeadline;
```

**Updated code:**
```csharp
var paymentSettings = await LoadPaymentSettingsAsync(cancellationToken);
edition.FirstPaymentDeadline = request.FirstPaymentDeadline
    ?? edition.StartDate.AddDays(-paymentSettings.FirstInstallmentDaysBefore);
edition.SecondPaymentDeadline = request.SecondPaymentDeadline
    ?? edition.StartDate.AddDays(-paymentSettings.SecondInstallmentDaysBefore);
edition.ExtrasPaymentDeadline = request.ExtrasPaymentDeadline
    ?? edition.StartDate.AddDays(paymentSettings.ExtrasInstallmentDaysFromCampStart);
```

- **Implementation Notes**:
  - `edition.StartDate` is already set to `request.StartDate` on line ~242 before this code runs.
  - This means: if the frontend sends `null` for a deadline, the backend re-calculates it from the current settings and the new `StartDate`. If the frontend sends a specific date, that date is used.
  - This supports both "reset to default" (send `null`) and "custom override" (send a date) workflows.

---

### Step 7: Write Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs`
- **Action**: Update existing settings tests to include new fields.

**Tests to update:**
1. `GetPaymentSettings_ReturnsDefaults_WhenNoSettingsExist` — verify `FirstInstallmentDaysBefore = 30` and `ExtrasInstallmentDaysFromCampStart = 0` are in the response.
2. `UpdatePaymentSettings_SavesAndReturns` — include new fields in the request and verify they round-trip.

**Tests to add:**
3. `CreateInstallments_UsesFirstPaymentDeadlineFromEdition_WhenSet` — verify P1 due date comes from `CampEdition.FirstPaymentDeadline`.
4. `CreateInstallments_FallsBackToSettings_WhenFirstPaymentDeadlineIsNull` — verify P1 due date = `StartDate.AddDays(-FirstInstallmentDaysBefore)`.

- **File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsValidatorTests.cs`
- **Action**: Add validator tests.

**Tests to add:**
1. `PaymentSettings_FirstInstallmentDaysBefore_OutOfRange_Fails` — values < 0 or > 365 should fail.
2. `PaymentSettings_ExtrasInstallmentDaysFromCampStart_OutOfRange_Fails` — values < -90 or > 90 should fail.
3. `PaymentSettings_NewFieldsValid_Passes` — valid values pass.

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs` (create if not exists, or add to existing)
- **Action**: Add tests for deadline materialization.

**Tests to add:**
1. `ProposeAsync_MaterializesDeadlinesFromSettings` — verify all 3 deadlines are set from `StartDate` + settings offsets.
2. `ProposeAsync_UsesDefaultSettings_WhenNoSettingsExist` — verify defaults (30, 15, 0) are used.
3. `UpdateAsync_NullDeadline_RederivesFromSettings` — verify `null` in request triggers recalculation.
4. `UpdateAsync_ExplicitDeadline_UsesProvidedValue` — verify explicit date overrides settings.
5. `UpdateAsync_StartDateChange_NullDeadlines_RecalculatesFromNewStartDate` — verify deadlines update when `StartDate` changes and deadlines are `null`.

- **Implementation Notes**:
  - Follow existing test patterns: xUnit + FluentAssertions + NSubstitute.
  - Test naming: `MethodName_StateUnderTest_ExpectedBehavior`.
  - CampEditionsService tests will need to mock `IAssociationSettingsRepository` (new dependency).

---

### Step 8: Update Technical Documentation

- **Action**: Review and update documentation after implementation.
- **Implementation Steps**:
  1. Update `ai-specs/specs/data-model.md` — document the new `PaymentSettingsJson` fields and the deadline materialization behavior.
  2. Update `ai-specs/specs/api-spec.yml` — update `PaymentSettingsRequest`/`PaymentSettingsResponse` schemas with new fields.
  3. Verify the `payment-deadlines-inheritance.md` spec acceptance criteria match the implementation.

---

## Implementation Order

1. **Step 0**: Verify branch
2. **Step 1**: Add new fields to DTOs (`PaymentsModels.cs`)
3. **Step 2**: Add validation rules (`PaymentsValidators.cs`)
4. **Step 3**: Update `PaymentsService` (settings methods + deadline resolution)
5. **Step 4**: Inject settings repo into `CampEditionsService`
6. **Step 5**: Materialize deadlines in `ProposeAsync`
7. **Step 6**: Re-derive deadlines in `UpdateAsync`
8. **Step 7**: Write unit tests
9. **Step 8**: Update documentation

---

## Testing Checklist

- [ ] Existing `PaymentsServiceTests` pass (settings fields backward compatible)
- [ ] Existing `PaymentsValidatorTests` pass (existing rules unchanged)
- [ ] Existing `PaymentsService_SyncTests` pass (sync logic unaffected)
- [ ] New settings validator tests cover boundary values for both new fields
- [ ] `ProposeAsync` materializes all 3 deadlines from settings
- [ ] `UpdateAsync` re-derives deadlines when `null`; preserves explicit overrides
- [ ] `CreateInstallmentsAsync` uses CampEdition deadlines with settings fallback
- [ ] `SyncExtrasInstallmentAsync` uses `ExtrasPaymentDeadline` from CampEdition
- [ ] All tests pass: `dotnet test src/Abuvi.Tests/`

---

## Error Response Format

No new error responses. Existing `ApiResponse<T>` envelope applies. Validation errors for new fields return 400 with field-level messages.

---

## Dependencies

- No new NuGet packages required.
- No EF Core migrations required — all fields already exist.
- `CampEditionsService` gains dependency on `IAssociationSettingsRepository` (already registered in DI).
- `CampEditionsService` gains `using` on `Abuvi.API.Features.Payments` (for `PaymentSettingsJson`).

---

## Notes

- **Backward compatibility**: Existing `PaymentSettingsJson` blobs in the database that lack the new fields will deserialize with C# defaults (`FirstInstallmentDaysBefore = 30`, `ExtrasInstallmentDaysFromCampStart = 0`). No data migration needed.
- **Existing CampEditions**: Editions created before this feature will still have `null` deadlines. The fallback logic in `PaymentsService` handles this gracefully.
- **Cross-slice dependency**: `CampEditionsService` now depends on `IAssociationSettingsRepository` and knows about `PaymentSettingsJson`. This is acceptable because settings are a shared concern, and the alternative (a dedicated settings service) adds unnecessary abstraction for 2 consumers.
- **No frontend changes in this plan**: The frontend will need to be updated separately to send/display the new settings fields and handle deadline editing in the CampEdition form.

---

## Next Steps After Implementation

1. Frontend: Update payment settings admin form to include `FirstInstallmentDaysBefore` and `ExtrasInstallmentDaysFromCampStart`.
2. Frontend: Update CampEdition edit form to display/edit materialized deadlines.
3. Consider a one-time backfill script to materialize deadlines on existing CampEditions (optional — fallback logic handles it).

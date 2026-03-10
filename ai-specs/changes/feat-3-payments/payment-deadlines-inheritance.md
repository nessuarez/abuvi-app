# Spec: Payment Deadline Inheritance Pattern

## Context & Problem

Currently there is a disconnect between payment settings and CampEdition deadline fields:

1. **AssociationSettings** stores `SecondInstallmentDaysBefore` (days before camp start for P2).
2. **CampEdition** has 3 nullable deadline fields: `FirstPaymentDeadline`, `SecondPaymentDeadline`, `ExtrasPaymentDeadline`.
3. At creation (`ProposeAsync`), **deadlines are never populated** — they stay `null`.
4. The fallback logic lives in `PaymentsService.CreateInstallmentsAsync`, computing dates on the fly from settings when deadlines are `null`.

**Problems:**

- The admin cannot see the effective deadlines until payments are created.
- Fallback logic is scattered across `PaymentsService` instead of being centralized.
- Adding `ExtrasInstallmentDaysFromCampStart` (from the 3-payment spec) would add yet another setting with implicit fallback behavior.
- No single source of truth for "what are the deadlines for this edition?"

---

## Proposed Pattern: Defaults in Settings, Materialized on CampEdition

### 1. AssociationSettings (`PaymentSettingsJson`) — stores day-offset defaults

| Property | Type | Default | Semantics |
|---|---|---|---|
| `SecondInstallmentDaysBefore` | `int` | `15` | **Already exists.** `StartDate.AddDays(-value)` |
| `FirstInstallmentDaysBefore` | `int` | `30` | **New.** `StartDate.AddDays(-value)`. Deadline for the first installment. Families register before this date but must pay by it. |
| `ExtrasInstallmentDaysFromCampStart` | `int` | `0` | **New.** `StartDate.AddDays(value)`. Negative = before, positive = after. |

> `Iban`, `BankName`, `AccountHolder`, `TransferConceptPrefix` remain unchanged.

### 2. CampEdition — stores materialized dates (already exists, no schema change)

The existing nullable fields are reused:

- `FirstPaymentDeadline` (`DateTime?`)
- `SecondPaymentDeadline` (`DateTime?`)
- `ExtrasPaymentDeadline` (`DateTime?`)

**Change:** These fields are **populated at creation time** from settings + `StartDate`, instead of being left `null`.

### 3. Lifecycle

#### A. `ProposeAsync` (CampEdition creation)

After creating the edition entity and before saving:

```
settings = LoadPaymentSettingsAsync()

edition.FirstPaymentDeadline  = edition.StartDate.AddDays(-settings.FirstInstallmentDaysBefore)
edition.SecondPaymentDeadline = edition.StartDate.AddDays(-settings.SecondInstallmentDaysBefore)
edition.ExtrasPaymentDeadline = edition.StartDate.AddDays(settings.ExtrasInstallmentDaysFromCampStart)
```

All three deadlines are materialized. The admin can adjust any of them later per edition.

#### B. `UpdateAsync` (CampEdition edit)

- The admin can override `FirstPaymentDeadline`, `SecondPaymentDeadline` and `ExtrasPaymentDeadline` to any date.
- If `StartDate` changes and the admin hasn't explicitly set custom deadlines, the deadlines should be recalculated. **Simplest approach:** always let the frontend send the deadline values explicitly. The backend does **not** auto-recalculate on update — the admin is in control.
- If the request sends `null` for a deadline, re-derive it from settings + the new `StartDate`.

#### C. `PaymentsService` — simplified deadline resolution

After this change, `PaymentsService.CreateInstallmentsAsync` becomes:

```csharp
var edition = registration.CampEdition;
var settings = await LoadPaymentSettingsAsync(ct);

var dueDate1 = edition.FirstPaymentDeadline
    ?? edition.StartDate.AddDays(-settings.FirstInstallmentDaysBefore);  // safety fallback
var dueDate2 = edition.SecondPaymentDeadline
    ?? edition.StartDate.AddDays(-settings.SecondInstallmentDaysBefore);  // safety fallback
var dueDate3 = edition.ExtrasPaymentDeadline
    ?? edition.StartDate;  // safety fallback
```

The fallbacks remain as a safety net for old data, but for any newly created edition all three values will be materialized.

---

## Files to Modify

| File | Change |
|---|---|
| `PaymentsModels.cs` | Add `FirstInstallmentDaysBefore` and `ExtrasInstallmentDaysFromCampStart` to `PaymentSettingsJson`, `PaymentSettingsRequest`, `PaymentSettingsResponse` |
| `PaymentsService.cs` | Update `GetPaymentSettingsAsync` and `UpdatePaymentSettingsAsync` to include new fields |
| `PaymentsValidators.cs` | Add validation for new fields if needed |
| `CampEditionsService.cs` | In `ProposeAsync`: load payment settings, populate `SecondPaymentDeadline` and `ExtrasPaymentDeadline` from settings + `StartDate`. In `UpdateAsync`: if deadline is `null`, re-derive from settings + `StartDate`. |
| `ICampEditionsService.cs` | No signature changes expected (settings are loaded internally) |

**No migrations needed** — all fields already exist:

- `PaymentSettingsJson` is a JSON blob (schema-less).
- `CampEdition.FirstPaymentDeadline`, `SecondPaymentDeadline`, `ExtrasPaymentDeadline` already exist as nullable `DateTime` columns.

---

## Impact on `feat-three-payments-system.md`

The 3-payment spec's section "New Setting: `ExtrasInstallmentDaysFromCampStart`" is **absorbed by this spec**. The setting is still added to `PaymentSettingsJson`, but the due date for P3 is read from `CampEdition.ExtrasPaymentDeadline` (materialized), not computed at payment-creation time.

Update `SyncExtrasInstallmentAsync` pseudocode step 4:

```
// Before (from 3-payment spec):
dueDate = campStartDate.AddDays(ExtrasInstallmentDaysFromCampStart)

// After (with this spec):
dueDate = registration.CampEdition.ExtrasPaymentDeadline ?? campStartDate
```

---

## Acceptance Criteria

- [ ] `PaymentSettingsJson` includes `FirstInstallmentDaysBefore` (default `0`) and `ExtrasInstallmentDaysFromCampStart` (default `0`)
- [ ] Admin payment settings endpoint exposes and accepts both new fields
- [ ] `ProposeAsync` materializes all three deadlines (`FirstPaymentDeadline`, `SecondPaymentDeadline`, `ExtrasPaymentDeadline`) from settings + `StartDate`
- [ ] `UpdateAsync` re-derives deadlines from settings when `null` is sent for a deadline field
- [ ] `PaymentsService` reads deadlines from `CampEdition` fields, with settings-based fallback only as safety net
- [ ] Existing CampEditions with `null` deadlines continue to work (fallback logic preserved)
- [ ] All existing tests pass; new tests cover the materialization in `ProposeAsync`

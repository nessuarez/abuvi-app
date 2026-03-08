# Feature: Three-Installment Payment System (Base × 2 + Extras)

## Context & Motivation

The current system generates two payments when a registration is created, splitting the total amount evenly. However:

1. **Extras are added after registration** — families select extras (meals, equipment, activities) in separate steps, sometimes over several weeks or even at the last minute. Extras cannot be baked into the initial 2-way split.
2. **Members can also be added late** — a family member may join at the last moment, changing the base total after the first installment has already been paid.

The solution is a **3-installment system** where extras live in a dedicated third payment with a separate (and later) due date.

---

## Root Cause of the Current Bug

1. `CreateAsync` creates the registration with `ExtrasAmount = 0` and immediately calls `CreateInstallmentsAsync`, which splits `TotalAmount` (without extras) into 2 payments.
2. `SetExtrasAsync` (called later) correctly updates `registration.ExtrasAmount` / `TotalAmount` but **never updates the existing payment amounts**.

Result: `P1 + P2 < Registration.TotalAmount` whenever extras exist.

---

## New Payment Structure

| # | Name | Amount | Due Date |
| --- | --- | --- | --- |
| 1 | First installment | `ceil(BaseTotalAmount / 2)` | Immediately (registration date) |
| 2 | Second installment | `BaseTotalAmount - ceil(BaseTotalAmount / 2)` | `CampStartDate - SecondInstallmentDaysBefore` (existing setting) |
| 3 | Extras installment | `ExtrasAmount` | `CampStartDate + ExtrasInstallmentDaysFromCampStart` (**new setting**, default = `0`) |

**Payment 3 lifecycle:**

- **Created** the first time extras are set and `ExtrasAmount > 0`.
- **Updated** whenever extras change (amount and due date recalculated from settings).
- **Deleted** when all extras are removed (`ExtrasAmount == 0`).

**Transfer concept for P3:** `{prefix}-{year}-{familyName}-3`

**Invariant** that must hold at all times:

```
P1.Amount + P2.Amount + (P3?.Amount ?? 0) == Registration.TotalAmount
```

---

## New Setting: `ExtrasInstallmentDaysFromCampStart`

Add to `PaymentSettingsJson`, `PaymentSettingsRequest`, and `PaymentSettingsResponse`:

| Property | Type | Default | Semantics |
| --- | --- | --- | --- |
| `ExtrasInstallmentDaysFromCampStart` | `int` | `0` | `CampStartDate.AddDays(value)`. Negative = before camp; positive = after. |

Examples: `-5` → 5 days before camp starts; `7` → 7 days after camp starts.

---

## Payment Sync Rules on Registration Changes

### Case A: Extras change (`SetExtrasAsync`)

| P3 Status | Action |
| --- | --- |
| Does not exist, `ExtrasAmount > 0` | Create P3 |
| `Pending`, `ExtrasAmount > 0` | Update `P3.Amount`, recalculate due date |
| `Pending`, `ExtrasAmount == 0` | Delete P3 |
| `PendingReview` | Throw `BusinessRuleException` — cannot change extras while under review |
| `Completed` | Throw `BusinessRuleException` — extras payment already completed |

P1 and P2 are **never touched** when only extras change.

### Case B: Members change (`UpdateMembersAsync`)

| P1 Status | P2 Status | Action |
| --- | --- | --- |
| `Pending` | `Pending` | Recalculate P1 = `ceil(newBase/2)`, P2 = `newBase - P1` |
| `PendingReview` | any | Throw `BusinessRuleException` |
| `Completed` | `Pending` | Absorb delta into P2: `P2.Amount += (newBase - oldBase)`. Guard: P2.Amount must remain > 0 |
| `Completed` | `PendingReview` or `Completed` | Throw `BusinessRuleException` |

P3 is **never touched** when only members change.

### Case C: Admin edit (`AdminUpdateAsync`)

Apply the same rules as A and B above. The admin-only difference is that the `Pending` status guard on the registration is skipped (admins can edit `Draft` registrations too). Registration status is set to `Draft` after any edit.

---

## Registration Auto-Confirmation

Existing logic in `ConfirmPaymentAsync`: registration is confirmed when **all existing payments** are `Completed`. This already handles the 3-payment case correctly — if P3 exists, all three must be completed; if P3 was deleted (no extras), only P1+P2 suffice. **No change needed here.**

---

## Files to Modify

| File | Change |
| --- | --- |
| `src/Abuvi.API/Features/Payments/PaymentsModels.cs` | Add `ExtrasInstallmentDaysFromCampStart` to settings classes |
| `src/Abuvi.API/Features/Payments/IPaymentsService.cs` | Add `SyncExtrasInstallmentAsync` and `SyncBaseInstallmentsAsync` signatures |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Implement both new methods; include new setting in get/update settings |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Call sync methods at end of `SetExtrasAsync`, `UpdateMembersAsync`, and `AdminUpdateAsync` |
| `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs` | Add unit tests for both new methods (see below) |

Check `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs` for any `CHECK` constraint limiting `InstallmentNumber <= 2` — remove it if present (no new migration needed otherwise since the schema already supports `int`).

---

## Interface Signatures

```csharp
// IPaymentsService.cs — add:

/// Creates, updates, or deletes the extras installment (P3) for a registration.
/// Returns the P3 response, or null if P3 was deleted or never needed.
Task<PaymentResponse?> SyncExtrasInstallmentAsync(
    Guid registrationId, decimal extrasAmount, CancellationToken ct);

/// Recalculates P1 and/or P2 when the base member total changes.
Task SyncBaseInstallmentsAsync(
    Guid registrationId, decimal newBaseTotalAmount, decimal oldBaseTotalAmount, CancellationToken ct);
```

---

## `SyncExtrasInstallmentAsync` Pseudocode

```
1. Load registration with details (registrationsRepo.GetByIdWithDetailsAsync) for campStartDate + family name
2. Load all payments for registration (paymentsRepo.GetByRegistrationIdAsync)
3. Find P3 = payment with InstallmentNumber == 3
4. Load settings → dueDate = campStartDate.AddDays(ExtrasInstallmentDaysFromCampStart)
5. if extrasAmount > 0:
     if P3 exists:
       guard: P3.Status is PendingReview or Completed → throw BusinessRuleException
       P3.Amount = extrasAmount; P3.DueDate = dueDate; P3.UpdatedAt = now
       paymentsRepo.UpdateAsync(P3)
     else:
       concept = "{prefix}-{year}-{familyName}-3" (truncate to 100)
       create P3: InstallmentNumber=3, Amount=extrasAmount, Status=Pending, DueDate=dueDate, concept
       paymentsRepo.AddAsync(P3)
6. if extrasAmount == 0 and P3 exists:
     guard: P3.Status is PendingReview or Completed → throw BusinessRuleException
     paymentsRepo.DeleteAsync(P3.Id)
     return null
7. return MapToResponse(P3)
```

---

## `SyncBaseInstallmentsAsync` Pseudocode

```
1. Load all payments for registration
2. Find P1 (InstallmentNumber == 1), P2 (InstallmentNumber == 2)
3. if P1.Status is PendingReview → throw BusinessRuleException
4. if P1.Status is Pending:
     newP1 = ceil(newBaseTotalAmount / 2)
     newP2 = newBaseTotalAmount - newP1
     P1.Amount = newP1; P2.Amount = newP2
     paymentsRepo.UpdateAsync(P1); paymentsRepo.UpdateAsync(P2)
5. if P1.Status is Completed:
     guard: P2.Status is PendingReview or Completed → throw BusinessRuleException
     delta = newBaseTotalAmount - oldBaseTotalAmount
     guard: P2.Amount + delta > 0 → else throw BusinessRuleException
     P2.Amount += delta
     paymentsRepo.UpdateAsync(P2)
```

---

## Unit Tests to Add

### `SyncExtrasInstallmentAsync`

1. `SyncExtras_NoP3Exists_PositiveExtras_CreatesP3WithCorrectAmount`
2. `SyncExtras_P3ExistsPending_UpdatesAmount`
3. `SyncExtras_P3ExistsPending_ZeroExtras_DeletesP3`
4. `SyncExtras_P3PendingReview_ThrowsBusinessRuleException`
5. `SyncExtras_P3Completed_ThrowsBusinessRuleException`
6. `SyncExtras_DueDateReflectsSettingOffset`

### `SyncBaseInstallmentsAsync`

1. `SyncBase_BothPending_RecalculatesBoth`
2. `SyncBase_BothPending_OddAmount_RoundsP1Up`
3. `SyncBase_P1Completed_P2Pending_AbsorbsDelta`
4. `SyncBase_P1Completed_P2Pending_NegativeDelta_P2DecreasesCorrectly`
5. `SyncBase_P1Completed_P2Pending_DeltaWouldMakeP2NonPositive_Throws`
6. `SyncBase_P1PendingReview_Throws`
7. `SyncBase_P1Completed_P2PendingReview_Throws`

---

## Acceptance Criteria

- [ ] When extras are first set on a pending registration, P3 is created with correct amount and due date
- [ ] When extras are updated, P3 amount and due date are updated
- [ ] When all extras are removed, P3 is deleted
- [ ] P1 and P2 amounts are not affected when only extras change
- [ ] Transfer concept for P3 follows the format `{prefix}-{year}-{familyName}-3`
- [ ] When members are updated and P1 is still `Pending`, both P1 and P2 are recalculated correctly
- [ ] When members are updated and P1 is `Completed`, delta is absorbed into P2
- [ ] Attempting to change extras when P3 is `PendingReview` or `Completed` throws a user-friendly error
- [ ] Attempting to change members when P1 is `PendingReview` throws a user-friendly error
- [ ] `P1 + P2 + P3 == Registration.TotalAmount` holds after every mutation
- [ ] `ExtrasInstallmentDaysFromCampStart` is configurable via the admin payment settings endpoint
- [ ] Registration is auto-confirmed only when all existing payments (including P3 if present) are `Completed`
- [ ] All new unit tests pass; all existing tests continue to pass

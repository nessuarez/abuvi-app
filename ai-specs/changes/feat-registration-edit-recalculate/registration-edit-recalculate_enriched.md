# User Story: Edición completa de inscripción y recálculo de pagos

## Summary

As a **family representative**, I want to **edit my registration (members and extras) after creation** so that I can **fix mistakes or accommodate last-minute changes** without losing my spot.

As an **admin/board member**, I want to **add surcharges or penalty extras** to a registration so that the **payment installments automatically reflect the updated total**.

## Context & Motivation

The current registration flow has three operational gaps:

**Gap 1 — Payments not updated on edit**: When a family updates members or extras (both already supported), `TotalAmount` is recalculated but the auto-generated `Payment` records keep their original amounts. The installments become desynchronized with the actual total.

**Gap 2 — Edit is blocked once a proof is uploaded**: A family could change the total while a payment is being reviewed, causing inconsistencies.

**Gap 3 — No payment breakdown visible**: Families and the board have no way to see which members and extras make up each installment amount inside the app.

**Context on payment creation**: Payments are created **immediately when the registration is created** (`CreateAsync`). The architecture uses **3 installments**:

- **P1 and P2**: Cover the base member total (50/50 split, ceiling on P1).
- **P3**: Covers extras only. Created when extras > 0, deleted when extras = 0.

Editing is permitted while `Status == Pending` (or `Draft`) and no installment has a proof uploaded.

---

## Registration Status Flow

```
Create registration
  → Status: Pending
  → P1 and P2 created (50/50 of BaseTotalAmount)
  → P3 created if ExtrasAmount > 0

Family edits members
  → Allowed while: (Status == Pending OR Draft) AND no Payment has ProofFileUrl != null
  → BaseTotalAmount recalculated
  → P1 and P2 redistributed via SyncBaseInstallmentsAsync (delta-based)
  → Status: stays Pending/Draft

Family edits extras
  → Allowed while: (Status == Pending OR Draft) AND no Payment has ProofFileUrl != null
  → ExtrasAmount recalculated
  → P3 created/updated/deleted via SyncExtrasInstallmentAsync
  → Status: stays Pending/Draft

Family uploads proof for P1
  → P1 Status: PendingReview
  → Editing is now blocked (proof exists)

Admin confirms P1
  → P1 Status: Completed

Family uploads proof for P2
  → P2 Status: PendingReview

Admin confirms P2
  → P2 Status: Completed

(If P3 exists) Family uploads proof for P3
  → P3 Status: PendingReview

Admin confirms P3
  → P3 Status: Completed
  → Registration Status: Confirmed (when all installments completed)

Admin adds surcharge extra (AdminUpdateAsync)
  → ExtrasAmount recalculated → P3 created/updated/deleted
  → MembersAmount recalculated (if members also sent) → P1/P2 synced
  → Status: Draft (signals family that admin changed something)
  → ⚠️ No proof guard currently — admin can edit even with proofs uploaded
```

---

## Functional Requirements

### Business Rules

| # | Rule | Status | Details |
|---|------|--------|---------|
| BR-1 | **Edit window** | ✅ Done | Family can edit while `Status == Pending OR Draft` AND no `Payment` has `ProofFileUrl != null`. Both guards are in `UpdateMembersAsync` (line 228) and `SetExtrasAsync` (line 359). |
| BR-2 | **Auto-sync base installments** | ✅ Done | `SyncBaseInstallmentsAsync(registrationId, newBase, oldBase, ct)` called after `UpdateMembersAsync` and `AdminUpdateAsync`. Uses delta: adds/subtracts difference from P2 (if P1 Completed) or re-splits P1/P2 (if both pending). |
| BR-3 | **Auto-sync extras installment** | ✅ Done | `SyncExtrasInstallmentAsync(registrationId, extrasAmount, ct)` called after `SetExtrasAsync` and `AdminUpdateAsync`. Creates P3 if new>0, updates P3 if exists+pending, deletes P3 if new=0, throws if P3 is PendingReview or Completed. |
| BR-4 | **Transfer concept update** | ✅ Done | Handled inside the sync methods when updating payment amounts. |
| BR-5 | **Payment breakdown in app** | ✅ Done | `RegistrationPricingBreakdown.vue` shows members table + extras table + total. Installment cards shown in the Payments section of `RegistrationDetailPage.vue`. |
| BR-6 | **Draft → family editable** | ✅ Done | Status check in `UpdateMembersAsync` (line 224) and `SetExtrasAsync` (line 355) allows `Pending OR Draft`. |
| BR-7 | **Admin edit proof guard** | ❌ Missing | `AdminUpdateAsync` has no guard for `ProofFileUrl != null`. Intentional or oversight — see OD-1. |

### Edge Cases

- If any payment has `ProofFileUrl != null`, family edit returns 422: guard message varies by context ("No se pueden modificar los miembros / extras porque ya hay un justificante de pago subido.").
- If P1 is `Completed` and P2 is `Pending`: `SyncBaseInstallmentsAsync` applies the delta to P2 only.
- If the delta would make P2 non-positive (e.g. total reduced below what P1 already covered): `SyncBaseInstallmentsAsync` throws `BusinessRuleException`.
- If P1 is `PendingReview`, or P2 is `PendingReview`: `SyncBaseInstallmentsAsync` throws `BusinessRuleException`.
- If P3 is `PendingReview` or `Completed`: `SyncExtrasInstallmentAsync` throws `BusinessRuleException`.

---

## Technical Specification

### Architecture: 3-Installment Model

The implementation uses **3 installments** instead of the 2 described in the original spec:

| Installment | Covers | Created when | Deleted when |
|---|---|---|---|
| P1 (InstallmentNumber=1) | Base member total (50%, ceiling) | Registration created | Never |
| P2 (InstallmentNumber=2) | Base member total (50%) | Registration created | Never |
| P3 (InstallmentNumber=3) | Extras total | ExtrasAmount > 0 | ExtrasAmount set to 0 |

### Implemented Methods

#### `SyncBaseInstallmentsAsync` (PaymentsService)

```csharp
Task SyncBaseInstallmentsAsync(Guid registrationId, decimal newBaseTotalAmount, decimal oldBaseTotalAmount, CancellationToken ct);
```

- Computes `delta = newBaseTotalAmount - oldBaseTotalAmount`
- If both P1/P2 are `Pending`: re-splits as 50/50 (ceiling on P1)
- If P1 is `Completed` and P2 is `Pending`: applies delta to P2 only
- Throws if any installment is `PendingReview` or delta would make P2 ≤ 0
- Updates `TransferConcept` on changed payments

#### `SyncExtrasInstallmentAsync` (PaymentsService)

```csharp
Task<PaymentResponse?> SyncExtrasInstallmentAsync(Guid registrationId, decimal extrasAmount, CancellationToken ct);
```

- If `extrasAmount > 0` and no P3 exists: creates P3 with `DueDate` from `CampEdition.ExtrasPaymentDeadline`
- If `extrasAmount > 0` and P3 exists and `Pending`: updates P3 amount
- If `extrasAmount == 0` and P3 exists and `Pending`: deletes P3
- If P3 is `PendingReview` or `Completed`: throws `BusinessRuleException`

### Remaining Work

#### 1. Proof guard in `AdminUpdateAsync` ❌

Currently `AdminUpdateAsync` (`RegistrationsService.cs:746`) has no proof guard. Admins can modify a registration even if a payment proof has been uploaded. Decision needed (OD-1 below) before implementing.

If the guard should be added, insert after the cancelled-status check:

```csharp
if (registration.Payments?.Any(p => p.ProofFileUrl != null) == true)
    throw new BusinessRuleException(
        "No se puede modificar la inscripción porque ya hay justificantes de pago subidos.");
```

#### 2. Missing unit tests ❌

The following tests are not yet written:

**`RegistrationsServiceTests.cs` or new `RegistrationsService_EditGuard_Tests.cs`:**

| Test | Expected |
|------|----------|
| `UpdateMembersAsync_WhenProofUploaded_ThrowsBusinessRuleException` | Throws when any payment has `ProofFileUrl != null` |
| `UpdateMembersAsync_WhenNullProof_CallsSyncBaseInstallments` | `paymentsService.SyncBaseInstallmentsAsync` called once |
| `SetExtrasAsync_WhenProofUploaded_ThrowsBusinessRuleException` | Throws when any payment has `ProofFileUrl != null` |
| `SetExtrasAsync_WhenNullProof_CallsSyncExtrasInstallment` | `paymentsService.SyncExtrasInstallmentAsync` called once |

### Files to Modify (Remaining)

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Add proof guard to `AdminUpdateAsync` (after OD-1 resolved) |
| `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs` (or new file) | Add 4 missing unit tests listed above |

### Already Implemented — No Action Needed

| File | What was done |
|------|--------------|
| `src/Abuvi.API/Features/Payments/IPaymentsService.cs` | `SyncBaseInstallmentsAsync` + `SyncExtrasInstallmentAsync` declared |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Both methods fully implemented |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Proof guard + sync call in `UpdateMembersAsync` and `SetExtrasAsync`; sync calls in `AdminUpdateAsync` |
| `src/Abuvi.Tests/Unit/Features/Payments/PaymentsService_SyncTests.cs` | Full test coverage for both sync methods |
| `frontend/src/components/registrations/RegistrationPricingBreakdown.vue` | Members + extras breakdown with totals |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Installment cards + paid/remaining summary |

---

## Open Decisions

| # | Question | Current state |
|---|----------|---------------|
| OD-1 | **Admin edit proof guard** | Admins currently bypass the proof guard. Is this intentional? Options: (a) add same guard as family edit — admin cannot edit if any proof exists; (b) leave as-is — admin has override authority and is trusted to handle manually. |

---

## Acceptance Criteria

- [x] Family cannot edit members or extras if any payment has a proof uploaded
- [x] When members are updated, P1/P2 installment amounts are automatically recalculated (delta-based)
- [x] When extras are updated, P3 installment is created, updated, or deleted accordingly
- [x] When admin edits members or extras, the same sync methods are called
- [x] A registration in Draft status is still editable by the family (if no proof uploaded)
- [x] If P1 is already Completed, only P2 is adjusted when members change
- [x] Transfer concepts are regenerated when amounts change
- [x] Payment breakdown (members table + extras table + installment cards) is visible on the registration detail page
- [x] `PaymentsService_SyncTests.cs` covers both sync methods comprehensively
- [ ] Admin cannot edit a registration if a payment proof has been uploaded (pending OD-1 decision)
- [ ] Unit tests cover the proof guard and sync-call scenarios in `UpdateMembersAsync` and `SetExtrasAsync`

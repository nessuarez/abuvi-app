# User Story: Edición completa de inscripción y recálculo de pagos

## Summary

As a **family representative**, I want to **edit my registration (members and extras) after creation** so that I can **fix mistakes or accommodate last-minute changes** without losing my spot.

As an **admin/board member**, I want to **add surcharges or penalty extras** to a registration so that the **payment installments automatically reflect the updated total**.

## Context & Motivation

The current registration flow has three operational gaps:

**Gap 1 — Payments not updated on edit**: When a family updates members or extras (both already supported), `TotalAmount` is recalculated but the two auto-generated `Payment` records keep their original amounts. The installments become desynchronized with the actual total.

**Gap 2 — Edit is blocked once a proof is uploaded**: Today there is no guard preventing edits after a proof has been uploaded. This means a family could change the total while a payment is being reviewed.

**Gap 3 — No payment breakdown visible**: The bank transfer concept is `PREFIX-FAMILYNAME-1` / `PREFIX-FAMILYNAME-2`. Families and the board have no way to see which members and extras make up each installment amount inside the app.

**Context on payment creation**: Payments (2 installments, 50/50 split) are created **immediately when the registration is created** (`CreateAsync`), not on confirmation. Editing is permitted while `Status == Pending` and no installment has a proof uploaded (`ProofFileUrl == null`).

---

## Registration Status Flow (clarified)

```
Create registration
  → Status: Pending
  → Payment 1 and Payment 2 created (50/50 of initial TotalAmount)

Family edits members and/or extras
  → Allowed while: Status == Pending AND no Payment has ProofFileUrl != null
  → TotalAmount recalculated
  → Payment amounts redistributed automatically
  → Status: stays Pending

Family uploads proof for installment 1
  → Payment 1 Status: PendingReview
  → Editing is now blocked (proof exists)

Admin confirms Payment 1
  → Payment 1 Status: Completed

Family uploads proof for installment 2
  → Payment 2 Status: PendingReview

Admin confirms Payment 2
  → Payment 2 Status: Completed
  → Registration Status: Confirmed (automatic)

Admin adds surcharge extra
  → TotalAmount recalculated
  → Clean installments (no proof, not Completed) updated
  → Status: Draft (signals family that admin changed something)
```

---

## Functional Requirements

### Business Rules

| # | Rule | Details |
|---|------|---------|
| BR-1 | **Edit window** | Family can edit members and extras only while `Status == Pending` AND no `Payment` has `ProofFileUrl != null`. |
| BR-2 | **Auto-recalculate installments** | Any time `TotalAmount` changes (family edit or admin surcharge), all `Payment` records with `ProofFileUrl == null` and `Status != Completed` are redistributed to cover the remaining balance. |
| BR-3 | **Remaining balance redistribution** | `remaining = TotalAmount - sum(Completed payments)`. If only one installment is clean, it absorbs the full remaining. If both are clean, split 50/50 (ceiling on installment 1). |
| BR-4 | **Transfer concept update** | When `Payment.Amount` changes, `Payment.TransferConcept` is regenerated with the same `PREFIX-FAMILYNAME-N` format (transfer concept prefix from settings + normalized family name + installment number). |
| BR-5 | **Payment breakdown in app** | The registration detail view shows a breakdown: member list with individual amounts and period, extras with quantity and amount, and which installment covers which portion. This is computed dynamically — no additional DB snapshot needed. |
| BR-6 | **Draft → editing** | A registration in `Draft` status (set by `AdminUpdateAsync`) can still be edited by the family if no proof has been uploaded. |

### Edge Cases

- If all payments are `Completed` or have proofs, return 422: "No se puede modificar la inscripción porque ya hay pagos en proceso o confirmados."
- If `Payment 1` is `Completed` and `Payment 2` has no proof: only `Payment 2` is updated with the full remaining balance.
- If admin adds a surcharge extra that increases the total but `Payment 1` proof is already uploaded (PendingReview): only `Payment 2` is updated (Payment 1 has `ProofFileUrl != null` so it is excluded from redistribution).

---

## Technical Specification

### Backend

#### 1. Guard on `UpdateMembersAsync` and `SetExtrasAsync`

Add to both methods after authorization check:

```csharp
// Block edit if any payment has a proof uploaded
var payments = await paymentsRepo.GetByRegistrationIdAsync(registrationId, ct);
if (payments.Any(p => p.ProofFileUrl != null))
    throw new BusinessRuleException(
        "No se puede modificar la inscripción porque ya hay pagos en proceso o confirmados.");
```

Note: Both methods already require `Status == Pending`. `Draft` registrations (set by admin) can be edited by the family if no proofs exist — the guard above is sufficient, no status check change needed for Draft.

#### 2. New service method: `RecalculateInstallmentsAsync`

Add to `IPaymentsService` and `PaymentsService`:

```csharp
Task RecalculateInstallmentsAsync(Guid registrationId, CancellationToken ct);
```

**Logic:**

```csharp
public async Task RecalculateInstallmentsAsync(Guid registrationId, CancellationToken ct)
{
    var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    var settings = await LoadPaymentSettingsAsync(ct);
    var familyName = NormalizeName(registration.FamilyUnit.Name);
    var prefix = settings.TransferConceptPrefix;

    var payments = await paymentsRepo.GetByRegistrationIdAsync(registrationId, ct);
    var cleanPayments = payments
        .Where(p => p.ProofFileUrl == null && p.Status != PaymentStatus.Completed)
        .OrderBy(p => p.InstallmentNumber)
        .ToList();

    if (!cleanPayments.Any()) return; // all paid or in review — nothing to recalculate

    var amountPaid = payments
        .Where(p => p.Status == PaymentStatus.Completed)
        .Sum(p => p.Amount);

    var remaining = registration.TotalAmount - amountPaid;

    if (cleanPayments.Count == 1)
    {
        var p = cleanPayments[0];
        p.Amount = remaining;
        p.TransferConcept = BuildConcept(prefix, familyName, p.InstallmentNumber);
        await paymentsRepo.UpdateAsync(p, ct);
    }
    else // 2 clean payments
    {
        var first = cleanPayments[0];
        var second = cleanPayments[1];
        first.Amount = Math.Ceiling(remaining / 2m);
        second.Amount = remaining - first.Amount;
        first.TransferConcept = BuildConcept(prefix, familyName, first.InstallmentNumber);
        second.TransferConcept = BuildConcept(prefix, familyName, second.InstallmentNumber);
        await paymentsRepo.UpdateAsync(first, ct);
        await paymentsRepo.UpdateAsync(second, ct);
    }

    logger.LogInformation(
        "Recalculated installments for registration {RegistrationId}. New total: {Total}",
        registrationId, registration.TotalAmount);
}

private static string BuildConcept(string prefix, string familyName, int installmentNumber)
{
    var concept = $"{prefix}-{familyName}-{installmentNumber}";
    return concept.Length > 100 ? concept[..100] : concept;
}
```

#### 3. Call `RecalculateInstallmentsAsync` from edit methods

At the end of `UpdateMembersAsync`, `SetExtrasAsync`, and `AdminUpdateAsync` (after `SaveChangesAsync`):

```csharp
await paymentsService.RecalculateInstallmentsAsync(registrationId, ct);
```

#### 4. New repository method: `UpdateAsync` for multiple payments (if not already batch)

Check if `IPaymentsRepository.UpdateAsync` accepts a single Payment — if so, two calls are fine. No new method needed unless batch update is preferred.

### Files to Modify (Backend)

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Payments/IPaymentsService.cs` | Add `RecalculateInstallmentsAsync` |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Implement `RecalculateInstallmentsAsync`; extract `BuildConcept` helper |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Add proof guard to `UpdateMembersAsync` and `SetExtrasAsync`; call `RecalculateInstallmentsAsync` from all three edit methods |

### Frontend

#### Payment breakdown section (Feature 3)

Add to `RegistrationDetailPage.vue` a computed section showing:

```
Desglose del importe:
  Miembros:
    - Ana García (Completo)   €500
    - Pedro García (1ª semana) €280
  Extras:
    - Seguro de accidentes (×2) €30
  ─────────────────────────────
  Total:                      €810

  Plazo 1 (vence: dd/mm/yyyy): €405
  Plazo 2 (vence: dd/mm/yyyy): €405
```

All data comes from the existing `RegistrationResponse` which already includes members, extras, and payments. No new API call needed.

### Files to Modify (Frontend)

| File | Change |
|------|--------|
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Add payment breakdown computed section |

---

## Unit Tests

### `PaymentsService_RecalculateInstallmentsAsync_Tests.cs`

| Test | Expected |
|------|----------|
| `Should_redistribute_both_installments_when_both_clean` | Payment 1 = ceil(total/2), Payment 2 = remainder |
| `Should_update_only_clean_installment_when_one_completed` | Only the clean payment updated with full remaining |
| `Should_do_nothing_when_all_payments_have_proofs` | No DB updates called |
| `Should_update_transfer_concepts_on_recalculation` | `TransferConcept` updated with new format |

### `RegistrationsService_UpdateMembersAsync_Tests.cs` (additions)

| Test | Expected |
|------|----------|
| `Should_block_edit_when_proof_uploaded` | `BusinessRuleException` |
| `Should_call_recalculate_after_successful_member_update` | `paymentsService.RecalculateInstallmentsAsync` called once |

### `RegistrationsService_SetExtrasAsync_Tests.cs` (additions)

| Test | Expected |
|------|----------|
| `Should_block_edit_when_proof_uploaded` | `BusinessRuleException` |
| `Should_call_recalculate_after_successful_extras_update` | `paymentsService.RecalculateInstallmentsAsync` called once |

---

## Open Decisions (to resolve before implementing)

| # | Question | Options |
|---|----------|---------|
| OD-1 | Draft status and family edit | Can family edit while Draft (if no proof)? Current plan: yes, the proof guard is sufficient. |
| OD-2 | Edit after Payment 1 Completed | Allow editing (only Payment 2 recalculates)? Or block all edits once any payment is Completed? |
| OD-3 | Admin surcharge scope | Should `AdminUpdateAsync` continue to allow editing members, or only extras? |

---

## Acceptance Criteria

- [ ] Family cannot edit members or extras if any payment has a proof uploaded
- [ ] When members are updated, installment amounts are automatically recalculated
- [ ] When extras are updated, installment amounts are automatically recalculated
- [ ] When admin adds a surcharge extra, installment amounts are automatically recalculated
- [ ] If one installment is already Completed, only the remaining clean installments are adjusted
- [ ] Transfer concepts are regenerated when amounts change
- [ ] Payment breakdown (members + extras + installment split) is visible on the registration detail page
- [ ] All unit tests pass

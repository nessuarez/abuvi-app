# Payment Adjustments & Admin Registration Management — Enriched User Story

## Summary

Four interrelated capabilities to allow the board (Junta) to handle real-world payment edge cases and correct registration data after submission:

1. **Edit any payment** — Admin can change the amount and concept of auto-generated payments (P1, P2, P3), not just manual ones.
2. **Recalculate pending payments** — After editing a completed or pending payment, the system recalculates the remaining pending payments to reflect the new total owed.
3. **Combined installment handling** — Admin can confirm multiple payments (e.g., P1 + P2 paid together in a single transfer) in one action.
4. **Remove members from a registration + refund** — Admin removes a non-member person from the registration, the system recalculates payment totals and generates a refund payment for the already-paid portion attributed to that person.

> **Note**: The admin registration editing workflow (add/remove members, change extras) + family representative acknowledgment is **already implemented** via the `Draft` status, `HasPendingUserAcknowledgement`, and `DraftTargetStatus` on `Registration`. What is missing is the payment-level correction layer on top of it.

---

## Problem

### Real-world scenarios requiring payment adjustments

**Scenario A — Combined P1 + P2 payment**
A family sends a single bank transfer covering both first and second installments. The current system has P1 and P2 as separate `Payment` records. The admin cannot confirm both from a single action — they must confirm P1, wait for sequential ordering to unlock P2, then confirm P2 separately. There is no way to record that a single transfer covered both, and no way to split the amount across both.

**Scenario B — Non-member included in registration**
A family registered a person (e.g., a sibling) who does not have an active `Membership`. The Junta reviews the list and decides to remove that person. The registration total needs to decrease, and if P1 (or P2) has already been `Completed`, the family must receive a partial refund for that person's portion.

**Scenario C — Payment amount or concept needs correction**
A data entry error or pricing discrepancy means the recorded amount for P1/P2 does not match what the family actually owes or paid. Currently, only `IsManual = true` payments can be updated via `PUT /api/admin/payments/{id}/manual`. Auto-generated payments (P1, P2, P3) cannot be edited.

**Scenario D — Recalculation chain after edit**
After correcting P1 (e.g., the family actually paid more than P1 alone), P2 should be adjusted to reflect the remaining balance. Currently there is no mechanism to propagate a payment amount change to downstream pending payments.

---

## Use Cases & Business Rules

### UC1 — Edit any payment amount and concept (admin)

**Who**: Board/Admin role
**When**: Any payment, regardless of `IsManual` flag, can be edited if its status is `Pending` or `Completed`.
**What changes**:

- `Amount`: New amount (must be > 0, max 2 decimal places)
- `ConceptLinesSerialized`: Free-text concept override (replaces existing concept lines with a `ManualPaymentConceptLine`)
- `AdminNotes`: Optional internal note explaining the change

**Business rules**:

- If the payment is `Pending`, change the amount directly. No recalculation triggered (the payment has not yet been confirmed, so it represents the new expected amount).
- If the payment is `Completed`, changing the amount triggers recalculation of subsequent pending auto-generated payments (see UC2).
- Cannot edit a payment in `Failed` or `Refunded` status.
- Cannot edit `Amount` to be <= 0.
- Editing a `Completed` payment records a `ConceptOverride` flag on the payment to indicate it was manually adjusted after confirmation.

### UC2 — Recalculate pending payments after editing a completed payment

**Trigger**: Admin edits the `Amount` of a `Completed` auto-generated payment (P1 or P2).
**Recalculation logic**:

- `TotalOwed = Registration.BaseTotalAmount + Registration.ExtrasAmount` (current value, unchanged by this action)
- `AlreadyPaid = sum of Completed payments (excluding Refunded and Manual)`
- `Remaining = TotalOwed - AlreadyPaid`
- Distribute `Remaining` across pending auto-generated installments (P2 if P1 was edited, P3 if P2 was edited), preserving the existing proportions or reassigning fully to the next pending installment.
- If `Remaining <= 0` (overpaid), do not reduce pending payments below 0. Instead, create a refund payment (see UC4) for the surplus amount.
- Manual payments (IsManual = true) are never recalculated.

**Example**:

```
TotalOwed: 1000€
P1 was 500€ (Completed) → admin corrects to 600€ → remaining = 400€
P2 was 500€ (Pending) → updated to 400€
```

### UC3 — Confirm multiple payments from a single transfer (combined installments)

**Who**: Board/Admin role
**When**: A family pays P1 and P2 in a single bank transfer. The transfer amount covers both.
**Endpoint**: `POST /api/admin/registrations/{registrationId}/payments/confirm-combined`

**Request**:

```csharp
public record ConfirmCombinedPaymentsRequest
{
    public List<Guid> PaymentIds { get; init; }  // e.g., [p1Id, p2Id]
    public decimal TotalReceivedAmount { get; init; }  // actual transfer amount
    public string? AdminNotes { get; init; }
}
```

**Behavior**:

1. Validate all payment IDs belong to the same registration.
2. Validate all payments are auto-generated (not Manual), and in `Pending` or `PendingReview` status.
3. Distribute `TotalReceivedAmount` across the listed payments:
   - Assign full `TotalReceivedAmount` to P1 first, then the surplus to P2 (greedy fill), OR
   - Respect the original P1/P2 split proportionally if `TotalReceivedAmount` matches both combined.
4. Set all listed payments to `Completed`, recording `ConfirmedByUserId`, `ConfirmedAt`, and updating each `Amount` to the distributed value.
5. Trigger `SyncBaseInstallmentsAsync` or recalculation of downstream pending payments if amounts changed.
6. Skip sequential-ordering validation for admin-initiated bulk confirmation.
7. Update `Registration.Status` based on resulting payment completion (same logic as single `ConfirmPaymentAsync`).

**Amount distribution rules**:

- If `TotalReceivedAmount == sum(payments[i].Amount)`: confirm each at its original amount (no redistribution needed).
- If `TotalReceivedAmount != sum(payments[i].Amount)`: fill payments in installment number order (P1 gets min(amount, original), P2 gets remainder).
- If surplus remains after all listed payments are filled: create a surplus refund manual payment, OR apply surplus to the next unlisted pending payment (P3). Let admin choose via request flag `applySurplusToNext: bool`.

### UC4 — Remove a member from a registration and generate a refund

**Who**: Board/Admin role
**Trigger**: Admin removes a `RegistrationMember` from a `Registration` and one or more payments (P1 or P2) are already `Completed`.
**Endpoint**: Extends existing `PUT /api/admin/registrations/{id}/members` (new admin-scoped variant) or reuses `UpdateRegistrationMembers` with admin override capability.

> **Note**: Currently `PUT /{id}/members` is representative-only. A new admin endpoint is required:
> `PUT /api/admin/registrations/{id}/members`

**Behavior on member removal**:

1. Calculate the removed member's `IndividualAmount` from their `RegistrationMember` record.
2. Recalculate `Registration.BaseTotalAmount` by removing the individual amount.
3. Compare new `BaseTotalAmount` to total completed base payments (P1 + P2 `Completed` amounts).
4. If `CompletedBasePayments > NewBaseTotalAmount`:
   - `RefundAmount = CompletedBasePayments - NewBaseTotalAmount`
   - Create a refund `Payment` with:
     - `Amount = -RefundAmount` (negative)
     - `Status = Refunded`
     - `IsManual = true`
     - `InstallmentNumber = next available`
     - `TransferConcept = "{prefix}-{familyCode}-{n}"` (same format)
     - `AdminNotes = "Refund: removed member {firstName} {lastName}"`
     - `ConceptLinesSerialized = ManualPaymentConceptLine("Devolución por baja de {fullName}", RefundAmount)`
5. Update P2 (if Pending) to reflect the reduced total: `P2.Amount = NewBaseTotalAmount - P1.Amount`.
6. Sync P3 if extras were also changed.
7. Update `Registration.BaseTotalAmount`, `Registration.TotalAmount`.
8. Put registration into `Draft` status with `HasPendingUserAcknowledgement = true` and `DraftTargetStatus = PartiallyPaid` (or whatever the appropriate target state is), so the family representative is notified and must acknowledge.

**Member removal validation**:

- Cannot remove a member if they are the only adult/parent in the registration (business constraint: at least one responsible adult required).
- If all members are removed, cancel the registration instead.

---

## Data Model Changes

### Payment entity — new fields

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `concept_overridden` | `boolean` | NO | `false` | Marks that concept/amount was manually adjusted after confirmation |
| `original_amount` | `numeric(10,2)` | YES | `null` | Snapshot of original amount before admin edit (for audit) |

```csharp
// In Payment entity (RegistrationsModels.cs)
public bool ConceptOverridden { get; set; } = false;
public decimal? OriginalAmount { get; set; }
```

### Registration entity — no new fields needed

The existing `Draft` status + `HasPendingUserAcknowledgement` + `DraftTargetStatus` already covers the family acknowledgment flow. No additions required.

---

## API Changes

### New endpoint — Edit any payment

```
PUT /api/admin/payments/{paymentId}
```

Replaces/extends the existing `PUT /api/admin/payments/{id}/manual` to work on any payment.

**Request**:

```csharp
public record AdminEditPaymentRequest
{
    public decimal? Amount { get; init; }          // null = no change
    public string? ConceptDescription { get; init; } // null = no change; replaces concept lines
    public DateTime? DueDate { get; init; }
    public string? AdminNotes { get; init; }
}
```

**Response**: `AdminPaymentResponse` (updated payment)

**HTTP Codes**:

| Code | Condition |
|------|-----------|
| 200 | Payment updated |
| 400 | Validation error (amount <= 0, etc.) |
| 404 | Payment not found |
| 409 | Payment is in `Failed` or `Refunded` status |

### New endpoint — Confirm combined payments

```
POST /api/admin/registrations/{registrationId}/payments/confirm-combined
```

**Request**: `ConfirmCombinedPaymentsRequest` (see UC3)

**Response**: `List<AdminPaymentResponse>` (all updated payments)

**HTTP Codes**:

| Code | Condition |
|------|-----------|
| 200 | All payments confirmed |
| 400 | Validation error |
| 404 | Registration or payment not found |
| 409 | Payments belong to different registrations, or a payment is not confirmable |
| 422 | Total amount doesn't cover even P1 (warn but don't block — admin must confirm intent) |

### New endpoint — Admin update registration members

```
PUT /api/admin/registrations/{registrationId}/members
```

**Request**: Same shape as existing `UpdateRegistrationMembersRequest` (list of member IDs + attendance details).

**Behavior differences from user-facing version**:

- Admin can remove members even if payments are `Completed` (triggers refund generation).
- Admin can add members from any `FamilyUnit` (not just the registration's own family unit), to handle edge cases like guest additions.
- Always puts registration in `Draft` status + sets `HasPendingUserAcknowledgement = true` if any payment already in `Completed` state (because the financial terms changed).

**Response**: `RegistrationResponse` with updated amounts and list of generated refund payments.

### Modified endpoint — Edit manual payment

```
PUT /api/admin/payments/{id}/manual
```

Keep this endpoint as-is for backward compatibility. It is already limited to `IsManual = true` payments. The new `PUT /api/admin/payments/{id}` endpoint handles all payment types.

---

## Files to Modify

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` | Add `ConceptOverridden`, `OriginalAmount` to `Payment` entity |
| `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs` | Add EF mappings for new columns |
| `src/Abuvi.API/Features/Payments/PaymentsModels.cs` | Add `AdminEditPaymentRequest`, `ConfirmCombinedPaymentsRequest`; add `ConceptOverridden`, `OriginalAmount` to `AdminPaymentResponse` |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Add `AdminEditPaymentAsync`, `ConfirmCombinedPaymentsAsync`, `RecalculatePendingInstallmentsAsync`, `GenerateRefundPaymentAsync` |
| `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs` | Add `PUT /api/admin/payments/{id}`, `POST /api/admin/registrations/{id}/payments/confirm-combined` |
| `src/Abuvi.API/Features/Payments/PaymentsValidators.cs` | Add validators for `AdminEditPaymentRequest`, `ConfirmCombinedPaymentsRequest` |
| `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` | Add `PUT /api/admin/registrations/{id}/members` |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Admin member update logic with refund generation and Draft transition |
| EF Migration | New migration for `concept_overridden`, `original_amount` columns |

---

## Service Layer Design

### `AdminEditPaymentAsync`

```
Input: paymentId, AdminEditPaymentRequest, adminUserId
1. Load payment + all sibling payments for the registration
2. Validate: status must not be Failed or Refunded
3. If Amount changed:
   a. Snapshot original amount → payment.OriginalAmount
   b. Set payment.ConceptOverridden = true
   c. If payment.Status == Completed:
      → call RecalculatePendingInstallmentsAsync
4. If ConceptDescription provided:
   → replace ConceptLinesSerialized with ManualPaymentConceptLine(description, newAmount)
5. Update AdminNotes, DueDate
6. Save
```

### `RecalculatePendingInstallmentsAsync`

```
Input: registrationId
1. Load registration + all payments
2. TotalOwed = registration.BaseTotalAmount + registration.ExtrasAmount
3. CompletedNonManual = sum(p.Amount for p in payments where p.Status == Completed and !p.IsManual)
4. Remaining = TotalOwed - CompletedNonManual
5. PendingAutoPayments = payments where Status == Pending and !IsManual, ordered by InstallmentNumber
6. For each pending payment:
   a. If Remaining > 0: assign min(Remaining, original proportional amount)
   b. Remaining -= assigned amount
7. If Remaining < 0 after filling:
   → call GenerateRefundPaymentAsync(abs(Remaining))
8. Save updated amounts
```

### `ConfirmCombinedPaymentsAsync`

```
Input: registrationId, ConfirmCombinedPaymentsRequest, adminUserId
1. Load all specified payments; validate same registration
2. Sort by InstallmentNumber
3. Distribute TotalReceivedAmount across payments (greedy fill)
4. For each payment: set Status = Completed, ConfirmedByUserId, ConfirmedAt, update Amount
5. If surplus and applySurplusToNext = true: reduce next pending payment by surplus
6. Trigger registration status update (same logic as single confirm)
```

### `GenerateRefundPaymentAsync`

```
Input: registrationId, amount (positive decimal), reason (string), adminUserId
1. Determine next installment number
2. Create Payment:
   - Amount = -amount (negative to indicate refund)
   - Status = Refunded
   - IsManual = true
   - ConceptLinesSerialized = ManualPaymentConceptLine(reason, amount)
   - AdminNotes = reason
3. Update Registration.TotalAmount
4. Save
```

---

## Acceptance Criteria

### UC1 — Edit any payment

1. `PUT /api/admin/payments/{id}` accepts non-manual payments (P1, P2, P3).
2. Editing a `Pending` payment only updates amount/concept — no downstream recalculation.
3. Editing a `Completed` payment's amount triggers recalculation of pending installments.
4. `OriginalAmount` is recorded the first time an amount is changed (subsequent edits do not overwrite it).
5. `ConceptOverridden = true` is returned in `AdminPaymentResponse` when applicable.
6. Returns 409 if payment is `Failed` or `Refunded`.

### UC2 — Recalculation after edit

1. After editing P1 (Completed, 500€ → 600€), P2 (Pending) is reduced by 100€.
2. After editing P1 (Completed, 500€ → 1000€, i.e., full total), P2 (Pending) is set to 0€ and a refund payment is NOT created (0 is valid, the payment just becomes a no-op).
3. After editing P1 to exceed total owed, a refund payment is created for the surplus.
4. Manual payments (IsManual = true) are never touched by recalculation.

### UC3 — Combined installment confirmation

1. `POST /api/admin/registrations/{id}/payments/confirm-combined` marks all listed payments as `Completed`.
2. `TotalReceivedAmount` is distributed across listed payments in installment number order.
3. If amounts match exactly, each payment is confirmed at its original amount.
4. Sequential ordering validation is bypassed for admin bulk confirms.
5. Registration status is updated after bulk confirm (same trigger as single confirm).

### UC4 — Member removal + refund

1. `PUT /api/admin/registrations/{id}/members` (admin endpoint) allows removing members even when P1/P2 are `Completed`.
2. Removing a member generates a negative-amount refund `Payment` if `CompletedBasePayments > NewBaseTotalAmount`.
3. Registration transitions to `Draft` status with `HasPendingUserAcknowledgement = true` when financial terms change.
4. The refund payment appears in `GET /api/registrations/{id}/payments` with `Status = Refunded` and a negative amount.
5. Removing the last adult from a registration returns 422 (business rule violation).

---

## Non-Functional Requirements

- **Audit trail**: `OriginalAmount` and `ConceptOverridden` provide a non-destructive record of admin corrections.
- **Atomicity**: `ConfirmCombinedPaymentsAsync` and member-removal-with-refund must execute in a single DB transaction.
- **Authorization**: All new `PUT/POST` endpoints under `/api/admin/...` require `Board` or `Admin` role.
- **Performance**: `RecalculatePendingInstallmentsAsync` operates on in-memory data already loaded; no additional DB queries.
- **Backward compatibility**: `ConceptOverridden` defaults to `false`; `OriginalAmount` defaults to `null` for existing payments. EF migration adds both columns as nullable.

---

## Testing

- Unit: `RecalculatePendingInstallmentsAsync` — all combinations (surplus, deficit, exact match, no pending left).
- Unit: `AdminEditPaymentAsync` — cannot edit Failed/Refunded; OriginalAmount snapshot only on first edit.
- Unit: `ConfirmCombinedPaymentsAsync` — amount distribution with exact, surplus, deficit cases.
- Unit: `GenerateRefundPaymentAsync` — negative amount, IsManual=true, InstallmentNumber increments correctly.
- Unit: Admin member removal — refund generated when CompletedBasePayments > NewBaseTotalAmount; Draft status set.
- Integration: Full flow — register → pay P1+P2 combined → confirm-combined → check remaining balance zero.
- Integration: Remove member → check refund payment created → check registration in Draft → family rep acknowledges → check status restored.

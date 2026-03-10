# Pagos Secuenciales y Pagos Manuales — Enriched User Story

## Summary

Two independent features to improve payment flow control and admin flexibility:

1. **Sequential Payment Enforcement**: Block P2 submission until P1 is completed; block P3 until P2 is completed. Users see the next payment but cannot act on it until the previous one is confirmed.
2. **Manual/Custom Payment Addition**: Allow admins to create ad-hoc payments on any registration to handle exceptions (discounts, surcharges, corrections, additional services not covered by extras).

## Problem

### Sequential Payments

Currently, all three installments (P1, P2, P3) are created at registration time and can be submitted in any order. The association wants families to pay in order — P1 first, then P2, then P3 — so they can track cash flow predictably and ensure commitment before proceeding to the next installment. The existing deadline fields (`DueDate` on each payment) define *when* a payment is due but don't enforce *ordering*.

**Can this be achieved with current deadlines alone?** No. Deadlines are fixed dates (e.g., "P1 due June 1, P2 due June 15") and don't react to whether the previous payment was actually completed. A family could skip P1 and submit P2 first. Sequential enforcement requires **status-based gating**, not date-based gating.

### Manual Payments

There are situations where the association needs to add a payment that doesn't fit the standard P1/P2/P3 model:

- A family needs a discount or partial refund after registration
- An extra charge for a special activity added after registration closes
- A correction for a pricing error
- Late fees or penalties
- Custom arrangements agreed outside the standard flow

Currently, there is no mechanism to create additional payments beyond the auto-generated installments.

---

## Solution 1: Sequential Payment Enforcement

### Approach

Add a **read-time visibility/actionability rule** rather than changing the data model. Each payment gets an `IsActionable` flag in the API response that indicates whether the user can submit/upload proof for it.

### Business Rules

| Payment | Actionable When |
|---------|----------------|
| P1 | Always actionable (if status is `Pending`) |
| P2 | P1 status is `Completed` |
| P3 | P2 status is `Completed` |
| Manual (P4+) | Always actionable (independent of sequence) |

> **Note**: Manual payments (see Solution 2) are exempt from sequential ordering — they exist outside the standard installment flow.

### Data Model Changes

**No new database columns needed.** The `IsActionable` flag is computed at read time based on the statuses of sibling payments within the same registration.

### API Response Changes

**PaymentResponse** — add computed field:

```csharp
public bool IsActionable { get; init; }  // Can the user submit/upload proof?
```

**AdminPaymentResponse** — same field added.

### Backend Logic

In `PaymentsService`, when building payment responses:

```csharp
// Pseudocode for computing IsActionable
bool ComputeIsActionable(Payment payment, List<Payment> allPaymentsForRegistration)
{
    // Manual payments (installment > 3) are always actionable
    if (payment.InstallmentNumber > 3)
        return payment.Status == PaymentStatus.Pending;

    // Already completed/failed/refunded — not actionable
    if (payment.Status != PaymentStatus.Pending)
        return false;

    // P1 is always actionable
    if (payment.InstallmentNumber == 1)
        return true;

    // P2 requires P1 to be Completed
    if (payment.InstallmentNumber == 2)
    {
        var p1 = allPaymentsForRegistration
            .FirstOrDefault(p => p.InstallmentNumber == 1);
        return p1?.Status == PaymentStatus.Completed;
    }

    // P3 requires P2 to be Completed
    if (payment.InstallmentNumber == 3)
    {
        var p2 = allPaymentsForRegistration
            .FirstOrDefault(p => p.InstallmentNumber == 2);
        return p2?.Status == PaymentStatus.Completed;
    }

    return false;
}
```

### Frontend Behavior

- **Non-actionable payments**: Shown in the list but with a locked/disabled state. The "Upload proof" or "Pay" button is disabled. A tooltip or message explains: *"Debes completar el pago anterior antes de realizar este."*
- **Actionable payments**: Normal interactive state.
- **Admin view**: Admins can always see all payments. The `IsActionable` flag is informational — admins can still confirm/reject any payment regardless of order (to handle edge cases).

### API Endpoint Changes

No new endpoints. Existing payment list endpoints include the new `IsActionable` field:

- `GET /api/registrations/{id}/payments` — user view
- `GET /api/admin/payments` — admin list view
- `GET /api/admin/payments/{id}` — admin detail view

### Validation: Block Proof Upload for Non-Actionable Payments

Add a server-side check in the proof upload endpoint to prevent users from submitting proof for a non-actionable payment:

```csharp
// In UploadPaymentProofAsync or equivalent
if (!ComputeIsActionable(payment, allPayments))
    return Results.Problem("Debes completar el pago anterior antes de subir un comprobante.",
        statusCode: 409);
```

This ensures sequential ordering is enforced even if the frontend is bypassed.

---

## Solution 2: Manual/Custom Payment Addition

### Approach

Add an **admin-only endpoint** to create a custom payment on any registration. The payment has a flexible structure: admin sets the amount, description, and optionally a deadline. It generates its own `TransferConcept` with the next available installment number.

### Data Model Changes

**No new entity needed.** The existing `Payment` entity supports all required fields:

- `InstallmentNumber`: Use 4, 5, 6... for manual payments (auto-incremented)
- `Amount`: Admin-defined
- `DueDate`: Optional, admin-defined
- `AdminNotes`: Required for manual payments — explains why it was created
- `ConceptLinesSerialized`: Store a custom concept line with the admin-provided description
- `TransferConcept`: Auto-generated with next installment number (e.g., `CAMP-GARG-4`)
- `Status`: Starts as `Pending`

**New field on Payment entity** (optional but recommended):

```csharp
public bool IsManual { get; set; } = false;  // Distinguishes auto-generated from admin-created
```

**Database migration** — add column:

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `is_manual` | `boolean` | NO | `false` | Marks admin-created payments |

### New DTO: CreateManualPaymentRequest

```csharp
public record CreateManualPaymentRequest
{
    public decimal Amount { get; init; }           // Required, > 0
    public string Description { get; init; }       // Required — what the payment is for
    public DateTime? DueDate { get; init; }         // Optional deadline
    public string? AdminNotes { get; init; }        // Optional additional notes
}
```

### New Concept Line Type: ManualConceptLine

```csharp
public record ManualPaymentConceptLine(
    string Description,       // Admin-provided description (e.g., "Cargo adicional por actividad especial")
    decimal Amount             // The amount
);
```

The `PaymentConceptLinesJson` wrapper gets an additional field:

```csharp
public record PaymentConceptLinesJson(
    List<PaymentConceptLine>? MemberLines,
    List<PaymentExtraConceptLine>? ExtraLines,
    ManualPaymentConceptLine? ManualLine         // NEW
);
```

### New API Endpoint

```
POST /api/admin/registrations/{registrationId}/payments/manual
```

**Request body**: `CreateManualPaymentRequest`

**Response**: `AdminPaymentResponse` (the created payment)

**Authorization**: Admin only (same auth as other admin payment endpoints)

### Backend Logic

```csharp
// In PaymentsService
public async Task<Payment> CreateManualPaymentAsync(
    Guid registrationId,
    CreateManualPaymentRequest request,
    Guid adminUserId)
{
    var registration = await GetRegistrationWithPayments(registrationId);

    // Determine next installment number
    var maxInstallment = registration.Payments.Max(p => p.InstallmentNumber);
    var nextInstallment = maxInstallment + 1;

    // Generate transfer concept
    var concept = GenerateTransferConcept(registration, nextInstallment);

    // Build concept lines
    var conceptLines = new PaymentConceptLinesJson(
        MemberLines: null,
        ExtraLines: null,
        ManualLine: new ManualPaymentConceptLine(request.Description, request.Amount)
    );

    var payment = new Payment
    {
        RegistrationId = registrationId,
        Amount = request.Amount,
        PaymentDate = DateTime.UtcNow,
        Method = PaymentMethod.Transfer,   // Default; admin can update later
        Status = PaymentStatus.Pending,
        InstallmentNumber = nextInstallment,
        DueDate = request.DueDate,
        TransferConcept = concept,
        AdminNotes = request.AdminNotes,
        IsManual = true,
        ConceptLinesSerialized = JsonSerializer.Serialize(conceptLines),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    await _paymentRepository.AddAsync(payment);

    // Update registration TotalAmount
    registration.TotalAmount += request.Amount;
    await _registrationRepository.UpdateAsync(registration);

    return payment;
}
```

### Manual Payment Behavior Rules

1. **Not affected by recalculation**: `SyncBaseInstallmentsAsync` and `SyncExtrasInstallmentAsync` must skip payments where `IsManual == true`.
2. **Not affected by sequential ordering**: Manual payments are always actionable if `Pending`.
3. **Deletable by admin**: Unlike auto-generated payments, manual payments can be deleted by an admin (new endpoint).
4. **Editable by admin**: Amount and description can be updated while status is `Pending`.

### Additional Admin Endpoints

```
PUT    /api/admin/payments/{id}/manual     — Update manual payment (amount, description, dueDate, notes)
DELETE /api/admin/payments/{id}/manual     — Delete manual payment (only if Pending)
```

**Update request DTO**:

```csharp
public record UpdateManualPaymentRequest
{
    public decimal? Amount { get; init; }
    public string? Description { get; init; }
    public DateTime? DueDate { get; init; }
    public string? AdminNotes { get; init; }
}
```

### Display Example

**Registration Payments View (user):**

```
✅ P1 — Primer plazo — 475€ — Completado
🔓 P2 — Segundo plazo — 475€ — Pendiente (disponible)
🔒 P3 — Extras — 170€ — Pendiente (bloqueado hasta completar P2)
🔓 P4 — Cargo adicional: Actividad especial — 30€ — Pendiente
```

**Admin Registration View:**

```
P1  | 475.00€  | Completed  | Auto  | CAMP-GARG-1
P2  | 475.00€  | Pending    | Auto  | CAMP-GARG-2
P3  | 170.00€  | Pending    | Auto  | CAMP-GARG-3
P4  | 30.00€   | Pending    | Manual| CAMP-GARG-4  [Edit] [Delete]
                                      └─ "Cargo adicional por actividad especial"
```

---

## Files to Modify

### Solution 1: Sequential Payments

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Payments/PaymentsModels.cs` | Add `IsActionable` to `PaymentResponse` and `AdminPaymentResponse` |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Add `ComputeIsActionable()` logic; include in response mapping; add validation in proof upload |

### Solution 2: Manual Payments

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` | Add `IsManual` property to `Payment` entity |
| `src/Abuvi.API/Features/Payments/PaymentsModels.cs` | Add `ManualPaymentConceptLine`, `CreateManualPaymentRequest`, `UpdateManualPaymentRequest` DTOs; update `PaymentConceptLinesJson` |
| `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs` | Add `is_manual` column mapping |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Add `CreateManualPaymentAsync`, `UpdateManualPaymentAsync`, `DeleteManualPaymentAsync`; skip manual payments in sync methods |
| `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs` | Add `POST/PUT/DELETE` manual payment endpoints |
| EF Migration | New migration for `is_manual` column |

---

## Acceptance Criteria

### Sequential Payments

1. P2 cannot have proof uploaded until P1 status is `Completed`.
2. P3 cannot have proof uploaded until P2 status is `Completed`.
3. The API response includes `IsActionable` boolean for each payment.
4. Server-side validation returns HTTP 409 if attempting to upload proof for a non-actionable payment.
5. Admins can confirm/reject any payment regardless of sequence (admin override).
6. When P1 is confirmed, P2 becomes immediately actionable (no delay/refresh needed beyond normal API call).

### Manual Payments

1. Admins can create a manual payment on any registration via `POST /api/admin/registrations/{id}/payments/manual`.
2. Manual payments get the next sequential installment number (4, 5, ...) and auto-generated transfer concept.
3. Manual payments have `IsManual = true` and are flagged as such in API responses.
4. Manual payments are not affected by `SyncBaseInstallmentsAsync` or `SyncExtrasInstallmentAsync`.
5. Manual payments are always actionable (not subject to sequential ordering).
6. Admins can update amount, description, deadline, and notes for a pending manual payment.
7. Admins can delete a manual payment if its status is `Pending`.
8. Deleting a manual payment updates the registration's `TotalAmount` accordingly.
9. Manual payment concept lines show the admin-provided description and amount.

## Non-Functional Requirements

- **Performance**: `IsActionable` is computed at read time from in-memory data (payments already loaded); no additional DB query.
- **Backward compatibility**: `IsActionable` defaults to `true` for existing payments in completed/failed states. `IsManual` defaults to `false` for all existing payments.
- **Security**: Manual payment endpoints require admin authorization. Proof upload validation prevents user-side bypass of sequential ordering.
- **Data integrity**: Manual payment amounts are reflected in `Registration.TotalAmount`. Deletion reverses the amount adjustment.

## Testing

- Unit test: `ComputeIsActionable` returns correct values for all combinations of P1/P2/P3 statuses.
- Unit test: Proof upload is rejected (409) for non-actionable payments.
- Unit test: Manual payment creation assigns correct installment number and transfer concept.
- Unit test: Sync methods skip manual payments.
- Unit test: Manual payment deletion adjusts registration total.
- Integration test: Full flow — create registration → pay P1 → P2 becomes actionable → add manual payment → verify independence from sequence.

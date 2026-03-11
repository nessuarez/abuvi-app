# Enhanced Registration Status Flow with Installment-Based Transitions

## Summary

Currently, a camp registration stays in `Pending` status from the moment it is created until ALL payment installments are confirmed, at which point it jumps to `Confirmed`. This creates confusion for families who may have already paid their first installment but still see "Pending" for months.

This feature introduces intermediate registration statuses tied to installment confirmation, along with transactional email notifications on each status transition.

## Current State

### Registration Status Enum (`RegistrationsModels.cs:113`)

```csharp
public enum RegistrationStatus { Pending, Confirmed, Cancelled, Draft }
```

### Current Flow

```
[User creates registration] → Pending
[Admin confirms ALL payments] → Confirmed
[User/Admin cancels] → Cancelled
[Admin edits registration] → Draft
```

### Current Payment Confirmation Logic (`PaymentsService.cs:159-195`)

```csharp
// Only transitions to Confirmed when ALL payments are completed
if (allPayments.All(p => p.Status == PaymentStatus.Completed))
{
    registration.Status = RegistrationStatus.Confirmed;
}
```

### Payment Installments Structure

- **2 base installments** (always created, hardcoded split: P1 = ceil(total/2), P2 = total - P1)
- **Optional P3** for extras (created by `SyncExtrasInstallmentAsync`)
- **Manual payments** can add additional installments

### Existing Email Infrastructure

- Provider: **Resend** (`ResendEmailService.cs`)
- Existing relevant methods:
  - `SendCampRegistrationConfirmationAsync()` — sent on registration creation
  - `SendCampRegistrationCancellationAsync()` — sent on cancellation
  - `SendPaymentReceiptAsync()` — sent on payment (takes amount + reference)
- Data model: `CampRegistrationEmailData` with full registration details

---

## Proposed Changes

### 1. New Registration Status Enum

```csharp
public enum RegistrationStatus
{
    Pending,           // Registration created, no payments confirmed yet
    PartiallyPaid,     // At least one installment confirmed, but not all
    Confirmed,         // All installments confirmed (registration fully paid)
    Cancelled,         // Registration cancelled
    Draft              // Admin has edited the registration (under review)
}
```

**Design decision: `PartiallyPaid` instead of `InstallmentOnePaid` / `InstallmentTwoPaid`**

Rationale:
- The system supports 2, 3, or more installments (base + extras + manual). Hardcoding installment numbers in the enum is fragile.
- The frontend can derive which installments are paid by inspecting the `payments` array (already returned in responses).
- `PartiallyPaid` is semantically clear: "your registration is confirmed and we've received at least one payment."

### 2. Updated Payment Confirmation Logic

**File:** `PaymentsService.cs` — `ConfirmPaymentAsync` method

```csharp
// Replace current logic (lines 179-192) with:
var allPayments = await paymentsRepo.GetByRegistrationIdAsync(
    payment.RegistrationId, ct);

var completedCount = allPayments.Count(p => p.Status == PaymentStatus.Completed);
var totalCount = allPayments.Count;
var registration = payment.Registration;

if (completedCount == totalCount)
{
    // All installments paid → fully confirmed
    registration.Status = RegistrationStatus.Confirmed;
    await registrationsRepo.UpdateAsync(registration, ct);

    // Send full confirmation email
    await SendRegistrationConfirmedEmailAsync(registration, ct);
}
else if (completedCount > 0 && registration.Status == RegistrationStatus.Pending)
{
    // First payment confirmed → partially paid
    registration.Status = RegistrationStatus.PartiallyPaid;
    await registrationsRepo.UpdateAsync(registration, ct);

    // Send installment confirmation email
    await SendInstallmentConfirmedEmailAsync(
        registration, payment.InstallmentNumber, completedCount, totalCount, ct);
}
else if (completedCount > 0)
{
    // Additional payment confirmed but not all → still partially paid, send email
    await SendInstallmentConfirmedEmailAsync(
        registration, payment.InstallmentNumber, completedCount, totalCount, ct);
}
```

### 3. New Email Methods

**File:** `IEmailService.cs` — Add two new methods:

```csharp
/// <summary>
/// Sends an email when an installment payment is confirmed (not all paid yet)
/// </summary>
Task SendInstallmentConfirmedEmailAsync(
    InstallmentConfirmedEmailData data,
    CancellationToken ct);

/// <summary>
/// Sends an email when all installments are paid and registration is fully confirmed
/// </summary>
Task SendRegistrationFullyConfirmedEmailAsync(
    RegistrationConfirmedEmailData data,
    CancellationToken ct);
```

**New DTOs:**

```csharp
public record InstallmentConfirmedEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public required int InstallmentNumber { get; init; }
    public required int TotalInstallments { get; init; }
    public required int PaidInstallments { get; init; }
    public required decimal InstallmentAmount { get; init; }
    public required decimal TotalPaid { get; init; }
    public required decimal TotalAmount { get; init; }
    public string? AdminNotes { get; init; }
}

public record RegistrationConfirmedEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required int TotalInstallments { get; init; }
}
```

### 4. Email Implementation

**File:** `ResendEmailService.cs` — Implement both new methods following existing patterns:

- **Installment confirmed email**: Subject like "Pago {N} de {Total} confirmado - {CampName}". Body should include installment number, amount paid, remaining amount, and admin notes if any.
- **Fully confirmed email**: Subject like "Inscripción confirmada - {CampName}". Body should confirm all payments received and registration is fully confirmed.
- Both emails should BCC the board email (`junta.abuvi@gmail.com`).

### 5. Frontend Changes

#### 5.1. TypeScript types

**File:** `frontend/src/types/registration.ts` (or equivalent)

Add `'PartiallyPaid'` to the `RegistrationStatus` type union.

#### 5.2. Status badge display

**File:** Registration status badge/display component

| Status | Label (ES) | Color | Icon |
|--------|-----------|-------|------|
| `Pending` | Pendiente | Yellow/Warning | Clock |
| `PartiallyPaid` | Pago parcial confirmado | Blue/Info | CheckCircle |
| `Confirmed` | Confirmada | Green/Success | CheckCircle |
| `Cancelled` | Cancelada | Red/Danger | XCircle |
| `Draft` | Borrador | Gray/Secondary | Edit |

For `PartiallyPaid`, the badge tooltip or subtitle should show "Plazo {N} de {Total} confirmado" derived from the payments array in the registration response.

#### 5.3. Progress bar (camp capacity)

The progress bar counting registered people should count registrations with status `Pending`, `PartiallyPaid`, or `Confirmed` (excluding `Cancelled` and `Draft`). Verify current logic and adjust if needed.

#### 5.4. Admin filters

Update any admin filter dropdowns to include `PartiallyPaid` as a filterable status option.

### 6. Database Migration

A new EF Core migration is required to update the `RegistrationStatus` enum column. Since PostgreSQL stores enums as strings in this project, the migration should be straightforward (no data transformation needed — existing values remain valid).

### 7. Existing Status Checks — Impact Analysis

Review and update all places that check registration status:

| Location | Current Check | Action Required |
|----------|--------------|-----------------|
| `RegistrationsService.CreateAsync` | Sets `Pending` | No change |
| `RegistrationsService.CancelAsync` | Checks `!= Cancelled` | No change |
| `RegistrationsService.AdminUpdateAsync` | Checks `!= Cancelled`, sets `Draft` | No change |
| `PaymentsService.ConfirmPaymentAsync` | Sets `Confirmed` when all paid | **Update** (core change) |
| Frontend capacity counter | Counts non-cancelled | **Verify** includes `PartiallyPaid` |
| Frontend status filter | Lists known statuses | **Update** to include `PartiallyPaid` |
| Frontend status badge | Maps status to color/label | **Update** to handle `PartiallyPaid` |
| `RegistrationsEndpoints` authorization | Various checks | **Review** — `PartiallyPaid` should behave like `Confirmed` for access |

---

## Out of Scope (Separate Feature)

### User-Initiated Registration Edits with Admin Notification

The user mentioned wanting to be notified when a family modifies their registration. Currently:
- Only admins can edit registrations (via `AdminUpdateAsync`)
- Users cannot edit their own registrations after creation (except cancellation)

If user-editable registrations are desired, this would require:
1. New endpoint: `PUT /api/registrations/{id}` (family representative)
2. Status transition: `PartiallyPaid` or `Confirmed` → `PendingReview` (new status)
3. Email notification to the board when a family edits their registration
4. Admin approval flow to move back to previous status

This is a separate, larger feature and should be tracked independently.

---

## Acceptance Criteria

1. When an admin confirms a payment installment and not all installments are paid, the registration status changes to `PartiallyPaid`
2. When an admin confirms the last remaining payment installment, the registration status changes to `Confirmed`
3. An email is sent to the family representative on each installment confirmation, including installment number, amount, and remaining balance
4. An email is sent to the family representative when all installments are confirmed (full confirmation)
5. The frontend displays `PartiallyPaid` with a blue/info badge and shows which installment was paid
6. Admin filters include the new `PartiallyPaid` status
7. The camp capacity progress bar correctly counts `PartiallyPaid` registrations
8. All existing functionality (cancellation, admin edit, payment rejection) continues to work correctly
9. Board BCC email is included in all transactional emails

## Files to Modify

### Backend
- `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — Add `PartiallyPaid` to enum
- `src/Abuvi.API/Features/Payments/PaymentsService.cs` — Update `ConfirmPaymentAsync` transition logic + email sending
- `src/Abuvi.API/Common/Services/IEmailService.cs` — Add new email method signatures + DTOs
- `src/Abuvi.API/Common/Services/ResendEmailService.cs` — Implement new email methods
- New migration file for enum update

### Frontend
- Registration types file — Add `'PartiallyPaid'` to status type
- Status badge component — Add display for new status
- Admin registration list — Update filters
- Camp capacity component — Verify counting logic

### Tests
- Unit tests for `ConfirmPaymentAsync` with partial and full payment scenarios
- Unit tests for new email methods
- Frontend component tests for new status display

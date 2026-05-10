# Registration Status Flow — Complete Lifecycle & Notifications

## Summary

This feature redesigns the registration status lifecycle to give the board full control over status transitions, ensures families are notified at every meaningful change, and introduces a complete audit trail of all status transitions for timeline display.

The key insight is that **status changes are a board/admin responsibility**, not automatic side-effects of payment confirmation. The only exception is the automatic transition to `FullyPaid` when all payment installments are confirmed.

---

## Current State (Baseline)

### Status Enum (`RegistrationsModels.cs:113`)

```csharp
public enum RegistrationStatus { Pending, Confirmed, Cancelled, Draft }
```

### Current Payment Confirmation Logic (`PaymentsService.cs:159-195`)

```csharp
// Only transitions to Confirmed when ALL payments are completed
if (allPayments.All(p => p.Status == PaymentStatus.Completed))
{
    registration.Status = RegistrationStatus.Confirmed;
}
```

No email is sent on payment confirmation today. The only transactional emails that exist are:

- `SendCampRegistrationConfirmationAsync` — sent when registration is created
- `SendCampRegistrationCancellationAsync` — sent when registration is cancelled

---

## Proposed Changes

### 1. New Registration Status Enum

```csharp
public enum RegistrationStatus
{
    Pending,        // Registration created, awaiting board validation
    PartiallyPaid,  // Board has explicitly validated: P1 received + registration data correct
    FullyPaid,      // All payments confirmed (automatic); board final review pending
    Confirmed,      // Board gave explicit final approval; registration fully confirmed
    Draft,          // Board is making changes; user must acknowledge
    Cancelled       // Registration cancelled
}
```

**Design rationale — why `FullyPaid` instead of making `Confirmed` automatic:**

The board may want to add last-minute changes (e.g., an extra) before camp starts, even after all payments are received. `FullyPaid` signals "we have the money, but we're not done yet." `Confirmed` is the board's explicit stamp of approval. `Confirmed` can still be reopened (→ `Draft`) at any point before the camp starts.

---

### 2. Complete Status Transition Map

```
[User creates registration] ──────────────────────────► Pending
                                                             │
[Board explicit approval]                                    │
(payment received + data validated)                          ▼
Pending ────────────────────────────────────────────► PartiallyPaid
                                                             │
[Last payment confirmed by admin] (automatic)                │
PartiallyPaid ──────────────────────────────────────► FullyPaid
                                                             │
[Board explicit final confirmation]                          │
FullyPaid ──────────────────────────────────────────► Confirmed
                                                             │
[Board edits registration] (any non-Cancelled status)        │
Any ────────────────────────────────────────────────► Draft
                                                             │
[User confirms changes, or Board force-confirms]             │
Draft ──────────────────────────────────────────────► [Board-chosen target status]

[User or Admin] ────────────────────────────────────► Cancelled (from any non-Cancelled status)
```

**Intermediate payments (not the last one):**
When P2 of 3 is confirmed, no status change occurs. Only a "pago recibido" email is sent. `FullyPaid` only triggers when the last payment makes `allPayments.All(Completed)` true.

**Permission matrix:**

| Transition | Who can trigger |
|-----------|----------------|
| Any → `Pending` | System (on creation) |
| `Pending` → `PartiallyPaid` | Board / Admin |
| `PartiallyPaid` → `FullyPaid` | **Automatic** (last payment confirmed) |
| `FullyPaid` → `Confirmed` | Board / Admin |
| Any → `Draft` | Board / Admin (when editing) |
| `Draft` → [any] | User (confirm) or Board (force-confirm) |
| Any → `Cancelled` | User (own registration) or Board / Admin |

---

### 3. Email Notifications Per Status Transition

Every status transition sends an automatic email to the family representative. Additionally, payment confirmation always sends a payment receipt email regardless of status changes.

| Event | Email | Automatic? |
|-------|-------|-----------|
| Registration created (`Pending`) | Existing: "Inscripción registrada" | ✅ Always |
| `Pending` → `PartiallyPaid` | New: "Inscripción al corriente — plazo 1 confirmado" | ✅ Always |
| Payment confirmed (not last) | New: "Pago recibido — plazo N de M" | ✅ Always |
| `PartiallyPaid` → `FullyPaid` (last payment) | New: "Todos los pagos recibidos" | ✅ Always |
| `FullyPaid` → `Confirmed` | New: "Inscripción totalmente confirmada" | ✅ Always |
| `Draft` sent to user | New: "Hay cambios en tu inscripción que revisar" | ✅ When board notifies (board chooses, with warning) |
| `Draft` → [target] (user confirms) | New: "Has confirmado los cambios — inscripción [status]" | ✅ Always |
| Any → `Cancelled` | Existing: "Inscripción cancelada" | ✅ Always |

**Board edit notification (warning UX):**
When a board/admin saves changes to a registration via the admin edit interface, the UI **must show a warning**: _"Hay cambios pendientes de notificar a la familia. ¿Deseas enviar una notificación ahora?"_ The board can send immediately or defer. This is not automatic because the board may want to batch edits before notifying.

The backend `AdminUpdateAsync` endpoint accepts an optional `notifyUser: bool` flag. If `true`, the email is sent immediately. If `false`, no email is sent but the registration goes to `Draft` and the user will see the pending changes on next login.

---

### 4. Status History / Audit Trail

Every status transition must be persisted for timeline display and audit purposes.

**New entity: `RegistrationStatusHistory`**

```csharp
public class RegistrationStatusHistory
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public RegistrationStatus PreviousStatus { get; set; }
    public RegistrationStatus NewStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }      // null for automatic transitions
    public DateTime ChangedAt { get; set; }
    public StatusChangeTrigger Trigger { get; set; }
    public string? Notes { get; set; }              // optional reason / context

    // Navigation
    public Registration Registration { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}

public enum StatusChangeTrigger
{
    Automatic,       // Triggered by system (e.g., last payment confirmed)
    AdminAction,     // Board/admin explicitly changed status
    UserConfirmed    // User acknowledged Draft changes
}
```

**EF Core table**: `registration_status_history`

This history is returned in the registration detail response and used to render a frontend timeline:

```
✅ 2026-03-01  Inscripción creada — Pendiente
💶 2026-03-15  Pago del plazo 1 recibido (150,00 €)
✅ 2026-03-16  Junta confirmó inscripción — Al corriente
✏️  2026-04-01  Junta realizó cambios — En revisión
✅ 2026-04-02  Familia confirmó cambios — Al corriente
💶 2026-04-20  Pago del plazo 2 recibido (150,00 €) — Todos los pagos recibidos
✅ 2026-04-25  Junta confirmó inscripción final — Confirmada
```

---

### 5. Registration Entity Changes

Add two fields to `Registration`:

```csharp
// Target status to restore after Draft is resolved (set by board when editing)
public RegistrationStatus? DraftTargetStatus { get; set; }

// True if there are pending changes not yet acknowledged by the user
public bool HasPendingUserAcknowledgement { get; set; } = false;
```

`DraftTargetStatus` defaults to the status that was active immediately before the `Draft` transition, but the board can override it when force-confirming or when the user confirms.

---

### 6. Backend API Changes

#### 6.1. New endpoint: Manual Status Change

```
PATCH /api/registrations/{id}/status
Authorization: Admin or Board role only
```

**Request:**

```json
{
  "newStatus": "PartiallyPaid",
  "notes": "Membresía y cuota 2026 validadas. P1 recibido.",
  "notifyUser": true
}
```

**Validation rules:**

- Only `Admin` / `Board` roles allowed
- `Cancelled` is blocked here — use the existing cancel endpoint
- `Draft` is blocked here — happens automatically on edit
- Valid manual transitions: `Pending→PartiallyPaid`, `FullyPaid→Confirmed`, `Any→Pending` (reopen), `Draft→[any]` (force-confirm only)
- All transitions are logged to `RegistrationStatusHistory`

**Response:** Updated `RegistrationResponse` (existing shape)

#### 6.2. New endpoint: User confirms Draft

```
POST /api/registrations/{id}/confirm-changes
Authorization: Authenticated (own registration) or Admin/Board (force)
```

**Request body:** empty (no params)

**Behavior:**

1. Validates registration is in `Draft` status
2. Validates caller owns the registration OR is Admin/Board
3. Transitions to `DraftTargetStatus` (or previous status if not set)
4. Logs to `RegistrationStatusHistory` with `Trigger = UserConfirmed`
5. Sends "changes confirmed" email to family
6. Sets `HasPendingUserAcknowledgement = false`

**Response:** Updated `RegistrationResponse`

#### 6.3. Updated: `AdminUpdateAsync` — optional notify flag

Add `notifyUser` parameter (defaults to `false` if omitted):

```
PUT /api/registrations/{id}/admin
Authorization: Admin or Board
```

**Request body additions:**

```json
{
  "...existing fields...",
  "notifyUser": true,
  "draftTargetStatus": "PartiallyPaid"
}
```

**Behavior changes:**

1. Status changes to `Draft` (existing)
2. Stores `DraftTargetStatus` on the registration entity
3. Sets `HasPendingUserAcknowledgement = true`
4. If `notifyUser = true` → sends "hay cambios en tu inscripción" email to family immediately
5. Logs transition to `RegistrationStatusHistory` with `Trigger = AdminAction`

#### 6.4. Updated: `ConfirmPaymentAsync` — payment emails + auto `FullyPaid`

Remove the current automatic `Confirmed` logic. Replace with:

```csharp
var completedCount = allPayments.Count(p => p.Status == PaymentStatus.Completed);
var totalCount = allPayments.Count;

if (completedCount == totalCount)
{
    // Last payment confirmed → auto-transition to FullyPaid
    registration.Status = RegistrationStatus.FullyPaid;
    await registrationsRepo.UpdateAsync(registration, ct);
    await LogStatusHistoryAsync(registration, previousStatus, RegistrationStatus.FullyPaid,
        changedByUserId: adminUserId, trigger: StatusChangeTrigger.Automatic, ct);

    // Send "todos los pagos recibidos" email
    await SendAllPaymentsReceivedEmailAsync(registration, allPayments, ct);
}
else
{
    // Intermediate payment — just send receipt, no status change
    await SendPaymentReceivedEmailAsync(registration, payment, completedCount, totalCount, ct);
}
```

**Important**: `ConfirmPaymentAsync` does **not** trigger `PartiallyPaid` — that is a board-only action via the new status endpoint.

#### 6.5. New helper: `LogStatusHistoryAsync`

Private helper shared across service methods. Inserts a `RegistrationStatusHistory` row. Must be called on every status transition, including cancellation (existing `CancelAsync`).

#### 6.6. Updated: `RegistrationDetailResponse`

Add `statusHistory` to the registration detail response:

```json
{
  "id": "...",
  "status": "PartiallyPaid",
  "hasP endingUserAcknowledgement": false,
  "statusHistory": [
    {
      "id": "...",
      "previousStatus": "Pending",
      "newStatus": "PartiallyPaid",
      "changedAt": "2026-03-16T10:00:00Z",
      "changedByUserName": "Junta Abuvi",
      "trigger": "AdminAction",
      "notes": "Membresía y cuota 2026 validadas."
    }
  ],
  "...": "..."
}
```

---

### 7. Frontend Changes

#### 7.1. Status badge display

| Status | Badge (ES) | Color | Icon |
|--------|-----------|-------|------|
| `Pending` | Pendiente | Yellow/Warning | Clock |
| `PartiallyPaid` | Al corriente | Blue/Info | CheckCircle |
| `FullyPaid` | Pago completo | Teal/Success | CreditCard |
| `Confirmed` | Confirmada | Green/Success | CheckCircle |
| `Draft` | En revisión | Orange/Warning | Edit |
| `Cancelled` | Cancelada | Red/Danger | XCircle |

#### 7.2. Timeline component (user-facing registration detail)

Display a vertical timeline using `statusHistory`. Each entry shows:

- Icon (based on trigger + status)
- Date + time
- Human-readable description in Spanish
- Who made the change (admin name or "Sistema" for automatic)
- Notes (if present)

#### 7.3. User: "Confirmar cambios" banner

When `status === 'Draft'` and `hasPendingUserAcknowledgement === true`, show a prominent banner on the registration detail page:

> _"La Junta ha realizado cambios en tu inscripción. Revisa los detalles y confirma que todo es correcto."_
> **[Confirmar cambios]** button → calls `POST /api/registrations/{id}/confirm-changes`

#### 7.4. Admin: Status change UI

In the admin registration detail view:

- Dropdown to change status manually (only shows valid target statuses)
- Required "notes" field for all manual changes
- Optional "Notificar a la familia" checkbox (default: checked)
- Calls `PATCH /api/registrations/{id}/status`

#### 7.5. Admin: Warning on edit

When admin saves an edit to a registration (AdminUpdateAsync), the frontend shows:

> ⚠️ _"Hay cambios en la inscripción pendientes de notificar a la familia."_
> Toggle: **Notificar ahora** (default: on) / **Notificar más tarde**

If "Notificar ahora" → `notifyUser: true` in the request body.

#### 7.6. Admin filters

Update status filter dropdown to include: `PartiallyPaid`, `FullyPaid`.

#### 7.7. Capacity progress bar

Count registrations with status `Pending`, `PartiallyPaid`, `FullyPaid`, or `Confirmed`. Exclude `Cancelled` and `Draft`.

---

### 8. Database Migrations

Two migrations required:

**Migration 1: `AddRegistrationStatusHistory`**

- New table `registration_status_history`
- Columns: `id uuid PK`, `registration_id uuid FK`, `previous_status varchar(30)`, `new_status varchar(30)`, `changed_by_user_id uuid FK nullable`, `changed_at timestamptz`, `trigger varchar(20)`, `notes text nullable`
- Index on `registration_id`

**Migration 2: `UpdateRegistrationForDraftFlow`**

- Add `draft_target_status varchar(30) nullable` to `registrations`
- Add `has_pending_user_acknowledgement bool NOT NULL DEFAULT false` to `registrations`
- No data migration needed for existing rows (new columns are nullable / have defaults)

---

### 9. Existing Status Checks — Impact Analysis

| Location | Current check | Action |
|----------|--------------|--------|
| `RegistrationsService.CreateAsync` | Sets `Pending` | No change |
| `RegistrationsService.CancelAsync` | Checks `!= Cancelled` | Add history log |
| `RegistrationsService.AdminUpdateAsync` | Sets `Draft` | Add `notifyUser`, `draftTargetStatus`, history log |
| `RegistrationsService.DeleteAsync` | Blocks `Confirmed` | **Add `FullyPaid` to block list** |
| `RegistrationsService.SyncExtrasInstallmentAsync` | Checks `== Pending` | **Review: may need to allow `FullyPaid` or `Draft`** |
| `RegistrationsService.MapStatusEs` | Switch on status | **Add `PartiallyPaid`, `FullyPaid`** |
| `PaymentsService.ConfirmPaymentAsync` | Auto `Confirmed` | **Replace with `FullyPaid` auto + email** |
| Frontend capacity counter | Non-cancelled | **Verify includes `PartiallyPaid`, `FullyPaid`** |
| Frontend status badge | Maps status | **Add `PartiallyPaid`, `FullyPaid`** |
| Frontend admin filters | Status list | **Add new statuses** |

---

## Out of Scope (This Feature)

- **Backfill notifications**: Existing P1-confirmed registrations will be transitioned manually by the board via the new status endpoint. No bulk notification backfill is included.
- **User-initiated registration edits**: Users cannot edit registrations (only cancel). This is a separate future feature.
- **Payment due date reminders**: Automated reminders before payment deadlines.
- **Camp-start auto-confirmation**: Auto-confirming `FullyPaid` registrations when the camp start date passes.

---

## Acceptance Criteria

1. Adding `PartiallyPaid` and `FullyPaid` to the status enum does not break existing data
2. Only board/admin roles can call `PATCH /api/registrations/{id}/status`
3. Every status transition is logged to `registration_status_history`
4. The registration detail response includes the full status history
5. Confirming the last payment automatically transitions status to `FullyPaid` and sends a "todos los pagos recibidos" email
6. Confirming any non-last payment sends a "pago recibido" email with no status change
7. When board edits a registration (→ `Draft`), `draftTargetStatus` is stored and `hasPendingUserAcknowledgement` is set to `true`
8. When `notifyUser = true` on admin edit, a "changes pending" email is sent immediately
9. When user calls `confirm-changes`, registration transitions to `draftTargetStatus`, history is logged, and a confirmation email is sent
10. Board can call `confirm-changes` (force) on behalf of the user
11. `Confirmed` and `FullyPaid` registrations cannot be deleted (must cancel first)
12. Frontend shows the "Confirmar cambios" banner when registration is in `Draft` with pending acknowledgement
13. Frontend shows the status history timeline in registration detail
14. Frontend warns board when saving admin edits about pending notification
15. All transactional emails are sent in Spanish with BCC to `junta.abuvi@gmail.com`
16. Cancellation email continues to send automatically (no change to existing behaviour)

---

## Files to Modify

### Backend

- `Features/Registrations/RegistrationsModels.cs` — Add `PartiallyPaid`, `FullyPaid` to enum; new `RegistrationStatusHistory` entity + `StatusChangeTrigger` enum; add `DraftTargetStatus` and `HasPendingUserAcknowledgement` to `Registration`; update response DTOs to include `statusHistory` + `hasPendingUserAcknowledgement`
- `Features/Registrations/RegistrationsService.cs` — Add `ConfirmChangesAsync` (user confirm draft); update `AdminUpdateAsync` with `notifyUser` + `draftTargetStatus`; add `LogStatusHistoryAsync` helper; update `MapStatusEs`; update `DeleteAsync` guard
- `Features/Registrations/RegistrationsRepository.cs` — Add `AddStatusHistoryAsync`; add `GetStatusHistoryAsync`; update `GetByIdWithDetailsAsync` to include status history
- `Features/Registrations/RegistrationsEndpoints.cs` — Add `PATCH /{id}/status` endpoint; add `POST /{id}/confirm-changes` endpoint; update admin edit endpoint signature
- `Features/Payments/PaymentsService.cs` — Replace `ConfirmPaymentAsync` status logic; add `IEmailService` dependency; add payment email helpers
- `Common/Services/IEmailService.cs` — Add new email method signatures + DTOs for payment received, all payments received, changes pending, changes confirmed
- `Common/Services/ResendEmailService.cs` — Implement new email methods in Spanish
- `Data/Configurations/RegistrationConfiguration.cs` — Add `DraftTargetStatus`, `HasPendingUserAcknowledgement` column mappings
- New: `Data/Configurations/RegistrationStatusHistoryConfiguration.cs`
- New EF Core migrations (×2)

### Frontend

- Registration types file — Add `'PartiallyPaid'`, `'FullyPaid'` to status type union; add `statusHistory` and `hasPendingUserAcknowledgement` to registration type
- Status badge component — Add display for new statuses
- Registration detail page — Add status timeline component; add "Confirmar cambios" banner; add payment history with statuses
- Admin registration detail — Add status change dropdown UI; add force-confirm action
- Admin registration edit form — Add "Notificar a la familia" toggle with warning
- Admin registration list — Update status filter dropdown
- Camp capacity component — Verify `PartiallyPaid` and `FullyPaid` counted

### Tests

- Unit tests for `ConfirmPaymentAsync`: partial payment email, last payment → `FullyPaid`, email failure non-blocking
- Unit tests for new `ConfirmChangesAsync` service method
- Unit tests for `PATCH /status` endpoint: role checks, valid transitions, invalid transitions
- Unit tests for `LogStatusHistoryAsync`
- Frontend component tests: timeline, banner visibility, status badge

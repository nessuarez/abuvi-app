# Fix: Enrollment Status Change Email Notification Not Sent

## Summary

When an admin changes the status of a registration (`PATCH /api/registrations/{id}/status`) with the "Notificar a la familia" toggle enabled, the corresponding Resend email is not sent (or it is sent silently without feedback). Additionally, notification emails lack context: they do not include the admin's notes/reason, a summary of what changed, or a warning to the admin when changes remain unnotified.

Four root causes / gaps have been identified:

1. **Silent failures**: `ChangeStatusAsync` catches exceptions from the email service and logs them, but does **not** surface the failure to the admin. If Resend throws, the status change succeeds silently.
2. **Missing email for `Pending` status**: Only `PartiallyPaid` and `Confirmed` trigger an email in `ChangeStatusAsync`. Changing to `Pending` falls into `_ => Task.CompletedTask`.
3. **No board reason in email body**: The admin's required `Notes` field is stored in `RegistrationStatusHistory` but never included in any notification email template.
4. **No change summary in draft-edit email**: `SendDraftChangesNotificationAsync` sends a generic "hay cambios" message with no detail. The family has to log in to discover what changed.
5. **No unnotified-changes warning in the admin UI**: When the admin saves changes with `NotifyUser = false`, there is no persistent indicator that the family has not been notified.

---

## Current Behavior (bugs)

- Admin opens `AdminStatusChangeDialog`, selects `Pending`, enables "Notificar a la familia" → no email is sent.
- Admin selects `PartiallyPaid` or `Confirmed`, enables notification → email is sent, but the family sees no reason ("Motivo: …") and no list of what changed.
- Admin edits registration with `NotifyUser = false` → no warning is shown afterward that the family remains unaware.
- Any Resend failure is swallowed; admin sees no feedback.

---

## Expected Behavior

- All status transitions with a corresponding email template trigger that email when `NotifyUser = true`.
- Every notification email includes the board's reason/notes (when present).
- Draft-edit notification emails include a human-readable list of what changed on the registration.
- When the admin saves changes with `NotifyUser = false` (registration goes to Draft but family is not notified), the UI displays a visible warning banner on the registration detail page.
- The admin sees explicit feedback (toast) if the email could not be delivered.

---

## Scope

### Out of scope

- Changing email copy beyond the additions described here.
- New email templates for `Cancelled`, `Draft` or `FullyPaid` (auto-assigned / separate endpoint).

### In scope

1. Add `SendRegistrationRevertedToPendingAsync` email template and wire it into `ChangeStatusAsync`.
2. Add `BoardNotes` (optional) to `RegistrationStatusEmailData`; render it in all status-change templates: `SendRegistrationPartiallyPaidAsync`, `SendRegistrationFinallyConfirmedAsync`, `SendRegistrationRevertedToPendingAsync`.
3. Extend `DraftChangesEmailData` (new dedicated DTO) with `BoardNotes` and `ChangeSummary: IReadOnlyList<string>`. Render both in `SendDraftChangesNotificationAsync`.
4. Compute `ChangeSummary` inside `AdminUpdateAsync` by diffing old vs new members and extras before the delete-and-rebuild.
5. Surface email-send errors to the admin (warning toast).
6. In `RegistrationDetailPage.vue`, show an orange warning banner when `HasPendingUserAcknowledgement = true` and `FamilyNotifiedOfDraft = false` (new field).
7. In `AdminStatusChangeDialog.vue`, hide/disable "Notificar a la familia" when the target status has no email template.

---

## Affected Files

### Backend

| File | Change |
|---|---|
| `src/Abuvi.API/Common/Services/IEmailService.cs` | (1) Add `BoardNotes` to `RegistrationStatusEmailData`. (2) Add `DraftChangesEmailData` record with `BoardNotes` and `ChangeSummary`. (3) Update `SendDraftChangesNotificationAsync` signature. (4) Add `SendRegistrationRevertedToPendingAsync`. |
| `src/Abuvi.API/Common/Services/ResendEmailService.cs` | (1) Render `BoardNotes` block in `SendRegistrationPartiallyPaidAsync`, `SendRegistrationFinallyConfirmedAsync`, `SendRegistrationRevertedToPendingAsync`. (2) Render `ChangeSummary` list and `BoardNotes` in `SendDraftChangesNotificationAsync`. (3) Implement `SendRegistrationRevertedToPendingAsync`. |
| `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` | Add `FamilyNotifiedOfDraft` bool to `Registration` entity (default `false`). Include it in `RegistrationResponse`. |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | (1) In `ChangeStatusAsync`: map `Pending` → new template; pass `request.Notes` as `BoardNotes`; set `registration.FamilyNotifiedOfDraft = true` when email sent. (2) In `AdminUpdateAsync`: diff old/new members & extras to build `ChangeSummary`; pass `request.Notes` and `ChangeSummary` into `DraftChangesEmailData`; set `FamilyNotifiedOfDraft` accordingly. |
| `src/Abuvi.API/Data/Migrations/` | Add migration for `family_notified_of_draft` column on `registrations`. |

### Frontend

| File | Change |
|---|---|
| `frontend/src/types/registration.ts` | Add `familyNotifiedOfDraft: boolean` to `RegistrationResponse`. |
| `frontend/src/components/registrations/AdminStatusChangeDialog.vue` | Disable/hide the "Notificar a la familia" toggle when selected status has no email template; show helper text. |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Show orange warning banner when `registration.hasUnnotifiedDraftChanges` (computed: `status === 'Draft' && hasPendingUserAcknowledgement && !familyNotifiedOfDraft`). |
| `frontend/src/composables/useRegistrations.ts` | After `changeStatus` or `adminUpdateRegistration`, show a warning toast if the email was not sent (`emailSent: false` in response meta). |

---

## Data Model Changes

### `Registration` entity — new field

```csharp
// True when the family was notified via email of the current Draft changes
public bool FamilyNotifiedOfDraft { get; set; } = false;
```

Reset to `false` whenever the registration is put back into Draft (new admin edit). Set to `true` when `NotifyUser = true` AND the email is dispatched successfully.

### `RegistrationStatusEmailData` — updated

```csharp
public record RegistrationStatusEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public string? BoardNotes { get; init; }        // NEW — admin's reason/notes for the change
}
```

### `DraftChangesEmailData` — new DTO (replaces `RegistrationStatusEmailData` for draft notifications)

```csharp
public record DraftChangesEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public string? BoardNotes { get; init; }                    // NEW — admin's reason for the edit
    public IReadOnlyList<string> ChangeSummary { get; init; } = []; // NEW — human-readable diff lines
}
```

### `RegistrationResponse` — additive field

```csharp
bool FamilyNotifiedOfDraft   // whether the family received an email about current Draft state
```

---

## Change Summary Computation

Inside `AdminUpdateAsync`, before deleting existing members/extras, capture the current state. After rebuilding, compare and build the diff list.

### Members diff logic

```csharp
// Before deletion:
var oldMembers = registration.Members
    .Select(m => new { m.FamilyMemberId, m.FamilyMember.FirstName, m.FamilyMember.LastName, m.AttendancePeriod })
    .ToList();

// After rebuilding (newMembers list already built):
var oldMemberIds = oldMembers.Select(m => m.FamilyMemberId).ToHashSet();
var newMemberIds = newMembers.Select(m => m.FamilyMemberId).ToHashSet();

foreach (var removed in oldMembers.Where(m => !newMemberIds.Contains(m.FamilyMemberId)))
    changeSummary.Add($"Eliminado: {removed.FirstName} {removed.LastName}");

foreach (var added in newMembers.Where(m => !oldMemberIds.Contains(m.FamilyMemberId)))
{
    var member = /* already resolved familyMember from loop above */;
    changeSummary.Add($"Añadido: {member.FirstName} {member.LastName}");
}
```

### Extras diff logic

```csharp
// Before deletion: registration.RegistrationExtras (loaded with details)
var oldExtras = registration.RegistrationExtras
    .Select(e => new { e.CampEditionExtraId, Name = e.CampEditionExtra.Name, e.Quantity, e.TotalAmount })
    .ToList();

// After rebuilding:
var oldExtraIds = oldExtras.Select(e => e.CampEditionExtraId).ToHashSet();
var newExtraIds = newExtras.Select(e => e.CampEditionExtraId).ToHashSet();

foreach (var removed in oldExtras.Where(e => !newExtraIds.Contains(e.CampEditionExtraId)))
    changeSummary.Add($"Eliminado extra: {removed.Name}");

foreach (var added in newExtras.Where(e => !oldExtraIds.Contains(e.CampEditionExtraId)))
{
    var name = /* campExtra.Name from already-resolved loop */;
    changeSummary.Add($"Añadido extra: {name} ×{added.Quantity}");
}

// Quantity changes on existing extras:
foreach (var kept in oldExtras.Where(e => newExtraIds.Contains(e.CampEditionExtraId)))
{
    var updated = newExtras.First(e => e.CampEditionExtraId == kept.CampEditionExtraId);
    if (updated.Quantity != kept.Quantity)
        changeSummary.Add($"Extra {kept.Name}: cantidad {kept.Quantity} → {updated.Quantity}");
}
```

### Amount change

```csharp
// After recalculating totals:
if (registration.BaseTotalAmount != oldBaseTotalAmount)
    changeSummary.Add(
        $"Importe actualizado: {oldBaseTotalAmount:0.##}€ → {registration.TotalAmount:0.##}€");
```

---

## Email Templates (HTML snippets to add)

### Board notes block (all status-change emails)

Rendered only when `BoardNotes` is non-null and non-empty:

```html
<div style='background:#f8fafc;border-left:3px solid #2563eb;padding:10px 14px;margin:16px 0;border-radius:0 4px 4px 0;'>
  <p style='margin:0;font-size:13px;color:#555;'>
    <strong>Nota de la junta:</strong> {BoardNotes}
  </p>
</div>
```

### Change summary block (`SendDraftChangesNotificationAsync`)

Rendered only when `ChangeSummary.Count > 0`:

```html
<div style='background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;padding:12px 16px;margin:16px 0;'>
  <p style='margin:0 0 8px;font-size:13px;font-weight:600;color:#374151;'>Cambios realizados:</p>
  <ul style='margin:0;padding-left:20px;font-size:13px;color:#555;'>
    {foreach change} <li>{change}</li> {/foreach}
  </ul>
</div>
```

---

## Statuses with email templates (after fix)

| New status | Email method | Notes rendered |
| --- | --- | --- |
| `PartiallyPaid` | `SendRegistrationPartiallyPaidAsync` | ✅ |
| `Confirmed` | `SendRegistrationFinallyConfirmedAsync` | ✅ |
| `Pending` (revert) | `SendRegistrationRevertedToPendingAsync` 🆕 | ✅ |
| Draft (admin edit) | `SendDraftChangesNotificationAsync` | ✅ + change summary 🆕 |

Statuses with no admin status-change email (toggle hidden in dialog):

- `Draft`, `FullyPaid`, `Cancelled` — blocked in `ChangeStatusAsync`

---

## Email Template: Reverted to Pending

**Subject:** `Tu inscripción está pendiente de revisión — {CampName}`

**Body:**

> Hola, {RecipientFirstName}:
>
> Queremos informarte de que tu inscripción al campamento **{CampName}** ha vuelto a estado **pendiente**.
>
> {Board notes block if present}
>
> La junta revisará tu inscripción próximamente. Te avisaremos de cualquier novedad.
>
> Si tienes alguna pregunta, no dudes en ponerte en contacto con nosotros.
>
> Saludos cordiales, El equipo de Abuvi

---

## Frontend: Unnotified Changes Warning

In `RegistrationDetailPage.vue`, add a computed property:

```ts
const hasUnnotifiedDraftChanges = computed(
  () =>
    registration.value?.status === 'Draft' &&
    registration.value.hasPendingUserAcknowledgement &&
    !registration.value.familyNotifiedOfDraft
)
```

Render a banner (admin/board only, below the status badge):

```html
<Message
  v-if="isAdminOrBoard && hasUnnotifiedDraftChanges"
  severity="warn"
  :closable="false"
  class="mb-4"
>
  La inscripción tiene cambios pendientes de revisión, pero la familia
  <strong>no ha sido notificada</strong> por correo. Usa el botón
  "Notificar a la familia" para enviar el aviso.
</Message>
```

Pair with a standalone "Notificar a la familia" button that triggers a re-send of `SendDraftChangesNotificationAsync` (new endpoint: `POST /api/registrations/{id}/notify-draft`) so the admin can notify without re-editing.

---

## API — New Endpoint (optional but recommended)

```
POST /api/registrations/{id:guid}/notify-draft
```

Sends `SendDraftChangesNotificationAsync` for a registration that is in `Draft` with `FamilyNotifiedOfDraft = false`. Sets `FamilyNotifiedOfDraft = true` on success. Returns `204 No Content`.

Requires admin/board role. Throws `BusinessRuleException` if registration is not in `Draft` or family is already notified.

---

## API Contract Changes (response)

### `ChangeRegistrationStatusRequest` — unchanged

### `RegistrationResponse` — additive

```json
{
  "familyNotifiedOfDraft": false
}
```

---

## Implementation Steps (TDD)

1. **Backend — data model**
   - Add `FamilyNotifiedOfDraft` bool to `Registration` entity, default `false`.
   - Create migration.
   - Update `RegistrationResponse` and `ToResponse()` mapping.

2. **Backend — email service**
   - Add `BoardNotes` to `RegistrationStatusEmailData`.
   - Create `DraftChangesEmailData` with `BoardNotes` + `ChangeSummary`.
   - Update `IEmailService.SendDraftChangesNotificationAsync` signature to accept `DraftChangesEmailData`.
   - Add `IEmailService.SendRegistrationRevertedToPendingAsync`.
   - Implement both in `ResendEmailService` with the HTML snippets above.
   - Update existing templates (`PartiallyPaid`, `Confirmed`) to render `BoardNotes` block.

3. **Backend — service layer (tests first)**
   - Test: `ChangeStatusAsync` with `Pending` + `NotifyUser: true` → calls `SendRegistrationRevertedToPendingAsync` with `BoardNotes = request.Notes`.
   - Test: `ChangeStatusAsync` with `PartiallyPaid` + notes → email data has `BoardNotes` set.
   - Test: `AdminUpdateAsync` member removal → `ChangeSummary` contains "Eliminado: …".
   - Test: `AdminUpdateAsync` extra addition → `ChangeSummary` contains "Añadido extra: …".
   - Test: `AdminUpdateAsync` with `NotifyUser: false` → `FamilyNotifiedOfDraft = false`.
   - Test: `AdminUpdateAsync` with `NotifyUser: true` + email succeeds → `FamilyNotifiedOfDraft = true`.
   - Implement diff logic and wire into both `ChangeStatusAsync` and `AdminUpdateAsync`.

4. **Backend — new notify-draft endpoint**
   - Map `POST /api/registrations/{id:guid}/notify-draft` → new service method `NotifyDraftAsync`.
   - Method: guard status == Draft, send `SendDraftChangesNotificationAsync`, set `FamilyNotifiedOfDraft = true`.

5. **Frontend — types**
   - Add `familyNotifiedOfDraft: boolean` to `RegistrationResponse`.

6. **Frontend — `AdminStatusChangeDialog.vue`**
   - Define `STATUSES_WITH_EMAIL: RegistrationStatus[] = ['PartiallyPaid', 'Confirmed', 'Pending']`.
   - Disable toggle + show helper text when `!STATUSES_WITH_EMAIL.includes(selectedStatus)`.

7. **Frontend — `RegistrationDetailPage.vue`**
   - Add `hasUnnotifiedDraftChanges` computed.
   - Render `<Message>` warning banner + "Notificar a la familia" action button.

8. **Frontend — composable**
   - After `changeStatus`: if API meta returns `emailSent: false`, show warn toast.
   - Wire `notifyDraft(registrationId)` composable method calling the new endpoint.

---

## Non-functional Requirements

- **No breaking change**: all new fields are additive.
- **Error isolation**: email failures must never roll back the status change or edit.
- **Logging**: every email attempt logs `RegistrationId`, target status or "draft", `ToEmail`, and Resend message ID (success) or exception (failure).
- **Security**: `notify-draft` endpoint requires admin/board role (same as other admin registration endpoints).
- **Test coverage**: all new service-layer paths covered by unit tests; `ResendEmailService` methods covered by existing pattern (mock `IResendClient`).

---

## Acceptance Criteria

- [ ] Changing status to `Pending` with `NotifyUser: true` sends the "pendiente de revisión" email including `BoardNotes`.
- [ ] Changing status to `PartiallyPaid` or `Confirmed` includes `BoardNotes` in the email body.
- [ ] Admin-editing a registration with `NotifyUser: true` sends the draft-changes email with `BoardNotes` and a list of changed items.
- [ ] Admin-editing a registration with `NotifyUser: false` sets `FamilyNotifiedOfDraft = false`; the UI warning banner is visible to admin/board.
- [ ] After email send failure, the status change still persists and the admin sees a warning toast.
- [ ] The "Notificar a la familia" toggle in `AdminStatusChangeDialog` is disabled when the target status has no email template.
- [ ] The standalone "Notificar a la familia" button in the detail page triggers the notify-draft endpoint and dismisses the warning banner.
- [ ] All new unit tests pass; no regressions in existing tests.

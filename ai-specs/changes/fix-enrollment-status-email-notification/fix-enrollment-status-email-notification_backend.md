# Backend Implementation Plan: fix-enrollment-status-email-notification

## Overview

Fix several gaps in the registration status change / admin-edit email notification flow:

1. Add a missing email for the `Pending` revert status.
2. Include the board's notes/reason in every status-change email body.
3. Compute and include a human-readable change summary in the draft-edit notification email.
4. Track whether the family was notified when a registration was put in Draft (`FamilyNotifiedOfDraft`).
5. Add a `POST /api/registrations/{id}/notify-draft` endpoint so admins can send a notification without re-editing.
6. Surface email-send failures via a non-fatal warning in the API response.

Architecture: **Vertical Slice Architecture** — all changes stay inside `Features/Registrations/` and `Common/Services/`. No new feature slices are created.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Registrations/`

**Files to modify:**

- `RegistrationsModels.cs` — entity field, response DTO, email data DTO, mapping
- `RegistrationsService.cs` — `ChangeStatusAsync`, `AdminUpdateAsync`, new `NotifyDraftAsync`
- `RegistrationsEndpoints.cs` — new endpoint registration
- `Data/Configurations/RegistrationConfiguration.cs` — EF Core column config
- `Data/AbuviDbContext.cs` — no change (auto-discovered by `ApplyConfigurationsFromAssembly`)

**Cross-cutting (Common/Services):**

- `IEmailService.cs` — new DTO, updated signatures, new method
- `ResendEmailService.cs` — new method implementations, updated existing templates

**Tests:**

- `Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceStatusTests.cs` — add cases for `Pending` email, `BoardNotes`, and `FamilyNotifiedOfDraft`
- `Abuvi.Tests/Unit/Features/Registrations/AdminRegistrationServiceTests.cs` — add cases for diff computation and `FamilyNotifiedOfDraft`
- New file: `Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceNotifyDraftTests.cs`

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/fix-enrollment-status-email-notification-backend`
- **Base branch**: `dev`
- **Implementation steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/fix-enrollment-status-email-notification-backend`
  3. `git branch` — verify active branch

---

### Step 1: Add `FamilyNotifiedOfDraft` to the `Registration` Entity

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add the new bool property to the `Registration` class after `HasPendingUserAcknowledgement`.

```csharp
public bool FamilyNotifiedOfDraft { get; set; } = false;
```

- **Placement**: After line with `public bool HasPendingUserAcknowledgement { get; set; } = false;`
- **Notes**:
  - Reset to `false` whenever the registration is put in `Draft` (both on first edit and on subsequent re-edits).
  - Set to `true` only after a notification email is successfully dispatched.

---

### Step 2: Update EF Core Configuration

- **File**: `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs`
- **Action**: Add column mapping for the new field, after the `HasPendingUserAcknowledgement` property configuration.

```csharp
builder.Property(r => r.FamilyNotifiedOfDraft)
    .HasDefaultValue(false)
    .HasColumnName("family_notified_of_draft");
```

---

### Step 3: Create EF Core Migration

- **Action**: Generate and review the migration.
- **Command** (run from the repo root):

```bash
dotnet ef migrations add AddFamilyNotifiedOfDraftToRegistrations --project src/Abuvi.API
```

- **Review**: Confirm the generated migration adds column `family_notified_of_draft boolean NOT NULL DEFAULT false` to the `registrations` table.
- **Apply locally**:

```bash
dotnet ef database update --project src/Abuvi.API
```

---

### Step 4: Update `RegistrationStatusEmailData` and Add `DraftChangesEmailData`

- **File**: `src/Abuvi.API/Common/Services/IEmailService.cs`
- **Action (a)**: Add `BoardNotes` to the existing `RegistrationStatusEmailData` record:

```csharp
public record RegistrationStatusEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public string? BoardNotes { get; init; }   // admin's reason/notes for the change
}
```

- **Action (b)**: Add new `DraftChangesEmailData` record after `RegistrationStatusEmailData`:

```csharp
public record DraftChangesEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public string? BoardNotes { get; init; }
    public IReadOnlyList<string> ChangeSummary { get; init; } = [];
}
```

- **Action (c)**: Update `IEmailService` interface:
  - Change `SendDraftChangesNotificationAsync` signature to accept `DraftChangesEmailData` (instead of `RegistrationStatusEmailData`).
  - Add `SendRegistrationRevertedToPendingAsync`:

```csharp
/// <summary>Sends "Tu inscripción está pendiente de revisión" when board reverts to Pending</summary>
Task SendRegistrationRevertedToPendingAsync(
    RegistrationStatusEmailData data,
    CancellationToken ct);

/// <summary>Sends "Hay cambios en tu inscripción que revisar" with change summary and board notes</summary>
Task SendDraftChangesNotificationAsync(
    DraftChangesEmailData data,
    CancellationToken ct);
```

---

### Step 5: Implement New and Updated Email Templates in `ResendEmailService`

- **File**: `src/Abuvi.API/Common/Services/ResendEmailService.cs`

#### 5a — Shared helper: `BuildBoardNotesHtml`

Add a private static helper to avoid duplicating the notes block HTML:

```csharp
private static string BuildBoardNotesHtml(string? boardNotes)
{
    if (string.IsNullOrWhiteSpace(boardNotes)) return string.Empty;
    return $@"
        <div style='background:#f8fafc;border-left:3px solid #2563eb;padding:10px 14px;margin:16px 0;border-radius:0 4px 4px 0;'>
          <p style='margin:0;font-size:13px;color:#555;'>
            <strong>Nota de la junta:</strong> {boardNotes}
          </p>
        </div>";
}
```

#### 5b — Update `SendRegistrationPartiallyPaidAsync`

Insert `{BuildBoardNotesHtml(data.BoardNotes)}` in the HTML body, after the main message paragraph and before the closing remarks. Follow the exact indentation of the existing template.

#### 5c — Update `SendRegistrationFinallyConfirmedAsync`

Same: insert `{BuildBoardNotesHtml(data.BoardNotes)}` after the main paragraph.

#### 5d — Implement `SendRegistrationRevertedToPendingAsync`

Follow the exact same pattern as `SendRegistrationPartiallyPaidAsync` (IsTestAddress guard, `EmailMessage` build, try/catch that logs and throws `InvalidOperationException`):

```csharp
public async Task SendRegistrationRevertedToPendingAsync(
    RegistrationStatusEmailData data,
    CancellationToken ct)
{
    if (IsTestAddress(data.ToEmail))
    {
        _logger.LogDebug("Skipping email to test address {Email}", data.ToEmail);
        return;
    }

    var message = new EmailMessage
    {
        From = $"{_fromName} <{_fromEmail}>",
        To = data.ToEmail,
        Bcc = [_boardBccEmail],
        Subject = $"Tu inscripción está pendiente de revisión — {data.CampName}",
        HtmlBody = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #2563eb;'>¡Hola, {data.RecipientFirstName}!</h2>
                    <p>Queremos informarte de que tu inscripción al campamento <strong>{data.CampName}</strong> ha vuelto a estado <strong>pendiente</strong>.</p>
                    {BuildBoardNotesHtml(data.BoardNotes)}
                    <p>La junta revisará tu inscripción próximamente y te mantendremos informado de cualquier novedad.</p>
                    <p style='color: #666; font-size: 14px;'>Si tienes alguna pregunta, no dudes en ponerte en contacto con nosotros.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                    <p style='color: #999; font-size: 12px;'>
                        Saludos cordiales,<br>
                        El equipo de Abuvi
                    </p>
                </div>
            </body>
            </html>
        "
    };

    try
    {
        var messageId = await _resend.SendEmailAsync(message);
        _logger.LogInformation(
            "RevertedToPending notification sent to {Email} for registration {RegistrationId}, Resend ID: {MessageId}",
            data.ToEmail, data.RegistrationId, messageId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send RevertedToPending notification to {Email}", data.ToEmail);
        throw new InvalidOperationException($"Failed to send RevertedToPending notification: {ex.Message}", ex);
    }
}
```

#### 5e — Update `SendDraftChangesNotificationAsync`

Change the parameter type from `RegistrationStatusEmailData` to `DraftChangesEmailData`. Add helpers for the change summary block and the board notes block:

```csharp
public async Task SendDraftChangesNotificationAsync(
    DraftChangesEmailData data,
    CancellationToken ct)
{
    if (IsTestAddress(data.ToEmail))
    {
        _logger.LogDebug("Skipping email to test address {Email}", data.ToEmail);
        return;
    }

    var registrationUrl = $"{_frontendUrl}/registrations/{data.RegistrationId}";

    var changeSummaryHtml = data.ChangeSummary.Count > 0
        ? $@"<div style='background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;padding:12px 16px;margin:16px 0;'>
              <p style='margin:0 0 8px;font-size:13px;font-weight:600;color:#374151;'>Cambios realizados:</p>
              <ul style='margin:0;padding-left:20px;font-size:13px;color:#555;'>
                {string.Join("", data.ChangeSummary.Select(c => $"<li>{c}</li>"))}
              </ul>
            </div>"
        : string.Empty;

    var message = new EmailMessage
    {
        From = $"{_fromName} <{_fromEmail}>",
        To = data.ToEmail,
        Bcc = [_boardBccEmail],
        Subject = $"Hay cambios en tu inscripción que revisar — {data.CampName}",
        HtmlBody = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #2563eb;'>¡Hola, {data.RecipientFirstName}!</h2>
                    <p>La junta ha realizado cambios en tu inscripción al campamento <strong>{data.CampName}</strong>.</p>
                    {BuildBoardNotesHtml(data.BoardNotes)}
                    {changeSummaryHtml}
                    <p>Por favor, accede a tu área de usuario, revisa los cambios y confírmalos para que tu inscripción quede al día.</p>
                    <p style='margin: 30px 0;'>
                        <a href=""{registrationUrl}""
                           style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                            Ver mi inscripción
                        </a>
                    </p>
                    <p style='color: #666; font-size: 14px;'>Si tienes alguna pregunta, no dudes en ponerte en contacto con nosotros.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                    <p style='color: #999; font-size: 12px;'>
                        Saludos cordiales,<br>
                        El equipo de Abuvi
                    </p>
                </div>
            </body>
            </html>
        "
    };

    try
    {
        var messageId = await _resend.SendEmailAsync(message);
        _logger.LogInformation(
            "DraftChangesNotification sent to {Email} for registration {RegistrationId}, Resend ID: {MessageId}",
            data.ToEmail, data.RegistrationId, messageId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send DraftChangesNotification to {Email}", data.ToEmail);
        throw new InvalidOperationException($"Failed to send DraftChangesNotification: {ex.Message}", ex);
    }
}
```

---

### Step 6: Update `RegistrationResponse` and `ToResponse` Mapping

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

#### 6a — Add `FamilyNotifiedOfDraft` to `RegistrationResponse`

The record currently ends with `List<FriendLinkResponse>? FriendLinks`. Add the new field in the existing positional record or after `HasPendingUserAcknowledgement`:

Add to the `RegistrationResponse` record (positional parameter after `HasPendingUserAcknowledgement`):

```csharp
bool FamilyNotifiedOfDraft,
```

#### 6b — Update `ToResponse` mapping in `RegistrationMappingExtensions`

After the `r.HasPendingUserAcknowledgement` line, add:

```csharp
r.FamilyNotifiedOfDraft,
```

Ensure the order of positional parameters in `RegistrationResponse` matches the constructor call in `ToResponse`.

---

### Step 7: Update `RegistrationsService.ChangeStatusAsync`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Method**: `ChangeStatusAsync` (currently around line 1112)

#### 7a — Reset `FamilyNotifiedOfDraft` before update

Immediately after `registration.Status = request.NewStatus;` (line ~1145) and before `await registrationsRepo.UpdateAsync(...)`, reset the field:

```csharp
registration.FamilyNotifiedOfDraft = false;
```

This ensures that any future Draft state is correctly tracked even if the new status is not Draft (it costs nothing for non-Draft transitions and is safe).

Actually, `FamilyNotifiedOfDraft` only matters when status == Draft. But since `ChangeStatusAsync` blocks Draft as a target status, we do NOT need to reset it here. **Skip** the reset in this method.

#### 7b — Add `Pending` to the email switch and pass `BoardNotes`

Replace the switch expression (lines ~1173-1180):

```csharp
Task emailTask = request.NewStatus switch
{
    RegistrationStatus.PartiallyPaid =>
        emailService.SendRegistrationPartiallyPaidAsync(
            new RegistrationStatusEmailData
            {
                ToEmail = emailData.ToEmail,
                RecipientFirstName = emailData.RecipientFirstName,
                CampName = emailData.CampName,
                RegistrationId = emailData.RegistrationId,
                BoardNotes = request.Notes
            }, ct),
    RegistrationStatus.Confirmed =>
        emailService.SendRegistrationFinallyConfirmedAsync(
            new RegistrationStatusEmailData
            {
                ToEmail = emailData.ToEmail,
                RecipientFirstName = emailData.RecipientFirstName,
                CampName = emailData.CampName,
                RegistrationId = emailData.RegistrationId,
                BoardNotes = request.Notes
            }, ct),
    RegistrationStatus.Pending =>
        emailService.SendRegistrationRevertedToPendingAsync(
            new RegistrationStatusEmailData
            {
                ToEmail = emailData.ToEmail,
                RecipientFirstName = emailData.RecipientFirstName,
                CampName = emailData.CampName,
                RegistrationId = emailData.RegistrationId,
                BoardNotes = request.Notes
            }, ct),
    _ => Task.CompletedTask
};
```

- **Note**: Since all three cases share the same `emailData` shape plus `BoardNotes`, simplify by building one `RegistrationStatusEmailData` at the top of the `if (request.NotifyUser)` block with `BoardNotes = request.Notes` and reference it in all three arms.

#### 7c — Rebuild `emailData` with `BoardNotes`

Replace the `emailData` local variable declaration (currently around line 1165):

```csharp
var emailData = new RegistrationStatusEmailData
{
    ToEmail = registration.RegisteredByUser.Email,
    RecipientFirstName = registration.RegisteredByUser.FirstName,
    CampName = registration.CampEdition.Camp.Name,
    RegistrationId = registration.Id,
    BoardNotes = request.Notes
};
```

---

### Step 8: Update `RegistrationsService.AdminUpdateAsync` — Change Summary and `FamilyNotifiedOfDraft`

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Method**: `AdminUpdateAsync` (currently around line 771)

#### 8a — Capture old members before deletion

After the old values capture block (lines ~783-785), add:

```csharp
// Capture old state for change summary before any mutations
var oldMembers = registration.Members
    .Select(m => new
    {
        m.FamilyMemberId,
        FirstName = m.FamilyMember.FirstName,
        LastName = m.FamilyMember.LastName,
        m.AttendancePeriod
    })
    .ToList();

var oldExtras = registration.Extras
    .Select(e => new
    {
        e.CampEditionExtraId,
        Name = e.CampEditionExtra.Name,
        e.Quantity
    })
    .ToList();

var changeSummary = new List<string>();
```

#### 8b — Build members diff after rebuilding members

After `await registrationsRepo.AddMembersAsync(newMembers, ct);` and the amount recalculations (inside `if (request.Members != null)`), add:

```csharp
// Members diff
var oldMemberIds = oldMembers.Select(m => m.FamilyMemberId).ToHashSet();
var newMemberIds = newMembers.Select(m => m.FamilyMemberId).ToHashSet();

foreach (var removed in oldMembers.Where(m => !newMemberIds.Contains(m.FamilyMemberId)))
    changeSummary.Add($"Eliminado: {removed.FirstName} {removed.LastName}");

// newMembers already has FamilyMemberId; resolve names from familyUnitsRepo cache
// (familyMember was already loaded during rebuild — capture names in the rebuild loop)
```

**Important implementation note**: To get the first/last name of newly added members, the names must be captured during the rebuild loop where `familyMember` is already in scope. Add a local list during the rebuild:

```csharp
var newMemberDetails = new List<(Guid FamilyMemberId, string FirstName, string LastName)>();
// Inside the foreach (var memberReq in request.Members) loop, after familyMember is resolved:
newMemberDetails.Add((familyMember.Id, familyMember.FirstName, familyMember.LastName));
```

Then after the loop:

```csharp
foreach (var added in newMemberDetails.Where(m => !oldMemberIds.Contains(m.FamilyMemberId)))
    changeSummary.Add($"Añadido: {added.FirstName} {added.LastName}");
```

#### 8c — Build extras diff after rebuilding extras

Inside `if (request.Extras != null)`, after `await extrasRepo.AddRangeAsync(newExtras, ct);`, add:

```csharp
// Extras diff
var oldExtraIds = oldExtras.Select(e => e.CampEditionExtraId).ToHashSet();
var newExtraIds = newExtras.Select(e => e.CampEditionExtraId).ToHashSet();

// Capture name from campExtra (already resolved in rebuild loop below)
// Add newExtraDetails list in the rebuild loop similarly to members
```

Capture in the extras rebuild loop (`foreach (var extraReq in request.Extras)`):

```csharp
var newExtraDetails = new List<(Guid CampEditionExtraId, string Name, int Quantity)>();
// Inside loop, after campExtra is resolved:
newExtraDetails.Add((campExtra.Id, campExtra.Name, extraReq.Quantity));
```

Then after the loop:

```csharp
foreach (var removed in oldExtras.Where(e => !newExtraIds.Contains(e.CampEditionExtraId)))
    changeSummary.Add($"Eliminado extra: {removed.Name}");

foreach (var added in newExtraDetails.Where(e => !oldExtraIds.Contains(e.CampEditionExtraId)))
    changeSummary.Add($"Añadido extra: {added.Name} ×{added.Quantity}");

foreach (var kept in oldExtras.Where(e => newExtraIds.Contains(e.CampEditionExtraId)))
{
    var updated = newExtraDetails.First(e => e.CampEditionExtraId == kept.CampEditionExtraId);
    if (updated.Quantity != kept.Quantity)
        changeSummary.Add($"Extra {kept.Name}: cantidad {kept.Quantity} → {updated.Quantity}");
}
```

#### 8d — Amount change diff

After both members and extras blocks (both amounts are final at this point), before the Draft status assignment:

```csharp
var newTotalAmount = registration.BaseTotalAmount + registration.ExtrasAmount;
if (newTotalAmount != registration.TotalAmount && request.Members != null || request.Extras != null)
{
    var oldTotal = oldBaseTotalAmount + registration.ExtrasAmount; // approximate
}
```

Actually, total amount tracking is cleaner if done by comparing `oldBaseTotalAmount` captured at the start with the new `registration.TotalAmount` at the end:

```csharp
// After all mutations and before UpdateAsync
var newTotalAmount = registration.BaseTotalAmount + registration.ExtrasAmount;
var oldTotalAmount = oldBaseTotalAmount + (request.Extras != null
    ? oldExtras.Sum(e => e.Quantity * /* we don't have old UnitPrice easily */)
    : registration.ExtrasAmount);
```

**Simpler approach**: Only track member amount change, since extras are also visible in the extras diff:

```csharp
if (request.Members != null && registration.BaseTotalAmount != oldBaseTotalAmount)
    changeSummary.Add(
        $"Importe de miembros: {oldBaseTotalAmount:0.##}€ → {registration.BaseTotalAmount:0.##}€");
```

This is cleaner and avoids needing old extras amounts (which aren't captured).

#### 8e — Reset `FamilyNotifiedOfDraft` and set on success

In the Draft assignment block (around line 894):

```csharp
registration.Status = RegistrationStatus.Draft;
registration.DraftTargetStatus = request.DraftTargetStatus ?? previousStatus;
registration.HasPendingUserAcknowledgement = true;
registration.AdminModifiedAt = DateTime.UtcNow;
registration.FamilyNotifiedOfDraft = false;   // Always reset when entering Draft
```

In the notification block (around line 927), replace with:

```csharp
if (request.NotifyUser)
{
    try
    {
        await emailService.SendDraftChangesNotificationAsync(new DraftChangesEmailData
        {
            ToEmail = updated.RegisteredByUser.Email,
            RecipientFirstName = updated.RegisteredByUser.FirstName,
            CampName = updated.CampEdition.Camp.Name,
            RegistrationId = updated.Id,
            BoardNotes = request.Notes,
            ChangeSummary = changeSummary
        }, ct);

        // Mark family as notified on success
        updated.FamilyNotifiedOfDraft = true;
        await registrationsRepo.UpdateAsync(updated, ct);
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Failed to send draft notification email for registration {RegistrationId}",
            registrationId);
        // FamilyNotifiedOfDraft stays false — frontend banner will surface this
    }
}
```

---

### Step 9: Add `NotifyDraftAsync` Service Method

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Interface**: Add to `IRegistrationsService` (if one exists) or keep as a method on `RegistrationsService` directly.
- **Action**: Add the method after `ChangeStatusAsync`:

```csharp
public async Task NotifyDraftAsync(
    Guid registrationId, CancellationToken ct)
{
    var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    if (registration.Status != RegistrationStatus.Draft)
        throw new BusinessRuleException(
            "Solo se puede notificar a la familia cuando la inscripción está en estado En revisión.");

    if (registration.FamilyNotifiedOfDraft)
        throw new BusinessRuleException(
            "La familia ya ha sido notificada de los cambios en esta inscripción.");

    await emailService.SendDraftChangesNotificationAsync(new DraftChangesEmailData
    {
        ToEmail = registration.RegisteredByUser.Email,
        RecipientFirstName = registration.RegisteredByUser.FirstName,
        CampName = registration.CampEdition.Camp.Name,
        RegistrationId = registration.Id,
        BoardNotes = null,
        ChangeSummary = []
    }, ct);

    registration.FamilyNotifiedOfDraft = true;
    await registrationsRepo.UpdateAsync(registration, ct);

    logger.LogInformation(
        "Draft notification sent to family for registration {RegistrationId}",
        registrationId);
}
```

---

### Step 10: Add `POST /api/registrations/{id}/notify-draft` Endpoint

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

#### 10a — Register the endpoint

In the `MapRegistrationsEndpoints` method, alongside the other admin-only PATCH/POST endpoints:

```csharp
group.MapPost("/{id:guid}/notify-draft", NotifyDraft)
    .WithName("NotifyDraft")
    .RequireAuthorization("BoardOrAdmin")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status422UnprocessableEntity);
```

#### 10b — Handler

```csharp
internal static async Task<IResult> NotifyDraft(
    Guid id,
    RegistrationsService service,
    CancellationToken ct)
{
    await service.NotifyDraftAsync(id, ct);
    return Results.NoContent();
}
```

- **Auth**: Requires `BoardOrAdmin` policy (same as other admin registration endpoints).
- **Returns**: `204 No Content` on success; `404` if registration not found (via global middleware); `422` if business rule violated.

---

### Step 11: Write Unit Tests — Status Change Email

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceStatusTests.cs`
- **Action**: Add the following new tests to the existing class:

```csharp
[Fact]
public async Task ChangeStatusAsync_PendingTarget_WithNotifyUser_SendsRevertedToPendingEmail()
{
    // Arrange
    var registration = BuildRegistration(RegistrationStatus.PartiallyPaid);
    _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
        .Returns(registration, BuildRegistration(RegistrationStatus.Pending));

    var request = new ChangeRegistrationStatusRequest(RegistrationStatus.Pending, "Motivo de prueba", NotifyUser: true);

    // Act
    await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

    // Assert
    await _emailService.Received(1).SendRegistrationRevertedToPendingAsync(
        Arg.Is<RegistrationStatusEmailData>(d =>
            d.BoardNotes == "Motivo de prueba" &&
            d.RegistrationId == RegistrationId),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task ChangeStatusAsync_PartiallyPaidTarget_WithNotes_PassesBoardNotesToEmail()
{
    // Arrange
    var registration = BuildRegistration(RegistrationStatus.Pending);
    _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
        .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

    var request = new ChangeRegistrationStatusRequest(RegistrationStatus.PartiallyPaid, "Primer plazo recibido");

    // Act
    await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

    // Assert
    await _emailService.Received(1).SendRegistrationPartiallyPaidAsync(
        Arg.Is<RegistrationStatusEmailData>(d => d.BoardNotes == "Primer plazo recibido"),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task ChangeStatusAsync_WithNotifyUser_False_DoesNotSendEmail()
{
    // Arrange
    var registration = BuildRegistration(RegistrationStatus.Pending);
    _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
        .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

    var request = new ChangeRegistrationStatusRequest(RegistrationStatus.PartiallyPaid, null, NotifyUser: false);

    // Act
    await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

    // Assert
    await _emailService.DidNotReceive().SendRegistrationPartiallyPaidAsync(
        Arg.Any<RegistrationStatusEmailData>(), Arg.Any<CancellationToken>());
}

[Fact]
public async Task ChangeStatusAsync_EmailThrows_DoesNotFailStatusChange()
{
    // Arrange
    var registration = BuildRegistration(RegistrationStatus.Pending);
    _repo.GetByIdWithDetailsAsync(RegistrationId, Arg.Any<CancellationToken>())
        .Returns(registration, BuildRegistration(RegistrationStatus.PartiallyPaid));

    _emailService.SendRegistrationPartiallyPaidAsync(
            Arg.Any<RegistrationStatusEmailData>(), Arg.Any<CancellationToken>())
        .ThrowsAsync(new InvalidOperationException("Resend down"));

    var request = new ChangeRegistrationStatusRequest(RegistrationStatus.PartiallyPaid, null);

    // Act
    var act = async () => await _sut.ChangeStatusAsync(RegistrationId, AdminUserId, request, CancellationToken.None);

    // Assert — status change must not throw even when email fails
    await act.Should().NotThrowAsync();
    await _repo.Received(1).UpdateAsync(
        Arg.Is<Registration>(r => r.Status == RegistrationStatus.PartiallyPaid),
        Arg.Any<CancellationToken>());
}
```

---

### Step 12: Write Unit Tests — Admin Edit Change Summary and `FamilyNotifiedOfDraft`

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/AdminRegistrationServiceTests.cs`
- **Action**: Add new tests:

```csharp
[Fact]
public async Task AdminUpdateAsync_MemberRemoved_ChangeSummaryContainsRemovedEntry()
{
    // Arrange: registration with one member, request removes that member
    // Assert: emailService received DraftChangesEmailData with ChangeSummary containing "Eliminado: ..."
}

[Fact]
public async Task AdminUpdateAsync_MemberAdded_ChangeSummaryContainsAddedEntry()
{
    // Assert: ChangeSummary contains "Añadido: ..."
}

[Fact]
public async Task AdminUpdateAsync_ExtraRemoved_ChangeSummaryContainsRemovedExtra()
{
    // Assert: ChangeSummary contains "Eliminado extra: ..."
}

[Fact]
public async Task AdminUpdateAsync_WithNotifyUser_True_EmailSucceeds_SetsFamilyNotifiedOfDraftTrue()
{
    // Assert: repo.UpdateAsync called with registration.FamilyNotifiedOfDraft == true
}

[Fact]
public async Task AdminUpdateAsync_WithNotifyUser_False_FamilyNotifiedOfDraftRemainsDefault()
{
    // Assert: FamilyNotifiedOfDraft == false on the registration passed to UpdateAsync
}

[Fact]
public async Task AdminUpdateAsync_WithNotifyUser_True_EmailFails_FamilyNotifiedOfDraftStaysFalse()
{
    // emailService.SendDraftChangesNotificationAsync throws
    // Assert: FamilyNotifiedOfDraft == false; operation does not throw
}

[Fact]
public async Task AdminUpdateAsync_AmountChanged_ChangeSummaryContainsAmountUpdate()
{
    // members change → base amount changes
    // Assert: ChangeSummary contains "Importe de miembros: X€ → Y€"
}
```

Use the existing test helpers from `AdminRegistrationServiceTests` (e.g., `CreateEdition()`, `CreateRegistration()`) as the base for the new test setup.

---

### Step 13: Write Unit Tests — `NotifyDraftAsync`

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceNotifyDraftTests.cs` (new file)

```csharp
public class RegistrationsServiceNotifyDraftTests
{
    private readonly IRegistrationsRepository _repo = Substitute.For<IRegistrationsRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly RegistrationsService _sut;

    public RegistrationsServiceNotifyDraftTests()
    {
        // Same constructor setup as RegistrationsServiceStatusTests
    }

    [Fact]
    public async Task NotifyDraftAsync_WhenDraftAndNotNotified_SendsEmail()
    {
        // Arrange: registration Status == Draft, FamilyNotifiedOfDraft == false
        // Act: NotifyDraftAsync
        // Assert: emailService received SendDraftChangesNotificationAsync; repo.UpdateAsync called with FamilyNotifiedOfDraft == true
    }

    [Fact]
    public async Task NotifyDraftAsync_WhenNotDraft_ThrowsBusinessRuleException()
    {
        // registration Status == Pending
        // Assert: throws BusinessRuleException
    }

    [Fact]
    public async Task NotifyDraftAsync_WhenAlreadyNotified_ThrowsBusinessRuleException()
    {
        // FamilyNotifiedOfDraft == true
        // Assert: throws BusinessRuleException
    }

    [Fact]
    public async Task NotifyDraftAsync_WhenRegistrationNotFound_ThrowsNotFoundException()
    {
        // repo returns null
        // Assert: throws NotFoundException
    }
}
```

---

### Step 14: Update Technical Documentation

- **File**: `ai-specs/specs/data-model.md`
  - Add `familyNotifiedOfDraft` to the `Registration` entity description (under the existing Draft-related fields).

- **File**: `ai-specs/specs/api-endpoints.md`
  - Add `POST /api/registrations/{id}/notify-draft` endpoint entry.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `FamilyNotifiedOfDraft` to `Registration` entity
3. Step 2 — Update EF Core configuration
4. Step 3 — Create and apply EF Core migration
5. Step 4 — Update `RegistrationStatusEmailData`, add `DraftChangesEmailData`, update `IEmailService`
6. Step 5 — Implement/update email templates in `ResendEmailService`
7. Step 6 — Add `FamilyNotifiedOfDraft` to `RegistrationResponse` and `ToResponse` mapping
8. Step 7 — Update `ChangeStatusAsync` (Pending case + `BoardNotes` in all arms)
9. Step 8 — Update `AdminUpdateAsync` (change summary + `FamilyNotifiedOfDraft`)
10. Step 9 — Add `NotifyDraftAsync` service method
11. Step 10 — Add `POST /{id}/notify-draft` endpoint
12. Step 11 — Unit tests for status change email
13. Step 12 — Unit tests for admin edit change summary / `FamilyNotifiedOfDraft`
14. Step 13 — Unit tests for `NotifyDraftAsync`
15. Step 14 — Documentation updates

---

## Testing Checklist

- [ ] `ChangeStatusAsync` + `Pending` target + `NotifyUser: true` → calls `SendRegistrationRevertedToPendingAsync` with `BoardNotes`
- [ ] `ChangeStatusAsync` + `PartiallyPaid` + notes → `SendRegistrationPartiallyPaidAsync` receives `BoardNotes`
- [ ] `ChangeStatusAsync` + `Confirmed` + notes → `SendRegistrationFinallyConfirmedAsync` receives `BoardNotes`
- [ ] `ChangeStatusAsync` + `NotifyUser: false` → no email called
- [ ] `ChangeStatusAsync` + email throws → status change still persists (no exception propagated)
- [ ] `AdminUpdateAsync` member removed → `ChangeSummary` contains "Eliminado: …"
- [ ] `AdminUpdateAsync` member added → `ChangeSummary` contains "Añadido: …"
- [ ] `AdminUpdateAsync` extra removed/added/quantity changed → `ChangeSummary` correct entries
- [ ] `AdminUpdateAsync` base amount changes → `ChangeSummary` contains amount diff line
- [ ] `AdminUpdateAsync` + `NotifyUser: true` + email succeeds → `FamilyNotifiedOfDraft = true`
- [ ] `AdminUpdateAsync` + `NotifyUser: false` → `FamilyNotifiedOfDraft = false`
- [ ] `AdminUpdateAsync` + `NotifyUser: true` + email throws → `FamilyNotifiedOfDraft = false`, no exception propagated
- [ ] `NotifyDraftAsync` when Draft + not notified → email sent, `FamilyNotifiedOfDraft = true`
- [ ] `NotifyDraftAsync` when not Draft → throws `BusinessRuleException`
- [ ] `NotifyDraftAsync` when already notified → throws `BusinessRuleException`
- [ ] `NotifyDraftAsync` when not found → throws `NotFoundException`
- [ ] `POST /api/registrations/{id}/notify-draft` returns `204` on success
- [ ] `POST /api/registrations/{id}/notify-draft` returns `404` when not found
- [ ] `POST /api/registrations/{id}/notify-draft` returns `422` when business rule violated
- [ ] `dotnet test` passes all tests
- [ ] Migration applied cleanly: column `family_notified_of_draft` exists in DB

---

## Error Response Format

All endpoints follow the standard `ApiResponse<T>` envelope:

```json
// 204 No Content — notify-draft success (no body)

// 422 — business rule violation
{
  "success": false,
  "data": null,
  "error": {
    "message": "La familia ya ha sido notificada de los cambios en esta inscripción.",
    "code": "BUSINESS_RULE_VIOLATION"
  }
}

// 404 — registration not found
{
  "success": false,
  "data": null,
  "error": {
    "message": "Inscripción with ID '…' was not found",
    "code": "NOT_FOUND"
  }
}
```

---

## Dependencies

No new NuGet packages required. All changes use existing dependencies:

- `Resend` (already registered)
- `IEmailService` / `ResendEmailService` (already in DI)
- `EF Core` (existing)

**Migration command:**

```bash
dotnet ef migrations add AddFamilyNotifiedOfDraftToRegistrations --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Notes

- **Language**: All user-facing strings in email bodies and `BusinessRuleException` messages must be in Spanish. All log messages and code (variables, method names, comments) in English.
- **Error isolation**: Email failures must never roll back the status change or admin edit. The `try/catch` around email dispatch stays in both `ChangeStatusAsync` and `AdminUpdateAsync`.
- **Additive API**: `RegistrationResponse` gains `FamilyNotifiedOfDraft: bool` — no breaking change.
- **`DraftChangesEmailData.ChangeSummary`**: When `NotifyDraftAsync` is called directly (not via `AdminUpdateAsync`), the `ChangeSummary` is empty `[]` — this is intentional; the email still informs the family to review without repeating a diff that may be stale.
- **`ChangeStatusAsync` does NOT set `FamilyNotifiedOfDraft`**: That field only applies to Draft state, which `ChangeStatusAsync` cannot produce (Draft is blocked as a target status).
- **Test naming**: Use `MethodName_StateUnderTest_ExpectedBehavior` as per `backend-standards.mdc`.
- **No FluentValidation changes needed**: `ChangeRegistrationStatusRequest` and `AdminEditRegistrationRequest` already have validators; no new request fields are added.

---

## Next Steps After Implementation

1. Hand off to `/plan-frontend-ticket` for:
   - Adding `familyNotifiedOfDraft` to frontend `RegistrationResponse` type
   - Warning banner in `RegistrationDetailPage.vue`
   - "Notificar a la familia" action button wired to `POST /notify-draft`
   - Disabling the notification toggle in `AdminStatusChangeDialog` for unsupported statuses
2. QA: verify emails are received in a staging environment with a real Resend API key.

---

## Implementation Verification

- [ ] **Code Quality**: No nullable warnings, no analyzer errors (`TreatWarningsAsErrors` is on)
- [ ] **Functionality**: `PATCH /api/registrations/{id}/status` with `Pending` + `notifyUser: true` delivers email
- [ ] **Functionality**: `POST /api/registrations/{id}/notify-draft` returns `204` and sets `FamilyNotifiedOfDraft = true`
- [ ] **Testing**: All new unit tests pass; no regressions in existing suite
- [ ] **Migration**: `dotnet ef database update` applies without errors
- [ ] **Documentation**: `data-model.md` and `api-endpoints.md` updated

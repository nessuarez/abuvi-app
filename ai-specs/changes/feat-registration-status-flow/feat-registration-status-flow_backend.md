# Backend Implementation Plan: feat-registration-status-flow — Registration Status Flow

## Overview

Implements the complete registration status lifecycle redesign: new `PartiallyPaid` and `FullyPaid` statuses, a full status history/audit trail, board-controlled manual status transitions with email notifications, automatic `FullyPaid` on last payment confirmation, user-acknowledged Draft flow, and transactional emails on every meaningful event.

All emails are sent in Spanish via the existing `IEmailService` / `ResendEmailService` infrastructure. Email failures are non-blocking (existing pattern). All status transitions are persisted in a new `registration_status_history` table.

**Architecture**: Vertical Slice — changes span `Features/Registrations/` and `Features/Payments/`, with cross-cutting impact on `Common/Services/IEmailService.cs` and `Data/`.

---

## Architecture Context

**Primary slices:**
- `src/Abuvi.API/Features/Registrations/` — Models, Service, Repository, Endpoints
- `src/Abuvi.API/Features/Payments/` — Service, Repository
- `src/Abuvi.API/Common/Services/` — IEmailService, ResendEmailService

**New files:**
- `src/Abuvi.API/Data/Configurations/RegistrationStatusHistoryConfiguration.cs`
- Two EF Core migrations

**Files modified:**
- `Features/Registrations/RegistrationsModels.cs`
- `Features/Registrations/RegistrationsService.cs`
- `Features/Registrations/RegistrationsRepository.cs`
- `Features/Registrations/RegistrationsEndpoints.cs`
- `Features/Payments/PaymentsService.cs`
- `Features/Payments/PaymentsRepository.cs`
- `Common/Services/IEmailService.cs`
- `Common/Services/ResendEmailService.cs`
- `Data/Configurations/RegistrationConfiguration.cs`
- `Data/AbuviDbContext.cs`
- `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs`

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the dedicated backend feature branch.
- **Branch name**: `feature/feat-registration-status-flow-backend`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-registration-status-flow-backend`
  3. `git branch` — verify you are on the new branch before any code changes.

---

### Step 1: Update RegistrationsModels.cs — Enums, Entities, DTOs

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

#### 1a. Expand `RegistrationStatus` enum (line ~113)

Replace:
```csharp
public enum RegistrationStatus { Pending, Confirmed, Cancelled, Draft }
```
With:
```csharp
public enum RegistrationStatus
{
    Pending,        // Registration created, awaiting board review
    PartiallyPaid,  // Board confirmed P1 received + data valid
    FullyPaid,      // All payments confirmed (automatic); board review pending
    Confirmed,      // Board gave final approval
    Draft,          // Board editing; user must acknowledge
    Cancelled
}
```

#### 1b. Add `StatusChangeTrigger` enum (after `AttendancePeriod`)

```csharp
public enum StatusChangeTrigger
{
    Automatic,       // System-triggered (e.g., last payment confirmed)
    AdminAction,     // Board/admin explicitly changed status
    UserConfirmed    // User acknowledged Draft changes
}
```

#### 1c. Add `RegistrationStatusHistory` entity (after `Payment` class)

```csharp
public class RegistrationStatusHistory
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public RegistrationStatus PreviousStatus { get; set; }
    public RegistrationStatus NewStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
    public StatusChangeTrigger Trigger { get; set; }
    public string? Notes { get; set; }

    public Registration Registration { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}
```

#### 1d. Add new fields to `Registration` entity

Add to `Registration` class (after `AdminModifiedAt`):
```csharp
public RegistrationStatus? DraftTargetStatus { get; set; }
public bool HasPendingUserAcknowledgement { get; set; } = false;
public ICollection<RegistrationStatusHistory> StatusHistory { get; set; } = [];
```

#### 1e. Update `AdminEditRegistrationRequest` DTO

Add two optional parameters at the end:
```csharp
public record AdminEditRegistrationRequest(
    List<MemberAttendanceRequest>? Members,
    List<ExtraSelectionRequest>? Extras,
    List<AccommodationPreferenceRequest>? Preferences,
    string? Notes,
    string? SpecialNeeds,
    string? CampatesPreference,
    bool? HasPet,
    bool NotifyUser = true,                         // new
    RegistrationStatus? DraftTargetStatus = null    // new
);
```

#### 1f. Add new request DTO

```csharp
public record ChangeRegistrationStatusRequest(
    RegistrationStatus NewStatus,
    string? Notes,
    bool NotifyUser = true
);
```

#### 1g. Add `StatusHistoryItemResponse` DTO

```csharp
public record StatusHistoryItemResponse(
    Guid Id,
    RegistrationStatus PreviousStatus,
    RegistrationStatus NewStatus,
    DateTime ChangedAt,
    string? ChangedByUserName,
    StatusChangeTrigger Trigger,
    string? Notes
);
```

#### 1h. Update `RegistrationResponse` record

Add three new fields at the end of the positional record (after `IsAdminModified`):
```csharp
public record RegistrationResponse(
    Guid Id,
    RegistrationFamilyUnitSummary FamilyUnit,
    RegistrationCampEditionSummary CampEdition,
    RegistrationStatus Status,
    string? Notes,
    PricingBreakdown Pricing,
    List<PaymentSummary> Payments,
    decimal AmountPaid,
    decimal AmountRemaining,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? SpecialNeeds,
    string? CampatesPreference,
    bool HasPet,
    bool IsAdminModified,
    RegistrationStatus? DraftTargetStatus,               // new
    bool HasPendingUserAcknowledgement,                  // new
    List<StatusHistoryItemResponse> StatusHistory        // new
);
```

#### 1i. Update `ToResponse()` mapping extension

In `RegistrationMappingExtensions.ToResponse()`, add the three new fields to the constructor call after `IsAdminModified`:
```csharp
r.AdminModifiedAt != null && r.Status == RegistrationStatus.Draft,  // IsAdminModified (existing)
r.DraftTargetStatus,
r.HasPendingUserAcknowledgement,
r.StatusHistory
    .OrderBy(h => h.ChangedAt)
    .Select(h => new StatusHistoryItemResponse(
        h.Id,
        h.PreviousStatus,
        h.NewStatus,
        h.ChangedAt,
        h.ChangedByUser != null
            ? $"{h.ChangedByUser.FirstName} {h.ChangedByUser.LastName}"
            : null,
        h.Trigger,
        h.Notes))
    .ToList()
```

> **Note**: `StatusHistory` is loaded via `.Include()` in `GetByIdWithDetailsAsync`. For responses from other methods (list views, admin update) that reload via `GetByIdWithDetailsAsync`, it will be populated. Avoid calling `ToResponse()` on registrations not loaded with full details.

---

### Step 2: Create EF Core Configuration for RegistrationStatusHistory

**New file**: `src/Abuvi.API/Data/Configurations/RegistrationStatusHistoryConfiguration.cs`

```csharp
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationStatusHistoryConfiguration : IEntityTypeConfiguration<RegistrationStatusHistory>
{
    public void Configure(EntityTypeBuilder<RegistrationStatusHistory> builder)
    {
        builder.ToTable("registration_status_history");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");

        builder.Property(h => h.RegistrationId).IsRequired().HasColumnName("registration_id");
        builder.Property(h => h.PreviousStatus)
            .HasConversion<string>().IsRequired().HasMaxLength(30).HasColumnName("previous_status");
        builder.Property(h => h.NewStatus)
            .HasConversion<string>().IsRequired().HasMaxLength(30).HasColumnName("new_status");
        builder.Property(h => h.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(h => h.ChangedAt).IsRequired().HasColumnName("changed_at");
        builder.Property(h => h.Trigger)
            .HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("trigger");
        builder.Property(h => h.Notes).HasMaxLength(1000).HasColumnName("notes");

        builder.HasIndex(h => h.RegistrationId)
            .HasDatabaseName("IX_RegistrationStatusHistory_RegistrationId");

        builder.HasOne(h => h.Registration).WithMany(r => r.StatusHistory)
            .HasForeignKey(h => h.RegistrationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(h => h.ChangedByUser).WithMany()
            .HasForeignKey(h => h.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
```

---

### Step 3: Update RegistrationConfiguration.cs

**File**: `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs`

Add after the `AdminModifiedAt` property mapping (before `CreatedAt`):
```csharp
builder.Property(r => r.DraftTargetStatus)
    .HasConversion<string>().HasMaxLength(30).HasColumnName("draft_target_status")
    .IsRequired(false);

builder.Property(r => r.HasPendingUserAcknowledgement)
    .HasDefaultValue(false).HasColumnName("has_pending_user_acknowledgement");
```

> The `HasMany(r => r.StatusHistory)` relationship is configured on the dependent side (`RegistrationStatusHistoryConfiguration`), so no additional relationship configuration is needed here.

---

### Step 4: Update AbuviDbContext.cs

**File**: `src/Abuvi.API/Data/AbuviDbContext.cs`

Add to the `DbSet` declarations (after `Payment`):
```csharp
public DbSet<RegistrationStatusHistory> RegistrationStatusHistories => Set<RegistrationStatusHistory>();
```

---

### Step 5: Add New Email Methods to IEmailService and ResendEmailService

#### 5a. Update `IEmailService.cs`

**File**: `src/Abuvi.API/Common/Services/IEmailService.cs`

Add new methods to the interface (add a new `// Registration Status Notifications` section):

```csharp
// ========================================
// Registration Status Notifications
// ========================================

/// <summary>Sends "Al corriente — plazo 1 confirmado" when board sets PartiallyPaid</summary>
Task SendRegistrationPartiallyPaidAsync(
    RegistrationStatusEmailData data,
    CancellationToken ct);

/// <summary>Sends "Todos los pagos recibidos" when last payment confirmed (auto → FullyPaid)</summary>
Task SendAllPaymentsReceivedAsync(
    AllPaymentsReceivedEmailData data,
    CancellationToken ct);

/// <summary>Sends "Pago recibido — plazo N de M" for intermediate payment confirmations</summary>
Task SendPaymentReceivedAsync(
    PaymentReceivedEmailData data,
    CancellationToken ct);

/// <summary>Sends "Inscripción totalmente confirmada" when board sets Confirmed (from FullyPaid)</summary>
Task SendRegistrationFinallyConfirmedAsync(
    RegistrationStatusEmailData data,
    CancellationToken ct);

/// <summary>Sends "Hay cambios en tu inscripción" when board notifies user of Draft changes</summary>
Task SendDraftChangesNotificationAsync(
    RegistrationStatusEmailData data,
    CancellationToken ct);

/// <summary>Sends "Has confirmado los cambios" after user or board force-confirms Draft</summary>
Task SendDraftChangesConfirmedAsync(
    DraftChangesConfirmedEmailData data,
    CancellationToken ct);
```

Add new DTOs at the bottom of the file:

```csharp
public record RegistrationStatusEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
}

public record PaymentReceivedEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public required int InstallmentNumber { get; init; }
    public required int TotalInstallments { get; init; }
    public required decimal Amount { get; init; }
}

public record AllPaymentsReceivedEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public required decimal TotalAmount { get; init; }
}

public record DraftChangesConfirmedEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string CampName { get; init; }
    public required Guid RegistrationId { get; init; }
    public required string NewStatusEs { get; init; }
}
```

#### 5b. Implement new methods in `ResendEmailService.cs`

**File**: `src/Abuvi.API/Common/Services/ResendEmailService.cs`

Implement each new method following the existing pattern (same `From`, `To`, `Bcc = [_boardBccEmail]`, `try/catch` that logs and rethrows as `InvalidOperationException`).

**Email content (Spanish):**

`SendRegistrationPartiallyPaidAsync`:
- Subject: `"Tu inscripción al campamento está al corriente — {data.CampName}"`
- Body: Informs the family that P1 has been received and registration data has been validated. The board confirmed everything is in order.

`SendAllPaymentsReceivedAsync`:
- Subject: `"Todos los pagos de tu inscripción han sido recibidos — {data.CampName}"`
- Body: All installments confirmed. Total amount `{data.TotalAmount:F2} €` confirmed. Registration is now under final board review before full confirmation.

`SendPaymentReceivedAsync`:
- Subject: `"Pago recibido — Plazo {data.InstallmentNumber} de {data.TotalInstallments} — {data.CampName}"`
- Body: Installment `{data.InstallmentNumber}` of `{data.TotalInstallments}` confirmed (`{data.Amount:F2} €`).

`SendRegistrationFinallyConfirmedAsync`:
- Subject: `"¡Tu inscripción está totalmente confirmada! — {data.CampName}"`
- Body: Board has given final approval. The registration is fully confirmed.

`SendDraftChangesNotificationAsync`:
- Subject: `"Hay cambios en tu inscripción que revisar — {data.CampName}"`
- Body: Board has made changes to the registration. Family must log in, review the changes, and confirm they are correct.

`SendDraftChangesConfirmedAsync`:
- Subject: `"Has confirmado los cambios en tu inscripción — {data.CampName}"`
- Body: Changes have been acknowledged. Registration is now in state `{data.NewStatusEs}`.

---

### Step 6: Update RegistrationsRepository.cs

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`

#### 6a. Add to `IRegistrationsRepository` interface

```csharp
Task AddStatusHistoryAsync(RegistrationStatusHistory history, CancellationToken ct);
```

#### 6b. Implement `AddStatusHistoryAsync` in `RegistrationsRepository`

```csharp
public async Task AddStatusHistoryAsync(RegistrationStatusHistory history, CancellationToken ct)
{
    db.RegistrationStatusHistories.Add(history);
    await db.SaveChangesAsync(ct);
}
```

#### 6c. Update `GetByIdWithDetailsAsync` to include status history

Add `.Include(r => r.StatusHistory).ThenInclude(h => h.ChangedByUser)` to the existing include chain:

```csharp
public async Task<Registration?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct)
    => await db.Registrations
        .AsNoTracking()
        .Include(r => r.FamilyUnit)
        .Include(r => r.CampEdition).ThenInclude(e => e.Camp)
        .Include(r => r.RegisteredByUser)
        .Include(r => r.Members).ThenInclude(m => m.FamilyMember)
        .Include(r => r.Extras).ThenInclude(e => e.CampEditionExtra)
        .Include(r => r.Payments)
        .Include(r => r.StatusHistory).ThenInclude(h => h.ChangedByUser)
        .FirstOrDefaultAsync(r => r.Id == id, ct);
```

> `GetByFamilyUnitAsync`, `GetAllForExportAsync`, and `GetAdminPagedAsync` do NOT need status history — those are list/export views.

---

### Step 7: Update PaymentsRepository.cs

**File**: `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`

Update `GetByIdWithRegistrationAsync` to include `Registration.RegisteredByUser` (required for email sending in `ConfirmPaymentAsync`):

```csharp
public async Task<Payment?> GetByIdWithRegistrationAsync(Guid paymentId, CancellationToken ct)
    => await db.Payments
        .Include(p => p.Registration)
            .ThenInclude(r => r.CampEdition)
                .ThenInclude(ce => ce.Camp)
        .Include(p => p.Registration)
            .ThenInclude(r => r.FamilyUnit)
        .Include(p => p.Registration)
            .ThenInclude(r => r.RegisteredByUser)   // new
        .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
```

---

### Step 8: Update RegistrationsService.cs

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

#### 8a. Add `LogStatusHistoryAsync` private helper

Add as a private method (near the bottom with other private helpers):

```csharp
private async Task LogStatusHistoryAsync(
    Guid registrationId,
    RegistrationStatus previousStatus,
    RegistrationStatus newStatus,
    Guid? changedByUserId,
    StatusChangeTrigger trigger,
    string? notes,
    CancellationToken ct)
{
    var history = new RegistrationStatusHistory
    {
        Id = Guid.NewGuid(),
        RegistrationId = registrationId,
        PreviousStatus = previousStatus,
        NewStatus = newStatus,
        ChangedByUserId = changedByUserId,
        ChangedAt = DateTime.UtcNow,
        Trigger = trigger,
        Notes = notes
    };
    await registrationsRepo.AddStatusHistoryAsync(history, ct);
}
```

#### 8b. Add `ChangeStatusAsync` (board-only manual status change)

Add as a new public method:

```csharp
public async Task<RegistrationResponse> ChangeStatusAsync(
    Guid registrationId, Guid adminUserId, ChangeRegistrationStatusRequest request, CancellationToken ct)
{
    // 1. Load registration
    var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    var previousStatus = registration.Status;

    // 2. Reject transitions that use dedicated endpoints/are automatic
    if (request.NewStatus == RegistrationStatus.Cancelled)
        throw new BusinessRuleException(
            "Use el endpoint de cancelación para cancelar una inscripción.");
    if (request.NewStatus == RegistrationStatus.Draft)
        throw new BusinessRuleException(
            "El estado En revisión se asigna automáticamente al editar la inscripción.");
    if (request.NewStatus == RegistrationStatus.FullyPaid)
        throw new BusinessRuleException(
            "El estado Pago completo se asigna automáticamente al confirmar todos los pagos.");

    // 3. Validate allowed transitions
    var validTransitions = new Dictionary<RegistrationStatus, HashSet<RegistrationStatus>>
    {
        [RegistrationStatus.Pending]       = [RegistrationStatus.PartiallyPaid, RegistrationStatus.Confirmed],
        [RegistrationStatus.PartiallyPaid] = [RegistrationStatus.Pending, RegistrationStatus.Confirmed],
        [RegistrationStatus.FullyPaid]     = [RegistrationStatus.Confirmed, RegistrationStatus.Pending],
        [RegistrationStatus.Confirmed]     = [RegistrationStatus.Pending, RegistrationStatus.PartiallyPaid],
        [RegistrationStatus.Draft]         = [RegistrationStatus.Pending, RegistrationStatus.PartiallyPaid,
                                              RegistrationStatus.FullyPaid, RegistrationStatus.Confirmed],
        [RegistrationStatus.Cancelled]     = [],
    };

    if (!validTransitions.TryGetValue(previousStatus, out var allowed) || !allowed.Contains(request.NewStatus))
        throw new BusinessRuleException(
            $"La transición de {MapStatusEs(previousStatus)} a {MapStatusEs(request.NewStatus)} no está permitida.");

    // 4. Apply transition, clear draft fields when exiting Draft
    registration.Status = request.NewStatus;
    if (previousStatus == RegistrationStatus.Draft)
    {
        registration.DraftTargetStatus = null;
        registration.HasPendingUserAcknowledgement = false;
    }

    await registrationsRepo.UpdateAsync(registration, ct);

    // 5. Log history
    await LogStatusHistoryAsync(registrationId, previousStatus, request.NewStatus,
        adminUserId, StatusChangeTrigger.AdminAction, request.Notes, ct);

    logger.LogInformation(
        "Registration {RegistrationId} status changed {Previous} → {New} by admin {AdminUserId}",
        registrationId, previousStatus, request.NewStatus, adminUserId);

    // 6. Send notification email if requested (non-blocking)
    if (request.NotifyUser)
    {
        try
        {
            var emailData = new RegistrationStatusEmailData
            {
                ToEmail = registration.RegisteredByUser.Email,
                RecipientFirstName = registration.RegisteredByUser.FirstName,
                CampName = registration.CampEdition.Camp.Name,
                RegistrationId = registration.Id
            };

            Task emailTask = request.NewStatus switch
            {
                RegistrationStatus.PartiallyPaid =>
                    emailService.SendRegistrationPartiallyPaidAsync(emailData, ct),
                RegistrationStatus.Confirmed =>
                    emailService.SendRegistrationFinallyConfirmedAsync(emailData, ct),
                _ => Task.CompletedTask
            };
            await emailTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send status change notification email for registration {RegistrationId}",
                registrationId);
        }
    }

    // 7. Reload with full history for response
    var updated = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    var amountPaid = updated.Payments
        .Where(p => p.Status == PaymentStatus.Completed)
        .Sum(p => p.Amount);

    return updated.ToResponse(amountPaid);
}
```

#### 8c. Add `ConfirmChangesAsync` (user or board force-confirm closes Draft)

```csharp
public async Task<RegistrationResponse> ConfirmChangesAsync(
    Guid registrationId, Guid requestingUserId, bool isAdminOrBoard, CancellationToken ct)
{
    // 1. Load registration
    var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    // 2. Validate status
    if (registration.Status != RegistrationStatus.Draft)
        throw new BusinessRuleException(
            "La inscripción no está en estado de revisión pendiente.");

    // 3. Validate authorization
    if (!isAdminOrBoard && registration.FamilyUnit.RepresentativeUserId != requestingUserId)
        throw new UnauthorizedAccessException(
            "No tienes permiso para confirmar los cambios de esta inscripción.");

    var previousStatus = registration.Status;
    var targetStatus = registration.DraftTargetStatus ?? RegistrationStatus.Pending;

    // 4. Apply transition
    registration.Status = targetStatus;
    registration.DraftTargetStatus = null;
    registration.HasPendingUserAcknowledgement = false;

    await registrationsRepo.UpdateAsync(registration, ct);

    // 5. Log history
    var trigger = isAdminOrBoard
        ? StatusChangeTrigger.AdminAction
        : StatusChangeTrigger.UserConfirmed;
    await LogStatusHistoryAsync(registrationId, previousStatus, targetStatus,
        requestingUserId, trigger, null, ct);

    logger.LogInformation(
        "Registration {RegistrationId} Draft confirmed by {UserId} (isAdmin={IsAdmin}), → {Target}",
        registrationId, requestingUserId, isAdminOrBoard, targetStatus);

    // 6. Send confirmation email (non-blocking)
    try
    {
        await emailService.SendDraftChangesConfirmedAsync(new DraftChangesConfirmedEmailData
        {
            ToEmail = registration.RegisteredByUser.Email,
            RecipientFirstName = registration.RegisteredByUser.FirstName,
            CampName = registration.CampEdition.Camp.Name,
            RegistrationId = registration.Id,
            NewStatusEs = MapStatusEs(targetStatus)
        }, ct);
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Failed to send draft-confirmed email for registration {RegistrationId}",
            registrationId);
    }

    // 7. Reload with history for response
    var updated = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    var amountPaid = updated.Payments
        .Where(p => p.Status == PaymentStatus.Completed)
        .Sum(p => p.Amount);

    return updated.ToResponse(amountPaid);
}
```

#### 8d. Update `AdminUpdateAsync`

1. Capture `previousStatus` before the status change:
   ```csharp
   var previousStatus = registration.Status;
   ```

2. After setting `registration.Status = RegistrationStatus.Draft`, also set:
   ```csharp
   registration.DraftTargetStatus = request.DraftTargetStatus ?? previousStatus;
   registration.HasPendingUserAcknowledgement = true;
   ```

3. After `registrationsRepo.UpdateAsync`, log history only if status actually changed:
   ```csharp
   if (previousStatus != RegistrationStatus.Draft)
   {
       await LogStatusHistoryAsync(registrationId, previousStatus, RegistrationStatus.Draft,
           adminUserId, StatusChangeTrigger.AdminAction, null, ct);
   }
   ```
   > Avoids duplicate `Draft→Draft` history rows for consecutive admin edits on an already-Draft registration.

4. After reloading `updated`, send optional notification email (non-blocking):
   ```csharp
   if (request.NotifyUser)
   {
       try
       {
           await emailService.SendDraftChangesNotificationAsync(new RegistrationStatusEmailData
           {
               ToEmail = updated.RegisteredByUser.Email,
               RecipientFirstName = updated.RegisteredByUser.FirstName,
               CampName = updated.CampEdition.Camp.Name,
               RegistrationId = updated.Id
           }, ct);
       }
       catch (Exception ex)
       {
           logger.LogError(ex,
               "Failed to send draft notification email for registration {RegistrationId}",
               registrationId);
       }
   }
   ```

5. The method signature must receive `adminUserId` — verify it's already a parameter. If not, add it.

#### 8e. Update `CancelAsync` — add history log

Capture `previousStatus` before the status change and add history log after `UpdateAsync`:
```csharp
var previousStatus = registration.Status;
// ... existing cancel logic ...
await registrationsRepo.UpdateAsync(registration, ct);
await LogStatusHistoryAsync(registrationId, previousStatus, RegistrationStatus.Cancelled,
    requestingUserId, StatusChangeTrigger.AdminAction, null, ct);
```

#### 8f. Update `DeleteAsync` — block `FullyPaid`

Replace the existing status guard:
```csharp
if (registration.Status is RegistrationStatus.Confirmed)
    throw new BusinessRuleException("Confirmed registrations cannot be deleted. Please cancel first.");
```
With:
```csharp
if (registration.Status is RegistrationStatus.Confirmed or RegistrationStatus.FullyPaid)
    throw new BusinessRuleException(
        "Confirmed or fully-paid registrations cannot be deleted. Please cancel first.");
```

#### 8g. Update `MapStatusEs`

```csharp
private static string MapStatusEs(RegistrationStatus status) => status switch
{
    RegistrationStatus.Pending       => "Pendiente",
    RegistrationStatus.PartiallyPaid => "Al corriente",
    RegistrationStatus.FullyPaid     => "Pago completo",
    RegistrationStatus.Confirmed     => "Confirmada",
    RegistrationStatus.Cancelled     => "Cancelada",
    RegistrationStatus.Draft         => "En revisión",
    _                                => status.ToString()
};
```

---

### Step 9: Update PaymentsService.cs

**File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`

#### 9a. Add `IEmailService` to constructor

```csharp
public class PaymentsService(
    IPaymentsRepository paymentsRepo,
    IRegistrationsRepository registrationsRepo,
    IAssociationSettingsRepository settingsRepo,
    IBlobStorageService blobStorageService,
    IEmailService emailService,       // new — add before ILogger
    ILogger<PaymentsService> logger) : IPaymentsService
```

> `IEmailService` is already registered in the DI container (used by `RegistrationsService`). No `Program.cs` change needed.

#### 9b. Replace auto-`Confirmed` logic in `ConfirmPaymentAsync`

Replace the existing block (lines ~183-192):
```csharp
if (allPayments.All(p => p.Status == PaymentStatus.Completed))
{
    var registration = payment.Registration;
    registration.Status = RegistrationStatus.Confirmed;
    await registrationsRepo.UpdateAsync(registration, ct);
    logger.LogInformation(...);
}
```

With:
```csharp
var completedCount = allPayments.Count(p => p.Status == PaymentStatus.Completed);
var totalCount = allPayments.Count;
var registration = payment.Registration;

if (completedCount == totalCount)
{
    // Last payment: auto-transition to FullyPaid
    var previousStatus = registration.Status;
    registration.Status = RegistrationStatus.FullyPaid;
    await registrationsRepo.UpdateAsync(registration, ct);

    await AddStatusHistoryInternalAsync(registration.Id, previousStatus, RegistrationStatus.FullyPaid,
        adminUserId, StatusChangeTrigger.Automatic, null, ct);

    logger.LogInformation(
        "Registration {RegistrationId} auto-transitioned to FullyPaid - all {Count} installments confirmed",
        registration.Id, totalCount);

    try
    {
        await emailService.SendAllPaymentsReceivedAsync(new AllPaymentsReceivedEmailData
        {
            ToEmail = registration.RegisteredByUser.Email,
            RecipientFirstName = registration.RegisteredByUser.FirstName,
            CampName = registration.CampEdition.Camp.Name,
            RegistrationId = registration.Id,
            TotalAmount = registration.TotalAmount
        }, ct);
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Failed to send all-payments-received email for registration {RegistrationId}",
            registration.Id);
    }
}
else
{
    // Intermediate payment: send receipt only, no status change
    logger.LogInformation(
        "Registration {RegistrationId} partial payment confirmed ({Completed}/{Total})",
        registration.Id, completedCount, totalCount);

    try
    {
        await emailService.SendPaymentReceivedAsync(new PaymentReceivedEmailData
        {
            ToEmail = registration.RegisteredByUser.Email,
            RecipientFirstName = registration.RegisteredByUser.FirstName,
            CampName = registration.CampEdition.Camp.Name,
            RegistrationId = registration.Id,
            InstallmentNumber = payment.InstallmentNumber,
            TotalInstallments = totalCount,
            Amount = payment.Amount
        }, ct);
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Failed to send payment-received email for payment {PaymentId}", paymentId);
    }
}
```

#### 9c. Add `AddStatusHistoryInternalAsync` private helper to `PaymentsService`

```csharp
private async Task AddStatusHistoryInternalAsync(
    Guid registrationId,
    RegistrationStatus previousStatus,
    RegistrationStatus newStatus,
    Guid? changedByUserId,
    StatusChangeTrigger trigger,
    string? notes,
    CancellationToken ct)
{
    var history = new RegistrationStatusHistory
    {
        Id = Guid.NewGuid(),
        RegistrationId = registrationId,
        PreviousStatus = previousStatus,
        NewStatus = newStatus,
        ChangedByUserId = changedByUserId,
        ChangedAt = DateTime.UtcNow,
        Trigger = trigger,
        Notes = notes
    };
    await registrationsRepo.AddStatusHistoryAsync(history, ct);
}
```

> The helper is intentionally duplicated between `RegistrationsService` and `PaymentsService` to avoid cross-slice coupling. If a third consumer appears, extract to a shared `IStatusHistoryService`.

---

### Step 10: Update RegistrationsEndpoints.cs

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

#### 10a. Register new `PATCH /{id}/status` endpoint (admin group)

Add in the `adminEditGroup` section (after `AdminEditRegistration`):

```csharp
adminEditGroup.MapPatch("/{id:guid}/status", ChangeRegistrationStatus)
    .WithName("ChangeRegistrationStatus")
    .WithSummary("Change registration status manually (Admin/Board only)")
    .AddEndpointFilter<ValidationFilter<ChangeRegistrationStatusRequest>>()
    .Produces<ApiResponse<RegistrationResponse>>()
    .Produces(400).Produces(401).Produces(403).Produces(404).Produces(422);
```

#### 10b. Register new `POST /{id}/confirm-changes` endpoint (user group)

Add in the regular `group` section (after `CancelRegistration`):

```csharp
group.MapPost("/{id:guid}/confirm-changes", ConfirmRegistrationChanges)
    .WithName("ConfirmRegistrationChanges")
    .WithSummary("Confirm pending Draft changes (own registration or Admin/Board force-confirm)")
    .Produces<ApiResponse<RegistrationResponse>>()
    .Produces(401).Produces(403).Produces(404).Produces(422);
```

> Placed in the user `group` (auth-only) rather than the admin group because family representatives must also call this. The service layer enforces ownership.

#### 10c. Add handler functions

```csharp
private static async Task<IResult> ChangeRegistrationStatus(
    Guid id, ChangeRegistrationStatusRequest request,
    RegistrationsService service, ClaimsPrincipal user, CancellationToken ct)
{
    var adminUserId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var result = await service.ChangeStatusAsync(id, adminUserId, request, ct);
    return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
}

private static async Task<IResult> ConfirmRegistrationChanges(
    Guid id, RegistrationsService service, ClaimsPrincipal user, CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var userRole = user.GetUserRole();
    var isAdminOrBoard = userRole is "Admin" or "Board";
    var result = await service.ConfirmChangesAsync(id, userId, isAdminOrBoard, ct);
    return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
}
```

#### 10d. Add FluentValidation validator for `ChangeRegistrationStatusRequest`

Add in `RegistrationsModels.cs` (or a `RegistrationsValidators.cs` file if validators are kept separate — follow the existing project pattern):

```csharp
public class ChangeRegistrationStatusRequestValidator : AbstractValidator<ChangeRegistrationStatusRequest>
{
    public ChangeRegistrationStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}
```

---

### Step 11: Run EF Core Migrations

From `src/Abuvi.API/` directory:

```bash
# Migration 1 — new status history table
dotnet ef migrations add AddRegistrationStatusHistory

# Migration 2 — new columns on registrations table
dotnet ef migrations add UpdateRegistrationForDraftFlow

# Apply to database
dotnet ef database update
```

**Expected schema changes:**

Migration 1 creates:
```sql
CREATE TABLE registration_status_history (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    registration_id uuid NOT NULL REFERENCES registrations(id) ON DELETE CASCADE,
    previous_status varchar(30) NOT NULL,
    new_status varchar(30) NOT NULL,
    changed_by_user_id uuid REFERENCES users(id) ON DELETE SET NULL,
    changed_at timestamptz NOT NULL,
    trigger varchar(20) NOT NULL,
    notes text
);
CREATE INDEX IX_RegistrationStatusHistory_RegistrationId ON registration_status_history(registration_id);
```

Migration 2 adds:
```sql
ALTER TABLE registrations ADD COLUMN draft_target_status varchar(30);
ALTER TABLE registrations ADD COLUMN has_pending_user_acknowledgement bool NOT NULL DEFAULT false;
```

> No data migration needed. Existing rows default to `null` / `false` which are valid.

---

### Step 12: Update Unit Tests

**File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs`

#### 12a. Update `PaymentsService` constructor in tests

```csharp
private readonly IEmailService _emailService = Substitute.For<IEmailService>();

public PaymentsServiceTests()
{
    _sut = new PaymentsService(
        _paymentsRepo, _registrationsRepo, _settingsRepo,
        _blobStorageService, _emailService, _logger);
}
```

#### 12b. Update `ConfirmPaymentAsync_BothInstallmentsCompleted_ConfirmsRegistration`

Rename to `ConfirmPaymentAsync_AllInstallmentsCompleted_TransitionsToFullyPaid`. Update assertions:

```csharp
// Assert: registration auto-set to FullyPaid (board must explicitly confirm → Confirmed)
await _registrationsRepo.Received(1).UpdateAsync(
    Arg.Is<Registration>(r => r.Status == RegistrationStatus.FullyPaid),
    Arg.Any<CancellationToken>());

// Assert: status history logged as Automatic
await _registrationsRepo.Received(1).AddStatusHistoryAsync(
    Arg.Is<RegistrationStatusHistory>(h =>
        h.NewStatus == RegistrationStatus.FullyPaid &&
        h.Trigger == StatusChangeTrigger.Automatic),
    Arg.Any<CancellationToken>());

// Assert: "all payments received" email sent
await _emailService.Received(1).SendAllPaymentsReceivedAsync(
    Arg.Any<AllPaymentsReceivedEmailData>(), Arg.Any<CancellationToken>());
```

#### 12c. Update `ConfirmPaymentAsync_OnlyOneInstallmentCompleted_RegistrationStaysPending`

The current assertion `DidNotReceive().UpdateAsync` breaks because `UpdateAsync` is now always called (for the payment itself). Update:

```csharp
// Assert: no registration status change (registration UpdateAsync not called for status)
await _registrationsRepo.DidNotReceive().UpdateAsync(
    Arg.Any<Registration>(), Arg.Any<CancellationToken>());

// Assert: payment received email sent
await _emailService.Received(1).SendPaymentReceivedAsync(
    Arg.Any<PaymentReceivedEmailData>(), Arg.Any<CancellationToken>());

// Assert: all-payments email NOT sent
await _emailService.DidNotReceive().SendAllPaymentsReceivedAsync(
    Arg.Any<AllPaymentsReceivedEmailData>(), Arg.Any<CancellationToken>());
```

#### 12d. Add new `ConfirmPaymentAsync` tests

```csharp
[Fact]
public async Task ConfirmPaymentAsync_AllPaid_EmailFailureIsNonBlocking()
{
    var payment = CreatePayment(PaymentStatus.PendingReview);
    _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
        .Returns(payment);
    _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
        .Returns([CreatePaymentEntity(PaymentStatus.Completed, 1),
                  CreatePaymentEntity(PaymentStatus.Completed, 2)]);
    _emailService.SendAllPaymentsReceivedAsync(Arg.Any<AllPaymentsReceivedEmailData>(), Arg.Any<CancellationToken>())
        .ThrowsAsync(new Exception("SMTP failure"));

    var act = () => _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, null, CancellationToken.None);

    await act.Should().NotThrowAsync();
}

[Fact]
public async Task ConfirmPaymentAsync_IntermediatePayment_NoStatusHistoryLogged()
{
    var payment = CreatePayment(PaymentStatus.PendingReview);
    _paymentsRepo.GetByIdWithRegistrationAsync(PaymentId, Arg.Any<CancellationToken>())
        .Returns(payment);
    // Only 1 of 2 completed (intermediate)
    _paymentsRepo.GetByRegistrationIdAsync(RegistrationId, Arg.Any<CancellationToken>())
        .Returns([CreatePaymentEntity(PaymentStatus.Pending, 2), payment]);

    await _sut.ConfirmPaymentAsync(PaymentId, AdminUserId, null, CancellationToken.None);

    await _registrationsRepo.DidNotReceive().AddStatusHistoryAsync(
        Arg.Any<RegistrationStatusHistory>(), Arg.Any<CancellationToken>());
}
```

#### 12e. Add `RegistrationsService` tests for new methods

Create `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceStatusTests.cs`:

**Key test cases for `ChangeStatusAsync`:**
- `ChangeStatusAsync_PendingToPartiallyPaid_TransitionsAndLogsHistory` — verifies status update + history row
- `ChangeStatusAsync_FullyPaidToConfirmed_TransitionsAndSendsEmail` — verifies email sent
- `ChangeStatusAsync_PendingToFullyPaid_ThrowsBusinessRuleException` — FullyPaid is blocked
- `ChangeStatusAsync_AnyToCancelled_ThrowsBusinessRuleException`
- `ChangeStatusAsync_AnyToDraft_ThrowsBusinessRuleException`
- `ChangeStatusAsync_InvalidTransition_ThrowsBusinessRuleException` — e.g., `Cancelled → Pending`
- `ChangeStatusAsync_NotifyUserFalse_DoesNotSendEmail`
- `ChangeStatusAsync_DraftToPending_ClearsDraftFields`

**Key test cases for `ConfirmChangesAsync`:**
- `ConfirmChangesAsync_DraftWithTargetStatus_TransitionsToTarget`
- `ConfirmChangesAsync_DraftWithNullTarget_TransitionsToPending`
- `ConfirmChangesAsync_NotDraftStatus_ThrowsBusinessRuleException`
- `ConfirmChangesAsync_WrongUser_ThrowsUnauthorizedAccessException`
- `ConfirmChangesAsync_AdminForceConfirm_SucceedsRegardlessOfOwnership`
- `ConfirmChangesAsync_SendsConfirmationEmail`
- `ConfirmChangesAsync_EmailFailure_IsNonBlocking`
- `ConfirmChangesAsync_UserConfirm_LogsTriggerAsUserConfirmed`
- `ConfirmChangesAsync_AdminConfirm_LogsTriggerAsAdminAction`

---

### Step 13: Update Technical Documentation

- **`ai-specs/specs/data-model.md`**: Add `RegistrationStatusHistory` entity; add `DraftTargetStatus`, `HasPendingUserAcknowledgement`, `StatusHistory` to `Registration`; document all new enum values.
- **`ai-specs/specs/api-spec.yml`**: Add `PATCH /api/registrations/{id}/status` and `POST /api/registrations/{id}/confirm-changes`; update `RegistrationResponse` schema; update `AdminEditRegistrationRequest` schema.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Update `RegistrationsModels.cs` (enums, entities, DTOs, mapping extension)
3. Step 2 — Create `RegistrationStatusHistoryConfiguration.cs`
4. Step 3 — Update `RegistrationConfiguration.cs`
5. Step 4 — Update `AbuviDbContext.cs`
6. Step 11 — Run EF Core migrations (schema must exist for compile/test)
7. Step 5 — Update `IEmailService.cs` + `ResendEmailService.cs`
8. Step 6 — Update `RegistrationsRepository.cs`
9. Step 7 — Update `PaymentsRepository.cs`
10. Step 8 — Update `RegistrationsService.cs`
11. Step 9 — Update `PaymentsService.cs`
12. Step 10 — Update `RegistrationsEndpoints.cs` + add validator
13. Step 12 — Update/add unit tests
14. Step 13 — Update documentation

---

## Testing Checklist

- [ ] `PaymentsService` constructor updated with `IEmailService` mock — all existing tests still compile
- [ ] `ConfirmPaymentAsync`: all installments done → `FullyPaid` (not `Confirmed`), history logged as `Automatic`, email sent
- [ ] `ConfirmPaymentAsync`: intermediate payment → receipt email sent, no status change, no history row
- [ ] `ConfirmPaymentAsync`: email failure → exception swallowed, response returned normally
- [ ] `ChangeStatusAsync`: valid transitions succeed, history logged, email sent when `NotifyUser=true`
- [ ] `ChangeStatusAsync`: `FullyPaid`, `Draft`, `Cancelled` as target → `BusinessRuleException`
- [ ] `ChangeStatusAsync`: invalid transition (e.g., `Cancelled → Pending`) → `BusinessRuleException`
- [ ] `ChangeStatusAsync`: `Draft → Pending` clears `DraftTargetStatus` and `HasPendingUserAcknowledgement`
- [ ] `ConfirmChangesAsync`: only works when status is `Draft`
- [ ] `ConfirmChangesAsync`: transitions to `DraftTargetStatus`, or `Pending` if null
- [ ] `ConfirmChangesAsync`: wrong user + not admin → `UnauthorizedAccessException`
- [ ] `ConfirmChangesAsync`: admin force-confirm → succeeds regardless of ownership
- [ ] `ConfirmChangesAsync`: email failure → non-blocking
- [ ] `AdminUpdateAsync`: `DraftTargetStatus` stored, `HasPendingUserAcknowledgement = true`, email sent only when `NotifyUser=true`
- [ ] `DeleteAsync`: `FullyPaid` → `BusinessRuleException`
- [ ] `CancelAsync`: history row logged
- [ ] `ToResponse()`: `StatusHistory` ordered ascending by `ChangedAt`

---

## Error Response Format

```json
{ "success": true, "data": { ... } }
{ "success": false, "error": "Mensaje de error" }
```

HTTP status mapping:
| Code | Scenario |
|------|---------|
| 200 | Successful status change or confirm |
| 400 | FluentValidation error (invalid enum value) |
| 403 | Role check (middleware) or ownership (service `UnauthorizedAccessException`) |
| 404 | Registration not found |
| 422 | `BusinessRuleException` (invalid transition, wrong status) |

---

## Dependencies

No new NuGet packages required. All existing dependencies apply:
- `Microsoft.EntityFrameworkCore` (EF Core)
- `Resend` (email)
- `FluentValidation`
- `NSubstitute` (tests)

---

## Notes

1. **`HasMaxLength(20)` on `Status` column**: All new enum values fit — `PartiallyPaid` (12 chars), `FullyPaid` (8 chars). No column resize needed.
2. **Email failures are always non-blocking**: Every email call must be wrapped in `try/catch` per the existing project pattern.
3. **`RegisteredByUser` must be loaded for payment emails**: Step 7 adds the missing include to `PaymentsRepository.GetByIdWithRegistrationAsync`.
4. **Status history only in `GetByIdWithDetailsAsync`**: List, export, and admin-paged queries intentionally do not include it.
5. **`Draft → Draft` deduplication**: `AdminUpdateAsync` only logs a history row if `previousStatus != Draft` to avoid duplicate rows on consecutive edits.
6. **`FullyPaid` is automatic-only**: `ChangeStatusAsync` explicitly rejects `FullyPaid` as a target. Only `ConfirmPaymentAsync` can set it.
7. **Existing `Confirmed` rows are unaffected**: The new `FullyPaid` status only applies to future payment confirmations. All existing data is valid.
8. **All emails in Spanish with BCC to `junta.abuvi@gmail.com`**: Follow the exact pattern in `ResendEmailService`.

---

## Next Steps After Implementation

- **Frontend ticket**: new status badges (`PartiallyPaid`, `FullyPaid`), status timeline component, "Confirmar cambios" banner, admin status-change dropdown, notify toggle on edit form.
- **Board action**: existing `Confirmed` registrations (auto-confirmed before this feature) require no changes. New registrations follow the new flow.
- **Smoke test**: confirm a payment → verify `FullyPaid` + email; board calls `PATCH /status` to `Confirmed` → verify email.

---

## Implementation Verification

- [ ] No nullable reference type warnings, no analyzer warnings
- [ ] `PATCH /api/registrations/{id}/status` returns 422 for blocked/invalid transitions, 200 for valid
- [ ] `POST /api/registrations/{id}/confirm-changes` returns 422 if not in Draft, 403 if wrong user, 200 on success
- [ ] `GET /api/registrations/{id}` response includes `statusHistory`, `hasPendingUserAcknowledgement`, `draftTargetStatus`
- [ ] Confirming last payment sets `FullyPaid` (not `Confirmed`)
- [ ] 90% test coverage via xUnit + FluentAssertions + NSubstitute
- [ ] EF Core migrations applied cleanly — `registration_status_history` table created, two columns added to `registrations`
- [ ] `data-model.md` and `api-spec.yml` updated

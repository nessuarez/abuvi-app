# Backend Implementation Plan: feat-payment-adjustments — Payment Adjustments & Admin Registration Management

## Overview

This plan covers four interrelated admin capabilities for correcting real-world payment discrepancies and registration errors:

1. **Edit any payment** — `PUT /api/admin/payments/{id}` (extends beyond manual-only)
2. **Recalculate pending installments** — auto-triggered when a `Completed` payment's amount changes
3. **Confirm combined payments** — `POST /api/admin/registrations/{id}/payments/confirm-combined`
4. **Admin member update + refund** — `PUT /api/admin/registrations/{id}/members`

Follows Vertical Slice Architecture. All changes live in `Features/Payments/` and `Features/Registrations/`. No new feature slices.

---

## Architecture Context

**Feature slices affected:**
- `src/Abuvi.API/Features/Payments/` — new DTOs, service methods, endpoint handlers
- `src/Abuvi.API/Features/Registrations/` — new admin member-update endpoint
- `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs` — two new columns
- `src/Abuvi.API/Data/Migrations/` — one new EF migration

**No new feature slices** — all changes extend existing slices.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/feat-payment-adjustments-backend`
- **Base branch**: `dev`
- **Commands**:
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/feat-payment-adjustments-backend
  git branch
  ```

---

### Step 1: Add New Fields to `Payment` Entity + EF Configuration

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

**Action**: Add two audit fields to the `Payment` class after the existing `IsManual` property.

**Fields to add**:
```csharp
public bool ConceptOverridden { get; set; } = false;
public decimal? OriginalAmount { get; set; }
```

- `ConceptOverridden`: set to `true` the first time an admin edits a Completed payment's amount or concept.
- `OriginalAmount`: snapshot of `Amount` **before the first admin edit** (subsequent edits must not overwrite it — it always preserves the original).

**File**: `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs`

**Action**: Add EF mappings after the existing `is_manual` mapping:

```csharp
builder.Property(p => p.ConceptOverridden)
    .HasColumnName("concept_overridden")
    .IsRequired()
    .HasDefaultValue(false);

builder.Property(p => p.OriginalAmount)
    .HasPrecision(10, 2)
    .HasColumnName("original_amount");

// Remove the existing DB-level check constraint on amount > 0
// (needed for refund payments that have negative amounts)
// The constraint "CK_Payments_Amount" must be removed in the migration.
```

> **Important**: The existing check constraint `CK_Payments_Amount` (`amount > 0`) in `PaymentConfiguration.cs` will block negative refund payments. The EF migration must drop this constraint. Remove the `builder.ToTable(t => t.HasCheckConstraint(...))` line from the configuration **or** replace it with a less restrictive constraint (e.g., allow negative only when `is_manual = true`). Simplest: remove the check constraint entirely; amount validation is enforced at the service layer.

**Implementation steps**:
1. Add `ConceptOverridden` and `OriginalAmount` to `Payment` class in `RegistrationsModels.cs`.
2. Add EF mappings in `PaymentConfiguration.cs`.
3. Remove `builder.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "amount > 0"))` from `PaymentConfiguration.cs` (refunds need negative amounts).

---

### Step 2: Add New DTOs to `PaymentsModels.cs`

**File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`

**Action**: Add the following new request/response records:

#### 2a. `AdminEditPaymentRequest`

```csharp
public record AdminEditPaymentRequest
{
    public decimal? Amount { get; init; }           // null = no change
    public string? ConceptDescription { get; init; } // null = no change; replaces concept lines with a ManualPaymentConceptLine
    public DateTime? DueDate { get; init; }
    public string? AdminNotes { get; init; }
}
```

#### 2b. `ConfirmCombinedPaymentsRequest`

```csharp
public record ConfirmCombinedPaymentsRequest
{
    public List<Guid> PaymentIds { get; init; } = [];
    public decimal TotalReceivedAmount { get; init; }
    public bool ApplySurplusToNext { get; init; } = false; // if true, surplus reduces next pending auto payment
    public string? AdminNotes { get; init; }
}
```

#### 2c. Update `AdminPaymentResponse`

Add two fields to the existing `AdminPaymentResponse` record:

```csharp
bool ConceptOverridden,
decimal? OriginalAmount,
```

These must also be mapped in the `MapToAdminResponse` / `MapToResponse` mapping helpers.

**Implementation steps**:
1. Add `AdminEditPaymentRequest` record.
2. Add `ConfirmCombinedPaymentsRequest` record.
3. Add `ConceptOverridden` and `OriginalAmount` to `AdminPaymentResponse`.
4. Update the mapping method that builds `AdminPaymentResponse` to include the two new fields.

---

### Step 3: Add Validators

**File**: `src/Abuvi.API/Features/Payments/PaymentsValidators.cs`

**Action**: Add validators for the two new request types.

#### 3a. `AdminEditPaymentRequestValidator`

```csharp
public class AdminEditPaymentRequestValidator : AbstractValidator<AdminEditPaymentRequest>
{
    public AdminEditPaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El importe debe ser mayor que cero")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("El importe no puede tener más de 2 decimales")
            .When(x => x.Amount.HasValue);

        RuleFor(x => x.ConceptDescription)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.ConceptDescription is not null);

        RuleFor(x => x.AdminNotes)
            .MaximumLength(2000).WithMessage("Las notas no pueden superar los 2000 caracteres")
            .When(x => x.AdminNotes is not null);

        // Must provide at least one field to change
        RuleFor(x => x)
            .Must(x => x.Amount.HasValue || x.ConceptDescription is not null
                       || x.DueDate.HasValue || x.AdminNotes is not null)
            .WithMessage("Se debe proporcionar al menos un campo para actualizar");
    }
}
```

#### 3b. `ConfirmCombinedPaymentsRequestValidator`

```csharp
public class ConfirmCombinedPaymentsRequestValidator : AbstractValidator<ConfirmCombinedPaymentsRequest>
{
    public ConfirmCombinedPaymentsRequestValidator()
    {
        RuleFor(x => x.PaymentIds)
            .NotEmpty().WithMessage("Se debe proporcionar al menos un pago")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("No se pueden duplicar los IDs de pago");

        RuleFor(x => x.TotalReceivedAmount)
            .GreaterThan(0).WithMessage("El importe recibido debe ser mayor que cero")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithMessage("El importe no puede tener más de 2 decimales");
    }
}
```

---

### Step 4: Add New Repository Methods

**File**: `src/Abuvi.API/Features/Payments/IPaymentsRepository.cs`

No new repository methods are needed. The existing `GetByIdWithRegistrationAsync`, `GetByRegistrationIdTrackedAsync`, `AddAsync`, `UpdateAsync`, `AddRangeAsync` cover all scenarios. Verify these exist:

- `GetByRegistrationIdTrackedAsync(Guid registrationId, CancellationToken ct)` — must return tracking-enabled list for in-transaction updates
- `AddAsync(Payment payment, CancellationToken ct)` — for new refund payments

If `GetByRegistrationIdTrackedAsync` does not exist, add it to `IPaymentsRepository` and `PaymentsRepository`:

```csharp
// Interface addition (if missing)
Task<List<Payment>> GetByRegistrationIdTrackedAsync(Guid registrationId, CancellationToken ct);

// Repository implementation
public async Task<List<Payment>> GetByRegistrationIdTrackedAsync(Guid registrationId, CancellationToken ct)
    => await db.Payments
        .Where(p => p.RegistrationId == registrationId)
        .OrderBy(p => p.InstallmentNumber)
        .ToListAsync(ct);
```

---

### Step 5: Implement New Service Methods

**File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`

Add the following methods. All must be added to `IPaymentsService` as well.

---

#### 5a. `AdminEditPaymentAsync`

```csharp
Task<AdminPaymentResponse> AdminEditPaymentAsync(
    Guid paymentId, AdminEditPaymentRequest request, Guid adminUserId, CancellationToken ct)
```

**Implementation steps**:
1. Load payment via `paymentsRepo.GetByIdWithRegistrationAsync(paymentId, ct)` → throw `NotFoundException` if null.
2. Guard: if `payment.Status` is `Failed` or `Refunded` → throw `BusinessRuleException("No se puede editar un pago en estado fallido o devuelto")`.
3. If `request.Amount` has value and differs from `payment.Amount`:
   - If `payment.OriginalAmount` is `null` (first edit) → set `payment.OriginalAmount = payment.Amount`.
   - Set `payment.Amount = request.Amount.Value`.
   - Set `payment.ConceptOverridden = true`.
   - If `payment.Status == PaymentStatus.Completed`:
     - Call `await RecalculatePendingInstallmentsAsync(payment.RegistrationId, adminUserId, ct)`.
4. If `request.ConceptDescription` is not null:
   - Build `ManualPaymentConceptLine(request.ConceptDescription, payment.Amount)`.
   - Serialize and set `payment.ConceptLinesSerialized`.
   - Set `payment.ConceptOverridden = true`.
5. If `request.DueDate` has value → set `payment.DueDate = request.DueDate`.
6. If `request.AdminNotes` is not null → set `payment.AdminNotes = request.AdminNotes`.
7. Set `payment.UpdatedAt = DateTime.UtcNow`.
8. Call `await paymentsRepo.UpdateAsync(payment, ct)`.
9. Log: `"Admin {AdminUserId} edited payment {PaymentId}: Amount={Amount}"`.
10. Load sibling payments and return `MapToAdminResponse(payment, siblingPayments)`.

---

#### 5b. `RecalculatePendingInstallmentsAsync` (internal helper, also exposed via interface)

```csharp
Task RecalculatePendingInstallmentsAsync(Guid registrationId, Guid adminUserId, CancellationToken ct)
```

**Logic**:
1. Load registration via `registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)`.
2. Load all payments tracked: `var allPayments = await paymentsRepo.GetByRegistrationIdTrackedAsync(registrationId, ct)`.
3. `decimal totalOwed = registration.BaseTotalAmount + registration.ExtrasAmount`.
4. `decimal completedNonManual = allPayments.Where(p => p.Status == PaymentStatus.Completed && !p.IsManual).Sum(p => p.Amount)`.
5. `decimal remaining = totalOwed - completedNonManual`.
6. `var pendingAuto = allPayments.Where(p => p.Status == PaymentStatus.Pending && !p.IsManual).OrderBy(p => p.InstallmentNumber).ToList()`.
7. For each pending auto payment:
   - If `remaining > 0`: assign `payment.Amount = remaining` for the first one, then check if there are more — for a 2-payment system (P1+P2 base), assign proportionally based on original amounts, or simply assign all remaining to the first pending payment and set the rest to 0.
   - **Simple rule** (sufficient for current 3-installment model): assign `remaining` entirely to the first pending auto payment; set all subsequent pending auto payments to `0.01m` (minimum, not 0 since there's a DB constraint) — actually, since we're removing the DB check constraint, set to `0`.
   - Actually: assign `remaining` to the first pending auto payment, then for each additional pending auto payment, set to `0` (or mark as already covered).
   - Decrement `remaining` by the amount assigned.
8. If `remaining < 0` after going through all pending payments (i.e., overpaid):
   - `surplusAmount = Math.Abs(remaining)`.
   - Call `await GenerateRefundPaymentAsync(registrationId, surplusAmount, "Devolución por ajuste de pago", adminUserId, ct)`.
9. Save all changed payments: `foreach (var p in pendingAuto) await paymentsRepo.UpdateAsync(p, ct)`.
10. Log: `"Recalculated pending installments for registration {RegistrationId}: remaining={Remaining}"`.

> **Note on amount = 0**: If a pending installment is set to 0 after recalculation, it means it's been "absorbed" by overpayment. Leave it at 0 with status Pending — the family doesn't owe anything for that installment. The admin can optionally confirm it at 0 or leave it as-is.

---

#### 5c. `ConfirmCombinedPaymentsAsync`

```csharp
Task<List<AdminPaymentResponse>> ConfirmCombinedPaymentsAsync(
    Guid registrationId, ConfirmCombinedPaymentsRequest request, Guid adminUserId, CancellationToken ct)
```

**Implementation steps**:
1. Load registration via `registrationsRepo.GetByIdAsync(registrationId, ct)` → throw `NotFoundException` if null.
2. Load all payments for registration: `var allPayments = await paymentsRepo.GetByRegistrationIdTrackedAsync(registrationId, ct)`.
3. `var targetPayments = allPayments.Where(p => request.PaymentIds.Contains(p.Id)).OrderBy(p => p.InstallmentNumber).ToList()`.
4. Validate: if `targetPayments.Count != request.PaymentIds.Count` → throw `NotFoundException("Uno o más pagos no se encontraron en esta inscripción")`.
5. Validate: all target payments must be in `Pending` or `PendingReview` status → throw `BusinessRuleException("Solo se pueden confirmar pagos en estado pendiente o en revisión")` if any fail.
6. **Amount distribution** (greedy fill in installment number order):
   ```
   remaining = request.TotalReceivedAmount
   foreach payment in targetPayments (ordered by InstallmentNumber):
       assign = min(remaining, payment.Amount)
       payment.Amount = assign
       remaining -= assign
   ```
7. After filling all listed payments, if `remaining > 0` and `request.ApplySurplusToNext == true`:
   - Find next pending auto payment not in `targetPayments` (lowest `InstallmentNumber`).
   - If found: reduce its amount by `remaining` (but not below 0). If it goes to 0, leave at 0.
8. For each target payment:
   - `payment.Status = PaymentStatus.Completed`
   - `payment.ConfirmedByUserId = adminUserId`
   - `payment.ConfirmedAt = DateTime.UtcNow`
   - `payment.AdminNotes = request.AdminNotes ?? payment.AdminNotes`
   - `payment.UpdatedAt = DateTime.UtcNow`
   - `await paymentsRepo.UpdateAsync(payment, ct)`
9. Update `Registration.Status` by calling the existing status-update helper (same logic as `ConfirmPaymentAsync` — check if all auto payments Completed → FullyPaid, etc.).
10. Log: `"Admin {AdminUserId} confirmed combined payments {PaymentIds} for registration {RegistrationId}, total={TotalAmount}"`.
11. Return `targetPayments.Select(p => MapToAdminResponse(p, allPayments)).ToList()`.

---

#### 5d. `GenerateRefundPaymentAsync` (internal helper — also expose via interface)

```csharp
Task<Payment> GenerateRefundPaymentAsync(
    Guid registrationId, decimal refundAmount, string reason, Guid adminUserId, CancellationToken ct)
```

**Implementation steps**:
1. Load all payments for registration to get next installment number.
2. `int nextInstallment = allPayments.Any() ? allPayments.Max(p => p.InstallmentNumber) + 1 : 4`.
3. Load registration to get family name and transfer concept prefix.
4. Build transfer concept: `"{prefix}-{familyName}-{nextInstallment}"` (same normalization as `CreateInstallmentsAsync`).
5. Build concept lines:
   ```csharp
   var lines = new PaymentConceptLinesJson(null, null,
       new ManualPaymentConceptLine(reason, refundAmount));
   ```
6. Create `Payment`:
   ```csharp
   new Payment
   {
       Id = Guid.NewGuid(),
       RegistrationId = registrationId,
       Amount = -refundAmount,      // negative amount
       PaymentDate = DateTime.UtcNow,
       Method = PaymentMethod.Transfer,
       Status = PaymentStatus.Refunded,
       InstallmentNumber = nextInstallment,
       TransferConcept = transferConcept,
       IsManual = true,
       ConceptLinesSerialized = Serialize(lines),
       AdminNotes = reason,
       ConfirmedByUserId = adminUserId,
       ConfirmedAt = DateTime.UtcNow,
       CreatedAt = DateTime.UtcNow,
       UpdatedAt = DateTime.UtcNow
   }
   ```
7. `await paymentsRepo.AddAsync(payment, ct)`.
8. Update `registration.TotalAmount -= refundAmount; await registrationsRepo.UpdateAsync(registration, ct)`.
9. Log: `"Generated refund payment {PaymentId} of {Amount}€ for registration {RegistrationId}: {Reason}"`.
10. Return the created payment.

---

#### 5e. Update `IPaymentsService` interface

Add these signatures:

```csharp
Task<AdminPaymentResponse> AdminEditPaymentAsync(
    Guid paymentId, AdminEditPaymentRequest request, Guid adminUserId, CancellationToken ct);

Task<List<AdminPaymentResponse>> ConfirmCombinedPaymentsAsync(
    Guid registrationId, ConfirmCombinedPaymentsRequest request, Guid adminUserId, CancellationToken ct);

Task RecalculatePendingInstallmentsAsync(Guid registrationId, Guid adminUserId, CancellationToken ct);
```

`GenerateRefundPaymentAsync` stays internal (private method of `PaymentsService`) since it's always called from within the service.

---

### Step 6: Add New Admin Endpoints in `PaymentsEndpoints.cs`

**File**: `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs`

#### 6a. Registration of new routes (inside `MapPaymentsEndpoints`)

In the existing `admin` group (`/api/admin/payments`):

```csharp
admin.MapPut("/{paymentId:guid}", AdminEditPayment)
    .WithName("AdminEditPayment")
    .WithSummary("Edit any payment (admin)")
    .AddEndpointFilter<ValidationFilter<AdminEditPaymentRequest>>()
    .Produces<ApiResponse<AdminPaymentResponse>>()
    .Produces(403).Produces(404).Produces(409);
```

In the existing `adminReg` group (`/api/admin/registrations`):

```csharp
adminReg.MapPost("/{registrationId:guid}/payments/confirm-combined", ConfirmCombinedPayments)
    .WithName("ConfirmCombinedPayments")
    .WithSummary("Confirm multiple payments from a single transfer (admin)")
    .AddEndpointFilter<ValidationFilter<ConfirmCombinedPaymentsRequest>>()
    .Produces<ApiResponse<List<AdminPaymentResponse>>>()
    .Produces(403).Produces(404).Produces(409);
```

#### 6b. Handler: `AdminEditPayment`

```csharp
private static async Task<IResult> AdminEditPayment(
    Guid paymentId,
    AdminEditPaymentRequest request,
    ClaimsPrincipal user,
    IPaymentsService service,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var userRole = user.GetUserRole();

    if (userRole is not ("Admin" or "Board"))
        return TypedResults.Forbid();

    try
    {
        var result = await service.AdminEditPaymentAsync(paymentId, request, userId, ct);
        return TypedResults.Ok(ApiResponse<AdminPaymentResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex)
    {
        return TypedResults.Conflict(ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
    }
}
```

#### 6c. Handler: `ConfirmCombinedPayments`

```csharp
private static async Task<IResult> ConfirmCombinedPayments(
    Guid registrationId,
    ConfirmCombinedPaymentsRequest request,
    ClaimsPrincipal user,
    IPaymentsService service,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var userRole = user.GetUserRole();

    if (userRole is not ("Admin" or "Board"))
        return TypedResults.Forbid();

    try
    {
        var result = await service.ConfirmCombinedPaymentsAsync(registrationId, request, userId, ct);
        return TypedResults.Ok(ApiResponse<List<AdminPaymentResponse>>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex)
    {
        return TypedResults.Conflict(ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
    }
}
```

---

### Step 7: Add Admin Member-Update Endpoint in `RegistrationsEndpoints.cs`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

#### 7a. Register new route

In the existing admin registrations group (find where `MapGroup("/api/admin/registrations")` is defined, or add a new group):

```csharp
var adminReg = app.MapGroup("/api/admin/registrations")
    .WithTags("Registrations Admin")
    .WithOpenApi()
    .RequireAuthorization();

adminReg.MapPut("/{id:guid}/members", AdminUpdateRegistrationMembers)
    .WithName("AdminUpdateRegistrationMembers")
    .WithSummary("Update registration members (admin — triggers refund if payments exist)")
    .AddEndpointFilter<ValidationFilter<UpdateRegistrationMembersRequest>>()
    .Produces<ApiResponse<RegistrationResponse>>()
    .Produces(400).Produces(403).Produces(404).Produces(422);
```

#### 7b. Handler: `AdminUpdateRegistrationMembers`

```csharp
private static async Task<IResult> AdminUpdateRegistrationMembers(
    Guid id,
    UpdateRegistrationMembersRequest request,
    RegistrationsService service,
    ClaimsPrincipal user,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var userRole = user.GetUserRole();

    if (userRole is not ("Admin" or "Board"))
        return TypedResults.Forbid();

    try
    {
        var result = await service.AdminUpdateMembersAsync(id, userId, request, ct);
        return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex)
    {
        return TypedResults.UnprocessableEntity(
            ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE"));
    }
}
```

---

### Step 8: Implement `AdminUpdateMembersAsync` in `RegistrationsService.cs`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**New method**: `AdminUpdateMembersAsync`

```csharp
public async Task<RegistrationResponse> AdminUpdateMembersAsync(
    Guid registrationId, Guid adminUserId, UpdateRegistrationMembersRequest request, CancellationToken ct)
```

**Implementation steps**:
1. Load registration with details and payments: `registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)` → throw `NotFoundException` if null.
2. Identify removed members: `var removedMemberIds = registration.Members.Select(m => m.FamilyMemberId).Except(request.Members.Select(m => m.FamilyMemberId)).ToList()`.
3. **Business rule**: must have at least one adult (AgeCategory == Adult) in the remaining members. Load `FamilyMember` records to check `AgeAtCamp` / existing `RegistrationMember.AgeCategory`. If no Adult remains after removal → throw `BusinessRuleException("La inscripción debe tener al menos un adulto responsable")`.
4. `decimal removedAmount = registration.Members.Where(m => removedMemberIds.Contains(m.FamilyMemberId)).Sum(m => m.IndividualAmount)`.
5. `decimal newBaseTotalAmount = registration.BaseTotalAmount - removedAmount`.
6. `decimal completedBasePayments = registration.Payments.Where(p => p.Status == PaymentStatus.Completed && !p.IsManual && p.InstallmentNumber <= 2).Sum(p => p.Amount)`.
7. **Call existing member update logic** (reuse internal steps from `UpdateMembersAsync`, or call a shared private method):
   - Remove old `RegistrationMember` records.
   - Add new `RegistrationMember` records (same pricing/age calculation as existing `UpdateMembersAsync`).
   - Update `registration.BaseTotalAmount = newBaseTotalAmount`.
   - Update `registration.TotalAmount = newBaseTotalAmount + registration.ExtrasAmount`.
8. If `completedBasePayments > newBaseTotalAmount`:
   - `decimal refundAmount = completedBasePayments - newBaseTotalAmount`.
   - Call `paymentsService.GenerateRefundPaymentAsync(registrationId, refundAmount, $"Devolución por baja de participante", adminUserId, ct)`.
   - Note: `GenerateRefundPaymentAsync` is a private method on `PaymentsService`, not on `IPaymentsService`. To allow `RegistrationsService` to call it, either:
     - **Option A (preferred)**: Expose it on `IPaymentsService` interface.
     - **Option B**: Move the refund logic into `RegistrationsService` directly (duplicates some code).
   - Use **Option A**: add `Task<Payment> GenerateRefundPaymentAsync(Guid registrationId, decimal refundAmount, string reason, Guid adminUserId, CancellationToken ct)` to `IPaymentsService`.
9. Sync pending P2 if not yet Completed:
   - `paymentsService.SyncBaseInstallmentsAsync(registrationId, newBaseTotalAmount, registration.BaseTotalAmount, ct)`.
   - This existing method already handles recalculating P1/P2 when base total changes.
10. Determine `DraftTargetStatus`:
    - If `completedBasePayments > 0` or any payment in `Completed` state → `DraftTargetStatus = RegistrationStatus.PartiallyPaid`.
    - Otherwise → `DraftTargetStatus = RegistrationStatus.Pending`.
11. Set registration to Draft + pending acknowledgement:
    ```csharp
    registration.Status = RegistrationStatus.Draft;
    registration.DraftTargetStatus = draftTargetStatus;
    registration.HasPendingUserAcknowledgement = true;
    registration.AdminModifiedAt = DateTime.UtcNow;
    ```
12. Save registration.
13. Log: `"Admin {AdminUserId} updated members for registration {RegistrationId}. Removed {Count} members, refund={RefundAmount}€"`.
14. Reload and return `RegistrationResponse`.

> **Note**: `RegistrationsService` receives `IPaymentsService` via constructor injection. Add it if not already present.

---

### Step 9: Generate EF Core Migration

**Action**: Create a database migration for the two new columns and the dropped check constraint.

```bash
dotnet ef migrations add AddPaymentAuditFields --project src/Abuvi.API
```

**Review the generated migration** to verify:
- `concept_overridden` column added (`boolean NOT NULL DEFAULT false`)
- `original_amount` column added (`numeric(10,2) NULL`)
- `CK_Payments_Amount` check constraint is dropped

If EF doesn't auto-detect the constraint removal, add it manually to the migration `Up()`:

```csharp
migrationBuilder.Sql("ALTER TABLE payments DROP CONSTRAINT IF EXISTS \"CK_Payments_Amount\";");
```

Apply migration:
```bash
dotnet ef database update --project src/Abuvi.API
```

---

### Step 10: Write Unit Tests

**File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs`  
**File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`

#### 10a. `AdminEditPaymentAsync` tests

```
AdminEditPaymentAsync_WhenPaymentPending_UpdatesAmountWithoutRecalculation
AdminEditPaymentAsync_WhenPaymentCompleted_TriggersRecalculation
AdminEditPaymentAsync_WhenFirstEdit_SnapshotsOriginalAmount
AdminEditPaymentAsync_WhenSecondEdit_DoesNotOverwriteOriginalAmount
AdminEditPaymentAsync_WhenConceptProvided_ReplacesConceptLines
AdminEditPaymentAsync_WhenPaymentFailed_ThrowsBusinessRuleException
AdminEditPaymentAsync_WhenPaymentRefunded_ThrowsBusinessRuleException
AdminEditPaymentAsync_WhenAmountNotProvided_DoesNotUpdateAmount
AdminEditPaymentAsync_SetsConceptOverriddenToTrue
```

#### 10b. `RecalculatePendingInstallmentsAsync` tests

```
RecalculatePendingInstallments_WhenP1EditedHigher_ReducesP2Accordingly
RecalculatePendingInstallments_WhenP1EditedToFullTotal_SetsP2ToZero
RecalculatePendingInstallments_WhenOverpaid_GeneratesRefundPayment
RecalculatePendingInstallments_WhenNoPendingPayments_DoesNothing
RecalculatePendingInstallments_SkipsManualPayments
RecalculatePendingInstallments_ExactMatch_NoRecalculationNeeded
```

#### 10c. `ConfirmCombinedPaymentsAsync` tests

```
ConfirmCombinedPayments_ExactAmountMatch_ConfirmsAtOriginalAmounts
ConfirmCombinedPayments_SurplusAmount_GreedyFillsInOrder
ConfirmCombinedPayments_WhenApplySurplusToNext_ReducesNextPending
ConfirmCombinedPayments_PaymentNotInRegistration_ThrowsNotFoundException
ConfirmCombinedPayments_PaymentAlreadyCompleted_ThrowsBusinessRuleException
ConfirmCombinedPayments_SetsConfirmedByAndConfirmedAt
```

#### 10d. `GenerateRefundPaymentAsync` tests

```
GenerateRefundPayment_CreatesNegativeAmountPayment
GenerateRefundPayment_SetsIsManualTrue
GenerateRefundPayment_SetsStatusRefunded
GenerateRefundPayment_AssignsNextInstallmentNumber
GenerateRefundPayment_UpdatesRegistrationTotalAmount
```

#### 10e. `AdminUpdateMembersAsync` tests

```
AdminUpdateMembers_RemovesNonMemberWithNoPaidPayments_RecalculatesWithNoRefund
AdminUpdateMembers_RemovesNonMemberWithPaidP1_GeneratesRefund
AdminUpdateMembers_RemovingLastAdult_ThrowsBusinessRuleException
AdminUpdateMembers_SetsRegistrationToDraft
AdminUpdateMembers_SetsDraftTargetStatus
AdminUpdateMembers_SetsHasPendingUserAcknowledgement
```

**Test setup pattern** (follow existing project conventions with NSubstitute + FluentAssertions):

```csharp
public class PaymentsServiceTests
{
    private readonly IPaymentsRepository _paymentsRepo = Substitute.For<IPaymentsRepository>();
    private readonly IRegistrationsRepository _registrationsRepo = Substitute.For<IRegistrationsRepository>();
    // ... other dependencies
    private readonly PaymentsService _sut;

    public PaymentsServiceTests()
    {
        _sut = new PaymentsService(_paymentsRepo, _registrationsRepo, ...);
    }

    [Fact]
    public async Task AdminEditPaymentAsync_WhenPaymentFailed_ThrowsBusinessRuleException()
    {
        // Arrange
        var payment = new PaymentBuilder().WithStatus(PaymentStatus.Failed).Build();
        _paymentsRepo.GetByIdWithRegistrationAsync(payment.Id, Arg.Any<CancellationToken>())
            .Returns(payment);

        // Act
        var act = () => _sut.AdminEditPaymentAsync(
            payment.Id, new AdminEditPaymentRequest { Amount = 100 }, Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}
```

---

### Step 11: Update Technical Documentation

**Action**: Update the following documentation files to reflect all changes made.

**Implementation steps**:

1. **`ai-specs/specs/data-model.md`**:
   - Add `concept_overridden` and `original_amount` to the `Payment` entity section.
   - Note: `amount` field can now be negative (for refund payments where `is_manual = true`).

2. **`ai-specs/specs/api-endpoints.md`**:
   - Add `PUT /api/admin/payments/{id}` — edit any payment.
   - Add `POST /api/admin/registrations/{id}/payments/confirm-combined` — confirm combined payments.
   - Add `PUT /api/admin/registrations/{id}/members` — admin member update.
   - Note that `AdminPaymentResponse` now includes `conceptOverridden` and `originalAmount`.

3. **No `*-standards.mdc` changes needed** — no new libraries, patterns, or architectural changes introduced.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `ConceptOverridden`, `OriginalAmount` to `Payment` entity + EF configuration (includes removing check constraint from config)
3. Step 2 — Add new DTOs (`AdminEditPaymentRequest`, `ConfirmCombinedPaymentsRequest`, extend `AdminPaymentResponse`)
4. Step 3 — Add validators for both new request types
5. Step 4 — Verify/add `GetByRegistrationIdTrackedAsync` to repository if missing
6. Step 5 — Implement service methods: `AdminEditPaymentAsync`, `RecalculatePendingInstallmentsAsync`, `ConfirmCombinedPaymentsAsync`, `GenerateRefundPaymentAsync` + update `IPaymentsService`
7. Step 6 — Add new endpoints in `PaymentsEndpoints.cs`
8. Step 7 — Add admin member-update endpoint in `RegistrationsEndpoints.cs`
9. Step 8 — Implement `AdminUpdateMembersAsync` in `RegistrationsService.cs`
10. Step 9 — Generate and review EF migration (includes dropping check constraint)
11. Step 10 — Write unit tests
12. Step 11 — Update documentation

---

## Testing Checklist

- [ ] `AdminEditPaymentAsync` — all 9 test cases pass
- [ ] `RecalculatePendingInstallmentsAsync` — all 6 test cases pass
- [ ] `ConfirmCombinedPaymentsAsync` — all 6 test cases pass
- [ ] `GenerateRefundPaymentAsync` — all 5 test cases pass
- [ ] `AdminUpdateMembersAsync` — all 6 test cases pass
- [ ] No regressions in existing payment tests (`ConfirmPaymentAsync`, `SyncBaseInstallmentsAsync`, `CreateManualPaymentAsync`)
- [ ] EF migration applied cleanly to a fresh database
- [ ] `dotnet build` with zero warnings (TreatWarningsAsErrors = true)
- [ ] `dotnet test` — all tests pass, ≥ 90% coverage

---

## Error Response Format

All endpoints use `ApiResponse<T>` envelope:

```json
// Success
{ "success": true, "data": { ... }, "error": null }

// Validation error (400)
{ "success": false, "data": null, "error": { "message": "Validation failed", "code": "VALIDATION_ERROR" } }

// Not found (404)
{ "success": false, "data": null, "error": { "message": "Pago no encontrado", "code": "NOT_FOUND" } }

// Business rule / conflict (409)
{ "success": false, "data": null, "error": { "message": "No se puede editar un pago en estado fallido o devuelto", "code": "BUSINESS_RULE" } }

// Unprocessable (422)
{ "success": false, "data": null, "error": { "message": "La inscripción debe tener al menos un adulto responsable", "code": "BUSINESS_RULE" } }
```

---

## Dependencies

**No new NuGet packages required.** All functionality uses existing dependencies.

**EF migration command**:
```bash
dotnet ef migrations add AddPaymentAuditFields --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Notes

### Business Rules to Enforce

- A `Failed` or `Refunded` payment cannot be edited (UC1 guard).
- `OriginalAmount` is a write-once snapshot — never overwrite after first set.
- `RecalculatePendingInstallmentsAsync` must never touch `IsManual = true` payments.
- `ConfirmCombinedPaymentsAsync` bypasses sequential-ordering validation (admin override).
- Admin member update (`AdminUpdateMembersAsync`) always transitions to `Draft` status when any payment is already `Completed`, ensuring family rep must acknowledge the financial change.
- At least one adult must remain in a registration after member removal.

### Language Requirements

- All user-facing error messages: **Spanish** (e.g., "No se puede editar un pago en estado fallido o devuelto").
- All log messages: **English**.
- All code, comments, identifiers: **English**.

### Data Integrity

- `GenerateRefundPaymentAsync` must run atomically with registration total update — both in the same `SaveChangesAsync` call or via the repository's `UpdateAsync` chain. Since we save sequentially (add payment, then update registration), ensure both succeed or use a DB transaction at service level if needed.
- Negative payment amounts (`Amount < 0`) are now allowed at the DB level (check constraint removed). Validation still enforces `Amount > 0` for `AdminEditPaymentRequest` and `CreateManualPaymentRequest` — refunds are only created internally.

### Cross-Service Dependency

`RegistrationsService` calls `IPaymentsService.GenerateRefundPaymentAsync` and `IPaymentsService.SyncBaseInstallmentsAsync`. Ensure `IPaymentsService` is injected into `RegistrationsService` constructor (add to DI registration in `Program.cs` if not already present).

---

## Next Steps After Implementation

1. Frontend: surface `ConceptOverridden` / `OriginalAmount` in admin payment detail view.
2. Frontend: add "Edit Payment" modal for admin.
3. Frontend: add "Confirm Combined" button on admin registration payments view.
4. Frontend: admin member-edit panel that triggers the new `PUT /api/admin/registrations/{id}/members` endpoint.
5. Notification: consider emailing the family rep when registration transitions to `Draft` with financial changes (may already be implemented by existing Draft notification flow).

---

## Implementation Verification

- [ ] **Code Quality**: No C# analyzer warnings; nullable reference types satisfied; TreatWarningsAsErrors passes.
- [ ] **Functionality**: All 3 new endpoints return correct status codes for all scenarios.
- [ ] **Testing**: ≥ 90% coverage (branches, functions, lines, statements) via xUnit + FluentAssertions + NSubstitute.
- [ ] **Integration**: EF Core migration applied successfully; no constraint violations on existing data.
- [ ] **Documentation**: `data-model.md` and `api-endpoints.md` updated.

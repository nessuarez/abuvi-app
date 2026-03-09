# Backend Implementation Plan: feat-3-payments — Three-Installment Payment System

## Overview

Fixes the invariant `P1 + P2 == Registration.TotalAmount` which breaks when extras are added after registration creation. Introduces a dedicated third payment (P3) for extras and adds payment sync logic when members or extras change.

Architecture: Vertical Slice Architecture — changes span the `Payments`, `Registrations`, and `Camps` feature slices. No new endpoints are created; only service/repository methods are added or modified.

---

## Architecture Context

**Feature slices affected:**
- `src/Abuvi.API/Features/Payments/` — new sync methods, repository changes
- `src/Abuvi.API/Features/Registrations/` — guards + sync calls in edit methods
- `src/Abuvi.API/Features/Camps/` — new `ExtrasPaymentDeadline` field

**Cross-cutting:** EF Core migration for `camp_editions.extras_payment_deadline`.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch**: `feature/feat-3-payments-backend`
- **Base**: `dev` (current working branch)

```bash
git checkout dev && git pull origin dev
git checkout -b feature/feat-3-payments-backend
```

---

### Step 1: Add `ExtrasPaymentDeadline` to `CampEdition`

**File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add to the `CampEdition` entity class (after `SecondPaymentDeadline`):
```csharp
// null = use CampEdition.StartDate as fallback
public DateTime? ExtrasPaymentDeadline { get; set; }
```

Add to all DTOs that include `FirstPaymentDeadline` and `SecondPaymentDeadline`:
- `ProposeCampEditionRequest` record — add `DateTime? ExtrasPaymentDeadline`
- `UpdateCampEditionRequest` record — add `DateTime? ExtrasPaymentDeadline`
- `CampEditionResponse` record — add `DateTime? ExtrasPaymentDeadline`
- `ActiveCampEditionResponse` record — add `DateTime? ExtrasPaymentDeadline`
- `CurrentCampEditionResponse` record — add `DateTime? ExtrasPaymentDeadline`

---

**File**: `src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs`

Add after the `second_payment_deadline` mapping:
```csharp
builder.Property(e => e.ExtrasPaymentDeadline)
    .HasColumnName("extras_payment_deadline");
```

---

**File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`

In `CreateAsync` / `ProposeAsync`: assign `ExtrasPaymentDeadline = request.ExtrasPaymentDeadline`.

In `UpdateAsync`: add the assignment alongside the other two deadline fields (currently lines 265-266):
```csharp
edition.ExtrasPaymentDeadline = request.ExtrasPaymentDeadline;
```

In `MapToCampEditionResponse()` (and equivalent mappers): include `ExtrasPaymentDeadline` in the returned DTO.

---

**Migration**:
```bash
cd src/Abuvi.API
dotnet ef migrations add AddExtrasPaymentDeadlineToCampEdition --project ../Abuvi.Infrastructure
```

Expected migration: adds nullable `TIMESTAMP WITH TIME ZONE extras_payment_deadline` column to `camp_editions` table.

---

### Step 2: Add `DeleteAsync(Guid paymentId)` to Payments Repository

Needed to delete P3 when all extras are removed.

**File**: `src/Abuvi.API/Features/Payments/IPaymentsRepository.cs`

```csharp
Task DeleteAsync(Guid paymentId, CancellationToken ct);
```

**File**: `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`

```csharp
public async Task DeleteAsync(Guid paymentId, CancellationToken ct)
    => await db.Payments
        .Where(p => p.Id == paymentId)
        .ExecuteDeleteAsync(ct);
```

---

### Step 3: Add `GetByRegistrationIdTrackedAsync` to Payments Repository

The existing `GetByRegistrationIdAsync` uses `AsNoTracking()` — entities loaded this way are not tracked by EF Core, so `UpdateAsync` (which calls `SaveChangesAsync()`) would not persist changes.
The sync methods need a tracked version.

**File**: `src/Abuvi.API/Features/Payments/IPaymentsRepository.cs`

```csharp
Task<List<Payment>> GetByRegistrationIdTrackedAsync(Guid registrationId, CancellationToken ct);
```

**File**: `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`

```csharp
public async Task<List<Payment>> GetByRegistrationIdTrackedAsync(Guid registrationId, CancellationToken ct)
    => await db.Payments
        .Where(p => p.RegistrationId == registrationId)
        .OrderBy(p => p.InstallmentNumber)
        .ToListAsync(ct); // No AsNoTracking — entities remain tracked
```

---

### Step 4: Update `CreateInstallmentsAsync` to Use CampEdition Deadlines

**File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`

Replace the current hardcoded due dates with CampEdition-specific deadlines:

```csharp
// P1 DueDate: use CampEdition.FirstPaymentDeadline if set, else registration date (now)
var dueDate1 = registration.CampEdition.FirstPaymentDeadline ?? DateTime.UtcNow;

// P2 DueDate: use CampEdition.SecondPaymentDeadline if set,
// else fall back to StartDate - SecondInstallmentDaysBefore from settings
var dueDate2 = registration.CampEdition.SecondPaymentDeadline
    ?? registration.CampEdition.StartDate.AddDays(-settings.SecondInstallmentDaysBefore);
```

Update the `Payment` constructors:
- P1: `DueDate = dueDate1`
- P2: `DueDate = dueDate2`

Note: `PaymentDate` on both payments remains `DateTime.UtcNow` (the timestamp of registration creation, not the due date).

---

### Step 5: Add `SyncExtrasInstallmentAsync` to PaymentsService

**File**: `src/Abuvi.API/Features/Payments/IPaymentsService.cs`

```csharp
/// <summary>
/// Creates, updates, or deletes the extras installment (P3) for a registration.
/// Call after SetExtrasAsync completes and registration totals are updated.
/// </summary>
Task<PaymentResponse?> SyncExtrasInstallmentAsync(
    Guid registrationId, decimal extrasAmount, CancellationToken ct);
```

**File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`

```csharp
public async Task<PaymentResponse?> SyncExtrasInstallmentAsync(
    Guid registrationId, decimal extrasAmount, CancellationToken ct)
{
    var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    var settings = await LoadPaymentSettingsAsync(ct);
    var familyName = NormalizeName(registration.FamilyUnit.Name);
    var prefix = settings.TransferConceptPrefix;

    // P3 DueDate: use CampEdition.ExtrasPaymentDeadline if set, else StartDate
    var dueDate = registration.CampEdition.ExtrasPaymentDeadline
        ?? registration.CampEdition.StartDate;

    // Load tracked payments (needed for UpdateAsync to persist changes)
    var payments = await paymentsRepo.GetByRegistrationIdTrackedAsync(registrationId, ct);
    var p3 = payments.FirstOrDefault(p => p.InstallmentNumber == 3);

    if (extrasAmount > 0)
    {
        if (p3 is not null)
        {
            // Guard: cannot modify if under review or already paid
            if (p3.Status is PaymentStatus.PendingReview or PaymentStatus.Completed)
                throw new BusinessRuleException(
                    "No se pueden modificar los extras porque el pago de extras ya está en revisión o ha sido confirmado.");

            p3.Amount = extrasAmount;
            p3.DueDate = dueDate;
            await paymentsRepo.UpdateAsync(p3, ct);
        }
        else
        {
            var concept = $"{prefix}-{familyName}-3";
            if (concept.Length > 100) concept = concept[..100];

            var newP3 = new Payment
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                Amount = extrasAmount,
                PaymentDate = DateTime.UtcNow,
                Method = PaymentMethod.Transfer,
                Status = PaymentStatus.Pending,
                InstallmentNumber = 3,
                DueDate = dueDate,
                TransferConcept = concept,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await paymentsRepo.AddAsync(newP3, ct);
            return MapToResponse(newP3);
        }
    }
    else if (p3 is not null) // extrasAmount == 0 and P3 exists
    {
        if (p3.Status is PaymentStatus.PendingReview or PaymentStatus.Completed)
            throw new BusinessRuleException(
                "No se pueden eliminar los extras porque el pago de extras ya está en revisión o ha sido confirmado.");

        await paymentsRepo.DeleteAsync(p3.Id, ct);
        return null;
    }

    return p3 is not null ? MapToResponse(p3) : null;
}
```

---

### Step 6: Add `SyncBaseInstallmentsAsync` to PaymentsService

**File**: `src/Abuvi.API/Features/Payments/IPaymentsService.cs`

```csharp
/// <summary>
/// Recalculates P1 and/or P2 when the base member total changes.
/// oldBaseTotalAmount must be captured BEFORE the registration entity is modified.
/// P3 is never touched by this method.
/// </summary>
Task SyncBaseInstallmentsAsync(
    Guid registrationId, decimal newBaseTotalAmount, decimal oldBaseTotalAmount, CancellationToken ct);
```

**File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`

```csharp
public async Task SyncBaseInstallmentsAsync(
    Guid registrationId, decimal newBaseTotalAmount, decimal oldBaseTotalAmount, CancellationToken ct)
{
    // Load tracked payments (needed for UpdateAsync to persist changes)
    var payments = await paymentsRepo.GetByRegistrationIdTrackedAsync(registrationId, ct);

    var p1 = payments.FirstOrDefault(p => p.InstallmentNumber == 1)
        ?? throw new InvalidOperationException($"P1 not found for registration {registrationId}");
    var p2 = payments.FirstOrDefault(p => p.InstallmentNumber == 2)
        ?? throw new InvalidOperationException($"P2 not found for registration {registrationId}");

    if (p1.Status == PaymentStatus.PendingReview)
        throw new BusinessRuleException(
            "No se pueden modificar los miembros porque el primer pago está en revisión.");

    if (p1.Status == PaymentStatus.Pending)
    {
        // Both clean: recalculate 50/50 split
        p1.Amount = Math.Ceiling(newBaseTotalAmount / 2m);
        p2.Amount = newBaseTotalAmount - p1.Amount;
        await paymentsRepo.UpdateAsync(p1, ct);
        await paymentsRepo.UpdateAsync(p2, ct);
    }
    else if (p1.Status == PaymentStatus.Completed)
    {
        // P1 already paid: absorb delta into P2
        if (p2.Status is PaymentStatus.PendingReview or PaymentStatus.Completed)
            throw new BusinessRuleException(
                "No se pueden modificar los miembros porque ambos plazos ya están en revisión o confirmados.");

        var delta = newBaseTotalAmount - oldBaseTotalAmount;
        var newP2Amount = p2.Amount + delta;

        if (newP2Amount <= 0)
            throw new BusinessRuleException(
                "La reducción supera el importe del segundo plazo. Contacta con la administración.");

        p2.Amount = newP2Amount;
        await paymentsRepo.UpdateAsync(p2, ct);
    }

    logger.LogInformation(
        "Synced base installments for registration {RegistrationId}. NewBase: {NewBase}, OldBase: {OldBase}",
        registrationId, newBaseTotalAmount, oldBaseTotalAmount);
}
```

---

### Step 7: Update `UpdateMembersAsync` in RegistrationsService

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**Add proof guard** after the existing status check (step 3, around line 194):

```csharp
// 3b. Guard: block edit if any payment has a proof uploaded
var existingPayments = await paymentsRepo.GetByRegistrationIdAsync(registrationId, ct);
if (existingPayments.Any(p => p.ProofFileUrl != null))
    throw new BusinessRuleException(
        "No se puede modificar la inscripción porque ya hay pagos con justificante subido.");
```

Note: `UpdateMembersAsync` currently loads registration via `GetByIdAsync` (without payments), so an explicit payment query is needed here. Use the existing `GetByRegistrationIdAsync` — it's read-only (no need for tracking here).

**Capture `oldBase` before update** (insert before line 285):

```csharp
var oldBaseTotalAmount = registration.BaseTotalAmount; // capture before overwriting
```

**Call sync after `UpdateAsync`** (after line 291, before the reload):

```csharp
// Sync P1 and P2 amounts to reflect new base total
await paymentsService.SyncBaseInstallmentsAsync(
    registrationId, baseTotalAmount, oldBaseTotalAmount, ct);
```

Note: `paymentsService` needs to be injected into `RegistrationsService`. Add it to the constructor parameter list and the DI registration if not already present.

---

### Step 8: Update `SetExtrasAsync` in RegistrationsService

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

`SetExtrasAsync` already loads `GetByIdWithDetailsAsync` which includes `registration.Payments`. Use this to add the proof guard.

**Add proof guard** after the status check (step 3, around line 317):

```csharp
// 3b. Guard: block edit if any payment has a proof uploaded
if (registration.Payments?.Any(p => p.ProofFileUrl != null) == true)
    throw new BusinessRuleException(
        "No se puede modificar los extras porque ya hay pagos con justificante subido.");
```

**Call sync after `UpdateAsync`** (after line 362, before the reload):

```csharp
// Sync P3 (extras installment): create, update, or delete as needed
var newExtrasAmount = newExtras.Sum(e => e.TotalAmount);
await paymentsService.SyncExtrasInstallmentAsync(registrationId, newExtrasAmount, ct);
```

---

### Step 9: Update `AdminUpdateAsync` in RegistrationsService

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

After `AdminUpdateAsync` calls `SaveChangesAsync` / `UpdateAsync`, add sync calls for whichever totals changed:

```csharp
// Sync payments if extras changed
if (extrasChanged) // track whether the extras were modified in AdminUpdateAsync
    await paymentsService.SyncExtrasInstallmentAsync(registrationId, registration.ExtrasAmount, ct);

// Sync payments if base changed
if (baseChanged)
    await paymentsService.SyncBaseInstallmentsAsync(
        registrationId, registration.BaseTotalAmount, oldBaseTotalAmount, ct);
```

Determine `extrasChanged` and `baseChanged` by comparing new vs. old amounts before the update — same pattern as `UpdateMembersAsync`.

---

### Step 10: Verify `PaymentConfiguration.cs` — No `InstallmentNumber` Constraint

**File**: `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs`

Confirmed: there is no `CHECK` constraint limiting `InstallmentNumber <= 2`. The existing constraint is only `amount > 0`. P3 (`InstallmentNumber = 3`) will insert without schema changes.

---

### Step 11: Write Unit Tests

**File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsService_SyncTests.cs` (new file)

Follow the project's AAA pattern with NSubstitute mocks. Test class setup mirrors `RegistrationsService_DeleteAsync_Tests.cs`:

```csharp
public class PaymentsService_SyncExtrasInstallmentAsync_Tests
{
    private readonly IPaymentsRepository _paymentsRepo;
    private readonly IRegistrationsRepository _registrationsRepo;
    private readonly IAssociationSettingsRepository _settingsRepo;
    private readonly PaymentsService _sut;

    public PaymentsService_SyncExtrasInstallmentAsync_Tests()
    {
        _paymentsRepo = Substitute.For<IPaymentsRepository>();
        _registrationsRepo = Substitute.For<IRegistrationsRepository>();
        _settingsRepo = Substitute.For<IAssociationSettingsRepository>();
        var logger = Substitute.For<ILogger<PaymentsService>>();
        var blobStorage = Substitute.For<IBlobStorageService>();
        _sut = new PaymentsService(_paymentsRepo, _registrationsRepo, _settingsRepo, blobStorage, logger);
    }
    // ...
}
```

#### `SyncExtrasInstallmentAsync` Tests (6)

| Test | Scenario | Expected |
|------|----------|----------|
| `SyncExtras_NoP3Exists_PositiveExtras_CreatesP3WithCorrectAmount` | No P3, extrasAmount = 50 | `paymentsRepo.AddAsync` called with Amount=50, InstallmentNumber=3 |
| `SyncExtras_P3ExistsPending_PositiveExtras_UpdatesAmount` | P3 Pending, extrasAmount changes | `paymentsRepo.UpdateAsync` called with new Amount |
| `SyncExtras_P3ExistsPending_ZeroExtras_DeletesP3` | P3 Pending, extrasAmount = 0 | `paymentsRepo.DeleteAsync(p3.Id)` called |
| `SyncExtras_P3PendingReview_Throws` | P3 PendingReview, extrasAmount > 0 | `BusinessRuleException` |
| `SyncExtras_P3Completed_Throws` | P3 Completed, extrasAmount = 0 | `BusinessRuleException` |
| `SyncExtras_DueDateReflectsCampEditionExtrasDeadline` | `ExtrasPaymentDeadline` set on edition | P3 DueDate matches `CampEdition.ExtrasPaymentDeadline` |

#### `SyncBaseInstallmentsAsync` Tests (7)

| Test | Scenario | Expected |
|------|----------|----------|
| `SyncBase_BothPending_RecalculatesBoth` | P1 Pending, P2 Pending, newBase=600 | P1=300, P2=300 |
| `SyncBase_BothPending_OddAmount_RoundsP1Up` | newBase=601 | P1=301, P2=300 |
| `SyncBase_P1Completed_P2Pending_AbsorbsDelta` | P1 Completed, P2=300, delta=+100 | P2=400 |
| `SyncBase_P1Completed_P2Pending_NegativeDelta_P2DecreasesCorrectly` | P1 Completed, P2=300, delta=-100 | P2=200 |
| `SyncBase_P1Completed_P2Pending_DeltaWouldMakeP2NonPositive_Throws` | P1 Completed, P2=100, delta=-200 | `BusinessRuleException` |
| `SyncBase_P1PendingReview_Throws` | P1 PendingReview | `BusinessRuleException` |
| `SyncBase_P1Completed_P2PendingReview_Throws` | P1 Completed, P2 PendingReview | `BusinessRuleException` |

---

### Step 12: Update Technical Documentation

- **`ai-specs/specs/data-model.md`**: Add `extras_payment_deadline` to the `camp_editions` table description and document P3 payment lifecycle.
- **`ai-specs/specs/api-spec.yml`**: Update `CampEditionResponse`, `UpdateCampEditionRequest`, `ProposeCampEditionRequest` schemas to include `extrasPaymentDeadline`. Update `PaymentResponse` docs to note `installmentNumber` can be 3.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `ExtrasPaymentDeadline` to CampEdition (entity + DTOs + service + config)
3. Step 2 — Add `DeleteAsync(Guid paymentId)` to payments repository
4. Step 3 — Add `GetByRegistrationIdTrackedAsync` to payments repository
5. Step 4 — Update `CreateInstallmentsAsync` to use CampEdition deadlines
6. Step 5 — Implement `SyncExtrasInstallmentAsync`
7. Step 6 — Implement `SyncBaseInstallmentsAsync`
8. Step 7 — Update `UpdateMembersAsync` (proof guard + sync call)
9. Step 8 — Update `SetExtrasAsync` (proof guard + sync call)
10. Step 9 — Update `AdminUpdateAsync` (sync calls)
11. Step 10 — Verify `PaymentConfiguration` (no action needed)
12. Step 11 — Write unit tests
13. Migration: `dotnet ef migrations add AddExtrasPaymentDeadlineToCampEdition`
14. Step 12 — Update technical documentation

---

## Testing Checklist

```bash
dotnet test src/Abuvi.Tests --filter "FullyQualifiedName~PaymentsService_Sync"
dotnet test src/Abuvi.Tests --filter "FullyQualifiedName~RegistrationsService"
dotnet test src/Abuvi.Tests # full suite — no regressions
```

Manual verification:
1. Create registration → P1 DueDate = `CampEdition.FirstPaymentDeadline` (or today if null)
2. Set extras → P3 created with Amount = ExtrasAmount, DueDate = `CampEdition.ExtrasPaymentDeadline` (or StartDate)
3. Update extras → P3 amount updated
4. Remove all extras → P3 deleted
5. Update members (P1 Pending) → P1 and P2 recalculated
6. Confirm P1 → update members → only P2 absorbs delta
7. Upload proof for P1 → attempt member edit → 422 BusinessRuleException
8. Invariant: `P1 + P2 + (P3 ?? 0) == Registration.TotalAmount` after every operation

---

## Error Response Format

All `BusinessRuleException` map to HTTP 422 via the existing exception handler middleware:
```json
{
  "success": false,
  "message": "No se puede modificar la inscripción porque ya hay pagos con justificante subido.",
  "errors": []
}
```

---

## Dependencies

No new NuGet packages required.

**EF Core migration command:**
```bash
cd src/Abuvi.API
dotnet ef migrations add AddExtrasPaymentDeadlineToCampEdition \
  --output-dir ../Abuvi.Infrastructure/Migrations
dotnet ef database update
```

---

## Notes

- **EF Core tracking**: `GetByRegistrationIdAsync` uses `AsNoTracking()` (read-only, safe). The new `GetByRegistrationIdTrackedAsync` returns tracked entities required by `UpdateAsync(payment, ct)` which calls `SaveChangesAsync()` without re-attaching.
- **P3 amount > 0**: Guaranteed by the `if (extrasAmount > 0)` guard before creating P3. The existing `CK_Payments_Amount` (`amount > 0`) constraint will not be violated.
- **Invariant enforcement**: `SyncExtrasInstallmentAsync` never touches P1/P2. `SyncBaseInstallmentsAsync` never touches P3. Together they maintain `P1 + P2 + P3 == TotalAmount`.
- **`paymentsService` injection into `RegistrationsService`**: Check if `IPaymentsService` is already in the constructor — if not, add it and update `Program.cs` DI registration accordingly. Be careful to avoid circular DI (if `PaymentsService` also depends on `IRegistrationsRepository` — it does — that's fine since it's not a circular service dependency, only a repository dependency).
- **Draft status**: `UpdateMembersAsync` and `SetExtrasAsync` currently block `Status != Pending`. Per the design decision (D-4), Draft registrations should also be editable if no proof exists. Update the status check in both methods:
  ```csharp
  if (registration.Status != RegistrationStatus.Pending && registration.Status != RegistrationStatus.Draft)
      throw new BusinessRuleException("Solo se pueden modificar inscripciones en estado Pendiente o Borrador");
  ```
- All error messages are in Spanish (user-facing), all code and logs in English.

---

## Next Steps After Implementation

1. Merge to `dev` and trigger integration environment deployment
2. Test against real database with existing registrations (ensure migration is non-breaking — column is nullable)
3. Implement frontend plan (`feat-three-payments-frontend.md`)
4. Implement `feat-camp-edition-edit-fullpage` to expose `ExtrasPaymentDeadline` admin configuration UI

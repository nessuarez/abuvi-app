# Backend Implementation Plan: fix-extras-editable-after-p1-p2-proof

## Overview

This is a focused backend bug fix. The guard in `RegistrationsService.SetExtrasAsync()` incorrectly blocks extras modifications the moment **any** payment proof is uploaded — including P1 and P2. The correct behavior is to allow extras changes as long as the P3 (extras installment) has not yet been submitted for review, and the extras payment deadline has not passed.

No new endpoints, no schema changes, no migrations needed. Only business-logic and test changes.

**Architecture:** Vertical Slice — change is scoped entirely within `src/Abuvi.API/Features/Registrations/`.

---

## Architecture Context

**Slice:** `src/Abuvi.API/Features/Registrations/`

**Files to modify:**
| File | Change |
|---|---|
| `RegistrationsService.cs` | Replace overly broad guard in `SetExtrasAsync()` |
| `Abuvi.Tests/.../RegistrationsServiceTests.cs` | Fix existing tests broken by the new deadline guard; add 5 new tests |

**Files to verify (no changes expected):**
| File | Why to verify |
|---|---|
| `RegistrationsService.cs` (`UpdateMembersAsync`, line ~231) | Confirm its guard correctly stays scoped to P1/P2 proofs |
| `PaymentsService.cs` (`SyncExtrasInstallmentAsync`, lines 996–998, 1042–1044) | Already correct; ensure the inner guard is not disturbed |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch name:** `feature/fix-extras-editable-after-p1-p2-proof-backend`
- **Base branch:** `dev`
- **Commands:**
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/fix-extras-editable-after-p1-p2-proof-backend
  git branch
  ```

---

### Step 1: Fix `SetExtrasAsync()` guard in `RegistrationsService.cs`

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**Location:** Lines 361–363 (the `// 3b. Guard` comment block inside `SetExtrasAsync()`).

**Action:** Replace the broad guard with two targeted guards:
1. Block only if P3 is `PendingReview` or `Completed` (proof submitted / confirmed).
2. Block if the extras payment deadline has passed.

**Old code (lines 361–363):**
```csharp
// 3b. Guard: block if any payment has a proof uploaded
if (registration.Payments?.Any(p => p.ProofFileUrl != null) == true)
    throw new BusinessRuleException("No se pueden modificar los extras porque ya hay un justificante de pago subido.");
```

**New code:**
```csharp
// 3b. Guard: block if P3 has been submitted or confirmed
var p3Payment = registration.Payments?.FirstOrDefault(p => p.InstallmentNumber == 3);
if (p3Payment?.Status is PaymentStatus.PendingReview or PaymentStatus.Completed)
    throw new BusinessRuleException("No se pueden modificar los extras porque el justificante de extras ya está en revisión o confirmado.");

// 3c. Guard: block if the extras payment deadline has passed
var extrasDeadline = registration.CampEdition.ExtrasPaymentDeadline
    ?? registration.CampEdition.StartDate;
if (DateTime.UtcNow > extrasDeadline)
    throw new BusinessRuleException("No se pueden añadir extras porque ha pasado el plazo de inscripción de extras.");
```

**Implementation notes:**
- `PaymentStatus` is already imported via `Abuvi.API.Features.Payments` at the top of the file.
- `registration.CampEdition` is always populated because the registration was loaded via `GetByIdWithDetailsAsync()` earlier in the method.
- `ExtrasPaymentDeadline` and `StartDate` are both `DateTime` / `DateTime?` — direct `>` comparison with `DateTime.UtcNow` is correct.
- Do NOT touch the inner guard in `PaymentsService.SyncExtrasInstallmentAsync()` (lines 996–998 and 1042–1044) — it is already correct and acts as a second layer of defense.

---

### Step 2: Verify `UpdateMembersAsync()` guard scope

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**Location:** Line ~231 inside `UpdateMembersAsync()`.

**Action:** Read and confirm the guard reads:
```csharp
if (registration.Payments?.Any(p => p.ProofFileUrl != null) == true)
    throw new BusinessRuleException("No se pueden modificar los miembros porque ya hay un justificante de pago subido.");
```

This guard is **intentionally broad** for member changes (blocking all payment proofs is correct here, because payment proofs are uploaded sequentially — if P3 has a proof, P1 and P2 must also have proofs). **No change needed here.** Confirm and move on.

---

### Step 3: Fix existing unit tests broken by the new deadline guard

**File:** `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`

**Problem:** The private `CreateOpenEdition()` helper (line ~491) uses:
```csharp
StartDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
EndDate   = new DateTime(2025, 7, 14, 0, 0, 0, DateTimeKind.Utc),
```

Today is 2026-06-03. With the new deadline guard (`DateTime.UtcNow > StartDate` when no `ExtrasPaymentDeadline` is set), all existing `SetExtrasAsync_*` tests will throw `BusinessRuleException` and fail.

**Fix options — choose one:**
- **(Recommended)** Update `CreateOpenEdition()` to use future dates (e.g. `2030-07-01` / `2030-07-14`). This is a pure test-helper change and does not affect other test logic, since the camp duration (13 days) remains the same.
- Alternatively, explicitly set `edition.ExtrasPaymentDeadline = DateTime.UtcNow.AddYears(2)` in each affected test. This is more verbose.

**Recommended change to `CreateOpenEdition()`:**
```csharp
private static CampEdition CreateOpenEdition(int? maxCapacity = null) => new()
{
    Id = CampEditionId,
    CampId = Guid.NewGuid(),
    Year = 2030,
    StartDate = new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc),
    EndDate   = new DateTime(2030, 7, 14, 0, 0, 0, DateTimeKind.Utc),
    // ... rest unchanged ...
};
```

**Impact check:** Search for all usages of `CreateOpenEdition()` in the test file. Verify that no test asserts a specific value of `StartDate` or `Year` — none do, so this change is safe.

---

### Step 4: Add new unit tests for `SetExtrasAsync()`

**File:** `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs`

Add 5 new `[Fact]` tests in the `// ── SetExtrasAsync ───` section (after line ~397). Follow the AAA pattern and the naming convention `MethodName_StateUnderTest_ExpectedBehavior`.

For tests that need payments in `Pending` state (proofs on P1/P2 but NOT P3), attach them to `existing.Payments` using the `Payment` entity.

**Helper to add (private static, near the bottom of the test class):**
```csharp
private static Payment CreatePayment(int installmentNumber, PaymentStatus status, string? proofFileUrl = null) => new()
{
    Id = Guid.NewGuid(),
    RegistrationId = Guid.NewGuid(), // will be overwritten in test if needed
    InstallmentNumber = installmentNumber,
    Amount = 100m,
    Status = status,
    ProofFileUrl = proofFileUrl,
    Method = PaymentMethod.Transfer,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

**Test 1 — P1 proof present, extras still allowed:**
```csharp
[Fact]
public async Task SetExtrasAsync_WhenP1HasProof_AllowsExtrasModification()
{
    // Arrange
    var registrationId = Guid.NewGuid();
    var extraId = Guid.NewGuid();
    var familyUnit = CreateFamilyUnit(UserId);
    var edition = CreateOpenEdition(); // StartDate 2030 — in the future
    var extra = CreateCampEditionExtra(extraId, CampEditionId, price: 50m);

    var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
    existing.Status = RegistrationStatus.Pending;
    existing.Payments =
    [
        CreatePayment(1, PaymentStatus.PendingReview, proofFileUrl: "https://blob/p1.pdf")
    ];

    _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
    _editionsRepo.GetExtraByIdAsync(extraId, Arg.Any<CancellationToken>()).Returns(extra);
    _extrasRepo.DeleteByRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    _extrasRepo.AddRangeAsync(Arg.Any<IEnumerable<RegistrationExtra>>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

    var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(extraId, 1)]);

    // Act
    var act = async () => await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None);

    // Assert
    await act.Should().NotThrowAsync();
}
```

**Test 2 — P2 proof present, extras still allowed:**
```csharp
[Fact]
public async Task SetExtrasAsync_WhenP2HasProof_AllowsExtrasModification()
{
    // Arrange — same as Test 1 but with P2 proof
    var registrationId = Guid.NewGuid();
    var extraId = Guid.NewGuid();
    var familyUnit = CreateFamilyUnit(UserId);
    var edition = CreateOpenEdition();
    var extra = CreateCampEditionExtra(extraId, CampEditionId, price: 50m);

    var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
    existing.Status = RegistrationStatus.Pending;
    existing.Payments =
    [
        CreatePayment(1, PaymentStatus.Completed, proofFileUrl: "https://blob/p1.pdf"),
        CreatePayment(2, PaymentStatus.PendingReview, proofFileUrl: "https://blob/p2.pdf")
    ];

    _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);
    _editionsRepo.GetExtraByIdAsync(extraId, Arg.Any<CancellationToken>()).Returns(extra);
    _extrasRepo.DeleteByRegistrationIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    _extrasRepo.AddRangeAsync(Arg.Any<IEnumerable<RegistrationExtra>>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    _repo.UpdateAsync(Arg.Any<Registration>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

    var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(extraId, 1)]);

    // Act & Assert
    await (async () => await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None))
        .Should().NotThrowAsync();
}
```

**Test 3 — P3 is PendingReview → throws:**
```csharp
[Fact]
public async Task SetExtrasAsync_WhenP3IsPendingReview_ThrowsBusinessRuleException()
{
    // Arrange
    var registrationId = Guid.NewGuid();
    var familyUnit = CreateFamilyUnit(UserId);
    var edition = CreateOpenEdition();

    var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
    existing.Status = RegistrationStatus.Pending;
    existing.Payments =
    [
        CreatePayment(1, PaymentStatus.Completed, "https://blob/p1.pdf"),
        CreatePayment(2, PaymentStatus.Completed, "https://blob/p2.pdf"),
        CreatePayment(3, PaymentStatus.PendingReview, "https://blob/p3.pdf")
    ];

    _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);

    var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(Guid.NewGuid(), 1)]);

    // Act & Assert
    await ((Func<Task>)(async () => await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None)))
        .Should().ThrowAsync<BusinessRuleException>()
        .WithMessage("*revisión*");
}
```

**Test 4 — P3 is Completed → throws:**
```csharp
[Fact]
public async Task SetExtrasAsync_WhenP3IsCompleted_ThrowsBusinessRuleException()
{
    // Arrange
    var registrationId = Guid.NewGuid();
    var familyUnit = CreateFamilyUnit(UserId);
    var edition = CreateOpenEdition();

    var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
    existing.Status = RegistrationStatus.Pending;
    existing.Payments =
    [
        CreatePayment(1, PaymentStatus.Completed, "https://blob/p1.pdf"),
        CreatePayment(2, PaymentStatus.Completed, "https://blob/p2.pdf"),
        CreatePayment(3, PaymentStatus.Completed,  "https://blob/p3.pdf")
    ];

    _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);

    var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(Guid.NewGuid(), 1)]);

    // Act & Assert
    await ((Func<Task>)(async () => await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None)))
        .Should().ThrowAsync<BusinessRuleException>()
        .WithMessage("*revisión*");
}
```

**Test 5 — Past extras deadline → throws:**
```csharp
[Fact]
public async Task SetExtrasAsync_WhenPastExtrasPaymentDeadline_ThrowsBusinessRuleException()
{
    // Arrange
    var registrationId = Guid.NewGuid();
    var familyUnit = CreateFamilyUnit(UserId);
    var edition = CreateOpenEdition();
    edition.ExtrasPaymentDeadline = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); // past date

    var existing = CreateRegistrationWithFamilyUnit(registrationId, familyUnit, edition);
    existing.Status = RegistrationStatus.Pending;

    _repo.GetByIdWithDetailsAsync(registrationId, Arg.Any<CancellationToken>()).Returns(existing);

    var request = new UpdateRegistrationExtrasRequest([new ExtraSelectionRequest(Guid.NewGuid(), 1)]);

    // Act & Assert
    await ((Func<Task>)(async () => await _sut.SetExtrasAsync(registrationId, UserId, request, CancellationToken.None)))
        .Should().ThrowAsync<BusinessRuleException>()
        .WithMessage("*plazo*");
}
```

---

### Step 5: Run the tests

```bash
dotnet test src/Abuvi.Tests --filter "FullyQualifiedName~RegistrationsServiceTests" --verbosity normal
```

All tests in `RegistrationsServiceTests` must pass (existing + new). If anything red, diagnose before proceeding.

---

### Step 6: Update Technical Documentation

**File:** `ai-specs/specs/api-spec.yml`

Locate the `POST /registrations/{id}/extras` endpoint section. Update the description of the `422` error response to accurately reflect the two blocking conditions:

Current description likely says something about "payment proof uploaded". Update to:
> **422 Unprocessable Entity** is returned when:
> - The P3 (extras installment) has been submitted for review or confirmed (`PendingReview`/`Completed`)
> - The extras payment deadline has passed (`CampEdition.ExtrasPaymentDeadline ?? CampEdition.StartDate`)
> - The registration is not in `Pending` or `Draft` status

**No changes needed to `ai-specs/specs/data-model.md`** — no schema changes.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Fix `SetExtrasAsync()` guard (the core change)
3. Step 2 — Verify `UpdateMembersAsync()` guard (read-only check)
4. Step 3 — Update `CreateOpenEdition()` dates in tests
5. Step 4 — Add 5 new unit tests
6. Step 5 — Run tests (all green)
7. Step 6 — Update API spec documentation

---

## Testing Checklist

- [ ] `SetExtrasAsync_WhenExtrasValid_UpdatesExtrasAmountAndTotal` — still passes (no regression)
- [ ] `SetExtrasAsync_WhenExtraNotInEdition_ThrowsBusinessRuleException` — still passes
- [ ] `SetExtrasAsync_WhenQuantityExceedsMax_ThrowsBusinessRuleException` — still passes
- [ ] `SetExtrasAsync_WhenRegistrationNotPending_ThrowsBusinessRuleException` — still passes
- [ ] `SetExtrasAsync_WhenP1HasProof_AllowsExtrasModification` — **new**, must pass
- [ ] `SetExtrasAsync_WhenP2HasProof_AllowsExtrasModification` — **new**, must pass
- [ ] `SetExtrasAsync_WhenP3IsPendingReview_ThrowsBusinessRuleException` — **new**, must pass
- [ ] `SetExtrasAsync_WhenP3IsCompleted_ThrowsBusinessRuleException` — **new**, must pass
- [ ] `SetExtrasAsync_WhenPastExtrasPaymentDeadline_ThrowsBusinessRuleException` — **new**, must pass
- [ ] All other `RegistrationsServiceTests` — no regressions

---

## Error Response Format

No endpoint changes. The existing error path applies:

| Condition | HTTP Status | Code |
|---|---|---|
| P3 PendingReview or Completed | 422 | `BUSINESS_RULE_VIOLATION` |
| Past extras deadline | 422 | `BUSINESS_RULE_VIOLATION` |
| Registration not Pending/Draft | 422 | `BUSINESS_RULE_VIOLATION` |

---

## Dependencies

- No new NuGet packages
- No EF Core migrations required

---

## Notes

- **Error messages in Spanish** — follow the project convention (`No se pueden... porque...`).
- The inner guard in `PaymentsService.SyncExtrasInstallmentAsync()` (lines 996–998, 1042–1044) already handles P3 status correctly and acts as a second layer of defense. Do not remove it.
- **No changes to `UpdateMembersAsync()`** — its broad guard is intentionally correct because payment proofs are uploaded sequentially (P1 → P2 → P3), so if P3 has a proof, P1 and P2 necessarily already have proofs, making the broad check functionally equivalent.
- The `CreateOpenEdition()` date update (2025 → 2030) does not affect test correctness because no test asserts specific date values; it only affects whether `DateTime.UtcNow > StartDate` fires.

---

## Next Steps After Implementation

- Frontend fix tracked separately — see `fix_extras_editable_after_payment_proof_enriched.md`.
- After backend PR is merged to `dev`, the frontend fix can proceed independently.

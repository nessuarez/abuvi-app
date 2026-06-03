# Fix: Allow Extras to Be Modified After P1/P2 Payment Proofs Are Uploaded

## Problem

Extras cannot be added or modified once the family uploads any payment proof (P1 or P2 justificante). This is too restrictive: last-minute extra charges must be possible up until the extras payment deadline (close to camp start), not blocked by earlier installment uploads.

**Root cause — two overly broad guards:**

1. **Backend** `RegistrationsService.SetExtrasAsync()` ([RegistrationsService.cs:362](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L362)):

   ```csharp
   // CURRENT — too broad: blocks if ANY payment has a proof
   if (registration.Payments?.Any(p => p.ProofFileUrl != null) == true)
       throw new BusinessRuleException("No se pueden modificar los extras...");
   ```

   This fires as soon as P1 or P2 has a proof, long before the extras deadline.

2. **Frontend** `RegistrationDetailPage.vue` ([RegistrationDetailPage.vue:157](frontend/src/views/registrations/RegistrationDetailPage.vue#L157)):

   ```typescript
   // CURRENT — too broad: hides "Editar extras" button if any installment has proofFileUrl
   return !installments.value.some((p) => p.proofFileUrl != null)
   ```

   The same problem: any P1/P2 proof hides the button.

---

## Correct Business Rules

| State | Can edit members (P1/P2)? | Can edit extras (P3)? |
|---|---|---|
| No proofs uploaded | Yes | Yes |
| P1 proof uploaded | No | **Yes** |
| P2 proof uploaded | No | **Yes** |
| P3 proof uploaded (status `PendingReview`) | No | No |
| P3 confirmed (status `Completed`) | No | No |
| Past extras payment deadline | No | No |

Extras are editable until the **extras payment deadline** (`CampEdition.ExtrasPaymentDeadline`) or `CampEdition.StartDate` if not set — regardless of P1/P2 proof status.

---

## Changes Required

### 1. Backend — `RegistrationsService.SetExtrasAsync()` ([RegistrationsService.cs:361-363](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L361-L363))

**Remove** the overly broad guard and replace with:

- A P3-specific proof/status check (P3 PendingReview or Completed → block).
- A deadline check: if `DateTime.UtcNow > ExtrasPaymentDeadline` → block.

```csharp
// NEW guard — only block based on P3 state and deadline
var p3 = registration.Payments?.FirstOrDefault(p => p.InstallmentNumber == 3);
if (p3?.Status is PaymentStatus.PendingReview or PaymentStatus.Completed)
    throw new BusinessRuleException("No se pueden modificar los extras porque el justificante de extras ya está en revisión o confirmado.");

var extrasDeadline = registration.CampEdition.ExtrasPaymentDeadline
    ?? registration.CampEdition.StartDate.AddDays(settings.ExtrasInstallmentDaysFromCampStart);
if (DateTime.UtcNow > extrasDeadline)
    throw new BusinessRuleException("No se pueden modificar los extras porque ha pasado el plazo de pago de extras.");
```

> Note: `SyncExtrasInstallmentAsync()` in `PaymentsService.cs` already has a correct inner guard for P3 status (lines 996-998 and 1042-1044). The fix here removes the redundant and wrong outer guard.

> The `settings` object (`CampEditionSettings`) is already available in the service. Check how `SyncExtrasInstallmentAsync` accesses it for the pattern.

**File:** [src/Abuvi.API/Features/Registrations/RegistrationsService.cs](src/Abuvi.API/Features/Registrations/RegistrationsService.cs)
**Method:** `SetExtrasAsync()` (~line 362)

---

### 2. Backend — `UpdateMembersAsync()` guard review ([RegistrationsService.cs:231-232](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L231-L232))

The member-update guard should remain blocking if P1 **or** P2 has a proof (members affect P1/P2 amounts). Verify the existing guard is scoped to `InstallmentNumber <= 2` only, not P3:

```csharp
// Should block ONLY on P1/P2 proofs
if (registration.Payments?.Any(p => p.InstallmentNumber <= 2 && p.ProofFileUrl != null) == true)
    throw new BusinessRuleException("...");
```

If this guard currently checks ALL payments, fix it to be P1/P2-specific.

**File:** [src/Abuvi.API/Features/Registrations/RegistrationsService.cs](src/Abuvi.API/Features/Registrations/RegistrationsService.cs)
**Method:** `UpdateMembersAsync()` (~line 231)

---

### 3. Frontend — `RegistrationDetailPage.vue` ([RegistrationDetailPage.vue:152-164](frontend/src/views/registrations/RegistrationDetailPage.vue#L152-L164))

Add a separate `canUserEditExtras` computed alongside `canUserEdit`:

```typescript
const canUserEditExtras = computed(() => {
  if (!registration.value) return false
  const status = registration.value.status
  if (status !== 'Pending' && status !== 'Draft') return false
  if (!isRepresentative.value) return false
  const p3 = installments.value.find((p) => p.installmentNumber === 3)
  // Allow if no P3 yet, or P3 is still Pending (no proof submitted)
  return !p3 || (p3.status !== 'PendingReview' && p3.status !== 'Completed')
})
```

Then split the button group at line 829 so each button uses its own guard:

```html
<!-- Before: v-if="canEdit || canAdminEdit" on the whole div -->
<!-- After: each button has its own guard -->

<Button
  v-if="canEdit || canAdminEdit"
  label="Editar participantes"
  ...
/>
<Button
  v-if="canUserEditExtras || canAdminEdit"
  label="Editar extras"
  ...
/>
```

**File:** [frontend/src/views/registrations/RegistrationDetailPage.vue](frontend/src/views/registrations/RegistrationDetailPage.vue)

---

### 4. Tests — Backend

Add/update unit tests in [src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs](src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs):

| Test | Scenario | Expected |
|---|---|---|
| `SetExtras_WhenP1HasProof_ShouldSucceed` | P1 has `ProofFileUrl`, P3 is Pending | Extras saved successfully |
| `SetExtras_WhenP2HasProof_ShouldSucceed` | P2 has `ProofFileUrl`, P3 is Pending | Extras saved successfully |
| `SetExtras_WhenP3IsPendingReview_ShouldThrow` | P3 status is `PendingReview` | `BusinessRuleException` |
| `SetExtras_WhenP3IsCompleted_ShouldThrow` | P3 status is `Completed` | `BusinessRuleException` |
| `SetExtras_WhenPastExtrasDeadline_ShouldThrow` | `DateTime.UtcNow > ExtrasPaymentDeadline` | `BusinessRuleException` |
| `SetExtras_WhenNoDeadlineSet_UsesStartDateFallback` | No `ExtrasPaymentDeadline`, `StartDate` in future | Extras saved successfully |

---

## Files to Modify

| File | Change |
|---|---|
| [src/Abuvi.API/Features/Registrations/RegistrationsService.cs](src/Abuvi.API/Features/Registrations/RegistrationsService.cs) | Fix `SetExtrasAsync()` guard (line ~362); review `UpdateMembersAsync()` guard (line ~231) |
| [frontend/src/views/registrations/RegistrationDetailPage.vue](frontend/src/views/registrations/RegistrationDetailPage.vue) | Add `canUserEditExtras` computed; split button guards |
| [src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs](src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsServiceTests.cs) | Add 5-6 new unit tests covering the scenarios above |

---

## Acceptance Criteria

- [ ] A family that has uploaded P1 or P2 proof can still add/modify extras from the registration detail page.
- [ ] A family that has uploaded P3 proof (status `PendingReview`) cannot modify extras — backend returns 422, button is hidden.
- [ ] A family whose P3 is `Completed` cannot modify extras.
- [ ] Extras cannot be modified after `CampEdition.ExtrasPaymentDeadline` (or camp start if not set).
- [ ] The "Editar participantes" button remains hidden once P1 or P2 proof is uploaded.
- [ ] Admins can always edit extras (regardless of payment state) via `canAdminEdit`.
- [ ] All new backend unit tests pass.

---

## Non-Functional Requirements

- **Security**: The backend guard is the authoritative check. The frontend guard is a UX improvement only.
- **Backwards compatibility**: No data migration needed — this is a logic-only change.
- **Error messages**: Backend exception messages must be in Spanish (existing convention in this service).

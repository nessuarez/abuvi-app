# Backend Implementation Plan: fix-admin-remove-payment-proof Admin Remove Payment Proof

## Overview

Add an admin-only endpoint that allows `Admin` and `Board` users to delete a payment proof (justificante) uploaded by any family, regardless of payment status or ownership. The feature follows the existing Vertical Slice Architecture inside `src/Abuvi.API/Features/Payments/`. No schema changes are needed — the proof fields already exist on the `Payment` entity.

---

## Architecture Context

**Feature slice**: `src/Abuvi.API/Features/Payments/`

**Files to modify** (no new files):

| File | Change |
|------|--------|
| `IPaymentsService.cs` | Add `AdminRemoveProofAsync` method signature |
| `PaymentsService.cs` | Implement `AdminRemoveProofAsync` |
| `PaymentsEndpoints.cs` | Register `DELETE /api/admin/payments/{paymentId:guid}/proof` and its handler |
| `Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs` | Add unit tests for `AdminRemoveProofAsync` |

**Cross-cutting concerns**: None. No middleware, shared types, or migrations required.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the backend feature branch.
- **Implementation Steps**:
  1. Ensure you are on `dev` and it is up to date: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/fix-admin-remove-payment-proof-backend`
  3. Verify: `git branch`

---

### Step 1: Add `AdminRemoveProofAsync` to `IPaymentsService`

- **File**: `src/Abuvi.API/Features/Payments/IPaymentsService.cs`
- **Action**: Add the method signature under the manual payment management block (after `DeleteManualPaymentAsync`).
- **Signature**:
  ```csharp
  Task AdminRemoveProofAsync(Guid paymentId, Guid adminUserId, CancellationToken ct);
  ```
- **Implementation Steps**:
  1. Open `IPaymentsService.cs`.
  2. Insert the new method after `Task DeleteManualPaymentAsync(...)`:
     ```csharp
     Task AdminRemoveProofAsync(Guid paymentId, Guid adminUserId, CancellationToken ct);
     ```

---

### Step 2: Implement `AdminRemoveProofAsync` in `PaymentsService`

- **File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- **Action**: Add the method after the existing `RemoveProofAsync` method (around line 160).
- **Implementation Steps**:
  1. Retrieve the payment using `paymentsRepo.GetByIdAsync(paymentId, ct)`. Use the **non-registration** overload — ownership is irrelevant here. If `null`, throw `new NotFoundException("Pago", paymentId)`.
  2. If `payment.ProofFileUrl is null`, throw `new BusinessRuleException("El pago no tiene ningún justificante adjunto")`.
  3. Capture `var previousStatus = payment.Status` and `var proofFileName = payment.ProofFileName` for logging.
  4. Delete from blob storage:
     ```csharp
     var key = ExtractBlobKey(payment.ProofFileUrl);
     await blobStorageService.DeleteManyAsync([key], ct);
     ```
     This follows the exact same pattern used in `RemoveProofAsync` (lines 141–143).
  5. Clear proof fields:
     ```csharp
     payment.ProofFileUrl = null;
     payment.ProofFileName = null;
     payment.ProofUploadedAt = null;
     ```
  6. Apply status transition:
     ```csharp
     if (payment.Status == PaymentStatus.PendingReview)
         payment.Status = PaymentStatus.Pending;
     // All other statuses (Pending, Completed, Rejected) stay unchanged
     ```
  7. Update `payment.UpdatedAt = DateTime.UtcNow` (if `UpdatedAt` is managed manually; if EF Core handles it via interceptor, skip this line — check `PaymentConfiguration.cs`).
  8. Persist: `await paymentsRepo.UpdateAsync(payment, ct)`.
  9. Log with structured logging (English, as required by standards):
     ```csharp
     logger.LogInformation(
         "Admin {AdminUserId} removed proof {FileName} from payment {PaymentId} (registration {RegistrationId}). Previous status: {PreviousStatus}",
         adminUserId, proofFileName, payment.Id, payment.RegistrationId, previousStatus);
     ```

- **Implementation Notes**:
  - `GetByIdAsync` (without registration) should already exist in `IPaymentsRepository`. If only `GetByIdWithRegistrationAsync` is available, use that — but do NOT validate ownership.
  - `ExtractBlobKey` is a private method already present in `PaymentsService`. Re-use it.
  - Return type is `Task` (void), not `Task<PaymentResponse>` — the admin UI will refresh the list separately.

---

### Step 3: Register the Endpoint in `PaymentsEndpoints`

- **File**: `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs`
- **Action**: Register the new route inside the `admin` group and add a private handler method.
- **Implementation Steps**:

  **3a — Register route** (after `admin.MapDelete("/{paymentId:guid}/manual", ...)`, around line 104):
  ```csharp
  admin.MapDelete("/{paymentId:guid}/proof", AdminRemoveProof)
      .WithName("AdminRemovePaymentProof")
      .WithSummary("Remove a payment proof (admin)")
      .Produces(204)
      .Produces(403).Produces(404).Produces(422);
  ```

  **3b — Add private handler** (after the `DeleteManualPayment` handler, around line 445):
  ```csharp
  private static async Task<IResult> AdminRemoveProof(
      Guid paymentId,
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
          await service.AdminRemoveProofAsync(paymentId, userId, ct);
          return TypedResults.NoContent();
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

- **Implementation Notes**:
  - Returns `204 No Content` on success (consistent with `DeleteManualPayment` which uses `Ok(null!)` — use `NoContent()` here as it is semantically cleaner for a DELETE with no body).
  - The route `DELETE /{paymentId:guid}/proof` does not conflict with `DELETE /{paymentId:guid}/manual` already registered.
  - No `ValidationFilter` needed — there is no request body.

---

### Step 4: Write Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs`
- **Action**: Add a new test region `// ── AdminRemoveProofAsync` following the existing section style.
- **Setup**: Reuse the existing `_paymentsRepo`, `_blobStorageService`, `_logger`, and `_sut` already configured in `PaymentsServiceTests`.

**Tests to add**:

```csharp
// ── AdminRemoveProofAsync ─────────────────────────────────────────────────

[Fact]
public async Task AdminRemoveProofAsync_WhenPaymentHasProofAndStatusIsPendingReview_ClearsProofAndResetsStatusToPending()
{
    // Arrange
    var payment = CreatePayment(PaymentStatus.PendingReview);
    payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/proof.jpg";
    payment.ProofFileName = "proof.jpg";
    payment.ProofUploadedAt = DateTime.UtcNow;
    _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

    // Act
    await _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

    // Assert
    payment.ProofFileUrl.Should().BeNull();
    payment.ProofFileName.Should().BeNull();
    payment.ProofUploadedAt.Should().BeNull();
    payment.Status.Should().Be(PaymentStatus.Pending);
    await _paymentsRepo.Received(1).UpdateAsync(payment, Arg.Any<CancellationToken>());
}

[Fact]
public async Task AdminRemoveProofAsync_WhenPaymentHasProofAndStatusIsCompleted_ClearsProofAndKeepsStatusCompleted()
{
    // Arrange
    var payment = CreatePayment(PaymentStatus.Completed);
    payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/proof.jpg";
    payment.ProofFileName = "proof.jpg";
    _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

    // Act
    await _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

    // Assert
    payment.ProofFileUrl.Should().BeNull();
    payment.Status.Should().Be(PaymentStatus.Completed);
}

[Fact]
public async Task AdminRemoveProofAsync_WhenPaymentHasProofAndStatusIsRejected_ClearsProofAndKeepsStatusRejected()
{
    // Arrange
    var payment = CreatePayment(PaymentStatus.Rejected);
    payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/proof.jpg";
    payment.ProofFileName = "proof.jpg";
    _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

    // Act
    await _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

    // Assert
    payment.ProofFileUrl.Should().BeNull();
    payment.Status.Should().Be(PaymentStatus.Rejected);
}

[Fact]
public async Task AdminRemoveProofAsync_WhenPaymentHasNoProof_ThrowsBusinessRuleException()
{
    // Arrange
    var payment = CreatePayment(PaymentStatus.PendingReview);
    payment.ProofFileUrl = null;
    _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

    // Act
    var act = () => _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<BusinessRuleException>()
        .WithMessage("El pago no tiene ningún justificante adjunto");
}

[Fact]
public async Task AdminRemoveProofAsync_WhenPaymentDoesNotExist_ThrowsNotFoundException()
{
    // Arrange
    _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).ReturnsNull();

    // Act
    var act = () => _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<NotFoundException>();
}

[Fact]
public async Task AdminRemoveProofAsync_WhenProofRemoved_DeletesBlobFromStorage()
{
    // Arrange
    var payment = CreatePayment(PaymentStatus.PendingReview);
    payment.ProofFileUrl = "https://cdn.test.com/payment-proofs/abc123.jpg";
    payment.ProofFileName = "abc123.jpg";
    _paymentsRepo.GetByIdAsync(PaymentId, Arg.Any<CancellationToken>()).Returns(payment);

    // Act
    await _sut.AdminRemoveProofAsync(PaymentId, AdminUserId, CancellationToken.None);

    // Assert
    await _blobStorageService.Received(1).DeleteManyAsync(
        Arg.Is<string[]>(keys => keys.Length == 1),
        Arg.Any<CancellationToken>());
}
```

- **Implementation Notes**:
  - If `IPaymentsRepository` only exposes `GetByIdWithRegistrationAsync` and not `GetByIdAsync`, use `GetByIdWithRegistrationAsync` in both the service and the test mocks.
  - The `CreatePayment` helper in the test file sets `RegisteredByUserId = UserId`; this is irrelevant for admin removal (no ownership check), but the helper is still valid.
  - Check if `PaymentStatus.Rejected` is the enum value name used in the codebase (could also be `Rejected` — verify in `PaymentsModels.cs`).

---

### Step 5: Update Technical Documentation

- **File**: `ai-specs/specs/api-spec.yml`
- **Action**: Add the new endpoint under the `Payments Admin` tag:
  ```yaml
  /api/admin/payments/{paymentId}/proof:
    delete:
      summary: Remove a payment proof (admin)
      tags: [Payments Admin]
      parameters:
        - name: paymentId
          in: path
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '204': { description: Proof removed successfully }
        '403': { description: Forbidden }
        '404': { description: Payment not found or has no proof }
        '422': { description: Business rule violation }
  ```
- **Notes**: No data model changes → `data-model.md` does not need updating.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `AdminRemoveProofAsync` to `IPaymentsService`
3. Step 2 — Implement `AdminRemoveProofAsync` in `PaymentsService`
4. Step 3 — Register endpoint and handler in `PaymentsEndpoints`
5. Step 4 — Write unit tests
6. Step 5 — Update `api-spec.yml`

---

## Testing Checklist

- [ ] `dotnet build` — zero warnings (TreatWarningsAsErrors is enabled)
- [ ] `dotnet test` — all existing tests still pass
- [ ] New tests cover: happy path (PendingReview→Pending), status preservation (Completed, Rejected), no-proof guard, not-found guard, blob deletion called
- [ ] Manual test via Swagger or curl: `DELETE /api/admin/payments/{id}/proof` with Admin JWT → 204

---

## Error Response Format

| Scenario | HTTP | `ApiResponse` code |
|----------|------|-------------------|
| Proof deleted | `204 No Content` | — |
| Role not Admin/Board | `403 Forbidden` | — |
| Payment not found | `404 Not Found` | `NOT_FOUND` |
| Payment has no proof | `422 Unprocessable Entity` | `BUSINESS_RULE` |

---

## Dependencies

- No new NuGet packages.
- No EF Core migration needed.

---

## Notes

- **No ownership check**: `AdminRemoveProofAsync` must NOT validate that the admin owns the registration. This is intentional — admins manage other families' payments.
- **Blob cleanup first**: Always call `blobStorageService.DeleteManyAsync` before clearing DB fields to avoid orphaned blobs.
- **Status rule**: Only `PendingReview → Pending` is reset. `Completed` and `Rejected` statuses are kept so the admin does not accidentally un-confirm a payment by removing a superseded proof.
- **Language**: All user-facing `BusinessRuleException` messages in Spanish; all log messages in English.
- **Return type `Task` not `Task<PaymentResponse>`**: The admin UI refreshes the full list after deletion; there is no need to return the updated payment from this endpoint.

---

## Next Steps After Implementation

- Frontend ticket (`fix-admin-remove-payment-proof-frontend`) adds the delete proof button to `PaymentsAllList.vue` and the `adminRemoveProof()` composable.

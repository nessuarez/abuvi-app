# Admin: Remove Payment Proof

## Summary

Board members (`Board`) and Admins (`Admin`) need to be able to delete an erroneously uploaded payment proof from the admin payments interface. Currently, no admin-facing endpoint or UI action exists for this operation. All deletions must be logged.

---

## Context

The user-facing proof removal endpoint (`DELETE /api/payments/{paymentId}/proof`) exists in `PaymentsEndpoints.cs` and is handled by `PaymentsService.RemoveProofAsync()`. It enforces ownership validation (the authenticated user must own the registration) and only allows removal when `Status` is `Pending` or `PendingReview`. Admins cannot use this endpoint for other users' payments.

The admin UI (`PaymentsAllList.vue`) renders proofs as image/link previews (lines 410–423) but offers no delete action.

---

## Acceptance Criteria

### Backend

1. **New endpoint**: `DELETE /api/admin/payments/{paymentId:guid}/proof`
   - Route group: `/api/admin/payments` (`PaymentsEndpoints.cs`)
   - Authorization: role must be `"Admin"` or `"Board"` (pattern matching existing admin endpoints, e.g. line 249)
   - Returns `204 No Content` on success
   - Returns `403 Forbidden` if role check fails
   - Returns `404 Not Found` if payment does not exist or has no proof

2. **New service method**: `AdminRemoveProofAsync(Guid paymentId, Guid adminUserId, CancellationToken ct)` in `PaymentsService.cs`
   - Loads payment; throws `NotFoundException` if not found
   - Throws `BusinessRuleException("El pago no tiene ningún justificante adjunto")` if `ProofFileUrl` is null
   - Extracts the blob key from `ProofFileUrl` and calls `blobStorageService.DeleteManyAsync()` to remove the file (same pattern as `RemoveProofAsync`)
   - Clears `ProofFileUrl`, `ProofFileName`, `ProofUploadedAt` on the payment entity
   - **Status transition**:
     - If `Status` is `PendingReview` → reset to `Pending`
     - If `Status` is `Pending`, `Confirmed`, or `Rejected` → leave status unchanged
   - Sets `UpdatedAt = DateTime.UtcNow`
   - Saves changes via EF Core
   - Logs via `ILogger<PaymentsService>` using structured logging (English):
     ```
     logger.LogInformation(
         "Admin {AdminUserId} removed proof {FileName} from payment {PaymentId} (registration {RegistrationId}). Previous status: {PreviousStatus}",
         adminUserId, proofFileName, payment.Id, payment.RegistrationId, previousStatus);
     ```

3. **Interface update**: Add `AdminRemoveProofAsync` to `IPaymentsService`

4. **No new DB migration needed**: No new fields; existing proof fields (`proof_file_url`, `proof_file_name`, `proof_uploaded_at`) and `updated_at` are already present.

### Frontend

5. **Delete proof button** in `PaymentsAllList.vue`:
   - Visible when `proofFileUrl` is not null/empty, regardless of payment status
   - Located adjacent to the existing proof preview (lines 410–423)
   - Icon button (e.g., trash icon) with a confirmation dialog before deletion
   - Confirmation dialog text: `"¿Seguro que quieres eliminar el justificante de este pago? Esta acción no se puede deshacer."`
   - On confirm: calls a new composable function `adminRemoveProof(paymentId)`
   - On success: refreshes the payments list and shows a success toast
   - On error: shows error toast with the API message

6. **New composable function** `adminRemoveProof(paymentId: string)` in `usePayments.ts`:
   - `DELETE /api/admin/payments/{paymentId}/proof`
   - Returns `void` on success; throws on error

7. **Type update**: No new types needed; `AdminPaymentResponse` already includes proof fields.

---

## Files to Modify

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs` | Add `DELETE /api/admin/payments/{paymentId}/proof` endpoint and handler |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Add `AdminRemoveProofAsync` method |
| `src/Abuvi.API/Features/Payments/IPaymentsService.cs` | Add `AdminRemoveProofAsync` to the interface |
| `frontend/src/composables/usePayments.ts` | Add `adminRemoveProof()` function |
| `frontend/src/components/admin/PaymentsAllList.vue` | Add delete proof button and confirmation dialog |

---

## Tests

### Unit tests (`Abuvi.Tests/Unit/Features/Payments/PaymentsServiceTests.cs`)

- `AdminRemoveProofAsync_WhenPaymentHasProofAndStatusIsPendingReview_ClearsProofAndResetsStatusToPending`
- `AdminRemoveProofAsync_WhenPaymentHasProofAndStatusIsConfirmed_ClearsProofAndKeepsStatusConfirmed`
- `AdminRemoveProofAsync_WhenPaymentHasNoProof_ThrowsBusinessRuleException`
- `AdminRemoveProofAsync_WhenPaymentDoesNotExist_ThrowsNotFoundException`
- `AdminRemoveProofAsync_WhenProofRemoved_LogsAdminUserIdAndFileName`

### Integration tests (`Abuvi.Tests/Integration/Features/Payments/`)

- `DELETE /api/admin/payments/{id}/proof` → `204` when proof exists
- `DELETE /api/admin/payments/{id}/proof` → `404` when payment has no proof
- `DELETE /api/admin/payments/{id}/proof` → `404` when payment does not exist
- `DELETE /api/admin/payments/{id}/proof` → `403` when role is not Admin/Board

---

## Non-Functional Requirements

- **Security**: Endpoint requires authentication (`RequireAuthorization()`) and role guard (`"Admin"` or `"Board"`). Never allow users to delete other users' proofs through this endpoint.
- **Audit**: Every deletion must be logged with `adminUserId`, `paymentId`, `registrationId`, `proofFileName`, and `previousStatus`.
- **Blob cleanup**: Always delete the file from blob storage before clearing DB fields to avoid orphaned files.
- **Idempotency**: If `ProofFileUrl` is null, return `404` (do not silently succeed).

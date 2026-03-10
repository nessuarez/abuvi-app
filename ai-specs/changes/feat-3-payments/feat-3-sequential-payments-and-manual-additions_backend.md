# Backend Implementation Plan: feat-3 Sequential Payments & Manual Payment Additions

## Overview

Two independent features for the Payments vertical slice:

1. **Sequential Payment Enforcement** — Computed `IsActionable` flag on payment responses + server-side validation blocking proof upload for out-of-order payments.
2. **Manual/Custom Payments** — New admin endpoints to create, update, and delete ad-hoc payments on registrations, with a new `IsManual` column and `ManualPaymentConceptLine` type.

Both features follow the existing Vertical Slice Architecture within `src/Abuvi.API/Features/Payments/`.

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Payments/`
- **Files to modify**:
  - `PaymentsModels.cs` — New DTOs, updated response records
  - `PaymentsService.cs` — New methods + `IsActionable` logic + proof upload validation
  - `PaymentsEndpoints.cs` — New admin endpoints for manual payments
  - `PaymentConfiguration.cs` — New `is_manual` column mapping
- **Files to modify (cross-feature)**:
  - `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — Add `IsManual` property to `Payment` entity
- **New files**: EF Core migration (auto-generated)

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a backend-specific branch
- **Branch Naming**: `feature/feat-3-sequential-manual-payments-backend`
- **Implementation Steps**:
  1. Ensure on latest `dev`: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/feat-3-sequential-manual-payments-backend`
  3. Verify: `git branch`
- **Notes**: If already on an appropriate branch for this feature (e.g., `feature/feat-3-payment-concept-lines-backend`), continue there instead to avoid fragmentation. Use judgement based on current branch state.

---

### Step 1: Update Payment Entity — Add `IsManual` Property

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `IsManual` boolean property to the `Payment` class
- **Implementation Steps**:
  1. Locate the `Payment` class (around line 85)
  2. Add property after `ConceptLinesSerialized`:
     ```csharp
     public bool IsManual { get; set; } = false;
     ```
- **Notes**: Default `false` ensures backward compatibility — existing auto-generated payments are unaffected.

---

### Step 2: Update EF Core Configuration — Map `is_manual` Column

- **File**: `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs`
- **Action**: Add column mapping for `IsManual`
- **Implementation Steps**:
  1. Add after the `ConceptLinesSerialized` mapping:
     ```csharp
     builder.Property(p => p.IsManual)
         .HasColumnName("is_manual")
         .IsRequired()
         .HasDefaultValue(false);
     ```
- **Dependencies**: Step 1 (entity property exists)

---

### Step 3: Update Models — New DTOs and Updated Responses

- **File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
- **Action**: Add new records and update existing response DTOs
- **Implementation Steps**:

  1. **Add `ManualPaymentConceptLine` record**:
     ```csharp
     public record ManualPaymentConceptLine(
         string Description,
         decimal Amount
     );
     ```

  2. **Update `PaymentConceptLinesJson`** — add `ManualLine` field:
     ```csharp
     public record PaymentConceptLinesJson(
         List<PaymentConceptLine>? MemberLines,
         List<PaymentExtraConceptLine>? ExtraLines,
         ManualPaymentConceptLine? ManualLine = null
     );
     ```
     > Use default `null` to maintain backward compatibility with existing serialized JSON.

  3. **Update `PaymentResponse`** — add `IsActionable` and `IsManual`:
     ```csharp
     record PaymentResponse(
         Guid Id,
         Guid RegistrationId,
         int InstallmentNumber,
         decimal Amount,
         DateTime? DueDate,
         PaymentMethod Method,
         PaymentStatus Status,
         string? TransferConcept,
         string? ProofFileUrl,
         string? ProofFileName,
         DateTime? ProofUploadedAt,
         string? AdminNotes,
         DateTime CreatedAt,
         bool IsActionable,
         bool IsManual,
         List<PaymentConceptLine>? ConceptLines = null,
         List<PaymentExtraConceptLine>? ExtraConceptLines = null,
         ManualPaymentConceptLine? ManualConceptLine = null
     );
     ```

  4. **Update `AdminPaymentResponse`** — add `IsActionable` and `IsManual`:
     ```csharp
     record AdminPaymentResponse(
         Guid Id,
         Guid RegistrationId,
         string FamilyUnitName,
         string CampEditionName,
         int InstallmentNumber,
         decimal Amount,
         DateTime? DueDate,
         PaymentStatus Status,
         string? TransferConcept,
         string? ProofFileUrl,
         string? ProofFileName,
         DateTime? ProofUploadedAt,
         string? AdminNotes,
         string? ConfirmedByUserName,
         DateTime? ConfirmedAt,
         DateTime CreatedAt,
         bool IsActionable,
         bool IsManual,
         List<PaymentConceptLine>? ConceptLines = null,
         List<PaymentExtraConceptLine>? ExtraConceptLines = null,
         ManualPaymentConceptLine? ManualConceptLine = null
     );
     ```

  5. **Add `CreateManualPaymentRequest` record**:
     ```csharp
     public record CreateManualPaymentRequest(
         decimal Amount,
         string Description,
         DateTime? DueDate = null,
         string? AdminNotes = null
     );
     ```

  6. **Add `UpdateManualPaymentRequest` record**:
     ```csharp
     public record UpdateManualPaymentRequest(
         decimal? Amount = null,
         string? Description = null,
         DateTime? DueDate = null,
         string? AdminNotes = null
     );
     ```

---

### Step 4: Add Validators for Manual Payment Requests

- **File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs` (or a separate `PaymentsValidators.cs` if the project uses separate validator files — follow existing convention)
- **Action**: Create FluentValidation validators
- **Implementation Steps**:

  1. **`CreateManualPaymentValidator`**:
     ```csharp
     public class CreateManualPaymentValidator : AbstractValidator<CreateManualPaymentRequest>
     {
         public CreateManualPaymentValidator()
         {
             RuleFor(x => x.Amount).GreaterThan(0)
                 .WithMessage("El importe debe ser mayor que 0.");
             RuleFor(x => x.Description).NotEmpty()
                 .WithMessage("La descripción es obligatoria.")
                 .MaximumLength(500);
             RuleFor(x => x.AdminNotes).MaximumLength(2000)
                 .When(x => x.AdminNotes != null);
         }
     }
     ```

  2. **`UpdateManualPaymentValidator`**:
     ```csharp
     public class UpdateManualPaymentValidator : AbstractValidator<UpdateManualPaymentRequest>
     {
         public UpdateManualPaymentValidator()
         {
             RuleFor(x => x.Amount).GreaterThan(0)
                 .When(x => x.Amount.HasValue)
                 .WithMessage("El importe debe ser mayor que 0.");
             RuleFor(x => x.Description).NotEmpty()
                 .When(x => x.Description != null)
                 .WithMessage("La descripción no puede estar vacía.")
                 .MaximumLength(500);
             RuleFor(x => x.AdminNotes).MaximumLength(2000)
                 .When(x => x.AdminNotes != null);
         }
     }
     ```

- **Dependencies**: Step 3 (DTOs exist). Also ensure `FluentValidation` is imported.

---

### Step 5: Update PaymentsService — Sequential Logic & Manual Payment Methods

- **File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- **Action**: Add `ComputeIsActionable`, manual payment CRUD, update mapping methods, add proof upload validation
- **Implementation Steps**:

  1. **Add `ComputeIsActionable` private method**:
     ```csharp
     private static bool ComputeIsActionable(Payment payment, List<Payment> allPayments)
     {
         // Manual payments are always actionable if Pending
         if (payment.IsManual)
             return payment.Status == PaymentStatus.Pending;

         // Non-pending payments are not actionable
         if (payment.Status != PaymentStatus.Pending)
             return false;

         // P1 is always actionable
         if (payment.InstallmentNumber == 1)
             return true;

         // P2 requires P1 Completed
         if (payment.InstallmentNumber == 2)
         {
             var p1 = allPayments.FirstOrDefault(p => p.InstallmentNumber == 1);
             return p1?.Status == PaymentStatus.Completed;
         }

         // P3 requires P2 Completed
         if (payment.InstallmentNumber == 3)
         {
             var p2 = allPayments.FirstOrDefault(p => p.InstallmentNumber == 2);
             return p2?.Status == PaymentStatus.Completed;
         }

         return false;
     }
     ```

  2. **Update `MapToResponse`** — pass `allPayments` to compute `IsActionable`, include `IsManual` and `ManualConceptLine`:
     - The method currently takes a single `Payment`. Change signature to also accept `List<Payment> allPayments` (list of sibling payments for the same registration).
     - Add `IsActionable: ComputeIsActionable(payment, allPayments)` to the response mapping.
     - Add `IsManual: payment.IsManual` to the response mapping.
     - Deserialize and include `ManualLine` from `PaymentConceptLinesJson`.
     - **Important**: Update ALL callers of `MapToResponse` to pass the sibling payments list. For single-payment endpoints (`GetByIdAsync`), load all payments for the registration to compute `IsActionable`.

  3. **Update `MapToAdminResponse`** — same changes as `MapToResponse` (add `IsActionable`, `IsManual`, `ManualConceptLine`).

  4. **Add proof upload validation in `UploadProofAsync`**:
     - After loading the payment and before processing the upload, load all payments for the same registration.
     - Call `ComputeIsActionable(payment, allPayments)`.
     - If `false`, return an error result (409 Conflict):
       ```csharp
       if (!ComputeIsActionable(payment, allPayments))
       {
           return Results.Problem(
               "Debes completar el pago anterior antes de subir un comprobante.",
               statusCode: 409);
       }
       ```
     - **Note**: This requires the method signature or return type to support returning a problem result. If `UploadProofAsync` currently throws exceptions for validation, adapt to the existing pattern (check current error handling approach).

  5. **Add `IPaymentsService` interface methods** (add to interface):
     ```csharp
     Task<IResult> CreateManualPaymentAsync(Guid registrationId, CreateManualPaymentRequest request, Guid adminUserId, CancellationToken ct);
     Task<IResult> UpdateManualPaymentAsync(Guid paymentId, UpdateManualPaymentRequest request, Guid adminUserId, CancellationToken ct);
     Task<IResult> DeleteManualPaymentAsync(Guid paymentId, Guid adminUserId, CancellationToken ct);
     ```

  6. **Implement `CreateManualPaymentAsync`**:
     ```csharp
     public async Task<IResult> CreateManualPaymentAsync(
         Guid registrationId, CreateManualPaymentRequest request,
         Guid adminUserId, CancellationToken ct)
     {
         // 1. Load registration with payments, family unit, camp edition
         var registration = await _registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct);
         if (registration is null)
             return TypedResults.NotFound(ApiResponse<object>.Error("Inscripción no encontrada."));

         // 2. Determine next installment number
         var maxInstallment = registration.Payments.Any()
             ? registration.Payments.Max(p => p.InstallmentNumber)
             : 0;
         var nextInstallment = maxInstallment + 1;

         // 3. Load payment settings for transfer concept prefix
         var settings = await LoadPaymentSettingsAsync(ct);

         // 4. Generate transfer concept
         var familyName = NormalizeName(registration.FamilyUnit.Name);
         var concept = $"{settings.TransferConceptPrefix}-{familyName}-{nextInstallment}";
         if (concept.Length > 100) concept = concept[..100];

         // 5. Build concept lines
         var conceptLines = new PaymentConceptLinesJson(
             MemberLines: null,
             ExtraLines: null,
             ManualLine: new ManualPaymentConceptLine(request.Description, request.Amount)
         );

         // 6. Create payment
         var payment = new Payment
         {
             RegistrationId = registrationId,
             Amount = request.Amount,
             PaymentDate = DateTime.UtcNow,
             Method = PaymentMethod.Transfer,
             Status = PaymentStatus.Pending,
             InstallmentNumber = nextInstallment,
             DueDate = request.DueDate,
             TransferConcept = concept,
             AdminNotes = request.AdminNotes,
             IsManual = true,
             ConceptLinesSerialized = JsonSerializer.Serialize(conceptLines),
             CreatedAt = DateTime.UtcNow,
             UpdatedAt = DateTime.UtcNow
         };

         await _paymentsRepo.AddAsync(payment, ct);

         // 7. Update registration TotalAmount
         registration.TotalAmount += request.Amount;
         await _registrationsRepo.UpdateAsync(registration, ct);

         // 8. Return admin response
         var allPayments = await _paymentsRepo.GetByRegistrationIdAsync(registrationId, ct);
         return TypedResults.Created(
             $"/api/admin/payments/{payment.Id}",
             ApiResponse<AdminPaymentResponse>.Success(
                 MapToAdminResponse(payment, allPayments)));
     }
     ```

  7. **Implement `UpdateManualPaymentAsync`**:
     ```csharp
     public async Task<IResult> UpdateManualPaymentAsync(
         Guid paymentId, UpdateManualPaymentRequest request,
         Guid adminUserId, CancellationToken ct)
     {
         var payment = await _paymentsRepo.GetByIdAsync(paymentId, ct);
         if (payment is null)
             return TypedResults.NotFound(ApiResponse<object>.Error("Pago no encontrado."));

         if (!payment.IsManual)
             return TypedResults.UnprocessableEntity(
                 ApiResponse<object>.Error("Solo se pueden editar pagos manuales."));

         if (payment.Status != PaymentStatus.Pending)
             return TypedResults.UnprocessableEntity(
                 ApiResponse<object>.Error("Solo se pueden editar pagos en estado Pendiente."));

         var oldAmount = payment.Amount;

         // Apply partial updates
         if (request.Amount.HasValue)
             payment.Amount = request.Amount.Value;

         if (request.Description is not null)
         {
             var conceptLines = new PaymentConceptLinesJson(
                 MemberLines: null,
                 ExtraLines: null,
                 ManualLine: new ManualPaymentConceptLine(
                     request.Description, request.Amount ?? payment.Amount)
             );
             payment.ConceptLinesSerialized = JsonSerializer.Serialize(conceptLines);
         }
         else if (request.Amount.HasValue)
         {
             // Update amount in existing concept lines
             var (_, _, manualLine) = DeserializeConceptLines(payment.ConceptLinesSerialized);
             if (manualLine is not null)
             {
                 var conceptLines = new PaymentConceptLinesJson(
                     MemberLines: null,
                     ExtraLines: null,
                     ManualLine: manualLine with { Amount = request.Amount.Value }
                 );
                 payment.ConceptLinesSerialized = JsonSerializer.Serialize(conceptLines);
             }
         }

         if (request.DueDate.HasValue)
             payment.DueDate = request.DueDate.Value;

         if (request.AdminNotes is not null)
             payment.AdminNotes = request.AdminNotes;

         payment.UpdatedAt = DateTime.UtcNow;
         await _paymentsRepo.UpdateAsync(payment, ct);

         // Adjust registration TotalAmount if amount changed
         if (request.Amount.HasValue && request.Amount.Value != oldAmount)
         {
             var registration = await _registrationsRepo.GetByIdAsync(
                 payment.RegistrationId, ct);
             if (registration is not null)
             {
                 registration.TotalAmount += (request.Amount.Value - oldAmount);
                 await _registrationsRepo.UpdateAsync(registration, ct);
             }
         }

         var allPayments = await _paymentsRepo.GetByRegistrationIdAsync(
             payment.RegistrationId, ct);
         return TypedResults.Ok(ApiResponse<AdminPaymentResponse>.Success(
             MapToAdminResponse(payment, allPayments)));
     }
     ```

  8. **Implement `DeleteManualPaymentAsync`**:
     ```csharp
     public async Task<IResult> DeleteManualPaymentAsync(
         Guid paymentId, Guid adminUserId, CancellationToken ct)
     {
         var payment = await _paymentsRepo.GetByIdAsync(paymentId, ct);
         if (payment is null)
             return TypedResults.NotFound(ApiResponse<object>.Error("Pago no encontrado."));

         if (!payment.IsManual)
             return TypedResults.UnprocessableEntity(
                 ApiResponse<object>.Error("Solo se pueden eliminar pagos manuales."));

         if (payment.Status != PaymentStatus.Pending)
             return TypedResults.UnprocessableEntity(
                 ApiResponse<object>.Error("Solo se pueden eliminar pagos en estado Pendiente."));

         // Adjust registration TotalAmount
         var registration = await _registrationsRepo.GetByIdAsync(
             payment.RegistrationId, ct);
         if (registration is not null)
         {
             registration.TotalAmount -= payment.Amount;
             await _registrationsRepo.UpdateAsync(registration, ct);
         }

         await _paymentsRepo.DeleteAsync(payment, ct);

         return TypedResults.Ok(ApiResponse<object>.Success(null, "Pago manual eliminado."));
     }
     ```

  9. **Update `SyncBaseInstallmentsAsync`** — add guard to skip manual payments:
     - At the start or when filtering payments, ensure `IsManual == false`:
       ```csharp
       var basePayments = payments.Where(p => !p.IsManual).ToList();
       ```

  10. **Update `SyncExtrasInstallmentAsync`** — same guard:
      - Filter out manual payments when looking for P3:
        ```csharp
        var p3 = payments.FirstOrDefault(p => p.InstallmentNumber == 3 && !p.IsManual);
        ```

  11. **Update `DeserializeConceptLines`** — return `ManualLine` as well:
      - Current signature returns `(List<PaymentConceptLine>?, List<PaymentExtraConceptLine>?)`.
      - Update to return `(List<PaymentConceptLine>?, List<PaymentExtraConceptLine>?, ManualPaymentConceptLine?)`.
      - Update all callers.

---

### Step 6: Update PaymentsEndpoints — Add Manual Payment Routes

- **File**: `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs`
- **Action**: Add three new admin endpoints for manual payment CRUD
- **Implementation Steps**:

  1. **Add to the admin payment group** (the existing `/api/admin/payments` or create a sub-group under `/api/admin/registrations`):

     ```csharp
     // Manual payment management (admin only)
     // Under the admin group:
     adminGroup.MapPost(
         "/registrations/{registrationId:guid}/payments/manual",
         async (Guid registrationId, CreateManualPaymentRequest request,
                ClaimsPrincipal user, IPaymentsService service, CancellationToken ct) =>
         {
             var userId = user.GetUserId();
             var userRole = user.GetUserRole();
             if (userRole is not ("Admin" or "Board"))
                 return TypedResults.Forbid();

             return await service.CreateManualPaymentAsync(
                 registrationId, request, userId, ct);
         })
         .AddEndpointFilter<ValidationFilter<CreateManualPaymentRequest>>()
         .WithName("CreateManualPayment")
         .Produces<ApiResponse<AdminPaymentResponse>>(201)
         .Produces(403).Produces(404).Produces(422);
     ```

     > **Note on route structure**: The create endpoint uses `/registrations/{id}/payments/manual` because it needs the `registrationId`. The update/delete endpoints use `/payments/{id}/manual` since we already have the `paymentId`. Check how the existing admin group is structured and follow the same pattern. If admin endpoints are at `/api/admin/payments`, consider adding a separate admin registration group or nesting appropriately.

  2. **Update manual payment endpoint**:
     ```csharp
     adminGroup.MapPut(
         "/{paymentId:guid}/manual",
         async (Guid paymentId, UpdateManualPaymentRequest request,
                ClaimsPrincipal user, IPaymentsService service, CancellationToken ct) =>
         {
             var userId = user.GetUserId();
             var userRole = user.GetUserRole();
             if (userRole is not ("Admin" or "Board"))
                 return TypedResults.Forbid();

             return await service.UpdateManualPaymentAsync(
                 paymentId, request, userId, ct);
         })
         .AddEndpointFilter<ValidationFilter<UpdateManualPaymentRequest>>()
         .WithName("UpdateManualPayment")
         .Produces<ApiResponse<AdminPaymentResponse>>(200)
         .Produces(403).Produces(404).Produces(422);
     ```

  3. **Delete manual payment endpoint**:
     ```csharp
     adminGroup.MapDelete(
         "/{paymentId:guid}/manual",
         async (Guid paymentId, ClaimsPrincipal user,
                IPaymentsService service, CancellationToken ct) =>
         {
             var userId = user.GetUserId();
             var userRole = user.GetUserRole();
             if (userRole is not ("Admin" or "Board"))
                 return TypedResults.Forbid();

             return await service.DeleteManualPaymentAsync(paymentId, userId, ct);
         })
         .WithName("DeleteManualPayment")
         .Produces<ApiResponse<object>>(200)
         .Produces(403).Produces(404).Produces(422);
     ```

- **Dependencies**: Step 5 (service methods exist)

---

### Step 7: Create EF Core Migration

- **Action**: Generate a migration for the new `is_manual` column
- **Implementation Steps**:
  1. Run from solution root:
     ```bash
     dotnet ef migrations add AddIsManualToPayments --project src/Abuvi.API
     ```
  2. Review the generated migration to ensure it only adds:
     - `is_manual` boolean column with default `false`, NOT NULL
  3. Apply migration:
     ```bash
     dotnet ef database update --project src/Abuvi.API
     ```
- **Dependencies**: Steps 1 and 2 (entity + configuration)

---

### Step 8: Add Repository Methods (if needed)

- **File**: `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`
- **Action**: Verify existing repository methods suffice; add `DeleteAsync` if not present
- **Implementation Steps**:
  1. Check if `DeleteAsync(Payment payment, CancellationToken ct)` exists in the repository interface and implementation.
  2. If not, add:
     ```csharp
     // In IPaymentsRepository
     Task DeleteAsync(Payment payment, CancellationToken ct);

     // In PaymentsRepository
     public async Task DeleteAsync(Payment payment, CancellationToken ct)
     {
         _context.Payments.Remove(payment);
         await _context.SaveChangesAsync(ct);
     }
     ```
  3. Verify `AddAsync(Payment payment, CancellationToken ct)` exists for creating new payments.
  4. Verify `UpdateAsync(Payment payment, CancellationToken ct)` exists for updating payments.

---

### Step 9: Write Unit Tests

- **File**: `tests/Abuvi.API.Tests/Features/Payments/` (create test files as needed)
- **Action**: Add unit tests with xUnit + FluentAssertions + NSubstitute

#### 9a: Sequential Payment Tests — `ComputeIsActionableTests.cs`

Since `ComputeIsActionable` is a private static method, test it indirectly through the service methods that use it (e.g., `GetByRegistrationAsync`, `UploadProofAsync`). Alternatively, if the team prefers, make it `internal` with `[InternalsVisibleTo]`.

**Test Cases**:
1. **P1 Pending → IsActionable = true**
2. **P2 Pending, P1 Pending → IsActionable = false**
3. **P2 Pending, P1 Completed → IsActionable = true**
4. **P2 Pending, P1 PendingReview → IsActionable = false**
5. **P3 Pending, P2 Pending → IsActionable = false**
6. **P3 Pending, P2 Completed → IsActionable = true**
7. **P3 Pending, P1 Completed, P2 Pending → IsActionable = false** (P3 depends on P2, not P1)
8. **Manual payment Pending → IsActionable = true** (regardless of P1/P2 status)
9. **Completed payment → IsActionable = false**

#### 9b: Proof Upload Validation Tests

1. **Upload proof for P2 when P1 not completed → 409 Conflict**
2. **Upload proof for P2 when P1 completed → succeeds**
3. **Upload proof for P1 → always succeeds**
4. **Upload proof for manual payment → always succeeds**

#### 9c: Manual Payment CRUD Tests

**Successful Cases**:
1. Create manual payment → returns 201 with correct installment number, transfer concept, IsManual = true
2. Update manual payment amount → returns 200, registration TotalAmount adjusted
3. Update manual payment description → concept lines updated
4. Delete manual payment → returns 200, registration TotalAmount decreased

**Validation Errors**:
5. Create with Amount ≤ 0 → 400
6. Create with empty Description → 400
7. Update non-manual payment → 422
8. Delete non-manual payment → 422

**Not Found**:
9. Create on non-existent registration → 404
10. Update non-existent payment → 404
11. Delete non-existent payment → 404

**Business Rule Violations**:
12. Update completed manual payment → 422
13. Delete completed manual payment → 422

**Edge Cases**:
14. Multiple manual payments on same registration → correct installment numbering (4, 5, 6...)
15. Sync methods skip manual payments (add/remove members doesn't affect manual payments)

---

### Step 10: Update Technical Documentation

- **Action**: Review and update documentation to reflect new functionality
- **Implementation Steps**:

  1. **Update `ai-specs/specs/data-model.md`**:
     - Add `is_manual` column to `payments` table documentation
     - Document `ManualPaymentConceptLine` type

  2. **Update `ai-specs/specs/api-spec.yml`**:
     - Add `POST /api/admin/registrations/{registrationId}/payments/manual` endpoint
     - Add `PUT /api/admin/payments/{paymentId}/manual` endpoint
     - Add `DELETE /api/admin/payments/{paymentId}/manual` endpoint
     - Update `PaymentResponse` and `AdminPaymentResponse` schemas with `IsActionable`, `IsManual`, `ManualConceptLine`

  3. **Verify auto-generated OpenAPI** matches manual spec updates

- **References**: Follow `ai-specs/specs/documentation-standards.mdc`
- **Notes**: All documentation in English. This step is MANDATORY.

---

## Implementation Order

1. **Step 0** — Create/verify feature branch
2. **Step 1** — Add `IsManual` to Payment entity
3. **Step 2** — Add EF Core column mapping
4. **Step 3** — Add/update DTOs and response records
5. **Step 4** — Add validators
6. **Step 8** — Verify/add repository methods
7. **Step 5** — Update service (core logic — largest step)
8. **Step 6** — Add endpoints
9. **Step 7** — Create and apply EF migration
10. **Step 9** — Write unit tests
11. **Step 10** — Update documentation

---

## Testing Checklist

- [ ] `ComputeIsActionable` returns correct values for all P1/P2/P3 status combinations
- [ ] Proof upload blocked (409) for non-actionable payments
- [ ] Proof upload allowed for actionable payments
- [ ] Manual payment creation assigns correct sequential installment number
- [ ] Manual payment creation generates correct transfer concept
- [ ] Manual payment creation updates registration `TotalAmount`
- [ ] Manual payment update (amount) adjusts registration `TotalAmount`
- [ ] Manual payment update (description) regenerates concept lines
- [ ] Manual payment deletion adjusts registration `TotalAmount`
- [ ] Non-manual payment cannot be updated/deleted via manual endpoints (422)
- [ ] Completed manual payment cannot be updated/deleted (422)
- [ ] `SyncBaseInstallmentsAsync` skips manual payments
- [ ] `SyncExtrasInstallmentAsync` skips manual payments
- [ ] All existing payment tests still pass (backward compatibility)

---

## Error Response Format

All endpoints use `ApiResponse<T>` envelope:

| Scenario | HTTP Status | Response |
|----------|-------------|----------|
| Success (create) | 201 Created | `ApiResponse<AdminPaymentResponse>` |
| Success (update) | 200 OK | `ApiResponse<AdminPaymentResponse>` |
| Success (delete) | 200 OK | `ApiResponse<object>` with message |
| Validation error | 400 Bad Request | `ApiResponse<object>` with validation errors |
| Auth failure | 403 Forbidden | — |
| Not found | 404 Not Found | `ApiResponse<object>` with message |
| Business rule violation | 422 Unprocessable | `ApiResponse<object>` with message |
| Sequential violation (proof upload) | 409 Conflict | Problem details |

---

## Partial Update Support

The `UpdateManualPaymentRequest` supports partial updates:
- Only non-null fields are applied
- `Amount` uses `decimal?` — only updated if provided
- `Description` uses `string?` — only updated if provided
- `DueDate` uses `DateTime?` — only updated if provided
- `AdminNotes` uses `string?` — only updated if provided

---

## Dependencies

- **NuGet packages**: No new packages required (FluentValidation, System.Text.Json already present)
- **EF Core migration**:
  ```bash
  dotnet ef migrations add AddIsManualToPayments --project src/Abuvi.API
  dotnet ef database update --project src/Abuvi.API
  ```

---

## Notes

- **Sequential ordering is enforced server-side** via proof upload validation (409 Conflict). The `IsActionable` flag in responses is for frontend UX guidance, but the real enforcement is at the API level.
- **Admins bypass sequential ordering** — they can confirm/reject any payment regardless of order. This is intentional for edge case handling.
- **Manual payments are outside the installment system** — they use `InstallmentNumber > 3` and `IsManual = true`. They are never touched by sync/recalculation methods.
- **`TotalAmount` on Registration** must be kept in sync when manual payments are created, updated, or deleted.
- **Transfer concept naming**: Manual payments follow the same pattern (`PREFIX-NAME-N`) with the next available number.
- **All user-facing messages in Spanish** (error messages, validation messages).
- **RGPD/GDPR**: No new personal data fields introduced. Manual payment `Description` and `AdminNotes` should not contain personal data beyond what's necessary.

---

## Next Steps After Implementation

1. Frontend implementation for sequential payment UX (locked/unlocked states)
2. Frontend admin UI for manual payment CRUD
3. Integration testing of full flow (create registration → sequential payments → manual additions)
4. Consider notification emails when manual payments are created (future enhancement)

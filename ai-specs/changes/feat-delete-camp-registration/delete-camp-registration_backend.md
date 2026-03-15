# Backend Implementation Plan: Delete Camp Registration

## 1. Overview

Implement a hard-delete endpoint for camp registrations, allowing family representatives (within a 24-hour grace period) and admins to permanently remove `Pending`/`Draft` registrations so families can re-register from scratch for the same camp edition.

This follows **Vertical Slice Architecture** — all changes are scoped to the `Registrations` feature slice, with no new entities, migrations, or cross-cutting changes required.

## 2. Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Registrations/`
- **Files to modify**:
  - `RegistrationsEndpoints.cs` — new `DELETE` endpoint
  - `IRegistrationsService.cs` — new `DeleteAsync` signature
  - `RegistrationsService.cs` — business logic implementation
  - `IRegistrationsRepository.cs` — new `DeleteAsync` signature
  - `RegistrationsRepository.cs` — EF Core delete implementation
- **Test file to create**:
  - `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsService_DeleteAsync_Tests.cs`
- **Cross-cutting concerns**: None. Uses existing `ApiResponse<T>`, `NotFoundException`, `BusinessRuleException`, and authorization helpers.

## 3. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/delete-camp-registration-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `main` branch
  2. Pull latest changes: `git pull origin main`
  3. Create new branch: `git checkout -b feature/delete-camp-registration-backend`
  4. Verify branch creation: `git branch`
- **Notes**: This must be the FIRST step before any code changes.

---

### Step 1: Write Unit Tests (TDD — Red Phase)

- **File**: `src/Abuvi.Tests/Unit/Features/Registrations/RegistrationsService_DeleteAsync_Tests.cs`
- **Action**: Create test class with all test cases before implementation
- **Dependencies**: xUnit, NSubstitute, FluentAssertions (already available)

Follow existing test patterns from `RegistrationsServiceTests.cs`:

- Constructor setup with NSubstitute mocks for all `RegistrationsService` dependencies
- AAA pattern (Arrange, Act, Assert)
- Naming convention: `DeleteAsync_WhenCondition_ExpectedBehavior`

**Test Cases**:

#### Successful Cases

1. `DeleteAsync_WhenPendingRegistrationWithinTimeWindow_ShouldDeleteSuccessfully`
   - Arrange: Registration with `Status = Pending`, `CreatedAt = 1 hour ago`, no payments, requester is representative
   - Act: Call `DeleteAsync`
   - Assert: `_registrationsRepo.Received(1).DeleteAsync(registrationId, ct)`

2. `DeleteAsync_WhenDraftRegistrationWithinTimeWindow_ShouldDeleteSuccessfully`
   - Same as above but `Status = Draft`

3. `DeleteAsync_WhenAdminDeletesOutsideTimeWindow_ShouldDeleteSuccessfully`
   - Arrange: Registration `CreatedAt = 3 days ago`, `isAdminOrBoard = true`, no payments
   - Assert: Deletion succeeds despite expired time window

#### Not Found

1. `DeleteAsync_WhenRegistrationNotFound_ShouldThrowNotFoundException`
   - Arrange: `_registrationsRepo.GetByIdWithDetailsAsync()` returns `null`
   - Assert: Throws `NotFoundException`

#### Authorization Errors

1. `DeleteAsync_WhenUserIsNotRepresentativeOrAdmin_ShouldThrowUnauthorizedAccessException`
   - Arrange: Requester userId does not match `RegisteredByUserId`, `isAdminOrBoard = false`
   - Assert: Throws `UnauthorizedAccessException`

#### Business Rule Violations

1. `DeleteAsync_WhenStatusIsConfirmed_ShouldThrowBusinessRuleException`
   - Arrange: `Status = Confirmed`
   - Assert: Throws `BusinessRuleException` with message about confirmed registrations

2. `DeleteAsync_WhenStatusIsCancelled_ShouldThrowBusinessRuleException`
   - Arrange: `Status = Cancelled`
   - Assert: Throws `BusinessRuleException` with message about cancelled registrations

3. `DeleteAsync_WhenPaymentsExist_ShouldThrowBusinessRuleException`
   - Arrange: Registration has 1+ payments in `Payments` collection
   - Assert: Throws `BusinessRuleException` with message about existing payments

4. `DeleteAsync_WhenTimeWindowExpiredForRepresentative_ShouldThrowBusinessRuleException`
   - Arrange: `CreatedAt = 25 hours ago`, `isAdminOrBoard = false`
   - Assert: Throws `BusinessRuleException` with message about 24-hour window

5. `DeleteAsync_WhenAdminAndPaymentsExist_ShouldStillThrowBusinessRuleException`
    - Arrange: `isAdminOrBoard = true`, registration has payments
    - Assert: Throws `BusinessRuleException` (payment guard applies to admins too)

**Implementation Notes**:

- Mock `_registrationsRepo.GetByIdWithDetailsAsync()` to return a `Registration` entity with all needed navigation properties (especially `Payments` collection and `FamilyUnit.RepresentativeUserId`)
- Use `RegistrationBuilder` if available, otherwise construct manually
- All tests should FAIL at this point (Red phase)

---

### Step 2: Add Repository Method

- **File**: `src/Abuvi.API/Features/Registrations/IRegistrationsRepository.cs`
- **Action**: Add method signature to interface
- **Function Signature**:

  ```csharp
  Task DeleteAsync(Guid id, CancellationToken ct = default);
  ```

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`
- **Action**: Implement the delete method
- **Implementation Steps**:
  1. Call `_context.Registrations.FindAsync(new object[] { id }, ct)` to load the entity
  2. If entity is `null`, return (no-op, service handles not-found)
  3. Call `_context.Registrations.Remove(entity)`
  4. Call `await _context.SaveChangesAsync(ct)`
- **Implementation Notes**:
  - Cascade FK configuration automatically deletes `RegistrationMembers`, `RegistrationExtras`, and `RegistrationAccommodationPreferences`
  - `Payments` FK is `Restrict` — if payments exist, the DB will throw. The service layer prevents this by checking first.
  - Follow the same pattern as `CampEditionExtrasRepository.DeleteAsync()`

---

### Step 3: Add Service Method

- **File**: `src/Abuvi.API/Features/Registrations/IRegistrationsService.cs`
- **Action**: Add method signature to interface
- **Function Signature**:

  ```csharp
  Task DeleteAsync(Guid registrationId, Guid requestingUserId, bool isAdminOrBoard, CancellationToken ct = default);
  ```

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Implement `DeleteAsync` with business rules
- **Implementation Steps**:

  1. **Load registration with details**:

     ```csharp
     var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct);
     if (registration is null)
         throw new NotFoundException(nameof(Registration), registrationId);
     ```

  2. **Validate authorization** (follow `CancelAsync` pattern):

     ```csharp
     if (!isAdminOrBoard)
     {
         var familyUnit = registration.FamilyUnit;
         if (familyUnit?.RepresentativeUserId != requestingUserId)
             throw new UnauthorizedAccessException("You are not authorized to delete this registration.");
     }
     ```

  3. **Validate status** — only `Pending` or `Draft`:

     ```csharp
     if (registration.Status is RegistrationStatus.Confirmed)
         throw new BusinessRuleException("Confirmed registrations cannot be deleted. Please cancel first.");
     if (registration.Status is RegistrationStatus.Cancelled)
         throw new BusinessRuleException("Cancelled registrations cannot be deleted.");
     ```

  4. **Validate no payments exist**:

     ```csharp
     if (registration.Payments?.Any() == true)
         throw new BusinessRuleException("Cannot delete registration with existing payments. Please contact an administrator.");
     ```

  5. **Validate time window (representative only)**:

     ```csharp
     if (!isAdminOrBoard)
     {
         var gracePeriod = TimeSpan.FromHours(24);
         if (DateTime.UtcNow - registration.CreatedAt > gracePeriod)
             throw new BusinessRuleException("Registration can only be deleted within 24 hours of creation.");
     }
     ```

  6. **Execute deletion**:

     ```csharp
     await registrationsRepo.DeleteAsync(registrationId, ct);
     ```

  7. **Log the action**:

     ```csharp
     logger.LogInformation(
         "Registration {RegistrationId} deleted by user {UserId} (Admin: {IsAdmin})",
         registrationId, requestingUserId, isAdminOrBoard);
     ```

- **Implementation Notes**:
  - Order of validation matters: authorization first, then status, then payments, then time window
  - Uses `GetByIdWithDetailsAsync()` to eagerly load `Payments` and `FamilyUnit` in a single query
  - No email notification on delete (unlike cancel) — the record is gone, there's nothing to notify about
  - No return value needed (void/Task) — follows the `DELETE → 204` convention

---

### Step 4: Add Endpoint

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`
- **Action**: Add `DELETE /{id:guid}` endpoint mapping and handler
- **Implementation Steps**:

  1. **Add route mapping** (in the `MapRegistrationEndpoints` method, alongside existing endpoints):

     ```csharp
     group.MapDelete("/{id:guid}", DeleteRegistration)
         .WithName("DeleteRegistration")
         .Produces(StatusCodes.Status204NoContent)
         .Produces(StatusCodes.Status401Unauthorized)
         .Produces(StatusCodes.Status403Forbidden)
         .Produces(StatusCodes.Status404NotFound)
         .Produces(StatusCodes.Status409Conflict)
         .Produces(StatusCodes.Status422UnprocessableEntity)
         .RequireAuthorization();
     ```

  2. **Add handler method** (follow existing `CancelRegistration` pattern):

     ```csharp
     private static async Task<IResult> DeleteRegistration(
         [FromRoute] Guid id,
         IRegistrationsService service,
         ClaimsPrincipal user,
         CancellationToken ct)
     {
         try
         {
             var userId = user.GetUserId();
             var userRole = user.GetUserRole();
             var isAdminOrBoard = userRole is "Admin" or "Board";

             await service.DeleteAsync(id, userId, isAdminOrBoard, ct);
             return Results.NoContent();
         }
         catch (NotFoundException ex)
         {
             return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
         }
         catch (UnauthorizedAccessException)
         {
             return TypedResults.Forbid();
         }
         catch (BusinessRuleException ex)
         {
             // Use 409 Conflict for payment-related errors, 422 for other business rules
             if (ex.Message.Contains("payments", StringComparison.OrdinalIgnoreCase))
                 return TypedResults.Conflict(ApiResponse<object>.Fail(ex.Message, "REGISTRATION_HAS_PAYMENTS"));

             return TypedResults.UnprocessableEntity(ApiResponse<object>.Fail(ex.Message, "REGISTRATION_DELETE_BLOCKED"));
         }
     }
     ```

- **Implementation Notes**:
  - Follow the same try/catch + `ApiResponse` pattern used in `CancelRegistration`
  - Use `TypedResults` for strongly-typed responses
  - Inject `IRegistrationsService` (interface), not `RegistrationsService` (concrete)
  - Error code `REGISTRATION_HAS_PAYMENTS` for payment guard (409), `REGISTRATION_DELETE_BLOCKED` for other business rules (422)

---

### Step 5: Verify Tests Pass (TDD — Green Phase)

- **Action**: Run all unit tests and verify they pass
- **Command**: `dotnet test src/Abuvi.Tests/ --filter "RegistrationsService_DeleteAsync"`
- **Implementation Steps**:
  1. Run tests — all 10 should now pass
  2. If any fail, fix the implementation (not the tests)
  3. Run full test suite to ensure no regressions: `dotnet test src/Abuvi.Tests/`

---

### Step 6: Refactor (TDD — Refactor Phase)

- **Action**: Review implementation for code quality
- **Implementation Steps**:
  1. Check for duplication with `CancelAsync` — consider extracting shared authorization logic if the pattern repeats more than twice
  2. Verify error messages are clear and consistent
  3. Ensure structured logging follows existing patterns
  4. Run tests again after any refactoring

---

### Step 7: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Update `ai-specs/specs/data-model.md`**: Add note about hard-delete behavior for registrations (cascade children, restrict payments)
  2. **Update `ai-specs/specs/api-spec.yml`**: Add `DELETE /api/registrations/{id}` endpoint definition with:
     - Path parameter: `id` (guid)
     - Responses: 204, 401, 403, 404, 409, 422
     - Authorization: Bearer token required
     - Description: Deletes a camp registration permanently. Representatives have a 24-hour grace period. Admins can delete anytime. Blocked if payments exist.
  3. **Verify auto-generated OpenAPI** matches the new endpoint
  4. **Confirm all documentation is in English**
- **Notes**: This step is MANDATORY before considering the implementation complete.

---

## 4. Implementation Order

1. **Step 0**: Create feature branch `feature/delete-camp-registration-backend`
2. **Step 1**: Write unit tests (TDD Red phase — all tests fail)
3. **Step 2**: Add repository method (`IRegistrationsRepository` + `RegistrationsRepository`)
4. **Step 3**: Add service method (`IRegistrationsService` + `RegistrationsService`)
5. **Step 4**: Add endpoint (`RegistrationsEndpoints.cs`)
6. **Step 5**: Verify all tests pass (TDD Green phase)
7. **Step 6**: Refactor if needed (TDD Refactor phase)
8. **Step 7**: Update technical documentation

## 5. Testing Checklist

- [ ] `DeleteAsync_WhenPendingRegistrationWithinTimeWindow_ShouldDeleteSuccessfully`
- [ ] `DeleteAsync_WhenDraftRegistrationWithinTimeWindow_ShouldDeleteSuccessfully`
- [ ] `DeleteAsync_WhenAdminDeletesOutsideTimeWindow_ShouldDeleteSuccessfully`
- [ ] `DeleteAsync_WhenRegistrationNotFound_ShouldThrowNotFoundException`
- [ ] `DeleteAsync_WhenUserIsNotRepresentativeOrAdmin_ShouldThrowUnauthorizedAccessException`
- [ ] `DeleteAsync_WhenStatusIsConfirmed_ShouldThrowBusinessRuleException`
- [ ] `DeleteAsync_WhenStatusIsCancelled_ShouldThrowBusinessRuleException`
- [ ] `DeleteAsync_WhenPaymentsExist_ShouldThrowBusinessRuleException`
- [ ] `DeleteAsync_WhenTimeWindowExpiredForRepresentative_ShouldThrowBusinessRuleException`
- [ ] `DeleteAsync_WhenAdminAndPaymentsExist_ShouldStillThrowBusinessRuleException`
- [ ] Full test suite passes with no regressions
- [ ] Manual smoke test: DELETE endpoint returns 204 on success

## 6. Error Response Format

| Scenario | HTTP Status | Error Code | Message |
|----------|-------------|------------|---------|
| Success | 204 | — | No body |
| Not found | 404 | `NOT_FOUND` | "Registration with ID {id} was not found." |
| Not authorized | 403 | — | Forbid (no body) |
| Confirmed status | 422 | `REGISTRATION_DELETE_BLOCKED` | "Confirmed registrations cannot be deleted. Please cancel first." |
| Cancelled status | 422 | `REGISTRATION_DELETE_BLOCKED` | "Cancelled registrations cannot be deleted." |
| Payments exist | 409 | `REGISTRATION_HAS_PAYMENTS` | "Cannot delete registration with existing payments. Please contact an administrator." |
| Time window expired | 422 | `REGISTRATION_DELETE_BLOCKED` | "Registration can only be deleted within 24 hours of creation." |

All errors wrapped in `ApiResponse<object>` envelope:

```json
{
  "success": false,
  "message": "Error message here",
  "error": {
    "code": "ERROR_CODE",
    "details": null
  }
}
```

## 7. Dependencies

- **No new NuGet packages** required
- **No EF Core migration** needed (no schema changes — using existing cascade/restrict FK configuration)
- All infrastructure already exists in the project

## 8. Notes

- **Business rule**: The 24-hour grace period is hardcoded. If it needs to be configurable in the future, extract to `appsettings.json` — but do NOT over-engineer this now.
- **Payments guard**: Even admins cannot delete registrations with payments. If an admin needs to force-delete, payments must be manually removed first (separate operation, out of scope).
- **No email notification**: Unlike cancel, delete does not send emails. The record is permanently removed.
- **GDPR**: Hard delete is actually GDPR-friendly — it removes personal data. No additional data retention concerns.
- **Concurrency**: No special isolation level needed. The delete is atomic at the DB level. If a payment is created between the check and the delete, the `Restrict` FK will catch it.

## 9. Next Steps After Implementation

- Frontend: Add delete button to registration detail view (separate frontend ticket)
- Integration test with `WebApplicationFactory` (optional, based on project coverage goals)
- PR review and merge to `main`

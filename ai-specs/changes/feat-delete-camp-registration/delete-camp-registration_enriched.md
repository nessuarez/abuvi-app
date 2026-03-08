# User Story: Delete Camp Registration

## Summary

As a **family representative** or **admin/board member**, I want to **delete a camp registration shortly after creation** so that I can **start over from scratch** if I made mistakes or changed my mind.

## Context & Motivation

Some users create a camp registration and immediately realize they need to start over (wrong members selected, wrong edition, etc.). Currently, the only option is to cancel (`POST /api/registrations/{id}/cancel`), which changes the status to `Cancelled` but preserves the record. This prevents re-registration due to the unique constraint `IX_Registrations_FamilyUnitId_CampEditionId` (one registration per family per camp edition).

A **hard delete** is needed so the family can register again from zero for the same camp edition.

## Recommended Strategy

**Hard delete with a time window and payment guard.**

### Why hard delete instead of soft delete?

- The unique constraint `IX_Registrations_FamilyUnitId_CampEditionId` prevents creating a new registration for the same family + edition if the old one still exists (even if soft-deleted).
- The goal is "start again from zero" — the old record has no business value.
- Child entities (Members, Extras, AccommodationPreferences) already cascade-delete via FK configuration.

### Why a time window?

- Unrestricted deletion is risky — a registration with confirmed payments or that has been around for days should not be silently removed.
- A configurable grace period (e.g., **24 hours after creation**) limits the feature to its intended use case: fixing mistakes right after creation.
- Admins bypass the time window and can delete any registration regardless of age.

### Why a payment guard?

- The `Payments` FK uses `DeleteBehavior.Restrict` — the database will reject deletion if payments exist.
- The service must check for payments and return a clear error before hitting the DB constraint.

---

## Functional Requirements

### Business Rules

| # | Rule | Details |
|---|------|---------|
| BR-1 | **Time window for representatives** | A representative can only delete a registration created within the last **24 hours** (`CreatedAt + 24h > now`). |
| BR-2 | **No payments exist** | Deletion is blocked if the registration has **any** associated payments (regardless of status). Return a clear error message. |
| BR-3 | **Status restriction** | Only registrations with status `Pending` or `Draft` can be deleted by representatives. `Confirmed` and `Cancelled` registrations cannot be deleted by representatives. |
| BR-4 | **Admin override** | Admin/Board roles can delete any `Pending`, `Draft`, or `Cancelled` registration regardless of the time window (BR-1 does not apply). `Confirmed` registrations still cannot be deleted. Payment guard (BR-2) still applies. |
| BR-5 | **Authorization** | The requester must be the representative of the registration's family unit, OR have Admin/Board role. |
| BR-6 | **Cascade cleanup** | Deleting a registration removes all child entities: `RegistrationMembers`, `RegistrationExtras`, `RegistrationAccommodationPreferences`. |

### Edge Cases

- If the registration is already cancelled (`Status = Cancelled`) and the requester is not an admin, return 422 with message: "Cancelled registrations cannot be deleted."
- If the registration is confirmed (`Status = Confirmed`), return 422 with message: "Confirmed registrations cannot be deleted. Please cancel first."
- If payments exist, return 409 with message: "Cannot delete registration with existing payments. Please contact an administrator."
- If the time window has expired (representative only), return 422 with message: "Registration can only be deleted within 24 hours of creation."

---

## Technical Specification

### Backend

#### 1. Endpoint

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Route | `/api/registrations/{id:guid}` |
| Auth | `.RequireAuthorization()` |
| Success | `204 NoContent` |
| Errors | `401`, `403`, `404`, `409`, `422` |

Add to: [RegistrationsEndpoints.cs](src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs)

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

#### 2. Service Method

Add to `IRegistrationsService` and `RegistrationsService`:

```csharp
Task DeleteAsync(Guid registrationId, Guid requestingUserId, bool isAdmin, CancellationToken ct);
```

**Logic flow:**

1. Fetch registration by ID (include Payments). Throw `NotFoundException` if not found.
2. Validate authorization: requester is representative of `FamilyUnitId` OR `isAdmin`.
3. Validate status: if admin, allow `Pending`, `Draft`, or `Cancelled`; if representative, allow only `Pending` or `Draft`. Throw `BusinessRuleException` otherwise.
4. Validate no payments exist. Throw `BusinessRuleException` if any.
5. If not admin, validate `CreatedAt + 24h > DateTime.UtcNow`. Throw `BusinessRuleException` if expired.
6. Call repository `DeleteAsync(id, ct)`.
7. Log deletion with structured context: `registrationId`, `requestingUserId`, `isAdmin`.

#### 3. Repository Method

Add to `IRegistrationsRepository` and `RegistrationsRepository`:

```csharp
Task DeleteAsync(Guid id, CancellationToken ct);
```

Implementation: `FindAsync` + `Remove` + `SaveChangesAsync`. Cascade FK configuration handles child entity cleanup automatically.

#### 4. Endpoint Handler

```csharp
private static async Task<IResult> DeleteRegistration(
    [FromRoute] Guid id,
    RegistrationsService service,
    ClaimsPrincipal user,
    CancellationToken ct)
{
    var userId = user.GetUserId();
    var isAdmin = user.IsInRole("Admin") || user.IsInRole("Board");
    await service.DeleteAsync(id, userId, isAdmin, ct);
    return Results.NoContent();
}
```

### Files to Modify

| File | Change |
|------|--------|
| `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` | Add `DELETE /{id:guid}` endpoint |
| `src/Abuvi.API/Features/Registrations/IRegistrationsService.cs` | Add `DeleteAsync` method signature |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Implement `DeleteAsync` with business rules |
| `src/Abuvi.API/Features/Registrations/IRegistrationsRepository.cs` | Add `DeleteAsync` method signature |
| `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs` | Implement `DeleteAsync` |

### Frontend (if applicable)

- Add a "Delete" button on the registration detail view, visible only when:
  - Registration status is `Pending` or `Draft`
  - User is the representative OR admin
- Show a confirmation dialog: "Are you sure you want to delete this registration? This action cannot be undone. You will be able to register again for this camp edition."
- On success, redirect to the registrations list.
- On error, display the error message from the API response.

---

## Unit Tests

Following TDD, create tests in `src/Abuvi.Tests/Unit/Features/Registrations/`:

### `RegistrationsService_DeleteAsync_Tests.cs`

| Test | Expected |
|------|----------|
| `Should_delete_pending_registration_within_time_window` | Success, repository `DeleteAsync` called |
| `Should_delete_draft_registration_within_time_window` | Success |
| `Should_throw_NotFoundException_when_registration_not_found` | `NotFoundException` |
| `Should_throw_when_status_is_Confirmed` | `BusinessRuleException` |
| `Should_throw_when_status_is_Cancelled_and_user_is_representative` | `BusinessRuleException` |
| `Should_allow_admin_to_delete_cancelled_registration` | Success (admin bypass) |
| `Should_throw_when_payments_exist` | `BusinessRuleException` |
| `Should_throw_when_time_window_expired_for_representative` | `BusinessRuleException` |
| `Should_allow_admin_to_delete_outside_time_window` | Success (admin bypass) |
| `Should_throw_when_user_is_not_representative_or_admin` | Authorization exception |
| `Should_still_block_admin_when_payments_exist` | `BusinessRuleException` |

---

## Non-Functional Requirements

- **Security**: Authorization check before any business logic. No information leakage in error messages for unauthorized users.
- **Performance**: Single DB round-trip for delete (cascade handles children). Include `Payments` in the initial query to avoid N+1.
- **Logging**: Structured log entry on successful deletion with `registrationId`, `userId`, `isAdmin` for audit trail.
- **Idempotency**: Return `404` if registration doesn't exist (not 204), ensuring clear feedback.

---

## Acceptance Criteria

- [ ] Representative can delete their own `Pending`/`Draft` registration within 24 hours of creation
- [ ] Representative cannot delete registration after 24-hour window
- [ ] Admin/Board can delete any `Pending`/`Draft` registration regardless of time
- [ ] Deletion is blocked when payments exist (for both representative and admin)
- [ ] Deletion is blocked for `Confirmed` registrations (for everyone) and `Cancelled` registrations (for representatives only)
- [ ] Admin/Board can delete `Cancelled` registrations
- [ ] After deletion, the family can create a new registration for the same camp edition
- [ ] Confirmation dialog shown before deletion on the frontend
- [ ] All unit tests pass
- [ ] API returns appropriate HTTP status codes for all scenarios

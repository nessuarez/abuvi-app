# Backend Implementation Plan: fix-membership-enrollment — Membership Enrollment & Fee Fixes

## Overview

This plan addresses three backend gaps in the membership system identified in the enriched bug report:

1. **Auto-create first-year fee on membership activation** — `CreateAsync` and `BulkActivateAsync` must generate a `MembershipFee(Status=Pending)` for the start year so that admins can immediately pay it and unlock camp registration.
2. **Manual fee creation endpoint** — A new `POST /api/memberships/{membershipId}/fees` endpoint (Admin/Board only) to charge an annual fee to an existing active membership when the automated background service hasn't generated it yet.
3. **Membership reactivation endpoint** — A new `POST /membership/reactivate` endpoint (Admin/Board only) to reactivate a deactivated membership, avoiding the DB unique-constraint crash that would occur if `CreateAsync` were called on a member with an existing `IsActive=false` record.

**No schema migrations are required.** The DB model already has the correct structure (`membership_fees` table, unique index on `(membership_id, year)`, etc.).

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Memberships/`

| File | Action |
|------|--------|
| `MembershipsModels.cs` | Add two new request DTOs |
| `MembershipsRepository.cs` | Add `GetByFamilyMemberIdIgnoringActiveAsync` |
| `MembershipsService.cs` | Fix `CreateAsync` + `BulkActivateAsync`; add `CreateFeeAsync`; add `ReactivateAsync` |
| `MembershipsEndpoints.cs` | Register two new endpoints |
| `CreateMembershipFeeValidator.cs` *(new)* | FluentValidation for the new fee DTO |
| `ReactivateMembershipValidator.cs` *(new)* | FluentValidation for the reactivate DTO |
| `Program.cs` | No changes required (services already registered) |

**Tests:**

| File | Action |
|------|--------|
| `Unit/Features/Memberships/MembershipsServiceTests.cs` | Update existing `CreateAsync` tests; add new cases for fee auto-creation |
| `Unit/Features/Memberships/MembershipsServiceTests_CreateFee.cs` *(new)* | Unit tests for `CreateFeeAsync` |
| `Unit/Features/Memberships/MembershipsServiceTests_Reactivate.cs` *(new)* | Unit tests for `ReactivateAsync` |
| `Integration/Features/Memberships/MembershipsEndpointsTests.cs` | Add integration tests for the two new endpoints |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Switch to `dev`, pull latest, create branch.
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/fix-membership-enrollment-backend`
  3. `git branch` — verify you are on the new branch.

---

### Step 1: Add Request DTOs

**File:** `src/Abuvi.API/Features/Memberships/MembershipsModels.cs`

Add at the bottom of the file, after the existing DTOs:

```csharp
/// <summary>Admin/Board: manually create an annual fee for an existing membership.</summary>
public record CreateMembershipFeeRequest(int Year, decimal Amount);

/// <summary>Admin/Board: reactivate a deactivated membership.</summary>
public record ReactivateMembershipRequest(int Year);
```

---

### Step 2: Add FluentValidation Validators

**File (new):** `src/Abuvi.API/Features/Memberships/CreateMembershipFeeValidator.cs`

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class CreateMembershipFeeValidator : AbstractValidator<CreateMembershipFeeRequest>
{
    public CreateMembershipFeeValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.Year)
            .WithMessage("Year must be between 2001 and the current year.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Amount must be >= 0.");
    }
}
```

**File (new):** `src/Abuvi.API/Features/Memberships/ReactivateMembershipValidator.cs`

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class ReactivateMembershipValidator : AbstractValidator<ReactivateMembershipRequest>
{
    public ReactivateMembershipValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000)
            .LessThanOrEqualTo(_ => DateTime.UtcNow.Year)
            .WithMessage("Year must be between 2001 and the current year.");
    }
}
```

---

### Step 3: Update Repository — Add `GetByFamilyMemberIdIgnoringActiveAsync`

**File:** `src/Abuvi.API/Features/Memberships/MembershipsRepository.cs`

**Interface** — add to `IMembershipsRepository`:

```csharp
Task<Membership?> GetByFamilyMemberIdIgnoringActiveAsync(Guid familyMemberId, CancellationToken ct);
```

**Implementation** — add to `MembershipsRepository`:

```csharp
public async Task<Membership?> GetByFamilyMemberIdIgnoringActiveAsync(Guid familyMemberId, CancellationToken ct)
    => await db.Memberships
        .AsNoTracking()
        .Include(m => m.Fees)
        .FirstOrDefaultAsync(m => m.FamilyMemberId == familyMemberId, ct);
```

> Note: No `&& m.IsActive` filter — returns the record regardless of active state.

---

### Step 4: Fix `CreateAsync` — Auto-create first-year `MembershipFee`

**File:** `src/Abuvi.API/Features/Memberships/MembershipsService.cs`

After the existing `await repository.AddAsync(membership, ct);` call inside `CreateAsync`, add:

```csharp
// Auto-create fee for the start year (admin will mark as Paid separately)
var fee = new MembershipFee
{
    Id = Guid.NewGuid(),
    MembershipId = membership.Id,
    Year = request.Year,
    Amount = 0m,
    Status = FeeStatus.Pending,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
await repository.AddFeeAsync(fee, ct);
```

The final return must include fees. Because the membership is inserted before the fee, the `Fees` collection on the returned object will be empty at this point. Update the final `return` to reload fees from the fee just created:

```csharp
// Reload fees so the response reflects the newly created fee
var created = new Membership
{
    Id = membership.Id,
    FamilyMemberId = membership.FamilyMemberId,
    MemberNumber = membership.MemberNumber,
    StartDate = membership.StartDate,
    EndDate = membership.EndDate,
    IsActive = membership.IsActive,
    Fees = new List<MembershipFee> { fee },
    CreatedAt = membership.CreatedAt,
    UpdatedAt = membership.UpdatedAt
};
return created.ToResponse();
```

> Alternatively, reload via `repository.GetByIdAsync(membership.Id, ct)` — simpler but adds a DB round-trip. Either approach is acceptable.

---

### Step 5: Fix `BulkActivateAsync` — Auto-create `MembershipFee` per activated member

**File:** `src/Abuvi.API/Features/Memberships/MembershipsService.cs`

Inside the `foreach (var member in members)` loop, after `await repository.AddAsync(membership, ct);`, add:

```csharp
var memberFee = new MembershipFee
{
    Id = Guid.NewGuid(),
    MembershipId = membership.Id,
    Year = request.Year,
    Amount = 0m,
    Status = FeeStatus.Pending,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
await repository.AddFeeAsync(memberFee, ct);
```

---

### Step 6: Add `CreateFeeAsync` to Service

**File:** `src/Abuvi.API/Features/Memberships/MembershipsService.cs`

Add a new public method:

```csharp
public async Task<MembershipFeeResponse> CreateFeeAsync(
    Guid membershipId,
    CreateMembershipFeeRequest request,
    CancellationToken ct)
{
    var membership = await repository.GetByIdAsync(membershipId, ct);
    if (membership is null)
        throw new NotFoundException(nameof(Membership), membershipId);

    // Check for duplicate fee for the same year
    var existingFee = await repository.GetCurrentYearFeeAsync(membershipId, ct);
    // GetCurrentYearFeeAsync only checks current year, so use a broader check:
    var allFees = await repository.GetFeesByMembershipAsync(membershipId, ct);
    if (allFees.Any(f => f.Year == request.Year))
        throw new BusinessRuleException($"A fee for year {request.Year} already exists for this membership.");

    var fee = new MembershipFee
    {
        Id = Guid.NewGuid(),
        MembershipId = membershipId,
        Year = request.Year,
        Amount = request.Amount,
        Status = FeeStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    await repository.AddFeeAsync(fee, ct);

    return fee.ToResponse();
}
```

> **Alternative for the duplicate check**: Add a dedicated repository method `GetFeeByYearAsync(Guid membershipId, int year, CancellationToken ct)` to avoid loading all fees. This is preferable for performance. See Step 3-bis below.

#### Step 3-bis (optional but recommended): Add `GetFeeByYearAsync` to repository

**Interface addition:**
```csharp
Task<MembershipFee?> GetFeeByYearAsync(Guid membershipId, int year, CancellationToken ct);
```

**Implementation:**
```csharp
public async Task<MembershipFee?> GetFeeByYearAsync(Guid membershipId, int year, CancellationToken ct)
    => await db.MembershipFees
        .AsNoTracking()
        .FirstOrDefaultAsync(f => f.MembershipId == membershipId && f.Year == year, ct);
```

If `GetFeeByYearAsync` is added, simplify `CreateFeeAsync`:
```csharp
var existing = await repository.GetFeeByYearAsync(membershipId, request.Year, ct);
if (existing is not null)
    throw new BusinessRuleException($"A fee for year {request.Year} already exists for this membership.");
```

---

### Step 7: Add `ReactivateAsync` to Service

**File:** `src/Abuvi.API/Features/Memberships/MembershipsService.cs`

```csharp
public async Task<MembershipResponse> ReactivateAsync(
    Guid familyMemberId,
    ReactivateMembershipRequest request,
    CancellationToken ct)
{
    var membership = await repository.GetByFamilyMemberIdIgnoringActiveAsync(familyMemberId, ct);

    if (membership is null)
        throw new NotFoundException(nameof(Membership), familyMemberId);

    if (membership.IsActive)
        throw new BusinessRuleException("The member already has an active membership.");

    // Reactivate
    membership.IsActive = true;
    membership.EndDate = null;
    membership.UpdatedAt = DateTime.UtcNow;

    await repository.UpdateAsync(membership, ct);

    // Create fee for the requested year (if not already present)
    var existingFee = await repository.GetFeeByYearAsync(membership.Id, request.Year, ct);
    if (existingFee is null)
    {
        var fee = new MembershipFee
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Year = request.Year,
            Amount = 0m,
            Status = FeeStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await repository.AddFeeAsync(fee, ct);
    }

    // Reload updated membership
    var updated = await repository.GetByFamilyMemberIdAsync(familyMemberId, ct);
    return updated!.ToResponse();
}
```

> `UpdateAsync` uses `db.Memberships.Update(membership)` which tracks the entity by PK. Since `GetByFamilyMemberIdIgnoringActiveAsync` uses `AsNoTracking()`, this is correct — the call to `Update()` attaches the detached entity.

---

### Step 8: Register New Endpoints

**File:** `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs`

#### 8a — Add `POST /api/memberships/{membershipId}/fees` to `MapMembershipFeeEndpoints`

Inside the existing `group` in `MapMembershipFeeEndpoints`, add:

```csharp
group.MapPost("/", CreateMembershipFee)
    .WithName("CreateMembershipFee")
    .WithSummary("Manually create an annual fee for a membership (Admin/Board only)")
    .AddEndpointFilter<ValidationFilter<CreateMembershipFeeRequest>>()
    .Produces<ApiResponse<MembershipFeeResponse>>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status409Conflict);
```

Add the handler:

```csharp
private static async Task<IResult> CreateMembershipFee(
    [FromRoute] Guid membershipId,
    [FromBody] CreateMembershipFeeRequest request,
    ClaimsPrincipal user,
    MembershipsService service,
    CancellationToken ct)
{
    var userRole = user.GetUserRole();
    if (userRole != "Admin" && userRole != "Board")
        return Results.Forbid();

    var fee = await service.CreateFeeAsync(membershipId, request, ct);
    return Results.Created(
        $"/api/memberships/{membershipId}/fees/{fee.Id}",
        ApiResponse<MembershipFeeResponse>.Ok(fee));
}
```

#### 8b — Add `POST /api/family-units/{familyUnitId}/members/{memberId}/membership/reactivate` to `MapMembershipsEndpoints`

Inside the existing `group` in `MapMembershipsEndpoints`, add:

```csharp
group.MapPost("/reactivate", ReactivateMembership)
    .WithName("ReactivateMembership")
    .WithSummary("Reactivate a previously deactivated membership (Admin/Board only)")
    .AddEndpointFilter<ValidationFilter<ReactivateMembershipRequest>>()
    .Produces<ApiResponse<MembershipResponse>>()
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status409Conflict);
```

Add the handler:

```csharp
private static async Task<IResult> ReactivateMembership(
    [FromRoute] Guid familyUnitId,
    [FromRoute] Guid memberId,
    [FromBody] ReactivateMembershipRequest request,
    ClaimsPrincipal user,
    MembershipsService service,
    CancellationToken ct)
{
    var userRole = user.GetUserRole();
    if (userRole != "Admin" && userRole != "Board")
        return Results.Forbid();

    var membership = await service.ReactivateAsync(memberId, request, ct);
    return Results.Ok(ApiResponse<MembershipResponse>.Ok(membership));
}
```

---

### Step 9: Write / Update Unit Tests

**File:** `src/Abuvi.Tests/Unit/Features/Memberships/MembershipsServiceTests.cs`

Update the existing `CreateAsync_WhenFamilyMemberExists_CreatesMembership` test to also assert that `AddFeeAsync` is called once with a `Pending` fee for `request.Year`:

```csharp
await _membershipsRepository.Received(1).AddFeeAsync(
    Arg.Is<MembershipFee>(f =>
        f.Year == request.Year &&
        f.Status == FeeStatus.Pending &&
        f.Amount == 0m),
    Arg.Any<CancellationToken>());
```

Similarly update `BulkActivateAsync` tests (if they exist) or add new ones asserting fees are created per activated member.

---

**File (new):** `src/Abuvi.Tests/Unit/Features/Memberships/MembershipsServiceTests_CreateFee.cs`

```
Namespace: Abuvi.Tests.Unit.Features.Memberships
Class: MembershipsServiceTests_CreateFee
```

Test cases to implement (AAA pattern, name format: `MethodName_StateUnderTest_ExpectedBehavior`):

| Test name | Scenario | Expected outcome |
|-----------|----------|-----------------|
| `CreateFeeAsync_WhenMembershipNotFound_ThrowsNotFoundException` | Repository returns null | `NotFoundException` thrown |
| `CreateFeeAsync_WhenFeeAlreadyExistsForYear_ThrowsBusinessRuleException` | `GetFeeByYearAsync` returns a fee | `BusinessRuleException` thrown |
| `CreateFeeAsync_WhenValidRequest_CreatesFeeWithPendingStatus` | All valid | `AddFeeAsync` called once, returns `MembershipFeeResponse` with `Status=Pending` |
| `CreateFeeAsync_WhenValidRequest_SetsCorrectYearAndAmount` | Valid request with year=2024, amount=25.50 | Response has `Year=2024`, `Amount=25.50` |

---

**File (new):** `src/Abuvi.Tests/Unit/Features/Memberships/MembershipsServiceTests_Reactivate.cs`

```
Namespace: Abuvi.Tests.Unit.Features.Memberships
Class: MembershipsServiceTests_Reactivate
```

Test cases:

| Test name | Scenario | Expected outcome |
|-----------|----------|-----------------|
| `ReactivateAsync_WhenMembershipNotFound_ThrowsNotFoundException` | `GetByFamilyMemberIdIgnoringActiveAsync` returns null | `NotFoundException` |
| `ReactivateAsync_WhenMembershipAlreadyActive_ThrowsBusinessRuleException` | Membership has `IsActive=true` | `BusinessRuleException` with appropriate message |
| `ReactivateAsync_WhenMembershipInactive_SetsIsActiveTrue` | Membership has `IsActive=false` | `UpdateAsync` called with `IsActive=true`, `EndDate=null` |
| `ReactivateAsync_WhenMembershipInactive_CreatesFeeForRequestedYear` | No existing fee for year | `AddFeeAsync` called once with `Status=Pending`, `Year=request.Year` |
| `ReactivateAsync_WhenFeeAlreadyExistsForYear_SkipsFeeCreation` | `GetFeeByYearAsync` returns existing fee | `AddFeeAsync` NOT called |

---

### Step 10: Write / Update Integration Tests

**File:** `src/Abuvi.Tests/Integration/Features/Memberships/MembershipsEndpointsTests.cs`

Add the following integration test scenarios (create a Board-authenticated client as per the existing pattern in the file):

#### `POST /api/memberships/{id}/fees`

| Test | Expected HTTP |
|------|--------------|
| Board user creates fee for existing membership, valid year/amount | `201 Created` |
| Board user creates fee for non-existent membership | `404 Not Found` |
| Board user creates duplicate fee for same year | `409 Conflict` |
| Member user (non-Admin/Board) tries to create fee | `403 Forbidden` |
| Invalid year (future year) | `400 Bad Request` |
| Invalid amount (negative) | `400 Bad Request` |

#### `POST /membership/reactivate`

| Test | Expected HTTP |
|------|--------------|
| Board user reactivates an inactive membership | `200 OK` |
| Board user reactivates a non-existent membership | `404 Not Found` |
| Board user reactivates an already active membership | `409 Conflict` |
| Member user attempts reactivation | `403 Forbidden` |
| Invalid year (> current year) | `400 Bad Request` |

---

### Step 11: Update Technical Documentation

**Files to update:**

1. **`ai-specs/specs/api-endpoints.md`** — Add entries for the two new endpoints:
   - `POST /api/memberships/{membershipId}/fees`
   - `POST /api/family-units/{familyUnitId}/members/{memberId}/membership/reactivate`

2. **`ai-specs/specs/data-model.md`** — No schema changes, but update the `Membership` section to note:
   - "On membership creation, a `MembershipFee` with `Status=Pending` is auto-generated for the start year."
   - "A deactivated membership can be reactivated via the reactivate endpoint."

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Add DTOs
3. Step 2 — Add validators
4. Step 3 + 3-bis — Update repository (add `GetByFamilyMemberIdIgnoringActiveAsync` + `GetFeeByYearAsync`)
5. Step 4 — Fix `CreateAsync` (auto-create fee)
6. Step 5 — Fix `BulkActivateAsync` (auto-create fee)
7. Step 6 — Add `CreateFeeAsync`
8. Step 7 — Add `ReactivateAsync`
9. Step 8 — Register endpoints
10. Step 9 — Unit tests
11. Step 10 — Integration tests
12. Step 11 — Documentation update

---

## Testing Checklist

- [ ] Existing `MembershipsServiceTests.CreateAsync_WhenFamilyMemberExists_CreatesMembership` passes with the new `AddFeeAsync` assertion
- [ ] All new `MembershipsServiceTests_CreateFee` tests pass
- [ ] All new `MembershipsServiceTests_Reactivate` tests pass
- [ ] New integration tests for `POST /fees` pass (201, 403, 404, 409, 400)
- [ ] New integration tests for `POST /reactivate` pass (200, 403, 404, 409, 400)
- [ ] Full test suite passes: `dotnet test src/Abuvi.Tests`

---

## Error Response Format

All endpoints use the existing `ApiResponse<T>` envelope:

```json
{
  "success": false,
  "data": null,
  "error": "A fee for year 2026 already exists for this membership."
}
```

| Scenario | HTTP Code |
|----------|-----------|
| Success (create fee) | 201 Created |
| Success (reactivate) | 200 OK |
| Validation error | 400 Bad Request |
| Not Admin/Board | 403 Forbidden |
| Membership/fee not found | 404 Not Found |
| Duplicate fee / already active | 409 Conflict |

`BusinessRuleException` → 409, `NotFoundException` → 404, validation → 400.
These are mapped by the existing global exception middleware — no changes needed.

---

## Dependencies

No new NuGet packages required. No EF Core migrations required (no schema changes).

---

## Notes

- **`Amount = 0m` on auto-creation**: Intentional. The fee amount is set by the admin when calling `POST /fees/{feeId}/pay` (via `PayFeeRequest`). The `PayFee` endpoint does not update `Amount` though — if the `Amount` field needs to be non-zero for display purposes, the `PayFeeRequest` should include it or a separate update endpoint should be added (out of scope for this ticket).
- **Error messages** are in Spanish to match the existing codebase convention, **except** inside test names and code comments (English per `base-standards.mdc`).
- **`UpdateAsync` with detached entity**: `MembershipsRepository.UpdateAsync` calls `db.Memberships.Update(membership)`, which correctly attaches and marks the entity as modified when using `AsNoTracking()` — this is the existing pattern and is already used by `DeactivateAsync`.
- **`GetByFamilyMemberIdAsync` vs `GetByFamilyMemberIdIgnoringActiveAsync`**: The existing method keeps its `&& m.IsActive` filter unchanged. The new method is additive — no existing behavior is modified.

---

## Next Steps After Implementation

- Frontend ticket: Fix "sin alta de socio/a" count in `BulkActivate` modal (separate story).
- Frontend ticket: Add UI flow to create/pay annual fee for an active membership.

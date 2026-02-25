# Backend Implementation Plan: feat-bulk-family-membership

## Overview

This feature extends the membership system with two changes:

1. **`CreateMembershipRequest` breaking change**: Replace `startDate: DateTime` with `year: int`. The backend normalizes to `{year}-01-01T00:00:00Z` for storage. The `MembershipResponse` shape is unchanged.
2. **New bulk endpoint**: `POST /api/family-units/{familyUnitId}/membership/bulk` activates memberships for all family members who do not yet have one, in a single request.

Architecture principle: all changes are contained within the existing `Features/Memberships` vertical slice. The `IFamilyUnitsRepository` already exposes `GetFamilyMembersByFamilyUnitIdAsync`, so no new repository method is required.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Memberships/`

**Files to modify:**
- `MembershipsModels.cs` — change `CreateMembershipRequest`, add bulk DTOs
- `MembershipsService.cs` — update `CreateAsync`, add `BulkActivateAsync`
- `CreateMembershipValidator.cs` — validate `Year` instead of `StartDate`
- `MembershipsEndpoints.cs` — register new bulk endpoint handler

**Files to create:**
- `BulkActivateMembershipValidator.cs` — new validator for the bulk request

**Test files to modify:**
- `Unit/Features/Memberships/MembershipsServiceTests.cs`
- `Unit/Features/Memberships/CreateMembershipValidatorTests.cs`
- `Integration/Features/Memberships/MembershipsEndpointsTests.cs`

**Test files to create:**
- `Unit/Features/Memberships/BulkActivateMembershipValidatorTests.cs`

**Cross-cutting:** No changes to `Program.cs` (no new service registrations needed — `MembershipsService` is already registered). No EF Core migration required (schema is unchanged; `StartDate` stays as a `DateTime` column, always normalized to Jan 1st).

**Docs to update after implementation:**
- `ai-specs/specs/api-endpoints.md` — new bulk endpoint, updated CreateMembership contract
- `ai-specs/specs/data-model.md` — note on `StartDate` normalization

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the backend feature branch.
- **Branch name**: `feature/feat-bulk-family-membership-backend`
- **Implementation Steps**:
  1. Ensure you are on `main`: `git checkout main && git pull origin main`
  2. Create branch: `git checkout -b feature/feat-bulk-family-membership-backend`
  3. Verify: `git branch`
- **Notes**: Do NOT keep working on `feature/adding-new-specs` or any general task branch. Backend concerns must live on their own branch.

---

### Step 1: Update `MembershipsModels.cs` — Change `CreateMembershipRequest` + Add Bulk DTOs

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsModels.cs`
- **Action**: Replace the `CreateMembershipRequest` record and append the new bulk DTOs.

**Change `CreateMembershipRequest`:**
```csharp
// BEFORE
public record CreateMembershipRequest(DateTime StartDate);

// AFTER
public record CreateMembershipRequest(int Year);
```

**Add bulk DTOs at the bottom of the file (after `MembershipFeeResponse`):**
```csharp
// Bulk membership DTOs
public record BulkActivateMembershipRequest(int Year);

public enum BulkMembershipResultStatus { Activated, Skipped, Failed }

public record BulkMembershipMemberResult(
    Guid MemberId,
    string MemberName,
    BulkMembershipResultStatus Status,
    string? Reason = null
);

public record BulkActivateMembershipResponse(
    int Activated,
    int Skipped,
    IReadOnlyList<BulkMembershipMemberResult> Results
);
```

- **Implementation Notes**:
  - `BulkMembershipResultStatus` is an enum — it will be serialized as a string in the JSON response because the project uses `JsonStringEnumConverter` (verify in `Program.cs`; if not yet configured globally, add `[JsonConverter(typeof(JsonStringEnumConverter))]` to the enum).
  - No `using` changes needed; the file already imports `Abuvi.API.Features.FamilyUnits`.

---

### Step 2: Update `CreateMembershipValidator.cs`

- **File**: `src/Abuvi.API/Features/Memberships/CreateMembershipValidator.cs`
- **Action**: Replace the `StartDate` rules with `Year` rules.

```csharp
// BEFORE
public class CreateMembershipValidator : AbstractValidator<CreateMembershipRequest>
{
    public CreateMembershipValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("La fecha de inicio es obligatoria")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("La fecha de inicio no puede ser futura");
    }
}

// AFTER
public class CreateMembershipValidator : AbstractValidator<CreateMembershipRequest>
{
    public CreateMembershipValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000).WithMessage("El año de inicio no es válido")
            .LessThanOrEqualTo(DateTime.UtcNow.Year).WithMessage("El año de inicio no puede ser futuro");
    }
}
```

- **Implementation Notes**:
  - `DateTime.UtcNow.Year` is evaluated at validation time (not at startup), which is correct.
  - Remove the `NotEmpty()` — an `int` cannot be empty; `GreaterThan(2000)` already handles the lower bound.

---

### Step 3: Create `BulkActivateMembershipValidator.cs`

- **File**: `src/Abuvi.API/Features/Memberships/BulkActivateMembershipValidator.cs` (new file)
- **Action**: Create the FluentValidation validator for `BulkActivateMembershipRequest`.

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class BulkActivateMembershipValidator : AbstractValidator<BulkActivateMembershipRequest>
{
    public BulkActivateMembershipValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000).WithMessage("El año de inicio no es válido")
            .LessThanOrEqualTo(DateTime.UtcNow.Year).WithMessage("El año de inicio no puede ser futuro");
    }
}
```

- **Implementation Notes**: This mirrors `CreateMembershipValidator` exactly. If the project ever centralizes year validation into a shared rule, that is a future refactor — do not abstract now.

---

### Step 4: Update `MembershipsService.cs`

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsService.cs`
- **Action A**: Update `CreateAsync` to use `request.Year` instead of `request.StartDate`.
- **Action B**: Add the new `BulkActivateAsync` method.

**Action A — Update `CreateAsync`:**

```csharp
// BEFORE
var membership = new Membership
{
    Id = Guid.NewGuid(),
    FamilyMemberId = familyMemberId,
    StartDate = request.StartDate,
    IsActive = true,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

// AFTER
var membership = new Membership
{
    Id = Guid.NewGuid(),
    FamilyMemberId = familyMemberId,
    StartDate = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    IsActive = true,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

**Action B — Add `BulkActivateAsync` method:**

Add after `CreateAsync` (before `GetByFamilyMemberIdAsync`):

```csharp
public async Task<BulkActivateMembershipResponse> BulkActivateAsync(
    Guid familyUnitId,
    BulkActivateMembershipRequest request,
    CancellationToken ct)
{
    // Validate family unit exists
    var familyUnit = await familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, ct);
    if (familyUnit is null)
        throw new NotFoundException(nameof(FamilyUnit), familyUnitId);

    // Fetch all members of the family unit
    var members = await familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, ct);

    var results = new List<BulkMembershipMemberResult>();
    int activated = 0, skipped = 0;

    var startDate = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    foreach (var member in members)
    {
        var memberName = $"{member.FirstName} {member.LastName}";

        // Check if already has an active membership
        var existing = await repository.GetByFamilyMemberIdAsync(member.Id, ct);
        if (existing is not null)
        {
            results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Skipped, "Ya tiene membresía activa"));
            skipped++;
            continue;
        }

        try
        {
            var membership = new Membership
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = member.Id,
                StartDate = startDate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await repository.AddAsync(membership, ct);
            results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Activated));
            activated++;
        }
        catch (Exception ex)
        {
            results.Add(new(member.Id, memberName, BulkMembershipResultStatus.Failed, ex.Message));
        }
    }

    return new BulkActivateMembershipResponse(activated, skipped, results);
}
```

- **Implementation Notes**:
  - The method uses `familyUnitsRepository.GetFamilyUnitByIdAsync` (already exists on `IFamilyUnitsRepository`) to validate the family unit.
  - It uses `familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync` (already exists on `IFamilyUnitsRepository`) to list all members.
  - The per-member try/catch ensures partial failures are recorded without aborting the whole batch (Rule 3 from the spec).
  - No `FamilyUnit` import is needed — it is already imported via `using Abuvi.API.Features.FamilyUnits;` at the top of the file.

---

### Step 5: Update `MembershipsEndpoints.cs` — Register Bulk Endpoint

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs`
- **Action**: Add the bulk endpoint handler and registration inside `MapMembershipsEndpoints`.

**Add the endpoint registration** inside `MapMembershipsEndpoints`, after the existing `group` registrations. Since the bulk endpoint URL is `/api/family-units/{familyUnitId}/membership/bulk` (no `/members/{memberId}` segment), register it on a separate `MapGroup` or directly on `app`:

```csharp
// In MapMembershipsEndpoints, add AFTER the existing group definition:

var bulkGroup = app.MapGroup("/api/family-units/{familyUnitId:guid}/membership")
    .WithTags("Memberships")
    .RequireAuthorization();

bulkGroup.MapPost("/bulk", BulkActivateMemberships)
    .WithName("BulkActivateMemberships")
    .WithSummary("Bulk activate memberships for all family members without one")
    .AddEndpointFilter<ValidationFilter<BulkActivateMembershipRequest>>()
    .Produces<ApiResponse<BulkActivateMembershipResponse>>()
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound);
```

**Add the handler method** (private static, after existing handlers):

```csharp
private static async Task<IResult> BulkActivateMemberships(
    [FromRoute] Guid familyUnitId,
    [FromBody] BulkActivateMembershipRequest request,
    ClaimsPrincipal user,
    MembershipsService service,
    CancellationToken ct)
{
    // Authorization: Board/Admin only
    var userRole = user.GetUserRole();
    var isAdminOrBoard = userRole == "Admin" || userRole == "Board";
    if (!isAdminOrBoard)
        return Results.Forbid();

    var result = await service.BulkActivateAsync(familyUnitId, request, ct);
    return Results.Ok(ApiResponse<BulkActivateMembershipResponse>.Ok(result));
}
```

- **Dependencies**: Add `using System.Security.Claims;` and `using Abuvi.API.Common.Extensions;` to the file if not already present (for `GetUserRole()`).
- **Implementation Notes**:
  - The existing individual membership endpoint (`CreateMembership`) still has a `// TODO: Add authorization check` comment. **Do not change it as part of this ticket** — the enriched spec explicitly says authorization for the individual endpoint was a pre-existing TODO.
  - The bulk endpoint returns `200 OK` (not `201 Created`), because it is not creating a single resource — it is a batch operation that returns a summary.
  - `Results.Forbid()` returns `HTTP 403 Forbidden` when authorization fails.

---

### Step 6: Write Unit Tests

#### 6a. Update `CreateMembershipValidatorTests.cs`

- **File**: `src/Abuvi.Tests/Unit/Features/Memberships/CreateMembershipValidatorTests.cs`
- **Action**: Replace all existing tests (they test `StartDate`) with tests for `Year`.

Test cases:
- `ValidYear_CurrentYear_PassesValidation` — `Year = DateTime.UtcNow.Year` → valid
- `ValidYear_PastYear_PassesValidation` — `Year = 2001` → valid
- `Year2000_FailsValidation` — `Year = 2000` → fails (`GreaterThan(2000)`)
- `Year1999_FailsValidation` — `Year = 1999` → fails
- `FutureYear_FailsValidation` — `Year = DateTime.UtcNow.Year + 1` → fails

#### 6b. Create `BulkActivateMembershipValidatorTests.cs`

- **File**: `src/Abuvi.Tests/Unit/Features/Memberships/BulkActivateMembershipValidatorTests.cs` (new)
- **Action**: Mirror the same test cases as `CreateMembershipValidatorTests` but for `BulkActivateMembershipRequest`.

Test cases:
- `ValidYear_CurrentYear_PassesValidation`
- `Year2000_FailsValidation`
- `FutureYear_FailsValidation`

#### 6c. Update `MembershipsServiceTests.cs`

- **File**: `src/Abuvi.Tests/Unit/Features/Memberships/MembershipsServiceTests.cs`
- **Action**: Update the existing `CreateAsync` tests to use `new CreateMembershipRequest(DateTime.UtcNow.Year)` (sending `Year` int, not `DateTime`). Add new tests for `BulkActivateAsync`.

**Update existing tests** — change all occurrences of `new CreateMembershipRequest(DateTime.UtcNow...)` to `new CreateMembershipRequest(DateTime.UtcNow.Year)`.

Also update the assertion in `CreateAsync_WhenFamilyMemberExists_CreatesMembership`:
```csharp
// BEFORE
result.StartDate.Should().BeCloseTo(request.StartDate, TimeSpan.FromSeconds(1));

// AFTER — verify normalization to Jan 1st
result.StartDate.Should().Be(new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
```

**New test cases for `BulkActivateAsync`:**

```
BulkActivateAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException
BulkActivateAsync_WhenFamilyHasNoMembers_ReturnsZeroActivated
BulkActivateAsync_WhenAllMembersHaveNoMembership_ActivatesAll
BulkActivateAsync_WhenSomeMembersAlreadyHaveMembership_SkipsThose
BulkActivateAsync_WhenMemberHasMembership_IncludesSkippedInResults
```

**Test pattern for bulk (follow AAA and project NSubstitute patterns):**

```csharp
[Fact]
public async Task BulkActivateAsync_WhenAllMembersHaveNoMembership_ActivatesAll()
{
    // Arrange
    var familyUnitId = Guid.NewGuid();
    var familyUnit = CreateTestFamilyUnit(familyUnitId);
    var members = new[]
    {
        CreateTestFamilyMember(Guid.NewGuid(), familyUnitId),
        CreateTestFamilyMember(Guid.NewGuid(), familyUnitId),
    };
    var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

    _familyUnitsRepository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);
    _familyUnitsRepository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, Arg.Any<CancellationToken>())
        .Returns(members.AsReadOnly() as IReadOnlyList<FamilyMember>);
    _membershipsRepository.GetByFamilyMemberIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
        .Returns((Membership?)null);

    // Act
    var result = await _service.BulkActivateAsync(familyUnitId, request, CancellationToken.None);

    // Assert
    result.Activated.Should().Be(2);
    result.Skipped.Should().Be(0);
    result.Results.Should().HaveCount(2);
    result.Results.Should().AllSatisfy(r => r.Status.Should().Be(BulkMembershipResultStatus.Activated));
    await _membershipsRepository.Received(2).AddAsync(Arg.Any<Membership>(), Arg.Any<CancellationToken>());
}
```

Add `CreateTestFamilyUnit` helper:
```csharp
private static FamilyUnit CreateTestFamilyUnit(Guid id) => new()
{
    Id = id,
    Name = "Test Family",
    RepresentativeUserId = Guid.NewGuid(),
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

Update `CreateTestFamilyMember` to accept `familyUnitId` parameter (overload):
```csharp
private static FamilyMember CreateTestFamilyMember(Guid id, Guid familyUnitId) => new()
{
    Id = id,
    FamilyUnitId = familyUnitId,
    FirstName = "John",
    LastName = "Doe",
    DateOfBirth = new DateOnly(1990, 1, 1),
    Relationship = FamilyRelationship.Parent,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

---

### Step 7: Write Integration Tests

#### 7a. Update `MembershipsEndpointsTests.cs`

- **File**: `src/Abuvi.Tests/Integration/Features/Memberships/MembershipsEndpointsTests.cs`
- **Action**: Update existing tests + add new tests for the bulk endpoint and the year-based create.

**Update existing tests** — change `new CreateMembershipRequest(DateTime.UtcNow.AddDays(-1))` to `new CreateMembershipRequest(DateTime.UtcNow.Year)` (or a past year like `DateTime.UtcNow.Year - 1`). Also update the `CreateMembership_WithFutureStartDate_Returns400BadRequest` test to send `new CreateMembershipRequest(DateTime.UtcNow.Year + 1)`.

**Update assertions** that previously checked `result.Data.StartDate.Should().BeCloseTo(request.StartDate, ...)` to:
```csharp
result.Data!.StartDate.Should().Be(new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
```

**New integration tests to add:**

```
POST_CreateMembership_WithValidYear_Returns201AndStartDateIsJanFirst
POST_CreateMembership_FutureYear_Returns400
POST_BulkActivateMemberships_WithValidYear_Returns200WithActivatedCount
POST_BulkActivateMemberships_AllMembersAlreadyHaveMembership_Returns200WithZeroActivated
POST_BulkActivateMemberships_FutureYear_Returns400
POST_BulkActivateMemberships_FamilyNotFound_Returns404
POST_BulkActivateMemberships_WithoutBoardRole_Returns403Forbidden
```

**Key integration test pattern for bulk:**

```csharp
[Fact]
public async Task POST_BulkActivateMemberships_WithValidYear_Returns200WithActivatedCount()
{
    // Arrange — seed a family unit with 2 members, neither with membership
    await GetAuthTokenAsync(); // authenticates as a regular user — see note below
    var (familyUnit, members) = await SeedFamilyWithMembersAsync(memberCount: 2);
    var request = new BulkActivateMembershipRequest(DateTime.UtcNow.Year);

    // Act
    var response = await _authenticatedClient.PostAsJsonAsync(
        $"/api/family-units/{familyUnit.Id}/membership/bulk",
        request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<ApiResponse<BulkActivateMembershipResponse>>();
    result!.Data!.Activated.Should().Be(2);
    result.Data.Skipped.Should().Be(0);
}
```

**Authorization test note**: The existing `GetAuthTokenAsync` registers a `Member` role user. For the `403 Forbidden` test, use the existing token (non-board user). For tests that must succeed, you need a Board/Admin token — add a `GetBoardAuthTokenAsync` helper that seeds a user with `UserRole.Board`.

---

### Step 8: Update Technical Documentation

- **Action**: Update API and data model docs to reflect the changes.
- **Files**:

**`ai-specs/specs/api-endpoints.md`** — update the `POST /api/family-units/{id}/members/{id}/membership` entry:
  - Change request body from `{ startDate: string }` to `{ year: integer }`.
  - Add the new `POST /api/family-units/{familyUnitId}/membership/bulk` endpoint with request/response schema.

**`ai-specs/specs/data-model.md`** — add a note under `Membership.StartDate`:
  - Document that `StartDate` is always normalized to `{year}-01-01T00:00:00Z` as of this feature; the frontend sends only a year integer.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-bulk-family-membership-backend`
2. **Step 1** — Update `MembershipsModels.cs` (change `CreateMembershipRequest`, add bulk DTOs)
3. **Step 2** — Update `CreateMembershipValidator.cs` (Year rules)
4. **Step 3** — Create `BulkActivateMembershipValidator.cs`
5. **Step 4** — Update `MembershipsService.cs` (update `CreateAsync`, add `BulkActivateAsync`)
6. **Step 5** — Update `MembershipsEndpoints.cs` (register bulk endpoint and handler)
7. **Step 6** — Write unit tests (validator + service)
8. **Step 7** — Write integration tests (endpoints)
9. **Step 8** — Update technical documentation

---

## Testing Checklist

### Unit Tests

| Test Class | Test | Expected |
|---|---|---|
| `CreateMembershipValidatorTests` | `ValidYear_CurrentYear_PassesValidation` | Valid |
| `CreateMembershipValidatorTests` | `Year2000_FailsValidation` | Invalid (must be `> 2000`) |
| `CreateMembershipValidatorTests` | `FutureYear_FailsValidation` | Invalid |
| `BulkActivateMembershipValidatorTests` | `ValidYear_CurrentYear_PassesValidation` | Valid |
| `BulkActivateMembershipValidatorTests` | `Year2000_FailsValidation` | Invalid |
| `BulkActivateMembershipValidatorTests` | `FutureYear_FailsValidation` | Invalid |
| `MembershipsServiceTests` | `CreateAsync_WhenValidYear_SetsStartDateToJanFirst` | `StartDate == {year}-01-01` |
| `MembershipsServiceTests` | `BulkActivateAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException` | throws |
| `MembershipsServiceTests` | `BulkActivateAsync_WhenFamilyHasNoMembers_ReturnsZeroActivated` | `Activated == 0` |
| `MembershipsServiceTests` | `BulkActivateAsync_WhenAllMembersHaveNoMembership_ActivatesAll` | `Activated == N` |
| `MembershipsServiceTests` | `BulkActivateAsync_WhenSomeMembersAlreadyHaveMembership_SkipsThose` | `Skipped == K` |

### Integration Tests

| Test | HTTP | Expected |
|---|---|---|
| `POST_CreateMembership_WithValidYear_Returns201AndStartDateIsJanFirst` | `POST .../membership` `{year: 2025}` | 201, `startDate = 2025-01-01` |
| `POST_CreateMembership_FutureYear_Returns400` | `POST .../membership` `{year: next+1}` | 400 |
| `POST_BulkActivateMemberships_WithValidYear_Returns200WithActivatedCount` | `POST .../membership/bulk` `{year: 2025}` | 200, `activated > 0` |
| `POST_BulkActivateMemberships_AllMembersAlreadyHaveMembership_Returns200WithZeroActivated` | board user, all pre-seeded | 200, `activated == 0` |
| `POST_BulkActivateMemberships_FutureYear_Returns400` | `{year: next+1}` | 400 |
| `POST_BulkActivateMemberships_FamilyNotFound_Returns404` | random `familyUnitId` | 404 |
| `POST_BulkActivateMemberships_WithoutBoardRole_Returns403Forbidden` | regular Member token | 403 |

---

## Error Response Format

All error responses follow the existing `ApiResponse<T>` envelope:

```json
// 400 Bad Request (FluentValidation)
{
  "success": false,
  "error": {
    "message": "El año de inicio no puede ser futuro",
    "details": { "year": ["El año de inicio no puede ser futuro"] }
  }
}

// 403 Forbidden (authorization)
// No body (Results.Forbid())

// 404 Not Found (family unit not found)
{
  "success": false,
  "error": {
    "message": "FamilyUnit '...' not found"
  }
}

// 200 OK (bulk success)
{
  "success": true,
  "data": {
    "activated": 3,
    "skipped": 1,
    "results": [
      { "memberId": "...", "memberName": "Ana García", "status": "Activated" },
      { "memberId": "...", "memberName": "María García", "status": "Skipped", "reason": "Ya tiene membresía activa" }
    ]
  }
}
```

---

## Dependencies

- No new NuGet packages.
- No EF Core migration (`StartDate` schema is unchanged — always `DateTime`, now always normalized to Jan 1st by the service layer).
- No `Program.cs` changes (no new services to register — `MembershipsService` is already `AddScoped`).

---

## Notes

1. **Breaking change is safe**: The `CreateMembershipRequest` change (`{ startDate }` → `{ year }`) is safe because the individual membership endpoint was never wired to any UI before `feat-my-memberships-dialog` (implemented on the same day). Ensure `feat-my-memberships-dialog` is merged to `main` **before** or **alongside** this backend change.

2. **No repository changes**: `IFamilyUnitsRepository` already has `GetFamilyMembersByFamilyUnitIdAsync` — no interface or implementation changes needed there.

3. **`BulkMembershipResultStatus` enum serialization**: Verify the project serializes enums as strings (check `Program.cs` for `JsonStringEnumConverter`). The spec response shows `"status": "Activated"` (string). If global string enum serialization is not configured, add the `[JsonConverter(typeof(JsonStringEnumConverter))]` attribute to `BulkMembershipResultStatus`.

4. **Authorization for bulk endpoint**: Board/Admin only (`userRole == "Admin" || userRole == "Board"`). Unlike the individual endpoint (which has a `// TODO` comment for auth), the bulk endpoint must implement the authorization check immediately.

5. **Partial failure handling**: Per Rule 3 in the spec, the service uses a per-member try/catch. The endpoint always returns `200 OK` (not `500`). A `Failed` status in results signals a per-member error.

6. **Test naming**: Follow `MethodName_StateUnderTest_ExpectedBehavior` as per `backend-standards.mdc`.

7. **Language**: Business messages in Spanish (error messages like `"Ya tiene membresía activa"`, `"El año de inicio no es válido"`). Code and documentation in English.

---

## Next Steps After Implementation

- Coordinate with frontend ticket `feat-bulk-family-membership-frontend` — the frontend must update `CreateMembershipRequest` type to `{ year: number }` simultaneously.
- After merging, run the full test suite: `dotnet test src/Abuvi.Tests/`.
- Verify Swagger/OpenAPI docs reflect the new endpoint at `/api/family-units/{familyUnitId}/membership/bulk`.

---

## Implementation Verification

- [ ] **Code Quality**: C# analyzers pass, nullable reference types handled (`string? Reason`)
- [ ] **No N+1**: `BulkActivateAsync` fetches all members in one call; checks membership per member (acceptable — small family sizes)
- [ ] **Functionality**: `POST .../membership` with `{ year: 2025 }` returns 201 with `startDate = "2025-01-01T00:00:00Z"`
- [ ] **Functionality**: `POST .../membership/bulk` with `{ year: 2025 }` returns 200 with `activated`, `skipped`, `results[]`
- [ ] **Validation**: Future year rejected with 400 on both endpoints
- [ ] **Authorization**: Non-board user gets 403 on bulk endpoint
- [ ] **Testing**: All unit + integration tests pass (`dotnet test`)
- [ ] **No migration**: `dotnet ef migrations list` shows no pending migrations
- [ ] **Documentation**: `api-endpoints.md` and `data-model.md` updated

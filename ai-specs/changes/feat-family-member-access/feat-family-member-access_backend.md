# Backend Implementation Plan: feat-family-member-access — Family Member Access to Family Unit and Registrations

## Overview

A user linked as a `FamilyMember` (via `FamilyMember.UserId`) cannot currently see their family unit or the family's camp registrations. The backend enforces authorization only based on `RepresentativeUserId`, ignoring linked members. This plan fixes that by introducing a second authorization role ("linked family member") that grants **read-only** access to family unit data and registration read endpoints.

No schema changes are required. This is a pure service/repository/endpoint authorization fix.

---

## Architecture Context

**Feature slices affected:**
- `src/Abuvi.API/Features/FamilyUnits/` — repository, service, endpoints
- `src/Abuvi.API/Features/Registrations/` — service only (endpoint authorization already delegates to service)

**Files to modify (no new files):**

| File | Change type |
|---|---|
| `FamilyUnitsRepository.cs` | Add new repository method |
| `FamilyUnitsService.cs` | Update 1 existing method, add 1 new helper |
| `FamilyUnitsEndpoints.cs` | Update authorization in 3 read handlers |
| `RegistrationsService.cs` | Update 2 existing methods |
| `FamilyUnitsServiceTests.cs` | Add 5 unit tests |
| `FamilyUnitsUserLinkingTests.cs` | Add 7 integration tests |

**No migrations needed** — no entity or schema changes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Base branch**: `dev`
- **Branch name**: `feature/feat-family-member-access-backend`

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-family-member-access-backend
```

---

### Step 1: Add `GetFamilyUnitByMemberUserIdAsync` to Repository

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`

**Why the subquery approach**: `FamilyUnit` entity has no `FamilyMembers` navigation property (confirmed in `FamilyUnitsModels.cs` and `FamilyUnitConfiguration.cs`). `FamilyMemberConfiguration` uses `HasOne<FamilyUnit>().WithMany()` — no inverse collection is configured. The repository already accesses `db.FamilyMembers` directly as a DbSet.

**1a. Add to `IFamilyUnitsRepository` interface** (around line 14, after `GetFamilyUnitByRepresentativeIdAsync`):

```csharp
Task<FamilyUnit?> GetFamilyUnitByMemberUserIdAsync(Guid userId, CancellationToken ct);
```

**1b. Add implementation** (in `FamilyUnitsRepository` class, after `GetFamilyUnitByRepresentativeIdAsync`):

```csharp
public async Task<FamilyUnit?> GetFamilyUnitByMemberUserIdAsync(Guid userId, CancellationToken ct)
    => await db.FamilyUnits
        .AsNoTracking()
        .Where(fu => db.FamilyMembers.Any(fm => fm.FamilyUnitId == fu.Id && fm.UserId == userId))
        .FirstOrDefaultAsync(ct);
```

This translates to a single SQL `EXISTS` subquery — no N+1, no navigation property needed.

---

### Step 2: Update `FamilyUnitsService`

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

**2a. Update `GetCurrentUserFamilyUnitAsync`** (lines 91–97):

Replace:
```csharp
var familyUnit = await repository.GetFamilyUnitByRepresentativeIdAsync(userId, ct)
    ?? throw new NotFoundException("No se encontró unidad familiar para el usuario actual");
```

With:
```csharp
var familyUnit = await repository.GetFamilyUnitByRepresentativeIdAsync(userId, ct)
              ?? await repository.GetFamilyUnitByMemberUserIdAsync(userId, ct)
              ?? throw new NotFoundException("No se encontró unidad familiar para el usuario actual");
```

**2b. Add `IsFamilyMemberOfUnitAsync` helper** in the `#region Authorization Helpers` block, after `IsRepresentativeAsync`:

```csharp
/// <summary>
/// Checks if the user is a linked family member (not necessarily representative) of the family unit
/// </summary>
public async Task<bool> IsFamilyMemberOfUnitAsync(Guid familyUnitId, Guid userId, CancellationToken ct)
{
    var familyUnit = await repository.GetFamilyUnitByMemberUserIdAsync(userId, ct);
    return familyUnit?.Id == familyUnitId;
}
```

---

### Step 3: Update Authorization in `FamilyUnitsEndpoints` Read Handlers

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`

Three handlers need updating. Only the **authorization check** changes — the rest of each handler is untouched.

**3a. `GetFamilyUnitById`** (around line 221):

Replace:
```csharp
var isRepresentative = result.RepresentativeUserId == userId;
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

if (!isRepresentative && !isAdminOrBoard)
{
    return TypedResults.Forbid();
}
```

With:
```csharp
var isRepresentative = result.RepresentativeUserId == userId;
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";
var isFamilyMember = !isRepresentative && !isAdminOrBoard
    && await service.IsFamilyMemberOfUnitAsync(id, userId, ct);

if (!isRepresentative && !isFamilyMember && !isAdminOrBoard)
    return TypedResults.Forbid();
```

**3b. `GetFamilyMembers`** (around line 338):

Replace:
```csharp
var isRepresentative = familyUnit.RepresentativeUserId == userId;
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

if (!isRepresentative && !isAdminOrBoard)
{
    return TypedResults.Forbid();
}
```

With:
```csharp
var isRepresentative = familyUnit.RepresentativeUserId == userId;
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";
var isFamilyMember = !isRepresentative && !isAdminOrBoard
    && await service.IsFamilyMemberOfUnitAsync(familyUnitId, userId, ct);

if (!isRepresentative && !isFamilyMember && !isAdminOrBoard)
    return TypedResults.Forbid();
```

**3c. `GetFamilyMemberById`** (around line 369):

Same pattern as 3b, using `familyUnitId`:
```csharp
var isRepresentative = familyUnit.RepresentativeUserId == userId;
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";
var isFamilyMember = !isRepresentative && !isAdminOrBoard
    && await service.IsFamilyMemberOfUnitAsync(familyUnitId, userId, ct);

if (!isRepresentative && !isFamilyMember && !isAdminOrBoard)
    return TypedResults.Forbid();
```

> **Note**: `IsFamilyMemberOfUnitAsync` is only called when the user is neither representative nor admin, short-circuiting via `&&` to avoid the extra DB query in the common case.

---

### Step 4: Update `RegistrationsService`

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

**4a. `GetByFamilyUnitAsync`** (line 546):

Replace:
```csharp
var familyUnit = await familyUnitsRepo.GetFamilyUnitByRepresentativeIdAsync(userId, ct);
```

With:
```csharp
var familyUnit = await familyUnitsRepo.GetFamilyUnitByRepresentativeIdAsync(userId, ct)
              ?? await familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(userId, ct);
```

The `if (familyUnit is null) return [];` guard on the next line already handles the case where neither query finds a family unit.

**4b. `GetByIdAsync`** (lines 528–542):

Replace:
```csharp
if (!isAdminOrBoard && registration.FamilyUnit.RepresentativeUserId != userId)
    throw new BusinessRuleException("No tienes permiso para ver esta inscripción");
```

With:
```csharp
if (!isAdminOrBoard)
{
    var isRepresentative = registration.FamilyUnit.RepresentativeUserId == userId;
    if (!isRepresentative)
    {
        var memberUnit = await familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(userId, ct);
        if (memberUnit?.Id != registration.FamilyUnitId)
            throw new BusinessRuleException("No tienes permiso para ver esta inscripción");
    }
}
```

---

### Step 5: Write Unit Tests

**File**: `src/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsServiceTests.cs`

Add 5 tests in the `#region Authorization Helper Tests` block. The existing test constructor already sets up `_repository` as `Substitute.For<IFamilyUnitsRepository>()`.

**Test 1**: `GetCurrentUserFamilyUnitAsync_WhenUserIsMemberButNotRepresentative_ReturnsFamilyUnit`
```csharp
[Fact]
public async Task GetCurrentUserFamilyUnitAsync_WhenUserIsMemberButNotRepresentative_ReturnsFamilyUnit()
{
    // Arrange
    var userId = Guid.NewGuid();
    var familyUnitId = Guid.NewGuid();
    var familyUnit = new FamilyUnit
    {
        Id = familyUnitId,
        Name = "Test Family",
        RepresentativeUserId = Guid.NewGuid(), // Different user is rep
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    _repository.GetFamilyUnitByRepresentativeIdAsync(userId, Arg.Any<CancellationToken>())
        .ReturnsNull();
    _repository.GetFamilyUnitByMemberUserIdAsync(userId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);

    // Act
    var result = await _sut.GetCurrentUserFamilyUnitAsync(userId, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().Be(familyUnitId);
}
```

**Test 2**: `GetCurrentUserFamilyUnitAsync_WhenUserIsNeitherRepresentativeNorMember_ThrowsNotFoundException`
```csharp
[Fact]
public async Task GetCurrentUserFamilyUnitAsync_WhenUserIsNeitherRepresentativeNorMember_ThrowsNotFoundException()
{
    // Arrange
    var userId = Guid.NewGuid();

    _repository.GetFamilyUnitByRepresentativeIdAsync(userId, Arg.Any<CancellationToken>())
        .ReturnsNull();
    _repository.GetFamilyUnitByMemberUserIdAsync(userId, Arg.Any<CancellationToken>())
        .ReturnsNull();

    // Act
    var act = async () => await _sut.GetCurrentUserFamilyUnitAsync(userId, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<NotFoundException>()
        .WithMessage("No se encontró unidad familiar para el usuario actual");
}
```

**Test 3**: `IsFamilyMemberOfUnitAsync_WhenUserIsLinkedMemberOfUnit_ReturnsTrue`
```csharp
[Fact]
public async Task IsFamilyMemberOfUnitAsync_WhenUserIsLinkedMemberOfUnit_ReturnsTrue()
{
    // Arrange
    var userId = Guid.NewGuid();
    var familyUnitId = Guid.NewGuid();
    var familyUnit = new FamilyUnit
    {
        Id = familyUnitId,
        Name = "Test Family",
        RepresentativeUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    _repository.GetFamilyUnitByMemberUserIdAsync(userId, Arg.Any<CancellationToken>())
        .Returns(familyUnit);

    // Act
    var result = await _sut.IsFamilyMemberOfUnitAsync(familyUnitId, userId, CancellationToken.None);

    // Assert
    result.Should().BeTrue();
}
```

**Test 4**: `IsFamilyMemberOfUnitAsync_WhenUserIsMemberOfDifferentUnit_ReturnsFalse`
```csharp
[Fact]
public async Task IsFamilyMemberOfUnitAsync_WhenUserIsMemberOfDifferentUnit_ReturnsFalse()
{
    // Arrange
    var userId = Guid.NewGuid();
    var familyUnitId = Guid.NewGuid();
    var otherFamilyUnit = new FamilyUnit
    {
        Id = Guid.NewGuid(), // Different ID
        Name = "Other Family",
        RepresentativeUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    _repository.GetFamilyUnitByMemberUserIdAsync(userId, Arg.Any<CancellationToken>())
        .Returns(otherFamilyUnit);

    // Act
    var result = await _sut.IsFamilyMemberOfUnitAsync(familyUnitId, userId, CancellationToken.None);

    // Assert
    result.Should().BeFalse();
}
```

**Test 5**: `IsFamilyMemberOfUnitAsync_WhenUserIsNotAMember_ReturnsFalse`
```csharp
[Fact]
public async Task IsFamilyMemberOfUnitAsync_WhenUserIsNotAMember_ReturnsFalse()
{
    // Arrange
    var userId = Guid.NewGuid();
    var familyUnitId = Guid.NewGuid();

    _repository.GetFamilyUnitByMemberUserIdAsync(userId, Arg.Any<CancellationToken>())
        .ReturnsNull();

    // Act
    var result = await _sut.IsFamilyMemberOfUnitAsync(familyUnitId, userId, CancellationToken.None);

    // Assert
    result.Should().BeFalse();
}
```

---

### Step 6: Write Integration Tests

**File**: `src/Abuvi.Tests/Integration/Features/FamilyUnits/FamilyUnitsUserLinkingTests.cs`

Append to the existing `FamilyUnitsUserLinkingTests` class. The class already has `CreateAuthenticatedUserAsync()` helper. These tests follow the same pattern as the existing 3 tests.

**Test setup pattern** (reuse across tests):
```
1. User1 creates a family unit
2. User1 adds User2 as a family member using User2's email
3. Verify User2 (authenticated) can/cannot access the resource
```

**7 tests to add:**

```
LinkedMember_CanGetFamilyUnit_ViaMe
  → User2 GET /api/family-units/me → 200, Id matches family unit

LinkedMember_CanGetFamilyUnit_ById
  → User2 GET /api/family-units/{id} → 200

LinkedMember_CanGetFamilyMembers
  → User2 GET /api/family-units/{id}/members → 200, non-empty list

LinkedMember_CannotUpdateFamilyUnit
  → User2 PUT /api/family-units/{id} with { Name = "Hack" } → 403

LinkedMember_CannotAddFamilyMember
  → User2 POST /api/family-units/{id}/members with valid body → 403

LinkedMember_CanSeeRegistrations_ViaMyRegistrations
  → User1 registers the family for a camp edition
  → User2 GET /api/registrations → 200, list contains 1 registration

LinkedMember_CanSeeRegistrationDetail
  → (continuing above) User2 GET /api/registrations/{registrationId} → 200
```

> **Note for `CanSeeRegistrations_*` tests**: Creating a camp registration requires an open `CampEdition`. Look at how existing registration integration tests (e.g., `MembershipsEndpointsTests.cs` or similar) seed camp data, or skip this test if seeding is too complex and mark it `[Fact(Skip = "Requires camp edition seed")]` initially. The family unit access tests are higher priority.

---

### Step 7: Run Tests and Verify

```bash
cd src
dotnet test Abuvi.Tests/Abuvi.Tests.csproj --filter "FamilyUnits" --no-build
dotnet test Abuvi.Tests/Abuvi.Tests.csproj --no-build
```

All existing tests must continue to pass.

---

### Step 8: Update Documentation

**File**: `ai-specs/specs/api-spec.yml` (if it exists and is manually maintained)

Update access annotations for:
- `GET /api/family-units/me` — add: also accessible by linked family members
- `GET /api/family-units/{id}` — add: also accessible by linked family members
- `GET /api/family-units/{id}/members` — add: also accessible by linked family members
- `GET /api/registrations` — add: also accessible by linked family members
- `GET /api/registrations/{id}` — add: also accessible by linked family members

**File**: `ai-specs/changes/feat-family-member-access_enriched.md` — update status to "COMPLETE" after implementation.

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Add `GetFamilyUnitByMemberUserIdAsync` to repository (interface + implementation)
3. Step 2 — Update `FamilyUnitsService` (update `GetCurrentUserFamilyUnitAsync` + add `IsFamilyMemberOfUnitAsync`)
4. Step 3 — Update 3 read handlers in `FamilyUnitsEndpoints`
5. Step 4 — Update 2 methods in `RegistrationsService`
6. Step 5 — Write 5 unit tests
7. Step 6 — Write 7 integration tests
8. Step 7 — Run tests, verify no regressions
9. Step 8 — Update documentation

---

## Testing Checklist

- [ ] `FamilyUnitsServiceTests` — 5 new tests pass
- [ ] `FamilyUnitsUserLinkingTests` — 7 new tests pass
- [ ] All existing `FamilyUnits*` unit tests still pass
- [ ] All existing `FamilyUnitsUserLinkingTests` still pass
- [ ] All existing `RegistrationsService*` unit tests still pass
- [ ] Full test suite passes (no regressions)

---

## Error Response Format

No new error responses. Existing responses apply:

| Scenario | HTTP Status | Error code |
|---|---|---|
| Not representative, not member, not admin/board on read | 403 | (Forbid, no body) |
| Not representative on write | 403 | (Forbid, no body) |
| Family unit not found | 404 | `ApiResponse.NotFound` |
| Registration not found | 404 | `ApiResponse.NotFound` |
| Registration not owned | 403 via `BusinessRuleException` | (Forbid) |

---

## Dependencies

- No new NuGet packages
- No EF Core migrations (no schema changes)

---

## Notes

- **Short-circuit optimization**: `IsFamilyMemberOfUnitAsync` is only called via `&& await ...` when the user is neither representative nor admin/board, avoiding an extra DB query for the common case.
- **Single responsibility**: `GetFamilyUnitByMemberUserIdAsync` uses an `EXISTS` subquery on `FamilyMembers` — no navigation property needed on `FamilyUnit` entity.
- **Write operations unchanged**: `CreateFamilyMember`, `UpdateFamilyMember`, `DeleteFamilyMember`, `UpdateFamilyUnit`, `DeleteFamilyUnit`, and all registration write ops (cancel, extras, accommodation, delete) remain representative-only. No changes needed.
- **`RegistrationsService` only needs `familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync`** — this method is already available via the injected `IFamilyUnitsRepository` dependency.

---

## Next Steps After Implementation

- Frontend fix: update `isViewingOther` in `FamilyUnitPage.vue` (separate frontend ticket/branch)
- QA: manually test with two real user accounts to verify the full flow

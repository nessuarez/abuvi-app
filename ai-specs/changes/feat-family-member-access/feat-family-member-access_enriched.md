# Family Member Access: View Family Unit and Registrations

**Task ID**: feat-family-member-access
**Created**: 2026-03-10
**Status**: Ready to implement

---

## Problem

A user (User A, representative) registers their family and links their wife (User B) as a family member via User B's email. When User B later logs in:

1. **She cannot see her family unit** — `GET /api/family-units/me` returns 404 because it only looks up units where the user is the `RepresentativeUserId`, not where they are a linked `FamilyMember`.
2. **She cannot see the family's registrations** — `GET /api/registrations` returns an empty list for the same reason.
3. **She cannot see the registration detail** — `GET /api/registrations/{id}` returns 403.
4. **If she somehow gets the family unit ID**, `GET /api/family-units/{id}` also returns 403.

Additionally, the frontend incorrectly shows edit buttons to non-representative members who access `/mi-familia`.

---

## Root Cause

### Backend

| Location | Current behavior | Problem |
|---|---|---|
| `FamilyUnitsService.GetCurrentUserFamilyUnitAsync` | Calls `GetFamilyUnitByRepresentativeIdAsync(userId)` | Returns null/404 for non-representatives |
| `FamilyUnitsEndpoints.GetFamilyUnitById` | Checks `result.RepresentativeUserId == userId` | 403 for non-representatives |
| `FamilyUnitsEndpoints.GetFamilyMembers` | Same check | 403 |
| `FamilyUnitsEndpoints.GetFamilyMemberById` | Same check | 403 |
| `RegistrationsService.GetByFamilyUnitAsync` | Uses `GetFamilyUnitByRepresentativeIdAsync` | Returns empty list |
| `RegistrationsService.GetByIdAsync` | Checks `RepresentativeUserId != userId` | 403 |

### Frontend

`isViewingOther` in `FamilyUnitPage.vue` is computed as:
```ts
!!route.params.id && familyUnit.value?.representativeUserId !== auth.user?.id
```
When a non-representative accesses `/mi-familia` (no `route.params.id`), `isViewingOther` is `false` → edit buttons appear incorrectly.

---

## Access Rules (After Fix)

| Action | Representative | Linked Family Member | Admin/Board |
|---|:---:|:---:|:---:|
| `GET /api/family-units/me` | ✅ | ✅ | — |
| `GET /api/family-units/{id}` | ✅ | ✅ | ✅ |
| `GET /api/family-units/{id}/members` | ✅ | ✅ | ✅ |
| `GET /api/family-units/{id}/members/{memberId}` | ✅ | ✅ | ✅ |
| `PUT /api/family-units/{id}` | ✅ | ❌ | ❌ |
| `DELETE /api/family-units/{id}` | ✅ | ❌ | ❌ |
| `POST /api/family-units/{id}/members` | ✅ | ❌ | ❌ |
| `PUT /api/family-units/{id}/members/{memberId}` | ✅ | ❌ | ❌ |
| `DELETE /api/family-units/{id}/members/{memberId}` | ✅ | ❌ | ❌ |
| `GET /api/registrations` | ✅ | ✅ | — |
| `GET /api/registrations/{id}` | ✅ | ✅ | ✅ |
| `POST /api/registrations` | ✅ | ❌ | ❌ |
| `PUT /api/registrations/{id}/members` | ✅ | ❌ | ❌ |

Write operations on registrations (cancel, extras, accommodation, delete) remain representative-only (no change needed — current backend already enforces this).

A "linked family member" is defined as a user whose `Id` matches a `FamilyMember.UserId` in that family unit.

---

## Backend Changes

### 1. New repository method: `GetFamilyUnitByMemberUserIdAsync`

**File**: [FamilyUnitsRepository.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs)

Add to `IFamilyUnitsRepository` interface:
```csharp
Task<FamilyUnit?> GetFamilyUnitByMemberUserIdAsync(Guid userId, CancellationToken ct);
```

Add implementation:
```csharp
public async Task<FamilyUnit?> GetFamilyUnitByMemberUserIdAsync(Guid userId, CancellationToken ct)
    => await db.FamilyUnits
        .AsNoTracking()
        .FirstOrDefaultAsync(fu => fu.FamilyMembers.Any(fm => fm.UserId == userId), ct);
```

This requires EF Core navigation property `FamilyUnit.FamilyMembers` to be present. Verify in `FamilyUnit` entity and `AppDbContext` configuration.

---

### 2. Update `FamilyUnitsService.GetCurrentUserFamilyUnitAsync`

**File**: [FamilyUnitsService.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs#L91-L97)

```csharp
public async Task<FamilyUnitResponse> GetCurrentUserFamilyUnitAsync(Guid userId, CancellationToken ct)
{
    var familyUnit = await repository.GetFamilyUnitByRepresentativeIdAsync(userId, ct)
                  ?? await repository.GetFamilyUnitByMemberUserIdAsync(userId, ct)
                  ?? throw new NotFoundException("No se encontró unidad familiar para el usuario actual");

    return familyUnit.ToResponse();
}
```

---

### 3. Add `IsFamilyMemberOfUnitAsync` to `FamilyUnitsService`

**File**: [FamilyUnitsService.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs) — Authorization Helpers region

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

### 4. Update authorization in `FamilyUnitsEndpoints` read handlers

**File**: [FamilyUnitsEndpoints.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs)

Three handlers need updating: `GetFamilyUnitById`, `GetFamilyMembers`, `GetFamilyMemberById`.

Replace the `isRepresentative || isAdminOrBoard` check with `isRepresentative || isFamilyMember || isAdminOrBoard`.

**`GetFamilyUnitById`** (around line 218):
```csharp
var isRepresentative = result.RepresentativeUserId == userId;
var isFamilyMember = !isRepresentative && await service.IsFamilyMemberOfUnitAsync(id, userId, ct);
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

if (!isRepresentative && !isFamilyMember && !isAdminOrBoard)
    return TypedResults.Forbid();
```

**`GetFamilyMembers`** (around line 338):
```csharp
var isRepresentative = familyUnit.RepresentativeUserId == userId;
var isFamilyMember = !isRepresentative && await service.IsFamilyMemberOfUnitAsync(familyUnitId, userId, ct);
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

if (!isRepresentative && !isFamilyMember && !isAdminOrBoard)
    return TypedResults.Forbid();
```

**`GetFamilyMemberById`** (around line 369):
```csharp
var isRepresentative = familyUnit.RepresentativeUserId == userId;
var isFamilyMember = !isRepresentative && await service.IsFamilyMemberOfUnitAsync(familyUnitId, userId, ct);
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";

if (!isRepresentative && !isFamilyMember && !isAdminOrBoard)
    return TypedResults.Forbid();
```

---

### 5. Update `RegistrationsService.GetByFamilyUnitAsync`

**File**: [RegistrationsService.cs](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L544-L571)

```csharp
public async Task<List<RegistrationListResponse>> GetByFamilyUnitAsync(Guid userId, CancellationToken ct)
{
    var familyUnit = await familyUnitsRepo.GetFamilyUnitByRepresentativeIdAsync(userId, ct)
                  ?? await familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(userId, ct);
    if (familyUnit is null) return [];

    var registrations = await registrationsRepo.GetByFamilyUnitAsync(familyUnit.Id, ct);
    // ... rest of method unchanged
}
```

---

### 6. Update `RegistrationsService.GetByIdAsync`

**File**: [RegistrationsService.cs](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L528-L542)

```csharp
public async Task<RegistrationResponse> GetByIdAsync(
    Guid registrationId, Guid userId, bool isAdminOrBoard, CancellationToken ct)
{
    var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    if (!isAdminOrBoard)
    {
        var isRepresentative = registration.FamilyUnit.RepresentativeUserId == userId;
        if (!isRepresentative)
        {
            var memberUnit = await familyUnitsRepo.GetFamilyUnitByMemberUserIdAsync(userId, ct);
            var isFamilyMember = memberUnit?.Id == registration.FamilyUnitId;
            if (!isFamilyMember)
                throw new BusinessRuleException("No tienes permiso para ver esta inscripción");
        }
    }

    var amountPaid = registration.Payments
        .Where(p => p.Status == PaymentStatus.Completed)
        .Sum(p => p.Amount);

    return registration.ToResponse(amountPaid);
}
```

---

## Frontend Changes

### Fix `isViewingOther` in `FamilyUnitPage.vue`

**File**: [FamilyUnitPage.vue](frontend/src/views/FamilyUnitPage.vue#L54-L56)

Current:
```ts
const isViewingOther = computed(() =>
  !!route.params.id && familyUnit.value?.representativeUserId !== auth.user?.id
)
```

Fix:
```ts
const isViewingOther = computed(() =>
  familyUnit.value !== null && familyUnit.value.representativeUserId !== auth.user?.id
)
```

This ensures that non-representative members see the page in read-only mode regardless of whether they accessed it via `/mi-familia` (no ID param) or via `/familia/{id}`.

---

## Tests

### Unit Tests — Backend

**File**: [FamilyUnitsServiceTests.cs](src/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsServiceTests.cs)

Add to `#region Authorization Helper Tests`:

#### `GetCurrentUserFamilyUnitAsync_WhenUserIsMemberButNotRepresentative_ReturnsFamilyUnit`
```
Arrange: GetFamilyUnitByRepresentativeIdAsync → null
         GetFamilyUnitByMemberUserIdAsync → valid FamilyUnit
Act:     await sut.GetCurrentUserFamilyUnitAsync(userId, ct)
Assert:  result.Id == familyUnit.Id
```

#### `GetCurrentUserFamilyUnitAsync_WhenUserIsNeitherRepresentativeNorMember_ThrowsNotFoundException`
```
Arrange: GetFamilyUnitByRepresentativeIdAsync → null
         GetFamilyUnitByMemberUserIdAsync → null
Act:     await sut.GetCurrentUserFamilyUnitAsync(userId, ct)
Assert:  throws NotFoundException
```

#### `IsFamilyMemberOfUnitAsync_WhenUserIsLinkedMemberOfUnit_ReturnsTrue`
```
Arrange: GetFamilyUnitByMemberUserIdAsync(userId) → FamilyUnit { Id = familyUnitId }
Act:     await sut.IsFamilyMemberOfUnitAsync(familyUnitId, userId, ct)
Assert:  result == true
```

#### `IsFamilyMemberOfUnitAsync_WhenUserIsMemberOfDifferentUnit_ReturnsFalse`
```
Arrange: GetFamilyUnitByMemberUserIdAsync(userId) → FamilyUnit { Id = otherFamilyUnitId }
Act:     await sut.IsFamilyMemberOfUnitAsync(familyUnitId, userId, ct)
Assert:  result == false
```

#### `IsFamilyMemberOfUnitAsync_WhenUserIsNotAMember_ReturnsFalse`
```
Arrange: GetFamilyUnitByMemberUserIdAsync(userId) → null
Act:     await sut.IsFamilyMemberOfUnitAsync(familyUnitId, userId, ct)
Assert:  result == false
```

### Integration Tests — Backend

**File**: [FamilyUnitsUserLinkingTests.cs](src/Abuvi.Tests/Integration/Features/FamilyUnits/FamilyUnitsUserLinkingTests.cs)

Add to existing class:

#### `LinkedMember_CanGetFamilyUnit_ViaMe`
```
Arrange: User1 creates family unit → adds User2 as family member with User2's email
Act:     User2 calls GET /api/family-units/me
Assert:  200 OK, FamilyUnitId matches
```

#### `LinkedMember_CanGetFamilyUnit_ById`
```
Act:     User2 calls GET /api/family-units/{familyUnitId}
Assert:  200 OK
```

#### `LinkedMember_CanGetFamilyMembers`
```
Act:     User2 calls GET /api/family-units/{familyUnitId}/members
Assert:  200 OK, returns members list
```

#### `LinkedMember_CannotUpdateFamilyUnit`
```
Act:     User2 calls PUT /api/family-units/{familyUnitId}
Assert:  403 Forbidden
```

#### `LinkedMember_CannotAddFamilyMember`
```
Act:     User2 calls POST /api/family-units/{familyUnitId}/members
Assert:  403 Forbidden
```

#### `LinkedMember_CanSeeRegistrations_ViaMyRegistrations`
```
Arrange: User1 creates family unit, registers for a camp → User2 linked as member
Act:     User2 calls GET /api/registrations
Assert:  200 OK, list contains User1's family registration
```

#### `LinkedMember_CanSeeRegistrationDetail`
```
Act:     User2 calls GET /api/registrations/{registrationId}
Assert:  200 OK
```

---

## Entity Navigation Property Verification

Before implementing, verify that `FamilyUnit` entity has a `FamilyMembers` navigation property and that EF Core is configured to include it. Check:

- **Entity**: `src/Abuvi.API/Data/Entities/FamilyUnit.cs` (or equivalent) — should have `ICollection<FamilyMember> FamilyMembers`
- **Configuration**: EF Core `HasMany` / `WithOne` relationship in `AppDbContext` or `FamilyUnitConfiguration.cs`

If not present, add:
```csharp
// FamilyUnit entity
public ICollection<FamilyMember> FamilyMembers { get; set; } = [];
```

The alternative (avoiding navigation property) is to use a subquery:
```csharp
public async Task<FamilyUnit?> GetFamilyUnitByMemberUserIdAsync(Guid userId, CancellationToken ct)
{
    var familyUnitId = await db.Set<FamilyMember>()
        .AsNoTracking()
        .Where(fm => fm.UserId == userId)
        .Select(fm => fm.FamilyUnitId)
        .FirstOrDefaultAsync(ct);

    if (familyUnitId == default) return null;
    return await db.FamilyUnits.AsNoTracking().FirstOrDefaultAsync(fu => fu.Id == familyUnitId, ct);
}
```

Use this approach if navigation properties are not available or not included in queries.

---

## Implementation Checklist

- [ ] Add `GetFamilyUnitByMemberUserIdAsync` to `IFamilyUnitsRepository` interface
- [ ] Implement `GetFamilyUnitByMemberUserIdAsync` in `FamilyUnitsRepository` (verify entity structure first)
- [ ] Update `FamilyUnitsService.GetCurrentUserFamilyUnitAsync` to fall back to member lookup
- [ ] Add `FamilyUnitsService.IsFamilyMemberOfUnitAsync` helper method
- [ ] Update `GetFamilyUnitById` endpoint handler to allow linked members
- [ ] Update `GetFamilyMembers` endpoint handler to allow linked members
- [ ] Update `GetFamilyMemberById` endpoint handler to allow linked members
- [ ] Update `RegistrationsService.GetByFamilyUnitAsync` to fall back to member lookup
- [ ] Update `RegistrationsService.GetByIdAsync` to allow linked members to read
- [ ] Fix `isViewingOther` computed in `FamilyUnitPage.vue`
- [ ] Write 5 unit tests in `FamilyUnitsServiceTests.cs`
- [ ] Write 7 integration tests in `FamilyUnitsUserLinkingTests.cs`
- [ ] Run full test suite — no regressions

---

## Acceptance Criteria

- ✅ User B (linked as `FamilyMember.UserId`) can access `GET /api/family-units/me` and gets the family unit they belong to
- ✅ User B can access `GET /api/family-units/{id}` for their family unit
- ✅ User B can access `GET /api/family-units/{id}/members`
- ✅ User B gets 403 on any write operation (PUT/DELETE family unit, POST/PUT/DELETE members)
- ✅ User B sees the family's registrations in `GET /api/registrations`
- ✅ User B can view `GET /api/registrations/{id}` for their family's registrations
- ✅ Frontend shows FamilyUnitPage in read-only mode for non-representative members
- ✅ All unit and integration tests pass

# Backend Enhancement: Family Units - User Linking and Representative Email

**Status**: Backend COMPLETE — Tests pending
**Updated**: 2026-03-10

---

## Overview

This enhancement adds two features to the Family Units backend:

1. **Representative Email**: When creating a family unit, the auto-created representative member includes the user's email from the Users table.
2. **Auto-linking Users by Email**: When creating/updating family members, the system automatically links them to existing users if their email matches.

---

## Implementation Status

### ✅ Change 1: Representative Email — DONE

**File**: [FamilyUnitsService.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs#L54-L66)
**Method**: `CreateFamilyUnitAsync`

`Email = user.Email` is already set on line 61 when creating the representative `FamilyMember`.

---

### ✅ Change 2: Repository Method `GetUserByEmailAsync` — DONE

**File**: [FamilyUnitsRepository.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs)

- Interface declaration: line 28
- Implementation: lines 126–129

```csharp
public async Task<User?> GetUserByEmailAsync(string email, CancellationToken ct)
    => await db.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Email == email, ct);
```

---

### ✅ Change 3: Auto-link in `CreateFamilyMemberAsync` — DONE

**File**: [FamilyUnitsService.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs#L160-L172)

If a non-empty email is provided, the service calls `GetUserByEmailAsync`. If a match is found, `UserId` is set to the matched user's ID and an info log is emitted.

---

### ✅ Change 4: Auto-link/Unlink in `UpdateFamilyMemberAsync` — DONE

**File**: [FamilyUnitsService.cs](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs#L241-L266)

Three cases handled:
- Email matches an existing user → link (`UserId = existingUser.Id`)
- Email doesn't match any user but member was linked → unlink (`UserId = null`)
- Email removed entirely but member was linked → unlink (`UserId = null`)

---

## Remaining Work: Tests

No test infrastructure exists yet (`tests/` directory is absent).

### Test Project Setup

**Framework**: xUnit + FluentAssertions + NSubstitute (per [backend-standards.mdc](ai-specs/specs/backend-standards.mdc))
**Project path**: `tests/Abuvi.Tests/`
**Test file**: `tests/Abuvi.Tests/Features/FamilyUnits/FamilyUnitsServiceTests.cs`

---

### Unit Tests to Write

Use NSubstitute to mock `IFamilyUnitsRepository`, `IEncryptionService`, `IBlobStorageService`, `IOptions<BlobStorageOptions>`, and `ILogger<FamilyUnitsService>`.

**Base Arrange** (reuse across tests):
```csharp
var repository = Substitute.For<IFamilyUnitsRepository>();
var encryptionService = Substitute.For<IEncryptionService>();
var blobStorage = Substitute.For<IBlobStorageService>();
var blobOptions = Options.Create(new BlobStorageOptions());
var logger = Substitute.For<ILogger<FamilyUnitsService>>();
var sut = new FamilyUnitsService(repository, encryptionService, blobStorage, blobOptions, logger);
var ct = CancellationToken.None;
```

#### Test 1: `CreateFamilyUnit_ShouldSetRepresentativeEmail`

```
Arrange: mock GetUserByIdAsync → User { Email = "test@example.com", ... }
         mock GetFamilyUnitByRepresentativeIdAsync → null (no existing unit)
         mock CreateFamilyUnitAsync, UpdateUserFamilyUnitIdAsync, CreateFamilyMemberAsync
Act:     await sut.CreateFamilyUnitAsync(userId, request, ct)
Assert:  repository.CreateFamilyMemberAsync called with member where Email == "test@example.com"
```

#### Test 2: `CreateFamilyMember_WithExistingUserEmail_ShouldLinkUser`

```
Arrange: mock GetFamilyUnitByIdAsync → valid FamilyUnit
         mock GetUserByEmailAsync("maria@example.com") → User { Id = mariaId }
         mock CreateFamilyMemberAsync
Act:     await sut.CreateFamilyMemberAsync(familyUnitId, request { Email = "maria@example.com" }, ct)
Assert:  repository.CreateFamilyMemberAsync called with member where UserId == mariaId
```

#### Test 3: `CreateFamilyMember_WithNonExistingEmail_ShouldNotLink`

```
Arrange: mock GetFamilyUnitByIdAsync → valid FamilyUnit
         mock GetUserByEmailAsync → null
Act:     await sut.CreateFamilyMemberAsync(familyUnitId, request { Email = "unknown@example.com" }, ct)
Assert:  repository.CreateFamilyMemberAsync called with member where UserId == null
```

#### Test 4: `UpdateFamilyMember_ChangingToExistingUserEmail_ShouldLinkUser`

```
Arrange: mock GetFamilyMemberByIdAsync → FamilyMember { UserId = null, Email = null }
         mock GetUserByEmailAsync("john@example.com") → User { Id = johnId }
         mock UpdateFamilyMemberAsync
Act:     await sut.UpdateFamilyMemberAsync(memberId, request { Email = "john@example.com" }, ct)
Assert:  repository.UpdateFamilyMemberAsync called with member where UserId == johnId
```

#### Test 5: `UpdateFamilyMember_ChangingEmailToNonExisting_ShouldUnlink`

```
Arrange: mock GetFamilyMemberByIdAsync → FamilyMember { UserId = existingUserId }
         mock GetUserByEmailAsync("other@example.com") → null
Act:     await sut.UpdateFamilyMemberAsync(memberId, request { Email = "other@example.com" }, ct)
Assert:  repository.UpdateFamilyMemberAsync called with member where UserId == null
```

#### Test 6: `UpdateFamilyMember_RemovingEmail_ShouldUnlink`

```
Arrange: mock GetFamilyMemberByIdAsync → FamilyMember { UserId = existingUserId, Email = "old@example.com" }
Act:     await sut.UpdateFamilyMemberAsync(memberId, request { Email = null or "" }, ct)
Assert:  repository.UpdateFamilyMemberAsync called with member where UserId == null
         GetUserByEmailAsync NOT called
```

---

### Integration Tests (Optional / future)

If `WebApplicationFactory`-based integration tests exist or are added:

1. **Create family unit → verify representative email**
   POST `/api/family-units` → GET `/api/family-units/{id}/members` → assert first member has email

2. **Create family member with existing user email → verify UserId linked**
   POST `/api/family-units/{id}/members` with email of user2 → assert `UserId` == user2.Id

3. **Update family member email → link/unlink cycle**
   Update to existing user email → assert linked. Update to non-existing → assert unlinked.

---

## Database Considerations

No schema changes required. The `Email` column already exists on `FamilyMembers`, and `Users.Email` has a unique constraint.

### Backfill Script (Optional)

Run once to fix existing representative members missing email:

```sql
UPDATE "FamilyMembers" fm
SET "Email" = u."Email"
FROM "Users" u
INNER JOIN "FamilyUnits" fu ON fu."RepresentativeUserId" = u."Id"
WHERE fm."FamilyUnitId" = fu."Id"
  AND fm."UserId" = u."Id"
  AND fm."Email" IS NULL;
```

---

## Implementation Checklist

- [x] Add `Email = user.Email` in `CreateFamilyUnitAsync` (representative creation)
- [x] Add `GetUserByEmailAsync` to `IFamilyUnitsRepository` interface
- [x] Implement `GetUserByEmailAsync` in `FamilyUnitsRepository`
- [x] Add email lookup and linking logic to `CreateFamilyMemberAsync`
- [x] Add email lookup and linking/unlinking logic to `UpdateFamilyMemberAsync`
- [ ] Create test project `tests/Abuvi.Tests/` with xUnit + FluentAssertions + NSubstitute
- [ ] Write 6 unit tests for `FamilyUnitsService` (listed above)
- [ ] Run all tests to confirm no regressions
- [ ] (Optional) Run backfill SQL script on existing data
- [ ] (Optional) Write integration tests for end-to-end flows

---

## Acceptance Criteria

- ✅ Creating a family unit sets the representative member's `Email` from the Users table
- ✅ Creating a family member with a matching user email automatically sets `UserId`
- ✅ Updating a family member's email to match an existing user links them
- ✅ Updating a family member's email to a non-matching address unlinks them
- ✅ Removing a family member's email unlinks them
- ⬜ All unit tests pass (6 new tests)
- ⬜ No regressions in existing tests

---

**Document Version**: 1.1 (updated from 1.0 to reflect completed implementation)
**Original**: 2026-02-16
**Updated**: 2026-03-10

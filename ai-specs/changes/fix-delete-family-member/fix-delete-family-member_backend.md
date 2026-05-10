# Backend Implementation Plan: fix-delete-family-member — Family Member Soft Delete + GDPR Anonymisation

## Overview

The `DELETE /api/family-units/{familyUnitId}/members/{memberId}` endpoint fails silently (HTTP 500) when the target member has any past registration or membership, because the DB has `DeleteBehavior.Restrict` FKs on both `registration_members.family_member_id` and `memberships.family_member_id`.

This plan replaces the hard-delete-or-error approach with a two-tier strategy:
- **Hard delete** when the member has no historical references (clean slate).
- **Soft delete** (`deleted_at`) otherwise — preserves FK integrity, hides the member from all API responses via a global EF query filter.

A new Admin-only endpoint performs GDPR right-to-erasure anonymisation when even the soft-deleted record must have its PII cleared.

Architecture: Vertical Slice, `Features/FamilyUnits/`.

---

## Architecture Context

**Feature slice**: `src/Abuvi.API/Features/FamilyUnits/`

| File | Status |
|---|---|
| `FamilyUnitsModels.cs` | Modify — add `DeletedAt` to `FamilyMember` |
| `FamilyUnitsRepository.cs` | Modify — add 3 new interface methods + implementations |
| `FamilyUnitsService.cs` | Modify — rewrite `DeleteFamilyMemberAsync`, add `AnonymiseFamilyMemberAsync` |
| `FamilyUnitsEndpoints.cs` | Modify — add PII endpoint registration on `adminGroup` + handler |
| `Data/Configurations/FamilyMemberConfiguration.cs` | Modify — add `deleted_at` column mapping |
| `Data/AbuviDbContext.cs` | Modify — add global query filter |
| `src/Abuvi.Tests/Unit/Features/FamilyUnits/DeleteFamilyMemberTests.cs` | Create — TDD unit tests |
| EF Core migration | Create — `AddDeletedAtToFamilyMembers` |

**Cross-cutting concerns**: Global query filter in `AbuviDbContext` affects all `db.FamilyMembers` queries across the application (desired behaviour — soft-deleted members should be invisible everywhere).

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the feature branch.
- **Branch name**: `fix/fix-delete-family-member-backend`
- **Implementation Steps**:
  1. `git checkout dev && git pull`
  2. `git checkout -b fix/fix-delete-family-member-backend`
  3. `git branch` — verify you are on the new branch.

---

### Step 1: Add `DeletedAt` to `FamilyMember` Entity

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`

- **Action**: Add `DeletedAt` property to `FamilyMember`.
- **Implementation Steps**:
  1. In the `FamilyMember` class, after `UpdatedAt`, add:
     ```csharp
     public DateTime? DeletedAt { get; set; }
     ```

---

### Step 2: Add `deleted_at` Column Mapping

**File**: `src/Abuvi.API/Data/Configurations/FamilyMemberConfiguration.cs`

- **Action**: Explicitly map the new nullable column so it gets the correct snake_case name.
- **Implementation Steps**:
  1. After the `UpdatedAt` configuration block, add:
     ```csharp
     builder.Property(fm => fm.DeletedAt)
         .HasColumnName("deleted_at");
     ```

---

### Step 3: Add Global Query Filter in `AbuviDbContext`

**File**: `src/Abuvi.API/Data/AbuviDbContext.cs`

- **Action**: Exclude soft-deleted family members from all EF queries automatically.
- **Implementation Steps**:
  1. In `OnModelCreating`, find the `FamilyMember` entity configuration block (or add it if absent).
  2. Add the query filter:
     ```csharp
     modelBuilder.Entity<FamilyMember>()
         .HasQueryFilter(m => m.DeletedAt == null);
     ```
  3. **Important**: this filter applies project-wide. Any future query that intentionally needs soft-deleted members (e.g., the anonymisation method) must call `.IgnoreQueryFilters()`.

---

### Step 4: Create EF Core Migration

- **Action**: Generate the migration for the new `deleted_at` column.
- **Implementation Steps**:
  1. Run:
     ```bash
     dotnet ef migrations add AddDeletedAtToFamilyMembers --project src/Abuvi.API
     ```
  2. Review the generated migration — confirm a single `AddColumn` for `deleted_at TIMESTAMPTZ NULL` on `family_members`.
  3. Do **not** apply the migration now; the developer will apply it locally against the dev DB.

---

### Step 5: Add Repository Methods

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`

#### 5.1 — Extend `IFamilyUnitsRepository` interface

Add three new method signatures:

```csharp
/// <summary>
/// Returns true if the member has any RegistrationMember row (any status).
/// </summary>
Task<bool> MemberHasAnyRegistrationsAsync(Guid memberId, CancellationToken ct);

/// <summary>
/// Returns true if the member has any Membership row (any status/date).
/// </summary>
Task<bool> MemberHasMembershipAsync(Guid memberId, CancellationToken ct);

/// <summary>
/// Soft-deletes the member by setting DeletedAt = UtcNow.
/// </summary>
Task SoftDeleteFamilyMemberAsync(Guid id, CancellationToken ct);

/// <summary>
/// Anonymises all PII fields on the member row (GDPR right-to-erasure).
/// Uses IgnoreQueryFilters so it works on already soft-deleted members.
/// </summary>
Task AnonymiseFamilyMemberAsync(Guid id, CancellationToken ct);
```

#### 5.2 — Implement the four methods in `FamilyUnitsRepository`

```csharp
public async Task<bool> MemberHasAnyRegistrationsAsync(Guid memberId, CancellationToken ct)
    => await db.RegistrationMembers
        .AnyAsync(rm => rm.FamilyMemberId == memberId, ct);

public async Task<bool> MemberHasMembershipAsync(Guid memberId, CancellationToken ct)
    => await db.Memberships
        .AnyAsync(m => m.FamilyMemberId == memberId, ct);

public async Task SoftDeleteFamilyMemberAsync(Guid id, CancellationToken ct)
{
    await db.FamilyMembers
        .Where(fm => fm.Id == id)
        .ExecuteUpdateAsync(s => s
            .SetProperty(fm => fm.DeletedAt, DateTime.UtcNow)
            .SetProperty(fm => fm.UpdatedAt, DateTime.UtcNow), ct);
}

public async Task AnonymiseFamilyMemberAsync(Guid id, CancellationToken ct)
{
    await db.FamilyMembers
        .IgnoreQueryFilters()
        .Where(fm => fm.Id == id)
        .ExecuteUpdateAsync(s => s
            .SetProperty(fm => fm.FirstName, "[deleted]")
            .SetProperty(fm => fm.LastName, "[deleted]")
            .SetProperty(fm => fm.DateOfBirth, new DateOnly(1900, 1, 1))
            .SetProperty(fm => fm.DocumentNumber, (string?)null)
            .SetProperty(fm => fm.Email, (string?)null)
            .SetProperty(fm => fm.Phone, (string?)null)
            .SetProperty(fm => fm.MedicalNotes, (string?)null)
            .SetProperty(fm => fm.Allergies, (string?)null)
            .SetProperty(fm => fm.ProfilePhotoUrl, (string?)null)
            .SetProperty(fm => fm.UserId, (Guid?)null)
            .SetProperty(fm => fm.DeletedAt, DateTime.UtcNow)
            .SetProperty(fm => fm.UpdatedAt, DateTime.UtcNow), ct);
}
```

**Notes**:
- `MemberHasAnyRegistrationsAsync` queries `db.RegistrationMembers` directly — not filtered by the `FamilyMember` global filter.
- `MemberHasMembershipAsync` queries `db.Memberships` directly — same reasoning.
- `AnonymiseFamilyMemberAsync` uses `IgnoreQueryFilters()` so it can anonymise already soft-deleted members.
- Keep `MemberHasActiveRegistrationsAsync` unchanged (still used by the Admin/Board active-registration guard).

---

### Step 6: Rewrite Service Methods

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

#### 6.1 — Rewrite `DeleteFamilyMemberAsync`

Replace the current implementation with:

```csharp
public async Task DeleteFamilyMemberAsync(Guid id, bool isAdminOrBoard, CancellationToken ct)
{
    var member = await repository.GetFamilyMemberByIdAsync(id, ct)
        ?? throw new NotFoundException("Miembro Familiar", id);

    // Cannot delete the representative's own member record
    var familyUnit = await repository.GetFamilyUnitByIdAsync(member.FamilyUnitId, ct);
    if (familyUnit != null && member.UserId.HasValue
        && familyUnit.RepresentativeUserId == member.UserId.Value)
    {
        throw new BusinessRuleException(
            "No se puede eliminar al representante de la unidad familiar.");
    }

    // Admin/Board: block deletion of members with active (Pending/Confirmed) registrations
    if (isAdminOrBoard)
    {
        var hasActiveRegs = await repository.MemberHasActiveRegistrationsAsync(id, ct);
        if (hasActiveRegs)
            throw new BusinessRuleException(
                "No se puede eliminar un miembro con inscripciones activas (Pendiente/Confirmada).");
    }

    // Check for any historical references that block hard delete
    var hasRegistrations = await repository.MemberHasAnyRegistrationsAsync(id, ct);
    var hasMembership = await repository.MemberHasMembershipAsync(id, ct);

    if (hasRegistrations || hasMembership)
    {
        await repository.SoftDeleteFamilyMemberAsync(id, ct);
        logger.LogInformation(
            "Soft-deleted family member {MemberId} ({FirstName} {LastName}) from family unit {FamilyUnitId}",
            id, member.FirstName, member.LastName, member.FamilyUnitId);
    }
    else
    {
        await repository.DeleteFamilyMemberAsync(id, ct);
        logger.LogInformation(
            "Hard-deleted family member {MemberId} ({FirstName} {LastName}) from family unit {FamilyUnitId}",
            id, member.FirstName, member.LastName, member.FamilyUnitId);
    }
}
```

#### 6.2 — Add `AnonymiseFamilyMemberAsync`

Add this new method to `FamilyUnitsService` (within the Family Member region):

```csharp
/// <summary>
/// GDPR right-to-erasure: anonymises all PII fields of a family member.
/// Works on both active and soft-deleted members. Admin/Board only.
/// </summary>
public async Task AnonymiseFamilyMemberAsync(
    Guid familyUnitId, Guid memberId, CancellationToken ct)
{
    // Load with IgnoreQueryFilters to find soft-deleted members too
    var member = await repository.GetFamilyMemberByIdIgnoringFiltersAsync(memberId, ct)
        ?? throw new NotFoundException("Miembro Familiar", memberId);

    // Cross-family guard
    if (member.FamilyUnitId != familyUnitId)
        throw new NotFoundException("Miembro Familiar", memberId);

    await repository.AnonymiseFamilyMemberAsync(memberId, ct);

    logger.LogInformation(
        "Anonymised PII for family member {MemberId} in family unit {FamilyUnitId}",
        memberId, familyUnitId);
}
```

> `GetFamilyMemberByIdIgnoringFiltersAsync` is a new repository method (see Step 5.1 addendum below).

#### 6.2 Addendum — Add `GetFamilyMemberByIdIgnoringFiltersAsync` to repository

Add to the `IFamilyUnitsRepository` interface and `FamilyUnitsRepository`:

```csharp
// Interface
Task<FamilyMember?> GetFamilyMemberByIdIgnoringFiltersAsync(Guid id, CancellationToken ct);

// Implementation
public async Task<FamilyMember?> GetFamilyMemberByIdIgnoringFiltersAsync(Guid id, CancellationToken ct)
    => await db.FamilyMembers
        .IgnoreQueryFilters()
        .AsNoTracking()
        .FirstOrDefaultAsync(fm => fm.Id == id, ct);
```

---

### Step 7: Add PII Endpoint

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`

#### 7.1 — Register the endpoint (in `MapFamilyUnitsEndpoints`)

In the `adminGroup` registration block, add:

```csharp
adminGroup.MapDelete("/{familyUnitId:guid}/members/{memberId:guid}/pii", AnonymiseFamilyMemberPii)
    .WithName("AnonymiseFamilyMemberPii")
    .WithSummary("GDPR right-to-erasure: anonymise all PII of a family member (Admin/Board only)")
    .Produces(204)
    .Produces(403)
    .Produces(404);
```

#### 7.2 — Add the handler

```csharp
private static async Task<IResult> AnonymiseFamilyMemberPii(
    Guid familyUnitId,
    Guid memberId,
    FamilyUnitsService service,
    CancellationToken ct)
{
    try
    {
        await service.AnonymiseFamilyMemberAsync(familyUnitId, memberId, ct);
        return TypedResults.NoContent();
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

**Notes**:
- Authorization is inherited from `adminGroup` which already requires `Admin` or `Board` role.
- No `BusinessRuleException` path — anonymisation is always allowed for admin on any state.

---

### Step 8: Write Unit Tests (TDD)

**File**: `src/Abuvi.Tests/Unit/Features/FamilyUnits/DeleteFamilyMemberTests.cs`

Create the file with the following test cases. Follow the AAA pattern and NSubstitute for mocking.

#### Constructor setup

```csharp
public class DeleteFamilyMemberTests
{
    private readonly IFamilyUnitsRepository _repository;
    private readonly FamilyUnitsService _sut;

    public DeleteFamilyMemberTests()
    {
        _repository = Substitute.For<IFamilyUnitsRepository>();
        var encryptionService = Substitute.For<IEncryptionService>();
        var blobService = Substitute.For<IBlobStorageService>();
        var blobOptions = Options.Create(new BlobStorageOptions());
        var logger = Substitute.For<ILogger<FamilyUnitsService>>();

        _sut = new FamilyUnitsService(
            _repository, encryptionService, blobService, blobOptions, logger);
    }
}
```

#### Test cases

**`Delete_NoHistory_HardDeletes`**
- Arrange: member exists, not representative, no active regs, no registrations, no membership.
- Act: call `DeleteFamilyMemberAsync(id, isAdminOrBoard: false, ct)`.
- Assert: `repository.Received(1).DeleteFamilyMemberAsync(id, ct)` and `SoftDeleteFamilyMemberAsync` not called.

**`Delete_WithCancelledRegistration_SoftDeletes`**
- Arrange: `MemberHasAnyRegistrationsAsync` returns `true`, `MemberHasMembershipAsync` returns `false`.
- Assert: `SoftDeleteFamilyMemberAsync` called, `DeleteFamilyMemberAsync` not called.

**`Delete_WithActiveMembership_SoftDeletes`**
- Arrange: `MemberHasAnyRegistrationsAsync` returns `false`, `MemberHasMembershipAsync` returns `true`.
- Assert: `SoftDeleteFamilyMemberAsync` called, `DeleteFamilyMemberAsync` not called.

**`Delete_Representative_Returns409`**
- Arrange: member's `UserId` matches `familyUnit.RepresentativeUserId`.
- Assert: throws `BusinessRuleException`.

**`Delete_AdminBoard_ActiveRegistration_Returns409`**
- Arrange: `isAdminOrBoard = true`, `MemberHasActiveRegistrationsAsync` returns `true`.
- Assert: throws `BusinessRuleException` with message containing "activas".

**`Delete_NotFound_Returns404`**
- Arrange: `GetFamilyMemberByIdAsync` returns `null`.
- Assert: throws `NotFoundException`.

**`Anonymise_ValidMember_CallsRepository`**
- Arrange: `GetFamilyMemberByIdIgnoringFiltersAsync` returns a member with correct `FamilyUnitId`.
- Assert: `repository.Received(1).AnonymiseFamilyMemberAsync(memberId, ct)`.

**`Anonymise_WrongFamilyUnit_Returns404`**
- Arrange: member's `FamilyUnitId` differs from the requested `familyUnitId`.
- Assert: throws `NotFoundException`.

**`Anonymise_NotFound_Returns404`**
- Arrange: `GetFamilyMemberByIdIgnoringFiltersAsync` returns `null`.
- Assert: throws `NotFoundException`.

---

### Step 9: Update Technical Documentation

- **Action**: Update affected spec files.
- **Implementation Steps**:
  1. **`ai-specs/specs/data-model.md`** (if it exists): Add `deleted_at TIMESTAMPTZ NULL` to the `family_members` table description; document that soft-deleted members are excluded from API responses via a global EF query filter.
  2. **`ai-specs/specs/api-spec.yml`**: Add the new endpoint `DELETE /api/family-units/{familyUnitId}/members/{memberId}/pii` with its request/response schema (204, 403, 404).
  3. All documentation must be written in **English**.

---

## Implementation Order

1. Step 0 — Create branch
2. Step 8 — Write unit tests first (TDD — they will fail initially)
3. Step 1 — Add `DeletedAt` to `FamilyMember`
4. Step 2 — Add `deleted_at` column mapping
5. Step 3 — Add global query filter in `AbuviDbContext`
6. Step 4 — Create EF Core migration
7. Step 5 — Add repository methods (interface + implementation)
8. Step 6 — Rewrite service methods
9. Step 7 — Add PII endpoint
10. Run tests — all should pass
11. Step 9 — Update documentation

---

## Testing Checklist

- [ ] `Delete_NoHistory_HardDeletes` — hard delete when no refs
- [ ] `Delete_WithCancelledRegistration_SoftDeletes` — soft delete path
- [ ] `Delete_WithActiveMembership_SoftDeletes` — soft delete path
- [ ] `Delete_Representative_Returns409` — guard still present
- [ ] `Delete_AdminBoard_ActiveRegistration_Returns409` — admin guard still present
- [ ] `Delete_NotFound_Returns404`
- [ ] `Anonymise_ValidMember_CallsRepository`
- [ ] `Anonymise_WrongFamilyUnit_Returns404`
- [ ] `Anonymise_NotFound_Returns404`
- [ ] `dotnet test` — no new failures outside the pre-existing set

---

## Error Response Format

All errors use `ApiResponse<T>` envelope:

```json
{
  "success": false,
  "error": {
    "message": "...",
    "code": "CANNOT_DELETE_MEMBER"
  }
}
```

| Status | Condition |
|---|---|
| 204 | Member deleted (hard or soft) or PII anonymised |
| 403 | Caller is not representative / not Admin-Board for PII endpoint |
| 404 | Member not found |
| 409 | Cannot delete representative; or Admin/Board blocking active registration |

---

## Dependencies

No new NuGet packages required.

**Migration command**:
```bash
dotnet ef migrations add AddDeletedAtToFamilyMembers --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Notes

- **Global query filter caveat**: All existing code that reads `db.FamilyMembers` will automatically exclude soft-deleted members after this change. Review `GetAllPagedAsync` in the repository — `MembersCount` sub-query already uses `db.FamilyMembers.Count(m => m.FamilyUnitId == fu.Id)`, which will now exclude soft-deleted members. This is correct behaviour.
- **Soft-deleted member and profile photo**: The existing `DeleteFamilyMemberAsync` (repository, hard-delete) does not remove the profile photo from blob storage. The soft-delete path also does not need to; the anonymisation endpoint clears `ProfilePhotoUrl` to `null` but does not delete the blob (out of scope).
- **Representative guard**: A soft-deleted member who is also a representative cannot exist in practice (representative has an active user link), but the guard is retained regardless.
- **GDPR**: `AnonymiseFamilyMemberAsync` permanently replaces all PII with sentinel values. This action is irreversible. The log entry uses `LogInformation` with no PII in the log message (only IDs).
- **`isAdminOrBoard` parameter**: The active-registration check (Admin/Board guard) runs **before** the new `MemberHasAnyRegistrations` / `MemberHasMembership` check, so an admin who tries to delete a member with a Pending registration still gets a 409 — they must first move the registration to a terminal status.

---

## Next Steps After Implementation

- Merge PR to `dev`.
- Frontend must stop showing soft-deleted members in the family member list (already handled — API excludes them via query filter, so frontend sees an empty slot).
- Consider adding a soft-delete indicator to the admin family unit detail view (out of scope for this ticket).

# Backend Enhancement: Family Units - User Linking and Representative Email

## Overview

This enhancement addresses two missing features in the Family Units backend implementation:

1. **Representative Email Missing**: When a family unit is created, the representative member should include the user's email from the Users table
2. **Auto-linking Users by Email**: When creating/updating family members, the system should automatically link them to existing users if their email matches

## Business Rules

### Rule 1: Representative Member Email

- When a user creates a family unit, they become the representative
- The system auto-creates a FamilyMember record for the representative
- **REQUIRED**: The representative's email must be copied from the Users table to the FamilyMember record

### Rule 2: Auto-link Family Members to Users

- When creating or updating a family member with an email address
- **IF** that email exists in the Users table (email is unique constraint)
- **THEN** automatically set the FamilyMember.UserId to link them
- This allows family members who are also app users to be properly linked

## Current Implementation Issues

### Issue 1: Representative Email Not Set

**Location**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs:49-63`

**Current Code**:

```csharp
// Automatically create representative as family member
var representativeMember = new FamilyMember
{
    Id = Guid.NewGuid(),
    FamilyUnitId = familyUnit.Id,
    UserId = userId,
    FirstName = user.FirstName,
    LastName = user.LastName,
    DateOfBirth = DateOnly.MinValue, // User should update this later
    Relationship = FamilyRelationship.Parent,
    // ❌ MISSING: Email field not set
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

**Problem**: The representative's email is not being copied from the User entity, even though we have access to the `user` object.

### Issue 2: No Auto-linking by Email

**Location**:

- `CreateFamilyMemberAsync` (lines 148-190)
- `UpdateFamilyMemberAsync` (lines 216-245)

**Current Behavior**:

- UserId is always set to `null` when creating family members (line 169)
- No check is performed to see if the provided email matches an existing user
- Family members remain unlinked even if they have a registered account

**Problem**: Family members who are also users cannot be automatically linked, requiring manual database updates.

## Required Changes

### Change 1: Add Email to Representative Member Creation

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

**Method**: `CreateFamilyUnitAsync` (line 49-63)

**Implementation**:

```csharp
// Automatically create representative as family member
var representativeMember = new FamilyMember
{
    Id = Guid.NewGuid(),
    FamilyUnitId = familyUnit.Id,
    UserId = userId,
    FirstName = user.FirstName,
    LastName = user.LastName,
    Email = user.Email,  // ✅ ADD THIS LINE
    DateOfBirth = DateOnly.MinValue, // User should update this later
    Relationship = FamilyRelationship.Parent,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

### Change 2: Add Auto-linking Logic

#### 2a. Add Repository Method

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`

**New Method** (add to IFamilyUnitsRepository interface and implementation):

```csharp
/// <summary>
/// Gets a user by email address
/// </summary>
Task<User?> GetUserByEmailAsync(string email, CancellationToken ct);
```

**Implementation in FamilyUnitsRepository**:

```csharp
public async Task<User?> GetUserByEmailAsync(string email, CancellationToken ct)
{
    return await context.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Email == email, ct);
}
```

#### 2b. Update CreateFamilyMemberAsync

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

**Method**: `CreateFamilyMemberAsync` (lines 148-190)

**Changes**:

```csharp
public async Task<FamilyMemberResponse> CreateFamilyMemberAsync(
    Guid familyUnitId, CreateFamilyMemberRequest request, CancellationToken ct)
{
    // Verify family unit exists
    var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
        ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

    // ✅ ADD: Check if email belongs to an existing user
    Guid? userId = null;
    if (!string.IsNullOrWhiteSpace(request.Email))
    {
        var existingUser = await repository.GetUserByEmailAsync(request.Email, ct);
        if (existingUser != null)
        {
            userId = existingUser.Id;
            logger.LogInformation(
                "Family member auto-linked to user {UserId} by email {Email}",
                userId, request.Email);
        }
    }

    // Encrypt sensitive data if provided
    var encryptedMedicalNotes = !string.IsNullOrEmpty(request.MedicalNotes)
        ? encryptionService.Encrypt(request.MedicalNotes)
        : null;

    var encryptedAllergies = !string.IsNullOrEmpty(request.Allergies)
        ? encryptionService.Encrypt(request.Allergies)
        : null;

    // Create family member
    var member = new FamilyMember
    {
        Id = Guid.NewGuid(),
        FamilyUnitId = familyUnitId,
        UserId = userId,  // ✅ CHANGED: Use looked-up userId instead of null
        FirstName = request.FirstName,
        LastName = request.LastName,
        DateOfBirth = request.DateOfBirth,
        Relationship = request.Relationship,
        DocumentNumber = request.DocumentNumber?.ToUpperInvariant(),
        Email = request.Email,
        Phone = request.Phone,
        MedicalNotes = encryptedMedicalNotes,
        Allergies = encryptedAllergies,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    await repository.CreateFamilyMemberAsync(member, ct);

    logger.LogInformation(
        "Family member {MemberId} created in family unit {FamilyUnitId}",
        member.Id, familyUnitId);

    return member.ToResponse();
}
```

#### 2c. Update UpdateFamilyMemberAsync

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

**Method**: `UpdateFamilyMemberAsync` (lines 216-245)

**Changes**:

```csharp
public async Task<FamilyMemberResponse> UpdateFamilyMemberAsync(
    Guid id, UpdateFamilyMemberRequest request, CancellationToken ct)
{
    var member = await repository.GetFamilyMemberByIdAsync(id, ct)
        ?? throw new NotFoundException("Miembro Familiar", id);

    // ✅ ADD: Check if email belongs to an existing user (update linking)
    if (!string.IsNullOrWhiteSpace(request.Email))
    {
        var existingUser = await repository.GetUserByEmailAsync(request.Email, ct);
        if (existingUser != null && member.UserId != existingUser.Id)
        {
            member.UserId = existingUser.Id;
            logger.LogInformation(
                "Family member {MemberId} auto-linked to user {UserId} by email {Email}",
                id, existingUser.Id, request.Email);
        }
        else if (existingUser == null && member.UserId.HasValue)
        {
            // Email changed and no longer matches a user - unlink
            logger.LogInformation(
                "Family member {MemberId} unlinked from user {UserId} - email no longer matches",
                id, member.UserId.Value);
            member.UserId = null;
        }
    }
    else if (member.UserId.HasValue)
    {
        // Email removed - unlink
        logger.LogInformation(
            "Family member {MemberId} unlinked from user {UserId} - email removed",
            id, member.UserId.Value);
        member.UserId = null;
    }

    // Update basic fields
    member.FirstName = request.FirstName;
    member.LastName = request.LastName;
    member.DateOfBirth = request.DateOfBirth;
    member.Relationship = request.Relationship;
    member.DocumentNumber = request.DocumentNumber?.ToUpperInvariant();
    member.Email = request.Email;
    member.Phone = request.Phone;

    // Encrypt sensitive data if provided
    member.MedicalNotes = !string.IsNullOrEmpty(request.MedicalNotes)
        ? encryptionService.Encrypt(request.MedicalNotes)
        : null;

    member.Allergies = !string.IsNullOrEmpty(request.Allergies)
        ? encryptionService.Encrypt(request.Allergies)
        : null;

    await repository.UpdateFamilyMemberAsync(member, ct);

    logger.LogInformation("Family member {MemberId} updated", id);

    return member.ToResponse();
}
```

## Testing Requirements

### Unit Tests

**File**: `tests/Abuvi.Tests/Features/FamilyUnits/FamilyUnitsServiceTests.cs`

**New Tests to Add**:

1. **CreateFamilyUnit_ShouldSetRepresentativeEmail**
   - Arrange: User with email "<test@example.com>"
   - Act: Create family unit
   - Assert: Representative member has email set to "<test@example.com>"

2. **CreateFamilyMember_WithExistingUserEmail_ShouldLinkUser**
   - Arrange: Existing user with email "<maria@example.com>"
   - Act: Create family member with same email
   - Assert: FamilyMember.UserId is set to the existing user's ID

3. **CreateFamilyMember_WithNonExistingEmail_ShouldNotLink**
   - Arrange: No user with email "<unknown@example.com>"
   - Act: Create family member with that email
   - Assert: FamilyMember.UserId is null

4. **UpdateFamilyMember_ChangingToExistingUserEmail_ShouldLinkUser**
   - Arrange: Family member with no email, existing user with "<john@example.com>"
   - Act: Update family member to use "<john@example.com>"
   - Assert: FamilyMember.UserId is set to John's user ID

5. **UpdateFamilyMember_ChangingEmailToNonExisting_ShouldUnlink**
   - Arrange: Family member linked to user
   - Act: Change email to one that doesn't exist in Users
   - Assert: FamilyMember.UserId is set to null

6. **UpdateFamilyMember_RemovingEmail_ShouldUnlink**
   - Arrange: Family member linked to user with email
   - Act: Update family member with null/empty email
   - Assert: FamilyMember.UserId is set to null

### Integration Tests

**Test Scenarios**:

1. **End-to-End: Create Family Unit → Verify Representative Email**
   - Create user and authenticate
   - Create family unit via API
   - GET /api/family-units/{id}/members
   - Verify representative member has correct email

2. **End-to-End: Create Family Member with Existing User Email**
   - Create two users (user1 and user2)
   - User1 creates family unit
   - User1 creates family member with user2's email
   - Verify family member is linked to user2

3. **End-to-End: Update Family Member Email to Link/Unlink**
   - Create family member without user link
   - Update with existing user's email
   - Verify link is created
   - Update with non-existing email
   - Verify link is removed

## Database Considerations

### Email Uniqueness

- The Users table already has a unique constraint on the Email column
- This ensures one user per email address
- No database schema changes required

### Existing Data Migration

- **Representative Members**: Existing representative members created before this fix will NOT have emails
- **Recommended**: Create a one-time migration script to backfill representative emails:

```sql
-- Backfill representative member emails from Users table
UPDATE FamilyMembers fm
SET Email = u.Email
FROM Users u
INNER JOIN FamilyUnits fu ON fu.RepresentativeUserId = u.Id
WHERE fm.FamilyUnitId = fu.Id
  AND fm.UserId = u.Id
  AND fm.Email IS NULL;
```

## Impact Analysis

### Frontend Impact

- **None**: Frontend already expects email in FamilyMemberResponse
- Frontend will automatically display the representative's email once backend is fixed

### API Contract Impact

- **None**: No breaking changes to API request/response models
- Behavior enhancement only (auto-linking)

### Security Considerations

- **Privacy**: Email linking uses existing user data - no new privacy concerns
- **Authorization**: Family members can only be created/updated by family unit representative or Admin/Board
- **Data Exposure**: UserId linking doesn't expose any additional data - family members already show email

## Implementation Checklist

- [ ] Add `Email = user.Email` in CreateFamilyUnitAsync (representative creation)
- [ ] Add `GetUserByEmailAsync` method to IFamilyUnitsRepository interface
- [ ] Implement `GetUserByEmailAsync` in FamilyUnitsRepository
- [ ] Add email lookup and linking logic to CreateFamilyMemberAsync
- [ ] Add email lookup and linking/unlinking logic to UpdateFamilyMemberAsync
- [ ] Write unit tests for all scenarios (6 new tests)
- [ ] Write integration tests (3 scenarios)
- [ ] Run existing tests to ensure no regressions
- [ ] Create data migration script for backfilling representative emails (optional but recommended)
- [ ] Update API documentation if needed (no changes expected)
- [ ] Test manually with Postman/Swagger

## Acceptance Criteria

### Feature 1: Representative Email

✅ When a user creates a family unit, the auto-created representative member includes their email from the Users table

### Feature 2: Auto-linking

✅ When creating a family member with an email that exists in Users table, the UserId is automatically set
✅ When updating a family member's email to match an existing user, the UserId is automatically set
✅ When updating a family member's email to not match any user, the UserId is cleared (unlinked)
✅ When removing a family member's email, the UserId is cleared if previously linked

### Testing

✅ All unit tests pass (existing + 6 new tests)
✅ All integration tests pass (3 new scenarios)
✅ Manual testing confirms both features work end-to-end

## Timeline Estimate

- **Implementation**: 2-3 hours
- **Testing**: 2 hours
- **Code Review & Adjustments**: 1 hour
- **Total**: ~5-6 hours

## Priority

**HIGH** - This affects user experience and data completeness:

- Representative email is critical for contact purposes
- Auto-linking improves data integrity and user management

---

**Document Version**: 1.0
**Created**: 2026-02-16
**Status**: Ready for Implementation

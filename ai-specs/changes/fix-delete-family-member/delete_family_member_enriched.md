# Fix: Family Member Deletion — Soft Delete + GDPR Anonymisation

## Problem

Deleting a `FamilyMember` fails silently or with an opaque database error because the service only guards against _active_ registrations, but two `DeleteBehavior.Restrict` foreign keys block **any** hard delete at the DB level:

| Blocking entity | FK field | Delete behaviour |
|---|---|---|
| `RegistrationMember` | `FamilyMemberId` | Restrict |
| `Membership` | `FamilyMemberId` | Restrict |

This means a member who was ever registered (even in a cancelled/completed registration) or who ever had a membership (even cancelled) cannot be deleted, even though the current service code does not explain this to the caller.

---

## Root Cause Analysis

### Entities referencing `FamilyMember`

```
FamilyUnit (Cascade → FamilyMember)
  └── FamilyMember
        ├── RegistrationMember (Restrict)  ← blocks deletion of any past registrant
        │     └── Registration (status: Pending | Confirmed | Cancelled | Completed | Draft)
        └── Membership (Restrict)          ← blocks deletion of any past/present member
              └── MembershipFee (Cascade)
```

### What the current code does

`FamilyUnitsService.DeleteFamilyMemberAsync`:

1. Guards if member is the representative → 409.
2. Guards if member has **active** registrations (Pending/Confirmed) for Admin/Board → 409.
3. Otherwise calls `ExecuteDeleteAsync` → **DB throws** for Cancelled/Completed `RegistrationMember` rows or any `Membership` row.

The caller receives an unhandled 500 instead of a meaningful 409.

---

## Recommended Strategy

### Two-tier approach

| Tier | Who | Condition | Action |
|---|---|---|---|
| **Standard delete** | Representative / Admin | Member has no registrations AND no membership | Hard delete |
| **Standard delete** | Representative / Admin | Member has any registration OR any membership | **Soft delete** (set `DeletedAt`) |
| **Right to erasure** | Admin only | Any state | Anonymise PII, keep record for audit trail |

**Why soft delete instead of cascade hard delete for the standard path:**

- Registration records are financial/historical artefacts. Orphaning a `RegistrationMember` would corrupt the registration history.
- Membership records may have fee history that must be preserved for accounting.
- Soft delete is the safest reversible action; the UI simply stops showing the member.

**Why not cascade delete everything:**

- Completed registrations represent payments and attendance records — deleting them breaks the audit trail.
- Memberships have associated fee payments that cannot be silently removed.

**Right to erasure (GDPR):**

- Admin-only endpoint anonymises PII fields in-place rather than deleting the row, preserving FK integrity.
- PII fields to clear: `FirstName`, `LastName`, `DateOfBirth`, `DocumentNumber`, `Email`, `Phone`, `MedicalNotes`, `Allergies`, `ProfilePhotoUrl`.
- Replace name fields with `"[deleted]"` and a fixed sentinel DOB (e.g. `1900-01-01`); set all nullable PII to `null`.

---

## Implementation Plan

### Step 1 — Add `DeletedAt` to `FamilyMember`

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`

Add field to `FamilyMember`:

```csharp
public DateTime? DeletedAt { get; set; }
```

**File**: `src/Abuvi.API/Data/Configurations/FamilyMemberConfiguration.cs`

No extra configuration needed; EF Core handles nullable DateTime.

**Migration**: Add column `deleted_at TIMESTAMPTZ NULL` to `family_members`.

---

### Step 2 — Apply soft-delete filter globally

**File**: `src/Abuvi.API/Data/AbuviDbContext.cs`

Add a global query filter so soft-deleted members are automatically excluded:

```csharp
modelBuilder.Entity<FamilyMember>().HasQueryFilter(m => m.DeletedAt == null);
```

> All existing queries automatically ignore soft-deleted members. No query changes needed.

---

### Step 3 — Rewrite `DeleteFamilyMemberAsync` service logic

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

New logic:

1. Load member → 404 if not found.
2. Guard: cannot delete representative → 409.
3. Check `HasAnyRegistrationsAsync(memberId)` (any status) OR `HasMembershipAsync(memberId)`:
   - **True** → soft delete: set `DeletedAt = DateTime.UtcNow`, save changes, return 204.
   - **False** → hard delete: `ExecuteDeleteAsync`, return 204.
4. Log the action (soft or hard) with the member name and family unit ID.

New repository queries needed (Step 4).

---

### Step 4 — Add repository methods

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`

```csharp
// Returns true if member has any RegistrationMember row (any status)
public async Task<bool> MemberHasAnyRegistrationsAsync(Guid memberId, CancellationToken ct)

// Returns true if member has any Membership row
public async Task<bool> MemberHasMembershipAsync(Guid memberId, CancellationToken ct)

// Soft deletes the member
public async Task SoftDeleteFamilyMemberAsync(Guid id, CancellationToken ct)
```

The existing `MemberHasActiveRegistrationsAsync` is still used for the Admin/Board active-registration guard (keep it).

---

### Step 5 — Right-to-erasure endpoint (Admin only)

**Endpoint**: `DELETE /api/family-units/{familyUnitId}/members/{memberId}/pii`

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`

- Authorization: Admin/Board only.
- Anonymises PII in the `FamilyMember` row:
  - `FirstName = "[deleted]"`, `LastName = "[deleted]"`, `DateOfBirth = new DateOnly(1900, 1, 1)`
  - `DocumentNumber = null`, `Email = null`, `Phone = null`, `MedicalNotes = null`, `Allergies = null`, `ProfilePhotoUrl = null`
- Sets `DeletedAt = DateTime.UtcNow` (so the record is hidden from normal queries).
- Clears `UserId` link (set to null).
- Returns 204.

**Service method**: `AnonymiseFamilyMemberAsync(Guid familyUnitId, Guid memberId, ClaimsPrincipal user, CancellationToken ct)`

---

### Step 6 — Update existing endpoint response codes

**File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`

The `DELETE /{familyUnitId}/members/{memberId}` endpoint now returns:

- 204 — Member deleted (hard or soft).
- 403 — Caller is not representative or admin/board.
- 404 — Member not found.
- 409 — Member is the family representative (cannot delete self-record).

Remove the 409 for "active registrations" from the non-admin path — the service now handles it transparently via soft delete.

---

### Step 7 — Unit tests

**File**: `src/Abuvi.Tests/Unit/Features/FamilyUnits/DeleteFamilyMemberTests.cs`

Test cases:

- `Delete_NoHistory_HardDeletes` — member with no registrations/memberships → hard delete.
- `Delete_WithCancelledRegistration_SoftDeletes` — member with Cancelled registration → soft delete.
- `Delete_WithCompletedRegistration_SoftDeletes` — member with Completed registration → soft delete.
- `Delete_WithActiveMembership_SoftDeletes` — member with Membership row → soft delete.
- `Delete_Representative_Returns409` — cannot delete representative.
- `Delete_ActiveRegistration_AdminBoard_Returns409` — admin guard still blocks Pending/Confirmed.
- `Anonymise_AdminOnly_ClearsPii` — right-to-erasure anonymises all PII fields.
- `Anonymise_NonAdmin_Returns403`.

---

## Files to modify

| File | Change |
|---|---|
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs` | Add `DeletedAt` to `FamilyMember` |
| `src/Abuvi.API/Data/AbuviDbContext.cs` | Add global query filter for soft delete |
| `src/Abuvi.API/Data/Configurations/FamilyMemberConfiguration.cs` | (optional) explicit column name for `deleted_at` |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs` | Add `MemberHasAnyRegistrationsAsync`, `MemberHasMembershipAsync`, `SoftDeleteFamilyMemberAsync` |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs` | Rewrite `DeleteFamilyMemberAsync`, add `AnonymiseFamilyMemberAsync` |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Update delete endpoint docs; add `DELETE .../pii` endpoint |
| `src/Abuvi.Tests/Unit/Features/FamilyUnits/DeleteFamilyMemberTests.cs` | New test file (TDD first) |
| EF migration | Add `deleted_at` column to `family_members` |

---

## Non-functional requirements

- **Security**: The anonymisation endpoint must be gated to Admin/Board roles only. Validate that `familyUnitId` matches the member to prevent cross-family attacks.
- **Data integrity**: Global query filter must be applied before all reads. Consider using `IgnoreQueryFilters()` only in admin reporting queries where soft-deleted records are intentionally visible.
- **Performance**: Index on `(id, deleted_at)` is not strictly needed — the PK lookup is used — but consider a partial index `WHERE deleted_at IS NOT NULL` for future soft-delete audits.
- **Audit trail**: Log both soft deletes and anonymisations with the acting user's ID, timestamp, and family unit ID.
- **Migration**: Non-destructive. The new `deleted_at` column is nullable and defaults to `NULL`, so existing rows are unaffected.

---

## Acceptance Criteria

- [ ] A family member with no registration or membership history is hard-deleted and cannot be retrieved.
- [ ] A family member with any past or present registration/membership is soft-deleted (hidden from API responses) without breaking existing FK references.
- [ ] Attempting to delete the family representative returns 409 with a clear message.
- [ ] An admin can invoke the right-to-erasure endpoint to anonymise all PII fields of any family member, including soft-deleted ones.
- [ ] Non-admin callers receive 403 on the right-to-erasure endpoint.
- [ ] No 500 errors are returned to the caller for FK constraint violations.
- [ ] All new paths are covered by unit tests (TDD).

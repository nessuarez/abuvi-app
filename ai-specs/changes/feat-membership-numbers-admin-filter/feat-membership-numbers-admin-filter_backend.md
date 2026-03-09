# Backend Implementation Plan: feat-membership-numbers-admin-filter

## Overview

Add sequential human-readable numbers for family units (número de familia socia) and individual members (número de socio/a), auto-assigned upon membership activation. Also add a membership status filter to the admin family units listing endpoint. Follows Vertical Slice Architecture — changes span the `FamilyUnits` and `Memberships` feature slices.

## Architecture Context

### Feature slices involved

- `src/Abuvi.API/Features/FamilyUnits/` — entity, DTOs, repository, service, endpoints, validator
- `src/Abuvi.API/Features/Memberships/` — entity, DTOs, repository, service, endpoints, validator
- `src/Abuvi.API/Data/Configurations/` — EF Core entity configurations

### Files to modify

| File | Action |
|---|---|
| `Features/FamilyUnits/FamilyUnitsModels.cs` | Add `FamilyNumber` to entity + DTOs |
| `Features/Memberships/MembershipsModels.cs` | Add `MemberNumber` to entity + DTOs |
| `Data/Configurations/FamilyUnitConfiguration.cs` | Add column + unique filtered index |
| `Data/Configurations/MembershipConfiguration.cs` | Add column + unique filtered index |
| `Features/FamilyUnits/FamilyUnitsRepository.cs` | Add number helpers + membership filter |
| `Features/Memberships/MembershipsRepository.cs` | Add number helpers |
| `Features/Memberships/MembershipsService.cs` | Auto-assign numbers on create/bulk |
| `Features/FamilyUnits/FamilyUnitsService.cs` | Pass filter + new UpdateFamilyNumber |
| `Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Add filter param + PUT family-number |
| `Features/Memberships/MembershipsEndpoints.cs` | Add PUT member-number |

### Files to create

| File | Purpose |
|---|---|
| `Features/FamilyUnits/UpdateFamilyNumberValidator.cs` | Validate FamilyNumber > 0 |
| `Features/Memberships/UpdateMemberNumberValidator.cs` | Validate MemberNumber > 0 |
| New migration file (auto-generated) | Schema changes |

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-membership-numbers-admin-filter-backend`
- **Implementation Steps**:
  1. Ensure on latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/feat-membership-numbers-admin-filter-backend`
  3. Verify: `git branch`

---

### Step 1: Update Entities — Add FamilyNumber and MemberNumber

#### Step 1a: FamilyUnit entity

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`
- **Action**: Add `FamilyNumber` property to `FamilyUnit` class

**Add after `RepresentativeUserId` property (line 10):**

```csharp
public int? FamilyNumber { get; set; }  // Assigned when first member gets membership activated
```

#### Step 1b: Membership entity

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsModels.cs`
- **Action**: Add `MemberNumber` property to `Membership` class

**Add after `IsActive` property (line 14):**

```csharp
public int? MemberNumber { get; set; }  // Assigned on membership activation
```

---

### Step 2: Update Request/Response DTOs

#### Step 2a: FamilyUnits DTOs

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`
- **Action**: Update all relevant DTOs and add new request DTO

**Add new request DTO (after `UpdateFamilyUnitRequest`, line 59):**

```csharp
public record UpdateFamilyNumberRequest(int FamilyNumber);
```

**Update `FamilyUnitResponse` (line 86) — add `int? FamilyNumber`:**

```csharp
public record FamilyUnitResponse(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    int? FamilyNumber,
    string? ProfilePhotoUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Update `FamilyUnitAdminProjection` (line 119) — add `int? FamilyNumber`:**

```csharp
public record FamilyUnitAdminProjection(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    string RepresentativeName,
    int? FamilyNumber,
    int MembersCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Update `FamilyUnitListItemResponse` (line 132) — add `int? FamilyNumber`:**

```csharp
public record FamilyUnitListItemResponse(
    Guid Id,
    string Name,
    Guid RepresentativeUserId,
    string RepresentativeName,
    int? FamilyNumber,
    int MembersCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Update `FamilyUnitExtensions.ToResponse()` (line 156) — include `FamilyNumber`:**

```csharp
public static FamilyUnitResponse ToResponse(this FamilyUnit unit)
    => new(
        unit.Id,
        unit.Name,
        unit.RepresentativeUserId,
        unit.FamilyNumber,
        unit.ProfilePhotoUrl,
        unit.CreatedAt,
        unit.UpdatedAt
    );
```

#### Step 2b: Memberships DTOs

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsModels.cs`
- **Action**: Update response DTO and add new request DTO

**Add new request DTO (after `CreateMembershipRequest`, line 57):**

```csharp
public record UpdateMemberNumberRequest(int MemberNumber);
```

**Update `MembershipResponse` (line 66) — add `int? MemberNumber`:**

```csharp
public record MembershipResponse(
    Guid Id,
    Guid FamilyMemberId,
    int? MemberNumber,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    IReadOnlyList<MembershipFeeResponse> Fees,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Update `MembershipExtensions.ToResponse()` (line 162) — include `MemberNumber`:**

```csharp
public static MembershipResponse ToResponse(this Membership membership)
    => new(
        membership.Id,
        membership.FamilyMemberId,
        membership.MemberNumber,
        membership.StartDate,
        membership.EndDate,
        membership.IsActive,
        membership.Fees.Select(f => f.ToResponse()).ToList(),
        membership.CreatedAt,
        membership.UpdatedAt
    );
```

---

### Step 3: Create FluentValidation Validators

#### Step 3a: UpdateFamilyNumberValidator

- **File**: `src/Abuvi.API/Features/FamilyUnits/UpdateFamilyNumberValidator.cs` (NEW)

```csharp
using FluentValidation;

namespace Abuvi.API.Features.FamilyUnits;

public class UpdateFamilyNumberValidator : AbstractValidator<UpdateFamilyNumberRequest>
{
    public UpdateFamilyNumberValidator()
    {
        RuleFor(x => x.FamilyNumber)
            .GreaterThan(0).WithMessage("El número de familia debe ser mayor a 0");
    }
}
```

#### Step 3b: UpdateMemberNumberValidator

- **File**: `src/Abuvi.API/Features/Memberships/UpdateMemberNumberValidator.cs` (NEW)

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Memberships;

public class UpdateMemberNumberValidator : AbstractValidator<UpdateMemberNumberRequest>
{
    public UpdateMemberNumberValidator()
    {
        RuleFor(x => x.MemberNumber)
            .GreaterThan(0).WithMessage("El número de socio/a debe ser mayor a 0");
    }
}
```

---

### Step 4: Update EF Core Configurations

#### Step 4a: FamilyUnitConfiguration

- **File**: `src/Abuvi.API/Data/Configurations/FamilyUnitConfiguration.cs`
- **Action**: Add `family_number` column with unique filtered index

**Add before the ProfilePhotoUrl configuration (before line 39):**

```csharp
// Family number: optional, unique when assigned
builder.Property(fu => fu.FamilyNumber)
    .HasColumnName("family_number");

builder.HasIndex(fu => fu.FamilyNumber)
    .IsUnique()
    .HasFilter("family_number IS NOT NULL");
```

#### Step 4b: MembershipConfiguration

- **File**: `src/Abuvi.API/Data/Configurations/MembershipConfiguration.cs`
- **Action**: Add `member_number` column with unique filtered index

**Add before the audit fields (before line 33):**

```csharp
// Member number: optional, unique when assigned
builder.Property(m => m.MemberNumber)
    .HasColumnName("member_number");

builder.HasIndex(m => m.MemberNumber)
    .IsUnique()
    .HasFilter("member_number IS NOT NULL");
```

---

### Step 5: Generate EF Core Migration

- **Action**: Run migration generation command from `src/Abuvi.API/` directory

```bash
dotnet ef migrations add AddFamilyAndMemberNumbers
```

- **Verify**: Check the generated migration creates:
  - `family_number` nullable int column on `family_units` table
  - `member_number` nullable int column on `memberships` table
  - Unique filtered indexes on both columns

---

### Step 6: Implement Repository Changes

#### Step 6a: FamilyUnitsRepository

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`

**Add to `IFamilyUnitsRepository` interface (after line 42):**

```csharp
// Family number operations
Task<int> GetNextFamilyNumberAsync(CancellationToken ct);
Task<bool> IsFamilyNumberTakenAsync(int familyNumber, Guid? excludeId, CancellationToken ct);
```

**Update `GetAllPagedAsync` interface signature — add `string? membershipStatus` parameter:**

```csharp
Task<(List<FamilyUnitAdminProjection> Items, int TotalCount)> GetAllPagedAsync(
    int page, int pageSize, string? search, string? sortBy, string? sortOrder,
    string? membershipStatus, CancellationToken ct);
```

**Add implementations to `FamilyUnitsRepository` class (before the `GetAllPagedAsync` method):**

```csharp
public async Task<int> GetNextFamilyNumberAsync(CancellationToken ct)
{
    var max = await db.FamilyUnits
        .Where(fu => fu.FamilyNumber != null)
        .MaxAsync(fu => (int?)fu.FamilyNumber, ct);
    return (max ?? 0) + 1;
}

public async Task<bool> IsFamilyNumberTakenAsync(int familyNumber, Guid? excludeId, CancellationToken ct)
{
    return await db.FamilyUnits
        .AnyAsync(fu => fu.FamilyNumber == familyNumber
            && (!excludeId.HasValue || fu.Id != excludeId.Value), ct);
}
```

**Update `GetAllPagedAsync` implementation — add `string? membershipStatus` parameter and filter logic:**

Add parameter to method signature. After the search filter block (after line 160), add:

```csharp
if (membershipStatus == "active")
{
    query = query.Where(x =>
        db.Memberships.Any(m => m.IsActive &&
            db.FamilyMembers.Any(fm => fm.FamilyUnitId == x.Id && fm.Id == m.FamilyMemberId)));
}
else if (membershipStatus == "none")
{
    query = query.Where(x =>
        !db.Memberships.Any(m => m.IsActive &&
            db.FamilyMembers.Any(fm => fm.FamilyUnitId == x.Id && fm.Id == m.FamilyMemberId)));
}
```

**Update the projection in `GetAllPagedAsync` to include `FamilyNumber`:**

In the anonymous type select (line 141-152), add `fu.FamilyNumber`.

In the `FamilyUnitAdminProjection` constructor (line 177-179), pass `x.FamilyNumber`.

#### Step 6b: MembershipsRepository

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsRepository.cs`

**Add to `IMembershipsRepository` interface (after line 18):**

```csharp
// Member number operations
Task<int> GetNextMemberNumberAsync(CancellationToken ct);
Task<bool> IsMemberNumberTakenAsync(int memberNumber, Guid? excludeId, CancellationToken ct);
```

**Add implementations to `MembershipsRepository` class:**

```csharp
public async Task<int> GetNextMemberNumberAsync(CancellationToken ct)
{
    var max = await db.Memberships
        .Where(m => m.MemberNumber != null)
        .MaxAsync(m => (int?)m.MemberNumber, ct);
    return (max ?? 0) + 1;
}

public async Task<bool> IsMemberNumberTakenAsync(int memberNumber, Guid? excludeId, CancellationToken ct)
{
    return await db.Memberships
        .AnyAsync(m => m.MemberNumber == memberNumber
            && (!excludeId.HasValue || m.Id != excludeId.Value), ct);
}
```

---

### Step 7: Implement Service Changes

#### Step 7a: MembershipsService — Auto-assign numbers on creation

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsService.cs`

**Update `CreateAsync` (line 10-39):**

After creating the `Membership` object (line 26-34), before `await repository.AddAsync`:

1. Assign `MemberNumber`:
```csharp
membership.MemberNumber = await repository.GetNextMemberNumberAsync(ct);
```

2. Check and assign `FamilyNumber` if needed:
```csharp
// Assign family number if this is the first membership for the family
var familyUnit = await familyUnitsRepository.GetFamilyUnitByIdAsync(familyMember.FamilyUnitId, ct);
if (familyUnit is not null && familyUnit.FamilyNumber is null)
{
    familyUnit.FamilyNumber = await familyUnitsRepository.GetNextFamilyNumberAsync(ct);
    await familyUnitsRepository.UpdateFamilyUnitAsync(familyUnit, ct);
}
```

**Important**: The `familyMember` variable already exists from line 16. Use `familyMember.FamilyUnitId` to load the family unit.

**Update `BulkActivateAsync` (line 41-91):**

After loading `familyUnit` (line 46), check if family number needs assignment:

```csharp
bool familyNumberAssigned = familyUnit.FamilyNumber is not null;
```

Inside the try block where each membership is created (line 71-80), before `await repository.AddAsync`:

```csharp
membership.MemberNumber = await repository.GetNextMemberNumberAsync(ct);

// Assign family number on first successful activation
if (!familyNumberAssigned)
{
    familyUnit.FamilyNumber = await familyUnitsRepository.GetNextFamilyNumberAsync(ct);
    await familyUnitsRepository.UpdateFamilyUnitAsync(familyUnit, ct);
    familyNumberAssigned = true;
}
```

**Note**: `familyUnitsRepository.GetFamilyUnitByIdAsync` uses `AsNoTracking`, so the family unit retrieved at line 46 won't be tracked. The `UpdateFamilyUnitAsync` method uses `db.FamilyUnits.Update()` which will attach+update — this matches the existing pattern.

#### Step 7b: MembershipsService — Add UpdateMemberNumberAsync

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsService.cs`
- **Action**: Add new method after `PayFeeAsync`

```csharp
public async Task<MembershipResponse> UpdateMemberNumberAsync(
    Guid membershipId,
    UpdateMemberNumberRequest request,
    CancellationToken ct)
{
    var membership = await repository.GetByIdAsync(membershipId, ct);
    if (membership is null)
        throw new NotFoundException(nameof(Membership), membershipId);

    // Check uniqueness
    var isTaken = await repository.IsMemberNumberTakenAsync(request.MemberNumber, membershipId, ct);
    if (isTaken)
        throw new BusinessRuleException($"El número de socio/a {request.MemberNumber} ya está en uso");

    membership.MemberNumber = request.MemberNumber;
    membership.UpdatedAt = DateTime.UtcNow;

    await repository.UpdateAsync(membership, ct);

    return membership.ToResponse();
}
```

#### Step 7c: FamilyUnitsService — Add UpdateFamilyNumberAsync and pass filter

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

**Update `GetAllFamilyUnitsAsync` (line 324-350):**

Add `string? membershipStatus` parameter to the method signature. Pass it to the repository call:

```csharp
public async Task<PagedFamilyUnitsResponse> GetAllFamilyUnitsAsync(
    int page, int pageSize, string? search, string? sortBy, string? sortOrder,
    string? membershipStatus, CancellationToken ct)
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var (items, totalCount) = await repository.GetAllPagedAsync(
        page, pageSize, search, sortBy, sortOrder, membershipStatus, ct);

    var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

    return new PagedFamilyUnitsResponse(
        Items: items.Select(p => new FamilyUnitListItemResponse(
            p.Id, p.Name, p.RepresentativeUserId,
            p.RepresentativeName, p.FamilyNumber, p.MembersCount, p.CreatedAt, p.UpdatedAt
        )).ToList(),
        TotalCount: totalCount,
        Page: page,
        PageSize: pageSize,
        TotalPages: totalPages
    );
}
```

**Add `UpdateFamilyNumberAsync` method in the Admin Operations region:**

```csharp
public async Task<FamilyUnitResponse> UpdateFamilyNumberAsync(
    Guid familyUnitId,
    UpdateFamilyNumberRequest request,
    CancellationToken ct)
{
    var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct);
    if (familyUnit is null)
        throw new NotFoundException(nameof(FamilyUnit), familyUnitId);

    // Check uniqueness
    var isTaken = await repository.IsFamilyNumberTakenAsync(request.FamilyNumber, familyUnitId, ct);
    if (isTaken)
        throw new BusinessRuleException($"El número de familia {request.FamilyNumber} ya está en uso");

    familyUnit.FamilyNumber = request.FamilyNumber;
    await repository.UpdateFamilyUnitAsync(familyUnit, ct);

    return familyUnit.ToResponse();
}
```

---

### Step 8: Create Minimal API Endpoints

#### Step 8a: FamilyUnitsEndpoints — Add membershipStatus filter + PUT family-number

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`

**Add new endpoint in the `adminGroup` (after `GetAllFamilyUnits` mapping, line 35):**

```csharp
adminGroup.MapPut("/{id:guid}/family-number", UpdateFamilyNumber)
    .WithName("UpdateFamilyNumber")
    .WithSummary("Update family number (Admin/Board only)")
    .AddEndpointFilter<ValidationFilter<UpdateFamilyNumberRequest>>()
    .Produces<ApiResponse<FamilyUnitResponse>>()
    .Produces(400)
    .Produces(404)
    .Produces(409);
```

**Update `GetAllFamilyUnits` handler (line 407) — add `membershipStatus` parameter:**

```csharp
private static async Task<IResult> GetAllFamilyUnits(
    FamilyUnitsService service,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? search = null,
    [FromQuery] string? sortBy = null,
    [FromQuery] string? sortOrder = null,
    [FromQuery] string? membershipStatus = null,
    CancellationToken ct = default)
{
    var result = await service.GetAllFamilyUnitsAsync(
        page, pageSize, search, sortBy, sortOrder, membershipStatus, ct);
    return TypedResults.Ok(ApiResponse<PagedFamilyUnitsResponse>.Ok(result));
}
```

**Add `UpdateFamilyNumber` handler method:**

```csharp
private static async Task<IResult> UpdateFamilyNumber(
    [FromRoute] Guid id,
    [FromBody] UpdateFamilyNumberRequest request,
    FamilyUnitsService service,
    CancellationToken ct)
{
    try
    {
        var result = await service.UpdateFamilyNumberAsync(id, request, ct);
        return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex)
    {
        return TypedResults.Conflict(ApiResponse<object>.Fail(ex.Message));
    }
}
```

#### Step 8b: MembershipsEndpoints — Add PUT member-number

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs`

**Add new endpoint group in `MapMembershipFeeEndpoints` (or create a separate admin group after line 106):**

```csharp
var adminGroup = app.MapGroup("/api/memberships/{membershipId:guid}")
    .WithTags("Memberships")
    .RequireAuthorization();

adminGroup.MapPut("/member-number", UpdateMemberNumber)
    .WithName("UpdateMemberNumber")
    .WithSummary("Update member number (Admin/Board only)")
    .AddEndpointFilter<ValidationFilter<UpdateMemberNumberRequest>>()
    .Produces<ApiResponse<MembershipResponse>>()
    .Produces(400)
    .Produces(404)
    .Produces(409);
```

**Add `UpdateMemberNumber` handler method:**

```csharp
private static async Task<IResult> UpdateMemberNumber(
    [FromRoute] Guid membershipId,
    [FromBody] UpdateMemberNumberRequest request,
    ClaimsPrincipal user,
    MembershipsService service,
    CancellationToken ct)
{
    var userRole = user.GetUserRole();
    var isAdminOrBoard = userRole == "Admin" || userRole == "Board";
    if (!isAdminOrBoard)
        return Results.Forbid();

    var result = await service.UpdateMemberNumberAsync(membershipId, request, ct);
    return Results.Ok(ApiResponse<MembershipResponse>.Ok(result));
}
```

**Register the new endpoint group**: Ensure `MapMembershipFeeEndpoints` (or the new admin group) is called in `Program.cs`. Check if the existing registration already covers this by reading the endpoint mapping section.

---

### Step 9: Write Unit Tests

- **File**: Create test files following existing test patterns (xUnit + FluentAssertions + NSubstitute)

#### Test categories:

**MembershipsService Tests:**

1. **CreateAsync_AssignsMemberNumber**: Verify `MemberNumber` is set to `GetNextMemberNumberAsync()` result
2. **CreateAsync_AssignsFamilyNumber_WhenFirstMembershipInFamily**: Verify `FamilyNumber` is set on the family unit when it was null
3. **CreateAsync_DoesNotReassignFamilyNumber_WhenAlreadySet**: Verify existing `FamilyNumber` is preserved
4. **BulkActivateAsync_AssignsMemberNumbers**: Each activated membership gets a unique `MemberNumber`
5. **BulkActivateAsync_AssignsFamilyNumber_OnceForFamily**: `FamilyNumber` is assigned only once, not per member
6. **UpdateMemberNumberAsync_Success**: Updates number when unique
7. **UpdateMemberNumberAsync_ThrowsWhenDuplicate**: Throws `BusinessRuleException` when number is taken
8. **UpdateMemberNumberAsync_ThrowsWhenNotFound**: Throws `NotFoundException`

**FamilyUnitsService Tests:**

9. **UpdateFamilyNumberAsync_Success**: Updates number when unique
10. **UpdateFamilyNumberAsync_ThrowsWhenDuplicate**: Throws `BusinessRuleException`
11. **UpdateFamilyNumberAsync_ThrowsWhenNotFound**: Throws `NotFoundException`
12. **GetAllFamilyUnitsAsync_FiltersByMembershipStatus_Active**: Returns only families with active memberships
13. **GetAllFamilyUnitsAsync_FiltersByMembershipStatus_None**: Returns only families without active memberships
14. **GetAllFamilyUnitsAsync_NoFilter_ReturnsAll**: Default behavior unchanged

---

### Step 10: Update Technical Documentation

- **Action**: Review and update documentation

**Implementation Steps:**

1. **Update `ai-specs/specs/data-model.md`**:
   - Add `familyNumber` field to `FamilyUnit` entity: `int?`, optional, unique when not null, auto-assigned on first membership activation
   - Add `memberNumber` field to `Membership` entity: `int?`, optional, unique when not null, auto-assigned on creation
   - Document uniqueness constraints

2. **Verify OpenAPI spec**: The auto-generated OpenAPI spec should reflect the new query parameter `membershipStatus` and the two new PUT endpoints. Run the app and verify at `/swagger`.

3. **Update enriched spec**: Mark completed acceptance criteria in `ai-specs/changes/feat-membership-numbers-admin-filter_enriched.md`

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Update entities (FamilyUnit.FamilyNumber, Membership.MemberNumber)
3. Step 2 — Update DTOs (request + response records)
4. Step 3 — Create validators
5. Step 4 — Update EF Core configurations
6. Step 5 — Generate migration
7. Step 6 — Implement repository changes
8. Step 7 — Implement service changes
9. Step 8 — Create endpoints
10. Step 9 — Write unit tests
11. Step 10 — Update documentation

## Testing Checklist

- [ ] `dotnet build` compiles without errors
- [ ] `dotnet test` — all tests pass
- [ ] New tests cover: auto-assignment on create, auto-assignment on bulk, family number only on first, update with uniqueness, filter by membership status
- [ ] Manual verification: create membership → check response includes `memberNumber` and family unit has `familyNumber`
- [ ] Manual verification: GET /api/family-units?membershipStatus=none returns only non-member families
- [ ] Manual verification: PUT /api/family-units/{id}/family-number with duplicate returns 409
- [ ] Manual verification: PUT /api/memberships/{id}/member-number with duplicate returns 409

## Error Response Format

| Scenario | HTTP Status | Response |
|---|---|---|
| Update number — success | 200 | `ApiResponse<T>.Ok(data)` |
| Update number — not found | 404 | `ApiResponse<object>.NotFound(message)` |
| Update number — duplicate | 409 | `ApiResponse<object>.Fail(message)` |
| Update number — invalid (< 1) | 400 | Validation error from FluentValidation |
| Update number — forbidden (not Admin/Board) | 403 | Forbid |

## Dependencies

- No new NuGet packages required
- EF Core migration command: `dotnet ef migrations add AddFamilyAndMemberNumbers`
- Apply migration: `dotnet ef database update`

## Notes

- **Spanish for user-facing content**: All error messages and validation messages in Spanish (per backend-standards.mdc)
- **Concurrency**: `MAX + 1` combined with unique DB constraint is sufficient for this low-traffic scenario. If a concurrent collision occurs, the unique constraint will reject the second insert — acceptable behavior.
- **AsNoTracking pattern**: The existing repository uses `AsNoTracking` for reads. When updating `FamilyNumber` from `MembershipsService`, we must use `UpdateFamilyUnitAsync` which calls `db.FamilyUnits.Update()` to re-attach the entity. This is the existing pattern in the codebase.
- **No changes to Program.cs**: Validators are auto-discovered by FluentValidation's assembly scanning. Services and repositories are already registered.

## Next Steps After Implementation

1. Implement the frontend ticket (`feat-membership-numbers-admin-filter_frontend`) to display filter, family number column, and member numbers in the UI
2. Consider adding a "Nº Socio/a" column to the admin family detail view (if/when that view is implemented)

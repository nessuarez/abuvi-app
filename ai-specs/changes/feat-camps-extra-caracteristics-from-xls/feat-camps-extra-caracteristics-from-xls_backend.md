# Backend Implementation Plan: feat-camps-extra-caracteristics-from-xls — Add Extra Camp Characteristics

## Overview

Extend the `Camp` entity with 15 new database columns (contact info, pricing, ABUVI internal tracking, audit metadata), extend the `AccommodationCapacity` JSON with 11 new fields (capacity descriptions + facility flags), and introduce two new entities: `CampObservation` (append-only notes with authorship) and `CampAuditLog` (automatic field-level change tracking). This follows Vertical Slice Architecture — all changes are within the `Features/Camps/` slice, plus EF configurations and DI registration.

**Scope:** Data model + API endpoints + audit logic + observations service. No CSV file importer — data will be entered manually through the API/UI.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`
**EF configurations:** `src/Abuvi.API/Data/Configurations/`
**Tests:** `src/Abuvi.Tests/Unit/Features/Camps/`

### Files to Create

| File | Purpose |
|------|---------|
| `Data/Configurations/CampObservationConfiguration.cs` | EF config for `camp_observations` table |
| `Data/Configurations/CampAuditLogConfiguration.cs` | EF config for `camp_audit_logs` table |
| `Features/Camps/ICampObservationsService.cs` | Interface for observations service |
| `Features/Camps/CampObservationsService.cs` | Add/list observations |
| `Features/Camps/ICampObservationsRepository.cs` | Interface for observations repository |
| `Features/Camps/CampObservationsRepository.cs` | EF Core implementation |
| `src/Abuvi.Tests/Unit/Features/Camps/CampObservationsServiceTests.cs` | Unit tests |

### Files to Modify

| File | Change |
|------|--------|
| `Features/Camps/CampsModels.cs` | New Camp fields, new entities (`CampObservation`, `CampAuditLog`), extended `AccommodationCapacity`, updated DTOs, new DTOs |
| `Data/Configurations/CampConfiguration.cs` | New column mappings, indexes, FK relationships |
| `Data/AbuviDbContext.cs` | Add `CampObservations` and `CampAuditLogs` DbSets |
| `Features/Camps/ICampsRepository.cs` | Add `AddAuditLogsAsync`, `GetAuditLogAsync` |
| `Features/Camps/CampsRepository.cs` | Implement new methods |
| `Features/Camps/CampsService.cs` | Add `IUsersRepository` dep, accept `updatedByUserId` in `UpdateAsync`, implement `BuildAuditEntries`, validate `AbuviManagedByUserId`, map new fields |
| `Features/Camps/CampsValidators.cs` or `UpdateCampValidator.cs` | Add validation rules for new fields |
| `Features/Camps/CampsEndpoints.cs` | Pass userId to `UpdateAsync`; add 3 new endpoints (observations + audit log) |
| `Program.cs` | Register new services |
| `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs` | Update `UpdateAsync` calls for new signature, add audit log tests |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the feature branch before any code changes
- **Branch Naming**: `feature/feat-camps-extra-caracteristics-from-xls-backend`
- **Implementation Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-camps-extra-caracteristics-from-xls-backend`
  3. `git branch` — verify you are on the new branch
- **Note**: If already on a branch named after the ticket ID (without `-backend`), create a new one with the `-backend` suffix to separate concerns

---

### Step 1: Data Model Changes — `CampsModels.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add new scalar properties and navigation properties to `Camp`, create two new entity classes, extend `AccommodationCapacity`, update and add DTOs

#### 1a — New Scalar Properties on `Camp`

Add after existing properties:

```csharp
// Contact info
public string? Province { get; set; }
public string? ContactEmail { get; set; }
public string? ContactPerson { get; set; }      // "Nombre" CSV column
public string? ContactCompany { get; set; }     // "Empresa" CSV column
public string? SecondaryWebsiteUrl { get; set; }

// Pricing
public decimal? BasePrice { get; set; }         // PRECIO — catalogue/reference price
public bool? VatIncluded { get; set; }          // IVA: true = Si, false = No, null = unknown

// ABUVI internal tracking
public int? ExternalSourceId { get; set; }
public Guid? AbuviManagedByUserId { get; set; } // FK → User with Board role
public string? AbuviContactedAt { get; set; }
public string? AbuviPossibility { get; set; }
public string? AbuviLastVisited { get; set; }
public bool? AbuviHasDataErrors { get; set; }

// Audit
public Guid? LastModifiedByUserId { get; set; }
```

#### 1b — New Navigation Properties on `Camp`

```csharp
public User? AbuviManagedByUser { get; set; }
public ICollection<CampObservation> Observations { get; set; } = new List<CampObservation>();
public ICollection<CampAuditLog> AuditLogs { get; set; } = new List<CampAuditLog>();
```

**Important**: The navigation uses `User` (not `ApplicationUser`) — check the actual class name in `UsersModels.cs`. The project uses `User` with `UserRole` enum (`Admin`, `Board`, `Member`).

#### 1c — New `CampObservation` Entity

```csharp
public class CampObservation
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Season { get; set; }           // "2023", "2024", "2025", "2025/2026", null
    public Guid? CreatedByUserId { get; set; }    // null = system-created
    public DateTime CreatedAt { get; set; }
    public Camp Camp { get; set; } = null!;
}
```

#### 1d — New `CampAuditLog` Entity

```csharp
public class CampAuditLog
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
    public Camp Camp { get; set; } = null!;
}
```

#### 1e — Extend `AccommodationCapacity`

Add to the existing `AccommodationCapacity` class:

```csharp
// Capacity descriptions (raw text from spreadsheet)
public int? TotalCapacity { get; set; }
public string? RoomsDescription { get; set; }
public string? BungalowsDescription { get; set; }
public string? TentsDescription { get; set; }
public string? TentAreaDescription { get; set; }
public int? ParkingSpots { get; set; }

// Facility flags
public bool? HasAdaptedMenu { get; set; }
public bool? HasEnclosedDiningRoom { get; set; }
public bool? HasSwimmingPool { get; set; }
public bool? HasSportsCourt { get; set; }
public bool? HasForestArea { get; set; }
```

These serialize into the existing `accommodation_capacity_json` column — no new DB column needed. All new properties are nullable, so existing records deserialize safely.

#### 1f — Update `UpdateCampRequest`

Add new optional parameters (with defaults) after existing parameters:

```csharp
// New fields — add after AccommodationCapacity parameter
string? Province = null,
string? ContactEmail = null,
string? ContactPerson = null,
string? ContactCompany = null,
string? SecondaryWebsiteUrl = null,
decimal? BasePrice = null,
bool? VatIncluded = null,
Guid? AbuviManagedByUserId = null,
string? AbuviContactedAt = null,
string? AbuviPossibility = null,
string? AbuviLastVisited = null,
bool? AbuviHasDataErrors = null
```

#### 1g — Update `CampDetailResponse`

Add new fields after existing parameters:

```csharp
string? Province,
string? ContactEmail,
string? ContactPerson,
string? ContactCompany,
string? SecondaryWebsiteUrl,
decimal? BasePrice,
bool? VatIncluded,
int? ExternalSourceId,
Guid? AbuviManagedByUserId,
string? AbuviManagedByUserName,       // resolved from FK
string? AbuviContactedAt,
string? AbuviPossibility,
string? AbuviLastVisited,
bool? AbuviHasDataErrors,
Guid? LastModifiedByUserId,
IReadOnlyList<CampObservationResponse> Observations
```

**Note**: AuditLogs are NOT included in CampDetailResponse — they have their own endpoint.

#### 1h — New DTOs

```csharp
public record AddCampObservationRequest(string Text, string? Season);

public record CampObservationResponse(
    Guid Id, string Text, string? Season, Guid? CreatedByUserId, DateTime CreatedAt);

public record CampAuditLogResponse(
    Guid Id, string FieldName, string? OldValue, string? NewValue,
    Guid ChangedByUserId, DateTime ChangedAt);
```

---

### Step 2: EF Core Configurations

#### 2a — Update `CampConfiguration.cs`

- **File**: `src/Abuvi.API/Data/Configurations/CampConfiguration.cs`
- **Action**:
  1. Add column mappings for all 15 new Camp columns (snake_case):

     ```csharp
     builder.Property(c => c.Province).HasMaxLength(100).HasColumnName("province");
     builder.Property(c => c.ContactEmail).HasMaxLength(200).HasColumnName("contact_email");
     builder.Property(c => c.ContactPerson).HasMaxLength(200).HasColumnName("contact_person");
     builder.Property(c => c.ContactCompany).HasMaxLength(200).HasColumnName("contact_company");
     builder.Property(c => c.SecondaryWebsiteUrl).HasMaxLength(500).HasColumnName("secondary_website_url");
     builder.Property(c => c.BasePrice).HasPrecision(10, 2).HasColumnName("base_price");
     builder.Property(c => c.VatIncluded).HasColumnName("vat_included");
     builder.Property(c => c.ExternalSourceId).HasColumnName("external_source_id");
     builder.HasIndex(c => c.ExternalSourceId).HasDatabaseName("ix_camps_external_source_id");
     builder.Property(c => c.AbuviManagedByUserId).HasColumnName("abuvi_managed_by_user_id");
     builder.HasOne(c => c.AbuviManagedByUser)
         .WithMany()
         .HasForeignKey(c => c.AbuviManagedByUserId)
         .OnDelete(DeleteBehavior.SetNull);
     builder.Property(c => c.AbuviContactedAt).HasMaxLength(100).HasColumnName("abuvi_contacted_at");
     builder.Property(c => c.AbuviPossibility).HasMaxLength(100).HasColumnName("abuvi_possibility");
     builder.Property(c => c.AbuviLastVisited).HasMaxLength(200).HasColumnName("abuvi_last_visited");
     builder.Property(c => c.AbuviHasDataErrors).HasColumnName("abuvi_has_data_errors");
     builder.Property(c => c.LastModifiedByUserId).HasColumnName("last_modified_by_user_id");
     ```

  2. Add relationship configurations:

     ```csharp
     builder.HasMany(c => c.Observations)
         .WithOne(o => o.Camp)
         .HasForeignKey(o => o.CampId)
         .OnDelete(DeleteBehavior.Cascade);
     builder.HasMany(c => c.AuditLogs)
         .WithOne(a => a.Camp)
         .HasForeignKey(a => a.CampId)
         .OnDelete(DeleteBehavior.Cascade);
     ```

#### 2b — Create `CampObservationConfiguration.cs`

- **File**: `src/Abuvi.API/Data/Configurations/CampObservationConfiguration.cs`
- Table: `camp_observations`, columns in snake_case
- Index on `camp_id`
- `text` column: max 4000, required
- `created_at`: default `NOW()`

#### 2c — Create `CampAuditLogConfiguration.cs`

- **File**: `src/Abuvi.API/Data/Configurations/CampAuditLogConfiguration.cs`
- Table: `camp_audit_logs`, columns in snake_case
- Index on `camp_id`
- Composite index on `(camp_id, changed_at)`
- `changed_at`: default `NOW()`

#### 2d — Update `AbuviDbContext.cs`

- **File**: `src/Abuvi.API/Data/AbuviDbContext.cs`
- Add two new DbSets:

  ```csharp
  public DbSet<CampObservation> CampObservations => Set<CampObservation>();
  public DbSet<CampAuditLog> CampAuditLogs => Set<CampAuditLog>();
  ```

---

### Step 3: Repository Changes

#### 3a — Update `ICampsRepository.cs` + `CampsRepository.cs`

Add two new methods:

```csharp
Task AddAuditLogsAsync(IEnumerable<CampAuditLog> entries, CancellationToken ct = default);
Task<List<CampAuditLog>> GetAuditLogAsync(Guid campId, CancellationToken ct = default);
```

Implementation:

- `AddAuditLogsAsync`: `_context.CampAuditLogs.AddRange(entries); await _context.SaveChangesAsync(ct);`
- `GetAuditLogAsync`: query `CampAuditLogs` where `CampId == campId`, ordered by `ChangedAt DESC`, `AsNoTracking()`

#### 3b — Create `ICampObservationsRepository.cs` + `CampObservationsRepository.cs`

```csharp
public interface ICampObservationsRepository
{
    Task<CampObservation> AddAsync(CampObservation observation, CancellationToken ct = default);
    Task<List<CampObservation>> GetByCampIdAsync(Guid campId, CancellationToken ct = default);
    Task<bool> CampExistsAsync(Guid campId, CancellationToken ct = default);
}
```

Implementation:

- `AddAsync`: add + save
- `GetByCampIdAsync`: query where `CampId`, ordered by `CreatedAt DESC`, `AsNoTracking()`
- `CampExistsAsync`: `_context.Camps.AnyAsync(c => c.Id == campId, ct)`

---

### Step 4: Write Failing Audit Tests — TDD Red Phase

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`
- **Action**: First update existing tests, then write new failing tests

#### 4a — Update Existing Tests

All existing `UpdateAsync` test calls must be updated:

```csharp
// OLD
await _sut.UpdateAsync(campId, request, ct);

// NEW — add a userId parameter
var userId = Guid.NewGuid();
await _sut.UpdateAsync(campId, request, userId, ct);
```

Add `IUsersRepository` mock to the test class setup (even though existing tests don't use it, the constructor will require it).

#### 4b — New Audit Log Test Cases

Write these as failing tests first:

```
UpdateAsync_WhenBasePriceChanges_CreatesAuditLogEntry
UpdateAsync_WhenIsActiveChanges_CreatesAuditLogEntry
UpdateAsync_WhenNoTrackedFieldChanges_DoesNotCreateAuditLog
UpdateAsync_WhenMultipleTrackedFieldsChange_CreatesOneEntryPerField
UpdateAsync_AlwaysSetsLastModifiedByUserId
UpdateAsync_WhenCampNotFound_ReturnsNull
UpdateAsync_WhenAbuviManagedByUserIdChanges_CreatesAuditLogEntry
UpdateAsync_WhenAbuviManagedByUserIdIsValidBoardUser_Succeeds
UpdateAsync_WhenAbuviManagedByUserIdIsNotBoardUser_ThrowsBusinessRuleException
UpdateAsync_WhenAbuviManagedByUserIdDoesNotExist_ThrowsBusinessRuleException
UpdateAsync_WhenAbuviManagedByUserIdIsNull_ClearsAssignment
```

**Key testing details**:

- Mock `_campsRepository.AddAuditLogsAsync()` to capture the audit entries passed
- Use `Arg.Is<IEnumerable<CampAuditLog>>(...)` to verify correct field names, old/new values
- For `AbuviManagedByUserId` validation: mock `_usersRepository.GetByIdAsync()` to return a `User` with the appropriate `Role` (or `null` for not found)
- `UserRole` is an **enum** (`Admin`, `Board`, `Member`) — validate using `user.Role`

**9 tracked fields**: `BasePrice`, `VatIncluded`, `AbuviPossibility`, `AbuviLastVisited`, `AbuviContactedAt`, `AbuviManagedByUserId`, `IsActive`, `ContactPerson`, `ContactEmail`

---

### Step 5: Implement Audit Logic — TDD Green Phase

- **File**: `src/Abuvi.API/Features/Camps/CampsService.cs`
- **Action**: Make all failing audit tests pass

#### 5a — Add `IUsersRepository` Dependency

Add to the primary constructor:

```csharp
public class CampsService(
    ICampsRepository repository,
    IGooglePlacesSyncService googlePlacesSync,
    IUsersRepository usersRepository) : ICampsService
```

#### 5b — Update `UpdateAsync` Signature

```csharp
public async Task<CampDetailResponse?> UpdateAsync(
    Guid id, UpdateCampRequest request, Guid updatedByUserId, CancellationToken ct = default)
```

#### 5c — Add `AbuviManagedByUserId` Validation

Before applying changes:

```csharp
if (request.AbuviManagedByUserId.HasValue)
{
    var managedByUser = await usersRepository.GetByIdAsync(request.AbuviManagedByUserId.Value, ct);
    if (managedByUser is null)
        throw new BusinessRuleException("AbuviManagedByUserId references a non-existent user.");
    if (managedByUser.Role != UserRole.Board && managedByUser.Role != UserRole.Admin)
        throw new BusinessRuleException("AbuviManagedByUserId must reference a user with Board or Admin role.");
}
```

#### 5d — Implement `BuildAuditEntries`

Private static method comparing old vs new values for the 9 tracked fields. Returns `List<CampAuditLog>`.

#### 5e — Apply New Fields + Save Audit Logs

- Map all new request fields to camp entity
- Set `camp.LastModifiedByUserId = updatedByUserId`
- After `repository.UpdateAsync(camp, ct)`, if `auditEntries.Count > 0`, call `repository.AddAuditLogsAsync(auditEntries, ct)`

#### 5f — Update `MapToCampDetailResponse`

Map all new fields. For observations, pass an empty list from UpdateAsync (the GET path can include them by loading via `ICampObservationsRepository`).

**Important**: Also update the `ICampsService` interface to match the new `UpdateAsync` signature.

---

### Step 6: Write Failing Observation Tests — TDD Red Phase

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampObservationsServiceTests.cs` (new)
- **Action**: Write failing tests

```
AddObservationAsync_WhenCampExists_CreatesAndReturnsObservation
AddObservationAsync_WhenCampDoesNotExist_ThrowsNotFoundException
GetObservationsAsync_ReturnsObservationsOrderedByCreatedAtDesc
```

Test class setup:

```csharp
private readonly ICampObservationsRepository _repository = Substitute.For<ICampObservationsRepository>();
private readonly CampObservationsService _sut;

public CampObservationsServiceTests()
    => _sut = new CampObservationsService(_repository);
```

---

### Step 7: Implement `CampObservationsService` — TDD Green Phase

- **File**: `src/Abuvi.API/Features/Camps/ICampObservationsService.cs` (new)
- **File**: `src/Abuvi.API/Features/Camps/CampObservationsService.cs` (new)
- **Action**: Make all failing observation tests pass

```csharp
public interface ICampObservationsService
{
    Task<CampObservationResponse> AddAsync(
        Guid campId, AddCampObservationRequest request, Guid userId, CancellationToken ct = default);
    Task<List<CampObservationResponse>> GetByCampIdAsync(Guid campId, CancellationToken ct = default);
}
```

Implementation:

- Check camp exists via `repository.CampExistsAsync()` → throw `NotFoundException` if not
- Create `CampObservation` entity, save via `repository.AddAsync()`
- Map to `CampObservationResponse`

---

### Step 8: Update Validators

- **File**: `src/Abuvi.API/Features/Camps/CampsValidators.cs` (or `UpdateCampValidator.cs`)
- **Action**: Add validation for new fields in `UpdateCampRequest`

Rules:

- `ContactEmail`: valid email format when provided (`.EmailAddress()`)
- `SecondaryWebsiteUrl`: max length 500
- `BasePrice`: `>= 0` when provided
- `Province`: max length 100
- `ContactPerson`: max length 200
- `ContactCompany`: max length 200
- `AbuviContactedAt`: max length 100
- `AbuviPossibility`: max length 100
- `AbuviLastVisited`: max length 200

New validator for `AddCampObservationRequest`:

- `Text`: required, max 4000 chars
- `Season`: max 20 chars when provided

---

### Step 9: Update `CampsEndpoints.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`
- **Action**: Update PUT handler, add new endpoint groups

#### 9a — Update `PUT /api/camps/{id}` Handler

Extract `userId` from `ClaimsPrincipal` and pass to `UpdateAsync`:

```csharp
var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
var result = await service.UpdateAsync(id, request, userId, ct);
```

#### 9b — Add Observation Endpoints (Board+)

```
POST /api/camps/{campId}/observations   → ICampObservationsService.AddAsync
GET  /api/camps/{campId}/observations   → ICampObservationsService.GetByCampIdAsync
```

Register as a new `MapGroup` with appropriate authorization (Board+).

#### 9c — Add Audit Log Endpoint (Admin only)

```
GET /api/camps/{campId}/audit-log   → ICampsRepository.GetAuditLogAsync
```

Map results to `CampAuditLogResponse`. Restrict to Admin role.

---

### Step 10: Register Services in `Program.cs`

- **File**: `src/Abuvi.API/Program.cs`
- **Action**: Add DI registrations

```csharp
builder.Services.AddScoped<ICampObservationsRepository, CampObservationsRepository>();
builder.Services.AddScoped<ICampObservationsService, CampObservationsService>();
```

- `IUsersRepository` and `ICampsRepository` are already registered
- FluentValidation auto-registration should pick up new validators

---

### Step 11: Fix Downstream References

- **Action**: Search the codebase for callers of `CampsService.UpdateAsync` with the old 2-parameter signature and update them
- Check `RegistrationsService.cs` and any other files
- Check test files that call `UpdateAsync`

---

### Step 12: Build and Run All Tests

- **Action**: Verify compilation and test results
- `dotnet build src/Abuvi.API`
- `dotnet test src/Abuvi.Tests`
- Fix any compilation errors or test failures

---

### Step 13: Update Technical Documentation

- **Action**: Review and update relevant documentation files
- **Implementation Steps**:
  1. Update `ai-specs/specs/data-model.md` (if exists) with:
     - 15 new `camps` columns
     - New `camp_observations` table
     - New `camp_audit_logs` table
     - Extended `AccommodationCapacity` JSON fields
  2. Update `ai-specs/specs/api-spec.yml` (if exists) with 3 new endpoints + updated `PUT /api/camps/{id}` signature
- **Notes**: This step is MANDATORY before considering implementation complete

---

## Implementation Order

1. **Step 0** — Create feature branch
2. **Step 1** — Data model changes (`CampsModels.cs`)
3. **Step 2** — EF Core configurations + DbContext
4. **Step 3** — Repository changes (audit log + observations)
5. **Step 4** — [TDD Red] Write failing audit tests in `CampsServiceTests.cs`
6. **Step 5** — [TDD Green] Implement audit logic in `CampsService.cs`
7. **Step 6** — [TDD Red] Write failing observation tests
8. **Step 7** — [TDD Green] Implement `CampObservationsService`
9. **Step 8** — Update validators
10. **Step 9** — Update `CampsEndpoints.cs` (PUT userId + 3 new endpoints)
11. **Step 10** — Register services in `Program.cs`
12. **Step 11** — Fix downstream references
13. **Step 12** — Build and run all tests
14. **Step 13** — Update technical documentation

---

## Testing Checklist

### Unit Tests — `CampsServiceTests.cs`

- [ ] All existing `UpdateAsync` tests pass with updated signature (3 params → 4 params)
- [ ] `UpdateAsync_WhenBasePriceChanges_CreatesAuditLogEntry`
- [ ] `UpdateAsync_WhenIsActiveChanges_CreatesAuditLogEntry`
- [ ] `UpdateAsync_WhenNoTrackedFieldChanges_DoesNotCreateAuditLog`
- [ ] `UpdateAsync_WhenMultipleTrackedFieldsChange_CreatesOneEntryPerField`
- [ ] `UpdateAsync_AlwaysSetsLastModifiedByUserId`
- [ ] `UpdateAsync_WhenCampNotFound_ReturnsNull`
- [ ] `UpdateAsync_WhenAbuviManagedByUserIdChanges_CreatesAuditLogEntry`
- [ ] `UpdateAsync_WhenAbuviManagedByUserIdIsValidBoardUser_Succeeds`
- [ ] `UpdateAsync_WhenAbuviManagedByUserIdIsNotBoardUser_ThrowsBusinessRuleException`
- [ ] `UpdateAsync_WhenAbuviManagedByUserIdDoesNotExist_ThrowsBusinessRuleException`
- [ ] `UpdateAsync_WhenAbuviManagedByUserIdIsNull_ClearsAssignment`

### Unit Tests — `CampObservationsServiceTests.cs`

- [ ] `AddObservationAsync_WhenCampExists_CreatesAndReturnsObservation`
- [ ] `AddObservationAsync_WhenCampDoesNotExist_ThrowsNotFoundException`
- [ ] `GetObservationsAsync_ReturnsObservationsOrderedByCreatedAtDesc`

### General

- [ ] All existing tests in other files compile and pass
- [ ] Unit test coverage ≥ 90% for `CampsService`, `CampObservationsService`

---

## Error Response Format

All endpoints use the existing `ApiResponse<T>` envelope:

| HTTP Status | Scenario |
|-------------|----------|
| `200 OK` | Successful GET, PUT |
| `201 Created` | Successful POST (observation created) |
| `400 Bad Request` | Validation error (FluentValidation), invalid `AbuviManagedByUserId` |
| `404 Not Found` | Camp not found |
| `403 Forbidden` | Insufficient role |

---

## Dependencies

### NuGet Packages

No new NuGet packages required. All needed packages (EF Core, FluentValidation, NSubstitute, FluentAssertions, xUnit) are already referenced.

### EF Core Migration

```bash
dotnet ef migrations add AddCampExtraFieldsAndAudit --project src/Abuvi.API
```

Note: Migration will be applied automatically on startup via the existing auto-migration logic in `Program.cs`.

---

## Notes

### `UserRole` is an Enum

`UserRole` has values `Admin`, `Board`, `Member`. The enriched spec mentions `IsUserInRoleAsync(userId, "Board")` but since the codebase uses an enum, validate using:

```csharp
var user = await usersRepository.GetByIdAsync(userId, ct);
if (user is null || (user.Role != UserRole.Board && user.Role != UserRole.Admin))
    throw new BusinessRuleException("...");
```

### `Location` Field — Not Renamed

The `Location` property remains unchanged in this feature. The overlap with `Locality`/`Province`/`AdministrativeArea` is noted but not addressed in this scope.

### Dual Pricing Models

`PricePerAdult/Child/Baby` (age-based, used for registrations) coexists with the new `BasePrice` (catalogue/reference price from spreadsheet). They serve different purposes.

### CampObservation — Append-Only

No UPDATE or DELETE endpoints. Enforced at service level (no methods exposed). Users create observations via POST, read via GET.

### CampAuditLog — Immutable

Written only by `CampsService.UpdateAsync`. No write endpoints exposed. `GET /audit-log` is Admin-only.

### AccommodationCapacity JSON — Backward Compatibility

Existing records without new fields deserialize safely because all new properties are nullable. No migration needed for JSON data.

---

## Next Steps After Implementation

1. Frontend feature to display and edit new camp fields (separate ticket)
2. Frontend feature for observations management (separate ticket)
3. Frontend feature for audit log viewer (separate ticket)
4. Data entry: manually populate new fields via API/UI

---

## Implementation Verification

- [ ] **Code Quality**: No nullable reference type warnings, no C# analyzer errors
- [ ] **Data Model**: 15 new `camps` columns, `camp_observations` table, `camp_audit_logs` table
- [ ] **JSON**: `AccommodationCapacity` supports 11 new fields
- [ ] **Functionality**: 3 new endpoints return correct status codes
- [ ] **Audit**: `PUT /api/camps/{id}` writes entries only for changed tracked fields (9 fields)
- [ ] **Audit**: `PUT /api/camps/{id}` sets `LastModifiedByUserId` on every save
- [ ] **Validation**: Invalid `AbuviManagedByUserId` returns 400
- [ ] **Testing**: 90%+ coverage on `CampsService`, `CampObservationsService`
- [ ] **Migration**: Applied successfully, new tables exist, new columns present
- [ ] **Documentation**: Updated

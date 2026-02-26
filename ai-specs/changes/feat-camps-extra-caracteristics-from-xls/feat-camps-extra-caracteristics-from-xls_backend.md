# Backend Implementation Plan: feat-camps-extra-caracteristics-from-xls — Import Extra Camp Characteristics

## 2. Overview

Import 34 extra fields from `CAMPAMENTOS.csv` into the `Camp` entity. The work spans:

- **Rename** `Location` → `RawAddress` (column rename migration, data preserved)
- **13 new columns** on `camps` table (contact info, ABUVI tracking, `VatIncluded`, `LastModifiedByUserId`, `AbuviManagedByUserId` FK)
- **2 new tables**: `camp_observations` (append-only notes with authorship) and `camp_audit_logs` (field-level change history)
- **11 new JSON fields** in `AccommodationCapacity` (capacity descriptions + facility flags)
- **CSV import service** with Windows-1252 encoding, semicolon separator, and three match strategies
- **4 new API endpoints** + update `PUT /api/camps/{id}` to write audit logs and accept `updatedByUserId`

All service logic is covered with unit tests following TDD (Red→Green→Refactor).

---

## 3. Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

### Files to create

| File | Purpose |
|------|---------|
| `Features/Camps/CampObservationsService.cs` | Add/list observations |
| `Features/Camps/ICampObservationsService.cs` | Interface |
| `Features/Camps/ICampObservationsRepository.cs` | Interface |
| `Features/Camps/CampObservationsRepository.cs` | EF Core implementation |
| `Features/Camps/CampCsvImportService.cs` | CSV parsing + upsert |
| `Features/Camps/ICampCsvImportService.cs` | Interface |
| `Features/Camps/CampCsvImportModels.cs` | `CampCsvRow`, `CampImportResult`, `CampImportRowResult` |
| `Data/Configurations/CampObservationConfiguration.cs` | EF Core fluent config |
| `Data/Configurations/CampAuditLogConfiguration.cs` | EF Core fluent config |
| `src/Abuvi.Tests/Unit/Features/Camps/CampObservationsServiceTests.cs` | Unit tests |
| `src/Abuvi.Tests/Unit/Features/Camps/CampCsvImportServiceTests.cs` | Unit tests |
| `src/Abuvi.Tests/Helpers/TestFiles/sample_campamentos.csv` | Test CSV fixture |

### Files to modify

| File | Change |
|------|--------|
| `Features/Camps/CampsModels.cs` | New entity properties + 2 new entities + new DTOs + AccommodationCapacity fields |
| `Features/Camps/CampsService.cs` | Add `updatedByUserId` to `UpdateAsync`, write audit logs, validate `AbuviManagedByUserId` |
| `Features/Camps/ICampsRepository.cs` | Add 3 new methods |
| `Features/Camps/CampsRepository.cs` | Implement 3 new methods |
| `Features/Camps/CampsEndpoints.cs` | Pass userId to `UpdateAsync`; add 4 new endpoints |
| `Data/Configurations/CampConfiguration.cs` | Rename `Location` → `RawAddress`, add new column mappings + relationships |
| `Data/AbuviDbContext.cs` | Add `CampObservations` and `CampAuditLogs` DbSets |
| `Features/Users/IUsersRepository.cs` | Add `IsUserInRoleAsync` method |
| `Features/Users/UsersRepository.cs` | Implement `IsUserInRoleAsync` |
| `Program.cs` | Register new services |
| `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs` | Update all `UpdateAsync` test calls to pass userId |

---

## 4. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the feature branch before any code changes
- **Implementation Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-camps-extra-caracteristics-from-xls-backend`
  3. `git branch` — verify you are on the new branch
- **Note**: If already on a branch named after the ticket ID (without `-backend`), create a new one with the `-backend` suffix to separate frontend/backend concerns

---

### Step 1: Data Model Changes — `CampsModels.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Rename `Location` → `RawAddress` on the `Camp` class; add new scalar properties, navigation properties, two new entity classes, and extend `AccommodationCapacity`

#### 1a — Rename `Location` property on `Camp`

```csharp
// BEFORE
public string? Location { get; set; }

// AFTER
public string? RawAddress { get; set; }   // formerly Location
```

#### 1b — New scalar properties on `Camp`

Add after existing properties:

```csharp
// Contact info from CSV
public string? Province { get; set; }
public string? ContactEmail { get; set; }
public string? ContactPerson { get; set; }      // "Nombre" CSV column
public string? ContactCompany { get; set; }     // "Empresa" CSV column
public string? SecondaryWebsiteUrl { get; set; }

// Pricing (CSV PRECIO → both PricePerAdult and PricePerChild on import)
public bool? VatIncluded { get; set; }          // IVA column

// ABUVI internal tracking
public int? ExternalSourceId { get; set; }
public Guid? AbuviManagedByUserId { get; set; }
public string? AbuviContactedAt { get; set; }
public string? AbuviPossibility { get; set; }
public string? AbuviLastVisited { get; set; }
public bool? AbuviHasDataErrors { get; set; }

// Audit
public Guid? LastModifiedByUserId { get; set; }
```

#### 1c — New navigation properties on `Camp`

```csharp
public ApplicationUser? AbuviManagedByUser { get; set; }
public ICollection<CampObservation> Observations { get; set; } = new List<CampObservation>();
public ICollection<CampAuditLog> AuditLogs { get; set; } = new List<CampAuditLog>();
```

#### 1d — New `CampObservation` entity class

```csharp
public class CampObservation
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Season { get; set; }           // "2023", "2024", "2025", "2025/2026", null
    public Guid? CreatedByUserId { get; set; }    // null = imported from CSV
    public DateTime CreatedAt { get; set; }
    public Camp Camp { get; set; } = null!;
}
```

#### 1e — New `CampAuditLog` entity class

```csharp
public class CampAuditLog
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public string FieldName { get; set; } = string.Empty;   // e.g. "PricePerAdult"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
    public Camp Camp { get; set; } = null!;
}
```

#### 1f — Extend `AccommodationCapacity` class

```csharp
// Capacity descriptions from CSV (raw text — reference only)
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

These serialize into the existing `accommodation_capacity_json` column — no new DB column needed.

#### 1g — Update DTOs

**Update `UpdateCampRequest`** — rename `Location` parameter to `RawAddress`, remove any `BasePrice` if present, and add new writable fields:

```csharp
public record UpdateCampRequest(
    string Name,
    string? Description,
    string? RawAddress,              // renamed from Location
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    AccommodationCapacity? AccommodationCapacity = null,
    // New fields
    string? Province = null,
    string? ContactEmail = null,
    string? ContactPerson = null,
    string? ContactCompany = null,
    string? SecondaryWebsiteUrl = null,
    bool? VatIncluded = null,
    Guid? AbuviManagedByUserId = null,
    string? AbuviContactedAt = null,
    string? AbuviPossibility = null,
    string? AbuviLastVisited = null,
    bool? AbuviHasDataErrors = null
);
```

**Update `CampDetailResponse`** — add new fields after existing ones:

```csharp
// Add to the existing parameters list:
string? Province,
string? ContactEmail,
string? ContactPerson,
string? ContactCompany,
string? SecondaryWebsiteUrl,
bool? VatIncluded,
int? ExternalSourceId,
Guid? AbuviManagedByUserId,
string? AbuviManagedByUserName,
string? AbuviContactedAt,
string? AbuviPossibility,
string? AbuviLastVisited,
bool? AbuviHasDataErrors,
Guid? LastModifiedByUserId,
IReadOnlyList<CampObservationResponse> Observations
```

**Add new DTOs:**

```csharp
public record AddCampObservationRequest(string Text, string? Season);

public record CampObservationResponse(
    Guid Id,
    string Text,
    string? Season,
    Guid? CreatedByUserId,
    DateTime CreatedAt
);

public record CampAuditLogResponse(
    Guid Id,
    string FieldName,
    string? OldValue,
    string? NewValue,
    Guid ChangedByUserId,
    DateTime ChangedAt
);
```

---

### Step 2: EF Core Configurations

#### 2a — Update `CampConfiguration.cs`

- **File**: `src/Abuvi.API/Data/Configurations/CampConfiguration.cs`
- **Action**:
  1. Find `builder.Property(c => c.Location)...` and change it to:
     ```csharp
     builder.Property(c => c.RawAddress).HasMaxLength(500).HasColumnName("raw_address");
     ```
  2. Add new column mappings:
     ```csharp
     builder.Property(c => c.Province).HasMaxLength(100).HasColumnName("province");
     builder.Property(c => c.ContactEmail).HasMaxLength(200).HasColumnName("contact_email");
     builder.Property(c => c.ContactPerson).HasMaxLength(200).HasColumnName("contact_person");
     builder.Property(c => c.ContactCompany).HasMaxLength(200).HasColumnName("contact_company");
     builder.Property(c => c.SecondaryWebsiteUrl).HasMaxLength(500).HasColumnName("secondary_website_url");
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
  3. Add relationship configurations:
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

```csharp
public class CampObservationConfiguration : IEntityTypeConfiguration<CampObservation>
{
    public void Configure(EntityTypeBuilder<CampObservation> builder)
    {
        builder.ToTable("camp_observations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.CampId).HasColumnName("camp_id").IsRequired();
        builder.Property(o => o.Text).HasMaxLength(4000).HasColumnName("text").IsRequired();
        builder.Property(o => o.Season).HasMaxLength(20).HasColumnName("season");
        builder.Property(o => o.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.HasIndex(o => o.CampId).HasDatabaseName("ix_camp_observations_camp_id");
    }
}
```

#### 2c — Create `CampAuditLogConfiguration.cs`

- **File**: `src/Abuvi.API/Data/Configurations/CampAuditLogConfiguration.cs`

```csharp
public class CampAuditLogConfiguration : IEntityTypeConfiguration<CampAuditLog>
{
    public void Configure(EntityTypeBuilder<CampAuditLog> builder)
    {
        builder.ToTable("camp_audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.CampId).HasColumnName("camp_id").IsRequired();
        builder.Property(a => a.FieldName).HasMaxLength(100).HasColumnName("field_name").IsRequired();
        builder.Property(a => a.OldValue).HasMaxLength(2000).HasColumnName("old_value");
        builder.Property(a => a.NewValue).HasMaxLength(2000).HasColumnName("new_value");
        builder.Property(a => a.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
        builder.Property(a => a.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("NOW()");
        builder.HasIndex(a => a.CampId).HasDatabaseName("ix_camp_audit_logs_camp_id");
        builder.HasIndex(a => new { a.CampId, a.ChangedAt })
            .HasDatabaseName("ix_camp_audit_logs_camp_id_changed_at");
    }
}
```

---

### Step 3: Update `AbuviDbContext.cs`

- **File**: `src/Abuvi.API/Data/AbuviDbContext.cs`
- **Action**: Add two new DbSets (configurations are auto-discovered via `ApplyConfigurationsFromAssembly`):

```csharp
public DbSet<CampObservation> CampObservations => Set<CampObservation>();
public DbSet<CampAuditLog> CampAuditLogs => Set<CampAuditLog>();
```

---

### Step 4: EF Core Migration

- **Action**: Generate the migration that renames `location` → `raw_address` and adds all new columns/tables
- **Command**:
  ```bash
  dotnet ef migrations add AddCampExtraFieldsRenameLocationAndAudit --project src/Abuvi.API
  ```
- **Important**: After generation, open the migration file and verify:
  1. `RenameColumn(table: "camps", name: "location", newName: "raw_address")` is present
  2. The new `camp_observations` table is created
  3. The new `camp_audit_logs` table is created
  4. All 13 new `camps` columns are added
  5. EF Core may generate a `DropColumn`/`AddColumn` instead of `RenameColumn` — if so, **manually** change it to `migrationBuilder.RenameColumn(...)` to avoid data loss
- **Apply**:
  ```bash
  dotnet ef database update --project src/Abuvi.API
  ```

---

### Step 5: Add `IsUserInRoleAsync` to `IUsersRepository`

- **File**: `src/Abuvi.API/Features/Users/IUsersRepository.cs` (find the correct path)
- **Action**: Add method:
  ```csharp
  Task<bool> IsUserInRoleAsync(Guid userId, string role, CancellationToken ct = default);
  ```
- **File**: `src/Abuvi.API/Features/Users/UsersRepository.cs`
- **Action**: Implement using `UserManager<ApplicationUser>`:
  ```csharp
  public async Task<bool> IsUserInRoleAsync(Guid userId, string role, CancellationToken ct = default)
  {
      var user = await _userManager.FindByIdAsync(userId.ToString());
      if (user is null) return false;
      return await _userManager.IsInRoleAsync(user, role);
  }
  ```

---

### Step 6: Update `ICampsRepository` + `CampsRepository`

#### 6a — `ICampsRepository.cs`

Add three new methods:

```csharp
Task<List<Camp>> GetAllForImportAsync(CancellationToken ct = default);
Task<Camp?> GetByExternalSourceIdAsync(int externalSourceId, CancellationToken ct = default);
Task AddAuditLogsAsync(IEnumerable<CampAuditLog> entries, CancellationToken ct = default);
```

#### 6b — `CampsRepository.cs`

```csharp
public async Task<List<Camp>> GetAllForImportAsync(CancellationToken ct = default)
    => await _context.Camps.AsNoTracking().ToListAsync(ct);

public async Task<Camp?> GetByExternalSourceIdAsync(int externalSourceId, CancellationToken ct = default)
    => await _context.Camps.AsNoTracking()
        .FirstOrDefaultAsync(c => c.ExternalSourceId == externalSourceId, ct);

public async Task AddAuditLogsAsync(IEnumerable<CampAuditLog> entries, CancellationToken ct = default)
{
    _context.CampAuditLogs.AddRange(entries);
    await _context.SaveChangesAsync(ct);
}
```

---

### Step 7: TDD — `CampsService.UpdateAsync` Audit Log

**7a — Write failing tests first** in `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`

Before writing tests, **update all existing `UpdateAsync` test method calls** — the signature will change to add `Guid updatedByUserId`:
```csharp
// OLD
await sut.UpdateAsync(campId, request, ct);

// NEW
await sut.UpdateAsync(campId, request, userId, ct);
```

Add `IUsersRepository` mock in the test class setup.

**New test cases to add** (all within the existing `CampsServiceTests` class):

```csharp
// Audit log tests
UpdateAsync_WhenPricePerAdultChanges_CreatesAuditLogEntry
UpdateAsync_WhenPricePerChildChanges_CreatesAuditLogEntry
UpdateAsync_WhenIsActiveChanges_CreatesAuditLogEntry
UpdateAsync_WhenNoTrackedFieldChanges_DoesNotCreateAuditLog
UpdateAsync_WhenMultipleTrackedFieldsChange_CreatesOneEntryPerField
UpdateAsync_AlwaysSetsLastModifiedByUserId
UpdateAsync_WhenAbuviManagedByUserIdChanges_CreatesAuditLogEntry
UpdateAsync_WhenAbuviManagedByUserIdIsValidBoardUser_Succeeds
UpdateAsync_WhenAbuviManagedByUserIdIsNotBoardUser_ThrowsValidationException
UpdateAsync_WhenAbuviManagedByUserIdDoesNotExist_ThrowsValidationException
UpdateAsync_WhenAbuviManagedByUserIdIsNull_ClearsAssignment
```

Test structure example:
```csharp
[Fact]
public async Task UpdateAsync_WhenPricePerAdultChanges_CreatesAuditLogEntry()
{
    // Arrange
    var campId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var existing = new Camp { Id = campId, PricePerAdult = 100m, PricePerChild = 80m, PricePerBaby = 0m, IsActive = true, Name = "Test" };
    _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>()).Returns(existing);

    var request = new UpdateCampRequest("Test", null, null, null, null, null, 120m, 80m, 0m, true);

    // Act
    await _sut.UpdateAsync(campId, request, userId);

    // Assert
    await _repository.Received(1).AddAuditLogsAsync(
        Arg.Is<IEnumerable<CampAuditLog>>(logs =>
            logs.Any(l => l.FieldName == "PricePerAdult" && l.OldValue == "100" && l.NewValue == "120")),
        Arg.Any<CancellationToken>());
}
```

**7b — Implement audit logic in `CampsService.cs`**

1. Add `IUsersRepository _usersRepository` dependency (constructor injection)
2. Change `UpdateAsync` signature:
   ```csharp
   public async Task<CampDetailResponse?> UpdateAsync(
       Guid id,
       UpdateCampRequest request,
       Guid updatedByUserId,
       CancellationToken cancellationToken = default)
   ```
3. Before applying field changes, call `BuildAuditEntries(existing, request, updatedByUserId)`
4. Validate `AbuviManagedByUserId` if provided:
   ```csharp
   if (request.AbuviManagedByUserId.HasValue)
   {
       var isBoard = await _usersRepository.IsUserInRoleAsync(
           request.AbuviManagedByUserId.Value, "Board", cancellationToken);
       if (!isBoard)
           throw new ValidationException("AbuviManagedByUserId must reference a user with Board role.");
   }
   ```
5. Apply all new field assignments in the update block (`camp.RawAddress = request.RawAddress`, all new fields, `camp.LastModifiedByUserId = updatedByUserId`)
6. After `_repository.UpdateAsync(camp, ct)`, if `auditEntries.Count > 0`, call `_repository.AddAuditLogsAsync(auditEntries, ct)`

**Tracked fields for audit** (10 total):

```csharp
private static List<CampAuditLog> BuildAuditEntries(Camp existing, UpdateCampRequest request, Guid userId)
{
    var entries = new List<CampAuditLog>();
    var now = DateTime.UtcNow;

    void Track(string field, string? oldVal, string? newVal)
    {
        if (oldVal != newVal)
            entries.Add(new CampAuditLog
            {
                Id = Guid.NewGuid(),
                CampId = existing.Id,
                FieldName = field,
                OldValue = oldVal,
                NewValue = newVal,
                ChangedByUserId = userId,
                ChangedAt = now
            });
    }

    Track("PricePerAdult", existing.PricePerAdult.ToString(), request.PricePerAdult.ToString());
    Track("PricePerChild", existing.PricePerChild.ToString(), request.PricePerChild.ToString());
    Track("VatIncluded", existing.VatIncluded?.ToString(), request.VatIncluded?.ToString());
    Track("AbuviPossibility", existing.AbuviPossibility, request.AbuviPossibility);
    Track("AbuviLastVisited", existing.AbuviLastVisited, request.AbuviLastVisited);
    Track("AbuviContactedAt", existing.AbuviContactedAt, request.AbuviContactedAt);
    Track("AbuviManagedByUserId", existing.AbuviManagedByUserId?.ToString(), request.AbuviManagedByUserId?.ToString());
    Track("IsActive", existing.IsActive.ToString(), request.IsActive.ToString());
    Track("ContactPerson", existing.ContactPerson, request.ContactPerson);
    Track("ContactEmail", existing.ContactEmail, request.ContactEmail);

    return entries;
}
```

7. Update `MapToCampDetailResponse(...)` to map all new fields and include `Observations` (pass an empty list `[]` from the Update path; the GET endpoint can load them via a separate repository call if needed)

---

### Step 8: TDD — `CampObservationsService`

**8a — Create `ICampObservationsRepository.cs`**

```csharp
public interface ICampObservationsRepository
{
    Task<CampObservation> AddAsync(CampObservation observation, CancellationToken ct = default);
    Task<List<CampObservation>> GetByCampIdAsync(Guid campId, CancellationToken ct = default);
    Task<bool> CampExistsAsync(Guid campId, CancellationToken ct = default);
}
```

**8b — Write failing tests** in `src/Abuvi.Tests/Unit/Features/Camps/CampObservationsServiceTests.cs`

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

**8c — Implement `ICampObservationsService.cs`**

```csharp
public interface ICampObservationsService
{
    Task<CampObservationResponse> AddObservationAsync(
        Guid campId, AddCampObservationRequest request, Guid? createdByUserId, CancellationToken ct = default);
    Task<List<CampObservationResponse>> GetObservationsAsync(Guid campId, CancellationToken ct = default);
}
```

**8d — Implement `CampObservationsService.cs`**

```csharp
public class CampObservationsService(ICampObservationsRepository repository) : ICampObservationsService
{
    public async Task<CampObservationResponse> AddObservationAsync(
        Guid campId, AddCampObservationRequest request, Guid? createdByUserId, CancellationToken ct = default)
    {
        if (!await repository.CampExistsAsync(campId, ct))
            throw new NotFoundException($"Camp {campId} not found.");

        var observation = new CampObservation
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            Text = request.Text,
            Season = request.Season,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
        var saved = await repository.AddAsync(observation, ct);
        return new CampObservationResponse(saved.Id, saved.Text, saved.Season, saved.CreatedByUserId, saved.CreatedAt);
    }

    public async Task<List<CampObservationResponse>> GetObservationsAsync(Guid campId, CancellationToken ct = default)
    {
        var observations = await repository.GetByCampIdAsync(campId, ct);
        return observations
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new CampObservationResponse(o.Id, o.Text, o.Season, o.CreatedByUserId, o.CreatedAt))
            .ToList();
    }
}
```

**8e — Implement `CampObservationsRepository.cs`**

```csharp
public class CampObservationsRepository(AbuviDbContext context) : ICampObservationsRepository
{
    public async Task<CampObservation> AddAsync(CampObservation observation, CancellationToken ct = default)
    {
        context.CampObservations.Add(observation);
        await context.SaveChangesAsync(ct);
        return observation;
    }

    public async Task<List<CampObservation>> GetByCampIdAsync(Guid campId, CancellationToken ct = default)
        => await context.CampObservations
            .AsNoTracking()
            .Where(o => o.CampId == campId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> CampExistsAsync(Guid campId, CancellationToken ct = default)
        => await context.Camps.AnyAsync(c => c.Id == campId, ct);
}
```

---

### Step 9: TDD — `CampCsvImportService`

#### 9a — Create `CampCsvImportModels.cs`

```csharp
public class CampCsvRow
{
    public string? ExternalSourceId { get; set; }   // "N°"
    public string? GestionPor { get; set; }
    public string? AbuviContactedAt { get; set; }
    public string? AbuviPossibility { get; set; }
    public string? AbuviLastVisited { get; set; }
    public string? Name { get; set; }
    public string? AdministrativeArea { get; set; }
    public string? Province { get; set; }
    public string? Locality { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactCompany { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NationalPhoneNumber { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? SecondaryWebsiteUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? Notes20252026 { get; set; }
    public string? Precio { get; set; }
    public string? Iva { get; set; }
    public string? TotalCapacity { get; set; }
    public string? RoomsDescription { get; set; }
    public string? BungalowsDescription { get; set; }
    public string? TentsDescription { get; set; }
    public string? TentAreaDescription { get; set; }
    public string? ParkingSpots { get; set; }
    public string? HasAdaptedMenu { get; set; }
    public string? HasEnclosedDiningRoom { get; set; }
    public string? HasSwimmingPool { get; set; }
    public string? HasSportsCourt { get; set; }
    public string? HasForestArea { get; set; }
    public string? Notes2024 { get; set; }
    public string? Notes2023 { get; set; }
    public string? Notes2025 { get; set; }
    public string? AbuviHasDataErrors { get; set; }
}

public record CampImportResult(int Created, int Updated, int Skipped, List<CampImportRowResult> Rows);

public record CampImportRowResult(
    int RowNumber,
    string? CampName,
    string Status,        // "Created", "Updated", "Skipped", "Error"
    string? Message,
    string? GestionPor    // non-null = needs manual AbuviManagedByUserId assignment
);
```

#### 9b — Create test fixture: `sample_campamentos.csv`

- **File**: `src/Abuvi.Tests/Helpers/TestFiles/sample_campamentos.csv`
- **Encoding**: Windows-1252 (ANSI)
- **Content**: 5 rows covering all edge cases:
  - Row 1: Valid row with all columns populated, notes 2023/2024/2025/2025-2026, GestionPor non-empty, PRECIO=150,50 (decimal comma), IVA=Si, facilities=Si/No/0
  - Row 2: Matching row (same name as an existing camp) to test update path
  - Row 3: Row with empty Name (should be skipped)
  - Row 4: Quoted fields containing semicolons
  - Row 5: Special characters: ñ, á, é, ó, ü

#### 9c — Write failing tests in `CampCsvImportServiceTests.cs`

Organize tests in three groups:

**Parsing tests** (test the private `ParseCsv` method via reflection or make it `internal`):
```
ParseCsv_WhenValidCsvStream_ReturnsCorrectNumberOfRows
ParseCsv_WhenFileIsWindowsEncoded_ReturnsCorrectSpecialCharacters
ParseCsv_WhenRowHasQuotedField_HandlesQuotesCorrectly
ParseCsv_WhenPrecioIsDecimalWithComma_ParsesCorrectly
ParseCsv_WhenIvaIsSi_ReturnsTrueVatIncluded
ParseCsv_WhenIvaIsNo_ReturnsFalseVatIncluded
ParseCsv_WhenIvaIsEmpty_ReturnsNullVatIncluded
ParseCsv_WhenFacilityIsSi_ReturnsTrueFlag
ParseCsv_WhenFacilityIs0_ReturnsNullFlag
ParseCsv_WhenFacilityIsNo_ReturnsFalseFlag
```

**Matching tests** (test the private `FindMatchingCamp` method via `internal`):
```
FindMatchingCamp_WhenExternalSourceIdMatches_ReturnsThatCamp
FindMatchingCamp_WhenNameMatchesCaseInsensitive_ReturnsThatCamp
FindMatchingCamp_WhenWebsiteMatches_ReturnsThatCamp
FindMatchingCamp_WhenSecondaryWebsiteMatches_ReturnsThatCamp
FindMatchingCamp_WhenNoMatchFound_ReturnsNull
FindMatchingCamp_WhenMultiplePossible_PrioritizesExternalSourceId
```

**Orchestration tests**:
```
ImportFromStreamAsync_WhenCampNotFound_CreatesNewCampWithIsActiveFalse
ImportFromStreamAsync_WhenCampFound_UpdatesExistingCamp
ImportFromStreamAsync_WhenRowHasNonEmptyNotes_CreatesObservationRecords
ImportFromStreamAsync_WhenRowHasEmptyNotes_SkipsObservationCreation
ImportFromStreamAsync_WhenRowHasNoName_SkipsRow
ImportFromStreamAsync_WhenAllRowsProcessed_ReturnsCorrectSummary
ImportFromStreamAsync_WhenRepositoryThrows_ReturnsErrorForRow
ImportFromStreamAsync_WhenGestionPorIsNonEmpty_CreatesObservationWithImportPrefix
ImportFromStreamAsync_WhenGestionPorIsEmpty_SkipsGestionPorObservation
ImportFromStreamAsync_DoesNotSetAbuviManagedByUserIdOnImport
```

To make private methods testable, declare the service's internal parsing and matching methods as `internal` and add `[assembly: InternalsVisibleTo("Abuvi.Tests")]` in `Abuvi.API`.

#### 9d — Implement `ICampCsvImportService.cs`

```csharp
public interface ICampCsvImportService
{
    Task<CampImportResult> ImportFromStreamAsync(Stream csvStream, CancellationToken ct = default);
}
```

#### 9e — Implement `CampCsvImportService.cs`

Key implementation notes:

1. **Encoding**: Register before use (in constructor or `Program.cs`):
   ```csharp
   Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
   var encoding = Encoding.GetEncoding(1252);
   ```
2. **CSV parsing**: Use `CsvHelper` NuGet (`CsvHelper` v33+) with `';'` delimiter and `BadDataFound = null`:
   ```csharp
   using var reader = new StreamReader(csvStream, encoding);
   using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
   {
       Delimiter = ";",
       BadDataFound = null,
       MissingFieldFound = null
   });
   ```
3. **Column mapping**: Map by header index (columns 0–33) since header names contain special characters. Use `CsvHelper`'s index-based mapping with a custom `ClassMap`.
4. **Decimal parsing**: `"150,50"` → `decimal.TryParse(raw, NumberStyles.Any, new CultureInfo("es-ES"), out var val)` (Spanish locale uses comma as decimal separator).
5. **Boolean flag parsing**: `"Si"` / `"si"` → `true`; `"No"` / `"no"` → `false`; `"0"` or empty → `null`
6. **Match strategy**:
   1. Match by `ExternalSourceId` if non-null
   2. Match by `Name` (case-insensitive, trimmed)
   3. Match by `WebsiteUrl` or `SecondaryWebsiteUrl`
   4. No match → create new Camp with `IsActive = false`
7. **Observation creation** (only if text is non-empty and not `"0"`):
   - `Notes2023` → `Season = "2023"`
   - `Notes2024` → `Season = "2024"`
   - `Notes2025` → `Season = "2025"`
   - `Notes20252026` → `Season = "2025/2026"`
   - `GestionPor` → `Season = null`, `Text = $"[CSV Import] Gestión por: {row.GestionPor}"`
8. **`AbuviManagedByUserId` is NOT set during import** — only the `GestionPor` observation is created
9. **PRECIO mapping**: Parse decimal, set both `PricePerAdult` and `PricePerChild` to the same value
10. **Error handling**: Each row is wrapped in try/catch; errors are recorded in `CampImportRowResult` with `Status = "Error"`; processing continues

**NuGet**: `CsvHelper` — check `src/Abuvi.API/Abuvi.API.csproj`. If not present:
```bash
dotnet add src/Abuvi.API package CsvHelper
dotnet add src/Abuvi.Tests package CsvHelper   # only if tests parse CSV directly
```

---

### Step 10: Update `CampsEndpoints.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`

#### 10a — Update `PUT /api/camps/{id}`

Extract `userId` from `ClaimsPrincipal` and pass it to `UpdateAsync`:

```csharp
.MapPut("/{id:guid}", async (
    Guid id,
    UpdateCampRequest request,
    ICampsService service,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var result = await service.UpdateAsync(id, request, userId, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
})
```

#### 10b — Add new observation endpoints

Group under `camps/{campId:guid}/observations`, require `Board` policy:

```csharp
POST  /api/camps/{campId:guid}/observations   → ICampObservationsService.AddObservationAsync (Board+)
GET   /api/camps/{campId:guid}/observations   → ICampObservationsService.GetObservationsAsync (Board+)
```

#### 10c — Add audit log endpoint

```csharp
GET /api/camps/{campId:guid}/audit-log   → query _context.CampAuditLogs directly (Admin only)
```

Inject `ICampsAuditLogRepository` (or directly use a new `ICampsRepository.GetAuditLogsAsync`) and return `IReadOnlyList<CampAuditLogResponse>` ordered by `ChangedAt DESC`.

Add to `ICampsRepository`:
```csharp
Task<List<CampAuditLog>> GetAuditLogAsync(Guid campId, CancellationToken ct = default);
```

#### 10d — Add CSV import endpoint

```csharp
POST /api/admin/camps/import-csv   → Admin only
```

```csharp
.MapPost("/import-csv", async (
    IFormFile file,
    ICampCsvImportService importService,
    ClaimsPrincipal user,
    ILogger<...> logger,
    CancellationToken ct) =>
{
    if (file is null || file.Length == 0)
        return Results.BadRequest("No file provided.");
    if (file.Length > 1_048_576)    // 1 MB limit
        return Results.BadRequest("File exceeds 1 MB limit.");

    var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    using var stream = file.OpenReadStream();
    var result = await importService.ImportFromStreamAsync(stream, ct);
    logger.LogInformation("CSV import by Admin {UserId}: {Created} created, {Updated} updated, {Skipped} skipped",
        userId, result.Created, result.Updated, result.Skipped);
    return Results.Ok(result);
})
.RequireAuthorization("Admin")
.DisableAntiforgery();
```

---

### Step 11: Register Services in `Program.cs`

```csharp
builder.Services.AddScoped<ICampObservationsService, CampObservationsService>();
builder.Services.AddScoped<ICampObservationsRepository, CampObservationsRepository>();
builder.Services.AddScoped<ICampCsvImportService, CampCsvImportService>();

// Windows-1252 encoding support for CSV parsing
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
```

---

### Step 12: Update Technical Documentation

- **Action**: After implementation, update relevant documentation files
- **Implementation Steps**:
  1. Update `ai-specs/specs/data-model.md` to reflect:
     - `location` column renamed to `raw_address` on `camps`
     - 13 new columns on `camps`
     - New `camp_observations` table
     - New `camp_audit_logs` table
  2. Update `ai-specs/specs/api-spec.yml` with 4 new endpoints + updated `PUT /api/camps/{id}` signature

---

## 5. Implementation Order

1. **Step 0** — Create feature branch `feature/feat-camps-extra-caracteristics-from-xls-backend`
2. **Step 1** — Data model changes (`CampsModels.cs`)
3. **Step 2** — EF Core configurations (update `CampConfiguration`, create `CampObservationConfiguration`, `CampAuditLogConfiguration`)
4. **Step 3** — Update `AbuviDbContext` (new DbSets)
5. **Step 4** — Generate and review EF Core migration (verify rename vs drop/add)
6. **Step 5** — Add `IsUserInRoleAsync` to `IUsersRepository` + implementation
7. **Step 6** — Add 3 new methods to `ICampsRepository` + `CampsRepository`
8. **Step 7** — [TDD] Write failing tests → Implement `CampsService.UpdateAsync` audit log
9. **Step 8** — [TDD] Write failing tests → Implement `CampObservationsService` + repository
10. **Step 9** — [TDD] Write failing tests → Implement `CampCsvImportService`
11. **Step 10** — Update `CampsEndpoints.cs` (pass userId, 4 new endpoints)
12. **Step 11** — Register services in `Program.cs`
13. **Step 12** — Update technical documentation

---

## 6. Testing Checklist

- [ ] All existing `CampsServiceTests.UpdateAsync` tests pass with updated signature
- [ ] 11 new audit log tests green
- [ ] 3 `CampObservationsServiceTests` green
- [ ] 21 `CampCsvImportServiceTests` green (parsing + matching + orchestration)
- [ ] `sample_campamentos.csv` committed in `src/Abuvi.Tests/Helpers/TestFiles/`
- [ ] `[assembly: InternalsVisibleTo("Abuvi.Tests")]` added to `Abuvi.API` (if internal methods are tested)
- [ ] Unit test coverage ≥ 90% for `CampsService`, `CampObservationsService`, `CampCsvImportService`

---

## 7. Error Response Format

All endpoints use `ApiResponse<T>` envelope:
- `200 OK` — successful GET or PUT
- `201 Created` — successful POST observation
- `400 Bad Request` — invalid `AbuviManagedByUserId`, missing file, file too large
- `403 Forbidden` — insufficient role
- `404 Not Found` — camp not found

---

## 9. Dependencies

### NuGet packages

| Package | Purpose | Already present? |
|---------|---------|-----------------|
| `CsvHelper` | CSV parsing with semicolon delimiter + Windows-1252 | Check `.csproj` |

If `CsvHelper` is not present:
```bash
dotnet add src/Abuvi.API package CsvHelper
```

### EF Core migration
```bash
dotnet ef migrations add AddCampExtraFieldsRenameLocationAndAudit --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## 10. Notes

### Critical: `Location` → `RawAddress` rename

- **Breaking change**: All existing code referencing `camp.Location` must be updated to `camp.RawAddress`
- Check `CampsService.cs` (`camp.Location = request.Location;` → `camp.RawAddress = request.RawAddress;`)
- Check `CampsEndpoints.cs` validators if any reference the field by name
- Check existing `CampsServiceTests` if any assertion references `Location`
- The EF Core migration **must** use `RenameColumn` — not `DropColumn + AddColumn` — to preserve data

### PRECIO → PricePerAdult + PricePerChild

- On CSV import, parse `PRECIO` (comma-decimal, e.g. `"150,50"`) and set both `PricePerAdult` and `PricePerChild` to the same value
- `PricePerBaby` is NOT set from CSV; it retains its existing value (or 0 on new camps)

### CampObservation — append-only

- No UPDATE or DELETE endpoints
- On import: `CreatedByUserId = null`, `CreatedAt = DateTime.UtcNow` (import timestamp)
- On manual creation: `CreatedByUserId = userId` from `ClaimsPrincipal`

### Audit log — immutable

- Written only by `CampsService.UpdateAsync`
- No write endpoints exposed via API
- `GET /api/camps/{id}/audit-log` is Admin-only

### Windows-1252 encoding

- Must call `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` before creating the `StreamReader`
- This can be placed in `Program.cs` at startup (once) rather than inside the service

### Accommodation capacity JSON — backward compatibility

- Existing records without the new fields will deserialize safely because all new properties are nullable
- The `AccommodationCapacity` class uses `JsonSerializer` — no migration needed

### Form file upload

- The `POST /api/admin/camps/import-csv` endpoint uses `IFormFile`
- Ensure the endpoint group has `.DisableAntiforgery()` to allow multipart form uploads from non-browser clients
- File size limit: 1 MB (enforced in the endpoint before calling the service)

---

## 11. Next Steps After Implementation

1. Run `POST /api/admin/camps/import-csv` with the real `CAMPAMENTOS.csv` file
2. Review `CampImportResult` summary (created / updated / skipped counts)
3. For camps where `GestionPor` was non-empty, use `GET /api/camps/{id}/observations` to find the observation, identify the correct user, and call `PUT /api/camps/{id}` with `AbuviManagedByUserId` set
4. Manually review and activate new camps (`IsActive = false`) via `PUT /api/camps/{id}`

---

## 12. Implementation Verification

- [ ] **Code Quality**: No nullable reference type warnings, no C# analyzer errors
- [ ] **Rename**: `RawAddress` used everywhere — no remaining references to `Location` in Camps feature
- [ ] **Functionality**: All 4 new endpoints return correct status codes
- [ ] **Audit**: `PUT /api/camps/{id}` writes audit entries only for the 10 tracked fields when they change
- [ ] **Testing**: 90%+ coverage on `CampsService`, `CampObservationsService`, `CampCsvImportService`
- [ ] **Migration**: Applied successfully, `camp_observations` and `camp_audit_logs` tables exist, `raw_address` column present
- [ ] **Documentation**: `data-model.md` and `api-spec.yml` updated

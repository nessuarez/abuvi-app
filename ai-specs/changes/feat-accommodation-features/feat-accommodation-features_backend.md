# Backend Implementation Plan: feat-accommodation-features — Configurable Accommodation Features

## Overview

This feature adds an extensible catalogue of accommodation characteristics (`AccommodationFeature`) that can be tagged to existing `CampEditionAccommodation` and `AccommodationZone` entities (many-to-many), and extends `MediaItem` to allow photos/plans to be attached to individual accommodations and zones.

**No new Zone entity is needed.** The existing `AccommodationZone` entity (table `accommodation_zones`, endpoints at `/api/camps/editions/{campEditionId}/accommodation-zones`) is the "Zone" referenced by the spec. Its CRUD infrastructure is fully implemented; this feature only adds:

1. Feature tagging via new join tables
2. Media attachment support
3. The feature catalogue itself (CRUD)

---

## Architecture Context

**Primary slice:** `src/Abuvi.API/Features/Camps/`

**Cross-cutting changes:**

- `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs` — add FK fields
- `src/Abuvi.API/Features/MediaItems/MediaItemsService.cs` — filtering + default flags
- `src/Abuvi.API/Features/MediaItems/MediaItemsEndpoints.cs` — new query params
- `src/Abuvi.API/Data/AbuviDbContext.cs` — new `DbSet<T>` entries
- `src/Abuvi.API/Data/Configurations/` — 2 new configs, 3 modified
- `src/Abuvi.API/Program.cs` — DI registration + endpoint registration
- `src/Abuvi.API/Features/BlobStorage/BlobStorageEndpoints.cs` — allowed folder list

---

## Files to Create

| File | Content |
|---|---|
| `Features/Camps/AccommodationFeaturesModels.cs` | `AccommodationFeature`, `AccommodationFeatureAssignment`, `ZoneFeatureAssignment` entities + all DTOs |
| `Features/Camps/AccommodationFeaturesValidators.cs` | FluentValidation validators |
| `Features/Camps/IAccommodationFeaturesRepository.cs` | Repository interface |
| `Features/Camps/AccommodationFeaturesRepository.cs` | Repository implementation |
| `Features/Camps/AccommodationFeaturesService.cs` | Catalogue CRUD service |
| `Features/Camps/AccommodationFeatureAssignmentService.cs` | Assignment service |
| `Features/Camps/AccommodationFeaturesEndpoints.cs` | All feature-related endpoints |
| `Data/Configurations/AccommodationFeatureConfiguration.cs` | EF Core config |
| `Data/Configurations/AccommodationFeatureAssignmentConfiguration.cs` | EF Core config |
| `Data/Configurations/ZoneFeatureAssignmentConfiguration.cs` | EF Core config |
| `Tests/Unit/Features/Camps/AccommodationFeaturesServiceTests.cs` | Unit tests |
| `Tests/Unit/Features/Camps/AccommodationFeatureAssignmentServiceTests.cs` | Unit tests |
| `Tests/Integration/Features/Camps/AccommodationFeaturesIntegrationTests.cs` | Integration tests |
| `Tests/Helpers/Builders/AccommodationFeatureBuilder.cs` | Test builder |

## Files to Modify

| File | Change |
|---|---|
| `Features/Camps/CampsModels.cs` | Add nav properties to `AccommodationZone` and `CampEditionAccommodation`; add `Features` to both response DTOs; add `ZoneId` to `UpdateCampEditionAccommodationRequest` and `CreateCampEditionAccommodationRequest`|
| `Features/Camps/CampEditionAccommodationsService.cs` | Include features in response; handle `ZoneId` in create/update |
| `Features/Camps/AccommodationZonesService.cs` | Include features + media in zone response |
| `Features/MediaItems/MediaItemsModels.cs` | Add `AccommodationId`, `ZoneId` to entity + DTOs |
| `Features/MediaItems/MediaItemsService.cs` | Filter by `accommodationId`/`zoneId`; auto-approve for internal media |
| `Features/MediaItems/MediaItemsEndpoints.cs` | Add `accommodationId`/`zoneId` query params |
| `Data/AbuviDbContext.cs` | Add new DbSets |
| `Data/Configurations/CampEditionAccommodationConfiguration.cs` | Add `FeatureAssignments` relationship |
| `Data/Configurations/AccommodationZoneConfiguration.cs` | Add `FeatureAssignments` + `MediaItems` relationships |
| `Data/Configurations/MediaItemConfiguration.cs` | Add `AccommodationId`/`ZoneId` FKs |
| `Program.cs` | Register new services + endpoints |
| `Features/BlobStorage/BlobStorageEndpoints.cs` | Add `accommodation-media` folder |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch:** `feature/feat-accommodation-features-backend`
- Create from the current `feat-encaje-bolillos` worktree base.

---

### Step 1: Create `AccommodationFeaturesModels.cs`

**File:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesModels.cs` *(new)*

```csharp
namespace Abuvi.API.Features.Camps;

public enum FeatureApplicabilityLevel
{
    Zone,
    Accommodation,
    AccommodationType,
    Any
}

public class AccommodationFeature
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FeatureApplicabilityLevel ApplicabilityLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AccommodationFeatureAssignment> AccommodationAssignments { get; set; } = [];
    public ICollection<ZoneFeatureAssignment> ZoneAssignments { get; set; } = [];
}

// Many-to-many: CampEditionAccommodation ↔ AccommodationFeature
public class AccommodationFeatureAssignment
{
    public Guid AccommodationId { get; set; }
    public Guid FeatureId { get; set; }
    public DateTime CreatedAt { get; set; }

    public CampEditionAccommodation Accommodation { get; set; } = null!;
    public AccommodationFeature Feature { get; set; } = null!;
}

// Many-to-many: AccommodationZone ↔ AccommodationFeature
public class ZoneFeatureAssignment
{
    public Guid ZoneId { get; set; }      // FK → accommodation_zones.id
    public Guid FeatureId { get; set; }
    public DateTime CreatedAt { get; set; }

    public AccommodationZone Zone { get; set; } = null!;
    public AccommodationFeature Feature { get; set; } = null!;
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record AccommodationFeatureResponse(
    Guid Id,
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateAccommodationFeatureRequest(
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    int SortOrder = 0
);

public record UpdateAccommodationFeatureRequest(
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    bool IsActive,
    int SortOrder
);

public record SetFeatureAssignmentsRequest(
    List<Guid> FeatureIds
);

// ── Mapping ───────────────────────────────────────────────────────────────────

public static class AccommodationFeatureMappingExtensions
{
    public static AccommodationFeatureResponse ToResponse(this AccommodationFeature f)
        => new(f.Id, f.Name, f.Icon, f.Description, f.ApplicabilityLevel,
               f.IsActive, f.SortOrder, f.CreatedAt, f.UpdatedAt);
}
```

---

### Step 2: Modify `CampsModels.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

#### 2a. Add nav properties to `AccommodationZone`

```csharp
// After the existing Accommodations collection:
public ICollection<ZoneFeatureAssignment> FeatureAssignments { get; set; } = [];
public ICollection<MediaItem> MediaItems { get; set; } = [];   // using Abuvi.API.Features.MediaItems;
```

Add the required using at the top of the file: `using Abuvi.API.Features.MediaItems;`

#### 2b. Add nav property to `CampEditionAccommodation`

```csharp
// After the existing Zone navigation property:
public ICollection<AccommodationFeatureAssignment> FeatureAssignments { get; set; } = [];
```

#### 2c. Update `CampEditionAccommodationResponse`

Add 3 new fields at the end (before `DateTime CreatedAt`):

```csharp
public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool IsActive,
    int SortOrder,
    int CurrentPreferenceCount,
    int FirstChoiceCount,
    Guid? ZoneId,
    string? ZoneName,
    IReadOnlyList<AccommodationFeatureResponse> Features,   // NEW
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 2d. Update `CreateCampEditionAccommodationRequest`

`ZoneId` is already absent — the spec says to add it. Check if it's already there; if not:

```csharp
public record CreateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    Guid? ZoneId,       // NEW — optional link to AccommodationZone
    int SortOrder = 0
);
```

#### 2e. Update `UpdateCampEditionAccommodationRequest`

```csharp
public record UpdateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool IsActive,
    Guid? ZoneId,       // NEW — optional link to AccommodationZone
    int SortOrder
);
```

#### 2f. Update `AccommodationZoneResponse`

Add two new fields (keep all existing fields, just add):

```csharp
public record AccommodationZoneResponse(
    Guid Id,
    Guid CampEditionId,
    AccommodationType AccommodationType,
    string Name,
    int? MaxCapacity,
    string? DistributionNotes,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<Guid> AccommodationIds,
    IReadOnlyList<AccommodationFeatureResponse> Features,   // NEW
    IReadOnlyList<MediaItemResponse> MediaItems,            // NEW
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

Add the required using: `using Abuvi.API.Features.MediaItems;`

---

### Step 3: Modify `MediaItemsModels.cs`

**File:** `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs`

#### 3a. Add FK fields to `MediaItem`

```csharp
// After CampLocationId:
public Guid? AccommodationId { get; set; }
public Guid? ZoneId { get; set; }          // FK → accommodation_zones.id

// Navigation (add after existing Memory? Memory):
public CampEditionAccommodation? Accommodation { get; set; }
public AccommodationZone? Zone { get; set; }
```

Add usings: `using Abuvi.API.Features.Camps;`

#### 3b. Update `CreateMediaItemRequest`

```csharp
public record CreateMediaItemRequest(
    string FileUrl,
    string? ThumbnailUrl,
    MediaItemType Type,
    string Title,
    string? Description,
    int? Year,
    Guid? MemoryId,
    Guid? CampLocationId,
    Guid? AccommodationId,   // NEW
    Guid? ZoneId,            // NEW
    string? Context);
```

#### 3c. Update `MediaItemResponse`

```csharp
public record MediaItemResponse(
    Guid Id,
    Guid UploadedByUserId,
    string UploadedByName,
    string FileUrl,
    string? ThumbnailUrl,
    string Type,
    string Title,
    string? Description,
    int? Year,
    string? Decade,
    Guid? MemoryId,
    Guid? AccommodationId,   // NEW
    Guid? ZoneId,            // NEW
    string? Context,
    bool IsPublished,
    bool IsApproved,
    DateTime CreatedAt);
```

#### 3d. Update `ToResponse()` mapping

Add `AccommodationId: item.AccommodationId` and `ZoneId: item.ZoneId` to the mapping in `MediaItemMappingExtensions.ToResponse()`.

---

### Step 4: Create EF Core Configurations

#### 4a. `AccommodationFeatureConfiguration.cs` *(new)*

**File:** `src/Abuvi.API/Data/Configurations/AccommodationFeatureConfiguration.cs`

```csharp
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationFeatureConfiguration : IEntityTypeConfiguration<AccommodationFeature>
{
    public void Configure(EntityTypeBuilder<AccommodationFeature> builder)
    {
        builder.ToTable("accommodation_features");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(f => f.Name).IsRequired().HasMaxLength(100).HasColumnName("name");
        builder.HasIndex(f => f.Name).IsUnique();
        builder.Property(f => f.Icon).IsRequired().HasMaxLength(100).HasColumnName("icon");
        builder.Property(f => f.Description).HasColumnType("text").HasColumnName("description");
        builder.Property(f => f.ApplicabilityLevel).IsRequired()
            .HasConversion<string>().HasColumnName("applicability_level");
        builder.Property(f => f.IsActive).IsRequired().HasDefaultValue(true).HasColumnName("is_active");
        builder.Property(f => f.SortOrder).IsRequired().HasDefaultValue(0).HasColumnName("sort_order");
        builder.ToTable(t => t.HasCheckConstraint("CK_AccommodationFeatures_SortOrder", "sort_order >= 0"));
        builder.Property(f => f.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(f => f.UpdatedAt).IsRequired().HasColumnName("updated_at").HasDefaultValueSql("NOW()");
    }
}
```

#### 4b. `AccommodationFeatureAssignmentConfiguration.cs` *(new)*

**File:** `src/Abuvi.API/Data/Configurations/AccommodationFeatureAssignmentConfiguration.cs`

```csharp
public class AccommodationFeatureAssignmentConfiguration
    : IEntityTypeConfiguration<AccommodationFeatureAssignment>
{
    public void Configure(EntityTypeBuilder<AccommodationFeatureAssignment> builder)
    {
        builder.ToTable("accommodation_feature_assignments");
        builder.HasKey(a => new { a.AccommodationId, a.FeatureId });
        builder.Property(a => a.AccommodationId).HasColumnName("accommodation_id");
        builder.Property(a => a.FeatureId).HasColumnName("feature_id");
        builder.Property(a => a.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasOne(a => a.Accommodation)
            .WithMany(acc => acc.FeatureAssignments)
            .HasForeignKey(a => a.AccommodationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Feature)
            .WithMany(f => f.AccommodationAssignments)
            .HasForeignKey(a => a.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### 4c. `ZoneFeatureAssignmentConfiguration.cs` *(new)*

**File:** `src/Abuvi.API/Data/Configurations/ZoneFeatureAssignmentConfiguration.cs`

```csharp
public class ZoneFeatureAssignmentConfiguration : IEntityTypeConfiguration<ZoneFeatureAssignment>
{
    public void Configure(EntityTypeBuilder<ZoneFeatureAssignment> builder)
    {
        builder.ToTable("zone_feature_assignments");
        builder.HasKey(a => new { a.ZoneId, a.FeatureId });
        builder.Property(a => a.ZoneId).HasColumnName("zone_id");
        builder.Property(a => a.FeatureId).HasColumnName("feature_id");
        builder.Property(a => a.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasOne(a => a.Zone)
            .WithMany(z => z.FeatureAssignments)
            .HasForeignKey(a => a.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Feature)
            .WithMany(f => f.ZoneAssignments)
            .HasForeignKey(a => a.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

### Step 5: Modify Existing EF Core Configurations

#### 5a. `AccommodationZoneConfiguration.cs` — Add relationships

Add inside `Configure()` after the existing `HasOne(CampEdition)` block:

```csharp
// ZoneFeatureAssignment relationship is configured in ZoneFeatureAssignmentConfiguration.
// MediaItems inverse relationship:
builder.HasMany(z => z.MediaItems)
    .WithOne(m => m.Zone)
    .HasForeignKey(m => m.ZoneId)
    .OnDelete(DeleteBehavior.SetNull);
```

#### 5b. `CampEditionAccommodationConfiguration.cs` — Add FeatureAssignments relationship

The `AccommodationFeatureAssignment` config already configures `WithMany(acc => acc.FeatureAssignments)` on the accommodation side. No additional config needed here — EF Core discovers it from `AccommodationFeatureAssignmentConfiguration`. Only add if needed after verifying `dotnet build` passes.

#### 5c. `MediaItemConfiguration.cs` — Add AccommodationId and ZoneId FKs

Add inside `Configure()`:

```csharp
builder.Property(m => m.AccommodationId)
    .HasColumnName("accommodation_id")
    .IsRequired(false);

builder.Property(m => m.ZoneId)
    .HasColumnName("zone_id")
    .IsRequired(false);

builder.HasOne(m => m.Accommodation)
    .WithMany()
    .HasForeignKey(m => m.AccommodationId)
    .OnDelete(DeleteBehavior.SetNull);

// Zone (accommodation_zones) relationship configured in AccommodationZoneConfiguration.
```

Note: The `HasMany(MediaItems)` on `AccommodationZone` is already wired in Step 5a above, which creates the inverse for `MediaItem.Zone`. Only the `Accommodation` FK needs to be wired here.

---

### Step 6: Update `AbuviDbContext.cs`

Add after the existing `MediaItems` DbSet:

```csharp
public DbSet<AccommodationFeature> AccommodationFeatures => Set<AccommodationFeature>();
public DbSet<AccommodationFeatureAssignment> AccommodationFeatureAssignments => Set<AccommodationFeatureAssignment>();
public DbSet<ZoneFeatureAssignment> ZoneFeatureAssignments => Set<ZoneFeatureAssignment>();
```

---

### Step 7: Create `IAccommodationFeaturesRepository.cs` and `AccommodationFeaturesRepository.cs`

**File:** `src/Abuvi.API/Features/Camps/IAccommodationFeaturesRepository.cs` *(new)*

```csharp
namespace Abuvi.API.Features.Camps;

public interface IAccommodationFeaturesRepository
{
    Task<IReadOnlyList<AccommodationFeature>> GetAllAsync(bool? activeOnly, CancellationToken ct);
    Task<AccommodationFeature?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<AccommodationFeature?> GetByNameAsync(string name, CancellationToken ct);
    Task<AccommodationFeature> AddAsync(AccommodationFeature feature, CancellationToken ct);
    Task<AccommodationFeature> UpdateAsync(AccommodationFeature feature, CancellationToken ct);
    Task DeleteAsync(AccommodationFeature feature, CancellationToken ct);
    Task<bool> HasAssignmentsAsync(Guid featureId, CancellationToken ct);
    Task<IReadOnlyList<AccommodationFeature>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    // Accommodation assignments — replace-all (atomic DELETE + INSERT)
    Task SetAccommodationAssignmentsAsync(Guid accommodationId, IEnumerable<Guid> featureIds, CancellationToken ct);
    Task<IReadOnlyList<AccommodationFeature>> GetForAccommodationAsync(Guid accommodationId, CancellationToken ct);

    // Zone assignments — replace-all (atomic DELETE + INSERT)
    Task SetZoneAssignmentsAsync(Guid zoneId, IEnumerable<Guid> featureIds, CancellationToken ct);
    Task<IReadOnlyList<AccommodationFeature>> GetForZoneAsync(Guid zoneId, CancellationToken ct);
}
```

**File:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesRepository.cs` *(new)*

```csharp
namespace Abuvi.API.Features.Camps;

public class AccommodationFeaturesRepository(AbuviDbContext db) : IAccommodationFeaturesRepository
{
    public async Task<IReadOnlyList<AccommodationFeature>> GetAllAsync(bool? activeOnly, CancellationToken ct)
    {
        var query = db.AccommodationFeatures.AsNoTracking();
        if (activeOnly == true)
            query = query.Where(f => f.IsActive);
        return await query.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToListAsync(ct);
    }

    public async Task<AccommodationFeature?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.AccommodationFeatures.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<AccommodationFeature?> GetByNameAsync(string name, CancellationToken ct)
        => await db.AccommodationFeatures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Name.ToLower() == name.ToLower(), ct);

    public async Task<AccommodationFeature> AddAsync(AccommodationFeature feature, CancellationToken ct)
    {
        feature.CreatedAt = DateTime.UtcNow;
        feature.UpdatedAt = DateTime.UtcNow;
        db.AccommodationFeatures.Add(feature);
        await db.SaveChangesAsync(ct);
        return feature;
    }

    public async Task<AccommodationFeature> UpdateAsync(AccommodationFeature feature, CancellationToken ct)
    {
        feature.UpdatedAt = DateTime.UtcNow;
        db.AccommodationFeatures.Update(feature);
        await db.SaveChangesAsync(ct);
        return feature;
    }

    public async Task DeleteAsync(AccommodationFeature feature, CancellationToken ct)
    {
        db.AccommodationFeatures.Remove(feature);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> HasAssignmentsAsync(Guid featureId, CancellationToken ct)
        => await db.AccommodationFeatureAssignments.AnyAsync(a => a.FeatureId == featureId, ct)
           || await db.ZoneFeatureAssignments.AnyAsync(a => a.FeatureId == featureId, ct);

    public async Task<IReadOnlyList<AccommodationFeature>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.ToList();
        return await db.AccommodationFeatures.AsNoTracking()
            .Where(f => idList.Contains(f.Id))
            .ToListAsync(ct);
    }

    public async Task SetAccommodationAssignmentsAsync(
        Guid accommodationId, IEnumerable<Guid> featureIds, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.AccommodationFeatureAssignments.RemoveRange(
            db.AccommodationFeatureAssignments.Where(a => a.AccommodationId == accommodationId));

        db.AccommodationFeatureAssignments.AddRange(featureIds.Select(fId =>
            new AccommodationFeatureAssignment
            {
                AccommodationId = accommodationId,
                FeatureId = fId,
                CreatedAt = DateTime.UtcNow
            }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AccommodationFeature>> GetForAccommodationAsync(
        Guid accommodationId, CancellationToken ct)
        => await db.AccommodationFeatureAssignments.AsNoTracking()
            .Where(a => a.AccommodationId == accommodationId)
            .Select(a => a.Feature)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(ct);

    public async Task SetZoneAssignmentsAsync(
        Guid zoneId, IEnumerable<Guid> featureIds, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ZoneFeatureAssignments.RemoveRange(
            db.ZoneFeatureAssignments.Where(a => a.ZoneId == zoneId));

        db.ZoneFeatureAssignments.AddRange(featureIds.Select(fId =>
            new ZoneFeatureAssignment
            {
                ZoneId = zoneId,
                FeatureId = fId,
                CreatedAt = DateTime.UtcNow
            }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AccommodationFeature>> GetForZoneAsync(
        Guid zoneId, CancellationToken ct)
        => await db.ZoneFeatureAssignments.AsNoTracking()
            .Where(a => a.ZoneId == zoneId)
            .Select(a => a.Feature)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(ct);
}
```

---

### Step 8: Create Services

#### 8a. `AccommodationFeaturesService.cs` *(new)*

**File:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesService.cs`

```csharp
namespace Abuvi.API.Features.Camps;

public class AccommodationFeaturesService(IAccommodationFeaturesRepository repo)
{
    public async Task<IReadOnlyList<AccommodationFeatureResponse>> GetAllAsync(
        bool? activeOnly, CancellationToken ct)
    {
        var features = await repo.GetAllAsync(activeOnly, ct);
        return features.Select(f => f.ToResponse()).ToList().AsReadOnly();
    }

    public async Task<AccommodationFeatureResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var feature = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("AccommodationFeature", id);
        return feature.ToResponse();
    }

    public async Task<AccommodationFeatureResponse> CreateAsync(
        CreateAccommodationFeatureRequest request, CancellationToken ct)
    {
        if (await repo.GetByNameAsync(request.Name, ct) is not null)
            throw new BusinessRuleException("Ya existe una característica con ese nombre");

        var feature = new AccommodationFeature
        {
            Name = request.Name,
            Icon = request.Icon,
            Description = request.Description,
            ApplicabilityLevel = request.ApplicabilityLevel,
            SortOrder = request.SortOrder
        };
        return (await repo.AddAsync(feature, ct)).ToResponse();
    }

    public async Task<AccommodationFeatureResponse> UpdateAsync(
        Guid id, UpdateAccommodationFeatureRequest request, CancellationToken ct)
    {
        var feature = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("AccommodationFeature", id);

        var duplicate = await repo.GetByNameAsync(request.Name, ct);
        if (duplicate is not null && duplicate.Id != id)
            throw new BusinessRuleException("Ya existe una característica con ese nombre");

        feature.Name = request.Name;
        feature.Icon = request.Icon;
        feature.Description = request.Description;
        feature.ApplicabilityLevel = request.ApplicabilityLevel;
        feature.IsActive = request.IsActive;
        feature.SortOrder = request.SortOrder;
        return (await repo.UpdateAsync(feature, ct)).ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var feature = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("AccommodationFeature", id);

        if (await repo.HasAssignmentsAsync(id, ct))
            throw new BusinessRuleException(
                "No se puede eliminar una característica que está en uso. Desactívela en su lugar.");

        await repo.DeleteAsync(feature, ct);
    }
}
```

#### 8b. `AccommodationFeatureAssignmentService.cs` *(new)*

**File:** `src/Abuvi.API/Features/Camps/AccommodationFeatureAssignmentService.cs`

```csharp
namespace Abuvi.API.Features.Camps;

public class AccommodationFeatureAssignmentService(
    IAccommodationFeaturesRepository featuresRepo,
    ICampEditionAccommodationsRepository accommodationsRepo,
    IAccommodationZonesRepository zonesRepo)
{
    public async Task<IReadOnlyList<AccommodationFeatureResponse>> SetAccommodationFeaturesAsync(
        Guid accommodationId, SetFeatureAssignmentsRequest request, CancellationToken ct)
    {
        if (await accommodationsRepo.GetByIdAsync(accommodationId, ct) is null)
            throw new NotFoundException("CampEditionAccommodation", accommodationId);

        await ValidateFeaturesActiveAsync(request.FeatureIds, ct);
        await featuresRepo.SetAccommodationAssignmentsAsync(accommodationId, request.FeatureIds, ct);
        var features = await featuresRepo.GetForAccommodationAsync(accommodationId, ct);
        return features.Select(f => f.ToResponse()).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<AccommodationFeatureResponse>> SetZoneFeaturesAsync(
        Guid zoneId, SetFeatureAssignmentsRequest request, CancellationToken ct)
    {
        if (await zonesRepo.GetByIdAsync(zoneId, ct) is null)
            throw new NotFoundException("AccommodationZone", zoneId);

        await ValidateFeaturesActiveAsync(request.FeatureIds, ct);
        await featuresRepo.SetZoneAssignmentsAsync(zoneId, request.FeatureIds, ct);
        var features = await featuresRepo.GetForZoneAsync(zoneId, ct);
        return features.Select(f => f.ToResponse()).ToList().AsReadOnly();
    }

    private async Task ValidateFeaturesActiveAsync(List<Guid> featureIds, CancellationToken ct)
    {
        if (featureIds.Count == 0) return;

        var features = await featuresRepo.GetByIdsAsync(featureIds, ct);
        var foundIds = features.Select(f => f.Id).ToHashSet();

        var missing = featureIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missing.Count != 0)
            throw new ValidationException($"Las siguientes características no existen: {string.Join(", ", missing)}");

        var inactive = features.Where(f => !f.IsActive).Select(f => f.Name).ToList();
        if (inactive.Count != 0)
            throw new ValidationException(
                $"Las siguientes características están inactivas: {string.Join(", ", inactive)}");
    }
}
```

> **Check existing interfaces:** Verify that `ICampEditionAccommodationsRepository` exposes `GetByIdAsync(Guid, CancellationToken)` and `IAccommodationZonesRepository` exposes `GetByIdAsync(Guid, CancellationToken)`. If the repository interface doesn't have that method, add it and implement it. Alternatively, inject the existing service and use its method.

---

### Step 9: Update Existing Services

#### 9a. `CampEditionAccommodationsService.cs` — Features in response + ZoneId in create/update

1. Find all queries loading `CampEditionAccommodation` and add:

   ```csharp
   .Include(a => a.FeatureAssignments).ThenInclude(fa => fa.Feature)
   ```

2. Update every construction of `CampEditionAccommodationResponse` to add:

   ```csharp
   Features: a.FeatureAssignments.Select(fa => fa.Feature.ToResponse()).ToList().AsReadOnly()
   ```

3. In `CreateAsync` and `UpdateAsync`, apply `ZoneId` from the request to the entity.

#### 9b. `AccommodationZonesService.cs` — Features + MediaItems in response

1. Find queries loading `AccommodationZone` and add:

   ```csharp
   .Include(z => z.FeatureAssignments).ThenInclude(fa => fa.Feature)
   .Include(z => z.MediaItems)
   ```

2. Update every construction of `AccommodationZoneResponse` to add:

   ```csharp
   Features: z.FeatureAssignments.Select(fa => fa.Feature.ToResponse()).ToList().AsReadOnly(),
   MediaItems: z.MediaItems.Select(m => m.ToResponse()).ToList().AsReadOnly()
   ```

---

### Step 10: Create Validators

**File:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesValidators.cs` *(new)*

```csharp
namespace Abuvi.API.Features.Camps;

public class CreateAccommodationFeatureValidator : AbstractValidator<CreateAccommodationFeatureRequest>
{
    public CreateAccommodationFeatureValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la característica es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("El icono es obligatorio")
            .MaximumLength(100).WithMessage("El icono no puede superar los 100 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.Description is not null);

        RuleFor(x => x.ApplicabilityLevel)
            .IsInEnum().WithMessage("El nivel de aplicación no es válido");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden debe ser mayor o igual a 0");
    }
}

public class UpdateAccommodationFeatureValidator : AbstractValidator<UpdateAccommodationFeatureRequest>
{
    public UpdateAccommodationFeatureValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la característica es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("El icono es obligatorio")
            .MaximumLength(100).WithMessage("El icono no puede superar los 100 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.Description is not null);

        RuleFor(x => x.ApplicabilityLevel)
            .IsInEnum().WithMessage("El nivel de aplicación no es válido");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden debe ser mayor o igual a 0");
    }
}

public class SetFeatureAssignmentsValidator : AbstractValidator<SetFeatureAssignmentsRequest>
{
    public SetFeatureAssignmentsValidator()
    {
        RuleFor(x => x.FeatureIds)
            .NotNull().WithMessage("La lista de características no puede ser nula");

        RuleForEach(x => x.FeatureIds)
            .NotEmpty().WithMessage("El identificador de característica no puede ser vacío");
    }
}
```

---

### Step 11: Create `AccommodationFeaturesEndpoints.cs`

**File:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesEndpoints.cs` *(new)*

This file holds all feature-related endpoints: the catalogue CRUD, feature assignments to accommodations, and feature assignments to zones.

```csharp
namespace Abuvi.API.Features.Camps;

public static class AccommodationFeaturesEndpoints
{
    public static void MapAccommodationFeaturesEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Catalogue CRUD ─────────────────────────────────────────────────────
        var catalogue = app.MapGroup("/api/accommodation-features")
            .WithTags("AccommodationFeatures")
            .RequireAuthorization("BoardOrAdmin");

        catalogue.MapGet("/", GetAll).WithName("GetAllAccommodationFeatures")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>();

        catalogue.MapGet("/{id:guid}", GetById).WithName("GetAccommodationFeatureById")
            .Produces<ApiResponse<AccommodationFeatureResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        catalogue.MapPost("/", Create).WithName("CreateAccommodationFeature")
            .Produces<ApiResponse<AccommodationFeatureResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<CreateAccommodationFeatureRequest>>();

        catalogue.MapPut("/{id:guid}", Update).WithName("UpdateAccommodationFeature")
            .Produces<ApiResponse<AccommodationFeatureResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<UpdateAccommodationFeatureRequest>>();

        catalogue.MapDelete("/{id:guid}", Delete).WithName("DeleteAccommodationFeature")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // ── Feature assignments to accommodations ──────────────────────────────
        // Note: base path matches existing accommodation endpoints for consistency
        var accommodationFeatures = app.MapGroup(
                "/api/camps/editions/{editionId:guid}/accommodations/{accommodationId:guid}/features")
            .WithTags("AccommodationFeatures")
            .RequireAuthorization("BoardOrAdmin");

        accommodationFeatures.MapGet("/", GetAccommodationFeatures)
            .WithName("GetAccommodationFeatureAssignments")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>();

        accommodationFeatures.MapPut("/", SetAccommodationFeatures)
            .WithName("SetAccommodationFeatureAssignments")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AddEndpointFilter<ValidationFilter<SetFeatureAssignmentsRequest>>();

        // ── Feature assignments to zones ────────────────────────────────────────
        // Uses the same base path as existing accommodation-zones endpoints
        var zoneFeatures = app.MapGroup(
                "/api/camps/editions/{editionId:guid}/accommodation-zones/{zoneId:guid}/features")
            .WithTags("AccommodationFeatures")
            .RequireAuthorization("BoardOrAdmin");

        zoneFeatures.MapGet("/", GetZoneFeatures)
            .WithName("GetZoneFeatureAssignments")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>();

        zoneFeatures.MapPut("/", SetZoneFeatures)
            .WithName("SetZoneFeatureAssignments")
            .Produces<ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AddEndpointFilter<ValidationFilter<SetFeatureAssignmentsRequest>>();
    }

    // ── Handlers ───────────────────────────────────────────────────────────────

    private static async Task<IResult> GetAll(
        AccommodationFeaturesService service, bool? activeOnly, CancellationToken ct)
    {
        var features = await service.GetAllAsync(activeOnly, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(features));
    }

    private static async Task<IResult> GetById(
        Guid id, AccommodationFeaturesService service, CancellationToken ct)
        => Results.Ok(ApiResponse<AccommodationFeatureResponse>.Ok(
            await service.GetByIdAsync(id, ct)));

    private static async Task<IResult> Create(
        CreateAccommodationFeatureRequest request,
        AccommodationFeaturesService service, CancellationToken ct)
    {
        var feature = await service.CreateAsync(request, ct);
        return Results.Created($"/api/accommodation-features/{feature.Id}",
            ApiResponse<AccommodationFeatureResponse>.Ok(feature));
    }

    private static async Task<IResult> Update(
        Guid id, UpdateAccommodationFeatureRequest request,
        AccommodationFeaturesService service, CancellationToken ct)
        => Results.Ok(ApiResponse<AccommodationFeatureResponse>.Ok(
            await service.UpdateAsync(id, request, ct)));

    private static async Task<IResult> Delete(
        Guid id, AccommodationFeaturesService service, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAccommodationFeatures(
        Guid accommodationId, IAccommodationFeaturesRepository repo, CancellationToken ct)
    {
        var features = await repo.GetForAccommodationAsync(accommodationId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(
            features.Select(f => f.ToResponse()).ToList().AsReadOnly()));
    }

    private static async Task<IResult> SetAccommodationFeatures(
        Guid accommodationId, SetFeatureAssignmentsRequest request,
        AccommodationFeatureAssignmentService service, CancellationToken ct)
        => Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(
            await service.SetAccommodationFeaturesAsync(accommodationId, request, ct)));

    private static async Task<IResult> GetZoneFeatures(
        Guid zoneId, IAccommodationFeaturesRepository repo, CancellationToken ct)
    {
        var features = await repo.GetForZoneAsync(zoneId, ct);
        return Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(
            features.Select(f => f.ToResponse()).ToList().AsReadOnly()));
    }

    private static async Task<IResult> SetZoneFeatures(
        Guid zoneId, SetFeatureAssignmentsRequest request,
        AccommodationFeatureAssignmentService service, CancellationToken ct)
        => Results.Ok(ApiResponse<IReadOnlyList<AccommodationFeatureResponse>>.Ok(
            await service.SetZoneFeaturesAsync(zoneId, request, ct)));
}
```

---

### Step 12: Update `MediaItemsService.cs`

1. Add `Guid? accommodationId` and `Guid? zoneId` parameters to the `GetAllAsync` (or equivalent list) method.
2. Filter: `if (accommodationId.HasValue) query = query.Where(m => m.AccommodationId == accommodationId);` and same for `zoneId`.
3. In `CreateAsync`, when `request.AccommodationId.HasValue || request.ZoneId.HasValue`, force `IsApproved = true` and `IsPublished = false` (internal Board media, no approval workflow).
4. Set `mediaItem.AccommodationId = request.AccommodationId` and `mediaItem.ZoneId = request.ZoneId` when creating.

---

### Step 13: Update `MediaItemsEndpoints.cs`

Add `accommodationId` and `zoneId` as optional query parameters to `GET /api/media-items` and pass them to the service.

---

### Step 14: Update `BlobStorageEndpoints.cs`

Add `"accommodation-media"` to the list of allowed upload folder names.

---

### Step 15: Update `Program.cs`

Add DI registrations (after existing camp service registrations):

```csharp
builder.Services.AddScoped<IAccommodationFeaturesRepository, AccommodationFeaturesRepository>();
builder.Services.AddScoped<AccommodationFeaturesService>();
builder.Services.AddScoped<AccommodationFeatureAssignmentService>();
```

Add endpoint registration (after existing camp endpoint registrations):

```csharp
app.MapAccommodationFeaturesEndpoints();
```

---

### Step 16: Generate and Review EF Core Migration

```bash
dotnet ef migrations add AddAccommodationFeaturesAndZoneAssignments --project src/Abuvi.API
```

**Expected operations in the generated migration:**

1. `CreateTable` — `accommodation_features` (id, name, icon, description, applicability_level, is_active, sort_order, created_at, updated_at) + unique index on `name`
2. `CreateTable` — `accommodation_feature_assignments` (accommodation_id, feature_id, created_at) + FK cascade on `accommodation_id`, restrict on `feature_id`
3. `CreateTable` — `zone_feature_assignments` (zone_id, feature_id, created_at) + FK cascade on `zone_id`, restrict on `feature_id`
4. `AddColumn` — `media_items.accommodation_id` (uuid, nullable) + FK to `camp_edition_accommodations`
5. `AddColumn` — `media_items.zone_id` (uuid, nullable) + FK to `accommodation_zones`

**No migration changes expected for `camp_edition_accommodations`** — `zone_id` already exists from the previous migration. The `FeatureAssignments` collection on `CampEditionAccommodation` is driven by the join table, not a column.

```bash
dotnet ef database update --project src/Abuvi.API
```

> Always review the generated `.cs` migration file before applying. Confirm all column names are `snake_case`.

---

### Step 17: Write Unit Tests

#### `AccommodationFeaturesServiceTests.cs`

```
GetAllAsync_WhenFeaturesExist_ReturnsAllFeatureResponses
GetAllAsync_WithActiveOnlyTrue_ReturnsOnlyActiveFeatures
GetAllAsync_WithActiveOnlyFalse_ReturnsAllFeatures
GetByIdAsync_WhenFeatureExists_ReturnsFeatureResponse
GetByIdAsync_WhenFeatureDoesNotExist_ThrowsNotFoundException
CreateAsync_WithValidRequest_SetsDefaultIsActiveTrue
CreateAsync_WithDuplicateName_ThrowsBusinessRuleException
UpdateAsync_WithValidRequest_UpdatesAllFields
UpdateAsync_WhenFeatureNotFound_ThrowsNotFoundException
UpdateAsync_WithDuplicateNameOnAnotherFeature_ThrowsBusinessRuleException
DeleteAsync_WhenNoAssignments_DeletesSuccessfully
DeleteAsync_WhenHasAccommodationAssignments_ThrowsBusinessRuleException
DeleteAsync_WhenHasZoneAssignments_ThrowsBusinessRuleException
DeleteAsync_WhenFeatureNotFound_ThrowsNotFoundException
```

#### `AccommodationFeatureAssignmentServiceTests.cs`

```
SetAccommodationFeaturesAsync_WithValidFeatureIds_ReturnsUpdatedList
SetAccommodationFeaturesAsync_WithEmptyList_RemovesAllAndReturnsEmpty
SetAccommodationFeaturesAsync_WithNonExistentFeatureId_ThrowsValidationException
SetAccommodationFeaturesAsync_WithInactiveFeature_ThrowsValidationException
SetAccommodationFeaturesAsync_WhenAccommodationNotFound_ThrowsNotFoundException
SetZoneFeaturesAsync_WithValidFeatureIds_ReturnsUpdatedList
SetZoneFeaturesAsync_WhenZoneNotFound_ThrowsNotFoundException
```

#### `AccommodationFeaturesBuilder.cs`

```csharp
public class AccommodationFeatureBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Feature";
    private string _icon = "🛏";
    private FeatureApplicabilityLevel _level = FeatureApplicabilityLevel.Any;
    private bool _isActive = true;
    private int _sortOrder = 0;

    public AccommodationFeatureBuilder WithId(Guid id) { _id = id; return this; }
    public AccommodationFeatureBuilder WithName(string name) { _name = name; return this; }
    public AccommodationFeatureBuilder WithIsActive(bool active) { _isActive = active; return this; }
    public AccommodationFeatureBuilder WithSortOrder(int order) { _sortOrder = order; return this; }

    public AccommodationFeature Build() => new()
    {
        Id = _id, Name = _name, Icon = _icon,
        ApplicabilityLevel = _level, IsActive = _isActive, SortOrder = _sortOrder,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
```

---

### Step 18: Write Integration Tests

**File:** `src/Abuvi.Tests/Integration/Features/Camps/AccommodationFeaturesIntegrationTests.cs`

```
AccommodationFeature_CanBeSavedAndRetrievedFromDatabase
AccommodationFeatureAssignment_CascadeDeletesWhenAccommodationDeleted
AccommodationFeatureAssignment_RestrictsDeleteWhenFeatureHasAssignments
ZoneFeatureAssignment_CascadeDeletesWhenZoneDeleted
ZoneFeatureAssignment_RestrictsDeleteWhenFeatureHasAssignments
MediaItem_WithAccommodationId_IsSavedAndFilteredCorrectly
MediaItem_WithZoneId_IsSavedAndFilteredCorrectly
MediaItem_WithAccommodationId_IsAutoApproved
```

---

### Step 19: Update Documentation

1. **`ai-specs/specs/data-model.md`** — Add `AccommodationFeature`, `AccommodationFeatureAssignment`, `ZoneFeatureAssignment`. Document that `ZoneFeatureAssignment.ZoneId` → `accommodation_zones.id`. Update `CampEditionAccommodation` (new `FeatureAssignments` collection) and `AccommodationZone` (new `FeatureAssignments` + `MediaItems` collections). Update `MediaItem` to show `AccommodationId`/`ZoneId` FKs.
2. **`ai-specs/specs/api-spec.yml`** — Add new endpoints under `AccommodationFeatures`.

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Create `AccommodationFeaturesModels.cs`
3. Step 2 — Modify `CampsModels.cs` (nav props + DTO updates)
4. Step 3 — Modify `MediaItemsModels.cs`
5. Step 4 — Create 3 new EF Core configurations
6. Step 5 — Modify 3 existing EF Core configurations
7. Step 6 — Update `AbuviDbContext`
8. Step 7 — Create repository interface + implementation
9. Step 8 — Create services
10. Step 9 — Update existing services (Accommodations + Zones)
11. Step 10 — Create validators
12. Step 11 — Create endpoints
13. Step 12–14 — Update MediaItems service, endpoints, blob storage
14. Step 15 — Register in `Program.cs`
15. Step 16 — Generate + apply migration
16. Step 17–18 — Tests
17. Step 19 — Documentation

---

## Testing Checklist

- [ ] `dotnet build` — zero errors, zero warnings (`TreatWarningsAsErrors` is enabled)
- [ ] All existing tests pass after model changes (especially any tests that construct `CampEditionAccommodationResponse` or `AccommodationZoneResponse` — adding new required record fields breaks existing call sites)
- [ ] Unit tests: 14 `AccommodationFeaturesService` + 7 `AccommodationFeatureAssignmentService`
- [ ] Integration tests: 8 DB-level tests
- [ ] Coverage ≥ 90% on new code

---

## Error Response Format

| Scenario | HTTP | Error Code |
|---|---|---|
| Feature not found | 404 | `NOT_FOUND` |
| Duplicate feature name | 409 | `BUSINESS_RULE_VIOLATION` |
| Feature in use (delete blocked) | 409 | `BUSINESS_RULE_VIOLATION` |
| Inactive/missing feature in assignment | 400 | `VALIDATION_ERROR` |
| Accommodation not found (in assignment) | 404 | `NOT_FOUND` |
| Zone not found (in assignment) | 404 | `NOT_FOUND` |

---

## Notes

1. **Zone = AccommodationZone.** The `Zone` concept in the spec maps to the existing `AccommodationZone` entity (table `accommodation_zones`). No new `Zone` entity or `zones` table.

2. **Zone feature endpoint URL.** The feature assignment endpoint for zones lives under `/api/camps/editions/{editionId}/accommodation-zones/{zoneId}/features` (matching the existing zone URL pattern), NOT `/api/camps/editions/{editionId}/zones/{zoneId}/features` as the spec states.

3. **Record field ordering matters.** Adding new fields to `CampEditionAccommodationResponse` and `AccommodationZoneResponse` (which are C# `record` types) will break every call site that constructs them positionally. Search for all construction sites with `new CampEditionAccommodationResponse(` and `new AccommodationZoneResponse(` before building.

4. **Validation messages — Spanish.** All `.WithMessage()` calls must be in Spanish.

5. **Transactions.** The `SetAccommodationAssignmentsAsync` and `SetZoneAssignmentsAsync` repository methods run DELETE + INSERT inside a single DB transaction.

6. **`AsNoTracking()`.** All read-only queries must use `AsNoTracking()`.

7. **`TreatWarningsAsErrors` is on.** Nullable reference type warnings, unused variables, etc. fail the build.

8. **`ValidationException` class.** Verify it exists in `Common/` and is handled in `GlobalExceptionMiddleware` as 400. If not, create it following the same pattern as `BusinessRuleException`.

9. **Check existing repository interfaces.** Before injecting `ICampEditionAccommodationsRepository` and `IAccommodationZonesRepository` into `AccommodationFeatureAssignmentService`, confirm these interfaces expose `GetByIdAsync(Guid, CancellationToken)`. Add the method if missing.

---

## Next Steps After Implementation

- Inform the frontend team that zone feature endpoints are at `.../accommodation-zones/{zoneId}/features` (not `.../zones/{zoneId}/features`).
- The encaje de bolillos board endpoint (`GET /api/camp-editions/{editionId}/assignment-status`) will need to include features in each accommodation's response — that update belongs in the encaje de bolillos spec, not here.

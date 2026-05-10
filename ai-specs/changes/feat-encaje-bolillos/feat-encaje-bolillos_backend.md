# Backend Implementation Plan: feat-encaje-bolillos — Accommodation Assignment System

## Overview

Implement the backend for the "Encaje de Bolillos" accommodation assignment feature. This covers **Task 1 (Data Model)** and **Task 2 (API Extensions)** from the enriched spec at `ai-specs/changes/feat-encaje-bolillos/feat-encaje-bolillos_enriched.md`.

Everything lives inside the existing `Camps` vertical slice (`src/Abuvi.API/Features/Camps/`). New files follow the same naming and injection conventions as `CampEditionAccommodationsService.cs` and related files.

**Important scope note:** The frontend (Task 3) is a separate ticket. This plan ends at the API layer.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

**New files to create:**

| File | Purpose |
|------|---------|
| `AccommodationZonesService.cs` | Zone business logic |
| `IAccommodationZonesRepository.cs` | Zone repository interface |
| `AccommodationZonesRepository.cs` | Zone data access |
| `AccommodationAssignmentProposalsService.cs` | Proposal management logic |
| `IAccommodationAssignmentProposalsRepository.cs` | Proposal repository interface |
| `AccommodationAssignmentProposalsRepository.cs` | Proposal data access |
| `AccommodationAssignmentsService.cs` | Assignment logic + auto-assign |
| `IAccommodationAssignmentsRepository.cs` | Assignment repository interface |
| `AccommodationAssignmentsRepository.cs` | Assignment data access |
| `AutoAssignService.cs` | Stateless greedy auto-assign algorithm |

**Files to modify:**

| File | What changes |
|------|-------------|
| `CampsModels.cs` | Add 3 new entities + all new DTOs |
| `CampsEndpoints.cs` | Add 3 new endpoint groups |
| `Data/AbuviDbContext.cs` | Add 3 new DbSets |
| `Data/Configurations/CampEditionAccommodationConfiguration.cs` | Add `ZoneId` FK |
| `Program.cs` | Register new services and repositories |

**New EF configurations to create:**

| File | Purpose |
|------|---------|
| `Data/Configurations/AccommodationZoneConfiguration.cs` | Zone entity config |
| `Data/Configurations/AccommodationAssignmentProposalConfiguration.cs` | Proposal entity config |
| `Data/Configurations/AccommodationAssignmentConfiguration.cs` | Assignment entity config |

---

## Implementation Steps

### Step 0: Create Feature Branch

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-encaje-bolillos-backend
git branch
```

> **Do not work on `feat-encaje-bolillos` directly** — that branch is for tracking. Create `feature/feat-encaje-bolillos-backend` as the implementation branch.

---

### Step 1: Add New Entities to `CampsModels.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add the following three entities and all associated DTOs at the end of the file (or after the existing `CampEditionAccommodation` class block). Keep the same namespace `Abuvi.API.Features.Camps`.

#### 1a. `AccommodationZone` entity

```csharp
public class AccommodationZone
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public AccommodationType AccommodationType { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MaxCapacity { get; set; }
    public string? DistributionNotes { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CampEdition CampEdition { get; set; } = null!;
    public ICollection<CampEditionAccommodation> Accommodations { get; set; } = [];
}
```

#### 1b. `AccommodationAssignmentProposal` entity

```csharp
public class AccommodationAssignmentProposal
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = false;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CampEdition CampEdition { get; set; } = null!;
    public ICollection<AccommodationAssignment> Assignments { get; set; } = [];
}
```

#### 1c. `AccommodationAssignment` entity

```csharp
public class AccommodationAssignment
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid AccommodationId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AccommodationAssignmentProposal Proposal { get; set; } = null!;
    public Registration Registration { get; set; } = null!;
    public CampEditionAccommodation Accommodation { get; set; } = null!;
}
```

#### 1d. Extend `CampEditionAccommodation`

Add two new properties to the existing `CampEditionAccommodation` class:

```csharp
public Guid? ZoneId { get; set; }
public AccommodationZone? Zone { get; set; }
```

#### 1e. Zone DTOs

```csharp
public record CreateAccommodationZoneRequest(
    AccommodationType AccommodationType,
    string Name,
    int? MaxCapacity,
    string? DistributionNotes,
    int SortOrder = 0
);

public record UpdateAccommodationZoneRequest(
    string Name,
    int? MaxCapacity,
    string? DistributionNotes,
    int SortOrder
);

public record AttachAccommodationsToZoneRequest(IReadOnlyList<Guid> AccommodationIds);

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
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 1f. Proposal DTOs

```csharp
public record CreateAccommodationAssignmentProposalRequest(
    string Name,
    string? Notes,
    Guid? CopyFromProposalId = null
);

public record UpdateAccommodationAssignmentProposalRequest(string Name, string? Notes);

public record AccommodationAssignmentProposalSummaryResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    string? Notes,
    bool IsActive,
    int AssignmentCount,
    int UnassignedCount,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 1g. Assignment DTOs

```csharp
public record AssignmentEntry(Guid RegistrationId, Guid AccommodationId);

public record SingleAssignRequest(Guid AccommodationId);

public record BulkAssignRequest(IReadOnlyList<AssignmentEntry> Assignments);

public record AutoAssignRequest(bool OverwriteExisting = false);

public record AccommodationPreferenceItem(Guid AccommodationId, int PreferenceOrder);

public record AssignmentFamilyResponse(
    Guid RegistrationId,
    Guid FamilyUnitId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    int AdultCount,
    int ChildCount,
    bool HasPet,
    string? SpecialNeeds,
    string? CampatesPreference,
    IReadOnlyList<AccommodationPreferenceItem> AccommodationPreferences
);

public record AssignmentAccommodationResponse(
    Guid Id,
    string Name,
    AccommodationType Type,
    int? Capacity,
    bool CountByFamily,
    Guid? ZoneId,
    string? ZoneName,
    int SortOrder
);

public record ProposalAssignmentStateResponse(
    Guid ProposalId,
    IReadOnlyList<AssignmentFamilyResponse> Families,
    IReadOnlyList<AssignmentAccommodationResponse> Accommodations,
    IReadOnlyList<AssignmentEntry> Assignments
);

public record AssignmentReportFamilyRow(
    Guid RegistrationId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    string? AccommodationName,
    string? ZoneName
);

public record AssignmentReportGroupResponse(
    string GroupKey,
    string GroupLabel,
    int TotalCapacity,
    int UsedCapacity,
    IReadOnlyList<AssignmentReportFamilyRow> Families
);
```

#### 1h. Update `CampEditionAccommodationResponse`

Add two nullable fields to the **existing** record (add at the end of the constructor parameters):

```csharp
// Before (existing):
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
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// After (add ZoneId and ZoneName):
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
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

Update the `ToResponse()` extension method in `CampEditionAccommodationsService.cs` to pass `a.ZoneId, a.Zone?.Name` in the correct positions.

---

### Step 2: EF Core Configurations

#### 2a. `AccommodationZoneConfiguration.cs`

**File:** `src/Abuvi.API/Data/Configurations/AccommodationZoneConfiguration.cs`

```csharp
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationZoneConfiguration : IEntityTypeConfiguration<AccommodationZone>
{
    public void Configure(EntityTypeBuilder<AccommodationZone> builder)
    {
        builder.ToTable("accommodation_zones");

        builder.HasKey(z => z.Id);
        builder.Property(z => z.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(z => z.CampEditionId)
            .IsRequired()
            .HasColumnName("camp_edition_id");

        builder.Property(z => z.AccommodationType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("accommodation_type");

        builder.Property(z => z.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(z => z.MaxCapacity)
            .HasColumnName("max_capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AccommodationZones_MaxCapacity",
            "max_capacity IS NULL OR max_capacity > 0"));

        builder.Property(z => z.DistributionNotes)
            .HasMaxLength(500)
            .HasColumnName("distribution_notes");

        builder.Property(z => z.SortOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("sort_order");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AccommodationZones_SortOrder",
            "sort_order >= 0"));

        builder.Property(z => z.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(z => z.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(z => z.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(z => z.CampEdition)
            .WithMany()
            .HasForeignKey(z => z.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### 2b. Update `CampEditionAccommodationConfiguration.cs`

Add the `ZoneId` FK mapping inside the existing `Configure` method, after the existing `SortOrder` constraint:

```csharp
builder.Property(e => e.ZoneId)
    .HasColumnName("zone_id");

builder.HasOne(e => e.Zone)
    .WithMany(z => z.Accommodations)
    .HasForeignKey(e => e.ZoneId)
    .OnDelete(DeleteBehavior.SetNull);
```

#### 2c. `AccommodationAssignmentProposalConfiguration.cs`

**File:** `src/Abuvi.API/Data/Configurations/AccommodationAssignmentProposalConfiguration.cs`

```csharp
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationAssignmentProposalConfiguration
    : IEntityTypeConfiguration<AccommodationAssignmentProposal>
{
    public void Configure(EntityTypeBuilder<AccommodationAssignmentProposal> builder)
    {
        builder.ToTable("accommodation_assignment_proposals");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.CampEditionId)
            .IsRequired()
            .HasColumnName("camp_edition_id");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(p => p.Notes)
            .HasMaxLength(500)
            .HasColumnName("notes");

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_active");

        builder.Property(p => p.CreatedByUserId)
            .IsRequired()
            .HasColumnName("created_by_user_id");

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(p => p.CampEdition)
            .WithMany()
            .HasForeignKey(p => p.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### 2d. `AccommodationAssignmentConfiguration.cs`

**File:** `src/Abuvi.API/Data/Configurations/AccommodationAssignmentConfiguration.cs`

```csharp
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationAssignmentConfiguration
    : IEntityTypeConfiguration<AccommodationAssignment>
{
    public void Configure(EntityTypeBuilder<AccommodationAssignment> builder)
    {
        builder.ToTable("accommodation_assignments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.ProposalId)
            .IsRequired()
            .HasColumnName("proposal_id");

        builder.Property(a => a.RegistrationId)
            .IsRequired()
            .HasColumnName("registration_id");

        builder.Property(a => a.AccommodationId)
            .IsRequired()
            .HasColumnName("accommodation_id");

        builder.Property(a => a.AssignedByUserId)
            .IsRequired()
            .HasColumnName("assigned_by_user_id");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // One registration per proposal (cannot be in two places at once)
        builder.HasIndex(a => new { a.ProposalId, a.RegistrationId })
            .IsUnique()
            .HasDatabaseName("IX_AccommodationAssignments_Proposal_Registration");

        builder.HasOne(a => a.Proposal)
            .WithMany(p => p.Assignments)
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Registration)
            .WithMany()
            .HasForeignKey(a => a.RegistrationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Accommodation)
            .WithMany()
            .HasForeignKey(a => a.AccommodationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

### Step 3: Update `AbuviDbContext.cs`

Add three new DbSet properties (follow the existing pattern):

```csharp
public DbSet<AccommodationZone> AccommodationZones => Set<AccommodationZone>();
public DbSet<AccommodationAssignmentProposal> AccommodationAssignmentProposals
    => Set<AccommodationAssignmentProposal>();
public DbSet<AccommodationAssignment> AccommodationAssignments
    => Set<AccommodationAssignment>();
```

Place them after the existing `CampEditionAccommodations` DbSet.

---

### Step 4: Create EF Core Migration

```bash
cd src/Abuvi.API
dotnet ef migrations add AddAccommodationZonesAndAssignmentProposals
```

**Review the generated migration before applying.** Verify:
- `accommodation_zones` table created with all columns and constraints.
- `accommodation_assignment_proposals` table created.
- `accommodation_assignments` table created with the unique index.
- `zone_id` column added to `camp_edition_accommodations` with FK to `accommodation_zones`.

Apply:
```bash
dotnet ef database update
```

---

### Step 5: Zone Repository

#### 5a. Interface: `IAccommodationZonesRepository.cs`

**File:** `src/Abuvi.API/Features/Camps/IAccommodationZonesRepository.cs`

```csharp
namespace Abuvi.API.Features.Camps;

public interface IAccommodationZonesRepository
{
    Task<AccommodationZone?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<AccommodationZone>> GetByCampEditionAsync(Guid campEditionId, CancellationToken ct = default);
    Task AddAsync(AccommodationZone zone, CancellationToken ct = default);
    Task UpdateAsync(AccommodationZone zone, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasActiveAssignmentsAsync(Guid zoneId, CancellationToken ct = default);
    Task AttachAccommodationsAsync(Guid zoneId, IReadOnlyList<Guid> accommodationIds, CancellationToken ct = default);
}
```

#### 5b. Implementation: `AccommodationZonesRepository.cs`

**File:** `src/Abuvi.API/Features/Camps/AccommodationZonesRepository.cs`

```csharp
using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Camps;

public class AccommodationZonesRepository(AbuviDbContext db) : IAccommodationZonesRepository
{
    public async Task<AccommodationZone?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.AccommodationZones
            .AsNoTracking()
            .Include(z => z.Accommodations)
            .FirstOrDefaultAsync(z => z.Id == id, ct);

    public async Task<List<AccommodationZone>> GetByCampEditionAsync(
        Guid campEditionId,
        CancellationToken ct = default)
        => await db.AccommodationZones
            .AsNoTracking()
            .Where(z => z.CampEditionId == campEditionId && z.IsActive)
            .Include(z => z.Accommodations)
            .OrderBy(z => z.AccommodationType.ToString())
            .ThenBy(z => z.SortOrder)
            .ThenBy(z => z.Name)
            .ToListAsync(ct);

    public async Task AddAsync(AccommodationZone zone, CancellationToken ct = default)
    {
        db.AccommodationZones.Add(zone);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AccommodationZone zone, CancellationToken ct = default)
    {
        db.AccommodationZones.Update(zone);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var zone = await db.AccommodationZones.FindAsync([id], ct);
        if (zone is not null)
        {
            db.AccommodationZones.Remove(zone);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> HasActiveAssignmentsAsync(Guid zoneId, CancellationToken ct = default)
        => await db.AccommodationAssignments
            .AnyAsync(a => a.Accommodation.ZoneId == zoneId, ct);

    public async Task AttachAccommodationsAsync(
        Guid zoneId,
        IReadOnlyList<Guid> accommodationIds,
        CancellationToken ct = default)
    {
        // Clear existing zone references for this zone
        var currentlyAttached = await db.CampEditionAccommodations
            .Where(a => a.ZoneId == zoneId)
            .ToListAsync(ct);

        foreach (var acc in currentlyAttached)
            acc.ZoneId = null;

        // Set new ones
        var newOnes = await db.CampEditionAccommodations
            .Where(a => accommodationIds.Contains(a.Id))
            .ToListAsync(ct);

        foreach (var acc in newOnes)
            acc.ZoneId = zoneId;

        await db.SaveChangesAsync(ct);
    }
}
```

---

### Step 6: Zone Service

**File:** `src/Abuvi.API/Features/Camps/AccommodationZonesService.cs`

```csharp
using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Features.Camps;

public class AccommodationZonesService(
    IAccommodationZonesRepository zonesRepository,
    ICampEditionsRepository editionsRepository)
{
    public async Task<List<AccommodationZoneResponse>> GetByEditionAsync(
        Guid campEditionId,
        CancellationToken ct = default)
    {
        var zones = await zonesRepository.GetByCampEditionAsync(campEditionId, ct);
        return zones.Select(ToResponse).ToList();
    }

    public async Task<AccommodationZoneResponse> CreateAsync(
        Guid campEditionId,
        CreateAccommodationZoneRequest request,
        CancellationToken ct = default)
    {
        var edition = await editionsRepository.GetByIdAsync(campEditionId, ct)
            ?? throw new NotFoundException("CampEdition", campEditionId);

        var zone = new AccommodationZone
        {
            Id = Guid.NewGuid(),
            CampEditionId = campEditionId,
            AccommodationType = request.AccommodationType,
            Name = request.Name,
            MaxCapacity = request.MaxCapacity,
            DistributionNotes = request.DistributionNotes,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await zonesRepository.AddAsync(zone, ct);
        return ToResponse(zone);
    }

    public async Task<AccommodationZoneResponse> UpdateAsync(
        Guid zoneId,
        UpdateAccommodationZoneRequest request,
        CancellationToken ct = default)
    {
        var zone = await zonesRepository.GetByIdAsync(zoneId, ct)
            ?? throw new NotFoundException("AccommodationZone", zoneId);

        zone.Name = request.Name;
        zone.MaxCapacity = request.MaxCapacity;
        zone.DistributionNotes = request.DistributionNotes;
        zone.SortOrder = request.SortOrder;
        zone.UpdatedAt = DateTime.UtcNow;

        await zonesRepository.UpdateAsync(zone, ct);
        return ToResponse(zone);
    }

    public async Task DeleteAsync(Guid zoneId, CancellationToken ct = default)
    {
        var zone = await zonesRepository.GetByIdAsync(zoneId, ct)
            ?? throw new NotFoundException("AccommodationZone", zoneId);

        if (await zonesRepository.HasActiveAssignmentsAsync(zoneId, ct))
            throw new BusinessRuleException(
                "No se puede eliminar la zona porque tiene familias asignadas en alguna propuesta activa.");

        await zonesRepository.DeleteAsync(zoneId, ct);
    }

    public async Task<AccommodationZoneResponse> AttachAccommodationsAsync(
        Guid zoneId,
        AttachAccommodationsToZoneRequest request,
        CancellationToken ct = default)
    {
        var zone = await zonesRepository.GetByIdAsync(zoneId, ct)
            ?? throw new NotFoundException("AccommodationZone", zoneId);

        await zonesRepository.AttachAccommodationsAsync(zoneId, request.AccommodationIds, ct);

        var updated = await zonesRepository.GetByIdAsync(zoneId, ct);
        return ToResponse(updated!);
    }

    private static AccommodationZoneResponse ToResponse(AccommodationZone z) =>
        new(
            z.Id,
            z.CampEditionId,
            z.AccommodationType,
            z.Name,
            z.MaxCapacity,
            z.DistributionNotes,
            z.SortOrder,
            z.IsActive,
            z.Accommodations.Select(a => a.Id).ToList(),
            z.CreatedAt,
            z.UpdatedAt
        );
}
```

---

### Step 7: Zone Validators

**File:** `src/Abuvi.API/Features/Camps/AccommodationZoneValidators.cs`

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Camps;

public class CreateAccommodationZoneRequestValidator
    : AbstractValidator<CreateAccommodationZoneRequest>
{
    public CreateAccommodationZoneRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la zona es obligatorio")
            .MaximumLength(100).WithMessage("El nombre de la zona no puede superar 100 caracteres");

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).When(x => x.MaxCapacity.HasValue)
            .WithMessage("La capacidad máxima debe ser mayor que cero");

        RuleFor(x => x.DistributionNotes)
            .MaximumLength(500).When(x => x.DistributionNotes is not null)
            .WithMessage("Las notas de distribución no pueden superar 500 caracteres");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El orden de visualización debe ser mayor o igual a cero");
    }
}

public class UpdateAccommodationZoneRequestValidator
    : AbstractValidator<UpdateAccommodationZoneRequest>
{
    public UpdateAccommodationZoneRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la zona es obligatorio")
            .MaximumLength(100).WithMessage("El nombre de la zona no puede superar 100 caracteres");

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).When(x => x.MaxCapacity.HasValue)
            .WithMessage("La capacidad máxima debe ser mayor que cero");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El orden de visualización debe ser mayor o igual a cero");
    }
}
```

---

### Step 8: Proposal Repository and Service

#### 8a. Interface: `IAccommodationAssignmentProposalsRepository.cs`

```csharp
namespace Abuvi.API.Features.Camps;

public interface IAccommodationAssignmentProposalsRepository
{
    Task<AccommodationAssignmentProposal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<AccommodationAssignmentProposal>> GetByCampEditionAsync(Guid campEditionId, CancellationToken ct = default);
    Task AddAsync(AccommodationAssignmentProposal proposal, CancellationToken ct = default);
    Task UpdateAsync(AccommodationAssignmentProposal proposal, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ActivateAsync(Guid proposalId, Guid campEditionId, CancellationToken ct = default);
    Task<int> CountAssignmentsAsync(Guid proposalId, CancellationToken ct = default);
    Task<int> CountRegistrationsAsync(Guid campEditionId, CancellationToken ct = default);
}
```

#### 8b. Implementation: `AccommodationAssignmentProposalsRepository.cs`

Key methods:

- `GetByCampEditionAsync` — no-tracking, ordered by `IsActive DESC, CreatedAt ASC`.
- `ActivateAsync` — **single transaction**: set `IsActive = false` for all proposals of the edition, then set `IsActive = true` for the target. Use `ExecuteUpdateAsync` for efficiency:
  ```csharp
  public async Task ActivateAsync(Guid proposalId, Guid campEditionId, CancellationToken ct = default)
  {
      await db.AccommodationAssignmentProposals
          .Where(p => p.CampEditionId == campEditionId)
          .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsActive, false), ct);

      await db.AccommodationAssignmentProposals
          .Where(p => p.Id == proposalId)
          .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsActive, true), ct);
  }
  ```
- `CountAssignmentsAsync` — counts rows in `AccommodationAssignments` where `ProposalId == proposalId`.
- `CountRegistrationsAsync` — counts `Registrations` where `CampEditionId == campEditionId` and `Status != Cancelled`.

#### 8c. Service: `AccommodationAssignmentProposalsService.cs`

Key operations:
- `CreateAsync`: if `CopyFromProposalId` is set, load source assignments and create new ones for the new proposal.
- `ActivateAsync`: delegate to repository; single call.
- `DeleteAsync`: throw `BusinessRuleException` if the proposal is active and is the only one.
- `GetSummaryAsync`: returns `AccommodationAssignmentProposalSummaryResponse` with `AssignmentCount` and `UnassignedCount` (total registrations − assigned).

#### 8d. Proposal validators

```csharp
public class CreateAccommodationAssignmentProposalRequestValidator
    : AbstractValidator<CreateAccommodationAssignmentProposalRequest>
{
    public CreateAccommodationAssignmentProposalRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la propuesta es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres");

        RuleFor(x => x.Notes)
            .MaximumLength(500).When(x => x.Notes is not null);
    }
}
```

---

### Step 9: Assignment Repository and Service

#### 9a. Interface: `IAccommodationAssignmentsRepository.cs`

```csharp
namespace Abuvi.API.Features.Camps;

public interface IAccommodationAssignmentsRepository
{
    Task<ProposalAssignmentStateResponse> GetAssignmentStateAsync(
        Guid campEditionId, Guid proposalId, CancellationToken ct = default);

    Task AssignAsync(
        Guid proposalId, Guid registrationId, Guid accommodationId,
        Guid assignedByUserId, CancellationToken ct = default);

    Task UnassignAsync(Guid proposalId, Guid registrationId, CancellationToken ct = default);

    Task BulkReplaceAsync(
        Guid proposalId, Guid campEditionId,
        IReadOnlyList<AssignmentEntry> assignments,
        Guid assignedByUserId, CancellationToken ct = default);

    Task<bool> ProposalBelongsToEditionAsync(
        Guid proposalId, Guid campEditionId, CancellationToken ct = default);

    Task<bool> RegistrationBelongsToEditionAsync(
        Guid registrationId, Guid campEditionId, CancellationToken ct = default);

    Task<bool> AccommodationBelongsToEditionAsync(
        Guid accommodationId, Guid campEditionId, CancellationToken ct = default);
}
```

#### 9b. Implementation: `AccommodationAssignmentsRepository.cs`

The most complex repository. Critical methods:

**`GetAssignmentStateAsync`** — single compound query joining Registrations, FamilyUnits, Users, RegistrationMembers, RegistrationAccommodationPreferences, CampEditionAccommodations, AccommodationZones, AccommodationAssignments. Use `.AsNoTracking()` and project into DTOs directly. Keep joins eager to avoid N+1.

**`BulkReplaceAsync`** — run in a transaction:
1. Delete all existing assignments for the proposal.
2. Validate: each `RegistrationId` must belong to the edition; each `AccommodationId` must belong to the edition.
3. Validate capacity: group assignments by accommodation, compute occupancy, reject if any accommodation is over capacity.
4. Insert new assignments in bulk.

```csharp
public async Task BulkReplaceAsync(
    Guid proposalId, Guid campEditionId,
    IReadOnlyList<AssignmentEntry> assignments,
    Guid assignedByUserId, CancellationToken ct = default)
{
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    try
    {
        // 1. Validate all registrations belong to edition
        var validRegistrationIds = await db.Registrations
            .Where(r => r.CampEditionId == campEditionId && assignments.Select(a => a.RegistrationId).Contains(r.Id))
            .Select(r => r.Id)
            .ToHashSetAsync(ct);

        var invalidRegs = assignments.Where(a => !validRegistrationIds.Contains(a.RegistrationId)).ToList();
        if (invalidRegs.Count > 0)
            throw new BusinessRuleException("Algunas inscripciones no pertenecen a esta edición del campamento.");

        // 2. Validate accommodations
        var accommodations = await db.CampEditionAccommodations
            .Where(a => a.CampEditionId == campEditionId)
            .ToListAsync(ct);

        var validAccIds = accommodations.Select(a => a.Id).ToHashSet();
        if (assignments.Any(a => !validAccIds.Contains(a.AccommodationId)))
            throw new BusinessRuleException("Alguno de los alojamientos no pertenece a esta edición del campamento.");

        // 3. Validate capacity
        var regSizes = await db.RegistrationMembers
            .Where(m => validRegistrationIds.Contains(m.RegistrationId))
            .GroupBy(m => m.RegistrationId)
            .Select(g => new { RegistrationId = g.Key, Size = g.Count() })
            .ToDictionaryAsync(x => x.RegistrationId, x => x.Size, ct);

        var byFamilyTypes = new[] { AccommodationType.Caravan, AccommodationType.Tent };

        foreach (var accGroup in assignments.GroupBy(a => a.AccommodationId))
        {
            var acc = accommodations.First(a => a.Id == accGroup.Key);
            if (acc.Capacity is null) continue;

            if (byFamilyTypes.Contains(acc.AccommodationType))
            {
                if (accGroup.Count() > acc.Capacity)
                    throw new BusinessRuleException(
                        $"El alojamiento '{acc.Name}' no tiene capacidad para {accGroup.Count()} familias (máximo: {acc.Capacity}).");
            }
            else
            {
                var totalPersons = accGroup.Sum(a => regSizes.GetValueOrDefault(a.RegistrationId, 0));
                if (totalPersons > acc.Capacity)
                    throw new BusinessRuleException(
                        $"El alojamiento '{acc.Name}' no tiene capacidad para {totalPersons} personas (máximo: {acc.Capacity}).");
            }
        }

        // 4. Delete existing and insert new
        await db.AccommodationAssignments
            .Where(a => a.ProposalId == proposalId)
            .ExecuteDeleteAsync(ct);

        var now = DateTime.UtcNow;
        var newAssignments = assignments.Select(a => new AccommodationAssignment
        {
            Id = Guid.NewGuid(),
            ProposalId = proposalId,
            RegistrationId = a.RegistrationId,
            AccommodationId = a.AccommodationId,
            AssignedByUserId = assignedByUserId,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        await db.AccommodationAssignments.AddRangeAsync(newAssignments, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
}
```

#### 9c. Service: `AccommodationAssignmentsService.cs`

Thin orchestration layer. Delegates to repository. Adds:
- Authorization check: proposal must belong to edition.
- `AutoAssignAsync`: calls `AutoAssignService.Compute()` then optionally persists via `BulkReplaceAsync`.

---

### Step 10: AutoAssign Service

**File:** `src/Abuvi.API/Features/Camps/AutoAssignService.cs`

Stateless service — takes in the current assignment state and returns a computed assignment list. No database calls; pure computation.

```csharp
namespace Abuvi.API.Features.Camps;

public static class AutoAssignService
{
    private static readonly HashSet<AccommodationType> ByFamilyTypes =
        [AccommodationType.Caravan, AccommodationType.Tent];

    public static IReadOnlyList<AssignmentEntry> Compute(
        ProposalAssignmentStateResponse state,
        bool overwriteExisting)
    {
        var assignments = overwriteExisting
            ? new Dictionary<Guid, Guid>()                          // start fresh
            : state.Assignments.ToDictionary(a => a.RegistrationId, a => a.AccommodationId);

        // Occupancy tracker: accommodationId → list of registrationIds assigned
        var occupancy = new Dictionary<Guid, List<Guid>>();
        foreach (var acc in state.Accommodations)
            occupancy[acc.Id] = [];

        foreach (var (regId, accId) in assignments)
            occupancy[accId].Add(regId);

        var sizeMap = state.Families.ToDictionary(f => f.RegistrationId, f => f.MemberCount);

        // Sort unassigned families: larger families first
        var unassigned = state.Families
            .Where(f => !assignments.ContainsKey(f.RegistrationId))
            .OrderByDescending(f => f.MemberCount)
            .ToList();

        foreach (var family in unassigned)
        {
            var assigned = false;

            // Phase 1: try preferences in order
            foreach (var pref in family.AccommodationPreferences.OrderBy(p => p.PreferenceOrder))
            {
                var candidates = state.Accommodations
                    .Where(acc =>
                        acc.Id == pref.AccommodationId &&
                        HasCapacity(acc, occupancy[acc.Id], family.MemberCount, sizeMap))
                    .ToList();

                if (candidates.Count == 0) continue;

                // Tightest fit: score = remaining_capacity − family_size (lower = better match)
                var best = candidates
                    .OrderBy(acc => GetRemainingCapacity(acc, occupancy[acc.Id], sizeMap) - family.MemberCount)
                    .First();

                assignments[family.RegistrationId] = best.Id;
                occupancy[best.Id].Add(family.RegistrationId);
                assigned = true;
                break;
            }

            if (assigned) continue;

            // Phase 2: fallback — any accommodation with space
            var fallback = state.Accommodations
                .Where(acc => HasCapacity(acc, occupancy[acc.Id], family.MemberCount, sizeMap))
                .OrderBy(acc => GetRemainingCapacity(acc, occupancy[acc.Id], sizeMap))
                .FirstOrDefault();

            if (fallback is not null)
            {
                assignments[family.RegistrationId] = fallback.Id;
                occupancy[fallback.Id].Add(family.RegistrationId);
            }
        }

        return assignments
            .Select(kvp => new AssignmentEntry(kvp.Key, kvp.Value))
            .ToList();
    }

    private static bool HasCapacity(
        AssignmentAccommodationResponse acc,
        List<Guid> assignedRegIds,
        int familySize,
        Dictionary<Guid, int> sizeMap)
    {
        if (acc.Capacity is null) return true;
        var remaining = GetRemainingCapacity(acc, assignedRegIds, sizeMap);
        return acc.CountByFamily ? remaining >= 1 : remaining >= familySize;
    }

    private static int GetRemainingCapacity(
        AssignmentAccommodationResponse acc,
        List<Guid> assignedRegIds,
        Dictionary<Guid, int> sizeMap)
    {
        if (acc.Capacity is null) return int.MaxValue;
        var used = acc.CountByFamily
            ? assignedRegIds.Count
            : assignedRegIds.Sum(id => sizeMap.GetValueOrDefault(id, 0));
        return acc.Capacity.Value - used;
    }
}
```

`CountByFamily` is computed from the accommodation type when building `AssignmentAccommodationResponse`:
```csharp
bool CountByFamily = acc.AccommodationType is AccommodationType.Caravan or AccommodationType.Tent;
```

---

### Step 11: Reports Service

**File:** `src/Abuvi.API/Features/Camps/AccommodationAssignmentReportsService.cs`

Two query methods projecting into `AssignmentReportGroupResponse`:

- `GetByTypeAsync`: group accommodations by `AccommodationType`, sum capacities and occupied units.
- `GetByZoneAsync`: group by zone name (or "Sin zona"), same aggregation.
- `GetUnassignedAsync`: filter families not in the proposal's assignments.

All queries use `.AsNoTracking()`.

---

### Step 12: New Endpoint Groups in `CampsEndpoints.cs`

Add three new private static sections to the existing `MapCampsEndpoints` method.

#### Zone endpoints (Board+ only)

```csharp
var zonesGroup = app.MapGroup("/api/camps/editions/{campEditionId:guid}/accommodation-zones")
    .WithTags("Accommodation Zones")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

zonesGroup.MapGet("/", GetZonesByEdition)
    .WithName("GetAccommodationZonesByEdition")
    .Produces<ApiResponse<List<AccommodationZoneResponse>>>();

zonesGroup.MapPost("/", CreateZone)
    .WithName("CreateAccommodationZone")
    .AddEndpointFilter<ValidationFilter<CreateAccommodationZoneRequest>>()
    .Produces<ApiResponse<AccommodationZoneResponse>>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest);

zonesGroup.MapPut("/{zoneId:guid}", UpdateZone)
    .WithName("UpdateAccommodationZone")
    .AddEndpointFilter<ValidationFilter<UpdateAccommodationZoneRequest>>()
    .Produces<ApiResponse<AccommodationZoneResponse>>()
    .Produces(StatusCodes.Status404NotFound);

zonesGroup.MapDelete("/{zoneId:guid}", DeleteZone)
    .WithName("DeleteAccommodationZone")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status422UnprocessableEntity);

zonesGroup.MapPatch("/{zoneId:guid}/accommodations", AttachAccommodationsToZone)
    .WithName("AttachAccommodationsToZone")
    .Produces<ApiResponse<AccommodationZoneResponse>>();
```

#### Proposal endpoints (Board+ only)

```csharp
var proposalsGroup = app.MapGroup(
    "/api/camps/editions/{campEditionId:guid}/assignment-proposals")
    .WithTags("Accommodation Assignment Proposals")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

proposalsGroup.MapGet("/", GetProposalsByEdition)
    .WithName("GetAccommodationAssignmentProposals")
    .Produces<ApiResponse<List<AccommodationAssignmentProposalSummaryResponse>>>();

proposalsGroup.MapPost("/", CreateProposal)
    .WithName("CreateAccommodationAssignmentProposal")
    .AddEndpointFilter<ValidationFilter<CreateAccommodationAssignmentProposalRequest>>()
    .Produces<ApiResponse<AccommodationAssignmentProposalSummaryResponse>>(StatusCodes.Status201Created);

proposalsGroup.MapPut("/{proposalId:guid}", UpdateProposal)
    .WithName("UpdateAccommodationAssignmentProposal")
    .AddEndpointFilter<ValidationFilter<UpdateAccommodationAssignmentProposalRequest>>()
    .Produces<ApiResponse<AccommodationAssignmentProposalSummaryResponse>>();

proposalsGroup.MapDelete("/{proposalId:guid}", DeleteProposal)
    .WithName("DeleteAccommodationAssignmentProposal")
    .Produces(StatusCodes.Status204NoContent);

proposalsGroup.MapPost("/{proposalId:guid}/activate", ActivateProposal)
    .WithName("ActivateAccommodationAssignmentProposal")
    .Produces<ApiResponse<AccommodationAssignmentProposalSummaryResponse>>();
```

#### Assignment endpoints (Board+ only)

```csharp
var assignmentsGroup = app.MapGroup(
    "/api/camps/editions/{campEditionId:guid}/assignment-proposals/{proposalId:guid}/assignments")
    .WithTags("Accommodation Assignments")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

assignmentsGroup.MapGet("/", GetAssignmentState)
    .WithName("GetProposalAssignmentState")
    .Produces<ApiResponse<ProposalAssignmentStateResponse>>();

assignmentsGroup.MapPut("/", BulkReplaceAssignments)
    .WithName("BulkReplaceAssignments")
    .Produces<ApiResponse<ProposalAssignmentStateResponse>>()
    .Produces(StatusCodes.Status422UnprocessableEntity);

assignmentsGroup.MapPost("/{registrationId:guid}", AssignFamily)
    .WithName("AssignFamilyToAccommodation")
    .Produces<ApiResponse<ProposalAssignmentStateResponse>>();

assignmentsGroup.MapDelete("/{registrationId:guid}", UnassignFamily)
    .WithName("UnassignFamilyFromAccommodation")
    .Produces(StatusCodes.Status204NoContent);

assignmentsGroup.MapPost("/auto-assign", AutoAssign)
    .WithName("AutoAssignFamiliesToAccommodations")
    .Produces<ApiResponse<ProposalAssignmentStateResponse>>();
```

**Report sub-group:**

```csharp
var reportsGroup = app.MapGroup(
    "/api/camps/editions/{campEditionId:guid}/assignment-proposals/{proposalId:guid}/reports")
    .WithTags("Accommodation Assignment Reports")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

reportsGroup.MapGet("/by-type", GetReportByType)
    .WithName("GetAccommodationAssignmentReportByType")
    .Produces<ApiResponse<List<AssignmentReportGroupResponse>>>();

reportsGroup.MapGet("/by-zone", GetReportByZone)
    .WithName("GetAccommodationAssignmentReportByZone")
    .Produces<ApiResponse<List<AssignmentReportGroupResponse>>>();

reportsGroup.MapGet("/unassigned", GetUnassignedFamilies)
    .WithName("GetUnassignedFamilies")
    .Produces<ApiResponse<List<AssignmentFamilyResponse>>>();
```

**Endpoint handler pattern** (same as existing accommodations):

```csharp
private static async Task<IResult> GetAssignmentState(
    Guid campEditionId,
    Guid proposalId,
    [FromServices] AccommodationAssignmentsService service,
    CancellationToken ct)
{
    try
    {
        var state = await service.GetAssignmentStateAsync(campEditionId, proposalId, ct);
        return Results.Ok(ApiResponse<ProposalAssignmentStateResponse>.Ok(state));
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(ApiResponse<ProposalAssignmentStateResponse>.NotFound(ex.Message));
    }
}

private static async Task<IResult> AutoAssign(
    Guid campEditionId,
    Guid proposalId,
    AutoAssignRequest request,
    [FromServices] AccommodationAssignmentsService service,
    CancellationToken ct)
{
    try
    {
        var state = await service.AutoAssignAsync(campEditionId, proposalId, request, ct);
        return Results.Ok(ApiResponse<ProposalAssignmentStateResponse>.Ok(state));
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex)
    {
        return Results.UnprocessableEntity(ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
    }
}
```

---

### Step 13: Register in `Program.cs`

Add after the existing accommodation service/repository registrations (around line 163):

```csharp
// Accommodation Zones
builder.Services.AddScoped<IAccommodationZonesRepository, AccommodationZonesRepository>();
builder.Services.AddScoped<AccommodationZonesService>();

// Accommodation Assignment Proposals
builder.Services.AddScoped<IAccommodationAssignmentProposalsRepository, AccommodationAssignmentProposalsRepository>();
builder.Services.AddScoped<AccommodationAssignmentProposalsService>();

// Accommodation Assignments
builder.Services.AddScoped<IAccommodationAssignmentsRepository, AccommodationAssignmentsRepository>();
builder.Services.AddScoped<AccommodationAssignmentsService>();
builder.Services.AddScoped<AccommodationAssignmentReportsService>();
```

`AutoAssignService` is static — no DI registration needed.

---

### Step 14: Unit Tests

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AutoAssignServiceTests.cs`

Required test cases (AAA pattern, `MethodName_StateUnderTest_ExpectedBehavior`):

```
Compute_WithNoFamilies_ReturnsEmptyList
Compute_WithAllFamiliesHavingFirstPreference_AssignsToFirstPreference
Compute_WhenFirstPreferenceOverCapacity_AssignsToSecondPreference
Compute_WhenAllPreferencesOverCapacity_AssignsToFallback
Compute_WhenNoCapacityAnywhere_LeavesUnassigned
Compute_WithByFamilyTypeAccommodation_CountsUnitsByFamily
Compute_WithByPersonTypeAccommodation_CountsByPersons
Compute_WithOverwriteExistingFalse_KeepsAlreadyAssigned
Compute_WithOverwriteExistingTrue_IgnoresPreviousAssignments
Compute_TightestFitHeuristic_PrefersTighterFit
```

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationAssignmentsServiceTests.cs`

```
BulkReplace_WithValidAssignments_Succeeds
BulkReplace_WithRegistrationNotInEdition_ThrowsBusinessRuleException
BulkReplace_WithAccommodationNotInEdition_ThrowsBusinessRuleException
BulkReplace_WithCapacityExceeded_ThrowsBusinessRuleException
BulkReplace_WithByFamilyCapacityExceeded_ThrowsBusinessRuleException
AssignFamily_WhenProposalDoesNotBelongToEdition_ThrowsNotFoundException
```

---

### Step 15: Update Technical Documentation

After implementation:

1. **`ai-specs/specs/data-model.md`** — add the three new entities, their fields, and relationships.
2. **`ai-specs/specs/api-spec.yml`** — add new endpoint definitions for zones, proposals, assignments, and reports.
3. Verify OpenAPI docs are auto-generated and accessible at `/swagger`.

---

## Implementation Order

1. Step 0 — Create branch
2. Step 1 — Add entities and DTOs to `CampsModels.cs`
3. Step 2a,2b,2c,2d — Create EF configurations + update `CampEditionAccommodationConfiguration`
4. Step 3 — Update `AbuviDbContext.cs`
5. Step 4 — **Create and apply migration** (verify before continuing)
6. Step 5 — Zone repository (interface + implementation)
7. Step 6 — Zone service
8. Step 7 — Zone validators
9. Step 8 — Proposal repository, service, validators
10. Step 9 — Assignment repository, service
11. Step 10 — `AutoAssignService` (stateless)
12. Step 11 — Reports service
13. Step 12 — Add endpoint groups to `CampsEndpoints.cs`
14. Step 13 — Register in `Program.cs`
15. Step 14 — Unit tests
16. Step 15 — Documentation

---

## Testing Checklist

- [ ] `dotnet build` passes with zero warnings
- [ ] Migration runs clean on fresh database
- [ ] `GET /api/camps/editions/{id}/accommodation-zones` returns 200 for valid edition
- [ ] `POST` zone with empty name returns 400 with Spanish message
- [ ] Activating a proposal deactivates all others for the edition
- [ ] `PUT /assignments` with duplicate registration in body returns 422
- [ ] `PUT /assignments` exceeding capacity returns 422 with accommodation name in message
- [ ] `POST /auto-assign` with `overwriteExisting=false` leaves pre-assigned families in place
- [ ] Reports endpoint returns correct grouping and occupancy counts
- [ ] All `AutoAssignServiceTests` pass
- [ ] All `AccommodationAssignmentsServiceTests` pass
- [ ] `dotnet test` passes with ≥ 90% coverage on new code

---

## Error Response Format

All responses use `ApiResponse<T>` envelope:

```json
{ "success": true, "data": { ... }, "error": null }
{ "success": false, "data": null, "error": { "message": "...", "code": "..." } }
```

HTTP status codes:
- `201 Created` — new resource created (zone, proposal)
- `204 No Content` — delete successful
- `400 Bad Request` — FluentValidation failure
- `404 Not Found` — entity does not exist (`NotFoundException`)
- `422 Unprocessable Entity` — business rule violation (`BusinessRuleException`): capacity exceeded, invalid assignment, cannot delete active proposal

---

## Dependencies

No new NuGet packages needed. All existing dependencies (`EF Core`, `FluentValidation`, `xUnit`, `FluentAssertions`, `NSubstitute`) cover this feature.

Migration commands:
```bash
dotnet ef migrations add AddAccommodationZonesAndAssignmentProposals --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Key Notes

1. **`CountByFamily` logic** — Caravan and Tent count occupancy by number of families assigned; Lodge, Bungalow, Motorhome count by total persons. This determines capacity validation in both `BulkReplaceAsync` and `AutoAssignService.Compute()`.
2. **Single active proposal per edition** — enforced at the application level in `ActivateAsync` (two `ExecuteUpdateAsync` calls, not a DB constraint). This is intentional: a DB unique partial index would require PostgreSQL-specific syntax.
3. **`AttachAccommodationsToZone`** — replaces all current attachments for the zone (not additive). Existing assignments in proposals are NOT invalidated — the zone-accommodation link is display metadata, not enforcement.
4. **`GetAssignmentStateAsync`** loads ALL registrations for the edition (not just assigned ones) so the frontend can render the full unassigned family list. Filter by `Status != Cancelled`.
5. **Spanish validation messages** — all FluentValidation `.WithMessage()` calls must be in Spanish.
6. **Transaction in `BulkReplaceAsync`** — use `db.Database.BeginTransactionAsync()` explicitly, not ambient transactions, to match existing project patterns.
7. **`AutoAssignService`** is pure computation — it does not touch the database. The service layer (`AccommodationAssignmentsService.AutoAssignAsync`) orchestrates: load state → compute → optionally persist.
8. **Zone delete guard** — check `HasActiveAssignmentsAsync` before deleting. "Active" means any assignment in any proposal, not just the active proposal — a zone in use anywhere should not be deleted without explicit cleanup.

---

## Next Steps After Implementation

1. Create the frontend ticket (`feature/feat-encaje-bolillos-frontend`) using `/plan-frontend-ticket`.
2. Coordinate with frontend developer on the exact shape of `ProposalAssignmentStateResponse` before any UI work begins.
3. Add `data-model.md` entries for the new tables before closing this ticket.

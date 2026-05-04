# Backend Implementation Plan: feat-family-needs-tagging — Registration Accommodation Needs & Friend Links

## Overview

This ticket (B) enriches each camp registration with structured accommodation tagging managed by the Board. It introduces two new child entities (`RegistrationAccommodationNeed`, `RegistrationFriendLink`), adds an `AccommodationInternalNotes` scalar to `Registration`, and exposes five new endpoints plus an extension to the existing detail endpoint. All new fields are Admin/Board-only; `Member` callers never see them. Architecture follows Vertical Slice Architecture within the `Features/Registrations/` slice.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Registrations/`

**Cross-cutting:** `Data/Configurations/` (two new EF config files), `Data/AbuviDbContext.cs`, `Program.cs`.

**Dependency on Ticket A:** `AccommodationFeature` (table `accommodation_features`) must already exist. `IAccommodationFeaturesRepository.GetByIdsAsync()` is available and will be used to validate feature IDs in the accommodation needs endpoint.

| Action | File |
|--------|------|
| Modify | `Features/Registrations/RegistrationsModels.cs` |
| Modify | `Features/Registrations/RegistrationsEndpoints.cs` |
| Modify | `Features/Registrations/RegistrationsService.cs` |
| Create | `Features/Registrations/RegistrationAccommodationNeedsRepository.cs` |
| Create | `Features/Registrations/RegistrationFriendLinksRepository.cs` |
| Create | `Features/Registrations/UpdateAccommodationNeedsValidator.cs` |
| Create | `Features/Registrations/UpdateAccommodationNotesValidator.cs` |
| Create | `Features/Registrations/UpdateFriendLinksValidator.cs` |
| Create | `Data/Configurations/RegistrationAccommodationNeedConfiguration.cs` |
| Create | `Data/Configurations/RegistrationFriendLinkConfiguration.cs` |
| Modify | `Data/Configurations/RegistrationConfiguration.cs` |
| Modify | `Data/AbuviDbContext.cs` |
| Modify | `Program.cs` |
| Create | EF Migration `AddRegistrationAccommodationNeedsAndFriendLinks` |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action:** Create and switch to a dedicated backend branch.
- **Branch naming:** `feature/feat-family-needs-tagging-backend`
- **Implementation steps:**
  1. Ensure you are on the latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/feat-family-needs-tagging-backend`
  3. Verify: `git branch`
- **Notes:** Never commit directly to the ticket's general branch or `dev`. The `-backend` suffix isolates backend changes from the frontend branch.

---

### Step 1: Update Domain Entities — `RegistrationsModels.cs`

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

#### 1a. Add `AccommodationInternalNotes` scalar to `Registration`

Add after `CampatesPreference`:

```csharp
public string? AccommodationInternalNotes { get; set; }
```

Add navigation collections after `StatusHistory`:

```csharp
public ICollection<RegistrationAccommodationNeed> AccommodationNeeds { get; set; } = [];
public ICollection<RegistrationFriendLink> FriendLinks { get; set; } = [];
```

#### 1b. Add `RegistrationAccommodationNeed` entity class

Add after `RegistrationAccommodationPreference` class:

```csharp
public class RegistrationAccommodationNeed
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid AccommodationFeatureId { get; set; }
    public Guid? TaggedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Registration Registration { get; set; } = null!;
    public AccommodationFeature AccommodationFeature { get; set; } = null!;
}
```

#### 1c. Add `RegistrationFriendLink` entity class

Add after `RegistrationAccommodationNeed`:

```csharp
public class RegistrationFriendLink
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid LinkedRegistrationId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Registration Registration { get; set; } = null!;
    public Registration LinkedRegistration { get; set; } = null!;
}
```

**Dependencies:** Add `using Abuvi.API.Features.Camps;` if not already present (needed for `AccommodationFeature`).

---

### Step 2: Add Request/Response DTOs — `RegistrationsModels.cs`

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`

#### 2a. Request records (add in the Request DTOs section)

```csharp
public record UpdateAccommodationNeedsRequest(List<Guid> FeatureIds);
public record UpdateAccommodationNotesRequest(string? AccommodationInternalNotes);
public record UpdateFriendLinksRequest(List<Guid> LinkedRegistrationIds);
```

#### 2b. Response records (add in the Response DTOs section)

```csharp
public record AccommodationNeedResponse(
    Guid FeatureId,
    string FeatureName,
    string FeatureCategory,
    Guid? TaggedByUserId,
    DateTime CreatedAt
);

public record AccommodationNeedsResponse(
    Guid RegistrationId,
    List<AccommodationNeedResponse> Needs
);

public record AccommodationNotesResponse(
    Guid RegistrationId,
    string? AccommodationInternalNotes,
    DateTime UpdatedAt
);

public record FriendLinkResponse(
    Guid LinkedRegistrationId,
    string LinkedFamilyName,
    Guid? CreatedByUserId,
    DateTime CreatedAt
);

public record FriendLinksResponse(
    Guid RegistrationId,
    List<FriendLinkResponse> FriendLinks
);
```

#### 2c. Extend `RegistrationResponse` with three optional fields

Add three optional positional parameters at the end of the existing `RegistrationResponse` record:

```csharp
public record RegistrationResponse(
    // ... all existing 18 fields unchanged ...
    List<StatusHistoryItemResponse> StatusHistory,
    // New optional fields — null/empty for Member callers:
    string? AccommodationInternalNotes = null,
    List<AccommodationNeedResponse>? AccommodationNeeds = null,
    List<FriendLinkResponse>? FriendLinks = null
);
```

The `ToResponse()` mapping extension requires no change — the three new params have defaults and will be `null` for all existing call sites.

#### 2d. Add `ToAdminResponse` mapping extension

Add to `RegistrationMappingExtensions`:

```csharp
public static RegistrationResponse ToAdminResponse(
    this Registration r,
    decimal amountPaid,
    List<AccommodationNeedResponse> needs,
    List<FriendLinkResponse> friendLinks)
    => r.ToResponse(amountPaid) with
    {
        AccommodationInternalNotes = r.AccommodationInternalNotes,
        AccommodationNeeds = needs,
        FriendLinks = friendLinks
    };
```

**Note on `FeatureCategory`:** The current `AccommodationFeature` model exposes `ApplicabilityLevel` (enum: Zone, Accommodation, AccommodationType, Any). Map `FeatureCategory = feature.AccommodationFeature.ApplicabilityLevel.ToString()`. If Ticket A introduced a dedicated string `Category` property, use that instead — verify before implementing.

---

### Step 3: EF Core Entity Configurations

#### 3a. Create `RegistrationAccommodationNeedConfiguration.cs`

**File:** `src/Abuvi.API/Data/Configurations/RegistrationAccommodationNeedConfiguration.cs`

```csharp
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationAccommodationNeedConfiguration
    : IEntityTypeConfiguration<RegistrationAccommodationNeed>
{
    public void Configure(EntityTypeBuilder<RegistrationAccommodationNeed> builder)
    {
        builder.ToTable("registration_accommodation_needs");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");
        builder.Property(n => n.RegistrationId)
            .IsRequired().HasColumnName("registration_id");
        builder.Property(n => n.AccommodationFeatureId)
            .IsRequired().HasColumnName("accommodation_feature_id");
        builder.Property(n => n.TaggedByUserId)
            .HasColumnName("tagged_by_user_id");
        builder.Property(n => n.CreatedAt)
            .IsRequired().HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(n => new { n.RegistrationId, n.AccommodationFeatureId })
            .IsUnique()
            .HasDatabaseName("IX_RegistrationAccommodationNeeds_RegistrationId_FeatureId");
        builder.HasIndex(n => n.RegistrationId)
            .HasDatabaseName("IX_RegistrationAccommodationNeeds_RegistrationId");

        builder.HasOne(n => n.Registration)
            .WithMany(r => r.AccommodationNeeds)
            .HasForeignKey(n => n.RegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.AccommodationFeature)
            .WithMany()
            .HasForeignKey(n => n.AccommodationFeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### 3b. Create `RegistrationFriendLinkConfiguration.cs`

**File:** `src/Abuvi.API/Data/Configurations/RegistrationFriendLinkConfiguration.cs`

```csharp
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationFriendLinkConfiguration
    : IEntityTypeConfiguration<RegistrationFriendLink>
{
    public void Configure(EntityTypeBuilder<RegistrationFriendLink> builder)
    {
        builder.ToTable("registration_friend_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");
        builder.Property(l => l.RegistrationId)
            .IsRequired().HasColumnName("registration_id");
        builder.Property(l => l.LinkedRegistrationId)
            .IsRequired().HasColumnName("linked_registration_id");
        builder.Property(l => l.CreatedByUserId)
            .HasColumnName("created_by_user_id");
        builder.Property(l => l.CreatedAt)
            .IsRequired().HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(l => new { l.RegistrationId, l.LinkedRegistrationId })
            .IsUnique()
            .HasDatabaseName("IX_RegistrationFriendLinks_RegistrationId_LinkedId");
        builder.HasIndex(l => l.RegistrationId)
            .HasDatabaseName("IX_RegistrationFriendLinks_RegistrationId");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_RegistrationFriendLinks_NoSelfLink",
            "registration_id <> linked_registration_id"));

        builder.HasOne(l => l.Registration)
            .WithMany(r => r.FriendLinks)
            .HasForeignKey(l => l.RegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
        // LinkedRegistration has no inverse collection; use the second WithMany() overload
        builder.HasOne(l => l.LinkedRegistration)
            .WithMany()
            .HasForeignKey(l => l.LinkedRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**EF multiple cascade paths note:** PostgreSQL allows multiple cascade paths to the same table (unlike SQL Server). Both FKs targeting `registrations` with `Cascade` are valid.

#### 3c. Modify `RegistrationConfiguration.cs`

**File:** `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs`

Add after the `CampatesPreference` property configuration:

```csharp
builder.Property(r => r.AccommodationInternalNotes)
    .HasMaxLength(4000)
    .HasColumnName("accommodation_internal_notes");
```

---

### Step 4: Update `AbuviDbContext.cs`

**File:** `src/Abuvi.API/Data/AbuviDbContext.cs`

Add two new `DbSet` properties after `RegistrationStatusHistories`:

```csharp
public DbSet<RegistrationAccommodationNeed> RegistrationAccommodationNeeds => Set<RegistrationAccommodationNeed>();
public DbSet<RegistrationFriendLink> RegistrationFriendLinks => Set<RegistrationFriendLink>();
```

No other changes needed — `ApplyConfigurationsFromAssembly` auto-discovers the new configurations.

---

### Step 5: Create FluentValidation Validators

#### 5a. `UpdateAccommodationNeedsValidator.cs`

**File:** `src/Abuvi.API/Features/Registrations/UpdateAccommodationNeedsValidator.cs`

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateAccommodationNeedsValidator : AbstractValidator<UpdateAccommodationNeedsRequest>
{
    public UpdateAccommodationNeedsValidator()
    {
        RuleFor(x => x.FeatureIds)
            .NotNull().WithMessage("La lista de características es obligatoria")
            .Must(ids => ids.Count <= 20)
            .WithMessage("No se pueden etiquetar más de 20 características")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("La lista contiene identificadores duplicados");

        RuleForEach(x => x.FeatureIds)
            .NotEmpty().WithMessage("El identificador de característica no puede estar vacío");
    }
}
```

#### 5b. `UpdateAccommodationNotesValidator.cs`

**File:** `src/Abuvi.API/Features/Registrations/UpdateAccommodationNotesValidator.cs`

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateAccommodationNotesValidator : AbstractValidator<UpdateAccommodationNotesRequest>
{
    public UpdateAccommodationNotesValidator()
    {
        RuleFor(x => x.AccommodationInternalNotes)
            .MaximumLength(4000)
            .WithMessage("Las notas internas no pueden superar los 4000 caracteres");
    }
}
```

#### 5c. `UpdateFriendLinksValidator.cs`

**File:** `src/Abuvi.API/Features/Registrations/UpdateFriendLinksValidator.cs`

```csharp
using FluentValidation;

namespace Abuvi.API.Features.Registrations;

public class UpdateFriendLinksValidator : AbstractValidator<UpdateFriendLinksRequest>
{
    public UpdateFriendLinksValidator()
    {
        RuleFor(x => x.LinkedRegistrationIds)
            .NotNull().WithMessage("La lista de inscripciones vinculadas es obligatoria")
            .Must(ids => ids.Count <= 10)
            .WithMessage("No se pueden vincular más de 10 familias amigas")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("La lista contiene identificadores duplicados");

        RuleForEach(x => x.LinkedRegistrationIds)
            .NotEmpty().WithMessage("El identificador de inscripción vinculada no puede estar vacío");
    }
}
```

---

### Step 6: Create Repositories

#### 6a. `RegistrationAccommodationNeedsRepository.cs`

**File:** `src/Abuvi.API/Features/Registrations/RegistrationAccommodationNeedsRepository.cs`

```csharp
using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Registrations;

public interface IRegistrationAccommodationNeedsRepository
{
    Task<List<RegistrationAccommodationNeed>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task ReplaceAsync(Guid registrationId, IEnumerable<RegistrationAccommodationNeed> needs, CancellationToken ct);
}

public class RegistrationAccommodationNeedsRepository(AbuviDbContext db)
    : IRegistrationAccommodationNeedsRepository
{
    public async Task<List<RegistrationAccommodationNeed>> GetByRegistrationIdAsync(
        Guid registrationId, CancellationToken ct)
        => await db.RegistrationAccommodationNeeds
            .AsNoTracking()
            .Include(n => n.AccommodationFeature)
            .Where(n => n.RegistrationId == registrationId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task ReplaceAsync(
        Guid registrationId, IEnumerable<RegistrationAccommodationNeed> needs, CancellationToken ct)
    {
        await db.RegistrationAccommodationNeeds
            .Where(n => n.RegistrationId == registrationId)
            .ExecuteDeleteAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var need in needs)
            need.CreatedAt = now;

        db.RegistrationAccommodationNeeds.AddRange(needs);
        await db.SaveChangesAsync(ct);
    }
}
```

#### 6b. `RegistrationFriendLinksRepository.cs`

**File:** `src/Abuvi.API/Features/Registrations/RegistrationFriendLinksRepository.cs`

The bidirectional sync strategy:
- **Replace** removes only A↔B pairs no longer in the desired set and adds new ones (both directions).
- **Get** queries `WHERE registration_id = registrationId` since both directions are stored; deduplication via LINQ in case of any inconsistency.

```csharp
using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Registrations;

public interface IRegistrationFriendLinksRepository
{
    Task<List<RegistrationFriendLink>> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task ReplaceAsync(Guid registrationId, IEnumerable<Guid> linkedRegistrationIds, Guid? createdByUserId, CancellationToken ct);
}

public class RegistrationFriendLinksRepository(AbuviDbContext db)
    : IRegistrationFriendLinksRepository
{
    public async Task<List<RegistrationFriendLink>> GetByRegistrationIdAsync(
        Guid registrationId, CancellationToken ct)
    {
        // Query both outgoing (A→B) and incoming (B→A) to be robust
        var outgoing = await db.RegistrationFriendLinks
            .AsNoTracking()
            .Include(l => l.LinkedRegistration).ThenInclude(r => r.FamilyUnit)
            .Where(l => l.RegistrationId == registrationId)
            .ToListAsync(ct);

        var incoming = await db.RegistrationFriendLinks
            .AsNoTracking()
            .Include(l => l.Registration).ThenInclude(r => r.FamilyUnit)
            .Where(l => l.LinkedRegistrationId == registrationId)
            .ToListAsync(ct);

        // Deduplicate: outgoing already covers all bidirectional links if properly maintained;
        // incoming covers gaps. Key = the "other" registration ID.
        var seen = new HashSet<Guid>();
        var result = new List<RegistrationFriendLink>();

        foreach (var link in outgoing)
        {
            if (seen.Add(link.LinkedRegistrationId))
                result.Add(link);
        }

        foreach (var link in incoming)
        {
            if (seen.Add(link.RegistrationId))
            {
                // Normalize so LinkedRegistrationId always = the "other" side
                result.Add(new RegistrationFriendLink
                {
                    Id = link.Id,
                    RegistrationId = registrationId,
                    LinkedRegistrationId = link.RegistrationId,
                    CreatedByUserId = link.CreatedByUserId,
                    CreatedAt = link.CreatedAt,
                    LinkedRegistration = link.Registration
                });
            }
        }

        return result;
    }

    public async Task ReplaceAsync(
        Guid registrationId,
        IEnumerable<Guid> linkedRegistrationIds,
        Guid? createdByUserId,
        CancellationToken ct)
    {
        var desired = linkedRegistrationIds.ToHashSet();

        // Current A→B outgoing links from this registration
        var current = await db.RegistrationFriendLinks
            .Where(l => l.RegistrationId == registrationId)
            .Select(l => l.LinkedRegistrationId)
            .ToListAsync(ct);
        var currentSet = current.ToHashSet();

        var toDelete = currentSet.Except(desired).ToList();
        var toInsert = desired.Except(currentSet).ToList();

        if (toDelete.Count > 0)
        {
            foreach (var otherId in toDelete)
            {
                // Delete both directions atomically
                await db.RegistrationFriendLinks
                    .Where(l => (l.RegistrationId == registrationId && l.LinkedRegistrationId == otherId)
                             || (l.RegistrationId == otherId && l.LinkedRegistrationId == registrationId))
                    .ExecuteDeleteAsync(ct);
            }
        }

        if (toInsert.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var otherId in toInsert)
            {
                // A → B
                db.RegistrationFriendLinks.Add(new RegistrationFriendLink
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    LinkedRegistrationId = otherId,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = now
                });
                // B → A (reciprocal)
                db.RegistrationFriendLinks.Add(new RegistrationFriendLink
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = otherId,
                    LinkedRegistrationId = registrationId,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = now
                });
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
```

**Note on `ExecuteDeleteAsync` in loop:** EF Core `ExecuteDeleteAsync` issues a single DELETE per call. For the typical case (small lists, ≤10 items), this is fine. A future optimization could batch into a single `WHERE registration_id = X AND linked_registration_id IN (...)`.

---

### Step 7: Implement Service Methods — `RegistrationsService.cs`

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

#### 7a. Add constructor dependencies

Extend the primary constructor with:

```csharp
public class RegistrationsService(
    // ... existing params ...
    IRegistrationAccommodationNeedsRepository accommodationNeedsRepo,
    IRegistrationFriendLinksRepository friendLinksRepo,
    IAccommodationFeaturesRepository accommodationFeaturesRepo)
```

Add `using Abuvi.API.Features.Camps;` if not present.

#### 7b. `UpdateAccommodationNeedsAsync`

```csharp
public async Task<AccommodationNeedsResponse> UpdateAccommodationNeedsAsync(
    Guid registrationId, Guid taggedByUserId, UpdateAccommodationNeedsRequest request, CancellationToken ct)
{
    var registration = await registrationsRepo.GetByIdAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    // Validate all feature IDs exist
    if (request.FeatureIds.Count > 0)
    {
        var features = await accommodationFeaturesRepo.GetByIdsAsync(request.FeatureIds, ct);
        if (features.Count != request.FeatureIds.Count)
            throw new ValidationException("Uno o más identificadores de característica no existen");
    }

    var needs = request.FeatureIds.Select(featureId => new RegistrationAccommodationNeed
    {
        Id = Guid.NewGuid(),
        RegistrationId = registrationId,
        AccommodationFeatureId = featureId,
        TaggedByUserId = taggedByUserId
    }).ToList();

    await accommodationNeedsRepo.ReplaceAsync(registrationId, needs, ct);

    var saved = await accommodationNeedsRepo.GetByRegistrationIdAsync(registrationId, ct);

    return new AccommodationNeedsResponse(
        registrationId,
        saved.Select(n => new AccommodationNeedResponse(
            n.AccommodationFeatureId,
            n.AccommodationFeature.Name,
            n.AccommodationFeature.ApplicabilityLevel.ToString(),
            n.TaggedByUserId,
            n.CreatedAt)).ToList());
}
```

**Note:** Throw `ValidationException` (from `Abuvi.API.Common.Exceptions`) with error code `VALIDATION_ERROR` per the project's exception model. Verify which exception type maps to `400` — inspect existing usages such as `BusinessRuleException`. If `ValidationException` does not exist, reuse `BusinessRuleException` or create a new one matching the pattern. Check `src/Abuvi.API/Common/Exceptions/`.

#### 7c. `GetAccommodationNeedsAsync`

```csharp
public async Task<List<AccommodationNeedResponse>> GetAccommodationNeedsAsync(
    Guid registrationId, CancellationToken ct)
{
    _ = await registrationsRepo.GetByIdAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    var needs = await accommodationNeedsRepo.GetByRegistrationIdAsync(registrationId, ct);

    return needs.Select(n => new AccommodationNeedResponse(
        n.AccommodationFeatureId,
        n.AccommodationFeature.Name,
        n.AccommodationFeature.ApplicabilityLevel.ToString(),
        n.TaggedByUserId,
        n.CreatedAt)).ToList();
}
```

#### 7d. `UpdateAccommodationNotesAsync`

```csharp
public async Task<AccommodationNotesResponse> UpdateAccommodationNotesAsync(
    Guid registrationId, UpdateAccommodationNotesRequest request, CancellationToken ct)
{
    var registration = await registrationsRepo.GetByIdAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    // Normalize empty string → null
    registration.AccommodationInternalNotes = string.IsNullOrWhiteSpace(request.AccommodationInternalNotes)
        ? null
        : request.AccommodationInternalNotes;

    await registrationsRepo.UpdateAsync(registration, ct);

    return new AccommodationNotesResponse(
        registrationId,
        registration.AccommodationInternalNotes,
        DateTime.UtcNow);
}
```

#### 7e. `UpdateFriendLinksAsync`

```csharp
public async Task<FriendLinksResponse> UpdateFriendLinksAsync(
    Guid registrationId, Guid createdByUserId, UpdateFriendLinksRequest request, CancellationToken ct)
{
    var registration = await registrationsRepo.GetByIdAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    // Validate no self-link
    if (request.LinkedRegistrationIds.Contains(registrationId))
        throw new BusinessRuleException("NO_SELF_LINK: No se puede crear un vínculo de una inscripción consigo misma");

    // Validate all linked registrations exist and belong to same camp edition
    foreach (var linkedId in request.LinkedRegistrationIds)
    {
        var linked = await registrationsRepo.GetByIdAsync(linkedId, ct)
            ?? throw new NotFoundException("Inscripción vinculada", linkedId);

        if (linked.CampEditionId != registration.CampEditionId)
            throw new BusinessRuleException("SAME_EDITION_REQUIRED: Todas las inscripciones vinculadas deben pertenecer a la misma edición de campamento");
    }

    await friendLinksRepo.ReplaceAsync(registrationId, request.LinkedRegistrationIds, createdByUserId, ct);

    var saved = await friendLinksRepo.GetByRegistrationIdAsync(registrationId, ct);

    return new FriendLinksResponse(
        registrationId,
        saved.Select(l => new FriendLinkResponse(
            l.LinkedRegistrationId,
            l.LinkedRegistration.FamilyUnit.Name,
            l.CreatedByUserId,
            l.CreatedAt)).ToList());
}
```

**Note:** `GetByRegistrationIdAsync` returns normalized `RegistrationFriendLink` objects where `LinkedRegistration` is always the "other" side. Ensure the navigation property is loaded (the repo uses `.Include(l => l.LinkedRegistration).ThenInclude(r => r.FamilyUnit)`).

#### 7f. `GetFriendLinksAsync`

```csharp
public async Task<List<FriendLinkResponse>> GetFriendLinksAsync(
    Guid registrationId, CancellationToken ct)
{
    _ = await registrationsRepo.GetByIdAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    var links = await friendLinksRepo.GetByRegistrationIdAsync(registrationId, ct);

    return links.Select(l => new FriendLinkResponse(
        l.LinkedRegistrationId,
        l.LinkedRegistration.FamilyUnit.Name,
        l.CreatedByUserId,
        l.CreatedAt)).ToList();
}
```

#### 7g. Extend `GetByIdAsync` to include Admin/Board data

Update the existing `GetByIdAsync` method. After the existing permission check and `amountPaid` calculation, branch on `isAdminOrBoard`:

```csharp
public async Task<RegistrationResponse> GetByIdAsync(
    Guid registrationId, Guid userId, bool isAdminOrBoard, CancellationToken ct)
{
    var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
        ?? throw new NotFoundException("Inscripción", registrationId);

    if (!isAdminOrBoard)
    {
        // ... existing Member access check unchanged ...
    }

    var amountPaid = registration.Payments
        .Where(p => p.Status == PaymentStatus.Completed)
        .Sum(p => p.Amount);

    if (!isAdminOrBoard)
        return registration.ToResponse(amountPaid);

    // Admin/Board: load accommodation data (separate queries to avoid heavy base query)
    var needs = await accommodationNeedsRepo.GetByRegistrationIdAsync(registrationId, ct);
    var friendLinks = await friendLinksRepo.GetByRegistrationIdAsync(registrationId, ct);

    var needResponses = needs.Select(n => new AccommodationNeedResponse(
        n.AccommodationFeatureId,
        n.AccommodationFeature.Name,
        n.AccommodationFeature.ApplicabilityLevel.ToString(),
        n.TaggedByUserId,
        n.CreatedAt)).ToList();

    var friendLinkResponses = friendLinks.Select(l => new FriendLinkResponse(
        l.LinkedRegistrationId,
        l.LinkedRegistration.FamilyUnit.Name,
        l.CreatedByUserId,
        l.CreatedAt)).ToList();

    return registration.ToAdminResponse(amountPaid, needResponses, friendLinkResponses);
}
```

---

### Step 8: Register Services in `Program.cs`

**File:** `src/Abuvi.API/Program.cs`

In the Registrations feature block (after `IRegistrationAccommodationPreferencesRepository`), add:

```csharp
builder.Services.AddScoped<IRegistrationAccommodationNeedsRepository, RegistrationAccommodationNeedsRepository>();
builder.Services.AddScoped<IRegistrationFriendLinksRepository, RegistrationFriendLinksRepository>();
```

Also update `RegistrationsService` registration. Since it's registered as `AddScoped<RegistrationsService>()` (concrete type, no interface), the two new dependencies are automatically injected as long as they're registered before.

---

### Step 9: Add Endpoints — `RegistrationsEndpoints.cs`

**File:** `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`

#### 9a. Add endpoint declarations to `MapRegistrationsEndpoints`

Add after the existing `accommodation-preferences` endpoints, inside the admin `adminEditGroup`:

```csharp
// Accommodation needs (Board/Admin tagging)
adminEditGroup.MapPut("/{id:guid}/accommodation-needs", UpdateAccommodationNeeds)
    .WithName("UpdateAccommodationNeeds")
    .WithSummary("Replace structured accommodation needs for a registration (Admin/Board only)")
    .AddEndpointFilter<ValidationFilter<UpdateAccommodationNeedsRequest>>()
    .Produces<ApiResponse<AccommodationNeedsResponse>>()
    .Produces(400).Produces(401).Produces(403).Produces(404);

adminEditGroup.MapGet("/{id:guid}/accommodation-needs", GetAccommodationNeeds)
    .WithName("GetAccommodationNeeds")
    .WithSummary("Get accommodation needs for a registration (Admin/Board only)")
    .Produces<ApiResponse<List<AccommodationNeedResponse>>>()
    .Produces(401).Produces(403).Produces(404);

// Internal accommodation notes
adminEditGroup.MapPatch("/{id:guid}/accommodation-notes", UpdateAccommodationNotes)
    .WithName("UpdateAccommodationNotes")
    .WithSummary("Update internal accommodation notes for a registration (Admin/Board only)")
    .AddEndpointFilter<ValidationFilter<UpdateAccommodationNotesRequest>>()
    .Produces<ApiResponse<AccommodationNotesResponse>>()
    .Produces(400).Produces(401).Produces(403).Produces(404);

// Friend links
adminEditGroup.MapPut("/{id:guid}/friend-links", UpdateFriendLinks)
    .WithName("UpdateFriendLinks")
    .WithSummary("Replace friend family links for a registration (Admin/Board only)")
    .AddEndpointFilter<ValidationFilter<UpdateFriendLinksRequest>>()
    .Produces<ApiResponse<FriendLinksResponse>>()
    .Produces(400).Produces(401).Produces(403).Produces(404);

adminEditGroup.MapGet("/{id:guid}/friend-links", GetFriendLinks)
    .WithName("GetFriendLinks")
    .WithSummary("Get friend family links for a registration (Admin/Board only)")
    .Produces<ApiResponse<List<FriendLinkResponse>>>()
    .Produces(401).Produces(403).Produces(404);
```

#### 9b. Add handler methods

Add the following static handler methods at the end of `RegistrationsEndpoints`:

```csharp
private static async Task<IResult> UpdateAccommodationNeeds(
    Guid id,
    UpdateAccommodationNeedsRequest request,
    RegistrationsService service,
    ClaimsPrincipal user,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    try
    {
        var result = await service.UpdateAccommodationNeedsAsync(id, userId, request, ct);
        return TypedResults.Ok(ApiResponse<AccommodationNeedsResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex)
    {
        return TypedResults.BadRequest(ApiResponse<object>.Error(ex.Message, "VALIDATION_ERROR"));
    }
}

private static async Task<IResult> GetAccommodationNeeds(
    Guid id,
    RegistrationsService service,
    CancellationToken ct)
{
    try
    {
        var result = await service.GetAccommodationNeedsAsync(id, ct);
        return TypedResults.Ok(ApiResponse<List<AccommodationNeedResponse>>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}

private static async Task<IResult> UpdateAccommodationNotes(
    Guid id,
    UpdateAccommodationNotesRequest request,
    RegistrationsService service,
    CancellationToken ct)
{
    try
    {
        var result = await service.UpdateAccommodationNotesAsync(id, request, ct);
        return TypedResults.Ok(ApiResponse<AccommodationNotesResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}

private static async Task<IResult> UpdateFriendLinks(
    Guid id,
    UpdateFriendLinksRequest request,
    RegistrationsService service,
    ClaimsPrincipal user,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    try
    {
        var result = await service.UpdateFriendLinksAsync(id, userId, request, ct);
        return TypedResults.Ok(ApiResponse<FriendLinksResponse>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
    catch (BusinessRuleException ex) when (ex.Message.StartsWith("NO_SELF_LINK"))
    {
        return TypedResults.BadRequest(ApiResponse<object>.Error(ex.Message, "NO_SELF_LINK"));
    }
    catch (BusinessRuleException ex) when (ex.Message.StartsWith("SAME_EDITION_REQUIRED"))
    {
        return TypedResults.BadRequest(ApiResponse<object>.Error(ex.Message, "SAME_EDITION_REQUIRED"));
    }
}

private static async Task<IResult> GetFriendLinks(
    Guid id,
    RegistrationsService service,
    CancellationToken ct)
{
    try
    {
        var result = await service.GetFriendLinksAsync(id, ct);
        return TypedResults.Ok(ApiResponse<List<FriendLinkResponse>>.Ok(result));
    }
    catch (NotFoundException ex)
    {
        return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
    }
}
```

**Note on `ApiResponse.Error` signature:** Verify the exact factory method name used in the project. Check existing 400 handlers in `RegistrationsEndpoints.cs` for the correct `ApiResponse<object>` error factory. Adapt if needed (it may be `ValidationError`, `BadRequest`, etc.).

---

### Step 10: Create EF Core Migration

Run from the solution root:

```bash
dotnet ef migrations add AddRegistrationAccommodationNeedsAndFriendLinks \
    --project src/Abuvi.API \
    --startup-project src/Abuvi.API
```

The migration must:
- Create table `registration_accommodation_needs` with unique index on `(registration_id, accommodation_feature_id)`.
- Create table `registration_friend_links` with unique index on `(registration_id, linked_registration_id)` and check constraint `registration_id <> linked_registration_id`.
- Add column `accommodation_internal_notes text NULL` to `registrations`.

**Review the generated migration** before applying. Ensure the check constraint DDL is correct for PostgreSQL. Apply with:

```bash
dotnet ef database update --project src/Abuvi.API
```

---

### Step 11: Write Unit Tests

**Test file locations** (follow existing pattern):
- `src/Abuvi.API/Features/Registrations/RegistrationAccommodationNeedsServiceTests.cs`
- `src/Abuvi.API/Features/Registrations/RegistrationFriendLinksServiceTests.cs`

**Framework:** xUnit + FluentAssertions + NSubstitute (pattern from existing `RegistrationsServiceTests.cs`).

#### 11a. Accommodation Needs — `RegistrationAccommodationNeedsServiceTests.cs`

**Successful Cases:**
- `UpdateAccommodationNeedsAsync_WithValidFeatureIds_ReturnsPopulatedResponse`
- `UpdateAccommodationNeedsAsync_WithEmptyList_ClearsAllNeeds`
- `GetAccommodationNeedsAsync_ReturnsNeedsForRegistration`

**Validation Errors:**
- `UpdateAccommodationNeedsAsync_WithNonExistentFeatureId_ThrowsValidationException`
- `UpdateAccommodationNeedsValidator_WithMoreThan20Ids_FailsValidation`
- `UpdateAccommodationNeedsValidator_WithDuplicateIds_FailsValidation`

**Not Found:**
- `UpdateAccommodationNeedsAsync_WithNonExistentRegistration_ThrowsNotFoundException`
- `GetAccommodationNeedsAsync_WithNonExistentRegistration_ThrowsNotFoundException`

#### 11b. Accommodation Notes — include in `RegistrationsServiceTests.cs`

**Successful Cases:**
- `UpdateAccommodationNotesAsync_SetsNotesCorrectly`
- `UpdateAccommodationNotesAsync_WithEmptyString_SetsNullNotes`
- `UpdateAccommodationNotesAsync_WithNull_SetsNullNotes`

**Validation:**
- `UpdateAccommodationNotesValidator_ExceededLength_FailsValidation`

**Not Found:**
- `UpdateAccommodationNotesAsync_WithNonExistentRegistration_ThrowsNotFoundException`

#### 11c. Friend Links — `RegistrationFriendLinksServiceTests.cs`

**Successful Cases:**
- `UpdateFriendLinksAsync_WithValidLinks_CreatesBidirectionalLinks`
- `UpdateFriendLinksAsync_WithEmptyList_RemovesAllLinks`
- `UpdateFriendLinksAsync_AddingNew_KeepsExistingAndAddsNew`
- `UpdateFriendLinksAsync_RemovingOne_RemovesBothDirections`
- `GetFriendLinksAsync_ReturnsDeduplicatedLinks`

**Business Rule Violations:**
- `UpdateFriendLinksAsync_WithSelfLink_ThrowsBusinessRuleException`
- `UpdateFriendLinksAsync_WithDifferentEdition_ThrowsBusinessRuleException`

**Validation:**
- `UpdateFriendLinksValidator_WithMoreThan10Ids_FailsValidation`
- `UpdateFriendLinksValidator_WithDuplicateIds_FailsValidation`

**Not Found:**
- `UpdateFriendLinksAsync_WithNonExistentRegistration_ThrowsNotFoundException`
- `UpdateFriendLinksAsync_WithNonExistentLinkedRegistration_ThrowsNotFoundException`

#### 11d. `GetByIdAsync` Admin extension

Add to existing `RegistrationsServiceTests.cs`:
- `GetByIdAsync_AsAdmin_IncludesAccommodationNeedsAndFriendLinks`
- `GetByIdAsync_AsMember_ExcludesAccommodationFieldsFromResponse`

---

### Step 12: Update Technical Documentation

**File:** `ai-specs/specs/data-model.md`

Add the two new entities in the Registrations section:

- `RegistrationAccommodationNeed`: fields, constraints, indexes, FKs.
- `RegistrationFriendLink`: fields, constraints, indexes, check constraint, bidirectionality note.
- `Registration.AccommodationInternalNotes`: new field note.

**File:** `ai-specs/specs/api-spec.yml`

Add the five new endpoints:
- `PUT /api/registrations/{id}/accommodation-needs`
- `GET /api/registrations/{id}/accommodation-needs`
- `PATCH /api/registrations/{id}/accommodation-notes`
- `PUT /api/registrations/{id}/friend-links`
- `GET /api/registrations/{id}/friend-links`

Document extended fields for `GET /api/registrations/{id}` (Admin/Board response).

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-family-needs-tagging-backend`
2. **Step 1** — Update `Registration` entity + add new entity classes in `RegistrationsModels.cs`
3. **Step 2** — Add request/response DTOs and `ToAdminResponse` mapping in `RegistrationsModels.cs`
4. **Step 3** — Create EF configurations (`RegistrationAccommodationNeedConfiguration`, `RegistrationFriendLinkConfiguration`, modify `RegistrationConfiguration`)
5. **Step 4** — Update `AbuviDbContext.cs` (add two new DbSets)
6. **Step 5** — Create validators
7. **Step 6** — Create repositories (`RegistrationAccommodationNeedsRepository`, `RegistrationFriendLinksRepository`)
8. **Step 7** — Implement service methods in `RegistrationsService.cs` (add constructor deps, add all new methods, extend `GetByIdAsync`)
9. **Step 8** — Register new repos in `Program.cs`
10. **Step 9** — Add endpoints and handlers in `RegistrationsEndpoints.cs`
11. **Step 10** — Create and apply EF Core migration
12. **Step 11** — Write unit tests
13. **Step 12** — Update `data-model.md` and `api-spec.yml`

---

## Testing Checklist

- [ ] `PUT /api/registrations/{id}/accommodation-needs` replaces the full needs list idempotently
- [ ] `PUT accommodation-needs` with unknown feature ID returns `400 VALIDATION_ERROR`
- [ ] `PUT accommodation-needs` with more than 20 feature IDs returns `400`
- [ ] `PUT accommodation-needs` with empty list clears all tags and returns `200` with empty `needs`
- [ ] `GET /api/registrations/{id}/accommodation-needs` returns correct tags
- [ ] `PATCH /api/registrations/{id}/accommodation-notes` updates notes correctly
- [ ] `PATCH accommodation-notes` with `null`/empty string sets notes to `null`
- [ ] `PATCH accommodation-notes` with >4000 chars returns `400`
- [ ] `PUT /api/registrations/{id}/friend-links` with valid list creates bidirectional rows in DB
- [ ] `PUT friend-links` with self-link returns `400 NO_SELF_LINK`
- [ ] `PUT friend-links` with registration from another edition returns `400 SAME_EDITION_REQUIRED`
- [ ] `PUT friend-links` removing a link also deletes its reciprocal row
- [ ] `GET /api/registrations/{id}/friend-links` returns deduplicated view
- [ ] `GET /api/registrations/{id}` as Admin/Board includes `accommodationInternalNotes`, `accommodationNeeds: []`, `friendLinks: []`
- [ ] `GET /api/registrations/{id}` as Member: `accommodationInternalNotes = null`, `accommodationNeeds = null`, `friendLinks = null`
- [ ] All five new endpoints return `403` when called with Member role
- [ ] All five new endpoints return `404` when registration does not exist
- [ ] EF Core migration applies without errors on a clean database
- [ ] 90% xUnit test coverage (FluentAssertions + NSubstitute)

---

## Error Response Format

Uses the standard `ApiResponse<T>` envelope:

```json
{
  "success": false,
  "error": {
    "message": "...",
    "code": "VALIDATION_ERROR | NOT_FOUND | NO_SELF_LINK | SAME_EDITION_REQUIRED"
  }
}
```

| HTTP Status | Scenario |
|-------------|----------|
| 200 OK | Successful read or update |
| 400 Bad Request | Validation failure, feature ID not found, self-link, different edition |
| 403 Forbidden | Caller has `Member` role (enforced by `RequireRole("Admin", "Board")` on `adminEditGroup`) |
| 404 Not Found | Registration or linked registration does not exist |

---

## Dependencies

- **NuGet:** No new packages required. EF Core, FluentValidation, and xUnit/NSubstitute are already in the project.
- **EF Core migration command:**
  ```bash
  dotnet ef migrations add AddRegistrationAccommodationNeedsAndFriendLinks --project src/Abuvi.API
  ```

---

## Notes

- **Ticket A dependency:** `AccommodationFeature` and `IAccommodationFeaturesRepository.GetByIdsAsync()` must be present. Verify on the target branch before starting.
- **`featureCategory` field:** Currently maps to `feature.ApplicabilityLevel.ToString()`. If Ticket A added a dedicated `Category` string property to `AccommodationFeature`, use that property instead.
- **`BusinessRuleException` error codes:** The error code (`NO_SELF_LINK`, `SAME_EDITION_REQUIRED`) is embedded in the message prefix to allow endpoint handlers to pattern-match. A cleaner alternative is to add a `Code` property to `BusinessRuleException` — align with the existing convention in `Common/Exceptions/`.
- **`ValidationException`:** If the project has a dedicated `ValidationException` type (for 400s that are not business rules), use it for the "feature ID not found" case. If not, reuse `BusinessRuleException` — the endpoint maps it to `BadRequest`.
- **`AccommodationInternalNotes` visibility:** The field is already excluded for Members via the `ToResponse` extension (the field defaults to `null`). No role check is needed in the query; the separation is purely at the mapping layer.
- **Bidirectionality:** `registration_friend_links` stores both A→B and B→A. The `WHERE registration_id = ?` query in the assignment-status endpoint (Ticket C) is therefore correct without a UNION.
- **Language standard:** All code identifiers and comments in English; all user-facing error messages in Spanish.
- **RGPD:** `AccommodationInternalNotes` does not contain health data; no AES-256 encryption required. Standard column access controls apply.

---

## Next Steps After Implementation

- Frontend implementation (Ticket B frontend plan) consumes the five new endpoints.
- Ticket C (Encaje de Bolillos assignment board) consumes `accommodationNeeds`, `accommodationInternalNotes`, and `friendLinkRegistrationIds` from the assignment-status endpoint — the data structures defined here are ready for that integration.
- Verify the `AccommodationFeature.ApplicabilityLevel` → `featureCategory` mapping with the Ticket A implementer before merging to `dev`.

---

## Implementation Verification

- [ ] **Code quality:** C# nullable reference types enabled; no `#nullable disable`; no unhandled `null` dereferences.
- [ ] **Endpoints:** All five endpoints return documented HTTP status codes under each scenario.
- [ ] **Testing:** ≥90% coverage with xUnit + FluentAssertions + NSubstitute; no live DB calls in unit tests.
- [ ] **Migration:** `dotnet ef database update` succeeds on a clean PostgreSQL instance.
- [ ] **Auth:** All five new endpoints enforce `RequireRole("Admin", "Board")` — Member calls return `403`.
- [ ] **Documentation:** `data-model.md` and `api-spec.yml` updated in English.

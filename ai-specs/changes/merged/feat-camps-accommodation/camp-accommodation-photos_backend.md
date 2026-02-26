# Backend Implementation Plan: feat-camps-accommodation — Camp Accommodation Capacity & Photos

## Overview

This plan implements two capabilities in the `Camps` Vertical Slice:

1. **Accommodation Capacity** — A JSON column (`accommodation_capacity_json`) on `camps` and `camp_editions` tables, serialized as `AccommodationCapacity`. Includes automatic synchronization from `CampEdition` to its parent `Camp` template.
2. **Manual Photo Management** — CRUD + reorder + set-primary endpoints for `CampPhoto`, extending the existing Google Places photo entity with a new `description` column and new repository/service methods.

**Architecture:** All changes stay within `src/Abuvi.API/Features/Camps/`. No new feature slices. No cross-slice dependencies introduced.

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

**Files to modify:**

- `CampsModels.cs` — entities, value objects, DTOs
- `CampsValidators.cs` — validators (NOT `CreateCampValidator.cs` or `UpdateCampValidator.cs`)
- `CampsService.cs` — accommodation in create/update
- `CampEditionsService.cs` — accommodation in propose/promote + auto-sync
- `ICampsRepository.cs` — new photo method signatures
- `CampsRepository.cs` — implement new photo methods
- `CampsEndpoints.cs` — new `/api/camps/{campId}/photos` endpoint group
- `Data/Configurations/CampConfiguration.cs` — add accommodation column + Photos relationship
- `Data/Configurations/CampEditionConfiguration.cs` — add accommodation column
- `Data/Configurations/CampPhotoConfiguration.cs` — add description column
- `Program.cs` — register `CampPhotosService`

**Files to create:**

- `CampPhotosService.cs` — new service for manual photo management
- `Migrations/{timestamp}_AddAccommodationCapacity.cs` — EF Core migration (generated)
- `src/Abuvi.Tests/Unit/Features/Camps/AccommodationCapacityTests.cs`
- `src/Abuvi.Tests/Unit/Features/Camps/CampAccommodationSerializationTests.cs`
- `src/Abuvi.Tests/Unit/Features/Camps/CampPhotosServiceTests.cs`
- `src/Abuvi.Tests/Integration/Features/Camps/CampAccommodationIntegrationTests.cs`
- `src/Abuvi.Tests/Helpers/Builders/AccommodationCapacityBuilder.cs`

**Cross-cutting:** None. Uses existing `ValidationFilter<T>`, `ApiResponse<T>`, `NotFoundException`, `BusinessRuleException`.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action:** Create and switch to a dedicated backend feature branch.
- **Branch name:** `feature/feat-camps-accommodation-backend`
- **Steps:**
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-camps-accommodation-backend`
  3. Verify: `git branch`

> Current branch is `feature/feat-camps-extra-data-backend`. Create the new branch from `main` to keep concerns separated.

---

### Step 1: Add Value Objects and Entity Fields — `CampsModels.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

#### 1a. Add `AccommodationCapacity` class

Add after the existing entity classes:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

public class AccommodationCapacity
{
    public int? PrivateRoomsWithBathroom { get; set; }
    public int? PrivateRoomsSharedBathroom { get; set; }
    public List<SharedRoomInfo>? SharedRooms { get; set; }
    public int? Bungalows { get; set; }
    public int? CampOwnedTents { get; set; }
    public int? MemberTentAreaSquareMeters { get; set; }
    public int? MemberTentCapacityEstimate { get; set; }
    public int? MotorhomeSpots { get; set; }
    public string? Notes { get; set; }

    public int CalculateTotalBedCapacity()
    {
        var total = 0;
        total += (PrivateRoomsWithBathroom ?? 0) * 2;
        total += (PrivateRoomsSharedBathroom ?? 0) * 2;
        total += SharedRooms?.Sum(r => r.Quantity * r.BedsPerRoom) ?? 0;
        return total;
    }
}

public class SharedRoomInfo
{
    public int Quantity { get; set; }
    public int BedsPerRoom { get; set; }
    public bool HasBathroom { get; set; }
    public bool HasShower { get; set; }
    public string? Notes { get; set; }
}
```

#### 1b. Add `AccommodationCapacityJson` property and helpers to `Camp` class

Add inside the `Camp` class body (after the `Photos` navigation property):

```csharp
public string? AccommodationCapacityJson { get; set; }

public AccommodationCapacity? GetAccommodationCapacity()
{
    if (string.IsNullOrWhiteSpace(AccommodationCapacityJson))
        return null;
    return JsonSerializer.Deserialize<AccommodationCapacity>(
        AccommodationCapacityJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}

public void SetAccommodationCapacity(AccommodationCapacity? capacity)
{
    AccommodationCapacityJson = capacity is null
        ? null
        : JsonSerializer.Serialize(capacity, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
}
```

#### 1c. Add same helpers to `CampEdition` class

Add inside the `CampEdition` class body (after the `Extras` navigation property):

```csharp
public string? AccommodationCapacityJson { get; set; }

public AccommodationCapacity? GetAccommodationCapacity() { /* same impl as Camp */ }
public void SetAccommodationCapacity(AccommodationCapacity? capacity) { /* same impl as Camp */ }
```

#### 1d. Add `Description` to `CampPhoto` class

Add inside the existing `CampPhoto` class:

```csharp
public string? Description { get; set; }
```

---

### Step 2: Update Request/Response DTOs — `CampsModels.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

> **IMPORTANT:** C# records are positional. Adding parameters changes the constructor signature and all call sites must be updated simultaneously. Make all changes in one commit.

#### 2a. Update `CreateCampRequest`

```csharp
public record CreateCampRequest(
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    AccommodationCapacity? AccommodationCapacity   // NEW — last, nullable
);
```

#### 2b. Update `UpdateCampRequest`

```csharp
public record UpdateCampRequest(
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    AccommodationCapacity? AccommodationCapacity   // NEW — last, nullable
);
```

#### 2c. Update `CampDetailResponse`

```csharp
public record CampDetailResponse(
    // ... all existing fields ...
    AccommodationCapacity? AccommodationCapacity,       // NEW
    int? CalculatedTotalBedCapacity,                   // NEW
    IReadOnlyList<CampPhotoResponse> Photos,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 2d. Update `ProposeCampEditionRequest`

```csharp
public record ProposeCampEditionRequest(
    // ... all existing fields ...
    string? Notes,
    AccommodationCapacity? AccommodationCapacity   // NEW — last, nullable
);
```

#### 2e. Update `CampEditionResponse`

```csharp
public record CampEditionResponse(
    // ... all existing fields ...
    AccommodationCapacity? AccommodationCapacity,       // NEW
    int? CalculatedTotalBedCapacity,                   // NEW
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 2f. Update `CampPhotoResponse`

```csharp
public record CampPhotoResponse(
    Guid Id,
    string? PhotoReference,
    string? PhotoUrl,
    int Width,
    int Height,
    string AttributionName,
    string? AttributionUrl,
    string? Description,    // NEW
    bool IsPrimary,
    int DisplayOrder
);
```

#### 2g. Add new photo DTOs

```csharp
public record AddCampPhotoRequest(
    string Url,
    string? Description,
    int DisplayOrder,
    bool IsPrimary
);

public record UpdateCampPhotoRequest(
    string Url,
    string? Description,
    int DisplayOrder,
    bool IsPrimary
);

public record ReorderCampPhotosRequest(
    List<PhotoOrderItem> Photos
);

public record PhotoOrderItem(
    Guid Id,
    int DisplayOrder
);
```

---

### Step 3: Update EF Core Configurations

#### 3a. `CampConfiguration`

**File:** `src/Abuvi.API/Data/Configurations/CampConfiguration.cs`

Add inside `Configure()`:

```csharp
// Accommodation capacity JSON (nullable text column)
builder.Property(c => c.AccommodationCapacityJson)
    .HasColumnType("text")
    .HasColumnName("accommodation_capacity_json");

// Photos relationship (cascade delete — already defined in CampPhotoConfiguration,
// but must also be declared here for EF to map the navigation correctly)
builder.HasMany(c => c.Photos)
    .WithOne(p => p.Camp)
    .HasForeignKey(p => p.CampId)
    .OnDelete(DeleteBehavior.Cascade);
```

> **Note:** If adding the relationship here causes a conflict with `CampPhotoConfiguration` (duplicate relationship registration), remove it from `CampPhotoConfiguration` and keep it only in `CampConfiguration`. EF Core allows defining the relationship on either side — pick one and be consistent.

#### 3b. `CampEditionConfiguration`

**File:** `src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs`

Add inside `Configure()`:

```csharp
builder.Property(e => e.AccommodationCapacityJson)
    .HasColumnType("text")
    .HasColumnName("accommodation_capacity_json");
```

#### 3c. `CampPhotoConfiguration`

**File:** `src/Abuvi.API/Data/Configurations/CampPhotoConfiguration.cs`

Add inside `Configure()`:

```csharp
builder.Property(p => p.Description)
    .HasMaxLength(500)
    .HasColumnName("description");
```

---

### Step 4: Generate and Apply EF Core Migration

```bash
dotnet ef migrations add AddAccommodationCapacity --project src/Abuvi.API
```

**Review the generated file** — it should contain exactly:

1. `AddColumn` on `camps`: `accommodation_capacity_json TEXT NULL`
2. `AddColumn` on `camp_editions`: `accommodation_capacity_json TEXT NULL`
3. `AddColumn` on `camp_photos`: `description VARCHAR(500) NULL`

**Apply:**

```bash
dotnet ef database update --project src/Abuvi.API
```

**Down() should reverse all three `AddColumn` calls.**

---

### Step 5: Update `CampsValidators.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsValidators.cs`

> The file already contains `CreateCampRequestValidator`, `UpdateCampRequestValidator`, and `ProposeCampEditionRequestValidator`. Add to each, and add the two new photo validators.

#### 5a. Add to `CreateCampRequestValidator` and `UpdateCampRequestValidator`

```csharp
// Optional accommodation capacity — validate nested structure if provided
When(x => x.AccommodationCapacity != null, () =>
{
    RuleFor(x => x.AccommodationCapacity!.PrivateRoomsWithBathroom)
        .GreaterThanOrEqualTo(0)
        .WithMessage("El número de habitaciones privadas con baño debe ser mayor o igual a 0")
        .When(x => x.AccommodationCapacity!.PrivateRoomsWithBathroom.HasValue);

    RuleFor(x => x.AccommodationCapacity!.PrivateRoomsSharedBathroom)
        .GreaterThanOrEqualTo(0)
        .WithMessage("El número de habitaciones privadas con baño compartido debe ser mayor o igual a 0")
        .When(x => x.AccommodationCapacity!.PrivateRoomsSharedBathroom.HasValue);

    RuleFor(x => x.AccommodationCapacity!.Bungalows)
        .GreaterThanOrEqualTo(0)
        .WithMessage("El número de bungalows debe ser mayor o igual a 0")
        .When(x => x.AccommodationCapacity!.Bungalows.HasValue);

    RuleFor(x => x.AccommodationCapacity!.CampOwnedTents)
        .GreaterThanOrEqualTo(0)
        .WithMessage("El número de tiendas del campamento debe ser mayor o igual a 0")
        .When(x => x.AccommodationCapacity!.CampOwnedTents.HasValue);

    RuleFor(x => x.AccommodationCapacity!.MotorhomeSpots)
        .GreaterThanOrEqualTo(0)
        .WithMessage("El número de plazas para autocaravanas debe ser mayor o igual a 0")
        .When(x => x.AccommodationCapacity!.MotorhomeSpots.HasValue);

    RuleForEach(x => x.AccommodationCapacity!.SharedRooms)
        .ChildRules(room =>
        {
            room.RuleFor(r => r.Quantity)
                .GreaterThan(0)
                .WithMessage("La cantidad de habitaciones compartidas debe ser mayor que 0");
            room.RuleFor(r => r.BedsPerRoom)
                .GreaterThan(0)
                .WithMessage("El número de camas por habitación debe ser mayor que 0");
        })
        .When(x => x.AccommodationCapacity?.SharedRooms?.Any() == true);
});
```

#### 5b. Add same block to `ProposeCampEditionRequestValidator`

Same structure, apply to `x.AccommodationCapacity`.

#### 5c. Add `AddCampPhotoRequestValidator`

```csharp
public class AddCampPhotoRequestValidator : AbstractValidator<AddCampPhotoRequest>
{
    public AddCampPhotoRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("La URL de la foto es obligatoria")
            .MaximumLength(2000).WithMessage("La URL no puede superar los 2000 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.Description is not null);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden de visualización debe ser mayor o igual a 0");
    }
}
```

#### 5d. Add `UpdateCampPhotoRequestValidator`

Identical rules to `AddCampPhotoRequestValidator`.

---

### Step 6: Update `CampsService.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsService.cs`

#### 6a. In `CreateAsync`

After building the `Camp` object, add:

```csharp
camp.SetAccommodationCapacity(request.AccommodationCapacity);
```

#### 6b. In `UpdateAsync`

After updating the existing fields, add:

```csharp
camp.SetAccommodationCapacity(request.AccommodationCapacity);
```

#### 6c. In `MapToCampDetailResponse`

Update the static helper to include the two new fields:

```csharp
private static CampDetailResponse MapToCampDetailResponse(Camp camp, IEnumerable<CampPhoto> photos)
{
    var accommodation = camp.GetAccommodationCapacity();
    return new CampDetailResponse(
        // ... all existing fields ...
        AccommodationCapacity: accommodation,
        CalculatedTotalBedCapacity: accommodation?.CalculateTotalBedCapacity(),
        Photos: photos.Select(MapToPhotoResponse).ToList(),
        CreatedAt: camp.CreatedAt,
        UpdatedAt: camp.UpdatedAt
    );
}
```

#### 6d. In `MapToPhotoResponse`

Add `Description: photo.Description` to the `CampPhotoResponse` constructor call.

---

### Step 7: Update `CampEditionsService.cs`

**File:** `src/Abuvi.API/Features/Camps/CampEditionsService.cs`

#### 7a. In `ProposeAsync`

After building the `edition` object:

```csharp
edition.SetAccommodationCapacity(request.AccommodationCapacity);

var created = await _repository.CreateAsync(edition, cancellationToken);

// Auto-sync accommodation to camp template if provided
if (request.AccommodationCapacity is not null)
{
    camp.SetAccommodationCapacity(request.AccommodationCapacity);
    await _campsRepository.UpdateAsync(camp, cancellationToken);
}

return MapToCampEditionResponse(created, camp.Name);
```

#### 7b. In `PromoteToDraftAsync`

Ensure `GetByIdAsync` loads `Camp` (see Step 8 for repository change). After setting `edition.Status = CampEditionStatus.Draft`:

```csharp
// Sync accommodation to camp template
if (!string.IsNullOrWhiteSpace(edition.AccommodationCapacityJson))
{
    edition.Camp.SetAccommodationCapacity(edition.GetAccommodationCapacity());
    await _campsRepository.UpdateAsync(edition.Camp, cancellationToken);
}

var updated = await _repository.UpdateAsync(edition, cancellationToken);
```

#### 7c. In `MapToCampEditionResponse`

```csharp
private static CampEditionResponse MapToCampEditionResponse(CampEdition edition, string campName)
{
    var accommodation = edition.GetAccommodationCapacity();
    return new CampEditionResponse(
        // ... all existing fields ...
        AccommodationCapacity: accommodation,
        CalculatedTotalBedCapacity: accommodation?.CalculateTotalBedCapacity(),
        IsArchived: edition.IsArchived,
        CreatedAt: edition.CreatedAt,
        UpdatedAt: edition.UpdatedAt
    );
}
```

---

### Step 8: Update `ICampsRepository` and `CampsRepository`

#### 8a. `ICampEditionsRepository` — add `Camp` eager-load

**File:** `src/Abuvi.API/Features/Camps/ICampEditionsRepository.cs`

No interface change needed — but the implementation must `Include(e => e.Camp)` in `GetByIdAsync`.

**File:** `src/Abuvi.API/Features/Camps/CampEditionsRepository.cs`

In `GetByIdAsync`, change:

```csharp
// Before
return await _db.CampEditions.FindAsync([id], cancellationToken);

// After
return await _db.CampEditions
    .Include(e => e.Camp)
    .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
```

#### 8b. `ICampsRepository` — add photo-specific methods

**File:** `src/Abuvi.API/Features/Camps/ICampsRepository.cs`

Add:

```csharp
/// <summary>
/// Gets a specific photo by camp ID and photo ID
/// </summary>
Task<CampPhoto?> GetPhotoAsync(Guid campId, Guid photoId, CancellationToken ct = default);

/// <summary>
/// Adds a single manually-uploaded photo
/// </summary>
Task<CampPhoto> AddPhotoAsync(CampPhoto photo, CancellationToken ct = default);

/// <summary>
/// Updates a single photo
/// </summary>
Task<CampPhoto> UpdatePhotoAsync(CampPhoto photo, CancellationToken ct = default);

/// <summary>
/// Deletes a photo
/// </summary>
Task DeletePhotoAsync(CampPhoto photo, CancellationToken ct = default);

/// <summary>
/// Bulk-clears IsPrimary on all photos for a camp using ExecuteUpdateAsync (no entity load)
/// </summary>
Task ClearPrimaryPhotoAsync(Guid campId, CancellationToken ct = default);

/// <summary>
/// Bulk-updates DisplayOrder values for a set of photos
/// </summary>
Task UpdatePhotoOrdersAsync(IEnumerable<CampPhoto> photos, CancellationToken ct = default);
```

#### 8c. `CampsRepository` — implement new methods

**File:** `src/Abuvi.API/Features/Camps/CampsRepository.cs`

```csharp
public async Task<CampPhoto?> GetPhotoAsync(Guid campId, Guid photoId, CancellationToken ct = default)
    => await _db.CampPhotos
        .FirstOrDefaultAsync(p => p.CampId == campId && p.Id == photoId, ct);

public async Task<CampPhoto> AddPhotoAsync(CampPhoto photo, CancellationToken ct = default)
{
    _db.CampPhotos.Add(photo);
    await _db.SaveChangesAsync(ct);
    return photo;
}

public async Task<CampPhoto> UpdatePhotoAsync(CampPhoto photo, CancellationToken ct = default)
{
    _db.CampPhotos.Update(photo);
    await _db.SaveChangesAsync(ct);
    return photo;
}

public async Task DeletePhotoAsync(CampPhoto photo, CancellationToken ct = default)
{
    _db.CampPhotos.Remove(photo);
    await _db.SaveChangesAsync(ct);
}

public async Task ClearPrimaryPhotoAsync(Guid campId, CancellationToken ct = default)
    => await _db.CampPhotos
        .Where(p => p.CampId == campId && p.IsPrimary)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsPrimary, false), ct);

public async Task UpdatePhotoOrdersAsync(IEnumerable<CampPhoto> photos, CancellationToken ct = default)
{
    foreach (var photo in photos)
        _db.Entry(photo).Property(p => p.DisplayOrder).IsModified = true;
    await _db.SaveChangesAsync(ct);
}
```

> `DbSet<CampPhoto>` — verify this is exposed on `AbuviDbContext`. If not, add `public DbSet<CampPhoto> CampPhotos => Set<CampPhoto>();`.

---

### Step 9: Create `CampPhotosService.cs`

**File:** `src/Abuvi.API/Features/Camps/CampPhotosService.cs` *(new)*

```csharp
namespace Abuvi.API.Features.Camps;

public class CampPhotosService(ICampsRepository repository)
{
    public async Task<CampPhotoResponse> AddPhotoAsync(
        Guid campId, AddCampPhotoRequest request, CancellationToken ct = default)
    {
        // Validate camp exists
        var camp = await repository.GetByIdAsync(campId, ct)
            ?? throw new NotFoundException("Campamento", campId);

        // Clear primary if new photo is being set as primary
        if (request.IsPrimary)
            await repository.ClearPrimaryPhotoAsync(campId, ct);

        var photo = new CampPhoto
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            PhotoUrl = request.Url,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsPrimary = request.IsPrimary,
            IsOriginal = false,
            AttributionName = string.Empty,  // Not applicable for manual photos
            Width = 0,
            Height = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var saved = await repository.AddPhotoAsync(photo, ct);
        return MapToResponse(saved);
    }

    public async Task<CampPhotoResponse> UpdatePhotoAsync(
        Guid campId, Guid photoId, UpdateCampPhotoRequest request, CancellationToken ct = default)
    {
        var photo = await repository.GetPhotoAsync(campId, photoId, ct)
            ?? throw new NotFoundException("Foto", photoId);

        if (request.IsPrimary && !photo.IsPrimary)
            await repository.ClearPrimaryPhotoAsync(campId, ct);

        photo.PhotoUrl = request.Url;
        photo.Description = request.Description;
        photo.DisplayOrder = request.DisplayOrder;
        photo.IsPrimary = request.IsPrimary;
        photo.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdatePhotoAsync(photo, ct);
        return MapToResponse(updated);
    }

    public async Task DeletePhotoAsync(
        Guid campId, Guid photoId, CancellationToken ct = default)
    {
        var photo = await repository.GetPhotoAsync(campId, photoId, ct)
            ?? throw new NotFoundException("Foto", photoId);

        await repository.DeletePhotoAsync(photo, ct);
    }

    public async Task<CampPhotoResponse> SetPrimaryPhotoAsync(
        Guid campId, Guid photoId, CancellationToken ct = default)
    {
        var photo = await repository.GetPhotoAsync(campId, photoId, ct)
            ?? throw new NotFoundException("Foto", photoId);

        await repository.ClearPrimaryPhotoAsync(campId, ct);

        photo.IsPrimary = true;
        photo.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdatePhotoAsync(photo, ct);
        return MapToResponse(updated);
    }

    public async Task ReorderPhotosAsync(
        Guid campId, ReorderCampPhotosRequest request, CancellationToken ct = default)
    {
        // Load all camp photos to validate ownership
        var camp = await repository.GetByIdWithPhotosAsync(campId, ct)
            ?? throw new NotFoundException("Campamento", campId);

        var campPhotoIds = camp.Photos.Select(p => p.Id).ToHashSet();

        foreach (var item in request.Photos)
        {
            if (!campPhotoIds.Contains(item.Id))
                throw new BusinessRuleException(
                    $"La foto con ID '{item.Id}' no pertenece a este campamento");
        }

        // Apply new order
        var photosToUpdate = camp.Photos
            .Where(p => request.Photos.Any(i => i.Id == p.Id))
            .ToList();

        foreach (var photo in photosToUpdate)
        {
            var newOrder = request.Photos.First(i => i.Id == photo.Id).DisplayOrder;
            photo.DisplayOrder = newOrder;
        }

        await repository.UpdatePhotoOrdersAsync(photosToUpdate, ct);
    }

    private static CampPhotoResponse MapToResponse(CampPhoto photo) => new(
        Id: photo.Id,
        PhotoReference: photo.PhotoReference,
        PhotoUrl: photo.PhotoUrl,
        Width: photo.Width,
        Height: photo.Height,
        AttributionName: photo.AttributionName,
        AttributionUrl: photo.AttributionUrl,
        Description: photo.Description,
        IsPrimary: photo.IsPrimary,
        DisplayOrder: photo.DisplayOrder
    );
}
```

---

### Step 10: Update `CampsEndpoints.cs`

**File:** `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`

Inside `MapCampsEndpoints`, after the existing `editionsGroup` block, add:

```csharp
// Camp Photos endpoints — manual photo management
var photosGroup = app.MapGroup("/api/camps/{campId:guid}/photos")
    .WithTags("Camp Photos")
    .WithOpenApi()
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

photosGroup.MapPost("/", AddCampPhoto)
    .WithName("AddCampPhoto")
    .WithSummary("Add a manual photo to a camp")
    .AddEndpointFilter<ValidationFilter<AddCampPhotoRequest>>()
    .Produces<ApiResponse<CampPhotoResponse>>(201)
    .Produces(400).Produces(401).Produces(403).Produces(404);

photosGroup.MapPut("/{photoId:guid}", UpdateCampPhoto)
    .WithName("UpdateCampPhoto")
    .WithSummary("Update a camp photo")
    .AddEndpointFilter<ValidationFilter<UpdateCampPhotoRequest>>()
    .Produces<ApiResponse<CampPhotoResponse>>()
    .Produces(400).Produces(401).Produces(403).Produces(404);

photosGroup.MapDelete("/{photoId:guid}", DeleteCampPhoto)
    .WithName("DeleteCampPhoto")
    .WithSummary("Delete a camp photo")
    .Produces(204)
    .Produces(401).Produces(403).Produces(404);

photosGroup.MapPost("/{photoId:guid}/set-primary", SetPrimaryPhoto)
    .WithName("SetPrimaryPhoto")
    .WithSummary("Set a photo as the primary (main) photo for the camp")
    .Produces<ApiResponse<CampPhotoResponse>>()
    .Produces(401).Produces(403).Produces(404);

photosGroup.MapPut("/reorder", ReorderCampPhotos)
    .WithName("ReorderCampPhotos")
    .WithSummary("Reorder camp photos")
    .Produces(204)
    .Produces(400).Produces(401).Produces(403).Produces(404);
```

**Add handler methods (private static):**

```csharp
private static async Task<IResult> AddCampPhoto(
    [FromServices] CampPhotosService service,
    Guid campId,
    AddCampPhotoRequest request,
    CancellationToken ct)
{
    var photo = await service.AddPhotoAsync(campId, request, ct);
    return Results.Created($"/api/camps/{campId}/photos/{photo.Id}",
        ApiResponse<CampPhotoResponse>.Ok(photo));
}

private static async Task<IResult> UpdateCampPhoto(
    [FromServices] CampPhotosService service,
    Guid campId,
    Guid photoId,
    UpdateCampPhotoRequest request,
    CancellationToken ct)
{
    var photo = await service.UpdatePhotoAsync(campId, photoId, request, ct);
    return Results.Ok(ApiResponse<CampPhotoResponse>.Ok(photo));
}

private static async Task<IResult> DeleteCampPhoto(
    [FromServices] CampPhotosService service,
    Guid campId,
    Guid photoId,
    CancellationToken ct)
{
    await service.DeletePhotoAsync(campId, photoId, ct);
    return Results.NoContent();
}

private static async Task<IResult> SetPrimaryPhoto(
    [FromServices] CampPhotosService service,
    Guid campId,
    Guid photoId,
    CancellationToken ct)
{
    var photo = await service.SetPrimaryPhotoAsync(campId, photoId, ct);
    return Results.Ok(ApiResponse<CampPhotoResponse>.Ok(photo));
}

private static async Task<IResult> ReorderCampPhotos(
    [FromServices] CampPhotosService service,
    Guid campId,
    ReorderCampPhotosRequest request,
    CancellationToken ct)
{
    await service.ReorderPhotosAsync(campId, request, ct);
    return Results.NoContent();
}
```

> `NotFoundException` and `BusinessRuleException` are caught by `GlobalExceptionMiddleware` → 404 and 422 respectively. No try/catch needed in endpoint handlers.

---

### Step 11: Register in `Program.cs`

**File:** `src/Abuvi.API/Program.cs`

Add alongside the existing `CampsService` and `CampEditionsService` registrations:

```csharp
builder.Services.AddScoped<CampPhotosService>();
```

---

### Step 12: Write Unit Tests

#### 12a. `AccommodationCapacityTests.cs` (new)

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationCapacityTests.cs`

```csharp
[Fact]
public void CalculateTotalBedCapacity_WithAllTypes_ReturnsCorrectSum()
// (5+3)*2 + (2*8 + 8*2) = 16+32 = 48

[Fact]
public void CalculateTotalBedCapacity_WithNullPrivateRooms_CountsAsZero()

[Fact]
public void CalculateTotalBedCapacity_WithNullSharedRooms_CountsAsZero()

[Fact]
public void CalculateTotalBedCapacity_WithEmptySharedRoomsList_CountsAsZero()

[Fact]
public void CalculateTotalBedCapacity_WithOnlySharedRooms_ReturnsCorrectSum()
```

#### 12b. `CampAccommodationSerializationTests.cs` (new)

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampAccommodationSerializationTests.cs`

```csharp
[Fact]
public void SetAccommodationCapacity_WithValidCapacity_SerializesAsCamelCase()
// Assert JSON contains "privateRoomsWithBathroom", not "PrivateRoomsWithBathroom"

[Fact]
public void SetAccommodationCapacity_WithNull_SetsJsonToNull()

[Fact]
public void GetAccommodationCapacity_WithValidJson_DeserializesCorrectly()

[Fact]
public void GetAccommodationCapacity_WithNullJson_ReturnsNull()

[Fact]
public void GetAccommodationCapacity_WithWhiteSpaceJson_ReturnsNull()

[Fact]
public void SetThenGet_RoundTrip_PreservesAllValues()
// SharedRooms array, all nullable int fields
```

#### 12c. Extend `CampsServiceTests.cs`

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`

Add:

```csharp
[Fact]
public async Task CreateAsync_WithAccommodationCapacity_SavesAndReturnsCapacity()

[Fact]
public async Task CreateAsync_WithNullAccommodationCapacity_SavesNullCapacity()

[Fact]
public async Task UpdateAsync_WithAccommodationCapacity_UpdatesAndReturnsCapacity()

[Fact]
public async Task GetByIdAsync_WhenCampHasAccommodation_ReturnsCapacityAndCalculatedTotal()
```

Use the existing test setup (`ICampsRepository` mock, `IGooglePlacesService` mock, `IGooglePlacesMapperService` mock).

#### 12d. Extend `CampEditionsServiceTests.cs`

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs`

Add:

```csharp
[Fact]
public async Task ProposeAsync_WithAccommodationCapacity_SetsCapacityOnEdition()

[Fact]
public async Task ProposeAsync_WithAccommodationCapacity_SyncsCampTemplate()
// Verify _campsRepository.Received(1).UpdateAsync(Arg.Is<Camp>(c => c.AccommodationCapacityJson != null), ...)

[Fact]
public async Task ProposeAsync_WithNullAccommodationCapacity_DoesNotSyncCampTemplate()
// Verify _campsRepository.DidNotReceive().UpdateAsync(...)

[Fact]
public async Task PromoteToDraftAsync_WhenEditionHasCapacity_SyncsCampTemplate()

[Fact]
public async Task PromoteToDraftAsync_WhenEditionHasNoCapacity_DoesNotSyncCampTemplate()
```

#### 12e. `CampPhotosServiceTests.cs` (new)

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampPhotosServiceTests.cs`

```csharp
public class CampPhotosServiceTests
{
    private readonly ICampsRepository _repository;
    private readonly CampPhotosService _sut;

    public CampPhotosServiceTests()
    {
        _repository = Substitute.For<ICampsRepository>();
        _sut = new CampPhotosService(_repository);
    }

    [Fact]
    public async Task AddPhotoAsync_WithValidRequest_CreatesPhotoWithIsOriginalFalse()

    [Fact]
    public async Task AddPhotoAsync_WithIsPrimaryTrue_ClearsPreviousPrimaryBeforeAdding()
    // _repository.Received(1).ClearPrimaryPhotoAsync(campId, ...)

    [Fact]
    public async Task AddPhotoAsync_WhenCampNotFound_ThrowsNotFoundException()
    // _repository.GetByIdAsync returns null → NotFoundException

    [Fact]
    public async Task UpdatePhotoAsync_WithValidRequest_UpdatesPhoto()

    [Fact]
    public async Task UpdatePhotoAsync_WithIsPrimaryTrue_WhenPhotoWasNotPrimary_ClearsPreviousPrimary()

    [Fact]
    public async Task UpdatePhotoAsync_WithIsPrimaryTrue_WhenPhotoWasAlreadyPrimary_DoesNotClearPrimary()

    [Fact]
    public async Task UpdatePhotoAsync_WhenPhotoNotFound_ThrowsNotFoundException()

    [Fact]
    public async Task DeletePhotoAsync_WithValidIds_DeletesPhoto()

    [Fact]
    public async Task DeletePhotoAsync_WhenPhotoNotFound_ThrowsNotFoundException()

    [Fact]
    public async Task SetPrimaryPhotoAsync_WithValidIds_SetsPrimaryAndClearsOthers()

    [Fact]
    public async Task SetPrimaryPhotoAsync_WhenPhotoNotFound_ThrowsNotFoundException()

    [Fact]
    public async Task ReorderPhotosAsync_WithValidOrder_UpdatesDisplayOrders()

    [Fact]
    public async Task ReorderPhotosAsync_WhenPhotoIdDoesNotBelongToCamp_ThrowsBusinessRuleException()

    [Fact]
    public async Task ReorderPhotosAsync_WhenCampNotFound_ThrowsNotFoundException()
}
```

#### 12f. `AccommodationCapacityBuilder.cs` (new)

**File:** `src/Abuvi.Tests/Helpers/Builders/AccommodationCapacityBuilder.cs`

```csharp
namespace Abuvi.Tests.Helpers.Builders;

public class AccommodationCapacityBuilder
{
    private int? _privateRoomsWithBathroom;
    private int? _privateRoomsSharedBathroom;
    private List<SharedRoomInfo>? _sharedRooms;
    private int? _bungalows;
    private int? _motorhomeSpots;

    public AccommodationCapacityBuilder WithPrivateRoomsWithBathroom(int count)
    { _privateRoomsWithBathroom = count; return this; }

    public AccommodationCapacityBuilder WithPrivateRoomsSharedBathroom(int count)
    { _privateRoomsSharedBathroom = count; return this; }

    public AccommodationCapacityBuilder WithSharedRooms(List<SharedRoomInfo> rooms)
    { _sharedRooms = rooms; return this; }

    public AccommodationCapacityBuilder WithBungalows(int count)
    { _bungalows = count; return this; }

    public AccommodationCapacity Build() => new()
    {
        PrivateRoomsWithBathroom = _privateRoomsWithBathroom,
        PrivateRoomsSharedBathroom = _privateRoomsSharedBathroom,
        SharedRooms = _sharedRooms,
        Bungalows = _bungalows,
        MotorhomeSpots = _motorhomeSpots
    };
}
```

---

### Step 13: Write Integration Tests

**File:** `src/Abuvi.Tests/Integration/Features/Camps/CampAccommodationIntegrationTests.cs`

```csharp
[Fact]
public async Task Camp_WithAccommodationJson_CanBeSavedAndRetrievedFromDatabase()
// Create Camp, set AccommodationCapacity, SaveChanges, FindAsync, GetAccommodationCapacity()
// Assert PrivateRoomsWithBathroom, SharedRooms count, etc.

[Fact]
public async Task CampEdition_WithAccommodationJson_CanBeSavedAndRetrievedFromDatabase()
// Same for CampEdition entity
```

Use the existing `CampsDbSchemaTests.cs` pattern for test setup (in-memory or TestContainers as already established in the project).

---

### Step 14: Update Technical Documentation

**Files to update:**

| File | What to update |
|------|---------------|
| `ai-specs/specs/data-model.md` | Add `AccommodationCapacity` and `SharedRoomInfo` descriptions to Camp and CampEdition sections; add `description` field to CampPhoto |
| `ai-specs/specs/api-endpoints.md` | Document new photo endpoints (`POST`, `PUT`, `DELETE`, `POST /set-primary`, `PUT /reorder`) under Camp Management; update Camp request/response examples with `accommodationCapacity` |

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add value objects and entity fields to `CampsModels.cs`
3. Step 12a+b — Write failing unit tests for `AccommodationCapacity` (RED)
4. (Tests pass after Step 1 implementations — GREEN)
5. Step 2 — Update DTOs
6. Step 3 — Update EF Core configurations
7. Step 4 — Generate and apply migration
8. Step 13 — Write failing integration tests (RED)
9. (Tests pass after migration applied — GREEN)
10. Step 5 — Update validators
11. Step 6 — Update `CampsService`
12. Step 12c — Write failing CampsService tests (RED) → implement → GREEN
13. Step 7 — Update `CampEditionsService`
14. Step 8a — Update `CampEditionsRepository` (`Include(e => e.Camp)`)
15. Step 12d — Write failing CampEditionsService tests (RED) → implement → GREEN
16. Step 8b+c — Update `ICampsRepository` + `CampsRepository`
17. Step 9 — Create `CampPhotosService`
18. Step 11 — Register `CampPhotosService` in `Program.cs`
19. Step 12e+f — Write failing CampPhotosService tests (RED) → implement → GREEN
20. Step 10 — Update `CampsEndpoints` with photo endpoints
21. Step 14 — Update documentation

---

## Testing Checklist

- [ ] All `AccommodationCapacity` calculation tests pass
- [ ] Round-trip serialization tests pass (camelCase, null fields omitted)
- [ ] `CampsService` tests: accommodation in create/update/response
- [ ] `CampEditionsService` tests: propose syncs camp, promote syncs camp, null does not sync
- [ ] `CampPhotosService` tests: all 13 named test cases pass
- [ ] Integration tests: JSON saved/retrieved from DB correctly
- [ ] `dotnet test` passes with 0 failures
- [ ] 90%+ line/branch coverage on new code paths

---

## Error Response Format

All endpoints use `ApiResponse<T>` envelope (already in `Common/Models/ApiResponse.cs`):

| Scenario | HTTP Code | Error Code |
|----------|-----------|------------|
| Validation failure | 400 | `VALIDATION_ERROR` |
| Camp or photo not found | 404 | `NOT_FOUND` |
| Photo doesn't belong to camp | 422 | `BUSINESS_RULE_VIOLATION` |
| Unexpected error | 500 | `INTERNAL_ERROR` |

`NotFoundException` → 404 (handled by `GlobalExceptionMiddleware`)
`BusinessRuleException` → 422 (handled by `GlobalExceptionMiddleware`)

---

## Dependencies

No new NuGet packages required. `System.Text.Json` is already included in .NET 9.

**EF Core migration commands:**

```bash
dotnet ef migrations add AddAccommodationCapacity --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Notes

- **Existing `CampPhoto` entity must NOT be replaced.** It already exists with Google Places fields. This feature adds `Description` and management endpoints only.
- **`IsOriginal = false`** must be set when creating manual photos so they can be distinguished from Google Places photos.
- **`AttributionName`** is required by the existing entity (non-nullable). For manual photos, set it to `string.Empty` or `"Manual"` — not a Google attribution.
- **`ClearPrimaryPhotoAsync` uses `ExecuteUpdateAsync`** (EF Core 7+) — bulk SQL update without loading entities. This avoids N+1 for camps with many photos.
- **Validators in `CampsValidators.cs`** — NOT in `CreateCampValidator.cs` or `UpdateCampValidator.cs`. Check both files; if duplicate validators exist, consolidate into `CampsValidators.cs`.
- **Record DTOs are positional** — adding a parameter requires updating all instantiation sites. Use IDE "Find All References" before making changes.
- **Language:** Validation messages → Spanish. Log messages and code → English.
- **No GDPR concerns** — accommodation data and photo URLs are not sensitive personal data.

---

## Next Steps After Implementation

1. Frontend integration (if applicable) — update API call DTOs to include `accommodationCapacity`
2. OpenAPI schema review — verify `AccommodationCapacity` serializes correctly in Swagger UI
3. PR to `main` after all tests pass and documentation is updated

---

## Implementation Verification

- [ ] **Code Quality:** No analyzer warnings (`TreatWarningsAsErrors = true`). Nullable reference types handled. No unused `using` statements.
- [ ] **Functionality:** All 5 photo endpoints return correct status codes. Accommodation data appears in `GET /api/camps/{id}` and `POST /api/camps/editions/propose` responses.
- [ ] **Testing:** `dotnet test` passes. 90%+ coverage on new code.
- [ ] **Migration:** `dotnet ef database update` applied successfully. 3 new columns visible in DB schema.
- [ ] **Documentation:** `data-model.md` and `api-endpoints.md` updated in English.

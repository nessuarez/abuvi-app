# Feature: Camp Accommodation Capacity & Photos — Enriched Spec

## Context

This feature adds two capabilities to the Camps domain:

1. **Accommodation capacity** — A flexible JSON structure stored in `Camp` and `CampEdition` to describe room types, tents, bungalows, motorhome spots, etc.
2. **Manual photo management** — CRUD operations on `CampPhoto` for non-Google-Places photos (user-uploaded URLs).

> **CRITICAL — Read before implementing:**
>
> - `CampPhoto` already exists in [CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs) with a Google Places-oriented structure. **Do NOT create a new entity.** Extend the existing one.
> - `CampPhotoConfiguration` already exists in [CampPhotoConfiguration.cs](src/Abuvi.API/Data/Configurations/CampPhotoConfiguration.cs).
> - `CampConfiguration` and `CampEditionConfiguration` exist — only add columns.
> - The existing `camp_photos` table already exists. The migration only adds `description` and `accommodation_capacity_json` columns.
> - All photos navigation is already on `Camp.Photos`.

---

## TDD Approach — MANDATORY

**Every ticket: Red → Green → Refactor.**

---

## Business Requirements

### Accommodation Capacity

Los campamentos tienen diferentes tipos de alojamiento que influyen en:

- Capacidad total de camas calculable
- Asignación de participantes a habitaciones
- Pricing diferenciado por tipo de alojamiento
- Toma de decisiones de la junta

**Tipos soportados:**

| Type | Field |
|------|-------|
| Habitaciones privadas con baño | `privateRoomsWithBathroom` |
| Habitaciones privadas baño compartido | `privateRoomsSharedBathroom` |
| Habitaciones compartidas (flexible) | `sharedRooms` (array) |
| Bungalows/Casetas | `bungalows` |
| Tiendas propias del camping | `campOwnedTents` |
| Área tiendas socios (m²) | `memberTentAreaSquareMeters` |
| Capacidad estimada tiendas socios | `memberTentCapacityEstimate` |
| Plazas autocaravanas | `motorhomeSpots` |
| Notas libres | `notes` |

### Auto-sync Camp Template from Edition

When `AccommodationCapacityJson` is updated on a `CampEdition`, the parent `Camp.AccommodationCapacityJson` is automatically synced to the new value. This keeps the camp template current. The update is atomic (same transaction).

**Trigger points:**
- `CampEditionsService.ProposeAsync()` — when the request includes an accommodation capacity
- `CampEditionsService.PromoteToDraftAsync()` — always syncs the edition's current value to the camp

### Manual Photo Management

The existing `CampPhoto` entity supports both Google Places photos (`IsOriginal = true`) and manually managed photos (`IsOriginal = false`). This feature adds:

- A `description` column to the existing `camp_photos` table (nullable, max 500 chars)
- API endpoints to add, update, delete, reorder, and set the primary photo
- Business rule: only one `IsPrimary = true` photo per camp at a time

---

## Data Model Changes

### 1. `AccommodationCapacity` and `SharedRoomInfo` — New Value Objects

**File:** [src/Abuvi.API/Features/Camps/CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs)

Add these classes to the file:

```csharp
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

### 2. `Camp` entity — add fields

**File:** [src/Abuvi.API/Features/Camps/CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs)

Add to the `Camp` class:

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
    AccommodationCapacityJson = capacity == null
        ? null
        : JsonSerializer.Serialize(capacity, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
}
```

### 3. `CampEdition` entity — add fields

**File:** [src/Abuvi.API/Features/Camps/CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs)

Add the exact same `AccommodationCapacityJson` field and `GetAccommodationCapacity()` / `SetAccommodationCapacity()` helper methods to `CampEdition`.

### 4. `CampPhoto` entity — add `Description` field

**File:** [src/Abuvi.API/Features/Camps/CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs)

Add to the existing `CampPhoto` class:

```csharp
public string? Description { get; set; }  // Optional caption, max 500 chars
```

### 5. DTOs

**File:** [src/Abuvi.API/Features/Camps/CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs)

**Update existing records:**

`CreateCampRequest` — add `AccommodationCapacity? AccommodationCapacity` parameter

`UpdateCampRequest` — add `AccommodationCapacity? AccommodationCapacity` parameter

`CampDetailResponse` — add:
- `AccommodationCapacity? AccommodationCapacity`
- `int? CalculatedTotalBedCapacity`

`ProposeCampEditionRequest` — add `AccommodationCapacity? AccommodationCapacity` parameter

`CampEditionResponse` — add:
- `AccommodationCapacity? AccommodationCapacity`
- `int? CalculatedTotalBedCapacity`

`CampPhotoResponse` — add `string? Description` parameter

**New DTOs:**

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

## EF Core Configuration Changes

### 6. `CampConfiguration` — add column

**File:** [src/Abuvi.API/Data/Configurations/CampConfiguration.cs](src/Abuvi.API/Data/Configurations/CampConfiguration.cs)

```csharp
builder.Property(c => c.AccommodationCapacityJson)
    .HasColumnType("text")
    .HasColumnName("accommodation_capacity_json");
```

Also add the Photos relationship (currently missing from CampConfiguration):

```csharp
builder.HasMany(c => c.Photos)
    .WithOne(p => p.Camp)
    .HasForeignKey(p => p.CampId)
    .OnDelete(DeleteBehavior.Cascade);
```

### 7. `CampEditionConfiguration` — add column

**File:** [src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs](src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs)

```csharp
builder.Property(e => e.AccommodationCapacityJson)
    .HasColumnType("text")
    .HasColumnName("accommodation_capacity_json");
```

### 8. `CampPhotoConfiguration` — add column

**File:** [src/Abuvi.API/Data/Configurations/CampPhotoConfiguration.cs](src/Abuvi.API/Data/Configurations/CampPhotoConfiguration.cs)

```csharp
builder.Property(p => p.Description)
    .HasMaxLength(500)
    .HasColumnName("description");
```

---

## Migration

**Command:**
```bash
dotnet ef migrations add AddAccommodationCapacity --project src/Abuvi.API
```

**Expected migration operations:**
1. `AddColumn<string>` on `camps` table: `accommodation_capacity_json` (type `text`, nullable)
2. `AddColumn<string>` on `camp_editions` table: `accommodation_capacity_json` (type `text`, nullable)
3. `AddColumn<string>` on `camp_photos` table: `description` (type `varchar(500)`, nullable)

**File:** `src/Abuvi.API/Migrations/{timestamp}_AddAccommodationCapacity.cs`

> Always review the generated migration before applying. Run `dotnet ef database update` afterward.

---

## Validators

### 9. New validators

**File:** [src/Abuvi.API/Features/Camps/CampsValidators.cs](src/Abuvi.API/Features/Camps/CampsValidators.cs)

Add to existing file:

```csharp
public class AddCampPhotoValidator : AbstractValidator<AddCampPhotoRequest>
{
    public AddCampPhotoValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("La URL de la foto es obligatoria")
            .MaximumLength(2000).WithMessage("La URL no puede superar los 2000 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.Description != null);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden de visualización debe ser mayor o igual a 0");
    }
}

public class UpdateCampPhotoValidator : AbstractValidator<UpdateCampPhotoRequest>
{
    public UpdateCampPhotoValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("La URL de la foto es obligatoria")
            .MaximumLength(2000).WithMessage("La URL no puede superar los 2000 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.Description != null);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden de visualización debe ser mayor o igual a 0");
    }
}
```

**Update existing validators** (`CreateCampValidator`, `UpdateCampValidator`, `ProposeCampEditionValidator`) to add optional validation for `AccommodationCapacity`:

```csharp
When(x => x.AccommodationCapacity != null, () =>
{
    RuleFor(x => x.AccommodationCapacity!.PrivateRoomsWithBathroom)
        .GreaterThanOrEqualTo(0).When(v => v.AccommodationCapacity!.PrivateRoomsWithBathroom.HasValue)
        .WithMessage("El número de habitaciones privadas con baño debe ser mayor o igual a 0");
    // Similar for other nullable int fields...
    RuleForEach(x => x.AccommodationCapacity!.SharedRooms).ChildRules(room =>
    {
        room.RuleFor(r => r.Quantity).GreaterThan(0).WithMessage("La cantidad de habitaciones debe ser mayor que 0");
        room.RuleFor(r => r.BedsPerRoom).GreaterThan(0).WithMessage("El número de camas por habitación debe ser mayor que 0");
    }).When(x => x.AccommodationCapacity?.SharedRooms?.Any() == true);
});
```

---

## Service Layer

### 10. `CampsService` — update create/update

**File:** [src/Abuvi.API/Features/Camps/CampsService.cs](src/Abuvi.API/Features/Camps/CampsService.cs)

**In `CreateAsync`:** After building the `Camp` object, call `camp.SetAccommodationCapacity(request.AccommodationCapacity)`.

**In `UpdateAsync`:** After updating existing fields, call `camp.SetAccommodationCapacity(request.AccommodationCapacity)`.

**In `MapToCampDetailResponse`:** Include `AccommodationCapacity` and `CalculatedTotalBedCapacity`:
```csharp
AccommodationCapacity: camp.GetAccommodationCapacity(),
CalculatedTotalBedCapacity: camp.GetAccommodationCapacity()?.CalculateTotalBedCapacity()
```

### 11. `CampEditionsService` — auto-sync camp template

**File:** [src/Abuvi.API/Features/Camps/CampEditionsService.cs](src/Abuvi.API/Features/Camps/CampEditionsService.cs)

**In `ProposeAsync`:** When `request.AccommodationCapacity != null`:
```csharp
edition.SetAccommodationCapacity(request.AccommodationCapacity);
// After saving the edition, sync to camp:
if (request.AccommodationCapacity != null)
{
    camp.SetAccommodationCapacity(request.AccommodationCapacity);
    await _campsRepository.UpdateAsync(camp, cancellationToken);
}
```

**In `PromoteToDraftAsync`:** If the edition has an AccommodationCapacity, sync it to the camp:
```csharp
if (!string.IsNullOrWhiteSpace(edition.AccommodationCapacityJson))
{
    edition.Camp.SetAccommodationCapacity(edition.GetAccommodationCapacity());
    await _campsRepository.UpdateAsync(edition.Camp, cancellationToken);
}
```

> Note: `PromoteToDraftAsync` must eager-load `edition.Camp`. Update `ICampEditionsRepository.GetByIdAsync` to include `.Include(e => e.Camp)`.

**In `MapToCampEditionResponse`:** Include:
```csharp
AccommodationCapacity: edition.GetAccommodationCapacity(),
CalculatedTotalBedCapacity: edition.GetAccommodationCapacity()?.CalculateTotalBedCapacity()
```

### 12. `CampPhotosService` — new service

**File:** [src/Abuvi.API/Features/Camps/CampPhotosService.cs](src/Abuvi.API/Features/Camps/CampPhotosService.cs) *(new file)*

```csharp
namespace Abuvi.API.Features.Camps;

public class CampPhotosService(ICampsRepository campsRepository)
{
    public async Task<CampPhotoResponse> AddPhotoAsync(Guid campId, AddCampPhotoRequest request, CancellationToken ct);
    public async Task<CampPhotoResponse> UpdatePhotoAsync(Guid campId, Guid photoId, UpdateCampPhotoRequest request, CancellationToken ct);
    public async Task DeletePhotoAsync(Guid campId, Guid photoId, CancellationToken ct);
    public async Task<CampPhotoResponse> SetPrimaryPhotoAsync(Guid campId, Guid photoId, CancellationToken ct);
    public async Task ReorderPhotosAsync(Guid campId, ReorderCampPhotosRequest request, CancellationToken ct);
}
```

**Business rules:**
- `AddPhotoAsync`: Creates a new `CampPhoto` with `IsOriginal = false`. If `request.IsPrimary = true`, first set all other camp photos to `IsPrimary = false` (in the same call to `UpdatePhotosAsync` or `ClearPrimaryAsync` on the repository).
- `UpdatePhotoAsync`: If `request.IsPrimary = true`, first clear `IsPrimary` on all other photos for that camp.
- `DeletePhotoAsync`: Throws `NotFoundException` if photo not found or if `photoId` does not belong to `campId`.
- `SetPrimaryPhotoAsync`: Sets `IsPrimary = true` on the target photo and `false` on all others for that camp. Returns the updated photo.
- `ReorderPhotosAsync`: Updates `DisplayOrder` for each `PhotoOrderItem`. Validates all IDs belong to the camp.

**Error codes (Spanish, `BusinessRuleException`):**
- Photo not found: `"La foto no se encontró en este campamento"`
- Camp not found: `"El campamento no se encontró"`

### 13. `ICampsRepository` — new method signatures

**File:** [src/Abuvi.API/Features/Camps/ICampsRepository.cs](src/Abuvi.API/Features/Camps/ICampsRepository.cs)

Add:
```csharp
Task<CampPhoto?> GetPhotoAsync(Guid campId, Guid photoId, CancellationToken ct);
Task<CampPhoto> AddPhotoAsync(CampPhoto photo, CancellationToken ct);
Task<CampPhoto> UpdatePhotoAsync(CampPhoto photo, CancellationToken ct);
Task DeletePhotoAsync(CampPhoto photo, CancellationToken ct);
Task ClearPrimaryPhotoAsync(Guid campId, CancellationToken ct);
Task UpdatePhotoOrdersAsync(IEnumerable<CampPhoto> photos, CancellationToken ct);
```

> Note: `AddPhotosAsync` (plural) already exists for Google Places batch import. Keep it. The new `AddPhotoAsync` (singular) is for manual additions.

---

## API Endpoints

### 14. `CampsEndpoints` — photo endpoints

**File:** [src/Abuvi.API/Features/Camps/CampsEndpoints.cs](src/Abuvi.API/Features/Camps/CampsEndpoints.cs)

Add a new endpoint group inside `MapCampsEndpoints` (after the camps CRUD group):

```csharp
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

**Handler signatures:**
```csharp
private static async Task<IResult> AddCampPhoto(
    [FromServices] CampPhotosService service,
    Guid campId,
    AddCampPhotoRequest request,
    CancellationToken ct)

private static async Task<IResult> UpdateCampPhoto(
    [FromServices] CampPhotosService service,
    Guid campId,
    Guid photoId,
    UpdateCampPhotoRequest request,
    CancellationToken ct)

private static async Task<IResult> DeleteCampPhoto(
    [FromServices] CampPhotosService service,
    Guid campId,
    Guid photoId,
    CancellationToken ct)

private static async Task<IResult> SetPrimaryPhoto(
    [FromServices] CampPhotosService service,
    Guid campId,
    Guid photoId,
    CancellationToken ct)

private static async Task<IResult> ReorderCampPhotos(
    [FromServices] CampPhotosService service,
    Guid campId,
    ReorderCampPhotosRequest request,
    CancellationToken ct)
```

**Existing accommodation endpoints:** The accommodation data is returned in the existing `CampDetailResponse` and `CampEditionResponse`. No new endpoints needed — the existing `POST /api/camps`, `PUT /api/camps/{id}`, `POST /api/camps/editions/propose` now include `AccommodationCapacity` in their requests.

---

## DI Registration

**File:** [src/Abuvi.API/Program.cs](src/Abuvi.API/Program.cs)

Add:
```csharp
builder.Services.AddScoped<CampPhotosService>();
```

---

## Files to Create or Modify

| File | Action |
|------|--------|
| [src/Abuvi.API/Features/Camps/CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs) | Add `AccommodationCapacity`, `SharedRoomInfo`, helper methods on `Camp` and `CampEdition`, `Description` on `CampPhoto`, update request/response DTOs, add `AddCampPhotoRequest`, `UpdateCampPhotoRequest`, `ReorderCampPhotosRequest`, `PhotoOrderItem` |
| [src/Abuvi.API/Features/Camps/CampsService.cs](src/Abuvi.API/Features/Camps/CampsService.cs) | Handle `AccommodationCapacity` in create/update, update response mapping |
| [src/Abuvi.API/Features/Camps/CampEditionsService.cs](src/Abuvi.API/Features/Camps/CampEditionsService.cs) | Handle `AccommodationCapacity` in propose and promote, auto-sync to camp |
| [src/Abuvi.API/Features/Camps/CampPhotosService.cs](src/Abuvi.API/Features/Camps/CampPhotosService.cs) | **NEW** — full CRUD + reorder + set-primary |
| [src/Abuvi.API/Features/Camps/ICampsRepository.cs](src/Abuvi.API/Features/Camps/ICampsRepository.cs) | Add photo-specific method signatures |
| [src/Abuvi.API/Features/Camps/CampsRepository.cs](src/Abuvi.API/Features/Camps/CampsRepository.cs) | Implement new photo methods |
| [src/Abuvi.API/Features/Camps/CampsEndpoints.cs](src/Abuvi.API/Features/Camps/CampsEndpoints.cs) | Add `/api/camps/{campId}/photos` endpoint group |
| [src/Abuvi.API/Features/Camps/CampsValidators.cs](src/Abuvi.API/Features/Camps/CampsValidators.cs) | Add `AddCampPhotoValidator`, `UpdateCampPhotoValidator`; update existing validators for `AccommodationCapacity` |
| [src/Abuvi.API/Data/Configurations/CampConfiguration.cs](src/Abuvi.API/Data/Configurations/CampConfiguration.cs) | Add `accommodation_capacity_json` column and `Photos` relationship |
| [src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs](src/Abuvi.API/Data/Configurations/CampEditionConfiguration.cs) | Add `accommodation_capacity_json` column |
| [src/Abuvi.API/Data/Configurations/CampPhotoConfiguration.cs](src/Abuvi.API/Data/Configurations/CampPhotoConfiguration.cs) | Add `description` column |
| [src/Abuvi.API/Program.cs](src/Abuvi.API/Program.cs) | Register `CampPhotosService` |
| `src/Abuvi.API/Migrations/{timestamp}_AddAccommodationCapacity.cs` | **NEW** — generated migration |

---

## Testing

### Unit Tests

#### `AccommodationCapacityTests`

**File:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationCapacityTests.cs`

```
CalculateTotalBedCapacity_WithAllTypes_ReturnsCorrectSum
CalculateTotalBedCapacity_WithNullPrivateRooms_CountsAsZero
CalculateTotalBedCapacity_WithNullSharedRooms_CountsAsZero
CalculateTotalBedCapacity_WithEmptySharedRoomsList_CountsAsZero
CalculateTotalBedCapacity_WithOnlySharedRooms_ReturnsCorrectSum
```

#### `CampAccommodationSerializationTests`

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampAccommodationSerializationTests.cs`

```
SetAccommodationCapacity_WithValidCapacity_SerializesAsCamelCase
SetAccommodationCapacity_WithNull_SetsJsonToNull
GetAccommodationCapacity_WithValidJson_DeserializesCorrectly
GetAccommodationCapacity_WithNullJson_ReturnsNull
GetAccommodationCapacity_WithEmptyJson_ReturnsNull
SetThenGet_RoundTrip_PreservesAllValues
```

#### `CampsServiceTests` (extend existing)

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`

```
CreateAsync_WithAccommodationCapacity_SavesAndReturnsCapacity
CreateAsync_WithNullAccommodationCapacity_SavesNullCapacity
UpdateAsync_WithAccommodationCapacity_UpdatesAndReturnsCapacity
GetByIdAsync_WhenCampHasAccommodation_ReturnsCapacityAndCalculatedTotal
```

#### `CampEditionsServiceTests` (extend existing)

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs`

```
ProposeAsync_WithAccommodationCapacity_SetsCapacityOnEdition
ProposeAsync_WithAccommodationCapacity_SyncsCampTemplate
ProposeAsync_WithNullAccommodationCapacity_DoesNotSyncCampTemplate
PromoteToDraftAsync_WhenEditionHasCapacity_SyncsCampTemplate
PromoteToDraftAsync_WhenEditionHasNoCapacity_DoesNotSyncCampTemplate
```

#### `CampPhotosServiceTests`

**File:** `src/Abuvi.Tests/Unit/Features/Camps/CampPhotosServiceTests.cs`

```
AddPhotoAsync_WithValidRequest_CreatesPhotoWithIsOriginalFalse
AddPhotoAsync_WithIsPrimaryTrue_ClearsPreviousPrimaryBeforeAdding
AddPhotoAsync_WhenCampNotFound_ThrowsNotFoundException
UpdatePhotoAsync_WithValidRequest_UpdatesPhoto
UpdatePhotoAsync_WithIsPrimaryTrue_ClearsPreviousPrimary
UpdatePhotoAsync_WhenPhotoNotFound_ThrowsNotFoundException
UpdatePhotoAsync_WhenPhotoDoesNotBelongToCamp_ThrowsNotFoundException
DeletePhotoAsync_WithValidIds_DeletesPhoto
DeletePhotoAsync_WhenPhotoNotFound_ThrowsNotFoundException
SetPrimaryPhotoAsync_WithValidIds_SetsPrimaryAndClearsOthers
SetPrimaryPhotoAsync_WhenPhotoNotFound_ThrowsNotFoundException
ReorderPhotosAsync_WithValidOrder_UpdatesDisplayOrders
ReorderPhotosAsync_WhenPhotoIdDoesNotBelongToCamp_ThrowsBusinessRuleException
```

### Integration Tests

**File:** `src/Abuvi.Tests/Integration/Features/Camps/CampAccommodationIntegrationTests.cs`

```
Camp_WithAccommodationJson_CanBeSavedAndRetrievedFromDatabase
CampEdition_WithAccommodationJson_CanBeSavedAndRetrievedFromDatabase
```

### Test Builders

Add to `src/Abuvi.Tests/Helpers/Builders/`:

**`AccommodationCapacityBuilder.cs`:**
```csharp
public class AccommodationCapacityBuilder
{
    private int? _privateRoomsWithBathroom;
    private int? _privateRoomsSharedBathroom;
    private List<SharedRoomInfo>? _sharedRooms;

    public AccommodationCapacityBuilder WithPrivateRoomsWithBathroom(int count)
    { _privateRoomsWithBathroom = count; return this; }

    public AccommodationCapacityBuilder WithSharedRooms(List<SharedRoomInfo> rooms)
    { _sharedRooms = rooms; return this; }

    public AccommodationCapacity Build() => new()
    {
        PrivateRoomsWithBathroom = _privateRoomsWithBathroom,
        PrivateRoomsSharedBathroom = _privateRoomsSharedBathroom,
        SharedRooms = _sharedRooms
    };
}
```

---

## Implementation Order (TDD per ticket)

### Ticket 1 — AccommodationCapacity value objects and serialization

1. Write failing unit tests for `CalculateTotalBedCapacity` and round-trip serialization
2. Add `AccommodationCapacity`, `SharedRoomInfo` to `CampsModels.cs`
3. Add helper methods to `Camp` and `CampEdition`
4. All tests green

### Ticket 2 — Database migration

1. Write failing integration tests that try to save/retrieve `AccommodationCapacityJson`
2. Add EF config changes to `CampConfiguration`, `CampEditionConfiguration`, `CampPhotoConfiguration`
3. Add columns to entities in `CampsModels.cs`
4. Run `dotnet ef migrations add AddAccommodationCapacity`
5. Apply migration, verify tests green

### Ticket 3 — CampsService accommodation integration

1. Write failing service unit tests (accommodation in create/update/get)
2. Update `CampsService.CreateAsync`, `UpdateAsync`, `MapToCampDetailResponse`
3. Update `CreateCampRequest`, `UpdateCampRequest`, `CampDetailResponse` DTOs
4. All tests green

### Ticket 4 — CampEditionsService auto-sync

1. Write failing service unit tests (propose with capacity syncs camp; promote syncs)
2. Update `CampEditionsService.ProposeAsync` and `PromoteToDraftAsync`
3. Update `ProposeCampEditionRequest`, `CampEditionResponse` DTOs
4. Ensure `ICampEditionsRepository.GetByIdAsync` eager-loads `Camp`
5. All tests green

### Ticket 5 — CampPhotosService and repository

1. Write failing unit tests for all photo service methods
2. Add new methods to `ICampsRepository` and `CampsRepository`
3. Implement `CampPhotosService`
4. Register in `Program.cs`
5. All tests green

### Ticket 6 — Camp photo API endpoints

1. Write failing integration/endpoint tests
2. Add `photosGroup` to `CampsEndpoints.MapCampsEndpoints`
3. Add validators to `CampsValidators.cs`
4. All tests green

---

## Acceptance Criteria

- [ ] `AccommodationCapacity.CalculateTotalBedCapacity()` returns correct values for all scenarios
- [ ] JSON serialization uses camelCase and omits null fields
- [ ] `Camp.SetAccommodationCapacity()` / `GetAccommodationCapacity()` round-trip correctly
- [ ] Same helpers work identically on `CampEdition`
- [ ] Migration adds 3 columns without data loss
- [ ] `POST /api/camps` accepts `AccommodationCapacity` and returns it with calculated total
- [ ] `PUT /api/camps/{id}` same
- [ ] `GET /api/camps/{id}` returns accommodation data
- [ ] `POST /api/camps/editions/propose` accepts `AccommodationCapacity`, updates edition AND camp template
- [ ] `POST /api/camps/editions/{id}/promote` syncs edition accommodation to camp template
- [ ] `POST /api/camps/{campId}/photos` creates a manual photo (`IsOriginal = false`)
- [ ] `PUT /api/camps/{campId}/photos/{photoId}` updates photo metadata
- [ ] `DELETE /api/camps/{campId}/photos/{photoId}` removes photo
- [ ] `POST /api/camps/{campId}/photos/{photoId}/set-primary` sets `IsPrimary = true`, clears others
- [ ] `PUT /api/camps/{campId}/photos/reorder` updates `DisplayOrder` for specified photos
- [ ] Only one `IsPrimary = true` per camp enforced
- [ ] All endpoints require `Admin` or `Board` role
- [ ] Spanish validation messages in all new validators
- [ ] All unit + integration tests pass
- [ ] 90%+ branch/line coverage on new code

---

## Non-Functional Requirements

- **Performance:** `ClearPrimaryPhotoAsync` must use a bulk update (`ExecuteUpdateAsync`) rather than loading all photos into memory.
- **Atomicity:** Camp template sync (edition → camp) must complete in the same `SaveChangesAsync` call where possible, or as two sequential saves with documented rollback risk.
- **Security:** All photo endpoints enforce `Admin` or `Board` authorization (same as the rest of the Camps group).
- **GDPR:** No sensitive data involved in this feature.

---

## Out of Scope (Future Enhancements)

- Blob storage for actual image uploads (Phase 2)
- Image optimization / CDN
- Capacity analytics and reporting
- Room assignment algorithm

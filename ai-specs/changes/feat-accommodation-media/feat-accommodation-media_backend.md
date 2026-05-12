# Backend Implementation Plan: feat-accommodation-media — Accommodation Media

## Overview

Allow admins to attach photos/videos to accommodation zones, individual accommodations, and accommodation-type defaults. Expose primary thumbnails on the assignment board.

The `MediaItem` entity already exists with `ZoneId` and `AccommodationId` FKs and a working endpoint/service/repository. This feature **extends** that entity rather than creating a new one, adding `IsPrimary`, `DisplayOrder`, and a new `AccommodationTypeMedia` table for type-level defaults.

Architecture: **Vertical Slice** — changes are scoped to `Features/MediaItems/` for the MediaItem extensions and a new `AccommodationTypeMedia` sub-concern within `Features/Camps/`.

---

## Architecture Context

### Modified slices
- `src/Abuvi.API/Features/MediaItems/` — extend entity, service, endpoints
- `src/Abuvi.API/Features/Camps/` — extend models, zone/accommodation response DTOs, repositories

### Files to create
| File | Purpose |
|---|---|
| `src/Abuvi.API/Features/Camps/AccommodationTypeMediaModels.cs` | Entity + DTOs for type defaults |
| `src/Abuvi.API/Data/Configurations/AccommodationTypeMediaConfiguration.cs` | EF config |
| `src/Abuvi.API/Features/Camps/AccommodationTypeMediaRepository.cs` | Repository + interface |
| `src/Abuvi.API/Features/Camps/AccommodationTypeMediaService.cs` | Service |
| `src/Abuvi.API/Features/Camps/AccommodationTypeMediaEndpoints.cs` | Endpoints |
| `src/Abuvi.Tests/Unit/Features/MediaItems/AccommodationMediaServiceTests.cs` | Unit tests |
| `src/Abuvi.Tests/Unit/Features/Camps/AccommodationTypeMediaServiceTests.cs` | Unit tests |

### Files to modify
| File | Change |
|---|---|
| `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs` | Add `IsPrimary`, `DisplayOrder` to entity; update DTOs |
| `src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs` | Add bidirectional config for accommodation; add DisplayOrder/IsPrimary config |
| `src/Abuvi.API/Features/Camps/CampsModels.cs` | Add `MediaItems` nav to `CampEditionAccommodation`; add `PrimaryThumbnailUrl`/`PrimaryFileUrl` to zone/accommodation response DTOs |
| `src/Abuvi.API/Features/Camps/AccommodationZoneConfiguration.cs` | Already has Zone↔MediaItem; no change needed |
| `src/Abuvi.API/Features/Camps/CampEditionAccommodationConfiguration.cs` | Add bidirectional `WithMany` config for `MediaItems` |
| `src/Abuvi.API/Features/MediaItems/MediaItemsService.cs` | Add `SetPrimaryAsync`, `GetForAccommodationAsync`, `GetForZoneAsync`; enforce max-10 rule |
| `src/Abuvi.API/Features/MediaItems/MediaItemsRepository.cs` | Add `GetByAccommodationAsync`, `GetByZoneAsync`, `GetPrimaryAsync`, `CountByOwnerAsync` |
| `src/Abuvi.API/Features/MediaItems/MediaItemsEndpoints.cs` | Add PATCH `.../primary` endpoints for zones and accommodations |
| `src/Abuvi.API/Features/Camps/AccommodationZonesService.cs` | Include primary MediaItem in zone responses |
| `src/Abuvi.API/Features/Camps/CampEditionAccommodationsService.cs` | Include primary MediaItem in accommodation responses |
| `src/Abuvi.API/Data/AbuviDbContext.cs` | Add `DbSet<AccommodationTypeMedia>` |
| `src/Abuvi.API/Program.cs` | Register new services; map new endpoints |

---

## Implementation Steps

### Step 0 — Create Feature Branch

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-accommodation-media-backend
git branch
```

> All code changes start from this branch. Do not work on `dev` directly.

---

### Step 1 — Extend `MediaItem` Entity

**File:** `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs`

Add two fields to the `MediaItem` class immediately after `Context`:

```csharp
public int DisplayOrder { get; set; } = 0;   // sort order within the owner scope
public bool IsPrimary { get; set; } = false;  // one primary per owner scope
```

No other fields change. `Year`, `Decade`, `IsApproved`, `IsPublished` are retained for backward compatibility with memories.

---

### Step 2 — Update `MediaItemConfiguration`

**File:** `src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs`

**Add** the following properties and relationship after the existing Zone relationship:

```csharp
builder.Property(m => m.DisplayOrder)
    .IsRequired()
    .HasDefaultValue(0)
    .HasColumnName("display_order");

builder.Property(m => m.IsPrimary)
    .IsRequired()
    .HasDefaultValue(false)
    .HasColumnName("is_primary");

// Bidirectional: Accommodation → MediaItems (currently one-way — change WithMany() to use collection)
builder.HasOne(m => m.Accommodation)
    .WithMany(a => a.MediaItems)          // <-- was WithMany() (no nav), now uses collection
    .HasForeignKey(m => m.AccommodationId)
    .OnDelete(DeleteBehavior.SetNull);     // match Zone: SetNull on delete

builder.HasIndex(m => new { m.ZoneId, m.IsPrimary })
    .HasDatabaseName("ix_media_items_zone_id_is_primary");

builder.HasIndex(m => new { m.AccommodationId, m.IsPrimary })
    .HasDatabaseName("ix_media_items_accommodation_id_is_primary");
```

> **Note:** The Zone relationship is already bidirectional (configured in `AccommodationZoneConfiguration.cs`). Do **not** duplicate it here.

---

### Step 3 — Add `MediaItems` to `CampEditionAccommodation`

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Add to `CampEditionAccommodation`:

```csharp
public ICollection<MediaItem> MediaItems { get; set; } = [];
```

Place it after the existing `FeatureAssignments` collection, before any closing brace.

---

### Step 4 — Update DTOs for MediaItem

**File:** `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs`

Update `MediaItemResponse` record to include the new fields:

```csharp
public record MediaItemResponse(
    Guid Id,
    // ... existing fields unchanged ...
    int DisplayOrder,       // ADD
    bool IsPrimary,         // ADD
    // ... rest unchanged ...
);
```

Add a new request DTO for the POST to accommodation/zone media:

```csharp
public record AddAccommodationMediaRequest(
    string FileUrl,
    string? ThumbnailUrl,
    MediaItemType Type,
    string Title,
    string? Caption,       // maps to Description
    int DisplayOrder = 0);

public record SetMediaPrimaryRequest();  // empty body — PATCH /primary needs no payload
```

Add a new response DTO used only by zone/accommodation list queries:

```csharp
public record MediaItemSummaryResponse(
    Guid Id,
    string FileUrl,
    string? ThumbnailUrl,
    bool IsPrimary);
```

---

### Step 5 — Extend Zone / Accommodation Response DTOs

**File:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Find `AccommodationZoneResponse` and add:

```csharp
string? PrimaryThumbnailUrl,
string? PrimaryFileUrl
```

Find `AssignmentAccommodationResponse` (and any other accommodation response record used by the assignment board — check `AccommodationAssignmentsService.cs` for which DTO is used) and add the same two fields.

Also find `CampEditionAccommodationResponse` (used by the accommodations management panel) and add the two fields there too.

---

### Step 6 — Extend `MediaItemsRepository`

**File:** `src/Abuvi.API/Features/MediaItems/MediaItemsRepository.cs`

Add to the interface and implementation:

```csharp
Task<IReadOnlyList<MediaItem>> GetByZoneAsync(Guid zoneId, CancellationToken ct);
Task<IReadOnlyList<MediaItem>> GetByAccommodationAsync(Guid accommodationId, CancellationToken ct);
Task<int> CountByZoneAsync(Guid zoneId, CancellationToken ct);
Task<int> CountByAccommodationAsync(Guid accommodationId, CancellationToken ct);
Task ClearPrimaryForZoneAsync(Guid zoneId, CancellationToken ct);
Task ClearPrimaryForAccommodationAsync(Guid accommodationId, CancellationToken ct);
```

Implementation notes:
- All queries: `.AsNoTracking()` for reads, tracked for writes
- `GetBy*` queries: `OrderBy(m => m.IsPrimary).ThenBy(m => m.DisplayOrder)`
- `ClearPrimary*`: bulk update — `ExecuteUpdateAsync(s => s.SetProperty(m => m.IsPrimary, false))`

---

### Step 7 — Extend `MediaItemsService`

**File:** `src/Abuvi.API/Features/MediaItems/MediaItemsService.cs`

Add the following methods:

#### `AddToZoneAsync`

```csharp
public async Task<MediaItemResponse> AddToZoneAsync(
    Guid zoneId, AddAccommodationMediaRequest request, Guid uploadedByUserId, CancellationToken ct)
```

Implementation steps:
1. Verify zone exists (call zone repository or `db.AccommodationZones.AnyAsync`). Throw `NotFoundException` if not.
2. Count existing items: `await _repository.CountByZoneAsync(zoneId, ct)`. If ≥ 10, throw `BusinessRuleException("No se pueden añadir más de 10 archivos por zona")`.
3. Create entity: `ZoneId = zoneId`, `IsApproved = true`, `IsPublished = true`, `IsPrimary = false` (caller uses PATCH /primary to set primary).
4. `await _repository.AddAsync(item, ct)`.
5. Return mapped `MediaItemResponse`.

#### `AddToAccommodationAsync`

Identical pattern to `AddToZoneAsync` but uses `AccommodationId` and `CountByAccommodationAsync`. Error: `"No se pueden añadir más de 10 archivos por alojamiento"`.

#### `SetPrimaryForZoneAsync`

```csharp
public async Task SetPrimaryForZoneAsync(Guid zoneId, Guid mediaId, CancellationToken ct)
```

1. Load item by `mediaId` from db. Throw `NotFoundException` if not found or if `item.ZoneId != zoneId`.
2. `await _repository.ClearPrimaryForZoneAsync(zoneId, ct)` — bulk clear.
3. Set `item.IsPrimary = true`, `await _repository.UpdateAsync(item, ct)`.

#### `SetPrimaryForAccommodationAsync`

Identical pattern for accommodation scope.

---

### Step 8 — Update Zone / Accommodation Queries to Include Primary Media

**File:** `src/Abuvi.API/Features/Camps/AccommodationZonesRepository.cs`

In the query that builds zone list responses (used by the assignment panel and zone management), update the `.Select()` projection to include:

```csharp
PrimaryThumbnailUrl = z.MediaItems
    .Where(m => m.IsPrimary)
    .Select(m => m.ThumbnailUrl)
    .FirstOrDefault(),
PrimaryFileUrl = z.MediaItems
    .Where(m => m.IsPrimary)
    .Select(m => m.FileUrl)
    .FirstOrDefault()
```

> This is a subquery within the `.Select()` projection — EF Core will translate it to a correlated subquery (single additional SQL per row, not N+1, because it's part of the same projection).

**File:** `src/Abuvi.API/Features/Camps/CampEditionAccommodationsRepository.cs`

Apply the same pattern to the accommodation list query.

---

### Step 9 — Add Media Endpoints for Zones and Accommodations

**File:** `src/Abuvi.API/Features/MediaItems/MediaItemsEndpoints.cs`

Add two new `MapGroup` blocks to the existing `MapMediaItemsEndpoints` extension method:

#### Zone media group

```
POST   /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media
GET    /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media
DELETE /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media/{mediaId}
PATCH  /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media/{mediaId}/primary
```

- `POST`: requires `Admin` or `Board` role. Calls `service.AddToZoneAsync(...)`. Returns `201 Created`.
- `GET`: requires any authenticated user. Calls `service.GetByZoneAsync(zoneId, ct)`. Returns `200 OK`.
- `DELETE`: requires `Admin` or `Board`. Removes DB record only (no blob deletion). Returns `204 No Content`.
- `PATCH /primary`: requires `Admin` or `Board`. Calls `service.SetPrimaryForZoneAsync(zoneId, mediaId, ct)`. Returns `204 No Content`.

Validate `editionId` ownership of `zoneId` in the service or endpoint (ensure zone belongs to the given edition — query `AccommodationZones` WHERE `Id = zoneId AND CampEditionId = editionId`). Return `404` if not matched.

#### Accommodation media group

```
POST   /api/camps/editions/{editionId}/accommodations/{accommodationId}/media
GET    /api/camps/editions/{editionId}/accommodations/{accommodationId}/media
DELETE /api/camps/editions/{editionId}/accommodations/{accommodationId}/media/{mediaId}
PATCH  /api/camps/editions/{editionId}/accommodations/{accommodationId}/media/{mediaId}/primary
```

Same rules as zone group, validating `accommodation.CampEditionId == editionId`.

---

### Step 10 — Create `AccommodationTypeMedia` Entity (Type Defaults)

The `MediaItem` entity has no accommodation type concept. Type-level defaults require a separate lightweight entity.

**File:** `src/Abuvi.API/Features/Camps/AccommodationTypeMediaModels.cs`

```csharp
namespace Abuvi.API.Features.Camps;

public class AccommodationTypeMedia
{
    public Guid Id { get; set; }
    public AccommodationType AccommodationType { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsPrimary { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record AddAccommodationTypeMediaRequest(
    AccommodationType AccommodationType,
    string FileUrl,
    string? ThumbnailUrl,
    string? Caption,
    int DisplayOrder = 0);

public record AccommodationTypeMediaResponse(
    Guid Id,
    string AccommodationType,
    string FileUrl,
    string? ThumbnailUrl,
    string? Caption,
    int DisplayOrder,
    bool IsPrimary,
    DateTime CreatedAt);
```

---

### Step 11 — Create `AccommodationTypeMediaConfiguration`

**File:** `src/Abuvi.API/Data/Configurations/AccommodationTypeMediaConfiguration.cs`

```csharp
public class AccommodationTypeMediaConfiguration : IEntityTypeConfiguration<AccommodationTypeMedia>
{
    public void Configure(EntityTypeBuilder<AccommodationTypeMedia> builder)
    {
        builder.ToTable("accommodation_type_media");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(m => m.AccommodationType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("accommodation_type");
        builder.Property(m => m.FileUrl).IsRequired().HasMaxLength(500).HasColumnName("file_url");
        builder.Property(m => m.ThumbnailUrl).HasMaxLength(500).HasColumnName("thumbnail_url");
        builder.Property(m => m.Caption).HasMaxLength(200).HasColumnName("caption");
        builder.Property(m => m.DisplayOrder).IsRequired().HasDefaultValue(0).HasColumnName("display_order");
        builder.Property(m => m.IsPrimary).IsRequired().HasDefaultValue(false).HasColumnName("is_primary");
        builder.Property(m => m.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(m => m.UpdatedAt).IsRequired().HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        builder.HasIndex(m => m.AccommodationType).HasDatabaseName("ix_accommodation_type_media_type");
        builder.ToTable(t => t.HasCheckConstraint("CK_AccommodationTypeMedia_DisplayOrder", "display_order >= 0"));
    }
}
```

---

### Step 12 — Create `AccommodationTypeMediaRepository`

**File:** `src/Abuvi.API/Features/Camps/AccommodationTypeMediaRepository.cs`

```csharp
public interface IAccommodationTypeMediaRepository
{
    Task<IReadOnlyList<AccommodationTypeMedia>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<AccommodationTypeMedia>> GetByTypeAsync(AccommodationType type, CancellationToken ct);
    Task<AccommodationTypeMedia?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<int> CountByTypeAsync(AccommodationType type, CancellationToken ct);
    Task AddAsync(AccommodationTypeMedia item, CancellationToken ct);
    Task UpdateAsync(AccommodationTypeMedia item, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task ClearPrimaryForTypeAsync(AccommodationType type, CancellationToken ct);
}
```

Implementation: standard EF Core queries with `AsNoTracking()` on reads, bulk `ExecuteUpdateAsync` for `ClearPrimary`.

---

### Step 13 — Create `AccommodationTypeMediaService`

**File:** `src/Abuvi.API/Features/Camps/AccommodationTypeMediaService.cs`

Methods:
- `GetAllAsync` — returns all grouped by type
- `GetByTypeAsync(AccommodationType type, ct)` — filtered list
- `AddAsync(AddAccommodationTypeMediaRequest request, ct)` — enforces max 10 per type; validates enum value is defined
- `SetPrimaryAsync(Guid mediaId, ct)` — bulk clear for type, then set primary
- `DeleteAsync(Guid mediaId, ct)` — removes DB record (no blob deletion)

Business rules:
- Max 10 per `AccommodationType`. Error: `"No se pueden añadir más de 10 archivos para este tipo de alojamiento"`.
- Validate `AccommodationType` enum: if the string from route is not a valid enum value, throw `BusinessRuleException("Tipo de alojamiento inválido")`.

---

### Step 14 — Create `AccommodationTypeMediaEndpoints`

**File:** `src/Abuvi.API/Features/Camps/AccommodationTypeMediaEndpoints.cs`

```
GET    /api/accommodation-types/media            → all type defaults (authenticated)
GET    /api/accommodation-types/{type}/media     → by type (authenticated)
POST   /api/accommodation-types/{type}/media     → add (Admin/Board)
DELETE /api/accommodation-types/media/{mediaId}  → delete (Admin/Board)
PATCH  /api/accommodation-types/media/{mediaId}/primary → set primary (Admin/Board)
```

Register via extension method `MapAccommodationTypeMediaEndpoints`.

FluentValidation for `AddAccommodationTypeMediaRequest`:
- `FileUrl`: `NotEmpty().MaximumLength(500).Must(url => url.StartsWith("https://")).WithMessage("La URL del archivo no es válida")`
- `Caption`: `MaximumLength(200).WithMessage("La descripción no puede superar los 200 caracteres")`
- `DisplayOrder`: `GreaterThanOrEqualTo(0).WithMessage("El orden debe ser mayor o igual a 0")`

Create `AddAccommodationTypeMediaRequestValidator` in the same file (or adjacent `...Validator.cs`).

Also add validator for `AddAccommodationMediaRequest` (used by zone/accommodation endpoints):
- Same `FileUrl` and `Caption` rules.

---

### Step 15 — Register in `Program.cs` and `AbuviDbContext`

**File:** `src/Abuvi.API/Data/AbuviDbContext.cs`

Add:
```csharp
public DbSet<AccommodationTypeMedia> AccommodationTypeMedia => Set<AccommodationTypeMedia>();
```

**File:** `src/Abuvi.API/Program.cs`

In the service registration section (after accommodation assignments block):
```csharp
// Accommodation Type Media
builder.Services.AddScoped<IAccommodationTypeMediaRepository, AccommodationTypeMediaRepository>();
builder.Services.AddScoped<AccommodationTypeMediaService>();
```

In the endpoint mapping section (after `app.MapAccommodationFeaturesEndpoints()`):
```csharp
app.MapAccommodationTypeMediaEndpoints();
```

The zone/accommodation media endpoints are registered inside the existing `MapMediaItemsEndpoints` call (Step 9), so no additional line needed there.

---

### Step 16 — Create EF Core Migration

```bash
dotnet ef migrations add AddAccommodationMediaFields --project src/Abuvi.API
```

This migration will:
1. Add `display_order` (int, default 0, NOT NULL) and `is_primary` (bool, default false, NOT NULL) to `media_items`
2. Create `accommodation_type_media` table with all fields
3. Update `media_items.accommodation_id` relationship to match bidirectional configuration (if EF detects config change)
4. Add indexes: `ix_media_items_zone_id_is_primary`, `ix_media_items_accommodation_id_is_primary`

**Review the migration before applying.** Ensure no unintended changes appear.

```bash
dotnet ef database update --project src/Abuvi.API
```

---

### Step 17 — Write Unit Tests

#### File 1: `src/Abuvi.Tests/Unit/Features/MediaItems/AccommodationMediaServiceTests.cs`

Test class `AccommodationMediaServiceTests`. SUT: `MediaItemsService`. Mock: `IMediaItemsRepository` + a zone/accommodation existence checker (or `AbuviDbContext` mock if existence check is inline).

**Test cases (zone scope):**

```
AddToZoneAsync_WhenZoneExists_AndUnderLimit_ReturnsCreatedItem
AddToZoneAsync_WhenZoneDoesNotExist_ThrowsNotFoundException
AddToZoneAsync_WhenAtMaxItems_ThrowsBusinessRuleException
AddToZoneAsync_WhenFileUrlDoesNotStartWithHttps_ThrowsValidationException
SetPrimaryForZoneAsync_WhenItemExists_ClearsPreviousAndSetsNew
SetPrimaryForZoneAsync_WhenItemNotFound_ThrowsNotFoundException
SetPrimaryForZoneAsync_WhenItemBelongsToDifferentZone_ThrowsNotFoundException
DeleteMediaAsync_WhenItemExists_RemovesRecord
DeleteMediaAsync_WhenItemNotFound_ThrowsNotFoundException
```

**Test cases (accommodation scope):**

Same set as zone, prefixed with `AddToAccommodation*`, `SetPrimaryForAccommodation*`.

#### File 2: `src/Abuvi.Tests/Unit/Features/Camps/AccommodationTypeMediaServiceTests.cs`

Test class `AccommodationTypeMediaServiceTests`. SUT: `AccommodationTypeMediaService`. Mock: `IAccommodationTypeMediaRepository`.

```
AddAsync_WhenValidTypeAndUnderLimit_ReturnsCreatedItem
AddAsync_WhenAtMaxItems_ThrowsBusinessRuleException
AddAsync_WhenInvalidAccommodationType_ThrowsBusinessRuleException
SetPrimaryAsync_WhenItemExists_ClearsPreviousAndSetsNew
SetPrimaryAsync_WhenItemNotFound_ThrowsNotFoundException
DeleteAsync_WhenItemExists_RemovesRecord
DeleteAsync_WhenItemNotFound_ThrowsNotFoundException
GetAllAsync_ReturnsAllItems
GetByTypeAsync_WhenTypeValid_ReturnsFilteredItems
```

All tests follow AAA pattern with `NSubstitute`. Aim for ≥ 90% branch coverage.

---

### Step 18 — Update Technical Documentation

1. **`ai-specs/specs/data-model.md`** (if it exists): Add `accommodation_type_media` table, and note the new `display_order` + `is_primary` columns on `media_items`.
2. **`ai-specs/specs/api-spec.yml`** (if it exists): Add the new endpoint paths for zone media, accommodation media, and type media.
3. **No standards files need updating** — no new libraries or architectural patterns are introduced.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Extend `MediaItem` entity (add `IsPrimary`, `DisplayOrder`)
3. Step 2 — Update `MediaItemConfiguration`
4. Step 3 — Add `MediaItems` navigation to `CampEditionAccommodation`
5. Step 4 — Update `MediaItemResponse` DTO + add new request DTOs
6. Step 5 — Extend zone/accommodation response DTOs with `PrimaryThumbnailUrl`/`PrimaryFileUrl`
7. Step 6 — Extend `MediaItemsRepository` with new methods
8. Step 7 — Extend `MediaItemsService` with `AddToZoneAsync`, `AddToAccommodationAsync`, `SetPrimaryForZoneAsync`, `SetPrimaryForAccommodationAsync`
9. Step 8 — Update zone/accommodation repository queries to include primary media
10. Step 9 — Add zone/accommodation media endpoints
11. Step 10 — Create `AccommodationTypeMedia` entity + DTOs
12. Step 11 — Create `AccommodationTypeMediaConfiguration`
13. Step 12 — Create `AccommodationTypeMediaRepository`
14. Step 13 — Create `AccommodationTypeMediaService`
15. Step 14 — Create `AccommodationTypeMediaEndpoints` + validators
16. Step 15 — Register in `Program.cs` and `AbuviDbContext`
17. Step 16 — Run and review EF Core migration
18. Step 17 — Write unit tests
19. Step 18 — Update technical documentation

---

## Testing Checklist

- [ ] `dotnet build` passes with zero warnings
- [ ] `dotnet test` passes — all tests green
- [ ] Coverage ≥ 90% branches/lines on new service methods
- [ ] `POST /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media` returns `201`
- [ ] `GET /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media` returns `200` with ordered list
- [ ] `PATCH .../primary` returns `204`, subsequent GET shows only one `IsPrimary = true`
- [ ] `DELETE .../media/{id}` returns `204`, item no longer appears in GET
- [ ] Posting an 11th media item returns `422` with Spanish message
- [ ] Zone/accommodation list responses include `primaryThumbnailUrl` (non-null when a primary item is set)
- [ ] Type defaults endpoints work for all 5 `AccommodationType` values
- [ ] Invalid type string in route (e.g. `/accommodation-types/Unknown/media`) returns `422`

---

## Error Response Format

All errors use `ApiResponse<T>` envelope:

```json
// 422 — max items exceeded
{ "success": false, "data": null, "error": { "message": "No se pueden añadir más de 10 archivos por zona", "code": "BUSINESS_RULE_VIOLATION" } }

// 404 — zone not found
{ "success": false, "data": null, "error": { "message": "No se encontró la zona con el ID especificado", "code": "NOT_FOUND" } }

// 400 — validation (invalid URL)
{ "success": false, "data": null, "error": { "message": "La URL del archivo no es válida", "code": "VALIDATION_ERROR" } }
```

| Status | When |
|---|---|
| 200 | GET list or single |
| 201 | POST (media added) |
| 204 | PATCH primary, DELETE |
| 400 | FluentValidation failure |
| 404 | Zone/accommodation/item not found, or ownership mismatch |
| 422 | Business rule violation (max items, invalid type) |

---

## Dependencies

No new NuGet packages required. All dependencies already exist in the project.

Migration command:
```bash
dotnet ef migrations add AddAccommodationMediaFields --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

---

## Key Notes

- **Do NOT delete blobs** — the DELETE endpoints remove only the DB record. Blob lifecycle is managed independently via the blob storage service.
- **File URL validation:** validate `FileUrl` starts with `https://` in FluentValidation. This prevents SSRF via stored malicious URLs.
- **`IsApproved = true` / `IsPublished = true`** — when creating accommodation/zone media, set both flags to `true` immediately (admin-uploaded content doesn't go through the approval workflow used by memories).
- **`IsPrimary` is scoped** — "primary" applies within one owner scope (one primary per zone, one primary per accommodation, one primary per accommodation type). The `PATCH /primary` endpoint bulk-clears the scope then sets the one item.
- **`SetNull` on delete** — MediaItems linked to a zone/accommodation are not deleted when the zone/accommodation is deleted; they become orphaned (`ZoneId`/`AccommodationId` set to null). This is intentional — it matches the existing Zone config and avoids accidental blob loss. If stricter cascade is needed, change to `Cascade` in the configuration.
- **AccommodationType enum:** values are `Lodge`, `Caravan`, `Tent`, `Bungalow`, `Motorhome`. The route parameter `{type}` is a string — parse with `Enum.TryParse<AccommodationType>` in the service. Return `422` for unrecognized values.
- **Spanish messages:** all user-facing exception messages must be in Spanish. Log messages stay in English.
- **TDD:** write failing tests (Step 17) in parallel with or before the service methods (Step 7, Step 13).

---

## Next Steps After Implementation

- Frontend ticket: `feat-accommodation-media_frontend.md` — add `AccommodationMediaGallery.vue`, `AccommodationMediaManager.vue`, update `AccommodationSlotCard.vue` and `AccommodationAssignmentPanel.vue`
- Verify the migration applies cleanly on the staging database before merging

---

## Implementation Verification

- [ ] **Code quality:** `dotnet build` with zero warnings; nullable reference types enabled; no `var` where type is not obvious
- [ ] **Functionality:** all listed endpoints return correct HTTP status codes per the testing checklist
- [ ] **Testing:** ≥ 90% coverage with xUnit + FluentAssertions + NSubstitute; AAA pattern in all tests; no `Thread.Sleep`
- [ ] **Integration:** migration applied successfully; `dotnet ef migrations list` shows new migration as applied
- [ ] **Documentation:** data model and API spec files updated
- [ ] **Language:** all `BusinessRuleException` and FluentValidation `.WithMessage()` calls use Spanish

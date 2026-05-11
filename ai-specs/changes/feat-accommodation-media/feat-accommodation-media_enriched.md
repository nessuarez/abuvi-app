# feat: Accommodation Media (Zones, Accommodations & Type Defaults)

## Summary

Allow admins to attach photos and multimedia to **accommodation zones**, **individual accommodations**, and **accommodation type defaults**. These media items must be visible in the room-assignment board ("encaje de bolillos") so administrators have a visual reference when assigning families.

---

## Context

The following entities already exist and are in scope:

| Entity | Table | Key File |
|---|---|---|
| `AccommodationZone` | `accommodation_zones` | `Features/Camps/CampsModels.cs` |
| `CampEditionAccommodation` | `camp_edition_accommodations` | `Features/Camps/CampsModels.cs` |
| `AccommodationType` (enum) | — | `Features/Camps/CampsModels.cs` |

The blob upload infrastructure is already in place:

- **Endpoint:** `POST /api/blobs/upload` (multipart/form-data)
- **Allowed folder:** `accommodation-media` ✅ (already whitelisted)
- **Storage:** Hetzner S3-compatible (`abuvi-media` bucket), public ACL
- **Limits:** 50 MB, images `.jpg/.jpeg/.png/.webp/.gif`, videos `.mp4/.mov/.avi/.webm`
- **Thumbnails:** auto-generated (400×400 WebP) when `generateThumbnail=true`

Reference pattern: `CampPhoto` entity — follow the same shape.

---

## Data Model

### New Entity: `AccommodationMediaItem`

Single table with nullable FKs (exactly one must be non-null per row, enforced by a check constraint in the migration).

```csharp
public class AccommodationMediaItem
{
    public Guid Id { get; set; }

    // Exactly one of these three must be set
    public Guid? ZoneId { get; set; }
    public AccommodationZone? Zone { get; set; }

    public Guid? AccommodationId { get; set; }
    public CampEditionAccommodation? Accommodation { get; set; }

    public AccommodationType? DefaultForType { get; set; }

    public string FileUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Caption { get; set; }     // max 200 chars
    public int DisplayOrder { get; set; }    // >= 0, default 0
    public bool IsPrimary { get; set; }      // only one primary per owner
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**EF Core configuration** (`Data/Configurations/AccommodationMediaItemConfiguration.cs`):

- Table: `accommodation_media_items`
- PK: `Id` with `gen_random_uuid()`
- FK `ZoneId` → `accommodation_zones.Id` ON DELETE CASCADE
- FK `AccommodationId` → `camp_edition_accommodations.Id` ON DELETE CASCADE
- `DefaultForType` stored as `varchar(20)` (enum → string conversion)
- `Caption` max length 200
- `FileUrl` required, max length 500
- `ThumbnailUrl` optional, max length 500
- Check constraint: exactly one of (`ZoneId`, `AccommodationId`, `DefaultForType`) is NOT NULL
- `IsPrimary` index: partial unique index `WHERE is_primary = true` per owner scope (enforced in service logic, not DB constraint)

**Navigation properties to add** on existing entities:

- `AccommodationZone.MediaItems` → `ICollection<AccommodationMediaItem>`
- `CampEditionAccommodation.MediaItems` → `ICollection<AccommodationMediaItem>`

**Migration name:** `AddAccommodationMediaItems`

---

## API Endpoints

All endpoints require `Admin` or `Board` role for write operations. GET endpoints are accessible to any authenticated user.

### Zone Media

```
GET    /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media
POST   /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media
DELETE /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media/{mediaId}
PATCH  /api/camps/editions/{editionId}/accommodation-zones/{zoneId}/media/{mediaId}/primary
```

### Accommodation Media

```
GET    /api/camps/editions/{editionId}/accommodations/{accommodationId}/media
POST   /api/camps/editions/{editionId}/accommodations/{accommodationId}/media
DELETE /api/camps/editions/{editionId}/accommodations/{accommodationId}/media/{mediaId}
PATCH  /api/camps/editions/{editionId}/accommodations/{accommodationId}/media/{mediaId}/primary
```

### Accommodation Type Default Media

```
GET    /api/accommodation-types/media
GET    /api/accommodation-types/{type}/media
POST   /api/accommodation-types/{type}/media
DELETE /api/accommodation-types/media/{mediaId}
PATCH  /api/accommodation-types/media/{mediaId}/primary
```

### Request / Response DTOs

```csharp
// POST body — the file has already been uploaded via /api/blobs/upload
public record AddAccommodationMediaRequest(
    string FileUrl,
    string? ThumbnailUrl,
    string? Caption,
    int DisplayOrder = 0);

public record AccommodationMediaItemResponse(
    Guid Id,
    string FileUrl,
    string? ThumbnailUrl,
    string? Caption,
    int DisplayOrder,
    bool IsPrimary,
    DateTime CreatedAt);
```

**POST flow** (two-step, matching existing pattern):

1. Frontend calls `POST /api/blobs/upload` with `folder=accommodation-media` and `contextId={ownerId}` → gets back `FileUrl` + `ThumbnailUrl`.
2. Frontend calls the appropriate media endpoint with the returned URLs.

**PATCH /primary** sets `IsPrimary = true` for the given item and `IsPrimary = false` for all others in the same owner scope (zone, accommodation, or type).

**DELETE** removes the DB record. Does **not** delete the blob (blobs are managed independently).

**Business rules:**

- Max **10 media items per owner** (zone, accommodation, or type). Return `422` if exceeded.
- Validate that `ZoneId`/`AccommodationId` belong to the given `editionId` (404 if not).
- `AccommodationType` enum values: `Lodge`, `Caravan`, `Tent`, `Bungalow`, `Motorhome`.

---

## Files to Create / Modify

### Backend

| Action | File |
|---|---|
| Create | `src/Abuvi.API/Data/Configurations/AccommodationMediaItemConfiguration.cs` |
| Create | `src/Abuvi.API/Migrations/<timestamp>_AddAccommodationMediaItems.cs` |
| Modify | `src/Abuvi.API/Data/AbuviDbContext.cs` — add `DbSet<AccommodationMediaItem>` |
| Modify | `src/Abuvi.API/Features/Camps/CampsModels.cs` — add entity, DTOs, add nav props to existing entities |
| Create | `src/Abuvi.API/Features/Camps/AccommodationMediaRepository.cs` + interface |
| Create | `src/Abuvi.API/Features/Camps/AccommodationMediaService.cs` |
| Create | `src/Abuvi.API/Features/Camps/AccommodationMediaEndpoints.cs` |
| Modify | `src/Abuvi.API/Program.cs` — register new services and map endpoints |

### Tests

| Action | File |
|---|---|
| Create | `src/Abuvi.Tests/Unit/Features/Camps/AccommodationMediaServiceTests.cs` |

Test cases required:

- `AddToZone_WhenZoneBelongsToEdition_ReturnsCreated`
- `AddToZone_WhenZoneNotInEdition_ThrowsNotFoundException`
- `AddToZone_WhenExceedsMaxItems_ThrowsBusinessRuleException`
- `SetPrimary_WhenItemExists_UpdatesAllItemsInScope`
- `SetPrimary_WhenItemNotFound_ThrowsNotFoundException`
- `DeleteMedia_WhenExists_RemovesRecord`
- `DeleteMedia_WhenNotFound_ThrowsNotFoundException`
- Same set for Accommodation scope
- `AddTypeDefault_WhenTypeIsValid_ReturnsCreated`
- `AddTypeDefault_WhenTypeIsInvalid_ThrowsValidationException`

### Frontend

| Action | File |
|---|---|
| Create | `frontend/src/types/accommodation-media.ts` |
| Create | `frontend/src/composables/useAccommodationMedia.ts` |
| Create | `frontend/src/components/camps/AccommodationMediaGallery.vue` — reusable display component |
| Create | `frontend/src/components/camps/AccommodationMediaManager.vue` — admin upload/delete UI |
| Modify | `frontend/src/components/camps/AccommodationZonePanel.vue` — embed `AccommodationMediaGallery` |
| Modify | `frontend/src/components/camps/CampEditionAccommodationDialog.vue` — embed `AccommodationMediaManager` (admin) + `AccommodationMediaGallery` (view) |
| Modify | `frontend/src/components/camps/AccommodationSlotCard.vue` — show primary thumbnail |
| Modify | `frontend/src/components/camps/AccommodationAssignmentPanel.vue` — show zone/accommodation thumbnails |
| Modify | `frontend/src/types/accommodation-assignment.ts` — add `primaryThumbnailUrl?: string` to zone/accommodation response types |

---

## Frontend Component Specs

### `AccommodationMediaGallery.vue`

- Props: `mediaItems: AccommodationMediaItem[]`, `readonly?: boolean` (default `true`)
- Shows a horizontal scrollable strip of thumbnails (or full images if no thumbnail)
- Primary item displayed first / highlighted
- If `readonly = false`, shows delete (×) button per item + upload button
- Uses PrimeVue `Image` component for lightbox preview on click

### `AccommodationMediaManager.vue`

- Props: `ownerType: 'zone' | 'accommodation' | 'type'`, `ownerId: string`, `editionId?: string`
- Internally uses `useAccommodationMedia` composable
- Upload flow: calls `POST /api/blobs/upload` first (using existing blob uploader), then `POST` to the media endpoint
- Shows count badge `(n/10)`

### `AccommodationSlotCard.vue` changes

- Add `primaryThumbnailUrl?: string` to the accommodation data it receives
- Display a small `32×32` thumbnail in the top-right corner of the card if available
- Fallback to type-based icon if no media

### `AccommodationAssignmentPanel.vue` changes

- When loading accommodations, include `primaryThumbnailUrl` in the response
- Zone headers in the panel may show the zone's primary thumbnail

---

## API Response Changes

Update existing accommodation/zone list responses to include primary media:

```csharp
// Extend existing response records:
public record AssignmentAccommodationResponse(
    Guid Id,
    string Name,
    // ... existing fields ...
    string? PrimaryThumbnailUrl,   // ADD
    string? PrimaryFileUrl);       // ADD

public record AccommodationZoneResponse(
    Guid Id,
    string Name,
    // ... existing fields ...
    string? PrimaryThumbnailUrl,   // ADD
    string? PrimaryFileUrl);       // ADD
```

In the repository queries that build these responses, use a `.Select()` projection with a subquery or `.Include(z => z.MediaItems.Where(m => m.IsPrimary).Take(1))`.

---

## Acceptance Criteria

- [ ] Admin can upload photos/videos to a zone via the zone management panel
- [ ] Admin can upload photos/videos to an individual accommodation via its edit dialog
- [ ] Admin can set default media for each accommodation type (Lodge, Caravan, Tent, Bungalow, Motorhome) in the accommodation features catalogue section
- [ ] Maximum 10 media items per owner enforced (returns `422` with Spanish error message)
- [ ] Setting a primary media item unsets all others for that owner
- [ ] Deleting a media item removes the DB record (blob is NOT deleted)
- [ ] Primary thumbnail is visible on each accommodation slot in the assignment board
- [ ] Zone headers in the assignment board show the zone's primary thumbnail
- [ ] Media items are loaded alongside accommodation/zone data to avoid extra round-trips in the assignment board
- [ ] All new GET endpoints return data for any authenticated user; mutating endpoints require `Admin` or `Board` role
- [ ] Unit tests cover all service methods with ≥ 90% branch coverage
- [ ] All user-facing text (validation messages, toast notifications) is in Spanish

---

## Non-Functional Requirements

- **Performance:** Zone/accommodation list queries for the assignment board must use projection (`.Select()`) to load only `PrimaryThumbnailUrl` — do not eagerly load all media items in list views.
- **Security:** File URLs come from the trusted blob service — validate they start with the configured storage base URL before persisting (prevent open redirect / SSRF via stored URLs).
- **Storage:** Use `folder=accommodation-media` and `contextId={ownerId}` when calling the blob upload endpoint, so files are organized by owner in the bucket.
- **GDPR:** No personal data in media captions. Captions are internal admin notes only.

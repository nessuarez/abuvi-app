# Follow-up Ticket: Historical Archive — Memories & Multimedia Gallery

**Depends on:** `feat/blob-storage` (merged)
**Suggested branch:** `feat/media-memories-archive`
**Priority:** Medium — core to the association's digital heritage section

---

## Context

The data model defines two archive entities:

- **`Memory`** — a written story or anecdote authored by a member, optionally linked to a `CampLocation`. Requires board/admin approval before publication.
- **`MediaItem`** — a multimedia file (photo, video, audio, interview, document) that can be attached to a `Memory` or to a `CampLocation`. Also requires approval.

Both entities already have `fileUrl`/`thumbnailUrl` fields pointing to blob storage, but there are no endpoints, no upload UI, and no approval workflow implemented yet.

The interactive map of historical camp locations (`CampLocation`) is also part of this section, with a `coverPhotoUrl` field.

---

## Scope

### What members can do

- Submit a written `Memory` (title, content, year, optional `CampLocation` link).
- Attach `MediaItem` files (photos, videos, audio, documents) to their memories.
- View approved + published memories and media in the historical gallery.

### What Admin/Board can do

- Approve/reject submitted `Memory` records.
- Approve/reject submitted `MediaItem` records.
- Create and manage `CampLocation` records (with cover photo).
- Upload `MediaItem` records directly (without a linked `Memory`), e.g. digitized archive photos.

---

## Data Model

### `MediaItem.type` enum — add `Audio`

The existing enum is `Photo | Video | Interview | Document`. Audio content (oral histories, recorded interviews) is a natural fit for the historical archive. Add `Audio` to the enum.

> **Note:** This same change is also required by `feat-media-50-aniversary.md`. Whichever ticket is implemented first must include the migration.

### `CampLocation.coverPhotoUrl`

Already in the model. This ticket implements the upload flow for that field via `POST /api/blobs/upload` (folder: `camp-locations`).

---

## Backend

### Memories (`Features/Memories/`)

| Endpoint | Role | Description |
| --- | --- | --- |
| `GET /api/memories` | Any authenticated | List approved + published memories (paginated, filterable by year, campLocationId) |
| `GET /api/memories/{id}` | Any authenticated | Get a single published memory with its media items |
| `POST /api/memories` | Member | Submit a new memory (starts unapproved/unpublished) |
| `PUT /api/memories/{id}` | Author or Admin | Update own unpublished memory |
| `PATCH /api/memories/{id}/approve` | Admin, Board | Set `isApproved=true`, `isPublished=true` |
| `PATCH /api/memories/{id}/reject` | Admin, Board | Set `isApproved=false`, `isPublished=false` |
| `DELETE /api/memories/{id}` | Admin | Hard delete (rare; memories are meant to be permanent) |

### Media Items (`Features/MediaItems/`)

| Endpoint | Role | Description |
| --- | --- | --- |
| `GET /api/media-items` | Any authenticated | List approved + published items (paginated, filterable by type, year, decade, campLocationId, memoryId) |
| `POST /api/media-items` | Member, Admin, Board | Upload a media item (calls `IBlobStorageService`; `generateThumbnail=true` for images) |
| `PATCH /api/media-items/{id}/approve` | Admin, Board | Approve and publish |
| `PATCH /api/media-items/{id}/reject` | Admin, Board | Reject |
| `DELETE /api/media-items/{id}` | Admin | Delete record + blob from storage |

### Camp Locations (`Features/CampLocations/`)

| Endpoint | Role | Description |
| --- | --- | --- |
| `GET /api/camp-locations` | Any authenticated | List all camp locations (for the interactive map) |
| `POST /api/camp-locations` | Admin, Board | Create a location (with optional cover photo upload via blob storage) |
| `PUT /api/camp-locations/{id}` | Admin, Board | Update location data or cover photo |
| `DELETE /api/camp-locations/{id}` | Admin | Delete (cascades cover photo blob) |

---

## Frontend

### Historical archive page (`/archive`)

- **Map view**: interactive map (Leaflet or Google Maps) showing `CampLocation` pins. Click → popup with name, year, description, cover photo.
- **Gallery view**: filterable grid of approved `MediaItem` records by type (photo, video, audio, document) and decade.
- **Stories view**: list of approved `Memory` entries (paginated cards with title, year, excerpt, author).
- Tab or sidebar navigation between the three views.

### Memory submission form

- Rich text editor for `content` (use existing pattern or `Quill`/`TipTap` if already in use).
- Year picker (1975–current year).
- `CampLocation` dropdown (optional).
- Attach media files: photo, video, audio, document (delegates to `IBlobStorageService`).
- Submit → success toast with pending-review notice.

### Admin review panel (`/admin/archive`)

- Tabbed view: pending memories | pending media items.
- Each item shows content preview + approve/reject buttons.
- Bulk approval not required in v1.

---

## Bucket Key Schema

```
abuvi-media/
├── media-items/{guid}.{ext}           # MediaItem.fileUrl
├── media-items/thumbs/{guid}.webp     # MediaItem.thumbnailUrl (images only)
└── camp-locations/{locationId}/{guid}.{ext}  # CampLocation.coverPhotoUrl
```

All already defined in the blob storage spec.

---

## Performance Considerations

- All gallery and memory list endpoints must be **paginated**.
- Add a database index on `MediaItem.decade` and `MediaItem.year` for efficient filtering.
- Add a database index on `Memory.year` and `Memory.campLocationId`.
- `MediaItem` thumbnail is served from blob storage — never resize on-the-fly.

---

## Testing

| Area | Tests |
| --- | --- |
| `MemoriesService` unit | Submit creates memory with `isApproved=false`; approve sets both flags |
| `MediaItemsService` unit | Image upload generates thumbnail; audio upload sets `thumbnailUrl=null`; delete removes blob |
| `MemoriesEndpoints` integration | POST unauthenticated → 401; PATCH approve as Member → 403 |
| Memory form component | Submit calls API; shows toast; disables submit during upload |
| Gallery component | Renders by type filter; shows audio player for audio items |
| Map component | Renders pins for all locations; popup shows cover photo |

---

## Acceptance Criteria

- [ ] Members can submit written memories and attach media files.
- [ ] Submissions are stored as unapproved records (not visible to others until approved).
- [ ] Admin/Board can approve or reject from the review panel.
- [ ] The gallery shows only approved + published items, filterable by type and decade.
- [ ] The interactive map renders all `CampLocation` pins with cover photos.
- [ ] Audio `MediaItem` records render with an inline HTML5 audio player.
- [ ] Deleting a `MediaItem` or `CampLocation` also removes its blobs from storage.
- [ ] `MediaItem.type` enum includes `Audio`.
- [ ] All list endpoints are paginated.
- [ ] Unit and integration tests pass at ≥ 90% coverage.

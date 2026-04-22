# Follow-up Ticket: Camp Photo Galleries

**Depends on:** `feat/blob-storage` (merged)
**Suggested branch:** `feat/media-camps`
**Priority:** Medium — galleries enrich the camp experience pages but are not required for registration

---

## Context

The data model already defines `PhotoAlbum` (per camp) and `Photo` (individual photo within an album). The `Photo` entity has `fileUrl` and `thumbnailUrl` pointing to blob storage, but there is currently no UI or backend endpoint to upload photos, create albums, or display the gallery.

Camp pages currently show Google Places photos (`CampPhoto`, `isOriginal = true`). This ticket adds the ability for Admin/Board users to create albums and upload camp photos, and for all authenticated users to browse those albums.

---

## Scope

### What Admin/Board can do

- Create a `PhotoAlbum` linked to a specific camp.
- Upload multiple photos to an album (batch upload).
- Set a cover photo for the album (`PhotoAlbum.coverPhotoId`).
- Reorder photos within an album (`Photo.sortOrder`).
- Delete individual photos.

### What authenticated members can do

- View all published photo albums for a camp.
- Browse photos within an album in a lightbox/gallery view.

---

## Data Model

No new entities required. The following fields are already present:

| Entity | Relevant fields |
| --- | --- |
| `PhotoAlbum` | `campId`, `title`, `description`, `coverPhotoId` |
| `Photo` | `photoAlbumId`, `uploadedByUserId`, `fileUrl`, `thumbnailUrl`, `caption`, `sortOrder` |

---

## Backend

### New endpoints (in `Features/PhotoAlbums/` and `Features/Photos/`)

| Endpoint | Role | Description |
| --- | --- | --- |
| `GET /api/camps/{campId}/albums` | Any authenticated | List all albums for a camp (with cover photo) |
| `POST /api/camps/{campId}/albums` | Admin, Board | Create a new album |
| `PUT /api/camps/{campId}/albums/{albumId}` | Admin, Board | Update album metadata (title, description, coverPhotoId) |
| `DELETE /api/camps/{campId}/albums/{albumId}` | Admin | Delete album and all its photos (cascade delete blobs too) |
| `GET /api/albums/{albumId}/photos` | Any authenticated | List photos in an album (paginated) |
| `POST /api/albums/{albumId}/photos` | Admin, Board | Upload a photo (calls `IBlobStorageService` with `generateThumbnail=true`) |
| `PATCH /api/albums/{albumId}/photos/{photoId}` | Admin, Board | Update caption or sortOrder |
| `DELETE /api/albums/{albumId}/photos/{photoId}` | Admin, Board | Delete a photo + its blob from storage |

### Cascade blob deletion

When a `Photo` is deleted (or a `PhotoAlbum` is deleted), the backend must also call `IBlobStorageService.DeleteManyAsync()` for `fileUrl` and `thumbnailUrl` blobs. Extract the blob key from the stored URL using `PublicBaseUrl` prefix stripping.

### Thumbnail generation

All photo uploads use `generateThumbnail = true`. Thumbnails are stored at `photos/{albumId}/thumbs/{guid}.webp`.

---

## Frontend

### Camp detail page

- Add a **"Galería"** tab or section to the camp detail page.
- Display all albums as cards (cover photo + title + photo count).
- Clicking an album opens the album view.

### Album view

- Responsive photo grid (masonry or uniform grid).
- PrimeVue `Galleria` component for lightbox preview.
- Show caption and uploader below each photo.

### Admin: Album management

- Add an **"Álbumes"** section to the admin camp management panel.
- Create/edit album form (title, description).
- Batch photo upload with drag-and-drop (`PrimeVue FileUpload` in advanced mode).
- Drag-to-reorder photos (`sortOrder` update).
- Cover photo picker.

---

## Bucket Key Schema

```
abuvi-media/
└── photos/{photoAlbumId}/{guid}.{ext}          # Photo.fileUrl
└── photos/{photoAlbumId}/thumbs/{guid}.webp    # Photo.thumbnailUrl
```

Already defined in the blob storage spec. No new folders needed.

---

## Performance Considerations

- Photo list endpoints must be **paginated** (`PagedResult<T>`, default page size 20).
- Use `.AsNoTracking()` on all read queries.
- Thumbnails must be generated server-side at upload time — never load full-size images for grid views.
- Consider a configurable max photos-per-album limit (e.g., 500) to prevent abuse.

---

## Testing

| Area | Tests |
| --- | --- |
| `PhotosService` unit | Upload creates Photo with correct `fileUrl`/`thumbnailUrl`; delete removes blob |
| `PhotoAlbumsEndpoints` integration | POST album as Member → 403; POST as Board → 201; DELETE as Admin → 204 |
| `PhotosEndpoints` integration | POST photo → blob upload called with `generateThumbnail=true`; DELETE → blob deleted |
| Album view component | Renders photo grid; lightbox opens on click |

---

## Acceptance Criteria

- [ ] Admin/Board can create albums and upload photos to them.
- [ ] Thumbnails are auto-generated on upload and served in the grid view.
- [ ] Full-size photos are accessible via the lightbox only.
- [ ] Deleting a photo also removes its blobs from Hetzner Object Storage.
- [ ] Photo list endpoints are paginated.
- [ ] The camp detail page shows a gallery tab with albums.
- [ ] All backend endpoints follow Vertical Slice Architecture.
- [ ] Unit and integration tests pass at ≥ 90% coverage.

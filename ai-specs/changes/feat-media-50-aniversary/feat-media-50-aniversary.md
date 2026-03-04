# Follow-up Ticket: Real Media Uploads for the 50th Anniversary Page

**Depends on:** `feat/blob-storage` (merged)
**Suggested branch:** `feat/media-50-aniversary`
**Priority:** High — the 50th anniversary is in 2026 and the upload form is currently mocked

---

## Context

The `/anniversary` page (`AnniversaryPage.vue`) is currently a fully static mock. The upload form (`AnniversaryUploadForm.vue`) and the gallery (`AnniversaryGallery.vue`) use placeholder data. The submit button is intentionally disabled pending backend readiness.

This ticket replaces the mock with real file uploads, persistence, and display — backed by the `Memory` and `MediaItem` entities already in the data model.

**Current mock spec:** `ai-specs/changes/merged/feat-mock-50-aniversary_enriched.md`

---

## Scope

### What members can do

- Upload **photos, videos, audio recordings, and written memories** linked to the 50th anniversary.
- Each submission maps to one `Memory` (written story) and/or one or more `MediaItem` records.
- Submissions start as **unapproved** (`isApproved = false`, `isPublished = false`) and require board/admin review.

### What admins/board can do

- Review submitted memories and media items in the admin panel.
- Approve and publish them (`isApproved = true`, `isPublished = true`), making them visible in the gallery.
- Reject (set `isApproved = false`, leave `isPublished = false`).

---

## Data Model Changes

### `MediaItem.type` enum — add `Audio`

The existing enum is `Photo | Video | Interview | Document`. The 50th anniversary upload form already exposes "Audio" as a content type option. Add `Audio` to the enum.

**Migration required:** Add migration to update the `type` CHECK constraint in PostgreSQL.

### No new tables required

`Memory` and `MediaItem` already exist in the schema. The anniversary page content is stored there with no special tagging (the year `2026` in `Memory.year` / `MediaItem.year` can distinguish anniversary content).

Optionally, add a `tag` or `context` field to `MediaItem` to filter by context (e.g., `anniversary-50`) — defer to the implementing developer.

---

## Backend

### New endpoints (in `Features/Memories/` and `Features/MediaItems/`)

| Endpoint | Role | Description |
| --- | --- | --- |
| `POST /api/memories` | Member | Create a written memory (calls `IBlobStorageService` if attaching a file) |
| `POST /api/media-items` | Member | Upload a photo, video, audio, or document |
| `GET /api/media-items?year=2026&approved=true` | Public (authenticated) | List approved + published media items for the gallery |
| `PATCH /api/media-items/{id}/approve` | Admin, Board | Approve and publish a media item |
| `PATCH /api/media-items/{id}/reject` | Admin, Board | Reject a media item |

### File upload flow

1. Frontend calls `POST /api/blobs/upload` (folder: `media-items`) → receives `fileUrl` + optional `thumbnailUrl`.
2. Frontend calls `POST /api/media-items` with the URLs, type, title, description, year.
3. Backend saves the `MediaItem` with `isApproved = false`, `isPublished = false`.

---

## Frontend

### Changes to `AnniversaryUploadForm.vue`

- Enable the submit button.
- On submit: call the real `POST /api/blobs/upload` (multipart), then `POST /api/media-items`.
- Show a progress indicator during upload.
- Show success toast: *"¡Tu recuerdo ha sido enviado! Lo revisaremos pronto."*
- Show error toast on failure.

### Changes to `AnniversaryGallery.vue`

- Replace placeholder images with real data from `GET /api/media-items?year=2026&approved=true`.
- Render photos in the responsive grid.
- Support audio player inline (HTML5 `<audio>` element with controls).
- Support video playback (HTML5 `<video>` or PrimeVue equivalent).
- Maintain lazy loading for performance.

### New admin view

- Add a `MediaItemsReviewPage.vue` under the admin section.
- List unapproved items (`GET /api/media-items?approved=false`) with approve/reject buttons.

---

## Bucket Key Schema (new folder)

```
abuvi-media/
└── media-items/{guid}.{ext}        # already defined in blob-storage spec
```

No new bucket folders needed.

---

## Testing

| Area | Tests |
| --- | --- |
| `MediaItemsService` unit | Upload creates record with `isApproved=false`; approve sets both flags |
| `MediaItemsEndpoints` integration | POST unauthenticated → 401; POST valid → 201; PATCH approve as Member → 403 |
| `AnniversaryUploadForm` component | Submit calls upload API then media-items API; shows toast on success/error |
| `AnniversaryGallery` component | Renders items from API; shows audio player for audio type |

---

## Acceptance Criteria

- [ ] Members can upload photos, videos, audio files, and documents from the anniversary page.
- [ ] Submissions are saved as unapproved `MediaItem` records.
- [ ] The gallery shows only approved + published items.
- [ ] Admins/board can approve or reject items from the admin panel.
- [ ] Audio items render with an inline HTML5 audio player.
- [ ] `MediaItem.type` enum includes `Audio`.
- [ ] All new backend endpoints follow Vertical Slice Architecture.
- [ ] Unit and integration tests pass at ≥ 90% coverage.

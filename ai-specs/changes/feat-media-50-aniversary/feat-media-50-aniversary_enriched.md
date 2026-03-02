# Real Media Uploads for the 50th Anniversary Page — Enriched User Story

**Feature branch:** `feat/media-50-aniversary`
**Depends on:** `feat/blob-storage` (merged)
**Related specs:**

- Original story: `ai-specs/changes/feat-media-50-aniversary/feat-media-50-aniversary.md`
- Mock spec: `ai-specs/changes/merged/feat-mock-50-aniversary_enriched.md`
- Blob storage: `ai-specs/changes/feat-blob-storage/feat-blob-storage_enriched.md`
- Data model: `ai-specs/specs/data-model.md` (Memory, MediaItem entities)
**Date enriched:** 2026-03-02
**Priority:** High — the 50th anniversary is in 2026 and the upload form is currently mocked

---

## Summary

Replace the static mock on the `/anniversary` page with real file uploads, persistence, and display. Members can upload photos, videos, audio, and written memories. All submissions require admin/board approval before appearing in the public gallery. This ticket creates the `Memory` and `MediaItem` entities (which do not yet exist in the codebase), their API endpoints, and integrates the existing frontend components with the backend.

---

## Context & Current State

The `/anniversary` page is fully implemented as a **static mock** (merged via `feat-mock-50-aniversary`):

| Component | File | Current behavior |
|---|---|---|
| `AnniversaryPage.vue` | `frontend/src/views/AnniversaryPage.vue` | Orchestrates 5 sections |
| `AnniversaryUploadForm.vue` | `frontend/src/components/anniversary/AnniversaryUploadForm.vue` | Form with disabled submit, no API calls |
| `AnniversaryGallery.vue` | `frontend/src/components/anniversary/AnniversaryGallery.vue` | 9 hardcoded placeholder items |
| `AnniversaryHero.vue` | `frontend/src/components/anniversary/AnniversaryHero.vue` | Static hero banner |
| `AnniversaryContactForm.vue` | `frontend/src/components/anniversary/AnniversaryContactForm.vue` | Mock contact form |

The `Memory` and `MediaItem` entities are defined in the data model spec but **do not exist in code yet** — no entity classes, no EF configurations, no DbSets, no migrations.

The `BlobStorage` infrastructure (`IBlobStorageService`, `POST /api/blobs/upload`) is fully implemented and available.

---

## Data Model Changes

### 1. New Entity: `Memory`

Per `data-model.md`. Create in `Features/Memories/MemoriesModels.cs`:

```csharp
public class Memory
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Title { get; set; } = string.Empty;       // max 200
    public string Content { get; set; } = string.Empty;     // rich text
    public int? Year { get; set; }                          // optional, 1975–2026
    public Guid? CampLocationId { get; set; }               // optional FK
    public bool IsPublished { get; set; } = false;
    public bool IsApproved { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User Author { get; set; } = null!;
    public CampLocation? CampLocation { get; set; }
    public ICollection<MediaItem> MediaItems { get; set; } = [];
}
```

### 2. New Entity: `MediaItem`

Per `data-model.md`. Create in `Features/MediaItems/MediaItemsModels.cs`:

```csharp
public enum MediaItemType
{
    Photo,
    Video,
    Interview,
    Document,
    Audio       // NEW — required for anniversary uploads
}

public class MediaItem
{
    public Guid Id { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileUrl { get; set; } = string.Empty;     // max 2048
    public string? ThumbnailUrl { get; set; }               // max 2048, required for Photo/Video
    public MediaItemType Type { get; set; }
    public string Title { get; set; } = string.Empty;       // max 200
    public string? Description { get; set; }                 // max 1000
    public int? Year { get; set; }
    public string? Decade { get; set; }                      // max 10, auto-derived from year
    public Guid? MemoryId { get; set; }                      // optional FK
    public Guid? CampLocationId { get; set; }                // optional FK
    public bool IsPublished { get; set; } = false;
    public bool IsApproved { get; set; } = false;
    public string? Context { get; set; }                     // max 50, e.g. "anniversary-50"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User UploadedBy { get; set; } = null!;
    public Memory? Memory { get; set; }
    public CampLocation? CampLocation { get; set; }
}
```

**Note on `Context` field:** Adding an optional `Context` string field (max 50 chars) to `MediaItem` to filter by context (e.g., `"anniversary-50"`). This avoids relying solely on `year = 2026` for filtering and supports future event-based galleries.

### 3. EF Core Configurations

Create `Data/Configurations/MemoryConfiguration.cs` and `Data/Configurations/MediaItemConfiguration.cs` following the existing `CampPhotoConfiguration.cs` pattern.

Key constraints:

- `Memory.Title`: `HasMaxLength(200)`, `IsRequired()`
- `Memory.Year`: optional, check constraint `>= 1975 AND <= 2026`
- `MediaItem.FileUrl`: `HasMaxLength(2048)`, `IsRequired()`
- `MediaItem.ThumbnailUrl`: `HasMaxLength(2048)`
- `MediaItem.Title`: `HasMaxLength(200)`, `IsRequired()`
- `MediaItem.Description`: `HasMaxLength(1000)`
- `MediaItem.Decade`: `HasMaxLength(10)`
- `MediaItem.Context`: `HasMaxLength(50)`
- `MediaItem.Type`: stored as string via `HasConversion<string>()`
- Indexes on `MediaItem.Year`, `MediaItem.Context`, `MediaItem.IsApproved + IsPublished`

### 4. DbContext Changes

Add to `AbuviDbContext.cs`:

```csharp
public DbSet<Memory> Memories { get; set; }
public DbSet<MediaItem> MediaItems { get; set; }
```

### 5. Migration

Create migration: `dotnet ef migrations add AddMemoriesAndMediaItems`

---

## Backend — Vertical Slice Architecture

### Feature Slice: `Features/Memories/`

```
src/Abuvi.API/Features/Memories/
├── MemoriesEndpoints.cs
├── MemoriesModels.cs         # Entity + DTOs
├── MemoriesService.cs
├── IMemoriesService.cs
├── MemoriesValidator.cs
└── MemoriesExtensions.cs     # DI registration
```

### Feature Slice: `Features/MediaItems/`

```
src/Abuvi.API/Features/MediaItems/
├── MediaItemsEndpoints.cs
├── MediaItemsModels.cs       # Entity + DTOs
├── MediaItemsService.cs
├── IMediaItemsService.cs
├── MediaItemsValidator.cs
└── MediaItemsExtensions.cs   # DI registration
```

### API Endpoints

#### Memories

| Method | Route | Roles | Description |
|---|---|---|---|
| `POST` | `/api/memories` | Member, Admin, Board | Create a written memory |
| `GET` | `/api/memories?year={year}&approved={bool}` | Authenticated | List memories with optional filters |
| `GET` | `/api/memories/{id}` | Authenticated | Get single memory |
| `PATCH` | `/api/memories/{id}/approve` | Admin, Board | Set `isApproved = true`, `isPublished = true` |
| `PATCH` | `/api/memories/{id}/reject` | Admin, Board | Set `isApproved = false`, `isPublished = false` |

#### MediaItems

| Method | Route | Roles | Description |
|---|---|---|---|
| `POST` | `/api/media-items` | Member, Admin, Board | Create a media item (file already uploaded via `/api/blobs/upload`) |
| `GET` | `/api/media-items?year={year}&approved={bool}&context={ctx}&type={type}` | Authenticated | List media items with filters |
| `GET` | `/api/media-items/{id}` | Authenticated | Get single media item |
| `PATCH` | `/api/media-items/{id}/approve` | Admin, Board | Approve and publish |
| `PATCH` | `/api/media-items/{id}/reject` | Admin, Board | Reject |
| `DELETE` | `/api/media-items/{id}` | Admin | Delete media item and its blob |

### DTOs

```csharp
// Features/Memories/MemoriesModels.cs (DTOs section)

public record CreateMemoryRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int? Year { get; init; }
    public Guid? CampLocationId { get; init; }
}

public record MemoryResponse(
    Guid Id,
    Guid AuthorUserId,
    string AuthorName,
    string Title,
    string Content,
    int? Year,
    Guid? CampLocationId,
    bool IsPublished,
    bool IsApproved,
    DateTime CreatedAt,
    List<MediaItemResponse> MediaItems);

// Features/MediaItems/MediaItemsModels.cs (DTOs section)

public record CreateMediaItemRequest
{
    public string FileUrl { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public MediaItemType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? Year { get; init; }
    public Guid? MemoryId { get; init; }
    public Guid? CampLocationId { get; init; }
    public string? Context { get; init; }
}

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
    string? Context,
    bool IsPublished,
    bool IsApproved,
    DateTime CreatedAt);
```

### Validation Rules

**CreateMemoryRequestValidator:**

- `Title`: NotEmpty, MaxLength(200)
- `Content`: NotEmpty
- `Year`: When provided, must be >= 1975 and <= 2026

**CreateMediaItemRequestValidator:**

- `FileUrl`: NotEmpty, MaxLength(2048), must start with configured `PublicBaseUrl`
- `Type`: Must be a valid `MediaItemType` enum value
- `Title`: NotEmpty, MaxLength(200)
- `Description`: When provided, MaxLength(1000)
- `Year`: When provided, must be >= 1975 and <= 2026
- `Context`: When provided, MaxLength(50)
- `ThumbnailUrl`: Required when `Type` is `Photo` or `Video`

### Business Logic

**Decade auto-derivation:** When `Year` is provided, derive `Decade` automatically:

```csharp
private static string? DerivateDecade(int? year) => year switch
{
    >= 1970 and < 1980 => "70s",
    >= 1980 and < 1990 => "80s",
    >= 1990 and < 2000 => "90s",
    >= 2000 and < 2010 => "00s",
    >= 2010 and < 2020 => "10s",
    >= 2020 and < 2030 => "20s",
    _ => null
};
```

**Approve/Reject:** Only Admin or Board roles. Sets both `isApproved` and `isPublished` on approve. On reject, sets `isApproved = false`, `isPublished = false`.

**Delete MediaItem:** Admin only. Also deletes the associated blob from storage by extracting the key from `fileUrl` and calling `IBlobStorageService.DeleteManyAsync`.

### Registration in `Program.cs`

```csharp
builder.Services.AddMemories();
builder.Services.AddMediaItems();
// ...
app.MapMemoriesEndpoints();
app.MapMediaItemsEndpoints();
```

---

## Frontend

### New TypeScript Types

**File: `frontend/src/types/memory.ts`**

```typescript
export interface Memory {
  id: string
  authorUserId: string
  authorName: string
  title: string
  content: string
  year?: number
  campLocationId?: string
  isPublished: boolean
  isApproved: boolean
  createdAt: string
  mediaItems: MediaItem[]
}

export interface CreateMemoryRequest {
  title: string
  content: string
  year?: number
  campLocationId?: string
}
```

**File: `frontend/src/types/media-item.ts`**

```typescript
export type MediaItemType = 'Photo' | 'Video' | 'Audio' | 'Interview' | 'Document'

export interface MediaItem {
  id: string
  uploadedByUserId: string
  uploadedByName: string
  fileUrl: string
  thumbnailUrl: string | null
  type: MediaItemType
  title: string
  description: string | null
  year: number | null
  decade: string | null
  memoryId: string | null
  context: string | null
  isPublished: boolean
  isApproved: boolean
  createdAt: string
}

export interface CreateMediaItemRequest {
  fileUrl: string
  thumbnailUrl?: string | null
  type: MediaItemType
  title: string
  description?: string
  year?: number
  memoryId?: string
  campLocationId?: string
  context?: string
}
```

### New Composables

**File: `frontend/src/composables/useMediaItems.ts`**

Follow the existing pattern from `useCampPhotos.ts`:

```typescript
// Exposes:
// - mediaItems: Ref<MediaItem[]>
// - loading: Ref<boolean>
// - error: Ref<string | null>
// - fetchMediaItems(params?: { year?: number; approved?: boolean; context?: string; type?: string }): Promise<void>
// - createMediaItem(request: CreateMediaItemRequest): Promise<MediaItem>
// - approveMediaItem(id: string): Promise<void>
// - rejectMediaItem(id: string): Promise<void>
// - deleteMediaItem(id: string): Promise<void>
```

**File: `frontend/src/composables/useMemories.ts`**

```typescript
// Exposes:
// - memories: Ref<Memory[]>
// - loading: Ref<boolean>
// - error: Ref<string | null>
// - fetchMemories(params?: { year?: number; approved?: boolean }): Promise<void>
// - createMemory(request: CreateMemoryRequest): Promise<Memory>
// - approveMemory(id: string): Promise<void>
// - rejectMemory(id: string): Promise<void>
```

### Changes to `AnniversaryUploadForm.vue`

Current state: Form has fields for name, content type, year, description, file upload. Submit button is **disabled**.

Required changes:

1. **Enable the submit button** — remove the disabled state
2. **Map content types** to `MediaItemType` enum values:
   - `'foto'` → `'Photo'`
   - `'video'` → `'Video'`
   - `'audio'` → `'Audio'`
   - `'historia'` → creates a `Memory` (not a `MediaItem`)
3. **Upload flow for photos/videos/audio:**
   - Call `POST /api/blobs/upload` (via `useBlobStorage` composable) with `folder: 'media-items'` and `generateThumbnail: true` for images
   - On blob success, call `POST /api/media-items` (via `useMediaItems` composable) with:
     - `fileUrl` and `thumbnailUrl` from blob response
     - `type` mapped from content type
     - `title`: user's name + " — Recuerdo 50 aniversario" (or user-provided title)
     - `description` from the form
     - `year`: 2026 (or the user-selected year)
     - `context`: `"anniversary-50"`
4. **Upload flow for written stories:**
   - Call `POST /api/memories` (via `useMemories` composable) with:
     - `title`: user's name + " — Historia 50 aniversario"
     - `content`: the description/message from the form
     - `year`: 2026 (or user-selected)
5. **Progress indicator:** Show a `ProgressBar` (PrimeVue) during upload
6. **Success toast:** `"¡Tu recuerdo ha sido enviado! Lo revisaremos pronto."`
7. **Error toast:** Show error message from API response
8. **Reset form** after successful submission

### Changes to `AnniversaryGallery.vue`

Current state: 9 hardcoded placeholder items with `picsum.photos` URLs.

Required changes:

1. **Fetch real data** on mount: `GET /api/media-items?year=2026&approved=true&context=anniversary-50`
2. **Replace hardcoded items** with data from the API
3. **Render by type:**
   - `Photo`: PrimeVue `Image` with preview (current pattern)
   - `Video`: HTML5 `<video>` element with controls, poster from `thumbnailUrl`
   - `Audio`: HTML5 `<audio>` element with controls, styled card with title/description
   - `Document`: Download link card with file icon
4. **Loading state:** Show `Skeleton` components while loading
5. **Empty state:** When no approved items exist, show the current placeholder gallery with a message: `"Aún no hay recuerdos aprobados. ¡Sé el primero en compartir!"`
6. **Lazy loading:** Use `IntersectionObserver` or PrimeVue lazy patterns for performance
7. **Keep responsive grid:** 1 col mobile / 3 col tablet / 4 col desktop

### New Admin View: `MediaItemsReviewPage.vue`

**File: `frontend/src/views/admin/MediaItemsReviewPage.vue`**

| Aspect | Detail |
|---|---|
| Route | `/admin/media-review` (add to router, `requiresAuth: true`, role: Admin or Board) |
| Layout | Inside `AdminLayout` |
| Data | `GET /api/media-items?approved=false` |
| Columns | Thumbnail/preview, Title, Type, Uploader, Year, Context, Submitted date |
| Actions | Approve button, Reject button per item |
| Preview | Clicking an item opens a modal with full preview (image/video/audio player) |
| Empty state | "No hay elementos pendientes de revisión" |

Also add a route entry for this page in `frontend/src/router/index.ts`.

Add a navigation link in the admin sidebar/menu to "Revisión de medios".

---

## File Upload Flow (End to End)

```
User fills form → Submit
  ↓
[If file attached]
  Frontend: POST /api/blobs/upload (multipart, folder: 'media-items')
  ← { fileUrl, thumbnailUrl, fileName, contentType, sizeBytes }
  ↓
  Frontend: POST /api/media-items
    { fileUrl, thumbnailUrl, type, title, description, year: 2026, context: 'anniversary-50' }
  ← 201 Created { id, ... isApproved: false }
  ↓
  Show success toast

[If written story only]
  Frontend: POST /api/memories
    { title, content, year: 2026 }
  ← 201 Created { id, ... isApproved: false }
  ↓
  Show success toast
```

---

## Files to Create

### Backend — New Files

| File | Purpose |
|---|---|
| `src/Abuvi.API/Features/Memories/MemoriesModels.cs` | Entity + DTOs |
| `src/Abuvi.API/Features/Memories/IMemoriesService.cs` | Service interface |
| `src/Abuvi.API/Features/Memories/MemoriesService.cs` | Business logic |
| `src/Abuvi.API/Features/Memories/MemoriesEndpoints.cs` | Minimal API endpoints |
| `src/Abuvi.API/Features/Memories/MemoriesValidator.cs` | FluentValidation rules |
| `src/Abuvi.API/Features/Memories/MemoriesExtensions.cs` | DI registration |
| `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs` | Entity + DTOs + enum |
| `src/Abuvi.API/Features/MediaItems/IMediaItemsService.cs` | Service interface |
| `src/Abuvi.API/Features/MediaItems/MediaItemsService.cs` | Business logic |
| `src/Abuvi.API/Features/MediaItems/MediaItemsEndpoints.cs` | Minimal API endpoints |
| `src/Abuvi.API/Features/MediaItems/MediaItemsValidator.cs` | FluentValidation rules |
| `src/Abuvi.API/Features/MediaItems/MediaItemsExtensions.cs` | DI registration |
| `src/Abuvi.API/Data/Configurations/MemoryConfiguration.cs` | EF Core entity config |
| `src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs` | EF Core entity config |
| `src/Abuvi.API/Data/Migrations/{timestamp}_AddMemoriesAndMediaItems.cs` | EF migration |

### Backend — Modified Files

| File | Change |
|---|---|
| `src/Abuvi.API/Data/AbuviDbContext.cs` | Add `DbSet<Memory>` and `DbSet<MediaItem>` |
| `src/Abuvi.API/Program.cs` | Register services and map endpoints |
| `ai-specs/specs/api-endpoints.md` | Document new endpoints |

### Frontend — New Files

| File | Purpose |
|---|---|
| `frontend/src/types/memory.ts` | TypeScript interfaces for Memory |
| `frontend/src/types/media-item.ts` | TypeScript interfaces for MediaItem |
| `frontend/src/composables/useMediaItems.ts` | API composable for media items |
| `frontend/src/composables/useMemories.ts` | API composable for memories |
| `frontend/src/views/admin/MediaItemsReviewPage.vue` | Admin review page |

### Frontend — Modified Files

| File | Change |
|---|---|
| `frontend/src/components/anniversary/AnniversaryUploadForm.vue` | Enable submit, integrate with API |
| `frontend/src/components/anniversary/AnniversaryGallery.vue` | Replace placeholders with real data |
| `frontend/src/router/index.ts` | Add `/admin/media-review` route |

---

## Testing Requirements

### Backend Unit Tests

**File: `src/Abuvi.Tests/Unit/Features/Memories/MemoriesServiceTests.cs`**

| Test | Expected |
|---|---|
| `CreateAsync_WithValidRequest_CreatesMemoryWithApprovedFalse` | Memory created, `isApproved = false`, `isPublished = false` |
| `CreateAsync_WithYear_SetsYearCorrectly` | Year stored |
| `ApproveAsync_WithValidId_SetsBothFlags` | `isApproved = true`, `isPublished = true` |
| `RejectAsync_WithValidId_SetsBothFlagsFalse` | `isApproved = false`, `isPublished = false` |
| `GetListAsync_WithApprovedTrue_ReturnsOnlyApprovedAndPublished` | Filters correctly |

**File: `src/Abuvi.Tests/Unit/Features/MediaItems/MediaItemsServiceTests.cs`**

| Test | Expected |
|---|---|
| `CreateAsync_WithValidRequest_CreatesItemWithApprovedFalse` | Item created, `isApproved = false` |
| `CreateAsync_WithYear_DerivesDecadeAutomatically` | Year 1985 → Decade "80s" |
| `CreateAsync_WithAudioType_AcceptsNullThumbnail` | No validation error |
| `CreateAsync_WithPhotoType_RequiresThumbnailUrl` | Validation enforced |
| `ApproveAsync_WithValidId_SetsBothFlags` | `isApproved = true`, `isPublished = true` |
| `RejectAsync_WithValidId_SetsBothFlagsFalse` | Both flags false |
| `DeleteAsync_AsAdmin_DeletesItemAndBlob` | Both DB record and blob deleted |
| `GetListAsync_WithContextFilter_ReturnsMatchingItems` | Filters by context |

**File: `src/Abuvi.Tests/Unit/Features/MediaItems/MediaItemsValidatorTests.cs`**

| Test | Expected |
|---|---|
| `Validate_WithEmptyFileUrl_Fails` | Validation error |
| `Validate_WithInvalidType_Fails` | Validation error |
| `Validate_WithPhotoTypeAndNoThumbnail_Fails` | Validation error |
| `Validate_WithAudioTypeAndNoThumbnail_Passes` | Valid |
| `Validate_WithYearOutOfRange_Fails` | Validation error |
| `Validate_WithValidRequest_Passes` | Valid |

### Backend Integration Tests

**File: `src/Abuvi.Tests/Integration/Features/MediaItems/MediaItemsEndpointsTests.cs`**

| Test | Expected |
|---|---|
| `PostMediaItem_Unauthenticated_Returns401` | 401 |
| `PostMediaItem_ValidRequest_Returns201` | 201 with response body |
| `GetMediaItems_WithApprovedFilter_ReturnsFilteredList` | 200 with filtered items |
| `PatchApprove_AsMember_Returns403` | 403 |
| `PatchApprove_AsAdmin_Returns200` | 200, flags updated |
| `PatchReject_AsBoard_Returns200` | 200, flags updated |
| `DeleteMediaItem_AsMember_Returns403` | 403 |
| `DeleteMediaItem_AsAdmin_Returns204` | 204, item deleted |

**File: `src/Abuvi.Tests/Integration/Features/Memories/MemoriesEndpointsTests.cs`**

| Test | Expected |
|---|---|
| `PostMemory_Unauthenticated_Returns401` | 401 |
| `PostMemory_ValidRequest_Returns201` | 201 |
| `PatchApprove_AsMember_Returns403` | 403 |
| `PatchApprove_AsAdmin_Returns200` | 200 |

### Frontend Component Tests

**File: `frontend/src/components/anniversary/__tests__/AnniversaryUploadForm.test.ts`**

Update existing tests and add:

| Test | Description |
|---|---|
| `should call blob upload API then media-items API on photo submission` | Full upload flow |
| `should call memories API on written story submission` | Memory creation flow |
| `should show progress indicator during upload` | Progress bar visible |
| `should show success toast after successful upload` | Toast called |
| `should show error toast on API failure` | Error handling |
| `should reset form after successful submission` | Form cleared |

**File: `frontend/src/components/anniversary/__tests__/AnniversaryGallery.test.ts`**

| Test | Description |
|---|---|
| `should fetch and render approved media items from API` | API called, items rendered |
| `should render audio player for audio type items` | `<audio>` element present |
| `should render video player for video type items` | `<video>` element present |
| `should show empty state when no approved items` | Fallback message shown |
| `should show loading skeleton while fetching` | Skeleton components visible |

**File: `frontend/src/views/admin/__tests__/MediaItemsReviewPage.test.ts`**

| Test | Description |
|---|---|
| `should fetch unapproved items on mount` | API called with `approved=false` |
| `should call approve API on approve button click` | Approve endpoint called |
| `should call reject API on reject button click` | Reject endpoint called |
| `should remove item from list after approve/reject` | UI updated |

### Coverage Threshold

90% branches, functions, lines, and statements for all new code.

---

## Acceptance Criteria

- [ ] `Memory` and `MediaItem` entities exist in the database with correct schema and constraints
- [ ] `MediaItemType` enum includes `Audio` in addition to `Photo`, `Video`, `Interview`, `Document`
- [ ] `MediaItem` has an optional `Context` field for filtering by event context
- [ ] `POST /api/memories` creates a memory with `isApproved = false`, `isPublished = false`
- [ ] `POST /api/media-items` creates a media item with `isApproved = false`, `isPublished = false`
- [ ] `GET /api/media-items?year=2026&approved=true&context=anniversary-50` returns only approved+published items
- [ ] `PATCH /api/media-items/{id}/approve` sets both `isApproved` and `isPublished` to `true` (Admin/Board only)
- [ ] `PATCH /api/media-items/{id}/reject` sets both flags to `false` (Admin/Board only)
- [ ] Members can upload photos, videos, audio files from the anniversary page via the enabled submit button
- [ ] Members can submit written stories (creates a `Memory` record)
- [ ] Upload form shows progress indicator during file upload
- [ ] Upload form shows success/error toast messages
- [ ] Gallery displays only approved+published items from the API
- [ ] Audio items render with an inline HTML5 `<audio>` player
- [ ] Video items render with an HTML5 `<video>` player
- [ ] Admin review page at `/admin/media-review` lists unapproved items with approve/reject buttons
- [ ] All new endpoints follow Vertical Slice Architecture
- [ ] All new backend endpoints documented in `api-endpoints.md`
- [ ] Unit and integration tests pass at >= 90% coverage
- [ ] Decade field is auto-derived from year

---

## Non-Functional Requirements

| Requirement | Target |
|---|---|
| **Max file size** | 50 MB (enforced by blob storage configuration) |
| **Upload performance** | Files streamed directly to blob storage, not buffered in memory |
| **Gallery loading** | Lazy load media items with skeleton placeholders |
| **Security** | All endpoints require JWT authentication; approve/reject restricted to Admin/Board; delete restricted to Admin |
| **Validation** | Server-side validation via FluentValidation; client-side validation before API calls |
| **Accessibility** | `aria-label` on audio/video players, alt text on images, semantic HTML |
| **Observability** | Structured logs for create/approve/reject/delete operations with userId and itemId |
| **GDPR** | No personal data in blob storage; uploader identity stored only in DB |

---

## Out of Scope

- Real email sending from the contact form (remains mock)
- Full historical archive browsing page (separate ticket: `feat-media-memories-archive`)
- Camp photo galleries (separate ticket: `feat-media-camps`)
- Profile photos (separate ticket: `feat-media-profile-photos`)
- Pagination for gallery (can be added in follow-up if item count grows)
- Batch approve/reject in admin panel (can be added in follow-up)

---

## Implementation Order (Suggested)

1. **Backend Phase 1:** Create entities, configurations, migration, DbContext changes
2. **Backend Phase 2:** Implement Memories feature slice (service, endpoints, validators)
3. **Backend Phase 3:** Implement MediaItems feature slice (service, endpoints, validators)
4. **Backend Phase 4:** Unit and integration tests
5. **Frontend Phase 1:** Create types and composables
6. **Frontend Phase 2:** Update `AnniversaryUploadForm.vue` with real API integration
7. **Frontend Phase 3:** Update `AnniversaryGallery.vue` with real data
8. **Frontend Phase 4:** Create `MediaItemsReviewPage.vue` admin view
9. **Frontend Phase 5:** Component tests
10. **Documentation:** Update `api-endpoints.md`

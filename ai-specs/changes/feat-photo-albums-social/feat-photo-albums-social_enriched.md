# Camp Edition Albums, Themes and Collective Memory

## Summary

The 50th-anniversary section already has the skeleton (50 editions with year, venue and coordinates) but no content. This feature turns the community into the engine of the archive: a bulk importer that places historical media into its edition, one album per edition, cross-cutting themes that span years, provenance recording who gave us each item, comments, collaborative dating of undated media, and an "I was at this camp" attendance marker.

Everything is built on the **existing** `MediaItem` / `Memory` / blob-storage stack. No new media pipeline, no new upload flow, no new approval panel.

The feature is split into four tasks:

| # | Task | Branch suffix |
|---|------|---------------|
| 1 | Data model changes | `-backend-model` |
| 2 | API endpoints | `-backend-api` |
| 3 | Bulk media importer (Abuvi.Setup CLI) | `-setup-importer` |
| 4 | Frontend albums, themes and social interactions | `-frontend` |

Tasks 1 → 2 → 4 are strictly sequential. Task 3 depends only on Task 1 and can run in parallel with Task 2.

---

## Two organising axes

This is the central idea of the model. Everything else follows from it.

```
                     THEMES  (cross-cutting, span many years)
                 ┌──────────────┬──────────────┬──────────────┐
                 │ San Abuvino  │ Actuaciones  │  Asambleas   │
   EDITIONS      ├──────────────┼──────────────┼──────────────┤
   ┌──────────┐  │              │              │              │
   │   1998   │──┼──── photo ───┼──── photo ───┼──────────────┤
   ├──────────┤  │              │              │              │
   │   2003   │──┼──── photo ───┼──────────────┼──── photo ───┤
   ├──────────┤  │              │              │              │
   │ unknown  │──┼──── photo ───┼──────────────┼──────────────┤
   └──────────┘  └──────────────┴──────────────┴──────────────┘
    (CampEditionId)              (MediaItemTheme, N:M)
```

**Axis 1 — placement.** `MediaItem.CampEditionId` is nullable. `NULL` means **"we do not know which edition yet"** — always a temporary state, always resolvable by collaborative dating. All ABUVI media belongs to *some* camp; we just may not know which one. There is deliberately **no** "does not belong to a camp" state.

**Axis 2 — themes.** A theme like *San Abuvino* recurs across many years, so themes are a many-to-many tag dimension, not a rival container. A photo is *edition 1998* **and** *San Abuvino* at the same time. An item with no edition can still carry themes — and its themes become a dating clue.

Both axes accept **all** `MediaItemType` values: `Photo`, `Video`, `Audio`, `Interview`, `Document`. `Memory` (written stories) also attaches to an edition.

---

## Decisions taken (previously open)

| # | Question | Decision |
|---|----------|----------|
| 1 | Person tagging | **Comments only in this feature.** Region-based person tags linked to `User`/`FamilyMember` are specified as deferred Phase B (see [Deferred: Phase B](#deferred-phase-b--person-tagging)) and **not built now**. Avoids the RGPD surface until the policy exists. |
| 2 | Moderation | **Comments publish directly** on already-approved media. Any member can report a comment; Admin/Board can soft-delete it. Pre-publication approval stays exactly as today for `MediaItem` and `Memory` uploads only. |
| 3 | Importer | **`Abuvi.Setup` CLI command**, alongside `CampImporter` and the `geocode` command. Not an admin web feature. |
| 4 | Access | **Registered users only.** `/anniversary` and all new routes keep `requiresAuth: true`. No public endpoints. |
| 5 | Unplaced content | **`CampEditionId = NULL` is always temporary**, never a permanent category. Uploading without knowing the edition is a first-class flow, because that is precisely what feeds collaborative dating. Recurring subjects are modelled as **themes** (axis 2), not as a placement state. |

---

## Context — what already exists

### Backend (do not rebuild)

| Piece | Location | State |
|-------|----------|-------|
| `MediaItem` entity + `MediaItemType` enum (`Photo`, `Video`, `Interview`, `Document`, `Audio`) | [MediaItemsModels.cs](src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs) | Complete. Has `Year`, `Decade`, `Context`, `IsApproved`, `IsPublished`, `DisplayOrder`, `IsPrimary` |
| `MediaItem` EF config (table `media_items`) | [MediaItemConfiguration.cs](src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs) | Complete, snake_case columns, indexes on `year`, `context`, `(is_approved, is_published)` |
| `MediaItems` endpoints (`/api/media-items`) | [MediaItemsEndpoints.cs](src/Abuvi.API/Features/MediaItems/MediaItemsEndpoints.cs) | CRUD + `approve` / `reject` (Admin, Board) |
| `Memory` entity + endpoints | [Memories/](src/Abuvi.API/Features/Memories/) | Complete |
| Blob storage + thumbnail generation (ImageSharp) | [BlobStorageService.cs](src/Abuvi.API/Features/BlobStorage/BlobStorageService.cs) | Complete. Allowed extensions for image / video / audio / document already configured in `BlobStorageOptions` |
| `Camp` (venue / *sede*) and `CampEdition` (year) | [CampsModels.cs](src/Abuvi.API/Features/Camps/CampsModels.cs) | 31 venues, 50 editions 1976–2025 loaded |
| `Registration` / `RegistrationMember` | [Registrations/](src/Abuvi.API/Features/Registrations/) | Source of *derived* attendance for recent editions |

### Frontend (do not rebuild)

| Piece | Location |
|-------|----------|
| `AnniversaryPage` (route `/anniversary`, `requiresAuth: true`) | [AnniversaryPage.vue](frontend/src/views/AnniversaryPage.vue) |
| `AnniversaryGallery` (flat grid, filters `approved=true&context=anniversary-50`) | [AnniversaryGallery.vue](frontend/src/components/anniversary/AnniversaryGallery.vue) |
| `AnniversaryUploadForm` (blob upload → `POST /api/media-items`, all media types) | [AnniversaryUploadForm.vue](frontend/src/components/anniversary/AnniversaryUploadForm.vue) |
| `useMediaItems` / `useMemories` composables | [useMediaItems.ts](frontend/src/composables/useMediaItems.ts) |
| Admin approval panel | [MediaItemsReviewPanel.vue](frontend/src/components/admin/MediaItemsReviewPanel.vue) |

### Traps found while surveying the codebase

Four things in the existing code will mislead an implementer. Read these before starting.

1. **`MediaItem.CampLocationId` is dead.** The property exists with a `// TODO: Add FK relationship when CampLocation entity is created` comment. **`CampLocation` was never implemented** — it is documented in [data-model.md](ai-specs/specs/data-model.md) (§ CampLocation) but there is no entity, no `DbSet`, no table FK. The column is provably always `NULL`. Do **not** build on it. The new anchor is `MediaItem.CampEditionId` (Task 1.1). Dropping the dead column is an explicit follow-up, not part of this feature (see [Out of scope](#out-of-scope)). The same dead property exists on `Memory`.

2. **`PhotoAlbum` and `Photo` are documented but not implemented.** [data-model.md](ai-specs/specs/data-model.md) §§ PhotoAlbum / Photo describe a parallel album/photo model. **Do not build them.** An "album" in this feature is a *query* over `MediaItem` filtered by `CampEditionId` — it is not an entity, and it is not photo-only. Building `PhotoAlbum` would fork the media model in two. Task 1 includes a documentation fix marking both as superseded.

3. **"CampLocation" in the frontend means `Camp`.** `CampLocationForm.vue` / `CampLocationsPage.vue` edit `Camp` records (venues). Unrelated to the dead `CampLocationId` above.

4. **`PagedResult<T>` does not exist.** [backend-standards.mdc](ai-specs/specs/backend-standards.mdc) § Pagination documents a shared `PagedResult<T>` in `Common/Models/`, but `Common/Models/` contains only `ApiResponse.cs`. The codebase precedent is a feature-local paged record (`AdminRegistrationListResponse` in Registrations). **Follow the existing precedent**, not the aspirational standard — introducing the shared type is a separate refactor.

### Naming rule for this feature

Nothing in this feature is photo-only. Entities, endpoints, DTOs and components are named `Media*`, never `Photo*`, even though photos will be the bulk of the content. An implementer who sees `PhotoComment` will build a photo-only lightbox and the audio and interviews will have nowhere to live.

---

## Task 1 — Data Model Changes

**Branch:** `feature/feat-photo-albums-social-backend-model`

### 1.1 `MediaItem` — new fields

Add to [MediaItemsModels.cs](src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs):

| Field | Type | Notes |
|-------|------|-------|
| `CampEditionId` | `Guid?` | FK → `CampEdition`, `OnDelete(SetNull)`. The album anchor. `NULL` = edition unknown, pending collaborative dating |
| `CampEdition` | `CampEdition?` | Navigation property |
| `YearSource` | `MediaItemYearSource` | New enum, stored as string, default `Unknown` |
| `CommentCount` | `int` | Denormalised counter, default `0`. Avoids N+1 on album grids |
| `Themes` | `List<MediaItemTheme>` | Navigation for the N:M join (1.4) |
| `MediaSourceId` | `Guid?` | FK → `MediaSource`, `OnDelete(SetNull)`. `NULL` = the uploader is also the provider (1.3) |
| `SourcePath` | `string?` | Max 1024. Original folder path the file came from. A dating clue — see below |

New enum in the same file:

```csharp
public enum MediaItemYearSource
{
    Unknown,    // no year yet — eligible for collaborative dating
    Exif,       // EXIF DateTimeOriginal
    FolderName, // resolved from the import folder name
    Uploader,   // typed into the web upload form
    Community,  // set by collaborative dating consensus
    Admin       // set manually by Admin/Board (always wins, never overwritten)
}
```

EF config additions in [MediaItemConfiguration.cs](src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs):

```csharp
builder.Property(m => m.CampEditionId).HasColumnName("camp_edition_id");

builder.Property(m => m.YearSource)
    .IsRequired()
    .HasConversion<string>()
    .HasMaxLength(20)
    .HasDefaultValue(MediaItemYearSource.Unknown)
    .HasColumnName("year_source");

builder.Property(m => m.CommentCount)
    .IsRequired()
    .HasDefaultValue(0)
    .HasColumnName("comment_count");

builder.HasOne(m => m.CampEdition)
    .WithMany()
    .HasForeignKey(m => m.CampEditionId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasIndex(m => m.CampEditionId)
    .HasDatabaseName("ix_media_items_camp_edition_id");

// Album grid query: edition + approval state
builder.HasIndex(m => new { m.CampEditionId, m.IsApproved, m.IsPublished })
    .HasDatabaseName("ix_media_items_edition_approved_published");

// The unplaced pile: partial index, since this query is "WHERE camp_edition_id IS NULL"
// (declared as raw SQL in the migration — EF cannot express filtered indexes on null checks cleanly)
```

**`SourcePath` and privacy.** The original path is a genuine dating clue: a human may recognise *"Verano con los Martínez"* or *"carrete de la tía Puri"* where the resolver's regex sees nothing. But raw filesystem paths leak — `D:/Users/maria.carmen.lopez/Fotos privadas/...` carries a person's name and their directory structure.

Store the **full** path, but expose only the **last three segments** to regular members — which is exactly where the camp clues live — and the full value to Admin/Board. Implement the trimming in the response mapper, never in the database: the full path is evidence and must not be destroyed.

**Backfill in the migration:** for every existing `media_items` row with a non-null `year`, set `camp_edition_id` to the single `camp_editions` row with that year (there is exactly one edition per year historically), and set `year_source = 'Uploader'`. Rows whose year matches zero or multiple editions are left `NULL` / `Unknown`.

```sql
UPDATE media_items m
SET camp_edition_id = e.id, year_source = 'Uploader'
FROM (
    SELECT year, MIN(id) AS id
    FROM camp_editions
    GROUP BY year
    HAVING COUNT(*) = 1
) e
WHERE m.year = e.year AND m.camp_edition_id IS NULL;

CREATE INDEX ix_media_items_unplaced
ON media_items (created_at DESC)
WHERE camp_edition_id IS NULL;
```

### 1.2 `Memory` — new field

The original note requires each edition to show *"sus fotos, sus audios y sus relatos"*. Written stories currently have no way to attach to an edition.

Add to [MemoriesModels.cs](src/Abuvi.API/Features/Memories/MemoriesModels.cs):

| Field | Type | Notes |
|-------|------|-------|
| `CampEditionId` | `Guid?` | FK → `CampEdition`, `OnDelete(SetNull)`. Same `NULL` semantics as `MediaItem` |
| `CampEdition` | `CampEdition?` | Navigation property |

`MemoryConfiguration.cs` gains the column, FK and an index on `camp_edition_id`. Add `CampEditionId` to `MemoryResponse`. The same year-based backfill as 1.1 applies to `memories`.

> Memories are **not** themed in this feature — themes attach to `MediaItem` only. Adding `MemoryTheme` later is additive and blocks nothing.

### 1.3 New entity: `MediaSource` — who gave us this

Historical material rarely arrives from its own subject. A member hands over a USB stick of 800 photos taken by their late father; a family lends an album; someone who left the association twenty years ago emails a folder. Today `MediaItem.UploadedByUserId` records **the account that performed the upload** — it does not record **who provided the material**, and those are usually different people. The provider is frequently not a registered user at all, so a `User` FK cannot represent them.

This matters well beyond credit. The person who gave you the photo is the single best person to ask what year it is — which makes provenance a direct input to collaborative dating, not just an attribution nicety.

New feature slice: `src/Abuvi.API/Features/MediaSources/`. Table `media_sources`.

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `ContributorName` | `string` | Required, max 200. Free text — *"Manolo García"*. The provider need not be a registered user |
| `ContributorUserId` | `Guid?` | FK → `User`, SetNull. Set when the provider *is* a member, which enables "ask them" links |
| `ContributorContact` | `string?` | Max 200, email or phone. **Admin/Board only — never serialised in member-facing responses** |
| `Notes` | `string?` | Max 1000 — *"pendrive entregado en la asamblea de 2024"* |
| `ReceivedAt` | `DateTime?` | When the material reached the association |
| `RegisteredByUserId` | `Guid` | FK → `User`, Cascade — who recorded this source |
| `CreatedAt` / `UpdatedAt` | `DateTime` | `NOW()` |

Index on `contributor_user_id`; index on `contributor_name` for duplicate detection.

**One source row per donation, not per file.** A batch of 800 photos shares a single `MediaSource`. Correcting a misspelled name once fixes all 800, and *"todo lo que aportó Manolo"* becomes one query instead of a `GROUP BY` over free text.

**Duplicates are inevitable** with free-text names — *"Manolo García"*, *"Manuel García"*, *"manolo garcia"*. The admin panel needs a merge operation (2.2), not just CRUD. Plan for it from the start; retrofitting a merge after 3.000 items point at the wrong rows is painful.

### 1.4 New entities: `MediaTheme` and `MediaItemTheme`

New feature slice: `src/Abuvi.API/Features/MediaThemes/`.

**`MediaTheme`** — table `media_themes`:

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `Name` | `string` | Required, max 100, e.g. *"San Abuvino"* |
| `Slug` | `string` | Required, max 100, **unique index**, kebab-case, e.g. `san-abuvino`. Used in URLs |
| `Description` | `string?` | Max 500 |
| `IsActive` | `bool` | Default `true`. Soft retirement without deleting tags |
| `CreatedAt` / `UpdatedAt` | `DateTime` | `NOW()` |

**`MediaItemTheme`** — join table `media_item_themes`:

| Field | Type | Rules |
|-------|------|-------|
| `MediaItemId` | `Guid` | FK → `MediaItem`, Cascade. Composite PK part 1 |
| `MediaThemeId` | `Guid` | FK → `MediaTheme`, Cascade. Composite PK part 2 |
| `TaggedByUserId` | `Guid` | FK → `User`, Cascade — who applied the tag |
| `CreatedAt` | `DateTime` | `NOW()` |

Composite primary key `(media_item_id, media_theme_id)` makes duplicate tagging impossible at the database level. Index on `media_theme_id` for the theme-browse query.

**Seed catalogue.** Ship an initial theme list so the feature is not empty on day one. Add `src/Abuvi.Setup/seed/media-themes.csv` + a `MediaThemeImporter`, registered in `import-order.json` (no dependencies, same tier as `camps.csv`). Starting set, to be confirmed with the board: *San Abuvino*, *Actuaciones*, *Asambleas*, *Cocina y comedor*, *Excursiones*, *Montaje y desmontaje*, *Juegos de noche*, *Deportes*, *Talleres*.

### 1.5 New entity: `MediaComment`

New feature slice: `src/Abuvi.API/Features/MediaComments/`.

> Named `MediaComment`, **not** `PhotoComment` — comments work on audio, interviews and video too. An interview recording with no date is exactly the kind of item the community will discuss.

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `MediaItemId` | `Guid` | FK → `MediaItem`, **Cascade** delete |
| `AuthorUserId` | `Guid` | FK → `User`, **Cascade** delete |
| `Body` | `string` | Required, 1–1000 chars |
| `CreatedAt` | `DateTime` | `NOW()` |
| `UpdatedAt` | `DateTime` | `NOW()` |
| `DeletedAt` | `DateTime?` | Soft delete — mirrors the `FamilyMember.DeletedAt` pattern already in the codebase |
| `DeletedByUserId` | `Guid?` | Who removed it (author or moderator) |

Table `media_comments`. Indexes: `(media_item_id, created_at)` for the thread query, `author_user_id`.

**Editing window:** an author may edit or delete their own comment for **15 minutes** after creation; after that only Admin/Board can remove it. This keeps the archive stable while allowing typo fixes.

### 1.6 New entity: `MediaCommentReport`

Same slice.

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `MediaCommentId` | `Guid` | FK → `MediaComment`, Cascade |
| `ReportedByUserId` | `Guid` | FK → `User`, Cascade |
| `Reason` | `MediaCommentReportReason` | Enum as string: `Offensive`, `PrivacyConcern`, `Incorrect`, `Other` |
| `Notes` | `string?` | Max 500 |
| `Status` | `MediaCommentReportStatus` | Enum as string: `Pending`, `Actioned`, `Dismissed`. Default `Pending` |
| `CreatedAt` | `DateTime` | `NOW()` |
| `ReviewedAt` | `DateTime?` | |
| `ReviewedByUserId` | `Guid?` | |

Table `media_comment_reports`. **Unique index** on `(media_comment_id, reported_by_user_id)` — one report per user per comment. Index on `status` for the moderation queue.

### 1.7 New entity: `MediaItemYearProposal`

New feature slice: `src/Abuvi.API/Features/MediaDating/`.

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `MediaItemId` | `Guid` | FK → `MediaItem`, Cascade |
| `ProposedByUserId` | `Guid` | FK → `User`, Cascade |
| `ProposedYear` | `int` | Required, 1975 ≤ year ≤ current year |
| `ProposedCampEditionId` | `Guid?` | FK → `CampEdition`, SetNull. Optional venue precision |
| `Rationale` | `string?` | Max 500 — *"my sister was born that summer"* |
| `CreatedAt` / `UpdatedAt` | `DateTime` | `NOW()` |

Table `media_item_year_proposals`. **Unique index** on `(media_item_id, proposed_by_user_id)` — one vote per user per item; re-proposing updates the existing row rather than adding a second vote. Index on `media_item_id`.

Applies to **any** `MediaItemType`, not just photos.

#### Consensus rule (define once, implement in `MediaDatingService`)

Evaluated after every proposal insert, update or withdrawal, for that item only:

1. Skip entirely if `MediaItem.YearSource == Admin` — a manual admin decision is never overwritten by the community.
2. Group the item's proposals by `ProposedYear`. Let `top` be the largest group and `total` the proposal count.
3. If `top.Count >= 3` **and** `top.Count / total >= 0.66`, apply consensus:
   - `Year = top.Year`
   - `Decade = MediaItemMappingExtensions.DeriveDecade(top.Year)` (reuse the existing helper)
   - `CampEditionId` = the most-proposed non-null `ProposedCampEditionId` within `top`, else the unique edition for that year, else unchanged
   - `YearSource = Community`
4. Otherwise leave the item unplaced and expose the current tally through the API so the UI can show *"3 personas dicen 1998, 1 dice 1999"*.

Withdrawal re-runs the rule and **can un-resolve** an item whose consensus no longer holds. Admin/Board can apply any year manually at any time, which sets `YearSource = Admin` and freezes the item against rule 3.

### 1.8 New entity: `CampEditionAttendance`

Added to the existing `Camps` slice (it is edition-scoped, not media-scoped).

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `CampEditionId` | `Guid` | FK → `CampEdition`, Cascade |
| `UserId` | `Guid` | FK → `User`, Cascade — the declarer |
| `FamilyMemberId` | `Guid?` | FK → `FamilyMember`, Cascade. `NULL` = the declarer themselves |
| `CreatedAt` | `DateTime` | `NOW()` |

Table `camp_edition_attendances`. **Unique index** on `(camp_edition_id, user_id, family_member_id)`. Index on `user_id` for the personal timeline.

> **PostgreSQL note:** a `NULL` in a composite unique index does not collide, so `(edition, user, NULL)` could be inserted twice. Enforce self-declaration uniqueness with a **partial unique index** as well:
> ```sql
> CREATE UNIQUE INDEX ux_camp_edition_attendances_self
> ON camp_edition_attendances (camp_edition_id, user_id)
> WHERE family_member_id IS NULL;
> ```

**Ownership rule:** a user may only declare attendance for a `FamilyMember` belonging to their own `FamilyUnit`. Validated in the service; violation → `403`.

**Derived attendance:** attendance is *also* inferred from `Registration` + `RegistrationMember` for recent editions. The read model unions both sources and tags each row `Declared` or `Registration`. Never write derived rows into the table.

### 1.9 Registration and migration

- Register all seven new `DbSet`s in [AbuviDbContext.cs](src/Abuvi.API/Data/AbuviDbContext.cs): `MediaSources`, `MediaThemes`, `MediaItemThemes`, `MediaComments`, `MediaCommentReports`, `MediaItemYearProposals`, `CampEditionAttendances`.
- Add EF configurations in `src/Abuvi.API/Data/Configurations/`: `MediaSourceConfiguration.cs`, `MediaThemeConfiguration.cs`, `MediaItemThemeConfiguration.cs`, `MediaCommentConfiguration.cs`, `MediaCommentReportConfiguration.cs`, `MediaItemYearProposalConfiguration.cs`, `CampEditionAttendanceConfiguration.cs`. All follow the existing snake_case + `HasConversion<string>()` for enums + `HasDefaultValueSql("NOW()")` conventions.
- Single migration: `dotnet ef migrations add AddCampAlbumsThemesAndSocial --project src/Abuvi.API`
- The migration must contain the 1.1 and 1.2 backfills, the unplaced partial index, and the 1.8 partial unique index as raw SQL (`migrationBuilder.Sql(...)`).
- Review the generated migration before applying.

### 1.10 Task 1 tests

`src/Abuvi.Tests/Unit/Data/Entities/` — mirror the existing entity-config tests:

- `MediaSourceConfigurationTests` — column names, `contributor_name` required, FK delete behaviours
- `MediaThemeConfigurationTests` — unique slug index; `MediaItemThemeConfigurationTests` — composite PK present
- `MediaCommentConfigurationTests` — table/column names, cascade behaviour, max lengths
- `MediaItemYearProposalConfigurationTests` — unique `(media_item_id, proposed_by_user_id)` index present
- `CampEditionAttendanceConfigurationTests` — both unique indexes present
- `MediaItemConfigurationTests` — extend for `camp_edition_id`, `year_source`, `comment_count`, `media_source_id`, `source_path`
- `MemoryConfigurationTests` — extend for `camp_edition_id`

### Task 1 Definition of Done

- [ ] Seven new entities + seven EF configurations, all `DbSet`s registered
- [ ] `MediaItem` extended with `CampEditionId`, `YearSource`, `CommentCount`, `Themes`, `MediaSourceId`, `SourcePath`
- [ ] `Memory` extended with `CampEditionId`
- [ ] Theme seed CSV + importer wired into `import-order.json`
- [ ] Migration generated, reviewed, applied locally; both backfills verified against seeded data
- [ ] `dotnet test` green
- [ ] [data-model.md](ai-specs/specs/data-model.md) updated: seven new entity sections, `Memory` and `MediaItem` sections revised; `PhotoAlbum`, `Photo` and `CampLocation` sections marked **`SUPERSEDED — never implemented, do not build`** with a pointer to `MediaItem.CampEditionId`
- [ ] ER diagram in data-model.md updated

---

## Task 2 — API Endpoints

**Branch:** `feature/feat-photo-albums-social-backend-api`
**Depends on:** Task 1

All endpoints require authentication (`RequireAuthorization()`). No anonymous access anywhere in this feature.

### 2.1 Upload without knowing the edition

This is the flow that feeds everything else, and it is the one gap that would otherwise block the whole feature: **today's `CreateMediaItemRequest` has no way to name an edition at all.**

Extend `CreateMediaItemRequest` in [MediaItemsModels.cs](src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs):

```csharp
public record CreateMediaItemRequest(
    string FileUrl,
    string? ThumbnailUrl,
    MediaItemType Type,
    string Title,
    string? Description,
    int? Year,
    Guid? MemoryId,
    Guid? CampLocationId,        // DEPRECATED — dead column, see trap 1. Ignored by the service
    string? Context,
    Guid? AccommodationId = null,
    Guid? ZoneId = null,
    Guid? CampEditionId = null,  // NEW — null means "I don't know, let the community place it"
    IReadOnlyList<Guid>? ThemeIds = null,   // NEW — optional themes at upload time
    Guid? MediaSourceId = null,             // NEW — an existing source, when uploading a further batch
    NewMediaSourceRequest? NewSource = null, // NEW — create a source inline for a first-time contributor
    string? SourcePath = null);             // NEW — original folder path, when the browser exposes one

public record NewMediaSourceRequest(
    string ContributorName,
    Guid? ContributorUserId,
    string? ContributorContact,
    string? Notes,
    DateTime? ReceivedAt);
```

Extend `CreateMemoryRequest` the same way with `Guid? CampEditionId`.

Service rules in `MediaItemsService.CreateAsync`:

- `CampEditionId` provided → set it, and set `YearSource = Uploader`; derive `Year` and `Decade` from the edition when the caller did not supply a year.
- `CampEditionId` null but `Year` provided → resolve to the unique edition for that year if one exists; `YearSource = Uploader`.
- Neither provided → `CampEditionId = NULL`, `YearSource = Unknown`. The item lands in the unplaced pile and becomes eligible for collaborative dating. **This is a valid, expected outcome, not an error.** No validation rule may require an edition or a year.
- `ThemeIds` → create `MediaItemTheme` rows, ignoring unknown or inactive theme ids rather than failing the upload.
- Provenance: `MediaSourceId` and `NewSource` are mutually exclusive → `400` if both are supplied. `NewSource` creates one `MediaSource` with `RegisteredByUserId` = the caller. Both null → `MediaSourceId = NULL`, meaning the uploader is the provider, which is the common case for a member uploading their own material.
- `SourcePath` is stored verbatim when supplied. Browsers only expose a relative path for directory uploads (`webkitRelativePath`), so it is usually null on the web and always populated by the importer.

All of the above is type-agnostic: an audio interview with no known year is as valid as a photo with no known year.

### 2.2 Provenance — sources

New slice `src/Abuvi.API/Features/MediaSources/`: `MediaSourcesEndpoints.cs`, `MediaSourcesModels.cs`, `MediaSourcesService.cs`, `MediaSourcesRepository.cs` + interface, `MediaSourcesValidator.cs`, `MediaSourcesExtensions.cs`.

| Method | URL | Auth | Purpose |
|--------|-----|------|---------|
| `GET` | `/api/media-sources` | Any authenticated | Catalogue of contributors with item counts and year span |
| `GET` | `/api/media-sources/{id}` | Any authenticated | One contributor + their contributed items (paged) |
| `POST` | `/api/media-sources` | Any authenticated | Register a contributor |
| `PUT` | `/api/media-sources/{id}` | Admin, Board, or the registrar | Correct name / notes / contact |
| `POST` | `/api/media-sources/{id}/merge` | Admin, Board | Merge into another source. Body `{ targetId }` |
| `DELETE` | `/api/media-sources/{id}` | Admin | Delete; items keep their media, `MediaSourceId` becomes `NULL` |
| `PATCH` | `/api/media-items/{id}/source` | Admin, Board, or the uploader | Reassign one item's source |

```csharp
public record MediaSourceResponse(
    Guid Id,
    string ContributorName,
    Guid? ContributorUserId,
    string? ContributorContact,   // null unless the caller is Admin/Board — see below
    string? Notes,
    DateTime? ReceivedAt,
    Guid RegisteredByUserId,
    string RegisteredByName,
    int ItemCount,
    int UndatedItemCount,         // how much of their material still needs dating
    int? FirstYear,
    int? LastYear,
    DateTime CreatedAt);
```

**`ContributorContact` is Admin/Board only.** It is the contact detail of a person who may not be a member and never agreed to be listed to the whole association. Strip it in the mapper based on the caller's role — do not rely on the frontend to hide it. Cover this with an explicit integration test asserting the field is `null` for a Member caller.

**Merge** (`POST /api/media-sources/{id}/merge`) repoints every `MediaItem.MediaSourceId` from the source to the target inside one transaction, then deletes the emptied source. It is the operation that keeps a free-text contributor list usable over time; without it the catalogue degrades into near-duplicates within a year.

**Why members can create sources.** Any member can register a contributor because any member may be the one collecting a neighbour's shoebox of photos. Editing is restricted to Admin/Board or the person who registered it, which prevents drive-by renaming while keeping correction easy for whoever actually knows the provenance.

### 2.3 Albums

Albums are queries over `MediaItem` + `Memory`, not an entity. Added to the existing `MediaItems` slice.

| Method | URL | Auth | Purpose |
|--------|-----|------|---------|
| `GET` | `/api/camp-editions/albums` | Any authenticated | Index of all 50 editions with counts and cover |
| `GET` | `/api/camp-editions/{editionId}/album` | Any authenticated | One album: edition metadata + paged media of all types |
| `GET` | `/api/media-items/unplaced` | Any authenticated | The "sin ubicar" pile — approved items with `CampEditionId IS NULL` |
| `PATCH` | `/api/media-items/{id}/edition` | Admin, Board | Manually assign/reassign an edition. Sets `YearSource = Admin` |

**`GET /api/camp-editions/albums`** — returns `AlbumSummaryResponse[]`, ordered by `Year` descending:

```csharp
public record AlbumSummaryResponse(
    Guid CampEditionId,
    int Year,
    Guid CampId,
    string CampName,
    string? CampLocality,
    decimal? Latitude,
    decimal? Longitude,
    int PhotoCount,
    int VideoCount,
    int AudioCount,       // includes Interview
    int DocumentCount,
    int MemoryCount,      // now computable — Memory.CampEditionId from 1.2
    string? CoverThumbnailUrl,   // IsPrimary photo, else most recent approved photo
    bool ViewerAttended);        // from CampEditionAttendance ∪ Registration
```

Single query with `GROUP BY` — must not be N+1 across 50 editions. Verify with EF logging in the integration test.

**`GET /api/camp-editions/{editionId}/album`** — query params `page` (default 1), `pageSize` (default 24, max 100), `type` (optional `MediaItemType`), `themeId` (optional). Returns:

```csharp
public record AlbumDetailResponse(
    AlbumSummaryResponse Edition,
    IReadOnlyList<AlbumMediaItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
```

`AlbumMediaItemResponse` = the existing `MediaItemResponse` fields plus `CampEditionId`, `YearSource`, `CommentCount`, `Themes` (`IReadOnlyList<MediaThemeSummaryResponse>`), and provenance:

```csharp
    Guid? MediaSourceId,
    string? MediaSourceName,      // "Aportado por Manolo García"
    string? SourcePathDisplay,    // last three segments only for Members; full path for Admin/Board
```

With no `type` filter the album returns **all** media types interleaved, ordered by `DisplayOrder` then `CreatedAt`. The frontend groups them for display; the API does not pre-segment.

Relatos are fetched separately via the extended `GET /api/memories?campEditionId=` (2.8), so the two lists paginate independently.

Non-Admin callers see only `IsApproved && IsPublished` items. Admin/Board additionally see pending items, flagged as such.

Follow the `AdminRegistrationListResponse` precedent for the paged shape — do **not** introduce `PagedResult<T>` (see trap 4).

### 2.4 Themes

New slice `src/Abuvi.API/Features/MediaThemes/`: `MediaThemesEndpoints.cs`, `MediaThemesModels.cs`, `MediaThemesService.cs`, `MediaThemesRepository.cs` + interface, `MediaThemesValidator.cs`, `MediaThemesExtensions.cs`.

| Method | URL | Auth | Purpose |
|--------|-----|------|---------|
| `GET` | `/api/media-themes` | Any authenticated | Catalogue with item counts and year span |
| `POST` | `/api/media-themes` | Admin, Board | Create a theme |
| `PUT` | `/api/media-themes/{id}` | Admin, Board | Rename / edit / deactivate |
| `DELETE` | `/api/media-themes/{id}` | Admin | Delete; cascades its tags. `409` if it has tags unless `?force=true` |
| `GET` | `/api/media-themes/{slug}/items` | Any authenticated | Paged items for a theme, **across all editions** |
| `POST` | `/api/media-items/{id}/themes` | Any authenticated | Attach a theme. Body `{ themeId }` |
| `DELETE` | `/api/media-items/{id}/themes/{themeId}` | Tagger, or Admin/Board | Detach |

```csharp
public record MediaThemeSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int ItemCount,
    int? FirstYear,      // earliest dated item carrying this theme
    int? LastYear,       // latest — this is what makes "spans many years" visible
    int UndatedCount);   // items with this theme still awaiting an edition

public record ThemeItemsResponse(
    MediaThemeSummaryResponse Theme,
    IReadOnlyList<AlbumMediaItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
```

`GET /api/media-themes/{slug}/items` supports `?year=`, `?campEditionId=`, `?undatedOnly=true` and `?type=`. Default ordering is `Year` descending with undated items last.

Any authenticated member can attach and detach themes on approved items. Themes carry no personal data, tagging is trivially reversible, and this is exactly the low-effort contribution the note asks for — a member who cannot remember the year may still recognise *San Abuvino*.

**Slug generation:** derive from `Name` (lowercase, strip accents, non-alphanumerics → `-`), enforce uniqueness by appending `-2`, `-3` on collision. Implement in `MediaThemesService`, unit-tested.

### 2.5 Comments

New slice `src/Abuvi.API/Features/MediaComments/`: `MediaCommentsEndpoints.cs`, `MediaCommentsModels.cs`, `MediaCommentsService.cs`, `MediaCommentsRepository.cs` + `IMediaCommentsRepository.cs`, `MediaCommentsValidator.cs`, `MediaCommentsExtensions.cs`.

| Method | URL | Auth | Purpose |
|--------|-----|------|---------|
| `GET` | `/api/media-items/{mediaItemId}/comments` | Any authenticated | Thread, oldest first, excludes soft-deleted |
| `POST` | `/api/media-items/{mediaItemId}/comments` | Any authenticated | Add a comment. Publishes immediately |
| `PUT` | `/api/media-comments/{id}` | Author, within 15 min | Edit body |
| `DELETE` | `/api/media-comments/{id}` | Author (15 min) or Admin/Board (always) | Soft delete |
| `POST` | `/api/media-comments/{id}/report` | Any authenticated | Report a comment |
| `GET` | `/api/media-comments/reports` | Admin, Board | Moderation queue, filter by `status` |
| `PATCH` | `/api/media-comments/reports/{id}` | Admin, Board | `{ status: "Actioned" \| "Dismissed" }` |

DTOs:

```csharp
public record CreateMediaCommentRequest(string Body);
public record UpdateMediaCommentRequest(string Body);
public record ReportMediaCommentRequest(MediaCommentReportReason Reason, string? Notes);

public record MediaCommentResponse(
    Guid Id,
    Guid MediaItemId,
    Guid AuthorUserId,
    string AuthorName,
    string Body,
    bool CanEdit,        // viewer is author AND within the 15-minute window
    bool CanDelete,      // viewer is author-in-window OR Admin/Board
    bool ViewerReported, // viewer already reported this comment
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

Service rules:

- Reject a comment on a `MediaItem` that is not `IsApproved` unless the caller is Admin/Board → `403`.
- Increment / decrement `MediaItem.CommentCount` in the same transaction as insert / soft-delete.
- `CanEdit` window: `DateTime.UtcNow - CreatedAt < TimeSpan.FromMinutes(15)`. Define the window as a `const` in the service, not a magic number at the call site.
- Rate limit: max **10 comments per user per minute**, `429` beyond that.
- Validation (FluentValidation, Spanish messages per [backend-standards.mdc](ai-specs/specs/backend-standards.mdc) § Validation Messages): `Body` not empty, ≤ 1000 chars → `"El comentario no puede superar los 1000 caracteres"`.

### 2.6 Collaborative dating

New slice `src/Abuvi.API/Features/MediaDating/`.

| Method | URL | Auth | Purpose |
|--------|-----|------|---------|
| `GET` | `/api/media-items/{mediaItemId}/year-proposals` | Any authenticated | Current tally + viewer's own proposal + theme hints |
| `PUT` | `/api/media-items/{mediaItemId}/year-proposals` | Any authenticated | Upsert the caller's proposal, then run consensus |
| `DELETE` | `/api/media-items/{mediaItemId}/year-proposals` | Any authenticated | Withdraw, then re-run consensus |
| `PATCH` | `/api/media-items/{id}/year` | Admin, Board | Force a year. Sets `YearSource = Admin` |

```csharp
public record UpsertYearProposalRequest(
    int ProposedYear,
    Guid? ProposedCampEditionId,
    string? Rationale);

public record YearProposalTallyResponse(
    Guid MediaItemId,
    int? ResolvedYear,
    string YearSource,
    bool IsResolved,
    IReadOnlyList<YearProposalGroupResponse> Groups,   // ordered by Count desc
    YearProposalResponse? ViewerProposal,
    IReadOnlyList<ThemeYearHintResponse> ThemeHints,
    SourceHintResponse? SourceHint);

public record YearProposalGroupResponse(
    int Year,
    Guid? CampEditionId,
    string? CampName,
    int Count,
    IReadOnlyList<string> ProposerNames);   // capped at 5 for display

// Themes are a dating clue: if this item is tagged "San Abuvino" and other
// San Abuvino items are already dated to 1998, 2003 and 2011, show those years.
public record ThemeYearHintResponse(
    Guid ThemeId,
    string ThemeName,
    IReadOnlyList<int> YearsWithDatedItems);

// Provenance is the strongest clue of all: the person who handed over the
// material usually knows roughly when it is from, and the folder it came out
// of often names the year or the venue outright.
public record SourceHintResponse(
    Guid? MediaSourceId,
    string? ContributorName,
    Guid? ContributorUserId,          // non-null => the UI can offer "preguntar a esta persona"
    IReadOnlyList<int> YearsFromSameSource,  // editions this contributor's other items resolved to
    string? SourcePathDisplay);              // trimmed per the 1.1 privacy rule
```

`PUT` uses an upsert keyed on the unique `(media_item_id, proposed_by_user_id)` index. Consensus evaluation (rule 1.7) runs inside the same transaction.

**Suggestion hook:** `GET /api/media-items/unplaced` accepts `?suggestedForMe=true`, restricting results to items whose EXIF, partial-folder, theme or **source** evidence points at an edition the caller attended (`CampEditionAttendance` ∪ `Registration`), plus everything the caller personally contributed. This is the payoff described in the note — "who to ask about an undated photo" — and provenance answers it more directly than attendance does: ask the person who gave it to you first.

It also accepts `?mediaSourceId=` so a contributor's whole undated batch can be worked through in one sitting, which is how this material actually gets dated in practice.

### 2.7 Attendance — "I was at this camp"

Added to the existing `Camps` slice: `CampEditionAttendanceService.cs`, `CampEditionAttendanceRepository.cs` + interface, endpoints in [CampsEndpoints.cs](src/Abuvi.API/Features/Camps/CampsEndpoints.cs).

| Method | URL | Auth | Purpose |
|--------|-----|------|---------|
| `POST` | `/api/camp-editions/{editionId}/attendance` | Any authenticated | Declare. Body `{ familyMemberId?: Guid }` |
| `DELETE` | `/api/camp-editions/{editionId}/attendance` | Any authenticated | Withdraw. Query `?familyMemberId=` |
| `GET` | `/api/camp-editions/{editionId}/attendance` | Any authenticated | Who attended (declared ∪ derived) |
| `GET` | `/api/users/me/camp-timeline` | Any authenticated | The caller's timeline across all 50 editions |

```csharp
public record CampTimelineResponse(
    int TotalEditionsAttended,
    IReadOnlyList<CampTimelineEntryResponse> Entries);

public record CampTimelineEntryResponse(
    Guid CampEditionId,
    int Year,
    string CampName,
    decimal? Latitude,
    decimal? Longitude,
    bool Attended,
    string AttendanceSource,   // "Declared" | "Registration" | "None"
    int MediaCount);
```

`GET /api/users/me/camp-timeline` returns **all 50 editions** with `Attended` true/false, so the frontend can render "your camps" over the full map without a second call.

Rules:

- `POST` with a `familyMemberId` outside the caller's `FamilyUnit` → `403` `"No puedes declarar asistencia por este familiar"`.
- `POST` for an already-declared `(edition, user, member)` → `200` idempotent, not `409`.
- Derived rows (from `Registration`) cannot be deleted through `DELETE` → `400` with an explanatory message.

### 2.8 Filter extensions on existing endpoints

- `GET /api/media-items` — add `campEditionId`, `unplacedOnly`, `themeId`. **Keep all current parameters and behaviour intact** — `AnniversaryGallery.vue` depends on `approved` + `context`.
- `GET /api/memories` — add `campEditionId` and `unplacedOnly`.

### 2.9 Task 2 tests

Unit (`src/Abuvi.Tests/Unit/Features/`):

- `MediaItems/MediaItemsServiceTests` — **upload with no edition and no year succeeds** and lands `Unknown`/`NULL`; upload with year only resolves the unique edition; upload with unknown `ThemeIds` still succeeds
- `MediaSources/MediaSourcesServiceTests` — merge repoints all items and deletes the emptied source; `ContributorContact` stripped for a Member caller and present for Admin; `SourcePathDisplay` trimmed to three segments for a Member and full for Admin; deleting a source nulls `MediaSourceId` without deleting media
- `MediaThemes/MediaThemesServiceTests` — slug generation and collision suffixes; attach/detach; duplicate attach is idempotent; `FirstYear`/`LastYear` span calculation
- `MediaComments/MediaCommentsServiceTests` — comment on unapproved item forbidden; edit window boundary (14:59 allowed, 15:01 rejected); `CommentCount` increments and decrements; duplicate report rejected
- `MediaDating/MediaDatingServiceTests` — consensus at exactly 3 proposals / 66 %; no consensus at 2; `Admin` source never overwritten; withdrawal re-runs consensus and can un-resolve; theme hints returned
- `Camps/CampEditionAttendanceServiceTests` — ownership rule, idempotent declare, derived rows not deletable, timeline returns all 50 editions

Integration (`src/Abuvi.Tests/Integration/Features/`):

- `MediaItems/AlbumEndpointsTests` — album index has no N+1 (assert query count); pagination boundaries; non-admin cannot see unapproved items; **an album containing a photo, an audio, a video and a memory returns all four in the right counts**
- `MediaThemes/MediaThemesEndpointsTests` — a theme tagged on items from three different editions returns all three from one call; role checks on create/delete
- `MediaSources/MediaSourcesEndpointsTests` — **`contributorContact` is `null` in the response for a Member and populated for Admin/Board**; upload with `NewSource` creates exactly one source for a batch; supplying both `MediaSourceId` and `NewSource` returns `400`
- `MediaComments/MediaCommentsEndpointsTests` — full lifecycle; `403` for Member on moderation endpoints
- `Camps/CampEditionAttendanceEndpointsTests` — declare / withdraw / timeline

### Task 2 Definition of Done

- [ ] All endpoints implemented, registered in `Program.cs`, visible in Swagger
- [ ] **Upload with no edition works end to end for every `MediaItemType`**
- [ ] **Contributor contact details never leave the API for non-Admin callers** (integration test, not a frontend guard)
- [ ] Source merge verified transactional against a batch spanning several editions
- [ ] FluentValidation validators for every request DTO, Spanish messages, none of them requiring an edition or a year
- [ ] Role checks enforced with `RequireAuthorization(policy => policy.RequireRole(...))`
- [ ] Album index query verified free of N+1
- [ ] Existing `/api/media-items` and `/api/memories` behaviour unchanged (regression test)
- [ ] `dotnet test` green
- [ ] [api-endpoints.md](ai-specs/specs/api-endpoints.md) updated with all new endpoints, request/response examples and error codes

---

## Task 3 — Bulk Media Importer (Abuvi.Setup CLI)

**Branch:** `feature/feat-photo-albums-social-setup-importer`
**Depends on:** Task 1 only — can run in parallel with Task 2

New command in [Program.cs](src/Abuvi.Setup/Program.cs), modelled on the existing `geocode` command:

```
dotnet run --project src/Abuvi.Setup -- import-media --dir=<root> [--dry-run] [--report=<path>]
    [--min-confidence=medium] [--uploader=<email>]
    [--source-name="Manolo García"] [--source-user=<email>] [--source-contact=<email|phone>]
    [--source-notes="pendrive entregado en la asamblea de 2024"] [--source-id=<guid>]
```

New files:

- `src/Abuvi.Setup/Media/MediaImportRunner.cs` — walk, resolve, upload, persist
- `src/Abuvi.Setup/Media/EditionResolver.cs` — pure resolution logic (the testable core)
- `src/Abuvi.Setup/Media/ExifReader.cs` — thin wrapper over ImageSharp's `ExifProfile`
- `src/Abuvi.Setup/Media/MediaImportReportWriter.cs` — HTML review report

**No new NuGet packages.** ImageSharp comes transitively via the `Abuvi.API` project reference and reads EXIF through `Image.Identify(path).Metadata.ExifProfile`. `BlobStorageService` is constructible by hand — `new BlobStorageService(repository, Options.Create(cfg), new MemoryCache(new MemoryCacheOptions()))` — the same way the `geocode` command builds its Google Places dependencies.

### 3.1 Supported file types

Not photo-only. Map extension → `MediaItemType` using the lists already in `BlobStorageOptions`:

| Extensions | `MediaItemType` | Thumbnail | EXIF |
|------------|-----------------|-----------|------|
| `AllowedImageExtensions` | `Photo` | yes | yes |
| `AllowedVideoExtensions` | `Video` | no | no |
| `AllowedAudioExtensions` | `Audio` | no | no |
| `AllowedDocumentExtensions` | `Document` | no | no |

Non-image files resolve their edition from **folder evidence only** — that is the whole difference. They are not skipped, and an unresolved audio file lands in the unplaced pile exactly like an unresolved photo. Anything outside all four lists, or over `MaxFileSizeBytes`, is logged and skipped.

`Interview` is not inferred automatically; an admin can reclassify an `Audio` item afterwards.

### 3.2 Resolution algorithm (`EditionResolver`)

For each file, produce a `MediaMatch { CampEditionId?, Year?, YearSource, Confidence, ThemeIds[], Evidence[] }`.

**Step 1 — folder-name year.** Scan path segments from the file's own folder outward. First match wins.

- 4-digit: `\b(19[7-9]\d|20[0-4]\d)\b` → e.g. `2003 Espinosa`, `Campa 1998`
- 2-digit, **only** when the same segment also matched a camp name (step 2): `\b([7-9]\d|[0-2]\d)\b` → `Selva de Oza 77` → 1977. Never accept a bare 2-digit number with no camp-name evidence.

**Step 2 — folder-name venue.** Normalise each path segment (lowercase, strip accents, drop the stopwords `campa`, `campamento`, `abuvi`, `fotos`, `verano`) and match against `Camp.Name` normalised the same way. Accept on token containment or Levenshtein distance ≤ 2.

**Step 3 — folder-name theme.** Match remaining normalised segments against `MediaTheme.Name` / `Slug` using the same comparison. A folder `1998/San Abuvino/` yields both the edition **and** the theme. Themes are attached regardless of whether the edition resolved — a `Fotos sueltas/San Abuvino/` folder still produces a themed, undated item, which is a much better starting point for the community than an untagged one.

**Step 4 — EXIF** (images only). `DateTimeOriginal` (tag 36867) → year. GPS latitude/longitude when present → nearest `Camp` within **25 km**. No haversine helper exists in the codebase — add a small private one to `EditionResolver` (earth radius 6371 km) and unit-test it.

**Step 5 — combine.** There is exactly one edition per year in the historical data, so a resolved year alone usually determines the edition.

| Evidence | Edition | Confidence | `YearSource` |
|----------|---------|------------|--------------|
| Folder year + folder venue agree on one edition | that edition | `High` | `FolderName` |
| Folder year → unique edition for that year | that edition | `High` | `FolderName` |
| Folder venue only, and that venue has exactly one edition | that edition | `Medium` | `FolderName` |
| EXIF year → unique edition, no folder evidence | that edition | `Medium` | `Exif` |
| EXIF GPS venue only, one edition at that venue | that edition | `Medium` | `Exif` |
| Folder year and EXIF year disagree | **folder year wins** | `Medium` | `FolderName` |
| Nothing resolves | `NULL` | `None` | `Unknown` |

Below `--min-confidence` (default `medium`) → imported into the **unplaced pile** with `CampEditionId = NULL`, not discarded, **with any themes still attached**. This is the pile the community dates in Task 2.6.

### 3.3 Import behaviour

- **Idempotent.** Compute a SHA-256 of the file bytes; skip if a `MediaItem` already exists with that hash. Store the hash in `MediaItem.Context` as `import:<sha256-prefix-16>` — no schema change, and `context` is already indexed.
- **Upload** through `IBlobStorageService.UploadAsync(..., folder: "anniversary", contextId: campEditionId, generateThumbnail: <image only>, ct)`.
- **Provenance.** `--source-name` creates one `MediaSource` for the entire run and links every imported item to it; `--source-id` reuses an existing one instead. Supplying neither leaves `MediaSourceId` null. `--source-user=<email>` resolves to a `User` and sets `ContributorUserId`, which is what later enables "ask this person" on undated items. **This is the flag that makes a shoebox donation traceable a decade later — the runbook should treat it as effectively required.**
- **`SourcePath`** is recorded for every file as the path **relative to `--dir`**, never the absolute path. This deliberately drops `C:/Users/<name>/` at the point of capture: the association has no reason to store the donor's home directory layout, and the camp clues live in the trailing segments anyway.
- **Create** `MediaItem` with the mapped `Type`, `Title` = original filename without extension, `UploadedByUserId` from `--uploader=<email>` (default: seeded admin), `IsApproved = true`, `IsPublished = true` (historical archive material is vetted by whoever runs the import), `Year`, `Decade` via the existing `DeriveDecade`, `CampEditionId`, `YearSource`, plus `MediaItemTheme` rows.
- **`--dry-run` resolves and reports but uploads nothing and writes nothing** — consistent with the existing `SetupConfig.DryRun` behaviour.
- **Report**: an HTML file mirroring the existing `docs/CAMPAMENTOS_HISTORICOS-geocode-review.html` produced by `ReviewMapWriter` — one row per file with thumbnail (images) or type icon, resolved edition, themes, confidence, evidence trail, and any folder/EXIF year conflict highlighted. Group unresolved files at the top; they are the ones a human should eyeball. Default path `docs/media-import-review.html`.
- Return a `SeedResult` and print it through the existing `SeedResult.Print(dryRun)` so output matches the other importers.

### 3.4 Task 3 tests

`src/Abuvi.Tests/Unit/Setup/Media/`:

- `EditionResolverTests` — one test per row of the 3.2 table, plus: `Campa 1998` → 1998; `Selva de Oza 77` → 1977 only when the venue matches; bare `77` with no venue → unresolved; `2003 Espinosa` where folder and EXIF disagree → folder wins with `Medium`; `1998/San Abuvino/` → edition **and** theme; `Fotos sueltas/San Abuvino/` → theme only, edition `NULL`
- `ExifReaderTests` — reads `DateTimeOriginal` and GPS from a fixture image in `src/Abuvi.Tests/Helpers/TestFiles/`; missing EXIF returns nulls without throwing
- `MediaImportRunnerTests` — dry-run performs no uploads (assert on a substituted `IBlobStorageService`); duplicate hash skipped; **an `.mp3` and a `.pdf` are imported with the right `MediaItemType` and no thumbnail**; unresolved files still produce a `MediaItem` with `CampEditionId = NULL`; `--source-name` creates **one** `MediaSource` for a 50-file run, not fifty; `SourcePath` is relative to `--dir` and never contains the absolute prefix

### Task 3 Definition of Done

- [ ] `import-media` command works for images, audio, video and documents
- [ ] `--dry-run` verified to write nothing
- [ ] Re-running the import produces zero duplicates
- [ ] Themes inferred from folder names and attached even when the edition is unresolved
- [ ] `--source-name` produces a single `MediaSource` per run, linked to every item
- [ ] `SourcePath` stored relative to `--dir`, verified to exclude the absolute prefix
- [ ] HTML review report generated and readable, unresolved files grouped first
- [ ] `dotnet test` green
- [ ] `src/Abuvi.Setup/schemas/README.md` documents the command, flags and folder conventions
- [ ] Usage block in `Program.cs` updated

---

## Task 4 — Frontend

**Branch:** `feature/feat-photo-albums-social-frontend`
**Depends on:** Task 2

All routes carry `requiresAuth: true`.

### 4.1 Routes

| Path | Name | Component |
|------|------|-----------|
| `/anniversary/albums` | `anniversary-albums` | `AlbumsIndexPage.vue` |
| `/anniversary/albums/:editionId` | `anniversary-album` | `AlbumDetailPage.vue` |
| `/anniversary/temas` | `anniversary-themes` | `ThemesIndexPage.vue` |
| `/anniversary/temas/:slug` | `anniversary-theme` | `ThemeDetailPage.vue` |
| `/anniversary/aportaciones` | `anniversary-sources` | `SourcesIndexPage.vue` |
| `/anniversary/aportaciones/:id` | `anniversary-source` | `SourceDetailPage.vue` |
| `/anniversary/sin-ubicar` | `anniversary-unplaced` | `UnplacedMediaPage.vue` |
| `/anniversary/mis-campamentos` | `anniversary-my-camps` | `MyCampTimelinePage.vue` |

Add these to [router/index.ts](frontend/src/router/index.ts) and to the sticky section nav in [AnniversaryPage.vue](frontend/src/views/AnniversaryPage.vue).

### 4.2 Components

`frontend/src/components/anniversary/` — **media-neutral names throughout** (see the naming rule above):

| Component | Responsibility |
|-----------|----------------|
| `AlbumGrid.vue` | 50 edition cards: year, venue, per-type counts, cover, "Yo estuve" badge |
| `AlbumMediaGrid.vue` | Paged media grid for one edition. Type filter chips (Todo / Fotos / Audios / Vídeos / Documentos) |
| `MediaCard.vue` | One item rendered by type: photo thumbnail, audio player row, video poster, document icon + filename |
| `MediaLightbox.vue` | Full item view. Photo, `<video>`, `<audio>` or document link by type, plus metadata, comments, themes, dating |
| `MediaCommentThread.vue` | List + composer. Optimistic insert, rollback on failure |
| `MediaCommentItem.vue` | One comment: author, relative time, edit/delete when `canEdit`/`canDelete`, report action |
| `MediaDatingPanel.vue` | Year tally bars, theme hints, year picker + optional edition picker, rationale field |
| `MediaThemeChips.vue` | Themes on an item; add via autocomplete, remove when permitted |
| `MediaSourceBadge.vue` | *"Aportado por Manolo García"* on an item; links to the contributor page |
| `MediaSourcePicker.vue` | Upload-form control: Yo / Otra persona (name, optional notes); reuses an existing source via autocomplete |
| `SourceGrid.vue` | Contributor cards: name, item count, year span, how many of theirs still need dating |
| `SourceDetail.vue` | Everything one person contributed, with a *"ayúdanos a datar lo suyo"* call to action |
| `ThemeGrid.vue` | Theme catalogue cards: name, item count, year span (*"1981 – 2019"*), undated count |
| `ThemeTimeline.vue` | One theme's items grouped by year, undated group last |
| `AttendanceButton.vue` | "Yo estuve en este campamento" toggle; family-member dropdown when the user has a family unit |
| `CampTimelineMap.vue` | The 50 editions on the map with the viewer's attended ones highlighted. Reuse the existing map integration ([frontend-standards.mdc](ai-specs/specs/frontend-standards.mdc) § Maps Integration) |

`frontend/src/components/admin/`:

| Component | Responsibility |
|-----------|----------------|
| `MediaCommentReportsPanel.vue` | Moderation queue; Actioned / Dismissed. Sits next to `MediaItemsReviewPanel.vue` |
| `MediaThemesAdminPanel.vue` | Theme catalogue CRUD |
| `MediaSourcesAdminPanel.vue` | Contributor CRUD **plus merge** — select two near-duplicates, pick the survivor. Shows `contributorContact`, which no other screen does |

### 4.3 Changes to the existing upload form

[AnniversaryUploadForm.vue](frontend/src/components/anniversary/AnniversaryUploadForm.vue) gains, for **all** media types:

- A camp-edition selector, with a first, pre-selected option **"No lo sé — que la comunidad lo ubique"**. Choosing it sends `campEditionId: null`, which is a valid submission.
- A theme multi-select (optional), backed by `GET /api/media-themes`.
- Helper text under the "no lo sé" option: *"Tu recuerdo irá a la sección Sin ubicar, donde otros abuvinos podrán ayudar a datarlo."*
- A `MediaSourcePicker`: **"¿De quién es este material?"** with two options — *"Mío"* (default, sends no source) or *"De otra persona"*, which reveals a name field, an optional autocomplete against existing contributors, and optional notes. The contact field appears **only for Admin/Board**.
- When the browser supplies `webkitRelativePath` (directory uploads), send it as `sourcePath` without showing it in the form.

The form must not block submission on a missing edition or year. This is the single most important behavioural change in Task 4 — it is what makes the collaborative dating loop start.

### 4.4 Composables and types

`frontend/src/composables/`: `useAlbums.ts`, `useMediaSources.ts`, `useMediaThemes.ts`, `useMediaComments.ts`, `useMediaDating.ts`, `useCampAttendance.ts` — all following the `useMediaItems.ts` shape exactly (`ref` state, `loading`/`error`, `ApiResponse<T>` unwrapping, Spanish error strings, `console.error` on catch).

`frontend/src/types/`: `album.ts`, `media-source.ts`, `media-theme.ts`, `media-comment.ts`, `media-dating.ts`, `camp-attendance.ts`. Extend [media-item.ts](frontend/src/types/media-item.ts) with `campEditionId`, `yearSource`, `commentCount`, `themes`, `mediaSourceId`, `mediaSourceName`, `sourcePathDisplay`, and add `campEditionId`, `themeIds`, `mediaSourceId`, `newSource` and `sourcePath` to `CreateMediaItemRequest`. Extend [memory.ts](frontend/src/types/memory.ts) with `campEditionId`.

### 4.5 UX rules

- All user-facing text in Spanish, per [frontend-standards.mdc](ai-specs/specs/frontend-standards.mdc) § Language Standards. Suggested strings: *"Yo estuve aquí"*, *"¿De qué año es esto?"*, *"Has estado en 14 campamentos"*, *"Sin ubicar"*, *"Este tema aparece entre 1981 y 2019"*, *"Denunciar comentario"*.
- **Never say "foto" in shared chrome.** Use *"recuerdo"* or *"contenido"* in anything that can hold an audio or a document: the unplaced pile, the dating panel, the comment composer.
- Album and theme grids are responsive: 1 column mobile, 2 tablet, 4 desktop. Lazy-load thumbnails (`loading="lazy"`).
- Audio items render an inline `<audio controls>` in the grid — the anniversary gallery already does this and the pattern should be reused, not reinvented.
- `MediaLightbox` is keyboard navigable (←/→ between items, Esc to close) and traps focus. The comment thread is a labelled region.
- Empty states matter — most albums start at zero. *"Este campamento aún no tiene recuerdos. ¿Tienes alguno?"* with a link to the upload form.
- Undated items show the current tally and theme hints inline, so a user sees the disagreement and the clues before voting.
- The dating panel hides the vote control and shows *"Fecha confirmada"* once `isResolved` and `yearSource === 'Admin'`.
- Provenance is shown as recognition, not metadata: *"Aportado por Manolo García"* under the item, linking to everything else they gave. The trimmed source path appears in the dating panel as a clue, labelled *"Venía en la carpeta: …/Verano 98/Selva de Oza"*, not as a technical field.
- **Never render `contributorContact` anywhere outside `MediaSourcesAdminPanel`.** The API already strips it, so a leak means someone widened the endpoint — but do not add a second path to it.
- The unplaced pile leads with its purpose, not an apology: *"Estos recuerdos aún no tienen campamento. Si reconoces alguno, ayúdanos a ubicarlo."*

### 4.6 Task 4 tests

Vitest component tests in `__tests__/` beside each component, following [AnniversaryGallery.test.ts](frontend/src/components/anniversary/__tests__/AnniversaryGallery.test.ts):

- `AlbumGrid.test.ts` — renders 50 cards, attended badge, empty counts
- `MediaCard.test.ts` — **renders the right control per type**: `img` for Photo, `audio` for Audio, `video` for Video, link for Document
- `AnniversaryUploadForm.test.ts` — extend: submitting with "No lo sé" selected posts `campEditionId: null` and succeeds
- `MediaCommentThread.test.ts` — posts a comment, optimistic insert, rollback on API error, edit/delete visibility driven by `canEdit`/`canDelete`
- `MediaDatingPanel.test.ts` — tally rendering, theme hints, vote submission, resolved state hides the control
- `MediaSourcePicker.test.ts` — "Mío" sends no source; "De otra persona" sends `newSource`; contact field hidden for a Member
- `SourceDetail.test.ts` — renders a contributor's items across several years; never renders a contact detail
- `ThemeGrid.test.ts` — year span and undated count rendering
- `AttendanceButton.test.ts` — toggle on/off, family-member selection
- Composable tests in `frontend/src/composables/__tests__/` mirroring `useMediaItems.test.ts`

### Task 4 Definition of Done

- [ ] All six routes work behind auth
- [ ] **Upload with "No lo sé" works and the item appears in Sin ubicar**
- [ ] **An album with a photo, an audio, a video and a relato renders all four**
- [ ] A theme page shows items from several different years in one view
- [ ] A contributor page shows everything one person gave, across editions
- [ ] `contributorContact` renders nowhere outside the admin panel
- [ ] `npm run build` (runs `vue-tsc --noEmit`), `npm run lint` and `npm run test:run` all green
- [ ] Responsive at 360 px, 768 px, 1280 px
- [ ] Keyboard navigation and focus trap verified in the lightbox
- [ ] All user-facing text in Spanish, no hard-coded English, no "foto" in type-agnostic chrome
- [ ] Existing `AnniversaryGallery` still works unchanged

---

## Non-functional requirements

**Performance**

- Album index (50 editions with counts and covers) must be a single grouped query — no N+1. Assert query count in the integration test.
- Theme catalogue counts and year spans likewise: one grouped query, not one per theme.
- Album and theme detail paginated at 24 items, `pageSize` capped at 100 server-side.
- `CommentCount` is denormalised precisely so grids never join comments.
- Thumbnails only in grids; full-size media loads in the lightbox only. Audio and video use `preload="none"`.
- The importer streams files one at a time — never loads a whole folder into memory.

**Security and privacy (RGPD)**

- Every endpoint requires authentication. No anonymous access, per decision 4.
- Comment bodies and theme names are user-generated: escape on render (Vue does this by default — do **not** use `v-html`).
- Rate limit comments at 10/min/user.
- Soft-deleted comments are excluded from all read paths, including the moderation queue detail.
- Deleting a `User` cascades to their comments, reports, proposals, theme tags and attendance rows — this is the "right to erasure" path and must be verified by test. A `MediaSource` whose `ContributorUserId` pointed at them keeps the row with the FK nulled, so donated material is not silently orphaned; the free-text name survives unless anonymisation is also requested.
- **`MediaSource` holds personal data about people who are not members and never signed anything.** This is the most delicate part of the feature and needs three things: (a) `ContributorName` is visible to members as attribution, which is the point, so the person handing over material must be told their name will appear — add that sentence to the donation runbook; (b) `ContributorContact` is Admin/Board only, enforced server-side; (c) erasure on request nullifies `ContributorName`, `ContributorContact` and `ContributorUserId` while keeping the media and the `MediaSource` row, so the archive survives but the person disappears from it. Add a `PATCH /api/media-sources/{id}/anonymise` (Admin) rather than making an admin edit three fields by hand and miss one.
- **`SourcePath` can itself carry personal data.** The importer already stores it relative to `--dir`, and members only ever see the last three segments. Do not add an endpoint that returns the raw column to non-Admin callers.
- `CampEditionAttendance` reveals which camps a person attended. It is visible only to authenticated members, matching the existing visibility of registrations.
- The importer runs against production blob storage: `--dry-run` first is mandatory in the runbook, and `SafetyGuard` conventions from the existing setup tool apply.

**Storage**

- A bulk import of thousands of files will move the needle on the blob quota, and video is far heavier than photos. Check `GET /api/blobs/stats` before and after; `BlobStorageOptions.StorageQuotaBytes` and the 80 %/95 % thresholds already exist and will warn.

---

## Deferred: Phase B — person tagging

**Not built in this feature.** Recorded so the data model above does not foreclose it.

The intended shape is `MediaItemPersonTag { Id, MediaItemId, RegionX, RegionY, RegionWidth, RegionHeight, UserId?, FamilyMemberId?, FreeText?, TaggedByUserId, ConfirmedAt?, CreatedAt }` with normalised 0–1 region coordinates.

Before it can be built, a written policy must answer:

1. Who may tag whom — anyone, or only within your own family unit until the tagged person confirms?
2. How a tag is removed, and whether the tagged person can remove it unilaterally (they must be able to).
3. **Minors.** `FamilyMember` already stores encrypted health data and dates of birth; tagging a child by name creates a new category of personal data about a minor. The likely answer is that tags on members under 18 are visible only within their own family unit.
4. Whether tags are searchable ("all photos with my mother") for everyone or only for the tagged person and their family.

Nothing in Tasks 1–4 blocks this. `MediaComment`, `MediaItemTheme` and `MediaItemPersonTag` are independent tables.

---

## Out of scope

- `PhotoAlbum` / `Photo` entities — superseded, see trap 2
- Dropping the dead `camp_location_id` columns on `media_items` and `memories` — separate cleanup ticket after verifying they are `NULL` in production
- Introducing the shared `PagedResult<T>` from the standards — separate refactor, see trap 4
- Themes on `Memory` — additive later, blocks nothing
- Community-proposed *new* themes (members attach existing themes; only Admin/Board create them)
- Automatic contributor de-duplication (fuzzy name matching). The merge operation is manual and admin-driven; suggesting likely duplicates is a later refinement
- Notifying a contributor by email that their material was published — no consent flow exists for non-members
- Admin-facing multi-file web upload — the CLI importer covers the historical backlog
- Face detection or any automated person recognition
- Public (unauthenticated) album sharing
- Automatic `Interview` classification in the importer — audio imports as `Audio`, an admin reclassifies

---

## Documentation to update

| Document | Task | What |
|----------|------|------|
| [data-model.md](ai-specs/specs/data-model.md) | 1 | Seven new entities; revise `MediaItem` and `Memory`; mark `PhotoAlbum`, `Photo`, `CampLocation` superseded; update ER diagram |
| [api-endpoints.md](ai-specs/specs/api-endpoints.md) | 2 | All new endpoints with examples and error codes |
| `src/Abuvi.Setup/schemas/README.md` | 3 | `import-media` command, flags, folder conventions, `media-themes.csv`, and a donation runbook: always pass `--source-name`, always `--dry-run` first, and tell the contributor their name will be shown |
| [INDEX.md](ai-specs/changes/INDEX.md) | 1 | Add `feat-photo-albums-social` entry with backend/frontend checkboxes |

---

## Suggested build order

1. **Task 1** — unblocks everything else
2. **Task 3 in parallel with Task 2** — the importer only needs the schema, and real content in the database early makes Task 4 far easier to build and review
3. **Task 2**
4. **Task 4**

Within Task 4, build in this order so each step is independently demoable: album index → album detail with all media types → upload form "No lo sé" and source picker → lightbox → comments → themes → contributor pages → attendance → dating → timeline map.

If the scope needs trimming, cut in this order: the timeline map (`CampTimelineMap`), then collaborative dating (2.5 + `MediaDatingPanel`). **Do not cut the upload-without-edition path, themes, or provenance capture** — the first fills the unplaced pile, the second makes it tractable, and the third records who to ask. Provenance in particular is cheap to write and impossible to reconstruct later: an undated photo whose donor was never recorded may simply never be dated. The contributor *pages* can be cut; the contributor *field* cannot. Keep attendance too: it is the cheapest piece and, per the original note, the highest-return one.

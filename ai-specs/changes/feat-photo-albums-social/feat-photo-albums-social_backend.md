# Backend Implementation Plan: feat-photo-albums-social — Camp Edition Albums, Themes and Collective Memory

## Overview

Implements the backend for camp-edition albums, cross-cutting themes, provenance tracking, photo/media comments, collaborative dating and attendance declaration, per [feat-photo-albums-social_enriched.md](./feat-photo-albums-social_enriched.md).

Covers **Task 1 (data model)** and **Task 2 (API)** of the enriched spec. Task 3 (the `Abuvi.Setup` bulk importer) is a different project on a different branch and has its own plan: [feat-photo-albums-social_setup-importer_backend.md](./feat-photo-albums-social_setup-importer_backend.md).

Architecture principles applied:

- **Vertical Slice Architecture** — four new slices (`MediaSources`, `MediaThemes`, `MediaComments`, `MediaDating`), plus extensions to the existing `MediaItems`, `Memories` and `Camps` slices. No shared "media service" layer.
- Each slice is self-contained: `[Feature]Endpoints.cs`, `[Feature]Models.cs`, `[Feature]Service.cs`, `[Feature]Repository.cs` + `I[Feature]Repository.cs`, `[Feature]Validator.cs`, `[Feature]Extensions.cs`.
- EF Core configurations live centrally in `src/Abuvi.API/Data/Configurations/` — this is the existing project convention, not per-slice.
- `ApiResponse<T>` envelope everywhere. Errors flow through `GlobalExceptionMiddleware`.

### Scale warning

This is a large ticket: 7 new entities, 2 extended entities, ~35 endpoints. **Cut a PR at the end of Step 6** (migration applied, no endpoints yet) — that is the Task 1 / Task 2 boundary from the enriched spec and it keeps the review tractable. The branch name below covers both halves; if you prefer two branches, use `feature/feat-photo-albums-social-backend-model` up to Step 6 and `feature/feat-photo-albums-social-backend-api` after.

---

## Architecture Context

### New feature slices

| Slice | Path | Contents |
|-------|------|----------|
| `MediaSources` | `src/Abuvi.API/Features/MediaSources/` | Provenance: who gave us the material |
| `MediaThemes` | `src/Abuvi.API/Features/MediaThemes/` | Cross-cutting themes + N:M tagging |
| `MediaComments` | `src/Abuvi.API/Features/MediaComments/` | Comments + reports/moderation |
| `MediaDating` | `src/Abuvi.API/Features/MediaDating/` | Year proposals + consensus |

### Modified existing slices

| Slice | Files | Change |
|-------|-------|--------|
| `MediaItems` | `MediaItemsModels.cs`, `MediaItemsRepository.cs`, `MediaItemsService.cs`, `MediaItemsEndpoints.cs`, `MediaItemsValidator.cs` | New fields, album queries, unplaced pile, upload-without-edition |
| `Memories` | `MemoriesModels.cs`, `MemoriesRepository.cs`, `MemoriesService.cs`, `MemoriesEndpoints.cs` | `CampEditionId` + filter |
| `Camps` | `CampsModels.cs`, `CampsEndpoints.cs` + new `CampEditionAttendance*.cs` | Attendance entity, service, repository, endpoints |

### Files to create

**Entities/DTOs and slice files** (24 files)

```
src/Abuvi.API/Features/MediaSources/MediaSourcesModels.cs
src/Abuvi.API/Features/MediaSources/MediaSourcesEndpoints.cs
src/Abuvi.API/Features/MediaSources/MediaSourcesService.cs
src/Abuvi.API/Features/MediaSources/IMediaSourcesRepository.cs
src/Abuvi.API/Features/MediaSources/MediaSourcesRepository.cs
src/Abuvi.API/Features/MediaSources/MediaSourcesValidator.cs
src/Abuvi.API/Features/MediaSources/MediaSourcesExtensions.cs
src/Abuvi.API/Features/MediaThemes/…                (same 7 files)
src/Abuvi.API/Features/MediaComments/…              (same 7 files)
src/Abuvi.API/Features/MediaDating/…                (same 7 files, minus Validator → 6)
src/Abuvi.API/Features/Camps/CampEditionAttendanceService.cs
src/Abuvi.API/Features/Camps/ICampEditionAttendanceRepository.cs
src/Abuvi.API/Features/Camps/CampEditionAttendanceRepository.cs
```

**EF configurations** (7 files)

```
src/Abuvi.API/Data/Configurations/MediaSourceConfiguration.cs
src/Abuvi.API/Data/Configurations/MediaThemeConfiguration.cs
src/Abuvi.API/Data/Configurations/MediaItemThemeConfiguration.cs
src/Abuvi.API/Data/Configurations/MediaCommentConfiguration.cs
src/Abuvi.API/Data/Configurations/MediaCommentReportConfiguration.cs
src/Abuvi.API/Data/Configurations/MediaItemYearProposalConfiguration.cs
src/Abuvi.API/Data/Configurations/CampEditionAttendanceConfiguration.cs
```

### Cross-cutting concerns affected

| Concern | Change |
|---------|--------|
| `src/Abuvi.API/Data/AbuviDbContext.cs` | 7 new `DbSet`s |
| `src/Abuvi.API/Program.cs` | 4 `Add*()` + 4 `Map*Endpoints()` calls, **plus a new rate limiter** |
| **Rate limiting** | **Does not exist in this project today.** Step 12 adds `AddRateLimiter` (built into ASP.NET Core 9 — no NuGet package) purely for the comment endpoint |
| `GlobalExceptionMiddleware` | No change. Reuse `NotFoundException` (404), `BusinessRuleException` (409), `ValidationException` (400) |
| 403 handling | **There is no `ForbiddenException`.** The project convention is `Results.Forbid()` at the endpoint after `user.IsInRole(...)`, as in [CampsEndpoints.cs:946](src/Abuvi.API/Features/Camps/CampsEndpoints.cs#L946). Follow it — do not invent an exception type |

### Read this before starting

Four traps documented in the enriched spec, repeated here because they cost real time:

1. `MediaItem.CampLocationId` and `Memory.CampLocationId` are **dead columns** — `CampLocation` was never implemented. Do not build on them, do not remove them in this ticket.
2. `PhotoAlbum` / `Photo` in [data-model.md](ai-specs/specs/data-model.md) were **never implemented**. An album here is a *query*, not an entity.
3. `PagedResult<T>` from [backend-standards.mdc](ai-specs/specs/backend-standards.mdc) **does not exist**. Use a feature-local paged record, following `AdminRegistrationListResponse` in Registrations.
4. Nothing here is photo-only. Name everything `Media*`, never `Photo*`.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the backend feature branch. Check whether it exists first.
- **Branch Naming**: `feature/feat-photo-albums-social-backend` — **required**. Do not work on a generic `feat-photo-albums-social` branch; backend and frontend must stay separate.
- **Implementation Steps**:
  1. `git checkout dev`
  2. `git pull origin dev` — **base branch is `dev`, not `main`.** PRs target `dev`; `main` is release-only.
  3. `git checkout -b feature/feat-photo-albums-social-backend`
  4. `git branch` to verify
- **Notes**: FIRST step, before any code change. See [backend-standards.mdc](ai-specs/specs/backend-standards.mdc) § Development Workflow.

---

### Step 1: Extend `MediaItem` Entity

- **File**: `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs`
- **Action**: Add provenance, placement, theme navigation and the comment counter.

**Implementation Steps**:

1. Add the new enum above the `MediaItem` class:

```csharp
public enum MediaItemYearSource
{
    Unknown,    // no year yet — eligible for collaborative dating
    Exif,       // EXIF DateTimeOriginal
    FolderName, // resolved from the import folder name
    Uploader,   // typed into the web upload form
    Community,  // set by collaborative dating consensus
    Admin       // set manually by Admin/Board — never overwritten by consensus
}
```

2. Add to `MediaItem`:

```csharp
    public Guid? CampEditionId { get; set; }
    public MediaItemYearSource YearSource { get; set; } = MediaItemYearSource.Unknown;
    public int CommentCount { get; set; }
    public Guid? MediaSourceId { get; set; }
    public string? SourcePath { get; set; }

    // Navigation
    public CampEdition? CampEdition { get; set; }
    public MediaSource? MediaSource { get; set; }
    public List<MediaItemTheme> Themes { get; set; } = [];
```

3. Add `using Abuvi.API.Features.MediaSources;` and `using Abuvi.API.Features.MediaThemes;`.

- **Implementation Notes**:
  - `CampEditionId == null` means **"edition unknown"** — always temporary, always resolvable. There is deliberately no "not camp related" state.
  - Leave `CampLocationId` in place, untouched (trap 1).
  - Do **not** extend `MediaItemResponse` yet — DTOs are Step 7.

---

### Step 2: Extend `Memory` Entity

- **File**: `src/Abuvi.API/Features/Memories/MemoriesModels.cs`
- **Action**: Let written stories attach to an edition, so `MemoryCount` on an album is computable.

**Implementation Steps**:

1. Add to `Memory`:

```csharp
    public Guid? CampEditionId { get; set; }
    public CampEdition? CampEdition { get; set; }
```

2. Add `CampEditionId` to `MemoryResponse` (after `CampLocationId`) and to the `ToResponse()` mapping.
3. Add `Guid? CampEditionId` to `CreateMemoryRequest` as the last parameter with a `= null` default, so existing callers keep compiling.

- **Implementation Notes**: `Memory` is deliberately **not** themed in this ticket. Adding `MemoryTheme` later is purely additive.

---

### Step 3: Create New Entities

#### 3a. `MediaSource` — provenance

- **File**: `src/Abuvi.API/Features/MediaSources/MediaSourcesModels.cs`

```csharp
using Abuvi.API.Features.Users;

namespace Abuvi.API.Features.MediaSources;

/// <summary>
/// Who provided a batch of historical material. Distinct from MediaItem.UploadedByUserId,
/// which records the account that performed the upload. The provider is frequently not a
/// registered user, which is why ContributorName is free text rather than a User FK.
/// One row per donation, not per file.
/// </summary>
public class MediaSource
{
    public Guid Id { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public Guid? ContributorUserId { get; set; }
    public string? ContributorContact { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid RegisteredByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User? ContributorUser { get; set; }
    public User RegisteredBy { get; set; } = null!;
}
```

#### 3b. `MediaTheme` and `MediaItemTheme` — cross-cutting themes

- **File**: `src/Abuvi.API/Features/MediaThemes/MediaThemesModels.cs`

```csharp
public class MediaTheme
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<MediaItemTheme> Items { get; set; } = [];
}

/// <summary>N:M join. Composite PK (MediaItemId, MediaThemeId) makes duplicate tagging impossible.</summary>
public class MediaItemTheme
{
    public Guid MediaItemId { get; set; }
    public Guid MediaThemeId { get; set; }
    public Guid TaggedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public MediaItem MediaItem { get; set; } = null!;
    public MediaTheme MediaTheme { get; set; } = null!;
    public User TaggedBy { get; set; } = null!;
}
```

#### 3c. `MediaComment` and `MediaCommentReport`

- **File**: `src/Abuvi.API/Features/MediaComments/MediaCommentsModels.cs`

```csharp
public class MediaComment
{
    public Guid Id { get; set; }
    public Guid MediaItemId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }        // soft delete, mirrors FamilyMember.DeletedAt
    public Guid? DeletedByUserId { get; set; }

    public MediaItem MediaItem { get; set; } = null!;
    public User Author { get; set; } = null!;
}

public enum MediaCommentReportReason { Offensive, PrivacyConcern, Incorrect, Other }
public enum MediaCommentReportStatus { Pending, Actioned, Dismissed }

public class MediaCommentReport
{
    public Guid Id { get; set; }
    public Guid MediaCommentId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public MediaCommentReportReason Reason { get; set; }
    public string? Notes { get; set; }
    public MediaCommentReportStatus Status { get; set; } = MediaCommentReportStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public MediaComment MediaComment { get; set; } = null!;
    public User ReportedBy { get; set; } = null!;
}
```

#### 3d. `MediaItemYearProposal`

- **File**: `src/Abuvi.API/Features/MediaDating/MediaDatingModels.cs`

```csharp
public class MediaItemYearProposal
{
    public Guid Id { get; set; }
    public Guid MediaItemId { get; set; }
    public Guid ProposedByUserId { get; set; }
    public int ProposedYear { get; set; }
    public Guid? ProposedCampEditionId { get; set; }
    public string? Rationale { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MediaItem MediaItem { get; set; } = null!;
    public User ProposedBy { get; set; } = null!;
    public CampEdition? ProposedCampEdition { get; set; }
}
```

#### 3e. `CampEditionAttendance`

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs` (append — it is edition-scoped, so it belongs in the Camps slice)

```csharp
/// <summary>
/// A member declaring "I was at this camp", optionally on behalf of a family member.
/// Attendance is ALSO derived from Registration for recent editions; derived rows are
/// never persisted here.
/// </summary>
public class CampEditionAttendance
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public Guid UserId { get; set; }
    public Guid? FamilyMemberId { get; set; }   // null = the declarer themselves
    public DateTime CreatedAt { get; set; }

    public CampEdition CampEdition { get; set; } = null!;
    public User User { get; set; } = null!;
    public FamilyMember? FamilyMember { get; set; }
}
```

---

### Step 4: EF Core Configurations

- **Files**: 7 new in `src/Abuvi.API/Data/Configurations/`, plus edits to `MediaItemConfiguration.cs` and `MemoryConfiguration.cs`.
- **Action**: snake_case tables and columns, enums via `HasConversion<string>()`, timestamps via `HasDefaultValueSql("NOW()")` — match [MediaItemConfiguration.cs](src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs) exactly.

#### 4a. Update `MediaItemConfiguration.cs`

```csharp
builder.Property(m => m.CampEditionId).HasColumnName("camp_edition_id");

builder.Property(m => m.YearSource)
    .IsRequired()
    .HasConversion<string>()
    .HasMaxLength(20)
    .HasDefaultValue(MediaItemYearSource.Unknown)
    .HasColumnName("year_source");

builder.Property(m => m.CommentCount)
    .IsRequired().HasDefaultValue(0).HasColumnName("comment_count");

builder.Property(m => m.MediaSourceId).HasColumnName("media_source_id");

builder.Property(m => m.SourcePath)
    .HasMaxLength(1024).HasColumnName("source_path");

builder.HasOne(m => m.CampEdition)
    .WithMany()
    .HasForeignKey(m => m.CampEditionId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasOne(m => m.MediaSource)
    .WithMany()
    .HasForeignKey(m => m.MediaSourceId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasIndex(m => m.CampEditionId)
    .HasDatabaseName("ix_media_items_camp_edition_id");

builder.HasIndex(m => new { m.CampEditionId, m.IsApproved, m.IsPublished })
    .HasDatabaseName("ix_media_items_edition_approved_published");

builder.HasIndex(m => m.MediaSourceId)
    .HasDatabaseName("ix_media_items_media_source_id");
```

#### 4b. Update `MemoryConfiguration.cs`

```csharp
builder.Property(m => m.CampEditionId).HasColumnName("camp_edition_id");
builder.HasOne(m => m.CampEdition).WithMany()
    .HasForeignKey(m => m.CampEditionId).OnDelete(DeleteBehavior.SetNull);
builder.HasIndex(m => m.CampEditionId).HasDatabaseName("ix_memories_camp_edition_id");
```

#### 4c. `MediaSourceConfiguration.cs`

Table `media_sources`. `ContributorName` required max 200; `ContributorContact` max 200; `Notes` max 1000. FKs: `ContributorUser` → `SetNull`, `RegisteredBy` → `Cascade`. Indexes on `contributor_user_id` and `contributor_name`.

#### 4d. `MediaThemeConfiguration.cs`

Table `media_themes`. `Name` required max 100. `Slug` required max 100 with **unique** index `ux_media_themes_slug`. `IsActive` default `true`.

#### 4e. `MediaItemThemeConfiguration.cs`

Table `media_item_themes`.

```csharp
builder.HasKey(t => new { t.MediaItemId, t.MediaThemeId });

builder.HasOne(t => t.MediaItem).WithMany(m => m.Themes)
    .HasForeignKey(t => t.MediaItemId).OnDelete(DeleteBehavior.Cascade);
builder.HasOne(t => t.MediaTheme).WithMany(th => th.Items)
    .HasForeignKey(t => t.MediaThemeId).OnDelete(DeleteBehavior.Cascade);
builder.HasOne(t => t.TaggedBy).WithMany()
    .HasForeignKey(t => t.TaggedByUserId).OnDelete(DeleteBehavior.Cascade);

builder.HasIndex(t => t.MediaThemeId).HasDatabaseName("ix_media_item_themes_theme_id");
```

#### 4f. `MediaCommentConfiguration.cs`

Table `media_comments`. `Body` required max 1000. Cascade on both FKs. Indexes: `(media_item_id, created_at)` as `ix_media_comments_item_created`, and `author_user_id`.

#### 4g. `MediaCommentReportConfiguration.cs`

Table `media_comment_reports`. Enums as strings max 20. **Unique** index `(media_comment_id, reported_by_user_id)` named `ux_media_comment_reports_comment_reporter`. Index on `status`.

#### 4h. `MediaItemYearProposalConfiguration.cs`

Table `media_item_year_proposals`. `Rationale` max 500. **Unique** index `(media_item_id, proposed_by_user_id)` named `ux_media_item_year_proposals_item_user`. Index on `media_item_id`. `ProposedCampEdition` FK → `SetNull`.

#### 4i. `CampEditionAttendanceConfiguration.cs`

Table `camp_edition_attendances`. All FKs Cascade. Unique index `(camp_edition_id, user_id, family_member_id)`; index on `user_id`.

> The partial unique index for self-declarations cannot be expressed here — it goes in the migration as raw SQL (Step 6).

---

### Step 5: Update `AbuviDbContext.cs`

- **File**: `src/Abuvi.API/Data/AbuviDbContext.cs`

```csharp
public DbSet<MediaSource> MediaSources => Set<MediaSource>();
public DbSet<MediaTheme> MediaThemes => Set<MediaTheme>();
public DbSet<MediaItemTheme> MediaItemThemes => Set<MediaItemTheme>();
public DbSet<MediaComment> MediaComments => Set<MediaComment>();
public DbSet<MediaCommentReport> MediaCommentReports => Set<MediaCommentReport>();
public DbSet<MediaItemYearProposal> MediaItemYearProposals => Set<MediaItemYearProposal>();
public DbSet<CampEditionAttendance> CampEditionAttendances => Set<CampEditionAttendance>();
```

Add the matching `using` statements. Configurations are picked up by `ApplyConfigurationsFromAssembly` if that is how the context registers them — verify, and register explicitly if not.

---

### Step 6: Create EF Core Migration

- **Command**: `dotnet ef migrations add AddCampAlbumsThemesAndSocial --project src/Abuvi.API`
- **Action**: Review the generated file, then hand-add three raw SQL blocks.

**Implementation Steps**:

1. **Backfill `media_items.camp_edition_id`** from `year` — exactly one edition exists per historical year.
   **Do not reach for `MIN(id)`: PostgreSQL has no `min()` aggregate for `uuid`.** The correlated
   `COUNT(*) = 1` says "only when that year is unambiguous" and needs no aggregate:

```csharp
migrationBuilder.Sql(@"
    UPDATE media_items m
    SET camp_edition_id = e.id, year_source = 'Uploader'
    FROM camp_editions e
    WHERE m.year = e.year
      AND m.camp_edition_id IS NULL
      AND (SELECT COUNT(*) FROM camp_editions e2 WHERE e2.year = e.year) = 1;
");
```

2. **Backfill `memories.camp_edition_id`** with the same shape (no `year_source` column on memories).

3. **Partial index for the unplaced pile**:

```csharp
migrationBuilder.Sql(@"
    CREATE INDEX ix_media_items_unplaced
    ON media_items (created_at DESC)
    WHERE camp_edition_id IS NULL;
");
```

4. **Partial unique index for self-declared attendance** — a `NULL` does not collide in a composite unique index, so `(edition, user, NULL)` could otherwise be inserted twice:

```csharp
migrationBuilder.Sql(@"
    CREATE UNIQUE INDEX ux_camp_edition_attendances_self
    ON camp_edition_attendances (camp_edition_id, user_id)
    WHERE family_member_id IS NULL;
");
```

5. Add matching `DROP INDEX` statements to `Down()`.
6. Apply: `dotnet ef database update --project src/Abuvi.API`
7. Verify the backfill against seeded data: existing anniversary media with a year must now have an edition.

- **Notes**: **This is the PR cut point for the Task 1 half.**

---

### Step 7: Create Request/Response DTOs

All DTOs are `record` types in the respective `*Models.cs`.

#### 7a. `MediaItems` — upload without an edition

- **File**: `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs`

```csharp
public record CreateMediaItemRequest(
    string FileUrl,
    string? ThumbnailUrl,
    MediaItemType Type,
    string Title,
    string? Description,
    int? Year,
    Guid? MemoryId,
    Guid? CampLocationId,                       // DEPRECATED, dead column — service ignores it
    string? Context,
    Guid? AccommodationId = null,
    Guid? ZoneId = null,
    Guid? CampEditionId = null,                 // null = "I don't know" — VALID
    IReadOnlyList<Guid>? ThemeIds = null,
    Guid? MediaSourceId = null,
    NewMediaSourceRequest? NewSource = null,
    string? SourcePath = null);

public record NewMediaSourceRequest(
    string ContributorName,
    Guid? ContributorUserId,
    string? ContributorContact,
    string? Notes,
    DateTime? ReceivedAt);
```

Extend `MediaItemResponse` with `CampEditionId`, `YearSource` (string), `CommentCount`, `MediaSourceId`, `MediaSourceName`, `SourcePathDisplay`, `Themes`.

#### 7b. Album DTOs

```csharp
public record AlbumSummaryResponse(
    Guid CampEditionId, int Year, Guid CampId, string CampName, string? CampLocality,
    decimal? Latitude, decimal? Longitude,
    int PhotoCount, int VideoCount, int AudioCount, int DocumentCount, int MemoryCount,
    string? CoverThumbnailUrl, bool ViewerAttended);

public record AlbumMediaItemResponse(
    Guid Id, Guid UploadedByUserId, string UploadedByName,
    string FileUrl, string? ThumbnailUrl, string Type, string Title, string? Description,
    int? Year, string? Decade, Guid? CampEditionId, string YearSource,
    int CommentCount, Guid? MediaSourceId, string? MediaSourceName, string? SourcePathDisplay,
    IReadOnlyList<MediaThemeSummaryResponse> Themes,
    bool IsApproved, bool IsPublished, int DisplayOrder, bool IsPrimary, DateTime CreatedAt);

public record AlbumDetailResponse(
    AlbumSummaryResponse Edition,
    IReadOnlyList<AlbumMediaItemResponse> Items,
    int TotalCount, int Page, int PageSize);
```

> Feature-local paged shape, following `AdminRegistrationListResponse`. Do **not** create `PagedResult<T>` (trap 3).

#### 7c. `MediaSources` DTOs

```csharp
public record CreateMediaSourceRequest(
    string ContributorName, Guid? ContributorUserId,
    string? ContributorContact, string? Notes, DateTime? ReceivedAt);

public record UpdateMediaSourceRequest(
    string ContributorName, Guid? ContributorUserId,
    string? ContributorContact, string? Notes, DateTime? ReceivedAt);

public record MergeMediaSourceRequest(Guid TargetId);

public record MediaSourceResponse(
    Guid Id, string ContributorName, Guid? ContributorUserId,
    string? ContributorContact,          // null unless caller is Admin/Board
    string? Notes, DateTime? ReceivedAt,
    Guid RegisteredByUserId, string RegisteredByName,
    int ItemCount, int UndatedItemCount, int? FirstYear, int? LastYear,
    DateTime CreatedAt);
```

#### 7d. `MediaThemes` DTOs

```csharp
public record CreateMediaThemeRequest(string Name, string? Description);
public record UpdateMediaThemeRequest(string Name, string? Description, bool IsActive);
public record AttachThemeRequest(Guid ThemeId);

public record MediaThemeSummaryResponse(
    Guid Id, string Name, string Slug, string? Description,
    int ItemCount, int? FirstYear, int? LastYear, int UndatedCount);

public record ThemeItemsResponse(
    MediaThemeSummaryResponse Theme,
    IReadOnlyList<AlbumMediaItemResponse> Items,
    int TotalCount, int Page, int PageSize);
```

#### 7e. `MediaComments` DTOs

```csharp
public record CreateMediaCommentRequest(string Body);
public record UpdateMediaCommentRequest(string Body);
public record ReportMediaCommentRequest(MediaCommentReportReason Reason, string? Notes);
public record ReviewReportRequest(MediaCommentReportStatus Status);

public record MediaCommentResponse(
    Guid Id, Guid MediaItemId, Guid AuthorUserId, string AuthorName, string Body,
    bool CanEdit, bool CanDelete, bool ViewerReported,
    DateTime CreatedAt, DateTime UpdatedAt);

public record MediaCommentReportResponse(
    Guid Id, Guid MediaCommentId, string CommentBody, Guid MediaItemId,
    Guid ReportedByUserId, string ReportedByName,
    string Reason, string? Notes, string Status,
    DateTime CreatedAt, DateTime? ReviewedAt);
```

#### 7f. `MediaDating` DTOs

```csharp
public record UpsertYearProposalRequest(
    int ProposedYear, Guid? ProposedCampEditionId, string? Rationale);

public record SetYearRequest(int Year, Guid? CampEditionId);   // Admin override

public record YearProposalResponse(
    Guid Id, Guid ProposedByUserId, string ProposedByName,
    int ProposedYear, Guid? ProposedCampEditionId, string? Rationale, DateTime CreatedAt);

public record YearProposalGroupResponse(
    int Year, Guid? CampEditionId, string? CampName,
    int Count, IReadOnlyList<string> ProposerNames);          // capped at 5

public record ThemeYearHintResponse(
    Guid ThemeId, string ThemeName, IReadOnlyList<int> YearsWithDatedItems);

public record SourceHintResponse(
    Guid? MediaSourceId, string? ContributorName, Guid? ContributorUserId,
    IReadOnlyList<int> YearsFromSameSource, string? SourcePathDisplay);

public record YearProposalTallyResponse(
    Guid MediaItemId, int? ResolvedYear, string YearSource, bool IsResolved,
    IReadOnlyList<YearProposalGroupResponse> Groups,
    YearProposalResponse? ViewerProposal,
    IReadOnlyList<ThemeYearHintResponse> ThemeHints,
    SourceHintResponse? SourceHint);
```

#### 7g. Attendance DTOs

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`

```csharp
public record DeclareAttendanceRequest(Guid? FamilyMemberId);

public record AttendanceEntryResponse(
    Guid CampEditionId, Guid UserId, string UserName,
    Guid? FamilyMemberId, string? FamilyMemberName,
    string Source);   // "Declared" | "Registration"

public record CampTimelineEntryResponse(
    Guid CampEditionId, int Year, string CampName,
    decimal? Latitude, decimal? Longitude,
    bool Attended, string AttendanceSource, int MediaCount);

public record CampTimelineResponse(
    int TotalEditionsAttended, IReadOnlyList<CampTimelineEntryResponse> Entries);
```

---

### Step 8: Create FluentValidation Validators

Spanish messages, per [backend-standards.mdc](ai-specs/specs/backend-standards.mdc) § Validation Messages.

| Validator | File | Rules |
|-----------|------|-------|
| `CreateMediaSourceRequestValidator` | `MediaSources/MediaSourcesValidator.cs` | `ContributorName` NotEmpty, ≤200 → `"El nombre no puede superar los 200 caracteres"`; `ContributorContact` ≤200; `Notes` ≤1000 |
| `UpdateMediaSourceRequestValidator` | same | as above |
| `CreateMediaThemeRequestValidator` | `MediaThemes/MediaThemesValidator.cs` | `Name` NotEmpty ≤100; `Description` ≤500 |
| `CreateMediaCommentRequestValidator` | `MediaComments/MediaCommentsValidator.cs` | `Body` NotEmpty → `"El comentario no puede estar vacío"`; ≤1000 → `"El comentario no puede superar los 1000 caracteres"` |
| `ReportMediaCommentRequestValidator` | same | `Reason` IsInEnum; `Notes` ≤500 |
| `UpsertYearProposalRequestValidator` | `MediaDating/MediaDatingValidator.cs` | `ProposedYear` InclusiveBetween(1975, DateTime.UtcNow.Year) → `"El año debe estar entre 1975 y el año actual"`; `Rationale` ≤500 |

**Extend `MediaItemsValidator.cs`**: add `Themes`/source rules **without** adding any rule that requires `CampEditionId` or `Year`.

> **Critical:** no validator may make an edition or a year mandatory. Uploading unplaced material is the flow that feeds collaborative dating.

Register validators the way the project already does — `ValidationFilter<T>` on the endpoint plus assembly scanning in `Program.cs`.

---

### Step 9: Implement Repositories

Each slice gets `I[Feature]Repository.cs` + `[Feature]Repository.cs`, constructor-injected `AbuviDbContext`, `AsNoTracking()` on reads.

#### 9a. `IMediaSourcesRepository`

```csharp
Task<MediaSource?> GetByIdAsync(Guid id, CancellationToken ct);
Task<IReadOnlyList<MediaSource>> GetAllAsync(CancellationToken ct);
Task<IReadOnlyDictionary<Guid, MediaSourceStats>> GetStatsAsync(
    IReadOnlyList<Guid> sourceIds, CancellationToken ct);
Task AddAsync(MediaSource source, CancellationToken ct);
Task UpdateAsync(MediaSource source, CancellationToken ct);
Task<int> RepointItemsAsync(Guid fromSourceId, Guid toSourceId, CancellationToken ct);
Task DeleteAsync(Guid id, CancellationToken ct);
```

`MediaSourceStats` is an internal record `(int ItemCount, int UndatedItemCount, int? FirstYear, int? LastYear)`.

**`GetStatsAsync` must be one grouped query** over `media_items` for all requested source ids — not one query per source:

```csharp
var stats = await db.MediaItems.AsNoTracking()
    .Where(m => m.MediaSourceId != null && sourceIds.Contains(m.MediaSourceId.Value))
    .GroupBy(m => m.MediaSourceId!.Value)
    .Select(g => new {
        SourceId = g.Key,
        ItemCount = g.Count(),
        UndatedItemCount = g.Count(m => m.CampEditionId == null),
        FirstYear = g.Min(m => m.Year),
        LastYear  = g.Max(m => m.Year)
    })
    .ToListAsync(ct);
```

**`RepointItemsAsync`** is the merge primitive — use `ExecuteUpdateAsync` so 800 rows do not load into memory:

```csharp
return await db.MediaItems
    .Where(m => m.MediaSourceId == fromSourceId)
    .ExecuteUpdateAsync(s => s.SetProperty(m => m.MediaSourceId, toSourceId), ct);
```

#### 9b. `IMediaThemesRepository`

```csharp
Task<MediaTheme?> GetByIdAsync(Guid id, CancellationToken ct);
Task<MediaTheme?> GetBySlugAsync(string slug, CancellationToken ct);
Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
Task<IReadOnlyList<MediaTheme>> GetAllAsync(bool includeInactive, CancellationToken ct);
Task<IReadOnlyDictionary<Guid, ThemeStats>> GetStatsAsync(CancellationToken ct);
Task<IReadOnlyList<MediaItemTheme>> GetThemesForItemsAsync(
    IReadOnlyList<Guid> mediaItemIds, CancellationToken ct);
Task AttachAsync(MediaItemTheme tag, CancellationToken ct);
Task DetachAsync(Guid mediaItemId, Guid themeId, CancellationToken ct);
Task AddAsync(MediaTheme theme, CancellationToken ct);
Task UpdateAsync(MediaTheme theme, CancellationToken ct);
Task DeleteAsync(Guid id, CancellationToken ct);
```

`GetThemesForItemsAsync` is the **N+1 killer** for album and theme grids: fetch every tag for a page of items in one query, then map in memory.

#### 9c. `IMediaCommentsRepository`

```csharp
Task<MediaComment?> GetByIdAsync(Guid id, CancellationToken ct);
Task<IReadOnlyList<MediaComment>> GetThreadAsync(Guid mediaItemId, CancellationToken ct);
Task<IReadOnlyList<Guid>> GetReportedCommentIdsForUserAsync(
    Guid userId, IReadOnlyList<Guid> commentIds, CancellationToken ct);
Task AddAsync(MediaComment comment, CancellationToken ct);
Task UpdateAsync(MediaComment comment, CancellationToken ct);
Task<bool> ReportExistsAsync(Guid commentId, Guid userId, CancellationToken ct);
Task AddReportAsync(MediaCommentReport report, CancellationToken ct);
Task<MediaCommentReport?> GetReportByIdAsync(Guid id, CancellationToken ct);
Task<IReadOnlyList<MediaCommentReport>> GetReportsAsync(
    MediaCommentReportStatus? status, CancellationToken ct);
```

`GetThreadAsync` filters `DeletedAt == null`, orders by `CreatedAt` ascending, includes `Author`.

#### 9d. `IMediaDatingRepository`

```csharp
Task<MediaItemYearProposal?> GetByItemAndUserAsync(Guid mediaItemId, Guid userId, CancellationToken ct);
Task<IReadOnlyList<MediaItemYearProposal>> GetForItemAsync(Guid mediaItemId, CancellationToken ct);
Task AddAsync(MediaItemYearProposal p, CancellationToken ct);
Task UpdateAsync(MediaItemYearProposal p, CancellationToken ct);
Task DeleteAsync(Guid id, CancellationToken ct);
Task<IReadOnlyList<int>> GetYearsForThemeAsync(Guid themeId, CancellationToken ct);
Task<IReadOnlyList<int>> GetYearsForSourceAsync(Guid sourceId, CancellationToken ct);
```

#### 9e. `ICampEditionAttendanceRepository`

```csharp
Task<CampEditionAttendance?> GetAsync(Guid editionId, Guid userId, Guid? familyMemberId, CancellationToken ct);
Task<IReadOnlyList<CampEditionAttendance>> GetDeclaredForEditionAsync(Guid editionId, CancellationToken ct);
Task<IReadOnlyList<Guid>> GetDeclaredEditionIdsForUserAsync(Guid userId, CancellationToken ct);
Task<IReadOnlyList<Guid>> GetRegisteredEditionIdsForUserAsync(Guid userId, CancellationToken ct);
Task AddAsync(CampEditionAttendance a, CancellationToken ct);
Task DeleteAsync(Guid id, CancellationToken ct);
```

`GetRegisteredEditionIdsForUserAsync` derives attendance from `Registration` joined to the user's `FamilyUnit` — read-only, never persisted.

#### 9f. Extend `IMediaItemsRepository`

```csharp
Task<IReadOnlyList<AlbumCountRow>> GetAlbumCountsAsync(CancellationToken ct);
Task<IReadOnlyList<MediaItem>> GetCoversAsync(CancellationToken ct);
Task<(IReadOnlyList<MediaItem> Items, int Total)> GetAlbumPageAsync(
    Guid editionId, int page, int pageSize, MediaItemType? type, Guid? themeId,
    bool includeUnapproved, CancellationToken ct);
Task<(IReadOnlyList<MediaItem> Items, int Total)> GetUnplacedPageAsync(
    int page, int pageSize, MediaItemType? type, Guid? mediaSourceId,
    IReadOnlyList<Guid>? suggestedEditionIds, CancellationToken ct);
```

---

### Step 10: Implement Services

#### 10a. `MediaSourcesService`

- **File**: `src/Abuvi.API/Features/MediaSources/MediaSourcesService.cs`

**Two rules carry real risk. Implement them in the mapper, once.**

```csharp
public class MediaSourcesService(
    IMediaSourcesRepository repository,
    IUsersRepository usersRepository)
{
    private const int MemberVisiblePathSegments = 3;

    /// <summary>
    /// Contributor contact details belong to people who are often not members and never
    /// agreed to be listed. Strip them for anyone below Admin/Board — server-side, because
    /// the frontend is not a security boundary.
    /// </summary>
    private static string? VisibleContact(MediaSource s, bool isAdminOrBoard)
        => isAdminOrBoard ? s.ContributorContact : null;

    /// <summary>
    /// Raw paths leak: "D:/Users/maria.carmen.lopez/Fotos privadas/..." names a person and
    /// their directory layout. Members see only the trailing segments, which is where the
    /// camp clues live. Admin/Board see the full value. Never trim in the database — the
    /// full path is evidence.
    /// </summary>
    public static string? TrimSourcePath(string? path, bool isAdminOrBoard)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (isAdminOrBoard) return path;

        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length <= MemberVisiblePathSegments
            ? string.Join('/', segments)
            : ".../" + string.Join('/', segments[^MemberVisiblePathSegments..]);
    }
}
```

**Methods**:

| Method | Behaviour |
|--------|-----------|
| `GetListAsync(bool isAdminOrBoard, ct)` | All sources + one grouped stats query. Contact stripped per role |
| `GetByIdAsync(id, isAdminOrBoard, ct)` | `NotFoundException` if missing |
| `CreateAsync(userId, request, ct)` | `RegisteredByUserId = userId`. Validate `ContributorUserId` exists if supplied |
| `UpdateAsync(id, userId, isAdminOrBoard, request, ct)` | Allowed if `isAdminOrBoard` **or** `RegisteredByUserId == userId`. Otherwise the endpoint returns `Results.Forbid()` — the service returns a bool or throws, the endpoint maps it |
| `MergeAsync(sourceId, targetId, ct)` | See below |
| `DeleteAsync(id, ct)` | `MediaSourceId` becomes `NULL` via the `SetNull` FK; media survives |
| `AnonymiseAsync(id, ct)` | RGPD erasure: null `ContributorName` (set to `"(anónimo)"`), `ContributorContact`, `ContributorUserId`. Keeps the row and the media |

**`MergeAsync`** — one transaction, both steps or neither:

```csharp
public async Task<int> MergeAsync(Guid sourceId, Guid targetId, CancellationToken ct)
{
    if (sourceId == targetId)
        throw new ValidationException("No se puede fusionar un aportante consigo mismo");

    var source = await repository.GetByIdAsync(sourceId, ct)
        ?? throw new NotFoundException("aportante", sourceId);
    _ = await repository.GetByIdAsync(targetId, ct)
        ?? throw new NotFoundException("aportante", targetId);

    await using var tx = await db.Database.BeginTransactionAsync(ct);
    var moved = await repository.RepointItemsAsync(sourceId, targetId, ct);
    await repository.DeleteAsync(sourceId, ct);
    await tx.CommitAsync(ct);
    return moved;
}
```

#### 10b. `MediaThemesService`

**Slug generation** — deterministic, accent-stripping, collision-suffixed:

```csharp
public static string Slugify(string name)
{
    var normalized = name.Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder();
    foreach (var c in normalized)
        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            sb.Append(c);

    var slug = Regex.Replace(sb.ToString().ToLowerInvariant(), @"[^a-z0-9]+", "-")
                    .Trim('-');
    return slug.Length == 0 ? "tema" : slug;
}
```

On create: compute the slug, then loop `slug`, `slug-2`, `slug-3` … until `SlugExistsAsync` returns false. Unit-test *"San Abuvino"* → `san-abuvino` and the collision path.

**Other methods**: `GetCatalogueAsync` (one grouped stats query), `GetItemsBySlugAsync` (paged, `?year=`, `?campEditionId=`, `?undatedOnly=`, `?type=`; ordered by `Year` descending, **undated last**), `AttachAsync`, `DetachAsync`.

`AttachAsync` is idempotent — the composite PK makes a duplicate a no-op, so catch the unique violation or check first. `DetachAsync` is allowed for the tagger or Admin/Board.

#### 10c. `MediaItemsService` — extensions

**`CreateAsync` gains provenance and placement resolution:**

```csharp
// 1. Provenance — mutually exclusive
if (request.MediaSourceId is not null && request.NewSource is not null)
    throw new ValidationException(
        "Indica un aportante existente o crea uno nuevo, pero no ambos");

Guid? sourceId = request.MediaSourceId;
if (request.NewSource is { } ns)
    sourceId = await mediaSourcesService.CreateAsync(userId, ns.ToCreateRequest(), ct);

// 2. Placement — every branch is valid, none is an error
Guid? editionId = request.CampEditionId;
var yearSource = MediaItemYearSource.Unknown;
int? year = request.Year;

if (editionId is not null)
{
    var edition = await campEditionsRepository.GetByIdAsync(editionId.Value, ct)
        ?? throw new NotFoundException("edición", editionId.Value);
    year ??= edition.Year;
    yearSource = MediaItemYearSource.Uploader;
}
else if (year is not null)
{
    // exactly one edition per historical year — resolve when unambiguous
    var candidates = await campEditionsRepository.GetByYearAsync(year.Value, ct);
    if (candidates.Count == 1) editionId = candidates[0].Id;
    yearSource = MediaItemYearSource.Uploader;
}
// else: editionId stays null, yearSource stays Unknown — the item goes to the
// unplaced pile and becomes eligible for collaborative dating. THIS IS VALID.

item.CampEditionId = editionId;
item.Year = year;
item.Decade = MediaItemMappingExtensions.DeriveDecade(year);
item.YearSource = yearSource;
item.MediaSourceId = sourceId;
item.SourcePath = request.SourcePath;

// 3. Themes — unknown or inactive ids are ignored, never fatal
if (request.ThemeIds is { Count: > 0 })
    await themesService.AttachManyIgnoringUnknownAsync(item.Id, request.ThemeIds, userId, ct);
```

**`GetAlbumSummariesAsync(viewerUserId, ct)`** — the N+1 risk. Use a **constant number of queries regardless of edition count**, not one per edition:

1. Editions + camp (one query, `Include(e => e.Camp)` or a projection join)
2. Media counts grouped by `(camp_edition_id, type)` (one query)
3. Memory counts grouped by `camp_edition_id` (one query)
4. Covers: `IsPrimary` first, else most recent approved photo per edition (one query)
5. Viewer attendance ids: declared ∪ derived (two queries)

Then assemble in memory. **Six queries total, whether there are 50 editions or 500.** The integration test asserts this.

**`GetAlbumPageAsync`** — non-admin callers get `IsApproved && IsPublished` only; Admin/Board also see pending, flagged. `pageSize` clamped to 100 server-side.

**`GetUnplacedAsync`** — `CampEditionId == null`, supports `?mediaSourceId=`, `?type=`, and `?suggestedForMe=true`, which restricts to editions the caller attended plus everything the caller contributed.

#### 10d. `MediaCommentsService`

```csharp
public class MediaCommentsService(
    IMediaCommentsRepository repository,
    IMediaItemsRepository mediaItemsRepository,
    AbuviDbContext db)
{
    private const int EditWindowMinutes = 15;

    private static bool IsWithinEditWindow(MediaComment c)
        => DateTime.UtcNow - c.CreatedAt < TimeSpan.FromMinutes(EditWindowMinutes);
}
```

**`CreateAsync`**:

1. Load the `MediaItem`; `NotFoundException` if missing.
2. If `!item.IsApproved` and the caller is not Admin/Board → the endpoint returns `Results.Forbid()`. Surface this from the service as a distinct result, not a generic exception.
3. Insert the comment **and** increment `MediaItem.CommentCount` in one transaction.

**`UpdateAsync` / `DeleteAsync`**: allowed if `AuthorUserId == userId && IsWithinEditWindow(c)`, or if the caller is Admin/Board (delete only). Delete is **soft** — set `DeletedAt` and `DeletedByUserId`, decrement `CommentCount`, same transaction.

**`ReportAsync`**: `ReportExistsAsync` first → `BusinessRuleException` ("Ya has denunciado este comentario") → 409 via the middleware.

**Mapping**: `CanEdit` = viewer is author AND in window. `CanDelete` = that OR Admin/Board. `ViewerReported` from one batched `GetReportedCommentIdsForUserAsync` per thread, never per comment.

#### 10e. `MediaDatingService` — consensus

This is the algorithm with the most edge cases. Implement it as one private method called after every insert, update and withdrawal, inside the same transaction.

```csharp
private const int MinProposalsForConsensus = 3;
private const double ConsensusRatio = 0.66;

private async Task EvaluateConsensusAsync(MediaItem item, CancellationToken ct)
{
    // 1. A manual admin decision is never overwritten by the community.
    if (item.YearSource == MediaItemYearSource.Admin) return;

    var proposals = await repository.GetForItemAsync(item.Id, ct);
    if (proposals.Count == 0)
    {
        // withdrawal can un-resolve a previously community-dated item
        if (item.YearSource == MediaItemYearSource.Community)
        {
            item.Year = null; item.Decade = null;
            item.CampEditionId = null;
            item.YearSource = MediaItemYearSource.Unknown;
        }
        return;
    }

    var groups = proposals.GroupBy(p => p.ProposedYear)
                          .OrderByDescending(g => g.Count())
                          .ToList();
    var top = groups[0];

    if (top.Count() >= MinProposalsForConsensus &&
        (double)top.Count() / proposals.Count >= ConsensusRatio)
    {
        item.Year = top.Key;
        item.Decade = MediaItemMappingExtensions.DeriveDecade(top.Key);
        item.YearSource = MediaItemYearSource.Community;

        // most-proposed non-null edition within the winning year group,
        // else the unique edition for that year, else leave unchanged
        var editionId = top.Where(p => p.ProposedCampEditionId is not null)
                           .GroupBy(p => p.ProposedCampEditionId!.Value)
                           .OrderByDescending(g => g.Count())
                           .Select(g => (Guid?)g.Key)
                           .FirstOrDefault();

        if (editionId is null)
        {
            var candidates = await campEditionsRepository.GetByYearAsync(top.Key, ct);
            if (candidates.Count == 1) editionId = candidates[0].Id;
        }
        if (editionId is not null) item.CampEditionId = editionId;
    }
    else if (item.YearSource == MediaItemYearSource.Community)
    {
        // consensus no longer holds after a withdrawal — revert to unplaced
        item.Year = null; item.Decade = null;
        item.CampEditionId = null;
        item.YearSource = MediaItemYearSource.Unknown;
    }
}
```

**`UpsertAsync`** uses the unique `(media_item_id, proposed_by_user_id)` index: fetch by item+user, update if present, insert otherwise, then evaluate consensus in the same transaction.

**`GetTallyAsync`** builds `Groups` (ordered by count descending, `ProposerNames` capped at 5), `ViewerProposal`, `ThemeHints` (`GetYearsForThemeAsync` per attached theme) and `SourceHint` (`GetYearsForSourceAsync` plus the trimmed path via `MediaSourcesService.TrimSourcePath`).

**`SetYearAsAdminAsync`** sets `Year`, `Decade`, `CampEditionId` and `YearSource = Admin`, which freezes the item against rule 1 forever.

#### 10f. `CampEditionAttendanceService`

**`DeclareAsync(editionId, userId, familyMemberId, ct)`**:

1. Edition must exist → `NotFoundException`.
2. If `familyMemberId` is supplied, load the member and verify `FamilyUnitId` matches the caller's. Mismatch → the endpoint returns `Results.Forbid()` with `"No puedes declarar asistencia por este familiar"`.
3. Already declared → return the existing row. **`200` idempotent, not `409`.**

**`WithdrawAsync`**: only declared rows. If the attendance is derived from a `Registration`, throw `ValidationException("La asistencia derivada de una inscripción no se puede eliminar")` → 400.

**`GetTimelineAsync(userId, ct)`**: returns **all 50 editions**, `Attended` true/false, `AttendanceSource` one of `Declared` / `Registration` / `None`, plus a media count per edition. One query for editions, one for declared ids, one for derived ids, one for media counts — four total.

---

### Step 11: Create Minimal API Endpoints

Every group `.RequireAuthorization()`. No anonymous access anywhere in this feature.

#### 11a. `MediaSourcesEndpoints.cs` — `/api/media-sources`

| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/` | Any authenticated |
| `GET` | `/{id:guid}` | Any authenticated |
| `POST` | `/` | Any authenticated |
| `PUT` | `/{id:guid}` | Admin/Board **or** the registrar (checked in handler) |
| `POST` | `/{id:guid}/merge` | Admin, Board |
| `PATCH` | `/{id:guid}/anonymise` | Admin |
| `DELETE` | `/{id:guid}` | Admin |

Pass `user.IsInRole("Admin") || user.IsInRole("Board")` into the service so the mapper can strip `ContributorContact`.

#### 11b. `MediaThemesEndpoints.cs` — `/api/media-themes`

`GET /`, `GET /{slug}/items`, `POST /` (Admin/Board), `PUT /{id}` (Admin/Board), `DELETE /{id}` (Admin, `409` unless `?force=true`).

Item tagging lives on the media-items group: `POST /api/media-items/{id}/themes`, `DELETE /api/media-items/{id}/themes/{themeId}`.

#### 11c. `MediaCommentsEndpoints.cs`

`GET`/`POST /api/media-items/{mediaItemId}/comments`; `PUT`/`DELETE /api/media-comments/{id}`; `POST /api/media-comments/{id}/report`; `GET /api/media-comments/reports` and `PATCH /api/media-comments/reports/{id}` (Admin/Board).

The `POST` comment endpoint carries `.RequireRateLimiting("comments")` — see Step 12.

#### 11d. `MediaDating` endpoints

`GET`/`PUT`/`DELETE /api/media-items/{mediaItemId}/year-proposals`; `PATCH /api/media-items/{id}/year` (Admin/Board).

#### 11e. Album and attendance endpoints

Albums extend `MediaItemsEndpoints.cs`: `GET /api/camp-editions/albums`, `GET /api/camp-editions/{editionId}/album`, `GET /api/media-items/unplaced`, `PATCH /api/media-items/{id}/edition` (Admin/Board), `PATCH /api/media-items/{id}/source`.

Attendance extends `CampsEndpoints.cs`: `POST`/`DELETE`/`GET /api/camp-editions/{editionId}/attendance`, `GET /api/users/me/camp-timeline`.

#### 11f. Filter extensions

`GET /api/media-items` gains `campEditionId`, `unplacedOnly`, `themeId`. `GET /api/memories` gains `campEditionId`, `unplacedOnly`.

> **Regression risk:** [AnniversaryGallery.vue](frontend/src/components/anniversary/AnniversaryGallery.vue) calls `/media-items?approved=true&context=anniversary-50`. Keep every existing parameter and its behaviour. Cover it with a test.

---

### Step 12: Register Services, Endpoints and Rate Limiter in `Program.cs`

**Extensions** — one `[Feature]Extensions.cs` per slice, following [MediaItemsExtensions.cs](src/Abuvi.API/Features/MediaItems/MediaItemsExtensions.cs):

```csharp
builder.Services.AddMediaSources();
builder.Services.AddMediaThemes();
builder.Services.AddMediaComments();
builder.Services.AddMediaDating();
builder.Services.AddScoped<ICampEditionAttendanceRepository, CampEditionAttendanceRepository>();
builder.Services.AddScoped<CampEditionAttendanceService>();
```

```csharp
app.MapMediaSourcesEndpoints();
app.MapMediaThemesEndpoints();
app.MapMediaCommentsEndpoints();
app.MapMediaDatingEndpoints();
```

**Rate limiter — new infrastructure.** This project has **no** rate limiting today; verify with `grep -rn "AddRateLimiter" src/Abuvi.API/`. Built into ASP.NET Core 9, **no NuGet package needed**:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("comments", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.GetUserId()?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

`app.UseRateLimiter();` goes **after** `UseAuthentication()`/`UseAuthorization()` — the partition key reads `httpContext.User`, which is empty before authentication runs. Getting this order wrong silently buckets every member into the `"anonymous"` partition. Check the ordering against [backend-standards.mdc](ai-specs/specs/backend-standards.mdc) § Middleware Pipeline Order.

---

### Step 13: Write Unit Tests

`src/Abuvi.Tests/Unit/` mirroring source structure. xUnit + FluentAssertions + NSubstitute.

#### Entity configuration tests — `Unit/Data/Entities/`

- `MediaSourceConfigurationTests` — columns, `contributor_name` required, FK delete behaviours
- `MediaThemeConfigurationTests` — unique slug index present
- `MediaItemThemeConfigurationTests` — composite PK present
- `MediaCommentConfigurationTests` — table/columns, cascade, max lengths
- `MediaItemYearProposalConfigurationTests` — unique `(media_item_id, proposed_by_user_id)`
- `CampEditionAttendanceConfigurationTests` — both unique indexes
- `MediaItemConfigurationTests` / `MemoryConfigurationTests` — extend for the new columns

#### `Unit/Features/MediaItems/MediaItemsServiceTests`

**Successful cases**
- Upload with `CampEditionId` → year derived from the edition, `YearSource = Uploader`
- Upload with `Year` only → resolves the unique edition for that year
- **Upload with neither → `CampEditionId` null, `YearSource = Unknown`, no exception.** The single most important test in this ticket
- Upload with `NewSource` → creates one `MediaSource`, links the item

**Validation errors**
- Both `MediaSourceId` and `NewSource` → `ValidationException` → 400

**Edge cases**
- Unknown/inactive `ThemeIds` → upload still succeeds, unknown ids ignored
- `Year` matching zero or several editions → item stays unplaced

#### `Unit/Features/MediaSources/MediaSourcesServiceTests`

- `ContributorContact` present for Admin, `null` for Member
- `TrimSourcePath` — 5 segments → `.../` + last 3 for a Member; full path for Admin; 2 segments → unchanged; null/empty → null; backslash paths normalised
- `MergeAsync` repoints all items and deletes the emptied source
- `MergeAsync` with `sourceId == targetId` → `ValidationException`
- `DeleteAsync` nulls `MediaSourceId` without deleting media
- `AnonymiseAsync` clears name/contact/user but keeps the row and the media

#### `Unit/Features/MediaThemes/MediaThemesServiceTests`

- `Slugify("San Abuvino")` → `san-abuvino`; accents and punctuation stripped
- Slug collision → `-2`, then `-3`
- Attach twice → idempotent, one row
- `FirstYear`/`LastYear` span across editions; `UndatedCount` counts null-edition items

#### `Unit/Features/MediaComments/MediaCommentsServiceTests`

- Comment on unapproved item by a Member → forbidden; by Admin → allowed
- Edit window: 14:59 allowed, 15:01 rejected (inject a clock or construct `CreatedAt` explicitly)
- `CommentCount` increments on create, decrements on soft delete
- Soft-deleted comments excluded from the thread
- Duplicate report → `BusinessRuleException` → 409

#### `Unit/Features/MediaDating/MediaDatingServiceTests`

- Consensus at exactly 3 proposals / 66 % → applied
- 2 agreeing proposals → **not** applied
- 3 agreeing of 6 total (50 %) → not applied
- `YearSource = Admin` → consensus skipped entirely
- Withdrawal dropping below threshold → item **un-resolves** back to `Unknown`
- Winning year with competing edition proposals → most-proposed edition wins
- Theme hints and source hints returned

#### `Unit/Features/Camps/CampEditionAttendanceServiceTests`

- Declaring for a family member outside the caller's unit → forbidden
- Declaring twice → idempotent, `200`, one row
- Withdrawing derived attendance → `ValidationException` → 400
- Timeline returns all 50 editions with correct `Attended` flags and sources

---

### Step 14: Write Integration Tests

`src/Abuvi.Tests/Integration/Features/` using the existing `WebApplicationFactory` setup.

- `MediaItems/AlbumEndpointsTests`
  - **Album index issues a constant number of queries** — assert with an EF command interceptor, not eyeballing
  - An album containing a photo, an audio, a video and a memory returns all four in the right counts
  - Pagination boundaries; `pageSize > 100` clamped
  - Member cannot see unapproved items; Admin can
  - **Regression:** `/api/media-items?approved=true&context=anniversary-50` returns exactly what it did before
- `MediaSources/MediaSourcesEndpointsTests`
  - **`contributorContact` is `null` for a Member and populated for Admin/Board** — the RGPD guarantee, tested at the HTTP boundary
  - `sourcePathDisplay` trimmed for a Member, full for Admin
  - Upload with `NewSource` creates exactly one source for a batch of several items
  - Both `MediaSourceId` and `NewSource` → `400`
  - Merge across a batch spanning several editions is transactional
- `MediaThemes/MediaThemesEndpointsTests` — a theme tagged on items from three editions returns all three in one call; role checks on create/delete
- `MediaComments/MediaCommentsEndpointsTests` — full lifecycle; `403` for a Member on moderation endpoints; `429` after 10 comments in a minute
- `Camps/CampEditionAttendanceEndpointsTests` — declare / withdraw / timeline
- `Users/UserErasureTests` — deleting a `User` cascades to comments, reports, proposals, theme tags and attendance; a `MediaSource` they were linked to keeps its row with `ContributorUserId` nulled

---

### Step 15: Update Technical Documentation

- **Action**: Review and update technical documentation according to the changes made. **MANDATORY** before considering the implementation complete.
- **Implementation Steps**:
  1. **Review Changes** — 7 new entities, 2 extended, ~35 endpoints, 1 new middleware concern (rate limiting).
  2. **Identify Documentation Files**:
     - [data-model.md](ai-specs/specs/data-model.md) — add `MediaSource`, `MediaTheme`, `MediaItemTheme`, `MediaComment`, `MediaCommentReport`, `MediaItemYearProposal`, `CampEditionAttendance`; revise `MediaItem` and `Memory`; **mark `PhotoAlbum`, `Photo` and `CampLocation` as `SUPERSEDED — never implemented, do not build`** with a pointer to `MediaItem.CampEditionId`; update the ER diagram
     - [api-endpoints.md](ai-specs/specs/api-endpoints.md) — all new endpoints with request/response examples and error codes
     - [backend-standards.mdc](ai-specs/specs/backend-standards.mdc) — document the new rate limiter in § Middleware Pipeline Order
     - [INDEX.md](ai-specs/changes/INDEX.md) — add the `feat-photo-albums-social` entry, Backend `[x]`
  3. **Update Documentation** — English, matching existing structure and formatting.
  4. **Verify** — changes accurately reflected, structure preserved.
  5. **Report Updates** — list which files changed and how.
- **References**: [documentation-standards.mdc](ai-specs/specs/documentation-standards.mdc). All documentation in English.

---

## Implementation Order

1. **Step 0** — Create branch `feature/feat-photo-albums-social-backend` off `dev`
2. **Step 1** — Extend `MediaItem`
3. **Step 2** — Extend `Memory`
4. **Step 3** — Create 7 new entities
5. **Step 4** — EF Core configurations (7 new, 2 updated)
6. **Step 5** — `AbuviDbContext` DbSets
7. **Step 6** — Migration + raw SQL backfills and partial indexes — **PR cut point**
8. **Step 7** — Request/Response DTOs
9. **Step 8** — FluentValidation validators
10. **Step 9** — Repositories
11. **Step 10** — Services
12. **Step 11** — Endpoints
13. **Step 12** — `Program.cs` registration + rate limiter
14. **Step 13** — Unit tests
15. **Step 14** — Integration tests
16. **Step 15** — Update technical documentation

---

## Testing Checklist

- [ ] `dotnet build` with no warnings; nullable reference types satisfied
- [ ] `dotnet test` green
- [ ] Coverage ≥ 90 % on new services
- [ ] **Upload with no edition and no year succeeds for every `MediaItemType`**
- [ ] **`contributorContact` never returned to a non-Admin caller** (integration test, not a frontend guard)
- [ ] **`sourcePathDisplay` trimmed to 3 segments for Members**
- [ ] Consensus boundary cases: 3/66 % applies, 2 does not, withdrawal un-resolves
- [ ] `YearSource = Admin` never overwritten by consensus
- [ ] Album index query count constant regardless of edition count
- [ ] Comment `429` after 10 in a minute
- [ ] Attendance ownership rule returns `403`
- [ ] Existing `/api/media-items` and `/api/memories` behaviour unchanged
- [ ] Migration applies cleanly on a fresh database **and** on a copy of production-shaped data
- [ ] Both backfills verified against seeded data

---

## Error Response Format

All responses use the `ApiResponse<T>` envelope from `src/Abuvi.API/Common/Models/ApiResponse.cs`.

```json
{ "success": true,  "data": { }, "error": null }
{ "success": false, "data": null, "error": { "message": "…", "code": "NOT_FOUND", "details": null } }
```

| Status | When | Produced by |
|--------|------|-------------|
| `200` | Read, update, idempotent declare | `Results.Ok(ApiResponse<T>.Ok(...))` |
| `201` | Comment, proposal, source created | `Results.Created(...)` |
| `204` | Delete / detach | `Results.NoContent()` |
| `400` | Validation, mutually-exclusive fields, withdrawing derived attendance | `ValidationFilter<T>` or `ValidationException` → middleware |
| `403` | Role or ownership violation | `Results.Forbid()` **at the endpoint** — there is no `ForbiddenException` |
| `404` | Missing entity | `NotFoundException` → middleware |
| `409` | Duplicate report, deleting a theme in use | `BusinessRuleException` → middleware |
| `429` | Comment rate limit | Rate limiter (Step 12) |
| `500` | Unhandled | `GlobalExceptionMiddleware` |

---

## Partial Update Support

- `PATCH /api/media-items/{id}/edition`, `/year`, `/source` are deliberately **narrow single-field endpoints**, not a general `PATCH`. Each carries distinct authorisation and distinct side effects (`/year` sets `YearSource = Admin` and freezes consensus). Do not collapse them into one.
- `PUT /api/media-sources/{id}` is a full replace: every field is sent, nulls clear values.
- `PUT /api/media-items/{mediaItemId}/year-proposals` is an upsert keyed on `(media_item_id, proposed_by_user_id)`.

---

## Dependencies

**NuGet** — none new. `Microsoft.AspNetCore.RateLimiting` is part of the ASP.NET Core 9 shared framework; add `using System.Threading.RateLimiting;` in `Program.cs`.

**EF Core migration commands**

```bash
dotnet ef migrations add AddCampAlbumsThemesAndSocial --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
dotnet ef migrations script --idempotent --project src/Abuvi.API   # for production
```

---

## Notes

**Business rules**

- `CampEditionId == null` means *"edition unknown"* — always temporary, never a permanent category. Every ABUVI item belongs to some camp; we may just not know which.
- Exactly one `CampEdition` exists per historical year (50 editions, 1976–2025). Both backfills and several service branches rely on this — assert `COUNT(*) = 1` rather than assuming it.
- Consensus: ≥3 proposals **and** ≥66 % share. `Admin` freezes an item permanently.
- Comment edit window: 15 minutes, then moderator-only.
- Attendance is declared ∪ derived; derived rows are never persisted and cannot be deleted.

**Language**

- Code, comments and documentation in **English**.
- User-facing validation and error messages in **Spanish** — see [backend-standards.mdc](ai-specs/specs/backend-standards.mdc) § Language Standards for User-Facing Content.

**RGPD / GDPR**

- `MediaSource` holds personal data about people who are **not members and never signed anything**. Three obligations: `ContributorName` is visible to members as attribution (so donors must be told); `ContributorContact` is Admin/Board only, enforced server-side; `PATCH /{id}/anonymise` exists so erasure is one operation rather than an admin editing three fields and missing one.
- `SourcePath` can itself carry personal data. Members see the last three segments only. **Never add an endpoint returning the raw column to non-Admin callers.**
- Deleting a `User` cascades to their comments, reports, proposals, theme tags and attendance. A `MediaSource` that referenced them keeps its row with the FK nulled, so donated material is not orphaned.
- `CampEditionAttendance` reveals which camps a person attended — authenticated members only, matching existing registration visibility.

**Constraints**

- Do not create `PhotoAlbum` / `Photo` entities (trap 2).
- Do not create `PagedResult<T>` (trap 3).
- Do not remove the dead `camp_location_id` columns in this ticket.
- Name everything `Media*`, never `Photo*` (trap 4).

---

## Next Steps After Implementation

1. Open the PR against **`dev`**, not `main`.
2. Run `/plan-frontend-ticket` for Task 4 once these endpoints are merged.
3. Task 3 (bulk importer) is independent and can proceed in parallel — see [feat-photo-albums-social_setup-importer_backend.md](./feat-photo-albums-social_setup-importer_backend.md).
4. Seed the theme catalogue before the frontend lands, or the theme UI demos empty.
5. Confirm the starting theme list with the board (spec suggests: San Abuvino, Actuaciones, Asambleas, Cocina y comedor, Excursiones, Montaje y desmontaje, Juegos de noche, Deportes, Talleres).

---

## Implementation Verification

**Code Quality**
- [ ] C# analyzers clean, no new warnings
- [ ] Nullable reference types satisfied — no `!` suppressions outside EF navigations
- [ ] Vertical Slice boundaries respected; no cross-slice repository calls except through services
- [ ] `AsNoTracking()` on all read queries
- [ ] `CancellationToken` threaded through every async path

**Functionality**
- [ ] All endpoints return documented status codes
- [ ] Every group carries `RequireAuthorization()`
- [ ] Role-restricted endpoints verified with a Member token (expect `403`)

**Testing**
- [ ] ≥ 90 % coverage on new services (xUnit + FluentAssertions + NSubstitute)
- [ ] Integration tests pass against a real PostgreSQL instance

**Integration**
- [ ] Migration applies and rolls back cleanly
- [ ] Backfills verified on seeded data
- [ ] Swagger renders all new endpoints
- [ ] `UseRateLimiter()` positioned after authentication — verified, not assumed

**Documentation**
- [ ] `data-model.md`, `api-endpoints.md`, `backend-standards.mdc`, `INDEX.md` all updated

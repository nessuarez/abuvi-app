# Backend Implementation Plan: feat-media-50-aniversary — Real Media Uploads for the 50th Anniversary

## Overview

Implement the `Memory` and `MediaItem` entities with full CRUD and approval workflow, following Vertical Slice Architecture. These entities are defined in the data model spec but do not yet exist in code. The blob storage infrastructure (`IBlobStorageService`) is already implemented — this ticket builds the domain layer on top of it.

**Two feature slices:**
- `Features/Memories/` — Written memories/stories contributed by members
- `Features/MediaItems/` — Multimedia content (photos, videos, audio, documents) for the historical archive

Both slices share an approval workflow: items start as `isApproved = false, isPublished = false` and require Admin/Board review.

---

## Architecture Context

### Feature Slices

```
src/Abuvi.API/Features/
├── Memories/
│   ├── MemoriesModels.cs        # Entity + DTOs + response records
│   ├── MemoriesRepository.cs     # EF Core data access (interface + implementation)
│   ├── MemoriesService.cs        # Business logic
│   ├── MemoriesEndpoints.cs      # Minimal API route handlers
│   ├── MemoriesValidator.cs      # FluentValidation rules
│   └── MemoriesExtensions.cs     # DI registration
├── MediaItems/
│   ├── MediaItemsModels.cs       # Entity + enum + DTOs + response records
│   ├── MediaItemsRepository.cs   # EF Core data access (interface + implementation)
│   ├── MediaItemsService.cs      # Business logic
│   ├── MediaItemsEndpoints.cs    # Minimal API route handlers
│   ├── MediaItemsValidator.cs    # FluentValidation rules
│   └── MediaItemsExtensions.cs   # DI registration
```

### EF Core Configurations

```
src/Abuvi.API/Data/Configurations/
├── MemoryConfiguration.cs
├── MediaItemConfiguration.cs
```

### Cross-Cutting Concerns Used

- `ApiResponse<T>` — from `Common/Models/ApiResponse.cs`
- `ValidationFilter<T>` — from `Common/Filters/ValidationFilter.cs`
- `NotFoundException` — from `Common/Exceptions/NotFoundException.cs`
- `HttpContextExtensions.GetUserId()` — from `Common/Extensions/HttpContextExtensions.cs`
- `IBlobStorageService` — from `Features/BlobStorage/` (for blob deletion on media item delete)
- `AbuviDbContext` — from `Data/AbuviDbContext.cs`

### Important: CampLocation Does Not Exist

The `Memory` and `MediaItem` entities reference `CampLocationId` as an optional FK in the data model. However, `CampLocation` does not exist in the codebase yet. **For this ticket, make `CampLocationId` a nullable `Guid` property without a navigation property or FK constraint.** The FK relationship will be established when the `CampLocation` feature is implemented in a future ticket. Add a `// TODO: Add FK relationship when CampLocation entity is created` comment.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a backend-specific branch
- **Branch name**: `feature/feat-media-50-aniversary-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/feat-media-50-aniversary-backend`
  3. Verify: `git branch`
- **Notes**: Do NOT work directly on `feat/media-50-aniversary` to separate backend concerns from frontend.

---

### Step 1: Create Memory Entity and MediaItem Entity + Enum

**File**: `src/Abuvi.API/Features/Memories/MemoriesModels.cs`

**Action**: Define the `Memory` entity class and all request/response DTOs.

**Entity fields** (per data-model.md):

```csharp
public class Memory
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Title { get; set; } = string.Empty;       // max 200
    public string Content { get; set; } = string.Empty;     // rich text
    public int? Year { get; set; }                          // 1975–2026
    public Guid? CampLocationId { get; set; }               // nullable, no FK yet
    public bool IsPublished { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User Author { get; set; } = null!;
    // TODO: Add CampLocation navigation when CampLocation entity is created
}
```

**DTOs to create in same file:**

```csharp
public record CreateMemoryRequest(
    string Title,
    string Content,
    int? Year,
    Guid? CampLocationId);

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
    DateTime UpdatedAt,
    List<MediaItemResponse> MediaItems);
```

**Extension method for mapping** (add at bottom of file):

```csharp
public static class MemoryMappingExtensions
{
    public static MemoryResponse ToResponse(this Memory memory, List<MediaItemResponse>? mediaItems = null) =>
        new(
            memory.Id,
            memory.AuthorUserId,
            memory.Author?.FirstName + " " + memory.Author?.LastName ?? "Unknown",
            memory.Title,
            memory.Content,
            memory.Year,
            memory.CampLocationId,
            memory.IsPublished,
            memory.IsApproved,
            memory.CreatedAt,
            memory.UpdatedAt,
            mediaItems ?? []);
}
```

---

**File**: `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs`

**Action**: Define the `MediaItemType` enum, `MediaItem` entity, and all DTOs.

**Enum:**

```csharp
public enum MediaItemType
{
    Photo,
    Video,
    Interview,
    Document,
    Audio
}
```

**Entity fields** (per data-model.md + enriched spec):

```csharp
public class MediaItem
{
    public Guid Id { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileUrl { get; set; } = string.Empty;     // max 2048
    public string? ThumbnailUrl { get; set; }               // max 2048
    public MediaItemType Type { get; set; }
    public string Title { get; set; } = string.Empty;       // max 200
    public string? Description { get; set; }                 // max 1000
    public int? Year { get; set; }
    public string? Decade { get; set; }                      // max 10, auto-derived
    public Guid? MemoryId { get; set; }                      // optional FK
    public Guid? CampLocationId { get; set; }                // nullable, no FK yet
    public bool IsPublished { get; set; }
    public bool IsApproved { get; set; }
    public string? Context { get; set; }                     // max 50
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User UploadedBy { get; set; } = null!;
    public Memory? Memory { get; set; }
    // TODO: Add CampLocation navigation when CampLocation entity is created
}
```

**DTOs:**

```csharp
public record CreateMediaItemRequest(
    string FileUrl,
    string? ThumbnailUrl,
    MediaItemType Type,
    string Title,
    string? Description,
    int? Year,
    Guid? MemoryId,
    Guid? CampLocationId,
    string? Context);

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

**Extension method + decade helper:**

```csharp
public static class MediaItemMappingExtensions
{
    public static MediaItemResponse ToResponse(this MediaItem item) =>
        new(
            item.Id,
            item.UploadedByUserId,
            item.UploadedBy?.FirstName + " " + item.UploadedBy?.LastName ?? "Unknown",
            item.FileUrl,
            item.ThumbnailUrl,
            item.Type.ToString(),
            item.Title,
            item.Description,
            item.Year,
            item.Decade,
            item.MemoryId,
            item.Context,
            item.IsPublished,
            item.IsApproved,
            item.CreatedAt);

    public static string? DeriveDecade(int? year) => year switch
    {
        >= 1970 and < 1980 => "70s",
        >= 1980 and < 1990 => "80s",
        >= 1990 and < 2000 => "90s",
        >= 2000 and < 2010 => "00s",
        >= 2010 and < 2020 => "10s",
        >= 2020 and < 2030 => "20s",
        _ => null
    };
}
```

**Dependencies**: `using Abuvi.API.Features.Users;`

---

### Step 2: Create EF Core Configurations

**File**: `src/Abuvi.API/Data/Configurations/MemoryConfiguration.cs`

**Action**: Configure `Memory` entity for PostgreSQL.

**Key implementation details:**
- Table name: `memories` (snake_case)
- Column names: snake_case (e.g., `author_user_id`, `is_published`, `created_at`)
- `Title`: `HasMaxLength(200)`, `IsRequired()`
- `Content`: `IsRequired()` (no max length for rich text)
- `Year`: optional
- `CampLocationId`: optional (no FK constraint yet — just a column)
- `IsPublished`: `HasDefaultValue(false)`
- `IsApproved`: `HasDefaultValue(false)`
- `CreatedAt`, `UpdatedAt`: `HasDefaultValueSql("NOW()")`
- **Index**: `ix_memories_author_user_id` on `AuthorUserId`
- **Index**: `ix_memories_year` on `Year`
- **Index**: `ix_memories_approved_published` on `(IsApproved, IsPublished)` (for filtering approved content)
- **Relationship**: `HasOne(m => m.Author).WithMany().HasForeignKey(m => m.AuthorUserId).OnDelete(DeleteBehavior.Cascade)`

**Follow the `CampPhotoConfiguration.cs` pattern exactly** for property configuration syntax.

---

**File**: `src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs`

**Action**: Configure `MediaItem` entity for PostgreSQL.

**Key implementation details:**
- Table name: `media_items`
- Column names: snake_case
- `FileUrl`: `HasMaxLength(2048)`, `IsRequired()`
- `ThumbnailUrl`: `HasMaxLength(2048)`, optional
- `Type`: `HasConversion<string>()`, `IsRequired()`, `HasMaxLength(20)` — store enum as string
- `Title`: `HasMaxLength(200)`, `IsRequired()`
- `Description`: `HasMaxLength(1000)`, optional
- `Decade`: `HasMaxLength(10)`, optional
- `Context`: `HasMaxLength(50)`, optional
- `IsPublished`: `HasDefaultValue(false)`
- `IsApproved`: `HasDefaultValue(false)`
- `CreatedAt`, `UpdatedAt`: `HasDefaultValueSql("NOW()")`
- **Index**: `ix_media_items_uploaded_by_user_id` on `UploadedByUserId`
- **Index**: `ix_media_items_year` on `Year`
- **Index**: `ix_media_items_context` on `Context`
- **Index**: `ix_media_items_approved_published` on `(IsApproved, IsPublished)`
- **Index**: `ix_media_items_memory_id` on `MemoryId`
- **Relationship**: `HasOne(m => m.UploadedBy).WithMany().HasForeignKey(m => m.UploadedByUserId).OnDelete(DeleteBehavior.Cascade)`
- **Relationship**: `HasOne(m => m.Memory).WithMany(mem => mem.MediaItems).HasForeignKey(m => m.MemoryId).OnDelete(DeleteBehavior.SetNull)`

---

### Step 3: Update DbContext

**File**: `src/Abuvi.API/Data/AbuviDbContext.cs`

**Action**: Add new DbSets and imports.

**Changes:**
1. Add using directives:
   ```csharp
   using Abuvi.API.Features.Memories;
   using Abuvi.API.Features.MediaItems;
   ```
2. Add DbSet properties (after existing DbSets, around line 34):
   ```csharp
   public DbSet<Memory> Memories => Set<Memory>();
   public DbSet<MediaItem> MediaItems => Set<MediaItem>();
   ```

**Notes**: `ApplyConfigurationsFromAssembly` in `OnModelCreating` will auto-discover the new configurations.

---

### Step 4: Create FluentValidation Validators

**File**: `src/Abuvi.API/Features/Memories/MemoriesValidator.cs`

**Action**: Create `CreateMemoryRequestValidator`.

**Rules:**
- `Title`: `NotEmpty()`, `MaximumLength(200)`
- `Content`: `NotEmpty()`
- `Year`: When provided, `InclusiveBetween(1975, 2026)`

**Follow the pattern from `BlobStorageValidator.cs`** — inherit from `AbstractValidator<CreateMemoryRequest>`.

---

**File**: `src/Abuvi.API/Features/MediaItems/MediaItemsValidator.cs`

**Action**: Create `CreateMediaItemRequestValidator`.

**Rules:**
- `FileUrl`: `NotEmpty()`, `MaximumLength(2048)`
- `Type`: `IsInEnum()` — validates against `MediaItemType`
- `Title`: `NotEmpty()`, `MaximumLength(200)`
- `Description`: When provided, `MaximumLength(1000)`
- `Year`: When provided, `InclusiveBetween(1975, 2026)`
- `Context`: When provided, `MaximumLength(50)`
- `ThumbnailUrl`: **Required** when `Type` is `Photo` or `Video` → use `.Must()` or `.When()`:
  ```csharp
  RuleFor(x => x.ThumbnailUrl)
      .NotEmpty()
      .WithMessage("ThumbnailUrl is required for Photo and Video types")
      .When(x => x.Type is MediaItemType.Photo or MediaItemType.Video);
  ```

**Dependencies**: `using FluentValidation;`

---

### Step 5: Implement Repositories

**File**: `src/Abuvi.API/Features/Memories/MemoriesRepository.cs`

**Action**: Define interface and implementation for `Memory` data access.

**Interface:**
```csharp
public interface IMemoriesRepository
{
    Task<Memory?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Memory>> GetListAsync(int? year, bool? approved, CancellationToken ct);
    Task AddAsync(Memory memory, CancellationToken ct);
    Task UpdateAsync(Memory memory, CancellationToken ct);
}
```

**Implementation** — follow `GuestsRepository` pattern:
- Constructor: `MemoriesRepository(AbuviDbContext db)`
- `GetByIdAsync`: Include `Author` navigation, use `AsNoTracking()`
- `GetListAsync`: Filter by `Year` (if provided), filter by `IsApproved && IsPublished` (if `approved = true`), or `!IsApproved` (if `approved = false`). Include `Author`. Order by `CreatedAt` descending.
- `AddAsync`: `db.Memories.Add(memory); await db.SaveChangesAsync(ct);`
- `UpdateAsync`: `db.Memories.Update(memory); await db.SaveChangesAsync(ct);`

---

**File**: `src/Abuvi.API/Features/MediaItems/MediaItemsRepository.cs`

**Action**: Define interface and implementation for `MediaItem` data access.

**Interface:**
```csharp
public interface IMediaItemsRepository
{
    Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MediaItem>> GetListAsync(int? year, bool? approved, string? context, MediaItemType? type, CancellationToken ct);
    Task<IReadOnlyList<MediaItem>> GetByMemoryIdAsync(Guid memoryId, CancellationToken ct);
    Task AddAsync(MediaItem mediaItem, CancellationToken ct);
    Task UpdateAsync(MediaItem mediaItem, CancellationToken ct);
    Task DeleteAsync(MediaItem mediaItem, CancellationToken ct);
}
```

**Implementation:**
- Constructor: `MediaItemsRepository(AbuviDbContext db)`
- `GetByIdAsync`: Include `UploadedBy`, `Memory` navigations, use `AsNoTracking()`
- `GetListAsync`: Apply optional filters for `Year`, `IsApproved/IsPublished`, `Context`, `Type`. Include `UploadedBy`. Order by `CreatedAt` descending.
- `GetByMemoryIdAsync`: Filter by `MemoryId`, include `UploadedBy`
- `AddAsync`, `UpdateAsync`, `DeleteAsync`: Standard EF Core patterns

---

### Step 6: Implement Services

**File**: `src/Abuvi.API/Features/Memories/MemoriesService.cs`

**Action**: Implement business logic for memories.

**Constructor DI:**
```csharp
public class MemoriesService(
    IMemoriesRepository repository,
    IMediaItemsRepository mediaItemsRepository,
    ILogger<MemoriesService> logger)
```

**Methods:**

| Method | Logic |
|---|---|
| `CreateAsync(Guid userId, CreateMemoryRequest req, CancellationToken ct)` | Create `Memory` with `Id = Guid.NewGuid()`, `AuthorUserId = userId`, `IsApproved = false`, `IsPublished = false`, `CreatedAt = DateTime.UtcNow`, `UpdatedAt = DateTime.UtcNow`. Log creation. Return `MemoryResponse`. |
| `GetByIdAsync(Guid id, CancellationToken ct)` | Get memory by ID, throw `NotFoundException` if not found. Also fetch associated `MediaItems` via `mediaItemsRepository.GetByMemoryIdAsync()`. Return `MemoryResponse` with media items. |
| `GetListAsync(int? year, bool? approved, CancellationToken ct)` | Delegate to repository. Map to list of `MemoryResponse` (without media items for list performance). |
| `ApproveAsync(Guid id, CancellationToken ct)` | Get memory, throw if not found. Set `IsApproved = true`, `IsPublished = true`, `UpdatedAt = DateTime.UtcNow`. Log approval. |
| `RejectAsync(Guid id, CancellationToken ct)` | Get memory, throw if not found. Set `IsApproved = false`, `IsPublished = false`, `UpdatedAt = DateTime.UtcNow`. Log rejection. |

**Important notes:**
- For `GetByIdAsync`, need a separate repository method that does NOT use `AsNoTracking()` (for approve/reject to work with change tracking). Or load the entity for update separately.
- Actually, for approve/reject, use a `GetByIdForUpdateAsync` pattern (without `AsNoTracking()`) or simply `db.Memories.FindAsync()`.
- The repository `GetByIdAsync` used for reads should use `AsNoTracking()`, but approve/reject need tracked entities. Handle this by either:
  - A) Having two methods in repository (`GetByIdAsync` for reads, `GetByIdTrackingAsync` for updates)
  - B) Using `db.Memories.FindAsync()` directly in the update methods (follow the pattern in `GuestsService` where `GetByIdAsync` returns tracked entities in repository)

**Recommended approach**: Follow the existing `GuestsRepository` pattern where `GetByIdAsync` returns tracked entities (no `AsNoTracking()`), and only the list queries use `AsNoTracking()`.

---

**File**: `src/Abuvi.API/Features/MediaItems/MediaItemsService.cs`

**Action**: Implement business logic for media items.

**Constructor DI:**
```csharp
public class MediaItemsService(
    IMediaItemsRepository repository,
    IBlobStorageService blobStorageService,
    ILogger<MediaItemsService> logger)
```

**Methods:**

| Method | Logic |
|---|---|
| `CreateAsync(Guid userId, CreateMediaItemRequest req, CancellationToken ct)` | Create `MediaItem` with `Id = Guid.NewGuid()`, `UploadedByUserId = userId`, `IsApproved = false`, `IsPublished = false`, auto-derive `Decade` from `Year`, set timestamps. Log creation. Return `MediaItemResponse`. |
| `GetByIdAsync(Guid id, CancellationToken ct)` | Get by ID, throw `NotFoundException` if not found. Return `MediaItemResponse`. |
| `GetListAsync(int? year, bool? approved, string? context, MediaItemType? type, CancellationToken ct)` | Delegate to repository. Map to list of `MediaItemResponse`. |
| `ApproveAsync(Guid id, CancellationToken ct)` | Get item, throw if not found. Set `IsApproved = true`, `IsPublished = true`, `UpdatedAt`. Log. |
| `RejectAsync(Guid id, CancellationToken ct)` | Get item, throw if not found. Set `IsApproved = false`, `IsPublished = false`, `UpdatedAt`. Log. |
| `DeleteAsync(Guid id, CancellationToken ct)` | Get item, throw if not found. Extract blob key from `FileUrl` (strip `PublicBaseUrl` prefix). Build list of keys to delete (file + thumbnail if present). Call `blobStorageService.DeleteManyAsync()`. Then delete from DB. Log deletion. |

**Decade derivation**: Use the `MediaItemMappingExtensions.DeriveDecade()` static method defined in Step 1.

**Blob key extraction for delete**: The `FileUrl` looks like `https://abuvi-media.fsn1.your-objectstorage.com/media-items/{guid}.{ext}`. To get the blob key, strip the `PublicBaseUrl` prefix and the leading `/`:
```csharp
private static string ExtractBlobKey(string fileUrl, string publicBaseUrl)
{
    return fileUrl.Replace(publicBaseUrl, "").TrimStart('/');
}
```

---

### Step 7: Create Minimal API Endpoints

**File**: `src/Abuvi.API/Features/Memories/MemoriesEndpoints.cs`

**Action**: Define memory API endpoints following the `GuestsEndpoints.cs` pattern.

```csharp
public static class MemoriesEndpoints
{
    public static void MapMemoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/memories")
            .WithTags("Memories")
            .RequireAuthorization();

        group.MapPost("/", CreateMemory)
            .AddEndpointFilter<ValidationFilter<CreateMemoryRequest>>()
            .WithName("CreateMemory")
            .Produces<ApiResponse<MemoryResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/", ListMemories)
            .WithName("ListMemories")
            .Produces<ApiResponse<IReadOnlyList<MemoryResponse>>>();

        group.MapGet("/{id:guid}", GetMemory)
            .WithName("GetMemory")
            .Produces<ApiResponse<MemoryResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/approve", ApproveMemory)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("ApproveMemory")
            .Produces<ApiResponse<MemoryResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/reject", RejectMemory)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("RejectMemory")
            .Produces<ApiResponse<MemoryResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
```

**Endpoint handlers** (private static async methods):

- `CreateMemory`: Extract `userId` from `ClaimsPrincipal user` via `user.GetUserId()`. Call `service.CreateAsync(userId, request, ct)`. Return `Results.Created(...)`.
- `ListMemories`: Accept `[FromQuery] int? year`, `[FromQuery] bool? approved`. Call `service.GetListAsync(...)`. Return `Results.Ok(...)`.
- `GetMemory`: Accept `Guid id`. Call `service.GetByIdAsync(id, ct)`. Return `Results.Ok(...)`.
- `ApproveMemory`: Call `service.ApproveAsync(id, ct)`. Return `Results.Ok(...)`.
- `RejectMemory`: Call `service.RejectAsync(id, ct)`. Return `Results.Ok(...)`.

**Imports needed:**
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Abuvi.API.Common.Models;
using Abuvi.API.Common.Filters;
using Abuvi.API.Common.Extensions;
```

---

**File**: `src/Abuvi.API/Features/MediaItems/MediaItemsEndpoints.cs`

**Action**: Define media item API endpoints.

```csharp
public static class MediaItemsEndpoints
{
    public static void MapMediaItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media-items")
            .WithTags("MediaItems")
            .RequireAuthorization();

        group.MapPost("/", CreateMediaItem)
            .AddEndpointFilter<ValidationFilter<CreateMediaItemRequest>>()
            .WithName("CreateMediaItem")
            .Produces<ApiResponse<MediaItemResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/", ListMediaItems)
            .WithName("ListMediaItems")
            .Produces<ApiResponse<IReadOnlyList<MediaItemResponse>>>();

        group.MapGet("/{id:guid}", GetMediaItem)
            .WithName("GetMediaItem")
            .Produces<ApiResponse<MediaItemResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/approve", ApproveMediaItem)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("ApproveMediaItem")
            .Produces<ApiResponse<MediaItemResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/reject", RejectMediaItem)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .WithName("RejectMediaItem")
            .Produces<ApiResponse<MediaItemResponse>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteMediaItem)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("DeleteMediaItem")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
```

**Endpoint handlers:**

- `CreateMediaItem`: Extract `userId` from claims. Call `service.CreateAsync(userId, request, ct)`. Return `Results.Created(...)`.
- `ListMediaItems`: Accept `[FromQuery] int? year`, `[FromQuery] bool? approved`, `[FromQuery] string? context`, `[FromQuery] MediaItemType? type`. Call `service.GetListAsync(...)`. Return `Results.Ok(...)`.
- `GetMediaItem`, `ApproveMediaItem`, `RejectMediaItem`: Same pattern as memories.
- `DeleteMediaItem`: Call `service.DeleteAsync(id, ct)`. Return `Results.NoContent()`.

---

### Step 8: Create DI Extensions

**File**: `src/Abuvi.API/Features/Memories/MemoriesExtensions.cs`

```csharp
public static class MemoriesExtensions
{
    public static IServiceCollection AddMemories(this IServiceCollection services)
    {
        services.AddScoped<IMemoriesRepository, MemoriesRepository>();
        services.AddScoped<MemoriesService>();
        return services;
    }
}
```

**Note**: No `IOptions` needed — no configuration section for memories. Validators are auto-registered via `AddValidatorsFromAssemblyContaining<Program>()` already in `Program.cs` (line 137).

---

**File**: `src/Abuvi.API/Features/MediaItems/MediaItemsExtensions.cs`

```csharp
public static class MediaItemsExtensions
{
    public static IServiceCollection AddMediaItems(this IServiceCollection services)
    {
        services.AddScoped<IMediaItemsRepository, MediaItemsRepository>();
        services.AddScoped<MediaItemsService>();
        return services;
    }
}
```

---

### Step 9: Register in Program.cs

**File**: `src/Abuvi.API/Program.cs`

**Action**: Add service registration and endpoint mapping.

**Changes:**

1. Add using directives at the top:
   ```csharp
   using Abuvi.API.Features.Memories;
   using Abuvi.API.Features.MediaItems;
   ```

2. Add service registration (after line 191 `builder.Services.AddBlobStorage(...)`:
   ```csharp
   // Memories feature
   builder.Services.AddMemories();

   // Media Items feature
   builder.Services.AddMediaItems();
   ```

3. Add endpoint mapping (after line 368 `app.MapBlobStorageEndpoints()`:
   ```csharp
   app.MapMemoriesEndpoints();
   app.MapMediaItemsEndpoints();
   ```

---

### Step 10: Create EF Core Migration

**Action**: Generate and verify the migration.

**Commands:**
```bash
cd src/Abuvi.API
dotnet ef migrations add AddMemoriesAndMediaItems
```

**Verify the migration:**
- Check the generated `Up()` method creates `memories` and `media_items` tables
- Verify all columns, indexes, and FK constraints are correct
- Verify the `Down()` method drops both tables
- The `type` column on `media_items` should store as `text` (string conversion)

**Apply the migration** (auto-applied on startup, but can also run manually):
```bash
dotnet ef database update
```

---

### Step 11: Write Unit Tests

**File**: `src/Abuvi.Tests/Unit/Features/Memories/MemoriesServiceTests.cs`

**Tests** (follow `GuestsServiceTests.cs` pattern with NSubstitute + FluentAssertions):

| Test Name | Description |
|---|---|
| `CreateAsync_WithValidRequest_CreatesMemoryWithDefaultFlags` | Verify `IsApproved = false`, `IsPublished = false`, `CreatedAt` set |
| `CreateAsync_WithYear_SetsYearCorrectly` | Year stored correctly |
| `CreateAsync_CallsRepositoryAdd` | Verify `repository.AddAsync` called |
| `GetByIdAsync_WithExistingId_ReturnsMemoryResponse` | Returns correct mapping |
| `GetByIdAsync_WithNonExistentId_ThrowsNotFoundException` | Throws `NotFoundException` |
| `GetListAsync_WithApprovedTrue_DelegatesToRepository` | Repository called with correct filter |
| `ApproveAsync_WithExistingId_SetsBothFlagsTrue` | `IsApproved = true`, `IsPublished = true` |
| `ApproveAsync_WithNonExistentId_ThrowsNotFoundException` | Throws |
| `RejectAsync_WithExistingId_SetsBothFlagsFalse` | Both flags false |

---

**File**: `src/Abuvi.Tests/Unit/Features/MediaItems/MediaItemsServiceTests.cs`

| Test Name | Description |
|---|---|
| `CreateAsync_WithValidRequest_CreatesItemWithDefaultFlags` | `IsApproved = false`, `IsPublished = false` |
| `CreateAsync_WithYear1985_DerivesDecadeAs80s` | Decade auto-derived |
| `CreateAsync_WithYearNull_DerivesDecadeAsNull` | Decade null |
| `CreateAsync_WithAudioType_AcceptsNullThumbnail` | No error |
| `ApproveAsync_WithExistingId_SetsBothFlagsTrue` | Correct flags |
| `RejectAsync_WithExistingId_SetsBothFlagsFalse` | Correct flags |
| `DeleteAsync_WithExistingId_DeletesItemAndBlob` | Both `repository.DeleteAsync` and `blobStorageService.DeleteManyAsync` called |
| `DeleteAsync_WithThumbnail_DeletesBothBlobs` | Two blob keys passed to delete |
| `DeleteAsync_WithNonExistentId_ThrowsNotFoundException` | Throws |
| `GetListAsync_WithContextFilter_DelegatesToRepository` | Repository called with correct params |

---

**File**: `src/Abuvi.Tests/Unit/Features/Memories/MemoriesValidatorTests.cs`

| Test Name | Description |
|---|---|
| `Validate_WithEmptyTitle_Fails` | Validation error on Title |
| `Validate_WithTitleOver200Chars_Fails` | Validation error |
| `Validate_WithEmptyContent_Fails` | Validation error on Content |
| `Validate_WithYearBelow1975_Fails` | Validation error |
| `Validate_WithYearAbove2026_Fails` | Validation error |
| `Validate_WithValidRequest_Passes` | No errors |
| `Validate_WithNullYear_Passes` | Year is optional |

---

**File**: `src/Abuvi.Tests/Unit/Features/MediaItems/MediaItemsValidatorTests.cs`

| Test Name | Description |
|---|---|
| `Validate_WithEmptyFileUrl_Fails` | Validation error |
| `Validate_WithFileUrlOver2048Chars_Fails` | Validation error |
| `Validate_WithInvalidType_Fails` | Validation error |
| `Validate_WithEmptyTitle_Fails` | Validation error |
| `Validate_WithDescriptionOver1000Chars_Fails` | Validation error |
| `Validate_WithPhotoTypeAndNoThumbnail_Fails` | Validation error |
| `Validate_WithVideoTypeAndNoThumbnail_Fails` | Validation error |
| `Validate_WithAudioTypeAndNoThumbnail_Passes` | Valid |
| `Validate_WithDocumentTypeAndNoThumbnail_Passes` | Valid |
| `Validate_WithYearOutOfRange_Fails` | Validation error |
| `Validate_WithContextOver50Chars_Fails` | Validation error |
| `Validate_WithValidPhotoRequest_Passes` | No errors |

---

### Step 12: Write Integration Tests

**File**: `src/Abuvi.Tests/Integration/Features/Memories/MemoriesEndpointsTests.cs`

Follow the `GuestsEndpointsTests.cs` or `BlobStorageEndpointsTests.cs` pattern with `WebApplicationFactory<Program>`.

| Test Name | Description |
|---|---|
| `PostMemory_Unauthenticated_Returns401` | 401 |
| `PostMemory_ValidRequest_Returns201` | 201 with `MemoryResponse` in body |
| `PostMemory_EmptyTitle_Returns400` | Validation error |
| `GetMemories_Returns200WithList` | 200 |
| `GetMemoryById_NonExistent_Returns404` | 404 |
| `PatchApprove_AsMember_Returns403` | 403 |
| `PatchApprove_AsAdmin_Returns200` | 200 |
| `PatchReject_AsBoard_Returns200` | 200 |

---

**File**: `src/Abuvi.Tests/Integration/Features/MediaItems/MediaItemsEndpointsTests.cs`

| Test Name | Description |
|---|---|
| `PostMediaItem_Unauthenticated_Returns401` | 401 |
| `PostMediaItem_ValidRequest_Returns201` | 201 with `MediaItemResponse` |
| `PostMediaItem_PhotoWithoutThumbnail_Returns400` | Validation error |
| `GetMediaItems_WithFilters_Returns200` | Filtered list |
| `GetMediaItemById_NonExistent_Returns404` | 404 |
| `PatchApprove_AsMember_Returns403` | 403 |
| `PatchApprove_AsAdmin_Returns200` | 200, flags updated |
| `PatchReject_AsBoard_Returns200` | 200 |
| `DeleteMediaItem_AsMember_Returns403` | 403 |
| `DeleteMediaItem_AsAdmin_Returns204` | 204 |

**Note for integration tests:** Mock `IBlobStorageService` via NSubstitute in the test fixture to avoid real S3 calls during delete tests.

---

### Step 13: Update Technical Documentation

**Action**: Update documentation per changes made.

**Files to update:**

1. **`ai-specs/specs/api-endpoints.md`** — Add new sections for:
   - `POST /api/memories` — request/response format, roles, status codes
   - `GET /api/memories` — query params, response format
   - `GET /api/memories/{id}` — response format
   - `PATCH /api/memories/{id}/approve` — roles, status codes
   - `PATCH /api/memories/{id}/reject` — roles, status codes
   - `POST /api/media-items` — request/response format, roles
   - `GET /api/media-items` — query params (year, approved, context, type)
   - `GET /api/media-items/{id}` — response format
   - `PATCH /api/media-items/{id}/approve` — roles
   - `PATCH /api/media-items/{id}/reject` — roles
   - `DELETE /api/media-items/{id}` — roles, blob cleanup behavior

2. **`ai-specs/specs/data-model.md`** — Mark `Memory` and `MediaItem` as implemented. Add `Audio` to `MediaItemType` enum. Add `Context` field to `MediaItem`.

**All documentation in English per `documentation-standards.mdc`.**

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-media-50-aniversary-backend`
2. **Step 1**: Create entities and DTOs (`MemoriesModels.cs`, `MediaItemsModels.cs`)
3. **Step 2**: Create EF Core configurations (`MemoryConfiguration.cs`, `MediaItemConfiguration.cs`)
4. **Step 3**: Update `AbuviDbContext.cs` with new DbSets
5. **Step 4**: Create validators (`MemoriesValidator.cs`, `MediaItemsValidator.cs`)
6. **Step 5**: Implement repositories (`MemoriesRepository.cs`, `MediaItemsRepository.cs`)
7. **Step 6**: Implement services (`MemoriesService.cs`, `MediaItemsService.cs`)
8. **Step 7**: Create endpoints (`MemoriesEndpoints.cs`, `MediaItemsEndpoints.cs`)
9. **Step 8**: Create DI extensions (`MemoriesExtensions.cs`, `MediaItemsExtensions.cs`)
10. **Step 9**: Register in `Program.cs`
11. **Step 10**: Create EF Core migration
12. **Step 11**: Write unit tests
13. **Step 12**: Write integration tests
14. **Step 13**: Update technical documentation

---

## Testing Checklist

- [ ] All unit tests pass (`MemoriesServiceTests`, `MediaItemsServiceTests`, `MemoriesValidatorTests`, `MediaItemsValidatorTests`)
- [ ] All integration tests pass (`MemoriesEndpointsTests`, `MediaItemsEndpointsTests`)
- [ ] Coverage >= 90% for new code
- [ ] `dotnet build` succeeds with no warnings
- [ ] Migration applies and rolls back cleanly
- [ ] Swagger/OpenAPI shows all new endpoints with correct schemas

---

## Error Response Format

All endpoints use `ApiResponse<T>` envelope:

| HTTP Status | When | Response |
|---|---|---|
| 200 | Successful GET, PATCH | `{ "success": true, "data": { ... } }` |
| 201 | Successful POST (create) | `{ "success": true, "data": { ... } }` |
| 204 | Successful DELETE | No body |
| 400 | Validation error | `{ "success": false, "error": { "message": "Validation failed", "code": "VALIDATION_ERROR", "details": [...] } }` |
| 401 | Unauthenticated | Standard ASP.NET challenge |
| 403 | Insufficient role | Standard ASP.NET forbid |
| 404 | Entity not found | `{ "success": false, "error": { "message": "No se encontró ...", "code": "NOT_FOUND" } }` |

---

## Dependencies

### NuGet Packages

No new NuGet packages required. All dependencies are already in the project:
- `FluentValidation.DependencyInjectionExtensions` — already used
- `Microsoft.EntityFrameworkCore` — already used
- `AWSSDK.S3` — already used (via BlobStorage feature)

### EF Core Migration Command

```bash
cd src/Abuvi.API
dotnet ef migrations add AddMemoriesAndMediaItems
dotnet ef database update
```

---

## Notes

### Business Rules
- All new memories and media items start with `IsApproved = false, IsPublished = false`
- Items are only visible to non-admin users when `IsApproved = true AND IsPublished = true`
- Only Admin and Board roles can approve/reject
- Only Admin can delete media items (which also deletes the blob)
- Decade is auto-derived from Year — never provided by the client

### CampLocation FK
- `CampLocationId` is stored as a nullable `Guid` column **without FK constraint** for now
- The FK relationship will be added when the `CampLocation` entity is implemented in a future ticket
- Add `// TODO` comments on the entity properties

### Logging
- Log every create, approve, reject, and delete operation with structured logging
- Include `userId`, `entityId`, and operation type in log messages
- Use `ILogger<T>` injected via constructor

### GDPR
- No sensitive personal data stored in Memory/MediaItem entities
- `AuthorUserId` / `UploadedByUserId` are references, not PII
- No encryption needed for these entities

### Language
- All code in English (class names, method names, comments, log messages)
- Validation error messages in Spanish (matching existing pattern: `"El archivo es obligatorio"`)
- API error messages in Spanish (matching existing `NotFoundException`: `"No se encontró..."`)

---

## Next Steps After Implementation

1. **Frontend implementation** — Separate ticket/branch for frontend changes
2. **CampLocation entity** — Future ticket to create the entity and add FK constraints
3. **Pagination** — Consider adding cursor-based pagination to `GetListAsync` if item count grows
4. **Batch operations** — Consider adding batch approve/reject for admin convenience

---

## Implementation Verification

- [ ] **Code Quality**: No C# analyzer warnings, nullable reference types handled
- [ ] **Architecture**: Both feature slices are self-contained, no cross-slice dependencies except shared `IBlobStorageService`
- [ ] **Endpoints**: Correct HTTP methods and status codes, proper role-based authorization
- [ ] **Validation**: All input validated via FluentValidation, thumbnail required for Photo/Video types
- [ ] **EF Core**: Migration creates correct schema, indexes present, FK relationships correct
- [ ] **Testing**: ≥ 90% coverage, AAA pattern, descriptive test names (`MethodName_State_Expected`)
- [ ] **Documentation**: `api-endpoints.md` updated, `data-model.md` updated
- [ ] **Logging**: Structured logs on all mutations
- [ ] **No secrets committed**: No API keys or connection strings in source

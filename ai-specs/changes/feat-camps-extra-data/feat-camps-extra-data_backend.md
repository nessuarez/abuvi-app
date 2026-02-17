# Backend Implementation Plan: feat-camps-extra-data — Camp Extended Information from Google Places

## Overview

This feature enriches the Camp model with contact information, photos, and metadata automatically sourced from Google Places API. When a camp is created with a `GooglePlaceId`, the backend fetches extended details server-side, stores them on the Camp entity, and saves Google Places photo references to a new `CampPhoto` table.

Architecture: Vertical Slice Architecture. Changes primarily span the **Camps** and **GooglePlaces** feature slices, plus EF Core Data layer. The GooglePlaces slice provides richer data; the Camps slice consumes and persists it.

**Non-breaking constraint**: All new Camp fields are nullable. Camps created without a `GooglePlaceId` continue to work without modification.

---

## Architecture Context

### Feature Slices Affected

- `src/Abuvi.API/Features/Camps/` — Primary slice (entity, DTOs, service, repository, endpoints)
- `src/Abuvi.API/Features/GooglePlaces/` — Extended to return richer place details
- `src/Abuvi.API/Data/` — New EF Core configuration + migration
- `src/Abuvi.Tests/Unit/Features/Camps/` — New + updated unit tests

### Files to Create

| File | Purpose |
|------|---------|
| `src/Abuvi.API/Features/Camps/GooglePlacesMapperService.cs` | Maps PlaceDetails → Camp extended fields + CampPhoto entities |
| `src/Abuvi.API/Data/Configurations/CampPhotoConfiguration.cs` | EF Core Fluent config for CampPhoto table |
| `src/Abuvi.Tests/Unit/Features/Camps/GooglePlacesMapperServiceTests.cs` | Unit tests for the mapper |

### Files to Modify

| File | Change Summary |
|------|---------------|
| `src/Abuvi.API/Features/GooglePlaces/GooglePlacesService.cs` | Extend PlaceDetails DTO + fields query param |
| `src/Abuvi.API/Features/Camps/CampsModels.cs` | Add Camp fields, CampPhoto entity, new DTOs |
| `src/Abuvi.API/Features/Camps/CampsService.cs` | Inject mapper, enrich from Google on create, return detail DTO |
| `src/Abuvi.API/Features/Camps/ICampsRepository.cs` | Add GetByIdWithPhotosAsync + AddPhotosAsync |
| `src/Abuvi.API/Features/Camps/CampsRepository.cs` | Implement new repository methods |
| `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` | GET /{id} returns CampDetailResponse |
| `src/Abuvi.API/Data/Configurations/CampConfiguration.cs` | Add new column mappings + Photos relationship |
| `src/Abuvi.API/Data/AbuviDbContext.cs` | Add CampPhotos DbSet |
| `src/Abuvi.API/Program.cs` | Register GooglePlacesMapperService |
| `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs` | Update for new deps + new test cases |

### Cross-Cutting Concerns

- No changes to middleware or shared models required
- `CampPhoto` entity is Camp-specific — stays in Camps feature
- `GooglePlacesMapperService` lives in Camps feature (maps TO Camp entities, uses GooglePlaces DTOs as input — one-way dependency, no circular ref)

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new backend-specific branch
- **Branch Naming**: `feature/feat-camps-extra-data-backend`
- **Implementation Steps**:
  1. Verify you are on `main` (or the most up-to-date base): `git checkout main && git pull origin main`
  2. Create new branch: `git checkout -b feature/feat-camps-extra-data-backend`
  3. Verify: `git branch`
- **Notes**: Do NOT continue on `feature/feat-google-places-camps-frontend`. This backend work is independent.

---

### Step 1: Extend `PlaceDetails` DTO and `GooglePlacesService`

- **File**: `src/Abuvi.API/Features/GooglePlaces/GooglePlacesService.cs`
- **Action**: Extend the `GetPlaceDetailsAsync` method to request and return contact, rating, status, and photo data from Google Places API

#### 1a. New internal DTOs (append at bottom of file)

```csharp
// Add to existing internal Google API response models:
internal record GoogleAddressComponent(
    [property: JsonPropertyName("long_name")] string LongName,
    [property: JsonPropertyName("short_name")] string ShortName,
    [property: JsonPropertyName("types")] string[] Types
);

internal record GooglePhotoResult(
    [property: JsonPropertyName("photo_reference")] string PhotoReference,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("html_attributions")] string[] HtmlAttributions
);
```

#### 1b. Update `PlaceResult` internal record

Replace the existing `PlaceResult` with:

```csharp
internal record PlaceResult(
    [property: JsonPropertyName("place_id")] string PlaceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("formatted_address")] string FormattedAddress,
    [property: JsonPropertyName("geometry")] Geometry Geometry,
    [property: JsonPropertyName("types")] string[] Types,
    [property: JsonPropertyName("international_phone_number")] string? InternationalPhoneNumber,
    [property: JsonPropertyName("formatted_phone_number")] string? FormattedPhoneNumber,
    [property: JsonPropertyName("website")] string? Website,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("rating")] double? Rating,
    [property: JsonPropertyName("user_ratings_total")] int? UserRatingsTotal,
    [property: JsonPropertyName("business_status")] string? BusinessStatus,
    [property: JsonPropertyName("address_components")] GoogleAddressComponent[]? AddressComponents,
    [property: JsonPropertyName("photos")] GooglePhotoResult[]? Photos
);
```

#### 1c. New public DTO: `PlacePhoto`

```csharp
public record PlacePhoto(
    string PhotoReference,
    int Width,
    int Height,
    string[] HtmlAttributions
);
```

#### 1d. Update public `PlaceDetails` record

```csharp
public record PlaceDetails(
    string PlaceId,
    string Name,
    string FormattedAddress,
    decimal Latitude,
    decimal Longitude,
    string[] Types,
    // New extended fields:
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? Website,
    string? GoogleMapsUrl,
    decimal? Rating,
    int? RatingCount,
    string? BusinessStatus,
    GoogleAddressComponent[]? AddressComponents,
    IReadOnlyList<PlacePhoto> Photos
);
```

**Note**: Make `GoogleAddressComponent` `public` (not `internal`) since it needs to be readable by `GooglePlacesMapperService` in the Camps feature.

#### 1e. Update `GetPlaceDetailsAsync` method

Change the fields query parameter:
```
// Old:
var fields = "place_id,name,formatted_address,geometry,types";

// New:
var fields = "place_id,name,formatted_address,geometry,types,address_component,formatted_phone_number,international_phone_number,website,url,rating,user_ratings_total,business_status,photos";
```

Update the `PlaceDetails` construction in the return statement to map all new fields:
```csharp
return new PlaceDetails(
    place.PlaceId,
    place.Name,
    place.FormattedAddress,
    (decimal)place.Geometry.Location.Lat,
    (decimal)place.Geometry.Location.Lng,
    place.Types,
    PhoneNumber: place.InternationalPhoneNumber,
    NationalPhoneNumber: place.FormattedPhoneNumber,
    Website: place.Website,
    GoogleMapsUrl: place.Url,
    Rating: place.Rating.HasValue ? (decimal)place.Rating.Value : null,
    RatingCount: place.UserRatingsTotal,
    BusinessStatus: place.BusinessStatus,
    AddressComponents: place.AddressComponents,
    Photos: place.Photos?
        .Select(p => new PlacePhoto(p.PhotoReference, p.Width, p.Height, p.HtmlAttributions))
        .ToList() ?? []
);
```

- **Dependencies**: `System.Text.Json.Serialization` (already present for JSON deserialization)
- **Notes**:
  - The Google Places Details API billing is per field group. Adding these fields increases API cost. This is acceptable for MVP.
  - The existing frontend calls to `/api/places/details` will automatically receive richer data with this change — no breaking changes since new fields are additive.
  - `GoogleAddressComponent` must be `public` (not `internal`) because it's used by `GooglePlacesMapperService` in the Camps feature.

---

### Step 2: Create `GooglePlacesMapperService`

- **File**: `src/Abuvi.API/Features/Camps/GooglePlacesMapperService.cs`
- **Action**: New service that maps a `PlaceDetails` object (from GooglePlaces feature) into Camp extended fields and `CampPhoto` entities

#### Interface

```csharp
namespace Abuvi.API.Features.Camps;

public interface IGooglePlacesMapperService
{
    CampGoogleData MapToCampData(PlaceDetails details);
    IReadOnlyList<CampPhoto> MapToPhotos(PlaceDetails details, Guid campId);
}
```

#### New value object `CampGoogleData`

```csharp
public record CampGoogleData(
    string? FormattedAddress,
    string? StreetAddress,
    string? Locality,
    string? AdministrativeArea,
    string? PostalCode,
    string? Country,
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    decimal? GoogleRating,
    int? GoogleRatingCount,
    string? BusinessStatus,
    string? PlaceTypes  // JSON array serialized as string e.g. "[\"campground\",\"lodging\"]"
);
```

#### Implementation: `MapToCampData`

```csharp
public CampGoogleData MapToCampData(PlaceDetails details)
{
    var components = details.AddressComponents ?? [];

    string? GetComponent(params string[] types)
        => components.FirstOrDefault(c => c.Types.Intersect(types).Any())?.LongName;

    var streetNumber = components.FirstOrDefault(c => c.Types.Contains("street_number"))?.LongName;
    var route = components.FirstOrDefault(c => c.Types.Contains("route"))?.LongName;
    var streetAddress = (streetNumber, route) switch
    {
        (not null, not null) => $"{route}, {streetNumber}",
        (null, not null) => route,
        _ => null
    };

    var placeTypes = details.Types.Length > 0
        ? System.Text.Json.JsonSerializer.Serialize(details.Types)
        : null;

    return new CampGoogleData(
        FormattedAddress: details.FormattedAddress,
        StreetAddress: streetAddress,
        Locality: GetComponent("locality", "sublocality"),
        AdministrativeArea: GetComponent("administrative_area_level_2", "administrative_area_level_1"),
        PostalCode: GetComponent("postal_code"),
        Country: GetComponent("country"),
        PhoneNumber: details.PhoneNumber,
        NationalPhoneNumber: details.NationalPhoneNumber,
        WebsiteUrl: details.Website,
        GoogleMapsUrl: details.GoogleMapsUrl,
        GoogleRating: details.Rating,
        GoogleRatingCount: details.RatingCount,
        BusinessStatus: details.BusinessStatus,
        PlaceTypes: placeTypes
    );
}
```

#### Implementation: `MapToPhotos`

```csharp
public IReadOnlyList<CampPhoto> MapToPhotos(PlaceDetails details, Guid campId)
{
    if (details.Photos.Count == 0) return [];

    var now = DateTime.UtcNow;
    return details.Photos
        .Select((photo, index) => new CampPhoto
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            PhotoReference = photo.PhotoReference,
            PhotoUrl = null,  // Phase 1: references only; Phase 2: download + store URL
            Width = photo.Width,
            Height = photo.Height,
            AttributionName = StripHtmlAttribution(photo.HtmlAttributions.FirstOrDefault() ?? "Google"),
            AttributionUrl = ExtractAttributionUrl(photo.HtmlAttributions.FirstOrDefault()),
            IsOriginal = true,
            IsPrimary = index == 0,
            DisplayOrder = index + 1,
            CreatedAt = now,
            UpdatedAt = now
        })
        .ToList();
}

// Private helpers for parsing Google's HTML attribution string
// e.g. "<a href=\"https://maps.google.com/maps/contrib/123\">John Doe</a>"
private static string StripHtmlAttribution(string htmlAttribution)
{
    // Simple HTML tag strip — use System.Text.RegularExpressions
    return System.Text.RegularExpressions.Regex.Replace(htmlAttribution, "<[^>]+>", "").Trim();
}

private static string? ExtractAttributionUrl(string? htmlAttribution)
{
    if (string.IsNullOrEmpty(htmlAttribution)) return null;
    var match = System.Text.RegularExpressions.Regex.Match(htmlAttribution, @"href=""([^""]+)""");
    return match.Success ? match.Groups[1].Value : null;
}
```

- **Dependencies**: No new NuGet packages. Uses `System.Text.Json`, `System.Text.RegularExpressions`, `System.Linq`.
- **Implementation Notes**:
  - The `using` import for `Abuvi.API.Features.GooglePlaces` is needed at the top of this file for `PlaceDetails` and `GoogleAddressComponent`.
  - Keep private helpers `private static` — they do not access instance state.
  - `CampGoogleData` and `CampPhoto` are defined in `CampsModels.cs` (Camps feature namespace).

---

### Step 3: Extend `Camp` Entity with New Fields

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add new nullable properties to the `Camp` class after `GooglePlaceId`

```csharp
// Add after: public string? GooglePlaceId { get; set; }

// --- Extended Contact Information (from Google Places) ---
public string? FormattedAddress { get; set; }    // Full formatted address
public string? StreetAddress { get; set; }        // Street + number
public string? Locality { get; set; }             // City/town
public string? AdministrativeArea { get; set; }   // Province/region
public string? PostalCode { get; set; }
public string? Country { get; set; }
public string? PhoneNumber { get; set; }          // International format: "+34 972 59 05 07"
public string? NationalPhoneNumber { get; set; }  // National format: "972 59 05 07"
public string? WebsiteUrl { get; set; }
public string? GoogleMapsUrl { get; set; }        // Direct Google Maps link

// --- Google Metadata ---
public decimal? GoogleRating { get; set; }        // e.g., 3.7
public int? GoogleRatingCount { get; set; }       // e.g., 113
public DateTime? LastGoogleSyncAt { get; set; }
public string? BusinessStatus { get; set; }       // "OPERATIONAL", "CLOSED_TEMPORARILY"
public string? PlaceTypes { get; set; }           // JSON array: "[\"campground\"]"

// --- Navigation Properties (add after IsActive / existing nav props) ---
public ICollection<CampPhoto> Photos { get; set; } = new List<CampPhoto>();
```

**Keep existing navigation property**: `ICollection<CampEdition> Editions` stays.

---

### Step 4: Create `CampPhoto` Entity

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Append the new entity class at the end of the file

```csharp
/// <summary>
/// Photo associated with a camp, sourced from Google Places API (Phase 1: references only)
/// </summary>
public class CampPhoto
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }

    public string? PhotoReference { get; set; }  // Google Places photo reference token
    public string? PhotoUrl { get; set; }         // Future: direct URL if downloaded and stored

    public int Width { get; set; }
    public int Height { get; set; }

    public string AttributionName { get; set; } = string.Empty;  // Photo author (required by Google T&C)
    public string? AttributionUrl { get; set; }                   // Author profile URL

    public bool IsOriginal { get; set; } = true;   // true = from Google Places, false = manually added
    public bool IsPrimary { get; set; } = false;   // Primary display photo
    public int DisplayOrder { get; set; } = 0;     // Sort order in gallery (1-based)

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Camp Camp { get; set; } = null!;
}
```

---

### Step 5: Update DTOs in `CampsModels.cs`

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add new DTOs, update existing `CampResponse`

#### 5a. New `CampPhotoResponse` record

```csharp
/// <summary>
/// DTO for camp photo data returned by API
/// </summary>
public record CampPhotoResponse(
    Guid Id,
    string? PhotoReference,
    string? PhotoUrl,
    int Width,
    int Height,
    string AttributionName,
    string? AttributionUrl,
    bool IsPrimary,
    int DisplayOrder
);
```

#### 5b. Update `CampResponse` record

Add extended contact + metadata fields (no Photos — lightweight for list endpoints):

```csharp
public record CampResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    // Extended fields (nullable — may be null for manually-created camps):
    string? FormattedAddress,
    string? PhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    decimal? GoogleRating,
    int? GoogleRatingCount,
    string? BusinessStatus,
    // Existing pricing + status:
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

#### 5c. New `CampDetailResponse` record (full detail with photos)

```csharp
/// <summary>
/// Full camp detail DTO including all extended Google Places fields and photos
/// Used by GET /api/camps/{id}
/// </summary>
public record CampDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId,
    // Contact info:
    string? FormattedAddress,
    string? StreetAddress,
    string? Locality,
    string? AdministrativeArea,
    string? PostalCode,
    string? Country,
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    // Metadata:
    decimal? GoogleRating,
    int? GoogleRatingCount,
    string? BusinessStatus,
    string? PlaceTypes,
    DateTime? LastGoogleSyncAt,
    // Pricing:
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    // Photos:
    IReadOnlyList<CampPhotoResponse> Photos,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Note**: `CreateCampRequest` and `UpdateCampRequest` do NOT need to change. The backend auto-enriches from Google Places when `GooglePlaceId` is provided. This keeps the API surface minimal.

---

### Step 6: Update EF Core Configuration

#### 6a. Update `CampConfiguration.cs`

- **File**: `src/Abuvi.API/Data/Configurations/CampConfiguration.cs`
- **Action**: Add mappings for all new Camp fields after the `GooglePlaceId` section, plus the Photos relationship

Add after the `builder.HasIndex(c => c.GooglePlaceId)` block:

```csharp
// Extended Contact Information (all nullable)
builder.Property(c => c.FormattedAddress)
    .HasMaxLength(500)
    .HasColumnName("formatted_address");

builder.Property(c => c.StreetAddress)
    .HasMaxLength(200)
    .HasColumnName("street_address");

builder.Property(c => c.Locality)
    .HasMaxLength(100)
    .HasColumnName("locality");

builder.Property(c => c.AdministrativeArea)
    .HasMaxLength(100)
    .HasColumnName("administrative_area");

builder.Property(c => c.PostalCode)
    .HasMaxLength(20)
    .HasColumnName("postal_code");

builder.Property(c => c.Country)
    .HasMaxLength(100)
    .HasColumnName("country");

builder.Property(c => c.PhoneNumber)
    .HasMaxLength(30)
    .HasColumnName("phone_number");

builder.Property(c => c.NationalPhoneNumber)
    .HasMaxLength(30)
    .HasColumnName("national_phone_number");

builder.Property(c => c.WebsiteUrl)
    .HasMaxLength(500)
    .HasColumnName("website_url");

builder.Property(c => c.GoogleMapsUrl)
    .HasMaxLength(500)
    .HasColumnName("google_maps_url");

// Google Metadata
builder.Property(c => c.GoogleRating)
    .HasPrecision(3, 1)
    .HasColumnName("google_rating");

builder.Property(c => c.GoogleRatingCount)
    .HasColumnName("google_rating_count");

builder.Property(c => c.LastGoogleSyncAt)
    .HasColumnName("last_google_sync_at");

builder.Property(c => c.BusinessStatus)
    .HasMaxLength(50)
    .HasColumnName("business_status");

builder.Property(c => c.PlaceTypes)
    .HasMaxLength(500)
    .HasColumnName("place_types");

// Photos relationship (defined in CampPhotoConfiguration via HasOne → WithMany)
// No explicit configuration needed here as it's configured from the CampPhoto side
```

#### 6b. Create `CampPhotoConfiguration.cs`

- **File**: `src/Abuvi.API/Data/Configurations/CampPhotoConfiguration.cs`
- **Action**: Create new file

```csharp
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for CampPhoto entity
/// </summary>
public class CampPhotoConfiguration : IEntityTypeConfiguration<CampPhoto>
{
    public void Configure(EntityTypeBuilder<CampPhoto> builder)
    {
        builder.ToTable("camp_photos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.CampId)
            .IsRequired()
            .HasColumnName("camp_id");

        builder.Property(p => p.PhotoReference)
            .HasMaxLength(500)
            .HasColumnName("photo_reference");

        builder.Property(p => p.PhotoUrl)
            .HasMaxLength(1000)
            .HasColumnName("photo_url");

        builder.Property(p => p.Width)
            .IsRequired()
            .HasColumnName("width");

        builder.Property(p => p.Height)
            .IsRequired()
            .HasColumnName("height");

        builder.Property(p => p.AttributionName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("attribution_name");

        builder.Property(p => p.AttributionUrl)
            .HasMaxLength(500)
            .HasColumnName("attribution_url");

        builder.Property(p => p.IsOriginal)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_original");

        builder.Property(p => p.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_primary");

        builder.Property(p => p.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("display_order");

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(p => p.CampId)
            .HasDatabaseName("ix_camp_photos_camp_id");

        builder.HasIndex(p => new { p.CampId, p.IsPrimary })
            .HasDatabaseName("ix_camp_photos_camp_id_is_primary");

        // Relationship: Camp → Photos (cascade delete — photos deleted when camp deleted)
        builder.HasOne(p => p.Camp)
            .WithMany(c => c.Photos)
            .HasForeignKey(p => p.CampId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### Step 7: Update `AbuviDbContext`

- **File**: `src/Abuvi.API/Data/AbuviDbContext.cs`
- **Action**: Add `CampPhotos` DbSet

Add after `public DbSet<AssociationSettings> AssociationSettings => Set<AssociationSettings>();`:

```csharp
public DbSet<CampPhoto> CampPhotos => Set<CampPhoto>();
```

No new `using` needed — `CampPhoto` is already in `Abuvi.API.Features.Camps` which is already imported.

---

### Step 8: Create EF Core Migration

- **Action**: Generate migration `AddExtendedCampInformation`
- **Command**:

```bash
dotnet ef migrations add AddExtendedCampInformation --project src/Abuvi.API
```

- **Verify the migration includes**:
  - 14 new nullable columns on `camps` table (all contact + metadata fields)
  - New `camp_photos` table with all columns + FK constraint + indexes
  - No changes to existing `camps` columns (non-breaking)
  - `camp_photos.camp_id` has cascade delete referencing `camps.id`
- **Then apply to dev database**:

```bash
dotnet ef database update --project src/Abuvi.API
```

- **Notes**: Review the generated `.cs` file before running `database update`. Confirm all new columns have nullable types in the migration (`string?` = no `IsRequired()` on column).

---

### Step 9: Update `ICampsRepository` and `CampsRepository`

#### 9a. Update interface

- **File**: `src/Abuvi.API/Features/Camps/ICampsRepository.cs`
- **Action**: Add two new methods

```csharp
/// <summary>
/// Gets a camp by ID including its photos (ordered by DisplayOrder)
/// Use this for detail view endpoints where photos are needed
/// </summary>
Task<Camp?> GetByIdWithPhotosAsync(Guid id, CancellationToken cancellationToken = default);

/// <summary>
/// Persists a collection of camp photos
/// </summary>
Task<IReadOnlyList<CampPhoto>> AddPhotosAsync(
    IEnumerable<CampPhoto> photos,
    CancellationToken cancellationToken = default);
```

#### 9b. Implement in `CampsRepository`

- **File**: `src/Abuvi.API/Features/Camps/CampsRepository.cs`
- **Action**: Add the two new method implementations

```csharp
public async Task<Camp?> GetByIdWithPhotosAsync(Guid id, CancellationToken cancellationToken = default)
    => await _db.Camps
        .AsNoTracking()
        .Include(c => c.Photos.OrderBy(p => p.DisplayOrder))
        .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

public async Task<IReadOnlyList<CampPhoto>> AddPhotosAsync(
    IEnumerable<CampPhoto> photos,
    CancellationToken cancellationToken = default)
{
    var photoList = photos.ToList();
    if (photoList.Count == 0) return photoList;

    _db.CampPhotos.AddRange(photoList);
    await _db.SaveChangesAsync(cancellationToken);
    return photoList;
}
```

- **Dependencies**: `Microsoft.EntityFrameworkCore` (already present). Needs `_db.CampPhotos` which is now registered in DbContext.

---

### Step 10: Update `CampsService`

- **File**: `src/Abuvi.API/Features/Camps/CampsService.cs`
- **Action**: Inject new dependencies, enrich from Google Places on create, return `CampDetailResponse` for detail queries

#### 10a. Update constructor

Add two new dependencies:

```csharp
public class CampsService
{
    private readonly ICampsRepository _repository;
    private readonly IGooglePlacesService _googlePlacesService;
    private readonly IGooglePlacesMapperService _mapper;

    public CampsService(
        ICampsRepository repository,
        IGooglePlacesService googlePlacesService,
        IGooglePlacesMapperService mapper)
    {
        _repository = repository;
        _googlePlacesService = googlePlacesService;
        _mapper = mapper;
    }
```

Add `using Abuvi.API.Features.GooglePlaces;` at the top of the file.

#### 10b. Update `CreateAsync`

After the camp is created and saved, add Google Places enrichment:

```csharp
public async Task<CampDetailResponse> CreateAsync(
    CreateCampRequest request,
    CancellationToken cancellationToken = default)
{
    // ... existing validation + camp entity creation ...

    var created = await _repository.CreateAsync(camp, cancellationToken);

    // Auto-enrich from Google Places if GooglePlaceId is provided
    if (!string.IsNullOrWhiteSpace(created.GooglePlaceId))
    {
        created = await EnrichFromGooglePlacesAsync(created, cancellationToken);
    }

    return MapToCampDetailResponse(created, created.Photos);
}

private async Task<Camp> EnrichFromGooglePlacesAsync(Camp camp, CancellationToken ct)
{
    var details = await _googlePlacesService.GetPlaceDetailsAsync(camp.GooglePlaceId!, ct);
    if (details is null) return camp;

    var googleData = _mapper.MapToCampData(details);
    var photos = _mapper.MapToPhotos(details, camp.Id);

    // Apply Google data to camp entity
    camp.FormattedAddress = googleData.FormattedAddress;
    camp.StreetAddress = googleData.StreetAddress;
    camp.Locality = googleData.Locality;
    camp.AdministrativeArea = googleData.AdministrativeArea;
    camp.PostalCode = googleData.PostalCode;
    camp.Country = googleData.Country;
    camp.PhoneNumber = googleData.PhoneNumber;
    camp.NationalPhoneNumber = googleData.NationalPhoneNumber;
    camp.WebsiteUrl = googleData.WebsiteUrl;
    camp.GoogleMapsUrl = googleData.GoogleMapsUrl;
    camp.GoogleRating = googleData.GoogleRating;
    camp.GoogleRatingCount = googleData.GoogleRatingCount;
    camp.BusinessStatus = googleData.BusinessStatus;
    camp.PlaceTypes = googleData.PlaceTypes;
    camp.LastGoogleSyncAt = DateTime.UtcNow;

    var updatedCamp = await _repository.UpdateAsync(camp, ct);

    if (photos.Count > 0)
    {
        var savedPhotos = await _repository.AddPhotosAsync(photos, ct);
        updatedCamp.Photos = savedPhotos.ToList();
    }

    return updatedCamp;
}
```

**Important**: `CreateAsync` return type changes from `CampResponse` to `CampDetailResponse` — update accordingly.

#### 10c. Update `GetByIdAsync`

Change to use `GetByIdWithPhotosAsync` and return `CampDetailResponse`:

```csharp
public async Task<CampDetailResponse?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken = default)
{
    var camp = await _repository.GetByIdWithPhotosAsync(id, cancellationToken);
    return camp is null ? null : MapToCampDetailResponse(camp, camp.Photos);
}
```

#### 10d. Update `GetAllAsync`

Stays returning `IReadOnlyList<CampResponse>` — no photos needed for the list. Ensure `MapToCampResponse` maps new extended fields.

#### 10e. Add / update mapping helpers

```csharp
private static CampResponse MapToCampResponse(Camp camp) => new(
    camp.Id, camp.Name, camp.Description, camp.Location,
    camp.Latitude, camp.Longitude, camp.GooglePlaceId,
    camp.FormattedAddress, camp.PhoneNumber, camp.WebsiteUrl,
    camp.GoogleMapsUrl, camp.GoogleRating, camp.GoogleRatingCount,
    camp.BusinessStatus,
    camp.PricePerAdult, camp.PricePerChild, camp.PricePerBaby,
    camp.IsActive, camp.CreatedAt, camp.UpdatedAt
);

private static CampDetailResponse MapToCampDetailResponse(Camp camp, IEnumerable<CampPhoto> photos) => new(
    camp.Id, camp.Name, camp.Description, camp.Location,
    camp.Latitude, camp.Longitude, camp.GooglePlaceId,
    camp.FormattedAddress, camp.StreetAddress, camp.Locality,
    camp.AdministrativeArea, camp.PostalCode, camp.Country,
    camp.PhoneNumber, camp.NationalPhoneNumber,
    camp.WebsiteUrl, camp.GoogleMapsUrl,
    camp.GoogleRating, camp.GoogleRatingCount,
    camp.BusinessStatus, camp.PlaceTypes, camp.LastGoogleSyncAt,
    camp.PricePerAdult, camp.PricePerChild, camp.PricePerBaby,
    camp.IsActive,
    Photos: photos.Select(MapToPhotoResponse).ToList(),
    camp.CreatedAt, camp.UpdatedAt
);

private static CampPhotoResponse MapToPhotoResponse(CampPhoto photo) => new(
    photo.Id, photo.PhotoReference, photo.PhotoUrl,
    photo.Width, photo.Height, photo.AttributionName,
    photo.AttributionUrl, photo.IsPrimary, photo.DisplayOrder
);
```

- **Implementation Notes**:
  - If `GetPlaceDetailsAsync` throws `ExternalServiceException`, let it propagate — the global middleware will return a 500. Consider whether to catch it and continue with partial data (recommendation: propagate for now, make it optional in Phase 5).
  - `UpdateAsync` on the Camp entity after creation requires the EF Core change tracker to work. Since `CreateAsync` in the repository likely calls `SaveChangesAsync`, the `camp` entity returned may be detached. Use `UpdateAsync` (which re-attaches via `db.Camps.Update(camp)`).

---

### Step 11: Update `CampsEndpoints`

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`
- **Action**: Update GET by ID to return `CampDetailResponse`, and POST to return `CampDetailResponse`

**Changes**:
1. `GET /{id:guid}` — change `Produces<ApiResponse<CampResponse>>()` to `Produces<ApiResponse<CampDetailResponse>>()`
2. `POST /` — change `Produces<ApiResponse<CampResponse>>(StatusCodes.Status201Created)` to `Produces<ApiResponse<CampDetailResponse>>(StatusCodes.Status201Created)`
3. Update handler return types accordingly

```csharp
// Updated GET by ID handler
private static async Task<IResult> GetCampById(
    Guid id,
    CampsService service,
    CancellationToken ct)
{
    var camp = await service.GetByIdAsync(id, ct);
    return camp is not null
        ? Results.Ok(ApiResponse<CampDetailResponse>.Ok(camp))
        : Results.NotFound(ApiResponse<CampDetailResponse>.NotFound("No se encontró el campamento"));
}

// Updated POST handler
private static async Task<IResult> CreateCamp(
    CreateCampRequest request,
    CampsService service,
    CancellationToken ct)
{
    var camp = await service.CreateAsync(request, ct);
    return Results.Created($"/api/camps/{camp.Id}", ApiResponse<CampDetailResponse>.Ok(camp));
}
```

---

### Step 12: Register `GooglePlacesMapperService` in `Program.cs`

- **File**: `src/Abuvi.API/Program.cs`
- **Action**: Register the new service

Find the block where `CampsService` and `CampsRepository` are registered, and add:

```csharp
builder.Services.AddScoped<IGooglePlacesMapperService, GooglePlacesMapperService>();
```

Place it near the other Camps service registrations for consistency.

---

### Step 13: Write Unit Tests for `GooglePlacesMapperService`

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/GooglePlacesMapperServiceTests.cs`
- **Action**: Create comprehensive unit tests. No mocking needed — the mapper is a pure transformation service.

**Test cases to cover**:

```csharp
namespace Abuvi.Tests.Unit.Features.Camps;

public class GooglePlacesMapperServiceTests
{
    private readonly GooglePlacesMapperService _sut = new();

    // --- MapToCampData Tests ---

    [Fact]
    public void MapToCampData_WithFullPlaceDetails_MapsAllContactFields()

    [Fact]
    public void MapToCampData_WithAddressComponents_ExtractsStreetAndLocality()

    [Fact]
    public void MapToCampData_WithRouteAndStreetNumber_CombinesStreetAddress()
    // e.g., route = "Carrer Major", number = "5" → StreetAddress = "Carrer Major, 5"

    [Fact]
    public void MapToCampData_WithNullPhoneNumber_ReturnsNullPhoneFields()

    [Fact]
    public void MapToCampData_WithNullRating_ReturnsNullRating()

    [Fact]
    public void MapToCampData_WithPlaceTypes_SerializesAsJsonArray()
    // Types = ["campground", "lodging"] → PlaceTypes = "[\"campground\",\"lodging\"]"

    [Fact]
    public void MapToCampData_WithEmptyPlaceTypes_ReturnsNullPlaceTypes()

    [Fact]
    public void MapToCampData_WithNullAddressComponents_ReturnsNullAddressFields()

    // --- MapToPhotos Tests ---

    [Fact]
    public void MapToPhotos_WithNoPhotos_ReturnsEmptyList()

    [Fact]
    public void MapToPhotos_WithMultiplePhotos_FirstPhotoIsPrimary()

    [Fact]
    public void MapToPhotos_WithMultiplePhotos_SetsCorrectDisplayOrder()
    // First photo: DisplayOrder=1, second: 2, etc.

    [Fact]
    public void MapToPhotos_SetsCorrectCampId()

    [Fact]
    public void MapToPhotos_WithHtmlAttribution_ParsesNameAndUrl()
    // Input: "<a href=\"https://maps.google.com/maps/contrib/123\">John Doe</a>"
    // → AttributionName = "John Doe", AttributionUrl = "https://maps.google.com/maps/contrib/123"

    [Fact]
    public void MapToPhotos_WithNoHtmlAttribution_UsesDefaultAttributionName()
    // AttributionName = "Google"

    [Fact]
    public void MapToPhotos_SetsIsOriginalTrue()

    [Fact]
    public void MapToPhotos_SetsPhotoUrlNull()
    // Phase 1: references only, URL is null until downloaded
}
```

**Test data helper** — create a private factory method in the test class:

```csharp
private static PlaceDetails CreateFullPlaceDetails() => new PlaceDetails(
    PlaceId: "ChIJ38SpLTDCuhIRgdtW_484UBk",
    Name: "Alba Colònies",
    FormattedAddress: "Crta Pujarnol, km 5, 17834 Pujarnol, Girona, España",
    Latitude: 42.0851m,
    Longitude: 2.7631m,
    Types: ["campground", "lodging", "establishment"],
    PhoneNumber: "+34 972 59 05 07",
    NationalPhoneNumber: "972 59 05 07",
    Website: "http://www.albacolonies.com/",
    GoogleMapsUrl: "https://maps.google.com/?cid=123456",
    Rating: 4.2m,
    RatingCount: 113,
    BusinessStatus: "OPERATIONAL",
    AddressComponents: [
        new("Crta Pujarnol, km 5", "Crta Pujarnol, km 5", ["route"]),
        new("Pujarnol", "Pujarnol", ["locality", "political"]),
        new("Girona", "Girona", ["administrative_area_level_2", "political"]),
        new("17834", "17834", ["postal_code"]),
        new("España", "ES", ["country", "political"])
    ],
    Photos: [
        new("photo_ref_1", 1200, 900, ["<a href=\"https://maps.google.com/maps/contrib/123\">John Doe</a>"]),
        new("photo_ref_2", 800, 600, ["<a href=\"https://maps.google.com/maps/contrib/456\">Jane Smith</a>"])
    ]
);
```

---

### Step 14: Update `CampsServiceTests`

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`
- **Action**: Update constructor to mock new dependencies, add new test cases

#### Constructor update

```csharp
private readonly ICampsRepository _repository;
private readonly IGooglePlacesService _googlePlacesService;
private readonly IGooglePlacesMapperService _mapper;
private readonly CampsService _sut;

public CampsServiceTests()
{
    _repository = Substitute.For<ICampsRepository>();
    _googlePlacesService = Substitute.For<IGooglePlacesService>();
    _mapper = Substitute.For<IGooglePlacesMapperService>();
    _sut = new CampsService(_repository, _googlePlacesService, _mapper);
}
```

#### New test cases to add

```csharp
[Fact]
public async Task CreateAsync_WithGooglePlaceId_CallsGooglePlacesAndEnrichsCamp()
// Setup: request with GooglePlaceId, _googlePlacesService returns details, _mapper returns data + photos
// Assert: _googlePlacesService.GetPlaceDetailsAsync called once
//         _mapper.MapToCampData called once
//         _mapper.MapToPhotos called once
//         _repository.AddPhotosAsync called with photos
//         returned CampDetailResponse has FormattedAddress populated

[Fact]
public async Task CreateAsync_WithoutGooglePlaceId_SkipsGoogleEnrichment()
// Setup: request with null GooglePlaceId
// Assert: _googlePlacesService.GetPlaceDetailsAsync NOT called
//         _repository.AddPhotosAsync NOT called

[Fact]
public async Task CreateAsync_WithGooglePlaceId_GoogleApiUnavailable_ThrowsExternalServiceException()
// Setup: _googlePlacesService.GetPlaceDetailsAsync throws ExternalServiceException
// Assert: exception propagates

[Fact]
public async Task GetByIdAsync_WhenCampExists_CallsGetByIdWithPhotosAsync()
// Assert: _repository.GetByIdWithPhotosAsync called (not GetByIdAsync)

[Fact]
public async Task GetByIdAsync_WhenCampExists_ReturnsCampDetailResponse()
// Assert: result is CampDetailResponse with Photos list (even if empty)

[Fact]
public async Task GetByIdAsync_WhenCampDoesNotExist_ReturnsNull()
```

#### Update existing `CreateAsync` tests

Existing tests that create `CampResponse` assertions need to be updated to `CampDetailResponse`.

---

### Step 15: Update Technical Documentation

- **Action**: Review and update relevant documentation files after implementation is complete

**1. `ai-specs/specs/data-model.md`**
- Add `camp_photos` table definition (all columns, types, constraints, indexes)
- Update `camps` table definition to include the 14 new nullable columns
- Add `camp_photos` → `camps` FK relationship

**2. `ai-specs/specs/api-endpoints.md`**
- Update `GET /api/camps/{id}` response schema to show `CampDetailResponse` fields (including Photos array)
- Update `POST /api/camps` response schema to show `CampDetailResponse`
- Note that `GET /api/camps` (list) returns `CampResponse` (lightweight, no Photos)

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-camps-extra-data-backend`
2. **Step 1**: Extend `GooglePlacesService` — `PlaceDetails` DTO + API fields
3. **Step 2**: Create `GooglePlacesMapperService` + `CampGoogleData` record
4. **Step 3**: Add new fields to `Camp` entity
5. **Step 4**: Create `CampPhoto` entity
6. **Step 5**: Add new DTOs (`CampPhotoResponse`, update `CampResponse`, add `CampDetailResponse`)
7. **Step 6a**: Update `CampConfiguration.cs` with new columns + relationship
8. **Step 6b**: Create `CampPhotoConfiguration.cs`
9. **Step 7**: Add `CampPhotos` DbSet to `AbuviDbContext`
10. **Step 8**: Generate and verify EF Core migration `AddExtendedCampInformation`
11. **Step 9**: Update `ICampsRepository` + `CampsRepository` (new methods)
12. **Step 10**: Update `CampsService` (new deps, enrichment logic, detail DTO)
13. **Step 11**: Update `CampsEndpoints` (return types)
14. **Step 12**: Register `GooglePlacesMapperService` in `Program.cs`
15. **Step 13**: Write `GooglePlacesMapperServiceTests`
16. **Step 14**: Update `CampsServiceTests`
17. **Step 15**: Update documentation

---

## Testing Checklist

### Unit Tests (xUnit + FluentAssertions + NSubstitute)

- [ ] `GooglePlacesMapperServiceTests` — all 12 test cases pass
- [ ] `CampsServiceTests` — existing tests updated + 6 new tests pass
- [ ] All test method names follow `MethodName_StateUnderTest_ExpectedBehavior` pattern
- [ ] `CampsServiceTests` constructor uses NSubstitute for all 3 dependencies

### Integration Verification

- [ ] `dotnet build src/Abuvi.API` — no compilation errors
- [ ] `dotnet build src/Abuvi.Tests` — no compilation errors
- [ ] `dotnet test` — all tests pass
- [ ] Migration applies cleanly: `dotnet ef database update --project src/Abuvi.API`
- [ ] `GET /api/camps` returns all existing camps with new nullable fields (null for old camps)
- [ ] `GET /api/camps/{id}` returns `CampDetailResponse` with empty Photos array for existing camps
- [ ] `POST /api/camps` without GooglePlaceId — creates camp, returns `CampDetailResponse` with nulls, no Google API called
- [ ] `POST /api/camps` with valid GooglePlaceId — creates camp, backend enriches from Google, returns detail with address + photos

### Regression Verification

- [ ] All existing camp endpoints still work (GET all, GET by id, POST, PUT, DELETE)
- [ ] Camp edition endpoints unaffected
- [ ] Association settings endpoints unaffected
- [ ] Google Places autocomplete/details endpoints still work (and now return richer PlaceDetails)

---

## Error Response Format

All endpoints follow the existing `ApiResponse<T>` envelope:

```json
// Success
{ "success": true, "data": { ... }, "error": null }

// Not found (404)
{ "success": false, "data": null, "error": { "message": "No se encontró el campamento", "code": "NOT_FOUND" } }

// Google API failure (500 via GlobalExceptionMiddleware)
{ "success": false, "data": null, "error": { "message": "An unexpected error occurred", "code": "INTERNAL_ERROR" } }
```

Error messages for user-facing errors must be in **Spanish** (per backend-standards.mdc).

---

## Dependencies

### NuGet Packages

No new NuGet packages required. All necessary packages are already present:
- `Microsoft.EntityFrameworkCore.Relational` — for `Include()`
- `System.Text.Json` — for JSON serialization of PlaceTypes
- `System.Text.RegularExpressions` — for HTML attribution parsing

### EF Core Migration Commands

```bash
# Generate migration
dotnet ef migrations add AddExtendedCampInformation --project src/Abuvi.API

# Review generated migration file before applying!

# Apply to dev database
dotnet ef database update --project src/Abuvi.API

# Generate SQL for staging/production (for DBA review)
dotnet ef migrations script --idempotent --project src/Abuvi.API -o migration.sql
```

---

## Notes

### Business Rules

- **All extended fields are optional**: Camps created without `GooglePlaceId` are fully functional — all new fields are nullable with no default value (except navigation collections).
- **Auto-enrichment is one-shot at creation**: Phase 5 (sync) handles updates. For MVP, enrichment only runs at `CreateAsync` time.
- **Photos are references only (Phase 1)**: `PhotoUrl` is null; `PhotoReference` is the Google token. Frontend must build the photo URL using `/api/places/photo?reference={ref}&maxwidth={w}`.
- **Attribution is required**: Google Places API Terms of Service require displaying photo attribution. The `AttributionName` field is non-nullable and must always be populated.
- **Google API failures on creation**: If Google Places API is unavailable when creating a camp with a `GooglePlaceId`, the camp is created without enrichment (consider catching `ExternalServiceException` in `EnrichFromGooglePlacesAsync` and logging a warning instead of propagating). This is a **design decision** for the implementor to confirm.
- **PlaceTypes storage**: Stored as a JSON string (e.g. `"[\"campground\",\"lodging\"]"`) to avoid a separate join table for this low-cardinality data.

### GDPR Considerations

- All data stored (address, phone, website, rating, photos) is **public Google Places data** — no GDPR concerns.
- Do NOT store Google review text — this may contain personal information (per spec: "Won't Have").
- Photo attribution names are publicly visible on Google Maps — acceptable to store.

### Language Requirements

- All C# code, comments, logs: **English**
- User-facing error messages (validation, API errors): **Spanish**
- Example: `"No se encontró el campamento"` (not `"Camp not found"`)

### Breaking Changes

- **None**: All new Camp fields are nullable. `CampResponse` is extended (additive). Existing clients that parse `CampResponse` will still work — new fields are simply ignored.
- `GET /api/camps/{id}` response type changes from `CampResponse` to `CampDetailResponse`. Frontend TypeScript types need to be updated (handled in the separate frontend ticket).

---

## Next Steps After Implementation

1. **Frontend ticket**: Update TypeScript types for `CampDetailResponse` and `CampPhotoResponse`; implement `CampContactInfo.vue` and `CampPhotoGallery.vue` components (see `feat-camps-extra-data` user story — Phases 3 & 4).
2. **Phase 5 (Post-MVP)**: Implement `CampSyncService` for individual and batch sync operations.
3. **Photo URL proxy endpoint**: Consider adding `GET /api/places/photo?reference={ref}&maxwidth={w}` endpoint that proxies Google Places Photo API (keeps Google API key server-side).

---

## Implementation Verification

### Code Quality

- [ ] All new C# files use file-scoped namespaces (`namespace Abuvi.API.Features.Camps;`)
- [ ] Nullable reference types enabled and handled (all nullable fields declared with `?`)
- [ ] Primary constructors used where appropriate
- [ ] No `var` abuse — explicit types used when not obvious from the right-hand side
- [ ] Structured logging with named parameters (no string interpolation in log calls)
- [ ] All async methods accept and propagate `CancellationToken`

### Functionality

- [ ] `GET /api/camps` → 200 with list including null extended fields for legacy camps
- [ ] `GET /api/camps/{id}` → 200 with `CampDetailResponse` including Photos (empty array if none)
- [ ] `GET /api/camps/{nonexistent-id}` → 404 with Spanish error message
- [ ] `POST /api/camps` (no GooglePlaceId) → 201 with `CampDetailResponse`, no Google API calls
- [ ] `POST /api/camps` (with GooglePlaceId) → 201 with enriched `CampDetailResponse` including photos
- [ ] `PUT /api/camps/{id}` → 200 (unchanged behavior; does not re-enrich from Google)
- [ ] `DELETE /api/camps/{id}` → 204, associated photos cascade-deleted

### Testing

- [ ] 90%+ coverage on new code (`GooglePlacesMapperService`, new service methods, new repository methods)
- [ ] All tests follow `MethodName_StateUnderTest_ExpectedBehavior` naming
- [ ] Tests use AAA pattern consistently
- [ ] NSubstitute used for all interface dependencies in service tests
- [ ] No real database or external HTTP calls in unit tests

### Integration

- [ ] EF Core migration `AddExtendedCampInformation` applied successfully
- [ ] `camp_photos` table created with correct schema and indexes
- [ ] No orphaned photos if `CampPhoto.Camp` is deleted (cascade delete confirmed)

### Documentation

- [ ] `ai-specs/specs/data-model.md` updated with CampPhoto table and new Camp columns
- [ ] `ai-specs/specs/api-endpoints.md` updated with new response schemas

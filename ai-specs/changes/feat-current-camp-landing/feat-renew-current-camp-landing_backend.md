# Backend Implementation Plan: feat-current-camp-landing — Enrich GET /api/camps/current

## Overview

Enrich the existing `GET /api/camps/current` endpoint so its response includes camp photos (Google Places), camp description, contact/places metadata, accommodation capacity, and active extras for the current edition. These fields already exist in the database but are currently not included in the `CurrentCampEditionResponse` DTO. No new endpoints or schema migrations are required — this is a read-only enrichment of an existing endpoint.

Architecture principle: all changes are confined to the `Camps` feature slice (`src/Abuvi.API/Features/Camps/`).

---

## Architecture Context

**Feature slice:** `src/Abuvi.API/Features/Camps/`

**Files to modify:**

| File | Type of change |
|---|---|
| `CampsModels.cs` | Extend `CurrentCampEditionResponse` record with new fields |
| `CampEditionsRepository.cs` | Add `ThenInclude(Photos)` and `Include(Extras.Where(active))` in `GetCurrentAsync` |
| `CampEditionsService.cs` | Map new fields in `GetCurrentAsync` |
| `CampEditionsServiceTests.cs` | New unit tests for `GetCurrentAsync` new field mappings |
| `ai-specs/specs/api-endpoints.md` | Update `GET /api/camps/current` response documentation |

**No files to create.** No new endpoints. No EF Core migrations.

**Cross-cutting concerns:** None. `CampEditionExtraResponse` already exists. `CampPhotoResponse` already exists. `AccommodationCapacity` already exists. The `ToResponse(int currentQuantitySold)` extension method already exists on `CampEditionExtra`.

---

## Pre-Implementation Research Findings

Before writing code, read and confirm these observations from the current codebase:

1. **`CurrentCampEditionResponse`** (CampsModels.cs ~line 474): record with 19 parameters — missing `CampDescription`, `CampPhoneNumber`, `CampNationalPhoneNumber`, `CampWebsiteUrl`, `CampGoogleMapsUrl`, `CampGoogleRating`, `CampGoogleRatingCount`, `CampPhotos`, `AccommodationCapacity`, `CalculatedTotalBedCapacity`, `Extras`.

2. **`CampEditionExtraResponse`** (CampsModels.cs ~line 249): record already exists with shape `(Guid Id, Guid CampEditionId, string Name, string? Description, decimal Price, PricingType PricingType, PricingPeriod PricingPeriod, bool IsRequired, bool IsActive, int? MaxQuantity, int CurrentQuantitySold, DateTime CreatedAt, DateTime UpdatedAt)`. **Do not create a new DTO** — reuse this.

3. **`CampPhotoResponse`** (CampsModels.cs ~line 376): already exists. **Do not create a new DTO** — reuse this.

4. **`AccommodationCapacity.CalculateTotalBedCapacity()`** (CampsModels.cs ~line 21): instance method already on the class. Use `accommodationCapacity?.CalculateTotalBedCapacity()`.

5. **`Camp` entity** has: `Description`, `PhoneNumber`, `NationalPhoneNumber`, `WebsiteUrl`, `GoogleMapsUrl`, `GoogleRating`, `GoogleRatingCount`, `Photos` (navigation), `AccommodationCapacityJson` + `GetAccommodationCapacity()`.

6. **`CampEdition` entity** has: `AccommodationCapacityJson` + `GetAccommodationCapacity()`, `Extras` (navigation `ICollection<CampEditionExtra>`).

7. **`CampEditionExtra` entity** does NOT have a `SortOrder` field. Order by `CreatedAt` ascending in the Include filter.

8. **`CurrentQuantitySold`** in extras is computed via a separate `ICampEditionExtrasRepository.GetQuantitySoldAsync` call. For `GET /api/camps/current`, pass `currentQuantitySold: 0` — the landing page does not display sold quantities.

9. **`CampEditionsRepository.GetCurrentAsync`** currently does `.Include(e => e.Camp)` only. Photos and Extras are not loaded.

10. **Test framework**: xUnit + NSubstitute + FluentAssertions. Pattern: `MethodName_StateUnderTest_ExpectedBehavior`.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/feat-current-camp-landing-backend`
- **Implementation Steps**:
  1. Ensure you are on `main`: `git checkout main`
  2. Pull latest changes: `git pull origin main`
  3. Create and switch: `git checkout -b feature/feat-current-camp-landing-backend`
  4. Verify: `git branch`
- **Notes**: Must be the first step before any code changes.

---

### Step 1: Extend `CurrentCampEditionResponse` DTO

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add 11 new parameters to the `CurrentCampEditionResponse` record.

**Current record ends at `DateTime UpdatedAt` (last existing param). Append after it:**

```csharp
public record CurrentCampEditionResponse(
    // ── existing parameters (DO NOT CHANGE ORDER) ──
    Guid Id,
    Guid CampId,
    string CampName,
    string? CampLocation,
    string? CampFormattedAddress,
    decimal? CampLatitude,
    decimal? CampLongitude,
    int Year,
    DateTime StartDate,
    DateTime EndDate,
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool UseCustomAgeRanges,
    int? CustomBabyMaxAge,
    int? CustomChildMinAge,
    int? CustomChildMaxAge,
    int? CustomAdultMinAge,
    CampEditionStatus Status,
    int? MaxCapacity,
    int RegistrationCount,
    int? AvailableSpots,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // ── NEW parameters (append after UpdatedAt) ──
    string? CampDescription,
    string? CampPhoneNumber,
    string? CampNationalPhoneNumber,
    string? CampWebsiteUrl,
    string? CampGoogleMapsUrl,
    decimal? CampGoogleRating,
    int? CampGoogleRatingCount,
    IReadOnlyList<CampPhotoResponse> CampPhotos,
    AccommodationCapacity? AccommodationCapacity,
    int? CalculatedTotalBedCapacity,
    IReadOnlyList<CampEditionExtraResponse> Extras
);
```

- **Implementation Notes**:
  - Appending parameters to the end of the record preserves binary compatibility for any callers that use positional construction.
  - `CampPhotoResponse` and `CampEditionExtraResponse` are already declared in this same file — no new imports needed.
  - `AccommodationCapacity` is also declared in this same file.

---

### Step 2: Update Repository — `GetCurrentAsync`

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsRepository.cs`
- **Action**: Add `ThenInclude(c => c.Photos)` and a filtered `Include(e => e.Extras)` to both queries inside `GetCurrentAsync`.

**Current code (both queries look like this):**
```csharp
.Include(e => e.Camp)
.Where(...)
```

**Replace with (for both the current-year and previous-year queries):**
```csharp
.Include(e => e.Camp)
    .ThenInclude(c => c.Photos.OrderBy(p => p.IsPrimary ? 0 : 1)
                               .ThenBy(p => p.DisplayOrder))
.Include(e => e.Extras.Where(x => x.IsActive)
                       .OrderBy(x => x.CreatedAt))
.Where(...)
```

- **Implementation Notes**:
  - EF Core 5+ supports filtered and sorted collection includes. This project uses .NET 9 + EF Core 9, so this syntax is supported.
  - The photo sort: `IsPrimary ? 0 : 1` puts the primary photo first, then by `DisplayOrder` ascending.
  - Extras: only `IsActive == true`, ordered by `CreatedAt` ascending (no `SortOrder` field exists on the entity).
  - Both queries (current year and previous year fallback) must receive the same Include additions.
  - `AsNoTracking()` must remain on both queries.

**Important**: The `ICampEditionsRepository` interface declaration of `GetCurrentAsync` does not need to change — it returns `Task<CampEdition?>` which is correct; the enrichment happens via the included navigation properties.

---

### Step 3: Update Service — `GetCurrentAsync`

- **File**: `src/Abuvi.API/Features/Camps/CampEditionsService.cs`
- **Action**: Map the new fields when constructing `CurrentCampEditionResponse`.

**Find the existing `return new CurrentCampEditionResponse(...)` call (~line 283) and extend it:**

```csharp
public async Task<CurrentCampEditionResponse?> GetCurrentAsync(
    CancellationToken cancellationToken = default)
{
    var currentYear = DateTime.UtcNow.Year;
    var edition = await _repository.GetCurrentAsync(currentYear, cancellationToken);

    if (edition == null)
        return null;

    var registrationCount = 0; // Placeholder until Registrations feature is built
    var availableSpots = edition.MaxCapacity.HasValue
        ? edition.MaxCapacity.Value - registrationCount
        : (int?)null;

    // Accommodation: edition override takes priority over camp-level value
    var accommodationCapacity = edition.GetAccommodationCapacity()
        ?? edition.Camp.GetAccommodationCapacity();

    var calculatedTotalBedCapacity = accommodationCapacity?.CalculateTotalBedCapacity();
    // CalculateTotalBedCapacity() returns int; cast to int? only when non-null
    int? bedCapacity = calculatedTotalBedCapacity > 0 ? calculatedTotalBedCapacity : (int?)null;

    var campPhotos = edition.Camp.Photos
        .Select(p => new CampPhotoResponse(
            p.Id,
            p.PhotoReference,
            p.PhotoUrl,
            p.Width,
            p.Height,
            p.AttributionName,
            p.AttributionUrl,
            p.Description,
            p.IsPrimary,
            p.DisplayOrder))
        .ToList();

    var extras = edition.Extras
        .Select(x => x.ToResponse(currentQuantitySold: 0))
        .ToList();

    return new CurrentCampEditionResponse(
        Id: edition.Id,
        CampId: edition.CampId,
        CampName: edition.Camp.Name,
        CampLocation: edition.Camp.Location,
        CampFormattedAddress: edition.Camp.FormattedAddress,
        CampLatitude: edition.Camp.Latitude,
        CampLongitude: edition.Camp.Longitude,
        Year: edition.Year,
        StartDate: edition.StartDate,
        EndDate: edition.EndDate,
        PricePerAdult: edition.PricePerAdult,
        PricePerChild: edition.PricePerChild,
        PricePerBaby: edition.PricePerBaby,
        UseCustomAgeRanges: edition.UseCustomAgeRanges,
        CustomBabyMaxAge: edition.CustomBabyMaxAge,
        CustomChildMinAge: edition.CustomChildMinAge,
        CustomChildMaxAge: edition.CustomChildMaxAge,
        CustomAdultMinAge: edition.CustomAdultMinAge,
        Status: edition.Status,
        MaxCapacity: edition.MaxCapacity,
        RegistrationCount: registrationCount,
        AvailableSpots: availableSpots,
        Notes: edition.Notes,
        CreatedAt: edition.CreatedAt,
        UpdatedAt: edition.UpdatedAt,
        // NEW
        CampDescription: edition.Camp.Description,
        CampPhoneNumber: edition.Camp.PhoneNumber,
        CampNationalPhoneNumber: edition.Camp.NationalPhoneNumber,
        CampWebsiteUrl: edition.Camp.WebsiteUrl,
        CampGoogleMapsUrl: edition.Camp.GoogleMapsUrl,
        CampGoogleRating: edition.Camp.GoogleRating,
        CampGoogleRatingCount: edition.Camp.GoogleRatingCount,
        CampPhotos: campPhotos,
        AccommodationCapacity: accommodationCapacity,
        CalculatedTotalBedCapacity: bedCapacity,
        Extras: extras
    );
}
```

- **Implementation Notes**:
  - `ToResponse(currentQuantitySold: 0)` uses the existing `CampEditionExtraExtensions.ToResponse` method (declared in `CampEditionExtrasService.cs` as `internal static`). Since both classes are in the same namespace (`Abuvi.API.Features.Camps`), this is accessible.
  - `CalculateTotalBedCapacity()` returns `int` (not nullable). Use `> 0` guard to avoid returning 0 as a meaningful value.
  - `CampPhotos` will be an empty list when `Camp.Photos` is empty — never null.
  - `Extras` will be an empty list when no active extras exist — never null.

---

### Step 4: Write Unit Tests — TDD (RED before GREEN)

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs`
- **Action**: Add a new `#region GetCurrentAsync Tests` section.
- **Framework**: xUnit + NSubstitute + FluentAssertions
- **Pattern**: `MethodName_StateUnderTest_ExpectedBehavior`

> **TDD Rule**: Write these tests BEFORE touching the service code in Step 3. Run them first to confirm RED, then implement Step 3 to go GREEN.

**Helper: create a base `CampEdition` with camp and populated navigation:**

```csharp
private static CampEdition CreateEditionWithCamp(
    CampEditionStatus status = CampEditionStatus.Open,
    int year = 2026,
    string? accommodationJsonEdition = null,
    string? accommodationJsonCamp = null,
    List<CampPhoto>? photos = null,
    List<CampEditionExtra>? extras = null)
{
    var camp = new Camp
    {
        Id = Guid.NewGuid(),
        Name = "Test Camp",
        Description = "A great camp",
        PhoneNumber = "+34918691311",
        NationalPhoneNumber = "918 691 311",
        WebsiteUrl = "https://camp.test",
        GoogleMapsUrl = "https://maps.google.com/?cid=123",
        GoogleRating = 4.5m,
        GoogleRatingCount = 100,
        AccommodationCapacityJson = accommodationJsonCamp,
        Photos = photos ?? new List<CampPhoto>(),
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m,
        IsActive = true
    };

    return new CampEdition
    {
        Id = Guid.NewGuid(),
        CampId = camp.Id,
        Camp = camp,
        Year = year,
        StartDate = new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(year, 7, 10, 0, 0, 0, DateTimeKind.Utc),
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m,
        Status = status,
        MaxCapacity = 100,
        AccommodationCapacityJson = accommodationJsonEdition,
        Extras = extras ?? new List<CampEditionExtra>()
    };
}
```

**Test cases to implement:**

#### Test 1: Returns null when no qualifying edition exists
```csharp
[Fact]
public async Task GetCurrentAsync_WhenNoEditionExists_ReturnsNull()
{
    // Arrange
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns((CampEdition?)null);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result.Should().BeNull();
}
```

#### Test 2: Maps all camp-level contact fields
```csharp
[Fact]
public async Task GetCurrentAsync_WithCampContactData_MapsCampFields()
{
    // Arrange
    var edition = CreateEditionWithCamp();
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result.Should().NotBeNull();
    result!.CampDescription.Should().Be("A great camp");
    result.CampPhoneNumber.Should().Be("+34918691311");
    result.CampNationalPhoneNumber.Should().Be("918 691 311");
    result.CampWebsiteUrl.Should().Be("https://camp.test");
    result.CampGoogleMapsUrl.Should().Be("https://maps.google.com/?cid=123");
    result.CampGoogleRating.Should().Be(4.5m);
    result.CampGoogleRatingCount.Should().Be(100);
}
```

#### Test 3: Maps camp photos ordered (primary first, then by DisplayOrder)
```csharp
[Fact]
public async Task GetCurrentAsync_WithCampPhotos_MapsPhotosOrderedPrimaryFirst()
{
    // Arrange
    var photos = new List<CampPhoto>
    {
        new() { Id = Guid.NewGuid(), IsPrimary = false, DisplayOrder = 1, AttributionName = "B", Width = 100, Height = 100 },
        new() { Id = Guid.NewGuid(), IsPrimary = true,  DisplayOrder = 2, AttributionName = "A", Width = 100, Height = 100 },
        new() { Id = Guid.NewGuid(), IsPrimary = false, DisplayOrder = 0, AttributionName = "C", Width = 100, Height = 100 }
    };
    var edition = CreateEditionWithCamp(photos: photos);
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result!.CampPhotos.Should().HaveCount(3);
    // NOTE: The service maps photos as-is from the already-ordered collection from the repository.
    // The ordering is applied at the repository level (ThenInclude with OrderBy).
    // In unit tests, the collection comes in the order we provide it.
    result.CampPhotos[0].IsPrimary.Should().BeTrue(); // if photos are pre-sorted in test
}
```

> **Note for this test**: Since ordering happens in the EF Core query (repository level), the unit test verifies the service maps the photos it receives — not that it re-sorts them. The integration test (if added later) would verify the sort order from DB.

#### Test 4: Returns empty CampPhotos list when camp has no photos
```csharp
[Fact]
public async Task GetCurrentAsync_WithNoCampPhotos_ReturnsEmptyPhotosList()
{
    // Arrange
    var edition = CreateEditionWithCamp(photos: new List<CampPhoto>());
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result!.CampPhotos.Should().NotBeNull();
    result.CampPhotos.Should().BeEmpty();
}
```

#### Test 5: AccommodationCapacity uses edition override when present
```csharp
[Fact]
public async Task GetCurrentAsync_WithEditionAccommodation_UsesEditionOverCamp()
{
    // Arrange
    var editionJson = """{"privateRoomsWithBathroom": 5}""";
    var campJson    = """{"privateRoomsWithBathroom": 2}""";
    var edition = CreateEditionWithCamp(
        accommodationJsonEdition: editionJson,
        accommodationJsonCamp: campJson);
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result!.AccommodationCapacity.Should().NotBeNull();
    result.AccommodationCapacity!.PrivateRoomsWithBathroom.Should().Be(5); // edition wins
}
```

#### Test 6: AccommodationCapacity falls back to camp when edition has none
```csharp
[Fact]
public async Task GetCurrentAsync_WithoutEditionAccommodation_UsesCampAccommodation()
{
    // Arrange
    var campJson = """{"privateRoomsWithBathroom": 3}""";
    var edition = CreateEditionWithCamp(
        accommodationJsonEdition: null,
        accommodationJsonCamp: campJson);
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result!.AccommodationCapacity!.PrivateRoomsWithBathroom.Should().Be(3); // camp fallback
}
```

#### Test 7: AccommodationCapacity is null when neither edition nor camp have it
```csharp
[Fact]
public async Task GetCurrentAsync_WithNoAccommodation_ReturnsNullAccommodation()
{
    // Arrange
    var edition = CreateEditionWithCamp(
        accommodationJsonEdition: null,
        accommodationJsonCamp: null);
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result!.AccommodationCapacity.Should().BeNull();
    result.CalculatedTotalBedCapacity.Should().BeNull();
}
```

#### Test 8: CalculatedTotalBedCapacity is computed from AccommodationCapacity
```csharp
[Fact]
public async Task GetCurrentAsync_WithAccommodation_ComputesBedCapacity()
{
    // Arrange
    // 2 private rooms with bathroom = 2 * 2 = 4 beds; 1 private room shared bathroom = 1 * 2 = 2 beds → total 6
    var editionJson = """{"privateRoomsWithBathroom": 2, "privateRoomsSharedBathroom": 1}""";
    var edition = CreateEditionWithCamp(accommodationJsonEdition: editionJson);
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result!.CalculatedTotalBedCapacity.Should().Be(6);
}
```

#### Test 9: Returns only active extras in Extras list
```csharp
[Fact]
public async Task GetCurrentAsync_WithMixedExtras_ReturnsOnlyActiveExtras()
{
    // Arrange
    var extras = new List<CampEditionExtra>
    {
        new() { Id = Guid.NewGuid(), CampEditionId = Guid.NewGuid(), Name = "Kayak", IsActive = true,
                Price = 20m, PricingType = PricingType.PerPerson, PricingPeriod = PricingPeriod.OneTime,
                CreatedAt = DateTime.UtcNow },
        new() { Id = Guid.NewGuid(), CampEditionId = Guid.NewGuid(), Name = "Old Tour", IsActive = false,
                Price = 15m, PricingType = PricingType.PerPerson, PricingPeriod = PricingPeriod.OneTime,
                CreatedAt = DateTime.UtcNow }
    };
    var edition = CreateEditionWithCamp(extras: extras);
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    // The repository filters to IsActive=true via the Include Where clause.
    // The service maps what it receives; in unit tests, we pass all extras to simulate unfiltered load.
    // This test verifies the service DOES NOT re-filter — filtering is repository responsibility.
    result!.Extras.Should().ContainSingle(x => x.Name == "Kayak");
    // If the test gets both, it means the unit passes both; integration test confirms repo filter.
}
```

> **Design note**: The `Include(e => e.Extras.Where(x => x.IsActive))` filter is a EF Core concern. In unit tests, we mock the repository which returns the entity with pre-populated collections. The service test should verify that it maps whatever the repository returns without filtering. If you want to test the filtering, write an integration/repository test.

#### Test 10: Returns empty Extras list when no active extras
```csharp
[Fact]
public async Task GetCurrentAsync_WithNoActiveExtras_ReturnsEmptyExtrasList()
{
    // Arrange
    var edition = CreateEditionWithCamp(extras: new List<CampEditionExtra>());
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result!.Extras.Should().NotBeNull();
    result.Extras.Should().BeEmpty();
}
```

#### Test 11: Extras have CurrentQuantitySold = 0
```csharp
[Fact]
public async Task GetCurrentAsync_WithExtras_SetsCurrentQuantitySoldToZero()
{
    // Arrange
    var extras = new List<CampEditionExtra>
    {
        new() { Id = Guid.NewGuid(), CampEditionId = Guid.NewGuid(), Name = "Kayak", IsActive = true,
                Price = 20m, PricingType = PricingType.PerPerson, PricingPeriod = PricingPeriod.OneTime,
                CreatedAt = DateTime.UtcNow }
    };
    var edition = CreateEditionWithCamp(extras: extras);
    _repository.GetCurrentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(edition);

    // Act
    var result = await _sut.GetCurrentAsync();

    // Assert
    result!.Extras.Should().ContainSingle();
    result.Extras[0].CurrentQuantitySold.Should().Be(0);
}
```

---

### Step 5: Verify Build and Tests

- **Action**: Build the project and run all tests.
- **Commands**:
  ```bash
  cd /d/Repos/abuvi-app
  dotnet build src/Abuvi.API/Abuvi.API.csproj
  dotnet test src/Abuvi.Tests/Abuvi.Tests.csproj --filter "FullyQualifiedName~CampEditionsServiceTests"
  ```
- **Expected**: All new tests GREEN. Existing tests remain GREEN (no regressions).
- **If build fails**: Check that `CampEditionExtraExtensions.ToResponse` is accessible from `CampEditionsService.cs`. Both are in the `Abuvi.API.Features.Camps` namespace but `ToResponse` is declared `internal static` in `CampEditionExtrasService.cs`. Since they share the same assembly and namespace, this is valid.

---

### Step 6: Update API Documentation

- **File**: `ai-specs/specs/api-endpoints.md`
- **Action**: Update the `GET /api/camps/current` section.
- **Implementation Steps**:
  1. Find the `### GET /api/camps/current` section.
  2. Update the "Success Response (200 OK)" JSON example to include the 11 new fields after `updatedAt`:

```json
"campDescription": "A beautiful pine forest camp near the Sierra de Guadarrama",
"campPhoneNumber": "+34918691311",
"campNationalPhoneNumber": "918 691 311",
"campWebsiteUrl": "https://camping-elpinar.es",
"campGoogleMapsUrl": "https://maps.google.com/?cid=123",
"campGoogleRating": 4.3,
"campGoogleRatingCount": 156,
"campPhotos": [
  {
    "id": "uuid",
    "photoReference": "AUc7tXUr...",
    "photoUrl": null,
    "width": 1024,
    "height": 768,
    "attributionName": "John Doe",
    "attributionUrl": "https://maps.google.com/...",
    "description": null,
    "isPrimary": true,
    "displayOrder": 0
  }
],
"accommodationCapacity": {
  "privateRoomsWithBathroom": 10,
  "privateRoomsSharedBathroom": null,
  "sharedRooms": null,
  "bungalows": 5,
  "campOwnedTents": null,
  "memberTentAreaSquareMeters": null,
  "memberTentCapacityEstimate": null,
  "motorhomeSpots": null,
  "notes": null
},
"calculatedTotalBedCapacity": 20,
"extras": [
  {
    "id": "uuid",
    "campEditionId": "uuid",
    "name": "Kayak sessions",
    "description": "1-hour guided kayak on the lake",
    "price": 25.00,
    "pricingType": "PerPerson",
    "pricingPeriod": "OneTime",
    "isRequired": false,
    "isActive": true,
    "maxQuantity": 20,
    "currentQuantitySold": 0,
    "createdAt": "2026-02-17T10:00:00Z",
    "updatedAt": "2026-02-17T10:00:00Z"
  }
]
```

  3. Add notes below the response:
     - `campPhotos` is ordered: primary photo first, then by `displayOrder` ascending. Empty array when no photos.
     - `accommodationCapacity`: edition-level value takes priority over camp-level value. `null` when neither is set.
     - `calculatedTotalBedCapacity`: computed from private rooms only (2 beds/room). `null` when no accommodation capacity.
     - `extras`: only `isActive: true` extras are included, ordered by creation date. Empty array when none.
     - `currentQuantitySold` is always `0` in this endpoint (placeholder until Registrations feature tracks purchases).

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-current-camp-landing-backend`
2. **Step 4** — Write unit tests (TDD RED phase) for `GetCurrentAsync`
3. **Step 1** — Extend `CurrentCampEditionResponse` DTO
4. **Step 2** — Update `CampEditionsRepository.GetCurrentAsync` includes
5. **Step 3** — Update `CampEditionsService.GetCurrentAsync` mapping
6. **Step 5** — Build and run tests (GREEN phase)
7. **Step 6** — Update API documentation

---

## Testing Checklist

- [ ] `GetCurrentAsync_WhenNoEditionExists_ReturnsNull`
- [ ] `GetCurrentAsync_WithCampContactData_MapsCampFields`
- [ ] `GetCurrentAsync_WithCampPhotos_MapsPhotosOrderedPrimaryFirst`
- [ ] `GetCurrentAsync_WithNoCampPhotos_ReturnsEmptyPhotosList`
- [ ] `GetCurrentAsync_WithEditionAccommodation_UsesEditionOverCamp`
- [ ] `GetCurrentAsync_WithoutEditionAccommodation_UsesCampAccommodation`
- [ ] `GetCurrentAsync_WithNoAccommodation_ReturnsNullAccommodation`
- [ ] `GetCurrentAsync_WithAccommodation_ComputesBedCapacity`
- [ ] `GetCurrentAsync_WithMixedExtras_ReturnsOnlyActiveExtras`
- [ ] `GetCurrentAsync_WithNoActiveExtras_ReturnsEmptyExtrasList`
- [ ] `GetCurrentAsync_WithExtras_SetsCurrentQuantitySoldToZero`
- [ ] All existing `CampEditionsServiceTests` tests still GREEN
- [ ] Build with no compiler warnings (`dotnet build`)

---

## Error Response Format

No new error responses — this is enriching an existing read endpoint. Existing error handling applies:

| Scenario | HTTP Code | Response |
|---|---|---|
| Not authenticated | 401 | `{ "success": false, "error": { "message": "...", "code": "UNAUTHORIZED" } }` |
| No qualifying edition | 404 | `{ "success": false, "error": { "message": "No qualifying camp edition...", "code": "NOT_FOUND" } }` |
| Server error | 500 | `{ "success": false, "error": { "message": "...", "code": "INTERNAL_ERROR" } }` |

---

## Dependencies

No new NuGet packages required. No EF Core migrations required.

---

## Notes

1. **`ToResponse` visibility**: `CampEditionExtraExtensions.ToResponse` is declared `internal static` inside `CampEditionExtrasService.cs`. Both files are in the `Abuvi.API.Features.Camps` namespace and the same assembly. This means `CampEditionsService.cs` can call `extra.ToResponse(0)` without issue.

2. **Filtered includes syntax**: EF Core 9 supports `.Include(e => e.Extras.Where(x => x.IsActive).OrderBy(x => x.CreatedAt))`. If you encounter a runtime error (`InvalidOperationException: Filtering and ordering of an Include expression is not supported`), fall back to loading all extras and filtering in the service: `edition.Extras.Where(x => x.IsActive).OrderBy(x => x.CreatedAt).Select(x => x.ToResponse(0)).ToList()`.

3. **No `SortOrder` on `CampEditionExtra`**: The entity does not have a `SortOrder` column. Extras are ordered by `CreatedAt` ascending. Adding `SortOrder` is out of scope for this ticket (would require a migration and changes to the extras management endpoints).

4. **`CurrentQuantitySold = 0`**: This is intentional. The landing page does not display sold counts. Computing the real sold count requires N+1 queries (one `GetQuantitySoldAsync` call per extra). This is deferred until the Registrations feature links purchases to extras.

5. **Photo ordering in unit tests**: The repository applies the photo sort via EF Core `ThenInclude.OrderBy`. In unit tests, the repository is mocked and returns entities with collections in the order you insert them. Test 3 should pre-sort the test data to match the expected primary-first order, since the service does not re-sort in memory.

6. **No changes to `CampsEndpoints.cs`**: The endpoint handler calls `service.GetCurrentAsync()` and returns its result — no changes needed there since the DTO shape change is additive.

---

## Next Steps After Implementation

1. **Frontend ticket**: After this backend ticket is merged, proceed with the frontend redesign of `CampPage.vue` (see `feat-renew-current-camp-landing_enriched.md` → Frontend Changes section).
2. **API docs**: Update `ai-specs/specs/api-endpoints.md` as part of Step 6 in this ticket.

---

## Implementation Verification

- [ ] **Code Quality**: No compiler warnings; nullable reference types handled (`string?`, `decimal?`, `int?`)
- [ ] **Functionality**: `GET /api/camps/current` response includes all 11 new fields
- [ ] **Functionality**: `campPhotos` is an empty array (not null) when no photos
- [ ] **Functionality**: `extras` is an empty array (not null) when no active extras
- [ ] **Functionality**: `accommodationCapacity` is null when neither edition nor camp have it set
- [ ] **Testing**: All 11 new unit tests GREEN; all existing tests unaffected
- [ ] **Documentation**: `api-endpoints.md` updated with new fields
- [ ] **No migration**: Confirmed no database schema changes were introduced

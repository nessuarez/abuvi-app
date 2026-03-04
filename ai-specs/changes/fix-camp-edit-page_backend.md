# Backend Implementation Plan: fix-camp-edit-page — Google Places Refresh & List Response Enhancement

## Overview

This plan covers the **backend-only** changes required for the "Fix Camp Edit Page" enriched story. The majority of the issues identified (BUG 1–4, IMPROVEMENTS 1–5) are **frontend-only** and require no backend modifications. The backend work consists of two items:

1. **IMPROVEMENT 6**: Add a `POST /api/camps/{id}/refresh-places` endpoint to re-sync Google Places data for an existing camp.
2. **Phase 8 (Optional)**: Add `EditionCount` to `CampResponse` for the list endpoint.

**Architecture**: Vertical Slice Architecture — all changes within `src/Abuvi.API/Features/Camps/`.

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Camps/`
- **Files to modify**:
  - `CampsEndpoints.cs` — Register new endpoint
  - `CampsService.cs` — Add `RefreshGooglePlacesAsync` method, change `EnrichFromGooglePlacesAsync` visibility
  - `CampsModels.cs` — Add `EditionCount` to `CampResponse` (optional enhancement)
  - `CampsRepository.cs` / `ICampsRepository.cs` — Add `GetAllWithEditionCountAsync` (optional, only if adding EditionCount)
- **Files to create**:
  - None (all changes fit within existing slice files)
- **Cross-cutting concerns**: None affected — uses existing `ApiResponse<T>` envelope, existing authorization (Board+), existing `IGooglePlacesService`

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/fix-camp-edit-page-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/fix-camp-edit-page-backend`
  4. Verify branch creation: `git branch`
- **Notes**: This must be the FIRST step before any code changes.

---

### Step 1: Change `EnrichFromGooglePlacesAsync` Visibility

- **File**: `src/Abuvi.API/Features/Camps/CampsService.cs`
- **Action**: Change the `EnrichFromGooglePlacesAsync` method from `private` to `private` but extract the core logic into a reusable internal flow that `RefreshGooglePlacesAsync` can also call. The simplest approach: keep it as a single private method and just call it from the new public method within the same class.
- **Implementation Steps**:
  1. No visibility change needed since `RefreshGooglePlacesAsync` will live in the same class (`CampsService`). The existing `private` method is already callable from within the class.
  2. **However**, the current `EnrichFromGooglePlacesAsync` calls `_repository.UpdateAsync` and `_repository.AddPhotosAsync` internally. For the refresh scenario, we need to also **delete existing Google Places photos** before re-adding them, to avoid duplicates.
  3. Refactor `EnrichFromGooglePlacesAsync` to accept an optional `bool clearExistingGooglePhotos = false` parameter, or create a dedicated refresh flow.

- **Current signature** (line 235):

  ```csharp
  private async Task<Camp> EnrichFromGooglePlacesAsync(Camp camp, CancellationToken ct)
  ```

- **Updated signature**:

  ```csharp
  private async Task<Camp> EnrichFromGooglePlacesAsync(
      Camp camp,
      bool clearExistingGooglePhotos = false,
      CancellationToken ct = default)
  ```

- **Implementation Notes**:
  - When `clearExistingGooglePhotos` is `true`, before adding new Google Places photos, delete all existing photos where `IsOriginal == true` (Google-sourced photos) for that camp.
  - This requires a new repository method: `DeleteGooglePhotosAsync(Guid campId, CancellationToken)`.
  - User-uploaded photos (`IsOriginal == false`) must **not** be deleted.

---

### Step 2: Add Repository Method to Delete Google-Sourced Photos

- **File**: `src/Abuvi.API/Features/Camps/ICampsRepository.cs`
- **Action**: Add interface method
- **Function Signature**:

  ```csharp
  /// <summary>
  /// Deletes all Google Places-sourced photos (IsOriginal == true) for a camp
  /// </summary>
  Task<int> DeleteGooglePhotosAsync(Guid campId, CancellationToken cancellationToken = default);
  ```

- **File**: `src/Abuvi.API/Features/Camps/CampsRepository.cs`
- **Action**: Implement the method
- **Implementation Steps**:
  1. Query `CampPhotos` where `CampId == campId && IsOriginal == true`
  2. Remove all matching photos
  3. Save changes
  4. Return count of deleted photos
- **Implementation**:

  ```csharp
  public async Task<int> DeleteGooglePhotosAsync(Guid campId, CancellationToken cancellationToken = default)
  {
      var googlePhotos = await _context.CampPhotos
          .Where(p => p.CampId == campId && p.IsOriginal)
          .ToListAsync(cancellationToken);

      if (googlePhotos.Count == 0) return 0;

      _context.CampPhotos.RemoveRange(googlePhotos);
      await _context.SaveChangesAsync(cancellationToken);

      return googlePhotos.Count;
  }
  ```

---

### Step 3: Implement `RefreshGooglePlacesAsync` Service Method

- **File**: `src/Abuvi.API/Features/Camps/CampsService.cs`
- **Action**: Add public method for refreshing Google Places data on an existing camp
- **Function Signature**:

  ```csharp
  /// <summary>
  /// Re-syncs Google Places data (address, phone, rating, photos) for an existing camp.
  /// Requires the camp to have a GooglePlaceId.
  /// </summary>
  public async Task<CampDetailResponse?> RefreshGooglePlacesAsync(
      Guid id,
      CancellationToken cancellationToken = default)
  ```

- **Implementation Steps**:
  1. **Retrieve camp**: Use `_repository.GetByIdWithPhotosAsync(id, ct)` — returns `null` if not found
  2. **Validate GooglePlaceId**: If `camp.GooglePlaceId` is null or whitespace, throw `BusinessRuleException("Camp does not have a Google Place ID. Cannot refresh Google Places data.")`
  3. **Delete existing Google photos**: Call `_repository.DeleteGooglePhotosAsync(id, ct)` to remove stale Google Places photos before re-enriching
  4. **Re-enrich from Google Places**: Call the existing `EnrichFromGooglePlacesAsync(camp, ct)` — this fetches fresh data from Google Places API, updates all Google-sourced fields, and adds new photos
  5. **Re-fetch updated camp**: Call `_repository.GetByIdWithPhotosAsync(id, ct)` to get the fully updated entity with new photos loaded
  6. **Return mapped response**: Return `MapToCampDetailResponse(updatedCamp, updatedCamp.Photos)`

- **Full implementation**:

  ```csharp
  public async Task<CampDetailResponse?> RefreshGooglePlacesAsync(
      Guid id,
      CancellationToken cancellationToken = default)
  {
      var camp = await _repository.GetByIdWithPhotosAsync(id, cancellationToken);
      if (camp is null) return null;

      if (string.IsNullOrWhiteSpace(camp.GooglePlaceId))
          throw new BusinessRuleException(
              "Camp does not have a Google Place ID. Cannot refresh Google Places data.");

      // Clear stale Google-sourced photos before re-enriching
      await _repository.DeleteGooglePhotosAsync(id, cancellationToken);

      // Re-enrich from Google Places (updates fields + adds fresh photos)
      camp = await EnrichFromGooglePlacesAsync(camp, cancellationToken);

      // Re-fetch to get updated photos navigation property
      var refreshed = await _repository.GetByIdWithPhotosAsync(id, cancellationToken);
      return refreshed is null ? null : MapToCampDetailResponse(refreshed, refreshed.Photos);
  }
  ```

- **Dependencies**:
  - `BusinessRuleException` from `Abuvi.API.Common.Exceptions` (already imported)
  - `IGooglePlacesService` (already injected)
  - `IGooglePlacesMapperService` (already injected)

- **Implementation Notes**:
  - The existing `EnrichFromGooglePlacesAsync` uses `_repository.UpdateAsync` which calls `_context.Camps.Update(camp)`. Since the camp was fetched with `AsNoTracking()`, this will attach and update correctly.
  - The `EnrichFromGooglePlacesAsync` sets `camp.LastGoogleSyncAt = DateTime.UtcNow` — so the sync timestamp is automatically updated.
  - We re-fetch after enrichment because `EnrichFromGooglePlacesAsync` creates new photo entities, and we need the navigation property to be fresh for the response mapping.

---

### Step 4: Register the Refresh Endpoint

- **File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`
- **Action**: Add `POST /api/camps/{id}/refresh-places` endpoint within the existing `group` MapGroup
- **Implementation Steps**:
  1. Add the endpoint registration in `MapCampsEndpoints` method, after the DELETE endpoint and before the age-ranges section:

     ```csharp
     // POST /api/camps/{id}/refresh-places - Re-sync Google Places data
     group.MapPost("/{id:guid}/refresh-places", RefreshGooglePlaces)
         .WithName("RefreshGooglePlaces")
         .WithSummary("Re-sync camp data from Google Places API")
         .Produces<ApiResponse<CampDetailResponse>>()
         .Produces(400)
         .Produces(401)
         .Produces(403)
         .Produces(404);
     ```

  2. Add the handler method:

     ```csharp
     /// <summary>
     /// Re-syncs Google Places data for an existing camp
     /// </summary>
     private static async Task<IResult> RefreshGooglePlaces(
         Guid id,
         CampsService campsService)
     {
         try
         {
             var result = await campsService.RefreshGooglePlacesAsync(id);
             return result is null
                 ? TypedResults.NotFound(ApiResponse<CampDetailResponse>.Fail("Camp not found"))
                 : TypedResults.Ok(ApiResponse<CampDetailResponse>.Ok(result));
         }
         catch (BusinessRuleException ex)
         {
             return TypedResults.BadRequest(
                 ApiResponse<CampDetailResponse>.Fail(ex.Message));
         }
     }
     ```

- **Implementation Notes**:
  - The endpoint inherits the group's `RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))` — Board+ access only, matching the requirement.
  - No request body needed — the camp's existing `GooglePlaceId` is used.
  - Returns `200 OK` with updated `CampDetailResponse` on success.
  - Returns `404` if camp not found.
  - Returns `400` if camp has no `GooglePlaceId`.

---

### Step 5: Add `EditionCount` to `CampResponse` (Optional Enhancement)

- **File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`
- **Action**: Add `EditionCount` field to the `CampResponse` record
- **Updated record**:

  ```csharp
  public record CampResponse(
      Guid Id,
      string Name,
      string? Description,
      string? Location,
      decimal? Latitude,
      decimal? Longitude,
      string? GooglePlaceId,
      string? FormattedAddress,
      string? PhoneNumber,
      string? WebsiteUrl,
      string? GoogleMapsUrl,
      decimal? GoogleRating,
      int? GoogleRatingCount,
      string? BusinessStatus,
      decimal PricePerAdult,
      decimal PricePerChild,
      decimal PricePerBaby,
      bool IsActive,
      DateTime CreatedAt,
      DateTime UpdatedAt,
      int EditionCount  // NEW: number of editions for quick list reference
  );
  ```

- **File**: `src/Abuvi.API/Features/Camps/CampsService.cs`
- **Action**: Update `MapToCampResponse` to include `EditionCount`
- **Implementation Steps**:
  1. Update the `MapToCampResponse` mapper (line 270):

     ```csharp
     private static CampResponse MapToCampResponse(Camp camp) => new(
         // ... all existing fields ...
         UpdatedAt: camp.UpdatedAt,
         EditionCount: camp.Editions.Count  // NEW
     );
     ```

  2. The `Editions` navigation property needs to be loaded. Check if `GetAllAsync` in the repository includes editions.

- **File**: `src/Abuvi.API/Features/Camps/CampsRepository.cs`
- **Action**: Update `GetAllAsync` to include `Editions` for counting
- **Implementation Steps**:
  1. Add `.Include(c => c.Editions)` to the query in `GetAllAsync` (line 32):

     ```csharp
     var query = _context.Camps.AsNoTracking().Include(c => c.Editions);
     ```

  2. **Alternative (better performance)**: Use a projection to only count editions without loading all edition entities. However, since `MapToCampResponse` works on `Camp` entities, and edition count is a simple `.Count`, including editions is acceptable for the current data scale.

- **Implementation Notes**:
  - This is a **non-breaking API change** — adding a new field to the response. Existing frontend code will simply ignore the new field until updated.
  - If performance becomes a concern with many editions, switch to a Select projection with a subquery count.

---

### Step 6: Write Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceRefreshGooglePlacesTests.cs` (new file)
- **Action**: Create unit tests for the new `RefreshGooglePlacesAsync` method
- **Implementation Steps**:
  1. **Test class setup**: Use NSubstitute to mock `ICampsRepository`, `IGooglePlacesService`, `IGooglePlacesMapperService`, `IUsersRepository`
  2. Create test data builders for `Camp`, `CampPhoto`, `PlaceDetails` (Google Places response)

- **Test Cases**:

  #### Successful Cases

  ```
  RefreshGooglePlacesAsync_ValidCampWithGooglePlaceId_ReturnsUpdatedResponse
  RefreshGooglePlacesAsync_ValidCamp_DeletesExistingGooglePhotosBeforeReEnriching
  RefreshGooglePlacesAsync_ValidCamp_PreservesUserUploadedPhotos
  RefreshGooglePlacesAsync_ValidCamp_UpdatesLastGoogleSyncAt
  RefreshGooglePlacesAsync_GoogleReturnsNewPhotos_AddsNewPhotos
  ```

  #### Not Found

  ```
  RefreshGooglePlacesAsync_NonExistentCamp_ReturnsNull
  ```

  #### Business Rule Violations

  ```
  RefreshGooglePlacesAsync_CampWithoutGooglePlaceId_ThrowsBusinessRuleException
  RefreshGooglePlacesAsync_CampWithEmptyGooglePlaceId_ThrowsBusinessRuleException
  ```

  #### Edge Cases

  ```
  RefreshGooglePlacesAsync_GooglePlacesServiceReturnsNull_ReturnsResponseWithoutChanges
  RefreshGooglePlacesAsync_CampWithNoExistingPhotos_AddsNewPhotosSuccessfully
  ```

- **File**: `src/Abuvi.Tests/Unit/Features/Camps/CampsServiceMapToCampResponseTests.cs` (new file, only if Step 5 implemented)
- **Action**: Test that `EditionCount` is correctly mapped in `CampResponse`
- **Test Cases**:

  ```
  MapToCampResponse_CampWithEditions_ReturnsCorrectEditionCount
  MapToCampResponse_CampWithNoEditions_ReturnsZeroEditionCount
  ```

- **Dependencies**: xUnit, FluentAssertions, NSubstitute
- **Naming Convention**: `MethodName_StateUnderTest_ExpectedBehavior`
- **Pattern**: AAA (Arrange-Act-Assert)

---

### Step 7: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes made during implementation
  2. **Identify Documentation Files**: Determine which documentation files need updates:
     - API endpoint changes → Update `ai-specs/specs/api-spec.yml` (add `POST /api/camps/{id}/refresh-places`)
     - Data model changes → Update `ai-specs/specs/data-model.md` if it documents `CampResponse` fields
  3. **Update Documentation**: For each affected file:
     - Add the new endpoint to API documentation
     - Document the `EditionCount` field addition to `CampResponse`
     - Ensure proper formatting
  4. **Verify Documentation**:
     - Confirm all changes are accurately reflected
     - Check that documentation follows established structure
  5. **Report Updates**: Document which files were updated and what changes were made
- **References**:
  - Follow process described in `ai-specs/specs/documentation-standards.mdc`
  - All documentation must be written in English
- **Notes**: This step is MANDATORY before considering the implementation complete.

---

## Implementation Order

1. **Step 0**: Create Feature Branch (`feature/fix-camp-edit-page-backend`)
2. **Step 2**: Add `DeleteGooglePhotosAsync` to repository interface and implementation
3. **Step 1**: Verify `EnrichFromGooglePlacesAsync` can be called from new method (no visibility change needed)
4. **Step 3**: Implement `RefreshGooglePlacesAsync` in `CampsService`
5. **Step 4**: Register `POST /{id}/refresh-places` endpoint
6. **Step 5**: Add `EditionCount` to `CampResponse` and update mapper + repository (optional)
7. **Step 6**: Write unit tests
8. **Step 7**: Update technical documentation

## Testing Checklist

- [ ] `RefreshGooglePlacesAsync` returns `null` for non-existent camp
- [ ] `RefreshGooglePlacesAsync` throws `BusinessRuleException` when `GooglePlaceId` is null/empty
- [ ] `RefreshGooglePlacesAsync` deletes existing Google-sourced photos before re-enriching
- [ ] `RefreshGooglePlacesAsync` preserves user-uploaded photos (`IsOriginal == false`)
- [ ] `RefreshGooglePlacesAsync` returns updated `CampDetailResponse` with fresh Google data
- [ ] `RefreshGooglePlacesAsync` updates `LastGoogleSyncAt` timestamp
- [ ] `POST /api/camps/{id}/refresh-places` returns `200` with updated response on success
- [ ] `POST /api/camps/{id}/refresh-places` returns `404` for non-existent camp
- [ ] `POST /api/camps/{id}/refresh-places` returns `400` for camp without Google Place ID
- [ ] `POST /api/camps/{id}/refresh-places` returns `401/403` for unauthorized/insufficient role
- [ ] `CampResponse.EditionCount` correctly reflects the number of editions (if Step 5 implemented)
- [ ] `DeleteGooglePhotosAsync` only deletes photos where `IsOriginal == true`
- [ ] All tests pass with `dotnet test`
- [ ] Build succeeds with `dotnet build`

## Error Response Format

All responses use the `ApiResponse<T>` envelope:

| HTTP Status | Scenario | Response |
|-------------|----------|----------|
| `200` | Successful refresh | `ApiResponse<CampDetailResponse>.Ok(result)` |
| `400` | Camp has no GooglePlaceId | `ApiResponse<CampDetailResponse>.Fail("Camp does not have a Google Place ID...")` |
| `404` | Camp not found | `ApiResponse<CampDetailResponse>.Fail("Camp not found")` |
| `401` | Not authenticated | Standard auth middleware response |
| `403` | Insufficient role (not Board+) | Standard auth middleware response |

## Dependencies

- **NuGet packages**: No new packages required — all dependencies already present
- **EF Core migration**: No schema changes — `CampPhoto.IsOriginal` and all other fields already exist
- **External services**: `IGooglePlacesService` (already configured and injected)

## Notes

- **No breaking changes**: The new endpoint is purely additive. The `EditionCount` addition to `CampResponse` is also additive (no existing fields changed).
- **Google API costs**: Each refresh triggers a Google Places Details API call. Consider rate-limiting or adding a cooldown (e.g., only allow refresh if `LastGoogleSyncAt` is older than 24 hours). This is optional and can be added later if needed.
- **Business rule**: The refresh only updates Google-sourced fields (address, phone, rating, photos, etc.). It does NOT overwrite user-edited fields (Name, Description, pricing, accommodation, contacts, ABUVI tracking).
- **Language**: All code artifacts in English. User-facing error messages in Spanish where applicable (following `BusinessRuleException` pattern — but current service uses English messages for exceptions; follow existing pattern).
- **RGPD**: No sensitive personal data involved in this change.

## Next Steps After Implementation

1. **Frontend ticket**: The companion frontend ticket covers all remaining work (BUG 1–4, IMPROVEMENTS 1–5, frontend for IMPROVEMENT 6). The frontend will call `POST /api/camps/{id}/refresh-places` and consume the `EditionCount` field.
2. **Manual verification**: After deployment to dev, manually test:
   - Navigate to a camp with a GooglePlaceId → click "Actualizar datos de Google" → verify data refreshes
   - Navigate to a camp without a GooglePlaceId → verify the button is not shown (frontend) or returns 400 (API)
3. **Consider adding** a cooldown mechanism if Google API costs become a concern.

## Implementation Verification

- [ ] **Code Quality**: C# analyzers pass, nullable reference types enabled, no warnings
- [ ] **Functionality**: New endpoint returns correct HTTP status codes (200, 400, 404)
- [ ] **Testing**: Unit tests cover all success, failure, and edge cases with xUnit + FluentAssertions + NSubstitute
- [ ] **Integration**: No EF Core migrations needed (no schema changes)
- [ ] **Documentation**: API spec and data model documentation updated

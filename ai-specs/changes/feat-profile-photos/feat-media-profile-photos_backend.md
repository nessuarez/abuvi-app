# Backend Implementation Plan: feat-media-profile-photos — Profile Photos for FamilyMember and FamilyUnit

## Overview

This feature adds an optional `ProfilePhotoUrl` field to both `FamilyMember` and `FamilyUnit` entities, along with dedicated upload/delete endpoints. It leverages the existing `IBlobStorageService` infrastructure and follows the Vertical Slice Architecture pattern established in the FamilyUnits feature slice.

The profile photo endpoints live within the existing **FamilyUnits** feature slice since FamilyMember and FamilyUnit are already managed there. A new `profile-photos` folder is registered in the `BlobStorageValidator` to restrict uploads to image-only extensions.

---

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/FamilyUnits/` (existing — extend, do not create a new slice)
- **Cross-cutting**: `src/Abuvi.API/Features/BlobStorage/BlobStorageValidator.cs` (add allowed folder)
- **Database**: Two new nullable `varchar(2048)` columns via EF Core migration

### Files to modify

| File | Action |
|------|--------|
| `Features/FamilyUnits/FamilyUnitsModels.cs` | Add `ProfilePhotoUrl` property to `FamilyMember` and `FamilyUnit` entities; add it to response DTOs and `ToResponse()` extensions |
| `Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Add 4 new endpoints (PUT/DELETE profile-photo for members and units) |
| `Features/FamilyUnits/FamilyUnitsService.cs` | Add `UploadProfilePhotoAsync` / `RemoveProfilePhotoAsync` methods for both entity types |
| `Features/FamilyUnits/IFamilyUnitsService.cs` (if exists) or service interface | Add new method signatures |
| `Features/FamilyUnits/FamilyUnitsRepository.cs` | No new methods needed — existing `GetFamilyMemberByIdAsync`, `UpdateFamilyMemberAsync`, `GetFamilyUnitByIdAsync`, `UpdateFamilyUnitAsync` suffice |
| `Data/Configurations/FamilyMemberConfiguration.cs` | Add `ProfilePhotoUrl` column mapping (max 2048) |
| `Data/Configurations/FamilyUnitConfiguration.cs` | Add `ProfilePhotoUrl` column mapping (max 2048) |
| `Features/BlobStorage/BlobStorageValidator.cs` | Add `"profile-photos"` to `AllowedFolders` array |

### Files to create

| File | Purpose |
|------|---------|
| `Abuvi.Tests/Unit/Features/FamilyUnits/ProfilePhotoTests.cs` | Unit tests for upload/delete service methods |
| `Abuvi.Tests/Integration/Features/FamilyUnits/ProfilePhotoEndpointsTests.cs` | Integration tests for authorization and upload flow |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to branch `feat/media-profile-photos-backend`
- **Implementation Steps**:
  1. Ensure latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feat/media-profile-photos-backend`
  3. Verify: `git branch`
- **Notes**: The spec suggests branch `feat/media-profile-photos`. Since this plan covers backend only, use the `-backend` suffix per project convention. If `feat/media-profile-photos` already exists, branch from it instead of `dev`.

---

### Step 1: Add `ProfilePhotoUrl` to Entity Classes

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`
- **Action**: Add nullable `ProfilePhotoUrl` property to both entity classes

**FamilyMember entity** (add after existing properties, before `CreatedAt`):
```csharp
public string? ProfilePhotoUrl { get; set; }
```

**FamilyUnit entity** (add after existing properties, before `CreatedAt`):
```csharp
public string? ProfilePhotoUrl { get; set; }
```

**Response DTOs** — add `string? ProfilePhotoUrl` to both `FamilyMemberResponse` and `FamilyUnitResponse` records.

**Extension methods** — update `ToResponse()` for both entities to map the new field.

- **Implementation Notes**:
  - No validation annotation needed on the entity — it's set by the service, not user input.
  - The field stores the thumbnail URL (400x400 WebP) as the profile display URL.

---

### Step 2: Update EF Core Configurations

- **File**: `src/Abuvi.API/Data/Configurations/FamilyMemberConfiguration.cs`
- **Action**: Add column mapping

```csharp
builder.Property(fm => fm.ProfilePhotoUrl)
    .HasColumnName("profile_photo_url")
    .HasMaxLength(2048)
    .IsRequired(false);
```

- **File**: `src/Abuvi.API/Data/Configurations/FamilyUnitConfiguration.cs`
- **Action**: Same pattern

```csharp
builder.Property(fu => fu.ProfilePhotoUrl)
    .HasColumnName("profile_photo_url")
    .HasMaxLength(2048)
    .IsRequired(false);
```

---

### Step 3: Add `profile-photos` to BlobStorageValidator Allowed Folders

- **File**: `src/Abuvi.API/Features/BlobStorage/BlobStorageValidator.cs`
- **Action**: Add `"profile-photos"` to the `AllowedFolders` array

```csharp
private static readonly string[] AllowedFolders =
    ["photos", "media-items", "camp-locations", "camp-photos", "payment-proofs", "profile-photos"];
```

The `profile-photos` folder falls into the default `_ =>` branch of `IsExtensionAllowed`, which only allows image extensions. This matches the spec requirement (`.jpg`, `.jpeg`, `.png`, `.webp` — `.gif` is also allowed by default, which is acceptable).

- **Implementation Notes**: No changes to `IsExtensionAllowed()` needed. The default case already restricts to `cfg.AllowedImageExtensions`.

---

### Step 4: Implement Profile Photo Service Methods

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`
- **Action**: Add 4 new methods to the service

#### 4a: `UploadFamilyMemberProfilePhotoAsync`

```csharp
public async Task<FamilyMemberResponse> UploadFamilyMemberProfilePhotoAsync(
    Guid familyUnitId, Guid memberId, Guid userId, bool isAdmin,
    IFormFile file, CancellationToken ct)
```

**Implementation Steps**:
1. Load FamilyUnit by `familyUnitId` — return 404 if not found
2. **Authorization**: If not admin, verify `user.familyUnitId == familyUnitId` (representative check via `IsRepresentativeAsync`)
3. Load FamilyMember by `memberId` — return 404 if not found
4. Verify member belongs to the family unit (`member.FamilyUnitId == familyUnitId`)
5. If `member.ProfilePhotoUrl` is not null, extract blob key and delete old blob (and its thumbnail) via `blobStorageService.DeleteManyAsync()`
6. Open file stream: `await using var stream = file.OpenReadStream()`
7. Call `blobStorageService.UploadAsync(stream, file.FileName, file.ContentType, "profile-photos", memberId, true, ct)` — note `generateThumbnail: true`
8. Set `member.ProfilePhotoUrl = result.ThumbnailUrl ?? result.FileUrl` (prefer thumbnail for profile display; fallback to original if thumbnail generation failed)
9. Call `repository.UpdateFamilyMemberAsync(member, ct)`
10. Log the upload
11. Return `member.ToResponse()`

**Key detail — deleting old blobs**: When replacing a photo, both the original and thumbnail must be deleted. Extract two keys:
```csharp
private static List<string> ExtractProfilePhotoBlobKeys(string photoUrl)
{
    var uri = new Uri(photoUrl);
    var key = uri.AbsolutePath.TrimStart('/');
    var keys = new List<string> { key };

    // If this IS the thumbnail URL, also derive the original key
    // If this is the original URL, also derive the thumbnail key
    // Pattern: profile-photos/family-members/{id}/thumbs/{guid}.webp (thumbnail)
    //          profile-photos/family-members/{id}/{guid}.{ext} (original)
    if (key.Contains("/thumbs/"))
    {
        // This is a thumbnail — derive original (we don't know the ext, but we stored thumbnail)
        // Since we store thumbnailUrl, the original was uploaded alongside it
        // The original key shares the same GUID but different extension — we can't easily derive it
        // Solution: store both URLs, or just accept orphaned originals
    }

    return keys;
}
```

**Simplified approach**: Since `BlobStorageService.UploadAsync` generates a GUID and uploads both original + thumbnail with the same GUID, and we store only the thumbnail URL, we cannot reliably derive the original key from the thumbnail URL (different extension). **Best approach**: Store the `result.FileUrl` as well (or accept that the original stays until bucket cleanup).

**Recommended solution**: Store `result.ThumbnailUrl` as `ProfilePhotoUrl`. On re-upload, delete by prefix `profile-photos/family-members/{memberId}/` if the service supports it, OR store both URLs. Since `IBlobStorageService` doesn't support prefix deletion, the simplest approach is:
- Store `result.FileUrl` in `ProfilePhotoUrl` (the original)
- On the next upload, extract the key from the stored URL and delete it
- The thumbnail is at a predictable path relative to the original (same folder + `/thumbs/` + same GUID + `.webp`)
- Delete both keys in one `DeleteManyAsync` call

```csharp
if (member.ProfilePhotoUrl is not null)
{
    var originalKey = new Uri(member.ProfilePhotoUrl).AbsolutePath.TrimStart('/');
    // Derive thumbnail key: replace {guid}.{ext} with thumbs/{guid}.webp
    var dir = originalKey[..originalKey.LastIndexOf('/')];
    var fileName = Path.GetFileNameWithoutExtension(originalKey.Split('/').Last());
    var thumbKey = $"{dir}/thumbs/{fileName}.webp";
    await blobStorageService.DeleteManyAsync([originalKey, thumbKey], ct);
}
```

**Final decision**: Store `result.FileUrl` (original) in `ProfilePhotoUrl`. The frontend can derive thumbnail URL by convention or the API can return both in the response. However, since the spec says "use thumbnail URL as the stored value", follow the spec and store `result.ThumbnailUrl`. Accept that the original blob may become orphaned on replacement (minor storage cost for profile photos). A bucket lifecycle policy can clean these up later.

**Go with spec**: `member.ProfilePhotoUrl = result.ThumbnailUrl ?? result.FileUrl`

For deletion of old photo, delete only the known URL's key. The paired original/thumbnail orphan is acceptable.

#### 4b: `RemoveFamilyMemberProfilePhotoAsync`

```csharp
public async Task RemoveFamilyMemberProfilePhotoAsync(
    Guid familyUnitId, Guid memberId, Guid userId, bool isAdmin,
    CancellationToken ct)
```

**Implementation Steps**:
1. Load and authorize (same as 4a steps 1-4)
2. If `member.ProfilePhotoUrl` is null, return (no-op or 404 — prefer no-op with 204)
3. Extract blob key from URL and delete via `blobStorageService.DeleteManyAsync([key], ct)`
4. Set `member.ProfilePhotoUrl = null`
5. Call `repository.UpdateFamilyMemberAsync(member, ct)`
6. Log the removal

#### 4c: `UploadFamilyUnitProfilePhotoAsync`

Same pattern as 4a but for FamilyUnit entity:
```csharp
public async Task<FamilyUnitResponse> UploadFamilyUnitProfilePhotoAsync(
    Guid familyUnitId, Guid userId, bool isAdmin,
    IFormFile file, CancellationToken ct)
```

- Context ID for blob key: `familyUnitId`
- Folder sub-path: `profile-photos` with contextId `familyUnitId` (produces key `profile-photos/{familyUnitId}/{guid}.{ext}`)
- Authorization: representative of unit OR admin

#### 4d: `RemoveFamilyUnitProfilePhotoAsync`

Same pattern as 4b but for FamilyUnit entity.

- **Dependencies**: `IBlobStorageService` must be injected into `FamilyUnitsService` (add to constructor)

---

### Step 5: Add Profile Photo Endpoints

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`
- **Action**: Add 4 new endpoints to the existing route group

#### Endpoint definitions (add within `MapFamilyUnitsEndpoints`):

```csharp
// Profile photo — Family Member
group.MapPut("/{familyUnitId:guid}/members/{memberId:guid}/profile-photo", UploadMemberProfilePhoto)
    .WithName("UploadMemberProfilePhoto")
    .WithSummary("Upload a profile photo for a family member")
    .DisableAntiforgery()
    .Produces<ApiResponse<FamilyMemberResponse>>()
    .Produces(403).Produces(404);

group.MapDelete("/{familyUnitId:guid}/members/{memberId:guid}/profile-photo", RemoveMemberProfilePhoto)
    .WithName("RemoveMemberProfilePhoto")
    .WithSummary("Remove the profile photo of a family member")
    .Produces(204)
    .Produces(403).Produces(404);

// Profile photo — Family Unit
group.MapPut("/{id:guid}/profile-photo", UploadUnitProfilePhoto)
    .WithName("UploadUnitProfilePhoto")
    .WithSummary("Upload a profile photo for a family unit")
    .DisableAntiforgery()
    .Produces<ApiResponse<FamilyUnitResponse>>()
    .Produces(403).Produces(404);

group.MapDelete("/{id:guid}/profile-photo", RemoveUnitProfilePhoto)
    .WithName("RemoveUnitProfilePhoto")
    .WithSummary("Remove the profile photo of a family unit")
    .Produces(204)
    .Produces(403).Produces(404);
```

#### Endpoint handlers:

```csharp
private static async Task<IResult> UploadMemberProfilePhoto(
    Guid familyUnitId,
    Guid memberId,
    IFormFile file,
    ClaimsPrincipal user,
    IFamilyUnitsService service,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var isAdmin = user.IsInRole("Admin");

    var result = await service.UploadFamilyMemberProfilePhotoAsync(
        familyUnitId, memberId, userId, isAdmin, file, ct);
    return TypedResults.Ok(ApiResponse<FamilyMemberResponse>.Ok(result));
}

private static async Task<IResult> RemoveMemberProfilePhoto(
    Guid familyUnitId,
    Guid memberId,
    ClaimsPrincipal user,
    IFamilyUnitsService service,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var isAdmin = user.IsInRole("Admin");

    await service.RemoveFamilyMemberProfilePhotoAsync(
        familyUnitId, memberId, userId, isAdmin, ct);
    return TypedResults.NoContent();
}

private static async Task<IResult> UploadUnitProfilePhoto(
    [FromRoute(Name = "id")] Guid familyUnitId,
    IFormFile file,
    ClaimsPrincipal user,
    IFamilyUnitsService service,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var isAdmin = user.IsInRole("Admin");

    var result = await service.UploadFamilyUnitProfilePhotoAsync(
        familyUnitId, userId, isAdmin, file, ct);
    return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Ok(result));
}

private static async Task<IResult> RemoveUnitProfilePhoto(
    [FromRoute(Name = "id")] Guid familyUnitId,
    ClaimsPrincipal user,
    IFamilyUnitsService service,
    CancellationToken ct)
{
    var userId = user.GetUserId()
        ?? throw new UnauthorizedAccessException("Usuario no autenticado");
    var isAdmin = user.IsInRole("Admin");

    await service.RemoveFamilyUnitProfilePhotoAsync(
        familyUnitId, userId, isAdmin, ct);
    return TypedResults.NoContent();
}
```

- **Implementation Notes**:
  - `.DisableAntiforgery()` is required for `multipart/form-data` PUT endpoints (same pattern as Payments `UploadProof`)
  - No `ValidationFilter` needed — there's no request DTO body to validate. File validation (size, extension) is handled by the `BlobStorageService` via the `UploadBlobRequestValidator` at upload time. However, since we call `UploadAsync` directly (not through the `/api/blobs/upload` endpoint), we should validate file size and extension in the service method.
  - **Add inline validation in service**: Check `file.Length <= maxFileSize` and extension is in `AllowedImageExtensions` before calling `UploadAsync`. Throw `BusinessRuleException` on failure. Read limits from `IOptions<BlobStorageOptions>`.

---

### Step 6: Add IBlobStorageService Dependency to FamilyUnitsService

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`
- **Action**: Add `IBlobStorageService` and `IOptions<BlobStorageOptions>` to constructor injection

```csharp
public class FamilyUnitsService(
    IFamilyUnitsRepository repository,
    IEncryptionService encryptionService,
    IBlobStorageService blobStorageService,
    IOptions<BlobStorageOptions> blobOptions,
    ILogger<FamilyUnitsService> logger)
```

- **Implementation Notes**: No changes to DI registration in `Program.cs` needed — `IBlobStorageService` is already registered by `BlobStorageExtensions`.

---

### Step 7: Create EF Core Migration

- **Action**: Generate migration after entity and configuration changes

```bash
cd src/Abuvi.API
dotnet ef migrations add AddProfilePhotoUrls
```

- **Expected result**: Two nullable `varchar(2048)` columns:
  - `family_members.profile_photo_url`
  - `family_units.profile_photo_url`

- **Verify**: Inspect the generated migration file to confirm it only adds the two columns with no data loss.

---

### Step 8: Write Unit Tests

- **File**: `src/Abuvi.Tests/Unit/Features/FamilyUnits/ProfilePhotoTests.cs`
- **Framework**: xUnit + FluentAssertions + NSubstitute

#### Test cases:

**Upload FamilyMember Profile Photo:**
1. `UploadFamilyMemberProfilePhoto_RepresentativeUploadsForOwnMember_SetsProfilePhotoUrl` — Happy path
2. `UploadFamilyMemberProfilePhoto_AdminUploadsForAnyMember_SetsProfilePhotoUrl` — Admin bypass
3. `UploadFamilyMemberProfilePhoto_NonRepresentative_ThrowsBusinessRuleException` — 403 scenario
4. `UploadFamilyMemberProfilePhoto_MemberNotFound_ThrowsNotFoundException` — 404 scenario
5. `UploadFamilyMemberProfilePhoto_FamilyUnitNotFound_ThrowsNotFoundException` — 404 scenario
6. `UploadFamilyMemberProfilePhoto_ExistingPhotoPresent_DeletesOldBlobFirst` — Replacement flow
7. `UploadFamilyMemberProfilePhoto_MemberNotInUnit_ThrowsBusinessRuleException` — Cross-unit attempt
8. `UploadFamilyMemberProfilePhoto_InvalidExtension_ThrowsBusinessRuleException` — Non-image file
9. `UploadFamilyMemberProfilePhoto_FileTooLarge_ThrowsBusinessRuleException` — Exceeds limit

**Remove FamilyMember Profile Photo:**
10. `RemoveFamilyMemberProfilePhoto_RepresentativeRemovesOwnMember_ClearsField` — Happy path
11. `RemoveFamilyMemberProfilePhoto_NoExistingPhoto_ReturnsNoContent` — Idempotent
12. `RemoveFamilyMemberProfilePhoto_NonRepresentative_ThrowsBusinessRuleException` — 403

**Upload FamilyUnit Profile Photo:**
13. `UploadFamilyUnitProfilePhoto_RepresentativeUploads_SetsProfilePhotoUrl` — Happy path
14. `UploadFamilyUnitProfilePhoto_AdminUploads_SetsProfilePhotoUrl` — Admin bypass
15. `UploadFamilyUnitProfilePhoto_NonRepresentative_ThrowsBusinessRuleException` — 403
16. `UploadFamilyUnitProfilePhoto_ExistingPhotoPresent_DeletesOldBlobFirst` — Replacement

**Remove FamilyUnit Profile Photo:**
17. `RemoveFamilyUnitProfilePhoto_RepresentativeRemoves_ClearsField` — Happy path
18. `RemoveFamilyUnitProfilePhoto_NonRepresentative_ThrowsBusinessRuleException` — 403

---

### Step 9: Write Integration Tests

- **File**: `src/Abuvi.Tests/Integration/Features/FamilyUnits/ProfilePhotoEndpointsTests.cs`
- **Framework**: xUnit + WebApplicationFactory + NSubstitute (mock `IBlobStorageService`)

#### Test cases:

1. `PUT_MemberProfilePhoto_ByRepresentative_Returns200` — authenticated representative uploads
2. `PUT_MemberProfilePhoto_ByOtherFamily_Returns403` — cross-family attempt
3. `PUT_MemberProfilePhoto_ByAdmin_Returns200` — admin override
4. `PUT_MemberProfilePhoto_Unauthenticated_Returns401`
5. `DELETE_MemberProfilePhoto_ByRepresentative_Returns204`
6. `DELETE_MemberProfilePhoto_ByOtherFamily_Returns403`
7. `PUT_UnitProfilePhoto_ByRepresentative_Returns200`
8. `PUT_UnitProfilePhoto_ByOtherFamily_Returns403`
9. `DELETE_UnitProfilePhoto_ByRepresentative_Returns204`

---

### Step 10: Update Technical Documentation

- **Action**: Review and update technical documentation
- **Implementation Steps**:
  1. **Data model** — Update `ai-specs/specs/data-model.md`:
     - Add `profilePhotoUrl: string?` to FamilyMember and FamilyUnit entity definitions
  2. **API spec** — Update `ai-specs/specs/api-spec.yml` (if manually maintained):
     - Add 4 new endpoints under `/api/family-units/`
  3. **Blob storage docs** — Update any blob storage documentation to include `profile-photos` as an allowed folder with the key schema:
     ```
     profile-photos/{familyMemberId|familyUnitId}/{guid}.{ext}
     profile-photos/{familyMemberId|familyUnitId}/thumbs/{guid}.webp
     ```
  4. **Verify**: Confirm all changes are accurately reflected in documentation
- **Notes**: All documentation must be in English per `documentation-standards.mdc`.

---

## Implementation Order

1. **Step 0** — Create feature branch
2. **Step 1** — Add `ProfilePhotoUrl` to entity classes and DTOs
3. **Step 2** — Update EF Core configurations
4. **Step 3** — Add `profile-photos` to `BlobStorageValidator`
5. **Step 6** — Add `IBlobStorageService` dependency to `FamilyUnitsService`
6. **Step 4** — Implement service methods (upload + delete for both entities)
7. **Step 5** — Add endpoints
8. **Step 7** — Create EF Core migration
9. **Step 8** — Write unit tests
10. **Step 9** — Write integration tests
11. **Step 10** — Update documentation

---

## Testing Checklist

- [ ] All 18 unit tests pass (xUnit + FluentAssertions + NSubstitute)
- [ ] All 9 integration tests pass (WebApplicationFactory)
- [ ] Test coverage >= 90% for new code
- [ ] `dotnet build` succeeds with no warnings
- [ ] `dotnet ef migrations` generates clean migration
- [ ] Manual smoke test: upload via Swagger/Postman with multipart/form-data

---

## Error Response Format

All endpoints use the standard `ApiResponse<T>` envelope:

| Scenario | HTTP Status | Response |
|----------|-------------|----------|
| Successful upload | 200 | `ApiResponse<FamilyMemberResponse>` or `ApiResponse<FamilyUnitResponse>` |
| Successful delete | 204 | No content |
| Validation error (bad file type/size) | 400 | `ApiResponse` with error details |
| Not authenticated | 401 | Standard auth challenge |
| Not authorized (wrong family / insufficient role) | 403 | `ApiResponse` with error message |
| Entity not found | 404 | `ApiResponse` with error message |
| Server error | 500 | `ApiResponse` with generic error |

---

## Dependencies

### NuGet packages (already installed)
- `FluentValidation` — validators
- `SixLabors.ImageSharp` — thumbnail generation (via BlobStorageService)
- `AWSSDK.S3` — blob storage (via BlobStorageRepository)

### EF Core migration command
```bash
cd src/Abuvi.API
dotnet ef migrations add AddProfilePhotoUrls
```

---

## Notes

1. **Blob key schema**: Uses the BlobStorageService's built-in key generation with `folder = "profile-photos"` and `contextId = memberId` or `familyUnitId`. This produces paths like `profile-photos/{contextId}/{guid}.{ext}`.
2. **Thumbnail as stored value**: Per spec, store the thumbnail URL (400x400 WebP) as `ProfilePhotoUrl`. This is adequate for profile display. The original full-size image is also uploaded but its URL is not persisted — it will exist in blob storage but is effectively orphaned on replacement. This is acceptable for profile photos (small number, low storage impact).
3. **No separate sub-folders**: The spec suggests `profile-photos/family-members/{id}/` and `profile-photos/family-units/{id}/` sub-paths. However, `BlobStorageService.UploadAsync` builds keys as `{folder}/{contextId}/{guid}.{ext}`. To achieve the spec's sub-folder structure, pass `folder = "profile-photos/family-members"` or `"profile-photos/family-units"` respectively. **Important**: This requires that `BlobStorageValidator` allows folder names with slashes, or we add both sub-paths to `AllowedFolders`. **Simpler alternative**: Use `"profile-photos"` as the folder and use `contextId` to distinguish. The key becomes `profile-photos/{memberId}/{guid}.{ext}` — member IDs and unit IDs are UUIDs so they won't collide. This is simpler and works with the existing validator without changes.
4. **File validation in service**: Since profile photo upload bypasses the generic `/api/blobs/upload` endpoint (which applies `UploadBlobRequestValidator`), the service must validate file size and image extension inline before calling `UploadAsync`.
5. **Authorization model**: Family representatives can manage photos for their own family members and unit. Admins can manage any. Board members have no upload permission (read-only access to the existing response DTOs which now include `ProfilePhotoUrl`).
6. **RGPD**: Profile photos are not classified as sensitive health data, so no encryption at rest is required. Standard access controls apply.
7. **Error messages**: Follow existing project convention (Spanish user-facing messages in exceptions, English for logs).

---

## Next Steps After Implementation

1. **Frontend ticket**: Implement the profile page UI changes (`FamilyUnitCard.vue`, `FamilyMemberCard.vue`, `ProfilePage.vue`) as described in the spec's Frontend section.
2. **Bucket lifecycle policy** (optional): Configure a lifecycle rule to clean up orphaned original files in the `profile-photos/` prefix that have no corresponding thumbnail reference in the database.

---

## Implementation Verification

- [ ] **Code Quality**: No C# analyzer warnings; nullable reference types handled properly
- [ ] **Functionality**: PUT endpoints return 200 with updated entity; DELETE endpoints return 204
- [ ] **Authorization**: Representative-only write access verified; Admin bypass works; Board gets read-only via existing list endpoints
- [ ] **Blob cleanup**: Re-uploading a photo deletes the previous blob key
- [ ] **Testing**: >= 90% coverage with xUnit + FluentAssertions + NSubstitute
- [ ] **Integration**: EF Core migration applies cleanly; `dotnet ef database update` succeeds
- [ ] **Documentation**: `data-model.md` and API docs updated

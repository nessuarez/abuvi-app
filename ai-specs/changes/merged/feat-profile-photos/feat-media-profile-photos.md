# Follow-up Ticket: Profile Photos for FamilyMember and FamilyUnit

**Depends on:** `feat/blob-storage` (merged)
**Suggested branch:** `feat/media-profile-photos`
**Priority:** Low — useful for recognising attendees in lists and rosters, but not blocking any core workflow

---

## Context

`FamilyMember` (individual person) and `FamilyUnit` (family group) have no photo field in the current data model. This ticket adds an optional profile photo to both entities, primarily useful for:

- Quickly identifying children in the camp attendees roster.
- Personalising the family profile page.

`User` intentionally excluded — the User entity is a platform account, not a person card. Profile photos at the member/family level are more useful.

---

## Data Model Changes

### `FamilyMember`

Add one optional field:

| Field | Type | Constraints |
| --- | --- | --- |
| `profilePhotoUrl` | `string?` | Optional, max 2048 characters, must be a valid URL |

### `FamilyUnit`

Add one optional field:

| Field | Type | Constraints |
| --- | --- | --- |
| `profilePhotoUrl` | `string?` | Optional, max 2048 characters, must be a valid URL |

### Migration

```bash
dotnet ef migrations add AddProfilePhotoUrls --project src/Abuvi.API
```

Two nullable `varchar(2048)` columns, one on `family_members`, one on `family_units`.

---

## Backend

### New endpoints

| Endpoint | Role | Description |
| --- | --- | --- |
| `PUT /api/family-members/{id}/profile-photo` | Family representative (own family only), Admin | Upload a profile photo for a family member |
| `DELETE /api/family-members/{id}/profile-photo` | Family representative (own family only), Admin | Remove the profile photo (deletes blob + clears field) |
| `PUT /api/family-units/{id}/profile-photo` | Family representative (own unit only), Admin | Upload a family group photo |
| `DELETE /api/family-units/{id}/profile-photo` | Family representative (own unit only), Admin | Remove the family group photo |

### Upload flow

1. Endpoint receives `multipart/form-data` with the image file.
2. Calls `IBlobStorageService.UploadAsync(folder: "profile-photos", contextId: memberId/unitId, generateThumbnail: true)`.
3. Sets `profilePhotoUrl = fileUrl` on the entity (use thumbnail URL as the stored value — thumbnails are 400×400, adequate for profile display).
4. If a previous photo existed, deletes the old blob before saving the new one.
5. Returns the new `profilePhotoUrl`.

### Bucket key schema (new folder)

```
abuvi-media/
├── profile-photos/family-members/{familyMemberId}/{guid}.{ext}
├── profile-photos/family-members/{familyMemberId}/thumbs/{guid}.webp
├── profile-photos/family-units/{familyUnitId}/{guid}.{ext}
└── profile-photos/family-units/{familyUnitId}/thumbs/{guid}.webp
```

Add `profile-photos` to the `AllowedFolders` list in `BlobStorageValidator`. Only image extensions are accepted for this folder.

### Authorization rules

- **Family representative**: Can only manage members/units that belong to their own `familyUnitId`. Check `user.familyUnitId == familyUnit.id` at the service layer.
- **Admin**: Can manage any family member or unit.
- **Board**: Read-only (no upload permission for other families' photos).

---

## Frontend

### Family profile page (`/profile`)

- Display `FamilyUnit.profilePhotoUrl` as a family avatar at the top of the profile page (fallback: initials or default icon).
- Each `FamilyMember` card shows their `profilePhotoUrl` as a small avatar (fallback: initial letter icon).
- "Editar foto" button on hover opens a file picker — on select, uploads and refreshes the display.

### Components affected

| Component | Change |
| --- | --- |
| `FamilyUnitCard.vue` | Show `profilePhotoUrl` avatar |
| `FamilyMemberCard.vue` | Show `profilePhotoUrl` avatar |
| `ProfilePage.vue` | Wire up upload/delete photo flow |

---

## Validation

- Only image extensions allowed: `.jpg`, `.jpeg`, `.png`, `.webp`.
- Max file size: same as global `MaxFileSizeBytes` (50 MB).
- Thumbnail is always generated (`generateThumbnail = true`).

---

## Testing

| Area | Tests |
| --- | --- |
| `FamilyMembersService` unit | Upload sets `profilePhotoUrl`; second upload deletes old blob first |
| `FamilyMembersEndpoints` integration | PUT by own representative → 200; PUT by other family → 403; PUT by Admin → 200 |
| `FamilyUnitsEndpoints` integration | Same authorization scenarios |
| `FamilyMemberCard` component | Renders avatar when `profilePhotoUrl` set; renders fallback when null |

---

## Acceptance Criteria

- [ ] `FamilyMember.profilePhotoUrl` and `FamilyUnit.profilePhotoUrl` fields added via migration.
- [ ] Family representatives can upload and delete profile photos for their own members and family unit.
- [ ] Admins can manage any profile photo.
- [ ] Uploading a new photo deletes the previous blob.
- [ ] Profile page displays avatars with fallback for missing photos.
- [ ] `profile-photos` folder added to the allowed folders list in `BlobStorageValidator`.
- [ ] Only image extensions accepted for profile photos.
- [ ] Unit and integration tests pass at ≥ 90% coverage.

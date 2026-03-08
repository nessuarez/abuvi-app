# User Story: Camp Extended Information from Google Places

## Epic

Camp Location Management Enhancement

## Story ID

feat-camps-extra-data

## User Stories

### Primary User Story

**As a** Board member managing camp locations
**I want to** automatically enrich camp information with data from Google Places API
**So that** families can access comprehensive, accurate contact and location details without manual data entry

### Supporting User Stories

1. **As a** family member browsing camps
   **I want to** see complete contact information (address, phone, website) for each camp
   **So that** I can easily reach out with questions or visit the location

2. **As a** family member evaluating camps
   **I want to** view photos of the actual camp facilities from Google Maps
   **So that** I can make informed decisions about which camp suits my family

3. **As a** family member planning travel
   **I want to** open the camp location directly in Google Maps
   **So that** I can get directions and plan my journey

4. **As a** Board member
   **I want to** see Google ratings and review counts for camp locations
   **So that** I can assess the reputation and quality of potential venues

5. **As a** Board administrator
   **I want to** periodically sync camp information with Google Places (annually)
   **So that** contact details, ratings, and photos stay current without manual updates

## Business Value

### Primary Benefits

- **Reduced Manual Work**: Eliminates ~30 minutes per camp for manual data entry
- **Data Accuracy**: Ensures contact information stays current via Google Places sync
- **User Experience**: Provides comprehensive camp information, reducing support inquiries
- **Decision Support**: Visual previews and ratings help families choose appropriate camps

### Metrics

- Reduce "camp information" support tickets by 50%
- Increase camp registration completion rate (families have all needed info)
- Eliminate manual address/contact updates (automated annual sync)

## Prerequisites & Dependencies

### Required First

1. ✅ Google Places API integration exists (`feat-google-places-camps`)
2. ✅ Basic Camp model with coordinates defined
3. ✅ Authentication/authorization for Board role

### Technical Dependencies

- Google Places API key with Places API and Places Photos API enabled
- Database migration capability (Entity Framework)
- Blob storage or CDN for photo storage (if downloading photos)

## Acceptance Criteria

### Must Have (Phase 1-4 - MVP)

#### Backend

- [ ] Camp model extended with all contact fields (address, phone, website, Google Maps URL)
- [ ] CampPhoto entity created with proper relationships
- [ ] Database migration executed successfully in dev, staging, production
- [ ] DTOs updated to include all extended fields
- [ ] GooglePlacesMapperService maps Place Details → Camp fields correctly
- [ ] All extended fields are optional (nullable)
- [ ] API returns extended camp information in GET endpoints

#### Frontend

- [ ] TypeScript types updated for Camp and CampPhoto
- [ ] CampContactInfo component displays address, phone, website, Google Maps link
- [ ] CampPhotoGallery component displays primary photo + thumbnail grid
- [ ] Camp detail page shows contact info and photos when available
- [ ] Google Places autocomplete auto-fills extended fields in camp creation form
- [ ] System works correctly for camps WITHOUT Google Place ID (graceful degradation)

#### Quality

- [ ] Unit tests for GooglePlacesMapperService
- [ ] Integration tests for camp endpoints with extended fields
- [ ] E2E test: Create camp via Google Places autocomplete → verify extended data saved
- [ ] E2E test: View camp detail page → verify contact info and photos displayed

### Should Have (Phase 5 - Post-MVP)

#### Sync Features

- [ ] Individual camp sync: Board member can manually sync one camp
- [ ] Batch sync: Board admin can sync all active camps
- [ ] Sync preview shows: total camps, estimated duration, camps without Place ID
- [ ] Sync result shows: detected changes, new photos, errors
- [ ] Changes detected but NOT auto-applied (user reviews and approves)
- [ ] Email notification sent to admin with sync report
- [ ] Rate limiting (200ms delay between requests) to respect Google API limits

### Won't Have (Out of Scope)

- ❌ Automatic scheduled background sync (do manually once per year)
- ❌ User-generated photo uploads (only Google Places photos)
- ❌ Storing/caching Google reviews (privacy/GDPR concerns)
- ❌ Opening hours display (low priority, future enhancement)
- ❌ Download and store photos locally (Phase 1 uses Google Photo API references)

## Technical Specification

### Data Model Changes

#### Camp Entity - New Fields

```csharp
// Contact Information
public string? FormattedAddress { get; set; }        // "Crta Pujarnol, km 5, 17834 Pujarnol, Girona, España"
public string? StreetAddress { get; set; }            // "Crta Pujarnol, km 5"
public string? Locality { get; set; }                 // "Pujarnol"
public string? AdministrativeArea { get; set; }       // "Girona"
public string? PostalCode { get; set; }               // "17834"
public string? Country { get; set; }                  // "España"
public string? PhoneNumber { get; set; }              // "+34 972 59 05 07"
public string? NationalPhoneNumber { get; set; }      // "972 59 05 07"
public string? WebsiteUrl { get; set; }               // "http://www.example.com/"

// Google Maps Integration
public string? GoogleMapsUrl { get; set; }            // Direct link to Google Maps
public string? GooglePlaceId { get; set; }            // For future sync operations

// Ratings & Metadata
public decimal? GoogleRating { get; set; }            // 3.7
public int? GoogleRatingCount { get; set; }           // 113
public DateTime? LastGoogleSyncAt { get; set; }       // Last sync timestamp
public string? BusinessStatus { get; set; }           // "OPERATIONAL" | "CLOSED_TEMPORARILY"
public string? PlaceTypes { get; set; }               // JSON array as string

// Navigation Properties
public ICollection<CampPhoto> Photos { get; set; } = new List<CampPhoto>();
```

**Validation**: All new fields are nullable/optional. Camps can be created manually without Google Places data.

#### CampPhoto Entity - New Table

```csharp
public class CampPhoto
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }                  // FK to Camp

    public string? PhotoReference { get; set; }       // Google Places photo reference
    public string? PhotoUrl { get; set; }             // URL to photo (Google or stored)

    public int Width { get; set; }
    public int Height { get; set; }

    public string AttributionName { get; set; }       // Photo author
    public string? AttributionUrl { get; set; }       // Author profile link

    public bool IsOriginal { get; set; }              // true = Google Places, false = manual
    public bool IsPrimary { get; set; }               // Primary photo for display
    public int DisplayOrder { get; set; }             // Sort order in gallery

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Camp Camp { get; set; } = null!;
}
```

**Indexes**:

- `CampId` (FK)
- `GooglePlaceId` on Camps table (for sync lookup)

### API Endpoints

#### Existing Endpoints - Updated Responses

- `GET /api/camps` - Include extended fields in list DTOs (lightweight, no photos)
- `GET /api/camps/{id}` - Include full extended info + photos array in detail DTO

#### New Endpoints (Phase 5 - Sync)

- `POST /api/camps/{id}/sync` - Sync individual camp with Google Places
  - Query params: `autoApprove` (bool, default: false)
  - Returns: `SyncResultDto` with detected changes

- `POST /api/camps/batch-sync` - Sync multiple camps
  - Body: `{ campIds: Guid[] | null, includeInactive: bool }`
  - Returns: `BatchSyncResultDto` with results array

- `GET /api/camps/sync-preview` - Preview batch sync
  - Query params: `includeInactive` (bool)
  - Returns: `SyncPreviewDto` (count, estimated duration)

### Frontend Components

#### New Components

1. **CampContactInfo.vue** - Displays contact details section
   - Location: `frontend/src/components/camps/`
   - Props: `{ camp: Camp }`
   - Displays: Address, phone (tel: link), website (external link), Google Maps link, rating

2. **CampPhotoGallery.vue** - Photo gallery with thumbnails
   - Location: `frontend/src/components/camps/`
   - Props: `{ photos: CampPhoto[] }`
   - Features: Primary photo (large), thumbnail grid, Google attribution, PrimeVue Image with preview

3. **CampBatchSyncDialog.vue** - Batch sync management (Phase 5)
   - Location: `frontend/src/components/camps/`
   - 3-step flow: Preview → Syncing → Results
   - Shows: Camp counts, estimated time, progress bar, results table

4. **CampSyncButton.vue** - Individual camp sync (Phase 5)
   - Location: `frontend/src/components/camps/`
   - Props: `{ camp: Camp }`
   - Shows: Last sync date, sync button, change comparison dialog

#### Updated Components

- **CampLocationDetailPage.vue** - Add CampContactInfo and CampPhotoGallery sections
- **CampLocationsPage.vue** - Add batch sync button (Board/Admin only, Phase 5)

### Services & Business Logic

#### GooglePlacesMapperService

**Responsibility**: Map Google Places API response → Camp entity fields

**Key Methods**:

```csharp
CampExtendedInfo MapPlaceDetailsToCamp(PlaceDetailsResponse details)
List<CampPhotoDto> MapPlacePhotos(PlaceDetailsResponse details)
```

**Logic**:

- Extract address components (street, locality, postal code, country)
- Map phone numbers (international + national format)
- Extract website, Google Maps URL, Place ID
- Map rating and rating count
- Parse business status
- Convert photos array to CampPhotoDto list

#### CampSyncService (Phase 5)

**Responsibility**: Synchronize camp data with Google Places

**Key Methods**:

```csharp
Task<SyncResultDto> SyncCampAsync(Guid campId, bool autoApprove)
Task<BatchSyncResultDto> SyncAllCampsAsync(bool includeInactive)
Task<SyncPreviewDto> GetSyncPreviewAsync(bool includeInactive)
```

**Logic**:

- Fetch current data from Google Places API
- Compare with existing camp data field-by-field
- Detect new photos (by photoReference)
- Generate ChangeDetectionDto with all differences
- Apply changes if autoApprove = true
- Update LastGoogleSyncAt timestamp
- Respect rate limits (200ms delay between camps)

### Photo Storage Strategy

**Phase 1 (MVP) - Recommended: Photo References**

- Store only `photoReference` from Google Places
- Build URLs dynamically using Google Places Photo API
- Format: `/api/places/photo?reference={ref}&maxwidth={width}`
- **Pros**: No storage costs, always current, simple implementation
- **Cons**: API request cost per photo view, dependency on Google

**Phase 2 (Future) - Downloaded Photos**

- Download photos from Google Places
- Store in Azure Blob Storage / AWS S3
- Save `photoUrl` pointing to our storage
- **Pros**: Full control, no runtime Google dependency
- **Cons**: Storage costs, potential copyright issues, stale photos

**Decision**: Use Phase 1 (references) for MVP, migrate to Phase 2 if needed.

### Sync Policy (Phase 5)

#### Fields That SYNC (Auto-update from Google)

- ✅ All address fields (formattedAddress, streetAddress, locality, etc.)
- ✅ Phone numbers (phoneNumber, nationalPhoneNumber)
- ✅ Website URL
- ✅ Google Maps URL
- ✅ Rating and rating count
- ✅ Business status
- ✅ Place types
- ✅ Photos (ADD new ones, never DELETE existing)

#### Fields That DON'T SYNC (User-managed)

- ❌ Camp name (may differ from Google Place name)
- ❌ Description (custom content)
- ❌ Coordinates (fixed on creation)
- ❌ Prices (internal business logic)
- ❌ Camp status (Draft/Open/Closed/Completed)
- ❌ Manual photos (isOriginal = false)

#### Sync Frequency

- **Recommended**: Once per year (January, before planning new season)
- **Trigger**: Manual batch sync via admin UI
- **Approval**: Changes detected but NOT auto-applied (admin reviews)

## Implementation Plan

### Phase 1: Backend Foundation (Priority: High)

**Estimated**: 1-2 days

**Tasks**:

1. Update Camp entity with extended fields
2. Create CampPhoto entity
3. Generate and test migration (`AddExtendedCampInformation`)
4. Update CampDto, CampDetailDto, CreateCampRequest, UpdateCampRequest
5. Write unit tests for entity validation

**Files Modified**:

- `backend/Models/Camp.cs`
- `backend/Models/CampPhoto.cs`
- `backend/Data/Migrations/YYYYMMDDHHMMSS_AddExtendedCampInformation.cs`
- `backend/DTOs/CampDTOs.cs`
- `backend/Tests/Unit/Models/CampTests.cs`

**Acceptance**:

- Migration runs successfully
- All DTOs serialize/deserialize correctly
- Unit tests pass

### Phase 2: Backend Mapping Service (Priority: High)

**Estimated**: 1 day

**Tasks**:

1. Implement GooglePlacesMapperService
2. Integrate with existing PlacesController (if exists) or CampsController
3. Update camp creation endpoint to handle extended fields
4. Write unit tests for mapping logic
5. Write integration tests for camp CRUD with extended fields

**Files Modified**:

- `backend/Services/GooglePlacesMapperService.cs`
- `backend/Controllers/CampsController.cs`
- `backend/Tests/Unit/Services/GooglePlacesMapperServiceTests.cs`
- `backend/Tests/Integration/CampsControllerTests.cs`

**Acceptance**:

- Google Places response correctly maps to Camp fields
- Camp creation via Places autocomplete fills extended fields
- All tests pass

### Phase 3: Frontend Types & Data Layer (Priority: High)

**Estimated**: 0.5 days

**Tasks**:

1. Update Camp and create CampPhoto TypeScript interfaces
2. Update useCamps composable to handle extended fields
3. Ensure API client correctly sends/receives new fields

**Files Modified**:

- `frontend/src/types/camp.ts`
- `frontend/src/composables/useCamps.ts`
- `frontend/src/utils/api.ts` (if needed)

**Acceptance**:

- TypeScript compilation succeeds
- API calls include extended fields
- No type errors

### Phase 4: Frontend UI Components (Priority: High)

**Estimated**: 2 days

**Tasks**:

1. Create CampContactInfo.vue component
2. Create CampPhotoGallery.vue component
3. Update CampLocationDetailPage.vue to include new components
4. Optionally update CampLocationCard.vue to show rating badge
5. Test with real Google Places data

**Files Created**:

- `frontend/src/components/camps/CampContactInfo.vue`
- `frontend/src/components/camps/CampPhotoGallery.vue`

**Files Modified**:

- `frontend/src/views/camps/CampLocationDetailPage.vue`
- `frontend/src/components/camps/CampLocationCard.vue` (optional)

**Acceptance**:

- Contact info displays correctly with all fields
- Photo gallery shows primary photo + thumbnails
- Google attribution displayed
- Graceful handling when fields are null (camps without Google data)

### Phase 5: Sync Features (Priority: Medium - Post-MVP)

**Estimated**: 3-4 days

**Tasks**:

1. Implement CampSyncService with comparison logic
2. Create sync endpoints (individual, batch, preview)
3. Implement email notification service
4. Create useCampSync composable
5. Create CampBatchSyncDialog.vue
6. Create CampSyncButton.vue
7. Add batch sync button to CampLocationsPage.vue
8. Add individual sync button to CampLocationDetailPage.vue
9. Write E2E tests for sync flow

**Files Created**:

- `backend/Services/CampSyncService.cs`
- `backend/DTOs/SyncDTOs.cs`
- `frontend/src/composables/useCampSync.ts`
- `frontend/src/components/camps/CampBatchSyncDialog.vue`
- `frontend/src/components/camps/CampSyncButton.vue`
- `backend/Tests/Integration/CampSyncTests.cs`
- `frontend/tests/e2e/camp-sync.spec.ts`

**Files Modified**:

- `backend/Controllers/CampsController.cs`
- `backend/Repositories/ICampRepository.cs` (add GetCampsWithGooglePlaceIdAsync)
- `frontend/src/views/camps/CampLocationsPage.vue`
- `frontend/src/views/camps/CampLocationDetailPage.vue`

**Acceptance**:

- Individual sync compares and reports changes
- Batch sync processes all camps with rate limiting
- Preview shows accurate counts and duration
- Email report sent on completion
- UI clearly shows detected vs applied changes

### Phase 6: Testing & Documentation (Priority: High)

**Estimated**: 1 day

**Tasks**:

1. E2E test: Create camp via autocomplete → extended data saved
2. E2E test: View camp detail → contact info and photos shown
3. E2E test: Sync individual camp → changes detected and applied
4. E2E test: Batch sync → multiple camps processed
5. Update API documentation (Swagger/OpenAPI)
6. Update user guide for Board members

**Files Created/Modified**:

- `frontend/tests/e2e/camp-extended-info.spec.ts`
- `frontend/tests/e2e/camp-sync.spec.ts`
- `docs/api/camps.md`
- `docs/user-guides/board-camp-management.md`

**Acceptance**:

- All E2E tests pass
- API documentation reflects new fields and endpoints
- User guide explains sync process

## Security Considerations

### Data Privacy (GDPR/LOPD)

- ✅ Google Places data is public information (addresses, phone, ratings)
- ✅ Photos must include Google attribution (legal requirement)
- ❌ Do NOT store Google reviews (may contain personal information)
- ✅ Business status changes (closed) should trigger notifications to admins

### API Security

- ✅ Sync endpoints require `Board` or `Admin` role
- ✅ Google API key must be server-side only (never exposed to frontend)
- ✅ Rate limiting on sync endpoints to prevent abuse
- ✅ Google API rate limits respected (200ms delay between requests)

### Data Integrity

- ✅ All extended fields are optional (nullable) - no breaking changes
- ✅ Existing camps without Google Place ID continue to work
- ✅ Sync operations log all changes for audit trail
- ✅ Failed syncs don't corrupt existing data (transaction rollback)

## Performance Considerations

### Database

- ✅ Index on `GooglePlaceId` for sync lookups
- ✅ CampPhotos FK index on `CampId`
- ✅ Pagination on photos if count is very high (unlikely, but safe)

### API Performance

- ✅ Photo URLs use Google CDN (fast, globally distributed)
- ⚠️ Consider caching camp detail responses (includes photos)
- ⚠️ Batch sync is long-running - consider background job queue (future)

### Frontend Performance

- ✅ Lazy load photo thumbnails (use `loading="lazy"`)
- ✅ Primary photo displayed first, thumbnails loaded after
- ✅ Image optimization via Google Photo API (specify maxwidth)

## Monitoring & Observability

### Metrics to Track

- Number of camps with/without Google Place ID
- Sync success/failure rates
- Google API usage (requests per day)
- Average sync duration per camp
- Photo load times (Google CDN performance)

### Logging

- Log all sync operations (camp ID, timestamp, changes detected)
- Log sync errors with Google API responses
- Log rate limit hits
- Log email notification sends

### Alerts

- Alert on sync failure rate > 10%
- Alert on Google API quota approaching limit
- Alert on business status change to "CLOSED_PERMANENTLY"

## Rollout Plan

### Development Environment

1. Enable Google Places API in dev project
2. Run migrations
3. Test with real Google Places data (use Alba Colònies example)
4. Verify all extended fields populate correctly

### Staging Environment

1. Run migrations
2. Test sync with production-like data
3. Verify email notifications work
4. Performance test batch sync with all camps

### Production Environment

1. Deploy backend with migrations during maintenance window
2. Verify migrations successful
3. Deploy frontend
4. Smoke test: View camp detail with extended info
5. Announce new features to Board members
6. Schedule first batch sync for following week

### Rollback Plan

- Database: Rollback migration if issues detected (Drop table, drop columns)
- Backend: Revert to previous version (extended fields are optional, no breaking changes)
- Frontend: Revert to previous version (gracefully handles missing fields)

## Definition of Done

### Feature Complete When

- ✅ All Phase 1-4 acceptance criteria met (MVP features)
- ✅ Camp detail page shows contact info and photos from Google Places
- ✅ Google Places autocomplete auto-fills extended fields
- ✅ System works for camps with AND without Google Place ID
- ✅ All unit tests pass (> 80% coverage on new code)
- ✅ All integration tests pass
- ✅ All E2E tests pass
- ✅ No TypeScript errors
- ✅ API documentation updated
- ✅ Code reviewed and approved
- ✅ Deployed to production successfully

### Phase 5 (Sync) Complete When

- ✅ All Phase 5 acceptance criteria met
- ✅ Batch sync successfully processes all camps
- ✅ Email notifications sent correctly
- ✅ UI shows detected changes clearly
- ✅ Rate limiting prevents Google API quota issues
- ✅ Board user guide includes sync instructions

## Questions & Clarifications

### Open Questions

1. **Photo Storage**: Confirm Phase 1 approach (references only) or immediately implement Phase 2 (download and store)?
   - **Recommendation**: Phase 1 for MVP, evaluate cost after 3 months

2. **Sync Approval**: Should there be individual approval per camp or batch approval?
   - **Recommendation**: Individual review, batch apply (all approved changes at once)

3. **Sync Frequency**: Enforce annual limit or allow on-demand sync?
   - **Recommendation**: On-demand, but warn if < 6 months since last sync (to reduce API costs)

4. **Email Recipients**: Who receives sync reports?
   - **Recommendation**: User who initiated sync + all Board members with Admin role

### Assumptions

- Google Places API quota is sufficient for annual batch sync (estimate: 50 camps × 2 API calls = 100 requests)
- Google API key has Places API and Places Photos API enabled
- Camp locations are primarily in Spain (address format consistent)
- Photos from Google Places are acceptable quality for our use case

## References

### Technical Documentation

- [Google Places API - Place Details](https://developers.google.com/maps/documentation/places/web-service/details)
- [Google Places API - Photos](https://developers.google.com/maps/documentation/places/web-service/photos)
- [Google Places API - Usage Limits](https://developers.google.com/maps/documentation/places/web-service/usage-and-billing)

### Related Specifications

- [Google Places Autocomplete Integration](../feat-google-places-camps/google-places-autocomplete.md)
- [Camp Data Model](../../specs/data-model.md)
- [Backend Standards](../../specs/backend-standards.mdc)
- [Frontend Standards](../../specs/frontend-standards.mdc)

### Example Data

- [Google Places Response Example](../feat-google-places-camps/examples/google-places-response.json)
- Real place: Alba Colònies (ChIJ38SpLTDCuhIRgdtW_484UBk)

## Change Log

| Date | Author | Changes |
|------|--------|---------|
| 2026-02-16 | AI Agent | Initial enriched user story created from technical specification |

---

**Story Status**: Ready for Development
**Priority**: High (MVP - Phases 1-4), Medium (Phase 5 - Post-MVP)
**Estimated Total Effort**: 6-8 days (MVP), +3-4 days (Sync features)

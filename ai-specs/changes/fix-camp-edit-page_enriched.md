# Fix Camp Location Edit Page - Enriched User Story

## Summary

The camp location detail/edit page has multiple bugs and missing functionality that prevent proper editing of camps. Images don't display, prices can't be edited after creation, and many new fields are missing from the edit form. Additionally, the creation form is too complex (should be lightweight) and the Google Places integration doesn't provide useful location feedback to the user.

## Current Issues Identified

### BUG 1: Field Name Mismatch `rawAddress` vs `location` (Critical - Data Loss)

The frontend uses `rawAddress` as the field name for the camp address, while the backend uses `Location` (serialized as `location` in JSON).

**Impact**: Address data is silently lost on create/update, and not displayed when reading from the API.

**Files affected**:

- `frontend/src/types/camp.ts` — `Camp.rawAddress`, `CreateCampRequest.rawAddress`
- `frontend/src/components/camps/CampLocationForm.vue` — form uses `formData.rawAddress`
- Backend `CampsModels.cs` — `CreateCampRequest.Location`, `UpdateCampRequest.Location`, `CampResponse.Location`, `CampDetailResponse.Location`

**Fix**: Align field names. Either:

- **(Recommended)** Rename frontend `rawAddress` → `location` everywhere to match the backend
- Or add `[JsonPropertyName("rawAddress")]` on the backend (less desirable, breaks consistency)

---

### BUG 2: Edit Dialog Uses Incomplete List Data (Critical - Data Overwrite)

The edit dialog on `CampLocationsPage.vue` opens with `selectedCamp` from the list endpoint. The list endpoint returns `CampResponse` which only includes basic fields:

- Name, Description, Location, Latitude, Longitude, GooglePlaceId
- FormattedAddress, PhoneNumber, WebsiteUrl, GoogleMapsUrl, GoogleRating, GoogleRatingCount, BusinessStatus
- PricePerAdult, PricePerChild, PricePerBaby, IsActive, CreatedAt, UpdatedAt

**Missing from list response** (but present in DB and needed for editing):

- `province`, `contactEmail`, `contactPerson`, `contactCompany`, `secondaryWebsiteUrl`
- `basePrice`, `vatIncluded`
- `accommodationCapacity`, `calculatedTotalBedCapacity`
- `abuviManagedByUserId`, `abuviContactedAt`, `abuviPossibility`, `abuviLastVisited`, `abuviHasDataErrors`
- `externalSourceId`, `lastModifiedByUserId`
- `editionCount`

**Impact**: When editing from the list view, all extended fields are initialized as `null`/`undefined` in the form. Submitting the form overwrites existing data with null values.

**Fix options**:

1. **(Recommended)** Fetch full camp detail (`GET /api/camps/{id}`) when opening the edit dialog, instead of using the list item data
2. Or extend `CampResponse` to include all editable fields (increases list payload)

---

### BUG 3: No Edit Button on Detail Page (UX Gap)

`CampLocationDetailPage.vue` displays all camp information but has **no edit button**. Users must navigate back to the list page and click the edit icon there, which opens a dialog with incomplete data (see BUG 2).

**Fix**: Add an "Edit" button to the detail page header that either:

- **(Recommended)** Opens an inline edit mode or the existing edit dialog, pre-populated with the full `CampDetailResponse` data already loaded
- Or navigates to a dedicated edit page

---

### BUG 4: User-Uploaded Photos Never Loaded (Photos Not Displayed)

`CampLocationDetailPage.vue` line 48 initializes `campPhotos` as an empty array:

```typescript
const campPhotos = ref<CampPhoto[]>([])
```

This is passed to `CampPhotoGallery` as `initialPhotos` but is **never populated** from the API. The `useCampPhotos` composable has no `fetchPhotos`/`listPhotos` method, even though the backend endpoint exists: `GET /api/camps/{campId}/photos`.

**Impact**: User-uploaded photos are never displayed on page load. They only appear after adding a new photo in the current session.

**Fix**:

1. Add a `fetchPhotos(campId)` method to `useCampPhotos.ts` composable that calls `GET /api/camps/{campId}/photos`
2. In `CampLocationDetailPage.vue`, call `fetchPhotos` on mount and pass the result to `CampPhotoGallery`

---

### BUG 5: Google Places Photo Proxy May Be Broken

`CampPlacesGallery.vue` constructs photo URLs like:

```
${apiBase}/places/photo?reference=${photoReference}&maxwidth=${maxWidth}
```

If the `/places/photo` proxy endpoint is not working or the `VITE_API_URL` env var is misconfigured, Google Places photos won't display.

**Action**: Verify the `/api/places/photo` endpoint is functioning correctly in the dev environment. Check browser network tab for 404/500 errors on photo requests.

---

### IMPROVEMENT 1: Creation Form Too Complex (UX)

The current creation form (`CampLocationForm.vue` in `mode="create"`) shows ALL fields: accommodation, contact info, prices, ABUVI tracking, etc. This is overwhelming when creating a new camp. The user flow should be:

1. **Create**: Only name (via Google Places autocomplete) + Google Places auto-filled data. That's it.
2. **Edit later**: All other fields (prices, accommodation, contact info, ABUVI tracking, etc.) are added through the edit form on the detail page.

**Current state**: `CampLocationForm.vue` renders the same form for both `create` and `edit` modes.

**Fix**: Split the form behavior by mode:

- **Create mode**: Minimal form — only name search (Google Places autocomplete). On place selection, auto-store Google Places data (address, coordinates, placeId). No pricing, no accommodation, no contacts, no ABUVI fields.
- **Edit mode**: Full form with all fields (as it is now, but with all bugs fixed).

**Note**: The backend `CreateCampRequest` already has sensible defaults (prices default to `0`, optional fields are nullable), so a minimal creation payload is already supported.

---

### IMPROVEMENT 2: Bad Auto-Generated Description from Google Places (UX)

When a place is selected from Google Places, the current code generates a description like:

```
"Establecimiento ubicada en Calle Example 123, Madrid, España"
```

This is a low-quality, mechanical description. The `generateDescription` function in `CampLocationForm.vue` maps place types to generic Spanish labels (`campground` → "Zona de camping", `park` → "Parque natural", etc.) and appends the formatted address. Grammar is also wrong ("Establecimiento ubicada" should be "ubicado").

**Fix**: Remove the auto-generated description entirely. Leave the description field empty on creation — the user will fill it in later when editing. The Google Places `formattedAddress` is already stored in a dedicated field, so duplicating it into the description adds no value.

---

### IMPROVEMENT 3: Show Resolved Location Info Instead of Raw Coordinates (UX)

After selecting a place from Google Places, the form shows raw latitude/longitude values (e.g., `40.416775`, `-3.703790`). This is not useful for the user — they need to verify that the **correct place** was identified, not check GPS coordinates.

**Current state**: The frontend `PlaceDetails` interface (`useGooglePlaces.ts`) only exposes `placeId, name, formattedAddress, latitude, longitude, types`. But the backend `PlaceDetails` record returns much more data that is currently discarded:

- `AddressComponents` (locality, province, administrative area, postal code, country)
- `PhoneNumber`, `NationalPhoneNumber`
- `Website`, `GoogleMapsUrl`
- `Rating`, `RatingCount`
- `BusinessStatus`

**Fix**:

1. Extend the frontend `PlaceDetails` interface in `useGooglePlaces.ts` to include `addressComponents` (or at least `locality`, `administrativeArea`, `country` as pre-extracted fields from the backend).
2. After place selection in the creation form, instead of showing lat/lng inputs, show a **read-only confirmation card** with:
   - Place name
   - Formatted address
   - Locality / Province / Administrative area / Country (extracted from address components)
   - Google rating (if available)
   - A "Change" button to re-search
3. Coordinates are still stored but hidden from the user — they're internal data, not something the user needs to verify.

---

### IMPROVEMENT 4: Facilities Display Needs Icons and Better UX (UX)

The `AccommodationCapacityDisplay.vue` shows facility features as plain text badges (green pills with text like "Piscina", "Comedor cerrado"). These should use **representative icons** to make the information scannable at a glance.

The `AccommodationCapacityForm.vue` uses `ToggleSwitch` for facilities, which works, but the fields are small and could benefit from larger touch targets with icons.

**Fix for display** (`AccommodationCapacityDisplay.vue`):
- Add PrimeVue icons to each facility badge:
  - Piscina → `pi pi-sun` (or custom pool icon)
  - Comedor cerrado → `pi pi-building`
  - Menú adaptado → `pi pi-list`
  - Pista polideportiva → `pi pi-star` (or sports icon)
  - Pinar / zona natural → `pi pi-globe`
- Make badges larger and more visual

**Fix for form** (`AccommodationCapacityForm.vue`):
- Replace small `ToggleSwitch` with larger clickable cards/chips with icons
- Each facility should be a card-style toggle: icon + label, highlighted when active
- This makes it easier to check/uncheck, especially on mobile

---

### IMPROVEMENT 5: Remove "Temporada" (Season) Field from Observations (Data Cleanup)

The `CampObservationsSection.vue` includes a "Temporada" (Season) `Select` dropdown when adding observations. This field was inherited from the CSV import and has **no practical purpose** for manual observations.

**Current state**: The form shows a season selector with options like "2023", "2024", "2025/2026". Existing imported observations may have season values, but new manual observations shouldn't require this.

**Fix**:
1. **Frontend**: Remove the `Select` for season from the add-observation form in `CampObservationsSection.vue`. Always send `season: null`.
2. **Display**: Keep showing the season badge on existing observations that have one (imported data), but don't allow setting it for new ones.
3. **Backend**: No change needed — `season` is already nullable in `AddCampObservationRequest` and `CampObservation`.

---

### IMPROVEMENT 6: Add "Refresh Google Places Data" Button (Feature)

There is no way to re-sync a camp's Google Places data after initial creation. The `EnrichFromGooglePlacesAsync` method in `CampsService.cs` only runs during `CreateAsync`. If Google Places data changes (new photos, updated rating, new phone number), there's no way to pull fresh data.

**Fix**:

1. **Backend**: Add a new endpoint `POST /api/camps/{id}/refresh-places` that:
   - Validates the camp has a `GooglePlaceId`
   - Calls `EnrichFromGooglePlacesAsync` (or extracts it into a reusable public method)
   - Returns the updated `CampDetailResponse`
   - Requires Board+ role

2. **Frontend**: Add a "Actualizar datos de Google" button on the camp detail page (and/or edit dialog) that:
   - Calls the new endpoint
   - Shows a loading spinner during refresh
   - On success, refreshes the displayed data and shows a success toast
   - Only visible for camps that have a `GooglePlaceId`

---

## Implementation Plan

### Phase 1: Fix Critical Data Bugs

#### Step 1.1: Fix field name mismatch (`rawAddress` → `location`)

**Frontend files to modify**:

- `frontend/src/types/camp.ts`:
  - Rename `rawAddress` → `location` in `Camp` interface
  - Rename `rawAddress` → `location` in `CreateCampRequest` interface
  - (UpdateCampRequest extends CreateCampRequest, so it inherits the fix)
- `frontend/src/components/camps/CampLocationForm.vue`:
  - Rename all `formData.rawAddress` → `formData.location`
  - Update template label/placeholder references
- `frontend/src/views/camps/CampLocationsPage.vue`:
  - Update `campLocations` computed: `rawAddress` → `location`
- `frontend/src/components/camps/CampLocationCard.vue`:
  - Update any `rawAddress` references → `location`
- `frontend/src/types/camp.ts` `CampLocation` interface:
  - Rename `rawAddress` → `location`
- `frontend/src/components/camps/CampLocationMap.vue`:
  - Update any `rawAddress` references → `location`
- `frontend/src/views/camps/CampLocationDetailPage.vue`:
  - Update `rawAddress` → `location` in CampLocationMap binding

#### Step 1.2: Fix edit dialog data fetching

**File**: `frontend/src/views/camps/CampLocationsPage.vue`

- Modify `handleEdit()` to fetch full camp detail before opening the edit dialog:

  ```typescript
  const handleEdit = async (camp: Camp) => {
    const detail = await getCampById(camp.id)
    if (detail) {
      selectedCamp.value = detail
      showEditDialog.value = true
    }
  }
  ```

- Import `getCampById` from `useCamps` composable
- Show loading indicator while fetching

### Phase 2: Fix Photo Display

#### Step 2.1: Add `fetchPhotos` to composable

**File**: `frontend/src/composables/useCampPhotos.ts`

Add method:

```typescript
const fetchPhotos = async (campId: string): Promise<CampPhoto[]> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.get<ApiResponse<CampPhoto[]>>(`/camps/${campId}/photos`)
    if (response.data.success && response.data.data) {
      photos.value = response.data.data
      return response.data.data
    }
    return []
  } catch (err: unknown) {
    error.value = (err as ApiErrorShape)?.response?.data?.error?.message || 'Error al cargar las fotos'
    return []
  } finally {
    loading.value = false
  }
}
```

Export `fetchPhotos` in the return object.

#### Step 2.2: Load photos on detail page mount

**File**: `frontend/src/views/camps/CampLocationDetailPage.vue`

- Import `useCampPhotos` and call `fetchPhotos(campId)` in `onMounted`
- Populate `campPhotos` ref with the result

### Phase 3: Add Edit Button to Detail Page

#### Step 3.1: Add edit capability to detail page

**File**: `frontend/src/views/camps/CampLocationDetailPage.vue`

- Add an "Editar" button in the header section (next to back button)
- Add the edit dialog with `CampLocationForm` (similar to CampLocationsPage)
- Use the already-loaded `camp.value` (full `CampDetailResponse`) as form data
- On successful edit, refresh the camp detail

### Phase 4: Simplify Creation Form + Improve Google Places UX

#### Step 4.1: Extend frontend `PlaceDetails` interface

**File**: `frontend/src/composables/useGooglePlaces.ts`

Extend the `PlaceDetails` interface to expose additional fields already returned by the backend:

```typescript
export interface PlaceDetails {
  placeId: string
  name: string
  formattedAddress: string
  latitude: number
  longitude: number
  types: string[]
  // New fields from backend (already returned, currently discarded)
  phoneNumber: string | null
  nationalPhoneNumber: string | null
  website: string | null
  googleMapsUrl: string | null
  rating: number | null
  ratingCount: number | null
  businessStatus: string | null
  addressComponents: GoogleAddressComponent[] | null
}

export interface GoogleAddressComponent {
  longName: string
  shortName: string
  types: string[]
}
```

#### Step 4.2: Simplify creation form (create mode only)

**File**: `frontend/src/components/camps/CampLocationForm.vue`

When `mode === 'create'`:

- Show only the name/autocomplete search field
- After a place is selected, show a **read-only location confirmation card** with:
  - Place name (bold)
  - Formatted address
  - Locality, Province/Administrative area, Country (extracted from `addressComponents`)
  - Google rating + rating count (if available)
  - A "Cambiar" (Change) button to clear and re-search
- Hide all other sections: pricing, accommodation, contacts, ABUVI tracking, coordinates
- The "Crear Campamento" button submits only: `name`, `location` (formattedAddress), `latitude`, `longitude`, `googlePlaceId`, `description` (left empty, not auto-generated)
- Prices default to `0` (backend already supports this)

When `mode === 'edit'`:

- Show all fields as currently (full form), unchanged

#### Step 4.3: Remove bad auto-generated description

**File**: `frontend/src/components/camps/CampLocationForm.vue`

- Remove the `generateDescription()` function entirely
- Remove the auto-fill of `formData.description` in `handlePlaceSelected()`
- The description field remains available in edit mode for the user to fill manually

### Phase 5: Improve Facilities Display + Form UX

#### Step 5.1: Add icons to facility display

**File**: `frontend/src/components/camps/AccommodationCapacityDisplay.vue`

Update `facilityLabels` to include icons:

```typescript
const facilityLabels: { key: keyof AccommodationCapacity; label: string; icon: string }[] = [
  { key: 'hasAdaptedMenu', label: 'Menú adaptado', icon: 'pi pi-list' },
  { key: 'hasEnclosedDiningRoom', label: 'Comedor cerrado', icon: 'pi pi-building' },
  { key: 'hasSwimmingPool', label: 'Piscina', icon: 'pi pi-sun' },
  { key: 'hasSportsCourt', label: 'Pista polideportiva', icon: 'pi pi-flag' },
  { key: 'hasForestArea', label: 'Pinar / zona natural', icon: 'pi pi-globe' }
]
```

Render badges with icons, larger size, more visual.

#### Step 5.2: Improve facility toggle UX in form

**File**: `frontend/src/components/camps/AccommodationCapacityForm.vue`

Replace the small `ToggleSwitch` + label layout with clickable card-style chips:
- Each facility is a bordered card with icon + label
- Clicking toggles the value
- Active state: filled background (e.g., green/primary), bold text
- Inactive state: outlined, muted text
- Larger touch target for mobile usability

### Phase 6: Remove Season from Observations

#### Step 6.1: Simplify observation form

**File**: `frontend/src/components/camps/CampObservationsSection.vue`

- Remove the `Select` for `newSeason` and the `seasonOptions` array
- Always send `season: null` when adding new observations
- Keep the season badge display on existing observations (read-only, for imported data)
- The form becomes just a `Textarea` + "Añadir" button (full width)

### Phase 7: Add Google Places Refresh Button

#### Step 7.1: Backend — Add refresh endpoint

**File**: `src/Abuvi.API/Features/Camps/CampsEndpoints.cs`

Add new endpoint:

```csharp
group.MapPost("/{id:guid}/refresh-places", RefreshGooglePlaces)
    .WithName("RefreshGooglePlaces")
    .WithSummary("Re-sync camp data from Google Places")
    .Produces<ApiResponse<CampDetailResponse>>(200)
    .Produces(400)
    .Produces(404);
```

**File**: `src/Abuvi.API/Features/Camps/CampsService.cs`

Add public method:

```csharp
public async Task<CampDetailResponse?> RefreshGooglePlacesAsync(
    Guid id, CancellationToken cancellationToken = default)
{
    var camp = await _repository.GetByIdWithPhotosAsync(id, cancellationToken);
    if (camp == null) return null;
    if (string.IsNullOrWhiteSpace(camp.GooglePlaceId))
        throw new ArgumentException("Camp does not have a Google Place ID");

    camp = await EnrichFromGooglePlacesAsync(camp, cancellationToken);
    return MapToCampDetailResponse(camp, camp.Photos);
}
```

Note: `EnrichFromGooglePlacesAsync` is already implemented as a private method — make it `internal` or refactor to be reusable.

#### Step 7.2: Frontend — Add refresh button

**File**: `frontend/src/composables/useCamps.ts`

Add method:

```typescript
const refreshGooglePlaces = async (id: string): Promise<CampDetailResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.post<ApiResponse<CampDetailResponse>>(`/camps/${id}/refresh-places`)
    if (response.data.success && response.data.data) {
      return response.data.data
    }
    return null
  } catch (err: unknown) {
    error.value = extractError(err, 'Error al actualizar datos de Google Places')
    return null
  } finally {
    loading.value = false
  }
}
```

**File**: `frontend/src/views/camps/CampLocationDetailPage.vue`

- Add a "Actualizar datos de Google" button (with `pi pi-refresh` icon) in the Google Places contact info section
- Only visible when `camp.googlePlaceId` is not null
- On click: call `refreshGooglePlaces(camp.id)`, show spinner, refresh displayed data on success
- Board+ role check (`v-if="auth.isBoard"`)

### Phase 8: Extend Backend List Response (Optional Enhancement)

#### Step 8.1: Add `editionCount` to `CampResponse`

**File**: `src/Abuvi.API/Features/Camps/CampsModels.cs`

- Add `int EditionCount` field to `CampResponse` record
- Update `MapToCampResponse` in `CampsService.cs` to include the count
- This requires loading edition counts in `GetAllAsync` (may need repository change)

---

## Endpoints Used

| Method | URL | Purpose |
|--------|-----|---------|
| `GET` | `/api/camps` | List camps (returns `CampResponse[]`) |
| `GET` | `/api/camps/{id}` | Get camp detail (returns `CampDetailResponse`) |
| `PUT` | `/api/camps/{id}` | Update camp |
| `POST` | `/api/camps/{id}/refresh-places` | **(NEW)** Re-sync Google Places data |
| `GET` | `/api/camps/{campId}/photos` | List user-uploaded photos |
| `POST` | `/api/places/details` | Get Google Places details (backend already returns full data) |
| `GET` | `/api/places/photo?reference=...&maxwidth=...` | Proxy Google Places photos |

## Files to Modify

### Frontend

| File | Changes |
|------|---------|
| `frontend/src/types/camp.ts` | Rename `rawAddress` → `location` in Camp, CreateCampRequest, CampLocation |
| `frontend/src/composables/useGooglePlaces.ts` | Extend `PlaceDetails` interface with addressComponents, phone, website, rating, etc. |
| `frontend/src/components/camps/CampLocationForm.vue` | Split create/edit modes; simplify create to name-only + confirmation card; remove auto-description; fix `rawAddress` → `location`; hide lat/lng, show resolved location info |
| `frontend/src/views/camps/CampLocationsPage.vue` | Fetch full detail on edit; update `rawAddress` references |
| `frontend/src/views/camps/CampLocationDetailPage.vue` | Add edit button/dialog; load photos on mount; add Google Places refresh button; update `rawAddress` references |
| `frontend/src/composables/useCamps.ts` | Add `refreshGooglePlaces(id)` method |
| `frontend/src/composables/useCampPhotos.ts` | Add `fetchPhotos(campId)` method |
| `frontend/src/components/camps/AccommodationCapacityDisplay.vue` | Add icons to facility badges, larger visual style |
| `frontend/src/components/camps/AccommodationCapacityForm.vue` | Replace ToggleSwitch with card-style icon toggles for facilities |
| `frontend/src/components/camps/CampObservationsSection.vue` | Remove season selector from add form; keep season display on existing observations |
| `frontend/src/components/camps/CampLocationMap.vue` | Update `rawAddress` → `location` if referenced |
| `frontend/src/components/camps/CampLocationCard.vue` | Update `rawAddress` → `location` if referenced |

### Backend

| File | Changes |
|------|---------|
| `src/Abuvi.API/Features/Camps/CampsEndpoints.cs` | Add `POST /api/camps/{id}/refresh-places` endpoint |
| `src/Abuvi.API/Features/Camps/CampsService.cs` | Add `RefreshGooglePlacesAsync` method; make `EnrichFromGooglePlacesAsync` reusable |
| `src/Abuvi.API/Features/Camps/CampsModels.cs` | (Optional) Add `EditionCount` to `CampResponse` |
| `src/Abuvi.API/Features/Camps/CampsRepository.cs` | (Optional) Include edition count in list query |

## Acceptance Criteria

### Bug Fixes

- [ ] Camp address (location) is correctly saved when creating/editing a camp
- [ ] Camp address is correctly displayed on the detail page and in the form
- [ ] Editing a camp from the list view preserves all existing field values (no silent data overwrite)
- [ ] All editable fields (prices, contact info, accommodation, ABUVI tracking) are editable in the edit form
- [ ] User-uploaded photos are displayed on the camp detail page after reload
- [ ] Google Places photos are displayed correctly (if available for the camp)
- [ ] An "Edit" button is available on the camp detail page
- [ ] Editing from the detail page pre-populates all fields correctly
- [ ] After saving edits, the detail page reflects the updated data

### Creation Flow

- [ ] Creation form only shows name search + Google Places confirmation card (no pricing, accommodation, contacts, or internal fields)
- [ ] After selecting a Google Places result during creation, user sees locality/province/region for verification (not raw lat/lng)
- [ ] No auto-generated description ("Establecimiento ubicada en...") — description left empty for user to fill later
- [ ] Created camp stores all Google Places data (coordinates, placeId, address) even though they're not shown in the creation form

### Facilities UX

- [ ] Facility badges in `AccommodationCapacityDisplay` show representative icons
- [ ] Facility toggles in `AccommodationCapacityForm` use card-style clickable chips with icons (larger touch targets)

### Observations

- [ ] New observations can be added without selecting a season
- [ ] Season selector is removed from the add-observation form
- [ ] Existing observations with season values still display the season badge

### Google Places Refresh

- [ ] A "Actualizar datos de Google" button is visible on the detail page for camps with a GooglePlaceId
- [ ] Clicking the button re-syncs Google Places data (address, phone, rating, photos) and refreshes the page
- [ ] Button is only visible for Board+ users

## Testing Requirements

- [ ] Unit test: `CampLocationForm` correctly maps `location` field (not `rawAddress`)
- [ ] Unit test: `useCampPhotos.fetchPhotos` returns photos from API
- [ ] Unit test: Create mode form only renders name search + confirmation card
- [ ] Unit test: Edit mode form renders all fields
- [ ] Unit test: `PlaceDetails` interface includes extended fields (addressComponents, rating, etc.)
- [ ] Unit test: Observation form does not render season selector
- [ ] Unit test: `AccommodationCapacityDisplay` renders icons for facilities
- [ ] Integration test: Edit camp from detail page preserves all fields
- [ ] Integration test: `POST /api/camps/{id}/refresh-places` re-syncs Google data
- [ ] Manual test: Verify Google Places photos display in dev environment
- [ ] Manual test: Verify user-uploaded photos persist across page reloads
- [ ] Manual test: Create a camp → verify only name search shown → verify location confirmation card appears
- [ ] Manual test: Click "Actualizar datos de Google" → verify data refreshes

## Non-Functional Requirements

- No breaking changes to existing backend API contracts (new endpoint is additive)
- Maintain existing 90% test coverage threshold
- Follow existing code conventions (Vue 3 Composition API, PrimeVue, Tailwind CSS)

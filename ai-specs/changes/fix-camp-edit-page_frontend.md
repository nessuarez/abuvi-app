# Frontend Implementation Plan: fix-camp-edit-page

## Overview

Fix multiple bugs and improve UX on the camp location edit/detail pages. The frontend uses Vue 3 Composition API with PrimeVue components and Tailwind CSS. Changes span type definitions, composables, and several view/component files.

**Key issues**: field name mismatch (`rawAddress` vs `location`), edit dialog using incomplete list data, missing edit button on detail page, photos not loading, creation form too complex, and missing Google Places refresh capability.

## Architecture Context

### Components/Composables involved

- **Types**: `frontend/src/types/camp.ts` — Camp, CreateCampRequest, CampLocation interfaces
- **Composables**:
  - `frontend/src/composables/useCamps.ts` — camp CRUD + new `refreshGooglePlaces`
  - `frontend/src/composables/useCampPhotos.ts` — needs `fetchPhotos` method
  - `frontend/src/composables/useGooglePlaces.ts` — extend `PlaceDetails` interface
- **Views**:
  - `frontend/src/views/camps/CampLocationsPage.vue` — list page with edit dialog
  - `frontend/src/views/camps/CampLocationDetailPage.vue` — detail page (add edit button, load photos, refresh button)
- **Components**:
  - `frontend/src/components/camps/CampLocationForm.vue` — create/edit form (simplify create mode)
  - `frontend/src/components/camps/AccommodationCapacityDisplay.vue` — add facility icons
  - `frontend/src/components/camps/AccommodationCapacityForm.vue` — card-style facility toggles
  - `frontend/src/components/camps/CampObservationsSection.vue` — remove season selector
  - `frontend/src/components/camps/CampLocationCard.vue` — update rawAddress refs
  - `frontend/src/components/camps/CampLocationMap.vue` — update rawAddress refs

### State Management

- Local component state via composables (no Pinia store changes needed)
- Camp detail data flows through composable → view → child components via props

### Routing

- No new routes needed. Edit will be handled **inline on the detail page** (not in a modal dialog).

### UX Architecture Decision: Inline Edit on Detail Page

The detail page will have **two modes**: read-only (default) and edit mode. Toggling to edit mode transforms the read-only sections into editable form fields **in place**, without opening any dialog/modal. This provides a more natural editing experience for a form with many fields.

The page header will have a **top-right action bar** with escalation-style buttons:

- "Editar" (toggle edit mode) — primary action
- "Ediciones" (navigate to editions) — secondary action
- "Crear propuesta" (create edition proposal) — secondary action

This layout follows an escalation pattern: the user lands on the detail page, can edit the camp, view its editions, or create a new proposal — all from the same page header.

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/fix-camp-edit-page-frontend` from `dev`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/fix-camp-edit-page-frontend`
  3. Verify: `git branch`

---

### Step 1: Fix `rawAddress` → `location` Field Name Mismatch (BUG 1)

- **Files**:
  - `frontend/src/types/camp.ts`
  - `frontend/src/components/camps/CampLocationForm.vue`
  - `frontend/src/views/camps/CampLocationsPage.vue`
  - `frontend/src/views/camps/CampLocationDetailPage.vue`
  - `frontend/src/components/camps/CampLocationCard.vue`
  - `frontend/src/components/camps/CampLocationMap.vue`
- **Action**: Rename all `rawAddress` references to `location` to match the backend field name
- **Implementation Steps**:
  1. In `types/camp.ts`:
     - `Camp` interface: rename `rawAddress: string | null` → `location: string | null`
     - `CreateCampRequest` interface: rename `rawAddress: string | null` → `location: string | null`
     - `CampLocation` interface: rename `rawAddress?: string` → `location?: string`
  2. In `CampLocationForm.vue`: rename all `formData.rawAddress` → `formData.location`, update template labels/bindings
  3. In `CampLocationsPage.vue`: update any `rawAddress` references in computed properties and template
  4. In `CampLocationDetailPage.vue`: update `rawAddress` → `location` in CampLocationMap binding
  5. In `CampLocationCard.vue`: update any `rawAddress` references
  6. In `CampLocationMap.vue`: update any `rawAddress` prop/references
  7. Run `npx tsc --noEmit` to verify no type errors remain
- **Implementation Notes**: This is a breaking rename — use find-and-replace carefully. The backend has always used `location`.

---

### Step 2: Remove Edit Dialog from List Page, Navigate to Detail Instead (BUG 2)

- **File**: `frontend/src/views/camps/CampLocationsPage.vue`
- **Action**: Replace the edit dialog with navigation to the detail page. Editing will happen inline on the detail page, not in a modal.
- **Implementation Steps**:
  1. Remove the edit `Dialog` and `CampLocationForm` in edit mode from the template
  2. Remove `showEditDialog`, `selectedCamp` refs and related edit dialog logic
  3. Change `handleEdit(camp)` to navigate to the detail page:

     ```typescript
     const handleEdit = (camp: Camp) => {
       router.push({ name: 'camp-detail', params: { id: camp.id } })
     }
     ```

  4. Keep the edit icon button in the DataTable row actions — it now navigates instead of opening a dialog
  5. Clean up unused imports (Dialog, CampLocationForm in edit mode)
- **Implementation Notes**: This eliminates BUG 2 entirely — no more editing with incomplete list data. The detail page always has the full `CampDetailResponse`.

---

### Step 3: Add `fetchPhotos` to `useCampPhotos` Composable (BUG 4)

- **File**: `frontend/src/composables/useCampPhotos.ts`
- **Action**: Add a method to load user-uploaded photos from the backend
- **Implementation Steps**:
  1. Add `fetchPhotos(campId: string)` method that calls `GET /camps/${campId}/photos`
  2. Store result in the composable's reactive `photos` ref
  3. Return the photos array for the caller
  4. Handle loading and error states consistently with existing methods
  5. Export `fetchPhotos` in the composable return object
- **Implementation Notes**: The backend endpoint already exists. The response is `ApiResponse<CampPhoto[]>`.

---

### Step 4: Load Photos on Detail Page Mount (BUG 4)

- **File**: `frontend/src/views/camps/CampLocationDetailPage.vue`
- **Action**: Call `fetchPhotos` on mount to populate user-uploaded photos
- **Implementation Steps**:
  1. Import `useCampPhotos` composable (if not already imported)
  2. Destructure `fetchPhotos` from the composable
  3. In `onMounted` (or after camp data is loaded), call `const loadedPhotos = await fetchPhotos(campId)`
  4. Set `campPhotos.value = loadedPhotos`
  5. Pass `campPhotos` to `CampPhotoGallery` as `initialPhotos` (already wired)

---

### Step 5: Add Inline Edit Mode and Top-Right Action Bar to Detail Page (BUG 3)

- **File**: `frontend/src/views/camps/CampLocationDetailPage.vue`
- **Action**: Transform the detail page to support inline editing (no modal). Add a top-right action bar with "Editar", "Ediciones", and "Crear propuesta" buttons.
- **Implementation Steps**:
  1. **Page header layout**: Restructure the header to have a left side (back button + camp name) and a right side (action buttons):
     - "Editar" button (`pi pi-pencil`, primary style) — toggles inline edit mode. Board+ only.
     - "Ediciones" button (`pi pi-list`, outlined style) — navigates to camp editions page
     - "Crear propuesta" button (`pi pi-plus`, outlined style) — navigates to create edition proposal
     - Use `flex justify-between items-center` for the header layout
  2. **Inline edit mode**: Add `isEditing = ref(false)` reactive state
     - When `isEditing` is `false`: render read-only display sections (current behavior)
     - When `isEditing` is `true`: render `CampLocationForm` in `mode="edit"` **inline** (not in a Dialog), pre-populated with `camp.value` data
     - The "Editar" button label changes to "Cancelar" when in edit mode (with `pi pi-times` icon)
     - Map `CampDetailResponse` → form data when entering edit mode
  3. **Form integration**: Embed `CampLocationForm` directly in the page template (conditionally rendered)
     - Wrap read-only sections in `v-if="!isEditing"` and form in `v-else`
     - On successful save: set `isEditing = false`, re-fetch camp detail to refresh displayed data, show success toast
     - On cancel: set `isEditing = false`, discard changes
  4. **Visibility**: Only show "Editar" button for Board+ role (`v-if="auth.isBoard"`)
  5. **Remove any Dialog-based edit approach** — editing is fully inline
- **Implementation Notes**: The detail page already has the full `CampDetailResponse`, so the form gets all fields populated. No data overwrite risk. The inline approach avoids the cramped modal UX for a form with many fields.

---

### Step 6: Extend `PlaceDetails` Interface (IMPROVEMENT 3)

- **File**: `frontend/src/composables/useGooglePlaces.ts`
- **Action**: Expose additional fields from the backend `PlaceDetails` response that are currently discarded
- **Implementation Steps**:
  1. Extend the `PlaceDetails` interface to include:

     ```typescript
     phoneNumber: string | null
     nationalPhoneNumber: string | null
     website: string | null
     googleMapsUrl: string | null
     rating: number | null
     ratingCount: number | null
     businessStatus: string | null
     addressComponents: GoogleAddressComponent[] | null
     ```

  2. Add new interface:

     ```typescript
     export interface GoogleAddressComponent {
       longName: string
       shortName: string
       types: string[]
     }
     ```

  3. Update the `getPlaceDetails` response mapping to include these fields (the backend already returns them)

---

### Step 7: Simplify Creation Form (IMPROVEMENT 1 + 2 + 3)

- **File**: `frontend/src/components/camps/CampLocationForm.vue`
- **Action**: Split create/edit modes — create shows only name search + location confirmation card
- **Implementation Steps**:
  1. **Create mode** (`mode === 'create'`):
     - Show only the Google Places autocomplete search field
     - After place selection, show a **read-only location confirmation card** with:
       - Place name (bold)
       - Formatted address
       - Locality / Province / Country (extracted from `addressComponents`)
       - Google rating + count (if available)
       - A "Cambiar" button to clear selection and re-search
     - Hide ALL other sections: pricing, accommodation, contacts, ABUVI tracking, lat/lng inputs
     - Submit payload: `name`, `location` (formatted address), `latitude`, `longitude`, `googlePlaceId` only
     - Prices default to 0 (backend supports this)
  2. **Edit mode** (`mode === 'edit'`):
     - Show full form with all fields (same as current behavior, with rawAddress fix applied)
  3. **Remove `generateDescription()` function** entirely (IMPROVEMENT 2)
     - Remove the auto-fill of `formData.description` in `handlePlaceSelected()`
     - Description remains available in edit mode for manual entry
  4. Use `v-if="mode === 'edit'"` to wrap sections only shown in edit mode
- **Implementation Notes**: The backend `CreateCampRequest` already has sensible defaults (prices=0, optional fields nullable).

---

### Step 8: Add Facility Icons to Display (IMPROVEMENT 4)

- **File**: `frontend/src/components/camps/AccommodationCapacityDisplay.vue`
- **Action**: Add PrimeVue icons to facility badges and improve visual style
- **Implementation Steps**:
  1. Update facility data structure to include icon mappings:
     - `hasAdaptedMenu` → `pi pi-list` + "Menú adaptado"
     - `hasEnclosedDiningRoom` → `pi pi-building` + "Comedor cerrado"
     - `hasSwimmingPool` → `pi pi-sun` + "Piscina"
     - `hasSportsCourt` → `pi pi-flag` + "Pista polideportiva"
     - `hasForestArea` → `pi pi-globe` + "Pinar / zona natural"
  2. Render badges with `<i :class="icon" />` + label text
  3. Increase badge size with Tailwind: `px-3 py-2 text-sm font-medium` (instead of small pills)
  4. Keep green color scheme for active facilities

---

### Step 9: Improve Facility Toggles in Form (IMPROVEMENT 4)

- **File**: `frontend/src/components/camps/AccommodationCapacityForm.vue`
- **Action**: Replace small `ToggleSwitch` with card-style clickable chips
- **Implementation Steps**:
  1. Create a grid of facility cards (3 columns on desktop, 2 on mobile)
  2. Each card: bordered div with icon + label, click toggles the boolean value
  3. Active state: filled primary/green background, white text, `shadow-sm`
  4. Inactive state: outlined border, muted text color
  5. Use `@click="toggleFacility(key)"` handler
  6. Maintain same reactive `localCapacity` data binding
  7. Larger touch targets: minimum `h-16` or `min-h-[4rem]`

---

### Step 10: Remove Season from Observations Form (IMPROVEMENT 5)

- **File**: `frontend/src/components/camps/CampObservationsSection.vue`
- **Action**: Remove the season selector from the add-observation form
- **Implementation Steps**:
  1. Remove the `Select` for `newSeason` and the `seasonOptions` array
  2. Always send `season: null` when submitting a new observation
  3. Keep the season badge on existing observations (read-only, for imported data)
  4. Simplify form layout: just `Textarea` + "Añadir" button (full width)
  5. Remove unused `newSeason` ref

---

### Step 11: Add `refreshGooglePlaces` to `useCamps` Composable (IMPROVEMENT 6)

- **File**: `frontend/src/composables/useCamps.ts`
- **Action**: Add method to call the new backend refresh endpoint
- **Implementation Steps**:
  1. Add `refreshGooglePlaces(id: string)` method that calls `POST /camps/${id}/refresh-places`
  2. Returns `CampDetailResponse | null`
  3. Handle loading, error states consistently
  4. Export in composable return object

---

### Step 12: Add Google Places Refresh Button to Detail Page (IMPROVEMENT 6)

- **File**: `frontend/src/views/camps/CampLocationDetailPage.vue`
- **Action**: Add "Actualizar datos de Google" button for camps with a Google Place ID
- **Implementation Steps**:
  1. Import `refreshGooglePlaces` from `useCamps`
  2. Add button with `pi pi-refresh` icon in the Google Places / contact info section
  3. Visibility: `v-if="camp.googlePlaceId && auth.isBoard"`
  4. On click: call `refreshGooglePlaces(camp.id)`, show loading spinner on button
  5. On success: refresh camp data (`camp.value = result`), show success toast
  6. On error: show error toast with message from composable
  7. Add `refreshing` ref for button loading state

---

### Step 13: Write Unit Tests

- **Files**: `frontend/src/composables/__tests__/`, `frontend/src/components/camps/__tests__/`
- **Action**: Write Vitest tests for key changes
- **Tests to write**:
  1. **`useCampPhotos.spec.ts`**: Test `fetchPhotos` returns photos from API mock
  2. **`useCamps.spec.ts`**: Test `refreshGooglePlaces` calls correct endpoint and handles success/error
  3. **`CampLocationForm.spec.ts`**:
     - Test create mode renders only name search + confirmation card (not pricing/accommodation sections)
     - Test edit mode renders all fields
     - Test form uses `location` field (not `rawAddress`)
  4. **`CampObservationsSection.spec.ts`**: Test form does not render season selector
  5. **`AccommodationCapacityDisplay.spec.ts`**: Test facility badges render with icons
- **Implementation Notes**: Use Vitest + Vue Test Utils. Mock API calls with `vi.mock`. Follow existing test patterns in the project.

---

### Step 14: Update Technical Documentation

- **Action**: Review and update documentation
- **Implementation Steps**:
  1. Update `ai-specs/specs/data-model.md` if any type changes need documenting
  2. Verify `frontend-standards.mdc` component patterns still apply
  3. Document the `rawAddress` → `location` rename for future reference
  4. Ensure all documentation in English

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Fix `rawAddress` → `location` (critical data bug, must be first)
3. **Step 2**: Fix edit dialog data fetching (critical data overwrite bug)
4. **Step 3**: Add `fetchPhotos` to composable
5. **Step 4**: Load photos on detail page mount
6. **Step 5**: Add edit button/dialog to detail page
7. **Step 6**: Extend `PlaceDetails` interface
8. **Step 7**: Simplify creation form + remove auto-description + show resolved location
9. **Step 8**: Add facility icons to display
10. **Step 9**: Improve facility toggles in form
11. **Step 10**: Remove season from observations form
12. **Step 11**: Add `refreshGooglePlaces` composable method
13. **Step 12**: Add refresh button to detail page
14. **Step 13**: Write unit tests
15. **Step 14**: Update technical documentation

## Testing Checklist

- [ ] `types/camp.ts` uses `location` everywhere (no `rawAddress` references remain)
- [ ] Creating a camp sends `location` field to backend and data persists correctly
- [ ] Editing from list view navigates to detail page (no more edit dialog on list)
- [ ] Detail page has top-right action bar with "Editar", "Ediciones", "Crear propuesta"
- [ ] Clicking "Editar" toggles inline edit mode (no modal)
- [ ] Inline edit form pre-populates all fields correctly from `CampDetailResponse`
- [ ] Clicking "Cancelar" exits edit mode and discards changes
- [ ] Saving inline edit refreshes displayed data and exits edit mode
- [ ] User-uploaded photos display on detail page after page reload
- [ ] Google Places photos display correctly via proxy endpoint
- [ ] Create mode shows only name search + confirmation card
- [ ] Edit mode shows all fields including pricing, accommodation, contacts
- [ ] No auto-generated description on place selection
- [ ] After selecting a place, user sees locality/province/country (not lat/lng)
- [ ] Facility badges show icons in display component
- [ ] Facility toggles are card-style with icons in form
- [ ] Season selector removed from observation form
- [ ] Existing observations with seasons still show season badge
- [ ] Google Places refresh button visible for Board+ on camps with GooglePlaceId
- [ ] Refresh button re-syncs data and updates the page
- [ ] All Vitest unit tests pass
- [ ] No TypeScript errors (`npx tsc --noEmit`)

## Error Handling Patterns

- **Composable errors**: Use reactive `error` ref, set from catch blocks, clear on new requests
- **API errors**: Extract message from `ApiResponse` error envelope via Axios interceptors
- **User feedback**: PrimeVue `Toast` for success/error messages after operations
- **Loading states**: Reactive `loading` ref per composable, bound to button `loading` prop and skeleton displays
- **Edit save failure**: Show toast error, keep form open in edit mode so user doesn't lose changes

## UI/UX Considerations

- **Create form**: Minimal — Google Places autocomplete + read-only confirmation card. Clean, focused experience.
- **Confirmation card**: Show place name (bold), formatted address, locality/province/country, rating. "Cambiar" button to re-search.
- **Detail page header**: Left side = back button + camp name. Right side = action bar with "Editar" (primary), "Ediciones" (outlined), "Crear propuesta" (outlined). Escalation-style layout.
- **Inline edit mode**: Clicking "Editar" replaces read-only sections with the edit form in place. Button toggles to "Cancelar". No modal/dialog. Board+ visibility.
- **Facility cards**: Grid layout (`grid-cols-2 md:grid-cols-3`), min height for touch targets, visual active/inactive states
- **Refresh button**: Secondary style with `pi pi-refresh`, loading spinner during operation, positioned near Google Places data section
- **Responsive**: All changes must work on mobile (`sm:` breakpoints), especially facility cards and create form

## Dependencies

- **PrimeVue components**: Button, AutoComplete, Toast, InputText, Textarea, Tag (already in use). Dialog still used for create on list page, but removed from edit flow.
- **No new npm packages required**
- **Backend**: Requires `POST /api/camps/{id}/refresh-places` endpoint (already implemented in PR #99)

## Notes

- Backend field is `Location` (serialized as `location` in JSON) — the frontend must match exactly
- The backend `CreateCampRequest` already supports minimal payloads (prices default to 0)
- `CampDetailResponse` extends `CampResponse` with additional fields — always use detail endpoint for edit forms
- The `generateDescription()` function in `CampLocationForm.vue` should be completely removed, not just disabled
- Keep season display on existing observations for backward compatibility with imported CSV data
- All code and documentation must be in English
- Follow `<script setup lang="ts">` pattern, no `any` types

## Next Steps After Implementation

1. Create PR targeting `dev` branch
2. Manual QA testing in dev environment
3. Verify Google Places photo proxy works correctly in dev
4. Confirm no regressions on camp list, create, and detail pages

## Implementation Verification

- [ ] TypeScript strict mode passes (`npx tsc --noEmit`)
- [ ] All components use `<script setup lang="ts">`
- [ ] No `any` types introduced
- [ ] All composable methods have proper TypeScript return types
- [ ] PrimeVue components used correctly (props, events)
- [ ] Tailwind utility classes follow project conventions
- [ ] Unit tests cover key functionality (composables, form modes, field rename)
- [ ] Documentation updated
- [ ] No `rawAddress` references remain anywhere in the codebase

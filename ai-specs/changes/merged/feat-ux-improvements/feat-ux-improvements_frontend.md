# Frontend Implementation Plan: feat-ux-improvements — UX Improvements

## Overview

Five targeted UX improvements to the Abuvi web application. All changes follow Vue 3 Composition API with `<script setup lang="ts">`, PrimeVue components, Tailwind CSS, and composable-based architecture. Changes are frontend-only except Improvement 1, which has a minor backend impact (already handled — backend already accepts optional `proposalReason`).

## Architecture Context

### Components Involved

| Component | File | Improvement |
|-----------|------|-------------|
| `CampEditionProposeDialog.vue` | `frontend/src/components/camps/CampEditionProposeDialog.vue` | 1 |
| `CampEditionsPage.vue` | `frontend/src/views/camps/CampEditionsPage.vue` | 2 |
| `CampLocationMap.vue` | `frontend/src/components/camps/CampLocationMap.vue` | 3 |
| `CampLocationCard.vue` | `frontend/src/components/camps/CampLocationCard.vue` | 4 |
| `ProfilePage.vue` | `frontend/src/views/ProfilePage.vue` | 5 |

### Types Involved

| Type File | Types | Improvement |
|-----------|-------|-------------|
| `frontend/src/types/camp-edition.ts` | `ProposeCampEditionRequest` | 1 |
| `frontend/src/types/camp.ts` | `CampLocation` | 3 |
| `frontend/src/types/camp-photo.ts` | `CampPhoto` | 4 |

### Composables Involved

| Composable | File | Improvement |
|------------|------|-------------|
| `useCampEditions` | `frontend/src/composables/useCampEditions.ts` | 1 (read editions for date pre-population) |

### Routing

- Route `/camps/editions/:id` (name: `camp-edition-detail`) already exists in `frontend/src/router/index.ts` — no route changes needed.

### State Management

- No Pinia store changes required. All state is local or composable-based.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a frontend-specific feature branch
- **Branch Name**: `feature/feat-ux-improvements-frontend`
- **Implementation Steps**:
  1. Ensure on latest `main`: `git checkout main && git pull origin main`
  2. Create branch: `git checkout -b feature/feat-ux-improvements-frontend`
  3. Verify: `git branch`
- **Notes**: Separate from any backend branch. The backend already supports optional `proposalReason`/`proposalNotes`.

---

### Step 1: Update TypeScript Types (Improvement 1)

- **File**: `frontend/src/types/camp-edition.ts`
- **Action**: Make `proposalReason` optional and remove `proposalNotes` from `ProposeCampEditionRequest`
- **Implementation Steps**:
  1. In `ProposeCampEditionRequest` interface, change `proposalReason: string` to `proposalReason?: string`
  2. Remove `proposalNotes: string` from `ProposeCampEditionRequest` (backend still accepts it but frontend will no longer send it)
- **Current State** (lines 74-90):
  ```typescript
  export interface ProposeCampEditionRequest extends CreateCampEditionRequest {
    proposalReason: string    // ← change to optional
    proposalNotes: string     // ← remove
    // ...
  }
  ```
- **Target State**:
  ```typescript
  export interface ProposeCampEditionRequest extends CreateCampEditionRequest {
    proposalReason?: string
    // proposalNotes removed
    // ...
  }
  ```

---

### Step 2: Simplify Proposal Dialog (Improvement 1)

- **File**: `frontend/src/components/camps/CampEditionProposeDialog.vue`
- **Action**: Remove `proposalNotes` field, make `proposalReason` optional, add date pre-population
- **Implementation Steps**:

  1. **Remove `proposalNotes` from form ref** (line ~40):
     - Delete `proposalNotes: ''` from the `form` reactive object

  2. **Remove `proposalReason` required validation** (line ~73):
     - Delete the validation block: `if (!form.value.proposalReason.trim()) { errors.proposalReason = 'El motivo de la propuesta es obligatorio' }`
     - Keep `proposalReason` in the form as optional textarea (no validation error when empty)

  3. **Remove `proposalNotes` from template** (around lines 180-191):
     - Delete the entire `proposalNotes` textarea block (label + Textarea + error message)

  4. **Remove `proposalNotes` from submission payload** (line ~92):
     - Delete `proposalNotes: form.value.proposalNotes` from the `proposeEdition()` call

  5. **Add date pre-population logic**:
     - Access `editions` from `useCampEditions()` composable (already imported)
     - Add a `watch` or `computed` that triggers when the dialog opens (`visible` prop changes to `true`):
       ```typescript
       watch(() => props.visible, (isVisible) => {
         if (isVisible) {
           prefillDatesFromPreviousYear()
         }
       })
       ```
     - Implement `prefillDatesFromPreviousYear()`:
       ```typescript
       const prefillDatesFromPreviousYear = () => {
         const targetYear = form.value.year
         const previousEdition = editions.value
           .filter(e => e.year === targetYear - 1)
           .sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime())[0]

         if (previousEdition) {
           const prevStart = new Date(previousEdition.startDate)
           const prevEnd = new Date(previousEdition.endDate)
           form.value.startDate = new Date(targetYear, prevStart.getMonth(), prevStart.getDate())
           form.value.endDate = new Date(targetYear, prevEnd.getMonth(), prevEnd.getDate())
         } else {
           // Default: Aug 15 — Aug 22
           form.value.startDate = new Date(targetYear, 7, 15)
           form.value.endDate = new Date(targetYear, 7, 22)
         }
       }
       ```
     - Also re-trigger when `form.value.year` changes (the user might change the year in the form):
       ```typescript
       watch(() => form.value.year, () => {
         if (props.visible) {
           prefillDatesFromPreviousYear()
         }
       })
       ```

  6. **Pre-populate location and pricing from camp prop** (if not already done):
     - Check if `camp` prop data is used to pre-fill `location`, pricing fields
     - If already pre-filled, no action needed

- **Dependencies**: `useCampEditions` composable must expose `editions` ref (already does)
- **Notes**: Backend already treats both fields as optional — no backend changes needed

---

### Step 3: Add Clickable Edition Names in DataTable (Improvement 2)

- **File**: `frontend/src/views/camps/CampEditionsPage.vue`
- **Action**: Wrap the camp name in Column 1 ("Ubicación") with a `<router-link>` to the edition detail page
- **Implementation Steps**:

  1. **Import `RouterLink`** (if not auto-imported via Vue Router):
     - `import { RouterLink } from 'vue-router'` (Vue 3 auto-registers it globally, so likely no import needed)

  2. **Modify Column 1 template** (lines ~188-192):
     - **Current**:
       ```vue
       <template #body="{ data }">
         <span class="font-medium">{{ data.camp?.name ?? '—' }}</span>
       </template>
       ```
     - **Target**:
       ```vue
       <template #body="{ data }">
         <router-link
           :to="{ name: 'camp-edition-detail', params: { id: data.id } }"
           class="font-medium text-primary hover:underline"
         >
           {{ data.camp?.name ?? '—' }}
         </router-link>
       </template>
       ```

  3. **Verify route exists**: Route `camp-edition-detail` at `/camps/editions/:id` is already defined in `router/index.ts` — confirmed.

- **Notes**: No additional API calls. The `data.id` is the edition ID already present in each DataTable row. Navigation uses Vue Router (no full page reload).

---

### Step 4: Extend CampLocation Type (Improvement 3)

- **File**: `frontend/src/types/camp.ts`
- **Action**: Add `location` and `lastEditionYear` fields to `CampLocation` interface
- **Implementation Steps**:

  1. **Current interface** (lines 94-99):
     ```typescript
     export interface CampLocation {
       latitude: number
       longitude: number
       name: string
       year?: number
     }
     ```

  2. **Updated interface**:
     ```typescript
     export interface CampLocation {
       latitude: number
       longitude: number
       name: string
       year?: number
       location?: string
       lastEditionYear?: number
     }
     ```

  3. **New fields are optional** to maintain backward compatibility with existing usages in `CampEditionDetails.vue`, `CampLocationDetailPage.vue`, and `CampLocationsPage.vue` that don't pass these fields.

---

### Step 5: Improve Map Height and Popups (Improvement 3)

- **File**: `frontend/src/components/camps/CampLocationMap.vue`
- **Action**: Increase map height, enrich popup content
- **Implementation Steps**:

  1. **Increase map container height** (line 99):
     - **Current**: `class="h-96 w-full rounded-lg border border-gray-200"`  (384px)
     - **Target**: `class="h-[500px] w-full rounded-lg border border-gray-200"` (500px)

  2. **Enrich popup content** (lines ~56-59):
     - **Current**:
       ```typescript
       .bindPopup(
         `<strong>${location.name}</strong>${location.year ? `<br>${location.year}` : ''}`
       )
       ```
     - **Target**:
       ```typescript
       .bindPopup(
         `<div class="text-sm">
           <strong>${location.name}</strong>
           ${location.location ? `<br><span>${location.location}</span>` : ''}
           ${location.lastEditionYear ? `<br><span>Última edición: ${location.lastEditionYear}</span>` : ''}
           ${location.year ? `<br><span>${location.year}</span>` : ''}
         </div>`
       )
       ```

  3. **Update parent components** that pass data to `CampLocationMap`:

     **a) `CampLocationsPage.vue`** (main multi-marker usage, line ~301):
     - Current `campLocations` computed already maps camps — add `location` and `lastEditionYear`:
       ```typescript
       const campLocations = computed(() => {
         return filteredCamps.value
           .filter((camp) => camp.latitude !== null && camp.longitude !== null)
           .map((camp) => ({
             latitude: camp.latitude as number,
             longitude: camp.longitude as number,
             name: camp.name,
             location: camp.location ?? undefined,
             lastEditionYear: /* derive from editions if available, or from camp data */,
             year: undefined
           }))
       })
       ```
     - Check if camp editions data is available in this page. If not, `lastEditionYear` can be omitted (it's optional).

     **b) `CampLocationDetailPage.vue`** (single marker, lines 204-214):
     - Add `location` from `camp.location`:
       ```vue
       :locations="[{
         latitude: camp.latitude,
         longitude: camp.longitude,
         name: camp.name,
         location: camp.location ?? undefined
       }]"
       ```

     **c) `CampEditionDetails.vue`** (single marker, lines 80-83):
     - Add `location` from `camp.location` in `mapLocations` computed.

- **Notes**: Leaflet cleanup on `onUnmounted` remains unchanged. Auto-fit bounds logic unchanged.

---

### Step 6: Add Photo Carousel to Camp Cards (Improvement 4)

- **File**: `frontend/src/components/camps/CampLocationCard.vue`
- **Action**: Add a PrimeVue Galleria carousel at the top of the card for camp photos
- **Dependencies**: `import Galleria from 'primevue/galleria'`
- **Implementation Steps**:

  1. **Check data availability**: The `CampLocationCard` receives a `camp` prop. Verify if `camp` includes a `photos` array. Based on exploration:
     - `Camp` type does NOT include photos
     - `CampDetailResponse` includes `photos: CampPlacesPhoto[]`
     - The card is used in list views where full detail may not be loaded

  2. **Add optional `photos` prop** to the card:
     ```typescript
     interface Props {
       camp: Camp
       photos?: CampPhoto[]  // Optional — only shown if provided
     }
     ```

  3. **Add Galleria at the top of the card template** (before the camp name header):
     ```vue
     <Galleria
       v-if="sortedPhotos.length > 0"
       :value="sortedPhotos"
       :show-thumbnails="false"
       :show-item-navigators="sortedPhotos.length > 1"
       :show-indicators="sortedPhotos.length > 1"
       :circular="true"
       :auto-play="false"
       class="w-full"
     >
       <template #item="{ item }">
         <img
           :src="item.url"
           :alt="item.description ?? camp.name"
           class="h-48 w-full object-cover"
         />
       </template>
       <template #caption="{ item }">
         <span v-if="item.description" class="text-sm">{{ item.description }}</span>
       </template>
     </Galleria>
     ```

  4. **Add computed for sorted photos**:
     ```typescript
     const sortedPhotos = computed(() =>
       [...(props.photos ?? [])].sort((a, b) => a.displayOrder - b.displayOrder)
     )
     ```

  5. **Add placeholder when no photos**:
     ```vue
     <div
       v-if="sortedPhotos.length === 0"
       class="flex h-48 w-full items-center justify-center bg-gray-100 text-2xl font-bold text-gray-400"
     >
       {{ camp.name.charAt(0) }}
     </div>
     ```

  6. **Update parent components** passing photos to `CampLocationCard`:
     - Check `CampLocationsPage.vue` and any other parent that renders `CampLocationCard`
     - Pass `photos` prop if camp detail data is available
     - If photos are not loaded in list views, the placeholder will show (graceful fallback)

- **Notes**:
  - `CampPhoto.url` (not `photoUrl` as the enriched spec assumed — confirmed from `camp-photo.ts`)
  - Galleria is already available in PrimeVue setup (used in `AnniversaryCarousel.vue`)
  - Auto-play is disabled per spec

---

### Step 7: Extend Profile Page Layout (Improvement 5)

- **File**: `frontend/src/views/ProfilePage.vue`
- **Action**: Widen the container and use a two-column grid on desktop (Option A — extended width)
- **Implementation Steps**:

  1. **Widen the container** (current: `max-w-3xl`):
     - **Current**: `<div class="py-8 space-y-6 max-w-3xl">`
     - **Target**: `<div class="py-8 space-y-6 max-w-5xl">`

  2. **Add two-column grid for sections**:
     - Wrap the Card sections in a responsive grid:
       ```vue
       <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
         <!-- Personal Information Card -->
         <!-- Account Security Card -->
       </div>
       ```
     - The Family & Members section (tallest, most complex) spans full width below:
       ```vue
       <!-- Family Unit & Members Card — full width -->
       <Card class="lg:col-span-2"> ... </Card>
       ```
     - Or keep Family/Members outside the grid as a separate full-width section.

  3. **Verify responsive behavior**:
     - On `< lg` breakpoint: single column, all sections stacked (same as current)
     - On `>= lg` breakpoint: Personal Info and Security side by side, Family below full width
     - No content removed, no overflow issues

- **Notes**: No backend changes. The `Container` component defaults to `max-w-screen-xl`, so `max-w-5xl` on the inner div is well within bounds.

---

### Step 8: Write Unit Tests

- **Action**: Write Vitest unit tests for all changes following TDD-compatible patterns
- **Test Files**:

  1. **`frontend/src/components/camps/__tests__/CampEditionProposeDialog.test.ts`**
     - Test: form renders without `proposalNotes` field
     - Test: form submits successfully with empty `proposalReason`
     - Test: dates pre-populate from previous year edition (year Y-1 → year Y)
     - Test: dates default to Aug 15-22 when no previous edition exists
     - Test: changing year re-triggers date pre-population

  2. **`frontend/src/views/camps/__tests__/CampEditionsPage.test.ts`**
     - Test: clicking edition name renders `<router-link>` with correct route
     - Test: link points to `camp-edition-detail` with correct `id` param

  3. **`frontend/src/components/camps/__tests__/CampLocationMap.test.ts`**
     - Test: map container has `h-[500px]` class
     - Test: popup content includes `location` when provided
     - Test: popup content includes `lastEditionYear` when provided
     - Test: popup gracefully omits optional fields when not provided

  4. **`frontend/src/components/camps/__tests__/CampLocationCard.test.ts`**
     - Test: renders Galleria when photos are provided (2+ photos)
     - Test: renders placeholder when no photos
     - Test: single photo renders without navigation controls
     - Test: photos are sorted by `displayOrder`

  5. **`frontend/src/views/__tests__/ProfilePage.test.ts`**
     - Test: container uses `max-w-5xl` class
     - Test: grid layout applies `lg:grid-cols-2` on desktop
     - Test: all existing sections are rendered

- **Coverage Target**: ≥90% for all modified files

---

### Step 9: Update Technical Documentation

- **Action**: Update documentation to reflect all UI/UX changes
- **Implementation Steps**:

  1. **`ai-specs/specs/api-endpoints.md`**:
     - Document that `proposalReason` is optional in `POST /api/camps/editions/propose`
     - Note that `proposalNotes` is no longer sent by the frontend

  2. **`ai-specs/specs/frontend-standards.mdc`** (if applicable):
     - No new patterns introduced — existing conventions followed

  3. **Update enriched spec**:
     - Mark completed acceptance criteria in `ai-specs/changes/feat-ux-improvements/feat-ux-improvements_enriched.md`

- **References**: Follow `ai-specs/specs/documentation-standards.mdc`
- **Language**: All documentation in English

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-ux-improvements-frontend`
2. **Step 1**: Update TypeScript types (`ProposeCampEditionRequest`)
3. **Step 2**: Simplify Proposal Dialog (remove fields, add date pre-population)
4. **Step 3**: Add clickable edition names in DataTable
5. **Step 4**: Extend `CampLocation` type
6. **Step 5**: Improve map height and popups
7. **Step 6**: Add photo carousel to camp cards
8. **Step 7**: Extend profile page layout
9. **Step 8**: Write unit tests for all improvements
10. **Step 9**: Update technical documentation

---

## Testing Checklist

### Unit Tests (Vitest)
- [ ] `CampEditionProposeDialog`: proposalNotes field not rendered
- [ ] `CampEditionProposeDialog`: submits with empty proposalReason
- [ ] `CampEditionProposeDialog`: dates pre-populate from Y-1 edition
- [ ] `CampEditionProposeDialog`: dates default to Aug 15-22 when no previous edition
- [ ] `CampEditionsPage`: camp name renders as router-link
- [ ] `CampEditionsPage`: link navigates to correct edition detail route
- [ ] `CampLocationMap`: container height is 500px
- [ ] `CampLocationMap`: popup includes location and lastEditionYear
- [ ] `CampLocationCard`: Galleria renders with multiple photos
- [ ] `CampLocationCard`: placeholder renders with no photos
- [ ] `CampLocationCard`: photos sorted by displayOrder
- [ ] `ProfilePage`: max-w-5xl applied
- [ ] `ProfilePage`: two-column grid on lg breakpoint
- [ ] `ProfilePage`: all sections rendered

### Manual Verification
- [ ] Proposal form: submit without proposalReason — no validation error
- [ ] Proposal form: proposalNotes field is gone
- [ ] Proposal form: dates auto-fill correctly from previous year
- [ ] Editions table: camp name is clickable and navigates to detail
- [ ] Map: visually taller (500px)
- [ ] Map: popup shows location and last edition year
- [ ] Camp cards: photo carousel works with 2+ photos
- [ ] Camp cards: single photo shows without arrows
- [ ] Camp cards: placeholder shows when no photos
- [ ] Profile page: wider layout on desktop
- [ ] Profile page: mobile layout unchanged

---

## Error Handling Patterns

- **Proposal form**: Existing error handling in `useCampEditions` composable (catches errors, sets `error.value`). No changes needed.
- **Router navigation**: `router-link` handles navigation errors via Vue Router's error handling. No custom handling needed.
- **Map popups**: Optional chaining with fallback to empty string for missing `location`/`lastEditionYear`. No error states.
- **Photo carousel**: Graceful fallback to placeholder when `photos` is empty or undefined.
- **Profile page**: Layout-only change, no error scenarios.

---

## UI/UX Considerations

### PrimeVue Components Used
- `Galleria` (Improvement 4 — already available, used in `AnniversaryCarousel`)
- `DataTable`, `Column` (Improvement 2 — already in use)
- `Card`, `Button`, `InputText`, `Tag`, `Skeleton` (Improvement 5 — already in use)
- `DatePicker`, `Textarea`, `InputNumber` (Improvement 1 — already in use)

### Responsive Design
- Improvement 2: Links work at all breakpoints (inline text)
- Improvement 3: Map is full-width with fixed 500px height
- Improvement 5: `grid-cols-1` on mobile, `lg:grid-cols-2` on desktop
- Improvement 6: Galleria is responsive by default (`w-full`)

### Accessibility
- `router-link` provides keyboard-navigable links (Improvement 2)
- Galleria has built-in keyboard navigation (Improvement 4)
- `aria-label` on action buttons maintained (Improvement 2)
- Tab order in profile page remains logical top-to-bottom (Improvement 5)

---

## Dependencies

### npm Packages
- No new packages required. All components (`Galleria`, `DataTable`, `Card`, Leaflet) are already installed.

### PrimeVue Components
- `Galleria` from `primevue/galleria` — already available and configured

---

## Notes

- **Language**: All UI text in Spanish (existing convention). Documentation in English.
- **Backend sync**: Backend already treats `proposalReason` and `proposalNotes` as optional (confirmed in `ProposeCampEditionRequestValidator`). No backend PR needed.
- **CampPhoto.url**: The actual field name is `url` (not `photoUrl` as the enriched spec suggested). Confirmed from `frontend/src/types/camp-photo.ts`.
- **No breaking changes**: All type extensions use optional fields. Existing consumers are unaffected.
- **Galleria auto-play**: Disabled per spec to avoid distracting UX.

---

## Next Steps After Implementation

1. Create PR to `main` with all frontend changes
2. Manual QA on staging environment
3. Verify photo carousel with real camp photo data
4. Monitor for any Leaflet rendering issues on different browsers

---

## Implementation Verification

- [ ] **Code Quality**: All components use `<script setup lang="ts">`, no `any` types, strict TypeScript
- [ ] **Functionality**: All 5 improvements render correctly and behave as specified
- [ ] **Testing**: Vitest coverage ≥90% for modified files
- [ ] **Integration**: Composables connect to backend API correctly (proposal form submission)
- [ ] **Documentation**: All specs and docs updated
- [ ] **Accessibility**: Keyboard navigation works for links, carousel, and grid layout
- [ ] **Responsive**: All improvements work on mobile and desktop breakpoints

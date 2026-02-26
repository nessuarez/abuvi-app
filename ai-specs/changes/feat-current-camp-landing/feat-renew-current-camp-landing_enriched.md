# Enriched User Story: Renew Current Camp Landing Page

**Source:** `ai-specs/changes/feat-current-camp-landing/feat-renew-current-camp-landing.md`
**Date:** 2026-02-26
**Scope:** Frontend-only — redesign and enrichment of `CampPage.vue` (the `/camp` route)

---

## Summary

The current camp landing page (`/camp`, rendered by `frontend/src/views/CampPage.vue`) is minimal: it fetches only the "active" (Open-status) edition via `GET /api/camps/editions/active` and renders a basic `ActiveEditionCard`. It provides no rich information about the camp venue, no photos, no accommodation details, no location map, and no extras.

This story replaces that minimal view with a rich, modern landing page that:
1. Uses the existing `GET /api/camps/current` endpoint (which returns the best-available edition across statuses and includes camp coordinates) instead of `GET /api/camps/editions/active`.
2. Fetches camp venue details (photos, contact info, accommodation capacity, map) from the existing `GET /api/camps/{campId}` endpoint.
3. Fetches the edition's optional extras from `GET /api/camps/editions/{editionId}/extras`.
4. Displays all information in a multi-section, visually rich layout using existing components where possible.

No backend changes are required.

---

## Background: Current State vs Target State

### Current State

**File:** `frontend/src/views/CampPage.vue`

- Calls `GET /api/camps/editions/active` (returns `ActiveCampEditionResponse` — minimal: no coordinates, no venue photos, no accommodation)
- Renders only `ActiveEditionCard` (dates, prices, registration count)
- Shows a single CTA button (register) or "no camp available" message
- No photos, no map, no accommodation, no contact info, no extras

### Target State

A rich landing page with the following sections (in order):
1. **Hero / Header** — camp name, edition year, status badge, key dates and prices
2. **Photo Gallery** — camp venue photos (from `CampDetailResponse.photos` via `GET /api/camps/{campId}`)
3. **Key Info Bar** — capacity/spots, age ranges, registration count
4. **Accommodation** — existing `AccommodationCapacityDisplay` component
5. **Location & Contact** — existing `CampLocationMap` and `CampContactInfo` components
6. **Extras** — optional add-ons from `GET /api/camps/editions/{editionId}/extras`
7. **Registration CTA** — register button (representative only, status-gated)

---

## Data Strategy

### Primary Data Sources

| Data | Endpoint | Response Type (backend) | Current Frontend Type |
|------|----------|-------------------------|-----------------------|
| Current edition (status, dates, prices, coords) | `GET /api/camps/current` | `CurrentCampEditionResponse` | `CampEdition` (via `fetchCurrentCampEdition` in `useCampEditions`) |
| Camp venue detail (photos, contact, accommodation) | `GET /api/camps/{campId}` | `CampDetailResponse` | `CampDetailResponse` (via `getCampById` in `useCamps`) |
| Edition extras | `GET /api/camps/editions/{editionId}/extras` | `CampEditionExtraResponse[]` | `CampEditionExtra[]` |
| Family unit (for representative check) | Already loaded by `useFamilyUnits` | — | — |

### Type Alignment Note

The composable `fetchCurrentCampEdition()` in `useCampEditions.ts` currently stores the result as `CampEdition` (line 19: `const currentCampEdition = ref<CampEdition | null>(null)`), but the backend `GET /api/camps/current` returns `CurrentCampEditionResponse` — which includes extra fields: `CampLatitude`, `CampLongitude`, `AvailableSpots`. A new type `CurrentCampEditionResponse` must be added to `frontend/src/types/camp-edition.ts`, and the ref type in the composable updated accordingly.

---

## Detailed Changes

### 1. Add `CurrentCampEditionResponse` Type

**File:** `frontend/src/types/camp-edition.ts`

Add the following interface (mirrors backend `CurrentCampEditionResponse`):

```typescript
export interface CurrentCampEditionResponse {
  id: string
  campId: string
  campName: string
  campLocation: string | null
  campFormattedAddress: string | null
  campLatitude: number | null
  campLongitude: number | null
  year: number
  startDate: string
  endDate: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge?: number
  customChildMinAge?: number
  customChildMaxAge?: number
  customAdultMinAge?: number
  status: CampEditionStatus
  maxCapacity?: number
  registrationCount: number
  availableSpots?: number
  notes?: string
  createdAt: string
  updatedAt: string
}
```

### 2. Update `useCampEditions` Composable

**File:** `frontend/src/composables/useCampEditions.ts`

- Import `CurrentCampEditionResponse` from `@/types/camp-edition`
- Change the `currentCampEdition` ref type from `CampEdition | null` to `CurrentCampEditionResponse | null`
- Update the return type annotation accordingly

No logic changes — only the ref's generic type changes.

### 3. Redesign `CampPage.vue` (main change)

**File:** `frontend/src/views/CampPage.vue`

**Replace the current implementation entirely** with the new multi-section layout described below.

#### Data Loading

```typescript
// Composables to use
const { currentCampEdition, loading: editionLoading, error: editionError, fetchCurrentCampEdition } = useCampEditions()
const { loading: campLoading, getCampById } = useCamps()
const { familyUnit, getCurrentUserFamilyUnit } = useFamilyUnits()
const auth = useAuthStore()
const router = useRouter()

const camp = ref<CampDetailResponse | null>(null)
const extras = ref<CampEditionExtra[]>([])
const extrasLoading = ref(false)

const loading = computed(() => editionLoading.value || campLoading.value)
const isRepresentative = computed(
  () => !!familyUnit.value && familyUnit.value.representativeUserId === auth.user?.id
)
const canRegister = computed(
  () => currentCampEdition.value?.status === 'Open' && isRepresentative.value
)
```

#### `onMounted` Flow

```typescript
onMounted(async () => {
  await fetchCurrentCampEdition()
  await getCurrentUserFamilyUnit()

  if (currentCampEdition.value) {
    // Load camp venue in parallel with extras
    const [campResult] = await Promise.all([
      getCampById(currentCampEdition.value.campId),
      loadExtras(currentCampEdition.value.id)
    ])
    camp.value = campResult
  }
})

const loadExtras = async (editionId: string): Promise<void> => {
  extrasLoading.value = true
  try {
    const response = await api.get<ApiResponse<CampEditionExtra[]>>(
      `/camps/editions/${editionId}/extras?activeOnly=true`
    )
    extras.value = response.data.success ? (response.data.data ?? []) : []
  } catch {
    extras.value = []
  } finally {
    extrasLoading.value = false
  }
}
```

#### Template Structure

```html
<template>
  <Container>
    <div class="py-8">

      <!-- LOADING STATE -->
      <div v-if="loading" class="flex justify-center py-16" role="status" data-testid="camp-loading">
        <ProgressSpinner />
      </div>

      <!-- ERROR STATE -->
      <Message v-else-if="editionError" severity="error" :closable="false" class="mb-4">
        {{ editionError }}
      </Message>

      <!-- NO CAMP AVAILABLE -->
      <div v-else-if="!currentCampEdition"
        class="rounded-lg border border-gray-200 bg-gray-50 p-8 text-center"
        data-testid="camp-empty">
        <p class="text-gray-500">No hay ningún campamento disponible para este año.</p>
        <p class="mt-1 text-sm text-gray-400">Cuando haya una edición disponible, aparecerá aquí.</p>
      </div>

      <!-- MAIN CONTENT -->
      <div v-else class="space-y-8">

        <!-- SECTION 1: HERO HEADER -->
        <CampHeroSection
          :edition="currentCampEdition"
          data-testid="camp-hero"
        />

        <!-- SECTION 2: PHOTO GALLERY (only if camp has photos) -->
        <CampPlacesGallery
          v-if="camp && camp.photos.length > 0"
          :photos="camp.photos"
          data-testid="camp-gallery"
        />

        <!-- SECTION 3: REGISTRATION CTA -->
        <div class="flex flex-col items-start gap-3 sm:flex-row sm:items-center"
          data-testid="camp-cta">
          <Button
            v-if="currentCampEdition.status === 'Open' && isRepresentative"
            label="Inscribirse al campamento"
            icon="pi pi-user-plus"
            size="large"
            @click="goToRegister"
            data-testid="register-button"
          />
          <Button
            v-else-if="currentCampEdition.status === 'Open' && !isRepresentative"
            label="Solo el representante puede inscribirse"
            icon="pi pi-info-circle"
            severity="secondary"
            size="large"
            disabled
          />
          <RouterLink
            v-if="currentCampEdition.status === 'Open'"
            :to="{ name: 'registrations' }"
            class="text-sm text-blue-600 underline hover:text-blue-800"
          >
            Ver mis inscripciones
          </RouterLink>
        </div>

        <p
          v-if="currentCampEdition.status === 'Open' && !isRepresentative && familyUnit"
          class="text-sm text-amber-600"
        >
          Solo el representante de la unidad familiar puede inscribir a la familia.
        </p>

        <!-- SECTION 4: ACCOMMODATION (from camp venue) -->
        <AccommodationCapacityDisplay
          v-if="camp?.accommodationCapacity"
          :capacity="camp.accommodationCapacity"
          :total-bed-capacity="camp.calculatedTotalBedCapacity"
          data-testid="camp-accommodation"
        />

        <!-- SECTION 5: LOCATION & CONTACT (from camp venue) -->
        <div v-if="camp" class="grid grid-cols-1 gap-6 lg:grid-cols-2">
          <CampContactInfo :camp="camp" data-testid="camp-contact" />

          <div v-if="camp.latitude !== null && camp.longitude !== null"
            class="rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="mb-4 text-lg font-semibold text-gray-900">Ubicación</h2>
            <CampLocationMap
              :locations="[{
                latitude: camp.latitude,
                longitude: camp.longitude,
                name: camp.name
              }]"
            />
          </div>
        </div>

        <!-- SECTION 6: EXTRAS (only if any active extras exist) -->
        <CampExtrasSection
          v-if="extras.length > 0"
          :extras="extras"
          :loading="extrasLoading"
          data-testid="camp-extras"
        />

      </div>
    </div>
  </Container>
</template>
```

### 4. Create `CampHeroSection.vue` (new component)

**File:** `frontend/src/components/camps/CampHeroSection.vue`

This component replaces the existing `ActiveEditionCard.vue` for the landing page context. It renders the top summary of the current edition with richer visual design.

**Props:**
```typescript
defineProps<{
  edition: CurrentCampEditionResponse
}>()
```

**Displayed Fields:**
- `edition.campName` — main heading (h1)
- `edition.campLocation` or `edition.campFormattedAddress` — subtitle
- `edition.status` — `CampEditionStatusBadge` component
- `edition.startDate` / `edition.endDate` — formatted date range
- `edition.year` — year badge
- `edition.pricePerAdult`, `edition.pricePerChild`, `edition.pricePerBaby` — price grid (3 columns)
- `edition.maxCapacity` — if present, show capacity
- `edition.registrationCount` — registration count
- `edition.availableSpots` — if present, show available spots with visual indicator (green/amber/red based on fill rate)
- `edition.notes` — if present, show in a highlighted info box

**Styling:** Follow the existing `ActiveEditionCard.vue` design language (green header, gray body cards), but extend with availability indicator and richer date/info display.

**`data-testid` attributes:**
- `camp-hero-name`
- `camp-hero-dates`
- `camp-hero-prices`
- `camp-hero-status`
- `camp-hero-availability`

### 5. Create `CampExtrasSection.vue` (new component)

**File:** `frontend/src/components/camps/CampExtrasSection.vue`

Renders the list of optional add-ons available for the edition.

**Props:**
```typescript
defineProps<{
  extras: CampEditionExtra[]
  loading: boolean
}>()
```

**Displayed Fields per extra:**
- `extra.name` — bold title
- `extra.description` — optional subtitle
- `extra.price` — formatted as currency
- `extra.pricingType` — display as badge: "Por persona" / "Por familia"
- `extra.pricingPeriod` — display as badge: "Precio único" / "Por día"
- `extra.isRequired` — if true, show "Incluido" badge in amber
- `extra.maxQuantity` / `extra.currentQuantity` — if `maxQuantity` is set, show availability bar

**Layout:** Responsive grid `grid-cols-1 sm:grid-cols-2 lg:grid-cols-3`, each extra as a card with border.

**`data-testid` attributes:**
- `extras-section`
- `extra-card-{extra.id}`

---

## Files to Modify / Create

| File | Action | Description |
|------|--------|-------------|
| `frontend/src/types/camp-edition.ts` | Modify | Add `CurrentCampEditionResponse` interface |
| `frontend/src/composables/useCampEditions.ts` | Modify | Update `currentCampEdition` ref type to `CurrentCampEditionResponse` |
| `frontend/src/views/CampPage.vue` | Modify | Full redesign — multi-section layout |
| `frontend/src/components/camps/CampHeroSection.vue` | Create | New hero/header component for edition summary |
| `frontend/src/components/camps/CampExtrasSection.vue` | Create | New extras listing component |

**Existing components reused without modification:**
- `frontend/src/components/camps/CampPlacesGallery.vue` — photo gallery
- `frontend/src/components/camps/CampContactInfo.vue` — contact info
- `frontend/src/components/camps/CampLocationMap.vue` — Leaflet map
- `frontend/src/components/camps/AccommodationCapacityDisplay.vue` — accommodation breakdown
- `frontend/src/components/camps/CampEditionStatusBadge.vue` — status chip

---

## API Endpoints Used (No Changes Required)

| Method | URL | Auth | Used For |
|--------|-----|------|----------|
| `GET` | `/api/camps/current` | Member+ | Best-available edition (status priority + coords) |
| `GET` | `/api/camps/{campId}` | Admin/Board | Camp venue detail (photos, contact, accommodation) |
| `GET` | `/api/camps/editions/{editionId}/extras?activeOnly=true` | Member+ | Active add-ons for the edition |

> Note: `GET /api/camps/{campId}` is currently restricted to `Admin/Board` only (see `CampsEndpoints.cs` line 22). If this page is intended for all authenticated Members, the route authorization must be relaxed to `Member+` or a new Member-accessible endpoint must be created. **This must be confirmed with the product owner before implementation.** As a safe default, the venue detail section (`camp`) should render gracefully if the fetch returns 403 (e.g., skip photos/contact/map sections silently).

---

## Acceptance Criteria

- [ ] Navigating to `/camp` loads and displays the current best-available edition (not only Open status)
- [ ] If no edition exists within the 1-year lookback window, the empty state message is shown
- [ ] The hero section shows: camp name, location, status badge, date range, price grid, available spots, and notes
- [ ] Camp venue photos (from Google Places) are displayed in a gallery when available
- [ ] Accommodation capacity breakdown is visible when camp has accommodation data
- [ ] Location map is rendered when camp has coordinates
- [ ] Contact information (address, phone, website, Google rating) is displayed when available
- [ ] Active extras are listed when the edition has them
- [ ] The "Inscribirse" button appears only when status is `Open` AND the user is the family representative
- [ ] The page renders correctly at mobile (sm), tablet (md), and desktop (lg) breakpoints
- [ ] Loading spinner is shown while data is fetching
- [ ] Error messages are shown if the API calls fail

---

## Non-Functional Requirements

### Performance
- The three API calls (`/api/camps/current`, `/api/camps/{campId}`, `/api/camps/editions/{editionId}/extras`) should be executed with maximum parallelism: the camp and extras calls must be fired together using `Promise.all` only after the edition call resolves (since `campId` and `editionId` depend on it).
- Photo images use `loading="lazy"` (already the case in `CampPlacesGallery`).
- No polling or reactive watchers that trigger re-fetches.

### Security
- The page requires authentication (`requiresAuth: true` in router — already set for the `/camp` route).
- Do not expose `campId` or `editionId` in the URL; they are internal IDs used only for API calls.
- If `GET /api/camps/{campId}` returns 403 for non-Board users, degrade gracefully (hide venue sections, do not throw an unhandled error).

### Accessibility
- All images must have meaningful `alt` text.
- Map container must have `aria-label="Mapa de ubicación del campamento"`.
- Status badges must not rely solely on color (include text).
- Loading state must have `role="status"`.

### Testing
- All new components must have Vitest unit tests with ≥90% branch coverage (per `frontend-standards.mdc`).

---

## Testing Requirements

### `CampHeroSection.vue` unit tests
**File:** `frontend/src/components/camps/__tests__/CampHeroSection.test.ts`

- Renders camp name, location, date range, and prices
- Renders `CampEditionStatusBadge` with correct status
- Shows available spots when `availableSpots` is defined
- Does not render availability bar when `availableSpots` is null
- Renders notes block when `notes` is present
- Does not render notes block when `notes` is null/undefined

### `CampExtrasSection.vue` unit tests
**File:** `frontend/src/components/camps/__tests__/CampExtrasSection.test.ts`

- Renders one card per extra
- Displays price formatted as currency
- Shows "Por persona" badge for `PerPerson` pricing type
- Shows "Por familia" badge for `PerFamily` pricing type
- Shows "Incluido" badge when `isRequired` is true
- Does not render when `extras` is empty (component should guard with `v-if` in parent)

### `CampPage.vue` unit tests
**File:** `frontend/src/views/__tests__/CampPage.test.ts`

- Shows loading spinner while fetching
- Shows empty state when `currentCampEdition` is null
- Renders `CampHeroSection` when edition is loaded
- Does NOT render photo gallery when `camp.photos` is empty
- Renders gallery when `camp.photos` has entries
- Does NOT render extras section when `extras` is empty
- Renders extras section when active extras exist
- "Inscribirse" button is visible when status is `Open` and user is representative
- "Inscribirse" button is disabled/hidden when user is NOT the representative
- Clicking "Inscribirse" navigates to `{ name: 'registration-new', params: { editionId } }`

---

## Implementation Order (Recommended)

1. Add `CurrentCampEditionResponse` to `frontend/src/types/camp-edition.ts` and update the composable ref type — smallest, safest change, unblocks everything else.
2. Create `CampHeroSection.vue` with unit tests — UI-only, no API dependency.
3. Create `CampExtrasSection.vue` with unit tests — UI-only, no API dependency.
4. Rewrite `CampPage.vue` — wires everything together; write view-level tests last.

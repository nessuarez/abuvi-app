# Frontend Implementation Plan: feat-camps-extra-data — Camp Extended Information from Google Places

## Overview

This feature enriches the frontend camp experience with contact information and photo galleries sourced from Google Places. When a camp has a `googlePlaceId`, the backend auto-enriches it with address, phone, website, Google Maps link, rating, and photos. The frontend must display this data in dedicated components, handle graceful degradation when data is absent, and update TypeScript types to align with the new backend DTOs.

**Architecture principles applied**:
- Vue 3 Composition API (`<script setup lang="ts">`)
- Composable-based API communication (`useCamps` updated to handle `CampDetailResponse`)
- PrimeVue + Tailwind CSS for all UI (no `<style>` blocks)
- Strict TypeScript typing — no `any`

---

## Architecture Context

### Key Backend Changes (consumed by frontend)

- `GET /api/camps` now returns **`CampResponse`** with lightweight extended fields: `formattedAddress`, `phoneNumber`, `websiteUrl`, `googleMapsUrl`, `googleRating`, `googleRatingCount`, `businessStatus`
- `GET /api/camps/{id}` now returns **`CampDetailResponse`** — all extended fields + `photos: CampPhotoResponse[]`
- Photos use **`photoReference`** (not URL). To display, build URL: `/api/places/photo?reference={ref}&maxwidth={w}` — this is a backend proxy that keeps the Google API key server-side
- No changes to `CreateCampRequest` or `UpdateCampRequest` — forms unchanged

### Components Involved

**New**:
- `frontend/src/components/camps/CampContactInfo.vue` — displays address, phone, website, Google Maps link, rating
- `frontend/src/components/camps/CampPhotoGallery.vue` — photo gallery with primary photo + thumbnail grid + Google attribution

**Modified**:
- `frontend/src/types/camp.ts` — add `CampDetailResponse`, `CampPhoto`, update `Camp` with new optional fields
- `frontend/src/composables/useCamps.ts` — `getCampById` returns `CampDetailResponse`
- `frontend/src/views/camps/CampLocationDetailPage.vue` — integrate new components, fix field name mismatches
- `frontend/src/components/camps/CampLocationCard.vue` — add optional Google rating badge

### State Management

- No Pinia store needed — camp detail data is page-local (`ref` in `CampLocationDetailPage.vue`)
- `useCamps` composable remains the sole API communication layer

### Routing

- No new routes required
- `CampLocationDetailPage.vue` is already routed as `camp-location-detail`

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a dedicated frontend branch
- **Branch Naming**: `feature/feat-camps-extra-data-frontend`
- **Implementation Steps**:
  1. Verify current branch and status: `git status`
  2. Checkout main and pull latest: `git checkout main && git pull origin main`
  3. Create new branch: `git checkout -b feature/feat-camps-extra-data-frontend`
  4. Verify: `git branch`
- **Notes**: Do NOT commit on `feature/feat-camps-extra-data-backend` branch. The backend branch must be merged and running locally before E2E tests can be executed against real data.

---

### Step 1: Update TypeScript Types

- **File**: `frontend/src/types/camp.ts`
- **Action**: Add `CampPhoto`, `CampDetailResponse` interfaces; extend existing `Camp` interface with lightweight extended fields
- **Implementation Steps**:

  1. **Extend existing `Camp` interface** — add new optional fields after `googlePlaceId`:

  ```typescript
  export interface Camp {
    id: string
    name: string
    description: string | null
    location: string | null
    latitude: number | null
    longitude: number | null
    googlePlaceId: string | null
    // --- NEW: lightweight extended fields (present in list AND detail) ---
    formattedAddress: string | null
    phoneNumber: string | null
    websiteUrl: string | null
    googleMapsUrl: string | null
    googleRating: number | null
    googleRatingCount: number | null
    businessStatus: string | null
    // --- existing fields ---
    pricePerAdult: number
    pricePerChild: number
    pricePerBaby: number
    isActive: boolean
    createdAt: string
    updatedAt: string
    editionCount?: number
  }
  ```

  2. **Add `CampPhoto` interface** (matches `CampPhotoResponse` DTO):

  ```typescript
  export interface CampPhoto {
    id: string
    photoReference: string | null
    photoUrl: string | null
    width: number
    height: number
    attributionName: string
    attributionUrl: string | null
    isPrimary: boolean
    displayOrder: number
  }
  ```

  3. **Add `CampDetailResponse` interface** (extends `Camp` with all detail fields and photos):

  ```typescript
  export interface CampDetailResponse extends Camp {
    // Full address breakdown (detail-only):
    streetAddress: string | null
    locality: string | null
    administrativeArea: string | null
    postalCode: string | null
    country: string | null
    nationalPhoneNumber: string | null
    // Metadata:
    placeTypes: string | null   // JSON string e.g. "[\"campground\"]"
    lastGoogleSyncAt: string | null
    // Photos:
    photos: CampPhoto[]
  }
  ```

- **Notes**:
  - `Camp` is used for list endpoints (`GET /api/camps`) — all new fields are nullable, backward compatible
  - `CampDetailResponse` is used for the detail endpoint (`GET /api/camps/{id}`)
  - Keep `CreateCampRequest` and `UpdateCampRequest` **unchanged** — backend auto-enriches from GooglePlaceId
  - Keep `CampLocation` interface unchanged — it is used for Leaflet map markers and is unrelated

---

### Step 2: Update `useCamps` Composable

- **File**: `frontend/src/composables/useCamps.ts`
- **Action**: Update `getCampById` to return `CampDetailResponse` instead of `Camp`. Update `createCamp` return type as POST now also returns `CampDetailResponse`.
- **Implementation Steps**:

  1. Add `CampDetailResponse` to the import from `@/types/camp`:
  ```typescript
  import type { Camp, CampDetailResponse, CreateCampRequest, UpdateCampRequest, CampStatus } from '@/types/camp'
  ```

  2. Update `getCampById` signature and response type:
  ```typescript
  const getCampById = async (id: string): Promise<CampDetailResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampDetailResponse>>(`/camps/${id}`)
      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: unknown) {
      error.value = (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message || 'Error al cargar campamento'
      console.error('Failed to fetch camp:', err)
      return null
    } finally {
      loading.value = false
    }
  }
  ```

  3. Update `createCamp` return type (backend POST now returns `CampDetailResponse`):
  ```typescript
  const createCamp = async (request: CreateCampRequest): Promise<CampDetailResponse | null> => {
    // ... existing try/catch/finally structure ...
    const response = await api.post<ApiResponse<CampDetailResponse>>('/camps', request)
    if (response.data.success && response.data.data) {
      camps.value = [...(camps.value || []), response.data.data]
      return response.data.data
    }
    // ...
  }
  ```

  4. Update the composable return statement to reflect the new types (the function signatures change, no structural change to the returned object)

- **Notes**:
  - `fetchCamps` returns `Camp[]` (lightweight `CampResponse` from list endpoint) — no change needed
  - `updateCamp` and `deleteCamp` — no change needed

---

### Step 3: Create `CampContactInfo.vue` Component

- **File**: `frontend/src/components/camps/CampContactInfo.vue`
- **Action**: New component that displays all contact information from Google Places
- **Component Signature**: `defineProps<{ camp: CampDetailResponse }>()`
- **Implementation Steps**:

  1. Create the file. The component renders **nothing** when all contact fields are null (graceful degradation for camps without Google Places data).

  2. Full implementation:

  ```vue
  <script setup lang="ts">
  import { computed } from 'vue'
  import Button from 'primevue/button'
  import type { CampDetailResponse } from '@/types/camp'

  const props = defineProps<{ camp: CampDetailResponse }>()

  const hasContactInfo = computed(() =>
    !!(props.camp.formattedAddress ||
    props.camp.phoneNumber ||
    props.camp.websiteUrl ||
    props.camp.googleMapsUrl ||
    props.camp.googleRating !== null)
  )

  const businessStatusLabel = computed((): string | null => {
    const labels: Record<string, string> = {
      OPERATIONAL: 'Operativo',
      CLOSED_TEMPORARILY: 'Cerrado temporalmente',
      CLOSED_PERMANENTLY: 'Cerrado permanentemente'
    }
    return props.camp.businessStatus ? (labels[props.camp.businessStatus] ?? props.camp.businessStatus) : null
  })

  const businessStatusClass = computed((): string => {
    const classes: Record<string, string> = {
      OPERATIONAL: 'bg-green-100 text-green-800',
      CLOSED_TEMPORARILY: 'bg-yellow-100 text-yellow-800',
      CLOSED_PERMANENTLY: 'bg-red-100 text-red-800'
    }
    return props.camp.businessStatus ? (classes[props.camp.businessStatus] ?? 'bg-gray-100 text-gray-800') : ''
  })

  const formattedRating = computed((): string | null =>
    props.camp.googleRating !== null ? props.camp.googleRating.toFixed(1) : null
  )
  </script>

  <template>
    <div v-if="hasContactInfo" class="rounded-lg border border-gray-200 bg-white p-6">
      <h2 class="mb-4 text-lg font-semibold text-gray-900">Información de contacto</h2>

      <div class="space-y-3">
        <!-- Address -->
        <div v-if="camp.formattedAddress" class="flex items-start gap-3">
          <i class="pi pi-map-marker mt-0.5 text-gray-400"></i>
          <div>
            <p class="text-gray-700">{{ camp.formattedAddress }}</p>
            <a
              v-if="camp.googleMapsUrl"
              :href="camp.googleMapsUrl"
              target="_blank"
              rel="noopener noreferrer"
              class="text-sm text-blue-600 hover:underline"
              aria-label="Abrir en Google Maps"
            >
              Ver en Google Maps
              <i class="pi pi-external-link ml-1 text-xs"></i>
            </a>
          </div>
        </div>

        <!-- Phone -->
        <div v-if="camp.phoneNumber" class="flex items-center gap-3">
          <i class="pi pi-phone text-gray-400"></i>
          <a
            :href="`tel:${camp.phoneNumber}`"
            class="text-gray-700 hover:text-blue-600"
            :aria-label="`Llamar al ${camp.phoneNumber}`"
          >
            {{ camp.nationalPhoneNumber ?? camp.phoneNumber }}
          </a>
        </div>

        <!-- Website -->
        <div v-if="camp.websiteUrl" class="flex items-center gap-3">
          <i class="pi pi-globe text-gray-400"></i>
          <a
            :href="camp.websiteUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="truncate text-blue-600 hover:underline"
            :aria-label="`Visitar web de ${camp.name}`"
          >
            {{ camp.websiteUrl.replace(/^https?:\/\//, '').replace(/\/$/, '') }}
          </a>
        </div>

        <!-- Rating -->
        <div v-if="formattedRating" class="flex items-center gap-3">
          <i class="pi pi-star-fill text-yellow-400"></i>
          <div class="flex items-center gap-2">
            <span class="font-semibold text-gray-900">{{ formattedRating }}</span>
            <span v-if="camp.googleRatingCount" class="text-sm text-gray-500">
              ({{ camp.googleRatingCount }} valoraciones en Google)
            </span>
          </div>
        </div>

        <!-- Business Status -->
        <div v-if="businessStatusLabel" class="flex items-center gap-3">
          <i class="pi pi-info-circle text-gray-400"></i>
          <span
            :class="businessStatusClass"
            class="rounded-full px-2 py-0.5 text-xs font-medium"
          >
            {{ businessStatusLabel }}
          </span>
        </div>

        <!-- Google Maps standalone button (when no address but there is a Maps URL) -->
        <div v-if="camp.googleMapsUrl && !camp.formattedAddress" class="pt-1">
          <Button
            label="Abrir en Google Maps"
            icon="pi pi-map"
            outlined
            size="small"
            as="a"
            :href="camp.googleMapsUrl"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="Abrir ubicación en Google Maps"
          />
        </div>

        <!-- Last sync info -->
        <p v-if="camp.lastGoogleSyncAt" class="pt-2 text-xs text-gray-400">
          Datos de Google Places actualizados el
          {{ new Date(camp.lastGoogleSyncAt).toLocaleDateString('es-ES') }}
        </p>
      </div>
    </div>
  </template>
  ```

- **Dependencies**: `primevue/button`, `@/types/camp`
- **Implementation Notes**:
  - No `<style>` block — Tailwind only
  - `hasContactInfo` computed at root level — entire section hidden when all fields are null
  - Phone links use `tel:` URI scheme for mobile click-to-call
  - National phone is preferred for display; falls back to international if absent
  - External links have `target="_blank" rel="noopener noreferrer"` for security
  - Website URL stripped of protocol and trailing slash for clean UX

---

### Step 4: Create `CampPhotoGallery.vue` Component

- **File**: `frontend/src/components/camps/CampPhotoGallery.vue`
- **Action**: New component displaying a primary photo + thumbnail grid with Google attribution
- **Component Signature**: `defineProps<{ photos: CampPhoto[] }>()`
- **Implementation Steps**:

  1. Create the file. Use the **backend photo proxy URL** to build image `src`:
     - Format: `{VITE_API_URL}/places/photo?reference={photoReference}&maxwidth={width}`
     - This keeps the Google API key server-side
     - If `photoUrl` is already set (Phase 2), use it directly

  2. Full implementation:

  ```vue
  <script setup lang="ts">
  import { computed, ref } from 'vue'
  import Image from 'primevue/image'
  import type { CampPhoto } from '@/types/camp'

  const props = defineProps<{ photos: CampPhoto[] }>()

  const apiBase = (import.meta.env.VITE_API_URL as string) ?? ''

  const getPhotoUrl = (photo: CampPhoto, maxWidth = 800): string => {
    if (photo.photoUrl) return photo.photoUrl
    if (photo.photoReference) {
      return `${apiBase}/places/photo?reference=${encodeURIComponent(photo.photoReference)}&maxwidth=${maxWidth}`
    }
    return ''
  }

  const primaryPhoto = computed<CampPhoto | null>(() =>
    props.photos.find((p) => p.isPrimary) ?? props.photos[0] ?? null
  )

  const thumbnailPhotos = computed<CampPhoto[]>(() =>
    props.photos.filter((p) => !p.isPrimary).slice(0, 8)
  )

  const activePhoto = ref<CampPhoto | null>(null)

  const displayedPhoto = computed<CampPhoto | null>(() =>
    activePhoto.value ?? primaryPhoto.value
  )

  const selectPhoto = (photo: CampPhoto): void => {
    activePhoto.value = photo
  }
  </script>

  <template>
    <div v-if="photos.length > 0" class="rounded-lg border border-gray-200 bg-white p-6">
      <h2 class="mb-4 text-lg font-semibold text-gray-900">Fotos</h2>

      <!-- Primary / Active Photo -->
      <div v-if="displayedPhoto" class="mb-3">
        <Image
          :src="getPhotoUrl(displayedPhoto, 800)"
          :alt="`Foto del campamento`"
          :preview="true"
          image-class="w-full rounded-lg object-cover max-h-72"
          class="block w-full"
        />
        <!-- Attribution -->
        <p class="mt-1 text-right text-xs text-gray-400">
          Foto de
          <a
            v-if="displayedPhoto.attributionUrl"
            :href="displayedPhoto.attributionUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="hover:underline"
          >
            {{ displayedPhoto.attributionName }}
          </a>
          <span v-else>{{ displayedPhoto.attributionName }}</span>
          · Google Maps
        </p>
      </div>

      <!-- Thumbnail Grid -->
      <div
        v-if="thumbnailPhotos.length > 0"
        class="grid grid-cols-4 gap-2 sm:grid-cols-6 md:grid-cols-8"
      >
        <button
          v-for="photo in thumbnailPhotos"
          :key="photo.id"
          type="button"
          class="overflow-hidden rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
          :aria-label="`Ver foto de ${photo.attributionName}`"
          @click="selectPhoto(photo)"
        >
          <img
            :src="getPhotoUrl(photo, 200)"
            :alt="`Miniatura`"
            class="h-16 w-full object-cover transition-opacity hover:opacity-80"
            loading="lazy"
          />
        </button>
      </div>

      <!-- Footer attribution -->
      <p class="mt-3 text-xs text-gray-400">
        Imágenes proporcionadas por Google Maps.
        <span v-if="photos.length > 1">{{ photos.length }} fotos disponibles.</span>
      </p>
    </div>
  </template>
  ```

- **Dependencies**: `primevue/image`, `@/types/camp`
- **Implementation Notes**:
  - PrimeVue `Image` provides built-in preview/zoom — use `:preview="true"`
  - `maxwidth=800` for primary photo, `200` for thumbnails — controls Google API bandwidth cost
  - Thumbnails limited to 8 to avoid excessive API requests
  - `loading="lazy"` on `<img>` thumbnail elements
  - Attribution display is **required** by Google Places API Terms of Service
  - `VITE_API_URL` is already configured in the project environment

---

### Step 5: Update `CampLocationDetailPage.vue`

- **File**: `frontend/src/views/camps/CampLocationDetailPage.vue`
- **Action**: Import and integrate the two new components; update camp type to `CampDetailResponse`; fix field name mismatches present in the current template
- **Implementation Steps**:

  1. **Update imports** in `<script setup>`:
  ```typescript
  import CampContactInfo from '@/components/camps/CampContactInfo.vue'
  import CampPhotoGallery from '@/components/camps/CampPhotoGallery.vue'
  import type { CampDetailResponse } from '@/types/camp'
  // Remove the existing: import type { Camp } from '@/types/camp'
  ```

  2. **Update camp ref type**:
  ```typescript
  const camp = ref<CampDetailResponse | null>(null)
  ```

  3. **Fix field name mismatches** — the current template references fields that don't match the actual Camp type. Replace:
     - `camp.basePriceAdult` → `camp.pricePerAdult`
     - `camp.basePriceChild` → `camp.pricePerChild`
     - `camp.basePriceBaby` → `camp.pricePerBaby`
     - `camp.status` — the `Camp` type has `isActive: boolean`, not a `status` string. Replace the status badge logic: show "Activo" / "Inactivo" based on `camp.isActive`. Remove the `statusLabel` function and replace the badge with:
     ```html
     <span
       :class="camp.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'"
       class="rounded-full px-3 py-1 text-sm font-medium"
     >
       {{ camp.isActive ? 'Activo' : 'Inactivo' }}
     </span>
     ```
     - Coordinates display `camp.latitude.toFixed(4), camp.longitude.toFixed(4)` — guard with `v-if="camp.latitude !== null && camp.longitude !== null"`

  4. **Add `CampContactInfo`** in the template's left column (Info Panel), after the "Descripción" section:
  ```html
  <!-- Contact Information (Google Places) -->
  <CampContactInfo :camp="camp" />
  ```

  5. **Add `CampPhotoGallery`** in the template's right column (Map Panel), after the map section closing `</div>`:
  ```html
  <!-- Photo Gallery (Google Places) -->
  <CampPhotoGallery v-if="camp.photos.length > 0" :photos="camp.photos" class="mt-6" />
  ```

  6. Remove the `statusLabel` function from the script (it's replaced by inline `isActive` logic).

- **Notes**:
  - Both new components handle their own null/empty states — no additional conditional wrapping needed in the parent beyond what's shown
  - The existing `v-else-if="camp"` guard on the main content div ensures both components only mount when `camp` data is loaded

---

### Step 6: Update `CampLocationCard.vue` (Rating Badge)

- **File**: `frontend/src/components/camps/CampLocationCard.vue`
- **Action**: Add an optional Google rating badge when `googleRating` is present
- **Implementation Steps**:

  1. In the card header area (the `div` containing the name and status badge), add after the status `<span>`:
  ```html
  <span
    v-if="camp.googleRating !== null"
    class="flex items-center gap-1 rounded-full bg-yellow-50 px-2 py-1 text-xs font-medium text-yellow-700"
    :aria-label="`Valoración de Google: ${camp.googleRating.toFixed(1)}`"
  >
    <i class="pi pi-star-fill text-yellow-400 text-xs"></i>
    {{ camp.googleRating.toFixed(1) }}
  </span>
  ```

  2. No other changes to this file.

- **Notes**: `Camp` type already has `googleRating: number | null` after Step 1 — no import/type changes needed here.

---

### Step 7: Write Vitest Unit Tests

- **File**: `frontend/src/components/camps/__tests__/CampContactInfo.test.ts`
- **Action**: Create unit tests for `CampContactInfo.vue`

  ```typescript
  import { describe, it, expect } from 'vitest'
  import { mount } from '@vue/test-utils'
  import CampContactInfo from '../CampContactInfo.vue'
  import type { CampDetailResponse } from '@/types/camp'

  const makeCamp = (overrides: Partial<CampDetailResponse> = {}): CampDetailResponse => ({
    id: '1', name: 'Test Camp', description: null, location: null,
    latitude: 42.0, longitude: 2.7, googlePlaceId: 'ChIJ123',
    formattedAddress: 'Crta Pujarnol, km 5, Girona',
    streetAddress: 'Crta Pujarnol, km 5', locality: 'Pujarnol',
    administrativeArea: 'Girona', postalCode: '17834', country: 'España',
    phoneNumber: '+34 972 59 05 07', nationalPhoneNumber: '972 59 05 07',
    websiteUrl: 'http://www.example.com/', googleMapsUrl: 'https://maps.google.com/?cid=123',
    googleRating: 4.2, googleRatingCount: 113, businessStatus: 'OPERATIONAL',
    placeTypes: '["campground"]', lastGoogleSyncAt: '2026-01-15T00:00:00Z',
    pricePerAdult: 100, pricePerChild: 80, pricePerBaby: 0,
    isActive: true, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z',
    photos: [],
    ...overrides
  })

  describe('CampContactInfo', () => {
    it('renders nothing when all contact fields are null', () => {
      const wrapper = mount(CampContactInfo, {
        props: { camp: makeCamp({ formattedAddress: null, phoneNumber: null, websiteUrl: null, googleMapsUrl: null, googleRating: null }) }
      })
      expect(wrapper.html()).toBe('')
    })

    it('renders formatted address when present', () => {
      const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
      expect(wrapper.text()).toContain('Crta Pujarnol, km 5, Girona')
    })

    it('renders phone as tel: link using national number', () => {
      const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
      const link = wrapper.find('a[href="tel:+34 972 59 05 07"]')
      expect(link.exists()).toBe(true)
      expect(link.text()).toContain('972 59 05 07')
    })

    it('renders website as external link', () => {
      const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
      const link = wrapper.find('a[href="http://www.example.com/"]')
      expect(link.exists()).toBe(true)
      expect(link.attributes('target')).toBe('_blank')
      expect(link.attributes('rel')).toContain('noopener')
    })

    it('renders Google rating with review count', () => {
      const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
      expect(wrapper.text()).toContain('4.2')
      expect(wrapper.text()).toContain('113')
    })

    it('renders OPERATIONAL status as "Operativo"', () => {
      const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
      expect(wrapper.text()).toContain('Operativo')
    })

    it('renders CLOSED_TEMPORARILY as "Cerrado temporalmente"', () => {
      const wrapper = mount(CampContactInfo, { props: { camp: makeCamp({ businessStatus: 'CLOSED_TEMPORARILY' }) } })
      expect(wrapper.text()).toContain('Cerrado temporalmente')
    })

    it('renders Google Maps link when address present', () => {
      const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
      expect(wrapper.find('a[href="https://maps.google.com/?cid=123"]').exists()).toBe(true)
    })

    it('renders last sync date when present', () => {
      const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
      expect(wrapper.text()).toContain('15')  // day from lastGoogleSyncAt
    })

    it('renders correctly with only phone number (partial data)', () => {
      const wrapper = mount(CampContactInfo, {
        props: { camp: makeCamp({ formattedAddress: null, websiteUrl: null, googleMapsUrl: null, googleRating: null }) }
      })
      expect(wrapper.exists()).toBe(true)
      expect(wrapper.text()).toContain('972 59 05 07')
    })
  })
  ```

- **File**: `frontend/src/components/camps/__tests__/CampPhotoGallery.test.ts`
- **Action**: Create unit tests for `CampPhotoGallery.vue`

  ```typescript
  import { describe, it, expect, vi } from 'vitest'
  import { mount } from '@vue/test-utils'
  import CampPhotoGallery from '../CampPhotoGallery.vue'
  import type { CampPhoto } from '@/types/camp'

  vi.stubEnv('VITE_API_URL', 'http://localhost:5000')

  const makePhoto = (overrides: Partial<CampPhoto> = {}): CampPhoto => ({
    id: '1', photoReference: 'ref_abc123', photoUrl: null,
    width: 1200, height: 900, attributionName: 'John Doe',
    attributionUrl: 'https://maps.google.com/maps/contrib/123',
    isPrimary: true, displayOrder: 1,
    ...overrides
  })

  describe('CampPhotoGallery', () => {
    it('renders nothing when photos array is empty', () => {
      const wrapper = mount(CampPhotoGallery, { props: { photos: [] } })
      expect(wrapper.html()).toBe('')
    })

    it('renders primary photo with correct proxy URL', () => {
      const wrapper = mount(CampPhotoGallery, { props: { photos: [makePhoto()] } })
      expect(wrapper.html()).toContain('reference=ref_abc123')
      expect(wrapper.html()).toContain('maxwidth=800')
    })

    it('renders thumbnail with smaller maxwidth', () => {
      const photos = [
        makePhoto({ isPrimary: true, id: '1' }),
        makePhoto({ isPrimary: false, id: '2', photoReference: 'ref_thumb', displayOrder: 2 })
      ]
      const wrapper = mount(CampPhotoGallery, { props: { photos } })
      expect(wrapper.html()).toContain('maxwidth=200')
    })

    it('uses photoUrl directly when available (Phase 2)', () => {
      const photo = makePhoto({ photoUrl: 'https://cdn.example.com/photo.jpg', photoReference: null })
      const wrapper = mount(CampPhotoGallery, { props: { photos: [photo] } })
      expect(wrapper.html()).toContain('https://cdn.example.com/photo.jpg')
    })

    it('renders attribution name with link', () => {
      const wrapper = mount(CampPhotoGallery, { props: { photos: [makePhoto()] } })
      expect(wrapper.text()).toContain('John Doe')
      expect(wrapper.find('a[href="https://maps.google.com/maps/contrib/123"]').exists()).toBe(true)
    })

    it('renders attribution name as plain text when no URL', () => {
      const photo = makePhoto({ attributionUrl: null })
      const wrapper = mount(CampPhotoGallery, { props: { photos: [photo] } })
      expect(wrapper.text()).toContain('John Doe')
    })

    it('limits thumbnails to 8 even with more photos', () => {
      const photos = Array.from({ length: 12 }, (_, i) =>
        makePhoto({ id: String(i), isPrimary: i === 0, displayOrder: i + 1, photoReference: `ref_${i}` })
      )
      const wrapper = mount(CampPhotoGallery, { props: { photos } })
      const thumbnailButtons = wrapper.findAll('button[type="button"]')
      expect(thumbnailButtons.length).toBeLessThanOrEqual(8)
    })

    it('shows photo count footer when multiple photos', () => {
      const photos = [makePhoto({ id: '1' }), makePhoto({ id: '2', isPrimary: false, displayOrder: 2 })]
      const wrapper = mount(CampPhotoGallery, { props: { photos } })
      expect(wrapper.text()).toContain('2 fotos')
    })

    it('renders "Google Maps" attribution footer', () => {
      const wrapper = mount(CampPhotoGallery, { props: { photos: [makePhoto()] } })
      expect(wrapper.text()).toContain('Google Maps')
    })
  })
  ```

- **Notes**:
  - Test files go in `frontend/src/components/camps/__tests__/` subdirectory
  - `vi.stubEnv('VITE_API_URL', ...)` controls the API base URL in tests
  - If PrimeVue is not globally registered in the test setup, either register it in mount options or stub the `Image` component. Check `frontend/src/test/setup.ts` or `vitest.config.ts` for existing global setup.

---

### Step 8: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/camp-extra-data.cy.ts`
- **Action**: E2E tests for the camp extended information feature
- **Implementation Steps**:

  1. Add `data-testid` attributes to existing components where needed (minimal additions):
     - In `CampLocationForm.vue`: add `data-testid="camp-name-input"` to the `AutoComplete` component
     - In `CampLocationsPage.vue` DataTable: add `data-testid="camp-table-row"` on Column body template root elements (or use `cy.contains` targeting the camp name)

  2. Create the E2E test file:

  ```typescript
  describe('Camp Extended Information', () => {
    beforeEach(() => {
      cy.loginAsBoard()   // Use existing Cypress custom command; add if missing
    })

    context('Camp detail page — with Google Places data', () => {
      it('displays contact information section for Google-enriched camp', () => {
        cy.visit('/camp-locations')
        // Navigate to detail of a camp known to have GooglePlaceId
        cy.get('[data-testid="view-camp-detail-btn"]').first().click()

        cy.contains('Información de contacto').should('be.visible')
      })

      it('renders phone as clickable tel: link', () => {
        cy.visit('/camp-locations')
        cy.get('[data-testid="view-camp-detail-btn"]').first().click()

        cy.get('a[href^="tel:"]').should('exist')
      })

      it('renders website as external link with _blank target', () => {
        cy.visit('/camp-locations')
        cy.get('[data-testid="view-camp-detail-btn"]').first().click()

        cy.get('a[target="_blank"]').should('exist')
      })

      it('renders Google rating with star icon', () => {
        cy.visit('/camp-locations')
        cy.get('[data-testid="view-camp-detail-btn"]').first().click()

        cy.get('.pi-star-fill').should('exist')
        cy.contains('valoraciones en Google').should('be.visible')
      })

      it('renders photo gallery when camp has photos', () => {
        cy.visit('/camp-locations')
        cy.get('[data-testid="view-camp-detail-btn"]').first().click()

        cy.contains('Fotos').should('be.visible')
        cy.get('img[src*="places/photo"]').should('have.length.greaterThan', 0)
      })

      it('shows Google Maps attribution in photo gallery', () => {
        cy.visit('/camp-locations')
        cy.get('[data-testid="view-camp-detail-btn"]').first().click()

        cy.contains('Google Maps').should('be.visible')
      })
    })

    context('Camp detail page — without Google Places data (graceful degradation)', () => {
      it('does not show contact info section for manually-created camps', () => {
        // This test requires a manually-created camp (no GooglePlaceId) to exist in the test DB
        // Seed via API in beforeEach or use a known manual camp
        cy.visit('/camp-locations')
        // Click "Create" and create a camp manually (no autocomplete)
        cy.contains('Nuevo Campamento').click()
        cy.get('[data-testid="camp-name-input"]').type('Manual Test Camp')
        cy.contains('Guardar').click()

        // Navigate to its detail page
        cy.contains('Manual Test Camp').click()

        cy.contains('Información de contacto').should('not.exist')
        cy.contains('Fotos').should('not.exist')
      })
    })

    context('Camp cards list — rating badge', () => {
      it('shows rating badge on cards for Google-enriched camps', () => {
        cy.visit('/camp-locations')
        // Switch to card view
        cy.get('button[aria-label="Vista de tarjetas"]').click()

        cy.get('.pi-star-fill').should('exist')
      })
    })
  })
  ```

  3. Add `data-testid="view-camp-detail-btn"` to the "Ver detalle" Button in `CampLocationsPage.vue` DataTable column:
  ```html
  <Button
    v-tooltip.top="'Ver detalle'"
    icon="pi pi-eye"
    text rounded size="small"
    aria-label="Ver detalle de ubicación"
    data-testid="view-camp-detail-btn"
    @click="handleViewDetails(data)"
  />
  ```

- **Notes**:
  - Check `frontend/cypress/support/commands.ts` for existing `cy.loginAsBoard()` — add it if missing, following the pattern of existing `cy.login()` commands
  - E2E tests require the backend running with a seeded camp that has a `GooglePlaceId` (e.g., Alba Colònies: `ChIJ38SpLTDCuhIRgdtW_484UBk`)

---

### Step 9: Update Technical Documentation

- **Action**: Review and update affected documentation after all code changes
- **Implementation Steps**:

  1. Check if `ai-specs/specs/api-spec.yml` exists — if so, update `GET /api/camps/{id}` response schema to reference `CampDetailResponse` (including `photos` array and full address breakdown fields)

  2. Run TypeScript check to confirm zero errors:
     ```bash
     cd frontend && npx tsc --noEmit
     ```

  3. If the frontend-standards.mdc documents component patterns, note the photo proxy URL pattern for future reference: `{VITE_API_URL}/places/photo?reference={ref}&maxwidth={w}`

- **References**: Follow `ai-specs/specs/documentation-standards.mdc` — all documentation in English

---

## Implementation Order

1. **Step 0**: Create branch `feature/feat-camps-extra-data-frontend`
2. **Step 1**: Update TypeScript types in `camp.ts`
3. **Step 2**: Update `useCamps` composable — `getCampById` and `createCamp` return `CampDetailResponse`
4. **Step 3**: Create `CampContactInfo.vue`
5. **Step 4**: Create `CampPhotoGallery.vue`
6. **Step 5**: Update `CampLocationDetailPage.vue` — integrate components, fix field mismatches
7. **Step 6**: Update `CampLocationCard.vue` — add rating badge
8. **Step 7**: Write Vitest unit tests for both new components
9. **Step 8**: Write Cypress E2E tests, add `data-testid` attributes
10. **Step 9**: Update technical documentation

---

## Testing Checklist

### Vitest Unit Tests

- [ ] `CampContactInfo.test.ts` — all 10 test cases pass
- [ ] `CampPhotoGallery.test.ts` — all 9 test cases pass
- [ ] No `any` types in test files
- [ ] `vi.stubEnv('VITE_API_URL', ...)` used to control photo URL construction
- [ ] Test coverage meets 90% threshold on new components

### Cypress E2E Tests

- [ ] Contact info section visible for Google-enriched camps
- [ ] Phone and website links rendered correctly
- [ ] Photo gallery visible with correct proxy URLs
- [ ] Contact info / photo sections absent for manually-created camps (graceful degradation)
- [ ] Rating badge visible in card view

### Manual Verification

- [ ] Phone link opens dialer on mobile
- [ ] Website/Google Maps links open in new tab
- [ ] Thumbnail click updates the main photo display
- [ ] PrimeVue Image preview/zoom works on primary photo click
- [ ] Google attribution always visible on photo gallery
- [ ] Rating badge appears on camp cards
- [ ] All sections absent for camps without GooglePlaceId

---

## Error Handling Patterns

- **Composable errors**: `useCamps` handles loading/error states; `CampLocationDetailPage.vue` shows PrimeVue `Message` on API failure — no changes needed
- **Missing Google data**: All extended fields are `null` for manual camps. Both new components use `v-if` computed guards — nothing renders
- **Empty photo URL**: If both `photoReference` and `photoUrl` are null, `getPhotoUrl` returns `''` — the `<Image>` will not render a broken image (guard with `v-if="getPhotoUrl(displayedPhoto)"` if needed)
- **Photo load failure**: Browser native fallback. Can add `@error` handler on `<img>` to hide broken images as a Phase 2 enhancement

---

## UI/UX Considerations

- **PrimeVue components used**: `Button` (Google Maps link), `Image` (photo with preview)
- **Tailwind CSS**: All layout via utilities — no `<style>` blocks
- **Responsive design**:
  - `CampContactInfo`: single column, works on all breakpoints
  - `CampPhotoGallery`: thumbnail grid adapts `grid-cols-4` → `sm:grid-cols-6` → `md:grid-cols-8`
  - `CampLocationDetailPage`: existing `grid-cols-1 lg:grid-cols-2` handles stacking
- **Accessibility**: `aria-label` on all interactive elements; external links clearly indicated with `pi-external-link` icon
- **Loading**: Handled by parent page, not individual components
- **Photo lazy loading**: `loading="lazy"` on all `<img>` thumbnail elements

---

## Dependencies

### npm Packages

No new packages required. All already in the project:
- `primevue` — `Image`, `Button` components
- `@vue/test-utils` — component testing
- `vitest` — test runner
- `cypress` — E2E testing

### Environment Variables

- `VITE_API_URL` — already configured; used to build photo proxy URL pattern

---

## Notes

### Business Rules

- **Attribution is mandatory**: Google Places API Terms of Service require showing photo author attribution. `attributionName` is non-nullable — never omit it from `CampPhotoGallery`.
- **All fields are optional**: Camps without `GooglePlaceId` have all extended fields as `null`. Both new components must render nothing rather than empty sections.
- **Phase 1 photos**: `photoUrl` is `null`. Photos served via backend proxy at `/api/places/photo`. Confirm the backend implements this proxy endpoint (should be in `feat-camps-extra-data-backend` or a related ticket).
- **Phase 5 (sync) excluded from MVP**: Do NOT implement `CampBatchSyncDialog.vue`, `CampSyncButton.vue`, or `useCampSync.ts` in this ticket.

### Language Requirements

- TypeScript code, interfaces, method names, test descriptions: **English**
- UI text displayed to users: **Spanish** (matching existing app language)

### TypeScript Strict Typing

- No `any` — use `unknown` for caught errors, proper interfaces for all data
- `defineProps<T>()` with TypeScript generics — no runtime `defineProps({})` style

### Breaking Changes

- `getCampById` return type changes from `Camp | null` to `CampDetailResponse | null` — only `CampLocationDetailPage.vue` calls this; update it in Step 5
- `Camp` interface gains new nullable fields — fully backward compatible

---

## Next Steps After Implementation

1. **Backend dependency**: This plan depends on `feature/feat-camps-extra-data-backend` being merged. Verify the backend photo proxy endpoint (`GET /api/places/photo?reference=...&maxwidth=...`) is implemented before testing photos.
2. **Phase 5 (Post-MVP)**: Implement sync features: `CampBatchSyncDialog.vue`, `CampSyncButton.vue`, `useCampSync.ts` — tracked as a separate ticket.

---

## Implementation Verification

### Code Quality

- [ ] All `.vue` files use `<script setup lang="ts">` — no Options API
- [ ] No `<style>` blocks anywhere — Tailwind utilities only
- [ ] No `any` types in TypeScript
- [ ] Props use `defineProps<T>()` with TypeScript generics
- [ ] All `<a target="_blank">` have `rel="noopener noreferrer"`
- [ ] All interactive elements have `aria-label` attributes

### Functionality

- [ ] `CampContactInfo` renders all fields correctly when populated
- [ ] `CampContactInfo` renders nothing when all contact fields are null
- [ ] `CampPhotoGallery` shows primary photo + thumbnails
- [ ] `CampPhotoGallery` renders nothing for empty `photos` array
- [ ] Thumbnail click updates the main photo display
- [ ] `CampLocationDetailPage` shows both sections for Google-enriched camps
- [ ] `CampLocationCard` shows rating badge when `googleRating !== null`

### Testing

- [ ] `npx vitest run` — all unit tests pass
- [ ] `npx cypress run --spec cypress/e2e/camp-extra-data.cy.ts` — all E2E tests pass
- [ ] 90%+ test coverage on new files

### Integration

- [ ] `npx tsc --noEmit` — zero TypeScript errors
- [ ] `npm run build` — zero build errors
- [ ] No console errors in browser after navigation to camp detail page
- [ ] Photo proxy URLs correctly constructed with `VITE_API_URL` prefix

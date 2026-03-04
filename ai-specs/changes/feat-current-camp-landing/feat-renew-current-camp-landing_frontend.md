# Frontend Implementation Plan: feat-current-camp-landing — Renew Camp Landing Page

## Overview

Redesign `CampPage.vue` (`/camp`) from a minimal card-based view into a rich, multi-section landing page. The page switches its data source from `GET /api/camps/editions/active` to `GET /api/camps/current` (which carries the enriched response with photos, extras, accommodation, and contact details after the backend ticket is completed). A new `CampExtrasSection.vue` component is created; all other UI sections reuse existing components (`CampPlacesGallery`, `CampLocationMap`, `AccommodationCapacityDisplay`, `PricingBreakdown`, `CampEditionStatusBadge`). The frontend type system is updated to include a proper `CurrentCampEditionResponse` interface matching the enriched backend DTO.

Architecture: Vue 3 Composition API (`<script setup lang="ts">`), composable-based data fetching, PrimeVue + Tailwind CSS, no Pinia store (page-level state is sufficient).

---

## Architecture Context

### Components involved

| Component | Action |
|---|---|
| `frontend/src/views/CampPage.vue` | **Rewrite** — switch composable, full layout redesign |
| `frontend/src/components/camps/CampExtrasSection.vue` | **Create new** |
| `frontend/src/components/camps/CampPlacesGallery.vue` | **Reuse as-is** |
| `frontend/src/components/camps/CampLocationMap.vue` | **Reuse as-is** |
| `frontend/src/components/camps/AccommodationCapacityDisplay.vue` | **Reuse as-is** |
| `frontend/src/components/camps/PricingBreakdown.vue` | **Reuse as-is** |
| `frontend/src/components/camps/CampEditionStatusBadge.vue` | **Reuse as-is** |

### Types involved

| File | Action |
|---|---|
| `frontend/src/types/camp-edition.ts` | **Add** `CurrentCampEditionResponse` interface |

### Composable involved

| File | Action |
|---|---|
| `frontend/src/composables/useCampEditions.ts` | **Update** `currentCampEdition` ref type to `CurrentCampEditionResponse` |

### Tests involved

| File | Action |
|---|---|
| `frontend/src/views/__tests__/CampPage.spec.ts` | **Create new** Vitest unit tests |
| `frontend/cypress/e2e/camp-edition.cy.ts` | **Verify/extend** — tests already exist and intercept `/api/camps/current`; update fixture data |
| `frontend/cypress/fixtures/camp-edition-open.json` | **Update** — fixture shape must match `CurrentCampEditionResponse` |
| `frontend/cypress/fixtures/camp-edition-2025.json` | **Update** — fixture shape must match `CurrentCampEditionResponse` |

### Routing

No changes. Route `/camp → CampPage.vue` with name `camp` remains unchanged.

### State management

No Pinia store. All state (`currentCampEdition`, `loading`, `error`) comes from the `useCampEditions` composable. Family unit state comes from the existing `useFamilyUnits` composable.

---

## Pre-Implementation Research Findings

1. **`fetchCurrentCampEdition`** already exists in `useCampEditions.ts` and calls `GET /camps/current`. It currently types the result as `CampEdition | null`. **Must be updated** to `CurrentCampEditionResponse | null`.

2. **`CampPage.vue`** currently uses `getActiveEdition()` which calls `/camps/editions/active`. This must be replaced with `fetchCurrentCampEdition()`.

3. **Cypress tests in `camp-edition.cy.ts`** already intercept `/api/camps/current` — they were written ahead of the frontend implementation (TDD). The tests define expected behavior: loading state, year title in `<h1>`, previous-year warning message, 404 empty state, registration CTA visibility. These tests must pass after implementation.

4. **Cypress fixture shape is wrong**: `camp-edition-open.json` and `camp-edition-2025.json` use an old nested shape (`camp.latitude`, `camp.name`). They must be replaced with the flat `CurrentCampEditionResponse` shape (`campLatitude`, `campName`, `campPhotos`, etc.).

5. **`PricingBreakdown`** requires `AgeRangeSettings`. Derive from `currentCampEdition.useCustomAgeRanges`:
   - If `useCustomAgeRanges === true`: use `customBabyMaxAge`, `customChildMinAge`, `customChildMaxAge`, `customAdultMinAge`
   - Defaults: `{ babyMaxAge: 3, childMinAge: 4, childMaxAge: 14, adultMinAge: 15 }`

6. **`CampLocationMap`** takes `CampLocation[]` with `{ latitude: number, longitude: number, name: string }`. Build from `campLatitude`, `campLongitude`, `campName`.

7. **`CampPlacesGallery`** takes `CampPlacesPhoto[]` — exactly matches `CampPhotos` from `CurrentCampEditionResponse`.

8. **Previous-year detection**: compare `edition.year < new Date().getFullYear()` to show the warning banner.

9. **No `CampPage.spec.ts` exists** yet — must be created.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch name**: `feature/feat-current-camp-landing-frontend`
- **Implementation Steps**:
  1. `git checkout main`
  2. `git pull origin main`
  3. `git checkout -b feature/feat-current-camp-landing-frontend`
  4. `git branch` — verify branch
- **Notes**: Must be the first step. Do not merge with backend branch if working concurrently — the frontend can be developed against mocked data.

---

### Step 1: Add `CurrentCampEditionResponse` TypeScript Interface

- **File**: `frontend/src/types/camp-edition.ts`
- **Action**: Add a new exported interface at the end of the file. Do NOT modify `CampEdition` — the new type is separate.

**Interface to add:**

```typescript
import type { CampPlacesPhoto, AccommodationCapacity } from './camp'

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
  customBabyMaxAge?: number | null
  customChildMinAge?: number | null
  customChildMaxAge?: number | null
  customAdultMinAge?: number | null
  status: CampEditionStatus
  maxCapacity?: number | null
  registrationCount: number
  availableSpots?: number | null
  notes?: string | null
  createdAt: string
  updatedAt: string
  // Enriched fields (added by backend ticket)
  campDescription: string | null
  campPhoneNumber: string | null
  campNationalPhoneNumber: string | null
  campWebsiteUrl: string | null
  campGoogleMapsUrl: string | null
  campGoogleRating: number | null
  campGoogleRatingCount: number | null
  campPhotos: CampPlacesPhoto[]
  accommodationCapacity: AccommodationCapacity | null
  calculatedTotalBedCapacity: number | null
  extras: CampEditionExtra[]
}
```

- **Implementation Notes**:
  - `CampPlacesPhoto` is already defined in `frontend/src/types/camp.ts` — add to the import if not already present.
  - `AccommodationCapacity` is also already in `frontend/src/types/camp.ts`.
  - `CampEditionExtra` is already in `frontend/src/types/camp-edition.ts`.
  - All nullable fields that are optional in the backend use `| null` (not `?`) to force explicit null-checking in the template.

---

### Step 2: Update `useCampEditions` Composable

- **File**: `frontend/src/composables/useCampEditions.ts`
- **Action**: Change the type of `currentCampEdition` ref from `CampEdition | null` to `CurrentCampEditionResponse | null`, and update the `fetchCurrentCampEdition` function's API response type.

**Changes:**

1. Add `CurrentCampEditionResponse` to imports:
   ```typescript
   import type {
     CampEdition,
     // ... existing imports ...
     CurrentCampEditionResponse   // ADD THIS
   } from '@/types/camp-edition'
   ```

2. Change the ref declaration:
   ```typescript
   // BEFORE:
   const currentCampEdition = ref<CampEdition | null>(null)

   // AFTER:
   const currentCampEdition = ref<CurrentCampEditionResponse | null>(null)
   ```

3. Update `fetchCurrentCampEdition` generic type:
   ```typescript
   // BEFORE:
   const response = await api.get<ApiResponse<CampEdition>>('/camps/current')

   // AFTER:
   const response = await api.get<ApiResponse<CurrentCampEditionResponse>>('/camps/current')
   ```

- **Implementation Notes**:
  - The function body logic (error handling, 404 handling, `currentCampEdition.value` assignment) stays exactly the same — only the type changes.
  - Do NOT change any other function in the composable.

---

### Step 3: Create `CampExtrasSection.vue` Component

- **File**: `frontend/src/components/camps/CampExtrasSection.vue`
- **Action**: Create a new reusable component that displays the list of active camp extras.

**Complete component:**

```vue
<script setup lang="ts">
import { computed } from 'vue'
import Badge from 'primevue/badge'
import type { CampEditionExtra } from '@/types/camp-edition'

defineProps<{
  extras: CampEditionExtra[]
}>()

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const pricingLabel = (extra: CampEditionExtra): string => {
  const type = extra.pricingType === 'PerPerson' ? 'por persona' : 'por familia'
  const period = extra.pricingPeriod === 'PerDay' ? '/ día' : '(pago único)'
  return `${type} ${period}`
}
</script>

<template>
  <section class="rounded-lg border border-gray-200 bg-white p-6">
    <h2 class="mb-4 text-lg font-semibold text-gray-900">Servicios adicionales</h2>
    <ul class="space-y-4">
      <li
        v-for="extra in extras"
        :key="extra.id"
        class="flex items-start justify-between gap-4 rounded-md border border-gray-100 bg-gray-50 p-4"
        :data-testid="`extra-item-${extra.id}`"
      >
        <div class="flex-1 min-w-0">
          <div class="flex flex-wrap items-center gap-2">
            <span class="font-medium text-gray-900">{{ extra.name }}</span>
            <Badge
              v-if="extra.isRequired"
              value="Obligatorio"
              severity="danger"
              class="text-xs"
            />
          </div>
          <p v-if="extra.description" class="mt-1 text-sm text-gray-600">
            {{ extra.description }}
          </p>
        </div>
        <div class="shrink-0 text-right">
          <p class="font-semibold text-gray-900">{{ formatCurrency(extra.price) }}</p>
          <p class="text-xs text-gray-500">{{ pricingLabel(extra) }}</p>
        </div>
      </li>
    </ul>
  </section>
</template>
```

- **Implementation Notes**:
  - No `<style>` block — Tailwind only.
  - `Badge` from PrimeVue is used for the "Obligatorio" label. Import from `primevue/badge`.
  - The `data-testid` attribute on each `<li>` allows specific extra targeting in tests.
  - `extra.pricingType` and `extra.pricingPeriod` are string literals (`'PerPerson'`, `'PerFamily'`, `'OneTime'`, `'PerDay'`) matching the existing `CampEditionExtra` type.

---

### Step 4: Rewrite `CampPage.vue`

- **File**: `frontend/src/views/CampPage.vue`
- **Action**: Full rewrite. Switch from `getActiveEdition` / `ActiveEditionCard` to `fetchCurrentCampEdition` with a multi-section layout.

**Complete component:**

```vue
<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import Container from '@/components/ui/Container.vue'
import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
import CampPlacesGallery from '@/components/camps/CampPlacesGallery.vue'
import CampLocationMap from '@/components/camps/CampLocationMap.vue'
import AccommodationCapacityDisplay from '@/components/camps/AccommodationCapacityDisplay.vue'
import PricingBreakdown from '@/components/camps/PricingBreakdown.vue'
import CampExtrasSection from '@/components/camps/CampExtrasSection.vue'
import { useCampEditions } from '@/composables/useCampEditions'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import { useAuthStore } from '@/stores/auth'
import type { AgeRangeSettings } from '@/types/association-settings'
import type { CampLocation } from '@/types/camp'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Button from 'primevue/button'
import ProgressBar from 'primevue/progressbar'

const router = useRouter()
const auth = useAuthStore()
const { currentCampEdition, loading, error, fetchCurrentCampEdition } = useCampEditions()
const { familyUnit, getCurrentUserFamilyUnit } = useFamilyUnits()

// ── Derived state ──────────────────────────────────────────────────────────────

const isRepresentative = computed(
  () => !!familyUnit.value && familyUnit.value.representativeUserId === auth.user?.id
)

const isPreviousYear = computed(() => {
  if (!currentCampEdition.value) return false
  return currentCampEdition.value.year < new Date().getFullYear()
})

const ageRanges = computed<AgeRangeSettings>(() => {
  const e = currentCampEdition.value
  if (!e || !e.useCustomAgeRanges) {
    return { babyMaxAge: 3, childMinAge: 4, childMaxAge: 14, adultMinAge: 15 }
  }
  return {
    babyMaxAge: e.customBabyMaxAge ?? 3,
    childMinAge: e.customChildMinAge ?? 4,
    childMaxAge: e.customChildMaxAge ?? 14,
    adultMinAge: e.customAdultMinAge ?? 15
  }
})

const mapLocations = computed<CampLocation[]>(() => {
  const e = currentCampEdition.value
  if (!e || e.campLatitude == null || e.campLongitude == null) return []
  return [{ latitude: e.campLatitude, longitude: e.campLongitude, name: e.campName }]
})

const capacityPercent = computed((): number => {
  const e = currentCampEdition.value
  if (!e || !e.maxCapacity) return 0
  return Math.round((e.registrationCount / e.maxCapacity) * 100)
})

const hasContactInfo = computed(() => {
  const e = currentCampEdition.value
  if (!e) return false
  return !!(e.campFormattedAddress || e.campPhoneNumber || e.campWebsiteUrl || e.campGoogleMapsUrl || e.campGoogleRating)
})

const formattedRating = computed((): string | null => {
  const r = currentCampEdition.value?.campGoogleRating
  return r != null ? r.toFixed(1) : null
})

// ── Date helpers ───────────────────────────────────────────────────────────────

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: 'long', year: 'numeric' }).format(
    new Date(dateStr)
  )

const durationDays = computed((): number | null => {
  const e = currentCampEdition.value
  if (!e) return null
  const diff = new Date(e.endDate).getTime() - new Date(e.startDate).getTime()
  return Math.round(diff / (1000 * 60 * 60 * 24)) + 1
})

// ── Actions ───────────────────────────────────────────────────────────────────

const goToRegister = () => {
  if (currentCampEdition.value) {
    router.push({
      name: 'registration-new',
      params: { editionId: currentCampEdition.value.id }
    })
  }
}

// ── Lifecycle ─────────────────────────────────────────────────────────────────

onMounted(() => {
  fetchCurrentCampEdition()
  getCurrentUserFamilyUnit()
})
</script>

<template>
  <div class="bg-gray-50 min-h-screen">

    <!-- Loading -->
    <div v-if="loading" class="flex justify-center py-24" data-testid="camp-loading" role="status">
      <ProgressSpinner />
    </div>

    <!-- Error -->
    <Container v-else-if="error">
      <div class="py-8">
        <Message severity="error" :closable="false">{{ error }}</Message>
      </div>
    </Container>

    <!-- No edition found -->
    <Container v-else-if="!currentCampEdition">
      <div
        class="py-16 text-center rounded-lg border border-gray-200 bg-white mt-8"
        data-testid="camp-empty"
      >
        <i class="pi pi-calendar-times mb-4 text-5xl text-gray-300" />
        <p class="text-lg font-medium text-gray-600">No hay información de campamento disponible</p>
        <p class="mt-2 text-sm text-gray-400">
          Cuando se publique un campamento, aparecerá aquí.
        </p>
      </div>
    </Container>

    <!-- Edition found -->
    <div v-else>

      <!-- ── HERO ─────────────────────────────────────────────────────────────── -->
      <div class="bg-white border-b border-gray-200">
        <Container>
          <div class="py-8">

            <!-- Previous-year banner -->
            <Message
              v-if="isPreviousYear"
              severity="warn"
              :closable="false"
              class="mb-4"
            >
              Mostrando información del campamento de {{ currentCampEdition.year }}
            </Message>

            <!-- Title row -->
            <div class="flex flex-wrap items-start justify-between gap-4">
              <div>
                <h1 class="text-3xl font-bold text-gray-900">
                  Campamento {{ currentCampEdition.year }}
                  <span v-if="currentCampEdition.campName !== `Campamento ${currentCampEdition.year}`">
                    — {{ currentCampEdition.campName }}
                  </span>
                </h1>
                <p v-if="currentCampEdition.campLocation || currentCampEdition.campFormattedAddress"
                   class="mt-1 text-gray-500">
                  <i class="pi pi-map-marker mr-1" />
                  {{ currentCampEdition.campFormattedAddress ?? currentCampEdition.campLocation }}
                </p>
              </div>
              <CampEditionStatusBadge :status="currentCampEdition.status" />
            </div>

            <!-- Date + duration -->
            <div class="mt-4 flex flex-wrap items-center gap-4 text-sm text-gray-600">
              <span class="flex items-center gap-1">
                <i class="pi pi-calendar text-green-600" />
                {{ formatDate(currentCampEdition.startDate) }}
                &ndash;
                {{ formatDate(currentCampEdition.endDate) }}
              </span>
              <span v-if="durationDays" class="text-gray-400">
                {{ durationDays }} días
              </span>
            </div>

            <!-- Notes notice -->
            <Message
              v-if="currentCampEdition.notes"
              severity="info"
              :closable="false"
              class="mt-4"
            >
              {{ currentCampEdition.notes }}
            </Message>

            <!-- CTA -->
            <div class="mt-6 flex flex-col gap-3 sm:flex-row sm:items-center">
              <!-- Open + representative -->
              <template v-if="currentCampEdition.status === 'Open'">
                <Button
                  v-if="isRepresentative"
                  label="Inscripciones Abiertas"
                  icon="pi pi-user-plus"
                  size="large"
                  data-testid="register-button"
                  @click="goToRegister"
                />
                <Button
                  v-else
                  label="Solo el representante puede inscribirse"
                  icon="pi pi-info-circle"
                  severity="secondary"
                  size="large"
                  disabled
                />
              </template>

              <!-- Closed -->
              <div v-else-if="currentCampEdition.status === 'Closed'"
                   class="rounded-md bg-orange-50 px-4 py-3 text-sm text-orange-700">
                <i class="pi pi-lock mr-2" />
                Inscripciones cerradas
              </div>

              <!-- Completed or previous year -->
              <div
                v-else-if="currentCampEdition.status === 'Completed' || isPreviousYear"
                class="rounded-md bg-blue-50 px-4 py-3 text-sm text-blue-700"
              >
                <i class="pi pi-check-circle mr-2" />
                Este campamento ha finalizado
              </div>

              <!-- Link to registrations (always visible) -->
              <RouterLink
                :to="{ name: 'registrations' }"
                class="text-sm text-blue-600 underline hover:text-blue-800"
              >
                Ver mis inscripciones
              </RouterLink>
            </div>

            <!-- Non-representative notice -->
            <p
              v-if="currentCampEdition.status === 'Open' && !isRepresentative && familyUnit"
              class="mt-2 text-xs text-amber-600"
            >
              Solo el representante de la unidad familiar puede inscribir a la familia.
            </p>
          </div>
        </Container>
      </div>

      <!-- ── CAPACITY BAR ──────────────────────────────────────────────────────── -->
      <Container v-if="currentCampEdition.maxCapacity">
        <div class="mt-6 rounded-lg border border-gray-200 bg-white p-6">
          <div class="mb-2 flex items-center justify-between text-sm text-gray-600">
            <span>
              <i class="pi pi-users mr-1" />
              {{ currentCampEdition.registrationCount }} inscritos
            </span>
            <span>{{ currentCampEdition.maxCapacity }} plazas totales</span>
          </div>
          <ProgressBar :value="capacityPercent" class="h-3" />
          <p v-if="currentCampEdition.availableSpots != null" class="mt-1 text-xs text-gray-400 text-right">
            {{ currentCampEdition.availableSpots }} plazas disponibles
          </p>
        </div>
      </Container>

      <!-- ── PHOTO GALLERY ─────────────────────────────────────────────────────── -->
      <Container v-if="currentCampEdition.campPhotos.length > 0">
        <div class="mt-6">
          <CampPlacesGallery :photos="currentCampEdition.campPhotos" />
        </div>
      </Container>

      <!-- ── ABOUT + MAP ───────────────────────────────────────────────────────── -->
      <Container>
        <div class="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">

          <!-- Description -->
          <div
            v-if="currentCampEdition.campDescription"
            class="rounded-lg border border-gray-200 bg-white p-6"
          >
            <h2 class="mb-3 text-lg font-semibold text-gray-900">Sobre el campamento</h2>
            <p class="text-sm text-gray-700 leading-relaxed">
              {{ currentCampEdition.campDescription }}
            </p>
          </div>

          <!-- Map -->
          <div v-if="mapLocations.length > 0" class="rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="mb-3 text-lg font-semibold text-gray-900">Ubicación</h2>
            <CampLocationMap :locations="mapLocations" />
          </div>
        </div>
      </Container>

      <!-- ── ACCOMMODATION ─────────────────────────────────────────────────────── -->
      <Container v-if="currentCampEdition.accommodationCapacity">
        <div class="mt-6">
          <AccommodationCapacityDisplay
            :capacity="currentCampEdition.accommodationCapacity"
            :total-bed-capacity="currentCampEdition.calculatedTotalBedCapacity"
          />
        </div>
      </Container>

      <!-- ── PRICING ───────────────────────────────────────────────────────────── -->
      <Container>
        <div class="mt-6 rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-4 text-lg font-semibold text-gray-900">Precios</h2>
          <PricingBreakdown
            :price-per-adult="currentCampEdition.pricePerAdult"
            :price-per-child="currentCampEdition.pricePerChild"
            :price-per-baby="currentCampEdition.pricePerBaby"
            :age-ranges="ageRanges"
          />
        </div>
      </Container>

      <!-- ── EXTRAS ────────────────────────────────────────────────────────────── -->
      <Container v-if="currentCampEdition.extras.length > 0">
        <div class="mt-6">
          <CampExtrasSection :extras="currentCampEdition.extras" />
        </div>
      </Container>

      <!-- ── CONTACT INFO ──────────────────────────────────────────────────────── -->
      <Container v-if="hasContactInfo">
        <div class="mt-6 rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-4 text-lg font-semibold text-gray-900">Información de contacto</h2>
          <div class="space-y-3 text-sm">

            <!-- Address -->
            <div v-if="currentCampEdition.campFormattedAddress" class="flex items-start gap-3">
              <i class="pi pi-map-marker mt-0.5 text-gray-400" />
              <div>
                <p class="text-gray-700">{{ currentCampEdition.campFormattedAddress }}</p>
                <a
                  v-if="currentCampEdition.campGoogleMapsUrl"
                  :href="currentCampEdition.campGoogleMapsUrl"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="text-blue-600 hover:underline"
                  aria-label="Abrir en Google Maps"
                >
                  Ver en Google Maps <i class="pi pi-external-link ml-1 text-xs" />
                </a>
              </div>
            </div>

            <!-- Phone -->
            <div v-if="currentCampEdition.campPhoneNumber" class="flex items-center gap-3">
              <i class="pi pi-phone text-gray-400" />
              <a
                :href="`tel:${currentCampEdition.campPhoneNumber}`"
                class="text-gray-700 hover:text-blue-600"
              >
                {{ currentCampEdition.campNationalPhoneNumber ?? currentCampEdition.campPhoneNumber }}
              </a>
            </div>

            <!-- Website -->
            <div v-if="currentCampEdition.campWebsiteUrl" class="flex items-center gap-3">
              <i class="pi pi-globe text-gray-400" />
              <a
                :href="currentCampEdition.campWebsiteUrl"
                target="_blank"
                rel="noopener noreferrer"
                class="text-blue-600 hover:underline truncate"
              >
                {{ currentCampEdition.campWebsiteUrl.replace(/^https?:\/\//, '').replace(/\/$/, '') }}
              </a>
            </div>

            <!-- Rating -->
            <div v-if="formattedRating" class="flex items-center gap-3">
              <i class="pi pi-star-fill text-yellow-400" />
              <span class="font-semibold">{{ formattedRating }}</span>
              <span v-if="currentCampEdition.campGoogleRatingCount" class="text-gray-500">
                ({{ currentCampEdition.campGoogleRatingCount }} valoraciones en Google)
              </span>
            </div>
          </div>
        </div>
      </Container>

      <!-- ── BOTTOM CTA ─────────────────────────────────────────────────────────── -->
      <Container>
        <div class="mt-6 mb-10 flex flex-col items-center gap-3 rounded-lg border border-gray-200 bg-white p-6 text-center sm:flex-row sm:justify-center">
          <template v-if="currentCampEdition.status === 'Open' && isRepresentative">
            <Button
              label="Inscribirse al campamento"
              icon="pi pi-user-plus"
              size="large"
              @click="goToRegister"
            />
          </template>
          <RouterLink
            :to="{ name: 'registrations' }"
            class="text-sm text-blue-600 underline hover:text-blue-800"
          >
            Ver mis inscripciones
          </RouterLink>
        </div>
      </Container>

    </div>
  </div>
</template>
```

- **Implementation Notes**:
  - `ProgressBar` from PrimeVue — import `from 'primevue/progressbar'`. Already a PrimeVue dependency.
  - The hero `<h1>` always shows "Campamento {year}" — this matches the existing Cypress test expectation: `cy.get('h1').should('contain.text', 'Campamento 2026')`.
  - The "Inscripciones Abiertas" label on the CTA button matches the Cypress test: `cy.contains('Inscripciones Abiertas').should('be.visible')`.
  - "Mostrando información del campamento de {year}" text matches the Cypress test expectation.
  - Empty state text "No hay información de campamento disponible" matches the Cypress test: `cy.contains('No hay información de campamento disponible').should('be.visible')`.
  - `data-testid="camp-loading"` matches the existing Cypress test.
  - `data-testid="camp-empty"` matches the existing Cypress test.
  - `data-testid="register-button"` maintained for test targeting.
  - No `<style>` block — Tailwind only.

---

### Step 5: Update Cypress Fixtures

- **Files**:
  - `frontend/cypress/fixtures/camp-edition-open.json`
  - `frontend/cypress/fixtures/camp-edition-2025.json`
- **Action**: Replace both fixtures with the enriched `CurrentCampEditionResponse` shape.

**`camp-edition-open.json`** — Replace entirely:
```json
{
  "success": true,
  "data": {
    "id": "edition-2026",
    "campId": "camp-1",
    "campName": "Camping El Pinar",
    "campLocation": "Sierra de Guadarrama",
    "campFormattedAddress": "Calle del Pinar, 1, 28740 Rascafría, Madrid",
    "campLatitude": 40.8842,
    "campLongitude": -3.8668,
    "year": 2026,
    "startDate": "2026-07-01T00:00:00Z",
    "endDate": "2026-07-15T00:00:00Z",
    "pricePerAdult": 450,
    "pricePerChild": 300,
    "pricePerBaby": 0,
    "useCustomAgeRanges": false,
    "customBabyMaxAge": null,
    "customChildMinAge": null,
    "customChildMaxAge": null,
    "customAdultMinAge": null,
    "status": "Open",
    "maxCapacity": 120,
    "registrationCount": 45,
    "availableSpots": 75,
    "notes": null,
    "createdAt": "2026-01-01T00:00:00Z",
    "updatedAt": "2026-01-01T00:00:00Z",
    "campDescription": "Un hermoso campamento en el corazón de la Sierra de Guadarrama.",
    "campPhoneNumber": "+34918691311",
    "campNationalPhoneNumber": "918 691 311",
    "campWebsiteUrl": "https://camping-elpinar.es",
    "campGoogleMapsUrl": "https://maps.google.com/?cid=123",
    "campGoogleRating": 4.3,
    "campGoogleRatingCount": 156,
    "campPhotos": [],
    "accommodationCapacity": null,
    "calculatedTotalBedCapacity": null,
    "extras": []
  },
  "error": null
}
```

**`camp-edition-2025.json`** — Replace entirely:
```json
{
  "success": true,
  "data": {
    "id": "edition-2025",
    "campId": "camp-1",
    "campName": "Camping Valle del Sur",
    "campLocation": "Andalucía",
    "campFormattedAddress": "Ctra. del Sur, km 12, 41400 Écija, Sevilla",
    "campLatitude": 37.3891,
    "campLongitude": -5.9845,
    "year": 2025,
    "startDate": "2025-07-01T00:00:00Z",
    "endDate": "2025-07-15T00:00:00Z",
    "pricePerAdult": 420,
    "pricePerChild": 280,
    "pricePerBaby": 0,
    "useCustomAgeRanges": false,
    "customBabyMaxAge": null,
    "customChildMinAge": null,
    "customChildMaxAge": null,
    "customAdultMinAge": null,
    "status": "Completed",
    "maxCapacity": 100,
    "registrationCount": 98,
    "availableSpots": 2,
    "notes": null,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-07-15T00:00:00Z",
    "campDescription": null,
    "campPhoneNumber": null,
    "campNationalPhoneNumber": null,
    "campWebsiteUrl": null,
    "campGoogleMapsUrl": null,
    "campGoogleRating": null,
    "campGoogleRatingCount": null,
    "campPhotos": [],
    "accommodationCapacity": null,
    "calculatedTotalBedCapacity": null,
    "extras": []
  },
  "error": null
}
```

- **Implementation Notes**:
  - The `year: 2025` fixture is for the "previous year" test — `isPreviousYear` computed checks `year < currentYear`.
  - Both fixtures have `campPhotos: []` and `extras: []` to keep tests simple and avoid testing photo/extra rendering in E2E.
  - Do NOT modify `camp-edition.cy.ts` test expectations — they already match the implementation.

---

### Step 6: Write Vitest Unit Tests for `CampPage.vue`

- **File**: `frontend/src/views/__tests__/CampPage.spec.ts`
- **Action**: Create new test file following the existing pattern from `FamilyUnitPage.spec.ts` and `ProfilePage.spec.ts`.

**Test setup pattern:**

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { nextTick } from 'vue'
import CampPage from '../CampPage.vue'
import type { CurrentCampEditionResponse } from '@/types/camp-edition'

// ── Auth mock ─────────────────────────────────────────────────────────────────
const authMock = vi.hoisted(() => ({
  user: { id: 'u1', role: 'Member' },
  isBoard: false,
  isAdmin: false,
}))

vi.mock('@/stores/auth', () => ({ useAuthStore: () => authMock }))

// ── Router mock ───────────────────────────────────────────────────────────────
const routerPushMock = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: routerPushMock }),
  RouterLink: { template: '<a><slot /></a>' },
}))

// ── Composable mocks ─────────────────────────────────────────────────────────
const mockFetchCurrentCampEdition = vi.fn()
const mockGetCurrentUserFamilyUnit = vi.fn()

const campEditionsMock = vi.hoisted(() => ({
  currentCampEdition: { value: null as CurrentCampEditionResponse | null },
  loading: { value: false },
  error: { value: null as string | null },
  fetchCurrentCampEdition: mockFetchCurrentCampEdition,
}))

const familyUnitsMock = vi.hoisted(() => ({
  familyUnit: { value: null as { representativeUserId: string } | null },
  getCurrentUserFamilyUnit: mockGetCurrentUserFamilyUnit,
}))

vi.mock('@/composables/useCampEditions', () => ({
  useCampEditions: () => campEditionsMock,
}))

vi.mock('@/composables/useFamilyUnits', () => ({
  useFamilyUnits: () => familyUnitsMock,
}))

// ── PrimeVue stubs ────────────────────────────────────────────────────────────
const globalStubs = {
  ProgressSpinner: true,
  Message: { template: '<div><slot /></div>', props: ['severity', 'closable'] },
  Button: { template: '<button @click="$emit(\'click\')"><slot /></button>', props: ['label', 'disabled', 'size', 'severity', 'icon'], emits: ['click'] },
  ProgressBar: true,
  CampEditionStatusBadge: true,
  CampPlacesGallery: true,
  CampLocationMap: true,
  AccommodationCapacityDisplay: true,
  PricingBreakdown: true,
  CampExtrasSection: true,
  Container: { template: '<div><slot /></div>' },
}

// ── Factory helper ────────────────────────────────────────────────────────────
const makeEdition = (overrides: Partial<CurrentCampEditionResponse> = {}): CurrentCampEditionResponse => ({
  id: 'edition-1',
  campId: 'camp-1',
  campName: 'Test Camp',
  campLocation: 'Sierra Norte',
  campFormattedAddress: 'Calle Test, Madrid',
  campLatitude: 40.4,
  campLongitude: -3.7,
  year: 2026,
  startDate: '2026-07-01T00:00:00Z',
  endDate: '2026-07-15T00:00:00Z',
  pricePerAdult: 450,
  pricePerChild: 300,
  pricePerBaby: 0,
  useCustomAgeRanges: false,
  customBabyMaxAge: null,
  customChildMinAge: null,
  customChildMaxAge: null,
  customAdultMinAge: null,
  status: 'Open',
  maxCapacity: 100,
  registrationCount: 30,
  availableSpots: 70,
  notes: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  campDescription: null,
  campPhoneNumber: null,
  campNationalPhoneNumber: null,
  campWebsiteUrl: null,
  campGoogleMapsUrl: null,
  campGoogleRating: null,
  campGoogleRatingCount: null,
  campPhotos: [],
  accommodationCapacity: null,
  calculatedTotalBedCapacity: null,
  extras: [],
  ...overrides,
})

const mountPage = () =>
  mount(CampPage, {
    global: {
      plugins: [createPinia()],
      stubs: globalStubs,
    },
  })

// ── Tests ─────────────────────────────────────────────────────────────────────
describe('CampPage.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    campEditionsMock.currentCampEdition.value = null
    campEditionsMock.loading.value = false
    campEditionsMock.error.value = null
    familyUnitsMock.familyUnit.value = null
  })

  it('calls fetchCurrentCampEdition and getCurrentUserFamilyUnit on mount', () => {
    mountPage()
    expect(mockFetchCurrentCampEdition).toHaveBeenCalledOnce()
    expect(mockGetCurrentUserFamilyUnit).toHaveBeenCalledOnce()
  })

  it('shows loading spinner when loading', () => {
    campEditionsMock.loading.value = true
    const wrapper = mountPage()
    expect(wrapper.find('[data-testid="camp-loading"]').exists()).toBe(true)
  })

  it('shows empty state when no edition exists', () => {
    campEditionsMock.currentCampEdition.value = null
    const wrapper = mountPage()
    expect(wrapper.find('[data-testid="camp-empty"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('No hay información de campamento disponible')
  })

  it('shows camp name and year in h1 when edition exists', async () => {
    campEditionsMock.currentCampEdition.value = makeEdition()
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.find('h1').text()).toContain('Campamento 2026')
  })

  it('shows previous-year warning message for old editions', async () => {
    campEditionsMock.currentCampEdition.value = makeEdition({ year: 2025 })
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.text()).toContain('Mostrando información del campamento de 2025')
  })

  it('shows register button for representative on Open edition', async () => {
    campEditionsMock.currentCampEdition.value = makeEdition({ status: 'Open' })
    familyUnitsMock.familyUnit.value = { representativeUserId: 'u1' }
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.find('[data-testid="register-button"]').exists()).toBe(true)
  })

  it('does NOT show register button for non-representative on Open edition', async () => {
    campEditionsMock.currentCampEdition.value = makeEdition({ status: 'Open' })
    familyUnitsMock.familyUnit.value = { representativeUserId: 'other-user' }
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.find('[data-testid="register-button"]').exists()).toBe(false)
  })

  it('navigates to registration-new on register button click', async () => {
    campEditionsMock.currentCampEdition.value = makeEdition({ id: 'edition-abc', status: 'Open' })
    familyUnitsMock.familyUnit.value = { representativeUserId: 'u1' }
    const wrapper = mountPage()
    await nextTick()
    await wrapper.find('[data-testid="register-button"]').trigger('click')
    expect(routerPushMock).toHaveBeenCalledWith({
      name: 'registration-new',
      params: { editionId: 'edition-abc' },
    })
  })

  it('shows error message when error is set', async () => {
    campEditionsMock.error.value = 'Error de conexión'
    const wrapper = mountPage()
    await nextTick()
    expect(wrapper.text()).toContain('Error de conexión')
  })
})
```

- **Implementation Notes**:
  - Uses `vi.hoisted()` for mutable reactive-like refs (same pattern as `FamilyUnitPage.spec.ts`).
  - Stubs PrimeVue components to avoid rendering complexities.
  - The `makeEdition` factory keeps each test focused by only overriding relevant fields.
  - The test for year 2025 relies on real clock (`new Date().getFullYear()`). Since 2025 < 2026, it works as long as the test runs in 2026. If needed in the future, inject a clock mock.

---

### Step 7: Update Technical Documentation

- **Action**: Review and update relevant documentation files.
- **Implementation Steps**:

  1. **`ai-specs/specs/api-endpoints.md`**: Verify the `GET /api/camps/current` section reflects the enriched response (this is also covered in the backend ticket's Step 6; coordinate to avoid duplicate edits). If the backend ticket already updated it, no further changes needed here.

  2. **`ai-specs/specs/frontend-standards.mdc`**: No structural changes needed. The patterns used (composables, `<script setup>`, PrimeVue + Tailwind) are already documented.

  3. **No new routing, stores, or library additions** — no documentation impact beyond what the backend ticket covers.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-current-camp-landing-frontend`
2. **Step 6** — Write Vitest unit tests first (TDD RED phase) — `CampPage.spec.ts`
3. **Step 1** — Add `CurrentCampEditionResponse` TypeScript interface
4. **Step 2** — Update `useCampEditions` composable ref type
5. **Step 3** — Create `CampExtrasSection.vue` component
6. **Step 4** — Rewrite `CampPage.vue`
7. Run Vitest: `cd frontend && npx vitest run src/views/__tests__/CampPage.spec.ts` (GREEN phase)
8. **Step 5** — Update Cypress fixtures
9. Run Cypress: `npx cypress run --spec cypress/e2e/camp-edition.cy.ts` — verify all 5 tests pass
10. **Step 7** — Update documentation if needed

---

## Testing Checklist

### Vitest Unit Tests (`CampPage.spec.ts`)
- [ ] Calls `fetchCurrentCampEdition` and `getCurrentUserFamilyUnit` on mount
- [ ] Shows `[data-testid="camp-loading"]` when loading
- [ ] Shows `[data-testid="camp-empty"]` + "No hay información de campamento disponible" when no edition
- [ ] Shows "Campamento 2026" in `<h1>` when edition exists
- [ ] Shows previous-year warning for `year: 2025`
- [ ] Shows `[data-testid="register-button"]` for representative + Open status
- [ ] Does NOT show `[data-testid="register-button"]` for non-representative
- [ ] `router.push` called with correct edition ID on register click
- [ ] Shows error message when `error` is set

### Cypress E2E Tests (`camp-edition.cy.ts`) — already written, must pass
- [ ] Shows loading state while fetching (`camp-loading` visible during delay)
- [ ] Displays "Campamento 2026" in h1 for open camp
- [ ] Shows "Mostrando información del campamento de 2025" for previous-year fixture
- [ ] Shows "No hay información de campamento disponible" on 404
- [ ] Shows "Inscripciones Abiertas" CTA when status is Open

### Manual verification
- [ ] Photos section visible with photos in response
- [ ] Map section visible with coordinates in response
- [ ] Accommodation section visible when `accommodationCapacity` is non-null
- [ ] Extras section visible when `extras.length > 0`
- [ ] Contact section visible when any contact field is present
- [ ] Page is responsive on mobile (single column) and desktop (2-column for description/map)
- [ ] No `<style>` blocks in any new/modified components

---

## Error Handling Patterns

| State | UI Behavior |
|---|---|
| `loading === true` | Full-page centered `ProgressSpinner` with `data-testid="camp-loading"` and `role="status"` |
| `error !== null` | PrimeVue `<Message severity="error">` with error text |
| `currentCampEdition === null` (404 or no data) | Empty state card with `data-testid="camp-empty"` and informative text |
| Network error | Composable sets `error.value` → shows error `Message` |

The composable's `fetchCurrentCampEdition` already handles 404 as `null` (not an error), so `currentCampEdition.value === null` on 404 renders the empty state, while other errors populate `error.value` and render the error message.

---

## UI/UX Considerations

- **Mobile-first**: Single column, sections stack vertically. `lg:grid-cols-2` for description/map side-by-side on desktop.
- **PrimeVue components used**: `ProgressSpinner`, `Message`, `Button`, `ProgressBar`, `Badge` — all already registered globally in the project.
- **Accessibility**:
  - Loading spinner has `role="status"`.
  - External links (Google Maps, website) have `target="_blank" rel="noopener noreferrer"` and `aria-label`.
  - Phone link uses `href="tel:..."`.
- **No `<style>` blocks** — pure Tailwind.
- **Consistent section spacing**: `mt-6` between sections, `p-6` inside cards, `rounded-lg border border-gray-200 bg-white` for card styling.

---

## Dependencies

No new npm packages required. All PrimeVue components used (`ProgressBar`, `Badge`) are already part of the project dependency. `ProgressBar` is a PrimeVue component — verify it is registered in the PrimeVue plugin setup if auto-import is not configured.

> **Check**: Look at `frontend/src/main.ts` or the PrimeVue plugin setup to confirm `ProgressBar` and `Badge` are registered. If using PrimeVue auto-import (via `unplugin-vue-components`), no manual registration is needed.

---

## Notes

1. **TDD requirement**: Write `CampPage.spec.ts` tests BEFORE implementing `CampPage.vue`. This is a project-wide TDD rule.

2. **Backend dependency**: The new fields (`campPhotos`, `extras`, `campDescription`, etc.) will return empty arrays / null until the backend ticket is deployed. The page handles this gracefully — each section only renders when its data is non-null/non-empty.

3. **Type alignment**: The `CurrentCampEditionResponse` TS interface must exactly mirror the backend DTO. If the backend adds/changes a field, update the interface accordingly.

4. **`ProgressBar` value**: The `value` prop accepts `0-100`. The `capacityPercent` computed ensures this range with `Math.round(...)`.

5. **Previous-year detection**: Uses `new Date().getFullYear()` on the client — no time-zone issues since we compare year integers only.

6. **`CampExtrasSection` extras type**: The `extras` prop uses `CampEditionExtra[]`. The `CampEditionExtra` type in `camp-edition.ts` has `pricingType: 'PerPerson' | 'PerFamily'` and `pricingPeriod: 'OneTime' | 'PerDay'` as string literals — confirm the backend serializes enums as strings (it does, per the existing `CampEditionExtraResponse`).

---

## Next Steps After Implementation

1. **Merge order**: Backend ticket must be merged and deployed before the new fields appear in production. In development, the backend enrichment can be tested locally.
2. **Visual polish**: Once base implementation is working, consider adding a hero image using the primary camp photo as a full-width background with overlay for a more magazine-style design (this would be a follow-up ticket).
3. **Countdown timer**: A "X días para el campamento" countdown badge near the hero could be a follow-up enhancement.

---

## Implementation Verification

- [ ] **Code Quality**: All new/modified files use `<script setup lang="ts">`, no Options API, no `any`, no `<style>` blocks
- [ ] **Composable**: `currentCampEdition` ref is typed as `CurrentCampEditionResponse | null`
- [ ] **Functionality**: Page renders all sections correctly for Open / Closed / Completed / null states
- [ ] **Fixtures**: Both Cypress fixtures use flat `CurrentCampEditionResponse` shape
- [ ] **Testing**: All Vitest unit tests GREEN; all 5 Cypress E2E tests GREEN
- [ ] **Responsiveness**: Verified on mobile (375px) and desktop (1280px) viewport
- [ ] **Accessibility**: `role="status"` on spinner; `aria-label` on external links
- [ ] **No regressions**: Other existing Cypress tests still pass (`camp-editions.cy.ts`, `camp-photos.cy.ts`, etc.)

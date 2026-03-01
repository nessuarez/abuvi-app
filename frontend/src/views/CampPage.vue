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

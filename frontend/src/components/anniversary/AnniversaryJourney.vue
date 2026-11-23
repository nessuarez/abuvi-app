<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import Button from 'primevue/button'
import Skeleton from 'primevue/skeleton'
import CampLocationMap from '@/components/camps/CampLocationMap.vue'
import AnniversaryVenueList from '@/components/anniversary/AnniversaryVenueList.vue'
import AnniversaryYearStrip from '@/components/anniversary/AnniversaryYearStrip.vue'
import { RouterLink } from 'vue-router'
import { useCampHistory } from '@/composables/useCampHistory'
import { useAlbums } from '@/composables/useAlbums'
import type { CampLocation } from '@/types/camp'

const emit = defineEmits<{
  'update:year': [year: number | null]
}>()

/** Slow enough to read a venue name, fast enough to cross fifty years in under two minutes. */
const STEP_MS = 2000

const { entries, venues, years, loading, error, fetchHistory, entryByYear } = useCampHistory()

const selectedYear = ref<number | null>(null)

const selectedEntry = computed(() =>
  selectedYear.value == null ? undefined : entryByYear(selectedYear.value)
)
const selectedCampId = computed(() => selectedEntry.value?.campId ?? null)

// The history endpoint is keyed by year and carries no edition id, so the album index
// supplies it. Both assume one edition per year, true for 1976-2025.
const { fetchIndex: fetchAlbumIndex, editionIdByYear } = useAlbums()

const selectedEditionId = computed(() =>
  selectedYear.value == null ? null : (editionIdByYear.value.get(selectedYear.value) ?? null)
)

/** Venues the map can actually place. A venue without coordinates still shows in the list. */
const mapLocations = computed<CampLocation[]>(() =>
  venues.value
    .filter((venue) => venue.latitude !== null && venue.longitude !== null)
    .map((venue) => ({
      id: venue.campId,
      name: venue.campName,
      latitude: venue.latitude as number,
      longitude: venue.longitude as number,
      location: venue.location ?? undefined,
      editionYears: venue.years,
      editionCount: venue.totalEditionsAtVenue
    }))
)

const unplacedVenueCount = computed(() => venues.value.length - mapLocations.value.length)

// --- Presentation mode -------------------------------------------------------------------

const isPlaying = ref(false)
let timer: ReturnType<typeof setInterval> | null = null

const stopPlayback = () => {
  if (timer !== null) {
    clearInterval(timer)
    timer = null
  }
  isPlaying.value = false
}

/** Advances without going through selectYear, so the tour does not stop itself. */
const advance = () => {
  const all = years.value
  if (all.length === 0) return
  const current = selectedYear.value == null ? -1 : all.indexOf(selectedYear.value)
  selectedYear.value = all[(current + 1) % all.length]
}

const startPlayback = () => {
  if (years.value.length === 0) return
  isPlaying.value = true
  advance()
  timer = setInterval(advance, STEP_MS)
}

const togglePlayback = () => (isPlaying.value ? stopPlayback() : startPlayback())

// --- Selection ---------------------------------------------------------------------------

/** Every user-driven path funnels through here, and every one of them stops the tour. */
const selectYear = (year: number) => {
  stopPlayback()
  selectedYear.value = year
}

const selectVenue = (campId: string) => {
  const venue = venues.value.find((v) => v.campId === campId)
  if (!venue || venue.years.length === 0) return
  // The latest edition is the one most likely to have something to show.
  selectYear(venue.years[venue.years.length - 1])
}

const clearSelection = () => {
  stopPlayback()
  selectedYear.value = null
}

const scrollToUpload = () => {
  document.getElementById('subir-recuerdo')?.scrollIntoView({ behavior: 'smooth' })
}

watch(selectedYear, (year) => emit('update:year', year))

onMounted(() => {
  fetchHistory()
  // Independent of the history call: a failure here only costs the album link.
  fetchAlbumIndex()
})
// A leaked interval keeps panning a destroyed Leaflet map, which throws on every tick.
onUnmounted(stopPlayback)
</script>

<template>
  <section aria-label="Recorrido por los campamentos" class="mx-auto max-w-7xl px-6">
    <div class="mb-8 text-center">
      <h2 class="mb-4 text-3xl font-bold text-amber-900 md:text-4xl">Cincuenta años de mapa</h2>
      <p class="mx-auto max-w-2xl text-gray-600">
        Cada punto es un lugar donde ABUVI plantó las tiendas. Los más grandes son a los que
        volvimos una y otra vez.
      </p>
    </div>

    <!-- Loading state: shaped like the final layout, so nothing jumps when it arrives -->
    <div v-if="loading" class="grid grid-cols-1 gap-6 lg:grid-cols-5">
      <Skeleton height="55vh" class="lg:col-span-3" />
      <div class="lg:col-span-2">
        <Skeleton v-for="i in 6" :key="i" height="4rem" class="mb-2" />
      </div>
    </div>

    <!-- Error state -->
    <div v-else-if="error" class="py-12 text-center">
      <i class="pi pi-exclamation-triangle mb-4 text-4xl text-red-400" />
      <p class="text-lg text-gray-500">No se pudo cargar el recorrido.</p>
      <p class="mt-2 text-sm text-gray-400">{{ error }}</p>
    </div>

    <template v-else>
      <!-- Controls -->
      <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
        <p class="text-sm text-gray-600">
          {{ entries.length }} ediciones en {{ venues.length }} sedes
          <span v-if="unplacedVenueCount > 0" class="text-gray-400">
            · {{ unplacedVenueCount }} sin ubicar en el mapa
          </span>
        </p>
        <div class="flex items-center gap-2">
          <Button
            v-if="selectedYear !== null"
            label="Ver todos los años"
            severity="secondary"
            text
            size="small"
            @click="clearSelection"
          />
          <Button
            :label="isPlaying ? 'Pausar recorrido' : 'Recorrer los 50 años'"
            :icon="isPlaying ? 'pi pi-pause' : 'pi pi-play'"
            severity="warn"
            size="small"
            @click="togglePlayback"
          />
        </div>
      </div>

      <!-- Map (60%) and venue list (40%) -->
      <div class="grid grid-cols-1 gap-6 lg:grid-cols-5">
        <div class="lg:col-span-3">
          <CampLocationMap
            :locations="mapLocations"
            :selected-id="selectedCampId ?? undefined"
            height-class="h-[55vh] lg:h-[600px]"
            @select-location="selectVenue"
            @select-year="selectYear"
          />
        </div>
        <div class="lg:col-span-2">
          <AnniversaryVenueList
            :venues="venues"
            :selected-year="selectedYear"
            :selected-camp-id="selectedCampId"
            height-class="h-[45vh] lg:h-[600px]"
            @select-year="selectYear"
            @select-venue="selectVenue"
          />
        </div>
      </div>

      <!-- Year strip -->
      <div class="mt-6">
        <AnniversaryYearStrip
          :entries="entries"
          :selected-year="selectedYear"
          @select-year="selectYear"
        />
      </div>

      <!-- What we have (or do not have) for the selected year -->
      <div v-if="selectedEntry" class="mt-6 rounded-xl border border-amber-200 bg-white p-6">
        <div class="flex flex-wrap items-baseline gap-x-2">
          <span class="text-2xl font-bold text-amber-600">{{ selectedEntry.year }}</span>
          <span class="text-lg font-semibold text-gray-900">{{ selectedEntry.campName }}</span>
          <span v-if="selectedEntry.location" class="text-sm text-gray-500">
            {{ selectedEntry.location }}
          </span>
          <span v-if="selectedEntry.totalEditionsAtVenue > 1" class="text-sm text-gray-500">
            · edición {{ selectedEntry.editionNumber }} de
            {{ selectedEntry.totalEditionsAtVenue }} aquí
          </span>
        </div>

        <!-- The way into the year's full album. Hidden when no edition maps to this
             year, rather than rendering a link that would 404. -->
        <RouterLink
          v-if="selectedEditionId"
          :to="{ name: 'anniversary-album', params: { editionId: selectedEditionId } }"
          class="mt-4 inline-flex items-center gap-2 font-medium text-amber-700 underline hover:text-amber-900"
        >
          Ver el álbum de {{ selectedEntry.year }}
          <i class="pi pi-arrow-right text-xs" aria-hidden="true" />
        </RouterLink>

        <!-- Nothing survives from this year: that is the ask, not a failure -->
        <div v-if="selectedEntry.photoCount === 0" class="mt-4">
          <p class="text-gray-600">
            De {{ selectedEntry.year }} en {{ selectedEntry.campName }} no conservamos nada
            todavía. ¿Tienes algo?
          </p>
          <Button
            label="Comparte tu recuerdo"
            icon="pi pi-upload"
            severity="warn"
            size="small"
            class="mt-3"
            @click="scrollToUpload"
          />
        </div>

        <!-- Previews come with the same request: no second call to show them -->
        <div v-else class="mt-4">
          <p class="mb-3 text-sm text-gray-600">
            {{ selectedEntry.photoCount }}
            {{ selectedEntry.photoCount === 1 ? 'recuerdo' : 'recuerdos' }} de este año.
          </p>
          <div class="flex flex-wrap gap-3">
            <img
              v-for="photo in selectedEntry.previewPhotos"
              :key="photo.id"
              :src="photo.thumbnailUrl"
              :alt="photo.title"
              loading="lazy"
              class="h-24 w-32 rounded-lg object-cover"
            />
          </div>
        </div>
      </div>
    </template>
  </section>
</template>

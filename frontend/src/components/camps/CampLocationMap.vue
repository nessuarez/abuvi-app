<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { CampLocation } from '@/types/camp'

interface Props {
  locations: CampLocation[]
  selectedId?: string
  /** Tailwind height class for the map container. The default preserves the original size. */
  heightClass?: string
}

const props = withDefaults(defineProps<Props>(), {
  heightClass: 'h-[500px]'
})
const emit = defineEmits<{
  selectLocation: [id: string]
  selectYear: [year: number]
}>()

const mapContainer = ref<HTMLElement | null>(null)
let map: L.Map | null = null
const markers: Map<string, L.Marker> = new Map()

/** Callers that pass no id keep being identified by name, as before. */
const keyOf = (location: CampLocation): string => location.id ?? location.name

// Fix Leaflet default icon issue with Vite
delete (L.Icon.Default.prototype as any)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png'
})

/**
 * Popup content is built as a DOM node, not an HTML string: the year chips need real click
 * handlers, and venue names reach us from the database, so they go in via textContent.
 */
const buildPopup = (location: CampLocation): HTMLElement => {
  const root = document.createElement('div')
  root.className = 'text-sm'

  const name = document.createElement('strong')
  name.textContent = location.name
  root.appendChild(name)

  const addLine = (text: string) => {
    const line = document.createElement('div')
    line.textContent = text
    root.appendChild(line)
  }

  if (location.location) addLine(location.location)
  if (location.lastEditionYear) addLine(`Última edición: ${location.lastEditionYear}`)
  if (location.year) addLine(String(location.year))

  if (location.editionYears?.length) {
    const years = document.createElement('div')
    years.className = 'mt-2 flex flex-wrap gap-1'

    for (const year of location.editionYears) {
      const chip = document.createElement('button')
      chip.type = 'button'
      chip.textContent = String(year)
      chip.className =
        'rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800 hover:bg-amber-200'
      chip.setAttribute('aria-label', `Edición de ${year} en ${location.name}`)
      chip.addEventListener('click', () => emit('selectYear', year))
      years.appendChild(chip)
    }

    root.appendChild(years)
  }

  return root
}

/**
 * Venues with several editions get a bigger, numbered pin, so where the association kept
 * coming back reads off the map without opening anything.
 */
const buildIcon = (location: CampLocation): L.DivIcon | undefined => {
  if (location.editionCount == null) return undefined

  const size = 28 + Math.min(location.editionCount, 4) * 6
  const label = location.editionCount > 1 ? String(location.editionCount) : ''

  return L.divIcon({
    className: 'camp-marker',
    html: `<span class="flex h-full w-full items-center justify-center rounded-full border-2 border-white bg-amber-500 text-xs font-bold text-white shadow-md">${label}</span>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2]
  })
}

const highlightSelected = () => {
  markers.forEach((marker, key) => {
    marker.getElement()?.classList.toggle('camp-marker--selected', key === props.selectedId)
  })
}

const updateMarkers = () => {
  if (!map) return

  // Clear existing markers
  markers.forEach((marker) => marker.remove())
  markers.clear()

  // Add new markers
  props.locations.forEach((location) => {
    const icon = buildIcon(location)
    const marker = L.marker([location.latitude, location.longitude], icon ? { icon } : undefined)
      .addTo(map as L.Map)
      .bindPopup(buildPopup(location))

    // Handle marker click
    marker.on('click', () => {
      emit('selectLocation', keyOf(location))
    })

    markers.set(keyOf(location), marker)
  })

  // Fit map to markers if there are any
  if (props.locations.length > 0) {
    const bounds = L.latLngBounds(
      props.locations.map((loc) => [loc.latitude, loc.longitude] as L.LatLngTuple)
    )
    map?.fitBounds(bounds, { padding: [50, 50], maxZoom: 10 })
  }

  highlightSelected()
}

const initializeMap = () => {
  if (!mapContainer.value || map) return

  // Initialize map centered on Spain
  map = L.map(mapContainer.value).setView([40.4168, -3.7038], 6)

  // Add OpenStreetMap tiles
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 19
  }).addTo(map)

  // Add markers for each location
  updateMarkers()
}

// Leaflet caches the container size, so a viewport change leaves the map clipped until it
// is told to measure again. Rotating a phone or resizing a projector window hits this.
const handleResize = () => map?.invalidateSize()

onMounted(async () => {
  initializeMap()
  // The container is often sized by its grid/flex parent after mount; without this the
  // tiles render grey in a side-by-side layout.
  await nextTick()
  map?.invalidateSize()
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  if (map) {
    map.remove()
    map = null
  }
})

// Watch for location changes
watch(
  () => props.locations,
  () => {
    updateMarkers()
  },
  { deep: true }
)

// Selection is driven from outside: centre the map on the chosen venue and open its popup.
watch(
  () => props.selectedId,
  (id) => {
    highlightSelected()
    if (!map || !id) return

    const marker = markers.get(id)
    if (!marker) return

    map.panTo(marker.getLatLng())
    marker.openPopup()
  }
)
</script>

<template>
  <div ref="mapContainer" :class="heightClass" class="w-full rounded-lg border border-gray-200" />
</template>

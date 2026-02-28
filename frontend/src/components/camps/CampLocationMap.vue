<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { CampLocation } from '@/types/camp'

interface Props {
  locations: CampLocation[]
  selectedId?: string
}

const props = defineProps<Props>()
const emit = defineEmits<{
  selectLocation: [id: string]
}>()

const mapContainer = ref<HTMLElement | null>(null)
let map: L.Map | null = null
const markers: Map<string, L.Marker> = new Map()

// Fix Leaflet default icon issue with Vite
delete (L.Icon.Default.prototype as any)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png'
})

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

const updateMarkers = () => {
  if (!map) return

  // Clear existing markers
  markers.forEach((marker) => marker.remove())
  markers.clear()

  // Add new markers
  props.locations.forEach((location) => {
    const marker = L.marker([location.latitude, location.longitude])
      .addTo(map as L.Map)
      .bindPopup(
        `<div class="text-sm"><strong>${location.name}</strong>${location.rawAddress ? `<br><span>${location.rawAddress}</span>` : ''}${location.lastEditionYear ? `<br><span>Última edición: ${location.lastEditionYear}</span>` : ''}${location.year ? `<br><span>${location.year}</span>` : ''}</div>`
      )

    // Handle marker click
    marker.on('click', () => {
      emit('selectLocation', location.name)
    })

    markers.set(location.name, marker)
  })

  // Fit map to markers if there are any
  if (props.locations.length > 0) {
    const bounds = L.latLngBounds(
      props.locations.map((loc) => [loc.latitude, loc.longitude] as L.LatLngTuple)
    )
    map?.fitBounds(bounds, { padding: [50, 50], maxZoom: 10 })
  }
}

onMounted(() => {
  initializeMap()
})

onUnmounted(() => {
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
</script>

<template>
  <div ref="mapContainer" class="h-[500px] w-full rounded-lg border border-gray-200" />
</template>

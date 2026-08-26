<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import Button from 'primevue/button'

interface Props {
  latitude: number | null
  longitude: number | null
  /** Shown in the marker tooltip so you know which camp you are placing. */
  name?: string
}

const props = defineProps<Props>()
const emit = defineEmits<{
  'update:latitude': [value: number]
  'update:longitude': [value: number]
}>()

// Centre of the Iberian peninsula, used when a camp has no coordinates yet.
const FALLBACK_CENTER: L.LatLngTuple = [40.4168, -3.7038]
const FALLBACK_ZOOM = 6
const PLACED_ZOOM = 15

const mapContainer = ref<HTMLElement | null>(null)
const isSatellite = ref(true)

// The marker stays locked until it is explicitly unlocked, so a stray click on a
// map you are only reading never silently moves a camp that was already correct.
const isEditing = ref(false)

let map: L.Map | null = null
let marker: L.Marker | null = null
let streetLayer: L.TileLayer | null = null
let satelliteLayer: L.TileLayer | null = null

// Leaflet's default icon paths break under Vite; same fix as CampLocationMap.
delete (L.Icon.Default.prototype as unknown as { _getIconUrl?: unknown })._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png'
})

const hasPosition = () => props.latitude !== null && props.longitude !== null

const currentPosition = (): L.LatLngTuple =>
  hasPosition() ? [props.latitude as number, props.longitude as number] : FALLBACK_CENTER

const publish = (latlng: L.LatLng) => {
  // Six decimals is ~10 cm, well beyond what we need and what the DB column keeps.
  emit('update:latitude', Number(latlng.lat.toFixed(6)))
  emit('update:longitude', Number(latlng.lng.toFixed(6)))
}

const placeMarker = (position: L.LatLngTuple) => {
  if (!map) return

  if (marker) {
    marker.setLatLng(position)
    return
  }

  marker = L.marker(position, { draggable: isEditing.value, autoPan: true }).addTo(map)
  marker.bindTooltip(props.name || 'Ubicación actual', { permanent: false })
  marker.on('dragend', () => {
    if (marker) publish(marker.getLatLng())
  })
}

const initializeMap = () => {
  if (!mapContainer.value || map) return

  map = L.map(mapContainer.value).setView(
    currentPosition(),
    hasPosition() ? PLACED_ZOOM : FALLBACK_ZOOM
  )

  streetLayer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    maxZoom: 19
  })

  // Aerial imagery makes an actual campsite visible, which a street map does not.
  satelliteLayer = L.tileLayer(
    'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    { attribution: '&copy; Esri, Maxar, Earthstar Geographics', maxZoom: 19 }
  )

  ;(isSatellite.value ? satelliteLayer : streetLayer).addTo(map)

  // Clicking anywhere drops (or moves) the marker there, but only once unlocked.
  map.on('click', (e: L.LeafletMouseEvent) => {
    if (!isEditing.value) return
    placeMarker([e.latlng.lat, e.latlng.lng])
    publish(e.latlng)
  })

  if (hasPosition()) placeMarker(currentPosition())
}

const toggleBaseLayer = () => {
  if (!map || !streetLayer || !satelliteLayer) return

  const [remove, add] = isSatellite.value
    ? [satelliteLayer, streetLayer]
    : [streetLayer, satelliteLayer]

  map.removeLayer(remove)
  add.addTo(map)
  isSatellite.value = !isSatellite.value
}

const toggleEditing = () => {
  isEditing.value = !isEditing.value

  const dragging = (marker as unknown as { dragging?: { enable: () => void; disable: () => void } })
    ?.dragging
  if (isEditing.value) dragging?.enable()
  else dragging?.disable()
}

const centreOnMarker = () => {
  if (!map || !hasPosition()) return
  map.setView(currentPosition(), Math.max(map.getZoom(), PLACED_ZOOM))
}

// Leaflet caches the container size, so a viewport change leaves the map clipped until it
// is told to measure again. Rotating a phone or resizing a projector window hits this.
const handleResize = () => map?.invalidateSize()

onMounted(async () => {
  initializeMap()
  // This picker sits inside a form laid out by its flex/grid parent, which finishes sizing
  // the container after mount; without this the tiles never load, same fix as CampLocationMap.
  await nextTick()
  map?.invalidateSize()
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  map?.remove()
  map = null
  marker = null
})

// Coordinates can also change from the number inputs or a Google Places pick.
watch(
  () => [props.latitude, props.longitude],
  () => {
    if (!map || !hasPosition()) return
    placeMarker(currentPosition())
    map.setView(currentPosition(), Math.max(map.getZoom(), PLACED_ZOOM))
  }
)
</script>

<template>
  <div class="space-y-2">
    <div class="flex flex-wrap items-center justify-between gap-2">
      <p class="text-sm" :class="isEditing ? 'font-medium text-amber-800' : 'text-gray-600'">
        {{
          isEditing
            ? 'Edición activa: al hacer clic o arrastrar cambiarás la ubicación guardada.'
            : 'La ubicación está bloqueada. Desbloquéala para poder moverla.'
        }}
      </p>
      <div class="flex gap-2">
        <Button
          type="button"
          size="small"
          :severity="isEditing ? 'warn' : 'secondary'"
          :outlined="!isEditing"
          :icon="isEditing ? 'pi pi-lock-open' : 'pi pi-lock'"
          :label="isEditing ? 'Bloquear ubicación' : 'Desbloquear para mover'"
          @click="toggleEditing"
        />
        <Button
          type="button"
          size="small"
          severity="secondary"
          outlined
          :icon="isSatellite ? 'pi pi-map' : 'pi pi-globe'"
          :label="isSatellite ? 'Callejero' : 'Satélite'"
          @click="toggleBaseLayer"
        />
        <Button
          type="button"
          size="small"
          severity="secondary"
          outlined
          icon="pi pi-crosshairs"
          label="Centrar"
          :disabled="latitude === null || longitude === null"
          @click="centreOnMarker"
        />
      </div>
    </div>

    <div
      v-if="isEditing"
      class="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900"
      role="alert"
    >
      <i class="pi pi-exclamation-triangle mt-0.5" />
      <span>
        Estás moviendo el punto de <strong>{{ name || 'este campamento' }}</strong>.
        El cambio no se guarda hasta que envíes el formulario.
      </span>
    </div>

    <div
      ref="mapContainer"
      class="h-[420px] w-full rounded-lg border-2"
      :class="isEditing ? 'border-amber-400' : 'border-gray-200'"
    />

    <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs">
      <span v-if="latitude !== null && longitude !== null" class="font-mono text-gray-700">
        {{ latitude.toFixed(6) }}, {{ longitude.toFixed(6) }}
      </span>
      <span v-else class="text-amber-700">
        Sin coordenadas — haz clic en el mapa para asignarlas.
      </span>
    </div>
  </div>
</template>

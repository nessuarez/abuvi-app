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

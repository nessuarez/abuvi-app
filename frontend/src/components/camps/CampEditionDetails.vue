<script setup lang="ts">
import { computed } from 'vue'
import type { CampEdition } from '@/types/camp-edition'
import type { AgeRangeSettings } from '@/types/association-settings'
import type { CampLocation } from '@/types/camp'
import Card from 'primevue/card'
import PricingBreakdown from '@/components/camps/PricingBreakdown.vue'
import CampLocationMap from '@/components/camps/CampLocationMap.vue'
import AccommodationCapacityDisplay from '@/components/camps/AccommodationCapacityDisplay.vue'

interface Props {
  campEdition: CampEdition
}

const props = defineProps<Props>()

const formatDate = (dateStr: string) =>
  new Date(dateStr).toLocaleDateString('es-ES', {
    year: 'numeric', month: 'long', day: 'numeric'
  })

const durationDays = computed(() => {
  const start = new Date(props.campEdition.startDate)
  const end = new Date(props.campEdition.endDate)
  return Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24)) + 1
})

const ageRanges = computed<AgeRangeSettings>(() => ({
  babyMaxAge: props.campEdition.babyMaxAge ?? 3,
  childMinAge: props.campEdition.childMinAge ?? 4,
  childMaxAge: props.campEdition.childMaxAge ?? 14,
  adultMinAge: props.campEdition.adultMinAge ?? 15
}))

const mapLocations = computed<CampLocation[]>(() => {
  const camp = props.campEdition.camp
  if (!camp || camp.latitude == null || camp.longitude == null) return []
  return [{ latitude: camp.latitude, longitude: camp.longitude, name: camp.name }]
})
</script>

<template>
  <div class="grid grid-cols-1 gap-4 md:grid-cols-2">
    <!-- Dates Card -->
    <Card>
      <template #title>
        <div class="flex items-center gap-2">
          <i class="pi pi-calendar text-primary-600" />
          Fechas
        </div>
      </template>
      <template #content>
        <div class="space-y-2 text-sm">
          <div class="flex justify-between">
            <span class="text-gray-500">Inicio:</span>
            <span class="font-medium">{{ formatDate(campEdition.startDate) }}</span>
          </div>
          <div class="flex justify-between">
            <span class="text-gray-500">Fin:</span>
            <span class="font-medium">{{ formatDate(campEdition.endDate) }}</span>
          </div>
          <div class="flex justify-between">
            <span class="text-gray-500">Duración:</span>
            <span class="font-medium">{{ durationDays }} días</span>
          </div>
        </div>
      </template>
    </Card>

    <!-- Location Card -->
    <Card>
      <template #title>
        <div class="flex items-center gap-2">
          <i class="pi pi-map-marker text-primary-600" />
          Ubicación
        </div>
      </template>
      <template #content>
        <p class="mb-3 text-sm font-medium text-gray-700">{{ campEdition.location }}</p>
        <CampLocationMap
          v-if="mapLocations.length > 0"
          :locations="mapLocations"
        />
      </template>
    </Card>

    <!-- Description Card -->
    <Card v-if="campEdition.description" class="md:col-span-2">
      <template #title>
        <div class="flex items-center gap-2">
          <i class="pi pi-info-circle text-primary-600" />
          Descripción
        </div>
      </template>
      <template #content>
        <p class="text-sm text-gray-700">{{ campEdition.description }}</p>
      </template>
    </Card>

    <!-- Pricing Card -->
    <Card class="md:col-span-2">
      <template #title>
        <div class="flex items-center gap-2">
          <i class="pi pi-euro text-primary-600" />
          Precios
        </div>
      </template>
      <template #content>
        <PricingBreakdown
          :price-per-adult="campEdition.pricePerAdult"
          :price-per-child="campEdition.pricePerChild"
          :price-per-baby="campEdition.pricePerBaby"
          :age-ranges="ageRanges"
        />
      </template>
    </Card>

    <!-- Capacity Card -->
    <Card v-if="campEdition.registrationCount !== undefined || campEdition.availableSpots !== undefined">
      <template #title>
        <div class="flex items-center gap-2">
          <i class="pi pi-users text-primary-600" />
          Capacidad
        </div>
      </template>
      <template #content>
        <div class="space-y-2 text-sm">
          <div class="flex justify-between">
            <span class="text-gray-500">Capacidad máxima:</span>
            <span class="font-medium">{{ campEdition.maxCapacity }}</span>
          </div>
          <div v-if="campEdition.registrationCount !== undefined" class="flex justify-between">
            <span class="text-gray-500">Inscritos:</span>
            <span class="font-medium">{{ campEdition.registrationCount }}</span>
          </div>
          <div v-if="campEdition.availableSpots !== undefined" class="flex justify-between">
            <span class="text-gray-500">Plazas disponibles:</span>
            <span class="font-medium text-green-600">{{ campEdition.availableSpots }}</span>
          </div>
        </div>
      </template>
    </Card>

    <!-- Accommodation Capacity Card -->
    <AccommodationCapacityDisplay
      v-if="campEdition.accommodationCapacity"
      :capacity="campEdition.accommodationCapacity"
      :total-bed-capacity="campEdition.calculatedTotalBedCapacity"
      class="md:col-span-2"
    />

    <!-- Contact Card -->
    <Card v-if="campEdition.contactEmail || campEdition.contactPhone">
      <template #title>
        <div class="flex items-center gap-2">
          <i class="pi pi-envelope text-primary-600" />
          Contacto
        </div>
      </template>
      <template #content>
        <div class="space-y-2 text-sm">
          <div v-if="campEdition.contactEmail" class="flex items-center gap-2">
            <i class="pi pi-envelope text-gray-400" />
            <a :href="`mailto:${campEdition.contactEmail}`" class="text-primary-600 hover:underline">
              {{ campEdition.contactEmail }}
            </a>
          </div>
          <div v-if="campEdition.contactPhone" class="flex items-center gap-2">
            <i class="pi pi-phone text-gray-400" />
            <a :href="`tel:${campEdition.contactPhone}`" class="text-primary-600 hover:underline">
              {{ campEdition.contactPhone }}
            </a>
          </div>
        </div>
      </template>
    </Card>
  </div>
</template>

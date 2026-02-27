<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import Galleria from 'primevue/galleria'
import type { Camp } from '@/types/camp'
import type { CampPhoto } from '@/types/camp-photo'

interface Props {
  camp: Camp
  photos?: CampPhoto[]
}

const props = defineProps<Props>()

const sortedPhotos = computed(() =>
  [...(props.photos ?? [])].sort((a, b) => a.displayOrder - b.displayOrder)
)
const emit = defineEmits<{
  edit: [camp: Camp]
  delete: [camp: Camp]
  viewDetails: [camp: Camp]
}>()

const truncatedDescription = computed(() => {
  if (!props.camp.description) return ''
  return props.camp.description.length > 150
    ? props.camp.description.substring(0, 150) + '...'
    : props.camp.description
})

const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('es-ES', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0
  }).format(amount)
}
</script>

<template>
  <div
    class="overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm transition-shadow hover:shadow-md"
  >
    <!-- Photo Carousel -->
    <Galleria
      v-if="sortedPhotos.length > 0"
      :value="sortedPhotos"
      :show-thumbnails="false"
      :show-item-navigators="sortedPhotos.length > 1"
      :show-indicators="sortedPhotos.length > 1"
      :circular="true"
      :auto-play="false"
      class="w-full"
    >
      <template #item="{ item }">
        <img
          :src="item.url"
          :alt="item.description ?? camp.name"
          class="h-48 w-full object-cover"
        />
      </template>
      <template #caption="{ item }">
        <span v-if="item.description" class="text-sm">{{ item.description }}</span>
      </template>
    </Galleria>
    <div
      v-else
      class="flex h-48 w-full items-center justify-center bg-gray-100 text-2xl font-bold text-gray-400"
    >
      {{ camp.name.charAt(0) }}
    </div>

    <div class="p-6">
    <!-- Header -->
    <div class="mb-3 flex items-start justify-between">
      <div>
        <h3 class="text-lg font-semibold text-gray-900">{{ camp.name }}</h3>
        <p v-if="camp.latitude !== null && camp.longitude !== null" class="text-sm text-gray-500">
          {{ camp.latitude.toFixed(4) }}, {{ camp.longitude.toFixed(4) }}
        </p>
      </div>
      <div class="flex items-center gap-2">
        <span
          v-if="camp.googleRating !== null"
          class="flex items-center gap-1 rounded-full bg-yellow-50 px-2 py-1 text-xs font-medium text-yellow-700"
          :aria-label="`Valoración de Google: ${camp.googleRating.toFixed(1)}`"
        >
          <i class="pi pi-star-fill text-xs text-yellow-400"></i>
          {{ camp.googleRating.toFixed(1) }}
        </span>
        <span
          :class="camp.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'"
          class="rounded-full px-2 py-1 text-xs font-medium"
        >
          {{ camp.isActive ? 'Activo' : 'Inactivo' }}
        </span>
      </div>
    </div>

    <!-- Description -->
    <p class="mb-4 text-sm text-gray-600">{{ truncatedDescription }}</p>

    <!-- Pricing Summary -->
    <div class="mb-4 grid grid-cols-3 gap-2">
      <div class="rounded bg-gray-50 p-2 text-center">
        <p class="text-xs text-gray-500">Adulto</p>
        <p class="text-sm font-semibold text-gray-900">{{ formatCurrency(camp.pricePerAdult) }}</p>
      </div>
      <div class="rounded bg-gray-50 p-2 text-center">
        <p class="text-xs text-gray-500">Niño</p>
        <p class="text-sm font-semibold text-gray-900">{{ formatCurrency(camp.pricePerChild) }}</p>
      </div>
      <div class="rounded bg-gray-50 p-2 text-center">
        <p class="text-xs text-gray-500">Bebé</p>
        <p class="text-sm font-semibold text-gray-900">{{ formatCurrency(camp.pricePerBaby) }}</p>
      </div>
    </div>

    <!-- Edition Count -->
    <div v-if="camp.editionCount !== undefined" class="mb-4">
      <span class="text-sm text-gray-600">
        {{ camp.editionCount }} {{ camp.editionCount === 1 ? 'edición' : 'ediciones' }}
      </span>
    </div>

    <!-- Actions -->
    <div class="flex gap-2">
      <Button
        label="Ver detalles"
        icon="pi pi-eye"
        size="small"
        text
        @click="emit('viewDetails', camp)"
      />
      <Button label="Editar" icon="pi pi-pencil" size="small" outlined @click="emit('edit', camp)" />
      <Button
        label="Eliminar"
        icon="pi pi-trash"
        size="small"
        severity="danger"
        outlined
        @click="emit('delete', camp)"
      />
    </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import type { Camp } from '@/types/camp'

interface Props {
  camp: Camp
}

const props = defineProps<Props>()
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

const statusLabel = computed(() => {
  const labels: Record<string, string> = {
    Active: 'Activo',
    Inactive: 'Inactivo',
    HistoricalArchive: 'Archivo Histórico'
  }
  return labels[props.camp.status] || props.camp.status
})
</script>

<template>
  <div
    class="rounded-lg border border-gray-200 bg-white p-6 shadow-sm transition-shadow hover:shadow-md"
  >
    <!-- Header -->
    <div class="mb-3 flex items-start justify-between">
      <div>
        <h3 class="text-lg font-semibold text-gray-900">{{ camp.name }}</h3>
        <p class="text-sm text-gray-500">
          {{ camp.latitude.toFixed(4) }}, {{ camp.longitude.toFixed(4) }}
        </p>
      </div>
      <span
        :class="{
          'bg-green-100 text-green-800': camp.status === 'Active',
          'bg-gray-100 text-gray-800': camp.status === 'Inactive',
          'bg-blue-100 text-blue-800': camp.status === 'HistoricalArchive'
        }"
        class="rounded-full px-2 py-1 text-xs font-medium"
      >
        {{ statusLabel }}
      </span>
    </div>

    <!-- Description -->
    <p class="mb-4 text-sm text-gray-600">{{ truncatedDescription }}</p>

    <!-- Pricing Summary -->
    <div class="mb-4 grid grid-cols-3 gap-2">
      <div class="rounded bg-gray-50 p-2 text-center">
        <p class="text-xs text-gray-500">Adulto</p>
        <p class="text-sm font-semibold text-gray-900">{{ formatCurrency(camp.basePriceAdult) }}</p>
      </div>
      <div class="rounded bg-gray-50 p-2 text-center">
        <p class="text-xs text-gray-500">Niño</p>
        <p class="text-sm font-semibold text-gray-900">{{ formatCurrency(camp.basePriceChild) }}</p>
      </div>
      <div class="rounded bg-gray-50 p-2 text-center">
        <p class="text-xs text-gray-500">Bebé</p>
        <p class="text-sm font-semibold text-gray-900">{{ formatCurrency(camp.basePriceBaby) }}</p>
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
</template>

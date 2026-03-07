<script setup lang="ts">
import type { RegistrationListItem } from '@/types/registration'
import RegistrationStatusBadge from './RegistrationStatusBadge.vue'
import Button from 'primevue/button'

defineProps<{
  registration: RegistrationListItem
}>()

const emit = defineEmits<{
  view: [id: string]
}>()

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short', year: 'numeric' }).format(
    new Date(dateStr)
  )

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)
</script>

<template>
  <div
    class="rounded-lg border border-gray-200 bg-white p-4 shadow-sm transition hover:shadow-md"
    data-testid="registration-card"
  >
    <div class="flex items-start justify-between gap-4">
      <div class="flex-1">
        <h3 class="text-base font-semibold text-gray-900">
          {{ registration.campEdition.campName }} {{ registration.campEdition.year }}
        </h3>
        <p class="mt-1 text-sm text-gray-500">
          {{ formatDate(registration.campEdition.startDate) }} —
          {{ formatDate(registration.campEdition.endDate) }}
        </p>
        <p v-if="registration.campEdition.location" class="mt-0.5 text-sm text-gray-400">
          <i class="pi pi-map-marker mr-1" />{{ registration.campEdition.location }}
        </p>
      </div>
      <RegistrationStatusBadge :status="registration.status" />
    </div>

    <div class="mt-3 flex items-center justify-between">
      <span class="text-sm font-medium text-gray-700">
        Total: {{ formatCurrency(registration.totalAmount) }}
      </span>
      <Button
        label="Ver detalles"
        icon="pi pi-arrow-right"
        icon-pos="right"
        size="small"
        severity="secondary"
        @click="emit('view', registration.id)"
        data-testid="view-registration-btn"
      />
    </div>
  </div>
</template>

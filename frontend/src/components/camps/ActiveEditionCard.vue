<script setup lang="ts">
import { computed } from 'vue'
import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
import type { ActiveCampEditionResponse } from '@/types/camp-edition'

defineProps<{
  edition: ActiveCampEditionResponse
}>()

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(new Date(dateStr))

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0
  }).format(amount)
</script>

<template>
  <div
    class="overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm"
    data-testid="active-edition-card"
  >
    <!-- Header -->
    <div class="border-b border-gray-100 bg-green-50 px-6 py-4">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 class="text-xl font-bold text-gray-900">{{ edition.campName }}</h2>
          <p v-if="edition.campLocation" class="mt-0.5 text-sm text-gray-600">
            {{ edition.campLocation }}
          </p>
        </div>
        <CampEditionStatusBadge :status="edition.status" />
      </div>
    </div>

    <!-- Body -->
    <div class="p-6">
      <!-- Date range -->
      <div class="mb-6 flex items-center gap-2 text-gray-700">
        <i class="pi pi-calendar text-green-600" />
        <span class="font-medium">
          {{ formatDate(edition.startDate) }} — {{ formatDate(edition.endDate) }}
        </span>
        <span class="text-sm text-gray-500">({{ edition.year }})</span>
      </div>

      <!-- Pricing grid -->
      <div class="mb-6">
        <h3 class="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">Precios</h3>
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div class="rounded-md bg-gray-50 p-3 text-center">
            <p class="text-xs text-gray-500">Adulto</p>
            <p class="text-lg font-bold text-gray-900">{{ formatCurrency(edition.pricePerAdult) }}</p>
          </div>
          <div class="rounded-md bg-gray-50 p-3 text-center">
            <p class="text-xs text-gray-500">Niño</p>
            <p class="text-lg font-bold text-gray-900">{{ formatCurrency(edition.pricePerChild) }}</p>
          </div>
          <div class="rounded-md bg-gray-50 p-3 text-center">
            <p class="text-xs text-gray-500">Bebé</p>
            <p class="text-lg font-bold text-gray-900">{{ formatCurrency(edition.pricePerBaby) }}</p>
          </div>
        </div>
      </div>

      <!-- Capacity & registrations -->
      <div class="mb-4 flex flex-wrap gap-6 text-sm">
        <div v-if="edition.maxCapacity" class="flex items-center gap-1.5 text-gray-600">
          <i class="pi pi-users" />
          <span>Capacidad máxima: <strong>{{ edition.maxCapacity }}</strong></span>
        </div>
        <div class="flex items-center gap-1.5 text-gray-600">
          <i class="pi pi-check-circle" />
          <span>Inscripciones: <strong>{{ edition.registrationCount }}</strong></span>
        </div>
      </div>

      <!-- Notes -->
      <div v-if="edition.notes" class="rounded-md border border-gray-200 bg-gray-50 p-3">
        <p class="text-sm text-gray-700">{{ edition.notes }}</p>
      </div>
    </div>
  </div>
</template>

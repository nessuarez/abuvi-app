<script setup lang="ts">
import { computed } from 'vue'
import Badge from 'primevue/badge'
import type { CampEditionExtra } from '@/types/camp-edition'

const props = defineProps<{
  extras: CampEditionExtra[]
}>()

const sortedExtras = computed(() =>
  [...props.extras].sort((a, b) => a.sortOrder - b.sortOrder)
)

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const pricingLabel = (extra: CampEditionExtra): string => {
  const type = extra.pricingType === 'PerPerson' ? 'por persona' : 'por familia'
  const period = extra.pricingPeriod === 'PerDay' ? '/ día' : '(pago único)'
  return `${type} ${period}`
}
</script>

<template>
  <section class="rounded-lg border border-gray-200 bg-white p-6">
    <h2 class="mb-4 text-lg font-semibold text-gray-900">Servicios adicionales</h2>
    <ul class="space-y-4">
      <li
        v-for="extra in sortedExtras"
        :key="extra.id"
        class="flex items-start justify-between gap-4 rounded-md border border-gray-100 bg-gray-50 p-4"
        :data-testid="`extra-item-${extra.id}`"
      >
        <div class="flex-1 min-w-0">
          <div class="flex flex-wrap items-center gap-2">
            <span class="font-medium text-gray-900">{{ extra.name }}</span>
            <Badge
              v-if="extra.isRequired"
              value="Obligatorio"
              severity="danger"
              class="text-xs"
            />
          </div>
          <p v-if="extra.description" class="mt-1 text-sm text-gray-600">
            {{ extra.description }}
          </p>
        </div>
        <div class="shrink-0 text-right">
          <p class="font-semibold text-gray-900">{{ formatCurrency(extra.price) }}</p>
          <p class="text-xs text-gray-500">{{ pricingLabel(extra) }}</p>
        </div>
      </li>
    </ul>
  </section>
</template>

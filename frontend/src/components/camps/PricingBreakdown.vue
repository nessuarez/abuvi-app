<script setup lang="ts">
import { computed } from 'vue'
import type { AgeRangeSettings } from '@/types/association-settings'

interface Props {
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  ageRanges: AgeRangeSettings
}

const props = defineProps<Props>()

const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('es-ES', {
    style: 'currency',
    currency: 'EUR'
  }).format(amount)
}

const adultLabel = computed(() => `Adulto (${props.ageRanges.adultMinAge}+ años)`)
const childLabel = computed(
  () => `Niño (${props.ageRanges.childMinAge}-${props.ageRanges.childMaxAge} años)`
)
const babyLabel = computed(() => `Bebé (0-${props.ageRanges.babyMaxAge} años)`)
</script>

<template>
  <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
    <div class="rounded border border-gray-200 bg-white p-3">
      <p class="text-xs text-gray-500">{{ adultLabel }}</p>
      <p class="text-lg font-semibold text-gray-900">{{ formatCurrency(pricePerAdult) }}</p>
    </div>
    <div class="rounded border border-gray-200 bg-white p-3">
      <p class="text-xs text-gray-500">{{ childLabel }}</p>
      <p class="text-lg font-semibold text-gray-900">{{ formatCurrency(pricePerChild) }}</p>
    </div>
    <div class="rounded border border-gray-200 bg-white p-3">
      <p class="text-xs text-gray-500">{{ babyLabel }}</p>
      <p class="text-lg font-semibold text-gray-900">{{ formatCurrency(pricePerBaby) }}</p>
    </div>
  </div>
</template>

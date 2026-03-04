<script setup lang="ts">
import { computed } from 'vue'
import Select from 'primevue/select'
import type { CampEditionAccommodation, AccommodationType } from '@/types/camp-edition'
import type { WizardAccommodationPreference } from '@/types/registration'

const props = defineProps<{
  accommodations: CampEditionAccommodation[]
  modelValue: WizardAccommodationPreference[]
}>()

const emit = defineEmits<{
  'update:modelValue': [selections: WizardAccommodationPreference[]]
}>()

const ACCOMMODATION_TYPE_LABELS: Record<AccommodationType, string> = {
  Lodge: 'Refugio',
  Caravan: 'Caravana',
  Tent: 'Tienda de campaña',
  Bungalow: 'Bungalow',
  Motorhome: 'Autocaravana'
}

const activeAccommodations = computed(() =>
  props.accommodations.filter((a) => a.isActive).sort((a, b) => a.sortOrder - b.sortOrder)
)

const getSelectedId = (order: number): string | null => {
  const found = props.modelValue.find((p) => p.preferenceOrder === order)
  return found?.campEditionAccommodationId ?? null
}

const selectedIds = computed(() => props.modelValue.map((p) => p.campEditionAccommodationId))

const optionsForOrder = (order: number) =>
  activeAccommodations.value.filter(
    (a) => !selectedIds.value.includes(a.id) || getSelectedId(order) === a.id
  )

const optionLabel = (acc: CampEditionAccommodation): string => {
  const type = ACCOMMODATION_TYPE_LABELS[acc.accommodationType]
  const capacity = acc.capacity ? ` (${acc.capacity} plazas)` : ''
  return `${acc.name} — ${type}${capacity}`
}

const handleSelect = (order: number, accommodationId: string | null) => {
  let updated = props.modelValue.filter((p) => p.preferenceOrder !== order)

  // If clearing a higher preference, clear lower ones too
  if (!accommodationId) {
    updated = updated.filter((p) => p.preferenceOrder < order)
  } else {
    const acc = activeAccommodations.value.find((a) => a.id === accommodationId)
    if (acc) {
      updated.push({
        campEditionAccommodationId: acc.id,
        accommodationName: acc.name,
        accommodationType: acc.accommodationType,
        preferenceOrder: order
      })
    }
  }

  emit('update:modelValue', updated.sort((a, b) => a.preferenceOrder - b.preferenceOrder))
}

const has1st = computed(() => getSelectedId(1) !== null)
const has2nd = computed(() => getSelectedId(2) !== null)
</script>

<template>
  <div class="space-y-4">
    <template v-if="activeAccommodations.length > 0">
      <!-- 1st preference -->
      <div data-testid="accommodation-pref-1">
        <label class="mb-1 block text-sm font-medium text-gray-700">1ª preferencia</label>
        <Select
          :model-value="getSelectedId(1)"
          :options="optionsForOrder(1)"
          option-value="id"
          :option-label="(opt: CampEditionAccommodation) => optionLabel(opt)"
          placeholder="Selecciona tu primera opción..."
          show-clear
          class="w-full"
          @update:model-value="handleSelect(1, $event)"
        />
      </div>

      <!-- 2nd preference -->
      <div v-if="has1st" data-testid="accommodation-pref-2">
        <label class="mb-1 block text-sm font-medium text-gray-700">
          2ª preferencia
          <span class="font-normal text-gray-400">(opcional)</span>
        </label>
        <Select
          :model-value="getSelectedId(2)"
          :options="optionsForOrder(2)"
          option-value="id"
          :option-label="(opt: CampEditionAccommodation) => optionLabel(opt)"
          placeholder="Selecciona tu segunda opción..."
          show-clear
          class="w-full"
          @update:model-value="handleSelect(2, $event)"
        />
      </div>

      <!-- 3rd preference -->
      <div v-if="has1st && has2nd" data-testid="accommodation-pref-3">
        <label class="mb-1 block text-sm font-medium text-gray-700">
          3ª preferencia
          <span class="font-normal text-gray-400">(opcional)</span>
        </label>
        <Select
          :model-value="getSelectedId(3)"
          :options="optionsForOrder(3)"
          option-value="id"
          :option-label="(opt: CampEditionAccommodation) => optionLabel(opt)"
          placeholder="Selecciona tu tercera opción..."
          show-clear
          class="w-full"
          @update:model-value="handleSelect(3, $event)"
        />
      </div>
    </template>

    <p v-else class="py-4 text-center text-sm text-gray-400">
      No hay opciones de alojamiento disponibles para esta edición.
    </p>
  </div>
</template>

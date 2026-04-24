<script setup lang="ts">
import { computed } from 'vue'
import ProgressBar from 'primevue/progressbar'
import type {
  AssignmentAccommodationResponse,
  AssignmentFamilyResponse
} from '@/types/accommodation-assignment'

const props = defineProps<{
  accommodation: AssignmentAccommodationResponse
  assignedFamilies: AssignmentFamilyResponse[]
  selectedFamily: AssignmentFamilyResponse | null
}>()

defineEmits<{
  (e: 'assign', accommodationId: string): void
  (e: 'unassign', registrationId: string): void
}>()

const occupiedUnits = computed(() => {
  if (props.accommodation.countByFamily) return props.assignedFamilies.length
  return props.assignedFamilies.reduce((sum, f) => sum + f.memberCount, 0)
})

const isOverCapacity = computed(
  () =>
    props.accommodation.capacity !== null &&
    occupiedUnits.value > props.accommodation.capacity
)

const capacityPercent = computed(() => {
  if (!props.accommodation.capacity) return 0
  return Math.min(100, Math.round((occupiedUnits.value / props.accommodation.capacity) * 100))
})

const signalClass = computed(() => {
  if (!props.selectedFamily) return 'border-gray-200'
  const pref = props.selectedFamily.accommodationPreferences.find(
    (p) => p.accommodationId === props.accommodation.id
  )
  if (pref?.preferenceOrder === 1) return 'border-green-400 ring-1 ring-green-300'
  if (pref?.preferenceOrder === 2 || pref?.preferenceOrder === 3)
    return 'border-amber-400 ring-1 ring-amber-300'
  if (isOverCapacity.value) return 'border-red-400 ring-1 ring-red-300'
  return 'border-blue-200'
})
</script>

<template>
  <div
    class="rounded-lg border-2 p-3 transition-all"
    :class="[signalClass, selectedFamily ? 'cursor-pointer hover:shadow-sm' : '']"
    @click="selectedFamily && $emit('assign', accommodation.id)"
  >
    <div class="flex items-center justify-between">
      <span class="text-sm font-semibold text-gray-800">{{ accommodation.name }}</span>
      <span
        class="text-xs"
        :class="isOverCapacity ? 'font-bold text-red-600' : 'text-gray-500'"
      >
        {{ occupiedUnits }} / {{ accommodation.capacity ?? '∞' }}
        {{ accommodation.countByFamily ? 'fam.' : 'pers.' }}
      </span>
    </div>

    <ProgressBar
      v-if="accommodation.capacity"
      :value="capacityPercent"
      class="mt-1"
      style="height: 6px"
      :pt="{
        value: {
          class: isOverCapacity ? '!bg-red-500' : '!bg-primary-500'
        }
      }"
    />

    <div class="mt-2 flex flex-wrap gap-1">
      <div
        v-for="f in assignedFamilies"
        :key="f.registrationId"
        class="flex items-center gap-1 rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-700"
      >
        <span>{{ f.familyName }} ({{ f.memberCount }})</span>
        <button
          class="ml-0.5 text-gray-400 hover:text-red-500"
          title="Quitar asignación"
          @click.stop="$emit('unassign', f.registrationId)"
        >
          ×
        </button>
      </div>
      <span v-if="assignedFamilies.length === 0" class="text-xs italic text-gray-300">
        Vacío
      </span>
    </div>
  </div>
</template>

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
  hasFriendlyFamilyInZone: boolean
}>()

defineEmits<{
  (e: 'assign', accommodationId: string, unitIndex: number | null): void
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

const allFeaturesMatch = computed(() => {
  if (!props.selectedFamily) return false
  const required = props.selectedFamily.requiredFeatures ?? []
  if (required.length === 0) return false
  return required.every((feat) => (props.accommodation.availableFeatures ?? []).includes(feat))
})

const missingFeatures = computed(() => {
  if (!props.selectedFamily) return []
  return (props.selectedFamily.requiredFeatures ?? []).filter(
    (feat) => !(props.accommodation.availableFeatures ?? []).includes(feat)
  )
})

const displayThumbnail = computed(
  () => props.accommodation.primaryThumbnailUrl ?? props.accommodation.zonePrimaryThumbnailUrl ?? null
)
const thumbnailIsZoneFallback = computed(
  () => !props.accommodation.primaryThumbnailUrl && !!props.accommodation.zonePrimaryThumbnailUrl
)

const hasFriendlyFamilyHere = computed(() => {
  if (!props.selectedFamily || (props.selectedFamily.friendlyFamilyUnitIds ?? []).length === 0) return false
  return props.assignedFamilies.some((f) =>
    props.selectedFamily!.friendlyFamilyUnitIds.includes(f.familyUnitId)
  )
})

const canFitSelectedFamily = computed(() => {
  if (!props.selectedFamily || props.accommodation.capacity === null) return true
  const needed = props.accommodation.countByFamily ? 1 : props.selectedFamily.memberCount
  return (props.accommodation.capacity - occupiedUnits.value) >= needed
})

const signalClass = computed(() => {
  if (!props.selectedFamily) return 'border-gray-200'

  // Priority 1 — Red: family does not fit
  const needed = props.accommodation.countByFamily ? 1 : props.selectedFamily.memberCount
  const remaining = props.accommodation.capacity === null
    ? Infinity
    : props.accommodation.capacity - occupiedUnits.value
  if (remaining < needed) return 'border-red-500 ring-1 ring-red-400'

  // Priority 2 — Green: 1st preference OR all features match OR friendly family already here
  const prefs = props.selectedFamily.accommodationPreferences
  const pref = prefs.find((p) => p.accommodationId === props.accommodation.id)
  if (pref?.preferenceOrder === 1 || allFeaturesMatch.value || hasFriendlyFamilyHere.value) {
    return 'border-green-400 ring-1 ring-green-300'
  }

  // Priority 3 — Blue: friendly family in same zone (different accommodation)
  if (props.hasFriendlyFamilyInZone) return 'border-blue-400 ring-1 ring-blue-300'

  // Priority 4 — Amber: 2nd/3rd preference OR missing required features
  if (pref?.preferenceOrder === 2 || pref?.preferenceOrder === 3 || missingFeatures.value.length > 0) {
    return 'border-amber-400 ring-1 ring-amber-300'
  }

  return 'border-blue-200'
})
</script>

<template>
  <div
    class="relative rounded-lg border-2 p-2 transition-all"
    :class="[signalClass, selectedFamily ? 'cursor-pointer hover:shadow-sm' : '']"
    @click="selectedFamily && $emit('assign', accommodation.id, accommodation.unitIndex)"
  >
    <!-- Thumbnail: accommodation photo or zone fallback -->
    <div
      v-if="displayThumbnail"
      class="absolute right-2 top-2 overflow-hidden rounded-md shadow-sm"
      :class="thumbnailIsZoneFallback ? 'h-7 w-7 opacity-60' : 'h-8 w-8'"
    >
      <img
        :src="displayThumbnail"
        alt=""
        class="h-full w-full object-cover"
        @error="($event.target as HTMLImageElement).style.display = 'none'"
      />
      <span
        v-if="thumbnailIsZoneFallback"
        class="absolute bottom-0 left-0 w-full bg-black/40 text-center text-[7px] text-white"
      >
        zona
      </span>
    </div>

    <div class="flex items-center justify-between">
      <span class="text-xs font-semibold text-gray-800">{{ accommodation.name }}</span>
      <span
        class="text-xs"
        :class="isOverCapacity ? 'font-bold text-red-600' : canFitSelectedFamily ? 'text-gray-500' : 'font-medium text-red-500'"
      >
        <template v-if="!selectedFamily || canFitSelectedFamily || isOverCapacity">
          {{ occupiedUnits }} / {{ accommodation.capacity ?? '∞' }}
          {{ accommodation.countByFamily ? 'fam.' : 'pers.' }}
        </template>
        <template v-else>
          Necesitan {{ accommodation.countByFamily ? '1 plaza' : `${selectedFamily.memberCount} pers.` }},
          quedan {{ Math.max(0, (accommodation.capacity ?? 0) - occupiedUnits) }}
        </template>
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

    <!-- Compatibility badges — visible only when a family is selected -->
    <div v-if="selectedFamily" class="mt-1 flex flex-wrap gap-1">
      <span
        v-if="allFeaturesMatch"
        class="rounded bg-green-100 px-1.5 py-0.5 text-xs text-green-700"
        title="El alojamiento tiene todas las características requeridas"
      >
        Cumple todas las preferencias
      </span>

      <span
        v-if="hasFriendlyFamilyHere"
        class="rounded bg-green-100 px-1.5 py-0.5 text-xs text-green-700"
      >
        Familia amiga ya aquí
      </span>

      <span
        v-if="hasFriendlyFamilyInZone && !hasFriendlyFamilyHere"
        class="rounded bg-blue-100 px-1.5 py-0.5 text-xs text-blue-700"
      >
        Familia amiga en misma zona
      </span>

      <span
        v-if="missingFeatures.length > 0"
        class="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-700"
        :title="`Faltan: ${missingFeatures.join(', ')}`"
      >
        Preferencia no cubierta: {{ missingFeatures.join(', ') }}
      </span>
    </div>

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

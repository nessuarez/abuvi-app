<script setup lang="ts">
import type { AssignmentFamilyResponse, AccommodationTypeValue } from '@/types/accommodation-assignment'
import { ACCOMMODATION_TYPE_LABELS, ACCOMMODATION_TYPE_ICONS } from '@/types/accommodation-assignment'

const props = defineProps<{
  family: AssignmentFamilyResponse
  assignedAccommodationName: string | null
  isSelected: boolean
  accommodationTypeMap: Map<string, AccommodationTypeValue>
}>()

defineEmits<{
  (e: 'select', registrationId: string): void
}>()

function prefTypeLabel(accommodationId: string): string {
  const type = props.accommodationTypeMap.get(accommodationId)
  return type ? ACCOMMODATION_TYPE_LABELS[type] : '?'
}

function prefTypeIcon(accommodationId: string): string {
  const type = props.accommodationTypeMap.get(accommodationId)
  return type ? ACCOMMODATION_TYPE_ICONS[type] : 'pi pi-question'
}
</script>

<template>
  <div
    class="cursor-pointer rounded-lg border p-3 transition-colors"
    :class="
      isSelected
        ? 'border-primary-500 bg-primary-50'
        : 'border-gray-200 bg-white hover:border-gray-300'
    "
    @click="$emit('select', family.registrationId)"
  >
    <div class="flex items-center justify-between">
      <span class="text-sm font-medium text-gray-900">{{ family.familyName }}</span>
      <span
        class="inline-flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full bg-primary-500 text-xs font-bold text-white"
        v-tooltip.top="`${family.memberCount} personas`"
      >
        {{ family.memberCount }}
      </span>
    </div>

    <p class="mt-0.5 text-xs text-gray-500">{{ family.representativeName }}</p>

    <div class="mt-1 flex flex-wrap items-center gap-1">
      <span
        v-if="family.hasPet"
        class="rounded-full border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[10px] text-amber-700"
      >
        Con mascotas
      </span>
      <i
        v-if="family.specialNeeds"
        class="pi pi-exclamation-circle text-xs text-red-400"
        title="Necesidades especiales"
      />
      <span
        v-for="pref in family.accommodationPreferences"
        :key="pref.preferenceOrder"
        class="inline-flex items-center gap-0.5 rounded-full border border-gray-200 bg-gray-50 px-1.5 py-0.5"
      >
        <span class="text-[10px] font-medium text-gray-500">{{ pref.preferenceOrder }}º</span>
        <i :class="[prefTypeIcon(pref.accommodationId), 'text-[9px] text-gray-400']" />
        <span class="text-[10px] text-gray-400">{{ prefTypeLabel(pref.accommodationId) }}</span>
      </span>
    </div>

    <p
      class="mt-1 text-xs"
      :class="assignedAccommodationName ? 'text-green-600' : 'text-gray-400'"
    >
      {{ assignedAccommodationName ?? 'Sin asignar' }}
    </p>
  </div>
</template>

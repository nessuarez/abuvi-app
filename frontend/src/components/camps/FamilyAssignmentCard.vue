<script setup lang="ts">
import type { AssignmentFamilyResponse } from '@/types/accommodation-assignment'

defineProps<{
  family: AssignmentFamilyResponse
  assignedAccommodationName: string | null
  isSelected: boolean
}>()

defineEmits<{
  (e: 'select', registrationId: string): void
}>()
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
      <span class="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
        {{ family.memberCount }} pers.
      </span>
    </div>

    <p class="mt-0.5 text-xs text-gray-500">{{ family.representativeName }}</p>

    <div class="mt-1 flex flex-wrap items-center gap-1">
      <i
        v-if="family.hasPet"
        class="pi pi-tag text-xs text-amber-500"
        title="Tiene mascota"
      />
      <i
        v-if="family.specialNeeds"
        class="pi pi-exclamation-circle text-xs text-red-400"
        title="Necesidades especiales"
      />
      <span
        v-for="pref in family.accommodationPreferences"
        :key="pref.preferenceOrder"
        class="text-xs text-gray-400"
      >{{ pref.preferenceOrder }}ª</span>
    </div>

    <p
      class="mt-1 text-xs"
      :class="assignedAccommodationName ? 'text-green-600' : 'text-gray-400'"
    >
      {{ assignedAccommodationName ?? 'Sin asignar' }}
    </p>
  </div>
</template>

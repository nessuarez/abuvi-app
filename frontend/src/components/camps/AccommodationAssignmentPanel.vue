<script setup lang="ts">
import { ref, computed } from 'vue'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import FamilyAssignmentCard from './FamilyAssignmentCard.vue'
import AccommodationSlotCard from './AccommodationSlotCard.vue'
import type {
  ProposalAssignmentStateResponse,
  AssignmentFamilyResponse,
  AssignmentAccommodationResponse,
  AccommodationTypeValue,
  ACCOMMODATION_TYPE_LABELS as _labels
} from '@/types/accommodation-assignment'
import { ACCOMMODATION_TYPE_LABELS } from '@/types/accommodation-assignment'

const props = defineProps<{
  state: ProposalAssignmentStateResponse
  assignmentsMap: Map<string, string>
  selectedRegistrationId: string | null
  saving: boolean
}>()

const emit = defineEmits<{
  (e: 'selectFamily', registrationId: string): void
  (e: 'assign', registrationId: string, accommodationId: string): void
  (e: 'unassign', registrationId: string): void
}>()

const searchQuery = ref('')

const sortedFamilies = computed((): AssignmentFamilyResponse[] =>
  [...props.state.families].sort((a, b) => {
    const aAssigned = props.assignmentsMap.has(a.registrationId)
    const bAssigned = props.assignmentsMap.has(b.registrationId)
    if (aAssigned !== bAssigned) return aAssigned ? 1 : -1
    return a.familyName.localeCompare(b.familyName)
  })
)

const filteredFamilies = computed(() => {
  const q = searchQuery.value.toLowerCase()
  if (!q) return sortedFamilies.value
  return sortedFamilies.value.filter(
    (f) =>
      f.familyName.toLowerCase().includes(q) ||
      f.representativeName.toLowerCase().includes(q)
  )
})

const unassignedCount = computed(
  () => props.state.families.filter((f) => !props.assignmentsMap.has(f.registrationId)).length
)

const selectedFamily = computed(
  () =>
    props.state.families.find((f) => f.registrationId === props.selectedRegistrationId) ?? null
)

const accommodationNameMap = computed((): Map<string, string> => {
  const map = new Map<string, string>()
  for (const acc of props.state.accommodations) map.set(acc.id, acc.name)
  return map
})

function assignedAccommodationName(registrationId: string): string | null {
  const accId = props.assignmentsMap.get(registrationId)
  if (!accId) return null
  return accommodationNameMap.value.get(accId) ?? null
}

function assignedFamiliesFor(accId: string): AssignmentFamilyResponse[] {
  return props.state.families.filter((f) => props.assignmentsMap.get(f.registrationId) === accId)
}

// Group: type → zone → accommodations
const groupedAccommodations = computed((): Map<string, Map<string, AssignmentAccommodationResponse[]>> => {
  const byType = new Map<string, Map<string, AssignmentAccommodationResponse[]>>()
  const sorted = [...props.state.accommodations].sort((a, b) => a.sortOrder - b.sortOrder)
  for (const acc of sorted) {
    if (!byType.has(acc.type)) byType.set(acc.type, new Map())
    const byZone = byType.get(acc.type)!
    const zoneKey = acc.zoneName ?? 'Sin zona'
    if (!byZone.has(zoneKey)) byZone.set(zoneKey, [])
    byZone.get(zoneKey)!.push(acc)
  }
  return byType
})

function handleAssign(accId: string) {
  if (props.selectedRegistrationId) {
    emit('assign', props.selectedRegistrationId, accId)
  }
}
</script>

<template>
  <div class="grid h-full overflow-hidden" style="grid-template-columns: 300px 1fr">
    <!-- Left: Family list -->
    <div class="flex flex-col overflow-hidden border-r bg-gray-50">
      <div class="shrink-0 border-b p-3">
        <IconField>
          <InputIcon class="pi pi-search" />
          <InputText
            v-model="searchQuery"
            placeholder="Buscar familia..."
            class="w-full"
            size="small"
          />
        </IconField>
        <p class="mt-1 text-xs text-gray-500">
          {{ unassignedCount }} sin asignar · {{ state.families.length }} total
        </p>
      </div>
      <div class="flex flex-col gap-1 overflow-y-auto p-2">
        <FamilyAssignmentCard
          v-for="family in filteredFamilies"
          :key="family.registrationId"
          :family="family"
          :assigned-accommodation-name="assignedAccommodationName(family.registrationId)"
          :is-selected="family.registrationId === selectedRegistrationId"
          @select="$emit('selectFamily', $event)"
        />
        <p
          v-if="filteredFamilies.length === 0"
          class="py-4 text-center text-xs text-gray-400"
        >
          No se encontraron familias
        </p>
      </div>
    </div>

    <!-- Right: Accommodation grid -->
    <div class="overflow-y-auto p-4">
      <div
        v-if="selectedFamily"
        class="mb-4 rounded-lg border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-700"
      >
        <span class="font-medium">{{ selectedFamily.familyName }}</span> seleccionada —
        haz clic en un alojamiento para asignarla
      </div>

      <div
        v-for="[type, byZone] in groupedAccommodations"
        :key="type"
        class="mb-6"
      >
        <h3 class="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">
          {{ ACCOMMODATION_TYPE_LABELS[type as AccommodationTypeValue] }}
        </h3>
        <div v-for="[zoneName, accommodations] in byZone" :key="zoneName" class="mb-4">
          <h4 class="mb-2 text-xs font-medium text-gray-400">{{ zoneName }}</h4>
          <div class="grid grid-cols-2 gap-2 lg:grid-cols-3 xl:grid-cols-4">
            <AccommodationSlotCard
              v-for="acc in accommodations"
              :key="acc.id"
              :accommodation="acc"
              :assigned-families="assignedFamiliesFor(acc.id)"
              :selected-family="selectedFamily"
              @assign="handleAssign"
              @unassign="$emit('unassign', $event)"
            />
          </div>
        </div>
      </div>

      <p
        v-if="state.accommodations.length === 0"
        class="py-8 text-center text-sm text-gray-400"
      >
        No hay alojamientos configurados para esta edición.
      </p>
    </div>
  </div>
</template>

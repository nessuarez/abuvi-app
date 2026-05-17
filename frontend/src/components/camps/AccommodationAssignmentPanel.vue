<script setup lang="ts">
import { ref, computed } from 'vue'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import Dialog from 'primevue/dialog'
import Galleria from 'primevue/galleria'
import ProgressSpinner from 'primevue/progressspinner'
import FamilyAssignmentCard from './FamilyAssignmentCard.vue'
import AccommodationSlotCard from './AccommodationSlotCard.vue'
import { api } from '@/utils/api'
import type {
  ProposalAssignmentStateResponse,
  AssignmentFamilyResponse,
  AssignmentAccommodationResponse,
  AccommodationTypeValue
} from '@/types/accommodation-assignment'
import { ACCOMMODATION_TYPE_LABELS, ACCOMMODATION_TYPE_ICONS } from '@/types/accommodation-assignment'
import type { MediaItem } from '@/types/media-item'

const props = defineProps<{
  state: ProposalAssignmentStateResponse
  assignmentsMap: Map<string, { accommodationId: string; unitIndex: number | null }>
  selectedRegistrationId: string | null
  saving: boolean
  campEditionId: string
}>()

const emit = defineEmits<{
  (e: 'selectFamily', registrationId: string): void
  (e: 'assign', registrationId: string, accommodationId: string, unitIndex: number | null): void
  (e: 'unassign', registrationId: string): void
}>()

const searchQuery = ref('')
const filterSpecialNeeds = ref(false)
const activeTypeFilter = ref<AccommodationTypeValue | null>(null)
const filterZone = ref<string | null>(null)
const filterOnlyAvailable = ref(false)

// Zone gallery modal
const zoneGalleryVisible = ref(false)
const zoneGalleryTitle = ref('')
const zoneGalleryImages = ref<MediaItem[]>([])
const zoneGalleryLoading = ref(false)

async function openZoneGallery(zoneId: string, zoneName: string): Promise<void> {
  zoneGalleryTitle.value = zoneName
  zoneGalleryVisible.value = true
  zoneGalleryLoading.value = true
  zoneGalleryImages.value = []
  try {
    const res = await api.get(
      `/camps/editions/${props.campEditionId}/accommodation-zones/${zoneId}`
    )
    zoneGalleryImages.value = res.data.data?.mediaItems ?? []
  } catch {
    // silently fail — gallery shows empty state
  } finally {
    zoneGalleryLoading.value = false
  }
}

const availableTypes = computed((): AccommodationTypeValue[] =>
  [...new Set(props.state.accommodations.map((a) => a.type))] as AccommodationTypeValue[]
)

const accommodationTypeMap = computed((): Map<string, AccommodationTypeValue> => {
  const map = new Map<string, AccommodationTypeValue>()
  props.state.accommodations.forEach((a) => map.set(a.id, a.type))
  return map
})

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
  return sortedFamilies.value.filter((f) => {
    const matchesSearch =
      !q ||
      f.familyName.toLowerCase().includes(q) ||
      f.representativeName.toLowerCase().includes(q)
    const matchesSpecialNeeds = !filterSpecialNeeds.value || f.hasSpecialNeeds
    return matchesSearch && matchesSpecialNeeds
  })
})

const unassignedCount = computed(
  () => props.state.families.filter((f) => !props.assignmentsMap.has(f.registrationId)).length
)

const selectedFamily = computed(
  () =>
    props.state.families.find((f) => f.registrationId === props.selectedRegistrationId) ?? null
)

const friendlyFamilyInZoneMap = computed((): Map<string, boolean> => {
  const map = new Map<string, boolean>()
  if (!selectedFamily.value || (selectedFamily.value.friendlyFamilyUnitIds ?? []).length === 0) {
    props.state.accommodations.forEach((acc) => map.set(slotKey(acc), false))
    return map
  }

  const zoneToSlotKeys = new Map<string | null, string[]>()
  for (const acc of props.state.accommodations) {
    const zoneKey = acc.zoneId ?? null
    if (!zoneToSlotKeys.has(zoneKey)) zoneToSlotKeys.set(zoneKey, [])
    zoneToSlotKeys.get(zoneKey)!.push(slotKey(acc))
  }

  for (const acc of props.state.accommodations) {
    const zoneKey = acc.zoneId ?? null
    const currentKey = slotKey(acc)
    const sameZoneSlotKeys = (zoneToSlotKeys.get(zoneKey) ?? []).filter((k) => k !== currentKey)

    const hasFriendlyInZone = sameZoneSlotKeys.some((k) => {
      const slotAcc = props.state.accommodations.find((a) => slotKey(a) === k)
      if (!slotAcc) return false
      const familiesHere = assignedFamiliesFor(slotAcc)
      return familiesHere.some((f) =>
        selectedFamily.value!.friendlyFamilyUnitIds.includes(f.familyUnitId)
      )
    })

    map.set(currentKey, hasFriendlyInZone)
  }
  return map
})

const accommodationNameMap = computed((): Map<string, string> => {
  const map = new Map<string, string>()
  for (const acc of props.state.accommodations) map.set(acc.id, acc.name)
  return map
})

function slotKey(acc: AssignmentAccommodationResponse): string {
  return `${acc.id}|${acc.unitIndex ?? ''}`
}

function assignedAccommodationName(registrationId: string): string | null {
  const assignment = props.assignmentsMap.get(registrationId)
  if (!assignment) return null
  return accommodationNameMap.value.get(assignment.accommodationId) ?? null
}

function assignedFamiliesFor(acc: AssignmentAccommodationResponse): AssignmentFamilyResponse[] {
  return props.state.families.filter((f) => {
    const a = props.assignmentsMap.get(f.registrationId)
    return a?.accommodationId === acc.id && a?.unitIndex === acc.unitIndex
  })
}

const availableZoneOptions = computed(() => {
  const zones = [
    ...new Set(
      props.state.accommodations.map((a) => a.zoneName).filter((z): z is string => z !== null)
    ),
  ]
  return zones.map((z) => ({ label: z, value: z }))
})

const groupedAccommodations = computed((): Map<string, Map<string, AssignmentAccommodationResponse[]>> => {
  const byType = new Map<string, Map<string, AssignmentAccommodationResponse[]>>()
  const sorted = [...props.state.accommodations].sort((a, b) => a.sortOrder - b.sortOrder)

  for (const acc of sorted) {
    if (activeTypeFilter.value && acc.type !== activeTypeFilter.value) continue
    if (filterZone.value && acc.zoneName !== filterZone.value) continue
    if (filterOnlyAvailable.value) {
      const families = assignedFamiliesFor(acc)
      const used = acc.countByFamily
        ? families.length
        : families.reduce((sum, f) => sum + f.memberCount, 0)
      if (acc.capacity !== null && used >= acc.capacity) continue
    }

    if (!byType.has(acc.type)) byType.set(acc.type, new Map())
    const byZone = byType.get(acc.type)!
    const zoneKey = acc.zoneName ?? 'Sin zona'
    if (!byZone.has(zoneKey)) byZone.set(zoneKey, [])
    byZone.get(zoneKey)!.push(acc)
  }
  return byType
})

function handleAssign(accId: string, unitIndex: number | null) {
  if (props.selectedRegistrationId) {
    emit('assign', props.selectedRegistrationId, accId, unitIndex)
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
        <div class="mt-2 flex items-center gap-2">
          <ToggleSwitch v-model="filterSpecialNeeds" input-id="filter-special-needs" size="small" />
          <label for="filter-special-needs" class="cursor-pointer text-xs text-gray-600">
            Solo con necesidades especiales
          </label>
        </div>
      </div>
      <div class="flex flex-col gap-1 overflow-y-auto p-2">
        <FamilyAssignmentCard
          v-for="family in filteredFamilies"
          :key="family.registrationId"
          :family="family"
          :assigned-accommodation-name="assignedAccommodationName(family.registrationId)"
          :is-selected="family.registrationId === selectedRegistrationId"
          :accommodation-type-map="accommodationTypeMap"
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

      <!-- Accommodation filter bar -->
      <div class="mb-4 space-y-2">
        <!-- Type filter chips -->
        <div class="flex flex-wrap gap-1.5">
          <button
            class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors"
            :class="activeTypeFilter === null
              ? 'border-primary-500 bg-primary-500 text-white'
              : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'"
            @click="activeTypeFilter = null"
          >
            Todos
          </button>
          <button
            v-for="type in availableTypes"
            :key="type"
            class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors"
            :class="activeTypeFilter === type
              ? 'border-primary-500 bg-primary-500 text-white'
              : 'border-gray-300 bg-white text-gray-600 hover:border-gray-400'"
            @click="activeTypeFilter = activeTypeFilter === type ? null : type"
          >
            <i :class="ACCOMMODATION_TYPE_ICONS[type]" />
            {{ ACCOMMODATION_TYPE_LABELS[type] }}
          </button>
        </div>

        <!-- Zone + availability filters -->
        <div class="flex flex-wrap items-center gap-2">
          <Select
            v-model="filterZone"
            :options="availableZoneOptions"
            option-label="label"
            option-value="value"
            placeholder="Todas las zonas"
            show-clear
            class="w-44"
            size="small"
          />
          <div class="flex items-center gap-1.5">
            <ToggleSwitch v-model="filterOnlyAvailable" input-id="filter-available" size="small" />
            <label for="filter-available" class="cursor-pointer text-xs text-gray-600">
              Solo disponibles
            </label>
          </div>
        </div>
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
          <!-- Zone header -->
          <div class="mb-2 flex items-center gap-2">
            <template v-if="accommodations[0]?.zoneId">
              <img
                v-if="accommodations[0]?.zonePrimaryThumbnailUrl"
                :src="accommodations[0].zonePrimaryThumbnailUrl"
                alt=""
                class="h-7 w-10 cursor-pointer flex-shrink-0 rounded object-cover shadow-sm hover:opacity-80"
                @click="openZoneGallery(accommodations[0].zoneId!, zoneName)"
              />
              <div
                v-else
                class="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded bg-gray-100 text-gray-400"
              >
                <i class="pi pi-image" style="font-size: 0.6rem" />
              </div>
            </template>
            <h4 class="text-xs font-medium text-gray-400">{{ zoneName }}</h4>
            <button
              v-if="accommodations[0]?.zoneId"
              class="ml-auto flex items-center gap-1 text-[10px] text-gray-400 hover:text-primary-500"
              @click="openZoneGallery(accommodations[0].zoneId!, zoneName)"
            >
              <i class="pi pi-images text-[10px]" />
              ver fotos
            </button>
          </div>
          <div class="grid grid-cols-3 gap-1.5 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6">
            <AccommodationSlotCard
              v-for="acc in accommodations"
              :key="`${acc.id}-${acc.unitIndex ?? 'null'}`"
              :accommodation="acc"
              :assigned-families="assignedFamiliesFor(acc)"
              :selected-family="selectedFamily"
              :has-friendly-family-in-zone="friendlyFamilyInZoneMap.get(slotKey(acc)) ?? false"
              @assign="(accId, unitIndex) => handleAssign(accId, unitIndex)"
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

  <!-- Zone gallery modal -->
  <Dialog
    v-model:visible="zoneGalleryVisible"
    :header="zoneGalleryTitle"
    modal
    class="w-[90vw] max-w-2xl"
  >
    <div v-if="zoneGalleryLoading" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>
    <Galleria
      v-else-if="zoneGalleryImages.length"
      :value="zoneGalleryImages"
      :num-visible="4"
      :show-thumbnails="true"
      :show-indicators="true"
      class="w-full"
    >
      <template #item="{ item }: { item: MediaItem }">
        <img
          :src="item.fileUrl"
          :alt="item.title"
          class="max-h-96 w-full rounded object-contain"
          @error="($event.target as HTMLImageElement).style.display = 'none'"
        />
      </template>
      <template #thumbnail="{ item }: { item: MediaItem }">
        <img
          :src="item.thumbnailUrl ?? item.fileUrl"
          class="h-12 w-16 rounded object-cover"
          @error="($event.target as HTMLImageElement).style.display = 'none'"
        />
      </template>
    </Galleria>
    <p v-else class="py-6 text-center text-sm text-gray-400">
      Esta zona no tiene fotografías.
    </p>
  </Dialog>
</template>

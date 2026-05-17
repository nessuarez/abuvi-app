<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Dialog from 'primevue/dialog'
import CampEditionAccommodationDialog from './CampEditionAccommodationDialog.vue'
import FeatureAssignmentDialog from './FeatureAssignmentDialog.vue'
import { useCampAccommodations } from '@/composables/useCampAccommodations'
import { useAccommodationFeatureAssignment } from '@/composables/useAccommodationFeatureAssignment'
import type { CampEditionAccommodation, AccommodationType } from '@/types/camp-edition'
import type { AccommodationFeature } from '@/types/accommodation-feature'

const props = defineProps<{
  editionId: string
  availableFeatures: AccommodationFeature[]
}>()

const toast = useToast()
const {
  accommodations,
  loading,
  error,
  fetchAccommodations,
  deleteAccommodation,
  activateAccommodation,
  deactivateAccommodation,
  toggleIsAssignable,
} = useCampAccommodations(props.editionId)
const {
  saving: featureSaving,
  error: featureError,
  setAccommodationFeatures,
} = useAccommodationFeatureAssignment(props.editionId)

const showDialog = ref(false)
const editingAccommodation = ref<CampEditionAccommodation | undefined>(undefined)
const deleteTarget = ref<CampEditionAccommodation | null>(null)
const showDeleteConfirm = ref(false)
const showFeatureDialog = ref(false)
const featureTarget = ref<CampEditionAccommodation | null>(null)

const ACCOMMODATION_TYPE_LABELS: Record<AccommodationType, string> = {
  Lodge: 'Refugio',
  Caravan: 'Caravana',
  Tent: 'Tienda de campaña',
  Bungalow: 'Bungalow',
  Motorhome: 'Autocaravana',
}

const sortedAccommodations = () => [...accommodations.value].sort((a, b) => a.sortOrder - b.sortOrder)

const openCreate = () => {
  editingAccommodation.value = undefined
  showDialog.value = true
}

const openEdit = (acc: CampEditionAccommodation) => {
  editingAccommodation.value = acc
  showDialog.value = true
}

const confirmDelete = (acc: CampEditionAccommodation) => {
  deleteTarget.value = acc
  showDeleteConfirm.value = true
}

const handleDelete = async () => {
  if (!deleteTarget.value) return
  const success = await deleteAccommodation(deleteTarget.value.id)
  showDeleteConfirm.value = false
  if (success) {
    toast.add({ severity: 'success', summary: 'Alojamiento eliminado', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
  deleteTarget.value = null
}

const handleToggleIsAssignable = async (acc: CampEditionAccommodation) => {
  const success = await toggleIsAssignable(acc)
  if (!success) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleToggleActive = async (acc: CampEditionAccommodation) => {
  const success = acc.isActive
    ? await deactivateAccommodation(acc.id)
    : await activateAccommodation(acc.id)
  if (!success) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleSaved = () => {
  fetchAccommodations()
}

const openFeatureAssign = (acc: CampEditionAccommodation) => {
  featureTarget.value = acc
  showFeatureDialog.value = true
}

const handleFeaturesSaved = async (featureIds: string[]) => {
  if (!featureTarget.value) return
  const result = await setAccommodationFeatures(featureTarget.value.id, featureIds)
  if (result) {
    const idx = accommodations.value.findIndex((a) => a.id === featureTarget.value!.id)
    if (idx !== -1) accommodations.value[idx] = { ...accommodations.value[idx], features: result }
    showFeatureDialog.value = false
    featureTarget.value = null
    toast.add({ severity: 'success', summary: 'Características actualizadas', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: featureError.value, life: 5000 })
  }
}

onMounted(() => fetchAccommodations())
</script>

<template>
  <div class="rounded-lg border border-gray-200 bg-white p-6">
    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-lg font-semibold text-gray-900">Alojamientos</h2>
      <Button label="Añadir" icon="pi pi-plus" size="small" @click="openCreate" />
    </div>

    <div v-if="loading && accommodations.length === 0" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>

    <Message v-else-if="error && accommodations.length === 0" severity="error" :closable="false">
      {{ error }}
    </Message>

    <div
      v-else-if="accommodations.length === 0"
      class="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-400"
    >
      No hay alojamientos configurados para esta edición.
    </div>

    <div v-else class="space-y-3">
      <div
        v-for="acc in sortedAccommodations()"
        :key="acc.id"
        class="flex items-center justify-between rounded-lg border border-gray-200 px-4 py-3"
        :class="{ 'opacity-50': !acc.isActive }"
      >
        <div class="flex-1">
          <div class="flex items-center gap-2">
            <span class="text-sm font-medium text-gray-900">{{ acc.name }}</span>
            <span
              v-if="acc.quantity > 1"
              class="inline-flex items-center rounded bg-primary-100 px-1.5 py-0.5 text-xs font-semibold text-primary-700"
              title="Número de unidades de este tipo"
            >
              {{ acc.quantity }}×
            </span>
            <Tag
              :value="ACCOMMODATION_TYPE_LABELS[acc.accommodationType]"
              severity="info"
              class="text-xs"
            />
            <Tag v-if="acc.countByFamily" value="Por unidad" severity="warn" class="text-xs" />
            <Tag v-else value="Por personas" severity="info" class="text-xs" />
            <Tag v-if="!acc.isActive" value="Inactivo" severity="secondary" class="text-xs" />
          </div>
          <div class="mt-1 flex flex-wrap gap-4 text-xs text-gray-500">
            <span v-if="acc.capacity">
              Capacidad: {{ acc.capacity }}<template v-if="acc.quantity > 1"> × {{ acc.quantity }} = {{ acc.capacity * acc.quantity }}</template>
            </span>
            <span>Preferencias: {{ acc.currentPreferenceCount }}</span>
            <span>1ª opción: {{ acc.firstChoiceCount }}</span>
            <span v-if="acc.zoneName" class="rounded bg-gray-100 px-2 py-0.5 text-gray-600">
              {{ acc.zoneName }}
            </span>
          </div>
          <div v-if="(acc.features ?? []).length > 0" class="mt-1.5 flex flex-wrap gap-1">
            <span
              v-for="f in (acc.features ?? []).slice(0, 3)"
              :key="f.id"
              class="inline-flex items-center gap-1 rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600"
            >
              {{ f.icon }} {{ f.name }}
            </span>
            <span v-if="(acc.features ?? []).length > 3" class="text-xs text-gray-400">
              +{{ (acc.features ?? []).length - 3 }} más
            </span>
          </div>
        </div>
        <div class="flex items-center gap-1">
          <div class="flex items-center gap-1 pr-2" title="Visible en tablero de asignación">
            <ToggleSwitch
              :model-value="acc.isAssignable"
              size="small"
              @change="handleToggleIsAssignable(acc)"
            />
            <span class="text-xs text-gray-400">Asignable</span>
          </div>
          <Button
            icon="pi pi-star"
            severity="secondary"
            text
            size="small"
            title="Características"
            @click="openFeatureAssign(acc)"
          />
          <Button
            :icon="acc.isActive ? 'pi pi-eye-slash' : 'pi pi-eye'"
            severity="secondary"
            text
            size="small"
            :title="acc.isActive ? 'Desactivar' : 'Activar'"
            @click="handleToggleActive(acc)"
          />
          <Button
            icon="pi pi-pencil"
            severity="secondary"
            text
            size="small"
            title="Editar"
            @click="openEdit(acc)"
          />
          <Button
            icon="pi pi-trash"
            severity="danger"
            text
            size="small"
            title="Eliminar"
            @click="confirmDelete(acc)"
          />
        </div>
      </div>
    </div>

    <!-- Create/Edit Dialog -->
    <CampEditionAccommodationDialog
      v-model:visible="showDialog"
      :edition-id="editionId"
      :accommodation="editingAccommodation"
      @saved="handleSaved"
    />

    <!-- Feature Assignment Dialog -->
    <FeatureAssignmentDialog
      v-model:visible="showFeatureDialog"
      :title="`Características — ${featureTarget?.name ?? ''}`"
      :initial-feature-ids="(featureTarget?.features ?? []).map((f) => f.id)"
      :available-features="availableFeatures"
      @saved="handleFeaturesSaved"
    />

    <!-- Delete Confirmation -->
    <Dialog
      v-model:visible="showDeleteConfirm"
      header="Eliminar alojamiento"
      modal
      class="w-full max-w-sm"
    >
      <p class="text-sm text-gray-700">
        ¿Eliminar <strong>{{ deleteTarget?.name }}</strong>? Esta acción no se puede deshacer.
      </p>
      <template #footer>
        <div class="flex justify-end gap-2">
          <Button
            label="Cancelar"
            severity="secondary"
            text
            @click="showDeleteConfirm = false"
          />
          <Button label="Eliminar" severity="danger" :loading="loading" @click="handleDelete" />
        </div>
      </template>
    </Dialog>
  </div>
</template>

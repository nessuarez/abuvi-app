<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Dialog from 'primevue/dialog'
import CampEditionAccommodationDialog from './CampEditionAccommodationDialog.vue'
import { useCampAccommodations } from '@/composables/useCampAccommodations'
import type { CampEditionAccommodation, AccommodationType } from '@/types/camp-edition'

const props = defineProps<{
  editionId: string
}>()

const toast = useToast()
const {
  accommodations,
  loading,
  error,
  fetchAccommodations,
  deleteAccommodation,
  activateAccommodation,
  deactivateAccommodation
} = useCampAccommodations(props.editionId)

const showDialog = ref(false)
const editingAccommodation = ref<CampEditionAccommodation | undefined>(undefined)
const deleteTarget = ref<CampEditionAccommodation | null>(null)
const showDeleteConfirm = ref(false)

const ACCOMMODATION_TYPE_LABELS: Record<AccommodationType, string> = {
  Lodge: 'Refugio',
  Caravan: 'Caravana',
  Tent: 'Tienda de campaña',
  Bungalow: 'Bungalow',
  Motorhome: 'Autocaravana'
}

const sortedAccommodations = () =>
  [...accommodations.value].sort((a, b) => a.sortOrder - b.sortOrder)

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
            <Tag
              :value="ACCOMMODATION_TYPE_LABELS[acc.accommodationType]"
              severity="info"
              class="text-xs"
            />
            <Tag v-if="!acc.isActive" value="Inactivo" severity="secondary" class="text-xs" />
          </div>
          <div class="mt-1 flex gap-4 text-xs text-gray-500">
            <span v-if="acc.capacity">
              Capacidad: {{ acc.capacity }}
            </span>
            <span>
              Preferencias: {{ acc.currentPreferenceCount }}
            </span>
            <span>
              1ª opción: {{ acc.firstChoiceCount }}
            </span>
          </div>
        </div>
        <div class="flex items-center gap-1">
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

    <!-- Delete Confirmation -->
    <Dialog
      v-model:visible="showDeleteConfirm"
      header="Eliminar alojamiento"
      modal
      class="w-full max-w-sm"
    >
      <p class="text-sm text-gray-700">
        ¿Eliminar <strong>{{ deleteTarget?.name }}</strong
        >? Esta acción no se puede deshacer.
      </p>
      <template #footer>
        <div class="flex justify-end gap-2">
          <Button
            label="Cancelar"
            severity="secondary"
            text
            @click="showDeleteConfirm = false"
          />
          <Button
            label="Eliminar"
            severity="danger"
            :loading="loading"
            @click="handleDelete"
          />
        </div>
      </template>
    </Dialog>
  </div>
</template>

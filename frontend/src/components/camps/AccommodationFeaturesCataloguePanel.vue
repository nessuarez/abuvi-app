<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import AccommodationFeatureDialog from './AccommodationFeatureDialog.vue'
import { useAccommodationFeatures } from '@/composables/useAccommodationFeatures'
import { FEATURE_APPLICABILITY_LABELS } from '@/types/accommodation-feature'
import type { AccommodationFeature, FeatureApplicabilityLevel } from '@/types/accommodation-feature'

const toast = useToast()
const { features, loading, error, saveError, fetchFeatures, deleteFeature } =
  useAccommodationFeatures()

const showFeatureDialog = ref(false)
const editingFeature = ref<AccommodationFeature | null>(null)
const showDeleteDialog = ref(false)
const deleteTarget = ref<AccommodationFeature | null>(null)

const LEVEL_SEVERITY: Record<FeatureApplicabilityLevel, string> = {
  Zone: 'secondary',
  Accommodation: 'info',
  AccommodationType: 'warn',
  Any: 'success',
}

function openCreate() {
  editingFeature.value = null
  showFeatureDialog.value = true
}

function openEdit(feature: AccommodationFeature) {
  editingFeature.value = feature
  showFeatureDialog.value = true
}

function openDeleteConfirm(feature: AccommodationFeature) {
  deleteTarget.value = feature
  showDeleteDialog.value = true
}

async function handleDelete() {
  if (!deleteTarget.value) return
  const success = await deleteFeature(deleteTarget.value.id)
  showDeleteDialog.value = false
  if (success) {
    toast.add({ severity: 'success', summary: 'Característica eliminada', life: 3000 })
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: saveError.value ?? 'Error al eliminar',
      life: 6000,
    })
  }
  deleteTarget.value = null
}

function handleFeatureSaved() {
  fetchFeatures()
}

onMounted(() => fetchFeatures())
</script>

<template>
  <div class="rounded-lg border border-gray-200 bg-white p-6">
    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-lg font-semibold text-gray-900">Catálogo de características</h2>
      <Button label="Nueva característica" icon="pi pi-plus" size="small" @click="openCreate" />
    </div>

    <div v-if="loading && features.length === 0" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>

    <Message v-else-if="error && features.length === 0" severity="error" :closable="false">
      {{ error }}
    </Message>

    <div
      v-else-if="features.length === 0"
      class="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-400"
    >
      No hay características configuradas. Crea la primera haciendo clic en "Nueva característica".
    </div>

    <DataTable v-else :value="features" :loading="loading" class="text-sm">
      <Column header="Icono" style="width: 5rem">
        <template #body="{ data }">
          <span class="text-xl">{{ data.icon }}</span>
        </template>
      </Column>
      <Column field="name" header="Nombre" />
      <Column header="Nivel">
        <template #body="{ data }">
          <Tag
            :value="FEATURE_APPLICABILITY_LABELS[data.applicabilityLevel as FeatureApplicabilityLevel]"
            :severity="LEVEL_SEVERITY[data.applicabilityLevel as FeatureApplicabilityLevel]"
          />
        </template>
      </Column>
      <Column header="Estado" style="width: 7rem">
        <template #body="{ data }">
          <Tag
            :value="data.isActive ? 'Activo' : 'Inactivo'"
            :severity="data.isActive ? 'success' : 'danger'"
          />
        </template>
      </Column>
      <Column field="sortOrder" header="Orden" style="width: 6rem" />
      <Column header="Acciones" style="width: 8rem">
        <template #body="{ data }">
          <div class="flex gap-1">
            <Button
              icon="pi pi-pencil"
              size="small"
              text
              severity="secondary"
              title="Editar"
              @click="openEdit(data)"
            />
            <Button
              icon="pi pi-trash"
              size="small"
              text
              severity="danger"
              title="Eliminar"
              @click="openDeleteConfirm(data)"
            />
          </div>
        </template>
      </Column>
    </DataTable>

    <!-- Create/Edit Dialog -->
    <AccommodationFeatureDialog
      v-model:visible="showFeatureDialog"
      :feature="editingFeature"
      @saved="handleFeatureSaved"
    />

    <!-- Delete Confirmation -->
    <Dialog
      v-model:visible="showDeleteDialog"
      header="Eliminar característica"
      modal
      class="w-full max-w-sm"
    >
      <p class="text-sm text-gray-700">
        ¿Eliminar <strong>{{ deleteTarget?.name }}</strong>? Esta acción no se puede deshacer.
      </p>
      <template #footer>
        <div class="flex justify-end gap-2">
          <Button label="Cancelar" severity="secondary" text @click="showDeleteDialog = false" />
          <Button label="Eliminar" severity="danger" :loading="loading" @click="handleDelete" />
        </div>
      </template>
    </Dialog>
  </div>
</template>

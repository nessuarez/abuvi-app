<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import ConfirmDialog from 'primevue/confirmdialog'
import Toast from 'primevue/toast'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import CampLocationCard from '@/components/camps/CampLocationCard.vue'
import CampLocationForm from '@/components/camps/CampLocationForm.vue'
import CampLocationMap from '@/components/camps/CampLocationMap.vue'
import { useCamps } from '@/composables/useCamps'
import type { Camp, CreateCampRequest, CampStatus } from '@/types/camp'

const router = useRouter()
const toast = useToast()
const confirm = useConfirm()

const { camps, loading, error, fetchCamps, createCamp, updateCamp, deleteCamp } = useCamps()

const showCreateDialog = ref(false)
const showEditDialog = ref(false)
const selectedCamp = ref<Camp | null>(null)
const searchQuery = ref('')
const selectedStatus = ref<CampStatus | null>(null)
const viewMode = ref<'table' | 'cards' | 'map'>('table')

const statusOptions = [
  { label: 'Todos', value: null },
  { label: 'Activo', value: 'Active' },
  { label: 'Inactivo', value: 'Inactive' },
  { label: 'Archivo Histórico', value: 'HistoricalArchive' }
]

const filteredCamps = computed(() => {
  let result = camps.value || []

  // Filter by search query
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(
      (camp) =>
        camp.name.toLowerCase().includes(query) ||
        camp.description.toLowerCase().includes(query)
    )
  }

  // Filter by status
  if (selectedStatus.value) {
    result = result.filter((camp) => camp.status === selectedStatus.value)
  }

  return result
})

const campLocations = computed(() => {
  return filteredCamps.value.map((camp) => ({
    latitude: camp.latitude,
    longitude: camp.longitude,
    name: camp.name,
    year: undefined
  }))
})

const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('es-ES', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0
  }).format(amount)
}

const statusLabel = (status: string): string => {
  const labels: Record<string, string> = {
    Active: 'Activo',
    Inactive: 'Inactivo',
    HistoricalArchive: 'Archivo Histórico'
  }
  return labels[status] || status
}

onMounted(() => {
  fetchCamps()
})

const handleCreate = () => {
  selectedCamp.value = null
  showCreateDialog.value = true
}

const handleEdit = (camp: Camp) => {
  selectedCamp.value = camp
  showEditDialog.value = true
}

const handleDelete = (camp: Camp) => {
  confirm.require({
    message: `¿Estás seguro de que quieres eliminar "${camp.name}"?`,
    header: 'Confirmar Eliminación',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Sí, eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const success = await deleteCamp(camp.id)
      if (success) {
        toast.add({
          severity: 'success',
          summary: 'Éxito',
          detail: 'Campamento eliminado correctamente',
          life: 3000
        })
      } else {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: error.value || 'Error al eliminar campamento',
          life: 5000
        })
      }
    }
  })
}

const handleViewDetails = (camp: Camp) => {
  router.push({ name: 'camp-location-detail', params: { id: camp.id } })
}

const handleViewEditions = (campId: string) => {
  router.push({ name: 'camp-editions', query: { campId } })
}

const handleSubmitCreate = async (data: CreateCampRequest) => {
  const result = await createCamp(data)
  if (result) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Campamento creado correctamente',
      life: 3000
    })
    showCreateDialog.value = false
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Error al crear campamento',
      life: 5000
    })
  }
}

const handleSubmitEdit = async (data: CreateCampRequest) => {
  if (!selectedCamp.value) return

  const result = await updateCamp(selectedCamp.value.id, { ...data, id: selectedCamp.value.id })
  if (result) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Campamento actualizado correctamente',
      life: 3000
    })
    showEditDialog.value = false
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Error al actualizar campamento',
      life: 5000
    })
  }
}
</script>

<template>
  <div class="container mx-auto p-4">
    <Toast />
    <ConfirmDialog />

    <!-- Header -->
    <div class="mb-6">
      <h1 class="mb-2 text-3xl font-bold text-gray-900">Ubicaciones de Campamento</h1>
      <p class="text-gray-600">Gestiona las ubicaciones de campamento reutilizables</p>
    </div>

    <!-- Toolbar -->
    <div class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <!-- Search and Filter -->
      <div class="flex flex-1 gap-2">
        <span class="p-input-icon-left flex-1">
          <i class="pi pi-search" />
          <InputText
            v-model="searchQuery"
            placeholder="Buscar campamentos..."
            class="w-full"
          />
        </span>
        <Select
          v-model="selectedStatus"
          :options="statusOptions"
          option-label="label"
          option-value="value"
          placeholder="Filtrar por estado"
          class="w-48"
        />
      </div>

      <!-- View Mode & Create Button -->
      <div class="flex gap-2">
        <Button
          :icon="viewMode === 'table' ? 'pi pi-table' : 'pi pi-th-large'"
          :outlined="viewMode !== 'table'"
          @click="viewMode = viewMode === 'table' ? 'cards' : 'table'"
        />
        <Button
          icon="pi pi-map"
          :outlined="viewMode !== 'map'"
          @click="viewMode = 'map'"
        />
        <Button
          label="Nuevo Campamento"
          icon="pi pi-plus"
          @click="handleCreate"
        />
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading && (!camps || camps.length === 0)" class="flex justify-center p-12">
      <ProgressSpinner />
    </div>

    <!-- Error State -->
    <Message v-else-if="error && (!camps || camps.length === 0)" severity="error" :closable="false">
      {{ error }}
      <Button label="Reintentar" text class="ml-2 underline" @click="fetchCamps" />
    </Message>

    <!-- Empty State -->
    <div
      v-else-if="filteredCamps.length === 0"
      class="rounded-lg border-2 border-dashed border-gray-300 p-12 text-center"
    >
      <i class="pi pi-map-marker mb-4 text-4xl text-gray-400"></i>
      <h3 class="mb-2 text-lg font-semibold text-gray-900">No hay campamentos registrados</h3>
      <p class="mb-4 text-gray-600">Comienza creando tu primer campamento</p>
      <Button label="Crear Campamento" icon="pi pi-plus" @click="handleCreate" />
    </div>

    <!-- Table View -->
    <div v-else-if="viewMode === 'table'" class="rounded-lg border border-gray-200 bg-white">
      <DataTable :value="filteredCamps" striped-rows paginator :rows="10">
        <Column field="name" header="Nombre" sortable>
          <template #body="{ data }">
            <div>
              <p class="font-semibold">{{ data.name }}</p>
              <p class="text-sm text-gray-500">
                {{ data.latitude.toFixed(4) }}, {{ data.longitude.toFixed(4) }}
              </p>
            </div>
          </template>
        </Column>

        <Column header="Precios">
          <template #body="{ data }">
            <div class="text-sm">
              <p>Adulto: {{ formatCurrency(data.basePriceAdult) }}</p>
              <p>Niño: {{ formatCurrency(data.basePriceChild) }}</p>
              <p>Bebé: {{ formatCurrency(data.basePriceBaby) }}</p>
            </div>
          </template>
        </Column>

        <Column field="status" header="Estado" sortable>
          <template #body="{ data }">
            <span
              :class="{
                'bg-green-100 text-green-800': data.status === 'Active',
                'bg-gray-100 text-gray-800': data.status === 'Inactive',
                'bg-blue-100 text-blue-800': data.status === 'HistoricalArchive'
              }"
              class="rounded-full px-2 py-1 text-xs font-medium"
            >
              {{ statusLabel(data.status) }}
            </span>
          </template>
        </Column>

        <Column field="editionCount" header="Ediciones">
          <template #body="{ data }">
            <span v-if="data.editionCount !== undefined" class="text-sm">
              {{ data.editionCount }} {{ data.editionCount === 1 ? 'edición' : 'ediciones' }}
            </span>
          </template>
        </Column>

        <Column header="Acciones">
          <template #body="{ data }">
            <div class="flex gap-1">
              <Button
                v-tooltip.top="'Ver detalle'"
                icon="pi pi-eye"
                text
                rounded
                size="small"
                aria-label="Ver detalle de ubicación"
                @click="handleViewDetails(data)"
              />
              <Button
                v-tooltip.top="'Ver ediciones'"
                icon="pi pi-calendar"
                text
                rounded
                size="small"
                aria-label="Ver ediciones de esta ubicación"
                data-testid="view-camp-editions-btn"
                @click="handleViewEditions(data.id)"
              />
              <Button
                v-tooltip.top="'Editar'"
                icon="pi pi-pencil"
                text
                rounded
                size="small"
                aria-label="Editar ubicación"
                @click="handleEdit(data)"
              />
              <Button
                v-tooltip.top="'Eliminar'"
                icon="pi pi-trash"
                text
                rounded
                size="small"
                severity="danger"
                aria-label="Eliminar ubicación"
                @click="handleDelete(data)"
              />
            </div>
          </template>
        </Column>
      </DataTable>
    </div>

    <!-- Cards View -->
    <div v-else-if="viewMode === 'cards'" class="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
      <CampLocationCard
        v-for="camp in filteredCamps"
        :key="camp.id"
        :camp="camp"
        @edit="handleEdit"
        @delete="handleDelete"
        @view-details="handleViewDetails"
      />
    </div>

    <!-- Map View -->
    <div v-else-if="viewMode === 'map'">
      <CampLocationMap :locations="campLocations" />
    </div>

    <!-- Create Dialog -->
    <Dialog
      v-model:visible="showCreateDialog"
      header="Nuevo Campamento"
      modal
      class="w-full max-w-2xl"
    >
      <CampLocationForm mode="create" @submit="handleSubmitCreate" @cancel="showCreateDialog = false" />
    </Dialog>

    <!-- Edit Dialog -->
    <Dialog
      v-model:visible="showEditDialog"
      header="Editar Campamento"
      modal
      class="w-full max-w-2xl"
    >
      <CampLocationForm
        v-if="selectedCamp"
        mode="edit"
        :camp="selectedCamp"
        @submit="handleSubmitEdit"
        @cancel="showEditDialog = false"
      />
    </Dialog>
  </div>
</template>

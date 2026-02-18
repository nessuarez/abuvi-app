<script setup lang="ts">
import { ref, reactive, watch, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import { useDebounceFn } from '@vueuse/core'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import Toast from 'primevue/toast'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Container from '@/components/ui/Container.vue'
import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
import CampEditionStatusDialog from '@/components/camps/CampEditionStatusDialog.vue'
import CampEditionUpdateDialog from '@/components/camps/CampEditionUpdateDialog.vue'
import { useCampEditions } from '@/composables/useCampEditions'
import { useCamps } from '@/composables/useCamps'
import type { CampEdition, CampEditionStatus, CampEditionFilters } from '@/types/camp-edition'

const router = useRouter()
const route = useRoute()
const toast = useToast()

const {
  allEditions,
  loading,
  error,
  fetchAllEditions,
  changeStatus
} = useCampEditions()

const { camps, fetchCamps } = useCamps()

const filters = reactive<{
  year: number | null
  status: CampEditionStatus | null
  campId: string | null
}>({
  year: null,
  status: null,
  campId: null
})

const selectedEdition = ref<CampEdition | null>(null)
const showStatusDialog = ref(false)
const showEditDialog = ref(false)
const statusLoading = ref(false)

const statusOptions: { label: string; value: CampEditionStatus | null }[] = [
  { label: 'Todos los estados', value: null },
  { label: 'Propuesta', value: 'Proposed' },
  { label: 'Borrador', value: 'Draft' },
  { label: 'Abierto', value: 'Open' },
  { label: 'Cerrado', value: 'Closed' },
  { label: 'Completado', value: 'Completed' }
]

const campOptions = computed(() => [
  { label: 'Todas las ubicaciones', value: null },
  ...camps.value.map((c) => ({ label: c.name, value: c.id }))
])

const buildFilters = (): CampEditionFilters => ({
  ...(filters.year ? { year: filters.year } : {}),
  ...(filters.status ? { status: filters.status } : {}),
  ...(filters.campId ? { campId: filters.campId } : {})
})

const debouncedFetch = useDebounceFn(() => {
  fetchAllEditions(buildFilters())
}, 300)

watch(filters, () => debouncedFetch(), { deep: true })

onMounted(() => {
  if (route.query.campId) {
    filters.campId = route.query.campId as string
    router.replace({ name: 'camp-editions' })
  }
  fetchAllEditions(buildFilters())
  fetchCamps()
})

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(new Date(dateStr))

const handleChangeStatus = (edition: CampEdition) => {
  selectedEdition.value = edition
  showStatusDialog.value = true
}

const handleEdit = (edition: CampEdition) => {
  selectedEdition.value = edition
  showEditDialog.value = true
}

const handleViewDetail = (edition: CampEdition) => {
  router.push({ name: 'camp-edition-detail', params: { id: edition.id } })
}

const handleStatusConfirm = async (newStatus: CampEditionStatus) => {
  if (!selectedEdition.value) return
  statusLoading.value = true
  const result = await changeStatus(selectedEdition.value.id, newStatus)
  statusLoading.value = false
  showStatusDialog.value = false

  if (result) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Estado actualizado correctamente',
      life: 3000
    })
    fetchAllEditions(buildFilters())
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Error al cambiar estado',
      life: 5000
    })
  }
}

const handleEditionSaved = (_edition: CampEdition) => {
  toast.add({
    severity: 'success',
    summary: 'Éxito',
    detail: 'Edición actualizada correctamente',
    life: 3000
  })
  fetchAllEditions(buildFilters())
}
</script>

<template>
  <Container>
    <Toast />

    <div class="py-8">
      <!-- Header -->
      <div class="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 class="text-3xl font-bold text-gray-900">Gestión de Ediciones de Campamento</h1>
      </div>

      <!-- Filters -->
      <div class="mb-6 flex flex-wrap gap-3">
        <InputNumber v-model="filters.year" placeholder="Año" :use-grouping="false" :min="2000" :max="2100"
          class="w-32" />
        <Select v-model="filters.status" :options="statusOptions" option-label="label" option-value="value"
          placeholder="Estado" class="w-48" />
        <Select v-model="filters.campId" :options="campOptions" option-label="label" option-value="value"
          placeholder="Ubicación" class="w-56" />
      </div>

      <!-- Loading -->
      <div v-if="loading && allEditions.length === 0" class="flex justify-center py-12">
        <ProgressSpinner />
      </div>

      <!-- Error -->
      <Message v-else-if="error && allEditions.length === 0" severity="error" :closable="false">
        {{ error }}
        <Button label="Reintentar" text class="ml-2" @click="fetchAllEditions(buildFilters())" />
      </Message>

      <!-- Empty -->
      <div v-else-if="!loading && allEditions.length === 0"
        class="rounded-lg border-2 border-dashed border-gray-300 p-12 text-center">
        <i class="pi pi-calendar mb-4 text-4xl text-gray-400" />
        <p class="text-lg font-semibold text-gray-700">No hay ediciones</p>
        <p class="mt-1 text-sm text-gray-500">No se encontraron ediciones con los filtros seleccionados.</p>
      </div>

      <!-- DataTable -->
      <div v-else class="overflow-hidden rounded-lg border border-gray-200 bg-white" data-testid="editions-table">
        <DataTable :value="allEditions" striped-rows paginator :rows="10" :loading="loading">
          <Column header="Ubicación" sortable sort-field="camp.name">
            <template #body="{ data }">
              <span class="font-medium">{{ data.camp?.name ?? '—' }}</span>
            </template>
          </Column>

          <Column field="year" header="Año" sortable class="w-20" />

          <Column header="Fechas">
            <template #body="{ data }">
              <span class="text-sm text-gray-700">
                {{ formatDate(data.startDate) }} — {{ formatDate(data.endDate) }}
              </span>
            </template>
          </Column>

          <Column header="Estado" class="hidden sm:table-cell">
            <template #body="{ data }">
              <CampEditionStatusBadge :status="data.status" size="sm" />
            </template>
          </Column>

          <Column header="Capacidad" class="hidden lg:table-cell">
            <template #body="{ data }">
              <span class="text-sm text-gray-500">
                {{ data.maxCapacity ? `${data.maxCapacity} plazas` : 'Sin límite' }}
              </span>
            </template>
          </Column>

          <Column header="Acciones" class="w-36">
            <template #body="{ data }">
              <div class="flex gap-1">
                <Button v-tooltip.top="'Ver detalle'" icon="pi pi-eye" text rounded size="small"
                  aria-label="Ver detalle de edición" @click="handleViewDetail(data)" />
                <Button v-tooltip.top="'Cambiar estado'" icon="pi pi-sync" text rounded size="small"
                  aria-label="Cambiar estado" :disabled="data.status === 'Completed'" data-testid="change-status-btn"
                  @click="handleChangeStatus(data)" />
                <Button v-tooltip.top="'Editar'" icon="pi pi-pencil" text rounded size="small"
                  aria-label="Editar edición" :disabled="data.status === 'Closed' || data.status === 'Completed'"
                  data-testid="edit-edition-btn" @click="handleEdit(data)" />
              </div>
            </template>
          </Column>
        </DataTable>
      </div>
    </div>

    <!-- Status Dialog -->
    <CampEditionStatusDialog v-if="selectedEdition" v-model:visible="showStatusDialog" :edition="selectedEdition"
      :loading="statusLoading" @confirm="handleStatusConfirm" />

    <!-- Edit Dialog -->
    <CampEditionUpdateDialog v-if="selectedEdition" v-model:visible="showEditDialog" :edition="selectedEdition"
      @saved="handleEditionSaved" />
  </Container>
</template>

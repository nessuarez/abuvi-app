<script setup lang="ts">
import { ref, watch, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useDebounceFn } from '@vueuse/core'
import { useAdminRegistrations } from '@/composables/useAdminRegistrations'
import { useCampEditions } from '@/composables/useCampEditions'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import ColumnGroup from 'primevue/columngroup'
import Row from 'primevue/row'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import type { DataTablePageEvent, DataTableRowClickEvent } from 'primevue/datatable'
import type { RegistrationStatus } from '@/types/registration'

const router = useRouter()

const { registrations, totals, totalCount, pagination, loading, error, fetchAdminRegistrations } =
  useAdminRegistrations()
const { allEditions, loading: editionsLoading, fetchAllEditions } = useCampEditions()

const selectedEditionId = ref<string | null>(null)
const searchQuery = ref('')
const statusFilter = ref<string | null>(null)

const campEditionOptions = computed(() =>
  allEditions.value.map((e) => ({
    label: `${e.name ?? 'Campamento'} ${e.year}`,
    value: e.id
  }))
)

const statusOptions = [
  { label: 'Todos', value: null },
  { label: 'Pendiente', value: 'Pending' },
  { label: 'Confirmada', value: 'Confirmed' },
  { label: 'Cancelada', value: 'Cancelled' },
  { label: 'Borrador', value: 'Draft' }
]

const statusSeverity = (status: RegistrationStatus): string => {
  const map: Record<RegistrationStatus, string> = {
    Pending: 'warn',
    Confirmed: 'success',
    Cancelled: 'danger',
    Draft: 'info'
  }
  return map[status] ?? 'secondary'
}

const statusLabel = (status: RegistrationStatus): string => {
  const map: Record<RegistrationStatus, string> = {
    Pending: 'Pendiente',
    Confirmed: 'Confirmada',
    Cancelled: 'Cancelada',
    Draft: 'Borrador'
  }
  return map[status] ?? status
}

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const formatDate = (dateStr: string): string =>
  new Date(dateStr).toLocaleDateString('es-ES', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })

const loadRegistrations = (page = 1) => {
  if (!selectedEditionId.value) return
  fetchAdminRegistrations(selectedEditionId.value, {
    page,
    pageSize: 20,
    search: searchQuery.value || undefined,
    status: statusFilter.value || undefined
  })
}

const debouncedSearch = useDebounceFn(() => {
  loadRegistrations(1)
}, 300)

watch(selectedEditionId, () => {
  loadRegistrations(1)
})

watch(searchQuery, debouncedSearch)

watch(statusFilter, () => {
  loadRegistrations(1)
})

const onPage = (event: DataTablePageEvent) => {
  loadRegistrations(event.page + 1)
}

const onRowClick = (event: DataTableRowClickEvent) => {
  router.push({ name: 'registration-detail', params: { id: event.data.id } })
}

onMounted(async () => {
  await fetchAllEditions()
  // Default to latest edition
  if (allEditions.value.length > 0) {
    const openEdition = allEditions.value.find((e) => e.status === 'Open')
    selectedEditionId.value = openEdition?.id ?? allEditions.value[0].id
  }
})
</script>

<template>
  <div data-testid="registrations-admin-panel" class="space-y-4">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-xl font-semibold text-gray-800">Inscripciones</h2>
    </div>

    <!-- Camp edition selector -->
    <div class="flex gap-3 flex-wrap items-end">
      <Select
        v-model="selectedEditionId"
        :options="campEditionOptions"
        :loading="editionsLoading"
        optionLabel="label"
        optionValue="value"
        placeholder="Seleccionar edición..."
        class="w-80"
        data-testid="edition-selector"
        aria-label="Seleccionar edición de campamento"
      />
    </div>

    <!-- Filters row -->
    <div v-if="selectedEditionId" class="flex gap-3 flex-wrap">
      <span class="p-input-icon-left">
        <i class="pi pi-search" />
        <InputText
          v-model="searchQuery"
          placeholder="Buscar familia o representante..."
          class="w-64"
          data-testid="search-input"
          aria-label="Buscar por familia o representante"
        />
      </span>
      <Select
        v-model="statusFilter"
        :options="statusOptions"
        optionLabel="label"
        optionValue="value"
        placeholder="Estado"
        class="w-48"
        data-testid="status-filter"
        aria-label="Filtrar por estado"
      />
    </div>

    <!-- Loading state -->
    <div v-if="loading && registrations.length === 0" class="flex justify-center py-12">
      <ProgressSpinner />
    </div>

    <!-- Error state -->
    <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
      {{ error }}
      <Button label="Reintentar" text size="small" class="ml-2" @click="loadRegistrations()" />
    </Message>

    <!-- No edition selected -->
    <div
      v-else-if="!selectedEditionId"
      class="rounded-lg border border-dashed border-gray-300 px-4 py-12 text-center text-sm text-gray-400"
    >
      Selecciona una edición de campamento para ver las inscripciones
    </div>

    <!-- Data Table -->
    <DataTable
      v-else
      :value="registrations"
      lazy
      paginator
      :rows="20"
      :total-records="totalCount"
      striped-rows
      class="rounded-lg cursor-pointer"
      @page="onPage"
      @row-click="onRowClick"
      data-testid="registrations-table"
    >
      <Column field="familyUnit.name" header="Familia">
        <template #body="{ data }">
          <span class="font-medium">{{ data.familyUnit.name }}</span>
        </template>
      </Column>
      <Column header="Representante">
        <template #body="{ data }">
          <span class="text-gray-600">
            {{ data.representative.firstName }} {{ data.representative.lastName }}
          </span>
        </template>
      </Column>
      <Column header="Email">
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ data.representative.email }}</span>
        </template>
      </Column>
      <Column header="Estado">
        <template #body="{ data }">
          <Tag :value="statusLabel(data.status)" :severity="statusSeverity(data.status)" />
        </template>
      </Column>
      <Column field="memberCount" header="Miembros">
        <template #body="{ data }">
          <span class="text-gray-600">{{ data.memberCount }}</span>
        </template>
      </Column>
      <Column header="Total">
        <template #body="{ data }">
          <span class="text-gray-900">{{ formatCurrency(data.totalAmount) }}</span>
        </template>
      </Column>
      <Column header="Pagado">
        <template #body="{ data }">
          <span class="text-green-700">{{ formatCurrency(data.amountPaid) }}</span>
        </template>
      </Column>
      <Column header="Pendiente">
        <template #body="{ data }">
          <span :class="data.amountRemaining > 0 ? 'text-red-600' : 'text-gray-600'">
            {{ formatCurrency(data.amountRemaining) }}
          </span>
        </template>
      </Column>
      <Column header="Creación">
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>

      <!-- Footer totals -->
      <ColumnGroup type="footer">
        <Row>
          <Column
            :footer="`Total: ${totals?.totalRegistrations ?? 0} inscripciones`"
            :colspan="4"
            footerClass="font-semibold text-gray-900"
          />
          <Column
            :footer="String(totals?.totalMembers ?? 0)"
            footerClass="font-semibold text-gray-900"
          />
          <Column
            :footer="formatCurrency(totals?.totalAmount ?? 0)"
            footerClass="font-semibold text-gray-900"
          />
          <Column
            :footer="formatCurrency(totals?.totalPaid ?? 0)"
            footerClass="font-semibold text-green-700"
          />
          <Column
            :footer="formatCurrency(totals?.totalRemaining ?? 0)"
            footerClass="font-semibold text-red-600"
          />
          <Column footer="" />
        </Row>
      </ColumnGroup>
    </DataTable>
  </div>
</template>

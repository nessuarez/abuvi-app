<script setup lang="ts">
import { ref, watch, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useDebounceFn } from '@vueuse/core'
import { useToast } from 'primevue/usetoast'
import { useAdminRegistrations } from '@/composables/useAdminRegistrations'
import { useCampEditions } from '@/composables/useCampEditions'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import ColumnGroup from 'primevue/columngroup'
import Row from 'primevue/row'
import InputText from 'primevue/inputtext'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import Select from 'primevue/select'
import MultiSelect from 'primevue/multiselect'
import Tag from 'primevue/tag'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import type { DataTablePageEvent, DataTableRowClickEvent, DataTableSortEvent } from 'primevue/datatable'
import type { RegistrationStatus, AccommodationPreferenceFilter, AttendancePeriod, AgeCategory } from '@/types/registration'
import type { CampEditionStatus, AccommodationType } from '@/types/camp-edition'
import { formatAttendancePeriods } from '@/utils/registration'

const router = useRouter()
const toast = useToast()

const {
  registrations, totals, totalCount, pagination, loading, error,
  editionExtras, editionAccommodations, filterOptionsLoading,
  exportLoading, exportError,
  fetchAdminRegistrations, fetchEditionFilterOptions, exportToCsv
} = useAdminRegistrations()
const { allEditions, loading: editionsLoading, fetchAllEditions } = useCampEditions()

const selectedEditionId = ref<string | null>(null)
const searchQuery = ref('')
const statusFilter = ref<string | null>(null)
const selectedAccommodationPreferenceKeys = ref<string[]>([])
const selectedExtraIds = ref<string[]>([])
const selectedAttendancePeriods = ref<AttendancePeriod[]>([])
const selectedAgeCategories = ref<AgeCategory[]>([])
const sortField = ref<string>('createdAt')
const sortOrder = ref<1 | -1>(-1)

const campEditionOptions = computed(() =>
  allEditions.value.map((e) => ({
    label: `${e.name ?? 'Campamento'} ${e.year}`,
    value: e.id,
    status: e.status,
  }))
)

const selectedEditionOption = computed(() =>
  campEditionOptions.value.find(o => o.value === selectedEditionId.value) ?? null
)

const statusOptions = [
  { label: 'Todos',          value: null },
  { label: 'Pendiente',      value: 'Pending' },
  { label: 'Al corriente',   value: 'PartiallyPaid' },
  { label: 'Pago completo',  value: 'FullyPaid' },
  { label: 'Confirmada',     value: 'Confirmed' },
  { label: 'En revisión',    value: 'Draft' },
  { label: 'Cancelada',      value: 'Cancelled' },
]

const PREFERENCE_LABELS: Record<1 | 2 | 3, string> = {
  1: '1ª opción',
  2: '2ª opción',
  3: '3ª opción',
}

const accommodationPreferenceOptions = computed(() =>
  ([1, 2, 3] as const).flatMap(order =>
    editionAccommodations.value.map(a => ({
      label: `${PREFERENCE_LABELS[order]}: ${a.name}`,
      value: `${a.id}:${order}`,
    }))
  )
)

const selectedAccommodationPreferences = computed<AccommodationPreferenceFilter[]>(() =>
  selectedAccommodationPreferenceKeys.value.map(key => {
    const colonIndex = key.lastIndexOf(':')
    return {
      accommodationId: key.slice(0, colonIndex),
      preferenceOrder: Number(key.slice(colonIndex + 1)) as 1 | 2 | 3,
    }
  })
)

const attendancePeriodOptions: { label: string; value: AttendancePeriod }[] = [
  { label: 'Campamento completo', value: 'Complete' },
  { label: 'Primera semana',      value: 'FirstWeek' },
  { label: 'Segunda semana',      value: 'SecondWeek' },
  { label: 'Fin de semana',       value: 'WeekendVisit' },
]

const ageCategoryOptions: { label: string; value: AgeCategory }[] = [
  { label: 'Bebés',   value: 'Baby' },
  { label: 'Niños',   value: 'Child' },
  { label: 'Adultos', value: 'Adult' },
]

const extraOptions = computed(() =>
  editionExtras.value.map(e => ({ label: e.name, value: e.id }))
)

const statusSeverity = (status: RegistrationStatus): string => {
  const map: Record<RegistrationStatus, string> = {
    Pending:       'warn',
    PartiallyPaid: 'info',
    FullyPaid:     'secondary',
    Confirmed:     'success',
    Cancelled:     'danger',
    Draft:         'warn',
  }
  return map[status] ?? 'secondary'
}

const statusLabel = (status: RegistrationStatus): string => {
  const map: Record<RegistrationStatus, string> = {
    Pending:       'Pendiente',
    PartiallyPaid: 'Al corriente',
    FullyPaid:     'Pago completo',
    Confirmed:     'Confirmada',
    Cancelled:     'Cancelada',
    Draft:         'En revisión',
  }
  return map[status] ?? status
}

const ACCOMMODATION_ICON: Record<AccommodationType, string> = {
  Lodge: 'pi pi-building',
  Bungalow: 'pi pi-home',
  Tent: 'pi pi-sun',
  Caravan: 'pi pi-car',
  Motorhome: 'pi pi-truck',
}

const ACCOMMODATION_LABEL: Record<AccommodationType, string> = {
  Lodge: 'Albergue',
  Bungalow: 'Bungalow',
  Tent: 'Tienda',
  Caravan: 'Caravana',
  Motorhome: 'Autocaravana',
}

const EDITION_STATUS_LABEL: Record<CampEditionStatus, string> = {
  Proposed: 'Propuesta',
  Draft: 'Borrador',
  Open: 'Abierto',
  Closed: 'Cerrado',
  Completed: 'Completado',
}

const EDITION_STATUS_SEVERITY: Record<CampEditionStatus, string> = {
  Proposed: 'secondary',
  Draft: 'warn',
  Open: 'success',
  Closed: 'danger',
  Completed: 'info',
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
  const apiSortBy = sortField.value === 'familyUnit.name' ? 'familyName' : 'createdAt'
  const apiSortDirection = sortOrder.value === 1 ? 'asc' : 'desc'
  fetchAdminRegistrations(selectedEditionId.value, {
    page,
    pageSize: 20,
    search: searchQuery.value || undefined,
    status: statusFilter.value || undefined,
    accommodationPreferences: selectedAccommodationPreferences.value.length > 0
      ? selectedAccommodationPreferences.value
      : undefined,
    extraIds: selectedExtraIds.value.length > 0 ? selectedExtraIds.value : undefined,
    attendancePeriods: selectedAttendancePeriods.value.length > 0
      ? selectedAttendancePeriods.value
      : undefined,
    ageCategories: selectedAgeCategories.value.length > 0
      ? selectedAgeCategories.value
      : undefined,
    sortBy: apiSortBy,
    sortDirection: apiSortDirection,
  })
}

const debouncedSearch = useDebounceFn(() => {
  loadRegistrations(1)
}, 300)

watch(selectedEditionId, (newId) => {
  searchQuery.value = ''
  statusFilter.value = null
  selectedAccommodationPreferenceKeys.value = []
  selectedExtraIds.value = []
  selectedAttendancePeriods.value = []
  selectedAgeCategories.value = []
  sortField.value = 'createdAt'
  sortOrder.value = -1
  if (newId) fetchEditionFilterOptions(newId)
  loadRegistrations(1)
})

watch(searchQuery, debouncedSearch)

watch(statusFilter, () => {
  loadRegistrations(1)
})

watch(selectedAccommodationPreferenceKeys, () => loadRegistrations(1))
watch(selectedExtraIds, () => loadRegistrations(1))
watch(selectedAttendancePeriods, () => loadRegistrations(1))
watch(selectedAgeCategories, () => loadRegistrations(1))

const onPage = (event: DataTablePageEvent) => {
  loadRegistrations(event.page + 1)
}

const onSort = (event: DataTableSortEvent) => {
  sortField.value = String(event.sortField ?? 'createdAt')
  sortOrder.value = (event.sortOrder as 1 | -1) ?? -1
  loadRegistrations(1)
}

const onRowClick = (event: DataTableRowClickEvent) => {
  router.push({
    name: 'registration-detail',
    params: { id: event.data.id },
    query: { returnTo: 'admin-registrations' },
  })
}

const handleExportCsv = async () => {
  if (!selectedEditionId.value) return
  await exportToCsv(selectedEditionId.value, {
    search: searchQuery.value || undefined,
    status: statusFilter.value || undefined,
    accommodationPreferences: selectedAccommodationPreferences.value.length > 0
      ? selectedAccommodationPreferences.value
      : undefined,
    extraIds: selectedExtraIds.value.length > 0 ? selectedExtraIds.value : undefined,
    attendancePeriods: selectedAttendancePeriods.value.length > 0
      ? selectedAttendancePeriods.value
      : undefined,
    ageCategories: selectedAgeCategories.value.length > 0
      ? selectedAgeCategories.value
      : undefined,
  })
  if (exportError.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: exportError.value, life: 4000 })
  }
}

onMounted(async () => {
  await fetchAllEditions()
  if (allEditions.value.length === 0) return

  const today = new Date().toISOString().slice(0, 10)
  const upcoming = allEditions.value
    .filter(e => (e.status === 'Open' || e.status === 'Draft') && e.startDate >= today)
    .sort((a, b) => a.startDate.localeCompare(b.startDate))

  if (upcoming.length > 0) {
    selectedEditionId.value = upcoming[0].id
  } else {
    const openEdition = allEditions.value.find(e => e.status === 'Open')
    selectedEditionId.value = openEdition?.id ?? allEditions.value[0].id
  }
})
</script>

<template>
  <div data-testid="registrations-admin-panel" class="space-y-4">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-xl font-semibold text-gray-800">Inscripciones</h2>
      <Button
        v-if="selectedEditionId"
        label="Exportar CSV"
        icon="pi pi-download"
        severity="secondary"
        :loading="exportLoading"
        :disabled="exportLoading"
        data-testid="export-csv-btn"
        @click="handleExportCsv"
      />
    </div>

    <!-- Camp edition selector -->
    <div class="flex gap-3 flex-wrap items-end">
      <Select
        v-model="selectedEditionId"
        :options="campEditionOptions"
        :loading="editionsLoading"
        option-label="label"
        option-value="value"
        placeholder="Seleccionar edición..."
        class="w-80"
        data-testid="edition-selector"
        aria-label="Seleccionar edición de campamento"
      >
        <template #option="{ option }">
          <div class="flex items-center gap-2">
            <span>{{ option.label }}</span>
            <Tag
              :value="EDITION_STATUS_LABEL[option.status as CampEditionStatus]"
              :severity="EDITION_STATUS_SEVERITY[option.status as CampEditionStatus]"
              class="text-xs"
            />
          </div>
        </template>
        <template #value="{ value }">
          <div v-if="value && selectedEditionOption" class="flex items-center gap-2">
            <span>{{ selectedEditionOption.label }}</span>
            <Tag
              :value="EDITION_STATUS_LABEL[selectedEditionOption.status as CampEditionStatus]"
              :severity="EDITION_STATUS_SEVERITY[selectedEditionOption.status as CampEditionStatus]"
              class="text-xs"
            />
          </div>
          <span v-else class="text-gray-400">Seleccionar edición...</span>
        </template>
      </Select>
    </div>

    <!-- Filters row -->
    <div v-if="selectedEditionId" class="flex gap-3 flex-wrap">
      <IconField>
        <InputIcon class="pi pi-search" />
        <InputText
          v-model="searchQuery"
          placeholder="Buscar familia o representante..."
          class="w-64"
          data-testid="search-input"
          aria-label="Buscar por familia o representante"
        />
      </IconField>
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
      <MultiSelect
        v-if="accommodationPreferenceOptions.length > 0"
        v-model="selectedAccommodationPreferenceKeys"
        :options="accommodationPreferenceOptions"
        optionLabel="label"
        optionValue="value"
        placeholder="Alojamiento"
        display="chip"
        :showSelectAll="false"
        class="w-72"
        :loading="filterOptionsLoading"
        data-testid="accommodation-preference-filter"
        aria-label="Filtrar por preferencia de alojamiento"
      />
      <MultiSelect
        v-if="extraOptions.length > 0"
        v-model="selectedExtraIds"
        :options="extraOptions"
        optionLabel="label"
        optionValue="value"
        placeholder="Extras"
        display="chip"
        :showSelectAll="false"
        class="w-56"
        :loading="filterOptionsLoading"
        data-testid="extras-filter"
        aria-label="Filtrar por extras"
      />
      <MultiSelect
        v-model="selectedAttendancePeriods"
        :options="attendancePeriodOptions"
        optionLabel="label"
        optionValue="value"
        placeholder="Período"
        display="chip"
        :showSelectAll="false"
        class="w-56"
        data-testid="attendance-period-filter"
        aria-label="Filtrar por período de asistencia"
      />
      <MultiSelect
        v-model="selectedAgeCategories"
        :options="ageCategoryOptions"
        optionLabel="label"
        optionValue="value"
        placeholder="Edad"
        display="chip"
        :showSelectAll="false"
        class="w-48"
        data-testid="age-category-filter"
        aria-label="Filtrar por categoría de edad"
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
      :sort-field="sortField"
      :sort-order="sortOrder"
      class="rounded-lg cursor-pointer"
      data-testid="registrations-table"
      @page="onPage"
      @row-click="onRowClick"
      @sort="onSort"
    >
      <Column field="familyUnit.name" header="Familia" sortable>
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
      <Column header="Estado">
        <template #body="{ data }">
          <Tag :value="statusLabel(data.status)" :severity="statusSeverity(data.status)" />
        </template>
      </Column>
      <Column header="Período">
        <template #body="{ data }">
          <span class="text-sm font-mono text-gray-700">
            {{ formatAttendancePeriods(data.attendancePeriods) }}
          </span>
        </template>
      </Column>
      <Column header="Aloj.">
        <template #body="{ data }">
          <div class="flex gap-1">
            <span
              v-for="pref in data.accommodationPreferences"
              :key="pref.preferenceOrder"
              v-tooltip.top="`${pref.preferenceOrder}ª opción: ${pref.accommodationName} (${ACCOMMODATION_LABEL[pref.accommodationType as AccommodationType]})`"
              class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-gray-100 text-gray-600 cursor-default"
            >
              <i :class="ACCOMMODATION_ICON[pref.accommodationType as AccommodationType]" class="text-xs" />
            </span>
          </div>
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
      <Column field="createdAt" header="Creación" sortable>
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>

      <!-- Footer totals -->
      <ColumnGroup type="footer">
        <Row>
          <Column
            :footer="`Total: ${totals?.totalRegistrations ?? 0} inscripciones`"
            :colspan="5"
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

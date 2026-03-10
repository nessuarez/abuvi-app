<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Select from 'primevue/select'
import DateInput from '@/components/shared/DateInput.vue'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import PaymentStatusBadge from '@/components/payments/PaymentStatusBadge.vue'
import PaymentConceptLines from '@/components/payments/PaymentConceptLines.vue'
import { usePayments } from '@/composables/usePayments'
import { useCampEditions } from '@/composables/useCampEditions'
import type { AdminPaymentResponse, PaymentFilterParams } from '@/types/payment'
import type { PaymentStatus } from '@/types/registration'
import { formatDateLocal } from '@/utils/date'

const { getAllPayments, loading, error } = usePayments()
const { allEditions, fetchAllEditions } = useCampEditions()

const payments = ref<AdminPaymentResponse[]>([])
const totalRecords = ref(0)
const initialLoading = ref(true)
const expandedRows = ref<AdminPaymentResponse[]>([])

// Filters
const selectedStatus = ref<PaymentStatus | null>(null)
const selectedEditionId = ref<string | null>(null)
const dateFrom = ref<Date | null>(null)
const dateTo = ref<Date | null>(null)
const currentPage = ref(1)
const pageSize = 20

const statusOptions = [
  { label: 'Todos', value: null },
  { label: 'Pendiente', value: 'Pending' },
  { label: 'En revisión', value: 'PendingReview' },
  { label: 'Completado', value: 'Completed' },
  { label: 'Fallido', value: 'Failed' },
  { label: 'Reembolsado', value: 'Refunded' }
]

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(
    new Date(dateStr)
  )

const toIsoDate = (d: Date): string => formatDateLocal(d)

const isImage = (fileName: string | null): boolean => {
  if (!fileName) return false
  return /\.(jpg|jpeg|png|webp)$/i.test(fileName)
}

const fetchPayments = async () => {
  const filter: PaymentFilterParams = {
    page: currentPage.value,
    pageSize
  }
  if (selectedStatus.value) filter.status = selectedStatus.value
  if (selectedEditionId.value) filter.campEditionId = selectedEditionId.value
  if (dateFrom.value) filter.fromDate = toIsoDate(dateFrom.value)
  if (dateTo.value) filter.toDate = toIsoDate(dateTo.value)

  const result = await getAllPayments(filter)
  if (result) {
    payments.value = result.items
    totalRecords.value = result.totalCount
  }
}

const onPageChange = (event: { page: number }) => {
  currentPage.value = event.page + 1
  fetchPayments()
}

const resetFilters = () => {
  selectedStatus.value = null
  selectedEditionId.value = null
  dateFrom.value = null
  dateTo.value = null
  currentPage.value = 1
  fetchPayments()
}

watch([selectedStatus, selectedEditionId, dateFrom, dateTo], () => {
  currentPage.value = 1
  fetchPayments()
})

onMounted(async () => {
  await Promise.all([fetchPayments(), fetchAllEditions()])
  initialLoading.value = false
})
</script>

<template>
  <div>
    <!-- Filters -->
    <div class="mb-4 flex flex-wrap items-end gap-3">
      <div>
        <label class="mb-1 block text-xs font-medium text-gray-600">Estado</label>
        <Select
          v-model="selectedStatus"
          :options="statusOptions"
          option-label="label"
          option-value="value"
          placeholder="Todos"
          class="w-40"
        />
      </div>
      <div>
        <label class="mb-1 block text-xs font-medium text-gray-600">Edición</label>
        <Select
          v-model="selectedEditionId"
          :options="[{ id: null, label: 'Todas' }, ...allEditions.map((e) => ({ id: e.id, label: `${e.name ?? 'Campamento'} ${e.year}` }))]"
          option-label="label"
          option-value="id"
          placeholder="Todas"
          class="w-48"
        />
      </div>
      <div>
        <label class="mb-1 block text-xs font-medium text-gray-600">Desde</label>
        <DateInput v-model="dateFrom" placeholder="Desde" :show-calendar="false" />
      </div>
      <div>
        <label class="mb-1 block text-xs font-medium text-gray-600">Hasta</label>
        <DateInput v-model="dateTo" placeholder="Hasta" :show-calendar="false" />
      </div>
      <Button
        icon="pi pi-filter-slash"
        severity="secondary"
        text
        rounded
        aria-label="Limpiar filtros"
        @click="resetFilters"
      />
    </div>

    <div v-if="initialLoading" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>

    <DataTable
      v-else
      v-model:expanded-rows="expandedRows"
      :value="payments"
      :loading="loading"
      lazy
      paginator
      :rows="pageSize"
      :total-records="totalRecords"
      :rows-per-page-options="[10, 20, 50]"
      striped-rows
      size="small"
      data-key="id"
      @page="onPageChange"
    >
      <Column expander style="width: 2rem" />
      <Column field="familyUnitName" header="Familia" />
      <Column field="campEditionName" header="Edición" />
      <Column field="installmentNumber" header="Plazo" style="width: 4rem">
        <template #body="{ data }">{{ data.installmentNumber }}</template>
      </Column>
      <Column field="amount" header="Importe" style="width: 7rem">
        <template #body="{ data }">{{ formatCurrency(data.amount) }}</template>
      </Column>
      <Column field="status" header="Estado" style="width: 8rem">
        <template #body="{ data }">
          <PaymentStatusBadge :status="data.status" />
        </template>
      </Column>
      <Column field="transferConcept" header="Concepto">
        <template #body="{ data }">
          <span v-if="data.transferConcept" class="font-mono text-xs">{{ data.transferConcept }}</span>
          <span v-else class="text-gray-400">—</span>
        </template>
      </Column>
      <Column header="Justificante" style="width: 6rem">
        <template #body="{ data }">
          <a
            v-if="data.proofFileUrl"
            :href="data.proofFileUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="text-blue-600 hover:underline"
          >
            <i :class="isImage(data.proofFileName) ? 'pi pi-image' : 'pi pi-file-pdf'" />
          </a>
          <span v-else class="text-gray-400">—</span>
        </template>
      </Column>
      <Column field="confirmedByUserName" header="Confirmado por">
        <template #body="{ data }">
          <span v-if="data.confirmedByUserName">{{ data.confirmedByUserName }}</span>
          <span v-else class="text-gray-400">—</span>
        </template>
      </Column>
      <Column field="createdAt" header="Fecha" style="width: 7rem">
        <template #body="{ data }">{{ formatDate(data.createdAt) }}</template>
      </Column>
      <template #expansion="{ data }">
        <div class="p-3">
          <PaymentConceptLines
            :concept-lines="data.conceptLines"
            :extra-concept-lines="data.extraConceptLines"
          />
        </div>
      </template>
    </DataTable>

    <Message v-if="error" severity="error" :closable="false" class="mt-4">
      {{ error }}
    </Message>
  </div>
</template>

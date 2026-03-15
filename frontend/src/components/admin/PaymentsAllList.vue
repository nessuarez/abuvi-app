<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import DateInput from '@/components/shared/DateInput.vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import PaymentStatusBadge from '@/components/payments/PaymentStatusBadge.vue'
import PaymentConceptLines from '@/components/payments/PaymentConceptLines.vue'
import { usePayments } from '@/composables/usePayments'
import { useCampEditions } from '@/composables/useCampEditions'
import type { AdminPaymentResponse, PaymentFilterParams } from '@/types/payment'
import type { PaymentStatus } from '@/types/registration'
import { formatDateLocal } from '@/utils/date'

const toast = useToast()
const { getAllPayments, updateManualPayment, deleteManualPayment, loading, error } = usePayments()
const { allEditions, fetchAllEditions } = useCampEditions()

const payments = ref<AdminPaymentResponse[]>([])
const totalRecords = ref(0)
const initialLoading = ref(true)
const expandedRows = ref<AdminPaymentResponse[]>([])

// Filters
const selectedStatus = ref<PaymentStatus | null>(null)
const selectedEditionId = ref<string | null>(null)
const selectedInstallment = ref<number | null>(null)
const filterMode = ref<'installment' | 'dates'>('installment')
const dateFrom = ref<Date | null>(null)
const dateTo = ref<Date | null>(null)
const currentPage = ref(1)
const pageSize = 20

// Edit manual payment dialog
const showEditDialog = ref(false)
const editTarget = ref<AdminPaymentResponse | null>(null)
const editAmount = ref<number | null>(null)
const editDescription = ref('')
const editNotes = ref('')
const saving = ref(false)

// Delete manual payment dialog
const showDeleteDialog = ref(false)
const deleteTarget = ref<AdminPaymentResponse | null>(null)
const deleting = ref(false)

const statusOptions = [
  { label: 'Todos', value: null },
  { label: 'Pendiente', value: 'Pending' },
  { label: 'En revisión', value: 'PendingReview' },
  { label: 'Completado', value: 'Completed' },
  { label: 'Fallido', value: 'Failed' },
  { label: 'Reembolsado', value: 'Refunded' }
]

const installmentOptions = [
  { label: 'Todos', value: null },
  { label: 'Plazo 1', value: 1 },
  { label: 'Plazo 2', value: 2 },
  { label: 'Plazo 3+', value: 3 }
]

const filterModeOptions = [
  { label: 'Por período', value: 'installment' },
  { label: 'Por fechas', value: 'dates' }
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
  if (filterMode.value === 'installment' && selectedInstallment.value) {
    filter.installmentNumber = selectedInstallment.value
  }
  if (filterMode.value === 'dates') {
    if (dateFrom.value) filter.fromDate = toIsoDate(dateFrom.value)
    if (dateTo.value) filter.toDate = toIsoDate(dateTo.value)
  }

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
  selectedInstallment.value = null
  filterMode.value = 'installment'
  dateFrom.value = null
  dateTo.value = null
  currentPage.value = 1
  fetchPayments()
}

// Edit manual payment
const openEditDialog = (payment: AdminPaymentResponse) => {
  editTarget.value = payment
  editAmount.value = payment.amount
  editDescription.value = payment.manualConceptLine?.description ?? ''
  editNotes.value = payment.adminNotes ?? ''
  showEditDialog.value = true
}

const handleSaveEdit = async () => {
  if (!editTarget.value || !editAmount.value || !editDescription.value) return
  saving.value = true
  const result = await updateManualPayment(editTarget.value.id, {
    amount: editAmount.value,
    description: editDescription.value,
    adminNotes: editNotes.value || null
  })
  saving.value = false
  if (result) {
    showEditDialog.value = false
    toast.add({ severity: 'success', summary: 'Pago actualizado', life: 3000 })
    await fetchPayments()
  }
}

// Delete manual payment
const openDeleteDialog = (payment: AdminPaymentResponse) => {
  deleteTarget.value = payment
  showDeleteDialog.value = true
}

const handleDelete = async () => {
  if (!deleteTarget.value) return
  deleting.value = true
  const success = await deleteManualPayment(deleteTarget.value.id)
  deleting.value = false
  if (success) {
    showDeleteDialog.value = false
    toast.add({ severity: 'success', summary: 'Pago eliminado', life: 3000 })
    await fetchPayments()
  }
}

watch([selectedStatus, selectedEditionId, selectedInstallment, dateFrom, dateTo], () => {
  currentPage.value = 1
  fetchPayments()
})

watch(filterMode, (newMode) => {
  if (newMode === 'installment') {
    dateFrom.value = null
    dateTo.value = null
  } else {
    selectedInstallment.value = null
  }
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
        <label class="mb-1 block text-xs font-medium text-gray-600">Edicion</label>
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
        <label class="mb-1 block text-xs font-medium text-gray-600">Filtrar por</label>
        <SelectButton
          v-model="filterMode"
          :options="filterModeOptions"
          option-label="label"
          option-value="value"
        />
      </div>
      <div v-if="filterMode === 'installment'">
        <label class="mb-1 block text-xs font-medium text-gray-600">Período de pago</label>
        <Select
          v-model="selectedInstallment"
          :options="installmentOptions"
          option-label="label"
          option-value="value"
          placeholder="Todos"
          class="w-40"
        />
      </div>
      <template v-if="filterMode === 'dates'">
        <div>
          <label class="mb-1 block text-xs font-medium text-gray-600">Desde</label>
          <DateInput v-model="dateFrom" placeholder="Desde" :show-calendar="false" />
        </div>
        <div>
          <label class="mb-1 block text-xs font-medium text-gray-600">Hasta</label>
          <DateInput v-model="dateTo" placeholder="Hasta" :show-calendar="false" />
        </div>
      </template>
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
      <Column field="campEditionName" header="Edicion" />
      <Column field="installmentNumber" header="Plazo" style="width: 5rem">
        <template #body="{ data }">
          <div class="flex items-center gap-1">
            <span>{{ data.installmentNumber }}</span>
            <span
              v-if="data.isManual"
              class="inline-flex items-center rounded-full bg-purple-100 px-1.5 py-0.5 text-[0.6rem] font-medium text-purple-700"
            >
              Manual
            </span>
          </div>
        </template>
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
          <span v-else class="text-gray-400">&mdash;</span>
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
          <span v-else class="text-gray-400">&mdash;</span>
        </template>
      </Column>
      <Column field="confirmedByUserName" header="Confirmado por">
        <template #body="{ data }">
          <span v-if="data.confirmedByUserName">{{ data.confirmedByUserName }}</span>
          <span v-else class="text-gray-400">&mdash;</span>
        </template>
      </Column>
      <Column field="createdAt" header="Fecha" style="width: 7rem">
        <template #body="{ data }">{{ formatDate(data.createdAt) }}</template>
      </Column>
      <Column header="" style="width: 5rem">
        <template #body="{ data }">
          <div v-if="data.isManual && data.status === 'Pending'" class="flex gap-1">
            <Button
              icon="pi pi-pencil"
              text
              rounded
              size="small"
              severity="secondary"
              aria-label="Editar pago manual"
              @click="openEditDialog(data)"
            />
            <Button
              icon="pi pi-trash"
              text
              rounded
              size="small"
              severity="danger"
              aria-label="Eliminar pago manual"
              @click="openDeleteDialog(data)"
            />
          </div>
        </template>
      </Column>
      <template #expansion="{ data }">
        <div class="p-3">
          <PaymentConceptLines
            :concept-lines="data.conceptLines"
            :extra-concept-lines="data.extraConceptLines"
            :manual-concept-line="data.manualConceptLine"
          />
          <p v-if="data.adminNotes" class="mt-2 text-xs text-gray-500">
            <strong>Notas:</strong> {{ data.adminNotes }}
          </p>
        </div>
      </template>
    </DataTable>

    <Message v-if="error" severity="error" :closable="false" class="mt-4">
      {{ error }}
    </Message>

    <!-- Edit manual payment dialog -->
    <Dialog
      v-model:visible="showEditDialog"
      header="Editar pago manual"
      :modal="true"
      :style="{ width: '28rem' }"
    >
      <div class="space-y-4">
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">Importe</label>
          <InputNumber
            v-model="editAmount"
            mode="currency"
            currency="EUR"
            locale="es-ES"
            :min="0.01"
            class="w-full"
          />
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">
            Descripcion <span class="text-red-500">*</span>
          </label>
          <InputText
            v-model="editDescription"
            class="w-full"
            placeholder="Descripcion del pago..."
          />
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">Notas (opcional)</label>
          <Textarea
            v-model="editNotes"
            :rows="2"
            class="w-full"
            placeholder="Notas adicionales..."
          />
        </div>
      </div>
      <template #footer>
        <Button label="Cancelar" severity="secondary" text @click="showEditDialog = false" />
        <Button
          label="Guardar"
          icon="pi pi-check"
          :loading="saving"
          :disabled="!editAmount || !editDescription"
          @click="handleSaveEdit"
        />
      </template>
    </Dialog>

    <!-- Delete manual payment dialog -->
    <Dialog
      v-model:visible="showDeleteDialog"
      header="Eliminar pago manual"
      :modal="true"
      :style="{ width: '28rem' }"
    >
      <p class="text-sm text-gray-600">
        ¿Eliminar el pago manual de
        <strong>{{ deleteTarget?.familyUnitName }}</strong>
        por {{ deleteTarget ? formatCurrency(deleteTarget.amount) : '' }}?
      </p>
      <p class="mt-2 text-xs text-gray-500">
        El importe se descontara del total de la inscripcion.
      </p>
      <template #footer>
        <Button label="Cancelar" severity="secondary" text @click="showDeleteDialog = false" />
        <Button
          label="Eliminar"
          severity="danger"
          icon="pi pi-trash"
          :loading="deleting"
          @click="handleDelete"
        />
      </template>
    </Dialog>
  </div>
</template>

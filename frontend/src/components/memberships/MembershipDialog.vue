<script setup lang="ts">
import { ref, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import Calendar from 'primevue/calendar'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import ConfirmDialog from 'primevue/confirmdialog'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Message from 'primevue/message'
import { useMemberships } from '@/composables/useMemberships'
import PayFeeDialog from './PayFeeDialog.vue'
import type { MembershipFeeResponse, PayFeeRequest } from '@/types/membership'
import { FeeStatusLabels, FeeStatusSeverity, FeeStatus } from '@/types/membership'

const props = defineProps<{
  visible: boolean
  familyUnitId: string
  memberId: string
  memberName: string
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
}>()

const toast = useToast()
const confirm = useConfirm()

const { membership, fees, loading, error, getMembership, createMembership, deactivateMembership, payFee } =
  useMemberships()

const showPayFeeDialog = ref(false)
const selectedFee = ref<MembershipFeeResponse | null>(null)
const createStartDate = ref<Date>(new Date())

// Fetch membership data when dialog opens
watch(
  () => props.visible,
  async (val) => {
    if (val && props.familyUnitId && props.memberId) {
      await getMembership(props.familyUnitId, props.memberId)
    }
  },
)

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return new Intl.DateTimeFormat('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
}

const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)
}

const handleCreate = async () => {
  const dateStr = createStartDate.value.toISOString().split('T')[0]
  const result = await createMembership(props.familyUnitId, props.memberId, { startDate: dateStr })
  if (result) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Membresía activada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleDeactivate = () => {
  confirm.require({
    message: `¿Desactivar la membresía de ${props.memberName}?`,
    header: 'Confirmar',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Desactivar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const ok = await deactivateMembership(props.familyUnitId, props.memberId)
      if (ok) {
        toast.add({ severity: 'success', summary: 'Éxito', detail: 'Membresía desactivada', life: 3000 })
      } else {
        toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
      }
    },
  })
}

const openPayFeeDialog = (fee: MembershipFeeResponse) => {
  selectedFee.value = fee
  showPayFeeDialog.value = true
}

const handlePayFee = async (request: PayFeeRequest) => {
  if (!selectedFee.value || !membership.value) return
  const result = await payFee(membership.value.id, selectedFee.value.id, request)
  if (result) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Pago registrado', life: 3000 })
    showPayFeeDialog.value = false
    selectedFee.value = null
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    :header="`Membresía — ${memberName}`"
    :modal="true"
    :closable="true"
    :dismissableMask="true"
    class="w-full max-w-2xl"
    @update:visible="$emit('update:visible', $event)"
  >
    <ConfirmDialog />

    <!-- Loading state -->
    <div v-if="loading" class="flex justify-center py-8">
      <i class="pi pi-spin pi-spinner text-3xl text-primary-500"></i>
    </div>

    <!-- Error state (non-404 API errors) -->
    <Message v-else-if="error" severity="error" class="mb-4">{{ error }}</Message>

    <div v-else>
      <!-- No active membership -->
      <div v-if="!membership" class="space-y-4">
        <Message severity="info">Este miembro no tiene una membresía activa.</Message>

        <p class="text-sm text-gray-600">
          Activa la membresía para registrar a este miembro como socio.
        </p>

        <div class="flex flex-col gap-2">
          <label for="membership-start-date" class="font-medium text-sm">
            Fecha de inicio <span class="text-red-500">*</span>
          </label>
          <Calendar
            id="membership-start-date"
            v-model="createStartDate"
            dateFormat="dd/mm/yy"
            :maxDate="new Date()"
            showIcon
            class="w-full"
          />
          <small class="text-gray-500">Debe ser la fecha actual o una fecha pasada.</small>
        </div>

        <div class="flex justify-end">
          <Button
            label="Activar membresía"
            icon="pi pi-check"
            :loading="loading"
            @click="handleCreate"
          />
        </div>
      </div>

      <!-- Existing membership (active or inactive) -->
      <div v-else class="space-y-4">
        <!-- Status row -->
        <div class="flex flex-wrap justify-between items-center gap-3">
          <div class="flex items-center gap-3">
            <Tag v-if="membership.isActive" value="Socio activo" severity="success" />
            <Tag v-else value="Membresía inactiva" severity="secondary" />
            <span class="text-sm text-gray-600">Desde {{ formatDate(membership.startDate) }}</span>
          </div>
          <Button
            v-if="membership.isActive"
            label="Desactivar membresía"
            severity="danger"
            outlined
            :loading="loading"
            @click="handleDeactivate"
          />
        </div>

        <!-- Fees table (only if active and fees exist) -->
        <div v-if="membership.isActive && fees.length > 0" class="space-y-2">
          <h3 class="font-semibold text-lg">Cuotas</h3>
          <DataTable
            :value="fees"
            :loading="loading"
            stripedRows
            responsiveLayout="scroll"
            class="p-datatable-sm"
          >
            <Column field="year" header="Año" :sortable="true" />
            <Column field="amount" header="Importe">
              <template #body="{ data }">
                {{ formatCurrency(data.amount) }}
              </template>
            </Column>
            <Column field="status" header="Estado">
              <template #body="{ data }">
                <Tag
                  :value="FeeStatusLabels[data.status as FeeStatus]"
                  :severity="FeeStatusSeverity[data.status as FeeStatus]"
                />
              </template>
            </Column>
            <Column field="paidDate" header="Fecha de pago">
              <template #body="{ data }">
                {{ data.paidDate ? formatDate(data.paidDate) : '—' }}
              </template>
            </Column>
            <Column header="Acciones">
              <template #body="{ data }">
                <Button
                  v-if="data.status !== FeeStatus.Paid"
                  label="Pagar"
                  severity="success"
                  size="small"
                  @click="openPayFeeDialog(data)"
                />
              </template>
            </Column>
          </DataTable>
        </div>

        <div
          v-else-if="membership.isActive && fees.length === 0"
          class="text-gray-500 text-sm italic"
        >
          No hay cuotas registradas todavía.
        </div>
      </div>
    </div>
  </Dialog>

  <!-- Pay Fee sub-dialog -->
  <PayFeeDialog
    v-if="selectedFee"
    v-model:visible="showPayFeeDialog"
    :fee="selectedFee"
    :loading="loading"
    @submit="handlePayFee"
  />
</template>

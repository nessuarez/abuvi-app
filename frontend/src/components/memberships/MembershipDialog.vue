<script setup lang="ts">
import { ref, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import ConfirmDialog from 'primevue/confirmdialog'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Message from 'primevue/message'
import { useMemberships } from '@/composables/useMemberships'
import PayFeeDialog from './PayFeeDialog.vue'
import CreateFeeDialog from './CreateFeeDialog.vue'
import type { MembershipFeeResponse, PayFeeRequest, CreateMembershipFeeRequest } from '@/types/membership'
import { parseDateSafe } from '@/utils/date'
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

const { membership, fees, loading, error, getMembership, createMembership, deactivateMembership, payFee, createFee, reactivateMembership } =
  useMemberships()

const showPayFeeDialog = ref(false)
const selectedFee = ref<MembershipFeeResponse | null>(null)
const showCreateFeeDialog = ref(false)
const currentYear = new Date().getFullYear()
const createStartYear = ref<number>(currentYear)
const reactivateYear = ref<number>(currentYear)

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
  const date = parseDateSafe(dateString)
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
  const result = await createMembership(props.familyUnitId, props.memberId, { year: createStartYear.value })
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

const handleReactivate = async () => {
  const result = await reactivateMembership(props.familyUnitId, props.memberId, { year: reactivateYear.value })
  if (result) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Membresía reactivada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleCreateFee = async (request: CreateMembershipFeeRequest) => {
  if (!membership.value) return
  const result = await createFee(membership.value.id, request)
  if (result) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Cuota registrada', life: 3000 })
    showCreateFeeDialog.value = false
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
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
      <!-- No membership yet -->
      <div v-if="!membership" class="space-y-4">
        <Message severity="info">Este miembro no tiene una membresía activa.</Message>

        <p class="text-sm text-gray-600">
          Activa la membresía para registrar a este miembro como socio.
        </p>

        <div class="flex flex-col gap-2">
          <label for="membership-start-year" class="font-medium text-sm">
            Año de inicio <span class="text-red-500">*</span>
          </label>
          <InputNumber
            id="membership-start-year"
            v-model="createStartYear"
            :min="2001"
            :max="currentYear"
            :use-grouping="false"
            class="w-full"
          />
          <small class="text-gray-500">Año en que el miembro se hizo socio. No puede ser futuro.</small>
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

      <!-- Inactive membership: reactivate flow -->
      <div v-else-if="!membership.isActive" class="space-y-4">
        <div class="flex items-center gap-3">
          <Tag value="Membresía inactiva" severity="secondary" />
          <span class="text-sm text-gray-600">Desde {{ formatDate(membership.startDate) }}</span>
        </div>

        <Message severity="warn">
          Esta membresía está desactivada. Puedes reactivarla eligiendo el año de reactivación.
        </Message>

        <div class="flex flex-col gap-2">
          <label for="reactivate-year" class="text-sm font-medium">
            Año de reactivación <span class="text-red-500">*</span>
          </label>
          <InputNumber
            id="reactivate-year"
            v-model="reactivateYear"
            :min="2001"
            :max="currentYear"
            :use-grouping="false"
            class="w-full"
          />
          <small class="text-gray-500">Se creará una cuota pendiente para este año.</small>
        </div>

        <div class="flex justify-end">
          <Button
            label="Reactivar membresía"
            icon="pi pi-refresh"
            severity="success"
            :loading="loading"
            data-testid="reactivate-btn"
            @click="handleReactivate"
          />
        </div>
      </div>

      <!-- Active membership -->
      <div v-else class="space-y-4">
        <!-- Status row -->
        <div class="flex flex-wrap justify-between items-center gap-3">
          <div class="flex items-center gap-3">
            <Tag value="Socio/a activo/a" severity="success" />
            <span class="text-sm text-gray-600">Desde {{ formatDate(membership.startDate) }}</span>
          </div>
          <Button
            label="Desactivar membresía"
            severity="danger"
            outlined
            :loading="loading"
            @click="handleDeactivate"
          />
        </div>

        <!-- No fees yet -->
        <div v-if="fees.length === 0" class="space-y-3">
          <Message severity="warn">
            No hay cuotas registradas para esta membresía. Carga la cuota del año en curso para
            que la familia pueda inscribirse al campamento.
          </Message>
          <div class="flex justify-end">
            <Button
              label="Cargar cuota anual"
              icon="pi pi-plus"
              severity="primary"
              :loading="loading"
              data-testid="create-fee-btn"
              @click="showCreateFeeDialog = true"
            />
          </div>
        </div>

        <!-- Fees table -->
        <div v-else class="space-y-2">
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

          <!-- Add fee for current year if missing -->
          <div class="flex justify-end mt-2">
            <Button
              v-if="!fees.some((f) => f.year === currentYear)"
              :label="`Cargar cuota ${currentYear}`"
              icon="pi pi-plus"
              size="small"
              severity="secondary"
              outlined
              data-testid="create-fee-btn"
              @click="showCreateFeeDialog = true"
            />
          </div>
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

  <!-- Create Fee sub-dialog -->
  <CreateFeeDialog
    v-if="showCreateFeeDialog && membership"
    v-model:visible="showCreateFeeDialog"
    :membership-id="membership.id"
    :loading="loading"
    @submit="handleCreateFee"
  />
</template>

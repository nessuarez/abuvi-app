<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Checkbox from 'primevue/checkbox'
import ToggleSwitch from 'primevue/toggleswitch'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { usePayments } from '@/composables/usePayments'
import type { AdminPaymentResponse } from '@/types/payment'

const props = defineProps<{
  visible: boolean
  registrationId: string
  familyUnitName: string
  payments: AdminPaymentResponse[]
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'confirmed', payments: AdminPaymentResponse[]): void
}>()

const toast = useToast()
const { confirmCombinedPayments, loading, error } = usePayments()

const selectedPaymentIds = ref<string[]>([])
const totalReceivedAmount = ref<number | null>(null)
const applySurplusToNext = ref(false)
const adminNotes = ref('')

const formatCurrency = (value: number) =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(value)

const installmentLabel = (payment: AdminPaymentResponse): string => {
  if (payment.isManual && payment.manualConceptLine) return payment.manualConceptLine.description
  switch (payment.installmentNumber) {
    case 1: return 'Primer pago'
    case 2: return 'Segundo pago'
    case 3: return 'Pago de extras'
    default: return `Plazo ${payment.installmentNumber}`
  }
}

const selectedPayments = computed(() =>
  props.payments.filter((p) => selectedPaymentIds.value.includes(p.id))
)

const distributionPreview = computed(() => {
  if (!totalReceivedAmount.value || selectedPayments.value.length === 0) return []
  let remaining = totalReceivedAmount.value
  return selectedPayments.value.map((p) => {
    const assigned = Math.min(remaining, p.amount)
    remaining -= assigned
    return { payment: p, assignedAmount: assigned }
  })
})

const surplus = computed(() => {
  if (!totalReceivedAmount.value || selectedPayments.value.length === 0) return 0
  const total = selectedPayments.value.reduce((s, p) => s + p.amount, 0)
  return Math.max(0, totalReceivedAmount.value - total)
})

const canConfirm = computed(
  () => totalReceivedAmount.value && totalReceivedAmount.value > 0 && selectedPaymentIds.value.length > 0
)

const togglePayment = (paymentId: string) => {
  const idx = selectedPaymentIds.value.indexOf(paymentId)
  if (idx === -1) selectedPaymentIds.value.push(paymentId)
  else selectedPaymentIds.value.splice(idx, 1)
}

const resetForm = () => {
  selectedPaymentIds.value = props.payments.map((p) => p.id)
  totalReceivedAmount.value = null
  applySurplusToNext.value = false
  adminNotes.value = ''
  error.value = null
}

watch(
  () => props.visible,
  (val) => {
    if (val) resetForm()
  },
  { immediate: true }
)

const handleConfirm = async () => {
  if (!totalReceivedAmount.value || selectedPaymentIds.value.length === 0) return
  const result = await confirmCombinedPayments(props.registrationId, {
    paymentIds: selectedPaymentIds.value,
    totalReceivedAmount: totalReceivedAmount.value,
    applySurplusToNext: applySurplusToNext.value,
    adminNotes: adminNotes.value.trim() || null
  })
  if (result) {
    toast.add({ severity: 'success', summary: 'Pagos confirmados', life: 3000 })
    emit('confirmed', result)
    emit('update:visible', false)
  }
}

const close = () => emit('update:visible', false)
</script>

<template>
  <Dialog
    :visible="visible"
    :modal="true"
    :style="{ width: '90vw', maxWidth: '560px' }"
    @update:visible="emit('update:visible', $event)"
  >
    <template #header>
      <span class="font-semibold">Confirmar pago combinado para {{ familyUnitName }}</span>
    </template>

    <div class="space-y-4">
      <!-- Payment selection -->
      <div>
        <p class="mb-2 text-sm font-medium text-gray-700">Pagos a confirmar</p>
        <div class="space-y-2">
          <div
            v-for="payment in payments"
            :key="payment.id"
            class="flex cursor-pointer items-center gap-3 rounded border p-2 hover:bg-gray-50"
            @click="togglePayment(payment.id)"
          >
            <Checkbox
              :model-value="selectedPaymentIds.includes(payment.id)"
              :binary="true"
              @click.stop="togglePayment(payment.id)"
            />
            <span class="text-sm">
              P{{ payment.installmentNumber }} — {{ installmentLabel(payment) }} —
              <strong>{{ formatCurrency(payment.amount) }}</strong>
            </span>
          </div>
        </div>
      </div>

      <!-- Total received amount -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">
          Importe recibido (€) <span class="text-red-500">*</span>
        </label>
        <InputNumber
          v-model="totalReceivedAmount"
          mode="currency"
          currency="EUR"
          locale="es-ES"
          :min="0.01"
          :fraction-digits="2"
          class="w-full"
          placeholder="0,00"
        />
      </div>

      <!-- Distribution preview -->
      <div v-if="totalReceivedAmount && distributionPreview.length > 0">
        <p class="mb-2 text-sm font-medium text-gray-700">Distribución del importe</p>
        <div class="rounded border divide-y text-sm">
          <div
            v-for="row in distributionPreview"
            :key="row.payment.id"
            class="flex justify-between px-3 py-1.5"
          >
            <span>P{{ row.payment.installmentNumber }} — {{ installmentLabel(row.payment) }}</span>
            <span class="font-medium">{{ formatCurrency(row.assignedAmount) }}</span>
          </div>
        </div>
        <div v-if="surplus > 0" class="mt-2">
          <p class="text-sm font-medium text-amber-600">
            Sobrante: {{ formatCurrency(surplus) }}
            <span v-if="!applySurplusToNext"> — se descartará</span>
            <span v-else> — se aplicará al siguiente pago pendiente</span>
          </p>
          <div class="mt-2 flex items-center gap-2">
            <ToggleSwitch v-model="applySurplusToNext" input-id="apply-surplus" />
            <label for="apply-surplus" class="cursor-pointer text-sm text-gray-700">
              Aplicar sobrante al siguiente pago pendiente
            </label>
          </div>
        </div>
      </div>

      <!-- Admin notes -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Notas (opcional)</label>
        <Textarea
          v-model="adminNotes"
          :rows="2"
          class="w-full"
          placeholder="Notas internas..."
          maxlength="2000"
        />
      </div>

      <Message v-if="error" severity="error" :closable="false" class="text-sm">
        {{ error }}
      </Message>
    </div>

    <template #footer>
      <Button label="Cancelar" severity="secondary" text @click="close" />
      <Button
        label="Confirmar pagos"
        icon="pi pi-check-circle"
        severity="success"
        :loading="loading"
        :disabled="!canConfirm"
        @click="handleConfirm"
      />
    </template>
  </Dialog>
</template>

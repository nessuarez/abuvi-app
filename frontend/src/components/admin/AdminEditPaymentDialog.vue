<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DateInput from '@/components/shared/DateInput.vue'
import { usePayments } from '@/composables/usePayments'
import type { AdminPaymentResponse, AdminEditPaymentRequest } from '@/types/payment'
import { formatDateLocal, parseDateSafe } from '@/utils/date'

const props = defineProps<{
  visible: boolean
  payment: AdminPaymentResponse
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'saved', payment: AdminPaymentResponse): void
}>()

const toast = useToast()
const { adminEditPayment, loading, error } = usePayments()

const amount = ref<number | null>(null)
const overrideConceptChecked = ref(false)
const conceptDescription = ref('')
const dueDate = ref<Date | null>(null)
const adminNotes = ref('')

const isCompleted = computed(() => props.payment.status === 'Completed')

const formatCurrency = (value: number) =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(value)

const resetForm = () => {
  amount.value = props.payment.amount
  overrideConceptChecked.value = false
  conceptDescription.value = ''
  dueDate.value = props.payment.dueDate ? parseDateSafe(props.payment.dueDate) : null
  adminNotes.value = props.payment.adminNotes ?? ''
  error.value = null
}

watch(
  () => props.visible,
  (val) => {
    if (val) resetForm()
  }
)

const handleSave = async () => {
  const request: AdminEditPaymentRequest = {}
  if (amount.value !== props.payment.amount) request.amount = amount.value
  if (overrideConceptChecked.value && conceptDescription.value.trim())
    request.conceptDescription = conceptDescription.value.trim()
  if (dueDate.value) {
    const formatted = formatDateLocal(dueDate.value)
    if (formatted !== props.payment.dueDate) request.dueDate = formatted
  }
  const notes = adminNotes.value.trim() || null
  if (notes !== props.payment.adminNotes) request.adminNotes = notes

  const result = await adminEditPayment(props.payment.id, request)
  if (result) {
    toast.add({ severity: 'success', summary: 'Pago actualizado', life: 3000 })
    emit('saved', result)
    emit('update:visible', false)
  }
}

const close = () => emit('update:visible', false)
</script>

<template>
  <Dialog
    :visible="visible"
    header="Editar pago"
    :modal="true"
    :style="{ width: '90vw', maxWidth: '560px' }"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4">
      <Message v-if="isCompleted" severity="warn" :closable="false" class="text-sm">
        Estás editando un pago ya completado. Si cambias el importe, los pagos pendientes
        posteriores serán recalculados automáticamente.
      </Message>

      <Message
        v-if="payment.conceptOverridden && payment.originalAmount != null"
        severity="info"
        :closable="false"
        class="text-sm"
      >
        Importe original: <strong>{{ formatCurrency(payment.originalAmount) }}</strong>
      </Message>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Importe</label>
        <InputNumber
          v-model="amount"
          mode="currency"
          currency="EUR"
          locale="es-ES"
          :min="0.01"
          :max="99999.99"
          :fraction-digits="2"
          class="w-full"
        />
      </div>

      <div>
        <div class="flex items-center gap-2">
          <Checkbox v-model="overrideConceptChecked" :binary="true" input-id="override-concept" />
          <label for="override-concept" class="cursor-pointer text-sm text-gray-700">
            Reemplazar descripción del concepto del pago
          </label>
        </div>
        <InputText
          v-if="overrideConceptChecked"
          v-model="conceptDescription"
          class="mt-2 w-full"
          placeholder="Nueva descripción del concepto..."
          maxlength="500"
        />
      </div>

      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Fecha de vencimiento</label>
        <DateInput v-model="dueDate" placeholder="Fecha de vencimiento" :show-calendar="false" />
      </div>

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
      <Button label="Guardar cambios" icon="pi pi-check" :loading="loading" @click="handleSave" />
    </template>
  </Dialog>
</template>

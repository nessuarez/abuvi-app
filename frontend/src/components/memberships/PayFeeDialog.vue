<script setup lang="ts">
import { ref, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Dialog from 'primevue/dialog'
import DatePicker from 'primevue/datepicker'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import type { MembershipFeeResponse, PayFeeRequest } from '@/types/membership'

const props = defineProps<{
  visible: boolean
  fee: MembershipFeeResponse | null
  loading?: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [request: PayFeeRequest]
}>()

const toast = useToast()

// Form state
const paidDate = ref<Date>(new Date())
const paymentReference = ref('')

// Validation
const paidDateError = ref<string | null>(null)
const paymentReferenceError = ref<string | null>(null)

// Reset form when dialog opens
watch(
  () => props.visible,
  (val) => {
    if (val) {
      paidDate.value = new Date()
      paymentReference.value = ''
      paidDateError.value = null
      paymentReferenceError.value = null
    }
  },
)

const validatePaidDate = () => {
  if (!paidDate.value) {
    paidDateError.value = 'La fecha de pago es obligatoria'
    return false
  }
  if (paidDate.value > new Date()) {
    paidDateError.value = 'La fecha de pago no puede ser futura'
    return false
  }
  paidDateError.value = null
  return true
}

const validatePaymentReference = () => {
  if (paymentReference.value && paymentReference.value.length > 100) {
    paymentReferenceError.value = 'La referencia de pago no puede exceder 100 caracteres'
    return false
  }
  paymentReferenceError.value = null
  return true
}

const handleSubmit = () => {
  const valid = validatePaidDate() && validatePaymentReference()
  if (!valid) {
    toast.add({
      severity: 'error',
      summary: 'Validación',
      detail: 'Por favor revisa los datos ingresados',
      life: 3000,
    })
    return
  }

  const request: PayFeeRequest = {
    paidDate: paidDate.value.toISOString().split('T')[0],
    paymentReference: paymentReference.value.trim() || null,
  }

  emit('submit', request)
}

const handleClose = () => {
  emit('update:visible', false)
}
</script>

<template>
  <Dialog
    :visible="visible"
    @update:visible="emit('update:visible', $event)"
    :header="`Registrar pago — Cuota ${fee?.year ?? ''}`"
    :modal="true"
    :closable="!loading"
    :dismissableMask="!loading"
    class="w-full max-w-md"
  >
    <form @submit.prevent="handleSubmit" class="space-y-4">
      <!-- Paid Date -->
      <div class="flex flex-col gap-2">
        <label for="paid-date" class="font-medium text-sm">
          Fecha de pago <span class="text-red-500">*</span>
        </label>
        <DatePicker
          id="paid-date"
          v-model="paidDate"
          dateFormat="dd/mm/yy"
          :maxDate="new Date()"
          :invalid="!!paidDateError"
          :disabled="loading"
          showIcon
          @blur="validatePaidDate"
          class="w-full"
        />
        <small v-if="paidDateError" class="text-red-500">{{ paidDateError }}</small>
      </div>

      <!-- Payment Reference -->
      <div class="flex flex-col gap-2">
        <label for="payment-reference" class="font-medium text-sm">Referencia de pago</label>
        <InputText
          id="payment-reference"
          v-model="paymentReference"
          placeholder="Ej: TRF-2026-001"
          :invalid="!!paymentReferenceError"
          :disabled="loading"
          :maxlength="100"
          @blur="validatePaymentReference"
        />
        <small v-if="paymentReferenceError" class="text-red-500">{{ paymentReferenceError }}</small>
        <small class="text-gray-500">Opcional. Máximo 100 caracteres.</small>
      </div>

      <div class="flex justify-end gap-2 pt-2">
        <Button
          type="button"
          label="Cancelar"
          severity="secondary"
          :disabled="loading"
          @click="handleClose"
        />
        <Button type="submit" label="Registrar pago" :loading="loading" :disabled="loading" />
      </div>
    </form>
  </Dialog>
</template>

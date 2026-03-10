<script setup lang="ts">
import { computed } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import PaymentStatusBadge from './PaymentStatusBadge.vue'
import PaymentConceptLines from './PaymentConceptLines.vue'
import ProofUploader from './ProofUploader.vue'
import type { PaymentResponse } from '@/types/payment'
import { parseDateSafe } from '@/utils/date'

const props = withDefaults(
  defineProps<{
    payment: PaymentResponse
    showUpload?: boolean
    locked?: boolean
  }>(),
  { showUpload: true, locked: false }
)

const emit = defineEmits<{
  (e: 'updated', payment: PaymentResponse): void
}>()

const toast = useToast()

const isLocked = computed(() => {
  // Use backend isActionable if available, otherwise fall back to parent-provided locked prop
  if (props.payment.isActionable !== undefined) {
    return !props.payment.isActionable && props.payment.status === 'Pending'
  }
  return props.locked && props.payment.status === 'Pending'
})

const installmentTitle = computed(() => {
  if (props.payment.isManual && props.payment.manualConceptLine) {
    return props.payment.manualConceptLine.description
  }
  return `Plazo ${props.payment.installmentNumber}`
})

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' }).format(
    parseDateSafe(dateStr)
  )

const copyToClipboard = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text)
    toast.add({
      severity: 'success',
      summary: 'Copiado',
      detail: 'Concepto copiado al portapapeles',
      life: 2000
    })
  } catch {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: 'No se pudo copiar al portapapeles',
      life: 3000
    })
  }
}

const handleProofUpdated = (updated: PaymentResponse) => {
  emit('updated', updated)
}
</script>

<template>
  <div
    class="rounded-lg border bg-white p-4"
    :class="isLocked ? 'border-gray-200 opacity-60' : 'border-gray-200'"
  >
    <!-- Header -->
    <div class="mb-3 flex items-center justify-between">
      <div class="flex items-center gap-2">
        <i v-if="isLocked" class="pi pi-lock text-xs text-gray-400" />
        <h3 class="text-sm font-semibold text-gray-900">
          {{ installmentTitle }}
        </h3>
        <span
          v-if="payment.isManual"
          class="inline-flex items-center rounded-full bg-purple-100 px-2 py-0.5 text-[0.65rem] font-medium text-purple-700"
        >
          Manual
        </span>
      </div>
      <PaymentStatusBadge :status="payment.status" />
    </div>

    <!-- Locked message -->
    <div v-if="isLocked" class="mb-3 rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-700">
      <i class="pi pi-info-circle mr-1" />
      Debes completar el pago anterior antes de realizar este.
    </div>

    <!-- Details -->
    <div class="mb-3 grid gap-2 text-sm sm:grid-cols-3">
      <div>
        <span class="text-xs font-medium text-gray-500">Importe</span>
        <p class="font-semibold text-gray-900">{{ formatCurrency(payment.amount) }}</p>
      </div>
      <div>
        <span class="text-xs font-medium text-gray-500">Vencimiento</span>
        <p class="text-gray-900">
          {{ payment.dueDate ? formatDate(payment.dueDate) : 'Inmediato' }}
        </p>
      </div>
      <div v-if="payment.transferConcept">
        <span class="text-xs font-medium text-gray-500">Concepto</span>
        <div class="flex items-center gap-1">
          <span class="font-mono text-xs font-semibold text-blue-700">
            {{ payment.transferConcept }}
          </span>
          <Button
            icon="pi pi-copy"
            text
            rounded
            size="small"
            aria-label="Copiar concepto"
            @click="copyToClipboard(payment.transferConcept!)"
          />
        </div>
      </div>
    </div>

    <!-- Concept lines breakdown -->
    <PaymentConceptLines
      :concept-lines="payment.conceptLines"
      :extra-concept-lines="payment.extraConceptLines"
      :manual-concept-line="payment.manualConceptLine"
    />

    <!-- Proof upload area -->
    <div v-if="showUpload" class="border-t border-gray-100 pt-3">
      <ProofUploader
        :payment-id="payment.id"
        :proof-file-url="payment.proofFileUrl"
        :proof-file-name="payment.proofFileName"
        :proof-uploaded-at="payment.proofUploadedAt"
        :status="payment.status"
        :admin-notes="payment.adminNotes"
        :disabled="isLocked"
        @uploaded="handleProofUpdated"
        @removed="handleProofUpdated"
      />
    </div>
  </div>
</template>

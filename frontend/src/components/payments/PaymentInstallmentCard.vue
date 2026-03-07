<script setup lang="ts">
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import PaymentStatusBadge from './PaymentStatusBadge.vue'
import ProofUploader from './ProofUploader.vue'
import type { PaymentResponse } from '@/types/payment'

const props = withDefaults(
  defineProps<{
    payment: PaymentResponse
    showUpload?: boolean
  }>(),
  { showUpload: true }
)

const emit = defineEmits<{
  (e: 'updated', payment: PaymentResponse): void
}>()

const toast = useToast()

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'long', year: 'numeric' }).format(
    new Date(dateStr)
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
  <div class="rounded-lg border border-gray-200 bg-white p-4">
    <!-- Header -->
    <div class="mb-3 flex items-center justify-between">
      <h3 class="text-sm font-semibold text-gray-900">
        Plazo {{ payment.installmentNumber }}
      </h3>
      <PaymentStatusBadge :status="payment.status" />
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

    <!-- Proof upload area -->
    <div v-if="showUpload" class="border-t border-gray-100 pt-3">
      <ProofUploader
        :payment-id="payment.id"
        :proof-file-url="payment.proofFileUrl"
        :proof-file-name="payment.proofFileName"
        :proof-uploaded-at="payment.proofUploadedAt"
        :status="payment.status"
        :admin-notes="payment.adminNotes"
        @uploaded="handleProofUpdated"
        @removed="handleProofUpdated"
      />
    </div>
  </div>
</template>

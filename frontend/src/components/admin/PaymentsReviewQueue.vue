<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import PaymentStatusBadge from '@/components/payments/PaymentStatusBadge.vue'
import PaymentConceptLines from '@/components/payments/PaymentConceptLines.vue'
import { usePayments } from '@/composables/usePayments'
import type { AdminPaymentResponse } from '@/types/payment'

const toast = useToast()
const { getPendingReviewPayments, confirmPayment, rejectPayment, loading, error } = usePayments()

const pendingPayments = ref<AdminPaymentResponse[]>([])
const initialLoading = ref(true)

// Confirm dialog
const showConfirmDialog = ref(false)
const confirmTarget = ref<AdminPaymentResponse | null>(null)
const confirmNotes = ref('')
const confirming = ref(false)

// Reject dialog
const showRejectDialog = ref(false)
const rejectTarget = ref<AdminPaymentResponse | null>(null)
const rejectNotes = ref('')
const rejecting = ref(false)

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(dateStr))

const isImage = (fileName: string | null): boolean => {
  if (!fileName) return false
  return /\.(jpg|jpeg|png|webp)$/i.test(fileName)
}

const openConfirmDialog = (payment: AdminPaymentResponse) => {
  confirmTarget.value = payment
  confirmNotes.value = ''
  showConfirmDialog.value = true
}

const openRejectDialog = (payment: AdminPaymentResponse) => {
  rejectTarget.value = payment
  rejectNotes.value = ''
  showRejectDialog.value = true
}

const handleConfirm = async () => {
  if (!confirmTarget.value) return
  confirming.value = true
  const result = await confirmPayment(confirmTarget.value.id, confirmNotes.value || undefined)
  confirming.value = false
  if (result) {
    pendingPayments.value = pendingPayments.value.filter((p) => p.id !== confirmTarget.value!.id)
    showConfirmDialog.value = false
    toast.add({
      severity: 'success',
      summary: 'Pago confirmado',
      detail: `Pago de ${confirmTarget.value.familyUnitName} confirmado.`,
      life: 4000
    })
  }
}

const handleReject = async () => {
  if (!rejectTarget.value || rejectNotes.value.length < 10) return
  rejecting.value = true
  const result = await rejectPayment(rejectTarget.value.id, rejectNotes.value)
  rejecting.value = false
  if (result) {
    pendingPayments.value = pendingPayments.value.filter((p) => p.id !== rejectTarget.value!.id)
    showRejectDialog.value = false
    toast.add({
      severity: 'info',
      summary: 'Pago rechazado',
      detail: `Se ha notificado a ${rejectTarget.value.familyUnitName}.`,
      life: 4000
    })
  }
}

onMounted(async () => {
  pendingPayments.value = await getPendingReviewPayments()
  initialLoading.value = false
})
</script>

<template>
  <div>
    <div v-if="initialLoading" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>

    <div v-else-if="pendingPayments.length === 0" class="py-8 text-center text-sm text-gray-400">
      No hay pagos pendientes de revisión
    </div>

    <div v-else class="space-y-4">
      <div
        v-for="payment in pendingPayments"
        :key="payment.id"
        class="rounded-lg border border-gray-200 bg-white p-4"
      >
        <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div class="flex-1">
            <div class="flex items-center gap-2">
              <span class="font-semibold text-gray-900">{{ payment.familyUnitName }}</span>
              <PaymentStatusBadge :status="payment.status" />
            </div>
            <p class="mt-0.5 text-sm text-gray-500">
              {{ payment.campEditionName }} · Plazo {{ payment.installmentNumber }}
            </p>
            <div class="mt-2 grid gap-2 text-sm sm:grid-cols-3">
              <div>
                <span class="text-xs text-gray-500">Importe</span>
                <p class="font-semibold">{{ formatCurrency(payment.amount) }}</p>
              </div>
              <div v-if="payment.transferConcept">
                <span class="text-xs text-gray-500">Concepto</span>
                <p class="font-mono text-xs">{{ payment.transferConcept }}</p>
              </div>
              <div v-if="payment.proofUploadedAt">
                <span class="text-xs text-gray-500">Subido</span>
                <p class="text-xs">{{ formatDate(payment.proofUploadedAt) }}</p>
              </div>
            </div>
          </div>

          <!-- Proof preview -->
          <div v-if="payment.proofFileUrl" class="shrink-0">
            <a :href="payment.proofFileUrl" target="_blank" rel="noopener noreferrer">
              <img
                v-if="isImage(payment.proofFileName)"
                :src="payment.proofFileUrl"
                :alt="payment.proofFileName ?? 'Justificante'"
                class="h-24 w-24 rounded border border-gray-200 object-cover"
              />
              <div
                v-else
                class="flex h-24 w-24 flex-col items-center justify-center rounded border border-gray-200 bg-gray-50"
              >
                <i class="pi pi-file-pdf text-2xl text-red-500" />
                <span class="mt-1 text-xs text-gray-500">Ver PDF</span>
              </div>
            </a>
          </div>
        </div>

        <!-- Concept lines -->
        <PaymentConceptLines
          :concept-lines="payment.conceptLines"
          :extra-concept-lines="payment.extraConceptLines"
        />

        <!-- Actions -->
        <div class="mt-3 flex justify-end gap-2 border-t border-gray-100 pt-3">
          <Button
            label="Rechazar"
            severity="danger"
            outlined
            size="small"
            icon="pi pi-times"
            @click="openRejectDialog(payment)"
          />
          <Button
            label="Confirmar"
            severity="success"
            size="small"
            icon="pi pi-check"
            @click="openConfirmDialog(payment)"
          />
        </div>
      </div>
    </div>

    <Message v-if="error" severity="error" :closable="false" class="mt-4">
      {{ error }}
    </Message>

    <!-- Confirm dialog -->
    <Dialog
      v-model:visible="showConfirmDialog"
      header="Confirmar pago"
      :modal="true"
      :style="{ width: '28rem' }"
    >
      <div class="space-y-3">
        <p class="text-sm text-gray-600">
          ¿Confirmar el pago de
          <strong>{{ confirmTarget?.familyUnitName }}</strong>
          por {{ confirmTarget ? formatCurrency(confirmTarget.amount) : '' }}?
        </p>
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">Notas (opcional)</label>
          <Textarea v-model="confirmNotes" :rows="2" class="w-full" placeholder="Notas opcionales..." />
        </div>
      </div>
      <template #footer>
        <Button label="Cancelar" severity="secondary" text @click="showConfirmDialog = false" />
        <Button
          label="Confirmar pago"
          severity="success"
          icon="pi pi-check"
          :loading="confirming"
          @click="handleConfirm"
        />
      </template>
    </Dialog>

    <!-- Reject dialog -->
    <Dialog
      v-model:visible="showRejectDialog"
      header="Rechazar justificante"
      :modal="true"
      :style="{ width: '28rem' }"
    >
      <div class="space-y-3">
        <p class="text-sm text-gray-600">
          Rechazar el justificante de
          <strong>{{ rejectTarget?.familyUnitName }}</strong>.
          Se podrá subir uno nuevo.
        </p>
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">
            Motivo del rechazo <span class="text-red-500">*</span>
          </label>
          <Textarea
            v-model="rejectNotes"
            :rows="3"
            class="w-full"
            placeholder="Explica el motivo del rechazo (mínimo 10 caracteres)..."
          />
          <p v-if="rejectNotes.length > 0 && rejectNotes.length < 10" class="mt-1 text-xs text-red-500">
            Mínimo 10 caracteres ({{ rejectNotes.length }}/10)
          </p>
        </div>
      </div>
      <template #footer>
        <Button label="Cancelar" severity="secondary" text @click="showRejectDialog = false" />
        <Button
          label="Rechazar"
          severity="danger"
          icon="pi pi-times"
          :loading="rejecting"
          :disabled="rejectNotes.length < 10"
          @click="handleReject"
        />
      </template>
    </Dialog>
  </div>
</template>

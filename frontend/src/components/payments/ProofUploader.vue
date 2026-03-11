<script setup lang="ts">
import { ref, computed } from 'vue'
import Button from 'primevue/button'
import FileUpload from 'primevue/fileupload'
import Message from 'primevue/message'
import { usePayments } from '@/composables/usePayments'
import type { PaymentResponse } from '@/types/payment'
import type { PaymentStatus } from '@/types/registration'

const props = defineProps<{
  paymentId: string
  proofFileUrl: string | null
  proofFileName: string | null
  proofUploadedAt: string | null
  status: PaymentStatus
  adminNotes: string | null
  disabled?: boolean
}>()

const emit = defineEmits<{
  (e: 'uploaded', payment: PaymentResponse): void
  (e: 'removed', payment: PaymentResponse): void
}>()

const { uploadProof, removeProof, loading, error } = usePayments()
const selectedFile = ref<File | null>(null)

const canUpload = computed(() => props.status === 'Pending' && !props.disabled)
const canRemove = computed(
  () => (props.status === 'Pending' || props.status === 'PendingReview') && props.proofFileUrl && !props.disabled
)
const isImage = computed(() => {
  if (!props.proofFileName) return false
  return /\.(jpg|jpeg|png|webp)$/i.test(props.proofFileName)
})
const wasRejected = computed(() => props.status === 'Pending' && !!props.adminNotes)
const hasConfirmationNote = computed(() => props.status === 'Completed' && !!props.adminNotes)

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(dateStr))

const onFileSelect = (event: { files: File[] }) => {
  selectedFile.value = event.files[0] ?? null
}

const handleUpload = async () => {
  if (!selectedFile.value) return
  const result = await uploadProof(props.paymentId, selectedFile.value)
  if (result) {
    selectedFile.value = null
    emit('uploaded', result)
  }
}

const handleRemove = async () => {
  const result = await removeProof(props.paymentId)
  if (result) {
    emit('removed', result)
  }
}
</script>

<template>
  <div class="space-y-3">
    <!-- Rejection message -->
    <Message v-if="wasRejected" severity="warn" :closable="false">
      <div>
        <strong>Justificante rechazado:</strong>
        <p class="mt-1 text-sm">{{ adminNotes }}</p>
        <p class="mt-1 text-xs text-gray-600">Puedes subir un nuevo justificante a continuación.</p>
      </div>
    </Message>

    <!-- Confirmation note -->
    <Message v-if="hasConfirmationNote" severity="success" :closable="false">
      <div>
        <strong>Nota de la administración:</strong>
        <p class="mt-1 text-sm">{{ adminNotes }}</p>
      </div>
    </Message>

    <!-- Proof preview -->
    <div v-if="proofFileUrl" class="rounded-lg border border-gray-200 bg-gray-50 p-3">
      <div class="flex items-start gap-3">
        <div v-if="isImage" class="shrink-0">
          <a :href="proofFileUrl" target="_blank" rel="noopener noreferrer">
            <img
              :src="proofFileUrl"
              :alt="proofFileName ?? 'Justificante'"
              class="h-20 w-20 rounded border border-gray-200 object-cover"
            />
          </a>
        </div>
        <div v-else class="flex shrink-0 items-center justify-center rounded border border-gray-200 bg-white p-3">
          <i class="pi pi-file-pdf text-2xl text-red-500" />
        </div>
        <div class="flex-1">
          <a
            :href="proofFileUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="text-sm font-medium text-blue-600 hover:underline"
          >
            {{ proofFileName ?? 'Justificante' }}
          </a>
          <p v-if="proofUploadedAt" class="mt-0.5 text-xs text-gray-500">
            Subido el {{ formatDate(proofUploadedAt) }}
          </p>
          <p v-if="status === 'PendingReview'" class="mt-1 text-xs text-blue-600">
            <i class="pi pi-clock mr-1" />En revisión por el administrador
          </p>
          <p v-if="status === 'Completed'" class="mt-1 text-xs text-green-600">
            <i class="pi pi-check-circle mr-1" />Pago confirmado
          </p>
        </div>
        <Button
          v-if="canRemove"
          icon="pi pi-trash"
          severity="danger"
          text
          rounded
          size="small"
          :loading="loading"
          aria-label="Eliminar justificante"
          @click="handleRemove"
        />
      </div>
    </div>

    <!-- Upload area -->
    <div v-if="canUpload && !proofFileUrl" class="space-y-2">
      <FileUpload
        mode="basic"
        accept=".jpg,.jpeg,.png,.webp,.pdf"
        :auto="false"
        choose-label="Seleccionar archivo"
        :disabled="loading"
        @select="onFileSelect"
      />
      <div v-if="selectedFile" class="flex items-center gap-2">
        <span class="text-sm text-gray-600">{{ selectedFile.name }}</span>
        <Button
          label="Subir justificante"
          icon="pi pi-upload"
          size="small"
          :loading="loading"
          @click="handleUpload"
        />
      </div>
    </div>

    <!-- Re-upload after rejection (when proof still exists from previous upload) -->
    <div v-if="canUpload && proofFileUrl && wasRejected" class="space-y-2">
      <p class="text-xs text-gray-500">Sube un nuevo justificante o elimina el actual:</p>
      <FileUpload
        mode="basic"
        accept=".jpg,.jpeg,.png,.webp,.pdf"
        :auto="false"
        choose-label="Seleccionar nuevo archivo"
        :disabled="loading"
        @select="onFileSelect"
      />
      <div v-if="selectedFile" class="flex items-center gap-2">
        <span class="text-sm text-gray-600">{{ selectedFile.name }}</span>
        <Button
          label="Subir justificante"
          icon="pi pi-upload"
          size="small"
          :loading="loading"
          @click="handleUpload"
        />
      </div>
    </div>

    <!-- Error message -->
    <Message v-if="error" severity="error" :closable="false" class="text-sm">
      {{ error }}
    </Message>
  </div>
</template>

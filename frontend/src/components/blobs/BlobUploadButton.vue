<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { useBlobStorage } from '@/composables/useBlobStorage'
import type { BlobFolder, BlobUploadResult } from '@/types/blob-storage'

const props = defineProps<{
  folder: BlobFolder
  contextId?: string
  generateThumbnail?: boolean
  accept?: string
  label?: string
  disabled?: boolean
}>()

const emit = defineEmits<{
  (e: 'uploaded', result: BlobUploadResult): void
  (e: 'error', message: string): void
}>()

const toast = useToast()
const { uploading, uploadError, uploadFile } = useBlobStorage()
const fileInput = ref<HTMLInputElement | null>(null)

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  const result = await uploadFile({
    file,
    folder: props.folder,
    contextId: props.contextId,
    generateThumbnail: props.generateThumbnail
  })

  // Clear input to allow re-upload of the same file
  input.value = ''

  if (result) {
    emit('uploaded', result)
  } else {
    const message = uploadError.value ?? 'Error al subir el archivo'
    emit('error', message)
    toast.add({
      severity: 'error',
      summary: 'Error al subir',
      detail: message,
      life: 5000
    })
  }
}
</script>

<template>
  <div class="flex items-center gap-2">
    <input
      ref="fileInput"
      type="file"
      class="hidden"
      :accept="accept"
      data-testid="blob-file-input"
      @change="onFileSelected"
    />
    <Button
      :label="uploading ? 'Subiendo...' : (label ?? 'Subir archivo')"
      icon="pi pi-upload"
      :loading="uploading"
      :disabled="disabled || uploading"
      data-testid="blob-upload-btn"
      @click="fileInput?.click()"
    />
  </div>
</template>

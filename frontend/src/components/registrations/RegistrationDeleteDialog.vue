<script setup lang="ts">
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'

defineProps<{
  visible: boolean
  loading: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  confirm: []
}>()
</script>

<template>
  <Dialog
    :visible="visible"
    header="Delete registration"
    :modal="true"
    :closable="!loading"
    :style="{ width: '28rem' }"
    @update:visible="emit('update:visible', $event)"
    data-testid="delete-registration-dialog"
  >
    <div class="flex items-start gap-3">
      <i class="pi pi-exclamation-triangle text-2xl text-red-500" />
      <div>
        <p class="text-gray-700">
          Are you sure you want to delete this registration? This action cannot be undone.
        </p>
        <p class="mt-2 text-sm text-gray-500">
          You will be able to register again for this camp edition.
        </p>
      </div>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button
          label="Cancel"
          severity="secondary"
          :disabled="loading"
          @click="emit('update:visible', false)"
          data-testid="delete-dialog-close-btn"
        />
        <Button
          label="Delete registration"
          severity="danger"
          icon="pi pi-trash"
          :loading="loading"
          @click="emit('confirm')"
          data-testid="delete-confirm-btn"
        />
      </div>
    </template>
  </Dialog>
</template>

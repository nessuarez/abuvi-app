<script setup lang="ts">
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'

defineProps<{
  visible: boolean
  registrationId: string
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
    header="Cancelar inscripción"
    :modal="true"
    :closable="!loading"
    :style="{ width: '28rem' }"
    @update:visible="emit('update:visible', $event)"
  >
    <p class="text-gray-700">
      ¿Seguro que quieres cancelar esta inscripción? Esta acción no se puede deshacer.
    </p>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button
          label="No, volver"
          severity="secondary"
          :disabled="loading"
          @click="emit('update:visible', false)"
          data-testid="cancel-dialog-close-btn"
        />
        <Button
          label="Sí, cancelar inscripción"
          severity="danger"
          :loading="loading"
          @click="emit('confirm')"
          data-testid="cancel-confirm-btn"
        />
      </div>
    </template>
  </Dialog>
</template>

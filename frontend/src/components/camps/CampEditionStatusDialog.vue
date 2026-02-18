<script setup lang="ts">
import { computed } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import Message from 'primevue/message'
import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
import type { CampEdition, CampEditionStatus } from '@/types/camp-edition'

interface Props {
  visible: boolean
  edition: CampEdition
  loading?: boolean
}

const props = withDefaults(defineProps<Props>(), { loading: false })

const emit = defineEmits<{
  'update:visible': [value: boolean]
  'confirm': [newStatus: CampEditionStatus]
}>()

const validNextStatus: Partial<Record<CampEditionStatus, CampEditionStatus>> = {
  Draft: 'Open',
  Open: 'Closed',
  Closed: 'Completed'
}

const nextStatus = computed(
  () => validNextStatus[props.edition.status] ?? null
)

const transitionWarning = computed(() => {
  if (props.edition.status === 'Draft') {
    return 'La edición se abrirá para inscripciones. Asegúrate de que la fecha de inicio no ha pasado.'
  }
  if (props.edition.status === 'Closed') {
    return 'La edición se marcará como completada. Solo es posible si la fecha de fin ya ha pasado.'
  }
  return null
})

const handleConfirm = () => {
  if (nextStatus.value) {
    emit('confirm', nextStatus.value)
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    header="Cambiar Estado"
    modal
    class="w-full max-w-md"
    data-testid="status-dialog"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4">
      <p class="text-sm text-gray-600">
        Vas a cambiar el estado de la edición
        <strong>{{ edition.year }}</strong>.
      </p>

      <div class="flex items-center justify-center gap-3">
        <CampEditionStatusBadge :status="edition.status" size="md" />
        <i class="pi pi-arrow-right text-gray-400" />
        <CampEditionStatusBadge v-if="nextStatus" :status="nextStatus" size="md" />
      </div>

      <Message v-if="transitionWarning" severity="warn" :closable="false" class="text-sm">
        {{ transitionWarning }}
      </Message>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button
          label="Cancelar"
          text
          :disabled="loading"
          @click="emit('update:visible', false)"
        />
        <Button
          label="Confirmar"
          :loading="loading"
          :disabled="!nextStatus || loading"
          data-testid="confirm-status-btn"
          @click="handleConfirm"
        />
      </div>
    </template>
  </Dialog>
</template>

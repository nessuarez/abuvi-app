<script setup lang="ts">
import { computed } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Divider from 'primevue/divider'
import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
import type { CampEdition, CampEditionStatus } from '@/types/camp-edition'
import { useAuthStore } from '@/stores/auth'

interface Props {
  visible: boolean
  edition: CampEdition
  loading?: boolean
}

const props = withDefaults(defineProps<Props>(), { loading: false })

const emit = defineEmits<{
  'update:visible': [value: boolean]
  'confirm': [newStatus: CampEditionStatus, force?: boolean]
}>()

const auth = useAuthStore()

const validNextStatus: Partial<Record<CampEditionStatus, CampEditionStatus>> = {
  Proposed: 'Draft',
  Draft: 'Open',
  Open: 'Closed',
  Closed: 'Completed'
}

const nextStatus = computed(
  () => validNextStatus[props.edition.status] ?? null
)

// Admin can roll back Open → Draft
const canRollbackToDraft = computed(
  () => props.edition.status === 'Open' && auth.isAdmin
)

// Admin transitioning Draft → Open with startDate in the past needs force=true
const needsForce = computed(() => {
  if (props.edition.status !== 'Draft') return false
  if (!auth.isAdmin) return false
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return new Date(props.edition.startDate) < today
})

const transitionWarning = computed(() => {
  if (props.edition.status === 'Draft' && needsForce.value) {
    return 'La fecha de inicio de esta edición ya ha pasado. Como administrador, puedes forzar la apertura igualmente.'
  }
  if (props.edition.status === 'Draft') {
    return 'La edición se abrirá para inscripciones. Asegúrate de que la fecha de inicio no ha pasado.'
  }
  if (props.edition.status === 'Closed') {
    return 'La edición se marcará como completada. Solo es posible si la fecha de fin ya ha pasado.'
  }
  return null
})

const rollbackWarning = 'Esta edición dejará de estar disponible para nuevas inscripciones mientras esté en borrador. Las inscripciones existentes no se verán afectadas.'

const handleConfirm = (targetStatus: CampEditionStatus) => {
  const force = targetStatus === 'Open' && needsForce.value ? true : undefined
  emit('confirm', targetStatus, force)
}

const handleRollback = () => {
  handleConfirm('Draft')
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

      <Message v-if="transitionWarning" :severity="needsForce ? 'warn' : 'info'" :closable="false" class="text-sm">
        {{ transitionWarning }}
      </Message>

      <!-- Admin-only rollback section -->
      <template v-if="canRollbackToDraft">
        <Divider />
        <div class="space-y-2">
          <p class="text-sm text-gray-600 font-medium">Acción de administrador</p>
          <Message severity="warn" :closable="false" class="text-sm">
            {{ rollbackWarning }}
          </Message>
        </div>
      </template>
    </div>

    <template #footer>
      <div class="flex justify-between gap-2">
        <!-- Rollback button — Admin-only, left-aligned -->
        <Button
          v-if="canRollbackToDraft"
          label="Volver a Borrador"
          severity="warn"
          outlined
          :loading="loading"
          data-testid="rollback-to-draft-btn"
          @click="handleRollback"
        />
        <div class="flex gap-2 ml-auto">
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
            @click="handleConfirm(nextStatus!)"
          />
        </div>
      </div>
    </template>
  </Dialog>
</template>

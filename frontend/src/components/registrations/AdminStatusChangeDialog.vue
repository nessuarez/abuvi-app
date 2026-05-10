<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import ToggleSwitch from 'primevue/toggleswitch'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { useRegistrations } from '@/composables/useRegistrations'
import type { RegistrationStatus, RegistrationResponse } from '@/types/registration'

const props = defineProps<{
  visible: boolean
  registrationId: string
  currentStatus: RegistrationStatus
  loading: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  changed: [registration: RegistrationResponse]
}>()

const toast = useToast()
const { changeStatus, error } = useRegistrations()

const selectedStatus = ref<RegistrationStatus | null>(null)
const notes = ref('')
const notifyUser = ref(true)
const submitting = ref(false)

const STATUS_LABELS: Record<RegistrationStatus, string> = {
  Pending:       'Pendiente',
  PartiallyPaid: 'Al corriente',
  FullyPaid:     'Pago completo',
  Confirmed:     'Confirmada',
  Draft:         'En revisión',
  Cancelled:     'Cancelada',
}

const VALID_TRANSITIONS: Partial<Record<RegistrationStatus, RegistrationStatus[]>> = {
  Pending:       ['PartiallyPaid', 'Confirmed'],
  PartiallyPaid: ['Pending', 'Confirmed'],
  FullyPaid:     ['Confirmed', 'Pending'],
  Confirmed:     ['Pending', 'PartiallyPaid'],
  Draft:         ['Pending', 'PartiallyPaid', 'FullyPaid', 'Confirmed'],
}

// Mirrors the backend ChangeStatusAsync switch arms that send an email
const STATUSES_WITH_EMAIL: RegistrationStatus[] = ['PartiallyPaid', 'Confirmed', 'Pending']

const notifyEnabled = computed(
  () => selectedStatus.value !== null && STATUSES_WITH_EMAIL.includes(selectedStatus.value)
)

watch(notifyEnabled, (enabled) => {
  if (!enabled) notifyUser.value = false
  else notifyUser.value = true
})

const validTargetStatuses = computed(() =>
  (VALID_TRANSITIONS[props.currentStatus] ?? []).map((s) => ({
    label: STATUS_LABELS[s],
    value: s,
  }))
)

const handleSubmit = async () => {
  if (!selectedStatus.value || !notes.value.trim()) return
  submitting.value = true
  const result = await changeStatus(props.registrationId, {
    newStatus: selectedStatus.value,
    notes: notes.value.trim(),
    notifyUser: notifyUser.value,
  })
  submitting.value = false
  if (result) {
    emit('changed', result)
    handleClose()
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value ?? 'Error al cambiar el estado',
      life: 5000,
    })
  }
}

const handleClose = () => {
  selectedStatus.value = null
  notes.value = ''
  notifyUser.value = true
  emit('update:visible', false)
}
</script>

<template>
  <Dialog
    :visible="visible"
    @update:visible="handleClose"
    header="Cambiar estado"
    :modal="true"
    :style="{ width: '30rem' }"
    data-testid="status-change-dialog"
  >
    <div class="space-y-4">
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Nuevo estado</label>
        <Select
          v-model="selectedStatus"
          :options="validTargetStatuses"
          option-label="label"
          option-value="value"
          placeholder="Selecciona estado"
          class="w-full"
          data-testid="status-select"
        />
      </div>
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Notas (obligatorio)</label>
        <Textarea
          v-model="notes"
          :rows="3"
          :maxlength="500"
          class="w-full"
          placeholder="Motivo del cambio de estado..."
          data-testid="status-notes"
        />
      </div>
      <div class="space-y-1">
        <div class="flex items-center gap-2">
          <ToggleSwitch
            v-model="notifyUser"
            input-id="notify-status-toggle"
            :disabled="!notifyEnabled"
          />
          <label
            for="notify-status-toggle"
            :class="['text-sm', notifyEnabled ? 'cursor-pointer text-gray-700' : 'cursor-not-allowed text-gray-400']"
          >
            Notificar a la familia
          </label>
        </div>
        <p v-if="selectedStatus && !notifyEnabled" class="text-xs text-gray-400">
          Este cambio de estado no envía notificación por correo.
        </p>
      </div>
    </div>
    <template #footer>
      <Button label="Cancelar" severity="secondary" text @click="handleClose" />
      <Button
        label="Cambiar estado"
        :loading="submitting"
        :disabled="!selectedStatus || !notes.trim()"
        @click="handleSubmit"
        data-testid="status-change-submit"
      />
    </template>
  </Dialog>
</template>

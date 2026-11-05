<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { useCampAttendance } from '@/composables/useCampAttendance'
import type { AttendanceSource } from '@/types/camp-attendance'

/**
 * "Yo estuve en este campamento".
 *
 * The cheapest feature in the archive and, per the original note, probably the one with
 * the highest return: it gives every member their own timeline and tells the archive who
 * to ask about an undated photo.
 */
interface Props {
  campEditionId: string
  attended: boolean
  /** Registration-derived attendance is real but cannot be withdrawn. */
  source?: AttendanceSource
}

const props = withDefaults(defineProps<Props>(), { source: 'None' })

const emit = defineEmits<{
  'update:attended': [attended: boolean]
}>()

const toast = useToast()
const { declare, withdraw, submitting, error } = useCampAttendance()

const localAttended = ref(props.attended)

/** Derived attendance has nothing to delete, so we do not offer a toggle that would fail. */
const isDerived = computed(() => props.source === 'Registration')

const label = computed(() => {
  if (isDerived.value) return 'Estuviste aquí'
  return localAttended.value ? 'Estuviste aquí' : 'Yo estuve aquí'
})

const toggle = async () => {
  if (isDerived.value) return

  const next = !localAttended.value
  const ok = next
    ? await declare(props.campEditionId)
    : await withdraw(props.campEditionId)

  if (!ok) {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value ?? 'No se pudo guardar tu asistencia',
      life: 5000,
    })
    return
  }

  localAttended.value = next
  emit('update:attended', next)
}
</script>

<template>
  <div class="flex flex-col items-start gap-1">
    <Button
      :label="label"
      :icon="localAttended || isDerived ? 'pi pi-check-circle' : 'pi pi-map-marker'"
      :severity="localAttended || isDerived ? 'success' : 'secondary'"
      :outlined="!localAttended && !isDerived"
      :loading="submitting"
      :disabled="isDerived"
      size="small"
      :aria-pressed="localAttended || isDerived"
      @click="toggle"
    />

    <p v-if="isDerived" class="text-xs text-gray-500">
      Consta por tu inscripción, no se puede quitar
    </p>
  </div>
</template>

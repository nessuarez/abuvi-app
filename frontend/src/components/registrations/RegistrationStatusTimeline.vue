<script setup lang="ts">
import Timeline from 'primevue/timeline'
import type { RegistrationStatusHistoryEntry, RegistrationStatus } from '@/types/registration'

defineProps<{
  history: RegistrationStatusHistoryEntry[]
}>()

const formatDateTime = (iso: string): string =>
  new Intl.DateTimeFormat('es-ES', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(iso))

const entryIcon = (entry: RegistrationStatusHistoryEntry): string => {
  if (entry.trigger === 'Automatic') return 'pi pi-bolt'
  if (entry.newStatus === 'Draft') return 'pi pi-pencil'
  if (entry.newStatus === 'Cancelled') return 'pi pi-times-circle'
  return 'pi pi-check-circle'
}

const STATUS_DESCRIPTIONS: Record<RegistrationStatus, string> = {
  Pending:       'Inscripción creada — Pendiente',
  PartiallyPaid: 'Junta confirmó primer pago — Al corriente',
  FullyPaid:     'Todos los pagos recibidos — Pago completo',
  Confirmed:     'Junta confirmó inscripción — Confirmada',
  Draft:         'Junta realizó cambios — En revisión',
  Cancelled:     'Inscripción cancelada',
}

const entryDescription = (entry: RegistrationStatusHistoryEntry): string =>
  STATUS_DESCRIPTIONS[entry.newStatus] ?? entry.newStatus
</script>

<template>
  <section v-if="history.length > 0">
    <h2 class="mb-4 text-base font-semibold text-gray-900">Historial de cambios</h2>
    <Timeline :value="[...history].sort((a, b) => a.changedAt.localeCompare(b.changedAt))">
      <template #marker="{ item }">
        <i :class="[entryIcon(item), 'text-gray-500']" />
      </template>
      <template #content="{ item }">
        <div class="pb-4">
          <p class="text-sm font-medium text-gray-800">{{ entryDescription(item) }}</p>
          <p class="text-xs text-gray-400">{{ formatDateTime(item.changedAt) }}</p>
          <p v-if="item.changedByUserName" class="text-xs text-gray-500">
            {{ item.trigger === 'Automatic' ? 'Sistema' : item.changedByUserName }}
          </p>
          <p v-if="item.notes" class="mt-1 text-xs italic text-gray-500">{{ item.notes }}</p>
        </div>
      </template>
    </Timeline>
  </section>
</template>

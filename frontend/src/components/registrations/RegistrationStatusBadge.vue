<script setup lang="ts">
import { computed } from 'vue'
import type { RegistrationStatus } from '@/types/registration'

const props = defineProps<{
  status: RegistrationStatus
}>()

const statusConfig = computed(() => {
  const configs: Record<RegistrationStatus, { label: string; colorClass: string }> = {
    Pending: { label: 'Pendiente', colorClass: 'bg-yellow-100 text-yellow-800' },
    Confirmed: { label: 'Confirmada', colorClass: 'bg-green-100 text-green-800' },
    Cancelled: { label: 'Cancelada', colorClass: 'bg-gray-100 text-gray-600' },
    Draft: { label: 'Borrador', colorClass: 'bg-blue-100 text-blue-800' }
  }
  return configs[props.status]
})
</script>

<template>
  <span
    class="inline-flex items-center rounded-full px-3 py-1 text-sm font-medium"
    :class="statusConfig.colorClass"
    data-testid="registration-status"
  >
    {{ statusConfig.label }}
  </span>
</template>

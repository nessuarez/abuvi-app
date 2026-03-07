<script setup lang="ts">
import { computed } from 'vue'
import type { PaymentStatus } from '@/types/registration'

const props = defineProps<{
  status: PaymentStatus
}>()

const statusConfig = computed(() => {
  const configs: Record<PaymentStatus, { label: string; colorClass: string }> = {
    Pending: { label: 'Pendiente', colorClass: 'bg-yellow-100 text-yellow-800' },
    PendingReview: { label: 'En revisión', colorClass: 'bg-blue-100 text-blue-800' },
    Completed: { label: 'Completado', colorClass: 'bg-green-100 text-green-800' },
    Failed: { label: 'Fallido', colorClass: 'bg-red-100 text-red-800' },
    Refunded: { label: 'Reembolsado', colorClass: 'bg-gray-100 text-gray-600' }
  }
  return configs[props.status]
})
</script>

<template>
  <span
    class="inline-flex items-center rounded-full px-3 py-1 text-xs font-medium"
    :class="statusConfig.colorClass"
    data-testid="payment-status"
  >
    {{ statusConfig.label }}
  </span>
</template>

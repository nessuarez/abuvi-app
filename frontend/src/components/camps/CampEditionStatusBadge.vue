<script setup lang="ts">
import { computed } from 'vue'
import type { CampEditionStatus } from '@/types/camp-edition'

const props = withDefaults(
  defineProps<{
    status: CampEditionStatus
    size?: 'sm' | 'md'
  }>(),
  { size: 'md' }
)

const statusConfig = computed(() => {
  const configs: Record<CampEditionStatus, { label: string; colorClass: string }> = {
    Proposed: { label: 'Propuesta', colorClass: 'bg-purple-100 text-purple-800' },
    Draft: { label: 'Borrador', colorClass: 'bg-gray-100 text-gray-700' },
    Open: { label: 'Abierto', colorClass: 'bg-green-100 text-green-800' },
    Closed: { label: 'Cerrado', colorClass: 'bg-orange-100 text-orange-800' },
    Completed: { label: 'Completado', colorClass: 'bg-blue-100 text-blue-800' }
  }
  return configs[props.status]
})
</script>

<template>
  <span
    class="inline-flex items-center rounded-full font-medium"
    :class="[
      statusConfig.colorClass,
      size === 'sm' ? 'px-2 py-0.5 text-xs' : 'px-3 py-1 text-sm'
    ]"
    data-testid="status-badge"
  >
    {{ statusConfig.label }}
  </span>
</template>

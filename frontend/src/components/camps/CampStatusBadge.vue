<script setup lang="ts">
import { computed } from 'vue'
import type { CampEditionStatus } from '@/types/camp-edition'

interface Props {
  status: CampEditionStatus
  size?: 'small' | 'medium'
}

const props = withDefaults(defineProps<Props>(), {
  size: 'medium'
})

const statusConfig = computed(() => {
  const configs: Record<CampEditionStatus, { label: string; colorClass: string }> = {
    Proposed: {
      label: 'Propuesta',
      colorClass: 'bg-yellow-100 text-yellow-800'
    },
    Draft: {
      label: 'Borrador',
      colorClass: 'bg-gray-100 text-gray-800'
    },
    Open: {
      label: 'Abierta',
      colorClass: 'bg-green-100 text-green-800'
    },
    Closed: {
      label: 'Cerrada',
      colorClass: 'bg-red-100 text-red-800'
    },
    Completed: {
      label: 'Completada',
      colorClass: 'bg-blue-100 text-blue-800'
    }
  }
  return configs[props.status]
})

const sizeClass = computed(() => {
  return props.size === 'small' ? 'px-2 py-0.5 text-xs' : 'px-2.5 py-1 text-sm'
})
</script>

<template>
  <span
    :class="[statusConfig.colorClass, sizeClass]"
    class="inline-flex items-center rounded-full font-semibold"
  >
    {{ statusConfig.label }}
  </span>
</template>

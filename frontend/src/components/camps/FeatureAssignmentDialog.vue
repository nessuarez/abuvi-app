<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import { FEATURE_APPLICABILITY_LABELS } from '@/types/accommodation-feature'
import type { AccommodationFeature, FeatureApplicabilityLevel } from '@/types/accommodation-feature'

const props = defineProps<{
  visible: boolean
  title: string
  initialFeatureIds: string[]
  availableFeatures: AccommodationFeature[]
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: [featureIds: string[]]
}>()

const selectedIds = ref<string[]>([])

watch(
  () => props.visible,
  (visible) => {
    if (visible) {
      selectedIds.value = [...props.initialFeatureIds]
    }
  },
)

const activeFeatures = computed(() => props.availableFeatures.filter((f) => f.isActive))

const groupedFeatures = computed(() => {
  const order: FeatureApplicabilityLevel[] = ['Zone', 'Accommodation', 'AccommodationType', 'Any']
  const groups = new Map<FeatureApplicabilityLevel, AccommodationFeature[]>()
  for (const level of order) groups.set(level, [])
  for (const f of activeFeatures.value) {
    groups.get(f.applicabilityLevel)!.push(f)
  }
  return (Array.from(groups.entries()) as [FeatureApplicabilityLevel, AccommodationFeature[]][]).filter(
    ([, items]) => items.length > 0,
  )
})

function toggleFeature(id: string) {
  const idx = selectedIds.value.indexOf(id)
  if (idx === -1) {
    selectedIds.value.push(id)
  } else {
    selectedIds.value.splice(idx, 1)
  }
}

function handleSave() {
  emit('saved', [...selectedIds.value])
}
</script>

<template>
  <Dialog
    :visible="visible"
    :header="title"
    modal
    class="w-full max-w-md"
    @update:visible="emit('update:visible', $event)"
  >
    <div v-if="activeFeatures.length === 0" class="py-4 text-center text-sm text-gray-400">
      No hay características activas disponibles.
    </div>

    <div v-else class="max-h-96 overflow-y-auto">
      <div v-for="[level, items] in groupedFeatures" :key="level" class="mb-4">
        <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500">
          {{ FEATURE_APPLICABILITY_LABELS[level] }}
        </p>
        <div class="space-y-1">
          <div
            v-for="feature in items"
            :key="feature.id"
            class="flex cursor-pointer items-center gap-3 rounded-lg border border-gray-100 px-3 py-2 hover:bg-gray-50"
            @click="toggleFeature(feature.id)"
          >
            <Checkbox
              :model-value="selectedIds.includes(feature.id)"
              binary
              @click.stop
              @update:model-value="toggleFeature(feature.id)"
            />
            <span class="text-lg leading-none">{{ feature.icon }}</span>
            <span class="text-sm text-gray-800">{{ feature.name }}</span>
          </div>
        </div>
      </div>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cancelar" severity="secondary" text @click="emit('update:visible', false)" />
        <Button label="Guardar" @click="handleSave" />
      </div>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import Image from 'primevue/image'
import ProgressSpinner from 'primevue/progressspinner'
import type { AccommodationMediaItem } from '@/types/accommodation-media'

const props = defineProps<{
  items: AccommodationMediaItem[]
  loading?: boolean
}>()

const sortedItems = computed(() =>
  [...props.items].sort((a, b) => {
    if (a.isPrimary !== b.isPrimary) return a.isPrimary ? -1 : 1
    return a.displayOrder - b.displayOrder
  })
)
</script>

<template>
  <div v-if="loading" class="flex items-center gap-2 py-2">
    <ProgressSpinner style="width: 24px; height: 24px" />
  </div>
  <div v-else-if="sortedItems.length > 0" class="flex gap-2 overflow-x-auto py-2">
    <div
      v-for="item in sortedItems"
      :key="item.id"
      class="relative flex-shrink-0"
    >
      <Image
        :src="item.thumbnailUrl ?? item.fileUrl"
        :preview="true"
        :preview-src="item.fileUrl"
        alt=""
        image-class="h-20 w-20 rounded-md object-cover"
        :class="item.isPrimary ? 'ring-2 ring-primary-500 rounded-md' : ''"
        :pt="{ image: { 'aria-label': 'Ver imagen' } }"
      />
    </div>
  </div>
</template>

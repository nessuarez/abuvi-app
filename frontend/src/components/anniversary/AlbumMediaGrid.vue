<script setup lang="ts">
import { computed } from 'vue'
import Skeleton from 'primevue/skeleton'
import Paginator from 'primevue/paginator'
import MediaCard from '@/components/anniversary/MediaCard.vue'
import type { AlbumMediaItem } from '@/types/album'
import type { MediaItemType } from '@/types/media-item'

interface Props {
  items: AlbumMediaItem[]
  totalCount: number
  page: number
  pageSize: number
  loading?: boolean
  error?: string | null
  activeType?: MediaItemType | null
  /** Shown when there is nothing to display. Deliberately caller-supplied. */
  emptyMessage?: string
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  error: null,
  activeType: null,
  emptyMessage: 'Aún no hay recuerdos aquí.',
})

const emit = defineEmits<{
  'update:page': [page: number]
  'update:type': [type: MediaItemType | null]
  open: [item: AlbumMediaItem]
}>()

/** "Todo" first, then the types. Interviews live under Audio — that is a playback detail. */
const typeFilters: { label: string; value: MediaItemType | null }[] = [
  { label: 'Todo', value: null },
  { label: 'Fotos', value: 'Photo' },
  { label: 'Audios', value: 'Audio' },
  { label: 'Vídeos', value: 'Video' },
  { label: 'Documentos', value: 'Document' },
]

const firstRecord = computed(() => (props.page - 1) * props.pageSize)

const onPage = (event: { page: number }) => emit('update:page', event.page + 1)
</script>

<template>
  <div>
    <!-- Type filter -->
    <div class="mb-6 flex flex-wrap justify-center gap-2" role="group" aria-label="Filtrar por tipo">
      <button
        v-for="filter in typeFilters"
        :key="filter.label"
        type="button"
        class="rounded-full px-4 py-1.5 text-sm font-medium transition-colors"
        :class="
          activeType === filter.value
            ? 'bg-amber-700 text-white'
            : 'bg-amber-100 text-amber-800 hover:bg-amber-200'
        "
        :aria-pressed="activeType === filter.value"
        @click="emit('update:type', filter.value)"
      >
        {{ filter.label }}
      </button>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-4">
      <div v-for="i in 8" :key="i" class="overflow-hidden rounded-xl bg-white shadow-sm">
        <Skeleton width="100%" height="12rem" />
        <div class="p-4">
          <Skeleton width="30%" height="1rem" class="mb-2" />
          <Skeleton width="60%" height="0.875rem" />
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="py-12 text-center">
      <i class="pi pi-exclamation-triangle mb-4 text-4xl text-red-400" aria-hidden="true" />
      <p class="text-lg text-gray-500">No se pudieron cargar los recuerdos.</p>
      <p class="mt-2 text-sm text-gray-400">{{ error }}</p>
    </div>

    <!-- Empty -->
    <div v-else-if="items.length === 0" class="py-12 text-center">
      <i class="pi pi-images mb-4 text-4xl text-amber-300" aria-hidden="true" />
      <p class="text-lg text-gray-500">{{ emptyMessage }}</p>
      <slot name="empty-action" />
    </div>

    <!-- Grid -->
    <template v-else>
      <div class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-4">
        <MediaCard
          v-for="item in items"
          :key="item.id"
          :item="item"
          @open="emit('open', $event)"
        />
      </div>

      <Paginator
        v-if="totalCount > pageSize"
        :rows="pageSize"
        :total-records="totalCount"
        :first="firstRecord"
        class="mt-8 bg-transparent"
        @page="onPage"
      />
    </template>
  </div>
</template>

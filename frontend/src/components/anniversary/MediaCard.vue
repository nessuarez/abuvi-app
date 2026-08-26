<script setup lang="ts">
import { computed } from 'vue'
import Image from 'primevue/image'
import type { AlbumMediaItem } from '@/types/album'

/**
 * One item in a grid, rendered according to its type.
 *
 * This is the component that keeps the feature from being photo-only: an audio
 * interview and a scanned document are first-class here, not afterthoughts.
 */
interface Props {
  item: AlbumMediaItem
}

const props = defineProps<Props>()

const emit = defineEmits<{
  open: [item: AlbumMediaItem]
}>()

const isAudio = computed(() => props.item.type === 'Audio' || props.item.type === 'Interview')

/** Undated items are the invitation to help, so the card says so rather than staying blank. */
const yearLabel = computed(() =>
  props.item.year === null ? 'Sin datar' : String(props.item.year)
)

const typeLabel = computed(
  () =>
    ({
      Photo: 'Foto',
      Video: 'Vídeo',
      Audio: 'Audio',
      Interview: 'Entrevista',
      Document: 'Documento',
    })[props.item.type]
)
</script>

<template>
  <article
    class="overflow-hidden rounded-xl bg-white shadow-sm transition-shadow hover:shadow-md"
  >
    <!-- Photo -->
    <template v-if="item.type === 'Photo'">
      <button
        type="button"
        class="block w-full overflow-hidden text-left"
        :aria-label="`Abrir ${item.title}`"
        @click="emit('open', item)"
      >
        <Image
          :src="item.thumbnailUrl ?? item.fileUrl"
          :alt="item.title"
          image-class="w-full h-48 object-cover transition-transform hover:scale-105"
          loading="lazy"
        />
      </button>
    </template>

    <!-- Video: poster only, never autoplay in a grid -->
    <template v-else-if="item.type === 'Video'">
      <button
        type="button"
        class="relative block w-full text-left"
        :aria-label="`Abrir ${item.title}`"
        @click="emit('open', item)"
      >
        <video
          :src="item.fileUrl"
          :poster="item.thumbnailUrl ?? undefined"
          preload="none"
          class="h-48 w-full bg-black object-cover"
        />
        <span
          class="absolute inset-0 flex items-center justify-center text-4xl text-white/90"
          aria-hidden="true"
        >
          <i class="pi pi-play-circle" />
        </span>
      </button>
    </template>

    <!-- Audio and interviews play in place: they need no lightbox to be useful -->
    <template v-else-if="isAudio">
      <div class="flex h-48 flex-col items-center justify-center gap-3 bg-amber-50 px-4">
        <i class="pi pi-volume-up text-4xl text-amber-400" aria-hidden="true" />
        <audio :src="item.fileUrl" controls preload="none" class="w-full" />
      </div>
    </template>

    <!-- Document -->
    <template v-else>
      <a
        :href="item.fileUrl"
        target="_blank"
        rel="noopener noreferrer"
        class="flex h-48 flex-col items-center justify-center gap-3 bg-gray-50 transition-colors hover:bg-gray-100"
      >
        <i class="pi pi-file-pdf text-4xl text-gray-400" aria-hidden="true" />
        <span class="px-4 text-center text-sm text-gray-600">Abrir documento</span>
      </a>
    </template>

    <div class="p-4">
      <div class="mb-1 flex items-center justify-between gap-2">
        <span class="text-xs font-semibold uppercase tracking-wide text-amber-700">
          {{ typeLabel }}
        </span>
        <span
          class="text-xs"
          :class="item.year === null ? 'font-medium text-amber-600' : 'text-gray-400'"
        >
          {{ yearLabel }}
        </span>
      </div>

      <h3 class="truncate text-sm font-medium text-gray-900" :title="item.title">
        {{ item.title }}
      </h3>

      <p v-if="item.mediaSourceName" class="mt-1 truncate text-xs text-gray-500">
        Aportado por {{ item.mediaSourceName }}
      </p>

      <div v-if="item.themes.length > 0" class="mt-2 flex flex-wrap gap-1">
        <RouterLink
          v-for="theme in item.themes"
          :key="theme.id"
          :to="{ name: 'anniversary-theme', params: { slug: theme.slug } }"
          class="rounded-full bg-amber-100 px-2 py-0.5 text-xs text-amber-800 hover:bg-amber-200"
        >
          {{ theme.name }}
        </RouterLink>
      </div>

      <button
        v-if="item.commentCount > 0"
        type="button"
        class="mt-2 flex items-center gap-1 text-xs text-gray-500 hover:text-amber-700"
        @click="emit('open', item)"
      >
        <i class="pi pi-comment" aria-hidden="true" />
        {{ item.commentCount }}
      </button>
    </div>
  </article>
</template>

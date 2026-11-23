<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import Skeleton from 'primevue/skeleton'
import AlbumMediaGrid from '@/components/anniversary/AlbumMediaGrid.vue'
import AttendanceButton from '@/components/anniversary/AttendanceButton.vue'
import { useAlbums } from '@/composables/useAlbums'
import { useMemories } from '@/composables/useMemories'
import type { AlbumMediaItem } from '@/types/album'
import type { MediaItemType } from '@/types/media-item'

/**
 * One camp edition's album: everything that survives from that year.
 *
 * Written stories are fetched separately so the two lists paginate independently —
 * a year with 300 photos and 4 relatos should not bury the relatos.
 */
const route = useRoute()

const { album, loading, error, fetchAlbum } = useAlbums()
const { memories, fetchMemories, loading: memoriesLoading } = useMemories()

const editionId = computed(() => route.params.editionId as string)
const page = ref(1)
const activeType = ref<MediaItemType | null>(null)

const load = async () => {
  await fetchAlbum(editionId.value, { page: page.value, type: activeType.value ?? undefined })
}

onMounted(async () => {
  await load()
  await fetchMemories({ approved: true, campEditionId: editionId.value })
})

watch([page, activeType], load)
watch(editionId, async () => {
  page.value = 1
  activeType.value = null
  await load()
  await fetchMemories({ approved: true, campEditionId: editionId.value })
})

const onTypeChange = (type: MediaItemType | null) => {
  activeType.value = type
  page.value = 1
}

const edition = computed(() => album.value?.edition ?? null)

const totalItems = computed(() => {
  const e = edition.value
  if (!e) return 0
  return e.photoCount + e.videoCount + e.audioCount + e.documentCount
})

const openItem = (_item: AlbumMediaItem) => {
  // The lightbox lands in a follow-up step; the grid already plays audio in place.
}
</script>

<template>
  <div class="min-h-screen bg-amber-50 pb-16">
    <!-- Header -->
    <header class="bg-amber-900 py-10 text-amber-50">
      <div class="mx-auto max-w-7xl px-6">
        <RouterLink
          to="/anniversary#historia"
          class="mb-4 inline-flex items-center gap-2 text-sm text-amber-200 hover:text-white"
        >
          <i class="pi pi-arrow-left" aria-hidden="true" />
          Volver al recorrido
        </RouterLink>

        <div v-if="loading && !edition">
          <Skeleton width="16rem" height="2.5rem" class="mb-3" />
          <Skeleton width="24rem" height="1.25rem" />
        </div>

        <template v-else-if="edition">
          <div class="flex flex-wrap items-end justify-between gap-4">
            <div>
              <h1 class="text-4xl font-bold md:text-5xl">{{ edition.year }}</h1>
              <p class="mt-1 text-xl text-amber-100">
                {{ edition.campName }}
                <span v-if="edition.campLocality" class="text-amber-300">
                  · {{ edition.campLocality }}
                </span>
              </p>
            </div>

            <AttendanceButton
              :camp-edition-id="edition.campEditionId"
              :attended="edition.viewerAttended"
            />
          </div>

          <!-- Counts. Zero is information too: it says "nothing survives", not "not loaded". -->
          <dl class="mt-6 flex flex-wrap gap-x-8 gap-y-2 text-sm text-amber-200">
            <div><dt class="inline">Fotos:</dt> <dd class="inline font-semibold text-white">{{ edition.photoCount }}</dd></div>
            <div><dt class="inline">Audios:</dt> <dd class="inline font-semibold text-white">{{ edition.audioCount }}</dd></div>
            <div><dt class="inline">Vídeos:</dt> <dd class="inline font-semibold text-white">{{ edition.videoCount }}</dd></div>
            <div><dt class="inline">Documentos:</dt> <dd class="inline font-semibold text-white">{{ edition.documentCount }}</dd></div>
            <div><dt class="inline">Relatos:</dt> <dd class="inline font-semibold text-white">{{ edition.memoryCount }}</dd></div>
          </dl>
        </template>
      </div>
    </header>

    <!-- Media -->
    <section aria-label="Recuerdos del campamento" class="mx-auto max-w-7xl px-6 py-12">
      <AlbumMediaGrid
        :items="album?.items ?? []"
        :total-count="album?.totalCount ?? 0"
        :page="album?.page ?? 1"
        :page-size="album?.pageSize ?? 24"
        :loading="loading"
        :error="error"
        :active-type="activeType"
        empty-message="Este campamento aún no tiene recuerdos."
        @update:page="page = $event"
        @update:type="onTypeChange"
        @open="openItem"
      >
        <template #empty-action>
          <RouterLink
            to="/anniversary#subir-recuerdo"
            class="mt-4 inline-block font-medium text-amber-700 underline hover:text-amber-900"
          >
            ¿Tienes alguno? Compártelo
          </RouterLink>
        </template>
      </AlbumMediaGrid>
    </section>

    <!-- Relatos -->
    <section
      v-if="memoriesLoading || memories.length > 0"
      aria-label="Relatos del campamento"
      class="mx-auto max-w-4xl px-6 pb-12"
    >
      <h2 class="mb-6 text-2xl font-bold text-amber-900">Relatos</h2>

      <div v-if="memoriesLoading" class="space-y-4">
        <Skeleton v-for="i in 2" :key="i" width="100%" height="6rem" />
      </div>

      <article
        v-for="memory in memories"
        v-else
        :key="memory.id"
        class="mb-4 rounded-xl bg-white p-6 shadow-sm"
      >
        <h3 class="mb-2 font-semibold text-gray-900">{{ memory.title }}</h3>
        <p class="whitespace-pre-line text-gray-700">{{ memory.content }}</p>
        <p class="mt-3 text-sm text-gray-500">— {{ memory.authorName }}</p>
      </article>
    </section>

    <p v-if="edition && totalItems === 0 && memories.length === 0" class="sr-only">
      Álbum vacío
    </p>
  </div>
</template>

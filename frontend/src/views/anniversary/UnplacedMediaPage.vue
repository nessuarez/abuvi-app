<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import ToggleSwitch from 'primevue/toggleswitch'
import AlbumMediaGrid from '@/components/anniversary/AlbumMediaGrid.vue'
import { useAlbums } from '@/composables/useAlbums'
import type { AlbumMediaItem } from '@/types/album'
import type { MediaItemType } from '@/types/media-item'

/**
 * The "sin ubicar" pile.
 *
 * A waiting room, not a rejects bin: everything here is expected to leave it. The copy
 * leads with the invitation rather than apologising for the gap.
 */
const { unplaced, loading, error, fetchUnplaced } = useAlbums()

const page = ref(1)
const activeType = ref<MediaItemType | null>(null)
const suggestedForMe = ref(false)

const load = () =>
  fetchUnplaced({
    page: page.value,
    type: activeType.value ?? undefined,
    suggestedForMe: suggestedForMe.value,
  })

onMounted(load)
watch([page, activeType, suggestedForMe], load)

const onTypeChange = (type: MediaItemType | null) => {
  activeType.value = type
  page.value = 1
}

const openItem = (_item: AlbumMediaItem) => {
  // The dating panel lands with the lightbox in a follow-up step.
}
</script>

<template>
  <div class="min-h-screen bg-amber-50 pb-16">
    <header class="bg-amber-900 py-10 text-amber-50">
      <div class="mx-auto max-w-7xl px-6">
        <RouterLink
          to="/anniversary"
          class="mb-4 inline-flex items-center gap-2 text-sm text-amber-200 hover:text-white"
        >
          <i class="pi pi-arrow-left" aria-hidden="true" />
          Volver al aniversario
        </RouterLink>

        <h1 class="text-4xl font-bold md:text-5xl">Sin ubicar</h1>
        <p class="mt-2 max-w-2xl text-amber-100">
          Estos recuerdos aún no tienen campamento. Si reconoces alguno, ayúdanos a ubicarlo.
        </p>

        <label class="mt-6 flex w-fit items-center gap-3 text-sm text-amber-100">
          <ToggleSwitch v-model="suggestedForMe" />
          Enseñarme solo los que podría reconocer
        </label>
      </div>
    </header>

    <section class="mx-auto max-w-7xl px-6 py-12">
      <AlbumMediaGrid
        :items="unplaced?.items ?? []"
        :total-count="unplaced?.totalCount ?? 0"
        :page="unplaced?.page ?? 1"
        :page-size="unplaced?.pageSize ?? 24"
        :loading="loading"
        :error="error"
        :active-type="activeType"
        :empty-message="
          suggestedForMe
            ? 'No hay recuerdos sin ubicar que encajen contigo.'
            : 'No queda nada sin ubicar. Todo el archivo tiene campamento.'
        "
        @update:page="page = $event"
        @update:type="onTypeChange"
        @open="openItem"
      />
    </section>
  </div>
</template>

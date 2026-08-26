<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import AlbumMediaGrid from '@/components/anniversary/AlbumMediaGrid.vue'
import { useMediaThemes } from '@/composables/useMediaThemes'
import type { AlbumMediaItem } from '@/types/album'
import type { MediaItemType } from '@/types/media-item'

/**
 * One theme across every edition.
 *
 * This is the view that makes the second axis visible: the same subject, decades apart.
 */
const route = useRoute()
const { themeItems, loading, error, fetchThemeItems } = useMediaThemes()

const slug = computed(() => route.params.slug as string)
const page = ref(1)
const activeType = ref<MediaItemType | null>(null)

const load = () =>
  fetchThemeItems(slug.value, {
    page: page.value,
    type: activeType.value ?? undefined,
  })

onMounted(load)
watch([page, activeType], load)
watch(slug, () => {
  page.value = 1
  activeType.value = null
  load()
})

const theme = computed(() => themeItems.value?.theme ?? null)

const spanLabel = computed(() => {
  const t = theme.value
  if (!t || t.firstYear === null || t.lastYear === null) return null
  return t.firstYear === t.lastYear
    ? `Solo consta en ${t.firstYear}`
    : `Este tema aparece entre ${t.firstYear} y ${t.lastYear}`
})

const onTypeChange = (type: MediaItemType | null) => {
  activeType.value = type
  page.value = 1
}

const openItem = (_item: AlbumMediaItem) => {
  // Lightbox lands in a follow-up step.
}
</script>

<template>
  <div class="min-h-screen bg-amber-50 pb-16">
    <header class="bg-amber-900 py-10 text-amber-50">
      <div class="mx-auto max-w-7xl px-6">
        <RouterLink
          :to="{ name: 'anniversary-themes' }"
          class="mb-4 inline-flex items-center gap-2 text-sm text-amber-200 hover:text-white"
        >
          <i class="pi pi-arrow-left" aria-hidden="true" />
          Todos los temas
        </RouterLink>

        <h1 class="text-4xl font-bold md:text-5xl">{{ theme?.name ?? 'Tema' }}</h1>
        <p v-if="theme?.description" class="mt-2 max-w-2xl text-amber-100">
          {{ theme.description }}
        </p>
        <p v-if="spanLabel" class="mt-3 text-sm text-amber-200">{{ spanLabel }}</p>
      </div>
    </header>

    <section class="mx-auto max-w-7xl px-6 py-12">
      <AlbumMediaGrid
        :items="themeItems?.items ?? []"
        :total-count="themeItems?.totalCount ?? 0"
        :page="themeItems?.page ?? 1"
        :page-size="themeItems?.pageSize ?? 24"
        :loading="loading"
        :error="error"
        :active-type="activeType"
        empty-message="Todavía no hay recuerdos con este tema."
        @update:page="page = $event"
        @update:type="onTypeChange"
        @open="openItem"
      />
    </section>
  </div>
</template>

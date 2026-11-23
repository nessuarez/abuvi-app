<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import Skeleton from 'primevue/skeleton'
import { useMediaThemes } from '@/composables/useMediaThemes'

/**
 * The themes catalogue.
 *
 * A theme cuts across editions — San Abuvino happened in many different years — so the
 * year span is the headline on each card, not the item count.
 */
const { themes, loading, error, fetchCatalogue } = useMediaThemes()

onMounted(() => fetchCatalogue())

const spanLabel = (first: number | null, last: number | null): string => {
  if (first === null || last === null) return 'Sin datar todavía'
  return first === last ? `${first}` : `${first} – ${last}`
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
        <h1 class="text-4xl font-bold md:text-5xl">Temas</h1>
        <p class="mt-2 max-w-2xl text-amber-100">
          Lo que se repite campamento tras campamento. Un mismo tema atraviesa muchos años.
        </p>
      </div>
    </header>

    <section class="mx-auto max-w-7xl px-6 py-12">
      <div v-if="loading" class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <Skeleton v-for="i in 6" :key="i" width="100%" height="8rem" />
      </div>

      <div v-else-if="error" class="py-12 text-center">
        <i class="pi pi-exclamation-triangle mb-4 text-4xl text-red-400" aria-hidden="true" />
        <p class="text-lg text-gray-500">{{ error }}</p>
      </div>

      <div v-else-if="themes.length === 0" class="py-12 text-center">
        <i class="pi pi-tags mb-4 text-4xl text-amber-300" aria-hidden="true" />
        <p class="text-lg text-gray-500">Todavía no hay temas.</p>
        <p class="mt-2 text-sm text-gray-400">
          Un administrador puede crearlos desde el panel de administración.
        </p>
      </div>

      <div v-else class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <RouterLink
          v-for="theme in themes"
          :key="theme.id"
          :to="{ name: 'anniversary-theme', params: { slug: theme.slug } }"
          class="rounded-xl bg-white p-6 shadow-sm transition-shadow hover:shadow-md"
        >
          <h2 class="text-lg font-semibold text-amber-900">{{ theme.name }}</h2>
          <p v-if="theme.description" class="mt-1 text-sm text-gray-600">
            {{ theme.description }}
          </p>

          <p class="mt-4 text-sm text-gray-500">
            <span class="font-medium text-gray-700">{{ theme.itemCount }}</span> recuerdos
            ·
            <span class="font-medium text-gray-700">{{ spanLabel(theme.firstYear, theme.lastYear) }}</span>
          </p>

          <p v-if="theme.undatedCount > 0" class="mt-1 text-xs text-amber-700">
            {{ theme.undatedCount }} sin datar — puedes ayudar
          </p>
        </RouterLink>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import Skeleton from 'primevue/skeleton'
import { useCampAttendance } from '@/composables/useCampAttendance'

/**
 * "Has estado en 14 campamentos".
 *
 * Shows every edition, attended or not, so the archive reads as a personal history
 * rather than a filtered list.
 */
const { timeline, loading, error, fetchTimeline } = useCampAttendance()

onMounted(fetchTimeline)

const total = computed(() => timeline.value?.totalEditionsAttended ?? 0)

const headline = computed(() => {
  if (total.value === 0) return 'Todavía no has marcado ningún campamento'
  if (total.value === 1) return 'Has estado en 1 campamento'
  return `Has estado en ${total.value} campamentos`
})
</script>

<template>
  <div class="min-h-screen bg-amber-50 pb-16">
    <header class="bg-amber-900 py-10 text-amber-50">
      <div class="mx-auto max-w-4xl px-6">
        <RouterLink
          to="/anniversary"
          class="mb-4 inline-flex items-center gap-2 text-sm text-amber-200 hover:text-white"
        >
          <i class="pi pi-arrow-left" aria-hidden="true" />
          Volver al aniversario
        </RouterLink>

        <h1 class="text-4xl font-bold md:text-5xl">{{ headline }}</h1>
        <p v-if="total === 0" class="mt-2 text-amber-100">
          Marca los campamentos a los que fuiste desde cada álbum y aparecerán aquí.
        </p>
      </div>
    </header>

    <section class="mx-auto max-w-4xl px-6 py-12">
      <div v-if="loading" class="space-y-3">
        <Skeleton v-for="i in 8" :key="i" width="100%" height="3.5rem" />
      </div>

      <div v-else-if="error" class="py-12 text-center">
        <i class="pi pi-exclamation-triangle mb-4 text-4xl text-red-400" aria-hidden="true" />
        <p class="text-lg text-gray-500">{{ error }}</p>
      </div>

      <ol v-else class="space-y-2">
        <li
          v-for="entry in timeline?.entries ?? []"
          :key="entry.campEditionId"
          class="flex items-center gap-4 rounded-xl px-4 py-3 transition-colors"
          :class="entry.attended ? 'bg-white shadow-sm' : 'bg-transparent'"
        >
          <span
            class="w-14 shrink-0 text-lg font-bold"
            :class="entry.attended ? 'text-amber-800' : 'text-gray-300'"
          >
            {{ entry.year }}
          </span>

          <span class="min-w-0 flex-1">
            <RouterLink
              :to="{ name: 'anniversary-album', params: { editionId: entry.campEditionId } }"
              class="block truncate font-medium"
              :class="entry.attended ? 'text-gray-900 hover:text-amber-700' : 'text-gray-400 hover:text-gray-600'"
            >
              {{ entry.campName }}
            </RouterLink>
            <span
              v-if="entry.attendanceSource === 'Registration'"
              class="text-xs text-gray-500"
            >
              Consta por tu inscripción
            </span>
          </span>

          <span v-if="entry.mediaCount > 0" class="shrink-0 text-sm text-gray-400">
            {{ entry.mediaCount }}
          </span>

          <i
            v-if="entry.attended"
            class="pi pi-check-circle shrink-0 text-green-600"
            aria-label="Asististe"
          />
        </li>
      </ol>
    </section>
  </div>
</template>

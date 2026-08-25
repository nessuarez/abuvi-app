<script setup lang="ts">
import { ref, watch } from 'vue'
import type { CampHistoryVenue } from '@/types/camp-history'

interface Props {
  venues: CampHistoryVenue[]
  selectedYear: number | null
  selectedCampId: string | null
  heightClass?: string
}

const props = withDefaults(defineProps<Props>(), {
  heightClass: 'lg:h-[600px]'
})
const emit = defineEmits<{
  selectYear: [year: number]
  selectVenue: [campId: string]
}>()

const rows = ref<Record<string, HTMLElement | null>>({})

const setRow = (campId: string, el: unknown) => {
  rows.value[campId] = (el as HTMLElement | null) ?? null
}

const prefersReducedMotion = (): boolean =>
  typeof window !== 'undefined' &&
  typeof window.matchMedia === 'function' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches

// The map drives the list as much as the list drives the map, so the selected venue has to
// come into view on its own. 'nearest' rather than 'center': centring yanks the list on
// every step of presentation mode.
watch(
  () => props.selectedCampId,
  (campId) => {
    if (!campId) return
    rows.value[campId]?.scrollIntoView({
      block: 'nearest',
      behavior: prefersReducedMotion() ? 'auto' : 'smooth'
    })
  }
)
</script>

<template>
  <div
    :class="heightClass"
    class="overflow-y-auto rounded-lg border border-amber-200 bg-white"
    aria-label="Sedes de los campamentos"
    role="region"
  >
    <ul class="divide-y divide-amber-100">
      <li v-for="venue in venues" :key="venue.campId" :ref="(el) => setRow(venue.campId, el)">
        <div
          class="w-full px-4 py-3 transition-colors"
          :class="
            venue.campId === selectedCampId ? 'bg-amber-50' : 'bg-white hover:bg-amber-50/60'
          "
        >
          <button
            type="button"
            class="w-full text-left"
            @click="emit('selectVenue', venue.campId)"
          >
            <div class="flex items-baseline justify-between gap-2">
              <span class="font-semibold text-amber-900">{{ venue.campName }}</span>
              <span
                v-if="venue.totalEditionsAtVenue > 1"
                class="shrink-0 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800"
              >
                {{ venue.totalEditionsAtVenue }} ediciones
              </span>
            </div>
            <span v-if="venue.location" class="text-sm text-gray-500">{{ venue.location }}</span>
          </button>

          <div class="mt-2 flex flex-wrap gap-1">
            <button
              v-for="year in venue.years"
              :key="year"
              type="button"
              class="rounded-full px-2 py-0.5 text-xs font-semibold transition-colors"
              :class="
                year === selectedYear
                  ? 'bg-amber-500 text-white'
                  : 'bg-amber-100 text-amber-800 hover:bg-amber-200'
              "
              :aria-label="`Edición de ${year} en ${venue.campName}`"
              :aria-current="year === selectedYear ? 'true' : undefined"
              @click="emit('selectYear', year)"
            >
              {{ year }}
            </button>
          </div>
        </div>
      </li>
    </ul>
  </div>
</template>

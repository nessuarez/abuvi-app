<script setup lang="ts">
import { ref, watch } from 'vue'
import type { CampHistoryEntry } from '@/types/camp-history'

interface Props {
  entries: CampHistoryEntry[]
  selectedYear: number | null
}

const props = defineProps<Props>()
const emit = defineEmits<{
  selectYear: [year: number]
}>()

const chips = ref<Record<number, HTMLElement | null>>({})

const setChip = (year: number, el: unknown) => {
  chips.value[year] = (el as HTMLElement | null) ?? null
}

const prefersReducedMotion = (): boolean =>
  typeof window !== 'undefined' &&
  typeof window.matchMedia === 'function' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches

/** A thin divider before each decade makes fifty chips scannable. */
const startsDecade = (year: number): boolean => year % 10 === 0

// Presentation mode walks the strip unattended, so the current year has to stay visible.
watch(
  () => props.selectedYear,
  (year) => {
    if (year == null) return
    chips.value[year]?.scrollIntoView({
      inline: 'center',
      block: 'nearest',
      behavior: prefersReducedMotion() ? 'auto' : 'smooth'
    })
  }
)
</script>

<template>
  <div class="overflow-x-auto pb-2" role="group" aria-label="Años con campamento">
    <div class="flex min-w-max items-end gap-1 px-1">
      <template v-for="entry in entries" :key="entry.year">
        <div v-if="startsDecade(entry.year)" class="mx-1 h-8 w-px shrink-0 bg-amber-300" />
        <button
          :ref="(el) => setChip(entry.year, el)"
          type="button"
          class="flex shrink-0 flex-col items-center gap-1 rounded-md px-2 py-1 transition-colors"
          :class="
            entry.year === selectedYear
              ? 'bg-amber-500 text-white'
              : 'text-amber-900 hover:bg-amber-100'
          "
          :aria-label="`${entry.year} en ${entry.campName}${entry.photoCount === 0 ? ', sin recuerdos' : `, ${entry.photoCount} recuerdos`}`"
          :aria-current="entry.year === selectedYear ? 'true' : undefined"
          @click="emit('selectYear', entry.year)"
        >
          <span class="text-xs font-semibold tabular-nums">{{ entry.year }}</span>
          <!-- Filled where memories survive, hollow where they do not: the gaps are the point. -->
          <span
            class="h-1.5 w-1.5 rounded-full border"
            :class="
              entry.photoCount > 0
                ? entry.year === selectedYear
                  ? 'border-white bg-white'
                  : 'border-amber-500 bg-amber-500'
                : entry.year === selectedYear
                  ? 'border-white bg-transparent'
                  : 'border-amber-400 bg-transparent'
            "
            :data-has-photos="entry.photoCount > 0"
          />
        </button>
      </template>
    </div>
  </div>
</template>

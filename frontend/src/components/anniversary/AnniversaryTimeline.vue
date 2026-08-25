<script setup lang="ts">
import Timeline from 'primevue/timeline'

/** A hand-picked moment in the association's history. Verified text only — see AnniversaryPage. */
interface AnniversaryMilestone {
  year: number
  title: string
  description: string
}

interface Props {
  milestones: AnniversaryMilestone[]
  selectedYear?: number | null
}

defineProps<Props>()
const emit = defineEmits<{
  selectYear: [year: number]
}>()
</script>

<template>
  <section aria-label="Historia de ABUVI" class="mx-auto max-w-6xl px-6">
    <div class="mb-12 text-center">
      <h2 class="mb-4 text-3xl font-bold text-amber-900 md:text-4xl">Nuestra historia</h2>
      <p class="mx-auto max-w-2xl text-gray-600">
        Cincuenta años de recuerdos, familias y momentos únicos en la naturaleza.
      </p>
    </div>

    <!-- Mobile: vertical timeline -->
    <div class="md:hidden">
      <Timeline :value="milestones" layout="vertical" align="alternate">
        <template #marker="{ item }">
          <button
            type="button"
            class="flex h-10 w-10 items-center justify-center rounded-full bg-amber-500 shadow-md"
            :class="item.year === selectedYear ? 'ring-2 ring-amber-700 ring-offset-2' : ''"
            :aria-label="`Ir a ${item.year}`"
            :aria-current="item.year === selectedYear ? 'true' : undefined"
            @click="emit('selectYear', item.year)"
          >
            <i class="pi pi-star-fill text-white text-sm" />
          </button>
        </template>
        <template #content="{ item }">
          <article class="mb-6 rounded-xl bg-white p-4 shadow-sm">
            <span class="text-sm font-bold text-amber-600">{{ item.year }}</span>
            <h3 class="mt-1 font-semibold text-gray-900 leading-snug">{{ item.title }}</h3>
            <p class="mt-1 text-sm text-gray-600">{{ item.description }}</p>
          </article>
        </template>
      </Timeline>
    </div>

    <!-- Desktop: horizontal timeline (scrollable) -->
    <div class="hidden overflow-x-auto md:block">
      <div class="min-w-[900px] pb-4">
        <Timeline :value="milestones" layout="horizontal" align="top">
          <template #marker="{ item }">
            <button
              type="button"
              class="flex h-10 w-10 items-center justify-center rounded-full bg-amber-500 shadow-md"
              :class="item.year === selectedYear ? 'ring-2 ring-amber-700 ring-offset-2' : ''"
              :aria-label="`Ir a ${item.year}`"
              :aria-current="item.year === selectedYear ? 'true' : undefined"
              @click="emit('selectYear', item.year)"
            >
              <i class="pi pi-star-fill text-white text-sm" />
            </button>
          </template>
          <template #content="{ item }">
            <article class="w-32 rounded-xl bg-white p-3 shadow-sm text-center">
              <span class="text-xs font-bold text-amber-600">{{ item.year }}</span>
              <h3 class="mt-1 text-xs font-semibold text-gray-900 leading-tight">
                {{ item.title }}
              </h3>
            </article>
          </template>
          <template #opposite="{ item }">
            <div class="w-32 px-1">
              <p class="text-xs text-gray-500 text-center leading-tight">
                {{ item.description }}
              </p>
            </div>
          </template>
        </Timeline>
      </div>
    </div>
  </section>
</template>

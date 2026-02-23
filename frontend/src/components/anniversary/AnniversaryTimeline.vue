<script setup lang="ts">
import Timeline from 'primevue/timeline'

interface Milestone {
  year: number
  title: string
  description: string
}

const milestones: Milestone[] = [
  {
    year: 1976,
    title: 'Primer campamento en Miraflores de la Sierra',
    description:
      'ABUVI celebra su primer campamento en las sierras de Madrid, dando inicio a una tradición familiar única.',
  },
  {
    year: 1982,
    title: 'Primera edición con más de 50 familias',
    description:
      'La comunidad crece y el campamento supera por primera vez las 50 familias participantes.',
  },
  {
    year: 1990,
    title: 'Fundación de la Junta Directiva formal',
    description:
      'Se constituye oficialmente la Junta Directiva de ABUVI, consolidando su estructura organizativa.',
  },
  {
    year: 2000,
    title: '25º Aniversario: campamento de verano histórico',
    description:
      'Un verano histórico para celebrar el cuarto de siglo de ABUVI con actividades y ediciones especiales.',
  },
  {
    year: 2010,
    title: 'Primer campamento de invierno',
    description:
      'ABUVI amplía su calendario con el primer campamento invernal, descubriendo aventuras en la nieve.',
  },
  {
    year: 2020,
    title: 'Pandemia: campamento virtual',
    description:
      'La comunidad demuestra su resiliencia y creatividad celebrando el campamento en formato digital.',
  },
  {
    year: 2026,
    title: '50º Aniversario: celebración especial',
    description:
      '¡Medio siglo de aventuras, amistad y naturaleza! Hoy celebramos este hito histórico contigo.',
  },
]
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
        <template #marker>
          <div
            class="flex h-10 w-10 items-center justify-center rounded-full bg-amber-500 shadow-md"
          >
            <i class="pi pi-star-fill text-white text-sm" />
          </div>
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
          <template #marker>
            <div
              class="flex h-10 w-10 items-center justify-center rounded-full bg-amber-500 shadow-md"
            >
              <i class="pi pi-star-fill text-white text-sm" />
            </div>
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

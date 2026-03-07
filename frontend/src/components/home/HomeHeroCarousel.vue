<script setup lang="ts">
import { computed, ref } from 'vue'
import Galleria from 'primevue/galleria'

import imgGrupo from '@/assets/images/grupo-abuvi.jpg'
import imgWhatsapp from '@/assets/images/swello-IRWj0hdSbM4-unsplash.jpg'
import imgTents from '@/assets/images/camping-tents-generic.jpg'
import imgFriends from '@/assets/images/camping-friends.jpg'

interface HeroSlide {
  image: string
  imageAlt: string
  headline: string
  description: string
  ctaLabel: string
  ctaPath: string
  external?: boolean
}

const slides: HeroSlide[] = [
  {
    image: imgGrupo,
    imageAlt: 'Comunidad ABUVI reunida',
    headline: '50 Años de Buena Vida',
    description:
      'Desde 1976 creando recuerdos. Celebra con nosotros medio siglo de ABUVI compartiendo tus historias y fotos.',
    ctaLabel: 'Participar en el Aniversario',
    ctaPath: '/anniversary',
  },
  {
    image: imgWhatsapp,
    imageAlt: 'Comunidad WhatsApp ABUVI',
    headline: 'Únete a la Comunidad de Whatsapp',
    description:
      'Mantente al día con las novedades, eventos y actividades de ABUVI. Únete a nuestro grupo de WhatsApp.',
    ctaLabel: 'Unirse a WhatsApp',
    ctaPath: 'https://chat.whatsapp.com/EBsp8GfXGPEB6PM8u3HUGu',
    external: true,
  },
  {
    image: imgTents,
    imageAlt: 'Carpas en la naturaleza',
    headline: 'Campamento 2026',
    description:
      '15 días inolvidables en plena naturaleza. Segunda quincena de agosto. ¡Prepárate para la aventura!',
    ctaLabel: 'Ver detalles del campamento',
    ctaPath: '/camp',
  },
  {
    image: imgFriends,
    imageAlt: 'Amigos en el campamento',
    headline: 'Configura tu Familia',
    description:
      'Actualiza los datos de tus familiares para facilitar las inscripciones y mantener tu información al día.',
    ctaLabel: 'Ir a Mi Perfil',
    ctaPath: '/profile',
  },
]

const activeIndex = ref(0)
const activeSlide = computed(() => slides[activeIndex.value] as HeroSlide)
</script>

<template>
  <section
    aria-label="Novedades y accesos destacados"
    class="relative min-h-[60vh] overflow-hidden md:min-h-[70vh]"
  >
    <!-- Background carousel -->
    <div class="absolute inset-0 z-0">
      <Galleria
        v-model:activeIndex="activeIndex"
        :value="slides"
        :num-visible="1"
        :auto-play="true"
        :transition-interval="6000"
        :circular="true"
        :show-thumbnails="false"
        :show-item-navigators="false"
        :show-indicators="false"
        class="h-full w-full"
      >
        <template #item="{ item }">
          <div class="h-[60vh] w-full md:h-[70vh]">
            <img
              :src="item.image"
              :alt="item.imageAlt"
              class="h-full w-full object-cover"
            />
          </div>
        </template>
      </Galleria>
    </div>

    <!-- Gradient overlay -->
    <div
      class="absolute inset-0 z-10 bg-gradient-to-br from-yellow-900/80 via-amber-800/65 to-yellow-700/60"
    />

    <!-- Radial vignette -->
    <div
      class="absolute inset-0 z-10"
      :style="{
        background:
          'radial-gradient(ellipse at center, transparent 30%, rgba(20, 8, 0, 0.55) 100%)',
      }"
    />

    <!-- Content overlay -->
    <div
      class="relative z-20 flex min-h-[60vh] flex-col items-center justify-center px-6 py-16 text-center md:min-h-[70vh]"
    >
      <div
        class="max-w-2xl rounded-3xl bg-amber-950/20 px-8 py-8 shadow-2xl backdrop-blur-md ring-1 ring-amber-300/15 md:px-12"
      >
        <h1
          class="mb-4 text-3xl font-bold text-white drop-shadow-lg md:text-5xl"
        >
          {{ activeSlide.headline }}
        </h1>

        <p
          class="mb-8 text-lg font-medium text-amber-100 drop-shadow-md md:text-xl"
        >
          {{ activeSlide.description }}
        </p>

        <a
          v-if="activeSlide.external"
          :href="activeSlide.ctaPath"
          target="_blank"
          rel="noopener noreferrer"
          class="inline-block rounded-lg bg-amber-400 px-8 py-4 text-lg font-semibold text-amber-900 shadow-lg transition-colors hover:bg-amber-300 focus:outline-none focus:ring-4 focus:ring-amber-300/50"
        >
          {{ activeSlide.ctaLabel }}
        </a>
        <router-link
          v-else
          :to="activeSlide.ctaPath"
          class="inline-block rounded-lg bg-amber-400 px-8 py-4 text-lg font-semibold text-amber-900 shadow-lg transition-colors hover:bg-amber-300 focus:outline-none focus:ring-4 focus:ring-amber-300/50"
        >
          {{ activeSlide.ctaLabel }}
        </router-link>
      </div>

      <!-- Dot indicators -->
      <div class="mt-8 flex gap-3">
        <button
          v-for="(slide, index) in slides"
          :key="index"
          :aria-label="`Ir a slide ${index + 1}: ${slide.headline}`"
          class="h-3 w-3 rounded-full transition-all focus:outline-none focus:ring-2 focus:ring-amber-300/50"
          :class="
            index === activeIndex
              ? 'bg-amber-400 scale-125'
              : 'bg-white/50 hover:bg-white/80'
          "
          @click="activeIndex = index"
        />
      </div>
    </div>
  </section>
</template>

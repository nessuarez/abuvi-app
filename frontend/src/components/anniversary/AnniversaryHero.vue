<script setup lang="ts">
import Galleria from 'primevue/galleria'

import imgGrupo from '@/assets/images/grupo-abuvi.jpg'
import imgFriends from '@/assets/images/camping-friends.jpg'
import imgGames from '@/assets/images/camping-games.jpg'
import imgTents from '@/assets/images/camping-tents-generic.jpg'
import imgPxl1 from '@/assets/images/PXL_20230816_073828281.jpg'
import imgPxl2 from '@/assets/images/PXL_20230817_164205290.jpg'
import imgPxl3 from '@/assets/images/PXL_20230822_174315543.jpg'
import imgPxl4 from '@/assets/images/PXL_20250817_120723887.jpg'
import imgPxl5 from '@/assets/images/PXL_20250825_180910617.jpg'

const slides = [
  { src: imgGrupo, alt: 'Comunidad ABUVI reunida' },
  { src: imgPxl1, alt: 'Campamento ABUVI 2023' },
  { src: imgFriends, alt: 'Amigos en el campamento' },
  { src: imgPxl2, alt: 'Tarde en el campamento 2023' },
  { src: imgGames, alt: 'Juegos en el campamento' },
  { src: imgPxl3, alt: 'Verano ABUVI 2023' },
  { src: imgTents, alt: 'Carpas en la naturaleza' },
  { src: imgPxl4, alt: 'Campamento 2025' },
  { src: imgPxl5, alt: 'Recuerdos del verano 2025' },
]

const scrollToUpload = () => {
  document.getElementById('subir-recuerdo')?.scrollIntoView({ behavior: 'smooth' })
}
</script>

<template>
  <section
    aria-label="Sección de bienvenida al 50 aniversario"
    class="relative flex min-h-[85vh] flex-col items-center justify-center overflow-hidden text-center"
  >
    <!-- Background: auto-sliding photo carousel -->
    <div class="absolute inset-0 z-0">
      <Galleria
        :value="slides"
        :num-visible="1"
        :auto-play="true"
        :transition-interval="5000"
        :circular="true"
        :show-thumbnails="false"
        :show-item-navigators="false"
        :show-indicators="false"
        class="h-full w-full"
      >
        <template #item="{ item }">
          <div class="h-[85vh] w-full">
            <img :src="item.src" :alt="item.alt" class="h-full w-full object-cover" />
          </div>
        </template>
      </Galleria>
    </div>

    <!-- Amber brand gradient overlay (maintains ABUVI color identity) -->
    <div
      class="absolute inset-0 z-10 bg-gradient-to-br from-yellow-900/80 via-amber-800/65 to-yellow-700/60"
    />

    <!-- Radial vignette: darker edges to draw focus to the center -->
    <div
      class="absolute inset-0 z-10"
      :style="{ background: 'radial-gradient(ellipse at center, transparent 30%, rgba(20, 8, 0, 0.55) 100%)' }"
    />

    <!-- Hero content -->
    <div class="relative z-20 flex flex-col items-center px-6 py-24">
      <!-- Frosted glass panel: difuminado en el borde del texto -->
      <div
        class="rounded-3xl bg-amber-950/20 px-10 py-8 shadow-2xl backdrop-blur-md ring-1 ring-amber-300/15"
      >
        <!-- Large "50" numeral -->
        <div
          class="mb-2 text-[9rem] font-black leading-none text-amber-300 drop-shadow-2xl md:text-[13rem]"
        >
          50
        </div>

        <!-- Subtitle -->
        <h1 class="mb-4 text-2xl font-bold tracking-widest text-white drop-shadow-lg md:text-4xl">
          ABUVI · 1976–2026
        </h1>

        <!-- Tagline -->
        <p class="text-xl font-semibold tracking-wide text-amber-200 drop-shadow-md md:text-2xl">
          50 Años de Buena Vida
        </p>
      </div>

      <!-- CTA button -->
      <button
        class="mt-10 rounded-lg bg-amber-400 px-8 py-4 text-lg font-semibold text-amber-900 shadow-lg transition-colors hover:bg-amber-300 focus:outline-none focus:ring-4 focus:ring-amber-300/50"
        @click="scrollToUpload"
      >
        Comparte tu recuerdo
      </button>
    </div>

  </section>
</template>

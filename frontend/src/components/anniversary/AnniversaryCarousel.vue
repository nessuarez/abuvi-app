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
</script>

<template>
  <section aria-label="Galería de fotos de campamentos ABUVI" class="overflow-hidden bg-amber-950">
    <Galleria
      :value="slides"
      :num-visible="1"
      :auto-play="true"
      :transition-interval="5000"
      :circular="true"
      :show-thumbnails="false"
      :show-item-navigators="true"
      :show-indicators="true"
      class="w-full"
    >
      <template #item="{ item }">
        <div class="relative h-64 w-full overflow-hidden md:h-[420px]">
          <img
            :src="item.src"
            :alt="item.alt"
            class="h-full w-full object-cover opacity-85 transition-opacity"
          />
          <!-- Gradient overlays for smooth blending with adjacent sections -->
          <div
            class="pointer-events-none absolute inset-x-0 top-0 h-16 bg-gradient-to-b from-yellow-900/70 to-transparent"
          />
          <div
            class="pointer-events-none absolute inset-x-0 bottom-0 h-16 bg-gradient-to-t from-amber-950/70 to-transparent"
          />
        </div>
      </template>
    </Galleria>
  </section>
</template>

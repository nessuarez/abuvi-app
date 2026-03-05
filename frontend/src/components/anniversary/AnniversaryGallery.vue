<script setup lang="ts">
import Image from 'primevue/image'

import imgGrupo from '@/assets/images/grupo-abuvi.jpg'
import imgFriends from '@/assets/images/camping-friends.jpg'
import imgGames from '@/assets/images/camping-games.jpg'
import imgTents from '@/assets/images/camping-tents-generic.jpg'
import imgPxl1 from '@/assets/images/PXL_20230816_073055985.jpg'
import imgPxl2 from '@/assets/images/PXL_20230819_085551527.jpg'
import imgPxl3 from '@/assets/images/PXL_20230821_133551849.jpg'
import imgPxl4 from '@/assets/images/PXL_20250818_070640135.jpg'
import imgPxl5 from '@/assets/images/PXL_20250826_102207711.jpg'

interface GalleryItem {
  src: string
  year: number
  author: string
}

const galleryItems: GalleryItem[] = [
  { src: imgGrupo, year: 2024, author: 'Comunidad ABUVI' },
  { src: imgFriends, year: 2023, author: 'Campamento de verano' },
  { src: imgPxl1, year: 2023, author: 'Campamento ABUVI' },
  { src: imgGames, year: 2022, author: 'Actividades de grupo' },
  { src: imgPxl2, year: 2023, author: 'Mañana en el campo' },
  { src: imgTents, year: 2021, author: 'Carpas ABUVI' },
  { src: imgPxl3, year: 2023, author: 'Tarde en el campamento' },
  { src: imgPxl4, year: 2025, author: 'Campamento 2025' },
  { src: imgPxl5, year: 2025, author: 'Verano 2025' },
]

const scrollToUpload = () => {
  document.getElementById('subir-recuerdo')?.scrollIntoView({ behavior: 'smooth' })
}
</script>

<template>
  <section aria-label="Galería de recuerdos" class="mx-auto max-w-7xl px-6">
    <div class="mb-12 text-center">
      <h2 class="mb-4 text-3xl font-bold text-amber-900 md:text-4xl">Galería de recuerdos</h2>
      <p class="mx-auto max-w-2xl text-gray-600">
        Un viaje visual a través de cincuenta años de campamentos y aventuras.
      </p>
    </div>

    <div class="grid grid-cols-1 gap-6 sm:grid-cols-3 lg:grid-cols-4">
      <article
        v-for="(item, index) in galleryItems"
        :key="index"
        class="overflow-hidden rounded-xl bg-white shadow-sm transition-shadow hover:shadow-md"
      >
        <div class="overflow-hidden">
          <Image
            :src="item.src"
            :alt="`Recuerdo de ${item.year} — ${item.author}`"
            preview
            image-class="w-full h-48 object-cover transition-transform hover:scale-105 cursor-pointer"
          />
        </div>
        <div class="p-4">
          <span class="text-sm font-bold text-amber-600">{{ item.year }}</span>
          <p class="mt-1 text-sm text-gray-500">{{ item.author }}</p>
        </div>
      </article>
    </div>

    <div class="mt-12 text-center">
      <button
        class="group inline-flex flex-col items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-8 py-5 text-amber-900 transition-colors hover:bg-amber-100 hover:border-amber-400"
        @click="scrollToUpload"
      >
        <span class="font-semibold">¿Tienes más recuerdos para compartir?</span>
        <span class="flex items-center gap-1 text-sm text-amber-700 group-hover:underline">
          <i class="pi pi-upload text-xs" />
          Añade el tuyo aquí
        </span>
      </button>
      <p class="mt-6 text-xs text-gray-400">
        En el futuro esta galería mostrará los recuerdos reales enviados por la comunidad.
      </p>
    </div>
  </section>
</template>

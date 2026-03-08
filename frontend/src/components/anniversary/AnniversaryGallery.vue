<script setup lang="ts">
import { onMounted } from 'vue'
import { useMediaItems } from '@/composables/useMediaItems'
import Image from 'primevue/image'
import Skeleton from 'primevue/skeleton'

const { mediaItems, loading, error, fetchMediaItems } = useMediaItems()

const scrollToUpload = () => {
  document.getElementById('subir-recuerdo')?.scrollIntoView({ behavior: 'smooth' })
}

onMounted(() => {
  fetchMediaItems({ approved: true, context: 'anniversary-50' })
})
</script>

<template>
  <section aria-label="Galería de recuerdos" class="mx-auto max-w-7xl px-6">
    <div class="mb-12 text-center">
      <h2 class="mb-4 text-3xl font-bold text-amber-900 md:text-4xl">Galería de recuerdos</h2>
      <p class="mx-auto max-w-2xl text-gray-600">
        Un viaje visual a través de cincuenta años de campamentos y aventuras.
      </p>
    </div>

    <!-- Loading state -->
    <div v-if="loading" class="grid grid-cols-1 gap-6 sm:grid-cols-3 lg:grid-cols-4">
      <div v-for="i in 8" :key="i" class="overflow-hidden rounded-xl bg-white shadow-sm">
        <Skeleton width="100%" height="12rem" />
        <div class="p-4">
          <Skeleton width="30%" height="1rem" class="mb-2" />
          <Skeleton width="60%" height="0.875rem" />
        </div>
      </div>
    </div>

    <!-- Error state -->
    <div v-else-if="error" class="py-12 text-center">
      <i class="pi pi-exclamation-triangle mb-4 text-4xl text-red-400" />
      <p class="text-lg text-gray-500">No se pudo cargar la galería.</p>
      <p class="mt-2 text-sm text-gray-400">{{ error }}</p>
    </div>

    <!-- Empty state -->
    <div v-else-if="mediaItems.length === 0" class="py-12 text-center">
      <i class="pi pi-images mb-4 text-4xl text-amber-300" />
      <p class="text-lg text-gray-500">Aún no hay recuerdos con aprobación.</p>
      <p class="mt-2 text-sm text-gray-400">¡Sé el primero en compartir!</p>
    </div>

    <!-- Gallery grid -->
    <div v-else class="grid grid-cols-1 gap-6 sm:grid-cols-3 lg:grid-cols-4">
      <article
        v-for="item in mediaItems"
        :key="item.id"
        class="overflow-hidden rounded-xl bg-white shadow-sm transition-shadow hover:shadow-md"
      >
        <!-- Photo type -->
        <template v-if="item.type === 'Photo'">
          <div class="overflow-hidden">
            <Image
              :src="item.fileUrl"
              :alt="item.title"
              preview
              image-class="w-full h-48 object-cover transition-transform hover:scale-105 cursor-pointer"
            />
          </div>
        </template>

        <!-- Video type -->
        <template v-else-if="item.type === 'Video'">
          <div class="relative">
            <video
              :src="item.fileUrl"
              :poster="item.thumbnailUrl ?? undefined"
              controls
              preload="metadata"
              class="h-48 w-full object-cover"
              :aria-label="item.title"
            />
          </div>
        </template>

        <!-- Audio type -->
        <template v-else-if="item.type === 'Audio'">
          <div class="flex h-48 flex-col items-center justify-center bg-amber-50 p-4">
            <i class="pi pi-volume-up mb-3 text-3xl text-amber-600" />
            <audio :src="item.fileUrl" controls class="w-full" :aria-label="item.title" />
          </div>
        </template>

        <!-- Document / other type -->
        <template v-else>
          <div class="flex h-48 flex-col items-center justify-center bg-gray-50 p-4">
            <i class="pi pi-file mb-3 text-3xl text-gray-400" />
            <a
              :href="item.fileUrl"
              target="_blank"
              rel="noopener"
              class="text-sm font-medium text-amber-700 hover:underline"
            >
              Descargar documento
            </a>
          </div>
        </template>

        <!-- Card footer -->
        <div class="p-4">
          <span class="text-sm font-bold text-amber-600">{{ item.year ?? '—' }}</span>
          <p class="mt-1 text-sm font-medium text-gray-800">{{ item.title }}</p>
          <p v-if="item.description" class="mt-1 line-clamp-2 text-xs text-gray-500">
            {{ item.description }}
          </p>
          <p class="mt-1 text-xs text-gray-400">{{ item.uploadedByName }}</p>
        </div>
      </article>
    </div>

    <div class="mt-12 text-center">
      <button
        class="group inline-flex flex-col items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-8 py-5 text-amber-900 transition-colors hover:border-amber-400 hover:bg-amber-100"
        @click="scrollToUpload"
      >
        <span class="font-semibold">¿Tienes más recuerdos para compartir?</span>
        <span class="flex items-center gap-1 text-sm text-amber-700 group-hover:underline">
          <i class="pi pi-upload text-xs" />
          Añade el tuyo aquí
        </span>
      </button>
    </div>
  </section>
</template>

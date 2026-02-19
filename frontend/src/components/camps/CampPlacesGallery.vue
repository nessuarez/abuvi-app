<script setup lang="ts">
import { computed, ref } from 'vue'
import Image from 'primevue/image'
import type { CampPlacesPhoto } from '@/types/camp'

const props = defineProps<{ photos: CampPlacesPhoto[] }>()

const apiBase = (import.meta.env.VITE_API_URL as string) ?? ''

const getPhotoUrl = (photo: CampPlacesPhoto, maxWidth = 800): string => {
  if (photo.photoUrl) return photo.photoUrl
  if (photo.photoReference) {
    return `${apiBase}/places/photo?reference=${encodeURIComponent(photo.photoReference)}&maxwidth=${maxWidth}`
  }
  return ''
}

const primaryPhoto = computed<CampPlacesPhoto | null>(() =>
  props.photos.find((p) => p.isPrimary) ?? props.photos[0] ?? null
)

const thumbnailPhotos = computed<CampPlacesPhoto[]>(() =>
  props.photos.filter((p) => !p.isPrimary).slice(0, 8)
)

const activePhoto = ref<CampPlacesPhoto | null>(null)

const displayedPhoto = computed<CampPlacesPhoto | null>(() =>
  activePhoto.value ?? primaryPhoto.value
)

const selectPhoto = (photo: CampPlacesPhoto): void => {
  activePhoto.value = photo
}
</script>

<template>
  <div v-if="photos.length > 0" class="rounded-lg border border-gray-200 bg-white p-6">
    <h2 class="mb-4 text-lg font-semibold text-gray-900">Fotos</h2>

    <!-- Primary / Active Photo -->
    <div v-if="displayedPhoto" class="mb-3">
      <Image
        :src="getPhotoUrl(displayedPhoto, 800)"
        :alt="`Foto del campamento`"
        :preview="true"
        image-class="w-full rounded-lg object-cover max-h-72"
        class="block w-full"
      />
      <!-- Attribution -->
      <p class="mt-1 text-right text-xs text-gray-400">
        Foto de
        <a
          v-if="displayedPhoto.attributionUrl"
          :href="displayedPhoto.attributionUrl"
          target="_blank"
          rel="noopener noreferrer"
          class="hover:underline"
        >
          {{ displayedPhoto.attributionName }}
        </a>
        <span v-else>{{ displayedPhoto.attributionName }}</span>
        · Google Maps
      </p>
    </div>

    <!-- Thumbnail Grid -->
    <div
      v-if="thumbnailPhotos.length > 0"
      class="grid grid-cols-4 gap-2 sm:grid-cols-6 md:grid-cols-8"
    >
      <button
        v-for="photo in thumbnailPhotos"
        :key="photo.id"
        type="button"
        class="overflow-hidden rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
        :aria-label="`Ver foto de ${photo.attributionName}`"
        @click="selectPhoto(photo)"
      >
        <img
          :src="getPhotoUrl(photo, 200)"
          :alt="`Miniatura`"
          class="h-16 w-full object-cover transition-opacity hover:opacity-80"
          loading="lazy"
        />
      </button>
    </div>

    <!-- Footer attribution -->
    <p class="mt-3 text-xs text-gray-400">
      Imágenes proporcionadas por Google Maps.
      <span v-if="photos.length > 1">{{ photos.length }} fotos disponibles.</span>
    </p>
  </div>
</template>

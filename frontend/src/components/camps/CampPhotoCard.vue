<script setup lang="ts">
import Button from 'primevue/button'
import type { CampPhoto } from '@/types/camp-photo'

interface Props {
  photo: CampPhoto
}

const props = defineProps<Props>()
const emit = defineEmits<{
  edit: [photo: CampPhoto]
  delete: [photo: CampPhoto]
  setPrimary: [photo: CampPhoto]
}>()

const handleImageError = (event: Event) => {
  const img = event.target as HTMLImageElement
  img.src = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 24 24"%3E%3Cpath fill="%23e5e7eb" d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z"/%3E%3C/svg%3E'
}
</script>

<template>
  <div
    class="relative overflow-hidden rounded-lg border border-gray-200 bg-white transition-shadow hover:shadow-md"
    data-testid="camp-photo-card"
  >
    <!-- Primary badge -->
    <span
      v-if="photo.isPrimary"
      class="absolute left-2 top-2 z-10 rounded-full bg-primary-500 px-2 py-0.5 text-xs font-medium text-white shadow"
    >
      Principal
    </span>

    <!-- Image -->
    <div class="h-40 w-full overflow-hidden bg-gray-100">
      <img
        :src="photo.url"
        :alt="photo.description || 'Foto del campamento'"
        class="h-full w-full object-cover"
        @error="handleImageError"
      />
    </div>

    <!-- Footer -->
    <div class="p-2">
      <p v-if="photo.description" class="mb-1 truncate text-xs text-gray-600">
        {{ photo.description }}
      </p>
      <p class="text-xs text-gray-400">Orden: {{ photo.displayOrder }}</p>

      <div class="mt-2 flex justify-end gap-1">
        <Button
          v-if="!photo.isPrimary"
          icon="pi pi-star"
          text
          size="small"
          v-tooltip.top="'Establecer como principal'"
          aria-label="Establecer como foto principal"
          @click="emit('setPrimary', props.photo)"
        />
        <Button
          icon="pi pi-pencil"
          text
          size="small"
          v-tooltip.top="'Editar'"
          aria-label="Editar foto"
          @click="emit('edit', props.photo)"
        />
        <Button
          icon="pi pi-trash"
          text
          severity="danger"
          size="small"
          v-tooltip.top="'Eliminar'"
          aria-label="Eliminar foto"
          @click="emit('delete', props.photo)"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'

const props = defineProps<{
  photoUrl: string | null
  initials: string
  size?: 'sm' | 'md' | 'lg'
  editable?: boolean
  loading?: boolean
}>()

const emit = defineEmits<{
  upload: [file: File]
  remove: []
}>()

const fileInput = ref<HTMLInputElement | null>(null)
const imgError = ref(false)

const sizeClasses = computed(() => {
  switch (props.size) {
    case 'sm': return 'h-10 w-10 text-sm'
    case 'lg': return 'h-20 w-20 text-2xl'
    default: return 'h-14 w-14 text-xl'
  }
})

const showPhoto = computed(() => props.photoUrl && !imgError.value)

function triggerUpload() {
  fileInput.value?.click()
}

function onFileSelected(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (file) {
    emit('upload', file)
  }
  if (fileInput.value) {
    fileInput.value.value = ''
  }
}
</script>

<template>
  <div class="group relative inline-flex">
    <!-- Avatar circle -->
    <div
      :class="[
        'flex shrink-0 items-center justify-center rounded-full font-bold',
        sizeClasses,
        showPhoto ? '' : 'bg-primary-100 text-primary-700'
      ]"
    >
      <img
        v-if="showPhoto"
        :src="photoUrl!"
        alt="Foto de perfil"
        class="h-full w-full rounded-full object-cover"
        @error="imgError = true"
      />
      <span v-else>{{ initials }}</span>
    </div>

    <!-- Edit overlay (shown on hover when editable) -->
    <div
      v-if="editable && !loading"
      class="absolute inset-0 flex items-center justify-center rounded-full bg-black/50 opacity-0 transition-opacity group-hover:opacity-100"
    >
      <div class="flex gap-1">
        <Button
          icon="pi pi-camera"
          severity="secondary"
          text
          rounded
          size="small"
          class="!text-white"
          aria-label="Subir foto"
          @click="triggerUpload"
        />
        <Button
          v-if="photoUrl"
          icon="pi pi-trash"
          severity="danger"
          text
          rounded
          size="small"
          class="!text-white"
          aria-label="Eliminar foto"
          @click="$emit('remove')"
        />
      </div>
    </div>

    <!-- Loading spinner overlay -->
    <div
      v-if="loading"
      class="absolute inset-0 flex items-center justify-center rounded-full bg-black/40"
    >
      <i class="pi pi-spin pi-spinner text-white" />
    </div>

    <!-- Hidden file input -->
    <input
      v-if="editable"
      ref="fileInput"
      type="file"
      accept=".jpg,.jpeg,.png,.webp"
      class="hidden"
      @change="onFileSelected"
    />
  </div>
</template>

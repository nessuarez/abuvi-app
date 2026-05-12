<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import AccommodationMediaGallery from './AccommodationMediaGallery.vue'
import { useAccommodationMedia } from '@/composables/useAccommodationMedia'
import { useBlobStorage } from '@/composables/useBlobStorage'
import type { AccommodationMediaOwnerType } from '@/types/accommodation-media'

const props = defineProps<{
  ownerType: AccommodationMediaOwnerType
  ownerId: string
  editionId?: string
  readonly?: boolean
}>()

const MAX_ITEMS = 10
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif', 'video/mp4', 'video/quicktime', 'video/x-msvideo', 'video/webm']
const ALLOWED_EXT = '.jpg,.jpeg,.png,.webp,.gif,.mp4,.mov,.avi,.webm'
const MAX_SIZE_BYTES = 50 * 1024 * 1024

const toast = useToast()
const confirm = useConfirm()
const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)

const {
  items,
  loading,
  error,
  fetchZoneMedia,
  addZoneMedia,
  deleteZoneMedia,
  setZonePrimary,
  fetchAccommodationMedia,
  addAccommodationMedia,
  deleteAccommodationMedia,
  setAccommodationPrimary,
  fetchTypeMedia,
  addTypeMedia,
  deleteTypeMedia,
  setTypePrimary,
} = useAccommodationMedia()

const { uploadFile } = useBlobStorage()

async function load() {
  if (props.ownerType === 'zone' && props.editionId) {
    await fetchZoneMedia(props.editionId, props.ownerId)
  } else if (props.ownerType === 'accommodation' && props.editionId) {
    await fetchAccommodationMedia(props.editionId, props.ownerId)
  } else if (props.ownerType === 'type') {
    await fetchTypeMedia(props.ownerId)
  }
}

onMounted(load)

watch(
  () => props.ownerId,
  () => load()
)

watch(error, (msg) => {
  if (msg) {
    toast.add({ severity: 'error', summary: 'Error', detail: msg, life: 5000 })
  }
})

const canAdd = computed(() => items.value.length < MAX_ITEMS)

function openFilePicker() {
  fileInput.value?.click()
}

async function handleFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  input.value = ''

  if (!ALLOWED_TYPES.includes(file.type)) {
    toast.add({
      severity: 'error',
      summary: 'Tipo no permitido',
      detail: 'Solo se permiten imágenes (JPG, PNG, WEBP, GIF) y vídeos (MP4, MOV, AVI, WEBM).',
      life: 5000,
    })
    return
  }

  if (file.size > MAX_SIZE_BYTES) {
    toast.add({
      severity: 'error',
      summary: 'Archivo demasiado grande',
      detail: 'El archivo es demasiado grande. El tamaño máximo es 50 MB.',
      life: 5000,
    })
    return
  }

  uploading.value = true
  try {
    const result = await uploadFile({
      file,
      folder: 'accommodation-media',
      contextId: props.ownerId,
      generateThumbnail: true,
    })

    if (!result) {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Error al subir el archivo. Por favor, inténtalo de nuevo.',
        life: 5000,
      })
      return
    }

    const request = {
      fileUrl: result.fileUrl,
      thumbnailUrl: result.thumbnailUrl,
      description: null,
      displayOrder: items.value.length,
    }

    let added = null
    if (props.ownerType === 'zone' && props.editionId) {
      added = await addZoneMedia(props.editionId, props.ownerId, request)
    } else if (props.ownerType === 'accommodation' && props.editionId) {
      added = await addAccommodationMedia(props.editionId, props.ownerId, request)
    } else if (props.ownerType === 'type') {
      added = await addTypeMedia(props.ownerId, request)
    }

    if (added) {
      toast.add({ severity: 'success', summary: 'Archivo añadido', life: 3000 })
    }
  } finally {
    uploading.value = false
  }
}

async function handleSetPrimary(mediaId: string) {
  if (props.ownerType === 'zone' && props.editionId) {
    await setZonePrimary(props.editionId, props.ownerId, mediaId)
  } else if (props.ownerType === 'accommodation' && props.editionId) {
    await setAccommodationPrimary(props.editionId, props.ownerId, mediaId)
  } else if (props.ownerType === 'type') {
    await setTypePrimary(mediaId)
  }
}

function confirmDelete(mediaId: string) {
  confirm.require({
    message: '¿Eliminar este archivo?',
    header: 'Confirmar eliminación',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => handleDelete(mediaId),
  })
}

async function handleDelete(mediaId: string) {
  let ok = false
  if (props.ownerType === 'zone' && props.editionId) {
    ok = await deleteZoneMedia(props.editionId, props.ownerId, mediaId)
  } else if (props.ownerType === 'accommodation' && props.editionId) {
    ok = await deleteAccommodationMedia(props.editionId, props.ownerId, mediaId)
  } else if (props.ownerType === 'type') {
    ok = await deleteTypeMedia(mediaId)
  }
  if (ok) {
    toast.add({ severity: 'success', summary: 'Archivo eliminado', life: 3000 })
  }
}

const sortedItems = computed(() =>
  [...items.value].sort((a, b) => {
    if (a.isPrimary !== b.isPrimary) return a.isPrimary ? -1 : 1
    return a.displayOrder - b.displayOrder
  })
)
</script>

<template>
  <div>
    <!-- Header row -->
    <div class="flex items-center justify-between">
      <span class="text-sm font-medium text-gray-700">
        Fotos y multimedia
        <span class="ml-1 text-xs text-gray-400">({{ items.length }}/{{ MAX_ITEMS }})</span>
      </span>
      <Button
        v-if="!readonly"
        icon="pi pi-plus"
        label="Añadir"
        size="small"
        severity="secondary"
        :disabled="!canAdd || uploading"
        :loading="uploading"
        aria-label="Subir archivo"
        @click="openFilePicker"
      />
    </div>

    <!-- Hidden file input -->
    <input
      v-if="!readonly"
      ref="fileInput"
      type="file"
      :accept="ALLOWED_EXT"
      class="hidden"
      @change="handleFileSelected"
    />

    <!-- Loading spinner -->
    <div v-if="loading && items.length === 0" class="flex items-center gap-2 py-2">
      <ProgressSpinner style="width: 24px; height: 24px" />
    </div>

    <!-- Empty state (admin mode only) -->
    <p
      v-else-if="!readonly && items.length === 0"
      class="mt-2 text-xs italic text-gray-400"
    >
      Sin archivos. Haz clic en '+ Añadir' para subir fotos.
    </p>

    <!-- Media strip with per-item controls -->
    <div v-else-if="items.length > 0" class="mt-2 flex gap-2 overflow-x-auto py-1">
      <div
        v-for="item in sortedItems"
        :key="item.id"
        class="group relative flex-shrink-0"
      >
        <!-- Thumbnail -->
        <img
          :src="item.thumbnailUrl ?? item.fileUrl"
          alt=""
          class="h-20 w-20 rounded-md object-cover"
          :class="item.isPrimary ? 'ring-2 ring-primary-500' : 'ring-1 ring-gray-200'"
          aria-label="Ver imagen"
        />

        <!-- Hover controls (admin only) -->
        <div
          v-if="!readonly"
          class="absolute inset-0 flex flex-col items-end justify-start gap-0.5 rounded-md bg-black/30 p-0.5 opacity-0 transition-opacity group-hover:opacity-100"
        >
          <!-- Set primary -->
          <button
            class="rounded bg-white/80 p-0.5 text-yellow-500 hover:bg-white"
            :title="item.isPrimary ? 'Ya es principal' : 'Establecer como principal'"
            :disabled="item.isPrimary"
            @click.stop="handleSetPrimary(item.id)"
          >
            <i :class="item.isPrimary ? 'pi pi-star-fill text-xs' : 'pi pi-star text-xs'" />
          </button>
          <!-- Delete -->
          <button
            class="rounded bg-white/80 p-0.5 text-red-500 hover:bg-white"
            title="Eliminar"
            @click.stop="confirmDelete(item.id)"
          >
            <i class="pi pi-times text-xs" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

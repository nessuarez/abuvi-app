<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useMediaItems } from '@/composables/useMediaItems'
import { useMemories } from '@/composables/useMemories'
import { useToast } from 'primevue/usetoast'
import type { MediaItem } from '@/types/media-item'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import Image from 'primevue/image'

const toast = useToast()

const {
  mediaItems,
  loading,
  fetchMediaItems,
  approveMediaItem,
  rejectMediaItem
} = useMediaItems()

const {
  memories,
  loading: memoriesLoading,
  fetchMemories,
  approveMemory,
  rejectMemory
} = useMemories()

const previewItem = ref<MediaItem | null>(null)
const showPreview = ref(false)

const isEmpty = computed(
  () => !loading.value && !memoriesLoading.value && mediaItems.value.length === 0 && memories.value.length === 0
)

const typeSeverity = (type: string): 'success' | 'info' | 'warn' | 'secondary' => {
  const map: Record<string, 'success' | 'info' | 'warn' | 'secondary'> = {
    Photo: 'success',
    Video: 'info',
    Audio: 'warn',
    Document: 'secondary',
    Interview: 'secondary'
  }
  return map[type] ?? 'secondary'
}

const formatDate = (dateStr: string): string => {
  return new Date(dateStr).toLocaleDateString('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  })
}

const openPreview = (item: MediaItem) => {
  previewItem.value = item
  showPreview.value = true
}

const handleApproveMedia = async (id: string) => {
  const success = await approveMediaItem(id)
  if (success) {
    toast.add({ severity: 'success', summary: 'Aprobado', detail: 'Elemento aprobado y publicado', life: 3000 })
    mediaItems.value = mediaItems.value.filter((i) => i.id !== id)
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: 'Error al aprobar el elemento', life: 5000 })
  }
}

const handleRejectMedia = async (id: string) => {
  const success = await rejectMediaItem(id)
  if (success) {
    toast.add({ severity: 'info', summary: 'Rechazado', detail: 'Elemento rechazado', life: 3000 })
    mediaItems.value = mediaItems.value.filter((i) => i.id !== id)
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: 'Error al rechazar el elemento', life: 5000 })
  }
}

const handleApproveMemory = async (id: string) => {
  const success = await approveMemory(id)
  if (success) {
    toast.add({ severity: 'success', summary: 'Aprobado', detail: 'Recuerdo aprobado y publicado', life: 3000 })
    memories.value = memories.value.filter((m) => m.id !== id)
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: 'Error al aprobar el recuerdo', life: 5000 })
  }
}

const handleRejectMemory = async (id: string) => {
  const success = await rejectMemory(id)
  if (success) {
    toast.add({ severity: 'info', summary: 'Rechazado', detail: 'Recuerdo rechazado', life: 3000 })
    memories.value = memories.value.filter((m) => m.id !== id)
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: 'Error al rechazar el recuerdo', life: 5000 })
  }
}

onMounted(() => {
  fetchMediaItems({ approved: false })
  fetchMemories({ approved: false })
})
</script>

<template>
  <div class="space-y-8">
    <!-- Empty state -->
    <div v-if="isEmpty" class="py-12 text-center">
      <i class="pi pi-check-circle mb-4 text-4xl text-green-400" />
      <p class="text-lg text-gray-500">No hay elementos pendientes de revisión</p>
    </div>

    <!-- Media Items Review -->
    <div v-if="mediaItems.length > 0 || loading">
      <h3 class="mb-4 text-xl font-semibold text-gray-800">Elementos multimedia pendientes</h3>
      <DataTable
        :value="mediaItems"
        :loading="loading"
        striped-rows
        :paginator="mediaItems.length > 5"
        :rows="5"
        scrollable
        data-testid="media-items-table"
      >
        <Column header="Vista previa" style="width: 6rem">
          <template #body="{ data }">
            <button
              v-if="data.thumbnailUrl || data.type === 'Photo'"
              class="cursor-pointer overflow-hidden rounded border-0 bg-transparent p-0"
              @click="openPreview(data)"
            >
              <img
                :src="data.thumbnailUrl ?? data.fileUrl"
                :alt="data.title"
                class="h-12 w-12 rounded object-cover"
              />
            </button>
            <i v-else-if="data.type === 'Audio'" class="pi pi-volume-up text-xl text-amber-600" />
            <i v-else class="pi pi-file text-xl text-gray-400" />
          </template>
        </Column>
        <Column field="title" header="Título" />
        <Column field="type" header="Tipo" style="width: 7rem">
          <template #body="{ data }">
            <Tag :value="data.type" :severity="typeSeverity(data.type)" />
          </template>
        </Column>
        <Column field="uploadedByName" header="Subido por" />
        <Column field="year" header="Año" style="width: 5rem" />
        <Column field="context" header="Contexto" style="width: 8rem" />
        <Column field="createdAt" header="Fecha" style="width: 7rem">
          <template #body="{ data }">
            {{ formatDate(data.createdAt) }}
          </template>
        </Column>
        <Column header="Acciones" style="width: 10rem">
          <template #body="{ data }">
            <div class="flex gap-2">
              <Button
                icon="pi pi-check"
                severity="success"
                size="small"
                rounded
                aria-label="Aprobar"
                @click="handleApproveMedia(data.id)"
              />
              <Button
                icon="pi pi-times"
                severity="danger"
                size="small"
                rounded
                aria-label="Rechazar"
                @click="handleRejectMedia(data.id)"
              />
            </div>
          </template>
        </Column>
      </DataTable>
    </div>

    <!-- Written Memories Review -->
    <div v-if="memories.length > 0 || memoriesLoading">
      <h3 class="mb-4 text-xl font-semibold text-gray-800">Historias escritas pendientes</h3>
      <DataTable
        :value="memories"
        :loading="memoriesLoading"
        striped-rows
        :paginator="memories.length > 5"
        :rows="5"
        scrollable
        data-testid="memories-table"
      >
        <Column field="title" header="Título" />
        <Column field="content" header="Contenido" style="max-width: 20rem">
          <template #body="{ data }">
            <p class="line-clamp-2 text-sm">{{ data.content }}</p>
          </template>
        </Column>
        <Column field="authorName" header="Autor" />
        <Column field="year" header="Año" style="width: 5rem" />
        <Column field="createdAt" header="Fecha" style="width: 7rem">
          <template #body="{ data }">
            {{ formatDate(data.createdAt) }}
          </template>
        </Column>
        <Column header="Acciones" style="width: 10rem">
          <template #body="{ data }">
            <div class="flex gap-2">
              <Button
                icon="pi pi-check"
                severity="success"
                size="small"
                rounded
                aria-label="Aprobar"
                @click="handleApproveMemory(data.id)"
              />
              <Button
                icon="pi pi-times"
                severity="danger"
                size="small"
                rounded
                aria-label="Rechazar"
                @click="handleRejectMemory(data.id)"
              />
            </div>
          </template>
        </Column>
      </DataTable>
    </div>

    <!-- Preview Dialog -->
    <Dialog
      v-model:visible="showPreview"
      :header="previewItem?.title ?? 'Vista previa'"
      modal
      :style="{ width: '50rem' }"
      :breakpoints="{ '768px': '95vw' }"
    >
      <template v-if="previewItem">
        <div v-if="previewItem.type === 'Photo'" class="text-center">
          <Image
            :src="previewItem.fileUrl"
            :alt="previewItem.title"
            preview
            image-class="max-h-[60vh] w-auto mx-auto"
          />
        </div>
        <div v-else-if="previewItem.type === 'Video'" class="text-center">
          <video
            :src="previewItem.fileUrl"
            controls
            class="mx-auto max-h-[60vh] w-auto"
            :aria-label="previewItem.title"
          />
        </div>
        <div v-else-if="previewItem.type === 'Audio'" class="py-8 text-center">
          <i class="pi pi-volume-up mb-4 text-4xl text-amber-600" />
          <audio :src="previewItem.fileUrl" controls class="mx-auto w-full max-w-md" :aria-label="previewItem.title" />
        </div>
        <div v-else class="py-8 text-center">
          <a :href="previewItem.fileUrl" target="_blank" rel="noopener" class="text-amber-700 hover:underline">
            Descargar archivo
          </a>
        </div>
        <p v-if="previewItem.description" class="mt-4 text-sm text-gray-600">
          {{ previewItem.description }}
        </p>
      </template>
    </Dialog>
  </div>
</template>

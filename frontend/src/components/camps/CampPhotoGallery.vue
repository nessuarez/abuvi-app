<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import OrderList from 'primevue/orderlist'
import ConfirmDialog from 'primevue/confirmdialog'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import CampPhotoCard from '@/components/camps/CampPhotoCard.vue'
import CampPhotoForm from '@/components/camps/CampPhotoForm.vue'
import { useCampPhotos } from '@/composables/useCampPhotos'
import type { CampPhoto } from '@/types/camp-photo'

interface Props {
  campId: string
  initialPhotos: CampPhoto[]
}

const props = defineProps<Props>()
const emit = defineEmits<{
  photosChanged: [photos: CampPhoto[]]
}>()

const confirm = useConfirm()
const toast = useToast()
const { loading, error, deletePhoto, setPrimaryPhoto, reorderPhotos } = useCampPhotos()

const photos = ref<CampPhoto[]>(
  [...props.initialPhotos].sort((a, b) => a.displayOrder - b.displayOrder)
)
const showForm = ref(false)
const editingPhoto = ref<CampPhoto | undefined>(undefined)
const reorderMode = ref(false)

const openAddForm = () => {
  editingPhoto.value = undefined
  showForm.value = true
}

const openEditForm = (photo: CampPhoto) => {
  editingPhoto.value = photo
  showForm.value = true
}

const handlePhotoSaved = (savedPhoto: CampPhoto) => {
  if (editingPhoto.value) {
    photos.value = photos.value.map((p) => (p.id === savedPhoto.id ? savedPhoto : p))
  } else {
    photos.value.push(savedPhoto)
    photos.value.sort((a, b) => a.displayOrder - b.displayOrder)
  }
  // If the saved photo is primary, clear isPrimary on all others
  if (savedPhoto.isPrimary) {
    photos.value = photos.value.map((p) => ({
      ...p,
      isPrimary: p.id === savedPhoto.id
    }))
  }
  emit('photosChanged', [...photos.value])
}

const confirmDelete = (photo: CampPhoto) => {
  confirm.require({
    message: '¿Estás seguro de que quieres eliminar esta foto? Esta acción no se puede deshacer.',
    header: 'Eliminar foto',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => handleDelete(photo)
  })
}

const handleDelete = async (photo: CampPhoto) => {
  const success = await deletePhoto(props.campId, photo.id)
  if (success) {
    photos.value = photos.value.filter((p) => p.id !== photo.id)
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Foto eliminada correctamente',
      life: 3000
    })
    emit('photosChanged', [...photos.value])
  } else if (error.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleSetPrimary = async (photo: CampPhoto) => {
  // Optimistic update
  photos.value = photos.value.map((p) => ({ ...p, isPrimary: p.id === photo.id }))

  const result = await setPrimaryPhoto(props.campId, photo.id)
  if (result) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Foto principal actualizada',
      life: 3000
    })
    emit('photosChanged', [...photos.value])
  } else {
    // Revert optimistic update on failure
    photos.value = photos.value.map((p) => ({ ...p, isPrimary: p.id === photo.id ? false : p.isPrimary }))
    if (error.value) {
      toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
    }
  }
}

const toggleReorderMode = async () => {
  if (reorderMode.value) {
    // Save the new order
    const request = {
      photos: photos.value.map((p, index) => ({ id: p.id, displayOrder: index }))
    }
    const success = await reorderPhotos(props.campId, request)
    if (success) {
      // Update local displayOrder values
      photos.value = photos.value.map((p, index) => ({ ...p, displayOrder: index }))
      toast.add({
        severity: 'success',
        summary: 'Éxito',
        detail: 'Orden de fotos guardado',
        life: 3000
      })
      emit('photosChanged', [...photos.value])
    } else if (error.value) {
      toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
    }
  }
  reorderMode.value = !reorderMode.value
}
</script>

<template>
  <div>
    <!-- Header -->
    <div class="mb-4 flex items-center justify-between">
      <h3 class="text-lg font-semibold text-gray-900">
        Fotos del campamento ({{ photos.length }})
      </h3>
      <Button
        label="Añadir foto"
        icon="pi pi-plus"
        size="small"
        data-testid="add-photo-button"
        @click="openAddForm"
      />
    </div>

    <!-- Loading state (for delete/set-primary/reorder actions) -->
    <div v-if="loading" class="mb-4 flex justify-center">
      <ProgressSpinner style="width: 30px; height: 30px" />
    </div>

    <!-- Error state -->
    <Message v-if="error && !loading" severity="error" class="mb-4">
      {{ error }}
    </Message>

    <!-- Empty state -->
    <div
      v-if="photos.length === 0 && !loading"
      class="flex flex-col items-center py-12 text-gray-400"
      data-testid="empty-photo-state"
    >
      <i class="pi pi-images mb-3 text-5xl" />
      <p class="text-sm">No hay fotos todavía. Añade la primera foto del campamento.</p>
    </div>

    <!-- Photo content -->
    <div v-if="photos.length > 0">
      <!-- Reorder toggle button -->
      <div class="mb-3">
        <Button
          :label="reorderMode ? 'Guardar orden' : 'Reordenar'"
          :icon="reorderMode ? 'pi pi-check' : 'pi pi-arrows-v'"
          text
          size="small"
          :loading="loading && reorderMode"
          @click="toggleReorderMode"
        />
        <span v-if="reorderMode" class="ml-2 text-xs text-gray-500">
          Arrastra las fotos para cambiar el orden
        </span>
      </div>

      <!-- Reorder mode: OrderList -->
      <OrderList
        v-if="reorderMode"
        v-model="photos"
        data-key="id"
        :option-label="() => ''"
        class="mb-4 w-full"
      >
        <template #item="{ item }">
          <div class="flex items-center gap-3 p-2">
            <img
              :src="item.url"
              :alt="item.description || 'Foto'"
              class="h-12 w-16 rounded object-cover"
            />
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-gray-700">
                {{ item.description || 'Sin descripción' }}
              </p>
              <p v-if="item.isPrimary" class="text-xs text-primary-600">Principal</p>
            </div>
          </div>
        </template>
      </OrderList>

      <!-- Grid mode -->
      <div
        v-else
        class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
        data-testid="photo-grid"
      >
        <CampPhotoCard
          v-for="photo in photos"
          :key="photo.id"
          :photo="photo"
          @edit="openEditForm"
          @delete="confirmDelete"
          @set-primary="handleSetPrimary"
        />
      </div>
    </div>

    <!-- Add/Edit Dialog -->
    <CampPhotoForm
      v-model:visible="showForm"
      :camp-id="campId"
      :photo="editingPhoto"
      @saved="handlePhotoSaved"
    />

    <!-- Delete Confirmation Dialog -->
    <ConfirmDialog />
  </div>
</template>

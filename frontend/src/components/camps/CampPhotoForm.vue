<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import InputNumber from 'primevue/inputnumber'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { useCampPhotos } from '@/composables/useCampPhotos'
import type { CampPhoto, AddCampPhotoRequest, UpdateCampPhotoRequest } from '@/types/camp-photo'

interface Props {
  visible: boolean
  campId: string
  photo?: CampPhoto
}

const props = defineProps<Props>()
const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: [photo: CampPhoto]
}>()

const toast = useToast()
const { loading, error, addPhoto, updatePhoto } = useCampPhotos()

const formData = reactive<AddCampPhotoRequest>({
  url: '',
  description: null,
  displayOrder: 0,
  isPrimary: false
})

const errors = ref<Record<string, string>>({})

// Sync form data when dialog opens or photo changes
watch(
  () => props.visible,
  (isVisible) => {
    if (isVisible) {
      if (props.photo) {
        formData.url = props.photo.url
        formData.description = props.photo.description ?? null
        formData.displayOrder = props.photo.displayOrder
        formData.isPrimary = props.photo.isPrimary
      } else {
        formData.url = ''
        formData.description = null
        formData.displayOrder = 0
        formData.isPrimary = false
      }
      errors.value = {}
    }
  }
)

const validate = (): boolean => {
  errors.value = {}

  if (!formData.url.trim()) {
    errors.value.url = 'La URL de la foto es obligatoria'
  } else if (formData.url.length > 2000) {
    errors.value.url = 'La URL no puede superar los 2000 caracteres'
  }

  if (formData.description && formData.description.length > 500) {
    errors.value.description = 'La descripción no puede superar los 500 caracteres'
  }

  if (formData.displayOrder < 0) {
    errors.value.displayOrder = 'El orden debe ser mayor o igual a 0'
  }

  return Object.keys(errors.value).length === 0
}

const handleSubmit = async () => {
  if (!validate()) return

  let result: CampPhoto | null = null

  if (props.photo) {
    const request: UpdateCampPhotoRequest = {
      url: formData.url,
      description: formData.description || null,
      displayOrder: formData.displayOrder,
      isPrimary: formData.isPrimary
    }
    result = await updatePhoto(props.campId, props.photo.id, request)
  } else {
    const request: AddCampPhotoRequest = {
      url: formData.url,
      description: formData.description || null,
      displayOrder: formData.displayOrder,
      isPrimary: formData.isPrimary
    }
    result = await addPhoto(props.campId, request)
  }

  if (result) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: props.photo ? 'Foto actualizada correctamente' : 'Foto añadida correctamente',
      life: 3000
    })
    emit('saved', result)
    close()
  } else if (error.value) {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value,
      life: 5000
    })
  }
}

const close = () => {
  emit('update:visible', false)
}
</script>

<template>
  <Dialog
    :visible="visible"
    :header="photo ? 'Editar foto' : 'Añadir foto'"
    modal
    class="w-full max-w-lg"
    @update:visible="emit('update:visible', $event)"
  >
    <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
      <!-- URL -->
      <div>
        <label for="photo-url" class="mb-1 block text-sm font-medium text-gray-700">
          URL de la foto *
        </label>
        <InputText
          id="photo-url"
          v-model="formData.url"
          class="w-full"
          :invalid="!!errors.url"
          placeholder="https://ejemplo.com/foto.jpg"
        />
        <small v-if="errors.url" class="text-red-500">{{ errors.url }}</small>
      </div>

      <!-- Description -->
      <div>
        <label for="photo-description" class="mb-1 block text-sm font-medium text-gray-700">
          Descripción (opcional)
        </label>
        <Textarea
          id="photo-description"
          v-model="formData.description"
          class="w-full"
          rows="2"
          placeholder="Descripción breve de la foto..."
          :maxlength="500"
        />
        <small v-if="errors.description" class="text-red-500">{{ errors.description }}</small>
      </div>

      <!-- Display Order -->
      <div>
        <label for="photo-order" class="mb-1 block text-sm font-medium text-gray-700">
          Orden de visualización
        </label>
        <InputNumber
          id="photo-order"
          v-model="formData.displayOrder"
          :min="0"
          class="w-full"
          :invalid="!!errors.displayOrder"
        />
        <small v-if="errors.displayOrder" class="text-red-500">{{ errors.displayOrder }}</small>
      </div>

      <!-- Is Primary -->
      <div class="flex items-center gap-2">
        <Checkbox v-model="formData.isPrimary" binary input-id="photo-is-primary" />
        <label for="photo-is-primary" class="cursor-pointer text-sm">
          Establecer como foto principal
        </label>
      </div>

      <!-- Actions -->
      <div class="flex justify-end gap-2 border-t border-gray-100 pt-4">
        <Button label="Cancelar" severity="secondary" @click="close" />
        <Button
          type="submit"
          :label="photo ? 'Guardar cambios' : 'Añadir foto'"
          :loading="loading"
          :disabled="loading"
        />
      </div>
    </form>
  </Dialog>
</template>

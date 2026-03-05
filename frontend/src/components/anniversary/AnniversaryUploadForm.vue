<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { useToast } from 'primevue/usetoast'
import { useBlobStorage } from '@/composables/useBlobStorage'
import { useMediaItems } from '@/composables/useMediaItems'
import { useMemories } from '@/composables/useMemories'
import type { MediaItemType } from '@/types/media-item'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import FileUpload from 'primevue/fileupload'
import Button from 'primevue/button'
import ProgressBar from 'primevue/progressbar'

const toast = useToast()
const { uploadFile, uploading, uploadError } = useBlobStorage()
const { createMediaItem, creating: creatingMedia, createError: mediaError } = useMediaItems()
const { createMemory, creating: creatingMemory, createError: memoryError } = useMemories()

const contentTypes = [
  { label: 'Foto', value: 'foto' },
  { label: 'Vídeo', value: 'video' },
  { label: 'Audio', value: 'audio' },
  { label: 'Historia escrita', value: 'historia' },
]

const contentTypeToMediaType: Record<string, MediaItemType> = {
  foto: 'Photo',
  video: 'Video',
  audio: 'Audio',
}

const form = reactive({
  name: '',
  contentType: null as string | null,
  year: null as number | null,
  description: '',
})

const selectedFile = ref<File | null>(null)
const errors = ref<Record<string, string>>({})

const isSubmitting = computed(() => uploading.value || creatingMedia.value || creatingMemory.value)

const validate = (): boolean => {
  errors.value = {}
  if (!form.name.trim()) errors.value.name = 'El nombre es obligatorio'
  if (!form.contentType) errors.value.contentType = 'El tipo de contenido es obligatorio'
  if (form.contentType && form.contentType !== 'historia' && !selectedFile.value) {
    errors.value.file = 'Debes seleccionar un archivo'
  }
  if (form.contentType === 'historia' && !form.description.trim()) {
    errors.value.description = 'La descripción es obligatoria para historias escritas'
  }
  return Object.keys(errors.value).length === 0
}

const resetForm = () => {
  form.name = ''
  form.contentType = null
  form.year = null
  form.description = ''
  selectedFile.value = null
  errors.value = {}
}

const handleSubmit = async () => {
  if (!validate()) return

  try {
    if (form.contentType === 'historia') {
      const memory = await createMemory({
        title: `${form.name} — Historia 50 aniversario`,
        content: form.description,
        year: form.year ?? 2026,
      })
      if (!memory) {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: memoryError.value ?? 'Error al enviar la historia',
          life: 5000,
        })
        return
      }
    } else {
      const mediaType = contentTypeToMediaType[form.contentType!]
      const isImage = form.contentType === 'foto'

      const blobResult = await uploadFile({
        file: selectedFile.value!,
        folder: 'media-items',
        generateThumbnail: isImage,
      })
      if (!blobResult) {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: uploadError.value ?? 'Error al subir el archivo',
          life: 5000,
        })
        return
      }

      const mediaItem = await createMediaItem({
        fileUrl: blobResult.fileUrl,
        thumbnailUrl: blobResult.thumbnailUrl,
        type: mediaType,
        title: `${form.name} — Recuerdo 50 aniversario`,
        description: form.description || undefined,
        year: form.year ?? 2026,
        context: 'anniversary-50',
      })
      if (!mediaItem) {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: mediaError.value ?? 'Error al crear el recuerdo',
          life: 5000,
        })
        return
      }
    }

    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: '¡Tu recuerdo ha sido enviado! Lo revisaremos pronto.',
      life: 4000,
    })
    resetForm()
  } catch {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: 'Error inesperado al enviar el recuerdo',
      life: 5000,
    })
  }
}
</script>

<template>
  <section id="subir-recuerdo" aria-label="Comparte tu recuerdo" class="mx-auto max-w-2xl px-6">
    <div class="mb-8 text-center">
      <h2 class="mb-4 text-3xl font-bold text-amber-900 md:text-4xl">
        Colabora con la Memoria ABUVINA
      </h2>
      <p class="mx-auto max-w-xl text-gray-600">
        El 50 aniversario lo construimos entre todos. Si tienes fotos antiguas, anécdotas escritas o
        quieres grabar un mensaje de voz para la cápsula del tiempo, este es el lugar.
      </p>
    </div>

    <form class="space-y-6 rounded-xl bg-white p-8 shadow-sm" @submit.prevent="handleSubmit">
      <!-- Nombre -->
      <div>
        <label for="upload-name" class="mb-2 block text-sm font-semibold text-gray-700">
          Nombre <span class="text-red-500">*</span>
        </label>
        <InputText
          id="upload-name"
          v-model="form.name"
          placeholder="Tu nombre completo"
          class="w-full"
          :invalid="!!errors.name"
        />
        <small v-if="errors.name" class="mt-1 block text-red-500">{{ errors.name }}</small>
      </div>

      <!-- Tipo de contenido -->
      <div>
        <label for="upload-type" class="mb-2 block text-sm font-semibold text-gray-700">
          Tipo de contenido <span class="text-red-500">*</span>
        </label>
        <Select
          id="upload-type"
          v-model="form.contentType"
          :options="contentTypes"
          option-label="label"
          option-value="value"
          placeholder="Selecciona el tipo de contenido"
          class="w-full"
          :invalid="!!errors.contentType"
        />
        <small v-if="errors.contentType" class="mt-1 block text-red-500">{{
          errors.contentType
        }}</small>
      </div>

      <!-- Año aproximado -->
      <div>
        <label for="upload-year" class="mb-2 block text-sm font-semibold text-gray-700">
          Año aproximado
        </label>
        <InputNumber
          id="upload-year"
          v-model="form.year"
          :min="1976"
          :max="2026"
          :use-grouping="false"
          placeholder="Ej: 2001"
          class="w-full"
        />
      </div>

      <!-- Descripción -->
      <div>
        <label for="upload-description" class="mb-2 block text-sm font-semibold text-gray-700">
          {{ form.contentType === 'historia' ? 'Tu historia *' : 'Descripción / mensaje' }}
        </label>
        <Textarea
          id="upload-description"
          v-model="form.description"
          :maxlength="form.contentType === 'historia' ? 5000 : 500"
          :rows="form.contentType === 'historia' ? 8 : 4"
          :placeholder="
            form.contentType === 'historia'
              ? 'Escribe aquí tu historia o anécdota...'
              : 'Cuéntanos algo sobre este recuerdo...'
          "
          class="w-full"
          :invalid="!!errors.description"
        />
        <small v-if="errors.description" class="mt-1 block text-red-500">{{
          errors.description
        }}</small>
        <small class="mt-1 block text-right text-gray-400">
          {{ form.description.length }}/{{ form.contentType === 'historia' ? 5000 : 500 }}
        </small>
      </div>

      <!-- Archivo (only for non-story types) -->
      <div v-if="form.contentType && form.contentType !== 'historia'">
        <label class="mb-2 block text-sm font-semibold text-gray-700">
          Archivo <span class="text-red-500">*</span>
        </label>
        <FileUpload
          mode="basic"
          name="memory"
          accept="image/*,video/*,audio/*"
          :max-file-size="50000000"
          choose-label="Seleccionar archivo"
          class="w-full"
          :auto="false"
          @select="(e: { files: File[] }) => (selectedFile = e.files[0])"
        />
        <small v-if="errors.file" class="mt-1 block text-red-500">{{ errors.file }}</small>
        <small class="mt-1 block text-gray-400">
          Formatos admitidos: imagen, vídeo o audio. Máx. 50 MB.
        </small>
      </div>

      <!-- Progress -->
      <ProgressBar
        v-if="isSubmitting"
        mode="indeterminate"
        class="mt-4"
        style="height: 6px"
        role="status"
      />

      <!-- Submit -->
      <div class="pt-2">
        <Button
          type="submit"
          label="Enviar recuerdo"
          icon="pi pi-send"
          class="w-full"
          :disabled="isSubmitting"
          :loading="isSubmitting"
        />
      </div>
    </form>
  </section>
</template>

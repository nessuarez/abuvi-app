<script setup lang="ts">
import { ref, reactive, computed, onMounted, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import { useBlobStorage } from '@/composables/useBlobStorage'
import { useMediaItems } from '@/composables/useMediaItems'
import { useMemories } from '@/composables/useMemories'
import { useCampHistory } from '@/composables/useCampHistory'
import type { MediaItemType } from '@/types/media-item'
import type { CampEditionOption } from '@/types/camp-history'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Checkbox from 'primevue/checkbox'
import Textarea from 'primevue/textarea'
import FileUpload from 'primevue/fileupload'
import Button from 'primevue/button'
import ProgressBar from 'primevue/progressbar'

const route = useRoute()
const toast = useToast()
const { uploadFile, uploading, uploadError } = useBlobStorage()
const { createMediaItem, creating: creatingMedia, createError: mediaError } = useMediaItems()
const { createMemory, creating: creatingMemory, createError: memoryError } = useMemories()
const { editionOptions, fetchEditionOptions } = useCampHistory()

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
  rightsAccepted: false,
})

const selectedFile = ref<File | null>(null)
const errors = ref<Record<string, string>>({})

const isSubmitting = computed(() => uploading.value || creatingMedia.value || creatingMemory.value)

/**
 * The QR printed for the camp may name a year that no endpoint can offer yet — the current
 * edition is only reachable once it leaves Draft. Rather than silently dropping what the
 * poster asked for, show it as a plain year.
 */
const yearOptions = computed<CampEditionOption[]>(() => {
  const options = editionOptions.value
  if (form.year === null || options.some((option) => option.year === form.year)) return options

  return [
    { year: form.year, label: String(form.year), campName: null, isCurrent: false },
    ...options,
  ]
})

const validate = (): boolean => {
  errors.value = {}
  if (!form.name.trim()) errors.value.name = 'El nombre es obligatorio'
  if (!form.contentType) errors.value.contentType = 'El tipo de contenido es obligatorio'
  if (form.year === null) errors.value.year = 'Elige la edición a la que pertenece el recuerdo'
  if (form.contentType && form.contentType !== 'historia' && !selectedFile.value) {
    errors.value.file = 'Debes seleccionar un archivo'
  }
  if (form.contentType === 'historia' && !form.description.trim()) {
    errors.value.description = 'La descripción es obligatoria para historias escritas'
  }
  if (!form.rightsAccepted) {
    errors.value.rights = 'Debes aceptar esta declaración para enviar'
  }
  return Object.keys(errors.value).length === 0
}

const resetForm = () => {
  form.name = ''
  form.contentType = null
  form.year = null
  form.description = ''
  // Cleared on purpose: a box left ticked would mean the next contributor never declared anything.
  form.rightsAccepted = false
  selectedFile.value = null
  errors.value = {}
}

/**
 * Prefill from the QR code: /anniversary?tipo=audio&anio=2026#subir-recuerdo
 * Anything unrecognised is ignored — a mistyped poster must not produce a broken page.
 */
const applyQueryPrefill = () => {
  const tipo = route.query.tipo
  if (typeof tipo === 'string' && contentTypes.some((t) => t.value === tipo)) {
    form.contentType = tipo
  }

  const anio = Number(route.query.anio)
  if (Number.isInteger(anio) && anio >= 1976 && anio <= new Date().getFullYear() + 1) {
    form.year = anio
  }
}

onMounted(async () => {
  applyQueryPrefill()
  fetchEditionOptions()

  // Browsers do not reliably honour a fragment on an SPA's first paint.
  if (route.hash === '#subir-recuerdo') {
    await nextTick()
    document.getElementById('subir-recuerdo')?.scrollIntoView({ behavior: 'smooth' })
  }
})

const handleSubmit = async () => {
  if (!validate()) return

  try {
    if (form.contentType === 'historia') {
      const memory = await createMemory({
        title: `${form.name} — Historia 50 aniversario`,
        content: form.description,
        year: form.year!,
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
        year: form.year!,
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
        El 50 aniversario lo construimos entre todas las personas. Si tienes fotos antiguas, anécdotas escritas o
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
          placeholder="Nombre completo"
          class="w-full"
          :invalid="!!errors.name"
          aria-describedby="upload-name-help"
        />
        <small v-if="errors.name" class="mt-1 block text-red-500">{{ errors.name }}</small>
        <small id="upload-name-help" class="mt-1 block text-gray-400">
          El nombre de quien recuerda. Si subes el recuerdo de otra persona, pon el suyo.
        </small>
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

      <!-- Edición -->
      <div>
        <label for="upload-year" class="mb-2 block text-sm font-semibold text-gray-700">
          Edición <span class="text-red-500">*</span>
        </label>
        <Select
          id="upload-year"
          v-model="form.year"
          :options="yearOptions"
          option-label="label"
          option-value="year"
          filter
          placeholder="¿De qué campamento es este recuerdo?"
          class="w-full"
          :invalid="!!errors.year"
        />
        <small v-if="errors.year" class="mt-1 block text-red-500">{{ errors.year }}</small>
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

      <!-- Derechos de imagen -->
      <div class="rounded-lg border border-amber-200 bg-amber-50 p-4">
        <div class="flex items-start gap-3">
          <Checkbox
            v-model="form.rightsAccepted"
            input-id="upload-rights"
            binary
            :invalid="!!errors.rights"
          />
          <label for="upload-rights" class="text-sm text-gray-700">
            Tengo derecho a compartir esta imagen y respeto la privacidad de quienes aparecen.
            <span class="text-red-500">*</span>
          </label>
        </div>
        <small v-if="errors.rights" class="mt-2 block text-red-500">{{ errors.rights }}</small>
      </div>

      <!-- Qué pasa con lo que envías -->
      <p class="text-sm text-gray-500">
        Todo lo que envíes se revisa antes de publicarse: no aparece en la web de forma automática.
        Si quieres que retiremos algo en lo que apareces, escríbenos desde
        <a href="#contacto" class="font-medium text-amber-700 hover:underline"
          >el formulario de contacto</a
        >
        y lo quitamos.
      </p>

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

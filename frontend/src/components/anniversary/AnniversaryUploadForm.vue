<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import FileUpload from 'primevue/fileupload'
import Button from 'primevue/button'

const toast = useToast()

const contentTypes = [
  { label: 'Foto', value: 'foto' },
  { label: 'Vídeo', value: 'video' },
  { label: 'Audio', value: 'audio' },
  { label: 'Historia escrita', value: 'historia' },
]

const form = reactive({
  name: '',
  contentType: null as string | null,
  year: null as number | null,
  description: '',
})

const errors = ref<Record<string, string>>({})

const validate = (): boolean => {
  errors.value = {}
  if (!form.name.trim()) errors.value.name = 'El nombre es obligatorio'
  if (!form.contentType) errors.value.contentType = 'El tipo de contenido es obligatorio'
  return Object.keys(errors.value).length === 0
}

const handleSubmit = () => {
  if (!validate()) return
  toast.add({
    severity: 'success',
    summary: 'Éxito',
    detail: '¡Gracias por compartir tu recuerdo!',
    life: 4000,
  })
  form.name = ''
  form.contentType = null
  form.year = null
  form.description = ''
  errors.value = {}
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
          Descripción / mensaje
        </label>
        <Textarea
          id="upload-description"
          v-model="form.description"
          :maxlength="500"
          :rows="4"
          placeholder="Cuéntanos algo sobre este recuerdo..."
          class="w-full"
        />
        <small class="mt-1 block text-right text-gray-400">{{ form.description.length }}/500</small>
      </div>

      <!-- Archivo -->
      <div>
        <label class="mb-2 block text-sm font-semibold text-gray-700">Archivo</label>
        <FileUpload
          mode="basic"
          name="memory"
          accept="image/*,video/*,audio/*"
          :max-file-size="50000000"
          choose-label="Seleccionar archivo"
          class="w-full"
        />
        <small class="mt-1 block text-gray-400">
          Formatos admitidos: imagen, vídeo o audio. Máx. 50 MB.
        </small>
      </div>

      <!-- Submit -->
      <div class="pt-2">
        <Button type="submit" label="Enviar recuerdo" icon="pi pi-send" class="w-full" />
      </div>
    </form>
  </section>
</template>

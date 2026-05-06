<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import Button from 'primevue/button'
import Message from 'primevue/message'
import EmojiPickerField from '@/components/ui/EmojiPickerField.vue'
import { useAccommodationFeatures } from '@/composables/useAccommodationFeatures'
import { FEATURE_APPLICABILITY_LABELS } from '@/types/accommodation-feature'
import type { AccommodationFeature, FeatureApplicabilityLevel } from '@/types/accommodation-feature'

const props = defineProps<{
  visible: boolean
  feature?: AccommodationFeature | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: []
}>()

const { createFeature, updateFeature, saving, saveError } = useAccommodationFeatures()

const isEdit = computed(() => !!props.feature)

const LEVEL_OPTIONS = (
  Object.entries(FEATURE_APPLICABILITY_LABELS) as [FeatureApplicabilityLevel, string][]
).map(([value, label]) => ({ value, label }))

const name = ref('')
const icon = ref('')
const description = ref('')
const applicabilityLevel = ref<FeatureApplicabilityLevel>('Any')
const isActive = ref(true)
const sortOrder = ref(0)
const validationErrors = ref<Record<string, string>>({})

watch(
  () => props.visible,
  (visible) => {
    if (visible) {
      validationErrors.value = {}
      saveError.value = null
      if (props.feature) {
        name.value = props.feature.name
        icon.value = props.feature.icon
        description.value = props.feature.description ?? ''
        applicabilityLevel.value = props.feature.applicabilityLevel
        isActive.value = props.feature.isActive
        sortOrder.value = props.feature.sortOrder
      } else {
        name.value = ''
        icon.value = ''
        description.value = ''
        applicabilityLevel.value = 'Any'
        isActive.value = true
        sortOrder.value = 0
      }
    }
  },
)

const validate = (): boolean => {
  const errors: Record<string, string> = {}
  if (!name.value.trim()) errors.name = 'El nombre es obligatorio'
  else if (name.value.trim().length > 100) errors.name = 'Máximo 100 caracteres'
  if (!icon.value.trim()) errors.icon = 'El icono es obligatorio'
  if (description.value.length > 500) errors.description = 'Máximo 500 caracteres'
  if (sortOrder.value < 0) errors.sortOrder = 'El orden debe ser mayor o igual a 0'
  validationErrors.value = errors
  return Object.keys(errors).length === 0
}

const handleSave = async () => {
  if (!validate()) return

  let result: AccommodationFeature | null = null

  if (isEdit.value && props.feature) {
    result = await updateFeature(props.feature.id, {
      name: name.value.trim(),
      icon: icon.value.trim(),
      description: description.value.trim() || null,
      applicabilityLevel: applicabilityLevel.value,
      isActive: isActive.value,
      sortOrder: sortOrder.value,
    })
  } else {
    result = await createFeature({
      name: name.value.trim(),
      icon: icon.value.trim(),
      description: description.value.trim() || null,
      applicabilityLevel: applicabilityLevel.value,
      sortOrder: sortOrder.value,
    })
  }

  if (result) {
    emit('saved')
    emit('update:visible', false)
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    :header="isEdit ? 'Editar característica' : 'Nueva característica'"
    modal
    :closable="!saving"
    class="w-full max-w-lg"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="flex flex-col gap-4">
      <Message v-if="saveError" severity="error" :closable="false" class="mb-2">
        {{ saveError }}
      </Message>

      <!-- Name -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Nombre *</label>
        <InputText
          v-model="name"
          :maxlength="100"
          placeholder="Ej: Wifi, Piscina, Accesible"
          class="w-full"
          :invalid="!!validationErrors.name"
        />
        <small v-if="validationErrors.name" class="text-red-500">{{ validationErrors.name }}</small>
      </div>

      <!-- Icon picker -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Icono *</label>
        <p class="mb-1 text-xs text-gray-400">
          Busca por nombre en inglés (ej: "wifi", "pool", "bed", "shower").
        </p>
        <EmojiPickerField v-model="icon" :error="validationErrors.icon" />
        <small v-if="validationErrors.icon" class="mt-1 block text-red-500">{{ validationErrors.icon }}</small>
      </div>

      <!-- Applicability Level -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Nivel de aplicación *</label>
        <Select
          v-model="applicabilityLevel"
          :options="LEVEL_OPTIONS"
          option-label="label"
          option-value="value"
          class="w-full"
        />
      </div>

      <!-- Description -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Descripción</label>
        <Textarea
          v-model="description"
          :rows="2"
          :maxlength="500"
          placeholder="Descripción opcional..."
          class="w-full"
          :invalid="!!validationErrors.description"
        />
        <small v-if="validationErrors.description" class="text-red-500">{{ validationErrors.description }}</small>
      </div>

      <!-- Sort Order -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Orden</label>
        <InputNumber v-model="sortOrder" :min="0" class="w-full" />
        <small v-if="validationErrors.sortOrder" class="text-red-500">{{ validationErrors.sortOrder }}</small>
      </div>

      <!-- Is Active (edit only) -->
      <div v-if="isEdit" class="flex items-center gap-3">
        <ToggleSwitch v-model="isActive" />
        <label class="text-sm text-gray-700">Activo</label>
      </div>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button
          label="Cancelar"
          severity="secondary"
          text
          :disabled="saving"
          @click="emit('update:visible', false)"
        />
        <Button :label="isEdit ? 'Guardar' : 'Crear'" :loading="saving" @click="handleSave" />
      </div>
    </template>
  </Dialog>
</template>

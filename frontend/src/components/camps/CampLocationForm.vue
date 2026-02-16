<script setup lang="ts">
import { reactive, ref, computed, watch } from 'vue'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import Button from 'primevue/button'
import type { Camp, CreateCampRequest, CampStatus } from '@/types/camp'

interface Props {
  camp?: Camp
  mode: 'create' | 'edit'
}

const props = defineProps<Props>()
const emit = defineEmits<{
  submit: [data: CreateCampRequest]
  cancel: []
}>()

const statusOptions = [
  { label: 'Activo', value: 'Active' },
  { label: 'Inactivo', value: 'Inactive' },
  { label: 'Archivo Histórico', value: 'HistoricalArchive' }
]

const formData = reactive<CreateCampRequest>({
  name: '',
  description: '',
  latitude: 40.4168,
  longitude: -3.7038,
  basePriceAdult: 0,
  basePriceChild: 0,
  basePriceBaby: 0,
  status: 'Active' as CampStatus
})

const errors = ref<Record<string, string>>({})
const submitting = ref(false)

// Initialize form data if editing
if (props.mode === 'edit' && props.camp) {
  Object.assign(formData, {
    name: props.camp.name,
    description: props.camp.description,
    latitude: props.camp.latitude,
    longitude: props.camp.longitude,
    basePriceAdult: props.camp.basePriceAdult,
    basePriceChild: props.camp.basePriceChild,
    basePriceBaby: props.camp.basePriceBaby,
    status: props.camp.status
  })
}

const validate = (): boolean => {
  errors.value = {}

  if (!formData.name.trim()) {
    errors.value.name = 'El nombre del campamento es obligatorio'
  } else if (formData.name.length > 200) {
    errors.value.name = 'El nombre no puede superar 200 caracteres'
  }

  if (formData.latitude < -90 || formData.latitude > 90) {
    errors.value.latitude = 'La latitud debe estar entre -90 y 90'
  }

  if (formData.longitude < -180 || formData.longitude > 180) {
    errors.value.longitude = 'La longitud debe estar entre -180 y 180'
  }

  if (formData.basePriceAdult < 0) {
    errors.value.basePriceAdult = 'El precio debe ser mayor o igual a 0'
  }

  if (formData.basePriceChild < 0) {
    errors.value.basePriceChild = 'El precio debe ser mayor o igual a 0'
  }

  if (formData.basePriceBaby < 0) {
    errors.value.basePriceBaby = 'El precio debe ser mayor o igual a 0'
  }

  return Object.keys(errors.value).length === 0
}

const handleSubmit = () => {
  if (!validate()) return

  submitting.value = true
  emit('submit', { ...formData })
  submitting.value = false
}

const isFormValid = computed(() => {
  return (
    formData.name.trim().length > 0 &&
    formData.latitude >= -90 &&
    formData.latitude <= 90 &&
    formData.longitude >= -180 &&
    formData.longitude <= 180 &&
    formData.basePriceAdult >= 0 &&
    formData.basePriceChild >= 0 &&
    formData.basePriceBaby >= 0
  )
})

// Clear error when field is modified
watch(
  () => formData.name,
  () => delete errors.value.name
)
</script>

<template>
  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <!-- Name -->
    <div>
      <label for="name" class="mb-1 block text-sm font-medium text-gray-700">
        Nombre del campamento *
      </label>
      <InputText
        id="name"
        v-model="formData.name"
        :invalid="!!errors.name"
        class="w-full"
        placeholder="Ej: Campamento de Montaña"
      />
      <small v-if="errors.name" class="text-red-500">{{ errors.name }}</small>
    </div>

    <!-- Description -->
    <div>
      <label for="description" class="mb-1 block text-sm font-medium text-gray-700">
        Descripción
      </label>
      <Textarea
        id="description"
        v-model="formData.description"
        class="w-full"
        rows="3"
        placeholder="Descripción detallada del campamento..."
      />
    </div>

    <!-- Coordinates -->
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div>
        <label for="latitude" class="mb-1 block text-sm font-medium text-gray-700">
          Latitud *
        </label>
        <InputNumber
          id="latitude"
          v-model="formData.latitude"
          :invalid="!!errors.latitude"
          :min-fraction-digits="2"
          :max-fraction-digits="6"
          :min="-90"
          :max="90"
          class="w-full"
        />
        <small v-if="errors.latitude" class="text-red-500">{{ errors.latitude }}</small>
        <small v-else class="text-gray-500">Entre -90 y 90</small>
      </div>

      <div>
        <label for="longitude" class="mb-1 block text-sm font-medium text-gray-700">
          Longitud *
        </label>
        <InputNumber
          id="longitude"
          v-model="formData.longitude"
          :invalid="!!errors.longitude"
          :min-fraction-digits="2"
          :max-fraction-digits="6"
          :min="-180"
          :max="180"
          class="w-full"
        />
        <small v-if="errors.longitude" class="text-red-500">{{ errors.longitude }}</small>
        <small v-else class="text-gray-500">Entre -180 y 180</small>
      </div>
    </div>

    <!-- Pricing -->
    <div class="rounded-lg border border-gray-200 p-4">
      <h4 class="mb-3 text-sm font-semibold text-gray-900">Precios Base</h4>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div>
          <label for="priceAdult" class="mb-1 block text-sm font-medium text-gray-700">
            Precio adulto (€) *
          </label>
          <InputNumber
            id="priceAdult"
            v-model="formData.basePriceAdult"
            :invalid="!!errors.basePriceAdult"
            mode="currency"
            currency="EUR"
            locale="es-ES"
            :min="0"
            class="w-full"
          />
          <small v-if="errors.basePriceAdult" class="text-red-500">{{
            errors.basePriceAdult
          }}</small>
        </div>

        <div>
          <label for="priceChild" class="mb-1 block text-sm font-medium text-gray-700">
            Precio niño (€) *
          </label>
          <InputNumber
            id="priceChild"
            v-model="formData.basePriceChild"
            :invalid="!!errors.basePriceChild"
            mode="currency"
            currency="EUR"
            locale="es-ES"
            :min="0"
            class="w-full"
          />
          <small v-if="errors.basePriceChild" class="text-red-500">{{
            errors.basePriceChild
          }}</small>
        </div>

        <div>
          <label for="priceBaby" class="mb-1 block text-sm font-medium text-gray-700">
            Precio bebé (€) *
          </label>
          <InputNumber
            id="priceBaby"
            v-model="formData.basePriceBaby"
            :invalid="!!errors.basePriceBaby"
            mode="currency"
            currency="EUR"
            locale="es-ES"
            :min="0"
            class="w-full"
          />
          <small v-if="errors.basePriceBaby" class="text-red-500">{{
            errors.basePriceBaby
          }}</small>
        </div>
      </div>
    </div>

    <!-- Status -->
    <div>
      <label for="status" class="mb-1 block text-sm font-medium text-gray-700"> Estado * </label>
      <Select
        id="status"
        v-model="formData.status"
        :options="statusOptions"
        option-label="label"
        option-value="value"
        class="w-full"
      />
    </div>

    <!-- Actions -->
    <div class="flex justify-end gap-2 pt-4">
      <Button label="Cancelar" severity="secondary" outlined @click="emit('cancel')" />
      <Button
        type="submit"
        :label="mode === 'create' ? 'Crear Campamento' : 'Guardar Cambios'"
        :loading="submitting"
        :disabled="!isFormValid || submitting"
      />
    </div>
  </form>
</template>

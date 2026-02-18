<script setup lang="ts">
import { reactive, ref, computed, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import InputNumber from 'primevue/inputnumber'
import AutoComplete from 'primevue/autocomplete'
import Message from 'primevue/message'
import Button from 'primevue/button'
import { useGooglePlaces, type PlaceAutocomplete } from '@/composables/useGooglePlaces'
import AccommodationCapacityForm from '@/components/camps/AccommodationCapacityForm.vue'
import type { Camp, CreateCampRequest, UpdateCampRequest, AccommodationCapacity } from '@/types/camp'

interface Props {
  camp?: Camp
  mode: 'create' | 'edit'
}

const props = defineProps<Props>()
const emit = defineEmits<{
  submit: [data: CreateCampRequest | UpdateCampRequest]
  cancel: []
}>()

const { loading: placesLoading, error: placesError, searchPlaces, getPlaceDetails } = useGooglePlaces()

const formData = reactive<CreateCampRequest | UpdateCampRequest>({
  name: '',
  description: null,
  location: null,
  latitude: null,
  longitude: null,
  googlePlaceId: null,
  pricePerAdult: 0,
  pricePerChild: 0,
  pricePerBaby: 0,
  accommodationCapacity: null,
  ...(props.mode === 'edit' && { isActive: true })
})

const placeSuggestions = ref<PlaceAutocomplete[]>([])
const selectedPlace = ref<PlaceAutocomplete | null>(null)
const autoFilledFromPlaces = ref(false)
const searchQuery = ref(formData.name)
const errors = ref<Record<string, string>>({})
const submitting = ref(false)

// Initialize form data if editing
if (props.mode === 'edit' && props.camp) {
  Object.assign(formData, {
    name: props.camp.name,
    description: props.camp.description,
    location: props.camp.location,
    latitude: props.camp.latitude,
    longitude: props.camp.longitude,
    googlePlaceId: props.camp.googlePlaceId,
    pricePerAdult: props.camp.pricePerAdult,
    pricePerChild: props.camp.pricePerChild,
    pricePerBaby: props.camp.pricePerBaby,
    accommodationCapacity: props.camp.accommodationCapacity ?? null,
    isActive: props.camp.isActive
  })
  searchQuery.value = props.camp.name
}

// Debounced autocomplete search
const debouncedSearch = useDebounceFn(async (query: string) => {
  if (!query || query.length < 3) {
    placeSuggestions.value = []
    return
  }
  placeSuggestions.value = await searchPlaces(query)
}, 300)

// Watch search query for autocomplete
watch(searchQuery, (newQuery) => {
  debouncedSearch(newQuery)
})

// Sync searchQuery with formData.name when typing directly
watch(searchQuery, (newQuery) => {
  formData.name = newQuery
})

// Handle place selection from autocomplete
const handlePlaceSelected = async (event: { value: PlaceAutocomplete }) => {
  const place = event.value
  if (!place) return

  selectedPlace.value = place
  const details = await getPlaceDetails(place.placeId)

  if (details) {
    formData.name = details.name
    formData.location = details.formattedAddress
    formData.latitude = details.latitude
    formData.longitude = details.longitude
    formData.googlePlaceId = details.placeId

    // Auto-generate description if empty
    if (!formData.description) {
      formData.description = generateDescription(details)
    }

    autoFilledFromPlaces.value = true
    searchQuery.value = details.name
  }
}

// Generate automatic description from place details
const generateDescription = (details: { name: string; formattedAddress: string; types: string[] }): string => {
  const typeDescriptions: Record<string, string> = {
    'campground': 'Zona de camping',
    'park': 'Parque natural',
    'lodging': 'Alojamiento',
    'establishment': 'Establecimiento'
  }

  const matchedType = details.types.find(t => typeDescriptions[t])
  const typeDesc = matchedType ? typeDescriptions[matchedType] : 'Ubicación'

  return `${typeDesc} ubicada en ${details.formattedAddress}`
}

// Clear autocomplete and allow manual entry
const clearAutocomplete = () => {
  selectedPlace.value = null
  autoFilledFromPlaces.value = false
  formData.googlePlaceId = null
  searchQuery.value = formData.name
  placeSuggestions.value = []
}

const validate = (): boolean => {
  errors.value = {}

  if (!formData.name.trim()) {
    errors.value.name = 'El nombre del campamento es obligatorio'
  } else if (formData.name.length > 200) {
    errors.value.name = 'El nombre no puede superar 200 caracteres'
  }

  if (formData.latitude !== null && (formData.latitude < -90 || formData.latitude > 90)) {
    errors.value.latitude = 'La latitud debe estar entre -90 y 90'
  }

  if (formData.longitude !== null && (formData.longitude < -180 || formData.longitude > 180)) {
    errors.value.longitude = 'La longitud debe estar entre -180 y 180'
  }

  if (formData.pricePerAdult < 0) {
    errors.value.pricePerAdult = 'El precio debe ser mayor o igual a 0'
  }

  if (formData.pricePerChild < 0) {
    errors.value.pricePerChild = 'El precio debe ser mayor o igual a 0'
  }

  if (formData.pricePerBaby < 0) {
    errors.value.pricePerBaby = 'El precio debe ser mayor o igual a 0'
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
    (formData.latitude === null || (formData.latitude >= -90 && formData.latitude <= 90)) &&
    (formData.longitude === null || (formData.longitude >= -180 && formData.longitude <= 180)) &&
    formData.pricePerAdult >= 0 &&
    formData.pricePerChild >= 0 &&
    formData.pricePerBaby >= 0
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
    <!-- Name with Autocomplete -->
    <div>
      <label for="name" class="mb-1 block text-sm font-medium text-gray-700">
        Nombre del campamento *
        <span class="text-xs text-gray-500">(Empieza a escribir para buscar)</span>
      </label>

      <AutoComplete
        id="name"
        v-model="searchQuery"
        :suggestions="placeSuggestions"
        option-label="description"
        placeholder="Buscar ubicación..."
        class="w-full"
        :loading="placesLoading"
        :invalid="!!errors.name"
        @complete="debouncedSearch(searchQuery)"
        @item-select="handlePlaceSelected"
      >
        <template #item="{ item }">
          <div class="flex flex-col">
            <span class="font-semibold">{{ item.mainText }}</span>
            <span class="text-sm text-gray-500">{{ item.secondaryText }}</span>
          </div>
        </template>
      </AutoComplete>

      <small v-if="errors.name" class="text-red-500">{{ errors.name }}</small>

      <Button
        v-if="autoFilledFromPlaces"
        label="Escribir manualmente"
        icon="pi pi-pencil"
        text
        size="small"
        class="mt-1"
        @click="clearAutocomplete"
      />
    </div>

    <!-- Auto-filled indicator -->
    <Message
      v-if="autoFilledFromPlaces"
      severity="info"
      :closable="false"
      class="mt-2"
    >
      <i class="pi pi-check-circle mr-2"></i>
      Datos cargados desde Google Places. Puedes ajustarlos antes de guardar.
    </Message>

    <!-- Places API error -->
    <Message v-if="placesError" severity="error" :closable="true">
      {{ placesError }}
    </Message>

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

    <!-- Location -->
    <div>
      <label for="location" class="mb-1 block text-sm font-medium text-gray-700">
        Ubicación
        <span v-if="autoFilledFromPlaces" class="text-xs text-blue-600">(Auto-completado)</span>
      </label>
      <InputText
        id="location"
        v-model="formData.location"
        class="w-full"
        placeholder="Dirección del campamento..."
      />
    </div>

    <!-- Coordinates -->
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div>
        <label for="latitude" class="mb-1 block text-sm font-medium text-gray-700">
          Latitud
          <span v-if="autoFilledFromPlaces" class="text-xs text-blue-600">(Auto-completado)</span>
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
          placeholder="40.416775"
        />
        <small v-if="errors.latitude" class="text-red-500">{{ errors.latitude }}</small>
        <small v-else class="text-gray-500">Entre -90 y 90</small>
      </div>

      <div>
        <label for="longitude" class="mb-1 block text-sm font-medium text-gray-700">
          Longitud
          <span v-if="autoFilledFromPlaces" class="text-xs text-blue-600">(Auto-completado)</span>
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
          placeholder="-3.703790"
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
            v-model="formData.pricePerAdult"
            :invalid="!!errors.pricePerAdult"
            mode="currency"
            currency="EUR"
            locale="es-ES"
            :min="0"
            class="w-full"
          />
          <small v-if="errors.pricePerAdult" class="text-red-500">{{
            errors.pricePerAdult
          }}</small>
        </div>

        <div>
          <label for="priceChild" class="mb-1 block text-sm font-medium text-gray-700">
            Precio niño (€) *
          </label>
          <InputNumber
            id="priceChild"
            v-model="formData.pricePerChild"
            :invalid="!!errors.pricePerChild"
            mode="currency"
            currency="EUR"
            locale="es-ES"
            :min="0"
            class="w-full"
          />
          <small v-if="errors.pricePerChild" class="text-red-500">{{
            errors.pricePerChild
          }}</small>
        </div>

        <div>
          <label for="priceBaby" class="mb-1 block text-sm font-medium text-gray-700">
            Precio bebé (€) *
          </label>
          <InputNumber
            id="priceBaby"
            v-model="formData.pricePerBaby"
            :invalid="!!errors.pricePerBaby"
            mode="currency"
            currency="EUR"
            locale="es-ES"
            :min="0"
            class="w-full"
          />
          <small v-if="errors.pricePerBaby" class="text-red-500">{{
            errors.pricePerBaby
          }}</small>
        </div>
      </div>
    </div>

    <!-- Accommodation Capacity -->
    <AccommodationCapacityForm
      :model-value="formData.accommodationCapacity ?? null"
      @update:model-value="(val: AccommodationCapacity | null) => (formData.accommodationCapacity = val)"
    />

    <!-- Status (Edit mode only) -->
    <div v-if="mode === 'edit' && 'isActive' in formData">
      <label class="mb-1 block text-sm font-medium text-gray-700">Estado</label>
      <div class="flex items-center gap-2">
        <input
          id="isActive"
          v-model="formData.isActive"
          type="checkbox"
          class="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <label for="isActive" class="text-sm text-gray-700">Campamento activo</label>
      </div>
    </div>

    <!-- Actions -->
    <div class="flex justify-end gap-2 pt-4">
      <Button label="Cancelar" severity="secondary" outlined @click="emit('cancel')" />
      <Button
        type="submit"
        :label="mode === 'create' ? 'Crear Campamento' : 'Guardar Cambios'"
        :loading="submitting"
        :disabled="!isFormValid || submitting || placesLoading"
      />
    </div>
  </form>
</template>

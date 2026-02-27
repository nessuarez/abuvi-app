<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useCampAccommodations } from '@/composables/useCampAccommodations'
import type { CampEditionAccommodation, AccommodationType } from '@/types/camp-edition'

const props = defineProps<{
  visible: boolean
  editionId: string
  accommodation?: CampEditionAccommodation
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: []
}>()

const toast = useToast()
const { createAccommodation, updateAccommodation, loading, error } = useCampAccommodations(
  props.editionId
)

const isEdit = computed(() => !!props.accommodation)

const ACCOMMODATION_TYPE_OPTIONS: { label: string; value: AccommodationType }[] = [
  { label: 'Refugio', value: 'Lodge' },
  { label: 'Caravana', value: 'Caravan' },
  { label: 'Tienda de campaña', value: 'Tent' },
  { label: 'Bungalow', value: 'Bungalow' },
  { label: 'Autocaravana', value: 'Motorhome' }
]

const name = ref('')
const accommodationType = ref<AccommodationType>('Lodge')
const description = ref('')
const capacity = ref<number | null>(null)
const sortOrder = ref(0)
const isActive = ref(true)
const validationErrors = ref<Record<string, string>>({})

watch(
  () => props.visible,
  (visible) => {
    if (visible) {
      validationErrors.value = {}
      error.value = null
      if (props.accommodation) {
        name.value = props.accommodation.name
        accommodationType.value = props.accommodation.accommodationType
        description.value = props.accommodation.description ?? ''
        capacity.value = props.accommodation.capacity ?? null
        sortOrder.value = props.accommodation.sortOrder
        isActive.value = props.accommodation.isActive
      } else {
        name.value = ''
        accommodationType.value = 'Lodge'
        description.value = ''
        capacity.value = null
        sortOrder.value = 0
        isActive.value = true
      }
    }
  }
)

const validate = (): boolean => {
  const errors: Record<string, string> = {}
  if (!name.value.trim()) errors.name = 'El nombre es obligatorio'
  else if (name.value.trim().length > 200) errors.name = 'Máximo 200 caracteres'
  if (description.value.length > 1000) errors.description = 'Máximo 1000 caracteres'
  if (capacity.value !== null && capacity.value < 1) errors.capacity = 'Mínimo 1'
  validationErrors.value = errors
  return Object.keys(errors).length === 0
}

const handleSave = async () => {
  if (!validate()) return

  if (isEdit.value && props.accommodation) {
    const result = await updateAccommodation(props.accommodation.id, {
      name: name.value.trim(),
      accommodationType: accommodationType.value,
      description: description.value.trim() || undefined,
      capacity: capacity.value ?? undefined,
      isActive: isActive.value,
      sortOrder: sortOrder.value
    })
    if (result) {
      toast.add({
        severity: 'success',
        summary: 'Alojamiento actualizado',
        life: 3000
      })
      emit('saved')
      emit('update:visible', false)
    }
  } else {
    const result = await createAccommodation({
      name: name.value.trim(),
      accommodationType: accommodationType.value,
      description: description.value.trim() || undefined,
      capacity: capacity.value ?? undefined,
      sortOrder: sortOrder.value
    })
    if (result) {
      toast.add({
        severity: 'success',
        summary: 'Alojamiento creado',
        life: 3000
      })
      emit('saved')
      emit('update:visible', false)
    }
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    :header="isEdit ? 'Editar alojamiento' : 'Nuevo alojamiento'"
    modal
    :closable="!loading"
    class="w-full max-w-lg"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="flex flex-col gap-4">
      <Message v-if="error" severity="error" :closable="false" class="mb-2">
        {{ error }}
      </Message>

      <!-- Name -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Nombre *</label>
        <InputText
          v-model="name"
          :maxlength="200"
          placeholder="Ej: Refugio Norte"
          class="w-full"
          :invalid="!!validationErrors.name"
        />
        <small v-if="validationErrors.name" class="text-red-500">
          {{ validationErrors.name }}
        </small>
      </div>

      <!-- Accommodation Type -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Tipo *</label>
        <Select
          v-model="accommodationType"
          :options="ACCOMMODATION_TYPE_OPTIONS"
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
          :rows="3"
          :maxlength="1000"
          placeholder="Descripción opcional del alojamiento..."
          class="w-full"
          :invalid="!!validationErrors.description"
        />
        <small v-if="validationErrors.description" class="text-red-500">
          {{ validationErrors.description }}
        </small>
      </div>

      <!-- Capacity -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Capacidad</label>
        <InputNumber
          v-model="capacity"
          :min="1"
          placeholder="Sin límite"
          class="w-full"
          :invalid="!!validationErrors.capacity"
        />
        <small v-if="validationErrors.capacity" class="text-red-500">
          {{ validationErrors.capacity }}
        </small>
        <small v-else class="text-gray-400">Dejar vacío si no hay límite</small>
      </div>

      <!-- Sort Order -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Orden</label>
        <InputNumber v-model="sortOrder" :min="0" class="w-full" />
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
          :disabled="loading"
          @click="emit('update:visible', false)"
        />
        <Button
          :label="isEdit ? 'Guardar' : 'Crear'"
          :loading="loading"
          @click="handleSave"
        />
      </div>
    </template>
  </Dialog>
</template>

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
import { useCampExtras } from '@/composables/useCampExtras'
import type { CampEditionExtra } from '@/types/camp-edition'

const props = defineProps<{
  visible: boolean
  editionId: string
  extra?: CampEditionExtra
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: []
}>()

const toast = useToast()
const { createExtra, updateExtra, loading, error } = useCampExtras(props.editionId)

const isEdit = computed(() => !!props.extra)

const PRICING_TYPE_OPTIONS: { label: string; value: 'PerPerson' | 'PerFamily' }[] = [
  { label: 'Por persona', value: 'PerPerson' },
  { label: 'Por familia', value: 'PerFamily' }
]

const PRICING_PERIOD_OPTIONS: { label: string; value: 'OneTime' | 'PerDay' }[] = [
  { label: 'Una vez', value: 'OneTime' },
  { label: 'Por día', value: 'PerDay' }
]

const name = ref('')
const description = ref('')
const price = ref<number | null>(0)
const pricingType = ref<'PerPerson' | 'PerFamily'>('PerPerson')
const pricingPeriod = ref<'OneTime' | 'PerDay'>('OneTime')
const isRequired = ref(false)
const maxQuantity = ref<number | null>(null)
const isActive = ref(true)
const validationErrors = ref<Record<string, string>>({})

const pricingTypeLabel = computed(() =>
  PRICING_TYPE_OPTIONS.find((o) => o.value === pricingType.value)?.label ?? ''
)
const pricingPeriodLabel = computed(() =>
  PRICING_PERIOD_OPTIONS.find((o) => o.value === pricingPeriod.value)?.label ?? ''
)

watch(
  () => props.visible,
  (visible) => {
    if (visible) {
      validationErrors.value = {}
      error.value = null
      if (props.extra) {
        name.value = props.extra.name
        description.value = props.extra.description ?? ''
        price.value = props.extra.price
        pricingType.value = props.extra.pricingType
        pricingPeriod.value = props.extra.pricingPeriod
        isRequired.value = props.extra.isRequired
        maxQuantity.value = props.extra.maxQuantity ?? null
        isActive.value = props.extra.isActive
      } else {
        name.value = ''
        description.value = ''
        price.value = 0
        pricingType.value = 'PerPerson'
        pricingPeriod.value = 'OneTime'
        isRequired.value = false
        maxQuantity.value = null
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
  if (price.value === null || price.value < 0) errors.price = 'El precio debe ser 0 o mayor'
  if (maxQuantity.value !== null && maxQuantity.value < 1)
    errors.maxQuantity = 'La cantidad máxima debe ser mayor que 0'
  validationErrors.value = errors
  return Object.keys(errors).length === 0
}

const handleSave = async () => {
  if (!validate()) return

  if (isEdit.value && props.extra) {
    const result = await updateExtra(props.extra.id, {
      name: name.value.trim(),
      description: description.value.trim() || undefined,
      price: price.value ?? 0,
      isRequired: isRequired.value,
      isActive: isActive.value,
      maxQuantity: maxQuantity.value ?? undefined
    })
    if (result) {
      toast.add({ severity: 'success', summary: 'Extra actualizado', life: 3000 })
      emit('saved')
      emit('update:visible', false)
    }
  } else {
    const result = await createExtra({
      name: name.value.trim(),
      description: description.value.trim() || undefined,
      price: price.value ?? 0,
      pricingType: pricingType.value,
      pricingPeriod: pricingPeriod.value,
      isRequired: isRequired.value,
      maxQuantity: maxQuantity.value ?? undefined
    })
    if (result) {
      toast.add({ severity: 'success', summary: 'Extra creado', life: 3000 })
      emit('saved')
      emit('update:visible', false)
    }
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    :header="isEdit ? 'Editar extra' : 'Nuevo extra'"
    modal
    :closable="!loading"
    class="w-full max-w-lg"
    @update:visible="emit('update:visible', $event)"
    data-testid="extra-form-dialog"
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
          placeholder="Ej: Camiseta del campamento"
          class="w-full"
          :invalid="!!validationErrors.name"
          data-testid="extra-name-input"
        />
        <small v-if="validationErrors.name" class="text-red-500">
          {{ validationErrors.name }}
        </small>
      </div>

      <!-- Description -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Descripción</label>
        <Textarea
          v-model="description"
          :rows="3"
          :maxlength="1000"
          placeholder="Descripción opcional del extra..."
          class="w-full"
          :invalid="!!validationErrors.description"
        />
        <small v-if="validationErrors.description" class="text-red-500">
          {{ validationErrors.description }}
        </small>
      </div>

      <!-- Price -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Precio (€) *</label>
        <InputNumber
          v-model="price"
          mode="currency"
          currency="EUR"
          locale="es-ES"
          :min="0"
          class="w-full"
          :invalid="!!validationErrors.price"
          data-testid="extra-price-input"
        />
        <small v-if="validationErrors.price" class="text-red-500">
          {{ validationErrors.price }}
        </small>
      </div>

      <!-- Pricing Type -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Tipo de precio *</label>
        <Select
          v-if="!isEdit"
          v-model="pricingType"
          :options="PRICING_TYPE_OPTIONS"
          option-label="label"
          option-value="value"
          class="w-full"
        />
        <p v-else class="text-sm text-gray-600">{{ pricingTypeLabel }}</p>
      </div>

      <!-- Pricing Period -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Período de precio *</label>
        <Select
          v-if="!isEdit"
          v-model="pricingPeriod"
          :options="PRICING_PERIOD_OPTIONS"
          option-label="label"
          option-value="value"
          class="w-full"
        />
        <p v-else class="text-sm text-gray-600">{{ pricingPeriodLabel }}</p>
      </div>

      <!-- Is Required -->
      <div class="flex items-center gap-3">
        <ToggleSwitch v-model="isRequired" />
        <label class="text-sm text-gray-700">Obligatorio</label>
      </div>

      <!-- Max Quantity -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Cantidad máxima</label>
        <InputNumber
          v-model="maxQuantity"
          :min="1"
          placeholder="Sin límite"
          class="w-full"
          :invalid="!!validationErrors.maxQuantity"
        />
        <small v-if="validationErrors.maxQuantity" class="text-red-500">
          {{ validationErrors.maxQuantity }}
        </small>
        <small v-else class="text-gray-400">Dejar vacío si no hay límite</small>
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
          data-testid="extra-submit-button"
        />
      </div>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { reactive, ref, computed, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import DatePicker from 'primevue/datepicker'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import ToggleSwitch from 'primevue/toggleswitch'
import Message from 'primevue/message'
import { useCampEditions } from '@/composables/useCampEditions'
import type { CampEdition, UpdateCampEditionRequest } from '@/types/camp-edition'

interface Props {
  visible: boolean
  edition: CampEdition
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  'saved': [edition: CampEdition]
}>()

const { updateEdition, loading, error } = useCampEditions()

interface FormModel {
  startDate: Date | null
  endDate: Date | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  customBabyMaxAge: number | null
  customChildMinAge: number | null
  customChildMaxAge: number | null
  customAdultMinAge: number | null
  maxCapacity: number | null
  notes: string
}

const form = reactive<FormModel>({
  startDate: null,
  endDate: null,
  pricePerAdult: 0,
  pricePerChild: 0,
  pricePerBaby: 0,
  useCustomAgeRanges: false,
  customBabyMaxAge: null,
  customChildMinAge: null,
  customChildMaxAge: null,
  customAdultMinAge: null,
  maxCapacity: null,
  notes: ''
})

const errors = ref<Record<string, string>>({})

const isOpenEdition = computed(() => props.edition.status === 'Open')

const initializeForm = () => {
  form.startDate = props.edition.startDate ? new Date(props.edition.startDate) : null
  form.endDate = props.edition.endDate ? new Date(props.edition.endDate) : null
  form.pricePerAdult = props.edition.pricePerAdult
  form.pricePerChild = props.edition.pricePerChild
  form.pricePerBaby = props.edition.pricePerBaby
  form.useCustomAgeRanges = props.edition.useCustomAgeRanges
  // Map from existing CampEdition field names (without 'custom' prefix) to form fields
  form.customBabyMaxAge = props.edition.babyMaxAge ?? null
  form.customChildMinAge = props.edition.childMinAge ?? null
  form.customChildMaxAge = props.edition.childMaxAge ?? null
  form.customAdultMinAge = props.edition.adultMinAge ?? null
  form.maxCapacity = props.edition.maxCapacity > 0 ? props.edition.maxCapacity : null
  form.notes = ''
  errors.value = {}
}

watch(() => props.visible, (visible) => {
  if (visible) initializeForm()
})

const formatDateToIso = (date: Date | null): string => {
  if (!date) return ''
  return date.toISOString().split('T')[0]
}

const validate = (): boolean => {
  errors.value = {}
  if (!form.startDate) errors.value.startDate = 'La fecha de inicio es obligatoria'
  if (!form.endDate) errors.value.endDate = 'La fecha de fin es obligatoria'
  if (form.endDate && form.startDate && form.endDate <= form.startDate) {
    errors.value.endDate = 'La fecha de fin debe ser posterior a la fecha de inicio'
  }
  if (form.pricePerAdult < 0) errors.value.pricePerAdult = 'El precio por adulto debe ser mayor o igual a 0'
  if (form.pricePerChild < 0) errors.value.pricePerChild = 'El precio por niño debe ser mayor o igual a 0'
  if (form.pricePerBaby < 0) errors.value.pricePerBaby = 'El precio por bebé debe ser mayor o igual a 0'
  if (form.maxCapacity !== null && form.maxCapacity !== undefined && form.maxCapacity <= 0) {
    errors.value.maxCapacity = 'La capacidad máxima debe ser mayor a 0'
  }
  if (form.notes && form.notes.length > 2000) {
    errors.value.notes = 'Las notas no deben superar los 2000 caracteres'
  }
  if (form.useCustomAgeRanges) {
    if (!form.customBabyMaxAge) errors.value.customBabyMaxAge = 'La edad máxima de bebé es obligatoria'
    if (!form.customChildMinAge) errors.value.customChildMinAge = 'La edad mínima de niño es obligatoria'
    if (!form.customChildMaxAge) errors.value.customChildMaxAge = 'La edad máxima de niño es obligatoria'
    if (!form.customAdultMinAge) errors.value.customAdultMinAge = 'La edad mínima de adulto es obligatoria'
    if (form.customBabyMaxAge && form.customChildMinAge && form.customBabyMaxAge >= form.customChildMinAge) {
      errors.value.customBabyMaxAge = 'La edad máxima de bebé debe ser menor a la edad mínima de niño'
    }
    if (form.customChildMaxAge && form.customAdultMinAge && form.customChildMaxAge >= form.customAdultMinAge) {
      errors.value.customChildMaxAge = 'La edad máxima de niño debe ser menor a la edad mínima de adulto'
    }
  }
  return Object.keys(errors.value).length === 0
}

const handleSave = async () => {
  if (!validate()) return

  const request: UpdateCampEditionRequest = {
    startDate: formatDateToIso(form.startDate),
    endDate: formatDateToIso(form.endDate),
    pricePerAdult: form.pricePerAdult,
    pricePerChild: form.pricePerChild,
    pricePerBaby: form.pricePerBaby,
    useCustomAgeRanges: form.useCustomAgeRanges,
    ...(form.useCustomAgeRanges && {
      customBabyMaxAge: form.customBabyMaxAge ?? undefined,
      customChildMinAge: form.customChildMinAge ?? undefined,
      customChildMaxAge: form.customChildMaxAge ?? undefined,
      customAdultMinAge: form.customAdultMinAge ?? undefined
    }),
    maxCapacity: form.maxCapacity ?? undefined,
    notes: form.notes || undefined
  }

  const result = await updateEdition(props.edition.id, request)
  if (result) {
    emit('saved', result)
    emit('update:visible', false)
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    header="Editar Edición"
    modal
    class="w-full max-w-2xl"
    data-testid="edition-dialog"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4">
      <Message v-if="isOpenEdition" severity="info" :closable="false">
        Esta edición está abierta para inscripciones. Solo se pueden modificar las notas y la capacidad máxima.
      </Message>

      <Message v-if="error" severity="error" :closable="false">
        {{ error }}
      </Message>

      <!-- Dates -->
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-gray-700">Fecha de inicio</label>
          <DatePicker
            v-model="form.startDate"
            date-format="dd/mm/yy"
            :disabled="isOpenEdition"
            class="w-full"
          />
          <span v-if="errors.startDate" class="text-xs text-red-600">{{ errors.startDate }}</span>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-gray-700">Fecha de fin</label>
          <DatePicker
            v-model="form.endDate"
            date-format="dd/mm/yy"
            :disabled="isOpenEdition"
            class="w-full"
          />
          <span v-if="errors.endDate" class="text-xs text-red-600">{{ errors.endDate }}</span>
        </div>
      </div>

      <!-- Pricing -->
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-gray-700">Precio adulto</label>
          <InputNumber
            v-model="form.pricePerAdult"
            mode="currency"
            currency="EUR"
            :min="0"
            :disabled="isOpenEdition"
            class="w-full"
          />
          <span v-if="errors.pricePerAdult" class="text-xs text-red-600">{{ errors.pricePerAdult }}</span>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-gray-700">Precio niño</label>
          <InputNumber
            v-model="form.pricePerChild"
            mode="currency"
            currency="EUR"
            :min="0"
            :disabled="isOpenEdition"
            class="w-full"
          />
          <span v-if="errors.pricePerChild" class="text-xs text-red-600">{{ errors.pricePerChild }}</span>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-gray-700">Precio bebé</label>
          <InputNumber
            v-model="form.pricePerBaby"
            mode="currency"
            currency="EUR"
            :min="0"
            :disabled="isOpenEdition"
            class="w-full"
          />
          <span v-if="errors.pricePerBaby" class="text-xs text-red-600">{{ errors.pricePerBaby }}</span>
        </div>
      </div>

      <!-- Capacity -->
      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-gray-700">Capacidad máxima (opcional)</label>
        <InputNumber
          v-model="form.maxCapacity"
          :min="1"
          :use-grouping="false"
          placeholder="Sin límite"
          class="w-full"
        />
        <span v-if="errors.maxCapacity" class="text-xs text-red-600">{{ errors.maxCapacity }}</span>
      </div>

      <!-- Notes -->
      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium text-gray-700">Notas</label>
        <Textarea v-model="form.notes" :max-length="2000" rows="3" class="w-full" />
        <span v-if="errors.notes" class="text-xs text-red-600">{{ errors.notes }}</span>
      </div>

      <!-- Custom Age Ranges -->
      <div class="flex flex-col gap-3">
        <div class="flex items-center gap-3">
          <ToggleSwitch v-model="form.useCustomAgeRanges" :disabled="isOpenEdition" />
          <label class="text-sm font-medium text-gray-700">Usar rangos de edad personalizados</label>
        </div>

        <div v-if="form.useCustomAgeRanges" class="grid grid-cols-2 gap-4">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-medium text-gray-600">Edad máx. bebé</label>
            <InputNumber
              v-model="form.customBabyMaxAge"
              :min="0"
              :max="10"
              :use-grouping="false"
              :disabled="isOpenEdition"
            />
            <span v-if="errors.customBabyMaxAge" class="text-xs text-red-600">{{ errors.customBabyMaxAge }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-medium text-gray-600">Edad mín. niño</label>
            <InputNumber
              v-model="form.customChildMinAge"
              :min="0"
              :max="18"
              :use-grouping="false"
              :disabled="isOpenEdition"
            />
            <span v-if="errors.customChildMinAge" class="text-xs text-red-600">{{ errors.customChildMinAge }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-medium text-gray-600">Edad máx. niño</label>
            <InputNumber
              v-model="form.customChildMaxAge"
              :min="0"
              :max="18"
              :use-grouping="false"
              :disabled="isOpenEdition"
            />
            <span v-if="errors.customChildMaxAge" class="text-xs text-red-600">{{ errors.customChildMaxAge }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-medium text-gray-600">Edad mín. adulto</label>
            <InputNumber
              v-model="form.customAdultMinAge"
              :min="0"
              :max="99"
              :use-grouping="false"
              :disabled="isOpenEdition"
            />
            <span v-if="errors.customAdultMinAge" class="text-xs text-red-600">{{ errors.customAdultMinAge }}</span>
          </div>
        </div>
      </div>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button
          label="Cancelar"
          text
          :disabled="loading"
          @click="emit('update:visible', false)"
        />
        <Button
          label="Guardar"
          :loading="loading"
          :disabled="loading"
          data-testid="save-edition-btn"
          @click="handleSave"
        />
      </div>
    </template>
  </Dialog>
</template>

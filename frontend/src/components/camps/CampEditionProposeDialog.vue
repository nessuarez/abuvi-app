<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import DatePicker from 'primevue/datepicker'
import Textarea from 'primevue/textarea'
import Message from 'primevue/message'
import type { Camp } from '@/types/camp'
import type { CampEdition } from '@/types/camp-edition'
import { useCampEditions } from '@/composables/useCampEditions'

interface Props {
  visible: boolean
  campId: string
  camp: Camp | null
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  'saved': [edition: CampEdition]
}>()

const { proposeEdition, loading, error } = useCampEditions()

const form = ref({
  year: new Date().getFullYear(),
  startDate: null as Date | null,
  endDate: null as Date | null,
  location: '',
  pricePerAdult: 0,
  pricePerChild: 0,
  pricePerBaby: 0,
  maxCapacity: 0,
  proposalReason: '',
  proposalNotes: ''
})

const errors = ref<Record<string, string>>({})

watch(() => props.visible, (val) => {
  if (val) {
    errors.value = {}
    form.value = {
      year: new Date().getFullYear(),
      startDate: null,
      endDate: null,
      location: props.camp?.location ?? props.camp?.name ?? '',
      pricePerAdult: props.camp?.pricePerAdult ?? 0,
      pricePerChild: props.camp?.pricePerChild ?? 0,
      pricePerBaby: props.camp?.pricePerBaby ?? 0,
      maxCapacity: 0,
      proposalReason: '',
      proposalNotes: ''
    }
  }
})

const validate = (): boolean => {
  errors.value = {}
  if (!form.value.year || form.value.year < 2000) errors.value.year = 'El año es obligatorio'
  if (!form.value.startDate) errors.value.startDate = 'La fecha de inicio es obligatoria'
  if (!form.value.endDate) errors.value.endDate = 'La fecha de fin es obligatoria'
  if (form.value.startDate && form.value.endDate && form.value.endDate <= form.value.startDate)
    errors.value.endDate = 'La fecha de fin debe ser posterior a la fecha de inicio'
  if (!form.value.location.trim()) errors.value.location = 'La ubicación es obligatoria'
  if (form.value.pricePerAdult < 0) errors.value.pricePerAdult = 'El precio debe ser mayor o igual a 0'
  if (form.value.pricePerChild < 0) errors.value.pricePerChild = 'El precio debe ser mayor o igual a 0'
  if (form.value.pricePerBaby < 0) errors.value.pricePerBaby = 'El precio debe ser mayor o igual a 0'
  if (!form.value.proposalReason.trim()) errors.value.proposalReason = 'El motivo de la propuesta es obligatorio'
  return Object.keys(errors.value).length === 0
}

const toISODate = (date: Date): string => date.toISOString().split('T')[0]

const handleSubmit = async () => {
  if (!validate()) return
  const result = await proposeEdition({
    campId: props.campId,
    year: form.value.year,
    startDate: toISODate(form.value.startDate!),
    endDate: toISODate(form.value.endDate!),
    location: form.value.location,
    pricePerAdult: form.value.pricePerAdult,
    pricePerChild: form.value.pricePerChild,
    pricePerBaby: form.value.pricePerBaby,
    maxCapacity: form.value.maxCapacity,
    proposalReason: form.value.proposalReason,
    proposalNotes: form.value.proposalNotes
  })
  if (result) {
    emit('saved', result)
    emit('update:visible', false)
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    header="Nueva Propuesta de Edición"
    modal
    class="w-full max-w-2xl"
    data-testid="propose-edition-dialog"
    @update:visible="emit('update:visible', $event)"
  >
    <div class="space-y-4">
      <Message v-if="error" severity="error" :closable="false">{{ error }}</Message>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium">Año *</label>
        <InputNumber v-model="form.year" :use-grouping="false" :min="2024" :max="2100" class="w-32" />
        <span v-if="errors.year" class="text-xs text-red-600">{{ errors.year }}</span>
      </div>

      <div class="grid grid-cols-2 gap-4">
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">Fecha de inicio *</label>
          <DatePicker v-model="form.startDate" date-format="dd/mm/yy" show-icon />
          <span v-if="errors.startDate" class="text-xs text-red-600">{{ errors.startDate }}</span>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">Fecha de fin *</label>
          <DatePicker v-model="form.endDate" date-format="dd/mm/yy" show-icon />
          <span v-if="errors.endDate" class="text-xs text-red-600">{{ errors.endDate }}</span>
        </div>
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium">Ubicación *</label>
        <InputText v-model="form.location" placeholder="Ubicación del campamento" />
        <span v-if="errors.location" class="text-xs text-red-600">{{ errors.location }}</span>
      </div>

      <div class="grid grid-cols-3 gap-4">
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">Precio adulto *</label>
          <InputNumber v-model="form.pricePerAdult" mode="currency" currency="EUR" locale="es-ES" :min="0" />
          <span v-if="errors.pricePerAdult" class="text-xs text-red-600">{{ errors.pricePerAdult }}</span>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">Precio niño *</label>
          <InputNumber v-model="form.pricePerChild" mode="currency" currency="EUR" locale="es-ES" :min="0" />
          <span v-if="errors.pricePerChild" class="text-xs text-red-600">{{ errors.pricePerChild }}</span>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium">Precio bebé *</label>
          <InputNumber v-model="form.pricePerBaby" mode="currency" currency="EUR" locale="es-ES" :min="0" />
          <span v-if="errors.pricePerBaby" class="text-xs text-red-600">{{ errors.pricePerBaby }}</span>
        </div>
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium">
          Capacidad máxima
          <span class="font-normal text-gray-500">(0 = sin límite)</span>
        </label>
        <InputNumber v-model="form.maxCapacity" :min="0" class="w-40" />
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium">Motivo de la propuesta *</label>
        <Textarea
          v-model="form.proposalReason"
          rows="3"
          placeholder="Explica por qué se propone esta edición..."
        />
        <span v-if="errors.proposalReason" class="text-xs text-red-600">{{ errors.proposalReason }}</span>
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium">Notas adicionales</label>
        <Textarea
          v-model="form.proposalNotes"
          rows="2"
          placeholder="Información adicional (opcional)..."
        />
      </div>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cancelar" text :disabled="loading" @click="emit('update:visible', false)" />
        <Button label="Enviar propuesta" :loading="loading" data-testid="submit-propose-btn" @click="handleSubmit" />
      </div>
    </template>
  </Dialog>
</template>

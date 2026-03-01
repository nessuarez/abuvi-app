<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import DatePicker from 'primevue/datepicker'
import Textarea from 'primevue/textarea'
import ToggleSwitch from 'primevue/toggleswitch'
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

const { proposeEdition, loading, error, editions } = useCampEditions()

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
  allowPartialAttendance: false,
  halfDate: null as Date | null,
  pricePerAdultWeek: null as number | null,
  pricePerChildWeek: null as number | null,
  pricePerBabyWeek: null as number | null,
  allowWeekendVisit: false,
  weekendStartDate: null as Date | null,
  weekendEndDate: null as Date | null,
  pricePerAdultWeekend: null as number | null,
  pricePerChildWeekend: null as number | null,
  pricePerBabyWeekend: null as number | null,
  maxWeekendCapacity: null as number | null,
  description: ''
})

const errors = ref<Record<string, string>>({})

const prefillDatesFromPreviousYear = (targetYear: number) => {
  const previousEdition = editions.value
    .filter(e => e.year === targetYear - 1)
    .sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime())[0]

  if (previousEdition) {
    const prevStart = new Date(previousEdition.startDate)
    const prevEnd = new Date(previousEdition.endDate)
    form.value.startDate = new Date(targetYear, prevStart.getMonth(), prevStart.getDate())
    form.value.endDate = new Date(targetYear, prevEnd.getMonth(), prevEnd.getDate())
  } else {
    form.value.startDate = new Date(targetYear, 7, 15)
    form.value.endDate = new Date(targetYear, 7, 22)
  }
}

watch(() => props.visible, (val) => {
  if (val) {
    errors.value = {}
    const targetYear = new Date().getFullYear()
    form.value = {
      year: targetYear,
      startDate: null,
      endDate: null,
      location: props.camp?.rawAddress ?? props.camp?.name ?? '',
      pricePerAdult: props.camp?.pricePerAdult ?? 0,
      pricePerChild: props.camp?.pricePerChild ?? 0,
      pricePerBaby: props.camp?.pricePerBaby ?? 0,
      maxCapacity: 0,
      proposalReason: '',
      allowPartialAttendance: false,
      halfDate: null,
      pricePerAdultWeek: null,
      pricePerChildWeek: null,
      pricePerBabyWeek: null,
      allowWeekendVisit: false,
      weekendStartDate: null,
      weekendEndDate: null,
      pricePerAdultWeekend: null,
      pricePerChildWeekend: null,
      pricePerBabyWeekend: null,
      maxWeekendCapacity: null,
      description: ''
    }
    prefillDatesFromPreviousYear(targetYear)
  }
})

watch(() => form.value.year, (newYear) => {
  if (props.visible && newYear) {
    prefillDatesFromPreviousYear(newYear)
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
  if (form.value.allowPartialAttendance) {
    if (form.value.pricePerAdultWeek == null || form.value.pricePerAdultWeek < 0)
      errors.value.pricePerAdultWeek = 'El precio por adulto/semana es obligatorio'
    if (form.value.pricePerChildWeek == null || form.value.pricePerChildWeek < 0)
      errors.value.pricePerChildWeek = 'El precio por niño/semana es obligatorio'
    if (form.value.pricePerBabyWeek == null || form.value.pricePerBabyWeek < 0)
      errors.value.pricePerBabyWeek = 'El precio por bebé/semana es obligatorio'
  }
  if (form.value.allowWeekendVisit) {
    if (!form.value.weekendStartDate)
      errors.value.weekendStartDate = 'La fecha de inicio del fin de semana es obligatoria'
    if (!form.value.weekendEndDate)
      errors.value.weekendEndDate = 'La fecha de fin del fin de semana es obligatoria'
    if (form.value.weekendStartDate && form.value.weekendEndDate
        && form.value.weekendEndDate <= form.value.weekendStartDate)
      errors.value.weekendEndDate = 'La fecha de fin debe ser posterior a la de inicio'
    if (form.value.pricePerAdultWeekend == null || form.value.pricePerAdultWeekend < 0)
      errors.value.pricePerAdultWeekend = 'El precio por adulto/fds es obligatorio'
    if (form.value.pricePerChildWeekend == null || form.value.pricePerChildWeekend < 0)
      errors.value.pricePerChildWeekend = 'El precio por niño/fds es obligatorio'
    if (form.value.pricePerBabyWeekend == null || form.value.pricePerBabyWeekend < 0)
      errors.value.pricePerBabyWeekend = 'El precio por bebé/fds es obligatorio'
    if (form.value.maxWeekendCapacity != null && form.value.maxWeekendCapacity <= 0)
      errors.value.maxWeekendCapacity = 'La capacidad debe ser mayor a 0'
  }
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
    maxCapacity: form.value.maxCapacity || null,
    proposalReason: form.value.proposalReason || undefined,
    halfDate: form.value.allowPartialAttendance && form.value.halfDate
      ? toISODate(form.value.halfDate) : null,
    pricePerAdultWeek: form.value.allowPartialAttendance ? form.value.pricePerAdultWeek : null,
    pricePerChildWeek: form.value.allowPartialAttendance ? form.value.pricePerChildWeek : null,
    pricePerBabyWeek: form.value.allowPartialAttendance ? form.value.pricePerBabyWeek : null,
    weekendStartDate: form.value.allowWeekendVisit && form.value.weekendStartDate
      ? toISODate(form.value.weekendStartDate) : null,
    weekendEndDate: form.value.allowWeekendVisit && form.value.weekendEndDate
      ? toISODate(form.value.weekendEndDate) : null,
    pricePerAdultWeekend: form.value.allowWeekendVisit ? form.value.pricePerAdultWeekend : null,
    pricePerChildWeekend: form.value.allowWeekendVisit ? form.value.pricePerChildWeekend : null,
    pricePerBabyWeekend: form.value.allowWeekendVisit ? form.value.pricePerBabyWeekend : null,
    maxWeekendCapacity: form.value.allowWeekendVisit ? (form.value.maxWeekendCapacity || null) : null,
    description: form.value.description || undefined
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

      <!-- Partial attendance (week pricing) -->
      <div class="flex flex-col gap-3">
        <div class="flex items-center gap-3">
          <ToggleSwitch v-model="form.allowPartialAttendance" />
          <label class="text-sm font-medium text-gray-700">Permitir inscripción por semanas</label>
        </div>

        <div v-if="form.allowPartialAttendance" class="space-y-4 pl-1">
          <div class="flex flex-col gap-1">
            <label class="text-xs font-medium text-gray-600">Fecha de corte (opcional)</label>
            <DatePicker v-model="form.halfDate" date-format="dd/mm/yy" show-icon class="w-full" />
          </div>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-gray-600">Precio adulto/sem</label>
              <InputNumber
                v-model="form.pricePerAdultWeek"
                mode="currency"
                currency="EUR"
                locale="es-ES"
                :min="0"
                class="w-full"
              />
              <span v-if="errors.pricePerAdultWeek" class="text-xs text-red-600">{{ errors.pricePerAdultWeek }}</span>
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-gray-600">Precio niño/sem</label>
              <InputNumber
                v-model="form.pricePerChildWeek"
                mode="currency"
                currency="EUR"
                locale="es-ES"
                :min="0"
                class="w-full"
              />
              <span v-if="errors.pricePerChildWeek" class="text-xs text-red-600">{{ errors.pricePerChildWeek }}</span>
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-gray-600">Precio bebé/sem</label>
              <InputNumber
                v-model="form.pricePerBabyWeek"
                mode="currency"
                currency="EUR"
                locale="es-ES"
                :min="0"
                class="w-full"
              />
              <span v-if="errors.pricePerBabyWeek" class="text-xs text-red-600">{{ errors.pricePerBabyWeek }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Weekend visit -->
      <div class="flex flex-col gap-3">
        <div class="flex items-center gap-3">
          <ToggleSwitch v-model="form.allowWeekendVisit" />
          <label class="text-sm font-medium text-gray-700">Permitir visitas de fin de semana</label>
        </div>

        <div v-if="form.allowWeekendVisit" class="space-y-4 pl-1">
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-gray-600">Fecha inicio fds</label>
              <DatePicker v-model="form.weekendStartDate" date-format="dd/mm/yy" show-icon class="w-full" />
              <span v-if="errors.weekendStartDate" class="text-xs text-red-600">{{ errors.weekendStartDate }}</span>
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-gray-600">Fecha fin fds</label>
              <DatePicker v-model="form.weekendEndDate" date-format="dd/mm/yy" show-icon class="w-full" />
              <span v-if="errors.weekendEndDate" class="text-xs text-red-600">{{ errors.weekendEndDate }}</span>
            </div>
          </div>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-gray-600">Precio adulto/fds</label>
              <InputNumber
                v-model="form.pricePerAdultWeekend"
                mode="currency"
                currency="EUR"
                locale="es-ES"
                :min="0"
                class="w-full"
              />
              <span v-if="errors.pricePerAdultWeekend" class="text-xs text-red-600">{{ errors.pricePerAdultWeekend }}</span>
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-gray-600">Precio niño/fds</label>
              <InputNumber
                v-model="form.pricePerChildWeekend"
                mode="currency"
                currency="EUR"
                locale="es-ES"
                :min="0"
                class="w-full"
              />
              <span v-if="errors.pricePerChildWeekend" class="text-xs text-red-600">{{ errors.pricePerChildWeekend }}</span>
            </div>
            <div class="flex flex-col gap-1">
              <label class="text-xs font-medium text-gray-600">Precio bebé/fds</label>
              <InputNumber
                v-model="form.pricePerBabyWeekend"
                mode="currency"
                currency="EUR"
                locale="es-ES"
                :min="0"
                class="w-full"
              />
              <span v-if="errors.pricePerBabyWeekend" class="text-xs text-red-600">{{ errors.pricePerBabyWeekend }}</span>
            </div>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-xs font-medium text-gray-600">Capacidad máx. fds (opcional)</label>
            <InputNumber
              v-model="form.maxWeekendCapacity"
              :min="1"
              :use-grouping="false"
              placeholder="Sin límite"
              class="w-full"
            />
            <span v-if="errors.maxWeekendCapacity" class="text-xs text-red-600">{{ errors.maxWeekendCapacity }}</span>
          </div>
        </div>
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium">Motivo de la propuesta</label>
        <Textarea
          v-model="form.proposalReason"
          rows="3"
          placeholder="Explica por qué se propone esta edición (opcional)..."
        />
      </div>

      <div class="flex flex-col gap-1">
        <label class="text-sm font-medium">Descripción</label>
        <Textarea
          v-model="form.description"
          rows="4"
          placeholder="Descripción de la edición (actividades, novedades, información pública...)"
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

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import Container from '@/components/ui/Container.vue'
import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
import CampEditionAccommodationsPanel from '@/components/camps/CampEditionAccommodationsPanel.vue'
import CampEditionExtrasList from '@/components/camps/CampEditionExtrasList.vue'
import DateInput from '@/components/shared/DateInput.vue'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Toast from 'primevue/toast'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import ToggleSwitch from 'primevue/toggleswitch'
import { useCampEditions } from '@/composables/useCampEditions'
import { useAuthStore } from '@/stores/auth'
import { parseDateLocal, formatDateLocal } from '@/utils/date'
import type { CampEdition, UpdateCampEditionRequest } from '@/types/camp-edition'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const toast = useToast()
const { loading, error, getEditionById, updateEdition } = useCampEditions()

const edition = ref<CampEdition | null>(null)
const isBoard = computed(() => auth.user?.role === 'Admin' || auth.user?.role === 'Board')

const activeTab = ref('0')

const sections = [
  { value: '0', label: 'General', icon: 'pi pi-info-circle' },
  { value: '1', label: 'Precios', icon: 'pi pi-euro' },
  { value: '2', label: 'Semanas', icon: 'pi pi-calendar-minus' },
  { value: '3', label: 'Fin de semana', icon: 'pi pi-sun' },
  { value: '4', label: 'Edades', icon: 'pi pi-users' },
  { value: '5', label: 'Pagos', icon: 'pi pi-credit-card' },
  { value: '6', label: 'Notas', icon: 'pi pi-file-edit' },
  { value: '7', label: 'Alojamientos', icon: 'pi pi-home' },
  { value: '8', label: 'Extras', icon: 'pi pi-plus-circle' },
] as const

// Edit mode state
const isEditing = ref(false)
const saving = ref(false)
const errors = ref<Record<string, string>>({})

const isOpenEdition = computed(() => edition.value?.status === 'Open')
const canEdit = computed(() =>
  isBoard.value &&
  edition.value != null &&
  edition.value.status !== 'Closed' &&
  edition.value.status !== 'Completed'
)

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
  allowPartialAttendance: boolean
  halfDate: Date | null
  pricePerAdultWeek: number | null
  pricePerChildWeek: number | null
  pricePerBabyWeek: number | null
  allowWeekendVisit: boolean
  weekendStartDate: Date | null
  weekendEndDate: Date | null
  pricePerAdultWeekend: number | null
  pricePerChildWeekend: number | null
  pricePerBabyWeekend: number | null
  maxWeekendCapacity: number | null
  description: string
  firstPaymentDeadline: Date | null
  secondPaymentDeadline: Date | null
}

const form = reactive<FormModel>({
  startDate: null, endDate: null,
  pricePerAdult: 0, pricePerChild: 0, pricePerBaby: 0,
  useCustomAgeRanges: false,
  customBabyMaxAge: null, customChildMinAge: null, customChildMaxAge: null, customAdultMinAge: null,
  maxCapacity: null, notes: '', description: '',
  allowPartialAttendance: false,
  halfDate: null, pricePerAdultWeek: null, pricePerChildWeek: null, pricePerBabyWeek: null,
  allowWeekendVisit: false,
  weekendStartDate: null, weekendEndDate: null,
  pricePerAdultWeekend: null, pricePerChildWeekend: null, pricePerBabyWeekend: null,
  maxWeekendCapacity: null,
  firstPaymentDeadline: null, secondPaymentDeadline: null
})

const formatDate = (dateStr: string | null | undefined): string => {
  if (!dateStr) return '—'
  return new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(
    parseDateLocal(dateStr)
  )
}

const formatCurrency = (amount: number | null | undefined): string => {
  if (amount == null) return '—'
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR', minimumFractionDigits: 0 }).format(amount)
}

const startEditing = () => {
  if (!edition.value) return
  const ed = edition.value

  form.startDate = ed.startDate ? parseDateLocal(ed.startDate) : null
  form.endDate = ed.endDate ? parseDateLocal(ed.endDate) : null
  form.pricePerAdult = ed.pricePerAdult
  form.pricePerChild = ed.pricePerChild
  form.pricePerBaby = ed.pricePerBaby
  form.useCustomAgeRanges = ed.useCustomAgeRanges
  form.customBabyMaxAge = ed.customBabyMaxAge ?? null
  form.customChildMinAge = ed.customChildMinAge ?? null
  form.customChildMaxAge = ed.customChildMaxAge ?? null
  form.customAdultMinAge = ed.customAdultMinAge ?? null
  form.maxCapacity = ed.maxCapacity > 0 ? ed.maxCapacity : null
  form.notes = ed.notes ?? ''
  form.description = ed.description ?? ''
  form.allowPartialAttendance = ed.pricePerAdultWeek != null
  form.halfDate = ed.halfDate ? parseDateLocal(ed.halfDate) : null
  form.pricePerAdultWeek = ed.pricePerAdultWeek ?? null
  form.pricePerChildWeek = ed.pricePerChildWeek ?? null
  form.pricePerBabyWeek = ed.pricePerBabyWeek ?? null
  form.allowWeekendVisit = ed.weekendStartDate != null
  form.weekendStartDate = ed.weekendStartDate ? parseDateLocal(ed.weekendStartDate) : null
  form.weekendEndDate = ed.weekendEndDate ? parseDateLocal(ed.weekendEndDate) : null
  form.pricePerAdultWeekend = ed.pricePerAdultWeekend ?? null
  form.pricePerChildWeekend = ed.pricePerChildWeekend ?? null
  form.pricePerBabyWeekend = ed.pricePerBabyWeekend ?? null
  form.maxWeekendCapacity = ed.maxWeekendCapacity ?? null
  form.firstPaymentDeadline = ed.firstPaymentDeadline ? parseDateLocal(ed.firstPaymentDeadline) : null
  form.secondPaymentDeadline = ed.secondPaymentDeadline ? parseDateLocal(ed.secondPaymentDeadline) : null
  errors.value = {}
  isEditing.value = true
}

const cancelEditing = () => {
  isEditing.value = false
  errors.value = {}
}

const validate = (): boolean => {
  errors.value = {}

  if (!isOpenEdition.value) {
    if (!form.startDate) errors.value.startDate = 'La fecha de inicio es obligatoria'
    if (!form.endDate) errors.value.endDate = 'La fecha de fin es obligatoria'
    if (form.endDate && form.startDate && form.endDate <= form.startDate)
      errors.value.endDate = 'La fecha de fin debe ser posterior a la fecha de inicio'
    if (form.pricePerAdult < 0) errors.value.pricePerAdult = 'El precio debe ser >= 0'
    if (form.pricePerChild < 0) errors.value.pricePerChild = 'El precio debe ser >= 0'
    if (form.pricePerBaby < 0) errors.value.pricePerBaby = 'El precio debe ser >= 0'

    if (form.allowPartialAttendance) {
      if (form.pricePerAdultWeek == null || form.pricePerAdultWeek < 0)
        errors.value.pricePerAdultWeek = 'El precio por adulto/semana es obligatorio'
      if (form.pricePerChildWeek == null || form.pricePerChildWeek < 0)
        errors.value.pricePerChildWeek = 'El precio infantil/semana es obligatorio'
      if (form.pricePerBabyWeek == null || form.pricePerBabyWeek < 0)
        errors.value.pricePerBabyWeek = 'El precio por bebé/semana es obligatorio'
    }

    if (form.allowWeekendVisit) {
      if (!form.weekendStartDate) errors.value.weekendStartDate = 'Obligatorio'
      if (!form.weekendEndDate) errors.value.weekendEndDate = 'Obligatorio'
      if (form.weekendStartDate && form.weekendEndDate && form.weekendEndDate <= form.weekendStartDate)
        errors.value.weekendEndDate = 'Debe ser posterior a la de inicio'
      if (form.pricePerAdultWeekend == null || form.pricePerAdultWeekend < 0)
        errors.value.pricePerAdultWeekend = 'Obligatorio'
      if (form.pricePerChildWeekend == null || form.pricePerChildWeekend < 0)
        errors.value.pricePerChildWeekend = 'Obligatorio'
      if (form.pricePerBabyWeekend == null || form.pricePerBabyWeekend < 0)
        errors.value.pricePerBabyWeekend = 'Obligatorio'
      if (form.maxWeekendCapacity != null && form.maxWeekendCapacity <= 0)
        errors.value.maxWeekendCapacity = 'Debe ser mayor a 0'
    }

    if (form.useCustomAgeRanges) {
      if (!form.customBabyMaxAge) errors.value.customBabyMaxAge = 'Obligatorio'
      if (!form.customChildMinAge) errors.value.customChildMinAge = 'Obligatorio'
      if (!form.customChildMaxAge) errors.value.customChildMaxAge = 'Obligatorio'
      if (!form.customAdultMinAge) errors.value.customAdultMinAge = 'Obligatorio'
      if (form.customBabyMaxAge && form.customChildMinAge && form.customBabyMaxAge >= form.customChildMinAge)
        errors.value.customBabyMaxAge = 'Debe ser menor a la edad mínima infantil'
      if (form.customChildMaxAge && form.customAdultMinAge && form.customChildMaxAge >= form.customAdultMinAge)
        errors.value.customChildMaxAge = 'Debe ser menor a la edad mínima de adulto'
    }
  }

  if (form.maxCapacity !== null && form.maxCapacity !== undefined && form.maxCapacity <= 0)
    errors.value.maxCapacity = 'La capacidad máxima debe ser mayor a 0'
  if (form.notes && form.notes.length > 2000)
    errors.value.notes = 'Las notas no deben superar los 2000 caracteres'

  return Object.keys(errors.value).length === 0
}

const formatDateToIso = (date: Date | null): string => {
  if (!date) return ''
  return formatDateLocal(date)
}

const handleSave = async () => {
  if (!validate() || !edition.value) return

  saving.value = true
  const ed = edition.value
  const open = isOpenEdition.value

  const request: UpdateCampEditionRequest = {
    startDate: open ? ed.startDate : formatDateToIso(form.startDate),
    endDate: open ? ed.endDate : formatDateToIso(form.endDate),
    pricePerAdult: open ? ed.pricePerAdult : form.pricePerAdult,
    pricePerChild: open ? ed.pricePerChild : form.pricePerChild,
    pricePerBaby: open ? ed.pricePerBaby : form.pricePerBaby,
    useCustomAgeRanges: open ? ed.useCustomAgeRanges : form.useCustomAgeRanges,
    ...((open ? ed.useCustomAgeRanges : form.useCustomAgeRanges) && {
      customBabyMaxAge: open ? (ed.customBabyMaxAge ?? undefined) : (form.customBabyMaxAge ?? undefined),
      customChildMinAge: open ? (ed.customChildMinAge ?? undefined) : (form.customChildMinAge ?? undefined),
      customChildMaxAge: open ? (ed.customChildMaxAge ?? undefined) : (form.customChildMaxAge ?? undefined),
      customAdultMinAge: open ? (ed.customAdultMinAge ?? undefined) : (form.customAdultMinAge ?? undefined),
    }),
    maxCapacity: form.maxCapacity ?? undefined,
    notes: form.notes || undefined,
    description: form.description || undefined,
    halfDate: open ? (ed.halfDate ?? null)
      : (form.allowPartialAttendance && form.halfDate ? formatDateToIso(form.halfDate) : null),
    pricePerAdultWeek: open ? (ed.pricePerAdultWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerAdultWeek : null),
    pricePerChildWeek: open ? (ed.pricePerChildWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerChildWeek : null),
    pricePerBabyWeek: open ? (ed.pricePerBabyWeek ?? null)
      : (form.allowPartialAttendance ? form.pricePerBabyWeek : null),
    weekendStartDate: open ? (ed.weekendStartDate ?? null)
      : (form.allowWeekendVisit && form.weekendStartDate ? formatDateToIso(form.weekendStartDate) : null),
    weekendEndDate: open ? (ed.weekendEndDate ?? null)
      : (form.allowWeekendVisit && form.weekendEndDate ? formatDateToIso(form.weekendEndDate) : null),
    pricePerAdultWeekend: open ? (ed.pricePerAdultWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerAdultWeekend : null),
    pricePerChildWeekend: open ? (ed.pricePerChildWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerChildWeekend : null),
    pricePerBabyWeekend: open ? (ed.pricePerBabyWeekend ?? null)
      : (form.allowWeekendVisit ? form.pricePerBabyWeekend : null),
    maxWeekendCapacity: open ? (ed.maxWeekendCapacity ?? null)
      : (form.allowWeekendVisit ? (form.maxWeekendCapacity || null) : null),
    firstPaymentDeadline: form.firstPaymentDeadline ? formatDateToIso(form.firstPaymentDeadline) : null,
    secondPaymentDeadline: form.secondPaymentDeadline ? formatDateToIso(form.secondPaymentDeadline) : null
  }

  const result = await updateEdition(ed.id, request)
  saving.value = false

  if (result) {
    edition.value = result
    isEditing.value = false
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Edición actualizada correctamente', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value || 'Error al actualizar', life: 5000 })
  }
}

onMounted(async () => {
  edition.value = await getEditionById(route.params.id as string)
  if (edition.value && route.query.edit === 'true' && canEdit.value) {
    startEditing()
    router.replace({ query: {} })
  }
})
</script>

<template>
  <Container>
    <Toast />
    <div class="py-8">
      <Button label="Volver" icon="pi pi-arrow-left" text class="mb-4" @click="router.back()" />

      <div v-if="loading" class="flex justify-center py-12">
        <ProgressSpinner />
      </div>

      <Message v-else-if="error && !edition" severity="error" :closable="false">
        {{ error }}
      </Message>

      <div v-else-if="!edition" class="rounded-lg border border-gray-200 bg-gray-50 p-8 text-center">
        <p class="text-gray-500">Edición no encontrada.</p>
        <Button label="Volver" text class="mt-2" @click="router.back()" />
      </div>

      <div v-else>
        <!-- Header -->
        <div class="mb-6 flex items-center justify-between">
          <div>
            <h1 class="text-3xl font-bold text-gray-900">
              Edición {{ edition.year }}
              <span v-if="edition.name"> — {{ edition.name }}</span>
            </h1>
            <p class="mt-1 text-gray-500">{{ edition.location }}</p>
          </div>
          <div class="flex gap-2">
            <Button
              v-if="canEdit && !isEditing"
              label="Editar"
              icon="pi pi-pencil"
              outlined
              data-testid="edit-edition-btn"
              @click="startEditing"
            />
            <template v-if="isEditing">
              <Button label="Cancelar" text :disabled="saving" @click="cancelEditing" />
              <Button label="Guardar" :loading="saving" :disabled="saving" data-testid="save-edition-btn" @click="handleSave" />
            </template>
          </div>
        </div>

        <!-- Info message for Open editions -->
        <Message v-if="isEditing && isOpenEdition" severity="info" :closable="false" class="mb-4">
          Esta edición está abierta para inscripciones. Solo se pueden modificar la capacidad,
          las notas, la descripción y las fechas de pago.
        </Message>

        <!-- API error in edit mode -->
        <Message v-if="isEditing && error" severity="error" :closable="false" class="mb-4">
          {{ error }}
        </Message>

        <!-- Sidebar + Content layout -->
        <div class="flex flex-col gap-6 lg:flex-row">
          <!-- Sidebar navigation -->
          <nav class="flex gap-1 overflow-x-auto lg:w-48 lg:shrink-0 lg:flex-col lg:overflow-visible">
            <button
              v-for="s in sections"
              :key="s.value"
              class="flex items-center gap-2 whitespace-nowrap rounded-md px-3 py-2 text-left text-sm transition-colors"
              :class="activeTab === s.value
                ? 'bg-primary-50 font-semibold text-primary-700'
                : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'"
              @click="activeTab = s.value"
            >
              <i :class="s.icon" />
              <span>{{ s.label }}</span>
            </button>
          </nav>

          <!-- Content panels -->
          <div class="min-w-0 flex-1">
            <!-- Tab 0: General Information -->
            <div v-if="activeTab === '0'">
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-4 text-lg font-semibold text-gray-900">Información General</h2>

                  <!-- Read mode -->
                  <div v-if="!isEditing" class="space-y-2 text-sm">
                    <div class="flex justify-between">
                      <span class="text-gray-600">Estado:</span>
                      <CampEditionStatusBadge :status="edition.status" size="sm" />
                    </div>
                    <div class="flex justify-between">
                      <span class="text-gray-600">Año:</span>
                      <span>{{ edition.year }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-gray-600">Fecha inicio:</span>
                      <span>{{ formatDate(edition.startDate) }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-gray-600">Fecha fin:</span>
                      <span>{{ formatDate(edition.endDate) }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-gray-600">Capacidad máxima:</span>
                      <span>{{ edition.maxCapacity ? edition.maxCapacity : 'Sin límite' }}</span>
                    </div>
                  </div>

                  <!-- Edit mode -->
                  <div v-else class="space-y-4">
                    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                      <div class="flex flex-col gap-1">
                        <label class="text-sm font-medium text-gray-700">Fecha de inicio</label>
                        <DateInput v-model="form.startDate" :disabled="isOpenEdition" />
                        <span v-if="errors.startDate" class="text-xs text-red-600">{{ errors.startDate }}</span>
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-sm font-medium text-gray-700">Fecha de fin</label>
                        <DateInput v-model="form.endDate" :disabled="isOpenEdition" />
                        <span v-if="errors.endDate" class="text-xs text-red-600">{{ errors.endDate }}</span>
                      </div>
                    </div>
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Capacidad máxima</label>
                      <InputNumber v-model="form.maxCapacity" :min="1" :use-grouping="false" placeholder="Sin límite" class="w-full" />
                      <span v-if="errors.maxCapacity" class="text-xs text-red-600">{{ errors.maxCapacity }}</span>
                    </div>
                  </div>
                </div>
            </div>

            <!-- Tab 1: Pricing -->
            <div v-if="activeTab === '1'">
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-4 text-lg font-semibold text-gray-900">Precios</h2>

                  <div v-if="!isEditing" class="space-y-2 text-sm">
                    <div class="flex justify-between">
                      <span class="text-gray-600">Precio adulto:</span>
                      <span class="font-semibold">{{ formatCurrency(edition.pricePerAdult) }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-gray-600">Precio infantil:</span>
                      <span class="font-semibold">{{ formatCurrency(edition.pricePerChild) }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-gray-600">Precio bebé:</span>
                      <span class="font-semibold">{{ formatCurrency(edition.pricePerBaby) }}</span>
                    </div>
                  </div>

                  <div v-else class="grid grid-cols-1 gap-4 sm:grid-cols-3">
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Precio adulto</label>
                      <InputNumber v-model="form.pricePerAdult" mode="currency" currency="EUR" :min="0" :disabled="isOpenEdition" class="w-full" />
                      <span v-if="errors.pricePerAdult" class="text-xs text-red-600">{{ errors.pricePerAdult }}</span>
                    </div>
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Precio infantil</label>
                      <InputNumber v-model="form.pricePerChild" mode="currency" currency="EUR" :min="0" :disabled="isOpenEdition" class="w-full" />
                      <span v-if="errors.pricePerChild" class="text-xs text-red-600">{{ errors.pricePerChild }}</span>
                    </div>
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Precio bebé</label>
                      <InputNumber v-model="form.pricePerBaby" mode="currency" currency="EUR" :min="0" :disabled="isOpenEdition" class="w-full" />
                      <span v-if="errors.pricePerBaby" class="text-xs text-red-600">{{ errors.pricePerBaby }}</span>
                    </div>
                  </div>
                </div>
            </div>

            <!-- Tab 2: Partial Attendance (Weeks) -->
            <div v-if="activeTab === '2'">
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-4 text-lg font-semibold text-gray-900">Inscripción por semanas</h2>

                  <div v-if="!isEditing">
                    <div v-if="edition.pricePerAdultWeek != null" class="space-y-2 text-sm">
                      <div v-if="edition.halfDate" class="flex justify-between">
                        <span class="text-gray-600">Fecha de corte:</span>
                        <span>{{ formatDate(edition.halfDate) }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Precio adulto/semana:</span>
                        <span class="font-semibold">{{ formatCurrency(edition.pricePerAdultWeek) }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Precio infantil/semana:</span>
                        <span class="font-semibold">{{ formatCurrency(edition.pricePerChildWeek) }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Precio bebé/semana:</span>
                        <span class="font-semibold">{{ formatCurrency(edition.pricePerBabyWeek) }}</span>
                      </div>
                    </div>
                    <p v-else class="text-sm text-gray-500">No configurado</p>
                  </div>

                  <div v-else class="space-y-4">
                    <div class="flex items-center gap-3">
                      <ToggleSwitch v-model="form.allowPartialAttendance" :disabled="isOpenEdition" />
                      <label class="text-sm font-medium text-gray-700">Permitir inscripción por semanas</label>
                    </div>
                    <div v-if="form.allowPartialAttendance" class="space-y-4 pl-1">
                      <div class="flex flex-col gap-1">
                        <label class="text-xs font-medium text-gray-600">Fecha de corte (opcional)</label>
                        <DateInput v-model="form.halfDate" :disabled="isOpenEdition" />
                      </div>
                      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
                        <div class="flex flex-col gap-1">
                          <label class="text-xs font-medium text-gray-600">Precio adulto/sem</label>
                          <InputNumber v-model="form.pricePerAdultWeek" mode="currency" currency="EUR" locale="es-ES" :min="0" :disabled="isOpenEdition" class="w-full" />
                          <span v-if="errors.pricePerAdultWeek" class="text-xs text-red-600">{{ errors.pricePerAdultWeek }}</span>
                        </div>
                        <div class="flex flex-col gap-1">
                          <label class="text-xs font-medium text-gray-600">Precio infantil/sem</label>
                          <InputNumber v-model="form.pricePerChildWeek" mode="currency" currency="EUR" locale="es-ES" :min="0" :disabled="isOpenEdition" class="w-full" />
                          <span v-if="errors.pricePerChildWeek" class="text-xs text-red-600">{{ errors.pricePerChildWeek }}</span>
                        </div>
                        <div class="flex flex-col gap-1">
                          <label class="text-xs font-medium text-gray-600">Precio bebé/sem</label>
                          <InputNumber v-model="form.pricePerBabyWeek" mode="currency" currency="EUR" locale="es-ES" :min="0" :disabled="isOpenEdition" class="w-full" />
                          <span v-if="errors.pricePerBabyWeek" class="text-xs text-red-600">{{ errors.pricePerBabyWeek }}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
            </div>

            <!-- Tab 3: Weekend Visits -->
            <div v-if="activeTab === '3'">
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-4 text-lg font-semibold text-gray-900">Visitas de fin de semana</h2>

                  <div v-if="!isEditing">
                    <div v-if="edition.weekendStartDate" class="space-y-2 text-sm">
                      <div class="flex justify-between">
                        <span class="text-gray-600">Fecha inicio fds:</span>
                        <span>{{ formatDate(edition.weekendStartDate) }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Fecha fin fds:</span>
                        <span>{{ formatDate(edition.weekendEndDate) }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Precio adulto/fds:</span>
                        <span class="font-semibold">{{ formatCurrency(edition.pricePerAdultWeekend) }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Precio infantil/fds:</span>
                        <span class="font-semibold">{{ formatCurrency(edition.pricePerChildWeekend) }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Precio bebé/fds:</span>
                        <span class="font-semibold">{{ formatCurrency(edition.pricePerBabyWeekend) }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Capacidad máx. fds:</span>
                        <span>{{ edition.maxWeekendCapacity ?? 'Sin límite' }}</span>
                      </div>
                    </div>
                    <p v-else class="text-sm text-gray-500">No configurado</p>
                  </div>

                  <div v-else class="space-y-4">
                    <div class="flex items-center gap-3">
                      <ToggleSwitch v-model="form.allowWeekendVisit" :disabled="isOpenEdition" />
                      <label class="text-sm font-medium text-gray-700">Permitir visitas de fin de semana</label>
                    </div>
                    <div v-if="form.allowWeekendVisit" class="space-y-4 pl-1">
                      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                        <div class="flex flex-col gap-1">
                          <label class="text-xs font-medium text-gray-600">Fecha inicio fds</label>
                          <DateInput v-model="form.weekendStartDate" :disabled="isOpenEdition" />
                          <span v-if="errors.weekendStartDate" class="text-xs text-red-600">{{ errors.weekendStartDate }}</span>
                        </div>
                        <div class="flex flex-col gap-1">
                          <label class="text-xs font-medium text-gray-600">Fecha fin fds</label>
                          <DateInput v-model="form.weekendEndDate" :disabled="isOpenEdition" />
                          <span v-if="errors.weekendEndDate" class="text-xs text-red-600">{{ errors.weekendEndDate }}</span>
                        </div>
                      </div>
                      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
                        <div class="flex flex-col gap-1">
                          <label class="text-xs font-medium text-gray-600">Precio adulto/fds</label>
                          <InputNumber v-model="form.pricePerAdultWeekend" mode="currency" currency="EUR" locale="es-ES" :min="0" :disabled="isOpenEdition" class="w-full" />
                          <span v-if="errors.pricePerAdultWeekend" class="text-xs text-red-600">{{ errors.pricePerAdultWeekend }}</span>
                        </div>
                        <div class="flex flex-col gap-1">
                          <label class="text-xs font-medium text-gray-600">Precio infantil/fds</label>
                          <InputNumber v-model="form.pricePerChildWeekend" mode="currency" currency="EUR" locale="es-ES" :min="0" :disabled="isOpenEdition" class="w-full" />
                          <span v-if="errors.pricePerChildWeekend" class="text-xs text-red-600">{{ errors.pricePerChildWeekend }}</span>
                        </div>
                        <div class="flex flex-col gap-1">
                          <label class="text-xs font-medium text-gray-600">Precio bebé/fds</label>
                          <InputNumber v-model="form.pricePerBabyWeekend" mode="currency" currency="EUR" locale="es-ES" :min="0" :disabled="isOpenEdition" class="w-full" />
                          <span v-if="errors.pricePerBabyWeekend" class="text-xs text-red-600">{{ errors.pricePerBabyWeekend }}</span>
                        </div>
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-xs font-medium text-gray-600">Capacidad máx. fds (opcional)</label>
                        <InputNumber v-model="form.maxWeekendCapacity" :min="1" :use-grouping="false" placeholder="Sin límite" :disabled="isOpenEdition" class="w-full" />
                        <span v-if="errors.maxWeekendCapacity" class="text-xs text-red-600">{{ errors.maxWeekendCapacity }}</span>
                      </div>
                    </div>
                  </div>
                </div>
            </div>

            <!-- Tab 4: Custom Age Ranges -->
            <div v-if="activeTab === '4'">
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-4 text-lg font-semibold text-gray-900">Rangos de edad</h2>

                  <div v-if="!isEditing">
                    <div v-if="edition.useCustomAgeRanges" class="space-y-2 text-sm">
                      <div class="flex justify-between">
                        <span class="text-gray-600">Edad máx. bebé:</span>
                        <span>{{ edition.customBabyMaxAge }} años</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Edad mín. infantil:</span>
                        <span>{{ edition.customChildMinAge }} años</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Edad máx. infantil:</span>
                        <span>{{ edition.customChildMaxAge }} años</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-600">Edad mín. adulto:</span>
                        <span>{{ edition.customAdultMinAge }} años</span>
                      </div>
                    </div>
                    <p v-else class="text-sm text-gray-500">Rangos de edad por defecto</p>
                  </div>

                  <div v-else class="space-y-4">
                    <div class="flex items-center gap-3">
                      <ToggleSwitch v-model="form.useCustomAgeRanges" :disabled="isOpenEdition" />
                      <label class="text-sm font-medium text-gray-700">Usar rangos de edad personalizados</label>
                    </div>
                    <div v-if="form.useCustomAgeRanges" class="grid grid-cols-2 gap-4">
                      <div class="flex flex-col gap-1">
                        <label class="text-xs font-medium text-gray-600">Edad máx. bebé</label>
                        <InputNumber v-model="form.customBabyMaxAge" :min="0" :max="10" :use-grouping="false" :disabled="isOpenEdition" />
                        <span v-if="errors.customBabyMaxAge" class="text-xs text-red-600">{{ errors.customBabyMaxAge }}</span>
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-xs font-medium text-gray-600">Edad mín. infantil</label>
                        <InputNumber v-model="form.customChildMinAge" :min="0" :max="18" :use-grouping="false" :disabled="isOpenEdition" />
                        <span v-if="errors.customChildMinAge" class="text-xs text-red-600">{{ errors.customChildMinAge }}</span>
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-xs font-medium text-gray-600">Edad máx. infantil</label>
                        <InputNumber v-model="form.customChildMaxAge" :min="0" :max="18" :use-grouping="false" :disabled="isOpenEdition" />
                        <span v-if="errors.customChildMaxAge" class="text-xs text-red-600">{{ errors.customChildMaxAge }}</span>
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-xs font-medium text-gray-600">Edad mín. adulto</label>
                        <InputNumber v-model="form.customAdultMinAge" :min="0" :max="99" :use-grouping="false" :disabled="isOpenEdition" />
                        <span v-if="errors.customAdultMinAge" class="text-xs text-red-600">{{ errors.customAdultMinAge }}</span>
                      </div>
                    </div>
                  </div>
                </div>
            </div>

            <!-- Tab 5: Payment Deadlines -->
            <div v-if="activeTab === '5'">
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-4 text-lg font-semibold text-gray-900">Fechas de pago</h2>

                  <div v-if="!isEditing" class="space-y-2 text-sm">
                    <div class="flex justify-between">
                      <span class="text-gray-600">Fecha límite 1er pago:</span>
                      <span>{{ edition.firstPaymentDeadline ? formatDate(edition.firstPaymentDeadline) : 'Automática' }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-gray-600">Fecha límite 2º pago:</span>
                      <span>{{ edition.secondPaymentDeadline ? formatDate(edition.secondPaymentDeadline) : 'Automática' }}</span>
                    </div>
                  </div>

                  <div v-else class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Fecha límite 1er pago</label>
                      <DateInput v-model="form.firstPaymentDeadline" />
                      <p class="text-xs text-gray-500">Si se deja vacío, se calcula 117 días antes del inicio.</p>
                    </div>
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Fecha límite 2º pago</label>
                      <DateInput v-model="form.secondPaymentDeadline" />
                      <p class="text-xs text-gray-500">Si se deja vacío, se calcula 75 días antes del inicio.</p>
                    </div>
                  </div>
                </div>
            </div>

            <!-- Tab 6: Notes & Description -->
            <div v-if="activeTab === '6'">
                <div class="rounded-lg border border-gray-200 bg-white p-6">
                  <h2 class="mb-4 text-lg font-semibold text-gray-900">Notas y descripción</h2>

                  <div v-if="!isEditing" class="space-y-4">
                    <div>
                      <h3 class="mb-1 text-sm font-medium text-gray-600">Notas</h3>
                      <p v-if="edition.notes" class="whitespace-pre-line text-sm text-gray-700">{{ edition.notes }}</p>
                      <p v-else class="text-sm text-gray-500">Sin notas</p>
                    </div>
                    <div>
                      <h3 class="mb-1 text-sm font-medium text-gray-600">Descripción</h3>
                      <p v-if="edition.description" class="whitespace-pre-line text-sm text-gray-700">{{ edition.description }}</p>
                      <p v-else class="text-sm text-gray-500">Sin descripción</p>
                    </div>
                  </div>

                  <div v-else class="space-y-4">
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Notas</label>
                      <Textarea v-model="form.notes" :max-length="2000" rows="3" class="w-full" />
                      <span v-if="errors.notes" class="text-xs text-red-600">{{ errors.notes }}</span>
                    </div>
                    <div class="flex flex-col gap-1">
                      <label class="text-sm font-medium text-gray-700">Descripción</label>
                      <Textarea v-model="form.description" rows="5" class="w-full" placeholder="Descripción de la edición (actividades, novedades, información pública...)" />
                    </div>
                  </div>
                </div>
            </div>

            <!-- Tab 7: Accommodations (no inline edit — has its own CRUD) -->
            <div v-if="activeTab === '7'">
                <div v-if="isBoard">
                  <CampEditionAccommodationsPanel :edition-id="edition.id" />
                </div>
                <div v-else class="rounded-lg border border-gray-200 bg-white p-6">
                  <p class="text-sm text-gray-500">Solo visible para la Junta Directiva y administradores.</p>
                </div>
            </div>

            <!-- Tab 8: Extras (no inline edit — has its own CRUD) -->
            <div v-if="activeTab === '8'">
                <div class="rounded-lg border border-gray-200 bg-white p-6" data-testid="edition-extras-section">
                  <CampEditionExtrasList
                    :edition-id="edition.id"
                    :edition-status="edition.status"
                  />
                </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </Container>
</template>

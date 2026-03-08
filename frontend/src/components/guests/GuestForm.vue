<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import DateInput from '@/components/shared/DateInput.vue'
import Button from 'primevue/button'
import type { GuestResponse, CreateGuestRequest } from '@/types/guest'
import { formatDateLocal, parseDateLocal } from '@/utils/date'

const props = defineProps<{
  guest?: GuestResponse | null
  loading?: boolean
}>()

const emit = defineEmits<{
  submit: [request: CreateGuestRequest]
  cancel: []
}>()

const toast = useToast()

// Form data
const firstName = ref(props.guest?.firstName || '')
const lastName = ref(props.guest?.lastName || '')
const dateOfBirth = ref<Date | null>(
  props.guest?.dateOfBirth ? parseDateLocal(props.guest.dateOfBirth) : null,
)
const documentNumber = ref(props.guest?.documentNumber || '')
const email = ref(props.guest?.email || '')
const phone = ref(props.guest?.phone || '')
// Validation errors
const firstNameError = ref<string | null>(null)
const lastNameError = ref<string | null>(null)
const dateOfBirthError = ref<string | null>(null)
const documentNumberError = ref<string | null>(null)
const emailError = ref<string | null>(null)
const phoneError = ref<string | null>(null)

const isEditing = computed(() => !!props.guest)

// Validation functions
const validateFirstName = () => {
  if (!firstName.value.trim()) {
    firstNameError.value = 'El nombre es obligatorio'
    return false
  }
  if (firstName.value.length > 100) {
    firstNameError.value = 'El nombre no puede exceder 100 caracteres'
    return false
  }
  firstNameError.value = null
  return true
}

const validateLastName = () => {
  if (!lastName.value.trim()) {
    lastNameError.value = 'Los apellidos son obligatorios'
    return false
  }
  if (lastName.value.length > 100) {
    lastNameError.value = 'Los apellidos no pueden exceder 100 caracteres'
    return false
  }
  lastNameError.value = null
  return true
}

const validateDateOfBirth = () => {
  if (!dateOfBirth.value) {
    dateOfBirthError.value = 'La fecha de nacimiento es obligatoria'
    return false
  }
  if (dateOfBirth.value > new Date()) {
    dateOfBirthError.value = 'La fecha de nacimiento debe ser una fecha pasada'
    return false
  }
  dateOfBirthError.value = null
  return true
}

const validateDocumentNumber = () => {
  if (documentNumber.value && documentNumber.value.trim()) {
    const uppercaseAlphanumeric = /^[A-Z0-9]+$/
    if (!uppercaseAlphanumeric.test(documentNumber.value)) {
      documentNumberError.value =
        'El número de documento debe contener solo letras mayúsculas y números'
      return false
    }
    if (documentNumber.value.length > 50) {
      documentNumberError.value = 'El número de documento no puede exceder 50 caracteres'
      return false
    }
  }
  documentNumberError.value = null
  return true
}

const validateEmail = () => {
  if (email.value && email.value.trim()) {
    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailPattern.test(email.value)) {
      emailError.value = 'Formato de correo electrónico inválido'
      return false
    }
    if (email.value.length > 255) {
      emailError.value = 'El correo electrónico no puede exceder 255 caracteres'
      return false
    }
  }
  emailError.value = null
  return true
}

const validatePhone = () => {
  if (phone.value && phone.value.trim()) {
    const e164Pattern = /^\+[1-9]\d{1,14}$/
    if (!e164Pattern.test(phone.value)) {
      phoneError.value = 'El teléfono debe estar en formato E.164 (ej. +34612345678)'
      return false
    }
    if (phone.value.length > 20) {
      phoneError.value = 'El teléfono no puede exceder 20 caracteres'
      return false
    }
  }
  phoneError.value = null
  return true
}

const validateAll = () => {
  const validations = [
    validateFirstName(),
    validateLastName(),
    validateDateOfBirth(),
    validateDocumentNumber(),
    validateEmail(),
    validatePhone(),
  ]
  return validations.every((v) => v)
}

// Auto-uppercase document number
watch(documentNumber, (newValue) => {
  if (newValue) {
    documentNumber.value = newValue.toUpperCase()
  }
})

const handleSubmit = () => {
  if (!validateAll()) {
    toast.add({
      severity: 'error',
      summary: 'Validación',
      detail: 'Por favor revisa los datos ingresados',
      life: 3000,
    })
    return
  }

  const request: CreateGuestRequest = {
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    dateOfBirth: formatDateLocal(dateOfBirth.value!),
    documentNumber: documentNumber.value.trim() || null,
    email: email.value.trim() || null,
    phone: phone.value.trim() || null,
  }

  emit('submit', request)
}

const handleCancel = () => {
  emit('cancel')
}
</script>

<template>
  <form @submit.prevent="handleSubmit" class="space-y-4">
    <!-- First Name -->
    <div class="flex flex-col gap-2">
      <label for="guest-first-name" class="font-medium text-sm">
        Nombre <span class="text-red-500">*</span>
      </label>
      <InputText
        id="guest-first-name"
        v-model="firstName"
        placeholder="Ej: María"
        :invalid="!!firstNameError"
        :disabled="loading"
        @blur="validateFirstName"
      />
      <small v-if="firstNameError" class="text-red-500">{{ firstNameError }}</small>
    </div>

    <!-- Last Name -->
    <div class="flex flex-col gap-2">
      <label for="guest-last-name" class="font-medium text-sm">
        Apellidos <span class="text-red-500">*</span>
      </label>
      <InputText
        id="guest-last-name"
        v-model="lastName"
        placeholder="Ej: García López"
        :invalid="!!lastNameError"
        :disabled="loading"
        @blur="validateLastName"
      />
      <small v-if="lastNameError" class="text-red-500">{{ lastNameError }}</small>
    </div>

    <!-- Date of Birth -->
    <div class="flex flex-col gap-2">
      <label for="guest-date-of-birth" class="font-medium text-sm">
        Fecha de Nacimiento <span class="text-red-500">*</span>
      </label>
      <DateInput
        id="guest-date-of-birth"
        v-model="dateOfBirth"
        :maxDate="new Date()"
        :invalid="!!dateOfBirthError"
        :disabled="loading"
        @blur="validateDateOfBirth"
      />
      <small v-if="dateOfBirthError" class="text-red-500">{{ dateOfBirthError }}</small>
    </div>

    <!-- Document Number (optional) -->
    <div class="flex flex-col gap-2">
      <label for="guest-document-number" class="font-medium text-sm">Número de Documento</label>
      <InputText
        id="guest-document-number"
        v-model="documentNumber"
        placeholder="Ej: 12345678A"
        :invalid="!!documentNumberError"
        :disabled="loading"
        @blur="validateDocumentNumber"
      />
      <small v-if="documentNumberError" class="text-red-500">{{ documentNumberError }}</small>
      <small class="text-gray-500">Solo letras mayúsculas y números</small>
    </div>

    <!-- Email (optional) -->
    <div class="flex flex-col gap-2">
      <label for="guest-email" class="font-medium text-sm">Correo Electrónico</label>
      <InputText
        id="guest-email"
        v-model="email"
        type="email"
        placeholder="Ej: maria@example.com"
        :invalid="!!emailError"
        :disabled="loading"
        @blur="validateEmail"
      />
      <small v-if="emailError" class="text-red-500">{{ emailError }}</small>
    </div>

    <!-- Phone (optional) -->
    <div class="flex flex-col gap-2">
      <label for="guest-phone" class="font-medium text-sm">Teléfono</label>
      <InputText
        id="guest-phone"
        v-model="phone"
        placeholder="Ej: +34612345678"
        :invalid="!!phoneError"
        :disabled="loading"
        @blur="validatePhone"
      />
      <small v-if="phoneError" class="text-red-500">{{ phoneError }}</small>
      <small class="text-gray-500">Formato E.164 con código de país (ej. +34)</small>
    </div>

    <div class="flex justify-end gap-2 pt-4">
      <Button
        type="button"
        label="Cancelar"
        severity="secondary"
        :disabled="loading"
        @click="handleCancel"
      />
      <Button
        type="submit"
        :label="isEditing ? 'Actualizar invitado/a' : 'Añadir invitado/a'"
        :loading="loading"
        :disabled="loading"
      />
    </div>
  </form>
</template>

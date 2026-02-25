<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import DatePicker from 'primevue/datepicker'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import type {
  FamilyMemberResponse,
  CreateFamilyMemberRequest,
  UpdateFamilyMemberRequest,
  FamilyRelationship
} from '@/types/family-unit'
import { FamilyRelationshipLabels } from '@/types/family-unit'
import { formatDateLocal, parseDateLocal } from '@/utils/date'

const props = defineProps<{
  member?: FamilyMemberResponse | null
  loading?: boolean
}>()

const emit = defineEmits<{
  submit: [request: CreateFamilyMemberRequest | UpdateFamilyMemberRequest]
  cancel: []
}>()

const toast = useToast()

// Relationship options for dropdown
const relationshipOptions = Object.entries(FamilyRelationshipLabels).map(([value, label]) => ({
  label,
  value
}))

// Form data
const firstName = ref(props.member?.firstName || '')
const lastName = ref(props.member?.lastName || '')
const dateOfBirth = ref<Date | null>(props.member?.dateOfBirth ? parseDateLocal(props.member.dateOfBirth) : null)
const relationship = ref<FamilyRelationship | null>(props.member?.relationship || null)
const documentNumber = ref(props.member?.documentNumber || '')
const email = ref(props.member?.email || '')
const phone = ref(props.member?.phone || '')
const medicalNotes = ref('')  // Never pre-fill sensitive data
const allergies = ref('')      // Never pre-fill sensitive data

// Validation errors
const firstNameError = ref<string | null>(null)
const lastNameError = ref<string | null>(null)
const dateOfBirthError = ref<string | null>(null)
const relationshipError = ref<string | null>(null)
const documentNumberError = ref<string | null>(null)
const emailError = ref<string | null>(null)
const phoneError = ref<string | null>(null)

const isEditing = computed(() => !!props.member)

// Show sensitive data info for editing
const hasMedicalNotesInfo = computed(() => isEditing.value && props.member?.hasMedicalNotes)
const hasAllergiesInfo = computed(() => isEditing.value && props.member?.hasAllergies)

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

const validateRelationship = () => {
  if (!relationship.value) {
    relationshipError.value = 'El tipo de relación es obligatorio'
    return false
  }
  relationshipError.value = null
  return true
}

const validateDocumentNumber = () => {
  if (documentNumber.value && documentNumber.value.trim()) {
    const uppercaseAlphanumeric = /^[A-Z0-9]+$/
    if (!uppercaseAlphanumeric.test(documentNumber.value)) {
      documentNumberError.value = 'El número de documento debe contener solo letras mayúsculas y números'
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
    validateRelationship(),
    validateDocumentNumber(),
    validateEmail(),
    validatePhone()
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
      life: 3000
    })
    return
  }

  const request: CreateFamilyMemberRequest | UpdateFamilyMemberRequest = {
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    dateOfBirth: formatDateLocal(dateOfBirth.value!),
    relationship: relationship.value!,
    documentNumber: documentNumber.value.trim() || null,
    email: email.value.trim() || null,
    phone: phone.value.trim() || null,
    medicalNotes: medicalNotes.value.trim() || null,
    allergies: allergies.value.trim() || null
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
      <label for="first-name" class="font-medium text-sm">
        Nombre <span class="text-red-500">*</span>
      </label>
      <InputText
        id="first-name"
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
      <label for="last-name" class="font-medium text-sm">
        Apellidos <span class="text-red-500">*</span>
      </label>
      <InputText
        id="last-name"
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
      <label for="date-of-birth" class="font-medium text-sm">
        Fecha de Nacimiento <span class="text-red-500">*</span>
      </label>
      <DatePicker
        id="date-of-birth"
        v-model="dateOfBirth"
        dateFormat="dd/mm/yy"
        :maxDate="new Date()"
        :invalid="!!dateOfBirthError"
        :disabled="loading"
        showIcon
        @blur="validateDateOfBirth"
        class="w-full"
      />
      <small v-if="dateOfBirthError" class="text-red-500">{{ dateOfBirthError }}</small>
    </div>

    <!-- Relationship -->
    <div class="flex flex-col gap-2">
      <label for="relationship" class="font-medium text-sm">
        Relación Familiar <span class="text-red-500">*</span>
      </label>
      <Select
        id="relationship"
        v-model="relationship"
        :options="relationshipOptions"
        optionLabel="label"
        optionValue="value"
        placeholder="Selecciona una relación"
        :invalid="!!relationshipError"
        :disabled="loading"
        @change="validateRelationship"
        class="w-full"
      />
      <small v-if="relationshipError" class="text-red-500">{{ relationshipError }}</small>
    </div>

    <!-- Document Number (optional) -->
    <div class="flex flex-col gap-2">
      <label for="document-number" class="font-medium text-sm">Número de Documento</label>
      <InputText
        id="document-number"
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
      <label for="email" class="font-medium text-sm">Correo Electrónico</label>
      <InputText
        id="email"
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
      <label for="phone" class="font-medium text-sm">Teléfono</label>
      <InputText
        id="phone"
        v-model="phone"
        placeholder="Ej: +34612345678"
        :invalid="!!phoneError"
        :disabled="loading"
        @blur="validatePhone"
      />
      <small v-if="phoneError" class="text-red-500">{{ phoneError }}</small>
      <small class="text-gray-500">Formato E.164 con código de país (ej. +34)</small>
    </div>

    <!-- Medical Notes (optional, sensitive) -->
    <div class="flex flex-col gap-2">
      <label for="medical-notes" class="font-medium text-sm">Notas Médicas</label>
      <div v-if="hasMedicalNotesInfo" class="p-2 bg-blue-50 border border-blue-200 rounded text-sm text-blue-800">
        ℹ️ Este miembro tiene notas médicas guardadas. Déjalo en blanco para mantener las notas existentes, o escribe nuevas notas para reemplazarlas.
      </div>
      <Textarea
        id="medical-notes"
        v-model="medicalNotes"
        placeholder="Ej: Asma - requiere inhalador"
        :disabled="loading"
        rows="3"
        :maxlength="2000"
        class="w-full"
      />
      <small class="text-gray-500">Información sensible, encriptada. Máximo 2000 caracteres.</small>
    </div>

    <!-- Allergies (optional, sensitive) -->
    <div class="flex flex-col gap-2">
      <label for="allergies" class="font-medium text-sm">Alergias</label>
      <div v-if="hasAllergiesInfo" class="p-2 bg-blue-50 border border-blue-200 rounded text-sm text-blue-800">
        ℹ️ Este miembro tiene alergias guardadas. Déjalo en blanco para mantener las alergias existentes, o escribe nuevas alergias para reemplazarlas.
      </div>
      <Textarea
        id="allergies"
        v-model="allergies"
        placeholder="Ej: Cacahuetes, mariscos"
        :disabled="loading"
        rows="2"
        :maxlength="1000"
        class="w-full"
      />
      <small class="text-gray-500">Información sensible, encriptada. Máximo 1000 caracteres.</small>
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
        :label="isEditing ? 'Actualizar' : 'Añadir Miembro'"
        :loading="loading"
        :disabled="loading"
      />
    </div>
  </form>
</template>

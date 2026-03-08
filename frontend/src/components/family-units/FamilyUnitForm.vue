<script setup lang="ts">
import { ref, computed } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Message from 'primevue/message'
import type { FamilyUnitResponse, CreateFamilyUnitRequest, UpdateFamilyUnitRequest } from '@/types/family-unit'

const props = defineProps<{
  familyUnit?: FamilyUnitResponse | null
  loading?: boolean
}>()

const emit = defineEmits<{
  submit: [request: CreateFamilyUnitRequest | UpdateFamilyUnitRequest]
  cancel: []
}>()

const toast = useToast()

// Form data
const name = ref(props.familyUnit?.name || '')
const consentAccepted = ref(false)

// Validation
const nameError = ref<string | null>(null)
const consentError = ref<string | null>(null)

const isValid = computed(() => {
  return name.value.trim().length > 0 && name.value.length <= 200
})

const isEditing = computed(() => !!props.familyUnit)

const validateName = () => {
  if (!name.value.trim()) {
    nameError.value = 'El nombre de la unidad familiar es obligatorio'
    return false
  }
  if (name.value.length > 200) {
    nameError.value = 'El nombre no puede exceder 200 caracteres'
    return false
  }
  nameError.value = null
  return true
}

const validateConsent = () => {
  if (!isEditing.value && !consentAccepted.value) {
    consentError.value = 'Debes aceptar las condiciones para crear la familia'
    return false
  }
  consentError.value = null
  return true
}

const handleSubmit = () => {
  const isNameValid = validateName()
  const isConsentValid = validateConsent()

  if (!isNameValid || !isConsentValid) {
    toast.add({
      severity: 'error',
      summary: 'Validación',
      detail: 'Por favor revisa los datos ingresados',
      life: 3000
    })
    return
  }

  if (isEditing.value) {
    emit('submit', { name: name.value.trim() } as UpdateFamilyUnitRequest)
  } else {
    emit('submit', { name: name.value.trim() } as CreateFamilyUnitRequest)
  }
}

const handleCancel = () => {
  emit('cancel')
}
</script>

<template>
  <form @submit.prevent="handleSubmit" class="space-y-4">
    <div class="flex flex-col gap-2">
      <label for="family-unit-name" class="font-medium text-sm">
        Nombre de la Unidad Familiar <span class="text-red-500">*</span>
      </label>
      <InputText id="family-unit-name" v-model="name" placeholder="Ej: Familia García" :invalid="!!nameError"
        :disabled="loading" @blur="validateName" class="w-full" />
      <small v-if="nameError" class="text-red-500">{{ nameError }}</small>
      <small class="text-gray-500">Máximo 200 caracteres</small>
    </div>

    <!-- Duplicate warning and consent (only when creating) -->
    <template v-if="!isEditing">
      <Message severity="warn" :closable="false">
        Antes de crear una familia, asegúrate de que ningún otro miembro de tu familia la haya creado ya.
        Si es así, indícale tu email de registro para que aparezcas en ella.
      </Message>

      <div class="flex flex-col gap-2">
        <div class="flex items-start gap-2">
          <Checkbox id="consent-accepted" v-model="consentAccepted" :binary="true" :invalid="!!consentError"
            :disabled="loading" data-testid="consent-checkbox" />
          <label for="consent-accepted" class="text-sm text-gray-700">
            Actuaré como representante de la familia y seré quien realice la inscripción de la familia
            al campamento. Además, confirmo que tengo el consentimiento de cada miembro de la
            familia para darles de alta en la plataforma.
          </label>
        </div>
        <small v-if="consentError" class="text-red-500">{{ consentError }}</small>
      </div>
    </template>

    <div class="flex justify-end gap-2 pt-4">
      <Button type="button" label="Cancelar" severity="secondary" :disabled="loading" @click="handleCancel" />
      <Button type="submit" :label="isEditing ? 'Actualizar' : 'Crear'" :loading="loading"
        :disabled="!isValid || (!isEditing && !consentAccepted) || loading" />
    </div>
  </form>
</template>

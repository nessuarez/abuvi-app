<script setup lang="ts">
import { ref, computed } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
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

// Validation
const nameError = ref<string | null>(null)

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

const handleSubmit = () => {
  if (!validateName()) {
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
      <InputText
        id="family-unit-name"
        v-model="name"
        placeholder="Ej: Familia García"
        :invalid="!!nameError"
        :disabled="loading"
        @blur="validateName"
        class="w-full"
      />
      <small v-if="nameError" class="text-red-500">{{ nameError }}</small>
      <small class="text-gray-500">Máximo 200 caracteres</small>
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
        :label="isEditing ? 'Actualizar' : 'Crear'"
        :loading="loading"
        :disabled="!isValid || loading"
      />
    </div>
  </form>
</template>

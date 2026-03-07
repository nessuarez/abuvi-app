<script setup lang="ts">
import { ref, onMounted } from 'vue'
import Card from 'primevue/card'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useAssociationSettings } from '@/composables/useAssociationSettings'

const { ageRanges, loading, error, fetchAgeRanges, updateAgeRanges } = useAssociationSettings()

const form = ref({
  babyMaxAge: 2,
  childMinAge: 3,
  childMaxAge: 12,
  adultMinAge: 13
})

const errors = ref<Record<string, string>>({})
const success = ref(false)

const initializeForm = () => {
  if (ageRanges.value) {
    form.value.babyMaxAge = ageRanges.value.babyMaxAge
    form.value.childMinAge = ageRanges.value.childMinAge
    form.value.childMaxAge = ageRanges.value.childMaxAge
    form.value.adultMinAge = ageRanges.value.adultMinAge
  }
}

const validate = (): boolean => {
  errors.value = {}
  if (form.value.babyMaxAge < 0) errors.value.babyMaxAge = 'Debe ser mayor o igual a 0'
  if (form.value.childMinAge < 0) errors.value.childMinAge = 'Debe ser mayor o igual a 0'
  if (form.value.childMaxAge < 0) errors.value.childMaxAge = 'Debe ser mayor o igual a 0'
  if (form.value.adultMinAge < 0) errors.value.adultMinAge = 'Debe ser mayor o igual a 0'
  if (form.value.babyMaxAge >= form.value.childMinAge) {
    errors.value.babyMaxAge = 'La edad máxima de bebé debe ser menor a la edad mínima de niño'
  }
  if (form.value.childMaxAge >= form.value.adultMinAge) {
    errors.value.childMaxAge = 'La edad máxima de niño debe ser menor a la edad mínima de adulto'
  }
  return Object.keys(errors.value).length === 0
}

const handleSave = async () => {
  if (!validate()) return
  success.value = false
  const result = await updateAgeRanges({
    babyMaxAge: form.value.babyMaxAge,
    childMinAge: form.value.childMinAge,
    childMaxAge: form.value.childMaxAge,
    adultMinAge: form.value.adultMinAge
  })
  if (result) {
    success.value = true
    setTimeout(() => { success.value = false }, 3000)
  }
}

onMounted(async () => {
  await fetchAgeRanges()
  initializeForm()
})
</script>

<template>
  <div class="space-y-6">
    <Card>
      <template #title>
        <div class="flex items-center gap-2">
          <i class="pi pi-sliders-h text-primary-600" />
          Rangos de edad por defecto
        </div>
      </template>
      <template #subtitle>
        Estos rangos se aplican a todas las ediciones de campamento que no tengan rangos personalizados.
      </template>
      <template #content>
        <Message v-if="error" severity="error" :closable="false" class="mb-4">{{ error }}</Message>
        <Message v-if="success" severity="success" :closable="false" class="mb-4">
          Rangos de edad actualizados correctamente.
        </Message>

        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div class="flex flex-col gap-1">
            <label class="text-sm font-medium text-gray-700">Edad máx. bebé</label>
            <InputNumber
              v-model="form.babyMaxAge"
              :min="0"
              :max="10"
              :use-grouping="false"
              class="w-full"
            />
            <span v-if="errors.babyMaxAge" class="text-xs text-red-600">{{ errors.babyMaxAge }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-sm font-medium text-gray-700">Edad mín. niño</label>
            <InputNumber
              v-model="form.childMinAge"
              :min="0"
              :max="18"
              :use-grouping="false"
              class="w-full"
            />
            <span v-if="errors.childMinAge" class="text-xs text-red-600">{{ errors.childMinAge }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-sm font-medium text-gray-700">Edad máx. niño</label>
            <InputNumber
              v-model="form.childMaxAge"
              :min="0"
              :max="18"
              :use-grouping="false"
              class="w-full"
            />
            <span v-if="errors.childMaxAge" class="text-xs text-red-600">{{ errors.childMaxAge }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-sm font-medium text-gray-700">Edad mín. adulto</label>
            <InputNumber
              v-model="form.adultMinAge"
              :min="0"
              :max="99"
              :use-grouping="false"
              class="w-full"
            />
            <span v-if="errors.adultMinAge" class="text-xs text-red-600">{{ errors.adultMinAge }}</span>
          </div>
        </div>

        <div class="mt-4 flex items-center gap-3">
          <Button
            label="Guardar"
            icon="pi pi-check"
            :loading="loading"
            @click="handleSave"
          />
          <span class="text-xs text-gray-500">
            Los cambios afectarán a las ediciones que no usen rangos personalizados.
          </span>
        </div>
      </template>
    </Card>
  </div>
</template>

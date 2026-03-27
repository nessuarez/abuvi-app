<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import type { CreateMembershipFeeRequest } from '@/types/membership'

const props = defineProps<{
  visible: boolean
  membershipId: string
  loading?: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [request: CreateMembershipFeeRequest]
}>()

const currentYear = new Date().getFullYear()

const year = ref<number>(currentYear)
const amount = ref<number>(0)

const yearError = ref<string | null>(null)
const amountError = ref<string | null>(null)

watch(
  () => props.visible,
  (val) => {
    if (val) {
      year.value = currentYear
      amount.value = 0
      yearError.value = null
      amountError.value = null
    }
  },
)

const validate = (): boolean => {
  let valid = true

  if (!year.value || year.value <= 2000 || year.value > currentYear) {
    yearError.value = `El año debe estar entre 2001 y ${currentYear}`
    valid = false
  } else {
    yearError.value = null
  }

  if (amount.value === null || amount.value === undefined || amount.value < 0) {
    amountError.value = 'El importe debe ser mayor o igual a 0'
    valid = false
  } else {
    amountError.value = null
  }

  return valid
}

const handleSubmit = () => {
  if (!validate()) return
  emit('submit', { year: year.value, amount: amount.value })
}

const handleClose = () => {
  emit('update:visible', false)
}
</script>

<template>
  <Dialog
    :visible="visible"
    header="Cargar cuota anual"
    :modal="true"
    :closable="!loading"
    :dismissable-mask="!loading"
    class="w-full max-w-md"
    @update:visible="emit('update:visible', $event)"
  >
    <form @submit.prevent="handleSubmit" class="space-y-4">
      <!-- Year -->
      <div class="flex flex-col gap-2">
        <label for="fee-year" class="text-sm font-medium">
          Año <span class="text-red-500">*</span>
        </label>
        <InputNumber
          id="fee-year"
          v-model="year"
          :min="2001"
          :max="currentYear"
          :use-grouping="false"
          :invalid="!!yearError"
          :disabled="loading"
          class="w-full"
        />
        <small v-if="yearError" class="text-red-500">{{ yearError }}</small>
      </div>

      <!-- Amount -->
      <div class="flex flex-col gap-2">
        <label for="fee-amount" class="text-sm font-medium">
          Importe (€) <span class="text-red-500">*</span>
        </label>
        <InputNumber
          id="fee-amount"
          v-model="amount"
          :min="0"
          mode="currency"
          currency="EUR"
          locale="es-ES"
          :invalid="!!amountError"
          :disabled="loading"
          class="w-full"
        />
        <small v-if="amountError" class="text-red-500">{{ amountError }}</small>
        <small class="text-gray-500">Introduce 0 si el importe aún no está definido.</small>
      </div>

      <div class="flex justify-end gap-2 pt-2">
        <Button
          type="button"
          label="Cancelar"
          severity="secondary"
          :disabled="loading"
          @click="handleClose"
        />
        <Button
          type="submit"
          label="Cargar cuota"
          icon="pi pi-plus"
          :loading="loading"
          :disabled="loading"
        />
      </div>
    </form>
  </Dialog>
</template>

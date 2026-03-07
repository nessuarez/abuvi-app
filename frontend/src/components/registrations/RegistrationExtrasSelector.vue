<script setup lang="ts">
import { computed } from 'vue'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import type { CampEditionExtra } from '@/types/camp-edition'
import type { WizardExtrasSelection } from '@/types/registration'

const props = defineProps<{
  extras: CampEditionExtra[]
  modelValue: WizardExtrasSelection[]
}>()

const emit = defineEmits<{
  'update:modelValue': [selections: WizardExtrasSelection[]]
}>()

const PRICING_TYPE_LABELS: Record<'PerPerson' | 'PerFamily', string> = {
  PerPerson: 'por persona',
  PerFamily: 'por familia'
}

const PRICING_PERIOD_LABELS: Record<'OneTime' | 'PerDay', string> = {
  OneTime: '',
  PerDay: '/día'
}

const activeExtras = computed(() => props.extras.filter((e) => e.isActive))

const getQuantity = (extra: CampEditionExtra): number => {
  const found = props.modelValue.find((s) => s.campEditionExtraId === extra.id)
  return found?.quantity ?? (extra.isRequired ? 1 : 0)
}

const getUserInput = (extra: CampEditionExtra): string => {
  const selection = props.modelValue.find((s) => s.campEditionExtraId === extra.id)
  return selection?.userInput ?? ''
}

const updateQuantity = (extra: CampEditionExtra, quantity: number) => {
  const current = props.modelValue.filter((s) => s.campEditionExtraId !== extra.id)
  const existingSelection = props.modelValue.find((s) => s.campEditionExtraId === extra.id)
  const updated: WizardExtrasSelection[] = [
    ...current,
    {
      campEditionExtraId: extra.id,
      name: extra.name,
      quantity,
      unitPrice: extra.price,
      userInput: quantity > 0 ? existingSelection?.userInput : undefined
    }
  ]
  emit('update:modelValue', updated)
}

const updateUserInput = (extra: CampEditionExtra, value: string) => {
  const updated = props.modelValue.map((s) =>
    s.campEditionExtraId === extra.id ? { ...s, userInput: value } : s
  )
  emit('update:modelValue', updated)
}

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)

const pricingLabel = (extra: CampEditionExtra): string => {
  if (extra.price === 0) return 'Incluido'
  const type = PRICING_TYPE_LABELS[extra.pricingType]
  const period = PRICING_PERIOD_LABELS[extra.pricingPeriod]
  return `${formatCurrency(extra.price)} ${type}${period}`
}
</script>

<template>
  <div class="space-y-3">
    <div
      v-for="extra in activeExtras"
      :key="extra.id"
      class="rounded-lg border border-gray-200 bg-white p-4"
      data-testid="extra-item"
    >
      <div class="flex items-start justify-between gap-4">
        <div class="flex-1">
          <div class="flex items-center gap-2">
            <span class="font-medium text-gray-900">{{ extra.name }}</span>
            <span
              v-if="extra.isRequired"
              class="inline-flex items-center gap-1 text-xs text-gray-500"
              data-testid="required-lock-icon"
              title="Extra obligatorio"
            >
              <i class="pi pi-lock" />
              Obligatorio
            </span>
          </div>
          <p v-if="extra.description" class="mt-0.5 text-sm text-gray-500">{{ extra.description }}</p>
          <p class="mt-1 text-sm font-medium text-blue-700">{{ pricingLabel(extra) }}</p>
        </div>
        <div class="flex shrink-0 items-center gap-2">
          <InputNumber
            :model-value="getQuantity(extra)"
            :min="extra.isRequired ? 1 : 0"
            :max="extra.maxQuantity ?? 99"
            :disabled="extra.isRequired"
            show-buttons
            button-layout="horizontal"
            decrement-button-class="p-button-secondary p-button-sm"
            increment-button-class="p-button-secondary p-button-sm"
            input-class="w-12 text-center"
            @update:model-value="updateQuantity(extra, $event ?? 0)"
            data-testid="extra-quantity-input"
          />
        </div>
      </div>
      <div v-if="extra.requiresUserInput && getQuantity(extra) > 0" class="mt-2" data-testid="extra-user-input">
        <label class="mb-1 block text-xs font-medium text-gray-600">
          {{ extra.userInputLabel || 'Información adicional' }}
        </label>
        <Textarea
          :model-value="getUserInput(extra)"
          @update:model-value="updateUserInput(extra, $event)"
          :rows="2"
          :maxlength="500"
          class="w-full"
          :placeholder="extra.userInputLabel || 'Escribe aquí...'"
        />
      </div>
    </div>

    <p v-if="activeExtras.length === 0" class="py-4 text-center text-sm text-gray-400">
      No hay extras disponibles para esta edición.
    </p>
  </div>
</template>

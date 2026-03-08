<script setup lang="ts">
import { ref, watch } from 'vue'
import InputText from 'primevue/inputtext'

interface Props {
  modelValue: string | null
  invalid?: boolean
  disabled?: boolean
  id?: string
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  invalid: false,
  disabled: false,
  id: 'phone',
})

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
  blur: []
}>()

const countryCode = ref('34')
const phoneNumber = ref('')

const ONE_DIGIT_CODES = new Set(['1', '7'])
const TWO_DIGIT_CODES = new Set([
  '20', '27', '30', '31', '32', '33', '34', '36', '39', '40', '41', '43', '44', '45', '46', '47',
  '48', '49', '51', '52', '53', '54', '55', '56', '57', '58', '60', '61', '62', '63', '64', '65',
  '66', '81', '82', '84', '86', '90', '91', '92', '93', '94', '95', '98',
])

function parseE164(value: string | null): { code: string; number: string } {
  if (!value) return { code: '34', number: '' }

  if (value.startsWith('+')) {
    const digits = value.slice(1)
    if (ONE_DIGIT_CODES.has(digits.charAt(0))) {
      return { code: digits.charAt(0), number: digits.slice(1) }
    }
    if (TWO_DIGIT_CODES.has(digits.slice(0, 2))) {
      return { code: digits.slice(0, 2), number: digits.slice(2) }
    }
    if (digits.length > 3) {
      const threeDigit = digits.slice(0, 3)
      return { code: threeDigit, number: digits.slice(3) }
    }
    return { code: digits.slice(0, 2) || '34', number: digits.slice(2) }
  }

  return { code: '34', number: value.replace(/\D/g, '') }
}

function emitValue() {
  if (!phoneNumber.value) {
    emit('update:modelValue', null)
    return
  }
  emit('update:modelValue', `+${countryCode.value}${phoneNumber.value}`)
}

function filterDigits(value: string): string {
  return value.replace(/\D/g, '')
}

function handleCountryCodeInput(event: Event) {
  const input = event.target as HTMLInputElement
  const filtered = filterDigits(input.value).slice(0, 4)
  countryCode.value = filtered
  input.value = filtered
  emitValue()
}

function handlePhoneNumberInput(event: Event) {
  const input = event.target as HTMLInputElement
  const filtered = filterDigits(input.value)
  phoneNumber.value = filtered
  input.value = filtered
  emitValue()
}

function handleBlur() {
  emit('blur')
}

// Sync external modelValue → internal fields
watch(
  () => props.modelValue,
  (newVal) => {
    const parsed = parseE164(newVal)
    const currentComposed = phoneNumber.value
      ? `+${countryCode.value}${phoneNumber.value}`
      : null
    if (newVal !== currentComposed) {
      countryCode.value = parsed.code
      phoneNumber.value = parsed.number
    }
  },
  { immediate: true },
)
</script>

<template>
  <div class="flex">
    <div class="relative flex items-center">
      <span
        class="pointer-events-none absolute left-3 text-surface-500"
        aria-hidden="true"
      >+</span>
      <InputText
        :model-value="countryCode"
        :invalid="invalid"
        :disabled="disabled"
        aria-label="Código de país"
        class="w-[70px] rounded-r-none border-r-0 pl-6 text-center"
        data-testid="phone-country-code"
        @input="handleCountryCodeInput"
      />
    </div>
    <InputText
      :id="id"
      :model-value="phoneNumber"
      :invalid="invalid"
      :disabled="disabled"
      placeholder="612345678"
      class="min-w-0 flex-1 rounded-l-none"
      inputmode="numeric"
      :data-testid="`${id}-number`"
      @input="handlePhoneNumberInput"
      @blur="handleBlur"
    />
  </div>
</template>

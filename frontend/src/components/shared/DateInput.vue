<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import InputMask from 'primevue/inputmask'
import DatePicker from 'primevue/datepicker'
import Popover from 'primevue/popover'
import Button from 'primevue/button'

interface Props {
  modelValue: Date | null
  invalid?: boolean
  disabled?: boolean
  minDate?: Date
  maxDate?: Date
  showCalendar?: boolean
  id?: string
  placeholder?: string
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  invalid: false,
  disabled: false,
  showCalendar: true,
  placeholder: 'DD/MM/AAAA'
})

const emit = defineEmits<{
  'update:modelValue': [value: Date | null]
  blur: []
}>()

const maskedValue = ref('')
const popoverRef = ref()
const internalInvalid = ref(false)

const isInvalid = computed(() => props.invalid || internalInvalid.value)

function parseInputToDate(masked: string): Date | null {
  const match = masked.match(/^(\d{2})\/(\d{2})\/(\d{4})$/)
  if (!match) return null
  const [, dd, mm, yyyy] = match
  const day = +dd
  const month = +mm
  const year = +yyyy
  if (month < 1 || month > 12 || day < 1 || year < 1) return null
  const date = new Date(year, month - 1, day)
  if (date.getDate() !== day || date.getMonth() !== month - 1 || date.getFullYear() !== year) return null
  return date
}

function formatDateToInput(date: Date): string {
  const dd = String(date.getDate()).padStart(2, '0')
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const yyyy = String(date.getFullYear())
  return `${dd}/${mm}/${yyyy}`
}

function isDateInRange(date: Date): boolean {
  if (props.minDate && date < props.minDate) return false
  if (props.maxDate && date > props.maxDate) return false
  return true
}

// Sync external modelValue → masked text
watch(
  () => props.modelValue,
  (newVal) => {
    if (newVal) {
      const formatted = formatDateToInput(newVal)
      if (maskedValue.value !== formatted) {
        maskedValue.value = formatted
      }
      internalInvalid.value = false
    } else {
      maskedValue.value = ''
      internalInvalid.value = false
    }
  },
  { immediate: true }
)

function handleMaskComplete() {
  const date = parseInputToDate(maskedValue.value)
  if (!date) {
    internalInvalid.value = true
    return
  }
  if (!isDateInRange(date)) {
    internalInvalid.value = true
    return
  }
  internalInvalid.value = false
  emit('update:modelValue', date)
}

function handleMaskChange() {
  if (!maskedValue.value || maskedValue.value.includes('_') || maskedValue.value.length < 10) {
    if (props.modelValue !== null) {
      internalInvalid.value = false
      emit('update:modelValue', null)
    }
    return
  }
  handleMaskComplete()
}

function handleBlur() {
  if (maskedValue.value && maskedValue.value.length === 10 && !maskedValue.value.includes('_')) {
    handleMaskComplete()
  }
  emit('blur')
}

function toggleCalendar(event: Event) {
  popoverRef.value?.toggle(event)
}

function handleCalendarSelect(date: Date | null) {
  if (date) {
    maskedValue.value = formatDateToInput(date)
    internalInvalid.value = false
    emit('update:modelValue', date)
  }
  popoverRef.value?.hide()
}
</script>

<template>
  <div class="flex items-center gap-1">
    <InputMask
      v-model="maskedValue"
      mask="99/99/9999"
      :placeholder="placeholder"
      :invalid="isInvalid"
      :disabled="disabled"
      :id="id"
      inputmode="numeric"
      class="flex-1"
      :slotChar="placeholder"
      data-testid="date-input-mask"
      @complete="handleMaskComplete"
      @update:model-value="handleMaskChange"
      @blur="handleBlur"
    />
    <Button
      v-if="showCalendar"
      type="button"
      icon="pi pi-calendar"
      severity="secondary"
      text
      :disabled="disabled"
      aria-label="Open calendar"
      data-testid="date-input-calendar-btn"
      @click="toggleCalendar"
    />
    <Popover v-if="showCalendar" ref="popoverRef">
      <DatePicker
        :model-value="modelValue"
        :min-date="minDate"
        :max-date="maxDate"
        inline
        date-format="dd/mm/yy"
        @update:model-value="handleCalendarSelect"
      />
    </Popover>
  </div>
</template>

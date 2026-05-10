<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { onClickOutside } from '@vueuse/core'

const props = defineProps<{
  modelValue: string
  error?: string | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const showPicker = ref(false)
const wrapperRef = ref<HTMLElement | null>(null)
const pickerContainerRef = ref<HTMLElement | null>(null)

onClickOutside(wrapperRef, () => {
  showPicker.value = false
})

onMounted(async () => {
  const emojiData = (await import('@emoji-mart/data')).default
  const { Picker } = await import('emoji-mart')

  const picker = new Picker({
    data: emojiData,
    locale: 'en',
    onEmojiSelect: (emoji: { native: string }) => {
      emit('update:modelValue', emoji.native)
      showPicker.value = false
    },
    previewPosition: 'none',
    skinTonePosition: 'none',
    theme: 'light',
  })

  if (pickerContainerRef.value) {
    pickerContainerRef.value.appendChild(picker as unknown as HTMLElement)
  }
})

function togglePicker() {
  showPicker.value = !showPicker.value
}

function clearValue() {
  emit('update:modelValue', '')
}
</script>

<template>
  <div ref="wrapperRef" class="relative inline-flex items-center gap-2">
    <button
      type="button"
      class="flex items-center gap-2 rounded-md border px-3 py-2 text-sm transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-primary-500"
      :class="error ? 'border-red-500' : 'border-gray-300'"
      @click="togglePicker"
    >
      <span v-if="modelValue" class="text-2xl leading-none">{{ modelValue }}</span>
      <span v-else class="text-gray-400">Seleccionar icono…</span>
      <i class="pi pi-angle-down text-xs text-gray-400" />
    </button>

    <button
      v-if="modelValue"
      type="button"
      class="text-gray-400 hover:text-gray-600 focus:outline-none"
      title="Limpiar icono"
      @click="clearValue"
    >
      <i class="pi pi-times text-xs" />
    </button>

    <div v-show="showPicker" class="absolute left-0 top-full z-50 mt-1 shadow-xl">
      <div ref="pickerContainerRef" />
    </div>
  </div>
</template>

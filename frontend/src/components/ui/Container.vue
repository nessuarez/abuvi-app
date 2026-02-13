<script setup lang="ts">
interface Props {
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl' | 'full'
  noPadding?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  maxWidth: 'xl',
  noPadding: false
})

const maxWidthClasses = {
  sm: 'max-w-screen-sm',
  md: 'max-w-screen-md',
  lg: 'max-w-screen-lg',
  xl: 'max-w-screen-xl',
  full: 'max-w-full'
}
</script>

<template>
  <div
    class="mx-auto w-full"
    :class="[
      maxWidthClasses[maxWidth],
      { 'px-4 sm:px-6 lg:px-8': !noPadding }
    ]"
  >
    <slot />
  </div>
</template>

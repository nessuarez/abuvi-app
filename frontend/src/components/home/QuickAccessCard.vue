<script setup lang="ts">
interface Props {
  icon: string
  label: string
  description: string
  ctaLabel: string
  ctaPath: string
}

defineProps<Props>()
</script>

<template>
  <div
    class="group rounded-lg border border-gray-200 bg-white p-6 shadow-sm transition-all hover:shadow-md"
  >
    <div class="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-primary-100">
      <i :class="`${icon} text-2xl text-primary-600`" />
    </div>

    <h3 class="mb-2 text-lg font-semibold text-gray-900">{{ label }}</h3>
    <p class="mb-4 text-sm text-gray-600">{{ description }}</p>

    <router-link
      :to="ctaPath"
      class="inline-flex items-center gap-2 text-sm font-medium text-primary-600 transition-colors hover:text-primary-700"
    >
      {{ ctaLabel }}
      <i class="pi pi-arrow-right" />
    </router-link>
  </div>
</template>

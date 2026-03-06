<script setup lang="ts">
import { onMounted, nextTick } from 'vue'
import HomeHeroCarousel from '@/components/home/HomeHeroCarousel.vue'
import { useOnboarding } from '@/composables/useOnboarding'

const { autoTrigger } = useOnboarding()

onMounted(() => {
  nextTick(() => {
    autoTrigger()
  })
})
</script>

<template>
  <div class="bg-gray-50">
    <div data-onboarding="hero-carousel">
      <HomeHeroCarousel />
    </div>
  </div>
</template>

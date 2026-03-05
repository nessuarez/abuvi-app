<script setup lang="ts">
import { onMounted, nextTick } from 'vue'
import Container from '@/components/ui/Container.vue'
import QuickAccessCards from '@/components/home/QuickAccessCards.vue'
import AnniversarySection from '@/components/home/AnniversarySection.vue'
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
    <Container>
      <div class="py-8" data-onboarding="welcome-heading">
        <QuickAccessCards />
        <AnniversarySection />
      </div>
    </Container>
  </div>
</template>

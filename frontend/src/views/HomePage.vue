<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
import QuickAccessCards from '@/components/home/QuickAccessCards.vue'
import AnniversarySection from '@/components/home/AnniversarySection.vue'
</script>

<template>
  <div class="bg-gray-50">
    <Container>
      <div class="py-8">
        <QuickAccessCards />
        <AnniversarySection />
      </div>
    </Container>
  </div>
</template>

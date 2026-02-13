<script setup lang="ts">
import AppHeader from '@/components/layout/AppHeader.vue'
import AppFooter from '@/components/layout/AppFooter.vue'
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <AppHeader />

    <main class="flex-1 bg-white">
      <router-view />
    </main>

    <AppFooter />
  </div>
</template>

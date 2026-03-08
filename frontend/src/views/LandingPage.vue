<script setup lang="ts">
import AuthContainer from '@/components/auth/AuthContainer.vue'
import landingBackground from '@/assets/images/landing-background.png'

const isDemo = import.meta.env.VITE_APP_ENV !== 'production'
</script>

<template>
  <div class="relative flex min-h-screen items-center justify-center">
    <!-- DEMO badge (non-production only) -->
    <div
      v-if="isDemo"
      class="fixed top-4 right-4 z-50 rounded-full bg-amber-500 px-4 py-1 text-sm font-bold text-white shadow-lg"
    >
      DEMO
    </div>

    <!-- Blurred background image -->
    <div
      class="absolute inset-0 bg-cover bg-center bg-no-repeat"
      :style="{
        backgroundImage: `url(${landingBackground})`,
        filter: 'blur(8px)',
        transform: 'scale(1.1)'
      }"
    />

    <!-- Dark overlay for better contrast -->
    <div class="absolute inset-0 bg-black/40" />

    <!-- Centered auth container -->
    <div class="relative z-10 px-4">
      <AuthContainer />
    </div>
  </div>
</template>

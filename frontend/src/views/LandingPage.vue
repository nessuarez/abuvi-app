<script setup lang="ts">
import AuthContainer from '@/components/auth/AuthContainer.vue'
</script>

<template>
  <div class="relative flex min-h-screen items-center justify-center">
    <!-- Blurred background image -->
    <div class="absolute inset-0 bg-cover bg-center bg-no-repeat" style="
        background-image: url('/src/assets/images/landing-background.png');
        filter: blur(8px);
        transform: scale(1.1);
      " />

    <!-- Dark overlay for better contrast -->
    <div class="absolute inset-0 bg-black/40" />

    <!-- Centered auth container -->
    <div class="relative z-10 px-4">
      <AuthContainer />
    </div>
  </div>
</template>

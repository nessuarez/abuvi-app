<script setup lang="ts">
import { ref } from 'vue'

const message = ref('Welcome to ABUVI')
</script>

<template>
  <div class="flex min-h-screen items-center justify-center">
    <h1 class="text-4xl font-bold">{{ message }}</h1>
  </div>
</template>

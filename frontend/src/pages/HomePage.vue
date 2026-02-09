<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
</script>

<template>
  <div class="flex min-h-screen items-center justify-center px-4">
    <div class="text-center">
      <h1 class="mb-4 text-4xl font-bold text-gray-900">
        Welcome to ABUVI
      </h1>

      <!-- Authenticated state -->
      <p v-if="authStore.user" class="text-lg text-gray-700">
        Hello,
        <span class="font-semibold text-primary-600">{{ authStore.user.firstName }}</span>!
        You are logged in as
        <span class="inline-flex items-center rounded-full bg-primary-100 px-3 py-1 text-sm font-semibold text-primary-800">
          {{ authStore.user.role }}
        </span>.
      </p>

      <!-- Unauthenticated state -->
      <p v-else class="text-gray-500">
        Please <router-link to="/login" class="font-medium text-primary-600 hover:text-primary-500">log in</router-link> to access your account.
      </p>
    </div>
  </div>
</template>

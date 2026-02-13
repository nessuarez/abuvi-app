<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <Container>
    <div class="py-12">
      <h1 class="mb-4 text-4xl font-bold text-gray-900">My Profile</h1>
      <div class="rounded-lg border border-gray-200 bg-white p-6">
        <p class="mb-2"><strong>Name:</strong> {{ auth.fullName }}</p>
        <p class="mb-2"><strong>Email:</strong> {{ auth.user?.email }}</p>
        <p><strong>Role:</strong> {{ auth.user?.role }}</p>
      </div>
      <p class="mt-4 text-gray-600">
        Full profile management will be implemented in future iterations.
      </p>
    </div>
  </Container>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '@/utils/api'

const message = ref('Welcome to ABUVI')
const healthStatus = ref<string | null>(null)
const loading = ref(false)

interface HealthResponse {
  status: string
  timestamp: string
}

onMounted(async () => {
  loading.value = true
  try {
    // Test backend health endpoint
    const response = await api.get<HealthResponse>('/health')
    healthStatus.value = response.data.status
  } catch (error) {
    console.error('Health check failed:', error)
    healthStatus.value = 'unavailable'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="flex min-h-screen flex-col items-center justify-center bg-gray-50">
    <h1 class="mb-4 text-4xl font-bold text-gray-900">{{ message }}</h1>
    <div v-if="loading" class="text-gray-600">
      Checking backend connection...
    </div>
    <div v-else-if="healthStatus" class="flex items-center gap-2">
      <span
        class="inline-block h-3 w-3 rounded-full"
        :class="healthStatus === 'healthy' ? 'bg-green-500' : 'bg-red-500'"
      />
      <span class="text-sm text-gray-600">
        Backend: {{ healthStatus }}
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuth } from '@/composables/useAuth'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Card from 'primevue/card'

const router = useRouter()
const route = useRoute()
const { login, loading, error } = useAuth()

const formData = reactive({
  email: '',
  password: ''
})

const validationErrors = ref<Record<string, string>>({})

const validate = (): boolean => {
  validationErrors.value = {}

  if (!formData.email) {
    validationErrors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    validationErrors.value.email = 'Invalid email format'
  }

  if (!formData.password) {
    validationErrors.value.password = 'Password is required'
  }

  return Object.keys(validationErrors.value).length === 0
}

const handleLogin = async () => {
  if (!validate()) return

  const success = await login(formData)

  if (success) {
    // Redirect to intended page or default to /users
    const redirect = (route.query.redirect as string) || '/users'
    router.push(redirect)
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-4">
    <Card class="w-full max-w-md">
      <template #title>
        <h1 class="text-2xl font-bold text-gray-900">Login to ABUVI</h1>
      </template>

      <template #content>
        <!-- Error message -->
        <Message v-if="error" severity="error" :closable="false" class="mb-4">
          {{ error }}
        </Message>

        <form @submit.prevent="handleLogin" class="flex flex-col gap-4">
          <!-- Email field -->
          <div>
            <label for="email" class="mb-2 block text-sm font-medium text-gray-700">
              Email *
            </label>
            <InputText
              id="email"
              v-model="formData.email"
              type="email"
              placeholder="your.email@example.com"
              class="w-full"
              :invalid="!!validationErrors.email"
              :disabled="loading"
            />
            <small v-if="validationErrors.email" class="text-red-500">
              {{ validationErrors.email }}
            </small>
          </div>

          <!-- Password field -->
          <div>
            <label for="password" class="mb-2 block text-sm font-medium text-gray-700">
              Password *
            </label>
            <InputText
              id="password"
              v-model="formData.password"
              type="password"
              placeholder="Enter your password"
              class="w-full"
              :invalid="!!validationErrors.password"
              :disabled="loading"
            />
            <small v-if="validationErrors.password" class="text-red-500">
              {{ validationErrors.password }}
            </small>
          </div>

          <!-- Submit button -->
          <Button
            type="submit"
            label="Login"
            :loading="loading"
            :disabled="loading"
            class="w-full"
          />
        </form>

        <!-- Register link -->
        <div class="mt-4 text-center">
          <p class="text-sm text-gray-600">
            Don't have an account?
            <router-link to="/register" class="font-medium text-primary-600 hover:text-primary-500">
              Register here
            </router-link>
          </p>
        </div>
      </template>
    </Card>
  </div>
</template>

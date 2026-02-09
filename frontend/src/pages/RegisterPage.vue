<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '@/composables/useAuth'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Card from 'primevue/card'

const router = useRouter()
const { register, loading, error } = useAuth()

const formData = reactive({
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  phone: ''
})

const validationErrors = ref<Record<string, string>>({})
const successMessage = ref<string | null>(null)

const validate = (): boolean => {
  validationErrors.value = {}

  // Email validation
  if (!formData.email) {
    validationErrors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    validationErrors.value.email = 'Invalid email format'
  } else if (formData.email.length > 255) {
    validationErrors.value.email = 'Email must not exceed 255 characters'
  }

  // Password validation (matches backend requirements)
  if (!formData.password) {
    validationErrors.value.password = 'Password is required'
  } else if (formData.password.length < 8) {
    validationErrors.value.password = 'Password must be at least 8 characters'
  } else if (!/[A-Z]/.test(formData.password)) {
    validationErrors.value.password = 'Password must contain at least one uppercase letter'
  } else if (!/[a-z]/.test(formData.password)) {
    validationErrors.value.password = 'Password must contain at least one lowercase letter'
  } else if (!/[0-9]/.test(formData.password)) {
    validationErrors.value.password = 'Password must contain at least one number'
  }

  // First name validation
  if (!formData.firstName) {
    validationErrors.value.firstName = 'First name is required'
  } else if (formData.firstName.length > 100) {
    validationErrors.value.firstName = 'First name must not exceed 100 characters'
  }

  // Last name validation
  if (!formData.lastName) {
    validationErrors.value.lastName = 'Last name is required'
  } else if (formData.lastName.length > 100) {
    validationErrors.value.lastName = 'Last name must not exceed 100 characters'
  }

  // Phone validation (optional)
  if (formData.phone && formData.phone.length > 20) {
    validationErrors.value.phone = 'Phone number must not exceed 20 characters'
  }

  return Object.keys(validationErrors.value).length === 0
}

const handleRegister = async () => {
  if (!validate()) return

  const result = await register({
    ...formData,
    phone: formData.phone || null // Convert empty string to null
  })

  if (result) {
    successMessage.value = 'Registration successful! Redirecting to login...'

    // Redirect to login page after 2 seconds
    setTimeout(() => {
      router.push('/login')
    }, 2000)
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-4">
    <Card class="w-full max-w-md">
      <template #title>
        <h1 class="text-2xl font-bold text-gray-900">Register for ABUVI</h1>
      </template>

      <template #content>
        <!-- Success message -->
        <Message v-if="successMessage" severity="success" :closable="false" class="mb-4">
          {{ successMessage }}
        </Message>

        <!-- Error message -->
        <Message v-if="error" severity="error" :closable="false" class="mb-4">
          {{ error }}
        </Message>

        <form @submit.prevent="handleRegister" class="flex flex-col gap-4">
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
              placeholder="Min 8 chars, uppercase, lowercase, number"
              class="w-full"
              :invalid="!!validationErrors.password"
              :disabled="loading"
            />
            <small v-if="validationErrors.password" class="text-red-500">
              {{ validationErrors.password }}
            </small>
          </div>

          <!-- First name field -->
          <div>
            <label for="firstName" class="mb-2 block text-sm font-medium text-gray-700">
              First Name *
            </label>
            <InputText
              id="firstName"
              v-model="formData.firstName"
              placeholder="John"
              class="w-full"
              :invalid="!!validationErrors.firstName"
              :disabled="loading"
            />
            <small v-if="validationErrors.firstName" class="text-red-500">
              {{ validationErrors.firstName }}
            </small>
          </div>

          <!-- Last name field -->
          <div>
            <label for="lastName" class="mb-2 block text-sm font-medium text-gray-700">
              Last Name *
            </label>
            <InputText
              id="lastName"
              v-model="formData.lastName"
              placeholder="Doe"
              class="w-full"
              :invalid="!!validationErrors.lastName"
              :disabled="loading"
            />
            <small v-if="validationErrors.lastName" class="text-red-500">
              {{ validationErrors.lastName }}
            </small>
          </div>

          <!-- Phone field (optional) -->
          <div>
            <label for="phone" class="mb-2 block text-sm font-medium text-gray-700">
              Phone (optional)
            </label>
            <InputText
              id="phone"
              v-model="formData.phone"
              placeholder="+34 123 456 789"
              class="w-full"
              :invalid="!!validationErrors.phone"
              :disabled="loading"
            />
            <small v-if="validationErrors.phone" class="text-red-500">
              {{ validationErrors.phone }}
            </small>
          </div>

          <!-- Submit button -->
          <Button
            type="submit"
            label="Register"
            :loading="loading"
            :disabled="loading || !!successMessage"
            class="w-full"
          />
        </form>

        <!-- Login link -->
        <div class="mt-4 text-center">
          <p class="text-sm text-gray-600">
            Already have an account?
            <router-link to="/login" class="font-medium text-primary-600 hover:text-primary-500">
              Login here
            </router-link>
          </p>
        </div>
      </template>
    </Card>
  </div>
</template>

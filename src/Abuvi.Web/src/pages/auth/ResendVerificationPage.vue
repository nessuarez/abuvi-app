<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Card from 'primevue/card'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useAuth } from '@/composables/useAuth'

const router = useRouter()
const { loading, error, resendVerification } = useAuth()

const email = ref('')
const emailError = ref<string | null>(null)
const showSuccess = ref(false)

const validateEmail = (): boolean => {
  emailError.value = null

  if (!email.value.trim()) {
    emailError.value = 'Email is required'
    return false
  }

  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  if (!emailRegex.test(email.value)) {
    emailError.value = 'Please enter a valid email address'
    return false
  }

  return true
}

const handleSubmit = async () => {
  if (!validateEmail()) return

  const success = await resendVerification({ email: email.value })

  if (success) {
    showSuccess.value = true
  }
}

const goToLogin = () => {
  router.push('/login')
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-4">
    <Card class="w-full max-w-md">
      <template #header>
        <div class="border-b p-6">
          <h1 class="text-2xl font-bold text-gray-900">Resend Verification Email</h1>
          <p class="mt-1 text-sm text-gray-600">
            Enter your email address to receive a new verification link
          </p>
        </div>
      </template>

      <template #content>
        <!-- Success Message -->
        <div v-if="showSuccess" class="space-y-4">
          <Message severity="success" :closable="false">
            <div class="flex flex-col gap-2">
              <p class="font-semibold">Verification email sent!</p>
              <p>
                Please check your inbox at <strong>{{ email }}</strong> for the
                verification link.
              </p>
            </div>
          </Message>

          <div class="flex flex-col gap-2 rounded-md border border-gray-200 bg-gray-50 p-4">
            <p class="text-sm text-gray-700">Already verified your email?</p>
            <button
              type="button"
              class="text-sm text-primary-600 hover:underline"
              @click="goToLogin"
            >
              Go to login
            </button>
          </div>
        </div>

        <!-- Resend Form -->
        <form v-else class="space-y-4" @submit.prevent="handleSubmit">
          <!-- API Error Message -->
          <Message
            v-if="error"
            severity="error"
            :closable="false"
          >
            {{ error }}
          </Message>

          <!-- Email Input -->
          <div>
            <label for="email" class="mb-1 block text-sm font-medium">
              Email Address *
            </label>
            <InputText
              id="email"
              v-model="email"
              type="email"
              placeholder="your.email@example.com"
              class="w-full"
              :invalid="!!emailError"
            />
            <small v-if="emailError" class="text-red-500">
              {{ emailError }}
            </small>
          </div>

          <!-- Submit Button -->
          <Button
            type="submit"
            label="Send Verification Email"
            class="w-full"
            :loading="loading"
            :disabled="loading || !email.trim()"
          />

          <!-- Link to Login -->
          <div class="border-t pt-4 text-center">
            <p class="text-sm text-gray-600">
              Remember your password?
              <router-link to="/login" class="text-primary-600 hover:underline">
                Log in
              </router-link>
            </p>
          </div>
        </form>
      </template>
    </Card>
  </div>
</template>

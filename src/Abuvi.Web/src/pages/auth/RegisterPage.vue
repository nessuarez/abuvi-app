<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Card from 'primevue/card'
import Message from 'primevue/message'
import RegistrationForm from '@/components/auth/RegistrationForm.vue'
import { useAuth } from '@/composables/useAuth'
import type { RegisterUserRequest } from '@/types/auth'

const router = useRouter()
const { loading, error, registerUser } = useAuth()

const showSuccessMessage = ref(false)
const registeredEmail = ref('')

const handleSubmit = async (data: RegisterUserRequest) => {
  const user = await registerUser(data)

  if (user) {
    registeredEmail.value = data.email
    showSuccessMessage.value = true
  }
}

const handleCancel = () => {
  router.push('/')
}

const goToResendVerification = () => {
  router.push('/resend-verification')
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-4">
    <Card class="w-full max-w-md">
      <template #header>
        <div class="border-b p-6">
          <h1 class="text-2xl font-bold text-gray-900">Create Account</h1>
          <p class="mt-1 text-sm text-gray-600">
            Join ABUVI to access camps and manage your registrations
          </p>
        </div>
      </template>

      <template #content>
        <!-- Success Message -->
        <div v-if="showSuccessMessage" class="space-y-4">
          <Message severity="success" :closable="false" data-testid="success-message">
            <div class="flex flex-col gap-2">
              <p class="font-semibold">Registration successful!</p>
              <p>
                Please check your email <strong>{{ registeredEmail }}</strong> to
                verify your account.
              </p>
            </div>
          </Message>

          <div class="flex flex-col gap-2 rounded-md border border-gray-200 bg-gray-50 p-4">
            <p class="text-sm text-gray-700">Didn't receive the email?</p>
            <button
              type="button"
              class="text-sm text-primary-600 hover:underline"
              @click="goToResendVerification"
            >
              Request a new verification email
            </button>
          </div>
        </div>

        <!-- Registration Form -->
        <div v-else>
          <!-- API Error Message -->
          <Message
            v-if="error"
            severity="error"
            :closable="false"
            class="mb-4"
            data-testid="error-message"
          >
            {{ error }}
          </Message>

          <RegistrationForm :loading="loading" @submit="handleSubmit" @cancel="handleCancel" />

          <!-- Link to Login -->
          <div class="mt-6 border-t pt-4 text-center">
            <p class="text-sm text-gray-600">
              Already have an account?
              <router-link to="/login" class="text-primary-600 hover:underline">
                Log in
              </router-link>
            </p>
          </div>
        </div>
      </template>
    </Card>
  </div>
</template>

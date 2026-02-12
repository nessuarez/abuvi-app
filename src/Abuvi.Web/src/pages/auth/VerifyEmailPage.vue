<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import Card from 'primevue/card'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import Button from 'primevue/button'
import { useAuth } from '@/composables/useAuth'

type VerificationStatus = 'verifying' | 'success' | 'error'

const router = useRouter()
const route = useRoute()
const { loading, error, verifyEmail } = useAuth()

const verificationStatus = ref<VerificationStatus>('verifying')

onMounted(async () => {
  const token = route.query.token as string

  if (!token) {
    verificationStatus.value = 'error'
    error.value = 'Invalid verification link. Please check your email for the correct link.'
    return
  }

  const success = await verifyEmail({ token })

  if (success) {
    verificationStatus.value = 'success'
  } else {
    verificationStatus.value = 'error'
  }
})

const goToLogin = () => {
  router.push('/login')
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
          <h1 class="text-2xl font-bold text-gray-900">Email Verification</h1>
        </div>
      </template>

      <template #content>
        <!-- Verifying State -->
        <div
          v-if="verificationStatus === 'verifying'"
          class="flex flex-col items-center gap-4 py-8"
        >
          <ProgressSpinner />
          <p class="text-gray-600">Verifying your email...</p>
        </div>

        <!-- Success State -->
        <div v-else-if="verificationStatus === 'success'" class="space-y-4">
          <Message severity="success" :closable="false">
            <div class="flex flex-col gap-2">
              <p class="font-semibold">Email verified successfully!</p>
              <p>Your account is now active. You can log in to access your account.</p>
            </div>
          </Message>

          <div class="flex justify-center">
            <Button label="Go to Login" @click="goToLogin" />
          </div>
        </div>

        <!-- Error State -->
        <div v-else class="space-y-4">
          <Message severity="error" :closable="false">
            <div class="flex flex-col gap-2">
              <p class="font-semibold">Verification failed</p>
              <p>{{ error }}</p>
            </div>
          </Message>

          <div class="flex flex-col gap-2 rounded-md border border-gray-200 bg-gray-50 p-4">
            <p class="text-sm text-gray-700">
              If your verification link has expired, you can request a new one:
            </p>
            <button
              type="button"
              class="text-sm text-primary-600 hover:underline"
              @click="goToResendVerification"
            >
              Request a new verification email
            </button>
          </div>
        </div>
      </template>
    </Card>
  </div>
</template>

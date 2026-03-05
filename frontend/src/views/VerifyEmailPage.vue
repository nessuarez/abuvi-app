<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { api } from '@/utils/api'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import landingBackground from '@/assets/images/landing-background.png'

type VerificationStatus = 'verifying' | 'success' | 'error'

const route = useRoute()
const status = ref<VerificationStatus>('verifying')
const errorMessage = ref('')

const mapErrorCodeToMessage = (errorCode?: string): string => {
  switch (errorCode) {
    case 'NOT_FOUND':
      return 'Enlace de verificación inválido.'
    case 'VERIFICATION_FAILED':
      return 'El enlace de verificación ha expirado. Por favor, solicita uno nuevo.'
    default:
      return 'Ha ocurrido un error. Por favor, inténtalo de nuevo.'
  }
}

onMounted(async () => {
  const token = route.query.token

  if (!token || typeof token !== 'string') {
    status.value = 'error'
    errorMessage.value = 'Enlace de verificación inválido. Revisa tu correo electrónico.'
    return
  }

  try {
    await api.post('/auth/verify-email', { token })
    status.value = 'success'
  } catch (err: any) {
    status.value = 'error'
    const errorCode = err.response?.data?.error?.code
    errorMessage.value = mapErrorCodeToMessage(errorCode)
  }
})
</script>

<template>
  <div class="relative flex min-h-screen items-center justify-center">
    <!-- Blurred background image -->
    <div
      class="absolute inset-0 bg-cover bg-center bg-no-repeat"
      :style="{
        backgroundImage: `url(${landingBackground})`,
        filter: 'blur(8px)',
        transform: 'scale(1.1)'
      }"
    />
    <!-- Dark overlay -->
    <div class="absolute inset-0 bg-black/40" />

    <!-- Card -->
    <div class="relative z-10 w-full max-w-md px-4">
      <div class="rounded-xl bg-white/90 p-8 shadow-2xl backdrop-blur-sm">
        <div class="mb-6 text-center">
          <h1 class="text-2xl font-bold text-gray-900">Verificación de Email</h1>
        </div>

        <!-- Verifying state -->
        <div v-if="status === 'verifying'" class="flex flex-col items-center gap-4 py-8">
          <ProgressSpinner />
          <p class="text-sm text-gray-600">Verificando tu correo electrónico...</p>
        </div>

        <!-- Success state -->
        <div v-else-if="status === 'success'" class="flex flex-col gap-4">
          <Message severity="success" :closable="false">
            <div class="flex flex-col gap-1">
              <p class="font-semibold">¡Email verificado correctamente!</p>
              <p>Tu cuenta está activa. Ya puedes iniciar sesión.</p>
            </div>
          </Message>
          <RouterLink
            to="/"
            class="text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
          >
            Ir al inicio de sesión
          </RouterLink>
        </div>

        <!-- Error state -->
        <div v-else class="flex flex-col gap-4">
          <Message severity="error" :closable="false">
            {{ errorMessage }}
          </Message>
          <RouterLink
            to="/"
            class="text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
          >
            Volver al inicio
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>

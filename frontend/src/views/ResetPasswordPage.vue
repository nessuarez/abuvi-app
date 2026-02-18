<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { usePasswordReset } from '@/composables/usePasswordReset'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'

const route = useRoute()
const router = useRouter()
const { loading, error, resetPassword } = usePasswordReset()

const token = ref<string | null>(null)
const tokenMissing = ref(false)
const successState = ref(false)

const formData = reactive({
  newPassword: '',
  confirmPassword: ''
})
const fieldErrors = ref<Record<string, string>>({})

onMounted(() => {
  const queryToken = route.query.token
  if (!queryToken || typeof queryToken !== 'string') {
    tokenMissing.value = true
    setTimeout(() => router.push('/'), 3000)
  } else {
    token.value = queryToken
  }
})

const validate = (): boolean => {
  fieldErrors.value = {}

  if (!formData.newPassword) {
    fieldErrors.value.newPassword = 'La nueva contraseña es obligatoria'
  } else if (formData.newPassword.length < 8) {
    fieldErrors.value.newPassword = 'La contraseña debe tener al menos 8 caracteres'
  }

  if (!formData.confirmPassword) {
    fieldErrors.value.confirmPassword = 'Debes confirmar la contraseña'
  } else if (formData.newPassword !== formData.confirmPassword) {
    fieldErrors.value.confirmPassword = 'Las contraseñas no coinciden'
  }

  return Object.keys(fieldErrors.value).length === 0
}

const handleSubmit = async () => {
  if (!token.value || !validate()) return

  const success = await resetPassword(token.value, formData.newPassword)
  if (success) {
    successState.value = true
  }
}
</script>

<template>
  <div class="relative flex min-h-screen items-center justify-center">
    <!-- Blurred background image -->
    <div
      class="absolute inset-0 bg-cover bg-center bg-no-repeat"
      style="
        background-image: url('/src/assets/images/landing-background.png');
        filter: blur(8px);
        transform: scale(1.1);
      "
    />
    <!-- Dark overlay -->
    <div class="absolute inset-0 bg-black/40" />

    <!-- Card -->
    <div class="relative z-10 w-full max-w-md px-4">
      <div class="rounded-xl bg-white/90 p-8 shadow-2xl backdrop-blur-sm">

        <!-- Missing token state -->
        <div v-if="tokenMissing" class="flex flex-col gap-4 text-center">
          <Message severity="error" :closable="false">
            El enlace de recuperación no es válido. Serás redirigido al inicio...
          </Message>
          <RouterLink to="/" class="text-sm text-primary-600 hover:text-primary-700 hover:underline">
            Volver al inicio de sesión
          </RouterLink>
        </div>

        <!-- Success state -->
        <div v-else-if="successState" class="flex flex-col gap-4">
          <Message severity="success" :closable="false">
            Tu contraseña ha sido restablecida exitosamente.
          </Message>
          <RouterLink
            to="/"
            class="text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
            data-testid="login-link"
          >
            Iniciar Sesión
          </RouterLink>
        </div>

        <!-- Form state -->
        <div v-else>
          <div class="mb-6 text-center">
            <h1 class="text-2xl font-bold text-gray-900">Nueva Contraseña</h1>
            <p class="mt-1 text-sm text-gray-600">Introduce tu nueva contraseña.</p>
          </div>

          <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
            <!-- Backend error (invalid/expired token) -->
            <Message v-if="error" severity="error" :closable="false" data-testid="error-message">
              {{ error }}
            </Message>

            <div class="flex flex-col gap-2">
              <label for="newPassword" class="text-sm font-medium text-gray-700">
                Nueva Contraseña *
              </label>
              <Password
                id="newPassword"
                v-model="formData.newPassword"
                toggle-mask
                :feedback="false"
                placeholder="Mínimo 8 caracteres"
                :invalid="!!fieldErrors.newPassword"
                :disabled="loading"
                input-class="w-full"
                data-testid="new-password-input"
              />
              <small v-if="fieldErrors.newPassword" class="text-red-500">
                {{ fieldErrors.newPassword }}
              </small>
            </div>

            <div class="flex flex-col gap-2">
              <label for="confirmPassword" class="text-sm font-medium text-gray-700">
                Confirmar Contraseña *
              </label>
              <Password
                id="confirmPassword"
                v-model="formData.confirmPassword"
                toggle-mask
                :feedback="false"
                placeholder="Repite la contraseña"
                :invalid="!!fieldErrors.confirmPassword"
                :disabled="loading"
                input-class="w-full"
                data-testid="confirm-password-input"
              />
              <small v-if="fieldErrors.confirmPassword" class="text-red-500">
                {{ fieldErrors.confirmPassword }}
              </small>
            </div>

            <Button
              type="submit"
              label="Restablecer Contraseña"
              :loading="loading"
              :disabled="loading"
              class="w-full"
              data-testid="submit-button"
            />
          </form>
        </div>

      </div>
    </div>
  </div>
</template>

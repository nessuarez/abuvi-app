<script setup lang="ts">
import { reactive, ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { usePasswordReset } from '@/composables/usePasswordReset'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'
import landingBackground from '@/assets/images/landing-background.png'

const route = useRoute()
const router = useRouter()
const { loading, error, fieldErrors: serverFieldErrors, resetPassword } = usePasswordReset()

const token = ref<string | null>(null)
const tokenMissing = ref(false)
const successState = ref(false)

const formData = reactive({
  newPassword: '',
  confirmPassword: ''
})
const clientFieldErrors = ref<Record<string, string>>({})

const clearPasswordErrorIfValid = () => {
  if (allCriteriaMet.value && clientFieldErrors.value.newPassword) {
    delete clientFieldErrors.value.newPassword
  }
  if (
    formData.confirmPassword &&
    formData.newPassword === formData.confirmPassword &&
    clientFieldErrors.value.confirmPassword
  ) {
    delete clientFieldErrors.value.confirmPassword
  }
}

const passwordCriteria = computed(() => ({
  hasMinLength: formData.newPassword.length >= 8,
  hasUppercase: /[A-Z]/.test(formData.newPassword),
  hasLowercase: /[a-z]/.test(formData.newPassword),
  hasDigit: /\d/.test(formData.newPassword),
  hasSpecialChar: /[@$!%*?&#]/.test(formData.newPassword)
}))

const allCriteriaMet = computed(() =>
  Object.values(passwordCriteria.value).every(Boolean)
)

watch(() => [formData.newPassword, formData.confirmPassword], clearPasswordErrorIfValid)

const mergedFieldErrors = computed(() => ({
  ...serverFieldErrors.value,
  ...clientFieldErrors.value
}))

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
  clientFieldErrors.value = {}

  if (!formData.newPassword) {
    clientFieldErrors.value.newPassword = 'La nueva contraseña es obligatoria'
  } else if (!allCriteriaMet.value) {
    clientFieldErrors.value.newPassword = 'La contraseña no cumple con los requisitos'
  }

  if (!formData.confirmPassword) {
    clientFieldErrors.value.confirmPassword = 'Debes confirmar la contraseña'
  } else if (formData.newPassword !== formData.confirmPassword) {
    clientFieldErrors.value.confirmPassword = 'Las contraseñas no coinciden'
  }

  return Object.keys(clientFieldErrors.value).length === 0
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
    <div class="absolute inset-0 bg-cover bg-center bg-no-repeat" :style="{
      backgroundImage: `url(${landingBackground})`,
      filter: 'blur(8px)',
      transform: 'scale(1.1)'
    }" />
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
          <RouterLink to="/" class="text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
            data-testid="login-link">
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
                placeholder="Introduce tu nueva contraseña"
                :invalid="!!mergedFieldErrors.newPassword"
                :disabled="loading"
                input-class="w-full"
                :input-props="{ autocomplete: 'new-password' }"
                data-testid="new-password-input"
              />
              <small v-if="mergedFieldErrors.newPassword" class="text-red-500">
                {{ mergedFieldErrors.newPassword }}
              </small>

              <!-- Password requirements checklist -->
              <ul v-if="formData.newPassword" class="mt-1 space-y-0.5 text-xs">
                <li :class="passwordCriteria.hasMinLength ? 'text-green-600' : 'text-gray-500'">
                  {{ passwordCriteria.hasMinLength ? '&#10003;' : '&#9675;' }} Mínimo 8 caracteres
                </li>
                <li :class="passwordCriteria.hasUppercase ? 'text-green-600' : 'text-gray-500'">
                  {{ passwordCriteria.hasUppercase ? '&#10003;' : '&#9675;' }} Una letra mayúscula (A-Z)
                </li>
                <li :class="passwordCriteria.hasLowercase ? 'text-green-600' : 'text-gray-500'">
                  {{ passwordCriteria.hasLowercase ? '&#10003;' : '&#9675;' }} Una letra minúscula (a-z)
                </li>
                <li :class="passwordCriteria.hasDigit ? 'text-green-600' : 'text-gray-500'">
                  {{ passwordCriteria.hasDigit ? '&#10003;' : '&#9675;' }} Un dígito (0-9)
                </li>
                <li :class="passwordCriteria.hasSpecialChar ? 'text-green-600' : 'text-gray-500'">
                  {{ passwordCriteria.hasSpecialChar ? '&#10003;' : '&#9675;' }} Un carácter especial (@ $ ! % * ? & #)
                </li>
              </ul>
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
                :invalid="!!mergedFieldErrors.confirmPassword"
                :disabled="loading"
                input-class="w-full"
                :input-props="{ autocomplete: 'new-password' }"
                data-testid="confirm-password-input"
              />
              <small v-if="mergedFieldErrors.confirmPassword" class="text-red-500">
                {{ mergedFieldErrors.confirmPassword }}
              </small>
            </div>

            <Button type="submit" label="Restablecer Contraseña" :loading="loading" :disabled="loading" class="w-full"
              data-testid="submit-button" />
          </form>
        </div>

      </div>
    </div>
  </div>
</template>

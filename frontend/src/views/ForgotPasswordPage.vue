<script setup lang="ts">
import { reactive, ref } from 'vue'
import { usePasswordReset } from '@/composables/usePasswordReset'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'

const { loading, error, forgotPassword } = usePasswordReset()

const formData = reactive({ email: '' })
const fieldErrors = ref<Record<string, string>>({})
const submitted = ref(false)

const validate = (): boolean => {
  fieldErrors.value = {}

  if (!formData.email.trim()) {
    fieldErrors.value.email = 'El correo electrónico es obligatorio'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    fieldErrors.value.email = 'Formato de correo electrónico inválido'
  }

  return Object.keys(fieldErrors.value).length === 0
}

const handleSubmit = async () => {
  if (!validate()) return

  const success = await forgotPassword(formData.email)
  if (success) {
    submitted.value = true
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
        <!-- Header -->
        <div class="mb-6 text-center">
          <h1 class="text-2xl font-bold text-gray-900">Recuperar Contraseña</h1>
          <p class="mt-1 text-sm text-gray-600">
            Introduce tu correo y te enviaremos un enlace de recuperación.
          </p>
        </div>

        <!-- Success state -->
        <div v-if="submitted" class="flex flex-col gap-4">
          <Message severity="success" :closable="false">
            Si tu correo está registrado, recibirás un enlace para restablecer tu contraseña.
          </Message>
          <RouterLink
            to="/"
            class="mt-2 text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
          >
            Volver al inicio de sesión
          </RouterLink>
        </div>

        <!-- Form state -->
        <form v-else class="flex flex-col gap-4" @submit.prevent="handleSubmit">
          <!-- Network error -->
          <Message v-if="error" severity="error" :closable="false">
            {{ error }}
          </Message>

          <div class="flex flex-col gap-2">
            <label for="email" class="text-sm font-medium text-gray-700">
              Correo Electrónico *
            </label>
            <InputText
              id="email"
              v-model="formData.email"
              type="email"
              placeholder="tu@email.com"
              :invalid="!!fieldErrors.email"
              :disabled="loading"
              data-testid="email-input"
            />
            <small v-if="fieldErrors.email" class="text-red-500">
              {{ fieldErrors.email }}
            </small>
          </div>

          <Button
            type="submit"
            label="Enviar enlace de recuperación"
            :loading="loading"
            :disabled="loading"
            class="w-full"
            data-testid="submit-button"
          />

          <RouterLink
            to="/"
            class="text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
          >
            Volver al inicio de sesión
          </RouterLink>
        </form>
      </div>
    </div>
  </div>
</template>

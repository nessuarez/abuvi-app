<script setup lang="ts">
import { reactive, ref } from 'vue'
import { api } from '@/utils/api'
import { useAuthStore } from '@/stores/auth'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'
import Message from 'primevue/message'

const emit = defineEmits<{
  'go-to-login': []
}>()

const auth = useAuthStore()

const formData = reactive({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  confirmPassword: '',
  acceptTerms: false
})

const errors = ref<Record<string, string>>({})
const submitting = ref(false)
const errorMessage = ref('')
const registrationComplete = ref(false)
const registeredEmail = ref('')
const resending = ref(false)
const resendSuccess = ref(false)

const PASSWORD_REGEX = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])/

const validate = (): boolean => {
  errors.value = {}

  if (!formData.firstName.trim()) {
    errors.value.firstName = 'El nombre es obligatorio'
  }

  if (!formData.lastName.trim()) {
    errors.value.lastName = 'Los apellidos son obligatorios'
  }

  if (!formData.email.trim()) {
    errors.value.email = 'El correo electrónico es obligatorio'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Formato de correo electrónico inválido'
  }

  if (!formData.password) {
    errors.value.password = 'La contraseña es obligatoria'
  } else if (formData.password.length < 8) {
    errors.value.password = 'La contraseña debe tener al menos 8 caracteres'
  } else if (!PASSWORD_REGEX.test(formData.password)) {
    errors.value.password =
      'La contraseña debe incluir mayúscula, minúscula, número y carácter especial (@$!%*?&#)'
  }

  if (formData.password !== formData.confirmPassword) {
    errors.value.confirmPassword = 'Las contraseñas no coinciden'
  }

  if (!formData.acceptTerms) {
    errors.value.acceptTerms = 'Debes aceptar los términos y condiciones'
  }

  return Object.keys(errors.value).length === 0
}

const resetForm = () => {
  formData.firstName = ''
  formData.lastName = ''
  formData.email = ''
  formData.password = ''
  formData.confirmPassword = ''
  formData.acceptTerms = false
}

const handleSubmit = async () => {
  if (!validate()) return

  errorMessage.value = ''
  submitting.value = true

  try {
    const result = await auth.register({
      email: formData.email,
      password: formData.password,
      firstName: formData.firstName,
      lastName: formData.lastName,
      acceptedTerms: formData.acceptTerms
    })

    if (result.success) {
      registeredEmail.value = result.email || formData.email
      resetForm()
      registrationComplete.value = true
    } else {
      errorMessage.value = result.error || 'Error al registrarse'
    }
  } finally {
    submitting.value = false
  }
}

const handleResendVerification = async () => {
  resending.value = true
  resendSuccess.value = false
  try {
    await api.post('/auth/resend-verification', { email: registeredEmail.value })
    resendSuccess.value = true
  } catch {
    // Silently handle — user can retry
  } finally {
    resending.value = false
  }
}

const handleGoToLogin = () => {
  registrationComplete.value = false
  emit('go-to-login')
}
</script>

<template>
  <!-- Success state -->
  <div v-if="registrationComplete" class="flex flex-col items-center gap-4 py-4 text-center" role="alert">
    <i class="pi pi-check-circle text-5xl text-green-500" />
    <h2 class="text-xl font-bold text-gray-900">¡Registro completado!</h2>
    <p class="text-sm text-gray-600">
      Hemos enviado un email de verificación a
      <strong class="text-gray-900">{{ registeredEmail }}</strong>.
      Revisa tu bandeja de entrada (y la carpeta de spam) para confirmar tu cuenta.
    </p>

    <Button
      label="Ir al inicio de sesión"
      class="w-full"
      @click="handleGoToLogin"
    />

    <button
      type="button"
      class="text-sm text-primary-600 hover:text-primary-700 hover:underline"
      :disabled="resending"
      @click="handleResendVerification"
    >
      {{ resending ? 'Reenviando...' : '¿No recibiste el email? Reenviar' }}
    </button>

    <Message v-if="resendSuccess" severity="success" :closable="false" class="w-full">
      Email de verificación reenviado correctamente.
    </Message>
  </div>

  <!-- Registration form -->
  <form v-else class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <Message v-if="errorMessage" severity="error" :closable="false">
      {{ errorMessage }}
    </Message>

    <div class="grid grid-cols-2 gap-4">
      <div class="flex flex-col gap-2">
        <label for="firstName" class="text-sm font-medium text-gray-700">Nombre *</label>
        <InputText
          id="firstName"
          v-model="formData.firstName"
          placeholder="Juan"
          :invalid="!!errors.firstName"
          :disabled="submitting"
        />
        <small v-if="errors.firstName" class="text-red-500">{{ errors.firstName }}</small>
      </div>

      <div class="flex flex-col gap-2">
        <label for="lastName" class="text-sm font-medium text-gray-700">Apellidos *</label>
        <InputText
          id="lastName"
          v-model="formData.lastName"
          placeholder="García"
          :invalid="!!errors.lastName"
          :disabled="submitting"
        />
        <small v-if="errors.lastName" class="text-red-500">{{ errors.lastName }}</small>
      </div>
    </div>

    <div class="flex flex-col gap-2">
      <label for="registerEmail" class="text-sm font-medium text-gray-700">Correo Electrónico *</label>
      <InputText
        id="registerEmail"
        v-model="formData.email"
        type="email"
        placeholder="tu@email.com"
        :invalid="!!errors.email"
        :disabled="submitting"
      />
      <small v-if="errors.email" class="text-red-500">{{ errors.email }}</small>
    </div>

    <div class="flex flex-col gap-2">
      <label for="registerPassword" class="text-sm font-medium text-gray-700">Contraseña *</label>
      <Password
        id="registerPassword"
        v-model="formData.password"
        toggle-mask
        :feedback="true"
        placeholder="••••••••"
        :invalid="!!errors.password"
        :disabled="submitting"
        input-class="w-full"
      />
      <small v-if="errors.password" class="text-red-500">{{ errors.password }}</small>
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
        placeholder="••••••••"
        :invalid="!!errors.confirmPassword"
        :disabled="submitting"
        input-class="w-full"
      />
      <small v-if="errors.confirmPassword" class="text-red-500">
        {{ errors.confirmPassword }}
      </small>
    </div>

    <div class="flex flex-col gap-2">
      <div class="flex items-start gap-2">
        <Checkbox
          id="acceptTerms"
          v-model="formData.acceptTerms"
          :binary="true"
          :invalid="!!errors.acceptTerms"
          :disabled="submitting"
        />
        <label for="acceptTerms" class="text-sm text-gray-700">
          Acepto los
          <router-link
            :to="{ name: 'legal-privacy' }"
            target="_blank"
            rel="noopener noreferrer"
            class="font-semibold underline text-primary-600 hover:text-primary-700"
          >
            términos y condiciones
          </router-link>
        </label>
      </div>
      <small v-if="errors.acceptTerms" class="text-red-500">{{ errors.acceptTerms }}</small>
    </div>

    <Button
      type="submit"
      label="Registrarse"
      :loading="submitting"
      :disabled="submitting"
      class="w-full"
    />
  </form>
</template>

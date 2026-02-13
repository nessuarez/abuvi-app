<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'
import Message from 'primevue/message'

const router = useRouter()
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

const passwordStrength = computed(() => {
  const pwd = formData.password
  if (pwd.length < 6) return 'weak'
  if (pwd.length < 10) return 'medium'
  return 'strong'
})

const validate = (): boolean => {
  errors.value = {}

  if (!formData.firstName.trim()) {
    errors.value.firstName = 'First name is required'
  }

  if (!formData.lastName.trim()) {
    errors.value.lastName = 'Last name is required'
  }

  if (!formData.email.trim()) {
    errors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Invalid email format'
  }

  if (!formData.password) {
    errors.value.password = 'Password is required'
  } else if (formData.password.length < 6) {
    errors.value.password = 'Password must be at least 6 characters'
  }

  if (formData.password !== formData.confirmPassword) {
    errors.value.confirmPassword = 'Passwords do not match'
  }

  if (!formData.acceptTerms) {
    errors.value.acceptTerms = 'You must accept the terms and conditions'
  }

  return Object.keys(errors.value).length === 0
}

const handleSubmit = async () => {
  if (!validate()) return

  errorMessage.value = ''
  submitting.value = true

  try {
    const result = await auth.register(formData)

    if (result.success) {
      router.push('/home')
    } else {
      errorMessage.value = result.error || 'Registration failed'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
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
      <label for="registerEmail" class="text-sm font-medium text-gray-700">Email *</label>
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
          <a href="#" class="text-primary-600 hover:text-primary-700">
            términos y condiciones
          </a>
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

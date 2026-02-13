<script setup lang="ts">
import { reactive, ref } from 'vue'
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
  email: '',
  password: '',
  rememberMe: false
})

const errors = ref<Record<string, string>>({})
const submitting = ref(false)
const errorMessage = ref('')

const validate = (): boolean => {
  errors.value = {}

  if (!formData.email.trim()) {
    errors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Invalid email format'
  }

  if (!formData.password) {
    errors.value.password = 'Password is required'
  }

  return Object.keys(errors.value).length === 0
}

const handleSubmit = async () => {
  if (!validate()) return

  errorMessage.value = ''
  submitting.value = true

  try {
    const result = await auth.login(formData)

    if (result.success) {
      const redirect = router.currentRoute.value.query.redirect as string | undefined
      router.push(redirect || '/home')
    } else {
      errorMessage.value = result.error || 'Login failed'
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

    <div class="flex flex-col gap-2">
      <label for="email" class="text-sm font-medium text-gray-700">Email *</label>
      <InputText
        id="email"
        v-model="formData.email"
        type="email"
        placeholder="tu@email.com"
        :invalid="!!errors.email"
        :disabled="submitting"
      />
      <small v-if="errors.email" class="text-red-500">{{ errors.email }}</small>
    </div>

    <div class="flex flex-col gap-2">
      <label for="password" class="text-sm font-medium text-gray-700">Contraseña *</label>
      <Password
        id="password"
        v-model="formData.password"
        toggle-mask
        :feedback="false"
        placeholder="••••••••"
        :invalid="!!errors.password"
        :disabled="submitting"
        input-class="w-full"
      />
      <small v-if="errors.password" class="text-red-500">{{ errors.password }}</small>
    </div>

    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <Checkbox
          id="rememberMe"
          v-model="formData.rememberMe"
          :binary="true"
          :disabled="submitting"
        />
        <label for="rememberMe" class="text-sm text-gray-700">Recordarme</label>
      </div>
      <a href="#" class="text-sm text-primary-600 hover:text-primary-700">
        ¿Olvidaste tu contraseña?
      </a>
    </div>

    <Button
      type="submit"
      label="Iniciar Sesión"
      :loading="submitting"
      :disabled="submitting"
      class="w-full"
    />
  </form>
</template>

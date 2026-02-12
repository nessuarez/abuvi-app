<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'
import PasswordStrengthMeter from './PasswordStrengthMeter.vue'
import type { RegisterUserRequest } from '@/types/auth'

interface Props {
  loading?: boolean
}

interface Emits {
  (e: 'submit', data: RegisterUserRequest): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  loading: false
})

const emit = defineEmits<Emits>()

const formData = reactive<RegisterUserRequest>({
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  documentNumber: '',
  phone: '',
  acceptedTerms: false
})

const errors = ref<Record<string, string>>({})

const validateEmail = (email: string): string | null => {
  if (!email.trim()) return 'Email is required'
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  if (!emailRegex.test(email)) return 'Please enter a valid email address'
  if (email.length > 255) return 'Email must not exceed 255 characters'
  return null
}

const validatePassword = (password: string): string | null => {
  if (!password) return 'Password is required'
  if (password.length < 8) return 'Password must be at least 8 characters'

  const hasUppercase = /[A-Z]/.test(password)
  const hasLowercase = /[a-z]/.test(password)
  const hasDigit = /\d/.test(password)
  const hasSpecialChar = /[@$!%*?&#]/.test(password)

  if (!hasUppercase || !hasLowercase || !hasDigit || !hasSpecialChar) {
    return 'Password must meet all strength requirements'
  }

  return null
}

const validateName = (name: string, fieldName: string): string | null => {
  if (!name.trim()) return `${fieldName} is required`
  if (name.length > 100) return `${fieldName} must not exceed 100 characters`
  return null
}

const validateDocumentNumber = (documentNumber: string): string | null => {
  if (!documentNumber.trim()) return null // Optional field

  const uppercaseAlphanumeric = /^[A-Z0-9]+$/
  if (!uppercaseAlphanumeric.test(documentNumber)) {
    return 'Document number must contain only uppercase letters and numbers'
  }

  if (documentNumber.length > 50) {
    return 'Document number must not exceed 50 characters'
  }

  return null
}

const validatePhone = (phone: string): string | null => {
  if (!phone.trim()) return null // Optional field

  const e164Regex = /^\+?[1-9]\d{1,14}$/
  if (!e164Regex.test(phone)) {
    return 'Please enter a valid phone number (e.g., +34612345678)'
  }

  return null
}

const validate = (): boolean => {
  errors.value = {}

  const emailError = validateEmail(formData.email)
  if (emailError) errors.value.email = emailError

  const passwordError = validatePassword(formData.password)
  if (passwordError) errors.value.password = passwordError

  const firstNameError = validateName(formData.firstName, 'First name')
  if (firstNameError) errors.value.firstName = firstNameError

  const lastNameError = validateName(formData.lastName, 'Last name')
  if (lastNameError) errors.value.lastName = lastNameError

  const documentError = validateDocumentNumber(formData.documentNumber || '')
  if (documentError) errors.value.documentNumber = documentError

  const phoneError = validatePhone(formData.phone || '')
  if (phoneError) errors.value.phone = phoneError

  if (!formData.acceptedTerms) {
    errors.value.acceptedTerms = 'You must accept the terms and conditions'
  }

  return Object.keys(errors.value).length === 0
}

const isFormValid = computed(() => {
  return (
    formData.email.trim() !== '' &&
    formData.password.trim() !== '' &&
    formData.firstName.trim() !== '' &&
    formData.lastName.trim() !== '' &&
    formData.acceptedTerms
  )
})

const handleSubmit = () => {
  if (!validate()) return

  // Clean up optional fields if empty
  const submitData: RegisterUserRequest = {
    ...formData,
    documentNumber: formData.documentNumber?.trim() || undefined,
    phone: formData.phone?.trim() || undefined
  }

  emit('submit', submitData)
}

const handleCancel = () => {
  emit('cancel')
}
</script>

<template>
  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <!-- Email Field -->
    <div>
      <label for="email" class="mb-1 block text-sm font-medium">Email *</label>
      <InputText
        id="email"
        v-model="formData.email"
        type="email"
        placeholder="your.email@example.com"
        class="w-full"
        :invalid="!!errors.email"
        data-testid="email-input"
      />
      <small v-if="errors.email" class="text-red-500" data-testid="email-error">
        {{ errors.email }}
      </small>
    </div>

    <!-- Password Field -->
    <div>
      <label for="password" class="mb-1 block text-sm font-medium">
        Password *
      </label>
      <Password
        id="password"
        v-model="formData.password"
        placeholder="Enter a strong password"
        class="w-full"
        :invalid="!!errors.password"
        :feedback="false"
        toggle-mask
        data-testid="password-input"
      />
      <small
        v-if="errors.password"
        class="text-red-500"
        data-testid="password-error"
      >
        {{ errors.password }}
      </small>
      <PasswordStrengthMeter :password="formData.password" />
    </div>

    <!-- First Name and Last Name -->
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div>
        <label for="firstName" class="mb-1 block text-sm font-medium">
          First Name *
        </label>
        <InputText
          id="firstName"
          v-model="formData.firstName"
          placeholder="John"
          class="w-full"
          :invalid="!!errors.firstName"
          data-testid="firstName-input"
        />
        <small
          v-if="errors.firstName"
          class="text-red-500"
          data-testid="firstName-error"
        >
          {{ errors.firstName }}
        </small>
      </div>

      <div>
        <label for="lastName" class="mb-1 block text-sm font-medium">
          Last Name *
        </label>
        <InputText
          id="lastName"
          v-model="formData.lastName"
          placeholder="Doe"
          class="w-full"
          :invalid="!!errors.lastName"
          data-testid="lastName-input"
        />
        <small
          v-if="errors.lastName"
          class="text-red-500"
          data-testid="lastName-error"
        >
          {{ errors.lastName }}
        </small>
      </div>
    </div>

    <!-- Document Number (Optional) -->
    <div>
      <label for="documentNumber" class="mb-1 block text-sm font-medium">
        Document Number
      </label>
      <InputText
        id="documentNumber"
        v-model="formData.documentNumber"
        placeholder="12345678A"
        class="w-full"
        :invalid="!!errors.documentNumber"
        data-testid="documentNumber-input"
      />
      <small
        v-if="errors.documentNumber"
        class="text-red-500"
        data-testid="documentNumber-error"
      >
        {{ errors.documentNumber }}
      </small>
      <small class="text-gray-500">
        Uppercase letters and numbers only
      </small>
    </div>

    <!-- Phone (Optional) -->
    <div>
      <label for="phone" class="mb-1 block text-sm font-medium">Phone</label>
      <InputText
        id="phone"
        v-model="formData.phone"
        type="tel"
        placeholder="+34612345678"
        class="w-full"
        :invalid="!!errors.phone"
        data-testid="phone-input"
      />
      <small v-if="errors.phone" class="text-red-500" data-testid="phone-error">
        {{ errors.phone }}
      </small>
      <small class="text-gray-500">E.164 format (e.g., +34612345678)</small>
    </div>

    <!-- Terms and Conditions -->
    <div class="flex items-start gap-2">
      <Checkbox
        id="acceptedTerms"
        v-model="formData.acceptedTerms"
        binary
        :invalid="!!errors.acceptedTerms"
        data-testid="terms-checkbox"
      />
      <label for="acceptedTerms" class="text-sm">
        I accept the
        <a href="#" class="text-primary-600 hover:underline">
          terms and conditions
        </a>
        *
      </label>
    </div>
    <small
      v-if="errors.acceptedTerms"
      class="text-red-500"
      data-testid="terms-error"
    >
      {{ errors.acceptedTerms }}
    </small>

    <!-- Action Buttons -->
    <div class="flex flex-col gap-2 sm:flex-row sm:justify-end">
      <Button
        type="button"
        label="Cancel"
        severity="secondary"
        outlined
        @click="handleCancel"
      />
      <Button
        type="submit"
        label="Register"
        :loading="props.loading"
        :disabled="props.loading || !isFormValid"
        data-testid="submit-button"
      />
    </div>
  </form>
</template>

<script setup lang="ts">
import { reactive, computed, watch } from 'vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import InputSwitch from 'primevue/inputswitch'
import Button from 'primevue/button'
import type { User, CreateUserRequest, UpdateUserRequest, UserRole } from '@/types/user'

interface Props {
  user?: User | null
  mode: 'create' | 'edit'
  loading?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  user: null,
  loading: false
})

const emit = defineEmits<{
  submit: [data: CreateUserRequest | UpdateUserRequest]
  cancel: []
}>()

const formData = reactive({
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  phone: '',
  role: 'Member' as UserRole,
  isActive: true
})

const errors = reactive<Record<string, string>>({})

const roleOptions = [
  { label: 'Member', value: 'Member' },
  { label: 'Board', value: 'Board' },
  { label: 'Admin', value: 'Admin' }
]

// Initialize form data in edit mode
watch(
  () => props.user,
  (user) => {
    if (user && props.mode === 'edit') {
      formData.email = user.email
      formData.firstName = user.firstName
      formData.lastName = user.lastName
      formData.phone = user.phone ?? ''
      formData.role = user.role
      formData.isActive = user.isActive
    }
  },
  { immediate: true }
)

const validate = (): boolean => {
  Object.keys(errors).forEach((key) => delete errors[key])

  if (props.mode === 'create') {
    if (!formData.email.trim()) {
      errors.email = 'Email is required'
    } else if (!formData.email.includes('@')) {
      errors.email = 'Email must be valid'
    }

    if (!formData.password.trim()) {
      errors.password = 'Password is required'
    } else if (formData.password.length < 8) {
      errors.password = 'Password must be at least 8 characters'
    }
  }

  if (!formData.firstName.trim()) {
    errors.firstName = 'First name is required'
  }

  if (!formData.lastName.trim()) {
    errors.lastName = 'Last name is required'
  }

  return Object.keys(errors).length === 0
}

const handleSubmit = () => {
  if (!validate()) return

  if (props.mode === 'create') {
    const request: CreateUserRequest = {
      email: formData.email.trim(),
      password: formData.password,
      firstName: formData.firstName.trim(),
      lastName: formData.lastName.trim(),
      phone: formData.phone.trim() || null,
      role: formData.role
    }
    emit('submit', request)
  } else {
    const request: UpdateUserRequest = {
      firstName: formData.firstName.trim(),
      lastName: formData.lastName.trim(),
      phone: formData.phone.trim() || null,
      isActive: formData.isActive
    }
    emit('submit', request)
  }
}

const handleCancel = () => {
  emit('cancel')
}

const isFormValid = computed(() => {
  if (props.mode === 'create') {
    return (
      formData.email.trim().length > 0 &&
      formData.password.length >= 8 &&
      formData.firstName.trim().length > 0 &&
      formData.lastName.trim().length > 0
    )
  } else {
    return formData.firstName.trim().length > 0 && formData.lastName.trim().length > 0
  }
})
</script>

<template>
  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <!-- Email field (create mode only) -->
    <div v-if="mode === 'create'">
      <label for="email" class="mb-2 block text-sm font-medium">Email *</label>
      <InputText
        id="email"
        v-model="formData.email"
        type="email"
        class="w-full"
        :invalid="!!errors.email"
        placeholder="user@example.com"
      />
      <small v-if="errors.email" class="text-red-500">{{ errors.email }}</small>
    </div>

    <!-- Email display (edit mode) -->
    <div v-else>
      <label class="mb-2 block text-sm font-medium">Email</label>
      <p class="text-gray-700">{{ user?.email }}</p>
    </div>

    <!-- Password field (create mode only) -->
    <div v-if="mode === 'create'">
      <label for="password" class="mb-2 block text-sm font-medium">Password *</label>
      <InputText
        id="password"
        v-model="formData.password"
        type="password"
        class="w-full"
        :invalid="!!errors.password"
        placeholder="Minimum 8 characters"
      />
      <small v-if="errors.password" class="text-red-500">{{ errors.password }}</small>
    </div>

    <!-- First Name -->
    <div>
      <label for="firstName" class="mb-2 block text-sm font-medium">First Name *</label>
      <InputText
        id="firstName"
        v-model="formData.firstName"
        class="w-full"
        :invalid="!!errors.firstName"
        placeholder="John"
      />
      <small v-if="errors.firstName" class="text-red-500">{{ errors.firstName }}</small>
    </div>

    <!-- Last Name -->
    <div>
      <label for="lastName" class="mb-2 block text-sm font-medium">Last Name *</label>
      <InputText
        id="lastName"
        v-model="formData.lastName"
        class="w-full"
        :invalid="!!errors.lastName"
        placeholder="Doe"
      />
      <small v-if="errors.lastName" class="text-red-500">{{ errors.lastName }}</small>
    </div>

    <!-- Phone -->
    <div>
      <label for="phone" class="mb-2 block text-sm font-medium">Phone (optional)</label>
      <InputText
        id="phone"
        v-model="formData.phone"
        type="tel"
        class="w-full"
        placeholder="+34 123 456 789"
      />
    </div>

    <!-- Role (create mode only) -->
    <div v-if="mode === 'create'">
      <label for="role" class="mb-2 block text-sm font-medium">Role *</label>
      <Select
        id="role"
        v-model="formData.role"
        :options="roleOptions"
        option-label="label"
        option-value="value"
        class="w-full"
        placeholder="Select a role"
      />
    </div>

    <!-- Active status (edit mode only) -->
    <div v-if="mode === 'edit'" class="flex items-center gap-3">
      <label for="isActive" class="text-sm font-medium">Active</label>
      <InputSwitch id="isActive" v-model="formData.isActive" />
    </div>

    <!-- Action buttons -->
    <div class="flex gap-3">
      <Button
        type="submit"
        :label="mode === 'create' ? 'Create User' : 'Update User'"
        :loading="loading"
        :disabled="!isFormValid || loading"
        class="flex-1"
      />
      <Button
        type="button"
        label="Cancel"
        severity="secondary"
        outlined
        @click="handleCancel"
        :disabled="loading"
        class="flex-1"
      />
    </div>
  </form>
</template>

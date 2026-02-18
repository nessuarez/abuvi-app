<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useUsers } from '@/composables/useUsers'
import { useAuthStore } from '@/stores/auth'
import type { User, UserRole, UpdateUserRoleRequest } from '@/types/user'
import { getRoleLabel } from '@/utils/user'

interface Props {
  visible: boolean
  user: User | null
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  roleUpdated: [user: User]
}>()

const auth = useAuthStore()
const { updateUserRole, loading, error, clearError } = useUsers()

// Form state
const newRole = ref<UserRole | null>(null)
const reason = ref('')

// Available roles based on current user's role
const availableRoles = computed(() => {
  const roles: { label: string; value: UserRole }[] = [
    { label: getRoleLabel('Member'), value: 'Member' },
    { label: getRoleLabel('Board'), value: 'Board' },
    { label: getRoleLabel('Admin'), value: 'Admin' }
  ]

  // Board members can only set Member role
  if (!auth.isAdmin) {
    return roles.filter((r) => r.value === 'Member')
  }

  return roles
})

// Check if user is trying to change their own role
const isSelfChange = computed(() => {
  return props.user?.id === auth.user?.id
})

// Validation
const canSubmit = computed(() => {
  return (
    !isSelfChange.value &&
    newRole.value !== null &&
    newRole.value !== props.user?.role &&
    !loading.value
  )
})

// Reset form when dialog opens/closes
watch(
  () => props.visible,
  (visible) => {
    if (visible && props.user) {
      newRole.value = props.user.role
      reason.value = ''
      clearError()
    }
  }
)

// Handle role update
const handleSubmit = async () => {
  if (!props.user || !newRole.value || !canSubmit.value) return

  const request: UpdateUserRoleRequest = {
    newRole: newRole.value,
    reason: reason.value.trim() || null
  }

  const updatedUser = await updateUserRole(props.user.id, request)

  if (updatedUser) {
    emit('roleUpdated', updatedUser)
    emit('update:visible', false)
  }
}

const handleCancel = () => {
  emit('update:visible', false)
}
</script>

<template>
  <Dialog
    :visible="visible"
    :header="`Actualizar rol: ${user?.firstName} ${user?.lastName}`"
    modal
    :closable="!loading"
    class="w-full max-w-md"
    data-testid="role-dialog"
    @update:visible="$emit('update:visible', $event)"
  >
    <div v-if="user" class="flex flex-col gap-4">
      <!-- Self-change warning -->
      <Message v-if="isSelfChange" severity="warn" :closable="false">
        No puedes cambiar tu propio rol
      </Message>

      <!-- Error message -->
      <Message
        v-if="error"
        severity="error"
        :closable="false"
        data-testid="error-message"
      >
        {{ error }}
      </Message>

      <!-- Current role display -->
      <div class="flex flex-col gap-2">
        <label class="text-sm font-medium text-gray-700">Rol actual</label>
        <div class="rounded-md bg-gray-100 px-3 py-2">
          <span
            class="inline-block rounded-full px-2 py-1 text-xs font-semibold"
            :class="{
              'bg-red-100 text-red-800': user.role === 'Admin',
              'bg-blue-100 text-blue-800': user.role === 'Board',
              'bg-gray-100 text-gray-800': user.role === 'Member'
            }"
          >
            {{ getRoleLabel(user.role) }}
          </span>
        </div>
      </div>

      <!-- New role dropdown -->
      <div class="flex flex-col gap-2">
        <label for="newRole" class="text-sm font-medium text-gray-700"
          >Nuevo rol *</label
        >
        <Select
          id="newRole"
          v-model="newRole"
          :options="availableRoles"
          option-label="label"
          option-value="value"
          placeholder="Seleccionar nuevo rol"
          :disabled="loading || isSelfChange"
          class="w-full"
          data-testid="role-dropdown"
        />
      </div>

      <!-- Reason textarea -->
      <div class="flex flex-col gap-2">
        <label for="reason" class="text-sm font-medium text-gray-700">
          Motivo (opcional)
        </label>
        <Textarea
          id="reason"
          v-model="reason"
          rows="3"
          placeholder="Indica el motivo del cambio de rol (para auditoría)"
          :disabled="loading || isSelfChange"
          class="w-full"
          maxlength="500"
          data-testid="reason-textarea"
        />
        <small class="text-gray-500">{{ reason.length }}/500 caracteres</small>
      </div>

      <!-- Action buttons -->
      <div class="flex justify-end gap-2 pt-4">
        <Button
          label="Cancelar"
          severity="secondary"
          text
          :disabled="loading"
          @click="handleCancel"
        />
        <Button
          label="Actualizar rol"
          :loading="loading"
          :disabled="!canSubmit"
          data-testid="submit-button"
          @click="handleSubmit"
        />
      </div>
    </div>
  </Dialog>
</template>

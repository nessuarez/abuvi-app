<script setup lang="ts">
import Button from 'primevue/button'
import { useAuthStore } from '@/stores/auth'
import type { User } from '@/types/user'

interface Props {
  user: User
}

const props = defineProps<Props>()

const emit = defineEmits<{
  editRole: [user: User]
}>()

const auth = useAuthStore()

// Show edit button only if:
// 1. Current user is Admin or Board
// 2. Not trying to edit own role
const canEditRole = (user: User): boolean => {
  if (!auth.isBoard) return false
  if (user.id === auth.user?.id) return false

  // Board members can only edit Member roles
  if (!auth.isAdmin && user.role !== 'Member') return false

  return true
}

const handleEditClick = () => {
  emit('editRole', props.user)
}
</script>

<template>
  <div class="flex items-center justify-between gap-2">
    <span
      class="inline-block rounded-full px-2 py-1 text-xs font-semibold"
      data-testid="role-badge"
      :class="{
        'bg-red-100 text-red-800': user.role === 'Admin',
        'bg-blue-100 text-blue-800': user.role === 'Board',
        'bg-gray-100 text-gray-800': user.role === 'Member'
      }"
    >
      {{ user.role }}
    </span>

    <Button
      v-if="canEditRole(user)"
      icon="pi pi-pencil"
      severity="secondary"
      text
      rounded
      size="small"
      aria-label="Edit role"
      data-testid="role-edit-button"
      @click="handleEditClick"
    />
  </div>
</template>

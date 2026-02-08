<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useUsers } from '@/composables/useUsers'
import Button from 'primevue/button'
import Card from 'primevue/card'
import Tag from 'primevue/tag'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import UserForm from '@/components/users/UserForm.vue'
import type { UpdateUserRequest } from '@/types/user'

const route = useRoute()
const router = useRouter()
const { selectedUser, loading, error, fetchUserById, updateUser, clearError } = useUsers()

const editMode = ref(false)
const updatingUser = ref(false)

const userId = computed(() => route.params.id as string)

onMounted(async () => {
  if (userId.value) {
    await fetchUserById(userId.value)
  }
})

const goBack = () => {
  router.push('/users')
}

const enableEditMode = () => {
  editMode.value = true
  clearError()
}

const cancelEdit = () => {
  editMode.value = false
  clearError()
}

const handleUpdateUser = async (data: UpdateUserRequest) => {
  if (!userId.value) return

  updatingUser.value = true
  const updated = await updateUser(userId.value, data)
  updatingUser.value = false

  if (updated) {
    editMode.value = false
    // Optional: Show success toast
  }
}

const getRoleSeverity = (role: string): 'success' | 'info' | 'warning' => {
  switch (role) {
    case 'Admin':
      return 'success'
    case 'Board':
      return 'info'
    case 'Member':
      return 'warning'
    default:
      return 'info'
  }
}

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleString('es-ES', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>

<template>
  <div class="container mx-auto max-w-3xl p-4">
    <div class="mb-6">
      <Button
        label="Back to Users"
        icon="pi pi-arrow-left"
        text
        @click="goBack"
        class="mb-4"
      />
      <h1 class="text-3xl font-bold text-gray-900">User Details</h1>
    </div>

    <!-- Loading state -->
    <div v-if="loading && !selectedUser" class="flex justify-center p-12">
      <ProgressSpinner />
    </div>

    <!-- Error state -->
    <Message v-else-if="error && !selectedUser" severity="error" :closable="false">
      {{ error }}
      <Button
        label="Go Back"
        text
        size="small"
        class="ml-2"
        @click="goBack"
      />
    </Message>

    <!-- User detail -->
    <div v-else-if="selectedUser">
      <!-- View mode -->
      <Card v-if="!editMode">
        <template #title>
          <div class="flex items-center justify-between">
            <span>{{ selectedUser.firstName }} {{ selectedUser.lastName }}</span>
            <Button
              label="Edit"
              icon="pi pi-pencil"
              @click="enableEditMode"
            />
          </div>
        </template>
        <template #content>
          <div class="space-y-4">
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Email</label>
                <p class="text-gray-900">{{ selectedUser.email }}</p>
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Role</label>
                <Tag :value="selectedUser.role" :severity="getRoleSeverity(selectedUser.role)" />
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Phone</label>
                <p class="text-gray-900">{{ selectedUser.phone || '—' }}</p>
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Status</label>
                <Tag
                  :value="selectedUser.isActive ? 'Active' : 'Inactive'"
                  :severity="selectedUser.isActive ? 'success' : 'danger'"
                />
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Created</label>
                <p class="text-sm text-gray-600">{{ formatDate(selectedUser.createdAt) }}</p>
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Last Updated</label>
                <p class="text-sm text-gray-600">{{ formatDate(selectedUser.updatedAt) }}</p>
              </div>
            </div>
          </div>
        </template>
      </Card>

      <!-- Edit mode -->
      <Card v-else>
        <template #title>
          <span>Edit User</span>
        </template>
        <template #content>
          <UserForm
            mode="edit"
            :user="selectedUser"
            :loading="updatingUser"
            @submit="handleUpdateUser"
            @cancel="cancelEdit"
          />
          <Message v-if="error" severity="error" :closable="false" class="mt-4">
            {{ error }}
          </Message>
        </template>
      </Card>
    </div>
  </div>
</template>

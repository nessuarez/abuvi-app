<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useUsers } from '@/composables/useUsers'
import { useToast } from 'primevue/usetoast'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import UserForm from '@/components/users/UserForm.vue'
import UserRoleCell from '@/components/users/UserRoleCell.vue'
import UserRoleDialog from '@/components/users/UserRoleDialog.vue'
import type { CreateUserRequest, User } from '@/types/user'
import { getRoleLabel } from '@/utils/user'

const router = useRouter()
const toast = useToast()
const { users, loading, error, fetchUsers, createUser, clearError } = useUsers()

const showCreateDialog = ref(false)
const creatingUser = ref(false)

// Role management state
const showRoleDialog = ref(false)
const selectedUser = ref<User | null>(null)

onMounted(() => {
  fetchUsers()
})

const openCreateDialog = () => {
  showCreateDialog.value = true
  clearError()
}

const closeCreateDialog = () => {
  showCreateDialog.value = false
  clearError()
}

const handleCreateUser = async (data: CreateUserRequest) => {
  creatingUser.value = true
  const newUser = await createUser(data)
  creatingUser.value = false

  if (newUser) {
    closeCreateDialog()
    // Optional: Show success toast
  }
}

const viewUserDetail = (userId: string) => {
  router.push(`/users/${userId}`)
}

// Handle edit role click from UserRoleCell
const handleEditRole = (user: User) => {
  selectedUser.value = user
  showRoleDialog.value = true
}

// Handle role update success
const handleRoleUpdated = (updatedUser: User) => {
  toast.add({
    severity: 'success',
    summary: 'Rol actualizado',
    detail: `El rol de ${updatedUser.firstName} ${updatedUser.lastName} ha sido actualizado a ${getRoleLabel(updatedUser.role)}`,
    life: 5000
  })
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
  return new Date(dateString).toLocaleDateString('es-ES', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}
</script>

<template>
  <div class="container mx-auto p-4">
    <div class="mb-6 flex items-center justify-between">
      <h1 class="text-3xl font-bold text-gray-900">Gestión de cuentas</h1>
      <Button label="Crear cuenta" icon="pi pi-plus" @click="openCreateDialog" />
    </div>

    <!-- Loading state -->
    <div v-if="loading && users.length === 0" class="flex justify-center p-12">
      <ProgressSpinner />
    </div>

    <!-- Error state -->
    <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
      {{ error }}
      <Button
        label="Reintentar"
        text
        size="small"
        class="ml-2"
        @click="fetchUsers"
      />
    </Message>

    <!-- Users DataTable -->
    <DataTable
      v-else
      :value="users"
      striped-rows
      paginator
      :rows="10"
      :rows-per-page-options="[5, 10, 20, 50]"
      class="rounded-lg"
      data-testid="users-table"
    >
      <Column field="firstName" header="Nombre" sortable>
        <template #body="{ data }">
          <span class="font-medium">{{ data.firstName }} {{ data.lastName }}</span>
        </template>
      </Column>
      <Column field="email" header="Correo electrónico" sortable />
      <Column field="role" header="Rol" sortable>
        <template #body="{ data }">
          <UserRoleCell :user="data" @edit-role="handleEditRole" />
        </template>
      </Column>
      <Column field="phone" header="Teléfono">
        <template #body="{ data }">
          <span class="text-gray-600">{{ data.phone || '—' }}</span>
        </template>
      </Column>
      <Column field="isActive" header="Estado" sortable>
        <template #body="{ data }">
          <Tag
            :value="data.isActive ? 'Activo' : 'Inactivo'"
            :severity="data.isActive ? 'success' : 'danger'"
          />
        </template>
      </Column>
      <Column field="createdAt" header="Fecha de alta" sortable>
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>
      <Column header="Acciones">
        <template #body="{ data }">
          <Button
            icon="pi pi-eye"
            text
            rounded
            severity="info"
            aria-label="Ver detalles"
            data-testid="view-user-button"
            @click="viewUserDetail(data.id)"
          />
        </template>
      </Column>
    </DataTable>

    <!-- Create User Dialog -->
    <Dialog
      v-model:visible="showCreateDialog"
      header="Crear nueva cuenta"
      modal
      class="w-full max-w-md"
    >
      <UserForm
        mode="create"
        :loading="creatingUser"
        @submit="handleCreateUser"
        @cancel="closeCreateDialog"
      />
      <Message v-if="error" severity="error" :closable="false" class="mt-4">
        {{ error }}
      </Message>
    </Dialog>

    <!-- Role Update Dialog -->
    <UserRoleDialog
      v-model:visible="showRoleDialog"
      :user="selectedUser"
      @role-updated="handleRoleUpdated"
    />
  </div>
</template>

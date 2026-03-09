<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useUsers } from '@/composables/useUsers'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import ConfirmDialog from 'primevue/confirmdialog'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import InputText from 'primevue/inputtext'
import UserForm from '@/components/users/UserForm.vue'
import UserRoleCell from '@/components/users/UserRoleCell.vue'
import UserRoleDialog from '@/components/users/UserRoleDialog.vue'
import type { CreateUserRequest, User } from '@/types/user'

const toast = useToast()
const confirm = useConfirm()
const { users, loading, error, fetchUsers, createUser, toggleUserActive, deleteUser, clearError } = useUsers()

const searchQuery = ref('')

const filteredUsers = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return users.value
  return users.value.filter(u =>
    u.email.toLowerCase().includes(q) ||
    `${u.firstName} ${u.lastName}`.toLowerCase().includes(q)
  )
})

const showCreateDialog = ref(false)
const creatingUser = ref(false)

const showRoleDialog = ref(false)
const selectedUser = ref<User | null>(null)

onMounted(() => { fetchUsers() })

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
  }
}

const handleEditRole = (user: User) => {
  selectedUser.value = user
  showRoleDialog.value = true
}

const handleRoleUpdated = (updatedUser: User) => {
  toast.add({
    severity: 'success',
    summary: 'Rol actualizado',
    detail: `El rol de ${updatedUser.firstName} ${updatedUser.lastName} ha sido actualizado`,
    life: 5000
  })
}

const handleToggleActive = async (user: User) => {
  const action = user.isActive ? 'desactivar' : 'activar'
  confirm.require({
    message: `¿Estás seguro de que deseas ${action} al usuario ${user.firstName} ${user.lastName}?`,
    header: `Confirmar ${action}`,
    icon: user.isActive ? 'pi pi-exclamation-triangle' : 'pi pi-check-circle',
    acceptLabel: 'Sí',
    rejectLabel: 'No',
    accept: async () => {
      const updated = await toggleUserActive(user)
      if (updated) {
        toast.add({
          severity: 'success',
          summary: updated.isActive ? 'Usuario activado' : 'Usuario desactivado',
          detail: `${updated.firstName} ${updated.lastName} ha sido ${updated.isActive ? 'activado' : 'desactivado'}`,
          life: 5000
        })
      }
    }
  })
}

const handleDeleteUser = (user: User) => {
  confirm.require({
    message: `¿Estás seguro de que deseas eliminar permanentemente al usuario ${user.firstName} ${user.lastName}? Esta acción no se puede deshacer.`,
    header: 'Confirmar eliminación',
    icon: 'pi pi-trash',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const deleted = await deleteUser(user.id)
      if (deleted) {
        toast.add({
          severity: 'success',
          summary: 'Usuario eliminado',
          detail: `${user.firstName} ${user.lastName} ha sido eliminado`,
          life: 5000
        })
      }
    }
  })
}

const formatDate = (dateString: string) =>
  new Date(dateString).toLocaleDateString('es-ES', {
    year: 'numeric', month: 'short', day: 'numeric'
  })
</script>

<template>
  <div data-testid="users-admin-panel" class="space-y-4">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-xl font-semibold text-gray-800">Gestión de Usuarios</h2>
      <div class="flex flex-wrap items-center gap-3">
        <IconField>
          <InputIcon class="pi pi-search" />
          <InputText
            v-model="searchQuery"
            placeholder="Buscar por nombre o email…"
            class="w-72"
            aria-label="Buscar usuarios"
            data-testid="users-search-input"
          />
        </IconField>
        <Button label="Crear Usuario" icon="pi pi-plus" @click="openCreateDialog" />
      </div>
    </div>

    <!-- Loading state -->
    <div v-if="loading && users.length === 0" class="flex justify-center py-12">
      <ProgressSpinner />
    </div>

    <!-- Error state -->
    <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
      {{ error }}
      <Button label="Reintentar" text size="small" class="ml-2" @click="fetchUsers" />
    </Message>

    <!-- Users DataTable -->
    <DataTable
      v-else
      :value="filteredUsers"
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
      <Column field="email" header="Email" sortable />
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
      <Column field="createdAt" header="Creado" sortable>
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>
      <Column header="Acciones" :exportable="false" class="w-32">
        <template #body="{ data }">
          <div class="flex items-center gap-1">
            <Button
              :icon="data.isActive ? 'pi pi-ban' : 'pi pi-check'"
              :severity="data.isActive ? 'warn' : 'success'"
              text
              rounded
              size="small"
              :aria-label="data.isActive ? 'Desactivar usuario' : 'Activar usuario'"
              v-tooltip.top="data.isActive ? 'Desactivar' : 'Activar'"
              :data-testid="`toggle-active-${data.id}`"
              @click="handleToggleActive(data)"
            />
            <Button
              icon="pi pi-trash"
              severity="danger"
              text
              rounded
              size="small"
              aria-label="Eliminar usuario"
              v-tooltip.top="'Eliminar'"
              :data-testid="`delete-user-${data.id}`"
              @click="handleDeleteUser(data)"
            />
          </div>
        </template>
      </Column>
      <template #empty>
        <span class="text-gray-500">No se encontraron usuarios que coincidan con la búsqueda.</span>
      </template>
    </DataTable>

    <!-- Create User Dialog -->
    <Dialog
      v-model:visible="showCreateDialog"
      header="Crear Nuevo Usuario"
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

    <!-- Confirm Dialog for activate/deactivate/delete -->
    <ConfirmDialog />

    <!-- Role Update Dialog -->
    <UserRoleDialog
      v-model:visible="showRoleDialog"
      :user="selectedUser"
      @role-updated="handleRoleUpdated"
    />
  </div>
</template>

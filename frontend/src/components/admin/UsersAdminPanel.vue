<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import { useUsers } from '@/composables/useUsers'
import { useToast } from 'primevue/usetoast'
import { useConfirm } from 'primevue/useconfirm'
import { useAuthStore } from '@/stores/auth'
import DataTable, { type DataTablePageEvent, type DataTableSortEvent } from 'primevue/datatable'
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
import type { CreateUserRequest, UpdateUserRequest, User, UserSortField } from '@/types/user'

const toast = useToast()
const confirm = useConfirm()
const auth = useAuthStore()
const { users, loading, error, pagination, fetchUsers, refresh, createUser, updateUser, toggleUserActive, deleteUser, clearError } = useUsers()

const searchQuery = ref('')
const sortField = ref<UserSortField>('createdAt')
const sortOrder = ref<'asc' | 'desc'>('desc')
const rows = ref(20)

// Columns the backend can sort by (column field -> API sort field)
const SORTABLE_FIELDS: readonly UserSortField[] = ['firstName', 'email', 'role', 'createdAt']

const loadUsers = (page = 1) =>
  fetchUsers({
    page,
    pageSize: rows.value,
    search: searchQuery.value,
    sortBy: sortField.value,
    sortOrder: sortOrder.value
  })

const debouncedSearch = useDebounceFn(() => loadUsers(1), 300)
watch(searchQuery, () => { debouncedSearch() })

const onPage = (event: DataTablePageEvent) => {
  rows.value = event.rows
  loadUsers(event.page + 1)
}

const onSort = (event: DataTableSortEvent) => {
  const field = SORTABLE_FIELDS.find(f => f === event.sortField)
  if (field && event.sortOrder) {
    sortField.value = field
    sortOrder.value = event.sortOrder === 1 ? 'asc' : 'desc'
  } else {
    sortField.value = 'createdAt'
    sortOrder.value = 'desc'
  }
  loadUsers(1)
}

const showCreateDialog = ref(false)
const creatingUser = ref(false)

const showRoleDialog = ref(false)
const selectedUser = ref<User | null>(null)

const showEditDialog = ref(false)
const editingUser = ref<User | null>(null)
const updatingUser = ref(false)

onMounted(() => { loadUsers(1) })

const openCreateDialog = () => {
  showCreateDialog.value = true
  clearError()
}

const closeCreateDialog = () => {
  showCreateDialog.value = false
  clearError()
}

const handleCreateUser = async (data: CreateUserRequest | import('@/types/user').UpdateUserRequest) => {
  creatingUser.value = true
  const newUser = await createUser(data as CreateUserRequest)
  creatingUser.value = false
  if (newUser) {
    closeCreateDialog()
  }
}

const openEditDialog = (user: User) => {
  editingUser.value = user
  showEditDialog.value = true
  clearError()
}

const closeEditDialog = () => {
  showEditDialog.value = false
  editingUser.value = null
  clearError()
}

const handleEditUserSubmit = async (data: CreateUserRequest | UpdateUserRequest) => {
  if (!editingUser.value) return
  updatingUser.value = true
  const updated = await updateUser(editingUser.value.id, data as UpdateUserRequest)
  updatingUser.value = false
  if (updated) {
    closeEditDialog()
    toast.add({
      severity: 'success',
      summary: 'Usuario actualizado',
      detail: `${updated.firstName} ${updated.lastName} ha sido actualizado`,
      life: 5000
    })
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
  // UserRoleDialog uses its own useUsers() instance, so reload the current page
  refresh()
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
      <div class="flex items-baseline gap-3">
        <h2 class="text-xl font-semibold text-gray-800">Gestión de Usuarios</h2>
        <span class="text-sm text-gray-500" data-testid="users-total">{{ pagination.totalCount }} usuarios</span>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <IconField>
          <InputIcon class="pi pi-search" />
          <InputText
            v-model="searchQuery"
            placeholder="Buscar por nombre o email…"
            class="w-full sm:w-72"
            aria-label="Buscar usuarios"
            data-testid="users-search-input"
          />
        </IconField>
        <Button v-if="auth.isAdmin" label="Crear Usuario" icon="pi pi-plus" @click="openCreateDialog" />
      </div>
    </div>

    <!-- Loading state -->
    <div v-if="loading && users.length === 0 && !searchQuery.trim()" class="flex justify-center py-12">
      <ProgressSpinner />
    </div>

    <!-- Error state -->
    <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
      {{ error }}
      <Button label="Reintentar" text size="small" class="ml-2" @click="refresh" />
    </Message>

    <!-- Users DataTable -->
    <DataTable
      v-else
      :value="users"
      lazy
      striped-rows
      paginator
      :rows="rows"
      :first="(pagination.page - 1) * rows"
      :total-records="pagination.totalCount"
      :rows-per-page-options="[10, 20, 50, 100]"
      :loading="loading"
      :sort-field="sortField"
      :sort-order="sortOrder === 'asc' ? 1 : -1"
      @page="onPage"
      @sort="onSort"
      scrollable
      scroll-height="flex"
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
      <Column field="isActive" header="Estado">
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
      <Column header="Acciones" :exportable="false" frozen align-frozen="right" class="w-32">
        <template #body="{ data }">
          <div class="flex items-center gap-1">
            <Button
              v-if="auth.isAdmin || auth.isBoard"
              icon="pi pi-pencil"
              severity="info"
              text
              rounded
              size="small"
              aria-label="Editar perfil"
              v-tooltip.top="'Editar perfil'"
              :data-testid="`edit-user-${data.id}`"
              @click="openEditDialog(data)"
            />
            <Button
              v-if="auth.isAdmin"
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
              v-if="auth.isAdmin"
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
        <span v-if="searchQuery.trim()" class="text-gray-500">
          No se encontraron usuarios para «{{ searchQuery.trim() }}».
        </span>
        <span v-else class="text-gray-500">No hay usuarios.</span>
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

    <!-- Edit User Dialog -->
    <Dialog
      v-model:visible="showEditDialog"
      header="Editar Perfil de Usuario"
      modal
      class="w-full max-w-md"
    >
      <UserForm
        v-if="editingUser"
        mode="edit"
        :user="editingUser"
        :loading="updatingUser"
        @submit="handleEditUserSubmit"
        @cancel="closeEditDialog"
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

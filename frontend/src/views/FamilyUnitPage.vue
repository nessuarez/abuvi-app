<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import Card from 'primevue/card'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import ConfirmDialog from 'primevue/confirmdialog'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
import FamilyUnitForm from '@/components/family-units/FamilyUnitForm.vue'
import FamilyMemberForm from '@/components/family-units/FamilyMemberForm.vue'
import FamilyMemberList from '@/components/family-units/FamilyMemberList.vue'
import { useAuthStore } from '@/stores/auth'
import type {
  CreateFamilyUnitRequest,
  UpdateFamilyUnitRequest,
  CreateFamilyMemberRequest,
  UpdateFamilyMemberRequest,
  FamilyMemberResponse
} from '@/types/family-unit'

const confirm = useConfirm()
const toast = useToast()

const {
  familyUnit,
  familyMembers,
  loading,
  error,
  createFamilyUnit,
  getCurrentUserFamilyUnit,
  updateFamilyUnit,
  deleteFamilyUnit,
  createFamilyMember,
  getFamilyMembers,
  updateFamilyMember,
  deleteFamilyMember
} = useFamilyUnits()

const auth = useAuthStore()

// UI State
const showFamilyUnitDialog = ref(false)
const showMemberDialog = ref(false)
const editingMember = ref<FamilyMemberResponse | null>(null)

// Membership dialog state
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<FamilyMemberResponse | null>(null)

// Load family unit and members on mount
onMounted(async () => {
  await loadFamilyUnit()
})

const loadFamilyUnit = async () => {
  const unit = await getCurrentUserFamilyUnit()
  if (unit) {
    await getFamilyMembers(unit.id)
  }
}

// Family Unit handlers

const openCreateFamilyUnitDialog = () => {
  showFamilyUnitDialog.value = true
}

const openEditFamilyUnitDialog = () => {
  showFamilyUnitDialog.value = true
}

const handleFamilyUnitSubmit = async (request: CreateFamilyUnitRequest | UpdateFamilyUnitRequest) => {
  let success = false

  if (familyUnit.value) {
    // Update
    const result = await updateFamilyUnit(familyUnit.value.id, request as UpdateFamilyUnitRequest)
    success = !!result
  } else {
    // Create
    const result = await createFamilyUnit(request as CreateFamilyUnitRequest)
    success = !!result

    if (success && result) {
      // Load members (representative should be auto-created)
      await getFamilyMembers(result.id)
    }
  }

  if (success) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: familyUnit.value ? 'Unidad familiar actualizada' : 'Unidad familiar creada',
      life: 3000
    })
    showFamilyUnitDialog.value = false
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Error al guardar la unidad familiar',
      life: 5000
    })
  }
}

const handleDeleteFamilyUnit = () => {
  if (!familyUnit.value) return

  confirm.require({
    message: '¿Estás seguro de que quieres eliminar la unidad familiar? Esto eliminará todos los miembros familiares.',
    header: 'Confirmar Eliminación',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const success = await deleteFamilyUnit(familyUnit.value!.id)

      if (success) {
        toast.add({
          severity: 'success',
          summary: 'Éxito',
          detail: 'Unidad familiar eliminada',
          life: 3000
        })
      } else {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: error.value || 'Error al eliminar la unidad familiar',
          life: 5000
        })
      }
    }
  })
}

// Family Member handlers

const openCreateMemberDialog = () => {
  editingMember.value = null
  showMemberDialog.value = true
}

const openEditMemberDialog = (member: FamilyMemberResponse) => {
  editingMember.value = member
  showMemberDialog.value = true
}

const handleMemberSubmit = async (request: CreateFamilyMemberRequest | UpdateFamilyMemberRequest) => {
  if (!familyUnit.value) return

  let success = false

  if (editingMember.value) {
    // Update
    const result = await updateFamilyMember(
      familyUnit.value.id,
      editingMember.value.id,
      request as UpdateFamilyMemberRequest
    )
    success = !!result
  } else {
    // Create
    const result = await createFamilyMember(familyUnit.value.id, request as CreateFamilyMemberRequest)
    success = !!result
  }

  if (success) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: editingMember.value ? 'Miembro actualizado' : 'Miembro añadido',
      life: 3000
    })
    showMemberDialog.value = false
    editingMember.value = null
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Error al guardar el miembro familiar',
      life: 5000
    })
  }
}

const handleDeleteMember = (member: FamilyMemberResponse) => {
  if (!familyUnit.value) return

  confirm.require({
    message: `¿Estás seguro de que quieres eliminar a ${member.firstName} ${member.lastName}?`,
    header: 'Confirmar Eliminación',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const success = await deleteFamilyMember(familyUnit.value!.id, member.id)

      if (success) {
        toast.add({
          severity: 'success',
          summary: 'Éxito',
          detail: 'Miembro eliminado',
          life: 3000
        })
      } else {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: error.value || 'Error al eliminar el miembro familiar',
          life: 5000
        })
      }
    }
  })
}

const handleManageMembership = (member: FamilyMemberResponse) => {
  selectedMemberForMembership.value = member
  showMembershipDialog.value = true
}
</script>

<template>
  <div class="family-unit-page p-4 max-w-7xl mx-auto">
    <ConfirmDialog />

    <div class="mb-6">
      <h1 class="text-3xl font-bold mb-2">Mi Unidad Familiar</h1>
      <p class="text-gray-600">
        Gestiona tu unidad familiar y los miembros que la componen
      </p>
    </div>

    <!-- No Family Unit Yet -->
    <div v-if="!loading && !familyUnit" class="text-center py-12">
      <Card class="max-w-md mx-auto">
        <template #content>
          <div class="space-y-4">
            <i class="pi pi-users text-6xl text-gray-300"></i>
            <h2 class="text-xl font-semibold">Aún no tienes una unidad familiar</h2>
            <p class="text-gray-600">
              Crea tu unidad familiar para poder gestionar los miembros de tu familia y registrarlos en campamentos.
            </p>
            <Button
              label="Crear Unidad Familiar"
              icon="pi pi-plus"
              @click="openCreateFamilyUnitDialog"
            />
          </div>
        </template>
      </Card>
    </div>

    <!-- Has Family Unit -->
    <div v-else-if="familyUnit" class="space-y-6">
      <!-- Family Unit Card -->
      <Card>
        <template #title>
          <div class="flex justify-between items-center">
            <span>{{ familyUnit.name }}</span>
            <div class="flex gap-2">
              <Button
                icon="pi pi-pencil"
                label="Editar"
                severity="info"
                outlined
                @click="openEditFamilyUnitDialog"
              />
              <Button
                icon="pi pi-trash"
                label="Eliminar"
                severity="danger"
                outlined
                @click="handleDeleteFamilyUnit"
              />
            </div>
          </div>
        </template>
        <template #content>
          <div class="text-sm text-gray-600">
            <p>Creada el {{ new Date(familyUnit.createdAt).toLocaleDateString('es-ES') }}</p>
          </div>
        </template>
      </Card>

      <!-- Family Members Section -->
      <Card>
        <template #title>
          <div class="flex justify-between items-center">
            <span>Miembros Familiares</span>
            <Button
              icon="pi pi-plus"
              label="Añadir Miembro"
              @click="openCreateMemberDialog"
            />
          </div>
        </template>
        <template #content>
          <FamilyMemberList
            :members="familyMembers"
            :loading="loading"
            :can-manage-memberships="auth.isBoard"
            @edit="openEditMemberDialog"
            @delete="handleDeleteMember"
            @manage-membership="handleManageMembership"
          />
        </template>
      </Card>
    </div>

    <!-- Loading State -->
    <div v-else class="text-center py-12">
      <i class="pi pi-spin pi-spinner text-4xl text-primary-500"></i>
      <p class="mt-4 text-gray-600">Cargando...</p>
    </div>

    <!-- Family Unit Dialog -->
    <Dialog
      v-model:visible="showFamilyUnitDialog"
      :header="familyUnit ? 'Editar Unidad Familiar' : 'Crear Unidad Familiar'"
      :modal="true"
      :closable="!loading"
      :dismissableMask="!loading"
      class="w-full max-w-md"
    >
      <FamilyUnitForm
        :family-unit="familyUnit"
        :loading="loading"
        @submit="handleFamilyUnitSubmit"
        @cancel="showFamilyUnitDialog = false"
      />
    </Dialog>

    <!-- Family Member Dialog -->
    <Dialog
      v-model:visible="showMemberDialog"
      :header="editingMember ? 'Editar Miembro' : 'Añadir Miembro'"
      :modal="true"
      :closable="!loading"
      :dismissableMask="!loading"
      class="w-full max-w-2xl"
    >
      <FamilyMemberForm
        :member="editingMember"
        :loading="loading"
        @submit="handleMemberSubmit"
        @cancel="showMemberDialog = false"
      />
    </Dialog>

    <!-- Membership Dialog -->
    <MembershipDialog
      v-if="selectedMemberForMembership"
      v-model:visible="showMembershipDialog"
      :family-unit-id="familyUnit?.id ?? ''"
      :member-id="selectedMemberForMembership.id"
      :member-name="`${selectedMemberForMembership.firstName} ${selectedMemberForMembership.lastName}`"
    />
  </div>
</template>

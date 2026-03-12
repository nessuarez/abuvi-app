<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useDebounceFn } from '@vueuse/core'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import SelectButton from 'primevue/selectbutton'
import Tag from 'primevue/tag'
import ConfirmDialog from 'primevue/confirmdialog'
import type { DataTablePageEvent } from 'primevue/datatable'
import type { FamilyUnitResponse } from '@/types/family-unit'

const router = useRouter()
const confirm = useConfirm()
const toast = useToast()
const { allFamilyUnits, familyUnitsPagination, loading, error, fetchAllFamilyUnits,
        adminDeleteFamilyUnit, updateFamilyUnitStatus } = useFamilyUnits()

const searchQuery = ref('')
const membershipFilter = ref<string>('all')
const membershipFilterOptions = [
  { label: 'Todas', value: 'all' },
  { label: 'Socias', value: 'active' },
  { label: 'No socias', value: 'none' }
]
const statusFilter = ref<string>('all')
const statusFilterOptions = [
  { label: 'Todas', value: 'all' },
  { label: 'Activas', value: 'active' },
  { label: 'Inactivas', value: 'inactive' }
]

const loadFamilyUnits = () => {
  fetchAllFamilyUnits({
    search: searchQuery.value,
    page: 1,
    membershipStatus: membershipFilter.value as 'all' | 'active' | 'none',
    isActive: statusFilter.value === 'all' ? null : statusFilter.value === 'active'
  })
}

const debouncedSearch = useDebounceFn(() => loadFamilyUnits(), 300)

watch(searchQuery, debouncedSearch)
watch(membershipFilter, () => loadFamilyUnits())
watch(statusFilter, () => loadFamilyUnits())

onMounted(() => loadFamilyUnits())

const onPage = (event: DataTablePageEvent) => {
  fetchAllFamilyUnits({
    page: event.page + 1,
    pageSize: event.rows,
    search: searchQuery.value,
    membershipStatus: membershipFilter.value as 'all' | 'active' | 'none',
    isActive: statusFilter.value === 'all' ? null : statusFilter.value === 'active'
  })
}

const formatDate = (dateStr: string) =>
  new Date(dateStr).toLocaleDateString('es-ES', {
    year: 'numeric', month: 'short', day: 'numeric'
  })

const handleToggleStatus = (familyUnit: FamilyUnitResponse) => {
  const action = familyUnit.isActive ? 'desactivar' : 'activar'
  confirm.require({
    message: `¿Estás seguro de que deseas ${action} la unidad familiar "${familyUnit.name}"?`,
    header: `Confirmar ${action}`,
    icon: familyUnit.isActive ? 'pi pi-ban' : 'pi pi-check-circle',
    acceptLabel: 'Sí',
    rejectLabel: 'No',
    acceptClass: familyUnit.isActive ? 'p-button-warn' : 'p-button-success',
    accept: async () => {
      const result = await updateFamilyUnitStatus(familyUnit.id, !familyUnit.isActive)
      if (result) {
        toast.add({
          severity: 'success',
          summary: familyUnit.isActive ? 'Desactivada' : 'Activada',
          detail: `La unidad familiar "${familyUnit.name}" ha sido ${action}da`,
          life: 5000
        })
        loadFamilyUnits()
      } else {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: error.value || `No se pudo ${action} la unidad familiar`,
          life: 5000
        })
      }
    }
  })
}

const handleAdminDelete = (familyUnit: FamilyUnitResponse) => {
  confirm.require({
    message: `¿Eliminar la unidad familiar "${familyUnit.name}"? Esta acción no se puede deshacer.`,
    header: 'Confirmar eliminación',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const result = await adminDeleteFamilyUnit(familyUnit.id)
      if (result) {
        toast.add({
          severity: 'success',
          summary: 'Eliminada',
          detail: `La unidad familiar "${familyUnit.name}" ha sido eliminada`,
          life: 5000
        })
        loadFamilyUnits()
      } else {
        toast.add({
          severity: 'error',
          summary: 'No se pudo eliminar',
          detail: error.value || 'Error al eliminar la unidad familiar',
          life: 8000
        })
      }
    }
  })
}
</script>

<template>
  <div data-testid="family-units-admin-panel" class="space-y-4">
    <ConfirmDialog />

    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-xl font-semibold text-gray-800">Unidades Familiares</h2>
      <div class="flex flex-wrap items-center gap-2">
        <span class="p-input-icon-left">
          <i class="pi pi-search" />
          <InputText
            v-model="searchQuery"
            placeholder="Buscar familia..."
            class="w-64"
          />
        </span>
        <SelectButton
          v-model="membershipFilter"
          :options="membershipFilterOptions"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          data-testid="membership-filter"
        />
        <SelectButton
          v-model="statusFilter"
          :options="statusFilterOptions"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          data-testid="status-filter"
        />
      </div>
    </div>

    <!-- Loading state -->
    <div v-if="loading && allFamilyUnits.length === 0" class="flex justify-center py-12">
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
        @click="loadFamilyUnits()"
      />
    </Message>

    <!-- Data Table -->
    <DataTable
      v-else
      :value="allFamilyUnits"
      lazy
      paginator
      :rows="familyUnitsPagination.pageSize"
      :total-records="familyUnitsPagination.totalCount"
      striped-rows
      class="rounded-lg"
      @page="onPage"
    >
      <Column field="name" header="Nombre Familia" sortable>
        <template #body="{ data }">
          <span class="font-medium">{{ data.name }}</span>
        </template>
      </Column>
      <Column field="familyNumber" header="Nº Familia">
        <template #body="{ data }">
          <span class="text-gray-600">{{ data.familyNumber ?? '—' }}</span>
        </template>
      </Column>
      <Column field="isActive" header="Estado" :sortable="true" style="min-width: 8rem">
        <template #body="{ data }">
          <Tag
            :value="data.isActive ? 'Activa' : 'Inactiva'"
            :severity="data.isActive ? 'success' : 'danger'"
          />
        </template>
      </Column>
      <Column field="representativeName" header="Representante">
        <template #body="{ data }">
          <span class="text-gray-600">{{ data.representativeName || '—' }}</span>
        </template>
      </Column>
      <Column field="membersCount" header="Miembros">
        <template #body="{ data }">
          <span class="text-gray-600">{{ data.membersCount ?? '—' }}</span>
        </template>
      </Column>
      <Column field="createdAt" header="Fecha Creación">
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>
      <Column header="Acciones" style="min-width: 12rem">
        <template #body="{ data }">
          <div class="flex gap-1">
            <Button
              icon="pi pi-eye"
              severity="info"
              text
              rounded
              v-tooltip.top="'Ver detalle'"
              @click="router.push(`/family-unit/${data.id}`)"
            />
            <Button
              :icon="data.isActive ? 'pi pi-ban' : 'pi pi-check-circle'"
              :severity="data.isActive ? 'warn' : 'success'"
              text
              rounded
              v-tooltip.top="data.isActive ? 'Desactivar' : 'Activar'"
              @click="handleToggleStatus(data)"
            />
            <Button
              icon="pi pi-trash"
              severity="danger"
              text
              rounded
              v-tooltip.top="'Eliminar'"
              @click="handleAdminDelete(data)"
            />
          </div>
        </template>
      </Column>
    </DataTable>
  </div>
</template>

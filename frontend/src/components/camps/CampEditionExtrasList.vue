<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Dialog from 'primevue/dialog'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import CampEditionExtrasFormDialog from './CampEditionExtrasFormDialog.vue'
import { useCampExtras } from '@/composables/useCampExtras'
import { useAuthStore } from '@/stores/auth'
import type { CampEditionExtra, CampEditionStatus } from '@/types/camp-edition'

const props = defineProps<{
  editionId: string
  editionStatus: CampEditionStatus
}>()

const toast = useToast()
const auth = useAuthStore()
const {
  extras,
  loading,
  error,
  fetchExtras,
  deleteExtra,
  activateExtra,
  deactivateExtra
} = useCampExtras(props.editionId)

const canManage = computed(() => auth.user?.role === 'Admin' || auth.user?.role === 'Board')
const canAdd = computed(
  () => canManage.value && props.editionStatus !== 'Completed' && props.editionStatus !== 'Closed'
)

const showDialog = ref(false)
const editingExtra = ref<CampEditionExtra | undefined>(undefined)
const deleteTarget = ref<CampEditionExtra | null>(null)
const showDeleteConfirm = ref(false)

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0
  }).format(amount)

const pricingTypeLabel = (type: 'PerPerson' | 'PerFamily'): string =>
  type === 'PerPerson' ? 'Por persona' : 'Por familia'

const pricingPeriodLabel = (period: 'OneTime' | 'PerDay'): string =>
  period === 'OneTime' ? 'Una vez' : 'Por día'

const soldDisplay = (extra: CampEditionExtra): string => {
  const qty = extra.currentQuantitySold ?? 0
  const qtyStr = extra.currentQuantitySold !== null ? String(qty) : '—'
  return extra.maxQuantity ? `${qtyStr} / ${extra.maxQuantity}` : qtyStr
}

const openCreate = () => {
  editingExtra.value = undefined
  showDialog.value = true
}

const openEdit = (extra: CampEditionExtra) => {
  editingExtra.value = extra
  showDialog.value = true
}

const confirmDelete = (extra: CampEditionExtra) => {
  deleteTarget.value = extra
  showDeleteConfirm.value = true
}

const handleDelete = async () => {
  if (!deleteTarget.value) return
  const success = await deleteExtra(deleteTarget.value.id)
  showDeleteConfirm.value = false
  if (success) {
    toast.add({ severity: 'success', summary: 'Extra eliminado', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
  deleteTarget.value = null
}

const handleToggleActive = async (extra: CampEditionExtra) => {
  const result = extra.isActive
    ? await deactivateExtra(extra.id)
    : await activateExtra(extra.id)
  if (result) {
    toast.add({
      severity: 'success',
      summary: extra.isActive ? 'Extra desactivado' : 'Extra activado',
      life: 3000
    })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

const handleSaved = () => {
  fetchExtras()
}

onMounted(() => fetchExtras())
</script>

<template>
  <div data-testid="extras-list">
    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-lg font-semibold text-gray-900">
        Extras de la edición ({{ extras.length }})
      </h2>
      <Button
        v-if="canAdd"
        label="Añadir extra"
        icon="pi pi-plus"
        size="small"
        @click="openCreate"
        data-testid="add-extra-button"
      />
    </div>

    <div v-if="loading && extras.length === 0" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>

    <Message v-else-if="error && extras.length === 0" severity="error" :closable="false">
      {{ error }}
    </Message>

    <div
      v-else-if="extras.length === 0"
      class="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-400"
      data-testid="empty-extras-state"
    >
      <i class="pi pi-list-check mb-2 text-2xl" />
      <p>No hay extras configurados para esta edición.</p>
      <Button
        v-if="canAdd"
        label="Añadir el primero"
        text
        size="small"
        class="mt-2"
        @click="openCreate"
      />
    </div>

    <DataTable
      v-else
      :value="extras"
      scrollable
      data-testid="extras-table"
    >
      <Column header="Nombre" field="name" style="min-width: 200px">
        <template #body="{ data }">
          <div>
            <span class="font-medium text-gray-900">{{ data.name }}</span>
            <span v-if="data.requiresUserInput" class="ml-2 inline-flex items-center gap-1 text-xs text-blue-600">
              <i class="pi pi-pencil text-xs" />
              Requiere info
            </span>
            <p v-if="data.description" class="mt-0.5 text-xs text-gray-500">
              {{ data.description }}
            </p>
          </div>
        </template>
      </Column>

      <Column header="Precio" field="price" style="min-width: 140px">
        <template #body="{ data }">
          <div>
            <span class="font-semibold">{{ formatCurrency(data.price) }}</span>
            <p class="text-xs text-gray-500">
              {{ pricingTypeLabel(data.pricingType) }} · {{ pricingPeriodLabel(data.pricingPeriod) }}
            </p>
          </div>
        </template>
      </Column>

      <Column header="Obligatorio" style="min-width: 100px">
        <template #body="{ data }">
          <Tag
            v-if="data.isRequired"
            value="Sí"
            severity="danger"
          />
          <span v-else class="text-sm text-gray-400">No</span>
        </template>
      </Column>

      <Column header="Estado" style="min-width: 100px">
        <template #body="{ data }">
          <Tag
            :value="data.isActive ? 'Activo' : 'Inactivo'"
            :severity="data.isActive ? 'success' : 'secondary'"
          />
        </template>
      </Column>

      <Column header="Vendidos" style="min-width: 80px">
        <template #body="{ data }">
          <span class="text-sm">{{ soldDisplay(data) }}</span>
        </template>
      </Column>

      <Column v-if="canManage" header="Acciones" style="min-width: 140px">
        <template #body="{ data }">
          <div class="flex items-center gap-1">
            <Button
              :icon="data.isActive ? 'pi pi-eye-slash' : 'pi pi-eye'"
              severity="secondary"
              text
              size="small"
              :title="data.isActive ? 'Desactivar' : 'Activar'"
              :data-testid="`toggle-active-button-${data.id}`"
              @click="handleToggleActive(data)"
            />
            <Button
              icon="pi pi-pencil"
              severity="secondary"
              text
              size="small"
              title="Editar"
              :data-testid="`edit-extra-button-${data.id}`"
              @click="openEdit(data)"
            />
            <Button
              icon="pi pi-trash"
              severity="danger"
              text
              size="small"
              title="Eliminar"
              :data-testid="`delete-extra-button-${data.id}`"
              @click="confirmDelete(data)"
            />
          </div>
        </template>
      </Column>
    </DataTable>

    <!-- Create/Edit Dialog -->
    <CampEditionExtrasFormDialog
      v-model:visible="showDialog"
      :edition-id="editionId"
      :extra="editingExtra"
      @saved="handleSaved"
    />

    <!-- Delete Confirmation -->
    <Dialog
      v-model:visible="showDeleteConfirm"
      header="Eliminar extra"
      modal
      class="w-full max-w-sm"
    >
      <p class="text-sm text-gray-700">
        ¿Estás seguro de que quieres eliminar
        <strong>{{ deleteTarget?.name }}</strong>? Esta acción no se puede deshacer. Si el extra
        tiene ventas registradas, no podrá eliminarse.
      </p>
      <template #footer>
        <div class="flex justify-end gap-2">
          <Button
            label="Cancelar"
            severity="secondary"
            text
            @click="showDeleteConfirm = false"
          />
          <Button
            label="Eliminar"
            severity="danger"
            :loading="loading"
            @click="handleDelete"
          />
        </div>
      </template>
    </Dialog>
  </div>
</template>

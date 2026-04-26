<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import Listbox from 'primevue/listbox'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useToast } from 'primevue/usetoast'
import { useAccommodationZones } from '@/composables/useAccommodationZones'
import type { AccommodationZoneResponse, AccommodationTypeValue } from '@/types/accommodation-assignment'
import { ACCOMMODATION_TYPE_LABELS } from '@/types/accommodation-assignment'
import type { CampEditionAccommodation } from '@/types/camp-edition'

const props = defineProps<{
  campEditionId: string
  accommodations: CampEditionAccommodation[]
}>()

const toast = useToast()
const campEditionIdRef = computed(() => props.campEditionId)
const { zones, loading, error, loadZones, createZone, updateZone, deleteZone, attachAccommodations } =
  useAccommodationZones(campEditionIdRef)

// Zone form dialog
const showZoneDialog = ref(false)
const editingZone = ref<AccommodationZoneResponse | null>(null)
const formName = ref('')
const formType = ref<AccommodationTypeValue>('Lodge')
const formCapacity = ref<number | null>(null)
const formNotes = ref('')
const formNameError = ref('')

// Delete dialog
const showDeleteDialog = ref(false)
const deleteTarget = ref<AccommodationZoneResponse | null>(null)

// Attach accommodations dialog
const showAttachDialog = ref(false)
const attachingZone = ref<AccommodationZoneResponse | null>(null)
const selectedAccommodationIds = ref<string[]>([])

const TYPE_OPTIONS = Object.entries(ACCOMMODATION_TYPE_LABELS).map(([value, label]) => ({ value, label }))

const accommodationsForZoneType = computed(() => {
  if (!attachingZone.value) return []
  return props.accommodations
    .filter((a) => a.accommodationType === attachingZone.value!.accommodationType && a.isActive)
    .map((a) => ({ id: a.id, name: a.name }))
})

function openCreate() {
  editingZone.value = null
  formName.value = ''
  formType.value = 'Lodge'
  formCapacity.value = null
  formNotes.value = ''
  formNameError.value = ''
  showZoneDialog.value = true
}

function openEdit(zone: AccommodationZoneResponse) {
  editingZone.value = zone
  formName.value = zone.name
  formType.value = zone.accommodationType
  formCapacity.value = zone.maxCapacity
  formNotes.value = zone.distributionNotes ?? ''
  formNameError.value = ''
  showZoneDialog.value = true
}

async function handleSave() {
  if (!formName.value.trim()) {
    formNameError.value = 'El nombre es obligatorio'
    return
  }
  formNameError.value = ''

  const payload = {
    name: formName.value.trim(),
    maxCapacity: formCapacity.value,
    distributionNotes: formNotes.value.trim() || null,
    sortOrder: editingZone.value?.sortOrder ?? zones.value.length + 1
  }

  if (editingZone.value) {
    const result = await updateZone(editingZone.value.id, payload)
    if (result) {
      toast.add({ severity: 'success', summary: 'Zona actualizada', life: 3000 })
      showZoneDialog.value = false
    } else {
      toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
    }
  } else {
    const result = await createZone({ ...payload, accommodationType: formType.value, sortOrder: zones.value.length + 1 })
    if (result) {
      toast.add({ severity: 'success', summary: 'Zona creada', life: 3000 })
      showZoneDialog.value = false
    } else {
      toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
    }
  }
}

function openDeleteConfirm(zone: AccommodationZoneResponse) {
  deleteTarget.value = zone
  showDeleteDialog.value = true
}

async function handleDelete() {
  if (!deleteTarget.value) return
  const success = await deleteZone(deleteTarget.value.id)
  showDeleteDialog.value = false
  if (success) {
    toast.add({ severity: 'success', summary: 'Zona eliminada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
  deleteTarget.value = null
}

function openAttachDialog(zone: AccommodationZoneResponse) {
  attachingZone.value = zone
  selectedAccommodationIds.value = [...zone.accommodationIds]
  showAttachDialog.value = true
}

async function handleAttach() {
  if (!attachingZone.value) return
  const success = await attachAccommodations(attachingZone.value.id, selectedAccommodationIds.value)
  showAttachDialog.value = false
  if (success) {
    toast.add({ severity: 'success', summary: 'Alojamientos actualizados', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}

onMounted(loadZones)
</script>

<template>
  <div class="rounded-lg border border-gray-200 bg-white p-6">
    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-lg font-semibold text-gray-900">Zonas de alojamiento</h2>
      <Button label="Nueva zona" icon="pi pi-plus" size="small" @click="openCreate" />
    </div>

    <div v-if="loading && zones.length === 0" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>

    <Message v-else-if="error && zones.length === 0" severity="error" :closable="false">
      {{ error }}
    </Message>

    <div
      v-else-if="zones.length === 0"
      class="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-400"
    >
      No hay zonas configuradas. Crea una zona para agrupar alojamientos.
    </div>

    <DataTable v-else :value="zones" :loading="loading" class="text-sm">
      <Column field="name" header="Nombre" />
      <Column header="Tipo">
        <template #body="{ data }">
          {{ ACCOMMODATION_TYPE_LABELS[data.accommodationType as AccommodationTypeValue] }}
        </template>
      </Column>
      <Column header="Capacidad">
        <template #body="{ data }">
          {{ data.maxCapacity ?? '—' }}
        </template>
      </Column>
      <Column header="Alojamientos">
        <template #body="{ data }">
          {{ data.accommodationIds.length }}
        </template>
      </Column>
      <Column header="Acciones" style="width: 11rem">
        <template #body="{ data }">
          <div class="flex gap-1">
            <Button
              icon="pi pi-link"
              size="small"
              text
              severity="secondary"
              title="Gestionar alojamientos"
              @click="openAttachDialog(data)"
            />
            <Button
              icon="pi pi-pencil"
              size="small"
              text
              severity="secondary"
              title="Editar"
              @click="openEdit(data)"
            />
            <Button
              icon="pi pi-trash"
              size="small"
              text
              severity="danger"
              title="Eliminar"
              @click="openDeleteConfirm(data)"
            />
          </div>
        </template>
      </Column>
    </DataTable>
  </div>

  <!-- Create / Edit Dialog -->
  <Dialog
    v-model:visible="showZoneDialog"
    :header="editingZone ? 'Editar zona' : 'Nueva zona'"
    modal
    class="w-full max-w-md"
  >
    <div class="flex flex-col gap-3">
      <div>
        <InputText
          v-model="formName"
          placeholder="Nombre de la zona"
          class="w-full"
          :class="formNameError ? 'p-invalid' : ''"
          @keyup.enter="handleSave"
        />
        <p v-if="formNameError" class="mt-1 text-xs text-red-500">{{ formNameError }}</p>
      </div>
      <Select
        v-model="formType"
        :options="TYPE_OPTIONS"
        option-label="label"
        option-value="value"
        placeholder="Tipo de alojamiento"
        class="w-full"
        :disabled="!!editingZone"
      />
      <InputNumber
        v-model="formCapacity"
        placeholder="Capacidad máxima (opcional)"
        class="w-full"
        :min="1"
        show-buttons
      />
      <Textarea
        v-model="formNotes"
        placeholder="Notas de distribución (opcional)"
        :rows="2"
        class="w-full"
        auto-resize
      />
    </div>
    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cancelar" severity="secondary" text @click="showZoneDialog = false" />
        <Button :label="editingZone ? 'Guardar' : 'Crear zona'" :loading="loading" @click="handleSave" />
      </div>
    </template>
  </Dialog>

  <!-- Delete Confirmation Dialog -->
  <Dialog v-model:visible="showDeleteDialog" header="Eliminar zona" modal class="w-full max-w-sm">
    <p class="text-sm text-gray-700">
      ¿Eliminar la zona <strong>{{ deleteTarget?.name }}</strong>? Esta acción no se puede deshacer.
    </p>
    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cancelar" severity="secondary" text @click="showDeleteDialog = false" />
        <Button label="Eliminar" severity="danger" :loading="loading" @click="handleDelete" />
      </div>
    </template>
  </Dialog>

  <!-- Attach Accommodations Dialog -->
  <Dialog
    v-model:visible="showAttachDialog"
    header="Gestionar alojamientos de la zona"
    modal
    class="w-full max-w-md"
  >
    <p class="mb-3 text-sm text-gray-500">
      Selecciona los alojamientos de tipo
      <strong>{{ attachingZone ? ACCOMMODATION_TYPE_LABELS[attachingZone.accommodationType] : '' }}</strong>
      que pertenecen a esta zona.
    </p>
    <Listbox
      v-model="selectedAccommodationIds"
      :options="accommodationsForZoneType"
      option-label="name"
      option-value="id"
      multiple
      class="w-full"
      :empty-message="'No hay alojamientos de este tipo'"
    />
    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cancelar" severity="secondary" text @click="showAttachDialog = false" />
        <Button label="Guardar" :loading="loading" @click="handleAttach" />
      </div>
    </template>
  </Dialog>
</template>

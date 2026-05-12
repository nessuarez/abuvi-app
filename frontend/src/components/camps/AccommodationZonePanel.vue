<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useToast } from 'primevue/usetoast'
import FeatureAssignmentDialog from './FeatureAssignmentDialog.vue'
import CampEditionAccommodationDialog from './CampEditionAccommodationDialog.vue'
import AccommodationMediaManager from './AccommodationMediaManager.vue'
import AccommodationMediaGallery from './AccommodationMediaGallery.vue'
import { useAccommodationZones } from '@/composables/useAccommodationZones'
import { useAccommodationFeatureAssignment } from '@/composables/useAccommodationFeatureAssignment'
import { useCampAccommodations } from '@/composables/useCampAccommodations'
import { useAuthStore } from '@/stores/auth'
import type { AccommodationZoneResponse, AccommodationTypeValue } from '@/types/accommodation-assignment'
import { ACCOMMODATION_TYPE_LABELS } from '@/types/accommodation-assignment'
import type { CampEditionAccommodation, AccommodationType } from '@/types/camp-edition'
import type { AccommodationFeature } from '@/types/accommodation-feature'

const props = defineProps<{
  campEditionId: string
  accommodations: CampEditionAccommodation[]
  availableFeatures: AccommodationFeature[]
}>()

const emit = defineEmits<{
  'unit-saved': []
}>()

const toast = useToast()
const { isBoard } = useAuthStore()
const campEditionIdRef = computed(() => props.campEditionId)
const { zones, loading, error, loadZones, createZone, updateZone, deleteZone } =
  useAccommodationZones(campEditionIdRef)
const {
  saving: featureSaving,
  error: featureError,
  setZoneFeatures,
} = useAccommodationFeatureAssignment(props.campEditionId)
const {
  loading: unitLoading,
  error: unitError,
  deleteAccommodation,
  activateAccommodation,
  deactivateAccommodation,
} = useCampAccommodations(props.campEditionId)

// Zone form dialog
const showZoneDialog = ref(false)
const editingZone = ref<AccommodationZoneResponse | null>(null)
const formName = ref('')
const formType = ref<AccommodationTypeValue>('Lodge')
const formCapacity = ref<number | null>(null)
const formNotes = ref('')
const formNameError = ref('')

// Delete zone dialog
const showDeleteDialog = ref(false)
const deleteTarget = ref<AccommodationZoneResponse | null>(null)

// Feature assignment dialog
const showFeatureDialog = ref(false)
const featureTarget = ref<AccommodationZoneResponse | null>(null)

// Row expansion
const expandedRows = ref<Record<string, boolean>>({})

// Unit create/edit dialog
const showUnitDialog = ref(false)
const editingUnit = ref<CampEditionAccommodation | undefined>(undefined)
const unitDialogZoneId = ref<string | null>(null)
const unitDialogType = ref<AccommodationType | undefined>(undefined)

// Delete unit dialog
const showDeleteUnitDialog = ref(false)
const deleteUnitTarget = ref<CampEditionAccommodation | null>(null)

const TYPE_OPTIONS = Object.entries(ACCOMMODATION_TYPE_LABELS).map(([value, label]) => ({
  value,
  label,
}))

function unitsForZone(zoneId: string): CampEditionAccommodation[] {
  return props.accommodations.filter((a) => a.zoneId === zoneId)
}

function occupancyLabel(acc: CampEditionAccommodation): string {
  return acc.countByFamily ? 'Por unidad' : 'Por personas'
}

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
    sortOrder: editingZone.value?.sortOrder ?? zones.value.length + 1,
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
    const result = await createZone({
      ...payload,
      accommodationType: formType.value,
      sortOrder: zones.value.length + 1,
    })
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

function openFeatureAssign(zone: AccommodationZoneResponse) {
  featureTarget.value = zone
  showFeatureDialog.value = true
}

async function handleFeaturesSaved(featureIds: string[]) {
  if (!featureTarget.value) return
  const result = await setZoneFeatures(featureTarget.value.id, featureIds)
  if (result) {
    const idx = zones.value.findIndex((z) => z.id === featureTarget.value!.id)
    if (idx !== -1) zones.value[idx] = { ...zones.value[idx], features: result }
    showFeatureDialog.value = false
    featureTarget.value = null
    toast.add({ severity: 'success', summary: 'Características actualizadas', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: featureError.value, life: 5000 })
  }
}

function openCreateUnit(zone: AccommodationZoneResponse) {
  editingUnit.value = undefined
  unitDialogZoneId.value = zone.id
  unitDialogType.value = zone.accommodationType as AccommodationType
  showUnitDialog.value = true
}

function openEditUnit(acc: CampEditionAccommodation) {
  editingUnit.value = acc
  unitDialogZoneId.value = null
  unitDialogType.value = undefined
  showUnitDialog.value = true
}

function handleUnitSaved() {
  emit('unit-saved')
}

function confirmDeleteUnit(acc: CampEditionAccommodation) {
  deleteUnitTarget.value = acc
  showDeleteUnitDialog.value = true
}

async function handleDeleteUnit() {
  if (!deleteUnitTarget.value) return
  const success = await deleteAccommodation(deleteUnitTarget.value.id)
  showDeleteUnitDialog.value = false
  if (success) {
    emit('unit-saved')
    toast.add({ severity: 'success', summary: 'Unidad eliminada', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: unitError.value, life: 5000 })
  }
  deleteUnitTarget.value = null
}

async function handleToggleUnitActive(acc: CampEditionAccommodation) {
  const success = acc.isActive
    ? await deactivateAccommodation(acc.id)
    : await activateAccommodation(acc.id)
  if (success) {
    emit('unit-saved')
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: unitError.value, life: 5000 })
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

    <DataTable
      v-else
      v-model:expanded-rows="expandedRows"
      :value="zones"
      :loading="loading"
      data-key="id"
      class="text-sm"
    >
      <Column expander style="width: 3rem" />
      <Column header="Nombre">
        <template #body="{ data }">
          <div>
            <span>{{ data.name }}</span>
            <div v-if="(data.features ?? []).length > 0" class="mt-1 flex flex-wrap gap-1">
              <span
                v-for="f in (data.features ?? []).slice(0, 3)"
                :key="f.id"
                class="inline-flex items-center gap-1 rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600"
              >
                {{ f.icon }} {{ f.name }}
              </span>
              <span v-if="(data.features ?? []).length > 3" class="text-xs text-gray-400">
                +{{ (data.features ?? []).length - 3 }} más
              </span>
            </div>
          </div>
        </template>
      </Column>
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
      <Column header="Unidades">
        <template #body="{ data }">
          {{ unitsForZone(data.id).length }}
        </template>
      </Column>
      <Column header="Acciones" style="width: 10rem">
        <template #body="{ data }">
          <div class="flex gap-1">
            <Button
              icon="pi pi-star"
              size="small"
              text
              severity="secondary"
              title="Características"
              @click="openFeatureAssign(data)"
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

      <template #expansion="{ data: zone }">
        <div class="px-4 pb-4 pt-2">
          <div class="mb-2 flex items-center justify-between">
            <span class="text-sm font-medium text-gray-700">Unidades</span>
            <Button
              label="Añadir unidad"
              icon="pi pi-plus"
              size="small"
              severity="secondary"
              @click="openCreateUnit(zone)"
            />
          </div>

          <div
            v-if="unitsForZone(zone.id).length === 0"
            class="rounded border border-dashed border-gray-200 px-4 py-6 text-center text-xs text-gray-400"
          >
            Sin unidades. Añade la primera.
          </div>

          <DataTable v-else :value="unitsForZone(zone.id)" class="text-xs">
            <Column header="Nombre">
              <template #body="{ data: unit }">
                <div>
                  <span :class="{ 'opacity-50': !unit.isActive }">{{ unit.name }}</span>
                  <div v-if="(unit.features ?? []).length > 0" class="mt-1 flex flex-wrap gap-1">
                    <span
                      v-for="f in (unit.features ?? []).slice(0, 3)"
                      :key="f.id"
                      class="inline-flex items-center gap-1 rounded bg-gray-100 px-1.5 py-0.5 text-gray-600"
                    >
                      {{ f.icon }} {{ f.name }}
                    </span>
                    <span v-if="(unit.features ?? []).length > 3" class="text-gray-400">
                      +{{ (unit.features ?? []).length - 3 }} más
                    </span>
                  </div>
                </div>
              </template>
            </Column>
            <Column header="Cap.">
              <template #body="{ data: unit }">
                {{ unit.capacity ?? '—' }}
              </template>
            </Column>
            <Column header="Ocupación">
              <template #body="{ data: unit }">
                <Tag
                  :value="occupancyLabel(unit)"
                  :severity="unit.countByFamily ? 'warn' : 'info'"
                  class="text-xs"
                />
              </template>
            </Column>
            <Column header="Estado">
              <template #body="{ data: unit }">
                <Tag
                  v-if="!unit.isActive"
                  value="Inactivo"
                  severity="secondary"
                  class="text-xs"
                />
              </template>
            </Column>
            <Column header="Acciones" style="width: 9rem">
              <template #body="{ data: unit }">
                <div class="flex gap-1">
                  <Button
                    :icon="unit.isActive ? 'pi pi-eye-slash' : 'pi pi-eye'"
                    size="small"
                    text
                    severity="secondary"
                    :title="unit.isActive ? 'Desactivar' : 'Activar'"
                    :loading="unitLoading"
                    @click="handleToggleUnitActive(unit)"
                  />
                  <Button
                    icon="pi pi-pencil"
                    size="small"
                    text
                    severity="secondary"
                    title="Editar"
                    @click="openEditUnit(unit)"
                  />
                  <Button
                    icon="pi pi-trash"
                    size="small"
                    text
                    severity="danger"
                    title="Eliminar"
                    @click="confirmDeleteUnit(unit)"
                  />
                </div>
              </template>
            </Column>
          </DataTable>

          <!-- Media section per zone -->
          <div class="mt-4 border-t pt-3">
            <AccommodationMediaManager
              v-if="isBoard"
              owner-type="zone"
              :owner-id="zone.id"
              :edition-id="campEditionId"
            />
            <AccommodationMediaGallery
              v-else
              :items="zone.mediaItems ?? []"
            />
          </div>
        </div>
      </template>
    </DataTable>
  </div>

  <!-- Create / Edit Zone Dialog -->
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
        <Button
          :label="editingZone ? 'Guardar' : 'Crear zona'"
          :loading="loading"
          @click="handleSave"
        />
      </div>
    </template>
  </Dialog>

  <!-- Delete Zone Confirmation -->
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

  <!-- Unit Create/Edit Dialog -->
  <CampEditionAccommodationDialog
    v-model:visible="showUnitDialog"
    :edition-id="campEditionId"
    :accommodation="editingUnit"
    :prefilled-zone-id="unitDialogZoneId"
    :prefilled-type="unitDialogType"
    @saved="handleUnitSaved"
  />

  <!-- Delete Unit Confirmation -->
  <Dialog
    v-model:visible="showDeleteUnitDialog"
    header="Eliminar unidad"
    modal
    class="w-full max-w-sm"
  >
    <p class="text-sm text-gray-700">
      ¿Eliminar <strong>{{ deleteUnitTarget?.name }}</strong>? Esta acción no se puede deshacer.
    </p>
    <template #footer>
      <div class="flex justify-end gap-2">
        <Button
          label="Cancelar"
          severity="secondary"
          text
          @click="showDeleteUnitDialog = false"
        />
        <Button
          label="Eliminar"
          severity="danger"
          :loading="unitLoading"
          @click="handleDeleteUnit"
        />
      </div>
    </template>
  </Dialog>

  <!-- Feature Assignment Dialog -->
  <FeatureAssignmentDialog
    v-model:visible="showFeatureDialog"
    :title="`Características — ${featureTarget?.name ?? ''}`"
    :initial-feature-ids="(featureTarget?.features ?? []).map((f) => f.id)"
    :available-features="availableFeatures"
    @saved="handleFeaturesSaved"
  />
</template>

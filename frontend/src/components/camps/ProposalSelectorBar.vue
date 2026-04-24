<script setup lang="ts">
import { ref, computed } from 'vue'
import Select from 'primevue/select'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import type { AccommodationAssignmentProposalSummaryResponse } from '@/types/accommodation-assignment'

const props = defineProps<{
  proposals: AccommodationAssignmentProposalSummaryResponse[]
  modelValue: string | null
  saving: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', proposalId: string): void
  (e: 'create', payload: { name: string; notes: string | null; copyFromId?: string }): void
  (e: 'activate', proposalId: string): void
  (e: 'delete', proposalId: string): void
  (e: 'autoAssign'): void
}>()

const newProposalDialog = ref(false)
const autoAssignDialog = ref(false)
const newName = ref('')
const newNotes = ref('')
const copyFromId = ref<string | null>(null)
const nameError = ref('')

const selectedProposal = computed(
  () => props.proposals.find((p) => p.id === props.modelValue) ?? null
)

const activeProposal = computed(() => props.proposals.find((p) => p.isActive) ?? null)

function openNewDialog() {
  newName.value = ''
  newNotes.value = ''
  copyFromId.value = null
  nameError.value = ''
  newProposalDialog.value = true
}

function handleCreate() {
  if (!newName.value.trim()) {
    nameError.value = 'El nombre es obligatorio'
    return
  }
  emit('create', {
    name: newName.value.trim(),
    notes: newNotes.value.trim() || null,
    copyFromId: copyFromId.value ?? undefined
  })
  newProposalDialog.value = false
}

function handleAutoAssign() {
  autoAssignDialog.value = false
  emit('autoAssign')
}
</script>

<template>
  <div class="flex flex-wrap items-center gap-2 border-b bg-white px-4 py-2">
    <Select
      :model-value="modelValue"
      :options="proposals"
      option-label="name"
      option-value="id"
      placeholder="Seleccionar propuesta..."
      class="w-56"
      @update:model-value="$emit('update:modelValue', $event)"
    />

    <Tag
      v-if="activeProposal && activeProposal.id === modelValue"
      value="Activa"
      severity="success"
    />

    <Button
      v-if="selectedProposal && !selectedProposal.isActive"
      label="Activar"
      icon="pi pi-check-circle"
      size="small"
      outlined
      @click="$emit('activate', modelValue!)"
    />

    <Button
      label="Nueva propuesta"
      icon="pi pi-plus"
      size="small"
      outlined
      @click="openNewDialog"
    />

    <Button
      v-if="selectedProposal && !selectedProposal.isActive"
      icon="pi pi-trash"
      severity="danger"
      size="small"
      text
      title="Eliminar propuesta"
      @click="$emit('delete', modelValue!)"
    />

    <Button
      label="Auto-asignar"
      icon="pi pi-bolt"
      size="small"
      :loading="saving"
      @click="autoAssignDialog = true"
    />

    <span v-if="selectedProposal" class="ml-auto text-sm text-gray-500">
      {{ selectedProposal.unassignedCount }} sin asignar ·
      {{ selectedProposal.assignmentCount }} asignadas
    </span>
  </div>

  <!-- New Proposal Dialog -->
  <Dialog
    v-model:visible="newProposalDialog"
    header="Nueva propuesta"
    modal
    class="w-full max-w-md"
  >
    <div class="flex flex-col gap-3">
      <div>
        <InputText
          v-model="newName"
          placeholder="Nombre de la propuesta"
          class="w-full"
          :class="nameError ? 'p-invalid' : ''"
          @keyup.enter="handleCreate"
        />
        <p v-if="nameError" class="mt-1 text-xs text-red-500">{{ nameError }}</p>
      </div>
      <Textarea
        v-model="newNotes"
        placeholder="Notas (opcional)"
        :rows="2"
        class="w-full"
        auto-resize
      />
      <Select
        v-model="copyFromId"
        :options="proposals"
        option-label="name"
        option-value="id"
        placeholder="Copiar asignaciones de (opcional)"
        class="w-full"
        show-clear
      />
    </div>
    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cancelar" severity="secondary" text @click="newProposalDialog = false" />
        <Button label="Crear propuesta" @click="handleCreate" />
      </div>
    </template>
  </Dialog>

  <!-- Auto-assign confirmation Dialog -->
  <Dialog
    v-model:visible="autoAssignDialog"
    header="Auto-asignar familias"
    modal
    class="w-full max-w-sm"
  >
    <p class="text-sm text-gray-700">
      Se asignarán automáticamente las familias sin asignar a los alojamientos disponibles
      usando el algoritmo de ajuste óptimo. Las familias ya asignadas no se modificarán.
    </p>
    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cancelar" severity="secondary" text @click="autoAssignDialog = false" />
        <Button label="Auto-asignar" icon="pi pi-bolt" @click="handleAutoAssign" />
      </div>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Chip from 'primevue/chip'
import Message from 'primevue/message'
import MultiSelect from 'primevue/multiselect'
import Textarea from 'primevue/textarea'
import { useAccommodationFeatures } from '@/composables/useAccommodationFeatures'
import { useRegistrationAccommodationTagging } from '@/composables/useRegistrationAccommodationTagging'
import type { AccommodationNeedResponse } from '@/types/registration'

const props = defineProps<{
  registrationId: string
  initialNeeds: AccommodationNeedResponse[]
  initialNotes: string | null
  specialNeeds: string | null
  campatesPreference: string | null
}>()

const emit = defineEmits<{
  (e: 'updated', needs: AccommodationNeedResponse[]): void
}>()

const toast = useToast()
const { features, fetchFeatures } = useAccommodationFeatures()
const { needs, internalNotes, saving, saveError, updateNeeds, updateNotes } =
  useRegistrationAccommodationTagging()

// Tags edit state
const isEditingTags = ref(false)
const selectedFeatureIds = ref<string[]>([])

// Notes edit state
const isEditingNotes = ref(false)
const editNotes = ref('')

const notesLength = computed(() => editNotes.value.length)
const notesCounterClass = computed(() =>
  notesLength.value > 3800 ? 'text-red-500' : 'text-gray-400',
)

onMounted(async () => {
  needs.value = [...props.initialNeeds]
  internalNotes.value = props.initialNotes
  await fetchFeatures(true)
})

watch(
  () => props.initialNeeds,
  (val) => {
    if (!isEditingTags.value) needs.value = [...val]
  },
)

watch(
  () => props.initialNotes,
  (val) => {
    if (!isEditingNotes.value) internalNotes.value = val
  },
)

// Tags handlers
function startEditingTags() {
  selectedFeatureIds.value = needs.value.map((n) => n.featureId)
  isEditingTags.value = true
}

function cancelEditingTags() {
  isEditingTags.value = false
  selectedFeatureIds.value = []
}

async function saveTags() {
  if (selectedFeatureIds.value.length > 20) {
    toast.add({ severity: 'warn', summary: 'Límite', detail: 'Máximo 20 etiquetas', life: 3000 })
    return
  }
  const result = await updateNeeds(props.registrationId, selectedFeatureIds.value)
  if (result) {
    isEditingTags.value = false
    emit('updated', result.needs)
    toast.add({ severity: 'success', summary: 'Guardado', detail: 'Etiquetas actualizadas', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: saveError.value ?? 'Error al guardar', life: 5000 })
  }
}

// Notes handlers
function startEditingNotes() {
  editNotes.value = internalNotes.value ?? ''
  isEditingNotes.value = true
}

function cancelEditingNotes() {
  isEditingNotes.value = false
  editNotes.value = ''
}

async function saveNotes() {
  if (editNotes.value.length > 4000) {
    toast.add({ severity: 'warn', summary: 'Límite', detail: 'Máximo 4000 caracteres', life: 3000 })
    return
  }
  const result = await updateNotes(props.registrationId, editNotes.value.trim() || null)
  if (result) {
    isEditingNotes.value = false
    toast.add({ severity: 'success', summary: 'Guardado', detail: 'Notas actualizadas', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: saveError.value ?? 'Error al guardar', life: 5000 })
  }
}
</script>

<template>
  <div class="rounded-lg border border-indigo-100 bg-indigo-50/30 p-4">
    <h2 class="mb-4 text-sm font-semibold text-indigo-900">Alojamiento (Junta)</h2>

    <!-- Sub-section 1: Family free-text (read-only) -->
    <div class="mb-4 rounded-md border border-indigo-100 bg-white/60 p-3">
      <h3 class="mb-2 text-xs font-medium uppercase tracking-wide text-indigo-700">Texto libre (familia)</h3>
      <dl class="space-y-2 text-sm">
        <div class="flex flex-col gap-0.5">
          <dt class="font-medium text-gray-600">Necesidades especiales</dt>
          <dd class="whitespace-pre-line text-gray-800">{{ specialNeeds || '—' }}</dd>
        </div>
        <div class="flex flex-col gap-0.5">
          <dt class="font-medium text-gray-600">Preferencia de compañeros</dt>
          <dd class="text-gray-800">{{ campatesPreference || '—' }}</dd>
        </div>
      </dl>
    </div>

    <!-- Sub-section 2: Structured feature tags -->
    <div class="mb-4 rounded-md border border-indigo-100 bg-white/60 p-3">
      <div class="mb-2 flex items-center justify-between">
        <h3 class="text-xs font-medium uppercase tracking-wide text-indigo-700">Etiquetas estructuradas</h3>
        <Button
          v-if="!isEditingTags"
          icon="pi pi-pencil"
          label="Editar"
          size="small"
          severity="secondary"
          outlined
          data-testid="edit-tags-btn"
          @click="startEditingTags"
        />
      </div>

      <!-- Read view -->
      <template v-if="!isEditingTags">
        <div v-if="needs.length > 0" class="flex flex-wrap gap-2">
          <Chip
            v-for="need in needs"
            :key="need.featureId"
            :label="need.featureName"
            class="text-xs"
          />
        </div>
        <p v-else class="text-sm italic text-gray-400">Sin etiquetas</p>
      </template>

      <!-- Edit view -->
      <template v-else>
        <MultiSelect
          v-model="selectedFeatureIds"
          :options="features"
          option-label="name"
          option-value="id"
          filter
          show-clear
          :max-selected-labels="3"
          placeholder="Seleccionar características..."
          class="w-full"
          aria-label="Características de alojamiento"
          data-testid="features-multiselect"
        />
        <p v-if="selectedFeatureIds.length >= 20" class="mt-1 text-xs text-amber-600">
          Límite de 20 etiquetas alcanzado
        </p>
        <Message v-if="saveError && !saving" severity="error" :closable="false" class="mt-2 text-sm">
          {{ saveError }}
        </Message>
        <div class="mt-3 flex gap-2">
          <Button
            label="Guardar"
            icon="pi pi-check"
            size="small"
            :loading="saving"
            data-testid="save-tags-btn"
            @click="saveTags"
          />
          <Button
            label="Cancelar"
            severity="secondary"
            text
            size="small"
            :disabled="saving"
            @click="cancelEditingTags"
          />
        </div>
      </template>
    </div>

    <!-- Sub-section 3: Internal notes -->
    <div class="rounded-md border border-indigo-100 bg-white/60 p-3">
      <div class="mb-2 flex items-center justify-between">
        <h3 class="text-xs font-medium uppercase tracking-wide text-indigo-700">Notas internas (Junta)</h3>
        <Button
          v-if="!isEditingNotes"
          icon="pi pi-pencil"
          label="Editar"
          size="small"
          severity="secondary"
          outlined
          data-testid="edit-notes-btn"
          @click="startEditingNotes"
        />
      </div>

      <!-- Read view -->
      <template v-if="!isEditingNotes">
        <p v-if="internalNotes" class="whitespace-pre-line text-sm text-gray-800">{{ internalNotes }}</p>
        <p v-else class="text-sm italic text-gray-400">Sin notas internas</p>
      </template>

      <!-- Edit view -->
      <template v-else>
        <Textarea
          v-model="editNotes"
          :rows="4"
          :maxlength="4000"
          placeholder="Notas internas de alojamiento (solo visibles para Admin y Junta)..."
          class="w-full"
          aria-label="Notas internas de alojamiento"
          data-testid="notes-textarea"
        />
        <p class="mt-1 text-right text-xs" :class="notesCounterClass">
          {{ notesLength }}/4000
        </p>
        <Message v-if="saveError && !saving" severity="error" :closable="false" class="mt-2 text-sm">
          {{ saveError }}
        </Message>
        <div class="mt-2 flex gap-2">
          <Button
            label="Guardar"
            icon="pi pi-check"
            size="small"
            :loading="saving"
            data-testid="save-notes-btn"
            @click="saveNotes"
          />
          <Button
            label="Cancelar"
            severity="secondary"
            text
            size="small"
            :disabled="saving"
            @click="cancelEditingNotes"
          />
        </div>
      </template>
    </div>
  </div>
</template>

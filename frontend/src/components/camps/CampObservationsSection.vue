<script setup lang="ts">
import { ref, onMounted } from 'vue'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useCamps } from '@/composables/useCamps'

const props = defineProps<{ campId: string }>()

const {
  campObservations,
  observationsLoading,
  observationsError,
  fetchCampObservations,
  addCampObservation
} = useCamps()

const newText = ref('')
const formError = ref<string | null>(null)
const submitting = ref(false)

onMounted(() => fetchCampObservations(props.campId))

const handleAdd = async () => {
  if (!newText.value.trim()) {
    formError.value = 'El texto no puede estar vacío'
    return
  }
  submitting.value = true
  formError.value = null
  const result = await addCampObservation(props.campId, {
    text: newText.value.trim(),
    season: null
  })
  if (result) {
    newText.value = ''
  }
  submitting.value = false
}
</script>

<template>
  <div class="space-y-4">
    <h3 class="text-lg font-semibold text-gray-800">Observaciones</h3>

    <!-- Add form -->
    <div class="rounded-lg border border-gray-200 bg-gray-50 p-4 space-y-3">
      <Textarea
        v-model="newText"
        rows="2"
        placeholder="Añadir observación..."
        class="w-full"
      />
      <Message v-if="formError" severity="error" :closable="false" class="text-sm">
        {{ formError }}
      </Message>
      <div class="flex justify-end">
        <Button
          label="Añadir"
          icon="pi pi-plus"
          size="small"
          :loading="submitting"
          @click="handleAdd"
        />
      </div>
    </div>

    <!-- Loading/error states -->
    <div v-if="observationsLoading" class="flex justify-center py-4">
      <ProgressSpinner style="width: 32px; height: 32px" />
    </div>
    <Message v-else-if="observationsError" severity="error" :closable="false">
      {{ observationsError }}
    </Message>

    <!-- List -->
    <div v-else-if="campObservations.length === 0" class="text-sm text-gray-500">
      No hay observaciones registradas.
    </div>
    <div v-else class="space-y-2">
      <div
        v-for="obs in campObservations"
        :key="obs.id"
        class="rounded-md border border-gray-100 bg-white p-3 text-sm"
      >
        <div class="flex items-start justify-between gap-2">
          <p class="whitespace-pre-wrap text-gray-800">{{ obs.text }}</p>
          <span
            v-if="obs.season"
            class="shrink-0 rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700"
          >
            {{ obs.season }}
          </span>
        </div>
        <p class="mt-1 text-xs text-gray-400">
          {{ obs.createdByUserId ? 'Manual' : 'Importado CSV' }} ·
          {{ new Date(obs.createdAt).toLocaleDateString('es-ES') }}
        </p>
      </div>
    </div>
  </div>
</template>

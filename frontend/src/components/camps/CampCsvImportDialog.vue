<script setup lang="ts">
import { ref } from 'vue'
import Dialog from 'primevue/dialog'
import FileUpload from 'primevue/fileupload'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import { useCamps } from '@/composables/useCamps'
import type { CampImportResult } from '@/types/camp'

defineProps<{ visible: boolean }>()
const emit = defineEmits<{
  'update:visible': [value: boolean]
  'imported': []
}>()

const { importCampsCsv, importLoading, importError } = useCamps()

const selectedFile = ref<File | null>(null)
const result = ref<CampImportResult | null>(null)

const handleFileSelect = (event: { files: File[] }) => {
  selectedFile.value = event.files[0] ?? null
}

const handleImport = async () => {
  if (!selectedFile.value) return
  result.value = null
  const res = await importCampsCsv(selectedFile.value)
  if (res) {
    result.value = res
    emit('imported')
  }
}

const handleClose = () => {
  selectedFile.value = null
  result.value = null
  emit('update:visible', false)
}
</script>

<template>
  <Dialog
    :visible="visible"
    header="Importar campamentos desde CSV"
    modal
    class="w-full max-w-2xl"
    @update:visible="handleClose"
  >
    <div class="space-y-4">
      <Message severity="info" :closable="false" class="text-sm">
        Selecciona el fichero <strong>CAMPAMENTOS.csv</strong> (codificación Windows-1252,
        separado por punto y coma). Tamaño máximo: 1 MB.
      </Message>

      <!-- File picker -->
      <div v-if="!result">
        <FileUpload
          mode="basic"
          accept=".csv"
          :max-file-size="1048576"
          choose-label="Seleccionar CSV"
          :auto="false"
          @select="handleFileSelect"
        />
        <p v-if="selectedFile" class="mt-2 text-sm text-gray-600">
          Fichero seleccionado: {{ selectedFile.name }}
        </p>
      </div>

      <!-- Error message -->
      <Message v-if="importError" severity="error" :closable="false">
        {{ importError }}
      </Message>

      <!-- Import result summary -->
      <div v-if="result" class="rounded-md bg-green-50 p-4 text-sm space-y-2">
        <p class="font-medium text-green-800">Importación completada</p>
        <div class="flex gap-4">
          <span class="text-gray-700">Creados: <strong>{{ result.created }}</strong></span>
          <span class="text-gray-700">Actualizados: <strong>{{ result.updated }}</strong></span>
          <span class="text-gray-700">Omitidos: <strong>{{ result.skipped }}</strong></span>
        </div>
      </div>

      <!-- Row results with errors/warnings -->
      <DataTable
        v-if="result && result.rows.filter(r => r.status === 'Error' || r.gestionPor).length > 0"
        :value="result.rows.filter(r => r.status === 'Error' || r.gestionPor)"
        size="small"
        class="text-xs"
      >
        <Column field="rowNumber" header="Fila" />
        <Column field="campName" header="Campamento" />
        <Column field="status" header="Estado">
          <template #body="{ data }">
            <Tag
              :value="data.status"
              :severity="data.status === 'Error' ? 'danger' : 'warn'"
            />
          </template>
        </Column>
        <Column header="Nota">
          <template #body="{ data }">
            <span v-if="data.message" class="text-red-600">{{ data.message }}</span>
            <span v-else-if="data.gestionPor" class="text-amber-600">
              Gestión por "{{ data.gestionPor }}" — asignar manualmente
            </span>
          </template>
        </Column>
      </DataTable>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cerrar" text @click="handleClose" />
        <Button
          v-if="!result"
          label="Importar"
          icon="pi pi-upload"
          :disabled="!selectedFile"
          :loading="importLoading"
          @click="handleImport"
        />
      </div>
    </template>
  </Dialog>
</template>

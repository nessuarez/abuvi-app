<script setup lang="ts">
import { onMounted } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import Message from 'primevue/message'
import { useCamps } from '@/composables/useCamps'

const props = defineProps<{ campId: string }>()

const { campAuditLog, auditLogLoading, auditLogError, fetchCampAuditLog } = useCamps()

onMounted(() => fetchCampAuditLog(props.campId))
</script>

<template>
  <div class="space-y-3">
    <h3 class="text-lg font-semibold text-gray-800">Registro de cambios</h3>

    <Message v-if="auditLogError" severity="error" :closable="false">
      {{ auditLogError }}
    </Message>

    <DataTable
      v-else
      :value="campAuditLog"
      :loading="auditLogLoading"
      paginator
      :rows="10"
      size="small"
      class="text-sm"
    >
      <Column field="changedAt" header="Fecha" sortable>
        <template #body="{ data }">
          {{ new Date(data.changedAt).toLocaleString('es-ES') }}
        </template>
      </Column>
      <Column field="fieldName" header="Campo" sortable />
      <Column header="Valor anterior">
        <template #body="{ data }">
          <span v-if="data.oldValue" class="text-gray-600">{{ data.oldValue }}</span>
          <span v-else class="italic text-gray-400">—</span>
        </template>
      </Column>
      <Column header="Nuevo valor">
        <template #body="{ data }">
          <Tag
            v-if="data.newValue"
            :value="data.newValue"
            severity="info"
            class="text-xs"
          />
          <span v-else class="italic text-gray-400">—</span>
        </template>
      </Column>
      <Column field="changedByUserId" header="Usuario" />
    </DataTable>
  </div>
</template>

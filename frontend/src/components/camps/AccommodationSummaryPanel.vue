<script setup lang="ts">
import { ref, onMounted } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { api } from '@/utils/api'
import type {
  AssignmentReportGroupResponse,
  AssignmentFamilyResponse
} from '@/types/accommodation-assignment'
import type { ApiResponse } from '@/types/api'

const props = defineProps<{
  proposalId: string
  campEditionId: string
}>()

const byZoneData = ref<AssignmentReportGroupResponse[]>([])
const unassignedFamilies = ref<AssignmentFamilyResponse[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const expandedRows = ref<Record<string, boolean>>({})

async function loadData(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [zoneRes, unassignedRes] = await Promise.all([
      api.get<ApiResponse<AssignmentReportGroupResponse[]>>(
        `/camps/editions/${props.campEditionId}/assignment-proposals/${props.proposalId}/reports/by-zone`
      ),
      api.get<ApiResponse<AssignmentFamilyResponse[]>>(
        `/camps/editions/${props.campEditionId}/assignment-proposals/${props.proposalId}/reports/unassigned`
      )
    ])
    byZoneData.value = zoneRes.data.data ?? []
    unassignedFamilies.value = unassignedRes.data.data ?? []
  } catch {
    error.value = 'Error al cargar el resumen'
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <div class="p-4">
    <div v-if="loading" class="flex justify-center py-8">
      <ProgressSpinner />
    </div>

    <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
      {{ error }}
    </Message>

    <template v-else>
      <Message
        v-if="unassignedFamilies.length > 0"
        severity="warn"
        :closable="false"
        class="mb-4"
      >
        <span class="font-medium">{{ unassignedFamilies.length }} familia(s) sin asignar:</span>
        {{ unassignedFamilies.map((f) => f.familyName).join(', ') }}
      </Message>

      <Message
        v-else
        severity="success"
        :closable="false"
        class="mb-4"
      >
        Todas las familias están asignadas.
      </Message>

      <DataTable
        v-model:expanded-rows="expandedRows"
        :value="byZoneData"
        :loading="loading"
        data-key="groupKey"
        row-hover
        class="text-sm"
      >
        <Column expander style="width: 3rem" />
        <Column field="groupLabel" header="Zona" />
        <Column header="Capacidad total">
          <template #body="{ data }">
            {{ data.totalCapacity > 0 ? data.totalCapacity : '—' }}
          </template>
        </Column>
        <Column header="Ocupación">
          <template #body="{ data }">
            <span :class="data.usedCapacity > data.totalCapacity ? 'font-bold text-red-600' : ''">
              {{ data.usedCapacity }}
            </span>
          </template>
        </Column>
        <Column header="% ocupación">
          <template #body="{ data }">
            {{
              data.totalCapacity > 0
                ? Math.round((data.usedCapacity / data.totalCapacity) * 100)
                : 0
            }}%
          </template>
        </Column>
        <Column header="Familias">
          <template #body="{ data }">
            {{ data.families.length }}
          </template>
        </Column>

        <template #expansion="{ data }">
          <div class="p-2">
            <DataTable :value="data.families" size="small">
              <Column field="familyName" header="Familia" />
              <Column field="representativeName" header="Representante" />
              <Column field="memberCount" header="Personas" style="width: 6rem" />
              <Column field="accommodationName" header="Alojamiento">
                <template #body="{ data: row }">
                  {{ row.accommodationName ?? '—' }}
                </template>
              </Column>
            </DataTable>
          </div>
        </template>
      </DataTable>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import { api } from '@/utils/api'
import type { AssignmentReportGroupResponse } from '@/types/accommodation-assignment'
import type { ApiResponse } from '@/types/api'

const props = defineProps<{
  proposalId: string
  campEditionId: string
}>()

const byTypeData = ref<AssignmentReportGroupResponse[]>([])
const byZoneData = ref<AssignmentReportGroupResponse[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const expandedRowsByType = ref<Record<string, boolean>>({})
const expandedRowsByZone = ref<Record<string, boolean>>({})

async function loadData(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [typeRes, zoneRes] = await Promise.all([
      api.get<ApiResponse<AssignmentReportGroupResponse[]>>(
        `/camps/editions/${props.campEditionId}/assignment-proposals/${props.proposalId}/reports/by-type`
      ),
      api.get<ApiResponse<AssignmentReportGroupResponse[]>>(
        `/camps/editions/${props.campEditionId}/assignment-proposals/${props.proposalId}/reports/by-zone`
      )
    ])
    byTypeData.value = typeRes.data.data ?? []
    byZoneData.value = zoneRes.data.data ?? []
  } catch {
    error.value = 'Error al cargar los informes'
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

    <Message v-else-if="error" severity="error" :closable="false">
      {{ error }}
    </Message>

    <Tabs v-else value="by-type">
      <TabList>
        <Tab value="by-type">Por tipo de alojamiento</Tab>
        <Tab value="by-zone">Por zona</Tab>
      </TabList>

      <TabPanels>
        <TabPanel value="by-type">
          <DataTable
            v-model:expanded-rows="expandedRowsByType"
            :value="byTypeData"
            data-key="groupKey"
            row-hover
            class="mt-2 text-sm"
          >
            <Column expander style="width: 3rem" />
            <Column field="groupLabel" header="Tipo" />
            <Column header="Capacidad total">
              <template #body="{ data }">
                {{ data.totalCapacity > 0 ? data.totalCapacity : '—' }}
              </template>
            </Column>
            <Column header="Ocupación">
              <template #body="{ data }">
                <span
                  :class="data.usedCapacity > data.totalCapacity ? 'font-bold text-red-600' : ''"
                >{{ data.usedCapacity }}</span>
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
              <template #body="{ data }">{{ data.families.length }}</template>
            </Column>
            <template #expansion="{ data }">
              <div class="p-2">
                <DataTable :value="data.families" size="small">
                  <Column field="familyName" header="Familia" />
                  <Column field="representativeName" header="Representante" />
                  <Column field="memberCount" header="Personas" style="width: 6rem" />
                  <Column field="accommodationName" header="Alojamiento">
                    <template #body="{ data: row }">{{ row.accommodationName ?? '—' }}</template>
                  </Column>
                  <Column field="zoneName" header="Zona">
                    <template #body="{ data: row }">{{ row.zoneName ?? '—' }}</template>
                  </Column>
                </DataTable>
              </div>
            </template>
          </DataTable>
        </TabPanel>

        <TabPanel value="by-zone">
          <DataTable
            v-model:expanded-rows="expandedRowsByZone"
            :value="byZoneData"
            data-key="groupKey"
            row-hover
            class="mt-2 text-sm"
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
                <span
                  :class="data.usedCapacity > data.totalCapacity ? 'font-bold text-red-600' : ''"
                >{{ data.usedCapacity }}</span>
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
              <template #body="{ data }">{{ data.families.length }}</template>
            </Column>
            <template #expansion="{ data }">
              <div class="p-2">
                <DataTable :value="data.families" size="small">
                  <Column field="familyName" header="Familia" />
                  <Column field="representativeName" header="Representante" />
                  <Column field="memberCount" header="Personas" style="width: 6rem" />
                  <Column field="accommodationName" header="Alojamiento">
                    <template #body="{ data: row }">{{ row.accommodationName ?? '—' }}</template>
                  </Column>
                </DataTable>
              </div>
            </template>
          </DataTable>
        </TabPanel>
      </TabPanels>
    </Tabs>
    <!-- TODO: Export to CSV/Excel -->
  </div>
</template>

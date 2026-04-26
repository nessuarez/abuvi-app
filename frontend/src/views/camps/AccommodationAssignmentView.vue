<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import ProgressSpinner from 'primevue/progressspinner'
import Toast from 'primevue/toast'
import { useAccommodationAssignment } from '@/composables/useAccommodationAssignment'
import ProposalSelectorBar from '@/components/camps/ProposalSelectorBar.vue'
import AccommodationAssignmentPanel from '@/components/camps/AccommodationAssignmentPanel.vue'
import AccommodationSummaryPanel from '@/components/camps/AccommodationSummaryPanel.vue'
import AccommodationReportsPanel from '@/components/camps/AccommodationReportsPanel.vue'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const campEditionId = computed(() => route.params.campEditionId as string)

const {
  proposals,
  selectedProposalId,
  assignmentState,
  selectedRegistrationId,
  loading,
  saving,
  error,
  assignmentsMap,
  loadProposals,
  loadAssignmentState,
  createProposal,
  activateProposal,
  deleteProposal,
  assignFamily,
  unassignFamily,
  autoAssign
} = useAccommodationAssignment(campEditionId)

async function handleAssign(registrationId: string, accommodationId: string) {
  await assignFamily(registrationId, accommodationId)
  if (error.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 4000 })
    error.value = null
  }
}

async function handleAutoAssign() {
  await autoAssign(false)
  if (error.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 4000 })
    error.value = null
  } else {
    toast.add({ severity: 'success', summary: 'Auto-asignado', detail: 'Familias asignadas automáticamente', life: 3000 })
  }
}

async function handleCreate(payload: { name: string; notes: string | null; copyFromId?: string }) {
  await createProposal(payload.name, payload.notes, payload.copyFromId)
  if (error.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 4000 })
    error.value = null
  }
}

async function handleActivate(proposalId: string) {
  await activateProposal(proposalId)
  if (error.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 4000 })
    error.value = null
  }
}

async function handleDelete(proposalId: string) {
  await deleteProposal(proposalId)
  if (error.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 4000 })
    error.value = null
  }
}

watch(selectedProposalId, (id) => {
  if (id) loadAssignmentState()
})

onMounted(async () => {
  await loadProposals()
  await loadAssignmentState()
})
</script>

<template>
  <div class="flex h-screen flex-col overflow-hidden">
    <Toast />

    <!-- Sticky top bar -->
    <div class="shrink-0 border-b bg-white shadow-sm">
      <div class="flex items-center gap-3 px-4 py-2">
        <Button icon="pi pi-arrow-left" text size="small" @click="router.back()" />
        <h1 class="text-base font-semibold">Distribución de Alojamientos</h1>
      </div>
      <ProposalSelectorBar
        v-model="selectedProposalId"
        :proposals="proposals"
        :saving="saving"
        @create="handleCreate"
        @activate="handleActivate"
        @delete="handleDelete"
        @auto-assign="handleAutoAssign"
      />
    </div>

    <!-- Tabs -->
    <Tabs value="assign" class="flex min-h-0 flex-1 flex-col overflow-hidden">
      <TabList class="shrink-0 bg-white px-4">
        <Tab value="assign">
          <i class="pi pi-objects-column mr-2" />Asignar
        </Tab>
        <Tab value="summary">
          <i class="pi pi-chart-bar mr-2" />Resumen
        </Tab>
        <Tab value="reports">
          <i class="pi pi-file-edit mr-2" />Informes
        </Tab>
      </TabList>

      <TabPanels class="min-h-0 flex-1 overflow-hidden p-0">
        <TabPanel value="assign" class="h-full overflow-hidden">
          <div v-if="loading" class="flex h-full items-center justify-center">
            <ProgressSpinner />
          </div>
          <AccommodationAssignmentPanel
            v-else-if="assignmentState"
            :state="assignmentState"
            :assignments-map="assignmentsMap"
            :selected-registration-id="selectedRegistrationId"
            :saving="saving"
            @select-family="selectedRegistrationId = $event"
            @assign="handleAssign"
            @unassign="unassignFamily"
          />
          <div v-else class="flex h-full items-center justify-center text-gray-400">
            Selecciona una propuesta para comenzar
          </div>
        </TabPanel>

        <TabPanel value="summary" class="overflow-y-auto">
          <AccommodationSummaryPanel
            v-if="selectedProposalId"
            :proposal-id="selectedProposalId"
            :camp-edition-id="campEditionId"
          />
          <div v-else class="flex h-full items-center justify-center text-gray-400 p-8">
            Selecciona una propuesta para ver el resumen
          </div>
        </TabPanel>

        <TabPanel value="reports" class="overflow-y-auto">
          <AccommodationReportsPanel
            v-if="selectedProposalId"
            :proposal-id="selectedProposalId"
            :camp-edition-id="campEditionId"
          />
          <div v-else class="flex h-full items-center justify-center text-gray-400 p-8">
            Selecciona una propuesta para ver los informes
          </div>
        </TabPanel>
      </TabPanels>
    </Tabs>
  </div>
</template>

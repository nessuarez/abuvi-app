<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useDebounceFn } from '@vueuse/core'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import type { DataTablePageEvent } from 'primevue/datatable'

const router = useRouter()
const { allFamilyUnits, familyUnitsPagination, loading, error, fetchAllFamilyUnits } =
  useFamilyUnits()

const searchQuery = ref('')

const debouncedSearch = useDebounceFn((val: string) => {
  fetchAllFamilyUnits({ search: val, page: 1 })
}, 300)

watch(searchQuery, debouncedSearch)

onMounted(() => { fetchAllFamilyUnits() })

const onPage = (event: DataTablePageEvent) => {
  fetchAllFamilyUnits({ page: event.page + 1, pageSize: event.rows, search: searchQuery.value })
}

const formatDate = (dateStr: string) =>
  new Date(dateStr).toLocaleDateString('es-ES', {
    year: 'numeric', month: 'short', day: 'numeric'
  })
</script>

<template>
  <div data-testid="family-units-admin-panel" class="space-y-4">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-xl font-semibold text-gray-800">Unidades Familiares</h2>
      <div class="flex items-center gap-2">
        <span class="p-input-icon-left">
          <i class="pi pi-search" />
          <InputText
            v-model="searchQuery"
            placeholder="Buscar familia..."
            class="w-64"
          />
        </span>
      </div>
    </div>

    <!-- Loading state -->
    <div v-if="loading && allFamilyUnits.length === 0" class="flex justify-center py-12">
      <ProgressSpinner />
    </div>

    <!-- Error state -->
    <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
      {{ error }}
      <Button
        label="Reintentar"
        text
        size="small"
        class="ml-2"
        @click="fetchAllFamilyUnits()"
      />
    </Message>

    <!-- Data Table -->
    <DataTable
      v-else
      :value="allFamilyUnits"
      lazy
      paginator
      :rows="familyUnitsPagination.pageSize"
      :total-records="familyUnitsPagination.totalCount"
      striped-rows
      class="rounded-lg"
      @page="onPage"
    >
      <Column field="name" header="Nombre Familia" sortable>
        <template #body="{ data }">
          <span class="font-medium">{{ data.name }}</span>
        </template>
      </Column>
      <Column field="representativeName" header="Representante">
        <template #body="{ data }">
          <span class="text-gray-600">{{ data.representativeName || '—' }}</span>
        </template>
      </Column>
      <Column field="membersCount" header="Miembros">
        <template #body="{ data }">
          <span class="text-gray-600">{{ data.membersCount ?? '—' }}</span>
        </template>
      </Column>
      <Column field="createdAt" header="Fecha Creación">
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>
      <Column header="Acciones">
        <template #body="{ data }">
          <Button
            icon="pi pi-eye"
            text
            rounded
            severity="info"
            aria-label="Ver detalle"
            v-tooltip.top="'Ver detalle'"
            @click="router.push(`/family-unit/${data.id}`)"
          />
        </template>
      </Column>
    </DataTable>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import ProgressBar from 'primevue/progressbar'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Message from 'primevue/message'
import { useBlobStorage } from '@/composables/useBlobStorage'

const { stats, statsLoading, statsError, fetchStats } = useBlobStorage()

onMounted(() => fetchStats())

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}

const progressBarColor = computed(() => {
  const pct = stats.value?.usedPct ?? 0
  if (pct >= 95) return '#ef4444'
  if (pct >= 80) return '#f59e0b'
  return '#22c55e'
})

const folderRows = computed(() =>
  Object.entries(stats.value?.byFolder ?? {}).map(([folder, s]) => ({
    folder,
    objects: s.objects,
    sizeHuman: formatBytes(s.sizeBytes)
  }))
)
</script>

<template>
  <div data-testid="blob-storage-admin-panel" class="space-y-6">
    <div class="flex items-center justify-between">
      <h2 class="text-xl font-semibold text-gray-800">Almacenamiento de Archivos</h2>
      <Button
        label="Actualizar"
        icon="pi pi-refresh"
        outlined
        :loading="statsLoading"
        data-testid="refresh-btn"
        @click="fetchStats"
      />
    </div>

    <ProgressSpinner v-if="statsLoading && !stats" data-testid="stats-spinner" />

    <Message v-if="statsError" severity="error" data-testid="stats-error">
      {{ statsError }}
    </Message>

    <template v-if="stats">
      <!-- Summary cards -->
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div
          class="rounded-lg border border-gray-200 bg-white p-4 shadow-sm"
          data-testid="card-total-objects"
        >
          <p class="text-sm text-gray-500">Total de archivos</p>
          <p class="mt-1 text-2xl font-bold text-gray-900">{{ stats.totalObjects }}</p>
        </div>

        <div
          class="rounded-lg border border-gray-200 bg-white p-4 shadow-sm"
          data-testid="card-total-size"
        >
          <p class="text-sm text-gray-500">Tamaño total</p>
          <p class="mt-1 text-2xl font-bold text-gray-900">{{ stats.totalSizeHumanReadable }}</p>
        </div>

        <div
          v-if="stats.quotaBytes"
          class="rounded-lg border border-gray-200 bg-white p-4 shadow-sm"
          data-testid="card-free-space"
        >
          <p class="text-sm text-gray-500">Espacio libre</p>
          <p class="mt-1 text-2xl font-bold text-gray-900">
            {{ stats.freeBytes != null ? formatBytes(stats.freeBytes) : '—' }}
          </p>
        </div>
      </div>

      <!-- Quota bar -->
      <div v-if="stats.quotaBytes" class="space-y-1" data-testid="quota-section">
        <div class="flex justify-between text-sm text-gray-600">
          <span>Uso de almacenamiento</span>
          <span>{{ stats.usedPct?.toFixed(1) }}%</span>
        </div>
        <ProgressBar
          :value="Math.round(stats.usedPct ?? 0)"
          :pt="{
            value: {
              style: {
                background: progressBarColor
              }
            }
          }"
          data-testid="quota-progress-bar"
        />
        <p class="text-xs text-gray-400">
          {{ stats.freeBytes != null ? formatBytes(stats.freeBytes) : '—' }} libres de
          {{ formatBytes(stats.quotaBytes) }}
        </p>
      </div>

      <!-- Folder breakdown -->
      <DataTable :value="folderRows" data-testid="folder-stats-table">
        <Column field="folder" header="Carpeta" />
        <Column field="objects" header="Objetos" />
        <Column field="sizeHuman" header="Tamaño" />
      </DataTable>
    </template>
  </div>
</template>

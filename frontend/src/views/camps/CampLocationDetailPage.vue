<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import CampLocationMap from '@/components/camps/CampLocationMap.vue'
import { useCamps } from '@/composables/useCamps'
import type { Camp } from '@/types/camp'

const route = useRoute()
const router = useRouter()
const { loading, error, getCampById } = useCamps()

const goToEditions = () => {
  router.push({ name: 'camp-editions', query: { campId: route.params.id as string } })
}

const proposeNewEdition = () => {
  router.push({
    name: 'camp-editions',
    query: { campId: route.params.id as string, action: 'propose' }
  })
}

const camp = ref<Camp | null>(null)

const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('es-ES', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0
  }).format(amount)
}

const statusLabel = (status: string): string => {
  const labels: Record<string, string> = {
    Active: 'Activo',
    Inactive: 'Inactivo',
    HistoricalArchive: 'Archivo Histórico'
  }
  return labels[status] || status
}

onMounted(async () => {
  const campId = route.params.id as string
  const result = await getCampById(campId)
  if (result) {
    camp.value = result
  }
})

const goBack = () => {
  router.push({ name: 'camp-locations' })
}
</script>

<template>
  <div class="container mx-auto p-4">
    <!-- Loading State -->
    <div v-if="loading" class="flex justify-center p-12">
      <ProgressSpinner />
    </div>

    <!-- Error State -->
    <Message v-else-if="error" severity="error" :closable="false">
      {{ error }}
      <Button label="Volver" text class="ml-2 underline" @click="goBack" />
    </Message>

    <!-- Camp Details -->
    <div v-else-if="camp">
      <!-- Header -->
      <div class="mb-6">
        <Button
          label="Volver"
          icon="pi pi-arrow-left"
          text
          class="mb-4"
          @click="goBack"
        />
        <div class="flex items-start justify-between">
          <div>
            <h1 class="mb-2 text-3xl font-bold text-gray-900">{{ camp.name }}</h1>
            <p class="text-gray-600">
              {{ camp.latitude.toFixed(4) }}, {{ camp.longitude.toFixed(4) }}
            </p>
          </div>
          <span
            :class="{
              'bg-green-100 text-green-800': camp.status === 'Active',
              'bg-gray-100 text-gray-800': camp.status === 'Inactive',
              'bg-blue-100 text-blue-800': camp.status === 'HistoricalArchive'
            }"
            class="rounded-full px-3 py-1 text-sm font-medium"
          >
            {{ statusLabel(camp.status) }}
          </span>
        </div>
      </div>

      <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <!-- Info Panel -->
        <div class="space-y-6">
          <!-- Description -->
          <div class="rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="mb-3 text-lg font-semibold text-gray-900">Descripción</h2>
            <p class="text-gray-700">{{ camp.description || 'Sin descripción' }}</p>
          </div>

          <!-- Pricing -->
          <div class="rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="mb-4 text-lg font-semibold text-gray-900">Precios Base</h2>
            <div class="space-y-3">
              <div class="flex justify-between">
                <span class="text-gray-600">Precio adulto:</span>
                <span class="font-semibold">{{ formatCurrency(camp.basePriceAdult) }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Precio niño:</span>
                <span class="font-semibold">{{ formatCurrency(camp.basePriceChild) }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Precio bebé:</span>
                <span class="font-semibold">{{ formatCurrency(camp.basePriceBaby) }}</span>
              </div>
            </div>
          </div>

          <!-- Metadata -->
          <div class="rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="mb-4 text-lg font-semibold text-gray-900">Información</h2>
            <div class="space-y-2 text-sm">
              <div class="flex justify-between">
                <span class="text-gray-600">Creado:</span>
                <span>{{ new Date(camp.createdAt).toLocaleDateString('es-ES') }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Última actualización:</span>
                <span>{{ new Date(camp.updatedAt).toLocaleDateString('es-ES') }}</span>
              </div>
            </div>
          </div>

          <!-- Editions -->
          <div class="rounded-lg border border-gray-200 bg-white p-6">
            <div class="mb-4 flex items-center justify-between">
              <h2 class="text-lg font-semibold text-gray-900">Ediciones</h2>
              <span
                v-if="camp.editionCount !== undefined"
                class="rounded-full bg-gray-100 px-2 py-0.5 text-sm text-gray-600"
              >
                {{ camp.editionCount }} {{ camp.editionCount === 1 ? 'edición' : 'ediciones' }}
              </span>
            </div>
            <div class="flex flex-col gap-2 sm:flex-row">
              <Button
                label="Ver ediciones"
                icon="pi pi-list"
                outlined
                class="flex-1"
                data-testid="view-editions-btn"
                @click="goToEditions"
              />
              <Button
                label="Nueva propuesta"
                icon="pi pi-plus"
                class="flex-1"
                data-testid="propose-edition-btn"
                @click="proposeNewEdition"
              />
            </div>
          </div>
        </div>

        <!-- Map Panel -->
        <div>
          <div class="rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="mb-4 text-lg font-semibold text-gray-900">Ubicación</h2>
            <CampLocationMap
              :locations="[
                {
                  latitude: camp.latitude,
                  longitude: camp.longitude,
                  name: camp.name
                }
              ]"
            />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

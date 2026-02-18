<script setup lang="ts">
import { useRouter } from 'vue-router'
import Card from 'primevue/card'
import Button from 'primevue/button'

const router = useRouter()

const goToCampLocations = () => router.push('/camps/locations')
const goToCampEditions = () => router.push('/camps/editions')
</script>

<template>
  <div data-testid="camps-admin-panel" class="space-y-4">
    <div class="flex items-center justify-between">
      <h2 class="text-xl font-semibold text-gray-800">Gestión de Campamentos</h2>
    </div>

    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <!-- Editions card -->
      <Card
        class="cursor-pointer transition-shadow hover:shadow-md"
        data-testid="editions-card"
        @click="goToCampEditions"
      >
        <template #content>
          <div class="flex flex-col items-center py-6 text-center">
            <i class="pi pi-calendar mb-4 text-4xl text-primary-400" />
            <p class="text-lg font-semibold text-gray-800">Ediciones de Campamento</p>
            <p class="mt-1 text-sm text-gray-500">
              Gestiona el ciclo de vida de las ediciones: propuestas, apertura y cierre.
            </p>
            <Button
              label="Gestionar ediciones"
              icon="pi pi-arrow-right"
              icon-pos="right"
              outlined
              class="mt-4"
              data-testid="go-to-editions-btn"
              @click.stop="goToCampEditions"
            />
          </div>
        </template>
      </Card>

      <!-- Locations card -->
      <Card
        class="cursor-pointer transition-shadow hover:shadow-md"
        data-testid="locations-card"
        @click="goToCampLocations"
      >
        <template #content>
          <div class="flex flex-col items-center py-6 text-center">
            <i class="pi pi-map-marker mb-4 text-4xl text-primary-400" />
            <p class="text-lg font-semibold text-gray-800">Ubicaciones de Campamento</p>
            <p class="mt-1 text-sm text-gray-500">
              Gestiona los lugares físicos donde se celebran los campamentos.
            </p>
            <Button
              label="Gestionar ubicaciones"
              icon="pi pi-arrow-right"
              icon-pos="right"
              outlined
              class="mt-4"
              data-testid="go-to-locations-btn"
              @click.stop="goToCampLocations"
            />
          </div>
        </template>
      </Card>
    </div>
  </div>
</template>

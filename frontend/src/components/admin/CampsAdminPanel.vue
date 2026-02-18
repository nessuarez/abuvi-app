<script setup lang="ts">
import { useRouter } from 'vue-router'
import Card from 'primevue/card'
import Button from 'primevue/button'

const router = useRouter()

const goToCampLocations = () => router.push('/camps/locations')
</script>

<template>
  <div data-testid="camps-admin-panel" class="space-y-4">
    <div class="flex items-center justify-between">
      <h2 class="text-xl font-semibold text-gray-800">Gestión de Campamentos</h2>
      <Button
        label="Ver ubicaciones"
        icon="pi pi-map-marker"
        outlined
        @click="goToCampLocations"
      />
    </div>

    <Card>
      <template #content>
        <div class="py-8 text-center text-gray-500">
          <i class="pi pi-tent mb-4 text-4xl text-gray-300" />
          <p class="text-lg font-medium">Gestión de ediciones de campamento</p>
          <p class="mt-2 text-sm">
            La gestión completa de ediciones estará disponible próximamente.
          </p>
          <Button
            label="Gestionar ubicaciones de campamento"
            icon="pi pi-map-marker"
            class="mt-4"
            @click="goToCampLocations"
          />
        </div>
      </template>
    </Card>
  </div>
</template>

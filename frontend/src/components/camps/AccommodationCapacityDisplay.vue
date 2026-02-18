<script setup lang="ts">
import Card from 'primevue/card'
import type { AccommodationCapacity } from '@/types/camp'

interface Props {
  capacity: AccommodationCapacity | null | undefined
  totalBedCapacity?: number | null
}

defineProps<Props>()
</script>

<template>
  <Card v-if="capacity">
    <template #title>
      <div class="flex items-center gap-2">
        <i class="pi pi-home text-primary-600" />
        <span>Capacidad de alojamiento</span>
        <span
          v-if="totalBedCapacity"
          class="ml-auto text-sm font-normal text-gray-500"
        >
          {{ totalBedCapacity }} camas estimadas
        </span>
      </div>
    </template>

    <template #content>
      <dl class="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
        <template v-if="capacity.privateRoomsWithBathroom">
          <dt class="text-gray-500">Hab. privadas con baño:</dt>
          <dd class="font-medium">{{ capacity.privateRoomsWithBathroom }}</dd>
        </template>

        <template v-if="capacity.privateRoomsSharedBathroom">
          <dt class="text-gray-500">Hab. privadas baño compartido:</dt>
          <dd class="font-medium">{{ capacity.privateRoomsSharedBathroom }}</dd>
        </template>

        <template v-if="capacity.bungalows">
          <dt class="text-gray-500">Bungalows / Casetas:</dt>
          <dd class="font-medium">{{ capacity.bungalows }}</dd>
        </template>

        <template v-if="capacity.campOwnedTents">
          <dt class="text-gray-500">Tiendas del camping:</dt>
          <dd class="font-medium">{{ capacity.campOwnedTents }}</dd>
        </template>

        <template v-if="capacity.memberTentAreaSquareMeters">
          <dt class="text-gray-500">Área tiendas socios:</dt>
          <dd class="font-medium">{{ capacity.memberTentAreaSquareMeters }} m²</dd>
        </template>

        <template v-if="capacity.memberTentCapacityEstimate">
          <dt class="text-gray-500">Capacidad estimada tiendas socios:</dt>
          <dd class="font-medium">{{ capacity.memberTentCapacityEstimate }} personas</dd>
        </template>

        <template v-if="capacity.motorhomeSpots">
          <dt class="text-gray-500">Plazas autocaravanas:</dt>
          <dd class="font-medium">{{ capacity.motorhomeSpots }}</dd>
        </template>
      </dl>

      <!-- Shared rooms breakdown -->
      <div v-if="capacity.sharedRooms?.length" class="mt-4">
        <p class="mb-2 text-sm font-medium text-gray-700">Habitaciones compartidas:</p>
        <ul class="space-y-1">
          <li
            v-for="(room, index) in capacity.sharedRooms"
            :key="index"
            class="flex flex-wrap items-center gap-2 text-sm text-gray-600"
          >
            <span>{{ room.quantity }} hab. × {{ room.bedsPerRoom }} camas</span>
            <span v-if="room.hasBathroom" class="rounded bg-blue-50 px-1.5 py-0.5 text-xs text-blue-700">baño</span>
            <span v-if="room.hasShower" class="rounded bg-cyan-50 px-1.5 py-0.5 text-xs text-cyan-700">ducha</span>
            <span v-if="room.notes" class="text-xs italic text-gray-400">— {{ room.notes }}</span>
          </li>
        </ul>
      </div>

      <p v-if="capacity.notes" class="mt-3 text-sm italic text-gray-500">
        {{ capacity.notes }}
      </p>
    </template>
  </Card>
</template>

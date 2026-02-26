<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Toast from 'primevue/toast'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import CampLocationMap from '@/components/camps/CampLocationMap.vue'
import AccommodationCapacityDisplay from '@/components/camps/AccommodationCapacityDisplay.vue'
import CampPhotoGallery from '@/components/camps/CampPhotoGallery.vue'
import CampContactInfo from '@/components/camps/CampContactInfo.vue'
import CampPlacesGallery from '@/components/camps/CampPlacesGallery.vue'
import CampEditionProposeDialog from '@/components/camps/CampEditionProposeDialog.vue'
import { useCamps } from '@/composables/useCamps'
import { useAuthStore } from '@/stores/auth'
import type { CampDetailResponse } from '@/types/camp'
import type { CampPhoto } from '@/types/camp-photo'
import type { CampEdition } from '@/types/camp-edition'
const route = useRoute()
const router = useRouter()
const toast = useToast()
const auth = useAuthStore()
const { loading, error, getCampById } = useCamps()

const showProposeDialog = ref(false)

const goToEditions = () => {
  router.push({ name: 'camp-editions', query: { campId: route.params.id as string } })
}

const proposeNewEdition = () => {
  showProposeDialog.value = true
}

const handleEditionProposed = (_edition: CampEdition) => {
  toast.add({
    severity: 'success',
    summary: 'Propuesta enviada',
    detail: 'La propuesta de edición ha sido enviada correctamente.',
    life: 4000
  })
}

const camp = ref<CampDetailResponse | null>(null)
const campPhotos = ref<CampPhoto[]>([])

const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('es-ES', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0
  }).format(amount)
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

const handlePhotosChanged = (updatedPhotos: CampPhoto[]) => {
  campPhotos.value = updatedPhotos
}
</script>

<template>
  <div class="container mx-auto p-4">
    <Toast />

    <!-- Propose Edition Dialog -->
    <CampEditionProposeDialog
      v-model:visible="showProposeDialog"
      :camp-id="route.params.id as string"
      :camp="camp"
      @saved="handleEditionProposed"
    />

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
            <p v-if="camp.latitude !== null && camp.longitude !== null" class="text-gray-600">
              {{ camp.latitude.toFixed(4) }}, {{ camp.longitude.toFixed(4) }}
            </p>
          </div>
          <span
            :class="camp.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'"
            class="rounded-full px-3 py-1 text-sm font-medium"
          >
            {{ camp.isActive ? 'Activo' : 'Inactivo' }}
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

          <!-- Contact Information (Google Places) -->
          <CampContactInfo :camp="camp" />

          <!-- Pricing -->
          <div class="rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="mb-4 text-lg font-semibold text-gray-900">Precios Base</h2>
            <div class="space-y-3">
              <div class="flex justify-between">
                <span class="text-gray-600">Precio adulto:</span>
                <span class="font-semibold">{{ formatCurrency(camp.pricePerAdult) }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Precio niño:</span>
                <span class="font-semibold">{{ formatCurrency(camp.pricePerChild) }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Precio bebé:</span>
                <span class="font-semibold">{{ formatCurrency(camp.pricePerBaby) }}</span>
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
              v-if="camp.latitude !== null && camp.longitude !== null"
              :locations="[
                {
                  latitude: camp.latitude,
                  longitude: camp.longitude,
                  name: camp.name
                }
              ]"
            />
            <p v-else class="text-sm italic text-gray-500">Sin ubicación registrada</p>
          </div>

          <!-- Photo Gallery (Google Places) -->
          <CampPlacesGallery
            v-if="camp.photos.length > 0"
            :photos="camp.photos"
            class="mt-6"
          />
        </div>
      </div>

      <!-- Accommodation Capacity Section -->
      <div v-if="camp.accommodationCapacity" class="mt-6">
        <AccommodationCapacityDisplay
          :capacity="camp.accommodationCapacity"
          :total-bed-capacity="camp.calculatedTotalBedCapacity"
        />
      </div>

      <!-- Photo Gallery Section (Board+ only) -->
      <div v-if="auth.isBoard" class="mt-6">
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <CampPhotoGallery
            :camp-id="camp.id"
            :initial-photos="campPhotos"
            @photos-changed="handlePhotosChanged"
          />
        </div>
      </div>
    </div>
  </div>
</template>

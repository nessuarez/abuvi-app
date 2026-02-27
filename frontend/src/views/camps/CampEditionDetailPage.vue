<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Container from '@/components/ui/Container.vue'
import CampEditionStatusBadge from '@/components/camps/CampEditionStatusBadge.vue'
import CampEditionAccommodationsPanel from '@/components/camps/CampEditionAccommodationsPanel.vue'
import CampEditionExtrasList from '@/components/camps/CampEditionExtrasList.vue'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import { useCampEditions } from '@/composables/useCampEditions'
import { useAuthStore } from '@/stores/auth'
import type { CampEdition } from '@/types/camp-edition'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const { loading, error, getEditionById } = useCampEditions()

const edition = ref<CampEdition | null>(null)
const isBoard = computed(() => auth.user?.role === 'Admin' || auth.user?.role === 'Board')

const formatDate = (dateStr: string): string =>
  new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(
    new Date(dateStr)
  )

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR', minimumFractionDigits: 0 }).format(amount)

onMounted(async () => {
  edition.value = await getEditionById(route.params.id as string)
})
</script>

<template>
  <Container>
    <div class="py-8">
      <Button label="Volver" icon="pi pi-arrow-left" text class="mb-4" @click="router.back()" />

      <div v-if="loading" class="flex justify-center py-12">
        <ProgressSpinner />
      </div>

      <Message v-else-if="error" severity="error" :closable="false">
        {{ error }}
      </Message>

      <div v-else-if="!edition" class="rounded-lg border border-gray-200 bg-gray-50 p-8 text-center">
        <p class="text-gray-500">Edición no encontrada.</p>
        <Button label="Volver" text class="mt-2" @click="router.back()" />
      </div>

      <div v-else>
        <div class="mb-6">
          <h1 class="text-3xl font-bold text-gray-900">
            Edición {{ edition.year }}
            <span v-if="edition.name"> — {{ edition.name }}</span>
          </h1>
          <p class="mt-1 text-gray-500">{{ edition.location }}</p>
        </div>

        <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
          <!-- Dates & Status -->
          <div class="space-y-4 rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="text-lg font-semibold text-gray-900">Información General</h2>
            <div class="space-y-2 text-sm">
              <div class="flex justify-between">
                <span class="text-gray-600">Estado:</span>
                <CampEditionStatusBadge :status="edition.status" size="sm" />
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Año:</span>
                <span>{{ edition.year }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Fecha inicio:</span>
                <span>{{ formatDate(edition.startDate) }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Fecha fin:</span>
                <span>{{ formatDate(edition.endDate) }}</span>
              </div>
              <div v-if="edition.maxCapacity" class="flex justify-between">
                <span class="text-gray-600">Capacidad máxima:</span>
                <span>{{ edition.maxCapacity }}</span>
              </div>
            </div>
          </div>

          <!-- Pricing -->
          <div class="space-y-4 rounded-lg border border-gray-200 bg-white p-6">
            <h2 class="text-lg font-semibold text-gray-900">Precios</h2>
            <div class="space-y-2 text-sm">
              <div class="flex justify-between">
                <span class="text-gray-600">Precio adulto:</span>
                <span class="font-semibold">{{ formatCurrency(edition.pricePerAdult) }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Precio niño:</span>
                <span class="font-semibold">{{ formatCurrency(edition.pricePerChild) }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-gray-600">Precio bebé:</span>
                <span class="font-semibold">{{ formatCurrency(edition.pricePerBaby) }}</span>
              </div>
            </div>
          </div>
        </div>

        <div v-if="edition.description" class="mt-6 rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-2 text-lg font-semibold text-gray-900">Descripción</h2>
          <p class="text-sm text-gray-700">{{ edition.description }}</p>
        </div>

        <CampEditionAccommodationsPanel
          v-if="isBoard"
          :edition-id="edition.id"
          class="mt-6"
        />

        <!-- Camp Edition Extras -->
        <div class="mt-6 rounded-lg border border-gray-200 bg-white p-6" data-testid="edition-extras-section">
          <CampEditionExtrasList
            :edition-id="edition.id"
            :edition-status="edition.status"
          />
        </div>
      </div>
    </div>
  </Container>
</template>

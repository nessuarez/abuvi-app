<script setup lang="ts">
import { onMounted, computed } from 'vue'
import { useCampEditions } from '@/composables/useCampEditions'
import Container from '@/components/ui/Container.vue'
import CampEditionDetails from '@/components/camps/CampEditionDetails.vue'
import CampStatusBadge from '@/components/camps/CampStatusBadge.vue'
import Card from 'primevue/card'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'

const { currentCampEdition, loading, error, fetchCurrentCampEdition } = useCampEditions()

onMounted(() => { fetchCurrentCampEdition() })

const displayTitle = computed(() =>
  currentCampEdition.value ? `Campamento ${currentCampEdition.value.year}` : 'Campamento'
)

const isFromPreviousYear = computed(() => {
  if (!currentCampEdition.value) return false
  return currentCampEdition.value.year < new Date().getFullYear()
})
</script>

<template>
  <Container>
    <div class="py-8">
      <!-- Loading state -->
      <div
        v-if="loading"
        class="flex justify-center py-16"
        data-testid="camp-loading"
        role="status"
      >
        <ProgressSpinner />
      </div>

      <!-- Error state -->
      <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
        {{ error }}
      </Message>

      <!-- Empty state (no camp edition found) -->
      <div v-else-if="!currentCampEdition" data-testid="camp-empty">
        <Message severity="info" :closable="false">
          No hay información de campamento disponible para este año. Contacta con la junta directiva
          para más información.
        </Message>
      </div>

      <!-- Camp edition content -->
      <div v-else data-testid="camp-page-content">
        <div class="mb-6 flex flex-wrap items-center gap-3">
          <h1 class="text-4xl font-bold text-gray-900">{{ displayTitle }}</h1>
          <CampStatusBadge :status="currentCampEdition.status" />
        </div>

        <!-- Previous year warning -->
        <Message
          v-if="isFromPreviousYear"
          severity="warn"
          :closable="false"
          class="mb-4"
        >
          Mostrando información del campamento de {{ currentCampEdition.year }}
        </Message>

        <!-- Camp details -->
        <CampEditionDetails :camp-edition="currentCampEdition" />

        <!-- Registration CTA (only when Open) -->
        <Card v-if="currentCampEdition.status === 'Open'" class="mt-6 border-l-4 border-green-500">
          <template #title>
            <div class="flex items-center gap-2 text-green-700">
              <i class="pi pi-check-circle" />
              Inscripciones Abiertas
            </div>
          </template>
          <template #content>
            <p class="mb-3 text-sm text-gray-700">
              <span v-if="currentCampEdition.availableSpots !== undefined">
                Quedan <strong>{{ currentCampEdition.availableSpots }}</strong> plazas disponibles.
              </span>
              Las inscripciones están abiertas. Contacta con la junta directiva para inscribir a tu
              familia.
            </p>
          </template>
        </Card>
      </div>
    </div>
  </Container>
</template>

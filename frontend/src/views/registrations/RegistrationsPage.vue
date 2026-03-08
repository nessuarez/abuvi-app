<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Button from 'primevue/button'
import Container from '@/components/ui/Container.vue'
import RegistrationCard from '@/components/registrations/RegistrationCard.vue'
import { useRegistrations } from '@/composables/useRegistrations'
import type { RegistrationListItem } from '@/types/registration'

const router = useRouter()
const { registrations, loading, error, fetchMyRegistrations } = useRegistrations()

const sortedRegistrations = computed<RegistrationListItem[]>(() => {
  const active = registrations.value.filter(
    (r) => r.status === 'Pending' || r.status === 'Confirmed'
  )
  const cancelled = registrations.value.filter((r) => r.status === 'Cancelled')
  return [...active, ...cancelled]
})

const navigateToDetail = (id: string) => {
  router.push({ name: 'registration-detail', params: { id } })
}

onMounted(() => fetchMyRegistrations())
</script>

<template>
  <Container>
    <div class="py-8">
      <div class="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 class="text-3xl font-bold text-gray-900">Mis Inscripciones</h1>
        <Button
          label="Ver campamento"
          icon="pi pi-arrow-right"
          icon-pos="right"
          severity="secondary"
          @click="router.push({ name: 'camp' })"
        />
      </div>

      <!-- Loading -->
      <div v-if="loading" class="flex justify-center py-12" role="status">
        <ProgressSpinner />
      </div>

      <!-- Error -->
      <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
        {{ error }}
      </Message>

      <!-- Empty state -->
      <div
        v-else-if="registrations.length === 0"
        class="rounded-lg border border-gray-200 bg-gray-50 p-8 text-center"
        data-testid="registrations-empty"
      >
        <p class="text-gray-600">Todavía no tienes inscripciones.</p>
        <p class="mt-1 text-sm text-gray-400">
          Cuando haya un campamento abierto, podrás inscribirte desde la página del campamento.
        </p>
        <Button
          label="Ir al campamento"
          icon="pi pi-arrow-right"
          icon-pos="right"
          class="mt-4"
          @click="router.push({ name: 'camp' })"
        />
      </div>

      <!-- List -->
      <div v-else class="flex flex-col gap-4">
        <RegistrationCard
          v-for="registration in sortedRegistrations"
          :key="registration.id"
          :registration="registration"
          @view="navigateToDetail"
        />
      </div>
    </div>
  </Container>
</template>

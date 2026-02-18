<script setup lang="ts">
import { onMounted } from 'vue'
import Container from '@/components/ui/Container.vue'
import ActiveEditionCard from '@/components/camps/ActiveEditionCard.vue'
import { useCampEditions } from '@/composables/useCampEditions'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'

const { activeEdition, loading, error, getActiveEdition } = useCampEditions()

onMounted(() => getActiveEdition())
</script>

<template>
  <Container>
    <div class="py-8">
      <h1 class="mb-6 text-3xl font-bold text-gray-900">
        Campamento {{ new Date().getFullYear() }}
      </h1>

      <div v-if="loading" class="flex justify-center py-12" data-testid="camp-loading" role="status">
        <ProgressSpinner />
      </div>

      <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
        {{ error }}
      </Message>

      <div v-else-if="activeEdition">
        <ActiveEditionCard :edition="activeEdition" />
      </div>

      <div v-else class="rounded-lg border border-gray-200 bg-gray-50 p-8 text-center" data-testid="camp-empty">
        <p class="text-gray-500">No hay ningún campamento abierto para este año.</p>
        <p class="mt-1 text-sm text-gray-400">
          Cuando haya una edición disponible, aparecerá aquí.
        </p>
      </div>
    </div>
  </Container>
</template>

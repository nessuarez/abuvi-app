<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
import { useRoute, useRouter } from 'vue-router'
import { onMounted } from 'vue'

const route = useRoute()
const router = useRouter()

onMounted(() => {
  // TODO: feat-camp-editions-management — apply campId filter and action=propose from query params
  // if (route.query.campId) { filters.campId = route.query.campId as string }
  // if (route.query.action === 'propose') { showProposalDialog.value = true }
  // router.replace({ name: 'camp-editions' })
})
</script>

<template>
  <Container>
    <div class="py-8">
      <div class="mb-6 flex items-center justify-between">
        <h1 class="text-3xl font-bold text-gray-900">Gestión de Ediciones de Campamento</h1>
      </div>

      <div class="rounded-lg border-2 border-dashed border-gray-300 p-12 text-center">
        <i class="pi pi-calendar mb-4 text-4xl text-gray-400" />
        <p class="text-lg font-semibold text-gray-700">Ediciones de Campamento</p>
        <p class="mt-2 text-sm text-gray-500">
          La gestión completa de ediciones se implementará próximamente.
        </p>
        <p v-if="route.query.campId" class="mt-2 text-xs text-primary-600">
          Filtrando por ubicación: {{ route.query.campId }}
        </p>
      </div>
    </div>
  </Container>
</template>

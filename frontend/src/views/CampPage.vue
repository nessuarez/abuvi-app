<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import Container from '@/components/ui/Container.vue'
import ActiveEditionCard from '@/components/camps/ActiveEditionCard.vue'
import { useCampEditions } from '@/composables/useCampEditions'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import { useAuthStore } from '@/stores/auth'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Button from 'primevue/button'

const router = useRouter()
const auth = useAuthStore()
const { activeEdition, loading, error, getActiveEdition } = useCampEditions()
const { familyUnit, getCurrentUserFamilyUnit } = useFamilyUnits()

const isRepresentative = computed(
  () => !!familyUnit.value && familyUnit.value.representativeUserId === auth.user?.id
)

const goToRegister = () => {
  if (activeEdition.value) {
    router.push({ name: 'registration-new', params: { editionId: activeEdition.value.id } })
  }
}

onMounted(() => {
  getActiveEdition()
  getCurrentUserFamilyUnit()
})
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

        <!-- Registration action section -->
        <div class="mt-6 flex flex-col items-start gap-3 sm:flex-row sm:items-center">
          <Button
            v-if="activeEdition.status === 'Open' && isRepresentative"
            label="Inscribirse al campamento"
            icon="pi pi-user-plus"
            size="large"
            @click="goToRegister"
            data-testid="register-button"
          />
          <Button
            v-else-if="activeEdition.status === 'Open' && !isRepresentative"
            label="Solo el representante puede inscribirse"
            icon="pi pi-info-circle"
            severity="secondary"
            size="large"
            disabled
          />
          <RouterLink
            :to="{ name: 'registrations' }"
            class="text-sm text-blue-600 underline hover:text-blue-800"
          >
            Ver mis inscripciones
          </RouterLink>
        </div>

        <!-- Non-representative note -->
        <p
          v-if="activeEdition.status === 'Open' && !isRepresentative && familyUnit"
          class="mt-2 text-sm text-amber-600"
        >
          Solo el representante de la unidad familiar puede inscribir a la familia.
        </p>
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

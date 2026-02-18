<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import Container from '@/components/ui/Container.vue'
import { useAuthStore } from '@/stores/auth'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import Card from 'primevue/card'
import Button from 'primevue/button'

const auth = useAuthStore()
const router = useRouter()
const { familyUnit, loading: familyLoading, getCurrentUserFamilyUnit } = useFamilyUnits()

onMounted(() => { getCurrentUserFamilyUnit() })

const goToFamilyManagement = () => router.push('/family-unit/me')

const translateRole = (role: string) =>
  ({ Admin: 'Administrador', Board: 'Junta Directiva', Member: 'Socio' }[role] ?? role)
</script>

<template>
  <Container>
    <div class="py-12">
      <h1 class="mb-4 text-4xl font-bold text-gray-900">Mi Perfil</h1>

      <!-- User info card -->
      <Card>
        <template #content>
          <div class="space-y-2">
            <p><strong>Nombre:</strong> {{ auth.fullName }}</p>
            <p><strong>Email:</strong> {{ auth.user?.email }}</p>
            <p><strong>Rol:</strong> {{ translateRole(auth.user?.role ?? '') }}</p>
          </div>
          <p class="mt-4 text-sm text-gray-500">
            La gestión completa del perfil se implementará en futuras iteraciones.
          </p>
        </template>
      </Card>

      <!-- Mi Unidad Familiar -->
      <Card class="mt-6">
        <template #title>
          <div class="flex items-center gap-2">
            <i class="pi pi-users" />
            Mi Unidad Familiar
          </div>
        </template>
        <template #content>
          <!-- Loading -->
          <div v-if="familyLoading" class="flex justify-center py-4">
            <i class="pi pi-spin pi-spinner text-2xl text-primary-500" />
          </div>
          <!-- No Family Unit -->
          <div v-else-if="!familyUnit" class="space-y-3 py-4 text-center">
            <p class="text-sm text-gray-600">
              Aún no has creado tu unidad familiar. Crea una para poder inscribirte en campamentos.
            </p>
            <Button
              label="Crear Unidad Familiar"
              icon="pi pi-plus"
              data-testid="create-family-unit-btn"
              @click="goToFamilyManagement"
            />
          </div>
          <!-- Has Family Unit -->
          <div v-else class="flex items-center justify-between">
            <div>
              <h3 class="text-lg font-semibold">{{ familyUnit.name }}</h3>
              <p class="text-sm text-gray-500">Unidad familiar activa</p>
            </div>
            <Button
              label="Gestionar"
              icon="pi pi-pencil"
              outlined
              data-testid="manage-family-unit-btn"
              @click="goToFamilyManagement"
            />
          </div>
        </template>
      </Card>
    </div>
  </Container>
</template>

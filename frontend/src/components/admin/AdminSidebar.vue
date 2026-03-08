<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

interface AdminMenuItem {
  label: string
  icon: string
  to: string
  testId: string
  visible: boolean
}

interface AdminMenuGroup {
  label: string
  items: AdminMenuItem[]
}

const route = useRoute()
const auth = useAuthStore()

const menuGroups = computed<AdminMenuGroup[]>(() => [
  {
    label: 'Gestión',
    items: [
      { label: 'Campamentos', icon: 'pi pi-map', to: '/admin/camps', testId: 'sidebar-camps', visible: true },
      { label: 'Inscripciones', icon: 'pi pi-list-check', to: '/admin/registrations', testId: 'sidebar-registrations', visible: true },
      { label: 'Unidades Familiares', icon: 'pi pi-users', to: '/admin/family-units', testId: 'sidebar-family-units', visible: true }
    ]
  },
  {
    label: 'Personas',
    items: [
      { label: 'Usuarios', icon: 'pi pi-user-edit', to: '/admin/users', testId: 'sidebar-users', visible: true }
    ]
  },
  {
    label: 'Contenido',
    items: [
      { label: 'Revisión de medios', icon: 'pi pi-images', to: '/admin/media-review', testId: 'sidebar-media-review', visible: auth.isBoard }
    ]
  },
  {
    label: 'Finanzas',
    items: [
      { label: 'Pagos', icon: 'pi pi-credit-card', to: '/admin/payments', testId: 'sidebar-payments', visible: auth.isBoard }
    ]
  },
  {
    label: 'Sistema',
    items: [
      { label: 'Almacenamiento', icon: 'pi pi-database', to: '/admin/storage', testId: 'sidebar-storage', visible: auth.isAdmin },
      { label: 'Configuración', icon: 'pi pi-cog', to: '/admin/settings', testId: 'sidebar-settings', visible: auth.isBoard }
    ]
  }
])

const visibleGroups = computed(() =>
  menuGroups.value
    .map(group => ({
      ...group,
      items: group.items.filter(item => item.visible)
    }))
    .filter(group => group.items.length > 0)
)

const isActive = (path: string): boolean => {
  return route.path === path
}
</script>

<template>
  <nav class="w-64 shrink-0" aria-label="Menú de administración" data-testid="admin-sidebar">
    <div class="sticky top-20 space-y-6">
      <div v-for="group in visibleGroups" :key="group.label">
        <p class="mb-2 px-3 text-xs font-semibold uppercase tracking-wider text-gray-500">
          {{ group.label }}
        </p>
        <ul class="space-y-1">
          <li v-for="item in group.items" :key="item.to">
            <router-link
              :to="item.to"
              :data-testid="item.testId"
              :aria-current="isActive(item.to) ? 'page' : undefined"
              class="flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors"
              :class="
                isActive(item.to)
                  ? 'border-l-4 border-red-600 bg-red-50 text-red-700'
                  : 'border-l-4 border-transparent text-gray-700 hover:bg-gray-100 hover:text-gray-900'
              "
            >
              <i :class="item.icon" />
              {{ item.label }}
            </router-link>
          </li>
        </ul>
      </div>
    </div>
  </nav>
</template>
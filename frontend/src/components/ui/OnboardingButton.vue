<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useOnboarding } from '@/composables/useOnboarding'
import Button from 'primevue/button'
import Menu from 'primevue/menu'
import type { MenuItem } from 'primevue/menuitem'

const route = useRoute()
const { getAvailableTours, startTour, resetAllTours } = useOnboarding()

const menu = ref()
const availableTours = ref(getAvailableTours())

watch(
  () => route.path,
  () => {
    availableTours.value = getAvailableTours()
  },
)

const menuItems = computed<MenuItem[]>(() => {
  const items: MenuItem[] = availableTours.value.map((tour) => ({
    label: tour.name,
    icon: 'pi pi-play',
    command: () => startTour(tour.id),
  }))

  if (items.length > 0) {
    items.push(
      { separator: true },
      {
        label: 'Reset all tours',
        icon: 'pi pi-refresh',
        command: () => {
          resetAllTours()
          availableTours.value = getAvailableTours()
        },
      },
    )
  }

  return items
})

const isVisible = computed(() => availableTours.value.length > 0)

const toggle = (event: Event) => {
  menu.value.toggle(event)
}
</script>

<template>
  <div v-if="isVisible" class="fixed bottom-6 right-6 z-40">
    <Button
      icon="pi pi-question-circle"
      rounded
      severity="info"
      aria-label="Help & Tours"
      @click="toggle"
    />
    <Menu ref="menu" :model="menuItems" :popup="true" />
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import Menu from 'primevue/menu'
import Button from 'primevue/button'
import Avatar from 'primevue/avatar'
import { ref } from 'vue'

const auth = useAuthStore()
const router = useRouter()
const menu = ref()

const menuItems = [
  {
    label: 'Mi Perfil',
    icon: 'pi pi-user',
    command: () => router.push('/profile')
  },
  {
    separator: true
  },
  {
    label: 'Cerrar Sesión',
    icon: 'pi pi-sign-out',
    command: () => {
      auth.logout()
      router.push('/')
    }
  }
]

const toggle = (event: Event) => {
  menu.value.toggle(event)
}

const getInitials = (name: string): string => {
  return name
    .split(' ')
    .map(n => n[0])
    .join('')
    .toUpperCase()
    .substring(0, 2)
}
</script>

<template>
  <div class="flex items-center gap-3">
    <span class="hidden text-sm font-medium text-gray-700 sm:block">
      {{ auth.fullName }}
    </span>
    <Button
      type="button"
      aria-label="User menu"
      text
      rounded
      @click="toggle"
    >
      <Avatar
        :label="getInitials(auth.fullName)"
        shape="circle"
        class="bg-primary-600 text-white"
      />
    </Button>
    <Menu ref="menu" :model="menuItems" popup />
  </div>
</template>

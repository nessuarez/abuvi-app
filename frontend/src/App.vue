<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AuthenticatedLayout from '@/layouts/AuthenticatedLayout.vue'
import Toast from 'primevue/toast'

const route = useRoute()
const auth = useAuthStore()

const isLandingPage = computed(() => route.path === '/')
const useLayout = computed(() => !isLandingPage.value && auth.isAuthenticated)

// Re-init Userback with user identity when authenticated
watch(() => auth.isAuthenticated, (isAuth) => {
  const ub = (window as any).Userback
  if (isAuth && ub) {
    ub.init(import.meta.env.VITE_USERBACK_TOKEN, {
      email: auth.user?.email,
      name: `${auth.user?.firstName ?? ''} ${auth.user?.lastName ?? ''}`.trim(),
    })
  }
}, { immediate: true })

onMounted(() => {
  auth.restoreSession()
})
</script>

<template>
  <Toast />
  <AuthenticatedLayout v-if="useLayout">
    <router-view />
  </AuthenticatedLayout>
  <router-view v-else />
</template>

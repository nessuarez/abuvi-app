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

// Attach user identity to Userback after authentication.
// The widget is auto-initialized via index.html (access_token) — never call init() again.
watch(() => auth.isAuthenticated, (isAuth) => {
  try {
    const ub = (window as any).Userback
    if (!isAuth || typeof ub?.identify !== 'function') return
    ub.identify(auth.user?.email ?? '', {
      name: `${auth.user?.firstName ?? ''} ${auth.user?.lastName ?? ''}`.trim(),
      email: auth.user?.email ?? '',
    })
  } catch (err) {
    console.warn('[Userback] Failed to identify user:', err)
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

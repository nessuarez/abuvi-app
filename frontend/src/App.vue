<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AuthenticatedLayout from '@/layouts/AuthenticatedLayout.vue'
import Toast from 'primevue/toast'

const route = useRoute()
const auth = useAuthStore()

const isLandingPage = computed(() => route.path === '/')
const useLayout = computed(() => !isLandingPage.value && auth.isAuthenticated)

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

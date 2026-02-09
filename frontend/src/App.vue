<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useAuth } from '@/composables/useAuth'
import Button from 'primevue/button'

const authStore = useAuthStore()
const { logout } = useAuth()

onMounted(() => {
  authStore.restoreSession()
})
</script>

<template>
  <div class="min-h-screen bg-gray-50">
    <!-- Navigation Header -->
    <header class="bg-white shadow">
      <div class="mx-auto flex max-w-7xl items-center justify-between px-4 py-4 sm:px-6 lg:px-8">
        <!-- Logo/Brand -->
        <router-link to="/" class="text-2xl font-bold text-primary-600">
          ABUVI
        </router-link>

        <!-- Navigation Links -->
        <nav class="flex items-center gap-6">
          <router-link
            to="/"
            class="text-gray-700 hover:text-primary-600 transition-colors"
          >
            Home
          </router-link>

          <router-link
            v-if="authStore.isAuthenticated"
            to="/users"
            class="text-gray-700 hover:text-primary-600 transition-colors"
          >
            Users
          </router-link>

          <!-- User info and logout -->
          <div v-if="authStore.isAuthenticated" class="flex items-center gap-4">
            <div class="text-right">
              <p class="text-sm font-medium text-gray-900">
                {{ authStore.user?.firstName }} {{ authStore.user?.lastName }}
              </p>
              <p class="text-xs text-gray-500">{{ authStore.user?.role }}</p>
            </div>

            <Button
              label="Logout"
              icon="pi pi-sign-out"
              severity="secondary"
              size="small"
              @click="logout"
            />
          </div>

          <!-- Login button -->
          <router-link v-else to="/login">
            <Button
              label="Login"
              icon="pi pi-sign-in"
              size="small"
            />
          </router-link>
        </nav>
      </div>
    </header>

    <!-- Page Content -->
    <main class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <router-view />
    </main>
  </div>
</template>

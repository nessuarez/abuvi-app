<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import Container from '@/components/ui/Container.vue'
import UserMenu from './UserMenu.vue'
import Button from 'primevue/button'

const auth = useAuthStore()
const router = useRouter()
const mobileMenuOpen = ref(false)

const navigationLinks = [
  { label: 'Home', path: '/home', icon: 'pi pi-home' },
  { label: 'Camp', path: '/camp', icon: 'pi pi-map' },
  { label: 'Anniversary', path: '/anniversary', icon: 'pi pi-star' },
  { label: 'My Profile', path: '/profile', icon: 'pi pi-user' }
]

const adminBoardLinks = [
  { label: 'Users', path: '/users', icon: 'pi pi-users' }
]

const isActive = (path: string): boolean => {
  return router.currentRoute.value.path === path
}

const toggleMobileMenu = () => {
  mobileMenuOpen.value = !mobileMenuOpen.value
}
</script>

<template>
  <header class="sticky top-0 z-50 border-b border-gray-200 bg-white shadow-sm">
    <Container>
      <div class="flex h-16 items-center justify-between">
        <!-- Logo -->
        <router-link to="/home" class="flex items-center gap-3">
          <img
            src="@/assets/images/logo.svg"
            alt="ABUVI Logo"
            class="h-10 w-10"
          />
          <span class="text-xl font-bold text-primary-600">ABUVI</span>
        </router-link>

        <!-- Desktop Navigation -->
        <nav class="hidden items-center gap-1 lg:flex">
          <router-link
            v-for="link in navigationLinks"
            :key="link.path"
            :to="link.path"
            class="rounded-md px-4 py-2 text-sm font-medium transition-colors"
            :class="
              isActive(link.path)
                ? 'bg-primary-50 text-primary-700'
                : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900'
            "
          >
            {{ link.label }}
          </router-link>

          <!-- Admin/Board links -->
          <router-link
            v-for="link in adminBoardLinks"
            v-if="auth.isBoard"
            :key="link.path"
            :to="link.path"
            class="rounded-md px-4 py-2 text-sm font-medium transition-colors"
            :class="
              isActive(link.path)
                ? 'bg-primary-50 text-primary-700'
                : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900'
            "
          >
            {{ link.label }}
          </router-link>

          <!-- Admin link (visible only to admins) -->
          <router-link
            v-if="auth.isAdmin"
            to="/admin"
            class="rounded-md px-4 py-2 text-sm font-medium transition-colors"
            :class="
              isActive('/admin')
                ? 'bg-red-50 text-red-700'
                : 'bg-red-600 text-white hover:bg-red-700'
            "
          >
            Admin
          </router-link>
        </nav>

        <!-- User Menu (Desktop) -->
        <div class="hidden lg:block">
          <UserMenu />
        </div>

        <!-- Mobile Menu Button -->
        <Button
          icon="pi pi-bars"
          text
          rounded
          class="lg:hidden"
          @click="toggleMobileMenu"
        />
      </div>

      <!-- Mobile Navigation -->
      <nav
        v-if="mobileMenuOpen"
        class="border-t border-gray-200 py-4 lg:hidden"
      >
        <div class="flex flex-col gap-2">
          <router-link
            v-for="link in navigationLinks"
            :key="link.path"
            :to="link.path"
            class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium transition-colors"
            :class="
              isActive(link.path)
                ? 'bg-primary-50 text-primary-700'
                : 'text-gray-700 hover:bg-gray-100'
            "
            @click="mobileMenuOpen = false"
          >
            <i :class="link.icon" />
            {{ link.label }}
          </router-link>

          <!-- Admin/Board links (mobile) -->
          <router-link
            v-for="link in adminBoardLinks"
            v-if="auth.isBoard"
            :key="link.path"
            :to="link.path"
            class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium transition-colors"
            :class="
              isActive(link.path)
                ? 'bg-primary-50 text-primary-700'
                : 'text-gray-700 hover:bg-gray-100'
            "
            @click="mobileMenuOpen = false"
          >
            <i :class="link.icon" />
            {{ link.label }}
          </router-link>

          <!-- Admin link (mobile) -->
          <router-link
            v-if="auth.isAdmin"
            to="/admin"
            class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium transition-colors"
            :class="
              isActive('/admin')
                ? 'bg-red-50 text-red-700'
                : 'bg-red-600 text-white'
            "
            @click="mobileMenuOpen = false"
          >
            <i class="pi pi-shield" />
            Admin
          </router-link>

          <!-- User info (mobile) -->
          <div class="mt-4 border-t border-gray-200 pt-4">
            <div class="px-4 text-sm text-gray-600">
              Signed in as <strong>{{ auth.fullName }}</strong>
            </div>
            <Button
              label="Logout"
              icon="pi pi-sign-out"
              text
              class="mt-2 w-full justify-start"
              @click="auth.logout(); $router.push('/')"
            />
          </div>
        </div>
      </nav>
    </Container>
  </header>
</template>

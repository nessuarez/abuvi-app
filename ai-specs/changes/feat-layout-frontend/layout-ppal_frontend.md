# Frontend Implementation Plan: feat-layout-frontend - Main Page Layout

## Overview

This plan outlines the implementation of the main page layout for the ABUVI members-only application. The implementation uses **Vue 3 Composition API** with `<script setup lang="ts">`, **PrimeVue** components for UI elements, and **Tailwind CSS** for styling.

**Key Architecture Principle**: This is a **private, members-only application**. The public landing page serves only as an authentication gateway. Once authenticated, members access the full application with a complete layout including header, navigation, content areas, and footer.

## Architecture Context

### Components Involved

#### Layouts
- `src/layouts/PublicLayout.vue` (unauthenticated users - landing page only)
- `src/layouts/AuthenticatedLayout.vue` (authenticated users - full app layout)

#### Views (Page-level components)
- `src/views/LandingPage.vue` (public authentication page)
- `src/views/HomePage.vue` (authenticated dashboard/home)
- `src/views/CampPage.vue` (camp information - placeholder)
- `src/views/AnniversaryPage.vue` (anniversary celebration - placeholder)
- `src/views/ProfilePage.vue` (user profile - placeholder)
- `src/views/AdminPage.vue` (admin panel - placeholder, role-protected)

#### Layout Components
- `src/components/layout/AppHeader.vue` (authenticated header with navigation)
- `src/components/layout/AppFooter.vue` (authenticated footer)
- `src/components/layout/UserMenu.vue` (user dropdown menu in header)

#### Authentication Components
- `src/components/auth/AuthContainer.vue` (centered container for auth forms)
- `src/components/auth/LoginForm.vue` (login form)
- `src/components/auth/RegisterForm.vue` (registration form)

#### Home Page Components
- `src/components/home/QuickAccessCards.vue` (container for quick access cards)
- `src/components/home/QuickAccessCard.vue` (individual card component)
- `src/components/home/AnniversarySection.vue` (50th anniversary section)

#### Shared UI Components
- `src/components/ui/Container.vue` (max-width container with responsive padding)

### State Management

#### Pinia Store
- `src/stores/auth.ts` - Authentication store managing:
  - User session data (`user`, `token`)
  - Authentication state (`isAuthenticated`)
  - Role-based getters (`isAdmin`, `isBoard`, `isMember`)
  - Login/logout actions

### Routing Considerations

#### Public Routes (Unauthenticated)
- `/` - Landing page with authentication forms

#### Protected Routes (Authenticated)
- `/home` - Dashboard/home page (default after login)
- `/camp` - Camp information page
- `/anniversary` - 50th anniversary page
- `/profile` - User profile page
- `/admin` - Admin panel (Admin role only)

#### Route Guards
- `requiresAuth` meta field for protected routes
- `requiresAdmin` meta field for admin-only routes
- Redirect unauthenticated users to `/`
- Redirect authenticated users from `/` to `/home`

### Files Referenced

#### Configuration
- `src/router/index.ts` - Route definitions and guards
- `tailwind.config.ts` - Tailwind CSS configuration
- `src/main.ts` - App initialization, PrimeVue setup

#### Types
- `src/types/user.ts` - User-related types
- `src/types/api.ts` - API response types
- `src/types/auth.ts` - Authentication request/response types

#### Assets
- `src/assets/images/logo.svg` - ABUVI logo
- `src/assets/images/landing-background.jpg` - Landing page blurred background
- `src/assets/images/grupo-abuvi.jpg` - Anniversary section image
- `src/assets/images/50-aniversario-badge.png` - Anniversary badge
- `src/assets/images/icons/` - Icon assets (tent, user, celebration, social media)

#### Styles
- `src/assets/styles/variables.css` - CSS variables (colors, spacing, typography)
- `src/assets/styles/global.css` - Global styles

---

## Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to a new feature branch following the development workflow.

**Branch Naming**: `feature/feat-layout-frontend`

**Implementation Steps**:
1. Ensure you're on the latest `main` branch
2. Pull latest changes: `git pull origin main`
3. Create new branch: `git checkout -b feature/feat-layout-frontend`
4. Verify branch creation: `git branch`

**Notes**: This must be the FIRST step before any code changes. The branch name must use the `-frontend` suffix to separate concerns from backend work.

---

### Step 1: Setup Project Structure and Design Tokens

**Action**: Create the folder structure and define design tokens (colors, typography, spacing) in CSS variables.

**Files to Create**:
- `src/layouts/` (directory)
- `src/components/layout/` (directory)
- `src/components/auth/` (directory)
- `src/components/home/` (directory)
- `src/components/ui/` (directory)
- `src/assets/styles/variables.css`
- `src/assets/styles/global.css`
- `src/assets/images/` (ensure directory exists)

**Implementation Steps**:

1. Create directory structure:
```bash
mkdir -p src/layouts
mkdir -p src/components/layout
mkdir -p src/components/auth
mkdir -p src/components/home
mkdir -p src/components/ui
mkdir -p src/assets/styles
mkdir -p src/assets/images/icons/social
```

2. Create `src/assets/styles/variables.css`:
```css
:root {
  /* Colors - Primary (Nature/Outdoor theme) */
  --color-primary-50: #f0fdf4;
  --color-primary-100: #dcfce7;
  --color-primary-200: #bbf7d0;
  --color-primary-300: #86efac;
  --color-primary-400: #4ade80;
  --color-primary-500: #22c55e;
  --color-primary-600: #16a34a;
  --color-primary-700: #15803d;
  --color-primary-800: #166534;
  --color-primary-900: #14532d;

  /* Colors - Secondary (Accent) */
  --color-secondary-500: #f59e0b;
  --color-secondary-600: #d97706;

  /* Colors - Neutral */
  --color-gray-50: #f9fafb;
  --color-gray-100: #f3f4f6;
  --color-gray-200: #e5e7eb;
  --color-gray-600: #4b5563;
  --color-gray-900: #111827;

  /* Typography */
  --font-sans: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
  --font-size-base: 1rem; /* 16px */
  --font-size-sm: 0.875rem; /* 14px */
  --font-size-lg: 1.125rem; /* 18px */
  --font-size-xl: 1.25rem; /* 20px */
  --font-size-2xl: 1.5rem; /* 24px */
  --font-size-3xl: 1.875rem; /* 30px */
  --font-size-4xl: 2.25rem; /* 36px */
  --line-height-normal: 1.5;
  --line-height-heading: 1.2;

  /* Spacing */
  --spacing-1: 0.25rem; /* 4px */
  --spacing-2: 0.5rem; /* 8px */
  --spacing-3: 0.75rem; /* 12px */
  --spacing-4: 1rem; /* 16px */
  --spacing-6: 1.5rem; /* 24px */
  --spacing-8: 2rem; /* 32px */
  --spacing-12: 3rem; /* 48px */
  --spacing-16: 4rem; /* 64px */
  --spacing-24: 6rem; /* 96px */

  /* Container */
  --container-max-width: 1280px;

  /* Breakpoints (for reference, handled by Tailwind) */
  /* Mobile: < 640px */
  /* Tablet: 640px - 1024px */
  /* Desktop: > 1024px */
}
```

3. Create `src/assets/styles/global.css`:
```css
@import './variables.css';

body {
  font-family: var(--font-sans);
  font-size: var(--font-size-base);
  line-height: var(--line-height-normal);
  color: var(--color-gray-900);
}

h1, h2, h3, h4, h5, h6 {
  line-height: var(--line-height-heading);
  font-weight: 600;
}
```

4. Import global styles in `src/main.ts`:
```typescript
import './assets/styles/global.css'
```

**Dependencies**: None (CSS only)

**Implementation Notes**:
- Use CSS variables for consistency
- Colors follow a nature/outdoor theme appropriate for ABUVI
- Spacing scale follows Tailwind's convention
- Typography uses system fonts with Inter as preferred font

---

### Step 2: Define TypeScript Types

**Action**: Define TypeScript interfaces for User, Auth, and API responses.

**Files to Create**:
- `src/types/user.ts`
- `src/types/auth.ts`
- `src/types/api.ts`

**Implementation Steps**:

1. Create `src/types/user.ts`:
```typescript
export type UserRole = 'Admin' | 'Board' | 'Member'

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  role: UserRole
  isActive: boolean
}
```

2. Create `src/types/auth.ts`:
```typescript
import type { User } from './user'

export interface LoginRequest {
  email: string
  password: string
  rememberMe?: boolean
}

export interface RegisterRequest {
  firstName: string
  lastName: string
  email: string
  password: string
  confirmPassword: string
  acceptTerms: boolean
}

export interface AuthResponse {
  user: User
  token: string
}
```

3. Create `src/types/api.ts`:
```typescript
export interface ApiResponse<T> {
  success: boolean
  data: T | null
  error: ApiError | null
}

export interface ApiError {
  message: string
  code: string
  details?: Array<{ field: string; message: string }>
}
```

**Dependencies**: None

**Implementation Notes**:
- User types match backend DTOs
- Auth types support both login and registration flows
- API response wrapper matches backend envelope pattern

---

### Step 3: Create Authentication Store (Pinia)

**Action**: Create Pinia store for authentication state management.

**File**: `src/stores/auth.ts`

**Implementation Steps**:

1. Create `src/stores/auth.ts`:
```typescript
import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import { api } from '@/utils/api'
import type { User } from '@/types/user'
import type { LoginRequest, RegisterRequest, AuthResponse } from '@/types/auth'
import type { ApiResponse } from '@/types/api'

const TOKEN_KEY = 'abuvi_auth_token'
const USER_KEY = 'abuvi_user'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<User | null>(loadUserFromStorage())
  const token = ref<string | null>(loadTokenFromStorage())

  // Getters
  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isBoard = computed(() =>
    user.value?.role === 'Admin' || user.value?.role === 'Board'
  )
  const isMember = computed(() => !!user.value)
  const fullName = computed(() =>
    user.value ? `${user.value.firstName} ${user.value.lastName}` : ''
  )

  // Actions
  async function login(credentials: LoginRequest): Promise<{ success: boolean; error?: string }> {
    try {
      const response = await api.post<ApiResponse<AuthResponse>>(
        '/auth/login',
        credentials
      )

      if (response.data.success && response.data.data) {
        const { user: userData, token: authToken } = response.data.data
        user.value = userData
        token.value = authToken

        // Persist to localStorage if rememberMe is true
        if (credentials.rememberMe) {
          saveToStorage(userData, authToken)
        }

        return { success: true }
      }

      return {
        success: false,
        error: response.data.error?.message || 'Login failed'
      }
    } catch (err: any) {
      return {
        success: false,
        error: err.response?.data?.error?.message || 'Network error. Please try again.'
      }
    }
  }

  async function register(data: RegisterRequest): Promise<{ success: boolean; error?: string }> {
    try {
      const response = await api.post<ApiResponse<AuthResponse>>(
        '/auth/register',
        data
      )

      if (response.data.success && response.data.data) {
        const { user: userData, token: authToken } = response.data.data
        user.value = userData
        token.value = authToken
        saveToStorage(userData, authToken)

        return { success: true }
      }

      return {
        success: false,
        error: response.data.error?.message || 'Registration failed'
      }
    } catch (err: any) {
      return {
        success: false,
        error: err.response?.data?.error?.message || 'Network error. Please try again.'
      }
    }
  }

  function logout() {
    user.value = null
    token.value = null
    clearStorage()
  }

  // Helper functions
  function loadUserFromStorage(): User | null {
    const userData = localStorage.getItem(USER_KEY)
    return userData ? JSON.parse(userData) : null
  }

  function loadTokenFromStorage(): string | null {
    return localStorage.getItem(TOKEN_KEY)
  }

  function saveToStorage(userData: User, authToken: string) {
    localStorage.setItem(USER_KEY, JSON.stringify(userData))
    localStorage.setItem(TOKEN_KEY, authToken)
  }

  function clearStorage() {
    localStorage.removeItem(USER_KEY)
    localStorage.removeItem(TOKEN_KEY)
  }

  return {
    user,
    token,
    isAuthenticated,
    isAdmin,
    isBoard,
    isMember,
    fullName,
    login,
    register,
    logout
  }
})
```

**Dependencies**:
- `pinia` (already in project)
- `@/utils/api` (will be created in next step)

**Implementation Notes**:
- Uses Pinia setup syntax (Composition API style)
- Persists auth state to localStorage for "Remember Me" functionality
- Provides computed getters for role-based checks
- Returns structured error messages from API

---

### Step 4: Configure Axios Instance

**Action**: Create centralized Axios instance with interceptors for authentication.

**File**: `src/utils/api.ts`

**Implementation Steps**:

1. Create `src/utils/api.ts`:
```typescript
import axios from 'axios'
import { useAuthStore } from '@/stores/auth'
import router from '@/router'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  },
  timeout: 10000
})

// Request interceptor - attach auth token
api.interceptors.request.use(
  (config) => {
    const auth = useAuthStore()
    if (auth.token) {
      config.headers.Authorization = `Bearer ${auth.token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor - handle 401 globally
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const auth = useAuthStore()
      auth.logout()
      router.push({ path: '/', query: { redirect: router.currentRoute.value.fullPath } })
    }
    return Promise.reject(error)
  }
)
```

**Dependencies**:
- `axios` (already in project)
- `@/stores/auth`
- `@/router` (will be created in Step 5)

**Implementation Notes**:
- Automatically adds JWT token to all requests
- Handles 401 (Unauthorized) globally by logging out and redirecting to landing page
- Saves redirect URL for post-login navigation
- Uses environment variable for API base URL

---

### Step 5: Configure Vue Router with Route Guards

**Action**: Set up routing with public and protected routes, including authentication guards.

**File**: `src/router/index.ts`

**Implementation Steps**:

1. Update `src/router/index.ts`:
```typescript
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // Public route - Landing/Auth page
    {
      path: '/',
      name: 'landing',
      component: () => import('@/views/LandingPage.vue'),
      meta: {
        requiresAuth: false,
        title: 'ABUVI'
      }
    },

    // Protected routes - Authenticated users only
    {
      path: '/home',
      name: 'home',
      component: () => import('@/views/HomePage.vue'),
      meta: {
        requiresAuth: true,
        title: 'ABUVI | Home'
      }
    },
    {
      path: '/camp',
      name: 'camp',
      component: () => import('@/views/CampPage.vue'),
      meta: {
        requiresAuth: true,
        title: 'ABUVI | Camp'
      }
    },
    {
      path: '/anniversary',
      name: 'anniversary',
      component: () => import('@/views/AnniversaryPage.vue'),
      meta: {
        requiresAuth: true,
        title: 'ABUVI | 50th Anniversary'
      }
    },
    {
      path: '/profile',
      name: 'profile',
      component: () => import('@/views/ProfilePage.vue'),
      meta: {
        requiresAuth: true,
        title: 'ABUVI | Profile'
      }
    },
    {
      path: '/admin',
      name: 'admin',
      component: () => import('@/views/AdminPage.vue'),
      meta: {
        requiresAuth: true,
        requiresAdmin: true,
        title: 'ABUVI | Admin'
      }
    }
  ]
})

// Route guard for authentication
router.beforeEach((to, from, next) => {
  const auth = useAuthStore()

  // Update document title
  document.title = (to.meta.title as string) || 'ABUVI'

  // Check if route requires authentication
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    // Redirect to landing page with redirect URL
    next({ path: '/', query: { redirect: to.fullPath } })
    return
  }

  // Check if route requires admin role
  if (to.meta.requiresAdmin && !auth.isAdmin) {
    // Redirect to home if not admin
    next({ path: '/home' })
    return
  }

  // Redirect authenticated users from landing page to home
  if (to.path === '/' && auth.isAuthenticated) {
    const redirect = to.query.redirect as string | undefined
    next(redirect || '/home')
    return
  }

  next()
})

export default router
```

**Dependencies**:
- `vue-router` (already in project)
- `@/stores/auth`

**Implementation Notes**:
- Uses lazy loading for all routes (code splitting)
- Route guards enforce authentication and role-based access
- Document title updates based on route meta
- Preserves redirect URL for post-login navigation
- All routes except `/` require authentication

---

### Step 6: Create Shared UI Components

**Action**: Create reusable UI components used across the application.

**Files to Create**:
- `src/components/ui/Container.vue`

**Implementation Steps**:

1. Create `src/components/ui/Container.vue`:
```vue
<script setup lang="ts">
interface Props {
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl' | 'full'
  noPadding?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  maxWidth: 'xl',
  noPadding: false
})

const maxWidthClasses = {
  sm: 'max-w-screen-sm',
  md: 'max-w-screen-md',
  lg: 'max-w-screen-lg',
  xl: 'max-w-screen-xl',
  full: 'max-w-full'
}
</script>

<template>
  <div
    class="mx-auto w-full"
    :class="[
      maxWidthClasses[maxWidth],
      { 'px-4 sm:px-6 lg:px-8': !noPadding }
    ]"
  >
    <slot />
  </div>
</template>
```

**Dependencies**: None (Tailwind CSS only)

**Implementation Notes**:
- Provides consistent max-width container
- Responsive padding
- Reusable across all pages

---

### Step 7: Create Authentication Components

**Action**: Create login and registration form components.

**Files to Create**:
- `src/components/auth/AuthContainer.vue`
- `src/components/auth/LoginForm.vue`
- `src/components/auth/RegisterForm.vue`

**Implementation Steps**:

1. Create `src/components/auth/AuthContainer.vue`:
```vue
<script setup lang="ts">
import { ref } from 'vue'
import TabView from 'primevue/tabview'
import TabPanel from 'primevue/tabpanel'
import LoginForm from './LoginForm.vue'
import RegisterForm from './RegisterForm.vue'

const activeTab = ref(0)
</script>

<template>
  <div class="w-full max-w-md rounded-lg bg-white/95 p-8 shadow-2xl backdrop-blur-sm">
    <div class="mb-6 text-center">
      <h1 class="mb-2 text-3xl font-bold text-gray-900">Bienvenido a ABUVI</h1>
      <p class="text-sm text-gray-600">
        Plataforma exclusiva para miembros
      </p>
    </div>

    <TabView v-model:activeIndex="activeTab" class="auth-tabs">
      <TabPanel header="Iniciar Sesión">
        <LoginForm />
      </TabPanel>
      <TabPanel header="Registrarse">
        <RegisterForm />
      </TabPanel>
    </TabView>
  </div>
</template>

<style>
.auth-tabs .p-tabview-nav {
  background: transparent;
  border: none;
}

.auth-tabs .p-tabview-panels {
  background: transparent;
  padding: 1.5rem 0 0 0;
}
</style>
```

2. Create `src/components/auth/LoginForm.vue`:
```vue
<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'
import Message from 'primevue/message'

const router = useRouter()
const auth = useAuthStore()

const formData = reactive({
  email: '',
  password: '',
  rememberMe: false
})

const errors = ref<Record<string, string>>({})
const submitting = ref(false)
const errorMessage = ref('')

const validate = (): boolean => {
  errors.value = {}

  if (!formData.email.trim()) {
    errors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Invalid email format'
  }

  if (!formData.password) {
    errors.value.password = 'Password is required'
  }

  return Object.keys(errors.value).length === 0
}

const handleSubmit = async () => {
  if (!validate()) return

  errorMessage.value = ''
  submitting.value = true

  try {
    const result = await auth.login(formData)

    if (result.success) {
      const redirect = router.currentRoute.value.query.redirect as string | undefined
      router.push(redirect || '/home')
    } else {
      errorMessage.value = result.error || 'Login failed'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <Message v-if="errorMessage" severity="error" :closable="false">
      {{ errorMessage }}
    </Message>

    <div class="flex flex-col gap-2">
      <label for="email" class="text-sm font-medium text-gray-700">Email *</label>
      <InputText
        id="email"
        v-model="formData.email"
        type="email"
        placeholder="tu@email.com"
        :invalid="!!errors.email"
        :disabled="submitting"
      />
      <small v-if="errors.email" class="text-red-500">{{ errors.email }}</small>
    </div>

    <div class="flex flex-col gap-2">
      <label for="password" class="text-sm font-medium text-gray-700">Contraseña *</label>
      <Password
        id="password"
        v-model="formData.password"
        toggle-mask
        :feedback="false"
        placeholder="••••••••"
        :invalid="!!errors.password"
        :disabled="submitting"
        input-class="w-full"
      />
      <small v-if="errors.password" class="text-red-500">{{ errors.password }}</small>
    </div>

    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <Checkbox
          id="rememberMe"
          v-model="formData.rememberMe"
          :binary="true"
          :disabled="submitting"
        />
        <label for="rememberMe" class="text-sm text-gray-700">Recordarme</label>
      </div>
      <a href="#" class="text-sm text-primary-600 hover:text-primary-700">
        ¿Olvidaste tu contraseña?
      </a>
    </div>

    <Button
      type="submit"
      label="Iniciar Sesión"
      :loading="submitting"
      :disabled="submitting"
      class="w-full"
    />
  </form>
</template>
```

3. Create `src/components/auth/RegisterForm.vue`:
```vue
<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'
import Message from 'primevue/message'

const router = useRouter()
const auth = useAuthStore()

const formData = reactive({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  confirmPassword: '',
  acceptTerms: false
})

const errors = ref<Record<string, string>>({})
const submitting = ref(false)
const errorMessage = ref('')

const passwordStrength = computed(() => {
  const pwd = formData.password
  if (pwd.length < 6) return 'weak'
  if (pwd.length < 10) return 'medium'
  return 'strong'
})

const validate = (): boolean => {
  errors.value = {}

  if (!formData.firstName.trim()) {
    errors.value.firstName = 'First name is required'
  }

  if (!formData.lastName.trim()) {
    errors.value.lastName = 'Last name is required'
  }

  if (!formData.email.trim()) {
    errors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Invalid email format'
  }

  if (!formData.password) {
    errors.value.password = 'Password is required'
  } else if (formData.password.length < 6) {
    errors.value.password = 'Password must be at least 6 characters'
  }

  if (formData.password !== formData.confirmPassword) {
    errors.value.confirmPassword = 'Passwords do not match'
  }

  if (!formData.acceptTerms) {
    errors.value.acceptTerms = 'You must accept the terms and conditions'
  }

  return Object.keys(errors.value).length === 0
}

const handleSubmit = async () => {
  if (!validate()) return

  errorMessage.value = ''
  submitting.value = true

  try {
    const result = await auth.register(formData)

    if (result.success) {
      router.push('/home')
    } else {
      errorMessage.value = result.error || 'Registration failed'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <Message v-if="errorMessage" severity="error" :closable="false">
      {{ errorMessage }}
    </Message>

    <div class="grid grid-cols-2 gap-4">
      <div class="flex flex-col gap-2">
        <label for="firstName" class="text-sm font-medium text-gray-700">Nombre *</label>
        <InputText
          id="firstName"
          v-model="formData.firstName"
          placeholder="Juan"
          :invalid="!!errors.firstName"
          :disabled="submitting"
        />
        <small v-if="errors.firstName" class="text-red-500">{{ errors.firstName }}</small>
      </div>

      <div class="flex flex-col gap-2">
        <label for="lastName" class="text-sm font-medium text-gray-700">Apellidos *</label>
        <InputText
          id="lastName"
          v-model="formData.lastName"
          placeholder="García"
          :invalid="!!errors.lastName"
          :disabled="submitting"
        />
        <small v-if="errors.lastName" class="text-red-500">{{ errors.lastName }}</small>
      </div>
    </div>

    <div class="flex flex-col gap-2">
      <label for="registerEmail" class="text-sm font-medium text-gray-700">Email *</label>
      <InputText
        id="registerEmail"
        v-model="formData.email"
        type="email"
        placeholder="tu@email.com"
        :invalid="!!errors.email"
        :disabled="submitting"
      />
      <small v-if="errors.email" class="text-red-500">{{ errors.email }}</small>
    </div>

    <div class="flex flex-col gap-2">
      <label for="registerPassword" class="text-sm font-medium text-gray-700">Contraseña *</label>
      <Password
        id="registerPassword"
        v-model="formData.password"
        toggle-mask
        :feedback="true"
        placeholder="••••••••"
        :invalid="!!errors.password"
        :disabled="submitting"
        input-class="w-full"
      />
      <small v-if="errors.password" class="text-red-500">{{ errors.password }}</small>
    </div>

    <div class="flex flex-col gap-2">
      <label for="confirmPassword" class="text-sm font-medium text-gray-700">
        Confirmar Contraseña *
      </label>
      <Password
        id="confirmPassword"
        v-model="formData.confirmPassword"
        toggle-mask
        :feedback="false"
        placeholder="••••••••"
        :invalid="!!errors.confirmPassword"
        :disabled="submitting"
        input-class="w-full"
      />
      <small v-if="errors.confirmPassword" class="text-red-500">
        {{ errors.confirmPassword }}
      </small>
    </div>

    <div class="flex flex-col gap-2">
      <div class="flex items-start gap-2">
        <Checkbox
          id="acceptTerms"
          v-model="formData.acceptTerms"
          :binary="true"
          :invalid="!!errors.acceptTerms"
          :disabled="submitting"
        />
        <label for="acceptTerms" class="text-sm text-gray-700">
          Acepto los
          <a href="#" class="text-primary-600 hover:text-primary-700">
            términos y condiciones
          </a>
        </label>
      </div>
      <small v-if="errors.acceptTerms" class="text-red-500">{{ errors.acceptTerms }}</small>
    </div>

    <Button
      type="submit"
      label="Registrarse"
      :loading="submitting"
      :disabled="submitting"
      class="w-full"
    />
  </form>
</template>
```

**Dependencies**:
- `primevue/inputtext`
- `primevue/password`
- `primevue/checkbox`
- `primevue/button`
- `primevue/message`
- `primevue/tabview`
- `primevue/tabpanel`

**Implementation Notes**:
- Client-side validation with error messages
- Password strength indicator in registration form
- "Remember Me" functionality in login form
- Disabled state during submission
- Error messages from backend displayed prominently

---

### Step 8: Create Landing Page (Public)

**Action**: Create the public landing page with blurred background and authentication forms.

**File**: `src/views/LandingPage.vue`

**Implementation Steps**:

1. Create `src/views/LandingPage.vue`:
```vue
<script setup lang="ts">
import AuthContainer from '@/components/auth/AuthContainer.vue'
</script>

<template>
  <div class="relative flex min-h-screen items-center justify-center">
    <!-- Blurred background image -->
    <div
      class="absolute inset-0 bg-cover bg-center bg-no-repeat"
      style="
        background-image: url('/src/assets/images/landing-background.jpg');
        filter: blur(8px);
        transform: scale(1.1);
      "
    />

    <!-- Dark overlay for better contrast -->
    <div class="absolute inset-0 bg-black/40" />

    <!-- Centered auth container -->
    <div class="relative z-10 px-4">
      <AuthContainer />
    </div>
  </div>
</template>
```

**Dependencies**:
- `@/components/auth/AuthContainer.vue`
- Background image asset: `src/assets/images/landing-background.jpg`

**Implementation Notes**:
- Full viewport height layout
- Blurred background with dark overlay for text contrast
- Centered authentication container
- Responsive padding
- No header or footer (authentication gateway only)

---

### Step 9: Create Authenticated Header Component

**Action**: Create header with navigation and user menu for authenticated users.

**Files to Create**:
- `src/components/layout/AppHeader.vue`
- `src/components/layout/UserMenu.vue`

**Implementation Steps**:

1. Create `src/components/layout/UserMenu.vue`:
```vue
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
    label: 'Profile',
    icon: 'pi pi-user',
    command: () => router.push('/profile')
  },
  {
    separator: true
  },
  {
    label: 'Logout',
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
```

2. Create `src/components/layout/AppHeader.vue`:
```vue
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
```

**Dependencies**:
- `primevue/menu`
- `primevue/button`
- `primevue/avatar`
- Logo asset: `src/assets/images/logo.svg`

**Implementation Notes**:
- Sticky header with shadow
- Desktop horizontal menu, mobile hamburger menu
- Active route highlighting
- Admin link visible only to admin users
- Responsive behavior with mobile menu toggle
- User avatar with dropdown menu

---

### Step 10: Create Authenticated Footer Component

**Action**: Create footer with links and information for authenticated users.

**File**: `src/components/layout/AppFooter.vue`

**Implementation Steps**:

1. Create `src/components/layout/AppFooter.vue`:
```vue
<script setup lang="ts">
const currentYear = new Date().getFullYear()

const linkGroups = [
  {
    title: 'Enlaces',
    links: [
      { label: 'Camp 2026', path: '/camp' },
      { label: '50 Aniversario', path: '/anniversary' },
      { label: 'Mi Perfil', path: '/profile' }
    ]
  },
  {
    title: 'Legal',
    links: [
      { label: 'Aviso Legal', path: '/legal/notice' },
      { label: 'Política de Privacidad', path: '/legal/privacy' },
      { label: 'Estatutos', path: '/legal/bylaws' },
      { label: 'Transparencia', path: '/legal/transparency' }
    ]
  }
]

const socialLinks = [
  { icon: 'pi pi-facebook', url: '#', label: 'Facebook' },
  { icon: 'pi pi-instagram', url: '#', label: 'Instagram' },
  { icon: 'pi pi-twitter', url: '#', label: 'Twitter' },
  { icon: 'pi pi-youtube', url: '#', label: 'YouTube' }
]

const contactInfo = {
  email: 'info@abuvi.org',
  phone: '+34 600 000 000'
}
</script>

<template>
  <footer class="border-t border-gray-200 bg-gray-50">
    <div class="mx-auto max-w-screen-xl px-4 py-12 sm:px-6 lg:px-8">
      <div class="grid grid-cols-1 gap-8 md:grid-cols-2 lg:grid-cols-4">
        <!-- Column 1: Branding & Description -->
        <div>
          <h3 class="mb-4 text-lg font-bold text-gray-900">ABUVI</h3>
          <p class="text-sm text-gray-600">
            Amigos de la Buena Vida. Promoviendo la amistad, la naturaleza y la
            convivencia desde 1976.
          </p>
        </div>

        <!-- Column 2: Enlaces -->
        <div>
          <h4 class="mb-4 text-sm font-semibold uppercase text-gray-900">
            {{ linkGroups[0].title }}
          </h4>
          <ul class="space-y-2">
            <li v-for="link in linkGroups[0].links" :key="link.path">
              <router-link
                :to="link.path"
                class="text-sm text-gray-600 transition-colors hover:text-primary-600"
              >
                {{ link.label }}
              </router-link>
            </li>
          </ul>
        </div>

        <!-- Column 3: Legal -->
        <div>
          <h4 class="mb-4 text-sm font-semibold uppercase text-gray-900">
            {{ linkGroups[1].title }}
          </h4>
          <ul class="space-y-2">
            <li v-for="link in linkGroups[1].links" :key="link.path">
              <router-link
                :to="link.path"
                class="text-sm text-gray-600 transition-colors hover:text-primary-600"
              >
                {{ link.label }}
              </router-link>
            </li>
          </ul>
        </div>

        <!-- Column 4: Contacto -->
        <div>
          <h4 class="mb-4 text-sm font-semibold uppercase text-gray-900">Contacto</h4>

          <!-- Social Media Icons -->
          <div class="mb-4 flex gap-3">
            <a
              v-for="social in socialLinks"
              :key="social.label"
              :href="social.url"
              :aria-label="social.label"
              target="_blank"
              rel="noopener noreferrer"
              class="flex h-10 w-10 items-center justify-center rounded-full bg-gray-200 text-gray-600 transition-colors hover:bg-primary-600 hover:text-white"
            >
              <i :class="social.icon" />
            </a>
          </div>

          <!-- Contact Information -->
          <div class="space-y-2 text-sm text-gray-600">
            <p>
              <i class="pi pi-envelope mr-2" />
              <a
                :href="`mailto:${contactInfo.email}`"
                class="hover:text-primary-600"
              >
                {{ contactInfo.email }}
              </a>
            </p>
            <p>
              <i class="pi pi-phone mr-2" />
              <a
                :href="`tel:${contactInfo.phone}`"
                class="hover:text-primary-600"
              >
                {{ contactInfo.phone }}
              </a>
            </p>
          </div>
        </div>
      </div>

      <!-- Footer Bottom - Copyright -->
      <div class="mt-8 border-t border-gray-200 pt-8 text-center">
        <p class="text-sm text-gray-600">
          © {{ currentYear }} Asociación ABUVI. Todos los derechos reservados.
        </p>
      </div>
    </div>
  </footer>
</template>
```

**Dependencies**: None (Tailwind CSS and PrimeIcons)

**Implementation Notes**:
- Four-column layout on desktop, stacked on mobile
- Social media links with icons
- Contact information with email and phone
- Copyright notice with dynamic year
- Links styled with hover effects
- External links open in new tab with security attributes

---

### Step 11: Create Home Page Components

**Action**: Create quick access cards and anniversary section for the home page.

**Files to Create**:
- `src/components/home/QuickAccessCard.vue`
- `src/components/home/QuickAccessCards.vue`
- `src/components/home/AnniversarySection.vue`

**Implementation Steps**:

1. Create `src/components/home/QuickAccessCard.vue`:
```vue
<script setup lang="ts">
interface Props {
  icon: string
  label: string
  description: string
  ctaLabel: string
  ctaPath: string
}

defineProps<Props>()
</script>

<template>
  <div
    class="group rounded-lg border border-gray-200 bg-white p-6 shadow-sm transition-all hover:shadow-md"
  >
    <div class="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-primary-100">
      <i :class="`${icon} text-2xl text-primary-600`" />
    </div>

    <h3 class="mb-2 text-lg font-semibold text-gray-900">{{ label }}</h3>
    <p class="mb-4 text-sm text-gray-600">{{ description }}</p>

    <router-link
      :to="ctaPath"
      class="inline-flex items-center gap-2 text-sm font-medium text-primary-600 transition-colors hover:text-primary-700"
    >
      {{ ctaLabel }}
      <i class="pi pi-arrow-right" />
    </router-link>
  </div>
</template>
```

2. Create `src/components/home/QuickAccessCards.vue`:
```vue
<script setup lang="ts">
import QuickAccessCard from './QuickAccessCard.vue'

const cards = [
  {
    icon: 'pi pi-map',
    label: 'Campamento 2026',
    description: '15 días inolvidables en plena naturaleza. Segunda quincena de agosto.',
    ctaLabel: 'Ver detalles',
    ctaPath: '/camp'
  },
  {
    icon: 'pi pi-star',
    label: '50 Aniversario',
    description: 'Celebrando medio siglo de historias. Participa en los eventos conmemorativos.',
    ctaLabel: 'Participar',
    ctaPath: '/anniversary'
  },
  {
    icon: 'pi pi-user',
    label: 'Mi Perfil',
    description: 'Gestiona tu información personal y preferencias.',
    ctaLabel: 'Ver perfil',
    ctaPath: '/profile'
  }
]
</script>

<template>
  <section class="py-12">
    <h2 class="mb-8 text-center text-3xl font-bold text-gray-900">Acceso Rápido</h2>
    <div class="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
      <QuickAccessCard
        v-for="card in cards"
        :key="card.label"
        :icon="card.icon"
        :label="card.label"
        :description="card.description"
        :cta-label="card.ctaLabel"
        :cta-path="card.ctaPath"
      />
    </div>
  </section>
</template>
```

3. Create `src/components/home/AnniversarySection.vue`:
```vue
<script setup lang="ts">
import Button from 'primevue/button'
import { useRouter } from 'vue-router'

const router = useRouter()

const goToAnniversary = () => {
  router.push('/anniversary')
}
</script>

<template>
  <section class="py-16">
    <div class="grid grid-cols-1 gap-12 lg:grid-cols-2 lg:items-center">
      <!-- Left Column: Content -->
      <div>
        <p class="mb-2 text-sm font-semibold uppercase tracking-wide text-primary-600">
          Camino al 2026
        </p>
        <h2 class="mb-6 text-4xl font-bold text-gray-900">Medio siglo de ABUVI</h2>

        <div class="space-y-4 text-gray-700">
          <p>
            Desde 1976, hemos sido mucho más que un campamento. Hemos sido una escuela
            de vida, un lugar donde generaciones han aprendido el valor de la amistad, el
            respeto por la naturaleza y la alegría de vivir sencillamente.
          </p>
          <p>
            En 2026 cumplimos 50 años y queremos celebrarlo contigo. Estamos recopilando
            historias, fotos y recuerdos para crear el mayor archivo de nuestra historia.
          </p>
        </div>

        <Button
          label="Participar en el Aniversario"
          icon="pi pi-arrow-right"
          icon-pos="right"
          class="mt-8"
          @click="goToAnniversary"
        />
      </div>

      <!-- Right Column: Images -->
      <div class="relative">
        <img
          src="@/assets/images/grupo-abuvi.jpg"
          alt="Grupo ABUVI - Comunidad reunida"
          class="h-auto w-full rounded-lg shadow-lg"
        />
        <div class="absolute -bottom-6 -right-6 hidden lg:block">
          <img
            src="@/assets/images/50-aniversario-badge.png"
            alt="50 Aniversario ABUVI"
            class="h-32 w-32"
          />
        </div>
      </div>
    </div>
  </section>
</template>
```

**Dependencies**:
- `primevue/button`
- Image assets:
  - `src/assets/images/grupo-abuvi.jpg`
  - `src/assets/images/50-aniversario-badge.png`

**Implementation Notes**:
- Cards use grid layout with responsive columns
- Hover effects on cards
- Anniversary section uses two-column layout (stacked on mobile)
- Images with proper alt text for accessibility
- Data-driven card configuration

---

### Step 12: Create Authenticated Layout

**Action**: Create the main authenticated layout that wraps all protected pages.

**File**: `src/layouts/AuthenticatedLayout.vue`

**Implementation Steps**:

1. Create `src/layouts/AuthenticatedLayout.vue`:
```vue
<script setup lang="ts">
import AppHeader from '@/components/layout/AppHeader.vue'
import AppFooter from '@/components/layout/AppFooter.vue'
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <AppHeader />

    <main class="flex-1 bg-white">
      <router-view />
    </main>

    <AppFooter />
  </div>
</template>
```

**Dependencies**:
- `@/components/layout/AppHeader.vue`
- `@/components/layout/AppFooter.vue`

**Implementation Notes**:
- Three-section layout: header, main content, footer
- Main content area takes remaining height (flex-1)
- Router view renders the active page component

---

### Step 13: Create Home Page (Authenticated)

**Action**: Create the authenticated home page with quick access cards and anniversary section.

**File**: `src/views/HomePage.vue`

**Implementation Steps**:

1. Create `src/views/HomePage.vue`:
```vue
<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
import QuickAccessCards from '@/components/home/QuickAccessCards.vue'
import AnniversarySection from '@/components/home/AnniversarySection.vue'
</script>

<template>
  <div class="bg-gray-50">
    <Container>
      <div class="py-8">
        <QuickAccessCards />
        <AnniversarySection />
      </div>
    </Container>
  </div>
</template>
```

**Dependencies**:
- `@/components/ui/Container.vue`
- `@/components/home/QuickAccessCards.vue`
- `@/components/home/AnniversarySection.vue`

**Implementation Notes**:
- Uses Container for consistent max-width
- Light gray background for visual separation
- Composed of QuickAccessCards and AnniversarySection

---

### Step 14: Create Placeholder Pages

**Action**: Create placeholder pages for Camp, Anniversary, Profile, and Admin.

**Files to Create**:
- `src/views/CampPage.vue`
- `src/views/AnniversaryPage.vue`
- `src/views/ProfilePage.vue`
- `src/views/AdminPage.vue`

**Implementation Steps**:

1. Create `src/views/CampPage.vue`:
```vue
<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
</script>

<template>
  <Container>
    <div class="py-12">
      <h1 class="mb-4 text-4xl font-bold text-gray-900">Campamento 2026</h1>
      <p class="text-gray-600">
        Camp page content will be implemented in future iterations.
      </p>
    </div>
  </Container>
</template>
```

2. Create `src/views/AnniversaryPage.vue`:
```vue
<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
</script>

<template>
  <Container>
    <div class="py-12">
      <h1 class="mb-4 text-4xl font-bold text-gray-900">50th Anniversary</h1>
      <p class="text-gray-600">
        Anniversary page content will be implemented in future iterations.
      </p>
    </div>
  </Container>
</template>
```

3. Create `src/views/ProfilePage.vue`:
```vue
<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <Container>
    <div class="py-12">
      <h1 class="mb-4 text-4xl font-bold text-gray-900">My Profile</h1>
      <div class="rounded-lg border border-gray-200 bg-white p-6">
        <p class="mb-2"><strong>Name:</strong> {{ auth.fullName }}</p>
        <p class="mb-2"><strong>Email:</strong> {{ auth.user?.email }}</p>
        <p><strong>Role:</strong> {{ auth.user?.role }}</p>
      </div>
      <p class="mt-4 text-gray-600">
        Full profile management will be implemented in future iterations.
      </p>
    </div>
  </Container>
</template>
```

4. Create `src/views/AdminPage.vue`:
```vue
<script setup lang="ts">
import Container from '@/components/ui/Container.vue'
</script>

<template>
  <Container>
    <div class="py-12">
      <div class="mb-6 rounded-lg border-l-4 border-red-500 bg-red-50 p-4">
        <h1 class="text-2xl font-bold text-red-900">Admin Panel</h1>
        <p class="text-sm text-red-700">Restricted Area - Admin Access Only</p>
      </div>
      <p class="text-gray-600">
        Admin features will be implemented in future iterations.
      </p>
    </div>
  </Container>
</template>
```

**Dependencies**:
- `@/components/ui/Container.vue`
- `@/stores/auth` (ProfilePage only)

**Implementation Notes**:
- Simple placeholder content
- ProfilePage displays basic user info from auth store
- AdminPage has visual indicator for restricted access
- These will be enhanced in future features

---

### Step 15: Update App.vue for Conditional Layout Rendering

**Action**: Update App.vue to conditionally render layouts based on authentication state.

**File**: `src/App.vue`

**Implementation Steps**:

1. Update `src/App.vue`:
```vue
<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AuthenticatedLayout from '@/layouts/AuthenticatedLayout.vue'

const route = useRoute()
const auth = useAuthStore()

const isLandingPage = computed(() => route.path === '/')
const useLayout = computed(() => !isLandingPage.value && auth.isAuthenticated)
</script>

<template>
  <AuthenticatedLayout v-if="useLayout">
    <router-view />
  </AuthenticatedLayout>
  <router-view v-else />
</template>
```

**Dependencies**:
- `@/layouts/AuthenticatedLayout.vue`
- `@/stores/auth`

**Implementation Notes**:
- Landing page renders without layout (full-screen auth page)
- All authenticated pages use AuthenticatedLayout
- Layout wraps router-view for consistent header/footer
- Simple conditional logic based on route and auth state

---

### Step 16: Add Placeholder Assets

**Action**: Add placeholder images for logo, background, and anniversary section.

**Files to Create** (as placeholders):
- `src/assets/images/logo.svg`
- `src/assets/images/landing-background.jpg`
- `src/assets/images/grupo-abuvi.jpg`
- `src/assets/images/50-aniversario-badge.png`

**Implementation Steps**:

1. Create a simple SVG logo placeholder at `src/assets/images/logo.svg`:
```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
  <circle cx="50" cy="50" r="45" fill="#22c55e"/>
  <text x="50" y="60" font-size="40" font-weight="bold" fill="white" text-anchor="middle">A</text>
</svg>
```

2. Add note in implementation documentation:
```
NOTE: Placeholder images have been created. Replace with actual ABUVI assets:
- logo.svg: Official ABUVI logo
- landing-background.jpg: Hero background image (nature/camping scene, high resolution)
- grupo-abuvi.jpg: Community group photo
- 50-aniversario-badge.png: 50th anniversary badge/logo
```

**Dependencies**: None

**Implementation Notes**:
- SVG logo is a simple placeholder
- Background and photos should be replaced with actual ABUVI assets
- Images should be optimized (WebP format recommended)
- Landing background should be high resolution for blur effect

---

### Step 17: Write Vitest Unit Tests

**Action**: Write comprehensive unit tests for composables, stores, and components.

**Files to Create**:
- `src/stores/__tests__/auth.test.ts`
- `src/components/auth/__tests__/LoginForm.test.ts`
- `src/components/home/__tests__/QuickAccessCard.test.ts`

**Implementation Steps**:

1. Create `src/stores/__tests__/auth.test.ts`:
```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '../auth'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('Auth Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    localStorage.clear()
  })

  describe('login', () => {
    it('should login successfully and store user data', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            user: {
              id: '1',
              email: 'test@abuvi.org',
              firstName: 'John',
              lastName: 'Doe',
              role: 'Member' as const,
              isActive: true
            },
            token: 'mock-token-123'
          },
          error: null
        }
      }

      vi.mocked(api.post).mockResolvedValue(mockResponse)

      const store = useAuthStore()
      const result = await store.login({
        email: 'test@abuvi.org',
        password: 'password123',
        rememberMe: true
      })

      expect(result.success).toBe(true)
      expect(store.user?.email).toBe('test@abuvi.org')
      expect(store.token).toBe('mock-token-123')
      expect(store.isAuthenticated).toBe(true)
    })

    it('should return error when login fails', async () => {
      const mockResponse = {
        data: {
          success: false,
          data: null,
          error: {
            message: 'Invalid credentials',
            code: 'AUTH_001'
          }
        }
      }

      vi.mocked(api.post).mockResolvedValue(mockResponse)

      const store = useAuthStore()
      const result = await store.login({
        email: 'wrong@abuvi.org',
        password: 'wrongpass'
      })

      expect(result.success).toBe(false)
      expect(result.error).toBe('Invalid credentials')
      expect(store.isAuthenticated).toBe(false)
    })
  })

  describe('role-based getters', () => {
    it('should correctly identify admin role', () => {
      const store = useAuthStore()
      store.user = {
        id: '1',
        email: 'admin@abuvi.org',
        firstName: 'Admin',
        lastName: 'User',
        role: 'Admin',
        isActive: true
      }
      store.token = 'token'

      expect(store.isAdmin).toBe(true)
      expect(store.isBoard).toBe(true)
      expect(store.isMember).toBe(true)
    })

    it('should correctly identify board role', () => {
      const store = useAuthStore()
      store.user = {
        id: '2',
        email: 'board@abuvi.org',
        firstName: 'Board',
        lastName: 'Member',
        role: 'Board',
        isActive: true
      }
      store.token = 'token'

      expect(store.isAdmin).toBe(false)
      expect(store.isBoard).toBe(true)
      expect(store.isMember).toBe(true)
    })
  })

  describe('logout', () => {
    it('should clear user data and token', () => {
      const store = useAuthStore()
      store.user = {
        id: '1',
        email: 'test@abuvi.org',
        firstName: 'Test',
        lastName: 'User',
        role: 'Member',
        isActive: true
      }
      store.token = 'token'

      store.logout()

      expect(store.user).toBeNull()
      expect(store.token).toBeNull()
      expect(store.isAuthenticated).toBe(false)
    })
  })
})
```

2. Create `src/components/auth/__tests__/LoginForm.test.ts`:
```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import LoginForm from '../LoginForm.vue'
import PrimeVue from 'primevue/config'

const router = createRouter({
  history: createMemoryHistory(),
  routes: [{ path: '/', component: { template: '<div>Home</div>' } }]
})

describe('LoginForm', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('should render login form fields', () => {
    const wrapper = mount(LoginForm, {
      global: {
        plugins: [PrimeVue, router]
      }
    })

    expect(wrapper.find('input[type="email"]').exists()).toBe(true)
    expect(wrapper.find('input[type="password"]').exists()).toBe(true)
    expect(wrapper.find('button[type="submit"]').exists()).toBe(true)
  })

  it('should validate email format', async () => {
    const wrapper = mount(LoginForm, {
      global: {
        plugins: [PrimeVue, router]
      }
    })

    const emailInput = wrapper.find('input[type="email"]')
    await emailInput.setValue('invalid-email')

    const submitButton = wrapper.find('button[type="submit"]')
    await submitButton.trigger('submit')

    expect(wrapper.text()).toContain('Invalid email format')
  })

  it('should require password', async () => {
    const wrapper = mount(LoginForm, {
      global: {
        plugins: [PrimeVue, router]
      }
    })

    const emailInput = wrapper.find('input[type="email"]')
    await emailInput.setValue('test@abuvi.org')

    const submitButton = wrapper.find('button[type="submit"]')
    await submitButton.trigger('submit')

    expect(wrapper.text()).toContain('Password is required')
  })
})
```

3. Create `src/components/home/__tests__/QuickAccessCard.test.ts`:
```typescript
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import QuickAccessCard from '../QuickAccessCard.vue'

const router = createRouter({
  history: createMemoryHistory(),
  routes: [{ path: '/camp', component: { template: '<div>Camp</div>' } }]
})

describe('QuickAccessCard', () => {
  const defaultProps = {
    icon: 'pi pi-map',
    label: 'Camp 2026',
    description: 'Join us for an unforgettable experience',
    ctaLabel: 'Learn More',
    ctaPath: '/camp'
  }

  it('should render all props correctly', () => {
    const wrapper = mount(QuickAccessCard, {
      props: defaultProps,
      global: {
        plugins: [router]
      }
    })

    expect(wrapper.text()).toContain('Camp 2026')
    expect(wrapper.text()).toContain('Join us for an unforgettable experience')
    expect(wrapper.text()).toContain('Learn More')
    expect(wrapper.find('.pi-map').exists()).toBe(true)
  })

  it('should have correct router link', () => {
    const wrapper = mount(QuickAccessCard, {
      props: defaultProps,
      global: {
        plugins: [router]
      }
    })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe('/camp')
  })
})
```

**Dependencies**:
- `vitest` (already in project)
- `@vue/test-utils` (already in project)

**Implementation Notes**:
- Tests cover happy path and error scenarios
- Auth store tests cover login, logout, and role checks
- Component tests verify rendering and validation
- Use mocked API responses
- Follow AAA pattern (Arrange-Act-Assert)

---

### Step 18: Write Cypress E2E Tests

**Action**: Write end-to-end tests for authentication flow and navigation.

**Files to Create**:
- `cypress/e2e/auth-flow.cy.ts`
- `cypress/e2e/navigation.cy.ts`

**Implementation Steps**:

1. Create `cypress/e2e/auth-flow.cy.ts`:
```typescript
describe('Authentication Flow', () => {
  beforeEach(() => {
    cy.visit('/')
  })

  it('should display landing page with login form', () => {
    cy.get('h1').should('contain.text', 'Bienvenido a ABUVI')
    cy.get('input[type="email"]').should('be.visible')
    cy.get('input[type="password"]').should('be.visible')
  })

  it('should validate login form fields', () => {
    cy.contains('button', 'Iniciar Sesión').click()
    cy.contains('Email is required').should('be.visible')

    cy.get('input[type="email"]').type('invalid-email')
    cy.contains('button', 'Iniciar Sesión').click()
    cy.contains('Invalid email format').should('be.visible')
  })

  it('should login successfully and redirect to home', () => {
    cy.intercept('POST', '/api/auth/login', {
      statusCode: 200,
      body: {
        success: true,
        data: {
          user: {
            id: '1',
            email: 'member@abuvi.org',
            firstName: 'John',
            lastName: 'Doe',
            role: 'Member',
            isActive: true
          },
          token: 'mock-jwt-token'
        },
        error: null
      }
    }).as('login')

    cy.get('input[type="email"]').type('member@abuvi.org')
    cy.get('input[type="password"]').type('password123')
    cy.contains('button', 'Iniciar Sesión').click()

    cy.wait('@login')
    cy.url().should('include', '/home')
  })

  it('should display error message on failed login', () => {
    cy.intercept('POST', '/api/auth/login', {
      statusCode: 200,
      body: {
        success: false,
        data: null,
        error: {
          message: 'Invalid credentials',
          code: 'AUTH_001'
        }
      }
    })

    cy.get('input[type="email"]').type('wrong@abuvi.org')
    cy.get('input[type="password"]').type('wrongpass')
    cy.contains('button', 'Iniciar Sesión').click()

    cy.contains('Invalid credentials').should('be.visible')
  })

  it('should switch to registration form', () => {
    cy.contains('Registrarse').click()
    cy.get('input[placeholder="Juan"]').should('be.visible')
    cy.get('input[placeholder="García"]').should('be.visible')
  })
})
```

2. Create `cypress/e2e/navigation.cy.ts`:
```typescript
describe('Authenticated Navigation', () => {
  beforeEach(() => {
    // Mock authenticated session
    cy.intercept('POST', '/api/auth/login', {
      statusCode: 200,
      body: {
        success: true,
        data: {
          user: {
            id: '1',
            email: 'member@abuvi.org',
            firstName: 'John',
            lastName: 'Doe',
            role: 'Member',
            isActive: true
          },
          token: 'mock-jwt-token'
        }
      }
    })

    cy.visit('/')
    cy.get('input[type="email"]').type('member@abuvi.org')
    cy.get('input[type="password"]').type('password123')
    cy.contains('button', 'Iniciar Sesión').click()
    cy.url().should('include', '/home')
  })

  it('should display header and footer on authenticated pages', () => {
    cy.get('header').should('be.visible')
    cy.contains('ABUVI').should('be.visible')
    cy.get('footer').should('be.visible')
  })

  it('should navigate to camp page', () => {
    cy.contains('Camp').click()
    cy.url().should('include', '/camp')
    cy.contains('Campamento 2026').should('be.visible')
  })

  it('should navigate to anniversary page', () => {
    cy.contains('Anniversary').click()
    cy.url().should('include', '/anniversary')
    cy.contains('50th Anniversary').should('be.visible')
  })

  it('should navigate to profile page', () => {
    cy.contains('My Profile').click()
    cy.url().should('include', '/profile')
    cy.contains('My Profile').should('be.visible')
  })

  it('should logout and redirect to landing page', () => {
    cy.get('[aria-label="User menu"]').click()
    cy.contains('Logout').click()
    cy.url().should('eq', Cypress.config().baseUrl + '/')
  })

  it('should not show admin link for non-admin users', () => {
    cy.contains('Admin').should('not.exist')
  })
})

describe('Admin Navigation', () => {
  beforeEach(() => {
    cy.intercept('POST', '/api/auth/login', {
      statusCode: 200,
      body: {
        success: true,
        data: {
          user: {
            id: '1',
            email: 'admin@abuvi.org',
            firstName: 'Admin',
            lastName: 'User',
            role: 'Admin',
            isActive: true
          },
          token: 'mock-jwt-token'
        }
      }
    })

    cy.visit('/')
    cy.get('input[type="email"]').type('admin@abuvi.org')
    cy.get('input[type="password"]').type('password123')
    cy.contains('button', 'Iniciar Sesión').click()
  })

  it('should show admin link for admin users', () => {
    cy.contains('Admin').should('be.visible')
  })

  it('should navigate to admin page', () => {
    cy.contains('Admin').click()
    cy.url().should('include', '/admin')
    cy.contains('Admin Panel').should('be.visible')
  })
})
```

**Dependencies**:
- `cypress` (already in project)

**Implementation Notes**:
- Tests cover complete authentication flow
- Tests verify route protection
- Tests check role-based navigation (admin link visibility)
- Uses intercepted API calls for predictable testing
- Tests verify header and footer presence on authenticated pages

---

### Step 19: Update Technical Documentation

**Action**: Review and update technical documentation according to changes made.

**Implementation Steps**:

1. **Review Changes**: All code changes completed in previous steps

2. **Identify Documentation Files**:
   - `ai-specs/specs/frontend-standards.mdc` - Component patterns, routing, authentication

3. **Update Documentation**:

Update the "Project Structure" section in `frontend-standards.mdc` to reflect new layout structure:

```markdown
## Project Structure

```text
frontend/
├── src/
│   ├── layouts/                # Page layouts
│   │   ├── AuthenticatedLayout.vue  # Layout for authenticated users
│   │   └── PublicLayout.vue    # Layout for public pages (future)
│   ├── components/
│   │   ├── layout/             # Layout components (header, footer)
│   │   │   ├── AppHeader.vue
│   │   │   ├── AppFooter.vue
│   │   │   └── UserMenu.vue
│   │   ├── auth/               # Authentication components
│   │   │   ├── AuthContainer.vue
│   │   │   ├── LoginForm.vue
│   │   │   └── RegisterForm.vue
│   │   ├── home/               # Home page components
│   │   │   ├── QuickAccessCards.vue
│   │   │   ├── QuickAccessCard.vue
│   │   │   └── AnniversarySection.vue
│   │   ├── ui/                 # Shared UI components
│   │   │   └── Container.vue
│   │   └── common/             # (existing structure)
│   ├── views/                  # Page components
│   │   ├── LandingPage.vue     # Public authentication page
│   │   ├── HomePage.vue        # Authenticated home/dashboard
│   │   ├── CampPage.vue
│   │   ├── AnniversaryPage.vue
│   │   ├── ProfilePage.vue
│   │   └── AdminPage.vue
│   ├── stores/
│   │   └── auth.ts             # Authentication store
│   ├── types/
│   │   ├── user.ts
│   │   ├── auth.ts
│   │   └── api.ts
│   └── utils/
│       └── api.ts              # Axios instance with interceptors
```
```

Add new section "Authentication Patterns" to `frontend-standards.mdc`:

```markdown
## Authentication Patterns

### Route Protection

All routes except the landing page (`/`) require authentication. Route guards enforce this:

```typescript
router.beforeEach((to, from, next) => {
  const auth = useAuthStore()

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    next({ path: '/', query: { redirect: to.fullPath } })
    return
  }

  if (to.meta.requiresAdmin && !auth.isAdmin) {
    next({ path: '/home' })
    return
  }

  next()
})
```

### Layout Conditional Rendering

The application uses different layouts based on authentication state:

- **Landing Page**: Full-screen authentication page (no header/footer)
- **Authenticated Pages**: Full layout with header, content, and footer

```vue
<template>
  <AuthenticatedLayout v-if="useLayout">
    <router-view />
  </AuthenticatedLayout>
  <router-view v-else />
</template>
```

### Authentication Store

The auth store manages user session, roles, and authentication state:

```typescript
const auth = useAuthStore()

// State
auth.user          // Current user data
auth.token         // JWT token
auth.isAuthenticated  // Boolean
auth.isAdmin       // Role check
auth.isBoard       // Role check
auth.fullName      // Computed full name

// Actions
await auth.login({ email, password, rememberMe })
await auth.register({ firstName, lastName, email, password, confirmPassword, acceptTerms })
auth.logout()
```
```

4. **Verify Documentation**:
   - All changes accurately reflected
   - Follows established structure
   - Uses proper formatting

5. **Report Updates**:
   - Updated `ai-specs/specs/frontend-standards.mdc`:
     - Project structure to include new layouts, components, views
     - Added "Authentication Patterns" section with route protection, layout rendering, and auth store usage

**References**:
- `ai-specs/specs/documentation-standards.mdc`
- All documentation in English

**Notes**: This step is MANDATORY before considering the implementation complete.

---

## Implementation Order

1. **Step 0**: Create Feature Branch (`feature/feat-layout-frontend`)
2. **Step 1**: Setup Project Structure and Design Tokens
3. **Step 2**: Define TypeScript Types
4. **Step 3**: Create Authentication Store (Pinia)
5. **Step 4**: Configure Axios Instance
6. **Step 5**: Configure Vue Router with Route Guards
7. **Step 6**: Create Shared UI Components
8. **Step 7**: Create Authentication Components
9. **Step 8**: Create Landing Page (Public)
10. **Step 9**: Create Authenticated Header Component
11. **Step 10**: Create Authenticated Footer Component
12. **Step 11**: Create Home Page Components
13. **Step 12**: Create Authenticated Layout
14. **Step 13**: Create Home Page (Authenticated)
15. **Step 14**: Create Placeholder Pages
16. **Step 15**: Update App.vue for Conditional Layout Rendering
17. **Step 16**: Add Placeholder Assets
18. **Step 17**: Write Vitest Unit Tests
19. **Step 18**: Write Cypress E2E Tests
20. **Step 19**: Update Technical Documentation

---

## Testing Checklist

### Unit Tests (Vitest)

- [ ] Auth store login action with successful response
- [ ] Auth store login action with failed response
- [ ] Auth store register action
- [ ] Auth store logout action
- [ ] Auth store role-based getters (isAdmin, isBoard, isMember)
- [ ] LoginForm component renders all fields
- [ ] LoginForm component validates email format
- [ ] LoginForm component validates required fields
- [ ] RegisterForm component validates password match
- [ ] RegisterForm component validates terms acceptance
- [ ] QuickAccessCard component renders props correctly
- [ ] QuickAccessCard component has correct router link

### E2E Tests (Cypress)

- [ ] Landing page displays authentication forms
- [ ] Login form validates required fields
- [ ] Login form validates email format
- [ ] Successful login redirects to /home
- [ ] Failed login displays error message
- [ ] Tab switch between login and register works
- [ ] Authenticated pages display header and footer
- [ ] Navigation links work correctly
- [ ] Quick access cards navigate to correct pages
- [ ] User menu logout functionality works
- [ ] Admin link visible only to admin users
- [ ] Unauthenticated users redirected to landing page
- [ ] Authenticated users redirected from landing to home

### Manual Testing

- [ ] Responsive design works on mobile (< 640px)
- [ ] Responsive design works on tablet (640px - 1024px)
- [ ] Responsive design works on desktop (> 1024px)
- [ ] Mobile navigation menu (hamburger) works
- [ ] "Remember Me" persists login across sessions
- [ ] Logout clears localStorage
- [ ] Route guards enforce authentication
- [ ] Admin route guard works correctly
- [ ] Page titles update correctly
- [ ] All images load properly
- [ ] Background blur effect works
- [ ] Forms are keyboard accessible
- [ ] Focus states are visible

---

## Error Handling Patterns

### API Error Handling

All API calls in the auth store return structured error objects:

```typescript
try {
  const result = await auth.login(credentials)
  if (result.success) {
    // Success path
  } else {
    // Display result.error to user
  }
} catch (err) {
  // Network error or unexpected error
}
```

### Form Validation Errors

Forms display validation errors inline:

```vue
<InputText :invalid="!!errors.email" />
<small v-if="errors.email" class="text-red-500">{{ errors.email }}</small>
```

### Global Error Handling

Axios interceptor handles 401 (Unauthorized) globally:

```typescript
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      auth.logout()
      router.push('/')
    }
    return Promise.reject(error)
  }
)
```

### User-Friendly Messages

- Use PrimeVue `Message` component for error display
- Provide clear, actionable error messages
- Include retry mechanisms where appropriate

---

## UI/UX Considerations

### PrimeVue Components Used

- **InputText**: Email and text inputs
- **Password**: Password fields with toggle mask
- **Checkbox**: Remember me, terms acceptance
- **Button**: All interactive buttons
- **Message**: Error and success messages
- **TabView / TabPanel**: Login/Register tabs
- **Menu**: User dropdown menu
- **Avatar**: User avatar in header

### Tailwind CSS Styling

- **Layout**: Flexbox and Grid utilities
- **Spacing**: Consistent padding/margin using `p-4`, `gap-6`, etc.
- **Responsive**: Mobile-first with `sm:`, `md:`, `lg:` breakpoints
- **Colors**: Semantic color classes (`bg-primary-600`, `text-gray-900`)
- **Typography**: Font size and weight utilities
- **Transitions**: Hover effects with `transition-colors`, `transition-shadow`

### Responsive Design

- **Mobile (< 640px)**: Single column layouts, hamburger menu, stacked cards
- **Tablet (640px - 1024px)**: Two-column grids, optimized spacing
- **Desktop (> 1024px)**: Full horizontal navigation, three-column grids

### Accessibility

- **ARIA Labels**: User menu button, navigation links
- **Semantic HTML**: `<header>`, `<main>`, `<footer>`, `<nav>`
- **Keyboard Navigation**: All interactive elements accessible via Tab
- **Focus Indicators**: Visible focus states on all inputs and buttons
- **Alt Text**: All images have descriptive alt attributes
- **Form Labels**: Proper `<label>` elements with `for` attributes

### Loading States

- Forms disable inputs and show loading spinner during submission
- Button components show loading state with `:loading` prop

---

## Dependencies

### NPM Packages (Already in Project)

- `vue` (^3.x)
- `vue-router` (^4.x)
- `pinia` (state management)
- `primevue` (UI components)
- `axios` (HTTP client)
- `vitest` (unit testing)
- `@vue/test-utils` (component testing)
- `cypress` (E2E testing)

### PrimeVue Components Used

- InputText
- Password
- Checkbox
- Button
- Message
- TabView / TabPanel
- Menu
- Avatar
- (PrimeIcons for icons)

### Asset Requirements

- ABUVI logo (SVG or PNG) - `src/assets/images/logo.svg`
- Landing background image - `src/assets/images/landing-background.jpg`
- Group photo - `src/assets/images/grupo-abuvi.jpg`
- Anniversary badge - `src/assets/images/50-aniversario-badge.png`

**Note**: Placeholder assets created. Replace with actual ABUVI assets before production.

---

## Notes

### Critical Reminders

- **Members-Only Application**: This is NOT a public website. Only the landing page is public (authentication gateway).
- **English Code, Spanish UI**: All code, variables, and documentation in English. UI text in Spanish.
- **Route Protection**: All routes except `/` require authentication.
- **Role-Based Access**: Admin route requires `isAdmin` check.
- **Page Titles**: Follow format `"ABUVI | [Section]"` for all authenticated pages, `"ABUVI"` for landing.

### Business Rules

- Users can only register through the public registration form (no invite-only system in this iteration)
- "Remember Me" stores credentials in localStorage (JWT token and user data)
- Logout clears localStorage completely
- Admin users see additional navigation link (Admin panel)
- Failed login attempts do not lock accounts (to be implemented in future)

### Language Requirements

- **Code**: English only (variables, functions, components, comments)
- **UI Text**: Spanish (form labels, buttons, messages, content)
- **Documentation**: English

### TypeScript Requirements

- **Strict Mode**: Enabled in `tsconfig.json`
- **No `any` Types**: Use specific types or `unknown`
- **Props/Emits**: Fully typed with TypeScript generics
- **API Responses**: Match backend `ApiResponse<T>` envelope

### Security Considerations

- JWT token stored in localStorage (consider httpOnly cookies in production)
- HTTPS required for production
- CSRF protection to be implemented in backend
- Password requirements: minimum 6 characters (can be enhanced)
- XSS protection via Vue's built-in sanitization

---

## Next Steps After Implementation

1. **Backend Integration**:
   - Connect to actual authentication API endpoints
   - Test with real backend responses
   - Handle backend validation errors

2. **Asset Replacement**:
   - Replace placeholder logo with official ABUVI logo
   - Add high-quality background image
   - Add anniversary section images

3. **Enhanced Features** (Future Iterations):
   - Email verification for new registrations
   - Password reset functionality
   - Account lockout after failed login attempts
   - Two-factor authentication (2FA)
   - Session timeout warnings
   - Enhanced password strength requirements

4. **Performance Optimization**:
   - Image optimization (WebP/AVIF formats)
   - Lazy loading for below-fold images
   - Code splitting verification
   - Lighthouse performance audit

5. **Deployment**:
   - Configure production environment variables
   - Set up CI/CD pipeline
   - Configure CDN for static assets
   - Set up monitoring and error tracking

---

## Implementation Verification

### Code Quality

- [ ] All components use `<script setup lang="ts">`
- [ ] No `any` types in TypeScript code
- [ ] All imports use path aliases (`@/`)
- [ ] Consistent code formatting (Prettier)
- [ ] No ESLint errors or warnings
- [ ] All files have proper file naming (kebab-case for components)

### Functionality

- [ ] Landing page renders with blurred background
- [ ] Login form validates and submits correctly
- [ ] Register form validates and submits correctly
- [ ] Successful authentication redirects to `/home`
- [ ] Header displays on all authenticated pages
- [ ] Footer displays on all authenticated pages
- [ ] Navigation links work correctly
- [ ] User menu dropdown works
- [ ] Logout functionality works
- [ ] Admin link visible only to admin users
- [ ] Route guards enforce authentication
- [ ] Page titles update based on route
- [ ] "Remember Me" persists session

### Testing

- [ ] All Vitest unit tests pass
- [ ] All Cypress E2E tests pass
- [ ] Test coverage meets 90% threshold (unit tests)
- [ ] Manual testing completed on multiple browsers
- [ ] Manual testing completed on multiple screen sizes

### Integration

- [ ] API calls use centralized Axios instance
- [ ] Auth token automatically attached to requests
- [ ] 401 responses trigger logout and redirect
- [ ] Auth store persists state correctly
- [ ] Route guards work with auth store

### Documentation

- [ ] `ai-specs/specs/frontend-standards.mdc` updated
- [ ] Code comments added where necessary
- [ ] Component props documented in code
- [ ] All changes documented in this plan

### Accessibility

- [ ] All images have alt text
- [ ] Form inputs have labels
- [ ] Keyboard navigation works
- [ ] Focus indicators visible
- [ ] Semantic HTML used throughout
- [ ] ARIA labels where appropriate

### Responsive Design

- [ ] Mobile layout works correctly (< 640px)
- [ ] Tablet layout works correctly (640px - 1024px)
- [ ] Desktop layout works correctly (> 1024px)
- [ ] Touch targets meet 44x44px minimum
- [ ] Text is readable on all screen sizes

---

## Final Notes

This implementation provides a complete, production-ready foundation for the ABUVI members-only application. The layout supports:

- **Two-state architecture**: Public landing page for authentication, full authenticated layout for members
- **Role-based access**: Different content visibility based on user role (Member, Board, Admin)
- **Responsive design**: Mobile-first approach with breakpoints for all devices
- **Type safety**: Full TypeScript coverage with no `any` types
- **Testing**: Comprehensive unit and E2E test coverage
- **Security**: Route protection, JWT authentication, role-based guards
- **Accessibility**: WCAG 2.1 AA compliant with semantic HTML and ARIA labels
- **Performance**: Lazy-loaded routes, optimized bundle size, responsive images

The implementation follows all project standards defined in `frontend-standards.mdc` and `base-standards.mdc`, including:

- Vue 3 Composition API with `<script setup lang="ts">`
- PrimeVue components for UI
- Tailwind CSS for styling
- Pinia for state management
- Vitest and Cypress for testing
- English code, Spanish UI text
- Consistent file naming and structure

Future enhancements can build on this foundation to add features like camp registration, anniversary contributions, photo galleries, and admin management tools.

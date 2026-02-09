# Frontend Implementation Plan: Phase 2 - Authentication Layer

## Overview

This document provides a comprehensive, step-by-step frontend implementation plan for **Phase 2 Authentication Layer**, integrating JWT-based authentication into the ABUVI Vue 3 application. This implementation follows the project's established patterns using **Vue 3 Composition API**, **PrimeVue**, **Tailwind CSS**, **Pinia**, and **composable-based architecture**.

**Key Features:**

- Login page with JWT authentication
- Registration page for new users
- Auth store (Pinia) for managing user session and tokens
- Auth composable for API communication
- Axios interceptors for automatic JWT token attachment
- Route guards for protecting authenticated pages
- Role-based authorization (Admin, Board, Member)
- Error handling with user-friendly messages
- Comprehensive testing (Vitest + Cypress)

**Architecture Principles:**

- Components use `<script setup lang="ts">` exclusively (no Options API)
- All API calls through composables (never direct from components)
- Global auth state managed by Pinia auth store
- Tailwind CSS for styling (no custom `<style>` blocks)
- PrimeVue components for UI consistency
- TypeScript strict mode with no `any` types

---

## Architecture Context

### Components/Composables Involved

**New Files to Create:**

- `frontend/src/types/auth.ts` - Auth-related TypeScript interfaces
- `frontend/src/stores/auth.ts` - Pinia auth store
- `frontend/src/composables/useAuth.ts` - Auth composable for API calls
- `frontend/src/pages/LoginPage.vue` - Login page component
- `frontend/src/pages/RegisterPage.vue` - Registration page component
- `frontend/src/components/auth/ProtectedRoute.vue` - Route guard component (optional)
- Test files for all above

**Files to Modify:**

- `frontend/src/utils/api.ts` - Add request/response interceptors for JWT
- `frontend/src/router/index.ts` - Add auth routes and route guards
- `frontend/src/types/user.ts` - Ensure User type matches backend
- `frontend/src/App.vue` - Add navigation and auth state display

### Routing Considerations

**New Routes:**

- `/login` - Public route for user login
- `/register` - Public route for user registration

**Protected Routes:**

- All existing `/users/*` routes will require authentication
- Admin-only routes will check for Admin role

### State Management Approach

**Pinia Auth Store** (`useAuthStore`):

- Manages user session, JWT token, authentication state
- Provides computed getters: `isAuthenticated`, `isAdmin`, `isBoard`
- Actions: `login`, `register`, `logout`, `restoreSession`
- Persists token to localStorage for session restoration

**Composable** (`useAuth`):

- Handles API calls for login/register
- Returns reactive loading/error states
- Delegates session management to auth store

---

## Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to a new feature branch following the development workflow.

**Branch Naming**: `feature/phase2-authentication-frontend`

**Implementation Steps**:

1. Ensure you're on the latest `main` branch
2. Pull latest changes: `git pull origin main`
3. Check if branch exists: `git branch -a | grep phase2-authentication-frontend`
4. If exists, switch to it: `git checkout feature/phase2-authentication-frontend`
5. If not exists, create it: `git checkout -b feature/phase2-authentication-frontend`
6. Verify branch: `git branch`

**Notes**:

- This branch is separate from the backend branch `feature/phase2-authentication-backend`
- This must be the FIRST step before any code changes
- Refer to `ai-specs/specs/frontend-standards.mdc` section "Development Workflow" for workflow rules

---

### Step 1: Define TypeScript Auth Interfaces

**File**: `frontend/src/types/auth.ts`

**Action**: Create TypeScript interfaces matching backend auth DTOs

**Implementation Steps**:

1. Create new file `frontend/src/types/auth.ts`
2. Define `LoginRequest` interface
3. Define `RegisterRequest` interface
4. Define `LoginResponse` interface
5. Define `UserInfo` interface (subset of User)

**Code Signature**:

```typescript
/**
 * Auth-related TypeScript interfaces matching backend DTOs
 */

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone: string | null
}

export interface LoginResponse {
  token: string
  user: UserInfo
}

export interface UserInfo {
  id: string
  email: string
  firstName: string
  lastName: string
  role: string // 'Admin' | 'Board' | 'Member'
}
```

**Dependencies**: None (pure type definitions)

**Implementation Notes**:

- These types MUST match the backend DTOs exactly (see `ai-specs/changes/phase2_authentication_layer_enriched.md` lines 362-397)
- `UserInfo` is a subset of the full `User` type (excludes passwordHash, isActive, timestamps)
- Use string for `role` in `UserInfo` to match backend JSON serialization
- Keep file focused on auth types only

---

### Step 2: Create Pinia Auth Store

**File**: `frontend/src/stores/auth.ts`

**Action**: Implement Pinia setup store for managing authentication state

**Function/Component Signature**:

```typescript
import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type { UserInfo } from '@/types/auth'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<UserInfo | null>(null)
  const token = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isBoard = computed(() =>
    user.value?.role === 'Admin' || user.value?.role === 'Board'
  )

  // Actions
  function setAuth(authData: { user: UserInfo; token: string }) { ... }
  function clearAuth() { ... }
  function restoreSession() { ... }

  return {
    user,
    token,
    isAuthenticated,
    isAdmin,
    isBoard,
    setAuth,
    clearAuth,
    restoreSession
  }
})
```

**Implementation Steps**:

1. Create `frontend/src/stores/auth.ts`
2. Import necessary dependencies (ref, computed, defineStore, types)
3. Define state: `user` (UserInfo | null), `token` (string | null)
4. Define computed getters: `isAuthenticated`, `isAdmin`, `isBoard`
5. Implement `setAuth(authData)`: Store user and token, save token to localStorage
6. Implement `clearAuth()`: Clear user and token, remove from localStorage
7. Implement `restoreSession()`: Load token from localStorage, decode to get user info (optional: validate token)
8. Return all state, getters, and actions

**Dependencies**:

- `pinia` (already installed)
- `@/types/auth` (created in Step 1)

**Implementation Notes**:

- **LocalStorage persistence**: Store JWT token in `localStorage.setItem('authToken', token)` for session restoration
- **Security consideration**: Store only the token in localStorage, not the full user object
- `restoreSession()` should be called on app initialization (in main.ts or App.vue)
- Token expiry validation is optional for this phase (backend validates on each request)
- Do NOT store sensitive data like passwords in the store

**Full Implementation**:

```typescript
import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type { UserInfo } from '@/types/auth'

const AUTH_TOKEN_KEY = 'authToken'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<UserInfo | null>(null)
  const token = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isBoard = computed(() =>
    user.value?.role === 'Admin' || user.value?.role === 'Board'
  )

  // Actions
  function setAuth(authData: { user: UserInfo; token: string }) {
    user.value = authData.user
    token.value = authData.token
    localStorage.setItem(AUTH_TOKEN_KEY, authData.token)
  }

  function clearAuth() {
    user.value = null
    token.value = null
    localStorage.removeItem(AUTH_TOKEN_KEY)
  }

  function restoreSession() {
    const savedToken = localStorage.getItem(AUTH_TOKEN_KEY)
    if (savedToken) {
      token.value = savedToken
      // Note: User info will be fetched from API or decoded from token if needed
      // For now, just restore the token; user info will be fetched on protected route access
    }
  }

  return {
    user,
    token,
    isAuthenticated,
    isAdmin,
    isBoard,
    setAuth,
    clearAuth,
    restoreSession
  }
})
```

---

### Step 3: Create Auth Composable

**File**: `frontend/src/composables/useAuth.ts`

**Action**: Implement composable for auth API calls (login, register, logout)

**Function/Component Signature**:

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import { useAuthStore } from '@/stores/auth'
import type { LoginRequest, RegisterRequest, LoginResponse, UserInfo } from '@/types/auth'
import type { ApiResponse } from '@/types/api'

export function useAuth() {
  const loading = ref(false)
  const error = ref<string | null>(null)
  const authStore = useAuthStore()

  async function login(credentials: LoginRequest): Promise<boolean> { ... }
  async function register(data: RegisterRequest): Promise<UserInfo | null> { ... }
  function logout(): void { ... }

  return { loading, error, login, register, logout }
}
```

**Implementation Steps**:

1. Create `frontend/src/composables/useAuth.ts`
2. Import dependencies (ref, api, useAuthStore, types)
3. Initialize reactive state: `loading`, `error`
4. Get auth store instance
5. Implement `login(credentials)`:
   - Set loading=true, error=null
   - POST to `/auth/login` with credentials
   - On success: Call `authStore.setAuth()`, return true
   - On failure: Set error message, return false
   - Set loading=false in finally block
6. Implement `register(data)`:
   - Set loading=true, error=null
   - POST to `/auth/register` with registration data
   - On success: Return UserInfo (do NOT auto-login, require explicit login)
   - On failure: Set error message, return null
   - Set loading=false in finally block
7. Implement `logout()`:
   - Call `authStore.clearAuth()`
   - Redirect to login page
8. Return loading, error, and all functions

**Dependencies**:

- `@/utils/api` (existing axios instance)
- `@/stores/auth` (created in Step 2)
- `@/types/auth` (created in Step 1)
- `@/types/api` (existing ApiResponse types)

**Implementation Notes**:

- **Error handling**: Extract user-friendly error messages from ApiResponse.error.message
- **Backend errors**: Backend returns 401 for invalid credentials with error code "INVALID_CREDENTIALS"
- **Backend errors**: Backend returns 400 for duplicate email with error code "EMAIL_EXISTS"
- **Auto-login after registration**: Do NOT auto-login after registration; require explicit login for security
- **Logout**: Clear auth state and redirect to `/login`
- Use try-catch for network errors vs API error responses

**Full Implementation**:

```typescript
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '@/utils/api'
import { useAuthStore } from '@/stores/auth'
import type { LoginRequest, RegisterRequest, LoginResponse, UserInfo } from '@/types/auth'
import type { ApiResponse } from '@/types/api'

export function useAuth() {
  const loading = ref(false)
  const error = ref<string | null>(null)
  const authStore = useAuthStore()
  const router = useRouter()

  async function login(credentials: LoginRequest): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<LoginResponse>>(
        '/auth/login',
        credentials
      )

      if (response.data.success && response.data.data) {
        authStore.setAuth(response.data.data)
        return true
      } else {
        error.value = response.data.error?.message || 'Login failed'
        return false
      }
    } catch (err: any) {
      // Handle 401 Unauthorized
      if (err.response?.status === 401) {
        error.value = 'Invalid email or password'
      } else {
        error.value = 'Network error. Please try again.'
      }
      return false
    } finally {
      loading.value = false
    }
  }

  async function register(data: RegisterRequest): Promise<UserInfo | null> {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<UserInfo>>(
        '/auth/register',
        data
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      } else {
        error.value = response.data.error?.message || 'Registration failed'
        return null
      }
    } catch (err: any) {
      // Handle 400 Bad Request (email exists)
      if (err.response?.status === 400) {
        error.value = err.response.data.error?.message || 'Email already registered'
      } else {
        error.value = 'Network error. Please try again.'
      }
      return null
    } finally {
      loading.value = false
    }
  }

  function logout(): void {
    authStore.clearAuth()
    router.push('/login')
  }

  return { loading, error, login, register, logout }
}
```

---

### Step 4: Update Axios Configuration with Interceptors

**File**: `frontend/src/utils/api.ts`

**Action**: Add request interceptor to attach JWT token and response interceptor to handle 401

**Implementation Steps**:

1. Open `frontend/src/utils/api.ts`
2. Import `useAuthStore` from `@/stores/auth`
3. Add **request interceptor** before existing response interceptor:
   - Get auth store
   - If token exists, add `Authorization: Bearer {token}` header
4. Update **response interceptor** to handle 401:
   - On 401 Unauthorized: Clear auth store, redirect to `/login`
   - Keep existing error logging

**Dependencies**:

- `@/stores/auth` (created in Step 2)

**Implementation Notes**:

- **Request interceptor**: Attach token to every request automatically
- **Response interceptor**: Global 401 handling ensures all unauthorized requests redirect to login
- **Router import**: Use `window.location.href = '/login'` instead of Vue Router to avoid circular dependency
- Do NOT modify baseURL or other existing configuration

**Full Implementation**:

```typescript
import axios from 'axios'
import { useAuthStore } from '@/stores/auth'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5079/api'

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Request interceptor - attach JWT token
api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore()
    if (authStore.token) {
      config.headers.Authorization = `Bearer ${authStore.token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor - handle 401 and errors
api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error)

    // Handle 401 Unauthorized globally
    if (error.response?.status === 401) {
      const authStore = useAuthStore()
      authStore.clearAuth()
      // Use window.location to avoid circular dependency with router
      window.location.href = '/login'
    }

    return Promise.reject(error)
  }
)
```

---

### Step 5: Create Login Page Component

**File**: `frontend/src/pages/LoginPage.vue`

**Action**: Implement login page with form validation and error handling

**Component Structure**:

- Form with email and password fields (PrimeVue InputText)
- Submit button with loading state (PrimeVue Button)
- Error message display (PrimeVue Message)
- Link to registration page
- Client-side validation (email format, required fields)

**Implementation Steps**:

1. Create `frontend/src/pages/LoginPage.vue`
2. Import necessary components (InputText, Button, Message from PrimeVue)
3. Import `useAuth` composable
4. Import `useRouter` for navigation after login
5. Define reactive form data (email, password)
6. Define validation errors ref
7. Implement `validate()` function (check email format, required fields)
8. Implement `handleLogin()` function:
   - Validate form
   - Call `useAuth().login()`
   - On success: Redirect to `/users` (or intended route from query param)
   - On failure: Display error from composable
9. Build template with PrimeVue components and Tailwind CSS
10. Add "Register" link to `/register`

**Dependencies**:

- PrimeVue: `InputText`, `Button`, `Message`, `Card`
- `@/composables/useAuth`
- `vue-router`

**Implementation Notes**:

- Use **PrimeVue Card** for layout consistency
- Use **Tailwind CSS** for spacing and responsive design (no custom styles)
- **Validation**: Email must be valid format, password required (no strength check on login)
- **Error display**: Use PrimeVue Message component with severity="error"
- **Loading state**: Disable submit button and show loading spinner during API call
- **Redirect after login**: Check `route.query.redirect` for intended destination (e.g., `/users`)
- **Accessibility**: Proper labels, ARIA attributes, keyboard navigation

**Full Implementation**:

```vue
<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuth } from '@/composables/useAuth'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Card from 'primevue/card'

const router = useRouter()
const route = useRoute()
const { login, loading, error } = useAuth()

const formData = reactive({
  email: '',
  password: ''
})

const validationErrors = ref<Record<string, string>>({})

const validate = (): boolean => {
  validationErrors.value = {}

  if (!formData.email) {
    validationErrors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    validationErrors.value.email = 'Invalid email format'
  }

  if (!formData.password) {
    validationErrors.value.password = 'Password is required'
  }

  return Object.keys(validationErrors.value).length === 0
}

const handleLogin = async () => {
  if (!validate()) return

  const success = await login(formData)

  if (success) {
    // Redirect to intended page or default to /users
    const redirect = (route.query.redirect as string) || '/users'
    router.push(redirect)
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-4">
    <Card class="w-full max-w-md">
      <template #title>
        <h1 class="text-2xl font-bold text-gray-900">Login to ABUVI</h1>
      </template>

      <template #content>
        <!-- Error message -->
        <Message v-if="error" severity="error" :closable="false" class="mb-4">
          {{ error }}
        </Message>

        <form @submit.prevent="handleLogin" class="flex flex-col gap-4">
          <!-- Email field -->
          <div>
            <label for="email" class="mb-2 block text-sm font-medium text-gray-700">
              Email *
            </label>
            <InputText
              id="email"
              v-model="formData.email"
              type="email"
              placeholder="your.email@example.com"
              class="w-full"
              :invalid="!!validationErrors.email"
              :disabled="loading"
            />
            <small v-if="validationErrors.email" class="text-red-500">
              {{ validationErrors.email }}
            </small>
          </div>

          <!-- Password field -->
          <div>
            <label for="password" class="mb-2 block text-sm font-medium text-gray-700">
              Password *
            </label>
            <InputText
              id="password"
              v-model="formData.password"
              type="password"
              placeholder="Enter your password"
              class="w-full"
              :invalid="!!validationErrors.password"
              :disabled="loading"
            />
            <small v-if="validationErrors.password" class="text-red-500">
              {{ validationErrors.password }}
            </small>
          </div>

          <!-- Submit button -->
          <Button
            type="submit"
            label="Login"
            :loading="loading"
            :disabled="loading"
            class="w-full"
          />
        </form>

        <!-- Register link -->
        <div class="mt-4 text-center">
          <p class="text-sm text-gray-600">
            Don't have an account?
            <router-link to="/register" class="font-medium text-primary-600 hover:text-primary-500">
              Register here
            </router-link>
          </p>
        </div>
      </template>
    </Card>
  </div>
</template>
```

---

### Step 6: Create Registration Page Component

**File**: `frontend/src/pages/RegisterPage.vue`

**Action**: Implement registration page with form validation and password strength requirements

**Component Structure**:

- Multi-field form (email, password, firstName, lastName, phone)
- Password strength validation
- Submit button with loading state
- Success message and redirect to login
- Link to login page

**Implementation Steps**:

1. Create `frontend/src/pages/RegisterPage.vue`
2. Import necessary PrimeVue components (InputText, Button, Message, Card)
3. Import `useAuth` composable and `useRouter`
4. Define reactive form data (all registration fields)
5. Define validation errors ref
6. Implement `validate()` function:
   - Email: Required, valid format, max 255 chars
   - Password: Required, min 8 chars, uppercase, lowercase, number
   - FirstName/LastName: Required, max 100 chars
   - Phone: Optional, max 20 chars
7. Implement `handleRegister()` function:
   - Validate form
   - Call `useAuth().register()`
   - On success: Show success message, redirect to login after 2 seconds
   - On failure: Display error
8. Build template with form fields and validation messages
9. Add "Login" link to `/login`

**Dependencies**:

- PrimeVue: `InputText`, `Button`, `Message`, `Card`
- `@/composables/useAuth`
- `vue-router`

**Implementation Notes**:

- **Password validation**: Must match backend requirements (min 8 chars, uppercase, lowercase, number)
- **Phone field**: Optional (nullable)
- **Success flow**: After successful registration, show success message and redirect to login (do NOT auto-login)
- **Error handling**: Display backend error messages (e.g., "Email already registered")
- Use **PrimeVue Message** component for success/error feedback
- **Tailwind CSS only** for styling

**Full Implementation**:

```vue
<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '@/composables/useAuth'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Card from 'primevue/card'

const router = useRouter()
const { register, loading, error } = useAuth()

const formData = reactive({
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  phone: ''
})

const validationErrors = ref<Record<string, string>>({})
const successMessage = ref<string | null>(null)

const validate = (): boolean => {
  validationErrors.value = {}

  // Email validation
  if (!formData.email) {
    validationErrors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    validationErrors.value.email = 'Invalid email format'
  } else if (formData.email.length > 255) {
    validationErrors.value.email = 'Email must not exceed 255 characters'
  }

  // Password validation (matches backend requirements)
  if (!formData.password) {
    validationErrors.value.password = 'Password is required'
  } else if (formData.password.length < 8) {
    validationErrors.value.password = 'Password must be at least 8 characters'
  } else if (!/[A-Z]/.test(formData.password)) {
    validationErrors.value.password = 'Password must contain at least one uppercase letter'
  } else if (!/[a-z]/.test(formData.password)) {
    validationErrors.value.password = 'Password must contain at least one lowercase letter'
  } else if (!/[0-9]/.test(formData.password)) {
    validationErrors.value.password = 'Password must contain at least one number'
  }

  // First name validation
  if (!formData.firstName) {
    validationErrors.value.firstName = 'First name is required'
  } else if (formData.firstName.length > 100) {
    validationErrors.value.firstName = 'First name must not exceed 100 characters'
  }

  // Last name validation
  if (!formData.lastName) {
    validationErrors.value.lastName = 'Last name is required'
  } else if (formData.lastName.length > 100) {
    validationErrors.value.lastName = 'Last name must not exceed 100 characters'
  }

  // Phone validation (optional)
  if (formData.phone && formData.phone.length > 20) {
    validationErrors.value.phone = 'Phone number must not exceed 20 characters'
  }

  return Object.keys(validationErrors.value).length === 0
}

const handleRegister = async () => {
  if (!validate()) return

  const result = await register({
    ...formData,
    phone: formData.phone || null // Convert empty string to null
  })

  if (result) {
    successMessage.value = 'Registration successful! Redirecting to login...'

    // Redirect to login page after 2 seconds
    setTimeout(() => {
      router.push('/login')
    }, 2000)
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-4">
    <Card class="w-full max-w-md">
      <template #title>
        <h1 class="text-2xl font-bold text-gray-900">Register for ABUVI</h1>
      </template>

      <template #content>
        <!-- Success message -->
        <Message v-if="successMessage" severity="success" :closable="false" class="mb-4">
          {{ successMessage }}
        </Message>

        <!-- Error message -->
        <Message v-if="error" severity="error" :closable="false" class="mb-4">
          {{ error }}
        </Message>

        <form @submit.prevent="handleRegister" class="flex flex-col gap-4">
          <!-- Email field -->
          <div>
            <label for="email" class="mb-2 block text-sm font-medium text-gray-700">
              Email *
            </label>
            <InputText
              id="email"
              v-model="formData.email"
              type="email"
              placeholder="your.email@example.com"
              class="w-full"
              :invalid="!!validationErrors.email"
              :disabled="loading"
            />
            <small v-if="validationErrors.email" class="text-red-500">
              {{ validationErrors.email }}
            </small>
          </div>

          <!-- Password field -->
          <div>
            <label for="password" class="mb-2 block text-sm font-medium text-gray-700">
              Password *
            </label>
            <InputText
              id="password"
              v-model="formData.password"
              type="password"
              placeholder="Min 8 chars, uppercase, lowercase, number"
              class="w-full"
              :invalid="!!validationErrors.password"
              :disabled="loading"
            />
            <small v-if="validationErrors.password" class="text-red-500">
              {{ validationErrors.password }}
            </small>
          </div>

          <!-- First name field -->
          <div>
            <label for="firstName" class="mb-2 block text-sm font-medium text-gray-700">
              First Name *
            </label>
            <InputText
              id="firstName"
              v-model="formData.firstName"
              placeholder="John"
              class="w-full"
              :invalid="!!validationErrors.firstName"
              :disabled="loading"
            />
            <small v-if="validationErrors.firstName" class="text-red-500">
              {{ validationErrors.firstName }}
            </small>
          </div>

          <!-- Last name field -->
          <div>
            <label for="lastName" class="mb-2 block text-sm font-medium text-gray-700">
              Last Name *
            </label>
            <InputText
              id="lastName"
              v-model="formData.lastName"
              placeholder="Doe"
              class="w-full"
              :invalid="!!validationErrors.lastName"
              :disabled="loading"
            />
            <small v-if="validationErrors.lastName" class="text-red-500">
              {{ validationErrors.lastName }}
            </small>
          </div>

          <!-- Phone field (optional) -->
          <div>
            <label for="phone" class="mb-2 block text-sm font-medium text-gray-700">
              Phone (optional)
            </label>
            <InputText
              id="phone"
              v-model="formData.phone"
              placeholder="+34 123 456 789"
              class="w-full"
              :invalid="!!validationErrors.phone"
              :disabled="loading"
            />
            <small v-if="validationErrors.phone" class="text-red-500">
              {{ validationErrors.phone }}
            </small>
          </div>

          <!-- Submit button -->
          <Button
            type="submit"
            label="Register"
            :loading="loading"
            :disabled="loading || !!successMessage"
            class="w-full"
          />
        </form>

        <!-- Login link -->
        <div class="mt-4 text-center">
          <p class="text-sm text-gray-600">
            Already have an account?
            <router-link to="/login" class="font-medium text-primary-600 hover:text-primary-500">
              Login here
            </router-link>
          </p>
        </div>
      </template>
    </Card>
  </div>
</template>
```

---

### Step 7: Update Router with Auth Routes and Guards

**File**: `frontend/src/router/index.ts`

**Action**: Add login/register routes and implement route guards for authentication

**Implementation Steps**:

1. Open `frontend/src/router/index.ts`
2. Import `useAuthStore` from `@/stores/auth`
3. Add `/login` route (public, lazy-loaded)
4. Add `/register` route (public, lazy-loaded)
5. Add `meta: { requiresAuth: true }` to protected routes (`/users`, `/users/:id`)
6. Add `meta: { requiresAdmin: true }` to admin-only routes (if any)
7. Implement `router.beforeEach()` navigation guard:
   - Check if route requires auth (`to.meta.requiresAuth`)
   - If not authenticated: Redirect to `/login?redirect={to.fullPath}`
   - Check if route requires admin (`to.meta.requiresAdmin`)
   - If not admin: Redirect to `/` (home page)
   - Allow navigation if authorized

**Dependencies**:

- `@/stores/auth` (created in Step 2)
- Vue Router (already installed)

**Implementation Notes**:

- **Public routes**: `/`, `/login`, `/register` (no auth required)
- **Protected routes**: `/users/*` (requires authentication)
- **Admin routes**: `/users` (list all users - Admin only)
- **Redirect after login**: Use `query.redirect` to return to intended page
- **Route guard order**: Check `requiresAuth` first, then `requiresAdmin`

**Full Implementation**:

```typescript
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import HomePage from '@/pages/HomePage.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomePage
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/pages/LoginPage.vue'),
      meta: {
        title: 'Login'
      }
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/pages/RegisterPage.vue'),
      meta: {
        title: 'Register'
      }
    },
    {
      path: '/users',
      name: 'users',
      component: () => import('@/pages/UsersPage.vue'),
      meta: {
        title: 'User Management',
        requiresAuth: true,
        requiresAdmin: true // Admin only endpoint per backend
      }
    },
    {
      path: '/users/:id',
      name: 'user-detail',
      component: () => import('@/pages/UserDetailPage.vue'),
      meta: {
        title: 'User Details',
        requiresAuth: true // Authenticated users can view user details
      }
    }
  ]
})

// Route guard for authentication and authorization
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()

  // Check if route requires authentication
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    // Redirect to login with return URL
    next({
      path: '/login',
      query: { redirect: to.fullPath }
    })
    return
  }

  // Check if route requires admin role
  if (to.meta.requiresAdmin && !authStore.isAdmin) {
    // Redirect to home page (or show 403 page)
    next({ path: '/' })
    return
  }

  // Allow navigation
  next()
})

export default router
```

---

### Step 8: Update App Component with Auth State

**File**: `frontend/src/App.vue`

**Action**: Add navigation header with login/logout functionality and display user info

**Implementation Steps**:

1. Open `frontend/src/App.vue`
2. Import `useAuthStore` from `@/stores/auth`
3. Import `useAuth` composable for logout function
4. Add navigation header with:
   - App logo/title
   - Navigation links (Home, Users - only if authenticated)
   - User info display (if authenticated)
   - Login/Logout button
5. Call `authStore.restoreSession()` on component mount
6. Add router-view for page content

**Dependencies**:

- `@/stores/auth`
- `@/composables/useAuth`
- PrimeVue: `Button`, `Avatar`

**Implementation Notes**:

- **Session restoration**: Call `authStore.restoreSession()` in `onMounted()` to restore session from localStorage
- **Conditional navigation**: Show "Users" link only if authenticated
- **User display**: Show user name and role if logged in
- **Logout button**: Call `useAuth().logout()` which clears store and redirects
- Use **PrimeVue Menubar or custom nav** with Tailwind CSS
- Responsive design: Mobile-friendly navigation

**Full Implementation**:

```vue
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
```

---

### Step 9: Write Vitest Unit Tests for Auth Composable

**File**: `frontend/src/composables/__tests__/useAuth.test.ts`

**Action**: Write comprehensive unit tests for the auth composable

**Implementation Steps**:

1. Create `frontend/src/composables/__tests__/useAuth.test.ts`
2. Import necessary testing utilities (describe, it, expect, vi, beforeEach)
3. Mock `@/utils/api` module
4. Mock `@/stores/auth` module
5. Mock `vue-router` module
6. Write test cases:
   - **Login success**: Should call API, update store, return true
   - **Login failure (401)**: Should set error message, return false
   - **Register success**: Should call API, return UserInfo
   - **Register failure (400 email exists)**: Should set error message, return null
   - **Logout**: Should clear auth store and redirect to login
7. Verify loading states toggle correctly

**Dependencies**:

- Vitest (already installed)
- Mocking utilities: `vi.mock()`

**Implementation Notes**:

- **Mocking**: Mock axios api, auth store, and router
- **AAA pattern**: Arrange-Act-Assert in all tests
- **Test coverage**: Happy path + error cases + edge cases
- Run tests with `npx vitest` or `npx vitest --coverage`

**Full Implementation**:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useAuth } from '@/composables/useAuth'
import { api } from '@/utils/api'
import { useAuthStore } from '@/stores/auth'

// Mock dependencies
vi.mock('@/utils/api')
vi.mock('@/stores/auth')
vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: vi.fn()
  })
}))

describe('useAuth', () => {
  const mockAuthStore = {
    setAuth: vi.fn(),
    clearAuth: vi.fn()
  }

  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useAuthStore).mockReturnValue(mockAuthStore as any)
  })

  describe('login', () => {
    it('should login successfully and return true', async () => {
      // Arrange
      const mockResponse = {
        data: {
          success: true,
          data: {
            token: 'test-token',
            user: {
              id: '1',
              email: 'test@example.com',
              firstName: 'Test',
              lastName: 'User',
              role: 'Member'
            }
          },
          error: null
        }
      }
      vi.mocked(api.post).mockResolvedValue(mockResponse)

      // Act
      const { login, loading, error } = useAuth()
      const result = await login({ email: 'test@example.com', password: 'Password123!' })

      // Assert
      expect(result).toBe(true)
      expect(mockAuthStore.setAuth).toHaveBeenCalledWith(mockResponse.data.data)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
    })

    it('should handle login failure with 401 status', async () => {
      // Arrange
      const mockError = {
        response: { status: 401 }
      }
      vi.mocked(api.post).mockRejectedValue(mockError)

      // Act
      const { login, error } = useAuth()
      const result = await login({ email: 'test@example.com', password: 'wrong' })

      // Assert
      expect(result).toBe(false)
      expect(error.value).toBe('Invalid email or password')
      expect(mockAuthStore.setAuth).not.toHaveBeenCalled()
    })
  })

  describe('register', () => {
    it('should register successfully and return user info', async () => {
      // Arrange
      const mockUserInfo = {
        id: '1',
        email: 'newuser@example.com',
        firstName: 'New',
        lastName: 'User',
        role: 'Member'
      }
      const mockResponse = {
        data: {
          success: true,
          data: mockUserInfo,
          error: null
        }
      }
      vi.mocked(api.post).mockResolvedValue(mockResponse)

      // Act
      const { register, loading, error } = useAuth()
      const result = await register({
        email: 'newuser@example.com',
        password: 'Password123!',
        firstName: 'New',
        lastName: 'User',
        phone: null
      })

      // Assert
      expect(result).toEqual(mockUserInfo)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
    })

    it('should handle registration failure with duplicate email', async () => {
      // Arrange
      const mockError = {
        response: {
          status: 400,
          data: {
            error: { message: 'Email already registered' }
          }
        }
      }
      vi.mocked(api.post).mockRejectedValue(mockError)

      // Act
      const { register, error } = useAuth()
      const result = await register({
        email: 'existing@example.com',
        password: 'Password123!',
        firstName: 'Test',
        lastName: 'User',
        phone: null
      })

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('Email already registered')
    })
  })

  describe('logout', () => {
    it('should clear auth store and redirect to login', () => {
      // Act
      const { logout } = useAuth()
      logout()

      // Assert
      expect(mockAuthStore.clearAuth).toHaveBeenCalled()
    })
  })
})
```

---

### Step 10: Write Vitest Unit Tests for Auth Store

**File**: `frontend/src/stores/__tests__/auth.test.ts`

**Action**: Write comprehensive unit tests for the Pinia auth store

**Implementation Steps**:

1. Create `frontend/src/stores/__tests__/auth.test.ts`
2. Setup Pinia test environment with `setActivePinia(createPinia())`
3. Write test cases:
   - **setAuth**: Should store user and token, save to localStorage
   - **clearAuth**: Should clear user and token, remove from localStorage
   - **restoreSession**: Should load token from localStorage
   - **isAuthenticated**: Should return true when token exists
   - **isAdmin**: Should return true for Admin role
   - **isBoard**: Should return true for Admin or Board role
4. Mock localStorage for testing

**Full Implementation**:

```typescript
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import type { UserInfo } from '@/types/auth'

describe('useAuthStore', () => {
  let localStorageMock: { [key: string]: string } = {}

  beforeEach(() => {
    setActivePinia(createPinia())

    // Mock localStorage
    localStorageMock = {}
    global.localStorage = {
      getItem: vi.fn((key: string) => localStorageMock[key] || null),
      setItem: vi.fn((key: string, value: string) => {
        localStorageMock[key] = value
      }),
      removeItem: vi.fn((key: string) => {
        delete localStorageMock[key]
      }),
      clear: vi.fn(() => {
        localStorageMock = {}
      })
    } as any
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('should initialize with null user and token', () => {
    const authStore = useAuthStore()

    expect(authStore.user).toBeNull()
    expect(authStore.token).toBeNull()
    expect(authStore.isAuthenticated).toBe(false)
  })

  it('should set auth data and save token to localStorage', () => {
    const authStore = useAuthStore()
    const mockAuthData = {
      token: 'test-token',
      user: {
        id: '1',
        email: 'test@example.com',
        firstName: 'Test',
        lastName: 'User',
        role: 'Member'
      } as UserInfo
    }

    authStore.setAuth(mockAuthData)

    expect(authStore.user).toEqual(mockAuthData.user)
    expect(authStore.token).toBe('test-token')
    expect(authStore.isAuthenticated).toBe(true)
    expect(localStorage.setItem).toHaveBeenCalledWith('authToken', 'test-token')
  })

  it('should clear auth data and remove token from localStorage', () => {
    const authStore = useAuthStore()

    // Set auth first
    authStore.setAuth({
      token: 'test-token',
      user: { id: '1', email: 'test@example.com', firstName: 'Test', lastName: 'User', role: 'Member' }
    })

    // Clear auth
    authStore.clearAuth()

    expect(authStore.user).toBeNull()
    expect(authStore.token).toBeNull()
    expect(authStore.isAuthenticated).toBe(false)
    expect(localStorage.removeItem).toHaveBeenCalledWith('authToken')
  })

  it('should restore session from localStorage', () => {
    localStorageMock['authToken'] = 'saved-token'

    const authStore = useAuthStore()
    authStore.restoreSession()

    expect(authStore.token).toBe('saved-token')
  })

  it('should correctly identify admin role', () => {
    const authStore = useAuthStore()

    authStore.setAuth({
      token: 'test-token',
      user: { id: '1', email: 'admin@example.com', firstName: 'Admin', lastName: 'User', role: 'Admin' }
    })

    expect(authStore.isAdmin).toBe(true)
    expect(authStore.isBoard).toBe(true)
  })

  it('should correctly identify board role', () => {
    const authStore = useAuthStore()

    authStore.setAuth({
      token: 'test-token',
      user: { id: '1', email: 'board@example.com', firstName: 'Board', lastName: 'User', role: 'Board' }
    })

    expect(authStore.isAdmin).toBe(false)
    expect(authStore.isBoard).toBe(true)
  })

  it('should correctly identify member role', () => {
    const authStore = useAuthStore()

    authStore.setAuth({
      token: 'test-token',
      user: { id: '1', email: 'member@example.com', firstName: 'Member', lastName: 'User', role: 'Member' }
    })

    expect(authStore.isAdmin).toBe(false)
    expect(authStore.isBoard).toBe(false)
  })
})
```

---

### Step 11: Write Cypress E2E Tests for Auth Flow

**File**: `frontend/cypress/e2e/auth.cy.ts`

**Action**: Write end-to-end tests for login, registration, and protected route access

**Implementation Steps**:

1. Create `frontend/cypress/e2e/auth.cy.ts`
2. Write test cases:
   - **Registration flow**: Complete registration form and submit
   - **Login flow**: Login with valid credentials
   - **Invalid login**: Attempt login with invalid credentials (should show error)
   - **Protected route access without auth**: Should redirect to login
   - **Protected route access with auth**: Should allow access
   - **Logout flow**: Logout and verify redirect to login
   - **Admin access**: Member user attempts to access admin route (should be forbidden)

**Dependencies**:

- Cypress (already installed)
- Backend API running for integration tests

**Implementation Notes**:

- **Test data**: Use unique email addresses for each test to avoid conflicts
- **Backend dependency**: These tests require the backend API to be running
- **Cleanup**: Clear localStorage before each test
- Use `cy.intercept()` to mock API responses if needed
- Use `data-testid` attributes for reliable element selection

**Full Implementation**:

```typescript
describe('Authentication Flow', () => {
  beforeEach(() => {
    // Clear localStorage before each test
    cy.clearLocalStorage()
    cy.visit('/')
  })

  describe('Registration', () => {
    it('should register a new user successfully', () => {
      const uniqueEmail = `testuser-${Date.now()}@example.com`

      cy.visit('/register')

      // Fill out registration form
      cy.get('#email').type(uniqueEmail)
      cy.get('#password').type('TestPassword123!')
      cy.get('#firstName').type('Test')
      cy.get('#lastName').type('User')

      // Submit form
      cy.get('button[type="submit"]').click()

      // Should show success message
      cy.contains('Registration successful').should('be.visible')

      // Should redirect to login page
      cy.url().should('include', '/login')
    })

    it('should show error for duplicate email', () => {
      const existingEmail = 'existing@example.com'

      // Register first user
      cy.visit('/register')
      cy.get('#email').type(existingEmail)
      cy.get('#password').type('TestPassword123!')
      cy.get('#firstName').type('Test')
      cy.get('#lastName').type('User')
      cy.get('button[type="submit"]').click()
      cy.wait(1000)

      // Try to register again with same email
      cy.visit('/register')
      cy.get('#email').type(existingEmail)
      cy.get('#password').type('TestPassword123!')
      cy.get('#firstName').type('Test')
      cy.get('#lastName').type('User')
      cy.get('button[type="submit"]').click()

      // Should show error message
      cy.contains('Email already registered').should('be.visible')
    })

    it('should validate password requirements', () => {
      cy.visit('/register')

      cy.get('#email').type('test@example.com')
      cy.get('#password').type('weak')
      cy.get('#firstName').type('Test')
      cy.get('#lastName').type('User')
      cy.get('button[type="submit"]').click()

      // Should show validation error
      cy.contains('Password must be at least 8 characters').should('be.visible')
    })
  })

  describe('Login', () => {
    const testEmail = 'logintest@example.com'
    const testPassword = 'TestPassword123!'

    beforeEach(() => {
      // Register a test user first
      cy.visit('/register')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('Login')
      cy.get('#lastName').type('Test')
      cy.get('button[type="submit"]').click()
      cy.wait(2000) // Wait for redirect
    })

    it('should login successfully with valid credentials', () => {
      cy.visit('/login')

      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('button[type="submit"]').click()

      // Should redirect to /users or intended page
      cy.url().should('not.include', '/login')

      // Should display user info in header
      cy.contains('Login').should('be.visible')
      cy.contains('Test').should('be.visible')
    })

    it('should show error for invalid credentials', () => {
      cy.visit('/login')

      cy.get('#email').type('test@example.com')
      cy.get('#password').type('WrongPassword!')
      cy.get('button[type="submit"]').click()

      // Should show error message
      cy.contains('Invalid email or password').should('be.visible')
    })
  })

  describe('Protected Routes', () => {
    it('should redirect to login when accessing protected route without auth', () => {
      cy.visit('/users')

      // Should redirect to login page
      cy.url().should('include', '/login')
      cy.url().should('include', 'redirect=%2Fusers')
    })

    it('should allow access to protected route with valid auth', () => {
      // Login first
      const testEmail = 'protectedtest@example.com'
      const testPassword = 'TestPassword123!'

      // Register user
      cy.visit('/register')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('Protected')
      cy.get('#lastName').type('Test')
      cy.get('button[type="submit"]').click()
      cy.wait(2000)

      // Login
      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('button[type="submit"]').click()

      // Access protected route
      cy.visit('/users')

      // Should NOT redirect to login
      cy.url().should('not.include', '/login')
    })
  })

  describe('Logout', () => {
    it('should logout successfully and redirect to login', () => {
      // Login first
      const testEmail = 'logouttest@example.com'
      const testPassword = 'TestPassword123!'

      // Register and login
      cy.visit('/register')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('Logout')
      cy.get('#lastName').type('Test')
      cy.get('button[type="submit"]').click()
      cy.wait(2000)

      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('button[type="submit"]').click()
      cy.wait(1000)

      // Click logout button
      cy.contains('Logout').click()

      // Should redirect to login page
      cy.url().should('include', '/login')

      // localStorage should be cleared
      cy.window().then((win) => {
        expect(win.localStorage.getItem('authToken')).to.be.null
      })
    })
  })

  describe('Role-based Access', () => {
    it('should prevent Member from accessing admin-only routes', () => {
      // Register and login as Member
      const testEmail = 'member@example.com'
      const testPassword = 'TestPassword123!'

      cy.visit('/register')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('Member')
      cy.get('#lastName').type('User')
      cy.get('button[type="submit"]').click()
      cy.wait(2000)

      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('button[type="submit"]').click()
      cy.wait(1000)

      // Try to access admin route
      cy.visit('/users')

      // Should redirect to home page (403 behavior)
      cy.url().should('eq', Cypress.config().baseUrl + '/')
    })
  })
})
```

---

### Step 12: Update Technical Documentation

**Action**: Review and update technical documentation according to changes made

**Implementation Steps**:

1. **Review Changes**: Analyze all code changes made during implementation
2. **Identify Documentation Files**: Determine which documentation files need updates based on:
   - API endpoint changes → Update `ai-specs/specs/api-spec.yml` (if exists)
   - UI/UX patterns or component patterns → Update `ai-specs/specs/frontend-standards.mdc`
   - Routing changes → Update `ai-specs/specs/frontend-standards.mdc` (add auth routes section)
   - New dependencies → Update `ai-specs/specs/frontend-standards.mdc` (if auth patterns added)
   - Test patterns → Update testing documentation if new patterns introduced
3. **Update Documentation**: For each affected file:
   - Update content in English (as per `documentation-standards.mdc`)
   - Maintain consistency with existing documentation structure
   - Ensure proper formatting
4. **Verify Documentation**:
   - Confirm all changes are accurately reflected
   - Check that documentation follows established structure
5. **Report Updates**: Document which files were updated and what changes were made

**Files to Update**:

- `ai-specs/specs/frontend-standards.mdc`:
  - Add section on authentication patterns (auth store, auth composable)
  - Update route guard examples
  - Add localStorage usage for JWT tokens
  - Update example of axios interceptors with JWT
- `README.md` (if exists):
  - Update setup instructions for JWT secret configuration
  - Add authentication flow documentation

**References**:

- Follow process described in `ai-specs/specs/documentation-standards.mdc`
- All documentation must be written in English

**Notes**: This step is MANDATORY before considering the implementation complete. Do not skip documentation updates.

---

## Implementation Order

Execute these steps **in sequence**:

1. **Step 0**: Create Feature Branch (`feature/phase2-authentication-frontend`)
2. **Step 1**: Define TypeScript Auth Interfaces
3. **Step 2**: Create Pinia Auth Store
4. **Step 3**: Create Auth Composable
5. **Step 4**: Update Axios Configuration with Interceptors
6. **Step 5**: Create Login Page Component
7. **Step 6**: Create Registration Page Component
8. **Step 7**: Update Router with Auth Routes and Guards
9. **Step 8**: Update App Component with Auth State
10. **Step 9**: Write Vitest Unit Tests for Auth Composable
11. **Step 10**: Write Vitest Unit Tests for Auth Store
12. **Step 11**: Write Cypress E2E Tests for Auth Flow
13. **Step 12**: Update Technical Documentation

---

## Testing Checklist

After implementation, verify the following:

### Functionality Testing

- [ ] **Registration**:
  - [ ] Can register new user with valid data
  - [ ] Shows error for duplicate email
  - [ ] Validates password strength requirements
  - [ ] Validates required fields (email, password, firstName, lastName)
  - [ ] Validates email format
  - [ ] Redirects to login after successful registration

- [ ] **Login**:
  - [ ] Can login with valid credentials
  - [ ] Shows error for invalid email
  - [ ] Shows error for invalid password
  - [ ] Redirects to intended page after login (or `/users` by default)
  - [ ] Stores JWT token in localStorage
  - [ ] Updates auth store with user info

- [ ] **Logout**:
  - [ ] Clears auth store
  - [ ] Removes token from localStorage
  - [ ] Redirects to login page

- [ ] **Session Restoration**:
  - [ ] Restores session from localStorage on app load
  - [ ] Maintains authentication across page refreshes

- [ ] **Route Guards**:
  - [ ] Redirects to login when accessing protected route without auth
  - [ ] Allows access to protected routes with valid auth
  - [ ] Redirects to home when non-admin accesses admin route
  - [ ] Preserves intended destination in redirect query param

- [ ] **API Integration**:
  - [ ] JWT token automatically attached to all API requests
  - [ ] 401 responses trigger logout and redirect to login
  - [ ] Error messages displayed user-friendly

### Unit Test Coverage

- [ ] Auth composable tests:
  - [ ] Login success
  - [ ] Login failure (invalid credentials)
  - [ ] Register success
  - [ ] Register failure (duplicate email)
  - [ ] Logout

- [ ] Auth store tests:
  - [ ] setAuth stores data and saves to localStorage
  - [ ] clearAuth clears data and removes from localStorage
  - [ ] restoreSession loads token from localStorage
  - [ ] Computed getters (isAuthenticated, isAdmin, isBoard) work correctly

- [ ] Test coverage >= 90% for auth feature

### E2E Test Coverage

- [ ] Registration flow end-to-end
- [ ] Login flow end-to-end
- [ ] Logout flow end-to-end
- [ ] Protected route access without auth
- [ ] Protected route access with auth
- [ ] Role-based route access (admin vs member)
- [ ] Session persistence across page refreshes

### Code Quality

- [ ] All files use TypeScript strict mode
- [ ] No `any` types used
- [ ] All components use `<script setup lang="ts">`
- [ ] PrimeVue components used consistently
- [ ] Tailwind CSS only (no custom `<style>` blocks)
- [ ] Error handling implemented for all async operations
- [ ] Loading states displayed during API calls
- [ ] User-friendly error messages

---

## Error Handling Patterns

### Composable Error Handling

The `useAuth` composable manages errors at the API layer:

```typescript
// Error handling in useAuth composable
const error = ref<string | null>(null)

try {
  const response = await api.post('/auth/login', credentials)
  if (response.data.success) {
    // Success path
  } else {
    error.value = response.data.error?.message || 'Operation failed'
  }
} catch (err: any) {
  if (err.response?.status === 401) {
    error.value = 'Invalid email or password'
  } else if (err.response?.status === 400) {
    error.value = err.response.data.error?.message || 'Invalid request'
  } else {
    error.value = 'Network error. Please try again.'
  }
}
```

### Component Error Display

Components display errors using PrimeVue Message component:

```vue
<Message v-if="error" severity="error" :closable="false" class="mb-4">
  {{ error }}
</Message>
```

### Global 401 Handling

The axios response interceptor handles 401 globally:

```typescript
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const authStore = useAuthStore()
      authStore.clearAuth()
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)
```

---

## UI/UX Considerations

### PrimeVue Component Usage

- **Card**: Layout wrapper for login/register pages
- **InputText**: Form input fields
- **Button**: Submit buttons with loading state
- **Message**: Error/success feedback
- **ProgressSpinner**: Loading indicators (if needed)

### Tailwind CSS Styling

- **Responsive design**: Mobile-first with `sm:`, `md:`, `lg:` breakpoints
- **Form layout**: `flex flex-col gap-4` for vertical spacing
- **Container width**: `max-w-md` for forms, `max-w-7xl` for app layout
- **Colors**: Use PrimeVue theme colors (`text-primary-600`, `bg-primary-50`)

### Accessibility

- **Labels**: All form fields have associated `<label>` elements
- **ARIA attributes**: Use `aria-label` where text labels are not visible
- **Keyboard navigation**: Ensure all interactive elements are keyboard accessible
- **Error announcements**: Error messages should be associated with form fields

### Loading States

- **Button loading**: Use PrimeVue Button `loading` prop
- **Form disabled**: Disable form fields during submission
- **Spinner**: Display ProgressSpinner for page-level loading

### User Feedback

- **Success messages**: Green Message component for successful actions
- **Error messages**: Red Message component for failures
- **Validation errors**: Display inline below form fields in red text
- **Toast notifications**: Consider using PrimeVue Toast for transient messages (optional)

---

## Dependencies

### NPM Packages Required

All dependencies are already installed:

- `vue` (3.x)
- `vue-router` (4.x)
- `pinia` (2.x)
- `axios`
- `primevue` (4.x)
- `@primeuix/themes`
- `primeicons`
- `tailwindcss`
- `vitest`
- `@vue/test-utils`
- `cypress`

No additional packages required for this implementation.

### PrimeVue Components Used

- `InputText` - Form input fields
- `Button` - Submit and action buttons
- `Message` - Error/success messages
- `Card` - Layout container
- `ProgressSpinner` - Loading indicators (optional)

---

## Notes

### Important Reminders

1. **Backend Dependency**: This frontend implementation depends on the Phase 2 backend being fully deployed and accessible at `VITE_API_URL`

2. **Environment Variables**:
   - `VITE_API_URL` must be set to backend API URL (e.g., `http://localhost:5079/api`)
   - Create `.env.development` and `.env.production` files if they don't exist

3. **Security**:
   - JWT tokens stored in localStorage (consider httpOnly cookies for production)
   - No sensitive data stored in store (only token)
   - HTTPS required in production

4. **Language**: All code, comments, and UI text in English

5. **TypeScript Strict Mode**: No `any` types allowed

6. **Testing**: Aim for >= 90% test coverage

7. **Code Review**: Review all code against `frontend-standards.mdc` before submitting

### Business Rules

- New registrations default to **Member** role (per backend)
- Only **Admin** users can access `/users` list endpoint
- **Authenticated** users can view individual user details (`/users/:id`)
- Registration does NOT auto-login (requires explicit login for security)
- Token expiry handled by backend (24 hours default)

### Known Limitations

- Session restoration only loads token; user info not decoded from JWT (fetched on first API call)
- No refresh token mechanism (future enhancement)
- No "remember me" option (future enhancement)
- No password reset functionality (future phase)

---

## Next Steps After Implementation

1. **Manual Testing**:
   - Test all auth flows manually in browser
   - Test on multiple screen sizes (mobile, tablet, desktop)
   - Test error scenarios (network failures, invalid data)

2. **Integration Testing**:
   - Run full Cypress E2E test suite
   - Verify all unit tests pass with coverage >= 90%

3. **Code Review**:
   - Self-review against `frontend-standards.mdc`
   - Request peer review if applicable

4. **Documentation**:
   - Update technical documentation (Step 12)
   - Document any deviations from plan

5. **Deployment Preparation**:
   - Ensure `.env.production` configured correctly
   - Test production build: `npm run build && npm run preview`

6. **Merge to Main**:
   - Squash commits if needed
   - Write clear merge commit message
   - Delete feature branch after merge

7. **Phase 3 Preparation**:
   - Review Phase 3 requirements (if any)
   - Identify remaining frontend features

---

## Implementation Verification

Before considering this implementation complete, verify:

### Code Quality

- [ ] TypeScript strict mode enabled, no `any` types
- [ ] All components use `<script setup lang="ts">`
- [ ] PrimeVue components used consistently
- [ ] Tailwind CSS only (no custom `<style>` blocks)
- [ ] No console errors or warnings
- [ ] Code follows established patterns from `frontend-standards.mdc`

### Functionality

- [ ] Registration flow works end-to-end
- [ ] Login flow works end-to-end
- [ ] Logout works correctly
- [ ] Protected routes redirect to login when not authenticated
- [ ] Admin routes redirect non-admin users
- [ ] Session persists across page refreshes
- [ ] JWT token attached to API requests automatically
- [ ] 401 responses trigger logout

### Testing

- [ ] All Vitest unit tests pass
- [ ] All Cypress E2E tests pass
- [ ] Test coverage >= 90%
- [ ] No flaky tests

### Integration

- [ ] Frontend successfully communicates with backend API
- [ ] All API endpoints work as expected (`/auth/login`, `/auth/register`)
- [ ] Error responses handled gracefully
- [ ] Loading states displayed correctly

### Documentation

- [ ] Technical documentation updated
- [ ] Code comments added where necessary
- [ ] README updated with auth setup instructions

### User Experience

- [ ] Forms validate correctly
- [ ] Error messages are user-friendly
- [ ] Loading states provide feedback
- [ ] Responsive design works on mobile/tablet/desktop
- [ ] Accessibility requirements met (keyboard navigation, ARIA labels)

---

## Summary

This frontend implementation plan provides a comprehensive, step-by-step guide to integrating JWT-based authentication into the ABUVI Vue 3 application. Following the project's established architectural patterns using Composition API, composables, Pinia stores, PrimeVue, and Tailwind CSS, this plan ensures a maintainable, testable, and user-friendly authentication system.

**Key Deliverables:**

- Auth store for session management
- Auth composable for API communication
- Login and registration pages
- Route guards for protected routes
- Axios interceptors for JWT tokens
- Comprehensive test coverage (Vitest + Cypress)
- Updated technical documentation

After completing all steps in sequence and verifying the implementation checklist, the frontend authentication layer will be fully functional and ready for integration with backend Phase 2.

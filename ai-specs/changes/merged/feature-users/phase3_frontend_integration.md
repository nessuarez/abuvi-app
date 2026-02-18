# Phase 3: Frontend Integration (Vue + Authentication)

## Goal

Connect the Vue 3 frontend to the authenticated backend, implementing login UI, auth state management, token persistence, route guards, and E2E testing.

## Prerequisites

- **Phase 1 & 2 completed**: Backend auth fully functional
- Backend running at <http://localhost:5079>
- Test users created for frontend testing
- JWT authentication tested with Postman/curl

## Why Last?

- Backend is fully functional and testable first
- UI can be developed against a working API
- Clear separation of concerns
- Can test auth flows end-to-end

## What We're Building

1. Pinia auth store (user state, token management)
2. TypeScript types for auth
3. Login page component (PrimeVue + Tailwind)
4. Axios interceptors (inject JWT, handle 401)
5. Vue Router guards (protect routes)
6. Logout functionality
7. User profile display
8. Cypress E2E tests

## Architecture

```
┌─────────────┐
│ LoginPage   │──> authStore.login(email, pass)
└──────┬──────┘
       │
       v
┌─────────────┐
│ Auth Store  │──> API call to /api/auth/login
│ (Pinia)     │──> Store token in localStorage
│             │──> Store user in state
└──────┬──────┘
       │
       v
┌─────────────┐
│ API Client  │──> Request interceptor adds JWT token
│ (Axios)     │──> Response interceptor handles 401
└──────┬──────┘
       │
       v
┌─────────────┐
│ Router      │──> beforeEach guard checks auth
│ Guards      │──> Redirect to /login if not authed
└─────────────┘
```

## Files to Create

### 1. Auth Types

**Path**: `frontend/src/types/auth.ts`

```typescript
export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone?: string
}

export interface LoginResponse {
  token: string
  user: User
}

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  role: 'Admin' | 'Board' | 'Member'
}

export interface AuthState {
  user: User | null
  token: string | null
}
```

### 2. Auth Store

**Path**: `frontend/src/stores/auth.ts`

```typescript
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { api } from '@/utils/api'
import type { LoginRequest, LoginResponse, User, AuthState } from '@/types/auth'

const TOKEN_KEY = 'abuvi_auth_token'
const USER_KEY = 'abuvi_user'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<User | null>(null)
  const token = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const userRole = computed(() => user.value?.role ?? null)
  const isAdmin = computed(() => userRole.value === 'Admin')
  const isBoard = computed(() => userRole.value === 'Board' || userRole.value === 'Admin')

  // Actions
  async function login(email: string, password: string): Promise<void> {
    try {
      const response = await api.post<{ success: boolean; data: LoginResponse }>(
        '/auth/login',
        { email, password }
      )

      if (!response.data.success) {
        throw new Error('Login failed')
      }

      const { token: authToken, user: userData } = response.data.data

      // Store in state
      token.value = authToken
      user.value = userData

      // Persist to localStorage
      localStorage.setItem(TOKEN_KEY, authToken)
      localStorage.setItem(USER_KEY, JSON.stringify(userData))
    } catch (error: any) {
      console.error('Login error:', error)
      throw new Error(error.response?.data?.error?.message || 'Invalid credentials')
    }
  }

  function logout(): void {
    // Clear state
    user.value = null
    token.value = null

    // Clear localStorage
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  }

  function initialize(): void {
    // Restore auth state from localStorage on app load
    const storedToken = localStorage.getItem(TOKEN_KEY)
    const storedUser = localStorage.getItem(USER_KEY)

    if (storedToken && storedUser) {
      token.value = storedToken
      try {
        user.value = JSON.parse(storedUser)
      } catch (error) {
        console.error('Failed to parse stored user', error)
        logout()
      }
    }
  }

  return {
    // State
    user,
    token,
    // Getters
    isAuthenticated,
    userRole,
    isAdmin,
    isBoard,
    // Actions
    login,
    logout,
    initialize
  }
})
```

**Features**:

- Token and user persistence in localStorage
- Computed properties for role checks
- Error handling
- Initialize from localStorage on app mount

### 3. Login Page

**Path**: `frontend/src/pages/LoginPage.vue`

```vue
<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'

const router = useRouter()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const loading = ref(false)

async function handleLogin() {
  error.value = null
  loading.value = true

  try {
    await authStore.login(email.value, password.value)
    router.push('/')
  } catch (err: any) {
    error.value = err.message || 'Login failed. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 px-4">
    <div class="w-full max-w-md rounded-lg bg-white p-8 shadow-md">
      <h1 class="mb-6 text-center text-3xl font-bold text-gray-900">
        ABUVI Login
      </h1>

      <Message v-if="error" severity="error" :closable="false" class="mb-4">
        {{ error }}
      </Message>

      <form @submit.prevent="handleLogin" class="space-y-4">
        <div>
          <label for="email" class="mb-2 block text-sm font-medium text-gray-700">
            Email
          </label>
          <InputText
            id="email"
            v-model="email"
            type="email"
            placeholder="your@email.com"
            required
            class="w-full"
            :disabled="loading"
          />
        </div>

        <div>
          <label for="password" class="mb-2 block text-sm font-medium text-gray-700">
            Password
          </label>
          <InputText
            id="password"
            v-model="password"
            type="password"
            placeholder="••••••••"
            required
            class="w-full"
            :disabled="loading"
          />
        </div>

        <Button
          type="submit"
          label="Login"
          class="w-full"
          :loading="loading"
          :disabled="loading"
        />
      </form>
    </div>
  </div>
</template>
```

**Features**:

- PrimeVue components (InputText, Button, Message)
- Tailwind CSS styling
- Loading state
- Error display
- Form validation
- Redirect to home on success

### 4. Main Layout with User Info

**Path**: `frontend/src/layouts/MainLayout.vue`

```vue
<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import Button from 'primevue/button'

const router = useRouter()
const authStore = useAuthStore()

function handleLogout() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <div class="min-h-screen bg-gray-50">
    <!-- Header -->
    <header class="bg-white shadow">
      <div class="mx-auto flex max-w-7xl items-center justify-between px-4 py-4">
        <h1 class="text-2xl font-bold text-gray-900">ABUVI</h1>

        <div v-if="authStore.isAuthenticated" class="flex items-center gap-4">
          <span class="text-sm text-gray-700">
            {{ authStore.user?.firstName }} {{ authStore.user?.lastName }}
            <span class="text-xs text-gray-500">({{ authStore.user?.role }})</span>
          </span>
          <Button
            label="Logout"
            icon="pi pi-sign-out"
            severity="secondary"
            size="small"
            @click="handleLogout"
          />
        </div>
      </div>
    </header>

    <!-- Main Content -->
    <main class="mx-auto max-w-7xl px-4 py-8">
      <slot />
    </main>
  </div>
</template>
```

**Features**:

- Header with user info
- Logout button
- Role display
- Slot for page content

### 5. Protected Home Page

**Path**: `frontend/src/pages/HomePage.vue` (Update existing)

```vue
<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
</script>

<template>
  <div>
    <h1 class="mb-4 text-4xl font-bold">Welcome to ABUVI</h1>
    <p class="text-gray-700">
      Hello, {{ authStore.user?.firstName }}! You are logged in as
      <span class="font-semibold">{{ authStore.user?.role }}</span>.
    </p>
  </div>
</template>
```

## Files to Modify

### 1. API Client (Add Interceptors)

**Path**: `frontend/src/utils/api.ts`

```typescript
import axios from 'axios'
import { useAuthStore } from '@/stores/auth'
import router from '@/router'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5079/api'

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Request interceptor - Add JWT token to all requests
api.interceptors.request.use(
  (config) => {
    // Get token from store (don't use useAuthStore() directly here due to timing)
    const token = localStorage.getItem('abuvi_auth_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor - Handle 401 errors (unauthorized)
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Token expired or invalid - logout and redirect to login
      const authStore = useAuthStore()
      authStore.logout()

      // Only redirect if not already on login page
      if (router.currentRoute.value.path !== '/login') {
        router.push('/login')
      }
    }

    console.error('API Error:', error)
    return Promise.reject(error)
  }
)
```

**Changes**:

- Request interceptor adds JWT token from localStorage
- Response interceptor handles 401 (auto-logout and redirect)
- Uses localStorage directly in request interceptor to avoid circular dependency

### 2. Router (Add Guards and Login Route)

**Path**: `frontend/src/router/index.ts`

```typescript
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import HomePage from '@/pages/HomePage.vue'
import LoginPage from '@/pages/LoginPage.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomePage,
      meta: { requiresAuth: true }
    },
    {
      path: '/login',
      name: 'login',
      component: LoginPage,
      meta: { requiresAuth: false }
    }
  ]
})

// Navigation guard
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    // Route requires auth but user not authenticated - redirect to login
    next({ name: 'login', query: { redirect: to.fullPath } })
  } else if (to.name === 'login' && authStore.isAuthenticated) {
    // User already authenticated trying to access login - redirect to home
    next({ name: 'home' })
  } else {
    next()
  }
})

export default router
```

**Features**:

- `requiresAuth` meta field for routes
- beforeEach guard checks authentication
- Redirect to login if not authenticated
- Redirect to home if already authenticated and accessing login
- Preserves intended destination in query param

### 3. Main App Entry (Initialize Auth)

**Path**: `frontend/src/main.ts`

```typescript
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import App from './App.vue'
import router from './router'
import { useAuthStore } from './stores/auth'

import './assets/main.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)
app.use(PrimeVue)

// Initialize auth store from localStorage
const authStore = useAuthStore()
authStore.initialize()

app.mount('#app')
```

**Changes**:

- Initialize auth store after Pinia is registered
- Restores token and user from localStorage on app load

### 4. App.vue (Use Layout)

**Path**: `frontend/src/App.vue`

```vue
<script setup lang="ts">
import { useRoute } from 'vue-router'
import MainLayout from '@/layouts/MainLayout.vue'

const route = useRoute()
</script>

<template>
  <MainLayout v-if="route.meta.requiresAuth">
    <RouterView />
  </MainLayout>
  <RouterView v-else />
</template>
```

**Features**:

- Conditionally show layout only for authenticated routes
- Login page doesn't show header/nav

## Testing

### Unit Tests (Vitest)

#### 1. Auth Store Tests

**Path**: `frontend/src/stores/auth.test.ts`

```typescript
import { setActivePinia, createPinia } from 'pinia'
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useAuthStore } from './auth'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('Auth Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('initializes with no user or token', () => {
    const store = useAuthStore()
    expect(store.user).toBeNull()
    expect(store.token).toBeNull()
    expect(store.isAuthenticated).toBe(false)
  })

  it('login sets user and token', async () => {
    const store = useAuthStore()
    const mockResponse = {
      data: {
        success: true,
        data: {
          token: 'mock-jwt-token',
          user: {
            id: '123',
            email: 'test@example.com',
            firstName: 'Test',
            lastName: 'User',
            role: 'Member'
          }
        }
      }
    }

    vi.mocked(api.post).mockResolvedValue(mockResponse)

    await store.login('test@example.com', 'password')

    expect(store.token).toBe('mock-jwt-token')
    expect(store.user?.email).toBe('test@example.com')
    expect(store.isAuthenticated).toBe(true)
  })

  it('logout clears user and token', () => {
    const store = useAuthStore()
    store.token = 'some-token'
    store.user = {
      id: '123',
      email: 'test@example.com',
      firstName: 'Test',
      lastName: 'User',
      role: 'Member'
    }

    store.logout()

    expect(store.user).toBeNull()
    expect(store.token).toBeNull()
    expect(store.isAuthenticated).toBe(false)
  })

  it('initialize restores auth from localStorage', () => {
    const mockUser = {
      id: '123',
      email: 'test@example.com',
      firstName: 'Test',
      lastName: 'User',
      role: 'Member'
    }
    localStorage.setItem('abuvi_auth_token', 'stored-token')
    localStorage.setItem('abuvi_user', JSON.stringify(mockUser))

    const store = useAuthStore()
    store.initialize()

    expect(store.token).toBe('stored-token')
    expect(store.user).toEqual(mockUser)
    expect(store.isAuthenticated).toBe(true)
  })
})
```

### E2E Tests (Cypress)

#### 2. Auth E2E Tests

**Path**: `frontend/cypress/e2e/auth.cy.ts`

```typescript
describe('Authentication', () => {
  beforeEach(() => {
    // Clear localStorage before each test
    cy.clearLocalStorage()
  })

  it('redirects to login when accessing protected route', () => {
    cy.visit('/')
    cy.url().should('include', '/login')
  })

  it('shows login page', () => {
    cy.visit('/login')
    cy.contains('ABUVI Login').should('be.visible')
    cy.get('input[type="email"]').should('be.visible')
    cy.get('input[type="password"]').should('be.visible')
    cy.contains('button', 'Login').should('be.visible')
  })

  it('shows error with invalid credentials', () => {
    cy.visit('/login')
    cy.get('input[type="email"]').type('invalid@example.com')
    cy.get('input[type="password"]').type('wrongpassword')
    cy.contains('button', 'Login').click()

    cy.contains('Invalid credentials').should('be.visible')
  })

  it('successfully logs in with valid credentials', () => {
    // Create test user first via API
    cy.request('POST', 'http://localhost:5079/api/auth/register', {
      email: 'testuser@example.com',
      password: 'Test123!',
      firstName: 'Test',
      lastName: 'User'
    })

    // Perform login
    cy.visit('/login')
    cy.get('input[type="email"]').type('testuser@example.com')
    cy.get('input[type="password"]').type('Test123!')
    cy.contains('button', 'Login').click()

    // Should redirect to home
    cy.url().should('not.include', '/login')
    cy.contains('Welcome to ABUVI').should('be.visible')
    cy.contains('Test User').should('be.visible')
  })

  it('persists authentication after page reload', () => {
    // Login first
    cy.request('POST', 'http://localhost:5079/api/auth/register', {
      email: 'persist@example.com',
      password: 'Test123!',
      firstName: 'Persist',
      lastName: 'Test'
    })

    cy.visit('/login')
    cy.get('input[type="email"]').type('persist@example.com')
    cy.get('input[type="password"]').type('Test123!')
    cy.contains('button', 'Login').click()

    // Reload page
    cy.reload()

    // Should still be authenticated
    cy.contains('Persist Test').should('be.visible')
    cy.url().should('not.include', '/login')
  })

  it('logs out successfully', () => {
    // Login first
    cy.request('POST', 'http://localhost:5079/api/auth/register', {
      email: 'logout@example.com',
      password: 'Test123!',
      firstName: 'Logout',
      lastName: 'Test'
    })

    cy.visit('/login')
    cy.get('input[type="email"]').type('logout@example.com')
    cy.get('input[type="password"]').type('Test123!')
    cy.contains('button', 'Login').click()

    // Click logout
    cy.contains('button', 'Logout').click()

    // Should redirect to login
    cy.url().should('include', '/login')
    cy.contains('ABUVI Login').should('be.visible')
  })

  it('handles 401 response by logging out', () => {
    // Manually set invalid token
    localStorage.setItem('abuvi_auth_token', 'invalid-token')
    localStorage.setItem('abuvi_user', JSON.stringify({
      id: '123',
      email: 'test@example.com',
      firstName: 'Test',
      lastName: 'User',
      role: 'Member'
    }))

    // Visit home (will trigger API call with invalid token)
    cy.visit('/')

    // Should redirect to login after 401
    cy.url().should('include', '/login')
  })
})
```

## Verification Checklist

After completing Phase 3, verify:

### Authentication Flow

- [ ] Accessing protected route (/) redirects to /login
- [ ] Login page displays correctly with form
- [ ] Login with valid credentials succeeds and redirects to home
- [ ] Login with invalid credentials shows error message
- [ ] Token is stored in localStorage after login
- [ ] User info is stored in localStorage after login

### State Management

- [ ] Auth store tracks user and token correctly
- [ ] isAuthenticated computed property works
- [ ] Role computed properties (isAdmin, isBoard) work correctly
- [ ] Initialize() restores auth from localStorage on app mount

### API Integration

- [ ] Axios request interceptor adds Authorization header
- [ ] Axios response interceptor handles 401 and logs out
- [ ] Protected API calls work with token
- [ ] Protected API calls fail without token

### Router Guards

- [ ] Protected routes redirect to /login when not authenticated
- [ ] Login route redirects to home when already authenticated
- [ ] Navigation preserves intended destination in redirect query

### UI/UX

- [ ] Login form validates input
- [ ] Loading state shows during login
- [ ] Error messages display for failed login
- [ ] User info displays in header after login
- [ ] Logout button works and redirects to login
- [ ] Page reload preserves authentication

### Tests

- [ ] All Vitest unit tests pass
- [ ] All Cypress E2E tests pass
- [ ] Test coverage >= 80%

## Manual Testing Steps

### 1. Start Backend

```bash
cd src/Abuvi.API
dotnet run
```

### 2. Start Frontend

```bash
cd frontend
npm run dev
```

### 3. Test Flow

1. Open <http://localhost:5173>
2. Should redirect to /login
3. Try invalid credentials → see error
4. Register a user via Postman:

   ```bash
   curl -X POST http://localhost:5079/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{
       "email": "test@example.com",
       "password": "Test123!",
       "firstName": "Test",
       "lastName": "User"
     }'
   ```

5. Login with valid credentials
6. Should redirect to home and show user info
7. Check localStorage → token and user present
8. Refresh page → still authenticated
9. Click logout → redirected to login
10. Check localStorage → token and user cleared

### 4. Run Tests

```bash
# Unit tests
npm run test

# E2E tests
npm run cypress:run
```

## PrimeVue Configuration

Ensure PrimeVue is properly configured. If CSS is commented out in `main.ts`, use CDN:

**Path**: `frontend/index.html`

Add in `<head>`:

```html
<link rel="stylesheet" href="https://unpkg.com/primevue/resources/themes/lara-light-blue/theme.css" />
<link rel="stylesheet" href="https://unpkg.com/primeicons/primeicons.css" />
```

Or configure Vite properly:

**Path**: `frontend/vite.config.ts`

```typescript
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  },
  css: {
    preprocessorOptions: {
      css: {
        charset: false
      }
    }
  }
})
```

## Next Steps

After Phase 3 is complete:

1. **User authentication system is fully functional** end-to-end
2. Test thoroughly with real user scenarios
3. Add more features:
   - Password reset flow
   - Email verification
   - Remember me functionality
   - User profile page
   - Admin panel for user management
4. Implement next entities: FamilyUnit, Camp, Registration

## Security Reminders

- Token stored in localStorage (acceptable for MVP, consider httpOnly cookies for production)
- Always use HTTPS in production
- Implement CSRF protection if using cookies
- Consider implementing refresh tokens for better UX
- Add rate limiting to login endpoint (backend)
- Log security events (failed logins, etc.)

## Performance Considerations

- Auth store initialization happens once on app mount
- Token is read from localStorage on each API request (fast)
- Consider using session storage for sensitive apps
- Implement token refresh to avoid frequent re-logins

## Troubleshooting

# **## Token not being sent with requests

- Check axios interceptor is configured
- Verify token exists in localStorage
- Check Authorization header in Network tab

### 401 errors on valid token

- Check JWT secret matches between backend and frontend
- Verify token hasn't expired
- Check token format (should start with "eyJ")

### Login successful but redirects back to login

- Check router guard logic
- Verify isAuthenticated computed property
- Check localStorage is not being cleared

### CORS errors

- Verify backend CORS allows localhost:5173
- Check credentials are allowed in CORS config
- Ensure Origin header is correct

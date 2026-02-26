# Frontend Implementation Plan: feat-forgot-password — Forgot Password Flow

## Overview

Implements the full two-step forgot password flow: a "request reset" page and a "set new password" page. The implementation follows the project's Vue 3 Composition API architecture using composables for API communication, PrimeVue for UI, and Tailwind CSS for layout. No Pinia store is needed — the flow is transient and local to each page.

---

## Architecture Context

### Components / Composables Involved

| Role | File |
|---|---|
| New composable | `frontend/src/composables/usePasswordReset.ts` |
| New view | `frontend/src/views/ForgotPasswordPage.vue` |
| New view | `frontend/src/views/ResetPasswordPage.vue` |
| Modified | `frontend/src/components/auth/LoginForm.vue` (line 105) |
| Modified | `frontend/src/router/index.ts` |
| Modified | `frontend/src/types/auth.ts` |
| New tests | `frontend/src/composables/__tests__/usePasswordReset.test.ts` |
| New E2E | `frontend/cypress/e2e/forgot-password.cy.ts` |

### Routing Considerations

Both routes are **public** (`requiresAuth: false`). The existing guard in `router/index.ts` requires no changes to its logic — routes without `requiresAuth: true` pass straight to `next()`. Authenticated users can access these routes normally (renders inside `AuthenticatedLayout` — acceptable since the spec marks this case as "low priority").

### State Management

No Pinia store needed. Each page manages its own local reactive state via `ref` / `reactive`. The `usePasswordReset` composable is the only shared logic unit.

### API Layer

`usePasswordReset` uses the centralized `api` axios instance from `@/utils/api`. The backend always returns HTTP 200 for `POST /auth/forgot-password` (never reveals whether the email exists), so the composable treats any non-network-error response as success for that endpoint.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the frontend-specific feature branch.
- **Implementation Steps**:
  1. Ensure you're on the latest `main`:
     ```bash
     git checkout main && git pull origin main
     ```
  2. Create the frontend branch:
     ```bash
     git checkout -b feature/feat-forgot-password-frontend
     ```
  3. Verify: `git branch`
- **Notes**: Must be done before any code changes. **Do not reuse the general `feat-forgot-password` branch** if it exists — keep frontend and backend concerns in separate branches.

---

### Step 1: Add TypeScript Interfaces to `auth.ts`

- **File**: `frontend/src/types/auth.ts`
- **Action**: Add request types for the two new endpoints.
- **Implementation Steps**: Append the following two interfaces at the end of the file:

```typescript
export interface ForgotPasswordRequest {
  email: string
}

export interface ResetPasswordRequest {
  token: string
  newPassword: string
}
```

- **Notes**: These mirror the backend DTOs exactly. No response types are needed — both endpoints return a generic success message wrapped in `ApiResponse<object>` and the composable only needs to know success/failure.

---

### Step 2: Create `usePasswordReset` Composable

- **File**: `frontend/src/composables/usePasswordReset.ts`
- **Action**: Create a new composable that encapsulates both forgot-password and reset-password API calls.
- **Implementation**:

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'

export function usePasswordReset() {
  const loading = ref(false)
  const error = ref<string | null>(null)

  const forgotPassword = async (email: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.post<ApiResponse<object>>('/auth/forgot-password', { email })
      return true
    } catch (err: any) {
      // Network-level failure only — backend always returns 200
      error.value =
        err.response?.data?.error?.message ||
        'Error de red. Por favor intenta de nuevo.'
      return false
    } finally {
      loading.value = false
    }
  }

  const resetPassword = async (
    token: string,
    newPassword: string
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.post<ApiResponse<object>>('/auth/reset-password', { token, newPassword })
      return true
    } catch (err: any) {
      error.value =
        err.response?.data?.error?.message ||
        'El enlace de recuperación es inválido o ha expirado.'
      return false
    } finally {
      loading.value = false
    }
  }

  return { loading, error, forgotPassword, resetPassword }
}
```

- **Notes**:
  - `forgotPassword` always returns `true` on HTTP success (200). The backend guarantees 200 even for unknown emails.
  - `resetPassword` returns `false` on HTTP 400 (expired/invalid token) and extracts the Spanish error message from `err.response.data.error.message`.
  - Both functions reset `error` to `null` before each call so stale errors don't persist.

---

### Step 3: Write Vitest Unit Tests for `usePasswordReset`

> **TDD: Write these BEFORE implementing** if following strict TDD. For this plan, Step 2 and Step 3 are presented together but tests should be written first and made to pass.

- **File**: `frontend/src/composables/__tests__/usePasswordReset.test.ts`
- **Action**: Full test coverage for the composable.
- **Implementation**:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { usePasswordReset } from '@/composables/usePasswordReset'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('usePasswordReset', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // --- forgotPassword ---

  describe('forgotPassword', () => {
    it('should call POST /auth/forgot-password with email', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { forgotPassword } = usePasswordReset()
      await forgotPassword('test@example.com')

      expect(api.post).toHaveBeenCalledWith('/auth/forgot-password', { email: 'test@example.com' })
    })

    it('should return true on 200 response', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { forgotPassword } = usePasswordReset()
      const result = await forgotPassword('test@example.com')

      expect(result).toBe(true)
    })

    it('should return false and set error on network error', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      const { error, forgotPassword } = usePasswordReset()
      const result = await forgotPassword('test@example.com')

      expect(result).toBe(false)
      expect(error.value).toBe('Error de red. Por favor intenta de nuevo.')
    })

    it('should reset loading to false after success', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { loading, forgotPassword } = usePasswordReset()
      await forgotPassword('test@example.com')

      expect(loading.value).toBe(false)
    })

    it('should reset loading to false after error', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      const { loading, forgotPassword } = usePasswordReset()
      await forgotPassword('test@example.com')

      expect(loading.value).toBe(false)
    })

    it('should clear previous error on new call', async () => {
      vi.mocked(api.post)
        .mockRejectedValueOnce(new Error('Network error'))
        .mockResolvedValueOnce({ data: { success: true, data: {}, error: null } })

      const { error, forgotPassword } = usePasswordReset()
      await forgotPassword('test@example.com') // fails
      expect(error.value).not.toBeNull()

      await forgotPassword('test@example.com') // succeeds
      expect(error.value).toBeNull()
    })
  })

  // --- resetPassword ---

  describe('resetPassword', () => {
    it('should call POST /auth/reset-password with token and newPassword', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { resetPassword } = usePasswordReset()
      await resetPassword('my-token', 'newPass123')

      expect(api.post).toHaveBeenCalledWith('/auth/reset-password', {
        token: 'my-token',
        newPassword: 'newPass123'
      })
    })

    it('should return true on 200 response', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { resetPassword } = usePasswordReset()
      const result = await resetPassword('valid-token', 'newPass123')

      expect(result).toBe(true)
    })

    it('should return false and set error message from backend on 400', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: {
          status: 400,
          data: { error: { message: 'El enlace de recuperación es inválido o ha expirado.' } }
        }
      })

      const { error, resetPassword } = usePasswordReset()
      const result = await resetPassword('expired-token', 'newPass123')

      expect(result).toBe(false)
      expect(error.value).toBe('El enlace de recuperación es inválido o ha expirado.')
    })

    it('should use fallback error message when backend provides none', async () => {
      vi.mocked(api.post).mockRejectedValue({ response: { status: 400, data: {} } })

      const { error, resetPassword } = usePasswordReset()
      await resetPassword('expired-token', 'newPass123')

      expect(error.value).toBe('El enlace de recuperación es inválido o ha expirado.')
    })

    it('should reset loading to false after success', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: {}, error: null } })

      const { loading, resetPassword } = usePasswordReset()
      await resetPassword('valid-token', 'newPass123')

      expect(loading.value).toBe(false)
    })

    it('should reset loading to false after error', async () => {
      vi.mocked(api.post).mockRejectedValue({ response: { status: 400, data: {} } })

      const { loading, resetPassword } = usePasswordReset()
      await resetPassword('expired-token', 'newPass123')

      expect(loading.value).toBe(false)
    })

    it('should clear previous error on new call', async () => {
      vi.mocked(api.post)
        .mockRejectedValueOnce({ response: { status: 400, data: {} } })
        .mockResolvedValueOnce({ data: { success: true, data: {}, error: null } })

      const { error, resetPassword } = usePasswordReset()
      await resetPassword('bad-token', 'newPass123')
      expect(error.value).not.toBeNull()

      await resetPassword('good-token', 'newPass123')
      expect(error.value).toBeNull()
    })
  })
})
```

- **Run tests**: `npx vitest run src/composables/__tests__/usePasswordReset.test.ts`

---

### Step 4: Update Router

- **File**: `frontend/src/router/index.ts`
- **Action**: Add two public routes. Insert **before** the existing legacy login/register redirect routes (near line 160).
- **Implementation**: Add these two route objects inside the `routes` array:

```typescript
// Password reset routes — public, no auth required
{
  path: '/forgot-password',
  name: 'forgot-password',
  component: () => import('@/views/ForgotPasswordPage.vue'),
  meta: { requiresAuth: false, title: 'ABUVI | Recuperar Contraseña' }
},
{
  path: '/reset-password',
  name: 'reset-password',
  component: () => import('@/views/ResetPasswordPage.vue'),
  meta: { requiresAuth: false, title: 'ABUVI | Nueva Contraseña' }
},
```

- **Notes**:
  - The existing route guard requires **no changes**. Since `requiresAuth` is `false` (or absent), the guard's first check (`to.meta.requiresAuth && !auth.isAuthenticated`) is false, and subsequent checks (`requiresAdmin`, `requiresBoard`) are also false. The guard falls through to `next()`.
  - The authenticated-user redirect only fires for `to.path === "/"`, so `/forgot-password` and `/reset-password` are unaffected.
  - Lazy-loaded with `() => import()` for code splitting.

---

### Step 5: Fix the Link in `LoginForm.vue`

- **File**: `frontend/src/components/auth/LoginForm.vue`
- **Action**: Replace the static `<a href="#">` at line 105 with a `RouterLink`.
- **Before** (lines 105–107):
  ```html
  <a href="#" class="text-sm text-primary-600 hover:text-primary-700">
    ¿Olvidaste tu contraseña?
  </a>
  ```
- **After**:
  ```html
  <RouterLink to="/forgot-password" class="text-sm text-primary-600 hover:text-primary-700">
    ¿Olvidaste tu contraseña?
  </RouterLink>
  ```
- **Notes**: `RouterLink` is globally registered in the project — no import needed. The `to` attribute uses the path directly. Using the name `forgot-password` would also work (`{ name: 'forgot-password' }`).

---

### Step 6: Create `ForgotPasswordPage.vue`

- **File**: `frontend/src/views/ForgotPasswordPage.vue`
- **Action**: Create the full-page forgot-password form.
- **Implementation**:

```vue
<script setup lang="ts">
import { reactive, ref } from 'vue'
import { usePasswordReset } from '@/composables/usePasswordReset'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Message from 'primevue/message'

const { loading, error, forgotPassword } = usePasswordReset()

const formData = reactive({ email: '' })
const fieldErrors = ref<Record<string, string>>({})
const submitted = ref(false)

const validate = (): boolean => {
  fieldErrors.value = {}

  if (!formData.email.trim()) {
    fieldErrors.value.email = 'El correo electrónico es obligatorio'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    fieldErrors.value.email = 'Formato de correo electrónico inválido'
  }

  return Object.keys(fieldErrors.value).length === 0
}

const handleSubmit = async () => {
  if (!validate()) return

  const success = await forgotPassword(formData.email)
  if (success) {
    submitted.value = true
  }
}
</script>

<template>
  <div class="relative flex min-h-screen items-center justify-center">
    <!-- Background image (same as LandingPage) -->
    <div
      class="absolute inset-0 bg-cover bg-center bg-no-repeat blur-sm"
      style="background-image: url('/src/assets/images/abuvi-background.jpg')"
    />
    <!-- Dark overlay -->
    <div class="absolute inset-0 bg-black/40" />

    <!-- Card -->
    <div class="relative z-10 w-full max-w-md px-4">
      <div class="rounded-xl bg-white/90 p-8 shadow-2xl backdrop-blur-sm">
        <!-- Header -->
        <div class="mb-6 text-center">
          <h1 class="text-2xl font-bold text-gray-900">Recuperar Contraseña</h1>
          <p class="mt-1 text-sm text-gray-600">
            Introduce tu correo y te enviaremos un enlace de recuperación.
          </p>
        </div>

        <!-- Success state -->
        <div v-if="submitted" class="flex flex-col gap-4">
          <Message severity="success" :closable="false">
            Si tu correo está registrado, recibirás un enlace para restablecer tu contraseña.
          </Message>
          <RouterLink
            to="/"
            class="mt-2 text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
          >
            Volver al inicio de sesión
          </RouterLink>
        </div>

        <!-- Form state -->
        <form v-else class="flex flex-col gap-4" @submit.prevent="handleSubmit">
          <!-- Network error -->
          <Message v-if="error" severity="error" :closable="false">
            {{ error }}
          </Message>

          <div class="flex flex-col gap-2">
            <label for="email" class="text-sm font-medium text-gray-700">
              Correo Electrónico *
            </label>
            <InputText
              id="email"
              v-model="formData.email"
              type="email"
              placeholder="tu@email.com"
              :invalid="!!fieldErrors.email"
              :disabled="loading"
              data-testid="email-input"
            />
            <small v-if="fieldErrors.email" class="text-red-500">
              {{ fieldErrors.email }}
            </small>
          </div>

          <Button
            type="submit"
            label="Enviar enlace de recuperación"
            :loading="loading"
            :disabled="loading"
            class="w-full"
            data-testid="submit-button"
          />

          <RouterLink
            to="/"
            class="text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
          >
            Volver al inicio de sesión
          </RouterLink>
        </form>
      </div>
    </div>
  </div>
</template>
```

- **Notes**:
  - Uses the same full-screen background + dark overlay + centered card as `LandingPage.vue`. Check the exact path to the background image in the `LandingPage.vue` component and use the same one.
  - `submitted` controls the two-state rendering (form vs. success message). Once submitted successfully, we never show the form again (the backend always returns 200, so `success` is always true unless there's a network error).
  - `error` from composable only appears on network failures (very rare scenario).
  - `data-testid` attributes are added for Cypress.
  - No `<style>` block — Tailwind only.

---

### Step 7: Create `ResetPasswordPage.vue`

- **File**: `frontend/src/views/ResetPasswordPage.vue`
- **Action**: Create the full-page reset-password form.
- **Implementation**:

```vue
<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { usePasswordReset } from '@/composables/usePasswordReset'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'

const route = useRoute()
const router = useRouter()
const { loading, error, resetPassword } = usePasswordReset()

const token = ref<string | null>(null)
const tokenMissing = ref(false)
const successState = ref(false)

const formData = reactive({
  newPassword: '',
  confirmPassword: ''
})
const fieldErrors = ref<Record<string, string>>({})

onMounted(() => {
  const queryToken = route.query.token
  if (!queryToken || typeof queryToken !== 'string') {
    tokenMissing.value = true
    // Redirect to landing after 3 seconds
    setTimeout(() => router.push('/'), 3000)
  } else {
    token.value = queryToken
  }
})

const validate = (): boolean => {
  fieldErrors.value = {}

  if (!formData.newPassword) {
    fieldErrors.value.newPassword = 'La nueva contraseña es obligatoria'
  } else if (formData.newPassword.length < 8) {
    fieldErrors.value.newPassword = 'La contraseña debe tener al menos 8 caracteres'
  }

  if (!formData.confirmPassword) {
    fieldErrors.value.confirmPassword = 'Debes confirmar la contraseña'
  } else if (formData.newPassword !== formData.confirmPassword) {
    fieldErrors.value.confirmPassword = 'Las contraseñas no coinciden'
  }

  return Object.keys(fieldErrors.value).length === 0
}

const handleSubmit = async () => {
  if (!token.value || !validate()) return

  const success = await resetPassword(token.value, formData.newPassword)
  if (success) {
    successState.value = true
  }
}
</script>

<template>
  <div class="relative flex min-h-screen items-center justify-center">
    <!-- Background image -->
    <div
      class="absolute inset-0 bg-cover bg-center bg-no-repeat blur-sm"
      style="background-image: url('/src/assets/images/abuvi-background.jpg')"
    />
    <!-- Dark overlay -->
    <div class="absolute inset-0 bg-black/40" />

    <!-- Card -->
    <div class="relative z-10 w-full max-w-md px-4">
      <div class="rounded-xl bg-white/90 p-8 shadow-2xl backdrop-blur-sm">

        <!-- Missing token state -->
        <div v-if="tokenMissing" class="flex flex-col gap-4 text-center">
          <Message severity="error" :closable="false">
            El enlace de recuperación no es válido. Serás redirigido al inicio...
          </Message>
          <RouterLink to="/" class="text-sm text-primary-600 hover:text-primary-700 hover:underline">
            Volver al inicio de sesión
          </RouterLink>
        </div>

        <!-- Success state -->
        <div v-else-if="successState" class="flex flex-col gap-4">
          <Message severity="success" :closable="false">
            Tu contraseña ha sido restablecida exitosamente.
          </Message>
          <RouterLink
            to="/"
            class="text-center text-sm text-primary-600 hover:text-primary-700 hover:underline"
            data-testid="login-link"
          >
            Iniciar Sesión
          </RouterLink>
        </div>

        <!-- Form state -->
        <div v-else>
          <div class="mb-6 text-center">
            <h1 class="text-2xl font-bold text-gray-900">Nueva Contraseña</h1>
            <p class="mt-1 text-sm text-gray-600">Introduce tu nueva contraseña.</p>
          </div>

          <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
            <!-- Backend error (invalid/expired token) -->
            <Message v-if="error" severity="error" :closable="false" data-testid="error-message">
              {{ error }}
            </Message>

            <div class="flex flex-col gap-2">
              <label for="newPassword" class="text-sm font-medium text-gray-700">
                Nueva Contraseña *
              </label>
              <Password
                id="newPassword"
                v-model="formData.newPassword"
                toggle-mask
                :feedback="false"
                placeholder="Mínimo 8 caracteres"
                :invalid="!!fieldErrors.newPassword"
                :disabled="loading"
                input-class="w-full"
                data-testid="new-password-input"
              />
              <small v-if="fieldErrors.newPassword" class="text-red-500">
                {{ fieldErrors.newPassword }}
              </small>
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
                placeholder="Repite la contraseña"
                :invalid="!!fieldErrors.confirmPassword"
                :disabled="loading"
                input-class="w-full"
                data-testid="confirm-password-input"
              />
              <small v-if="fieldErrors.confirmPassword" class="text-red-500">
                {{ fieldErrors.confirmPassword }}
              </small>
            </div>

            <Button
              type="submit"
              label="Restablecer Contraseña"
              :loading="loading"
              :disabled="loading"
              class="w-full"
              data-testid="submit-button"
            />
          </form>
        </div>

      </div>
    </div>
  </div>
</template>
```

- **Notes**:
  - `onMounted` reads `route.query.token`. If absent, shows error and redirects to `/` after 3 seconds.
  - `error` from the composable captures the backend 400 error message ("El enlace de recuperación es inválido o ha expirado.").
  - `Password` component from PrimeVue uses `toggle-mask` and `:feedback="false"` (no strength meter) — consistent with `LoginForm.vue`'s password field.
  - Uses `v-else-if` / `v-else` guard to show exactly one state at a time: missing token → success → form.
  - **Important**: Verify the background image path. Check `LandingPage.vue` for the exact `style` attribute to copy. The path used above (`/src/assets/images/abuvi-background.jpg`) should match what's used in `LandingPage.vue`.

---

### Step 8: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/forgot-password.cy.ts`
- **Action**: E2E tests for both pages.
- **Implementation**:

```typescript
describe('Forgot Password Flow', () => {
  beforeEach(() => {
    cy.clearLocalStorage()
  })

  describe('Navigation from Login', () => {
    it('should navigate to /forgot-password from login form', () => {
      cy.visit('/')
      cy.contains('¿Olvidaste tu contraseña?').click()
      cy.url().should('include', '/forgot-password')
    })
  })

  describe('Forgot Password Page', () => {
    beforeEach(() => {
      cy.visit('/forgot-password')
    })

    it('should display the email input and submit button', () => {
      cy.get('[data-testid="email-input"]').should('be.visible')
      cy.get('[data-testid="submit-button"]').should('be.visible')
      cy.contains('Enviar enlace de recuperación').should('be.visible')
    })

    it('should show error when email is empty on submit', () => {
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('El correo electrónico es obligatorio').should('be.visible')
    })

    it('should show error for invalid email format', () => {
      cy.get('[data-testid="email-input"]').type('not-an-email')
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('Formato de correo electrónico inválido').should('be.visible')
    })

    it('should show success message after valid email submission', () => {
      cy.intercept('POST', '/api/auth/forgot-password', {
        statusCode: 200,
        body: { success: true, data: { message: 'ok' }, error: null }
      }).as('forgotPassword')

      cy.get('[data-testid="email-input"]').type('user@example.com')
      cy.get('[data-testid="submit-button"]').click()

      cy.wait('@forgotPassword')
      cy.contains('Si tu correo está registrado').should('be.visible')
    })

    it('should show "Volver al inicio de sesión" link after success', () => {
      cy.intercept('POST', '/api/auth/forgot-password', {
        statusCode: 200,
        body: { success: true, data: {}, error: null }
      }).as('forgotPassword')

      cy.get('[data-testid="email-input"]').type('user@example.com')
      cy.get('[data-testid="submit-button"]').click()
      cy.wait('@forgotPassword')

      cy.contains('Volver al inicio de sesión').should('be.visible').click()
      cy.url().should('eq', Cypress.config().baseUrl + '/')
    })
  })

  describe('Reset Password Page — missing token', () => {
    it('should show error and redirect when token is absent', () => {
      cy.visit('/reset-password')
      cy.contains('El enlace de recuperación no es válido').should('be.visible')
      // Wait for auto-redirect (3 seconds)
      cy.url({ timeout: 4000 }).should('eq', Cypress.config().baseUrl + '/')
    })
  })

  describe('Reset Password Page — with token', () => {
    beforeEach(() => {
      cy.visit('/reset-password?token=test-token-abc')
    })

    it('should display password fields and submit button', () => {
      cy.get('[data-testid="new-password-input"]').should('be.visible')
      cy.get('[data-testid="confirm-password-input"]').should('be.visible')
      cy.get('[data-testid="submit-button"]').should('be.visible')
      cy.contains('Restablecer Contraseña').should('be.visible')
    })

    it('should show error when passwords are empty', () => {
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('La nueva contraseña es obligatoria').should('be.visible')
      cy.contains('Debes confirmar la contraseña').should('be.visible')
    })

    it('should show error when password is too short', () => {
      cy.get('[data-testid="new-password-input"]').find('input').type('short')
      cy.get('[data-testid="confirm-password-input"]').find('input').type('short')
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('La contraseña debe tener al menos 8 caracteres').should('be.visible')
    })

    it('should show error when passwords do not match', () => {
      cy.get('[data-testid="new-password-input"]').find('input').type('ValidPass123')
      cy.get('[data-testid="confirm-password-input"]').find('input').type('DifferentPass123')
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('Las contraseñas no coinciden').should('be.visible')
    })

    it('should show success message on valid submission', () => {
      cy.intercept('POST', '/api/auth/reset-password', {
        statusCode: 200,
        body: { success: true, data: { message: 'ok' }, error: null }
      }).as('resetPassword')

      cy.get('[data-testid="new-password-input"]').find('input').type('NewPassword123')
      cy.get('[data-testid="confirm-password-input"]').find('input').type('NewPassword123')
      cy.get('[data-testid="submit-button"]').click()

      cy.wait('@resetPassword')
      cy.contains('Tu contraseña ha sido restablecida exitosamente.').should('be.visible')
      cy.get('[data-testid="login-link"]').should('be.visible')
    })

    it('should show error message on invalid/expired token (400)', () => {
      cy.intercept('POST', '/api/auth/reset-password', {
        statusCode: 400,
        body: {
          success: false,
          data: null,
          error: { message: 'El enlace de recuperación es inválido o ha expirado.', code: 'INVALID_OR_EXPIRED_TOKEN' }
        }
      }).as('resetPassword')

      cy.get('[data-testid="new-password-input"]').find('input').type('NewPassword123')
      cy.get('[data-testid="confirm-password-input"]').find('input').type('NewPassword123')
      cy.get('[data-testid="submit-button"]').click()

      cy.wait('@resetPassword')
      cy.get('[data-testid="error-message"]').should('contain.text', 'El enlace de recuperación es inválido o ha expirado.')
    })
  })
})
```

- **Notes**:
  - All network calls use `cy.intercept()` so E2E tests don't depend on a live backend.
  - PrimeVue's `Password` component renders an `<input>` inside a wrapper div — Cypress must use `.find('input')` to type into it. Regular `InputText` renders the input directly so `cy.get('[data-testid="..."]')` works without `.find('input')`.
  - The auto-redirect test uses `{ timeout: 4000 }` to wait for the 3-second timer.

---

### Step 9: Update Technical Documentation

- **File**: `ai-specs/specs/frontend-standards.mdc`
- **Action**: Update the "Authentication Patterns" → "Public Route Pattern" section to explicitly mention the forgot/reset password routes as examples of public auth routes (alongside legal pages).
- **Implementation Steps**:
  1. In the `frontend-standards.mdc` "Public Route Pattern" section, add a note that forgot-password and reset-password are also public routes accessible without authentication.
  2. No changes to `api-spec.yml` needed from the frontend side (backend owns the API spec).
  3. If the project maintains a routing table doc, add the two new routes there.

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/feat-forgot-password-frontend`
2. **Step 1**: Add TypeScript types to `auth.ts`
3. **Step 2**: Write failing tests (Step 3 written first per TDD)
4. **Step 3**: Create `usePasswordReset.ts` to make tests pass
5. **Step 4**: Add routes to `router/index.ts`
6. **Step 5**: Fix `RouterLink` in `LoginForm.vue`
7. **Step 6**: Create `ForgotPasswordPage.vue`
8. **Step 7**: Create `ResetPasswordPage.vue`
9. **Step 8**: Write Cypress E2E tests
10. **Step 9**: Update documentation

---

## Testing Checklist

### Vitest Unit Tests (`usePasswordReset.test.ts`)

- [ ] `forgotPassword` — calls correct endpoint with email
- [ ] `forgotPassword` — returns `true` on 200
- [ ] `forgotPassword` — returns `false` + sets error on network failure
- [ ] `forgotPassword` — resets loading to false after success
- [ ] `forgotPassword` — resets loading to false after error
- [ ] `forgotPassword` — clears previous error on new call
- [ ] `resetPassword` — calls correct endpoint with token + newPassword
- [ ] `resetPassword` — returns `true` on 200
- [ ] `resetPassword` — returns `false` + backend error message on 400
- [ ] `resetPassword` — uses fallback error message when none from backend
- [ ] `resetPassword` — resets loading after success
- [ ] `resetPassword` — resets loading after error
- [ ] `resetPassword` — clears previous error on new call

### Cypress E2E Tests (`forgot-password.cy.ts`)

- [ ] "¿Olvidaste tu contraseña?" link navigates to `/forgot-password`
- [ ] Forgot page shows email input + submit button
- [ ] Empty email shows validation error
- [ ] Invalid email format shows validation error
- [ ] Valid email submission shows generic success message
- [ ] Success state shows "Volver al inicio de sesión" link → navigates to `/`
- [ ] `/reset-password` with no token shows error + auto-redirects to `/`
- [ ] `/reset-password?token=X` shows password fields
- [ ] Empty submit shows field validation errors
- [ ] Password shorter than 8 chars shows length error
- [ ] Mismatched passwords shows match error
- [ ] Valid submission shows success message + login link
- [ ] Backend 400 error shows "inválido o ha expirado" message

### Manual Verification

- [ ] Background image renders correctly on both pages
- [ ] "Recordarme" / "¿Olvidaste tu contraseña?" row in LoginForm still looks correct
- [ ] No regression in login / register flows
- [ ] Type-check passes: `npx vue-tsc --noEmit`

---

## Error Handling Patterns

| Scenario | Handling |
|---|---|
| Empty email | Client-side validation, field error below input |
| Invalid email format | Client-side validation, field error below input |
| Network error on `POST /forgot-password` | Composable `error` ref → `<Message severity="error">` |
| Empty password fields | Client-side validation, field error below each input |
| Passwords don't match | Client-side validation |
| Password < 8 chars | Client-side validation |
| Missing `token` in URL | `onMounted` check → shows error + auto-redirects |
| Backend 400 on `POST /reset-password` | Composable `error` ref → `<Message severity="error">` at top of form |
| Success states | Reactive `submitted` / `successState` ref controls which UI is shown |

---

## UI/UX Considerations

- **Layout**: Full-screen background (same `LandingPage.vue` style) — background image + `bg-black/40` overlay + centered white/translucent card.
- **Card**: `rounded-xl bg-white/90 p-8 shadow-2xl backdrop-blur-sm` — matches existing auth styling.
- **PrimeVue components**: `InputText`, `Password` (with `toggle-mask`, no `feedback`), `Button` (with `:loading`), `Message` (with `severity`).
- **Loading state**: `Button` gets `:loading="loading"` and `:disabled="loading"` to prevent double submission.
- **Success state**: Replaces the form entirely — no form is shown after submission.
- **Missing token**: Shown immediately on mount (not after a failed API call).
- **Responsive**: The `max-w-md w-full px-4` pattern ensures mobile-first responsive centering.
- **Accessibility**: All inputs have `id` + `<label for="">` pairs. Error messages appear as `<small>` below each field.

---

## Dependencies

No new npm packages required. All components and utilities are already available:

| Package | Usage |
|---|---|
| `primevue/inputtext` | Email input on ForgotPasswordPage |
| `primevue/password` | Password inputs on ResetPasswordPage |
| `primevue/button` | Submit buttons |
| `primevue/message` | Success / error feedback |
| `vue-router` | `RouterLink`, `useRoute`, `useRouter` |
| `@/utils/api` | Axios instance for API calls |

---

## Notes

1. **Background image path**: Before creating the view files, verify the exact background image path from `LandingPage.vue`. If the path differs from `'/src/assets/images/abuvi-background.jpg'`, update both views accordingly.

2. **PrimeVue Password in Cypress**: PrimeVue's `<Password>` component wraps the `<input>` in a container div. In Cypress, always use `.find('input')` after the `data-testid` selector to interact with the actual input. This matches the pattern in the reset password E2E tests above.

3. **Router guard unchanged**: No changes to the guard function itself are needed. The new routes naturally pass through because they have no `requiresAuth`, `requiresAdmin`, or `requiresBoard` metadata.

4. **One-time success**: The forgot-password page shows the success message and removes the form. There is no "send again" button — this prevents accidental double-submissions and is sufficient for the spec.

5. **Language**: All user-facing strings are in Spanish. All code, variable names, composable names, and comments are in English.

6. **TypeScript strict mode**: No `any` types. The `err: any` in catch blocks is the accepted project pattern (see `stores/auth.ts`).

---

## Next Steps After Implementation

- Merge `feature/feat-forgot-password-frontend` after tests pass and code review is approved.
- Coordinate with the backend branch (`feat-forgot-password`) — both must be merged before the feature is fully functional end-to-end.
- Smoke test the full flow in a staging environment once both branches are merged.

---

## Implementation Verification

### Code Quality

- [ ] All components use `<script setup lang="ts">` — no Options API
- [ ] No `any` types (except `err: any` in catch — project convention)
- [ ] No `<style>` blocks — Tailwind only
- [ ] `npx vue-tsc --noEmit` passes without errors

### Functionality

- [ ] `/forgot-password` renders and accepts email input
- [ ] `/reset-password?token=X` renders password form
- [ ] `/reset-password` (no token) shows error + redirects
- [ ] `RouterLink` in `LoginForm.vue` navigates correctly
- [ ] Composable handles all API response cases

### Testing

- [ ] All Vitest tests pass: `npx vitest run`
- [ ] All Cypress tests pass: `npx cypress run --spec 'cypress/e2e/forgot-password.cy.ts'`
- [ ] Existing tests unaffected (no regression)

### Integration

- [ ] `usePasswordReset` calls `POST /api/auth/forgot-password` with `{ email }`
- [ ] `usePasswordReset` calls `POST /api/auth/reset-password` with `{ token, newPassword }`
- [ ] Error messages from backend are displayed correctly in the UI

### Documentation

- [ ] `frontend-standards.mdc` updated with public auth route examples

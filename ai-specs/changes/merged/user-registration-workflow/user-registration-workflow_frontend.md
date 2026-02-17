# Frontend Implementation Plan: User Registration Workflow

## Overview

This plan details the frontend implementation for the complete user registration workflow with email verification. The implementation follows Vue 3 Composition API patterns with `<script setup lang="ts">`, PrimeVue component library, Tailwind CSS, and composable-based architecture.

The workflow consists of three main user flows:

1. **User Registration** - New users fill out a registration form with email/password
2. **Email Verification** - Users verify their email via a token sent to their inbox
3. **Resend Verification** - Users can request a new verification email if needed

All backend API endpoints are already implemented and documented in [api-endpoints.md](../../specs/api-endpoints.md).

## Architecture Context

### Components to Create

- `src/Abuvi.Web/src/pages/auth/RegisterPage.vue` - Registration form page
- `src/Abuvi.Web/src/pages/auth/VerifyEmailPage.vue` - Email verification confirmation page
- `src/Abuvi.Web/src/pages/auth/ResendVerificationPage.vue` - Resend verification email page
- `src/Abuvi.Web/src/components/auth/RegistrationForm.vue` - Reusable registration form component
- `src/Abuvi.Web/src/components/auth/PasswordStrengthMeter.vue` - Password validation feedback component

### Composables to Create

- `src/Abuvi.Web/src/composables/useAuth.ts` - Authentication API calls (register, verify, resend)

### Types to Create

- `src/Abuvi.Web/src/types/auth.ts` - TypeScript interfaces for auth-related data

### Routing

- Add routes for `/register`, `/verify-email`, `/resend-verification`
- Public routes (no authentication required)

### State Management

- **Local component state** for form data and UI state
- No Pinia store needed yet (auth store will be created in login feature)

## Implementation Steps

### Step 0: Create Feature Branch

**Action**: Create and switch to a new feature branch following the development workflow.

**Branch Naming**: `feature/user-registration-workflow-frontend`

**Implementation Steps**:

1. Ensure you're on the latest `main` branch
2. Pull latest changes: `git pull origin main`
3. Create new branch: `git checkout -b feature/user-registration-workflow-frontend`
4. Verify branch creation: `git branch`

**Notes**: This must be the FIRST step before any code changes. The branch name uses `-frontend` suffix to separate frontend work from backend implementation (which already exists on `main`).

---

### Step 1: Define TypeScript Interfaces

**File**: `src/Abuvi.Web/src/types/auth.ts`

**Action**: Create TypeScript interfaces matching the backend API contracts

**Implementation Steps**:

1. Create the `auth.ts` file in `src/Abuvi.Web/src/types/`
2. Define the following interfaces:

```typescript
// User type matching backend UserResponse DTO
export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  phone?: string
  documentNumber?: string
  role: UserRole
  isActive: boolean
  emailVerified: boolean
  createdAt: string
  updatedAt: string
}

export type UserRole = 'Admin' | 'Board' | 'Member'

// Registration request matching backend RegisterUserRequest
export interface RegisterUserRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  documentNumber?: string
  phone?: string
  acceptedTerms: boolean
}

// Verify email request
export interface VerifyEmailRequest {
  token: string
}

// Resend verification request
export interface ResendVerificationRequest {
  email: string
}

// Generic message response
export interface MessageResponse {
  message: string
}
```

**Dependencies**: None (uses built-in TypeScript types)

**Implementation Notes**:

- Types match backend DTOs exactly for type safety
- Optional fields use `?` syntax
- All fields use English naming per project standards
- `UserRole` uses string literal union type for type safety

---

### Step 2: Create Authentication Composable

**File**: `src/Abuvi.Web/src/composables/useAuth.ts`

**Action**: Implement composable for all authentication-related API calls

**Function Signature**:

```typescript
export function useAuth() {
  const loading = ref(false)
  const error = ref<string | null>(null)

  return {
    loading,
    error,
    registerUser,
    verifyEmail,
    resendVerification
  }
}
```

**Implementation Steps**:

1. Create `composables/` directory if it doesn't exist
2. Create `useAuth.ts` file
3. Implement the following methods:
   - `registerUser(request: RegisterUserRequest): Promise<User | null>`
   - `verifyEmail(request: VerifyEmailRequest): Promise<boolean>`
   - `resendVerification(request: ResendVerificationRequest): Promise<boolean>`
4. Each method should:
   - Set `loading.value = true` at start
   - Clear `error.value = null` at start
   - Make API call using configured `api` instance from `@/utils/api`
   - Handle success: return data
   - Handle error: set `error.value` with user-friendly message, return null/false
   - Set `loading.value = false` in finally block
5. Map backend error codes to user-friendly messages:
   - `EMAIL_EXISTS` → "An account with this email already exists"
   - `DOCUMENT_EXISTS` → "An account with this document number already exists"
   - `VERIFICATION_FAILED` → "Verification token has expired. Please request a new one."
   - `EMAIL_NOT_VERIFIED` → "Please verify your email before logging in"
   - `NOT_FOUND` → "Invalid verification token"
   - `RESEND_FAILED` → "Email is already verified"
   - Default → "An error occurred. Please try again."

**Dependencies**:

- `import { ref } from 'vue'`
- `import { api } from '@/utils/api'`
- `import type { ApiResponse } from '@/types/api'`
- `import type { RegisterUserRequest, VerifyEmailRequest, ResendVerificationRequest, User, MessageResponse } from '@/types/auth'`

**Implementation Notes**:

- Composable manages loading/error states internally
- Returns reactive refs for component consumption
- All API endpoints follow `/api/auth/*` pattern per backend documentation
- Handle validation errors from `ApiResponse.error.details` if present
- Use axios error handling to extract backend error codes

**Example Implementation Pattern**:

```typescript
const registerUser = async (request: RegisterUserRequest): Promise<User | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.post<ApiResponse<User>>('/auth/register-user', request)
    return response.data.data
  } catch (err: any) {
    const errorCode = err.response?.data?.error?.code
    error.value = mapErrorCodeToMessage(errorCode)
    return null
  } finally {
    loading.value = false
  }
}
```

---

### Step 3: Create Password Strength Meter Component

**File**: `src/Abuvi.Web/src/components/auth/PasswordStrengthMeter.vue`

**Action**: Create a reusable component to show password strength feedback

**Component Signature**:

```typescript
interface Props {
  password: string
}
```

**Implementation Steps**:

1. Create `components/auth/` directory
2. Create `PasswordStrengthMeter.vue` with `<script setup lang="ts">`
3. Define props: `{ password: string }`
4. Implement password strength logic:
   - Check length (min 8 characters)
   - Check uppercase presence
   - Check lowercase presence
   - Check digit presence
   - Check special character presence (@$!%*?&#)
5. Calculate strength score (0-5 based on criteria met)
6. Compute strength label: "Weak", "Fair", "Good", "Strong"
7. Render visual indicator using Tailwind:
   - Progress bar with color based on strength (red → yellow → green)
   - Text feedback for missing criteria
8. Use PrimeVue `ProgressBar` component for visual feedback

**Dependencies**:

- `import { computed } from 'vue'`
- `import ProgressBar from 'primevue/progressbar'`

**Implementation Notes**:

- Purely presentational component (no API calls)
- Real-time feedback as user types
- Criteria match backend validation rules
- Use Tailwind for styling (no `<style>` block)
- Accessible with proper ARIA labels

---

### Step 4: Create Registration Form Component

**File**: `src/Abuvi.Web/src/components/auth/RegistrationForm.vue`

**Action**: Create the main registration form component with validation

**Component Signature**:

```typescript
interface Emits {
  (e: 'submit', data: RegisterUserRequest): void
  (e: 'cancel'): void
}
```

**Implementation Steps**:

1. Create `RegistrationForm.vue` in `components/auth/`
2. Define reactive form state using `reactive()`:

   ```typescript
   const formData = reactive<RegisterUserRequest>({
     email: '',
     password: '',
     firstName: '',
     lastName: '',
     documentNumber: '',
     phone: '',
     acceptedTerms: false
   })
   ```

3. Implement client-side validation:
   - Email: required, valid format, max 255 chars
   - Password: required, min 8 chars, complexity rules (shown via PasswordStrengthMeter)
   - First/Last Name: required, max 100 chars
   - Document Number: optional, max 50 chars, uppercase alphanumeric only
   - Phone: optional, E.164 format validation (regex: `^\+?[1-9]\d{1,14}$`)
   - Terms: must be true
4. Create validation error state: `const errors = ref<Record<string, string>>({})`
5. Implement `validate()` function that checks all fields and populates `errors`
6. Implement `handleSubmit()` that validates then emits 'submit' event
7. Use PrimeVue form components:
   - `InputText` for text fields
   - `Password` for password field with toggle visibility
   - `Checkbox` for terms acceptance
   - `Button` for submit/cancel
8. Show validation errors inline below each field
9. Disable submit button while invalid or submitting
10. Use Tailwind CSS for layout and spacing

**Dependencies**:

- `import { reactive, ref, computed } from 'vue'`
- `import InputText from 'primevue/inputtext'`
- `import Password from 'primevue/password'`
- `import Checkbox from 'primevue/checkbox'`
- `import Button from 'primevue/button'`
- `import PasswordStrengthMeter from './PasswordStrengthMeter.vue'`
- `import type { RegisterUserRequest } from '@/types/auth'`

**Implementation Notes**:

- Form validation happens on submit (not on blur)
- Password field uses PrimeVue's built-in password component with toggle
- Phone and document number are optional (not marked with asterisk)
- Terms checkbox is required and must be checked
- Form is responsive using Tailwind grid classes
- All labels and messages in English
- Emit data upwards rather than calling API directly (separation of concerns)

---

### Step 5: Create Registration Page

**File**: `src/Abuvi.Web/src/pages/auth/RegisterPage.vue`

**Action**: Create the registration page that uses RegistrationForm and handles registration flow

**Implementation Steps**:

1. Create `pages/auth/` directory
2. Create `RegisterPage.vue` with `<script setup lang="ts">`
3. Use `useAuth()` composable for API calls
4. Use `useRouter()` for navigation
5. Define local state:
   - `showSuccessMessage` (boolean) - show after successful registration
   - `registeredEmail` (string) - store email for success message
6. Implement `handleSubmit(data: RegisterUserRequest)`:
   - Call `registerUser(data)` from composable
   - On success:
     - Store email
     - Show success message with instructions to check email
     - Optionally redirect to resend verification page after 5 seconds
   - On error: Display error from composable using PrimeVue `Message` component
7. Layout:
   - Centered card design with max-width
   - App title/logo at top
   - `RegistrationForm` component
   - Success message section (conditionally rendered)
   - Link to login page (for users who already have an account)
8. Use PrimeVue `Card` for container
9. Use PrimeVue `Message` for errors
10. Use Tailwind for responsive layout

**Dependencies**:

- `import { ref } from 'vue'`
- `import { useRouter } from 'vue-router'`
- `import Card from 'primevue/card'`
- `import Message from 'primevue/message'`
- `import RegistrationForm from '@/components/auth/RegistrationForm.vue'`
- `import { useAuth } from '@/composables/useAuth'`
- `import type { RegisterUserRequest } from '@/types/auth'`

**Implementation Notes**:

- Page manages orchestration between form and API
- Shows loading state during submission (passed to form)
- Success message should be clear: "Registration successful! Please check your email to verify your account."
- Include link to resend verification page in success message
- Mobile-friendly responsive design
- Accessible with proper page title and semantic HTML

---

### Step 6: Create Email Verification Page

**File**: `src/Abuvi.Web/src/pages/auth/VerifyEmailPage.vue`

**Action**: Create page that handles email verification via URL token

**Implementation Steps**:

1. Create `VerifyEmailPage.vue` in `pages/auth/`
2. Use `useAuth()` composable
3. Use `useRouter()` and `useRoute()` for navigation and query params
4. Define local state:
   - `verificationStatus`: 'verifying' | 'success' | 'error'
   - `errorMessage`: string | null
5. On component mount (`onMounted`):
   - Extract token from query params: `route.query.token`
   - If no token: show error "Invalid verification link"
   - If token present: call `verifyEmail({ token })`
   - On success: set status to 'success'
   - On error: set status to 'error' with error message
6. Render different UI based on status:
   - **Verifying**: Show loading spinner with "Verifying your email..."
   - **Success**: Show success icon + message "Email verified successfully! You can now log in."
     - Include button to navigate to login page
   - **Error**: Show error icon + error message
     - Include link to resend verification page
7. Use PrimeVue components:
   - `ProgressSpinner` for loading
   - `Message` for success/error states
   - `Button` for actions
8. Centered card layout with Tailwind

**Dependencies**:

- `import { ref, onMounted } from 'vue'`
- `import { useRouter, useRoute } from 'vue-router'`
- `import Card from 'primevue/card'`
- `import Message from 'primevue/message'`
- `import ProgressSpinner from 'primevue/progressspinner'`
- `import Button from 'primevue/button'`
- `import { useAuth } from '@/composables/useAuth'`

**Implementation Notes**:

- Token is extracted from URL query parameter (e.g., `/verify-email?token=abc123`)
- Verification happens automatically on page load
- Clear visual feedback for each state
- Users should not stay on this page long - redirect after success
- Handle edge case: user navigates directly without token

---

### Step 7: Create Resend Verification Page

**File**: `src/Abuvi.Web/src/pages/auth/ResendVerificationPage.vue`

**Action**: Create page for users to request a new verification email

**Implementation Steps**:

1. Create `ResendVerificationPage.vue` in `pages/auth/`
2. Use `useAuth()` composable
3. Define local state:
   - `email`: string (form input)
   - `emailError`: string | null (validation error)
   - `showSuccess`: boolean (show success message after sending)
4. Implement `validateEmail()`: check required, valid format
5. Implement `handleSubmit()`:
   - Validate email
   - Call `resendVerification({ email })`
   - On success: show success message "Verification email sent! Please check your inbox."
   - On error: display error from composable
6. Form layout:
   - Email input field with validation
   - Submit button (disabled while loading)
   - Link back to login page
7. Use PrimeVue components:
   - `InputText` for email
   - `Button` for submit
   - `Message` for success/error feedback
8. Centered card with Tailwind

**Dependencies**:

- `import { ref } from 'vue'`
- `import Card from 'primevue/card'`
- `import InputText from 'primevue/inputtext'`
- `import Button from 'primevue/button'`
- `import Message from 'primevue/message'`
- `import { useAuth } from '@/composables/useAuth'`

**Implementation Notes**:

- Simple single-field form
- Clear feedback: users should know email was sent successfully
- Handle "email already verified" error gracefully
- Suggest login if user's email is already verified
- Rate limiting should be handled by backend (future)

---

### Step 8: Update Router Configuration

**File**: `src/Abuvi.Web/src/router/index.ts`

**Action**: Add routes for authentication pages

**Implementation Steps**:

1. Import the new page components
2. Add routes for:
   - `/register` → `RegisterPage`
   - `/verify-email` → `VerifyEmailPage`
   - `/resend-verification` → `ResendVerificationPage`
3. All routes are public (no `meta.requiresAuth`)
4. Use lazy loading with dynamic imports for code splitting:

   ```typescript
   {
     path: '/register',
     name: 'register',
     component: () => import('@/pages/auth/RegisterPage.vue')
   }
   ```

**Dependencies**:

- None (routes use lazy imports)

**Implementation Notes**:

- Routes are public and do not require authentication
- Use kebab-case for route paths (`/verify-email`, not `/verifyEmail`)
- Route names use camelCase (`name: 'verifyEmail'`)
- Lazy loading improves initial bundle size

**Updated Router Code**:

```typescript
import { createRouter, createWebHistory } from 'vue-router'
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
      path: '/register',
      name: 'register',
      component: () => import('@/pages/auth/RegisterPage.vue')
    },
    {
      path: '/verify-email',
      name: 'verifyEmail',
      component: () => import('@/pages/auth/VerifyEmailPage.vue')
    },
    {
      path: '/resend-verification',
      name: 'resendVerification',
      component: () => import('@/pages/auth/ResendVerificationPage.vue')
    }
  ]
})

export default router
```

---

### Step 9: Write Vitest Unit Tests for Composable

**File**: `src/Abuvi.Web/src/composables/__tests__/useAuth.test.ts`

**Action**: Write comprehensive unit tests for the `useAuth` composable

**Implementation Steps**:

1. Create `__tests__/` directory in `composables/`
2. Create `useAuth.test.ts`
3. Mock the `api` instance from `@/utils/api`
4. Write tests for each method:

   **registerUser tests**:
   - ✅ Should register user successfully
   - ✅ Should handle EMAIL_EXISTS error
   - ✅ Should handle DOCUMENT_EXISTS error
   - ✅ Should handle validation errors
   - ✅ Should set loading state correctly

   **verifyEmail tests**:
   - ✅ Should verify email successfully
   - ✅ Should handle invalid token error (NOT_FOUND)
   - ✅ Should handle expired token error (VERIFICATION_FAILED)
   - ✅ Should set loading state correctly

   **resendVerification tests**:
   - ✅ Should resend verification successfully
   - ✅ Should handle email not found error
   - ✅ Should handle already verified error (RESEND_FAILED)
   - ✅ Should set loading state correctly

5. Use Vitest's `vi.mock()` to mock axios
6. Follow AAA pattern (Arrange-Act-Assert)
7. Use descriptive test names: `should [expected behavior] when [condition]`

**Dependencies**:

- `import { describe, it, expect, vi, beforeEach } from 'vitest'`
- `import { useAuth } from '../useAuth'`
- `import { api } from '@/utils/api'`

**Implementation Notes**:

- Mock successful responses with `ApiResponse<T>` structure
- Mock error responses with backend error codes
- Test both happy paths and error cases
- Verify loading state transitions
- Ensure error messages are user-friendly

**Example Test**:

```typescript
describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('registerUser', () => {
    it('should register user successfully', async () => {
      // Arrange
      const mockUser = {
        id: '123',
        email: 'test@example.com',
        firstName: 'John',
        lastName: 'Doe',
        role: 'Member' as const,
        isActive: false,
        emailVerified: false,
        createdAt: '2026-02-12T00:00:00Z',
        updatedAt: '2026-02-12T00:00:00Z'
      }
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      })

      // Act
      const { registerUser, loading, error } = useAuth()
      const result = await registerUser({
        email: 'test@example.com',
        password: 'Test123!@#',
        firstName: 'John',
        lastName: 'Doe',
        acceptedTerms: true
      })

      // Assert
      expect(result).toEqual(mockUser)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.post).toHaveBeenCalledWith('/auth/register-user', expect.any(Object))
    })

    it('should handle EMAIL_EXISTS error', async () => {
      // Arrange
      vi.mocked(api.post).mockRejectedValue({
        response: {
          data: {
            success: false,
            error: { code: 'EMAIL_EXISTS', message: 'Email exists' }
          }
        }
      })

      // Act
      const { registerUser, error } = useAuth()
      const result = await registerUser({
        email: 'existing@example.com',
        password: 'Test123!@#',
        firstName: 'John',
        lastName: 'Doe',
        acceptedTerms: true
      })

      // Assert
      expect(result).toBeNull()
      expect(error.value).toBe('An account with this email already exists')
    })
  })
})
```

---

### Step 10: Write Vitest Component Tests

**Files**:

- `src/Abuvi.Web/src/components/auth/__tests__/PasswordStrengthMeter.test.ts`
- `src/Abuvi.Web/src/components/auth/__tests__/RegistrationForm.test.ts`

**Action**: Write component tests using Vue Test Utils

**Implementation Steps**:

**PasswordStrengthMeter tests**:

1. ✅ Should show "Weak" for password with only lowercase letters
2. ✅ Should show "Good" for password meeting most criteria
3. ✅ Should show "Strong" for password meeting all criteria
4. ✅ Should show missing criteria feedback
5. ✅ Should update strength in real-time as password prop changes

**RegistrationForm tests**:

1. ✅ Should render all form fields
2. ✅ Should validate required fields
3. ✅ Should validate email format
4. ✅ Should validate password strength
5. ✅ Should validate document number format (uppercase alphanumeric)
6. ✅ Should validate phone format (E.164)
7. ✅ Should require terms acceptance
8. ✅ Should emit submit event with form data when valid
9. ✅ Should not submit when form is invalid
10. ✅ Should emit cancel event when cancel button clicked

**Dependencies**:

- `import { describe, it, expect } from 'vitest'`
- `import { mount } from '@vue/test-utils'`
- Component imports

**Implementation Notes**:

- Use `mount()` from Vue Test Utils
- Simulate user input with `setValue()` and `trigger()`
- Test emit events with `wrapper.emitted()`
- Test validation error display
- Use data-testid attributes for reliable element selection

---

### Step 11: Write Cypress E2E Tests

**File**: `src/Abuvi.Web/cypress/e2e/registration.cy.ts`

**Action**: Write end-to-end tests for the complete registration flow

**Implementation Steps**:

1. Create `registration.cy.ts` in `cypress/e2e/`
2. Write the following test scenarios:

   **Happy Path - Complete Registration Flow**:
   - Visit `/register`
   - Fill out registration form with valid data
   - Submit form
   - Verify success message appears
   - Verify "check your email" message

   **Email Verification Flow**:
   - Visit `/verify-email?token=valid-token` (mock API response)
   - Verify success message appears
   - Verify "Login" button is visible
   - Click login button → redirects to login page

   **Resend Verification Flow**:
   - Visit `/resend-verification`
   - Enter email address
   - Submit form
   - Verify success message appears

   **Validation Error Handling**:
   - Visit `/register`
   - Submit empty form
   - Verify validation errors are displayed
   - Fill invalid email → verify email error
   - Fill weak password → verify password strength feedback

   **API Error Handling**:
   - Mock API error response (EMAIL_EXISTS)
   - Submit registration form
   - Verify error message is displayed

3. Use Cypress intercept for API mocking:

   ```typescript
   cy.intercept('POST', '/api/auth/register-user', {
     statusCode: 200,
     body: { success: true, data: mockUser }
   }).as('register')
   ```

4. Use `data-testid` attributes for element selection
5. Test responsive design (mobile, tablet, desktop viewports)

**Dependencies**:

- Cypress (already installed)

**Implementation Notes**:

- Mock API responses to avoid dependency on backend
- Test both success and error paths
- Verify user feedback (messages, loading states)
- Test keyboard navigation and accessibility
- Use `cy.intercept()` to mock API calls
- Clean up state between tests

**Example E2E Test**:

```typescript
describe('User Registration', () => {
  beforeEach(() => {
    cy.visit('/register')
  })

  it('should complete registration successfully', () => {
    cy.intercept('POST', '/api/auth/register-user', {
      statusCode: 200,
      body: {
        success: true,
        data: {
          id: '123',
          email: 'test@example.com',
          firstName: 'John',
          lastName: 'Doe',
          role: 'Member',
          isActive: false,
          emailVerified: false,
          createdAt: '2026-02-12T00:00:00Z',
          updatedAt: '2026-02-12T00:00:00Z'
        }
      }
    }).as('register')

    cy.get('[data-testid="email-input"]').type('test@example.com')
    cy.get('[data-testid="password-input"]').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="terms-checkbox"]').check()
    cy.get('[data-testid="submit-button"]').click()

    cy.wait('@register')
    cy.get('[data-testid="success-message"]').should('be.visible')
    cy.get('[data-testid="success-message"]').should('contain', 'check your email')
  })

  it('should show validation errors for invalid input', () => {
    cy.get('[data-testid="submit-button"]').click()
    cy.get('[data-testid="email-error"]').should('be.visible')
    cy.get('[data-testid="password-error"]').should('be.visible')
    cy.get('[data-testid="firstName-error"]').should('be.visible')
    cy.get('[data-testid="lastName-error"]').should('be.visible')
    cy.get('[data-testid="terms-error"]').should('be.visible')
  })

  it('should handle EMAIL_EXISTS error', () => {
    cy.intercept('POST', '/api/auth/register-user', {
      statusCode: 400,
      body: {
        success: false,
        error: {
          code: 'EMAIL_EXISTS',
          message: 'An account with this email already exists'
        }
      }
    }).as('registerError')

    cy.get('[data-testid="email-input"]').type('existing@example.com')
    cy.get('[data-testid="password-input"]').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="terms-checkbox"]').check()
    cy.get('[data-testid="submit-button"]').click()

    cy.wait('@registerError')
    cy.get('[data-testid="error-message"]').should('contain', 'email already exists')
  })
})
```

---

### Step 12: Update Technical Documentation

**Action**: Review and update technical documentation according to changes made

**Implementation Steps**:

1. **Review Changes**: Analyze all code changes made during implementation:
   - New authentication pages (RegisterPage, VerifyEmailPage, ResendVerificationPage)
   - New components (RegistrationForm, PasswordStrengthMeter)
   - New composable (useAuth)
   - New types (auth.ts)
   - New routes (/register, /verify-email, /resend-verification)

2. **Identify Documentation Files**: Determine which documentation files need updates:
   - ✅ `ai-specs/specs/api-endpoints.md` - No changes needed (API already documented)
   - ✅ `ai-specs/specs/frontend-standards.mdc` - May need updates if new patterns introduced
   - ✅ `ai-specs/specs/development_guide.md` - Add registration flow documentation
   - ❌ No routing documentation exists yet (note for future)

3. **Update Documentation**: For each affected file:

   **ai-specs/specs/development_guide.md**:
   - Add section: "User Registration Flow"
   - Document the three-step registration process:
     1. User fills registration form
     2. User verifies email via token
     3. User can resend verification if needed
   - Include component hierarchy diagram
   - Document key validation rules
   - Link to API endpoints documentation

   **ai-specs/specs/frontend-standards.mdc** (if needed):
   - Add example of password validation pattern if it's a reusable pattern
   - Document email verification flow pattern if reusable
   - Add notes about URL query parameter handling pattern

4. **Verify Documentation**:
   - Confirm all changes are accurately reflected
   - Check that documentation follows established structure
   - Ensure all content is in English
   - Verify code examples are correct

5. **Report Updates**: Document which files were updated and what changes were made

**References**:

- Follow process described in `ai-specs/specs/documentation-standards.mdc`
- All documentation must be written in English

**Notes**: This step is MANDATORY before considering the implementation complete. Documentation ensures maintainability and knowledge transfer.

---

## Implementation Order

Execute steps in this exact sequence:

1. **Step 0**: Create Feature Branch (`feature/user-registration-workflow-frontend`)
2. **Step 1**: Define TypeScript Interfaces (`types/auth.ts`)
3. **Step 2**: Create Authentication Composable (`composables/useAuth.ts`)
4. **Step 3**: Create Password Strength Meter Component (`components/auth/PasswordStrengthMeter.vue`)
5. **Step 4**: Create Registration Form Component (`components/auth/RegistrationForm.vue`)
6. **Step 5**: Create Registration Page (`pages/auth/RegisterPage.vue`)
7. **Step 6**: Create Email Verification Page (`pages/auth/VerifyEmailPage.vue`)
8. **Step 7**: Create Resend Verification Page (`pages/auth/ResendVerificationPage.vue`)
9. **Step 8**: Update Router Configuration (`router/index.ts`)
10. **Step 9**: Write Vitest Unit Tests for Composable (`composables/__tests__/useAuth.test.ts`)
11. **Step 10**: Write Vitest Component Tests (`components/auth/__tests__/*.test.ts`)
12. **Step 11**: Write Cypress E2E Tests (`cypress/e2e/registration.cy.ts`)
13. **Step 12**: Update Technical Documentation

---

## Testing Checklist

After implementation, verify the following:

### Functionality

- ✅ Registration form accepts all required fields
- ✅ Password strength meter shows real-time feedback
- ✅ Form validation works for all fields (email, password, names, phone, document, terms)
- ✅ Registration API call succeeds with valid data
- ✅ Success message appears after registration
- ✅ Email verification works with valid token
- ✅ Email verification fails gracefully with invalid/expired token
- ✅ Resend verification sends new email
- ✅ All error states display user-friendly messages

### Error Handling

- ✅ EMAIL_EXISTS error shows appropriate message
- ✅ DOCUMENT_EXISTS error shows appropriate message
- ✅ Invalid token error handled in verification page
- ✅ Expired token error handled in verification page
- ✅ Network errors display generic error message
- ✅ Validation errors display inline below fields

### UI/UX

- ✅ Forms are responsive on mobile, tablet, desktop
- ✅ Loading states show spinner/disabled buttons
- ✅ Success states show confirmation messages
- ✅ Error states show error messages with retry options
- ✅ Password field has show/hide toggle
- ✅ Form fields have proper labels and placeholders
- ✅ Required fields marked with asterisk (*)
- ✅ Terms checkbox is clearly visible

### Testing Coverage

- ✅ Vitest unit tests pass for composable (all methods)
- ✅ Vitest component tests pass for all components
- ✅ Cypress E2E tests pass for all flows (happy path + error cases)
- ✅ Test coverage meets 90% threshold

### Accessibility

- ✅ Form fields have associated labels
- ✅ Error messages have proper ARIA attributes
- ✅ Keyboard navigation works (tab through fields)
- ✅ Focus indicators visible
- ✅ Screen reader compatible

### Integration

- ✅ API calls use correct endpoints from `useAuth` composable
- ✅ Router navigation works correctly between pages
- ✅ URL query parameters extracted correctly (verify-email)
- ✅ Form data passed correctly between components

---

## Error Handling Patterns

### Composable Error Handling

```typescript
// useAuth.ts error handling pattern
try {
  const response = await api.post<ApiResponse<User>>('/auth/register-user', request)
  return response.data.data
} catch (err: any) {
  const errorCode = err.response?.data?.error?.code

  // Map backend error codes to user-friendly messages
  switch (errorCode) {
    case 'EMAIL_EXISTS':
      error.value = 'An account with this email already exists'
      break
    case 'DOCUMENT_EXISTS':
      error.value = 'An account with this document number already exists'
      break
    case 'VALIDATION_ERROR':
      // Extract validation details if available
      const details = err.response?.data?.error?.details
      error.value = details ? formatValidationErrors(details) : 'Please check your input'
      break
    default:
      error.value = 'An error occurred. Please try again.'
  }

  return null
} finally {
  loading.value = false
}
```

### Component Error Display

```vue
<!-- Error message display in components -->
<Message v-if="error" severity="error" :closable="false">
  {{ error }}
</Message>

<!-- Inline validation errors -->
<small v-if="errors.email" class="text-red-500">
  {{ errors.email }}
</small>
```

### Loading State Management

```vue
<!-- Disable submit button while loading -->
<Button
  type="submit"
  label="Register"
  :loading="loading"
  :disabled="loading || !isFormValid"
/>

<!-- Show spinner during async operations -->
<ProgressSpinner v-if="loading" />
```

---

## UI/UX Considerations

### PrimeVue Component Usage

- **Forms**: `InputText`, `Password`, `Checkbox`, `Button`
- **Feedback**: `Message` (error/success), `ProgressSpinner` (loading)
- **Layout**: `Card` (page containers)
- **Password**: `Password` component with built-in toggle visibility

### Tailwind CSS Layout

```vue
<!-- Responsive centered form layout -->
<div class="flex min-h-screen items-center justify-center bg-gray-50 p-4">
  <Card class="w-full max-w-md">
    <!-- Form content -->
  </Card>
</div>

<!-- Form field layout -->
<div class="flex flex-col gap-4">
  <div>
    <label class="mb-1 block text-sm font-medium">Email *</label>
    <InputText class="w-full" />
    <small class="text-red-500">Error message</small>
  </div>
</div>
```

### Responsive Design

- Mobile-first approach using Tailwind breakpoints
- Form: Single column on mobile, may use grid on larger screens for some fields
- Centered card layout with max-width constraint
- Touch-friendly button sizes (min height 44px)

### Accessibility

- All form fields have associated `<label>` elements
- Required fields marked with asterisk (*)
- Error messages associated with fields via ARIA
- Keyboard navigation support (tab order)
- Focus indicators visible
- Semantic HTML elements (`<main>`, `<form>`, `<button>`)

### Loading States

- Submit button shows loading spinner and disables
- Full page spinner for verification page
- Clear "Verifying..." text for user feedback

### User Feedback

- **Success**: Green PrimeVue Message with checkmark icon
- **Error**: Red PrimeVue Message with X icon
- **Info**: Blue Message for informational content
- Clear, concise messages in English

---

## Dependencies

### NPM Packages (Already Installed)

All required packages are already in `package.json`:

- `vue@^3.5.13` - Core framework
- `vue-router@^4.4.5` - Routing
- `pinia@^2.2.8` - State management (for future auth store)
- `primevue@^4.2.2` - UI component library
- `primeicons@^7.0.0` - PrimeVue icons
- `axios@^1.7.9` - HTTP client
- `tailwindcss@^3.4.17` - Utility-first CSS
- `vitest@^2.1.8` - Unit testing
- `@vue/test-utils@^2.4.6` - Component testing utilities
- `cypress@^13.17.0` - E2E testing

### PrimeVue Components Used

- `Button` - Form submissions, actions
- `InputText` - Text input fields
- `Password` - Password field with toggle
- `Checkbox` - Terms acceptance
- `Card` - Page containers
- `Message` - Success/error feedback
- `ProgressSpinner` - Loading states
- `ProgressBar` - Password strength meter

### Internal Dependencies

- `@/utils/api` - Configured Axios instance
- `@/types/api` - `ApiResponse<T>`, `ApiError` interfaces

---

## Notes

### Important Reminders

1. **English Only**: All code, comments, and user-facing text must be in English
2. **TypeScript Strict**: Use strict typing, no `any` types
3. **Composition API**: All components use `<script setup lang="ts">` - no Options API
4. **No Custom Styles**: Use Tailwind utility classes exclusively, avoid `<style>` blocks
5. **Composables for API**: Never call API directly from components, always use composables
6. **Loading States**: Always show loading feedback during async operations
7. **Error Handling**: Map backend error codes to user-friendly messages
8. **Accessibility**: Follow WCAG 2.1 AA standards

### Business Rules

1. **Password Requirements**:
   - Minimum 8 characters
   - At least one uppercase letter
   - At least one lowercase letter
   - At least one digit
   - At least one special character (@$!%*?&#)

2. **Email Verification**:
   - User must verify email before logging in
   - Verification token expires after 24 hours
   - Token is single-use (deleted after verification)

3. **Optional Fields**:
   - Document number (uppercase alphanumeric only)
   - Phone (E.164 format)

4. **Required Fields**:
   - Email, password, first name, last name, terms acceptance

### Backend API Base URL

- **Development**: `http://localhost:5079/api` (configured in `.env.development`)
- Update `VITE_API_URL` environment variable if backend port changes

### Email Service Note

The backend currently logs verification emails to console (Resend integration pending). For testing, verification tokens must be extracted from API logs.

---

## Next Steps After Implementation

1. **Manual Testing**: Test registration flow end-to-end with running backend
2. **Code Review**: Submit PR for team review
3. **Integration Testing**: Test with real email service when Resend is integrated
4. **User Acceptance**: Get feedback on UX/flow from stakeholders
5. **Login Feature**: Implement login page to complete auth flow
6. **Auth Store**: Create Pinia auth store for session management
7. **Protected Routes**: Add route guards for authenticated pages

---

## Implementation Verification

Before marking this feature as complete, verify:

### Code Quality

- ✅ All files use TypeScript with strict mode
- ✅ No `any` types used
- ✅ All components use `<script setup lang="ts">`
- ✅ All text in English (code, comments, UI)
- ✅ No `<style>` blocks - Tailwind only
- ✅ ESLint passes with no errors
- ✅ Prettier formatting applied

### Functionality

- ✅ Registration form works with all fields
- ✅ Email verification page works with token
- ✅ Resend verification page works
- ✅ All API calls go through `useAuth` composable
- ✅ Router navigation works correctly
- ✅ Loading states display properly
- ✅ Error states display user-friendly messages

### Testing

- ✅ Vitest tests pass: `npm run test`
- ✅ Vitest coverage meets 90% threshold: `npm run test:coverage`
- ✅ Cypress tests pass: `npm run cypress:run`
- ✅ Manual testing completed with backend running

### Integration

- ✅ Composable connects to correct API endpoints
- ✅ Types match backend DTOs
- ✅ Error codes handled correctly
- ✅ API responses parsed correctly

### Documentation

- ✅ Technical documentation updated
- ✅ Code comments added where necessary
- ✅ README updated (if needed)

---

## Summary

This implementation plan provides complete details for building the user registration workflow frontend feature. The plan follows Vue 3 Composition API best practices, uses PrimeVue for UI components, Tailwind CSS for styling, and includes comprehensive testing with Vitest and Cypress.

Key deliverables:

- 3 pages (Register, Verify Email, Resend Verification)
- 2 reusable components (RegistrationForm, PasswordStrengthMeter)
- 1 composable (useAuth) for API communication
- TypeScript types matching backend contracts
- Router configuration with lazy loading
- Comprehensive unit and E2E tests
- Updated technical documentation

The feature integrates seamlessly with the existing backend API and sets the foundation for future authentication features (login, logout, protected routes).

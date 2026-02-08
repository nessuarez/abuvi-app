# Frontend Implementation Plan: Phase 1 User CRUD

## Overview

This document details the frontend implementation for Phase 1 User CRUD functionality. The implementation follows Vue 3 Composition API patterns with `<script setup lang="ts">`, PrimeVue components for UI, Tailwind CSS for styling, and composable-based architecture for API communication.

**Backend Context**: Phase 1 backend implements User entity CRUD endpoints without authentication. Authentication will be added in Phase 2. For now, all endpoints are publicly accessible.

**Frontend Goal**: Create a complete user management interface with list, create, edit views following established Vue 3 architectural patterns.

## Architecture Context

### Components & Files Involved

**New Files to Create:**
- `frontend/src/types/user.ts` - User type definitions
- `frontend/src/composables/useUsers.ts` - User API communication composable
- `frontend/src/components/users/UserCard.vue` - User card component
- `frontend/src/components/users/UserForm.vue` - User create/edit form
- `frontend/src/pages/UsersPage.vue` - User list page
- `frontend/src/pages/UserDetailPage.vue` - User detail page
- `frontend/src/composables/__tests__/useUsers.test.ts` - Unit tests for composable
- `frontend/src/components/users/__tests__/UserCard.test.ts` - Component tests
- `frontend/src/components/users/__tests__/UserForm.test.ts` - Component tests
- `frontend/cypress/e2e/users.cy.ts` - E2E tests

**Files to Modify:**
- `frontend/src/router/index.ts` - Add user management routes
- `frontend/src/types/api.ts` - Ensure ApiResponse types exist (may already exist)

### State Management Approach

- **No Pinia Store needed** for Phase 1 - users will be managed via composable with local reactive state
- **Local component state** with `ref()` / `reactive()` for form data
- **Composable pattern** for API calls and shared user state

### Routing Considerations

New routes to add:
- `/users` - User list page
- `/users/:id` - User detail page

**Note**: No authentication guards for Phase 1. Authentication will be added in Phase 2.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch following the development workflow
- **Branch Naming**: `feature/phase1-user-crud-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `main` branch
  2. Pull latest changes: `git pull origin main`
  3. Check if branch exists: `git branch -a | grep phase1-user-crud-frontend`
  4. If branch doesn't exist, create it: `git checkout -b feature/phase1-user-crud-frontend`
  5. If branch exists, switch to it: `git checkout feature/phase1-user-crud-frontend`
  6. Verify branch: `git branch` (should show `* feature/phase1-user-crud-frontend`)
- **Notes**:
  - This MUST be the FIRST step before any code changes
  - Use separate frontend branch to isolate frontend concerns from backend branch
  - Refer to `ai-specs/specs/frontend-standards.mdc` section "Development Workflow"

---

### Step 1: Define TypeScript Interfaces

- **File**: `frontend/src/types/user.ts`
- **Action**: Create TypeScript interfaces for User domain types matching backend DTOs
- **Implementation Steps**:
  1. Create `frontend/src/types/user.ts` file
  2. Define `User` interface matching backend UserResponse
  3. Define `CreateUserRequest` interface matching backend CreateUserRequest
  4. Define `UpdateUserRequest` interface matching backend UpdateUserRequest
  5. Define `UserRole` type enum matching backend UserRole enum
  6. Export all types

- **Code**:
```typescript
/**
 * User domain types matching backend DTOs
 */

export type UserRole = 'Admin' | 'Board' | 'Member'

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  role: UserRole
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateUserRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone: string | null
  role: UserRole
}

export interface UpdateUserRequest {
  firstName: string
  lastName: string
  phone: string | null
  isActive: boolean
}
```

- **Dependencies**: None
- **Implementation Notes**:
  - Types mirror backend DTOs exactly for type consistency
  - `UserRole` uses string literals matching backend enum values
  - `id` is `string` (UUID from backend)
  - `phone` is nullable to match backend optional field
  - Dates are strings (ISO 8601 format from backend)

---

### Step 2: Verify/Create API Response Types

- **File**: `frontend/src/types/api.ts`
- **Action**: Ensure `ApiResponse<T>` types exist to match backend response envelope
- **Implementation Steps**:
  1. Check if `frontend/src/types/api.ts` exists
  2. If it doesn't exist, create it with `ApiResponse`, `ApiError`, and `ValidationError` types
  3. If it exists, verify it matches backend structure
  4. Ensure it's exported and can be imported by composables

- **Code** (if file doesn't exist):
```typescript
/**
 * API response types matching backend envelope structure
 */

export interface ApiResponse<T> {
  success: boolean
  data: T | null
  error: ApiError | null
}

export interface ApiError {
  message: string
  code: string
  details?: ValidationError[]
}

export interface ValidationError {
  field: string
  message: string
}
```

- **Dependencies**: None
- **Implementation Notes**:
  - Matches backend `ApiResponse<T>` wrapper
  - Generic type `T` allows type-safe responses
  - `ValidationError` for field-level validation errors

---

### Step 3: Configure Axios Instance (if not already configured)

- **File**: `frontend/src/utils/api.ts` (or `frontend/src/lib/axios.ts` depending on project structure)
- **Action**: Verify centralized Axios instance exists with base URL configuration
- **Implementation Steps**:
  1. Check if Axios instance configuration exists
  2. If not, create `frontend/src/utils/api.ts` with configured Axios instance
  3. Set base URL to backend API (`http://localhost:5079/api` for development)
  4. Configure headers for JSON content type
  5. Export `api` instance

- **Code** (if file doesn't exist):
```typescript
import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5079/api'

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Response interceptor for error handling
api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error)
    return Promise.reject(error)
  }
)
```

- **Dependencies**: `axios` (should already be installed)
- **Environment Variables**: `VITE_API_URL` in `.env.development` and `.env.production`
- **Implementation Notes**:
  - Centralized Axios configuration
  - Base URL from environment variable for different environments
  - Response interceptor for global error logging
  - Phase 2 will add request interceptor for JWT tokens

---

### Step 4: Create useUsers Composable

- **File**: `frontend/src/composables/useUsers.ts`
- **Action**: Create composable for user API communication and state management
- **Function Signature**:
```typescript
export function useUsers(): {
  users: Ref<User[]>
  selectedUser: Ref<User | null>
  loading: Ref<boolean>
  error: Ref<string | null>
  fetchUsers: () => Promise<void>
  fetchUserById: (id: string) => Promise<User | null>
  createUser: (request: CreateUserRequest) => Promise<User | null>
  updateUser: (id: string, request: UpdateUserRequest) => Promise<User | null>
  clearError: () => void
}
```

- **Implementation Steps**:
  1. Create `frontend/src/composables/useUsers.ts`
  2. Import necessary types (`User`, `CreateUserRequest`, `UpdateUserRequest`, `ApiResponse`)
  3. Import `api` instance from utils
  4. Define reactive state: `users`, `selectedUser`, `loading`, `error`
  5. Implement `fetchUsers()` - GET /api/users
  6. Implement `fetchUserById(id)` - GET /api/users/:id
  7. Implement `createUser(request)` - POST /api/users
  8. Implement `updateUser(id, request)` - PUT /api/users/:id
  9. Implement `clearError()` - reset error state
  10. Return all state and methods

- **Complete Implementation**:
```typescript
import { ref, type Ref } from 'vue'
import { api } from '@/utils/api'
import type { User, CreateUserRequest, UpdateUserRequest } from '@/types/user'
import type { ApiResponse } from '@/types/api'

export function useUsers() {
  const users = ref<User[]>([])
  const selectedUser = ref<User | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  /**
   * Fetch all users from the API
   */
  const fetchUsers = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<User[]>>('/users')
      users.value = response.data.data ?? []
    } catch (err) {
      error.value = 'Failed to load users. Please try again.'
      console.error('fetchUsers error:', err)
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetch a single user by ID
   */
  const fetchUserById = async (id: string): Promise<User | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<User>>(`/users/${id}`)
      if (response.data.data) {
        selectedUser.value = response.data.data
        return response.data.data
      }
      return null
    } catch (err: any) {
      if (err.response?.status === 404) {
        error.value = 'User not found.'
      } else {
        error.value = 'Failed to load user details.'
      }
      console.error('fetchUserById error:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Create a new user
   */
  const createUser = async (request: CreateUserRequest): Promise<User | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<User>>('/users', request)
      if (response.data.data) {
        users.value.push(response.data.data)
        return response.data.data
      }
      return null
    } catch (err: any) {
      if (err.response?.status === 409) {
        error.value = 'Email already exists. Please use a different email.'
      } else if (err.response?.data?.error?.details) {
        const details = err.response.data.error.details
        error.value = details.map((d: any) => d.message).join(', ')
      } else {
        error.value = 'Failed to create user. Please try again.'
      }
      console.error('createUser error:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Update an existing user
   */
  const updateUser = async (
    id: string,
    request: UpdateUserRequest
  ): Promise<User | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<User>>(`/users/${id}`, request)
      if (response.data.data) {
        // Update user in list
        const index = users.value.findIndex((u) => u.id === id)
        if (index !== -1) {
          users.value[index] = response.data.data
        }
        selectedUser.value = response.data.data
        return response.data.data
      }
      return null
    } catch (err: any) {
      if (err.response?.status === 404) {
        error.value = 'User not found.'
      } else if (err.response?.data?.error?.details) {
        const details = err.response.data.error.details
        error.value = details.map((d: any) => d.message).join(', ')
      } else {
        error.value = 'Failed to update user. Please try again.'
      }
      console.error('updateUser error:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Clear error state
   */
  const clearError = () => {
    error.value = null
  }

  return {
    users,
    selectedUser,
    loading,
    error,
    fetchUsers,
    fetchUserById,
    createUser,
    updateUser,
    clearError
  }
}
```

- **Dependencies**:
  - `vue` (ref, Ref)
  - `@/utils/api` (axios instance)
  - `@/types/user` (User, CreateUserRequest, UpdateUserRequest)
  - `@/types/api` (ApiResponse)

- **Implementation Notes**:
  - Composable manages loading, error, and data states
  - Error messages are user-friendly and specific (409 Conflict, 404 Not Found, validation errors)
  - `createUser` adds new user to local `users` array on success
  - `updateUser` updates user in local array
  - All API calls use configured axios instance
  - Returns reactive refs and methods for component consumption

---

### Step 5: Create UserCard Component

- **File**: `frontend/src/components/users/UserCard.vue`
- **Action**: Create reusable user card component for displaying user information
- **Component Props**:
```typescript
interface Props {
  user: User
  selected?: boolean
}
```

- **Component Emits**:
```typescript
{
  select: [user: User]
}
```

- **Implementation Steps**:
  1. Create `frontend/src/components/users/UserCard.vue`
  2. Define props with TypeScript
  3. Define emits
  4. Create template with PrimeVue Card and Tailwind styling
  5. Display user name, email, role, and active status
  6. Add click handler to emit select event
  7. Add visual indicator for selected state
  8. Use role-specific badges with color coding

- **Complete Implementation**:
```vue
<script setup lang="ts">
import Card from 'primevue/card'
import Tag from 'primevue/tag'
import type { User } from '@/types/user'

interface Props {
  user: User
  selected?: boolean
}

defineProps<Props>()

const emit = defineEmits<{
  select: [user: User]
}>()

const getRoleSeverity = (role: string): 'success' | 'info' | 'warning' => {
  switch (role) {
    case 'Admin':
      return 'success'
    case 'Board':
      return 'info'
    case 'Member':
      return 'warning'
    default:
      return 'info'
  }
}

const handleCardClick = (user: User) => {
  emit('select', user)
}
</script>

<template>
  <Card
    class="cursor-pointer transition-shadow hover:shadow-md"
    :class="{
      'ring-2 ring-primary-500': selected
    }"
    @click="handleCardClick(user)"
  >
    <template #title>
      <div class="flex items-center justify-between">
        <span>{{ user.firstName }} {{ user.lastName }}</span>
        <Tag :value="user.role" :severity="getRoleSeverity(user.role)" />
      </div>
    </template>
    <template #content>
      <div class="space-y-2 text-sm">
        <div class="flex items-center gap-2">
          <i class="pi pi-envelope text-gray-500" />
          <span class="text-gray-700">{{ user.email }}</span>
        </div>
        <div v-if="user.phone" class="flex items-center gap-2">
          <i class="pi pi-phone text-gray-500" />
          <span class="text-gray-700">{{ user.phone }}</span>
        </div>
        <div class="flex items-center gap-2">
          <i
            class="pi text-gray-500"
            :class="user.isActive ? 'pi-check-circle' : 'pi-times-circle'"
          />
          <span class="text-gray-700">
            {{ user.isActive ? 'Active' : 'Inactive' }}
          </span>
        </div>
      </div>
    </template>
  </Card>
</template>
```

- **Dependencies**:
  - PrimeVue: `Card`, `Tag`
  - `@/types/user` (User type)

- **Implementation Notes**:
  - Uses PrimeVue Card for structure
  - Role badges with color coding (Admin=success/green, Board=info/blue, Member=warning/orange)
  - Icons from PrimeIcons
  - Responsive and accessible (keyboard navigation via Card)
  - Visual feedback for selection with ring border
  - Emits select event on click for parent components to handle navigation

---

### Step 6: Create UserForm Component

- **File**: `frontend/src/components/users/UserForm.vue`
- **Action**: Create user form component for create and edit operations
- **Component Props**:
```typescript
interface Props {
  user?: User | null
  mode: 'create' | 'edit'
  loading?: boolean
}
```

- **Component Emits**:
```typescript
{
  submit: [data: CreateUserRequest | UpdateUserRequest]
  cancel: []
}
```

- **Implementation Steps**:
  1. Create `frontend/src/components/users/UserForm.vue`
  2. Define props (user for edit mode, mode flag, loading state)
  3. Define emits (submit, cancel)
  4. Create reactive form data with `reactive()`
  5. Implement form validation
  6. Create template with PrimeVue form components (InputText, Dropdown, InputSwitch)
  7. Handle create vs edit mode differences (password field only in create mode)
  8. Implement submit handler
  9. Add cancel button
  10. Display validation errors

- **Complete Implementation**:
```vue
<script setup lang="ts">
import { reactive, computed, watch } from 'vue'
import InputText from 'primevue/inputtext'
import Dropdown from 'primevue/dropdown'
import InputSwitch from 'primevue/inputswitch'
import Button from 'primevue/button'
import type { User, CreateUserRequest, UpdateUserRequest, UserRole } from '@/types/user'

interface Props {
  user?: User | null
  mode: 'create' | 'edit'
  loading?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  user: null,
  loading: false
})

const emit = defineEmits<{
  submit: [data: CreateUserRequest | UpdateUserRequest]
  cancel: []
}>()

const formData = reactive({
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  phone: '',
  role: 'Member' as UserRole,
  isActive: true
})

const errors = reactive<Record<string, string>>({})

const roleOptions = [
  { label: 'Member', value: 'Member' },
  { label: 'Board', value: 'Board' },
  { label: 'Admin', value: 'Admin' }
]

// Initialize form data in edit mode
watch(
  () => props.user,
  (user) => {
    if (user && props.mode === 'edit') {
      formData.email = user.email
      formData.firstName = user.firstName
      formData.lastName = user.lastName
      formData.phone = user.phone ?? ''
      formData.role = user.role
      formData.isActive = user.isActive
    }
  },
  { immediate: true }
)

const validate = (): boolean => {
  Object.keys(errors).forEach((key) => delete errors[key])

  if (props.mode === 'create') {
    if (!formData.email.trim()) {
      errors.email = 'Email is required'
    } else if (!formData.email.includes('@')) {
      errors.email = 'Email must be valid'
    }

    if (!formData.password.trim()) {
      errors.password = 'Password is required'
    } else if (formData.password.length < 8) {
      errors.password = 'Password must be at least 8 characters'
    }
  }

  if (!formData.firstName.trim()) {
    errors.firstName = 'First name is required'
  }

  if (!formData.lastName.trim()) {
    errors.lastName = 'Last name is required'
  }

  return Object.keys(errors).length === 0
}

const handleSubmit = () => {
  if (!validate()) return

  if (props.mode === 'create') {
    const request: CreateUserRequest = {
      email: formData.email.trim(),
      password: formData.password,
      firstName: formData.firstName.trim(),
      lastName: formData.lastName.trim(),
      phone: formData.phone.trim() || null,
      role: formData.role
    }
    emit('submit', request)
  } else {
    const request: UpdateUserRequest = {
      firstName: formData.firstName.trim(),
      lastName: formData.lastName.trim(),
      phone: formData.phone.trim() || null,
      isActive: formData.isActive
    }
    emit('submit', request)
  }
}

const handleCancel = () => {
  emit('cancel')
}

const isFormValid = computed(() => {
  if (props.mode === 'create') {
    return (
      formData.email.trim().length > 0 &&
      formData.password.length >= 8 &&
      formData.firstName.trim().length > 0 &&
      formData.lastName.trim().length > 0
    )
  } else {
    return formData.firstName.trim().length > 0 && formData.lastName.trim().length > 0
  }
})
</script>

<template>
  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <!-- Email field (create mode only) -->
    <div v-if="mode === 'create'">
      <label for="email" class="mb-2 block text-sm font-medium">Email *</label>
      <InputText
        id="email"
        v-model="formData.email"
        type="email"
        class="w-full"
        :invalid="!!errors.email"
        placeholder="user@example.com"
      />
      <small v-if="errors.email" class="text-red-500">{{ errors.email }}</small>
    </div>

    <!-- Email display (edit mode) -->
    <div v-else>
      <label class="mb-2 block text-sm font-medium">Email</label>
      <p class="text-gray-700">{{ user?.email }}</p>
    </div>

    <!-- Password field (create mode only) -->
    <div v-if="mode === 'create'">
      <label for="password" class="mb-2 block text-sm font-medium">Password *</label>
      <InputText
        id="password"
        v-model="formData.password"
        type="password"
        class="w-full"
        :invalid="!!errors.password"
        placeholder="Minimum 8 characters"
      />
      <small v-if="errors.password" class="text-red-500">{{ errors.password }}</small>
    </div>

    <!-- First Name -->
    <div>
      <label for="firstName" class="mb-2 block text-sm font-medium">First Name *</label>
      <InputText
        id="firstName"
        v-model="formData.firstName"
        class="w-full"
        :invalid="!!errors.firstName"
        placeholder="John"
      />
      <small v-if="errors.firstName" class="text-red-500">{{ errors.firstName }}</small>
    </div>

    <!-- Last Name -->
    <div>
      <label for="lastName" class="mb-2 block text-sm font-medium">Last Name *</label>
      <InputText
        id="lastName"
        v-model="formData.lastName"
        class="w-full"
        :invalid="!!errors.lastName"
        placeholder="Doe"
      />
      <small v-if="errors.lastName" class="text-red-500">{{ errors.lastName }}</small>
    </div>

    <!-- Phone -->
    <div>
      <label for="phone" class="mb-2 block text-sm font-medium">Phone (optional)</label>
      <InputText
        id="phone"
        v-model="formData.phone"
        type="tel"
        class="w-full"
        placeholder="+34 123 456 789"
      />
    </div>

    <!-- Role (create mode only) -->
    <div v-if="mode === 'create'">
      <label for="role" class="mb-2 block text-sm font-medium">Role *</label>
      <Dropdown
        id="role"
        v-model="formData.role"
        :options="roleOptions"
        option-label="label"
        option-value="value"
        class="w-full"
        placeholder="Select a role"
      />
    </div>

    <!-- Active status (edit mode only) -->
    <div v-if="mode === 'edit'" class="flex items-center gap-3">
      <label for="isActive" class="text-sm font-medium">Active</label>
      <InputSwitch id="isActive" v-model="formData.isActive" />
    </div>

    <!-- Action buttons -->
    <div class="flex gap-3">
      <Button
        type="submit"
        :label="mode === 'create' ? 'Create User' : 'Update User'"
        :loading="loading"
        :disabled="!isFormValid || loading"
        class="flex-1"
      />
      <Button
        type="button"
        label="Cancel"
        severity="secondary"
        outlined
        @click="handleCancel"
        :disabled="loading"
        class="flex-1"
      />
    </div>
  </form>
</template>
```

- **Dependencies**:
  - PrimeVue: `InputText`, `Dropdown`, `InputSwitch`, `Button`
  - `@/types/user` (User, CreateUserRequest, UpdateUserRequest, UserRole)

- **Implementation Notes**:
  - Reactive form data with `reactive()`
  - Conditional fields based on mode (password and role only in create mode)
  - Real-time validation with error display
  - Email is read-only in edit mode (displayed as text)
  - Role dropdown with predefined options
  - Active toggle switch in edit mode
  - Form initialization from user prop in edit mode
  - Disabled submit button during loading
  - Computed `isFormValid` for basic client-side validation

---

### Step 7: Create UsersPage (List View)

- **File**: `frontend/src/pages/UsersPage.vue`
- **Action**: Create user list page with DataTable and create user dialog
- **Implementation Steps**:
  1. Create `frontend/src/pages/UsersPage.vue`
  2. Import and use `useUsers` composable
  3. Import `UserCard` component
  4. Create DataTable with PrimeVue
  5. Add "Create User" button that opens dialog
  6. Implement create user dialog with `UserForm`
  7. Add loading and error states
  8. Implement user selection navigation to detail page
  9. Fetch users on component mount

- **Complete Implementation**:
```vue
<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useUsers } from '@/composables/useUsers'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Tag from 'primevue/tag'
import UserForm from '@/components/users/UserForm.vue'
import type { CreateUserRequest } from '@/types/user'

const router = useRouter()
const { users, loading, error, fetchUsers, createUser, clearError } = useUsers()

const showCreateDialog = ref(false)
const creatingUser = ref(false)

onMounted(() => {
  fetchUsers()
})

const openCreateDialog = () => {
  showCreateDialog.value = true
  clearError()
}

const closeCreateDialog = () => {
  showCreateDialog.value = false
  clearError()
}

const handleCreateUser = async (data: CreateUserRequest) => {
  creatingUser.value = true
  const newUser = await createUser(data)
  creatingUser.value = false

  if (newUser) {
    closeCreateDialog()
    // Optional: Show success toast
  }
}

const viewUserDetail = (userId: string) => {
  router.push(`/users/${userId}`)
}

const getRoleSeverity = (role: string): 'success' | 'info' | 'warning' => {
  switch (role) {
    case 'Admin':
      return 'success'
    case 'Board':
      return 'info'
    case 'Member':
      return 'warning'
    default:
      return 'info'
  }
}

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleDateString('es-ES', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}
</script>

<template>
  <div class="container mx-auto p-4">
    <div class="mb-6 flex items-center justify-between">
      <h1 class="text-3xl font-bold text-gray-900">User Management</h1>
      <Button label="Create User" icon="pi pi-plus" @click="openCreateDialog" />
    </div>

    <!-- Loading state -->
    <div v-if="loading && users.length === 0" class="flex justify-center p-12">
      <ProgressSpinner />
    </div>

    <!-- Error state -->
    <Message v-else-if="error" severity="error" :closable="false" class="mb-4">
      {{ error }}
      <Button
        label="Retry"
        text
        size="small"
        class="ml-2"
        @click="fetchUsers"
      />
    </Message>

    <!-- Users DataTable -->
    <DataTable
      v-else
      :value="users"
      striped-rows
      paginator
      :rows="10"
      :rows-per-page-options="[5, 10, 20, 50]"
      class="rounded-lg"
      data-testid="users-table"
    >
      <Column field="firstName" header="Name" sortable>
        <template #body="{ data }">
          <span class="font-medium">{{ data.firstName }} {{ data.lastName }}</span>
        </template>
      </Column>
      <Column field="email" header="Email" sortable />
      <Column field="role" header="Role" sortable>
        <template #body="{ data }">
          <Tag :value="data.role" :severity="getRoleSeverity(data.role)" />
        </template>
      </Column>
      <Column field="phone" header="Phone">
        <template #body="{ data }">
          <span class="text-gray-600">{{ data.phone || '—' }}</span>
        </template>
      </Column>
      <Column field="isActive" header="Status" sortable>
        <template #body="{ data }">
          <Tag
            :value="data.isActive ? 'Active' : 'Inactive'"
            :severity="data.isActive ? 'success' : 'danger'"
          />
        </template>
      </Column>
      <Column field="createdAt" header="Created" sortable>
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ formatDate(data.createdAt) }}</span>
        </template>
      </Column>
      <Column header="Actions">
        <template #body="{ data }">
          <Button
            icon="pi pi-eye"
            text
            rounded
            aria-label="View Details"
            @click="viewUserDetail(data.id)"
          />
        </template>
      </Column>
    </DataTable>

    <!-- Create User Dialog -->
    <Dialog
      v-model:visible="showCreateDialog"
      header="Create New User"
      modal
      class="w-full max-w-md"
    >
      <UserForm
        mode="create"
        :loading="creatingUser"
        @submit="handleCreateUser"
        @cancel="closeCreateDialog"
      />
      <Message v-if="error" severity="error" :closable="false" class="mt-4">
        {{ error }}
      </Message>
    </Dialog>
  </div>
</template>
```

- **Dependencies**:
  - PrimeVue: `DataTable`, `Column`, `Button`, `Dialog`, `ProgressSpinner`, `Message`, `Tag`
  - Vue Router: `useRouter`
  - `@/composables/useUsers`
  - `@/components/users/UserForm`
  - `@/types/user`

- **Implementation Notes**:
  - DataTable with pagination, sorting, and filtering
  - Responsive layout with Tailwind
  - Loading state with spinner
  - Error state with retry button
  - Create user dialog with form
  - Row actions to view user detail
  - Role and status badges with color coding
  - Date formatting for user-friendly display
  - Keyboard-accessible actions

---

### Step 8: Create UserDetailPage (Detail/Edit View)

- **File**: `frontend/src/pages/UserDetailPage.vue`
- **Action**: Create user detail page with view and edit functionality
- **Implementation Steps**:
  1. Create `frontend/src/pages/UserDetailPage.vue`
  2. Import and use `useUsers` composable
  3. Import `UserForm` component
  4. Get user ID from route params
  5. Fetch user data on mount
  6. Display user details in read-only mode
  7. Add "Edit" button to toggle edit mode
  8. Show `UserForm` in edit mode
  9. Handle update submission
  10. Add back navigation

- **Complete Implementation**:
```vue
<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useUsers } from '@/composables/useUsers'
import Button from 'primevue/button'
import Card from 'primevue/card'
import Tag from 'primevue/tag'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import UserForm from '@/components/users/UserForm.vue'
import type { UpdateUserRequest } from '@/types/user'

const route = useRoute()
const router = useRouter()
const { selectedUser, loading, error, fetchUserById, updateUser, clearError } = useUsers()

const editMode = ref(false)
const updatingUser = ref(false)

const userId = computed(() => route.params.id as string)

onMounted(async () => {
  if (userId.value) {
    await fetchUserById(userId.value)
  }
})

const goBack = () => {
  router.push('/users')
}

const enableEditMode = () => {
  editMode.value = true
  clearError()
}

const cancelEdit = () => {
  editMode.value = false
  clearError()
}

const handleUpdateUser = async (data: UpdateUserRequest) => {
  if (!userId.value) return

  updatingUser.value = true
  const updated = await updateUser(userId.value, data)
  updatingUser.value = false

  if (updated) {
    editMode.value = false
    // Optional: Show success toast
  }
}

const getRoleSeverity = (role: string): 'success' | 'info' | 'warning' => {
  switch (role) {
    case 'Admin':
      return 'success'
    case 'Board':
      return 'info'
    case 'Member':
      return 'warning'
    default:
      return 'info'
  }
}

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleString('es-ES', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>

<template>
  <div class="container mx-auto max-w-3xl p-4">
    <div class="mb-6">
      <Button
        label="Back to Users"
        icon="pi pi-arrow-left"
        text
        @click="goBack"
        class="mb-4"
      />
      <h1 class="text-3xl font-bold text-gray-900">User Details</h1>
    </div>

    <!-- Loading state -->
    <div v-if="loading && !selectedUser" class="flex justify-center p-12">
      <ProgressSpinner />
    </div>

    <!-- Error state -->
    <Message v-else-if="error && !selectedUser" severity="error" :closable="false">
      {{ error }}
      <Button
        label="Go Back"
        text
        size="small"
        class="ml-2"
        @click="goBack"
      />
    </Message>

    <!-- User detail -->
    <div v-else-if="selectedUser">
      <!-- View mode -->
      <Card v-if="!editMode">
        <template #title>
          <div class="flex items-center justify-between">
            <span>{{ selectedUser.firstName }} {{ selectedUser.lastName }}</span>
            <Button
              label="Edit"
              icon="pi pi-pencil"
              @click="enableEditMode"
            />
          </div>
        </template>
        <template #content>
          <div class="space-y-4">
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Email</label>
                <p class="text-gray-900">{{ selectedUser.email }}</p>
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Role</label>
                <Tag :value="selectedUser.role" :severity="getRoleSeverity(selectedUser.role)" />
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Phone</label>
                <p class="text-gray-900">{{ selectedUser.phone || '—' }}</p>
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Status</label>
                <Tag
                  :value="selectedUser.isActive ? 'Active' : 'Inactive'"
                  :severity="selectedUser.isActive ? 'success' : 'danger'"
                />
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Created</label>
                <p class="text-sm text-gray-600">{{ formatDate(selectedUser.createdAt) }}</p>
              </div>
              <div>
                <label class="mb-1 block text-sm font-medium text-gray-600">Last Updated</label>
                <p class="text-sm text-gray-600">{{ formatDate(selectedUser.updatedAt) }}</p>
              </div>
            </div>
          </div>
        </template>
      </Card>

      <!-- Edit mode -->
      <Card v-else>
        <template #title>
          <span>Edit User</span>
        </template>
        <template #content>
          <UserForm
            mode="edit"
            :user="selectedUser"
            :loading="updatingUser"
            @submit="handleUpdateUser"
            @cancel="cancelEdit"
          />
          <Message v-if="error" severity="error" :closable="false" class="mt-4">
            {{ error }}
          </Message>
        </template>
      </Card>
    </div>
  </div>
</template>
```

- **Dependencies**:
  - PrimeVue: `Button`, `Card`, `Tag`, `ProgressSpinner`, `Message`
  - Vue Router: `useRoute`, `useRouter`
  - `@/composables/useUsers`
  - `@/components/users/UserForm`
  - `@/types/user`

- **Implementation Notes**:
  - Route param extraction for user ID
  - Toggle between view and edit modes
  - Read-only display with formatted data
  - Edit form with cancel functionality
  - Back navigation to user list
  - Loading and error states
  - Responsive grid layout
  - Date formatting for timestamps

---

### Step 9: Update Router Configuration

- **File**: `frontend/src/router/index.ts`
- **Action**: Add user management routes
- **Implementation Steps**:
  1. Open `frontend/src/router/index.ts`
  2. Import `UsersPage` and `UserDetailPage` with lazy loading
  3. Add `/users` route for user list
  4. Add `/users/:id` route for user detail
  5. No auth guards for Phase 1 (will be added in Phase 2)

- **Code Changes**:
```typescript
// Add to routes array
{
  path: '/users',
  name: 'users',
  component: () => import('@/pages/UsersPage.vue'),
  meta: {
    title: 'User Management'
  }
},
{
  path: '/users/:id',
  name: 'user-detail',
  component: () => import('@/pages/UserDetailPage.vue'),
  meta: {
    title: 'User Details'
  }
}
```

- **Dependencies**: None (lazy-loaded imports)
- **Implementation Notes**:
  - Lazy loading with `() => import()` for code splitting
  - No authentication guards for Phase 1
  - Meta title for page title management
  - Route name for programmatic navigation
  - Phase 2 will add `meta: { requiresAuth: true, requiresAdmin: true }`

---

### Step 10: Write Vitest Unit Tests for useUsers Composable

- **File**: `frontend/src/composables/__tests__/useUsers.test.ts`
- **Action**: Write comprehensive unit tests for useUsers composable
- **Implementation Steps**:
  1. Create `frontend/src/composables/__tests__/useUsers.test.ts`
  2. Mock axios API with `vi.mock`
  3. Test `fetchUsers()` success and error cases
  4. Test `fetchUserById()` success, 404, and error cases
  5. Test `createUser()` success, 409 conflict, and validation error cases
  6. Test `updateUser()` success, 404, and error cases
  7. Test loading state transitions
  8. Test error state management

- **Complete Implementation**:
```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useUsers } from '@/composables/useUsers'
import { api } from '@/utils/api'
import type { User } from '@/types/user'

vi.mock('@/utils/api')

const mockUser: User = {
  id: '1',
  email: 'test@example.com',
  firstName: 'John',
  lastName: 'Doe',
  phone: '+34 123 456 789',
  role: 'Member',
  isActive: true,
  createdAt: '2026-02-08T10:00:00Z',
  updatedAt: '2026-02-08T10:00:00Z'
}

describe('useUsers', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchUsers', () => {
    it('should fetch users successfully', async () => {
      const mockUsers = [mockUser]
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: mockUsers, error: null }
      })

      const { users, loading, error, fetchUsers } = useUsers()

      await fetchUsers()

      expect(users.value).toEqual(mockUsers)
      expect(loading.value).toBe(false)
      expect(error.value).toBeNull()
      expect(api.get).toHaveBeenCalledWith('/users')
    })

    it('should set error when fetch fails', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { users, error, fetchUsers } = useUsers()

      await fetchUsers()

      expect(users.value).toEqual([])
      expect(error.value).toBe('Failed to load users. Please try again.')
    })
  })

  describe('fetchUserById', () => {
    it('should fetch user by ID successfully', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      })

      const { selectedUser, fetchUserById } = useUsers()

      const result = await fetchUserById('1')

      expect(result).toEqual(mockUser)
      expect(selectedUser.value).toEqual(mockUser)
      expect(api.get).toHaveBeenCalledWith('/users/1')
    })

    it('should return null when user not found', async () => {
      vi.mocked(api.get).mockRejectedValue({
        response: { status: 404 }
      })

      const { error, fetchUserById } = useUsers()

      const result = await fetchUserById('999')

      expect(result).toBeNull()
      expect(error.value).toBe('User not found.')
    })
  })

  describe('createUser', () => {
    it('should create user successfully', async () => {
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: mockUser, error: null }
      })

      const { users, createUser } = useUsers()

      const request = {
        email: 'test@example.com',
        password: 'password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null,
        role: 'Member' as const
      }

      const result = await createUser(request)

      expect(result).toEqual(mockUser)
      expect(users.value).toContain(mockUser)
      expect(api.post).toHaveBeenCalledWith('/users', request)
    })

    it('should set error on duplicate email', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: { status: 409 }
      })

      const { error, createUser } = useUsers()

      const request = {
        email: 'test@example.com',
        password: 'password123',
        firstName: 'John',
        lastName: 'Doe',
        phone: null,
        role: 'Member' as const
      }

      const result = await createUser(request)

      expect(result).toBeNull()
      expect(error.value).toBe('Email already exists. Please use a different email.')
    })
  })

  describe('updateUser', () => {
    it('should update user successfully', async () => {
      const updatedUser = { ...mockUser, firstName: 'Jane' }
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updatedUser, error: null }
      })

      const { users, selectedUser, updateUser } = useUsers()
      users.value = [mockUser]

      const request = {
        firstName: 'Jane',
        lastName: 'Doe',
        phone: null,
        isActive: true
      }

      const result = await updateUser('1', request)

      expect(result).toEqual(updatedUser)
      expect(selectedUser.value).toEqual(updatedUser)
      expect(users.value[0].firstName).toBe('Jane')
      expect(api.put).toHaveBeenCalledWith('/users/1', request)
    })

    it('should return null when user not found', async () => {
      vi.mocked(api.put).mockRejectedValue({
        response: { status: 404 }
      })

      const { error, updateUser } = useUsers()

      const request = {
        firstName: 'Jane',
        lastName: 'Doe',
        phone: null,
        isActive: true
      }

      const result = await updateUser('999', request)

      expect(result).toBeNull()
      expect(error.value).toBe('User not found.')
    })
  })
})
```

- **Dependencies**: `vitest`, `@vue/test-utils`
- **Implementation Notes**:
  - Comprehensive test coverage following AAA pattern
  - Mocked API responses for isolation
  - Tests for success paths, error paths, and edge cases
  - Validates loading and error state transitions
  - Validates data transformations

---

### Step 11: Write Vitest Component Tests

- **File**: `frontend/src/components/users/__tests__/UserCard.test.ts`
- **Action**: Write component tests for UserCard
- **Implementation Steps**:
  1. Create component test file
  2. Test rendering with user props
  3. Test select event emission on click
  4. Test selected state styling
  5. Test role badge display

- **Complete Implementation**:
```typescript
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import UserCard from '@/components/users/UserCard.vue'
import type { User } from '@/types/user'

const mockUser: User = {
  id: '1',
  email: 'john@example.com',
  firstName: 'John',
  lastName: 'Doe',
  phone: '+34 123 456 789',
  role: 'Member',
  isActive: true,
  createdAt: '2026-02-08T10:00:00Z',
  updatedAt: '2026-02-08T10:00:00Z'
}

describe('UserCard', () => {
  const mountComponent = (props: any) => {
    return mount(UserCard, {
      props,
      global: {
        plugins: [PrimeVue]
      }
    })
  }

  it('should render user information', () => {
    const wrapper = mountComponent({ user: mockUser })

    expect(wrapper.text()).toContain('John Doe')
    expect(wrapper.text()).toContain('john@example.com')
    expect(wrapper.text()).toContain('+34 123 456 789')
    expect(wrapper.text()).toContain('Member')
  })

  it('should emit select event when clicked', async () => {
    const wrapper = mountComponent({ user: mockUser })

    await wrapper.trigger('click')

    expect(wrapper.emitted('select')).toHaveLength(1)
    expect(wrapper.emitted('select')![0]).toEqual([mockUser])
  })

  it('should apply selected styles when selected prop is true', () => {
    const wrapper = mountComponent({ user: mockUser, selected: true })

    expect(wrapper.classes()).toContain('ring-2')
  })

  it('should render phone as dash when null', () => {
    const userWithoutPhone = { ...mockUser, phone: null }
    const wrapper = mountComponent({ user: userWithoutPhone })

    expect(wrapper.text()).not.toContain('+34')
  })
})
```

- **File**: `frontend/src/components/users/__tests__/UserForm.test.ts`
- **Action**: Write component tests for UserForm
- **Implementation Steps**:
  1. Create component test file
  2. Test create mode renders password field
  3. Test edit mode does not render password field
  4. Test form validation
  5. Test submit event emission
  6. Test cancel event emission

- **Complete Implementation**:
```typescript
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import UserForm from '@/components/users/UserForm.vue'
import type { User } from '@/types/user'

const mockUser: User = {
  id: '1',
  email: 'john@example.com',
  firstName: 'John',
  lastName: 'Doe',
  phone: '+34 123 456 789',
  role: 'Member',
  isActive: true,
  createdAt: '2026-02-08T10:00:00Z',
  updatedAt: '2026-02-08T10:00:00Z'
}

describe('UserForm', () => {
  const mountComponent = (props: any) => {
    return mount(UserForm, {
      props,
      global: {
        plugins: [PrimeVue]
      }
    })
  }

  it('should render password field in create mode', () => {
    const wrapper = mountComponent({ mode: 'create' })

    expect(wrapper.find('#password').exists()).toBe(true)
  })

  it('should not render password field in edit mode', () => {
    const wrapper = mountComponent({ mode: 'edit', user: mockUser })

    expect(wrapper.find('#password').exists()).toBe(false)
  })

  it('should emit cancel event when cancel button clicked', async () => {
    const wrapper = mountComponent({ mode: 'create' })

    const cancelButton = wrapper.findAll('button').find((b) => b.text() === 'Cancel')
    await cancelButton?.trigger('click')

    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })

  it('should disable submit button when form is invalid', () => {
    const wrapper = mountComponent({ mode: 'create' })

    const submitButton = wrapper.findAll('button').find((b) => b.text().includes('Create User'))
    expect(submitButton?.attributes('disabled')).toBeDefined()
  })
})
```

- **Dependencies**: `vitest`, `@vue/test-utils`, PrimeVue
- **Implementation Notes**:
  - Tests component rendering and behavior
  - Tests mode-specific rendering (create vs edit)
  - Tests event emissions
  - Tests form validation

---

### Step 12: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/users.cy.ts`
- **Action**: Write end-to-end tests for user management workflows
- **Implementation Steps**:
  1. Create `frontend/cypress/e2e/users.cy.ts`
  2. Test user list page load
  3. Test create user flow
  4. Test view user detail
  5. Test edit user flow
  6. Test validation errors

- **Complete Implementation**:
```typescript
describe('User Management', () => {
  beforeEach(() => {
    cy.visit('/users')
  })

  it('should display users list', () => {
    cy.get('[data-testid="users-table"]').should('exist')
    cy.contains('User Management').should('be.visible')
  })

  it('should open create user dialog', () => {
    cy.contains('button', 'Create User').click()
    cy.contains('Create New User').should('be.visible')
    cy.get('#email').should('be.visible')
    cy.get('#password').should('be.visible')
  })

  it('should create a new user successfully', () => {
    cy.contains('button', 'Create User').click()

    // Fill form
    cy.get('#email').type('newuser@example.com')
    cy.get('#password').type('Password123!')
    cy.get('#firstName').type('New')
    cy.get('#lastName').type('User')
    cy.get('#phone').type('+34 111 222 333')

    // Submit
    cy.contains('button', 'Create User').click()

    // Verify success
    cy.contains('newuser@example.com').should('be.visible')
  })

  it('should show validation errors for invalid data', () => {
    cy.contains('button', 'Create User').click()

    // Try to submit empty form
    cy.contains('button', 'Create User').should('be.disabled')

    // Enter invalid email
    cy.get('#email').type('invalid-email')
    cy.get('#password').type('short')

    // Check validation messages
    cy.contains('Email must be valid').should('be.visible')
    cy.contains('Password must be at least 8 characters').should('be.visible')
  })

  it('should navigate to user detail page', () => {
    // Click view button on first user
    cy.get('[aria-label="View Details"]').first().click()

    // Verify detail page
    cy.url().should('include', '/users/')
    cy.contains('User Details').should('be.visible')
  })

  it('should edit user successfully', () => {
    // Navigate to detail page
    cy.get('[aria-label="View Details"]').first().click()

    // Click edit button
    cy.contains('button', 'Edit').click()

    // Modify first name
    cy.get('#firstName').clear().type('Updated')

    // Submit
    cy.contains('button', 'Update User').click()

    // Verify update
    cy.contains('Updated').should('be.visible')
    cy.contains('button', 'Edit').should('be.visible')
  })

  it('should cancel edit and return to view mode', () => {
    cy.get('[aria-label="View Details"]').first().click()
    cy.contains('button', 'Edit').click()

    // Cancel edit
    cy.contains('button', 'Cancel').click()

    // Should be back in view mode
    cy.contains('button', 'Edit').should('be.visible')
  })

  it('should navigate back to users list', () => {
    cy.get('[aria-label="View Details"]').first().click()

    cy.contains('button', 'Back to Users').click()

    cy.url().should('equal', Cypress.config().baseUrl + '/users')
  })
})
```

- **Dependencies**: Cypress
- **Implementation Notes**:
  - Tests complete user workflows
  - Uses semantic selectors (labels, button text)
  - Tests happy paths and error cases
  - Validates navigation between pages
  - Tests form validation

---

### Step 13: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes made during implementation
  2. **Identify Documentation Files**: Determine which documentation files need updates:
     - **API endpoints** → Verify `ai-specs/specs/api-spec.yml` (if exists) or create API documentation
     - **Component patterns** → Update `ai-specs/specs/frontend-standards.mdc` with new patterns if introduced
     - **Routing** → Document new routes in router documentation or README
     - **Types and interfaces** → Document type definitions structure
  3. **Update Documentation**: For each affected file:
     - Update content in English (as per documentation-standards.mdc)
     - Maintain consistency with existing documentation structure
     - Document new components (`UserCard`, `UserForm`)
     - Document new composable (`useUsers`)
     - Document new pages (`UsersPage`, `UserDetailPage`)
     - Document routes (`/users`, `/users/:id`)
  4. **Verify Documentation**:
     - Confirm all changes are accurately reflected
     - Check that documentation follows established structure
  5. **Create Frontend Implementation Summary**:
     - Create or update `ai-specs/changes/phase1_implementation_summary.md`
     - Document what was implemented
     - Note any deviations from plan
     - Document known issues or future improvements

- **Documentation Files to Update**:
  - `README.md` or `frontend/README.md` - Add user management feature to features list
  - `ai-specs/changes/phase1_implementation_summary.md` - Create implementation summary

- **References**:
  - Follow process described in `ai-specs/specs/documentation-standards.mdc`
  - All documentation must be written in English

- **Notes**:
  - This step is MANDATORY before considering the implementation complete
  - Do not skip documentation updates
  - Phase 1 has no authentication, document this clearly for Phase 2 transition

---

## Implementation Order

Complete implementation steps in this exact sequence:

1. **Step 0**: Create Feature Branch `feature/phase1-user-crud-frontend`
2. **Step 1**: Define TypeScript Interfaces (`frontend/src/types/user.ts`)
3. **Step 2**: Verify/Create API Response Types (`frontend/src/types/api.ts`)
4. **Step 3**: Configure Axios Instance (`frontend/src/utils/api.ts`)
5. **Step 4**: Create useUsers Composable (`frontend/src/composables/useUsers.ts`)
6. **Step 5**: Create UserCard Component (`frontend/src/components/users/UserCard.vue`)
7. **Step 6**: Create UserForm Component (`frontend/src/components/users/UserForm.vue`)
8. **Step 7**: Create UsersPage (`frontend/src/pages/UsersPage.vue`)
9. **Step 8**: Create UserDetailPage (`frontend/src/pages/UserDetailPage.vue`)
10. **Step 9**: Update Router Configuration (`frontend/src/router/index.ts`)
11. **Step 10**: Write Vitest Unit Tests for useUsers Composable
12. **Step 11**: Write Vitest Component Tests (UserCard, UserForm)
13. **Step 12**: Write Cypress E2E Tests (`frontend/cypress/e2e/users.cy.ts`)
14. **Step 13**: Update Technical Documentation

---

## Testing Checklist

### Unit Tests (Vitest)
- [ ] `useUsers` composable tests pass
- [ ] All API methods tested (fetchUsers, fetchUserById, createUser, updateUser)
- [ ] Loading state transitions tested
- [ ] Error handling tested (network errors, 404, 409, validation errors)
- [ ] `UserCard` component tests pass
- [ ] `UserForm` component tests pass (create mode, edit mode)
- [ ] Form validation tested
- [ ] Test coverage >= 90%

### Component Functionality
- [ ] UserCard displays user information correctly
- [ ] UserCard emits select event on click
- [ ] UserForm renders correctly in create mode (with password field)
- [ ] UserForm renders correctly in edit mode (without password field)
- [ ] UserForm validation works (client-side)
- [ ] UserForm emits submit with correct data structure
- [ ] UsersPage displays loading spinner while fetching
- [ ] UsersPage displays error message on fetch failure
- [ ] UsersPage displays DataTable with users
- [ ] UsersPage create dialog opens and closes
- [ ] UserDetailPage fetches user on mount
- [ ] UserDetailPage toggles between view and edit modes
- [ ] UserDetailPage updates user successfully

### E2E Tests (Cypress)
- [ ] Users list page loads
- [ ] Create user dialog opens
- [ ] Create user form validation works
- [ ] Create user flow completes successfully
- [ ] User appears in list after creation
- [ ] View user detail navigation works
- [ ] Edit user flow completes successfully
- [ ] Cancel edit returns to view mode
- [ ] Back navigation works
- [ ] All E2E tests pass

### Integration
- [ ] API calls reach backend correctly
- [ ] Backend responses are parsed correctly
- [ ] Error responses from backend display user-friendly messages
- [ ] ApiResponse<T> envelope handled correctly
- [ ] Validation errors display field-level messages
- [ ] Routes navigate correctly
- [ ] Composable state persists across components

---

## Error Handling Patterns

### Composable Error Handling

The `useUsers` composable implements comprehensive error handling:

- **Network errors**: Generic "Failed to..." message
- **404 Not Found**: Specific "User not found" message
- **409 Conflict** (duplicate email): "Email already exists" message
- **400 Bad Request** (validation): Extracts field-level errors from backend response
- **Console logging**: All errors logged for debugging

### User-Friendly Error Messages

- Components display errors from composable via PrimeVue `Message` component
- Validation errors shown inline below form fields
- Global errors shown at top of form/page
- Retry buttons provided for transient failures

### Loading States

- Spinner displayed during async operations
- Submit buttons disabled during loading
- Loading state prevents duplicate submissions

---

## UI/UX Considerations

### PrimeVue Components Used

- **DataTable**: User list with pagination, sorting, filtering
- **Card**: User cards and detail display
- **Dialog**: Create user modal
- **InputText**: Text inputs (email, name, phone, password)
- **Dropdown**: Role selection
- **InputSwitch**: Active status toggle
- **Button**: All actions (create, edit, save, cancel, view)
- **Tag**: Role and status badges
- **ProgressSpinner**: Loading indicator
- **Message**: Error and info messages

### Tailwind CSS Patterns

- **Layout**: Flexbox and Grid for responsive layouts
- **Spacing**: Consistent use of `gap`, `p-`, `m-` utilities
- **Responsive**: Mobile-first with `sm:`, `md:`, `lg:` breakpoints
- **Colors**: Semantic color classes (`text-gray-600`, `bg-primary-50`)
- **Typography**: Font weight, size utilities
- **Interactive states**: `hover:`, `focus:`, `disabled:` variants

### Responsive Design

- **Mobile (default)**: Single column layouts, full-width inputs
- **Tablet (sm:)**: 2-column grids where appropriate
- **Desktop (lg:)**: 3-column layouts for lists, wider modals

### Accessibility

- **Semantic HTML**: Proper heading hierarchy, labels
- **ARIA labels**: `aria-label` on icon-only buttons
- **Keyboard navigation**: Tab order, Enter to submit
- **Focus states**: Visible focus indicators
- **Screen reader support**: PrimeVue components have built-in ARIA support

### Loading States and User Feedback

- **Loading spinners**: Centered spinner during initial data fetch
- **Disabled states**: Buttons disabled during submission
- **Success feedback**: Immediate UI update on success (optional: toast notification)
- **Error feedback**: Inline error messages with retry options

---

## Dependencies

### NPM Packages Required

All dependencies should already be installed as part of the Vue 3 + PrimeVue scaffolding. Verify these are in `package.json`:

- **Core**:
  - `vue` (^3.x)
  - `vue-router` (^4.x)

- **UI**:
  - `primevue` (^3.x or ^4.x)
  - `primeicons` (^6.x or ^7.x)

- **HTTP**:
  - `axios` (^1.x)

- **Testing**:
  - `vitest` (^1.x or ^2.x)
  - `@vue/test-utils` (^2.x)
  - `cypress` (^13.x)

### PrimeVue Components

Import from `primevue/*`:
- `Card`
- `DataTable`, `Column`
- `Button`
- `Dialog`
- `InputText`
- `Dropdown`
- `InputSwitch`
- `Tag`
- `ProgressSpinner`
- `Message`

---

## Notes

### Important Reminders

1. **No Authentication in Phase 1**: All routes are publicly accessible. Authentication will be added in Phase 2.
2. **Password Storage**: Backend uses SHA-256 placeholder hashing in Phase 1. Phase 2 will replace with BCrypt.
3. **English Only**: All code, comments, and UI text must be in English as per `base-standards.mdc`.
4. **TypeScript Strict Mode**: No `any` types allowed. Use proper typing or `unknown`.
5. **Composable Pattern**: All API calls go through `useUsers` composable. Components never call API directly.
6. **Script Setup Syntax**: All components use `<script setup lang="ts">`. No Options API.
7. **No Custom Styles**: Use Tailwind utilities. No `<style>` blocks.

### Business Rules

- **User Roles**: Admin, Board, Member (enum values from backend)
- **Email Uniqueness**: Enforced by backend, handled in frontend with user-friendly error
- **Password Requirements**: Minimum 8 characters (basic validation for Phase 1)
- **Active Status**: Users can be active or inactive (editable in edit mode)
- **Phone Optional**: Phone number is optional field

### Language Requirements

- All variable names, function names, comments: **English**
- All UI text (labels, buttons, messages): **English**
- Date formatting: Spanish locale (`es-ES`) for user-friendly display
- Error messages: **English**

### TypeScript Requirements

- **Strict mode enabled** in `tsconfig.json`
- **No `any` types**: Use specific types or `unknown`
- **Interface definitions**: All data structures have typed interfaces
- **Props and emits**: Fully typed with generics
- **API responses**: Typed with `ApiResponse<T>` generic

---

## Next Steps After Implementation

### Post-Implementation Tasks

1. **Manual Testing**:
   - Test all CRUD operations in browser
   - Test responsive design on mobile, tablet, desktop
   - Test error scenarios (network failures, validation errors)
   - Verify backend integration

2. **Code Review**:
   - Review all files against frontend standards
   - Check TypeScript strictness (no `any`)
   - Verify composable pattern usage
   - Ensure PrimeVue + Tailwind consistency

3. **Performance Check**:
   - Verify lazy loading of routes
   - Check bundle size with `npm run build`
   - Test loading performance

4. **Documentation Verification**:
   - Ensure Step 13 completed
   - Verify all new features documented
   - Check that Phase 1 limitations noted (no auth)

5. **Prepare for Phase 2**:
   - Document authentication integration points
   - Note where route guards will be added
   - Plan JWT token handling in axios interceptors

---

## Implementation Verification

### Final Verification Checklist

#### Code Quality
- [ ] All files use TypeScript with strict mode
- [ ] No `any` types in codebase
- [ ] All components use `<script setup lang="ts">`
- [ ] No Options API components
- [ ] No custom `<style>` blocks (Tailwind only)
- [ ] All API calls through `useUsers` composable
- [ ] Proper error handling throughout

#### Functionality
- [ ] User list page displays all users
- [ ] Create user flow works end-to-end
- [ ] View user detail works
- [ ] Edit user flow works end-to-end
- [ ] Form validation works (client-side)
- [ ] Backend validation errors displayed correctly
- [ ] Loading states work
- [ ] Error states work with retry option
- [ ] Navigation between pages works
- [ ] Back navigation works

#### Testing
- [ ] All Vitest unit tests pass (`npm run test`)
- [ ] All Cypress E2E tests pass (`npx cypress run`)
- [ ] Test coverage >= 90%
- [ ] No console errors in browser during manual testing

#### Integration
- [ ] Frontend connects to backend API successfully
- [ ] `ApiResponse<T>` envelope parsed correctly
- [ ] Validation errors from backend displayed
- [ ] 404 and 409 status codes handled
- [ ] User list fetches from backend
- [ ] Create user posts to backend
- [ ] Update user puts to backend
- [ ] Route params work correctly

#### UI/UX
- [ ] Responsive design works (mobile, tablet, desktop)
- [ ] PrimeVue components render correctly
- [ ] Tailwind styling consistent
- [ ] Loading spinners display during async operations
- [ ] Error messages are user-friendly
- [ ] Buttons have proper states (disabled during loading)
- [ ] Accessibility: keyboard navigation works
- [ ] Accessibility: ARIA labels present

#### Documentation
- [ ] Step 13 completed
- [ ] Technical documentation updated
- [ ] Implementation summary created
- [ ] Phase 1 limitations documented
- [ ] README updated with new features

---

## Summary

This implementation plan provides a complete roadmap for Phase 1 User CRUD frontend functionality using Vue 3 Composition API, PrimeVue, and Tailwind CSS. The implementation follows established project patterns with composable-based architecture, TypeScript strict typing, and comprehensive testing.

**Key Deliverables**:
- Type-safe TypeScript interfaces
- `useUsers` composable for API communication
- Reusable `UserCard` and `UserForm` components
- Complete user management pages (list and detail)
- Router configuration
- Comprehensive unit and E2E tests
- Updated technical documentation

**Phase 1 Scope**:
- No authentication (Phase 2)
- Public routes (Phase 2 will add guards)
- Basic password validation (Phase 2 will enhance)

**Ready for Phase 2**: This implementation provides a solid foundation for adding JWT authentication, route guards, and role-based authorization in Phase 2.

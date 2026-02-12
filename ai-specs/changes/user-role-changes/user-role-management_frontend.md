# Frontend Implementation Plan: User Role Management

## Overview

Implement a secure frontend interface for updating user roles within the Abuvi platform. This feature allows administrators and board members to change user roles while maintaining strict UI-level security controls and providing clear user feedback. The implementation follows Vue 3 Composition API patterns with PrimeVue UI components and Tailwind CSS styling.

**Frontend Architecture Principles:**

- Vue 3 Composition API with `<script setup lang="ts">`
- Composable-based architecture for API communication (`useUsers`)
- PrimeVue components for UI (Dialog, Dropdown, Button, DataTable)
- Tailwind CSS for styling (no custom `<style>` blocks)
- TypeScript strict typing (no `any` types)
- Role-based UI rendering (Admin/Board only)

## Architecture Context

### Components/Composables Involved

**Existing (to be extended):**

- `frontend/src/composables/useUsers.ts` - Add `updateUserRole` method
- `frontend/src/types/user.ts` - Add `UpdateUserRoleRequest` interface
- `frontend/src/stores/auth.ts` - Already has `isAdmin` and `isBoard` computed properties

**New (to be created):**

- `frontend/src/components/users/UserRoleDialog.vue` - Modal for role updates
- `frontend/src/components/users/UserRoleCell.vue` - DataTable cell component with role badge and edit button
- `frontend/src/views/admin/UsersPage.vue` or `frontend/src/pages/UsersPage.vue` - Users management page (if not exists)

### Routing Considerations

- User role management UI should be accessible only to Admin/Board roles
- Route guard should check `auth.isBoard` (which includes Admin)
- Typical route: `/admin/users` with `meta: { requiresAuth: true, requiresBoard: true }`

### State Management Approach

- **No Pinia store needed** - Local component state with composables
- `useUsers` composable handles all API communication and state
- Auth state managed by existing `useAuthStore` for role checks

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch following the development workflow
- **Branch Naming**: `feature/user-role-management-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `main` branch
  2. Pull latest changes: `git pull origin main`
  3. Create new branch: `git checkout -b feature/user-role-management-frontend`
  4. Verify branch creation: `git branch`
- **Notes**: This must be the FIRST step before any code changes. The backend was implemented in `feature/user-role-management-backend`, so we use a separate frontend branch to maintain separation of concerns.

---

### Step 1: Define TypeScript Interfaces

- **File**: `frontend/src/types/user.ts`
- **Action**: Add `UpdateUserRoleRequest` interface for role update API calls
- **Implementation Steps**:
  1. Open `frontend/src/types/user.ts`
  2. Add the following interface after the existing interfaces:

     ```typescript
     /**
      * Request to update a user's role (Admin/Board only)
      */
     export interface UpdateUserRoleRequest {
       newRole: UserRole
       reason?: string | null  // Optional reason for audit trail
     }
     ```

  3. Verify that `UserRole` type already exists (it does: `'Admin' | 'Board' | 'Member'`)
- **Dependencies**: None (uses existing `UserRole` type)
- **Implementation Notes**:
  - The interface must match the backend DTO exactly
  - `reason` is optional but recommended for audit purposes
  - Use `null` instead of `undefined` for consistency with backend

---

### Step 2: Extend `useUsers` Composable

- **File**: `frontend/src/composables/useUsers.ts`
- **Action**: Add `updateUserRole` method to handle role update API calls
- **Function Signature**:

  ```typescript
  const updateUserRole = async (
    userId: string,
    request: UpdateUserRoleRequest
  ): Promise<User | null>
  ```

- **Implementation Steps**:
  1. Import `UpdateUserRoleRequest` from `@/types/user`
  2. Add the following method to the composable (before the `return` statement):

     ```typescript
     /**
      * Update a user's role (Admin/Board only)
      * Calls the backend PATCH /api/users/{id}/role endpoint
      */
     const updateUserRole = async (
       userId: string,
       request: UpdateUserRoleRequest
     ): Promise<User | null> => {
       loading.value = true
       error.value = null
       try {
         const response = await api.patch<ApiResponse<User>>(
           `/users/${userId}/role`,
           request
         )
         if (response.data.data) {
           // Update user in the list
           const index = users.value.findIndex((u) => u.id === userId)
           if (index !== -1) {
             users.value[index] = response.data.data
           }
           // Update selected user if it's the same
           if (selectedUser.value?.id === userId) {
             selectedUser.value = response.data.data
           }
           return response.data.data
         }
         return null
       } catch (err: any) {
         if (err.response?.status === 400) {
           // Self-role change attempt or invalid operation
           error.value = err.response.data?.error?.message || 'Invalid role change operation'
         } else if (err.response?.status === 403) {
           error.value = 'Insufficient privileges to change this role'
         } else if (err.response?.status === 404) {
           error.value = 'User not found'
         } else {
           error.value = 'Failed to update user role. Please try again.'
         }
         console.error('updateUserRole error:', err)
         return null
       } finally {
         loading.value = false
       }
     }
     ```

  3. Add `updateUserRole` to the composable's return statement
- **Dependencies**:
  - `UpdateUserRoleRequest` type from Step 1
  - Existing `api` instance from `@/utils/api`
- **Implementation Notes**:
  - Uses PATCH method to match backend endpoint
  - Updates both `users` array and `selectedUser` for reactivity
  - Handles specific error codes (400, 403, 404)
  - Error messages match backend error scenarios

---

### Step 3: Create `UserRoleDialog` Component

- **File**: `frontend/src/components/users/UserRoleDialog.vue`
- **Action**: Create a modal dialog for updating user roles with form validation
- **Component Signature**:

  ```vue
  <script setup lang="ts">
  interface Props {
    visible: boolean
    user: User | null
  }

  interface Emits {
    (e: 'update:visible', value: boolean): void
    (e: 'roleUpdated', user: User): void
  }
  </script>
  ```

- **Implementation Steps**:
  1. Create directory `frontend/src/components/users/` if it doesn't exist
  2. Create file `UserRoleDialog.vue` with the following structure:

     ```vue
     <script setup lang="ts">
     import { ref, watch, computed } from 'vue'
     import Dialog from 'primevue/dialog'
     import Dropdown from 'primevue/dropdown'
     import Textarea from 'primevue/textarea'
     import Button from 'primevue/button'
     import Message from 'primevue/message'
     import { useUsers } from '@/composables/useUsers'
     import { useAuthStore } from '@/stores/auth'
     import type { User, UserRole, UpdateUserRoleRequest } from '@/types/user'

     interface Props {
       visible: boolean
       user: User | null
     }

     const props = defineProps<Props>()

     const emit = defineEmits<{
       'update:visible': [value: boolean]
       roleUpdated: [user: User]
     }>()

     const auth = useAuthStore()
     const { updateUserRole, loading, error, clearError } = useUsers()

     // Form state
     const newRole = ref<UserRole | null>(null)
     const reason = ref('')

     // Available roles based on current user's role
     const availableRoles = computed(() => {
       const roles: { label: string; value: UserRole }[] = [
         { label: 'Member', value: 'Member' },
         { label: 'Board', value: 'Board' },
         { label: 'Admin', value: 'Admin' }
       ]

       // Board members can only set Member role
       if (!auth.isAdmin) {
         return roles.filter((r) => r.value === 'Member')
       }

       return roles
     })

     // Check if user is trying to change their own role
     const isSelfChange = computed(() => {
       return props.user?.id === auth.user?.id
     })

     // Validation
     const canSubmit = computed(() => {
       return (
         !isSelfChange.value &&
         newRole.value !== null &&
         newRole.value !== props.user?.role &&
         !loading.value
       )
     })

     // Reset form when dialog opens/closes
     watch(
       () => props.visible,
       (visible) => {
         if (visible && props.user) {
           newRole.value = props.user.role
           reason.value = ''
           clearError()
         }
       }
     )

     // Handle role update
     const handleSubmit = async () => {
       if (!props.user || !newRole.value || !canSubmit.value) return

       const request: UpdateUserRoleRequest = {
         newRole: newRole.value,
         reason: reason.value.trim() || null
       }

       const updatedUser = await updateUserRole(props.user.id, request)

       if (updatedUser) {
         emit('roleUpdated', updatedUser)
         emit('update:visible', false)
       }
     }

     const handleCancel = () => {
       emit('update:visible', false)
     }
     </script>

     <template>
       <Dialog
         :visible="visible"
         :header="`Update Role: ${user?.firstName} ${user?.lastName}`"
         modal
         :closable="!loading"
         class="w-full max-w-md"
         @update:visible="$emit('update:visible', $event)"
       >
         <div v-if="user" class="flex flex-col gap-4">
           <!-- Self-change warning -->
           <Message v-if="isSelfChange" severity="warn" :closable="false">
             You cannot change your own role
           </Message>

           <!-- Error message -->
           <Message v-if="error" severity="error" :closable="false">
             {{ error }}
           </Message>

           <!-- Current role display -->
           <div class="flex flex-col gap-2">
             <label class="text-sm font-medium text-gray-700">Current Role</label>
             <div class="rounded-md bg-gray-100 px-3 py-2">
               <span
                 class="inline-block rounded-full px-2 py-1 text-xs font-semibold"
                 :class="{
                   'bg-red-100 text-red-800': user.role === 'Admin',
                   'bg-blue-100 text-blue-800': user.role === 'Board',
                   'bg-gray-100 text-gray-800': user.role === 'Member'
                 }"
               >
                 {{ user.role }}
               </span>
             </div>
           </div>

           <!-- New role dropdown -->
           <div class="flex flex-col gap-2">
             <label for="newRole" class="text-sm font-medium text-gray-700">New Role *</label>
             <Dropdown
               id="newRole"
               v-model="newRole"
               :options="availableRoles"
               option-label="label"
               option-value="value"
               placeholder="Select new role"
               :disabled="loading || isSelfChange"
               class="w-full"
             />
           </div>

           <!-- Reason textarea -->
           <div class="flex flex-col gap-2">
             <label for="reason" class="text-sm font-medium text-gray-700">
               Reason (optional)
             </label>
             <Textarea
               id="reason"
               v-model="reason"
               rows="3"
               placeholder="Provide a reason for this role change (for audit purposes)"
               :disabled="loading || isSelfChange"
               class="w-full"
               maxlength="500"
             />
             <small class="text-gray-500">{{ reason.length }}/500 characters</small>
           </div>

           <!-- Action buttons -->
           <div class="flex justify-end gap-2 pt-4">
             <Button
               label="Cancel"
               severity="secondary"
               text
               :disabled="loading"
               @click="handleCancel"
             />
             <Button
               label="Update Role"
               :loading="loading"
               :disabled="!canSubmit"
               @click="handleSubmit"
             />
           </div>
         </div>
       </Dialog>
     </template>
     ```

  3. Ensure the directory exists before creating the file
- **Dependencies**:
  - PrimeVue: `Dialog`, `Dropdown`, `Textarea`, `Button`, `Message`
  - Composables: `useUsers`, `useAuthStore`
  - Types: `User`, `UserRole`, `UpdateUserRoleRequest`
- **Implementation Notes**:
  - Prevents self-role changes with clear UI warning
  - Board members only see "Member" role option (enforced client-side AND server-side)
  - Validates that new role differs from current role
  - Character counter for reason field (500 char limit)
  - Emits `roleUpdated` event on success for parent to show toast notification
  - Uses PrimeVue `v-model:visible` pattern for dialog visibility

---

### Step 4: Create `UserRoleCell` Component

- **File**: `frontend/src/components/users/UserRoleCell.vue`
- **Action**: Create a reusable DataTable cell component showing role badge with edit button
- **Component Signature**:

  ```vue
  <script setup lang="ts">
  interface Props {
    user: User
  }

  interface Emits {
    (e: 'editRole', user: User): void
  }
  </script>
  ```

- **Implementation Steps**:
  1. Create file `frontend/src/components/users/UserRoleCell.vue`:

     ```vue
     <script setup lang="ts">
     import Button from 'primevue/button'
     import { useAuthStore } from '@/stores/auth'
     import type { User } from '@/types/user'

     interface Props {
       user: User
     }

     const props = defineProps<Props>()

     const emit = defineEmits<{
       editRole: [user: User]
     }>()

     const auth = useAuthStore()

     // Show edit button only if:
     // 1. Current user is Admin or Board
     // 2. Not trying to edit own role
     const canEditRole = (user: User): boolean => {
       if (!auth.isBoard) return false
       if (user.id === auth.user?.id) return false

       // Board members can only edit Member roles
       if (!auth.isAdmin && user.role !== 'Member') return false

       return true
     }

     const handleEditClick = () => {
       emit('editRole', props.user)
     }
     </script>

     <template>
       <div class="flex items-center justify-between gap-2">
         <span
           class="inline-block rounded-full px-2 py-1 text-xs font-semibold"
           :class="{
             'bg-red-100 text-red-800': user.role === 'Admin',
             'bg-blue-100 text-blue-800': user.role === 'Board',
             'bg-gray-100 text-gray-800': user.role === 'Member'
           }"
         >
           {{ user.role }}
         </span>

         <Button
           v-if="canEditRole(user)"
           icon="pi pi-pencil"
           severity="secondary"
           text
           rounded
           size="small"
           aria-label="Edit role"
           @click="handleEditClick"
         />
       </div>
     </template>
     ```

  2. Verify PrimeVue icons are available (pi-pencil)
- **Dependencies**:
  - PrimeVue: `Button`
  - Stores: `useAuthStore`
  - Types: `User`
- **Implementation Notes**:
  - Role badges use consistent color scheme (Admin=red, Board=blue, Member=gray)
  - Edit button only shown when user has permission
  - Board members cannot edit Admin or other Board member roles
  - Users cannot edit their own role (button hidden)
  - Uses PrimeVue icon `pi-pencil` for edit action
  - Small, rounded, text button for compact DataTable cell

---

### Step 5: Create or Update Users Management Page

- **File**: `frontend/src/pages/UsersPage.vue` or `frontend/src/views/admin/UsersPage.vue`
- **Action**: Create users management page with DataTable and role management integration
- **Implementation Steps**:
  1. Check if a users page already exists:
     - If yes, skip to step 2
     - If no, create `frontend/src/pages/UsersPage.vue` (or `views/admin/UsersPage.vue` based on project structure)
  2. Implement the following structure:

     ```vue
     <script setup lang="ts">
     import { onMounted, ref } from 'vue'
     import DataTable from 'primevue/datatable'
     import Column from 'primevue/column'
     import Button from 'primevue/button'
     import { useToast } from 'primevue/usetoast'
     import { useUsers } from '@/composables/useUsers'
     import UserRoleCell from '@/components/users/UserRoleCell.vue'
     import UserRoleDialog from '@/components/users/UserRoleDialog.vue'
     import type { User } from '@/types/user'

     const { users, loading, error, fetchUsers } = useUsers()
     const toast = useToast()

     // Dialog state
     const showRoleDialog = ref(false)
     const selectedUser = ref<User | null>(null)

     // Fetch users on mount
     onMounted(() => {
       fetchUsers()
     })

     // Handle edit role click
     const handleEditRole = (user: User) => {
       selectedUser.value = user
       showRoleDialog.value = true
     }

     // Handle role update success
     const handleRoleUpdated = (updatedUser: User) => {
       toast.add({
         severity: 'success',
         summary: 'Role Updated',
         detail: `${updatedUser.firstName} ${updatedUser.lastName}'s role updated to ${updatedUser.role}`,
         life: 5000
       })
     }

     // Format date for display
     const formatDate = (dateString: string): string => {
       return new Date(dateString).toLocaleDateString('en-US', {
         year: 'numeric',
         month: 'short',
         day: 'numeric'
       })
     }
     </script>

     <template>
       <div class="flex flex-col gap-6 p-6">
         <!-- Page header -->
         <div class="flex items-center justify-between">
           <div>
             <h1 class="text-3xl font-bold text-gray-900">Users Management</h1>
             <p class="mt-1 text-sm text-gray-600">
               Manage user accounts and roles
             </p>
           </div>
           <Button
             label="Refresh"
             icon="pi pi-refresh"
             :loading="loading"
             @click="fetchUsers"
           />
         </div>

         <!-- Error message -->
         <Message v-if="error" severity="error" :closable="false">
           {{ error }}
         </Message>

         <!-- Users DataTable -->
         <DataTable
           :value="users"
           :loading="loading"
           striped-rows
           paginator
           :rows="10"
           :rows-per-page-options="[10, 25, 50]"
           responsive-layout="scroll"
           class="rounded-lg border border-gray-200"
         >
           <template #empty>
             <div class="flex justify-center p-8 text-gray-500">
               No users found
             </div>
           </template>

           <Column field="email" header="Email" sortable>
             <template #body="{ data }">
               <div class="flex flex-col">
                 <span class="font-medium">{{ data.email }}</span>
                 <span class="text-xs text-gray-500">{{ data.firstName }} {{ data.lastName }}</span>
               </div>
             </template>
           </Column>

           <Column field="phone" header="Phone">
             <template #body="{ data }">
               {{ data.phone || '—' }}
             </template>
           </Column>

           <Column field="role" header="Role" sortable>
             <template #body="{ data }">
               <UserRoleCell :user="data" @edit-role="handleEditRole" />
             </template>
           </Column>

           <Column field="isActive" header="Status" sortable>
             <template #body="{ data }">
               <span
                 class="inline-block rounded-full px-2 py-1 text-xs font-semibold"
                 :class="{
                   'bg-green-100 text-green-800': data.isActive,
                   'bg-gray-100 text-gray-600': !data.isActive
                 }"
               >
                 {{ data.isActive ? 'Active' : 'Inactive' }}
               </span>
             </template>
           </Column>

           <Column field="createdAt" header="Created" sortable>
             <template #body="{ data }">
               {{ formatDate(data.createdAt) }}
             </template>
           </Column>
         </DataTable>

         <!-- Role update dialog -->
         <UserRoleDialog
           v-model:visible="showRoleDialog"
           :user="selectedUser"
           @role-updated="handleRoleUpdated"
         />
       </div>
     </template>
     ```

  3. If using PrimeVue Toast, ensure `Toast` component is registered globally in `main.ts`
- **Dependencies**:
  - PrimeVue: `DataTable`, `Column`, `Button`, `Message`, `useToast`
  - Components: `UserRoleCell`, `UserRoleDialog`
  - Composables: `useUsers`
- **Implementation Notes**:
  - Uses PrimeVue DataTable with pagination and sorting
  - Integrates `UserRoleCell` component in role column
  - Shows success toast notification after role update
  - Responsive layout with Tailwind classes
  - Email column shows full name as secondary text
  - Status badges for active/inactive users
  - Phone field shows "—" if null

---

### Step 6: Update Routing Configuration

- **File**: `frontend/src/router/index.ts`
- **Action**: Add route for users management page with proper guards
- **Implementation Steps**:
  1. Open `frontend/src/router/index.ts`
  2. Add the following route (adjust based on project structure):

     ```typescript
     {
       path: '/admin/users',
       name: 'users-management',
       component: () => import('@/pages/UsersPage.vue'),
       meta: {
         requiresAuth: true,
         requiresBoard: true  // Only Admin and Board can access
       }
     }
     ```

  3. Verify route guard checks `requiresBoard` meta field (should already exist from auth implementation)
  4. If route guard doesn't exist, add:

     ```typescript
     router.beforeEach((to) => {
       const auth = useAuthStore()

       if (to.meta.requiresAuth && !auth.isAuthenticated) {
         return { path: '/login', query: { redirect: to.fullPath } }
       }

       if (to.meta.requiresBoard && !auth.isBoard) {
         return { path: '/', meta: { message: 'Access denied' } }
       }
     })
     ```

- **Dependencies**:
  - `useAuthStore` for auth checks
  - Users page component from Step 5
- **Implementation Notes**:
  - Route guard prevents unauthorized access at routing level
  - `requiresBoard` check allows both Admin and Board members
  - Lazy loading with `() => import()` for code splitting
  - Redirect to home page with access denied message for unauthorized users

---

### Step 7: Write Vitest Unit Tests for Composable

- **File**: `frontend/src/composables/__tests__/useUsers.test.ts`
- **Action**: Add unit tests for `updateUserRole` method
- **Implementation Steps**:
  1. Open `frontend/src/composables/__tests__/useUsers.test.ts`
  2. Add the following test cases:

     ```typescript
     import { describe, it, expect, vi, beforeEach } from 'vitest'
     import { useUsers } from '@/composables/useUsers'
     import { api } from '@/utils/api'
     import type { UpdateUserRoleRequest } from '@/types/user'

     vi.mock('@/utils/api')

     describe('useUsers - updateUserRole', () => {
       beforeEach(() => {
         vi.clearAllMocks()
       })

       it('should update user role successfully', async () => {
         // Arrange
         const mockUser = {
           id: 'user-123',
           email: 'john@example.com',
           firstName: 'John',
           lastName: 'Doe',
           phone: null,
           role: 'Member',
           isActive: true,
           createdAt: '2026-01-01T00:00:00Z',
           updatedAt: '2026-02-11T10:00:00Z'
         }
         const mockUpdatedUser = { ...mockUser, role: 'Board' }
         const request: UpdateUserRoleRequest = {
           newRole: 'Board',
           reason: 'Promotion to board member'
         }

         vi.mocked(api.patch).mockResolvedValue({
           data: { success: true, data: mockUpdatedUser, error: null }
         })

         // Act
         const { updateUserRole, loading, error } = useUsers()
         const result = await updateUserRole('user-123', request)

         // Assert
         expect(result).toEqual(mockUpdatedUser)
         expect(loading.value).toBe(false)
         expect(error.value).toBeNull()
         expect(api.patch).toHaveBeenCalledWith('/users/user-123/role', request)
       })

       it('should handle 400 error for self-role change', async () => {
         // Arrange
         const request: UpdateUserRoleRequest = {
           newRole: 'Admin',
           reason: null
         }

         vi.mocked(api.patch).mockRejectedValue({
           response: {
             status: 400,
             data: {
               error: { message: 'Users cannot change their own role' }
             }
           }
         })

         // Act
         const { updateUserRole, error } = useUsers()
         const result = await updateUserRole('user-123', request)

         // Assert
         expect(result).toBeNull()
         expect(error.value).toBe('Users cannot change their own role')
       })

       it('should handle 403 error for insufficient privileges', async () => {
         // Arrange
         const request: UpdateUserRoleRequest = {
           newRole: 'Admin',
           reason: null
         }

         vi.mocked(api.patch).mockRejectedValue({
           response: { status: 403 }
         })

         // Act
         const { updateUserRole, error } = useUsers()
         const result = await updateUserRole('user-123', request)

         // Assert
         expect(result).toBeNull()
         expect(error.value).toBe('Insufficient privileges to change this role')
       })

       it('should handle 404 error when user not found', async () => {
         // Arrange
         const request: UpdateUserRoleRequest = {
           newRole: 'Board',
           reason: null
         }

         vi.mocked(api.patch).mockRejectedValue({
           response: { status: 404 }
         })

         // Act
         const { updateUserRole, error } = useUsers()
         const result = await updateUserRole('nonexistent-user', request)

         // Assert
         expect(result).toBeNull()
         expect(error.value).toBe('User not found')
       })

       it('should update user in list after successful role change', async () => {
         // Arrange
         const mockUser = {
           id: 'user-123',
           email: 'john@example.com',
           firstName: 'John',
           lastName: 'Doe',
           phone: null,
           role: 'Member',
           isActive: true,
           createdAt: '2026-01-01T00:00:00Z',
           updatedAt: '2026-02-11T10:00:00Z'
         }
         const mockUpdatedUser = { ...mockUser, role: 'Board' }

         vi.mocked(api.get).mockResolvedValue({
           data: { success: true, data: [mockUser], error: null }
         })

         vi.mocked(api.patch).mockResolvedValue({
           data: { success: true, data: mockUpdatedUser, error: null }
         })

         // Act
         const { users, fetchUsers, updateUserRole } = useUsers()
         await fetchUsers()
         await updateUserRole('user-123', { newRole: 'Board', reason: null })

         // Assert
         expect(users.value[0].role).toBe('Board')
       })
     })
     ```

  3. Run tests: `npx vitest --run`
- **Dependencies**:
  - Vitest test utilities
  - `api` mock from `@/utils/api`
- **Implementation Notes**:
  - Tests cover success case, error cases (400, 403, 404), and reactivity
  - Uses AAA pattern (Arrange-Act-Assert)
  - Mocks API calls with predictable responses
  - Verifies that `users` array is updated after role change

---

### Step 8: Write Vitest Component Tests

- **File**: `frontend/src/components/users/__tests__/UserRoleDialog.test.ts`
- **Action**: Write component tests for `UserRoleDialog`
- **Implementation Steps**:
  1. Create test file `frontend/src/components/users/__tests__/UserRoleDialog.test.ts`:

     ```typescript
     import { describe, it, expect, vi, beforeEach } from 'vitest'
     import { mount } from '@vue/test-utils'
     import UserRoleDialog from '@/components/users/UserRoleDialog.vue'
     import { useUsers } from '@/composables/useUsers'
     import { useAuthStore } from '@/stores/auth'
     import type { User } from '@/types/user'

     // Mock composables
     vi.mock('@/composables/useUsers')
     vi.mock('@/stores/auth')

     const mockUser: User = {
       id: 'user-123',
       email: 'john@example.com',
       firstName: 'John',
       lastName: 'Doe',
       phone: null,
       role: 'Member',
       isActive: true,
       createdAt: '2026-01-01T00:00:00Z',
       updatedAt: '2026-02-11T10:00:00Z'
     }

     describe('UserRoleDialog', () => {
       let updateUserRoleMock: any
       let clearErrorMock: any

       beforeEach(() => {
         updateUserRoleMock = vi.fn().mockResolvedValue(mockUser)
         clearErrorMock = vi.fn()

         vi.mocked(useUsers).mockReturnValue({
           updateUserRole: updateUserRoleMock,
           loading: { value: false },
           error: { value: null },
           clearError: clearErrorMock
         } as any)

         vi.mocked(useAuthStore).mockReturnValue({
           isAdmin: true,
           isBoard: true,
           user: { id: 'admin-456', role: 'Admin' }
         } as any)
       })

       it('should render dialog with user information', () => {
         const wrapper = mount(UserRoleDialog, {
           props: {
             visible: true,
             user: mockUser
           }
         })

         expect(wrapper.text()).toContain('Update Role: John Doe')
         expect(wrapper.text()).toContain('Member')
       })

       it('should show warning when trying to change own role', () => {
         vi.mocked(useAuthStore).mockReturnValue({
           isAdmin: true,
           isBoard: true,
           user: { id: 'user-123', role: 'Admin' }  // Same as mockUser.id
         } as any)

         const wrapper = mount(UserRoleDialog, {
           props: {
             visible: true,
             user: mockUser
           }
         })

         expect(wrapper.text()).toContain('You cannot change your own role')
       })

       it('should only show Member role for Board users', () => {
         vi.mocked(useAuthStore).mockReturnValue({
           isAdmin: false,
           isBoard: true,
           user: { id: 'board-789', role: 'Board' }
         } as any)

         const wrapper = mount(UserRoleDialog, {
           props: {
             visible: true,
             user: mockUser
           }
         })

         // Verify dropdown options (implementation-specific)
         // This would need to inspect the Dropdown component's options prop
       })

       it('should emit roleUpdated event on successful update', async () => {
         const wrapper = mount(UserRoleDialog, {
           props: {
             visible: true,
             user: mockUser
           }
         })

         // Simulate form submission (implementation-specific)
         // This would trigger handleSubmit method

         await wrapper.vm.$nextTick()

         expect(updateUserRoleMock).toHaveBeenCalled()
       })

       it('should disable submit button when role unchanged', () => {
         const wrapper = mount(UserRoleDialog, {
           props: {
             visible: true,
             user: mockUser
           }
         })

         // Verify submit button is disabled when newRole === user.role
         // Implementation-specific based on canSubmit computed
       })
     })
     ```

  2. Create test file `frontend/src/components/users/__tests__/UserRoleCell.test.ts`:

     ```typescript
     import { describe, it, expect, vi } from 'vitest'
     import { mount } from '@vue/test-utils'
     import UserRoleCell from '@/components/users/UserRoleCell.vue'
     import { useAuthStore } from '@/stores/auth'
     import type { User } from '@/types/user'

     vi.mock('@/stores/auth')

     const mockUser: User = {
       id: 'user-123',
       email: 'john@example.com',
       firstName: 'John',
       lastName: 'Doe',
       phone: null,
       role: 'Member',
       isActive: true,
       createdAt: '2026-01-01T00:00:00Z',
       updatedAt: '2026-02-11T10:00:00Z'
     }

     describe('UserRoleCell', () => {
       it('should render role badge', () => {
         vi.mocked(useAuthStore).mockReturnValue({
           isAdmin: true,
           isBoard: true,
           user: { id: 'admin-456' }
         } as any)

         const wrapper = mount(UserRoleCell, {
           props: { user: mockUser }
         })

         expect(wrapper.text()).toContain('Member')
       })

       it('should show edit button for Admin users', () => {
         vi.mocked(useAuthStore).mockReturnValue({
           isAdmin: true,
           isBoard: true,
           user: { id: 'admin-456' }
         } as any)

         const wrapper = mount(UserRoleCell, {
           props: { user: mockUser }
         })

         expect(wrapper.find('button').exists()).toBe(true)
       })

       it('should hide edit button when editing own role', () => {
         vi.mocked(useAuthStore).mockReturnValue({
           isAdmin: true,
           isBoard: true,
           user: { id: 'user-123' }  // Same as mockUser.id
         } as any)

         const wrapper = mount(UserRoleCell, {
           props: { user: mockUser }
         })

         expect(wrapper.find('button').exists()).toBe(false)
       })

       it('should hide edit button for Board user trying to edit Admin', () => {
         const adminUser = { ...mockUser, role: 'Admin' as const }

         vi.mocked(useAuthStore).mockReturnValue({
           isAdmin: false,
           isBoard: true,
           user: { id: 'board-789' }
         } as any)

         const wrapper = mount(UserRoleCell, {
           props: { user: adminUser }
         })

         expect(wrapper.find('button').exists()).toBe(false)
       })

       it('should emit editRole event when button clicked', async () => {
         vi.mocked(useAuthStore).mockReturnValue({
           isAdmin: true,
           isBoard: true,
           user: { id: 'admin-456' }
         } as any)

         const wrapper = mount(UserRoleCell, {
           props: { user: mockUser }
         })

         await wrapper.find('button').trigger('click')

         expect(wrapper.emitted('editRole')).toHaveLength(1)
         expect(wrapper.emitted('editRole')![0]).toEqual([mockUser])
       })
     })
     ```

  3. Run tests: `npx vitest --run`
- **Dependencies**:
  - Vue Test Utils
  - Vitest
  - Component mocks
- **Implementation Notes**:
  - Tests verify role badge rendering
  - Tests verify edit button visibility logic
  - Tests verify self-change prevention
  - Tests verify Board member restrictions
  - Uses shallow mounting to isolate component logic

---

### Step 9: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/user-role-management.cy.ts`
- **Action**: Write end-to-end tests for role management user flow
- **Implementation Steps**:
  1. Create file `frontend/cypress/e2e/user-role-management.cy.ts`:

     ```typescript
     describe('User Role Management', () => {
       beforeEach(() => {
         // Login as Admin
         cy.login('admin@abuvi.org', 'password123')
         cy.visit('/admin/users')
       })

       it('should display users list with role badges', () => {
         cy.get('[data-testid="users-table"]').should('be.visible')
         cy.get('[data-testid="user-row"]').should('have.length.greaterThan', 0)
         cy.get('[data-testid="role-badge"]').should('exist')
       })

       it('should open role edit dialog when clicking edit button', () => {
         cy.get('[data-testid="role-edit-button"]').first().click()
         cy.get('[data-testid="role-dialog"]').should('be.visible')
         cy.get('[data-testid="role-dialog"]').should('contain.text', 'Update Role')
       })

       it('should successfully update user role', () => {
         // Find a Member user and edit their role
         cy.get('[data-testid="user-row"]')
           .contains('Member')
           .closest('[data-testid="user-row"]')
           .find('[data-testid="role-edit-button"]')
           .click()

         // Select new role
         cy.get('[data-testid="role-dropdown"]').click()
         cy.get('[data-testid="role-option-Board"]').click()

         // Add reason
         cy.get('[data-testid="reason-textarea"]').type(
           'Promotion for outstanding contributions'
         )

         // Submit
         cy.get('[data-testid="submit-button"]').click()

         // Verify success
         cy.get('[data-testid="toast-success"]').should('be.visible')
         cy.get('[data-testid="toast-success"]').should('contain.text', 'Role Updated')
       })

       it('should prevent self-role change', () => {
         // Try to edit own role (assumes admin user is in the list)
         cy.get('[data-testid="user-row"]')
           .contains('admin@abuvi.org')
           .closest('[data-testid="user-row"]')
           .find('[data-testid="role-edit-button"]')
           .should('not.exist')
       })

       it('should show error for unauthorized role change', () => {
         // Login as Board member
         cy.logout()
         cy.login('board@abuvi.org', 'password123')
         cy.visit('/admin/users')

         // Try to edit an Admin role (should not have edit button)
         cy.get('[data-testid="user-row"]')
           .contains('Admin')
           .closest('[data-testid="user-row"]')
           .find('[data-testid="role-edit-button"]')
           .should('not.exist')
       })

       it('should handle API errors gracefully', () => {
         // Intercept API call and return error
         cy.intercept('PATCH', '/api/users/*/role', {
           statusCode: 403,
           body: {
             success: false,
             error: { message: 'Insufficient privileges' }
           }
         }).as('updateRoleError')

         // Attempt role change
         cy.get('[data-testid="role-edit-button"]').first().click()
         cy.get('[data-testid="role-dropdown"]').click()
         cy.get('[data-testid="role-option-Board"]').click()
         cy.get('[data-testid="submit-button"]').click()

         // Verify error message
         cy.wait('@updateRoleError')
         cy.get('[data-testid="error-message"]').should('be.visible')
         cy.get('[data-testid="error-message"]').should(
           'contain.text',
           'Insufficient privileges'
         )
       })
     })

     describe('User Role Management - Board User', () => {
       beforeEach(() => {
         cy.login('board@abuvi.org', 'password123')
         cy.visit('/admin/users')
       })

       it('should only show Member role option for Board users', () => {
         cy.get('[data-testid="role-edit-button"]').first().click()
         cy.get('[data-testid="role-dropdown"]').click()

         // Verify only Member option available
         cy.get('[data-testid="role-option-Member"]').should('exist')
         cy.get('[data-testid="role-option-Board"]').should('not.exist')
         cy.get('[data-testid="role-option-Admin"]').should('not.exist')
       })

       it('should not show edit button for Admin or Board users', () => {
         // Admin users should not have edit button
         cy.get('[data-testid="user-row"]')
           .contains('Admin')
           .closest('[data-testid="user-row"]')
           .find('[data-testid="role-edit-button"]')
           .should('not.exist')

         // Board users (except self) should not have edit button
         cy.get('[data-testid="user-row"]')
           .contains('Board')
           .closest('[data-testid="user-row"]')
           .find('[data-testid="role-edit-button"]')
           .should('not.exist')
       })
     })
     ```

  2. Add `data-testid` attributes to components created in Steps 3-5
  3. Create Cypress custom commands if `cy.login()` doesn't exist:

     ```typescript
     // cypress/support/commands.ts
     Cypress.Commands.add('login', (email: string, password: string) => {
       cy.request('POST', '/api/auth/login', { email, password }).then((response) => {
         window.localStorage.setItem('abuvi_auth_token', response.body.data.token)
         window.localStorage.setItem('abuvi_user', JSON.stringify(response.body.data.user))
       })
     })

     Cypress.Commands.add('logout', () => {
       window.localStorage.removeItem('abuvi_auth_token')
       window.localStorage.removeItem('abuvi_user')
     })
     ```

  4. Run Cypress tests: `npx cypress open` or `npx cypress run`
- **Dependencies**:
  - Cypress
  - Custom commands (`cy.login`, `cy.logout`)
- **Implementation Notes**:
  - Tests cover complete user flows (Admin and Board)
  - Tests verify authorization logic
  - Tests verify error handling
  - Uses `data-testid` attributes for reliable element selection
  - Intercepts API calls to test error scenarios
  - Tests ensure Board members can only edit Member roles

---

### Step 10: Add `data-testid` Attributes to Components

- **Files**:
  - `frontend/src/components/users/UserRoleDialog.vue`
  - `frontend/src/components/users/UserRoleCell.vue`
  - `frontend/src/pages/UsersPage.vue`
- **Action**: Add `data-testid` attributes for Cypress test selectors
- **Implementation Steps**:
  1. **UserRoleDialog.vue**:
     - Add `data-testid="role-dialog"` to `<Dialog>` component
     - Add `data-testid="role-dropdown"` to `<Dropdown>` component
     - Add `data-testid="role-option-{role}"` to dropdown options
     - Add `data-testid="reason-textarea"` to `<Textarea>` component
     - Add `data-testid="submit-button"` to submit `<Button>`
     - Add `data-testid="error-message"` to error `<Message>` component
  2. **UserRoleCell.vue**:
     - Add `data-testid="role-badge"` to role badge `<span>`
     - Add `data-testid="role-edit-button"` to edit `<Button>`
  3. **UsersPage.vue**:
     - Add `data-testid="users-table"` to `<DataTable>`
     - Add `data-testid="user-row"` to each row (use DataTable's row class)
     - Add `data-testid="toast-success"` to success toast (if possible)
- **Dependencies**: None (HTML attributes)
- **Implementation Notes**:
  - `data-testid` attributes make E2E tests more reliable
  - Avoid using class names or IDs for test selectors
  - Keep `data-testid` values descriptive and unique

---

### Step 11: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes made during implementation:
     - New composable method: `updateUserRole` in `useUsers.ts`
     - New type: `UpdateUserRoleRequest` in `user.ts`
     - New components: `UserRoleDialog.vue`, `UserRoleCell.vue`
     - New or updated page: `UsersPage.vue`
     - New route: `/admin/users` with `requiresBoard` meta
  2. **Identify Documentation Files**: Determine which documentation files need updates:
     - `ai-specs/specs/api-spec.yml` - No update needed (backend API already documented)
     - `ai-specs/specs/frontend-standards.mdc` - No update needed (followed existing patterns)
     - `ai-specs/changes/user-role-changes/user-role-management.md` - Update status to "Implemented (Frontend)"
     - This file (`user-role-management_frontend.md`) - Add implementation notes section
  3. **Update Documentation**:
     - Update `ai-specs/changes/user-role-changes/user-role-management.md`:
       - Change status from "📋 Planned" to "✅ Implemented (Frontend)"
       - Add link to frontend implementation plan
     - Add implementation notes to this file (see below)
  4. **Verify Documentation**:
     - Confirm all changes are accurately reflected
     - Check that documentation follows established structure
  5. **Report Updates**: Document which files were updated and what changes were made
- **References**:
  - Follow process described in `ai-specs/specs/documentation-standards.mdc`
  - All documentation must be written in English
- **Notes**: This step is MANDATORY before considering the implementation complete.

**Implementation Notes to Add:**

```markdown
## Implementation Notes (Frontend)

**Completed**: 2026-02-11

### Components Created
- `UserRoleDialog.vue` - Modal dialog for role updates with validation
- `UserRoleCell.vue` - DataTable cell component with role badge and edit button
- `UsersPage.vue` - Users management page with DataTable

### Composable Extended
- `useUsers.ts` - Added `updateUserRole` method for PATCH `/users/{id}/role` endpoint

### Types Added
- `UpdateUserRoleRequest` - Interface for role update requests

### Security Implementation
- Client-side authorization checks prevent unauthorized UI access
- Self-role change prevented at UI level (button hidden, dialog shows warning)
- Board members only see "Member" role option in dropdown
- Route guard enforces `requiresBoard` meta for `/admin/users`

### Testing Coverage
- **Unit Tests**: `useUsers.test.ts` - 5 test cases for `updateUserRole` method
- **Component Tests**: `UserRoleDialog.test.ts`, `UserRoleCell.test.ts` - 9 test cases total
- **E2E Tests**: `user-role-management.cy.ts` - 8 test scenarios covering Admin and Board flows

### Known Limitations
- Audit trail display not implemented (backend stores audit logs, but frontend doesn't show history yet)
- No bulk role updates (single user at a time)
- No email notifications on role change (future enhancement)
```

---

## Implementation Order

Execute steps in the following sequence:

1. **Step 0**: Create Feature Branch (`feature/user-role-management-frontend`)
2. **Step 1**: Define TypeScript Interfaces (`UpdateUserRoleRequest`)
3. **Step 2**: Extend `useUsers` Composable (add `updateUserRole` method)
4. **Step 3**: Create `UserRoleDialog` Component
5. **Step 4**: Create `UserRoleCell` Component
6. **Step 5**: Create or Update Users Management Page
7. **Step 6**: Update Routing Configuration
8. **Step 7**: Write Vitest Unit Tests for Composable
9. **Step 8**: Write Vitest Component Tests
10. **Step 9**: Write Cypress E2E Tests
11. **Step 10**: Add `data-testid` Attributes
12. **Step 11**: Update Technical Documentation

---

## Testing Checklist

Post-implementation verification:

### Functionality

- [ ] Users table displays all users with role badges
- [ ] Edit button only visible for authorized users
- [ ] Role dialog opens and displays current role
- [ ] Role dropdown shows correct options based on user role
- [ ] Self-role change is prevented (button hidden, dialog shows warning)
- [ ] Board members can only change Member roles
- [ ] Admin can change any role
- [ ] Success toast notification appears after role update
- [ ] Error messages display correctly for API failures
- [ ] Reason field accepts up to 500 characters

### Vitest Unit Tests

- [ ] All composable tests pass (`useUsers.test.ts`)
- [ ] All component tests pass (`UserRoleDialog.test.ts`, `UserRoleCell.test.ts`)
- [ ] Test coverage ≥90% for new code

### Cypress E2E Tests

- [ ] All Admin flow tests pass
- [ ] All Board flow tests pass
- [ ] Error handling tests pass
- [ ] Authorization tests pass

### Component Rendering

- [ ] Role badges use correct colors (Admin=red, Board=blue, Member=gray)
- [ ] Dialog layout is responsive
- [ ] Buttons disable during loading
- [ ] Loading spinner shows during API calls

### Error Handling

- [ ] 400 error (self-role change) shows correct message
- [ ] 403 error (insufficient privileges) shows correct message
- [ ] 404 error (user not found) shows correct message
- [ ] Network errors handled gracefully

---

## Error Handling Patterns

### Composable Error Handling

```typescript
// In useUsers.ts
catch (err: any) {
  if (err.response?.status === 400) {
    error.value = err.response.data?.error?.message || 'Invalid role change operation'
  } else if (err.response?.status === 403) {
    error.value = 'Insufficient privileges to change this role'
  } else if (err.response?.status === 404) {
    error.value = 'User not found'
  } else {
    error.value = 'Failed to update user role. Please try again.'
  }
  console.error('updateUserRole error:', err)
  return null
}
```

### Component Error Display

```vue
<!-- In UserRoleDialog.vue -->
<Message v-if="error" severity="error" :closable="false">
  {{ error }}
</Message>
```

### Toast Notifications

```typescript
// In UsersPage.vue
toast.add({
  severity: 'success',
  summary: 'Role Updated',
  detail: `${updatedUser.firstName} ${updatedUser.lastName}'s role updated to ${updatedUser.role}`,
  life: 5000
})
```

---

## UI/UX Considerations

### PrimeVue Components Used

- **Dialog**: Modal for role update form (`v-model:visible` pattern)
- **Dropdown**: Role selection with option filtering
- **Textarea**: Reason input with character counter
- **Button**: Actions (edit, cancel, submit) with loading states
- **Message**: Error and warning display
- **DataTable**: Users list with pagination and sorting

### Tailwind CSS Classes

- Responsive grid layouts: `grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3`
- Spacing consistency: `gap-4`, `p-6`, `mt-2`
- Role badge colors: `bg-red-100 text-red-800` (Admin), `bg-blue-100 text-blue-800` (Board), `bg-gray-100 text-gray-800` (Member)
- Status badges: `bg-green-100 text-green-800` (Active), `bg-gray-100 text-gray-600` (Inactive)

### Responsive Design

- DataTable uses `responsive-layout="scroll"` for mobile
- Dialog is `w-full max-w-md` for consistent sizing
- Grid layouts adapt: `grid-cols-1 md:grid-cols-2 lg:grid-cols-3`

### Accessibility

- Proper labels for form inputs (`<label for="newRole">`)
- `aria-label` for icon-only buttons (`aria-label="Edit role"`)
- Keyboard navigation supported by PrimeVue components
- Semantic HTML structure

### Loading States

- Submit button shows spinner during API call (`:loading="loading"`)
- Submit button disabled during submission (`:disabled="!canSubmit || loading"`)
- DataTable shows loading skeleton during fetch

### User Feedback

- Success: Toast notification with user name and new role
- Error: Red Message component with specific error message
- Warning: Yellow Message component for self-role change attempt
- Validation: Inline validation messages and disabled submit button

---

## Dependencies

### npm Packages (Already Installed)

- `vue` (v3.x) - Framework
- `pinia` (v2.x) - State management
- `vue-router` (v4.x) - Routing
- `axios` (v1.x) - HTTP client
- `primevue` (v3.x) - UI components
- `tailwindcss` (v3.x) - CSS framework
- `typescript` (v5.x) - Type safety
- `vitest` (v1.x) - Unit testing
- `@vue/test-utils` (v2.x) - Component testing
- `cypress` (v13.x) - E2E testing

### PrimeVue Components Used

- `Dialog` - Modal dialogs
- `Dropdown` - Role selection
- `Textarea` - Reason input
- `Button` - Actions
- `Message` - Error/warning display
- `DataTable` - Users list
- `Column` - Table columns
- `useToast` - Toast notifications

### No New Dependencies Required

All required packages are already part of the project. No additional npm installs needed.

---

## Notes

### Important Reminders

- **Language**: All code, comments, and UI text must be in English
- **TypeScript**: Strict typing required, no `any` types
- **No Custom Styles**: Use Tailwind CSS utilities exclusively
- **Composition API**: Always use `<script setup lang="ts">`
- **Security**: Frontend checks are UX only; backend enforces security
- **Testing**: Write tests BEFORE implementation (TDD) - but this plan was created after backend implementation

### Business Rules

- Users cannot change their own role (enforced backend + frontend UX)
- Board members can only change Member roles (enforced backend + frontend UX)
- Admin can change any role (enforced backend + frontend UX)
- Role changes are logged with audit trail (backend only, not displayed in UI yet)

### Known Limitations

- **Audit Trail Display**: Backend stores audit logs, but frontend doesn't display role change history yet (future enhancement)
- **Bulk Operations**: No bulk role updates (single user at a time)
- **Email Notifications**: No email notifications on role change (future enhancement)
- **Role Approval Workflow**: No approval workflow for role changes (future enhancement)

### Performance Considerations

- DataTable pagination limits rows loaded at once
- Lazy loading of Users page with `() => import()`
- Composable maintains single `users` array for reactivity
- No caching of role update results (always fetch fresh data)

### Browser Compatibility

- Modern browsers only (ES2015+)
- Tested on Chrome, Firefox, Edge, Safari
- Mobile responsive design

---

## Next Steps After Implementation

1. **Code Review**: Request code review from team before merging
2. **Integration Testing**: Test with backend API in development environment
3. **Merge Backend Branch**: Ensure `feature/user-role-management-backend` is merged to `main` first
4. **Merge Frontend Branch**: Merge `feature/user-role-management-frontend` after backend
5. **Deployment**: Deploy to staging environment for QA testing
6. **User Documentation**: Update user guide with role management instructions (if applicable)
7. **Future Enhancements**:
   - Audit trail display in UI
   - Bulk role updates
   - Email notifications
   - Role approval workflow

---

## Implementation Verification

Final verification checklist:

### Code Quality

- [ ] TypeScript strict mode enabled, no `any` types
- [ ] All components use `<script setup lang="ts">`
- [ ] No custom `<style>` blocks (Tailwind only)
- [ ] ESLint passes with no errors
- [ ] Prettier formatting applied

### Functionality

- [ ] All user flows work end-to-end
- [ ] Role updates persist to backend
- [ ] Authorization checks work correctly
- [ ] Error handling covers all scenarios

### Testing

- [ ] All Vitest unit tests pass
- [ ] All Vitest component tests pass
- [ ] All Cypress E2E tests pass
- [ ] Test coverage ≥90%

### Integration

- [ ] Composables connect to backend API correctly
- [ ] API responses handled properly (success + errors)
- [ ] Backend endpoints match frontend expectations
- [ ] Authentication token included in requests

### Documentation

- [ ] This implementation plan updated with notes
- [ ] Main spec updated with "Implemented" status
- [ ] Code comments added where necessary
- [ ] README updated (if applicable)

---

**Implementation Plan Complete** ✅

This plan provides a comprehensive, step-by-step guide for implementing the user role management feature in the Abuvi frontend. Follow the steps in order, write tests alongside code, and ensure all verification checks pass before considering the implementation complete.

# Frontend Implementation Plan: Delete Camp Registration

## 1. Overview

Implement a "Delete Registration" feature in the frontend, allowing family representatives (within 24 hours of creation) and admin/board users to permanently delete `Pending`/`Draft` camp registrations. This follows Vue 3 Composition API patterns with PrimeVue components and Tailwind CSS styling, mirroring the existing cancel registration flow.

## 2. Architecture Context

### Components/Composables Involved

| Type | File | Action |
|------|------|--------|
| Composable | `frontend/src/composables/useRegistrations.ts` | Add `deleteRegistration()` method |
| Component (new) | `frontend/src/components/registrations/RegistrationDeleteDialog.vue` | Confirmation dialog |
| View | `frontend/src/views/registrations/RegistrationDetailPage.vue` | Add delete button + wire dialog |
| Types | `frontend/src/types/registration.ts` | No changes needed (existing types sufficient) |

### State Management

- **Local state** via composable refs (`loading`, `error`) — no Pinia store needed (consistent with current registration pattern)
- Auth store (`useAuthStore`) already provides `isAdmin`, `isBoard` for permission checks

### Routing

- No new routes needed
- On successful delete: navigate to `/registrations` (list view)

---

## 3. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/delete-camp-registration-frontend`
- **Implementation Steps**:
  1. Ensure you're on the latest `main` branch
  2. Pull latest changes: `git pull origin main`
  3. Create new branch: `git checkout -b feature/delete-camp-registration-frontend`
  4. Verify branch creation: `git branch`
- **Notes**: This must be the FIRST step before any code changes.

---

### Step 1: Add `deleteRegistration` to Composable

- **File**: `frontend/src/composables/useRegistrations.ts`
- **Action**: Add a new `deleteRegistration` method following the existing `cancelRegistration` pattern

- **Function Signature**:
  ```typescript
  const deleteRegistration = async (id: string): Promise<boolean>
  ```

- **Implementation Steps**:
  1. Set `loading.value = true`, `error.value = null`
  2. Call `await api.delete(`/registrations/${id}`)`
  3. On success: return `true`
  4. On error: extract message from `err.response?.data?.error?.message`, set `error.value`, return `false`
  5. In `finally`: set `loading.value = false`

- **Implementation Notes**:
  - Follow the exact same pattern as `deleteFamilyUnit` in `useFamilyUnits.ts`
  - The API returns `204 NoContent` on success (no response body to parse)
  - Error responses use the `ApiResponse` envelope — extract `error.message` for display
  - Include the new method in the composable's return object

---

### Step 2: Create `RegistrationDeleteDialog` Component

- **File**: `frontend/src/components/registrations/RegistrationDeleteDialog.vue` (new file)
- **Action**: Create a confirmation dialog mirroring `RegistrationCancelDialog.vue`

- **Component Interface**:
  ```typescript
  // Props
  visible: boolean
  loading: boolean

  // Emits
  'update:visible': [value: boolean]
  'confirm': []
  ```

- **Implementation Steps**:
  1. Use PrimeVue `Dialog` with `v-model:visible` and `modal` prop
  2. Header: `"Delete registration"` (English only per standards)
  3. Body content:
     - Warning icon (`pi pi-exclamation-triangle`) with danger color
     - Main message: "Are you sure you want to delete this registration? This action cannot be undone."
     - Secondary message: "You will be able to register again for this camp edition."
  4. Footer with two buttons:
     - "Cancel" — `severity="secondary"`, emits `update:visible` with `false`
     - "Delete registration" — `severity="danger"`, `icon="pi pi-trash"`, `:loading="loading"`, emits `confirm`
  5. Disable close button while loading: `:closable="!loading"`

- **Implementation Notes**:
  - Use `class="w-full max-w-md"` on Dialog for responsive sizing (consistent with existing dialogs)
  - Use Tailwind utilities only — no `<style>` blocks
  - Add `data-testid="delete-registration-dialog"` for testing

---

### Step 3: Update `RegistrationDetailPage` View

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Add delete button and wire up the dialog

- **Implementation Steps**:

  1. **Import the new dialog component**:
     ```typescript
     import RegistrationDeleteDialog from '@/components/registrations/RegistrationDeleteDialog.vue'
     ```

  2. **Add reactive state**:
     ```typescript
     const showDeleteDialog = ref(false)
     const deleting = ref(false)
     ```

  3. **Add permission computed**:
     ```typescript
     const canDelete = computed(() => {
       if (!registration.value) return false
       const status = registration.value.status
       // Only Pending or Draft can be deleted
       if (status !== 'Pending' && status !== 'Draft') return false
       // Representative or admin/board
       return isRepresentative.value || isAdminOrBoard.value
     })
     ```
     Note: The 24-hour time window is enforced server-side. The frontend shows the button optimistically — if the window has expired, the API will return a clear error message displayed via toast.

  4. **Add delete handler**:
     ```typescript
     const handleDelete = async () => {
       deleting.value = true
       const success = await deleteRegistration(registrationId.value)
       deleting.value = false
       showDeleteDialog.value = false
       if (success) {
         toast.add({
           severity: 'success',
           summary: 'Registration deleted',
           detail: 'Your registration has been deleted. You can register again for this camp edition.',
           life: 4000
         })
         router.push('/registrations')
       } else {
         toast.add({
           severity: 'error',
           summary: 'Error',
           detail: error.value || 'Could not delete the registration.',
           life: 5000
         })
       }
     }
     ```

  5. **Add delete button in template** (next to the existing cancel button area):
     ```vue
     <Button
       v-if="canDelete"
       label="Delete registration"
       severity="danger"
       icon="pi pi-trash"
       @click="showDeleteDialog = true"
       data-testid="delete-registration-btn"
     />
     ```
     Place it in the existing action buttons area (the `<div class="flex justify-end">` block at the bottom). If both cancel and delete are available, show them side by side with a gap.

  6. **Add dialog in template**:
     ```vue
     <RegistrationDeleteDialog
       v-model:visible="showDeleteDialog"
       :loading="deleting"
       @confirm="handleDelete"
     />
     ```

  7. **Destructure `deleteRegistration` from composable**:
     Update the existing `useRegistrations()` destructure to include `deleteRegistration`.

- **Implementation Notes**:
  - The delete button replaces or coexists with the cancel button depending on status
  - For `Draft` registrations: show delete only (cancel makes no sense for drafts)
  - For `Pending` registrations: show both cancel and delete buttons
  - On successful delete, redirect to `/registrations` list (unlike cancel which stays on page)
  - The 24-hour window check happens server-side — if expired, the error toast will explain

---

### Step 4: Write Unit Tests

- **File**: `frontend/src/components/registrations/__tests__/RegistrationDeleteDialog.spec.ts` (new file)
- **Action**: Write Vitest unit tests for the dialog component

- **Test Cases**:

  | Test | Description |
  |------|-------------|
  | `renders dialog when visible is true` | Dialog content visible, buttons present |
  | `emits update:visible with false when Cancel clicked` | Secondary button triggers close |
  | `emits confirm when Delete button clicked` | Danger button triggers confirm |
  | `shows loading state on Delete button` | When `loading=true`, button shows spinner |
  | `disables close when loading` | Dialog not closable during deletion |

- **File**: `frontend/src/composables/__tests__/useRegistrations.spec.ts` (update existing or create)
- **Action**: Add tests for `deleteRegistration` method

- **Test Cases**:

  | Test | Description |
  |------|-------------|
  | `deleteRegistration returns true on 204` | Successful API call |
  | `deleteRegistration returns false and sets error on failure` | API returns 409/422, error extracted |
  | `deleteRegistration sets loading during call` | Loading ref toggled correctly |

- **Implementation Notes**:
  - Use `@vue/test-utils` with `mount`/`shallowMount`
  - Mock `api` (axios instance) with `vi.mock`
  - Follow existing test patterns in the project

---

### Step 5: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: All code changes from steps 1-4
  2. **Update `ai-specs/specs/api-spec.yml`**: Ensure `DELETE /api/registrations/{id}` is documented (may already be done in backend plan)
  3. **Verify**: All documentation is in English, follows existing structure
- **Notes**: This step is MANDATORY before considering the implementation complete.

---

## 4. Implementation Order

1. **Step 0**: Create feature branch `feature/delete-camp-registration-frontend`
2. **Step 1**: Add `deleteRegistration` to `useRegistrations` composable
3. **Step 2**: Create `RegistrationDeleteDialog` component
4. **Step 3**: Update `RegistrationDetailPage` with button, dialog, and handler
5. **Step 4**: Write unit tests
6. **Step 5**: Update technical documentation

## 5. Testing Checklist

### Unit Tests (Vitest)
- [ ] `RegistrationDeleteDialog` renders correctly when visible
- [ ] `RegistrationDeleteDialog` emits close on Cancel click
- [ ] `RegistrationDeleteDialog` emits confirm on Delete click
- [ ] `RegistrationDeleteDialog` shows loading state
- [ ] `deleteRegistration` composable returns true on success
- [ ] `deleteRegistration` composable handles API errors

### Manual Verification
- [ ] Delete button visible for `Pending`/`Draft` registrations (representative)
- [ ] Delete button visible for admin/board users
- [ ] Delete button NOT visible for `Confirmed`/`Cancelled` registrations
- [ ] Confirmation dialog opens on button click
- [ ] Dialog closes on Cancel
- [ ] Successful deletion shows success toast and redirects to list
- [ ] API error shows error toast with message from backend
- [ ] Time window expiration error displays clearly
- [ ] Payment guard error displays clearly

## 6. Error Handling Patterns

| Error Source | HTTP Status | User-Facing Message | Toast Severity |
|-------------|-------------|---------------------|----------------|
| Registration not found | 404 | "Registration not found." | `error` |
| Not authorized | 403 | "You are not authorized to delete this registration." | `error` |
| Status blocked | 422 | Server message (e.g., "Confirmed registrations cannot be deleted.") | `error` |
| Time window expired | 422 | "Registration can only be deleted within 24 hours of creation." | `error` |
| Payments exist | 409 | "Cannot delete registration with existing payments." | `error` |
| Network error | — | "Could not delete the registration." | `error` |

Error messages are extracted from the API response `error.message` field. The fallback message is used only for unexpected errors.

## 7. UI/UX Considerations

- **Button placement**: In the action buttons area at the bottom of `RegistrationDetailPage`, alongside the existing cancel button
- **Button style**: `severity="danger"`, `icon="pi pi-trash"` — consistent with destructive actions in the codebase
- **Dialog**: Modal, centered, `max-w-md`, with warning icon and clear consequences text
- **Loading state**: Button spinner during API call, dialog not closable
- **Post-delete redirect**: Navigate to `/registrations` with success toast (user can't stay on a deleted page)
- **Responsive**: Dialog uses `w-full max-w-md` for mobile-friendly sizing
- **Accessibility**: Dialog is modal (focus trap), buttons have clear labels, `data-testid` attributes for testing

## 8. Dependencies

- **No new npm packages** required
- **PrimeVue components used**: `Dialog`, `Button` (already imported in the project)
- **Icons**: `pi pi-trash`, `pi pi-exclamation-triangle` (already available via PrimeIcons)

## 9. Notes

- **Language**: All user-facing text in English (per project standards). Note: existing cancel dialog uses Spanish — this may need a separate i18n cleanup but is out of scope for this ticket.
- **Time window**: The 24-hour restriction is enforced server-side only. The frontend does NOT calculate the window — it always shows the delete button for eligible statuses and relies on the API error for expired windows. This avoids clock sync issues and keeps the logic in one place.
- **Admin view**: Admin users will see the delete button for any `Pending`/`Draft` registration, regardless of family ownership. The backend handles the authorization.
- **TypeScript strict**: All new code must be fully typed with no `any` types.

## 10. Next Steps After Implementation

- Verify integration with the backend `DELETE /api/registrations/{id}` endpoint
- Consider adding Cypress E2E test for the full delete flow (optional, based on project coverage goals)
- PR review and merge to `main`

## 11. Implementation Verification

- [ ] **Code Quality**: TypeScript strict, no `any`, `<script setup lang="ts">`, no `<style>` blocks
- [ ] **Functionality**: Delete button appears for correct statuses/roles, dialog confirms, API call succeeds, redirect works
- [ ] **Error Handling**: All API error scenarios display appropriate toast messages
- [ ] **Testing**: Vitest unit tests pass for dialog component and composable method
- [ ] **Integration**: Composable correctly calls `DELETE /api/registrations/{id}` and handles all response codes
- [ ] **Documentation**: Updated as needed

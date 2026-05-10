# Frontend Implementation Plan: feat-registration-status-flow — Registration Status Flow

## Overview

Implements the frontend half of the registration status lifecycle redesign. New statuses `PartiallyPaid` and `FullyPaid` are added to the type union; every status transition is now visible in a status history timeline; the user sees a "Confirmar cambios" action banner when a board admin has edited their registration; and admins get a dedicated status-change UI and a "notify family" toggle when saving edits.

Architecture follows the project's established patterns: Vue 3 Composition API (`<script setup lang="ts">`), composables for all API calls, PrimeVue + Tailwind CSS for UI, no custom `<style>` blocks.

---

## Architecture Context

### Files modified
| File | Change |
|------|--------|
| `frontend/src/types/registration.ts` | New statuses, new request/response types, `statusHistory` and `hasPendingUserAcknowledgement` on response |
| `frontend/src/composables/useRegistrations.ts` | Add `changeStatus`, `confirmChanges`, `adminUpdate` |
| `frontend/src/components/registrations/RegistrationStatusBadge.vue` | Add `PartiallyPaid`, `FullyPaid`; update `Draft` colour/label |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | "Confirmar cambios" banner, status timeline, admin status-change section, admin save toggle |
| `frontend/src/views/registrations/RegistrationsPage.vue` | Include new active statuses in sort |
| `frontend/src/components/admin/RegistrationsAdminPanel.vue` | Add new statuses to filter dropdown |

### Files created
| File | Purpose |
|------|---------|
| `frontend/src/components/registrations/RegistrationStatusTimeline.vue` | Vertical timeline for status history |
| `frontend/src/components/registrations/AdminStatusChangeDialog.vue` | Admin manual status-change dialog |

### No new routes needed
All new UI is embedded in `RegistrationDetailPage` (same `/registrations/:id` route).

### State management
No Pinia store needed. All new state lives in composable reactive refs + local `ref()` in `RegistrationDetailPage`.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the frontend feature branch.
- **Branch name**: `feature/feat-registration-status-flow-frontend`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-registration-status-flow-frontend`
  3. `git branch` — verify you are on the new branch before any changes.
- **Note**: The backend branch is `feature/feat-registration-status-flow-backend`. This frontend branch is separate. During development point `VITE_API_URL` at the backend running locally from that branch, or use a shared dev env.

---

### Step 1: Update TypeScript Types

- **File**: `frontend/src/types/registration.ts`
- **Action**: Extend existing types and add new ones required by the new API contract.

**Implementation Steps**:

1. **Extend `RegistrationStatus`** (line 4):
   ```ts
   export type RegistrationStatus =
     | 'Pending'
     | 'PartiallyPaid'
     | 'FullyPaid'
     | 'Confirmed'
     | 'Draft'
     | 'Cancelled'
   ```

2. **Add `StatusChangeTrigger` type** (after `RegistrationStatus`):
   ```ts
   export type StatusChangeTrigger = 'Automatic' | 'AdminAction' | 'UserConfirmed'
   ```

3. **Add `RegistrationStatusHistoryEntry` interface**:
   ```ts
   export interface RegistrationStatusHistoryEntry {
     id: string
     previousStatus: RegistrationStatus
     newStatus: RegistrationStatus
     changedAt: string         // ISO 8601
     changedByUserName: string | null   // null for Automatic trigger
     trigger: StatusChangeTrigger
     notes: string | null
   }
   ```

4. **Update `RegistrationListItem`** — add `hasPendingUserAcknowledgement`:
   ```ts
   export interface RegistrationListItem {
     // ... existing fields ...
     hasPendingUserAcknowledgement: boolean
   }
   ```

5. **Update `RegistrationResponse`** — add three fields:
   ```ts
   export interface RegistrationResponse {
     // ... existing fields ...
     draftTargetStatus: RegistrationStatus | null
     hasPendingUserAcknowledgement: boolean
     statusHistory: RegistrationStatusHistoryEntry[]
   }
   ```

6. **Add request types** (at the bottom of the file):
   ```ts
   export interface ChangeRegistrationStatusRequest {
     newStatus: RegistrationStatus
     notes: string
     notifyUser: boolean
   }

   export interface AdminUpdateRegistrationRequest {
     members?: MemberAttendanceRequest[]
     extras?: ExtraSelectionRequest[]
     specialNeeds?: string | null
     campatesPreference?: string | null
     hasPet?: boolean
     notifyUser: boolean
     draftTargetStatus: RegistrationStatus | null
   }
   ```

---

### Step 2: Update `useRegistrations` Composable

- **File**: `frontend/src/composables/useRegistrations.ts`
- **Action**: Add three new API methods: `changeStatus`, `confirmChanges`, `adminUpdateRegistration`.

**Implementation Steps**:

1. Import the new types at the top:
   ```ts
   import type {
     // existing imports ...
     ChangeRegistrationStatusRequest,
     AdminUpdateRegistrationRequest,
   } from '@/types/registration'
   ```

2. Add `changeStatus` (calls `PATCH /api/registrations/{id}/status`):
   ```ts
   const changeStatus = async (
     id: string,
     request: ChangeRegistrationStatusRequest
   ): Promise<RegistrationResponse | null> => {
     loading.value = true
     error.value = null
     try {
       const response = await api.patch<ApiResponse<RegistrationResponse>>(
         `/registrations/${id}/status`,
         request
       )
       if (response.data.success && response.data.data) {
         registration.value = response.data.data
         return response.data.data
       }
       return null
     } catch (err: unknown) {
       error.value =
         (err as { response?: { data?: { error?: { message?: string } } } })
           ?.response?.data?.error?.message ?? 'Error al cambiar estado'
       return null
     } finally {
       loading.value = false
     }
   }
   ```

3. Add `confirmChanges` (calls `POST /api/registrations/{id}/confirm-changes`):
   ```ts
   const confirmChanges = async (id: string): Promise<RegistrationResponse | null> => {
     loading.value = true
     error.value = null
     try {
       const response = await api.post<ApiResponse<RegistrationResponse>>(
         `/registrations/${id}/confirm-changes`
       )
       if (response.data.success && response.data.data) {
         registration.value = response.data.data
         return response.data.data
       }
       return null
     } catch (err: unknown) {
       error.value =
         (err as { response?: { data?: { error?: { message?: string } } } })
           ?.response?.data?.error?.message ?? 'Error al confirmar cambios'
       return null
     } finally {
       loading.value = false
     }
   }
   ```

4. Add `adminUpdateRegistration` (calls `PUT /api/registrations/{id}/admin`):
   ```ts
   const adminUpdateRegistration = async (
     id: string,
     request: AdminUpdateRegistrationRequest
   ): Promise<RegistrationResponse | null> => {
     loading.value = true
     error.value = null
     try {
       const response = await api.put<ApiResponse<RegistrationResponse>>(
         `/registrations/${id}/admin`,
         request
       )
       if (response.data.success && response.data.data) {
         registration.value = response.data.data
         return response.data.data
       }
       return null
     } catch (err: unknown) {
       error.value =
         (err as { response?: { data?: { error?: { message?: string } } } })
           ?.response?.data?.error?.message ?? 'Error al actualizar inscripción'
       return null
     } finally {
       loading.value = false
     }
   }
   ```

5. Include the three new functions in the `return` statement.

---

### Step 3: Update `RegistrationStatusBadge` Component

- **File**: `frontend/src/components/registrations/RegistrationStatusBadge.vue`
- **Action**: Add display config for `PartiallyPaid` and `FullyPaid`; update `Draft` to use orange/warning colour and Spanish label matching the spec.

**Implementation Steps**:

Replace the `configs` object in the `statusConfig` computed:

```ts
const configs: Record<RegistrationStatus, { label: string; colorClass: string }> = {
  Pending:       { label: 'Pendiente',     colorClass: 'bg-yellow-100 text-yellow-800' },
  PartiallyPaid: { label: 'Al corriente',  colorClass: 'bg-blue-100 text-blue-800' },
  FullyPaid:     { label: 'Pago completo', colorClass: 'bg-teal-100 text-teal-700' },
  Confirmed:     { label: 'Confirmada',    colorClass: 'bg-green-100 text-green-800' },
  Draft:         { label: 'En revisión',   colorClass: 'bg-orange-100 text-orange-800' },
  Cancelled:     { label: 'Cancelada',     colorClass: 'bg-gray-100 text-gray-600' },
}
```

**Note**: `Draft` label changed from 'Borrador' → 'En revisión', and colour from blue → orange. This aligns with the spec (section 7.1). Any existing Cypress/Vitest snapshots that assert the 'Borrador' label must be updated.

---

### Step 4: Create `RegistrationStatusTimeline` Component

- **File**: `frontend/src/components/registrations/RegistrationStatusTimeline.vue` *(new)*
- **Action**: Vertical timeline rendering `RegistrationStatusHistoryEntry[]`.

**Component Signature**:
```ts
defineProps<{
  history: RegistrationStatusHistoryEntry[]
}>()
```

**Implementation Steps**:

1. Import `RegistrationStatusHistoryEntry`, `RegistrationStatus`, `StatusChangeTrigger`.

2. Define a helper `formatDateTime(iso: string): string` — uses `Intl.DateTimeFormat` with `'es-ES'` locale, day/month/year + hour/minute.

3. Define `entryIcon(entry: RegistrationStatusHistoryEntry): string` returning a PrimeIcons class:
   - `trigger === 'Automatic'` → `'pi pi-bolt'`
   - `newStatus === 'Draft'` → `'pi pi-pencil'`
   - `newStatus === 'Cancelled'` → `'pi pi-times-circle'`
   - default → `'pi pi-check-circle'`

4. Define `entryDescription(entry: RegistrationStatusHistoryEntry): string` — human-readable Spanish text:
   | newStatus | Description |
   |-----------|------------|
   | `Pending` | `'Inscripción creada — Pendiente'` |
   | `PartiallyPaid` | `'Junta confirmó primer pago — Al corriente'` |
   | `FullyPaid` | `'Todos los pagos recibidos — Pago completo'` |
   | `Confirmed` | `'Junta confirmó inscripción — Confirmada'` |
   | `Draft` | `'Junta realizó cambios — En revisión'` |
   | `Cancelled` | `'Inscripción cancelada'` |

5. Template: use PrimeVue `Timeline` component (`primevue/timeline`) with `value` bound to `history` prop (sorted ascending by `changedAt`):
   - `#marker` slot: `<i :class="entryIcon(item)" />`
   - `#content` slot:
     ```html
     <div class="pb-4">
       <p class="text-sm font-medium text-gray-800">{{ entryDescription(item) }}</p>
       <p class="text-xs text-gray-400">{{ formatDateTime(item.changedAt) }}</p>
       <p v-if="item.changedByUserName" class="text-xs text-gray-500">
         {{ item.trigger === 'Automatic' ? 'Sistema' : item.changedByUserName }}
       </p>
       <p v-if="item.notes" class="mt-1 text-xs italic text-gray-500">{{ item.notes }}</p>
     </div>
     ```

6. Wrap in a `<section>` with heading `"Historial de cambios"` visible only when `history.length > 0`.

**Dependencies**: PrimeVue `Timeline` component — check it is registered globally in `main.ts` or import it locally. If not globally registered, add it to `main.ts` alongside other PrimeVue components.

---

### Step 5: Create `AdminStatusChangeDialog` Component

- **File**: `frontend/src/components/registrations/AdminStatusChangeDialog.vue` *(new)*
- **Action**: Dialog for board/admin to manually change registration status.

**Component Signature**:
```ts
const props = defineProps<{
  visible: boolean
  registrationId: string
  currentStatus: RegistrationStatus
  loading: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  changed: [registration: RegistrationResponse]
}>()
```

**Implementation Steps**:

1. Import `RegistrationStatus`, `RegistrationResponse`, `ChangeRegistrationStatusRequest`, `useRegistrations`.

2. Define `validTargetStatuses` computed from `currentStatus` (implement the allowed transition map):
   ```ts
   const validTargetStatuses = computed((): { label: string; value: RegistrationStatus }[] => {
     const map: Partial<Record<RegistrationStatus, RegistrationStatus[]>> = {
       Pending:       ['PartiallyPaid'],
       PartiallyPaid: ['Pending'],
       FullyPaid:     ['Confirmed', 'PartiallyPaid'],
       Confirmed:     ['FullyPaid'],
       Draft:         ['Pending', 'PartiallyPaid', 'FullyPaid', 'Confirmed'],
     }
     const labelMap: Record<RegistrationStatus, string> = {
       Pending:       'Pendiente',
       PartiallyPaid: 'Al corriente',
       FullyPaid:     'Pago completo',
       Confirmed:     'Confirmada',
       Draft:         'En revisión',
       Cancelled:     'Cancelada',
     }
     return (map[props.currentStatus] ?? []).map(s => ({ label: labelMap[s], value: s }))
   })
   ```
   Note: `Cancelled` is excluded (use existing cancel endpoint). `Draft` is excluded from target (set automatically on edit).

3. Local form state:
   ```ts
   const selectedStatus = ref<RegistrationStatus | null>(null)
   const notes = ref('')
   const notifyUser = ref(true)
   ```

4. `handleSubmit`:
   - Validate `selectedStatus` and `notes` are non-empty.
   - Call `changeStatus(registrationId, { newStatus: selectedStatus.value!, notes: notes.value, notifyUser: notifyUser.value })`.
   - On success: emit `changed` with returned registration, close dialog, reset form.
   - On failure: show PrimeVue Toast error.

5. `handleClose`: reset form, emit `update:visible` false.

6. Template — PrimeVue `Dialog`:
   ```html
   <Dialog :visible="visible" @update:visible="handleClose" header="Cambiar estado" :modal="true" :style="{ width: '30rem' }">
     <div class="space-y-4">
       <div>
         <label class="mb-1 block text-sm font-medium">Nuevo estado</label>
         <Select v-model="selectedStatus" :options="validTargetStatuses"
                 optionLabel="label" optionValue="value" placeholder="Selecciona estado" class="w-full" />
       </div>
       <div>
         <label class="mb-1 block text-sm font-medium">Notas (obligatorio)</label>
         <Textarea v-model="notes" :rows="3" :maxlength="500" class="w-full"
                   placeholder="Motivo del cambio de estado..." />
       </div>
       <div class="flex items-center gap-2">
         <ToggleSwitch v-model="notifyUser" input-id="notify-toggle" />
         <label for="notify-toggle" class="cursor-pointer text-sm">Notificar a la familia</label>
       </div>
     </div>
     <template #footer>
       <Button label="Cancelar" severity="secondary" text @click="handleClose" />
       <Button label="Cambiar estado" :loading="loading"
               :disabled="!selectedStatus || !notes.trim()" @click="handleSubmit" />
     </template>
   </Dialog>
   ```

---

### Step 6: Update `RegistrationDetailPage`

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Multiple additions in both `<script setup>` and `<template>`.

#### 6a — Script setup additions

1. **Import new components and types**:
   ```ts
   import RegistrationStatusTimeline from '@/components/registrations/RegistrationStatusTimeline.vue'
   import AdminStatusChangeDialog from '@/components/registrations/AdminStatusChangeDialog.vue'
   import type { RegistrationStatus } from '@/types/registration'
   ```

2. **Destructure new composable methods**:
   ```ts
   const { ..., changeStatus, confirmChanges, adminUpdateRegistration } = useRegistrations()
   ```

3. **New local state refs**:
   ```ts
   const showStatusChangeDialog = ref(false)
   const changingStatus = ref(false)
   const confirmingChanges = ref(false)

   // Admin edit: notify toggle (used when saving members/extras/info as admin)
   const notifyFamilyOnAdminSave = ref(true)
   const draftTargetStatusOnAdminSave = ref<RegistrationStatus | null>(null)
   ```

4. **Update `canEdit` computed** — currently blocks non-representative; keep that. But also allow admin/board to see their own edit path (admin edit uses a different flow via `adminUpdateRegistration`):
   ```ts
   const canUserEdit = computed(() => {
     if (!registration.value) return false
     const status = registration.value.status
     if (status !== 'Pending' && status !== 'Draft') return false
     if (!isRepresentative.value) return false
     return !installments.value.some((p) => p.proofFileUrl != null)
   })

   const canAdminEdit = computed(
     () => isAdminOrBoard.value && registration.value?.status !== 'Cancelled'
   )

   // Keep `canEdit` as alias for user edit for backwards compat with existing template refs
   const canEdit = canUserEdit
   ```

5. **Update `canCancel` computed** to include new active statuses:
   ```ts
   const canCancel = computed(() => {
     const s = registration.value?.status
     return s === 'Pending' || s === 'PartiallyPaid' || s === 'FullyPaid' || s === 'Confirmed' || s === 'Draft'
   })
   ```

6. **Update `canDelete` computed** — also block `FullyPaid`:
   ```ts
   const canDelete = computed(() => {
     if (!registration.value) return false
     const status = registration.value.status
     if (status !== 'Pending' && status !== 'Draft') return false   // FullyPaid/Confirmed blocked
     return isRepresentative.value || isAdminOrBoard.value
   })
   ```

7. **Add `handleConfirmChanges`**:
   ```ts
   const handleConfirmChanges = async () => {
     confirmingChanges.value = true
     const result = await confirmChanges(registrationId.value)
     confirmingChanges.value = false
     if (result) {
       toast.add({ severity: 'success', summary: 'Cambios confirmados',
                   detail: 'Has confirmado los cambios en tu inscripción.', life: 3000 })
     } else {
       toast.add({ severity: 'error', summary: 'Error',
                   detail: error.value ?? 'Error al confirmar cambios', life: 5000 })
     }
   }
   ```

8. **Add `handleStatusChanged`** (called when `AdminStatusChangeDialog` emits `changed`):
   ```ts
   const handleStatusChanged = async () => {
     showStatusChangeDialog.value = false
     // Registration reactive ref already updated by changeStatus inside composable
     await refreshInstallments()
   }
   ```

9. **Admin save handlers** — The existing `handleSaveMembers`, `handleSaveExtras`, `handleSaveInfo` are user-only. Add parallel **admin** save handlers that call `adminUpdateRegistration` and pass `notifyUser`/`draftTargetStatus`. The admin UI will have separate save buttons that call these admin handlers. Example for members:
   ```ts
   const handleAdminSaveMembers = async () => {
     savingMembers.value = true
     const result = await adminUpdateRegistration(registrationId.value, {
       members: memberSelections.value.map((s) => ({
         memberId: s.memberId,
         attendancePeriod: s.attendancePeriod,
         visitStartDate: s.visitStartDate,
         visitEndDate: s.visitEndDate,
         guardianName: s.guardianName,
         guardianDocumentNumber: s.guardianDocumentNumber
       })),
       notifyUser: notifyFamilyOnAdminSave.value,
       draftTargetStatus: draftTargetStatusOnAdminSave.value
     })
     savingMembers.value = false
     if (result) {
       isEditingMembers.value = false
       await refreshInstallments()
       toast.add({ severity: 'success', summary: 'Éxito', detail: 'Miembros actualizados', life: 3000 })
     } else {
       toast.add({ severity: 'error', summary: 'Error', detail: error.value ?? 'Error al actualizar miembros', life: 5000 })
     }
   }
   ```
   Create equivalent `handleAdminSaveExtras` and `handleAdminSaveInfo` following the same pattern.

#### 6b — Template additions

All new template blocks go inside the existing `<template v-else-if="registration">` block.

1. **Replace the existing draft banner** (the current `<Message v-if="isDraft">`) with the new "Confirmar cambios" banner that includes the action button. Only show action button to the representative (or admin/board). Show the informational hint separately for representative:

   ```html
   <!-- "Confirmar cambios" banner (spec §7.3) -->
   <div
     v-if="registration.status === 'Draft' && registration.hasPendingUserAcknowledgement"
     class="mb-6 rounded-lg border border-orange-200 bg-orange-50 p-4"
     data-testid="confirm-changes-banner"
   >
     <p class="text-sm font-medium text-orange-800">
       La Junta ha realizado cambios en tu inscripción. Revisa los detalles y confirma que todo es correcto.
     </p>
     <Button
       v-if="isRepresentative || isAdminOrBoard"
       label="Confirmar cambios"
       icon="pi pi-check"
       severity="warning"
       class="mt-3"
       :loading="confirmingChanges"
       @click="handleConfirmChanges"
       data-testid="confirm-changes-btn"
     />
   </div>

   <!-- Generic draft info for representative (no pending ack) -->
   <Message
     v-else-if="registration.status === 'Draft' && isRepresentative"
     severity="info"
     :closable="false"
     class="mb-6"
     data-testid="draft-edit-hint"
   >
     Puedes revisar y editar los miembros o extras antes de confirmar.
   </Message>
   ```

2. **Admin status-change section** (show only for admin/board, after the header area, before notes):
   ```html
   <!-- Admin: manual status change (spec §7.4) -->
   <div v-if="isAdminOrBoard && registration.status !== 'Cancelled'" class="mb-6 flex items-center gap-2">
     <Button
       label="Cambiar estado"
       icon="pi pi-exchange"
       severity="secondary"
       outlined
       size="small"
       @click="showStatusChangeDialog = true"
     />
   </div>

   <AdminStatusChangeDialog
     v-model:visible="showStatusChangeDialog"
     :registration-id="registrationId"
     :current-status="registration.status"
     :loading="changingStatus"
     @changed="handleStatusChanged"
   />
   ```

3. **Admin edit sections** — For each edit section (members, extras, info), add a parallel admin path. When `canAdminEdit` is true and the user is admin/board, show admin edit button alongside (or instead of) the user edit button. The admin edit buttons call `startEditingMembers` / `startEditingExtras` / `startEditingInfo` (same data loading), but the save buttons inside each edit form should call admin handlers.

   In the member edit save footer, add:
   ```html
   <!-- Admin save row (instead of user save) -->
   <template v-if="isAdminOrBoard">
     <div class="mt-3 rounded-md border border-orange-100 bg-orange-50 p-3">
       <div class="mb-2 flex items-center gap-2">
         <ToggleSwitch v-model="notifyFamilyOnAdminSave" input-id="notify-admin-members" />
         <label for="notify-admin-members" class="cursor-pointer text-sm text-orange-800">
           Notificar a la familia
         </label>
       </div>
       <p class="mb-3 text-xs text-orange-700">
         Al guardar, la inscripción pasará a estado "En revisión" hasta que la familia confirme los cambios.
       </p>
       <div class="flex gap-2">
         <Button label="Guardar (admin)" icon="pi pi-check" :loading="savingMembers"
                 severity="warning" @click="handleAdminSaveMembers" />
         <Button label="Cancelar" severity="secondary" text :disabled="savingMembers"
                 @click="isEditingMembers = false" />
       </div>
     </div>
   </template>
   <template v-else>
     <!-- existing user save buttons -->
     <Button label="Guardar" icon="pi pi-check" :loading="savingMembers" @click="handleSaveMembers" />
     <Button label="Cancelar" severity="secondary" text :disabled="savingMembers" @click="isEditingMembers = false" />
   </template>
   ```

   Apply the same pattern for extras and info edit sections.

4. **Status timeline** — add at the bottom of the registration content, before the cancel/delete actions area:
   ```html
   <!-- Status history timeline (spec §7.2) -->
   <RegistrationStatusTimeline
     v-if="registration.statusHistory?.length"
     :history="registration.statusHistory"
     class="mt-8"
   />
   ```

---

### Step 7: Update `RegistrationsAdminPanel`

- **File**: `frontend/src/components/admin/RegistrationsAdminPanel.vue`
- **Action**: Add `PartiallyPaid` and `FullyPaid` to the `statusOptions` array.

**Implementation Steps**:

Find the `statusOptions` array (around line 59) and add the two new entries:

```ts
const statusOptions = [
  { label: 'Todos',          value: null },
  { label: 'Pendiente',      value: 'Pending' },
  { label: 'Al corriente',   value: 'PartiallyPaid' },
  { label: 'Pago completo',  value: 'FullyPaid' },
  { label: 'Confirmada',     value: 'Confirmed' },
  { label: 'Cancelada',      value: 'Cancelled' },
  { label: 'En revisión',    value: 'Draft' },
]
```

Note: `Draft` label updated from 'Borrador' → 'En revisión' to match the status badge.

---

### Step 8: Update `RegistrationsPage`

- **File**: `frontend/src/views/registrations/RegistrationsPage.vue`
- **Action**: Update the sort computed so `PartiallyPaid` and `FullyPaid` are treated as "active" (shown first, same as `Pending`/`Confirmed`).

**Implementation Steps**:

Find the `computed` sort logic and update the active status check:

```ts
const activeStatuses: RegistrationStatus[] = ['Pending', 'PartiallyPaid', 'FullyPaid', 'Confirmed', 'Draft']
const sortedRegistrations = computed(() =>
  [...registrations.value].sort((a, b) => {
    const aActive = activeStatuses.includes(a.status)
    const bActive = activeStatuses.includes(b.status)
    if (aActive && !bActive) return -1
    if (!aActive && bActive) return 1
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })
)
```

Also: if the page shows a "Confirmar cambios" indicator on the card, check `RegistrationCard.vue` — add a visual indicator (orange dot or badge) when `registration.hasPendingUserAcknowledgement === true`. This is optional but strongly recommended for UX (user sees at a glance which registration needs action). Implement by adding a conditional `<span>` badge in `RegistrationCard.vue`:

```html
<span
  v-if="registration.hasPendingUserAcknowledgement"
  class="inline-flex items-center rounded-full bg-orange-100 px-2 py-0.5 text-xs font-medium text-orange-800"
>
  Cambios pendientes
</span>
```

---

### Step 9: Write Vitest Unit Tests

- **Files**:
  - `frontend/src/composables/__tests__/useRegistrations.test.ts` *(extend existing)*
  - `frontend/src/components/registrations/__tests__/RegistrationStatusBadge.test.ts` *(extend)*
  - `frontend/src/components/registrations/__tests__/RegistrationStatusTimeline.test.ts` *(new)*
  - `frontend/src/components/registrations/__tests__/AdminStatusChangeDialog.test.ts` *(new)*

**`useRegistrations` test additions**:
- `changeStatus` — happy path returns `RegistrationResponse`, sets `registration.value`
- `changeStatus` — API error sets `error.value`, returns null
- `confirmChanges` — happy path returns updated registration
- `adminUpdateRegistration` — verifies correct URL (`/registrations/{id}/admin`) and body

**`RegistrationStatusBadge` test additions**:
- Assert label `'Al corriente'` for `PartiallyPaid`
- Assert label `'Pago completo'` for `FullyPaid`
- Assert label `'En revisión'` for `Draft` (regression check — was `'Borrador'`)

**`RegistrationStatusTimeline` tests**:
- Renders empty when `history` is empty
- Renders one entry per history item
- Shows `'Sistema'` when trigger is `Automatic`
- Shows `changedByUserName` when trigger is `AdminAction`
- Shows `notes` when present, hides when null

**`AdminStatusChangeDialog` tests**:
- Submit button disabled when no status selected
- Submit button disabled when notes is empty
- Emits `changed` on successful API call
- Shows only valid target statuses for current status (e.g. `Pending` → only `PartiallyPaid`)
- Toast shown on API error

---

### Step 10: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/registration-status-flow.cy.ts` *(new)*

**Critical flows to cover**:
1. User sees "Confirmar cambios" banner when registration is in `Draft` with `hasPendingUserAcknowledgement: true`
2. Clicking "Confirmar cambios" calls `POST /confirm-changes` and hides the banner on success
3. Status timeline renders entries from `statusHistory`
4. Admin sees "Cambiar estado" button; dialog opens with correct status options

Use `cy.intercept` to stub API responses rather than hitting a real backend.

---

### Step 11: Update Technical Documentation

- **Action**: Update specs after implementation.
- **Implementation Steps**:
  1. Update `ai-specs/specs/api-spec.yml` — add new endpoints `PATCH /registrations/{id}/status` and `POST /registrations/{id}/confirm-changes`; document updated `PUT /registrations/{id}/admin` request body.
  2. Update `ai-specs/specs/frontend-standards.mdc` if any new patterns are introduced (e.g. admin vs user edit handler pattern).
  3. No routing doc changes (no new routes).

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Update TypeScript types *(unblocks all other steps)*
3. Step 2 — Update `useRegistrations` composable
4. Step 3 — Update `RegistrationStatusBadge`
5. Step 4 — Create `RegistrationStatusTimeline`
6. Step 5 — Create `AdminStatusChangeDialog`
7. Step 6 — Update `RegistrationDetailPage` *(depends on Steps 4, 5)*
8. Step 7 — Update `RegistrationsAdminPanel`
9. Step 8 — Update `RegistrationsPage` and `RegistrationCard`
10. Step 9 — Write Vitest unit tests
11. Step 10 — Write Cypress E2E tests
12. Step 11 — Update technical documentation

---

## Testing Checklist

- [ ] `RegistrationStatus` type union includes `PartiallyPaid` and `FullyPaid` — no TypeScript errors
- [ ] `RegistrationStatusBadge` renders correct label + colour for all 6 statuses
- [ ] `RegistrationStatusTimeline` renders correctly with zero, one, and many entries
- [ ] `AdminStatusChangeDialog` shows only valid target statuses per current status
- [ ] `AdminStatusChangeDialog` requires notes before enabling submit
- [ ] "Confirmar cambios" banner visible when `status === 'Draft' && hasPendingUserAcknowledgement`
- [ ] "Confirmar cambios" banner hidden after successful `confirmChanges` API call
- [ ] Admin "Cambiar estado" button hidden for Cancelled registrations
- [ ] "Notificar a la familia" toggle present in admin edit save area
- [ ] `RegistrationsAdminPanel` filter shows `PartiallyPaid` and `FullyPaid` options
- [ ] `RegistrationsPage` active-first sort includes `PartiallyPaid` and `FullyPaid`
- [ ] Vitest coverage for all new composable methods
- [ ] Cypress E2E covers confirm-changes user flow
- [ ] No `any` types introduced
- [ ] No `<style>` blocks added

---

## Error Handling Patterns

- All new composable methods follow the existing pattern: `loading.value = true`, `error.value = null`, try/catch, set `error.value` from `response.data.error.message` on failure, `finally` resets loading.
- UI error feedback via PrimeVue `useToast()` (existing pattern in `RegistrationDetailPage`).
- `confirmChanges` and `changeStatus` failures must not crash the page — display toast and keep the current registration state intact.

---

## UI/UX Considerations

- PrimeVue `Timeline` for the status history — check if it is already registered globally in `main.ts`. If not, import locally.
- PrimeVue `ToggleSwitch` for the "Notificar a la familia" toggle — same check.
- PrimeVue `Select` (not `Dropdown` — the project uses the PrimeVue v4 rename) for the status selector in `AdminStatusChangeDialog`.
- "Confirmar cambios" banner uses orange palette (`bg-orange-50 border-orange-200 text-orange-800`) to visually match the `Draft` status badge colour.
- All user-visible text is in **Spanish**. All TypeScript identifiers, comments, and documentation are in **English**.
- Timeline entries are sorted ascending by `changedAt` (oldest first) so the user reads the history chronologically top-to-bottom.
- The admin edit area warning box uses an orange tint to signal that saving will put the registration into Draft mode.

---

## Dependencies

No new npm packages required. All needed PrimeVue components (`Timeline`, `ToggleSwitch`, `Select`, `Dialog`, `Textarea`, `Button`) are already part of the PrimeVue installation. Verify global registration in `main.ts`; add any missing ones.

---

## Notes

- **Backend dependency**: This frontend PR depends on the backend branch `feature/feat-registration-status-flow-backend` being deployed (or available locally). Do not merge before the backend PR is ready.
- **Draft label change**: `Draft` now renders as 'En revisión' (was 'Borrador'). Search for `'Borrador'` in tests and update.
- **TypeScript exhaustiveness**: After adding new values to `RegistrationStatus`, TypeScript will flag any `switch`/`Record` that doesn't cover all cases. Fix all compile errors before considering the step done.
- **`adminUpdateRegistration` scope**: This endpoint (`PUT /registrations/{id}/admin`) sends the *entire* updated registration in one call. In practice, the admin edit UI in `RegistrationDetailPage` still edits sections independently (members, extras, info). Each admin-save handler only sends the fields it touches; the backend `AdminUpdateAsync` must handle partial updates. Confirm with the backend spec that partial body is supported, or adjust the request type accordingly.
- **`canEdit` / `canAdminEdit` distinction**: The existing `canEdit` guards the *user* edit path (rep only, Pending/Draft, no proof uploaded). The new `canAdminEdit` guards the *admin* edit path (admin/board, any non-Cancelled status). These are independent — a user who is the rep of their own registration and also has board role will see both paths. For simplicity, show the admin path only when `isAdminOrBoard`, and show the user path only when `canUserEdit`.

---

## Next Steps After Implementation

- Coordinate deployment of backend and frontend branches together.
- After merge, board should test the `Pending → PartiallyPaid` transition manually to verify the "Al corriente" email is sent.
- The existing "Confirmar borrador" logic (if any) on the frontend should be checked for regressions after the `Draft` label change.

---

## Implementation Verification

- **Code Quality**: TypeScript strict — no `any`, all components use `<script setup lang="ts">`
- **Functionality**: `RegistrationStatusBadge` renders all 6 statuses; timeline renders history; admin dialog allows valid transitions only
- **Testing**: Vitest covers composable methods + component states; Cypress covers confirm-changes flow
- **Integration**: `changeStatus` calls `PATCH /registrations/{id}/status`, `confirmChanges` calls `POST /registrations/{id}/confirm-changes`, `adminUpdateRegistration` calls `PUT /registrations/{id}/admin`
- **Documentation**: `api-spec.yml` updated with new endpoints before PR is merged

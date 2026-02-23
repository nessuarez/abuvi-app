# Frontend Implementation Plan: feat-open-edition-amendments — Amendments to Open Camp Editions

## Overview

Expose the new Admin-only `Open → Draft` rollback and `force` re-open capabilities in the Camp Editions management UI. Changes are confined to the existing status-change flow: the `CampEditionStatusDialog`, the `useCampEditions` composable, and the `CampEditionsPage` view.

**Stack**: Vue 3 Composition API, `<script setup lang="ts">`, Pinia (`useAuthStore`), PrimeVue components, Tailwind CSS.

---

## Architecture Context

### Files to Create / Modify

| File | Change |
| --- | --- |
| `frontend/src/types/camp-edition.ts` | Add `force?: boolean` to `ChangeEditionStatusRequest` |
| `frontend/src/composables/useCampEditions.ts` | Add `force?: boolean` to `changeStatus()` |
| `frontend/src/components/camps/CampEditionStatusDialog.vue` | Main UI change — Admin rollback + force re-open |
| `frontend/src/views/camps/CampEditionsPage.vue` | Pass `force` from dialog confirmation |
| `frontend/src/composables/__tests__/useCampEditions.test.ts` | New tests for `force` parameter |

### No new routes or Pinia stores needed.

### Role access pattern

`auth.isAdmin` is already provided by `useAuthStore()` (Pinia) and is used throughout the app. The dialog will import it directly (same pattern as other components).

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-open-edition-amendments-frontend`
- **Implementation Steps**:
  1. Ensure you are on the latest `main`: `git checkout main && git pull origin main`
  2. Create new branch: `git checkout -b feature/feat-open-edition-amendments-frontend`
  3. Verify: `git branch`
- **Note**: This must be the FIRST step before any code changes.

---

### Step 1: Update TypeScript Type

- **File**: `frontend/src/types/camp-edition.ts`
- **Action**: Add `force?: boolean` to `ChangeEditionStatusRequest`

**Find**:
```typescript
export interface ChangeEditionStatusRequest {
  status: CampEditionStatus
}
```

**Replace with**:
```typescript
export interface ChangeEditionStatusRequest {
  status: CampEditionStatus
  force?: boolean // Admin-only: bypasses startDate < today when re-opening Draft → Open
}
```

---

### Step 2: Update Composable

- **File**: `frontend/src/composables/useCampEditions.ts`
- **Action**: Add optional `force` parameter to `changeStatus()` and include it in the request body

**Find** the `changeStatus` function signature and request body:
```typescript
const changeStatus = async (
  id: string,
  newStatus: CampEditionStatus
): Promise<CampEdition | null> => {
  ...
    const response = await api.patch<ApiResponse<CampEdition>>(
      `/camps/editions/${id}/status`,
      { status: newStatus } satisfies ChangeEditionStatusRequest
    )
```

**Replace with**:
```typescript
const changeStatus = async (
  id: string,
  newStatus: CampEditionStatus,
  force?: boolean
): Promise<CampEdition | null> => {
  ...
    const body: ChangeEditionStatusRequest = { status: newStatus }
    if (force) body.force = true
    const response = await api.patch<ApiResponse<CampEdition>>(
      `/camps/editions/${id}/status`,
      body
    )
```

- **Implementation Notes**:
  - Only include `force` in the body when `true` — omitting it is equivalent to `false` on the server side
  - All other logic in the function (loading state, error handling, local state update) remains unchanged

---

### Step 3: Update Status Dialog — Main UI Change

- **File**: `frontend/src/components/camps/CampEditionStatusDialog.vue`
- **Action**: Support two new scenarios:
  1. **Admin rollback** (`Open → Draft`): show an extra "Volver a Borrador" button alongside the normal "Cerrar inscripciones" forward button
  2. **Force re-open** (`Draft → Open` with `startDate` in the past, Admin only): show a warning in the dialog and set `force: true` on confirm

#### 3a. Script changes

Add `useAuthStore` import and computed logic for the new scenarios:

```typescript
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

// Existing: determines the single next status (forward path)
const validNextStatus: Partial<Record<CampEditionStatus, CampEditionStatus>> = {
  Proposed: 'Draft',
  Draft: 'Open',
  Open: 'Closed',
  Closed: 'Completed',
}
const nextStatus = computed(() => validNextStatus[props.edition.status] ?? null)

// NEW: Admin can also roll back Open → Draft
const canRollbackToDraft = computed(
  () => props.edition.status === 'Open' && auth.isAdmin
)

// NEW: Admin transitioning Draft → Open with startDate in the past needs force=true
const needsForce = computed(() => {
  if (props.edition.status !== 'Draft') return false
  if (!auth.isAdmin) return false
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return new Date(props.edition.startDate) < today
})

// Updated confirm handler — accepts the chosen target status
const handleConfirm = (targetStatus: CampEditionStatus) => {
  const force = targetStatus === 'Open' && needsForce.value ? true : undefined
  emit('confirm', targetStatus, force)
}

const handleRollback = () => {
  handleConfirm('Draft')
}
```

Update the `emit` definition to include `force`:
```typescript
// Before
const emit = defineEmits<{ confirm: [status: CampEditionStatus] }>()

// After
const emit = defineEmits<{ confirm: [status: CampEditionStatus, force?: boolean] }>()
```

#### 3b. Updated `transitionWarning` computed

```typescript
const transitionWarning = computed(() => {
  if (props.edition.status === 'Draft' && needsForce.value) {
    return 'La fecha de inicio de esta edición ya ha pasado. Como administrador, puedes forzar la apertura igualmente.'
  }
  if (props.edition.status === 'Draft') {
    return 'La edición se abrirá para inscripciones. Asegúrate de que la fecha de inicio no ha pasado.'
  }
  if (props.edition.status === 'Closed') {
    return 'La edición se marcará como completada. Solo es posible si la fecha de fin ya ha pasado.'
  }
  return null
})

// NEW: Warning shown when Admin chooses the rollback action
const rollbackWarning = 'Esta edición dejará de estar disponible para nuevas inscripciones mientras esté en borrador. Las inscripciones existentes no se verán afectadas.'
```

#### 3c. Template changes

The dialog currently has a single "Confirmar" button. Update it to:

1. **Keep** the existing forward "Confirmar" button (for `Proposed → Draft`, `Draft → Open`, `Open → Closed`, `Closed → Completed`)
2. **Add** a separate "Volver a Borrador" rollback button shown only when `canRollbackToDraft` is true, styled with `severity="warn"` or `outlined` to visually distinguish it from the primary forward action

```vue
<!-- Status arrow row — unchanged -->
<div class="flex items-center justify-center gap-3">
  <CampEditionStatusBadge :status="edition.status" size="md" />
  <i class="pi pi-arrow-right text-gray-400" />
  <CampEditionStatusBadge v-if="nextStatus" :status="nextStatus" size="md" />
</div>

<!-- Normal transition warning -->
<Message v-if="transitionWarning" :severity="needsForce ? 'warn' : 'info'" :closable="false" class="text-sm">
  {{ transitionWarning }}
</Message>

<!-- Admin-only rollback section — shown below the normal flow -->
<Divider v-if="canRollbackToDraft" />
<div v-if="canRollbackToDraft" class="space-y-2">
  <p class="text-sm text-gray-600 font-medium">Acción de administrador</p>
  <Message severity="warn" :closable="false" class="text-sm">
    {{ rollbackWarning }}
  </Message>
</div>

<!-- Footer buttons -->
<template #footer>
  <div class="flex justify-between gap-2">
    <!-- Rollback button — Admin-only, left-aligned -->
    <Button
      v-if="canRollbackToDraft"
      label="Volver a Borrador"
      severity="warn"
      outlined
      :loading="loading"
      data-testid="rollback-to-draft-btn"
      @click="handleRollback"
    />
    <div class="flex gap-2 ml-auto">
      <Button label="Cancelar" text @click="emit('cancel')" />
      <Button
        label="Confirmar"
        :loading="loading"
        :disabled="!nextStatus || loading"
        data-testid="confirm-status-btn"
        @click="handleConfirm(nextStatus!)"
      />
    </div>
  </div>
</template>
```

- **Implementation Notes**:
  - `Divider` is a PrimeVue component — already available in the project
  - "Volver a Borrador" is `severity="warn"` + `outlined` to visually indicate it is a reversing/cautious action, not a destructive one
  - The force-open path is invisible to the user: when `needsForce` is true, the dialog shows the extra warning but the "Confirmar" button still behaves normally — the `force: true` flag is added transparently by `handleConfirm`
  - Board users see no rollback button (the `canRollbackToDraft` check includes `auth.isAdmin`)

---

### Step 4: Update the Page Handler

- **File**: `frontend/src/views/camps/CampEditionsPage.vue`
- **Action**: Update `handleStatusConfirm` to accept and forward the optional `force` parameter from the dialog's updated `confirm` event

**Find**:
```typescript
const handleStatusConfirm = async (newStatus: CampEditionStatus) => {
  if (!selectedEdition.value) return
  statusLoading.value = true
  const result = selectedEdition.value.status === 'Proposed'
    ? await promoteEdition(selectedEdition.value.id)
    : await changeStatus(selectedEdition.value.id, newStatus)
```

**Replace with**:
```typescript
const handleStatusConfirm = async (newStatus: CampEditionStatus, force?: boolean) => {
  if (!selectedEdition.value) return
  statusLoading.value = true
  const result = selectedEdition.value.status === 'Proposed'
    ? await promoteEdition(selectedEdition.value.id)
    : await changeStatus(selectedEdition.value.id, newStatus, force)
```

Update the template's event binding on the dialog to match:
```vue
<!-- Before -->
<CampEditionStatusDialog
  ...
  @confirm="handleStatusConfirm"
/>

<!-- After — no template change needed; the event signature update is enough -->
<!-- Vue 3 emits with additional args are passed through automatically -->
```

---

### Step 5: Write Vitest Unit Tests

- **File**: `frontend/src/composables/__tests__/useCampEditions.test.ts`
- **Action**: Add tests covering the `force` parameter in `changeStatus`

Add the following test cases to the existing test file (or create if it doesn't exist):

```typescript
describe('changeStatus', () => {
  it('sends force=true in request body when force param is true', async () => {
    const mockPatch = vi.fn().mockResolvedValue({
      data: { success: true, data: { id: 'ed-1', status: 'Open' } }
    })
    api.patch = mockPatch

    await changeStatus('ed-1', 'Open', true)

    expect(mockPatch).toHaveBeenCalledWith(
      '/camps/editions/ed-1/status',
      { status: 'Open', force: true }
    )
  })

  it('omits force from request body when force param is false/undefined', async () => {
    const mockPatch = vi.fn().mockResolvedValue({
      data: { success: true, data: { id: 'ed-1', status: 'Open' } }
    })
    api.patch = mockPatch

    await changeStatus('ed-1', 'Open')

    expect(mockPatch).toHaveBeenCalledWith(
      '/camps/editions/ed-1/status',
      { status: 'Open' }
    )
  })

  it('sends Draft status without force for normal Open→Draft rollback', async () => {
    const mockPatch = vi.fn().mockResolvedValue({
      data: { success: true, data: { id: 'ed-1', status: 'Draft' } }
    })
    api.patch = mockPatch

    await changeStatus('ed-1', 'Draft')

    expect(mockPatch).toHaveBeenCalledWith(
      '/camps/editions/ed-1/status',
      { status: 'Draft' }
    )
  })
})
```

---

### Step 6: Update Technical Documentation

- **File**: `ai-specs/specs/api-spec.yml` — already updated in the backend plan (confirms `force` field in `ChangeEditionStatusRequest`)
- **File**: `ai-specs/specs/frontend-standards.mdc` — no structural changes; existing patterns are followed

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-open-edition-amendments-frontend`
2. **Step 1** — Add `force?: boolean` to `ChangeEditionStatusRequest` type
3. **Step 2** — Update `changeStatus()` composable to accept and forward `force`
4. **Step 3** — Update `CampEditionStatusDialog.vue` (main UI change)
5. **Step 4** — Update `handleStatusConfirm` in `CampEditionsPage.vue`
6. **Step 5** — Write / update Vitest tests for `changeStatus` with `force`
7. **Step 6** — Verify documentation is up to date

---

## Testing Checklist

### Vitest unit tests — `useCampEditions.test.ts`
- [ ] `changeStatus` sends `{ status, force: true }` when `force=true`
- [ ] `changeStatus` sends `{ status }` (no `force` key) when `force` is omitted
- [ ] `changeStatus` sends `{ status }` for normal forward transitions

### Manual verification (critical user flows)

#### Board user (no Admin role)
- [ ] Opens status dialog on an `Open` edition — sees only "Cerrar inscripciones" (forward button) — no "Volver a Borrador" button visible
- [ ] Dialog on a `Draft` edition with a past `startDate` — no special warning, forward "Confirmar" sends `{ status: 'Open' }` (no `force`)
- [ ] Backend returns 400 if Board tries to open with past date (expected — user should not be creating this scenario)

#### Admin user
- [ ] Opens status dialog on an `Open` edition — sees both "Confirmar" (→ Closed) AND "Volver a Borrador" (→ Draft) buttons
- [ ] "Volver a Borrador" shows rollback warning message
- [ ] Clicking "Volver a Borrador" calls `changeStatus(id, 'Draft')` — no `force` sent
- [ ] Dialog on a `Draft` edition with a **future** `startDate` — no extra warning; forward confirms normally
- [ ] Dialog on a `Draft` edition with a **past** `startDate` — shows "date in past, force-opening" warning; "Confirmar" sends `{ status: 'Open', force: true }`
- [ ] After rollback to Draft: `GET /api/camps/editions/active` returns null (no active edition in UI)
- [ ] Toast notification appears after each successful transition

---

## Error Handling Patterns

The composable's existing `catch` block surfaces the API error message:

```typescript
error.value = (err as { response?: { data?: { error?: { message?: string } } } })
  ?.response?.data?.error?.message || 'Error al cambiar estado'
```

- **403 from server** (Board tries Admin-only action): the button is hidden in the UI so this should never be reached in normal use. If it somehow occurs, the composable surfaces the server error message via the existing error handling, and the page shows a toast with the error.
- **400 from server** (`startDate` in past without `force`): for Board users this is expected. For Admin users, the dialog pre-detects the past date and adds `force: true` automatically, so a 400 should not occur for Admin in practice.

---

## UI/UX Considerations

- **"Volver a Borrador"** button uses `severity="warn"` + `outlined` to signal a cautious, reversible admin action — not red (danger) since it doesn't delete data, and not primary (blue) since it is not the main forward path
- The rollback section is separated from the forward flow by a `<Divider>` and a "Acción de administrador" label so Board users who somehow see the dialog have clear context (though they won't see it due to the `v-if="canRollbackToDraft"` check)
- The force-open warning uses `severity="warn"` (yellow) to indicate the Admin is doing something unusual (opening a past-dated edition)
- Status badge arrow visualization (→) remains for the forward path; the rollback has its own button without an arrow visual, keeping the dialog clean

---

## Dependencies

No new npm packages or PrimeVue components needed. The `Divider` component is already part of PrimeVue and used elsewhere in the project.

---

## Notes

1. **Backend must be merged first** — this frontend relies on the backend changes from `feat-open-edition-amendments_backend.md`. The `force` field is ignored by older API versions but adding it to the type and composable now is safe (it defaults to `undefined` = omitted from request body).
2. **Role enforcement is server-side** — the frontend `v-if="auth.isAdmin"` is a UX convenience only. The server enforces the role restriction independently.
3. **No `Proposed → Draft` path affected** — the `CampEditionsPage` already uses `promoteEdition()` for `Proposed` editions; no change to that path.
4. **TypeScript strict** — `nextStatus!` (non-null assertion) in `handleConfirm(nextStatus!)` is safe because the "Confirmar" button is `disabled` when `!nextStatus`.
5. **`auth.isAdmin` is already false for `Board` role** — the `isAdmin` computed in the store only returns `true` for `role === 'Admin'`, so the `canRollbackToDraft` guard is correct as-is.

---

## Next Steps After Implementation

- Backend ticket `feat-open-edition-amendments-backend` must be deployed before this frontend can be tested end-to-end
- Verify with an Admin account and a Board account against the live API

---

## Implementation Verification

- [ ] **TypeScript**: No `any`, all types explicit, `ChangeEditionStatusRequest.force` is `boolean | undefined`
- [ ] **Functionality**: "Volver a Borrador" button appears only for Admin on Open editions
- [ ] **Functionality**: Force-open warning appears for Admin on Draft editions with past start date
- [ ] **Functionality**: Board users see no rollback or force UI — dialog is unchanged for them
- [ ] **Functionality**: Toast success shown after rollback and re-open
- [ ] **Testing**: All 3 new Vitest tests for `changeStatus` with `force` pass
- [ ] **Integration**: `changeStatus('Draft')` sends `{ status: 'Draft' }`, `changeStatus('Open', true)` sends `{ status: 'Open', force: true }`
- [ ] **Documentation**: No doc changes required beyond what the backend plan already covers

# Frontend Implementation Plan: fix-enrollment-status-email-notification

## Overview

Implement the frontend changes for the registration status email notification fix. Three UI changes are needed:

1. **Type update**: Add `familyNotifiedOfDraft: boolean` to `RegistrationResponse`.
2. **Composable**: Add `notifyDraft(id)` method to `useRegistrations` that calls `POST /api/registrations/{id}/notify-draft`.
3. **`AdminStatusChangeDialog.vue`**: Disable the "Notificar a la familia" toggle (with helper text) when the selected target status has no email template.
4. **`RegistrationDetailPage.vue`**: Add an orange warning banner (admin/board only) when the registration is in Draft and the family has not been notified.

Architecture: Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue components (already imported), Tailwind CSS utility classes. No new Pinia stores — all changes use local state and the existing `useRegistrations` composable.

---

## Architecture Context

**Files to modify:**

| File | Type of change |
| --- | --- |
| `frontend/src/types/registration.ts` | Add `familyNotifiedOfDraft: boolean` to `RegistrationResponse` |
| `frontend/src/composables/useRegistrations.ts` | Add `notifyDraft` method; surface email warning toast from `changeStatus` / `adminUpdateRegistration` |
| `frontend/src/components/registrations/AdminStatusChangeDialog.vue` | Conditionally disable notify toggle |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Add unnotified-changes warning banner |

**Files to add (tests):**

| File | Purpose |
| --- | --- |
| `frontend/src/composables/__tests__/useRegistrations.notifyDraft.test.ts` | Unit tests for `notifyDraft` |

**No new routes, no new Pinia store, no new PrimeVue packages** — all components already available.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/fix-enrollment-status-email-notification-frontend`
- **Base branch**: `dev`
- **Implementation steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/fix-enrollment-status-email-notification-frontend`
  3. `git branch` — verify active branch
- **Notes**: Must be the FIRST step before any code changes.

---

### Step 1: Update `RegistrationResponse` TypeScript Interface

- **File**: `frontend/src/types/registration.ts`
- **Action**: Add `familyNotifiedOfDraft` to the `RegistrationResponse` interface.

**Current interface (around line 125):**

```ts
export interface RegistrationResponse {
  // ... existing fields ...
  hasPendingUserAcknowledgement: boolean
  statusHistory: RegistrationStatusHistoryEntry[]
  // Admin/Board-only fields (absent for Member role)
  accommodationInternalNotes?: string | null
  accommodationNeeds?: AccommodationNeedResponse[]
  friendLinks?: FriendLinkResponse[]
}
```

**After change** — add immediately after `hasPendingUserAcknowledgement`:

```ts
hasPendingUserAcknowledgement: boolean
familyNotifiedOfDraft: boolean
```

- **Implementation note**: The field is always present in the backend response (not nullable, defaults to `false`), so it is typed as `boolean`, not `boolean | undefined`.
- **Also update `mockRegistration`** in any existing test files that construct a full `RegistrationResponse` literal — add `familyNotifiedOfDraft: false` to avoid TypeScript errors.

---

### Step 2: Add `notifyDraft` to `useRegistrations` Composable

- **File**: `frontend/src/composables/useRegistrations.ts`
- **Action**: Add a new `notifyDraft` method after `confirmChanges`.

```ts
const notifyDraft = async (id: string): Promise<boolean> => {
  loading.value = true
  error.value = null
  try {
    await api.post(`/registrations/${id}/notify-draft`)
    if (registration.value?.id === id) {
      registration.value = { ...registration.value, familyNotifiedOfDraft: true }
    }
    return true
  } catch (err: unknown) {
    error.value =
      (err as { response?: { data?: { error?: { message?: string } } } })
        ?.response?.data?.error?.message ?? 'Error al notificar a la familia'
    return false
  } finally {
    loading.value = false
  }
}
```

- **Return value**: `true` on success, `false` on failure (same pattern as `cancelRegistration`).
- **Optimistic local update**: When the POST succeeds, patch `registration.value.familyNotifiedOfDraft = true` so the warning banner disappears immediately without a full refetch.
- **Export**: Add `notifyDraft` to the returned object at the bottom of `useRegistrations`.

---

### Step 3: Update `AdminStatusChangeDialog.vue` — Disable Toggle for Unsupported Statuses

- **File**: `frontend/src/components/registrations/AdminStatusChangeDialog.vue`
- **Action**: Disable the "Notificar a la familia" toggle and show helper text when the target status has no email template.

#### 3a — Add the constant in `<script setup>`

After the `VALID_TRANSITIONS` constant (around line 41):

```ts
const STATUSES_WITH_EMAIL: RegistrationStatus[] = ['PartiallyPaid', 'Confirmed', 'Pending']
```

#### 3b — Add computed `notifyEnabled`

```ts
const notifyEnabled = computed(
  () => selectedStatus.value !== null && STATUSES_WITH_EMAIL.includes(selectedStatus.value)
)
```

When `notifyEnabled` becomes `false` (user selects a status without an email template), also reset `notifyUser` to `false` so the toggle is unchecked when disabled. Use a `watch`:

```ts
watch(notifyEnabled, (enabled) => {
  if (!enabled) notifyUser.value = false
  else notifyUser.value = true
})
```

#### 3c — Update the toggle markup

Current markup (around line 119):

```html
<div class="flex items-center gap-2">
  <ToggleSwitch v-model="notifyUser" input-id="notify-status-toggle" />
  <label for="notify-status-toggle" class="cursor-pointer text-sm text-gray-700">
    Notificar a la familia
  </label>
</div>
```

Replace with:

```html
<div class="space-y-1">
  <div class="flex items-center gap-2">
    <ToggleSwitch
      v-model="notifyUser"
      input-id="notify-status-toggle"
      :disabled="!notifyEnabled"
    />
    <label
      for="notify-status-toggle"
      :class="['text-sm', notifyEnabled ? 'cursor-pointer text-gray-700' : 'cursor-not-allowed text-gray-400']"
    >
      Notificar a la familia
    </label>
  </div>
  <p v-if="selectedStatus && !notifyEnabled" class="text-xs text-gray-400">
    Este cambio de estado no envía notificación por correo.
  </p>
</div>
```

- **Dependencies**: `watch` already imported from `vue`; `computed` already imported. Add `watch` to the import if not present.
- **Imports to verify**: `import { ref, computed, watch } from 'vue'`

---

### Step 4: Add Unnotified-Changes Warning Banner to `RegistrationDetailPage.vue`

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Add a warning banner and a "Notificar a la familia" action button for admin/board users when the registration is in Draft but the family has not been notified.

#### 4a — Import `notifyDraft` from composable

In the destructuring at the top of the script (around line 79):

```ts
const {
  // ...existing...
  adminUpdateRegistration,
  adminUpdateMembers,
  notifyDraft,   // add this
} = useRegistrations()
```

#### 4b — Add local state

After the `confirmingChanges` ref (around line 92):

```ts
const notifyingDraft = ref(false)
```

#### 4c — Add computed `hasUnnotifiedDraftChanges`

After the existing computed properties block:

```ts
const hasUnnotifiedDraftChanges = computed(
  () =>
    registration.value?.status === 'Draft' &&
    registration.value.hasPendingUserAcknowledgement &&
    !registration.value.familyNotifiedOfDraft
)
```

#### 4d — Add handler `handleNotifyDraft`

```ts
const handleNotifyDraft = async () => {
  if (!registration.value) return
  notifyingDraft.value = true
  const success = await notifyDraft(registration.value.id)
  notifyingDraft.value = false
  if (success) {
    toast.add({
      severity: 'success',
      summary: 'Notificación enviada',
      detail: 'La familia ha sido notificada por correo.',
      life: 4000,
    })
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value ?? 'No se pudo enviar la notificación.',
      life: 5000,
    })
  }
}
```

#### 4e — Add warning banner to template

Place the banner **after** the existing `confirm-changes-banner` block and its `v-else-if` sibling (around line 609), but only for admin/board. The order of `v-if`/`v-else-if` guards must not conflict with the existing ones.

The existing pattern:

```html
<!-- Confirm-changes banner -->
<div v-if="registration.status === 'Draft' && registration.hasPendingUserAcknowledgement" ...>
  ...
</div>

<!-- Generic draft info when no pending ack -->
<Message v-else-if="registration.status === 'Draft' && isRepresentative" ...>
  ...
</Message>
```

Add a new banner **inside** the existing `v-if="registration.status === 'Draft' && registration.hasPendingUserAcknowledgement"` block, just above the "Confirmar cambios" button. This way it coexists with the confirm-changes UI:

```html
<!-- Unnotified-changes admin warning (shown inside the confirm-changes banner) -->
<div
  v-if="isAdminOrBoard && hasUnnotifiedDraftChanges"
  class="mb-3 flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 p-3"
  data-testid="unnotified-draft-banner"
>
  <i class="pi pi-exclamation-triangle mt-0.5 text-amber-600" />
  <div class="flex-1">
    <p class="text-sm font-medium text-amber-800">
      La familia <strong>no ha sido notificada</strong> de los cambios en esta inscripción.
    </p>
    <Button
      label="Notificar a la familia"
      icon="pi pi-send"
      severity="warning"
      size="small"
      class="mt-2"
      :loading="notifyingDraft"
      @click="handleNotifyDraft"
      data-testid="notify-draft-btn"
    />
  </div>
</div>
```

- **Placement**: Inside the existing `confirm-changes-banner` `<div>`, as the first child before the existing text content, so it appears at the top of the orange banner.
- **`data-testid`**: `unnotified-draft-banner` and `notify-draft-btn` for test targeting.

---

### Step 5: Write Unit Tests for `notifyDraft`

- **File**: `frontend/src/composables/__tests__/useRegistrations.notifyDraft.test.ts` (new file)
- **Pattern**: Follow `useRegistrations.changeStatus.test.ts` exactly — `vi.mock('@/utils/api')`, describe block, `beforeEach(() => vi.clearAllMocks())`.

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useRegistrations } from '../useRegistrations'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() }
}))

const mockRegistration = {
  id: 'reg-1',
  // ... same mock as in changeStatus tests, with familyNotifiedOfDraft: false
  familyNotifiedOfDraft: false,
  hasPendingUserAcknowledgement: true,
  status: 'Draft',
  // ... rest of required fields
}

describe('useRegistrations - notifyDraft', () => {
  beforeEach(() => vi.clearAllMocks())

  it('should return true and set familyNotifiedOfDraft to true on success', async () => {
    // Arrange: api.post resolves (204 no body)
    vi.mocked(api.post).mockResolvedValueOnce({ data: null })

    // Pre-load registration ref so the optimistic update can fire
    vi.mocked(api.get).mockResolvedValueOnce({
      data: { success: true, data: mockRegistration, error: null }
    })
    const { registration, getRegistrationById, notifyDraft } = useRegistrations()
    await getRegistrationById('reg-1')

    // Act
    const result = await notifyDraft('reg-1')

    // Assert
    expect(result).toBe(true)
    expect(api.post).toHaveBeenCalledWith('/registrations/reg-1/notify-draft')
    expect(registration.value?.familyNotifiedOfDraft).toBe(true)
  })

  it('should return false and set error on API failure', async () => {
    vi.mocked(api.post).mockRejectedValueOnce({
      response: { data: { error: { message: 'La familia ya ha sido notificada.' } } }
    })

    const { error, notifyDraft } = useRegistrations()
    const result = await notifyDraft('reg-1')

    expect(result).toBe(false)
    expect(error.value).toBe('La familia ya ha sido notificada.')
  })

  it('should set loading true during request and false after', async () => {
    let resolvePromise!: (v: unknown) => void
    vi.mocked(api.post).mockReturnValueOnce(
      new Promise((res) => { resolvePromise = res })
    )

    const { loading, notifyDraft } = useRegistrations()
    const promise = notifyDraft('reg-1')
    expect(loading.value).toBe(true)
    resolvePromise({ data: null })
    await promise
    expect(loading.value).toBe(false)
  })

  it('should not update registration ref when id does not match', async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: null })
    // registration.value is null (not loaded)

    const { registration, notifyDraft } = useRegistrations()
    await notifyDraft('reg-other')

    expect(registration.value).toBeNull()
  })
})
```

- **Update existing mock objects** in `useRegistrations.changeStatus.test.ts`: add `familyNotifiedOfDraft: false` to `mockRegistration` to satisfy the updated TypeScript interface.

---

### Step 6: Update Technical Documentation

- **File**: `ai-specs/specs/api-endpoints.md`
  - Verify `POST /api/registrations/{id}/notify-draft` is listed (backend plan already covers this, confirm the frontend perspective: `204 No Content`, admin/board only).
- **Notes**: No other documentation file changes needed for this frontend work — no new component patterns, no new routing, no new npm packages.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `familyNotifiedOfDraft` to `RegistrationResponse` type
3. Step 2 — Add `notifyDraft` to `useRegistrations` composable
4. Step 3 — Update `AdminStatusChangeDialog.vue` (disable toggle + helper text)
5. Step 4 — Add warning banner to `RegistrationDetailPage.vue`
6. Step 5 — Write unit tests for `notifyDraft`
7. Step 6 — Documentation update

---

## Testing Checklist

- [ ] `notifyDraft` returns `true` and patches `registration.value.familyNotifiedOfDraft = true` on success
- [ ] `notifyDraft` returns `false` and sets `error.value` on API failure
- [ ] `notifyDraft` manages `loading` correctly
- [ ] `mockRegistration` in existing test files compiles without TypeScript errors after adding `familyNotifiedOfDraft`
- [ ] `AdminStatusChangeDialog` — selecting `FullyPaid`/`Draft`/`Cancelled` (from Draft) disables the toggle
- [ ] `AdminStatusChangeDialog` — selecting `Pending`/`PartiallyPaid`/`Confirmed` enables the toggle
- [ ] `AdminStatusChangeDialog` — switching from an enabled to a disabled status resets `notifyUser` to `false`
- [ ] Warning banner renders when `status === 'Draft' && hasPendingUserAcknowledgement && !familyNotifiedOfDraft` and user is admin/board
- [ ] Warning banner is NOT rendered for representative (member) users
- [ ] Warning banner is NOT rendered when `familyNotifiedOfDraft === true`
- [ ] "Notificar a la familia" button in the banner calls `notifyDraft`, shows success toast, banner disappears
- [ ] "Notificar a la familia" button shows error toast on failure
- [ ] `vitest run` passes all tests including new ones
- [ ] TypeScript strict check: `vue-tsc --noEmit` passes

---

## Error Handling Patterns

- `notifyDraft` follows the same error pattern as all other composable methods: `error.value` is set from `response.data.error.message`, with a fallback string.
- The caller (`handleNotifyDraft` in `RegistrationDetailPage.vue`) reads `error.value` from the composable and forwards it to `useToast`.
- The "Notificar a la familia" toggle in `AdminStatusChangeDialog` is silently disabled — no error state needed, just helper text.

---

## UI/UX Considerations

- **Warning banner colours**: `border-amber-200 bg-amber-50 text-amber-800` — amber/orange tones, consistent with the existing confirm-changes banner that uses `border-orange-200 bg-orange-50`.
- **Icon**: `pi pi-exclamation-triangle` (already used elsewhere in the app, no new assets).
- **Button size**: `size="small"` inside the banner so it doesn't dominate the card.
- **`data-testid`**: `unnotified-draft-banner` and `notify-draft-btn` — ready for Cypress targeting.
- **Toggle disabled state**: PrimeVue `ToggleSwitch` accepts `:disabled` prop; the label colour shifts from `text-gray-700` to `text-gray-400` and the cursor to `not-allowed` to clearly indicate the disabled state.
- **Accessibility**: The helper text `"Este cambio de estado no envía notificación por correo."` is rendered in a `<p>` tag below the toggle row, visible to screen readers without ARIA hacks.

---

## Dependencies

No new npm packages required. All PrimeVue components used are already imported in the affected files:

| Component | Already imported in |
| --- | --- |
| `Message` | `RegistrationDetailPage.vue` line 6 |
| `Button` | `RegistrationDetailPage.vue` and `AdminStatusChangeDialog.vue` |
| `ToggleSwitch` | `AdminStatusChangeDialog.vue` |

---

## Notes

- **TypeScript strict mode**: All new code must pass `vue-tsc --noEmit`. No `any` types. `registration.value` must be guarded with `?.` before accessing `.familyNotifiedOfDraft`.
- **Language**: All user-facing strings remain in Spanish (`"La familia no ha sido notificada…"`, `"Notificar a la familia"`, `"Este cambio de estado no envía notificación por correo."`). All variable/function names in English.
- **`watch` import**: Ensure `watch` is included in the Vue import in `AdminStatusChangeDialog.vue`. Current import is `import { ref, computed } from 'vue'` — update to `import { ref, computed, watch } from 'vue'`.
- **`STATUSES_WITH_EMAIL` constant**: This constant mirrors the backend switch in `ChangeStatusAsync`. If the backend adds more statuses in future, this constant must be updated too. Consider a brief code comment explaining this coupling.
- **`familyNotifiedOfDraft` vs `hasPendingUserAcknowledgement`**: The banner condition requires BOTH — a registration can be in Draft without `hasPendingUserAcknowledgement` (edge case), so both guards are needed.

---

## Next Steps After Implementation

1. Integration test: deploy backend branch + frontend branch together on a staging environment with a real Resend key and verify emails arrive.
2. QA: test the full flow — admin edits registration with `NotifyUser: false` → banner appears → clicks "Notificar a la familia" → email arrives.

---

## Implementation Verification

- [ ] **TypeScript**: `vue-tsc --noEmit` passes with no errors
- [ ] **Lint**: `eslint frontend/src --ext .ts,.vue` passes
- [ ] **Tests**: `vitest run` — all existing + new tests pass
- [ ] **Feature**: Warning banner appears for admin/board when `familyNotifiedOfDraft === false`
- [ ] **Feature**: Banner disappears after "Notificar a la familia" is clicked and succeeds
- [ ] **Feature**: Toggle disabled for status targets without an email template
- [ ] **Feature**: `notifyDraft` POST hits `/registrations/{id}/notify-draft`
- [ ] **Documentation**: `api-endpoints.md` reviewed/updated

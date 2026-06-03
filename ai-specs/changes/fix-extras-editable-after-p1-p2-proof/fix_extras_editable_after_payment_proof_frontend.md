# Frontend Implementation Plan: fix-extras-editable-after-p1-p2-proof

## Overview

A focused UI bug fix in `RegistrationDetailPage.vue`. The "Editar extras" button is incorrectly hidden whenever any payment proof exists (P1 or P2), using the same `canUserEdit` guard as "Editar participantes". The fix introduces a dedicated `canUserEditExtras` computed that only checks the P3 installment's status, allowing extras to be edited independently of P1/P2 proofs.

**Architecture:** Pure component-level change. No new composables, no Pinia store changes, no routing changes. Two files touched: the view and its spec.

---

## Architecture Context

**Components involved:**
- `frontend/src/views/registrations/RegistrationDetailPage.vue` — the only file with logic changes

**Types used (no changes needed):**
- `frontend/src/types/payment.ts` — `PaymentResponse` (has `installmentNumber: number`, `status: PaymentStatus`, `proofFileUrl: string | null`)
- `frontend/src/types/registration.ts` — `PaymentStatus = 'Pending' | 'PendingReview' | 'Completed' | 'Failed' | 'Refunded'`

**Tests:**
- `frontend/src/views/__tests__/RegistrationDetailPage.spec.ts` — add new describe block

**State:** `installments` is a local `ref<PaymentResponse[]>([])` in the component, populated from `getRegistrationPayments()` on mount (line 289). No store involvement.

**No routing changes.** No new composables. No new types.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Branch name:** `feature/fix-extras-editable-after-p1-p2-proof-frontend`
- **Base branch:** `dev`
- **Commands:**
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/fix-extras-editable-after-p1-p2-proof-frontend
  git branch
  ```

---

### Step 1: Add `canUserEditExtras` computed in `RegistrationDetailPage.vue`

**File:** `frontend/src/views/registrations/RegistrationDetailPage.vue`

**Location:** Directly after the existing `canUserEdit` computed (currently lines 152–158).

**Action:** Add a new `canUserEditExtras` computed. The only difference from `canUserEdit` is the final guard: instead of checking whether *any* installment has a `proofFileUrl`, it checks only whether **P3** is in a non-editable status (`PendingReview` or `Completed`).

```typescript
const canUserEditExtras = computed(() => {
  if (!registration.value) return false
  const status = registration.value.status
  if (status !== 'Pending' && status !== 'Draft') return false
  if (!isRepresentative.value) return false
  const p3 = installments.value.find((p) => p.installmentNumber === 3)
  return !p3 || (p3.status !== 'PendingReview' && p3.status !== 'Completed')
})
```

**Implementation notes:**
- `installments` is the existing local `ref<PaymentResponse[]>` at line 97.
- `PaymentStatus` values `'PendingReview'` and `'Completed'` match the backend enum exactly (see `frontend/src/types/registration.ts:25`).
- If no P3 exists yet (no extras selected before), `find()` returns `undefined` → `!p3` is `true` → extras are editable. Correct.
- Keep the existing `canUserEdit` and `canEdit` unchanged — they still guard "Editar participantes".

---

### Step 2: Split the button group in the template

**File:** `frontend/src/views/registrations/RegistrationDetailPage.vue`

**Location:** Lines ~827–850 — the `<div v-if="canEdit || canAdminEdit" class="flex gap-2">` that wraps both edit buttons.

**Action:** Change the wrapping `<div>` condition to cover both buttons, then add individual `v-if` guards on each button.

**Old template:**
```html
<div v-if="canEdit || canAdminEdit" class="flex gap-2">
  <Button
    label="Editar participantes"
    icon="pi pi-pencil"
    size="small"
    severity="secondary"
    outlined
    :loading="loadingEditData && !isEditingMembers"
    data-testid="edit-members-btn"
    @click="startEditingMembers"
  />
  <Button
    label="Editar extras"
    icon="pi pi-pencil"
    size="small"
    severity="secondary"
    outlined
    :loading="loadingEditData && !isEditingExtras"
    data-testid="edit-extras-btn"
    @click="startEditingExtras"
  />
</div>
```

**New template:**
```html
<div v-if="canEdit || canUserEditExtras || canAdminEdit" class="flex gap-2">
  <Button
    v-if="canEdit || canAdminEdit"
    label="Editar participantes"
    icon="pi pi-pencil"
    size="small"
    severity="secondary"
    outlined
    :loading="loadingEditData && !isEditingMembers"
    data-testid="edit-members-btn"
    @click="startEditingMembers"
  />
  <Button
    v-if="canUserEditExtras || canAdminEdit"
    label="Editar extras"
    icon="pi pi-pencil"
    size="small"
    severity="secondary"
    outlined
    :loading="loadingEditData && !isEditingExtras"
    data-testid="edit-extras-btn"
    @click="startEditingExtras"
  />
</div>
```

**Implementation notes:**
- The wrapping `<div>` now shows if *either* button would be visible, preventing an empty flex container from rendering.
- "Editar participantes" uses the original `canEdit || canAdminEdit` guard — unchanged behavior.
- "Editar extras" now uses `canUserEditExtras || canAdminEdit`.
- `canAdminEdit` already bypasses all user guards for admins/board — no change in admin behavior.

---

### Step 3: Add unit tests for the new guard

**File:** `frontend/src/views/__tests__/RegistrationDetailPage.spec.ts`

**Action:** Add a new `describe` block that tests the visibility of the two edit buttons under different payment states. The existing `usePayments` mock uses a factory function that returns `vi.fn().mockResolvedValue([])` — to control per-test return values, introduce a **hoisted** payments ref following the same pattern as `registrationMock`.

**Step 3a — Update the `usePayments` mock to use a hoisted control variable.**

Add a hoisted variable near the top of the file (alongside `authMock`):
```typescript
const paymentsReturnMock = vi.hoisted(() => vi.fn().mockResolvedValue([]))
```

Update the existing `vi.mock('@/composables/usePayments', ...)` block to reference it:
```typescript
vi.mock('@/composables/usePayments', () => ({
  usePayments: () => ({
    getRegistrationPayments: paymentsReturnMock,
    getPaymentSettings: vi.fn().mockResolvedValue(null),
  }),
}))
```

**Note:** This is a refactor of the existing mock — the existing tests will continue to pass because `paymentsReturnMock` defaults to `mockResolvedValue([])`.

**Step 3b — Add a helper to build `PaymentResponse` test fixtures.**

Add near the bottom of the file (alongside `makeRegistration`):
```typescript
const makePayment = (
  installmentNumber: number,
  status: PaymentStatus,
  proofFileUrl: string | null = null
): PaymentResponse => ({
  id: `pay-${installmentNumber}`,
  registrationId: 'reg-1',
  installmentNumber,
  amount: 200,
  dueDate: null,
  method: 'Transfer',
  status,
  transferConcept: null,
  proofFileUrl,
  proofFileName: null,
  proofUploadedAt: null,
  adminNotes: null,
  createdAt: '2026-01-01T00:00:00Z',
  isActionable: false,
  isManual: false,
  conceptLines: null,
  extraConceptLines: null,
  manualConceptLine: null,
})
```

Import `PaymentStatus` and `PaymentResponse` from `@/types/payment` or `@/types/registration` at the top.

**Step 3c — Add the new describe block.**

```typescript
describe('RegistrationDetailPage — extras edit button visibility', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    routeQueryMock.value = {}
    registrationMock.value = makeRegistration()
    paymentsReturnMock.mockResolvedValue([])
  })

  it('shows both edit buttons when no payments exist', async () => {
    paymentsReturnMock.mockResolvedValue([])
    const wrapper = mountPage()
    await nextTick()
    await nextTick() // allow async init

    expect(wrapper.find('[data-testid="edit-members-btn"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="edit-extras-btn"]').exists()).toBe(true)
  })

  it('hides edit-members but shows edit-extras when P1 has a proof', async () => {
    paymentsReturnMock.mockResolvedValue([
      makePayment(1, 'PendingReview', 'https://blob/p1.pdf'),
    ])
    const wrapper = mountPage()
    await nextTick()
    await nextTick()

    expect(wrapper.find('[data-testid="edit-members-btn"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="edit-extras-btn"]').exists()).toBe(true)
  })

  it('hides edit-members but shows edit-extras when P2 has a proof', async () => {
    paymentsReturnMock.mockResolvedValue([
      makePayment(1, 'Completed', 'https://blob/p1.pdf'),
      makePayment(2, 'PendingReview', 'https://blob/p2.pdf'),
    ])
    const wrapper = mountPage()
    await nextTick()
    await nextTick()

    expect(wrapper.find('[data-testid="edit-members-btn"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="edit-extras-btn"]').exists()).toBe(true)
  })

  it('hides edit-extras when P3 is PendingReview', async () => {
    paymentsReturnMock.mockResolvedValue([
      makePayment(1, 'Completed', 'https://blob/p1.pdf'),
      makePayment(2, 'Completed', 'https://blob/p2.pdf'),
      makePayment(3, 'PendingReview', 'https://blob/p3.pdf'),
    ])
    const wrapper = mountPage()
    await nextTick()
    await nextTick()

    expect(wrapper.find('[data-testid="edit-extras-btn"]').exists()).toBe(false)
  })

  it('hides edit-extras when P3 is Completed', async () => {
    paymentsReturnMock.mockResolvedValue([
      makePayment(1, 'Completed', 'https://blob/p1.pdf'),
      makePayment(2, 'Completed', 'https://blob/p2.pdf'),
      makePayment(3, 'Completed', 'https://blob/p3.pdf'),
    ])
    const wrapper = mountPage()
    await nextTick()
    await nextTick()

    expect(wrapper.find('[data-testid="edit-extras-btn"]').exists()).toBe(false)
  })
})
```

**Implementation notes:**
- The component populates `installments.value` on mount (line ~289 and line ~568/574). Two `await nextTick()` calls cover the async resolution.
- The `makeRegistration()` fixture sets `familyUnit.representativeUserId: 'u1'` and `authMock.user.id: 'u1'`, so `isRepresentative` is `true` in all tests.
- Registration status is `'Pending'` in the fixture — satisfies the status guard.
- `data-testid` attributes `edit-members-btn` and `edit-extras-btn` already exist on the buttons (lines ~837 and ~847).

---

### Step 4: Run tests

```bash
cd frontend
npm run test -- --reporter=verbose src/views/__tests__/RegistrationDetailPage.spec.ts
```

All existing tests must continue passing. The new describe block adds 5 tests, all expected to be green.

---

### Step 5: Update Technical Documentation

**No API spec changes** — no endpoint or type changes were made.

**No frontend-standards.mdc changes** — this fix follows an existing pattern (computed guards in `RegistrationDetailPage.vue`).

If the project maintains a CHANGELOG or any UX documentation describing the extras edit flow, update it to reflect the corrected rule: "Extras can be edited until the extras payment (P3) is submitted."

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `canUserEditExtras` computed
3. Step 2 — Split button visibility in template
4. Step 3 — Add unit tests (update mock, add helper, add describe block)
5. Step 4 — Run tests (verify all green)
6. Step 5 — Documentation check

---

## Testing Checklist

- [ ] Existing `RegistrationDetailPage` back-navigation tests still pass (no regression from mock refactor)
- [ ] `shows both edit buttons when no payments exist` — passes
- [ ] `hides edit-members but shows edit-extras when P1 has a proof` — passes
- [ ] `hides edit-members but shows edit-extras when P2 has a proof` — passes
- [ ] `hides edit-extras when P3 is PendingReview` — passes
- [ ] `hides edit-extras when P3 is Completed` — passes
- [ ] TypeScript: no `any`, `<script setup lang="ts">` confirmed
- [ ] `npm run type-check` passes with no errors

---

## Error Handling Patterns

No new error paths introduced. If the backend returns `422 BUSINESS_RULE_VIOLATION` when `SetExtras` is called after the deadline (new backend guard), the existing error toast in `handleSaveExtras` already catches and displays it — no changes needed.

---

## UI/UX Considerations

- **No visual change for non-affected scenarios** — when no payments exist, both buttons remain visible exactly as before.
- **New scenario** — when P1/P2 proofs exist but P3 is `Pending`, only "Editar extras" remains visible. "Editar participantes" disappears (existing behavior, unchanged).
- **Accessibility** — `data-testid` attributes on both buttons allow screen-reader and automated test identification; no new ARIA additions needed.
- **Loading state** — the `:loading="loadingEditData && !isEditingExtras"` remains on the extras button, unchanged.

---

## Dependencies

No new npm packages. No new PrimeVue components.

---

## Notes

- **TypeScript strict**: `PaymentStatus` is already a union type in `registration.ts`. No `any` needed.
- **Computed granularity**: `canUserEditExtras` is intentionally a separate computed from `canUserEdit` — do NOT merge them. `canEdit` stays as an alias for `canUserEdit` (line 164) to keep "Editar participantes" behavior unchanged.
- **Admin bypass**: `canAdminEdit` already bypasses all user guards. Admins can always edit both members and extras regardless of payment state. This is correct and unchanged.
- **Reactivity**: `installments` is a `ref`, so `canUserEditExtras` reacts automatically when payments are updated (e.g., after an admin confirms P3).

---

## Next Steps After Implementation

- Backend fix tracked separately in `fix_extras_editable_after_payment_proof_backend.md`.
- This frontend fix can be implemented independently of the backend fix, but both must be deployed together for the full feature to work correctly.
- After merging both, test end-to-end: upload P1/P2 proof → verify "Editar extras" remains visible → add an extra → verify P3 is created → upload P3 proof → verify "Editar extras" disappears.

---

## Implementation Verification

- [ ] `canUserEditExtras` computed added after `canUserEdit` (line ~158)
- [ ] Wrapping `<div>` condition updated to `v-if="canEdit || canUserEditExtras || canAdminEdit"`
- [ ] "Editar participantes" has `v-if="canEdit || canAdminEdit"`
- [ ] "Editar extras" has `v-if="canUserEditExtras || canAdminEdit"`
- [ ] `paymentsReturnMock` hoisted and used in `vi.mock('@/composables/usePayments', ...)`
- [ ] `makePayment` helper added to spec file
- [ ] 5 new tests in new `describe` block
- [ ] All tests pass with `npm run test`
- [ ] `npm run type-check` passes

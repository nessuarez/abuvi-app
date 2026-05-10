# Frontend Implementation Plan: feat-payment-adjustments — Payment Adjustments & Admin Registration Management

## Overview

Admin-facing UI for four new payment correction capabilities: editing any payment's amount/concept, confirming multiple payments from a single bank transfer, and removing registration members with automatic refund generation. Built with Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue components, and Tailwind CSS. Follows the existing composable-based architecture in `usePayments.ts` and `useRegistrations.ts`.

---

## Architecture Context

**Components to create (new)**:
- `frontend/src/components/admin/AdminEditPaymentDialog.vue` — edit any payment (amount, concept, notes)
- `frontend/src/components/admin/ConfirmCombinedPaymentsDialog.vue` — confirm P1+P2 from a single transfer

**Components to modify**:
- `frontend/src/components/admin/PaymentsAllList.vue` — add Edit action to all payments; add multi-select + Confirm combined; show `conceptOverridden` badge
- `frontend/src/views/registrations/RegistrationDetailPage.vue` — admin member edit section uses new endpoint; shows refund warning before removing paid members

**Composables to modify**:
- `frontend/src/composables/usePayments.ts` — add `adminEditPayment()`, `confirmCombinedPayments()`
- `frontend/src/composables/useRegistrations.ts` — add `adminUpdateMembers()`

**Types to modify**:
- `frontend/src/types/payment.ts` — extend `AdminPaymentResponse`; add `AdminEditPaymentRequest`, `ConfirmCombinedPaymentsRequest`

**Routing**: No new routes — all functionality is within existing `/admin/payments` and `/registrations/:id` pages.

**State management**: All state is local to composable instances (no Pinia store changes). Components consume `usePayments()` and `useRegistrations()` locally.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/feat-payment-adjustments-frontend`
- **Base branch**: `dev`
- **Commands**:
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/feat-payment-adjustments-frontend
  git branch
  ```

---

### Step 1: Update TypeScript Type Definitions

**File**: `frontend/src/types/payment.ts`

**Action**: Add two fields to `AdminPaymentResponse` and add two new request interfaces.

#### 1a. Extend `AdminPaymentResponse`

Add after `confirmedAt`:
```typescript
conceptOverridden: boolean
originalAmount: number | null
```

Full updated interface:
```typescript
export interface AdminPaymentResponse extends PaymentResponse {
  familyUnitName: string
  campEditionName: string
  confirmedByUserName: string | null
  confirmedAt: string | null
  conceptOverridden: boolean
  originalAmount: number | null
}
```

#### 1b. Add `AdminEditPaymentRequest`

```typescript
export interface AdminEditPaymentRequest {
  amount?: number | null
  conceptDescription?: string | null
  dueDate?: string | null
  adminNotes?: string | null
}
```

#### 1c. Add `ConfirmCombinedPaymentsRequest`

```typescript
export interface ConfirmCombinedPaymentsRequest {
  paymentIds: string[]
  totalReceivedAmount: number
  applySurplusToNext?: boolean
  adminNotes?: string | null
}
```

---

### Step 2: Add New Methods to `usePayments.ts`

**File**: `frontend/src/composables/usePayments.ts`

**Action**: Add two new admin methods following the exact same error-handling pattern as existing methods.

#### 2a. `adminEditPayment`

```typescript
const adminEditPayment = async (
  paymentId: string,
  request: AdminEditPaymentRequest
): Promise<AdminPaymentResponse | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.put<ApiResponse<AdminPaymentResponse>>(
      `/admin/payments/${paymentId}`,
      request
    )
    if (response.data.success && response.data.data) return response.data.data
    error.value = response.data.error?.message ?? 'Error al actualizar el pago'
    return null
  } catch (err: unknown) {
    error.value = extractError(err, 'Error al actualizar el pago')
    console.error('Failed to edit payment:', err)
    return null
  } finally {
    loading.value = false
  }
}
```

#### 2b. `confirmCombinedPayments`

```typescript
const confirmCombinedPayments = async (
  registrationId: string,
  request: ConfirmCombinedPaymentsRequest
): Promise<AdminPaymentResponse[] | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.post<ApiResponse<AdminPaymentResponse[]>>(
      `/admin/registrations/${registrationId}/payments/confirm-combined`,
      request
    )
    if (response.data.success && response.data.data) return response.data.data
    error.value = response.data.error?.message ?? 'Error al confirmar los pagos combinados'
    return null
  } catch (err: unknown) {
    error.value = extractError(err, 'Error al confirmar los pagos combinados')
    console.error('Failed to confirm combined payments:', err)
    return null
  } finally {
    loading.value = false
  }
}
```

**Update the return object** of `usePayments()` to include both new methods.

**Add imports** at the top:
```typescript
import type {
  // existing...
  AdminEditPaymentRequest,
  ConfirmCombinedPaymentsRequest
} from '@/types/payment'
```

---

### Step 3: Add `adminUpdateMembers` to `useRegistrations.ts`

**File**: `frontend/src/composables/useRegistrations.ts`

**Action**: Add one new admin method that calls the new admin-scoped member update endpoint.

```typescript
const adminUpdateMembers = async (
  registrationId: string,
  request: UpdateRegistrationMembersRequest
): Promise<RegistrationDetail | null> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.put<ApiResponse<RegistrationDetail>>(
      `/admin/registrations/${registrationId}/members`,
      request
    )
    if (response.data.success && response.data.data) return response.data.data
    error.value = response.data.error?.message ?? 'Error al actualizar los participantes'
    return null
  } catch (err: unknown) {
    error.value = extractError(err, 'Error al actualizar los participantes')
    console.error('Failed to admin update members:', err)
    return null
  } finally {
    loading.value = false
  }
}
```

**Update the return object** to include `adminUpdateMembers`.

> **Note**: `UpdateRegistrationMembersRequest` and `RegistrationDetail` are already defined in the existing registration types. No new types needed for this method.

---

### Step 4: Create `AdminEditPaymentDialog.vue`

**File**: `frontend/src/components/admin/AdminEditPaymentDialog.vue`

**Purpose**: Dialog for editing any payment's amount, concept description, due date, and admin notes.

**Props**:
```typescript
defineProps<{
  visible: boolean
  payment: AdminPaymentResponse
}>()
```

**Emits**:
```typescript
defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'saved', payment: AdminPaymentResponse): void
}>()
```

**Internal state**:
- `amount: Ref<number | null>` — initialized from `payment.amount`
- `conceptDescription: Ref<string>` — initialized empty (only set if admin wants to override)
- `dueDate: Ref<Date | null>` — initialized from `payment.dueDate`
- `adminNotes: Ref<string>` — initialized from `payment.adminNotes`
- `overrideConceptChecked: Ref<boolean>` — checkbox to unlock concept override field

**Computed**:
- `isCompleted: computed(() => payment.status === 'Completed')` — drives warning banner visibility

**UI layout**:
1. **Warning banner** (shown only if `isCompleted`):
   ```
   PrimeVue <Message severity="warn">
   Estás editando un pago ya completado. Si cambias el importe, los pagos pendientes
   posteriores serán recalculados automáticamente.
   </Message>
   ```
2. **Original amount info** (shown only if `payment.conceptOverridden && payment.originalAmount`):
   ```
   <Message severity="info">
   Importe original: {formatCurrency(payment.originalAmount)}
   </Message>
   ```
3. **Amount field**: `InputNumber` — locale `es-ES`, min 0.01, max 99999.99, `fractionDigits: 2`
4. **Concept override section**: Checkbox "Reemplazar descripción del concepto" + `InputText` (shown when checked)
5. **Due date field**: `DateInput` shared component
6. **Admin notes**: `Textarea` (optional)
7. **Footer buttons**: "Cancelar" + "Guardar cambios" (disabled while loading)

**Save handler**:
```typescript
const handleSave = async () => {
  const request: AdminEditPaymentRequest = {}
  if (amount.value !== props.payment.amount) request.amount = amount.value
  if (overrideConceptChecked.value && conceptDescription.value)
    request.conceptDescription = conceptDescription.value
  if (dueDate.value) request.dueDate = formatDateForApi(dueDate.value)
  if (adminNotes.value !== props.payment.adminNotes) request.adminNotes = adminNotes.value

  const result = await adminEditPayment(props.payment.id, request)
  if (result) {
    toast.add({ severity: 'success', summary: 'Pago actualizado', life: 3000 })
    emit('saved', result)
    emit('update:visible', false)
  }
}
```

**Reset on open**:
Use `watch(() => props.visible, (val) => { if (val) resetForm() })` — same pattern as `ManualPaymentDialog.vue`.

---

### Step 5: Create `ConfirmCombinedPaymentsDialog.vue`

**File**: `frontend/src/components/admin/ConfirmCombinedPaymentsDialog.vue`

**Purpose**: Admin confirms multiple installments (e.g., P1 + P2) from a single bank transfer with amount distribution.

**Props**:
```typescript
defineProps<{
  visible: boolean
  registrationId: string
  familyUnitName: string
  payments: AdminPaymentResponse[]  // pre-filtered: Pending/PendingReview, non-manual
}>()
```

**Emits**:
```typescript
defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'confirmed', payments: AdminPaymentResponse[]): void
}>()
```

**Internal state**:
- `selectedPaymentIds: Ref<string[]>` — checkboxes for which payments to include (initialized with all passed payments)
- `totalReceivedAmount: Ref<number | null>` — single bank transfer amount
- `applySurplusToNext: Ref<boolean>` — toggle for surplus handling
- `adminNotes: Ref<string>`

**Computed — amount distribution preview**:
```typescript
const distributionPreview = computed(() => {
  if (!totalReceivedAmount.value || selectedPayments.value.length === 0) return []
  let remaining = totalReceivedAmount.value
  return selectedPayments.value.map(p => {
    const assigned = Math.min(remaining, p.amount)
    remaining -= assigned
    return { payment: p, assignedAmount: assigned }
  })
})
```

**UI layout**:
1. **Header**: "Confirmar pago combinado para {familyUnitName}"
2. **Payment selection**: Checkbox list showing each payment — "P{n} — {installmentLabel} — {amount}€"
3. **Total received amount**: `InputNumber` with label "Importe recibido (€)"
4. **Distribution preview table** (shown when `totalReceivedAmount` is filled):
   - Two columns: "Pago" | "Importe asignado"
   - Greedy fill shown in real-time
   - If surplus > 0: show "Sobrante: {surplus}€" in amber
5. **"Aplicar sobrante al siguiente pago"** toggle (shown when surplus > 0)
6. **Admin notes**: `Textarea`
7. **Footer**: "Cancelar" + "Confirmar pagos" button

**Confirm handler**:
```typescript
const handleConfirm = async () => {
  if (!totalReceivedAmount.value || selectedPaymentIds.value.length === 0) return
  const result = await confirmCombinedPayments(props.registrationId, {
    paymentIds: selectedPaymentIds.value,
    totalReceivedAmount: totalReceivedAmount.value,
    applySurplusToNext: applySurplusToNext.value,
    adminNotes: adminNotes.value || null
  })
  if (result) {
    toast.add({ severity: 'success', summary: 'Pagos confirmados', life: 3000 })
    emit('confirmed', result)
    emit('update:visible', false)
  }
}
```

---

### Step 6: Update `PaymentsAllList.vue`

**File**: `frontend/src/components/admin/PaymentsAllList.vue`

**Actions** (four independent additions):

#### 6a. Add `AdminEditPaymentDialog` + wire edit action for ALL payments

1. Import and register `AdminEditPaymentDialog`.
2. Add state:
   ```typescript
   const showAdminEditDialog = ref(false)
   const adminEditTarget = ref<AdminPaymentResponse | null>(null)
   ```
3. In the DataTable row actions, add an "Edit" button for **all** payments (not just manual). Existing manual payments have a pencil button — make it visible for all:
   ```html
   <Button icon="pi pi-pencil" size="small" text
     v-tooltip="'Editar pago'"
     @click="openAdminEdit(row)" />
   ```
4. `openAdminEdit(payment: AdminPaymentResponse)` sets `adminEditTarget = payment; showAdminEditDialog = true`.
5. On `@saved` event: replace the updated payment in the local `payments` array; show success toast.

#### 6b. Show `conceptOverridden` visual indicator

In the Amount column (or as a separate column suffix), add a PrimeVue `Tag` when `payment.conceptOverridden`:
```html
<span>{{ formatCurrency(payment.amount) }}</span>
<Tag v-if="payment.conceptOverridden" value="Ajustado" severity="warn"
     class="ml-1 text-xs"
     v-tooltip="payment.originalAmount
       ? `Importe original: ${formatCurrency(payment.originalAmount)}`
       : 'Importe ajustado por administración'" />
```

#### 6c. Add multi-select + "Confirm Combined" button

1. Enable row selection on `DataTable`: add `v-model:selection="selectedPayments"` and `selection-mode="multiple"`.
2. Add `selectedPayments: Ref<AdminPaymentResponse[]>` state.
3. Add a computed `canConfirmCombined` that is `true` when:
   - `selectedPayments.length >= 2`
   - All selected are from the **same registration** (`registrationId` all equal)
   - All selected are in `Pending` or `PendingReview` status
   - None are manual (`isManual === false`)
4. Add a "Confirmar pago combinado" button above the DataTable (shown only when `canConfirmCombined`):
   ```html
   <Button v-if="canConfirmCombined"
     label="Confirmar pago combinado"
     icon="pi pi-check-circle"
     severity="success"
     @click="openConfirmCombined" />
   ```
5. `openConfirmCombined()` sets `showConfirmCombinedDialog = true`.
6. On `@confirmed` event: refresh the payment list; clear selection.

#### 6d. Register `ConfirmCombinedPaymentsDialog`

1. Import and register `ConfirmCombinedPaymentsDialog`.
2. Pass `selectedPayments`, `registrationId` (from first selected payment), `familyUnitName` (from first selected payment).
3. Add `showConfirmCombinedDialog: Ref<boolean>` and `confirmCombinedRegistrationId: Ref<string>`.

**New destructured composable methods**:
```typescript
const {
  // existing...
  adminEditPayment,
  confirmCombinedPayments,
} = usePayments()
```

---

### Step 7: Update `RegistrationDetailPage.vue` for Admin Member Edit with Refund Warning

**File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`

**Action**: When an admin uses the member editor and removes a member whose portion has been paid, show a warning confirmation dialog before submitting.

**Implementation steps**:

1. **Detect admin role**: Use `authStore.isBoard || authStore.isAdmin` to conditionally show the admin member edit UI.

2. **Import `adminUpdateMembers`**:
   ```typescript
   const { adminUpdateMembers } = useRegistrations()
   ```

3. **Add refund warning state**:
   ```typescript
   const showRefundWarning = ref(false)
   const pendingMemberRequest = ref<UpdateRegistrationMembersRequest | null>(null)
   const estimatedRefundAmount = ref<number>(0)
   ```

4. **Compute whether removal would trigger refund**:
   ```typescript
   // Before submitting: compare original member list with new list
   // If any removed member had their IndividualAmount included in a Completed payment, warn
   const computeRefundWarning = (
     originalMembers: RegistrationMember[],
     newMemberIds: string[],
     completedBasePayments: number
   ): number => {
     const removedMembers = originalMembers.filter(m => !newMemberIds.includes(m.familyMemberId))
     const removedAmount = removedMembers.reduce((sum, m) => sum + m.individualAmount, 0)
     const newBaseTotal = originalMembers.reduce((sum, m) => sum + m.individualAmount, 0) - removedAmount
     return Math.max(0, completedBasePayments - newBaseTotal)
   }
   ```

5. **Save handler flow** (admin member edit):
   ```typescript
   const handleAdminMembersSubmit = async (request: UpdateRegistrationMembersRequest) => {
     const completedBase = registration.payments
       ?.filter(p => p.status === 'Completed' && !p.isManual && p.installmentNumber <= 2)
       .reduce((s, p) => s + p.amount, 0) ?? 0

     if (completedBase > 0) {
       const refund = computeRefundWarning(registration.members, request.members.map(m => m.familyMemberId), completedBase)
       if (refund > 0) {
         estimatedRefundAmount.value = refund
         pendingMemberRequest.value = request
         showRefundWarning.value = true
         return  // wait for user confirmation
       }
     }
     await submitAdminMembersUpdate(request)
   }
   ```

6. **Refund warning dialog** (inline in the template):
   ```html
   <Dialog v-model:visible="showRefundWarning" modal header="Confirmación de cambio">
     <Message severity="warn">
       Al eliminar este participante se generará automáticamente una devolución de
       <strong>{{ formatCurrency(estimatedRefundAmount) }}</strong> en los pagos de la inscripción.
       La inscripción pasará a estado "Borrador" hasta que la familia lo confirme.
     </Message>
     <template #footer>
       <Button label="Cancelar" text @click="showRefundWarning = false" />
       <Button label="Confirmar cambio y generar devolución" severity="warning"
         @click="confirmAndSubmitMembersUpdate" />
     </template>
   </Dialog>
   ```

7. **`submitAdminMembersUpdate`**:
   ```typescript
   const submitAdminMembersUpdate = async (request: UpdateRegistrationMembersRequest) => {
     const result = await adminUpdateMembers(registration.id, request)
     if (result) {
       registration = result  // update local state
       toast.add({ severity: 'success', summary: 'Participantes actualizados',
         detail: 'La inscripción ha pasado a estado Borrador pendiente de confirmación de la familia.',
         life: 5000 })
       showRefundWarning.value = false
       // Reload payments to show new refund payment
       await reloadPayments()
     }
   }
   ```

8. **Show Draft acknowledgment banner** (already partially implemented via `HasPendingUserAcknowledgement`): ensure the existing Draft/pending banner is visible after the member update triggers the Draft transition.

---

### Step 8: Write Unit Tests

**Files**:
- `frontend/src/composables/__tests__/usePayments.test.ts` — extend existing
- `frontend/src/components/admin/__tests__/AdminEditPaymentDialog.test.ts` — new
- `frontend/src/components/admin/__tests__/ConfirmCombinedPaymentsDialog.test.ts` — new

#### 8a. `usePayments` tests

```typescript
describe('adminEditPayment', () => {
  it('calls PUT /admin/payments/{id} with request body', async () => { ... })
  it('returns null and sets error on API failure', async () => { ... })
  it('returns null and sets error on 409 conflict', async () => { ... })
})

describe('confirmCombinedPayments', () => {
  it('calls POST /admin/registrations/{id}/payments/confirm-combined', async () => { ... })
  it('returns updated payments array on success', async () => { ... })
  it('returns null and sets error on API failure', async () => { ... })
})
```

#### 8b. `AdminEditPaymentDialog` component tests

```typescript
describe('AdminEditPaymentDialog', () => {
  it('shows warning banner when payment.status is Completed', async () => { ... })
  it('does NOT show warning banner when payment.status is Pending', async () => { ... })
  it('shows original amount info when conceptOverridden is true', async () => { ... })
  it('calls adminEditPayment with only changed fields', async () => { ... })
  it('emits saved event with updated payment on success', async () => { ... })
  it('shows error message when API returns error', async () => { ... })
  it('resets form when dialog reopens', async () => { ... })
})
```

#### 8c. `ConfirmCombinedPaymentsDialog` component tests

```typescript
describe('ConfirmCombinedPaymentsDialog', () => {
  it('shows distribution preview when totalReceivedAmount is filled', async () => { ... })
  it('greedy fill assigns correctly across payments', async () => { ... })
  it('shows surplus amount in amber when total exceeds all payments', async () => { ... })
  it('shows applySurplusToNext toggle only when surplus > 0', async () => { ... })
  it('calls confirmCombinedPayments with correct request', async () => { ... })
  it('emits confirmed event with updated payments on success', async () => { ... })
})
```

---

### Step 9: Update Technical Documentation

**Action**: Review and update specs documentation.

**Implementation steps**:

1. **`ai-specs/specs/api-endpoints.md`** — already updated by the backend plan. Verify the three new endpoints appear.

2. **No `frontend-standards.mdc` changes needed** — no new patterns introduced beyond existing composable + dialog conventions.

3. **No routing changes** — no updates to routing documentation needed.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Update TypeScript types (`AdminPaymentResponse`, `AdminEditPaymentRequest`, `ConfirmCombinedPaymentsRequest`)
3. Step 2 — Add `adminEditPayment()` and `confirmCombinedPayments()` to `usePayments.ts`
4. Step 3 — Add `adminUpdateMembers()` to `useRegistrations.ts`
5. Step 4 — Create `AdminEditPaymentDialog.vue`
6. Step 5 — Create `ConfirmCombinedPaymentsDialog.vue`
7. Step 6 — Update `PaymentsAllList.vue` (edit action for all, conceptOverridden badge, multi-select + combined confirm)
8. Step 7 — Update `RegistrationDetailPage.vue` (refund warning + `adminUpdateMembers` wiring)
9. Step 8 — Write unit tests
10. Step 9 — Update documentation

---

## Testing Checklist

- [ ] `usePayments.adminEditPayment` — calls correct endpoint, returns null on error
- [ ] `usePayments.confirmCombinedPayments` — calls correct endpoint, returns array on success
- [ ] `useRegistrations.adminUpdateMembers` — calls `/admin/registrations/{id}/members`
- [ ] `AdminEditPaymentDialog` — warning shown for Completed payments; original amount shown when overridden; saves only changed fields
- [ ] `ConfirmCombinedPaymentsDialog` — distribution preview correct; surplus shown; confirm fires correct request
- [ ] `PaymentsAllList` — Edit button visible for all payments (not just manual); `conceptOverridden` Tag shown; multi-select + combined button visible for eligible payments
- [ ] `RegistrationDetailPage` — refund warning appears before removing a paid member; Draft status banner shown after update
- [ ] No TypeScript errors (`vue-tsc --noEmit` passes)
- [ ] No regressions in existing payment tests

---

## Error Handling Patterns

All new composable methods follow the existing pattern in `usePayments.ts`:

```typescript
loading.value = true
error.value = null
try {
  const response = await api.{method}(...)
  if (response.data.success && response.data.data) return response.data.data
  error.value = response.data.error?.message ?? 'Mensaje de error por defecto'
  return null
} catch (err: unknown) {
  error.value = extractError(err, 'Mensaje de error por defecto')
  console.error('...', err)
  return null
} finally {
  loading.value = false
}
```

**User-visible errors** are displayed via:
- PrimeVue `<Message severity="error">` inside the dialog for API errors from the composable `error.value`
- PrimeVue `useToast()` for success notifications

**409 Conflict** (editing Failed/Refunded payment):
- The API returns a Spanish error message — `error.value` is set directly from the response
- Displayed inside the `AdminEditPaymentDialog` as a `<Message severity="error">`

---

## UI/UX Considerations

### `AdminEditPaymentDialog`
- **Warning banner color**: `severity="warn"` (yellow) for completed payment edit — matches PrimeVue's warn palette
- **Original amount**: `severity="info"` (blue) message showing original value for audit trail
- **Concept override**: Hidden by default behind a checkbox to prevent accidental overwrite. Label: "Reemplazar descripción del concepto del pago"
- **Loading state**: "Guardar cambios" button shows spinner while `loading.value === true`

### `ConfirmCombinedPaymentsDialog`
- **Distribution preview**: Rendered as a simple bordered list, not a full DataTable (lightweight)
- **Surplus**: Shown in amber/orange text: "Sobrante: X€ — se descartará" or "Sobrante: X€ — se aplicará al siguiente pago pendiente" depending on toggle
- **Confirm button disabled** until `totalReceivedAmount > 0` and at least one payment selected

### `PaymentsAllList` — `conceptOverridden` Tag
- **Position**: Inline after the amount value in the Amount column
- **Tag label**: "Ajustado" in `severity="warn"` (yellow)
- **Tooltip** (`v-tooltip`): Shows original amount if available — "Importe original: X€"
- **Does not affect sorting/filtering** — purely visual

### Refund warning dialog
- **Severity**: `warn` (yellow) — serious but not blocking
- **Estimated refund**: Calculated client-side from current member data + completed payments
- **Note**: Estimate is approximate; actual refund is computed server-side. Add a note: "El importe exacto de la devolución será calculado por el sistema."

### Responsive design
- All new dialogs use PrimeVue `Dialog` with `style="width: 90vw; max-width: 560px"` — matches existing dialog sizes in the codebase
- Amount distribution preview uses `w-full` table layout — responsive by default

---

## Dependencies

**No new npm packages required.** All functionality uses:
- Existing PrimeVue components: `Dialog`, `Button`, `InputNumber`, `InputText`, `Textarea`, `Message`, `Tag`, `DataTable` (multi-select), `ToggleSwitch`
- Existing utilities: `formatCurrency`, `formatDateLocal`, `formatDateForApi` from `@/utils/`
- Existing composables: `usePayments`, `useRegistrations`, `useToast`

---

## Notes

### Language Requirements
- All labels, placeholder text, error messages, and button labels: **Spanish**
- All TypeScript identifiers, comments, test names: **English**
- All `console.error` messages: **English**

### TypeScript Strict Mode
- No `any` types. All API response shapes typed via `ApiResponse<T>`.
- New interfaces added to `payment.ts` (not inline in components).
- Composable methods fully typed with explicit return types.

### Business Rules to Reflect in UI
- **Edit warning**: Only show Completed-payment warning in `AdminEditPaymentDialog` — not for Pending (no downstream effect).
- **Combined confirm eligibility**: Only auto-generated (non-manual) payments in Pending/PendingReview from the same registration.
- **Refund warning**: Only shown if the removed member's share was already included in a Completed payment (i.e., `completedBasePayments > newBaseTotalAmount`).
- **Draft banner**: After `adminUpdateMembers`, the registration will be in `Draft` with `HasPendingUserAcknowledgement = true` — the existing Draft status banner handles this display.

---

## Next Steps After Implementation

1. **Backend must be deployed** before frontend changes are functional. Coordinate with backend branch `feature/feat-payment-adjustments-backend`.
2. **E2E test** (Cypress): add a flow that:
   - Navigates to admin payments → selects 2 pending payments → confirms combined → verifies both marked Completed
   - Navigates to registration detail → removes a member → confirms refund warning → verifies refund payment in list + Draft banner

---

## Implementation Verification

- [ ] **TypeScript**: `vue-tsc --noEmit` passes with zero errors; no `any` types introduced
- [ ] **Functionality**: `AdminEditPaymentDialog` saves and triggers recalculation toast; `ConfirmCombinedPaymentsDialog` distributes correctly; `adminUpdateMembers` transitions registration to Draft
- [ ] **Testing**: Vitest tests pass; ≥ 90% coverage for new composable methods and dialog components
- [ ] **Integration**: New API endpoints return expected shapes matching updated TypeScript interfaces
- [ ] **Documentation**: `api-endpoints.md` verified to include all three new endpoints

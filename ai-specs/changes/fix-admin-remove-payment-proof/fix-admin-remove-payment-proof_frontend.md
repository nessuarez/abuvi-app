# Frontend Implementation Plan: fix-admin-remove-payment-proof Admin Remove Payment Proof

## Overview

Add a delete-proof action to the admin payments list so that `Admin` and `Board` users can remove an erroneously uploaded payment proof from any payment. The feature follows the existing composable-based architecture: a new `adminRemoveProof` function is added to `usePayments`, and a delete-proof button + confirmation dialog are added to `PaymentsAllList.vue`. No new components, stores, or routes are needed.

---

## Architecture Context

- **Composable**: `frontend/src/composables/usePayments.ts` — add `adminRemoveProof()`
- **Component**: `frontend/src/components/admin/PaymentsAllList.vue` — add button + dialog
- **Types**: `frontend/src/types/payment.ts` — no changes needed (`AdminPaymentResponse` already has `proofFileUrl`, `proofFileName`)
- **State management**: Local component state with `ref()` (same as the existing `deleteTarget` pattern)
- **No new routes**, **no Pinia store changes**, **no new files**

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the frontend feature branch.
- **Implementation Steps**:
  1. Ensure you are on `dev` and it is up to date: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/fix-admin-remove-payment-proof-frontend`
  3. Verify: `git branch`

---

### Step 1: Add `adminRemoveProof` to `usePayments`

- **File**: `frontend/src/composables/usePayments.ts`
- **Action**: Add a new function that calls `DELETE /admin/payments/{paymentId}/proof`. The function returns `boolean` (consistent with `deleteManualPayment`).

**Where to insert**: After `deleteManualPayment` (around line 267), before `adminEditPayment`.

**Function to add**:
```ts
const adminRemoveProof = async (paymentId: string): Promise<boolean> => {
  loading.value = true
  error.value = null
  try {
    await api.delete(`/admin/payments/${paymentId}/proof`)
    return true
  } catch (err: unknown) {
    error.value = extractError(err, 'Error al eliminar el justificante')
    console.error('Failed to remove payment proof:', err)
    return false
  } finally {
    loading.value = false
  }
}
```

- **Note on response**: The endpoint returns `204 No Content` (no body). Do not try to read `response.data` — just check that the call did not throw.

**Expose in return object**: Add `adminRemoveProof` to the `return { ... }` block at the end of the composable (around line 346):
```ts
return {
  // ... existing entries ...
  adminRemoveProof,
}
```

- **Dependencies**: No new imports needed. Uses the existing `api` Axios instance and `extractError` helper already in scope.

---

### Step 2: Add Delete Proof State and Handlers to `PaymentsAllList.vue`

- **File**: `frontend/src/components/admin/PaymentsAllList.vue`

#### 2a — Destructure `adminRemoveProof` from `usePayments`

Update the `usePayments()` destructure (lines 31–37):
```ts
const {
  getAllPayments,
  updateManualPayment,
  deleteManualPayment,
  adminRemoveProof,   // <-- add this
  loading,
  error
} = usePayments()
```

#### 2b — Add local state for the delete-proof dialog

Insert below the existing delete manual payment state block (after line 67):
```ts
// Delete proof dialog
const showDeleteProofDialog = ref(false)
const deleteProofTarget = ref<AdminPaymentResponse | null>(null)
const deletingProof = ref(false)
```

#### 2c — Add open/confirm handlers

Insert after `handleDelete` (after line 194), following the exact same shape as the existing delete handler:
```ts
// Delete proof
const openDeleteProofDialog = (payment: AdminPaymentResponse) => {
  deleteProofTarget.value = payment
  showDeleteProofDialog.value = true
}

const handleDeleteProof = async () => {
  if (!deleteProofTarget.value) return
  deletingProof.value = true
  const success = await adminRemoveProof(deleteProofTarget.value.id)
  deletingProof.value = false
  if (success) {
    showDeleteProofDialog.value = false
    toast.add({ severity: 'success', summary: 'Justificante eliminado', life: 3000 })
    await fetchPayments()
  }
}
```

---

### Step 3: Add Delete Proof Button in the Justificante Column

- **File**: `frontend/src/components/admin/PaymentsAllList.vue`
- **Action**: In the `Justificante` column body template (lines 411–423), add a trash icon button next to the existing link, visible when `data.proofFileUrl` is set.

**Replace the current column body** (lines 411–423):
```html
<Column header="Justificante" style="width: 8rem">
  <template #body="{ data }">
    <div v-if="data.proofFileUrl" class="flex items-center gap-1">
      <a
        :href="data.proofFileUrl"
        target="_blank"
        rel="noopener noreferrer"
        class="text-blue-600 hover:underline"
        :aria-label="isImage(data.proofFileName) ? 'Ver imagen del justificante' : 'Ver PDF del justificante'"
      >
        <i :class="isImage(data.proofFileName) ? 'pi pi-image' : 'pi pi-file-pdf'" />
      </a>
      <Button
        icon="pi pi-trash"
        text
        rounded
        size="small"
        severity="danger"
        aria-label="Eliminar justificante"
        v-tooltip="'Eliminar justificante'"
        @click="openDeleteProofDialog(data)"
      />
    </div>
    <span v-else class="text-gray-400">&mdash;</span>
  </template>
</Column>
```

- **Note**: The column `style` is widened from `6rem` to `8rem` to accommodate both the link icon and the trash button.

---

### Step 4: Add Delete Proof Confirmation Dialog

- **File**: `frontend/src/components/admin/PaymentsAllList.vue`
- **Action**: Add the confirmation dialog in the template, after the existing delete manual payment dialog (after line 572, before `</div>`).

```html
<!-- Delete proof dialog -->
<Dialog
  v-model:visible="showDeleteProofDialog"
  header="Eliminar justificante"
  :modal="true"
  :style="{ width: '28rem' }"
>
  <p class="text-sm text-gray-600">
    ¿Seguro que quieres eliminar el justificante de este pago? Esta acción no se puede deshacer.
  </p>
  <template #footer>
    <Button label="Cancelar" severity="secondary" text @click="showDeleteProofDialog = false" />
    <Button
      label="Eliminar"
      severity="danger"
      icon="pi pi-trash"
      :loading="deletingProof"
      @click="handleDeleteProof"
    />
  </template>
</Dialog>
```

- **Note**: No `<Dialog>` import needed — it is already imported at the top of the component (line 11).

---

### Step 5: Update Technical Documentation

- **File**: `ai-specs/specs/api-spec.yml`
- **Action**: Verify the new endpoint `DELETE /api/admin/payments/{paymentId}/proof` is present (the backend plan adds it). If the backend plan was already applied, this file should already be updated; otherwise verify it is not a duplicate effort.
- **Conclusion**: No frontend-specific documentation changes needed. The `frontend-standards.mdc` patterns are not affected — this change follows established composable + dialog patterns.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add `adminRemoveProof` to `usePayments.ts`
3. Step 2 — Add state and handlers to `PaymentsAllList.vue`
4. Step 3 — Add delete proof button in the template
5. Step 4 — Add confirmation dialog in the template
6. Step 5 — Verify documentation

---

## Testing Checklist

- [ ] `npm run type-check` — zero TypeScript errors
- [ ] `npm run build` — clean build
- [ ] Manual smoke test (Admin JWT):
  - Payment with proof: trash button visible → click → dialog appears → confirm → list refreshes, proof icon gone
  - Payment with proof: cancel dialog → nothing changes
  - Payment without proof: trash button not visible
  - Payment in `Completed` state with proof: trash button visible and works (no ownership restriction)
- [ ] Existing delete manual payment flow still works (regression)
- [ ] Error path: if API returns 422 (no proof), `error` ref is set and toast is not shown

---

## Error Handling Patterns

- `adminRemoveProof` sets `error.value` on failure (same pattern as `deleteManualPayment`)
- `deletingProof` ref disables the confirm button and shows a spinner during the request
- On success: dialog closes, success toast, list refreshes
- On failure: dialog stays open, `error` message surfaced via the existing `<Message v-if="error">` in the template (line 473)

---

## UI/UX Considerations

- **Button placement**: Trash icon sits inline with the proof link icon inside the `Justificante` column — compact, consistent with the pencil/trash actions column pattern elsewhere in the table
- **Column width**: Increased from `6rem` to `8rem` to prevent wrapping
- **Visibility rule**: Button is shown whenever `data.proofFileUrl` is truthy — regardless of payment status — matching the spec requirement
- **Dialog**: Uses the same `PrimeVue Dialog` modal pattern as the existing delete manual payment dialog (lines 547–572); `header`, `:modal="true"`, `:style="{ width: '28rem' }"` match exactly
- **Loading state**: `deletingProof` ref passed to `:loading` on the confirm button, preventing double-submission
- **Accessibility**: `aria-label="Eliminar justificante"` on the button; `v-tooltip` provides a visual hint on hover
- **No `<style>` blocks**: All styling via Tailwind utility classes

---

## Dependencies

- No new npm packages required
- PrimeVue components used: `Button`, `Dialog` — both already imported in `PaymentsAllList.vue`

---

## Notes

- **Return type `boolean`**: `adminRemoveProof` returns `boolean` (not the updated payment) because the backend endpoint returns `204 No Content`. The list is refreshed by calling `fetchPayments()` after success.
- **No toast on error**: The existing `<Message>` block already renders `error.value`. Do not add a separate error toast — it would duplicate the feedback.
- **Language**: All user-facing strings in Spanish (confirmation text, toast summary, tooltips) per project standards.
- **TypeScript**: No `any` — the `api.delete()` call is untyped on the response side intentionally since `204` has no body; `adminRemoveProof` is typed as `Promise<boolean>`.

---

## Next Steps After Implementation

- Merge backend branch (`feature/fix-admin-remove-payment-proof-backend`) first; the frontend branch depends on the endpoint being deployed.
- Verify end-to-end flow in the staging environment with an Admin and Board user.

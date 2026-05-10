# Frontend Implementation Plan: fix-delete-family-member — Soft Delete + GDPR Anonymisation

## Overview

The backend now handles family member deletion with a two-tier strategy: hard delete for members with no history, soft delete (sets `deleted_at`) for members with any registration or membership. The soft-deleted members are invisible to all API queries automatically via a global EF query filter. A new Admin/Board-only endpoint anonymises PII in-place for GDPR right-to-erasure requests.

Frontend changes are minimal and focused:

- Remove stale 409 "active registrations" error handling from the delete flow (soft delete is now transparent).
- Add `deletedAt` to the `FamilyMemberResponse` type.
- Add a new composable method `anonymiseFamilyMemberPii` for the new endpoint.
- Expose a "Anonymise PII" action in `FamilyMemberList.vue` for Admin/Board users.
- Wire the new action in `FamilyUnitPage.vue` with a confirmation dialog.

Architecture: Vue 3 Composition API, `useFamilyUnits` composable, PrimeVue + Tailwind CSS, no new Pinia store needed.

---

## Architecture Context

**Components / composables involved:**

- `frontend/src/types/family-unit.ts` — `FamilyMemberResponse` interface
- `frontend/src/composables/useFamilyUnits.ts` — `deleteFamilyMember`, new `anonymiseFamilyMemberPii`
- `frontend/src/components/family-units/FamilyMemberList.vue` — new emit + button
- `frontend/src/views/FamilyUnitPage.vue` — new `handleAnonymiseMemberPii` handler

**New endpoint:** `DELETE /api/family-units/{familyUnitId}/members/{memberId}/pii`
**Roles:** Admin / Board only

**State management:** Local composable state; no Pinia store. The existing pattern (remove from `familyMembers` ref on success) applies to soft delete identically — the member disappears from the list regardless of whether the backend hard- or soft-deleted it.

**Routing:** No route changes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch.
- **Branch name**: `feature/fix-delete-family-member-frontend`
- **Base branch**: `dev`
- **Implementation Steps**:
  1. Ensure you are on `dev` and it is up to date: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/fix-delete-family-member-frontend`
  3. Verify: `git branch`
- **Notes**: Never implement on `main` or directly on the task branch without the `feature/…-frontend` suffix.

---

### Step 1: Update TypeScript Interface — `FamilyMemberResponse`

- **File**: `frontend/src/types/family-unit.ts`
- **Action**: Add the optional `deletedAt` field to `FamilyMemberResponse`.
- **Change**:

  ```typescript
  interface FamilyMemberResponse {
    // … existing fields …
    createdAt: string
    updatedAt: string
    deletedAt?: string | null   // ← add this
  }
  ```

- **Implementation Notes**: The field is optional because existing cached API responses won't include it and because non-deleted members will return `null`. Treat any truthy `deletedAt` as "soft-deleted" if needed in future. For now it is informational only — the backend excludes soft-deleted members from all list/get responses.

---

### Step 2: Update Composable — `useFamilyUnits.ts`

- **File**: `frontend/src/composables/useFamilyUnits.ts`
- **Action** A — Remove stale 409 "active registrations" error handling from `deleteFamilyMember`.
  - Search for any `catch` / `error.response?.status === 409` block inside `deleteFamilyMember` that showed a message about "active registrations". Remove that specific branch. The only remaining 409 meaning is "cannot delete representative", which should be kept (or shown generically from the error response body).
  - The success path is unchanged: on 204, filter out the deleted member from `familyMembers.value`.

- **Action** B — Add `anonymiseFamilyMemberPii` method:

  ```typescript
  const anonymiseFamilyMemberPii = async (
    familyUnitId: string,
    memberId: string
  ): Promise<boolean> => {
    try {
      loading.value = true
      error.value = null
      await api.delete(
        `/family-units/${familyUnitId}/members/${memberId}/pii`
      )
      // Remove member from local list (they are soft-deleted + anonymised)
      familyMembers.value = familyMembers.value.filter(m => m.id !== memberId)
      return true
    } catch (err) {
      error.value = getErrorMessage(err)
      return false
    } finally {
      loading.value = false
    }
  }
  ```

  - Return the new method from the composable's return object.
  - **Dependencies**: Uses the existing `api` Axios instance and `getErrorMessage` helper already present in the composable.

---

### Step 3: Update `FamilyMemberList.vue` — Add "Anonymise PII" emit and button

- **File**: `frontend/src/components/family-units/FamilyMemberList.vue`
- **Action**: Add a new Admin/Board-only action button and corresponding emit.

**Emit addition** (in `<script setup>`):

```typescript
const emit = defineEmits<{
  edit: [member: FamilyMemberResponse]
  delete: [member: FamilyMemberResponse]
  anonymisePii: [member: FamilyMemberResponse]   // ← new
  manageMembership: [member: FamilyMemberResponse]
  uploadPhoto: [memberId: string, file: File]
  removePhoto: [memberId: string]
}>()
```

**Button addition** (in the actions column, after the existing delete button, visible only when `isAdminOrBoard`):

```html
<Button
  v-if="isAdminOrBoard"
  icon="pi pi-eraser"
  severity="warning"
  text
  rounded
  v-tooltip.top="'Anonimizar datos personales (RGPD)'"
  @click="emit('anonymisePii', member)"
  aria-label="Anonimizar datos personales"
/>
```

- **Icon**: `pi pi-eraser` (PrimeVue icon set — conveys data erasure).
- **Severity**: `warning` (amber) to signal a destructive but not irreversible-looking action distinct from the red delete.
- **Visibility**: Only when `isAdminOrBoard` prop is `true`. Do not show to representatives.

---

### Step 4: Update `FamilyUnitPage.vue` — Wire new handler

- **File**: `frontend/src/views/FamilyUnitPage.vue`
- **Action**: Destructure `anonymiseFamilyMemberPii` from the composable and add a handler.

**Composable destructure** (add alongside existing methods):

```typescript
const {
  // … existing …
  anonymiseFamilyMemberPii,
} = useFamilyUnits()
```

**New handler** (add after `handleDeleteMember`):

```typescript
const handleAnonymiseMemberPii = (member: FamilyMemberResponse) => {
  confirm.require({
    header: 'Anonimizar datos personales',
    message: `¿Anonimizar los datos personales de "${member.firstName} ${member.lastName}"? Esta acción elimina todos los datos identificativos del miembro de forma permanente (RGPD). El registro histórico se conserva sin datos personales.`,
    acceptLabel: 'Anonimizar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-warning',
    icon: 'pi pi-exclamation-triangle',
    accept: async () => {
      const success = await anonymiseFamilyMemberPii(
        familyUnit.value!.id,
        member.id
      )
      if (success) {
        toast.add({
          severity: 'success',
          summary: 'Datos anonimizados',
          detail: `Los datos personales del miembro han sido eliminados correctamente.`,
          life: 4000,
        })
      } else {
        toast.add({
          severity: 'error',
          summary: 'Error al anonimizar',
          detail: error.value ?? 'No se pudieron anonimizar los datos del miembro.',
          life: 5000,
        })
      }
    },
  })
}
```

**Template** — add the new emit listener to `<FamilyMemberList>`:

```html
<FamilyMemberList
  :members="familyMembers"
  :read-only="isViewingOther && !(auth.isAdmin || auth.isBoard)"
  :is-admin-or-board="auth.isAdmin || auth.isBoard"
  :representative-user-id="familyUnit?.representativeUserId"
  @edit="openEditMemberDialog"
  @delete="handleDeleteMember"
  @anonymise-pii="handleAnonymiseMemberPii"
  @manage-membership="handleManageMembership"
  @upload-photo="onUploadMemberPhoto"
  @remove-photo="onRemoveMemberPhoto"
/>
```

---

### Step 5: Write Vitest Unit Tests

- **File**: `frontend/src/composables/__tests__/useFamilyUnits.test.ts` (or wherever composable tests live; check for existing test file)
- **Action**: Add tests for the updated `deleteFamilyMember` and new `anonymiseFamilyMemberPii`.

**Test cases**:

```typescript
describe('deleteFamilyMember', () => {
  it('removes member from familyMembers on 204 success', async () => { … })
  it('does NOT show active-registrations error on 409 representative conflict', async () => {
    // 409 should show backend error message, not a hardcoded "active registrations" string
  })
  it('sets error ref on non-204 response', async () => { … })
})

describe('anonymiseFamilyMemberPii', () => {
  it('calls DELETE /family-units/:id/members/:id/pii', async () => { … })
  it('removes member from familyMembers on success', async () => { … })
  it('returns true on success', async () => { … })
  it('returns false and sets error on API failure', async () => { … })
})
```

- **Dependencies**: `vi.mock` for axios/api; use `vi.fn()` mocks returning resolved/rejected promises.

---

### Step 6: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/family-units/delete-family-member.cy.ts`
- **Action**: Add E2E tests for Admin anonymise flow.

**Test cases**:

```typescript
describe('Family member deletion', () => {
  it('deletes a member with no history (hard delete) — member disappears from list', () => {
    // Intercept DELETE .../members/:id → 204
    // Confirm dialog → member row gone from table
  })

  it('soft-deletes a member with history — member disappears from list', () => {
    // Same flow; backend decides hard/soft; UI behaviour identical
  })
})

describe('Admin: Anonymise PII', () => {
  it('shows Anonymise PII button only to Admin/Board', () => { … })
  it('opens confirmation dialog on click', () => { … })
  it('calls DELETE .../pii and removes member from table', () => {
    // Intercept DELETE .../members/:id/pii → 204
    // Confirm dialog → member row gone; success toast shown
  })
  it('shows error toast on API failure', () => {
    // Intercept → 403 → error toast shown
  })
})
```

---

### Step 7: Update Technical Documentation

- **Action**: Update `ai-specs/specs/api-endpoints.md` to document the new PII endpoint.
- **Implementation Steps**:
  1. Add entry for `DELETE /api/family-units/{familyUnitId}/members/{memberId}/pii`:
     - Description: GDPR right-to-erasure — anonymises all PII fields of a family member
     - Authorization: Admin/Board only
     - Response: 204 No Content
     - Errors: 403 Forbidden, 404 Not Found
  2. Update the entry for `DELETE /api/family-units/{familyUnitId}/members/{memberId}` to note that it now soft-deletes transparently when the member has any registration or membership history; the 409 for active registrations is removed (only representative-conflict 409 remains).
- **References**: `ai-specs/specs/documentation-standards.mdc` — all documentation in English.

---

## Implementation Order

1. Step 0 — Create feature branch `feature/fix-delete-family-member-frontend` off `dev`
2. Step 1 — Add `deletedAt?` to `FamilyMemberResponse`
3. Step 2 — Update `useFamilyUnits.ts` (remove stale 409 branch; add `anonymiseFamilyMemberPii`)
4. Step 3 — Add emit + button to `FamilyMemberList.vue`
5. Step 4 — Wire handler in `FamilyUnitPage.vue`
6. Step 5 — Vitest unit tests for composable
7. Step 6 — Cypress E2E tests
8. Step 7 — Update API documentation

---

## Testing Checklist

- [ ] `deleteFamilyMember` — 204 removes member from local list
- [ ] `deleteFamilyMember` — 409 shows generic backend error message, NOT a hardcoded "active registrations" string
- [ ] `anonymiseFamilyMemberPii` — calls correct URL, removes member on success, returns `true`
- [ ] `anonymiseFamilyMemberPii` — returns `false` and sets `error` on API failure
- [ ] `FamilyMemberList` — "Anonymise PII" button visible only when `isAdminOrBoard = true`
- [ ] `FamilyMemberList` — button disabled for the representative member (should not allow anonymising the live account)
- [ ] `FamilyUnitPage` — confirmation dialog shows before API call
- [ ] `FamilyUnitPage` — success toast shown after anonymisation
- [ ] `FamilyUnitPage` — error toast shown on failure
- [ ] Cypress — representative cannot see Anonymise button
- [ ] Cypress — full Admin flow: click → confirm → member gone → toast

---

## Error Handling Patterns

- **`deleteFamilyMember`**: On any error, set `error.value` from response body. If 409, show the backend's `detail` message (e.g. "Cannot delete the family representative"). Remove any hardcoded "active registrations" error message — that case now resolves as 204.
- **`anonymiseFamilyMemberPii`**: On 403 show "No tienes permisos para realizar esta acción." On 404 show "El miembro no existe." On other errors, show backend detail or fallback.
- All errors surface via PrimeVue `Toast` (`severity: 'error'`) in `FamilyUnitPage.vue` as per existing pattern.

---

## UI/UX Considerations

- **Anonymise button icon**: `pi pi-eraser` — semantically correct, distinct from `pi pi-trash` (delete).
- **Severity**: `warning` (amber) — signals significant, irreversible but non-catastrophic action. Red (`danger`) is reserved for delete.
- **Confirmation dialog message**: Must clearly communicate that personal data is permanently removed and cannot be recovered, while audit history is preserved.
- **Language**: All user-facing strings in Spanish; code, comments, and documentation in English.
- **Tooltip**: `v-tooltip.top` for accessibility.
- **Responsive**: The actions column in `FamilyMemberList.vue` may become crowded on mobile. If there are already ≥3 action icons, consider an overflow menu (`OverlayPanel`) for the Anonymise action — evaluate at implementation time.

---

## Dependencies

- No new npm packages required.
- PrimeVue components used: `Button`, `ConfirmDialog` (already in use), `Toast` (already in use).
- PrimeVue icons: `pi pi-eraser` (already available in PrimeIcons).

---

## Notes

- **Soft delete is transparent to the frontend**: After a successful `deleteFamilyMember` call (204), the member is removed from `familyMembers.value`. Whether the backend hard- or soft-deleted is irrelevant — the member no longer appears in API responses.
- **Anonymise removes the member from the visible list**: The PII endpoint also sets `deleted_at` on the backend, so the anonymised member will not appear in subsequent fetches. Removing from local state on success is correct.
- **Do not add a "restore member" UI** — soft delete is an internal implementation detail with no reversal UI in this ticket.
- **Representative cannot be anonymised via this flow**: The representative's user account must remain intact. The button should be disabled for the member whose `userId` matches `familyUnit.representativeUserId` — same logic as the delete button.
- **TypeScript strict**: No `any`. All API responses typed. Composable return type inferred from implementation.

---

## Next Steps After Implementation

- Open PR `feature/fix-delete-family-member-frontend` → `dev`.
- QA: verify with Admin and Representative roles in staging.
- Coordinate with backend PR #239 deployment — both must be live before QA.

---

## Implementation Verification

| Check | Criterion |
|---|---|
| TypeScript strict | No `any`, `<script setup lang="ts">` in all modified components |
| No stale error handling | No hardcoded "active registrations" string anywhere in delete flow |
| Composable | `anonymiseFamilyMemberPii` exported and callable |
| Component | Anonymise button shown only for `isAdminOrBoard`, disabled for representative |
| Handler | Confirmation dialog → API call → toast feedback |
| Vitest | `deleteFamilyMember` and `anonymiseFamilyMemberPii` covered |
| Cypress | Admin anonymise flow covered end-to-end |
| Docs | `api-endpoints.md` updated for new endpoint and modified delete semantics |

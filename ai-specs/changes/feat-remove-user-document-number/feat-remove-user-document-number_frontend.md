# Frontend Implementation Plan: feat-remove-user-document-number — Remove DocumentNumber from User Registration

## Overview

Remove the `documentNumber` field from the frontend registration type definition to align with the backend change that dropped `User.DocumentNumber`. The scope is a single-line deletion from a TypeScript interface. The `RegisterForm.vue` component, the `useAuthStore.register()` method, and all tests are already clean — they never sent `documentNumber` to the API. This task is purely a type-level cleanup.

Architecture principle: types in `frontend/src/types/` must mirror backend DTOs. The backend `RegisterUserRequest` record no longer has `DocumentNumber`, so the frontend `RegisterUserRequest` interface must match.

---

## Architecture Context

**Files affected:**
- `frontend/src/types/auth.ts` — remove `documentNumber?: string | null` from `RegisterUserRequest`

**Files already clean (no changes needed):**
- `frontend/src/components/auth/RegisterForm.vue` — form has no `documentNumber` field and does not send it
- `frontend/src/stores/auth.ts` — `register()` signature already omits `documentNumber`
- `frontend/src/components/auth/__tests__/RegisterForm.test.ts` — no `documentNumber` references
- `frontend/cypress/` — no E2E tests reference `documentNumber` in the auth flow

**Untouched (FamilyMember scope — spec explicitly excludes):**
- `frontend/src/components/family-units/FamilyMemberForm.vue` — `documentNumber` here is for `FamilyMember`, not `User`

**State management:** No Pinia store change needed. The auth store's `register()` function does not include `documentNumber`.

**Routing:** No routing changes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new frontend feature branch
- **Branch Naming**: `feature/feat-remove-user-document-number-frontend`
- **Implementation Steps**:
  1. Ensure you are on `dev` and it is up to date: `git checkout dev && git pull origin dev`
  2. Create branch: `git checkout -b feature/feat-remove-user-document-number-frontend`
  3. Verify: `git branch`
- **Notes**: Do not work directly on `dev`. PR must target `dev`.

---

### Step 1: Remove `documentNumber` from the `RegisterUserRequest` TypeScript Interface

- **File**: `frontend/src/types/auth.ts`
- **Action**: Delete the `documentNumber?: string | null` line from the `RegisterUserRequest` interface.
- **Before** (lines 21–29):
  ```typescript
  export interface RegisterUserRequest {
    email: string
    password: string
    firstName: string
    lastName: string
    documentNumber?: string | null
    phone?: string | null
    acceptedTerms: boolean
  }
  ```
- **After**:
  ```typescript
  export interface RegisterUserRequest {
    email: string
    password: string
    firstName: string
    lastName: string
    phone?: string | null
    acceptedTerms: boolean
  }
  ```
- **Implementation Notes**:
  - `RegisterUserRequest` is exported but the only consumer of `documentNumber` on that interface would have been code that was already removed. TypeScript will flag any remaining usage at compile time.
  - The `RegisterForm.vue` `handleSubmit` already doesn't include `documentNumber` in the object it passes to `auth.register()`.
  - The `useAuthStore.register()` method signature already has no `documentNumber` parameter. No store change needed.

---

### Step 2: Verify TypeScript Build Passes

- **Action**: Run the TypeScript compiler to confirm zero errors.
- **Command** (from the `frontend/` directory):
  ```bash
  npm run type-check
  ```
  or equivalently:
  ```bash
  npx vue-tsc --noEmit
  ```
- **Implementation Notes**: `TreatWarningsAsErrors` is on. Any type error will surface here. If `documentNumber` was referenced anywhere that was missed, the compiler will point to it directly.

---

### Step 3: Run Existing Tests

- **Action**: Run the Vitest unit/component test suite to confirm no regressions.
- **Command** (from `frontend/`):
  ```bash
  npm run test
  ```
- **Implementation Notes**:
  - `RegisterForm.test.ts` has 12 tests; all should pass without any modification.
  - No test in the frontend references `documentNumber` on the `RegisterUserRequest` interface or in auth flows.

---

### Step 4: Update Technical Documentation

- **Action**: Verify that no documentation references `documentNumber` as part of the `User`/`RegisterUserRequest` interface.
- **Files to check**:
  - `ai-specs/specs/api-spec.yml` — if the `register-user` request body schema lists `documentNumber`, remove it.
  - `ai-specs/specs/frontend-standards.mdc` — no change expected (no type patterns documented there for auth).
- **Implementation Steps**:
  1. Search `ai-specs/specs/api-spec.yml` for `documentNumber` in the `register-user` endpoint schema. Remove the field if present.
  2. Confirm no other spec document references `documentNumber` in the context of `RegisterUserRequest`.
- **Notes**: All documentation must be in English. This step is **mandatory** before closing the ticket.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-remove-user-document-number-frontend`
2. **Step 1** — Remove `documentNumber` from `RegisterUserRequest` in `frontend/src/types/auth.ts`
3. **Step 2** — Run `npm run type-check` — zero errors required
4. **Step 3** — Run `npm run test` — all tests green
5. **Step 4** — Check and update `ai-specs/specs/api-spec.yml` if needed

---

## Testing Checklist

- [ ] `frontend/src/components/auth/__tests__/RegisterForm.test.ts` — all 12 tests pass
- [ ] `npm run type-check` — zero TypeScript errors
- [ ] `npm run test` — zero test failures
- [ ] No TypeScript `any` introduced
- [ ] No reference to `documentNumber` remains in `RegisterUserRequest` or auth-related files (grep confirms)

---

## Error Handling Patterns

No new error handling is introduced. The `DOCUMENT_EXISTS` error code was already never handled in `RegisterForm.vue` or the auth store — error messages are displayed generically using `result.error`. After the backend change, this code is never returned, so there is nothing to remove on the error-handling side.

---

## UI/UX Considerations

No UI change. The registration form already has no `documentNumber` input field.

---

## Dependencies

No new npm packages required.

---

## Notes

- **`FamilyMemberForm.vue` is untouched.** Its `documentNumber` field is for `FamilyMember`, which is explicitly excluded from this change.
- **Non-breaking.** The `documentNumber` field on `RegisterUserRequest` was optional (`?`). Removing it from the type does not break any existing call site — the field was never populated in practice.
- **Branch target**: PR must target `dev`, not `main`.
- **Coordinate with backend PR #235**: This frontend PR should be reviewed alongside or after the backend PR to ensure both land together.

---

## Next Steps After Implementation

- Merge backend PR #235 first (drops the column and removes the API field).
- Merge this frontend PR after or in the same release window.
- No migration or deployment step specific to the frontend.

---

## Implementation Verification

- [ ] **Code Quality**: No TypeScript `any`, strict mode respected, `<script setup lang="ts">` in all components (no change needed here)
- [ ] **Functionality**: Registration form submits correctly without `documentNumber`; no runtime errors
- [ ] **Testing**: All Vitest tests pass; `RegisterForm.test.ts` all green
- [ ] **Type Safety**: `npm run type-check` exits with code 0
- [ ] **Documentation**: `api-spec.yml` checked and updated if `documentNumber` was present in the `register-user` request schema

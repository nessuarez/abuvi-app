# User Story: Mi Perfil — Enriched Specification

**Task ID:** `feat-my-profile-layout`
**Status:** Ready for Implementation
**Priority:** Medium
**Affected Area:** Frontend only (all backend endpoints already exist)

---

## Summary

Redesign the `ProfilePage.vue` (`/profile`) from a minimal placeholder into a comprehensive profile hub that consolidates: user personal data, edit-profile capability, family unit overview, family member list with membership status, and annual fee (cuota) status per member.

---

## Problem Statement

The current `ProfilePage.vue` shows only name, email, role, and a link to the family unit page. The user must navigate to `/family-unit/me` to see family data and has no way to edit their profile. The goal is a single page that surfaces all key personal information and membership status without excessive navigation.

**Out of scope for this iteration (no backend model):**

- Bank/payment account information
- Profile photo upload
- Email change (requires re-verification flow)
- Password change (use the existing forgot-password flow)
- Document number editing (not in `UpdateUserRequest`)

---

## Current State

**File:** `frontend/src/views/ProfilePage.vue`
**Route:** `/profile` (requires auth)
**What exists:**

- Reads `auth.user` (type `UserInfo` — no `phone` field) from Pinia store
- Fetches family unit via `useFamilyUnits().getCurrentUserFamilyUnit()`
- Shows a static card with name/email/role + a family unit status card with a "Gestionar" button to `/family-unit/me`
- Contains placeholder text: _"La gestión completa del perfil se implementará en futuras iteraciones."_

---

## Available Infrastructure (No Backend Changes Needed)

### API Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/users/{id}` | Load full user (includes `phone`) |
| `PUT` | `/api/users/{id}` | Update `firstName`, `lastName`, `phone`, `isActive` |
| `GET` | `/api/family-units/me` | Get current user's family unit |
| `GET` | `/api/family-units/{id}/members` | List family members |
| `GET` | `/api/family-units/{fuid}/members/{mid}/membership` | Member's membership |
| `GET` | `/api/memberships/{membershipId}/fees/current` | Current year fee |
| `POST` | `/api/memberships/{membershipId}/fees/{feeId}/pay` | Mark fee as paid (Board/Admin only) |

### Existing Composables

- `useFamilyUnits()` — `getCurrentUserFamilyUnit()`, `getFamilyMembers(familyUnitId)`
- `useMemberships()` — `getMembership(familyUnitId, memberId)`, `getCurrentFee(membershipId)`, `payFee(...)`
- `useUsers()` — `fetchUserById(id)`, `updateUser(id, request)`

### Existing Types

- `User` interface in `types/user.ts` includes `phone`
- `UserInfo` in `types/auth.ts` does **not** include `phone` — important gap
- `FamilyMemberResponse`, `MembershipResponse`, `MembershipFeeResponse` in `types/family-unit.ts`, `types/membership.ts`

---

## Functional Requirements

### Section 1 — Personal Information Card

**Display (read mode):**

- Full name (firstName + lastName)
- Email address (read-only — not editable)
- Phone number (from full User fetch; show "—" if not set)
- Role (Spanish label via `getRoleLabel()` from `utils/user.ts`)
- Member since (formatted `createdAt` date)

**Edit mode (inline or dialog):**

- Trigger: "Editar perfil" button (visible to self only)
- Editable fields: `Nombre` (firstName), `Apellidos` (lastName), `Teléfono` (phone)
- Non-editable in this form: email, role, document number
- Validation (client-side):
  - `firstName`: required, max 100 chars
  - `lastName`: required, max 100 chars
  - `phone`: optional; if provided, must match E.164 pattern (`+34612345678`)
- On submit: `PUT /api/users/{auth.user.id}` with `{ firstName, lastName, phone, isActive: true }`
- On success:
  - Show success toast: _"Perfil actualizado correctamente"_
  - **Update the Pinia auth store** `user.firstName` and `user.lastName` so the header reflects the change immediately
  - Exit edit mode
- On error: show error toast with backend message or fallback _"Error al actualizar el perfil"_

### Section 2 — Family Unit Summary Card

**If no family unit:**

- Message: _"Aún no has creado tu unidad familiar."_
- Button: "Crear Unidad Familiar" → navigates to `/family-unit/me`

**If family unit exists:**

- Family unit name (e.g., _"Familia García"_)
- Number of members
- Button: "Gestionar" → navigates to `/family-unit/me`
- Below: embedded family members list (see Section 3)

### Section 3 — Family Members & Membership Status

Displayed within the Family Unit card when a family unit exists.

**Per member row:**

- Full name
- Relationship (Spanish label)
- Age (calculated from `dateOfBirth`)
- Membership badge: _"Socio activo"_ (success) / _"Sin membresía"_ (warn) / _"Membresía inactiva"_ (danger)
- Current year fee status tag (using `FeeStatusSeverity` from `types/membership.ts`):
  - _"Pagada"_ (success) — shows payment date
  - _"Pendiente"_ (warn) — shows amount
  - _"Vencida"_ (danger) — shows amount
  - _"Sin cuota"_ (secondary) — if no active membership or no fee record
- For Board/Admin users: "Pagar cuota" button (opens existing `PayFeeDialog`)

**Data loading strategy** (to avoid N+1 UX):

- Fetch all family members in one call
- For each member, fetch their membership (with fees) in parallel using `Promise.all`
- Show a skeleton loader per member row while loading

### Section 4 — Account Security Card (read-only)

- Email: shown with "no editable" note
- Password: only show "Cambiar contraseña" link → navigates to `/forgot-password`
- No inline password change form

---

## Page Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Mi Perfil                                                    │
├─────────────────────────────────────────────────────────────┤
│  [👤] Nombre Apellidos                     [Editar perfil]  │
│       correo@email.com                                       │
│       📱 +34612345678  |  Rol: Socio                        │
│       Miembro desde: 01/01/2024                             │
├─────────────────────────────────────────────────────────────┤
│  👥 Mi Unidad Familiar: "Familia García"   [Gestionar →]    │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ María García   Padre/Madre  42 años                    │ │
│  │ ● Socio activo  |  Cuota 2026: Pagada ✓  10/01/2026   │ │
│  ├────────────────────────────────────────────────────────┤ │
│  │ Lucas García   Hijo/Hija    14 años                    │ │
│  │ ● Socio activo  |  Cuota 2026: Pendiente (€30)        │ │
│  └────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│  🔒 Seguridad                                                │
│       correo@email.com (no editable)                         │
│       [Cambiar contraseña]                                   │
└─────────────────────────────────────────────────────────────┘
```

---

## Files to Create / Modify

### Files to Create

| File | Purpose |
|------|---------|
| `frontend/src/composables/useProfile.ts` | Encapsulate "load full user" + "update profile" logic; update auth store on success |
| `frontend/src/composables/__tests__/useProfile.test.ts` | Vitest unit tests for the new composable |

### Files to Modify

| File | Changes |
|------|---------|
| `frontend/src/views/ProfilePage.vue` | Full redesign — 4 sections, edit mode, membership view |
| `frontend/src/types/auth.ts` | Add `phone?: string \| null` to `UserInfo` so auth store can reflect it |
| `frontend/src/stores/auth.ts` | Add `updateProfile(data: Partial<UserInfo>)` action to allow updating user info in store after edit |

---

## `useProfile` Composable Specification

```typescript
// frontend/src/composables/useProfile.ts

export interface UpdateProfileRequest {
  firstName: string
  lastName: string
  phone: string | null
}

export function useProfile() {
  const fullUser = ref<User | null>(null)      // Full User type (includes phone)
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Load the authenticated user's full profile (to get phone)
  const loadProfile = async (): Promise<void> => { ... }

  // Update user profile and sync auth store
  const updateProfile = async (request: UpdateProfileRequest): Promise<boolean> => { ... }

  return { fullUser, loading, error, loadProfile, updateProfile }
}
```

**Key behavior:**

- `loadProfile()`: calls `GET /api/users/{auth.user.id}`, stores result in `fullUser`
- `updateProfile()`: calls `PUT /api/users/{auth.user.id}` with `{ ...request, isActive: true }`, on success calls `auth.updateProfile({ firstName, lastName, phone })` to sync the Pinia store

---

## Auth Store Changes

Add to `frontend/src/stores/auth.ts`:

```typescript
// New action (no API call — pure store update)
function updateProfile(data: { firstName?: string; lastName?: string; phone?: string | null }) {
  if (!user.value) return
  user.value = { ...user.value, ...data }
  // Persist to localStorage if session was saved
  if (localStorage.getItem(USER_KEY)) {
    localStorage.setItem(USER_KEY, JSON.stringify(user.value))
  }
}
```

And update the return object to expose it.

---

## Validation Rules

### Client-side (ProfilePage.vue)

| Field | Rule | Error Message |
|-------|------|---------------|
| `firstName` | Required, max 100 chars | _"El nombre es obligatorio"_ |
| `lastName` | Required, max 100 chars | _"Los apellidos son obligatorios"_ |
| `phone` | Optional; if provided: E.164 format | _"El teléfono debe estar en formato internacional (ej. +34612345678)"_ |

---

## Test Cases

### `useProfile.test.ts` (Vitest)

1. `should load full user profile on loadProfile call`
2. `should set error when loadProfile fails`
3. `should update profile successfully and sync auth store`
4. `should set error when updateProfile API call fails`
5. `should set isActive: true when submitting update`
6. `should clear error before each API call`

### `ProfilePage.vue` Component Tests

1. `should render user name, email, and role in read mode`
2. `should show phone from full user fetch`
3. `should enter edit mode when "Editar perfil" is clicked`
4. `should disable save button while submitting`
5. `should show success toast after successful update`
6. `should show error toast when update fails`
7. `should exit edit mode after successful update`
8. `should show family unit name and member count`
9. `should show loading state while fetching membership data`
10. `should show membership status badge per family member`
11. `should show current year fee status per member`
12. `should show "Crear Unidad Familiar" when no family unit exists`
13. `should navigate to /family-unit/me when Gestionar is clicked`
14. `should navigate to /forgot-password when Cambiar contraseña is clicked`

---

## Acceptance Criteria

- [ ] User can view their full profile (name, email, phone, role, member since date) at `/profile`
- [ ] User can edit their firstName, lastName, and phone via an inline or dialog form
- [ ] After edit, the header/nav reflects the updated name (auth store updated)
- [ ] Phone validates against E.164 format when provided
- [ ] User can see their family unit name and list of members with age and relationship
- [ ] Each family member shows their membership status (active/inactive/none)
- [ ] Each family member shows their current year fee status with amount and payment date (if paid)
- [ ] Board/Admin users see a "Pagar cuota" button per member
- [ ] "Gestionar" button on the family unit card navigates to `/family-unit/me`
- [ ] "Cambiar contraseña" link navigates to `/forgot-password`
- [ ] Page handles loading states (skeleton/spinner) during API calls
- [ ] Page handles error states with user-facing messages in Spanish
- [ ] All composable unit tests pass (Vitest)
- [ ] All component tests pass (Vitest + Vue Test Utils)
- [ ] TypeScript compiles without errors (`vue-tsc`)
- [ ] ESLint passes without warnings

---

## Implementation Notes

### Phone Field Gap

`UserInfo` (auth store type) does not include `phone`. The workaround is:

1. Load the full `User` object via `GET /api/users/{id}` in `useProfile.ts`
2. Store it separately in the composable (`fullUser`)
3. Add `phone` to `UserInfo` so the auth store can hold it after edit

This avoids requiring a backend change to the JWT token contents.

### Edit Mode UX Options

Two acceptable patterns:

- **Option A**: Inline card switch between read/edit mode (simpler, no dialog)
- **Option B**: PrimeVue `Dialog` containing the edit form

Recommendation: **Option A** (inline) to match the style of the existing family unit card.

### Membership Data Loading

Loading memberships for all family members requires N API calls. Use `Promise.all()` to load them in parallel:

```typescript
const members = await getFamilyMembers(familyUnit.value.id)
const membershipData = await Promise.all(
  members.map(async (m) => {
    const membership = await getMembership(familyUnit.value.id, m.id)
    const fee = membership ? await getCurrentFee(membership.id) : null
    return { member: m, membership, fee }
  })
)
```

### Do NOT Re-implement Family CRUD

The family unit management CRUD (create/edit/delete members) stays on `FamilyUnitPage.vue` at `/family-unit/me`. The profile page only shows a **read-only summary** with a "Gestionar" navigation button. Do not duplicate the forms.

---

## Non-Functional Requirements

- **Performance**: Parallel API calls for membership data; skeleton loading to avoid layout shift
- **Responsiveness**: Mobile-first layout; single column on mobile, two columns on `md:` and above
- **Accessibility**: Semantic HTML, aria labels on edit buttons and form fields
- **Security**: The edit endpoint uses the authenticated user's own ID only — no privilege escalation possible. `isActive` is always sent as `true` (cannot self-deactivate).
- **Test coverage**: 90%+ on new composable and component

---

## Out of Scope

| Feature | Reason |
|---------|--------|
| Bank/payment information | No data model exists; future enhancement |
| Profile photo upload | No blob storage integration yet |
| Email change | Requires re-verification flow; separate feature |
| Document number editing | Not in `UpdateUserRequest` |
| Inline password change | Use forgot-password flow; separate feature |
| Family CRUD from profile page | Stays on `/family-unit/me` |

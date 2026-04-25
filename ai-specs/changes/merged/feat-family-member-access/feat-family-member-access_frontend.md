# Frontend Implementation Plan: feat-family-member-access — Family Member Access to Family Unit and Registrations

## Overview

A non-representative family member (a user whose `userId` is linked to a `FamilyMember` record) should be able to view the family unit page and camp registrations in read-only mode. The backend fix (separate ticket) makes this data available via the API. This frontend task ensures the UI correctly reflects read-only access for those users — specifically fixing one computed property in `FamilyUnitPage.vue` that currently shows edit controls when it should not.

No new composables, stores, or types are needed. The registrations views (`RegistrationsPage.vue`, `RegistrationDetailPage.vue`) already work correctly for read-only users once the API returns data — `canEdit` and `canCancel` are already gated behind `isRepresentative` in the detail page template.

---

## Architecture Context

**Components/composables involved:**
- `frontend/src/views/FamilyUnitPage.vue` — only file requiring code change
- `frontend/src/views/__tests__/FamilyUnitPage.spec.ts` — add new test cases

**Files NOT requiring changes (verified):**
- `frontend/src/views/registrations/RegistrationsPage.vue` — calls `fetchMyRegistrations()` → API response drives rendering, no authorization logic in template
- `frontend/src/views/registrations/RegistrationDetailPage.vue` — `canEdit` checks `isRepresentative.value`; `canCancel` used in template only as `isRepresentative && canCancel`; `canDelete` checks `isRepresentative || isAdminOrBoard`. All correctly gate mutating actions.
- `frontend/src/composables/useRegistrations.ts` — no changes needed
- `frontend/src/composables/useFamilyUnits.ts` — no changes needed
- `frontend/src/types/family-unit.ts` — no changes needed

**Routing:** No changes — existing routes work correctly for both representatives and linked members.

**State management:** No Pinia changes needed — `useAuthStore` is already used in `FamilyUnitPage.vue` to compare `auth.user?.id` with `representativeUserId`.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Base branch**: `dev` (after merging the backend branch `feature/feat-family-member-access-backend`)
- **Branch name**: `feature/feat-family-member-access-frontend`

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-family-member-access-frontend
```

> If the backend branch has not been merged yet, branch off of it for local testing, but target `dev` for the PR.

---

### Step 1: Fix `isViewingOther` in `FamilyUnitPage.vue`

**File**: `frontend/src/views/FamilyUnitPage.vue`

**Lines**: 53–56

**Problem**: The current computed:
```ts
const isViewingOther = computed(() =>
  !!route.params.id && familyUnit.value?.representativeUserId !== auth.user?.id
)
```
…evaluates to `false` when `route.params.id` is absent (the `/mi-familia` route), causing edit buttons to be shown to non-representative family members who access their own family page.

**Fix** — replace lines 53–56 with:
```ts
// Read-only mode for non-representative members (and admins viewing via /admin)
const isViewingOther = computed(() =>
  familyUnit.value !== null && familyUnit.value.representativeUserId !== auth.user?.id
)
```

**What `isViewingOther` gates in the template (verified):**
- `v-if="isViewingOther"` — "Back to Administration" button (correct, non-reps don't need it)
- Page title: `Mi Unidad Familiar` vs `Unidad Familiar` (correct labelling)
- `v-if="!isViewingOther"` — Edit/Delete family unit buttons
- `v-if="!isViewingOther"` — "Añadir Miembro" button
- `:editable="!isViewingOther"` — ProfilePhotoAvatar editable prop
- `:read-only="isViewingOther"` — FamilyMemberList read-only prop

All of these are correct. No additional template changes are needed.

---

### Step 2: Update Vitest Tests for `FamilyUnitPage`

**File**: `frontend/src/views/__tests__/FamilyUnitPage.spec.ts`

Add a new `describe` block for the `isViewingOther` behavior. The existing mock setup uses `representativeUserId: 'u1'` and `auth.user.id: 'u1'`, making the current user the representative.

**2a. Add mock for `useFamilyUnits` with `uploadMemberProfilePhoto`, `removeMemberProfilePhoto`, `uploadUnitProfilePhoto`, `removeUnitProfilePhoto`** — the existing mock is missing these. Check if existing tests fail without them; add stubs if needed:

```ts
uploadMemberProfilePhoto: vi.fn(),
removeMemberProfilePhoto: vi.fn(),
uploadUnitProfilePhoto: vi.fn(),
removeUnitProfilePhoto: vi.fn(),
getFamilyUnitById: vi.fn().mockResolvedValue(null),
```

**2b. Add new describe block:**

```ts
describe('FamilyUnitPage — isViewingOther', () => {
  it('shows edit controls when current user is the representative and no route id', async () => {
    // representativeUserId: 'u1', auth.user.id: 'u1' → isViewingOther = false
    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    // Edit button for family unit should be visible
    // (isViewingOther === false → v-if="!isViewingOther" passes)
    // Check the add-member button is rendered
    const buttons = wrapper.findAllComponents({ name: 'Button' })
    // At minimum, no "Volver a Administración" button (isViewingOther is false)
    const backButton = buttons.find(b => b.props('label') === 'Volver a Administración')
    expect(backButton).toBeUndefined()
  })

  it('hides edit controls when current user is NOT the representative (linked member)', async () => {
    // Override: representative is someone else
    const familyUnitsMock = {
      ...defaultFamilyUnitsMock,
      familyUnit: {
        value: {
          id: 'unit-1',
          name: 'Test Family',
          representativeUserId: 'other-user', // ← different from auth.user.id ('u1')
          profilePhotoUrl: null,
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        }
      }
    }
    vi.mocked(useFamilyUnits).mockReturnValueOnce(familyUnitsMock as any)

    const wrapper = mount(FamilyUnitPage, {
      global: { plugins: [createPinia()], stubs: componentStubs },
    })
    await nextTick()
    // isViewingOther === true → "Volver a Administración" button should appear
    const buttons = wrapper.findAllComponents({ name: 'Button' })
    const backButton = buttons.find(b => b.props('label') === 'Volver a Administración')
    expect(backButton).toBeDefined()
  })

  it('hides edit controls when accessed via /mi-familia and user is NOT representative', async () => {
    // This is the key regression test: no route.params.id, but user is NOT representative
    // Previous bug: isViewingOther was false in this case → edit buttons shown incorrectly
    const familyUnitsMock = {
      ...defaultFamilyUnitsMock,
      familyUnit: {
        value: {
          id: 'unit-1',
          name: 'Test Family',
          representativeUserId: 'representative-user', // ← not 'u1'
          profilePhotoUrl: null,
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        }
      }
    }
    vi.mocked(useFamilyUnits).mockReturnValueOnce(familyUnitsMock as any)

    // Ensure route has NO id param (simulates /mi-familia route)
    const wrapper = mount(FamilyUnitPage, {
      global: {
        plugins: [createPinia()],
        stubs: componentStubs,
        // vue-router not mounted → route.params.id is undefined (default)
      },
    })
    await nextTick()
    // FamilyMemberList should receive read-only=true
    const list = wrapper.findComponent({ name: 'FamilyMemberList' })
    expect(list.props('readOnly')).toBe(true)
  })
})
```

> **Implementation note**: The existing `vi.mock('@/composables/useFamilyUnits', ...)` uses a factory that always returns the same object. To override per-test, refactor to use `vi.mocked(useFamilyUnits).mockReturnValueOnce(...)` — this requires changing the mock from a static factory to `vi.fn()`. Alternatively, keep using a mutable `familyUnitMock` object (same pattern as `authMock`) that tests can mutate via `familyUnitMock.familyUnit.value.representativeUserId = 'other'` before mounting.
>
> Use the simpler mutable-object approach consistent with how `authMock` is done in the existing tests:

```ts
const familyUnitMock = vi.hoisted(() => ({
  familyUnit: {
    value: {
      id: 'unit-1',
      name: 'Test Family',
      representativeUserId: 'u1', // matches auth.user.id by default
      profilePhotoUrl: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    }
  },
  familyMembers: { value: [] },
  loading: { value: false },
  error: { value: null },
  getCurrentUserFamilyUnit: vi.fn().mockResolvedValue(null),
  getFamilyMembers: vi.fn().mockResolvedValue(undefined),
  getFamilyUnitById: vi.fn().mockResolvedValue(null),
  createFamilyUnit: vi.fn(),
  updateFamilyUnit: vi.fn(),
  deleteFamilyUnit: vi.fn(),
  createFamilyMember: vi.fn(),
  updateFamilyMember: vi.fn(),
  deleteFamilyMember: vi.fn(),
  uploadMemberProfilePhoto: vi.fn(),
  removeMemberProfilePhoto: vi.fn(),
  uploadUnitProfilePhoto: vi.fn(),
  removeUnitProfilePhoto: vi.fn(),
}))

vi.mock('@/composables/useFamilyUnits', () => ({
  useFamilyUnits: () => familyUnitMock,
}))
```

Then in each test, set `familyUnitMock.familyUnit.value.representativeUserId` before `mount()` and reset it in `beforeEach`.

Also update the `componentStubs` to include the missing stubs that `FamilyUnitPage` now requires:
```ts
ProfilePhotoAvatar: { name: 'ProfilePhotoAvatar', template: '<div />', props: ['photoUrl', 'initials', 'size', 'editable', 'loading'] },
FamilyMemberList: { name: 'FamilyMemberList', template: '<div />', props: ['members', 'loading', 'canManageMemberships', 'readOnly', 'uploadingMemberId'] },
```

---

### Step 3: Update Documentation

No documentation files require changes for this frontend fix — it's a one-line bug fix with no new patterns introduced.

Update `ai-specs/changes/feat-family-member-access_enriched.md` status to `COMPLETE` after both backend and frontend implementations are merged.

---

## Implementation Order

1. Step 0 — Create feature branch from `dev` (or backend branch for local testing)
2. Step 1 — Fix `isViewingOther` in `FamilyUnitPage.vue` (1 line)
3. Step 2 — Update Vitest tests: refactor mock to mutable pattern + add 3 new tests
4. Step 3 — Update enriched spec status

---

## Testing Checklist

- [ ] Existing Vitest tests for `FamilyUnitPage.spec.ts` still pass
- [ ] New test: representative user sees edit controls (no route param case)
- [ ] New test: linked member user (different `representativeUserId`) sees read-only
- [ ] New test: linked member via `/mi-familia` (no route param) sees read-only — this is the regression test
- [ ] Manual test: Log in as User B (linked family member), navigate to `/mi-familia` — edit buttons should NOT appear, members list should be read-only
- [ ] Manual test: Log in as User B, navigate to `/inscripciones` — family's registrations should be listed
- [ ] Manual test: Log in as User B, open registration detail — page loads, no edit buttons visible
- [ ] Manual test: Log in as User A (representative), confirm edit controls still appear normally

---

## Error Handling Patterns

No new error states are introduced. The existing `error` ref in `useFamilyUnits` composable handles the case where `getCurrentUserFamilyUnit()` returns 404 (user has no family unit at all — not a linked member).

---

## UI/UX Considerations

- Non-representative family members will see the page title **"Unidad Familiar"** (not "Mi Unidad Familiar") — this is already driven by `isViewingOther` and is the correct UX: it signals they are viewing as a member, not as the manager.
- The "Volver a Administración" button will appear for non-representative members — this is technically incorrect UX (they are not admins). Consider whether to also check `auth.isAdmin || auth.isBoard` for this button. The button can be left as-is since non-representatives are not admins/board, but the button appears for them via the current template logic. A minimal safe fix is to keep the button gated purely on `isViewingOther` (which is now true for both admins-viewing-other AND linked members) and add a back button to "Mi Unidad Familiar" or hide it for non-admins.

> **Recommended additional UI tweak** (optional, non-blocking): in the template, the "Volver a Administración" button should also check `auth.isAdmin || auth.isBoard`:
> ```html
> <Button
>   v-if="isViewingOther && (auth.isAdmin || auth.isBoard)"
>   ...
> />
> ```
> Without this tweak, a linked family member will see a "Volver a Administración" button that navigates to `/admin` — which they have no access to. This should be included in the implementation.

---

## Dependencies

No new npm packages required. No new PrimeVue components. No new Axios calls.

---

## Notes

- The backend branch (`feature/feat-family-member-access-backend`) must be merged to `dev` before this frontend can be fully tested end-to-end in a real environment.
- The change to `isViewingOther` is backward-compatible: Admin and Board users viewing another family's page via `/admin/family-units/{id}` will still see `isViewingOther = true` (since their `id !== representativeUserId`) and get the correct read-only view.
- **No changes are needed in `RegistrationDetailPage.vue`** — confirmed that `canEdit` already checks `isRepresentative.value` and the template uses `isRepresentative && canCancel` for the cancel button.
- TypeScript strict mode: `familyUnit.value !== null` (not `?.`) ensures the type narrowing is correct.

---

## Next Steps After Implementation

1. Merge backend branch into `dev` first
2. Merge this frontend branch into `dev`
3. QA: Test end-to-end with two real user accounts (representative + linked member) in the dev environment
4. Backfill: Verify existing family members in the database have their `UserId` populated correctly (may require the optional SQL backfill script from the enriched spec)

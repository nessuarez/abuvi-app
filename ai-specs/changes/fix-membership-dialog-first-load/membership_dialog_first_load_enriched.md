# Fix: MembershipDialog Does Not Load Data on First Open

## Problem

When a user opens the "Gestionar membresía" modal for a family member for the **first time** (or the first time after a page reload), no membership data is displayed. Closing and reopening the modal without refreshing shows the data correctly.

## Root Cause

**File:** `frontend/src/components/memberships/MembershipDialog.vue:45–52`  
**File:** `frontend/src/views/FamilyUnitPage.vue:243–245` (same pattern in `ProfilePage.vue:227–229`)

The parent opens the dialog by setting two reactive refs in the same synchronous block:

```ts
selectedMemberForMembership.value = member  // makes v-if true → component mounts
showMembershipDialog.value = true           // sets visible=true
```

Because both assignments are synchronous, Vue batches them into a single DOM update. The `MembershipDialog` component mounts with `visible=true` already set from its first render. The watcher inside the dialog:

```ts
watch(
  () => props.visible,
  async (val) => {
    if (val && props.familyUnitId && props.memberId) {
      await getMembership(props.familyUnitId, props.memberId)
    }
  },
)
```

…registers **after** the component mounts, at which point `visible` is already `true` and no transition (`false → true`) has occurred. Without `{ immediate: true }`, the callback never fires on the initial render, so `getMembership` is never called.

On second open: the parent only sets `showMembershipDialog = false` on close (it does not null `selectedMemberForMembership`), so the component stays mounted. When reopened, `visible` goes `false → true`, the watcher fires, and data loads correctly.

## Fix

### File to modify: `frontend/src/components/memberships/MembershipDialog.vue`

Add `{ immediate: true }` to the watcher so the callback fires once on mount with the current value of `visible`:

```ts
// Before
watch(
  () => props.visible,
  async (val) => {
    if (val && props.familyUnitId && props.memberId) {
      await getMembership(props.familyUnitId, props.memberId)
    }
  },
)

// After
watch(
  () => props.visible,
  async (val) => {
    if (val && props.familyUnitId && props.memberId) {
      await getMembership(props.familyUnitId, props.memberId)
    }
  },
  { immediate: true },
)
```

No other files need to change.

## Acceptance Criteria

- [ ] Opening the membership modal for the first time (fresh page load) correctly shows the loading spinner and then renders the membership state (no membership / inactive / active + fees).
- [ ] Opening the modal a second time for the same member re-fetches and shows current data.
- [ ] Opening the modal for a different member shows that member's correct data.
- [ ] No regression in the modal's create, deactivate, reactivate, pay fee, or add fee flows.
- [ ] `ProfilePage.vue` modal usage shows the same correct behavior (it shares the same `MembershipDialog` component, so the fix propagates automatically).

## Scope

| Area | Change |
|------|--------|
| `frontend/src/components/memberships/MembershipDialog.vue` | Add `{ immediate: true }` to the `visible` watcher |
| Backend | None |
| Tests | Update `MembershipDialog.spec.ts` to verify `getMembership` is called when the component mounts with `visible=true` |

## Non-functional Requirements

- **Performance:** The API call happens only when `visible` is `true`. Mounting with `visible=false` (hypothetical) would not trigger an unnecessary request.
- **No stale data:** Because `useMemberships()` is instantiated inside the component and the component is kept mounted (not destroyed on close), data from the previous fetch is displayed immediately while the new fetch is in flight. The `loading` state correctly covers this transition with a spinner.

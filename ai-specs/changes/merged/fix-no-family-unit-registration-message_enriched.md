# Fix: Differentiate "no family unit" vs "not representative" messages in camp registration

## Problem

When a user has **no family unit created**, the camp page (`CampPage.vue`) shows a disabled button with the text *"Solo el representante puede inscribirse"*, which is confusing because the user doesn't even have a family unit yet. The same issue propagates to `RegisterForCampPage.vue`, where no message at all is shown if the user has no family unit.

The `isRepresentative` computed property returns `false` in both cases:
- No family unit → `familyUnit.value` is `null` → `!!null` → `false`
- Has family unit but is not the representative → `representativeUserId !== auth.user.id` → `false`

## Desired Behavior

### Scenario 1: User has NO family unit
- **CampPage.vue (hero CTA area):** Show an informational message:
  > *"En primer lugar, define tu unidad familiar para poder inscribirte."*

  With a button/link to `/family-unit` (the family unit creation page).
- **RegisterForCampPage.vue:** Show a `Message` component with the same text and a link to `/family-unit`. The wizard should still be blocked (no members to select).

### Scenario 2: User has a family unit but is NOT the representative
- **CampPage.vue:** Current behavior is correct — disabled button + helper text.
- **RegisterForCampPage.vue:** Current warning message is correct.

## Files to Modify

### 1. `frontend/src/views/CampPage.vue`

**Changes in the CTA section (lines ~200-252):**

Add a new computed property:
```ts
const hasFamilyUnit = computed(() => !!familyUnit.value)
```

Update the template CTA block to handle three states when status is `'Open'`:
1. `isRepresentative` → show "Inscripciones Abiertas" button (existing, no change)
2. `!hasFamilyUnit` → show info message: "En primer lugar, define tu unidad familiar para poder inscribirte." + RouterLink button to `/family-unit`
3. `hasFamilyUnit && !isRepresentative` → show disabled "Solo el representante puede inscribirse" button (existing behavior)

Update the non-representative notice (line 248) condition to only show when `familyUnit` exists (already the case — `&& familyUnit`), no change needed there.

Update the **bottom CTA** section (lines ~396-412) to also show the "create family unit" message when `!hasFamilyUnit && status === 'Open'`.

### 2. `frontend/src/views/registrations/RegisterForCampPage.vue`

**Changes in the warning area (lines ~227-231):**

Add a new `Message` block for when `!familyUnit` (no family unit exists):
```html
<Message v-if="!familyUnit" severity="info" :closable="false" class="mb-6">
  En primer lugar, define tu unidad familiar para poder inscribirte.
  <RouterLink to="/family-unit" class="ml-1 font-semibold text-blue-600 underline">
    Crear unidad familiar
  </RouterLink>
</Message>
```

The existing warning for non-representative stays as-is (already conditioned on `familyUnit` being truthy).

## Acceptance Criteria

- [ ] When a user with **no family unit** visits the camp page with an open edition, they see the message "En primer lugar, define tu unidad familiar para poder inscribirte" with a link to `/family-unit`
- [ ] When a user with **no family unit** somehow navigates to the registration page, they see the same informational message with a link to create their family unit
- [ ] When a user has a family unit but is **not the representative**, the existing messages remain unchanged ("Solo el representante de la unidad familiar puede inscribirse...")
- [ ] When the user **is the representative**, the "Inscripciones Abiertas" button works as before
- [ ] The bottom CTA on the camp page also reflects the correct state for users without a family unit

## Non-Functional Requirements

- No backend changes needed — the frontend already fetches `familyUnit` on mount and handles `null` correctly
- Messages should be in Spanish, consistent with the rest of the UI
- Use existing PrimeVue `Message` and `RouterLink` components — no new dependencies

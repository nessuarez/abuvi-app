# Fix: "Términos y Condiciones" Link in Registration Form

## Overview

The "términos y condiciones" link in the registration form on the login/register page (`/`) currently does nothing because it uses `href="#"`. The legal pages (`/legal/privacy`, `/legal/notice`) already exist in the router. This fix wires the link to the correct destination.

---

## User Story

**As a** new user filling in the registration form on the landing page
**I want** to be able to click "términos y condiciones" and read the relevant legal content
**So that** I can make an informed decision before accepting the terms and submitting my registration

---

## Root Cause

In [frontend/src/components/auth/RegisterForm.vue](frontend/src/components/auth/RegisterForm.vue) (line 178), the link is a dead anchor:

```html
<!-- CURRENT (broken) -->
<a href="#" class="text-primary-600 hover:text-primary-700">
  términos y condiciones
</a>
```

It has never been wired to any route. The `/legal/privacy` route has been registered in the router (as part of `feat-legal-section`) but was never linked from the registration form.

---

## Acceptance Criteria

- [ ] Clicking "términos y condiciones" in the registration form navigates to `/legal/privacy`
- [ ] The link opens in a **new browser tab** so the registration form is not lost
- [ ] The link is implemented with `<router-link>` (not `<a href>`) so it benefits from Vue Router resolution
- [ ] The link style is preserved (`text-primary-600 hover:text-primary-700`)
- [ ] The existing `acceptTerms` checkbox and validation logic remain untouched
- [ ] Unit test updated/created to assert the link has `target="_blank"` and resolves to the correct route

---

## Technical Context

### Where the registration form lives

The registration form is shown in a tab (`Registrarse`) inside:

```
LandingPage.vue (/)
  └── AuthContainer.vue
        └── TabView
              ├── LoginForm.vue  (tab 0)
              └── RegisterForm.vue  (tab 1)  ← fix here
```

### Router routes already defined

The router at `frontend/src/router/index.ts` already registers these public legal routes:

| Route Name      | Path             | Component          |
|-----------------|------------------|--------------------|
| `legal-privacy` | `/legal/privacy` | `PrivacyPage.vue`  |
| `legal-notice`  | `/legal/notice`  | `NoticeLegalPage.vue` |

The correct destination for "términos y condiciones" is `/legal/privacy` (Política de Privacidad), which covers GDPR data processing terms — this is the standard legal page linked from registration forms.

### Note on `target="_blank"` with `<router-link>`

Vue Router's `<router-link>` forwards non-router HTML attributes to the rendered `<a>` tag. Therefore `<router-link :to="..." target="_blank" rel="noopener noreferrer">` works correctly and is the preferred approach per project standards (keep navigation in Vue Router rather than hardcoded `href` strings).

---

## Dependency: Legal Pages Must Exist

This fix depends on the legal pages being implemented (current branch `feature/feat-legal-section-frontend`). The views at `frontend/src/views/legal/PrivacyPage.vue` must exist before this fix is deployed.

**If both tasks are part of the same branch** (which they are — `feature/feat-legal-section-frontend`), implement the legal pages first, then apply this fix in the same PR.

---

## Files to Modify

| File | Change |
|------|--------|
| `frontend/src/components/auth/RegisterForm.vue` | Replace `<a href="#">` with `<router-link>` pointing to `{ name: 'legal-privacy' }` |

---

## Implementation

### Step 1: Update the link in `RegisterForm.vue`

**File**: `frontend/src/components/auth/RegisterForm.vue`

Replace (lines 177–180):

```html
<!-- BEFORE -->
<a href="#" class="text-primary-600 hover:text-primary-700">
  términos y condiciones
</a>
```

With:

```html
<!-- AFTER -->
<router-link
  :to="{ name: 'legal-privacy' }"
  target="_blank"
  rel="noopener noreferrer"
  class="text-primary-600 hover:text-primary-700"
>
  términos y condiciones
</router-link>
```

**No imports needed** — `RouterLink` is globally registered via Vue Router.

---

### Step 2: Write / update unit test

**File**: `frontend/src/components/auth/__tests__/RegisterForm.test.ts` (create if it doesn't exist)

Add the following test case:

```typescript
describe('RegisterForm', () => {
  // ... existing tests

  describe('terms and conditions link', () => {
    it('should render a router-link to the privacy policy page', () => {
      const wrapper = mount(RegisterForm, {
        global: { plugins: [router] }
      })
      const termsLink = wrapper.find('a[href*="legal/privacy"]')
      expect(termsLink.exists()).toBe(true)
    })

    it('should open the terms link in a new tab', () => {
      const wrapper = mount(RegisterForm, {
        global: { plugins: [router] }
      })
      const termsLink = wrapper.find('a[href*="legal/privacy"]')
      expect(termsLink.attributes('target')).toBe('_blank')
    })

    it('should include rel="noopener noreferrer" on the terms link', () => {
      const wrapper = mount(RegisterForm, {
        global: { plugins: [router] }
      })
      const termsLink = wrapper.find('a[href*="legal/privacy"]')
      expect(termsLink.attributes('rel')).toBe('noopener noreferrer')
    })
  })
})
```

---

## Non-Functional Requirements

- **Security**: `rel="noopener noreferrer"` must be present on all `target="_blank"` links to prevent tab-napping (tabnabbing attack vector)
- **UX**: New tab is essential — users must not lose their form data when clicking the link
- **Routing**: Use named route `{ name: 'legal-privacy' }` instead of hardcoded path string, so route changes are reflected automatically

---

## Out of Scope

- Changing validation behavior for `acceptTerms`
- Adding a link to Aviso Legal (`/legal/notice`) — Privacy Policy is the standard for this context
- Creating the legal page content (handled by `feat-legal-section`)

---

## Verification Checklist

- [ ] Clicking "términos y condiciones" opens `/legal/privacy` in a new tab
- [ ] Registration form state is preserved after clicking the link
- [ ] Link style matches surrounding text style
- [ ] Unit tests pass: `npm run test`
- [ ] ESLint passes: `npm run lint`
- [ ] TypeScript passes: `npx vue-tsc --noEmit`
- [ ] `rel="noopener noreferrer"` is present

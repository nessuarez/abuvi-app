# Fix: Userback Widget Not Loading & Homepage Buttons Not Working

## Context

Two symptoms are reported on the main page (`/home`):

1. **Userback feedback widget does not appear** — the floating feedback button is missing.
2. **Homepage buttons are not responding** — CTA buttons in `HomeHeroCarousel.vue` and possibly the `OnboardingButton` do not react to clicks.

These two issues likely share a **common root cause**: a JavaScript `TypeError` thrown in `App.vue` during Userback initialization, which may corrupt Vue's watcher state or block subsequent event handlers.

---

## Root Cause Analysis

### 1. Race condition: `ub.init()` called before Userback script finishes loading

**File:** `frontend/src/App.vue` (lines 15–23)

```ts
watch(() => auth.isAuthenticated, (isAuth) => {
  const ub = (window as any).Userback
  if (isAuth && ub) {          // ← BUG: `ub` is {} (truthy), not the full Userback API
    ub.init(...)               // ← TypeError: ub.init is not a function
  }
}, { immediate: true })
```

In `index.html`, the widget script is loaded **asynchronously** (`s.async = true`). Before the script finishes loading, `window.Userback` is the placeholder object `{}` set in:

```html
window.Userback = window.Userback || {};
```

This object is **truthy** but has no `init` method. When `auth.restoreSession()` restores a session on mount, `auth.isAuthenticated` becomes `true` immediately, and the `{ immediate: true }` watcher fires — calling `{}.init(...)` and throwing an **uncaught TypeError**.

### 2. Incorrect re-initialization pattern

**File:** `frontend/src/App.vue`

The `index.html` script uses `access_token` for **auto-initialization** — Userback reads this token when the script loads and initializes itself automatically. Calling `ub.init(token, ...)` a second time in `App.vue` is **redundant and conflicting**: Userback's auto-init already ran; calling `init()` again with a different argument may reset internal state.

The correct pattern to attach user identity after login is `Userback.identify()` or `Userback.setData()`, not `init()`.

### 3. Empty token in development environment

**File:** `frontend/.env.development`

```
VITE_USERBACK_TOKEN=
```

`index.html` uses `%VITE_USERBACK_TOKEN%` (Vite HTML env replacement). When the variable is empty, `access_token` is set to `''`, and Userback silently fails to authenticate — the widget loads but never displays the button because no project is associated.

**Fix for local dev:** developers must add the real token to `.env.local` (git-ignored). The `.env.development` file is intentionally empty as documented.

### 4. `ub` type-guard is insufficient

```ts
if (isAuth && ub) {  // truthy check on {} passes
```

The guard only checks truthiness, not whether `ub.init` is a callable function. It should be:

```ts
if (isAuth && typeof ub?.init === 'function') { ... }
```

---

## Debugging Steps (in Browser DevTools)

1. **Console tab** — look for `TypeError: ub.init is not a function` or similar errors on page load.
2. **Network tab** → filter by `userback` → check if `https://static.userback.io/widget/v1.js` loads (status 200 vs blocked/failed).
3. **Console** — run `window.Userback` to inspect the object:
   - If it's `{}` or has no `init` method → race condition confirmed.
   - If it's the full Userback API → token is the issue.
4. **Console** — run `window.Userback?.token` or `window.Userback?.access_token` to confirm the token is set.
5. **Vue DevTools** — check if any components show errors in the component tree (red warning icons).

---

## Files to Modify

| File | Change |
|---|---|
| `frontend/src/App.vue` | Fix watch callback: guard with `typeof ub?.init === 'function'`, replace `init()` re-call with `identify()`/`setData()` |
| `frontend/src/App.vue` | Wrap Userback call in try/catch to prevent uncaught errors from blocking Vue lifecycle |

### `frontend/src/App.vue` — Fixed implementation

```vue
<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AuthenticatedLayout from '@/layouts/AuthenticatedLayout.vue'
import Toast from 'primevue/toast'

const route = useRoute()
const auth = useAuthStore()

const isLandingPage = computed(() => route.path === '/')
const useLayout = computed(() => !isLandingPage.value && auth.isAuthenticated)

// Attach user identity to Userback after authentication.
// The widget is already auto-initialized via index.html (access_token).
// We only call identify() here — never init() again.
watch(() => auth.isAuthenticated, (isAuth) => {
  try {
    const ub = (window as any).Userback
    if (!isAuth || typeof ub?.identify !== 'function') return
    ub.identify(auth.user?.email ?? '', {
      name: `${auth.user?.firstName ?? ''} ${auth.user?.lastName ?? ''}`.trim(),
      email: auth.user?.email ?? '',
    })
  } catch (err) {
    console.warn('[Userback] Failed to identify user:', err)
  }
}, { immediate: true })

onMounted(() => {
  auth.restoreSession()
})
</script>
```

> **Note:** `Userback.identify()` is the documented API for attaching user identity after the widget is already initialized. If the Userback SDK version in use does not support `identify()`, use `Userback.setData({ email, name })` instead. Check the Userback JS API docs for the installed version.

---

## Acceptance Criteria

- [ ] No `TypeError` appears in the browser console on page load
- [ ] Userback floating feedback button is visible when a valid `VITE_USERBACK_TOKEN` is set in `.env.local`
- [ ] Homepage CTA buttons (`router-link`, `<a>` tags, dot indicators) respond to clicks normally
- [ ] When a user logs in, their email and name are attached to Userback feedback submissions
- [ ] If Userback is unavailable (token not set, script blocked), the application continues to work normally — no uncaught errors

## Non-Functional Requirements

- **Resilience**: Userback initialization must never crash the Vue application — wrap in try/catch
- **No double-init**: The widget is initialized by `index.html`; `App.vue` must only enrich identity, not call `init()` again
- **Type safety**: Use `typeof ub?.identify === 'function'` guards before calling any Userback API method

## Out of Scope

- Adding or removing the Userback widget script from `index.html`
- Changing the environment variable structure
- Adding new Userback features (annotations, recording, etc.)

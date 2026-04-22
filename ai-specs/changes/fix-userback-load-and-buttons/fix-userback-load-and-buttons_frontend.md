# Frontend Implementation Plan: fix-userback-load-and-buttons

## Overview

This fix resolves a race condition in `App.vue` where the Userback watcher fired before the async widget script finished loading, causing `TypeError: s.init is not a function`. The error propagated uncaught and could block Vue watcher execution, explaining both the broken widget and unresponsive homepage buttons.

The fix has two parts:
1. **Code fix** — already applied to `frontend/src/App.vue`.
2. **Tests** — unit tests for the Userback watcher behaviour in `App.vue` still need to be written.

No new composables, stores, types, or routes are required.

---

## Architecture Context

| Layer | File | Role |
|---|---|---|
| Root component | `frontend/src/App.vue` | Hosts Userback identity watcher — **already fixed** |
| Widget loader | `frontend/index.html` | Static script tag; auto-inits widget via `access_token` — no change |
| Auth store | `frontend/src/stores/auth.ts` | Provides `isAuthenticated`, `user.email`, `user.firstName`, `user.lastName` |
| Test | `frontend/src/views/__tests__/App.spec.ts` | New file — unit tests for the watcher |

**State management:** no Pinia changes needed; the watch reads from the existing `auth` store.

**Routing:** no changes.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a dedicated fix branch.
- **Implementation Steps**:
  1. Ensure you are on `dev`: `git checkout dev && git pull origin dev`
  2. Create the branch: `git checkout -b fix/fix-userback-load-and-buttons-frontend`
  3. Verify: `git branch`
- **Notes**: The code fix in `App.vue` has already been applied on the current branch. If already there, skip creation and just verify you are on the correct branch.

---

### Step 1: Verify Applied Fix in `App.vue` ✅ (already done)

- **File**: `frontend/src/App.vue`
- **Action**: Confirm the watcher uses `identify()` with a type-guard and try/catch.
- **Expected state after fix**:

```ts
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
```

- **Key invariants to verify**:
  - No call to `ub.init()` anywhere in `App.vue`
  - Guard checks `typeof ub?.identify === 'function'`, not just truthiness
  - Entire block is in `try/catch`
  - `console.warn` (not `console.error`) used so it does not trigger error monitors

---

### Step 2: Write Unit Tests for the Userback Watcher

- **File**: `frontend/src/views/__tests__/App.spec.ts` *(new file)*
- **Action**: Cover the four cases the watcher must handle.
- **Dependencies**: `vitest`, `@vue/test-utils`, `pinia`, `vue-router` (all already installed)

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import App from '@/App.vue'

// Stub heavy child components to keep tests fast
vi.mock('@/layouts/AuthenticatedLayout.vue', () => ({
  default: { name: 'AuthenticatedLayout', template: '<div><slot /></div>' }
}))
vi.mock('primevue/toast', () => ({
  default: { name: 'Toast', template: '<div />' }
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/', component: { template: '<div />' } },
    { path: '/home', component: { template: '<div />' } },
  ]
})

function mountApp() {
  return mount(App, {
    global: {
      plugins: [createPinia(), router],
    }
  })
}

describe('App.vue — Userback identity watcher', () => {
  let originalUserback: unknown

  beforeEach(() => {
    setActivePinia(createPinia())
    originalUserback = (window as any).Userback
  })

  afterEach(() => {
    (window as any).Userback = originalUserback
    vi.restoreAllMocks()
  })

  it('should not call identify when user is not authenticated', async () => {
    const identify = vi.fn()
    ;(window as any).Userback = { identify }

    mountApp()
    await flushPromises()

    expect(identify).not.toHaveBeenCalled()
  })

  it('should not throw when Userback script has not loaded yet (window.Userback is {})', async () => {
    ;(window as any).Userback = {}  // placeholder object — no identify method

    const auth = useAuthStore()
    auth.$patch({ token: 'token', user: { email: 'u@test.com', firstName: 'A', lastName: 'B' } as any })

    expect(() => mountApp()).not.toThrow()
  })

  it('should call identify with email and full name when authenticated and Userback is ready', async () => {
    const identify = vi.fn()
    ;(window as any).Userback = { identify }

    const auth = useAuthStore()
    auth.$patch({ token: 'token', user: { email: 'u@test.com', firstName: 'Ana', lastName: 'García' } as any })

    mountApp()
    await flushPromises()

    expect(identify).toHaveBeenCalledWith('u@test.com', {
      name: 'Ana García',
      email: 'u@test.com',
    })
  })

  it('should not propagate errors if Userback.identify throws', async () => {
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    ;(window as any).Userback = {
      identify: () => { throw new Error('Userback internal error') }
    }

    const auth = useAuthStore()
    auth.$patch({ token: 'token', user: { email: 'u@test.com', firstName: 'A', lastName: 'B' } as any })

    expect(() => mountApp()).not.toThrow()
    await flushPromises()
    expect(consoleWarn).toHaveBeenCalledWith('[Userback] Failed to identify user:', expect.any(Error))
  })
})
```

- **Implementation Notes**:
  - `auth.$patch` is used instead of `auth.login()` to set authenticated state without an HTTP call.
  - The `token` field must be non-null because `isAuthenticated` is `computed(() => !!token.value)` in the auth store — check `stores/auth.ts` to confirm the computed property name.
  - Stub `AuthenticatedLayout` and `Toast` to avoid pulling in PrimeVue's full setup in the test environment.
  - Test file goes in `src/views/__tests__/` to follow the convention established by `CampPage.spec.ts` and `ProfilePage.spec.ts`.

---

### Step 3: Update Technical Documentation

- **File**: `ai-specs/specs/frontend-standards.mdc`
- **Action**: Add a note under **External Service Integration Pattern** (or a new subsection) documenting the correct Userback widget pattern.
- **Content to add** (under the existing "External Service Integration Pattern" section or as a new subsection "Third-party Widget Lifecycle"):

```markdown
### Third-party Widget Lifecycle (Userback pattern)

When integrating a third-party widget that self-initializes via a static `<script>` tag in `index.html`:

1. **Never call `init()` again** from Vue — the widget already initialized via the HTML tag.
2. **For identity enrichment** after login, use the widget's identity API (e.g., `Userback.identify()`), not `init()`.
3. **Guard with a type-check** before calling any widget method — the script is async, so `window.Widget` may be a placeholder `{}` object when the Vue app boots:
   ```ts
   if (typeof window.Userback?.identify === 'function') { ... }
   ```
4. **Wrap in try/catch** so third-party failures never crash the Vue app.
5. **Use `console.warn`** (not `console.error`) for integration failures to avoid triggering error monitors.
```

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Verify `App.vue` fix (already applied)
3. Step 2 — Write unit tests in `App.spec.ts`
4. Step 3 — Update `frontend-standards.mdc` documentation

---

## Testing Checklist

- [ ] `npm run test` passes with no failures
- [ ] `App.spec.ts` covers all four watcher cases (unauthenticated, script not ready, script ready, identify throws)
- [ ] No `TypeError` in browser console on page load
- [ ] Userback floating button appears when `VITE_USERBACK_TOKEN` is set in `.env.local`
- [ ] Homepage CTA buttons and dot indicators respond to clicks
- [ ] TypeScript check passes: `npx vue-tsc --noEmit`

---

## Error Handling Patterns

- `try/catch` inside the `watch` callback prevents Userback errors from corrupting Vue's watcher queue.
- `console.warn` is used (not `throw`) so the error is visible in DevTools without being reported to GlitchTip as an application error.
- The `typeof ub?.identify !== 'function'` guard handles three cases silently: Userback not loaded, Userback blocked by ad-blocker, and token not configured.

---

## Dependencies

No new npm packages required. All test utilities (`vitest`, `@vue/test-utils`, `pinia`) are already installed.

---

## Notes

- `window.Userback` in `index.html` is initialized as `{}` before the async script loads. This object is **truthy** — a simple `if (ub)` guard is insufficient and will cause a `TypeError`.
- The `{ immediate: true }` option on the watcher means the callback fires synchronously during component setup, before any async script has time to load. The type-guard is therefore essential.
- `Userback.identify()` is the correct post-init API. If a future Userback SDK version deprecates this method, check for `Userback.setData({ email, name })` as a fallback.
- `VITE_USERBACK_TOKEN` is intentionally empty in `.env.development`. Developers must add the real token to `.env.local` (git-ignored) to test the widget locally.

---

## Next Steps After Implementation

- Verify in the Userback dashboard that feedback submissions include the user's email and name.
- If `identify()` is not recognized by the Userback SDK version in production, fall back to `Userback.setData({ email, name })`.

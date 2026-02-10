# Phase 3: Pending Integration Tasks

## Overview

Phase 3 (Frontend Authentication Integration) is **95% complete**. This document outlines the remaining tasks to fully match the specification.

**Status:** 2 minor tasks remaining (~15 minutes total)

---

## Task 1: Update HomePage with User Information

**Priority:** Medium
**Effort:** 5 minutes
**File:** `frontend/src/pages/HomePage.vue`

### Current State

The HomePage shows a generic welcome message without user context:

```vue
<script setup lang="ts">
import { ref } from 'vue'

const message = ref('Welcome to ABUVI')
</script>

<template>
  <div class="flex min-h-screen items-center justify-center">
    <h1 class="text-4xl font-bold">{{ message }}</h1>
  </div>
</template>
```

### Expected State (Per Phase 3 Spec)

Should display personalized greeting with user's name and role:

```vue
<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
</script>

<template>
  <div>
    <h1 class="mb-4 text-4xl font-bold">Welcome to ABUVI</h1>
    <p v-if="authStore.user" class="text-gray-700">
      Hello, {{ authStore.user.firstName }}! You are logged in as
      <span class="font-semibold">{{ authStore.user.role }}</span>.
    </p>
    <p v-else class="text-gray-500">
      Please log in to access your account.
    </p>
  </div>
</template>
```

### Acceptance Criteria

- [ ] HomePage imports and uses `useAuthStore`
- [ ] Displays user's first name when authenticated
- [ ] Displays user's role when authenticated
- [ ] Shows fallback message when not authenticated
- [ ] Uses Tailwind CSS for styling

---

## Task 2: Persist User Data in localStorage

**Priority:** High
**Effort:** 10 minutes
**File:** `frontend/src/stores/auth.ts`

### Problem

Currently, only the JWT token is persisted in localStorage. When the page reloads:

1. ✅ Token is restored → user is "authenticated"
2. ❌ User data is `null` → UI can't show user info immediately

This causes a mismatch where `isAuthenticated` is `true` but `user` is `null`.

### Current Implementation

**In `setAuth()` - Line 20-24:**
```typescript
function setAuth(authData: { user: UserInfo; token: string }) {
  user.value = authData.user
  token.value = authData.token
  localStorage.setItem(AUTH_TOKEN_KEY, authData.token)
  // ❌ User data NOT saved
}
```

**In `restoreSession()` - Line 32-39:**
```typescript
function restoreSession() {
  const savedToken = localStorage.getItem(AUTH_TOKEN_KEY)
  if (savedToken) {
    token.value = savedToken
    // ❌ User data NOT restored
  }
}
```

### Required Changes

#### 1. Update `setAuth()` to save user data

```typescript
function setAuth(authData: { user: UserInfo; token: string }) {
  user.value = authData.user
  token.value = authData.token

  // Persist both token and user
  localStorage.setItem(AUTH_TOKEN_KEY, authData.token)
  localStorage.setItem('abuvi_user', JSON.stringify(authData.user)) // ← ADD THIS
}
```

#### 2. Update `restoreSession()` to restore user data

```typescript
function restoreSession() {
  const savedToken = localStorage.getItem(AUTH_TOKEN_KEY)
  const savedUser = localStorage.getItem('abuvi_user') // ← ADD THIS

  if (savedToken && savedUser) {  // ← UPDATE CONDITION
    token.value = savedToken

    // Parse and restore user data
    try {
      user.value = JSON.parse(savedUser)
    } catch (error) {
      console.error('Failed to parse stored user data', error)
      clearAuth() // Clear everything if data is corrupted
    }
  }
}
```

#### 3. Update `clearAuth()` to remove user data

```typescript
function clearAuth() {
  user.value = null
  token.value = null
  localStorage.removeItem(AUTH_TOKEN_KEY)
  localStorage.removeItem('abuvi_user') // ← ADD THIS
}
```

### Complete Updated Store

**Path:** `frontend/src/stores/auth.ts`

```typescript
import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type { UserInfo } from '@/types/auth'

const AUTH_TOKEN_KEY = 'abuvi_auth_token'
const AUTH_USER_KEY = 'abuvi_user'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<UserInfo | null>(null)
  const token = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isBoard = computed(() =>
    user.value?.role === 'Admin' || user.value?.role === 'Board'
  )

  // Actions
  function setAuth(authData: { user: UserInfo; token: string }) {
    user.value = authData.user
    token.value = authData.token
    localStorage.setItem(AUTH_TOKEN_KEY, authData.token)
    localStorage.setItem(AUTH_USER_KEY, JSON.stringify(authData.user))
  }

  function clearAuth() {
    user.value = null
    token.value = null
    localStorage.removeItem(AUTH_TOKEN_KEY)
    localStorage.removeItem(AUTH_USER_KEY)
  }

  function restoreSession() {
    const savedToken = localStorage.getItem(AUTH_TOKEN_KEY)
    const savedUser = localStorage.getItem(AUTH_USER_KEY)

    if (savedToken && savedUser) {
      token.value = savedToken
      try {
        user.value = JSON.parse(savedUser)
      } catch (error) {
        console.error('Failed to parse stored user data', error)
        clearAuth()
      }
    }
  }

  return {
    user,
    token,
    isAuthenticated,
    isAdmin,
    isBoard,
    setAuth,
    clearAuth,
    restoreSession
  }
})
```

### Acceptance Criteria

- [ ] `setAuth()` saves both token and user to localStorage
- [ ] `restoreSession()` restores both token and user from localStorage
- [ ] `clearAuth()` removes both token and user from localStorage
- [ ] Error handling for corrupted user data in localStorage
- [ ] `isAuthenticated` checks both token AND user exist
- [ ] User info displays immediately after page reload (no flash of missing data)

### Testing

After implementing, verify:

1. **Login Flow:**
   - Log in → Check localStorage has both `abuvi_auth_token` and `abuvi_user`
   - User info displays in header immediately

2. **Page Reload:**
   - Reload page → User info still displays (no flash)
   - Check DevTools → Both keys present in localStorage

3. **Logout Flow:**
   - Log out → Check localStorage has neither key
   - User info disappears from UI

---

## Why These Tasks Matter

### Task 1 (HomePage)
- **User Experience:** Users expect to see their name after logging in
- **Spec Compliance:** Phase 3 spec explicitly requires personalized greeting

### Task 2 (User Persistence)
- **Bug Fix:** Currently `isAuthenticated` = true but `user` = null after reload
- **UX Issue:** User sees "undefined undefined" briefly on page reload
- **Data Consistency:** Token and user should always be in sync

---

## Implementation Order

1. **Task 2 first** (User Persistence) - Fixes the underlying data issue
2. **Task 1 second** (HomePage) - Builds on the fixed data layer

---

## Related Files

- **Main Spec:** `ai-specs/changes/phase3_frontend_integration.md`
- **Auth Store:** `frontend/src/stores/auth.ts`
- **HomePage:** `frontend/src/pages/HomePage.vue`
- **Auth Store Tests:** `frontend/src/stores/__tests__/auth.test.ts` (will need updates)

---

## Notes

- These tasks align the implementation with the Phase 3 specification
- Current implementation is functional but incomplete per spec
- Both tasks are low-risk, high-value improvements
- After completion, Phase 3 will be 100% complete per specification

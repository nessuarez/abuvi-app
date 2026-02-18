# Frontend Implementation Plan: feat-i18n-user-management — Traducción Sección Gestión de Usuarios

## Overview

Translate all user-facing text in the User Management section from English to Spanish. This affects 6 Vue components (2 pages + 4 sub-components) and introduces one new utility function for role label translation. No routing, composable, or API changes are required — this is a pure UI text update.

**Architecture principles:** Vue 3 Composition API (`<script setup lang="ts">`), PrimeVue components, Tailwind CSS, no `<style>` blocks. All code/variable/function names remain in English per project standards.

Full translation reference in: [feat-i18n-user-management_enriched.md](./feat-i18n-user-management_enriched.md)

---

## Architecture Context

### Components involved

| File | Type | Change |
|---|---|---|
| `frontend/src/utils/user.ts` | New utility | `getRoleLabel()` helper |
| `frontend/src/utils/user.test.ts` | New test | Unit tests for helper |
| `frontend/src/components/users/UserForm.vue` | Component | Labels, placeholders, validation messages, role labels |
| `frontend/src/components/users/UserRoleCell.vue` | Component | `aria-label`, role display |
| `frontend/src/components/users/UserRoleDialog.vue` | Component | All UI strings |
| `frontend/src/components/users/UserCard.vue` | Component | Status display, role display |
| `frontend/src/pages/UsersPage.vue` | Page | Title, column headers, buttons, tag values, toast |
| `frontend/src/pages/UserDetailPage.vue` | Page | Title, buttons, field labels, tag values |
| `frontend/src/components/users/__tests__/UserForm.test.ts` | Test update | Spanish button text, validation messages |
| `frontend/src/components/users/__tests__/UserCard.test.ts` | Test update | Spanish status text |
| `frontend/src/components/users/__tests__/UserRoleCell.test.ts` | New test | aria-label, role display |
| `frontend/src/components/users/__tests__/UserRoleDialog.test.ts` | New test | Key UI strings |

### Routing

No routing changes needed. Route meta titles at `/users` and `/users/:id` are already in Spanish.

### State management

No Pinia store changes. `getRoleLabel()` is a pure utility function imported directly into components.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-i18n-user-management-frontend`
- **Implementation steps**:
  1. `git checkout main`
  2. `git pull origin main`
  3. `git checkout -b feature/feat-i18n-user-management-frontend`
  4. `git branch` — verify branch is active

---

### Step 1: Create `getRoleLabel` Utility (TDD — RED first)

#### Step 1a: Write the test first

- **File**: `frontend/src/utils/user.test.ts` *(new file)*
- **Action**: Write failing unit tests for `getRoleLabel`

```typescript
import { describe, it, expect } from 'vitest'
import { getRoleLabel } from '@/utils/user'

describe('getRoleLabel', () => {
  it('should return "Administrador" for Admin role', () => {
    expect(getRoleLabel('Admin')).toBe('Administrador')
  })

  it('should return "Junta Directiva" for Board role', () => {
    expect(getRoleLabel('Board')).toBe('Junta Directiva')
  })

  it('should return "Socio" for Member role', () => {
    expect(getRoleLabel('Member')).toBe('Socio')
  })

  it('should return the original value for unknown roles', () => {
    expect(getRoleLabel('Unknown')).toBe('Unknown')
  })
})
```

Run: `npx vitest run src/utils/user.test.ts` → **expect failure** (file does not exist)

#### Step 1b: Implement the utility

- **File**: `frontend/src/utils/user.ts` *(new file)*
- **Action**: Create helper to translate role enum values to Spanish labels

```typescript
import type { UserRole } from '@/types/user'

const ROLE_LABELS: Record<string, string> = {
  Admin: 'Administrador',
  Board: 'Junta Directiva',
  Member: 'Socio',
}

export const getRoleLabel = (role: UserRole | string): string => {
  return ROLE_LABELS[role] ?? role
}
```

- **Implementation notes**:
  - Uses `UserRole` type from `@/types/user` for typing but accepts `string` as fallback for safety
  - Returns original value if role not found (defensive, no crash for future roles)
  - Import: `import { getRoleLabel } from '@/utils/user'`

Run: `npx vitest run src/utils/user.test.ts` → **expect all 4 tests to pass** (GREEN)

---

### Step 2: Translate `UserForm.vue` (TDD — update tests first)

#### Step 2a: Update existing tests

- **File**: `frontend/src/components/users/__tests__/UserForm.test.ts`
- **Action**: Update tests that reference English button text, and add Spanish validation message tests

```typescript
// Update existing test — cancel button text
it('should emit cancel event when cancel button clicked', async () => {
  const wrapper = mountComponent({ mode: 'create' })
  const cancelButton = wrapper.findAll('button').find((b) => b.text() === 'Cancelar') // was 'Cancel'
  await cancelButton?.trigger('click')
  expect(wrapper.emitted('cancel')).toHaveLength(1)
})

// Update existing test — submit button text
it('should disable submit button when form is invalid', () => {
  const wrapper = mountComponent({ mode: 'create' })
  const submitButton = wrapper.findAll('button').find((b) => b.text().includes('Crear usuario')) // was 'Create User'
  expect(submitButton?.attributes('disabled')).toBeDefined()
})

// Add new validation message tests
it('should show Spanish validation message when email is empty on submit', async () => {
  const wrapper = mountComponent({ mode: 'create' })
  const form = wrapper.find('form')
  await form.trigger('submit')
  expect(wrapper.text()).toContain('El correo electrónico es obligatorio')
})

it('should show Spanish validation message when password is too short', async () => {
  const wrapper = mountComponent({ mode: 'create' })
  const emailInput = wrapper.find('#email')
  await emailInput.setValue('test@example.com')
  const passwordInput = wrapper.find('#password')
  await passwordInput.setValue('short')
  const form = wrapper.find('form')
  await form.trigger('submit')
  expect(wrapper.text()).toContain('La contraseña debe tener al menos 8 caracteres')
})

it('should show "Actualizar usuario" submit button in edit mode', () => {
  const wrapper = mountComponent({ mode: 'edit', user: mockUser })
  const submitButton = wrapper.findAll('button').find((b) => b.text().includes('Actualizar usuario'))
  expect(submitButton).toBeDefined()
})
```

Run: `npx vitest run src/components/users/__tests__/UserForm.test.ts` → **expect failures** (RED)

#### Step 2b: Implement changes in `UserForm.vue`

- **File**: `frontend/src/components/users/UserForm.vue`
- **Action**: Translate all user-facing strings; update `roleOptions` labels; import `getRoleLabel` for display

**Script changes:**
```typescript
import { getRoleLabel } from '@/utils/user'

// Update roleOptions labels (values remain unchanged — sent to API)
const roleOptions = [
  { label: getRoleLabel('Member'), value: 'Member' },
  { label: getRoleLabel('Board'), value: 'Board' },
  { label: getRoleLabel('Admin'), value: 'Admin' },
]

// Update validation messages
const validate = (): boolean => {
  // ...
  if (!formData.email.trim()) {
    errors.email = 'El correo electrónico es obligatorio'
  } else if (!formData.email.includes('@')) {
    errors.email = 'El formato del correo electrónico no es válido'
  }
  if (!formData.password.trim()) {
    errors.password = 'La contraseña es obligatoria'
  } else if (formData.password.length < 8) {
    errors.password = 'La contraseña debe tener al menos 8 caracteres'
  }
  if (!formData.firstName.trim()) {
    errors.firstName = 'El nombre es obligatorio'
  }
  if (!formData.lastName.trim()) {
    errors.lastName = 'Los apellidos son obligatorios'
  }
  // ...
}
```

**Template changes (complete list):**

| Old | New |
|---|---|
| `label="Email *"` | `"Correo electrónico *"` |
| `label="Password *"` | `"Contraseña *"` |
| `placeholder="Minimum 8 characters"` | `"Mínimo 8 caracteres"` |
| `label="First Name *"` | `"Nombre *"` |
| `placeholder="John"` | Remove (not culturally appropriate) |
| `label="Last Name *"` | `"Apellidos *"` |
| `placeholder="Doe"` | Remove |
| `label="Phone (optional)"` | `"Teléfono (opcional)"` |
| `label="Role *"` | `"Rol *"` |
| `placeholder="Select a role"` | `"Seleccionar un rol"` |
| `label="Active"` (InputSwitch) | `"Activo"` |
| Button `"Create User"` | `"Crear usuario"` |
| Button `"Update User"` | `"Actualizar usuario"` |
| Button `"Cancel"` | `"Cancelar"` |

- **Import to add**: `import { getRoleLabel } from '@/utils/user'`

Run: `npx vitest run src/components/users/__tests__/UserForm.test.ts` → **expect all tests to pass** (GREEN)

---

### Step 3: Translate `UserRoleCell.vue` (TDD — write test first)

#### Step 3a: Write test

- **File**: `frontend/src/components/users/__tests__/UserRoleCell.test.ts` *(new file)*
- **Action**: Write tests verifying Spanish role display and aria-label

```typescript
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import UserRoleCell from '@/components/users/UserRoleCell.vue'
import type { User } from '@/types/user'

const mockUser: User = {
  id: 'other-user-id',
  email: 'john@example.com',
  firstName: 'John',
  lastName: 'Doe',
  phone: null,
  role: 'Member',
  isActive: true,
  createdAt: '2026-02-08T10:00:00Z',
  updatedAt: '2026-02-08T10:00:00Z',
}

describe('UserRoleCell', () => {
  const mountComponent = (user: User) => {
    return mount(UserRoleCell, {
      props: { user },
      global: {
        plugins: [createPinia(), PrimeVue],
      },
    })
  }

  it('should display "Socio" for Member role', () => {
    const wrapper = mountComponent({ ...mockUser, role: 'Member' })
    expect(wrapper.find('[data-testid="role-badge"]').text()).toBe('Socio')
  })

  it('should display "Junta Directiva" for Board role', () => {
    const wrapper = mountComponent({ ...mockUser, role: 'Board' })
    expect(wrapper.find('[data-testid="role-badge"]').text()).toBe('Junta Directiva')
  })

  it('should display "Administrador" for Admin role', () => {
    const wrapper = mountComponent({ ...mockUser, role: 'Admin' })
    expect(wrapper.find('[data-testid="role-badge"]').text()).toBe('Administrador')
  })
})
```

> **Note on auth store mock**: `UserRoleCell.vue` uses `useAuthStore()`. The `createPinia()` plugin provides a default store instance. If the test runner throws an error about `auth.isBoard` being undefined, you'll need to mock the store:
> ```typescript
> import { setActivePinia } from 'pinia'
> // In beforeEach: setActivePinia(createPinia())
> ```

Run: `npx vitest run src/components/users/__tests__/UserRoleCell.test.ts` → **expect failures** (RED)

#### Step 3b: Implement changes in `UserRoleCell.vue`

- **File**: `frontend/src/components/users/UserRoleCell.vue`
- **Action**: Use `getRoleLabel()` for role display, translate `aria-label`

```typescript
// Add import
import { getRoleLabel } from '@/utils/user'
```

```html
<!-- Template: replace {{ user.role }} with getRoleLabel(user.role) -->
<span ... data-testid="role-badge">{{ getRoleLabel(user.role) }}</span>

<!-- Translate aria-label -->
<Button ... aria-label="Editar rol" />
```

Run: `npx vitest run src/components/users/__tests__/UserRoleCell.test.ts` → **all pass** (GREEN)

---

### Step 4: Translate `UserRoleDialog.vue` (TDD — write test first)

#### Step 4a: Write test

- **File**: `frontend/src/components/users/__tests__/UserRoleDialog.test.ts` *(new file)*
- **Action**: Test key Spanish strings in the dialog

```typescript
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import UserRoleDialog from '@/components/users/UserRoleDialog.vue'
import type { User } from '@/types/user'

const mockUser: User = {
  id: 'other-user-id',
  email: 'user@example.com',
  firstName: 'Ana',
  lastName: 'García',
  phone: null,
  role: 'Member',
  isActive: true,
  createdAt: '2026-02-08T10:00:00Z',
  updatedAt: '2026-02-08T10:00:00Z',
}

describe('UserRoleDialog', () => {
  const mountComponent = (visible: boolean, user: User | null) => {
    return mount(UserRoleDialog, {
      props: { visible, user },
      global: {
        plugins: [createPinia(), PrimeVue],
      },
    })
  }

  it('should display "Rol actual" label when visible', () => {
    const wrapper = mountComponent(true, mockUser)
    expect(wrapper.text()).toContain('Rol actual')
  })

  it('should display "Nuevo rol" label when visible', () => {
    const wrapper = mountComponent(true, mockUser)
    expect(wrapper.text()).toContain('Nuevo rol')
  })

  it('should display "Cancelar" button', () => {
    const wrapper = mountComponent(true, mockUser)
    const cancelButton = wrapper.findAll('button').find((b) => b.text() === 'Cancelar')
    expect(cancelButton).toBeDefined()
  })

  it('should display "Actualizar rol" button', () => {
    const wrapper = mountComponent(true, mockUser)
    const submitButton = wrapper.findAll('button').find((b) => b.text().includes('Actualizar rol'))
    expect(submitButton).toBeDefined()
  })

  it('should show "Motivo (opcional)" label', () => {
    const wrapper = mountComponent(true, mockUser)
    expect(wrapper.text()).toContain('Motivo (opcional)')
  })
})
```

Run → **expect failures** (RED)

#### Step 4b: Implement changes in `UserRoleDialog.vue`

- **File**: `frontend/src/components/users/UserRoleDialog.vue`
- **Action**: Translate all strings; update `availableRoles` labels; import `getRoleLabel`

**Script changes:**
```typescript
import { getRoleLabel } from '@/utils/user'

// Update availableRoles labels (values unchanged)
const availableRoles = computed(() => {
  const roles: { label: string; value: UserRole }[] = [
    { label: getRoleLabel('Member'), value: 'Member' },
    { label: getRoleLabel('Board'), value: 'Board' },
    { label: getRoleLabel('Admin'), value: 'Admin' },
  ]
  if (!auth.isAdmin) {
    return roles.filter((r) => r.value === 'Member')
  }
  return roles
})
```

**Template changes (complete list):**

| Old | New |
|---|---|
| `` `:header="`Update Role: ${name}`"` `` | `` `:header="`Actualizar rol: ${user?.firstName} ${user?.lastName}`"` `` |
| `"You cannot change your own role"` | `"No puedes cambiar tu propio rol"` |
| `"Current Role"` | `"Rol actual"` |
| `"New Role *"` | `"Nuevo rol *"` |
| `placeholder="Select new role"` | `placeholder="Seleccionar nuevo rol"` |
| `"Reason (optional)"` | `"Motivo (opcional)"` |
| Reason placeholder | `"Indica el motivo del cambio de rol (queda registrado para auditoría)"` |
| `"characters"` (counter) | `"caracteres"` |
| Button `"Cancel"` | `"Cancelar"` |
| Button `"Update Role"` | `"Actualizar rol"` |

Run tests → **all pass** (GREEN)

---

### Step 5: Translate `UserCard.vue` (TDD — update test first)

#### Step 5a: Update `UserCard.test.ts`

- **File**: `frontend/src/components/users/__tests__/UserCard.test.ts`
- **Action**: Add test for Spanish status display; add test for Spanish role

```typescript
// Add new test: Active status in Spanish
it('should show "Activo" for active user', () => {
  const wrapper = mountComponent({ user: mockUser }) // mockUser.isActive = true
  expect(wrapper.text()).toContain('Activo')
})

it('should show "Inactivo" for inactive user', () => {
  const inactiveUser = { ...mockUser, isActive: false }
  const wrapper = mountComponent({ user: inactiveUser })
  expect(wrapper.text()).toContain('Inactivo')
})

it('should show Spanish role label "Socio" for Member role', () => {
  const wrapper = mountComponent({ user: mockUser }) // mockUser.role = 'Member'
  expect(wrapper.text()).toContain('Socio')
})
```

Run → **expect failures** (RED)

#### Step 5b: Implement changes in `UserCard.vue`

- **File**: `frontend/src/components/users/UserCard.vue`
- **Action**: Translate status display; use `getRoleLabel()` for role; use PrimeVue Tag correctly

```typescript
import { getRoleLabel } from '@/utils/user'
```

```html
<!-- Role tag: use getRoleLabel() -->
<Tag :value="getRoleLabel(user.role)" :severity="getRoleSeverity(user.role)" />

<!-- Status text: translate -->
<span class="text-gray-700">
  {{ user.isActive ? 'Activo' : 'Inactivo' }}
</span>
```

Run: `npx vitest run src/components/users/__tests__/UserCard.test.ts` → **all pass** (GREEN)

> **Note**: The existing test `should render user information` asserts `wrapper.text().toContain('Member')`. After translation, this becomes `'Socio'`. Update this assertion in the test file:
> ```typescript
> // Before
> expect(wrapper.text()).toContain('Member')
> // After
> expect(wrapper.text()).toContain('Socio')
> ```

---

### Step 6: Translate `UsersPage.vue`

No unit test file exists for `UsersPage.vue` (it's a page-level component with router and composable dependencies). Translation is applied directly and verified through manual testing and E2E.

- **File**: `frontend/src/pages/UsersPage.vue`
- **Action**: Translate all visible strings

**Template changes (complete list):**

| Old | New |
|---|---|
| `"User Management"` (h1) | `"Gestión de usuarios"` |
| Button `"Create User"` | `"Crear usuario"` |
| Button `"Retry"` | `"Reintentar"` |
| Column header `"Name"` | `"Nombre"` |
| Column header `"Email"` | `"Correo electrónico"` |
| Column header `"Role"` | `"Rol"` |
| Column header `"Phone"` | `"Teléfono"` |
| Column header `"Status"` | `"Estado"` |
| Column header `"Created"` | `"Alta"` |
| Column header `"Actions"` | `"Acciones"` |
| `"Active"` / `"Inactive"` (Tag) | `"Activo"` / `"Inactivo"` |
| `aria-label="View Details"` | `aria-label="Ver detalles"` |
| Dialog header `"Create New User"` | `"Crear nuevo usuario"` |

**Script changes — translate toast notification:**
```typescript
const handleRoleUpdated = (updatedUser: User) => {
  toast.add({
    severity: 'success',
    summary: 'Rol actualizado',
    detail: `El rol de ${updatedUser.firstName} ${updatedUser.lastName} ha sido actualizado a ${getRoleLabel(updatedUser.role)}`,
    life: 5000,
  })
}
```

- **Import to add**: `import { getRoleLabel } from '@/utils/user'`
- **Remove**: The local `getRoleSeverity` function in `UsersPage.vue` is used for Tag severity (not translated) — keep it but note the `getRoleLabel` helper from utils should be used for display labels instead.

> **Note**: The `getRoleSeverity()` function in `UsersPage.vue` (and `UserDetailPage.vue`) is a display helper for PrimeVue Tag severity, not a translation concern. It remains unchanged. However, consider whether to move it to `utils/user.ts` as a future refactor (out of scope for this ticket).

---

### Step 7: Translate `UserDetailPage.vue`

- **File**: `frontend/src/pages/UserDetailPage.vue`
- **Action**: Translate all visible strings

**Template changes (complete list):**

| Old | New |
|---|---|
| Button `"Back to Users"` | `"Volver a usuarios"` |
| `"User Details"` (h1) | `"Detalles del usuario"` |
| Button `"Go Back"` | `"Volver"` |
| Button `"Edit"` | `"Editar"` |
| Label `"Email"` | `"Correo electrónico"` |
| Label `"Role"` | `"Rol"` |
| Label `"Phone"` | `"Teléfono"` |
| Label `"Status"` | `"Estado"` |
| Label `"Created"` | `"Alta"` |
| Label `"Last Updated"` | `"Última actualización"` |
| Tag `"Active"` / `"Inactive"` | `"Activo"` / `"Inactivo"` |
| Card title `"Edit User"` | `"Editar usuario"` |

**Script changes — role display:**
```typescript
import { getRoleLabel } from '@/utils/user'
```

```html
<!-- Role Tag: show translated label -->
<Tag :value="getRoleLabel(selectedUser.role)" :severity="getRoleSeverity(selectedUser.role)" />
```

No new tests needed for the page (same as `UsersPage.vue` rationale above). Verified through manual testing.

---

### Step 8: Run Full Test Suite

- **Action**: Run all tests to verify no regressions

```bash
# From frontend/ directory
npx vitest run

# With coverage
npx vitest run --coverage
```

**Expected result**: All tests pass, including:
- `src/utils/user.test.ts` — 4 tests ✅
- `src/components/users/__tests__/UserForm.test.ts` — all tests ✅
- `src/components/users/__tests__/UserCard.test.ts` — all tests ✅
- `src/components/users/__tests__/UserRoleCell.test.ts` — 3 tests ✅
- `src/components/users/__tests__/UserRoleDialog.test.ts` — 5 tests ✅

---

### Step 9: Manual Verification

Navigate to `/users` and `/users/:id` and verify:

**Checklist for `/users`:**
- [ ] Page title: "Gestión de usuarios"
- [ ] "Crear usuario" button visible
- [ ] Table columns: Nombre, Correo electrónico, Rol, Teléfono, Estado, Alta, Acciones
- [ ] Status tags: "Activo" / "Inactivo"
- [ ] Roles shown as "Socio", "Junta Directiva", "Administrador" in the table
- [ ] "Crear nuevo usuario" dialog opens with Spanish form labels
- [ ] Validation messages in Spanish on empty submit
- [ ] Toast shows Spanish text after role update

**Checklist for `/users/:id`:**
- [ ] "Volver a usuarios" button
- [ ] Page title: "Detalles del usuario"
- [ ] Field labels: Correo electrónico, Rol, Teléfono, Estado, Alta, Última actualización
- [ ] Status tag: "Activo" / "Inactivo"
- [ ] Role tag: "Socio" / "Junta Directiva" / "Administrador"
- [ ] "Editar" button opens edit form with Spanish labels
- [ ] "Actualizar usuario" and "Cancelar" buttons in edit form

---

### Step 10: Update Technical Documentation

- **Action**: Review and update documentation affected by this change
- **Files to check**:
  - `ai-specs/specs/frontend-standards.mdc` — Already has a "Role Translation Helper" section with `translateRole()` example. Update the example to reference `getRoleLabel` from `utils/user.ts` (the canonical location going forward).
  - No other docs need updating (routing unchanged, no new API endpoints, no new dependencies)

```markdown
<!-- In frontend-standards.mdc, update "Role Translation Helper" section: -->
Always use the `getRoleLabel` helper from `@/utils/user`:

```typescript
import { getRoleLabel } from '@/utils/user'

// In template
{{ getRoleLabel(user.role) }}
```
```

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-i18n-user-management-frontend`
2. **Step 1** — Create `utils/user.test.ts` → create `utils/user.ts` → run tests (GREEN)
3. **Step 2** — Update `UserForm.test.ts` → update `UserForm.vue` → run tests (GREEN)
4. **Step 3** — Create `UserRoleCell.test.ts` → update `UserRoleCell.vue` → run tests (GREEN)
5. **Step 4** — Create `UserRoleDialog.test.ts` → update `UserRoleDialog.vue` → run tests (GREEN)
6. **Step 5** — Update `UserCard.test.ts` → update `UserCard.vue` → run tests (GREEN)
7. **Step 6** — Update `UsersPage.vue` (no test needed)
8. **Step 7** — Update `UserDetailPage.vue` (no test needed)
9. **Step 8** — Run full test suite, verify all passing
10. **Step 9** — Manual verification in browser
11. **Step 10** — Update documentation

---

## Testing Checklist

### Unit tests (Vitest)
- [ ] `utils/user.test.ts` — `getRoleLabel` for Admin, Board, Member, unknown role
- [ ] `UserForm.test.ts` — Spanish button labels, Spanish validation messages
- [ ] `UserCard.test.ts` — Spanish status text ("Activo"/"Inactivo"), Spanish role labels
- [ ] `UserRoleCell.test.ts` — Spanish role display for all 3 roles
- [ ] `UserRoleDialog.test.ts` — Spanish labels and button text
- [ ] All previous tests continue passing (no regressions)

### Manual / E2E
- [ ] `/users` — all strings in Spanish
- [ ] `/users/:id` — all strings in Spanish
- [ ] Create user dialog — labels and validation in Spanish
- [ ] Edit user form — labels in Spanish
- [ ] Role update dialog — all text in Spanish
- [ ] Toast notification after role update — Spanish text
- [ ] API values NOT affected (roles sent/received as English enum strings)

---

## Error Handling Patterns

No new error handling required. Existing error messages shown via `<Message severity="error">` from the composable are already using Spanish text from the backend (backend has been translated). The only frontend error messages are the client-side validation messages in `UserForm.vue`, which are updated in Step 2.

---

## UI/UX Considerations

- **Role display**: The colored badge in `UserRoleCell.vue` uses `user.role` for CSS class binding (`:class="{ 'bg-red-100': user.role === 'Admin' }"`) — **do not change** the class binding values; only change the display text via `getRoleLabel()`
- **Tag values**: PrimeVue `<Tag :value="...">` receives the translated string directly. The `severity` prop is determined by the original English role string for consistency with existing logic
- **Accessibility**: `aria-label` attributes on buttons should also be translated (done in Steps 3 and 6)
- **No responsive layout changes**: This is text-only — layout is unchanged

---

## Dependencies

No new npm packages required. All changes use existing imports:

| Package | Already installed | Used for |
|---|---|---|
| `@/types/user` (project type) | ✅ | `UserRole` type in `utils/user.ts` |
| `vitest` | ✅ | Unit tests |
| `@vue/test-utils` | ✅ | Component tests |
| `pinia` | ✅ | Store in component tests |
| `primevue` | ✅ | PrimeVue plugin in tests |

---

## Notes

1. **API values are NOT translated**: Role enum values (`Admin`, `Board`, `Member`) are always sent/received from the API in English. Only the *display labels* change.
2. **`getRoleLabel` in utils, not inline**: Centralizing in `utils/user.ts` ensures consistency across all components and makes future additions (e.g., `Guardian` role) a single-file change.
3. **Existing `getRoleSeverity` functions**: There are local `getRoleSeverity()` functions in `UsersPage.vue`, `UserDetailPage.vue`, and `UserCard.vue` that are NOT being moved in this ticket. They work correctly as-is.
4. **No `<style>` blocks**: All styling changes (if any) must use Tailwind utilities only.
5. **TypeScript**: No `any` types. `getRoleLabel` accepts `UserRole | string` for safety.
6. **`UserRoleCell` store mock in tests**: The component calls `useAuthStore()`. Tests using `createPinia()` from Pinia should initialize the store correctly. If the auth store requires specific initial state for `canEditRole()` tests, use `const authStore = useAuthStore(); authStore.user = { id: 'admin-id', role: 'Admin', ... }` after `createPinia()`.

---

## Next Steps After Implementation

1. Commit: `feat(users): translate user management section to Spanish`
2. Push branch and open PR against `main`
3. Verify CI tests pass
4. The remaining English sections of the app (home, camps, profile, auth) are tracked in the parent spec: [feat-spanish-texts-enriched.md](../merged/feat-spanish-texts/feat-spanish-texts-enriched.md)

---

## Implementation Verification

Before marking the ticket complete, verify:

- [ ] **TypeScript**: `npx vue-tsc --noEmit` — no type errors
- [ ] **Tests**: `npx vitest run` — all tests passing, no regressions
- [ ] **Lint**: `npm run lint` — no lint errors
- [ ] **Functionality**: Both pages load, CRUD operations work, role update works
- [ ] **API integration**: Role values in network requests are still `Admin`/`Board`/`Member`
- [ ] **No English UI text**: Zero English strings visible in `/users` or `/users/:id`
- [ ] **Code language**: All variable/function names remain in English
- [ ] **Documentation**: `frontend-standards.mdc` updated with canonical `getRoleLabel` reference

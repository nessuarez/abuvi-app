# Frontend Implementation Plan: feat-family-member-mobile-ux — Family Member List Mobile UX Redesign

## Overview

Replace the existing PrimeVue `DataTable` in `FamilyMemberList.vue` with a card-based list and a PrimeVue `Drawer` for member detail. The change applies to all screen sizes (no responsive conditional rendering). The props/emits contract of `FamilyMemberList` is preserved in full, so `FamilyUnitPage.vue` requires zero changes.

Architecture: Vue 3 Composition API (`<script setup lang="ts">`), PrimeVue 4 components (`Drawer`, `Button`, `Tag`, `Message`), Tailwind CSS utilities, Vitest for unit/component tests.

---

## Architecture Context

### Components involved

| Component | Role |
|---|---|
| `frontend/src/components/family-units/FamilyMemberList.vue` | **Primary file** — replaces DataTable with cards + Drawer |
| `frontend/src/components/family-units/ProfilePhotoAvatar.vue` | Reused as-is — renders avatar on cards (read-only) and inside Drawer (editable) |
| `frontend/src/views/FamilyUnitPage.vue` | Parent — **no changes needed** |

### Composables / utilities referenced (read-only, no changes)

- `@/utils/date` → `parseDateLocal`
- `@/utils/member-validation` → `getMemberDataWarnings`, `getWarningMessage`
- `@/types/family-unit` → `FamilyMemberResponse`, `FamilyRelationshipLabels`

### State management

All state is **local to `FamilyMemberList`** — no Pinia store involvement.

- `selectedMemberId: Ref<string | null>` — tracks which member's Drawer is open
- `drawerVisible: Ref<boolean>` — controls PrimeVue Drawer visibility
- `selectedMember: ComputedRef` — derived from `membersWithAge` by `selectedMemberId` (reactive to prop updates)

### Routing

No routing changes. No new pages. The Drawer is an in-place overlay.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the feature branch for this ticket.
- **Branch name**: `feature/feat-family-member-mobile-ux-frontend`
- **Implementation Steps**:
  1. Ensure the base branch (`dev`) is up to date: `git checkout dev && git pull origin dev`
  2. Create and switch to the feature branch: `git checkout -b feature/feat-family-member-mobile-ux-frontend`
  3. Verify: `git branch`
- **Notes**: Never work directly on `dev` or `main`. This must be completed before any file edits.

---

### Step 1: Write Failing Unit Tests (TDD)

- **File**: `frontend/src/components/family-units/__tests__/FamilyMemberList.spec.ts`
- **Action**: Rewrite the test file to reflect the new card + Drawer structure **before** touching the component. Tests will fail initially — that is expected.
- **Implementation Steps**:
  1. Remove the `beforeAll` block with `ResizeObserver` / `IntersectionObserver` mocks (no longer needed — DataTable is gone).
  2. Add a helper `openDrawerForMember(wrapper, memberId)` that clicks `[data-testid="member-card-{memberId}"]` and awaits `$nextTick`.
  3. Rewrite the `manageMembership` describe block:
     - `it('renders manageMembership button in drawer when canManageMemberships is true')`:
       - Mount with `canManageMemberships: true`, call `openDrawerForMember`, then assert `[data-testid="manage-membership-btn-member-1"]` exists.
     - `it('does not render manageMembership button when canManageMemberships is false')`:
       - Mount with `canManageMemberships: false`, open drawer, assert button does **not** exist.
     - `it('does not render manageMembership button when canManageMemberships is omitted')`:
       - Mount without prop, open drawer, assert button does **not** exist.
     - `it('emits manageMembership with correct member when button clicked')`:
       - Mount with `canManageMemberships: true`, open drawer, click button, assert `wrapper.emitted('manageMembership')[0][0]` matches `{ id: 'member-1', firstName: 'Ana', lastName: 'García' }`.
  4. Keep the `data completeness warnings` describe block unchanged — these tests verify card-level elements (`[data-testid="member-warning-icon"]`, `[data-testid="member-warnings-banner"]`) that are outside the Drawer.
- **Dependencies**: `vitest`, `@vue/test-utils`, `primevue/config`, `primevue/tooltip`
- **Run to confirm failures**: `cd frontend && npm run test:run -- FamilyMemberList`

---

### Step 2: Rewrite `FamilyMemberList.vue`

- **File**: `frontend/src/components/family-units/FamilyMemberList.vue`
- **Action**: Replace `DataTable` / `Column` with a card list + PrimeVue `Drawer`.

#### Script block

```typescript
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Message from 'primevue/message'
import Drawer from 'primevue/drawer'
import ProfilePhotoAvatar from '@/components/family-units/ProfilePhotoAvatar.vue'
import { FamilyRelationshipLabels } from '@/types/family-unit'
import type { FamilyMemberResponse } from '@/types/family-unit'
import { parseDateLocal } from '@/utils/date'
import { getMemberDataWarnings, getWarningMessage } from '@/utils/member-validation'
```

**Reactive state**:
```typescript
const selectedMemberId = ref<string | null>(null)
const drawerVisible = ref(false)
```

**Computed values**:
```typescript
// membersWithAge: same logic as current — adds `age` and `warnings` to each member

const selectedMember = computed(() =>
  membersWithAge.value.find((m) => m.id === selectedMemberId.value) ?? null
)
// ↑ Derived from membersWithAge (NOT a plain ref) so it stays fresh when props.members updates
```

**Helpers** (unchanged from current): `calculateAge`, `formatDate`, `getRelationshipLabel`, `isRepresentative`

**Handlers**:
```typescript
const openMemberDetail = (memberId: string) => {
  selectedMemberId.value = memberId
  drawerVisible.value = true
}

// Each action handler: closes drawer first, then emits
const handleEdit = () => { drawerVisible.value = false; emit('edit', selectedMember.value!) }
const handleDelete = () => { drawerVisible.value = false; emit('delete', selectedMember.value!) }
const handleAnonymise = () => { drawerVisible.value = false; emit('anonymisePii', selectedMember.value!) }
const handleManageMembership = () => { drawerVisible.value = false; emit('manageMembership', selectedMember.value!) }
```

#### Template block — Card List

```html
<div class="family-member-list">
  <!-- Empty state -->
  <div v-if="!loading && members.length === 0" class="text-center py-8 text-gray-500">
    No hay miembros familiares registrados
  </div>

  <!-- Loading state -->
  <div v-else-if="loading" class="flex justify-center py-8">
    <i class="pi pi-spin pi-spinner text-2xl text-primary-500"></i>
  </div>

  <!-- Card list -->
  <div v-else class="flex flex-col gap-3">
    <button
      v-for="member in membersWithAge"
      :key="member.id"
      type="button"
      :data-testid="`member-card-${member.id}`"
      class="w-full text-left bg-white border border-gray-200 rounded-xl p-4
             flex items-center gap-3 hover:bg-gray-50 active:bg-gray-100
             transition-colors cursor-pointer shadow-sm"
      @click="openMemberDetail(member.id)"
    >
      <ProfilePhotoAvatar :photo-url="member.profilePhotoUrl"
        :initials="(member.firstName?.[0] ?? '') + (member.lastName?.[0] ?? '')"
        size="sm" :editable="false" />
      <div class="flex-1 min-w-0">
        <div class="font-medium truncate flex items-center gap-1">
          {{ member.firstName }} {{ member.lastName }}
          <i v-if="member.warnings" class="pi pi-exclamation-triangle text-orange-500 text-xs"
             data-testid="member-warning-icon" />
        </div>
        <div class="flex items-center gap-2 mt-0.5 flex-wrap">
          <Tag :value="getRelationshipLabel(member.relationship)" severity="info" />
          <span class="text-sm text-gray-500">{{ member.age }} años</span>
        </div>
        <div v-if="member.userId" class="text-xs text-gray-400 mt-0.5">
          <i class="pi pi-user text-xs"></i> Usuario vinculado
        </div>
      </div>
      <i class="pi pi-chevron-right text-gray-400 flex-shrink-0"></i>
    </button>
  </div>

  <!-- Warnings banner -->
  <Message v-if="hasWarnings" severity="warn" :closable="false"
           class="mt-3" data-testid="member-warnings-banner">
    Algunos miembros adultos tienen datos incompletos ...
  </Message>
```

> **Important**: `data-testid="member-warning-icon"` stays on the **card**, not the Drawer. This keeps the existing test green without opening the drawer.

#### Template block — Drawer

```html
  <Drawer v-model:visible="drawerVisible" position="right" class="!w-full sm:!w-96">
    <template #header>
      <span class="font-semibold text-lg">Detalle del miembro</span>
    </template>

    <div v-if="selectedMember" class="flex flex-col gap-5 h-full">

      <!-- Avatar + name -->
      <div class="flex flex-col items-center gap-3 pb-4 border-b border-gray-200">
        <ProfilePhotoAvatar :photo-url="selectedMember.profilePhotoUrl"
          :initials="..." size="lg" :editable="!props.readOnly"
          :loading="props.uploadingMemberId === selectedMember.id"
          @upload="(file) => emit('uploadPhoto', selectedMember!.id, file)"
          @remove="() => emit('removePhoto', selectedMember!.id)" />
        <div class="text-center">
          <p class="text-xl font-semibold">{{ selectedMember.firstName }} {{ selectedMember.lastName }}</p>
          <Tag :value="getRelationshipLabel(selectedMember.relationship)" severity="info" class="mt-1" />
        </div>
      </div>

      <!-- Fields -->
      <div class="flex flex-col gap-4 text-sm">
        <!-- pi-calendar / pi-id-card / pi-envelope / pi-phone / pi-user rows -->
        <!-- Each row: v-if guard (except date of birth — always shown) -->
      </div>

      <!-- Inline warning -->
      <Message v-if="selectedMember.warnings && !props.readOnly" severity="warn" :closable="false">
        {{ getWarningMessage(selectedMember.warnings) }}
      </Message>

      <!-- Actions -->
      <div class="flex flex-col gap-2 pt-4 border-t border-gray-200 mt-auto">
        <Button v-if="props.canManageMemberships"
          icon="pi pi-id-card" label="Gestionar membresía"
          severity="secondary" outlined class="w-full justify-start"
          :data-testid="`manage-membership-btn-${selectedMember.id}`"
          @click="handleManageMembership" />
        <Button v-if="!props.readOnly"
          icon="pi pi-pencil" label="Editar"
          severity="info" class="w-full justify-start"
          @click="handleEdit" />
        <Button v-if="!props.readOnly || props.isAdminOrBoard"
          icon="pi pi-trash" label="Eliminar"
          severity="danger" outlined class="w-full justify-start"
          :disabled="isRepresentative(selectedMember)"
          v-tooltip.top="isRepresentative(selectedMember) ? 'No se puede eliminar al representante' : undefined"
          @click="handleDelete" />
        <Button v-if="props.isAdminOrBoard"
          icon="pi pi-eraser" label="Anonimizar datos (RGPD)"
          severity="warning" outlined class="w-full justify-start"
          :disabled="isRepresentative(selectedMember)"
          v-tooltip.top="isRepresentative(selectedMember) ? 'No se puede anonimizar al representante' : undefined"
          @click="handleAnonymise" />
      </div>
    </div>
  </Drawer>
</div>
```

- **Implementation Notes**:
  - Remove all `DataTable`, `Column` imports — they are unused after this change.
  - `Drawer` is imported from `primevue/drawer` (available in PrimeVue 4.x).
  - The `hasWarnings` computed must guard with `!props.readOnly` (same as current).
  - Warning icon on the card: no `v-if="!props.readOnly"` guard — the spec says "only when not `readOnly`" but looking at the current code, it's always shown. **Clarify with product**: current implementation shows the icon regardless of readOnly. Keep consistent with current behavior (show icon always) and only suppress the **banner** in readOnly mode.

---

### Step 3: Run Tests and Fix

- **Action**: Run unit tests and fix any failures.
- **Command**: `cd frontend && npm run test:run -- FamilyMemberList`
- **Expected result**: All tests green.
- **Common issues**:
  - If `Drawer` renders lazily (content not in DOM until first opened), the `openDrawerForMember` helper correctly triggers the card click first.
  - If PrimeVue `Drawer` requires `appendTo="self"` in test environment to avoid teleporting content outside the wrapper — add `:append-to="'self'"` prop to `<Drawer>` in the component if tests cannot find the button.

---

### Step 4: Manual Verification in Browser

- **Action**: Start the dev server and verify the feature visually.
- **Command**: `cd frontend && npm run dev`
- **Checklist**:
  - [ ] Navigate to "Mi Unidad Familiar"
  - [ ] Members appear as cards (avatar, name, tag, age, chevron)
  - [ ] Warning icon shows on card for adults with missing data
  - [ ] Tap/click a card → Drawer slides in from the right
  - [ ] Drawer shows: avatar (editable), name, relationship tag, all field rows with icons
  - [ ] Drawer shows inline warning for incomplete adult member
  - [ ] "Editar" button closes Drawer and opens the edit dialog
  - [ ] "Eliminar" button closes Drawer and shows confirm dialog
  - [ ] "Gestionar membresía" shows only for board users
  - [ ] "Anonimizar" shows only for admin/board users
  - [ ] Representative cannot be deleted/anonymised (buttons disabled with tooltip)
  - [ ] Photo upload works from Drawer avatar (editable mode)
  - [ ] `FamilyUnitPage.vue` shows no regressions (family unit card, add member button, bulk membership)
  - [ ] Warning banner appears below cards for incomplete adult data
  - [ ] Empty state message shown when no members
  - [ ] Loading spinner shown during fetch

---

### Step 5: Update Technical Documentation

- **Action**: Update `ai-specs/specs/frontend-standards.mdc` if any new pattern was introduced (e.g. Drawer usage pattern). No API spec changes (no backend changes).
- **Implementation Steps**:
  1. Review `frontend-standards.mdc` — check if a "Drawer" usage pattern section exists. If not, add a brief note under "PrimeVue Integration" documenting the card + Drawer pattern for list → detail navigation.
  2. No changes to `backend-standards.mdc`, `api-spec.yml`, or router docs.
- **Notes**: Documentation must be written in English.

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Write failing tests (TDD: tests first)
3. Step 2 — Rewrite `FamilyMemberList.vue`
4. Step 3 — Run tests, fix failures
5. Step 4 — Manual browser verification
6. Step 5 — Update documentation

---

## Testing Checklist

### Vitest Unit Tests (`FamilyMemberList.spec.ts`)

- [ ] `manageMembership` button renders in Drawer when `canManageMemberships: true`
- [ ] `manageMembership` button absent when `canManageMemberships: false`
- [ ] `manageMembership` button absent when `canManageMemberships` omitted
- [ ] `manageMembership` emit fires with correct member after open + click
- [ ] Warning icon on card for adult with missing DNI/email
- [ ] No warning icon for adult with complete data
- [ ] No warning icon for minor
- [ ] Warning banner shown for incomplete adult data
- [ ] Warning banner hidden for complete data
- [ ] Warning banner hidden in `readOnly` mode

### Manual / Browser

- [ ] Drawer opens and closes correctly
- [ ] All action buttons fire correct events
- [ ] Photo upload reactive in Drawer (props update reflected without reopening)
- [ ] Representative protection works (buttons disabled)
- [ ] No regressions in `FamilyUnitPage`

---

## Error Handling Patterns

No new API calls are introduced. Error handling remains in `useFamilyUnits.ts` (unchanged). The `FamilyMemberList` component is purely presentational — it receives state and emits events upward to `FamilyUnitPage`, which handles API errors and toast notifications.

---

## UI/UX Considerations

| Element | PrimeVue Component | Tailwind Notes |
|---|---|---|
| Card | `<button type="button">` | `rounded-xl`, `shadow-sm`, `hover:bg-gray-50` |
| Avatar on card | `ProfilePhotoAvatar` (size="sm", editable=false) | — |
| Relationship label | `Tag` (severity="info") | — |
| Drawer panel | `Drawer` (position="right") | `!w-full sm:!w-96` |
| Avatar in drawer | `ProfilePhotoAvatar` (size="lg", editable=!readOnly) | Centered |
| Field rows | Plain `<div>` with icon + label + value | `gap-4`, label in `text-xs uppercase tracking-wide text-gray-400` |
| Action buttons | `Button` (full-width) | `w-full justify-start` |
| Inline warning | `Message` (severity="warn") | — |
| Banner warning | `Message` (severity="warn", data-testid) | `mt-3` |

### Accessibility

- Cards are `<button type="button">` — keyboard focusable, Enter/Space triggers tap.
- Drawer action buttons have descriptive `label` attributes — screen-reader friendly.
- `aria-label` on avatar upload button (handled inside `ProfilePhotoAvatar`).

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `primevue` | `^4.5.4` (already installed) | `Drawer`, `Button`, `Tag`, `Message` |
| `primeicons` | `^7.0.0` (already installed) | `pi-chevron-right`, `pi-calendar`, etc. |

No new npm packages required.

---

## Notes

- **English only** in code: variables, functions, comments, test names.
- **Spanish only** in user-facing text: labels, messages, tooltips.
- **No `any`**: `selectedMember` typed as `ComputedRef<(FamilyMemberResponse & { age: number; warnings: ... }) | null>`.
- **No `DataTable`/`Column` imports** in the final file — remove them entirely.
- **Branch target for PR**: `dev` (not `main`). See project memory.
- **`FamilyUnitPage.vue`** must NOT be modified — the props/emits contract is unchanged.
- **Warning icon guard**: The spec says warning icon only when not `readOnly`. Verify with the current behavior; if the icon is shown in readOnly mode in production today, keep that behavior and note it in the PR.

---

## Next Steps After Implementation

1. Open PR against `dev` branch following commit conventions.
2. Link PR to Trello card if applicable.
3. QA review on mobile viewport (Chrome DevTools device emulation minimum).

---

## Implementation Verification

- [ ] **Code Quality**: `<script setup lang="ts">`, strict types, no `any`, no `DataTable` imports left
- [ ] **Functionality**: Cards render, Drawer opens/closes, all actions emit correctly
- [ ] **Testing**: All Vitest tests green (`npm run test:run`)
- [ ] **Type check**: `npx vue-tsc --noEmit` passes with no errors
- [ ] **Lint**: `npm run lint` passes
- [ ] **Integration**: `FamilyUnitPage.vue` unchanged and functional
- [ ] **Documentation**: `frontend-standards.mdc` updated if new patterns introduced

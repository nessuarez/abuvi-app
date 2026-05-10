# Frontend Implementation Plan: feat-family-needs-tagging — Accommodation Needs Tagging (Admin/Board)

## Overview

This feature adds an "Alojamiento (Junta)" section to the registration detail page, visible only to Admin and Board roles. It allows the Board to:

1. Tag structured accommodation needs using the `AccommodationFeature` catalog (Ticket A).
2. Add/edit internal accommodation notes (never shown to Member).
3. Link friend registrations for the assignment algorithm (bidirectional friend links).

Architecture approach: extend the existing `RegistrationDetailPage.vue` with a conditionally rendered section (`v-if="isAdminOrBoard"`), backed by a new composable `useRegistrationAccommodationTagging.ts` and two new sub-components.

---

## Architecture Context

### Components involved

| File | Action |
|------|--------|
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Modify — add admin tagging section below the existing "Información adicional" card |
| `frontend/src/components/admin/registration-accommodation-needs/RegistrationAccommodationNeeds.vue` | Create — multiselect panel for feature tagging |
| `frontend/src/components/admin/registration-accommodation-needs/RegistrationFriendLinks.vue` | Create — friend-link picker and list |

### Composables

| File | Action |
|------|--------|
| `frontend/src/composables/useRegistrationAccommodationTagging.ts` | Create — all API calls for accommodation needs, notes, and friend links |

### Types

| File | Action |
|------|--------|
| `frontend/src/types/registration.ts` | Modify — add DTOs and extend `RegistrationResponse` with optional admin fields |

### State management

No Pinia store needed. All state is local to `RegistrationDetailPage.vue` and passed as props/emits to child components. `useRegistrationAccommodationTagging` manages its own loading/error reactive refs.

### Routing

No routing changes needed. The feature lives inside the existing `/registrations/:id` route.

### Dependencies

| Composable | Usage |
|------------|-------|
| `useAccommodationFeatures` | Fetch active `AccommodationFeature` catalog for the multiselect |
| `useAdminRegistrations` | Fetch other registrations in the same camp edition (for friend-link picker) |

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/feat-family-needs-tagging-frontend`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-family-needs-tagging-frontend`
  3. `git branch` — verify you are on the new branch
- **Notes**: Base off `dev`, not `main`. The backend work is already merged into `dev`; `main` would be behind.

---

### Step 1: Add TypeScript Types in `registration.ts`

- **File**: `frontend/src/types/registration.ts`
- **Action**: Add new DTOs and extend `RegistrationResponse`

**New interfaces to add (append to the file):**

```typescript
// === Accommodation Needs Tagging (Admin/Board) ===

export interface AccommodationNeedResponse {
  featureId: string
  featureName: string
  featureCategory: string
  taggedByUserId: string | null
  createdAt: string
}

export interface FriendLinkResponse {
  linkedRegistrationId: string
  linkedFamilyName: string
  createdByUserId: string | null
  createdAt: string
}

// Request types
export interface UpdateAccommodationNeedsRequest {
  featureIds: string[]
}

export interface UpdateAccommodationNotesRequest {
  accommodationInternalNotes: string | null
}

export interface UpdateFriendLinksRequest {
  linkedRegistrationIds: string[]
}

// Response wrappers
export interface AccommodationNeedsResponse {
  registrationId: string
  needs: AccommodationNeedResponse[]
}

export interface AccommodationNotesResponse {
  registrationId: string
  accommodationInternalNotes: string | null
  updatedAt: string
}

export interface FriendLinksResponse {
  registrationId: string
  friendLinks: FriendLinkResponse[]
}
```

**Extend `RegistrationResponse`** — add three optional fields at the end of the interface:

```typescript
// Admin/Board only — undefined (absent) for Member responses
accommodationInternalNotes?: string | null
accommodationNeeds?: AccommodationNeedResponse[]
friendLinks?: FriendLinkResponse[]
```

- **Implementation Notes**: These are optional (`?`) because the backend only includes them for Admin/Board. Components must default to `[]` / `null` when absent.

---

### Step 2: Create `useRegistrationAccommodationTagging.ts`

- **File**: `frontend/src/composables/useRegistrationAccommodationTagging.ts`
- **Action**: Create a new composable with all API calls for the three sub-features

**Signature:**

```typescript
export function useRegistrationAccommodationTagging() {
  const needs = ref<AccommodationNeedResponse[]>([])
  const friendLinks = ref<FriendLinkResponse[]>([])
  const internalNotes = ref<string | null>(null)

  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)
  const saveError = ref<string | null>(null)

  async function fetchNeeds(registrationId: string): Promise<void>
  async function updateNeeds(registrationId: string, featureIds: string[]): Promise<AccommodationNeedsResponse | null>
  async function updateNotes(registrationId: string, notes: string | null): Promise<AccommodationNotesResponse | null>
  async function fetchFriendLinks(registrationId: string): Promise<void>
  async function updateFriendLinks(registrationId: string, linkedRegistrationIds: string[]): Promise<FriendLinksResponse | null>

  return { needs, friendLinks, internalNotes, loading, saving, error, saveError,
           fetchNeeds, updateNeeds, updateNotes, fetchFriendLinks, updateFriendLinks }
}
```

**Implementation details:**

- `fetchNeeds`: `GET /registrations/{id}/accommodation-needs` → stores result in `needs`
- `updateNeeds`: `PUT /registrations/{id}/accommodation-needs` with `{ featureIds }` → updates `needs`, returns response
- `updateNotes`: `PATCH /registrations/{id}/accommodation-notes` with `{ accommodationInternalNotes }` → updates `internalNotes`, returns response
- `fetchFriendLinks`: `GET /registrations/{id}/friend-links` → stores in `friendLinks`
- `updateFriendLinks`: `PUT /registrations/{id}/friend-links` with `{ linkedRegistrationIds }` → updates `friendLinks`, returns response

**Error extraction** — use the same pattern as `useAccommodationFeatures.ts`:

```typescript
type ApiErrorShape = { response?: { data?: { error?: { message?: string } }; status?: number } }
const extractError = (err: unknown): string =>
  (err as ApiErrorShape)?.response?.data?.error?.message ?? 'Ha ocurrido un error inesperado'
```

- **Dependencies**: `api` from `@/utils/api`, `ApiResponse` from `@/types/api`, new types from `@/types/registration`

---

### Step 3: Create `RegistrationAccommodationNeeds.vue`

- **File**: `frontend/src/components/admin/registration-accommodation-needs/RegistrationAccommodationNeeds.vue`
- **Action**: Panel for viewing/editing structured accommodation needs tags

**Props:**

```typescript
defineProps<{
  registrationId: string
  initialNeeds: AccommodationNeedResponse[]  // from RegistrationResponse.accommodationNeeds
  initialNotes: string | null                // from RegistrationResponse.accommodationInternalNotes
  specialNeeds: string | null               // read-only display of family's free-text
  campatesPreference: string | null         // read-only display of family's free-text
}>()
```

**Emits:**

```typescript
defineEmits<{
  (e: 'updated', needs: AccommodationNeedResponse[]): void
}>()
```

**Internal state:**

```typescript
const { features, fetchFeatures } = useAccommodationFeatures()
const { needs, saving, saveError, updateNeeds, updateNotes } = useRegistrationAccommodationTagging()
const selectedFeatureIds = ref<string[]>([])  // bound to MultiSelect
const isEditingTags = ref(false)
const isEditingNotes = ref(false)
const editNotes = ref<string>('')
```

**Template structure:**

```
<div class="rounded-lg border border-indigo-100 bg-indigo-50/30 p-4">
  <h3>Alojamiento (Junta)</h3>

  <!-- Sub-section 1: Read-only family free-text fields -->
  <section "Texto libre (familia)">
    <dt>Necesidades especiales</dt> <dd>{{ specialNeeds ?? '—' }}</dd>
    <dt>Preferencia de compañeros</dt> <dd>{{ campatesPreference ?? '—' }}</dd>
  </section>

  <!-- Sub-section 2: Editable structured tags -->
  <section "Etiquetas estructuradas">
    [read view: Chip list of featureName per need, or "Sin etiquetas" italic]
    [edit view: MultiSelect bound to selectedFeatureIds, options from features catalog]
    [Edit / Save / Cancel buttons]
  </section>

  <!-- Sub-section 3: Editable internal notes -->
  <section "Notas internas (Junta)">
    [read view: notes text or "Sin notas" italic]
    [edit view: Textarea with char counter {n}/4000]
    [Edit / Save / Cancel buttons]
    [saveError Message]
  </section>
</div>
```

**Key UX details:**

- Use PrimeVue `MultiSelect` with `optionLabel="name"` and `optionValue="id"` bound to `selectedFeatureIds`
- Call `fetchFeatures(true)` (activeOnly) on `onMounted` to populate options
- In read mode, display tags as PrimeVue `Chip` or small badges with the feature name
- On enter tag-edit mode: set `selectedFeatureIds` from current `needs.map(n => n.featureId)`
- On save tags: call `updateNeeds(registrationId, selectedFeatureIds)` → emit `updated` on success, show toast
- Max 20 items validated client-side (disable adding more when 20 selected)
- On enter notes-edit mode: set `editNotes` from current `internalNotes ?? ''`
- On save notes: call `updateNotes(registrationId, editNotes.trim() || null)` → show toast; character counter turns red > 3800

---

### Step 4: Create `RegistrationFriendLinks.vue`

- **File**: `frontend/src/components/admin/registration-accommodation-needs/RegistrationFriendLinks.vue`
- **Action**: Panel for viewing and editing friend registration links

**Props:**

```typescript
defineProps<{
  registrationId: string
  campEditionId: string
  initialFriendLinks: FriendLinkResponse[]
}>()
```

**Emits:**

```typescript
defineEmits<{
  (e: 'updated', links: FriendLinkResponse[]): void
}>()
```

**Internal state:**

```typescript
const { registrations: editionRegistrations, fetchAdminRegistrations } = useAdminRegistrations()
const { friendLinks, saving, saveError, updateFriendLinks } = useRegistrationAccommodationTagging()
const selectedLinkedIds = ref<string[]>([])  // IDs of linked registrations
const isEditing = ref(false)
```

**Template structure:**

```
<div class="rounded-lg border border-teal-100 bg-teal-50/30 p-4">
  <h3>Familias amigas</h3>

  <!-- Read view: list of linked families -->
  <ul v-if="!isEditing">
    <li v-for="link in friendLinks">
      {{ link.linkedFamilyName }} — {{ formatDate(link.createdAt) }}
    </li>
    <li v-if="!friendLinks.length" class="text-gray-400 italic">Sin vínculos</li>
  </ul>

  <!-- Edit view: MultiSelect of other registrations in same edition -->
  <div v-else>
    <MultiSelect
      v-model="selectedLinkedIds"
      :options="availableRegistrations"
      option-label="familyName"
      option-value="id"
      filter
      placeholder="Buscar familia..."
    />
    <p class="text-xs text-gray-500 mt-1">Máx. 10 familias</p>
  </div>

  [Edit / Save / Cancel buttons]
  [saveError Message]
</div>
```

**Key UX details:**

- On entering edit mode: call `fetchAdminRegistrations(campEditionId, { pageSize: 200 })` to load all registrations in the edition; set `selectedLinkedIds` from current `friendLinks.map(l => l.linkedRegistrationId)`
- `availableRegistrations` computed: filter out `registrationId` itself from `editionRegistrations`, map to `{ id, familyName: item.familyUnit.name }`
- On save: call `updateFriendLinks(registrationId, selectedLinkedIds)` → emit `updated` on success, show toast
- Max 10 items validated client-side (show warning when limit reached)
- On save error with code `SAME_EDITION_REQUIRED` or `NO_SELF_LINK`, show descriptive toast message

---

### Step 5: Integrate into `RegistrationDetailPage.vue`

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Mount the two new components inside a new admin-only section, and sync data from `RegistrationResponse`

**New imports to add:**

```typescript
import RegistrationAccommodationNeeds from '@/components/admin/registration-accommodation-needs/RegistrationAccommodationNeeds.vue'
import RegistrationFriendLinks from '@/components/admin/registration-accommodation-needs/RegistrationFriendLinks.vue'
import type { AccommodationNeedResponse, FriendLinkResponse } from '@/types/registration'
```

**New reactive refs to add:**

```typescript
// Initialized from registration.value after fetch
const localAccommodationNeeds = ref<AccommodationNeedResponse[]>([])
const localFriendLinks = ref<FriendLinkResponse[]>([])
```

**In `onMounted`** — after `getRegistrationById`, populate locals:

```typescript
localAccommodationNeeds.value = registration.value?.accommodationNeeds ?? []
localFriendLinks.value = registration.value?.friendLinks ?? []
```

**Template insertion** — add after the "Preferencias de alojamiento" card and before the "Desglose de precio" section:

```html
<!-- Admin/Board: Accommodation tagging (Junta) -->
<template v-if="isAdminOrBoard">
  <RegistrationAccommodationNeeds
    :registration-id="registrationId"
    :initial-needs="localAccommodationNeeds"
    :initial-notes="registration.accommodationInternalNotes ?? null"
    :special-needs="registration.specialNeeds"
    :campates-preference="registration.campatesPreference"
    class="mb-6"
    @updated="localAccommodationNeeds = $event"
  />
  <RegistrationFriendLinks
    :registration-id="registrationId"
    :camp-edition-id="registration.campEdition.id"
    :initial-friend-links="localFriendLinks"
    class="mb-6"
    @updated="localFriendLinks = $event"
  />
</template>
```

---

### Step 6: Write Vitest Unit Tests

- **Files**:
  - `frontend/src/composables/__tests__/useRegistrationAccommodationTagging.test.ts`
  - `frontend/src/components/admin/registration-accommodation-needs/__tests__/RegistrationAccommodationNeeds.test.ts`
  - `frontend/src/components/admin/registration-accommodation-needs/__tests__/RegistrationFriendLinks.test.ts`

**Composable tests** — mock `api` from `@/utils/api`, test:

- `fetchNeeds` populates `needs` ref
- `updateNeeds` calls `PUT` and returns response
- `updateNotes` calls `PATCH` and updates `internalNotes`
- `fetchFriendLinks` populates `friendLinks`
- `updateFriendLinks` calls `PUT` and returns response
- Error path: `saveError` is set on API failure

**Component tests** — use `@vue/test-utils` with PrimeVue installed, stub `useAccommodationFeatures` and `useRegistrationAccommodationTagging`, test:

- `RegistrationAccommodationNeeds`: renders free-text fields; toggle edit shows MultiSelect; save calls `updateNeeds`; emits `updated` on success
- `RegistrationFriendLinks`: renders linked families; edit mode shows MultiSelect; blocks self-link; save calls `updateFriendLinks`

---

### Step 7: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/admin-registration-accommodation-needs.cy.ts`
- **Fixtures**: Create `cypress/fixtures/accommodation-needs.json`, `friend-links.json`

**Critical flows to cover:**

1. Admin opens a registration detail page → "Alojamiento (Junta)" section is visible.
2. Admin clicks "Editar" on needs → MultiSelect appears with catalog features → selects 2 features → saves → chips shown.
3. Admin edits internal notes → types text → saves → notes displayed.
4. Admin clicks "Editar" on friend links → MultiSelect shows other families in same edition → selects one → saves → family name listed.
5. Member opens the same registration → the admin section is NOT rendered.
6. Save errors (400 from backend) → toast with error message shown.

Use `cy.intercept()` to mock all API calls. Guard against the section rendering for Member with role-based fixture.

---

### Step 8: Update Technical Documentation

- **Action**: Review and update documentation after implementation
- **Implementation Steps**:
  1. **Review changes**: new composable, two components, type extensions, view modification
  2. **Update `frontend-standards.mdc`** if any new pattern is introduced (e.g., multi-concern composable organization)
  3. **No routing changes** → no routing docs update needed
  4. **No new npm packages** → no dependency docs update needed
  5. **Verify** all changes are consistent with existing documentation structure
- **References**: `ai-specs/specs/documentation-standards.mdc`
- **Notes**: This step is MANDATORY before considering the implementation complete.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-family-needs-tagging-frontend`
2. **Step 1** — Add TypeScript types to `registration.ts`
3. **Step 2** — Create `useRegistrationAccommodationTagging.ts`
4. **Step 3** — Create `RegistrationAccommodationNeeds.vue`
5. **Step 4** — Create `RegistrationFriendLinks.vue`
6. **Step 5** — Integrate components into `RegistrationDetailPage.vue`
7. **Step 6** — Write Vitest unit tests
8. **Step 7** — Write Cypress E2E tests
9. **Step 8** — Update technical documentation

---

## Testing Checklist

- [ ] `RegistrationAccommodationNeeds` renders special needs and campates preference text (read-only)
- [ ] MultiSelect for features shows only `isActive = true` features
- [ ] Saving needs calls `PUT /registrations/{id}/accommodation-needs` and updates the chip list
- [ ] Empty `featureIds` (clear all) works correctly
- [ ] `accommodationInternalNotes` field saves via `PATCH` endpoint
- [ ] Notes longer than 4000 chars shows validation error (client-side counter + API 400 guard)
- [ ] `RegistrationFriendLinks` loads edition registrations on entering edit mode
- [ ] Cannot link to own registration (filter in `availableRegistrations` computed)
- [ ] `PUT /registrations/{id}/friend-links` with valid IDs saves and shows linked family names
- [ ] Removing all links sends `[]` and clears the list
- [ ] Section NOT rendered for Member role
- [ ] Toast shown on save success and save error
- [ ] Vitest tests pass for composable (all 5 methods + error path)
- [ ] Cypress E2E covers 6 critical flows listed in Step 7

---

## Error Handling Patterns

- **Loading states**: `loading` ref in composable; components show `ProgressSpinner` or disable buttons while loading
- **Save errors**: `saveError` ref populated on API failure; shown via PrimeVue `Message` component with `severity="error"` inside the component, and also via `useToast` for transient feedback
- **Validation errors (400)**: extract `error.message` from `ApiResponse.error.message` — covers `VALIDATION_ERROR`, `SAME_EDITION_REQUIRED`, `NO_SELF_LINK`
- **403 Forbidden**: Axios interceptor (already set up in `src/lib/axios.ts`) handles auth redirect; these endpoints are admin-only so UI never calls them for Members (conditional rendering)

---

## UI/UX Considerations

- **Section visual style**: Use a distinct border color (`border-indigo-200 bg-indigo-50/30`) to visually separate the admin tagging section from user-visible content — consistent with the `border-orange-100 bg-orange-50` pattern used for the admin notification banner
- **Feature chips**: Use PrimeVue `Chip` with a small icon from the `AccommodationFeature.icon` field
- **MultiSelect for features**: `filter` prop enabled for text search; `showClear` to reset all; `maxSelectedLabels={3}` to avoid overflow
- **Friend-links MultiSelect**: `filter` prop enabled for searching by family name; show a hint "Máx. 10 familias" below the field
- **Character counter for notes**: Show `{length}/4000` below the textarea, color turns red when > 3800
- **Loading skeleton**: While `loading=true` on initial fetch, show a subtle skeleton (or the parent `ProgressSpinner` already handles this since the entire page waits for the registration fetch)
- **Responsive**: The section uses `w-full` and `flex-col` layout; no horizontal overflow expected on mobile
- **Accessibility**: `aria-label` on MultiSelect fields; `id`/`for` pairs on textarea labels; `data-testid` attributes on key interactive elements for Cypress

---

## Dependencies

- **No new npm packages** — all functionality covered by existing PrimeVue + Tailwind + existing composables
- **PrimeVue components used**:
  - `MultiSelect` — feature tag selection and friend-link selection
  - `Chip` — display selected tags in read mode
  - `Textarea` — internal notes input
  - `Button` — edit/save/cancel actions
  - `Message` — inline error display
  - `ProgressSpinner` — loading state (reused from parent)

---

## Notes

- **Language**: All code in English; user-facing strings in Spanish (consistent with existing UI)
- **No `any` types**: Use proper typing — `unknown` + type guards or `ApiErrorShape` pattern
- **`<script setup lang="ts">`**: Mandatory for all components
- **No `<style>` blocks**: Tailwind utilities only
- **Backend already implemented**: The branch `feature/feat-family-needs-tagging-backend` is merged as of commit `7e76310`. All API endpoints are live.
- **`RegistrationResponse` extension**: The backend returns `accommodationNeeds`, `friendLinks`, and `accommodationInternalNotes` only for Admin/Board. They are absent (not `null`) in Member responses. Use `?? []` / `?? null` when accessing from `RegistrationResponse`.
- **Bidirectionality of friend links**: The backend manages reciprocal rows automatically. The frontend only sends the IDs it wants linked from the current registration. The GET endpoint returns the union of both directions.
- **`AccommodationFeature.applicabilityLevel`**: Not relevant for this ticket — the registration tagging uses any active feature regardless of applicability level (that constraint applies to zone/accommodation assignments, not registration tagging).

---

## Next Steps After Implementation

- **Integration test with backend**: Run the full dev stack and verify the admin tagging section works end-to-end against the real API
- **Ticket C (Encaje de Bolillos)**: The assignment dashboard will consume `accommodationNeeds` and `friendLinkRegistrationIds` from the assignment-status endpoint — this ticket's frontend work is prerequisite for validating that data flow
- **Notify QA**: The admin-only visibility rule for `accommodationInternalNotes` must be explicitly validated in QA with Member-role login

---

## Implementation Verification

- [ ] **Code Quality**: TypeScript strict — no `any`, all components use `<script setup lang="ts">`
- [ ] **Functionality**: `RegistrationAccommodationNeeds` and `RegistrationFriendLinks` render correctly in admin context; section hidden for Member
- [ ] **Testing**: Vitest unit tests for composable + Cypress E2E for 6 critical flows
- [ ] **Integration**: Composable calls correct endpoints (`PUT`/`PATCH`/`GET`) with correct payloads
- [ ] **Documentation**: `frontend-standards.mdc` updated if new patterns introduced

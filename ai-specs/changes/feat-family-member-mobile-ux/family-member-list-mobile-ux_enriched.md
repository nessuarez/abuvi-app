# Enriched User Story: Family Member List — Mobile UX Redesign

## Problem Statement

The current family member list in `FamilyUnitPage` uses a PrimeVue `DataTable` with horizontal scroll (`responsiveLayout="scroll"`). On mobile devices this creates a poor UX: users are unaware they can scroll horizontally, the action buttons (edit, delete, manage membership) are hidden off-screen, and the dense table format is hard to read with small text. Families typically have fewer than 6 members, making a paginated table unnecessarily complex.

## User Story

**As a** family representative using the app on a mobile device,
**I want** to see my family members as visual cards with name and basic info,
**So that** I can quickly identify each person and access their details and actions with a single tap without needing to discover or use horizontal scroll.

---

## Decisions Made

| Decision | Choice | Reason |
|---|---|---|
| Layout format | Cards for **all screen sizes** (replace table) | < 6 members on average; table adds no value; simpler to maintain one view |
| Detail interaction | PrimeVue **Drawer** (`position="right"`) | Native-feeling slide-in panel; full-screen on mobile, side panel on desktop |
| Photo upload access | Moved inside Drawer header area | Keeps card minimal; upload is not a frequent action |

---

## Acceptance Criteria

### Card List (replaces DataTable)

- [ ] Each member is shown as a tappable card with:
  - `ProfilePhotoAvatar` (read-only, `size="sm"`)
  - Full name (`firstName lastName`)
  - `Tag` with relationship label (e.g. "Padre/Madre")
  - Age in years (e.g. "42 años")
  - Warning icon (`pi-exclamation-triangle`, orange) if `getMemberDataWarnings` returns non-null — only when not `readOnly`
  - "Usuario vinculado" indicator if `member.userId` is set
  - Chevron icon (`pi-chevron-right`) on the right as affordance
- [ ] Cards are stacked vertically (single column), `flex-col gap-3`
- [ ] Empty state: "No hay miembros familiares registrados" (centered, gray)
- [ ] Loading state: spinner (`pi-spin pi-spinner`)
- [ ] Warning banner below the list (same as current) when any adult member has incomplete data and not `readOnly`
- [ ] Each card has `data-testid="member-card-{member.id}"` for testing

### Drawer (member detail)

- [ ] Opens on card tap; `position="right"`, full-width on mobile (`!w-full`), `sm:!w-96` on tablet+
- [ ] Header: "Detalle del miembro"
- [ ] Content:
  - **Avatar section**: `ProfilePhotoAvatar` (`size="lg"`, editable when not `readOnly`), full name below it, relationship `Tag`
  - **Fields section** (icon + label + value rows):
    - `pi-calendar` — "Fecha de nacimiento": formatted date + age
    - `pi-id-card` — "Documento": `documentNumber` (only if set)
    - `pi-envelope` — "Email": `email` (only if set)
    - `pi-phone` — "Teléfono": `phone` (only if set)
    - `pi-user` — "Cuenta": "Usuario vinculado" (only if `userId` is set)
  - **Inline warning** (`Message` severity="warn") if selected member has warnings and not `readOnly`
  - **Action buttons** (full-width, bottom of drawer, separated by top border):
    - "Gestionar membresía" (`pi-id-card`, secondary, outlined) — only if `canManageMemberships`; `data-testid="manage-membership-btn-{id}"`
    - "Editar" (`pi-pencil`, info) — only if not `readOnly`
    - "Eliminar" (`pi-trash`, danger, outlined) — only if not `readOnly` or `isAdminOrBoard`; disabled + tooltip if representative
    - "Anonimizar datos (RGPD)" (`pi-eraser`, warning, outlined) — only if `isAdminOrBoard`; disabled + tooltip if representative
- [ ] Clicking any action button: closes Drawer, then emits the corresponding event
- [ ] Photo upload/remove from Drawer emits `uploadPhoto` / `removePhoto` (same events as current)
- [ ] `selectedMember` is a `computed` derived from `membersWithAge` (not a plain `ref`) so it stays reactive when props update (e.g. after photo upload)

---

## Files to Modify

| File | Change |
|---|---|
| `frontend/src/components/family-units/FamilyMemberList.vue` | Replace `DataTable`/`Column` with card list + PrimeVue `Drawer` |
| `frontend/src/components/family-units/__tests__/FamilyMemberList.spec.ts` | Update tests: open drawer before checking drawer-internal elements |

**No changes needed** to:

- `FamilyUnitPage.vue` — props/emits interface unchanged
- `useFamilyUnits.ts` — data layer unchanged
- Backend — no API changes

---

## Component Props & Emits (unchanged)

```typescript
// Props
members: FamilyMemberResponse[]
loading?: boolean
canManageMemberships?: boolean
readOnly?: boolean
uploadingMemberId?: string | null
isAdminOrBoard?: boolean
representativeUserId?: string

// Emits
edit: [member: FamilyMemberResponse]
delete: [member: FamilyMemberResponse]
anonymisePii: [member: FamilyMemberResponse]
manageMembership: [member: FamilyMemberResponse]
uploadPhoto: [memberId: string, file: File]
removePhoto: [memberId: string]
```

---

## Key Implementation Detail: Reactive Selected Member

Use `selectedMemberId: Ref<string | null>` + a `computed` to derive the full member object. This ensures the Drawer content stays fresh after async operations (photo upload, etc.) without needing manual state sync:

```typescript
const selectedMemberId = ref<string | null>(null)

const selectedMember = computed(() =>
  membersWithAge.value.find((m) => m.id === selectedMemberId.value) ?? null
)
```

---

## Test Strategy

Tests use `@vue/test-utils` + Vitest. Since the Drawer content is only in the DOM after opening, tests that interact with drawer-internal elements (e.g. `manage-membership-btn`) must first trigger a click on the card:

```typescript
const openDrawerForMember = async (wrapper, memberId) => {
  await wrapper.find(`[data-testid="member-card-${memberId}"]`).trigger('click')
  await wrapper.vm.$nextTick()
}
```

Tests to cover:

- `manageMembership` button visibility in drawer (true/false/omitted `canManageMemberships`)
- `manageMembership` emit with correct member after opening drawer and clicking
- Warning icon on card (adult missing DNI/email → shown; complete data → hidden; minor → hidden)
- Warning banner (incomplete adult → shown; complete → hidden; `readOnly` → hidden)

> `ResizeObserver` / `IntersectionObserver` mocks from the old DataTable tests are **no longer needed**.

---

## Non-Functional Requirements

- **Accessibility**: Cards must be `<button type="button">` (keyboard-focusable). Action buttons in Drawer have descriptive labels.
- **Performance**: No new API calls introduced. `computed` for `selectedMember` avoids redundant reactivity.
- **Type safety**: All new reactive state fully typed. No `any`.
- **No regressions**: `FamilyUnitPage.vue` calls `FamilyMemberList` with same interface — zero changes required in the parent.

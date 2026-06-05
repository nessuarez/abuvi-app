# Frontend Implementation Plan: feat-registration-detail-tabs — Registration Detail Tab Layout

## Overview

Refactor `RegistrationDetailPage.vue` from a single-scroll layout into a four-tab layout using PrimeVue
Tabs (already installed). All existing functionality is preserved — no new composables, API calls, or
components are created. The change is purely structural: move existing template blocks into
`<TabPanel>` containers.

Tech stack: Vue 3 Composition API, `<script setup lang="ts">`, PrimeVue Tabs, Tailwind CSS.

---

## Architecture Context

### Components involved

| Component | Location | Tab |
|-----------|----------|-----|
| `RegistrationDetailPage.vue` | `frontend/src/views/registrations/` | **only file modified** |
| `RegistrationStatusBadge` | `components/registrations/` | header (above tabs) |
| `RegistrationMemberSelector` | `components/registrations/` | Tab 1 |
| `RegistrationExtrasSelector` | `components/registrations/` | Tab 1 |
| `RegistrationAccommodationNeeds` | `components/admin/registration-accommodation-needs/` | Tab 1 (admin only) |
| `RegistrationFriendLinks` | `components/admin/registration-accommodation-needs/` | Tab 1 (admin only) |
| `RegistrationPricingBreakdown` | `components/registrations/` | Tab 2 |
| `BankTransferInstructions` | `components/payments/` | Tab 3 |
| `PaymentInstallmentCard` | `components/payments/` | Tab 3 |
| `ManualPaymentDialog` | `components/admin/` | Tab 3 (dialog at template root) |
| `RegistrationStatusTimeline` | `components/registrations/` | Tab 4 |
| `AdminStatusChangeDialog` | `components/registrations/` | header (dialog at template root) |
| `RegistrationCancelDialog` | `components/registrations/` | dialogs at template root |
| `RegistrationDeleteDialog` | `components/registrations/` | dialogs at template root |

### Tab reference — existing tab pattern in codebase

Look at `frontend/src/components/admin/PaymentsAdminPanel.vue` for the exact PrimeVue Tabs usage to copy. It uses `v-model:value` with string tab identifiers, tab icons via PrimeVue icon props, and `<TabPanel>` content blocks.

### State management

No changes to Pinia stores. All state (`registration`, `installments`, `accommodationPrefs`, etc.) is
already loaded on `onMounted`. Tab switching is instant — no new API calls.

### Routing

No routing changes. The page continues to be accessed at the existing URL.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to the feature branch before any code changes.
- **Base branch**: `dev`
- **Branch name**: `feature/feat-registration-detail-tabs-frontend`
- **Commands**:
  ```bash
  git checkout dev
  git pull origin dev
  git checkout -b feature/feat-registration-detail-tabs-frontend
  git branch
  ```

---

### Step 1: Add PrimeVue Tab Imports to RegistrationDetailPage.vue

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Add tab component imports in `<script setup>`.
- **Implementation**: Add these five imports alongside the existing PrimeVue imports (around line 5):
  ```typescript
  import Tabs from 'primevue/tabs'
  import TabList from 'primevue/tablist'
  import Tab from 'primevue/tab'
  import TabPanels from 'primevue/tabpanels'
  import TabPanel from 'primevue/tabpanel'
  ```
- **Also add**: A reactive ref for the active tab (in the state section alongside the other `ref()` declarations, e.g. after line 90):
  ```typescript
  const activeTab = ref<string>('datos')
  ```

---

### Step 2: Restructure the Template — Wrap Sections in Tabs

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Replace the flat section layout inside `<template v-else-if="registration">` with a `<Tabs>` wrapper. Everything from the Notes section onward moves into tab panels.

#### What stays ABOVE the tabs (always visible — no change needed):

1. Header block: camp name, year, status badge, dates, location (current lines ~610–633)
2. Confirm-changes banner (Draft + hasPendingUserAcknowledgement) (~lines 636–679)
3. Generic draft info message (v-else-if Draft + isRepresentative) (~lines 681–690)
4. Admin status-change button + `<AdminStatusChangeDialog>` (~lines 692–714)

#### Cancel / Delete action buttons (stays BELOW tabs):

- Keep the `v-if="(isRepresentative && canCancel) || canDelete"` button row below the `</Tabs>` closing tag (~lines 1011–1030). These are global actions unrelated to a specific tab.

#### All dialogs stay at the template root (no change):

- `<RegistrationCancelDialog>`, `<RegistrationDeleteDialog>`, `<ManualPaymentDialog>`, refund warning `<Dialog>` remain at the bottom of the template. Dialogs are teleported to `<body>` by PrimeVue and do not need to live inside any specific tab.

#### Tab structure:

```html
<Tabs v-model:value="activeTab" class="mt-6">
  <TabList>
    <Tab value="datos">
      <i class="pi pi-users mr-2" />Datos de la Inscripción
    </Tab>
    <Tab value="precio">
      <i class="pi pi-calculator mr-2" />Desglose del precio
    </Tab>
    <Tab value="pagos">
      <i class="pi pi-credit-card mr-2" />Pagos
    </Tab>
    <Tab value="historial">
      <i class="pi pi-history mr-2" />Historial
    </Tab>
  </TabList>

  <TabPanels>

    <!-- TAB 1: Datos de la Inscripción -->
    <TabPanel value="datos">
      <!-- Notes (move from ~line 716) -->
      <!-- Información adicional edit block (move from ~line 722) -->
      <!-- Accommodation preferences read-only (move from ~line 805) -->
      <!-- Member/extras edit form area (move from pricing section ~line 840–935):
           - "Editar participantes" + "Editar extras" buttons
           - Edit members form (RegistrationMemberSelector)
           - Edit extras form (RegistrationExtrasSelector)
      -->
      <!-- Admin only: RegistrationAccommodationNeeds (move from ~line 819) -->
      <!-- Admin only: RegistrationFriendLinks (move from ~line 829) -->
    </TabPanel>

    <!-- TAB 2: Desglose del precio -->
    <TabPanel value="precio">
      <!-- RegistrationPricingBreakdown (move from ~line 937) -->
      <!-- Payment totals summary row — amountPaid + amountRemaining (move from ~line 989) -->
    </TabPanel>

    <!-- TAB 3: Pagos -->
    <TabPanel value="pagos">
      <!-- "Añadir pago manual" button (admin, move from ~line 944) -->
      <!-- BankTransferInstructions (move from ~line 956) -->
      <!-- Installment cards list (move from ~line 966) -->
      <!-- Empty state (move from ~line 982) -->
    </TabPanel>

    <!-- TAB 4: Historial de cambios -->
    <TabPanel value="historial">
      <!-- RegistrationStatusTimeline (move from ~line 1004) -->
      <!-- Empty state when no history: -->
      <p v-if="!registration.statusHistory?.length" class="text-sm text-gray-400 italic">
        Sin historial de cambios registrado.
      </p>
    </TabPanel>

  </TabPanels>
</Tabs>
```

#### Detailed content mapping (current line → new location):

| Current lines | Content | New location |
|---------------|---------|--------------|
| 716–720 | Notes block | Tab 1 (top) |
| 722–803 | Información adicional (read + edit form) | Tab 1 |
| 805–816 | Accommodation preferences | Tab 1 |
| 840–868 | Edit buttons (participantes/extras) + `<h2>` | Tab 1 (move heading + buttons here) |
| 870–904 | Edit members form | Tab 1 |
| 906–935 | Edit extras form | Tab 1 |
| 819–838 | RegistrationAccommodationNeeds + RegistrationFriendLinks (admin) | Tab 1 (after edit forms) |
| 937 | `<RegistrationPricingBreakdown>` | Tab 2 |
| 989–1001 | Payment totals summary (amountPaid / amountRemaining rows) | Tab 2 (below RegistrationPricingBreakdown) |
| 944–951 | "Añadir pago manual" button (admin) | Tab 3 |
| 956–963 | `<BankTransferInstructions>` | Tab 3 |
| 966–987 | Installment cards + empty state | Tab 3 |
| 1004–1009 | `<RegistrationStatusTimeline>` | Tab 4 |

#### Notes on the "Desglose de precio" heading:

The current `<h2>Desglose de precio</h2>` at ~line 843 becomes redundant because the tab label already says "Desglose del precio". Remove the `<h2>` (or replace it with a small subtitle if context helps). Similarly, remove the standalone `<h2>Pagos</h2>` at ~line 943.

---

### Step 3: Verify No Broken State References

- **Action**: Review that all reactive refs used in the moved blocks are still in scope (they are — all refs remain in `<script setup>`, which is component-level, not tab-level).
- **No changes needed**: `isEditingMembers`, `isEditingExtras`, `isEditingInfo`, `savingMembers`, `savingExtras`, `savingInfo`, `accommodationPrefs`, `localAccommodationNeeds`, `localFriendLinks`, `installments`, `paymentSettingsData`, `sortedInstallments`, `canEdit`, `canAdminEdit`, `canUserEditExtras` — all are still accessible from any tab panel.
- **Check**: The `showManualPaymentDialog` ref is triggered from a button in Tab 3 but the `<ManualPaymentDialog>` component lives outside the tabs. This is correct and already the PrimeVue pattern — the dialog teleports to `<body>`.

---

### Step 4: Update Documentation

- **File**: `ai-specs/specs/frontend-standards.mdc`
- **Action**: No structural change to the standards. The tab pattern is already documented via existing examples (`AccommodationReportsPanel`, `PaymentsAdminPanel`). No documentation update required for this refactor.
- **File**: `ai-specs/specs/api-endpoints.md` — no changes (no new endpoints).

---

## Implementation Order

1. Step 0 — Create feature branch
2. Step 1 — Add PrimeVue Tab imports and `activeTab` ref
3. Step 2 — Restructure template with Tabs/TabList/TabPanels
4. Step 3 — Verify reactive state references (review only)
5. Step 4 — Documentation review (no changes needed)

---

## Testing Checklist

- [ ] Page loads and defaults to "Datos de la Inscripción" tab
- [ ] All edit flows work from Tab 1: edit members, edit extras, edit info
- [ ] Admin-only sections (RegistrationAccommodationNeeds, RegistrationFriendLinks) visible in Tab 1 for admin/board; hidden for member
- [ ] Tab 2 shows pricing breakdown and payment totals correctly
- [ ] Tab 3 shows bank transfer instructions, installment cards, payment totals; "Añadir pago manual" visible for admin/board only
- [ ] Tab 4 shows timeline; empty state shown when `statusHistory` is empty
- [ ] Draft banner, status change button, header remain visible across all tab switches
- [ ] Cancel/Delete buttons remain below the tabs
- [ ] ManualPaymentDialog opens from Tab 3 button
- [ ] All existing dialogs (cancel, delete, refund warning) still function
- [ ] No extra API calls when switching tabs
- [ ] Responsive: tab labels visible on mobile (PrimeVue handles tab overflow scrolling)
- [ ] Cypress E2E: if `registrations.cy.ts` exists, verify the registration detail flow still passes

---

## Error Handling Patterns

No changes to error handling. All API calls and error states remain in the existing composables.
The `v-else-if="error && !registration"` block stays above the tabs and is not affected.

---

## UI/UX Considerations

- **Tab icons**: Use PrimeVue `pi` icons in tab labels (see `PaymentsAdminPanel.vue` for reference): `pi-users` for Datos, `pi-calculator` for Precio, `pi-credit-card` for Pagos, `pi-history` for Historial.
- **Mobile responsiveness**: PrimeVue `<Tabs>` with `<TabList>` scrolls horizontally on overflow automatically. No additional Tailwind needed.
- **Default tab**: `activeTab = ref('datos')` — always opens on the data tab.
- **Tab spacing**: Add `class="mt-6"` on `<Tabs>` to maintain the same vertical spacing the sections had.
- **Padding inside panels**: Each `<TabPanel>` should have `class="pt-4"` to add space below the tab bar. Alternatively, wrap the content with a `<div class="py-4">`.
- **Empty state for timeline**: Add a graceful empty state message in Tab 4 for registrations without status history.

---

## Dependencies

No new npm packages. PrimeVue Tabs components are already available:
- `primevue/tabs`
- `primevue/tablist`
- `primevue/tab`
- `primevue/tabpanels`
- `primevue/tabpanel`

---

## Notes

- **Single file change**: Only `RegistrationDetailPage.vue` is modified. No new files.
- **No backend changes**: This is a pure frontend refactor.
- **No new composables**: All data is already loaded on `onMounted`.
- **Language**: All user-facing tab labels must be in Spanish.
- **PrimeVue version**: The project uses PrimeVue with the new Tabs API (`primevue/tabs`, not the legacy `primevue/tabview`). Confirm by checking existing imports in `AccommodationReportsPanel.vue` or `PaymentsAdminPanel.vue`.
- **`v-model:value`**: PrimeVue Tabs uses `v-model:value` (string), not `v-model:activeIndex` (integer).
- **Dialogs outside tabs**: All `<Dialog>`-based components must remain at the root of the registration template (outside `<Tabs>`), not inside any `<TabPanel>`. PrimeVue dialogs teleport to `<body>` and work regardless of DOM position, but keeping them outside tabs makes the structure cleaner.
- **The `<h2>` headings**: The current section headings ("Desglose de precio", "Pagos") become redundant once moved to named tabs. Remove or visually demote them to avoid duplication.

---

## Next Steps After Implementation

1. Manual QA: navigate as member, admin, and board user — verify tab content and edit flows.
2. Run Cypress E2E if available: `npx cypress run --spec "cypress/e2e/registrations.cy.ts"`.
3. Run TypeScript check: `npx vue-tsc --noEmit`.
4. Open PR targeting `dev` branch.

---

## Implementation Verification

- [ ] Code Quality: `<script setup lang="ts">`, no `any`, no new `<style>` blocks
- [ ] Functionality: all four tabs render, default is Datos, all edit flows and dialogs work
- [ ] Testing: existing Cypress tests pass; no new unit tests needed (no logic added)
- [ ] Integration: no new API calls introduced
- [ ] Documentation: no spec files require update

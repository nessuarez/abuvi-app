# Feature: Registration Detail — Tab Layout

## Problem

`RegistrationDetailPage.vue` (currently ~1,081 lines) renders all sections in a single long scroll:
payments, pricing breakdown, member/extras data, accommodation preferences, admin-only tools, and the status timeline.
This makes the page hard to navigate — especially for admin/board users who need to jump between
sections frequently.

## Goal

Reorganise the registration detail view into a tabbed layout using PrimeVue Tabs (the same
pattern already used in `AccommodationReportsPanel.vue` and `PaymentsAdminPanel.vue`).
The header block (camp name, dates, status badge, draft-acknowledgement banner, and action buttons)
stays above the tabs and is always visible.

---

## Proposed Tab Structure

### Always-visible header (above tabs — no change needed)

- Camp name, year, location, dates
- `RegistrationStatusBadge`
- Confirm Changes banner (Draft + `hasPendingUserAcknowledgement`)
- Status change button (Admin/Board only)
- Back link and delete/cancel actions

---

### Tab 1 — Datos de la Inscripción

**Audience:** member (representative) + admin/board

**Content:**

- Member list with attendance periods — `RegistrationMemberSelector` (edit mode for representative when status allows)
- Extras selected — `RegistrationExtrasSelector` (edit mode for representative when status allows)
- Additional information: `specialNeeds`, `campatesPreference`, `hasPet` — editable by representative
- Accommodation preferences (up to 3 ranked choices) — `AccommodationPreferences` section
- Notes field (read-only for member, editable by admin/board)

**Admin/Board only (within this tab):**

- `RegistrationAccommodationNeeds` — accommodation feature tagging
- `RegistrationFriendLinks` — friend-family links

---

### Tab 2 — Desglose del precio

**Audience:** member + admin/board

**Content:**

- `RegistrationPricingBreakdown` component (members × price + extras breakdown)
- Total amount, amount paid, amount remaining (summary row)
- If admin/board: manual payment creation button (`ManualPaymentDialog`)

---

### Tab 3 — Pagos

**Audience:** member + admin/board

**Content:**

- `BankTransferInstructions` (only when payment pending)
- `PaymentInstallmentCard` list for all installments
- Payment totals (total / paid / remaining) — summary chip row

**Admin/Board only (within this tab):**

- Edit payment / confirm combined transfer actions
- Manual payment creation shortcut

---

### Tab 4 — Historial de cambios

**Audience:** member + admin/board (same data, no filtering needed)

**Content:**

- `RegistrationStatusTimeline` — renders `registration.statusHistory` already available in `RegistrationResponse`
- No new API calls required

---

### Tab 5 — Comunicaciones (future / out of scope for this ticket)

**Assessment:** Not feasible in this ticket.
There is no existing backend concept for board → family registration communications.
Implementing it would require:

- New database entity (e.g. `RegistrationCommunication` with `registrationId`, `authorUserId`, `body`, `createdAt`, `isRead`)
- New backend API endpoints (list, create, mark-read)
- Frontend component

**Recommendation:** Defer to a separate ticket. This tab should **not** be built as part of this story.

---

## Implementation Plan

### Phase 1 — Refactor `RegistrationDetailPage.vue`

1. Add PrimeVue Tabs imports (already available — no new package needed):

   ```typescript
   import Tabs from 'primevue/tabs'
   import TabList from 'primevue/tablist'
   import Tab from 'primevue/tab'
   import TabPanels from 'primevue/tabpanels'
   import TabPanel from 'primevue/tabpanel'
   ```

2. Keep the existing header block outside (above) the `<Tabs>` wrapper.

3. Move existing sections into the four tab panels as described above.
   - No new composables or API calls are needed for tabs 1–4.
   - No components need to be created; existing ones are just repositioned.

4. Default active tab: `"datos"` (Tab 1), using PrimeVue `v-model:value` on `<Tabs>`.

5. Use string values for tab identifiers: `"datos"`, `"precio"`, `"pagos"`, `"historial"`.

6. Role-based visibility: admin/board-only sections within Tab 1 stay wrapped in the same
   `v-if="auth.isBoard"` / `v-if="auth.isAdmin"` guards already present.
   No new visibility logic needed at the tab level (all four tabs are shown to all roles).

---

## Files to Modify

| File | Change |
|------|--------|
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Add `<Tabs>` wrapper, move sections into tab panels |

No new files, no new components, no backend changes.

---

## Acceptance Criteria

- [ ] The registration detail page renders a four-tab layout: Datos de la Inscripción / Desglose del precio / Pagos / Historial de cambios.
- [ ] The header (status badge, camp info, draft banner, action buttons) is always visible above the tabs.
- [ ] Tab 1 contains all member/extras/accommodation/info content, including admin-only sections hidden for members.
- [ ] Tab 2 contains the pricing breakdown and totals.
- [ ] Tab 3 contains payment installments, bank transfer instructions, and totals.
- [ ] Tab 4 contains the status change timeline.
- [ ] Switching tabs does not trigger any extra API calls — all data is already loaded on mount.
- [ ] The active tab defaults to "Datos de la Inscripción".
- [ ] The layout is responsive — tabs stack correctly on mobile.
- [ ] No existing functionality is broken (all edit flows, dialogs, and admin actions work as before).

---

## Non-Functional Requirements

- **Performance:** No new API calls. Tab switching must be instant (all data already in-memory).
- **Accessibility:** PrimeVue Tabs are ARIA-compliant out of the box — no extra aria attributes needed.
- **Tests:** Add/update Vitest unit tests for any extracted logic; update Cypress e2e if registration detail flow is covered.

---

## Out of Scope

- Tab 5 (Comunicaciones de la Junta) — separate future ticket.
- URL-based tab deep-linking (e.g. `?tab=pagos`) — can be added later if needed.
- Any backend changes.

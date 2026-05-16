# Navigation Improvements — Enriched User Story

## Summary

Refactor the application navigation to improve access to key admin and camp features, reorganize the admin sidebar sections, and remove the desktop collapsed-sidebar behavior.

---

## Changes Breakdown

### 1. AppHeader: Add shortcut to the current camp edition (admin)

**As** a Board/Admin user,
**I want** a direct link in the top navigation bar to the current camp edition's admin detail page,
**so that** I can access it quickly without going through the admin panel.

#### Behavior

- Visible only to Board/Admin users (`auth.isBoard`).
- Fetches the current camp edition via `fetchCurrentCampEdition()` from `useCampEditions()` (API: `GET /camps/current`).
- If no current edition exists, the link is not rendered.
- The link navigates to `/camps/editions/:id` (route name: `camp-edition-detail`), where `:id` is `currentCampEdition.value.id`.
- Label: **"Edición Actual"** with icon `pi pi-calendar`.
- Styled like the existing admin link (red tones), but a secondary/outline variant to differentiate it.
- Active state: `bg-red-50 text-red-700` when the current route starts with `/camps/editions`.
- Inactive state: `border border-red-600 text-red-600 hover:bg-red-50`.
- Both desktop nav and mobile nav drawer must include this link.
- The link is placed between the regular nav links and the "Administración" button.

#### Files to modify

- `frontend/src/components/layout/AppHeader.vue`
  - Import `useCampEditions` and call `fetchCurrentCampEdition()` on mount.
  - Add computed `currentEditionId` from `currentCampEdition.value?.id`.
  - Add the link to both desktop `<nav>` and mobile `<nav>` blocks, guarded by `auth.isBoard && currentEditionId`.

---

### 2. AdminSidebar: Add "Campamento Actual" to Gestión section

**As** a Board/Admin user,
**I want** a direct link to the current camp edition detail page in the admin sidebar's "Gestión" section,
**so that** I can navigate there directly from the admin panel.

#### Behavior

- New item in the **Gestión** group: label `"Campamento Actual"`, icon `pi pi-calendar`, route `/camps/editions/:id` (dynamic).
- The route uses the current edition's ID, fetched via `fetchCurrentCampEdition()`.
- If no current edition exists, the item is **not rendered** (set `visible: false`).
- Active detection: `route.path.startsWith('/camps/editions/' + currentEditionId)`.
- The `AdminMenuMenuItem` interface must be extended to support an optional dynamic `to` field, or the computed `menuGroups` must resolve the URL at compute time.
- Placed as the **first item** in the Gestión group (before Campamentos).

#### Files to modify

- `frontend/src/components/admin/AdminSidebar.vue`
  - Import `useCampEditions` and call `fetchCurrentCampEdition()` on mount (`onMounted`).
  - Add `currentCampEdition` ref to derive the dynamic route.
  - Update `menuGroups` computed to include the new item.
  - Update `isActive()` to handle prefix matching for this item.

---

### 3. AdminSidebar: Add "Asignación de Habitaciones" to Gestión → Campamento Actual context

**As** a Board/Admin user,
**I want** a direct link to the room assignment view for the current camp edition in the admin sidebar,
**so that** I can open it without navigating through the camp edition detail page.

#### Behavior

- New item in the **Gestión** group: label `"Asignación de Habitaciones"`, icon `pi pi-th-large`, route `/camps/editions/:id/assignment`.
- Only visible when a current edition exists (`visible: auth.isBoard && !!currentEditionId`).
- Placed below the "Campamento Actual" item.
- Active detection: `route.path === '/camps/editions/' + currentEditionId + '/assignment'`.

#### Files to modify

- `frontend/src/components/admin/AdminSidebar.vue` (same changes as item 2 above — resolved together in one pass).

---

### 4. AdminSidebar: Move "Unidades Familiares" from Gestión to Personas

**As** an administrator,
**I want** "Unidades Familiares" listed under the "Personas" section of the admin sidebar,
**so that** the menu organization reflects logical domain grouping.

#### Current state

```
Gestión:
  - Campamentos
  - Inscripciones
  - Unidades Familiares  ← here

Personas:
  - Usuarios
```

#### Target state

```
Gestión:
  - Campamento Actual  ← new (see change #2)
  - Asignación de Habitaciones  ← new (see change #3)
  - Campamentos
  - Inscripciones

Personas:
  - Usuarios
  - Unidades Familiares  ← moved here
```

#### Files to modify

- `frontend/src/components/admin/AdminSidebar.vue` — move the `Unidades Familiares` item object from the `Gestión` group array to the `Personas` group array.

---

### 5. AdminPage: Remove desktop collapsed-sidebar behavior

**As** a desktop user in the admin panel,
**I want** the hamburger button (three-line toggle) to be visible but non-functional on desktop,
**so that** the desktop layout always shows the full sidebar without a collapsed/drawer state.

#### Current state

- `AdminPage.vue` shows a `<Button icon="pi pi-bars">` with `class="md:hidden"` — already hidden on desktop (`md:hidden` = hidden at ≥768px).
- The drawer (`<Drawer>`) also has `class="md:hidden"`.

**Verification needed:** The user reports that the hamburger button is currently visible on desktop. This may mean the breakpoint is `lg:` not `md:`, or the `md:hidden` classes are not applied consistently.

#### Target state

- The hamburger `<Button>` must be visible on **all** screen sizes (remove `md:hidden` / `lg:hidden` from the button).
- Clicking it on desktop does **nothing** — the drawer is still `md:hidden` (hidden on desktop), so it never appears on desktop regardless.
- On mobile (`< md`), clicking it still opens the `<Drawer>` as before.
- No JavaScript logic changes needed — the behavior difference is purely CSS: the button is always visible, but the drawer it controls is still hidden on desktop via `class="md:hidden"`.

#### Implementation

In `AdminPage.vue`:

- Remove `class="md:hidden"` (or `lg:hidden`) from the `<Button icon="pi pi-bars">`.
- Keep `class="md:hidden"` on the `<Drawer>` — so clicking the button on desktop fires `drawerVisible = true` but the drawer never renders.

#### Files to modify

- `frontend/src/views/AdminPage.vue` — remove the responsive hide class from the toggle button only.

---

## Acceptance Criteria

- [ ] Board/Admin users see an "Edición Actual" link in `AppHeader` (desktop and mobile) that navigates to the current edition detail page; the link is absent when no current edition exists.
- [ ] The "Campamento Actual" item appears first in the Gestión section of the admin sidebar, linking to `/camps/editions/:currentId`.
- [ ] The "Asignación de Habitaciones" item appears in the Gestión section, linking to `/camps/editions/:currentId/assignment`; both are hidden when no current edition exists.
- [ ] "Unidades Familiares" no longer appears under Gestión; it appears under Personas after "Usuarios".
- [ ] On desktop, the hamburger button is visible in the admin panel; clicking it does not open a drawer.
- [ ] On mobile, the hamburger button opens the admin sidebar drawer as before.
- [ ] No regressions in mobile navigation or existing admin sidebar items.
- [ ] All new items use `data-testid` attributes following the pattern `sidebar-*` (e.g., `sidebar-current-edition`, `sidebar-room-assignment`).

---

## Files to Modify

| File | Changes |
|------|---------|
| `frontend/src/components/layout/AppHeader.vue` | Add "Edición Actual" link for Board users |
| `frontend/src/components/admin/AdminSidebar.vue` | Add 2 new Gestión items, move Unidades Familiares to Personas |
| `frontend/src/views/AdminPage.vue` | Remove responsive hide class from hamburger button |

## No backend changes required

---

## Non-Functional Requirements

- **Performance**: `fetchCurrentCampEdition()` in `AppHeader` shares the same API call pattern as `CampPage.vue`. Consider whether a Pinia store should cache `currentCampEdition` globally to avoid duplicate requests on the same page load.
- **Accessibility**: New nav links must include `aria-current="page"` when active.
- **Type safety**: Any dynamic `to` in `AdminMenuMenuItem` must remain fully typed (no `string | undefined` leaks into `router-link :to`).

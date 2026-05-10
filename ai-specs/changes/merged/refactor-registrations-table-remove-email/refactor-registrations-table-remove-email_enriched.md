# Remove Email Column from Admin Registrations Table

## Summary

The "Email" column in the admin registrations DataTable (`RegistrationsAdminPanel.vue`) is redundant: the representative's email is accessible on the registration detail page, and the column takes up horizontal space without adding actionable value in the list view.

This is a pure frontend display change — no API, type, or backend modifications required. The `representative.email` field remains in the TypeScript type and the API response; it is simply not rendered in the table.

---

## Current State

**File:** `frontend/src/components/admin/RegistrationsAdminPanel.vue`

Current column order (11 columns):

1. Familia *(sortable)*
2. Representante
3. **Email** ← remove this
4. Estado
5. Período
6. Aloj.
7. Miembros
8. Total
9. Pagado
10. Pendiente
11. Creación *(sortable)*

Footer `ColumnGroup` label column currently uses `:colspan="6"` to span columns 1–6
(Familia, Representante, Email, Estado, Período, Aloj.).

---

## Changes Required

### `frontend/src/components/admin/RegistrationsAdminPanel.vue`

#### 1. Remove the Email column (template)

Delete:

```vue
<Column header="Email">
  <template #body="{ data }">
    <span class="text-sm text-gray-600">{{ data.representative.email }}</span>
  </template>
</Column>
```

#### 2. Adjust footer colspan: 6 → 5

The footer label spans the first N columns up to and including Aloj. After removing Email, that is 5 columns (Familia, Representante, Estado, Período, Aloj.):

```vue
<!-- before -->
:colspan="6"

<!-- after -->
:colspan="5"
```

---

## Files to Modify

| File | Change |
|---|---|
| `frontend/src/components/admin/RegistrationsAdminPanel.vue` | Remove Email `<Column>`; update footer colspan 6 → 5 |

No other files need to change. The `AdminRegistrationListItem.representative.email` field in `frontend/src/types/registration.ts` stays as-is (the data is still available for future use or detail pages).

---

## Acceptance Criteria

1. The Email column is no longer visible in the admin registrations table.
2. The footer row still aligns correctly: the label cell spans exactly the first 5 columns.
3. All other columns and their data render correctly without visual regressions.
4. TypeScript compiles with no errors (`vue-tsc --noEmit`).

---

## Non-Functional Requirements

- No new dependencies.
- No API or backend changes.
- No migration required.

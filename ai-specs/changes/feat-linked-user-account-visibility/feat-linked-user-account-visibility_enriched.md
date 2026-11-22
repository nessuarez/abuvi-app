# Show the account linked to a family member: username (email) and role

**Feature ID:** `feat-linked-user-account-visibility`
**Date:** 2026-11-04
**Type:** UX improvement (frontend) + DTO extension (backend)
**Related:** [#302](https://github.com/nessuarez/abuvi-app/issues/302) — restrict `GET /api/users/{id}` (security debt found while enriching this story; this feature must not use that endpoint)

---

## 1. Summary

Today, when a family member has a login account, the UI only says *"Usuario vinculado"* ("Linked user"). Admin and Board users cannot tell **which** account it is, cannot look it up in the user list, and cannot see at a glance which people hold the Board or Admin role. Navigating families → members → users is a daily Board task.

**As** an Admin or Board member,
**I want** to see the username (email) of the account linked to each member, a label when that account has the Board or Admin role, and a way to jump to it in the user list,
**so that** I can identify which account each person logs in with, recognise who has elevated permissions, and move between families, members and users without searching blindly.

---

## 2. Verified current state

| Element | Location | Status |
| --- | --- | --- |
| "Usuario vinculado" text (list row) | [FamilyMemberList.vue:148-150](../../../frontend/src/components/family-units/FamilyMemberList.vue#L148-L150) | Plain text |
| "Usuario vinculado" text (detail, "Cuenta" field) | [FamilyMemberList.vue:235-241](../../../frontend/src/components/family-units/FamilyMemberList.vue#L235-L241) | Plain text |
| Only consumer | [FamilyUnitPage.vue:443](../../../frontend/src/views/FamilyUnitPage.vue#L443) | Already passes `is-admin-or-board` |
| Member DTO | `FamilyMemberResponse` in [FamilyUnitsModels.cs](../../../src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs) | Only `UserId`. No EF navigation `FamilyMember → User` |
| Linking | [FamilyUnitsService.cs:255-275](../../../src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs#L255-L275) | Linked by matching email, but `member.Email` can diverge afterwards → **show `User.Email`, not `member.Email`** |
| Roles | `UserRole { Admin, Board, Member }` in [UsersModels.cs](../../../src/Abuvi.API/Features/Users/UsersModels.cs) | — |
| Role labels | [utils/user.ts](../../../frontend/src/utils/user.ts) (`getRoleLabel`: Administrador / Junta Directiva / Socio) and coloured pill in [UserRoleCell.vue:39-49](../../../frontend/src/components/users/UserRoleCell.vue#L39-L49) | The pill is embedded in `UserRoleCell` and is not reusable |
| User list | [UsersAdminPanel.vue](../../../frontend/src/components/admin/UsersAdminPanel.vue) (`/admin/users`) | Local `searchQuery` filter by email/name; does not read query params |

---

## 3. Scope

### In scope

1. `FamilyMemberResponse` exposes a summary of the linked account: `linkedUser { email, role }`.
2. `FamilyMemberList` replaces "Usuario vinculado" with the email and indicates it is the login account.
3. If the account role is `Admin` or `Board`, a role label is shown next to the email. `Member` shows no label.
4. Admin/Board: in the member detail, the email links to `/admin/users?search=<email>`.
5. `UsersAdminPanel` initialises its search box from `?search=`.
6. Refactor: extract the role pill from `UserRoleCell` into a reusable `UserRoleBadge` component.

### Out of scope (possible later phases)

- Last login (`LastLoginAt`) in the user list.
- Opening the user's edit dialog directly (`?userId=`).
- "Tu cuenta" (your account) / "Inactiva" (inactive) labels.
- Reverse link user → family.

---

## 4. Backend

### 4.1 DTO

New record in `FamilyUnitsModels.cs`:

```csharp
public record LinkedUserSummary(string Email, UserRole Role);
```

- `FamilyMemberResponse` gets `LinkedUserSummary? LinkedUser` appended at the end. `UserId` is kept.
- The role serialises as a string (`"Admin"`, `"Board"` or `"Member"`), as in `UserResponse`. Verify the same global `JsonStringEnumConverter` applies.
- `FamilyMemberExtensions.ToResponse(this FamilyMember member, LinkedUserSummary? linkedUser = null)`.

### 4.2 Batch resolution

- New `IUsersRepository.GetLinkedUserSummariesByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)` → `IReadOnlyDictionary<Guid, LinkedUserSummary>`.
  - A single query, `AsNoTracking`.
  - Projection to `Id`, `Email` and `Role` only.
- `FamilyUnitsService.GetFamilyMembersByFamilyUnitIdAsync` and the methods that return a member to `FamilyUnitPage` (create/update member):
  - Collect the non-null `UserId` values.
  - Resolve them in one call and map. **No N+1.**
- A `UserId` pointing to a missing user → `LinkedUser = null`.

### 4.3 Affected endpoints (no new routes, no authorization changes)

| Method | Route | Change |
| --- | --- | --- |
| `GET` | `/api/family-units/{id}/members` | Each member includes `linkedUser` |
| `POST`/`PUT` | existing member endpoints | Response includes `linkedUser` |

Example:

```json
{
  "id": "…",
  "userId": "…",
  "firstName": "Ana",
  "linkedUser": { "email": "ana@example.com", "role": "Board" }
}
```

---

## 5. Frontend

### 5.1 Type

[types/family-unit.ts](../../../frontend/src/types/family-unit.ts), in `FamilyMember`:

```ts
linkedUser: { email: string; role: UserRole } | null
```

`UserRole` is imported from `@/types/user`.

### 5.2 `UserRoleBadge.vue` (new, presentational)

- Location: `frontend/src/components/users/UserRoleBadge.vue`.
- Props: `role: UserRole`, `size?: 'sm' | 'md'`.
- Content: the pill currently in [UserRoleCell.vue:39-49](../../../frontend/src/components/users/UserRoleCell.vue#L39-L49), with the same colours (Admin red, Board blue, Member grey) and `getRoleLabel`.
- `UserRoleCell` uses it. No visual change in `/admin/users`; existing `UserRoleCell` tests must stay green.
- Keeps `data-testid="role-badge"`.

### 5.3 `FamilyMemberList.vue`

Label rule: render `<UserRoleBadge size="sm">` **only** when `linkedUser.role !== 'Member'`.

**Row (list):** replace the block at lines 148-150 with:

```text
<i class="pi pi-user" /> {{ member.linkedUser.email }} [UserRoleBadge if Admin/Board]
```

- The email uses `truncate` and a `title`/tooltip *"Cuenta con la que accede: <email>"* ("Account used to log in").
- The label does not truncate (`flex-shrink-0`). On mobile, if space runs out, the email is cut before the label.
- **No link.** The whole row is a `<button>`, and an `<a>` inside a button is invalid HTML.

**Detail ("Cuenta" field):** replace lines 235-241:

- Label: *"Cuenta de acceso"* ("Login account").
- Value:
  - If `isAdminOrBoard`: `RouterLink` to `{ name: 'admin-users', query: { search: linkedUser.email } }` with a `pi-external-link` icon.
  - Otherwise: plain text.
  - In both cases, plus `UserRoleBadge` when the role is Admin/Board.
- `aria-label="Cuenta de acceso: <email>, rol <label>"`.

**Fallback:** if `member.userId` is set but `linkedUser` is `null`, keep the current *"Usuario vinculado"* text with no label.

`data-testid="linked-user-email"` in both places.

### 5.4 `UsersAdminPanel.vue`

- `searchQuery` is initialised from `route.query.search` when it is a non-empty string.
- Syncing the URL while typing in the search box is optional.

---

## 6. Tests (strict TDD: RED → GREEN → REFACTOR)

**Backend (`src/Abuvi.Tests`)**

- `GetFamilyMembers_WithLinkedUser_ReturnsUserEmailNotMemberEmail`
- `GetFamilyMembers_WithLinkedUser_ReturnsUserRole`
- `GetFamilyMembers_LinkedUserMissing_ReturnsNullLinkedUser`
- `GetFamilyMembers_ResolvesLinkedUsersInSingleCall` (one call to `GetLinkedUserSummariesByIdsAsync`)
- `GetFamilyMembers_NoLinkedUsers_DoesNotQueryUsers`
- Serialisation: `linkedUser.role` is emitted as a string.

**Frontend (Vitest)**

- `UserRoleBadge`: label and colour class for each role.
- `UserRoleCell`: existing tests still pass after the refactor.
- `FamilyMemberList`:
  - Shows the email in the row and in the detail.
  - Shows the label for `Admin` and `Board`, not for `Member`.
  - The link appears only with `isAdminOrBoard`, targeting `admin-users?search=<email>`.
  - Fallback "Usuario vinculado" when `linkedUser` is `null`.
  - Update existing tests that look for the old text.
- `UsersAdminPanel`: with `?search=foo@bar.com`, the list is pre-filtered.

---

## 7. Non-functional requirements

- **Privacy**: email and role are visible only to whoever can already see the member; authorization is unchanged.
  - A representative sees the account data of their own family.
  - Board membership is not sensitive information within the association.
- **Security**: do not add consumers of `GET /api/users/{id}` (see #302). The displayed role is informational; the frontend does not use it for permission decisions.
- **Performance**: at most one extra query per member list.
- **Accessibility**: the label does not rely on colour alone; it always shows the role text.
- **i18n**: UI copy in Spanish, reusing `getRoleLabel`.

---

## 8. Files to modify

**Backend**

- `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`
- `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`
- `src/Abuvi.API/Features/Users/IUsersRepository.cs`, `UsersRepository.cs`
- Tests in `src/Abuvi.Tests/Unit/Features/FamilyUnits/`

**Frontend**

- `frontend/src/types/family-unit.ts`
- `frontend/src/components/users/UserRoleBadge.vue` (new) + test
- `frontend/src/components/users/UserRoleCell.vue` (uses `UserRoleBadge`)
- `frontend/src/components/family-units/FamilyMemberList.vue` + tests
- `frontend/src/components/admin/UsersAdminPanel.vue` + tests

**Documentation**

- `ai-specs/specs/api-spec.yml` (if present): `linkedUser` in `FamilyMemberResponse`

---

## 9. Acceptance criteria

1. As Admin/Board, on any family's page I see the account email of every linked member, both in the list and in the detail.
2. The email shown is the account's (`User.Email`), even when the member's own email differs.
3. If the account is Administrator or Board, the matching label appears, with the same colours as in `/admin/users`. Regular members have no label.
4. From the member detail, one click takes me to `/admin/users` with that user already filtered.
5. A representative sees the email and label, but no link.
6. Members without an account show nothing. If the linked account cannot be found, "Usuario vinculado" is kept.
7. The role column in `/admin/users` looks the same as before the refactor.
8. `dotnet test`, `npm run test`, type-check and lint pass with no errors.

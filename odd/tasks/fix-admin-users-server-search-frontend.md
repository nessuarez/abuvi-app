# fix-admin-users-server-search (frontend)

**Objective**: The admin Users panel searches, paginates and sorts on the server, so every user can be found.
**Plan**: `ai-specs/changes/fix-admin-users-server-search/fix-admin-users-server-search_frontend.md`
**Branch**: `feature/fix-admin-users-server-search-frontend` (stacked on `feature/fix-admin-users-server-search-backend`, PR #303)
**Scope**:
- `frontend/src/types/user.ts`
- `frontend/src/composables/useUsers.ts`
- `frontend/src/components/admin/UsersAdminPanel.vue`
- their Vitest tests
- deletion of the dead `pages/UsersPage.vue` and `pages/UserDetailPage.vue`
- new Cypress spec `admin-users-search.cy.ts`
- a note in `frontend-standards.mdc`

**TDD**: enabled (global config). Runner: `npx vitest run` (in `frontend/`).
**RDD**: off (global).
**Delivery**: stacked PR on #303 (`feature-branch-chain`).

## Tasks

- [x] T1: `useUsers`: write the RED tests, then implement paged `fetchUsers`, `pagination`, `refresh`, the stale-response guard and the mutation refresh. Route: delegated writer.
- [x] T2: `UsersAdminPanel`: write the RED tests, then implement the lazy DataTable, debounced search, server sort, total counter, empty state and role refresh. Route: delegated writer (same writer).
- [x] T3: Delete the dead pages, add the Cypress spec and the `frontend-standards` note.
- [x] T4: Run lint, type-check and the Vitest suite. Then commit, push, and open a stacked PR.

## Progress / Evidence

Writer evidence (not committed; T4 pending):
- T1 RED: useUsers.test.ts 11 failed / 12 passed (new params, pagination, stale guard, refresh, create/delete refresh). GREEN: 23/23 passed.
- T2 RED: UsersAdminPanel.test.ts 10 failed / 12 passed. GREEN: 22/22; with src/components/users: 6 files, 71 tests passed.
- T3: UsersPage.vue and UserDetailPage.vue deleted (grep showed no references); cypress/e2e/admin-users-search.cy.ts written (not run); frontend-standards.mdc section added.
- vue-tsc --noEmit: clean.
- eslint: could not run (no eslint.config.* in repo, ESLint 10 installed; pre-existing).
- Full vitest: 18 failed / 1000 passed; identical 18 failures / 9 files on the base with changes stashed (pre-existing, unrelated).

- Parent spot check: useUsers + UsersAdminPanel tests 45/45 pass. Committed and opened a stacked PR.

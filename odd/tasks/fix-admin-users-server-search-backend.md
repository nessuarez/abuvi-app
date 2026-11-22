# fix-admin-users-server-search (backend)

**Objective**: `GET /api/users` supports server-side paging, search, filters and sort, so admins can find any user.
**Problem**: the endpoint returns at most 100 users (`take=100`) and has no search. The frontend filters only what it loaded.
**Plan**: `ai-specs/changes/fix-admin-users-server-search/fix-admin-users-server-search_backend.md`
**Branch**: `feature/fix-admin-users-server-search-backend` (from `dev`)
**Scope**: `src/Abuvi.API/Features/Users/*`, Users tests, and `ai-specs/specs/api-endpoints.md`. No schema changes.
**TDD**: enabled (global config). Runner: `dotnet test src/Abuvi.Tests`.
**RDD**: off (global), so ordinary checks apply.
**Delivery**: `single-pr` (small change; the frontend change follows on top).

## Tasks

- [ ] T1: Write RED tests (service unit tests, repository paging tests, endpoint shape tests). Then implement the DTOs, repository, service and endpoint until GREEN. Route: delegated writer (writer trigger: 5+ non-trivial files).
- [x] T2: Document `GET /api/users` in `api-endpoints.md`. Route: included in T1.
- [ ] T3: Run the full test suite, commit, push, and open a PR to `dev`.

## Acceptance

- A search finds a user beyond the first 100.
- Search is case-insensitive.
- `pageSize` is clamped to 100.
- 401 and 403 behavior is preserved.

## Progress / Evidence

- T1 (partial): RED observed (compile errors: GetPagedAsync, UserListQuery, PagedUsersResponse missing). GREEN: UsersServiceTests + UsersRepositoryPagingTests, 33/33 pass. ProtectedEndpointsTests GET /api/users tests could not be run: they need PostgreSQL at 127.0.0.1:5432, which is unavailable here (all WebApplicationFactory tests fail the same way). T1 stays unchecked until those run.
- T2: GET /api/users section added to ai-specs/specs/api-endpoints.md.
- Parent change: `ApplySort` now sorts ascending when a recognised `sortBy` arrives without an explicit `desc`; previously it fell back to `createdAt desc`. Docs updated to match. Re-ran the Users tests: 33/33 pass. `dotnet build src/Abuvi.API`: 0 errors.
- Pending: `ProtectedEndpointsTests` (GET /api/users) need PostgreSQL; run them locally before merging.

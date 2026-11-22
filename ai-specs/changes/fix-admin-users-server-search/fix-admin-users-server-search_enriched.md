# fix: Admin users list must search and paginate on the server

## Problem

On **Admin → Users** (`/admin/users`), the search box only filters rows that are already loaded in the browser. The frontend calls `GET /api/users` once with no query parameters. The backend defaults to `skip=0, take=100` and orders by `CreatedAt`. As a result:

- Only the **first 100 users by creation date** are ever loaded.
- Users created after those 100 do not appear in the table, and searching by name or email cannot find them.
- The UI gives no sign that the list is incomplete, because there is no total count.

Evidence:

| Layer | Location | Current behavior |
|---|---|---|
| Endpoint | `src/Abuvi.API/Features/Users/UsersEndpoints.cs:91-100` | `skip=0`, `take=100`. No `search`, no max clamp, no total. Returns `ApiResponse<List<UserResponse>>`. |
| Repository | `src/Abuvi.API/Features/Users/UsersRepository.cs:34-43` | `OrderBy(CreatedAt).Skip(skip).Take(take)` |
| Composable | `frontend/src/composables/useUsers.ts:15-27` | `api.get('/users')` with no params |
| Panel | `frontend/src/components/admin/UsersAdminPanel.vue:30-38, 49, 193-199` | `filteredUsers` computed filters client-side. The DataTable is not lazy (`:value="filteredUsers" paginator :rows="10"`). |

## Goal

The Users admin table queries the backend for every search, filter and page change. Any user in the database can be found by name or email.

## Scope

### Backend: `GET /api/users` (Admin/Board only, same authorization as today)

Replace `skip`/`take` with the paging contract already used by family units (`FamilyUnitsEndpoints.cs:446-452`, `FamilyUnitsService.cs:380-410`, `FamilyUnitsRepository.cs:187-246`).

**Query parameters**

| Param | Type | Default | Rules |
|---|---|---|---|
| `page` | int | 1 | Values below 1 become 1 |
| `pageSize` | int | 20 | `Math.Clamp(pageSize, 1, 100)` |
| `search` | string? | null | Trim the value. Match case-insensitively (`EF.Functions.ILike` with `%term%`, escaping `%` and `_`) against `Email`, `FirstName`, `LastName` and `FirstName + ' ' + LastName`. |
| `role` | `UserRole`? | null | Optional exact filter |
| `isActive` | bool? | null | Optional exact filter |
| `sortBy` | string? | `createdAt` | Allowlist: `email`, `firstName`, `lastName`, `createdAt`, `role` |
| `sortOrder` | string? | `desc` | `asc` or `desc` |

**Response**: `ApiResponse<PagedUsersResponse>`

```csharp
public record PagedUsersResponse(
    IReadOnlyList<UserResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
```

This matches the frontend `PagedResult<T>` in `frontend/src/types/api.ts:13-21`. Do not change `UserResponse`.

**Files**

- `Features/Users/UsersModels.cs`: add `PagedUsersResponse` and, optionally, a `UserListQuery` record bound with `[AsParameters]` (the pattern `PaymentFilterRequest` uses).
- `Features/Users/IUsersRepository.cs` and `UsersRepository.cs`: replace `GetAllAsync(skip, take)` with `GetPagedAsync(query, ct)`. It returns `(List<User> items, int total)` and uses `AsNoTracking()`, one count query and one page query.
- `Features/Users/UsersService.cs`: clamp the inputs, map the results, compute `TotalPages`.
- `Features/Users/UsersEndpoints.cs`: new parameters and response type. Update the OpenAPI summary.
- Validation: reject an unknown `sortBy` or `sortOrder` with 400, or fall back to the default. Choose one and test it.

**Performance**: there are currently no indexes on `users.first_name`/`last_name`. At the expected size (a few thousand users) a sequential `ILIKE` scan is acceptable. Do not add `pg_trgm` in this ticket.

### Frontend

- `frontend/src/composables/useUsers.ts`
  - `fetchUsers(params: { page, pageSize, search?, role?, isActive?, sortBy?, sortOrder? })` calls `GET /users` with those params and stores `users` (items) plus `pagination` (`totalCount`, `page`, `pageSize`, `totalPages`). Mirror `useFamilyUnits.ts` (~245-280).
  - `createUser`, `deleteUser` and `toggleUserActive` must not patch the local array. They re-fetch the current page so the counts stay correct.
- `frontend/src/components/admin/UsersAdminPanel.vue`
  - Remove the `filteredUsers` computed.
  - `<DataTable lazy paginator :value="users" :total-records="pagination.totalCount" :rows="pageSize" :first="..." @page="onPage" @sort="onSort">`, following `FamilyUnitsAdminPanel.vue:189-197`.
  - Search box: `useDebounceFn` (300 ms) from `@vueuse/core`. A new search resets to page 1. Keep `data-testid="users-search-input"`.
  - Show the total ("N usuarios") and an empty state ("No se encontraron usuarios para «term»").
  - Ignore stale responses so a fast typist never sees results from an older term. Use a request counter or an `AbortController`.
- Dead code: `frontend/src/pages/UsersPage.vue` is unreachable, because `/users` redirects to `/admin/users` (`router/index.ts:265-272`). Delete it and its tests rather than migrating it to the new response shape.

### Out of scope

Camps (`GET /camps` has the same `take=100` pattern) are tracked below as a follow-up and are not part of this ticket.

## Acceptance criteria

1. With 150 or more users seeded, searching for the email or full name of the 150th user (by `CreatedAt`) returns that user.
2. Search is case-insensitive and matches a partial email, first name, last name, or "first last".
3. The paginator shows the server `totalCount`. Moving between pages triggers a request with the correct `page`/`pageSize`.
4. `pageSize=1000` returns at most 100 items.
5. Clearing the search returns to page 1 with the full, unfiltered total.
6. After creating, deleting or toggling a user, the table reflects the change without a full page reload.
7. Non-admin roles still receive 403 on `GET /api/users`.

## Tests (TDD: RED first)

**Backend (xUnit, `src/Abuvi.Tests`)**

- Unit tests for `UsersService`: page clamping, `TotalPages` math, sort allowlist.
- Integration tests for the repository/endpoint against Postgres:
  - search by email, first name, last name and full name, plus case-insensitivity
  - `%`/`_` escaping
  - `role` and `isActive` filters
  - paging boundaries
- Update `Integration/Features/ProtectedEndpointsTests.cs:155-220` (GET `/api/users`) for the new response shape and keep the authorization assertions.

**Frontend (Vitest)**

- `useUsers` sends the expected query params and maps the `PagedResult`.
- `UsersAdminPanel`:
  - typing calls `fetchUsers` once after the debounce, with `page: 1`
  - a page change calls `fetchUsers` with the new page
  - no client-side filtering remains
- Update any Cypress/Playwright spec that relies on `users-search-input`.

## Definition of done

- Backend and frontend tests pass, and the build is green.
- `ai-specs/specs/api-spec.yml` (or the current API docs) documents the new `GET /api/users` contract.
- The breaking response change is noted in the PR. The only consumer is `useUsers.ts`, which this ticket updates.
- Manual check on an environment with more than 100 users.

---

## Audit: other lists that filter only on the client

| Screen | Data source | Filtering | Backend limit | Status |
|---|---|---|---|---|
| **Admin users** | `GET /users` (no params) | Client | `take=100` | **BUG (this ticket)** |
| Camp locations (`views/camps/CampLocationsPage.vue:60-80`) | `GET /camps` (no params) | Client (name/description/isActive) | `take=100`, no search | **Latent bug**. 31 venues today, so not visible yet. |
| Camp dropdown in `CampEditionsPage.vue:34,82` | `GET /camps` | Dropdown | `take=100` | **Latent bug** (same cause) |
| Accommodation assignment (`AccommodationAssignmentPanel.vue:121-135`) | Full per-edition state | Client | Unpaged, scoped to one edition | OK (bounded set) |
| Payments pending review (`PaymentsReviewQueue.vue`) | Unpaged | None | — | Risk at volume only |
| Camp editions | Server filters, unpaged | Server | — | OK |
| Family units, registrations, all payments | Lazy + server search | Server | Paged | OK (reference pattern) |
| Media, memories, guests, memberships, zones, extras | Scoped lists | No search UI | — | OK |

**Follow-up ticket (suggested)**: `fix-camps-list-limit`. Either load camps with an explicit server search and paging using the same contract, or (minimum) have `GET /camps` return all camps when called for a dropdown or map, since the venue count is bounded. Low priority until the number of venues approaches 100.

# Backend Implementation Plan: fix-admin-users-server-search — Server-side search and pagination for `GET /api/users`

## 1. Overview

`GET /api/users` currently returns at most 100 users (`skip=0`, `take=100`, ordered by `CreatedAt`). It supports no search and returns no total count. The admin Users panel loads that list once and filters it in the browser, so users beyond the first 100 cannot be found.

This plan turns the endpoint into a server-side paged, searchable, filterable and sortable list. It follows the existing family-units reference implementation (`FamilyUnitsEndpoints.cs:446-452`, `FamilyUnitsService.cs:380-410`, `FamilyUnitsRepository.cs:187-246`).

Architecture: Vertical Slice. All changes stay inside `src/Abuvi.API/Features/Users/`. There are no schema changes and no migration.

Source spec: [fix-admin-users-server-search_enriched.md](./fix-admin-users-server-search_enriched.md)

## 2. Architecture Context

**Feature slice**: `src/Abuvi.API/Features/Users/`

| File | Change |
|---|---|
| `UsersModels.cs` | Add `UserListQuery` (request) and `PagedUsersResponse` (response) records |
| `IUsersRepository.cs` | Replace `GetAllAsync(skip, take)` with `GetPagedAsync(...)` |
| `UsersRepository.cs` | Implement filter, search, count, sort and page |
| `UsersService.cs` | Replace `GetAllAsync` with `GetPagedAsync`: normalize inputs, map results, compute `TotalPages` |
| `UsersEndpoints.cs` | New query parameters (`[AsParameters]`) and new response type in `.Produces<>` |

**Tests**

| File | Change |
|---|---|
| `src/Abuvi.Tests/Unit/Features/UsersServiceTests.cs` | Replace the `GetAllAsync` test (line ~102) with the new service tests |
| `src/Abuvi.Tests/Integration/Features/Users/UsersRepositoryPagingTests.cs` (new) | Repository tests on the EF InMemory provider, following the `MembershipDatabaseTests.cs` setup |
| `src/Abuvi.Tests/Integration/Features/ProtectedEndpointsTests.cs:155-220` | Deserialize `ApiResponse<PagedUsersResponse>` instead of `List<UserResponse>` |

**Cross-cutting**: none. `ApiResponse<T>` is reused unchanged, and authorization stays `RequireRole("Admin", "Board")`.

**Breaking change**: the response shape changes from `List<UserResponse>` to `PagedUsersResponse`. The only consumer is `frontend/src/composables/useUsers.ts`, which the frontend ticket updates. Both must ship in the same PR or release.

## 3. Key design decisions

1. **Search uses `ToLower().Contains(term)`, not `EF.Functions.ILike`.** This deviates from the enriched spec, for two reasons:
   - Many integration tests run on the EF InMemory provider, where `ILike` throws. `ToLower().Contains` works on both InMemory and Npgsql.
   - It matches the existing family-units search.

   Npgsql translates `Contains` into an escaped `LIKE` or `strpos`, so `%` and `_` in the term are treated literally. No manual escaping is needed.
2. **Unknown `sortBy`/`sortOrder` fall back to the default (`createdAt desc`) instead of returning 400.** This is consistent with family units, and the frontend only sends allowlisted values.
3. **Stable ordering.** Always add `.ThenBy(u => u.Id)` after the primary sort, so pages never overlap or skip rows when sort keys tie (for example, the same `CreatedAt` or `Role`).
4. **Full-name match.** `(FirstName + " " + LastName).ToLower().Contains(term)` lets you search "juan pérez". Searching first and last name separately is covered by the same OR.
5. **No new indexes.** At the expected size (a few thousand users), a scan is fine. `pg_trgm` is out of scope.

## 4. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/fix-admin-users-server-search-backend`.
- **Implementation Steps**:
  1. `git checkout dev`
  2. `git pull origin dev`
  3. `git checkout -b feature/fix-admin-users-server-search-backend`. If it already exists, check it out.
  4. Confirm with `git branch`.
- **Notes**:
  - The base branch is `dev`, not `main`, per the project Git workflow.
  - The `-backend` suffix is mandatory (`backend-standards.mdc` → Development Workflow).
  - Carry or stash the untracked spec folder `ai-specs/changes/fix-admin-users-server-search/` onto the new branch.

### Step 1: Entity / EF Core configuration

- No changes. `User` already has `Email`, `FirstName`, `LastName`, `Role` (stored as string, `UserConfiguration.cs:52`), `IsActive` and `CreatedAt`.

### Step 2: Request/Response DTOs

- **File**: `src/Abuvi.API/Features/Users/UsersModels.cs`
- **Action**: Add two records next to `UserResponse`:

```csharp
/// <summary>Query parameters for the paged admin users list.</summary>
public record UserListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    UserRole? Role = null,
    bool? IsActive = null,
    string? SortBy = null,     // email | firstName | lastName | createdAt | role
    string? SortOrder = null); // asc | desc

/// <summary>Paginated response envelope for the admin users list.</summary>
public record PagedUsersResponse(
    List<UserResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
```

- **Notes**:
  - `UserRole?` binds from the query string by name (`?role=Board`). Verify that binding works with `[AsParameters]`; `Program.cs` may need the JSON enum converter, which does not affect query binding.
  - Property names serialize as camelCase (`items`, `totalCount`, …), matching the frontend `PagedResult<T>` (`frontend/src/types/api.ts:13-21`). That type also has `hasNextPage` and `hasPreviousPage`. Family units does not return them either, so the frontend derives them; do not add them here.

### Step 3: Validation

- No FluentValidation validator is needed. Inputs are normalized in the service: page values below 1 become 1, and the page size is clamped to 1..100. Unknown sort values fall back to the default (decision 2).

### Step 4: Repository

- **File**: `IUsersRepository.cs`. Replace the `GetAllAsync` declaration with:

```csharp
/// <summary>Gets a filtered, sorted page of users and the total matching count.</summary>
Task<(List<User> Items, int TotalCount)> GetPagedAsync(
    int page, int pageSize, string? search, UserRole? role, bool? isActive,
    string? sortBy, string? sortOrder, CancellationToken cancellationToken = default);
```

- **File**: `UsersRepository.cs`. Replace `GetAllAsync` (lines 34-42).
- **Implementation Steps**:
  1. Start from `IQueryable<User> query = context.Users.AsNoTracking();`.
  2. If `role.HasValue`, add `query = query.Where(u => u.Role == role.Value);`.
  3. If `isActive.HasValue`, add `query = query.Where(u => u.IsActive == isActive.Value);`.
  4. If `!string.IsNullOrWhiteSpace(search)`:

     ```csharp
     var term = search.Trim().ToLower();
     query = query.Where(u =>
         u.Email.ToLower().Contains(term) ||
         u.FirstName.ToLower().Contains(term) ||
         u.LastName.ToLower().Contains(term) ||
         (u.FirstName + " " + u.LastName).ToLower().Contains(term));
     ```

  5. `var totalCount = await query.CountAsync(cancellationToken);`
  6. Sort with a switch on `(sortBy?.ToLowerInvariant(), sortOrder?.ToLowerInvariant())`:
     - `email`, `firstname`, `lastname`, `role`, `createdat`, each with `asc` or `desc`.
     - The default (`_`) is `OrderByDescending(u => u.CreatedAt)`.
     - Then apply `.ThenBy(u => u.Id)`. Type the result as `IOrderedQueryable<User>`, or apply `ThenBy` inside each switch arm.
  7. `var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);`
  8. `return (items, totalCount);`
- **Implementation Notes**:
  - The repository trusts the page and page size it receives. The service normalizes them.
  - Sorting by `Role` orders by the stored string (`Admin` < `Board` < `Member`). That order is acceptable.

### Step 5: Service

- **File**: `UsersService.cs`. Replace `GetAllAsync` (lines 22-29).

```csharp
/// <summary>Gets a paged, filtered list of users for administration.</summary>
public async Task<PagedUsersResponse> GetPagedAsync(UserListQuery query, CancellationToken cancellationToken = default)
```

- **Implementation Steps**:
  1. `var page = Math.Max(1, query.Page);`
  2. `var pageSize = Math.Clamp(query.PageSize, 1, 100);`
  3. Call `repository.GetPagedAsync(page, pageSize, query.Search, query.Role, query.IsActive, query.SortBy, query.SortOrder, cancellationToken)`.
  4. `var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);`
  5. Return `new PagedUsersResponse(items.Select(MapToResponse).ToList(), totalCount, page, pageSize, totalPages)`.
- **Notes**:
  - Reuse the existing `MapToResponse`.
  - Do not keep the old `GetAllAsync`; it has no other callers (verified by grep).

### Step 6: Endpoint

- **File**: `UsersEndpoints.cs`
- **Implementation Steps**:
  1. Mapping (lines 23-29): update the summary to `"Get paged users with search and filters"` and change the produced type to `.Produces<ApiResponse<PagedUsersResponse>>()`. Keep `.RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))`, `.Produces(401)` and `.Produces(403)`.
  2. Handler (lines 88-100):

     ```csharp
     private static async Task<IResult> GetAllUsers(
         [FromServices] UsersService service,
         [AsParameters] UserListQuery query,
         CancellationToken cancellationToken = default)
     {
         var result = await service.GetPagedAsync(query, cancellationToken);
         return Results.Ok(ApiResponse<PagedUsersResponse>.Ok(result));
     }
     ```

  3. Update the XML doc comment.
- **Notes**:
  - `[AsParameters]` on a record with default values binds `page`, `pageSize`, `search`, `role`, `isActive`, `sortBy` and `sortOrder` from the query string case-insensitively.
  - If binding a record with optional constructor parameters causes trouble, fall back to explicit `[FromQuery]` parameters, as in `GetAllFamilyUnits`.

### Step 7: Registration

- No changes. `UsersService` and `IUsersRepository` are already registered, and `MapUsersEndpoints` is already mapped.

### Step 8: Migration

- None. There are no schema changes.

### Step 9: Tests (TDD: write them first and watch them fail)

**9a. Unit tests for `UsersServiceTests.cs`** (NSubstitute repository)

- Successful cases:
  - `GetPagedAsync_ReturnsMappedItemsAndPagingMetadata`: the repository returns 2 users with `total=45` and `pageSize=20`, so `TotalPages=3`. Items are mapped.
  - `GetPagedAsync_PassesFiltersToRepository`: `search`, `role`, `isActive`, `sortBy` and `sortOrder` are forwarded unchanged.
- Edge cases:
  - `GetPagedAsync_PageBelowOne_NormalizesToOne`: `page=0` and `page=-3` both call the repository with `page=1`.
  - `GetPagedAsync_PageSizeAboveMax_ClampsTo100`: `pageSize=1000` calls the repository with 100.
  - `GetPagedAsync_PageSizeBelowOne_ClampsTo1`.
  - `GetPagedAsync_NoResults_ReturnsZeroTotalPages`.
- Remove the obsolete `GetAllAsync` test (line ~102).

**9b. Repository tests in the new `Integration/Features/Users/UsersRepositoryPagingTests.cs`** (EF InMemory, a unique database name per test class, as in `MembershipDatabaseTests.cs`)

Seed about 150 users with distinct `CreatedAt` values, mixed roles and mixed `IsActive` values, including:
- `Ana García <ana.garcia@example.com>`, created at index 149, which is the newest
- `ANA@upper.com`
- `user_1@example.com` and `user%x@example.com`

Tests:

| Test | Expected result |
|---|---|
| `Search_ByEmailFragment_FindsUserBeyondFirst100` | The core bug regression. `search="ana.garcia"` returns `Ana García` and `totalCount=1`. |
| `Search_IsCaseInsensitive` | `search="ana@UPPER"` finds `ANA@upper.com`. |
| `Search_ByFirstName`, `Search_ByLastName`, `Search_ByFullName` | `"garcía"` and `"ana garcía"` match. |
| `Search_TrimsWhitespace` | `"  ana.garcia  "` matches. |
| `Search_WithLikeWildcards_TreatedLiterally` | `"user_1"` does not match `userX1`. This must also be verified on Npgsql (see 9d). |
| `Filter_ByRole` | Only Board users are returned. |
| `Filter_ByIsActive` | Only active or only inactive users are returned. |
| `Paging_ReturnsRequestedSliceAndTotal` | `page=2`, `pageSize=20` returns 20 items, `totalCount=150`, and no overlap with page 1. |
| `Paging_LastPagePartial` | `page=8`, `pageSize=20` returns 10 items. |
| `Sort_DefaultIsCreatedAtDesc` | The first item is the newest. |
| `Sort_ByEmailAsc` | Items are ordered by email. |
| `Sort_UnknownField_FallsBackToDefault` | Same result as the default sort. |

**9c. Endpoint and authorization tests in `ProtectedEndpointsTests.cs:155-220`**

- Change `ReadFromJsonAsync<ApiResponse<List<UserResponse>>>` to `ApiResponse<PagedUsersResponse>` in the Admin and Board tests, and assert that `Data.Items` is not null and `Data.Page == 1`.
- Keep the 401 test (no token) and the 403 test (Member).
- Add `GetAllUsers_WithSearchQuery_Returns200AndFilteredItems`: `GET /api/users?search=<seeded admin email>` returns an item whose email matches.
- Add `GetAllUsers_WithPageSizeAbove100_ClampsPageSize`: `?pageSize=500` gives `Data.PageSize == 100`.

**9d. Optional Npgsql check**

- If the local Postgres test harness is available (see `CampHistoryQueryCountTests.cs:38`, which uses `UseNpgsql`), add one test there confirming that `ToLower().Contains` with `_` and `%` and accented terms behaves correctly on PostgreSQL. Otherwise, verify it manually with Swagger.

### Step 10: Update Technical Documentation

- **Action**: Document the new `GET /api/users` contract.
- **Implementation Steps**:
  1. `ai-specs/specs/api-endpoints.md` has no entry for `GET /api/users` (only `/api/users/me/camp-timeline` at line ~4498). Add a `### GET /api/users` section in the Users area covering authorization (Admin, Board), the query parameter table (`page`, `pageSize` default 20 and max 100, `search`, `role`, `isActive`, `sortBy`, `sortOrder` with the allowlist and default), the response example (`ApiResponse<PagedUsersResponse>`) and status codes (200, 401, 403).
  2. `ai-specs/specs/data-model.md`: no change, since there is no schema change.
  3. Check that Swagger/OpenAPI shows the new parameters and response type.
  4. Mark backend progress in the enriched spec if your workflow tracks it there.
- **References**: `ai-specs/specs/documentation-standards.mdc`. Write all documentation in English.
- **Notes**: This step is mandatory.

## 5. Implementation Order

0. Create branch `feature/fix-admin-users-server-search-backend` from `dev`.
1. Write the 9a unit tests and 9b repository tests. Expect RED, since they do not compile yet.
2. Step 2: DTOs.
3. Step 4: repository. The 9b tests turn GREEN.
4. Step 5: service. The 9a tests turn GREEN.
5. Step 6: endpoint, then update the 9c tests to GREEN.
6. Refactor: tidy the sort switch and the XML docs.
7. Run the full test suite: `dotnet test src/Abuvi.Tests`.
8. Step 10: documentation.

## 6. Testing Checklist

- [ ] `dotnet build` passes with no new warnings (nullable enabled).
- [ ] All new unit tests (9a) pass.
- [ ] All new repository tests (9b) pass, including the regression for a user beyond the first 100.
- [ ] `ProtectedEndpointsTests` GET `/api/users` tests (9c) pass with the new shape. 401 and 403 are preserved.
- [ ] Full `dotnet test` suite passes. No other test referenced `GetAllAsync` for users.
- [ ] Manual check in Swagger: `?search=`, `?role=Board`, `?isActive=false`, `?page=2&pageSize=5`, `?pageSize=1000`, `?sortBy=email&sortOrder=asc`.

## 7. Error Response Format

The `ApiResponse<T>` envelope is unchanged.

| Status | When |
|---|---|
| 200 | Success. `data` is `PagedUsersResponse`. An empty result is still 200, with `items: []`, `totalCount: 0` and `totalPages: 0`. |
| 400 | A malformed query value that cannot be bound, such as `page=abc` or `role=Foo`. This is the framework default; no custom validation is added. |
| 401 | No or invalid token. |
| 403 | Authenticated, but not Admin or Board. |

Example `200` response:

```json
{
  "success": true,
  "data": {
    "items": [ { "id": "…", "email": "ana.garcia@example.com", "firstName": "Ana", "lastName": "García", "phone": null, "role": "Member", "isActive": true, "emailVerified": true, "createdAt": "…", "updatedAt": "…" } ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1
  }
}
```

## 8. Partial Update Support

Not applicable; this is a read-only endpoint.

## 9. Dependencies

- No new NuGet packages.
- No EF Core migration.

## 10. Notes

- RGPD: the response carries the same fields as today, and access stays restricted to Admin and Board. Do not log search terms at Information level, because they may contain personal data such as names or emails.
- Do not add `pg_trgm` indexes or full-text search.
- All code, comments and docs are in English. User-facing messages are unaffected.
- `GET /camps` has the same `take=100` pattern. It is out of scope and tracked as a follow-up in the enriched spec.

## 11. Next Steps After Implementation

- Implement the frontend part (`fix-admin-users-server-search_frontend.md`, via `/plan-frontend-ticket` and then `/develop-frontend`): lazy DataTable, debounced search, and removal of `UsersPage.vue`.
- Merge backend and frontend together into `dev`, because the response shape change is breaking.
- Manually verify on an environment with more than 100 users that a previously invisible user can be found.

## 12. Implementation Verification

- [ ] Code quality: nullable reference types respected, no analyzer warnings, XML docs on public members.
- [ ] Functionality: 200/401/403 behave as specified, clamping works, search finds users beyond the old 100 limit.
- [ ] Testing: new code covered by xUnit, FluentAssertions and NSubstitute, with the target of 90% coverage on the changed methods.
- [ ] Integration: no migration required; the app starts and Swagger shows the new contract.
- [ ] Documentation: `api-endpoints.md` updated with `GET /api/users`.

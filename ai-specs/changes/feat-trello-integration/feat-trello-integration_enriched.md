# Enriched Spec: Trello Integration

## Overview

Integrate Trello into the Abuvi platform so that developers can connect their Trello account, select a board, view lists/cards, and link cards to GitHub branches and pull requests. This enables tracking spec-driven development progress directly from the platform.

---

## User Story

**As a** developer working simultaneously on multiple branches and pull requests,
**I want to** link my Trello cards to the branches and PRs I'm working on,
**So that** I can easily track task progress and ensure each card is completed as specs are delivered.

### Acceptance Criteria

1. A user can connect their Trello account via OAuth 1.0a from the platform.
2. A user can disconnect their Trello account at any time.
3. A user can select one Trello board to sync with the platform.
4. The platform displays the selected board's lists and cards.
5. A user can create new cards on the synced board.
6. A user can update card title and description from the platform.
7. A user can move cards between lists from the platform.
8. A user can link a card to a GitHub branch name and/or pull request URL.
9. Linked GitHub references are visible on the card detail view.
10. Only authenticated users with role `Admin` or `Board` can manage the Trello integration settings (connect/disconnect). All authenticated users can view and interact with cards.

---

## Technical Design

### 1. Data Models

#### `TrelloConnection` (new entity)

| Field | Type | Constraints | Description |
|---|---|---|---|
| `Id` | `Guid` | PK, `gen_random_uuid()` | Unique identifier |
| `UserId` | `Guid` | FK → `Users.Id`, NOT NULL | Owner of the connection |
| `AccessToken` | `string` | NOT NULL, encrypted | Trello OAuth access token |
| `AccessTokenSecret` | `string` | NOT NULL, encrypted | Trello OAuth token secret |
| `TrelloMemberId` | `string` | NOT NULL | Trello member ID |
| `TrelloUsername` | `string` | NOT NULL | Trello display username |
| `SelectedBoardId` | `string?` | nullable | Currently synced board ID |
| `SelectedBoardName` | `string?` | nullable | Cached board name |
| `IsActive` | `bool` | default `true` | Soft delete flag |
| `CreatedAt` | `DateTime` | UTC, auto | Creation timestamp |
| `UpdatedAt` | `DateTime` | UTC, auto | Last update timestamp |

#### `TrelloCardLink` (new entity)

| Field | Type | Constraints | Description |
|---|---|---|---|
| `Id` | `Guid` | PK, `gen_random_uuid()` | Unique identifier |
| `TrelloCardId` | `string` | NOT NULL | Trello card ID |
| `TrelloCardName` | `string` | NOT NULL | Cached card name |
| `GitBranch` | `string?` | nullable | Linked Git branch name |
| `PullRequestUrl` | `string?` | nullable, valid URL | Linked PR URL |
| `PullRequestNumber` | `int?` | nullable | PR number (extracted) |
| `LinkedByUserId` | `Guid` | FK → `Users.Id`, NOT NULL | Who created the link |
| `CreatedAt` | `DateTime` | UTC, auto | Creation timestamp |
| `UpdatedAt` | `DateTime` | UTC, auto | Last update timestamp |

### 2. Backend — API Endpoints

All endpoints under prefix `/api/trello`. Grouped via `MapGroup("trello")`.

#### 2.1 OAuth & Connection

| Method | URL | Auth | Role | Description |
|---|---|---|---|---|
| `GET` | `/api/trello/auth/url` | Yes | Admin, Board | Generate Trello OAuth authorize URL |
| `GET` | `/api/trello/auth/callback` | No* | — | OAuth callback (redirected by Trello). Exchanges request token for access token. *Public but validates state/token. |
| `GET` | `/api/trello/connection` | Yes | Any | Get current user's Trello connection status |
| `DELETE` | `/api/trello/connection` | Yes | Admin, Board | Disconnect Trello account (soft delete, revoke token) |

#### 2.2 Boards

| Method | URL | Auth | Role | Description |
|---|---|---|---|---|
| `GET` | `/api/trello/boards` | Yes | Any | List all boards for connected account |
| `PUT` | `/api/trello/boards/select` | Yes | Admin, Board | Select a board to sync |

**Request — Select Board:**
```json
{
  "boardId": "string",
  "boardName": "string"
}
```

#### 2.3 Lists & Cards

| Method | URL | Auth | Role | Description |
|---|---|---|---|---|
| `GET` | `/api/trello/boards/{boardId}/lists` | Yes | Any | Get lists with cards for a board |
| `POST` | `/api/trello/cards` | Yes | Any | Create a new card on a list |
| `PUT` | `/api/trello/cards/{cardId}` | Yes | Any | Update card title/description |
| `PUT` | `/api/trello/cards/{cardId}/move` | Yes | Any | Move card to a different list |

**Request — Create Card:**
```json
{
  "listId": "string",
  "name": "string",
  "description": "string?"
}
```

**Request — Update Card:**
```json
{
  "name": "string?",
  "description": "string?"
}
```

**Request — Move Card:**
```json
{
  "targetListId": "string"
}
```

#### 2.4 GitHub Links

| Method | URL | Auth | Role | Description |
|---|---|---|---|---|
| `POST` | `/api/trello/cards/{cardId}/links` | Yes | Any | Link a GitHub branch/PR to a card |
| `GET` | `/api/trello/cards/{cardId}/links` | Yes | Any | Get all GitHub links for a card |
| `DELETE` | `/api/trello/cards/{cardId}/links/{linkId}` | Yes | Any | Remove a GitHub link |

**Request — Create Link:**
```json
{
  "gitBranch": "string?",
  "pullRequestUrl": "string?"
}
```
*At least one of `gitBranch` or `pullRequestUrl` must be provided.*

**Response — Card Link:**
```json
{
  "id": "guid",
  "trelloCardId": "string",
  "trelloCardName": "string",
  "gitBranch": "string?",
  "pullRequestUrl": "string?",
  "pullRequestNumber": 123,
  "linkedByUserId": "guid",
  "createdAt": "2026-02-27T00:00:00Z"
}
```

### 3. Backend — Files to Create/Modify

Following **Vertical Slice Architecture** under `src/Abuvi.API/Features/Trello/`:

#### New Files

| File | Purpose |
|---|---|
| `Features/Trello/TrelloEndpoints.cs` | Minimal API endpoint definitions (MapGroup) |
| `Features/Trello/TrelloModels.cs` | Request/Response DTOs |
| `Features/Trello/TrelloService.cs` | Business logic (calls Trello API, manages connections) |
| `Features/Trello/ITrelloService.cs` | Service interface |
| `Features/Trello/TrelloRepository.cs` | Data access for `TrelloConnection` and `TrelloCardLink` |
| `Features/Trello/ITrelloRepository.cs` | Repository interface |
| `Features/Trello/TrelloValidators.cs` | FluentValidation rules for all requests |
| `Features/Trello/TrelloApiClient.cs` | HTTP client wrapper for Trello REST API |
| `Features/Trello/ITrelloApiClient.cs` | API client interface |
| `Features/Trello/TrelloHealthCheck.cs` | Health check for Trello API connectivity |
| `Data/Configurations/TrelloConnectionConfiguration.cs` | EF Core entity config |
| `Data/Configurations/TrelloCardLinkConfiguration.cs` | EF Core entity config |
| `Data/Entities/TrelloConnection.cs` | Domain entity |
| `Data/Entities/TrelloCardLink.cs` | Domain entity |

#### Modified Files

| File | Change |
|---|---|
| `Data/AbuviDbContext.cs` | Add `DbSet<TrelloConnection>` and `DbSet<TrelloCardLink>` |
| `Program.cs` | Register Trello services, HTTP client, health check |
| `appsettings.json` | Add `Trello` configuration section (API key, callback URL) |

#### Migration

- New migration: `AddTrelloIntegration` — creates `TrelloConnections` and `TrelloCardLinks` tables.

### 4. Backend — Configuration

Add to `appsettings.json` (secrets via user-secrets):

```json
{
  "Trello": {
    "ApiKey": "<from-user-secrets>",
    "ApiSecret": "<from-user-secrets>",
    "CallbackUrl": "http://localhost:5079/api/trello/auth/callback"
  }
}
```

### 5. Frontend — Pages & Components

#### New Pages

| Route | File | Description |
|---|---|---|
| `/trello` | `pages/trello/TrelloBoardPage.vue` | Main board view: lists as columns, cards as draggable items |
| `/trello/settings` | `pages/trello/TrelloSettingsPage.vue` | Connect/disconnect, select board |

#### New Components

| Component | Location | Description |
|---|---|---|
| `TrelloConnectButton.vue` | `components/trello/` | OAuth connect/disconnect button |
| `TrelloBoardSelector.vue` | `components/trello/` | Dropdown to pick a board |
| `TrelloList.vue` | `components/trello/` | Single column (list) with cards |
| `TrelloCard.vue` | `components/trello/` | Card item with title, labels, link indicators |
| `TrelloCardDialog.vue` | `components/trello/` | Dialog for creating/editing a card |
| `TrelloCardLinkDialog.vue` | `components/trello/` | Dialog for linking GitHub branch/PR to a card |
| `TrelloCardLinkBadge.vue` | `components/trello/` | Badge showing linked branch/PR on a card |

#### New Composable

| File | Purpose |
|---|---|
| `composables/useTrello.ts` | API calls: connection status, boards, lists, cards, CRUD, links |

#### New Types

| File | Purpose |
|---|---|
| `types/trello.ts` | TypeScript interfaces: `TrelloConnection`, `TrelloBoard`, `TrelloList`, `TrelloCard`, `TrelloCardLink`, request/response DTOs |

#### Router Changes

| File | Change |
|---|---|
| `router/index.ts` | Add `/trello` and `/trello/settings` routes (auth-guarded) |

#### Navigation

| File | Change |
|---|---|
| `components/layout/Sidebar.vue` (or equivalent) | Add "Trello" navigation item with icon |

### 6. Frontend — UI Behavior

- **Board View**: Kanban-style columns using CSS grid or flexbox. Each list is a column, cards are stacked vertically.
- **Card Drag & Drop**: Use a lightweight library (e.g., `vuedraggable` or PrimeVue's `OrderList`) to move cards between lists. On drop, call `PUT /api/trello/cards/{cardId}/move`.
- **Card Detail**: Clicking a card opens `TrelloCardDialog` with editable title/description and a list of GitHub links.
- **Link Indicators**: Cards with GitHub links show a small branch/PR icon badge.
- **Loading States**: Skeleton loaders while fetching board data.
- **Error Handling**: Toast notifications for API errors (PrimeVue `Toast`).

---

## Non-Functional Requirements

### Security
- **Token Encryption**: Trello OAuth tokens must be encrypted at rest in the database (use AES-256 or ASP.NET Data Protection API).
- **Secret Management**: `ApiKey` and `ApiSecret` stored in user-secrets, never in committed config.
- **CSRF Protection**: OAuth callback must validate the `oauth_verifier` and match the request token stored in the user's session/temp store.
- **Input Validation**: All Trello IDs validated as non-empty strings. PR URLs validated as valid GitHub PR URLs (`https://github.com/{owner}/{repo}/pull/{number}`).
- **Rate Limiting**: Trello API has a rate limit of 100 requests per 10-second window per token. Implement basic rate-limit awareness (retry with backoff on 429).

### Performance
- **Caching**: Cache board lists/cards in memory for 30 seconds to reduce Trello API calls during rapid navigation.
- **Lazy Loading**: Only fetch card details when the card dialog is opened, not on board load.
- **Pagination**: If a board has many cards, paginate or virtualize the card list.

### Reliability
- **Health Check**: `TrelloHealthCheck` pings Trello API `/1/members/me` to verify connectivity.
- **Graceful Degradation**: If Trello API is unreachable, show cached data with a warning banner.
- **Token Revocation**: On disconnect, revoke the token via Trello API before deleting locally.

---

## Testing Strategy (TDD)

### Backend Unit Tests (`tests/Abuvi.Tests/Features/Trello/`)

| Test File | Covers |
|---|---|
| `TrelloServiceTests.cs` | Business logic: connect, disconnect, select board, CRUD cards, link management |
| `TrelloRepositoryTests.cs` | Data access: CRUD for `TrelloConnection` and `TrelloCardLink` |
| `TrelloValidatorsTests.cs` | Validation: all request DTOs, edge cases (empty strings, invalid URLs) |
| `TrelloApiClientTests.cs` | HTTP client: mock Trello API responses, error handling, rate limiting |
| `TrelloEndpointsTests.cs` | Endpoint authorization: role checks, unauthenticated access |

### Frontend Unit Tests

| Test File | Covers |
|---|---|
| `composables/__tests__/useTrello.spec.ts` | API calls, loading states, error handling |
| `components/trello/__tests__/TrelloCard.spec.ts` | Rendering, click events, link badge display |
| `components/trello/__tests__/TrelloCardDialog.spec.ts` | Form validation, submit/cancel events |
| `components/trello/__tests__/TrelloCardLinkDialog.spec.ts` | Link form validation, at-least-one-field rule |

### E2E Tests (`cypress/e2e/trello/`)

| Test | Scenario |
|---|---|
| `trello-connect.cy.ts` | Full OAuth flow (mocked Trello redirect) |
| `trello-board.cy.ts` | View board, create card, move card, link PR |

---

## Implementation Steps (ordered)

### Phase 1: Backend Foundation
1. **RED**: Write `TrelloConnection` and `TrelloCardLink` entity tests
2. **GREEN**: Create entities, EF configurations, migration
3. **RED**: Write `ITrelloRepository` / `TrelloRepository` tests
4. **GREEN**: Implement repository
5. **RED**: Write `ITrelloApiClient` tests (mock HTTP)
6. **GREEN**: Implement Trello API client with `HttpClient`
7. **RED**: Write `TrelloService` tests
8. **GREEN**: Implement service (OAuth flow, board selection, card CRUD, link management)
9. **RED**: Write validator tests
10. **GREEN**: Implement `FluentValidation` validators
11. **RED**: Write endpoint authorization tests
12. **GREEN**: Implement `TrelloEndpoints.cs` with `MapGroup`
13. Register services in `Program.cs`, add configuration, add health check
14. Run migration against local database

### Phase 2: Frontend Foundation
15. Define TypeScript types in `types/trello.ts`
16. **RED**: Write `useTrello` composable tests
17. **GREEN**: Implement `useTrello.ts` composable
18. Add routes to `router/index.ts`

### Phase 3: Frontend UI
19. Build `TrelloSettingsPage` (connect/disconnect, board selector)
20. Build `TrelloBoardPage` with `TrelloList` and `TrelloCard` components
21. Build `TrelloCardDialog` (create/edit)
22. Build `TrelloCardLinkDialog` (link branch/PR)
23. Add Trello item to sidebar navigation

### Phase 4: Polish
24. Add loading skeletons and error toasts
25. Add drag-and-drop for card movement
26. Write E2E tests
27. Update project documentation

---

## Dependencies

| Dependency | Type | Purpose |
|---|---|---|
| Trello API Key + Secret | Config | OAuth 1.0a authentication |
| `Microsoft.AspNetCore.DataProtection` | NuGet (built-in) | Token encryption at rest |
| `vuedraggable` (or similar) | npm | Drag-and-drop cards between lists |

---

## Out of Scope

- Real-time sync (webhooks from Trello) — can be added later.
- Multi-board sync — only one board at a time for now.
- Card labels, due dates, attachments management — future enhancement.
- Bidirectional GitHub ↔ Trello sync (e.g., auto-moving cards when a PR is merged).

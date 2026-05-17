# Frontend Implementation Plan: feat-e2e-test-credentials-setup — E2E Sandbox Infrastructure

## Overview

This plan sets up the Cypress + frontend side of the E2E sandbox infrastructure. The backend (separate `abuvi_e2e` database, E2E launch profile, `RegistrationSeeder`) is already implemented. This plan covers:

- `cy.login(role)` command for programmatic authentication (bypasses the UI login form)
- `cypress.env.json` with dev-only credentials (gitignored)
- `cypress.config.ts` updated with `env.API_URL` pointing to the E2E API port (5080)
- `frontend/package.json` `e2e:seed` script
- `.claude/e2e-credentials.md` reference file for Claude sessions

No Vue components, composables, or Pinia stores are involved — this is pure Cypress + tooling infrastructure.

## Architecture Context

- **Auth store**: `frontend/src/stores/auth.ts` uses `TOKEN_KEY = 'abuvi_auth_token'` and `USER_KEY = 'abuvi_user'`. The `setAuth()` action writes both to `localStorage`. `cy.login()` must replicate both writes.
- **Login API**: `POST http://localhost:5080/api/auth/login` → `response.body.data` → `{ token: string, user: { id, email, firstName, lastName, role, ... } }`
- **E2E API port**: 5080 (dev API runs on 5079 — both can run simultaneously)
- **Cypress support file**: `frontend/cypress/support/commands.ts` (TypeScript, `export {}` at bottom, `declare global` block present)
- **Cypress config**: `frontend/cypress.config.ts` — no `env` or `setupNodeEvents` currently

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Ensure work is on the correct feature branch
- **Branch**: `feature/feat-e2e-test-credentials-setup-frontend`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/feat-e2e-test-credentials-setup-frontend`
  3. `git branch` to verify

### Step 1: Create `.claude/e2e-credentials.md`

- **File**: `.claude/e2e-credentials.md` (repo root, NOT gitignored)
- **Action**: Create a reference file Claude reads at the start of every E2E session
- **Implementation Steps**:
  1. Create `.claude/` directory if it doesn't exist
  2. Write the file with the content below
- **Content**:

```markdown
# E2E Local Test Credentials

Frontend URL:  http://localhost:5173
API URL (E2E): http://localhost:5080/api
DB sandbox:    abuvi_e2e (NEVER touch abuvi_prod)

## Test Users

| Role   | Email               | Password      | FamilyUnit    |
| ------ | ------------------- | ------------- | ------------- |
| Admin  | admin@abuvi.local   | Admin@123456  | —             |
| Board  | board@abuvi.local   | Board@123456  | Lopez Family  |
| Member | member1@abuvi.local | Member@123456 | Garcia Family |

## Pre-seeded Registrations (Camp Costa 2027)

| FamilyUnit    | Status    | Total | Payments               |
| ------------- | --------- | ----- | ---------------------- |
| Garcia Family | Pending   | 600 € | 0                      |
| Lopez Family  | Confirmed | 180 € | 1 Transfer (Completed) |

## E2E Setup (first time or after reset)

1. `docker compose up -d`
2. `cd src/Abuvi.API && dotnet run --launch-profile E2E`  ← creates abuvi_e2e + runs migrations
3. `cd frontend && npm run e2e:seed`                      ← seeds test data
4. `cd frontend && npm run dev`
5. `cd frontend && npx cypress open`

## Reset the sandbox

`cd frontend && npm run e2e:seed`  (idempotent — resets and re-seeds abuvi_e2e)
```

- **Notes**: This file contains only local dev credentials. Never store production secrets here.

### Step 2: Add `cypress.env.json` to `frontend/.gitignore`

- **File**: `frontend/.gitignore`
- **Action**: Add `cypress.env.json` so credentials are never committed
- **Implementation Steps**:
  1. Append `cypress.env.json` to `frontend/.gitignore`

### Step 3: Create `frontend/cypress.env.json`

- **File**: `frontend/cypress.env.json` (gitignored)
- **Action**: Create with all E2E credentials + API URL
- **Content**:

```json
{
  "ADMIN_EMAIL": "admin@abuvi.local",
  "ADMIN_PASSWORD": "Admin@123456",
  "BOARD_EMAIL": "board@abuvi.local",
  "BOARD_PASSWORD": "Board@123456",
  "MEMBER_EMAIL": "member1@abuvi.local",
  "MEMBER_PASSWORD": "Member@123456",
  "API_URL": "http://localhost:5080/api"
}
```

- **Notes**: Cypress merges `cypress.env.json` into `Cypress.env()` automatically. Values here override `env` in `cypress.config.ts`.

### Step 4: Update `frontend/cypress.config.ts`

- **File**: `frontend/cypress.config.ts`
- **Action**: Add `env.API_URL` default (overridden by `cypress.env.json` at runtime)
- **Current content**: No `env` or `setupNodeEvents`
- **Implementation Steps**:
  1. Add `env: { API_URL: 'http://localhost:5080/api' }` inside the `e2e` config object
- **Result**:

```typescript
import { defineConfig } from 'cypress'

export default defineConfig({
  e2e: {
    projectId: '2oziu4',
    baseUrl: 'http://localhost:5173',
    specPattern: 'cypress/e2e/**/*.cy.{js,jsx,ts,tsx}',
    supportFile: 'cypress/support/e2e.ts',
    env: {
      API_URL: 'http://localhost:5080/api',
    },
  },
})
```

### Step 5: Add `cy.login()` to `frontend/cypress/support/commands.ts`

- **File**: `frontend/cypress/support/commands.ts`
- **Action**: Add programmatic login command that sets both auth localStorage keys
- **Implementation Steps**:
  1. Add the `Cypress.Commands.add('login', ...)` implementation before `export {}`
  2. Add `login(role?: 'admin' | 'board' | 'member'): Chainable<void>` to the `Chainable` interface inside the existing `declare global` block
- **Implementation**:

```typescript
Cypress.Commands.add('login', (role: 'admin' | 'board' | 'member' = 'member') => {
  const credentials = {
    admin:  { email: Cypress.env('ADMIN_EMAIL'),  password: Cypress.env('ADMIN_PASSWORD') },
    board:  { email: Cypress.env('BOARD_EMAIL'),  password: Cypress.env('BOARD_PASSWORD') },
    member: { email: Cypress.env('MEMBER_EMAIL'), password: Cypress.env('MEMBER_PASSWORD') },
  }
  const { email, password } = credentials[role]
  cy.request({
    method: 'POST',
    url: `${Cypress.env('API_URL')}/auth/login`,
    body: { email, password },
  }).then((response) => {
    const { token, user } = response.body.data
    localStorage.setItem('abuvi_auth_token', token)
    localStorage.setItem('abuvi_user', JSON.stringify(user))
  })
})
```

- **Type declaration** (inside the existing `Chainable` interface in `declare global`):

```typescript
/**
 * Authenticates programmatically via the E2E API. Sets both auth localStorage keys.
 * Requires `cypress.env.json` with credentials and `API_URL`.
 * @param role - 'admin' | 'board' | 'member' (default: 'member')
 * @example cy.login('admin')
 */
login(role?: 'admin' | 'board' | 'member'): Chainable<void>
```

- **Implementation Notes**:
  - `abuvi_auth_token` = the JWT token string
  - `abuvi_user` = JSON-stringified user object (mirrors what `setAuth()` in `auth.ts` stores)
  - Must set BOTH keys or the app won't recognize the session
  - `cy.request()` does not use the browser, so it bypasses CSRF and avoids UI flakiness

### Step 6: Add `e2e:seed` script to `frontend/package.json`

- **File**: `frontend/package.json`
- **Action**: Add a script that runs the Setup CLI targeting `abuvi_e2e`
- **Implementation Steps**:
  1. Add `"e2e:seed"` to the `scripts` section
- **Script**:

```json
"e2e:seed": "dotnet run --project ../src/Abuvi.Setup run-all --connection \"Host=localhost;Port=5432;Database=abuvi_e2e;Username=abuvi_user;Password=dev_password\""
```

- **Notes**:
  - `run-all` = reset + import CSVs + `RegistrationSeeder`
  - The `--connection` flag ensures `abuvi_prod` is never touched
  - Script is idempotent — safe to run multiple times

### Step 7: Update Technical Documentation

- **Action**: Review and update documentation to reflect the E2E sandbox infrastructure
- **Implementation Steps**:
  1. Review `ai-specs/specs/frontend-standards.mdc` — add note about `cy.login()` pattern and `cypress.env.json` setup under the Cypress/testing section if it exists
  2. Check `ai-specs/specs/api-spec.yml` — no changes needed (no new endpoints)
  3. Verify `.claude/e2e-credentials.md` is accurate and complete (created in Step 1)
- **Notes**: Documentation must be in English.

## Implementation Order

1. Step 0: Create feature branch
2. Step 1: Create `.claude/e2e-credentials.md`
3. Step 2: Add `cypress.env.json` to `frontend/.gitignore`
4. Step 3: Create `frontend/cypress.env.json`
5. Step 4: Update `frontend/cypress.config.ts`
6. Step 5: Add `cy.login()` to `frontend/cypress/support/commands.ts`
7. Step 6: Add `e2e:seed` script to `frontend/package.json`
8. Step 7: Update documentation

## Testing Checklist

- [ ] `npm run e2e:seed` completes without errors and targets `abuvi_e2e`
- [ ] `cy.login('admin')` sets `abuvi_auth_token` and `abuvi_user` in localStorage; app navigates to home
- [ ] `cy.login('board')` same — board role visible in UI
- [ ] `cy.login('member')` same — member role visible in UI
- [ ] `cy.login()` (no arg) defaults to `'member'`
- [ ] `Cypress.env('API_URL')` resolves to `http://localhost:5080/api` inside a test
- [ ] Existing tests (`auth.cy.ts`, `users.cy.ts`, etc.) still pass with no changes
- [ ] `cypress.env.json` is NOT tracked by git (`git status` shows nothing after creating it)
- [ ] After `e2e:seed`, `abuvi_e2e` has 2 registrations: Garcia (Pending, 600€) and Lopez (Confirmed, 180€, 1 payment)

## Error Handling Patterns

- If `cy.login()` gets a non-200 response, Cypress will fail the test immediately (no silent failure)
- If `cypress.env.json` is missing, `Cypress.env('ADMIN_EMAIL')` returns `undefined` — the request will fail with a clear 400/401 error from the API
- If the E2E API is not running, `cy.request()` will time out with a Cypress `ECONNREFUSED` error — no special handling needed

## Dependencies

No new npm packages required. All tooling already present:
- `cypress` — already installed
- `dotnet` CLI — required for `e2e:seed` (must be in PATH)

## Notes

- `cy.login()` must never navigate to the login page — it calls the API directly and writes to localStorage. The test then uses `cy.visit()` to navigate to the target page.
- The `cy.login()` approach is faster than UI-based login (~200ms vs ~2s) and avoids coupling auth tests to unrelated features.
- `cypress.env.json` takes precedence over `env` in `cypress.config.ts` — this is by Cypress design. The config file default is a fallback only.
- Do not add `cypress.env.json` to `.claude/` memory — `.claude/e2e-credentials.md` is the canonical reference.
- The `e2e:seed` script runs from the `frontend/` directory, so `../src/Abuvi.Setup` is correct relative path.
- TypeScript: `commands.ts` uses `export {}` to make it a module — keep it at the bottom.

## Next Steps After Implementation

- Run `npm run e2e:seed` once with the E2E API running to verify the full setup end-to-end
- Write a smoke test (`cypress/e2e/auth.cy.ts` or similar) using `cy.login()` to validate the command works across all three roles
- Share `.claude/e2e-credentials.md` with team (it contains only local dev credentials, safe to commit)

## Implementation Verification

- **Code Quality**: TypeScript types added to `Chainable` interface, no `any` types
- **Functionality**: `cy.login()` sets correct localStorage keys matching `auth.ts` constants
- **Testing**: Manual verification of all three roles in Cypress interactive mode
- **Integration**: `e2e:seed` → `cy.login()` → `cy.visit('/')` flow works end-to-end
- **Documentation**: `.claude/e2e-credentials.md` created and accurate

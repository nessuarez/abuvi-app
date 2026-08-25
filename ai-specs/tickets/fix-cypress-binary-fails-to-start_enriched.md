# Bug Fix: The Cypress Binary Fails to Start, So No E2E Test Can Run Locally

## Problem

`npx cypress run` and `npx cypress verify` both fail immediately on the development machine:

```
C:\Users\<user>\AppData\Local\Cypress\Cache\15.10.0\Cypress\Cypress.exe: bad option: --smoke-test
C:\Users\<user>\AppData\Local\Cypress\Cache\15.10.0\Cypress\Cypress.exe: bad option: --ping=922

Cypress failed to start.
Platform: win32-x64 (Microsoft Windows 11 Pro N - 10.0.26200)
Cypress Version: 15.10.0
```

This is not specific to one spec: **no E2E test in the repository can be executed**. All nine specs
under `frontend/cypress/e2e/` are equally blocked.

## Why it matters more than it looks

There is **no CI that runs tests** in this repository — the only workflow is
`.github/workflows/changelog.yml`. Cypress therefore runs in exactly one place: a developer's
machine, by hand. With the binary broken, the E2E suite is not "occasionally skipped", it is
**completely unreachable**, and nothing anywhere reports that fact.

The immediate consequence already materialised: `frontend/cypress/e2e/anniversary-journey.cy.ts`
(6 tests, added with the anniversary journey in PR #287) was **committed and merged without ever
being executed once**. Its flows were verified by other means — driving Chrome through the DevTools
Protocol against the real 50 camp editions — but the spec file itself has zero evidence of working.
A test that has never run is, statistically, a broken test.

## What was verified about the environment

Diagnosis stopped at "the binary is present and will not start". These are facts, checked directly:

| Check | Result |
| --- | --- |
| `Cypress.exe` present in the cache | Yes — 205 MB, dated 7 Feb 2026 |
| `resources/app` (the packaged application) | Present and populated |
| Version inside the cached binary | 15.10.0 |
| Version of the npm package | 15.10.0 — **they match** |
| Cached versions present | `13.17.0` and `15.10.0` |
| `debug.log` contents | Only crashpad noise: `CreateFile: El sistema no puede encontrar el archivo especificado (0x2)`, `not connected` |

So the usual suspects are ruled out: the binary is not missing, not truncated, and not a version
mismatch with the npm package.

## Hypotheses, not conclusions

Nothing below has been confirmed. Listed in the order worth trying.

1. **Corrupt extraction that passes a size check.** `Cypress.exe` falls back to plain Electron
   argument parsing when it cannot load its own application bundle, which is exactly what
   `bad option: --smoke-test` looks like. A forced reinstall replaces the whole tree.
2. **Windows 11 Pro *N* edition.** N editions ship without the Media Feature Pack. Electron-based
   applications are a known casualty. Installing the Media Feature Pack would settle this.
3. **Two cached versions interfering.** `13.17.0` and `15.10.0` coexist under
   `AppData\Local\Cypress\Cache`. Clearing the cache entirely removes the question.

One suspicious detail spotted while reading the packaged metadata, possibly meaningless: the cached
`resources/app/package.json` contains `"electronNodeVersion": "\r\n22.19.0"` — a stray CRLF inside
the version string.

## How to reproduce

```bash
cd frontend
npx cypress verify
```

Expected: `Verified Cypress!`. Actual: the `bad option` failure above.

## Suggested fix

Try in order, verifying after each step:

1. **Force a clean reinstall of the binary:**
   ```bash
   cd frontend
   npx cypress cache clear
   npx cypress install --force
   npx cypress verify
   ```
2. If it still fails, **install the Microsoft Media Feature Pack** for Windows N and retry.
3. If it still fails, capture the real error with debug logging on and attach it to this ticket:
   ```bash
   DEBUG=cypress:* npx cypress verify
   ```

## Once Cypress runs again

The point of fixing this is not the tool, it is the untested spec it left behind.

1. **Run `frontend/cypress/e2e/anniversary-journey.cy.ts` and make it pass, or delete it.** Do not
   leave it in the repository unexecuted — it gives false coverage. Expect it to need work on first
   run: selectors, waits, and especially credentials.
2. **Check the seeded database.** `cy.login('member')` reads `MEMBER_EMAIL` / `MEMBER_PASSWORD` from
   Cypress env vars, and the suite expects the `abuvi_e2e` database seeded via `npm run e2e:seed`.
   That database was lost when the Docker volume was cleared and has not been recreated, so the
   other eight specs may fail for that reason rather than this one. **These are two separate
   problems and should not be conflated.**
3. **Run the whole suite once**, so the true state of the E2E tests is known rather than assumed.

## Out of scope

- Recreating the `abuvi_e2e` database (related, separate).
- Adding CI that runs the test suites. Worth doing — a broken test runner went unnoticed precisely
  because nothing checks — but it is its own ticket.

## Affected Files

### Files to Modify

| File | Change |
| --- | --- |
| `frontend/cypress/e2e/anniversary-journey.cy.ts` | Run it; fix what fails, or remove the file |

### Reference Files

| File | Notes |
| --- | --- |
| `frontend/cypress.config.ts` | `baseUrl` 5173, `API_URL` 5080 — both differ from what a local run may actually use |
| `frontend/cypress/support/commands.ts` | `cy.login()` and the env vars it depends on |
| `frontend/package.json` | `cypress`, `cypress:run` and `e2e:seed` scripts |

## Notes

- No application code is at fault here. This is local tooling.
- Nothing about this blocks the 50th anniversary work: Phases 2 and 3 are merged into `dev` and were
  verified against the real data by other means.

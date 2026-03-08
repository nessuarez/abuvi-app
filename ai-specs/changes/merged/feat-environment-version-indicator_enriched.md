# Feature: Environment & Version Indicator

## Problem

There is no way to visually identify which environment (Development / Production) the user is on, nor what version of the application is deployed. This causes confusion — especially in dev/demo environments where testers or stakeholders might think they're in production.

## Requirements

### 1. Footer version & environment info (all pages with layout)

Show in the existing `AppFooter.vue` a small line with:

- **App version** following SemVer (e.g., `v1.2.3`)
- **Git commit short SHA** (e.g., `abc1234`) — useful for debugging
- **Environment name** (e.g., `Development`, `Production`)

Example rendering:

```
v1.2.3 (abc1234) · Development
```

### 2. "DEMO" badge on the Login/Landing page (dev only)

On the landing page (`LandingPage.vue`), display a prominent **"DEMO"** badge when the environment is not production. This helps users immediately know they're on a demo/test environment. The badge should:

- Be clearly visible (e.g., top-right corner floating badge, or above the auth container)
- Use a warning/info color scheme (e.g., orange/amber background)
- Not appear in production

## Technical Approach

### Build-time variable injection via `vite.config.ts`

Vite bakes `VITE_*` and `define` values at build time. Add these build-time constants:

**`frontend/vite.config.ts`** — add `define` block:

```ts
import { execSync } from 'child_process'

const commitHash = execSync('git rev-parse --short HEAD').toString().trim()
const packageVersion = process.env.npm_package_version || '0.0.0'

export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(packageVersion),
    __COMMIT_HASH__: JSON.stringify(commitHash),
  },
  // ...existing config
})
```

### New environment variables

**`frontend/.env.development`** — add:

```
VITE_APP_ENV=development
```

**Production** (supplied at Docker build time):

```
VITE_APP_ENV=production
```

### SemVer versioning strategy

- Update `frontend/package.json` from `"0.0.0"` to a meaningful version (e.g., `"0.1.0"` since the app is in early development)
- Version bumps follow standard SemVer: `MAJOR.MINOR.PATCH`
  - `MAJOR`: Breaking changes for users
  - `MINOR`: New features
  - `PATCH`: Bug fixes
- Version is read from `package.json` at build time and injected via `define`

### TypeScript declarations

**`frontend/src/env.d.ts`** (or extend existing `vite-env.d.ts`):

```ts
declare const __APP_VERSION__: string
declare const __COMMIT_HASH__: string
```

## Affected Files

### Files to Modify

| File | Change |
|---|---|
| `frontend/vite.config.ts` | Add `define` block with `__APP_VERSION__` and `__COMMIT_HASH__` |
| `frontend/package.json` | Update `version` from `"0.0.0"` to `"0.1.0"` |
| `frontend/.env.development` | Add `VITE_APP_ENV=development` |
| `frontend/src/vite-env.d.ts` | Add type declarations for `__APP_VERSION__` and `__COMMIT_HASH__` |
| `frontend/src/components/layout/AppFooter.vue` | Add version/env line at the bottom |
| `frontend/src/views/LandingPage.vue` | Add "DEMO" badge for non-production environments |

### Files to Create

None — all changes are modifications to existing files.

### Reference Files

| File | Notes |
|---|---|
| `frontend/src/components/layout/AppFooter.vue` | Existing footer — will add version line |
| `frontend/src/views/LandingPage.vue` | Landing page — will add DEMO badge |
| `frontend/src/main.ts` | Already uses `import.meta.env.MODE` for Sentry — reference for env usage pattern |

## Implementation Steps

### 1. Update `package.json` version

- Change `"version": "0.0.0"` to `"version": "0.1.0"`

### 2. Add build-time constants in `vite.config.ts`

- Import `execSync` from `child_process`
- Read git commit hash via `git rev-parse --short HEAD`
- Read version from `process.env.npm_package_version`
- Add `define` block to inject `__APP_VERSION__` and `__COMMIT_HASH__`

### 3. Add TypeScript declarations

- In `frontend/src/vite-env.d.ts`, declare `__APP_VERSION__` and `__COMMIT_HASH__` as global `string` constants

### 4. Add `VITE_APP_ENV` to `.env.development`

- Add `VITE_APP_ENV=development`
- Production builds should set `VITE_APP_ENV=production` via Docker build args

### 5. Update `AppFooter.vue`

- Add a small muted text line at the very bottom of the footer showing:
  - `v{version} ({commitHash}) · {environment}`
- Read `__APP_VERSION__`, `__COMMIT_HASH__`, and `import.meta.env.VITE_APP_ENV`
- Style: small, muted (`text-xs text-gray-400`), right-aligned or centered below existing copyright

### 6. Add "DEMO" badge to `LandingPage.vue`

- Show only when `import.meta.env.VITE_APP_ENV !== 'production'`
- Position: fixed top-right corner or above the auth container
- Style: amber/orange background, white text, rounded pill badge, e.g.:

  ```html
  <div class="fixed top-4 right-4 z-50 rounded-full bg-amber-500 px-4 py-1 text-sm font-bold text-white shadow-lg">
    DEMO
  </div>
  ```

### 7. Update `Dockerfile` for production builds

- Ensure `VITE_APP_ENV=production` is set as an `ARG`/`ENV` in the Docker build stage so it gets baked into the production bundle

## Acceptance Criteria

- [ ] Footer on authenticated pages shows version number, commit hash, and environment name
- [ ] Version follows SemVer format (e.g., `v0.1.0`)
- [ ] Commit hash is the short SHA of the build commit
- [ ] Environment shows "Development" in dev, "Production" in production
- [ ] Landing page displays a visible "DEMO" badge in non-production environments
- [ ] "DEMO" badge does NOT appear in production
- [ ] TypeScript compilation passes with no errors
- [ ] No new dependencies introduced

## Non-Functional Requirements

- **Security**: No sensitive information (full commit SHA, internal URLs, API keys) exposed. Short SHA and version are safe.
- **Performance**: Build-time injection adds zero runtime overhead — values are constants compiled into the bundle.
- **Maintainability**: Version in `package.json` is the single source of truth. Commit hash is auto-generated at build time.

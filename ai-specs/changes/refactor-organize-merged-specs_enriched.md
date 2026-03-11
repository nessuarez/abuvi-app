# Reorganize `ai-specs/changes/merged/` folder

## Problem

The `ai-specs/changes/merged/` folder has grown to **82 items** (48 directories + 34 loose `.md` files, ~5.8MB) all at the root level with no structure. This makes it difficult to:

- Find specs related to a specific feature area when planning new work
- Understand the history and evolution of a particular domain
- Onboard new team members or AI agents to the project context
- Avoid re-reading large amounts of irrelevant specs when working on a focused task

## Solution

### Phase 1: Reorganize by Feature Domain

Move all items in `merged/` into **category subfolders** organized by the primary functional domain they belong to. The proposed structure:

```
ai-specs/changes/merged/
├── 01-infrastructure/          # DevOps, logging, DB setup, health checks, scaffolding
├── 02-auth-and-users/          # Authentication, user management, roles, password recovery
├── 03-family-units/            # Family definitions, relationships, memberships, import
├── 04-camps-definition/        # Camp CRUD, editions, extras, navigation, places
├── 05-camp-registration/       # Registration flow, accommodations, extras, guardians, amendments
├── 06-payments/                # Payment instructions, pricing
├── 07-ui-ux-and-layout/        # Layout, hero carousel, admin sidebar, input UX, profile
├── 08-i18n-legal-content/      # Translations, inclusive language, legal pages
├── 09-email-notifications/     # Resend integration, welcome emails, registration emails
├── 10-onboarding/              # Onboarding tools
├── 11-tech-debt-and-qa/        # Tech debt, deprecated components, bug tracking
└── 12-events-and-special/      # 50th anniversary mock, one-off events
```

Numeric prefixes ensure consistent ordering and make it clear the grouping is intentional.

### Proposed item assignment

#### `01-infrastructure/` (10 items)

- `scaffolding-base-project_enriched.md`
- `feat-auto-migrate-on-startup/`
- `feat-dbsetup-logging/`
- `feat-logging/`
- `feat-generate-schemas/`
- `feat-heathcheck-improvements/`
- `feat-seq-healthcheck_enriched.md`
- `fix-seq-logging-visibility_backend.md`
- `fix-seq-logging-visibility_enriched.md`
- `feat-environment-version-indicator_enriched.md`

#### `02-auth-and-users/` (8 items)

- `feature-auth/`
- `feature-users/`
- `user-registration-workflow/`
- `user-role-changes/`
- `feat-forgot-password/`
- `feat-i18n-user-management/`
- `fix-verify-email-blank-page_enriched.md`
- `fix-login-background-image-production_enriched.md`

#### `03-family-units/` (10 items)

- `feat-family-units-definition/`
- `feat-expand-family-relationships/`
- `feat-family-creation-validations_enriched.md`
- `feat-family-unit-detail-admin-board_enriched.md`
- `feat-import-csv-families/`
- `feat-bulk-family-membership/`
- `feat-membership-and-guests/`
- `feat-my-memberships-dialog/`
- `feat-member-data-missing-warnings/`
- `fix-family-member-birthdate-timezone_enriched.md`

#### `04-camps-definition/` (9 items)

- `feat-camps-definition/`
- `feat-camp-editions-management/`
- `feat-campedition-description/`
- `feat-camps-extra-data/`
- `feat-camps-extra-caracteristics-from-xls/`
- `feat-camps-navigation-menu/`
- `feat-google-places-camps/`
- `feat-import-historical-camp-editions/`
- `dev-testing-camp-editions-strategy_enriched.md`

#### `05-camp-registration/` (17 items)

- `feat-camps-registration/`
- `feat-camps-registration-period/`
- `feat-camps-accommodation/`
- `feat-registration-accommodations/`
- `feat-registration-extra-fields2/`
- `feat-registration-extras-observations-field/`
- `feat-registration-guardian-prefill/`
- `feat-registration-legal-notice_enriched.md`
- `feat-pricing-preview-confirm-step_enriched.md`
- `feat-open-edition-amendments/`
- `camp-registration-dropdown-bug_enriched.md`
- `camp-registration-period-ux-improvement_enriched.md`
- `fix-registration-detail-btn/`
- `fix-registration-post-submit-ux/`
- `fix-registration-participant-checkbox-selection_enriched.md`
- `fix-registrations-not-listed_enriched.md`
- `fix-no-family-unit-registration-message_enriched.md`

#### `06-payments/` (2 items)

- `feat-payment-instructions-email_enriched.md`
- `fix-payment-instructions-duplicated_enriched.md`

#### `07-ui-ux-and-layout/` (11 items)

- `feat-layout-frontend/`
- `feat-home-hero-carousel_enriched.md`
- `add-whatsapp-community-hero-slide_enriched.md`
- `feat-admin-sidebar-navigation_enriched.md`
- `feat-admin-user-search_enriched.md`
- `feat-my-profile-layout/`
- `feat-date-input-ux_enriched.md`
- `feat-phone-input-ux_enriched.md`
- `feat-ux-improvements/`
- `hide-health-fields_enriched.md`
- `feat-extras-user-input-and-hide-zero-prices_enriched.md`

#### `08-i18n-legal-content/` (4 items)

- `feat-spanish-texts/`
- `lenguaje-inclusivo_enriched.md`
- `feat-legal-pages-cleanup_enriched.md`
- `feat-legal-section/`

#### `09-email-notifications/` (3 items)

- `feat-resend-integration/`
- `feat-registration-email-notification/`
- `feat-welcome-email-bcc-junta_enriched.md`

#### `10-onboarding/` (2 items)

- `feat-onboarding-tools/`
- `feat-onboarding-tools_frontend.md`

#### `11-tech-debt-and-qa/` (3 items)

- `tech-debt/`
- `tech-debt-primevue-deprecated-components/`
- `feat-bugs-tracking/`

#### `12-events-and-special/` (3 items)

- `feat-mock-50-aniversary.md`
- `feat-mock-50-aniversary_enriched.md`
- `feat-mock-50-anniversary/`

---

### Phase 2: Automate archival of completed specs

Create a script/command to move specs from `ai-specs/changes/` to the organized `merged/` structure after a feature is completed and merged.

#### Option A: Shell script (`ai-specs/.commands/archive-spec.sh`)

A simple Bash script that:

1. Accepts the spec name (file or folder) and target category as arguments
2. Moves the item from `ai-specs/changes/` to `ai-specs/changes/merged/<category>/`
3. Validates the category exists (or offers to create it)
4. Outputs a confirmation message

```bash
# Usage examples:
./ai-specs/.commands/archive-spec.sh feat-family-member-access 03-family-units
./ai-specs/.commands/archive-spec.sh feat-family-member-access_enriched.md 03-family-units
```

#### Option B: Claude Code custom command (`/archive-spec`)

A Claude Code slash command (`.md` file in `ai-specs/.commands/`) that:

1. Reads the spec name from arguments
2. Analyzes the spec content to auto-detect the best category
3. Moves the spec to the correct subfolder
4. Reports what was moved and where

**Recommendation**: Start with Option A (simple, reliable) and add Option B later for AI-assisted auto-categorization.

#### Category detection heuristic (for Option B)

The command can use keyword matching on the spec name and content to suggest a category:

| Keywords in name/content | Suggested category |
| --- | --- |
| `auth`, `login`, `password`, `user`, `role`, `verify-email` | `02-auth-and-users` |
| `family`, `member`, `membership`, `relationship` | `03-family-units` |
| `camp`, `edition`, `camp-definition` | `04-camps-definition` |
| `registration`, `accommodation`, `guardian`, `participant` | `05-camp-registration` |
| `payment`, `pricing`, `price` | `06-payments` |
| `layout`, `sidebar`, `hero`, `carousel`, `ux`, `input`, `profile` | `07-ui-ux-and-layout` |
| `i18n`, `spanish`, `legal`, `inclusiv` | `08-i18n-legal-content` |
| `email`, `notification`, `resend`, `welcome` | `09-email-notifications` |
| `onboarding` | `10-onboarding` |
| `tech-debt`, `deprecated`, `bug` | `11-tech-debt-and-qa` |
| `logging`, `health`, `migrate`, `schema`, `scaffold`, `dbsetup`, `seq` | `01-infrastructure` |

---

### Phase 3: Generate Development History Log (`DEVELOPMENT_LOG.md`)

Analyze all specs in `merged/` and produce a single Markdown document that acts as a **historical registry of everything developed**. This document serves two purposes:

1. **Human-readable project history** — quick reference for the team
2. **Changelog source** — consumable by the `ChangelogPage.vue` frontend component (see `ai-specs/changes/feat-changelog-page_frontend.md`) which renders markdown via `marked`

#### Output format

The document should be structured **chronologically** (newest first), grouped by month, with each entry containing:

```markdown
# Development Log

## March 2026

### feat-pricing-preview-confirm-step
- **Idea**: 2026-03-10 | **Deployed**: 2026-03-10
- Added a pricing preview and confirmation step before submitting camp registrations, showing a breakdown of costs per participant including extras and accommodations.

### feat-welcome-email-bcc-junta
- **Idea**: 2026-03-09 | **Deployed**: 2026-03-10
- Configured welcome emails to BCC the board (junta) so they are notified of new user registrations.

### fix-payment-instructions-duplicated
- **Idea**: 2026-03-09 | **Deployed**: 2026-03-10
- Fixed duplicated payment instruction emails being sent when a registration was updated.

...

## February 2026

### scaffolding-base-project
- **Idea**: 2026-02-06 | **Deployed**: 2026-02-18
- Initial project scaffolding: .NET 8 backend with vertical slice architecture, Vue 3 + PrimeVue frontend, Docker Compose setup, CI/CD pipeline, and base authentication flow.
  - Backend: ASP.NET Core Web API, EF Core with PostgreSQL, MediatR, FluentValidation
  - Frontend: Vue 3 + TypeScript, Vite, PrimeVue 4, Tailwind CSS 4, Pinia
  - Infrastructure: Docker Compose, GitHub Actions, Seq logging
```

#### Date extraction logic

- **Idea date** (`first_seen`): Earliest git commit date that introduced any file in the spec folder/file (`git log --diff-filter=A --follow --format="%ai"` — take the oldest)
- **Deploy date** (`last_modified`): Most recent git commit date touching any file in the spec folder/file (`git log -1 --format="%ai"`)
- Both dates already collected for all 82 items (see implementation)

#### Summary generation

For each spec, the AI agent should:

1. Read the spec content (the `_enriched.md` file or the main file in the folder)
2. Generate a **1-sentence summary** of what was built/fixed
3. Optionally add **2-5 bullet points** if the change was substantial (e.g., scaffolding, major features with multiple sub-components)
4. Categorize the entry type: `feat`, `fix`, `refactor`, `tech-debt`

#### Compatibility with Changelog Page

The `ChangelogPage.vue` component (spec: `feat-changelog-page_frontend.md`) fetches data from GitHub Releases API and renders markdown with `marked`. The `DEVELOPMENT_LOG.md` follows the same markdown conventions so it can optionally be:

- Published as a GitHub Release body (one release per month or per sprint)
- Rendered directly in the app as a "Full Development History" companion to the release changelog
- Used as source material for generating concise GitHub Release notes

#### Output file

- **Path**: `ai-specs/changes/merged/DEVELOPMENT_LOG.md`
- **Update strategy**: Re-generated or appended each time a new spec is archived via the Phase 2 automation

---

## Implementation Steps

### Phase 1 - Manual reorganization

1. Create the 12 category subfolders under `ai-specs/changes/merged/`
2. Move each item to its assigned category subfolder (using `git mv` to preserve history)
3. Add a `README.md` at `ai-specs/changes/merged/README.md` listing the categories and their purpose
4. Commit and push

### Phase 2 - Automation

1. Create `ai-specs/.commands/archive-spec.sh` with basic move + validation logic
2. Create `ai-specs/.commands/archive-spec.md` as a Claude Code slash command for AI-assisted archival
3. Update `ai-specs/.commands/enrich-us.md` to mention the archival step after a spec is enriched and implemented
4. Test both approaches with a real spec from `ai-specs/changes/`

### Phase 3 - Development history log

1. Create a Claude Code command `/generate-dev-log` that:
   - Scans all specs in `merged/` (recursing into category subfolders)
   - Extracts git dates (idea + deploy) for each item
   - Reads each spec and generates a 1-line summary (+ optional bullets)
   - Outputs `ai-specs/changes/merged/DEVELOPMENT_LOG.md` sorted by deploy date descending, grouped by month
2. Run the command to generate the initial `DEVELOPMENT_LOG.md` from all 82 existing specs
3. Update the `/archive-spec` command to append new entries to `DEVELOPMENT_LOG.md` when archiving

## Acceptance Criteria

- [ ] All 82 items in `merged/` are organized into category subfolders
- [ ] No items remain at the root of `merged/` (only subfolders)
- [ ] A `README.md` in `merged/` documents the category structure
- [ ] `archive-spec.sh` script works and moves specs to the correct category
- [ ] Git history is preserved for moved files (using `git mv`)
- [ ] Existing references to merged specs in other docs are not broken (verify with grep)
- [ ] `DEVELOPMENT_LOG.md` exists with all 82 specs summarized, dated, and grouped by month
- [ ] `DEVELOPMENT_LOG.md` renders correctly as markdown (compatible with `marked` parser)
- [ ] `/generate-dev-log` command can regenerate the full log from scratch
- [ ] `/archive-spec` appends to `DEVELOPMENT_LOG.md` when archiving a new spec

## Non-Functional Requirements

- **Backwards compatibility**: If any tooling reads from `merged/` directly, it must be updated to recurse into subfolders
- **Convention over configuration**: New categories should only be added when there are 3+ specs that don't fit existing ones
- **Naming**: Keep the numeric prefix convention (`NN-category-name`) for ordering
- **Changelog compatibility**: `DEVELOPMENT_LOG.md` must use standard markdown renderable by `marked` (headings, lists, bold, links — no custom syntax)

## Out of Scope

- Changing the content of any spec files
- Reorganizing `ai-specs/specs/` (separate concern)
- Automated PR/branch creation for archival (can be added later)
- Publishing releases to GitHub Releases API (separate task, can consume `DEVELOPMENT_LOG.md` as input)

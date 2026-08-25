# Changes Index

Overview of all pending and in-progress specs in `ai-specs/changes/`. Each entry links to its folder, shows available spec files, and reflects merge status into `dev`.

**Legend**

- `[feat]` / `[fix]` / `[refactor]` / `[test]` — change type
- Backend `[x]` = `_backend.md` exists | Frontend `[x]` = `_frontend.md` exists
- **In progress** = actively being built | **Planned** = spec ready, not started | **Spec only** = no backend/frontend spec yet | **Merged** = merged in dev branch

---

## Features

### feat-3-payments

**[feat]** · [folder](./feat-3-payments) · **Planned**

Add structured concept descriptions to payments showing what each installment covers (members, periods, prices), plus sequential payment enforcement and a manual payment creation flow for admins to handle exceptions.

- Backend: [x] | Frontend: [x]

---

### feat-admin-edit-profiles

**[feat]** · [folder](./feat-admin-edit-profiles) · **Planned**

Enable Admin/Board users to edit User, FamilyUnit, and FamilyMember profiles (names, phone, document number) and fix a security bug in `PUT /api/users/{id}` that lacks ownership checks.

- Backend: [x] | Frontend: [x]

---

### feat-camp-edition-edit-fullpage

**[feat]** · [folder](./feat-camp-edition-edit-fullpage) · **Planned**

Replace the cramped CampEdition edit modal with a full-page form at `/camps/editions/:id/edit` with proper data loading, date field fixes, and status-aware field disabling for Open editions.

- Backend: [ ] | Frontend: [x]

---

### feat-camp-edition-extras

**[feat]** · [folder](./feat-camp-edition-extras) · **Planned**

Manage extras (optional add-ons) attached to camp editions — creation, ordering, and registration flow integration.

- Backend: [x] | Frontend: [x]

---

### feat-camp-edition-out-modal

**[feat]** · [folder](./feat-camp-edition-out-modal) · **Planned**

Move CampEdition editing out of the modal into a dedicated full-page form — spec mirrors `feat-camp-edition-edit-fullpage`.

- Backend: [ ] | Frontend: [x]

---

### feat-camps-payments

**[feat]** · [folder](./feat-camps-payments) · **Planned**

Enable bank transfer payments for camp registrations (50/50 split installments), transfer proof upload, and move payment deadlines from global settings to per-edition configuration.

- Backend: [x] | Frontend: [x]

---

### feat-csv-bank-import

**[feat]** · [folder](./feat-csv-bank-import) · **Planned**

Parse Banc Sabadell Norma43 bank statement exports, fuzzy-match SEPA CORE debit transactions to membership fees, and bulk-confirm payments with human review and correction capability.

- Backend: [x] | Frontend: [x]

---

### feat-current-camp-landing

**[feat]** · [folder](./feat-current-camp-landing) · **Planned**

Redesign `CampPage.vue` into a rich landing page with photos, description, map, accommodation, extras, and pricing; extend `GET /api/camps/current` to return camp photos, extras, and accommodation details.

- Backend: [x] | Frontend: [x]

---

### feat-db-setup-tool

**[feat]** · [folder](./feat-db-setup-tool) · **Planned**

Backend tooling for database setup and seeding — no enriched spec available yet.

- Backend: [x] | Frontend: [ ]

---

### feat-delete-camp-registration

**[feat]** · [folder](./feat-delete-camp-registration) · **Planned**

Allow hard deletion of camp registrations within 24 hours of creation (representative) or anytime (admin) to enable correcting mistakes, with time-window and payment guards to prevent unintended deletions.

- Backend: [x] | Frontend: [x]

---

### feat-family-iban-direct-debit

**[feat]** · [folder](./feat-family-iban-direct-debit) · **Spec only**

Store a per-family IBAN (encrypted at rest) plus the account holder's identity and address for direct debits, notify the board by email on every change, and warn families that an outdated IBAN makes them liable for the returned-receipt surcharge. The SEPA mandate stays on paper — no RUM, sequence types or digital signature.

- Backend: [ ] | Frontend: [ ]

---

### feat-media-50-aniversary

**[feat]** · [folder](./feat-media-50-aniversary) · **Planned**

Replace static mock on `/anniversary` page with real file uploads and persistence; create Memory and MediaItem entities, API endpoints, and approval workflow for user-submitted photos, videos, and memories.

- Backend: [x] | Frontend: [x]

---

### feat-membership-numbers-admin-filter

**[feat]** · [folder](./feat-membership-numbers-admin-filter) · **Planned**

Auto-assign sequential member numbers and family numbers, add filtering by membership status in admin panels, and make member numbers editable by Admin/Board with unique constraints.

- Backend: [x] | Frontend: [x]

---

### feat-registration-activities

**[feat]** · [folder](./feat-registration-activities) · **Spec only**

Activity sign-up system extracted from `feat-registration-extra-fields2`: structured entities for camp activities (cooking, hikes, sports, etc.) with per-edition conditions/requirements and registration flow integration.

- Backend: [ ] | Frontend: [ ]

---

### feat-registration-edit-recalculate

**[feat]** · [folder](./feat-registration-edit-recalculate) · **Spec only**

Enable editing of registrations (members and extras) after creation with automatic payment recalculation; block edits once proofs are uploaded and provide payment breakdown visibility.

- Backend: [ ] | Frontend: [ ]

---

### feat-registration-status-flow

**[feat]** · [folder](./feat-registration-status-flow) · **Spec only**

Introduce intermediate registration statuses tied to installment confirmation (instead of jumping from Pending directly to Confirmed) and add transactional email notifications on each status transition.

- Backend: [ ] | Frontend: [ ]

---

### feat-trello-integration

**[feat]** · [folder](./feat-trello-integration) · **Spec only**

Synchronize Trello board automatically from Claude Code using MCP server, with `ai-specs/changes/` as source of truth and Trello as visual mirror of task status.

- Backend: [ ] | Frontend: [ ]

---

### feat-ux-improvements

**[feat]** · [folder](./feat-ux-improvements) · **Spec only**

Broad UX improvements: navigation simplification, responsiveness, visual design refresh, and camp edition definition workflow improvements.

- Backend: [ ] | Frontend: [ ]

---

## Fixes

### fix-duplicate-family-member-email

**[fix]** · [folder](./fix-duplicate-family-member-email) · **Planned**

Prevent duplicate emails in family members by validating that a member's email doesn't match the representative's email or other family members' emails, with real-time frontend validation and helpful UI hints.

- Backend: [x] | Frontend: [x]

---

### fix-userback-load-and-buttons

**[fix]** · [folder](./fix-userback-load-and-buttons) · **Planned**

Fix Userback widget not loading and homepage buttons not responding due to race condition in `App.vue` where `ub.init()` is called before Userback script finishes loading.

- Backend: [ ] | Frontend: [x]

---

## Other

### refactor-organize-merged-specs *(root-level spec)*

**[refactor]** · [spec](./refactor-organize-merged-specs_enriched.md) · **Planned**

Reorganize the `ai-specs/changes/merged/` folder (82 items, 5.8 MB) into 12 category subfolders by functional domain (infrastructure, auth, family units, camps, registration, payments, UI/UX, i18n, email, onboarding, tech debt, events).

---

### test-isolation *(root-level spec)*

**[test]** · [spec](./test-isolation_enriched.md) · **Planned**

Implement database isolation in integration tests using Testcontainers (ephemeral PostgreSQL containers) so test execution never modifies the development database and provides a clean schema for each test run.

---

### feat-trello-integration *(root-level spec)*

**[feat]** · [spec](./feat-trello-integration_enriched.md) · **Spec only**

Meta-spec: defines task ID conventions and workflow mapping from Claude Code commands to Trello board columns, with `ai-specs/changes/` as the source of truth.

---

*Last updated: 2026-04-22*

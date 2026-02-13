# AI Specs Organization Guide

This guide explains how to organize specifications, user stories, and implementation documents in the `ai-specs/` directory.

## Directory Structure

```
ai-specs/
├── .agents/              # Agent-specific configurations
├── .commands/            # Slash command definitions (/plan-backend-ticket, /develop-backend, etc.)
├── changes/              # Feature development specifications (organized by feature)
├── specs/                # Global project standards and reference docs
└── tickets/              # Actionable work items (bugs, refactors, small tasks)
```

---

## When to Use Each Directory

### 📁 `changes/` - Feature Development

**Use for:** Complete features that require planning, user stories, and implementation across multiple files.

**Structure:**

```
changes/
└── feat-{feature-name}/
    ├── {feature-name}-user-stories.md       # User stories with acceptance criteria
    ├── {feature-name}-enriched.md           # Detailed specification after enrichment
    ├── {feature-name}_backend.md            # Backend implementation details (optional)
    ├── {feature-name}_frontend.md           # Frontend implementation details (optional)
    ├── IMPLEMENTATION_PLAN.md               # TDD step-by-step implementation plan
    └── phase{N}_*.md                        # Phase-specific documents (if multi-phase)
```

**Examples:**

- `changes/feat-camps-definition/` - Camp CRUD feature
- `changes/feature-auth/` - Authentication system
- `changes/user-registration-workflow/` - User registration flow
- `changes/feat-resend-integration/` - Email service integration

**When to create a feature folder:**

- New user-facing functionality
- Backend features with multiple endpoints
- Features requiring database schema changes
- Multi-phase features with dependencies
- Features requiring both backend and frontend work

**Naming convention:**

- Use prefix: `feat-` for features, `feature-` also acceptable
- Use kebab-case: `feat-camps-definition`, not `feat_camps_definition`
- Be descriptive: `feat-user-profile-management`, not `feat-profile`

---

### 📁 `tickets/` - Work Items

**Use for:** Specific, actionable work items that can be completed independently.

**Structure:**

```
tickets/
├── REFACTOR-001-{short-description}.md
├── BUG-042-{short-description}.md
├── TASK-015-{short-description}.md
└── TECH-DEBT-003-{short-description}.md
```

**Examples:**

- `REFACTOR-001-consolidate-test-projects.md` - Consolidate NET 10 tests to NET 9
- `BUG-042-fix-password-validation.md` - Fix regex in password validator
- `TASK-015-update-packages.md` - Update all NuGet packages to latest
- `TECH-DEBT-003-remove-unused-services.md` - Clean up unused service registrations

**When to create a ticket:**

- Bug fixes (single issue)
- Refactoring tasks
- Technical debt cleanup
- Package updates
- Performance optimizations (isolated)
- Documentation fixes
- Code quality improvements

**Ticket Format:**

```markdown
# {TICKET-ID}: {Short Title}

## Problem Statement
Brief description of the issue or need

## Goal
What we want to achieve

## User Story (optional)
As a [role]
I want to [action]
So that [benefit]

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2

## Technical Notes
Implementation details, conversion guides, etc.

## Definition of Done
- [ ] Code complete
- [ ] Tests passing
- [ ] Documented
```

**Naming convention:**

- Prefix with type: `REFACTOR-`, `BUG-`, `TASK-`, `TECH-DEBT-`
- Use sequential numbers: `001`, `002`, `003`
- Keep description short: `consolidate-test-projects`, not `consolidate-all-test-projects-into-one-net9-project`

---

### 📁 `specs/` - Global Standards

**Use for:** Project-wide standards, conventions, and reference documentation.

**Current files:**

```
specs/
├── api-endpoints.md              # Complete API endpoint reference
├── backend-standards.mdc         # Backend coding standards
├── base-standards.mdc            # General project standards
├── data-model.md                 # Database schema reference
├── development_guide.md          # Setup and development instructions
├── documentation-standards.mdc   # Documentation conventions
└── frontend-standards.mdc        # Frontend coding standards
```

**When to add here:**

- Coding standards and conventions
- Architecture decisions (ADRs)
- API documentation
- Database schema references
- Development environment setup
- Testing strategies
- Deployment procedures

**DO NOT add:**

- Feature-specific documentation (use `changes/` instead)
- Implementation plans (use `changes/{feature}/IMPLEMENTATION_PLAN.md`)
- Work items or tickets (use `tickets/`)

---

## Decision Tree: Where Should My Document Go?

```
Is this a complete feature requiring user stories and implementation?
├─ YES → changes/feat-{name}/
│
├─ NO → Is it a bug fix, refactor, or small task?
│       ├─ YES → tickets/{TYPE}-{ID}-{description}.md
│       │
│       └─ NO → Is it a project-wide standard or reference?
│               ├─ YES → specs/{standard-name}.md
│               │
│               └─ NO → Unclear? Ask for clarification!
```

---

## Workflow: From Idea to Implementation

### For Features

1. **Create feature folder:** `changes/feat-{name}/`
2. **Write user stories:** `{name}-user-stories.md`
3. **Enrich specification:** Use `/enrich-us` command → `{name}-enriched.md`
4. **Plan backend:** Use `/plan-backend-ticket` → `IMPLEMENTATION_PLAN.md`
5. **Plan frontend:** Use `/plan-frontend-ticket` → `{name}_frontend.md`
6. **Implement:** Use `/develop-backend` and `/develop-frontend`
7. **Document completion:** Add `phase{N}_completion_report.md` if applicable

### For Tickets

1. **Create ticket:** `tickets/{TYPE}-{ID}-{description}.md`
2. **Fill template:** Problem, Goal, Acceptance Criteria, Technical Notes
3. **Implement:** Use `/develop-backend` or `/develop-frontend` with ticket reference
4. **Close:** Mark as complete when all acceptance criteria met

---

## Best Practices

### Feature Organization

✅ **DO:**

- Keep all related documents in the same feature folder
- Use consistent naming: `{feature-name}-user-stories.md`, `{feature-name}-enriched.md`
- Create `IMPLEMENTATION_PLAN.md` for TDD approach
- Document each phase if multi-phase feature
- Link related files with relative paths

❌ **DON'T:**

- Scatter feature docs across multiple folders
- Mix multiple features in one folder
- Skip user stories for new features
- Create overly generic feature names

### Ticket Management

✅ **DO:**

- Use descriptive but concise ticket names
- Include clear acceptance criteria
- Reference related features or docs
- Update ticket status in commit messages
- Close with a completion summary

❌ **DON'T:**

- Create tickets for features (use `changes/` instead)
- Skip problem statement
- Leave tickets without acceptance criteria
- Forget to link PRs to tickets

### Documentation Maintenance

✅ **DO:**

- Update `specs/api-endpoints.md` when endpoints change
- Update `specs/data-model.md` when schema changes
- Keep standards docs current
- Archive completed features (optional)
- Link to CLAUDE.md for AI-specific instructions

❌ **DON'T:**

- Duplicate content between `specs/` and `changes/`
- Let standards docs become outdated
- Mix implementation details with standards
- Forget to update the development guide

---

## Examples

### Feature: Camp Registration System

```
changes/feat-camps-registration/
├── camp-registration-flow.md            # Initial spec
├── camp-registration-enriched.md        # Enriched with details
├── IMPLEMENTATION_PLAN.md               # TDD backend plan
├── camp-registration_frontend.md        # Frontend implementation
└── phase1_completion_report.md          # Phase 1 summary
```

### Ticket: Consolidate Test Projects

```
tickets/REFACTOR-001-consolidate-test-projects.md
```

- Self-contained refactoring task
- Clear problem and solution
- Acceptance criteria defined
- Implementation completed in single PR

### Standard: Backend Coding Rules

```
specs/backend-standards.mdc
```

- Project-wide conventions
- Applies to all backend code
- Referenced by all developers
- Updated as standards evolve

---

## Migration Guide

If you have documents in the wrong location:

1. **Features in `specs/`** → Move to `changes/feat-{name}/`
2. **Tickets in `changes/`** → Move to `tickets/{TYPE}-{ID}/`
3. **Standards in `changes/`** → Move to `specs/`
4. **Update all internal links** after moving files

---

## Summary

| Location | Purpose | Examples |
|----------|---------|----------|
| **`changes/feat-*/`** | Complete features with user stories | Camp CRUD, User Auth, Email Integration |
| **`tickets/`** | Bugs, refactors, small tasks | Test consolidation, bug fixes, package updates |
| **`specs/`** | Project standards and references | Coding standards, API docs, data model |
| **`.commands/`** | Slash command definitions | `/plan-backend-ticket`, `/develop-backend` |
| **`.agents/`** | Agent configurations | Agent-specific settings |

---

## Questions?

- **"Should I create a feature or a ticket?"**
  → If it requires user stories and affects multiple files → **Feature**
  → If it's a single focused task → **Ticket**

- **"Where do I document API changes?"**
  → Update `specs/api-endpoints.md` (reference)
  → Document in feature folder for context (implementation)

- **"Can I have sub-folders in `changes/`?"**
  → Yes! Use `changes/feat-{name}/backend/`, `changes/feat-{name}/frontend/` if needed

- **"Should I archive old features?"**
  → Optional. You can move to `changes/_archive/` if desired, but not required

---

**Last Updated:** 2026-02-13
**Maintained By:** Development Team + Claude Code

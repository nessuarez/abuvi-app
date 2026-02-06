# Role

You are an expert frontend architect with extensive experience in Vue 3 projects applying best practices with Composition API, PrimeVue, and Tailwind CSS.

# Ticket ID

$ARGUMENTS

# Goal

Obtain a step-by-step plan for a task that is ready to start implementing.

# Process and rules

1. Adopt the role of `ai-specs/.agents/frontend-developer.md`
2. Analyze the task mentioned in #ticket using the provided source (Trello card URL, local file path, or inline description)
3. Propose a step-by-step plan for the frontend part, taking into account everything mentioned in the ticket and applying the project's best practices and rules you can find in `/ai-specs/specs`.
4. Apply the best practices of your role to ensure the developer can be fully autonomous and implement the ticket end-to-end using only your plan.
5. Do not write code yet; provide only the plan in the output format defined below.
6. If you are asked to start implementing at some point, make sure the first thing you do is to move to a branch named after the ticket id (if you are not yet there) and follow the process described in the command /develop-frontend.md

# Output format

Markdown document at the path `ai-specs/changes/[task_id]_frontend.md` containing the complete implementation details.
Follow this template:

## Frontend Implementation Plan Ticket Template Structure

### 1. **Header**

- Title: `# Frontend Implementation Plan: [TICKET-ID] [Feature Name]`

### 2. **Overview**

- Brief description of the feature and frontend architecture principles (Vue 3 Composition API, composable-based architecture, PrimeVue + Tailwind CSS)

### 3. **Architecture Context**

- Components/composables involved
- Files referenced
- Routing considerations (if applicable)
- State management approach (Pinia store vs local state)

### 4. **Implementation Steps**

Detailed steps, typically:

#### **Step 0: Create Feature Branch**

- **Action**: Create and switch to a new feature branch following the development workflow. Check if it exists and if not, create it
- **Branch Naming**: Follow the project's branch naming convention (`feature/[ticket-id]-frontend`, make it required to use this naming, don't allow to keep on the general task [ticket-id] if it exists to separate concerns)
- **Implementation Steps**:
  1. Ensure you're on the latest `main` or `develop` branch (or appropriate base branch)
  2. Pull latest changes: `git pull origin [base-branch]`
  3. Create new branch: `git checkout -b [branch-name]`
  4. Verify branch creation: `git branch`
- **Notes**: This must be the FIRST step before any code changes. Refer to `ai-specs/specs/frontend-standards.mdc` section "Development Workflow" for specific branch naming conventions and workflow rules.

#### **Step N: [Action Name]**

- **File**: Target file path
- **Action**: What to implement
- **Function/Component Signature**: Code signature
- **Implementation Steps**: Numbered list
- **Dependencies**: Required imports/npm packages
- **Implementation Notes**: Technical details

Common steps:

- **Step 1**: Define TypeScript Interfaces in `frontend/src/types/`
- **Step 2**: Create/Update Composables in `frontend/src/composables/` (API communication)
- **Step 3**: Create/Update Pinia Store in `frontend/src/stores/` (if shared state needed)
- **Step 4**: Create/Update Vue Components using `<script setup lang="ts">` with PrimeVue + Tailwind
- **Step 5**: Update Routing in `frontend/src/router/` (if new pages/routes needed)
- **Step 6**: Write Vitest Unit Tests for composables and components
- **Step 7**: Write Cypress E2E Tests for critical user flows (`frontend/cypress/e2e/`)

#### **Step N+1: Update Technical Documentation**

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze all code changes made during implementation
  2. **Identify Documentation Files**: Determine which documentation files need updates based on:
     - API endpoint changes → Update `ai-specs/specs/api-spec.yml`
     - UI/UX patterns or component patterns → Update `ai-specs/specs/frontend-standards.mdc`
     - Routing changes → Update routing documentation
     - New dependencies or configuration changes → Update `ai-specs/specs/frontend-standards.mdc`
     - Test patterns or Cypress changes → Update testing documentation
  3. **Update Documentation**: For each affected file:
     - Update content in English (as per `documentation-standards.mdc`)
     - Maintain consistency with existing documentation structure
     - Ensure proper formatting
  4. **Verify Documentation**:
     - Confirm all changes are accurately reflected
     - Check that documentation follows established structure
  5. **Report Updates**: Document which files were updated and what changes were made
- **References**:
  - Follow process described in `ai-specs/specs/documentation-standards.mdc`
  - All documentation must be written in English
- **Notes**: This step is MANDATORY before considering the implementation complete. Do not skip documentation updates.

### 5. **Implementation Order**

- Numbered list of steps in sequence (must start with Step 0: Create Feature Branch and end with documentation update step)

### 6. **Testing Checklist**

- Post-implementation verification checklist
- Vitest unit test coverage for composables and components
- Cypress E2E test coverage for critical user flows
- Component functionality verification
- Error handling verification

### 7. **Error Handling Patterns**

- Error state management in composables (loading, error, data refs)
- User-friendly error messages via PrimeVue Toast/Message components
- API error handling in composables with Axios interceptors

### 8. **UI/UX Considerations** (if applicable)

- PrimeVue component usage (DataTable, Dialog, Calendar, etc.)
- Tailwind CSS utility classes for layout and spacing
- Responsive design with mobile-first breakpoints (`sm:`, `md:`, `lg:`)
- Accessibility requirements (ARIA labels, keyboard navigation)
- Loading states and user feedback

### 9. **Dependencies**

- npm packages required
- PrimeVue components used
- Third-party packages (if any, with justification)

### 10. **Notes**

- Important reminders and constraints
- Business rules
- Language requirements (English only)
- TypeScript strict typing requirements

### 11. **Next Steps After Implementation**

- Post-implementation tasks (documentation is already covered in Step N+1, but may include integration, deployment, etc.)

### 12. **Implementation Verification**

- Final verification checklist:
  - Code Quality (TypeScript strict, no `any`, `<script setup lang="ts">`)
  - Functionality (components render correctly, API calls work)
  - Testing (Vitest + Cypress coverage)
  - Integration (composables connect to backend API correctly)
  - Documentation updates completed

# Frontend Implementation Plan: feat-csv-bank-import Banc Sabadell Norma43 Import

## Overview

This plan implements the frontend for the Banc Sabadell Norma43 bank statement import feature. The UI provides a two-step wizard for board members to upload `.txt` Norma43 export files, review fuzzy-matched debit transactions, and bulk-confirm membership fee payments.

**Architecture Principles:**
- **Vue 3 Composition API** with `<script setup lang="ts">` (single-file components)
- **Composable-based state management**: `useCsvImport()` for API communication
- **PrimeVue components**: `FileUpload`, `DataTable`, `Button`, `Tag`, `Message`, `Dialog`
- **Tailwind CSS** for styling and responsive design
- **TypeScript strict mode**: Full type safety, no `any` types
- **Reactive state**: `ref()` and `computed()` for local component state

---

## Architecture Context

### Components and Composables

**New Composables:**
- `frontend/src/composables/useCsvImport.ts` — API calls to `/api/memberships/fees/csv-import/parse` and `/confirm`

**New Components:**
- `frontend/src/components/memberships/CsvImportPanel.vue` — Two-step wizard (main component)
- `frontend/src/components/admin/MembershipsAdminPanel.vue` — Admin shell with tabs

**Modified Files:**
- `frontend/src/types/membership.ts` — Add CSV import types
- `frontend/src/router/index.ts` — Add `/admin/memberships` route
- `frontend/src/components/admin/AdminSidebar.vue` — Add "Cuotas" menu item

### State Management

**Local Component State** (no Pinia store needed):
- `step: ref<'upload' | 'review' | 'done'>` — Wizard step
- `selectedFile: ref<File | null>` — Uploaded file
- `matchResults: ref<CsvMatchResult[]>` — Parse results
- `selectedRows: ref<Set<number>>` — User-checked rows for confirmation
- `overrides: ref<Map<number, string>>` — Manual family unit overrides
- `confirmResult: ref<CsvBulkConfirmResult | null>` — Confirmation results

**Composable State:**
- `matchResults`, `loading`, `error` from `useCsvImport()`

### Routing

**New Route:**
- Path: `/admin/memberships`
- Component: `MembershipsAdminPanel.vue`
- Auth: Requires Board role
- Meta: `{ requiresAuth: true, requiredRole: 'Board' }`

### File Organization

```
frontend/src/
├── types/
│   └── membership.ts (modify: add CSV import types)
├── composables/
│   └── useCsvImport.ts (new)
├── components/
│   ├── memberships/
│   │   └── CsvImportPanel.vue (new)
│   └── admin/
│       ├── MembershipsAdminPanel.vue (new)
│       └── AdminSidebar.vue (modify)
├── router/
│   └── index.ts (modify: add route)
└── cypress/
    └── e2e/
        └── memberships/
            └── csv-import.cy.ts (new: E2E tests)
```

---

## Implementation Steps

### **Step 0: Create Feature Branch**

**Action**: Create and switch to a new feature branch for frontend implementation only

**Branch Naming**: `feature/feat-csv-bank-import-frontend` (separate from backend work)

**Implementation Steps**:

1. Ensure you're on the latest `dev` branch
2. Pull latest changes: `git pull origin dev`
3. Create new branch: `git checkout -b feature/feat-csv-bank-import-frontend`
4. Verify branch creation: `git branch`

**Notes**: Follow project workflow: feature branch → dev → main (hotfix only)

---

### **Step 1: Add CSV Import Types to membership.ts**

**File**: `frontend/src/types/membership.ts`

**Action**: Add TypeScript interfaces for CSV import feature

**Types to Add**:

```typescript
export type MatchConfidence = 'High' | 'Medium' | 'Low' | 'None'

// No CsvColumnMapping needed for Norma43 (fixed format)

export interface CsvMatchResult {
  rowIndex: number
  rawTransactionReference: string
  rawConceptLines: string
  amount: number
  valueDate: string // YYYY-MM-DD
  transactionType?: string // "05" for SEPA CORE
  feeId: string | null
  membershipId: string | null
  familyUnitName: string | null
  memberName: string | null
  feeAmount: number | null
  confidence: MatchConfidence
}

export interface CsvConfirmItem {
  rowIndex: number
  feeId: string
  membershipId: string
  paidDate: string // YYYY-MM-DD
  paymentReference: string | null
}

export interface CsvBulkConfirmRequest {
  items: CsvConfirmItem[]
}

export interface CsvBulkConfirmResult {
  confirmed: number
  failed: number
  results: Array<{ rowIndex: number; success: boolean; error: string | null }>
}
```

**Implementation Steps**:

1. Open `frontend/src/types/membership.ts`
2. Append types after existing interfaces (after `MemberMembershipData`)
3. No modifications to existing types needed

**Dependencies**: TypeScript (built-in)

**Implementation Notes**:
- All types are `export` (used in composables and components)
- `MatchConfidence` is string union (type-safe, no enum overhead)
- Dates as `string` (ISO 8601 format: YYYY-MM-DD) for API compatibility
- Amounts as `number` (from JSON API responses)

---

### **Step 2: Create useCsvImport Composable**

**File**: `frontend/src/composables/useCsvImport.ts` (NEW)

**Action**: Create composable for API communication with CSV import endpoints

**Function Signature**:

```typescript
export function useCsvImport() {
  const matchResults: Ref<CsvMatchResult[]>
  const loading: Ref<boolean>
  const error: Ref<string | null>

  const parseAndMatch(file: File): Promise<CsvMatchResult[]>
  const bulkConfirm(request: CsvBulkConfirmRequest): Promise<CsvBulkConfirmResult>

  return { matchResults, loading, error, parseAndMatch, bulkConfirm }
}
```

**Implementation Steps**:

1. Import required modules: `ref`, `Ref` from `vue`, `api` from `@/utils/api`, types from `@/types/membership`
2. Create refs for reactive state:
   - `matchResults: ref<CsvMatchResult[]>([])`
   - `loading: ref(false)`
   - `error: ref<string | null>(null)`
3. Implement `parseAndMatch(file: File)`:
   - Create `FormData` with file and empty mapping (Norma43 format doesn't need column mapping)
   - POST to `/api/memberships/fees/csv-import/parse` with `Content-Type: multipart/form-data`
   - Deserialize response as `ApiResponse<CsvMatchResult[]>`
   - Set `matchResults.value = response.data.data || []`
   - Return match results
   - Catch errors: set `error.value` and re-throw
   - Always set `loading.value = false` in finally block
4. Implement `bulkConfirm(request: CsvBulkConfirmRequest)`:
   - POST to `/api/memberships/fees/csv-import/confirm`
   - Body: JSON `CsvBulkConfirmRequest`
   - Return `CsvBulkConfirmResult` from response
   - Same error/loading handling
5. Return object with all state and methods

**Dependencies**:

```typescript
import { ref } from 'vue'
import type { Ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  CsvMatchResult,
  CsvBulkConfirmRequest,
  CsvBulkConfirmResult,
} from '@/types/membership'
```

**Implementation Notes**:
- `api` client handles axios configuration and error interceptors
- FormData for multipart: `formData.append('file', file)` and `formData.append('mapping', JSON.stringify({}))`
- Error handling: Catch and expose via `error.ref` (no toast/notification here — let component decide)
- Loading state: Set before request, clear in finally block
- No type `any` — all responses typed as `ApiResponse<T>`

---

### **Step 3: Create CsvImportPanel.vue Component**

**File**: `frontend/src/components/memberships/CsvImportPanel.vue` (NEW)

**Action**: Implement two-step wizard for Norma43 upload and confirmation

**Component Structure**:

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'
import { useCsvImport } from '@/composables/useCsvImport'
import type { CsvMatchResult, CsvConfirmItem } from '@/types/membership'

// Wizard state
const step = ref<'upload' | 'review' | 'done'>('upload')

// File upload state
const selectedFile = ref<File | null>(null)

// Review state
const selectedRows = ref<Set<number>>(new Set())
const overrideFamilies = ref<Map<number, string>>(new Map())

// Composable
const { matchResults, loading, error, parseAndMatch, bulkConfirm } = useCsvImport()

// Results
const confirmResult = ref<any>(null)

// Computed
const canConfirm = computed(() => selectedRows.value.size > 0)
</script>

<template>
  <div class="csv-import-panel">
    <!-- Step 1: Upload -->
    <div v-if="step === 'upload'" class="step-upload">
      <!-- File upload UI -->
    </div>

    <!-- Step 2: Review -->
    <div v-if="step === 'review'" class="step-review">
      <!-- Match results table -->
    </div>

    <!-- Step 3: Done -->
    <div v-if="step === 'done'" class="step-done">
      <!-- Confirmation summary -->
    </div>
  </div>
</template>

<style scoped>
/* Styles */
</style>
```

**Implementation Steps**:

1. **Upload Step**:
   - PrimeVue `FileUpload` component: `accept=".txt"`, `mode="basic"`, `auto="false"`
   - Label: "Importar extracto Norma43 de Banc Sabadell"
   - Hint text: "Descargue su extracto mensual de Banc Sabadell en formato Norma43 (.txt)"
   - On file select: save to `selectedFile` ref
   - Button "Procesar archivo":
     - Call `parseAndMatch(selectedFile.value)`
     - Set `step.value = 'review'` on success
     - Show error toast on failure

2. **Review Step**:
   - PrimeVue `DataTable`:
     - Columns: row#, transaction reference, concept lines, amount, value date, family, member, confidence, checkbox
     - Rows: `matchResults`
     - Paginator: 10 rows per page
     - Striped rows styling
   - Confidence `Tag` component:
     - High: green (`severity="success"`)
     - Medium: amber (`severity="warning"`)
     - Low: red (`severity="danger"`)
     - None: gray (`severity="secondary"`)
   - Manual override column:
     - `AutoComplete` for Low/None rows
     - Populated from pending families list (load from backend or pass as prop)
     - Allow user to search and select family unit
   - Checkbox column:
     - Control `selectedRows` set
     - Auto-check High/Medium confidence rows
     - User can uncheck to skip row
   - Buttons:
     - "Atrás" (back to upload)
     - "Confirmar seleccionados" (disabled if no rows selected):
       - Build `CsvBulkConfirmRequest` from selected rows + overrides
       - Call `bulkConfirm(request)`
       - Set `step.value = 'done'` on success

3. **Done Step**:
   - Message component: success message with confirmed/failed counts
   - Display error details if any:
     - Expandable section with failed rows + error messages
   - Button "Importar otro extracto":
     - Reset state: `step = 'upload'`, clear file, clear matches, clear selections
   - Summary table (optional): show confirmed vs failed breakdown

**Dependencies**:

```vue
import { ref, computed } from 'vue'
import FileUpload from 'primevue/fileupload'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Message from 'primevue/message'
import AutoComplete from 'primevue/autocomplete'
import { useCsvImport } from '@/composables/useCsvImport'
```

**Implementation Notes**:
- Confidence colors: PrimeVue `severity` values (success, warning, danger, secondary)
- Row selection: Use `Set<number>` for O(1) lookup when confirming
- Manual overrides: Map<rowIndex, familyUnitName> to persist user changes
- Error handling: Show error message at top of component, not toasts (more persistent)
- Loading state: Disable buttons, show loading spinner on parse/confirm
- Responsive: Use Tailwind responsive classes for mobile layout (stack columns on small screens)

---

### **Step 4: Create MembershipsAdminPanel.vue Component**

**File**: `frontend/src/components/admin/MembershipsAdminPanel.vue` (NEW)

**Action**: Create admin shell with tabbed interface for membership features

**Component**:

```vue
<script setup lang="ts">
import CsvImportPanel from '@/components/memberships/CsvImportPanel.vue'
</script>

<template>
  <div class="memberships-admin-panel">
    <Tabs>
      <TabList>
        <Tab>{{ $t('membership.csvImport.title') }}</Tab>
      </TabList>

      <TabPanels>
        <TabPanel>
          <CsvImportPanel />
        </TabPanel>
      </TabPanels>
    </Tabs>
  </div>
</template>

<style scoped>
.memberships-admin-panel {
  padding: 1.5rem;
}
</style>
```

**Implementation Steps**:

1. Import `CsvImportPanel` component
2. Wrap in PrimeVue `Tabs` component:
   - `TabList` with `Tab` for "Importar" (CSV import)
   - `TabPanels` with `TabPanel` containing `CsvImportPanel`
3. Add i18n placeholder for tab label (or hardcode Spanish text)
4. Add padding/spacing with Tailwind

**Dependencies**:

```vue
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import CsvImportPanel from '@/components/memberships/CsvImportPanel.vue'
```

**Implementation Notes**:
- Tab structure allows future expansion (e.g., Tab 2 for "Listado de cuotas")
- Component is intentionally simple (delegating logic to CsvImportPanel)
- Keep styling minimal — rely on PrimeVue theming

---

### **Step 5: Update Router with /admin/memberships Route**

**File**: `frontend/src/router/index.ts`

**Action**: Add new admin route for memberships

**Route to Add**:

```typescript
{
  path: '/admin/memberships',
  name: 'admin-memberships',
  component: () => import('@/components/admin/MembershipsAdminPanel.vue'),
  meta: {
    requiresAuth: true,
    requiredRole: 'Board',
  },
}
```

**Implementation Steps**:

1. Find the admin routes section (typically nested under `/admin`)
2. Add new route object after existing admin routes
3. Lazy-load component with dynamic import for code splitting
4. Set meta: `requiresAuth: true`, `requiredRole: 'Board'`

**Implementation Notes**:
- Lazy loading reduces initial bundle size
- Role check in meta allows route guards to enforce authorization
- Route name follows pattern: `admin-[feature]`

---

### **Step 6: Add "Cuotas" Menu Item to AdminSidebar.vue**

**File**: `frontend/src/components/admin/AdminSidebar.vue`

**Action**: Add navigation link for memberships admin

**Menu Item to Add**:

```typescript
{
  label: 'Cuotas',
  icon: 'pi pi-wallet',
  to: '/admin/memberships',
  testId: 'sidebar-memberships',
  visible: () => auth.isBoard,
}
```

**Implementation Steps**:

1. Find the "Finanzas" (Finance) section in AdminSidebar menu items
2. Add menu item object to that section's children array
3. Set visibility condition: `visible: () => auth.isBoard`
4. Use wallet icon: `pi pi-wallet` (PrimeIcons)
5. Route to: `/admin/memberships`

**Implementation Notes**:
- Visibility function ensures only Board role sees this menu
- Icon choice: wallet (pi pi-wallet) for finance-related feature
- Label: "Cuotas" (Spanish for "Fees" or "Quotas")
- Test ID for E2E testing: `sidebar-memberships`

---

### **Step 7: Write Vitest Unit Tests**

**Files**: 
- `frontend/src/composables/__tests__/useCsvImport.spec.ts` (NEW)
- `frontend/src/components/memberships/__tests__/CsvImportPanel.spec.ts` (NEW)

**Action**: Write unit tests for composable and component

**Test Structure**:

#### **useCsvImport.spec.ts**

```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useCsvImport } from '@/composables/useCsvImport'
import { api } from '@/utils/api'

vi.mock('@/utils/api', () => ({
  api: {
    post: vi.fn(),
  },
}))

describe('useCsvImport', () => {
  it('should initialize with empty state', () => {
    const { matchResults, loading, error } = useCsvImport()
    expect(matchResults.value).toEqual([])
    expect(loading.value).toBe(false)
    expect(error.value).toBeNull()
  })

  it('parseAndMatch: should call API and set results', async () => {
    // Mock API response
    vi.mocked(api.post).mockResolvedValueOnce({
      data: {
        success: true,
        data: [{ rowIndex: 1, confidence: 'High', ... }],
      },
    })

    const { parseAndMatch, matchResults } = useCsvImport()
    const file = new File(['content'], 'test.txt', { type: 'text/plain' })

    const results = await parseAndMatch(file)

    expect(results).toHaveLength(1)
    expect(matchResults.value).toEqual(results)
  })

  it('parseAndMatch: should set error on API failure', async () => {
    vi.mocked(api.post).mockRejectedValueOnce(new Error('Network error'))

    const { parseAndMatch, error } = useCsvImport()
    const file = new File(['content'], 'test.txt')

    try {
      await parseAndMatch(file)
    } catch {
      // Expected
    }

    expect(error.value).toContain('Error')
  })

  it('bulkConfirm: should POST items and return results', async () => {
    const mockResult = { confirmed: 2, failed: 0, results: [...] }
    vi.mocked(api.post).mockResolvedValueOnce({
      data: { success: true, data: mockResult },
    })

    const { bulkConfirm } = useCsvImport()
    const request = { items: [{ rowIndex: 1, feeId: 'uuid', ... }] }

    const result = await bulkConfirm(request)

    expect(result.confirmed).toBe(2)
    expect(result.failed).toBe(0)
  })
})
```

#### **CsvImportPanel.spec.ts**

```typescript
import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import CsvImportPanel from '@/components/memberships/CsvImportPanel.vue'

describe('CsvImportPanel.vue', () => {
  it('should render upload step initially', () => {
    const wrapper = mount(CsvImportPanel)
    expect(wrapper.find('.step-upload').exists()).toBe(true)
    expect(wrapper.find('.step-review').exists()).toBe(false)
  })

  it('should show file upload input', () => {
    const wrapper = mount(CsvImportPanel)
    const fileUpload = wrapper.findComponent({ name: 'FileUpload' })
    expect(fileUpload.exists()).toBe(true)
  })

  it('should transition to review step after parse success', async () => {
    // Mock composable
    const wrapper = mount(CsvImportPanel, {
      global: {
        stubs: { FileUpload: true, DataTable: true, Button: true },
      },
    })

    // Simulate file selection and parse
    // (component should move to 'review' step)
    // Assertion: wrapper.find('.step-review').exists() === true
  })

  it('should display confidence tags with correct colors', () => {
    const wrapper = mount(CsvImportPanel)
    const highTag = wrapper.find('[data-test="confidence-high"]')
    expect(highTag.attributes('severity')).toBe('success')
  })

  it('should reset state on "import another" button click', async () => {
    // Mount component in 'done' step
    // Click "Importar otro" button
    // Assert step is 'upload' again
  })
})
```

**Test Coverage Goals**:
- Composable: API calls, error handling, state updates (60%+ coverage)
- Component: Rendering, step transitions, user interactions (70%+ coverage)

**Dependencies**:

```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
```

**Implementation Notes**:
- Mock `api` client with vi.mock() to avoid actual HTTP calls
- Use `@vue/test-utils` mount() for component testing
- Test happy path and error scenarios
- Focus on business logic, not implementation details

---

### **Step 8: Write Cypress E2E Tests**

**File**: `frontend/cypress/e2e/memberships/csv-import.cy.ts` (NEW)

**Action**: Write end-to-end test for Norma43 import workflow

**Test Scenarios**:

```typescript
describe('Norma43 CSV Import Workflow', () => {
  beforeEach(() => {
    cy.login('board-user@example.com', 'password')
    cy.visit('/admin/memberships')
  })

  it('should display upload form', () => {
    cy.get('[data-test="sidebar-memberships"]').click()
    cy.get('.step-upload').should('be.visible')
    cy.get('input[type="file"]').should('exist')
    cy.get('button').contains('Procesar archivo').should('be.disabled')
  })

  it('should upload file and show matches', () => {
    cy.fixture('norma43-sample.txt').then(fileContent => {
      cy.get('input[type="file"]').selectFile({
        contents: Cypress.Buffer.from(fileContent),
        fileName: 'extracto.txt',
        mimeType: 'text/plain',
      })
    })

    cy.get('button').contains('Procesar archivo').click()
    cy.get('.step-review').should('be.visible')
    cy.get('[role="grid"]').should('contain', 'García') // Family name
  })

  it('should select rows and confirm', () => {
    // Setup: navigate to review step with matches
    cy.get('[data-test="row-1"] input[type="checkbox"]').check()
    cy.get('[data-test="row-2"] input[type="checkbox"]').check()

    cy.get('button').contains('Confirmar seleccionados').click()
    cy.get('.step-done').should('be.visible')
    cy.get('.step-done').should('contain', '2 cuotas registradas')
  })

  it('should override low-confidence match', () => {
    // Setup: navigate to review with Low match
    cy.get('[data-test="override-3"] input').type('García López')
    cy.get('[data-test="override-3"] .p-autocomplete-item').first().click()

    cy.get('[data-test="row-3"] input[type="checkbox"]').check()
    cy.get('button').contains('Confirmar seleccionados').click()

    cy.get('.step-done').should('contain', 'correcto')
  })

  it('should reset wizard on "import another"', () => {
    // Setup: done step
    cy.get('button').contains('Importar otro extracto').click()
    cy.get('.step-upload').should('be.visible')
    cy.get('input[type="file"]').should('have.value', '')
  })
})
```

**Test Fixtures**:

Create `frontend/cypress/fixtures/norma43-sample.txt`:
```
Record Type 0 header...
Record Type 1 account...
Record Type 2: SEPA CORE debit (05) for 50€, García familia
Record Type 3: Concept line 1 - García López Juan
Record Type 3: Concept line 2 - (empty)
...
```

**Implementation Steps**:

1. Create test file at `frontend/cypress/e2e/memberships/csv-import.cy.ts`
2. Add fixture file at `frontend/cypress/fixtures/norma43-sample.txt`
3. Write test for each user flow:
   - Upload file
   - Review matches
   - Confirm selection
   - Override low-confidence match
   - Reset wizard
4. Use data-test attributes in component for stable selectors

**Dependencies**:
- Cypress (already in project)
- `@cypress/webpack-dev-server` for component testing

**Implementation Notes**:
- Use `cy.login()` custom command (assuming already defined)
- Use `cy.fixture()` to load test Norma43 file
- `selectFile()` for file upload
- `data-test` attributes for element selection (more stable than CSS selectors)
- Test user-visible behavior, not implementation details

---

### **Step 9: Update Technical Documentation**

**Files to Update**:
- `docs/api/memberships.md` (or create if doesn't exist)
- `ai-specs/specs/frontend-standards.mdc` (if adding new patterns)

**Action**: Document API endpoints and frontend implementation

**Documentation Content**:

#### **docs/api/memberships.md**

Add sections:

```markdown
## CSV Import (Norma43)

### Endpoints

#### POST /api/memberships/fees/csv-import/parse
Parse Norma43 bank statement file and match debits to pending fees.

**Request:**
- Multipart form-data
- File: `.txt` Norma43 export from Banc Sabadell
- mapping: Empty JSON object (for API compatibility)

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "rowIndex": 1,
      "rawTransactionReference": "12345678-000",
      "rawConceptLines": "García López Juan",
      "amount": 50,
      "valueDate": "2026-04-01",
      "transactionType": "05",
      "feeId": "uuid",
      "confidence": "High",
      ...
    }
  ]
}
```

#### POST /api/memberships/fees/csv-import/confirm
Bulk confirm matched transactions as paid fees.

**Request:**
```json
{
  "items": [
    {
      "rowIndex": 1,
      "feeId": "uuid",
      "membershipId": "uuid",
      "paidDate": "2026-04-01",
      "paymentReference": "12345678-000"
    }
  ]
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "confirmed": 2,
    "failed": 0,
    "results": [...]
  }
}
```

### Norma43 Format
Explanation of Record Types 2 & 3, field positions, encoding...

### Matching Algorithm
Three-pass algorithm, confidence levels, fuzzy scoring...
```

#### **ai-specs/specs/frontend-standards.mdc**

Add section (if new patterns):

```markdown
## Wizard Pattern

Multi-step forms (wizards) use local component state with step refs:

```typescript
const step = ref<'step1' | 'step2' | 'done'>('step1')
```

Each step is conditionally rendered with v-if. No separate route per step.

Benefits:
- Simpler routing (single route for entire wizard)
- Preserve form state during step transitions
- Easy to reset on completion
```

**Implementation Steps**:

1. Update `docs/api/memberships.md`:
   - Add endpoint documentation with request/response examples
   - Explain Norma43 format
   - Document confidence levels and matching algorithm

2. Update `ai-specs/specs/frontend-standards.mdc` (if needed):
   - Document wizard pattern used in CsvImportPanel
   - Explain why local state instead of routes

**Implementation Notes**:
- Keep API docs concise; example payloads most important
- Frontend docs should explain patterns, not just list components
- All documentation in English per project standards

---

## Implementation Order

1. **Step 0**: Create feature branch (`feature/feat-csv-bank-import-frontend`)
2. **Step 1**: Add CSV import types to `membership.ts`
3. **Step 2**: Create `useCsvImport.ts` composable
4. **Step 3**: Create `CsvImportPanel.vue` component
5. **Step 4**: Create `MembershipsAdminPanel.vue` component
6. **Step 5**: Add route to `router/index.ts`
7. **Step 6**: Add menu item to `AdminSidebar.vue`
8. **Step 7**: Write Vitest unit tests
9. **Step 8**: Write Cypress E2E tests
10. **Step 9**: Update technical documentation

---

## Testing Checklist

### Vitest Unit Tests
- [ ] useCsvImport: initializes with empty state
- [ ] useCsvImport: parseAndMatch calls API and sets results
- [ ] useCsvImport: parseAndMatch sets error on API failure
- [ ] useCsvImport: bulkConfirm returns correct result structure
- [ ] CsvImportPanel: renders upload step initially
- [ ] CsvImportPanel: shows file upload input
- [ ] CsvImportPanel: transitions to review step after parse
- [ ] CsvImportPanel: displays confidence tags with correct colors
- [ ] CsvImportPanel: resets state on "import another"
- [ ] Coverage: 70%+ of components, 60%+ of composable

### Cypress E2E Tests
- [ ] User navigates to `/admin/memberships`
- [ ] User selects Norma43 file for upload
- [ ] System parses file and shows matches in table
- [ ] User selects High/Medium confidence rows
- [ ] User overrides Low confidence match with autocomplete
- [ ] User clicks "Confirmar seleccionados"
- [ ] System displays success summary with confirmed/failed counts
- [ ] User clicks "Importar otro extracto" and returns to upload step
- [ ] Error scenarios: invalid file, network error, authorization denied

### Manual Testing
- [ ] Responsive design: test on mobile, tablet, desktop
- [ ] Accessibility: keyboard navigation (Tab, Enter), screen reader labels
- [ ] Loading states: spinner visible during API calls
- [ ] Error messages: clear and user-friendly
- [ ] Tailwind styling: colors, spacing, responsive classes applied

---

## Error Handling Patterns

### Composable Error Handling

```typescript
// In useCsvImport.ts
try {
  const response = await api.post(...)
  matchResults.value = response.data.data || []
} catch (e: any) {
  error.value = e.response?.data?.error?.message || 'Error al procesar'
  throw e // Re-throw for component to handle
} finally {
  loading.value = false
}
```

### Component Error Handling

```vue
<script setup>
const { error, parseAndMatch } = useCsvImport()

const handleParse = async () => {
  try {
    await parseAndMatch(file.value)
    step.value = 'review'
  } catch {
    // error.value already set by composable
    // Show message component with error.value
  }
}
</script>

<template>
  <Message v-if="error" severity="error" :text="error" />
</template>
```

### User Feedback

- **Loading**: Show spinner/disabled buttons during API calls
- **Success**: Summary message with confirmed/failed counts
- **Errors**: 
  - Per-row errors in "done" step (not toasts)
  - File upload errors at top of form
  - API errors in message component

---

## UI/UX Considerations

### PrimeVue Components Used

- **FileUpload**: Single file upload with `.txt` filter, no auto-upload
- **DataTable**: Display match results with pagination (10 rows), striped rows
- **Column**: Row number, reference, concept, amount, date, family, member, confidence, checkbox, override
- **Tag**: Confidence level with color-coded severity
- **Button**: Process, Confirm, Back, Import Another
- **Message**: Error/success messages with severity levels
- **AutoComplete**: Family unit override with search
- **Dialog** (optional): Confirm bulk action before proceeding

### Tailwind CSS Classes

- **Spacing**: `px-4 py-2` (button padding), `mb-4` (vertical spacing between sections)
- **Colors**: Inherit from theme (primary, danger, warning)
- **Responsive**: `sm:px-2 md:px-4` for padding on small screens
- **Flexbox**: `flex gap-2` for button groups

### Accessibility

- **ARIA Labels**: All inputs have associated labels
- **Keyboard Navigation**: Tab order, Enter to submit, Escape to cancel
- **Semantic HTML**: Use `<section>`, `<header>`, roles on custom components
- **Color Contrast**: Ensure tags/badges have sufficient contrast
- **Loading States**: Announce loading to screen readers (`aria-busy`, `aria-label`)

### Responsive Design

- **Mobile**: Stack components vertically, hide optional columns
- **Tablet**: 2-column layout where applicable
- **Desktop**: Full DataTable with all columns visible

---

## Dependencies

### npm Packages (already present)
- `vue@3.x` — Vue 3 framework
- `primevue@^3.x` — UI component library
- `tailwindcss` — CSS utility framework
- `typescript` — Type safety
- `vitest` — Unit testing
- `@vue/test-utils` — Component testing
- `cypress` — E2E testing

### No additional packages required

### PrimeVue Components to Import

```typescript
import FileUpload from 'primevue/fileupload'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import Message from 'primevue/message'
import AutoComplete from 'primevue/autocomplete'
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
```

---

## Notes

### Important Reminders

1. **Norma43 File Format**: Always accept `.txt` files only (MIME type `text/plain`)
2. **Date Format**: API returns `YYYY-MM-DD`; display consistently
3. **Loading State**: Always show spinner during `parseAndMatch` and `bulkConfirm` calls
4. **Error Persistence**: Show errors in persistent message component, not toasts (board needs to see full error list)
5. **No Auto-Confirm**: Require explicit user checkbox selection; never auto-confirm without review

### Business Rules

- Board/Admin role only (enforced by route guard + API auth)
- Only pending fees for current year are matched
- Confidence levels guide user decision-making, not enforce it
- User can override any match or skip any row
- Confirmed fees are marked as paid immediately

### Language Requirements

- **Code**: English (component props, function names, comments)
- **UI**: Spanish (labels, hints, messages in templates)
- **Tests**: English (test descriptions)
- **Comments**: Explain "why", not "what"

### TypeScript Strict Mode

- No `any` types (use proper interfaces)
- All API responses typed as `ApiResponse<T>`
- Component props typed with proper interfaces
- Ref types explicitly declared: `Ref<Type>`

---

## Next Steps After Implementation

1. **Integration Testing**: Test with real backend (ensure parsing works with actual Norma43 exports)
2. **User Training**: Create onboarding guide for board members
3. **Monitoring**: Add analytics tracking for import usage (how many files, success rates)
4. **Future Enhancements**:
   - Save column mapping presets per bank
   - Batch import multiple files
   - Scheduled automatic imports from bank API (SEPA Direct API)
   - Audit log for all imports (who, when, what)

---

## Implementation Verification Checklist

### Final Verification Before PR

- [ ] **Code Quality**
  - [ ] No TypeScript errors (`npm run typecheck`)
  - [ ] No ESLint warnings (`npm run lint`)
  - [ ] All components use `<script setup lang="ts">`
  - [ ] No `any` types used
  - [ ] Consistent naming (camelCase variables, PascalCase components)

- [ ] **Functionality**
  - [ ] File upload accepts `.txt` files only
  - [ ] Parse endpoint called with correct multipart payload
  - [ ] Match results displayed in table with pagination
  - [ ] Confidence tags show correct colors (High=green, Medium=amber, Low=red, None=gray)
  - [ ] User can select/deselect rows with checkboxes
  - [ ] AutoComplete override works for Low/None rows
  - [ ] Confirm button disabled if no rows selected
  - [ ] Bulk confirm called with correct payload
  - [ ] Success/failure summary displayed in done step
  - [ ] "Import another" resets form correctly

- [ ] **Testing**
  - [ ] Vitest unit tests pass (`npm run test`)
  - [ ] 70%+ coverage for components
  - [ ] 60%+ coverage for composables
  - [ ] Cypress E2E tests pass (`npm run cypress:run`)
  - [ ] All user flows covered (happy path + error scenarios)

- [ ] **UI/UX**
  - [ ] Responsive on mobile (tested with DevTools)
  - [ ] Tailwind styling applied consistently
  - [ ] PrimeVue theme inherited correctly
  - [ ] Loading spinners shown during API calls
  - [ ] Error messages clear and helpful
  - [ ] Keyboard navigation works (Tab, Enter, Escape)
  - [ ] ARIA labels present on inputs

- [ ] **Integration**
  - [ ] Route `/admin/memberships` works
  - [ ] Sidebar menu item "Cuotas" visible to Board role
  - [ ] Composable calls correct API endpoints
  - [ ] API responses match TypeScript types
  - [ ] Authentication/authorization enforced

- [ ] **Documentation**
  - [ ] API endpoint docs updated with examples
  - [ ] Norma43 format explained
  - [ ] Confidence algorithm documented
  - [ ] Wizard pattern documented (if new)
  - [ ] All files in English

- [ ] **Git Hygiene**
  - [ ] Branch: `feature/feat-csv-bank-import-frontend`
  - [ ] Commits are logical and focused
  - [ ] Clear commit messages (feat/fix/refactor: description)
  - [ ] No merge conflicts with `dev` branch
  - [ ] Ready for squash-and-merge into `dev`

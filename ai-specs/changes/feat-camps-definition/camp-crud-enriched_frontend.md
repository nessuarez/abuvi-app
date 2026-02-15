# Frontend Implementation Plan: Camp CRUD - Enriched User Stories

## Overview

This document outlines the complete frontend implementation for the Camp Management feature in ABUVI, including Camp Locations, Camp Editions, Camp Edition Extras, and Proposal workflow management. The implementation follows Vue 3 Composition API patterns with PrimeVue components and Tailwind CSS styling.

**Key Features:**

- Camp Location inventory management (reusable templates)
- Camp Edition management with proposal workflow (Proposed → Draft → Open → Closed → Completed)
- Age-based pricing (adult, child, baby) with configurable age ranges
- Camp edition extras/add-ons management
- Interactive map visualization for camp locations
- Board-only access control

**Architecture Principles:**

- Vue 3 Composition API with `<script setup lang="ts">`
- Composable-based architecture for API communication
- PrimeVue components for complex UI (DataTable, Dialog, Calendar, etc.)
- Tailwind CSS for layout and styling
- Pinia store for minimal global state (if needed)
- Leaflet.js for interactive maps

## Architecture Context

### Components/Composables Involved

**New Files to Create:**

```
frontend/src/
├── types/
│   ├── camp.ts                         # Camp location types
│   ├── camp-edition.ts                 # Camp edition types
│   └── association-settings.ts         # Global settings types
├── composables/
│   ├── useCamps.ts                     # Camp locations API
│   ├── useCampEditions.ts              # Camp editions API
│   ├── useCampExtras.ts                # Extras API
│   └── useAssociationSettings.ts       # Settings API
├── components/
│   └── camps/
│       ├── CampLocationCard.vue        # Display camp location
│       ├── CampLocationForm.vue        # Create/edit camp location
│       ├── CampLocationMap.vue         # Interactive Leaflet map
│       ├── ProposalCard.vue            # Proposed camp candidate card
│       ├── ProposalComparison.vue      # Side-by-side comparison table
│       ├── CampEditionForm.vue         # Create/edit camp edition
│       ├── CampEditionCard.vue         # Display camp edition
│       ├── CampExtraForm.vue           # Create/edit extra
│       ├── CampExtrasTable.vue         # Manage extras
│       ├── CampStatusBadge.vue         # Status indicator
│       ├── PricingBreakdown.vue        # Age-based pricing display
│       └── AgeRangeSettings.vue        # Configure age ranges
└── views/
    └── camps/
        ├── CampLocationsPage.vue       # List/manage camp locations
        ├── CampLocationDetailPage.vue  # View/edit camp location
        ├── ProposedCampsPage.vue       # View/compare proposals
        ├── CampEditionsPage.vue        # List/manage editions
        └── CampEditionDetailPage.vue   # View/edit edition + extras
```

### Routing Considerations

**New Routes** (all require `requiresAuth: true, requiresBoard: true`):

```typescript
{
  path: '/camps/locations',
  name: 'camp-locations',
  component: CampLocationsPage,
  meta: { title: 'ABUVI | Campamentos', requiresAuth: true, requiresBoard: true }
}
{
  path: '/camps/locations/:id',
  name: 'camp-location-detail',
  component: CampLocationDetailPage,
  meta: { title: 'ABUVI | Detalles del Campamento', requiresAuth: true, requiresBoard: true }
}
{
  path: '/camps/proposals',
  name: 'proposed-camps',
  component: ProposedCampsPage,
  meta: { title: 'ABUVI | Propuestas de Campamento', requiresAuth: true, requiresBoard: true }
}
{
  path: '/camps/editions',
  name: 'camp-editions',
  component: CampEditionsPage,
  meta: { title: 'ABUVI | Ediciones', requiresAuth: true, requiresBoard: true }
}
{
  path: '/camps/editions/:id',
  name: 'camp-edition-detail',
  component: CampEditionDetailPage,
  meta: { title: 'ABUVI | Detalles de Edición', requiresAuth: true, requiresBoard: true }
}
```

### State Management Approach

**Local state preferred** for this feature:

- Camp locations, editions, and extras are managed via composables
- No global Pinia store needed unless shared state across multiple unrelated views
- Use reactive forms with `reactive()` for complex form objects
- Use `ref()` for primitives and loading/error states

## Implementation Steps

### **Step 0: Create Feature Branch**

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/camp-crud-frontend`
- **Implementation Steps**:
  1. Check current branch: `git branch`
  2. Ensure on latest main: `git checkout main && git pull origin main`
  3. Create feature branch: `git checkout -b feature/camp-crud-frontend`
  4. Verify: `git branch` (should show `* feature/camp-crud-frontend`)
- **Notes**: This MUST be the first step before any code changes.

---

### **Step 1: Define TypeScript Interfaces**

#### **File**: `frontend/src/types/camp.ts`

- **Action**: Define Camp location types
- **Implementation Steps**:
  1. Create `frontend/src/types/camp.ts`
  2. Define `Camp` interface matching backend model
  3. Define `CreateCampRequest` and `UpdateCampRequest` DTOs
  4. Define `CampStatus` enum type
  5. Export all types

- **Type Definitions**:

```typescript
// camp.ts
export interface Camp {
  id: string
  name: string
  description: string
  latitude: number
  longitude: number
  basePriceAdult: number
  basePriceChild: number
  basePriceBaby: number
  status: CampStatus
  createdAt: string
  updatedAt: string
  editionCount?: number // For display in list view
}

export type CampStatus = 'Active' | 'Inactive' | 'HistoricalArchive'

export interface CreateCampRequest {
  name: string
  description: string
  latitude: number
  longitude: number
  basePriceAdult: number
  basePriceChild: number
  basePriceBaby: number
  status: CampStatus
}

export interface UpdateCampRequest extends CreateCampRequest {
  id: string
}

export interface CampLocation {
  latitude: number
  longitude: number
  name: string
  year?: number
}
```

#### **File**: `frontend/src/types/camp-edition.ts`

- **Action**: Define CampEdition and related types
- **Implementation Steps**:
  1. Create `frontend/src/types/camp-edition.ts`
  2. Define `CampEdition` interface
  3. Define `CampEditionExtra` interface
  4. Define request/response DTOs
  5. Export all types

- **Type Definitions**:

```typescript
// camp-edition.ts
import type { Camp } from './camp'

export interface CampEdition {
  id: string
  campId: string
  year: number
  name?: string
  startDate: string
  endDate: string
  location: string
  description?: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges: boolean
  babyMaxAge?: number
  childMinAge?: number
  childMaxAge?: number
  adultMinAge?: number
  maxCapacity: number
  contactEmail?: string
  contactPhone?: string
  status: CampEditionStatus
  isArchived: boolean
  proposalReason?: string
  proposalNotes?: string
  createdAt: string
  updatedAt: string
  camp?: Camp
}

export type CampEditionStatus = 'Proposed' | 'Draft' | 'Open' | 'Closed' | 'Completed'

export interface CreateCampEditionRequest {
  campId: string
  year: number
  name?: string
  startDate: string
  endDate: string
  location: string
  description?: string
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  useCustomAgeRanges?: boolean
  babyMaxAge?: number
  childMinAge?: number
  childMaxAge?: number
  adultMinAge?: number
  maxCapacity: number
  contactEmail?: string
  contactPhone?: string
}

export interface ProposeCampEditionRequest extends CreateCampEditionRequest {
  proposalReason: string
  proposalNotes: string
}

export interface CampEditionExtra {
  id: string
  campEditionId: string
  name: string
  description?: string
  price: number
  pricingType: 'PerPerson' | 'PerFamily'
  pricingPeriod: 'OneTime' | 'PerDay'
  isRequired: boolean
  maxQuantity?: number
  currentQuantity: number
  sortOrder: number
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateCampExtraRequest {
  name: string
  description?: string
  price: number
  pricingType: 'PerPerson' | 'PerFamily'
  pricingPeriod: 'OneTime' | 'PerDay'
  isRequired: boolean
  maxQuantity?: number
  sortOrder: number
}
```

#### **File**: `frontend/src/types/association-settings.ts`

- **Action**: Define global settings types
- **Type Definitions**:

```typescript
// association-settings.ts
export interface AgeRangeSettings {
  babyMaxAge: number
  childMinAge: number
  childMaxAge: number
  adultMinAge: number
}

export interface UpdateAgeRangesRequest extends AgeRangeSettings {}
```

- **Dependencies**: None
- **Implementation Notes**:
  - All interfaces match backend DTOs for consistency
  - Use ISO 8601 date strings (`string`) for dates
  - All monetary values are `number` (decimals from backend)

---

### **Step 2: Create API Composables**

#### **File**: `frontend/src/composables/useCamps.ts`

- **Action**: Implement Camp location API composable
- **Function Signature**:

```typescript
export function useCamps() {
  const camps = ref<Camp[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchCamps = async (params?: { page?: number; pageSize?: number; status?: CampStatus }): Promise<void>
  const getCampById = async (id: string): Promise<Camp | null>
  const createCamp = async (request: CreateCampRequest): Promise<Camp | null>
  const updateCamp = async (id: string, request: UpdateCampRequest): Promise<Camp | null>
  const deleteCamp = async (id: string): Promise<boolean>

  return { camps, loading, error, fetchCamps, getCampById, createCamp, updateCamp, deleteCamp }
}
```

- **Implementation Steps**:
  1. Import required types and utilities
  2. Define reactive state (`camps`, `loading`, `error`)
  3. Implement `fetchCamps` with pagination support
  4. Implement CRUD operations
  5. Handle errors with Spanish messages
  6. Return reactive state and methods

- **Dependencies**:
  - `import { ref } from 'vue'`
  - `import { api } from '@/utils/api'`
  - `import type { Camp, CreateCampRequest, UpdateCampRequest, CampStatus } from '@/types/camp'`
  - `import type { ApiResponse, PagedResult } from '@/types/api'`

- **Implementation Notes**:
  - Use `api.get<ApiResponse<PagedResult<Camp>>>('/camps')` for paginated list
  - Error messages in Spanish: `'Error al cargar campamentos'`, `'Error al crear campamento'`, etc.
  - Always reset `error.value = null` at start of each method
  - Set `loading.value = true/false` appropriately

#### **File**: `frontend/src/composables/useCampEditions.ts`

- **Action**: Implement CampEdition API composable
- **Function Signature**:

```typescript
export function useCampEditions() {
  const editions = ref<CampEdition[]>([])
  const activeEdition = ref<CampEdition | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchProposedEditions = async (year: number): Promise<void>
  const getActiveEdition = async (): Promise<void>
  const getEditionById = async (id: string): Promise<CampEdition | null>
  const createEdition = async (request: CreateCampEditionRequest): Promise<CampEdition | null>
  const proposeEdition = async (request: ProposeCampEditionRequest): Promise<CampEdition | null>
  const promoteEdition = async (id: string): Promise<CampEdition | null>
  const rejectEdition = async (id: string, reason: string): Promise<boolean>
  const updateEdition = async (id: string, request: Partial<CreateCampEditionRequest>): Promise<CampEdition | null>
  const changeStatus = async (id: string, newStatus: CampEditionStatus): Promise<CampEdition | null>
  const deleteEdition = async (id: string): Promise<boolean>

  return {
    editions,
    activeEdition,
    loading,
    error,
    fetchProposedEditions,
    getActiveEdition,
    getEditionById,
    createEdition,
    proposeEdition,
    promoteEdition,
    rejectEdition,
    updateEdition,
    changeStatus,
    deleteEdition
  }
}
```

- **Implementation Steps**:
  1. Define reactive state
  2. Implement all edition management methods
  3. Implement proposal workflow methods (propose, promote, reject)
  4. Implement status transition method
  5. Handle errors with Spanish messages
  6. Return state and methods

- **Dependencies**: Same as `useCamps.ts` plus `CampEdition` types

- **API Endpoints**:
  - `GET /api/camps/editions/proposed?year={year}` → `fetchProposedEditions`
  - `GET /api/camps/editions/active` → `getActiveEdition`
  - `GET /api/camps/editions/{id}` → `getEditionById`
  - `POST /api/camps/editions` → `createEdition`
  - `POST /api/camps/editions/propose` → `proposeEdition`
  - `POST /api/camps/editions/{id}/promote` → `promoteEdition`
  - `DELETE /api/camps/editions/{id}/reject` → `rejectEdition`
  - `PUT /api/camps/editions/{id}` → `updateEdition`
  - `POST /api/camps/editions/{id}/status` → `changeStatus`
  - `DELETE /api/camps/editions/{id}` → `deleteEdition`

#### **File**: `frontend/src/composables/useCampExtras.ts`

- **Action**: Implement CampEditionExtra API composable
- **Function Signature**:

```typescript
export function useCampExtras(editionId: string) {
  const extras = ref<CampEditionExtra[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchExtras = async (): Promise<void>
  const getExtraById = async (extraId: string): Promise<CampEditionExtra | null>
  const createExtra = async (request: CreateCampExtraRequest): Promise<CampEditionExtra | null>
  const updateExtra = async (extraId: string, request: CreateCampExtraRequest): Promise<CampEditionExtra | null>
  const deleteExtra = async (extraId: string): Promise<boolean>

  return { extras, loading, error, fetchExtras, getExtraById, createExtra, updateExtra, deleteExtra }
}
```

- **Implementation Steps**:
  1. Accept `editionId` as parameter (scoped to specific edition)
  2. Define reactive state
  3. Implement CRUD operations for extras
  4. Return state and methods

- **API Endpoints**:
  - `GET /api/camps/editions/{editionId}/extras`
  - `POST /api/camps/editions/{editionId}/extras`
  - `PUT /api/camps/editions/{editionId}/extras/{extraId}`
  - `DELETE /api/camps/editions/{editionId}/extras/{extraId}`

#### **File**: `frontend/src/composables/useAssociationSettings.ts`

- **Action**: Implement global settings API composable
- **Function Signature**:

```typescript
export function useAssociationSettings() {
  const ageRanges = ref<AgeRangeSettings | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchAgeRanges = async (): Promise<void>
  const updateAgeRanges = async (request: UpdateAgeRangesRequest): Promise<boolean>

  return { ageRanges, loading, error, fetchAgeRanges, updateAgeRanges }
}
```

- **Implementation Steps**:
  1. Define reactive state
  2. Implement `fetchAgeRanges` → `GET /api/settings/age-ranges`
  3. Implement `updateAgeRanges` → `PUT /api/settings/age-ranges`
  4. Return state and methods

- **Dependencies**: Same as above plus `AgeRangeSettings` types

---

### **Step 3: Create Shared UI Components**

#### **File**: `frontend/src/components/camps/CampStatusBadge.vue`

- **Action**: Create status indicator badge component
- **Component Signature**:

```vue
<script setup lang="ts">
import type { CampEditionStatus } from '@/types/camp-edition'

interface Props {
  status: CampEditionStatus
  size?: 'small' | 'medium'
}

const props = withDefaults(defineProps<Props>(), {
  size: 'medium'
})
</script>
```

- **Implementation Steps**:
  1. Create component with `<script setup lang="ts">`
  2. Define props interface
  3. Map status to Tailwind colors and Spanish labels
  4. Render PrimeVue Badge or custom styled span

- **Implementation Notes**:
  - Status colors:
    - `Proposed`: Yellow (`bg-yellow-100 text-yellow-800`)
    - `Draft`: Gray (`bg-gray-100 text-gray-800`)
    - `Open`: Green (`bg-green-100 text-green-800`)
    - `Closed`: Red (`bg-red-100 text-red-800`)
    - `Completed`: Blue (`bg-blue-100 text-blue-800`)
  - Spanish labels:
    - `Proposed` → "Propuesta"
    - `Draft` → "Borrador"
    - `Open` → "Abierta"
    - `Closed` → "Cerrada"
    - `Completed` → "Completada"

#### **File**: `frontend/src/components/camps/PricingBreakdown.vue`

- **Action**: Display age-based pricing breakdown
- **Component Signature**:

```vue
<script setup lang="ts">
interface Props {
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  ageRanges: AgeRangeSettings
}

const props = defineProps<Props>()
</script>
```

- **Implementation Steps**:
  1. Display pricing in grid layout (3 columns)
  2. Show price per age category with age range labels
  3. Format currency with Intl.NumberFormat (EUR)

- **Example Template**:

```vue
<template>
  <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
    <div class="rounded border p-3">
      <p class="text-xs text-gray-500">Adulto ({{ ageRanges.adultMinAge }}+ años)</p>
      <p class="text-lg font-semibold">{{ formatCurrency(pricePerAdult) }}</p>
    </div>
    <div class="rounded border p-3">
      <p class="text-xs text-gray-500">Niño ({{ ageRanges.childMinAge }}-{{ ageRanges.childMaxAge }} años)</p>
      <p class="text-lg font-semibold">{{ formatCurrency(pricePerChild) }}</p>
    </div>
    <div class="rounded border p-3">
      <p class="text-xs text-gray-500">Bebé (0-{{ ageRanges.babyMaxAge }} años)</p>
      <p class="text-lg font-semibold">{{ formatCurrency(pricePerBaby) }}</p>
    </div>
  </div>
</template>
```

#### **File**: `frontend/src/components/camps/CampLocationMap.vue`

- **Action**: Interactive Leaflet map showing camp locations
- **Component Signature**:

```vue
<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import L from 'leaflet'
import type { CampLocation } from '@/types/camp'

interface Props {
  locations: CampLocation[]
  selectedId?: string
}

const props = defineProps<Props>()
const emit = defineEmits<{
  selectLocation: [id: string]
}>()
</script>
```

- **Implementation Steps**:
  1. Install Leaflet: `npm install leaflet @types/leaflet`
  2. Create map container ref
  3. Initialize Leaflet map in `onMounted`
  4. Add markers for each location
  5. Handle marker click to emit `selectLocation`
  6. Cleanup map in `onUnmounted`

- **Dependencies**:
  - `npm install leaflet @types/leaflet`
  - Import Leaflet CSS in `main.ts`: `import 'leaflet/dist/leaflet.css'`

- **Implementation Notes**:
  - Default center: Spain coordinates `[40.4168, -3.7038]`
  - Zoom level: 6 (country view)
  - Use OpenStreetMap tiles
  - Popup shows camp name and year

---

### **Step 4: Create Camp Location Pages and Components**

#### **File**: `frontend/src/components/camps/CampLocationCard.vue`

- **Action**: Display camp location in card format
- **Component Signature**:

```vue
<script setup lang="ts">
import type { Camp } from '@/types/camp'

interface Props {
  camp: Camp
}

const props = defineProps<Props>()
const emit = defineEmits<{
  edit: [camp: Camp]
  delete: [camp: Camp]
  viewDetails: [camp: Camp]
}>()
</script>
```

- **Implementation Steps**:
  1. Display camp name, description (truncated), coordinates, pricing summary
  2. Show status badge
  3. Show edition count badge
  4. Action buttons: Edit, Delete, View Details
  5. Use Tailwind card styling

#### **File**: `frontend/src/components/camps/CampLocationForm.vue`

- **Action**: Create/edit camp location form
- **Component Signature**:

```vue
<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import type { Camp, CreateCampRequest, CampStatus } from '@/types/camp'

interface Props {
  camp?: Camp // If editing
  mode: 'create' | 'edit'
}

const props = defineProps<Props>()
const emit = defineEmits<{
  submit: [data: CreateCampRequest]
  cancel: []
}>()
</script>
```

- **Implementation Steps**:
  1. Create reactive form data with validation
  2. PrimeVue form fields:
     - `InputText` for name
     - `Textarea` for description
     - `InputNumber` for latitude (-90 to 90) and longitude (-180 to 180)
     - `InputNumber` for basePriceAdult, basePriceChild, basePriceBaby
     - `Dropdown` for status
  3. Real-time validation
  4. Show map preview of location pin
  5. Submit button disabled during validation errors
  6. Emit `submit` event on valid form

- **Validation Rules**:
  - Name: required, max 200 chars
  - Latitude: -90 to 90
  - Longitude: -180 to 180
  - All prices: >= 0
  - Status: required

- **Spanish Labels**:
  - Name → "Nombre del campamento"
  - Description → "Descripción"
  - Latitude → "Latitud"
  - Longitude → "Longitud"
  - Adult price → "Precio adulto"
  - Child price → "Precio niño"
  - Baby price → "Precio bebé"
  - Status → "Estado"

#### **File**: `frontend/src/views/camps/CampLocationsPage.vue`

- **Action**: List and manage camp locations (Board only)
- **Implementation Steps**:
  1. Use `useCamps` composable
  2. Fetch camps on mount
  3. Display in PrimeVue DataTable with pagination
  4. Columns: Name, Coordinates, Pricing Summary, Status, Edition Count, Actions
  5. Filter by status (Active/Inactive)
  6. Search by name
  7. "Create New Location" button → open dialog with `CampLocationForm`
  8. Row actions: Edit (dialog), Delete (confirm), View Map
  9. Show interactive map below table with all camp pins

- **PrimeVue Components**:
  - `DataTable` with `paginator`, `rows="10"`
  - `Column` for each field
  - `Dialog` for create/edit form
  - `ConfirmDialog` for delete confirmation
  - `Button` for actions
  - `InputText` for search

- **Implementation Notes**:
  - Loading state shows `ProgressSpinner`
  - Error state shows `Message` component
  - Empty state: "No hay campamentos registrados"
  - Delete validation: show warning if camp has editions

#### **File**: `frontend/src/views/camps/CampLocationDetailPage.vue`

- **Action**: View/edit camp location details
- **Implementation Steps**:
  1. Get camp ID from route params
  2. Fetch camp details on mount
  3. Display camp info with map showing single pin
  4. List all editions for this camp
  5. Edit button → toggle edit mode (inline or dialog)
  6. Back button → navigate to camp locations list

---

### **Step 5: Create Proposal Management Pages**

#### **File**: `frontend/src/components/camps/ProposalCard.vue`

- **Action**: Display proposed camp candidate
- **Component Signature**:

```vue
<script setup lang="ts">
import type { CampEdition } from '@/types/camp-edition'

interface Props {
  edition: CampEdition
}

const props = defineProps<Props>()
const emit = defineEmits<{
  promote: [edition: CampEdition]
  reject: [edition: CampEdition]
  viewDetails: [edition: CampEdition]
}>()
</script>
```

- **Implementation Steps**:
  1. Display: camp name, location, dates, pricing summary, capacity, proposal reason/notes
  2. Highlight proposal reason prominently
  3. Action buttons: "Aprobar y Promover", "Rechazar", "Ver Detalles"
  4. Use card layout with Tailwind styling

#### **File**: `frontend/src/components/camps/ProposalComparison.vue`

- **Action**: Side-by-side comparison table for proposed camps
- **Component Signature**:

```vue
<script setup lang="ts">
import type { CampEdition } from '@/types/camp-edition'

interface Props {
  proposals: CampEdition[]
}

const props = defineProps<Props>()
const emit = defineEmits<{
  selectProposal: [edition: CampEdition]
}>()
</script>
```

- **Implementation Steps**:
  1. Create comparison table with PrimeVue `DataTable` or custom table
  2. Rows: Location, Dates, Duration, Adult Price, Child Price, Baby Price, Capacity, Proposal Reason, Pros/Cons (from notes)
  3. Columns: One per proposal
  4. Highlight differences in pricing/capacity
  5. "Select" button per column

#### **File**: `frontend/src/views/camps/ProposedCampsPage.vue`

- **Action**: View and compare proposed camp candidates (Board only)
- **Implementation Steps**:
  1. Year selector dropdown (current year + next 2 years)
  2. Fetch proposed editions for selected year
  3. Display in two views:
     - **Card View**: Grid of `ProposalCard` components
     - **Comparison View**: `ProposalComparison` table
  4. Toggle between views with buttons
  5. Actions:
     - Promote candidate → `POST /api/camps/editions/{id}/promote` → success toast → redirect to edition details
     - Reject candidate → confirm dialog with reason input → `DELETE /api/camps/editions/{id}/reject` → remove from list
  6. Empty state: "No hay propuestas para este año"

- **PrimeVue Components**:
  - `Dropdown` for year selector
  - `Button` for view toggle
  - `ConfirmDialog` for rejection with reason
  - `Toast` for success/error feedback

- **Spanish Labels**:
  - "Propuestas de Campamento {year}"
  - "Vista de Tarjetas" / "Vista de Comparación"
  - "Aprobar y Promover a Borrador"
  - "Rechazar Propuesta"
  - "Razón del rechazo" (dialog input)

---

### **Step 6: Create Camp Edition Management Pages**

#### **File**: `frontend/src/components/camps/CampEditionForm.vue`

- **Action**: Create/edit camp edition form
- **Component Signature**:

```vue
<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue'
import type { CampEdition, CreateCampEditionRequest } from '@/types/camp-edition'
import type { Camp } from '@/types/camp'
import { useCamps } from '@/composables/useCamps'
import { useAssociationSettings } from '@/composables/useAssociationSettings'

interface Props {
  edition?: CampEdition // If editing
  mode: 'create' | 'edit' | 'propose'
}

const props = defineProps<Props>()
const emit = defineEmits<{
  submit: [data: CreateCampEditionRequest]
  cancel: []
}>()
</script>
```

- **Implementation Steps**:
  1. If mode = 'create' or 'propose': Show camp location dropdown (fetch from `useCamps`)
  2. Pre-populate pricing from selected camp location
  3. Fetch global age ranges from `useAssociationSettings`
  4. PrimeVue form fields:
     - `Dropdown` for camp location (create/propose only)
     - `InputNumber` for year
     - `InputText` for name (optional, defaults to camp name)
     - `Calendar` for startDate and endDate
     - `InputText` for location (specific description)
     - `Textarea` for description
     - `InputNumber` for pricePerAdult, pricePerChild, pricePerBaby
     - `Checkbox` for useCustomAgeRanges → show/hide custom age inputs
     - `InputNumber` for custom age ranges (if enabled)
     - `InputNumber` for maxCapacity
     - `InputText` for contactEmail, contactPhone
     - If mode = 'propose': `Textarea` for proposalReason and proposalNotes
  5. Validation:
     - Year >= current year
     - endDate > startDate
     - All prices >= 0
     - If custom age ranges: validate consistency (childMinAge > babyMaxAge, etc.)
  6. Submit button disabled during validation errors
  7. Show pricing breakdown preview

- **Validation Messages** (Spanish):
  - "El año debe ser el año actual o futuro"
  - "La fecha de fin debe ser posterior a la fecha de inicio"
  - "El precio debe ser mayor o igual a 0"
  - "La edad mínima de niño debe ser mayor que la edad máxima de bebé"

#### **File**: `frontend/src/components/camps/CampEditionCard.vue`

- **Action**: Display camp edition in card format
- **Component Signature**:

```vue
<script setup lang="ts">
import type { CampEdition } from '@/types/camp-edition'

interface Props {
  edition: CampEdition
}

const props = defineProps<Props>()
const emit = defineEmits<{
  edit: [edition: CampEdition]
  changeStatus: [edition: CampEdition]
  manageExtras: [edition: CampEdition]
  viewRegistrations: [edition: CampEdition]
}>()
</script>
```

- **Implementation Steps**:
  1. Display: year, name/camp name, dates, location, status badge, capacity info (X/Y registered)
  2. Show pricing summary
  3. Action buttons based on status:
     - Draft: "Abrir Inscripciones", "Editar"
     - Open: "Cerrar Inscripciones", "Gestionar Extras", "Ver Inscripciones"
     - Closed: "Marcar como Completada", "Ver Inscripciones"
     - Completed: Read-only, "Ver Inscripciones"
  4. Use card layout with status-specific colors

#### **File**: `frontend/src/views/camps/CampEditionsPage.vue`

- **Action**: List and manage camp editions (Board only)
- **Implementation Steps**:
  1. Fetch camp editions (consider filtering by year)
  2. Display in PrimeVue DataTable
  3. Columns: Year, Name, Dates, Location, Status, Capacity, Actions
  4. Filter by status (Draft/Open/Closed/Completed)
  5. Filter by year
  6. "Create New Edition" button → dialog with `CampEditionForm` (mode='create')
  7. "Propose New Camp" button → dialog with `CampEditionForm` (mode='propose')
  8. Row actions: Edit, Change Status, Manage Extras, View Registrations

- **Implementation Notes**:
  - Show active edition prominently at top (card format)
  - Table shows all editions below

#### **File**: `frontend/src/views/camps/CampEditionDetailPage.vue`

- **Action**: View/edit camp edition details + manage extras
- **Implementation Steps**:
  1. Get edition ID from route params
  2. Fetch edition details and extras
  3. Display edition info with status badge
  4. Show pricing breakdown
  5. Status transition button (based on current status)
  6. Tabs:
     - **Detalles**: Edition info, edit button
     - **Extras**: Manage extras (see Step 7)
     - **Inscripciones**: List registrations (future feature, placeholder)
  7. Confirm dialog for status transitions

---

### **Step 7: Create Camp Extras Management**

#### **File**: `frontend/src/components/camps/CampExtraForm.vue`

- **Action**: Create/edit camp edition extra
- **Component Signature**:

```vue
<script setup lang="ts">
import { reactive, ref } from 'vue'
import type { CampEditionExtra, CreateCampExtraRequest } from '@/types/camp-edition'

interface Props {
  extra?: CampEditionExtra // If editing
  mode: 'create' | 'edit'
}

const props = defineProps<Props>()
const emit = defineEmits<{
  submit: [data: CreateCampExtraRequest]
  cancel: []
}>()
</script>
```

- **Implementation Steps**:
  1. PrimeVue form fields:
     - `InputText` for name
     - `Textarea` for description
     - `InputNumber` for price
     - `Dropdown` for pricingType (PerPerson / PerFamily)
     - `Dropdown` for pricingPeriod (OneTime / PerDay)
     - `Checkbox` for isRequired
     - `InputNumber` for maxQuantity (optional)
     - `InputNumber` for sortOrder
  2. Validation: name required, price >= 0
  3. Show example calculation based on selected pricing type/period
  4. Submit button

- **Spanish Labels**:
  - Name → "Nombre del extra"
  - Description → "Descripción"
  - Price → "Precio"
  - Pricing Type → "Tipo de precio"
    - PerPerson → "Por persona"
    - PerFamily → "Por familia"
  - Pricing Period → "Período de precio"
    - OneTime → "Una vez"
    - PerDay → "Por día"
  - Is Required → "Obligatorio para todos"
  - Max Quantity → "Cantidad máxima"
  - Sort Order → "Orden de visualización"

#### **File**: `frontend/src/components/camps/CampExtrasTable.vue`

- **Action**: Manage extras for a camp edition
- **Component Signature**:

```vue
<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useCampExtras } from '@/composables/useCampExtras'
import type { CampEditionExtra } from '@/types/camp-edition'

interface Props {
  editionId: string
}

const props = defineProps<Props>()
</script>
```

- **Implementation Steps**:
  1. Use `useCampExtras(props.editionId)` composable
  2. Fetch extras on mount
  3. Display in PrimeVue DataTable
  4. Columns: Name, Price, Pricing Type, Pricing Period, Required, Max Quantity, Current Quantity, Active, Actions
  5. "Add Extra" button → dialog with `CampExtraForm`
  6. Row actions: Edit, Activate/Deactivate, Delete (with validation)
  7. Sort by `sortOrder`
  8. Show "current quantity" with progress bar if maxQuantity is set

- **Implementation Notes**:
  - Delete validation: warn if registrations reference the extra
  - Deactivate instead of delete when extras are in use
  - Show pricing calculation examples in tooltips

---

### **Step 8: Create Age Range Settings Page**

#### **File**: `frontend/src/components/camps/AgeRangeSettings.vue`

- **Action**: Configure global age ranges (Board/Admin only)
- **Component Signature**:

```vue
<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useAssociationSettings } from '@/composables/useAssociationSettings'
import type { UpdateAgeRangesRequest } from '@/types/association-settings'
</script>
```

- **Implementation Steps**:
  1. Fetch current age ranges on mount
  2. Display in form with PrimeVue `InputNumber` for each value
  3. Show validation warnings about impact on pricing
  4. Real-time validation:
     - babyMaxAge >= 0
     - childMinAge > babyMaxAge
     - childMaxAge >= childMinAge
     - adultMinAge > childMaxAge
  5. Save button → `PUT /api/settings/age-ranges` → success toast
  6. Show current values and proposed changes side-by-side

- **Spanish Labels**:
  - "Configuración de Rangos de Edad"
  - "Edad máxima bebé" (Baby Max Age)
  - "Edad mínima niño" (Child Min Age)
  - "Edad máxima niño" (Child Max Age)
  - "Edad mínima adulto" (Adult Min Age)
  - Warning: "Cambiar los rangos de edad afectará el cálculo de precios para inscripciones pendientes"

- **Implementation Notes**:
  - This can be a section in Admin settings page or standalone page
  - Show example: "Bebé: 0-2 años, Niño: 3-12 años, Adulto: 13+ años"

---

### **Step 9: Update Router Configuration**

#### **File**: `frontend/src/router/index.ts`

- **Action**: Add routes for camp management pages
- **Implementation Steps**:
  1. Import new page components
  2. Add routes with proper guards (`requiresAuth: true, requiresBoard: true`)
  3. Set Spanish page titles in meta
  4. Use lazy loading with `() => import()`

- **Routes to Add**:

```typescript
{
  path: '/camps/locations',
  name: 'camp-locations',
  component: () => import('@/views/camps/CampLocationsPage.vue'),
  meta: {
    title: 'ABUVI | Ubicaciones de Campamento',
    requiresAuth: true,
    requiresBoard: true
  }
},
{
  path: '/camps/locations/:id',
  name: 'camp-location-detail',
  component: () => import('@/views/camps/CampLocationDetailPage.vue'),
  meta: {
    title: 'ABUVI | Detalles de Ubicación',
    requiresAuth: true,
    requiresBoard: true
  }
},
{
  path: '/camps/proposals',
  name: 'proposed-camps',
  component: () => import('@/views/camps/ProposedCampsPage.vue'),
  meta: {
    title: 'ABUVI | Propuestas de Campamento',
    requiresAuth: true,
    requiresBoard: true
  }
},
{
  path: '/camps/editions',
  name: 'camp-editions',
  component: () => import('@/views/camps/CampEditionsPage.vue'),
  meta: {
    title: 'ABUVI | Ediciones de Campamento',
    requiresAuth: true,
    requiresBoard: true
  }
},
{
  path: '/camps/editions/:id',
  name: 'camp-edition-detail',
  component: () => import('@/views/camps/CampEditionDetailPage.vue'),
  meta: {
    title: 'ABUVI | Detalles de Edición',
    requiresAuth: true,
    requiresBoard: true
  }
}
```

- **Dependencies**: Ensure route guard checks `auth.isBoard` for Board-level access

---

### **Step 10: Write Vitest Unit Tests**

#### **File**: `frontend/src/composables/__tests__/useCamps.test.ts`

- **Action**: Unit test for `useCamps` composable
- **Implementation Steps**:
  1. Mock `api` with `vi.mock('@/utils/api')`
  2. Test `fetchCamps` success case
  3. Test `fetchCamps` error case
  4. Test `createCamp` success case
  5. Test `updateCamp` success case
  6. Test `deleteCamp` success case
  7. Verify loading states
  8. Verify error messages (Spanish)

- **Test Coverage**:
  - Happy path: successful API calls
  - Error handling: network errors, API errors
  - Loading states: `loading.value` true/false at correct times

#### **File**: `frontend/src/composables/__tests__/useCampEditions.test.ts`

- **Action**: Unit test for `useCampEditions` composable
- **Implementation Steps**:
  1. Test `fetchProposedEditions` with year filter
  2. Test `getActiveEdition` success/empty
  3. Test `proposeEdition` success
  4. Test `promoteEdition` status transition
  5. Test `rejectEdition` with reason
  6. Test `changeStatus` validation

#### **File**: `frontend/src/components/camps/__tests__/CampStatusBadge.test.ts`

- **Action**: Component test for status badge
- **Implementation Steps**:
  1. Test rendering for each status (Proposed, Draft, Open, Closed, Completed)
  2. Verify correct Spanish label
  3. Verify correct color classes
  4. Test size prop

#### **File**: `frontend/src/components/camps/__tests__/PricingBreakdown.test.ts`

- **Action**: Component test for pricing display
- **Implementation Steps**:
  1. Test rendering with mock age ranges
  2. Verify currency formatting
  3. Verify age labels

#### **File**: `frontend/src/components/camps/__tests__/CampLocationForm.test.ts`

- **Action**: Component test for camp location form
- **Implementation Steps**:
  1. Test form validation rules
  2. Test submit event emission
  3. Test cancel event emission
  4. Test pre-population in edit mode
  5. Test error display for invalid inputs

---

### **Step 11: Write Cypress E2E Tests**

#### **File**: `frontend/cypress/e2e/camp-locations.cy.ts`

- **Action**: E2E test for camp locations management
- **Test Scenarios**:
  1. **View camp locations**: Board user can see list of camps
  2. **Create camp location**: Fill form, save, verify in list
  3. **Edit camp location**: Open edit dialog, change name, save, verify update
  4. **Delete camp location**: Delete camp without editions, verify removal
  5. **Map interaction**: Click camp on map, verify details

- **Implementation Steps**:
  1. `beforeEach`: Login as Board user, navigate to `/camps/locations`
  2. Use `data-testid` attributes for element selection
  3. Test happy paths and validation errors

#### **File**: `frontend/cypress/e2e/camp-proposals.cy.ts`

- **Action**: E2E test for proposal workflow
- **Test Scenarios**:
  1. **Propose camp**: Create new proposal, verify in proposals list
  2. **Compare proposals**: View comparison table, verify data
  3. **Promote proposal**: Select proposal, promote to draft, verify redirect
  4. **Reject proposal**: Reject with reason, verify removal

#### **File**: `frontend/cypress/e2e/camp-editions.cy.ts`

- **Action**: E2E test for camp edition management
- **Test Scenarios**:
  1. **Create camp edition**: Select camp, fill form, create draft
  2. **Change status**: Draft → Open → Closed → Completed (full workflow)
  3. **Add extras**: Create multiple extras, verify in table
  4. **Edit extras**: Modify price, verify update
  5. **Delete extras**: Delete extra, confirm removal

---

### **Step 12: Update Technical Documentation**

- **Action**: Review and update technical documentation
- **Implementation Steps**:
  1. **Review Changes**: Analyze all frontend code changes made
  2. **Identify Documentation Files**:
     - Update `ai-specs/specs/frontend-standards.mdc` with any new component patterns
     - Update routing documentation if significant routing changes
     - Update `ai-specs/specs/api-spec.yml` if API contracts changed (frontend perspective)
  3. **Update Documentation**:
     - Document new components in `frontend-standards.mdc` (if establishing new patterns)
     - Add camp management routes to routing documentation
     - Document Leaflet.js integration patterns
     - Update testing documentation with examples from camp feature tests
  4. **Verify Documentation**:
     - Ensure all changes are accurately reflected
     - Check consistency with existing structure
     - Verify English language usage
  5. **Report Updates**: List which files were updated and changes made

- **References**:
  - Follow `ai-specs/specs/documentation-standards.mdc`
  - All documentation in English

- **Notes**: This step is MANDATORY before implementation is complete

---

## Implementation Order

1. **Step 0**: Create Feature Branch (`feature/camp-crud-frontend`)
2. **Step 1**: Define TypeScript Interfaces (types)
3. **Step 2**: Create API Composables (useCamps, useCampEditions, useCampExtras, useAssociationSettings)
4. **Step 3**: Create Shared UI Components (CampStatusBadge, PricingBreakdown, CampLocationMap)
5. **Step 4**: Create Camp Location Pages (CampLocationsPage, CampLocationDetailPage, CampLocationCard, CampLocationForm)
6. **Step 5**: Create Proposal Management Pages (ProposedCampsPage, ProposalCard, ProposalComparison)
7. **Step 6**: Create Camp Edition Management Pages (CampEditionsPage, CampEditionDetailPage, CampEditionCard, CampEditionForm)
8. **Step 7**: Create Camp Extras Management (CampExtrasTable, CampExtraForm)
9. **Step 8**: Create Age Range Settings (AgeRangeSettings component)
10. **Step 9**: Update Router Configuration
11. **Step 10**: Write Vitest Unit Tests (composables, components)
12. **Step 11**: Write Cypress E2E Tests (user workflows)
13. **Step 12**: Update Technical Documentation

## Testing Checklist

### Post-Implementation Verification

- [ ] All Vitest unit tests pass (`npx vitest`)
- [ ] All Cypress E2E tests pass (`npx cypress run`)
- [ ] Test coverage >= 90% for composables and components
- [ ] TypeScript compilation succeeds (`npx vue-tsc --noEmit`)
- [ ] No ESLint errors (`npm run lint`)
- [ ] All user-facing text is in Spanish
- [ ] All code/comments/types are in English
- [ ] PrimeVue components used consistently
- [ ] Tailwind CSS used for all styling (no `<style>` blocks)
- [ ] Loading states displayed during API calls
- [ ] Error messages displayed in Spanish with Toast
- [ ] Forms validate inputs before submission
- [ ] Route guards enforce Board-only access
- [ ] Responsive design tested (mobile, tablet, desktop)
- [ ] Leaflet map renders correctly with markers
- [ ] Proposal workflow tested end-to-end (propose → promote → reject)
- [ ] Status transitions tested (Draft → Open → Closed → Completed)
- [ ] Extras management tested (create, edit, delete, quantity limits)
- [ ] Age range configuration tested with validation

### Component Functionality Verification

- [ ] `CampLocationCard` displays all camp info correctly
- [ ] `CampLocationForm` validates and submits correctly
- [ ] `CampLocationMap` shows markers and handles clicks
- [ ] `CampStatusBadge` shows correct colors and Spanish labels
- [ ] `PricingBreakdown` formats currency and displays age ranges
- [ ] `ProposalCard` displays proposal details and action buttons
- [ ] `ProposalComparison` compares multiple proposals side-by-side
- [ ] `CampEditionForm` validates dates, pricing, age ranges
- [ ] `CampEditionCard` shows correct actions based on status
- [ ] `CampExtrasTable` manages extras with quantity tracking
- [ ] `CampExtraForm` validates pricing configuration
- [ ] `AgeRangeSettings` validates age range consistency

### Error Handling Verification

- [ ] Network errors display Spanish error messages
- [ ] API validation errors displayed in forms
- [ ] Delete confirmation dialogs shown before destructive actions
- [ ] Forbidden actions (e.g., delete camp with editions) show warnings
- [ ] Status transition validation prevents invalid transitions
- [ ] Empty states displayed when no data available
- [ ] Loading spinners shown during async operations

## Error Handling Patterns

### Composable Error Handling

All composables follow this pattern:

```typescript
const fetchCamps = async () => {
  loading.value = true
  error.value = null
  try {
    const response = await api.get<ApiResponse<Camp[]>>('/camps')
    camps.value = response.data.data ?? []
  } catch (err: any) {
    error.value = err.response?.data?.error?.message || 'Error al cargar campamentos'
    console.error('Failed to fetch camps:', err)
  } finally {
    loading.value = false
  }
}
```

### Component Error Display

Use PrimeVue Toast for global notifications:

```typescript
import { useToast } from 'primevue/usetoast'

const toast = useToast()

const handleCreateCamp = async (data: CreateCampRequest) => {
  const result = await createCamp(data)
  if (result) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: 'Campamento creado correctamente',
      life: 3000
    })
    showDialog.value = false
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Error al crear campamento',
      life: 5000
    })
  }
}
```

### Form Validation Errors

Display validation errors inline with PrimeVue form components:

```vue
<InputText
  id="name"
  v-model="formData.name"
  :invalid="!!errors.name"
  class="w-full"
/>
<small v-if="errors.name" class="text-red-500">{{ errors.name }}</small>
```

## UI/UX Considerations

### PrimeVue Component Usage

**DataTable** for lists:

- Use `striped-rows`, `paginator`, `rows="10"`
- Column sorting with `sortable`
- Custom templates for status badges, actions
- Filter templates for search/status filters

**Dialog** for forms:

- Use `modal` for blocking dialogs
- Set `class="w-full max-w-lg"` for consistent sizing
- `v-model:visible` for show/hide control

**Calendar** for dates:

- Use `date-format="dd/mm/yy"` (Spanish format)
- Set `min-date` and `max-date` for validation

**Button** for actions:

- Use `severity` prop: `primary`, `secondary`, `success`, `danger`
- Use `icon` prop with PrimeIcons: `pi pi-plus`, `pi pi-pencil`, `pi pi-trash`
- Use `loading` prop during async operations

**ConfirmDialog** for destructive actions:

- Always confirm before delete
- Use Spanish messages: "¿Estás seguro de que quieres eliminar este campamento?"

### Tailwind CSS Patterns

**Responsive Grid**:

```html
<div class="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
  <!-- Cards -->
</div>
```

**Card Styling**:

```html
<div class="rounded-lg border border-gray-200 bg-white p-6 shadow-sm hover:shadow-md transition-shadow">
  <!-- Content -->
</div>
```

**Form Layout**:

```html
<form class="flex flex-col gap-4">
  <div>
    <label class="mb-1 block text-sm font-medium">Label</label>
    <InputText class="w-full" />
  </div>
</form>
```

### Responsive Design

- **Mobile-first**: Design for mobile, enhance for desktop
- **Breakpoints**: `sm:` (640px), `md:` (768px), `lg:` (1024px)
- **DataTables**: Use PrimeVue's responsive mode for mobile
- **Dialogs**: Full-screen on mobile, modal on desktop
- **Map**: Collapsible on mobile, always visible on desktop

### Accessibility

- Use semantic HTML: `<main>`, `<section>`, `<article>`
- Add `aria-label` to interactive elements
- Ensure keyboard navigation works (Tab, Enter, Escape)
- PrimeVue components have built-in ARIA support
- Test with screen reader

### Loading States

- Show `ProgressSpinner` during data fetching
- Disable buttons during form submission with `loading` prop
- Skeleton loaders for tables (optional enhancement)

### User Feedback

- Toast notifications for success/error
- Inline validation errors in forms
- Confirmation dialogs for destructive actions
- Empty states with helpful messages

## Dependencies

### npm Packages Required

```bash
# Core dependencies (already in project)
vue
vue-router
pinia
axios
primevue
primeicons
tailwindcss

# New dependencies to install
npm install leaflet @types/leaflet

# Dev dependencies (already in project)
@vitejs/plugin-vue
typescript
vitest
@vue/test-utils
cypress
```

### PrimeVue Components Used

- `DataTable`, `Column`
- `Dialog`, `ConfirmDialog`
- `Button`
- `InputText`, `InputNumber`, `Textarea`, `Dropdown`, `Calendar`, `Checkbox`
- `ProgressSpinner`
- `Message`
- `Toast` (with `useToast` composable)
- `Badge` (optional, can use custom span)

### Third-Party Packages

- **Leaflet.js**: Interactive maps for camp locations
  - Justification: Lightweight, open-source, mobile-friendly, no API key required
  - Alternative considered: Google Maps (requires API key, more complex)

## Notes

### Important Reminders

1. **Language Requirements**:
   - All user-facing text MUST be in Spanish
   - All code (variables, functions, types) MUST be in English
   - All comments MUST be in English

2. **TypeScript Strict Typing**:
   - No `any` types allowed
   - All interfaces must be defined in `src/types/`
   - Use generics for composables when appropriate

3. **Component Patterns**:
   - Always use `<script setup lang="ts">`
   - Never call API directly from components (use composables)
   - Use PrimeVue for complex UI, Tailwind for layout
   - No `<style>` blocks (Tailwind only)

4. **Testing**:
   - 90% coverage target for all code
   - Test composables independently
   - Test component rendering and events
   - E2E tests for critical user workflows

5. **Performance**:
   - Lazy load route components with `() => import()`
   - Use computed values for derived state
   - Debounce search inputs (if needed)
   - Optimize Leaflet map rendering (cleanup in `onUnmounted`)

### Business Rules

1. **Authorization**:
   - All camp management features require Board role
   - Verify `auth.isBoard` in route guards

2. **Validation**:
   - Camp location: latitude (-90 to 90), longitude (-180 to 180)
   - Camp edition: endDate > startDate, year >= current year
   - Age ranges: childMinAge > babyMaxAge, adultMinAge > childMaxAge
   - Pricing: all prices >= 0

3. **Workflow**:
   - Proposal status: Proposed → (promote) → Draft or (reject) → archived
   - Edition status: Draft → Open → Closed → Completed (no backward transitions)

4. **Extras**:
   - Required extras auto-added to new registrations
   - Extras with maxQuantity cannot exceed limit
   - Cannot delete extras referenced by registrations (deactivate instead)

5. **Age Ranges**:
   - Global settings affect all editions with `useCustomAgeRanges = false`
   - Editions can override with custom age ranges
   - Warn when changing global settings (impacts pricing)

## Next Steps After Implementation

1. **Integration Testing**:
   - Test with real backend API (not mocks)
   - Verify API contract matches frontend types
   - Test error responses from backend

2. **Code Review**:
   - Review all code against frontend standards
   - Check TypeScript types and coverage
   - Verify Spanish translations and gender agreement
   - Validate accessibility compliance

3. **User Acceptance Testing**:
   - Board users test proposal workflow
   - Test camp creation and edition management
   - Test extras configuration
   - Test map interaction

4. **Performance Testing**:
   - Measure page load times
   - Test with large datasets (100+ camps, 500+ editions)
   - Optimize if needed

5. **Documentation**:
   - Update user guide with camp management instructions
   - Document Board workflows
   - Create screenshots for documentation

6. **Deployment**:
   - Build production bundle: `npm run build`
   - Test production build: `npm run preview`
   - Deploy to staging environment
   - Smoke test on staging
   - Deploy to production

## Implementation Verification

### Final Verification Checklist

**Code Quality**:

- [ ] TypeScript strict mode enabled, no errors
- [ ] No `any` types in code
- [ ] All components use `<script setup lang="ts">`
- [ ] ESLint passes with no warnings
- [ ] Prettier formatting applied

**Functionality**:

- [ ] All components render correctly
- [ ] All API calls work (test with backend)
- [ ] Forms validate and submit correctly
- [ ] Loading states displayed
- [ ] Error handling works

**Testing**:

- [ ] Vitest unit tests >= 90% coverage
- [ ] All Vitest tests pass
- [ ] Cypress E2E tests cover critical workflows
- [ ] All Cypress tests pass

**Integration**:

- [ ] Composables connect to backend API correctly
- [ ] API responses match TypeScript types
- [ ] Authentication/authorization works
- [ ] Route guards enforce Board access

**Documentation**:

- [ ] Technical documentation updated
- [ ] Component patterns documented (if new patterns established)
- [ ] API integration documented
- [ ] Testing patterns documented

**UI/UX**:

- [ ] Responsive design works on mobile, tablet, desktop
- [ ] PrimeVue components styled correctly
- [ ] Tailwind CSS used consistently
- [ ] Accessibility tested (keyboard navigation, ARIA labels)
- [ ] Spanish text with correct gender agreement

**Deployment Ready**:

- [ ] Production build succeeds: `npm run build`
- [ ] Production preview works: `npm run preview`
- [ ] No console errors in production mode
- [ ] Environment variables configured correctly

---

**Implementation Plan Complete**

This plan provides complete step-by-step instructions for implementing the Camp CRUD frontend feature following Vue 3 Composition API patterns, PrimeVue components, and Tailwind CSS styling. The developer can now proceed autonomously with implementation.

**Next Action**: Start implementation by creating the feature branch (Step 0), then proceed sequentially through all steps.

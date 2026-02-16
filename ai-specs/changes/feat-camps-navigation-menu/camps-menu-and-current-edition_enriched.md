# Feature: Camps Navigation Menu and Current Camp Edition Display

## Task ID

`feat-camps-navigation-menu`

## User Story

**As a** Board member or authorized user
**I want to** access a dedicated Camps section from the navigation menu and see the current camp edition information on the "Campamento 2026" page
**So that** I can easily manage camps and view relevant camp edition details based on what's currently active or selected

## Problem Statement

Currently, the application has:

1. A single navigation menu item "Campamento" pointing to `/camp` that shows placeholder content
2. No distinction between:
   - The general camps management section (list/CRUD of all camps and editions)
   - The current camp edition view (information about the active or selected camp for the year)
3. No way to indicate which camp edition is "current" or "selected" for display
4. The "Campamento 2026" page is hardcoded and doesn't dynamically show the current year's camp edition

## Requirements Summary

1. **Add centralized Admin menu entry** - consolidate all administrative functions
   - Visible only to authorized users (Board/Admin roles)
   - Provides access to:
     - Camps management (list/CRUD of all camp editions and locations)
     - Family units management (view/manage all family units)
     - Users management (existing functionality)

2. **Update "Campamento 2026" page** to dynamically show:
   - **Primary**: Information about the current camp edition if one is assigned/selected
   - **Fallback**: Information about the most recent camp edition from the previous year if no current edition exists

3. **Determine "current" or "selected" camp logic**:
   - System should identify the active camp edition for the current year
   - If none exists, fallback to the most recent camp edition (latest year with status = 'Completed' or 'Closed')

4. **User Profile - Family Unit Management**:
   - All users can access their family unit management from their profile page
   - Option 1: Embed family unit management directly in profile
   - Option 2: Link/button to navigate to dedicated family unit management page
   - Users can view, create, and edit their own family unit and members

5. **Admin Panel - Centralized Administration**:
   - Board/Admin users access a unified admin dashboard
   - Tabbed or sectioned interface for different admin functions:
     - **Campamentos**: List and manage all camps/editions
     - **Unidades Familiares**: List and manage all family units
     - **Usuarios**: List and manage all users
   - Each section provides CRUD operations with appropriate filters and search

## Proposed Solution

### 1. Navigation Menu Changes

#### Centralize Admin Functions Under "Administración" (Board/Admin Only)

**Location**: [`frontend/src/components/layout/AppHeader.vue`](frontend/src/components/layout/AppHeader.vue)

**Current State**:
```typescript
const adminBoardLinks = [
  { label: 'Usuarios', path: '/users', icon: 'pi pi-users' }
]

// Separate Admin button (lines 80-91)
<router-link v-if="auth.isAdmin" to="/admin" ...>
  Administración
</router-link>
```

**New Structure**:
```typescript
// REMOVE adminBoardLinks array entirely
// const adminBoardLinks = [] // DELETE THIS

// UPDATE Admin button visibility from isAdmin to isBoard
<router-link v-if="auth.isBoard" to="/admin" ...>
  Administración
</router-link>
```

**Changes**:
- **Remove** the `adminBoardLinks` array and its references (lines 20-22, 64-77, 132-147)
- **Remove** separate "Usuarios" link from navigation
- **Update** "Administración" button visibility from `auth.isAdmin` to `auth.isBoard`
- **Keep** the admin button styling and icon

**Behavior**:
- Visible to both Admin AND Board users (`auth.isBoard === true`)
- Shows in both desktop and mobile navigation
- Links to `/admin` route which will have a tabbed interface for:
  - Campamentos management
  - Unidades Familiares management
  - Usuarios management

**Rationale**: Centralize all administrative functions under one menu entry instead of cluttering the navigation with multiple admin-specific items.

#### Keep "Campamento" Menu Item (All Users)

**Changes**:
- Keep in main navigation for all authenticated users
- Keep label as "Campamento" (simple and clean)
- Route remains `/camp`

**Recommendation**: Keep static label "Campamento" since the year is dynamically shown on the page itself

#### Add "Mi Familia" Link to Profile Menu or Page

**Location**: User profile page or user dropdown menu

**New Feature**:
- Add quick access to family unit management from user profile
- Implementation options:
  - **Option A**: Add "Mi Familia" section directly in profile page with embedded management
  - **Option B**: Add "Gestionar Familia" button in profile that navigates to `/family-unit/me`

**Recommendation**: Use Option B (dedicated page) for better organization and reusability

### 2. Router Changes

**Location**: [`frontend/src/router/index.ts`](frontend/src/router/index.ts)

**Update existing Admin route** to use enhanced AdminPage:

```typescript
{
  path: '/admin',
  name: 'admin',
  component: () => import('@/views/AdminPage.vue'),
  meta: {
    requiresAuth: true,
    requiresBoard: true,  // CHANGED from requiresAdmin
    title: 'ABUVI | Administración'
  }
}
```

**Add route for Family Unit management (user's own)**:

```typescript
{
  path: '/family-unit/me',
  name: 'my-family-unit',
  component: () => import('@/views/family/MyFamilyUnitPage.vue'),
  meta: {
    requiresAuth: true,
    title: 'ABUVI | Mi Unidad Familiar'
  }
}
```

**Update existing Camp route**:

```typescript
{
  path: '/camp',
  name: 'camp',
  component: () => import('@/views/CampPage.vue'),
  meta: {
    requiresAuth: true,
    title: 'ABUVI | Campamento'
  }
}
```

**Remove legacy /users route** (will be accessed via /admin):

```typescript
// DELETE THIS ROUTE - functionality moved to /admin
// {
//   path: '/users',
//   name: 'users',
//   component: () => import('@/pages/UsersPage.vue'),
//   ...
// }
```

**Note**: The `/users` and `/users/:id` routes will be deprecated. User management will be accessed through the Admin page tabs.

### 3. Admin Page Structure

#### Update AdminPage.vue to Tabbed Interface

**Location**: [`frontend/src/views/AdminPage.vue`](frontend/src/views/AdminPage.vue)

**Current State**: Basic admin page placeholder

**New Structure**: Tabbed interface with three main sections

```vue
<script setup lang="ts">
import { ref } from 'vue'
import TabView from 'primevue/tabview'
import TabPanel from 'primevue/tabpanel'
import Container from '@/components/ui/Container.vue'
import CampsAdminPanel from '@/components/admin/CampsAdminPanel.vue'
import FamilyUnitsAdminPanel from '@/components/admin/FamilyUnitsAdminPanel.vue'
import UsersAdminPanel from '@/components/admin/UsersAdminPanel.vue'

const activeTab = ref(0)
</script>

<template>
  <Container>
    <div class="py-12">
      <h1 class="text-4xl font-bold text-gray-900 mb-8">Panel de Administración</h1>

      <TabView v-model:activeIndex="activeTab">
        <!-- Tab 1: Campamentos -->
        <TabPanel>
          <template #header>
            <div class="flex items-center gap-2">
              <i class="pi pi-map-marker" />
              <span>Campamentos</span>
            </div>
          </template>
          <CampsAdminPanel />
        </TabPanel>

        <!-- Tab 2: Unidades Familiares -->
        <TabPanel>
          <template #header>
            <div class="flex items-center gap-2">
              <i class="pi pi-users" />
              <span>Unidades Familiares</span>
            </div>
          </template>
          <FamilyUnitsAdminPanel />
        </TabPanel>

        <!-- Tab 3: Usuarios -->
        <TabPanel>
          <template #header>
            <div class="flex items-center gap-2">
              <i class="pi pi-user" />
              <span>Usuarios</span>
            </div>
          </template>
          <UsersAdminPanel />
        </TabPanel>
      </TabView>
    </div>
  </Container>
</template>
```

**Components to Create**:

1. **CampsAdminPanel.vue**: List/CRUD for camps and camp editions
   - DataTable with camps/editions
   - Search, filter by year/status
   - Create, Edit, Delete actions
   - Status change actions

2. **FamilyUnitsAdminPanel.vue**: List/CRUD for all family units
   - DataTable with all family units
   - Search by family name, representative
   - View family members
   - Edit family unit details
   - Export functionality

3. **UsersAdminPanel.vue**: Existing users management (migrated from UsersPage)
   - Reuse existing UsersPage component content
   - DataTable with users
   - Search, filter by role
   - Edit user roles

### 4. User Profile - Family Unit Access

#### Update ProfilePage.vue

**Location**: [`frontend/src/views/ProfilePage.vue`](frontend/src/views/ProfilePage.vue)

**Add Family Unit Section**:

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import Card from 'primevue/card'
import Button from 'primevue/button'
import { useRouter } from 'vue-router'

const router = useRouter()
const { myFamilyUnit, loading, fetchMyFamilyUnit } = useFamilyUnits()

onMounted(async () => {
  await fetchMyFamilyUnit()
})

const goToFamilyManagement = () => {
  router.push('/family-unit/me')
}
</script>

<template>
  <!-- ... existing profile sections ... -->

  <!-- Family Unit Section -->
  <Card class="mt-6">
    <template #title>
      <div class="flex items-center gap-2">
        <i class="pi pi-users" />
        Mi Unidad Familiar
      </div>
    </template>
    <template #content>
      <!-- No Family Unit -->
      <div v-if="!loading && !myFamilyUnit" class="text-center py-6">
        <p class="text-gray-600 mb-4">
          Aún no has creado tu unidad familiar. Crea una para poder inscribirte en campamentos.
        </p>
        <Button
          label="Crear Unidad Familiar"
          icon="pi pi-plus"
          @click="goToFamilyManagement"
        />
      </div>

      <!-- Has Family Unit -->
      <div v-else-if="myFamilyUnit" class="flex items-center justify-between">
        <div>
          <h3 class="text-lg font-semibold mb-1">{{ myFamilyUnit.name }}</h3>
          <p class="text-gray-600 text-sm">
            {{ myFamilyUnit.membersCount || 0 }} miembros
          </p>
        </div>
        <Button
          label="Gestionar"
          icon="pi pi-pencil"
          outlined
          @click="goToFamilyManagement"
        />
      </div>

      <!-- Loading -->
      <div v-else class="text-center py-4">
        <i class="pi pi-spin pi-spinner text-2xl text-primary-500" />
      </div>
    </template>
  </Card>
</template>
```

### 5. Backend API Changes

#### New Endpoint: Get Current Camp Edition

**Endpoint**: `GET /api/camps/current`

**Purpose**: Returns the "current" or most recent camp edition

**Logic**:

1. Search for camp editions with `year === currentYear` and `status IN ['Open', 'Closed']`
2. If multiple exist, return the one with status 'Open' first, then 'Closed'
3. If none found for current year, return the most recent camp edition from previous years (order by year DESC, status priority: 'Completed' > 'Closed')
4. If still none found, return 404

**Response Schema**:

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "campId": "uuid",
    "year": 2026,
    "name": "Mountain Camp 2026",
    "startDate": "2026-07-15",
    "endDate": "2026-07-30",
    "location": "Swiss Alps region",
    "description": "...",
    "basePrice": 450.00,
    "minAge": 8,
    "maxAge": 17,
    "maxCapacity": 120,
    "contactEmail": "camp@abuvi.org",
    "contactPhone": "+34612345678",
    "status": "Open",
    "isArchived": false,
    "createdAt": "2026-01-15T10:00:00Z",
    "updatedAt": "2026-01-15T10:00:00Z",
    "camp": {
      "id": "uuid",
      "name": "Mountain Camp",
      "latitude": 46.8182,
      "longitude": 8.2275
    },
    "registrationCount": 45,
    "availableSpots": 75
  }
}
```

**Implementation Details**:

- Location: `backend/Controllers/CampsController.cs`
- Method: `GetCurrentCampEdition()`
- Authorization: `[Authorize]` - All authenticated users can view current camp
- Add business logic to determine "current" based on year and status

#### Optional: Add User Preference for Selected Camp

If you want users to "select" a preferred camp edition (rather than just showing the current one):

**New field in User table**:

- `selectedCampEditionId`: UUID (nullable, FK to CampEdition)

**New endpoint**: `PUT /api/users/me/selected-camp`

```json
{
  "campEditionId": "uuid"
}
```

**Updated logic for `GET /api/camps/current`**:

1. Check if authenticated user has `selectedCampEditionId` set
2. If yes, return that camp edition
3. Otherwise, fallback to the algorithm described above

#### Backend Support for Family Units Management

**Note**: The family units API endpoints already exist (see [api-endpoints.md](../../specs/api-endpoints.md#family-units-endpoints)). The following enhancements may be needed:

**Verify/Add Endpoint**: `GET /api/family-units/me`

**Purpose**: Get the authenticated user's family unit (shorthand for common operation)

**Response**: Same as `GET /api/family-units/{id}` but automatically resolves to the current user's family unit

**If not exists, can use existing**: `GET /api/family-units/me` (check if this already exists in the API)

**For Admin Panel**: `GET /api/family-units` with pagination

**Purpose**: List all family units for Board/Admin users

**Query Parameters**:
- `page`: Page number (default: 1)
- `pageSize`: Items per page (default: 20, max: 100)
- `search`: Search by family name or representative name
- `sortBy`: Field to sort by (name, createdAt)
- `sortOrder`: asc | desc

**Response Schema**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "uuid",
        "name": "Garcia Family",
        "representativeUserId": "uuid",
        "representativeName": "Juan Garcia",
        "membersCount": 4,
        "createdAt": "2026-01-15T10:00:00Z",
        "updatedAt": "2026-01-15T10:00:00Z"
      }
    ],
    "totalCount": 45,
    "page": 1,
    "pageSize": 20,
    "totalPages": 3
  }
}
```

**Authorization**: `[Authorize(Roles = "Admin,Board")]`

### 6. Frontend Components Changes

#### Update CampPage.vue

**Location**: [`frontend/src/views/CampPage.vue`](frontend/src/views/CampPage.vue)

**Current State**: Placeholder content showing "Campamento 2026"

**New Implementation**:

```vue
<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useCampEditions } from '@/composables/useCampEditions'
import Container from '@/components/ui/Container.vue'
import CampEditionDetails from '@/components/camps/CampEditionDetails.vue'
import CampStatusBadge from '@/components/camps/CampStatusBadge.vue'
import Card from 'primevue/card'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'

const { currentCampEdition, loading, error, fetchCurrentCampEdition } = useCampEditions()

onMounted(async () => {
  await fetchCurrentCampEdition()
})

const displayTitle = computed(() => {
  if (currentCampEdition.value) {
    return `Campamento ${currentCampEdition.value.year}`
  }
  return 'Campamento'
})

const isFromPreviousYear = computed(() => {
  if (!currentCampEdition.value) return false
  const currentYear = new Date().getFullYear()
  return currentCampEdition.value.year < currentYear
})
</script>

<template>
  <Container>
    <div class="py-12">
      <!-- Loading State -->
      <div v-if="loading" class="flex justify-center items-center py-20">
        <ProgressSpinner />
      </div>

      <!-- Error State -->
      <Message v-else-if="error" severity="error" :closable="false">
        {{ error }}
      </Message>

      <!-- No Camp Edition Found -->
      <Message v-else-if="!currentCampEdition" severity="info" :closable="false">
        <p class="mb-2">No hay información de campamento disponible para este año.</p>
        <p class="text-sm">Contacta con la junta directiva para más información.</p>
      </Message>

      <!-- Camp Edition Display -->
      <div v-else>
        <!-- Header with Title and Status -->
        <div class="mb-6 flex items-start justify-between">
          <div>
            <h1 class="text-4xl font-bold text-gray-900 mb-2">
              {{ displayTitle }}
            </h1>
            <div class="flex items-center gap-3">
              <CampStatusBadge :status="currentCampEdition.status" />
              <Message
                v-if="isFromPreviousYear"
                severity="warn"
                :closable="false"
                class="inline-flex"
              >
                Mostrando información del campamento de {{ currentCampEdition.year }}
              </Message>
            </div>
          </div>
        </div>

        <!-- Camp Edition Details Component -->
        <CampEditionDetails :camp-edition="currentCampEdition" />

        <!-- Registration CTA (if status is Open) -->
        <Card v-if="currentCampEdition.status === 'Open'" class="mt-6 bg-primary-50">
          <template #content>
            <div class="text-center">
              <h3 class="text-xl font-semibold text-primary-700 mb-2">
                ¡Inscripciones Abiertas!
              </h3>
              <p class="text-gray-700 mb-4">
                Quedan {{ currentCampEdition.availableSpots }} plazas disponibles de {{ currentCampEdition.maxCapacity }}
              </p>
              <router-link
                to="/registrations/new"
                class="inline-flex items-center gap-2 px-6 py-3 bg-primary-600 text-white font-semibold rounded-lg hover:bg-primary-700 transition-colors"
              >
                <i class="pi pi-plus-circle" />
                Inscribir mi familia
              </router-link>
            </div>
          </template>
        </Card>
      </div>
    </div>
  </Container>
</template>
```

#### Create CampEditionDetails.vue Component

**Location**: `frontend/src/components/camps/CampEditionDetails.vue` (NEW)

**Purpose**: Reusable component to display camp edition information

**Props**:

- `campEdition`: CampEdition object

**Content**:

- Camp name and year
- Dates (start/end)
- Location with map pin (if coordinates available)
- Description (rich text)
- Pricing information
- Age requirements
- Capacity and availability
- Contact information

#### Create CampsManagementPage.vue

**Location**: `frontend/src/views/camps/CampsManagementPage.vue` (NEW)

**Purpose**: Board/Admin page to manage all camps and editions

**Features**:

- Tabbed interface:
  - Tab 1: Camp Locations (list with map)
  - Tab 2: Camp Editions (list with filters by year, status)
  - Tab 3: Create new edition
- CRUD operations for camps and editions
- Search and filter functionality
- Bulk actions (archive, status changes)

**Note**: This page can be implemented in a separate task/ticket. For this story, just create a basic placeholder that says "Camp management interface - coming soon" with a link back to current camp.

### 5. Composables Changes

#### Create or Update useCampEditions.ts

**Location**: [`frontend/src/composables/useCampEditions.ts`](frontend/src/composables/useCampEditions.ts)

**New/Updated Methods**:

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { CampEdition } from '@/types/camp-edition'
import type { ApiResponse } from '@/types/api'

export function useCampEditions() {
  const currentCampEdition = ref<CampEdition | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchCurrentCampEdition = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampEdition>>('/camps/current')
      if (response.data.success && response.data.data) {
        currentCampEdition.value = response.data.data
      } else {
        currentCampEdition.value = null
      }
    } catch (err: unknown) {
      // 404 is expected if no camp edition exists
      if ((err as any)?.response?.status === 404) {
        currentCampEdition.value = null
        error.value = null
      } else {
        error.value = (err as { response?: { data?: { error?: { message?: string } } } })
          ?.response?.data?.error?.message || 'Error al cargar campamento actual'
        console.error('Failed to fetch current camp edition:', err)
      }
    } finally {
      loading.value = false
    }
  }

  return {
    currentCampEdition,
    loading,
    error,
    fetchCurrentCampEdition
  }
}
```

### 6. Type Definitions

#### Update camp-edition.ts

**Location**: [`frontend/src/types/camp-edition.ts`](frontend/src/types/camp-edition.ts)

**Ensure it includes**:

```typescript
export interface CampEdition {
  id: string
  campId: string
  year: number
  name?: string
  startDate: string
  endDate: string
  location: string
  description?: string
  basePrice: number
  minAge: number
  maxAge: number
  maxCapacity: number
  contactEmail?: string
  contactPhone?: string
  status: CampEditionStatus
  isArchived: boolean
  createdAt: string
  updatedAt: string

  // Populated relationships
  camp?: {
    id: string
    name: string
    latitude: number
    longitude: number
  }

  // Computed fields from backend
  registrationCount?: number
  availableSpots?: number
}

export type CampEditionStatus = 'Draft' | 'Open' | 'Closed' | 'Completed'
```

## Implementation Steps

### Phase 1: Backend API (Priority: HIGH)

1. **Create endpoint `GET /api/camps/current`**
   - File: `backend/Controllers/CampsController.cs`
   - Add method `GetCurrentCampEdition()`
   - Implement logic to determine current camp edition:
     - Query CampEditions where year = current year and status IN ('Open', 'Closed')
     - If none, query most recent year with status 'Completed' or 'Closed'
     - Return 404 if no editions exist
   - Include related Camp data (coordinates, name)
   - Add computed fields: `registrationCount`, `availableSpots`

2. **Verify/Create endpoint `GET /api/family-units`** (for admin panel)
   - File: `backend/Controllers/FamilyUnitsController.cs`
   - Add paginated list endpoint with search/filter
   - Authorization: Board/Admin only
   - Include representative user name and members count
   - Support query parameters: page, pageSize, search, sortBy, sortOrder

3. **Verify endpoint `GET /api/family-units/me`** exists
   - Should return authenticated user's family unit
   - If doesn't exist, users will use `GET /api/family-units/{id}` with their familyUnitId

4. **Add unit tests**
   - Test file: `backend.Tests/Controllers/CampsControllerTests.cs`
   - Test cases:
     - Returns current year's Open camp edition
     - Returns current year's Closed camp edition if no Open exists
     - Returns previous year's camp edition if current year has none
     - Returns 404 if no camp editions exist
     - Correctly calculates registrationCount and availableSpots

### Phase 2: Navigation and Admin Structure (Priority: HIGH)

1. **Update navigation menu**
   - File: [`frontend/src/components/layout/AppHeader.vue`](frontend/src/components/layout/AppHeader.vue)
   - **Remove** `adminBoardLinks` array and its usage
   - **Update** "Administración" button visibility from `auth.isAdmin` to `auth.isBoard`
   - Clean up removed references in desktop and mobile navigation
   - Test: Board users see "Administración", regular members do not

2. **Update router**
   - File: [`frontend/src/router/index.ts`](frontend/src/router/index.ts)
   - **Update** `/admin` route meta: change `requiresAdmin` to `requiresBoard`
   - **Add** new route `/family-unit/me` for user's family management
   - **Remove** `/users` route (functionality moved to admin panel)
   - **Remove** `/users/:id` route (functionality moved to admin panel)

3. **Restructure AdminPage.vue**
   - File: [`frontend/src/views/AdminPage.vue`](frontend/src/views/AdminPage.vue)
   - Replace with tabbed interface using PrimeVue TabView
   - Three tabs: Campamentos, Unidades Familiares, Usuarios
   - Each tab loads its respective admin panel component

4. **Create Admin Panel Components**
   - **File 1**: `frontend/src/components/admin/CampsAdminPanel.vue` (NEW)
     - List all camps and camp editions
     - DataTable with search, filter by year/status
     - Create, Edit, Delete actions
     - Link to existing CampLocationMap for location management

   - **File 2**: `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` (NEW)
     - List all family units with pagination
     - DataTable with search by name/representative
     - View/Edit family unit details
     - Show members count
     - Export functionality

   - **File 3**: `frontend/src/components/admin/UsersAdminPanel.vue` (NEW)
     - Migrate existing UsersPage component content
     - DataTable with users list
     - Search, filter by role
     - Edit user details and roles

### Phase 3: User Profile and Family Unit Management (Priority: HIGH)

1. **Update ProfilePage.vue**
   - File: [`frontend/src/views/ProfilePage.vue`](frontend/src/views/ProfilePage.vue)
   - Add "Mi Unidad Familiar" section
   - Show family unit status (exists or not)
   - Add button to navigate to family unit management
   - Use `useFamilyUnits` composable

2. **Create MyFamilyUnitPage.vue**
   - File: `frontend/src/views/family/MyFamilyUnitPage.vue` (NEW)
   - Dedicated page for user to manage their family unit
   - If no family unit: Show creation form
   - If has family unit: Show edit form and members list
   - Reuse existing family unit components where possible

3. **Create/Update composable `useFamilyUnits`**
   - File: `frontend/src/composables/useFamilyUnits.ts`
   - Add method: `fetchMyFamilyUnit()`
   - Add method: `fetchAllFamilyUnits()` (for admin)
   - Export reactive refs: `myFamilyUnit`, `familyUnits`, `loading`, `error`

### Phase 4: Current Camp Edition Display (Priority: HIGH)

1. **Create composable `useCampEditions`**
   - File: `frontend/src/composables/useCampEditions.ts` (NEW)
   - Implement `fetchCurrentCampEdition()` method
   - Handle 404 gracefully (no error, just null camp)
   - Export reactive refs: `currentCampEdition`, `loading`, `error`

2. **Update CampPage.vue**
   - File: [`frontend/src/views/CampPage.vue`](frontend/src/views/CampPage.vue)
   - Replace placeholder with dynamic content
   - Use `useCampEditions` composable
   - Display loading, error, and success states
   - Show warning if displaying previous year's camp
   - Add registration CTA if status is 'Open'

3. **Create CampEditionDetails component**
   - File: `frontend/src/components/camps/CampEditionDetails.vue` (NEW)
   - Display all camp edition information in a structured layout
   - Use PrimeVue Card components
   - Include map if coordinates available (use existing CampLocationMap)

### Phase 5: Polish and Testing (Priority: MEDIUM)

1. **Add E2E tests**
   - File: `frontend/cypress/e2e/camp-edition.cy.ts` (NEW)
   - Test scenarios:
     - User sees current camp edition on /camp page
     - User sees warning when viewing previous year's camp
     - Board user can access /camps page
     - Regular member cannot access /camps page
     - Page shows appropriate message when no camp edition exists

2. **Update unit tests**
   - File: `frontend/src/composables/__tests__/useCampEditions.test.ts` (NEW)
   - Test `fetchCurrentCampEdition()` method
   - Mock API responses for success and error cases

3. **Update documentation**
   - File: `ai-specs/specs/api-endpoints.md`
   - Document new `GET /api/camps/current` endpoint

## Files to Create/Modify

### Backend

**Create**:

- None (all endpoints added to existing files)

**Modify**:

- `backend/Controllers/CampsController.cs` - Add `GetCurrentCampEdition()` method
- `backend/Controllers/FamilyUnitsController.cs` - Verify/add paginated list endpoint for admin
- `backend.Tests/Controllers/CampsControllerTests.cs` - Add tests for new endpoint
- `backend.Tests/Controllers/FamilyUnitsControllerTests.cs` - Add tests for admin list endpoint

### Frontend

**Create**:

- `frontend/src/composables/useCampEditions.ts` - Composable for camp editions API
- `frontend/src/composables/useFamilyUnits.ts` - Enhanced composable for family units (if doesn't exist, or update existing)
- `frontend/src/components/camps/CampEditionDetails.vue` - Component to display camp edition info
- `frontend/src/components/admin/CampsAdminPanel.vue` - Admin panel for camps management
- `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` - Admin panel for family units management
- `frontend/src/components/admin/UsersAdminPanel.vue` - Admin panel for users management (migrate from UsersPage)
- `frontend/src/views/family/MyFamilyUnitPage.vue` - User's family unit management page
- `frontend/cypress/e2e/camp-edition.cy.ts` - E2E tests for camp edition
- `frontend/cypress/e2e/admin-panel.cy.ts` - E2E tests for admin panel tabs
- `frontend/cypress/e2e/family-unit-profile.cy.ts` - E2E tests for profile family section
- `frontend/src/composables/__tests__/useCampEditions.test.ts` - Unit tests for camp editions
- `frontend/src/composables/__tests__/useFamilyUnits.test.ts` - Unit tests for family units (if new)

**Modify**:

- [`frontend/src/components/layout/AppHeader.vue`](frontend/src/components/layout/AppHeader.vue) - Remove adminBoardLinks, update Admin button visibility
- [`frontend/src/router/index.ts`](frontend/src/router/index.ts) - Update /admin route, add /family-unit/me, remove /users routes
- [`frontend/src/views/AdminPage.vue`](frontend/src/views/AdminPage.vue) - Replace with tabbed interface
- [`frontend/src/views/ProfilePage.vue`](frontend/src/views/ProfilePage.vue) - Add family unit section
- [`frontend/src/views/CampPage.vue`](frontend/src/views/CampPage.vue) - Replace placeholder with dynamic content
- [`frontend/src/types/camp-edition.ts`](frontend/src/types/camp-edition.ts) - Ensure complete type definition

**Deprecate/Remove** (in future cleanup):

- `frontend/src/pages/UsersPage.vue` - Content migrated to UsersAdminPanel
- `frontend/src/pages/UserDetailPage.vue` - Functionality integrated into admin panel

### Documentation

**Modify**:

- `ai-specs/specs/api-endpoints.md` - Document `GET /api/camps/current`

## Acceptance Criteria

### Navigation Menu

- [ ] "Administración" button is visible to Board AND Admin users (not just Admin)
- [ ] "Administración" button is NOT visible to regular members
- [ ] Separate admin links (Usuarios, Campamentos) are removed from navigation
- [ ] Clicking "Administración" navigates to `/admin` page
- [ ] Non-board users trying to access `/admin` are redirected to `/home`
- [ ] Navigation is cleaner with consolidated admin access

### Admin Panel Structure

- [ ] Admin page shows tabbed interface with three tabs
- [ ] Tab 1: "Campamentos" - displays camps management panel
- [ ] Tab 2: "Unidades Familiares" - displays family units management panel
- [ ] Tab 3: "Usuarios" - displays users management panel
- [ ] Tabs are accessible via keyboard navigation
- [ ] Active tab state persists when navigating away and back
- [ ] Each tab panel loads its content independently

### Camps Admin Panel

- [ ] Shows DataTable with all camps and camp editions
- [ ] Supports search by name, year, location
- [ ] Supports filter by status (Draft, Open, Closed, Completed)
- [ ] Supports pagination (20 items per page by default)
- [ ] Shows: name, year, dates, status, capacity, registrations count
- [ ] Create button opens form for new camp edition
- [ ] Edit button opens form for selected camp
- [ ] Delete button with confirmation for selected camp
- [ ] Status change actions available for appropriate transitions

### Family Units Admin Panel

- [ ] Shows DataTable with all family units
- [ ] Supports search by family name or representative name
- [ ] Supports pagination (20 items per page by default)
- [ ] Shows: family name, representative, members count, created date
- [ ] View button shows family unit details with members list
- [ ] Edit button opens form for selected family unit
- [ ] Export button downloads CSV with family units data
- [ ] Only accessible to Board/Admin users

### Users Admin Panel

- [ ] Migrates existing users management functionality
- [ ] Shows DataTable with all users
- [ ] Supports search by name or email
- [ ] Supports filter by role (Admin, Board, Member)
- [ ] Shows: name, email, role, status, created date
- [ ] Edit button opens form to modify user details/roles
- [ ] Maintains all existing user management features

### User Profile - Family Unit Section

- [ ] Profile page shows "Mi Unidad Familiar" section
- [ ] If user has no family unit: Shows message and "Crear Unidad Familiar" button
- [ ] If user has family unit: Shows family name, members count, and "Gestionar" button
- [ ] Clicking "Crear" or "Gestionar" navigates to `/family-unit/me`
- [ ] Section shows loading state while fetching data
- [ ] Section handles errors gracefully with retry option

### My Family Unit Page

- [ ] Accessible at `/family-unit/me` for all authenticated users
- [ ] If no family unit exists: Shows creation form
- [ ] If family unit exists: Shows edit form and members list
- [ ] Users can create their family unit with name
- [ ] Users can add/edit/remove family members
- [ ] Users cannot delete their own representative member record
- [ ] Validation prevents duplicate members
- [ ] Save button updates family unit and shows success message
- [ ] Cancel button returns to profile or previous page

### Current Camp Edition Display

- [ ] `/camp` page shows loading spinner while fetching current camp edition
- [ ] If current year has an Open camp edition, display that edition's information
- [ ] If current year has no Open edition but has Closed/Completed, display that
- [ ] If no current year edition exists, display most recent previous year's edition with a warning message
- [ ] If no camp editions exist at all, show friendly message explaining the situation
- [ ] Page title dynamically shows "Campamento {year}" based on displayed edition
- [ ] Camp edition details include: name, dates, location, description, pricing, age limits, capacity
- [ ] If edition status is 'Open', show registration CTA with available spots count
- [ ] If displaying previous year's camp, show warning: "Mostrando información del campamento de {year}"

### API Behavior

- [ ] `GET /api/camps/current` returns current year's camp edition if exists (status Open prioritized)
- [ ] If no current year edition, returns most recent previous year edition
- [ ] Returns 404 if no camp editions exist
- [ ] Response includes related Camp data (coordinates, name)
- [ ] Response includes computed fields: `registrationCount`, `availableSpots`
- [ ] Endpoint is accessible to all authenticated users (not just Board)

### Error Handling

- [ ] Network errors show user-friendly error message
- [ ] 404 responses (no camp edition) handled gracefully with informative message
- [ ] No console errors in production build

## Non-Functional Requirements

### Performance

- API response time < 500ms for `GET /api/camps/current`
- Frontend page load < 2 seconds on 3G network
- No unnecessary re-fetches of camp edition data

### Security

- Only Board/Admin users can access `/admin` page and all its tabs
- All authenticated users can access `/family-unit/me` (their own family unit)
- Board/Admin can view all family units via admin panel
- Regular users cannot access other users' family units
- All users can view current camp edition at `/camp`
- No sensitive data (medical notes, allergies) exposed in API responses
- Family unit member details only visible to representative and Board/Admin

### Accessibility

- All interactive elements have proper ARIA labels
- Keyboard navigation works for all menu items
- Screen reader announces dynamic page title changes
- Color contrast meets WCAG AA standards for warning messages

### Testing

- Backend: Unit tests for current camp edition logic
- Frontend: E2E test covering full user flow
- Frontend: Unit tests for `useCampEditions` composable
- Test coverage > 90% for new code

## Questions for Clarification

1. **User Preference for Selected Camp**: Should users be able to "select" a preferred camp edition to view, or should it always show the current/latest automatically?
   - **Recommendation**: Start with automatic (current/latest), add user selection in a future iteration if needed

2. **Dynamic Menu Label**: Should the "Campamento" menu item show the current year dynamically (e.g., "Campamento 2026")?
   - **Recommendation**: Keep it simple as "Campamento" for now, since the year is shown on the page itself

3. **Multiple Camps per Year**: If there are multiple camp editions in the same year (e.g., Mountain Camp 2026, Beach Camp 2026), which one should be considered "current"?
   - **Proposed Logic**: Prioritize by status (Open > Closed > Draft), then by creation date (newest first)

4. **Archive Threshold**: When should camps be considered "archived" and not shown as fallback?
   - **Recommendation**: Don't show camps older than 2 years as fallback (only current year and previous year)

5. **Admin Panel Tab Order**: What should be the default tab when opening admin panel?
   - **Recommendation**: Default to "Campamentos" tab (index 0) as it's likely the most frequently used by Board members

6. **Family Unit in Profile**: Should the family unit section be prominent (top of profile) or below personal info?
   - **Recommendation**: Place below personal information but above additional settings, as it's important but secondary to user's own details

7. **Users Page Migration**: Should we keep the old `/users` route for backward compatibility or remove it entirely?
   - **Recommendation**: Keep as redirect to `/admin` for a transition period, then remove in next major version

## Related Documentation

- [Camp CRUD User Stories](../feat-camps-definition/camp-crud-user-stories-updated.md) - Context on Camp vs CampEdition data model
- [Data Model Documentation](../../specs/data-model.md) - Full entity definitions
- [API Endpoints Documentation](../../specs/api-endpoints.md) - Existing API patterns
- [Frontend Standards](../../specs/frontend-standards.mdc) - Component and composable patterns

## Summary of Changes from Original Requirements

### Original Requirements
1. Add "Campamentos" menu for Board users (separate from "Campamento 2026")
2. Update "Campamento 2026" page to show current or recent camp edition
3. Determine logic for "current" or "selected" camp

### Expanded Requirements Added
4. **Centralized Admin Panel**: Instead of multiple admin menu items, consolidate all admin functions under single "Administración" entry
5. **Family Unit Management in Profile**: Users can access their family unit management from their profile page
6. **Admin Panel for Family Units**: Board/Admin can manage all family units from admin panel
7. **Unified Admin Interface**: Tabbed admin page with three sections:
   - Campamentos (camps and editions management)
   - Unidades Familiares (family units management)
   - Usuarios (users management - migrated from separate page)

### Key Architectural Decisions

1. **Navigation Simplification**:
   - REMOVED: Separate "Usuarios" and "Campamentos" menu items
   - UPDATED: Single "Administración" button for Board/Admin
   - RESULT: Cleaner navigation, better UX

2. **Admin Panel Structure**:
   - Tabbed interface using PrimeVue TabView
   - Each tab is an independent admin panel component
   - Reuses existing components where possible (e.g., users management)

3. **Family Unit Access**:
   - Regular users: Access via Profile → "Mi Unidad Familiar" → `/family-unit/me`
   - Board/Admin: Access via Admin Panel → "Unidades Familiares" tab
   - Clear separation between self-management and administrative oversight

4. **Route Deprecation**:
   - `/users` route deprecated (moved to admin panel)
   - Old users pages can be removed after migration
   - Backward compatibility with redirect can be added if needed

## Notes

- This feature builds on the existing Camp and CampEdition data model
- The admin panel centralizes all administrative functions for better UX and maintenance
- The logic for determining "current" camp is implemented server-side to ensure consistency
- The design prioritizes showing relevant information to users rather than forcing them to navigate complex menus
- Family unit management is accessible from multiple entry points based on user role and context

## Definition of Done

### Backend
- [ ] `GET /api/camps/current` endpoint implemented and tested
- [ ] `GET /api/family-units` paginated endpoint verified/implemented for admin
- [ ] All backend unit tests pass with >90% coverage
- [ ] API documentation updated in api-endpoints.md
- [ ] No breaking changes to existing endpoints

### Frontend - Navigation & Admin
- [ ] Navigation menu updated (adminBoardLinks removed, Admin button updated)
- [ ] Admin page restructured with tabbed interface
- [ ] All three admin panels implemented (Camps, Family Units, Users)
- [ ] Router updated with new routes and deprecated old ones
- [ ] Admin panel accessible only to Board/Admin users
- [ ] Tab navigation works correctly with keyboard

### Frontend - Profile & Family Management
- [ ] Profile page updated with family unit section
- [ ] MyFamilyUnitPage created and functional
- [ ] Users can create/edit their family unit from profile
- [ ] Family unit composable updated with new methods
- [ ] Family unit management respects permissions

### Frontend - Current Camp Display
- [ ] CampPage.vue updated with dynamic content
- [ ] CampEditionDetails component created
- [ ] useCampEditions composable created
- [ ] Loading, error, and success states handled
- [ ] Warning shown when displaying previous year's camp
- [ ] Registration CTA shown when status is 'Open'

### Testing
- [ ] E2E tests pass for all new features:
  - [ ] Admin panel navigation and tabs
  - [ ] Family unit section in profile
  - [ ] Current camp edition display
  - [ ] Family unit management page
- [ ] Unit tests pass with >90% coverage
- [ ] No console errors in production build
- [ ] No accessibility violations (WCAG AA)

### Quality & Deployment
- [ ] Code reviewed and approved
- [ ] Tested on Chrome, Firefox, Safari
- [ ] Tested on mobile devices (responsive)
- [ ] Performance benchmarks met (< 500ms API, < 2s page load)
- [ ] Deployed to staging environment
- [ ] User acceptance testing completed
- [ ] Migration plan documented for deprecated routes

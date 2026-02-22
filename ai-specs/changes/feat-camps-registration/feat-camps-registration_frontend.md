# Frontend Implementation Plan: feat-camps-registration Camp Registration Flow

## Overview

This plan covers the frontend implementation of the camp registration workflow for ABUVI, allowing family representatives to register their family unit for an open camp edition, select extras, and track their registration status.

**Frontend stack**: Vue 3 with `<script setup lang="ts">`, PrimeVue components, Tailwind CSS, Pinia for auth state, composable-based API communication, Vitest unit tests + Cypress E2E tests.

---

## Architecture Context

### Components/Composables Involved

**New files to create:**

```
frontend/src/types/registration.ts
frontend/src/composables/useRegistrations.ts
frontend/src/components/registrations/
    RegistrationStatusBadge.vue
    RegistrationCard.vue
    RegistrationMemberSelector.vue
    RegistrationExtrasSelector.vue
    RegistrationPricingBreakdown.vue
    RegistrationCancelDialog.vue
    __tests__/
        RegistrationCard.test.ts
        RegistrationMemberSelector.test.ts
        RegistrationExtrasSelector.test.ts
        RegistrationPricingBreakdown.test.ts
frontend/src/views/registrations/
    RegistrationsPage.vue
    RegistrationDetailPage.vue
    RegisterForCampPage.vue
frontend/src/composables/__tests__/useRegistrations.test.ts
frontend/cypress/e2e/registrations.cy.ts
```

**Existing files to modify:**

```
frontend/src/router/index.ts        ← Add 3 new routes
frontend/src/views/CampPage.vue     ← Add registration entry point
```

### Routing Considerations

Three new routes under `requiresAuth: true`:

- `/registrations` — My registrations list (Member)
- `/registrations/:id` — Registration detail + cancel
- `/registrations/new/:editionId` — Multi-step registration wizard

No `requiresBoard` guard needed — all authenticated members can access their own registrations. Representative check is enforced at the service/API level.

### State Management Approach

- **Auth store** (existing `useAuthStore`): used to get `auth.user?.id` for representative check
- **Composable-local state** for all registration data (`useRegistrations`) — no Pinia store needed since registrations are user-scoped and not shared across views
- **Wizard state** is local `reactive()` in `RegisterForCampPage.vue` — step index, selected members, selected extras, notes

### Key Dependencies on Existing Composables

- `useCampEditions()` — the wizard uses `getEditionById(editionId)` to get edition pricing/dates
- `useCampExtras(editionId)` — Step 2 of wizard loads extras for the given edition
- `useFamilyUnits()` — Step 1 loads current user's family unit and members for selection

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to frontend-specific branch
- **Branch Name**: `feature/feat-camps-registration-frontend`
- **Implementation Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-camps-registration-frontend`
  3. `git branch` — verify branch exists

---

### Step 1: Define TypeScript Interfaces

- **File**: `frontend/src/types/registration.ts`
- **Action**: Create all registration-related TypeScript interfaces mirroring backend DTOs exactly
- **Content**:

```typescript
// Registration status and related enums
export type RegistrationStatus = 'Pending' | 'Confirmed' | 'Cancelled'
export type AgeCategory = 'Baby' | 'Child' | 'Adult'
export type PaymentMethod = 'Card' | 'Transfer' | 'Cash'
export type PaymentStatus = 'Pending' | 'Completed' | 'Failed' | 'Refunded'

// Embedded summaries in RegistrationResponse
export interface RegistrationFamilyUnitSummary {
  id: string
  name: string
  representativeUserId: string
}

export interface RegistrationCampEditionSummary {
  id: string
  campName: string
  year: number
  startDate: string
  endDate: string
  location: string | null
}

// Pricing breakdown (returned in RegistrationResponse)
export interface MemberPricingDetail {
  familyMemberId: string
  fullName: string
  ageAtCamp: number
  ageCategory: AgeCategory
  individualAmount: number
}

export interface ExtraPricingDetail {
  campEditionExtraId: string
  name: string
  unitPrice: number
  pricingType: 'PerPerson' | 'PerFamily'
  pricingPeriod: 'OneTime' | 'PerDay'
  quantity: number
  campDurationDays: number | null
  calculation: string
  totalAmount: number
}

export interface PricingBreakdown {
  members: MemberPricingDetail[]
  baseTotalAmount: number
  extras: ExtraPricingDetail[]
  extrasAmount: number
  totalAmount: number
}

export interface PaymentSummary {
  id: string
  amount: number
  paymentDate: string
  method: PaymentMethod
  status: PaymentStatus
}

// Main registration response (used for list and detail)
export interface RegistrationResponse {
  id: string
  familyUnit: RegistrationFamilyUnitSummary
  campEdition: RegistrationCampEditionSummary
  status: RegistrationStatus
  notes: string | null
  pricing: PricingBreakdown
  payments: PaymentSummary[]
  amountPaid: number
  amountRemaining: number
  createdAt: string
  updatedAt: string
}

// Available camp edition for registration wizard
export interface AgeRangesInfo {
  babyMaxAge: number
  childMinAge: number
  childMaxAge: number
  adultMinAge: number
}

export interface AvailableCampEditionResponse {
  id: string
  campName: string
  year: number
  startDate: string
  endDate: string
  location: string | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  maxCapacity: number | null
  currentRegistrations: number
  spotsRemaining: number | null
  status: string
  ageRanges: AgeRangesInfo
}

// Request types
export interface CreateRegistrationRequest {
  campEditionId: string
  familyUnitId: string
  memberIds: string[]
  notes?: string | null
}

export interface UpdateRegistrationMembersRequest {
  memberIds: string[]
}

export interface ExtraSelectionRequest {
  campEditionExtraId: string
  quantity: number
}

export interface UpdateRegistrationExtrasRequest {
  extras: ExtraSelectionRequest[]
}

// Wizard-local state (not sent to API)
export interface WizardExtrasSelection {
  campEditionExtraId: string
  name: string
  quantity: number
  unitPrice: number
}
```

- **Notes**:
  - `PricingBreakdown`, `MemberPricingDetail`, `ExtraPricingDetail` mirror backend response exactly — no client-side calculation needed
  - `WizardExtrasSelection` is a local type for the wizard UI state, not an API type
  - Never expose actual medical notes or allergy content — `MemberPricingDetail` only has `ageAtCamp` and `ageCategory` from the backend

---

### Step 2: Write Unit Tests for `useRegistrations` (TDD — Red Phase)

- **File**: `frontend/src/composables/__tests__/useRegistrations.test.ts`
- **Action**: Write failing tests FIRST before implementing the composable
- **Content**:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useRegistrations } from '@/composables/useRegistrations'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('useRegistrations', () => {
  beforeEach(() => vi.clearAllMocks())

  describe('fetchMyRegistrations', () => {
    it('should load registrations successfully', async () => {
      // Arrange: mock API response with registration list
      // Act: call fetchMyRegistrations()
      // Assert: registrations.value is populated, loading is false, error is null
    })

    it('should set error message when fetch fails', async () => {
      // Arrange: mock API to throw
      // Act: call fetchMyRegistrations()
      // Assert: error.value is set, registrations.value is empty
    })
  })

  describe('getRegistrationById', () => {
    it('should return registration with full pricing breakdown', async () => {
      // Arrange
      // Act: call getRegistrationById(id)
      // Assert: returns RegistrationResponse with pricing.members populated
    })

    it('should return null when registration not found', async () => {
      // Arrange: mock 404 response
      // Act
      // Assert: returns null, error.value is set
    })
  })

  describe('createRegistration', () => {
    it('should create registration and return response', async () => {
      // Arrange: valid CreateRegistrationRequest
      // Act: call createRegistration(request)
      // Assert: returns RegistrationResponse, adds to registrations list
    })

    it('should set error and return null when API returns error', async () => {
      // Arrange: mock API to throw with error code CAMP_FULL
      // Act
      // Assert: error.value contains API error message, returns null
    })

    it('should set error and return null when edition is not open', async () => {
      // Arrange: mock 409 with EDITION_NOT_OPEN code
      // Act
      // Assert: error.value set, returns null
    })
  })

  describe('setExtras', () => {
    it('should set extras and return updated registration', async () => {
      // Arrange: valid extras request
      // Act: call setExtras(id, request)
      // Assert: returns updated RegistrationResponse with extras pricing
    })

    it('should set error when extra does not belong to edition', async () => {
      // Arrange: mock 422 with EXTRA_NOT_IN_EDITION
      // Act
      // Assert: error.value set, returns null
    })
  })

  describe('cancelRegistration', () => {
    it('should return true when cancellation succeeds', async () => {
      // Arrange: mock 204 response
      // Act: call cancelRegistration(id)
      // Assert: returns true, updates registration status to Cancelled
    })

    it('should return false and set error when registration not cancellable', async () => {
      // Arrange: mock 422 REGISTRATION_NOT_EDITABLE
      // Act
      // Assert: returns false, error.value set
    })
  })
})
```

- **Notes**: These tests must fail (Red phase) before Step 3. Use `vi.mocked(api.get).mockResolvedValue(...)` pattern following existing `useCampEditions` tests.

---

### Step 3: Implement `useRegistrations` Composable (TDD — Green Phase)

- **File**: `frontend/src/composables/useRegistrations.ts`
- **Action**: Implement to make tests pass
- **Function Signature**:

```typescript
export function useRegistrations() {
  const registrations = ref<RegistrationResponse[]>([])
  const registration = ref<RegistrationResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchMyRegistrations = async (): Promise<void>
  const getRegistrationById = async (id: string): Promise<RegistrationResponse | null>
  const createRegistration = async (request: CreateRegistrationRequest): Promise<RegistrationResponse | null>
  const updateMembers = async (id: string, request: UpdateRegistrationMembersRequest): Promise<RegistrationResponse | null>
  const setExtras = async (id: string, request: UpdateRegistrationExtrasRequest): Promise<RegistrationResponse | null>
  const cancelRegistration = async (id: string): Promise<boolean>

  return { registrations, registration, loading, error, ... }
}
```

- **API Endpoints Mapped**:

  | Method | Description |
  |--------|-------------|
  | `GET /api/registrations` | `fetchMyRegistrations()` |
  | `GET /api/registrations/{id}` | `getRegistrationById(id)` |
  | `POST /api/registrations` | `createRegistration(request)` |
  | `PUT /api/registrations/{id}/members` | `updateMembers(id, request)` |
  | `POST /api/registrations/{id}/extras` | `setExtras(id, request)` |
  | `POST /api/registrations/{id}/cancel` | `cancelRegistration(id)` |

- **Error Handling Pattern** (follow existing `useCampEditions` pattern exactly):

```typescript
const fetchMyRegistrations = async (): Promise<void> => {
  loading.value = true
  error.value = null
  try {
    const response = await api.get<ApiResponse<RegistrationResponse[]>>('/registrations')
    registrations.value = response.data.success ? (response.data.data ?? []) : []
  } catch (err: unknown) {
    error.value = (err as { response?: { data?: { error?: { message?: string } } } })
      ?.response?.data?.error?.message || 'Error al cargar inscripciones'
    console.error('Failed to fetch registrations:', err)
    registrations.value = []
  } finally {
    loading.value = false
  }
}
```

- **`cancelRegistration` specifics**:
  - Backend returns 204 No Content on success
  - On success, update the `registration.value.status` to `'Cancelled'` locally (don't re-fetch)
  - Return `true` on success, `false` on error

- **`createRegistration` specifics**:
  - On success, push result to `registrations.value` list
  - Return the full `RegistrationResponse`

- **`setExtras` specifics**:
  - On success, update the matching registration in `registrations.value` list (by id)
  - Update `registration.value` if it matches the id

- **Implementation Notes**: Run tests after implementation to confirm Green phase.

---

### Step 4: Create Shared Registration Components

#### 4a. RegistrationStatusBadge.vue

- **File**: `frontend/src/components/registrations/RegistrationStatusBadge.vue`
- **Action**: Simple badge component for registration status
- **Props**: `status: RegistrationStatus`
- **Template pattern** (follow `CampEditionStatusBadge.vue`):
  - `Pending` → yellow badge, label "Pendiente"
  - `Confirmed` → green badge, label "Confirmada"
  - `Cancelled` → red/gray badge, label "Cancelada"
- **No test needed** — purely presentational, covered by parent component tests

#### 4b. RegistrationPricingBreakdown.vue (with tests)

- **File**: `frontend/src/components/registrations/RegistrationPricingBreakdown.vue`
- **Action**: Display the server-calculated pricing breakdown
- **Props**: `pricing: PricingBreakdown`
- **Template structure**:
  - Table or list of members: fullName, ageAtCamp, ageCategory (translated to Spanish: Bebé/Niño/Adulto), individualAmount (formatted as €)
  - Base total row
  - Extras list: name, calculation description, totalAmount
  - Extras subtotal row
  - **Total amount** prominently displayed in bold
- **Currency formatting**: `new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }).format(amount)`
- **AgeCategory labels** (inline util in this component):

  ```typescript
  const AGE_CATEGORY_LABELS: Record<AgeCategory, string> = {
    Baby: 'Bebé', Child: 'Niño/Niña', Adult: 'Adulto/Adulta'
  }
  ```

- **Test file**: `__tests__/RegistrationPricingBreakdown.test.ts`
  - `should render all member rows with correct age categories`
  - `should show extras section only when extras exist`
  - `should display total amount correctly formatted`
  - `should show zero extras amount when no extras selected`

#### 4c. RegistrationCard.vue (with tests)

- **File**: `frontend/src/components/registrations/RegistrationCard.vue`
- **Props**: `registration: RegistrationResponse`
- **Emits**: `{ view: [id: string] }`
- **Template**: Card showing camp name + year, dates, status badge, total amount, "Ver detalles" button
- **Test file**: `__tests__/RegistrationCard.test.ts`
  - `should render camp name and year`
  - `should display status badge`
  - `should emit view event with registration id on button click`

#### 4d. RegistrationMemberSelector.vue (with tests)

- **File**: `frontend/src/components/registrations/RegistrationMemberSelector.vue`
- **Props**: `members: FamilyMemberResponse[], modelValue: string[]` (selected member IDs)
- **Emits**: `{ 'update:modelValue': [ids: string[]] }`
- **Template**:
  - List of checkboxes (PrimeVue `Checkbox`) for each family member
  - Each item shows: full name, dateOfBirth formatted, relationship label (use `FamilyRelationshipLabels` from `types/family-unit.ts`)
  - Medical/allergy icons as badges (warning icon) if `hasMedicalNotes` or `hasAllergies` is true — tooltip says "Tiene notas médicas" / "Tiene alergias"
  - **NEVER show actual medical notes or allergy content** — only the flag icon
- **Validation**: At least 1 member must be selected (parent component validates)
- **Test file**: `__tests__/RegistrationMemberSelector.test.ts`
  - `should render all family members as checkboxes`
  - `should emit update:modelValue when checkbox is toggled`
  - `should show medical notes warning icon when hasMedicalNotes is true`
  - `should NOT expose actual medical note content in any rendered text`

#### 4e. RegistrationExtrasSelector.vue (with tests)

- **File**: `frontend/src/components/registrations/RegistrationExtrasSelector.vue`
- **Props**: `extras: CampEditionExtra[], modelValue: WizardExtrasSelection[]`
- **Emits**: `{ 'update:modelValue': [selections: WizardExtrasSelection[]] }`
- **Template**:
  - For each active extra (`isActive === true`): name, description, price, pricingType label, pricingPeriod label
  - PrimeVue `InputNumber` (min: 0, max: `extra.maxQuantity ?? 99`) for quantity — quantity 0 means not selected
  - Required extras (`isRequired === true`): show lock icon, quantity cannot be 0, pre-fill with 1
  - Price display: "X €/persona/día", "X €/familia", etc. based on `pricingType` and `pricingPeriod`
- **PricingType/Period labels**:

  ```typescript
  const PRICING_TYPE_LABELS = { PerPerson: 'por persona', PerFamily: 'por familia' }
  const PRICING_PERIOD_LABELS = { OneTime: '', PerDay: '/día' }
  ```

- **Test file**: `__tests__/RegistrationExtrasSelector.test.ts`
  - `should render all active extras`
  - `should not render inactive extras`
  - `should pre-fill required extras with quantity 1`
  - `should emit updated selections when quantity changes`

#### 4f. RegistrationCancelDialog.vue

- **File**: `frontend/src/components/registrations/RegistrationCancelDialog.vue`
- **Props**: `visible: boolean, registrationId: string, loading: boolean`
- **Emits**: `{ 'update:visible': [value: boolean], confirm: [] }`
- **Template**: PrimeVue `Dialog` with confirmation message "¿Seguro que quieres cancelar esta inscripción? Esta acción no se puede deshacer." — Cancel and Confirm buttons
- **No test needed** — pure UI dialog, covered by integration/Cypress

---

### Step 5: Create Views

#### 5a. RegisterForCampPage.vue — Multi-Step Registration Wizard

- **File**: `frontend/src/views/registrations/RegisterForCampPage.vue`
- **Route**: `/registrations/new/:editionId`
- **Architecture**: Full-page wizard (not a dialog) for better UX and mobile support

**Script setup**:

```typescript
const route = useRoute()
const router = useRouter()
const toast = useToast()
const auth = useAuthStore()

// Route param
const editionId = computed(() => route.params.editionId as string)

// Composables
const { getEditionById } = useCampEditions()
const { familyUnit, familyMembers, getCurrentUserFamilyUnit, fetchFamilyMembersForUnit } = useFamilyUnits()
const { extras: campExtras, fetchExtras } = useCampExtras(editionId.value)
const { createRegistration, setExtras, loading, error } = useRegistrations()

// Wizard state
const currentStep = ref(0)
const selectedMemberIds = ref<string[]>([])
const extrasSelections = ref<WizardExtrasSelection[]>([])
const notes = ref<string>('')
const edition = ref<CampEdition | null>(null)

// Guard: only family representatives can register
const isRepresentative = computed(() =>
  familyUnit.value?.representativeUserId === auth.user?.id
)
```

**Steps** (using PrimeVue `Steps` component):

```typescript
const steps = [
  { label: 'Participantes' },
  { label: 'Extras' },
  { label: 'Confirmar' }
]
```

**Step 0 — Member Selection**:

- Show `RegistrationMemberSelector` with `familyMembers`
- If not representative: show Message `severity="warn"` with "Solo el representante de la unidad familiar puede inscribirse. Si quieres registrar a tu familia, contacta con el representante."
- "Siguiente" button: disabled until `selectedMemberIds.length > 0`

**Step 1 — Extras Selection** (optional):

- Show `RegistrationExtrasSelector` with `campExtras` (only active extras)
- "Saltar este paso" link-style button to proceed with no extras
- "Siguiente" button always enabled

**Step 2 — Review & Confirm**:

- List selected members (names from `familyMembers` filtered by `selectedMemberIds`)
- Show pricing guide table from edition data: "Adulto: X€, Niño: X€, Bebé: X€"
- Show note: "El precio final se calculará al confirmar según las categorías de edad de cada persona"
- Show selected extras summary (name + quantity)
- `Textarea` for optional notes (PrimeVue `Textarea`, `:rows="3"`, max 1000 chars)
- "Confirmar inscripción" button (disabled while `loading`)

**On confirm — two-step API flow**:

```typescript
const handleConfirm = async () => {
  // Step 1: Create registration with members
  const created = await createRegistration({
    campEditionId: editionId.value,
    familyUnitId: familyUnit.value!.id,
    memberIds: selectedMemberIds.value,
    notes: notes.value || null
  })
  if (!created) {
    // error.value already set, show error toast
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
    return
  }

  // Step 2: Set extras if any selected
  const hasExtras = extrasSelections.value.some(e => e.quantity > 0)
  if (hasExtras) {
    const extrasResult = await setExtras(created.id, {
      extras: extrasSelections.value
        .filter(e => e.quantity > 0)
        .map(e => ({ campEditionExtraId: e.campEditionExtraId, quantity: e.quantity }))
    })
    if (!extrasResult) {
      // Registration created but extras failed — navigate to detail with warning
      toast.add({ severity: 'warn', summary: 'Inscripción creada', detail: 'No se pudieron guardar los extras. Puedes editarlos desde el detalle.', life: 6000 })
      router.push({ name: 'registration-detail', params: { id: created.id } })
      return
    }
  }

  toast.add({ severity: 'success', summary: '¡Inscripción realizada!', detail: 'Tu inscripción ha sido creada correctamente.', life: 4000 })
  router.push({ name: 'registration-detail', params: { id: created.id } })
}
```

**Guard on mount**:

```typescript
onMounted(async () => {
  edition.value = await getEditionById(editionId.value)
  if (!edition.value || edition.value.status !== 'Open') {
    toast.add({ severity: 'warn', summary: 'No disponible', detail: 'Esta edición no está abierta para inscripciones.', life: 4000 })
    router.push({ name: 'camp' })
    return
  }
  await getCurrentUserFamilyUnit()
  if (familyUnit.value) {
    await fetchFamilyMembersForUnit(familyUnit.value.id)
  }
  await fetchExtras()
})
```

#### 5b. RegistrationDetailPage.vue

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Route**: `/registrations/:id`

**Script setup**:

```typescript
const route = useRoute()
const toast = useToast()
const auth = useAuthStore()
const { registration, loading, error, getRegistrationById, cancelRegistration } = useRegistrations()
const showCancelDialog = ref(false)
const cancelling = ref(false)

const registrationId = computed(() => route.params.id as string)
const isRepresentative = computed(() =>
  registration.value?.familyUnit.representativeUserId === auth.user?.id
)
const canCancel = computed(() =>
  registration.value?.status === 'Pending' || registration.value?.status === 'Confirmed'
)

onMounted(() => getRegistrationById(registrationId.value))

const handleCancel = async () => {
  cancelling.value = true
  const success = await cancelRegistration(registrationId.value)
  cancelling.value = false
  showCancelDialog.value = false
  if (success) {
    toast.add({ severity: 'info', summary: 'Inscripción cancelada', detail: 'Tu inscripción ha sido cancelada.', life: 4000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}
```

**Template sections**:

1. Loading/error states (ProgressSpinner / Message)
2. Header: camp name + year, status badge, back button to `/registrations`
3. Camp edition info: dates, location
4. `RegistrationPricingBreakdown` component with `registration.pricing`
5. Payments section: list of payments (if any), amountPaid, amountRemaining
6. Cancel button (only if `isRepresentative && canCancel`): opens `RegistrationCancelDialog`
7. `RegistrationCancelDialog` component

#### 5c. RegistrationsPage.vue

- **File**: `frontend/src/views/registrations/RegistrationsPage.vue`
- **Route**: `/registrations`

**Script setup**:

```typescript
const router = useRouter()
const { registrations, loading, error, fetchMyRegistrations } = useRegistrations()

onMounted(() => fetchMyRegistrations())
```

**Template**:

- Title "Mis Inscripciones"
- "Inscribirse en un campamento" Button linking to `/camp` (active edition entry)
- Loading state: ProgressSpinner
- Error state: Message severity="error"
- Empty state: "Todavía no tienes inscripciones. Cuando haya un campamento abierto, podrás inscribirte desde la página del campamento."
- List: `RegistrationCard` for each registration, @view navigates to `/registrations/:id`
- Sorted: active first (Pending/Confirmed), then Cancelled

---

### Step 6: Update Router

- **File**: `frontend/src/router/index.ts`
- **Action**: Add 3 new routes after the existing `/camp` route

```typescript
// Registration routes — authenticated members
{
  path: '/registrations',
  name: 'registrations',
  component: () => import('@/views/registrations/RegistrationsPage.vue'),
  meta: {
    requiresAuth: true,
    title: 'ABUVI | Mis Inscripciones'
  }
},
{
  path: '/registrations/new/:editionId',
  name: 'registration-new',
  component: () => import('@/views/registrations/RegisterForCampPage.vue'),
  meta: {
    requiresAuth: true,
    title: 'ABUVI | Nueva Inscripción'
  }
},
{
  path: '/registrations/:id',
  name: 'registration-detail',
  component: () => import('@/views/registrations/RegistrationDetailPage.vue'),
  meta: {
    requiresAuth: true,
    title: 'ABUVI | Detalle de Inscripción'
  }
},
```

- **Important**: `/registrations/new/:editionId` must be declared BEFORE `/registrations/:id` to avoid route conflicts. Vue Router matches routes in order.

---

### Step 7: Update CampPage.vue

- **File**: `frontend/src/views/CampPage.vue`
- **Action**: Add registration entry point when active edition is Open

**Add to script setup**:

```typescript
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useFamilyUnits } from '@/composables/useFamilyUnits'

const router = useRouter()
const auth = useAuthStore()
const { familyUnit, getCurrentUserFamilyUnit } = useFamilyUnits()

const isRepresentative = computed(() =>
  !!familyUnit.value && familyUnit.value.representativeUserId === auth.user?.id
)

onMounted(() => {
  getActiveEdition()
  getCurrentUserFamilyUnit()
})

const goToRegister = () => {
  if (activeEdition.value) {
    router.push({ name: 'registration-new', params: { editionId: activeEdition.value.id } })
  }
}
```

**Add to template** (inside `v-else-if="activeEdition"` block, below `<ActiveEditionCard>`):

```html
<!-- Registration action section -->
<div class="mt-6 flex flex-col items-start gap-3 sm:flex-row sm:items-center">
  <Button
    v-if="activeEdition.status === 'Open' && isRepresentative"
    label="Inscribirse al campamento"
    icon="pi pi-user-plus"
    size="large"
    @click="goToRegister"
    data-testid="register-button"
  />
  <Button
    v-else-if="activeEdition.status === 'Open' && !isRepresentative"
    label="Ver campamento"
    icon="pi pi-info-circle"
    severity="secondary"
    size="large"
    disabled
  />
  <RouterLink
    :to="{ name: 'registrations' }"
    class="text-sm text-blue-600 underline hover:text-blue-800"
  >
    Ver mis inscripciones
  </RouterLink>
</div>

<!-- Non-representative note -->
<p
  v-if="activeEdition.status === 'Open' && !isRepresentative && familyUnit"
  class="mt-2 text-sm text-amber-600"
>
  Solo el representante de la unidad familiar puede inscribir a la familia.
</p>
```

---

### Step 8: Write Cypress E2E Tests

- **File**: `frontend/cypress/e2e/registrations.cy.ts`
- **Action**: Cover critical user flows

```typescript
describe('Camp Registration Flow', () => {
  describe('As family representative', () => {
    beforeEach(() => {
      cy.login('representative@abuvi.org', 'password123')
    })

    it('should show register button on camp page when edition is open', () => {
      cy.visit('/camp')
      cy.get('[data-testid="register-button"]').should('be.visible')
    })

    it('should complete full registration wizard — members only', () => {
      cy.visit('/camp')
      cy.get('[data-testid="register-button"]').click()
      cy.url().should('include', '/registrations/new/')

      // Step 0: select members
      cy.get('[data-testid="member-checkbox"]').first().click()
      cy.get('[data-testid="next-step-btn"]').click()

      // Step 1: skip extras
      cy.get('[data-testid="skip-extras-btn"]').click()

      // Step 2: confirm
      cy.get('[data-testid="confirm-registration-btn"]').click()

      // Should redirect to detail page
      cy.url().should('include', '/registrations/')
      cy.url().should('not.include', '/new/')
      cy.get('[data-testid="registration-status"]').should('contain', 'Pendiente')
    })

    it('should complete registration with extras', () => {
      // Fill out full wizard including extra selection
      // Assert pricing breakdown visible on detail page
    })

    it('should cancel a pending registration', () => {
      cy.visit('/registrations')
      cy.get('[data-testid="registration-card"]').first().click()
      cy.get('[data-testid="cancel-registration-btn"]').click()
      cy.get('[data-testid="cancel-confirm-btn"]').click()
      cy.get('[data-testid="registration-status"]').should('contain', 'Cancelada')
    })

    it('should show my registrations list', () => {
      cy.visit('/registrations')
      cy.get('[data-testid="registration-card"]').should('have.length.greaterThan', 0)
    })
  })

  describe('As non-representative member', () => {
    beforeEach(() => {
      cy.login('member@abuvi.org', 'password123')
    })

    it('should not show register button on camp page', () => {
      cy.visit('/camp')
      cy.get('[data-testid="register-button"]').should('not.exist')
    })

    it('should redirect away from wizard if not representative', () => {
      // Direct URL navigation should redirect back to /camp
    })
  })
})
```

---

### Step 9: Update Technical Documentation

- **Action**: Review changes and update documentation
- **Implementation Steps**:
  1. **Review** all new files and routes created
  2. **Update** `ai-specs/specs/frontend-standards.mdc` — add `registration.ts` types to the Project Structure section, add `useRegistrations.ts` to composables listing
  3. **Verify** no API spec changes needed (all endpoints already documented in backend enriched spec)
- **Notes**: All documentation must be in English

---

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Define TypeScript interfaces in `types/registration.ts`
3. **Step 2**: Write failing unit tests for `useRegistrations` (RED phase)
4. **Step 3**: Implement `useRegistrations` composable (GREEN phase) — run tests to confirm
5. **Step 4a–4b**: `RegistrationStatusBadge.vue` and `RegistrationPricingBreakdown.vue` (with tests)
6. **Step 4c**: `RegistrationCard.vue` (with tests)
7. **Step 4d**: `RegistrationMemberSelector.vue` (with tests) — security-sensitive, tests required
8. **Step 4e**: `RegistrationExtrasSelector.vue` (with tests)
9. **Step 4f**: `RegistrationCancelDialog.vue`
10. **Step 5a**: `RegisterForCampPage.vue` (wizard) — most complex view
11. **Step 5b**: `RegistrationDetailPage.vue`
12. **Step 5c**: `RegistrationsPage.vue`
13. **Step 6**: Update `router/index.ts`
14. **Step 7**: Update `CampPage.vue`
15. **Step 8**: Write Cypress E2E tests
16. **Step 9**: Update documentation

---

## Testing Checklist

### Vitest Unit Tests

- [ ] `useRegistrations.test.ts`: all 8 scenarios pass (happy + error paths)
- [ ] `RegistrationPricingBreakdown.test.ts`: 4 scenarios
- [ ] `RegistrationCard.test.ts`: 3 scenarios
- [ ] `RegistrationMemberSelector.test.ts`: 4 scenarios (including security test)
- [ ] `RegistrationExtrasSelector.test.ts`: 4 scenarios
- [ ] Run `npx vitest --coverage` — 90%+ coverage on new files

### Cypress E2E Tests

- [ ] Representative can complete registration wizard
- [ ] Representative can add extras
- [ ] Representative can cancel pending registration
- [ ] Non-representative cannot see register button
- [ ] Non-representative cannot access wizard directly
- [ ] My registrations list shows correct data

### Manual Verification

- [ ] Wizard Steps component renders correctly with navigation
- [ ] Member selection checkboxes work (at least 1 required)
- [ ] Required extras cannot be set to 0 quantity
- [ ] Pricing breakdown renders correctly after registration creation
- [ ] Status badge shows correct color for Pending/Confirmed/Cancelled
- [ ] Error toast appears when API calls fail
- [ ] Success toast appears after registration created
- [ ] Back navigation works from detail page to list

---

## Error Handling Patterns

### Composable-Level

- All composable methods follow the `try/catch/finally` pattern with `loading.value = true/false`
- API errors are extracted: `err.response?.data?.error?.message || 'Default Spanish message'`
- Error codes to watch for:
  - `CAMP_FULL` → "El campamento ha alcanzado su capacidad máxima"
  - `REGISTRATION_EXISTS` → "Ya tienes una inscripción para este campamento"
  - `EDITION_NOT_OPEN` → "Esta edición no está disponible para inscripciones"
  - `MEMBER_NOT_IN_FAMILY` → "Algunos miembros no pertenecen a tu unidad familiar"
  - `EXTRA_NOT_IN_EDITION` → "Algunos extras no pertenecen a esta edición"
  - `REGISTRATION_NOT_EDITABLE` → "La inscripción no se puede modificar en su estado actual"

### Component-Level

- `RegisterForCampPage.vue`: error from `createRegistration` → PrimeVue `Toast` with `severity="error"`
- `RegistrationDetailPage.vue`: error from `getRegistrationById` → PrimeVue `Message` inline
- All loading states: PrimeVue `ProgressSpinner` centered
- Empty states: Descriptive gray placeholder with actionable guidance

### Wizard Partial Failure

- If `createRegistration` succeeds but `setExtras` fails: navigate to detail page with `severity="warn"` toast explaining that extras were not saved and can be edited from the detail page

---

## UI/UX Considerations

### PrimeVue Components

- `Steps` — wizard progress indicator (3 steps)
- `Checkbox` — member selection in Step 0
- `InputNumber` — extras quantity in Step 1
- `Textarea` — notes field in Step 2
- `Button` — navigation between steps and submit
- `Dialog` — cancel confirmation
- `ProgressSpinner` — loading states
- `Message` — error and warning messages
- `Toast` — success/error notifications
- `Badge` or custom `<span>` — status badges

### Tailwind CSS Layout

- Wizard: `max-w-2xl mx-auto` container, `flex flex-col gap-6`
- Steps component: full width at top
- Member selector: `grid grid-cols-1 gap-3 sm:grid-cols-2`
- Pricing breakdown: responsive table or definition list
- Registration cards list: `flex flex-col gap-4`

### Responsive Design

- Wizard is mobile-first: single column on mobile, wider on `sm:` and above
- Step navigation buttons: stacked on mobile (`flex-col`), inline on `sm:` (`flex-row`)
- Registration card: compact on mobile, wider detail on desktop

### Accessibility

- `data-testid` attributes on all interactive elements for Cypress
- `aria-label` on icon-only buttons
- Medical/allergy icons: include `aria-label="Tiene notas médicas"` tooltip
- Wizard Steps: `aria-current="step"` handled by PrimeVue's `Steps` component

---

## Dependencies

No new npm packages required. Uses existing:

- `primevue` — Steps, Checkbox, InputNumber, Textarea, Button, Dialog, Toast, ProgressSpinner, Message
- `@vueuse/core` — available if debouncing needed (not required for this feature)
- `vue-router` — routing
- `pinia` — auth store
- `axios` via `@/utils/api`
- `vitest` + `@vue/test-utils` — unit tests
- `cypress` — E2E tests

---

## Notes

### Business Rules

- Only the **family representative** (`familyUnit.representativeUserId === auth.user.id`) can create, modify, or cancel registrations — enforced at API level, but frontend shows/hides UI accordingly
- Pricing is **always server-calculated** — never trust client-side pricing
- Registration creation requires **at least 1 family member** selected
- Medical notes and allergy content must **never** be displayed — only the `hasMedicalNotes` / `hasAllergies` boolean flags as warning indicators
- A registration can only be cancelled when status is `Pending` or `Confirmed`

### Wizard Design Decisions

- Two API calls on wizard confirmation: POST /registrations, then POST /registrations/{id}/extras
- If extras step fails after registration is created: graceful degradation with warning toast — user can retry via a future "Edit Extras" flow
- No client-side price calculation — Step 3 shows edition price table as reference only, actual pricing shown on detail page after creation

### Language

- All UI text (labels, buttons, messages, validation) in **Spanish**
- All code (variables, functions, types, comments) in **English**
- Follow Spanish gender agreement rules for validation messages

### Route Order

- `/registrations/new/:editionId` MUST be declared BEFORE `/registrations/:id` in router to prevent "new" being matched as an `:id` param

---

## Next Steps After Implementation

- Payment endpoints (`POST /api/payments`, `PATCH /api/payments/{id}/complete`) are a separate feature — the detail page shows payment history but no payment creation UI yet
- Admin view of all registrations (`GET /api/registrations?all=true`) can be added to the Admin panel as a separate card/tab in a future ticket
- Push notification / email confirmation when registration is auto-confirmed is out of scope

---

## Implementation Verification

### Code Quality

- [ ] All `.vue` files use `<script setup lang="ts">` — no Options API
- [ ] No `any` types — use `unknown` or proper interfaces
- [ ] All API calls go through `useRegistrations` composable — not from components directly
- [ ] No `<style>` blocks — Tailwind utilities only
- [ ] Route guard enforced: `/registrations/new/:editionId` redirects to `/camp` if edition is not `Open`

### Functionality

- [ ] Wizard navigation (forward + back) works without data loss
- [ ] Representative check prevents non-representatives from creating registrations
- [ ] Medical note content is never rendered anywhere in the UI
- [ ] Two-step wizard confirm (create + set extras) handles partial failure gracefully

### Testing

- [ ] Vitest unit tests: 90%+ coverage on `useRegistrations.ts` and all registration components
- [ ] Cypress E2E: representative full flow + cancel flow covered
- [ ] Tests run cleanly: `npx vitest run` and `npx cypress run`

### Integration

- [ ] Composable connects to correct API endpoints
- [ ] Error codes from backend are surfaced as user-friendly Spanish messages
- [ ] Auth token included automatically via Axios interceptor

### Documentation

- [ ] `frontend-standards.mdc` updated with new type/composable references
- [ ] All documentation in English

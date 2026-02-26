# Frontend Implementation Plan: feat-membership-and-guests — Membership and Guests System

**Source spec:** [feat-membership-and-guests_enriched.md](./feat-membership-and-guests_enriched.md)
**Status:** Ready for Implementation
**Architecture:** Vue 3 Composition API, composable-based, PrimeVue + Tailwind CSS

---

## Overview

This plan covers the frontend implementation for the Membership and Guests system. The backend exposes REST endpoints for:

- **Phase 1 — Memberships:** Activate/deactivate membership per `FamilyMember`, manage annual fees (list, current-year, mark-as-paid).
- **Phase 2 — Guests:** CRUD for external guests (`Guest`) invited by a `FamilyUnit`.

The implementation follows the project's established patterns:

- Composables (`useXxx`) as the single point of API communication
- `<script setup lang="ts">` on all components
- PrimeVue for complex UI, Tailwind for layout and spacing
- Local `ref`-based state in composables (no new Pinia store needed)
- All user-facing text in Spanish

---

## Architecture Context

### Files to Create

| Type | Path | Purpose |
|---|---|---|
| Type | `frontend/src/types/membership.ts` | Membership, MembershipFee, FeeStatus types |
| Type | `frontend/src/types/guest.ts` | Guest, CreateGuestRequest, UpdateGuestRequest types |
| Composable | `frontend/src/composables/useMemberships.ts` | Membership & fee API communication |
| Composable | `frontend/src/composables/useGuests.ts` | Guest API communication |
| Component | `frontend/src/components/memberships/MembershipDialog.vue` | Membership management dialog per member |
| Component | `frontend/src/components/memberships/PayFeeDialog.vue` | Mark-as-paid form dialog |
| Component | `frontend/src/components/guests/GuestList.vue` | DataTable of guests for a family unit |
| Component | `frontend/src/components/guests/GuestForm.vue` | Create/edit guest form |
| Component | `frontend/src/components/guests/GuestFormDialog.vue` | Dialog wrapper for GuestForm |
| Test | `frontend/src/composables/__tests__/useMemberships.spec.ts` | Composable unit tests |
| Test | `frontend/src/composables/__tests__/useGuests.spec.ts` | Composable unit tests |
| E2E | `frontend/cypress/e2e/membership-management.cy.ts` | Membership E2E flows |
| E2E | `frontend/cypress/e2e/guest-management.cy.ts` | Guest E2E flows |

### Files to Modify

| Path | Change |
|---|---|
| `frontend/src/components/family-units/FamilyMemberList.vue` | Add "Gestionar membresía" action button; emit `manageMembership` |
| `frontend/src/views/FamilyUnitPage.vue` | Add TabView (Miembros / Invitados), wire MembershipDialog and GuestFormDialog |

### Routing

No new routes required. Membership and guest management are integrated directly into the existing `/family-unit` page using PrimeVue `TabView` and `Dialog` components.

### State Management

- No new Pinia store. Each composable owns local reactive state (`ref`) consistent with existing patterns (see `useFamilyUnits.ts`).
- Auth store (`useAuthStore`) provides `isAdmin`, `isBoard`, and `user.id` for role-based UI visibility.

---

## Implementation Steps

---

### Step 0: Create Feature Branch

- **Action:** Create and switch to a dedicated frontend branch to isolate changes from the backend work.
- **Branch Naming:** `feature/feat-membership-and-guests-frontend`
- **Implementation Steps:**
  1. Ensure you're on the latest `main` branch: `git checkout main && git pull origin main`
  2. Create new branch: `git checkout -b feature/feat-membership-and-guests-frontend`
  3. Verify: `git branch`
- **Notes:** Do NOT reuse the general `feature/enhance-family-units-user-linking` branch. Keep frontend and backend concerns on separate branches.

---

### Step 1: Define TypeScript Interfaces

#### 1a. `frontend/src/types/membership.ts` (NEW)

```typescript
export enum FeeStatus {
  Pending = 'Pending',
  Paid = 'Paid',
  Overdue = 'Overdue',
}

export const FeeStatusLabels: Record<FeeStatus, string> = {
  [FeeStatus.Pending]: 'Pendiente',
  [FeeStatus.Paid]: 'Pagada',
  [FeeStatus.Overdue]: 'Vencida',
}

export const FeeStatusSeverity: Record<FeeStatus, 'warn' | 'success' | 'danger'> = {
  [FeeStatus.Pending]: 'warn',
  [FeeStatus.Paid]: 'success',
  [FeeStatus.Overdue]: 'danger',
}

export interface MembershipFeeResponse {
  id: string
  membershipId: string
  year: number
  amount: number
  status: FeeStatus
  paidDate: string | null
  paymentReference: string | null
  createdAt: string
}

export interface MembershipResponse {
  id: string
  familyMemberId: string
  startDate: string        // ISO 8601 date string
  endDate: string | null
  isActive: boolean
  fees: MembershipFeeResponse[]
  createdAt: string
  updatedAt: string
}

export interface CreateMembershipRequest {
  startDate: string        // ISO 8601 date string — must not be in the future
}

export interface PayFeeRequest {
  paidDate: string         // ISO 8601 date string — must not be in the future
  paymentReference?: string | null
}
```

**Notes:**

- `startDate` and `paidDate` should be serialized as `YYYY-MM-DD` strings matching `DateOnly` semantics on the backend.
- Use `FeeStatusSeverity` map to drive PrimeVue `Tag` severity in the fee table.

#### 1b. `frontend/src/types/guest.ts` (NEW)

```typescript
export interface GuestResponse {
  id: string
  familyUnitId: string
  firstName: string
  lastName: string
  dateOfBirth: string       // ISO 8601 date string (YYYY-MM-DD)
  documentNumber: string | null
  email: string | null
  phone: string | null
  hasMedicalNotes: boolean  // Never expose actual content — backend encrypts
  hasAllergies: boolean     // Never expose actual content — backend encrypts
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateGuestRequest {
  firstName: string
  lastName: string
  dateOfBirth: string       // ISO 8601 date string (YYYY-MM-DD)
  documentNumber?: string | null
  email?: string | null
  phone?: string | null
  medicalNotes?: string | null
  allergies?: string | null
}

export type UpdateGuestRequest = CreateGuestRequest
```

**Notes:**

- `hasMedicalNotes` and `hasAllergies` are boolean flags only. Never display or allow editing of actual encrypted content via this response type.
- `medicalNotes` and `allergies` are writable on the request type (sent to server, encrypted there).

---

### Step 2: Create `useMemberships` Composable

**File:** `frontend/src/composables/useMemberships.ts` (NEW)

**Purpose:** API communication for memberships and fees. Each call follows the established pattern: set `loading`, clear `error`, try/catch, finally clear `loading`.

```typescript
import { ref } from 'vue'
import type { Ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  MembershipResponse,
  MembershipFeeResponse,
  CreateMembershipRequest,
  PayFeeRequest,
} from '@/types/membership'

export function useMemberships() {
  const membership: Ref<MembershipResponse | null> = ref(null)
  const fees: Ref<MembershipFeeResponse[]> = ref([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  /**
   * Fetch the active membership for a specific family member.
   * Returns null (without setting error) when the member has no membership (404).
   */
  const getMembership = async (
    familyUnitId: string,
    memberId: string,
  ): Promise<MembershipResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<MembershipResponse>>(
        `/family-units/${familyUnitId}/members/${memberId}/membership`,
      )
      membership.value = response.data.data
      fees.value = response.data.data?.fees ?? []
      return response.data.data
    } catch (err: any) {
      if (err.response?.status === 404) {
        membership.value = null
        fees.value = []
        return null
      }
      error.value = err.response?.data?.error?.message || 'Error al obtener la membresía'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Create a new membership for a family member.
   */
  const createMembership = async (
    familyUnitId: string,
    memberId: string,
    request: CreateMembershipRequest,
  ): Promise<MembershipResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<MembershipResponse>>(
        `/family-units/${familyUnitId}/members/${memberId}/membership`,
        request,
      )
      membership.value = response.data.data
      fees.value = response.data.data?.fees ?? []
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al crear la membresía'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Deactivate the membership for a family member (soft delete).
   */
  const deactivateMembership = async (
    familyUnitId: string,
    memberId: string,
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/family-units/${familyUnitId}/members/${memberId}/membership`)
      membership.value = null
      fees.value = []
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al desactivar la membresía'
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * List all fees for a membership.
   */
  const getFees = async (membershipId: string): Promise<MembershipFeeResponse[]> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<MembershipFeeResponse[]>>(
        `/memberships/${membershipId}/fees`,
      )
      fees.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener las cuotas'
      return []
    } finally {
      loading.value = false
    }
  }

  /**
   * Mark a specific fee as paid.
   */
  const payFee = async (
    membershipId: string,
    feeId: string,
    request: PayFeeRequest,
  ): Promise<MembershipFeeResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<MembershipFeeResponse>>(
        `/memberships/${membershipId}/fees/${feeId}/pay`,
        request,
      )
      // Update fee in local array
      const idx = fees.value.findIndex((f) => f.id === feeId)
      if (idx !== -1) fees.value[idx] = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al registrar el pago'
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    membership,
    fees,
    loading,
    error,
    getMembership,
    createMembership,
    deactivateMembership,
    getFees,
    payFee,
  }
}
```

**Implementation Notes:**

- 404 on `getMembership` is NOT an error condition — it means the member is simply not a socio yet. Handle silently.
- 409 on `createMembership` means the member already has an active membership. The `error.value` will carry the Spanish message from the backend.
- The `fees` ref is kept in sync with membership.fees on fetch and after `payFee`.

---

### Step 3: Create `useGuests` Composable

**File:** `frontend/src/composables/useGuests.ts` (NEW)

```typescript
import { ref } from 'vue'
import type { Ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type { GuestResponse, CreateGuestRequest, UpdateGuestRequest } from '@/types/guest'

export function useGuests() {
  const guests: Ref<GuestResponse[]> = ref([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const listGuests = async (familyUnitId: string): Promise<GuestResponse[]> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<GuestResponse[]>>(
        `/family-units/${familyUnitId}/guests`,
      )
      guests.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener los invitados'
      return []
    } finally {
      loading.value = false
    }
  }

  const createGuest = async (
    familyUnitId: string,
    request: CreateGuestRequest,
  ): Promise<GuestResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.post<ApiResponse<GuestResponse>>(
        `/family-units/${familyUnitId}/guests`,
        request,
      )
      guests.value.push(response.data.data)
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al crear el invitado'
      return null
    } finally {
      loading.value = false
    }
  }

  const updateGuest = async (
    familyUnitId: string,
    guestId: string,
    request: UpdateGuestRequest,
  ): Promise<GuestResponse | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.put<ApiResponse<GuestResponse>>(
        `/family-units/${familyUnitId}/guests/${guestId}`,
        request,
      )
      const idx = guests.value.findIndex((g) => g.id === guestId)
      if (idx !== -1) guests.value[idx] = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al actualizar el invitado'
      return null
    } finally {
      loading.value = false
    }
  }

  const deleteGuest = async (
    familyUnitId: string,
    guestId: string,
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      await api.delete(`/family-units/${familyUnitId}/guests/${guestId}`)
      guests.value = guests.value.filter((g) => g.id !== guestId)
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al eliminar el invitado'
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    guests,
    loading,
    error,
    listGuests,
    createGuest,
    updateGuest,
    deleteGuest,
  }
}
```

---

### Step 4: Create Membership Components

#### 4a. `PayFeeDialog.vue` (NEW)

**File:** `frontend/src/components/memberships/PayFeeDialog.vue`

**Purpose:** Minimal dialog to mark a fee as paid. Opens from within MembershipDialog.

**Props:** `visible: boolean`, `fee: MembershipFeeResponse`, `loading: boolean`
**Emits:** `update:visible`, `submit: PayFeeRequest`

**Template outline:**

- Header: "Registrar pago — Cuota {{ fee.year }}"
- Body:
  - `DatePicker` (PrimeVue) for `paidDate` — default today, max today
  - `InputText` for `paymentReference` (optional, maxlength 100)
  - Validation: paidDate is required and must not be in the future
- Footer: "Cancelar" + "Registrar pago" (primary, `:loading="loading"`)

**Implementation Notes:**

- Use `DatePicker` component (PrimeVue 4.x replaces `Calendar` — check existing usage in `FamilyMemberForm.vue` to confirm which import name is in use).
- Serialize date as `YYYY-MM-DD` string using: `paidDate.toISOString().split('T')[0]` or a shared util.
- On submit emit: `emit('submit', { paidDate: formattedDate, paymentReference: ref.value || null })`.
- Parent handles loading state and closing the dialog on success.

#### 4b. `MembershipDialog.vue` (NEW)

**File:** `frontend/src/components/memberships/MembershipDialog.vue`

**Purpose:** Full membership management for one family member. Fetches membership on open, shows status + fees, allows activate/deactivate and pay-fee.

**Props:** `visible: boolean`, `familyUnitId: string`, `memberId: string`, `memberName: string`
**Emits:** `update:visible`

**Internal state (all local — uses `useMemberships()`):**

- `membership`, `fees`, `loading`, `error` from composable
- `showPayFeeDialog: ref(false)`
- `selectedFee: ref<MembershipFeeResponse | null>(null)`
- `createStartDate: ref<Date>(new Date())` — for the create form

**Template outline:**

```
Dialog (header="Membresía — [memberName]", width=max-w-2xl)
│
├── [loading] Spinner
│
├── [error] Message component (severity="error")
│
├── [!membership] — No active membership section
│   ├── Inline message: "Este miembro no tiene una membresía activa."
│   └── Create form:
│       ├── DatePicker label="Fecha de inicio" (default today, max today)
│       └── Button "Activar membresía" @click="handleCreate"
│
└── [membership] — Active/inactive membership section
    ├── Status row:
    │   ├── Tag severity="success" value="Socio activo" (if isActive)
    │   ├── Tag severity="secondary" value="Membresía inactiva" (if !isActive)
    │   ├── Label: "Desde {{ formatDate(membership.startDate) }}"
    │   └── Button "Desactivar membresía" severity="danger" outlined (if isActive) @click="handleDeactivate"
    │
    └── Fees section (if isActive && fees.length > 0):
        ├── Title: "Cuotas"
        └── DataTable :value="fees"
            ├── Column "Año"
            ├── Column "Importe" (€ formatted)
            ├── Column "Estado" → Tag :severity="FeeStatusSeverity[row.status]" :value="FeeStatusLabels[row.status]"
            ├── Column "Fecha de pago" (formatDate or "—")
            └── Column "Acciones" → Button "Pagar" (only if status !== Paid) @click="openPayFeeDialog(fee)"

PayFeeDialog v-model:visible="showPayFeeDialog"
  :fee="selectedFee"
  :loading="loading"
  @submit="handlePayFee"
```

**Key handlers:**

```typescript
// Fetch on open
watch(() => props.visible, async (val) => {
  if (val) await getMembership(props.familyUnitId, props.memberId)
})

const handleCreate = async () => {
  const dateStr = createStartDate.value.toISOString().split('T')[0]
  const result = await createMembership(props.familyUnitId, props.memberId, { startDate: dateStr })
  if (result) toast.add({ severity: 'success', summary: 'Éxito', detail: 'Membresía activada', life: 3000 })
  else toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
}

const handleDeactivate = () => {
  confirm.require({
    message: `¿Desactivar la membresía de ${props.memberName}?`,
    header: 'Confirmar',
    acceptLabel: 'Desactivar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const ok = await deactivateMembership(props.familyUnitId, props.memberId)
      if (ok) toast.add({ severity: 'success', summary: 'Éxito', detail: 'Membresía desactivada', life: 3000 })
      else toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
    }
  })
}

const openPayFeeDialog = (fee: MembershipFeeResponse) => {
  selectedFee.value = fee
  showPayFeeDialog.value = true
}

const handlePayFee = async (request: PayFeeRequest) => {
  if (!selectedFee.value || !membership.value) return
  const result = await payFee(membership.value.id, selectedFee.value.id, request)
  if (result) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Pago registrado', life: 3000 })
    showPayFeeDialog.value = false
    selectedFee.value = null
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}
```

---

### Step 5: Create Guest Components

#### 5a. `GuestForm.vue` (NEW)

**File:** `frontend/src/components/guests/GuestForm.vue`

**Purpose:** Create/edit form for a guest. Mirrors `FamilyMemberForm.vue` style closely.

**Props:** `guest?: GuestResponse | null`, `loading?: boolean`
**Emits:** `submit: CreateGuestRequest`, `cancel`

**Fields:**

| Field | Component | Validation |
|---|---|---|
| `firstName` | InputText | Required, max 100 |
| `lastName` | InputText | Required, max 100 |
| `dateOfBirth` | DatePicker | Required, must be past date |
| `documentNumber` | InputText | Optional, uppercase alphanumeric, max 50 |
| `email` | InputText (type=email) | Optional, valid email format, max 255 |
| `phone` | InputText | Optional, E.164 format (`+34...`), max 20 |
| `medicalNotes` | Textarea | Optional, max 2000 — show privacy notice |
| `allergies` | Textarea | Optional, max 1000 — show privacy notice |

**Implementation Notes:**

- Follow `FamilyMemberForm.vue` validation pattern: inline `validateXxx()` methods that set per-field error strings.
- `documentNumber` auto-uppercased on input: `documentNumber.value = e.target.value.toUpperCase()`.
- Show privacy notice next to `medicalNotes` and `allergies` (small italic text): _"Esta información se almacena de forma encriptada y no es visible por el sistema."_
- On edit mode (`guest` prop is provided): populate form fields on `onMounted` or with `watch`.
- `dateOfBirth` serialized as `YYYY-MM-DD` string on submit.

#### 5b. `GuestFormDialog.vue` (NEW)

**File:** `frontend/src/components/guests/GuestFormDialog.vue`

**Purpose:** Dialog wrapper around `GuestForm`. Handles create vs. edit header label.

**Props:** `visible: boolean`, `familyUnitId: string`, `guest?: GuestResponse | null`, `loading?: boolean`
**Emits:** `update:visible`, `saved: GuestResponse`

**Template:**

```
Dialog
  :header="guest ? 'Editar invitado' : 'Nuevo invitado'"
  :modal="true"
  :closable="!loading"
  class="w-full max-w-2xl"

  GuestForm
    :guest="guest"
    :loading="loading"
    @submit="$emit('saved', $event)"
    @cancel="$emit('update:visible', false)"
```

**Notes:** The `saved` event carries a `GuestResponse` — the parent (FamilyUnitPage) handles it for feedback and refreshing the list. Alternatively, the parent can call `listGuests()` again.

#### 5c. `GuestList.vue` (NEW)

**File:** `frontend/src/components/guests/GuestList.vue`

**Purpose:** DataTable of guests for the family unit. Receives guests array as prop; emits edit and delete.

**Props:** `guests: GuestResponse[]`, `loading?: boolean`
**Emits:** `edit: GuestResponse`, `delete: GuestResponse`

**Columns:**

| Column | Content |
|---|---|
| Nombre | `{{ data.firstName }} {{ data.lastName }}` |
| Fecha Nacimiento | Formatted date + calculated age (same helper as FamilyMemberList) |
| Documento | `data.documentNumber \|\| '—'` |
| Contacto | email + phone icons (same pattern as FamilyMemberList) |
| Salud | `Tag` "Notas médicas" (warning) + "Alergias" (danger) flags — same as FamilyMemberList |
| Acciones | Edit (pi-pencil) + Delete (pi-trash) icon buttons |

**Empty state:** "No hay invitados registrados para esta unidad familiar"

**Implementation Notes:**

- Reuse `calculateAge` and `formatDate` helpers — consider extracting to a shared utility `frontend/src/utils/date.ts` if they are not already in a shared location. Check `FamilyMemberList.vue` first; if not already extracted, extract them here and update FamilyMemberList to import from the util.
- Pagination: `:paginator="guests.length > 10"` `:rows="10"` — same as FamilyMemberList.

---

### Step 6: Modify `FamilyMemberList.vue`

**File:** `frontend/src/components/family-units/FamilyMemberList.vue` (MODIFY)

**Changes:**

1. **Add emit:** `manageMembership: [member: FamilyMemberResponse]`

2. **Add action button** in the "Acciones" column before the Edit/Delete buttons:

   ```html
   <Button
     icon="pi pi-id-card"
     severity="secondary"
     text
     rounded
     @click="emit('manageMembership', data)"
     v-tooltip.top="'Gestionar membresía'"
   />
   ```

3. **No other changes** — do not add a membership status column that requires API calls per row (avoids N+1).

---

### Step 7: Modify `FamilyUnitPage.vue`

**File:** `frontend/src/views/FamilyUnitPage.vue` (MODIFY)

This is the most substantial change. The "Has Family Unit" section is restructured to use PrimeVue `TabView`.

#### New imports to add

```typescript
import TabView from 'primevue/tabview'
import TabPanel from 'primevue/tabpanel'
import MembershipDialog from '@/components/memberships/MembershipDialog.vue'
import GuestList from '@/components/guests/GuestList.vue'
import GuestFormDialog from '@/components/guests/GuestFormDialog.vue'
import { useGuests } from '@/composables/useGuests'
import type { GuestResponse, CreateGuestRequest, UpdateGuestRequest } from '@/types/guest'
import type { FamilyMemberResponse } from '@/types/family-unit'
```

#### New state to add

```typescript
// Membership dialog state
const showMembershipDialog = ref(false)
const selectedMemberForMembership = ref<FamilyMemberResponse | null>(null)

// Guest state
const {
  guests,
  loading: guestLoading,
  error: guestError,
  listGuests,
  createGuest,
  updateGuest,
  deleteGuest,
} = useGuests()

const showGuestDialog = ref(false)
const editingGuest = ref<GuestResponse | null>(null)
```

#### Load guests on mount (alongside family members)

```typescript
const loadFamilyUnit = async () => {
  const unit = await getCurrentUserFamilyUnit()
  if (unit) {
    await getFamilyMembers(unit.id)
    await listGuests(unit.id)   // ADD THIS LINE
  }
}
```

#### New handlers

```typescript
const handleManageMembership = (member: FamilyMemberResponse) => {
  selectedMemberForMembership.value = member
  showMembershipDialog.value = true
}

const openCreateGuestDialog = () => {
  editingGuest.value = null
  showGuestDialog.value = true
}

const openEditGuestDialog = (guest: GuestResponse) => {
  editingGuest.value = guest
  showGuestDialog.value = true
}

const handleGuestSaved = async (guest: GuestResponse) => {
  showGuestDialog.value = false
  editingGuest.value = null
  toast.add({ severity: 'success', summary: 'Éxito', detail: 'Invitado guardado', life: 3000 })
  // Refresh list (composable already updates optimistically; refresh for safety)
  if (familyUnit.value) await listGuests(familyUnit.value.id)
}

const handleDeleteGuest = (guest: GuestResponse) => {
  if (!familyUnit.value) return
  confirm.require({
    message: `¿Eliminar a ${guest.firstName} ${guest.lastName} de la lista de invitados?`,
    header: 'Confirmar Eliminación',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const ok = await deleteGuest(familyUnit.value!.id, guest.id)
      toast.add(ok
        ? { severity: 'success', summary: 'Éxito', detail: 'Invitado eliminado', life: 3000 }
        : { severity: 'error', summary: 'Error', detail: guestError.value, life: 5000 })
    }
  })
}
```

#### Template restructure — "Has Family Unit" section

Replace the current "Family Members Section" `<Card>` with a `TabView`:

```html
<!-- Family Unit Card stays as-is -->
<Card>...</Card>

<!-- TabView replaces the single members Card -->
<TabView>
  <!-- Tab 1: Members -->
  <TabPanel header="Miembros">
    <div class="flex justify-end mb-4">
      <Button icon="pi pi-plus" label="Añadir Miembro" @click="openCreateMemberDialog" />
    </div>
    <FamilyMemberList
      :members="familyMembers"
      :loading="loading"
      @edit="openEditMemberDialog"
      @delete="handleDeleteMember"
      @manage-membership="handleManageMembership"
    />
  </TabPanel>

  <!-- Tab 2: Guests -->
  <TabPanel header="Invitados">
    <div class="flex justify-end mb-4">
      <Button icon="pi pi-plus" label="Añadir Invitado" @click="openCreateGuestDialog" />
    </div>
    <GuestList
      :guests="guests"
      :loading="guestLoading"
      @edit="openEditGuestDialog"
      @delete="handleDeleteGuest"
    />
  </TabPanel>
</TabView>

<!-- Membership Dialog -->
<MembershipDialog
  v-model:visible="showMembershipDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :member-id="selectedMemberForMembership?.id ?? ''"
  :member-name="selectedMemberForMembership
    ? `${selectedMemberForMembership.firstName} ${selectedMemberForMembership.lastName}`
    : ''"
/>

<!-- Guest Form Dialog -->
<GuestFormDialog
  v-model:visible="showGuestDialog"
  :family-unit-id="familyUnit?.id ?? ''"
  :guest="editingGuest"
  :loading="guestLoading"
  @saved="handleGuestSaved"
/>
```

**Implementation Notes:**

- `TabView`/`TabPanel` are in the `primevue` package — check if they require explicit registration in `main.ts` (they may already be globally registered via the PrimeVue preset, or you may need to import them).
- In `GuestFormDialog`, the `@saved` event receives the `GuestResponse` that the form emits after calling the API. However, since `useGuests` is owned by `FamilyUnitPage`, the dialog should NOT own the composable — instead, `GuestFormDialog` should emit the raw request and the parent calls the composable. **Revised approach:**
  - Change `GuestFormDialog` emits to: `submit: CreateGuestRequest | UpdateGuestRequest`
  - `FamilyUnitPage` handles the API call via `createGuest` / `updateGuest`
  - This keeps the composable ownership at page level, consistent with how `FamilyMemberForm` + `FamilyUnitPage` work today.

**Revised GuestFormDialog approach:**

```typescript
// GuestFormDialog.vue — emits raw request, no API calls
defineEmits<{
  'update:visible': [value: boolean]
  submit: [request: CreateGuestRequest | UpdateGuestRequest]
}>()
```

```typescript
// FamilyUnitPage.vue handler
const handleGuestSubmit = async (request: CreateGuestRequest | UpdateGuestRequest) => {
  if (!familyUnit.value) return
  let ok = false
  if (editingGuest.value) {
    ok = !!(await updateGuest(familyUnit.value.id, editingGuest.value.id, request as UpdateGuestRequest))
  } else {
    ok = !!(await createGuest(familyUnit.value.id, request as CreateGuestRequest))
  }
  if (ok) {
    toast.add({ severity: 'success', summary: 'Éxito', detail: editingGuest.value ? 'Invitado actualizado' : 'Invitado añadido', life: 3000 })
    showGuestDialog.value = false
    editingGuest.value = null
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: guestError.value, life: 5000 })
  }
}
```

---

### Step 8: Write Vitest Unit Tests

#### 8a. `useMemberships.spec.ts`

**File:** `frontend/src/composables/__tests__/useMemberships.spec.ts` (NEW)

Follow the pattern of existing `useFamilyUnits.spec.ts`. Mock `@/utils/api`:

```typescript
vi.mock('@/utils/api', () => ({ api: { get: vi.fn(), post: vi.fn(), delete: vi.fn() } }))
```

Test scenarios:

- `getMembership` — returns membership when found (200)
- `getMembership` — returns null silently on 404 (no error set)
- `getMembership` — sets error on other failures
- `createMembership` — returns membership and updates local state
- `createMembership` — sets error on 409 (already exists)
- `deactivateMembership` — returns true and clears membership/fees
- `deactivateMembership` — sets error on failure
- `payFee` — updates fee in fees array on success
- `payFee` — sets error if already paid (409)

#### 8b. `useGuests.spec.ts`

**File:** `frontend/src/composables/__tests__/useGuests.spec.ts` (NEW)

Test scenarios:

- `listGuests` — populates guests ref
- `createGuest` — pushes new guest to guests ref
- `updateGuest` — replaces updated guest in guests ref
- `deleteGuest` — removes guest from guests ref
- All methods — set loading during call, clear after
- All failure cases — set error message

---

### Step 9: Write Cypress E2E Tests

#### 9a. `membership-management.cy.ts`

**File:** `frontend/cypress/e2e/membership-management.cy.ts` (NEW)

Critical flows to test (use `cy.intercept` to stub API calls):

```
Scenario: Activate membership for a family member
  Given a family unit exists with a member
  When user clicks "Gestionar membresía" on a member
  Then the MembershipDialog opens showing "Sin membresía activa"
  When user clicks "Activar membresía"
  Then POST /api/family-units/{id}/members/{id}/membership is called
  And the dialog shows the membership as active

Scenario: View and pay a fee
  Given a member has an active membership with a Pending fee
  When user opens MembershipDialog
  Then fees table shows the pending fee
  When user clicks "Pagar"
  Then PayFeeDialog opens
  When user fills date and clicks "Registrar pago"
  Then POST /api/memberships/{id}/fees/{id}/pay is called
  And fee status changes to Paid

Scenario: Deactivate membership
  Given a member has an active membership
  When user clicks "Desactivar membresía"
  And confirms the dialog
  Then DELETE /api/family-units/{id}/members/{id}/membership is called
```

#### 9b. `guest-management.cy.ts`

**File:** `frontend/cypress/e2e/guest-management.cy.ts` (NEW)

```
Scenario: Create a guest
  Given family unit page is loaded
  When user clicks "Invitados" tab
  And clicks "Añadir Invitado"
  And fills the GuestForm (firstName, lastName, dateOfBirth required)
  Then POST /api/family-units/{id}/guests is called
  And the guest appears in the list

Scenario: Edit a guest
  Given guest list has entries
  When user clicks edit on a guest
  Then GuestFormDialog opens with pre-filled data
  When user changes a field and submits
  Then PUT /api/family-units/{id}/guests/{id} is called

Scenario: Delete a guest
  Given guest list has entries
  When user clicks delete on a guest and confirms
  Then DELETE /api/family-units/{id}/guests/{id} is called
  And guest disappears from list
```

---

### Step 10: Update Technical Documentation

- **Action:** Review changes and update docs.
- **Implementation Steps:**
  1. If any new API patterns were introduced, note them in `ai-specs/specs/api-spec.yml` (once it exists).
  2. If new shared utility functions were extracted (e.g., `date.ts`), document them in `ai-specs/specs/frontend-standards.mdc` under the utilities section.
  3. Verify this plan file is up to date with any deviations made during implementation.
  4. Confirm all comments in code are in English.
- **Notes:** User-facing text (labels, error messages, toasts) must remain in Spanish per project conventions.

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-membership-and-guests-frontend`
2. **Step 1** — Define TypeScript types (`membership.ts`, `guest.ts`)
3. **Step 2** — Create `useMemberships.ts` composable
4. **Step 3** — Create `useGuests.ts` composable
5. **Step 4a** — Create `PayFeeDialog.vue`
6. **Step 4b** — Create `MembershipDialog.vue`
7. **Step 5a** — Create `GuestForm.vue`
8. **Step 5b** — Create `GuestFormDialog.vue`
9. **Step 5c** — Create `GuestList.vue`
10. **Step 6** — Modify `FamilyMemberList.vue` (add membership action button + emit)
11. **Step 7** — Modify `FamilyUnitPage.vue` (TabView + wire all new components)
12. **Step 8** — Write Vitest unit tests for composables
13. **Step 9** — Write Cypress E2E tests
14. **Step 10** — Update documentation

---

## Testing Checklist

- [ ] `useMemberships.spec.ts` — all methods covered (loading, error, success, 404 silence)
- [ ] `useGuests.spec.ts` — all methods covered (loading, error, success, local state updates)
- [ ] `MembershipDialog.vue` — renders correctly when membership=null and when membership=active
- [ ] `PayFeeDialog.vue` — validation rejects future dates, emits correct payload
- [ ] `GuestForm.vue` — required field validation, documentNumber uppercased, email/phone format
- [ ] `GuestList.vue` — empty state shows when guests=[], hasMedicalNotes/hasAllergies flags display
- [ ] `FamilyMemberList.vue` — new "Gestionar membresía" button present, emits correct member
- [ ] `FamilyUnitPage.vue` — Invitados tab visible, tab switches correctly
- [ ] Cypress: full membership activate → pay-fee → deactivate flow
- [ ] Cypress: full guest create → edit → delete flow
- [ ] Responsive: TabView, DataTable, and Dialog all usable on mobile (< 640px)
- [ ] Toast notifications appear on success and error for all operations

---

## Error Handling Patterns

All errors follow the established project pattern:

- Composable catches API errors and sets `error.value` to the Spanish message from `err.response?.data?.error?.message` with a fallback.
- Components read `error.value` from composable and show it via PrimeVue `Toast` (add via `useToast()`).
- 404 on `getMembership` is silent — it simply means no membership exists, not an error.
- 409 on `createMembership` or `payFee` (duplicate/already paid) surfaces the backend Spanish message via toast.
- Network errors show generic fallback message.

---

## UI/UX Considerations

- **TabView tabs:** "Miembros" and "Invitados" are clear labels. Tabs persist no local storage state — they reset to tab 1 on page reload (acceptable for this feature).
- **MembershipDialog:** Reuse PrimeVue `ConfirmDialog` (already present in `FamilyUnitPage` via `useConfirm`) for the deactivate confirmation. Pass `confirm.require(...)` from `MembershipDialog` directly — but note that `ConfirmDialog` component must be mounted in the DOM tree. Since it is already mounted in `FamilyUnitPage`, it is inherited. Alternatively, mount it inside `MembershipDialog` template.
- **Membership status colors:**
  - Active → `severity="success"` (green) tag: "Socio activo"
  - Inactive → `severity="secondary"` (gray) tag: "Membresía inactiva"
  - Fee Pending → `severity="warn"` (yellow)
  - Fee Paid → `severity="success"` (green)
  - Fee Overdue → `severity="danger"` (red)
- **Date serialization:** All date fields (startDate, paidDate, dateOfBirth) are `YYYY-MM-DD` strings. When using PrimeVue `DatePicker`, the selected value is a `Date` object — convert on submit with: `date.toISOString().split('T')[0]`.
- **Loading states:** Use PrimeVue `Button` `:loading="loading"` prop and DataTable `:loading="loading"` prop consistently.
- **Responsive:** Dialog widths use `class="w-full max-w-2xl"` so they are full-width on mobile. DataTable uses `responsiveLayout="scroll"`.

---

## Dependencies

All required dependencies are already installed:

| Package | Version | Usage |
|---|---|---|
| PrimeVue | 4.x | Dialog, TabView, TabPanel, DataTable, Column, Tag, Button, DatePicker, InputText, Textarea |
| @primevue/icons | — | `pi pi-id-card`, `pi pi-pencil`, `pi pi-trash` |
| Axios | 1.x | Via `@/utils/api` |
| Pinia | 3.x | Auth store (read-only for role checks) |
| Vitest | 4.x | Unit tests |
| Cypress | 15.x | E2E tests |

No new npm packages required.

---

## Notes

- **Encrypted fields (medicalNotes, allergies):** The frontend sends plain text in the request; the backend encrypts it. The frontend NEVER receives decrypted content — only the boolean flags `hasMedicalNotes` / `hasAllergies`. Never display the actual text anywhere in the UI.
- **DocumentNumber normalization:** Auto-uppercase the `documentNumber` field on user input. The backend also normalizes this, but doing it on the frontend provides immediate visual feedback.
- **Date format consistency:** Confirm that the DatePicker locale in the project is set to `es` (Spanish). Check `main.ts` for PrimeVue locale config. If needed, set `dateFormat="dd/mm/yy"` on the DatePicker for display while still serializing as ISO for the API.
- **Fee generation is automatic:** The annual fee background service is a backend concern. The frontend only displays and pays fees — it does not create them.
- **Authorization (future):** The backend spec notes that proper Rep vs. Admin/Board authorization is a TODO on the backend. For now, all authenticated users can see the UI. When the backend enforces role-based access, API calls will return 403 — the composables will surface the Spanish error message via toast. No frontend-side role gating is required for this ticket beyond what the auth store already provides.
- **No new routes:** Everything is within `/family-unit`. If in the future a dedicated membership admin view is needed, add it then.

---

## Next Steps After Implementation

1. Create a PR targeting `main` branch with this frontend branch.
2. Link the PR to the backend PR(s) for coordinated review.
3. Confirm backend endpoints are deployed (or mocked) before E2E testing against real API.
4. Smoke-test the full flow: create family unit → add member → activate membership → generate/pay fee → add guest → edit guest → delete guest.

---

## Implementation Verification

Before marking the ticket as done, verify:

- [ ] **Code Quality:** All files use `<script setup lang="ts">`, no `any` types, no `<style>` blocks
- [ ] **Functionality:** MembershipDialog opens and closes correctly, fee payment updates the table, GuestForm validates and submits correctly
- [ ] **Testing:** Vitest passes for both new composables; Cypress tests pass for both flows
- [ ] **Integration:** Composables use `@/utils/api` (not raw `axios`); types mirror backend DTOs exactly
- [ ] **Responsive:** All new dialogs and tables are usable on mobile viewport
- [ ] **Accessibility:** All Buttons have tooltips or labels; forms have proper label associations
- [ ] **Documentation:** This plan file updated with any deviations; code comments in English; UI text in Spanish

---

**Document Version:** 1.0
**Created:** 2026-02-17
**Status:** Ready for Implementation

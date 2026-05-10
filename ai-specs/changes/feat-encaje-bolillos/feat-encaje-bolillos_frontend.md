# Frontend Implementation Plan: feat-encaje-bolillos — Accommodation Assignment Interface

## Overview

Implements the "Encaje de Bolillos" Board-only UI: a two-panel assignment view where the Board selects a family and places them into an accommodation slot, with zone grouping, versioned proposals, auto-assign, and occupancy reports. The UI wires to the backend from `feat-encaje-bolillos-backend` (PR #216).

Architecture principles: Vue 3 Composition API (`<script setup lang="ts">`), composable-based API communication, PrimeVue + Tailwind CSS, no Pinia store needed (all state is local to the composable).

---

## Architecture Context

### New files to create

| File | Purpose |
|------|---------|
| `frontend/src/types/accommodation-assignment.ts` | TS interfaces mirroring all backend DTOs |
| `frontend/src/composables/useAccommodationAssignment.ts` | All API calls + local reactive state |
| `frontend/src/views/camps/AccommodationAssignmentView.vue` | Page-level container (route target) |
| `frontend/src/components/camps/AccommodationAssignmentPanel.vue` | Two-panel "Asignar" tab |
| `frontend/src/components/camps/AccommodationSummaryPanel.vue` | "Resumen" tab (by zone + unassigned) |
| `frontend/src/components/camps/AccommodationReportsPanel.vue` | "Informes" tab (by type / by zone tables) |
| `frontend/src/components/camps/FamilyAssignmentCard.vue` | Single family card (left panel) |
| `frontend/src/components/camps/AccommodationSlotCard.vue` | Single accommodation card (right panel) |
| `frontend/src/components/camps/ProposalSelectorBar.vue` | Proposal Dropdown + manage actions |
| `frontend/src/components/camps/AccommodationZonePanel.vue` | Zone CRUD (inside edition detail) |

### Files to modify

| File | Change |
|------|--------|
| `frontend/src/router/index.ts` | Add assignment route |
| `frontend/src/views/camps/CampEditionDetailPage.vue` | Add "Gestionar distribución" button + `AccommodationZonePanel` in Tab 7 |
| `frontend/src/components/camps/CampEditionAccommodationsPanel.vue` | Expose `zoneId`/`zoneName` from updated `CampEditionAccommodationResponse` |

### Routing

New sibling to `camp-edition-detail` (not nested under `/admin` — follows existing flat pattern):

```typescript
{
  path: '/camps/editions/:campEditionId/assignment',
  name: 'accommodation-assignment',
  component: () => import('@/views/camps/AccommodationAssignmentView.vue'),
  meta: {
    title: 'ABUVI | Distribución de Alojamientos',
    requiresAuth: true,
    requiresBoard: true
  }
}
```

### State management

No Pinia store. `useAccommodationAssignment` composable owns all state. Individual assign/unassign calls persist immediately via individual API calls; auto-assign persists via the backend bulk operation.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create `feature/feat-encaje-bolillos-frontend` from the latest `dev` branch.

```bash
git checkout dev
git pull origin dev
git checkout -b feature/feat-encaje-bolillos-frontend
git branch
```

> Do NOT work on `feat-encaje-bolillos` directly.

---

### Step 1: TypeScript Types

**File:** `frontend/src/types/accommodation-assignment.ts`

**Action:** Define all interfaces mirroring the backend DTOs exactly. Use `string` for UUIDs (consistent with existing types).

```typescript
export interface AccommodationZoneResponse {
  id: string
  campEditionId: string
  accommodationType: AccommodationTypeValue
  name: string
  maxCapacity: number | null
  distributionNotes: string | null
  sortOrder: number
  isActive: boolean
  accommodationIds: string[]
  createdAt: string
  updatedAt: string
}

export interface CreateAccommodationZoneRequest {
  accommodationType: AccommodationTypeValue
  name: string
  maxCapacity: number | null
  distributionNotes: string | null
  sortOrder: number
}

export interface UpdateAccommodationZoneRequest {
  name: string
  maxCapacity: number | null
  distributionNotes: string | null
  sortOrder: number
}

export interface AccommodationAssignmentProposalSummaryResponse {
  id: string
  campEditionId: string
  name: string
  notes: string | null
  isActive: boolean
  assignmentCount: number
  unassignedCount: number
  createdByUserId: string
  createdAt: string
  updatedAt: string
}

export interface AccommodationPreferenceItem {
  accommodationId: string
  preferenceOrder: number
}

export interface AssignmentFamilyResponse {
  registrationId: string
  familyUnitId: string
  familyName: string
  representativeName: string
  memberCount: number
  adultCount: number
  childCount: number
  hasPet: boolean
  specialNeeds: string | null
  campatesPreference: string | null
  accommodationPreferences: AccommodationPreferenceItem[]
}

export interface AssignmentAccommodationResponse {
  id: string
  name: string
  type: AccommodationTypeValue
  capacity: number | null
  countByFamily: boolean
  zoneId: string | null
  zoneName: string | null
  sortOrder: number
}

export interface AssignmentEntry {
  registrationId: string
  accommodationId: string
}

export interface ProposalAssignmentStateResponse {
  proposalId: string
  families: AssignmentFamilyResponse[]
  accommodations: AssignmentAccommodationResponse[]
  assignments: AssignmentEntry[]
}

export interface AssignmentReportFamilyRow {
  registrationId: string
  familyName: string
  representativeName: string
  memberCount: number
  accommodationName: string | null
  zoneName: string | null
}

export interface AssignmentReportGroupResponse {
  groupKey: string
  groupLabel: string
  totalCapacity: number
  usedCapacity: number
  families: AssignmentReportFamilyRow[]
}

export type AccommodationTypeValue = 'Lodge' | 'Bungalow' | 'Motorhome' | 'Caravan' | 'Tent'

export const ACCOMMODATION_TYPE_LABELS: Record<AccommodationTypeValue, string> = {
  Lodge: 'Albergue',
  Bungalow: 'Bungalow',
  Motorhome: 'Autocaravana',
  Caravan: 'Caravana',
  Tent: 'Tienda'
}
```

---

### Step 2: Composable

**File:** `frontend/src/composables/useAccommodationAssignment.ts`

**Action:** Single composable covering all API calls and reactive state for the assignment view.

**Signature:**

```typescript
import { ref, computed, type Ref } from 'vue'
import { api } from '@/utils/api'
import type {
  AccommodationAssignmentProposalSummaryResponse,
  ProposalAssignmentStateResponse,
  AssignmentEntry,
  CreateAccommodationZoneRequest,
  UpdateAccommodationZoneRequest,
  AccommodationZoneResponse,
} from '@/types/accommodation-assignment'
import type { ApiResponse } from '@/types/api'

export function useAccommodationAssignment(campEditionId: Ref<string>) {
  // Proposal list
  const proposals = ref<AccommodationAssignmentProposalSummaryResponse[]>([])
  const selectedProposalId = ref<string | null>(null)

  // Full assignment state for selected proposal
  const assignmentState = ref<ProposalAssignmentStateResponse | null>(null)

  // Currently selected family (click-to-assign interaction)
  const selectedRegistrationId = ref<string | null>(null)

  // UI state
  const loading = ref(false)
  const saving = ref(false)
  const error = ref<string | null>(null)

  // O(1) lookup: registrationId → accommodationId
  const assignmentsMap = computed((): Map<string, string> => {
    const map = new Map<string, string>()
    assignmentState.value?.assignments.forEach((a) => map.set(a.registrationId, a.accommodationId))
    return map
  })

  // Sorted families: unassigned first, then alphabetically
  const sortedFamilies = computed(() => {
    if (!assignmentState.value) return []
    return [...assignmentState.value.families].sort((a, b) => {
      const aAssigned = assignmentsMap.value.has(a.registrationId)
      const bAssigned = assignmentsMap.value.has(b.registrationId)
      if (aAssigned !== bAssigned) return aAssigned ? 1 : -1
      return a.familyName.localeCompare(b.familyName)
    })
  })

  async function loadProposals(): Promise<void> { ... }
  async function loadAssignmentState(): Promise<void> { ... }
  async function selectProposal(proposalId: string): Promise<void> { ... }
  async function createProposal(name: string, notes: string | null, copyFromId?: string): Promise<void> { ... }
  async function updateProposal(proposalId: string, name: string, notes: string | null): Promise<void> { ... }
  async function deleteProposal(proposalId: string): Promise<void> { ... }
  async function activateProposal(proposalId: string): Promise<void> { ... }
  async function assignFamily(registrationId: string, accommodationId: string): Promise<void> { ... }
  async function unassignFamily(registrationId: string): Promise<void> { ... }
  async function autoAssign(overwriteExisting: boolean): Promise<void> { ... }

  return {
    proposals, selectedProposalId, assignmentState, selectedRegistrationId,
    loading, saving, error, assignmentsMap, sortedFamilies,
    loadProposals, loadAssignmentState, selectProposal,
    createProposal, updateProposal, deleteProposal, activateProposal,
    assignFamily, unassignFamily, autoAssign,
  }
}
```

**Implementation details for key methods:**

```typescript
async function loadProposals(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const res = await api.get<ApiResponse<AccommodationAssignmentProposalSummaryResponse[]>>(
      `/camps/editions/${campEditionId.value}/assignment-proposals`
    )
    if (res.data.success && res.data.data) {
      proposals.value = res.data.data
      // Auto-select active proposal
      const active = res.data.data.find((p) => p.isActive)
      if (active && !selectedProposalId.value) {
        selectedProposalId.value = active.id
      }
    }
  } catch {
    error.value = 'Error al cargar propuestas'
  } finally {
    loading.value = false
  }
}

async function loadAssignmentState(): Promise<void> {
  if (!selectedProposalId.value) return
  loading.value = true
  try {
    const res = await api.get<ApiResponse<ProposalAssignmentStateResponse>>(
      `/camps/editions/${campEditionId.value}/assignment-proposals/${selectedProposalId.value}/assignments`
    )
    if (res.data.success && res.data.data) {
      assignmentState.value = res.data.data
    }
  } catch {
    error.value = 'Error al cargar el estado de asignaciones'
  } finally {
    loading.value = false
  }
}

async function assignFamily(registrationId: string, accommodationId: string): Promise<void> {
  if (!selectedProposalId.value) return
  saving.value = true
  try {
    await api.post(
      `/camps/editions/${campEditionId.value}/assignment-proposals/${selectedProposalId.value}/assignments/${registrationId}`,
      { accommodationId }
    )
    await loadAssignmentState()
    selectedRegistrationId.value = null
  } catch (err: unknown) {
    const msg = (err as { response?: { data?: { error?: { message?: string } } } })
      ?.response?.data?.error?.message
    error.value = msg ?? 'Error al asignar familia'
  } finally {
    saving.value = false
  }
}

async function autoAssign(overwriteExisting: boolean): Promise<void> {
  if (!selectedProposalId.value) return
  saving.value = true
  try {
    const res = await api.post<ApiResponse<ProposalAssignmentStateResponse>>(
      `/camps/editions/${campEditionId.value}/assignment-proposals/${selectedProposalId.value}/assignments/auto-assign`,
      { overwriteExisting }
    )
    if (res.data.success && res.data.data) {
      assignmentState.value = res.data.data
    }
  } catch {
    error.value = 'Error en el auto-asignar'
  } finally {
    saving.value = false
  }
}
```

---

### Step 3: Zone Management Composable

**File:** `frontend/src/composables/useAccommodationZones.ts`

**Action:** Separate lightweight composable used only in the edition detail zone panel. Keeps zone CRUD isolated from the assignment board state.

```typescript
export function useAccommodationZones(campEditionId: Ref<string>) {
  const zones = ref<AccommodationZoneResponse[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadZones(): Promise<void> { ... }
  async function createZone(req: CreateAccommodationZoneRequest): Promise<void> { ... }
  async function updateZone(zoneId: string, req: UpdateAccommodationZoneRequest): Promise<void> { ... }
  async function deleteZone(zoneId: string): Promise<void> { ... }
  async function attachAccommodations(zoneId: string, accommodationIds: string[]): Promise<void> { ... }

  return { zones, loading, error, loadZones, createZone, updateZone, deleteZone, attachAccommodations }
}
```

API base path: `/camps/editions/{campEditionId}/accommodation-zones`

---

### Step 4: `FamilyAssignmentCard.vue`

**File:** `frontend/src/components/camps/FamilyAssignmentCard.vue`

**Action:** Display card for a single family in the left panel of the assignment board.

**Props:**

```typescript
defineProps<{
  family: AssignmentFamilyResponse
  assignedAccommodationName: string | null  // derived by parent from assignmentsMap
  isSelected: boolean
}>()

defineEmits<{
  (e: 'select', registrationId: string): void
}>()
```

**Template structure (Tailwind only, no `<style>`):**

```html
<div
  class="cursor-pointer rounded-lg border p-3 transition-colors"
  :class="isSelected ? 'border-primary-500 bg-primary-50' : 'border-gray-200 bg-white hover:border-gray-300'"
  @click="$emit('select', family.registrationId)"
>
  <!-- Row 1: Name + member count -->
  <div class="flex items-center justify-between">
    <span class="font-medium text-gray-900">{{ family.familyName }}</span>
    <span class="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
      {{ family.memberCount }} pers.
    </span>
  </div>
  <!-- Row 2: Representative -->
  <p class="mt-0.5 text-xs text-gray-500">{{ family.representativeName }}</p>
  <!-- Row 3: Signals + preferences -->
  <div class="mt-1 flex flex-wrap items-center gap-1">
    <i v-if="family.hasPet" class="pi pi-tag text-amber-500" title="Mascota" />
    <i v-if="family.specialNeeds" class="pi pi-exclamation-circle text-red-400" title="Necesidades especiales" />
    <!-- Preference badges: 1ª / 2ª / 3ª -->
    <span
      v-for="pref in family.accommodationPreferences"
      :key="pref.preferenceOrder"
      class="text-xs text-gray-400"
    >{{ pref.preferenceOrder }}ª</span>
  </div>
  <!-- Row 4: Assignment status -->
  <p class="mt-1 text-xs" :class="assignedAccommodationName ? 'text-green-600' : 'text-gray-400'">
    {{ assignedAccommodationName ?? 'Sin asignar' }}
  </p>
</div>
```

---

### Step 5: `AccommodationSlotCard.vue`

**File:** `frontend/src/components/camps/AccommodationSlotCard.vue`

**Action:** Accommodation card for the right panel grid. Shows occupancy, assigned families, and preference signal border when a family is selected.

**Props:**

```typescript
defineProps<{
  accommodation: AssignmentAccommodationResponse
  assignedFamilies: AssignmentFamilyResponse[]   // families currently assigned here
  selectedFamily: AssignmentFamilyResponse | null  // the family selected in the left panel
}>()

defineEmits<{
  (e: 'assign', accommodationId: string): void
  (e: 'unassign', registrationId: string): void
}>()
```

**Computed: preference signal** (determines border colour)

```typescript
const signalClass = computed(() => {
  if (!selectedFamily.value) return 'border-gray-200'
  const prefs = selectedFamily.value.accommodationPreferences
  const pref = prefs.find((p) => p.accommodationId === accommodation.id)
  if (pref?.preferenceOrder === 1) return 'border-green-400 ring-1 ring-green-300'
  if (pref?.preferenceOrder === 2 || pref?.preferenceOrder === 3) return 'border-amber-400 ring-1 ring-amber-300'
  // Check over-capacity
  if (isOverCapacity.value) return 'border-red-400 ring-1 ring-red-300'
  return 'border-blue-200'
})
```

**Computed: occupancy**

```typescript
const occupiedUnits = computed(() => {
  if (accommodation.countByFamily) return assignedFamilies.value.length
  return assignedFamilies.value.reduce((sum, f) => sum + f.memberCount, 0)
})

const isOverCapacity = computed(() =>
  accommodation.capacity !== null && occupiedUnits.value > accommodation.capacity
)
```

**Template structure:**

```html
<div
  class="rounded-lg border-2 p-3 transition-all"
  :class="[signalClass, selectedFamily ? 'cursor-pointer' : '']"
  @click="selectedFamily && $emit('assign', accommodation.id)"
>
  <!-- Header: name + capacity bar -->
  <div class="flex items-center justify-between">
    <span class="text-sm font-semibold">{{ accommodation.name }}</span>
    <span class="text-xs" :class="isOverCapacity ? 'text-red-600 font-bold' : 'text-gray-500'">
      {{ occupiedUnits }} / {{ accommodation.capacity ?? '∞' }}
      {{ accommodation.countByFamily ? 'fam.' : 'pers.' }}
    </span>
  </div>
  <ProgressBar
    v-if="accommodation.capacity"
    :value="Math.min(100, Math.round((occupiedUnits / accommodation.capacity) * 100))"
    class="mt-1 h-1.5"
    :pt="{ value: { class: isOverCapacity ? 'bg-red-500' : 'bg-primary-500' } }"
  />
  <!-- Assigned family chips -->
  <div class="mt-2 flex flex-wrap gap-1">
    <div
      v-for="f in assignedFamilies"
      :key="f.registrationId"
      class="flex items-center gap-1 rounded bg-gray-100 px-2 py-0.5 text-xs"
    >
      <span>{{ f.familyName }} ({{ f.memberCount }})</span>
      <button
        class="ml-1 text-gray-400 hover:text-red-500"
        @click.stop="$emit('unassign', f.registrationId)"
      >×</button>
    </div>
    <span v-if="assignedFamilies.length === 0" class="text-xs text-gray-300 italic">Vacío</span>
  </div>
</div>
```

---

### Step 6: `ProposalSelectorBar.vue`

**File:** `frontend/src/components/camps/ProposalSelectorBar.vue`

**Action:** Toolbar strip for proposal management.

**Props + emits:**

```typescript
defineProps<{
  proposals: AccommodationAssignmentProposalSummaryResponse[]
  modelValue: string | null  // selected proposalId (v-model)
  saving: boolean
}>()

defineEmits<{
  (e: 'update:modelValue', proposalId: string): void
  (e: 'create', payload: { name: string; notes: string | null; copyFromId?: string }): void
  (e: 'activate', proposalId: string): void
  (e: 'delete', proposalId: string): void
  (e: 'autoAssign'): void
}>()
```

**Template structure:**

```html
<div class="flex flex-wrap items-center gap-2 border-b bg-white p-3">
  <!-- Proposal selector -->
  <Select
    :model-value="modelValue"
    :options="proposals"
    option-label="name"
    option-value="id"
    placeholder="Seleccionar propuesta..."
    class="w-56"
    @update:model-value="$emit('update:modelValue', $event)"
  />

  <!-- Active badge -->
  <Tag
    v-if="activeProposal"
    value="Activa"
    severity="success"
    class="text-xs"
  />

  <!-- Activate button (only if selected is not active) -->
  <Button
    v-if="selectedProposal && !selectedProposal.isActive"
    label="Activar propuesta"
    size="small"
    outlined
    @click="$emit('activate', modelValue!)"
  />

  <!-- New proposal button -->
  <Button
    label="Nueva propuesta"
    icon="pi pi-plus"
    size="small"
    outlined
    @click="newProposalDialog = true"
  />

  <!-- Auto-assign button -->
  <Button
    label="Auto-asignar"
    icon="pi pi-bolt"
    size="small"
    :loading="saving"
    @click="$emit('autoAssign')"
  />

  <!-- Stats: X sin asignar -->
  <span v-if="selectedProposal" class="ml-auto text-sm text-gray-500">
    {{ selectedProposal.unassignedCount }} sin asignar · {{ selectedProposal.assignmentCount }} asignadas
  </span>
</div>

<!-- New Proposal Dialog -->
<Dialog v-model:visible="newProposalDialog" header="Nueva propuesta" modal class="w-96">
  <div class="flex flex-col gap-3">
    <InputText v-model="newName" placeholder="Nombre de la propuesta" />
    <Textarea v-model="newNotes" placeholder="Notas (opcional)" rows="2" />
    <Select
      v-model="copyFromId"
      :options="proposals"
      option-label="name"
      option-value="id"
      placeholder="Copiar desde (opcional)"
    />
  </div>
  <template #footer>
    <Button label="Cancelar" text @click="newProposalDialog = false" />
    <Button label="Crear" @click="handleCreate" />
  </template>
</Dialog>
```

---

### Step 7: `AccommodationAssignmentPanel.vue`

**File:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

**Action:** Two-panel assignment board. Left panel = family list; right panel = accommodation grid grouped by type → zone.

**Props:**

```typescript
defineProps<{
  state: ProposalAssignmentStateResponse
  assignmentsMap: Map<string, string>
  selectedRegistrationId: string | null
  saving: boolean
}>()

defineEmits<{
  (e: 'selectFamily', registrationId: string): void
  (e: 'assign', registrationId: string, accommodationId: string): void
  (e: 'unassign', registrationId: string): void
}>()
```

**Computed: grouped accommodations** (group by type, then by zone)

```typescript
// families sorted: unassigned first then alphabetical (derived from props)
const sortedFamilies = computed(() => ...)

// families assigned to a given accommodationId
function assignedFamiliesFor(accId: string): AssignmentFamilyResponse[] {
  return state.families.filter((f) => assignmentsMap.get(f.registrationId) === accId)
}

// Group accommodations: Map<AccommodationTypeValue, Map<string|null, AssignmentAccommodationResponse[]>>
const groupedAccommodations = computed(() => {
  const byType = new Map<string, Map<string | null, AssignmentAccommodationResponse[]>>()
  for (const acc of state.accommodations) {
    if (!byType.has(acc.type)) byType.set(acc.type, new Map())
    const byZone = byType.get(acc.type)!
    const zoneKey = acc.zoneName ?? null
    if (!byZone.has(zoneKey)) byZone.set(zoneKey, [])
    byZone.get(zoneKey)!.push(acc)
  }
  return byType
})

const selectedFamily = computed(() =>
  state.families.find((f) => f.registrationId === selectedRegistrationId) ?? null
)

const searchQuery = ref('')
const filteredFamilies = computed(() =>
  sortedFamilies.value.filter((f) =>
    f.familyName.toLowerCase().includes(searchQuery.value.toLowerCase()) ||
    f.representativeName.toLowerCase().includes(searchQuery.value.toLowerCase())
  )
)
```

**Template (CSS Grid two-column layout):**

```html
<div class="grid h-full grid-cols-[300px_1fr] overflow-hidden">
  <!-- Left: Family list -->
  <div class="flex flex-col border-r bg-gray-50 overflow-y-auto">
    <!-- Search + count -->
    <div class="border-b p-3">
      <IconField>
        <InputIcon class="pi pi-search" />
        <InputText v-model="searchQuery" placeholder="Buscar familia..." class="w-full" size="small" />
      </IconField>
      <p class="mt-1 text-xs text-gray-500">
        {{ unassignedCount }} sin asignar · {{ state.families.length }} total
      </p>
    </div>
    <!-- Family cards -->
    <div class="flex flex-col gap-1 p-2 overflow-y-auto">
      <FamilyAssignmentCard
        v-for="family in filteredFamilies"
        :key="family.registrationId"
        :family="family"
        :assigned-accommodation-name="assignedAccommodationName(family.registrationId)"
        :is-selected="family.registrationId === selectedRegistrationId"
        @select="$emit('selectFamily', $event)"
      />
    </div>
  </div>

  <!-- Right: Accommodation grid -->
  <div class="overflow-y-auto p-4">
    <div v-for="[type, byZone] in groupedAccommodations" :key="type" class="mb-6">
      <!-- Type header -->
      <h3 class="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">
        {{ ACCOMMODATION_TYPE_LABELS[type as AccommodationTypeValue] }}
      </h3>
      <!-- Zones -->
      <div v-for="[zoneName, accommodations] in byZone" :key="zoneName ?? '__none__'" class="mb-4">
        <h4 class="mb-2 text-xs font-medium text-gray-400">
          {{ zoneName ?? 'Sin zona' }}
        </h4>
        <div class="grid grid-cols-2 gap-2 lg:grid-cols-3 xl:grid-cols-4">
          <AccommodationSlotCard
            v-for="acc in accommodations"
            :key="acc.id"
            :accommodation="acc"
            :assigned-families="assignedFamiliesFor(acc.id)"
            :selected-family="selectedFamily"
            @assign="handleAssign(acc.id)"
            @unassign="$emit('unassign', $event)"
          />
        </div>
      </div>
    </div>
  </div>
</div>
```

---

### Step 8: `AccommodationSummaryPanel.vue`

**File:** `frontend/src/components/camps/AccommodationSummaryPanel.vue`

**Action:** Summary tab. Consumes `GET /reports/by-zone` response. Shows a DataTable with expandable rows per zone, and a warning section for unassigned families.

**Props:**

```typescript
defineProps<{
  proposalId: string
  campEditionId: string
}>()
```

**Internal state (loaded on mount):**

```typescript
const { api } = ... // import from @/utils/api
const byZoneData = ref<AssignmentReportGroupResponse[]>([])
const unassignedFamilies = ref<AssignmentFamilyResponse[]>([])
const loading = ref(false)

async function loadReports(): Promise<void> {
  loading.value = true
  // parallel fetch
  const [zoneRes, unassignedRes] = await Promise.all([
    api.get(`/camps/editions/${campEditionId}/assignment-proposals/${proposalId}/reports/by-zone`),
    api.get(`/camps/editions/${campEditionId}/assignment-proposals/${proposalId}/reports/unassigned`)
  ])
  byZoneData.value = zoneRes.data.data ?? []
  unassignedFamilies.value = unassignedRes.data.data ?? []
  loading.value = false
}

onMounted(loadReports)
```

**Template:**

```html
<div class="p-4">
  <!-- Unassigned warning -->
  <Message
    v-if="unassignedFamilies.length > 0"
    severity="warn"
    class="mb-4"
  >
    {{ unassignedFamilies.length }} familia(s) sin asignar:
    {{ unassignedFamilies.map(f => f.familyName).join(', ') }}
  </Message>

  <!-- By-zone DataTable with expandable rows -->
  <DataTable
    :value="byZoneData"
    row-group-mode="subheader"
    expandable-row-groups
    :loading="loading"
  >
    <Column field="groupLabel" header="Zona" />
    <Column header="Capacidad total">
      <template #body="{ data }">{{ data.totalCapacity }}</template>
    </Column>
    <Column header="Ocupación">
      <template #body="{ data }">
        <span :class="data.usedCapacity > data.totalCapacity ? 'text-red-600 font-bold' : ''">
          {{ data.usedCapacity }}
        </span>
      </template>
    </Column>
    <Column header="% ocupación">
      <template #body="{ data }">
        {{ data.totalCapacity > 0 ? Math.round((data.usedCapacity / data.totalCapacity) * 100) : 0 }}%
      </template>
    </Column>
    <template #expansion="{ data }">
      <DataTable :value="data.families" size="small">
        <Column field="familyName" header="Familia" />
        <Column field="representativeName" header="Representante" />
        <Column field="memberCount" header="Personas" />
        <Column field="accommodationName" header="Alojamiento" />
      </DataTable>
    </template>
  </DataTable>
</div>
```

---

### Step 9: `AccommodationReportsPanel.vue`

**File:** `frontend/src/components/camps/AccommodationReportsPanel.vue`

**Action:** Reports tab. Two sub-tabs (by-type and by-zone) using PrimeVue `Tabs`.

**Template:**

```html
<div class="p-4">
  <Tabs value="by-type">
    <TabList>
      <Tab value="by-type">Por tipo de alojamiento</Tab>
      <Tab value="by-zone">Por zona</Tab>
    </TabList>
    <TabPanels>
      <TabPanel value="by-type">
        <!-- DataTable same structure as SummaryPanel but with byTypeData -->
      </TabPanel>
      <TabPanel value="by-zone">
        <!-- DataTable with byZoneData -->
      </TabPanel>
    </TabPanels>
  </Tabs>
</div>
```

Each DataTable: columns `Grupo | Capacidad total | Ocupación | % | Familias`. Expandable rows show family list. Add a `<!-- TODO: Export to CSV/Excel -->` comment at the end of the component.

---

### Step 10: `AccommodationZonePanel.vue`

**File:** `frontend/src/components/camps/AccommodationZonePanel.vue`

**Action:** Zone CRUD panel embedded inside the "Alojamientos" section of `CampEditionDetailPage.vue`. Collapsible section. Board-only.

**Props:**

```typescript
defineProps<{
  campEditionId: string
  accommodations: CampEditionAccommodationResponse[]  // existing accommodations list (from parent)
}>()
```

Uses `useAccommodationZones` composable.

**Features:**
- List zones in a DataTable with columns: Nombre | Tipo | Capacidad | Alojamientos (count) | Acciones
- "Nueva zona" button opens a Dialog with fields: Nombre, Tipo (Select from AccommodationType), Capacidad máxima, Notas
- Edit icon (pencil) opens same dialog pre-filled
- Delete icon (with confirmation via `useConfirm()`) calls `deleteZone()`
- "Gestionar alojamientos" button opens a second dialog with a multi-select `Listbox` to attach/detach accommodations of the matching type

---

### Step 11: Main View `AccommodationAssignmentView.vue`

**File:** `frontend/src/views/camps/AccommodationAssignmentView.vue`

**Action:** Page container. Reads `:campEditionId` from route. Renders `ProposalSelectorBar` + tabs (Asignar / Resumen / Informes).

```typescript
<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import { useAccommodationAssignment } from '@/composables/useAccommodationAssignment'
import ProposalSelectorBar from '@/components/camps/ProposalSelectorBar.vue'
import AccommodationAssignmentPanel from '@/components/camps/AccommodationAssignmentPanel.vue'
import AccommodationSummaryPanel from '@/components/camps/AccommodationSummaryPanel.vue'
import AccommodationReportsPanel from '@/components/camps/AccommodationReportsPanel.vue'

const route = useRoute()
const toast = useToast()
const campEditionId = computed(() => route.params.campEditionId as string)

const {
  proposals, selectedProposalId, assignmentState, selectedRegistrationId,
  loading, saving, error, assignmentsMap,
  loadProposals, loadAssignmentState, selectProposal,
  createProposal, activateProposal, deleteProposal,
  assignFamily, unassignFamily, autoAssign,
} = useAccommodationAssignment(campEditionId)

async function handleAssign(registrationId: string, accommodationId: string) {
  await assignFamily(registrationId, accommodationId)
  if (error.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 4000 })
    error.value = null
  }
}

async function handleAutoAssign() {
  await autoAssign(false)
  if (error.value) {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 4000 })
    error.value = null
  } else {
    toast.add({ severity: 'success', summary: 'Auto-asignado', detail: 'Familias asignadas automáticamente', life: 3000 })
  }
}

watch(selectedProposalId, (id) => {
  if (id) loadAssignmentState()
})

onMounted(async () => {
  await loadProposals()
  await loadAssignmentState()
})
</script>
```

**Template:**

```html
<template>
  <div class="flex h-screen flex-col overflow-hidden">
    <!-- Sticky top bar -->
    <div class="shrink-0 border-b bg-white shadow-sm">
      <div class="flex items-center gap-3 px-4 py-2">
        <!-- Back button -->
        <Button
          icon="pi pi-arrow-left"
          text
          size="small"
          @click="$router.back()"
        />
        <h1 class="text-base font-semibold">Distribución de Alojamientos</h1>
      </div>
      <ProposalSelectorBar
        v-model="selectedProposalId"
        :proposals="proposals"
        :saving="saving"
        @create="createProposal($event.name, $event.notes, $event.copyFromId)"
        @activate="activateProposal($event)"
        @delete="deleteProposal($event)"
        @auto-assign="handleAutoAssign"
      />
    </div>

    <!-- Tabs -->
    <Tabs value="assign" class="flex min-h-0 flex-1 flex-col overflow-hidden">
      <TabList class="shrink-0 bg-white px-4">
        <Tab value="assign"><i class="pi pi-objects-column mr-2" />Asignar</Tab>
        <Tab value="summary"><i class="pi pi-chart-bar mr-2" />Resumen</Tab>
        <Tab value="reports"><i class="pi pi-file-edit mr-2" />Informes</Tab>
      </TabList>

      <TabPanels class="min-h-0 flex-1 overflow-hidden p-0">
        <TabPanel value="assign" class="h-full">
          <div v-if="loading" class="flex h-full items-center justify-center">
            <ProgressSpinner />
          </div>
          <AccommodationAssignmentPanel
            v-else-if="assignmentState"
            :state="assignmentState"
            :assignments-map="assignmentsMap"
            :selected-registration-id="selectedRegistrationId"
            :saving="saving"
            @select-family="selectedRegistrationId = $event"
            @assign="handleAssign"
            @unassign="unassignFamily"
          />
          <div v-else class="flex h-full items-center justify-center text-gray-400">
            Selecciona una propuesta para comenzar
          </div>
        </TabPanel>
        <TabPanel value="summary" class="overflow-y-auto">
          <AccommodationSummaryPanel
            v-if="selectedProposalId"
            :proposal-id="selectedProposalId"
            :camp-edition-id="campEditionId"
          />
        </TabPanel>
        <TabPanel value="reports" class="overflow-y-auto">
          <AccommodationReportsPanel
            v-if="selectedProposalId"
            :proposal-id="selectedProposalId"
            :camp-edition-id="campEditionId"
          />
        </TabPanel>
      </TabPanels>
    </Tabs>
  </div>
</template>
```

---

### Step 12: Routing

**File:** `frontend/src/router/index.ts`

**Action:** Add the new route after `camp-edition-detail` (line ~220), keeping the same flat structure.

```typescript
{
  path: '/camps/editions/:campEditionId/assignment',
  name: 'accommodation-assignment',
  component: () => import('@/views/camps/AccommodationAssignmentView.vue'),
  meta: {
    title: 'ABUVI | Distribución de Alojamientos',
    requiresAuth: true,
    requiresBoard: true
  }
},
```

---

### Step 13: Entry Point in `CampEditionDetailPage.vue`

**File:** `frontend/src/views/camps/CampEditionDetailPage.vue`

**Action:** In Tab 7 (Alojamientos), add a "Gestionar distribución" button above the existing `CampEditionAccommodationsPanel` and add the `AccommodationZonePanel` below it.

**Changes:**

1. Add import at top of `<script setup>`:
   ```typescript
   import AccommodationZonePanel from '@/components/camps/AccommodationZonePanel.vue'
   import { useRouter } from 'vue-router'
   ```

2. In the section `v-if="activeTab === '7'"` and `v-if="isBoard"` (line ~726), add:
   ```html
   <!-- Entry point to assignment board -->
   <div class="mb-4 flex justify-end">
     <Button
       label="Gestionar distribución de alojamientos"
       icon="pi pi-objects-column"
       @click="router.push({ name: 'accommodation-assignment', params: { campEditionId: edition.id } })"
     />
   </div>
   <!-- Existing panel -->
   <CampEditionAccommodationsPanel :edition-id="edition.id" />
   <!-- Zone management -->
   <div class="mt-6">
     <AccommodationZonePanel
       :camp-edition-id="edition.id"
       :accommodations="edition.accommodations ?? []"
     />
   </div>
   ```

---

### Step 14: Update `CampEditionAccommodationsPanel.vue`

**File:** `frontend/src/components/camps/CampEditionAccommodationsPanel.vue`

**Action:** The backend now returns `zoneId` and `zoneName` in `CampEditionAccommodationResponse`. Update the DataTable or list to show the zone name as an additional column or badge on each accommodation row.

Add to the type for accommodation (in the component or in `types/camp-edition.ts`):

```typescript
// Extend existing CampEditionAccommodationResponse (or the local type used in this component):
zoneId?: string | null
zoneName?: string | null
```

Add a "Zona" column in the DataTable:

```html
<Column header="Zona">
  <template #body="{ data }">
    <span v-if="data.zoneName" class="rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
      {{ data.zoneName }}
    </span>
    <span v-else class="text-xs text-gray-300">—</span>
  </template>
</Column>
```

---

### Step 15: Unit Tests (Vitest)

**File:** `frontend/src/composables/__tests__/useAccommodationAssignment.test.ts`

Required test cases (mock `api` from `@/utils/api` with `vi.mock`):

```
loadProposals_withActiveProposal_autoSelectsIt
loadProposals_withApiError_setsErrorMessage
assignFamily_callsCorrectEndpoint
assignFamily_onApiError_setsErrorMessage
unassignFamily_callsDeleteEndpoint
autoAssign_callsAutoAssignEndpoint_andUpdatesState
selectProposal_triggersLoadAssignmentState
assignmentsMap_returnsCorrectLookup
sortedFamilies_putsUnassignedFirst
```

**File:** `frontend/src/components/camps/__tests__/AccommodationSlotCard.test.ts`

```
renders_accommodationNameAndCapacity
showsGreenBorder_whenSelectedFamilyHasFirstPreference
showsAmberBorder_whenSelectedFamilyHasSecondPreference
showsRedBorder_whenOverCapacity
emitsAssign_onClickWhenFamilySelected
emitsUnassign_onChipClose
countsOccupancyByFamily_forCaravan
countsOccupancyByPerson_forLodge
```

---

### Step 16: Update Technical Documentation

**Action:** Review and update documentation after implementation.

- No API spec changes needed (backend PR #216 already added to `api-endpoints.md`)
- If any new Tailwind or PrimeVue patterns emerge, document them in `ai-specs/specs/frontend-standards.mdc`
- Update routing section in documentation if applicable

---

## Implementation Order

1. Step 0 — Create branch `feature/feat-encaje-bolillos-frontend`
2. Step 1 — TypeScript types (`accommodation-assignment.ts`)
3. Step 2 — `useAccommodationAssignment` composable
4. Step 3 — `useAccommodationZones` composable
5. Step 4 — `FamilyAssignmentCard.vue`
6. Step 5 — `AccommodationSlotCard.vue`
7. Step 6 — `ProposalSelectorBar.vue`
8. Step 7 — `AccommodationAssignmentPanel.vue`
9. Step 8 — `AccommodationSummaryPanel.vue`
10. Step 9 — `AccommodationReportsPanel.vue`
11. Step 10 — `AccommodationZonePanel.vue`
12. Step 11 — `AccommodationAssignmentView.vue`
13. Step 12 — Router update
14. Step 13 — Entry point in `CampEditionDetailPage.vue`
15. Step 14 — `CampEditionAccommodationsPanel.vue` update
16. Step 15 — Unit tests
17. Step 16 — Documentation

---

## Testing Checklist

- [ ] `npm run build` passes (no TypeScript errors)
- [ ] All Vitest unit tests pass (`npm run test:unit`)
- [ ] `/camps/editions/:id/assignment` loads for valid edition with ≥ 1 proposal
- [ ] Selecting a family highlights compatible accommodations with correct border colours
- [ ] Clicking an accommodation with family selected → assigns and reloads state
- [ ] Clicking × on a chip → unassigns family
- [ ] Auto-assign fills families that can be placed; unplaceable left as-is
- [ ] Proposal creation dialog validates empty name (shows error)
- [ ] Proposal activation deactivates others (Active badge moves)
- [ ] Summary tab shows correct unassigned warning and zone table
- [ ] Reports tab shows both sub-tabs with grouped data
- [ ] Zone panel in edition detail: create / rename / delete / attach accommodations
- [ ] "Gestionar distribución" button navigates correctly
- [ ] `requiresBoard` guard blocks non-Board users (redirect to /home)

---

## Error Handling Patterns

- Composable pattern: each async method sets `error.value` on failure; caller reads it after `await` and shows a `toast.add({ severity: 'error', ... })` before clearing `error.value = null`
- `saving.value = true` during all write operations; spinners on affected buttons via `:loading="saving"`
- Capacity-exceeded 422 from backend → extract `error.message` from `ApiResponse.error.message` and show as toast
- Network errors → generic "Error de red. Inténtalo de nuevo." toast

---

## UI/UX Considerations

- **Two-panel layout:** CSS Grid `grid-cols-[300px_1fr]` — fixed left panel, scrollable right. Full viewport height (`h-screen`). Avoids PrimeVue Splitter (not used elsewhere in the project).
- **PrimeVue components used:** `Tabs/TabList/Tab/TabPanels/TabPanel`, `Select`, `Dialog`, `DataTable/Column`, `ProgressBar`, `ProgressSpinner`, `Button`, `InputText`, `Textarea`, `Tag`, `Message`, `Listbox`, `IconField/InputIcon`
- **No `<style>` blocks** — all styling via Tailwind utilities
- **Responsive:** Two-panel layout collapses to single column on `md:` and below (left panel becomes a collapsible drawer or sheet)
- **Accessibility:** `title` attributes on icon-only signals (pet, special needs), keyboard navigation via `tabindex` on cards
- **Loading states:** `ProgressSpinner` covers the assignment panel while `loading = true`; `Button :loading="saving"` on write actions

---

## Dependencies

No new npm packages. All required components are already in the project:

| Dependency | Already installed |
|------------|-------------------|
| PrimeVue 4.x (`Tabs`, `DataTable`, `Dialog`, etc.) | ✅ |
| Tailwind CSS | ✅ |
| `@/utils/api` (Axios instance) | ✅ |
| Vitest + Vue Test Utils | ✅ |

---

## Notes

- **Branch naming mandatory:** `feature/feat-encaje-bolillos-frontend` — do not work on `feat-encaje-bolillos` directly.
- **API base path:** All calls use the pattern `/camps/editions/{campEditionId}/...` (matches the backend PR #216 endpoints). Import `api` from `@/utils/api`, **not** `@/lib/axios`.
- **No Pinia store:** All state lives inside `useAccommodationAssignment`. Do not create a store — this data is not shared across views.
- **Auto-assign persists server-side:** The backend `POST /auto-assign` always calls `BulkReplaceAsync` and persists. There is no dry-run mode. Show a confirmation dialog before calling with `overwriteExisting: true`.
- **`countByFamily` field:** The backend already computes this (`true` for Caravan/Tent). Use it directly — do not re-compute from `type` in the frontend.
- **Tab rendering:** Use PrimeVue `Tabs`/`TabList`/`Tab`/`TabPanels`/`TabPanel` (v4 headless API) — **not** the older `TabView`/`TabPanel` API. See `PaymentsAdminPanel.vue` for the exact import pattern.
- **TypeScript strict:** No `any`. Use `unknown` with type guards when needed. All props fully typed with interfaces from `accommodation-assignment.ts`.
- **Spanish UI text only.** Error messages from the API are already in Spanish.

---

## Next Steps After Implementation

1. Create PR `feature/feat-encaje-bolillos-frontend → dev`
2. Coordinate QA testing with the Board — they are the primary users
3. Zone drag-to-reorder (PrimeVue `OrderList`) is out of scope for this ticket — add as a follow-up
4. CSV/Excel export from reports is out of scope — TODO comment left in `AccommodationReportsPanel.vue`
5. Drag-and-drop family-to-accommodation (HTML5 native or PrimeVue OrderList) is optional — only implement if it can be done without a new DnD library

---

## Implementation Verification

- **TypeScript:** `npm run build` passes with zero errors (no `any`, `<script setup lang="ts">` everywhere)
- **Functionality:** Assignment board renders, individual assign/unassign work, auto-assign fills families
- **Testing:** Vitest unit tests cover composable methods and slot card signal logic
- **Integration:** Composable hits real backend endpoints (verify in browser network tab during dev)
- **Documentation:** Updated if any new patterns introduced

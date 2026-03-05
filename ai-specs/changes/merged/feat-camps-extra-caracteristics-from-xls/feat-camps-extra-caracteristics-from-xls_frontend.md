# Frontend Implementation Plan: feat-camps-extra-caracteristics-from-xls — Import Extra Camp Characteristics

> **Scope note**: The backend spec marks this as "Backend only (no frontend changes in this phase)". This plan covers the **follow-up UI phase** that exposes the new backend capabilities to users. Implement AFTER the backend plan (`feat-camps-extra-caracteristics-from-xls_backend.md`) is merged and deployed.

---

## 2. Overview

Expose the 13 new `Camp` fields, `CampObservation`, `CampAuditLog`, and the CSV import endpoint through the Admin/Board UI.

Key work:

- **Type updates**: Rename `location` → `rawAddress`; add new fields to `Camp`, `AccommodationCapacity`, `UpdateCampRequest`; add `CampObservation` and `CampAuditLog` types
- **Composable updates**: `useCamps` gains 4 new API operations
- **Form update**: `CampLocationForm` gains a new "ABUVI tracking" section and the rename
- **Detail page update**: `CampLocationDetailPage` gains three new sections (Contact Info, Observations, Audit Log)
- **AccommodationCapacity** display/form gains 11 new fields (descriptions + facility flags)
- **Admin CSV import**: New `CampCsvImportDialog` + import button on `CampLocationsPage`

Architecture: Vue 3 `<script setup lang="ts">`, PrimeVue + Tailwind CSS, composable-driven API calls, Pinia auth store for role-gating (Admin vs Board).

---

## 3. Architecture Context

### Files to create

| File | Purpose |
|------|---------|
| `src/components/camps/CampObservationsSection.vue` | List + add observation form (Board+) |
| `src/components/camps/CampAuditLogSection.vue` | Read-only audit log DataTable (Admin only) |
| `src/components/camps/CampAbuviTrackingForm.vue` | ABUVI tracking fields section (Board+) |
| `src/components/camps/CampCsvImportDialog.vue` | File upload dialog for CSV import (Admin only) |

### Files to modify

| File | Change |
|------|--------|
| `src/types/camp.ts` | Rename `location` → `rawAddress`; add 13 new fields; add `CampObservation`, `CampAuditLog`, `AddCampObservationRequest`, `CampAuditLogResponse` |
| `src/composables/useCamps.ts` | Add `fetchCampObservations`, `addCampObservation`, `fetchCampAuditLog`, `importCampsCsv` |
| `src/components/camps/CampLocationForm.vue` | Rename `location` → `rawAddress`; add new field groups |
| `src/components/camps/AccommodationCapacityForm.vue` | Add 11 new fields (descriptions + flags) |
| `src/components/camps/AccommodationCapacityDisplay.vue` | Display 11 new fields |
| `src/views/camps/CampLocationDetailPage.vue` | Add Contact Info, Observations, Audit Log sections |
| `src/views/camps/CampLocationsPage.vue` | Add "Import CSV" button (Admin only) |

### Routing

No new routes needed. Observations and Audit Log are sections within `CampLocationDetailPage`.

### State management

- Composable-local state (`ref`, `reactive`) for all new data
- `useAuthStore().isAdmin` / `isBoard` for conditional rendering of Admin-only sections
- No new Pinia store needed

---

## 4. Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch**: `feature/feat-camps-extra-caracteristics-from-xls-frontend`
- **Implementation Steps**:
  1. `git checkout main && git pull origin main`
  2. `git checkout -b feature/feat-camps-extra-caracteristics-from-xls-frontend`
  3. `git branch` — verify branch

---

### Step 1: Update TypeScript Types — `src/types/camp.ts`

- **File**: `src/types/camp.ts`
- **Action**: Rename `location` → `rawAddress` and add all new fields

#### 1a — Update `Camp` interface

```typescript
export interface Camp {
  id: string
  name: string
  description: string | null
  rawAddress: string | null          // renamed from location
  latitude: number | null
  longitude: number | null
  googlePlaceId: string | null
  formattedAddress: string | null
  streetAddress: string | null
  locality: string | null
  administrativeArea: string | null
  postalCode: string | null
  country: string | null
  phoneNumber: string | null
  nationalPhoneNumber: string | null
  websiteUrl: string | null
  googleMapsUrl: string | null
  googleRating: number | null
  googleRatingCount: number | null
  businessStatus: string | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  isActive: boolean
  accommodationCapacity?: AccommodationCapacity | null
  createdAt: string
  updatedAt: string

  // New fields
  province: string | null
  contactEmail: string | null
  contactPerson: string | null
  contactCompany: string | null
  secondaryWebsiteUrl: string | null
  vatIncluded: boolean | null
  externalSourceId: number | null
  abuviManagedByUserId: string | null
  abuviManagedByUserName: string | null
  abuviContactedAt: string | null
  abuviPossibility: string | null
  abuviLastVisited: string | null
  abuviHasDataErrors: boolean | null
  lastModifiedByUserId: string | null
  observations: CampObservation[]
}
```

#### 1b — Update `AccommodationCapacity` interface

```typescript
export interface AccommodationCapacity {
  // Existing fields
  privateRoomsWithBathroom?: number | null
  privateRoomsSharedBathroom?: number | null
  sharedRooms?: SharedRoomInfo[] | null
  bungalows?: number | null
  campOwnedTents?: number | null
  memberTentAreaSquareMeters?: number | null
  memberTentCapacityEstimate?: number | null
  motorhomeSpots?: number | null
  notes?: string | null

  // New fields from CSV
  totalCapacity?: number | null
  roomsDescription?: string | null
  bungalowsDescription?: string | null
  tentsDescription?: string | null
  tentAreaDescription?: string | null
  parkingSpots?: number | null
  hasAdaptedMenu?: boolean | null
  hasEnclosedDiningRoom?: boolean | null
  hasSwimmingPool?: boolean | null
  hasSportsCourt?: boolean | null
  hasForestArea?: boolean | null
}
```

#### 1c — Update `UpdateCampRequest` type

```typescript
export interface UpdateCampRequest {
  name: string
  description: string | null
  rawAddress: string | null         // renamed from location
  latitude: number | null
  longitude: number | null
  googlePlaceId: string | null
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  isActive: boolean
  accommodationCapacity?: AccommodationCapacity | null

  // New writable fields
  province?: string | null
  contactEmail?: string | null
  contactPerson?: string | null
  contactCompany?: string | null
  secondaryWebsiteUrl?: string | null
  vatIncluded?: boolean | null
  abuviManagedByUserId?: string | null
  abuviContactedAt?: string | null
  abuviPossibility?: string | null
  abuviLastVisited?: string | null
  abuviHasDataErrors?: boolean | null
}
```

#### 1d — Add new types

```typescript
export interface CampObservation {
  id: string
  campId: string
  text: string
  season: string | null
  createdByUserId: string | null
  createdAt: string
}

export interface CampAuditLogEntry {
  id: string
  fieldName: string
  oldValue: string | null
  newValue: string | null
  changedByUserId: string
  changedAt: string
}

export interface AddCampObservationRequest {
  text: string
  season: string | null
}

export interface CampImportResult {
  created: number
  updated: number
  skipped: number
  rows: CampImportRowResult[]
}

export interface CampImportRowResult {
  rowNumber: number
  campName: string | null
  status: 'Created' | 'Updated' | 'Skipped' | 'Error'
  message: string | null
  gestionPor: string | null
}
```

---

### Step 2: Update `useCamps.ts` Composable

- **File**: `src/composables/useCamps.ts`
- **Action**:
  1. Rename `location` → `rawAddress` in `updateCamp()` request building
  2. Add `fetchCampObservations(campId)` — GET `/api/camps/{campId}/observations`
  3. Add `addCampObservation(campId, request)` — POST `/api/camps/{campId}/observations`
  4. Add `fetchCampAuditLog(campId)` — GET `/api/camps/{campId}/audit-log`
  5. Add `importCampsCsv(file)` — POST `/api/admin/camps/import-csv` (multipart)

```typescript
// Observations state
const campObservations = ref<CampObservation[]>([])
const observationsLoading = ref(false)
const observationsError = ref<string | null>(null)

// Audit log state
const campAuditLog = ref<CampAuditLogEntry[]>([])
const auditLogLoading = ref(false)
const auditLogError = ref<string | null>(null)

// Import state
const importLoading = ref(false)
const importError = ref<string | null>(null)

const fetchCampObservations = async (campId: string) => {
  observationsLoading.value = true
  observationsError.value = null
  try {
    const res = await api.get<ApiResponse<CampObservation[]>>(
      `/camps/${campId}/observations`
    )
    if (res.data.success && res.data.data) {
      campObservations.value = res.data.data
    }
  } catch (err: unknown) {
    observationsError.value = extractErrorMessage(err) || 'Error al cargar las observaciones'
  } finally {
    observationsLoading.value = false
  }
}

const addCampObservation = async (
  campId: string,
  request: AddCampObservationRequest
): Promise<CampObservation | null> => {
  observationsLoading.value = true
  observationsError.value = null
  try {
    const res = await api.post<ApiResponse<CampObservation>>(
      `/camps/${campId}/observations`,
      request
    )
    if (res.data.success && res.data.data) {
      campObservations.value.unshift(res.data.data)
      return res.data.data
    }
    return null
  } catch (err: unknown) {
    observationsError.value = extractErrorMessage(err) || 'Error al añadir la observación'
    return null
  } finally {
    observationsLoading.value = false
  }
}

const fetchCampAuditLog = async (campId: string) => {
  auditLogLoading.value = true
  auditLogError.value = null
  try {
    const res = await api.get<ApiResponse<CampAuditLogEntry[]>>(
      `/camps/${campId}/audit-log`
    )
    if (res.data.success && res.data.data) {
      campAuditLog.value = res.data.data
    }
  } catch (err: unknown) {
    auditLogError.value = extractErrorMessage(err) || 'Error al cargar el registro de auditoría'
  } finally {
    auditLogLoading.value = false
  }
}

const importCampsCsv = async (file: File): Promise<CampImportResult | null> => {
  importLoading.value = true
  importError.value = null
  try {
    const formData = new FormData()
    formData.append('file', file)
    const res = await api.post<ApiResponse<CampImportResult>>(
      '/admin/camps/import-csv',
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    )
    if (res.data.success && res.data.data) {
      return res.data.data
    }
    return null
  } catch (err: unknown) {
    importError.value = extractErrorMessage(err) || 'Error al importar el fichero CSV'
    return null
  } finally {
    importLoading.value = false
  }
}
```

**Return new state and functions** alongside existing ones.

**Implementation Note**: Any existing reference to `camp.location` in `useCamps.ts` (e.g., when building `UpdateCampRequest`) must be renamed to `camp.rawAddress`.

---

### Step 3: Update `CampLocationForm.vue`

- **File**: `src/components/camps/CampLocationForm.vue`
- **Action**: Rename `location` field, add three new field groups

#### 3a — Rename `location` → `rawAddress`

In the form model:

```typescript
// BEFORE
form.location = camp?.location ?? null

// AFTER
form.rawAddress = camp?.rawAddress ?? null
```

Update the template label from `"Ubicación"` / `"Dirección"` to `"Dirección (referencia)"` and the `v-model` binding from `form.location` to `form.rawAddress`.

#### 3b — Add "Información de contacto" section

After the existing contact fields (phone, website), add:

```
ContactPerson      (InputText) — "Persona de contacto"
ContactCompany     (InputText) — "Empresa / Organización"
ContactEmail       (InputText, type=email) — "Email de contacto"
SecondaryWebsiteUrl (InputText) — "Web secundaria"
Province           (InputText) — "Provincia"
```

#### 3c — Add "Precio y fiscalidad" subsection

```
PricePerAdult  (InputNumber, mode=currency) — already exists
PricePerChild  (InputNumber, mode=currency) — already exists
PricePerBaby   (InputNumber, mode=currency) — already exists
VatIncluded    (Select: Sí / No / Desconocido → true/false/null) — "IVA incluido"
```

#### 3d — Embed `CampAbuviTrackingForm` (Board+ only)

Import `CampAbuviTrackingForm.vue` as a child component and conditionally render:

```vue
<CampAbuviTrackingForm
  v-if="auth.isBoard"
  v-model="form.abuviTracking"
/>
```

Pass the ABUVI tracking fields as a sub-object to keep the form clean.

---

### Step 4: Create `CampAbuviTrackingForm.vue`

- **File**: `src/components/camps/CampAbuviTrackingForm.vue`
- **Purpose**: Collapsible panel with all ABUVI internal tracking fields (Board+ only)
- **Fields**:

  ```
  ExternalSourceId    (InputNumber, integer) — "ID externo (N° hoja de cálculo)"
  AbuviContactedAt    (InputText) — "Contactado (texto libre)"
  AbuviPossibility    (InputText) — "Posibilidad"
  AbuviLastVisited    (InputText) — "Última visita ABUVI"
  AbuviHasDataErrors  (ToggleSwitch) — "¿Datos erróneos?"
  AbuviManagedByUserId (Select, options loaded from /api/users?role=Board) — "Responsable ABUVI"
  ```

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'
import Panel from 'primevue/panel'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import ToggleSwitch from 'primevue/toggleswitch'
import Select from 'primevue/select'

// Model
const props = defineProps<{
  modelValue: AbuviTrackingFields
  boardUsers: BoardUserOption[]   // passed from parent (fetched once)
}>()
const emit = defineEmits<{
  'update:modelValue': [value: AbuviTrackingFields]
}>()

const form = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})
</script>

<template>
  <Panel header="Seguimiento interno ABUVI" :toggleable="true" :collapsed="true">
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <!-- fields here -->
    </div>
  </Panel>
</template>
```

- **Implementation Note**: Board users list for the `AbuviManagedByUserId` select should be loaded once in the parent component (not inside this sub-form) to avoid repeated API calls. Pass as a prop.

---

### Step 5: Update `AccommodationCapacityForm.vue` and `AccommodationCapacityDisplay.vue`

#### 5a — `AccommodationCapacityForm.vue`

Add two new groups after existing fields:

**"Capacidad (referencia CSV)"** group:

```
TotalCapacity         (InputNumber) — "Total plazas"
RoomsDescription      (InputText)   — "Habitaciones (descripción)"
BungalowsDescription  (InputText)   — "Cabañas (descripción)"
TentsDescription      (InputText)   — "Tiendas (descripción)"
TentAreaDescription   (InputText)   — "Campa para tiendas"
ParkingSpots          (InputNumber) — "Plazas de aparcamiento"
```

**"Instalaciones"** group (ToggleSwitches in a 3-column grid):

```
HasAdaptedMenu        — "Menú adaptado"
HasEnclosedDiningRoom — "Comedor cerrado"
HasSwimmingPool       — "Piscina"
HasSportsCourt        — "Pista polideportiva"
HasForestArea         — "Pinar / zona natural"
```

#### 5b — `AccommodationCapacityDisplay.vue`

Mirror the form structure: add a "Capacidad (referencia)" row and an "Instalaciones" row with facility chips/badges.

Use a `v-if` guard on each field so they only show when non-null.

---

### Step 6: Update `CampLocationDetailPage.vue`

- **File**: `src/views/camps/CampLocationDetailPage.vue`
- **Action**: Add three new sections to the detail page

#### 6a — Contact info card

Add a new card after the main location card displaying:

```
Province, ContactPerson, ContactCompany, ContactEmail, SecondaryWebsiteUrl
```

Only show card if at least one contact field is non-null.

#### 6b — ABUVI tracking card

Add a collapsible card (Board+ only):

```
ExternalSourceId, AbuviContactedAt, AbuviPossibility, AbuviLastVisited,
AbuviHasDataErrors, AbuviManagedByUserName, LastModifiedByUserId
```

Show as a simple key-value list. Only `isBoard` users see this card.

#### 6c — Pricing card update

Add `VatIncluded` label next to existing pricing display:

```
Precio adulto: €XX  (IVA incluido / + IVA / -)
Precio niño: €XX
```

#### 6d — Add `CampObservationsSection`

Import and place after the detail cards (Board+ only):

```vue
<CampObservationsSection
  v-if="auth.isBoard"
  :camp-id="campId"
/>
```

#### 6e — Add `CampAuditLogSection`

Place after observations (Admin only):

```vue
<CampAuditLogSection
  v-if="auth.isAdmin"
  :camp-id="campId"
/>
```

---

### Step 7: Create `CampObservationsSection.vue`

- **File**: `src/components/camps/CampObservationsSection.vue`
- **Purpose**: List all observations (newest first) + add new observation form (Board+)

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import Textarea from 'primevue/textarea'
import Select from 'primevue/select'
import Button from 'primevue/button'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import { useCamps } from '@/composables/useCamps'

const props = defineProps<{ campId: string }>()

const { campObservations, observationsLoading, observationsError,
        fetchCampObservations, addCampObservation } = useCamps()

// New observation form state
const newText = ref('')
const newSeason = ref<string | null>(null)
const seasonOptions = [
  { label: '2023', value: '2023' },
  { label: '2024', value: '2024' },
  { label: '2025', value: '2025' },
  { label: '2025/2026', value: '2025/2026' },
  { label: 'Sin temporada', value: null },
]
const formError = ref<string | null>(null)
const submitting = ref(false)

onMounted(() => fetchCampObservations(props.campId))

const handleAdd = async () => {
  if (!newText.value.trim()) {
    formError.value = 'El texto no puede estar vacío'
    return
  }
  submitting.value = true
  formError.value = null
  const result = await addCampObservation(props.campId, {
    text: newText.value.trim(),
    season: newSeason.value,
  })
  if (result) {
    newText.value = ''
    newSeason.value = null
  }
  submitting.value = false
}
</script>

<template>
  <div class="space-y-4">
    <h3 class="text-lg font-semibold text-gray-800">Observaciones</h3>

    <!-- Add form -->
    <div class="rounded-lg border border-gray-200 bg-gray-50 p-4 space-y-3">
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-4">
        <div class="sm:col-span-3">
          <Textarea
            v-model="newText"
            rows="2"
            placeholder="Añadir observación..."
            class="w-full"
          />
        </div>
        <div>
          <Select
            v-model="newSeason"
            :options="seasonOptions"
            option-label="label"
            option-value="value"
            placeholder="Temporada"
            class="w-full"
          />
        </div>
      </div>
      <Message v-if="formError" severity="error" :closable="false" class="text-sm">
        {{ formError }}
      </Message>
      <div class="flex justify-end">
        <Button
          label="Añadir"
          icon="pi pi-plus"
          size="small"
          :loading="submitting"
          @click="handleAdd"
        />
      </div>
    </div>

    <!-- Loading/error states -->
    <div v-if="observationsLoading" class="flex justify-center py-4">
      <ProgressSpinner style="width: 32px; height: 32px" />
    </div>
    <Message v-else-if="observationsError" severity="error" :closable="false">
      {{ observationsError }}
    </Message>

    <!-- List -->
    <div v-else-if="campObservations.length === 0" class="text-sm text-gray-500">
      No hay observaciones registradas.
    </div>
    <div v-else class="space-y-2">
      <div
        v-for="obs in campObservations"
        :key="obs.id"
        class="rounded-md border border-gray-100 bg-white p-3 text-sm"
      >
        <div class="flex items-start justify-between gap-2">
          <p class="text-gray-800 whitespace-pre-wrap">{{ obs.text }}</p>
          <span
            v-if="obs.season"
            class="shrink-0 rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700"
          >
            {{ obs.season }}
          </span>
        </div>
        <p class="mt-1 text-xs text-gray-400">
          {{ obs.createdByUserId ? 'Manual' : 'Importado CSV' }} ·
          {{ new Date(obs.createdAt).toLocaleDateString('es-ES') }}
        </p>
      </div>
    </div>
  </div>
</template>
```

---

### Step 8: Create `CampAuditLogSection.vue`

- **File**: `src/components/camps/CampAuditLogSection.vue`
- **Purpose**: Read-only DataTable of field-level changes (Admin only)

```vue
<script setup lang="ts">
import { onMounted } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import { useCamps } from '@/composables/useCamps'

const props = defineProps<{ campId: string }>()

const { campAuditLog, auditLogLoading, auditLogError, fetchCampAuditLog } = useCamps()

onMounted(() => fetchCampAuditLog(props.campId))
</script>

<template>
  <div class="space-y-3">
    <h3 class="text-lg font-semibold text-gray-800">Registro de cambios</h3>

    <DataTable
      :value="campAuditLog"
      :loading="auditLogLoading"
      paginator
      :rows="10"
      size="small"
      class="text-sm"
    >
      <Column field="changedAt" header="Fecha" sortable>
        <template #body="{ data }">
          {{ new Date(data.changedAt).toLocaleString('es-ES') }}
        </template>
      </Column>
      <Column field="fieldName" header="Campo" sortable />
      <Column header="Valor anterior">
        <template #body="{ data }">
          <span v-if="data.oldValue" class="text-gray-600">{{ data.oldValue }}</span>
          <span v-else class="text-gray-400 italic">—</span>
        </template>
      </Column>
      <Column header="Nuevo valor">
        <template #body="{ data }">
          <Tag
            v-if="data.newValue"
            :value="data.newValue"
            severity="info"
            class="text-xs"
          />
          <span v-else class="text-gray-400 italic">—</span>
        </template>
      </Column>
      <Column field="changedByUserId" header="Usuario" />
    </DataTable>
  </div>
</template>
```

---

### Step 9: Create `CampCsvImportDialog.vue` + Add to `CampLocationsPage.vue`

#### 9a — `CampCsvImportDialog.vue`

- **File**: `src/components/camps/CampCsvImportDialog.vue`

```vue
<script setup lang="ts">
import { ref } from 'vue'
import Dialog from 'primevue/dialog'
import FileUpload from 'primevue/fileupload'
import Button from 'primevue/button'
import Message from 'primevue/message'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Tag from 'primevue/tag'
import { useCamps } from '@/composables/useCamps'
import type { CampImportResult } from '@/types/camp'

const props = defineProps<{ visible: boolean }>()
const emit = defineEmits<{
  'update:visible': [value: boolean]
  'imported': []
}>()

const { importCampsCsv, importLoading, importError } = useCamps()

const selectedFile = ref<File | null>(null)
const result = ref<CampImportResult | null>(null)

const handleFileSelect = (event: { files: File[] }) => {
  selectedFile.value = event.files[0] ?? null
}

const handleImport = async () => {
  if (!selectedFile.value) return
  result.value = null
  const res = await importCampsCsv(selectedFile.value)
  if (res) {
    result.value = res
    emit('imported')
  }
}

const handleClose = () => {
  selectedFile.value = null
  result.value = null
  emit('update:visible', false)
}
</script>

<template>
  <Dialog
    :visible="visible"
    header="Importar campamentos desde CSV"
    modal
    class="w-full max-w-2xl"
    @update:visible="handleClose"
  >
    <div class="space-y-4">
      <Message severity="info" :closable="false" class="text-sm">
        Selecciona el fichero <strong>CAMPAMENTOS.csv</strong> (codificación Windows-1252,
        separado por punto y coma). Tamaño máximo: 1 MB.
      </Message>

      <!-- File picker -->
      <div v-if="!result">
        <FileUpload
          mode="basic"
          accept=".csv"
          :max-file-size="1048576"
          choose-label="Seleccionar CSV"
          :auto="false"
          @select="handleFileSelect"
        />
        <p v-if="selectedFile" class="mt-2 text-sm text-gray-600">
          Fichero seleccionado: {{ selectedFile.name }}
        </p>
      </div>

      <!-- Error message -->
      <Message v-if="importError" severity="error" :closable="false">
        {{ importError }}
      </Message>

      <!-- Import result summary -->
      <div v-if="result" class="rounded-md bg-green-50 p-4 text-sm space-y-2">
        <p class="font-medium text-green-800">Importación completada</p>
        <div class="flex gap-4">
          <span class="text-gray-700">Creados: <strong>{{ result.created }}</strong></span>
          <span class="text-gray-700">Actualizados: <strong>{{ result.updated }}</strong></span>
          <span class="text-gray-700">Omitidos: <strong>{{ result.skipped }}</strong></span>
        </div>
      </div>

      <!-- Row results with gestionPor warnings -->
      <DataTable
        v-if="result"
        :value="result.rows.filter(r => r.status === 'Error' || r.gestionPor)"
        size="small"
        class="text-xs"
      >
        <Column field="rowNumber" header="Fila" />
        <Column field="campName" header="Campamento" />
        <Column field="status" header="Estado">
          <template #body="{ data }">
            <Tag
              :value="data.status"
              :severity="data.status === 'Error' ? 'danger' : 'warn'"
            />
          </template>
        </Column>
        <Column header="Nota">
          <template #body="{ data }">
            <span v-if="data.message" class="text-red-600">{{ data.message }}</span>
            <span v-else-if="data.gestionPor" class="text-amber-600">
              Gestión por "{{ data.gestionPor }}" — asignar manualmente
            </span>
          </template>
        </Column>
      </DataTable>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cerrar" text @click="handleClose" />
        <Button
          v-if="!result"
          label="Importar"
          icon="pi pi-upload"
          :disabled="!selectedFile"
          :loading="importLoading"
          @click="handleImport"
        />
      </div>
    </template>
  </Dialog>
</template>
```

#### 9b — Add import button to `CampLocationsPage.vue`

In the page header actions area, add (Admin only):

```vue
<Button
  v-if="auth.isAdmin"
  label="Importar CSV"
  icon="pi pi-upload"
  severity="secondary"
  size="small"
  @click="showImportDialog = true"
/>

<CampCsvImportDialog
  v-if="auth.isAdmin"
  v-model:visible="showImportDialog"
  @imported="fetchCamps()"
/>
```

---

### Step 10: Write Vitest Unit Tests

- **Files to create**:
  - `src/composables/__tests__/useCamps.test.ts` — test 4 new methods
  - `src/components/camps/__tests__/CampObservationsSection.test.ts`
  - `src/components/camps/__tests__/CampCsvImportDialog.test.ts`

**Test approach** (mirror existing test patterns):

```typescript
// useCamps.test.ts — example for new methods
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCamps } from '../useCamps'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('useCamps - fetchCampObservations', () => {
  it('sets campObservations on success', async () => {
    const mockData = [{ id: '1', text: 'Test', season: '2024', ... }]
    vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: mockData } })

    const { fetchCampObservations, campObservations } = useCamps()
    await fetchCampObservations('camp-id')

    expect(campObservations.value).toEqual(mockData)
  })

  it('sets error on failure', async () => {
    vi.mocked(api.get).mockRejectedValue({ response: { data: { error: { message: 'Error' } } } })

    const { fetchCampObservations, observationsError } = useCamps()
    await fetchCampObservations('camp-id')

    expect(observationsError.value).toBe('Error')
  })
})

describe('useCamps - importCampsCsv', () => {
  it('sends multipart form data', async () => { ... })
  it('returns result on success', async () => { ... })
  it('sets importError on failure', async () => { ... })
})
```

---

### Step 11: Update Technical Documentation

- **Action**: Update affected documentation files
- **Implementation Steps**:
  1. `ai-specs/specs/api-spec.yml` — add 4 new endpoint definitions (already done in backend plan; verify frontend paths match)
  2. No frontend-standards changes needed (no new patterns introduced)

---

## 5. Implementation Order

1. **Step 0** — Create branch `feature/feat-camps-extra-caracteristics-from-xls-frontend`
2. **Step 1** — Update TypeScript types (`camp.ts`) — the foundation everything else depends on
3. **Step 2** — Update `useCamps.ts` composable (add 4 new API operations, rename `location` → `rawAddress`)
4. **Step 3** — Update `CampLocationForm.vue` (rename field + new sections)
5. **Step 4** — Create `CampAbuviTrackingForm.vue`
6. **Step 5** — Update `AccommodationCapacityForm.vue` + `AccommodationCapacityDisplay.vue`
7. **Step 6** — Update `CampLocationDetailPage.vue` (add new sections)
8. **Step 7** — Create `CampObservationsSection.vue`
9. **Step 8** — Create `CampAuditLogSection.vue`
10. **Step 9** — Create `CampCsvImportDialog.vue` + add button to `CampLocationsPage.vue`
11. **Step 10** — Write Vitest unit tests
12. **Step 11** — Update technical documentation

---

## 6. Testing Checklist

- [ ] `useCamps` composable: `fetchCampObservations`, `addCampObservation`, `fetchCampAuditLog`, `importCampsCsv` each tested (success + error paths)
- [ ] `CampObservationsSection`: renders list, add form validates empty text, submits correctly
- [ ] `CampCsvImportDialog`: shows summary on success, shows row errors and `gestionPor` warnings
- [ ] `CampLocationForm`: `rawAddress` field sends correct field name in request; new fields included in request
- [ ] All new components render correctly with `null` data (guard all new nullable fields with `v-if`)
- [ ] Admin-only sections (`CampAuditLogSection`, Import button) hidden for Board users
- [ ] Board-only sections (`CampObservationsSection`, ABUVI tracking) hidden for Members

---

## 7. Error Handling Patterns

- All composable methods follow the `loading / error / data` ref pattern (already standard in `useCamps`)
- Form validation errors displayed inline (below the field) with `text-xs text-red-600`
- API errors displayed via `<Message severity="error">` above the form actions
- For the CSV import, row-level errors/warnings shown in a results DataTable after import

---

## 8. UI/UX Considerations

- **`CampAbuviTrackingForm`**: Wrapped in a `Panel` (collapsed by default) — ABUVI tracking is rarely edited; no need to show it expanded
- **Observations**: Newest-first order (matches backend); season badge in blue pill; "Importado CSV" vs "Manual" source indicator
- **Audit log**: Read-only DataTable; old/new values side by side; paginated (10 per page)
- **AccommodationCapacity facility flags**: Display as yes/no chips rather than checkboxes for readability
- **CSV import dialog**: Show only errors + `gestionPor` warnings in the result table (don't show all rows — could be 200+); summary counts give the big picture
- **`location` → `rawAddress` rename**: The form label changes from `"Dirección"` (or similar) to `"Dirección (referencia)"` to signal it's legacy/raw data
- All text, labels, placeholders, and messages in **Spanish** (consistent with the rest of the app)

---

## 9. Dependencies

No new npm packages needed. All components use:

- PrimeVue (already installed): `Dialog`, `DataTable`, `Column`, `Textarea`, `Select`, `FileUpload`, `Tag`, `Panel`, `ProgressSpinner`, `Message`, `Button`, `ToggleSwitch`, `InputText`, `InputNumber`
- Vue 3 Composition API (`ref`, `reactive`, `computed`, `watch`, `onMounted`)
- `@/composables/useCamps` (updated in this ticket)
- `@/stores/auth` (for role-gating)

---

## 10. Notes

- **`location` → `rawAddress` rename**: This is the most impactful breaking change. Search the entire codebase for `camp.location`, `form.location`, `request.location`, `"location"` in JSON serialization, and template bindings. Use `grep -r "\.location" frontend/src/` before starting.
- **Board users list for `AbuviManagedByUserId`**: Fetch from `GET /api/users?role=Board` (or equivalent). Check what endpoint exists for fetching users by role. This list should be fetched once in `CampLocationForm.vue` and passed as a prop to `CampAbuviTrackingForm`.
- **`AbuviHasDataErrors` ToggleSwitch**: On the form, show a warning badge or color change when this is `true`, so it's visually prominent.
- **Null guards**: Every new field is nullable — always use `v-if` or `??` before rendering to avoid template crashes.
- **TypeScript strict**: No `any` types. `CampAuditLogEntry`, `CampObservation` etc. must be fully typed.
- **Import file encoding**: The UI doesn't need to handle encoding — the backend handles Windows-1252 decoding. The frontend just sends the raw binary.
- **`gestionPor` follow-up UX**: After import, camps with non-empty `gestionPor` need manual assignment of `abuviManagedByUserId`. The import result dialog surfaces these. Consider adding a tooltip or help text in the ABUVI tracking form explaining the field purpose.

---

## 11. Next Steps After Implementation

1. QA the CSV import end-to-end with the real `CAMPAMENTOS.csv` file
2. Verify that `gestionPor` warnings in the import results correctly identify the camps needing manual assignment
3. Test observations flow: import creates observations → Board user sees them → adds manual observation → appears at top
4. Test audit log: update a camp's `PricePerAdult` → verify audit entry appears in the section

---

## 12. Implementation Verification

- [ ] **Code Quality**: `<script setup lang="ts">` on every new component; no `any` types; all new props typed with interfaces
- [ ] **`location` rename**: Zero remaining references to `camp.location` / `form.location` / `request.location` in the codebase
- [ ] **Functionality**: All 4 new composable methods call correct API endpoints; forms include new fields in request body
- [ ] **Role gating**: `CampAuditLogSection` and Import button only visible to Admin; `CampObservationsSection` and ABUVI section visible to Board+
- [ ] **Testing**: Vitest tests for composable new methods + key components
- [ ] **Documentation**: `api-spec.yml` reflects new endpoints

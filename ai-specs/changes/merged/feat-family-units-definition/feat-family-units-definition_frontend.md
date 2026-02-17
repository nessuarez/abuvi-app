# Frontend Implementation Plan: Family Units and Members Definition

## Overview

This implementation adds comprehensive family unit management functionality to the ABUVI frontend. Users can create one family unit, act as its representative, and manage family member profiles within it. This feature prepares the foundation for future camp registration where families register together.

**Architecture Principles:**
- **Vue 3 Composition API** with `<script setup lang="ts">` (mandatory)
- **Composables pattern** for API communication (`useFamilyUnits`)
- **PrimeVue components** for rich UI (DataTable, Dialog, Calendar, Button, InputText)
- **Tailwind CSS** for layout and spacing (no custom `<style>` blocks)
- **TypeScript strict mode** for type safety
- **Responsive design** with mobile-first approach

**Key Business Rules:**
- One family unit per user
- User who creates family unit becomes its representative
- Representative auto-created as first family member
- Medical notes and allergies **never displayed** (boolean flags only)
- Only representative OR Admin/Board can view/modify family data

---

## Architecture Context

### Components/Composables Involved

**New Composables:**
- `frontend/src/composables/useFamilyUnits.ts` - API communication for family units and members

**New Components:**
- `frontend/src/components/family-units/FamilyUnitCard.vue` - Display family unit summary
- `frontend/src/components/family-units/FamilyUnitForm.vue` - Create/Edit family unit
- `frontend/src/components/family-units/FamilyMemberList.vue` - List of family members with DataTable
- `frontend/src/components/family-units/FamilyMemberForm.vue` - Create/Edit family member dialog
- `frontend/src/components/family-units/FamilyMemberCard.vue` - Display single family member

**New Views:**
- `frontend/src/views/FamilyUnitPage.vue` - Main family unit management page

**New Types:**
- `frontend/src/types/family-unit.ts` - TypeScript interfaces for family units and members

### Routing Considerations

**New Routes:**
- `/family-unit` - Main family unit management page (Member role required, authenticated)

**Route Guards:**
- Authentication required (JWT token)
- Email verification required
- Member, Board, or Admin role required

### State Management Approach

**Local State (Component-level):**
- Form data (family unit name, member details) - `ref()` in components
- UI state (dialog visibility, loading, errors) - `ref()` in components
- Current editing member - `ref()` in parent component

**Composable State:**
- Family unit data - `ref()` in `useFamilyUnits`
- Family members list - `ref()` in `useFamilyUnits`
- Loading/error states - `ref()` in `useFamilyUnits`

**No Pinia Store Needed:**
- Family unit data is user-specific and not shared globally
- Composable pattern sufficient for this feature

---

## Implementation Steps

### Step 0: Create Feature Branch

**Action:** Create and switch to frontend feature branch

**Branch Naming:** `feature/feat-family-units-definition-frontend`

**Implementation Steps:**
1. Check if backend branch `feature/feat-family-units-definition-backend` exists
2. Ensure you're on latest `main` branch: `git checkout main && git pull origin main`
3. Create frontend branch: `git checkout -b feature/feat-family-units-definition-frontend`
4. Verify branch creation: `git branch`

**Notes:**
- This must be the FIRST step before any code changes
- Separate frontend branch from backend to isolate concerns
- Follow git workflow from `frontend-standards.mdc`

---

### Step 1: Define TypeScript Interfaces

**File:** `frontend/src/types/family-unit.ts`

**Action:** Create TypeScript interfaces for Family Units and Members

**Implementation Steps:**

1. **Create new file** `frontend/src/types/family-unit.ts`

2. **Define FamilyRelationship enum**:

```typescript
export enum FamilyRelationship {
  Parent = 'Parent',
  Child = 'Child',
  Sibling = 'Sibling',
  Spouse = 'Spouse',
  Other = 'Other'
}
```

**IMPORTANT:** Do NOT include `Friend` in this enum. Friends/guests will be handled separately in a future feature.

3. **Define FamilyUnit interfaces**:

```typescript
// Response from backend
export interface FamilyUnitResponse {
  id: string
  name: string
  representativeUserId: string
  createdAt: string
  updatedAt: string
}

// Request to create family unit
export interface CreateFamilyUnitRequest {
  name: string
}

// Request to update family unit
export interface UpdateFamilyUnitRequest {
  name: string
}
```

4. **Define FamilyMember interfaces**:

```typescript
// Response from backend
export interface FamilyMemberResponse {
  id: string
  familyUnitId: string
  userId: string | null
  firstName: string
  lastName: string
  dateOfBirth: string  // ISO 8601 date string (YYYY-MM-DD)
  relationship: FamilyRelationship
  documentNumber: string | null
  email: string | null
  phone: string | null
  hasMedicalNotes: boolean    // NEVER show actual content
  hasAllergies: boolean        // NEVER show actual content
  createdAt: string
  updatedAt: string
}

// Request to create family member
export interface CreateFamilyMemberRequest {
  firstName: string
  lastName: string
  dateOfBirth: string  // ISO 8601 date string (YYYY-MM-DD)
  relationship: FamilyRelationship
  documentNumber?: string | null
  email?: string | null
  phone?: string | null
  medicalNotes?: string | null
  allergies?: string | null
}

// Request to update family member
export interface UpdateFamilyMemberRequest {
  firstName: string
  lastName: string
  dateOfBirth: string
  relationship: FamilyRelationship
  documentNumber?: string | null
  email?: string | null
  phone?: string | null
  medicalNotes?: string | null
  allergies?: string | null
}
```

5. **Define helper types**:

```typescript
// For displaying relationship in Spanish
export const FamilyRelationshipLabels: Record<FamilyRelationship, string> = {
  [FamilyRelationship.Parent]: 'Padre/Madre',
  [FamilyRelationship.Child]: 'Hijo/Hija',
  [FamilyRelationship.Sibling]: 'Hermano/Hermana',
  [FamilyRelationship.Spouse]: 'Cónyuge',
  [FamilyRelationship.Other]: 'Otro'
}
```

**Dependencies:**
- None (TypeScript built-in)

**Implementation Notes:**
- All dates as ISO 8601 strings (YYYY-MM-DD) for `dateOfBirth`
- Medical notes and allergies **never** in response types (only boolean flags)
- Relationship enum stored as strings in backend, use enum for type safety in frontend
- Spanish labels for UI display (as per backend Spanish error messages)

---

### Step 2: Create API Composable

**File:** `frontend/src/composables/useFamilyUnits.ts`

**Action:** Create composable for family unit and member API communication

**Implementation Steps:**

1. **Create new file** `frontend/src/composables/useFamilyUnits.ts`

2. **Implement composable**:

```typescript
import { ref } from 'vue'
import type { Ref } from 'vue'
import api from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  FamilyUnitResponse,
  CreateFamilyUnitRequest,
  UpdateFamilyUnitRequest,
  FamilyMemberResponse,
  CreateFamilyMemberRequest,
  UpdateFamilyMemberRequest
} from '@/types/family-unit'

export function useFamilyUnits() {
  // State
  const familyUnit: Ref<FamilyUnitResponse | null> = ref(null)
  const familyMembers: Ref<FamilyMemberResponse[]> = ref([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Family Unit Operations

  /**
   * Create a new family unit for the current user
   */
  const createFamilyUnit = async (request: CreateFamilyUnitRequest): Promise<FamilyUnitResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<FamilyUnitResponse>>('/api/family-units', request)
      familyUnit.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al crear la unidad familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Get current user's family unit
   */
  const getCurrentUserFamilyUnit = async (): Promise<FamilyUnitResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<FamilyUnitResponse>>('/api/family-units/me')
      familyUnit.value = response.data.data
      return response.data.data
    } catch (err: any) {
      if (err.response?.status === 404) {
        familyUnit.value = null
        return null
      }
      error.value = err.response?.data?.error?.message || 'Error al obtener la unidad familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Get family unit by ID (Admin/Board or representative)
   */
  const getFamilyUnitById = async (id: string): Promise<FamilyUnitResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<FamilyUnitResponse>>(`/api/family-units/${id}`)
      familyUnit.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener la unidad familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Update family unit name
   */
  const updateFamilyUnit = async (
    id: string,
    request: UpdateFamilyUnitRequest
  ): Promise<FamilyUnitResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.put<ApiResponse<FamilyUnitResponse>>(
        `/api/family-units/${id}`,
        request
      )
      familyUnit.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al actualizar la unidad familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Delete family unit (and all members)
   */
  const deleteFamilyUnit = async (id: string): Promise<boolean> => {
    loading.value = true
    error.value = null

    try {
      await api.delete(`/api/family-units/${id}`)
      familyUnit.value = null
      familyMembers.value = []
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al eliminar la unidad familiar'
      return false
    } finally {
      loading.value = false
    }
  }

  // Family Member Operations

  /**
   * Create a new family member
   */
  const createFamilyMember = async (
    familyUnitId: string,
    request: CreateFamilyMemberRequest
  ): Promise<FamilyMemberResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<FamilyMemberResponse>>(
        `/api/family-units/${familyUnitId}/members`,
        request
      )
      familyMembers.value.push(response.data.data)
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al crear el miembro familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Get all family members for a family unit
   */
  const getFamilyMembers = async (familyUnitId: string): Promise<FamilyMemberResponse[]> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<FamilyMemberResponse[]>>(
        `/api/family-units/${familyUnitId}/members`
      )
      familyMembers.value = response.data.data
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener los miembros familiares'
      return []
    } finally {
      loading.value = false
    }
  }

  /**
   * Get single family member by ID
   */
  const getFamilyMemberById = async (
    familyUnitId: string,
    memberId: string
  ): Promise<FamilyMemberResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<FamilyMemberResponse>>(
        `/api/family-units/${familyUnitId}/members/${memberId}`
      )
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener el miembro familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Update family member
   */
  const updateFamilyMember = async (
    familyUnitId: string,
    memberId: string,
    request: UpdateFamilyMemberRequest
  ): Promise<FamilyMemberResponse | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.put<ApiResponse<FamilyMemberResponse>>(
        `/api/family-units/${familyUnitId}/members/${memberId}`,
        request
      )

      // Update in local array
      const index = familyMembers.value.findIndex((m) => m.id === memberId)
      if (index !== -1) {
        familyMembers.value[index] = response.data.data
      }

      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al actualizar el miembro familiar'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Delete family member
   */
  const deleteFamilyMember = async (
    familyUnitId: string,
    memberId: string
  ): Promise<boolean> => {
    loading.value = true
    error.value = null

    try {
      await api.delete(`/api/family-units/${familyUnitId}/members/${memberId}`)

      // Remove from local array
      familyMembers.value = familyMembers.value.filter((m) => m.id !== memberId)

      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al eliminar el miembro familiar'
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    // State
    familyUnit,
    familyMembers,
    loading,
    error,

    // Family Unit Methods
    createFamilyUnit,
    getCurrentUserFamilyUnit,
    getFamilyUnitById,
    updateFamilyUnit,
    deleteFamilyUnit,

    // Family Member Methods
    createFamilyMember,
    getFamilyMembers,
    getFamilyMemberById,
    updateFamilyMember,
    deleteFamilyMember
  }
}
```

**Dependencies:**
- `@/utils/api` - Axios instance with interceptors
- `@/types/api` - ApiResponse envelope type
- `@/types/family-unit` - Family unit types (created in Step 1)

**Implementation Notes:**
- All API calls use the configured Axios instance from `utils/api.ts`
- Error messages in Spanish (matching backend error messages)
- Composable manages loading/error/data states
- Local array updates after create/update/delete for optimistic UI
- 404 on getCurrentUserFamilyUnit is expected (user has no family unit yet)

---

### Step 3: Create Family Unit Form Component

**File:** `frontend/src/components/family-units/FamilyUnitForm.vue`

**Action:** Create form component for creating/editing family unit

**Implementation Steps:**

1. **Create component file** `frontend/src/components/family-units/FamilyUnitForm.vue`

2. **Implement component**:

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import type { FamilyUnitResponse, CreateFamilyUnitRequest, UpdateFamilyUnitRequest } from '@/types/family-unit'

const props = defineProps<{
  familyUnit?: FamilyUnitResponse | null
  loading?: boolean
}>()

const emit = defineEmits<{
  submit: [request: CreateFamilyUnitRequest | UpdateFamilyUnitRequest]
  cancel: []
}>()

const toast = useToast()

// Form data
const name = ref(props.familyUnit?.name || '')

// Validation
const nameError = ref<string | null>(null)

const isValid = computed(() => {
  return name.value.trim().length > 0 && name.value.length <= 200
})

const isEditing = computed(() => !!props.familyUnit)

const validateName = () => {
  if (!name.value.trim()) {
    nameError.value = 'El nombre de la unidad familiar es obligatorio'
    return false
  }
  if (name.value.length > 200) {
    nameError.value = 'El nombre no puede exceder 200 caracteres'
    return false
  }
  nameError.value = null
  return true
}

const handleSubmit = () => {
  if (!validateName()) {
    toast.add({
      severity: 'error',
      summary: 'Validación',
      detail: 'Por favor revisa los datos ingresados',
      life: 3000
    })
    return
  }

  if (isEditing.value) {
    emit('submit', { name: name.value.trim() } as UpdateFamilyUnitRequest)
  } else {
    emit('submit', { name: name.value.trim() } as CreateFamilyUnitRequest)
  }
}

const handleCancel = () => {
  emit('cancel')
}
</script>

<template>
  <form @submit.prevent="handleSubmit" class="space-y-4">
    <div class="flex flex-col gap-2">
      <label for="family-unit-name" class="font-medium text-sm">
        Nombre de la Unidad Familiar <span class="text-red-500">*</span>
      </label>
      <InputText
        id="family-unit-name"
        v-model="name"
        placeholder="Ej: Familia García"
        :invalid="!!nameError"
        :disabled="loading"
        @blur="validateName"
        class="w-full"
      />
      <small v-if="nameError" class="text-red-500">{{ nameError }}</small>
      <small class="text-gray-500">Máximo 200 caracteres</small>
    </div>

    <div class="flex justify-end gap-2 pt-4">
      <Button
        type="button"
        label="Cancelar"
        severity="secondary"
        :disabled="loading"
        @click="handleCancel"
      />
      <Button
        type="submit"
        :label="isEditing ? 'Actualizar' : 'Crear'"
        :loading="loading"
        :disabled="!isValid || loading"
      />
    </div>
  </form>
</template>
```

**Dependencies:**
- PrimeVue: `InputText`, `Button`, `useToast`
- Types from `@/types/family-unit`

**Implementation Notes:**
- Validation on blur and submit
- Spanish labels and error messages
- Loading state disables inputs
- Emits `submit` and `cancel` events
- Parent component handles actual API call

---

### Step 4: Create Family Member Form Component

**File:** `frontend/src/components/family-units/FamilyMemberForm.vue`

**Action:** Create form component for creating/editing family members

**Implementation Steps:**

1. **Create component file** `frontend/src/components/family-units/FamilyMemberForm.vue`

2. **Implement component** (this is a large form with many fields):

```vue
<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import Calendar from 'primevue/calendar'
import Dropdown from 'primevue/dropdown'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import type {
  FamilyMemberResponse,
  CreateFamilyMemberRequest,
  UpdateFamilyMemberRequest,
  FamilyRelationship
} from '@/types/family-unit'
import { FamilyRelationshipLabels } from '@/types/family-unit'

const props = defineProps<{
  member?: FamilyMemberResponse | null
  loading?: boolean
}>()

const emit = defineEmits<{
  submit: [request: CreateFamilyMemberRequest | UpdateFamilyMemberRequest]
  cancel: []
}>()

const toast = useToast()

// Relationship options for dropdown
const relationshipOptions = Object.entries(FamilyRelationshipLabels).map(([value, label]) => ({
  label,
  value
}))

// Form data
const firstName = ref(props.member?.firstName || '')
const lastName = ref(props.member?.lastName || '')
const dateOfBirth = ref<Date | null>(props.member?.dateOfBirth ? new Date(props.member.dateOfBirth) : null)
const relationship = ref<FamilyRelationship | null>(props.member?.relationship || null)
const documentNumber = ref(props.member?.documentNumber || '')
const email = ref(props.member?.email || '')
const phone = ref(props.member?.phone || '')
const medicalNotes = ref('')  // Never pre-fill sensitive data
const allergies = ref('')      // Never pre-fill sensitive data

// Validation errors
const firstNameError = ref<string | null>(null)
const lastNameError = ref<string | null>(null)
const dateOfBirthError = ref<string | null>(null)
const relationshipError = ref<string | null>(null)
const documentNumberError = ref<string | null>(null)
const emailError = ref<string | null>(null)
const phoneError = ref<string | null>(null)

const isEditing = computed(() => !!props.member)

// Show sensitive data info for editing
const hasMedicalNotesInfo = computed(() => isEditing.value && props.member?.hasMedicalNotes)
const hasAllergiesInfo = computed(() => isEditing.value && props.member?.hasAllergies)

// Validation functions
const validateFirstName = () => {
  if (!firstName.value.trim()) {
    firstNameError.value = 'El nombre es obligatorio'
    return false
  }
  if (firstName.value.length > 100) {
    firstNameError.value = 'El nombre no puede exceder 100 caracteres'
    return false
  }
  firstNameError.value = null
  return true
}

const validateLastName = () => {
  if (!lastName.value.trim()) {
    lastNameError.value = 'Los apellidos son obligatorios'
    return false
  }
  if (lastName.value.length > 100) {
    lastNameError.value = 'Los apellidos no pueden exceder 100 caracteres'
    return false
  }
  lastNameError.value = null
  return true
}

const validateDateOfBirth = () => {
  if (!dateOfBirth.value) {
    dateOfBirthError.value = 'La fecha de nacimiento es obligatoria'
    return false
  }
  if (dateOfBirth.value > new Date()) {
    dateOfBirthError.value = 'La fecha de nacimiento debe ser una fecha pasada'
    return false
  }
  dateOfBirthError.value = null
  return true
}

const validateRelationship = () => {
  if (!relationship.value) {
    relationshipError.value = 'El tipo de relación es obligatorio'
    return false
  }
  relationshipError.value = null
  return true
}

const validateDocumentNumber = () => {
  if (documentNumber.value && documentNumber.value.trim()) {
    const uppercaseAlphanumeric = /^[A-Z0-9]+$/
    if (!uppercaseAlphanumeric.test(documentNumber.value)) {
      documentNumberError.value = 'El número de documento debe contener solo letras mayúsculas y números'
      return false
    }
    if (documentNumber.value.length > 50) {
      documentNumberError.value = 'El número de documento no puede exceder 50 caracteres'
      return false
    }
  }
  documentNumberError.value = null
  return true
}

const validateEmail = () => {
  if (email.value && email.value.trim()) {
    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailPattern.test(email.value)) {
      emailError.value = 'Formato de correo electrónico inválido'
      return false
    }
    if (email.value.length > 255) {
      emailError.value = 'El correo electrónico no puede exceder 255 caracteres'
      return false
    }
  }
  emailError.value = null
  return true
}

const validatePhone = () => {
  if (phone.value && phone.value.trim()) {
    const e164Pattern = /^\+[1-9]\d{1,14}$/
    if (!e164Pattern.test(phone.value)) {
      phoneError.value = 'El teléfono debe estar en formato E.164 (ej. +34612345678)'
      return false
    }
    if (phone.value.length > 20) {
      phoneError.value = 'El teléfono no puede exceder 20 caracteres'
      return false
    }
  }
  phoneError.value = null
  return true
}

const validateAll = () => {
  const validations = [
    validateFirstName(),
    validateLastName(),
    validateDateOfBirth(),
    validateRelationship(),
    validateDocumentNumber(),
    validateEmail(),
    validatePhone()
  ]
  return validations.every((v) => v)
}

// Auto-uppercase document number
watch(documentNumber, (newValue) => {
  if (newValue) {
    documentNumber.value = newValue.toUpperCase()
  }
})

const handleSubmit = () => {
  if (!validateAll()) {
    toast.add({
      severity: 'error',
      summary: 'Validación',
      detail: 'Por favor revisa los datos ingresados',
      life: 3000
    })
    return
  }

  const request: CreateFamilyMemberRequest | UpdateFamilyMemberRequest = {
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    dateOfBirth: dateOfBirth.value!.toISOString().split('T')[0], // YYYY-MM-DD
    relationship: relationship.value!,
    documentNumber: documentNumber.value.trim() || null,
    email: email.value.trim() || null,
    phone: phone.value.trim() || null,
    medicalNotes: medicalNotes.value.trim() || null,
    allergies: allergies.value.trim() || null
  }

  emit('submit', request)
}

const handleCancel = () => {
  emit('cancel')
}
</script>

<template>
  <form @submit.prevent="handleSubmit" class="space-y-4">
    <!-- First Name -->
    <div class="flex flex-col gap-2">
      <label for="first-name" class="font-medium text-sm">
        Nombre <span class="text-red-500">*</span>
      </label>
      <InputText
        id="first-name"
        v-model="firstName"
        placeholder="Ej: María"
        :invalid="!!firstNameError"
        :disabled="loading"
        @blur="validateFirstName"
      />
      <small v-if="firstNameError" class="text-red-500">{{ firstNameError }}</small>
    </div>

    <!-- Last Name -->
    <div class="flex flex-col gap-2">
      <label for="last-name" class="font-medium text-sm">
        Apellidos <span class="text-red-500">*</span>
      </label>
      <InputText
        id="last-name"
        v-model="lastName"
        placeholder="Ej: García López"
        :invalid="!!lastNameError"
        :disabled="loading"
        @blur="validateLastName"
      />
      <small v-if="lastNameError" class="text-red-500">{{ lastNameError }}</small>
    </div>

    <!-- Date of Birth -->
    <div class="flex flex-col gap-2">
      <label for="date-of-birth" class="font-medium text-sm">
        Fecha de Nacimiento <span class="text-red-500">*</span>
      </label>
      <Calendar
        id="date-of-birth"
        v-model="dateOfBirth"
        dateFormat="dd/mm/yy"
        :maxDate="new Date()"
        :invalid="!!dateOfBirthError"
        :disabled="loading"
        showIcon
        @blur="validateDateOfBirth"
        class="w-full"
      />
      <small v-if="dateOfBirthError" class="text-red-500">{{ dateOfBirthError }}</small>
    </div>

    <!-- Relationship -->
    <div class="flex flex-col gap-2">
      <label for="relationship" class="font-medium text-sm">
        Relación Familiar <span class="text-red-500">*</span>
      </label>
      <Dropdown
        id="relationship"
        v-model="relationship"
        :options="relationshipOptions"
        optionLabel="label"
        optionValue="value"
        placeholder="Selecciona una relación"
        :invalid="!!relationshipError"
        :disabled="loading"
        @change="validateRelationship"
        class="w-full"
      />
      <small v-if="relationshipError" class="text-red-500">{{ relationshipError }}</small>
    </div>

    <!-- Document Number (optional) -->
    <div class="flex flex-col gap-2">
      <label for="document-number" class="font-medium text-sm">Número de Documento</label>
      <InputText
        id="document-number"
        v-model="documentNumber"
        placeholder="Ej: 12345678A"
        :invalid="!!documentNumberError"
        :disabled="loading"
        @blur="validateDocumentNumber"
      />
      <small v-if="documentNumberError" class="text-red-500">{{ documentNumberError }}</small>
      <small class="text-gray-500">Solo letras mayúsculas y números</small>
    </div>

    <!-- Email (optional) -->
    <div class="flex flex-col gap-2">
      <label for="email" class="font-medium text-sm">Correo Electrónico</label>
      <InputText
        id="email"
        v-model="email"
        type="email"
        placeholder="Ej: maria@example.com"
        :invalid="!!emailError"
        :disabled="loading"
        @blur="validateEmail"
      />
      <small v-if="emailError" class="text-red-500">{{ emailError }}</small>
    </div>

    <!-- Phone (optional) -->
    <div class="flex flex-col gap-2">
      <label for="phone" class="font-medium text-sm">Teléfono</label>
      <InputText
        id="phone"
        v-model="phone"
        placeholder="Ej: +34612345678"
        :invalid="!!phoneError"
        :disabled="loading"
        @blur="validatePhone"
      />
      <small v-if="phoneError" class="text-red-500">{{ phoneError }}</small>
      <small class="text-gray-500">Formato E.164 con código de país (ej. +34)</small>
    </div>

    <!-- Medical Notes (optional, sensitive) -->
    <div class="flex flex-col gap-2">
      <label for="medical-notes" class="font-medium text-sm">Notas Médicas</label>
      <div v-if="hasMedicalNotesInfo" class="p-2 bg-blue-50 border border-blue-200 rounded text-sm text-blue-800">
        ℹ️ Este miembro tiene notas médicas guardadas. Déjalo en blanco para mantener las notas existentes, o escribe nuevas notas para reemplazarlas.
      </div>
      <Textarea
        id="medical-notes"
        v-model="medicalNotes"
        placeholder="Ej: Asma - requiere inhalador"
        :disabled="loading"
        rows="3"
        :maxlength="2000"
        class="w-full"
      />
      <small class="text-gray-500">Información sensible, encriptada. Máximo 2000 caracteres.</small>
    </div>

    <!-- Allergies (optional, sensitive) -->
    <div class="flex flex-col gap-2">
      <label for="allergies" class="font-medium text-sm">Alergias</label>
      <div v-if="hasAllergiesInfo" class="p-2 bg-blue-50 border border-blue-200 rounded text-sm text-blue-800">
        ℹ️ Este miembro tiene alergias guardadas. Déjalo en blanco para mantener las alergias existentes, o escribe nuevas alergias para reemplazarlas.
      </div>
      <Textarea
        id="allergies"
        v-model="allergies"
        placeholder="Ej: Cacahuetes, mariscos"
        :disabled="loading"
        rows="2"
        :maxlength="1000"
        class="w-full"
      />
      <small class="text-gray-500">Información sensible, encriptada. Máximo 1000 caracteres.</small>
    </div>

    <div class="flex justify-end gap-2 pt-4">
      <Button
        type="button"
        label="Cancelar"
        severity="secondary"
        :disabled="loading"
        @click="handleCancel"
      />
      <Button
        type="submit"
        :label="isEditing ? 'Actualizar' : 'Añadir Miembro'"
        :loading="loading"
        :disabled="loading"
      />
    </div>
  </form>
</template>
```

**Dependencies:**
- PrimeVue: `InputText`, `Calendar`, `Dropdown`, `Textarea`, `Button`, `useToast`
- Types from `@/types/family-unit`

**Implementation Notes:**
- Comprehensive client-side validation (matches backend FluentValidation rules)
- Spanish labels and error messages
- Document number auto-uppercase on input
- Phone validation for E.164 format
- Medical notes/allergies never pre-filled on edit (security)
- Info message when editing member with existing sensitive data
- Calendar with maxDate (today) to prevent future dates
- All optional fields properly handled (empty string → null)

---

### Step 5: Create Family Member List Component

**File:** `frontend/src/components/family-units/FamilyMemberList.vue`

**Action:** Create component to display family members in a DataTable

**Implementation Steps:**

1. **Create component file** `frontend/src/components/family-units/FamilyMemberList.vue`

2. **Implement component**:

```vue
<script setup lang="ts">
import { computed } from 'vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import { FamilyRelationshipLabels } from '@/types/family-unit'
import type { FamilyMemberResponse } from '@/types/family-unit'

const props = defineProps<{
  members: FamilyMemberResponse[]
  loading?: boolean
}>()

const emit = defineEmits<{
  edit: [member: FamilyMemberResponse]
  delete: [member: FamilyMemberResponse]
}>()

const membersWithAge = computed(() => {
  return props.members.map((member) => {
    const age = calculateAge(member.dateOfBirth)
    return { ...member, age }
  })
})

const calculateAge = (dateOfBirth: string): number => {
  const birthDate = new Date(dateOfBirth)
  const today = new Date()
  let age = today.getFullYear() - birthDate.getFullYear()
  const monthDiff = today.getMonth() - birthDate.getMonth()

  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
    age--
  }

  return age
}

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return new Intl.DateTimeFormat('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(date)
}

const getRelationshipLabel = (relationship: string): string => {
  return FamilyRelationshipLabels[relationship as keyof typeof FamilyRelationshipLabels] || relationship
}

const handleEdit = (member: FamilyMemberResponse) => {
  emit('edit', member)
}

const handleDelete = (member: FamilyMemberResponse) => {
  emit('delete', member)
}
</script>

<template>
  <div class="family-member-list">
    <DataTable
      :value="membersWithAge"
      :loading="loading"
      stripedRows
      responsiveLayout="scroll"
      :paginator="members.length > 10"
      :rows="10"
      class="p-datatable-sm"
    >
      <template #empty>
        <div class="text-center py-8 text-gray-500">
          No hay miembros familiares registrados
        </div>
      </template>

      <Column field="firstName" header="Nombre" :sortable="true">
        <template #body="{ data }">
          <div class="font-medium">{{ data.firstName }} {{ data.lastName }}</div>
          <div v-if="data.userId" class="text-xs text-gray-500">
            <i class="pi pi-user text-xs"></i> Usuario vinculado
          </div>
        </template>
      </Column>

      <Column field="dateOfBirth" header="Fecha Nacimiento" :sortable="true">
        <template #body="{ data }">
          <div>{{ formatDate(data.dateOfBirth) }}</div>
          <div class="text-xs text-gray-500">{{ data.age }} años</div>
        </template>
      </Column>

      <Column field="relationship" header="Relación" :sortable="true">
        <template #body="{ data }">
          <Tag :value="getRelationshipLabel(data.relationship)" severity="info" />
        </template>
      </Column>

      <Column header="Contacto">
        <template #body="{ data }">
          <div class="text-sm space-y-1">
            <div v-if="data.email" class="flex items-center gap-1">
              <i class="pi pi-envelope text-xs text-gray-500"></i>
              <span>{{ data.email }}</span>
            </div>
            <div v-if="data.phone" class="flex items-center gap-1">
              <i class="pi pi-phone text-xs text-gray-500"></i>
              <span>{{ data.phone }}</span>
            </div>
            <div v-if="!data.email && !data.phone" class="text-gray-400 italic">
              Sin contacto
            </div>
          </div>
        </template>
      </Column>

      <Column header="Salud">
        <template #body="{ data }">
          <div class="flex gap-2">
            <Tag
              v-if="data.hasMedicalNotes"
              value="Notas médicas"
              severity="warning"
              class="text-xs"
            />
            <Tag
              v-if="data.hasAllergies"
              value="Alergias"
              severity="danger"
              class="text-xs"
            />
            <span v-if="!data.hasMedicalNotes && !data.hasAllergies" class="text-gray-400 italic text-sm">
              Sin info
            </span>
          </div>
        </template>
      </Column>

      <Column header="Acciones" :exportable="false" class="text-right">
        <template #body="{ data }">
          <div class="flex justify-end gap-2">
            <Button
              icon="pi pi-pencil"
              severity="info"
              text
              rounded
              @click="handleEdit(data)"
              v-tooltip.top="'Editar'"
            />
            <Button
              icon="pi pi-trash"
              severity="danger"
              text
              rounded
              @click="handleDelete(data)"
              v-tooltip.top="'Eliminar'"
            />
          </div>
        </template>
      </Column>
    </DataTable>
  </div>
</template>
```

**Dependencies:**
- PrimeVue: `DataTable`, `Column`, `Button`, `Tag`, `tooltip` directive
- Types from `@/types/family-unit`

**Implementation Notes:**
- DataTable with sorting and pagination (> 10 members)
- Age calculation from date of birth
- Spanish date formatting
- Tags for relationship and health info (visual indicators)
- Icons for email/phone/user linked
- **NEVER displays actual medical notes or allergies** (only boolean flags as tags)
- Responsive layout for mobile
- Empty state message

---

### Step 6: Create Main Family Unit Page

**File:** `frontend/src/views/FamilyUnitPage.vue`

**Action:** Create main page component that orchestrates family unit management

**Implementation Steps:**

1. **Create view file** `frontend/src/views/FamilyUnitPage.vue`

2. **Implement component** (this is the main orchestrator):

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import Card from 'primevue/card'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import ConfirmDialog from 'primevue/confirmdialog'
import { useFamilyUnits } from '@/composables/useFamilyUnits'
import FamilyUnitForm from '@/components/family-units/FamilyUnitForm.vue'
import FamilyMemberForm from '@/components/family-units/FamilyMemberForm.vue'
import FamilyMemberList from '@/components/family-units/FamilyMemberList.vue'
import type {
  CreateFamilyUnitRequest,
  UpdateFamilyUnitRequest,
  CreateFamilyMemberRequest,
  UpdateFamilyMemberRequest,
  FamilyMemberResponse
} from '@/types/family-unit'

const confirm = useConfirm()
const toast = useToast()

const {
  familyUnit,
  familyMembers,
  loading,
  error,
  createFamilyUnit,
  getCurrentUserFamilyUnit,
  updateFamilyUnit,
  deleteFamilyUnit,
  createFamilyMember,
  getFamilyMembers,
  updateFamilyMember,
  deleteFamilyMember
} = useFamilyUnits()

// UI State
const showFamilyUnitDialog = ref(false)
const showMemberDialog = ref(false)
const editingMember = ref<FamilyMemberResponse | null>(null)

// Load family unit and members on mount
onMounted(async () => {
  await loadFamilyUnit()
})

const loadFamilyUnit = async () => {
  const unit = await getCurrentUserFamilyUnit()
  if (unit) {
    await getFamilyMembers(unit.id)
  }
}

// Family Unit handlers

const openCreateFamilyUnitDialog = () => {
  showFamilyUnitDialog.value = true
}

const openEditFamilyUnitDialog = () => {
  showFamilyUnitDialog.value = true
}

const handleFamilyUnitSubmit = async (request: CreateFamilyUnitRequest | UpdateFamilyUnitRequest) => {
  let success = false

  if (familyUnit.value) {
    // Update
    const result = await updateFamilyUnit(familyUnit.value.id, request as UpdateFamilyUnitRequest)
    success = !!result
  } else {
    // Create
    const result = await createFamilyUnit(request as CreateFamilyUnitRequest)
    success = !!result

    if (success && result) {
      // Load members (representative should be auto-created)
      await getFamilyMembers(result.id)
    }
  }

  if (success) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: familyUnit.value ? 'Unidad familiar actualizada' : 'Unidad familiar creada',
      life: 3000
    })
    showFamilyUnitDialog.value = false
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Error al guardar la unidad familiar',
      life: 5000
    })
  }
}

const handleDeleteFamilyUnit = () => {
  if (!familyUnit.value) return

  confirm.require({
    message: '¿Estás seguro de que quieres eliminar la unidad familiar? Esto eliminará todos los miembros familiares.',
    header: 'Confirmar Eliminación',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const success = await deleteFamilyUnit(familyUnit.value!.id)

      if (success) {
        toast.add({
          severity: 'success',
          summary: 'Éxito',
          detail: 'Unidad familiar eliminada',
          life: 3000
        })
      } else {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: error.value || 'Error al eliminar la unidad familiar',
          life: 5000
        })
      }
    }
  })
}

// Family Member handlers

const openCreateMemberDialog = () => {
  editingMember.value = null
  showMemberDialog.value = true
}

const openEditMemberDialog = (member: FamilyMemberResponse) => {
  editingMember.value = member
  showMemberDialog.value = true
}

const handleMemberSubmit = async (request: CreateFamilyMemberRequest | UpdateFamilyMemberRequest) => {
  if (!familyUnit.value) return

  let success = false

  if (editingMember.value) {
    // Update
    const result = await updateFamilyMember(
      familyUnit.value.id,
      editingMember.value.id,
      request as UpdateFamilyMemberRequest
    )
    success = !!result
  } else {
    // Create
    const result = await createFamilyMember(familyUnit.value.id, request as CreateFamilyMemberRequest)
    success = !!result
  }

  if (success) {
    toast.add({
      severity: 'success',
      summary: 'Éxito',
      detail: editingMember.value ? 'Miembro actualizado' : 'Miembro añadido',
      life: 3000
    })
    showMemberDialog.value = false
    editingMember.value = null
  } else {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: error.value || 'Error al guardar el miembro familiar',
      life: 5000
    })
  }
}

const handleDeleteMember = (member: FamilyMemberResponse) => {
  if (!familyUnit.value) return

  confirm.require({
    message: `¿Estás seguro de que quieres eliminar a ${member.firstName} ${member.lastName}?`,
    header: 'Confirmar Eliminación',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: async () => {
      const success = await deleteFamilyMember(familyUnit.value!.id, member.id)

      if (success) {
        toast.add({
          severity: 'success',
          summary: 'Éxito',
          detail: 'Miembro eliminado',
          life: 3000
        })
      } else {
        toast.add({
          severity: 'error',
          summary: 'Error',
          detail: error.value || 'Error al eliminar el miembro familiar',
          life: 5000
        })
      }
    }
  })
}
</script>

<template>
  <div class="family-unit-page p-4 max-w-7xl mx-auto">
    <ConfirmDialog />

    <div class="mb-6">
      <h1 class="text-3xl font-bold mb-2">Mi Unidad Familiar</h1>
      <p class="text-gray-600">
        Gestiona tu unidad familiar y los miembros que la componen
      </p>
    </div>

    <!-- No Family Unit Yet -->
    <div v-if="!loading && !familyUnit" class="text-center py-12">
      <Card class="max-w-md mx-auto">
        <template #content>
          <div class="space-y-4">
            <i class="pi pi-users text-6xl text-gray-300"></i>
            <h2 class="text-xl font-semibold">Aún no tienes una unidad familiar</h2>
            <p class="text-gray-600">
              Crea tu unidad familiar para poder gestionar los miembros de tu familia y registrarlos en campamentos.
            </p>
            <Button
              label="Crear Unidad Familiar"
              icon="pi pi-plus"
              @click="openCreateFamilyUnitDialog"
            />
          </div>
        </template>
      </Card>
    </div>

    <!-- Has Family Unit -->
    <div v-else-if="familyUnit" class="space-y-6">
      <!-- Family Unit Card -->
      <Card>
        <template #title>
          <div class="flex justify-between items-center">
            <span>{{ familyUnit.name }}</span>
            <div class="flex gap-2">
              <Button
                icon="pi pi-pencil"
                label="Editar"
                severity="info"
                outlined
                @click="openEditFamilyUnitDialog"
              />
              <Button
                icon="pi pi-trash"
                label="Eliminar"
                severity="danger"
                outlined
                @click="handleDeleteFamilyUnit"
              />
            </div>
          </div>
        </template>
        <template #content>
          <div class="text-sm text-gray-600">
            <p>Creada el {{ new Date(familyUnit.createdAt).toLocaleDateString('es-ES') }}</p>
          </div>
        </template>
      </Card>

      <!-- Family Members Section -->
      <Card>
        <template #title>
          <div class="flex justify-between items-center">
            <span>Miembros Familiares</span>
            <Button
              icon="pi pi-plus"
              label="Añadir Miembro"
              @click="openCreateMemberDialog"
            />
          </div>
        </template>
        <template #content>
          <FamilyMemberList
            :members="familyMembers"
            :loading="loading"
            @edit="openEditMemberDialog"
            @delete="handleDeleteMember"
          />
        </template>
      </Card>
    </div>

    <!-- Loading State -->
    <div v-else class="text-center py-12">
      <i class="pi pi-spin pi-spinner text-4xl text-primary-500"></i>
      <p class="mt-4 text-gray-600">Cargando...</p>
    </div>

    <!-- Family Unit Dialog -->
    <Dialog
      v-model:visible="showFamilyUnitDialog"
      :header="familyUnit ? 'Editar Unidad Familiar' : 'Crear Unidad Familiar'"
      :modal="true"
      :closable="!loading"
      :dismissableMask="!loading"
      class="w-full max-w-md"
    >
      <FamilyUnitForm
        :family-unit="familyUnit"
        :loading="loading"
        @submit="handleFamilyUnitSubmit"
        @cancel="showFamilyUnitDialog = false"
      />
    </Dialog>

    <!-- Family Member Dialog -->
    <Dialog
      v-model:visible="showMemberDialog"
      :header="editingMember ? 'Editar Miembro' : 'Añadir Miembro'"
      :modal="true"
      :closable="!loading"
      :dismissableMask="!loading"
      class="w-full max-w-2xl"
    >
      <FamilyMemberForm
        :member="editingMember"
        :loading="loading"
        @submit="handleMemberSubmit"
        @cancel="showMemberDialog = false"
      />
    </Dialog>
  </div>
</template>
```

**Dependencies:**
- PrimeVue: `Card`, `Button`, `Dialog`, `ConfirmDialog`, `useConfirm`, `useToast`
- Composable: `useFamilyUnits`
- Components: `FamilyUnitForm`, `FamilyMemberForm`, `FamilyMemberList`
- Types from `@/types/family-unit`

**Implementation Notes:**
- Main orchestrator for family unit management
- Three states: no family unit, has family unit, loading
- Dialogs for create/edit family unit and members
- Confirmation dialogs for delete operations
- Toast notifications for success/error feedback
- Loads family unit and members on mount
- Auto-loads members after creating family unit (representative should be auto-created by backend)
- Responsive design with max-width container
- Spanish labels and messages

---

### Step 7: Add Route Configuration

**File:** `frontend/src/router/index.ts`

**Action:** Add route for family unit page with authentication guard

**Implementation Steps:**

1. **Read existing router configuration** to understand the structure

2. **Add new route** for family unit page:

```typescript
{
  path: '/family-unit',
  name: 'FamilyUnit',
  component: () => import('@/views/FamilyUnitPage.vue'),
  meta: {
    requiresAuth: true,
    requiresEmailVerification: true,
    roles: ['Member', 'Board', 'Admin']
  }
}
```

3. **Verify route guard** exists to check authentication and roles

**Dependencies:**
- Vue Router
- Existing auth store for user role checking
- Existing route guards (should already be implemented)

**Implementation Notes:**
- Lazy load component with `() => import()`
- Require authentication and email verification
- Allow Member, Board, and Admin roles
- Route guard should redirect to login if not authenticated

---

### Step 8: Write Vitest Unit Tests for Composable

**File:** `frontend/src/composables/__tests__/useFamilyUnits.spec.ts`

**Action:** Write comprehensive unit tests for useFamilyUnits composable

**Implementation Steps:**

1. **Create test file** `frontend/src/composables/__tests__/useFamilyUnits.spec.ts`

2. **Implement tests**:

```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useFamilyUnits } from '../useFamilyUnits'
import api from '@/utils/api'

vi.mock('@/utils/api')

describe('useFamilyUnits', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('createFamilyUnit', () => {
    it('should create family unit successfully', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: '123',
            name: 'Garcia Family',
            representativeUserId: 'user-1',
            createdAt: '2026-02-15T10:00:00Z',
            updatedAt: '2026-02-15T10:00:00Z'
          }
        }
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const { createFamilyUnit, familyUnit } = useFamilyUnits()
      const result = await createFamilyUnit({ name: 'Garcia Family' })

      expect(result).toEqual(mockResponse.data.data)
      expect(familyUnit.value).toEqual(mockResponse.data.data)
      expect(api.post).toHaveBeenCalledWith('/api/family-units', { name: 'Garcia Family' })
    })

    it('should handle error when creating family unit', async () => {
      const mockError = {
        response: {
          data: {
            error: {
              message: 'Ya tienes una unidad familiar'
            }
          }
        }
      }

      vi.mocked(api.post).mockRejectedValueOnce(mockError)

      const { createFamilyUnit, error } = useFamilyUnits()
      const result = await createFamilyUnit({ name: 'Garcia Family' })

      expect(result).toBeNull()
      expect(error.value).toBe('Ya tienes una unidad familiar')
    })
  })

  describe('getCurrentUserFamilyUnit', () => {
    it('should get current user family unit successfully', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: '123',
            name: 'Garcia Family',
            representativeUserId: 'user-1',
            createdAt: '2026-02-15T10:00:00Z',
            updatedAt: '2026-02-15T10:00:00Z'
          }
        }
      }

      vi.mocked(api.get).mockResolvedValueOnce(mockResponse)

      const { getCurrentUserFamilyUnit, familyUnit } = useFamilyUnits()
      const result = await getCurrentUserFamilyUnit()

      expect(result).toEqual(mockResponse.data.data)
      expect(familyUnit.value).toEqual(mockResponse.data.data)
    })

    it('should handle 404 when user has no family unit', async () => {
      const mockError = {
        response: {
          status: 404
        }
      }

      vi.mocked(api.get).mockRejectedValueOnce(mockError)

      const { getCurrentUserFamilyUnit, familyUnit } = useFamilyUnits()
      const result = await getCurrentUserFamilyUnit()

      expect(result).toBeNull()
      expect(familyUnit.value).toBeNull()
    })
  })

  describe('createFamilyMember', () => {
    it('should create family member successfully', async () => {
      const mockResponse = {
        data: {
          success: true,
          data: {
            id: 'member-1',
            familyUnitId: 'unit-1',
            userId: null,
            firstName: 'Maria',
            lastName: 'Garcia',
            dateOfBirth: '2015-06-15',
            relationship: 'Child',
            documentNumber: 'ABC123',
            email: 'maria@example.com',
            phone: '+34612345678',
            hasMedicalNotes: true,
            hasAllergies: true,
            createdAt: '2026-02-15T11:00:00Z',
            updatedAt: '2026-02-15T11:00:00Z'
          }
        }
      }

      vi.mocked(api.post).mockResolvedValueOnce(mockResponse)

      const { createFamilyMember, familyMembers } = useFamilyUnits()
      const request = {
        firstName: 'Maria',
        lastName: 'Garcia',
        dateOfBirth: '2015-06-15',
        relationship: 'Child' as any,
        documentNumber: 'ABC123',
        email: 'maria@example.com',
        phone: '+34612345678',
        medicalNotes: 'Asthma',
        allergies: 'Peanuts'
      }

      const result = await createFamilyMember('unit-1', request)

      expect(result).toEqual(mockResponse.data.data)
      expect(familyMembers.value).toContainEqual(mockResponse.data.data)
    })
  })

  describe('updateFamilyMember', () => {
    it('should update family member successfully', async () => {
      const initialMember = {
        id: 'member-1',
        familyUnitId: 'unit-1',
        userId: null,
        firstName: 'Maria',
        lastName: 'Garcia',
        dateOfBirth: '2015-06-15',
        relationship: 'Child' as any,
        documentNumber: 'ABC123',
        email: 'maria@example.com',
        phone: '+34612345678',
        hasMedicalNotes: false,
        hasAllergies: false,
        createdAt: '2026-02-15T11:00:00Z',
        updatedAt: '2026-02-15T11:00:00Z'
      }

      const updatedMember = { ...initialMember, lastName: 'Garcia Lopez' }

      const mockResponse = {
        data: {
          success: true,
          data: updatedMember
        }
      }

      vi.mocked(api.put).mockResolvedValueOnce(mockResponse)

      const { updateFamilyMember, familyMembers } = useFamilyUnits()
      familyMembers.value = [initialMember]

      const result = await updateFamilyMember('unit-1', 'member-1', {
        ...initialMember,
        lastName: 'Garcia Lopez'
      })

      expect(result).toEqual(updatedMember)
      expect(familyMembers.value[0]).toEqual(updatedMember)
    })
  })

  describe('deleteFamilyMember', () => {
    it('should delete family member successfully', async () => {
      vi.mocked(api.delete).mockResolvedValueOnce({})

      const { deleteFamilyMember, familyMembers } = useFamilyUnits()
      familyMembers.value = [
        {
          id: 'member-1',
          familyUnitId: 'unit-1',
          firstName: 'Maria',
          lastName: 'Garcia'
        } as any
      ]

      const result = await deleteFamilyMember('unit-1', 'member-1')

      expect(result).toBe(true)
      expect(familyMembers.value).toHaveLength(0)
    })
  })
})
```

**Dependencies:**
- Vitest
- Mock for `@/utils/api`

**Implementation Notes:**
- Test all composable methods (create, read, update, delete for both family units and members)
- Mock API responses and errors
- Verify state updates (familyUnit, familyMembers refs)
- Test error handling and 404 cases
- Use `vi.mocked()` for typed mocks

---

### Step 9: Write Cypress E2E Tests

**File:** `frontend/cypress/e2e/family-units.cy.ts`

**Action:** Write end-to-end tests for critical family unit user flows

**Implementation Steps:**

1. **Create E2E test file** `frontend/cypress/e2e/family-units.cy.ts`

2. **Implement tests**:

```typescript
describe('Family Units Management', () => {
  beforeEach(() => {
    // Login as Member user
    cy.login('member@example.com', 'password123')
    cy.visit('/family-unit')
  })

  it('should create a new family unit', () => {
    // Should show "no family unit" state
    cy.contains('Aún no tienes una unidad familiar').should('be.visible')

    // Click create button
    cy.contains('Crear Unidad Familiar').click()

    // Fill in family unit name
    cy.get('#family-unit-name').type('García Family')

    // Submit form
    cy.contains('button', 'Crear').click()

    // Should show success message
    cy.contains('Unidad familiar creada').should('be.visible')

    // Should show family unit card
    cy.contains('García Family').should('be.visible')

    // Should show representative as first member (auto-created by backend)
    cy.contains('Miembros Familiares').should('be.visible')
  })

  it('should add a new family member', () => {
    // Assuming family unit already exists
    cy.contains('Añadir Miembro').click()

    // Fill in member details
    cy.get('#first-name').type('María')
    cy.get('#last-name').type('García López')
    cy.get('#date-of-birth').type('15/06/2015')
    cy.get('#relationship').click()
    cy.contains('Hijo/Hija').click()
    cy.get('#document-number').type('ABC123')
    cy.get('#email').type('maria@example.com')
    cy.get('#phone').type('+34612345678')
    cy.get('#medical-notes').type('Asthma - requires inhaler')
    cy.get('#allergies').type('Peanuts')

    // Submit form
    cy.contains('button', 'Añadir Miembro').click()

    // Should show success message
    cy.contains('Miembro añadido').should('be.visible')

    // Should show member in table
    cy.contains('María García López').should('be.visible')
    cy.contains('ABC123').should('be.visible')
    cy.contains('maria@example.com').should('be.visible')

    // Should show health tags (NOT actual content)
    cy.contains('Notas médicas').should('be.visible')
    cy.contains('Alergias').should('be.visible')
  })

  it('should edit an existing family member', () => {
    // Click edit button on first member
    cy.get('[data-testid="edit-member-btn"]').first().click()

    // Change last name
    cy.get('#last-name').clear().type('García Martínez')

    // Submit form
    cy.contains('button', 'Actualizar').click()

    // Should show success message
    cy.contains('Miembro actualizado').should('be.visible')

    // Should show updated name
    cy.contains('García Martínez').should('be.visible')
  })

  it('should delete a family member', () => {
    // Click delete button
    cy.get('[data-testid="delete-member-btn"]').first().click()

    // Confirm deletion
    cy.contains('button', 'Eliminar').click()

    // Should show success message
    cy.contains('Miembro eliminado').should('be.visible')
  })

  it('should validate required fields when creating member', () => {
    cy.contains('Añadir Miembro').click()

    // Try to submit empty form
    cy.contains('button', 'Añadir Miembro').should('be.disabled')

    // Fill only first name
    cy.get('#first-name').type('María')

    // Should still be disabled (other required fields missing)
    cy.contains('button', 'Añadir Miembro').should('be.disabled')

    // Fill all required fields
    cy.get('#last-name').type('García')
    cy.get('#date-of-birth').type('15/06/2015')
    cy.get('#relationship').click()
    cy.contains('Hijo/Hija').click()

    // Now button should be enabled
    cy.contains('button', 'Añadir Miembro').should('not.be.disabled')
  })

  it('should never display sensitive data (medical notes/allergies)', () => {
    // Medical notes and allergies should NEVER be visible as text
    cy.contains('Asthma').should('not.exist')
    cy.contains('Peanuts').should('not.exist')

    // Only tags should be visible
    cy.contains('Notas médicas').should('be.visible')
    cy.contains('Alergias').should('be.visible')
  })

  it('should delete family unit and all members', () => {
    // Click delete family unit button
    cy.contains('button', 'Eliminar').click()

    // Confirm deletion
    cy.contains('¿Estás seguro de que quieres eliminar la unidad familiar?').should('be.visible')
    cy.contains('button', 'Eliminar').click()

    // Should show success message
    cy.contains('Unidad familiar eliminada').should('be.visible')

    // Should return to "no family unit" state
    cy.contains('Aún no tienes una unidad familiar').should('be.visible')
  })
})
```

**Dependencies:**
- Cypress
- Custom command: `cy.login()` (should already exist)

**Implementation Notes:**
- Test critical user flows (create, read, update, delete)
- Test validation (required fields, disabled buttons)
- **CRITICAL**: Verify that medical notes/allergies are NEVER displayed as text
- Test confirmation dialogs
- Test toast notifications
- Use data-testid attributes for reliable selectors (add to components if needed)
- Assumes `cy.login()` custom command exists for authentication

---

### Step 10: Update Technical Documentation

**File:** `ai-specs/specs/api-endpoints.md`

**Action:** Verify family unit endpoints are documented (should already be done in backend implementation)

**Implementation Steps:**

1. **Read `ai-specs/specs/api-endpoints.md`** to check if family unit endpoints are documented

2. **If not documented**, add section for family unit endpoints:
   - POST /api/family-units
   - GET /api/family-units/me
   - GET /api/family-units/{id}
   - PUT /api/family-units/{id}
   - DELETE /api/family-units/{id}
   - POST /api/family-units/{familyUnitId}/members
   - GET /api/family-units/{familyUnitId}/members
   - GET /api/family-units/{familyUnitId}/members/{memberId}
   - PUT /api/family-units/{familyUnitId}/members/{memberId}
   - DELETE /api/family-units/{familyUnitId}/members/{memberId}

3. **Document request/response formats** for each endpoint

**File:** `ai-specs/specs/frontend-standards.mdc`

**Action:** Add family units feature to component examples if relevant

**Implementation Steps:**

1. **Read existing examples** in frontend-standards.mdc

2. **Add brief mention** of family units feature as example of composable-based architecture

3. **No major changes needed** - feature follows existing patterns

**References:**
- Follow `ai-specs/specs/documentation-standards.mdc`
- All documentation in English

---

## Implementation Order

1. **Step 0**: Create Feature Branch (`feature/feat-family-units-definition-frontend`)
2. **Step 1**: Define TypeScript Interfaces (`types/family-unit.ts`)
3. **Step 2**: Create API Composable (`composables/useFamilyUnits.ts`)
4. **Step 3**: Create Family Unit Form Component (`components/family-units/FamilyUnitForm.vue`)
5. **Step 4**: Create Family Member Form Component (`components/family-units/FamilyMemberForm.vue`)
6. **Step 5**: Create Family Member List Component (`components/family-units/FamilyMemberList.vue`)
7. **Step 6**: Create Main Family Unit Page (`views/FamilyUnitPage.vue`)
8. **Step 7**: Add Route Configuration (`router/index.ts`)
9. **Step 8**: Write Vitest Unit Tests for Composable (`composables/__tests__/useFamilyUnits.spec.ts`)
10. **Step 9**: Write Cypress E2E Tests (`cypress/e2e/family-units.cy.ts`)
11. **Step 10**: Update Technical Documentation (`ai-specs/specs/api-endpoints.md`, `frontend-standards.mdc`)

---

## Testing Checklist

### Vitest Unit Tests

**Composable Tests (`useFamilyUnits`):**
- ✅ createFamilyUnit - success
- ✅ createFamilyUnit - error handling
- ✅ getCurrentUserFamilyUnit - success
- ✅ getCurrentUserFamilyUnit - 404 handling
- ✅ getFamilyUnitById - success
- ✅ updateFamilyUnit - success
- ✅ deleteFamilyUnit - success
- ✅ createFamilyMember - success
- ✅ getFamilyMembers - success
- ✅ updateFamilyMember - success and local state update
- ✅ deleteFamilyMember - success and local state update

**Component Tests (if time permits):**
- ✅ FamilyUnitForm - validation
- ✅ FamilyMemberForm - validation
- ✅ FamilyMemberList - rendering

### Cypress E2E Tests

**Critical User Flows:**
- ✅ Create family unit
- ✅ View family unit
- ✅ Edit family unit
- ✅ Delete family unit
- ✅ Add family member
- ✅ Edit family member
- ✅ Delete family member
- ✅ Form validation (required fields)
- ✅ **Security**: Medical notes/allergies NEVER displayed as text
- ✅ Confirmation dialogs work
- ✅ Toast notifications appear
- ✅ Representative auto-created after creating family unit

### Manual Testing

- ✅ All forms render correctly
- ✅ PrimeVue components styled properly
- ✅ Responsive design on mobile/tablet/desktop
- ✅ Loading states show correctly
- ✅ Error messages in Spanish
- ✅ Validation messages in Spanish
- ✅ DataTable sorting/pagination works
- ✅ Dialogs open/close correctly
- ✅ Route guards redirect unauthenticated users
- ✅ Sensitive data (medical notes/allergies) NEVER visible

---

## Error Handling Patterns

### Composable Error Handling

- API errors captured in `catch` blocks
- Error message extracted from `err.response.data.error.message`
- Fallback error message if backend error format unexpected
- `error` ref updated with Spanish error message
- Component displays error via Toast

### Component Error Handling

- Show Toast notification on error
- Display inline validation errors below inputs
- Disable submit buttons when validation fails
- Loading states prevent duplicate submissions

### Example Pattern:

```typescript
try {
  const result = await api.post('/api/family-units', request)
  return result.data.data
} catch (err: any) {
  error.value = err.response?.data?.error?.message || 'Error al crear la unidad familiar'
  return null
}
```

---

## UI/UX Considerations

### PrimeVue Components Used

- **DataTable**: Family members list with sorting, pagination
- **Dialog**: Modal dialogs for create/edit forms
- **Calendar**: Date picker for date of birth (maxDate: today)
- **Dropdown**: Relationship selection
- **InputText**: Text inputs
- **Textarea**: Medical notes and allergies (multiline)
- **Button**: Actions (create, edit, delete, cancel)
- **Tag**: Visual indicators for relationship, health info
- **ConfirmDialog**: Delete confirmations
- **Toast**: Success/error notifications

### Tailwind CSS Usage

- **Layout**: `flex`, `grid`, `space-y-4`, `gap-2`, `p-4`, `max-w-7xl`, `mx-auto`
- **Spacing**: `mb-6`, `pt-4`, `py-12`
- **Typography**: `text-3xl`, `font-bold`, `text-gray-600`, `text-sm`
- **Responsive**: `max-w-md`, `max-w-2xl`, `w-full`
- **Colors**: `bg-blue-50`, `border-blue-200`, `text-blue-800`, `text-red-500`
- **Icons**: PrimeIcons via `pi` classes

### Responsive Design

- Mobile-first approach
- Dialog max-width: `max-w-md` (family unit form), `max-w-2xl` (member form)
- DataTable: `responsiveLayout="scroll"` for mobile
- Container: `max-w-7xl mx-auto` for desktop
- Form inputs: `w-full` for mobile-friendly layout

### Accessibility

- Proper `<label>` elements with `for` attributes
- Required fields marked with `*`
- Error messages associated with inputs
- Button labels in Spanish
- Tooltip for icon-only buttons
- Keyboard navigation supported by PrimeVue components

### Loading States

- `loading` prop disables inputs and buttons
- Spinner icon during data fetch
- "Cargando..." text with spinner
- Submit button shows `:loading` prop (PrimeVue built-in spinner)

---

## Dependencies

### npm Packages Required

**Already Installed:**
- `vue@^3.x` - Vue 3 framework
- `vue-router@^4.x` - Routing
- `pinia@^2.x` - State management
- `axios@^1.x` - HTTP client
- `primevue@^4.x` - UI component library
- `primeicons@^7.x` - Icon library
- `tailwindcss@^3.x` - CSS framework
- `vitest@^2.x` - Unit testing
- `@vue/test-utils@^2.x` - Vue testing utilities
- `cypress@^13.x` - E2E testing
- `typescript@^5.x` - TypeScript

**No New Packages Needed** - all dependencies already in project

### PrimeVue Components Used

- `Button`
- `Card`
- `Calendar`
- `Column`
- `ConfirmDialog`
- `DataTable`
- `Dialog`
- `Dropdown`
- `InputText`
- `Tag`
- `Textarea`
- `useConfirm` composable
- `useToast` composable

---

## Notes

### CRITICAL Reminders

1. **NEVER display medical notes or allergies as text** - Only show boolean flags (`hasMedicalNotes`, `hasAllergies`)
2. **FamilyRelationship enum does NOT include "Friend"** - Friends/guests will be handled separately
3. **All user-facing text in Spanish** - Labels, validation messages, error messages, toast notifications
4. **Always use `<script setup lang="ts">`** - No Options API
5. **No `<style>` blocks** - Tailwind CSS only
6. **TypeScript strict mode** - No `any` types
7. **Composables for API communication** - Components never call API directly
8. **Route guards** - Authentication and email verification required

### Business Rules

- One family unit per user
- User becomes representative when creating family unit
- Representative auto-created as first family member (backend)
- Only representative OR Admin/Board can view/modify family unit
- Document number auto-uppercase on input
- Phone must be E.164 format (+ country code)
- Date of birth must be past date (maxDate: today in Calendar)
- Medical notes max 2000 chars, allergies max 1000 chars

### Language Requirements

**Spanish (User-Facing):**
- All labels, buttons, headings
- Validation error messages
- Toast notifications
- Confirmation dialogs
- Empty states

**English (Developer-Facing):**
- Code comments
- Variable names
- Function names
- Console logs (if any)
- Documentation

**Spanish Examples:**
- "Unidad Familiar" (Family Unit)
- "Miembros Familiares" (Family Members)
- "Añadir Miembro" (Add Member)
- "El nombre es obligatorio" (Name is required)
- "Fecha de Nacimiento" (Date of Birth)

### Security & Privacy (RGPD)

- **Sensitive Data**: Medical notes and allergies are health data
- **Encryption**: Backend encrypts at rest (AES-256)
- **Frontend Display**: NEVER show actual encrypted content
- **Boolean Flags**: Only show `hasMedicalNotes` and `hasAllergies` as visual tags
- **Edit Behavior**: Don't pre-fill medical notes/allergies on edit (security)
- **Info Messages**: Show info box if member has existing sensitive data when editing

---

## Next Steps After Implementation

1. **Frontend Testing**: Run all Vitest and Cypress tests, verify 90%+ coverage
2. **Manual QA**: Test all flows manually in different browsers
3. **Responsive Testing**: Test on mobile, tablet, desktop viewports
4. **Accessibility Testing**: Verify keyboard navigation, screen reader compatibility
5. **Integration with Backend**: Verify API calls work with backend implementation
6. **Performance Testing**: Check DataTable performance with many members
7. **Documentation**: Update user guide with screenshots of family unit management
8. **Future Enhancements**:
   - Photo upload for family unit and members (blob storage)
   - Link family members to their own User accounts (userId field)
   - Export family data to PDF
   - Bulk import members from CSV

---

## Implementation Verification

### Final Verification Checklist

**Code Quality:**
- ✅ All components use `<script setup lang="ts">`
- ✅ No `any` types (TypeScript strict)
- ✅ No `<style>` blocks (Tailwind only)
- ✅ Composable pattern for API communication
- ✅ Proper TypeScript interfaces for all data

**Functionality:**
- ✅ Create family unit works
- ✅ Edit family unit works
- ✅ Delete family unit works
- ✅ Add family member works
- ✅ Edit family member works
- ✅ Delete family member works
- ✅ Form validation works (client-side)
- ✅ API error handling works
- ✅ Loading states work
- ✅ Toast notifications work
- ✅ Confirmation dialogs work

**Testing:**
- ✅ Vitest unit tests passing (composable)
- ✅ Cypress E2E tests passing (user flows)
- ✅ Test coverage 90%+ (composable)
- ✅ Manual testing completed

**Integration:**
- ✅ Route configuration correct
- ✅ Route guards enforce authentication
- ✅ API calls use correct endpoints
- ✅ Request/response types match backend DTOs

**Security & Privacy:**
- ✅ Medical notes NEVER displayed as text
- ✅ Allergies NEVER displayed as text
- ✅ Only boolean flags shown (`hasMedicalNotes`, `hasAllergies`)
- ✅ Sensitive data not pre-filled on edit

**UI/UX:**
- ✅ PrimeVue components used correctly
- ✅ Tailwind CSS for all styling
- ✅ Responsive design (mobile/tablet/desktop)
- ✅ Accessibility (labels, ARIA, keyboard nav)
- ✅ Spanish labels and messages
- ✅ Loading states during API calls
- ✅ Error messages displayed properly

**Documentation:**
- ✅ API endpoints documented (verify in api-endpoints.md)
- ✅ Frontend patterns followed (per frontend-standards.mdc)
- ✅ Types documented in code comments

---

**Document Version:** 1.0
**Created:** 2026-02-15
**Status:** Ready for Implementation

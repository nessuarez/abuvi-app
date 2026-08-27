# Frontend Implementation Plan: feat-camp-meals-report — Camp Meals ("Comensales") Report

## Overview

A Board/Admin-only page for a `CampEdition` showing the day × meal × age-category diner report,
with inline controls to add extra diners or exclude a registered attendee from a specific meal,
and a button to download the same report as an `.xlsx` file. Depends on the backend endpoints
defined in [feat-camp-meals-report_backend.md](./feat-camp-meals-report_backend.md).

---

## Files to Create

| File | Purpose |
|---|---|
| `frontend/src/types/campMeals.ts` | TypeScript types mirroring the backend DTOs |
| `frontend/src/composables/useCampMeals.ts` | API calls: fetch report, CRUD extra diners/exclusions, trigger export download |
| `frontend/src/components/camps/CampMealsReportTable.vue` | The day/meal/age-category table |
| `frontend/src/components/camps/AddExtraDinerDialog.vue` | Dialog to add a manual extra-diner entry |
| `frontend/src/components/camps/ExcludeAttendeeDialog.vue` | Dialog to pick a registered attendee and exclude them from one meal |
| `frontend/src/views/camps/CampMealsReportPage.vue` | Route-level page hosting the table + dialogs + export button |
| `frontend/src/components/camps/__tests__/CampMealsReportTable.test.ts` | Component tests |
| `frontend/src/components/camps/__tests__/AddExtraDinerDialog.test.ts` | Component tests |
| `frontend/src/components/camps/__tests__/ExcludeAttendeeDialog.test.ts` | Component tests |
| `frontend/src/composables/__tests__/useCampMeals.test.ts` | Composable tests |

## Files to Modify

| File | Change |
|---|---|
| `frontend/src/router/index.ts` | Add route `/camps/editions/:id/meals`, `meta: { requiresAuth: true, requiresBoard: true, title: 'ABUVI | Comensales' }` |
| The camp edition detail/management page (wherever its action menu lives — check the current edition detail view for the existing "Extras" / "Alojamientos" navigation links and add a matching "Comensales" link) | Add navigation entry point to the new page |

---

## Types

```typescript
// types/campMeals.ts
export type MealType = 'Breakfast' | 'Lunch' | 'Snack' | 'Dinner'
export type AgeCategory = 'Baby' | 'Child' | 'Adult'

export interface CampMealsAgeCategoryCount {
  ageCategory: AgeCategory
  baseCount: number
  extraCount: number
  total: number
}

export interface CampMealsMeal {
  mealType: MealType
  counts: CampMealsAgeCategoryCount[]
  total: number
}

export interface CampMealsDay {
  date: string // ISO date (yyyy-MM-dd)
  meals: CampMealsMeal[]
}

export interface CampMealsReport {
  campEditionId: string
  days: CampMealsDay[]
}

export interface ExtraDiner {
  id: string
  campEditionId: string
  date: string
  mealType: MealType
  ageCategory: AgeCategory
  count: number
  notes: string | null
  createdAt: string
  createdByName: string
}

export interface CreateExtraDinerRequest {
  date: string
  mealType: MealType
  ageCategory: AgeCategory
  count: number
  notes?: string
}

export interface MealExclusion {
  id: string
  campEditionId: string
  registrationMemberId: string
  memberFullName: string
  date: string
  mealType: MealType
  reason: string | null
  createdAt: string
  createdByName: string
}

export interface CreateMealExclusionRequest {
  registrationMemberId: string
  date: string
  mealType: MealType
  reason?: string
}

export interface MealAttendee {
  registrationMemberId: string
  fullName: string
  ageCategory: AgeCategory
  isExcluded: boolean
  exclusionId: string | null
}
```

Spanish display labels for `MealType`/`AgeCategory` (`Desayuno`/`Comida`/`Merienda`/`Cena`,
`Bebés`/`Niños`/`Adultos`) belong in the component that renders them, as a small local lookup
map — not in `types/`, which stays free of presentation concerns per the existing convention
(`types/camp.ts` has no display-label maps either).

---

## Composable: `useCampMeals`

```typescript
// composables/useCampMeals.ts
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'
import type {
  CampMealsReport, ExtraDiner, CreateExtraDinerRequest,
  MealExclusion, CreateMealExclusionRequest, MealAttendee, MealType
} from '@/types/campMeals'

export function useCampMeals(campEditionId: string) {
  const report = ref<CampMealsReport | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchReport = async () => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<CampMealsReport>>(
        `/camps/editions/${campEditionId}/meals/report`
      )
      report.value = response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message ?? 'Error al cargar el informe de comensales'
    } finally {
      loading.value = false
    }
  }

  const fetchAttendees = async (date: string, mealType: MealType): Promise<MealAttendee[]> => {
    const response = await api.get<ApiResponse<MealAttendee[]>>(
      `/camps/editions/${campEditionId}/meals/attendees`,
      { params: { date, mealType } }
    )
    return response.data.data ?? []
  }

  const addExtraDiner = async (request: CreateExtraDinerRequest): Promise<ExtraDiner | null> => {
    try {
      const response = await api.post<ApiResponse<ExtraDiner>>(
        `/camps/editions/${campEditionId}/meals/extra-diners`, request
      )
      await fetchReport()
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message ?? 'Error al añadir el comensal extra'
      return null
    }
  }

  const removeExtraDiner = async (id: string): Promise<boolean> => {
    try {
      await api.delete(`/camps/editions/meals/extra-diners/${id}`)
      await fetchReport()
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message ?? 'Error al eliminar el comensal extra'
      return false
    }
  }

  const addExclusion = async (request: CreateMealExclusionRequest): Promise<MealExclusion | null> => {
    try {
      const response = await api.post<ApiResponse<MealExclusion>>(
        `/camps/editions/${campEditionId}/meals/exclusions`, request
      )
      await fetchReport()
      return response.data.data
    } catch (err: any) {
      error.value = err.response?.data?.error?.message ?? 'Error al excluir a la persona'
      return null
    }
  }

  const removeExclusion = async (id: string): Promise<boolean> => {
    try {
      await api.delete(`/camps/editions/meals/exclusions/${id}`)
      await fetchReport()
      return true
    } catch (err: any) {
      error.value = err.response?.data?.error?.message ?? 'Error al restaurar a la persona'
      return false
    }
  }

  const downloadExcel = async () => {
    try {
      const response = await api.get(`/camps/editions/${campEditionId}/meals/export`, {
        responseType: 'blob'
      })
      const url = URL.createObjectURL(new Blob([response.data]))
      const link = document.createElement('a')
      link.href = url
      link.download = `comensales-${campEditionId}.xlsx`
      link.click()
      URL.revokeObjectURL(url)
    } catch (err: any) {
      error.value = 'Error al exportar el informe'
    }
  }

  return {
    report, loading, error,
    fetchReport, fetchAttendees,
    addExtraDiner, removeExtraDiner,
    addExclusion, removeExclusion,
    downloadExcel
  }
}
```

Implementation notes:

- `responseType: 'blob'` on a shared Axios instance whose response interceptor calls
  `auth.logout()` on 401 (see `frontend-standards.mdc`'s Axios Configuration section) works
  unchanged — Axios still parses the response envelope type correctly per-request when
  `responseType` is set on that call, not globally.
- Every mutation re-fetches the whole report rather than patching local state, matching the
  simplicity of `useCamps.createCamp` (which also doesn't attempt optimistic updates) — the
  report is cheap to refetch and this avoids subtly wrong client-side recomputation of
  base/extra/total counts.

---

## `CampMealsReportTable.vue`

**Props:**

```typescript
interface Props {
  report: CampMealsReport
}
```

**Emits:**

```typescript
const emit = defineEmits<{
  addExtra: [date: string, mealType: MealType]
  excludeAttendee: [date: string, mealType: MealType]
}>()
```

**Layout**: one section per day (`Día 1 — Lun 15/07`), each with a small table: rows = meal
types, columns = age categories + total, each cell showing `total` with a muted `(+N extra)`
suffix when `extraCount > 0`. Two icon buttons per meal row: "Añadir extra" (opens
`AddExtraDinerDialog` via the `addExtra` emit) and "Excluir persona" (opens
`ExcludeAttendeeDialog` via `excludeAttendee`).

Use PrimeVue `DataTable`/`Column` per the UI standard (`frontend-standards.mdc` PrimeVue
Integration section) rather than hand-rolled `<table>` markup — a nested/grouped header
(`Column` with child `Column`s) is the natural fit for "meal → age categories" grouping PrimeVue
already supports.

**Accessibility**: this is a data table, not a map — no special accommodation needed beyond the
existing `frontend-standards.mdc` Accessibility rules (labelled buttons, keyboard-operable
PrimeVue `DataTable`).

---

## `AddExtraDinerDialog.vue`

**Props:** `visible: boolean`, `campEditionId: string`, `initialDate?: string`, `initialMealType?: MealType`.
**Emits:** `update:visible`, `saved`.

Form fields: `Date` (PrimeVue `DatePicker`, constrained to the edition's date range — pass
`minDate`/`maxDate` props from the parent), `MealType` (`Select`), `AgeCategory` (`Select`),
`Count` (`InputNumber`, min 1), `Notes` (`Textarea`, optional). On submit, calls
`useCampMeals(campEditionId).addExtraDiner(...)`; on success emits `saved` and closes.

---

## `ExcludeAttendeeDialog.vue`

**Props:** `visible: boolean`, `campEditionId: string`, `date: string`, `mealType: MealType`.
**Emits:** `update:visible`, `saved`.

On open (`watch(() => props.visible, ...)`), calls
`useCampMeals(campEditionId).fetchAttendees(date, mealType)` and renders the list with a
toggle per attendee: already-excluded attendees show a "Volver a incluir" (re-include) action
that calls `removeExclusion(exclusionId)`; present attendees show an "Excluir" action that opens
an optional reason input then calls `addExclusion(...)`. This single dialog handles both
directions so the board member doesn't need to remember which of two separate screens has the
undo action.

---

## `CampMealsReportPage.vue`

```vue
<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useCampMeals } from '@/composables/useCampMeals'
import CampMealsReportTable from '@/components/camps/CampMealsReportTable.vue'
import AddExtraDinerDialog from '@/components/camps/AddExtraDinerDialog.vue'
import ExcludeAttendeeDialog from '@/components/camps/ExcludeAttendeeDialog.vue'
import Button from 'primevue/button'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import { ref } from 'vue'
import type { MealType } from '@/types/campMeals'

const route = useRoute()
const campEditionId = route.params.id as string
const { report, loading, error, fetchReport, downloadExcel } = useCampMeals(campEditionId)

const addDialog = ref({ visible: false, date: '', mealType: 'Breakfast' as MealType })
const excludeDialog = ref({ visible: false, date: '', mealType: 'Breakfast' as MealType })

onMounted(fetchReport)
</script>

<template>
  <div class="space-y-4 p-4">
    <div class="flex items-center justify-between">
      <h1 class="text-xl font-semibold">Comensales</h1>
      <Button label="Exportar a Excel" icon="pi pi-file-excel" @click="downloadExcel" />
    </div>

    <ProgressSpinner v-if="loading" />
    <Message v-else-if="error" severity="error">{{ error }}</Message>
    <CampMealsReportTable
      v-else-if="report"
      :report="report"
      @add-extra="(date, mealType) => { addDialog = { visible: true, date, mealType } }"
      @exclude-attendee="(date, mealType) => { excludeDialog = { visible: true, date, mealType } }"
    />

    <AddExtraDinerDialog
      v-model:visible="addDialog.visible"
      :camp-edition-id="campEditionId"
      :initial-date="addDialog.date"
      :initial-meal-type="addDialog.mealType"
      @saved="fetchReport"
    />
    <ExcludeAttendeeDialog
      v-model:visible="excludeDialog.visible"
      :camp-edition-id="campEditionId"
      :date="excludeDialog.date"
      :meal-type="excludeDialog.mealType"
      @saved="fetchReport"
    />
  </div>
</template>
```

---

## Router

```typescript
{
  path: '/camps/editions/:id/meals',
  name: 'camp-edition-meals',
  component: () => import('@/views/camps/CampMealsReportPage.vue'),
  meta: { requiresAuth: true, requiresBoard: true, title: 'ABUVI | Comensales' }
}
```

`requiresBoard` mirrors whatever meta flag the router already uses to gate Board-only pages
(check `feat-camp-edition-extras_frontend.md` or the accommodation-assignment route for the
exact existing flag name before assuming `requiresBoard` is correct — use the established one).

---

## Test Coverage

### `useCampMeals.test.ts`

- `fetchReport should populate report on success`
- `fetchReport should set error message on failure`
- `addExtraDiner should refetch the report after a successful add`
- `addExtraDiner should set error and return null on failure`
- `removeExtraDiner should refetch the report after a successful delete`
- `addExclusion should refetch the report after a successful add`
- `removeExclusion should refetch the report after a successful delete`
- `fetchAttendees should return the attendee list`
- `downloadExcel should trigger a blob download without throwing`

### `CampMealsReportTable.test.ts`

- renders one section per day in `report.days`
- shows extra-count suffix only when `extraCount > 0`
- emits `addExtra` with the correct date/mealType when the add button is clicked
- emits `excludeAttendee` with the correct date/mealType when the exclude button is clicked

### `AddExtraDinerDialog.test.ts`

- pre-fills date/meal type from `initialDate`/`initialMealType` props
- disables submit while `count` is empty or non-positive
- calls `addExtraDiner` with the form values and emits `saved` on success

### `ExcludeAttendeeDialog.test.ts`

- fetches attendees when opened
- shows "Excluir" for present, non-excluded attendees
- shows "Volver a incluir" for already-excluded attendees
- calls `addExclusion`/`removeExclusion` respectively and emits `saved`

Coverage target: ≥90%, per `frontend-standards.mdc`.

---

## Manual Verification (UI change — must be checked in a browser per project rules)

1. Start the dev server and log in as a Board/Admin user.
2. Navigate to a camp edition with active registrations spanning `Complete`, partial-week, and
   `WeekendVisit` attendance, and at least one cancelled registration.
3. Open `/camps/editions/:id/meals` and confirm counts match expectations for at least one day
   in each attendance category, and that the cancelled registration's members are not counted.
4. Add an extra diner for a specific day/meal/age category; confirm the table updates.
5. Exclude a present attendee from one meal; confirm only that meal's count drops, other meals
   for that person on other days are unaffected, and re-including them restores the count.
6. Click "Exportar a Excel"; open the downloaded file and confirm the numbers match the on-screen
   table.
7. Confirm a Member-role user gets redirected/denied when visiting the route directly.

---

## Document Control

- **Version**: 1.0
- **Created**: 2026-08-26
- **Status**: ❌ Not Started
- **Dependencies**: backend endpoints from `feat-camp-meals-report_backend.md` must exist first.

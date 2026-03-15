# Frontend Implementation Plan: feat-admin-delete-family-units

## Overview

Implementar la interfaz de administracion para que usuarios con rol Admin/Board puedan eliminar unidades familiares (hard delete si no tienen inscripciones), desactivar/reactivar unidades (toggle `IsActive`), y eliminar miembros individuales. Incluye indicadores visuales de estado, filtro por estado activo/inactivo, y dialogos de confirmacion para todas las acciones destructivas.

Sigue la arquitectura del proyecto: Vue 3 Composition API con `<script setup lang="ts">`, PrimeVue para componentes UI, Tailwind CSS para layout, y composables para toda comunicacion con la API.

---

## Architecture Context

- **Componentes a modificar**:
  - `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` — acciones admin, filtro estado, badge inactiva
  - `frontend/src/components/family-units/FamilyMemberList.vue` — boton eliminar para Admin/Board
- **Composable a modificar**: `frontend/src/composables/useFamilyUnits.ts` — nuevos metodos API
- **Tipos a modificar**: `frontend/src/types/family-unit.ts` — agregar `isActive`
- **Sin nuevas rutas** — todo se integra en el panel admin existente
- **Sin Pinia stores nuevos** — estado local via composable

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Crear branch dedicado para el frontend
- **Branch Name**: `feature/feat-admin-delete-family-units-frontend`
- **Implementation Steps**:
  1. `git checkout dev`
  2. `git pull origin dev`
  3. `git checkout -b feature/feat-admin-delete-family-units-frontend`
  4. `git branch` — verificar
- **Notes**: Asume que el backend ya esta implementado y merged en `dev`. Si no, coordinarse con la rama backend para tener los endpoints disponibles.

---

### Step 1: Update TypeScript Types

- **File**: `frontend/src/types/family-unit.ts`
- **Action**: Agregar `isActive` a `FamilyUnitResponse`
- **Implementation Steps**:
  1. Agregar campo `isActive` al type `FamilyUnitResponse`:
     ```typescript
     export interface FamilyUnitResponse {
       id: string
       name: string
       representativeUserId: string
       familyNumber: number | null
       isActive: boolean              // NUEVO
       profilePhotoUrl: string | null
       createdAt: string
       updatedAt: string
       // Optional (from admin list):
       representativeName?: string
       membersCount?: number
     }
     ```
  2. Crear type para el request de update status:
     ```typescript
     export interface UpdateFamilyUnitStatusRequest {
       isActive: boolean
     }
     ```

---

### Step 2: Update Composable — Nuevos metodos API

- **File**: `frontend/src/composables/useFamilyUnits.ts`
- **Action**: Agregar metodos `adminDeleteFamilyUnit`, `updateFamilyUnitStatus`, y agregar parametro `isActive` al `fetchAllFamilyUnits`
- **Implementation Steps**:
  1. **`adminDeleteFamilyUnit`** — Hard delete via admin endpoint:
     ```typescript
     const adminDeleteFamilyUnit = async (id: string): Promise<boolean> => {
       loading.value = true
       error.value = null
       try {
         await api.delete(`/family-units/${id}/admin`)
         return true
       } catch (err: any) {
         error.value = err.response?.data?.error?.message || 'Error al eliminar la unidad familiar'
         return false
       } finally {
         loading.value = false
       }
     }
     ```
  2. **`updateFamilyUnitStatus`** — Toggle IsActive:
     ```typescript
     const updateFamilyUnitStatus = async (id: string, isActive: boolean): Promise<FamilyUnitResponse | null> => {
       loading.value = true
       error.value = null
       try {
         const response = await api.patch<ApiResponse<FamilyUnitResponse>>(
           `/family-units/${id}/status`,
           { isActive }
         )
         return response.data.data
       } catch (err: any) {
         error.value = err.response?.data?.error?.message || 'Error al actualizar el estado'
         return null
       } finally {
         loading.value = false
       }
     }
     ```
  3. **Actualizar `fetchAllFamilyUnits`** — Agregar parametro `isActive` opcional:
     ```typescript
     const fetchAllFamilyUnits = async (params: {
       page?: number
       pageSize?: number
       search?: string
       sortBy?: string
       sortOrder?: string
       membershipStatus?: string
       isActive?: boolean | null       // NUEVO
     } = {}) => {
       // ... existing code ...
       // Agregar al query string:
       if (params.isActive !== undefined && params.isActive !== null) {
         queryParams.append('isActive', String(params.isActive))
       }
       // ... rest of existing code ...
     }
     ```
  4. **Exponer** los nuevos metodos en el return del composable:
     ```typescript
     return {
       // ... existing ...
       adminDeleteFamilyUnit,
       updateFamilyUnitStatus,
     }
     ```

---

### Step 3: Update FamilyUnitsAdminPanel — Acciones admin y filtro

- **File**: `frontend/src/components/admin/FamilyUnitsAdminPanel.vue`
- **Action**: Agregar columna estado, botones de accion (eliminar, desactivar/activar), filtro por estado, y dialogos de confirmacion
- **Implementation Steps**:

  #### 3a: Imports y setup
  1. Agregar imports necesarios:
     ```typescript
     import { useConfirm } from 'primevue/useconfirm'
     import { useToast } from 'primevue/usetoast'
     import Tag from 'primevue/tag'
     ```
  2. Inicializar confirm y toast:
     ```typescript
     const confirm = useConfirm()
     const toast = useToast()
     ```
  3. Destructurar nuevos metodos del composable:
     ```typescript
     const { allFamilyUnits, familyUnitsPagination, loading, error, fetchAllFamilyUnits,
             adminDeleteFamilyUnit, updateFamilyUnitStatus } = useFamilyUnits()
     ```

  #### 3b: Filtro por estado activo
  4. Agregar ref para el filtro de estado:
     ```typescript
     const statusFilter = ref<string>('all')
     const statusOptions = [
       { label: 'Todas', value: 'all' },
       { label: 'Activas', value: 'active' },
       { label: 'Inactivas', value: 'inactive' },
     ]
     ```
  5. Agregar `SelectButton` en el template, junto al filtro de membership existente:
     ```vue
     <SelectButton
       v-model="statusFilter"
       :options="statusOptions"
       optionLabel="label"
       optionValue="value"
       @change="loadFamilyUnits"
     />
     ```
  6. Actualizar la funcion `loadFamilyUnits` para pasar el filtro:
     ```typescript
     const loadFamilyUnits = () => {
       fetchAllFamilyUnits({
         // ... existing params ...
         isActive: statusFilter.value === 'all' ? null : statusFilter.value === 'active',
       })
     }
     ```
  7. Agregar watcher para el filtro:
     ```typescript
     watch(statusFilter, () => loadFamilyUnits())
     ```

  #### 3c: Columna de estado en DataTable
  8. Agregar columna "Estado" entre las columnas existentes (despues de "Nº Familia"):
     ```vue
     <Column field="isActive" header="Estado" :sortable="true" style="min-width: 8rem">
       <template #body="{ data }">
         <Tag
           :value="data.isActive ? 'Activa' : 'Inactiva'"
           :severity="data.isActive ? 'success' : 'danger'"
         />
       </template>
     </Column>
     ```

  #### 3d: Botones de accion
  9. Reemplazar/extender la columna de acciones existente (actualmente solo tiene "ver detalle"):
     ```vue
     <Column header="Acciones" style="min-width: 12rem">
       <template #body="{ data }">
         <div class="flex gap-1">
           <!-- Ver detalle (existente) -->
           <Button
             icon="pi pi-eye"
             severity="info"
             text
             rounded
             v-tooltip.top="'Ver detalle'"
             @click="router.push(`/family-unit/${data.id}`)"
           />
           <!-- Desactivar / Activar -->
           <Button
             :icon="data.isActive ? 'pi pi-ban' : 'pi pi-check-circle'"
             :severity="data.isActive ? 'warn' : 'success'"
             text
             rounded
             v-tooltip.top="data.isActive ? 'Desactivar' : 'Activar'"
             @click="handleToggleStatus(data)"
           />
           <!-- Eliminar -->
           <Button
             icon="pi pi-trash"
             severity="danger"
             text
             rounded
             v-tooltip.top="'Eliminar'"
             @click="handleAdminDelete(data)"
           />
         </div>
       </template>
     </Column>
     ```

  #### 3e: Handlers con confirmacion
  10. Implementar `handleToggleStatus`:
      ```typescript
      const handleToggleStatus = (familyUnit: FamilyUnitResponse) => {
        const action = familyUnit.isActive ? 'desactivar' : 'activar'
        confirm.require({
          message: `¿Estás seguro de que deseas ${action} la unidad familiar "${familyUnit.name}"?`,
          header: `Confirmar ${action}`,
          icon: familyUnit.isActive ? 'pi pi-ban' : 'pi pi-check-circle',
          acceptLabel: 'Sí',
          rejectLabel: 'No',
          acceptClass: familyUnit.isActive ? 'p-button-warn' : 'p-button-success',
          accept: async () => {
            const result = await updateFamilyUnitStatus(familyUnit.id, !familyUnit.isActive)
            if (result) {
              toast.add({
                severity: 'success',
                summary: familyUnit.isActive ? 'Desactivada' : 'Activada',
                detail: `La unidad familiar "${familyUnit.name}" ha sido ${action}da`,
                life: 5000,
              })
              loadFamilyUnits()
            } else {
              toast.add({
                severity: 'error',
                summary: 'Error',
                detail: error.value || `No se pudo ${action} la unidad familiar`,
                life: 5000,
              })
            }
          },
        })
      }
      ```
  11. Implementar `handleAdminDelete`:
      ```typescript
      const handleAdminDelete = (familyUnit: FamilyUnitResponse) => {
        confirm.require({
          message: `¿Eliminar la unidad familiar "${familyUnit.name}"? Esta acción no se puede deshacer.`,
          header: 'Confirmar eliminación',
          icon: 'pi pi-exclamation-triangle',
          acceptLabel: 'Eliminar',
          rejectLabel: 'Cancelar',
          acceptClass: 'p-button-danger',
          accept: async () => {
            const result = await adminDeleteFamilyUnit(familyUnit.id)
            if (result) {
              toast.add({
                severity: 'success',
                summary: 'Eliminada',
                detail: `La unidad familiar "${familyUnit.name}" ha sido eliminada`,
                life: 5000,
              })
              loadFamilyUnits()
            } else {
              // El error 409 viene con mensaje del backend ("tiene inscripciones, desactive en su lugar")
              toast.add({
                severity: 'error',
                summary: 'No se pudo eliminar',
                detail: error.value || 'Error al eliminar la unidad familiar',
                life: 8000,
              })
            }
          },
        })
      }
      ```

  #### 3f: ConfirmDialog en template
  12. Agregar `<ConfirmDialog />` al template (si no existe ya):
      ```vue
      <template>
        <ConfirmDialog />
        <!-- ... rest of template ... -->
      </template>
      ```

---

### Step 4: Update FamilyMemberList — Boton eliminar para Admin/Board

- **File**: `frontend/src/components/family-units/FamilyMemberList.vue`
- **Action**: Hacer visible el boton de eliminar para Admin/Board en cualquier unidad familiar, con validacion de representante
- **Implementation Steps**:
  1. Agregar prop para indicar si el usuario es Admin/Board:
     ```typescript
     const props = defineProps<{
       members: FamilyMemberResponse[]
       loading?: boolean
       canManageMemberships?: boolean
       readOnly?: boolean
       uploadingMemberId?: string | null
       isAdminOrBoard?: boolean           // NUEVO
       representativeUserId?: string      // NUEVO - para saber quien es el representante
     }>()
     ```
  2. Actualizar la condicion de visibilidad del boton eliminar en la columna de acciones. Actualmente el boton se muestra con `v-if="!readOnly"`. Modificar para incluir Admin/Board:
     ```vue
     <!-- Delete button -->
     <Button
       v-if="!readOnly || isAdminOrBoard"
       :disabled="isRepresentative(data)"
       icon="pi pi-trash"
       severity="danger"
       text
       rounded
       v-tooltip.top="isRepresentative(data) ? 'No se puede eliminar al representante' : 'Eliminar'"
       @click="$emit('delete', data)"
     />
     ```
  3. Agregar computed helper para detectar representante:
     ```typescript
     const isRepresentative = (member: FamilyMemberResponse) => {
       return props.representativeUserId && member.userId === props.representativeUserId
     }
     ```
  4. **Nota**: La logica de confirmacion y la llamada API se manejan en el componente padre que escucha el evento `delete`. El componente `FamilyMemberList` solo emite el evento, no ejecuta la eliminacion directamente.

---

### Step 5: Update Parent Component — Pasar nuevas props a FamilyMemberList

- **File**: El componente padre que usa `FamilyMemberList` (probablemente la vista de detalle de la familia, ej: `FamilyUnitDetail.vue` o similar)
- **Action**: Pasar `isAdminOrBoard` y `representativeUserId` como props, y manejar el evento `delete` con confirmacion y validacion de registrations activas
- **Implementation Steps**:
  1. Importar auth store:
     ```typescript
     import { useAuthStore } from '@/stores/auth'
     const auth = useAuthStore()
     ```
  2. Pasar props al componente:
     ```vue
     <FamilyMemberList
       :members="familyMembers"
       :loading="loading"
       :read-only="!isRepresentative"
       :is-admin-or-board="auth.isBoard"
       :representative-user-id="familyUnit?.representativeUserId"
       @delete="handleDeleteMember"
       <!-- ... existing props/events ... -->
     />
     ```
  3. Manejar el evento `delete` con confirmacion:
     ```typescript
     const handleDeleteMember = (member: FamilyMemberResponse) => {
       confirm.require({
         message: `¿Eliminar al miembro "${member.firstName} ${member.lastName}"? Esta acción no se puede deshacer.`,
         header: 'Confirmar eliminación de miembro',
         icon: 'pi pi-exclamation-triangle',
         acceptLabel: 'Eliminar',
         rejectLabel: 'Cancelar',
         acceptClass: 'p-button-danger',
         accept: async () => {
           const result = await deleteFamilyMember(familyUnit.value!.id, member.id)
           if (result) {
             toast.add({
               severity: 'success',
               summary: 'Miembro eliminado',
               detail: `${member.firstName} ${member.lastName} ha sido eliminado`,
               life: 5000,
             })
             // Reload members
             await getFamilyMembers(familyUnit.value!.id)
           } else {
             // Error 409 si tiene registrations activas
             toast.add({
               severity: 'error',
               summary: 'No se pudo eliminar',
               detail: error.value || 'Error al eliminar el miembro',
               life: 8000,
             })
           }
         },
       })
     }
     ```
  4. **Nota**: `deleteFamilyMember` ya existe en el composable y llama al endpoint `DELETE /api/family-units/{familyUnitId}/members/{memberId}`. El backend ahora acepta Admin/Board ademas del representante, asi que no hace falta un metodo nuevo — el mismo endpoint funciona.

---

### Step 6: Update Technical Documentation

- **Action**: Actualizar documentacion tecnica
- **Implementation Steps**:
  1. **`ai-specs/specs/api-spec.yml`** (si existe):
     - Documentar nuevo endpoint `PATCH /api/family-units/{id}/status`
     - Documentar nuevo endpoint `DELETE /api/family-units/{id}/admin`
     - Actualizar schema `FamilyUnitResponse` con campo `isActive`
     - Documentar parametro query `?isActive` en `GET /api/family-units` admin
  2. **Verificar** que los nuevos componentes/metodos siguen las convenciones documentadas en `frontend-standards.mdc`
  3. Toda documentacion en ingles

---

## Implementation Order

1. **Step 0**: Create Feature Branch
2. **Step 1**: Update TypeScript Types — `isActive` en `FamilyUnitResponse`
3. **Step 2**: Update Composable — nuevos metodos API + filtro `isActive`
4. **Step 3**: Update FamilyUnitsAdminPanel — columna estado, acciones, filtro, confirmaciones
5. **Step 4**: Update FamilyMemberList — boton eliminar para Admin/Board
6. **Step 5**: Update Parent Component — pasar props y manejar delete con confirmacion
7. **Step 6**: Update Technical Documentation

---

## Testing Checklist

### Verificacion funcional manual

- [ ] El panel admin muestra la columna "Estado" con Tag verde (Activa) o rojo (Inactiva)
- [ ] El filtro por estado (Todas/Activas/Inactivas) filtra correctamente la tabla
- [ ] Boton "Desactivar" muestra dialogo de confirmacion y al aceptar cambia el estado
- [ ] Boton "Activar" muestra dialogo de confirmacion y al aceptar cambia el estado
- [ ] Boton "Eliminar" en una familia SIN inscripciones: dialogo → elimina → desaparece de la tabla
- [ ] Boton "Eliminar" en una familia CON inscripciones: dialogo → toast error con mensaje del backend (409)
- [ ] En detalle de familia, Admin/Board puede ver boton eliminar en cada miembro (excepto representante)
- [ ] Eliminar miembro sin registrations activas: funciona correctamente
- [ ] Eliminar miembro con registrations activas: toast error con mensaje del backend (409)
- [ ] Toast de exito aparece tras cada operacion exitosa
- [ ] Toast de error aparece con mensaje descriptivo del backend en cada fallo

### Vitest unit tests

- [ ] `useFamilyUnits` — `adminDeleteFamilyUnit`: retorna `true` en 204, `false` en 409/404
- [ ] `useFamilyUnits` — `updateFamilyUnitStatus`: retorna `FamilyUnitResponse` en 200, `null` en error
- [ ] `useFamilyUnits` — `fetchAllFamilyUnits` con parametro `isActive`: envia query param correctamente

### Cypress E2E tests (si aplica)

- [ ] Admin puede desactivar y reactivar una familia
- [ ] Admin puede eliminar una familia sin inscripciones
- [ ] Admin no puede eliminar una familia con inscripciones (error 409 visible)

---

## Error Handling Patterns

| Escenario | Comportamiento frontend |
|---|---|
| Delete familia sin inscripciones (204) | Toast success + reload tabla |
| Delete familia con inscripciones (409) | Toast error con mensaje backend: "No se puede eliminar... Desactivela en su lugar" |
| Delete familia no encontrada (404) | Toast error "Unidad familiar no encontrada" |
| Toggle status exitoso (200) | Toast success + reload tabla |
| Toggle status no encontrada (404) | Toast error |
| Delete miembro con registrations activas (409) | Toast error con mensaje backend |
| Delete representante (409) | Boton deshabilitado con tooltip explicativo, nunca llega al backend |
| Error de red / 500 | Toast error generico |

Todos los errores del backend llegan con mensaje descriptivo en `err.response?.data?.error?.message` gracias al envelope `ApiResponse<T>`.

---

## UI/UX Considerations

### PrimeVue Components utilizados

| Componente | Uso |
|---|---|
| `Tag` | Badge de estado (Activa/Inactiva) con severity `success`/`danger` |
| `Button` | Botones de accion: `text rounded` con iconos |
| `ConfirmDialog` | Dialogo de confirmacion para acciones destructivas |
| `SelectButton` | Filtro por estado (Todas/Activas/Inactivas) |
| `Toast` | Notificaciones de exito/error |

### Layout y responsive

- Botones de accion en `flex gap-1` para alineacion horizontal
- Columna acciones con `min-width: 12rem` para acomodar 3 botones
- Tag de estado usa `min-width: 8rem` para consistencia visual
- En mobile los botones de accion mantienen iconos sin label (ya son `text rounded`)

### Accesibilidad

- Todos los botones de accion tienen `v-tooltip` descriptivo
- Boton eliminar representante esta `disabled` con tooltip explicativo
- Iconos descriptivos: `pi pi-ban` (desactivar), `pi pi-check-circle` (activar), `pi pi-trash` (eliminar)
- Dialogos de confirmacion con `header`, `message`, e `icon` claros

### Feedback al usuario

- Loading state durante operaciones API (ya manejado por el composable)
- Toast con `life: 5000` para exito, `life: 8000` para errores (mas tiempo para leer)
- Mensaje de error 409 del backend es descriptivo y sugiere la accion alternativa

---

## Dependencies

- **npm packages**: Ninguno nuevo. Todo usa PrimeVue y Vue ya instalados.
- **PrimeVue components**: `Tag`, `ConfirmDialog`, `SelectButton`, `Button`, `Toast` — todos ya disponibles en el proyecto
- **Imports adicionales**: `useConfirm` de `primevue/useconfirm`, `useToast` de `primevue/usetoast`

---

## Notes

- **Orden de implementacion**: El backend debe estar implementado primero. Los endpoints `DELETE /admin`, `PATCH /status`, y el parametro `?isActive` en GET deben existir antes de probar el frontend.
- **Idioma UI**: Todos los textos de la interfaz en español (consistente con el resto de la app).
- **El composable `deleteFamilyMember` ya existe**: No hace falta crear uno nuevo para Admin/Board. El mismo endpoint ahora acepta ambos roles en el backend.
- **No crear componentes nuevos**: Todo se integra en componentes existentes. No hay necesidad de nuevos archivos Vue.
- **TypeScript strict**: No usar `any`. Todos los tipos estan definidos en `types/family-unit.ts`.
- **Sin `<style>` blocks**: Todo el styling via Tailwind CSS utilities y PrimeVue component props.

---

## Next Steps After Implementation

1. Crear PR hacia `dev` con los cambios frontend
2. Testing de integracion con el backend (verificar que los endpoints responden correctamente)
3. QA manual: verificar flujo completo (desactivar → verificar que no puede inscribirse → reactivar → puede inscribirse)
4. Verificar que el filtro por estado funciona con paginacion

---

## Implementation Verification

- [ ] **Code Quality**: TypeScript strict, no `any`, `<script setup lang="ts">`, sin `<style>` blocks
- [ ] **Functionality**: Panel admin muestra estado, filtro funciona, acciones con confirmacion operan correctamente
- [ ] **Testing**: Vitest tests para composable, verificacion manual de UI
- [ ] **Integration**: Composable conecta correctamente con los endpoints backend
- [ ] **Documentation**: Documentacion actualizada
- [ ] **UX**: Toast notifications claras, dialogos de confirmacion descriptivos, boton representante deshabilitado

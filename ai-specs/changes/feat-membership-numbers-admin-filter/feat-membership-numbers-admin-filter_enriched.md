# Números de socio/a y familia + Filtro admin de familias sin alta

## Descripción

Como administrador/junta, necesito poder identificar rápidamente qué familias aún no están dadas de alta como socias, y al darles de alta, asignarles un número de familia socia y un número de socio/a individual a cada miembro. Estos números deben ser únicos, secuenciales, auto-asignados y editables por admin.

## Contexto

Actualmente el panel de administración de familias (`/admin/family-units`) muestra todas las familias sin distinguir si son socias o no. Además, el sistema de membresías no asigna ningún número identificador humano — solo usa GUIDs internos. Esto dificulta la gestión administrativa y la comunicación con los socios/as.

## Requisitos funcionales

### 1. Número de familia socia (`familyNumber`)

- Campo `int?` en la entidad `FamilyUnit`
- Se auto-asigna (MAX + 1) cuando se activa la **primera** membresía de cualquier miembro de la familia
- NO se reasigna si la familia ya tiene número
- Editable por Admin/Board vía endpoint dedicado
- Debe ser **único** (constraint en BD, filtrado a valores no nulos)
- Se muestra en el panel admin de familias

### 2. Número de socio/a (`memberNumber`)

- Campo `int?` en la entidad `Membership`
- Se auto-asigna (MAX + 1) al crear una membresía (individual o bulk)
- Editable por Admin/Board vía endpoint dedicado
- Debe ser **único** (constraint en BD, filtrado a valores no nulos)
- Se muestra en el perfil y en la información de membresía

### 3. Filtro de familias por estado de membresía

- Añadir filtro al panel existente `FamilyUnitsAdminPanel` con opciones: "Todas", "Socias", "No socias"
- El filtro se envía como query param `membershipStatus` al endpoint existente `GET /api/family-units`
- Valores: `all` (default), `active`, `none`

## Cambios backend

### Entidades (Models)

| Archivo | Cambio |
|---|---|
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs` | Añadir `int? FamilyNumber` a `FamilyUnit`, `FamilyUnitAdminProjection`, `FamilyUnitListItemResponse`, `FamilyUnitResponse`. Nuevo DTO `UpdateFamilyNumberRequest(int FamilyNumber)` |
| `src/Abuvi.API/Features/Memberships/MembershipsModels.cs` | Añadir `int? MemberNumber` a `Membership` y `MembershipResponse`. Nuevo DTO `UpdateMemberNumberRequest(int MemberNumber)` |

### Configuración BD

| Archivo | Cambio |
|---|---|
| `src/Abuvi.API/Data/Configurations/FamilyUnitConfiguration.cs` | Añadir propiedad `family_number` con unique filtered index (`WHERE family_number IS NOT NULL`) |
| `src/Abuvi.API/Data/Configurations/MembershipConfiguration.cs` | Añadir propiedad `member_number` con unique filtered index (`WHERE member_number IS NOT NULL`) |

### Migración

Generar con `dotnet ef migrations add AddFamilyAndMemberNumbers`

### Repositorios

| Archivo | Cambio |
|---|---|
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs` | Nuevos métodos: `GetNextFamilyNumberAsync`, `IsFamilyNumberTakenAsync`. Añadir param `membershipStatus` a `GetAllPagedAsync` con filtro por membresías activas |
| `src/Abuvi.API/Features/Memberships/MembershipsRepository.cs` | Nuevos métodos: `GetNextMemberNumberAsync`, `IsMemberNumberTakenAsync` |

### Servicios

| Archivo | Cambio |
|---|---|
| `src/Abuvi.API/Features/Memberships/MembershipsService.cs` | En `CreateAsync`: asignar `MemberNumber` y `FamilyNumber` (si la familia no tiene). En `BulkActivateAsync`: misma lógica. Nuevo método `UpdateMemberNumberAsync` con validación de unicidad |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs` | Pasar `membershipStatus` al repo. Nuevo método `UpdateFamilyNumberAsync` con validación de unicidad |

### Endpoints

| Archivo | Cambio |
|---|---|
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Añadir `[FromQuery] string? membershipStatus` a `GetAllFamilyUnits`. Nuevo endpoint `PUT /api/family-units/{id}/family-number` (Admin/Board) |
| `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs` | Nuevo endpoint `PUT /api/memberships/{membershipId}/member-number` (Admin/Board) |

### Validadores (nuevos archivos)

| Archivo | Validación |
|---|---|
| `src/Abuvi.API/Features/FamilyUnits/UpdateFamilyNumberValidator.cs` | `FamilyNumber > 0` |
| `src/Abuvi.API/Features/Memberships/UpdateMemberNumberValidator.cs` | `MemberNumber > 0` |

## Cambios frontend

### Tipos

| Archivo | Cambio |
|---|---|
| `frontend/src/types/family-unit.ts` | Añadir `familyNumber: number \| null` a interfaces relevantes |
| `frontend/src/types/membership.ts` | Añadir `memberNumber: number \| null` a `MembershipResponse` |

### Composables

| Archivo | Cambio |
|---|---|
| `frontend/src/composables/useFamilyUnits.ts` | Añadir param `membershipStatus` a `fetchAllFamilyUnits`. Nuevo método `updateFamilyNumber` |
| `frontend/src/composables/useMemberships.ts` | Nuevo método `updateMemberNumber` |

### Componentes

| Archivo | Cambio |
|---|---|
| `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` | Añadir `SelectButton` con filtros "Todas" / "Socias" / "No socias". Añadir columna "Nº Familia". Pasar filtro al fetch |

## Concurrencia

La combinación de `MAX + 1` + unique constraint en BD es suficiente para este volumen de uso. Si dos requests concurrentes intentan el mismo número, una fallará por constraint y se puede reintentar.

## Criterios de aceptación

- [ ] Al activar la primera membresía de una familia, se asigna automáticamente un `familyNumber` secuencial
- [ ] Al activar cualquier membresía (individual o bulk), se asigna un `memberNumber` secuencial
- [ ] Los números son únicos (validado por BD y por servicio)
- [ ] Admin/Board puede editar ambos números desde endpoints dedicados, con validación de unicidad
- [ ] El panel admin de familias permite filtrar por "Todas", "Socias", "No socias"
- [ ] El panel admin muestra columna "Nº Familia"
- [ ] Los números se muestran en las respuestas de API de membresía y familia

## Verificación

1. Crear una membresía para un miembro → verificar que se asignan `memberNumber` y `familyNumber`
2. Crear membresía para segundo miembro de la misma familia → verificar que `familyNumber` no cambia
3. Usar bulk activate → verificar asignación correcta de números
4. Editar números vía PUT endpoints → verificar que funciona y que duplicados son rechazados
5. Filtrar familias en admin panel → verificar que los filtros devuelven resultados correctos
6. Ejecutar tests backend y frontend

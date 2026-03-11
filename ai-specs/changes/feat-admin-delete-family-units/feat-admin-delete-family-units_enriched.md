# Admin/Board: Eliminar unidades familiares y miembros individuales

## User Story

**Como** usuario con rol Admin o Board,
**Quiero** poder eliminar unidades familiares que no tengan inscripciones, desactivar las que sí las tengan, y eliminar miembros familiares de manera individual,
**Para** mantener limpia la base de datos de familias y corregir errores de registro sin depender del representante.

---

## Reglas de Negocio

### Doble control de acceso a inscripciones

El sistema aplica **dos controles independientes** para que una familia pueda inscribirse:

1. **Control administrativo (`IsActive`)**: La unidad familiar debe estar activa. Admin/Board puede desactivar una familia por cualquier motivo (baja voluntaria, incidencia, etc.) independientemente de si ha pagado o no.
2. **Control economico (cuota de membresia)**: Al menos un miembro de la familia debe tener la `MembershipFee` del año en curso con status `Paid`. Esto garantiza que solo familias al dia con sus cuotas puedan inscribirse.

Ambas condiciones deben cumplirse para crear una inscripcion. Son independientes porque cubren escenarios distintos:

- Una familia puede tener la cuota pagada pero estar desactivada por una incidencia administrativa.
- Una familia puede estar activa pero no haber pagado la cuota del año (ej: renovacion pendiente).
- Admin/Board puede marcar la cuota como pagada (flujo existente) para "abrir" el acceso economico.

### Flujo Board para habilitar inscripcion de una familia

1. Verificar que la familia tiene `IsActive = true` (si no, reactivarla via `PATCH /api/family-units/{id}/status`)
2. Verificar que la cuota del año en curso esta pagada (si no, marcarla como pagada via `POST /api/memberships/{id}/fees/{feeId}/pay` — endpoint ya existente)
3. Con ambos controles cumplidos, el representante puede crear inscripciones normalmente

### Eliminacion de Unidad Familiar

| Condicion | Accion permitida |
|---|---|
| Sin inscripciones (0 registrations) | **Eliminar** (hard delete, cascade a members) |
| Con inscripciones (>= 1 registration) | **Desactivar** (soft-disable via `IsActive = false`) |

- Solo roles **Admin** y **Board** pueden ejecutar estas acciones.
- El representante (Member) NO puede desactivar — solo eliminar su propia unidad como ya existe.
- Al desactivar una unidad familiar, los usuarios vinculados mantienen sus cuentas pero no pueden crear nuevas inscripciones desde esa unidad.
- Una unidad desactivada debe poder reactivarse por Admin/Board.

### Eliminacion de Miembro Familiar Individual (Admin/Board)

- Admin/Board pueden eliminar cualquier miembro de cualquier unidad familiar.
- No se puede eliminar al representante (el miembro vinculado a `RepresentativeUserId`) — igual que la regla existente.
- No se puede eliminar un miembro que tenga `RegistrationMember` entries en registrations activas (status Pending/Confirmed). Si solo tiene registrations en Draft/Cancelled, se permite.
- El endpoint existente `DELETE /api/family-units/{familyUnitId}/members/{memberId}` debe extenderse para aceptar Admin/Board ademas del representante.

---

## Cambios en el Modelo de Datos

### Nuevo campo: `FamilyUnit.IsActive`

```csharp
// FamilyUnitsModels.cs
public bool IsActive { get; set; } = true;
```

**Migracion EF Core:**

```csharp
// FamilyUnitConfiguration.cs
builder.Property(fu => fu.IsActive)
    .HasColumnName("is_active")
    .HasDefaultValue(true)
    .IsRequired();
```

**Migracion SQL generada esperada:**

```sql
ALTER TABLE family_units ADD COLUMN is_active boolean NOT NULL DEFAULT true;
```

---

## Endpoints

### 1. `DELETE /api/family-units/{id}/admin` — Eliminar unidad familiar (Admin/Board)

**Grupo:** `adminGroup` (ya requiere rol Admin/Board)

| Aspecto | Detalle |
|---|---|
| Ruta | `DELETE /api/family-units/{id}/admin` |
| Auth | Admin, Board |
| Validacion | Verificar que la unidad NO tenga registrations asociadas |
| Accion | Hard delete (cascade a members, limpia `User.FamilyUnitId` de todos los usuarios vinculados) |
| Response 204 | Eliminacion exitosa |
| Response 404 | Unidad familiar no encontrada |
| Response 409 | Unidad tiene inscripciones — no se puede eliminar, sugerir desactivar |

**Logica del Service:**

```csharp
public async Task AdminDeleteFamilyUnitAsync(Guid familyUnitId, CancellationToken ct)
{
    var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
        ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

    var hasRegistrations = await repository.HasRegistrationsAsync(familyUnitId, ct);
    if (hasRegistrations)
        throw new ConflictException("No se puede eliminar una unidad familiar con inscripciones. Desactivela en su lugar.");

    // Clear FamilyUnitId for all linked users (representative + linked members)
    await repository.ClearAllUserFamilyUnitLinksAsync(familyUnitId, ct);

    // Hard delete (cascade deletes members)
    await repository.DeleteFamilyUnitAsync(familyUnitId, ct);
}
```

### 2. `PATCH /api/family-units/{id}/status` — Activar/Desactivar unidad familiar

**Grupo:** `adminGroup`

| Aspecto | Detalle |
|---|---|
| Ruta | `PATCH /api/family-units/{id}/status` |
| Auth | Admin, Board |
| Body | `{ "isActive": false }` |
| Accion | Cambia `IsActive` de la unidad familiar |
| Response 200 | `ApiResponse<FamilyUnitResponse>` con el estado actualizado |
| Response 404 | Unidad familiar no encontrada |

**Request DTO:**

```csharp
public record UpdateFamilyUnitStatusRequest(bool IsActive);
```

### 3. Extender `DELETE /api/family-units/{familyUnitId}/members/{memberId}` — Admin/Board access

**Cambio en el endpoint existente (linea ~300 de FamilyUnitsEndpoints.cs):**

Actualmente solo permite al representante. Debe extenderse para permitir Admin/Board:

```csharp
// Antes:
var isRepresentative = await service.IsRepresentativeAsync(familyUnitId, userId, ct);
if (!isRepresentative)
    return TypedResults.Forbid();

// Despues:
var userRole = user.GetUserRole();
var isAdminOrBoard = userRole == "Admin" || userRole == "Board";
var isRepresentative = await service.IsRepresentativeAsync(familyUnitId, userId, ct);
if (!isRepresentative && !isAdminOrBoard)
    return TypedResults.Forbid();
```

**Validacion adicional para Admin/Board:** Verificar que el miembro no tenga `RegistrationMember` en registrations con status Pending o Confirmed.

---

## Cambios en el Repository

### Nuevos metodos en `FamilyUnitsRepository`

```csharp
/// Verifica si la unidad familiar tiene alguna registration asociada
public async Task<bool> HasRegistrationsAsync(Guid familyUnitId, CancellationToken ct)
{
    return await db.Registrations
        .AnyAsync(r => r.FamilyUnitId == familyUnitId, ct);
}

/// Limpia FamilyUnitId de todos los users vinculados a esta unidad
public async Task ClearAllUserFamilyUnitLinksAsync(Guid familyUnitId, CancellationToken ct)
{
    await db.Users
        .Where(u => u.FamilyUnitId == familyUnitId)
        .ExecuteUpdateAsync(u => u.SetProperty(x => x.FamilyUnitId, (Guid?)null), ct);
}

/// Actualiza IsActive de la unidad familiar
public async Task UpdateFamilyUnitStatusAsync(Guid familyUnitId, bool isActive, CancellationToken ct)
{
    var rows = await db.FamilyUnits
        .Where(fu => fu.Id == familyUnitId)
        .ExecuteUpdateAsync(fu => fu.SetProperty(x => x.IsActive, isActive), ct);

    if (rows == 0)
        throw new NotFoundException("Unidad Familiar", familyUnitId);
}

/// Verifica si un miembro tiene registrations activas (Pending/Confirmed)
public async Task<bool> MemberHasActiveRegistrationsAsync(Guid memberId, CancellationToken ct)
{
    return await db.RegistrationMembers
        .AnyAsync(rm => rm.FamilyMemberId == memberId
            && (rm.Registration.Status == RegistrationStatus.Pending
                || rm.Registration.Status == RegistrationStatus.Confirmed), ct);
}
```

---

## Impacto de `IsActive` en funcionalidad existente

### Backend — Filtrar unidades desactivadas

| Lugar | Cambio |
|---|---|
| `GET /api/family-units` (admin list) | Incluir campo `isActive` en response. Agregar filtro opcional `?isActive=true/false` |
| `POST /api/registrations` | Validar que `FamilyUnit.IsActive == true` Y que la familia tenga la cuota del año pagada. Retornar 409 con mensaje especifico para cada caso |
| `FamilyUnitResponse` DTO | Agregar `public bool IsActive { get; set; }` |

### Backend — Validacion en `RegistrationsService.CreateAsync`

Agregar dos validaciones nuevas despues de cargar la FamilyUnit (entre paso 1 y paso 2 actual):

```csharp
// 1. Load FamilyUnit
var familyUnit = await familyUnitsRepo.GetFamilyUnitByIdAsync(request.FamilyUnitId, ct)
    ?? throw new NotFoundException("Unidad Familiar", request.FamilyUnitId);

// 1b. Validate family unit is active (NEW)
if (!familyUnit.IsActive)
    throw new BusinessRuleException("La unidad familiar está desactivada. Contacte al administrador.");

// 1c. Validate current year membership fee is paid (NEW)
var hasPaidCurrentYearFee = await membershipsRepo.HasPaidCurrentYearFeeForFamilyAsync(request.FamilyUnitId, ct);
if (!hasPaidCurrentYearFee)
    throw new BusinessRuleException("La cuota de membresía del año en curso no está pagada. Contacte al administrador.");

// 2. Verify representative (existing)
```

### Nuevo metodo en `MembershipsRepository`

```csharp
/// Verifica si al menos un miembro de la familia tiene la cuota del año en curso pagada
public async Task<bool> HasPaidCurrentYearFeeForFamilyAsync(Guid familyUnitId, CancellationToken ct)
{
    var currentYear = DateTime.UtcNow.Year;
    return await db.MembershipFees
        .AnyAsync(f => f.Membership.FamilyMember.FamilyUnitId == familyUnitId
            && f.Year == currentYear
            && f.Status == FeeStatus.Paid
            && f.Membership.IsActive, ct);
}
```

### Inyeccion de dependencia en `RegistrationsService`

Agregar `IMembershipsRepository membershipsRepo` al constructor de `RegistrationsService` para poder consultar el estado de cuotas.

### Frontend — No bloquear la visualizacion

Las unidades desactivadas deben seguir siendo visibles en el panel admin pero marcadas visualmente. No deben poder crear nuevas inscripciones.

---

## Cambios Frontend

### 1. `FamilyUnitsAdminPanel.vue` — Agregar acciones

**Nuevos botones por fila:**

| Boton | Icono | Condicion visible | Accion |
|---|---|---|---|
| Desactivar | `pi pi-ban` | `isActive === true` | PATCH status `isActive: false` |
| Activar | `pi pi-check-circle` | `isActive === false` | PATCH status `isActive: true` |
| Eliminar | `pi pi-trash` | Siempre visible | DELETE (muestra 409 si tiene inscripciones) |
| Eliminar miembro | (dentro del detalle) | Per-member | DELETE member |

**Patron de confirmacion:** Usar `useConfirm()` de PrimeVue (patron existente en el proyecto):

```typescript
confirm.require({
  message: '¿Eliminar la unidad familiar? Esta accion no se puede deshacer.',
  header: 'Confirmar eliminacion',
  icon: 'pi pi-exclamation-triangle',
  acceptLabel: 'Eliminar',
  rejectLabel: 'Cancelar',
  acceptClass: 'p-button-danger',
  accept: async () => { /* call API */ },
})
```

**Indicador visual para desactivadas:** Badge o tag "Inactiva" en rojo junto al nombre de la familia.

### 2. `useFamilyUnits.ts` — Nuevos metodos en composable

```typescript
const updateFamilyUnitStatus = async (id: string, isActive: boolean): Promise<boolean> => { ... }
const adminDeleteFamilyUnit = async (id: string): Promise<boolean> => { ... }
// deleteFamilyMember ya existe — solo necesita funcionar con el nuevo auth
```

### 3. `FamilyMemberList.vue` — Boton eliminar para Admin/Board

Agregar boton de eliminar por cada miembro (excepto el representante) cuando el usuario actual es Admin/Board. Usar mismo patron de confirmacion.

### 4. Filtro en admin panel

Agregar filtro por estado (Activas / Inactivas / Todas) al panel de administracion, similar al filtro de membership existente.

---

## Archivos a Modificar

### Backend

| Archivo | Cambio |
|---|---|
| `Features/FamilyUnits/FamilyUnitsModels.cs` | Agregar `IsActive` a `FamilyUnit` |
| `Data/Configurations/FamilyUnitConfiguration.cs` | Configurar `is_active` column |
| `Features/FamilyUnits/FamilyUnitsRepository.cs` | Nuevos metodos: `HasRegistrationsAsync`, `ClearAllUserFamilyUnitLinksAsync`, `UpdateFamilyUnitStatusAsync`, `MemberHasActiveRegistrationsAsync` |
| `Features/FamilyUnits/FamilyUnitsService.cs` | Nuevos metodos: `AdminDeleteFamilyUnitAsync`, `UpdateFamilyUnitStatusAsync`. Modificar `DeleteFamilyMemberAsync` para aceptar Admin/Board |
| `Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Nuevos endpoints en `adminGroup`. Modificar auth en `DeleteFamilyMember` |
| `Features/FamilyUnits/FamilyUnitsDtos.cs` | Agregar `IsActive` a `FamilyUnitResponse`. Nuevo `UpdateFamilyUnitStatusRequest` |
| `Features/Registrations/RegistrationsService.cs` | Validar `FamilyUnit.IsActive` y cuota de membresia del año pagada al crear registration. Inyectar `IMembershipsRepository` |
| `Features/Memberships/MembershipsRepository.cs` | Nuevo metodo `HasPaidCurrentYearFeeForFamilyAsync` |
| **Nueva migracion EF Core** | `AddIsActiveToFamilyUnits` |

### Frontend

| Archivo | Cambio |
|---|---|
| `components/admin/FamilyUnitsAdminPanel.vue` | Botones eliminar/desactivar/activar, filtro por estado, badge inactiva |
| `composables/useFamilyUnits.ts` | Nuevos metodos `updateFamilyUnitStatus`, `adminDeleteFamilyUnit` |
| `components/family-units/FamilyMemberList.vue` | Boton eliminar miembro para Admin/Board |
| `types/family-unit.ts` | Agregar `isActive` a `FamilyUnitResponse` |

### Tests

| Archivo | Contenido |
|---|---|
| `Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsService_AdminDeleteAsync_Tests.cs` | Tests para delete: sin registrations OK, con registrations 409, not found 404 |
| `Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsService_UpdateStatusAsync_Tests.cs` | Tests para activar/desactivar |
| `Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsService_DeleteMemberAsync_Tests.cs` | Tests para delete member con nuevo auth Admin/Board y validacion de registrations activas |
| `Abuvi.Tests/Unit/Features/Registrations/RegistrationsService_CreateAsync_MembershipValidation_Tests.cs` | Tests para validacion de IsActive y cuota pagada al crear registration |

---

## Criterios de Aceptacion

- [ ] Admin/Board puede eliminar una unidad familiar que no tiene inscripciones
- [ ] Al intentar eliminar una unidad con inscripciones, retorna 409 con mensaje claro
- [ ] Admin/Board puede desactivar una unidad familiar con inscripciones
- [ ] Admin/Board puede reactivar una unidad familiar desactivada
- [ ] Una unidad desactivada no permite crear nuevas inscripciones (409 en POST /registrations con mensaje "unidad desactivada")
- [ ] Una familia sin cuota del año pagada no permite crear nuevas inscripciones (409 en POST /registrations con mensaje "cuota no pagada")
- [ ] Una familia activa con cuota pagada puede inscribirse normalmente (ambos controles pasan)
- [ ] Board puede "abrir" acceso: reactivar familia + marcar cuota como pagada (flujo de 2 pasos)
- [ ] Las unidades desactivadas aparecen marcadas visualmente en el panel admin
- [ ] Admin/Board puede eliminar miembros individuales de cualquier unidad familiar
- [ ] No se puede eliminar al representante (409)
- [ ] No se puede eliminar un miembro con registrations activas (Pending/Confirmed)
- [ ] Todos los cambios tienen tests unitarios
- [ ] Migracion EF Core creada y probada
- [ ] Dialogo de confirmacion antes de cada accion destructiva en el frontend

---

## Requisitos No Funcionales

- **Seguridad**: Todos los endpoints nuevos requieren autenticacion + rol Admin/Board
- **Auditoria**: Loggear todas las eliminaciones y cambios de estado con `logger.LogInformation`
- **Cascade**: Al eliminar unidad, limpiar `FamilyUnitId` de TODOS los usuarios vinculados (no solo el representante, tambien members con UserId linkado)
- **Consistencia**: Las validaciones de registrations deben ser atomicas (no race conditions entre check y delete)

# Backend Implementation Plan: feat-admin-delete-family-units

## Overview

Implementar funcionalidades administrativas para que usuarios con rol Admin/Board puedan eliminar unidades familiares sin inscripciones, desactivar las que tengan inscripciones, reactivarlas, y eliminar miembros individuales. Ademas, agregar un doble control de acceso a inscripciones: control administrativo (`IsActive`) y control economico (cuota de membresia del año pagada).

Sigue Vertical Slice Architecture, extendiendo la feature slice `FamilyUnits` existente y modificando `Registrations` y `Memberships`.

---

## Architecture Context

- **Feature slice principal**: `src/Abuvi.API/Features/FamilyUnits/`
- **Feature slices afectadas**: `Features/Registrations/`, `Features/Memberships/`
- **Archivos a modificar**:
  - `FamilyUnitsModels.cs` — nuevo campo `IsActive`
  - `FamilyUnitsDtos.cs` — nuevo DTO request + campo en response
  - `FamilyUnitsRepository.cs` — nuevos metodos de consulta
  - `FamilyUnitsService.cs` — nuevos metodos de negocio
  - `FamilyUnitsEndpoints.cs` — nuevos endpoints admin + modificar auth en delete member
  - `Data/Configurations/FamilyUnitConfiguration.cs` — configurar columna `is_active`
  - `Features/Registrations/RegistrationsService.cs` — validaciones de `IsActive` y cuota pagada
  - `Features/Memberships/MembershipsRepository.cs` — nuevo metodo `HasPaidCurrentYearFeeForFamilyAsync`
- **Nueva migracion EF Core**

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Crear branch dedicado para el backend
- **Branch Name**: `feature/feat-admin-delete-family-units-backend`
- **Implementation Steps**:
  1. `git checkout dev`
  2. `git pull origin dev`
  3. `git checkout -b feature/feat-admin-delete-family-units-backend`
  4. `git branch` — verificar

---

### Step 1: Update Entity — Agregar `IsActive` a `FamilyUnit`

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`
- **Action**: Agregar propiedad `IsActive` al entity `FamilyUnit`
- **Implementation Steps**:
  1. Agregar al entity `FamilyUnit`:
     ```csharp
     public bool IsActive { get; set; } = true;
     ```
  2. Ubicarla despues de `FamilyNumber` y antes de `ProfilePhotoUrl` para mantener orden logico

---

### Step 2: Update EF Core Configuration

- **File**: `src/Abuvi.API/Data/Configurations/FamilyUnitConfiguration.cs`
- **Action**: Configurar la nueva columna `is_active`
- **Implementation Steps**:
  1. Agregar en el metodo `Configure()`:
     ```csharp
     builder.Property(fu => fu.IsActive)
         .HasColumnName("is_active")
         .HasDefaultValue(true)
         .IsRequired();
     ```
  2. Ubicarlo despues de la configuracion de `FamilyNumber`

---

### Step 3: Update DTOs

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsDtos.cs`
- **Action**: Agregar `IsActive` al response y crear nuevo request DTO
- **Implementation Steps**:
  1. Agregar `IsActive` al record `FamilyUnitResponse`:
     ```csharp
     public record FamilyUnitResponse(
         Guid Id,
         string Name,
         Guid RepresentativeUserId,
         int? FamilyNumber,
         bool IsActive,          // NUEVO
         string? ProfilePhotoUrl,
         DateTime CreatedAt,
         DateTime UpdatedAt);
     ```
  2. Agregar `IsActive` a `FamilyUnitAdminProjection` y `FamilyUnitListItemResponse` si aplica
  3. Crear nuevo request DTO:
     ```csharp
     public record UpdateFamilyUnitStatusRequest(bool IsActive);
     ```
  4. Actualizar el extension method `ToResponse()` en `FamilyUnit` para incluir `IsActive`:
     ```csharp
     public static FamilyUnitResponse ToResponse(this FamilyUnit fu) => new(
         fu.Id, fu.Name, fu.RepresentativeUserId, fu.FamilyNumber,
         fu.IsActive,    // NUEVO
         fu.ProfilePhotoUrl, fu.CreatedAt, fu.UpdatedAt);
     ```

---

### Step 4: Create Validator for `UpdateFamilyUnitStatusRequest`

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsValidator.cs` (o archivo de validators existente)
- **Action**: Crear validator con FluentValidation
- **Implementation Steps**:
  1. Crear validator:
     ```csharp
     public class UpdateFamilyUnitStatusValidator : AbstractValidator<UpdateFamilyUnitStatusRequest>
     {
         public UpdateFamilyUnitStatusValidator()
         {
             // IsActive es bool, no necesita validacion adicional mas alla del binding
         }
     }
     ```
  2. **Nota**: El validator puede ser minimo ya que `bool` no tiene rangos invalidos. Se puede omitir si el proyecto no requiere validators para todos los DTOs — confirmar con el patron existente

---

### Step 5: Update Repository — Nuevos metodos en `FamilyUnitsRepository`

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`
- **Action**: Agregar metodos al interface y a la implementacion
- **Implementation Steps**:
  1. Agregar al interface `IFamilyUnitsRepository`:
     ```csharp
     Task<bool> HasRegistrationsAsync(Guid familyUnitId, CancellationToken ct);
     Task ClearAllUserFamilyUnitLinksAsync(Guid familyUnitId, CancellationToken ct);
     Task UpdateFamilyUnitStatusAsync(Guid familyUnitId, bool isActive, CancellationToken ct);
     Task<bool> MemberHasActiveRegistrationsAsync(Guid memberId, CancellationToken ct);
     ```
  2. Implementar `HasRegistrationsAsync`:
     ```csharp
     public async Task<bool> HasRegistrationsAsync(Guid familyUnitId, CancellationToken ct)
     {
         return await db.Registrations
             .AnyAsync(r => r.FamilyUnitId == familyUnitId, ct);
     }
     ```
  3. Implementar `ClearAllUserFamilyUnitLinksAsync`:
     ```csharp
     public async Task ClearAllUserFamilyUnitLinksAsync(Guid familyUnitId, CancellationToken ct)
     {
         await db.Users
             .Where(u => u.FamilyUnitId == familyUnitId)
             .ExecuteUpdateAsync(u => u.SetProperty(x => x.FamilyUnitId, (Guid?)null), ct);
     }
     ```
  4. Implementar `UpdateFamilyUnitStatusAsync`:
     ```csharp
     public async Task UpdateFamilyUnitStatusAsync(Guid familyUnitId, bool isActive, CancellationToken ct)
     {
         var familyUnit = await db.FamilyUnits.FindAsync([familyUnitId], ct)
             ?? throw new NotFoundException("Unidad Familiar", familyUnitId);
         familyUnit.IsActive = isActive;
         familyUnit.UpdatedAt = DateTime.UtcNow;
         await db.SaveChangesAsync(ct);
     }
     ```
     **Nota**: Usar `FindAsync` + `SaveChangesAsync` para mantener consistencia con el patron existente del repo (vs `ExecuteUpdateAsync` que bypasea change tracking). Verificar cual usa el repo actualmente y seguir ese patron.
  5. Implementar `MemberHasActiveRegistrationsAsync`:
     ```csharp
     public async Task<bool> MemberHasActiveRegistrationsAsync(Guid memberId, CancellationToken ct)
     {
         return await db.RegistrationMembers
             .AnyAsync(rm => rm.FamilyMemberId == memberId
                 && (rm.Registration.Status == RegistrationStatus.Pending
                     || rm.Registration.Status == RegistrationStatus.Confirmed), ct);
     }
     ```

---

### Step 6: Update Service — Nuevos metodos en `FamilyUnitsService`

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`
- **Action**: Agregar metodos de negocio para admin delete, status update, y modificar delete member
- **Implementation Steps**:
  1. **`AdminDeleteFamilyUnitAsync`**:
     ```csharp
     public async Task AdminDeleteFamilyUnitAsync(Guid familyUnitId, CancellationToken ct)
     {
         var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
             ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

         var hasRegistrations = await repository.HasRegistrationsAsync(familyUnitId, ct);
         if (hasRegistrations)
             throw new ConflictException(
                 "No se puede eliminar una unidad familiar con inscripciones. Desactívela en su lugar.");

         // Clear FamilyUnitId for all linked users
         await repository.ClearAllUserFamilyUnitLinksAsync(familyUnitId, ct);

         // Hard delete (cascade deletes members via EF config)
         await repository.DeleteFamilyUnitAsync(familyUnitId, ct);

         logger.LogInformation(
             "Admin deleted family unit {FamilyUnitId} ({FamilyName})",
             familyUnitId, familyUnit.Name);
     }
     ```
  2. **`UpdateFamilyUnitStatusAsync`**:
     ```csharp
     public async Task<FamilyUnitResponse> UpdateFamilyUnitStatusAsync(
         Guid familyUnitId, UpdateFamilyUnitStatusRequest request, CancellationToken ct)
     {
         var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
             ?? throw new NotFoundException("Unidad Familiar", familyUnitId);

         await repository.UpdateFamilyUnitStatusAsync(familyUnitId, request.IsActive, ct);

         logger.LogInformation(
             "Family unit {FamilyUnitId} ({FamilyName}) status changed to IsActive={IsActive}",
             familyUnitId, familyUnit.Name, request.IsActive);

         // Reload to return updated state
         var updated = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct);
         return updated!.ToResponse();
     }
     ```
  3. **Modificar `DeleteFamilyMemberAsync`**: Agregar parametro `isAdminOrBoard` para permitir que Admin/Board eliminen miembros de cualquier familia:
     ```csharp
     public async Task DeleteFamilyMemberAsync(
         Guid memberId, bool isAdminOrBoard, CancellationToken ct)
     {
         var member = await repository.GetFamilyMemberByIdAsync(memberId, ct)
             ?? throw new NotFoundException("Miembro Familiar", memberId);

         // Check if trying to delete representative's own member record
         var familyUnit = await repository.GetFamilyUnitByIdAsync(member.FamilyUnitId, ct);
         if (familyUnit != null && member.UserId == familyUnit.RepresentativeUserId)
             throw new BusinessRuleException(
                 "No se puede eliminar al representante de la unidad familiar.");

         // Admin/Board: check for active registrations
         if (isAdminOrBoard)
         {
             var hasActiveRegs = await repository.MemberHasActiveRegistrationsAsync(memberId, ct);
             if (hasActiveRegs)
                 throw new ConflictException(
                     "No se puede eliminar un miembro con inscripciones activas (Pendiente/Confirmada).");
         }

         await repository.DeleteFamilyMemberAsync(memberId, ct);

         logger.LogInformation(
             "Deleted family member {MemberId} ({FirstName} {LastName}) from family unit {FamilyUnitId}",
             memberId, member.FirstName, member.LastName, member.FamilyUnitId);
     }
     ```
     **Nota**: Revisar la firma actual de `DeleteFamilyMemberAsync` y adaptar. Si actualmente no recibe `isAdminOrBoard`, agregar el parametro. Si el check de representante ya existe, reutilizarlo.

---

### Step 7: Update Endpoints — Nuevos endpoints admin + modificar auth

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs`
- **Action**: Agregar endpoints al `adminGroup` y modificar auth en `DeleteFamilyMember`
- **Implementation Steps**:
  1. **Agregar al `adminGroup`** (ya requiere rol Admin/Board):

     **DELETE `/api/family-units/{id:guid}/admin`** — Admin delete:
     ```csharp
     adminGroup.MapDelete("/{id:guid}/admin", async (
         Guid id,
         FamilyUnitsService service,
         CancellationToken ct) =>
     {
         await service.AdminDeleteFamilyUnitAsync(id, ct);
         return TypedResults.NoContent();
     })
     .WithName("AdminDeleteFamilyUnit")
     .Produces(StatusCodes.Status204NoContent)
     .Produces<ApiResponse>(StatusCodes.Status404NotFound)
     .Produces<ApiResponse>(StatusCodes.Status409Conflict);
     ```

     **PATCH `/api/family-units/{id:guid}/status`** — Toggle IsActive:
     ```csharp
     adminGroup.MapPatch("/{id:guid}/status", async (
         Guid id,
         UpdateFamilyUnitStatusRequest request,
         FamilyUnitsService service,
         CancellationToken ct) =>
     {
         var response = await service.UpdateFamilyUnitStatusAsync(id, request, ct);
         return TypedResults.Ok(ApiResponse<FamilyUnitResponse>.Success(response));
     })
     .WithName("UpdateFamilyUnitStatus")
     .Produces<ApiResponse<FamilyUnitResponse>>(StatusCodes.Status200OK)
     .Produces<ApiResponse>(StatusCodes.Status404NotFound);
     ```

  2. **Modificar `DeleteFamilyMember`** para aceptar Admin/Board:
     - Localizar el endpoint `DELETE /{familyUnitId:guid}/members/{memberId:guid}`
     - Cambiar la logica de autorizacion:
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
     - Pasar `isAdminOrBoard` al service para la validacion de registrations activas:
     ```csharp
     await service.DeleteFamilyMemberAsync(memberId, isAdminOrBoard, ct);
     ```

---

### Step 8: Update Memberships Repository — Nuevo metodo

- **File**: `src/Abuvi.API/Features/Memberships/MembershipsRepository.cs`
- **Action**: Agregar metodo para verificar cuota pagada por familia
- **Implementation Steps**:
  1. Agregar al interface `IMembershipsRepository`:
     ```csharp
     Task<bool> HasPaidCurrentYearFeeForFamilyAsync(Guid familyUnitId, CancellationToken ct);
     ```
  2. Implementar:
     ```csharp
     public async Task<bool> HasPaidCurrentYearFeeForFamilyAsync(
         Guid familyUnitId, CancellationToken ct)
     {
         var currentYear = DateTime.UtcNow.Year;
         return await db.MembershipFees
             .AnyAsync(f => f.Membership.FamilyMember.FamilyUnitId == familyUnitId
                 && f.Year == currentYear
                 && f.Status == FeeStatus.Paid
                 && f.Membership.IsActive, ct);
     }
     ```
  3. **Verificar navegacion**: Confirmar que la cadena `MembershipFee → Membership → FamilyMember → FamilyUnitId` es navegable via EF Core (las FKs y relaciones existen en las configuraciones)

---

### Step 9: Update Registrations Service — Doble validacion

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Agregar validaciones de `IsActive` y cuota pagada al crear registration
- **Implementation Steps**:
  1. **Inyectar `IMembershipsRepository`** en el constructor:
     ```csharp
     public class RegistrationsService(
         IRegistrationsRepository registrationsRepo,
         IRegistrationExtrasRepository extrasRepo,
         IRegistrationAccommodationPreferencesRepository accommodationPrefsRepo,
         IFamilyUnitsRepository familyUnitsRepo,
         ICampEditionsRepository campEditionsRepo,
         ICampEditionAccommodationsRepository accommodationsRepo,
         RegistrationPricingService pricingService,
         IEmailService emailService,
         Payments.IPaymentsService paymentsService,
         IMembershipsRepository membershipsRepo,           // NUEVO
         ILogger<RegistrationsService> logger)
     ```
  2. **Agregar validaciones** en `CreateAsync`, despues de cargar la FamilyUnit (paso 1) y antes de verificar representante (paso 2):
     ```csharp
     // 1. Load FamilyUnit
     var familyUnit = await familyUnitsRepo.GetFamilyUnitByIdAsync(request.FamilyUnitId, ct)
         ?? throw new NotFoundException("Unidad Familiar", request.FamilyUnitId);

     // 1b. Validate family unit is active (NEW)
     if (!familyUnit.IsActive)
         throw new BusinessRuleException(
             "La unidad familiar está desactivada. Contacte al administrador.");

     // 1c. Validate current year membership fee is paid (NEW)
     var hasPaidCurrentYearFee = await membershipsRepo
         .HasPaidCurrentYearFeeForFamilyAsync(request.FamilyUnitId, ct);
     if (!hasPaidCurrentYearFee)
         throw new BusinessRuleException(
             "La cuota de membresía del año en curso no está pagada. Contacte al administrador.");

     // 2. Verify representative (existing)
     ```
  3. **Registrar en DI**: Si `IMembershipsRepository` no esta ya registrado en `Program.cs`, agregarlo. Verificar — probablemente ya esta registrado por la feature de Memberships.

---

### Step 10: Create EF Core Migration

- **Action**: Generar migracion para el nuevo campo `is_active`
- **Implementation Steps**:
  1. Ejecutar desde la raiz del proyecto:
     ```bash
     dotnet ef migrations add AddIsActiveToFamilyUnits \
       --project src/Abuvi.API \
       --startup-project src/Abuvi.API
     ```
  2. Verificar la migracion generada:
     - Debe incluir `ALTER TABLE family_units ADD COLUMN is_active boolean NOT NULL DEFAULT true`
     - Todas las familias existentes deben tener `IsActive = true` por default
  3. Aplicar la migracion:
     ```bash
     dotnet ef database update \
       --project src/Abuvi.API \
       --startup-project src/Abuvi.API
     ```

---

### Step 11: Update Admin List Endpoint — Filtro por IsActive

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs` y `FamilyUnitsRepository.cs`
- **Action**: Agregar filtro opcional `?isActive=true/false` al endpoint admin GET
- **Implementation Steps**:
  1. Agregar parametro opcional al endpoint `GET /` del `adminGroup`:
     ```csharp
     [FromQuery] bool? isActive = null
     ```
  2. Pasar al service y repository
  3. En el repository `GetAllPagedAsync`, agregar filtro condicional:
     ```csharp
     if (isActive.HasValue)
         query = query.Where(fu => fu.IsActive == isActive.Value);
     ```
  4. Agregar `IsActive` a `FamilyUnitAdminProjection` y `FamilyUnitListItemResponse` si no estan ya

---

### Step 12: Write Unit Tests

- **Action**: Crear tests unitarios para todas las funcionalidades nuevas
- **Implementation Steps**:

  #### 12a: `FamilyUnitsService_AdminDeleteAsync_Tests.cs`
  - **File**: `tests/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsService_AdminDeleteAsync_Tests.cs`
  - **Test Cases**:
    1. `AdminDeleteFamilyUnitAsync_WithoutRegistrations_DeletesSuccessfully` — happy path, verifica cascade
    2. `AdminDeleteFamilyUnitAsync_WithRegistrations_ThrowsConflictException` — familia con inscripciones, retorna 409
    3. `AdminDeleteFamilyUnitAsync_NotFound_ThrowsNotFoundException` — ID inexistente, retorna 404
    4. `AdminDeleteFamilyUnitAsync_ClearsUserFamilyUnitLinks` — verifica que se llama `ClearAllUserFamilyUnitLinksAsync`

  #### 12b: `FamilyUnitsService_UpdateStatusAsync_Tests.cs`
  - **File**: `tests/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsService_UpdateStatusAsync_Tests.cs`
  - **Test Cases**:
    1. `UpdateFamilyUnitStatusAsync_Deactivate_SetsIsActiveFalse` — desactivar
    2. `UpdateFamilyUnitStatusAsync_Reactivate_SetsIsActiveTrue` — reactivar
    3. `UpdateFamilyUnitStatusAsync_NotFound_ThrowsNotFoundException` — ID inexistente

  #### 12c: `FamilyUnitsService_DeleteMemberAsync_Tests.cs`
  - **File**: `tests/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsService_DeleteMemberAsync_Tests.cs`
  - **Test Cases**:
    1. `DeleteFamilyMemberAsync_AsAdminOrBoard_DeletesSuccessfully` — admin puede eliminar cualquier miembro
    2. `DeleteFamilyMemberAsync_AsAdminOrBoard_Representative_ThrowsBusinessRuleException` — no puede eliminar representante
    3. `DeleteFamilyMemberAsync_AsAdminOrBoard_WithActiveRegistrations_ThrowsConflictException` — miembro con inscripciones activas
    4. `DeleteFamilyMemberAsync_AsAdminOrBoard_WithDraftRegistrations_DeletesSuccessfully` — miembro con solo inscripciones Draft/Cancelled se puede eliminar

  #### 12d: `RegistrationsService_CreateAsync_MembershipValidation_Tests.cs`
  - **File**: `tests/Abuvi.Tests/Unit/Features/Registrations/RegistrationsService_CreateAsync_MembershipValidation_Tests.cs`
  - **Test Cases**:
    1. `CreateAsync_FamilyUnitInactive_ThrowsBusinessRuleException` — familia desactivada bloquea inscripcion
    2. `CreateAsync_NoCurrentYearFeePaid_ThrowsBusinessRuleException` — cuota no pagada bloquea inscripcion
    3. `CreateAsync_ActiveFamilyWithPaidFee_Succeeds` — ambos controles pasan, inscripcion exitosa
    4. `CreateAsync_InactiveFamilyWithPaidFee_ThrowsBusinessRuleException` — cuota pagada pero familia inactiva, bloquea
    5. `CreateAsync_ActiveFamilyWithUnpaidFee_ThrowsBusinessRuleException` — familia activa pero cuota no pagada, bloquea

  - **Patron de tests**: Usar AAA (Arrange-Act-Assert), NSubstitute para mocks, FluentAssertions para asserts
  - **Naming**: `MethodName_StateUnderTest_ExpectedBehavior`

---

### Step 13: Update Technical Documentation

- **Action**: Actualizar documentacion tecnica
- **Implementation Steps**:
  1. **`ai-specs/specs/data-model.md`**: Agregar campo `IsActive` a la seccion de `FamilyUnit`, documentar la relacion con el control de acceso a inscripciones
  2. **`ai-specs/specs/api-spec.yml`**: Agregar los nuevos endpoints (`DELETE /admin`, `PATCH /status`), actualizar `FamilyUnitResponse` schema con `isActive`, documentar el parametro `?isActive` en el GET admin
  3. **Verificar OpenAPI auto-generado**: Confirmar que la documentacion Swagger refleja los cambios
  4. Toda documentacion en ingles

---

## Implementation Order

1. **Step 0**: Create Feature Branch
2. **Step 1**: Update Entity — `FamilyUnit.IsActive`
3. **Step 2**: Update EF Core Configuration
4. **Step 3**: Update DTOs
5. **Step 4**: Create Validator (si aplica)
6. **Step 5**: Update Repository — nuevos metodos en `FamilyUnitsRepository`
7. **Step 6**: Update Service — nuevos metodos de negocio
8. **Step 7**: Update Endpoints — admin delete, status toggle, auth en delete member
9. **Step 8**: Update Memberships Repository — `HasPaidCurrentYearFeeForFamilyAsync`
10. **Step 9**: Update Registrations Service — doble validacion
11. **Step 10**: Create EF Core Migration
12. **Step 11**: Update Admin List — filtro por `IsActive`
13. **Step 12**: Write Unit Tests
14. **Step 13**: Update Technical Documentation

---

## Testing Checklist

- [ ] Tests para `AdminDeleteFamilyUnitAsync`: happy path, con registrations (409), not found (404), limpia links de users
- [ ] Tests para `UpdateFamilyUnitStatusAsync`: desactivar, reactivar, not found
- [ ] Tests para `DeleteFamilyMemberAsync` con Admin/Board: happy path, representante (409), registrations activas (409), solo Draft/Cancelled OK
- [ ] Tests para validacion de inscripciones: familia inactiva, cuota no pagada, ambos OK, combinaciones mixtas
- [ ] Compilacion exitosa sin warnings
- [ ] Migracion EF Core genera SQL correcto
- [ ] `dotnet test` pasa al 100%

---

## Error Response Format

Todos los errores siguen el envelope `ApiResponse<T>`:

| Escenario | HTTP Status | Exception | Mensaje |
|---|---|---|---|
| Unidad no encontrada | 404 | `NotFoundException` | "Unidad Familiar not found" |
| Eliminar unidad con inscripciones | 409 | `ConflictException` | "No se puede eliminar una unidad familiar con inscripciones. Desactivela en su lugar." |
| Eliminar representante | 409 | `BusinessRuleException` | "No se puede eliminar al representante de la unidad familiar." |
| Eliminar miembro con registrations activas | 409 | `ConflictException` | "No se puede eliminar un miembro con inscripciones activas (Pendiente/Confirmada)." |
| Inscripcion con familia inactiva | 409 | `BusinessRuleException` | "La unidad familiar esta desactivada. Contacte al administrador." |
| Inscripcion sin cuota pagada | 409 | `BusinessRuleException` | "La cuota de membresia del año en curso no esta pagada. Contacte al administrador." |

---

## Dependencies

- **NuGet packages**: No se requieren nuevos paquetes. Todo usa EF Core, FluentValidation, y librerias ya existentes.
- **EF Core Migration**:
  ```bash
  dotnet ef migrations add AddIsActiveToFamilyUnits --project src/Abuvi.API --startup-project src/Abuvi.API
  dotnet ef database update --project src/Abuvi.API --startup-project src/Abuvi.API
  ```

---

## Notes

- **Seguridad**: Los endpoints `DELETE /admin` y `PATCH /status` van en el `adminGroup` que ya tiene `RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))`. No se necesita configuracion adicional de auth.
- **Auditoria**: Usar `logger.LogInformation` en todos los metodos destructivos y cambios de estado, incluyendo el ID y nombre de la entidad afectada.
- **Cascade en delete**: EF Core ya tiene configurado cascade delete de `FamilyUnit → FamilyMembers`. Solo necesitamos limpiar `User.FamilyUnitId` manualmente via `ClearAllUserFamilyUnitLinksAsync`.
- **Consistencia**: Las validaciones en `AdminDeleteFamilyUnitAsync` (check registrations → delete) deben ejecutarse en secuencia dentro de la misma transaccion. EF Core `SaveChangesAsync` es atomico por defecto para operaciones en el mismo `DbContext`.
- **Doble control**: `IsActive` y cuota pagada son controles independientes. El primero es un toggle administrativo, el segundo se gestiona via el flujo de membresias existente. No se acoplan.
- **RGPD**: No hay datos sensibles nuevos en esta feature. Los miembros eliminados (hard delete) se borran completamente incluyendo datos cifrados.
- **Idioma**: Mensajes de error en español (consistente con el resto de la aplicacion). Documentacion tecnica en ingles.

---

## Next Steps After Implementation

1. Crear PR hacia `dev` con los cambios backend
2. Implementar la parte frontend (ticket separado: `feat-admin-delete-family-units_frontend`)
3. Testing de integracion E2E cuando ambas partes esten listas

---

## Implementation Verification

- [ ] **Code Quality**: Sin warnings de C# analyzers, nullable reference types correctos
- [ ] **Functionality**: Endpoints retornan status codes correctos (204, 200, 404, 409)
- [ ] **Testing**: Tests unitarios cubren happy paths, validaciones, edge cases
- [ ] **Integration**: Migracion EF Core aplicada sin errores
- [ ] **Documentation**: `data-model.md` y `api-spec.yml` actualizados
- [ ] **Security**: Endpoints nuevos protegidos con Admin/Board role
- [ ] **Audit**: Logging en todas las acciones destructivas

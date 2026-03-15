# Backend Implementation Plan: fix-duplicate-family-member-email — Prevenir emails duplicados en miembros de familia

## Overview

Implementar validación de negocio para impedir que un miembro de familia tenga el mismo email que otro miembro de su misma unidad familiar o que el usuario representante. Esto evita bloqueos de registro futuros y auto-linkings incorrectos. Sigue Vertical Slice Architecture: todos los cambios se concentran en `src/Abuvi.API/Features/FamilyUnits/`.

No requiere cambios de esquema ni migraciones — la validación es de lógica de negocio, no de constraint de base de datos (los emails pueden repetirse legítimamente entre familias distintas).

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/FamilyUnits/`
- **Files to modify**:
  - `FamilyUnitsService.cs` — Añadir validación de email duplicado en `CreateFamilyMemberAsync` y `UpdateFamilyMemberAsync`
  - `FamilyUnitsRepository.cs` — Añadir método para verificar email duplicado dentro de una familia
- **Files to create**:
  - Nuevos tests en `tests/Abuvi.API.Tests/Features/FamilyUnits/FamilyUnitsServiceTests.cs`
- **Cross-cutting concerns**: Ninguno. Se reutiliza `BusinessRuleException` existente.

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Crear y cambiar a la rama de backend para esta feature
- **Branch Naming**: `feature/fix-duplicate-family-member-email-backend`
- **Implementation Steps**:
  1. Asegurarse de estar en `dev` (rama base del proyecto)
  2. `git pull origin dev`
  3. `git checkout -b feature/fix-duplicate-family-member-email-backend`
  4. `git branch` para verificar

### Step 1: Add Repository Method for Email Duplicate Check

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs`
- **Action**: Añadir método para comprobar si ya existe un miembro con el mismo email dentro de una familia
- **Interface Addition** (en `IFamilyUnitsRepository`):
  ```csharp
  Task<bool> IsFamilyMemberEmailTakenAsync(Guid familyUnitId, string email, Guid? excludeMemberId, CancellationToken ct);
  ```
- **Implementation** (en `FamilyUnitsRepository`):
  ```csharp
  public async Task<bool> IsFamilyMemberEmailTakenAsync(
      Guid familyUnitId, string email, Guid? excludeMemberId, CancellationToken ct)
  {
      var query = _context.FamilyMembers
          .AsNoTracking()
          .Where(fm => fm.FamilyUnitId == familyUnitId
              && fm.Email != null
              && fm.Email.ToLower() == email.ToLower());

      if (excludeMemberId.HasValue)
          query = query.Where(fm => fm.Id != excludeMemberId.Value);

      return await query.AnyAsync(ct);
  }
  ```
- **Implementation Steps**:
  1. Añadir la firma del método a la interfaz `IFamilyUnitsRepository`
  2. Implementar en `FamilyUnitsRepository` con query case-insensitive vía `ToLower()` (PostgreSQL lo traduce a `LOWER()`)
  3. Parámetro `excludeMemberId` permite excluir al miembro en edición durante Update
- **Dependencies**: Ninguna nueva. Usa `_context.FamilyMembers` existente.
- **Implementation Notes**:
  - Se usa `ToLower()` en lugar de `StringComparison.OrdinalIgnoreCase` porque EF Core necesita traducirlo a SQL
  - `AsNoTracking()` porque es solo lectura
  - La query es eficiente: filtra por `familyUnitId` (FK indexada) + comparación de string

### Step 2: Add Email Validation Logic in Service

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`
- **Action**: Añadir validación de email duplicado en `CreateFamilyMemberAsync` y `UpdateFamilyMemberAsync`

#### Step 2a: Create Private Helper Method

- **Function Signature**:
  ```csharp
  private async Task ValidateFamilyMemberEmailAsync(
      Guid familyUnitId, string? email, Guid? excludeMemberId, CancellationToken ct)
  ```
- **Implementation Steps**:
  1. Si `email` es null o vacío, retornar inmediatamente (sin validación)
  2. Obtener la family unit para acceder a `RepresentativeUserId`
  3. Obtener el User representante con `repository.GetUserByIdAsync(familyUnit.RepresentativeUserId, ct)`
  4. Si el email coincide (case-insensitive) con el del representante → lanzar `BusinessRuleException("El correo electrónico no puede ser el mismo que el del representante de la familia")`
  5. Llamar a `repository.IsFamilyMemberEmailTakenAsync(familyUnitId, email, excludeMemberId, ct)`
  6. Si retorna `true` → lanzar `BusinessRuleException("Ya existe otro miembro en esta familia con el mismo correo electrónico")`
- **Implementation Notes**:
  - La family unit ya se obtiene en los métodos caller (Create/Update). Para evitar una segunda query, se puede extraer `RepresentativeUserId` del familyUnit que ya se cargó en el caller, y pasar los datos necesarios al helper, o bien el helper puede recibir el `representativeEmail` directamente. **Enfoque recomendado**: El helper recibe `representativeEmail` como parámetro adicional para evitar queries redundantes:
  ```csharp
  private async Task ValidateFamilyMemberEmailAsync(
      Guid familyUnitId, string? email, string? representativeEmail,
      Guid? excludeMemberId, CancellationToken ct)
  ```

#### Step 2b: Integrate in `CreateFamilyMemberAsync`

- **Location**: Después de obtener la family unit y antes de crear el miembro (antes de la línea que crea el entity `FamilyMember`)
- **Implementation Steps**:
  1. Después de verificar que la family unit existe, obtener el user representante (ya se hace implícitamente en algunos flows, pero aquí se necesita el email)
  2. Obtener el email del representante: El `RepresentativeUserId` está en la family unit → usar `repository.GetUserByIdAsync()` (ya existe) para obtener el User y su email
  3. Llamar a `ValidateFamilyMemberEmailAsync(familyUnitId, request.Email, representativeUser?.Email, null, ct)`
  4. El `excludeMemberId` es `null` porque es un Create (no hay miembro previo que excluir)
- **Implementation Notes**:
  - La family unit ya se obtiene en `CreateFamilyMemberAsync` para verificar que existe. Solo falta obtener el user representante para su email
  - Si el representante no tiene user (caso teórico), se pasa `null` como representativeEmail y se salta esa comparación

#### Step 2c: Integrate in `UpdateFamilyMemberAsync`

- **Location**: Después de obtener el miembro existente y antes de aplicar los cambios
- **Implementation Steps**:
  1. Obtener la family unit del miembro: `repository.GetFamilyUnitByIdAsync(existingMember.FamilyUnitId, ct)` — o bien usar el `familyUnitId` del endpoint
  2. Obtener el email del representante como en Step 2b
  3. Llamar a `ValidateFamilyMemberEmailAsync(familyUnitId, request.Email, representativeUser?.Email, memberId, ct)`
  4. El `excludeMemberId` es `memberId` para excluir al propio miembro de la comparación de duplicados
- **Implementation Notes**:
  - En `UpdateFamilyMemberAsync` el member se obtiene por `memberId`, y de ahí se puede obtener `FamilyUnitId`
  - Se debe validar ANTES de aplicar cambios al entity para evitar estado inconsistente

### Step 3: Write Unit Tests

- **File**: `tests/Abuvi.API.Tests/Features/FamilyUnits/FamilyUnitsServiceTests.cs`
- **Action**: Añadir tests para las nuevas validaciones de email duplicado

#### Tests para `CreateFamilyMemberAsync`:

1. **`CreateFamilyMemberAsync_WithRepresentativeEmail_ThrowsBusinessRuleException`**
   - **Arrange**: Configurar mock de repository para que `GetUserByIdAsync` retorne un User con email "rep@test.com". Request con email "rep@test.com".
   - **Act**: Llamar a `CreateFamilyMemberAsync`
   - **Assert**: `Should().ThrowAsync<BusinessRuleException>().WithMessage("*representante*")`

2. **`CreateFamilyMemberAsync_WithRepresentativeEmailDifferentCase_ThrowsBusinessRuleException`**
   - **Arrange**: User con email "Rep@Test.com", request con email "rep@test.com"
   - **Assert**: Debe lanzar BusinessRuleException (case-insensitive)

3. **`CreateFamilyMemberAsync_WithDuplicateSiblingEmail_ThrowsBusinessRuleException`**
   - **Arrange**: `IsFamilyMemberEmailTakenAsync` retorna `true`
   - **Assert**: Debe lanzar BusinessRuleException con mensaje sobre duplicado

4. **`CreateFamilyMemberAsync_WithUniqueEmail_Succeeds`**
   - **Arrange**: Email no coincide con representante, `IsFamilyMemberEmailTakenAsync` retorna `false`
   - **Assert**: Miembro se crea correctamente

5. **`CreateFamilyMemberAsync_WithNullEmail_SkipsValidation`**
   - **Arrange**: Request con email `null`
   - **Assert**: `IsFamilyMemberEmailTakenAsync` NO se invoca (`DidNotReceive()`)

6. **`CreateFamilyMemberAsync_WithEmptyEmail_SkipsValidation`**
   - **Arrange**: Request con email `""`
   - **Assert**: `IsFamilyMemberEmailTakenAsync` NO se invoca

#### Tests para `UpdateFamilyMemberAsync`:

7. **`UpdateFamilyMemberAsync_WithRepresentativeEmail_ThrowsBusinessRuleException`**
   - Misma lógica que test 1 pero en contexto de Update

8. **`UpdateFamilyMemberAsync_WithDuplicateSiblingEmail_ThrowsBusinessRuleException`**
   - Misma lógica que test 3 pero en contexto de Update

9. **`UpdateFamilyMemberAsync_WithOwnExistingEmail_Succeeds`**
   - **Arrange**: `IsFamilyMemberEmailTakenAsync` con `excludeMemberId` retorna `false` (se excluye a sí mismo)
   - **Assert**: Miembro se actualiza correctamente. Verificar que `IsFamilyMemberEmailTakenAsync` se llamó con el `memberId` correcto como `excludeMemberId`.

10. **`UpdateFamilyMemberAsync_WithNullEmail_SkipsValidation`**
    - Misma lógica que test 5 pero en contexto de Update

#### Tests para Repository Method:

11. **`IsFamilyMemberEmailTakenAsync_WhenEmailExists_ReturnsTrue`**
    - Integration test o test con InMemory DB si el proyecto lo usa

12. **`IsFamilyMemberEmailTakenAsync_WhenEmailExistsButExcluded_ReturnsFalse`**
    - Verifica que `excludeMemberId` funciona

13. **`IsFamilyMemberEmailTakenAsync_WhenNoMatch_ReturnsFalse`**

- **Implementation Notes**:
  - Seguir el patrón existente en `FamilyUnitsServiceTests.cs`: NSubstitute mocks, FluentAssertions
  - Naming: `MethodName_StateUnderTest_ExpectedBehavior`
  - AAA pattern: Arrange-Act-Assert

### Step 4: Verify Build and Tests

- **Action**: Compilar y ejecutar tests
- **Implementation Steps**:
  1. `dotnet build` — Verificar que no hay errores de compilación
  2. `dotnet test` — Verificar que todos los tests pasan (nuevos y existentes)
  3. Verificar que no hay warnings de nullable reference types

### Step 5: Update Technical Documentation

- **Action**: Actualizar documentación técnica afectada
- **Implementation Steps**:
  1. **Review Changes**: Analizar los cambios de código realizados
  2. **Update `ai-specs/specs/data-model.md`**:
     - En la sección de `FamilyMember`, añadir regla de validación: "Email must be unique within the same FamilyUnit (case-insensitive). Email cannot match the representative User's email."
  3. **Verify Documentation**: Confirmar que los cambios están reflejados correctamente
- **References**: Seguir proceso de `ai-specs/specs/documentation-standards.mdc`
- **Notes**: Documentación en inglés como requiere el estándar

## Implementation Order

1. **Step 0**: Create Feature Branch
2. **Step 1**: Add Repository Method (`IsFamilyMemberEmailTakenAsync`)
3. **Step 2a**: Create Private Helper Method (`ValidateFamilyMemberEmailAsync`)
4. **Step 2b**: Integrate in `CreateFamilyMemberAsync`
5. **Step 2c**: Integrate in `UpdateFamilyMemberAsync`
6. **Step 3**: Write Unit Tests
7. **Step 4**: Verify Build and Tests
8. **Step 5**: Update Technical Documentation

## Testing Checklist

- [ ] `CreateFamilyMemberAsync` rechaza email igual al del representante
- [ ] `CreateFamilyMemberAsync` rechaza email igual al de otro miembro de la familia
- [ ] `CreateFamilyMemberAsync` acepta email único
- [ ] `CreateFamilyMemberAsync` salta validación si email es null/vacío
- [ ] `UpdateFamilyMemberAsync` rechaza email igual al del representante
- [ ] `UpdateFamilyMemberAsync` rechaza email igual al de otro miembro (distinto al editado)
- [ ] `UpdateFamilyMemberAsync` acepta si el email es el mismo que ya tenía el miembro
- [ ] `UpdateFamilyMemberAsync` salta validación si email es null/vacío
- [ ] Comparación case-insensitive funciona correctamente
- [ ] `IsFamilyMemberEmailTakenAsync` respeta `excludeMemberId`
- [ ] Tests existentes siguen pasando (no hay regresiones)
- [ ] `dotnet build` sin warnings
- [ ] Coverage ≥ 90% en código modificado

## Error Response Format

Las validaciones de email duplicado usan `BusinessRuleException`, que se mapea en `FamilyUnitsEndpoints.cs` a:

| Escenario | HTTP Status | Mensaje |
|-----------|-------------|---------|
| Email = representante | 409 Conflict | `"El correo electrónico no puede ser el mismo que el del representante de la familia"` |
| Email = otro miembro | 409 Conflict | `"Ya existe otro miembro en esta familia con el mismo correo electrónico"` |

**Nota**: En los endpoints de FamilyUnits, `BusinessRuleException` se atrapa y devuelve 409 Conflict con `ApiResponse<object>.Fail(message)`. Esto es consistente con el manejo existente (ej: "Ya tienes una unidad familiar", "El número de familia ya está en uso").

## Dependencies

- **NuGet packages**: Ninguno nuevo
- **EF Core migrations**: No requerida (sin cambios de esquema)

## Notes

- **No se añade UNIQUE constraint en BD**: Los emails de FamilyMember pueden repetirse entre familias distintas (caso legítimo). La unicidad es por scope de familia + representante.
- **Compatibilidad con datos existentes**: Los duplicados existentes NO se corrigen automáticamente. Solo se validan al crear/editar. Esto es intencional para no romper datos en producción.
- **Idioma de mensajes de error**: En español, consistente con los mensajes existentes en el proyecto.
- **Performance**: La query `IsFamilyMemberEmailTakenAsync` es O(n) donde n = miembros de la familia (típicamente <10). No requiere índice adicional.
- **Case-insensitive**: Se usa `ToLower()` en EF Core que se traduce a `LOWER()` en PostgreSQL. Alternativa: si la collation de la BD ya es case-insensitive, el `ToLower()` es redundante pero no dañino.

## Next Steps After Implementation

1. Implementar la parte frontend (ver `fix-duplicate-family-member-email_enriched.md` sección Frontend)
2. Testing manual end-to-end: crear miembro con email del representante → verificar error 409
3. Verificar que el frontend muestra el mensaje de error correctamente
4. Considerar script de limpieza de datos para duplicados existentes (opcional, bajo demanda)

## Implementation Verification

- [ ] **Code Quality**: Sin warnings de C# analyzers, nullable reference types habilitados
- [ ] **Functionality**: POST y PUT de family members devuelven 409 con mensaje correcto cuando email duplicado
- [ ] **Testing**: ≥ 90% coverage con xUnit + FluentAssertions + NSubstitute
- [ ] **Integration**: No hay migraciones, no hay riesgo de integración
- [ ] **Documentation**: `data-model.md` actualizado con la nueva regla de validación

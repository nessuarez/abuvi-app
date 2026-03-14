# Fix: Prevenir emails duplicados en miembros de familia

## Problema

Cuando un usuario representante edita los miembros de su unidad familiar, puede introducir **su propio email** como email de un familiar. Esto causa:

1. **Bloqueo de registro**: Si el familiar intenta registrarse en la plataforma con ese email, el sistema rechaza el registro porque `Users.email` tiene restricción `UNIQUE`. El familiar no puede crear cuenta.
2. **Auto-linking incorrecto**: El sistema auto-enlaza al familiar con la cuenta del representante (vía `FamilyUnitsService.CreateFamilyMemberAsync` líneas 170-182), lo cual es semánticamente incorrecto.
3. **Confusión futura**: Múltiples miembros de la misma familia pueden tener el mismo email, generando ambigüedad en notificaciones y flujos de auto-linking.

## Solución propuesta

### Regla de negocio

> El email de un miembro familiar no puede ser igual al email de otro miembro de la misma unidad familiar, ni al email del usuario representante de esa unidad familiar.

El email sigue siendo **opcional**. Múltiples miembros pueden tenerlo vacío/null sin conflicto. La validación solo aplica cuando se proporciona un email no vacío.

### Cambios UX

1. **Validación en tiempo real en el frontend**: Al escribir/salir del campo email de un familiar, comprobar que no coincide con:
   - El email del usuario logueado (representante)
   - El email de cualquier otro miembro de la misma unidad familiar ya guardado o en edición
2. **Mensaje de error inline**: "Este correo ya está en uso por otro miembro de tu familia o por tu propia cuenta."
3. **Texto de ayuda mejorado** bajo el campo email: Aclarar que el campo es opcional y su propósito. Sugerencia:
   > "Opcional. Indica el correo **personal** de este familiar solo si deseas que pueda registrarse en la plataforma con su propia cuenta. Si no tiene correo propio, déjalo en blanco."

---

## Implementación detallada

### Frontend

#### Archivo: `frontend/src/components/family-units/FamilyMemberForm.vue`

**Cambios:**

1. **Recibir prop del email del representante** (el usuario logueado):
   - Añadir prop `representativeEmail: string` al componente
   - Añadir prop `siblingEmails: string[]` — lista de emails de los otros miembros de la misma familia (excluyendo el miembro actual)

2. **Extender `validateEmail()`** para incluir validación de duplicados:
   ```
   - Si email === representativeEmail → error: "No puedes usar tu propio correo para un familiar"
   - Si email está en siblingEmails (case-insensitive) → error: "Este correo ya está asignado a otro miembro de tu familia"
   ```

3. **Actualizar hint text** del campo email:
   - Cambiar el texto actual por: "Opcional. Indica el correo personal de este familiar solo si deseas que pueda registrarse en la plataforma con su propia cuenta. Si no tiene correo propio, déjalo en blanco."

4. **Bloquear submit** si hay error de email duplicado (el botón guardar no se debe habilitar si hay errores de validación).

#### Archivo: Componente padre que renderiza `FamilyMemberForm.vue`

- Pasar `representativeEmail` desde el store/auth del usuario logueado.
- Calcular `siblingEmails` como array de emails de los demás miembros de la familia (excluyendo el actual en edición).

### Backend

#### Archivo: `src/Abuvi.API/Features/FamilyUnits/CreateFamilyMemberValidator.cs`

**Añadir regla de validación** (requiere inyectar contexto de la familia):

No se puede validar uniqueness solo con FluentValidation porque necesita acceso a datos. La validación se hará en el **servicio**.

#### Archivo: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs`

**En `CreateFamilyMemberAsync`** (antes de crear el miembro):

1. Si `request.Email` no es null/vacío:
   - Obtener el representante de la familia (`FamilyUnit.RepresentativeUserId` → `User.Email`)
   - Si `request.Email` coincide (case-insensitive) con el email del representante → lanzar `BusinessRuleException("El correo electrónico no puede ser el mismo que el del representante de la familia")`
   - Obtener todos los miembros existentes de la familia
   - Si algún miembro ya tiene ese email (case-insensitive) → lanzar `BusinessRuleException("Ya existe otro miembro en esta familia con el mismo correo electrónico")`

**En `UpdateFamilyMemberAsync`** (antes de actualizar):

1. Misma lógica, pero excluir al miembro que se está editando de la comparación de duplicados.

#### Archivo: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs` (si es necesario)

- Añadir método `GetMembersByFamilyUnitIdAsync(Guid familyUnitId, CancellationToken ct)` si no existe ya, para obtener todos los miembros de una familia.
- Método para obtener el email del representante de la familia si no está disponible ya en el flujo.

### Endpoints afectados

| Método | Endpoint | Cambio |
|--------|----------|--------|
| POST | `/api/family-units/{familyUnitId}/members` | Validación de email duplicado antes de crear |
| PUT | `/api/family-units/{familyUnitId}/members/{memberId}` | Validación de email duplicado antes de actualizar |

### Respuestas de error esperadas

- **HTTP 400** con mensaje `"El correo electrónico no puede ser el mismo que el del representante de la familia"` cuando el email coincide con el del representante.
- **HTTP 400** con mensaje `"Ya existe otro miembro en esta familia con el mismo correo electrónico"` cuando el email ya existe en otro miembro de la misma familia.

---

## Archivos a modificar

| Archivo | Tipo de cambio |
|---------|---------------|
| `frontend/src/components/family-units/FamilyMemberForm.vue` | Validación frontend + texto de ayuda |
| Componente padre que usa `FamilyMemberForm` | Pasar props de emails |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs` | Validación backend en Create y Update |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs` | Query de miembros por familia (si no existe) |
| `ai-specs/specs/data-model.md` | Documentar regla de negocio |

## Criterios de aceptación

- [ ] Al crear un miembro familiar, si el email coincide con el del representante, se muestra error y no se permite guardar
- [ ] Al crear un miembro familiar, si el email coincide con el de otro miembro de la misma familia, se muestra error y no se permite guardar
- [ ] Al editar un miembro familiar, mismas validaciones (excluyendo al propio miembro de la comparación)
- [ ] La validación es case-insensitive (ej: "Test@Mail.com" = "test@mail.com")
- [ ] El campo email muestra hint actualizado que aclara que es opcional y para qué sirve
- [ ] Error de validación en frontend se muestra en tiempo real (on blur / on change)
- [ ] Error de validación en backend devuelve HTTP 400 con mensaje descriptivo en español
- [ ] Emails null/vacíos no generan conflicto (múltiples miembros pueden no tener email)
- [ ] Tests unitarios para las nuevas validaciones backend

## Requisitos no funcionales

- **Seguridad**: La validación DEBE existir en backend independientemente del frontend. El frontend es solo UX.
- **Performance**: La query de miembros existentes por familia es una consulta simple por `familyUnitId`, no requiere optimización especial.
- **Compatibilidad**: No se requiere migración de datos. Los duplicados existentes permanecen hasta que se editen manualmente.

## Notas de diseño

- **No se añade restricción UNIQUE a nivel de base de datos** en `FamilyMember.Email` global, porque emails legítimamente pueden repetirse entre familias diferentes (ej: una tía que aparece como miembro en dos unidades familiares distintas).
- La unicidad se valida **dentro del scope de la unidad familiar** + contra el email del representante, a nivel de lógica de negocio.
- Se mantiene la convención del proyecto de usar `BusinessRuleException` para errores de validación de negocio.
